// GenJava.cs - Java code generator
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

public class GenJava : SourceGenerator
{
	public GenJava(string outputPath)
	{
	}

	void WriteDoc(string text)
	{
		foreach (char c in text) {
			switch (c) {
			case '&': Write("&amp;"); break;
			case '<': Write("&lt;"); break;
			case '>': Write("&gt;"); break;
			case '\n': WriteLine(); StartLine(" * "); break;
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
				Write("<code>");
				WriteDoc(code.Text);
				Write("</code>");
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
			StartLine(" * <ul>");
			WriteLine();
			foreach (CiDocPara item in list.Items) {
				StartLine(" * <li>");
				Write(item);
				WriteLine("</li>");
			}
			StartLine(" * </ul>");
			WriteLine();
			StartLine(" * ");
			return;
		}
		Write((CiDocPara) block);
	}

	void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		StartLine("/**");
		WriteLine();
		StartLine(" * ");
		Write(doc.Summary);
		if (doc.Details.Length > 0) {
			WriteLine();
			StartLine(" * ");
			foreach (CiDocBlock block in doc.Details)
				Write(block);
		}
		WriteLine();
		StartLine(" */");
		WriteLine();
	}

	void WriteBaseType(CiType type)
	{
		if (type is CiStringType)
			Write("String");
		else if (type is CiClassType) {
			CiClassType classType = (CiClassType) type;
			Write(classType.Class.Name);
		}
		else if (type == CiBoolType.Value)
			Write("boolean");
		else if (type == CiByteType.Value)
			Write("byte");
		else if (type == CiIntType.Value)
			Write("int");
		else
			throw new ApplicationException();
	}

	void Write(CiType type)
	{
		if (type is CiClassStorageType || type is CiArrayStorageType)
			Write("final ");
		WriteBaseType(type.BaseType);
		for (int i = 0; i < type.ArrayLevel; i++)
			Write("[]");
		Write(' ');
	}

	void WriteInit(CiType type)
	{
		CiClassStorageType classType = type as CiClassStorageType;
		if (classType != null) {
			Write(" = new {0}()", classType.Class.Name);
			return;
		}
		CiArrayStorageType arrayType = type as CiArrayStorageType;
		if (arrayType != null) {
			Write(" = new ");
			WriteBaseType(arrayType.BaseType);
			WriteInitializer(arrayType);
		}
	}

	void Write(CiField field)
	{
		Write(field.Documentation);
		StartLine(field.IsPublic ? "public " : "");
		Write(field.Type);
		Write(field.Name);
		WriteInit(field.Type);
		WriteLine(";");
	}

	void Write(CiClass clazz)
	{
		WriteLine();
		Write(clazz.Documentation);
		StartLine(clazz.IsPublic ? "public " : "");
		Write("final class ");
		Write(clazz.Name);
		OpenBlock();
		foreach (CiField field in clazz.Fields)
			Write(field);
		CloseBlock();
	}

	protected override void Write(CiVar stmt)
	{
		StartLine("");
		Write(stmt.Type);
		Write(stmt.Name);
		if (stmt.InitialValue != null) {
			Write(" = ");
			Write(stmt.InitialValue);
		}
		WriteLine(";");
	}

	public override void Write(CiProgram prog)
	{
		WriteLine("// Generated automatically with \"cito\". Do not edit.");
		Write("package ");
		Write(string.Join(".", prog.NamespaceElements));
		WriteLine(";");
		foreach (CiSymbol symbol in prog.Globals.List)
			if (symbol is CiClass)
				Write((CiClass) symbol);
	}
}

}
