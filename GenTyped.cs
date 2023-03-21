// GenTyped.cs - C/C++/C#/D/Java code generator
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

namespace Foxoft.Ci
{

public abstract class GenTyped : GenBase
{
	protected abstract void WriteType(CiType type, bool promote);

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteType(value.Type, true);
		WriteChar(' ');
		WriteName(value);
	}

	public override void VisitLiteralDouble(double value)
	{
		base.VisitLiteralDouble(value);
		if ((float) value == value)
			WriteChar('f');
	}

	public override void VisitAggregateInitializer(CiAggregateInitializer expr)
	{
		CiType type = ((CiArrayStorageType) expr.Type).GetElementType();
		Write("{ ");
		WriteCoercedLiterals(type, expr.Items);
		Write(" }");
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Write("new ");
		WriteType(elementType.GetBaseType(), false);
		WriteChar('[');
		lengthExpr.Accept(this, CiPriority.Argument);
		WriteChar(']');
		while (elementType.IsArray()) {
			WriteChar('[');
			if (elementType is CiArrayStorageType arrayStorage)
				arrayStorage.LengthExpr.Accept(this, CiPriority.Argument);
			WriteChar(']');
			elementType = ((CiClassType) elementType).GetElementType();
		}
	}

	protected bool IsOneAsciiString(CiExpr expr, out char c)
	{
		if (expr is CiLiteralString literal) {
			int i = literal.GetOneAscii();
			if (i >= 0) {
				c = (char) i;
				return true;
			}
		}
		c = '\0';
		return false;
	}

	protected static bool IsNarrower(CiId left, CiId right)
	{
		switch (left) {
		case CiId.SByteRange:
			switch (right) {
			case CiId.ByteRange:
			case CiId.ShortRange:
			case CiId.UShortRange:
			case CiId.IntType:
			case CiId.LongType:
				return true;
			default:
				return false;
			}
		case CiId.ByteRange:
			switch (right) {
			case CiId.SByteRange:
			case CiId.ShortRange:
			case CiId.UShortRange:
			case CiId.IntType:
			case CiId.LongType:
				return true;
			default:
				return false;
			}
		case CiId.ShortRange:
			switch (right) {
			case CiId.UShortRange:
			case CiId.IntType:
			case CiId.LongType:
				return true;
			default:
				return false;
			}
		case CiId.UShortRange:
			switch (right) {
			case CiId.ShortRange:
			case CiId.IntType:
			case CiId.LongType:
				return true;
			default:
				return false;
			}
		case CiId.IntType:
			return right == CiId.LongType;
		default:
			return false;
		}
	}

	protected CiExpr GetStaticCastInner(CiType type, CiExpr expr)
	{
		if (expr is CiBinaryExpr binary && binary.Op == CiToken.And && binary.Right is CiLiteralLong rightMask
		 && type is CiIntegerType) {
			long mask;
			switch (GetTypeId(type, false)) {
			case CiId.ByteRange:
			case CiId.SByteRange:
				mask = 0xff;
				break;
			case CiId.ShortRange:
			case CiId.UShortRange:
				mask = 0xffff;
				break;
			case CiId.IntType:
				mask = 0xffffffff;
				break;
			default:
				return expr;
			}
			if ((rightMask.Value & mask) == mask)
				return binary.Left;
		}
		return expr;
	}

	protected void WriteStaticCastType(CiType type)
	{
		WriteChar('(');
		WriteType(type, false);
		Write(") ");
	}

	protected virtual void WriteStaticCast(CiType type, CiExpr expr)
	{
		WriteStaticCastType(type);
		GetStaticCastInner(type, expr).Accept(this, CiPriority.Primary);
	}

	protected override void WriteNotPromoted(CiType type, CiExpr expr)
	{
		if (type is CiIntegerType
		 && IsNarrower(GetTypeId(type, false), GetTypeId(expr.Type, true)))
			WriteStaticCast(type, expr);
		else
			expr.Accept(this, CiPriority.Argument);
	}

	protected virtual bool IsPromoted(CiExpr expr)
	{
		switch (expr) {
		case CiLiteral _:
		case CiBinaryExpr binary when binary.Op == CiToken.LeftBracket || binary.IsAssign():
			return false;
		default:
			return true;
		}
	}

	protected override void WriteAssignRight(CiBinaryExpr expr)
	{
		if (expr.Left.IsIndexing()) {
			if (expr.Right is CiLiteralLong) {
				WriteCoercedLiteral(expr.Left.Type, expr.Right);
				return;
			}
			CiId leftTypeId = GetTypeId(expr.Left.Type, false);
			CiId rightTypeId = GetTypeId(expr.Right.Type, IsPromoted(expr.Right));
			if (leftTypeId == CiId.SByteRange && rightTypeId == CiId.SByteRange) {
				expr.Right.Accept(this, CiPriority.Assign); // omit Java "& 0xff"
				return;
			}
			if (IsNarrower(leftTypeId, rightTypeId)) {
				WriteStaticCast(expr.Left.Type, expr.Right);
				return;
			}
		}
		base.WriteAssignRight(expr);
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		if (type is CiIntegerType && type.Id != CiId.LongType && expr.Type.Id == CiId.LongType)
			WriteStaticCast(type, expr);
		else if (type.Id == CiId.FloatType && expr.Type.Id == CiId.DoubleType) {
			if (expr is CiLiteralDouble literal) {
				base.VisitLiteralDouble(literal.Value);
				WriteChar('f');
			}
			else
				WriteStaticCast(type, expr);
		}
		else if (type is CiIntegerType && expr.Type.Id == CiId.FloatIntType) {
			if (expr is CiCallExpr call && call.Method.Symbol.Id == CiId.MathTruncate) {
				expr = call.Arguments[0];
				if (expr is CiLiteralDouble literal) {
					VisitLiteralLong((long) literal.Value); // TODO: range check
					return;
				}
			}
			WriteStaticCast(type, expr);
		}
		else
			base.WriteCoercedInternal(type, expr, parent);
	}

	protected override void WriteCharAt(CiBinaryExpr expr) => WriteIndexing(expr.Left, expr.Right);

	protected override void StartTemporaryVar(CiType type)
	{
		WriteType(type, true);
		WriteChar(' ');
	}

	protected override void WriteAssertCast(CiBinaryExpr expr)
	{
		CiVar def = (CiVar) expr.Right;
		WriteTypeAndName(def);
		Write(" = ");
		WriteStaticCast(def.Type, expr.Left);
		WriteCharLine(';');
	}
}

}
