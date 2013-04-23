// CiResolver.cs - Ci symbol resolver
//
// Copyright (C) 2011-2013  Piotr Fusik
//
// This file is part of CiTo, see http://cito.sourceforge.net
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

[Serializable]
public class ResolveException : Exception
{
	public ResolveException(string message) : base(message)
	{
	}

	public ResolveException(string format, params object[] args) : this(string.Format(format, args))
	{
	}
}

public class CiResolver : ICiSymbolVisitor, ICiTypeVisitor, ICiExprVisitor, ICiStatementVisitor
{
	public IEnumerable<string> SearchDirs = new string[0];
	readonly SortedDictionary<string, CiBinaryResource> BinaryResources = new SortedDictionary<string, CiBinaryResource>();
	SymbolTable Symbols;
	readonly HashSet<ICiPtrType> WritablePtrTypes = new HashSet<ICiPtrType>();
	readonly HashSet<CiMethod> ThrowingMethods = new HashSet<CiMethod>();
	public CiClass CurrentClass = null;
	public CiMethod CurrentMethod = null;
	CiLoop CurrentLoop = null;
	CiCondCompletionStatement CurrentLoopOrSwitch = null;

	public CiResolver()
	{
		this.WritablePtrTypes.Add(CiArrayPtrType.WritableByteArray);
	}

	string FindFile(string name)
	{
		foreach (string dir in this.SearchDirs) {
			string full = Path.Combine(dir, name);
			if (File.Exists(full))
				return full;
		}
		if (File.Exists(name))
			return name;
		throw new ResolveException("File {0} not found", name);
	}

	CiType ICiTypeVisitor.Visit(CiUnknownType type)
	{
		CiSymbol symbol = this.Symbols.Lookup(type.Name);
		if (symbol is CiType)
			return (CiType) symbol;
		if (symbol is CiClass)
			return new CiClassPtrType { Name = type.Name, Class = (CiClass) symbol};
		throw new ResolveException("{0} is not a type", type.Name);
	}

	CiType ICiTypeVisitor.Visit(CiStringStorageType type)
	{
		type.Length = (int) ResolveConstExpr(type.LengthExpr, CiIntType.Value);
		return type;
	}

	CiClass ResolveClass(CiClass klass)
	{
		if (klass is CiUnknownClass) {
			string name = klass.Name;
			klass = this.Symbols.Lookup(name) as CiClass;
			if (klass == null)
				throw new ResolveException("{0} is not a class", name);
		}
		return klass;
	}

	CiType ICiTypeVisitor.Visit(CiClassType type)
	{
		type.Class = ResolveClass(type.Class);
		return type;
	}

	CiType ICiTypeVisitor.Visit(CiArrayType type)
	{
		type.ElementType = Resolve(type.ElementType);
		return type;
	}

	CiType ICiTypeVisitor.Visit(CiArrayStorageType type)
	{
		type.ElementType = Resolve(type.ElementType);
		if (type.LengthExpr != null) {
			type.Length = (int) ResolveConstExpr(type.LengthExpr, CiIntType.Value);
			type.LengthExpr = null;
		}
		return type;
	}

	CiType Resolve(CiType type)
	{
		return type.Accept(this);
	}

	CiCondExpr Coerce(CiCondExpr expr, CiType expected)
	{
		return new CiCondExpr {
			Cond = expr.Cond,
			ResultType = expected,
			OnTrue = Coerce(expr.OnTrue, expected),
			OnFalse = Coerce(expr.OnFalse, expected)
		};
	}

	static bool Extends(CiType type, CiClass baseClass)
	{
		if (!(type is CiClassType))
			return false;
		CiClass klass = ((CiClassType) type).Class;
		while (klass != baseClass) {
			// TODO: resolve, make sure no loops
			klass = klass.BaseClass;
			if (klass == null)
				return false;
		}
		return true;
	}

