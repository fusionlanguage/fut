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

public class CiResolver
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
		// TODO: constant folding
		CiLiteral literal = expr as CiLiteral;
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
			ResolveTypes(klass);
	}
}

}
