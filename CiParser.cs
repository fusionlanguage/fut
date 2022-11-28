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

	protected override CiLiteralDouble ParseDouble()
	{
		if (!double.TryParse(GetLexeme().Replace("_", ""),
			NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign,
			CultureInfo.InvariantCulture, out double d))
			ReportError("Invalid floating-point number");
		CiLiteralDouble result = new CiLiteralDouble { Line = this.Line, Type = this.Program.System.DoubleType, Value = d };
		NextToken();
		return result;
	}
}

}
