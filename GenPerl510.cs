// GenPerl510.cs - Perl 5.10+ code generator
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

public class GenPerl510 : GenPerl5
{
	public GenPerl510(string package) : base(package)
	{
	}

	bool InSwitch = false;

	public override void Visit(CiBreak stmt)
	{
		if (this.InSwitch)
			WriteLine("break;");
		else
			base.Visit(stmt);
	}

	public override void Visit(CiContinue stmt)
	{
		WriteLine("next;");
	}

	public override void Visit(CiDoWhile stmt)
	{
		bool oldInSwitch = this.InSwitch;
		this.InSwitch = false;
		base.Visit(stmt);
		this.InSwitch = oldInSwitch;
	}

	public override void Visit(CiFor stmt)
	{
		bool oldInSwitch = this.InSwitch;
		this.InSwitch = false;
		base.Visit(stmt);
		this.InSwitch = oldInSwitch;
	}

	public override void Visit(CiWhile stmt)
	{
		bool oldInSwitch = this.InSwitch;
		this.InSwitch = false;
		base.Visit(stmt);
		this.InSwitch = oldInSwitch;
	}

	public override void Visit(CiSwitch stmt)
	{
		bool oldInSwitch = this.InSwitch;
		this.InSwitch = true;
		bool oldBreakDoWhile = this.BreakDoWhile;
		this.BreakDoWhile = false;
		Write("given (");
		Write(stmt.Value);
		Write(") ");
		OpenBlock();
		foreach (CiCase kase in stmt.Cases) {
			Write("when (");
			if (kase.Values.Length > 1) {
				Write("[ ");
				bool first = true;
				foreach (object value in kase.Values) {
					if (first)
						first = false;
					else
						Write(", ");
					WriteConst(value);
				}
				Write(" ]");
			}
			else
				WriteConst(kase.Values[0]);
			Write(") ");
			OpenBlock();
			Write(kase.Body, BodyLengthWithoutLastBreak(kase.Body));
			if (kase.Fallthrough)
				WriteLine("continue;");
			CloseBlock();
		}
		if (stmt.DefaultBody != null) {
			int length = BodyLengthWithoutLastBreak(stmt.DefaultBody);
			if (length > 0) {
				Write("default ");
				OpenBlock();
				Write(stmt.DefaultBody, length);
				CloseBlock();
			}
		}
		CloseBlock();
		this.BreakDoWhile = oldBreakDoWhile;
		this.InSwitch = oldInSwitch;
	}

	static bool HasSwitch(ICiStatement stmt)
	{
		if (stmt is CiSwitch)
			return true;
		CiIf ifStmt = stmt as CiIf;
		if (ifStmt != null)
			return HasSwitch(ifStmt.OnTrue) || HasSwitch(ifStmt.OnFalse);
		CiLoop loop = stmt as CiLoop;
		if (loop != null)
			return HasSwitch(loop.Body);
		CiBlock block = stmt as CiBlock;
		if (block != null)
			return block.Statements.Any(s => HasSwitch(s));
		return false;
	}

	static bool HasSwitch(CiClass klass)
	{
		if (klass.Constructor != null && HasSwitch(klass.Constructor.Body))
			return true;
		return klass.Members.OfType<CiMethod>().Any(method => HasSwitch(method.Body));
	}

	protected override void WritePragmas(CiProgram prog)
	{
		if (prog.Globals.OfType<CiClass>().Any(klass => HasSwitch(klass)))
			WriteLine("use feature 'switch';");
	}
}

}
