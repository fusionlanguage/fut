// CiResolver.cs - Ci symbol resolver
//
// Copyright (C) 2011-2014  Piotr Fusik
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

public class CiResolver : CiVisitor
{
	readonly CiProgram Program;
	CiClass CurrentClass;

	CiException StatementException(CiStatement statement, string message)
	{
		return new CiException(this.CurrentClass.Filename, statement.Line, message);
	}

	CiException StatementException(CiStatement statement, string format, params object[] args)
	{
		return StatementException(statement, string.Format(format, args));
	}

	void ResolveBase(CiClass klass)
	{
		switch (klass.VisitStatus) {
		case CiVisitStatus.NotYet:
			break;
		case CiVisitStatus.InProgress:
			throw new CiException(klass, "Circular inheritance for class {0}", klass.Name);
		case CiVisitStatus.Done:
			return;
		}
		if (klass.BaseClassName != null) {
			CiClass baseClass = Program.TryLookup(klass.BaseClassName) as CiClass;
			if (baseClass == null)
				throw new CiException(klass, "Base class {0} not found", klass.BaseClassName);
			klass.Parent = baseClass;
			klass.VisitStatus = CiVisitStatus.InProgress;
			ResolveBase(baseClass);
		}
		this.Program.Classes.Add(klass);
		klass.VisitStatus = CiVisitStatus.Done;

		foreach (CiConst konst in klass.Consts)
			klass.Add(konst);
		foreach (CiField field in klass.Fields)
			klass.Add(field);
		foreach (CiMethod method in klass.Methods)
			klass.Add(method);
	}

	static long SaturatedNeg(long a)
	{
		if (a == long.MinValue)
			return long.MaxValue;
		return -a;
	}

	static long SaturatedAdd(long a, long b)
	{
		long c = a + b;
		if (c >= 0) {
			if (a < 0 && b < 0)
				return long.MinValue;
		}
		else if (a > 0 && b > 0)
			return long.MaxValue;
		return c;
	}

	static long SaturatedSub(long a, long b)
	{
		if (b == long.MinValue)
			return a < 0 ? a ^ b : long.MaxValue;
		return SaturatedAdd(a, -b);
	}

