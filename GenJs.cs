// GenJs.cs - JavaScript code generator
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
using System.Linq;

namespace Foxoft.Ci
{

enum GenJsMethod
{
	CopyArray,
	SortListPart,
	RegexEscape,
	Count,
}

public class GenJs : GenBase
{
	readonly Dictionary<CiClass, bool> WrittenClasses = new Dictionary<CiClass, bool>();

	// TODO: Namespace
	protected readonly string[][] Library = new string[(int) GenJsMethod.Count][];

	void WriteCamelCaseNotKeyword(string name)
	{
		switch (name) {
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
			Write(name);
			Write('_');
			break;
		default:
			WriteCamelCase(name);
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
		case CiMember _:
			WriteCamelCaseNotKeyword(symbol.Name);
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
	}

	protected override void WriteTypeAndName(CiNamedValue value) => WriteName(value);

	protected static string GetArrayElementType(CiNumericType type)
	{
		if (type == CiSystem.IntType)
			return "Int32";
		if (type == CiSystem.DoubleType)
			return "Float64";
		if (type == CiSystem.FloatType)
			return "Float32";
		if (type == CiSystem.LongType)
			// TODO: UInt32 if possible?
			return "Float64"; // no 64-bit integers in JavaScript
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

	public override void VisitAggregateInitializer(CiAggregateInitializer expr)
	{
		CiNumericType number = ((CiArrayStorageType) expr.Type).ElementType as CiNumericType;
		if (number != null) {
			Write("new ");
			Write(GetArrayElementType(number));
			Write("Array(");
		}
		Write("[ ");
		WriteCoercedLiterals(null, expr.Items);
		Write(" ]");
		if (number != null)
			Write(')');
	}

	protected override void WriteNewStorage(CiStorageType storage)
	{
		switch (storage.Class.Id) {
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
		default:
			throw new NotImplementedException(storage.Class.Name);
		}
	}

	protected override void WriteVar(CiNamedValue def)
	{
		Write(def.Type.IsFinal && !def.IsAssignableStorage ? "const " : "let ");
		base.WriteVar(def);
	}

	void WriteInterpolatedLiteral(string s)
	{
		for (int i = 0; i < s.Length; i++) {
			char c = s[i];
			if (c == '`'
			 || (c == '$' && i + 1 < s.Length && s[i + 1] == '{'))
				Write('\\');
			Write(c);
		}
	}

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		Write('`');
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
						Write(')');
						break;
					case 'F':
					case 'f':
						Write(".toFixed(");
						if (part.Precision >= 0)
							VisitLiteralLong(part.Precision);
						Write(')');
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
					if (part.Precision >= 0 && "DdXx".IndexOf(part.Format) >= 0) {
						Write(".padStart(");
						VisitLiteralLong(part.Precision);
						Write(", \"0\")");
					}
				}
				if (part.Width > 0) {
					Write(".padStart(");
					VisitLiteralLong(part.Width);
					Write(')');
				}
				else if (part.Width < 0) {
					Write(".padEnd(");
					VisitLiteralLong(-part.Width);
					Write(')');
				}
			}
			else
				part.Argument.Accept(this, CiPriority.Argument);
			Write('}');
		}
		WriteInterpolatedLiteral(expr.Suffix);
		Write('`');
		return expr;
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiMember member) {
			if (!member.IsStatic)
				Write("this");
			else if (this.CurrentMethod != null)
				Write(this.CurrentMethod.Parent.Name);
			else if (symbol is CiConst konst) {
				konst.Value.Accept(this, parent);
				return;
			}
			else
				throw new NotImplementedException(symbol.ToString());
			Write('.');
		}
		WriteName(symbol);
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Write("new ");
		if (elementType is CiNumericType numeric)
			Write(GetArrayElementType(numeric));
		WriteCall("Array", lengthExpr);
	}

	bool HasInitCode(CiNamedValue def)
	{
		return def.Type is CiArrayStorageType array
			&& (array.ElementType is CiClass || array.ElementType is CiArrayStorageType);
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (!HasInitCode(def))
			return;
		CiArrayStorageType array = (CiArrayStorageType) def.Type;
		int nesting = 0;
		while (array.ElementType is CiArrayStorageType innerArray) {
			OpenLoop("let", nesting++, array.Length);
			WriteArrayElement(def, nesting);
			Write(" = ");
			WriteNewArray(innerArray.ElementType, innerArray.LengthExpr, CiPriority.Argument);
			WriteLine(';');
			array = innerArray;
		}
		if (array.ElementType is CiClass klass) {
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
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	public override CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.ListCount:
		case CiId.QueueCount:
		case CiId.StackCount:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".length");
			return expr;
		case CiId.HashSetCount:
		case CiId.OrderedDictionaryCount:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".size");
			return expr;
		case CiId.DictionaryCount:
		case CiId.SortedDictionaryCount:
			WriteCall("Object.keys", expr.Left);
			Write(".length");
			return expr;
		case CiId.MatchStart:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".index");
			return expr;
		case CiId.MatchEnd:
			if (parent > CiPriority.Add)
				Write('(');
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".index + ");
			expr.Left.Accept(this, CiPriority.Primary); // FIXME: side effect
			Write("[0].length");
			if (parent > CiPriority.Add)
				Write(')');
			return expr;
		case CiId.MatchLength:
			expr.Left.Accept(this, CiPriority.Primary);
			Write("[0].length");
			return expr;
		case CiId.MatchValue:
			expr.Left.Accept(this, CiPriority.Primary);
			Write("[0]");
			return expr;
		case CiId.MathNaN:
			Write("NaN");
			return expr;
		case CiId.MathNegativeInfinity:
			Write("-Infinity");
			return expr;
		case CiId.MathPositiveInfinity:
			Write("Infinity");
			return expr;
		default:
			return base.VisitSymbolReference(expr, parent);
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

	void AddLibrary(GenJsMethod id, params string[] method)
	{
		this.Library[(int) id] ??= method;
	}

	static bool IsIdentifier(string s)
	{
		return s.Length > 0
			&& s[0] >= 'A'
			&& s.All(c => CiLexer.IsLetterOrDigit(c));
	}

	void WriteRegex(List<CiExpr> args, int argIndex)
	{
		CiExpr pattern = args[argIndex];
		if (pattern.Type.IsClass(CiSystem.RegexClass))
			pattern.Accept(this, CiPriority.Primary);
		else if (pattern is CiLiteralString literal) {
			Write('/');
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
					Write('\\');
					escaped = false;
				}
				Write(c);
			}
			Write('/');
			WriteRegexOptions(args, "", "", "", "i", "m", "s");
		}
		else {
			Write("new RegExp(");
			pattern.Accept(this, CiPriority.Argument);
			WriteRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
			Write(')');
		}
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.StringContains:
		case CiId.ListContains:
			WriteCall(obj, "includes", args[0]);
			break;
		case CiId.StringSubstring:
			obj.Accept(this, CiPriority.Primary);
			Write(".substring(");
			args[0].Accept(this, CiPriority.Argument);
			if (args.Count == 2) {
				Write(", ");
				WriteAdd(args[0], args[1]); // TODO: side effect
			}
			Write(')');
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
			Write(')');
			break;
		case CiId.ArrayCopyTo:
		case CiId.ListCopyTo:
			AddLibrary(GenJsMethod.CopyArray,
				"copyArray(sa, soffset, da, doffset, length)",
				"if (typeof(sa.subarray) == \"function\" && typeof(da.set) == \"function\")",
				"\tda.set(sa.subarray(soffset, soffset + length), doffset);",
				"else",
				"\tfor (let i = 0; i < length; i++)",
				"\t\tda[doffset + i] = sa[soffset + i];");
			Write("Ci.copyArray(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteArgs(method, args);
			Write(')');
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
			AddLibrary(GenJsMethod.SortListPart,
				"sortListPart(a, offset, length)",
				"const sorted = a.slice(offset, offset + length).sort((a, b) => a - b);",
				"for (let i = 0; i < length; i++)",
				"\ta[offset + i] = sorted[i];");
			WriteCall("Ci.sortListPart", obj, args[0], args[1]);
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
			Write(obj.IsReferenceTo(CiSystem.ConsoleError) ? "console.error" : "console.log");
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
			if (args[2].IsLiteralZero)
				args[1].Accept(this, CiPriority.Argument);
			else {
				args[1].Accept(this, CiPriority.Primary);
				Write(".subarray(");
				args[2].Accept(this, CiPriority.Argument);
				Write(')');
			}
			Write(')');
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
				Write(']');
			}
			break;
		case CiId.RegexCompile:
			WriteRegex(args, 0);
			break;
		case CiId.RegexEscape:
			AddLibrary(GenJsMethod.RegexEscape,
				"regexEscape(s)",
				"return s.replace(/[-\\/\\\\^$*+?.()|[\\]{}]/g, '\\\\$&');");
			Write("Ci.regexEscape");
			WriteArgsInParentheses(method, args);
			break;
		case CiId.RegexIsMatchStr:
			WriteRegex(args, 1);
			WriteCall(".test", args[0]);
			break;
		case CiId.RegexIsMatchRegex:
			WriteCall(obj, "test", args[0]);
			break;
		case CiId.MatchFindStr:
		case CiId.MatchFindRegex:
			if (parent > CiPriority.Equality)
				Write('(');
			Write('(');
			obj.Accept(this, CiPriority.Assign);
			Write(" = ");
			WriteRegex(args, 1);
			WriteCall(".exec", args[0]);
			Write(") != null");
			if (parent > CiPriority.Equality)
				Write(')');
			break;
		case CiId.MatchGetCapture:
			WriteIndexing(obj, args[0]);
			break;
		case CiId.MathCeiling:
			WriteCall("Math.ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			if (parent > CiPriority.Add)
				Write('(');
			args[0].Accept(this, CiPriority.Mul);
			Write(" * ");
			args[1].Accept(this, CiPriority.Mul);
			Write(" + ");
			args[2].Accept(this, CiPriority.Add);
			if (parent > CiPriority.Add)
				Write(')');
			break;
		case CiId.MathIsFinite:
		case CiId.MathIsNaN:
			WriteCamelCase(method.Name);
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathIsInfinity:
			if (parent > CiPriority.Equality)
				Write('(');
			WriteCall("Math.abs", args[0]);
			Write(" == Infinity");
			if (parent > CiPriority.Equality)
				Write(')');
			break;
		case CiId.MathTruncate:
			WriteCall("Math.trunc", args[0]);
			break;
		default:
			if (obj == null)
				WriteLocalName(method, CiPriority.Primary);
			else if (obj.IsReferenceTo(CiSystem.BasePtr)) {
				//TODO: with "class" syntax: Write("super");
				WriteName(method.Parent);
				Write(".prototype.");
				WriteName(method);
				Write(".call(this");
				if (args.Count > 0) {
					Write(", ");
					WriteArgs(method, args);
				}
				Write(')');
				break;
			}
			else {
				obj.Accept(this, CiPriority.Primary);
				Write('.');
				WriteName(method);
			}
			WriteArgsInParentheses(method, args);
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

	protected override string IsOperator => " instanceof ";

	public override CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Type is CiIntegerType) {
			switch (expr.Op) {
			case CiToken.Slash:
				if (parent > CiPriority.Or)
					Write('(');
				expr.Left.Accept(this, CiPriority.Mul);
				Write(" / ");
				expr.Right.Accept(this, CiPriority.Primary);
				Write(" | 0"); // FIXME: long: Math.trunc?
				if (parent > CiPriority.Or)
					Write(')');
				return expr;
			case CiToken.DivAssign:
				if (parent > CiPriority.Assign)
					Write('(');
				expr.Left.Accept(this, CiPriority.Assign);
				Write(" = ");
				expr.Left.Accept(this, CiPriority.Mul); // TODO: side effect
				Write(" / ");
				expr.Right.Accept(this, CiPriority.Primary);
				Write(" | 0");
				if (parent > CiPriority.Assign)
					Write(')');
				return expr;
			default:
				break;
			}
		}
		return base.VisitBinaryExpr(expr, parent);
	}

	public override void VisitAssert(CiAssert statement)
	{
		Write("console.assert(");
		statement.Cond.Accept(this, CiPriority.Argument);
		if (statement.Message != null) {
			Write(", ");
			statement.Message.Accept(this, CiPriority.Argument);
		}
		WriteLine(");");
	}

	public override void VisitForeach(CiForeach statement)
	{
		Write("for (const ");
		CiClassType klass = (CiClassType) statement.Collection.Type;
		switch (klass.Class.Id) {
		case CiId.ArrayStorageClass:
		case CiId.ListClass:
		case CiId.HashSetClass:
			WriteName(statement.Element);
			Write(" of ");
			statement.Collection.Accept(this, CiPriority.Argument);
			break;
		case CiId.DictionaryClass:
		case CiId.SortedDictionaryClass:
		case CiId.OrderedDictionaryClass:
			Write('[');
			WriteName(statement.Element);
			Write(", ");
			WriteName(statement.ValueVar);
			Write("] of ");
			if (klass.Class.Id == CiId.OrderedDictionaryClass)
				statement.Collection.Accept(this, CiPriority.Argument);
			else {
				WriteCall("Object.entries", statement.Collection);
				switch (statement.Element.Type) {
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
					throw new NotImplementedException(statement.Element.Type.ToString());
				}
			}
			break;
		default:
			throw new NotImplementedException(klass.Class.Name);
		}
		Write(')');
		WriteChild(statement.Body);
	}

	public override void VisitLock(CiLock statement)
	{
		throw new NotImplementedException();
	}

	public override void VisitThrow(CiThrow statement)
	{
		Write("throw ");
		statement.Message.Accept(this, CiPriority.Argument);
		WriteLine(';');
	}

	protected virtual void WriteEnum(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		Write("const ");
		Write(enu.Name);
		Write(" = ");
		OpenBlock();
		bool first = true;
		foreach (CiConst konst in enu) {
			if (!first)
				WriteLine(',');
			first = false;
			Write(konst.Documentation);
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" : ");
			VisitLiteralLong(konst.Value.IntValue);
		}
		WriteLine();
		CloseBlock();
	}

	protected override void WriteConst(CiConst konst)
	{
		if (konst.Visibility != CiVisibility.Private || konst.Type is CiArrayStorageType) {
			WriteLine();
			Write(konst.Documentation);
			Write(konst.Parent.Container.Name);
			Write('.');
			base.WriteVar(konst);
			WriteLine(';');
		}
	}

	protected override void WriteField(CiField field)
	{
	}

	protected override void WriteMethod(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		WriteLine();
		WriteDoc(method);
		Write(method.Parent.Name);
		Write('.');
		if (method.CallType != CiCallType.Static)
			Write("prototype.");
		WriteCamelCase(method.Name);
		Write(" = function(");
		WriteParameters(method, true, true);
		WriteBody(method);
	}

	protected virtual void WriteClass(CiClass klass)
	{
		WriteLine();
		Write(klass.Documentation);
		OpenClass(klass, "", " extends ");
		WriteLine("constructor()");
		OpenBlock();
		if (klass.Parent is CiClass)
			WriteLine("super();");
		foreach (CiField field in klass.OfType<CiField>()) {
			if ((field.Value != null || field.Type.IsFinal) && !field.IsAssignableStorage) {
				Write("this.");
				base.WriteVar(field);
				WriteLine(';');
				WriteInitCode(field);
			}
		}
		WriteConstructorBody(klass);
		CloseBlock();
		CloseBlock();
		WriteMembers(klass, true);
	}

	void WriteSortedClass(CiClass klass)
	{
		// topological sorting of class hierarchy
		if (this.WrittenClasses.TryGetValue(klass, out bool done)) {
			if (done)
				return;
			throw new CiException(klass, $"Circular dependency for class {klass.Name}");
		}
		this.WrittenClasses.Add(klass, false);
		if (klass.Parent is CiClass baseClass)
			WriteSortedClass(baseClass);
		this.WrittenClasses[klass] = true;
		WriteClass(klass);
	}

	protected void WriteTypes(CiProgram program)
	{
		foreach (CiContainerType type in program) {
			switch (type) {
			case CiEnum enu:
				WriteEnum(enu);
				break;
			case CiClass klass:
				WriteSortedClass(klass);
				break;
			default:
				throw new NotImplementedException(type.Type.ToString());
			}
		}
	}

	protected void WriteLib(Dictionary<string, byte[]> resources)
	{
		if (this.Library.All(method => method == null) && resources.Count == 0)
			return;

		WriteLine();
		WriteLine("class Ci");
		OpenBlock();
		foreach (string[] method in this.Library) {
			if (method != null) {
				Write("static ");
				WriteLine(method[0]);
				OpenBlock();
				for (int i = 1; i < method.Length; i++)
					WriteLine(method[i]);
				this.Indent--;
				Write('}');
			}
		}

		foreach (string name in resources.Keys.OrderBy(k => k)) {
			Write("static ");
			WriteResource(name, -1);
			WriteLine(" = new Uint8Array([");
			Write('\t');
			Write(resources[name]);
			WriteLine(" ]);");
		}
		WriteLine();
		CloseBlock();
	}

	public override void Write(CiProgram program)
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