	CiMaybeAssign Coerce(CiMaybeAssign expr, CiType expected)
	{
		CiType got = expr.Type;
		if (expected.Equals(got))
			return expr;
		if (expected == CiIntType.Value && got == CiByteType.Value) {
			CiConstExpr konst = expr as CiConstExpr;
			if (konst != null)
				return new CiConstExpr((object) (int) (byte) konst.Value);
			CiCondExpr cond = expr as CiCondExpr;
			if (cond != null && (cond.OnTrue is CiConstExpr || cond.OnFalse is CiConstExpr)) {
				// avoid ((foo ? 1 : 0) & 0xff) in Java
				return Coerce(cond, expected);
			}
			if (expr is CiArrayAccess) {
				CiConstAccess ca = ((CiArrayAccess) expr).Array as CiConstAccess;
				if (ca != null && ca.Const.Is7Bit)
					return expr;
			}
			return new CiCoercion { ResultType = expected, Inner = expr };
		}
		if (expected == CiByteType.Value && got == CiIntType.Value) {
			CiConstExpr konst = expr as CiConstExpr;
			if (konst != null)
				return new CiConstExpr((object) (byte) (int) konst.Value);
			return new CiCoercion { ResultType = expected, Inner = expr };
		}
		if (expected == CiStringPtrType.Value && (got == CiType.Null || got is CiStringType))
			return expr;
		if (expected is CiStringStorageType && got is CiStringType)
			return expr;
		if (expected is CiClassType) {
			if (got == CiType.Null)
				return expr;
			if (Extends(got, ((CiClassType) expected).Class)) {
				if (expr is CiCondExpr) {
					// C doesn't like &(cond ? foo : bar)
					return Coerce((CiCondExpr) expr, expected);
				}
				return new CiCoercion { ResultType = expected, Inner = expr };
			}
		}
		if (expected is CiArrayPtrType) {
			if (got == CiType.Null)
				return expr;
			CiArrayType gotArray = got as CiArrayType;
			if (got != null && ((CiArrayPtrType) expected).ElementType.Equals(gotArray.ElementType))
				return new CiCoercion { ResultType = expected, Inner = expr };
		}
		throw new ResolveException("Expected {0}, got {1}", expected, got);
	}

	CiExpr Coerce(CiExpr expr, CiType expected)
	{
		return (CiExpr) Coerce((CiMaybeAssign) expr, expected);
	}

	object ResolveConstExpr(CiExpr expr, CiType type)
	{
		expr = Coerce(Resolve(expr), type);
		CiConstExpr ce = expr as CiConstExpr;
		if (ce == null)
			throw new ResolveException("{0} is not constant", expr);
		return ce.Value;
	}

	object ResolveConstInitializer(ref CiType type, object value)
	{
		if (type is CiArrayType) {
			object[] array = value as object[];
			if (array == null)
				return value;
			CiType elementType = ((CiArrayType) type).ElementType;
			if (type is CiArrayStorageType) {
				int expected = ((CiArrayStorageType) type).Length;
				if (array.Length != expected)
					throw new ResolveException("Expected {0} array elements, got {1}", expected, array.Length);
			}
			else {
				type = new CiArrayStorageType { ElementType = elementType, Length = array.Length };
			}
			Array dest = Array.CreateInstance(elementType.DotNetType, array.Length);
			for (int i = 0; i < array.Length; i++)
				dest.SetValue(ResolveConstInitializer(ref elementType, array[i]), i);
			return dest;
		}
		if (value is CiExpr)
			return ResolveConstExpr((CiExpr) value, type);
		return value;
	}

	void ICiSymbolVisitor.Visit(CiEnum enu)
	{
	}

	static bool Is7Bit(byte[] bytes)
	{
		foreach (byte b in bytes)
			if ((b & ~0x7f) != 0)
				return false;
		return true;
	}

	void ICiSymbolVisitor.Visit(CiConst konst)
	{
		if (konst.CurrentlyResolving)
			throw new ResolveException("Circular dependency for {0}", konst.Name);
		konst.CurrentlyResolving = true;
		konst.Type = Resolve(konst.Type);
		konst.Value = ResolveConstInitializer(ref konst.Type, konst.Value);
		byte[] bytes = konst.Value as byte[];
		if (bytes != null)
			konst.Is7Bit = Is7Bit(bytes);
		konst.CurrentlyResolving = false;
	}

	static string GetConstString(CiExpr expr)
	{
		object o = ((CiConstExpr) expr).Value;
		if (o is string || o is int || o is byte)
			return Convert.ToString(o, CultureInfo.InvariantCulture);
		throw new ResolveException("Cannot convert {0} to string", expr.Type);
	}

	static int GetConstInt(CiExpr expr)
	{
		return (int) ((CiConstExpr) expr).Value;
	}

	void MarkWritable(CiExpr target)
	{
		for (;;) {
			if (target is CiFieldAccess)
				target = ((CiFieldAccess) target).Obj;
			else if (target is CiArrayAccess)
				target = ((CiArrayAccess) target).Array;
			else
				break;
			while (target is CiCoercion)
				target = (CiExpr) ((CiCoercion) target).Inner;
			ICiPtrType pt = target.Type as ICiPtrType;
			if (pt != null) {
				this.WritablePtrTypes.Add(pt);
				break;
			}
		}
	}

