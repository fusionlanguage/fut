// GenJava.cs - Java code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
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
using System.Linq;

namespace Foxoft.Ci
{

public class GenJava : GenTyped
{
	string OutputDirectory;

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
		switch (type) {
		case null:
			Write("void");
			break;
		case CiIntegerType integer:
			Write(GetTypeCode(integer, promote));
			break;
		case CiStringType _:
			Write("String");
			break;
		case CiEnum enu:
			Write(enu == CiSystem.BoolType ? "boolean" : "int");
			break;
		case CiArrayType array:
			if (promote && array is CiArrayStorageType)
				Write("final ");
			Write(array.ElementType, false);
			Write("[]");
			break;
		default:
			if (promote && type is CiClass)
				Write("final ");
			Write(type.Name);
			break;
		}
	}

	protected override void WriteLiteral(object value)
	{
		base.WriteLiteral(value);
		if (value is long l && l != (int) l)
			Write('L');
	}

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiConst konst) {
			if (konst.InMethod != null) {
				WriteUppercaseWithUnderscores(konst.InMethod.Name);
				Write('_');
			}
			WriteUppercaseWithUnderscores(symbol.Name);
		}
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

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if ((expr.Left.Type is CiStringType && expr.Right.Type != CiSystem.NullType)
		 || (expr.Right.Type is CiStringType && expr.Left.Type != CiSystem.NullType)) {
			 if (not)
				 Write('!');
			 expr.Left.Accept(this, CiPriority.Primary);
			 Write(".equals(");
			 expr.Right.Accept(this, CiPriority.Statement);
			 Write(')');
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	static bool IsUnsignedByte(CiType type)
	{
		return type is CiRangeType range && range.Min >= 0 && range.Max > sbyte.MaxValue && range.Max <= byte.MaxValue;
	}

	protected override void WriteCoercedLiteral(CiType type, object value)
	{
		if (IsUnsignedByte(type))
			Write((sbyte) (long) value);
		else
			WriteLiteral(value);
	}

	protected override void WriteAnd(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)
		 && expr.Right is CiLiteral rightLiteral) {
			if (parent > CiPriority.And)
				Write('(');
			base.WriteIndexing(leftBinary, CiPriority.And);
			Write(" & ");
			Write(0xff & (long) rightLiteral.Value);
			if (parent > CiPriority.And)
				Write(')');
		}
		else
			base.WriteAnd(expr, parent);
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
		expr.Right.Accept(this, CiPriority.Statement);
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (method == CiSystem.StringSubstring) {
			obj.Accept(this, CiPriority.Primary);
			Write(".substring(");
			args[0].Accept(this, CiPriority.Statement);
			Write(", ");
			args[0].Accept(this, CiPriority.Add); // TODO: side effect
			Write(" + ");
			args[1].Accept(this, CiPriority.Add);
			Write(')');
		}
		else if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			Write("System.arraycopy(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			WriteArgs(method, args);
			Write(')');
		}
		else if (obj.Type is CiArrayStorageType array && method.Name == "Fill") {
			Write("java.util.Arrays.fill(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			if (array.ElementType is CiRangeType range
				&& ((range.Min >= 0 && range.Max <= byte.MaxValue)
					|| (range.Min >= sbyte.MinValue && range.Max <= sbyte.MaxValue))) {
				Write("(byte) ");
				args[0].Accept(this, CiPriority.Primary);
			}
			else
				args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else if (obj.Type == CiSystem.UTF8EncodingClass && method.Name == "GetString") {
			Write("new String(");
			WriteArgs(method, args);
			Write(", java.nio.charset.StandardCharsets.UTF_8)");
		}
		else {
			obj.Accept(this, CiPriority.Primary);
			Write('.');
			if (IsMathReference(obj) && method.Name == "Ceiling")
				Write("ceil");
			else
				WriteCamelCase(method.Name);
			WriteArgsInParentheses(method, args);
		}
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (parent != CiPriority.Assign && IsUnsignedByte(expr.Type)) {
			if (parent > CiPriority.And)
				Write('(');
			base.WriteIndexing(expr, CiPriority.And);
			Write(" & 0xff");
			if (parent > CiPriority.And)
				Write(')');
		}
		else
			base.WriteIndexing(expr, parent);
	}

	protected override void WriteAssignRight(CiBinaryExpr expr)
	{
		if ((!expr.Left.IsIndexing || !IsUnsignedByte(expr.Left.Type))
			&& expr.Right is CiBinaryExpr rightBinary && rightBinary.IsAssign && IsUnsignedByte(expr.Right.Type)) {
			Write('(');
			base.WriteAssignRight(expr);
			Write(") & 0xff");
		}
		else
			base.WriteAssignRight(expr);
	}

	protected override void WriteInnerArray(CiNamedValue def, int nesting, CiArrayStorageType array)
	{
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		if (!(def.Type is CiArrayStorageType array))
			return false;
		while (array.ElementType is CiArrayStorageType element)
			array = element;
		return array.ElementType is CiClass;
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
			Write("static ");
			if (!(konst.Type is CiArrayStorageType)) // for array storage WriteTypeAndName will write "final"
				Write("final ");
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
		OpenClass(klass, "", " extends ");
		
		if (NeedsConstructor(klass)) {
			if (klass.Constructor != null)
				Write(klass.Constructor.Visibility);
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			foreach (CiField field in klass.Fields)
				WriteInitCode(field);
			if (klass.Constructor != null)
				Write(((CiBlock) klass.Constructor.Body).Statements);
			CloseBlock();
		}

		WriteConsts(klass.Consts);

		foreach (CiField field in klass.Fields) {
			Write(field.Visibility);
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
				Write("final @Override ");
				break;
			default:
				throw new NotImplementedException(method.CallType.ToString());
			}
			WriteTypeAndName(method);
			WriteParameters(method);
			if (method.Throws)
				Write(" throws Exception");
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
			if (type is CiClass klass)
				Write(klass);
			else
				Write((CiEnum) type);
		}
		if (program.Resources.Count > 0)
			WriteResources();
	}
}

}
