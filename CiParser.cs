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

using System.Globalization;
using System.Text;

namespace Foxoft.Ci
{

public class CiParser : CiParserBase
{
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

	protected override string DocParseText()
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

	protected override CiExpr ParsePrimaryExpr()
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
			CiPrefixExpr newResult = new CiPrefixExpr { Line = this.Line, Op = NextToken() };
			result = ParseType();
			if (Eat(CiToken.LeftBrace))
				result = new CiBinaryExpr { Line = this.Line, Left = result, Op = CiToken.LeftBrace, Right = ParseObjectLiteral() };
			newResult.Inner = result;
			return newResult;
		case CiToken.LiteralLong:
			result = this.Program.System.NewLiteralLong(this.LongValue, this.Line);
			NextToken();
			break;
		case CiToken.LiteralDouble:
			if (!double.TryParse(GetLexeme().Replace("_", ""),
				NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture, out double d))
				ReportError("Invalid floating-point number");
			result = new CiLiteralDouble { Line = this.Line, Type = this.Program.System.DoubleType, Value = d };
			NextToken();
			break;
		case CiToken.LiteralChar:
			result = CiLiteralChar.New((int) this.LongValue, this.Line);
			NextToken();
			break;
		case CiToken.LiteralString:
			result = this.Program.System.NewLiteralString(this.StringValue, this.Line);
			NextToken();
			break;
		case CiToken.False:
			result = new CiLiteralFalse { Line = this.Line, Type = this.Program.System.BoolType };
			NextToken();
			break;
		case CiToken.True:
			result = new CiLiteralTrue { Line = this.Line, Type = this.Program.System.BoolType };
			NextToken();
			break;
		case CiToken.Null:
			result = new CiLiteralNull { Line = this.Line, Type = this.Program.System.NullType };
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
}

}