	static void CheckCopyPtr(CiType target, CiMaybeAssign source)
	{
		ICiPtrType tp = target as ICiPtrType;
		if (tp == null)
			return;
		CiCondExpr cond = source as CiCondExpr;
		if (cond != null) {
			CheckCopyPtr(target, cond.OnTrue);
			CheckCopyPtr(target, cond.OnFalse);
			return;
		}
		for (;;) {
			while (source is CiCoercion)
				source = ((CiCoercion) source).Inner;
			ICiPtrType sp = source.Type as ICiPtrType;
			if (sp != null) {
				tp.Sources.Add(sp);
				break;
			}
			if (source is CiFieldAccess)
				source = ((CiFieldAccess) source).Obj;
			else if (source is CiArrayAccess)
				source = ((CiArrayAccess) source).Array;
			else
				break;
		}
	}

	CiLValue ResolveLValue(CiExpr expr)
	{
		CiLValue result = Resolve(expr) as CiLValue;
		if (result == null)
			throw new ResolveException("Expected l-value");
		MarkWritable(result);
		return result;
	}

	CiSymbol Lookup(CiSymbolAccess expr)
	{
		CiSymbol symbol = expr.Symbol;
		if (symbol is CiUnknownSymbol)
			symbol = this.Symbols.Lookup(((CiUnknownSymbol) symbol).Name);
		return symbol;
	}

	CiExpr GetValue(CiConst konst)
	{
		((ICiSymbolVisitor) this).Visit(konst);
		if (konst.Type is CiArrayType)
			return new CiConstAccess { Const = konst };
		else
			return new CiConstExpr(konst.Value);
	}

	CiFieldAccess CreateFieldAccess(CiExpr obj, CiField field)
	{
		if (field.Class != this.CurrentClass && field.Visibility == CiVisibility.Private)
			field.Visibility = CiVisibility.Internal;
		if (!(obj.Type is CiClassPtrType) || ((CiClassPtrType) obj.Type).Class != field.Class)
			obj = Coerce(obj, new CiClassStorageType { Class = field.Class });
		return new CiFieldAccess { Obj = obj, Field = field };
	}

	CiExpr ICiExprVisitor.Visit(CiSymbolAccess expr)
	{
		CiSymbol symbol = Lookup(expr);
		if (symbol is CiVar)
			return new CiVarAccess { Var = (CiVar) symbol };
		if (symbol is CiConst)
			return GetValue((CiConst) symbol);
		if (symbol is CiField) {
			if (this.CurrentMethod.CallType == CiCallType.Static)
				throw new ResolveException("Cannot access field from a static method");
			symbol.Accept(this);
			return CreateFieldAccess(new CiVarAccess { Var = this.CurrentMethod.This }, (CiField) symbol);
		}
		throw new ResolveException("Invalid expression");
	}

	CiExpr ICiExprVisitor.Visit(CiUnknownMemberAccess expr)
	{
		if (expr.Parent is CiSymbolAccess) {
			CiSymbol symbol = Lookup((CiSymbolAccess) expr.Parent);
			if (symbol is CiEnum)
				return new CiConstExpr(((CiEnum) symbol).LookupMember(expr.Name));
			if (symbol is CiClass) {
				symbol = ((CiClass) symbol).Members.Lookup(expr.Name);
				if (symbol is CiConst)
					return GetValue((CiConst) symbol);
				throw new ResolveException("Cannot access " + expr.Name);
			}
		}
		CiExpr parent = Resolve(expr.Parent);
		CiSymbol member = parent.Type.LookupMember(expr.Name);
		member.Accept(this);
		if (member is CiField)
			return CreateFieldAccess(parent, (CiField) member);
		if (member is CiProperty) {
			CiProperty prop = (CiProperty) member;
			if (parent is CiConstExpr) {
				if (prop == CiLibrary.LowByteProperty)
					return new CiConstExpr((byte) GetConstInt(parent));
				if (prop == CiLibrary.SByteProperty)
					return new CiConstExpr((int) (sbyte) GetConstInt(parent));
				if (prop == CiLibrary.StringLengthProperty)
					return new CiConstExpr(((string) ((CiConstExpr) parent).Value).Length);
			}
			return new CiPropertyAccess { Obj = parent, Property = prop };
		}
		if (member is CiConst)
			return new CiConstExpr(((CiConst) member).Value);
		throw new ResolveException(member.ToString());
	}

