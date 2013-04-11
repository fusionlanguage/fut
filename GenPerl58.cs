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
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci
{

public class GenPerl58 : GenPerl5
{
	public GenPerl58(string package) : base(package)
	{
	}

	bool InEarlyBreakSwitch = false;

	public override void Visit(CiBreak stmt)
	{
		WriteLine("last;");
	}

	public override void Visit(CiContinue stmt)
	{
		if (this.InEarlyBreakSwitch)
			WriteLine("next LOOP;");
		else
			WriteLine("next;");
	}

	static bool HasEarlyBreak(CiSwitch stmt)
	{
		foreach (CiCase kase in stmt.Cases) {
			int length = BodyLengthWithoutLastBreak(kase);
			for (int i = 0; i < length; i++)
				if (HasBreak(kase.Body[i]))
					return true;
		}
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

	void WriteLoopLabel(CiLoop stmt)
	{
		if (HasSwitchContinueAndEarlyBreak(stmt.Body))
			Write("LOOP: ");
	}

	public override void Visit(CiFor stmt)
	{
		WriteLoopLabel(stmt);
		base.Visit(stmt);
	}

	public override void Visit(CiWhile stmt)
	{
		WriteLoopLabel(stmt);
		base.Visit(stmt);
	}

	public override void Visit(CiSwitch stmt)
	{
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
		for (int i = 0; i < stmt.Cases.Length; i++) {
			CiCase kase = stmt.Cases[i];
			if (kase.Value != null) {
				if (i > 0)
					Write("els");
				Write("if (");
				for (;;) {
					if (tmpVar)
						Write("$CISWITCH");
					else
						WriteChild(7, stmt.Value);
					Write(" == ");
					WriteConst(kase.Value);
					if (kase.Body.Length > 0 || i + 1 >= stmt.Cases.Length)
						break;
					Write(" || ");
					// TODO: "case 5: default:"
					// TODO: optimize ranges "case 1: case 2: case 3:"
					kase = stmt.Cases[++i];
				}
				Write(") ");
			}
			else
				Write("else "); // TODO: default that doesn't come last
			OpenBlock();
			Write(kase.Body, BodyLengthWithoutLastBreak(kase)); // TODO: fallthrough with gotos
			CloseBlock();
		}
		if (hasEarlyBreak) {
			CloseBlock();
			this.InEarlyBreakSwitch = oldInEarlyBreakSwitch;
		}
	}
}

}
