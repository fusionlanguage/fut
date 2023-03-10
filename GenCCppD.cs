// GenCCppD.cs - Base for C/C++/D code generators
//
// Copyright (C) 2011-2023  Piotr Fusik
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
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

public abstract class GenCCppD : GenTyped
{
	protected readonly List<CiSwitch> SwitchesWithGoto = new List<CiSwitch>();

	public override void VisitConst(CiConst statement)
	{
		if (statement.Type is CiArrayStorageType)
			WriteConst(statement);
	}

	public override void VisitBreak(CiBreak statement)
	{
		if (statement.LoopOrSwitch is CiSwitch switchStatement) {
			int gotoId = this.SwitchesWithGoto.IndexOf(switchStatement);
			if (gotoId >= 0) {
				Write("goto ciafterswitch");
				VisitLiteralLong(gotoId);
				WriteCharLine(';');
				return;
			}
		}
		base.VisitBreak(statement);
	}

	protected int GetSwitchGoto(CiSwitch statement)
	{
		if (statement.Cases.Any(kase => CiSwitch.HasEarlyBreakAndContinue(kase.Body))
		 || CiSwitch.HasEarlyBreakAndContinue(statement.DefaultBody)) {
			this.SwitchesWithGoto.Add(statement);
			return this.SwitchesWithGoto.Count - 1;
		}
		return -1;
	}

	protected void WriteIfCaseBody(List<CiStatement> body, bool doWhile)
	{
		int length = CiSwitch.LengthWithoutTrailingBreak(body);
		if (doWhile && CiSwitch.HasEarlyBreak(body)) {
			this.Indent++;
			WriteNewLine();
			Write("do ");
			OpenBlock();
			WriteFirstStatements(body, length);
			CloseBlock();
			WriteLine("while (0);");
			this.Indent--;
		}
		else if (length == 1)
			WriteChild(body[0]);
		else {
			WriteChar(' ');
			OpenBlock();
			WriteFirstStatements(body, length);
			CloseBlock();
		}
	}

	protected void EndSwitchAsIfs(CiSwitch statement, int gotoId)
	{
		if (statement.HasDefault()) {
			Write("else");
			WriteIfCaseBody(statement.DefaultBody, gotoId < 0);
		}
		if (gotoId >= 0) {
			Write("ciafterswitch");
			VisitLiteralLong(gotoId);
			WriteLine(": ;");
		}
	}


}

}
