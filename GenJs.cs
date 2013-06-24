// GenJs.cs - JavaScript code generator
//
// Copyright (C) 2011-2013  Piotr Fusik
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

public class GenJs : SourceGenerator
{
	CiClass CurrentClass;
	bool UsesSubstringMethod;
	bool UsesCopyArrayMethod;
	bool UsesBytesToStringMethod;
	bool UsesClearArrayMethod;

	protected override void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		// TODO
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		Write("var ");
		Write(enu.Name);
		Write(" = ");
		OpenBlock();
		for (int i = 0; i < enu.Values.Length; i++) {
			if (i > 0)
				WriteLine(",");
			CiEnumValue value = enu.Values[i];
			Write(value.Documentation);
			WriteUppercaseWithUnderscores(value.Name);
			Write(" : ");
			Write(i);
		}
		WriteLine();
		CloseBlock();
	}

	protected override void WriteNew(CiType type)
	{
		CiClassStorageType classType = type as CiClassStorageType;
		if (classType != null) {
			Write("new ");
			Write(classType.Class.Name);
			Write("()");
		}
		else {
			CiArrayStorageType arrayType = (CiArrayStorageType) type;
			Write("new Array(");
			if (arrayType.LengthExpr != null)
				Write(arrayType.LengthExpr);
			else
				Write(arrayType.Length);
			Write(')');
		}
	}

	bool WriteInit(CiType type)
	{
		if (type is CiClassStorageType || type is CiArrayStorageType) {
			Write(" = ");
			WriteNew(type);
			return true;
		}
		return false;
	}

	void Write(CiField field)
	{
		Write(field.Documentation);
		Write("this.");
		WriteCamelCase(field.Name);
		CiType type = field.Type;
		if (type == CiBoolType.Value)
			Write(" = false");
		else if (type == CiByteType.Value || type == CiIntType.Value)
			Write(" = 0");
		else if (type is CiEnum) {
			Write(" = ");
			WriteConst(((CiEnum) type).Values[0]);
		}
		else if (!WriteInit(type))
			Write(" = null");
		WriteLine(";");
	}

	protected override void WriteConst(object value)
	{
		if (value is CiEnumValue) {
			CiEnumValue ev = (CiEnumValue) value;
			Write(ev.Type.Name);
			Write('.');
			WriteUppercaseWithUnderscores(ev.Name);
		}
		else if (value is Array) {
			Write("[ ");
			WriteContent((Array) value);
			Write(" ]");
		}
		else
			base.WriteConst(value);
	}

	protected override void WriteName(CiConst konst)
	{
		Write(this.CurrentClass.Name);
		Write('.');
		WriteUppercaseWithUnderscores(konst.GlobalName ?? konst.Name);
	}

	protected override CiPriority GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiLibrary.SByteProperty)
				return CiPriority.Additive;
			if (prop == CiLibrary.LowByteProperty)
				return CiPriority.And;
		}
		else if (expr is CiBinaryExpr) {
			if (((CiBinaryExpr) expr).Op == CiToken.Slash)
				return CiPriority.Postfix;
		}
		return base.GetPriority(expr);
	}

	protected override void Write(CiFieldAccess expr)
	{
		WriteChild(expr, expr.Obj);
		Write('.');
		WriteCamelCase(expr.Field.Name);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiLibrary.SByteProperty) {
			Write('(');
			WriteChild(CiPriority.Xor, expr.Obj);
			Write(" ^ 128) - 128");
		}
		else if (expr.Property == CiLibrary.LowByteProperty) {
			WriteChild(expr, expr.Obj);
			Write(" & 0xff");
		}
		else if (expr.Property == CiLibrary.StringLengthProperty) {
			WriteChild(expr, expr.Obj);
			Write(".length");
		}
		else
			throw new ArgumentException(expr.Property.Name);
	}

	protected override void WriteName(CiBinaryResource resource)
	{
		Write(this.CurrentClass.Name);
		Write(".CI_BINARY_RESOURCE_");
		foreach (char c in resource.Name)
			Write(CiLexer.IsLetter(c) ? char.ToUpperInvariant(c) : '_');
	}

	protected override void WriteName(CiMethod method)
	{
		WriteCamelCase(method.Name);
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiLibrary.MulDivMethod) {
			Write("Math.floor(");
			WriteMulDiv(CiPriority.Multiplicative, expr);
		}
		else if (expr.Method == CiLibrary.CharAtMethod) {
			Write(expr.Obj);
			Write(".charCodeAt(");
			Write(expr.Arguments[0]);
			Write(')');
		}
		else if (expr.Method == CiLibrary.SubstringMethod) {
			if (expr.Arguments[0].HasSideEffect) {
				Write("Ci.substring(");
				Write(expr.Obj);
				Write(", ");
				Write(expr.Arguments[0]);
				Write(", ");
				Write(expr.Arguments[1]);
				Write(')');
				this.UsesSubstringMethod = true;
			}
			else {
				Write(expr.Obj);
				Write(".substring(");
				Write(expr.Arguments[0]);
				Write(", ");
				Write(new CiBinaryExpr { Left = expr.Arguments[0], Op = CiToken.Plus, Right = expr.Arguments[1] });
				Write(')');
			}
		}
		else if (expr.Method == CiLibrary.ArrayCopyToMethod) {
			Write("Ci.copyArray(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(", ");
			Write(expr.Arguments[2]);
			Write(", ");
			Write(expr.Arguments[3]);
			Write(')');
			this.UsesCopyArrayMethod = true;
		}
		else if (expr.Method == CiLibrary.ArrayToStringMethod) {
			Write("Ci.bytesToString(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
			this.UsesBytesToStringMethod = true;
		}
		else if (expr.Method == CiLibrary.ArrayStorageClearMethod) {
			Write("Ci.clearArray(");
			Write(expr.Obj);
			Write(", 0)");
			this.UsesClearArrayMethod = true;
		}
		else
			base.Write(expr);
	}

	protected override void Write(CiBinaryExpr expr)
	{
		if (expr.Op == CiToken.Slash) {
			Write("Math.floor(");
			WriteChild(CiPriority.Multiplicative, expr.Left);
			Write(" / ");
			WriteNonAssocChild(CiPriority.Multiplicative, expr.Right);
			Write(')');
		}
		else
			base.Write(expr);
	}

	protected virtual void WriteInitArrayStorageVar(CiVar stmt)
	{
		WriteLine(";");
		Write("Ci.clearArray(");
		Write(stmt.Name);
		Write(", ");
		Write(stmt.InitialValue);
		Write(')');
		this.UsesClearArrayMethod = true;
	}

	public override void Visit(CiVar stmt)
	{
		Write("var ");
		Write(stmt.Name);
		WriteInit(stmt.Type);
		if (stmt.InitialValue != null) {
			if (stmt.Type is CiArrayStorageType)
				WriteInitArrayStorageVar(stmt);
			else {
				Write(" = ");
				Write(stmt.InitialValue);
			}
		}
	}

	public override void Visit(CiThrow stmt)
	{
		Write("throw ");
		Write(stmt.Message);
		WriteLine(";");
	}

	void Write(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		WriteLine();
		Write(method.Class.Name);
		Write('.');
		if (method.CallType != CiCallType.Static)
			Write("prototype.");
		WriteCamelCase(method.Name);
		Write(" = function(");
		bool first = true;
		foreach (CiParam param in method.Signature.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Name);
		}
		Write(") ");
		Write(method.Body);
	}

	void Write(CiConst konst)
	{
		WriteName(konst);
		Write(" = ");
		WriteConst(konst.Value);
		WriteLine(";");
	}

	void Write(CiClass klass)
	{
		// topological sorting of class hierarchy
		if (klass.WriteStatus == CiWriteStatus.Done)
			return;
		if (klass.WriteStatus == CiWriteStatus.InProgress)
			throw new ResolveException("Circular dependency for class {0}", klass.Name);
		klass.WriteStatus = CiWriteStatus.InProgress;
		if (klass.BaseClass != null)
			Write(klass.BaseClass);
		klass.WriteStatus = CiWriteStatus.Done;

		this.CurrentClass = klass;
		WriteLine();
		Write(klass.Documentation);
		Write("function ");
		Write(klass.Name);
		WriteLine("()");
		OpenBlock();
		foreach (CiSymbol member in klass.Members) {
			if (member is CiField)
				Write((CiField) member);
		}
		if (klass.Constructor != null)
			Write(klass.Constructor.Body.Statements);
		CloseBlock();
		if (klass.BaseClass != null) {
			Write(klass.Name);
			Write(".prototype = new ");
			Write(klass.BaseClass.Name);
			WriteLine("();");
		}
		foreach (CiSymbol member in klass.Members) {
			if (member is CiMethod)
				Write((CiMethod) member);
			else if (member is CiConst && member.Visibility == CiVisibility.Public)
				Write((CiConst) member);
		}
		foreach (CiConst konst in klass.ConstArrays)
			Write(konst);
		foreach (CiBinaryResource resource in klass.BinaryResources) {
			WriteName(resource);
			Write(" = ");
			WriteConst(resource.Content);
			WriteLine(";");
		}
		this.CurrentClass = null;
	}

	void WriteBuiltins()
	{
		List<string[]> code = new List<string[]>();
		if (this.UsesSubstringMethod) {
			code.Add(new string[] {
				"substring : function(s, offset, length)",
				"return s.substring(offset, offset + length);"
			});
		}
		if (this.UsesCopyArrayMethod) {
			code.Add(new string[] {
				"copyArray : function(sa, soffset, da, doffset, length)",
				"for (var i = 0; i < length; i++)",
				"\tda[doffset + i] = sa[soffset + i];"
			});
		}
		if (this.UsesBytesToStringMethod) {
			code.Add(new string[] {
				"bytesToString : function(a, offset, length)",
				"var s = \"\";",
				"for (var i = 0; i < length; i++)",
				"\ts += String.fromCharCode(a[offset + i]);",
				"return s;"
			});
		}
		if (this.UsesClearArrayMethod) {
			code.Add(new string[] {
				"clearArray : function(a, value)",
				"for (var i = 0; i < a.length; i++)",
				"\ta[i] = value;"
			});
		}
		if (code.Count > 0) {
			WriteLine("var Ci = {");
			this.Indent++;
			for (int i = 0; ; ) {
				string[] lines = code[i];
				Write(lines[0]);
				WriteLine(" {");
				this.Indent++;
				for (int j = 1; j < lines.Length; j++)
					WriteLine(lines[j]);
				this.Indent--;
				Write('}');
				if (++i >= code.Count)
					break;
				WriteLine(",");
			}
			WriteLine();
			this.Indent--;
			WriteLine("};");
		}
	}

	public override void Write(CiProgram prog)
	{
		CreateFile(this.OutputFile);
		this.UsesSubstringMethod = false;
		this.UsesCopyArrayMethod = false;
		this.UsesBytesToStringMethod = false;
		this.UsesClearArrayMethod = false;
		foreach (CiSymbol symbol in prog.Globals)
			if (symbol is CiClass)
				((CiClass) symbol).WriteStatus = CiWriteStatus.NotYet;
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiEnum)
				Write((CiEnum) symbol);
			else if (symbol is CiClass)
				Write((CiClass) symbol);
		}
		WriteBuiltins();
		CloseFile();
	}
}

}
