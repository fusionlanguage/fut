// GenTyped.cs - C/C++/C#/Java code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
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

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Symbol is CiField)
			Write("this.");
		WriteName(expr.Symbol);
		return expr;
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
