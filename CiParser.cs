// CiParser.cs - Ci parser
//
// Copyright (C) 2011-2022  Piotr Fusik
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
using System.Globalization;
using System.Text;

namespace Foxoft.Ci
{

public class CiParser : CiLexer
{
	public CiProgram Program;
	CiLoop CurrentLoop;
	CiCondCompletionStatement CurrentLoopOrSwitch;
	string XcrementParent = null;

	string ParseId()
	{
		string id = this.StringValue;
		Expect(CiToken.Id);
		return id;
	}

	static void AppendUtf16(StringBuilder sb, int c)
	{
		if (c >= 0x10000) {
			sb.Append((char) (0xd800 + (c - 0x10000 >> 10 & 0x3ff)));
			c = 0xdc00 + (c & 0x3ff);
		}
		sb.Append((char) c);
	}

	string DocParseText()
	{
		StringBuilder sb = new StringBuilder();
		while (DocSee(CiDocToken.Char)) {
			AppendUtf16(sb, this.DocCurrentChar);
			DocNextToken();
		}
		if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
			sb.Length--;
		return sb.ToString();
	}

	void DocParsePara(CiDocPara para)
	{
		for (;;) {
			if (DocSee(CiDocToken.Char))
				para.Children.Add(new CiDocText { Text = DocParseText() });
			else if (DocEat(CiDocToken.CodeDelimiter)) {
				para.Children.Add(new CiDocCode { Text = DocParseText() });
				if (!DocEat(CiDocToken.CodeDelimiter))
					ReportError("Unterminated code in documentation comment");
			}
			else
				break;
		}
		DocEat(CiDocToken.Para);
	}

	CiDocBlock DocParseBlock()
	{
		if (DocEat(CiDocToken.Bullet)) {
			CiDocList list = new CiDocList();
			do {
				CiDocPara item = new CiDocPara();
				DocParsePara(item);
				list.Items.Add(item);
			} while (DocEat(CiDocToken.Bullet));
			DocEat(CiDocToken.Para);
			return list;
		}
		CiDocPara para = new CiDocPara();
		DocParsePara(para);
		return para;
	}

	CiCodeDoc ParseCodeDoc()
	{
		DocStartLexing();
		CiCodeDoc doc = new CiCodeDoc();
		DocParsePara(doc.Summary);
		if (DocEat(CiDocToken.Period)) {
			DocEat(CiDocToken.Para);
			while (!DocSee(CiDocToken.EndOfFile))
				doc.Details.Add(DocParseBlock());
		}
		return doc;
	}

	CiCodeDoc ParseDoc() => See(CiToken.DocComment) ? ParseCodeDoc() : null;

	CiExpr ParseSymbolReference(CiExpr left) => new CiSymbolReference { Line = this.Line, Left = left, Name = ParseId() };

	void ParseCollection(List<CiExpr> result, CiToken closing)
	{
		if (!See(closing)) {
			do
				result.Add(ParseExpr());
			while (Eat(CiToken.Comma));
		}
		ExpectOrSkip(closing);
	}

	CiExpr ParseConstInitializer()
	{
		if (Eat(CiToken.LeftBrace)) {
			CiAggregateInitializer result = new CiAggregateInitializer { Line = this.Line };
			ParseCollection(result.Items, CiToken.RightBrace);
			return result;
		}
		return ParseExpr();
	}

	void CheckXcrementParent()
	{
		if (this.XcrementParent != null) {
			string op = See(CiToken.Increment) ? "++" : "--";
			ReportError($"{op} not allowed on the right side of {this.XcrementParent}");
		}
	}

	bool SeeDigit()
	{
		int c = PeekChar();
		return c >= '0' && c <= '9';
	}