	CiExpr ICiExprVisitor.Visit(CiIndexAccess expr)
	{
		CiExpr parent = Resolve(expr.Parent);
		CiExpr index = Coerce(Resolve(expr.Index), CiIntType.Value);
		if (parent.Type is CiArrayType)
			return new CiArrayAccess { Array = parent, Index = index };
		if (parent.Type is CiStringType) {
			if (parent is CiConstExpr && index is CiConstExpr) {
				string s = (string) ((CiConstExpr) parent).Value;
				int i = GetConstInt(index);
				if (i < s.Length)
					return new CiConstExpr((int) s[i]);
			}
			return new CiMethodCall {
				Method = CiLibrary.CharAtMethod,
				Obj = parent,
				Arguments = new CiExpr[1] { index }
			};
		}
		throw new ResolveException("Indexed object is neither array or string");
	}

	void ICiSymbolVisitor.Visit(CiDelegate del)
	{
		del.ReturnType = Resolve(del.ReturnType);
		foreach (CiParam param in del.Params)
			param.Type = Resolve(param.Type);
	}

	CiType ICiTypeVisitor.Visit(CiDelegate del)
	{
		((ICiSymbolVisitor) this).Visit(del);
		return del;
	}	

	void ResolveObj(CiMethodCall expr)
	{
		if (expr.Obj is CiSymbolAccess) {
			// Foo(...)
			CiMethod method = Lookup((CiSymbolAccess) expr.Obj) as CiMethod;
			if (method != null) {
				expr.Method = method;
				if (method.CallType == CiCallType.Static)
					expr.Obj = null;
				else {
					if (this.CurrentMethod.CallType == CiCallType.Static)
						throw new ResolveException("Cannot call instance method from a static method");
					expr.Obj = Coerce(new CiVarAccess { Var = this.CurrentMethod.This }, new CiClassPtrType { Class = method.Class });
					CheckCopyPtr(method.This.Type, expr.Obj);
				}
				return;
			}
		}
		else if (expr.Obj is CiUnknownMemberAccess) {
			// ???.Foo(...)
			CiUnknownMemberAccess uma = (CiUnknownMemberAccess) expr.Obj;
			if (uma.Parent is CiSymbolAccess) {
				CiClass klass = Lookup((CiSymbolAccess) uma.Parent) as CiClass;
				if (klass != null) {
					// Class.Foo(...)
					CiMethod method = klass.Members.Lookup(uma.Name) as CiMethod;
					if (method != null) {
						if (method.CallType != CiCallType.Static)
							throw new ResolveException("{0} is a non-static method", method.Name);
						expr.Method = method;
						expr.Obj = null;
						return;
					}
				}
			}
			CiExpr obj = Resolve(uma.Parent);
			{
				CiMethod method = obj.Type.LookupMember(uma.Name) as CiMethod;
				if (method != null) {
					// obj.Foo(...)
					if (method.CallType == CiCallType.Static)
						throw new ResolveException("{0} is a static method", method.Name);
					if (method.This != null) {
						// user-defined method
						CheckCopyPtr(method.This.Type, obj);
						obj = Coerce(obj, new CiClassPtrType { Class = method.Class });
					}
					expr.Method = method;
					expr.Obj = obj;
					return;
				}
			}
		}
		expr.Obj = Resolve(expr.Obj);
		if (!(expr.Obj.Type is CiDelegate))
			throw new ResolveException("Invalid call");
		if (expr.Obj.HasSideEffect)
			throw new ResolveException("Side effects not allowed in delegate call");
	}

	void CoerceArguments(CiMethodCall expr)
	{
		expr.Signature.Accept(this);
		CiParam[] paramz = expr.Signature.Params;
		if (expr.Arguments.Length != paramz.Length)
			throw new ResolveException("Invalid number of arguments for {0}, expected {1}, got {2}", expr.Signature.Name, paramz.Length, expr.Arguments.Length);
		for (int i = 0; i < paramz.Length; i++) {
			CiExpr arg = Resolve(expr.Arguments[i]);
			CheckCopyPtr(paramz[i].Type, arg);
			expr.Arguments[i] = Coerce(arg, paramz[i].Type);
		}
	}

