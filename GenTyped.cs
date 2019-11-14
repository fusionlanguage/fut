// GenTyped.cs - C/C++/C#/Java code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
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
using System.Linq;

namespace Foxoft.Ci
{

public abstract class GenTyped : GenBase
{
	protected virtual TypeCode GetTypeCode(CiIntegerType integer, bool promote)
	{
		if (integer == CiSystem.LongType)
			return TypeCode.Int64;
		if (promote || integer == CiSystem.IntType)
			return TypeCode.Int32;
		CiRangeType range = (CiRangeType) integer;
		if (range.Min < 0) {
			if (range.Min < short.MinValue || range.Max > short.MaxValue)
				return TypeCode.Int32;
			if (range.Min < sbyte.MinValue || range.Max > sbyte.MaxValue)
				return TypeCode.Int16;
			return TypeCode.SByte;
		}
		if (range.Max > ushort.MaxValue)
			return TypeCode.Int32;
		if (range.Max > byte.MaxValue)
			return TypeCode.UInt16;
		return TypeCode.Byte;
	}

	protected abstract void Write(TypeCode typeCode);

	TypeCode GetTypeCode(CiType type, bool promote)
	{
		if (type is CiNumericType) {
			if (type is CiIntegerType integer)
				return GetTypeCode(integer, promote);
			if (type == CiSystem.DoubleType)
				return TypeCode.Double;
			if (type == CiSystem.FloatType)
				return TypeCode.Single;
			throw new NotImplementedException(type.ToString());
		}
		else if (type == CiSystem.BoolType)
			return TypeCode.Boolean;
		else if (type == CiSystem.NullType)
			return TypeCode.Empty;
		else if (type is CiStringType)
			return TypeCode.String;
		return TypeCode.Object;
	}

	protected abstract void Write(CiType type, bool promote);

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		Write(value.Type, true);
		Write(' ');
		WriteName(value);
	}

	static char GetPrintfFormat(CiType type)
	{
		switch (type) {
		case CiStringType _:
			return 's';
		case CiIntegerType _:
			return 'd';
		case CiNumericType _:
			return 'f';
		default:
			throw new NotImplementedException(type.ToString());
		}
	}

	protected void WriteArgs(CiInterpolatedString expr)
	{
		foreach (CiInterpolatedPart part in expr.Parts) {
			if (part.Argument != null) {
				Write(", ");
				part.Argument.Accept(this, CiPriority.Statement);
			}
		}
	}

