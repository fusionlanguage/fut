// GenJava.cs - Java code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
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

	protected override void WriteLiteral(object value)
	{
		base.WriteLiteral(value);
		if (value is long l && l != (int) l)
			Write('L');
	}

	protected override void WritePrintfWidth(CiInterpolatedPart part)
	{
		if (part.Precision >= 0 && part.Argument.Type is CiIntegerType) {
			if (part.WidthExpr != null)
				throw new NotImplementedException("Cannot format integer with both width and precision");
			Write('0');
			Write(part.Precision);
		}
		else
			base.WritePrintfWidth(part);
	}

	public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent)
	{
		if (expr.Parts.Length == 2
		 && expr.Parts[0].Prefix.Length == 0
		 && expr.Parts[1].Prefix.Length == 0
		 && expr.Parts[0].WidthExpr == null
		 && expr.Parts[0].Format == ' ') {
			CiExpr arg = expr.Parts[0].Argument;
			if (arg.Type == CiSystem.LongType)
				Write("Long");
			else if (arg.Type == CiSystem.DoubleType)
				Write("Double");
			else if (arg.Type == CiSystem.FloatType)
				Write("Float");
			else if (arg.Type is CiStringType) {
				arg.Accept(this, parent);
				return expr;
			}
			else
				Write("Integer");
			Write(".toString(");
			arg.Accept(this, CiPriority.Statement);
			Write(')');
		}
		else {
			Write("String.format(");
			WritePrintf(expr, false);
		}
		return expr;
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
		else if (symbol is CiMember) {
			if (symbol == CiSystem.CollectionCount)
				Write("size()");
			else
				WriteCamelCase(symbol.Name);
		}
		else
			Write(symbol.Name);
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
		if (integer == CiSystem.LongType)
			return TypeCode.Int64;
		if (promote || integer == CiSystem.IntType)
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

	void Write(TypeCode typeCode, bool klass)
	{
		switch (typeCode) {
		case TypeCode.Byte:
		case TypeCode.SByte: Write(klass ? "Byte" : "byte"); break;
		case TypeCode.Int16: Write(klass ? "Short" : "short"); break;
		case TypeCode.Int32: Write(klass ? "Integer" : "int"); break;
		case TypeCode.Int64: Write(klass ? "Long" : "long"); break;
		default: throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void Write(TypeCode typeCode)
	{
		Write(typeCode, false);
	}

	void Write(CiSortedDictionaryType dict)
	{
		Write("java.util.TreeMap<");
		Write(dict.KeyType, false, true);
		Write(", ");
		Write(dict.ValueType, false, true);
		Write('>');
	}

	void Write(CiType type, bool promote, bool klass)
	{
		switch (type) {
		case null:
			Write("void");
			break;
		case CiIntegerType integer:
			Write(GetTypeCode(integer, promote), klass);
			break;
		case CiStringType _:
			Write("String");
			break;
		case CiEnum enu:
			Write(enu == CiSystem.BoolType
				? klass ? "Boolean" : "boolean"
				: klass ? "Integer" : "int");
			break;
		case CiListType list:
			Write("java.util.ArrayList<");
			Write(list.ElementType, false, true);
			Write('>');
			break;
		case CiSortedDictionaryType dict:
			Write(dict);
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

	protected override void Write(CiType type, bool promote)
	{
		Write(type, promote, false);
	}

	protected override void WriteListStorageInit(CiListType list)
	{
		Write(" = new java.util.ArrayList<");
		Write(list.ElementType, false, true);
		Write(">()");
	}

	protected override void WriteSortedDictionaryStorageInit(CiSortedDictionaryType dict)
	{
		Write(" = new ");
		Write(dict);
		Write("()");
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
		else if (expr.Left is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)
			&& expr.Right is CiLiteral rightLiteral && rightLiteral.Value is long l && l >= 0 && l <= byte.MaxValue) {
			if (parent > CiPriority.Equality)
				Write('(');
			base.WriteIndexing(leftBinary, CiPriority.Equality); // omit "& 0xff"
			Write(not ? " != " : " == ");
			Write((sbyte) l);
			if (parent > CiPriority.Equality)
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
			if (parent > CiPriority.CondAnd && parent != CiPriority.And)
				Write('(');
			base.WriteIndexing(leftBinary, CiPriority.And);
			Write(" & ");
			Write(0xff & (long) rightLiteral.Value);
			if (parent > CiPriority.CondAnd && parent != CiPriority.And)
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

	void WriteConsoleWrite(CiExpr obj, CiMethod method, CiExpr[] args, bool newLine)
	{
		if (obj is CiSymbolReference symbol && symbol.Symbol == CiSystem.ConsoleError)
			Write("System.err");
		else
			Write("System.out");
		if (args.Length == 1 && args[0] is CiInterpolatedString interpolated) {
			Write(".format(");
			WritePrintf(interpolated, newLine);
		}
		else {
			Write(".print");
			if (newLine)
				Write("ln");
			WriteArgsInParentheses(method, args);
		}
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (method == CiSystem.StringSubstring) {
			obj.Accept(this, CiPriority.Primary);
			Write(".substring(");
			args[0].Accept(this, CiPriority.Statement);
			if (args.Length == 2) {
				Write(", ");
				args[0].Accept(this, CiPriority.Add); // TODO: side effect
				Write(" + ");
				args[1].Accept(this, CiPriority.Add);
			}
			Write(')');
		}
		else if (obj.Type is CiArrayType && !(obj.Type is CiListType) && method.Name == "CopyTo") {
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
			if (array.IsByteArray()) {
				Write("(byte) ");
				args[0].Accept(this, CiPriority.Primary);
			}
			else
				args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else if (method == CiSystem.ArraySort) {
			if (obj.Type is CiArrayStorageType) {
				Write("java.util.Arrays.sort(");
				obj.Accept(this, CiPriority.Statement);
				Write(')');
			}
			else {
				obj.Accept(this, CiPriority.Primary);
				Write(".sort(null)");
			}
		}
		else if (method == CiSystem.ListRemoveRange) {
			obj.Accept(this, CiPriority.Primary);
			Write(".subList(");
			args[0].Accept(this, CiPriority.Statement);
			Write(", ");
			args[0].Accept(this, CiPriority.Add); // TODO: side effect
			Write(" + ");
			args[1].Accept(this, CiPriority.Add);
			Write(").clear()");
		}
		else if (method == CiSystem.ConsoleWrite)
			WriteConsoleWrite(obj, method, args, false);
		else if (method == CiSystem.ConsoleWriteLine)
			WriteConsoleWrite(obj, method, args, true);
		else if (method == CiSystem.UTF8GetString) {
			Write("new String(");
			WriteArgs(method, args);
			Write(", java.nio.charset.StandardCharsets.UTF_8)");
		}
		else if (IsMathReference(obj) && method.Name == "Log2") {
			if (parent > CiPriority.Mul)
				Write('(');
			Write("Math.log(");
			args[0].Accept(this, CiPriority.Statement);
			Write(") * 1.4426950408889635");
			if (parent > CiPriority.Mul)
				Write(')');
		}
		else {
			obj.Accept(this, CiPriority.Primary);
			Write('.');
			if (obj.Type is CiListType && method.Name == "Insert")
				Write("add");
			else if (method == CiSystem.ListRemoveAt)
				Write("remove");
			else if (IsMathReference(obj) && method.Name == "Ceiling")
				Write("ceil");
			else if (IsMathReference(obj) && method.Name == "FusedMultiplyAdd")
				Write("fma");
			else
				WriteCamelCase(method.Name);
			WriteArgsInParentheses(method, args);
		}
	}

	static bool IsCollection(CiType type)
		=> type is CiListType || type is CiSortedDictionaryType;

	void WriteIndexingInternal(CiBinaryExpr expr, CiPriority parent)
	{
		if (IsCollection(expr.Left.Type)) {
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".get(");
			expr.Right.Accept(this, CiPriority.Statement);
			Write(')');
		}
		else
			base.WriteIndexing(expr, CiPriority.And);
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (parent != CiPriority.Assign && IsUnsignedByte(expr.Type)) {
			if (parent > CiPriority.And)
				Write('(');
			WriteIndexingInternal(expr, CiPriority.And);
			Write(" & 0xff");
			if (parent > CiPriority.And)
				Write(')');
		}
		else
			WriteIndexingInternal(expr, parent);
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

	protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left is CiBinaryExpr indexing
		 && indexing.Op == CiToken.LeftBracket
		 && IsCollection(indexing.Left.Type)) {
			 indexing.Left.Accept(this, CiPriority.Primary);
			 Write(indexing.Left.Type is CiSortedDictionaryType ? ".put(" : ".set(");
			 indexing.Right.Accept(this, CiPriority.Statement);
			 Write(", ");
			 WriteAssignRight(expr);
			 Write(')');
		}
		else
			base.WriteAssign(expr, parent);
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		return def.Type is CiArrayStorageType && def.Type.StorageType is CiClass;
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (!HasInitCode(def))
			return;
		CiArrayStorageType array = (CiArrayStorageType) def.Type;
		int nesting = 0;
		while (array.ElementType is CiArrayStorageType innerArray) {
			OpenLoop("int", nesting++, array.Length);
			array = innerArray;
		}
		OpenLoop("int", nesting++, array.Length);
		WriteArrayElement(def, nesting);
		Write(" = ");
		WriteNew((CiClass) array.ElementType, CiPriority.Statement);
		WriteLine(';');
		while (--nesting >= 0)
			CloseBlock();
	}

	public override void Visit(CiForeach statement)
	{
		Write("for (");
		WriteTypeAndName(statement.Element);
		Write(" : ");
		statement.Collection.Accept(this, CiPriority.Statement);
		Write(')');
		WriteChild(statement.Body);
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
			WriteLine(';');
		}
		WriteLine();
	}

	void CreateJavaFile(CiContainerType type, CiProgram program)
	{
		CreateJavaFile(type.Name);
		if (program != null)
			WriteTopLevelNatives(program);
		Write(type.Documentation);
		WritePublic(type);
	}

	void CloseJavaFile()
	{
		CloseBlock();
		CloseFile();
	}

	void Write(CiEnum enu)
	{
		CreateJavaFile(enu, null);
		Write("interface ");
		WriteLine(enu.Name);
		OpenBlock();
		int i = 0;
		foreach (CiConst konst in enu) {
			Write(konst.Documentation);
			Write("int ");
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			if (konst.Value != null)
				konst.Value.Accept(this, CiPriority.Statement);
			else
				Write(i);
			WriteLine(';');
			i++;
		}
		CloseJavaFile();
	}

	void WriteSignature(CiMethod method, int paramCount)
	{
		WriteLine();
		WriteDoc(method);
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
		Write('(');
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i >= paramCount)
				break;
			if (i > 0)
				Write(", ");
			WriteTypeAndName(param);
			i++;
		}
		Write(')');
		if (method.Throws)
			Write(" throws Exception");
	}

	void WriteOverloads(CiMethod method, int paramCount)
	{
		if (paramCount + 1 < method.Parameters.Count)
			WriteOverloads(method, paramCount + 1);
		WriteSignature(method, paramCount);
		WriteLine();
		OpenBlock();
		if (method.Type != null)
			Write("return ");
		WriteName(method);
		Write('(');
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i > 0)
				Write(", ");
			if (i >= paramCount) {
				param.Value.Accept(this, CiPriority.Statement);
				break;
			}
			Write(param.Name);
			i++;
		}
		WriteLine(");");
		CloseBlock();
	}

	void WriteConsts(IEnumerable<CiConst> konsts)
	{
		foreach (CiConst konst in konsts) {
			WriteLine();
			Write(konst.Documentation);
			Write(konst.Visibility);
			Write("static ");
			if (!(konst.Type is CiArrayStorageType)) // for array storage WriteTypeAndName will write "final"
				Write("final ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine(';');
		}
	}

	void Write(CiClass klass, CiProgram program)
	{
		CreateJavaFile(klass, program);
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
			WriteConstructorBody(klass);
			CloseBlock();
		}

		WriteConsts(klass.Consts);

		foreach (CiField field in klass.Fields) {
			Write(field.Visibility);
			WriteVar(field);
			WriteLine(';');
		}

		foreach (CiMethod method in klass.Methods) {
			WriteSignature(method, method.Parameters.Count);
			WriteBody(method);
			int i = 0;
			foreach (CiVar param in method.Parameters) {
				if (param.Value != null) {
					WriteOverloads(method, i);
					break;
				}
				i++;
			}
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
				Write(klass, program);
			else
				Write((CiEnum) type);
		}
		if (program.Resources.Count > 0)
			WriteResources();
	}
}

}
