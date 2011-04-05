// CiResolver.cs - Ci symbol resolver
//
// Copyright (C) 2011  Piotr Fusik
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

public class ResolveException : ApplicationException
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

	CiType ICiTypeVisitor.Visit(CiClassType type)
	{
		if (type.Class is CiUnknownClass) {
			string name = type.Class.Name;
			type.Class = this.Symbols.Lookup(name) as CiClass;
			if (type.Class == null)
				throw new ResolveException("{0} is not a class", name);
		}
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
		if (expected is CiClassPtrType) {
			if (got == CiType.Null)
				return expr;
			CiClassType gotClass = got as CiClassType;
			if (got != null && ((CiClassPtrType) expected).Class == gotClass.Class) {
				if (got is CiClassPtrType)
					return expr;
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
				return expr;
		}
		throw new ResolveException("Expected {0}, got {1}", expected, got);
	}

	CiExpr Coerce(CiExpr expr, CiType expected)
	{
		return (CiExpr) Coerce((CiMaybeAssign) expr, expected);
	}

	object ResolveConstExpr(CiExpr expr, CiType type)
	{
		CiConstExpr ce = Coerce(Resolve(expr), type) as CiConstExpr;
		if (ce == null)
			throw new ResolveException("Expression is not constant");
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

	CiExpr ICiExprVisitor.Visit(CiSymbolAccess expr)
	{
		CiSymbol symbol = Lookup(expr);
		if (symbol is CiVar)
			return new CiVarAccess { Var = (CiVar) symbol };
		if (symbol is CiConst)
			return GetValue((CiConst) symbol);
		if (symbol is CiField) {
			if (this.CurrentMethod.IsStatic)
				throw new ResolveException("Cannot access field from a static method");
			symbol.Accept(this);
			return new CiFieldAccess {
				Obj = new CiVarAccess { Var = this.CurrentMethod.This },
				Field = (CiField) symbol
			};
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
		if (member is CiField) {
			if (member.Visibility == CiVisibility.Private) {
				CiClass klass = ((CiClassType) parent.Type).Class;
				if (klass != this.CurrentClass)
					member.Visibility = CiVisibility.Internal;
			}
			return new CiFieldAccess { Obj = parent, Field = (CiField) member };
		}
		if (member is CiProperty) {
			CiProperty prop = (CiProperty) member;
			if (parent is CiConstExpr) {
				if (prop == CiIntType.LowByteProperty)
					return new CiConstExpr((byte) GetConstInt(parent));
				if (prop == CiIntType.SByteProperty)
					return new CiConstExpr((int) (sbyte) GetConstInt(parent));
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
				return new CiConstExpr((int) s[i]);
			}
			return new CiMethodCall {
				Method = CiStringType.CharAtMethod,
				Obj = parent,
				Arguments = new CiExpr[1] { index }
			};
		}
		throw new ResolveException("Indexed object is neither array or string");
	}

	void ResolveSignature(CiMethod method)
	{
		method.ReturnType = Resolve(method.ReturnType);
		foreach (CiParam param in method.Params)
			param.Type = Resolve(param.Type);
	}

	void CoerceArguments(CiMethodCall expr)
	{
		ResolveSignature(expr.Method);
		CiParam[] paramz = expr.Method.Params;
		if (expr.Arguments.Length != paramz.Length)
			throw new ResolveException("Invalid number of arguments for {0}, expected {1}, got {2}", expr.Name, paramz.Length, expr.Arguments.Length);
		for (int i = 0; i < paramz.Length; i++) {
			CiExpr arg = Resolve(expr.Arguments[i]);
			CheckCopyPtr(paramz[i].Type, arg);
			expr.Arguments[i] = Coerce(arg, paramz[i].Type);
		}
	}

	void ResolveObj(CiMethodCall expr)
	{
		if (expr.Obj is CiSymbolAccess) {
			CiSymbol symbol = Lookup((CiSymbolAccess) expr.Obj);
			if (symbol is CiClass) {
				expr.Method = ((CiClass) symbol).Members.Lookup(expr.Name) as CiMethod;
				if (expr.Method == null)
					throw new ResolveException("{0} is not a method", expr.Name);
				if (!expr.Method.IsStatic)
					throw new ResolveException("{0} is a non-static method", expr.Name);
				expr.Obj = null;
				return;
			}
		}
		CiExpr obj = Resolve(expr.Obj);
		expr.Method = obj.Type.LookupMember(expr.Name) as CiMethod;
		if (expr.Method == null)
			throw new ResolveException("{0} is not a method", expr.Name);
		if (expr.Method.IsStatic)
			throw new ResolveException("{0} is a static method", expr.Name);
		if (expr.Method.This != null) {
			// user-defined method
			CheckCopyPtr(expr.Method.This.Type, obj);
			obj = Coerce(obj, expr.Method.This.Type);
		}
		expr.Obj = obj;
	}

	CiExpr ICiExprVisitor.Visit(CiMethodCall expr)
	{
		if (expr.Obj != null) {
			ResolveObj(expr);
			if (expr.Method.Visibility == CiVisibility.Private && expr.Method.Class != this.CurrentClass)
				expr.Method.Visibility = CiVisibility.Internal;
		}
		else {
			expr.Method = this.Symbols.Lookup(expr.Name) as CiMethod;
			if (expr.Method == null)
				throw new ResolveException("{0} is not a method", expr.Name);
			if (!expr.Method.IsStatic) {
				if (this.CurrentMethod.IsStatic)
					throw new ResolveException("Cannot call instance method from a static method");
				CiExpr obj = new CiVarAccess { Var = this.CurrentMethod.This };
				CheckCopyPtr(expr.Method.This.Type, obj);
				expr.Obj = obj;
			}
		}
		CoerceArguments(expr);
		if (expr.Method.IsMutator)
			MarkWritable(expr.Obj);
		expr.Method.CalledBy.Add(this.CurrentMethod);
		this.CurrentMethod.Calls.Add(expr.Method);
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

	CiType FindCommonType(CiExpr expr1, CiExpr expr2)
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
		if (expr.Op == CiToken.CondAnd || expr.Op == CiToken.CondOr)
			type = CiBoolType.Value;
		else if (expr.Op == CiToken.Equal || expr.Op == CiToken.NotEqual)
			type = FindCommonType(left, right);
		else
			type = CiIntType.Value;
		expr.Left = Coerce(left, type);
		expr.Right = Coerce(right, type);
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
			if (type is CiArrayStorageType)
				type = ((CiArrayStorageType) type).ElementType;
			CiExpr initialValue = Resolve(statement.InitialValue);
			CheckCopyPtr(type, statement.InitialValue);
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
		CheckCopyPtr(statement.Target.Type, source);
		statement.Source = Coerce(source, statement.Target.Type);
		if (statement.Op != CiToken.Assign && statement.Target.Type != CiIntType.Value) {
			if (statement.Op == CiToken.AddAssign && statement.Target.Type is CiStringStorageType && statement.Source.Type is CiStringType)
				{} // OK
			else
				throw new ResolveException("Invalid compound assignment");
		}
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

	static bool IsFalse(CiExpr expr)
	{
		CiConstExpr ce = expr as CiConstExpr;
		return ce != null && false.Equals(ce.Value);
	}

	void ResolveLoop(CiLoop statement)
	{
		statement.CompletesNormally = false;
		if (statement.Cond != null) {
			statement.Cond = Coerce(Resolve(statement.Cond), CiBoolType.Value);
			statement.CompletesNormally = !IsFalse(statement.Cond);
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
		if (statement.Init != null) {
			Resolve(statement.Init);
			CiVar def = statement.Init as CiVar;
			if (def != null && def.InitialValue != null && (def.Type is CiStringStorageType || def.Type is CiArrayStorageType))
				throw new ResolveException("Cannot initialize variable of this type in the for statement");
		}
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
		CiType type = this.CurrentMethod.ReturnType;
		if (type != CiType.Void)
			statement.Value = Coerce(Resolve(statement.Value), type);
	}

	void ICiStatementVisitor.Visit(CiSwitch statement)
	{
		statement.Value = Resolve(statement.Value);
		CiType type = statement.Value.Type;
		HashSet<object> values = new HashSet<object>();
		CiCondCompletionStatement oldLoopOrSwitch = this.CurrentLoopOrSwitch;
		this.CurrentLoopOrSwitch = statement;
		CiCase fallthroughFrom = null;
		foreach (CiCase kase in statement.Cases) {
			if (kase.Value != null) {
				kase.Value = ResolveConstExpr((CiExpr) kase.Value, type);
				if (!values.Add(kase.Value))
					throw new ResolveException("Duplicate case value");
				if (fallthroughFrom != null) {
					if (fallthroughFrom.FallthroughTo == null)
						throw new ResolveException("goto default followed by case");
					if (!ResolveConstExpr(fallthroughFrom.FallthroughTo, type).Equals(kase.Value))
						throw new ResolveException("goto case doesn't match the next case");
				}
			}
			else {
				if (!values.Add(null))
					throw new ResolveException("Duplicate default case");
				if (fallthroughFrom != null && fallthroughFrom.FallthroughTo != null)
					throw new ResolveException("goto case followed by default");
			}
			bool reachable = Resolve(kase.Body);
			if (kase.Fallthrough) {
				if (!reachable)
					throw new ResolveException("goto is not reachable");
				fallthroughFrom = kase;
			}
			else {
				if (reachable && kase.Body.Length > 0)
					throw new ResolveException("Missing break, return, throw or goto");
				fallthroughFrom = null;
			}
		}
		if (fallthroughFrom != null)
			throw new ResolveException("goto cannot be the last statement in switch");
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
		ResolveSignature(method);
		Resolve(method.Body);
		if (method.ReturnType != CiType.Void && method.Body.CompletesNormally)
			throw new ResolveException("Method can complete without a return value");
		this.CurrentMethod = null;
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
		method.ErrorReturnValue = GetErrorValue(method.ReturnType);
		foreach (CiMethod calledBy in method.CalledBy)
			MarkThrows(calledBy);
	}

	static void MarkDead(CiMethod method)
	{
		if ((method.Visibility == CiVisibility.Private || method.Visibility == CiVisibility.Internal)
		 && method.CalledBy.Count == 0) {
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

	public void Resolve(CiProgram program)
	{
		this.Symbols = program.Globals;
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
	}
}
}