	protected void WritePrintf(CiInterpolatedString expr, bool newLine)
	{
		Write('"');
		foreach (CiInterpolatedPart part in expr.Parts) {
			foreach (char c in part.Prefix) {
				if (c == '%')
					Write("%%");
				else
					WriteEscapedChar(c);
			}
			if (part.Argument != null) {
				Write('%');
				if (part.WidthExpr != null)
					Write(part.Width);
				if (part.Format != ' ')
					Write(part.Format);
				else
					Write(GetPrintfFormat(part.Argument.Type));
			}
		}
		if (newLine)
			Write("\\n");
		Write('"');
		WriteArgs(expr);
		Write(')');
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left == null && expr.Symbol is CiField)
			Write("this.");
		return base.Visit(expr, parent);
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Write("new ");
		Write(elementType.BaseType, false);
		Write('[');
		lengthExpr.Accept(this, CiPriority.Statement);
		Write(']');
		while (elementType is CiArrayType array) {
			Write('[');
			if (array is CiArrayStorageType arrayStorage)
				arrayStorage.LengthExpr.Accept(this, CiPriority.Statement);
			Write(']');
			elementType = array.ElementType;
		}
	}

	static bool IsAscii(long c)
	{
		if (c >= ' ' && c <= 0x7e)
			return true;
		switch (c) {
		case '\a':
		case '\b':
		case '\f':
		case '\n':
		case '\r':
		case '\t':
		case '\v':
			return true;
		default:
			return false;
		}
	}

	protected bool IsOneAsciiString(CiExpr expr, out char c)
	{
		if (expr is CiLiteral literal && literal.Value is string s && s.Length == 1 && IsAscii(s[0])) {
			c = s[0];
			return true;
		}
		c = '\0';
		return false;
	}

	protected void WriteCharLiteral(char c)
	{
		Write('\'');
		WriteEscapedChar(c);
		Write('\'');
	}

	protected override void WriteComparison(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
	{
		if (expr.Left.IsIndexing && expr.Left is CiBinaryExpr indexing && indexing.Left.Type is CiStringType
		 && expr.Right is CiLiteral literal && literal.Value is long c && IsAscii(c)) {
			if (parent > child)
				Write('(');
			expr.Left.Accept(this, child);
			Write(op);
			WriteCharLiteral((char) c);
			if (parent > child)
				Write(')');
		}
		else
			base.WriteComparison(expr, parent, child, op);
	}

	static bool IsNarrower(TypeCode left, TypeCode right)
	{
		switch (left) {
		case TypeCode.SByte:
			switch (right) {
			case TypeCode.Byte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Byte:
			switch (right) {
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Int16:
			switch (right) {
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.UInt16:
			switch (right) {
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Int32:
			return right == TypeCode.Int64;
		default:
			return false;
		}
	}

	protected override void WriteAssignRight(CiBinaryExpr expr)
	{
		if (expr.Left.IsIndexing) {
			TypeCode leftTypeCode = GetTypeCode(expr.Left.Type, false);
			bool promote;
			switch (expr.Right) {
			case CiLiteral rightLiteral:
			case CiBinaryExpr rightBinary when rightBinary.Op == CiToken.LeftBracket || rightBinary.IsAssign:
				promote = false;
				break;
			default:
				promote = true;
				break;
			}
			TypeCode rightTypeCode = GetTypeCode(expr.Right.Type, promote);
			if (leftTypeCode == TypeCode.SByte && rightTypeCode == TypeCode.SByte) {
				expr.Right.Accept(this, CiPriority.Assign); // omit Java "& 0xff"
				return;
			}
			if (IsNarrower(leftTypeCode, rightTypeCode)) {
				Write('(');
				Write(leftTypeCode);
				Write(") ");
				expr.Right.Accept(this, CiPriority.Primary);
				return;
			}
		}
		WriteCoerced(expr.Left.Type, expr.Right, CiPriority.Statement);
	}

	protected virtual void WriteStaticCast(string type, CiExpr expr)
	{
		Write('(');
		Write(type);
		Write(") ");
		expr.Accept(this, CiPriority.Primary);
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		if (type != CiSystem.LongType && expr.Type == CiSystem.LongType)
			WriteStaticCast("int", expr);
		else if (type == CiSystem.FloatType && expr.Type == CiSystem.DoubleType) {
			if (expr is CiLiteral) {
				expr.Accept(this, CiPriority.Statement);
				Write('f');
			}
			else
				WriteStaticCast("float", expr);
		}
		else
			base.WriteCoercedInternal(type, expr, parent);
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		WriteIndexing(expr, CiPriority.Statement);
	}

	protected abstract bool HasInitCode(CiNamedValue def);

	protected virtual bool NeedsConstructor(CiClass klass)
	{
		return klass.Constructor != null
			|| klass.Fields.Any(field => HasInitCode(field));
	}

	protected void WriteParameters(CiMethod method, bool first, bool defaultArguments)
	{
		foreach (CiVar param in method.Parameters) {
			if (!first)
				Write(", ");
			first = false;
			WriteTypeAndName(param);
			if (defaultArguments)
				WriteVarInit(param);
		}
		Write(')');
	}

	protected void WriteParameters(CiMethod method, bool defaultArguments)
	{
		Write('(');
		WriteParameters(method, true, defaultArguments);
	}

	protected void WritePublic(CiContainerType container)
	{
		if (container.IsPublic)
			Write("public ");
	}

	protected void OpenClass(CiClass klass, string suffix, string extendsClause)
	{
		Write("class ");
		Write(klass.Name);
		Write(suffix);
		if (klass.BaseClassName != null) {
			Write(extendsClause);
			Write(klass.BaseClassName);
		}
		WriteLine();
		OpenBlock();
	}
}

}
