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

namespace Foxoft.Ci
{

public class GenJava : GenTyped
{
	string OutputDirectory;

	public override void VisitLiteralLong(long value)
	{
		base.VisitLiteralLong(value);
		if (value != (int) value)
			WriteChar('L');
	}

	protected override int GetLiteralChars() => 0x10000;

	protected override void WritePrintfWidth(CiInterpolatedPart part)
	{
		if (part.Precision >= 0 && part.Argument.Type is CiIntegerType) {
			if (part.WidthExpr != null)
				throw new NotImplementedException("Cannot format integer with both width and precision");
			WriteChar('0');
			VisitLiteralLong(part.Precision);
		}
		else
			base.WritePrintfWidth(part);
	}

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		if (expr.Suffix.Length == 0
		 && expr.Parts.Count == 1
		 && expr.Parts[0].Prefix.Length == 0
		 && expr.Parts[0].WidthExpr == null
		 && expr.Parts[0].Format == ' ') {
			CiExpr arg = expr.Parts[0].Argument;
			switch (arg.Type.Id) {
			case CiId.LongType:
				Write("Long");
				break;
			case CiId.FloatType:
				Write("Float");
				break;
			case CiId.DoubleType:
			case CiId.FloatIntType:
				Write("Double");
				break;
			case CiId.StringPtrType:
			case CiId.StringStorageType:
				arg.Accept(this, parent);
				return expr;
			default:
				if (arg.Type is CiIntegerType)
					Write("Integer");
				else if (arg.Type is CiClassType) {
					arg.Accept(this, CiPriority.Primary);
					Write(".toString()");
					return expr;
				}
				else
					throw new NotImplementedException(arg.Type.ToString());
				break;
			}
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
			WriteChar('_');
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
				WriteChar('_');
			}
			WriteUppercaseWithUnderscores(symbol.Name);
			break;
		case CiVar _:
			if (symbol.Parent is CiForeach forEach && forEach.Count() == 2) {
				CiVar element = forEach.GetVar();
				WriteCamelCaseNotKeyword(element.Name);
				Write(symbol == element ? ".getKey()" : ".getValue()");
			}
			else
				WriteCamelCaseNotKeyword(symbol.Name);
			break;
		case CiMember _:
			WriteCamelCaseNotKeyword(symbol.Name);
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
	}

	void WriteVisibility(CiVisibility visibility)
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
		if (integer.Id == CiId.LongType)
			return TypeCode.Int64;
		if (promote || integer.Id == CiId.IntType)
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

	void WriteTypeCode(TypeCode typeCode, bool needClass)
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

	protected override void WriteTypeCode(TypeCode typeCode) => WriteTypeCode(typeCode, false);

	void WriteCollectionType(string name, CiType elementType)
	{
		Include("java.util." + name);
		Write(name);
		WriteChar('<');
		WriteType(elementType, false, true);
		WriteChar('>');
	}

	void WriteDictType(string name, CiClassType dict)
	{
		Write(name);
		WriteChar('<');
		WriteType(dict.GetKeyType(), false, true);
		Write(", ");
		WriteType(dict.GetValueType(), false, true);
		WriteChar('>');
	}

