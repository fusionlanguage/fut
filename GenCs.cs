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

namespace Foxoft.Ci
{

public class GenCs : SourceGenerator, ICiSymbolVisitor
{
	string Namespace;

	public GenCs(string namespace_)
	{
		this.Namespace = namespace_;
	}

	void WriteDoc(string text)
	{
		foreach (char c in text) {
			switch (c) {
			case '&': Write("&amp;"); break;
			case '<': Write("&lt;"); break;
			case '>': Write("&gt;"); break;
			case '\n': WriteLine(); Write("/// "); break;
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
			WriteLine("/// <list type=\"bullet\">");
			foreach (CiDocPara item in list.Items) {
				Write("/// <item>");
				Write(item);
				WriteLine("</item>");
			}
			Write("/// </list>");
			WriteLine();
			Write("/// ");
			return;
		}
		Write((CiDocPara) block);
	}

	void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		Write("/// <summary>");
		Write(doc.Summary);
		WriteLine("</summary>");
		if (doc.Details.Length > 0) {
			Write("/// <remarks>");
			foreach (CiDocBlock block in doc.Details)
				Write(block);
			WriteLine("</remarks>");
		}
	}

	void ICiSymbolVisitor.Visit(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		Write(enu.IsPublic ? "public " : "internal ");
		Write("enum ");
		WriteLine(enu.Name);
		OpenBlock();
		bool first = true;
		foreach (CiEnumValue value in enu.Values) {
			if (first)
				first = false;
			else
				WriteLine(",");
			Write(value.Documentation);
			Write(value.Name);
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
			Write(" = new ");
			Write(classType.Class.Name);
			Write("()");
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

	void ICiSymbolVisitor.Visit(CiField field)
	{
		Write(field.Documentation);
		Write(field.IsPublic ? "public " : "internal ");
		if (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)
			Write("readonly ");
		Write(field.Type);
		Write(field.Name);
		WriteInit(field.Type);
		WriteLine(";");
	}

	void ICiSymbolVisitor.Visit(CiProperty prop)
	{
		throw new NotImplementedException();
	}

	void ICiSymbolVisitor.Visit(CiConst def)
	{
		if (!def.IsPublic)
			return;
		Write(def.Documentation);
		Write("public const ");
		Write(def.Type);
		Write(def.Name);
		Write(" = ");
		WriteConst(def.Value);
		WriteLine(";");
	}

	protected override int GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiIntType.SByteProperty || prop == CiIntType.LowByteProperty)
				return 2;
		}
		else if (expr is CiCoercion) {
			CiCoercion c = (CiCoercion) expr;
			if (c.ResultType == CiByteType.Value && c.Inner.Type == CiIntType.Value)
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
		if (expr.Method == CiIntType.MulDivMethod) {
			Write("(int) ((long) ");
			WriteChild(2, expr.Obj);
			Write(" * ");
			WriteChild(3, expr.Arguments[0]);
			Write(" / ");
			WriteRightChild(3, expr.Arguments[1]);
			Write(")");
		}
		else if (expr.Method == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(']');
		}
		else if (expr.Method == CiStringType.SubstringMethod) {
			Write(expr.Obj);
			Write(".Substring(");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiArrayType.CopyToMethod) {
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
		else if (expr.Method == CiArrayType.ToStringMethod) {
			Write("System.Text.Encoding.UTF8.GetString(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiArrayStorageType.ClearMethod) {
			Write("System.Array.Clear(");
			Write(expr.Obj);
			Write(", 0, ");
			Write(((CiArrayStorageType) expr.Obj.Type).Length);
			Write(')');
		}
		else
			base.Write(expr);
	}

	protected override void Write(CiCoercion expr)
	{
		if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
			Write("(byte) ");
			WriteChild(expr, (CiExpr) expr.Inner); // TODO: Assign
		}
		else
			base.Write(expr);
	}

	public override void Visit(CiVar stmt)
	{
		Write(stmt.Type);
		Write(stmt.Name);
		if (!WriteInit(stmt.Type) && stmt.InitialValue != null) {
			Write(" = ");
			Write(stmt.InitialValue);
		}
	}

	public override void Visit(CiThrow stmt)
	{
		Write("throw new System.Exception(");
		Write(stmt.Message);
		WriteLine(");");
	}

	void ICiSymbolVisitor.Visit(CiMethod method)
	{
		WriteLine();
		Write(method.Documentation);
		foreach (CiParam param in method.Params) {
			if (param.Documentation != null) {
				Write("/// <param name=\"");
				Write(param.Name);
				Write("\">");
				Write(param.Documentation.Summary);
				WriteLine("</param>");
			}
		}

		Write(method.IsPublic ? "public " : "internal ");
		if (method.IsStatic)
			Write("static ");
		Write(method.ReturnType);
		Write(method.Name);
		Write("(");
		bool first = true;
		foreach (CiParam param in method.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Type);
			Write(param.Name);
		}
		WriteLine(")");
		Write(method.Body);
	}

	void ICiSymbolVisitor.Visit(CiClass klass)
	{
		WriteLine();
		Write(klass.Documentation);
		Write(klass.IsPublic ? "public " : "internal ");
		Write("class ");
		WriteLine(klass.Name);
		OpenBlock();
		foreach (CiSymbol member in klass.Members)
			member.Accept(this);
		foreach (CiConst konst in klass.ConstArrays) {
			Write("static readonly ");
			Write(konst.Type);
			Write(konst.GlobalName);
			Write(" = ");
			WriteConst(konst.Value);
			WriteLine(";");
		}
		foreach (CiBinaryResource resource in klass.BinaryResources) {
			Write("static readonly byte[] ");
			WriteName(resource);
			Write(" = ");
			WriteConst(resource.Content);
			WriteLine(";");
		}
		CloseBlock();
	}

	public override void Write(CiProgram prog)
	{
		CreateFile(this.OutputPath);
		if (this.Namespace != null) {
			Write("namespace ");
			WriteLine(this.Namespace);
			OpenBlock();
		}
		foreach (CiSymbol symbol in prog.Globals)
			symbol.Accept(this);
		if (this.Namespace != null)
			CloseBlock();
		CloseFile();
	}
}

}
