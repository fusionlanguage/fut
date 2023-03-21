// GenCCpp.cs - C/C++ code generator
//
// Copyright (C) 2011-2023  Piotr Fusik
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
using System.Linq;

namespace Foxoft.Ci
{

public abstract class GenCCpp : GenCCppD
{
	protected abstract void IncludeStdInt();

	protected abstract void IncludeAssert();

	protected abstract void IncludeMath();

	protected void WriteIncludes() => WriteIncludes("#include <", ">");

	protected override int GetLiteralChars() => 127;

	protected virtual void WriteNumericType(CiId id)
	{
		switch (id) {
		case CiId.SByteRange:
			IncludeStdInt();
			Write("int8_t");
			break;
		case CiId.ByteRange:
			IncludeStdInt();
			Write("uint8_t");
			break;
		case CiId.ShortRange:
			IncludeStdInt();
			Write("int16_t");
			break;
		case CiId.UShortRange:
			IncludeStdInt();
			Write("uint16_t");
			break;
		case CiId.IntType:
			Write("int");
			break;
		case CiId.LongType:
			IncludeStdInt();
			Write("int64_t");
			break;
		case CiId.FloatType:
			Write("float");
			break;
		case CiId.DoubleType:
			Write("double");
			break;
		default:
			throw new NotImplementedException(id.ToString());
		}
	}

	public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.MathNaN:
			IncludeMath();
			Write("NAN");
			break;
		case CiId.MathNegativeInfinity:
			IncludeMath();
			Write("-INFINITY");
			break;
		case CiId.MathPositiveInfinity:
			IncludeMath();
			Write("INFINITY");
			break;
		default:
			base.VisitSymbolReference(expr, parent);
			break;
		}
	}

	protected abstract void WriteEqualString(CiExpr left, CiExpr right, CiPriority parent, bool not);

	static bool IsPtrTo(CiExpr ptr, CiExpr other) => ptr.Type is CiClassType klass && klass.Class.Id != CiId.StringClass && klass.IsAssignableFrom(other.Type);

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
			WriteChar('(');
		WriteCoerced(coercedType, expr.Left, CiPriority.Equality);
		Write(GetEqOp(not));
		WriteCoerced(coercedType, expr.Right, CiPriority.Equality);
		if (parent > CiPriority.Equality)
			WriteChar(')');
	}

	protected static bool IsStringEmpty(CiBinaryExpr expr, out CiExpr str)
	{
		if (expr.Left is CiSymbolReference symbol && symbol.Symbol.Id == CiId.StringLength
			&& expr.Right.IsLiteralZero()) {
			str = symbol.Left;
			return true;
		}
		str = null;
		return false;
	}

	protected abstract void WriteArrayPtr(CiExpr expr, CiPriority parent);

	protected void WriteArrayPtrAdd(CiExpr array, CiExpr index)
	{
		if (index.IsLiteralZero())
			WriteArrayPtr(array, CiPriority.Argument);
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
			List<CiExpr> args = call.Arguments;
			if (method.Id == CiId.StringSubstring && args.Count == 2) {
				cast = false;
				ptr = call.Method.Left;
				offset = args[0];
				length = args[1];
				return true;
			}
			if (method.Id == CiId.UTF8GetString) {
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
		 && offset.IsLiteralZero())
			return length;
		return null;
	}

	protected void WriteStringLiteralWithNewLine(string s)
	{
		WriteChar('"');
		Write(s);
		Write("\\n\"");
	}

	protected virtual void WriteUnreachable(CiAssert statement)
	{
		// TODO: C23, C++23: unreachable()
		Write("abort();");
		if (statement.Message != null) {
			Write(" // ");
			statement.Message.Accept(this, CiPriority.Argument);
		}
		WriteNewLine();
	}

	protected override void WriteAssert(CiAssert statement)
	{
		if (statement.CompletesNormally()) {
			IncludeAssert();
			Write("assert(");
			if (statement.Message == null)
				statement.Cond.Accept(this, CiPriority.Argument);
			else {
				statement.Cond.Accept(this, CiPriority.CondAnd);
				Write(" && ");
				statement.Message.Accept(this, CiPriority.Argument);
			}
			WriteLine(");");
		}
		else
			WriteUnreachable(statement);
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		if (!(statement.Value.Type is CiStringType)) {
			base.VisitSwitch(statement);
			return;
		}

		int gotoId = GetSwitchGoto(statement);
		string op = "if (";
		foreach (CiCase kase in statement.Cases) {
			foreach (CiExpr caseValue in kase.Values) {
				Write(op);
				WriteEqualString(statement.Value, caseValue, kase.Values.Count == 1 ? CiPriority.Argument : CiPriority.CondOr, false); // FIXME: side effect
				op = " || ";
			}
			WriteChar(')');
			WriteIfCaseBody(kase.Body, gotoId < 0);
			op = "else if (";
		}
		EndSwitchAsIfs(statement, gotoId);
	}

	protected void WriteMethods(CiClass klass)
	{
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMethod method) {
				WriteMethod(method);
				this.CurrentTemporaries.Clear();
			}
		}
	}

	protected abstract void WriteClass(CiClass klass);

	protected override void WriteClass(CiClass klass, CiProgram program)
	{
		// topological sorting of class hierarchy and class storage fields
		if (!WriteBaseClass(klass, program))
			return;
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiField field && field.Type.GetBaseType() is CiStorageType storage && storage.Class.Id == CiId.None)
				WriteClass(storage.Class, program);
		}
		WriteClass(klass);
	}
}

}
