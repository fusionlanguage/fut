// GenJs.cs - JavaScript code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
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
	UTF8GetString,
	Count
}

public class GenJs : GenBase
{
	// TODO: Namespace
	readonly string[][] Library = new string[(int) GenJsMethod.Count][];
	CiClass CurrentClass;

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiConst konst) {
			if (konst.InMethod != null) {
				WriteUppercaseWithUnderscores(konst.InMethod.Name);
				Write('_');
			}
			WriteUppercaseWithUnderscores(symbol.Name);
		}
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
		if (expr.Symbol is CiMember member) {
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

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr)
	{
		if (!(elementType is CiNumericType)) {
			Write("new Array(");
			lengthExpr.Accept(this, CiPriority.Statement);
			Write(')');
			return;
		}

		string name;
		int shift;
		if (elementType == CiSystem.IntType) {
			name = "Int32";
			shift = 2;
		}
		else if (elementType == CiSystem.DoubleType) {
			name = "Float64";
			shift = 3;
		}
		else if (elementType == CiSystem.FloatType) {
			name = "Float32";
			shift = 2;
		}
		else if (((CiIntegerType) elementType).IsLong) {
			// TODO: UInt32 if possible?
			name = "Float64"; // no 64-bit integers in JavaScript
			shift = 3;
		}
		else {
			CiRangeType range = (CiRangeType) elementType;
			if (range.Min < 0) {
				if (range.Min < short.MinValue || range.Max > short.MaxValue) {
					name = "Int32";
					shift = 2;
				}
				else if (range.Min < sbyte.MinValue || range.Max > sbyte.MaxValue) {
					name = "Int16";
					shift = 1;
				}
				else {
					name = "Int8";
					shift = 0;
				}
			}
			else if (range.Max > ushort.MaxValue) {
				name = "Int32";
				shift = 2;
			}
			else if (range.Max > byte.MaxValue) {
				name = "UInt16";
				shift = 1;
			}
			else {
				name = "Uint8";
				shift = 0;
			}
		}

		Write("new ");
		Write(name);
		Write("Array(new ArrayBuffer(");
		if (shift == 0)
			lengthExpr.Accept(this, CiPriority.Statement);
		else if (lengthExpr is CiLiteral literalLength)
			Write(((long) literalLength.Value) << shift);
		else {
			lengthExpr.Accept(this, CiPriority.Shift);
			Write(" << ");
			Write(shift);
		}
		Write("))");
	}

	protected override void WriteInt()
	{
		Write("var");
	}

	protected override void WriteCast(CiPrefixExpr expr, CiPriority parent)
	{
		expr.Inner.Accept(this, parent); // no-op
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("Ci.");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		if (expr.Op == CiToken.LeftParenthesis) {
			expr.Inner.Accept(this, parent); // no-op
			return expr;
		}
		return base.Visit(expr, parent);
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
		expr.Right.Accept(this, CiPriority.Statement);
		Write(')');
	}

	void AddLibrary(GenJsMethod id, params string[] method)
	{
		if (this.Library[(int) id] == null)
			this.Library[(int) id] = method;
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (method == CiSystem.StringSubstring) {
			obj.Accept(this, CiPriority.Primary);
			Write(".substring(");
			args[0].Accept(this, CiPriority.Statement);
			Write(", ");
			args[0].Accept(this, CiPriority.Add); // TODO: side effect
			Write(" + ");
			args[1].Accept(this, CiPriority.Add);
			Write(')');
		}
		else if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			AddLibrary(GenJsMethod.CopyArray,
				"copyArray : function(sa, soffset, da, doffset, length)",
				"for (var i = 0; i < length; i++)",
				"\tda[doffset + i] = sa[soffset + i];");
			Write("Ci.copyArray(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			WriteArgs(method, args);
			Write(')');
		}
		else if (obj.Type == CiSystem.UTF8EncodingClass && method.Name == "GetString") {
			AddLibrary(GenJsMethod.UTF8GetString,
				"utf8GetString : function(a, i, length)",
				"length += i;",
				"var s = \"\";",
				"while (i < length) {",
				"\tvar c = a[i];",
				"\tif (c < 0x80)",
				"\t\ti++;",
				"\telse if ((c & 0xe0) == 0xc0) {",
				"\t\tc = (c & 0x1f) << 6 | (a[i + 1] & 0x3f);",
				"\t\ti += 2;",
				"\t}",
				"\telse if ((c & 0xf0) == 0xe0) {",
				"\t\tc = (c & 0xf) << 12 | (a[i + 1] & 0x3f) << 6 | (a[i + 2] & 0x3f);",
				"\t\ti += 3;",
				"\t}",
				"\ts += String.fromCharCode(c);",
				"}",
				"return s;");
			Write("Ci.utf8GetString");
			WriteArgsInParentheses(method, args);
		}
		else {
			obj.Accept(this, CiPriority.Primary);
			Write('.');
			if (IsMathReference(obj) && method.Name == "Ceiling")
				Write("ceil");
			else if (method == CiSystem.StringContains)
				Write("includes");
			else
				WriteCamelCase(method.Name);
			WriteArgsInParentheses(method, args);
		}
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Op == CiToken.Slash && expr.Type is CiIntegerType) {
			if (parent > CiPriority.Or)
				Write('(');
			expr.Left.Accept(this, CiPriority.Mul);
			Write(" / ");
			expr.Right.Accept(this, CiPriority.Primary);
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
				WriteLine(";");
				WriteInitCode(field);
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

