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
	bool UsesSubstringMethod;
	bool UsesClearBytesMethod;
	bool UsesClearIntsMethod;

	public GenJava(string namespace_)
	{
		this.Namespace = namespace_;
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Dead:
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Internal:
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
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
		Write(symbol.Visibility);
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
			WriteUppercaseWithUnderscores(value.Name);
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
		Write(field.Visibility);
		if (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)
			Write("final ");
		Write(field.Type);
		WriteCamelCase(field.Name);
		WriteInit(field.Type);
		WriteLine(";");
	}

	void ICiSymbolVisitor.Visit(CiConst konst)
	{
		if (konst.Visibility != CiVisibility.Public)
			return;
		Write(konst.Documentation);
		Write("public static final ");
		Write(konst.Type);
		WriteUppercaseWithUnderscores(konst.Name);
		Write(" = ");
		WriteConst(konst.Value);
		WriteLine(";");
	}

	protected override void WriteConst(object value)
	{
		if (value is byte)
			Write((sbyte) (byte) value);
		else if (value is CiEnumValue) {
			CiEnumValue ev = (CiEnumValue) value;
			Write(ev.Type.Name);
			Write('.');
			WriteUppercaseWithUnderscores(ev.Name);
		}
		else
			base.WriteConst(value);
	}

	protected override void WriteName(CiConst konst)
	{
		WriteUppercaseWithUnderscores(konst.GlobalName);
	}

	protected override int GetPriority(CiExpr expr)
	{
		CiPropertyAccess pa = expr as CiPropertyAccess;
		if (pa != null) {
			if (pa.Property == CiIntType.SByteProperty)
				return GetPriority(pa.Obj);
			if (pa.Property == CiIntType.LowByteProperty)
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

	protected override void Write(CiFieldAccess expr)
	{
		WriteChild(expr, expr.Obj);
		Write('.');
		WriteCamelCase(expr.Field.Name);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiIntType.SByteProperty)
			Write(expr.Obj);
		else if (expr.Property == CiIntType.LowByteProperty) {
			Write("(byte) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiStringType.LengthProperty) {
			WriteChild(expr, expr.Obj);
			Write(".length()");
		}
		else
			throw new ApplicationException(expr.Property.Name);
	}

	protected override void WriteName(CiMethod method)
	{
		WriteCamelCase(method.Name);
	}

	protected override void WriteDelegateCall(CiExpr expr)
	{
		Write(expr);
		Write(".run");
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiIntType.MulDivMethod) {
			Write("(int) ((long) ");
			WriteChild(2, expr.Obj);
			Write(" * ");
			WriteChild(3, expr.Arguments[0]);
			Write(" / ");
			WriteNonAssocChild(3, expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write(".charAt(");
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
			Write("clear(");
			Write(expr.Obj);
			Write(')');
			CiType type = ((CiArrayStorageType) expr.Obj.Type).ElementType;
			if (type == CiByteType.Value)
				this.UsesClearBytesMethod = true;
			else if (type == CiIntType.Value)
				this.UsesClearIntsMethod = true;
			else
				throw new ApplicationException();
		}
		else
			base.Write(expr);
	}

	protected override void Write(CiBinaryResourceExpr expr)
	{
		Write("getBinaryResource(");
		WriteConst(expr.Resource.Name);
		Write(", ");
		Write(expr.Resource.Content.Length);
		Write(')');
	}

	protected override void Write(CiCoercion expr)
	{
		if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
			Write("(byte) ");
			WriteChild(expr, (CiExpr) expr.Inner); // TODO: Assign
		}
		else if (expr.ResultType == CiIntType.Value && expr.Inner.Type == CiByteType.Value) {
			WriteChild(8, (CiExpr) expr.Inner); // TODO: Assign
			Write(" & 0xff");
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

	protected override void EndCase(CiCase kase)
	{
		if (kase.Fallthrough)
			WriteLine("//$FALL-THROUGH$");
	}

	public override void Visit(CiThrow stmt)
	{
		Write("throw new Exception(");
		Write(stmt.Message);
		WriteLine(");");
	}

	void WriteSignature(CiDelegate del, string name)
	{
		Write(del.ReturnType);
		WriteCamelCase(name);
		Write('(');
		bool first = true;
		foreach (CiParam param in del.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Type);
			Write(param.Name);
		}
		Write(')');
	}

	void ICiSymbolVisitor.Visit(CiMethod method)
	{
		WriteLine();
		WriteDoc(method);
		Write(method.Visibility);
		if (method.IsStatic)
			Write("static ");
		WriteSignature(method.Signature, method.Name);
		if (method.Throws)
			WriteLine(" throws Exception");
		else
			WriteLine();
		Write(method.Body);
	}

	void WriteClearMethod(string elementType)
	{
		Write("private static void clear(");
		Write(elementType);
		WriteLine("[] array)");
		OpenBlock();
		WriteLine("for (int i = 0; i < array.length; i++)");
		this.Indent++;
		WriteLine("array[i] = 0;");
		this.Indent--;
		CloseBlock();
	}

	void WriteSubstringMethod()
	{
		WriteLine("private static String substring(String s, int offset, int length)");
		OpenBlock();
		WriteLine("return s.substring(offset, offset + length);");
		CloseBlock();
	}

	void WriteGetBinaryResource(CiClass klass)
	{
		WriteLine();
		WriteLine("private static byte[] getBinaryResource(String name, int length)");
		OpenBlock();
		Write("java.io.DataInputStream dis = new java.io.DataInputStream(");
		Write(klass.Name);
		WriteLine(".class.getResourceAsStream(name));");
		WriteLine("byte[] result = new byte[length];");
		Write("try "); OpenBlock();
		Write("try "); OpenBlock();
		WriteLine("dis.readFully(result);");
		CloseBlock();
		Write("finally "); OpenBlock();
		WriteLine("dis.close();");
		CloseBlock();
		CloseBlock();
		Write("catch (java.io.IOException e) "); OpenBlock();
		WriteLine("throw new RuntimeException();");
		CloseBlock();
		WriteLine("return result;");
		CloseBlock();
	}

	void ICiSymbolVisitor.Visit(CiClass klass)
	{
		CreateJavaFile("final class", klass);
		this.UsesSubstringMethod = false;
		this.UsesClearBytesMethod = false;
		this.UsesClearIntsMethod = false;
		if (klass.Constructor != null) {
			Write("public ");
			Write(klass.Name);
			WriteLine("()");
			Write(klass.Constructor.Body);
		}
		foreach (CiSymbol member in klass.Members)
			member.Accept(this);
		if (this.UsesSubstringMethod)
			WriteSubstringMethod();
		if (this.UsesClearBytesMethod)
			WriteClearMethod("byte");
		if (this.UsesClearIntsMethod)
			WriteClearMethod("int");
		if (klass.BinaryResources.Length > 0)
			WriteGetBinaryResource(klass);
		foreach (CiConst konst in klass.ConstArrays) {
			Write("private static final ");
			Write(konst.Type);
			WriteUppercaseWithUnderscores(konst.GlobalName);
			Write(" = ");
			WriteConst(konst.Value);
			WriteLine(";");
		}
		CloseJavaFile();
	}

	void ICiSymbolVisitor.Visit(CiDelegate del)
	{
		// TODO: doc
		CreateJavaFile("interface", del);
		WriteSignature(del, "run");
		WriteLine(";");
		CloseJavaFile();
	}

	public override void Write(CiProgram prog)
	{
		foreach (CiSymbol symbol in prog.Globals)
			symbol.Accept(this);
	}
}

}
