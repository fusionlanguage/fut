// GenJava.cs - Java code generator
//
// Copyright (C) 2011-2016  Piotr Fusik
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
using System.IO;

namespace Foxoft.Ci
{

public class GenJava : GenTyped
{
	string Namespace;
	string OutputDirectory;

	public GenJava(string namespace_)
	{
		this.Namespace = namespace_;
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Internal:
			break;
		case CiVisibility.Protected:
			Write("protected ");
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	protected override TypeCode GetTypeCode(CiIntegerType integer, bool promote)
	{
		if (integer.IsLong)
			return TypeCode.Int64;
		if (promote || integer is CiIntType)
			return TypeCode.Int32;
		CiRangeType range = (CiRangeType) integer;
		if (range.Min < 0) {
			if (range.Min < short.MinValue || range.Max > short.MaxValue)
				return TypeCode.Int32;
			if (range.Min < sbyte.MinValue || range.Max > sbyte.MaxValue)
				return TypeCode.Int16;
			return TypeCode.SByte;
		}
		if (range.Max > short.MaxValue)
			return TypeCode.Int32;
		if (range.Max > byte.MaxValue)
			return TypeCode.Int16;
		if (range.Min == range.Max && range.Max > sbyte.MaxValue) // CiLiteral
			return TypeCode.Byte;
		return TypeCode.SByte; // store unsigned bytes in Java signed bytes
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.Byte:
		case TypeCode.SByte: Write("byte"); break;
		case TypeCode.Int16: Write("short"); break;
		case TypeCode.Int32: Write("int"); break;
		case TypeCode.Int64: Write("long"); break;
		default: throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void Write(CiType type, bool promote)
	{
		if (type == null) {
			Write("void");
			return;
		}

		CiIntegerType integer = type as CiIntegerType;
		if (integer != null) {
			Write(GetTypeCode(integer, promote));
			return;
		}

		if (type == CiSystem.BoolType) {
			Write("boolean");
			return;
		}
		if (type is CiStringType) {
			Write("String");
			return;
		}
		if (type is CiEnum) {
			Write("int");
			return;
		}

		CiArrayType array = type as CiArrayType;
		if (array != null) {
			Write(array.ElementType, false);
			Write("[]");
			return;
		}

		Write(type.Name);
	}

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiConst)
			WriteUppercaseWithUnderscores(symbol.Name);
		else if (symbol is CiMember)
			WriteCamelCase(symbol.Name);
		else
			Write(symbol.Name);
	}

	protected override void WriteResource(string name, int length)
	{
		Write("CiResource.getByteArray(");
		WriteLiteral(name);
		Write(", ");
		Write(length);
		Write(')');
	}

