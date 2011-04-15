// GenAs.cs - ActionScript code generator
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

public class GenAs : SourceGenerator, ICiSymbolVisitor
{
	string Namespace;
	bool UsesSubstringMethod;
	bool UsesCopyArrayMethod;
	bool UsesBytesToStringMethod;
	bool UsesClearBytesMethod;
	bool UsesClearIntsMethod;

	public GenAs(string namespace_)
	{
		this.Namespace = namespace_;
	}

	void WriteVisibility(CiSymbol symbol)
	{
		switch (symbol.Visibility) {
		case CiVisibility.Dead:
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Internal:
			if (symbol.Documentation == null)
				WriteLine("/** @private */");
			Write("internal ");
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	void CreateAsFile(CiSymbol symbol)
	{
		CreateFile(Path.Combine(this.OutputPath, symbol.Name + ".as"));
		if (this.Namespace != null) {
			Write("package ");
			WriteLine(this.Namespace);
		}
		else
			WriteLine("package");
		OpenBlock();
		WriteLine("import flash.utils.ByteArray;");
		WriteLine();
		Write(symbol.Documentation);
		WriteVisibility(symbol);
		Write("class ");
		WriteLine(symbol.Name);
		OpenBlock();
	}

	void CloseAsFile()
	{
		CloseBlock(); // class
		CloseBlock(); // package
		CloseFile();
	}

	void ICiSymbolVisitor.Visit(CiEnum enu)
	{
		CreateAsFile(enu);
		for (int i = 0; i < enu.Values.Length; i++) {
			CiEnumValue value = enu.Values[i];
			Write(value.Documentation);
			Write("public static const ");
			WriteUppercaseWithUnderscores(value.Name);
			Write(" : int = ");
			Write(i);
			WriteLine(";");
		}
		CloseAsFile();
	}

	void Write(CiType type)
	{
		Write(" : ");
		if (type is CiStringType)
			Write("String");
		else if (type == CiBoolType.Value)
			Write("Boolean");
		else if (type is CiEnum)
			Write("int");
		else if (type is CiArrayType)
			Write(((CiArrayType) type).ElementType == CiByteType.Value ? "ByteArray" : "Array");
		else if (type is CiDelegate)
			Write("Function");
		else
			Write(type.Name);
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
			if (arrayType.ElementType == CiByteType.Value) {
				Write(" = new ByteArray()");
				return true;
			}
			Write(" = new Array(");
			Write(arrayType.Length);
			Write(')');
			return true;
		}
		return false;
	}

	void ICiSymbolVisitor.Visit(CiField field)
	{
		Write(field.Documentation);
		WriteVisibility(field);
		if (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)
			Write("const ");
		else
			Write("var ");
		WriteCamelCase(field.Name);
		Write(field.Type);
		WriteInit(field.Type);
		WriteLine(";");
	}

	void ICiSymbolVisitor.Visit(CiConst konst)
	{
		if (konst.Visibility != CiVisibility.Public)
			return;
		Write(konst.Documentation);
		Write("public static const ");
		WriteUppercaseWithUnderscores(konst.Name);
		Write(konst.Type);
		Write(" = ");
		WriteConst(konst.Value);
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
		WriteUppercaseWithUnderscores(konst.GlobalName ?? konst.Name);
	}

	protected override int GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiIntType.SByteProperty)
				return 4;
			if (prop == CiIntType.LowByteProperty)
				return 8;
		}
		else if (expr is CiBinaryExpr) {
			if (((CiBinaryExpr) expr).Op == CiToken.Slash)
				return 1;
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
		if (expr.Property == CiIntType.SByteProperty) {
			Write('(');
			WriteChild(9, expr.Obj);
			Write(" ^ 128) - 128");
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

	protected override void WriteName(CiMethod method)
	{
		WriteCamelCase(method.Name);
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiIntType.MulDivMethod) {
			Write("int(");
			WriteChild(3, expr.Obj);
			Write(" * ");
			WriteChild(3, expr.Arguments[0]);
			Write(" / ");
			WriteNonAssocChild(3, expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write(".charCodeAt(");
			Write(expr.Arguments[0]);
			Write(')');
		}
		else if (expr.Method == CiStringType.SubstringMethod) {
			if (expr.Arguments[0].HasSideEffect) {
				Write("substring(");
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
		else if (expr.Method == CiArrayType.CopyToMethod) {
			Write("copyArray(");
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
		else if (expr.Method == CiArrayType.ToStringMethod) {
			Write("bytesToString(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
			this.UsesBytesToStringMethod = true;
		}
		else if (expr.Method == CiArrayStorageType.ClearMethod) {
			CiType type = ((CiArrayStorageType) expr.Obj.Type).ElementType;
			if (type == CiByteType.Value) {
				Write("clearByteArray(");
				this.UsesClearBytesMethod = true;
			}
			else if (type == CiIntType.Value) {
				Write("clearIntArray(");
				this.UsesClearIntsMethod = true;
			}
			else
				throw new ApplicationException();
			Write(expr.Obj);
			Write(')');
		}
		else
			base.Write(expr);
	}

	protected override void Write(CiBinaryExpr expr)
	{
		if (expr.Op == CiToken.Slash) {
			Write("int(");
			WriteChild(3, expr.Left);
			Write(" / ");
			WriteNonAssocChild(3, expr.Right);
			Write(')');
		}
		else
			base.Write(expr);
	}

	protected override void Write(CiBinaryResourceExpr expr)
	{
		Write("new ");
		WriteName(expr.Resource);
	}

	public override void Visit(CiVar stmt)
	{
		Write("var ");
		Write(stmt.Name);
		Write(stmt.Type);
		if (!WriteInit(stmt.Type) && stmt.InitialValue != null) {
			Write(" = ");
			Write(stmt.InitialValue);
		}
	}

	public override void Visit(CiThrow stmt)
	{
		Write("throw ");
		Write(stmt.Message);
		WriteLine(";");
	}

	void ICiSymbolVisitor.Visit(CiMethod method)
	{
		WriteLine();
		WriteDoc(method);
		WriteVisibility(method);
		if (method.IsStatic)
			Write("static ");
		Write("function ");
		WriteCamelCase(method.Name);
		Write('(');
		bool first = true;
		foreach (CiParam param in method.Signature.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Name);
			Write(param.Type);
		}
		Write(")");
		Write(method.Signature.ReturnType);
		WriteLine();
		OpenBlock();
		ICiStatement[] statements = method.Body.Statements;
		Write(statements);
		if (method.Signature.ReturnType != CiType.Void && statements.Length > 0) {
			CiFor lastLoop = statements[statements.Length - 1] as CiFor;
			if (lastLoop != null && lastLoop.Cond == null)
				WriteLine("throw \"Unreachable\";");
		}
		CloseBlock();
	}

	void WriteClearMethod(string signature)
	{
		Write("private static function ");
		Write(signature);
		WriteLine(" : void");
		OpenBlock();
		WriteLine("for (var i : int = 0; i < a.length; i++)");
		WriteLine("\ta[i] = 0;");
		CloseBlock();
	}

	void WriteBuiltins()
	{
		if (this.UsesSubstringMethod) {
			WriteLine("private static function substring(s : String, offset : int, length : int) : String");
			OpenBlock();
			WriteLine("return s.substring(offset, offset + length);");
			CloseBlock();
		}
		if (this.UsesCopyArrayMethod) {
			WriteLine("private static function copyArray(sa : ByteArray, soffset : int, da : ByteArray, doffset : int, length : int) : void");
			OpenBlock();
			WriteLine("for (var i : int = 0; i < length; i++)");
			WriteLine("\tda[doffset + i] = sa[soffset + i];");
			CloseBlock();
		}
		if (this.UsesBytesToStringMethod) {
			WriteLine("private static function bytesToString(a : ByteArray, offset : int, length : int) : String");
			OpenBlock();
			WriteLine("var s : String = \"\";");
			WriteLine("for (var i : int = 0; i < length; i++)");
			WriteLine("\ts += String.fromCharCode(a[offset + i]);");
			WriteLine("return s;");
			CloseBlock();
		}
		if (this.UsesClearBytesMethod)
			WriteClearMethod("clearByteArray(a : ByteArray)");
		if (this.UsesClearIntsMethod)
			WriteClearMethod("clearIntArray(a : Array)");
	}

	void ICiSymbolVisitor.Visit(CiClass klass)
	{
		CreateAsFile(klass);
		this.UsesSubstringMethod = false;
		this.UsesCopyArrayMethod = false;
		this.UsesBytesToStringMethod = false;
		this.UsesClearBytesMethod = false;
		this.UsesClearIntsMethod = false;
		if (klass.Constructor != null) {
			Write("public function ");
			Write(klass.Name);
			WriteLine("()");
			Write(klass.Constructor.Body);
		}
		foreach (CiSymbol member in klass.Members)
			member.Accept(this);
		foreach (CiConst konst in klass.ConstArrays) {
			Write("private static const ");
			WriteUppercaseWithUnderscores(konst.GlobalName);
			byte[] bytes = konst.Value as byte[];
			if (bytes != null) {
				WriteLine(" : ByteArray = new ByteArray();");
				OpenBlock();
				foreach (byte b in bytes) {
					WriteUppercaseWithUnderscores(konst.GlobalName);
					Write(".writeByte(");
					Write(b);
					WriteLine(");");
				}
				CloseBlock();
			}
			else {
				Write(" : Array = ");
				WriteConst(konst.Value);
				WriteLine(";");
			}
		}
		foreach (CiBinaryResource resource in klass.BinaryResources) {
			Write("[Embed(source=\"/");
			Write(resource.Name);
			WriteLine("\", mimeType=\"application/octet-stream\")]");
			Write("private static const ");
			WriteName(resource);
			WriteLine(": Class;");
		}
		WriteBuiltins();
		CloseAsFile();
	}

	void ICiSymbolVisitor.Visit(CiDelegate del)
	{
	}

	public override void Write(CiProgram prog)
	{
		foreach (CiSymbol symbol in prog.Globals) {
			symbol.Accept(this);
		}
	}
}

}
