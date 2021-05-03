// GenPySwift.cs - Python/Swift code generator
//
// Copyright (C) 2020-2021  Piotr Fusik
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
	void WriteDoc(string text)
	{
		foreach (char c in text) {
			if (c == '\n') {
				WriteLine();
				StartDocLine();
			}
			else
				Write(c);
		}
	}

	protected override void Write(CiDocPara para, bool many)
	{
		if (many) {
			WriteLine();
			StartDocLine();
			WriteLine();
			StartDocLine();
		}
		foreach (CiDocInline inline in para.Children) {
			switch (inline) {
			case CiDocText text:
				WriteDoc(text.Text);
				break;
			case CiDocCode code:
				Write('`');
				WriteDoc(code.Text);
				Write('`');
				break;
			default:
				throw new ArgumentException(inline.GetType().Name);
			}
		}
	}

	protected abstract string DocBullet { get; }

	protected override void Write(CiDocList list)
	{
		WriteLine();
		foreach (CiDocPara item in list.Items) {
			Write(this.DocBullet);
			Write(item, false);
			WriteLine();
		}
		StartDocLine();
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiMember member && this.CurrentMethod != null) {
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

	protected virtual void WriteExpr(CiExpr expr, CiPriority parent)
	{
		expr.Accept(this, parent);
	}

	protected virtual bool VisitPreCall(CiCallExpr call) => false;

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
			foreach (CiInterpolatedPart part in interp.Parts)
				seen |= VisitXcrement<T>(part.Argument, write);
			return seen;
		case CiSymbolReference symbol:
			return symbol.Left != null && VisitXcrement<T>(symbol.Left, write);
		case CiUnaryExpr unary:
			if (unary.Inner == null) // new C()
				return false;
			seen = VisitXcrement<T>(unary.Inner, write);
			if ((unary.Op == CiToken.Increment || unary.Op == CiToken.Decrement) && unary is T) {
				if (write) {
					WriteExpr(unary.Inner, CiPriority.Assign);
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
		case CiSelectExpr select:
			seen = VisitXcrement<T>(select.Cond, write);
			// XXX: assert not seen in OnTrue and OnFalse
			// seen |= VisitXcrement<T>(select.OnTrue, write);
			// seen |= VisitXcrement<T>(select.OnFalse, write);
			return seen;
		case CiCallExpr call:
			seen = VisitXcrement<T>(call.Method, write);
			foreach (CiExpr item in call.Arguments)
				seen |= VisitXcrement<T>(item, write);
			if (typeof(T) == typeof(CiPrefixExpr))
				seen |= VisitPreCall(call);
			return seen;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	public override void Visit(CiExpr statement)
	{
		VisitXcrement<CiPrefixExpr>(statement, true);
		if (!(statement is CiUnaryExpr unary) || (unary.Op != CiToken.Increment && unary.Op != CiToken.Decrement)) {
			WriteExpr(statement, CiPriority.Statement);
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
		WriteExpr(cond, parent);
		OpenChild();
		return VisitXcrement<CiPostfixExpr>(cond, true);
	}

	void CloseWhile(CiLoop loop)
	{
		CloseChild();
		if (loop.Cond != null && VisitXcrement<CiPostfixExpr>(loop.Cond, false)) {
			if (loop.HasBreak) {
				Write("else");
				OpenChild();
				VisitXcrement<CiPostfixExpr>(loop.Cond, true);
				CloseChild();
			}
			else
				VisitXcrement<CiPostfixExpr>(loop.Cond, true);
		}
	}

	protected abstract void WriteForRange(CiVar iter, CiBinaryExpr cond, long rangeStep);

	public override void Visit(CiFor statement)
	{
		if (statement.IsRange) {
			CiVar iter = (CiVar) statement.Init;
			Write("for ");
			if (statement.IsIteratorUsed)
				WriteName(iter);
			else
				Write('_');
			Write(" in ");
			WriteForRange(iter, (CiBinaryExpr) statement.Cond, statement.RangeStep);
			WriteChild(statement.Body);
		}
		else {
			if (statement.Init != null)
				statement.Init.Accept(this);
			if (statement.Cond != null)
				OpenCond("while ", statement.Cond, CiPriority.Argument);
			else {
				Write("while ");
				WriteLiteral(true);
				OpenChild();
			}
			statement.Body.Accept(this);
			if (statement.Body.CompletesNormally)
				EndBody(statement);
			CloseWhile(statement);
		}
	}

	protected abstract void WriteElseIf();

	public override void Visit(CiIf statement)
	{
		bool condPostXcrement = OpenCond("if ", statement.Cond, CiPriority.Argument);
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

	protected abstract void WriteResultVar();

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return");
		else {
			VisitXcrement<CiPrefixExpr>(statement.Value, true);
			if (VisitXcrement<CiPostfixExpr>(statement.Value, false)) {
				WriteResultVar(); // FIXME: name clash? only matters if return ... result++, unlikely
				Write(" = ");
				WriteCoercedExpr(this.CurrentMethod.Type, statement.Value);
				WriteLine();
				VisitXcrement<CiPostfixExpr>(statement.Value, true);
				WriteLine("return result");
			}
			else {
				Write("return ");
				WriteCoercedExpr(this.CurrentMethod.Type, statement.Value);
				WriteLine();
			}
		}
	}

	public override void Visit(CiWhile statement)
	{
		OpenCond("while ", statement.Cond, CiPriority.Argument);
		statement.Body.Accept(this);
		VisitXcrement<CiPrefixExpr>(statement.Cond, true);
		CloseWhile(statement);
	}
}

}
