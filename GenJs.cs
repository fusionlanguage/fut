// GenJs.cs - JavaScript code generator
//
// Copyright (C) 2011-2023  Piotr Fusik
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
using System.Linq;

namespace Foxoft.Ci
{

public class GenJs : GenBase
{
	// TODO: Namespace

	protected readonly List<CiSwitch> SwitchesWithLabel = new List<CiSwitch>();

	protected override string GetTargetName() => "JavaScript";

	void WriteCamelCaseNotKeyword(string name)
	{
		WriteCamelCase(name);
		switch (name) {
		case "Constructor":
		case "catch":
		case "debugger":
		case "delete":
		case "export":
		case "extends":
		case "finally":
		case "function":
		case "implements":
		case "import":
		case "instanceof":
		case "interface":
		case "let":
		case "package":
		case "private":
		case "super":
		case "try":
		case "typeof":
		case "var":
		case "with":
		case "yield":
			WriteChar('_');
			break;
		default:
			break;
		}
	}

	protected virtual bool IsJsPrivate(CiMember member) => member.Visibility == CiVisibility.Private;

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
			WriteCamelCaseNotKeyword(symbol.Name);
			break;
		case CiMember member:
			if (IsJsPrivate(member)) {
				WriteChar('#');
				WriteCamelCase(symbol.Name);
				if (symbol.Name == "Constructor")
					WriteChar('_');
			}
			else
				WriteCamelCaseNotKeyword(symbol.Name);
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
	}

	protected override void WriteTypeAndName(CiNamedValue value) => WriteName(value);

	protected static string GetArrayElementType(CiNumericType type)
	{
		switch (type.Id) {
		case CiId.IntType:
			return "Int32";
		case CiId.LongType:
			// TODO: UInt32 if possible?
			return "Float64"; // no 64-bit integers in JavaScript
		case CiId.FloatType:
			return "Float32";
		case CiId.DoubleType:
			return "Float64";
		default:
			CiRangeType range = (CiRangeType) type;
			if (range.Min < 0) {
				if (range.Min < short.MinValue || range.Max > short.MaxValue)
					return "Int32";
				if (range.Min < sbyte.MinValue || range.Max > sbyte.MaxValue)
					return "Int16";
				return "Int8";
			}
			if (range.Max > ushort.MaxValue)
				return "Int32";
			if (range.Max > byte.MaxValue)
				return "Uint16";
			return "Uint8";
		}
	}

	public override void VisitAggregateInitializer(CiAggregateInitializer expr)
	{
		CiNumericType number = ((CiArrayStorageType) expr.Type).GetElementType() as CiNumericType;
		if (number != null) {
			Write("new ");
			Write(GetArrayElementType(number));
			Write("Array(");
		}
		Write("[ ");
		WriteCoercedLiterals(null, expr.Items);
		Write(" ]");
		if (number != null)
			WriteChar(')');
	}

	protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
	{
		switch (klass.Class.Id) {
		case CiId.ListClass:
		case CiId.QueueClass:
		case CiId.StackClass:
			Write("[]");
			break;
		case CiId.HashSetClass:
			Write("new Set()");
			break;
		case CiId.DictionaryClass:
		case CiId.SortedDictionaryClass:
			Write("{}");
			break;
		case CiId.OrderedDictionaryClass:
			Write("new Map()");
			break;
		case CiId.LockClass:
			NotSupported(klass, "Lock");
			break;
		default:
			Write("new ");
			Write(klass.Class.Name);
			Write("()");
			break;
		}
	}

	protected override void WriteNewWithFields(CiType type, CiAggregateInitializer init)
	{
		Write("Object.assign(");
		WriteNew((CiReadWriteClassType) type, CiPriority.Argument);
		WriteChar(',');
		WriteObjectLiteral(init, ": ");
		WriteChar(')');
	}

	protected override void WriteVar(CiNamedValue def)
	{
		Write(def.Type.IsFinal() && !def.IsAssignableStorage() ? "const " : "let ");
		base.WriteVar(def);
	}

