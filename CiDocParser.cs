// CiDocParser.cs - Ci documentation parser
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
using System.Text;

namespace Foxoft.Ci
{

public class CiDocParser : CiDocLexer
{
	public CiDocParser(CiLexer ciLexer) : base(ciLexer)
	{
	}

	bool See(CiDocToken token)
	{
		return this.CurrentToken == token;
	}

	bool Eat(CiDocToken token)
	{
		if (See(token)) {
			NextToken();
			return true;
		}
		return false;
	}

	void Expect(CiDocToken expected)
	{
		if (!See(expected))
			throw new ParseException("Expected {0}, got {1}", expected, this.CurrentToken);
		NextToken();
	}

	string ParseText()
	{
		StringBuilder sb = new StringBuilder();
		while (See(CiDocToken.Char)) {
			sb.Append((char) this.CurrentChar);
			NextToken();
		}
		if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
			sb.Length--;
		return sb.ToString();
	}

	CiDocPara ParsePara()
	{
		List<CiDocInline> children = new List<CiDocInline>();
		for (;;) {
			if (See(CiDocToken.Char)) {
				children.Add(new CiDocText {
					Text = ParseText()
				});
			}
			else if (Eat(CiDocToken.CodeDelimiter)) {
				children.Add(new CiDocCode {
					Text = ParseText()
				});
				Expect(CiDocToken.CodeDelimiter);
			}
			else
				break;
		}
		return new CiDocPara { Children = children.ToArray() };
	}

	CiDocBlock ParseBlock()
	{
		if (Eat(CiDocToken.Bullet)) {
			List<CiDocPara> items = new List<CiDocPara>();
			do
				items.Add(ParsePara());
			while (Eat(CiDocToken.Bullet));
			Eat(CiDocToken.Para);
			return new CiDocList { Items = items.ToArray() };
		}
		return ParsePara();
	}

	public CiCodeDoc ParseCodeDoc()
	{
		CiDocPara summary = ParsePara();
		List<CiDocBlock> details = new List<CiDocBlock>();
		if (Eat(CiDocToken.Period)) {
			while (!See(CiDocToken.EndOfFile))
				details.Add(ParseBlock());
		}
		return new CiCodeDoc { Summary = summary, Details = details.ToArray() };
	}
}

}