	CiExpr ICiExprVisitor.Visit(CiMethodCall expr)
	{
		ResolveObj(expr);
		CoerceArguments(expr);
		if (expr.Method != null && expr.Method != this.CurrentMethod) {
			if (expr.Method.IsMutator)
				MarkWritable(expr.Obj);
			expr.Method.CalledBy.Add(this.CurrentMethod);
			this.CurrentMethod.Calls.Add(expr.Method);
		}
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiUnaryExpr expr)
	{
		CiExpr resolved;
		if (expr.Op == CiToken.Increment || expr.Op == CiToken.Decrement)
			resolved = ResolveLValue(expr.Inner);
		else
			resolved = Resolve(expr.Inner);
		expr.Inner = Coerce(resolved, CiIntType.Value);
		if (expr.Op == CiToken.Minus && expr.Inner is CiConstExpr)
			return new CiConstExpr(-GetConstInt(expr.Inner));
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiCondNotExpr expr)
	{
		expr.Inner = Coerce(Resolve(expr.Inner), CiBoolType.Value);
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiPostfixExpr expr)
	{
		expr.Inner = Coerce(ResolveLValue(expr.Inner), CiIntType.Value);
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiBinaryExpr expr)
	{
		CiExpr left = Resolve(expr.Left);
		CiExpr right = Resolve(expr.Right);
		if (expr.Op == CiToken.Plus && (left.Type is CiStringType || right.Type is CiStringType)) {
			if (!(left is CiConstExpr && right is CiConstExpr))
				throw new ResolveException("String concatenation allowed only for constants. Consider using +=");
			string a = GetConstString(left);
			string b = GetConstString(right);
			return new CiConstExpr(a + b);
		}
		left = Coerce(left, CiIntType.Value);
		right = Coerce(right, CiIntType.Value);
		if (right is CiConstExpr) {
			int b = GetConstInt(right);
			if (left is CiConstExpr) {
				int a = GetConstInt(left);
				switch (expr.Op) {
				case CiToken.Asterisk: a *= b; break;
				case CiToken.Slash: a /= b; break;
				case CiToken.Mod: a %= b; break;
				case CiToken.And: a &= b; break;
				case CiToken.ShiftLeft: a <<= b; break;
				case CiToken.ShiftRight: a >>= b; break;
				case CiToken.Plus: a += b; break;
				case CiToken.Minus: a -= b; break;
				case CiToken.Or: a |= b; break;
				case CiToken.Xor: a ^= b; break;
				}
				return new CiConstExpr(a);
			}
			if (expr.Op == CiToken.And && (b & ~0xff) == 0) {
				CiCoercion c = left as CiCoercion;
				if (c != null && c.Inner.Type == CiByteType.Value)
					left = (CiExpr) c.Inner;
			}
		}
		expr.Left = left;
		expr.Right = right;
		return expr;
	}

	static CiType FindCommonType(CiExpr expr1, CiExpr expr2)
	{
		CiType type1 = expr1.Type;
		CiType type2 = expr2.Type;
		if (type1.Equals(type2))
			return type1;
		if ((type1 == CiIntType.Value && type2 == CiByteType.Value)
			|| (type1 == CiByteType.Value && type2 == CiIntType.Value))
			return CiIntType.Value;
		CiType type = type1.Ptr;
		if (type != null)
			return type; // stg, ptr || stg, null
		type = type2.Ptr;
		if (type != null)
			return type; // ptr, stg || null, stg
		if (type1 != CiType.Null)
			return type1; // ptr, null
		if (type2 != CiType.Null)
			return type2; // null, ptr
		throw new ResolveException("Incompatible types");
	}

