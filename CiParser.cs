// CiParser.cs - Ci parser
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
using System.IO;

namespace Foxoft.Ci
{

public class CiParser : CiLexer
{
	readonly CiScope Symbols;

	public CiParser()
	{
		CiScope globals = new CiScope();
		// TODO: add built-in types and constants
		this.Symbols = new CiScope { Parent = globals };
	}

	string ParseId()
	{
		object id = this.CurrentValue;
		Expect(CiToken.Id);
		return (string) id;
	}

	CiExpr ParseSymbolReference()
	{
		return new CiSymbolReference { Line = this.Line, Name = ParseId() };
	}

	CiExpr ParseConstInitializer()
	{
		if (Eat(CiToken.LeftBrace))
			return ParseCollection(CiToken.RightBrace);
		return ParseExpr();
	}

	CiCollection ParseCollection(CiToken closing)
	{
		int line = this.Line;
		List<CiExpr> items = new List<CiExpr>();
		if (!See(closing)) {
			do
				items.Add(ParseConstInitializer());
			while (Eat(CiToken.Comma));
		}
		Expect(closing);
		return new CiCollection { Line = line, Items = items.ToArray() };
	}

	CiExpr ParseParenthesized()
	{
		Expect(CiToken.LeftParenthesis);
		CiExpr result = ParseExpr();
		Expect(CiToken.RightParenthesis);
		return result;
	}

	CiExpr ParsePrimaryExpr()
	{
		CiExpr result;
		switch (this.CurrentToken) {
		case CiToken.Increment:
		case CiToken.Decrement:
		case CiToken.Minus:
		case CiToken.Tilde:
		case CiToken.ExclamationMark:
		case CiToken.New:
			return new CiUnaryExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr() };
		case CiToken.Literal:
			result = new CiLiteral { Line = this.Line, Value = this.CurrentValue };
			NextToken();
			break;
		case CiToken.LeftParenthesis:
			result = ParseParenthesized();
			break;
		case CiToken.Id:
			result = ParseSymbolReference();
			break;
		default:
			throw new ParseException("Invalid expression");
		}
		for (;;) {
			switch (this.CurrentToken) {
			case CiToken.Dot:
				result = new CiBinaryExpr { Line = this.Line, Left = result, Op = NextToken(), Right = ParseSymbolReference() };
				break;
			case CiToken.LeftParenthesis:
				result = new CiBinaryExpr { Line = this.Line, Left = result, Op = NextToken(), Right = ParseCollection(CiToken.RightParenthesis) };
				break;
			case CiToken.LeftBracket:
				result = new CiBinaryExpr { Line = this.Line, Left = result, Op = NextToken(), Right = See(CiToken.RightBracket) ? null : ParseExpr() };
				Expect(CiToken.RightBracket);
				break;
			case CiToken.Increment:
			case CiToken.Decrement:
			case CiToken.ExclamationMark:
				return new CiPostfixExpr { Line = this.Line, Inner = result, Op = NextToken() };
			default:
				return result;
			}
		}
	}

