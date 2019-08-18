// CiParser.cs - Ci parser
//
// Copyright (C) 2011-2018  Piotr Fusik
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
	public readonly CiProgram Program = new CiProgram { Parent = CiSystem.Value };

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
				items.Add(ParseExpr());
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
			return new CiPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr() };
		case CiToken.Literal:
			result = new CiLiteral(this.CurrentValue) { Line = this.Line };
			NextToken();
			break;
		case CiToken.LeftParenthesis:
			result = ParseParenthesized();
			break;
		case CiToken.Id:
			result = ParseSymbolReference();
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
		return new CiBlock { Filename = this.Filename, Line = this.Line, Statements = statements.ToArray() };
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
		NextToken();
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
						throw ParseException("Expected goto case or goto default");
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
		method.Parameters.Filename = this.Filename;
		Expect(CiToken.LeftParenthesis);
		if (!See(CiToken.RightParenthesis)) {
			do
				method.Parameters.Add(ParseVar());
			while (Eat(CiToken.Comma));
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

			CiExpr type = Eat(CiToken.Void) ? null : ParseType();
			if (See(CiToken.LeftBrace)
			 && type is CiBinaryExpr call
			 && call.Op == CiToken.LeftParenthesis
			 && call.Left is CiSymbolReference sr) {
				// constructor
				if (sr.Name != klass.Name)
					throw ParseException("Constructor name doesn't match class name");
				if (callType != CiCallType.Normal)
					throw ParseException("Constructor cannot be {0}", callType);
				if (call.RightCollection.Length != 0)
					throw ParseException("Constructor parameters not supported");
				if (klass.Constructor != null)
					throw ParseException("Duplicate constructor, already defined in line {0}", klass.Constructor.Line);
				if (visibility == CiVisibility.Private)
					visibility = CiVisibility.Internal; // TODO
				klass.Constructor = new CiMethodBase { Line = sr.Line, Visibility = visibility, Body = ParseBlock() };
				continue;
			}

			int line = this.Line;
			string name = ParseId();
			if (See(CiToken.LeftParenthesis) || See(CiToken.ExclamationMark)) {
				// method
				CiMethod method = new CiMethod { Line = line, Visibility = visibility, CallType = callType, TypeExpr = type, Name = name };
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
			if (type == null)
				throw ParseException("Field cannot be void");
			CiField field = new CiField { Line = line, Visibility = visibility, TypeExpr = type, Name = name };
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
			CiConst konst = new CiConst { Line = this.Line, Name = ParseId(), Type = enu };
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

			default:
				throw ParseException("Expected class or enum");
			}
			type.IsPublic = isPublic;
			this.Program.Add(type);
		}
	}
}

}