	CiExpr ICiExprVisitor.Visit(CiBoolBinaryExpr expr)
	{
		CiExpr left = Resolve(expr.Left);
		CiExpr right = Resolve(expr.Right);
		CiType type;
		switch (expr.Op) {
		case CiToken.CondAnd:
		case CiToken.CondOr:
			type = CiBoolType.Value;
			break;
		case CiToken.Equal:
		case CiToken.NotEqual:
			type = FindCommonType(left, right);
			break;
		default:
			type = CiIntType.Value;
			break;
		}
		expr.Left = Coerce(left, type);
		expr.Right = Coerce(right, type);
		CiConstExpr cleft = expr.Left as CiConstExpr;
		if (cleft != null) {
			switch (expr.Op) {
			case CiToken.CondAnd:
				return (bool) cleft.Value ? expr.Right : new CiConstExpr(false);
			case CiToken.CondOr:
				return (bool) cleft.Value ? new CiConstExpr(true) : expr.Right;
			case CiToken.Equal:
			case CiToken.NotEqual:
				CiConstExpr cright = expr.Right as CiConstExpr;
				if (cright != null) {
					bool eq = object.Equals(cleft.Value, cright.Value);
					return new CiConstExpr(expr.Op == CiToken.Equal ? eq : !eq);
				}
				break;
			default:
				if (expr.Right is CiConstExpr) {
					int a = GetConstInt(cleft);
					int b = GetConstInt(expr.Right);
					bool result;
					switch (expr.Op) {
					case CiToken.Less: result = a < b; break;
					case CiToken.LessOrEqual: result = a <= b; break;
					case CiToken.Greater: result = a > b; break;
					case CiToken.GreaterOrEqual: result = a >= b; break;
					default: return expr;
					}
					return new CiConstExpr(result);
				}
				break;
			}
		}
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiCondExpr expr)
	{
		expr.Cond = Coerce(Resolve(expr.Cond), CiBoolType.Value);
		CiExpr expr1 = Resolve(expr.OnTrue);
		CiExpr expr2 = Resolve(expr.OnFalse);
		expr.ResultType = FindCommonType(expr1, expr2);
		expr.OnTrue = Coerce(expr1, expr.ResultType);
		expr.OnFalse = Coerce(expr2, expr.ResultType);
		CiConstExpr konst = expr.Cond as CiConstExpr;
		if (konst != null)
			return (bool) konst.Value ? expr.OnTrue : expr.OnFalse;
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiBinaryResourceExpr expr)
	{
		string name = (string) ResolveConstExpr(expr.NameExpr, CiStringPtrType.Value);
		CiBinaryResource resource;
		if (!this.BinaryResources.TryGetValue(name, out resource)) {
			resource = new CiBinaryResource();
			resource.Name = name;
			resource.Content = File.ReadAllBytes(FindFile(name));
			resource.Type = new CiArrayStorageType { ElementType = CiByteType.Value, Length = resource.Content.Length };
			this.BinaryResources.Add(name, resource);
		}
		expr.Resource = resource;
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiNewExpr expr)
	{
		CiType type = expr.NewType;
		CiClassStorageType classStorageType = type as CiClassStorageType;
		if (classStorageType != null) {
			classStorageType.Class = ResolveClass(classStorageType.Class);
			classStorageType.Class.IsAllocated = true;
		}
		else {
			CiArrayStorageType arrayStorageType = (CiArrayStorageType) type;
			arrayStorageType.ElementType = Resolve(arrayStorageType.ElementType);
			arrayStorageType.LengthExpr = Coerce(Resolve(arrayStorageType.LengthExpr), CiIntType.Value);
		}
		return expr;
	}

	CiExpr Resolve(CiExpr expr)
	{
		return expr.Accept(this);
	}

	void ICiSymbolVisitor.Visit(CiField field)
	{
		field.Type = Resolve(field.Type);
	}

	bool Resolve(ICiStatement[] statements)
	{
		bool reachable = true;
		foreach (ICiStatement child in statements) {
			if (!reachable)
				throw new ResolveException("Unreachable statement");
			child.Accept(this);
			reachable = child.CompletesNormally;
		}
		return reachable;
	}

	void ICiStatementVisitor.Visit(CiBlock statement)
	{
		statement.CompletesNormally = Resolve(statement.Statements);
	}

	void ICiStatementVisitor.Visit(CiConst statement)
	{
	}

	void ICiStatementVisitor.Visit(CiVar statement)
	{
		statement.Type = Resolve(statement.Type);
		if (statement.InitialValue != null) {
			CiType type = statement.Type;
			CiExpr initialValue = Resolve(statement.InitialValue);
			CheckCopyPtr(type, initialValue);
			if (type is CiArrayStorageType) {
				type = ((CiArrayStorageType) type).ElementType;
				CiConstExpr ce = Coerce(initialValue, type) as CiConstExpr;
				if (ce == null)
					throw new ResolveException("Array initializer is not constant");
				statement.InitialValue = ce;
				if (type == CiBoolType.Value) {
					if (!false.Equals(ce.Value))
						throw new ResolveException("Bool arrays can only be initialized with false");
				}
				else if (type == CiByteType.Value) {
					if (!((byte) 0).Equals(ce.Value))
						throw new ResolveException("Byte arrays can only be initialized with zero");
				}
				else if (type == CiIntType.Value) {
					if (!0.Equals(ce.Value))
						throw new ResolveException("Int arrays can only be initialized with zero");
				}
				else
					throw new ResolveException("Invalid array initializer");
			}
			else
				statement.InitialValue = Coerce(initialValue, type);
		}
	}

	void ICiStatementVisitor.Visit(CiExpr statement)
	{
		Resolve((CiExpr) statement);
	}

	void ICiStatementVisitor.Visit(CiAssign statement)
	{
		statement.Target = ResolveLValue(statement.Target);
		if (statement.Target is CiVarAccess && ((CiVarAccess) statement.Target).Var == this.CurrentMethod.This)
			throw new ResolveException("Cannot assign to this");
		CiMaybeAssign source = statement.Source;
		if (source is CiAssign)
			Resolve((ICiStatement) source);
		else
			source = Resolve((CiExpr) source);
		CiType type = statement.Target.Type;
		CheckCopyPtr(type, source);
		statement.Source = Coerce(source, type);
		if (statement.Op != CiToken.Assign && type != CiIntType.Value && type != CiByteType.Value) {
			if (statement.Op == CiToken.AddAssign && type is CiStringStorageType && statement.Source.Type is CiStringType)
				{} // OK
			else
				throw new ResolveException("Invalid compound assignment");
		}
	}