	void WriteInterpolatedLiteral(string s)
	{
		for (int i = 0; i < s.Length; i++) {
			char c = s[i];
			if (c == '`'
			 || (c == '$' && i + 1 < s.Length && s[i + 1] == '{'))
				WriteChar('\\');
			WriteChar(c);
		}
	}

	public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		WriteChar('`');
		foreach (CiInterpolatedPart part in expr.Parts) {
			WriteInterpolatedLiteral(part.Prefix);
			Write("${");
			if (part.Width != 0 || part.Format != ' ') {
				part.Argument.Accept(this, CiPriority.Primary);
				if (part.Argument.Type is CiNumericType) {
					switch (part.Format) {
					case 'E':
						Write(".toExponential(");
						if (part.Precision >= 0)
							VisitLiteralLong(part.Precision);
						Write(").toUpperCase()");
						break;
					case 'e':
						Write(".toExponential(");
						if (part.Precision >= 0)
							VisitLiteralLong(part.Precision);
						WriteChar(')');
						break;
					case 'F':
					case 'f':
						Write(".toFixed(");
						if (part.Precision >= 0)
							VisitLiteralLong(part.Precision);
						WriteChar(')');
						break;
					case 'X':
						Write(".toString(16).toUpperCase()");
						break;
					case 'x':
						Write(".toString(16)");
						break;
					default:
						Write(".toString()");
						break;
					}
					if (part.Precision >= 0 && "DdXx".IndexOf((char) part.Format) >= 0) {
						Write(".padStart(");
						VisitLiteralLong(part.Precision);
						Write(", \"0\")");
					}
				}
				if (part.Width > 0) {
					Write(".padStart(");
					VisitLiteralLong(part.Width);
					WriteChar(')');
				}
				else if (part.Width < 0) {
					Write(".padEnd(");
					VisitLiteralLong(-part.Width);
					WriteChar(')');
				}
			}
			else
				part.Argument.Accept(this, CiPriority.Argument);
			WriteChar('}');
		}
		WriteInterpolatedLiteral(expr.Suffix);
		WriteChar('`');
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiMember member) {
			if (!member.IsStatic())
				Write("this");
			else if (this.CurrentMethod != null)
				Write(this.CurrentMethod.Parent.Name);
			else if (symbol is CiConst konst) {
				konst.Value.Accept(this, parent);
				return;
			}
			else
				throw new NotImplementedException(symbol.ToString());
			WriteChar('.');
		}
		WriteName(symbol);
		if (symbol.Parent is CiForeach forEach && forEach.Collection.Type is CiStringType)
			Write(".codePointAt(0)");
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Write("new ");
		if (elementType is CiNumericType numeric)
			Write(GetArrayElementType(numeric));
		WriteCall("Array", lengthExpr);
	}

	protected override bool HasInitCode(CiNamedValue def) => def.Type is CiArrayStorageType array && array.GetElementType() is CiStorageType;

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (!HasInitCode(def))
			return;
		CiArrayStorageType array = (CiArrayStorageType) def.Type;
		int nesting = 0;
		while (array.GetElementType() is CiArrayStorageType innerArray) {
			OpenLoop("let", nesting++, array.Length);
			WriteArrayElement(def, nesting);
			Write(" = ");
			WriteNewArray(innerArray.GetElementType(), innerArray.LengthExpr, CiPriority.Argument);
			WriteLine(';');
			array = innerArray;
		}
		if (array.GetElementType() is CiStorageType klass) {
			OpenLoop("let", nesting++, array.Length);
			WriteArrayElement(def, nesting);
			Write(" = ");
			WriteNew(klass, CiPriority.Argument);
			WriteLine(';');
		}
		while (--nesting >= 0)
			CloseBlock();
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("Ci.");
		foreach (char c in name)
			WriteChar(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.ListCount:
		case CiId.QueueCount:
		case CiId.StackCount:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".length");
			break;
		case CiId.HashSetCount:
		case CiId.OrderedDictionaryCount:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".size");
			break;
		case CiId.DictionaryCount:
		case CiId.SortedDictionaryCount:
			WriteCall("Object.keys", expr.Left);
			Write(".length");
			break;
		case CiId.MatchStart:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".index");
			break;
		case CiId.MatchEnd:
			if (parent > CiPriority.Add)
				WriteChar('(');
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".index + ");
			expr.Left.Accept(this, CiPriority.Primary); // FIXME: side effect
			Write("[0].length");
			if (parent > CiPriority.Add)
				WriteChar(')');
			break;
		case CiId.MatchLength:
			expr.Left.Accept(this, CiPriority.Primary);
			Write("[0].length");
			break;
		case CiId.MatchValue:
			expr.Left.Accept(this, CiPriority.Primary);
			Write("[0]");
			break;
		case CiId.MathNaN:
			Write("NaN");
			break;
		case CiId.MathNegativeInfinity:
			Write("-Infinity");
			break;
		case CiId.MathPositiveInfinity:
			Write("Infinity");
			break;
		default:
			base.VisitSymbolReference(expr, parent);
			break;
		}
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		expr.Accept(this, CiPriority.Primary);
		Write(".length");
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		WriteCall(expr.Left, "charCodeAt", expr.Right);
	}

	static bool IsIdentifier(string s)
	{
		return s.Length > 0
			&& s[0] >= 'A'
			&& s.All(c => CiLexer.IsLetterOrDigit(c));
	}

	void WriteNewRegex(List<CiExpr> args, int argIndex)
	{
		CiExpr pattern = args[argIndex];
		if (pattern is CiLiteralString literal) {
			WriteChar('/');
			bool escaped = false;
			foreach (char c in literal.Value) {
				switch (c) {
				case '\\':
					if (!escaped) {
						escaped = true;
						continue;
					}
					escaped = false;
					break;
				case '"':
				case '\'':
					escaped = false;
					break;
				case '/':
					escaped = true;
					break;
				default:
					break;
				}
				if (escaped) {
					WriteChar('\\');
					escaped = false;
				}
				WriteChar(c);
			}
			WriteChar('/');
			WriteRegexOptions(args, "", "", "", "i", "m", "s");
		}
		else {
			Write("new RegExp(");
			pattern.Accept(this, CiPriority.Argument);
			WriteRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
			WriteChar(')');
		}
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.None:
		case CiId.ClassToString:
		case CiId.StringEndsWith:
		case CiId.StringIndexOf:
		case CiId.StringLastIndexOf:
		case CiId.StringStartsWith:
		case CiId.ArraySortAll:
		case CiId.StackPush:
		case CiId.StackPop:
		case CiId.HashSetAdd:
		case CiId.HashSetClear:
		case CiId.OrderedDictionaryClear:
		case CiId.MathMethod:
		case CiId.MathLog2:
			if (obj == null)
				WriteLocalName(method, CiPriority.Primary);
			else {
				if (IsReferenceTo(obj, CiId.BasePtr))
					Write("super");
				else
					obj.Accept(this, CiPriority.Primary);
				WriteChar('.');
				WriteName(method);
			}
			WriteArgsInParentheses(method, args);
			break;
		case CiId.DoubleTryParse:
			Write("!isNaN(");
			obj.Accept(this, CiPriority.Assign);
			Write(" = parseFloat("); // ignores trailing invalid characters; Number() does not, but it accepts empty string and bin/oct/hex
			args[0].Accept(this, CiPriority.Argument);
			Write("))");
			break;
		case CiId.StringContains:
		case CiId.ListContains:
			WriteCall(obj, "includes", args[0]);
			break;
		case CiId.StringReplace:
			WriteCall(obj, "replaceAll", args[0], args[1]);
			break;
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
		case CiId.ArrayFillAll:
		case CiId.ArrayFillPart:
			obj.Accept(this, CiPriority.Primary);
			Write(".fill(");
			args[0].Accept(this, CiPriority.Argument);
			if (args.Count == 3) {
				Write(", ");
				WriteStartEnd(args[1], args[2]);
			}
			WriteChar(')');
			break;
		case CiId.ArrayCopyTo:
		case CiId.ListCopyTo:
			args[1].Accept(this, CiPriority.Primary);
			bool wholeSource = obj.Type is CiArrayStorageType sourceStorage && args[0].IsLiteralZero()
				&& args[3] is CiLiteralLong literalLength && literalLength.Value == sourceStorage.Length;
			if (((CiClassType) obj.Type).GetElementType() is CiNumericType) {
				Write(".set(");
				if (wholeSource)
					obj.Accept(this, CiPriority.Argument);
				else {
					obj.Accept(this, CiPriority.Primary);
					Write(method.Id == CiId.ArrayCopyTo ? ".subarray(" : ".slice(");
					WriteStartEnd(args[0], args[3]);
					WriteChar(')');
				}
				if (!args[2].IsLiteralZero()) {
					Write(", ");
					args[2].Accept(this, CiPriority.Argument);
				}
			}
			else {
				Write(".splice(");
				args[2].Accept(this, CiPriority.Argument);
				Write(", ");
				args[3].Accept(this, CiPriority.Argument);
				Write(", ...");
				obj.Accept(this, CiPriority.Primary);
				if (!wholeSource) {
					Write(".slice(");
					WriteStartEnd(args[0], args[3]);
					WriteChar(')');
				}
			}
			WriteChar(')');
			break;
		case CiId.ArraySortPart:
			obj.Accept(this, CiPriority.Primary);
			Write(".subarray(");
			WriteStartEnd(args[0], args[1]);
			Write(").sort()");
			break;
		case CiId.ListAdd:
			WriteListAdd(obj, "push", args);
			break;
		case CiId.ListAll:
			WriteCall(obj, "every", args[0]);
			break;
		case CiId.ListAny:
			WriteCall(obj, "some", args[0]);
			break;
		case CiId.ListClear:
		case CiId.QueueClear:
		case CiId.StackClear:
			obj.Accept(this, CiPriority.Primary);
			Write(".length = 0");
			break;
		case CiId.ListInsert:
			WriteListInsert(obj, "splice", args, ", 0, ");
			break;
		case CiId.ListRemoveAt:
			obj.Accept(this, CiPriority.Primary);
			Write(".splice(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", 1)");
			break;
		case CiId.ListRemoveRange:
			WriteCall(obj, "splice", args[0], args[1]);
			break;
		case CiId.ListSortAll:
			obj.Accept(this, CiPriority.Primary);
			Write(".sort((a, b) => a - b)");
			break;
		case CiId.ListSortPart:
			obj.Accept(this, CiPriority.Primary);
			Write(".splice(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			args[1].Accept(this, CiPriority.Argument);
			Write(", ...");
			obj.Accept(this, CiPriority.Primary); // TODO: side effect
			Write(".slice(");
			WriteStartEnd(args[0], args[1]); // TODO: side effect
			Write(").sort((a, b) => a - b))");
			break;
		case CiId.QueueDequeue:
			obj.Accept(this, CiPriority.Primary);
			Write(".shift()");
			break;
		case CiId.QueueEnqueue:
			WriteCall(obj, "push", args[0]);
			break;
		case CiId.QueuePeek:
			obj.Accept(this, CiPriority.Primary);
			Write("[0]");
			break;
		case CiId.StackPeek:
			obj.Accept(this, CiPriority.Primary);
			Write(".at(-1)");
			break;
		case CiId.HashSetContains:
			WriteCall(obj, "has", args[0]);
			break;
		case CiId.HashSetRemove:
			WriteCall(obj, "delete", args[0]);
			break;
		case CiId.DictionaryAdd:
			WriteDictionaryAdd(obj, args);
			break;
		case CiId.DictionaryClear:
		case CiId.SortedDictionaryClear:
			Write("for (const key in ");
			obj.Accept(this, CiPriority.Argument);
			WriteLine(')');
			Write("\tdelete ");
			obj.Accept(this, CiPriority.Primary); // FIXME: side effect
			Write("[key];");
			break;
		case CiId.DictionaryContainsKey:
		case CiId.SortedDictionaryContainsKey:
			WriteCall(obj, "hasOwnProperty", args[0]);
			break;
		case CiId.DictionaryRemove:
		case CiId.SortedDictionaryRemove:
			Write("delete ");
			WriteIndexing(obj, args[0]);
			break;
		case CiId.OrderedDictionaryContainsKey:
			WriteCall(obj, "has", args[0]);
			break;
		case CiId.OrderedDictionaryRemove:
			WriteCall(obj, "delete", args[0]);
			break;
		case CiId.ConsoleWrite: // FIXME: Console.Write same as Console.WriteLine
		case CiId.ConsoleWriteLine:
			Write(IsReferenceTo(obj, CiId.ConsoleError) ? "console.error" : "console.log");
			if (args.Count == 0)
				Write("(\"\")");
			else
				WriteArgsInParentheses(method, args);
			break;
		case CiId.UTF8GetByteCount:
			Write("new TextEncoder().encode(");
			args[0].Accept(this, CiPriority.Argument);
			Write(").length");
			break;
		case CiId.UTF8GetBytes:
			Write("new TextEncoder().encodeInto(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			if (args[2].IsLiteralZero())
				args[1].Accept(this, CiPriority.Argument);
			else {
				args[1].Accept(this, CiPriority.Primary);
				Write(".subarray(");
				args[2].Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			WriteChar(')');
			break;
		case CiId.UTF8GetString:
			Write("new TextDecoder().decode(");
			args[0].Accept(this, CiPriority.Primary);
			Write(".subarray(");
			args[1].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteAdd(args[1], args[2]); // FIXME: side effect
			Write("))");
			break;
		case CiId.EnvironmentGetEnvironmentVariable:
			if (args[0] is CiLiteralString literal && IsIdentifier(literal.Value)) {
				Write("process.env.");
				Write(literal.Value);
			}
			else {
				Write("process.env[");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(']');
			}
			break;
		case CiId.RegexCompile:
			WriteNewRegex(args, 0);
			break;
		case CiId.RegexEscape:
			args[0].Accept(this, CiPriority.Primary);
			Write(".replace(/[-\\/\\\\^$*+?.()|[\\]{}]/g, '\\\\$&')");
			break;
		case CiId.RegexIsMatchStr:
			WriteNewRegex(args, 1);
			WriteCall(".test", args[0]);
			break;
		case CiId.RegexIsMatchRegex:
			WriteCall(obj, "test", args[0]);
			break;
		case CiId.MatchFindStr:
		case CiId.MatchFindRegex:
			if (parent > CiPriority.Equality)
				WriteChar('(');
			WriteChar('(');
			obj.Accept(this, CiPriority.Assign);
			Write(" = ");
			if (method.Id == CiId.MatchFindStr)
				WriteNewRegex(args, 1);
			else
				args[1].Accept(this, CiPriority.Primary);
			WriteCall(".exec", args[0]);
			Write(") != null");
			if (parent > CiPriority.Equality)
				WriteChar(')');
			break;
		case CiId.MatchGetCapture:
			WriteIndexing(obj, args[0]);
			break;
		case CiId.MathCeiling:
			WriteCall("Math.ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			if (parent > CiPriority.Add)
				WriteChar('(');
			args[0].Accept(this, CiPriority.Mul);
			Write(" * ");
			args[1].Accept(this, CiPriority.Mul);
			Write(" + ");
			args[2].Accept(this, CiPriority.Add);
			if (parent > CiPriority.Add)
				WriteChar(')');
			break;
		case CiId.MathIsFinite:
		case CiId.MathIsNaN:
			WriteCamelCase(method.Name);
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathIsInfinity:
			if (parent > CiPriority.Equality)
				WriteChar('(');
			WriteCall("Math.abs", args[0]);
			Write(" == Infinity");
			if (parent > CiPriority.Equality)
				WriteChar(')');
			break;
		case CiId.MathTruncate:
			WriteCall("Math.trunc", args[0]);
			break;
		default:
			NotSupported(obj, method.Name);
			break;
		}
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left.Type is CiClassType dict && dict.Class.Id == CiId.OrderedDictionaryClass)
			WriteCall(expr.Left, "get", expr.Right);
		else
			base.WriteIndexing(expr, parent);
	}

	protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left is CiBinaryExpr indexing
		 && indexing.Op == CiToken.LeftBracket
		 && indexing.Left.Type is CiClassType dict
		 && dict.Class.Id == CiId.OrderedDictionaryClass)
			WriteCall(indexing.Left, "set", indexing.Right, expr.Right);
		else
			base.WriteAssign(expr, parent);
	}

	protected override string GetIsOperator() => " instanceof ";

	protected virtual void WriteBoolAndOr(CiBinaryExpr expr)
	{
		Write("!!");
		base.VisitBinaryExpr(expr, CiPriority.Primary);
	}

	void WriteBoolAndOrAssign(CiBinaryExpr expr, CiPriority parent)
	{
		expr.Right.Accept(this, parent);
		WriteLine(')');
		WriteChar('\t');
		expr.Left.Accept(this, CiPriority.Assign);
	}

	public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Slash when expr.Type is CiIntegerType:
			if (parent > CiPriority.Or)
				WriteChar('(');
			expr.Left.Accept(this, CiPriority.Mul);
			Write(" / ");
			expr.Right.Accept(this, CiPriority.Primary);
			Write(" | 0"); // FIXME: long: Math.trunc?
			if (parent > CiPriority.Or)
				WriteChar(')');
			break;
		case CiToken.DivAssign when expr.Type is CiIntegerType:
			if (parent > CiPriority.Assign)
				WriteChar('(');
			expr.Left.Accept(this, CiPriority.Assign);
			Write(" = ");
			expr.Left.Accept(this, CiPriority.Mul); // TODO: side effect
			Write(" / ");
			expr.Right.Accept(this, CiPriority.Primary);
			Write(" | 0");
			if (parent > CiPriority.Assign)
				WriteChar(')');
			break;
		case CiToken.And when expr.Type.Id == CiId.BoolType:
		case CiToken.Or when expr.Type.Id == CiId.BoolType:
			WriteBoolAndOr(expr);
			break;
		case CiToken.Xor when expr.Type.Id == CiId.BoolType:
			WriteEqual(expr, parent, true);
			break;
		case CiToken.AndAssign when expr.Type.Id == CiId.BoolType:
			Write("if (!"); // FIXME: picks up parent "else"
			WriteBoolAndOrAssign(expr, CiPriority.Primary);
			Write(" = false");
			break;
		case CiToken.OrAssign when expr.Type.Id == CiId.BoolType:
			Write("if ("); // FIXME: picks up parent "else"
			WriteBoolAndOrAssign(expr, CiPriority.Argument);
			Write(" = true");
			break;
		case CiToken.XorAssign when expr.Type.Id == CiId.BoolType:
			expr.Left.Accept(this, CiPriority.Assign);
			Write(" = ");
			WriteEqual(expr, CiPriority.Argument, true); // TODO: side effect
			break;
		case CiToken.Is when expr.Right is CiVar def:
			if (parent > CiPriority.CondAnd)
				WriteChar('(');
			expr.Left.Accept(this, CiPriority.Rel);
			Write(" instanceof ");
			Write(def.Type.Name);
			Write(" && !!(");
			WriteCamelCaseNotKeyword(def.Name);
			Write(" = ");
			expr.Left.Accept(this, CiPriority.Argument); // TODO: side effect
			WriteChar(')');
			if (parent > CiPriority.CondAnd)
				WriteChar(')');
			break;
		default:
			base.VisitBinaryExpr(expr, parent);
			break;
		}
	}

	public override void VisitLambdaExpr(CiLambdaExpr expr)
	{
		WriteName(expr.First);
		Write(" => ");
		expr.Body.Accept(this, CiPriority.Statement);
	}

	protected override void StartTemporaryVar(CiType type) => throw new NotImplementedException();

	protected override void DefineObjectLiteralTemporary(CiUnaryExpr expr)
	{
	}

	protected virtual void WriteAsType(CiVar def)
	{
	}

	void WriteVarCast(CiVar def, CiExpr value)
	{
		Write("const ");
		WriteCamelCaseNotKeyword(def.Name);
		Write(" = ");
		value.Accept(this, CiPriority.Argument);
		WriteAsType(def);
		WriteLine(';');
	}

	protected override void WriteAssertCast(CiBinaryExpr expr) => WriteVarCast((CiVar) expr.Right, expr.Left);

	protected override void WriteAssert(CiAssert statement)
	{
		if (statement.CompletesNormally()) {
			Write("console.assert(");
			statement.Cond.Accept(this, CiPriority.Argument);
			if (statement.Message != null) {
				Write(", ");
				statement.Message.Accept(this, CiPriority.Argument);
			}
		}
		else {
			Write("throw new Error(");
			if (statement.Message != null)
				statement.Message.Accept(this, CiPriority.Argument);
		}
		WriteLine(");");
	}

	public override void VisitBreak(CiBreak statement)
	{
		if (statement.LoopOrSwitch is CiSwitch switchStatement) {
			int label = this.SwitchesWithLabel.IndexOf(switchStatement);
			if (label >= 0) {
				Write("break ciswitch");
				VisitLiteralLong(label);
				WriteLine(';');
				return;
			}
		}
		base.VisitBreak(statement);
	}

	public override void VisitForeach(CiForeach statement)
	{
		Write("for (const ");
		CiClassType klass = (CiClassType) statement.Collection.Type;
		switch (klass.Class.Id) {
		case CiId.StringClass:
		case CiId.ArrayStorageClass:
		case CiId.ListClass:
		case CiId.HashSetClass:
			WriteName(statement.GetVar());
			Write(" of ");
			statement.Collection.Accept(this, CiPriority.Argument);
			break;
		case CiId.DictionaryClass:
		case CiId.SortedDictionaryClass:
		case CiId.OrderedDictionaryClass:
			WriteChar('[');
			WriteName(statement.GetVar());
			Write(", ");
			WriteName(statement.GetValueVar());
			Write("] of ");
			if (klass.Class.Id == CiId.OrderedDictionaryClass)
				statement.Collection.Accept(this, CiPriority.Argument);
			else {
				WriteCall("Object.entries", statement.Collection);
				switch (statement.GetVar().Type) {
				case CiStringType _:
					if (klass.Class.Id == CiId.SortedDictionaryClass)
						Write(".sort((a, b) => a[0].localeCompare(b[0]))");
					break;
				case CiNumericType _:
					Write(".map(e => [+e[0], e[1]])");
					if (klass.Class.Id == CiId.SortedDictionaryClass)
						Write(".sort((a, b) => a[0] - b[0])");
					break;
				default:
					throw new NotImplementedException(statement.GetVar().Type.ToString());
				}
			}
			break;
		default:
			throw new NotImplementedException(klass.Class.Name);
		}
		WriteChar(')');
		WriteChild(statement.Body);
	}

	public override void VisitLock(CiLock statement)
	{
		NotSupported(statement, "'lock'");
	}

	void WriteTypeMatchingSwitch(CiSwitch statement)
	{
		string op = "if (";
		foreach (CiCase kase in statement.Cases) {
			CiVar caseVar = null;
			foreach (CiVar def in kase.Values) { // TODO: when
				Write(op);
				statement.Value.Accept(this, CiPriority.Rel); // FIXME: side effect in every if
				Write(" instanceof "); 
				Write(def.Type.Name);
				op = " || ";
				if (def.Name != "_")
					caseVar = def;
			}
			Write(") ");
			OpenBlock();
			if (caseVar != null)
				WriteVarCast(caseVar, statement.Value); // FIXME: side effect
			WriteFirstStatements(kase.Body, CiSwitch.LengthWithoutTrailingBreak(kase.Body));
			CloseBlock();
			op = "else if (";
		}
		if (statement.HasDefault()) {
			Write("else ");
			OpenBlock();
			WriteFirstStatements(statement.DefaultBody, CiSwitch.LengthWithoutTrailingBreak(statement.DefaultBody));
			CloseBlock();
		}
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		if (statement.IsTypeMatching()) {
			if (statement.Cases.Any(kase => CiSwitch.HasEarlyBreak(kase.Body))
			 || CiSwitch.HasEarlyBreak(statement.DefaultBody)) {
				Write("ciswitch");
				VisitLiteralLong(this.SwitchesWithLabel.Count);
				this.SwitchesWithLabel.Add(statement);
				Write(": ");
				OpenBlock();
				WriteTypeMatchingSwitch(statement);
				CloseBlock();
			}
			else
				WriteTypeMatchingSwitch(statement);
		}
		else
			base.VisitSwitch(statement);
	}

	public override void VisitThrow(CiThrow statement)
	{
		Write("throw ");
		statement.Message.Accept(this, CiPriority.Argument);
		WriteLine(';');
	}

	public override void VisitEnumValue(CiConst konst, CiConst previous)
	{
		if (previous != null)
			WriteLine(',');
		WriteDoc(konst.Documentation);
		WriteUppercaseWithUnderscores(konst.Name);
		Write(" : ");
		VisitLiteralLong(konst.Value.IntValue());
	}

	protected override void WriteEnum(CiEnum enu)
	{
		WriteLine();
		WriteDoc(enu.Documentation);
		Write("const ");
		Write(enu.Name);
		Write(" = ");
		OpenBlock();
		enu.AcceptValues(this);
		WriteLine();
		CloseBlock();
	}

	protected override void WriteConst(CiConst konst)
	{
		if (konst.Visibility != CiVisibility.Private || konst.Type is CiArrayStorageType) {
			WriteLine();
			WriteDoc(konst.Documentation);
			Write("static ");
			WriteName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Argument);
			WriteLine(';');
		}
	}

	protected override void WriteField(CiField field)
	{
		WriteDoc(field.Documentation);
		base.WriteVar(field);
		WriteLine(';');
	}

	protected override void WriteMethod(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		this.SwitchesWithLabel.Clear();
		WriteLine();
		WriteMethodDoc(method);
		if (method.CallType == CiCallType.Static)
			Write("static ");
		WriteName(method);
		WriteParameters(method, true);
		WriteBody(method);
	}

	protected void WriteConstructor(CiClass klass)
	{
		this.SwitchesWithLabel.Clear();
		WriteLine("constructor()");
		OpenBlock();
		if (klass.Parent is CiClass)
			WriteLine("super();");
		WriteConstructorBody(klass);
		CloseBlock();
	}

	protected override void WriteClass(CiClass klass, CiProgram program)
	{
		if (!WriteBaseClass(klass, program))
			return;
		WriteLine();
		WriteDoc(klass.Documentation);
		OpenClass(klass, "", " extends ");
		if (NeedsConstructor(klass)) {
			if (klass.Constructor != null)
				WriteDoc(klass.Constructor.Documentation);
			WriteConstructor(klass);
		}
		WriteMembers(klass, true);
		CloseBlock();
	}

	protected void WriteLib(Dictionary<string, byte[]> resources)
	{
		if (resources.Count == 0)
			return;
		WriteLine();
		WriteLine("class Ci");
		OpenBlock();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			Write("static ");
			WriteResource(name, -1);
			WriteLine(" = new Uint8Array([");
			WriteChar('\t');
			WriteBytes(resources[name]);
			WriteLine(" ]);");
		}
		WriteLine();
		CloseBlock();
	}

	public override void WriteProgram(CiProgram program)
	{
		CreateFile(this.OutputFile);
		WriteLine();
		WriteLine("\"use strict\";");
		WriteTopLevelNatives(program);
		WriteTypes(program);
		WriteLib(program.Resources);
		CloseFile();
	}
}

}
