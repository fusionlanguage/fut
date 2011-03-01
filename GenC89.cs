// GenC89.cs - C89 code generator
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

using System;
using System.Collections.Generic;

namespace Foxoft.Ci
{

public class GenC89 : GenC
{
	protected override void WriteBanner()
	{
		WriteLine("/* Generated automatically with \"cito\". Do not edit. */");
	}

	protected override string ToString(CiType type)
	{
		if (type == CiBoolType.Value)
			return "cibool";
		return type.Name;
	}

	protected override void WriteConst(object value)
	{
		if (value is bool)
			Write((bool) value ? "TRUE" : "FALSE");
		else
			base.WriteConst(value);
	}

	protected override void WriteBoolType()
	{
		WriteLine("typedef int cibool;");
		WriteLine("#ifndef TRUE");
		WriteLine("#define TRUE 1");
		WriteLine("#endif");
		WriteLine("#ifndef FALSE");
		WriteLine("#define FALSE 0");
		WriteLine("#endif");
	}

	static bool IsInlineVar(CiVar def)
	{
		if (def == null)
			return false;
		if (def.InitialValue == null)
			return true;
		if (def.Type is CiStringStorageType || def.Type is CiArrayStorageType)
			return false;
		return true;
	}

	void WriteVar(CiVar def)
	{
		Write(def.Type, def.Name);
		WriteLine(";");
	}

	public override void Visit(CiBlock block)
	{
		OpenBlock();

		// variable and const definitions, with initializers if possible
		bool canInitVar = true;
		HashSet<string> forVars = new HashSet<string>();
		foreach (ICiStatement stmt in block.Statements) {
			if (stmt is CiConst) {
				base.Visit((CiConst) stmt);
				continue;
			}
			CiVar def = stmt as CiVar;
			if (canInitVar) {
				if (IsInlineVar(def)) {
					base.Visit(def);
					WriteLine(";");
					continue;
				}
				canInitVar = false;
			}
			if (def != null)
				WriteVar(def);
			else if (stmt is CiFor) {
				def = ((CiFor) stmt).Init as CiVar;
				#warning TODO: check "for" variables are same type
				if (def != null && !forVars.Contains(def.Name)) {
					forVars.Add(def.Name);
					WriteVar(def);
				}
			}
		}

		// other statements
		canInitVar = true;
		foreach (ICiStatement stmt in block.Statements) {
			CiVar def = stmt as CiVar;
			if (canInitVar) {
				if (IsInlineVar(def))
					continue;
				canInitVar = false;
			}
			if (def == null || def.InitialValue != null) // avoid lines with just semicolons
				Write(stmt);
		}

		CloseBlock();
	}

	public override void Visit(CiVar stmt)
	{
		if (stmt.InitialValue != null) {
			if (stmt.Type is CiArrayStorageType)
				WriteClearArray(new CiVarAccess { Var = stmt });
			else {
				Visit(new CiAssign {
					Target = new CiVarAccess { Var = stmt },
					Op = CiToken.Assign,
					Source = stmt.InitialValue
				});
			}
		}
	}

	public override void Visit(CiConst stmt)
	{
	}

	protected override void StartSwitch(CiCase[] kases)
	{
		foreach (CiCase kase in kases) {
			foreach (ICiStatement stmt in kase.Body) {
				if (stmt is CiConst)
					base.Visit((CiConst) stmt);
				else if (stmt is CiVar)
					WriteVar((CiVar) stmt);
			}
		}
	}

	protected override void StartCase(ICiStatement stmt)
	{
	}
}

}
