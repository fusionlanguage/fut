// CiParser.cs - Ci parser
//
// Copyright (C) 2011-2021  Piotr Fusik
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
using System.IO;
using System.Text;

namespace Foxoft.Ci
{

public class CiParser : CiLexer
{
	public CiProgram Program;
	CiLoop CurrentLoop;
	CiCondCompletionStatement CurrentLoopOrSwitch;
	string XcrementParent = null;

	CiException StatementException(CiStatement statement, string message)
	{
		return new CiException(this.Filename, statement.Line, message);
	}

	CiException StatementException(CiStatement statement, string format, params object[] args)
	{
		return StatementException(statement, string.Format(format, args));
	}

	string ParseId()
	{
		object id = this.CurrentValue;
		Expect(CiToken.Id);
		return (string) id;
	}

	CiCodeDoc ParseDoc()
	{
		if (See(CiToken.DocComment))
			return new CiDocParser(this).ParseCodeDoc();
		return null;
	}

	CiExpr ParseSymbolReference(CiExpr left)
	{
		return new CiSymbolReference { Line = this.Line, Left = left, Name = ParseId() };
	}

	CiExpr ParseListType()
	{
		int line = this.Line;
		Expect(CiToken.List);
		Expect(CiToken.Less);
		bool saveTypeArg = this.ParsingTypeArg;
		this.ParsingTypeArg = true;
		CiExpr elementType = ParseType();
		this.ParsingTypeArg = saveTypeArg;
		Expect(CiToken.RightAngle);
		return new CiSymbolReference { Line = line, Left = elementType, Symbol = CiSystem.ListClass };
	}

	CiExpr ParseDictionaryType(CiSymbol type)
	{
		int line = this.Line;
		NextToken();
		Expect(CiToken.Less);
		bool saveTypeArg = this.ParsingTypeArg;
		this.ParsingTypeArg = true;
		CiExpr keyType = ParseType();
		Expect(CiToken.Comma);
		CiExpr valueType = ParseType();
		this.ParsingTypeArg = saveTypeArg;
		Expect(CiToken.RightAngle);
		return new CiSymbolReference { Line = line, Left = new CiCollection { Items = new CiExpr[] { keyType, valueType } }, Symbol = type };
	}

	CiExpr ParseConstInitializer()
	{
		if (Eat(CiToken.LeftBrace))
			return new CiCollection { Line = this.Line, Items = ParseCollection(CiToken.RightBrace) };
		return ParseExpr();
	}

	CiExpr[] ParseCollection(CiToken closing)
	{
		List<CiExpr> items = new List<CiExpr>();
		if (!See(closing)) {
			do
				items.Add(ParseExpr());
			while (Eat(CiToken.Comma));
		}
		Expect(closing);
		return items.ToArray();
	}

	void CheckXcrementParent()
	{
		if (this.XcrementParent != null) {
			string op = this.CurrentToken == CiToken.Increment ? "++" : "--";
			throw ParseException(op + " not allowed on the right side of " + this.XcrementParent);
		}
	}

	bool SeeDigit()
	{
		int c = PeekChar();
		return c >= '0' && c <= '9';
	}