	CiInterpolatedString ParseInterpolatedString()
	{
		CiInterpolatedString result = new CiInterpolatedString { Line = this.Line };
		do {
			string prefix = this.StringValue.Replace("{{", "{");
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
					ReportError("Invalid format specifier");

				if (SeeDigit()) {
					precision = ReadChar() - '0';
					if (SeeDigit())
						precision = precision * 10 + ReadChar() - '0';
				}
				NextToken();
			}
			result.Parts.Add(new CiInterpolatedPart(prefix, arg) { WidthExpr = width, Format = format, Precision = precision });
			Check(CiToken.RightBrace);
		} while (ReadString(true) == CiToken.InterpolatedString);
		result.Suffix = this.StringValue.Replace("{{", "{");
		NextToken();
		return result;
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
			return new CiPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr() };
		case CiToken.New:
			return new CiPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParseType() };
		case CiToken.LiteralLong:
			result = new CiLiteralLong(this.LongValue) { Line = this.Line };
			NextToken();
			break;
		case CiToken.LiteralDouble:
			if (!double.TryParse(GetLexeme().Replace("_", ""),
				NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture, out double d))
				ReportError("Invalid floating-point number");
			result = new CiLiteralDouble(d) { Line = this.Line };
			NextToken();
			break;
		case CiToken.LiteralChar:
			result = new CiLiteralChar((int) this.LongValue) { Line = this.Line };
			NextToken();
			break;
		case CiToken.LiteralString:
			result = new CiLiteralString(this.StringValue) { Line = this.Line };
			NextToken();
			break;
		case CiToken.False:
			result = new CiLiteralFalse { Line = this.Line, Type = CiSystem.BoolType };
			NextToken();
			break;
		case CiToken.True:
			result = new CiLiteralTrue { Line = this.Line, Type = CiSystem.BoolType };
			NextToken();
			break;
		case CiToken.Null:
			result = new CiLiteralNull { Line = this.Line, Type = CiSystem.NullType };
			NextToken();
			break;
		case CiToken.InterpolatedString:
			result = ParseInterpolatedString();
			break;
		case CiToken.LeftParenthesis:
			result = ParseParenthesized();
			break;
		case CiToken.Id:
			result = ParseSymbolReference(null);
			if (Eat(CiToken.FatArrow)) {
				CiLambdaExpr lambda = new CiLambdaExpr { Line = result.Line };
				lambda.Add(CiVar.New(null, ((CiSymbolReference) result).Name));
				lambda.Body = ParseExpr();
				return lambda;
			}
			break;
		case CiToken.Resource:
			NextToken();
			if (Eat(CiToken.Less)
			 && ParseId() == "byte"
			 && Eat(CiToken.LeftBracket)
			 && Eat(CiToken.RightBracket)
			 && Eat(CiToken.Greater))
				result = new CiPrefixExpr { Line = this.Line, Op = CiToken.Resource, Inner = ParseParenthesized() };
			else {
				ReportError("Expected 'resource<byte[]>'");
				result = null;
			}
			break;
		default:
			ReportError("Invalid expression");
			result = null;
			break;
		}
		for (;;) {
			switch (this.CurrentToken) {
			case CiToken.Dot:
				NextToken();
				result = ParseSymbolReference(result);
				break;
			case CiToken.LeftParenthesis:
				CiSymbolReference symbol = result as CiSymbolReference;
				if (symbol == null)
					ReportError("Expected a method");
				NextToken();
				CiCallExpr call = new CiCallExpr { Line = this.Line, Method = symbol };
				ParseCollection(call.Arguments, CiToken.RightParenthesis);
				result = call;
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
				CiBinaryExpr isExpr = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParsePrimaryExpr() };
				if (See(CiToken.Id))
					isExpr.Right = new CiVar { Line = this.Line, TypeExpr = isExpr.Right, Name = ParseId() };
				return isExpr;
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
		CiExpr left = ParsePrimaryExpr();
		if (Eat(CiToken.Range))
			return new CiBinaryExpr { Line = this.Line, Left = left, Op = CiToken.Range, Right = ParsePrimaryExpr() };
		if (left is CiSymbolReference symbol && Eat(CiToken.Less)) {
			CiAggregateInitializer typeArgs = new CiAggregateInitializer();
			left = new CiSymbolReference { Line = this.Line, Left = typeArgs, Name = symbol.Name };
			bool saveTypeArg = this.ParsingTypeArg;
			this.ParsingTypeArg = true;
			do
				typeArgs.Items.Add(ParseType());
			while (Eat(CiToken.Comma));
			Expect(CiToken.RightAngle);
			this.ParsingTypeArg = saveTypeArg;
			if (Eat(CiToken.ExclamationMark))
				left = new CiPostfixExpr { Line = this.Line, Inner = left, Op = CiToken.ExclamationMark };
			else if (Eat(CiToken.LeftParenthesis)) {
				Expect(CiToken.RightParenthesis);
				left = new CiCallExpr { Line = this.Line, Method = (CiSymbolReference) left };
			}
		}
		return left;
	}

	CiExpr ParseAssign(bool allowVar)
	{
		CiExpr left = allowVar ? ParseType() : ParseExpr();
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

	CiExpr ParseInitializer()
	{
		if (!Eat(CiToken.Assign))
			return null;
		if (Eat(CiToken.LeftBrace)) {
			CiAggregateInitializer result = new CiAggregateInitializer { Line = this.Line };
			do {
				int line = this.Line;
				CiExpr field = ParseSymbolReference(null);
				Expect(CiToken.Assign);
				result.Items.Add(new CiBinaryExpr { Line = line, Left = field, Op = CiToken.Assign, Right = ParseExpr() });
			} while (Eat(CiToken.Comma));
			Expect(CiToken.RightBrace);
			return result;
		}
		return ParseExpr();
	}

	void AddSymbol(CiScope scope, CiSymbol symbol)
	{
		if (scope.Contains(symbol))
			ReportError("Duplicate symbol");
		else
			scope.Add(symbol);
	}

	CiVar ParseVar(CiExpr type) => new CiVar { Line = this.Line, TypeExpr = type, Name = ParseId(), Value = ParseInitializer() };

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
		CiBlock result = new CiBlock { Line = this.Line };
		Expect(CiToken.LeftBrace);
		while (!See(CiToken.RightBrace) && !See(CiToken.EndOfFile))
			result.Statements.Add(ParseStatement());
		Expect(CiToken.RightBrace);
		return result;
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
			ReportError("break outside loop or switch");
		CiBreak result = new CiBreak { Line = this.Line, LoopOrSwitch = this.CurrentLoopOrSwitch };
		Expect(CiToken.Break);
		Expect(CiToken.Semicolon);
		if (this.CurrentLoopOrSwitch is CiLoop loop)
			loop.HasBreak = true;
		return result;
	}

	CiContinue ParseContinue()
	{
		if (this.CurrentLoop == null)
			ReportError("continue outside loop");
		CiContinue result = new CiContinue { Line = this.Line, Loop = this.CurrentLoop };
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
		AddSymbol(result, new CiVar { Line = this.Line, TypeExpr = ParseType(), Name = ParseId() });
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
		Expect(CiToken.Lock_);
		result.Lock = ParseParenthesized();
		result.Body = ParseStatement();
		return result;
	}

	CiNative ParseNative()
	{
		int line = this.Line;
		Expect(CiToken.Native);
		int offset = this.CharOffset;
		Expect(CiToken.LeftBrace);
		int nesting = 1;
		for (;;) {
			if (See(CiToken.EndOfFile)) {
				Expect(CiToken.RightBrace);
				return null;
			}
			if (See(CiToken.LeftBrace))
				nesting++;
			else if (See(CiToken.RightBrace) && --nesting == 0)
				break;
			NextToken();
		}
		Trace.Assert(this.Input[this.CharOffset - 1] == '}');
		string content = Encoding.UTF8.GetString(this.Input, offset, this.CharOffset - 1 - offset);
		NextToken();
		return new CiNative { Line = line, Content = content };
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
		while (Eat(CiToken.Case)) {
			CiCase kase = new CiCase();
			do {
				CiExpr expr = ParseExpr();
				if (See(CiToken.Id))
					expr = ParseVar(expr);
				kase.Values.Add(expr);
				Expect(CiToken.Colon);
			} while (Eat(CiToken.Case));
			if (See(CiToken.Default)) {
				ReportError("Please remove 'case' before 'default'");
				break;
			}

			while (!See(CiToken.EndOfFile)) {
				kase.Body.Add(ParseStatement());
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
			result.Cases.Add(kase);
		}
		if (result.Cases.Count == 0)
			ReportError("Switch with no cases");

		if (Eat(CiToken.Default)) {
			Expect(CiToken.Colon);
			do {
				if (See(CiToken.EndOfFile))
					break;
				result.DefaultBody.Add(ParseStatement());
			} while (!See(CiToken.RightBrace));
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
		case CiToken.Lock_:
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
				CiVar param = ParseVar(ParseType());
				param.Documentation = doc;
				AddSymbol(method.Parameters, param);
			} while (Eat(CiToken.Comma));
		}
		Expect(CiToken.RightParenthesis);
		method.Throws = Eat(CiToken.Throws);
		if (method.CallType == CiCallType.Abstract)
			Expect(CiToken.Semicolon);
		else if (See(CiToken.FatArrow))
			method.Body = ParseReturn();
		else if (Check(CiToken.LeftBrace))
			method.Body = ParseBlock();
	}

	static readonly string[] CallTypeStrings = {
		"static",
		"normal",
		"abstract",
		"virtual",
		"override",
		"sealed"
	};

	static string ToString(CiCallType callType) => CallTypeStrings[(int) callType];

	public CiClass ParseClass(CiCallType callType)
	{
		Expect(CiToken.Class);
		CiClass klass = new CiClass { Parent = this.Program, Filename = this.Filename, Line = this.Line, CallType = callType, Name = ParseId() };
		if (Eat(CiToken.Colon))
			klass.BaseClassName = ParseId();
		Expect(CiToken.LeftBrace);

		while (!See(CiToken.RightBrace) && !See(CiToken.EndOfFile)) {
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
			case CiToken.Semicolon:
				ReportError("Semicolon in class definition");
				NextToken();
				continue;

			default:
				visibility = CiVisibility.Private;
				break;
			}

			if (See(CiToken.Const)) {
				// const
				CiConst konst = ParseConst();
				konst.Documentation = doc;
				konst.Visibility = visibility;
				AddSymbol(klass, konst);
				continue;
			}

			callType = ParseCallType();
			CiExpr type = Eat(CiToken.Void) ? CiSystem.VoidType : ParseType();
			if (See(CiToken.LeftBrace) && type is CiCallExpr call) {
				// constructor
				if (call.Method.Name != klass.Name)
					ReportError("Method with no return type");
				else {
					if (klass.CallType == CiCallType.Static)
						ReportError("Constructor in a static class");
					if (callType != CiCallType.Normal)
						ReportError($"Constructor cannot be {ToString(callType)}");
					if (call.Arguments.Count != 0)
						ReportError("Constructor parameters not supported");
					if (klass.Constructor != null)
						ReportError($"Duplicate constructor, already defined in line {klass.Constructor.Line}");
				}
				if (visibility == CiVisibility.Private)
					visibility = CiVisibility.Internal; // TODO
				klass.Constructor = new CiMethodBase { Line = call.Line, Documentation = doc, Visibility = visibility, Parent = klass, Type = CiSystem.VoidType, Name = klass.Name, Body = ParseBlock() };
				continue;
			}

			int line = this.Line;
			string name = ParseId();
			if (See(CiToken.LeftParenthesis) || See(CiToken.ExclamationMark)) {
				// method

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
					ReportError("Only static methods allowed in a static class");
				else if (callType == CiCallType.Abstract)
					ReportError("Abstract methods allowed only in an abstract class");
				else if (klass.CallType == CiCallType.Sealed && callType == CiCallType.Virtual)
					ReportError("Virtual methods disallowed in a sealed class");
				if (visibility == CiVisibility.Private && callType != CiCallType.Static && callType != CiCallType.Normal)
					ReportError($"{callType} method cannot be private");

				CiMethod method = new CiMethod { Line = line, Documentation = doc, Visibility = visibility, CallType = callType, TypeExpr = type, Name = name };
				method.Parameters.Parent = klass;
				ParseMethod(method);
				AddSymbol(klass, method);
				continue;
			}

			// field
			if (visibility == CiVisibility.Public)
				ReportError("Field cannot be public");
			if (callType != CiCallType.Normal)
				ReportError($"Field cannot be {ToString(callType)}");
			if (type == CiSystem.VoidType)
				ReportError("Field cannot be void");
			CiField field = new CiField { Line = line, Documentation = doc, Visibility = visibility, TypeExpr = type, Name = name, Value = ParseInitializer() };
			Expect(CiToken.Semicolon);
			AddSymbol(klass, field);
		}
		Expect(CiToken.RightBrace);
		return klass;
	}

	public CiEnum ParseEnum()
	{
		Expect(CiToken.Enum);
		bool flags = Eat(CiToken.Asterisk);
		CiEnum enu = flags ? new CiEnumFlags() : new CiEnum();
		enu.Parent = this.Program;
		enu.Filename = this.Filename;
		enu.Line = this.Line;
		enu.Name = ParseId();
		Expect(CiToken.LeftBrace);
		do {
			CiConst konst = new CiConst { Visibility = CiVisibility.Public, Documentation = ParseDoc(), Line = this.Line, Name = ParseId(), Type = enu };
			if (Eat(CiToken.Assign))
				konst.Value = ParseExpr();
			else if (flags)
				ReportError("enum* symbol must be assigned a value");
			AddSymbol(enu, konst);
		} while (Eat(CiToken.Comma));
		Expect(CiToken.RightBrace);
		return enu;
	}

	public void Parse(string filename, byte[] input)
	{
		Open(filename, input, input.Length);
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
				ReportError("Expected class or enum");
				NextToken();
				continue;
			}
			type.Documentation = doc;
			type.IsPublic = isPublic;
			AddSymbol(this.Program, type);
		}
	}
}

}