	CiExpr ParseMulExpr()
	{
		CiExpr left = ParsePrimaryExpr();
		for (;;) {
			switch (this.CurrentToken) {
			case CiToken.Asterisk:
			case CiToken.Slash:
			case CiToken.Mod:
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParsePrimaryExpr() };
				break;
			default:
				return left;
			}
		}
	}

	CiExpr ParseAddExpr()
	{
		CiExpr left = ParseMulExpr();
		while (See(CiToken.Plus) || See(CiToken.Minus))
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseMulExpr() };
		return left;
	}

	CiExpr ParseShiftExpr()
	{
		CiExpr left = ParseAddExpr();
		while (See(CiToken.ShiftLeft) || See(CiToken.ShiftRight))
			left = new CiBinaryExpr { Line = this.Line, Left =left, Op = NextToken(), Right = ParseAddExpr() };
		return left;
	}

	CiExpr ParseRelExpr()
	{
		if (See(CiToken.Less) || See(CiToken.LessOrEqual))
			return new CiUnaryExpr { Line = this.Line, Op = NextToken(), Inner = ParseShiftExpr() };
		CiExpr left = ParseShiftExpr();
		for (;;) {
			switch (this.CurrentToken) {
			case CiToken.Less:
			case CiToken.LessOrEqual:
			case CiToken.Greater:
			case CiToken.GreaterOrEqual:
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseShiftExpr() };
				break;
			default:
				return left;
			}
		}
	}

	CiExpr ParseEqualityExpr()
	{
		CiExpr left = ParseRelExpr();
		while (See(CiToken.Equal) || See(CiToken.NotEqual))
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseRelExpr() };
		return left;
	}

	CiExpr ParseAndExpr()
	{
		CiExpr left = ParseEqualityExpr();
		while (See(CiToken.And))
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseEqualityExpr() };
		return left;
	}

	CiExpr ParseXorExpr()
	{
		CiExpr left = ParseAndExpr();
		while (See(CiToken.Xor))
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAndExpr() };
		return left;
	}

	CiExpr ParseOrExpr()
	{
		CiExpr left = ParseXorExpr();
		while (See(CiToken.Or))
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseXorExpr() };
		return left;
	}

	CiExpr ParseCondAndExpr()
	{
		CiExpr left = ParseOrExpr();
		while (See(CiToken.CondAnd))
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseOrExpr() };
		return left;
	}

	CiExpr ParseCondOrExpr()
	{
		CiExpr left = ParseCondAndExpr();
		while (See(CiToken.CondOr))
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseCondAndExpr() };
		return left;
	}

	CiExpr ParseExpr()
	{
		CiExpr left = ParseCondOrExpr();
		if (See(CiToken.QuestionMark)) {
			CiCondExpr result = new CiCondExpr { Line = this.Line, Cond = left };
			NextToken();
			result.OnTrue = ParseExpr();
			Expect(CiToken.Colon);
			result.OnFalse = ParseExpr();
			return result;
		}
		return left;
	}

	CiExpr ParseType()
	{
		return ParseRelExpr();
	}

	CiExpr ParseAssign(bool allowVar)
	{
		CiExpr left = ParseExpr();
		switch (this.CurrentToken) {
		case CiToken.Assign:
		case CiToken.AddAssign:
		case CiToken.SubAssign:
		case CiToken.MulAssign:
		case CiToken.DivAssign:
		case CiToken.ModAssign:
		case CiToken.AndAssign:
		case CiToken.OrAssign:
		case CiToken.XorAssign:
		case CiToken.ShiftLeftAssign:
		case CiToken.ShiftRightAssign:
			return new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAssign(false) };
		case CiToken.Id:
			if (allowVar)
				return ParseVar(left);
			return left;
		default:
			return left;
		}
	}

	CiVar ParseVar(CiExpr type)
	{
		CiVar def = new CiVar { Type = type, Name = ParseId() };
		if (Eat(CiToken.Assign))
			def.Value = ParseAssign(false);
		return def;
	}

	CiVar ParseVar()
	{
		return ParseVar(ParseType());
	}

	CiConst ParseConst()
	{
		Expect(CiToken.Const);
		CiConst konst = new CiConst { Type = ParseType(), Name = ParseId() };
		Expect(CiToken.Assign);
		konst.Value = ParseConstInitializer();
		Expect(CiToken.Semicolon);
		return konst;
	}

	CiBlock ParseBlock()
	{
		Expect(CiToken.LeftBrace);
		List<CiStatement> statements = new List<CiStatement>();
		while (!Eat(CiToken.RightBrace))
			statements.Add(ParseStatement());
		return new CiBlock { Statements = statements.ToArray() };
	}

	CiBreak ParseBreak()
	{
		CiBreak result = new CiBreak { Line = this.Line };
		Expect(CiToken.Break);
		Expect(CiToken.Semicolon);
		return result;
	}

	CiContinue ParseContinue()
	{
		CiContinue result = new CiContinue { Line = this.Line };
		Expect(CiToken.Continue);
		Expect(CiToken.Semicolon);
		return result;
	}

	CiDelete ParseDelete()
	{
		CiDelete result = new CiDelete();
		Expect(CiToken.Delete);
		result.Expr = ParseExpr();
		Expect(CiToken.Semicolon);
		return result;
	}

	CiDoWhile ParseDoWhile()
	{
		CiDoWhile result = new CiDoWhile();
		Expect(CiToken.Do);
		result.Body = ParseStatement();
		Expect(CiToken.While);
		result.Cond = ParseParenthesized();
		Expect(CiToken.Semicolon);
		return result;
	}

	CiFor ParseFor()
	{
		CiFor result = new CiFor();
		Expect(CiToken.For);
		Expect(CiToken.LeftParenthesis);
		if (!See(CiToken.Semicolon))
			result.Init = ParseAssign(true);
		Expect(CiToken.Semicolon);
		if (!See(CiToken.Semicolon))
			result.Cond = ParseExpr();
		Expect(CiToken.Semicolon);
		if (!See(CiToken.RightParenthesis))
			result.Advance = ParseAssign(false);
		Expect(CiToken.RightParenthesis);
		result.Body = ParseStatement();
		return result;
	}

	CiIf ParseIf()
	{
		CiIf result = new CiIf();
		Expect(CiToken.If);
		result.Cond = ParseParenthesized();
		result.OnTrue = ParseStatement();
		if (Eat(CiToken.Else))
			result.OnFalse = ParseStatement();
		return result;
	}

	CiReturn ParseReturn()
	{
		CiReturn result = new CiReturn { Line = this.Line };
		Expect(CiToken.Return);
		if (!See(CiToken.Semicolon))
			result.Value = ParseExpr();
		Expect(CiToken.Semicolon);
		return result;
	}

	CiSwitch ParseSwitch()
	{
		CiSwitch result = new CiSwitch();
		Expect(CiToken.Switch);
		result.Value = ParseParenthesized();
		Expect(CiToken.LeftBrace);

		List<CiCase> cases = new List<CiCase>();
		while (Eat(CiToken.Case)) {
			List<CiExpr> values = new List<CiExpr>();
			do {
				values.Add(ParseExpr());
				Expect(CiToken.Colon);
			} while (Eat(CiToken.Case));
			if (See(CiToken.Default))
				throw new ParseException("Please remove case before default");
			CiCase kase = new CiCase { Values = values.ToArray() };

			List<CiStatement> statements = new List<CiStatement>();
			for (;;) {
				statements.Add(ParseStatement());
				switch (this.CurrentToken) {
				case CiToken.Case:
				case CiToken.Default:
				case CiToken.RightBrace:
					break;
				case CiToken.Goto:
					NextToken();
					switch (this.CurrentToken) {
					case CiToken.Case:
						NextToken();
						kase.Fallthrough = ParseExpr();
						break;
					case CiToken.Default:
						kase.Fallthrough = new CiGotoDefault { Line = this.Line };
						NextToken();
						break;
					default:
						throw new ParseException("Expected goto case or goto default");
					}
					Expect(CiToken.Semicolon);
					break;
				default:
					continue;
				}
				break;
			}
			kase.Body = statements.ToArray();
			cases.Add(kase);
		}
		if (cases.Count == 0)
			throw new ParseException("Switch with no cases");
		result.Cases = cases.ToArray();

		if (Eat(CiToken.Default)) {
			Expect(CiToken.Colon);
			List<CiStatement> statements = new List<CiStatement>();
			do
				statements.Add(ParseStatement());
			while (!See(CiToken.RightBrace));
			result.DefaultBody = statements.ToArray();
		}

		Expect(CiToken.RightBrace);
		return result;
	}

	CiThrow ParseThrow()
	{
		CiThrow result = new CiThrow { Line = this.Line };
		Expect(CiToken.Throw);
		result.Message = ParseExpr();
		Expect(CiToken.Semicolon);
		return result;
	}

	CiWhile ParseWhile()
	{
		CiWhile result = new CiWhile();
		Expect(CiToken.While);
		result.Cond = ParseParenthesized();
		result.Body = ParseStatement();
		return result;
	}

	CiStatement ParseStatement()
	{
		switch (this.CurrentToken) {
		case CiToken.LeftBrace:
			return ParseBlock();
		case CiToken.Break:
			return ParseBreak();
		case CiToken.Const:
			return ParseConst();
		case CiToken.Continue:
			return ParseContinue();
		case CiToken.Delete:
			return ParseDelete();
		case CiToken.Do:
			return ParseDoWhile();
		case CiToken.For:
			return ParseFor();
		case CiToken.If:
			return ParseIf();
		case CiToken.Return:
			return ParseReturn();
		case CiToken.Switch:
			return ParseSwitch();
		case CiToken.Throw:
			return ParseThrow();
		case CiToken.While:
			return ParseWhile();
		default:
			CiExpr expr = ParseAssign(true);
			Expect(CiToken.Semicolon);
			return expr;
		}
	}

	void ParseDestructor(CiClass klass)
	{
		if (klass.Destructor != null)
			throw new ParseException("Duplicate destructor");
		Expect(CiToken.Tilde);
		if (ParseId() != klass.Name)
			throw new ParseException("Destructor name doesn't match class name");
		Expect(CiToken.LeftParenthesis);
		Expect(CiToken.RightParenthesis);
		klass.Destructor = ParseBlock();
	}

	CiCallType ParseCallType()
	{
		switch (this.CurrentToken) {
		case CiToken.Static:
			NextToken();
			return CiCallType.Static;
		case CiToken.Abstract:
			NextToken();
			return CiCallType.Abstract;
		case CiToken.Virtual:
			NextToken();
			return CiCallType.Virtual;
		case CiToken.Override:
			NextToken();
			return CiCallType.Override;
		case CiToken.Sealed:
			NextToken();
			return CiCallType.Sealed;
		default:
			return CiCallType.Normal;
		}
	}

	void ParseMethod(CiMethod method)
	{
		method.IsMutator = Eat(CiToken.ExclamationMark);
		Expect(CiToken.LeftParenthesis);
		List<CiVar> parameters = new List<CiVar>();
		if (!See(CiToken.RightParenthesis)) {
			do
				parameters.Add(ParseVar());
			while (Eat(CiToken.Comma));
		}
		Expect(CiToken.RightParenthesis);
		method.Parameters = parameters.ToArray();
		if (method.CallType == CiCallType.Abstract)
			Expect(CiToken.Semicolon);
		else if (See(CiToken.Return))
			method.Body = ParseReturn();
		else
			method.Body = ParseBlock();
	}

	public CiClass ParseClass(CiCallType callType)
	{
		Expect(CiToken.Class);
		CiClass klass = new CiClass { CallType = callType, Name = ParseId() };
		if (Eat(CiToken.Colon))
			klass.BaseClassName = ParseId();
		Expect(CiToken.LeftBrace);
		while (!Eat(CiToken.RightBrace)) {
			CiVisibility visibility;
			switch (this.CurrentToken) {
			case CiToken.Internal:
				visibility = CiVisibility.Internal;
				NextToken();
				break;
			case CiToken.Protected:
				visibility = CiVisibility.Protected;
				NextToken();
				break;
			case CiToken.Public:
				visibility = CiVisibility.Public;
				NextToken();
				break;
			case CiToken.Tilde:
				ParseDestructor(klass);
				continue;
			default:
				visibility = CiVisibility.Private;
				break;
			}
			CiMember member;
			if (See(CiToken.Const))
				member = ParseConst();
			else {
				callType = ParseCallType();
				// \ class | static | normal | abstract | sealed
				// member \|        |        |          |
				// --------+--------+--------+----------+-------
				// static  |   +    |   +    |    +     |   +
				// normal  |   -    |   +    |    +     |   +
				// abstract|   -    |   -    |    +     |   -
				// virtual |   -    |   +    |    +     |   -
				// override|   -    |   +    |    +     |   +
				// sealed  |   -    |   +    |    +     |   +
				if (callType == CiCallType.Static || klass.CallType == CiCallType.Abstract) {
					// ok
				}
				else if (klass.CallType == CiCallType.Static)
					throw new ParseException("Only static members allowed in a static class");
				else if (callType == CiCallType.Abstract)
					throw new ParseException("Abstract methods allowed only in an abstract class");
				else if (klass.CallType == CiCallType.Sealed && callType == CiCallType.Virtual)
					throw new ParseException("Virtual methods disallowed in a sealed class");

				CiExpr type = ParseType();
				if (See(CiToken.LeftBrace)) {
					CiBinaryExpr call = type as CiBinaryExpr;
					if (call != null && call.Op == CiToken.LeftParenthesis) {
						CiSymbolReference sr = call.Left as CiSymbolReference;
						if (sr != null) {
							if (sr.Name != klass.Name)
								throw new ParseException("Constructor name doesn't match class name");
							if (callType != CiCallType.Normal)
								throw new ParseException("Constructor cannot be " + callType);
							if (((CiCollection) call.Right).Items.Length != 0)
								throw new ParseException("Constructor parameters not supported");
							if (klass.Constructor != null)
								throw new ParseException("Duplicate constructor");
							klass.Constructor = new CiMethodBase { Visibility = visibility, Body = ParseBlock() };
							continue;
						}
					}
				}
				string name = ParseId();
				if (See(CiToken.LeftParenthesis) || See(CiToken.ExclamationMark)) {
					CiMethod method = new CiMethod { CallType = callType, Type = type, Name = name };
					ParseMethod(method);
					member = method;
				}
				else {
					if (visibility == CiVisibility.Public)
						throw new ParseException("Fields cannot be public");
					if (callType != CiCallType.Normal)
						throw new ParseException("Constructor cannot be " + callType);
					Expect(CiToken.Semicolon);
					member = new CiField { Type = type, Name = name };
				}
			}
			member.Visibility = visibility;
			klass.Add(member);
		}
		return klass;
	}

	public CiEnum ParseEnum()
	{
		Expect(CiToken.Enum);
		CiEnum enu = new CiEnum { IsFlags = Eat(CiToken.Asterisk), Name = ParseId() };
		Expect(CiToken.LeftBrace);
		do {
			CiConst konst = new CiConst { Name = ParseId() };
			if (Eat(CiToken.Assign))
				konst.Value = ParseExpr();
			else if (enu.IsFlags)
				throw new ParseException("enum* symbol must be assigned a value");
			enu.Add(konst);
		} while (Eat(CiToken.Comma));
		Expect(CiToken.RightBrace);
		return enu;
	}

	public void Parse(string filename, TextReader reader)
	{
		Open(filename, reader);
		while (!See(CiToken.EndOfFile)) {
			CiContainerType symbol;
			CiVisibility visibility = Eat(CiToken.Public) ? CiVisibility.Public : CiVisibility.Internal;
			switch (this.CurrentToken) {
			case CiToken.Class:
				symbol = ParseClass(CiCallType.Normal);
				break;
			case CiToken.Static:
			case CiToken.Abstract:
			case CiToken.Sealed:
				symbol = ParseClass(ParseCallType());
				break;

			case CiToken.Enum:
				symbol = ParseEnum();
				break;
			default:
				throw new ParseException("Expected class or enum");
			}
			symbol.SourceFilename = filename;
			symbol.Visibility = visibility;
			this.Symbols.Add(symbol);
		}
	}
}

}
