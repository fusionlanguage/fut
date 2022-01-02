// GenJava.cs - Java code generator
//
// Copyright (C) 2011-2022  Piotr Fusik
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
		if (expr.Suffix.Length == 0
		 && expr.Parts.Length == 1
		 && expr.Parts[0].Prefix.Length == 0
		 && expr.Parts[0].WidthExpr == null
		 && expr.Parts[0].Format == ' ') {
			CiExpr arg = expr.Parts[0].Argument;
			if (arg.Type == CiSystem.LongType)
				Write("Long");
			else if (arg.Type == CiSystem.DoubleType || arg.Type == CiSystem.FloatIntType)
				Write("Double");
			else if (arg.Type == CiSystem.FloatType)
				Write("Float");
			else if (arg.Type is CiStringType) {
				arg.Accept(this, parent);
				return expr;
			}
			else
				Write("Integer");
			WriteCall(".toString", arg);
		}
		else {
			Write("String.format(");
			WritePrintf(expr, false);
		}
		return expr;
	}

	void WriteCamelCaseNotKeyword(string name)
	{
		WriteCamelCase(name);
		switch (name) {
		case "Abstract":
		case "Assert":
		case "Boolean":
		case "Break":
		case "Byte":
		case "Case":
		case "Catch":
		case "Char":
		case "Class":
		case "Const":
		case "Continue":
		case "Default":
		case "Do":
		case "Double":
		case "Else":
		case "Enum":
		case "Extends":
		case "False":
		case "Final":
		case "Finally":
		case "Float":
		case "For":
		case "Foreach":
		case "Goto":
		case "If":
		case "Implements":
		case "Import":
		case "Instanceof":
		case "Int":
		case "Interface":
		case "Long":
		case "Native":
		case "New":
		case "Null":
		case "Package":
		case "Private":
		case "Protected":
		case "Public":
		case "Return":
		case "Short":
		case "Static":
		case "Strictfp":
		case "String":
		case "Super":
		case "Switch":
		case "Synchronized":
		case "Transient":
		case "Throw":
		case "Throws":
		case "True":
		case "Try":
		case "Void":
		case "Volatile":
		case "While":
		case "Yield":
		case "boolean":
		case "catch":
		case "char":
		case "extends":
		case "final":
		case "finally":
		case "goto":
		case "implements":
		case "import":
		case "instanceof":
		case "interface":
		case "package":
		case "private":
		case "strictfp":
		case "super":
		case "synchronized":
		case "transient":
		case "try":
		case "volatile":
		case "yield":
			Write('_');
			break;
		default:
			break;
		}
	}

	protected override void WriteName(CiSymbol symbol)
	{
		switch (symbol) {
		case CiContainerType _:
			Write(symbol.Name);
			break;
		case CiConst konst:
			if (konst.InMethod != null) {
				WriteUppercaseWithUnderscores(konst.InMethod.Name);
				Write('_');
			}
			WriteUppercaseWithUnderscores(symbol.Name);
			break;
		case CiVar _:
			if (symbol.Parent is CiForeach forEach && forEach.Count == 2) {
				CiVar element = forEach.Element;
				WriteCamelCaseNotKeyword(element.Name);
				Write(symbol == element ? ".getKey()" : ".getValue()");
			}
			else
				WriteCamelCaseNotKeyword(symbol.Name);
			break;
		case CiMember _:
			if (symbol == CiSystem.CollectionCount)
				Write("size()");
			else
				WriteCamelCaseNotKeyword(symbol.Name);
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
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

	protected override TypeCode GetIntegerTypeCode(CiIntegerType integer, bool promote)
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

	void Write(TypeCode typeCode, bool needClass)
	{
		switch (typeCode) {
		case TypeCode.Byte:
		case TypeCode.SByte: Write(needClass ? "Byte" : "byte"); break;
		case TypeCode.Int16: Write(needClass ? "Short" : "short"); break;
		case TypeCode.Int32: Write(needClass ? "Integer" : "int"); break;
		case TypeCode.Int64: Write(needClass ? "Long" : "long"); break;
		default: throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void Write(TypeCode typeCode)
	{
		Write(typeCode, false);
	}

	void WriteCollectionType(string name, CiType elementType)
	{
		Include("java.util." + name);
		Write(name);
		Write('<');
		Write(elementType, false, true);
		Write('>');
	}

	void Write(string name, CiDictionaryType dict)
	{
		Include("java.util." + name);
		Write(name);
		Write('<');
		Write(dict.KeyType, false, true);
		Write(", ");
		Write(dict.ValueType, false, true);
		Write('>');
	}

	protected override void WriteClassName(CiClass klass)
	{
		if (klass == CiSystem.RegexClass) {
			Include("java.util.regex.Pattern");
			Write("Pattern");
		}
		else if (klass == CiSystem.MatchClass) {
			Include("java.util.regex.Matcher");
			Write("Matcher");
		}
		else if (klass == CiSystem.LockClass)
			Write("Object");
		else
			Write(klass.Name);
	}

	void Write(CiType type, bool promote, bool needClass)
	{
		switch (type) {
		case CiIntegerType integer:
			Write(GetIntegerTypeCode(integer, promote), needClass);
			break;
		case CiStringType _:
			Write("String");
			break;
		case CiEnum enu:
			Write(enu == CiSystem.BoolType
				? needClass ? "Boolean" : "boolean"
				: needClass ? "Integer" : "int");
			break;
		case CiListType list:
			WriteCollectionType("ArrayList", list.ElementType);
			break;
		case CiHashSetType set:
			WriteCollectionType("HashSet", set.ElementType);
			break;
		case CiSortedDictionaryType dict:
			Write("TreeMap", dict);
			break;
		case CiDictionaryType dict:
			Write("HashMap", dict);
			break;
		case CiArrayType array:
			Write(array.ElementType, false);
			Write("[]");
			break;
		case CiClass klass:
			WriteClassName(klass);
			break;
		case CiClassPtrType classPtr:
			WriteClassName(classPtr.Class);
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void Write(CiType type, bool promote)
	{
		Write(type, promote, false);
	}

	protected override void WriteNewStorage(CiType type)
	{
		switch (type) {
		case CiListType list:
			Write("new ");
			WriteCollectionType("ArrayList", list.ElementType);
			Write("()");
			break;
		case CiHashSetType set:
			Write("new ");
			WriteCollectionType("HashSet", set.ElementType);
			Write("()");
			break;
		case CiDictionaryType dict:
			Write("new ");
			Write(dict is CiSortedDictionaryType ? "TreeMap" : "HashMap", dict);
			Write("()");
			break;
		default:
			base.WriteNewStorage(type);
			break;
		}
	}

	protected override void WriteResource(string name, int length)
	{
		Write("CiResource.getByteArray(");
		WriteLiteral(name);
		Write(", ");
		Write(length);
		Write(')');
	}

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		if ((expr.Op == CiToken.Increment || expr.Op == CiToken.Decrement)
		 && expr.Inner is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)) {
			if (parent > CiPriority.And)
				Write('(');
			Write(expr.Op == CiToken.Increment ? "++" : "--");
			WriteIndexingInternal(leftBinary);
			if (parent != CiPriority.Statement)
				Write(" & 0xff");
			if (parent > CiPriority.And)
				Write(')');
			return expr;
		}
		else
			return base.Visit(expr, parent);
	}

	public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent)
	{
		if ((expr.Op == CiToken.Increment || expr.Op == CiToken.Decrement)
		 && expr.Inner is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)) {
			if (parent > CiPriority.And)
				Write('(');
			WriteIndexingInternal(leftBinary);
			Write(expr.Op == CiToken.Increment ? "++" : "--");
			if (parent != CiPriority.Statement)
				Write(" & 0xff");
			if (parent > CiPriority.And)
				Write(')');
			return expr;
		}
		else
			return base.Visit(expr, parent);
	}

	void WriteIndexingInternal(CiBinaryExpr expr)
	{
		if (IsCollection(expr.Left.Type))
			WriteCall(expr.Left, "get", expr.Right);
		else
			base.WriteIndexing(expr, CiPriority.And /* don't care */);
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if ((expr.Left.Type is CiStringType && expr.Right.Type != CiSystem.NullType)
		 || (expr.Right.Type is CiStringType && expr.Left.Type != CiSystem.NullType)) {
			 if (not)
				 Write('!');
			 WriteCall(expr.Left, "equals", expr.Right);
		}
		else if (expr.Left is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)
			&& expr.Right is CiLiteral rightLiteral && rightLiteral.Value is long l && l >= 0 && l <= byte.MaxValue) {
			if (parent > CiPriority.Equality)
				Write('(');
			WriteIndexingInternal(leftBinary); // omit "& 0xff"
			Write(GetEqOp(not));
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
		else {
			WriteLiteral(value);
			if (type == CiSystem.FloatType)
				Write('f');
		}
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
		WriteCall(expr.Left, "charAt", expr.Right);
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left != null && expr.Left.IsReferenceTo(CiSystem.MathClass)) {
			Write("Float.");
			Write(expr.Symbol == CiSystem.MathNaN ? "NaN"
				: expr.Symbol == CiSystem.MathNegativeInfinity ? "NEGATIVE_INFINITY"
				: expr.Symbol == CiSystem.MathPositiveInfinity ? "POSITIVE_INFINITY"
				: throw new NotImplementedException(expr.ToString()));
		}
		else if (WriteJavaMatchProperty(expr, parent))
			return expr;
		else
			return base.Visit(expr, parent);
		return expr;
	}

	void WriteArrayBinarySearchFill(CiExpr obj, string method, CiExpr[] args)
	{
		Include("java.util.Arrays");
		Write("Arrays.");
		Write(method);
		Write('(');
		obj.Accept(this, CiPriority.Argument);
		Write(", ");
		if (args.Length == 3) {
			WriteStartEnd(args[1], args[2]);
			Write(", ");
		}
		WriteNotPromoted(((CiArrayType) obj.Type).ElementType, args[0]);
		Write(')');
	}

	void WriteConsoleWrite(CiExpr obj, CiMethod method, CiExpr[] args, bool newLine)
	{
		Write(obj.IsReferenceTo(CiSystem.ConsoleError) ? "System.err" : "System.out");
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

	void WriteRegex(CiExpr[] args, int argIndex)
	{
		CiExpr pattern = args[argIndex];
		if (pattern.Type.IsClass(CiSystem.RegexClass))
			pattern.Accept(this, CiPriority.Primary);
		else {
			Include("java.util.regex.Pattern");
			Write("Pattern.compile(");
			pattern.Accept(this, CiPriority.Argument);
			WriteRegexOptions(args, ", ", " | ", "", "Pattern.CASE_INSENSITIVE", "Pattern.MULTILINE", "Pattern.DOTALL");
			Write(')');
		}
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj == null) {
			WriteName(method);
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.StringSubstring) {
			obj.Accept(this, CiPriority.Primary);
			Write(".substring(");
			args[0].Accept(this, CiPriority.Argument);
			if (args.Length == 2) {
				Write(", ");
				WriteAdd(args[0], args[1]); // TODO: side effect
			}
			Write(')');
		}
		else if (obj.Type is CiArrayType && method.Name == "BinarySearch")
			WriteArrayBinarySearchFill(obj, "binarySearch", args);
		else if (obj.Type is CiArrayType && !(obj.Type is CiListType) && method.Name == "CopyTo") {
			Write("System.arraycopy(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteArgs(method, args);
			Write(')');
		}
		else if (obj.Type is CiArrayType && method.Name == "Fill")
			WriteArrayBinarySearchFill(obj, "fill", args);
		else if (method == CiSystem.CollectionSortAll) {
			if (obj.Type is CiArrayStorageType) {
				Include("java.util.Arrays");
				WriteCall("Arrays.sort", obj);
			}
			else {
				obj.Accept(this, CiPriority.Primary);
				Write(".sort(null)");
			}
		}
		else if (method == CiSystem.CollectionSortPart) {
			if (obj.Type is CiListType) {
				obj.Accept(this, CiPriority.Primary);
				Write(".subList(");
				WriteStartEnd(args[0], args[1]);
				Write(").sort(null)");
			}
			else {
				Include("java.util.Arrays");
				Write("Arrays.sort(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteStartEnd(args[0], args[1]);
				Write(')');
			}
		}
		else if (WriteListAddInsert(obj, method, args, "add", "add", ", ")) {
			// done
		}
		else if (method == CiSystem.ListRemoveRange) {
			obj.Accept(this, CiPriority.Primary);
			Write(".subList(");
			WriteStartEnd(args[0], args[1]);
			Write(").clear()");
		}
		else if (obj.Type is CiDictionaryType dict && method.Name == "Add") {
			obj.Accept(this, CiPriority.Primary);
			Write(".put(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteNewStorage(dict.ValueType);
			Write(')');
		}
		else if (method == CiSystem.ConsoleWrite)
			WriteConsoleWrite(obj, method, args, false);
		else if (method == CiSystem.ConsoleWriteLine)
			WriteConsoleWrite(obj, method, args, true);
		else if (method == CiSystem.UTF8GetByteCount) {
			Include("java.nio.charset.StandardCharsets");
			args[0].Accept(this, CiPriority.Primary);
			Write(".getBytes(StandardCharsets.UTF_8).length"); // FIXME: quick&dirty!
		}
		else if (method == CiSystem.UTF8GetBytes) {
			Include("java.nio.ByteBuffer");
			Include("java.nio.CharBuffer");
			Include("java.nio.charset.StandardCharsets");
			Write("StandardCharsets.UTF_8.newEncoder().encode(CharBuffer.wrap(");
			args[0].Accept(this, CiPriority.Argument);
			Write("), ByteBuffer.wrap(");
			args[1].Accept(this, CiPriority.Argument);
			Write(", ");
			args[2].Accept(this, CiPriority.Argument);
			Write(", ");
			args[1].Accept(this, CiPriority.Primary); // FIXME: side effect
			Write(".length");
			if (!args[2].IsLiteralZero) {
				Write(" - ");
				args[2].Accept(this, CiPriority.Mul); // FIXME: side effect
			}
			Write("), true)");
		}
		else if (method == CiSystem.UTF8GetString) {
			Include("java.nio.charset.StandardCharsets");
			Write("new String(");
			WriteArgs(method, args);
			Write(", StandardCharsets.UTF_8)");
		}
		else if (method == CiSystem.EnvironmentGetEnvironmentVariable)
			WriteCall("System.getenv", args[0]);
		else if (method == CiSystem.RegexCompile)
			WriteRegex(args, 0);
		else if (method == CiSystem.RegexEscape) {
			Include("java.util.regex.Pattern");
			WriteCall("Pattern.quote", args[0]);
		}
		else if (method == CiSystem.RegexIsMatchStr) {
			WriteRegex(args, 1);
			WriteCall(".matcher", args[0]);
			Write(".find()");
		}
		else if (method == CiSystem.RegexIsMatchRegex) {
			WriteCall(obj, "matcher", args[0]);
			Write(".find()");
		}
		else if (method == CiSystem.MatchFindStr || method == CiSystem.MatchFindRegex) {
			Write('(');
			obj.Accept(this, CiPriority.Assign);
			Write(" = ");
			WriteRegex(args, 1);
			WriteCall(".matcher", args[0]);
			Write(").find()");
		}
		else if (method == CiSystem.MatchGetCapture)
			WriteCall(obj, "group", args[0]);
		else if (method == CiSystem.MathIsFinite || method == CiSystem.MathIsInfinity || method == CiSystem.MathIsNaN) {
			Write("Double.is");
			Write(method == CiSystem.MathIsFinite ? "Finite"
				: method == CiSystem.MathIsInfinity ? "Infinite"
				: method == CiSystem.MathIsNaN ? "NaN"
				: throw new NotImplementedException(method.Name));
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.MathLog2) {
			if (parent > CiPriority.Mul)
				Write('(');
			WriteCall("Math.log", args[0]);
			Write(" * 1.4426950408889635");
			if (parent > CiPriority.Mul)
				Write(')');
		}
		else {
			if (obj.IsReferenceTo(CiSystem.BasePtr))
				Write("super");
			else
				obj.Accept(this, CiPriority.Primary);
			Write('.');
			if (method == CiSystem.ListRemoveAt)
				Write("remove");
			else if (method == CiSystem.MathCeiling)
				Write("ceil");
			else if (method == CiSystem.MathFusedMultiplyAdd)
				Write("fma");
			else
				WriteName(method);
			WriteArgsInParentheses(method, args);
		}
	}

	static bool IsCollection(CiType type) => type is CiListType || type is CiDictionaryType;

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (parent != CiPriority.Assign && IsUnsignedByte(expr.Type)) {
			if (parent > CiPriority.And)
				Write('(');
			WriteIndexingInternal(expr);
			Write(" & 0xff");
			if (parent > CiPriority.And)
				Write(')');
		}
		else
			WriteIndexingInternal(expr);
	}

	protected override bool IsNotPromotedIndexing(CiBinaryExpr expr) => expr.Op == CiToken.LeftBracket && !IsUnsignedByte(expr.Type);

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
			 Write(indexing.Left.Type is CiDictionaryType ? ".put(" : ".set(");
			 indexing.Right.Accept(this, CiPriority.Argument);
			 Write(", ");
			 WriteAssignRight(expr);
			 Write(')');
		}
		else
			base.WriteAssign(expr, parent);
	}

	protected override string IsOperator => " instanceof ";

	protected override void WriteVar(CiNamedValue def)
	{
		if (def.Type.IsFinal && !def.IsAssignableStorage)
			Write("final ");
		base.WriteVar(def);
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
		WriteNew((CiClass) array.ElementType, CiPriority.Argument);
		WriteLine(';');
		while (--nesting >= 0)
			CloseBlock();
	}

	public override void Visit(CiAssert statement)
	{
		Write("assert ");
		statement.Cond.Accept(this, CiPriority.Argument);
		if (statement.Message != null) {
			Write(" : ");
			statement.Message.Accept(this, CiPriority.Argument);
		}
		WriteLine(';');
	}

	public override void Visit(CiForeach statement)
	{
		Write("for (");
		if (statement.Collection.Type is CiDictionaryType dict) {
			Include("java.util.Map");
			Write("Map.Entry", dict);
			Write(' ');
			Write(statement.Element.Name);
			Write(" : ");
			statement.Collection.Accept(this, CiPriority.Primary);
			Write(".entrySet()");
		}
		else {
			WriteTypeAndName(statement.Element);
			Write(" : ");
			statement.Collection.Accept(this, CiPriority.Argument);
		}
		Write(')');
		WriteChild(statement.Body);
	}

	public override void Visit(CiLock statement)
	{
		Write("synchronized (");
		statement.Lock.Accept(this, CiPriority.Argument);
		Write(')');
		WriteChild(statement.Body);
	}

	public override void Visit(CiThrow statement)
	{
		Write("throw new Exception(");
		statement.Message.Accept(this, CiPriority.Argument);
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
	}

	void Write(CiEnum enu)
	{
		CreateJavaFile(enu.Name);
		WriteLine();
		Write(enu.Documentation);
		WritePublic(enu);
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
				konst.Value.Accept(this, CiPriority.Argument);
			else
				Write(i);
			WriteLine(';');
			i++;
		}
		CloseBlock();
		CloseFile();
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
		if (method.Type != CiSystem.VoidType)
			Write("return ");
		WriteName(method);
		Write('(');
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i > 0)
				Write(", ");
			if (i >= paramCount) {
				param.Value.Accept(this, CiPriority.Argument);
				break;
			}
			WriteName(param);
			i++;
		}
		WriteLine(");");
		CloseBlock();
	}

	void WriteConsts(IEnumerable<CiConst> consts)
	{
		foreach (CiConst konst in consts) {
			WriteLine();
			Write(konst.Documentation);
			Write(konst.Visibility);
			Write("static final ");
			WriteTypeAndName(konst);
			Write(" = ");
			WriteCoercedExpr(konst.Type, konst.Value);
			WriteLine(';');
		}
	}

	void Write(CiClass klass, CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		OpenStringWriter();

		Write(klass.Documentation);
		WritePublic(klass);
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

		if (klass.CallType == CiCallType.Static) {
			Write("private ");
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			CloseBlock();
		}
		else if (NeedsConstructor(klass)) {
			if (klass.Constructor != null) {
				Write(klass.Constructor.Documentation);
				Write(klass.Constructor.Visibility);
			}
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
		CloseBlock();

		CreateJavaFile(klass.Name);
		WriteTopLevelNatives(program);
		WriteIncludes("import ", ";");
		WriteLine();
		CloseStringWriter();
		CloseFile();
	}

	void WriteResources()
	{
		CreateJavaFile("CiResource");
		WriteLine("import java.io.DataInputStream;");
		WriteLine("import java.io.IOException;");
		WriteLine();
		Write("class CiResource");
		WriteLine();
		OpenBlock();
		WriteLine("static byte[] getByteArray(String name, int length)");
		OpenBlock();
		Write("DataInputStream dis = new DataInputStream(");
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
		Write("catch (IOException e) ");
		OpenBlock();
		WriteLine("throw new RuntimeException();");
		CloseBlock();
		WriteLine("return result;");
		CloseBlock();
		CloseBlock();
		CloseFile();
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