	static long SaturatedMul(long a, long b)
	{
		if (a == 0 || b== 0)
			return 0;
		if (a == long.MinValue)
			return b >> 63 ^ a;
		if (b == long.MinValue)
			return a >> 63 ^ b;
		if (long.MaxValue / Math.Abs(a) < Math.Abs(b))
			return (a ^ b) >> 63 ^ long.MaxValue;
		return a * b;
	}

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		CiExpr[] items = expr.Items;
		if (items.Length == 0)
			throw StatementException(expr, "Cannot infer type of an empty array");
		items[0] = items[0].Accept(this, parent);
		CiType leftType = items[0].Type;
		for (int i = 1; i < items.Length; i++) {
			items[i] = items[i].Accept(this, parent);
			CiType rightType = items[i].Type;
			if (rightType == leftType)
				continue;
			CiRangeType leftRange = leftType as CiRangeType;
			CiRangeType rightRange = rightType as CiRangeType;
			if (leftRange != null && rightType != null)
				leftType = new CiRangeType(leftRange.Min, leftRange.Max, rightRange.Min, rightRange.Max);
			else if (leftType == CiSystem.DoubleType || rightType == CiSystem.DoubleType)
				leftType = CiSystem.DoubleType;
			else if (leftType == CiSystem.FloatType || rightType == CiSystem.FloatType)
				leftType = CiSystem.FloatType;
			else
				throw StatementException(expr, "Cannot infer type of array");
		}
		expr.Type = new CiArrayStorageType { ElementType = leftType, Length = items.Length };
		return expr;
	}

	public override CiExpr Visit(CiVar expr, CiPriority parent)
	{
		return expr; // TODO
	}

	public override CiExpr Visit(CiLiteral expr, CiPriority parent)
	{
		return expr; // TODO
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Symbol == null) {
			expr.Symbol = this.CurrentClass.TryLookup(expr.Name);
			if (expr.Symbol == null)
				throw StatementException(expr, "{0} not found", expr.Name);
			CiConst konst = expr.Symbol as CiConst;
			if (konst != null) {
				ResolveConst(konst);
				return konst.Value;
			}
			expr.Type = expr.Symbol.Type;
		}
		return expr;
	}

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		CiExpr inner = expr.Inner.Accept(this, parent);
		CiType type;
		switch (expr.Op) {
		case CiToken.Minus:
			if (!(inner.Type is CiNumericType))
				throw StatementException(expr, "Argument of unary minus must be numeric");
			CiRangeType range = inner.Type as CiRangeType;
			if (range != null) {
				if (range.Min == range.Max)
					return new CiLiteral(-range.Min);
				type = new CiRangeType(-range.Max, -range.Min);
			}
			else {
				type = inner.Type;
			}
			return new CiPrefixExpr { Op = expr.Op, Inner = inner, Type = type };
		default:
			 // TODO
			break;
		}
		return expr;
	}

	public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent)
	{
		return expr; // TODO
	}

	static bool IsLong(CiType type)
	{
		if (type == CiSystem.LongType)
			return true;
		CiRangeType range = type as CiRangeType;
		return range != null && (range.Min < int.MinValue || range.Max > int.MaxValue);
	}

	static CiType GetNumericType(CiBinaryExpr expr)
	{
		CiType leftType = expr.Left.Type;
		CiType rightType = expr.Right.Type;
		if (leftType == CiSystem.DoubleType || rightType == CiSystem.DoubleType
		 || leftType == CiSystem.FloatType || rightType == CiSystem.FloatType)
			return CiSystem.DoubleType;
		if (IsLong(leftType) || IsLong(rightType))
			return CiSystem.LongType;
		return CiSystem.IntType;
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		CiExpr left = expr.Left.Accept(this, parent);
		if (expr.Op == CiToken.Dot) {
			string name = ((CiSymbolReference) expr.Right).Name;
			CiSymbol symbol = left.Type.TryLookup(name);
			if (symbol == null)
				throw StatementException(expr, "{0} not found", name);
			if (symbol == CiSystem.StringLength) {
				CiLiteral leftLiteral = left as CiLiteral;
				if (leftLiteral != null)
					return new CiLiteral((long) ((string) leftLiteral.Value).Length);
			}
			return new CiBinaryExpr { Left = left, Op = expr.Op, Right = expr.Right, Type = symbol.Type };
		}
		CiExpr right = expr.Right.Accept(this, parent);
		CiType type;
		CiRangeType leftRange = left.Type as CiRangeType;
		CiRangeType rightRange = right.Type as CiRangeType;
	
		switch (expr.Op) {
		case CiToken.Plus:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedAdd(leftRange.Min, rightRange.Min),
					SaturatedAdd(leftRange.Max, rightRange.Max));
			}
			else if (left.Type is CiStringType || right.Type is CiStringType) {
				CiLiteral leftLiteral = left as CiLiteral;
				CiLiteral rightLiteral = right as CiLiteral;
				if (leftLiteral != null && rightLiteral != null)
					return new CiLiteral(Convert.ToString(leftLiteral.Value, CultureInfo.InvariantCulture) + Convert.ToString(rightLiteral.Value, CultureInfo.InvariantCulture));
				type = CiSystem.StringPtrType;
				// TODO: type check
			}
			else {
				type = GetNumericType(expr);
				// TODO: type check, constant folding
			}
			break;
		case CiToken.Minus:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedSub(leftRange.Min, rightRange.Max),
					SaturatedSub(leftRange.Max, rightRange.Min));
			}
			else {
				type = GetNumericType(expr);
				// TODO: type check, constant folding
			}
			break;
		case CiToken.Asterisk:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedMul(leftRange.Min, rightRange.Min),
					SaturatedMul(leftRange.Min, rightRange.Max),
					SaturatedMul(leftRange.Max, rightRange.Min),
					SaturatedMul(leftRange.Max, rightRange.Max));
			}
			else {
				type = GetNumericType(expr);
				// TODO: type check, constant folding
			}
			break;
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		CiRangeType range = type as CiRangeType;
		if (range != null && range.Min == range.Max)
			return new CiLiteral(range.Min);
		return new CiBinaryExpr { Left = left, Op = expr.Op, Right = right, Type = type };
	}

	public override CiExpr Visit(CiCondExpr expr, CiPriority parent)
	{
		return expr; // TODO
	}

	public override void Visit(CiConst statement)
	{
		// TODO
	}

	public override void Visit(CiExpr statement)
	{
		// TODO
	}

	public override void Visit(CiBlock statement)
	{
		// TODO
	}

	public override void Visit(CiBreak statement)
	{
		// TODO
	}

	public override void Visit(CiContinue statement)
	{
		// TODO
	}

	public override void Visit(CiDelete statement)
	{
		// TODO
	}

	public override void Visit(CiDoWhile statement)
	{
		// TODO
	}

	public override void Visit(CiFor statement)
	{
		// TODO
	}

	public override void Visit(CiIf statement)
	{
		// TODO
	}

	public override void Visit(CiReturn statement)
	{
		// TODO
	}

	public override void Visit(CiSwitch statement)
	{
		// TODO
	}

	public override void Visit(CiThrow statement)
	{
		// TODO
	}

	public override void Visit(CiWhile statement)
	{
		// TODO
	}

	static bool IsMutableType(ref CiExpr expr)
	{
		CiPostfixExpr postfix = expr as CiPostfixExpr;
		if (postfix == null || postfix.Op != CiToken.ExclamationMark)
			return false;
		expr = postfix.Inner;
		return true;
	}

	long FoldConstLong(CiExpr expr)
	{
		CiLiteral literal = expr.Accept(this, CiPriority.Statement) as CiLiteral;
		if (literal == null)
			throw StatementException(expr, "Expected constant value");
		if (literal.Value is long)
			return (long) literal.Value;
		throw StatementException(expr, "Expected integer");
	}

	int FoldConstUint(CiExpr expr)
	{
		long value = FoldConstLong(expr);
		if (value < 0)
			throw StatementException(expr, "Expected non-negative integer");
		if (value > int.MaxValue)
			throw StatementException(expr, "Integer too big");
		return (int) value;
	}

	CiRangeType ToRangeType(long min, CiExpr maxExpr, CiToken op)
	{
		long max = FoldConstLong(maxExpr);
		if (op == CiToken.Less)
			max--;
		if (min > max)
			throw StatementException(maxExpr, "Range min greated than max");
		return new CiRangeType(min, max);
	}

	CiType ToBaseType(CiExpr expr, bool mutable)
	{
		CiSymbolReference symbol = expr as CiSymbolReference;
		if (symbol != null) {
			// built-in, MyEnum, MyClass, MyClass!
			CiType type = this.Program.TryLookup(symbol.Name) as CiType;
			if (type == null)
				throw StatementException(expr, "Type {0} not found", symbol.Name);
			CiClass klass = type as CiClass;
			if (klass != null)
				return new CiClassPtrType { Class = klass, Mutable = mutable };
			if (mutable)
				throw StatementException(expr, "Unexpected exclamation mark on non-reference type");
			return type;
		}

		CiBinaryExpr binary = expr as CiBinaryExpr;
		if (binary != null) {
			if (mutable)
				throw StatementException(expr, "Unexpected exclamation mark on non-reference type");
			switch (binary.Op) {
			case CiToken.LeftParenthesis:
				// string(), MyClass()
				if (binary.RightCollection.Length != 0)
					throw StatementException(binary.Right, "Expected empty parentheses on storage type");
				symbol = binary.Left as CiSymbolReference;
				if (symbol == null)
					throw StatementException(binary.Left, "Expected name of storage type");
				if (symbol.Name == "string")
					return CiSystem.StringStorageType;
				CiClass klass = this.Program.TryLookup(symbol.Name) as CiClass;
				if (klass == null)
					throw StatementException(expr, "Class {0} not found", symbol.Name);
				return klass;
			case CiToken.Less: // a < b
			case CiToken.LessOrEqual: // a <= b
				return ToRangeType(FoldConstLong(binary.Left), binary.Right, binary.Op);
			default:
				throw StatementException(expr, "Invalid type");
			}
		}

		CiPrefixExpr prefix = expr as CiPrefixExpr;
		if (prefix != null) {
			switch (prefix.Op) {
			case CiToken.Less: // <b
			case CiToken.LessOrEqual: // <=b
				return ToRangeType(0, prefix.Inner, prefix.Op);
			default:
				break;
			}
		}

		throw StatementException(expr, "Invalid type");
	}

	CiType ToType(CiExpr expr)
	{
		if (expr == null)
			return null; // void
		bool mutable = IsMutableType(ref expr);
		CiArrayType outerArray = null; // left-most in source
		CiArrayType innerArray = null; // right-most in source
		do {
			CiBinaryExpr binary = expr as CiBinaryExpr;
			if (binary == null || binary.Op != CiToken.LeftBracket)
				break;
			if (binary.Right != null) {
				if (mutable)
					throw StatementException(expr, "Unexpected exclamation mark on non-reference type");
				outerArray = new CiArrayStorageType { Length = FoldConstUint(binary.Right), ElementType = outerArray };
			}
			else
				outerArray = new CiArrayPtrType { Mutable = mutable, ElementType = outerArray };
			if (innerArray == null)
				innerArray = outerArray;
			expr = binary.Left;
			mutable = IsMutableType(ref expr);
		} while (outerArray is CiArrayPtrType);

		CiType baseType = ToBaseType(expr, mutable);
		if (outerArray == null)
			return baseType;
		innerArray.ElementType = baseType;
		return outerArray;
	}

	void ResolveConst(CiConst konst)
	{
		switch (konst.VisitStatus) {
		case CiVisitStatus.NotYet:
			break;
		case CiVisitStatus.InProgress:
			throw StatementException(konst, "Circular dependency in value of constant {0}", konst.Name);
		case CiVisitStatus.Done:
			return;
		}
		konst.Value = konst.Value.Accept(this, CiPriority.Statement);
		konst.Type = konst.Value.Type;
		konst.VisitStatus = CiVisitStatus.Done;
	}

	void ResolveConsts(CiClass klass)
	{
		foreach (CiConst konst in klass.Consts) {
			this.CurrentClass = klass;
			ResolveConst(konst);
		}
	}

	void ResolveTypes(CiClass klass)
	{
		this.CurrentClass = klass;
		foreach (CiField field in klass.Fields)
			field.Type = ToType(field.TypeExpr);
		foreach (CiMethod method in klass.Methods) {
			method.Type = ToType(method.TypeExpr);
			foreach (CiVar param in method.Parameters)
				param.Type = ToType(param.TypeExpr);
		}
	}

	public CiResolver(CiProgram program)
	{
		this.Program = program;
		foreach (CiClass klass in program.OfType<CiClass>())
			ResolveBase(klass);
		foreach (CiClass klass in program.Classes)
			ResolveConsts(klass);
		foreach (CiClass klass in program.Classes)
			ResolveTypes(klass);
	}
}

}
