// GenPySwift.cs - Python/Swift code generator
//
// Copyright (C) 2020  Piotr Fusik
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

namespace Foxoft.Ci
{

public abstract class GenPySwift : GenBase
{
	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		CiType type = ((CiArrayStorageType) expr.Type).ElementType;
		Write("[ ");
		WriteCoercedLiterals(type, expr.Items);
		Write(" ]");
		return expr;
	}

	protected override void WriteNew(CiClass klass, CiPriority parent)
	{
		WriteName(klass);
		Write("()");
	}

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			expr.Inner.Accept(this, parent);
			return expr;
		default:
			return base.Visit(expr, parent);
		}
	}

	public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			expr.Inner.Accept(this, parent);
			return expr;
		default:
			return base.Visit(expr, parent);
		}
	}

	protected bool VisitXcrement<T>(CiExpr expr, bool write) where T : CiUnaryExpr
	{
		bool seen;
		switch (expr) {
		case CiVar def:
			return def.Value != null && VisitXcrement<T>(def.Value, write);
		case CiLiteral literal:
			return false;
		case CiInterpolatedString interp:
			seen = false;
			foreach (CiInterpolatedPart part in interp.Parts) {
				if (part.Argument != null)
					seen |= VisitXcrement<T>(part.Argument, write);
			}
			return seen;
		case CiSymbolReference symbol:
			return symbol.Left != null && VisitXcrement<T>(symbol.Left, write);
		case CiUnaryExpr unary:
			seen = VisitXcrement<T>(unary.Inner, write);
			if ((unary.Op == CiToken.Increment || unary.Op == CiToken.Decrement) && unary is T) {
				if (write) {
					unary.Inner.Accept(this, CiPriority.Assign);
					WriteLine(unary.Op == CiToken.Increment ? " += 1" : " -= 1");
				}
				seen = true;
			}
			return seen;
		case CiBinaryExpr binary:
			seen = VisitXcrement<T>(binary.Left, write);
			// XXX: assert not seen on the right side of CondAnd, CondOr
			seen |= VisitXcrement<T>(binary.Right, write);
			return seen;
		case CiCondExpr cond:
			seen = VisitXcrement<T>(cond.Cond, write);
			// XXX: assert not seen in OnTrue and OnFalse
			// seen |= VisitXcrement<T>(cond.OnTrue, write);
			// seen |= VisitXcrement<T>(cond.OnFalse, write);
			return seen;
		case CiCallExpr call:
			seen = VisitXcrement<T>(call.Method, write);
			foreach (CiExpr item in call.Arguments)
				seen |= VisitXcrement<T>(item, write);
			return seen;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	public override void Visit(CiExpr statement)
	{
		VisitXcrement<CiPrefixExpr>(statement, true);
		if (!(statement is CiUnaryExpr unary) || (unary.Op != CiToken.Increment && unary.Op != CiToken.Decrement)) {
			statement.Accept(this, CiPriority.Statement);
			WriteLine();
		}
		VisitXcrement<CiPostfixExpr>(statement, true);
	}
}

}
