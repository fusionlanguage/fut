// GenAs.cs - ActionScript code generator
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
	bool UsesClearMethod;

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
		string dir = Path.GetDirectoryName(this.OutputFile);
		CreateFile(Path.Combine(dir, symbol.Name + ".as"));
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
		Write("class ");
		WriteLine(enu.Name);
		OpenBlock();
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
		else if (type == CiByteType.Value || type is CiEnum)
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
		if (type is CiClassStorageType || type is CiArrayStorageType) {
			Write(" = ");
			WriteNew(type);
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

	void WriteClearArray(CiExpr expr)
	{
		CiArrayStorageType array = (CiArrayStorageType) expr.Type;
		if (array.ElementType == CiBoolType.Value) {
			Write("clearArray(");
			Write(expr);
			Write(", false)");
			this.UsesClearMethod = true;
		}
		else if (array.ElementType == CiByteType.Value) {
			Write("clearByteArray(");
			Write(expr);
			Write(", ");
			Write(array.Length);
			Write(')');
			this.UsesClearBytesMethod = true;
		}
		else if (array.ElementType == CiIntType.Value) {
			Write("clearArray(");
			Write(expr);
			Write(", 0)");
			this.UsesClearMethod = true;
		}
		else
			throw new ArgumentException(array.ElementType.Name);
	}

	protected override void WriteName(CiMethod method)
	{
		WriteCamelCase(method.Name);
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiLibrary.MulDivMethod) {
			Write("int(");
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
		else if (expr.Method == CiLibrary.ArrayCopyToMethod) {
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
		else if (expr.Method == CiLibrary.ArrayToStringMethod) {
			Write("bytesToString(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
			this.UsesBytesToStringMethod = true;
		}
		else if (expr.Method == CiLibrary.ArrayStorageClearMethod)
			WriteClearArray(expr.Obj);
		else
			base.Write(expr);
	}

	protected override void Write(CiBinaryExpr expr)
	{
		if (expr.Op == CiToken.Slash) {
			Write("int(");
			WriteChild(CiPriority.Multiplicative, expr.Left);
			Write(" / ");
			WriteNonAssocChild(CiPriority.Multiplicative, expr.Right);
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
			if (arrayType.ElementType == CiByteType.Value)
				Write("new ByteArray()");
			else {
				Write("new Array(");
				if (arrayType.LengthExpr != null)
					Write(arrayType.LengthExpr);
				else
					Write(arrayType.Length);
				Write(')');
			}
		}
	}

	public override void Visit(CiVar stmt)
	{
		Write("var ");
		Write(stmt.Name);
		Write(stmt.Type);
		WriteInit(stmt.Type);
		if (stmt.InitialValue != null) {
			if (stmt.Type is CiArrayStorageType) {
				WriteLine(";");
				WriteClearArray(new CiVarAccess { Var = stmt });
			}
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

	void ICiSymbolVisitor.Visit(CiMethod method)
	{
		WriteLine();
		WriteDoc(method);
		WriteVisibility(method);
		switch (method.CallType) {
		case CiCallType.Static: Write("static "); break;
		case CiCallType.Normal: if (method.Visibility != CiVisibility.Private) Write("final "); break;
		case CiCallType.Override: Write("override "); break;
		default: break;
		}
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
		if (method.CallType == CiCallType.Abstract)
			WriteLine("throw \"Abstract method called\";");
		else {
			ICiStatement[] statements = method.Body.Statements;
			Write(statements);
			if (method.Signature.ReturnType != CiType.Void && statements.Length > 0) {
				CiFor lastLoop = statements[statements.Length - 1] as CiFor;
				if (lastLoop != null && lastLoop.Cond == null)
					WriteLine("throw \"Unreachable\";");
			}
		}
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
		if (this.UsesClearBytesMethod) {
			WriteLine("private static function clearByteArray(a : ByteArray, length : int) : void");
			OpenBlock();
			WriteLine("for (var i : int = 0; i < length; i++)");
			WriteLine("\ta[i] = 0;");
			CloseBlock();
		}
		if (this.UsesClearMethod) {
			WriteLine("private static function clearArray(a : Array, value : *) : void");
			OpenBlock();
			WriteLine("for (var i : int = 0; i < a.length; i++)");
			WriteLine("\ta[i] = value;");
			CloseBlock();
		}
	}

	void ICiSymbolVisitor.Visit(CiClass klass)
	{
		CreateAsFile(klass);
		OpenClass(false, klass, " extends ");
		this.UsesSubstringMethod = false;
		this.UsesCopyArrayMethod = false;
		this.UsesBytesToStringMethod = false;
		this.UsesClearBytesMethod = false;
		this.UsesClearMethod = false;
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
		foreach (CiSymbol symbol in prog.Globals)
			symbol.Accept(this);
	}
}

}
