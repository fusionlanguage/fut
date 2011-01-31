// GenCs.cs - C# code generator
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
using System.Globalization;
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

public class GenCs : SourceGenerator
{
	public GenCs(string outputPath)
	{
	}

	void WriteDoc(string text)
	{
		foreach (char c in text) {
			switch (c) {
			case '&': Write("&amp;"); break;
			case '<': Write("&lt;"); break;
			case '>': Write("&gt;"); break;
			case '\n': WriteLine(); StartLine("/// "); break;
			default: Write(c); break;
			}
		}
	}

	void Write(CiDocPara para)
	{
		foreach (CiDocInline inline in para.Children) {
			CiDocText text = inline as CiDocText;
			if (text != null) {
				WriteDoc(text.Text);
				continue;
			}
			CiDocCode code = inline as CiDocCode;
			if (code != null) {
				switch (code.Text) {
				case "true": Write("<see langword=\"true\" />"); break;
				case "false": Write("<see langword=\"false\" />"); break;
				case "null": Write("<see langword=\"null\" />"); break;
				default:
					Write("<c>");
					WriteDoc(code.Text);
					Write("</c>");
					break;
				}
				continue;
			}
			throw new ApplicationException();
		}
	}

	void Write(CiDocBlock block)
	{
		CiDocList list = block as CiDocList;
		if (list != null) {
			WriteLine();
			StartLine("/// <list type=\"bullet\">");
			WriteLine();
			foreach (CiDocPara item in list.Items) {
				StartLine("/// <item>");
				Write(item);
				WriteLine("</item>");
			}
			StartLine("/// </list>");
			WriteLine();
			StartLine("/// ");
			return;
		}
		Write((CiDocPara) block);
	}

	void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		StartLine("/// <summary>");
		Write(doc.Summary);
		WriteLine("</summary>");
		if (doc.Details.Length > 0) {
			StartLine("/// <remarks>");
			foreach (CiDocBlock block in doc.Details)
				Write(block);
			WriteLine("</remarks>");
		}
	}

	void Write(CiEnumValue value)
	{
		Write(value.Documentation);
		StartLine(value.Name);
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		StartLine(enu.IsPublic ? "public " : "internal ");
		Write("enum ");
		Write(enu.Name);
		OpenBlock();
		bool first = true;
		foreach (CiEnumValue value in enu.Values) {
			if (first)
				first = false;
			else
				WriteLine(",");
			Write(value);
		}
		WriteLine();
		CloseBlock();
	}

	void WriteBaseType(CiType type)
	{
		if (type is CiStringType)
			Write("string");
		else
			Write(type.Name);
	}

	void Write(CiType type)
	{
		WriteBaseType(type.BaseType);
		for (int i = 0; i < type.ArrayLevel; i++)
			Write("[]");
		Write(' ');
	}

	bool WriteInit(CiType type)
	{
		CiClassStorageType classType = type as CiClassStorageType;
		if (classType != null) {
			Write(" = new {0}()", classType.Class.Name);
			return true;
		}
		CiArrayStorageType arrayType = type as CiArrayStorageType;
		if (arrayType != null) {
			Write(" = new ");
			WriteBaseType(arrayType.BaseType);
			WriteInitializer(arrayType);
			return true;
		}
		return false;
	}

	void Write(CiField field)
	{
		Write(field.Documentation);
		StartLine(field.IsPublic ? "public " : "internal ");
		if (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)
			Write("readonly ");
		Write(field.Type);
		Write(field.Name);
		WriteInit(field.Type);
		WriteLine(";");
	}

	void Write(CiClass clazz)
	{
		WriteLine();
		Write(clazz.Documentation);
		StartLine(clazz.IsPublic ? "public " : "internal ");
		Write("class ");
		Write(clazz.Name);
		OpenBlock();
		foreach (CiField field in clazz.Fields)
			Write(field);
		CloseBlock();
	}

	void Write(CiConst def)
	{
		Write(def.Documentation);
		StartLine(def.IsPublic ? "public " : "");
		Write("const ");
		Write(def.Type);
		Write(def.Name);
		Write(" = ");
		WriteConst(def.Value);
		WriteLine(";");
	}

	protected override void Write(CiConstAccess expr)
	{
		if (expr.Const.GlobalName != null)
			Write(expr.Const.GlobalName);
		else
			base.Write(expr);
	}

	protected override int GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiIntType.SByteProperty || prop == CiIntType.LowByteProperty)
			return 2;
		}
		return base.GetPriority(expr);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiIntType.SByteProperty) {
			Write("(sbyte) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiIntType.LowByteProperty) {
			Write("(byte) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiStringType.LengthProperty) {
			WriteChild(expr, expr.Obj);
			Write(".Length");
		}
		// TODO
		else
			throw new ApplicationException(expr.Property.Name);
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Function == CiIntType.MulDivMethod) {
			Write("(int) ((long) (");
			Write(expr.Obj);
			Write(") * (");
			Write(expr.Arguments[0]);
			Write(") / (");
			Write(expr.Arguments[1]);
			Write("))");
		}
		else if (expr.Function == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(']');
		}
		else if (expr.Function == CiArrayType.CopyToMethod) {
			Write("System.Array.Copy(");
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
		}
		else if (expr.Function == CiArrayType.ToStringMethod) {
			Write("System.Text.Encoding.UTF8.GetString(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Function == CiArrayStorageType.ClearMethod) {
			Write("Array_Clear(");
			Write(expr.Obj);
			Write(')');
		}
		// TODO
		else
			throw new ApplicationException(expr.Function.Name);
	}

	protected override void WriteInline(CiVar stmt)
	{
		Write(stmt.Type);
		Write(stmt.Name);
		if (!WriteInit(stmt.Type) && stmt.InitialValue != null) {
			Write(" = ");
			Write(stmt.InitialValue);
		}
	}

	protected override void WriteAssignSource(CiAssign assign)
	{
		if (assign.CastIntToByte) {
			Write("(byte) (");
			base.WriteAssignSource(assign);
			Write(')');
		}
		else
			base.WriteAssignSource(assign);
	}

	void Write(CiFunction func)
	{
		WriteLine();
		Write(func.Documentation);
		StartLine("static ");
		Write(func.ReturnType);
		Write(func.Name);
		Write("(");
		bool first = true;
		foreach (CiParam param in func.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Type);
			Write(param.Name);
		}
		Write(")");
		Write(func.Body);
	}

	public override void Write(CiProgram prog)
	{
		WriteLine("// Generated automatically with \"cito\". Do not edit.");
		Write("namespace ");
		Write(string.Join(".", prog.NamespaceElements.Where(e => e[0] >= 'A' && e[0] <= 'Z').ToArray()));
		OpenBlock();
		foreach (CiSymbol symbol in prog.Globals.List) {
			if (symbol is CiEnum)
				Write((CiEnum) symbol);
			else if (symbol is CiClass)
				Write((CiClass) symbol);
		}
		StartLine("public partial class ASAP");
		OpenBlock();
		foreach (CiConst konst in prog.ConstArrays) {
			StartLine("static readonly ");
			Write(konst.Type);
			Write(konst.GlobalName);
			Write(" = ");
			WriteConst(konst.Value);
			WriteLine(";");
		}
		foreach (CiSymbol symbol in prog.Globals.List) {
			if (symbol is CiConst && symbol.IsPublic)
				Write((CiConst) symbol);
			else if (symbol is CiFunction)
				Write((CiFunction) symbol);
		}
		CloseBlock();
		CloseBlock();
	}
}

}
