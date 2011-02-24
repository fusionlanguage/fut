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
using System.IO;

namespace Foxoft.Ci
{

public class GenJava : SourceGenerator, ICiSymbolVisitor
{
	string Namespace;

	public GenJava(string namespace_)
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
			case '\n': WriteLine(); Write(" * "); break;
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
			WriteLine(" * <ul>");
			foreach (CiDocPara item in list.Items) {
				Write(" * <li>");
				Write(item);
				WriteLine("</li>");
			}
			WriteLine(" * </ul>");
			Write(" * ");
			return;
		}
		Write((CiDocPara) block);
	}

	void WriteDontClose(CiCodeDoc doc)
	{
		WriteLine("/**");
		Write(" * ");
		Write(doc.Summary);
		if (doc.Details.Length > 0) {
			WriteLine();
			Write(" * ");
			foreach (CiDocBlock block in doc.Details)
				Write(block);
		}
		WriteLine();
	}

	void Write(CiCodeDoc doc)
	{
		if (doc != null) {
			WriteDontClose(doc);
			WriteLine(" */");
		}
	}

	void CreateJavaFile(string type, CiSymbol symbol)
	{
		CreateFile(Path.Combine(this.OutputPath, symbol.Name + ".java"));
		if (this.Namespace != null) {
			Write("package ");
			Write(this.Namespace);
			WriteLine(";");
		}
		WriteLine();
		Write(symbol.Documentation);
		if (symbol.IsPublic)
			Write("public ");
		Write(type);
		Write(' ');
		WriteLine(symbol.Name);
		OpenBlock();
	}

	void CloseJavaFile()
	{
		CloseBlock();
		CloseFile();
	}

	void ICiSymbolVisitor.Visit(CiEnum enu)
	{
		CreateJavaFile("interface", enu);
		for (int i = 0; i < enu.Values.Length; i++) {
			CiEnumValue value = enu.Values[i];
			Write(value.Documentation);
			Write("int ");
			Write(value.Name);
			Write(" = ");
			Write(i);
			WriteLine(";");
		}
		CloseJavaFile();
	}

	void WriteBaseType(CiType type)
	{
		if (type is CiStringType)
			Write("String");
		else if (type == CiBoolType.Value)
			Write("boolean");
		else if (type is CiEnum)
			Write("int");
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

	void ICiSymbolVisitor.Visit(CiField field)
	{
		Write(field.Documentation);
		if (field.IsPublic)
			Write("public ");
		if (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)
			Write("final ");
		Write(field.Type);
		Write(field.Name);
		WriteInit(field.Type);
		WriteLine(";");
	}

	void ICiSymbolVisitor.Visit(CiProperty prop)
	{
		throw new NotImplementedException();
	}

	protected override void WriteConst(object value)
	{
		if (value is byte)
			Write((sbyte) (byte) value);
		else
			base.WriteConst(value);
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
			if (c.ResultType == CiIntType.Value && c.Inner.Type == CiByteType.Value)
				return 8;
		}
		return base.GetPriority(expr);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiIntType.SByteProperty) {
			Write("(byte) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiIntType.LowByteProperty) {
			Write("(byte) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiStringType.LengthProperty) {
			WriteChild(expr, expr.Obj);
			Write(".length()");
		}
		// TODO
		else
			throw new ApplicationException(expr.Property.Name);
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiIntType.MulDivMethod) {
			Write("(int) ((long) (");
			Write(expr.Obj);
			Write(") * (");
			Write(expr.Arguments[0]);
			Write(") / (");
			Write(expr.Arguments[1]);
			Write("))");
		}
		else if (expr.Method == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write(".charAt(");
			Write(expr.Arguments[0]);
			Write(')');
		}
		else if (expr.Method == CiStringType.SubstringMethod) {
			Write("String_Substring(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiArrayType.CopyToMethod) {
			Write("System.arraycopy(");
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
			Write("new String(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiArrayStorageType.ClearMethod) {
			Write("Array_Clear(");
			Write(expr.Obj);
			Write(')');
		}
		else
			base.Write(expr);
	}

	protected override void Write(CiBinaryResourceExpr expr)
	{
		Write("BinaryResource_Get(");
		WriteConst(expr.Resource.Name);
		Write(')');
	}

	protected override void Write(CiCoercion expr)
	{
		if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
			Write("(byte) ");
			WriteChild(expr, (CiExpr) expr.Inner); // TODO: Assign
		}
		else if (expr.ResultType == CiIntType.Value && expr.Inner.Type == CiByteType.Value) {
			Write('(');
			Write((CiExpr) expr.Inner); // TODO: Assign
			Write(") & 0xff");
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
		Write("throw new Exception(");
		Write(stmt.Message);
		WriteLine(");");
	}

	void ICiSymbolVisitor.Visit(CiMethod method)
	{
		WriteLine();
		if (method.Documentation != null) {
			WriteDontClose(method.Documentation);
			foreach (CiParam param in method.Params) {
				if (param.Documentation != null) {
					Write(" * @param ");
					Write(param.Name);
					Write(' ');
					Write(param.Documentation.Summary);
					WriteLine();
				}
			}
			WriteLine(" */");
		}
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
		if (method.Throws)
			WriteLine(") throws Exception");
		else
			WriteLine(")");
		Write(method.Body);
	}

	void ICiSymbolVisitor.Visit(CiClass klass)
	{
		CreateJavaFile("final class", klass);
		foreach (CiSymbol member in klass.Members)
			member.Accept(this);
		foreach (CiConst konst in klass.ConstArrays) {
			Write("static final ");
			Write(konst.Type);
			Write(konst.GlobalName);
			Write(" = ");
			WriteConst(konst.Value);
			WriteLine(";");
		}
		CloseJavaFile();
	}

	public override void Write(CiProgram prog)
	{
		foreach (CiSymbol symbol in prog.Globals)
			symbol.Accept(this);
		/*
		CreateJavaFile("final class", new CiClass { IsPublic = true, Name = "ASAP" });
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiConst && symbol.IsPublic)
				Write((CiConst) symbol);
			else if (symbol is CiFunction)
				((CiFunction) symbol).Accept(this);
		}
		CloseJavaFile();
		*/
	}
}

}
