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

public class CiResolver : ICiTypeVisitor, ICiExprVisitor, ICiStatementVisitor
{
	public IEnumerable<string> SearchDirs = new string[0];
	readonly SortedDictionary<string, CiBinaryResource> BinaryResources = new SortedDictionary<string, CiBinaryResource>();
	SymbolTable Globals;
	readonly HashSet<ICiPtrType> WritablePtrTypes = new HashSet<ICiPtrType>();
	public CiFunction CurrentFunction;

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
		CiSymbol symbol = this.Globals.Lookup(type.Name);
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
			type.Class = this.Globals.Lookup(name) as CiClass;
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

	CiMaybeAssign Coerce(CiMaybeAssign expr, CiType expected)
	{
		CiType got = expr.Type;
		if (expected.Equals(got))
			return expr;
		if (expected == CiIntType.Value && got == CiByteType.Value) {
			CiConstExpr konst = expr as CiConstExpr;
			if (konst != null)
				return new CiConstExpr((object) (int) (byte) konst.Value);
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
				CiCondExpr cond = expr as CiCondExpr;
				if (cond != null) {
					// C doesn't like &(cond ? foo : bar)
					return new CiCondExpr {
						Cond = cond.Cond,
						OnTrue = new CiCoercion { ResultType = expected, Inner = cond.OnTrue },
						OnFalse = new CiCoercion { ResultType = expected, Inner = cond.OnFalse }
					};
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

	void Resolve(CiConst konst)
	{
		if (konst.CurrentlyResolving)
			throw new ResolveException("Circular dependency for {0}", konst.Name);
		konst.CurrentlyResolving = true;
		konst.Type = Resolve(konst.Type);
		konst.Value = ResolveConstInitializer(ref konst.Type, konst.Value);
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

	CiSymbol Lookup(CiSymbolAccess expr)
	{
		CiSymbol symbol = expr.Symbol;
		if (symbol is CiUnknownSymbol)
			symbol = this.Globals.Lookup(((CiUnknownSymbol) symbol).Name);
		return symbol;
	}

	CiExpr ICiExprVisitor.Visit(CiSymbolAccess expr)
	{
		CiSymbol symbol = Lookup(expr);
		if (symbol is CiVar)
			return new CiVarAccess { Var = (CiVar) symbol };
		else if (symbol is CiConst) {
			CiConst konst = (CiConst) symbol;
			Resolve(konst);
			if (konst.Type is CiArrayType)
				return new CiConstAccess { Const = konst };
			else
				return new CiConstExpr(konst.Value);
		}
		throw new ResolveException("Invalid expression");
	}

	CiExpr ICiExprVisitor.Visit(CiUnknownMemberAccess expr)
	{
		if (expr.Parent is CiSymbolAccess) {
			CiEnum enu = Lookup((CiSymbolAccess) expr.Parent) as CiEnum;
			if (enu != null)
				return new CiConstExpr(enu.LookupMember(expr.Name));
		}
		CiExpr parent = Resolve(expr.Parent);
		CiSymbol member = parent.Type.LookupMember(expr.Name);
		if (member is CiField)
			return new CiFieldAccess { Obj = parent, Field = (CiField) member };
		if (member is CiProperty)
			return new CiPropertyAccess { Obj = parent, Property = (CiProperty) member };
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
				Function = CiStringType.CharAtMethod,
				Obj = parent,
				Arguments = new CiExpr[1] { index }
			};
		}
		throw new ResolveException("Indexed object is neither array or string");
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

	void CoerceArguments(CiFunctionCall expr)
	{
		CiParam[] paramz = expr.Function.Params;
		if (expr.Arguments.Length != paramz.Length)
			throw new ResolveException("Invalid number of arguments for {0}, expected {1}, got {2}", expr.Name, paramz.Length, expr.Arguments.Length);
		for (int i = 0; i < paramz.Length; i++) {
			CiExpr arg = Resolve(expr.Arguments[i]);
			CheckCopyPtr(paramz[i].Type, arg);
			expr.Arguments[i] = Coerce(arg, paramz[i].Type);
		}
	}

	CiExpr ICiExprVisitor.Visit(CiFunctionCall expr)
	{
		expr.Function = this.Globals.Lookup(expr.Name) as CiFunction;
		if (expr.Function == null)
			throw new ResolveException("{0} is not a function", expr.Name);
		CoerceArguments(expr);
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiMethodCall expr)
	{
		expr.Obj = Resolve(expr.Obj);
		expr.Function = expr.Obj.Type.LookupMember(expr.Name) as CiFunction;
		if (expr.Function == null)
			throw new ResolveException("{0} is not a method", expr.Name);
		CoerceArguments(expr);
		if (expr.Function.IsMutatorMethod)
			MarkWritable(expr.Obj);
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiUnaryExpr expr)
	{
		expr.Inner = Coerce(Resolve(expr.Inner), CiIntType.Value);
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiCondNotExpr expr)
	{
		expr.Inner = Coerce(Resolve(expr.Inner), CiBoolType.Value);
		return expr;
	}

	CiExpr ICiExprVisitor.Visit(CiPostfixExpr expr)
	{
		expr.Inner = Coerce(Resolve(expr.Inner), CiIntType.Value);
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
		if (left is CiConstExpr && right is CiConstExpr) {
			int a = GetConstInt(left);
			int b = GetConstInt(right);
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

	void Resolve(CiClass klass)
	{
		foreach (CiField field in klass.Fields)
			field.Type = Resolve(field.Type);
	}

	void ICiStatementVisitor.Visit(CiBlock statement)
	{
		foreach (ICiStatement child in statement.Statements)
			child.Accept(this);
	}

	void ICiStatementVisitor.Visit(CiConst statement)
	{
		#warning TODO: const
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
		statement.Target = Resolve(statement.Target) as CiLValue;
		if (statement.Target == null)
			throw new ResolveException("Not an l-value for an assignment");
		MarkWritable(statement.Target);

		CiMaybeAssign source = statement.Source;
		if (source is CiAssign)
			Resolve((ICiStatement) source);
		else
			source = Resolve((CiExpr) source);
		CheckCopyPtr(statement.Target.Type, source);
		statement.Source = Coerce(source, statement.Target.Type);
	}

	void ICiStatementVisitor.Visit(CiBreak statement)
	{
	}

	void ICiStatementVisitor.Visit(CiContinue statement)
	{
	}

	void ICiStatementVisitor.Visit(CiDoWhile statement)
	{
		Resolve(statement.Body);
		statement.Cond = Coerce(Resolve(statement.Cond), CiBoolType.Value);
	}

	void ICiStatementVisitor.Visit(CiFor statement)
	{
		if (statement.Init != null)
			Resolve(statement.Init);
		if (statement.Cond != null)
			statement.Cond = Coerce(Resolve(statement.Cond), CiBoolType.Value);
		if (statement.Advance != null)
			Resolve(statement.Advance);
		Resolve(statement.Body);
	}

	void ICiStatementVisitor.Visit(CiIf statement)
	{
		statement.Cond = Coerce(Resolve(statement.Cond), CiBoolType.Value);
		Resolve(statement.OnTrue);
		if (statement.OnFalse != null)
			Resolve(statement.OnFalse);
	}

	void ICiStatementVisitor.Visit(CiReturn statement)
	{
		CiType type = this.CurrentFunction.ReturnType;
		if (type != CiType.Void)
			statement.Value = Coerce(Resolve(statement.Value), type);
	}

	void ICiStatementVisitor.Visit(CiSwitch statement)
	{
		statement.Value = Resolve(statement.Value);
		CiType type = statement.Value.Type;
		foreach (CiCase kase in statement.Cases) {
			if (kase.Value != null)
				kase.Value = ResolveConstExpr((CiExpr) kase.Value, type);
			foreach (ICiStatement child in kase.Body)
				Resolve(child);
		}
		#warning TODO: multiple "default", duplicate "case"
	}

	void ICiStatementVisitor.Visit(CiWhile statement)
	{
		statement.Cond = Coerce(Resolve(statement.Cond), CiBoolType.Value);
		Resolve(statement.Body);
	}

	void Resolve(ICiStatement statement)
	{
		statement.Accept(this);
	}

	void ResolveSignature(CiFunction func)
	{
		this.CurrentFunction = func;
		func.ReturnType = Resolve(func.ReturnType);
		foreach (CiParam param in func.Params)
			param.Type = Resolve(param.Type);
		this.CurrentFunction = null;
	}

	void Resolve(CiFunction func)
	{
		this.CurrentFunction = func;
		Resolve(func.Body);
		this.CurrentFunction = null;
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

	public void Resolve(CiProgram program)
	{
		this.Globals = program.Globals;
		foreach (CiConst konst in program.ConstArrays)
			Resolve(konst);
		foreach (CiSymbol symbol in program.Globals) {
			if (symbol is CiConst)
				Resolve((CiConst) symbol);
			else if (symbol is CiClass)
				Resolve((CiClass) symbol);
		}
		foreach (CiSymbol symbol in program.Globals) {
			if (symbol is CiFunction)
				ResolveSignature((CiFunction) symbol);
		}
		foreach (CiSymbol symbol in program.Globals) {
			if (symbol is CiFunction)
				Resolve((CiFunction) symbol);
		}
		program.BinaryResources = this.BinaryResources.Values.ToArray();
		foreach (ICiPtrType type in this.WritablePtrTypes)
			MarkWritable(type);
	}
}
}
