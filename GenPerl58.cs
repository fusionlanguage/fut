// GenPerl58.cs - Perl 5.8- code generator
//
// Copyright (C) 2013  Piotr Fusik
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
using System.Diagnostics;
using System.Linq;

namespace Foxoft.Ci
{

public class GenPerl58 : GenPerl5
{
	public GenPerl58(string package) : base(package)
	{
	}

	bool InEarlyBreakSwitch = false;

	public override void Visit(CiContinue stmt)
	{
		if (this.InEarlyBreakSwitch)
			WriteLine("next LOOP;");
		else
			WriteLine("next;");
	}

	static bool HasEarlyBreak(ICiStatement[] body)
	{
		return body.Any(stmt => HasBreak(stmt) && !(stmt is CiBreak));
	}

	static bool HasEarlyBreak(CiSwitch stmt)
	{
		if (stmt.Cases.Any(kase => HasEarlyBreak(kase.Body)))
			return true;
		if (stmt.DefaultBody != null && HasEarlyBreak(stmt.DefaultBody))
			return true;
		return false;
	}

	static bool HasSwitchContinueAndEarlyBreak(ICiStatement stmt)
	{
		CiIf ifStmt = stmt as CiIf;
		if (ifStmt != null)
			return HasSwitchContinueAndEarlyBreak(ifStmt.OnTrue) || HasSwitchContinueAndEarlyBreak(ifStmt.OnFalse);
		CiBlock block = stmt as CiBlock;
		if (block != null)
			return block.Statements.Any(s => HasSwitchContinueAndEarlyBreak(s));
		CiSwitch switchStmt = stmt as CiSwitch;
		if (switchStmt != null)
			return HasEarlyBreak(switchStmt) && HasContinue(switchStmt);
		return false;
	}

	protected override void WriteLoopLabel(CiLoop stmt)
	{
		if (HasSwitchContinueAndEarlyBreak(stmt.Body))
			Write("LOOP: ");
	}

	public override void Visit(CiSwitch stmt)
	{
		bool oldBreakDoWhile = this.BreakDoWhile;
		this.BreakDoWhile = false;
		bool hasEarlyBreak = HasEarlyBreak(stmt);
		bool oldInEarlyBreakSwitch = this.InEarlyBreakSwitch;
		if (hasEarlyBreak) {
			this.InEarlyBreakSwitch = true;
			OpenBlock(); // block that "last" will exit
		}

		bool tmpVar = stmt.Value.HasSideEffect;
		if (tmpVar) {
			Write("my $CISWITCH = ");
			Write(stmt.Value);
			WriteLine(";");
		}

		bool first = true;
		foreach (CiCase kase in stmt.Cases) {
			if (!first)
				Write("els");
			Write("if (");
			first = true;
			// TODO: optimize ranges "case 1: case 2: case 3:"
			foreach (object value in kase.Values) {
				if (first)
					first = false;
				else
					Write(" || ");
				if (tmpVar)
					Write("$CISWITCH");
				else
					WriteChild(CiPriority.Equality, stmt.Value);
				Write(" == ");
				WriteConst(value);
			}
			Write(") ");
			OpenBlock();
			Write(kase.Body, BodyLengthWithoutLastBreak(kase.Body));
			// TODO: fallthrough
			CloseBlock();
			Debug.Assert(!first);
		}

		if (stmt.DefaultBody != null) {
			int length = BodyLengthWithoutLastBreak(stmt.DefaultBody);
			if (length > 0) {
				Write("else ");
				OpenBlock();
				Write(stmt.DefaultBody, length);
				CloseBlock();
			}
		}

		if (hasEarlyBreak) {
			CloseBlock();
			this.InEarlyBreakSwitch = oldInEarlyBreakSwitch;
		}
		this.BreakDoWhile = oldBreakDoWhile;
	}
}

}
