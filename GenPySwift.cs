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
	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiMember member) {
			if (member.IsStatic())
				WriteName(this.CurrentMethod.Parent);
			else
				Write("self");
			Write('.');
		}
		WriteName(symbol);
	}

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

	protected abstract void OpenChild();

	protected abstract void CloseChild();

	protected override void WriteChild(CiStatement statement)
	{
		OpenChild();
		statement.Accept(this);
		CloseChild();
	}

	public override void Visit(CiBlock statement)
	{
		Write(statement.Statements);
	}

	protected abstract void WriteContinueDoWhile(CiExpr cond);

	protected void EndBody(CiFor statement)
	{
		if (statement.Advance != null)
			statement.Advance.Accept(this);
		if (statement.Cond != null)
			VisitXcrement<CiPrefixExpr>(statement.Cond, true);
	}

	public override void Visit(CiContinue statement)
	{
		switch (statement.Loop) {
		case CiDoWhile doWhile:
			WriteContinueDoWhile(doWhile.Cond);
			return;
		case CiFor forLoop when !forLoop.IsRange:
			EndBody(forLoop);
			break;
		case CiWhile whileLoop:
			VisitXcrement<CiPrefixExpr>(whileLoop.Cond, true);
			break;
		default:
			break;
		}
		WriteLine("continue");
	}

	protected bool OpenCond(string statement, CiExpr cond, CiPriority parent)
	{
		VisitXcrement<CiPrefixExpr>(cond, true);
		Write(statement);
		cond.Accept(this, parent);
		OpenChild();
		return VisitXcrement<CiPostfixExpr>(cond, true);
	}

	protected abstract void WriteElseIf();

	public override void Visit(CiIf statement)
	{
		bool condPostXcrement = OpenCond("if ", statement.Cond, CiPriority.Statement);
		statement.OnTrue.Accept(this);
		CloseChild();
		if (statement.OnFalse == null && condPostXcrement && !statement.OnTrue.CompletesNormally)
			VisitXcrement<CiPostfixExpr>(statement.Cond, true);
		else if (statement.OnFalse != null || condPostXcrement) {
			if (!condPostXcrement && statement.OnFalse is CiIf childIf && !VisitXcrement<CiPrefixExpr>(childIf.Cond, false)) {
				WriteElseIf();
				Visit(childIf);
			}
			else {
				Write("else");
				OpenChild();
				VisitXcrement<CiPostfixExpr>(statement.Cond, true);
				if (statement.OnFalse != null)
					statement.OnFalse.Accept(this);
				CloseChild();
			}
		}
	}

	protected abstract void WriteResultVar(CiReturn statement);

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return");
		else {
			VisitXcrement<CiPrefixExpr>(statement.Value, true);
			if (VisitXcrement<CiPostfixExpr>(statement.Value, false)) {
				WriteResultVar(statement);// FIXME: name clash? only matters if return ... result++, unlikely
				Write(" = ");
				statement.Value.Accept(this, CiPriority.Statement);
				WriteLine();
				VisitXcrement<CiPostfixExpr>(statement.Value, true);
				WriteLine("return result");
			}
			else {
				Write("return ");
				statement.Value.Accept(this, CiPriority.Statement);
				WriteLine();
			}
		}
	}
}

}