	void WriteType(CiType type, bool promote, bool needClass)
	{
		switch (type) {
		case CiIntegerType integer:
			WriteTypeCode(GetIntegerTypeCode(integer, promote), needClass);
			break;
		case CiEnum enu:
			Write(enu.Id == CiId.BoolType
				? needClass ? "Boolean" : "boolean"
				: needClass ? "Integer" : "int");
			break;
 		case CiClassType klass:
			switch (klass.Class.Id) {
			case CiId.StringClass:
				Write("String");
				break;
			case CiId.ArrayPtrClass:
			case CiId.ArrayStorageClass:
				WriteType(klass.GetElementType(), false);
				Write("[]");
				break;
			case CiId.ListClass:
				WriteCollectionType("ArrayList", klass.GetElementType());
				break;
			case CiId.QueueClass:
				WriteCollectionType("ArrayDeque", klass.GetElementType());
				break;
			case CiId.StackClass:
				WriteCollectionType("Stack", klass.GetElementType());
				break;
			case CiId.HashSetClass:
				WriteCollectionType("HashSet", klass.GetElementType());
				break;
			case CiId.DictionaryClass:
				Include("java.util.HashMap");
				WriteDictType("HashMap", klass);
				break;
			case CiId.SortedDictionaryClass:
				Include("java.util.TreeMap");
				WriteDictType("TreeMap", klass);
				break;
			case CiId.OrderedDictionaryClass:
				Include("java.util.LinkedHashMap");
				WriteDictType("LinkedHashMap", klass);
				break;
			case CiId.RegexClass:
				Include("java.util.regex.Pattern");
				Write("Pattern");
				break;
			case CiId.MatchClass:
				Include("java.util.regex.Matcher");
				Write("Matcher");
				break;
			case CiId.LockClass:
				Write("Object");
				break;
			default:
				Write(klass.Class.Name);
				break;
			}
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void WriteType(CiType type, bool promote) => WriteType(type, promote, false);

	protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
	{
		Write("new ");
		WriteType(klass, false, false);
		Write("()");
	}

	protected override void WriteResource(string name, int length)
	{
		Write("CiResource.getByteArray(");
		VisitLiteralString(name);
		Write(", ");
		VisitLiteralLong(length);
		WriteChar(')');
	}

	public override CiExpr VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
	{
		if ((expr.Op == CiToken.Increment || expr.Op == CiToken.Decrement)
		 && expr.Inner is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)) {
			if (parent > CiPriority.And)
				WriteChar('(');
			Write(expr.Op == CiToken.Increment ? "++" : "--");
			WriteIndexingInternal(leftBinary);
			if (parent != CiPriority.Statement)
				Write(" & 0xff");
			if (parent > CiPriority.And)
				WriteChar(')');
			return expr;
		}
		return base.VisitPrefixExpr(expr, parent);
	}

	public override CiExpr VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent)
	{
		if ((expr.Op == CiToken.Increment || expr.Op == CiToken.Decrement)
		 && expr.Inner is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)) {
			if (parent > CiPriority.And)
				WriteChar('(');
			WriteIndexingInternal(leftBinary);
			Write(expr.Op == CiToken.Increment ? "++" : "--");
			if (parent != CiPriority.Statement)
				Write(" & 0xff");
			if (parent > CiPriority.And)
				WriteChar(')');
			return expr;
		}
		return base.VisitPostfixExpr(expr, parent);
	}

	void WriteIndexingInternal(CiBinaryExpr expr)
	{
		if (expr.Left.Type.IsArray())
			base.WriteIndexing(expr, CiPriority.And /* don't care */);
		else
			WriteCall(expr.Left, "get", expr.Right);
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if ((expr.Left.Type is CiStringType && expr.Right.Type.Id != CiId.NullType)
		 || (expr.Right.Type is CiStringType && expr.Left.Type.Id != CiId.NullType)) {
			if (not)
				WriteChar('!');
			WriteCall(expr.Left, "equals", expr.Right);
		}
		else if (expr.Left is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)
			&& expr.Right is CiLiteralLong rightLiteral && rightLiteral.Value >= 0 && rightLiteral.Value <= byte.MaxValue) {
			if (parent > CiPriority.Equality)
				WriteChar('(');
			WriteIndexingInternal(leftBinary); // omit "& 0xff"
			Write(GetEqOp(not));
			VisitLiteralLong((sbyte) rightLiteral.Value);
			if (parent > CiPriority.Equality)
				WriteChar(')');
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	static bool IsUnsignedByte(CiType type)
	{
		return type is CiRangeType range && range.Min >= 0 && range.Max > sbyte.MaxValue && range.Max <= byte.MaxValue;
	}

	protected override void WriteCoercedLiteral(CiType type, CiExpr literal)
	{
		if (IsUnsignedByte(type))
			VisitLiteralLong((sbyte) ((CiLiteralLong) literal).Value);
		else
			literal.Accept(this, CiPriority.Argument);
	}

	protected override void WriteAnd(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && IsUnsignedByte(leftBinary.Type)
		 && expr.Right is CiLiteralLong rightLiteral) {
			if (parent > CiPriority.CondAnd && parent != CiPriority.And)
				WriteChar('(');
			WriteIndexingInternal(leftBinary);
			Write(" & ");
			VisitLiteralLong(0xff & rightLiteral.Value);
			if (parent > CiPriority.CondAnd && parent != CiPriority.And)
				WriteChar(')');
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

	public override CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.ListCount:
		case CiId.QueueCount:
		case CiId.StackCount:
		case CiId.HashSetCount:
		case CiId.DictionaryCount:
		case CiId.SortedDictionaryCount:
		case CiId.OrderedDictionaryCount:
			expr.Left.Accept(this, CiPriority.Primary);
			WriteMemberOp(expr.Left, expr);
			Write("size()");
			return expr;
		case CiId.MathNaN:
			Write("Float.NaN");
			return expr;
		case CiId.MathNegativeInfinity:
			Write("Float.NEGATIVE_INFINITY");
			return expr;
		case CiId.MathPositiveInfinity:
			Write("Float.POSITIVE_INFINITY");
			return expr;
		default:
			if (WriteJavaMatchProperty(expr, parent))
				return expr;
			return base.VisitSymbolReference(expr, parent);
		}
	}

	void WriteArrayBinarySearchFill(CiExpr obj, string method, List<CiExpr> args)
	{
		Include("java.util.Arrays");
		Write("Arrays.");
		Write(method);
		WriteChar('(');
		obj.Accept(this, CiPriority.Argument);
		Write(", ");
		if (args.Count == 3) {
			WriteStartEnd(args[1], args[2]);
			Write(", ");
		}
		WriteNotPromoted(((CiClassType) obj.Type).GetElementType(), args[0]);
		WriteChar(')');
	}

	void WriteConsoleWrite(CiExpr obj, CiMethod method, List<CiExpr> args, bool newLine)
	{
		Write(IsReferenceTo(obj, CiId.ConsoleError) ? "System.err" : "System.out");
		if (args.Count == 1 && args[0] is CiInterpolatedString interpolated) {
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

	void WriteCompileRegex(List<CiExpr> args, int argIndex)
	{
		Include("java.util.regex.Pattern");
		Write("Pattern.compile(");
		args[argIndex].Accept(this, CiPriority.Argument);
		WriteRegexOptions(args, ", ", " | ", "", "Pattern.CASE_INSENSITIVE", "Pattern.MULTILINE", "Pattern.DOTALL");
		WriteChar(')');
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.StringSubstring:
			obj.Accept(this, CiPriority.Primary);
			Write(".substring(");
			args[0].Accept(this, CiPriority.Argument);
			if (args.Count == 2) {
				Write(", ");
				WriteAdd(args[0], args[1]); // TODO: side effect
			}
			WriteChar(')');
			break;
		case CiId.ArrayBinarySearchAll:
		case CiId.ArrayBinarySearchPart:
			WriteArrayBinarySearchFill(obj, "binarySearch", args);
			break;
		case CiId.ArrayCopyTo:
			Write("System.arraycopy(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteArgs(method, args);
			WriteChar(')');
			break;
		case CiId.ArrayFillAll:
		case CiId.ArrayFillPart:
			WriteArrayBinarySearchFill(obj, "fill", args);
			break;
		case CiId.ArraySortAll:
			Include("java.util.Arrays");
			WriteCall("Arrays.sort", obj);
			break;
		case CiId.ArraySortPart:
			Include("java.util.Arrays");
			Write("Arrays.sort(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteStartEnd(args[0], args[1]);
			WriteChar(')');
			break;
		case CiId.ListAdd:
			WriteListAdd(obj, "add", args);
			break;
		case CiId.ListAny:
			WriteCall(obj, "stream().anyMatch", args[0]);
			break;
		case CiId.ListCopyTo:
			Write("for (int _i = 0; _i < ");
			args[3].Accept(this, CiPriority.Rel); // FIXME: side effect in every iteration
			WriteLine("; _i++)");
			Write("\t");
			args[1].Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
			WriteChar('[');
			StartAdd(args[2]); // FIXME: side effect in every iteration
			Write("_i] = ");
			obj.Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
			Write(".get(");
			StartAdd(args[0]); // FIXME: side effect in every iteration
			Write("_i)");
			break;
		case CiId.ListInsert:
			WriteListInsert(obj, "add", args);
			break;
		case CiId.ListRemoveAt:
			WriteCall(obj, "remove", args[0]);
			break;
		case CiId.ListRemoveRange:
			obj.Accept(this, CiPriority.Primary);
			Write(".subList(");
			WriteStartEnd(args[0], args[1]);
			Write(").clear()");
			break;
		case CiId.ListSortAll:
			obj.Accept(this, CiPriority.Primary);
			Write(".sort(null)");
			break;
		case CiId.ListSortPart:
			obj.Accept(this, CiPriority.Primary);
			Write(".subList(");
			WriteStartEnd(args[0], args[1]);
			Write(").sort(null)");
			break;
		case CiId.QueueDequeue:
			obj.Accept(this, CiPriority.Primary);
			Write(".remove()");
			break;
		case CiId.QueueEnqueue:
			WriteCall(obj, "add", args[0]);
			break;
		case CiId.QueuePeek:
			obj.Accept(this, CiPriority.Primary);
			Write(".element()");
			break;
		case CiId.DictionaryAdd:
			obj.Accept(this, CiPriority.Primary);
			Write(".put(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteNewStorage(((CiClassType) obj.Type).GetValueType());
			WriteChar(')');
			break;
		case CiId.ConsoleWrite:
			WriteConsoleWrite(obj, method, args, false);
			break;
		case CiId.ConsoleWriteLine:
			WriteConsoleWrite(obj, method, args, true);
			break;
		case CiId.UTF8GetByteCount:
			Include("java.nio.charset.StandardCharsets");
			args[0].Accept(this, CiPriority.Primary);
			Write(".getBytes(StandardCharsets.UTF_8).length"); // FIXME: quick&dirty!
			break;
		case CiId.UTF8GetBytes:
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
			if (!args[2].IsLiteralZero()) {
				Write(" - ");
				args[2].Accept(this, CiPriority.Mul); // FIXME: side effect
			}
			Write("), true)");
			break;
		case CiId.UTF8GetString:
			Include("java.nio.charset.StandardCharsets");
			Write("new String(");
			WriteArgs(method, args);
			Write(", StandardCharsets.UTF_8)");
			break;
		case CiId.EnvironmentGetEnvironmentVariable:
			WriteCall("System.getenv", args[0]);
			break;
		case CiId.RegexCompile:
			WriteCompileRegex(args, 0);
			break;
		case CiId.RegexEscape:
			Include("java.util.regex.Pattern");
			WriteCall("Pattern.quote", args[0]);
			break;
		case CiId.RegexIsMatchStr:
			WriteCompileRegex(args, 1);
			WriteCall(".matcher", args[0]);
			Write(".find()");
			break;
		case CiId.RegexIsMatchRegex:
			WriteCall(obj, "matcher", args[0]);
			Write(".find()");
			break;
		case CiId.MatchFindStr:
		case CiId.MatchFindRegex:
			WriteChar('(');
			obj.Accept(this, CiPriority.Assign);
			Write(" = ");
			if (method.Id == CiId.MatchFindStr)
				WriteCompileRegex(args, 1);
			else
				args[1].Accept(this, CiPriority.Primary);
			WriteCall(".matcher", args[0]);
			Write(").find()");
			break;
		case CiId.MatchGetCapture:
			WriteCall(obj, "group", args[0]);
			break;
		case CiId.MathCeiling:
			WriteCall("Math.ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			WriteCall("Math.fma", args[0], args[1], args[2]);
			break;
		case CiId.MathIsFinite:
			WriteCall("Double.isFinite", args[0]);
			break;
		case CiId.MathIsInfinity:
			WriteCall("Double.isInfinite", args[0]);
			break;
		case CiId.MathIsNaN:
			WriteCall("Double.isNaN", args[0]);
			break;
		case CiId.MathLog2:
			if (parent > CiPriority.Mul)
				WriteChar('(');
			WriteCall("Math.log", args[0]);
			Write(" * 1.4426950408889635");
			if (parent > CiPriority.Mul)
				WriteChar(')');
			break;
		default:
			if (obj != null) {
				if (IsReferenceTo(obj, CiId.BasePtr))
					Write("super");
				else
					obj.Accept(this, CiPriority.Primary);
				WriteChar('.');
			}
			WriteName(method);
			WriteArgsInParentheses(method, args);
			break;
		}
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (parent != CiPriority.Assign && IsUnsignedByte(expr.Type)) {
			if (parent > CiPriority.And)
				WriteChar('(');
			WriteIndexingInternal(expr);
			Write(" & 0xff");
			if (parent > CiPriority.And)
				WriteChar(')');
		}
		else
			WriteIndexingInternal(expr);
	}

	protected override bool IsNotPromotedIndexing(CiBinaryExpr expr) => expr.Op == CiToken.LeftBracket && !IsUnsignedByte(expr.Type);

	protected override void WriteAssignRight(CiBinaryExpr expr)
	{
		if ((!expr.Left.IsIndexing() || !IsUnsignedByte(expr.Left.Type))
		 && expr.Right is CiBinaryExpr rightBinary && rightBinary.IsAssign() && IsUnsignedByte(expr.Right.Type)) {
			WriteChar('(');
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
		 && indexing.Left.Type is CiClassType klass
		 && !klass.IsArray()) {
			indexing.Left.Accept(this, CiPriority.Primary);
			Write(klass.Class.Id == CiId.ListClass ? ".set(" : ".put(");
			indexing.Right.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteNotPromoted(expr.Type, expr.Right);
			WriteChar(')');
		}
		else
			base.WriteAssign(expr, parent);
	}

	protected override string GetIsOperator() => " instanceof ";

	protected override void WriteVar(CiNamedValue def)
	{
		if (def.Type.IsFinal() && !def.IsAssignableStorage())
			Write("final ");
		base.WriteVar(def);
	}

	protected override bool HasInitCode(CiNamedValue def) => (def.Type is CiArrayStorageType && def.Type.GetStorageType() is CiStorageType) || base.HasInitCode(def);

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (!HasInitCode(def))
			return;
		if (def.Type is CiArrayStorageType array) {
			int nesting = 0;
			while (array.GetElementType() is CiArrayStorageType innerArray) {
				OpenLoop("int", nesting++, array.Length);
				array = innerArray;
			}
			OpenLoop("int", nesting++, array.Length);
			WriteArrayElement(def, nesting);
			Write(" = ");
			WriteNew((CiStorageType) array.GetElementType(), CiPriority.Argument);
			WriteLine(';');
			while (--nesting >= 0)
				CloseBlock();
		}
		else
			base.WriteInitCode(def);
	}

	public override void VisitLambdaExpr(CiLambdaExpr expr)
	{
		WriteName(expr.First);
		Write(" -> ");
		expr.Body.Accept(this, CiPriority.Statement);
	}

	protected override void WriteAssert(CiAssert statement)
	{
		if (statement.CompletesNormally()) {
			Write("assert ");
			statement.Cond.Accept(this, CiPriority.Argument);
			if (statement.Message != null) {
				Write(" : ");
				statement.Message.Accept(this, CiPriority.Argument);
			}
		}
		else {
			// assert false;
			Write("throw new AssertionError(");
			statement.Message?.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}
		WriteLine(';');
	}

	public override void VisitForeach(CiForeach statement)
	{
		Write("for (");
		CiClassType klass = (CiClassType) statement.Collection.Type;
		switch (klass.Class.Id) {
		case CiId.StringClass:
			Write("int _i = 0; _i < ");
			WriteStringLength(statement.Collection); // FIXME: side effect in every iteration
			Write("; _i++) ");
			OpenBlock();
			WriteTypeAndName(statement.GetVar());
			Write(" = ");
			statement.Collection.Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
			WriteLine(".charAt(_i);");
			FlattenBlock(statement.Body);
			CloseBlock();
			return;
		case CiId.DictionaryClass:
		case CiId.SortedDictionaryClass:
		case CiId.OrderedDictionaryClass:
			Include("java.util.Map");
			WriteDictType("Map.Entry", klass);
			WriteChar(' ');
			Write(statement.GetVar().Name);
			Write(" : ");
			statement.Collection.Accept(this, CiPriority.Primary);
			Write(".entrySet()");
			break;
		default:
			WriteTypeAndName(statement.GetVar());
			Write(" : ");
			statement.Collection.Accept(this, CiPriority.Argument);
			break;
		}
		WriteChar(')');
		WriteChild(statement.Body);
	}

	public override void VisitLock(CiLock statement)
	{
		Write("synchronized (");
		statement.Lock.Accept(this, CiPriority.Argument);
		WriteChar(')');
		WriteChild(statement.Body);
	}

	protected override void WriteSwitchValue(CiExpr expr)
	{
		if (expr is CiBinaryExpr indexing && indexing.Op == CiToken.LeftBracket && IsUnsignedByte(indexing.Type))
			WriteIndexingInternal(indexing); // omit "& 0xff"
		else
			base.WriteSwitchValue(expr);
	}

	public override void VisitThrow(CiThrow statement)
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

	public override void VisitEnumValue(CiConst konst, CiConst previous)
	{
		WriteDoc(konst.Documentation);
		Write("int ");
		WriteUppercaseWithUnderscores(konst.Name);
		Write(" = ");
		if (konst.Value is CiImplicitEnumValue imp)
			VisitLiteralLong(imp.Value);
		else
			konst.Value.Accept(this, CiPriority.Argument);
		WriteLine(';');
	}

	protected override void WriteEnum(CiEnum enu)
	{
		CreateJavaFile(enu.Name);
		WriteLine();
		WriteDoc(enu.Documentation);
		WritePublic(enu);
		Write("interface ");
		WriteLine(enu.Name);
		OpenBlock();
		enu.AcceptValues(this);
		CloseBlock();
		CloseFile();
	}

	void WriteSignature(CiMethod method, int paramCount)
	{
		WriteLine();
		WriteMethodDoc(method);
		WriteVisibility(method.Visibility);
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
		WriteChar('(');
		CiVar param = method.Parameters.FirstParameter();
		for (int i = 0; i < paramCount; i++) {
			if (i > 0)
				Write(", ");
			WriteTypeAndName(param);
			param = param.NextParameter();
		}
		WriteChar(')');
		if (method.Throws)
			Write(" throws Exception");
	}

	void WriteOverloads(CiMethod method, int paramCount)
	{
		if (paramCount + 1 < method.Parameters.Count())
			WriteOverloads(method, paramCount + 1);
		WriteSignature(method, paramCount);
		WriteLine();
		OpenBlock();
		if (method.Type.Id != CiId.VoidType)
			Write("return ");
		WriteName(method);
		WriteChar('(');
		CiVar param = method.Parameters.FirstParameter();
		for (int i = 0; i < paramCount; i++) {
			WriteName(param);
			Write(", ");
			param = param.NextParameter();
		}
		param.Value.Accept(this, CiPriority.Argument);
		WriteLine(");");
		CloseBlock();
	}

	protected override void WriteConst(CiConst konst)
	{
		WriteLine();
		WriteDoc(konst.Documentation);
		WriteVisibility(konst.Visibility);
		Write("static final ");
		WriteTypeAndName(konst);
		Write(" = ");
		WriteCoercedExpr(konst.Type, konst.Value);
		WriteLine(';');
	}

	protected override void WriteField(CiField field)
	{
		WriteDoc(field.Documentation);
		WriteVisibility(field.Visibility);
		WriteVar(field);
		WriteLine(';');
	}

	protected override void WriteMethod(CiMethod method)
	{
		WriteSignature(method, method.Parameters.Count());
		WriteBody(method);
		int i = 0;
		for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
			if (param.Value != null) {
				WriteOverloads(method, i);
				break;
			}
			i++;
		}
	}

	protected override void WriteClass(CiClass klass, CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		OpenStringWriter();

		WriteDoc(klass.Documentation);
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
				WriteDoc(klass.Constructor.Documentation);
				WriteVisibility(klass.Constructor.Visibility);
			}
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			WriteConstructorBody(klass);
			CloseBlock();
		}

		WriteMembers(klass, true);

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

	public override void WriteProgram(CiProgram program)
	{
		if (Directory.Exists(this.OutputFile))
			this.OutputDirectory = this.OutputFile;
		else
			this.OutputDirectory = Path.GetDirectoryName(this.OutputFile);
		WriteTypes(program);
		if (program.Resources.Count > 0)
			WriteResources();
	}
}

}
