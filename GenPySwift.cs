// GenPySwift.cs - Python/Swift code generator
//
// Copyright (C) 2020-2023  Piotr Fusik
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
using System.Diagnostics;

namespace Foxoft.Ci
{

public abstract class GenPySwift : GenBase
{
	protected override void WriteDocPara(CiDocPara para, bool many)
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
				Write(text.Text);
				break;
			case CiDocCode code:
				WriteChar('`');
				Write(code.Text);
				WriteChar('`');
				break;
			case CiDocLine _:
				WriteLine();
				StartDocLine();
				break;
			default:
				throw new ArgumentException(inline.GetType().Name);
			}
		}
	}

	protected abstract string GetDocBullet();

	protected override void WriteDocList(CiDocList list)
	{
		WriteLine();
		foreach (CiDocPara item in list.Items) {
			Write(GetDocBullet());
			WriteDocPara(item, false);
			WriteLine();
		}
		StartDocLine();
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiMember member) {
			if (member.IsStatic())
				WriteName(this.CurrentMethod.Parent);
			else
				Write("self");
			WriteChar('.');
		}
		WriteName(symbol);
	}

	public override void VisitAggregateInitializer(CiAggregateInitializer expr)
	{
		CiType type = ((CiArrayStorageType) expr.Type).GetElementType();
		Write("[ ");
		WriteCoercedLiterals(type, expr.Items);
		Write(" ]");
	}

	public override void VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			expr.Inner.Accept(this, parent);
			break;
		default:
			base.VisitPrefixExpr(expr, parent);
			break;
		}
	}

	public override void VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			expr.Inner.Accept(this, parent);
			break;
		default:
			base.VisitPostfixExpr(expr, parent);
			break;
		}
	}

	static bool IsPtr(CiExpr expr) => expr.Type is CiClassType klass && klass.Class.Id != CiId.StringClass && !(klass is CiStorageType);

	protected abstract string GetReferenceEqOp(bool not);

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if (IsPtr(expr.Left) || IsPtr(expr.Right))
			WriteBinaryExpr2(expr, parent, CiPriority.Equality, GetReferenceEqOp(not));
		else
			base.WriteEqual(expr, parent, not);
	}

	protected virtual void WriteExpr(CiExpr expr, CiPriority parent) => expr.Accept(this, parent);

	protected void WriteListAppend(CiExpr obj, List<CiExpr> args)
	{
		obj.Accept(this, CiPriority.Primary);
		Write(".append(");
		CiType elementType = ((CiClassType) obj.Type).GetElementType();
		if (args.Count == 0)
			WriteNewStorage(elementType);
		else
			WriteCoerced(elementType, args[0], CiPriority.Argument);
		WriteChar(')');
	}

	protected virtual bool VisitPreCall(CiCallExpr call) => false;

	protected bool VisitXcrement<T>(CiExpr expr, bool write) where T : CiUnaryExpr
	{
		bool seen;
		switch (expr) {
		case CiVar def:
			return def.Value != null && VisitXcrement<T>(def.Value, write);
		case CiAggregateInitializer _:
		case CiLiteral _:
		case CiLambdaExpr _:
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
			if (binary.Op == CiToken.Is)
				return seen;
			if (binary.Op == CiToken.CondAnd || binary.Op == CiToken.CondOr)
				Trace.Assert(!VisitXcrement<T>(binary.Right, false));
			else
				seen |= VisitXcrement<T>(binary.Right, write);
			return seen;
		case CiSelectExpr select:
			seen = VisitXcrement<T>(select.Cond, write);
			Trace.Assert(!VisitXcrement<T>(select.OnTrue, false));
			Trace.Assert(!VisitXcrement<T>(select.OnFalse, false));
			return seen;
		case CiCallExpr call:
			seen = VisitXcrement<T>(call.Method, write);
			foreach (CiExpr arg in call.Arguments)
				seen |= VisitXcrement<T>(arg, write);
			if (typeof(T) == typeof(CiPrefixExpr))
				seen |= VisitPreCall(call);
			return seen;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	public override void VisitExpr(CiExpr statement)
	{
		VisitXcrement<CiPrefixExpr>(statement, true);
		if (!(statement is CiUnaryExpr unary) || (unary.Op != CiToken.Increment && unary.Op != CiToken.Decrement)) {
			WriteExpr(statement, CiPriority.Statement);
			WriteLine();
			if (statement is CiVar def)
				WriteInitCode(def);
		}
		VisitXcrement<CiPostfixExpr>(statement, true);
	}

	protected override void EndStatement() => WriteLine();

	protected abstract void OpenChild();

	protected abstract void CloseChild();

	protected override void WriteChild(CiStatement statement)
	{
		OpenChild();
		statement.AcceptStatement(this);
		CloseChild();
	}

	public override void VisitBlock(CiBlock statement) => WriteStatements(statement.Statements);

	bool OpenCond(string statement, CiExpr cond, CiPriority parent)
	{
		VisitXcrement<CiPrefixExpr>(cond, true);
		Write(statement);
		WriteExpr(cond, parent);
		OpenChild();
		return VisitXcrement<CiPostfixExpr>(cond, true);
	}

	protected virtual void WriteContinueDoWhile(CiExpr cond)
	{
		OpenCond("if ", cond, CiPriority.Argument);
		WriteLine("continue");
		CloseChild();
		VisitXcrement<CiPostfixExpr>(cond, true);
		WriteLine("break");
	}

	protected virtual bool NeedCondXcrement(CiLoop loop) => loop.Cond != null;

	void EndBody(CiLoop loop)
	{
		if (loop is CiFor forLoop) {
			if (forLoop.IsRange)
				return;
			forLoop.Advance?.AcceptStatement(this);
		}
		if (NeedCondXcrement(loop))
			VisitXcrement<CiPrefixExpr>(loop.Cond, true);
	}

	public override void VisitContinue(CiContinue statement)
	{
		if (statement.Loop is CiDoWhile doWhile)
			WriteContinueDoWhile(doWhile.Cond);
		else {
			EndBody(statement.Loop);
			WriteLine("continue");
		}
	}

	void OpenWhileTrue()
	{
		Write("while ");
		VisitLiteralTrue();
		OpenChild();
	}

	protected abstract string GetIfNot();

	public override void VisitDoWhile(CiDoWhile statement)
	{
		OpenWhileTrue();
		statement.Body.AcceptStatement(this);
		if (statement.Body.CompletesNormally()) {
			OpenCond(GetIfNot(), statement.Cond, CiPriority.Primary);
			WriteLine("break");
			CloseChild();
			VisitXcrement<CiPostfixExpr>(statement.Cond, true);
		}
		CloseChild();
	}

	protected virtual void OpenWhile(CiLoop loop)
	{
		OpenCond("while ", loop.Cond, CiPriority.Argument);
	}

	void CloseWhile(CiLoop loop)
	{
		loop.Body.AcceptStatement(this);
		if (loop.Body.CompletesNormally())
			EndBody(loop);
		CloseChild();
		if (NeedCondXcrement(loop)) {
			if (loop.HasBreak && VisitXcrement<CiPostfixExpr>(loop.Cond, false)) {
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

	public override void VisitFor(CiFor statement)
	{
		if (statement.IsRange) {
			CiVar iter = (CiVar) statement.Init;
			Write("for ");
			if (statement.IsIteratorUsed)
				WriteName(iter);
			else
				WriteChar('_');
			Write(" in ");
			WriteForRange(iter, (CiBinaryExpr) statement.Cond, statement.RangeStep);
			WriteChild(statement.Body);
		}
		else {
			statement.Init?.AcceptStatement(this);
			if (statement.Cond != null)
				OpenWhile(statement);
			else
				OpenWhileTrue();
			CloseWhile(statement);
		}
	}

	protected abstract void WriteElseIf();

	public override void VisitIf(CiIf statement)
	{
		bool condPostXcrement = OpenCond("if ", statement.Cond, CiPriority.Argument);
		statement.OnTrue.AcceptStatement(this);
		CloseChild();
		if (statement.OnFalse == null && condPostXcrement && !statement.OnTrue.CompletesNormally())
			VisitXcrement<CiPostfixExpr>(statement.Cond, true);
		else if (statement.OnFalse != null || condPostXcrement) {
			if (!condPostXcrement && statement.OnFalse is CiIf childIf && !VisitXcrement<CiPrefixExpr>(childIf.Cond, false)) {
				WriteElseIf();
				VisitIf(childIf);
			}
			else {
				Write("else");
				OpenChild();
				VisitXcrement<CiPostfixExpr>(statement.Cond, true);
				statement.OnFalse?.AcceptStatement(this);
				CloseChild();
			}
		}
	}

	protected abstract void WriteResultVar();

	public override void VisitReturn(CiReturn statement)
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

	public override void VisitWhile(CiWhile statement)
	{
		OpenWhile(statement);
		CloseWhile(statement);
	}
}

}
