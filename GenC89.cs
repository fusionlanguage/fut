// GenC89.cs - C89 code generator
//
// Copyright (C) 2011-2012  Piotr Fusik
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

	void WriteVar(CiVar def)
	{
		Write(def.Type, def.Name);
		WriteLine(";");
		def.WriteInitialValue = true;
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiIntType.MulDivMethod) {
			Write("(int) ((double) ");
			WriteMulDiv(2, expr);
		}
		else
			base.Write(expr);
	}

	protected override void StartBlock(ICiStatement[] statements)
	{
		// variable and const definitions, with initializers if possible
		bool canInitVar = true;
		HashSet<string> forVars = new HashSet<string>();
		foreach (ICiStatement stmt in statements) {
			if (stmt is CiConst) {
				base.Visit((CiConst) stmt);
				continue;
			}
			CiVar def = stmt as CiVar;
			if (canInitVar) {
				if (def != null && IsInlineVar(def)) {
					base.Visit(def);
					def.WriteInitialValue = false;
					WriteLine(";");
					continue;
				}
				canInitVar = false;
			}
			if (def != null)
				WriteVar(def);
			else if (stmt is CiFor) {
				def = ((CiFor) stmt).Init as CiVar;
				// TODO: check "for" variables are same type
				if (def != null) {
					if (!forVars.Contains(def.Name)) {
						forVars.Add(def.Name);
						WriteVar(def);
					}
					def.WriteInitialValue = true;
				}
			}
		}
	}

	public override void Visit(CiBlock block)
	{
		OpenBlock();
		StartBlock(block.Statements);
		Write(block.Statements);
		CloseBlock();
	}

	public override void Visit(CiVar stmt)
	{
		if (stmt.WriteInitialValue) {
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
			else if (stmt.Type is CiClassStorageType) {
				CiClass klass = ((CiClassStorageType) stmt.Type).Class;
				if (klass.Constructs)
					WriteConstruct(klass, stmt);
			}
			stmt.WriteInitialValue = false;
		}
	}

	public override void Visit(CiConst stmt)
	{
	}

	protected override void StartSwitch(CiCase[] kases)
	{
		this.Indent++;
		foreach (CiCase kase in kases) {
			foreach (ICiStatement stmt in kase.Body) {
				if (stmt is CiConst)
					base.Visit((CiConst) stmt);
				else if (stmt is CiVar)
					WriteVar((CiVar) stmt);
			}
		}
		this.Indent--;
	}

	protected override void StartCase(ICiStatement stmt)
	{
	}
}

}