	void ICiStatementVisitor.Visit(CiDelete statement)
	{
		statement.Expr = Resolve(statement.Expr);
		ICiPtrType type = statement.Expr.Type as ICiPtrType;
		if (type == null)
			throw new ResolveException("'delete' takes a class or array pointer");
		if (statement.Expr.HasSideEffect)
			throw new ResolveException("Side effects not allowed in 'delete'");
		this.WritablePtrTypes.Add(type);
	}

	void ICiStatementVisitor.Visit(CiBreak statement)
	{
		if (this.CurrentLoopOrSwitch == null)
			throw new ResolveException("break outside loop and switch");
		this.CurrentLoopOrSwitch.CompletesNormally = true;
	}

	void ICiStatementVisitor.Visit(CiContinue statement)
	{
		if (this.CurrentLoop == null)
			throw new ResolveException("continue outside loop");
	}

	void ResolveLoop(CiLoop statement)
	{
		statement.CompletesNormally = false;
		if (statement.Cond != null) {
			statement.Cond = Coerce(Resolve(statement.Cond), CiBoolType.Value);
			statement.CompletesNormally = !statement.Cond.IsConst(false);
		}
		CiLoop oldLoop = this.CurrentLoop;
		CiCondCompletionStatement oldLoopOrSwitch = this.CurrentLoopOrSwitch;
		this.CurrentLoopOrSwitch = this.CurrentLoop = statement;
		Resolve(statement.Body);
		this.CurrentLoop = oldLoop;
		this.CurrentLoopOrSwitch = oldLoopOrSwitch;
	}

	void ICiStatementVisitor.Visit(CiDoWhile statement)
	{
		ResolveLoop(statement);
	}

	void ICiStatementVisitor.Visit(CiFor statement)
	{
		if (statement.Init != null)
			Resolve(statement.Init);
		if (statement.Advance != null)
			Resolve(statement.Advance);
		ResolveLoop(statement);
	}

	void ICiStatementVisitor.Visit(CiIf statement)
	{
		statement.Cond = Coerce(Resolve(statement.Cond), CiBoolType.Value);
		Resolve(statement.OnTrue);
		if (statement.OnFalse != null) {
			Resolve(statement.OnFalse);
			statement.CompletesNormally = statement.OnTrue.CompletesNormally || statement.OnFalse.CompletesNormally;
		}
		else
			statement.CompletesNormally = true;
	}

	void ICiStatementVisitor.Visit(CiNativeBlock statement)
	{
	}

	void ICiStatementVisitor.Visit(CiReturn statement)
	{
		CiType type = this.CurrentMethod.Signature.ReturnType;
		if (type != CiType.Void)
			statement.Value = Coerce(Resolve(statement.Value), type);
	}

	void ICiStatementVisitor.Visit(CiSwitch statement)
	{
		statement.Value = Resolve(statement.Value);
		CiType type = statement.Value.Type;
		CiCondCompletionStatement oldLoopOrSwitch = this.CurrentLoopOrSwitch;
		this.CurrentLoopOrSwitch = statement;

		HashSet<object> values = new HashSet<object>();
		CiCase fallthroughFrom = null;
		foreach (CiCase kase in statement.Cases) {
			for (int i = 0; i < kase.Values.Length; i++) {
				kase.Values[i] = ResolveConstExpr((CiExpr) kase.Values[i], type);
				if (!values.Add(kase.Values[i]))
					throw new ResolveException("Duplicate case value");
			}
			if (fallthroughFrom != null) {
				if (fallthroughFrom.FallthroughTo == null)
					throw new ResolveException("goto default followed by case");
				if (!ResolveConstExpr(fallthroughFrom.FallthroughTo, type).Equals(kase.Values[0]))
					throw new ResolveException("goto case doesn't match the next case");
			}
			bool reachable = Resolve(kase.Body);
			if (kase.Fallthrough) {
				if (!reachable)
					throw new ResolveException("goto is not reachable");
				fallthroughFrom = kase;
			}
			else {
				if (reachable)
					throw new ResolveException("case must end with break, return, throw or goto");
				fallthroughFrom = null;
			}
		}

		if (statement.DefaultBody != null) {
			if (fallthroughFrom != null && fallthroughFrom.FallthroughTo != null)
				throw new ResolveException("goto case followed by default");
			bool reachable = Resolve(statement.DefaultBody);
			if (reachable)
				throw new ResolveException("default must end with break, return, throw or goto");
		}
		else {
			if (fallthroughFrom != null)
				throw new ResolveException("goto cannot be the last statement in switch");
		}

		this.CurrentLoopOrSwitch = oldLoopOrSwitch;
	}

