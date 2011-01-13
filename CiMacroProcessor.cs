// CiMacroProcessor.cs - Ci macro processor
//
// Copyright (C) 2011  Piotr Fusik
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Foxoft.Ci
{

public class CiMacro : CiSymbol
{
	public string[] Arguments;
	public bool IsStatement;
	public string Body;
}

public class CiMacroProcessor
{
	static void ParseBody(CiParser parser, CiToken left, CiToken right)
	{
		int level = 1;
		for (;;) {
			parser.NextToken();
			if (parser.See(CiToken.EndOfFile))
				throw new ParseException("Macro definition not terminated");
			if (parser.See(left))
				level++;
			else if (parser.See(right))
				if (--level == 0)
					break;
		}
		parser.NextToken();
	}

	public static CiMacro ParseDefinition(CiParser parser)
	{
		CiMacro macro = new CiMacro();
		macro.Name = parser.ParseId();
		parser.Expect(CiToken.LeftParenthesis);
		List<string> arguments = new List<string>();
		if (parser.See(CiToken.Id)) {
			do
				arguments.Add(parser.ParseId());
			while (parser.Eat(CiToken.Comma));
		}
		parser.Expect(CiToken.RightParenthesis);
		macro.Arguments = arguments.ToArray();
		StringBuilder sb = new StringBuilder();
		parser.Lexer.CopyTo = sb;
		try {
			if (parser.See(CiToken.LeftParenthesis)) {
				sb.Append('(');
				ParseBody(parser, CiToken.LeftParenthesis, CiToken.RightParenthesis);
			}
			else if (parser.See(CiToken.LeftBrace)) {
				ParseBody(parser, CiToken.LeftBrace, CiToken.RightBrace);
				Trace.Assert(sb[sb.Length - 1] == '}');
				sb.Length--;
				macro.IsStatement = true;
			}
		}
		finally {
			parser.Lexer.CopyTo = null;
		}
		macro.Body = sb.ToString();
		return macro;
	}

	static string ParseArg(CiParser parser)
	{
		int level = 0;
		for (;;) {
			parser.NextToken();
			if (parser.See(CiToken.EndOfFile))
				throw new ParseException("Macro definition not terminated");
			if (parser.See(CiToken.LeftParenthesis))
				level++;
			else if (parser.See(CiToken.RightParenthesis))
				if (--level < 0)
					break;
			else if (level == 0 && parser.See(CiToken.Comma))
				break;
		}
		return null; // TODO
	}

	public static void Expand(CiParser parser, CiMacro macro)
	{
		parser.Expect(CiToken.LeftParenthesis);
		if (!parser.See(CiToken.RightParenthesis)) {
			do
				ParseArg(parser);
			while (parser.Eat(CiToken.Comma));
//			StringBuilder sb = new StringBuilder();
//			parser.Lexer.CopyTo = 
		}
		parser.Expect(CiToken.RightParenthesis);
		if (macro.IsStatement)
			parser.Expect(CiToken.Semicolon);
	}
}

}
