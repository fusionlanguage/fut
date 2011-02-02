// GenJs.cs - JavaScript code generator
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

public class GenJs : SourceGenerator
{
	public GenJs(string outputPath)
	{
	}

	void Write(CiCodeDoc doc)
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
			Write(value.Name);
			Write(" : ");
			Write(i);
		}
		WriteLine();
		CloseBlock();
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
			Write(" = new Array(");
			Write(arrayType.Length);
			Write(')');
			return true;
		}
		return false;
	}

	void Write(CiField field)
	{
		Write(field.Documentation);
		Write("this.");
		Write(field.Name);
		if (field.Type is CiBoolType)
			Write(" = false");
		else if (field.Type is CiByteType || field.Type is CiIntType)
			Write(" = 0");
		else if (!WriteInit(field.Type))
			Write(" = null");
		WriteLine(";");
	}

	void Write(CiClass clazz)
	{
		WriteLine();
		Write(clazz.Documentation);
		Write("function ");
		Write(clazz.Name);
		WriteLine("()");
		OpenBlock();
		foreach (CiField field in clazz.Fields)
			Write(field);
		CloseBlock();
	}

	protected override void WriteConst(object value)
	{
		if (value is Array) {
			Write("[ ");
			WriteContent((Array) value);
			Write(" ]");
		}
		else
			base.WriteConst(value);
	}

	protected override int GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiIntType.SByteProperty)
				return 2;
			if (prop == CiIntType.LowByteProperty)
				return 8;
		}
		else if (expr is CiBinaryExpr) {
			if (((CiBinaryExpr) expr).Op == CiToken.Slash)
				return 1;
		}
		return base.GetPriority(expr);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiIntType.SByteProperty) {
			Write("Byte_SByte(");
			WriteChild(expr, expr.Obj);
			Write(')');
		}
		else if (expr.Property == CiIntType.LowByteProperty) {
			WriteChild(expr, expr.Obj);
			Write(" & 0xff");
		}
		else if (expr.Property == CiStringType.LengthProperty) {
			WriteChild(expr, expr.Obj);
			Write(".length");
		}
		// TODO
		else
			throw new ApplicationException(expr.Property.Name);
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Function == CiIntType.MulDivMethod) {
			Write("Math.floor((");
			Write(expr.Obj);
			Write(") * (");
			Write(expr.Arguments[0]);
			Write(") / (");
			Write(expr.Arguments[1]);
			Write("))");
		}
		else if (expr.Function == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write(".charCodeAt(");
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
			Write("ByteArray_Copy(");
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
			Write("ByteArray_ToString(");
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

	protected override void Write(CiBinaryExpr expr)
	{
		if (expr.Op == CiToken.Slash) {
			Write("Math.floor(");
			WriteChild(expr, expr.Left);
			Write(" / ");
			WriteRightChild(expr, expr.Right);
			Write(')');
		}
		else
			base.Write(expr);
	}

	protected override void WriteInline(CiVar stmt)
	{
		Write("var ");
		Write(stmt.Name);
		if (!WriteInit(stmt.Type) && stmt.InitialValue != null) {
			Write(" = ");
			Write(stmt.InitialValue);
		}
	}

	void Write(CiFunction func)
	{
		WriteLine();
		Write(func.Documentation);
		Write("function ");
		Write(func.Name);
		Write("(");
		bool first = true;
		foreach (CiParam param in func.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Name);
		}
		WriteLine(")");
		Write(func.Body);
	}

	public override void Write(CiProgram prog)
	{
		WriteLine("// Generated automatically with \"cito\". Do not edit.");
		foreach (CiSymbol symbol in prog.Globals.List) {
			if (symbol is CiEnum)
				Write((CiEnum) symbol);
			else if (symbol is CiClass)
				Write((CiClass) symbol);
		}
		foreach (CiConst konst in prog.ConstArrays) {
			Write("var ");
			Write(konst.GlobalName);
			Write(" = ");
			WriteConst(konst.Value);
			WriteLine(";");
		}
		foreach (CiBinaryResource resource in prog.BinaryResources) {
			Write("var ");
			WriteName(resource);
			Write(" = ");
			WriteConst(resource.Content);
			WriteLine(";");
		}
		foreach (CiSymbol symbol in prog.Globals.List) {
			if (symbol is CiConst && symbol.IsPublic)
				Write((CiConst) symbol);
			else if (symbol is CiFunction)
				Write((CiFunction) symbol);
		}
	}
}

}
