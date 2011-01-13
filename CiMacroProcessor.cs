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
using System.IO;
using System.Text;

namespace Foxoft.Ci
{

public class CiMacro : CiSymbol
{
	public string[] Arguments;
	public bool IsStatement;
	public string Body;
}

public class MacroExpansion
{
	public Dictionary<string, string> Args;
	public TextReader ParentReader;
	public string LookupArg(string name)
	{
		string value;
		if (this.Args.TryGetValue(name, out value))
			return value;
		return name;
	}
}

public partial class CiParser : CiLexer
{
	void ParseBody(CiToken left, CiToken right)
	{
		int level = 1;
		for (;;) {
			NextToken();
			if (See(CiToken.EndOfFile))
				throw new ParseException("Macro definition not terminated");
			if (See(left))
				level++;
			else if (See(right))
				if (--level == 0)
					break;
		}
	}

	CiMacro ParseMacro()
	{
		CiMacro macro = new CiMacro();
		macro.Name = ParseId();
		Expect(CiToken.LeftParenthesis);
		List<string> arguments = new List<string>();
		if (See(CiToken.Id)) {
			do {
				string name = ParseId();
				if (arguments.Contains(name))
					throw new ParseException("Duplicate macro argument {0}", name);
				arguments.Add(name);
			} while (Eat(CiToken.Comma));
		}
		Expect(CiToken.RightParenthesis);
		macro.Arguments = arguments.ToArray();
		StringBuilder sb = new StringBuilder();
		this.CopyTo = sb;
		try {
			if (See(CiToken.LeftParenthesis)) {
				sb.Append('(');
				ParseBody(CiToken.LeftParenthesis, CiToken.RightParenthesis);
			}
			else if (See(CiToken.LeftBrace)) {
				ParseBody(CiToken.LeftBrace, CiToken.RightBrace);
				Trace.Assert(sb[sb.Length - 1] == '}');
				sb.Length--;
				macro.IsStatement = true;
			}
		}
		finally {
			this.CopyTo = null;
		}
		macro.Body = sb.ToString();
		NextToken();
		return macro;
	}

	void ParseArg()
	{
		int level = 0;
		for (;;) {
			NextToken();
			if (See(CiToken.EndOfFile))
				throw new ParseException("Macro definition not terminated");
			if (See(CiToken.LeftParenthesis))
				level++;
			else if (See(CiToken.RightParenthesis)) {
				if (--level < 0)
					break;
			}
			else if (level == 0 && See(CiToken.Comma))
				break;
		}
	}

	readonly Stack<MacroExpansion> MacroStack = new Stack<MacroExpansion>();

	void Expand(CiMacro macro)
	{
		Dictionary<string, string> args = new Dictionary<string, string>();
		StringBuilder sb = new StringBuilder();
		this.CopyTo = sb;
		try {
			Expect(CiToken.LeftParenthesis);
			bool first = true;
			foreach (string name in macro.Arguments) {
				if (first)
					first = false;
				else
					Expect(CiToken.Comma);
				ParseArg();
				char c = sb[sb.Length - 1];
				Trace.Assert(c == ',' || c == ')');
				sb.Length--;
				args.Add(name, sb.ToString());
				sb.Clear();
			}
		}
		finally {
			this.CopyTo = null;
		}
		Check(CiToken.RightParenthesis);
		if (macro.IsStatement) {
			NextToken();
			Check(CiToken.Semicolon);
		}
		this.MacroStack.Push(new MacroExpansion {
			Args = args,
			ParentReader = SetReader(new StringReader(macro.Body))
		});
	}

	public string LookupMacroArg(string name)
	{
		if (this.MacroStack.Count > 0)
			return this.MacroStack.Peek().LookupArg(name);
		return name;
	}

	public bool EndMacroExpansion()
	{
		if (this.MacroStack.Count > 0) {
			MacroExpansion top = this.MacroStack.Pop();
			SetReader(top.ParentReader);
			return true;
		}
		return false;
	}
}

}
