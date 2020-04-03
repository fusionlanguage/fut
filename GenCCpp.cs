// GenCCpp.cs - C/C++ code generator
//
// Copyright (C) 2011-2020  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
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
using System.IO;

namespace Foxoft.Ci
{

public abstract class GenCCpp : GenTyped
{
	protected readonly Dictionary<CiClass, bool> WrittenClasses = new Dictionary<CiClass, bool>();

	protected abstract void IncludeStdInt();

	protected abstract void IncludeAssert();

	protected void WriteIncludes()
	{
		WriteIncludes("#include <", ">");
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte:
			IncludeStdInt();
			Write("int8_t");
			break;
		case TypeCode.Byte:
			IncludeStdInt();
			Write("uint8_t");
			break;
		case TypeCode.Int16:
			IncludeStdInt();
			Write("int16_t");
			break;
		case TypeCode.UInt16:
			IncludeStdInt();
			Write("uint16_t");
			break;
		case TypeCode.Int32:
			Write("int");
			break;
		case TypeCode.Int64:
			IncludeStdInt();
			Write("int64_t");
			break;
		default:
			throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Type is CiClass klass)
			return;
		base.WriteVarInit(def);
	}

	static bool IsPtrTo(CiExpr ptr, CiExpr other)
	{
		return (ptr.Type is CiClassPtrType || ptr.Type is CiArrayPtrType) && ptr.Type.IsAssignableFrom(other.Type);
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		CiType coercedType;
		if (IsPtrTo(expr.Left, expr.Right))
			coercedType = expr.Left.Type;
		else if (IsPtrTo(expr.Right, expr.Left))
			coercedType = expr.Right.Type;
		else {
			base.WriteEqual(expr, parent, not);
			return;
		}
		if (parent > CiPriority.Equality)
			Write('(');
		WriteCoerced(coercedType, expr.Left, CiPriority.Equality);
		Write(not ? " != " : " == ");
		WriteCoerced(coercedType, expr.Right, CiPriority.Equality);
		if (parent > CiPriority.Equality)
			Write(')');
	}

	protected static bool IsStringEmpty(CiBinaryExpr expr, out CiExpr str)
	{
		if (expr.Left is CiSymbolReference symbol && symbol.Symbol == CiSystem.StringLength
			&& expr.Right is CiLiteral literal && (long) literal.Value == 0) {
			str = symbol.Left;
			return true;
		}
		str = null;
		return false;
	}

	protected void WriteMathCall(CiMethod method, CiExpr[] args)
	{
		if (method == CiSystem.MathCeiling)
			Write("ceil");
		else if (method == CiSystem.MathFusedMultiplyAdd)
			Write("fma");
		else if (method == CiSystem.MathTruncate)
			Write("trunc");
		else
			WriteCamelCase(method.Name);
		WriteArgsInParentheses(method, args);
	}

	protected virtual void WriteArrayPtr(CiExpr expr, CiPriority parent)
	{
		expr.Accept(this, parent);
	}

	protected void WriteArrayPtrAdd(CiExpr array, CiExpr index)
	{
		if (index is CiLiteral literal && (long) literal.Value == 0)
			WriteArrayPtr(array, CiPriority.Statement);
		else {
			WriteArrayPtr(array, CiPriority.Add);
			Write(" + ");
			index.Accept(this, CiPriority.Add);
		}
	}

	protected static bool IsStringSubstring(CiExpr expr, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr length)
	{
		if (expr is CiCallExpr call) {
			CiMethod method = (CiMethod) call.Method.Symbol;
			CiExpr[] args = call.Arguments;
			if (method == CiSystem.StringSubstring && args.Length == 2) {
				cast = false;
				ptr = call.Method.Left;
				offset = args[0];
				length = args[1];
				return true;
			}
			if (method == CiSystem.UTF8GetString) {
				cast = true;
				ptr = args[0];
				offset = args[1];
				length = args[2];
				return true;
			}
		}
		cast = false;
		ptr = null;
		offset = null;
		length = null;
		return false;
	}

	protected static CiExpr IsTrimSubstring(CiBinaryExpr expr)
	{
		if (IsStringSubstring(expr.Right, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr length)
		 && !cast
		 && expr.Left is CiSymbolReference leftSymbol && ptr.IsReferenceTo(leftSymbol.Symbol) // TODO: more complex expr
		 && offset is CiLiteral literalOffset && (long) literalOffset.Value == 0) {
			return length;
		}
		return null;
	}

	protected abstract void WriteConst(CiConst konst);

	public override void Visit(CiConst konst)
	{
		if (konst.Type is CiArrayType)
			WriteConst(konst);
	}

	public override void Visit(CiAssert statement)
	{
		IncludeAssert();
		Write("assert(");
		if (statement.Message == null)
			statement.Cond.Accept(this, CiPriority.Statement);
		else if (statement.Cond is CiLiteral literal && !(bool) literal.Value) {
			Write('!');
			statement.Message.Accept(this, CiPriority.Primary);
		}
		else {
			statement.Cond.Accept(this, CiPriority.CondAnd);
			Write(" && ");
			statement.Message.Accept(this, CiPriority.Statement);
		}
		WriteLine(");");
	}
}

}