	CiInterpolatedString ParseInterpolatedString()
	{
		int line = this.Line;
		List<CiInterpolatedPart> parts = new List<CiInterpolatedPart>();
		do {
			string prefix = (string) this.CurrentValue;
			NextToken();
			CiExpr arg = ParseExpr();
			CiExpr width = null;
			char format = ' ';
			int precision = -1;
			if (Eat(CiToken.Comma))
				width = ParseExpr();
			if (See(CiToken.Colon)) {
				format = (char) ReadChar();
				if ("DdEeFfGgXx".IndexOf(format) < 0)
					throw ParseException("Invalid format specifier");

				if (SeeDigit()) {
					precision = ReadChar() - '0';
					if (SeeDigit())
						precision = precision * 10 + ReadChar() - '0';
				}
				NextToken();
			}
			parts.Add(new CiInterpolatedPart(prefix, arg) { WidthExpr = width, Format = format, Precision = precision });
			Check(CiToken.RightBrace);
		} while (ReadInterpolatedString() == CiToken.InterpolatedString);
		string suffix = (string) this.CurrentValue;
		NextToken();
		return new CiInterpolatedString(parts.ToArray(), suffix) { Line = line };
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
			CheckXcrementParent();
			goto case CiToken.Minus;
		case CiToken.Minus:
		case CiToken.Tilde:
		case CiToken.ExclamationMark:
		case CiToken.New:
			return new CiPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr() };
		case CiToken.Literal:
			result = new CiLiteral(this.CurrentValue) { Line = this.Line };
			NextToken();
			break;
		case CiToken.InterpolatedString:
			result = ParseInterpolatedString();
			break;
		case CiToken.LeftParenthesis:
			Expect(CiToken.LeftParenthesis);
			result = ParseType();
			Expect(CiToken.RightParenthesis);
			break;
		case CiToken.Id:
			result = ParseSymbolReference(null);
			break;
		case CiToken.List:
			result = ParseListType();
			break;
		case CiToken.Dictionary:
			result = ParseDictionaryType(CiSystem.DictionaryClass);
			break;
		case CiToken.SortedDictionary:
			result = ParseDictionaryType(CiSystem.SortedDictionaryClass);
			break;
		case CiToken.Resource:
			NextToken();
			Expect(CiToken.Less);
			if (ParseId() != "byte")
				throw ParseException("Expected resource<byte[]>");
			Expect(CiToken.LeftBracket);
			Expect(CiToken.RightBracket);
			Expect(CiToken.Greater);
			result = new CiPrefixExpr { Line = this.Line, Op = CiToken.Resource, Inner = ParseParenthesized() };
			break;
		default:
			throw ParseException("Invalid expression");
		}
		for (;;) {
			switch (this.CurrentToken) {
			case CiToken.Dot:
				NextToken();
				result = ParseSymbolReference(result);
				break;
			case CiToken.LeftParenthesis:
				if (!(result is CiSymbolReference symbol))
					throw ParseException("Expected a method");
				NextToken();
				result = new CiCallExpr { Line = this.Line, Method = symbol, Arguments = ParseCollection(CiToken.RightParenthesis) };
				break;
			case CiToken.LeftBracket:
				result = new CiBinaryExpr { Line = this.Line, Left = result, Op = NextToken(), Right = See(CiToken.RightBracket) ? null : ParseExpr() };
				Expect(CiToken.RightBracket);
				break;
			case CiToken.Increment:
			case CiToken.Decrement:
				CheckXcrementParent();
				goto case CiToken.ExclamationMark;
			case CiToken.ExclamationMark:
			case CiToken.Hash:
				result = new CiPostfixExpr { Line = this.Line, Inner = result, Op = NextToken() };
				break;
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
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAddExpr() };
		return left;
	}

