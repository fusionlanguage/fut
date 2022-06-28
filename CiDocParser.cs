// CiDocParser.cs - Ci documentation parser
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

using System.Collections.Generic;
using System.Text;

namespace Foxoft.Ci
{

public class CiDocParser
{
	readonly CiLexer Lexer;

	public CiDocParser(CiLexer lexer)
	{
		this.Lexer = lexer;
	}

	string ParseText()
	{
		StringBuilder sb = new StringBuilder();
		while (this.Lexer.DocSee(CiDocToken.Char)) {
			sb.Append((char) this.Lexer.DocCurrentChar);
			this.Lexer.DocNextToken();
		}
		if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
			sb.Length--;
		return sb.ToString();
	}

	CiDocPara ParsePara()
	{
		List<CiDocInline> children = new List<CiDocInline>();
		for (;;) {
			if (this.Lexer.DocSee(CiDocToken.Char)) {
				children.Add(new CiDocText {
					Text = ParseText()
				});
			}
			else if (this.Lexer.DocEat(CiDocToken.CodeDelimiter)) {
				children.Add(new CiDocCode {
					Text = ParseText()
				});
				this.Lexer.DocExpect(CiDocToken.CodeDelimiter);
			}
			else
				break;
		}
		this.Lexer.DocEat(CiDocToken.Para);
		return new CiDocPara { Children = children.ToArray() };
	}

	CiDocBlock ParseBlock()
	{
		if (this.Lexer.DocEat(CiDocToken.Bullet)) {
			List<CiDocPara> items = new List<CiDocPara>();
			do
				items.Add(ParsePara());
			while (this.Lexer.DocEat(CiDocToken.Bullet));
			this.Lexer.DocEat(CiDocToken.Para);
			return new CiDocList { Items = items.ToArray() };
		}
		return ParsePara();
	}

	public CiCodeDoc ParseCodeDoc()
	{
		this.Lexer.DocCheckPeriod = true;
		this.Lexer.DocCurrentChar = '\n';
		this.Lexer.DocNextToken();

		CiDocPara summary = ParsePara();
		List<CiDocBlock> details = new List<CiDocBlock>();
		if (this.Lexer.DocEat(CiDocToken.Period)) {
			this.Lexer.DocEat(CiDocToken.Para);
			while (!this.Lexer.DocSee(CiDocToken.EndOfFile))
				details.Add(ParseBlock());
		}
		return new CiCodeDoc { Summary = summary, Details = details.ToArray() };
	}
}

}
