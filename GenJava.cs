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

	void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
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
		WriteLine(" */");
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		Write(enu.IsPublic ? "public " : "internal ");
		Write("interface ");
		WriteLine(enu.Name);
		OpenBlock();
		for (int i = 0; i < enu.Values.Length; i++) {
			CiEnumValue value = enu.Values[i];
			Write(value.Documentation);
			Write("int ");
			Write(value.Name);
			Write(" = ");
			Write(i);
			WriteLine(";");
		}
		WriteLine();
		CloseBlock();
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

	void Write(CiField field)
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

	void Write(CiClass clazz)
	{
		WriteLine();
		Write(clazz.Documentation);
		if (clazz.IsPublic)
			Write("public ");
		Write("final class ");
		WriteLine(clazz.Name);
		OpenBlock();
		foreach (CiField field in clazz.Fields)
			Write(field);
		CloseBlock();
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
			Write(".charAt(");
			Write(expr.Arguments[0]);
			Write(')');
		}
		else if (expr.Function == CiStringType.SubstringMethod) {
			Write("String_Substring(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Function == CiArrayType.CopyToMethod) {
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
		else if (expr.Function == CiArrayType.ToStringMethod) {
			Write("new String(");
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

	protected override void Write(CiBinaryResourceExpr expr)
	{
		Write("BinaryResource_Get(");
		WriteConst(expr.Resource.Name);
		Write(')');
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
		Write("private static ");
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
		WriteLine(")");
		Write(func.Body);
	}

	public override void Write(CiProgram prog)
	{
		WriteLine("// Generated automatically with \"cito\". Do not edit.");
		Write("package ");
		Write(string.Join(".", prog.NamespaceElements));
		WriteLine(";");
		foreach (CiSymbol symbol in prog.Globals.List) {
			if (symbol is CiEnum)
				Write((CiEnum) symbol);
			else if (symbol is CiClass)
				Write((CiClass) symbol);
		}
		WriteLine("public final class ASAP");
		OpenBlock();
		foreach (CiConst konst in prog.ConstArrays) {
			Write("static final ");
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
	}
}

}