	CiExpr ParseRelExpr()
	{
		CiExpr left = ParseShiftExpr();
		for (;;) {
			switch (this.CurrentToken) {
			case CiToken.Less:
			case CiToken.LessOrEqual:
			case CiToken.Greater:
			case CiToken.GreaterOrEqual:
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseShiftExpr() };
				break;
			case CiToken.Is:
				return new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseSymbolReference(null) };
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
		while (See(CiToken.CondAnd)) {
			string saveXcrementParent = this.XcrementParent;
			this.XcrementParent = "&&";
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseOrExpr() };
			this.XcrementParent = saveXcrementParent;
		}
		return left;
	}

	CiExpr ParseCondOrExpr()
	{
		CiExpr left = ParseCondAndExpr();
		while (See(CiToken.CondOr)) {
			string saveXcrementParent = this.XcrementParent;
			this.XcrementParent = "||";
			left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseCondAndExpr() };
			this.XcrementParent = saveXcrementParent;
		}
		return left;
	}

	CiExpr ParseExpr()
	{
		CiExpr left = ParseCondOrExpr();
		if (See(CiToken.QuestionMark)) {
			CiSelectExpr result = new CiSelectExpr { Line = this.Line, Cond = left };
			NextToken();
			string saveXcrementParent = this.XcrementParent;
			this.XcrementParent = "?";
			result.OnTrue = ParseExpr();
			Expect(CiToken.Colon);
			result.OnFalse = ParseExpr();
			this.XcrementParent = saveXcrementParent;
			return result;
		}
		return left;
	}

	CiExpr ParseType()
	{
		CiExpr left = ParseExpr();
		if (Eat(CiToken.Range))
			return new CiBinaryExpr { Line = this.Line, Left = left, Op = CiToken.Range, Right = ParseExpr() };
		return left;
	}

	CiExpr ParseAssign(bool allowVar)
	{
		CiExpr left = ParseType();
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
		CiVar def = new CiVar { Line = this.Line, TypeExpr = type, Name = ParseId() };
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
		CiConst konst = new CiConst { Line = this.Line, TypeExpr = ParseType(), Name = ParseId() };
		Expect(CiToken.Assign);
		konst.Value = ParseConstInitializer();
		Expect(CiToken.Semicolon);
		return konst;
	}

	CiBlock ParseBlock()
	{
		int line = this.Line;
		Expect(CiToken.LeftBrace);
		List<CiStatement> statements = new List<CiStatement>();
		while (!Eat(CiToken.RightBrace))
			statements.Add(ParseStatement());
		return new CiBlock { Line = line, Statements = statements.ToArray() };
	}

	CiAssert ParseAssert()
	{
		CiAssert result = new CiAssert { Line = this.Line };
		Expect(CiToken.Assert);
		result.Cond = ParseExpr();
		if (Eat(CiToken.Comma))
			result.Message = ParseExpr();
		Expect(CiToken.Semicolon);
		return result;
	}

	CiBreak ParseBreak()
	{
		if (this.CurrentLoopOrSwitch == null)
			throw ParseException("break outside loop or switch");
		CiBreak result = new CiBreak(this.CurrentLoopOrSwitch) { Line = this.Line };
		Expect(CiToken.Break);
		Expect(CiToken.Semicolon);
		if (this.CurrentLoopOrSwitch is CiLoop loop)
			loop.HasBreak = true;
		return result;
	}

	CiContinue ParseContinue()
	{
		if (this.CurrentLoop == null)
			throw ParseException("continue outside loop");
		CiContinue result = new CiContinue(this.CurrentLoop) { Line = this.Line };
		Expect(CiToken.Continue);
		Expect(CiToken.Semicolon);
		return result;
	}

	void ParseLoopBody(CiLoop loop)
	{
		CiLoop outerLoop = this.CurrentLoop;
		CiCondCompletionStatement outerLoopOrSwitch = this.CurrentLoopOrSwitch;
		this.CurrentLoopOrSwitch = this.CurrentLoop = loop;
		loop.Body = ParseStatement();
		this.CurrentLoopOrSwitch = outerLoopOrSwitch;
		this.CurrentLoop = outerLoop;
	}

	CiDoWhile ParseDoWhile()
	{
		CiDoWhile result = new CiDoWhile { Line = this.Line };
		Expect(CiToken.Do);
		ParseLoopBody(result);
		Expect(CiToken.While);
		result.Cond = ParseParenthesized();
		Expect(CiToken.Semicolon);
		return result;
	}

	CiFor ParseFor()
	{
		CiFor result = new CiFor { Line = this.Line };
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
		ParseLoopBody(result);
		return result;
	}

	void ParseForeachIterator(CiForeach result)
	{
		result.Add(new CiVar { Line = this.Line, TypeExpr = ParseType(), Name = ParseId() });
	}

	CiForeach ParseForeach()
	{
		CiForeach result = new CiForeach { Line = this.Line };
		Expect(CiToken.Foreach);
		Expect(CiToken.LeftParenthesis);
		if (Eat(CiToken.LeftParenthesis)) {
			ParseForeachIterator(result);
			Expect(CiToken.Comma);
			ParseForeachIterator(result);
			Expect(CiToken.RightParenthesis);
		}
		else
			ParseForeachIterator(result);
		Expect(CiToken.In);
		result.Collection = ParseExpr();
		Expect(CiToken.RightParenthesis);
		ParseLoopBody(result);
		return result;
	}

	CiIf ParseIf()
	{
		CiIf result = new CiIf { Line = this.Line };
		Expect(CiToken.If);
		result.Cond = ParseParenthesized();
		result.OnTrue = ParseStatement();
		if (Eat(CiToken.Else))
			result.OnFalse = ParseStatement();
		return result;
	}

	CiLock ParseLock()
	{
		CiLock result = new CiLock { Line = this.Line };
		Expect(CiToken.Lock);
		result.Lock = ParseParenthesized();
		result.Body = ParseStatement();
		return result;
	}

	CiNative ParseNative()
	{
		int line = this.Line;
		Expect(CiToken.Native);
		StringBuilder sb = new StringBuilder();
		this.CopyTo = sb;
		try {
			Expect(CiToken.LeftBrace);
			int nesting = 1;
			for (;;) {
				if (See(CiToken.EndOfFile))
					throw ParseException("Native block not terminated");
				if (See(CiToken.LeftBrace))
					nesting++;
				else if (See(CiToken.RightBrace) && --nesting == 0)
					break;
				NextToken();
			}
		}
		finally {
			this.CopyTo = null;
		}
		NextToken();
		Trace.Assert(sb[sb.Length - 1] == '}');
		sb.Length--;
		return new CiNative { Line = line, Content = sb.ToString() };
	}

	CiReturn ParseReturn()
	{
		CiReturn result = new CiReturn { Line = this.Line };
		NextToken();
		if (!See(CiToken.Semicolon))
			result.Value = ParseExpr();
		Expect(CiToken.Semicolon);
		return result;
	}

	CiSwitch ParseSwitch()
	{
		CiSwitch result = new CiSwitch { Line = this.Line };
		Expect(CiToken.Switch);
		result.Value = ParseParenthesized();
		Expect(CiToken.LeftBrace);

		CiCondCompletionStatement outerLoopOrSwitch = this.CurrentLoopOrSwitch;
		this.CurrentLoopOrSwitch = result;
		List<CiCase> cases = new List<CiCase>();
		while (Eat(CiToken.Case)) {
			List<CiExpr> values = new List<CiExpr>();
			CiExpr value;
			do {
				value = ParseExpr();
				values.Add(value);
				Expect(CiToken.Colon);
			} while (Eat(CiToken.Case));
			if (See(CiToken.Default))
				throw StatementException(value, "Please remove case before default");
			CiCase kase = new CiCase { Values = values.ToArray() };

			List<CiStatement> statements = new List<CiStatement>();
			for (;;) {
				statements.Add(ParseStatement());
				switch (this.CurrentToken) {
				case CiToken.Case:
				case CiToken.Default:
				case CiToken.RightBrace:
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
			throw ParseException("Switch with no cases");
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
		this.CurrentLoopOrSwitch = outerLoopOrSwitch;
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
		CiWhile result = new CiWhile { Line = this.Line };
		Expect(CiToken.While);
		result.Cond = ParseParenthesized();
		ParseLoopBody(result);
		return result;
	}

	CiStatement ParseStatement()
	{
		switch (this.CurrentToken) {
		case CiToken.LeftBrace:
			return ParseBlock();
		case CiToken.Assert:
			return ParseAssert();
		case CiToken.Break:
			return ParseBreak();
		case CiToken.Const:
			return ParseConst();
		case CiToken.Continue:
			return ParseContinue();
		case CiToken.Do:
			return ParseDoWhile();
		case CiToken.For:
			return ParseFor();
		case CiToken.Foreach:
			return ParseForeach();
		case CiToken.If:
			return ParseIf();
		case CiToken.Lock:
			return ParseLock();
		case CiToken.Native:
			return ParseNative();
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
		if (!See(CiToken.RightParenthesis)) {
			do {
				CiCodeDoc doc = ParseDoc();
				CiVar param = ParseVar();
				param.Documentation = doc;
				method.Parameters.Add(param);
			} while (Eat(CiToken.Comma));
		}
		Expect(CiToken.RightParenthesis);
		method.Throws = Eat(CiToken.Throws);
		if (method.CallType == CiCallType.Abstract)
			Expect(CiToken.Semicolon);
		else if (See(CiToken.FatArrow))
			method.Body = ParseReturn();
		else
			method.Body = ParseBlock();
	}

	public CiClass ParseClass(CiCallType callType)
	{
		Expect(CiToken.Class);
		CiClass klass = new CiClass { Parent = this.Program, Filename = this.Filename, Line = this.Line, CallType = callType, Name = ParseId() };
		if (Eat(CiToken.Colon))
			klass.BaseClassName = ParseId();
		Expect(CiToken.LeftBrace);

		List<CiConst> consts = new List<CiConst>();
		List<CiField> fields = new List<CiField>();
		List<CiMethod> methods = new List<CiMethod>();
		while (!Eat(CiToken.RightBrace)) {
			CiCodeDoc doc = ParseDoc();

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

			default:
				visibility = CiVisibility.Private;
				break;
			}

			if (See(CiToken.Const)) {
				// const
				CiConst konst = ParseConst();
				konst.Documentation = doc;
				konst.Visibility = visibility;
				consts.Add(konst);
				continue;
			}

			callType = ParseCallType();
			// \ class | static | normal | abstract | sealed
			// method \|        |        |          |
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
				throw ParseException("Only static members allowed in a static class");
			else if (callType == CiCallType.Abstract)
				throw ParseException("Abstract methods allowed only in an abstract class");
			else if (klass.CallType == CiCallType.Sealed && callType == CiCallType.Virtual)
				throw ParseException("Virtual methods disallowed in a sealed class");
			if (visibility == CiVisibility.Private && callType != CiCallType.Static && callType != CiCallType.Normal)
				throw ParseException("{0} method cannot be private", callType);

			CiExpr type = Eat(CiToken.Void) ? CiSystem.VoidType : ParseType();
			if (See(CiToken.LeftBrace) && type is CiCallExpr call) {
				// constructor
				if (call.Method.Name != klass.Name)
					throw ParseException("Constructor name doesn't match class name");
				if (callType != CiCallType.Normal)
					throw ParseException("Constructor cannot be {0}", callType);
				if (call.Arguments.Length != 0)
					throw ParseException("Constructor parameters not supported");
				if (klass.Constructor != null)
					throw ParseException("Duplicate constructor, already defined in line {0}", klass.Constructor.Line);
				if (visibility == CiVisibility.Private)
					visibility = CiVisibility.Internal; // TODO
				klass.Constructor = new CiMethodBase { Line = call.Line, Documentation = doc, Visibility = visibility, Parent = klass, Type = CiSystem.VoidType, Name = klass.Name, Body = ParseBlock() };
				continue;
			}

			int line = this.Line;
			string name = ParseId();
			if (See(CiToken.LeftParenthesis) || See(CiToken.ExclamationMark)) {
				// method
				CiMethod method = new CiMethod { Line = line, Documentation = doc, Visibility = visibility, CallType = callType, TypeExpr = type, Name = name };
				method.Parameters.Parent = klass;
				ParseMethod(method);
				methods.Add(method);
				continue;
			}

			// field
			if (visibility == CiVisibility.Public)
				throw ParseException("Field cannot be public");
			if (callType != CiCallType.Normal)
				throw ParseException("Field cannot be {0}", callType);
			if (type == CiSystem.VoidType)
				throw ParseException("Field cannot be void");
			CiField field = new CiField { Line = line, Documentation = doc, Visibility = visibility, TypeExpr = type, Name = name };
			if (Eat(CiToken.Assign))
				field.Value = ParseExpr();
			Expect(CiToken.Semicolon);
			fields.Add(field);
		}

		klass.Consts = consts.ToArray();
		klass.Fields = fields.ToArray();
		klass.Methods = methods.ToArray();
		return klass;
	}

	public CiEnum ParseEnum()
	{
		Expect(CiToken.Enum);
		CiEnum enu = new CiEnum { Parent = this.Program, Filename = this.Filename, IsFlags = Eat(CiToken.Asterisk), Line = this.Line, Name = ParseId() };
		Expect(CiToken.LeftBrace);
		do {
			CiConst konst = new CiConst { Visibility = CiVisibility.Public, Documentation = ParseDoc(), Line = this.Line, Name = ParseId(), Type = enu };
			if (Eat(CiToken.Assign))
				konst.Value = ParseExpr();
			else if (enu.IsFlags)
				throw ParseException("enum* symbol must be assigned a value");
			enu.Add(konst);
		} while (Eat(CiToken.Comma));
		Expect(CiToken.RightBrace);
		return enu;
	}

	public void Parse(string filename, TextReader reader)
	{
		Open(filename, reader);
		while (!See(CiToken.EndOfFile)) {
			CiCodeDoc doc = ParseDoc();
			CiContainerType type;
			bool isPublic = Eat(CiToken.Public);
			switch (this.CurrentToken) {
			// class
			case CiToken.Class:
				type = ParseClass(CiCallType.Normal);
				break;
			case CiToken.Static:
			case CiToken.Abstract:
			case CiToken.Sealed:
				type = ParseClass(ParseCallType());
				break;

			// enum
			case CiToken.Enum:
				type = ParseEnum();
				break;

			// native
			case CiToken.Native:
				this.Program.TopLevelNatives.Add(ParseNative().Content);
				continue;

			default:
				throw ParseException("Expected class or enum");
			}
			type.Documentation = doc;
			type.IsPublic = isPublic;
			this.Program.Add(type);
		}
	}
}

}