	void ICiStatementVisitor.Visit(CiThrow statement)
	{
		statement.Message = Coerce(Resolve(statement.Message), CiStringPtrType.Value);
		this.ThrowingMethods.Add(this.CurrentMethod);
	}

	void ICiStatementVisitor.Visit(CiWhile statement)
	{
		ResolveLoop(statement);
	}

	void Resolve(ICiStatement statement)
	{
		statement.Accept(this);
	}

	void ICiSymbolVisitor.Visit(CiMethod method)
	{
		this.CurrentMethod = method;
		Resolve(method.Signature);
		if (method.CallType != CiCallType.Abstract) {
			Resolve(method.Body);
			if (method.Signature.ReturnType != CiType.Void && method.Body.CompletesNormally)
				throw new ResolveException("Method can complete without a return value");
		}
		this.CurrentMethod = null;
	}

	void ResolveBase(CiClass klass)
	{
		if (klass.BaseClass != null) {
			klass.BaseClass = ResolveClass(klass.BaseClass);
			klass.Members.Parent = klass.BaseClass.Members;
		}
	}

	void ICiSymbolVisitor.Visit(CiClass klass)
	{
		this.CurrentClass = klass;
		this.Symbols = klass.Members;
		if (klass.Constructor != null)
			klass.Constructor.Accept(this);
		foreach (CiSymbol member in klass.Members)
			member.Accept(this);
		klass.BinaryResources = this.BinaryResources.Values.ToArray();
		this.BinaryResources.Clear();
		this.Symbols = this.Symbols.Parent;
		this.CurrentClass = null;
	}

	static void MarkWritable(ICiPtrType type)
	{
		if (type.Writability == PtrWritability.ReadWrite)
			return;
		if (type.Writability == PtrWritability.ReadOnly)
			throw new ResolveException("Attempt to write a read-only array");
		type.Writability = PtrWritability.ReadWrite;
		foreach (ICiPtrType source in type.Sources)
			MarkWritable(source);
	}

	static object GetErrorValue(CiType type)
	{
		if (type == CiType.Void)
			return false;
		if (type == CiIntType.Value)
			return -1;
		if (type == CiStringPtrType.Value || type is CiClassPtrType || type is CiArrayPtrType)
			return null;
		throw new ResolveException("throw in a method of unsupported return type");
	}

	static void MarkThrows(CiMethod method)
	{
		if (method.Throws)
			return;
		method.Throws = true;
		method.ErrorReturnValue = GetErrorValue(method.Signature.ReturnType);
		foreach (CiMethod calledBy in method.CalledBy)
			MarkThrows(calledBy);
	}

	static void MarkDead(CiMethod method)
	{
		if (method.Visibility == CiVisibility.Private && method.CallType != CiCallType.Override && method.CalledBy.Count == 0) {
			method.Visibility = CiVisibility.Dead;
			foreach (CiMethod called in method.Calls) {
				called.CalledBy.Remove(method);
				MarkDead(called);
			}
		}
	}

	static void MarkDead(CiClass klass)
	{
		foreach (CiSymbol member in klass.Members) {
			if (member is CiMethod)
				MarkDead((CiMethod) member);
		}
	}

	static void MarkInternal(CiMethod method)
	{
		if (method.Visibility == CiVisibility.Private && method.CalledBy.Any(caller => caller.Class != method.Class))
			method.Visibility = CiVisibility.Internal;
	}

	static void MarkInternal(CiClass klass)
	{
		foreach (CiSymbol member in klass.Members) {
			if (member is CiMethod)
				MarkInternal((CiMethod) member);
		}
	}

	public void Resolve(CiProgram program)
	{
		this.Symbols = program.Globals;
		foreach (CiSymbol symbol in program.Globals) {
			if (symbol is CiClass)
				ResolveBase((CiClass) symbol);
		}
		foreach (CiSymbol symbol in program.Globals)
			symbol.Accept(this);
		foreach (ICiPtrType type in this.WritablePtrTypes)
			MarkWritable(type);
		foreach (CiMethod method in this.ThrowingMethods)
			MarkThrows(method);
		foreach (CiSymbol symbol in program.Globals) {
			if (symbol is CiClass)
				MarkDead((CiClass) symbol);
		}
		foreach (CiSymbol symbol in program.Globals) {
			if (symbol is CiClass)
				MarkInternal((CiClass) symbol);
		}
	}
}
}