	protected override void WritePromoted(CiExpr expr, CiPriority parent)
	{
		CiBinaryExpr binary = expr as CiBinaryExpr;
		if (binary != null && binary.Op == CiToken.LeftBracket) {
			CiRangeType range = expr.Type as CiRangeType;
			if (range != null && range.Min >= 0 && range.Max > sbyte.MaxValue && range.Max <= byte.MaxValue) {
				if (parent > CiPriority.And)
					Write('(');
				base.WritePromoted(expr, CiPriority.And);
				Write(" & 0xff");
				if (parent > CiPriority.And)
					Write(')');
				return;
			}
		}
		base.WritePromoted(expr, parent);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		expr.Accept(this, CiPriority.Primary);
		Write(".length()");
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		expr.Left.Accept(this, CiPriority.Primary);
		Write(".charAt(");
		WritePromoted(expr.Right, CiPriority.Statement);
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, string method, CiExpr[] args)
	{
		if (obj.Type is CiArrayType && method == "CopyTo") {
			Write("System.arraycopy(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			WritePromoted(args);
			Write(')');
		}
		else if (obj.Type is CiArrayStorageType && method == "Fill") {
			Write("java.util.Arrays.fill(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			WriteCoercedLiteral(args[0].Type, args[0], CiPriority.Statement);
			Write(')');
		}
		else {
			obj.Accept(this, CiPriority.Primary);
			Write('.');
			WriteCamelCase(method);
			Write('(');
			WritePromoted(args);
			Write(')');
		}
	}

	public override void Visit(CiThrow statement)
	{
		Write("throw new Exception(");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine(");");
	}

	void CreateJavaFile(string className)
	{
		CreateFile(Path.Combine(this.OutputDirectory, className + ".java"));
		if (this.Namespace != null) {
			Write("package ");
			Write(this.Namespace);
			WriteLine(";");
		}
		WriteLine();
	}

	void CreateJavaFile(CiContainerType type)
	{
		CreateJavaFile(type.Name);
		WritePublic(type);
	}

	void CloseJavaFile()
	{
		CloseBlock();
		CloseFile();
	}

	void Write(CiEnum enu)
	{
		CreateJavaFile(enu);
		Write("interface ");
		WriteLine(enu.Name);
		OpenBlock();
		int i = 0;
		foreach (CiConst konst in enu) {
			Write("int ");
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			if (konst.Value != null)
				konst.Value.Accept(this, CiPriority.Statement);
			else
				Write(i);
			WriteLine(";");
			i++;
		}
		CloseJavaFile();
	}

	void WriteConsts(IEnumerable<CiConst> konsts)
	{
		foreach (CiConst konst in konsts) {
			Write(konst.Visibility);
			Write("static final ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine(";");
		}
	}

	void Write(CiClass klass)
	{
		CreateJavaFile(klass);
		switch (klass.CallType) {
		case CiCallType.Normal:
			break;
		case CiCallType.Abstract:
			Write("abstract ");
			break;
		case CiCallType.Static:
		case CiCallType.Sealed:
			Write("final ");
			break;
		default:
			throw new NotImplementedException(klass.CallType.ToString());
		}
		OpenClass(klass, " extends ");
		
		if (klass.Constructor != null) {
			Write(klass.Constructor.Visibility);
			Write(klass.Name);
			WriteLine("()");
			Visit((CiBlock) klass.Constructor.Body);
		}
		else if (klass.IsPublic && klass.CallType != CiCallType.Static) {
			if (klass.CallType != CiCallType.Sealed)
				Write("protected ");
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			CloseBlock();
		}

		WriteConsts(klass.Consts);

		foreach (CiField field in klass.Fields) {
			Write(field.Visibility);
			if (field.Type is CiClass || field.Type is CiArrayStorageType)
				Write("final ");
			WriteVar(field);
			WriteLine(";");
		}

		foreach (CiMethod method in klass.Methods) {
			WriteLine();
			Write(method.Visibility);
			switch (method.CallType) {
			case CiCallType.Static:
				Write("static ");
				break;
			case CiCallType.Virtual:
				break;
			case CiCallType.Abstract:
				Write("abstract ");
				break;
			case CiCallType.Override:
				Write("@Override ");
				break;
			case CiCallType.Normal:
				if (method.Visibility != CiVisibility.Private)
					Write("final ");
				break;
			case CiCallType.Sealed:
				Write("final ");
				break;
			default:
				throw new NotImplementedException(method.CallType.ToString());
			}
			WriteTypeAndName(method);
			Write('(');
			bool first = true;
			foreach (CiVar param in method.Parameters) {
				if (!first)
					Write(", ");
				first = false;
				WriteTypeAndName(param);
			}
			Write(')');
			WriteBody(method);
		}

		WriteConsts(klass.ConstArrays);

		CloseJavaFile();
	}

	void WriteResources()
	{
		CreateJavaFile("CiResource");
		Write("class CiResource");
		WriteLine();
		OpenBlock();
		WriteLine("static byte[] getByteArray(String name, int length)");
		OpenBlock();
		Write("java.io.DataInputStream dis = new java.io.DataInputStream(");
		WriteLine("CiResource.class.getResourceAsStream(name));");
		WriteLine("byte[] result = new byte[length];");
		Write("try ");
		OpenBlock();
		Write("try ");
		OpenBlock();
		WriteLine("dis.readFully(result);");
		CloseBlock();
		Write("finally ");
		OpenBlock();
		WriteLine("dis.close();");
		CloseBlock();
		CloseBlock();
		Write("catch (java.io.IOException e) ");
		OpenBlock();
		WriteLine("throw new RuntimeException();");
		CloseBlock();
		WriteLine("return result;");
		CloseBlock();
		CloseJavaFile();
	}

	public override void Write(CiProgram program)
	{
		if (Directory.Exists(this.OutputFile))
			this.OutputDirectory = this.OutputFile;
		else
			this.OutputDirectory = Path.GetDirectoryName(this.OutputFile);
		foreach (CiContainerType type in program) {
			CiClass klass = type as CiClass;
			if (klass != null)
				Write(klass);
			else
				Write((CiEnum) type);
		}
		if (program.Resources.Count > 0)
			WriteResources();
	}
}

}
