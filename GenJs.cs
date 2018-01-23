// GenJs.cs - JavaScript code generator
//
// Copyright (C) 2011-2018  Piotr Fusik
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

enum GenJsMethod
{
	CopyArray,
	FillArray,
	Count
}

public class GenJs : GenBase
{
	// TODO: Namespace
	readonly string[][] Library = new string[(int) GenJsMethod.Count][];
	CiClass CurrentClass;

	protected override void Write(CiType type, bool promote)
	{
		throw new InvalidOperationException();
	}

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiConst)
			WriteUppercaseWithUnderscores(symbol.Name);
		else if (symbol is CiMember)
			WriteCamelCase(symbol.Name);
		else
			Write(symbol.Name);
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
	}

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		Write("[ ");
		WriteCoerced(null, expr.Items);
		Write(" ]");
		return expr;
	}

	protected override void WriteVar(CiNamedValue def)
	{
		Write("var ");
		base.WriteVar(def);
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		CiMember member = expr.Symbol as CiMember;
		if (member != null) {
			if (member.IsStatic()) {
				Write(this.CurrentClass.Name);
				Write('.');
			}
			else
				Write("this.");
		}
		WriteName(expr.Symbol);
		return expr;
	}

	protected override bool WriteNewArray(CiType type)
	{
		Write("new Array(");
		CiArrayStorageType array = (CiArrayStorageType) type;
		array.LengthExpr.Accept(this, CiPriority.Statement);
		Write(')');
		return false; // inner dimensions not allocated
	}

	protected override void WriteInt()
	{
		Write("var");
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("Ci.");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		expr.Accept(this, CiPriority.Primary);
		Write(".length");
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		expr.Left.Accept(this, CiPriority.Primary);
		Write(".charCodeAt(");
		WritePromoted(expr.Right, CiPriority.Statement);
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, string method, CiExpr[] args)
	{
		if (obj.Type is CiArrayType && method == "CopyTo") {
			if (this.Library[(int) GenJsMethod.CopyArray] == null) {
				this.Library[(int) GenJsMethod.CopyArray] = new string[] {
					"copyArray : function(sa, soffset, da, doffset, length)",
					"for (var i = 0; i < length; i++)",
					"\tda[doffset + i] = sa[soffset + i];"
				};
			}
			Write("Ci.copyArray(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			WritePromoted(args);
			Write(')');
		}
		else if (obj.Type is CiArrayStorageType && method == "Fill") {
			if (this.Library[(int) GenJsMethod.FillArray] == null) {
				this.Library[(int) GenJsMethod.FillArray] = new string[] {
					"fillArray : function(a, length, value)",
					"for (var i = 0; i < length; i++)",
					"\ta[i] = value;"
				};
			}
			Write("Ci.fillArray(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			((CiArrayStorageType) obj.Type).LengthExpr.Accept(this, CiPriority.Statement);
			Write(", ");
			args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else {
			obj.Accept(this, CiPriority.Primary);
			Write('.');
			if (IsMathReference(obj) && method == "Ceiling")
				Write("ceil");
			else
				WriteCamelCase(method);
			Write('(');
			WritePromoted(args);
			Write(')');
		}
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Op == CiToken.Slash && expr.Type is CiIntegerType) {
			if (parent > CiPriority.Or)
				Write('(');
			WritePromoted(expr.Left, CiPriority.Mul);
			Write(" / ");
			WritePromoted(expr.Right, CiPriority.Primary);
			Write(" | 0"); // FIXME: long: Math.trunc?
			if (parent > CiPriority.Or)
				Write(')');
			return expr;
		}
		return base.Visit(expr, parent);
	}

	public override void Visit(CiThrow statement)
	{
		Write("throw ");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine(";");
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write("var ");
		Write(enu.Name);
		Write(" = ");
		OpenBlock();
		int i = 0;
		foreach (CiConst konst in enu) {
			if (i > 0)
				WriteLine(",");
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" : ");
			if (konst.Value != null)
				konst.Value.Accept(this, CiPriority.Statement);
			else
				Write(i);
			i++;
		}
		WriteLine();
		CloseBlock();
	}

	void Write(CiClass klass, CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		WriteLine();
		Write(klass.Name);
		Write('.');
		if (method.CallType != CiCallType.Static)
			Write("prototype.");
		WriteCamelCase(method.Name);
		Write(" = function(");
		bool first = true;
		foreach (CiVar param in method.Parameters) {
			if (!first)
				Write(", ");
			first = false;
			Write(param.Name);
		}
		Write(')');
		WriteBody(method);
	}

	void WriteConsts(CiClass klass, IEnumerable<CiConst> konsts)
	{
		foreach (CiConst konst in konsts) {
			if (konst.Visibility != CiVisibility.Private || konst.Type is CiArrayStorageType) {
				Write(klass.Name);
				Write('.');
				base.WriteVar(konst);
				WriteLine(";");
			}
		}
	}

	void Write(CiClass klass)
	{
		this.CurrentClass = klass;
		WriteLine();
		Write("function ");
		Write(klass.Name);
		WriteLine("()");
		OpenBlock();
		foreach (CiField field in klass.Fields) {
			if (field.Value != null || field.Type is CiClass || field.Type is CiArrayStorageType) {
				Write("this.");
				base.WriteVar(field);
				TerminateStatement();
			}
		}
		if (klass.Constructor != null)
			Write(((CiBlock) klass.Constructor.Body).Statements);
		CloseBlock();
		if (klass.BaseClassName != null) {
			Write(klass.Name);
			Write(".prototype = new ");
			Write(klass.BaseClassName);
			WriteLine("();");
		}
		WriteConsts(klass, klass.Consts);
		foreach (CiMethod method in klass.Methods)
			Write(klass, method);
		WriteConsts(klass, klass.ConstArrays);
		this.CurrentClass = null;
	}

	void WriteLib(Dictionary<string, byte[]> resources)
	{
		WriteLine();
		Write("var Ci = ");
		OpenBlock();
		bool first = true;
		foreach (string[] method in this.Library) {
			if (method != null) {
				if (!first)
					WriteLine(",");
				first = false;
				WriteLine(method[0]);
				OpenBlock();
				for (int i = 1; i < method.Length; i++)
					WriteLine(method[i]);
				this.Indent--;
				Write('}');
			}
		}

		foreach (string name in resources.Keys.OrderBy(k => k)) {
			if (!first)
				WriteLine(",");
			first = false;
			WriteResource(name, -1);
			WriteLine(" : [");
			Write('\t');
			Write(resources[name]);
			Write(" ]");
		}
		WriteLine();
		CloseBlock();
	}

	public override void Write(CiProgram program)
	{
		CreateFile(this.OutputFile);
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.OfType<CiClass>()) // TODO: topological sort of class hierarchy
			Write(klass);
		if (program.Resources.Count > 0 || this.Library.Any(l => l != null))
			WriteLib(program.Resources);
		CloseFile();
	}
}

}

