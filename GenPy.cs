// GenPy.cs - Python code generator
//
// Copyright (C) 2020-2022  Piotr Fusik
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

public class GenPy : GenPySwift
{
	bool ChildPass;
	bool SwitchBreak;

	protected override void WriteBanner() => WriteLine("# Generated automatically with \"cito\". Do not edit.");

	protected override void StartDocLine()
	{
	}

	protected override string DocBullet => " * ";

	void StartDoc(CiCodeDoc doc)
	{
		Write("\"\"\"");
		Write(doc.Summary, false);
		if (doc.Details.Count > 0) {
			WriteLine();
			foreach (CiDocBlock block in doc.Details) {
				WriteLine();
				Write(block, false);
			}
		}
	}

	protected override void Write(CiCodeDoc doc)
	{
		if (doc != null) {
			StartDoc(doc);
			WriteLine("\"\"\"");
		}
	}

	void WritePyDoc(CiMethod method)
	{
		if (method.Documentation == null)
			return;
		StartDoc(method.Documentation);
		bool first = true;
		foreach (CiVar param in method.Parameters) {
			if (param.Documentation != null) {
				if (first) {
					WriteLine();
					WriteLine();
					first = false;
				}
				Write(":param ");
				WriteName(param);
				Write(": ");
				Write(param.Documentation.Summary, false);
				WriteLine();
			}
		}
		WriteLine("\"\"\"");
	}

	public override void VisitLiteralNull() => Write("None");

	public override void VisitLiteralFalse() => Write("False");

	public override void VisitLiteralTrue() => Write("True");

	void WriteNameNotKeyword(string name)
	{
		switch (name) {
		case "this":
			Write("self");
			break;
		case "and":
		case "array":
		case "as":
		case "async":
		case "await":
		case "def":
		case "del":
		case "elif":
		case "enum":
		case "except":
		case "finally":
		case "from":
		case "global":
		case "import":
		case "is":
		case "lambda":
		case "len":
		case "math":
		case "nonlocal":
		case "not":
		case "or":
		case "pass":
		case "pyfma":
		case "raise":
		case "re":
		case "sys":
		case "try":
		case "with":
		case "yield":
			Write(name);
			Write('_');
			break;
		default:
			WriteLowercaseWithUnderscores(name);
			break;
		}
	}

	protected override void WriteName(CiSymbol symbol)
	{
		switch (symbol) {
		case CiContainerType container:
			if (!container.IsPublic)
				Write('_');
			Write(symbol.Name);
			break;
		case CiConst konst:
			if (konst.Visibility != CiVisibility.Public)
				Write('_');
			if (konst.InMethod != null) {
				WriteUppercaseWithUnderscores(konst.InMethod.Name);
				Write('_');
			}
			WriteUppercaseWithUnderscores(symbol.Name);
			break;
		case CiVar _:
			WriteNameNotKeyword(symbol.Name);
			break;
		case CiMember member:
			if (member.Visibility == CiVisibility.Public)
				WriteNameNotKeyword(symbol.Name);
			else {
				Write('_');
				WriteLowercaseWithUnderscores(symbol.Name);
			}
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
	}

	protected override void WriteClassName(CiClass klass)
	{
		if (klass == CiSystem.LockClass) {
			Include("threading");
			Write("threading.RLock");
		}
		else
			base.WriteClassName(klass);
	}

	protected override void WriteTypeAndName(CiNamedValue value) => WriteName(value);

	public override void VisitAggregateInitializer(CiAggregateInitializer expr)
	{
		if (((CiArrayStorageType) expr.Type).ElementType is CiNumericType number) {
			char c = GetArrayCode(number);
			if (c == 'B')
				Write("bytes(");
			else {
				Include("array");
				Write("array.array(\"");
				Write(c);
				Write("\", ");
			}
			base.VisitAggregateInitializer(expr);
			Write(')');
		}
		else
			base.VisitAggregateInitializer(expr);
	}

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		Write("f\"");
		foreach (CiInterpolatedPart part in expr.Parts) {
			WriteDoubling(part.Prefix, '{');
			Write('{');
			part.Argument.Accept(this, CiPriority.Argument);
			if (part.WidthExpr != null || part.Precision >= 0 || (part.Format != ' ' && part.Format != 'D'))
				Write(':');
			if (part.WidthExpr != null) {
				if (part.Width >= 0) {
					if (!(part.Argument.Type is CiNumericType))
						Write('>');
					VisitLiteralLong(part.Width);
				}
				else {
					Write('<');
					VisitLiteralLong(-part.Width);
				}
			}
			if (part.Precision >= 0) {
				Write(part.Argument.Type is CiIntegerType ? '0' : '.');
				VisitLiteralLong(part.Precision);
			}
			if (part.Format != ' ' && part.Format != 'D')
				Write(part.Format);
			Write('}');
		}
		WriteDoubling(expr.Suffix, '{');
		Write('"');
		return expr;
	}

	public override CiExpr VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
	{
		if (expr.Op == CiToken.ExclamationMark) {
			if (parent > CiPriority.CondAnd)
				Write('(');
			Write("not ");
			expr.Inner.Accept(this, CiPriority.Or);
			if (parent > CiPriority.CondAnd)
				Write(')');
			return expr;
		}
		return base.VisitPrefixExpr(expr, parent);
	}

	protected override string GetReferenceEqOp(bool not) => not ? " is not " : " is ";

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		Write("ord(");
		WriteIndexing(expr, CiPriority.Argument);
		Write(')');
	}

	protected override void WriteStringLength(CiExpr expr) => WriteCall("len", expr);

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
			WriteStringLength(expr.Left);
			return expr;
		case CiId.MathNaN:
			Include("math");
			Write("math.nan");
			return expr;
		case CiId.MathNegativeInfinity:
			Include("math");
			Write("-math.inf");
			return expr;
		case CiId.MathPositiveInfinity:
			Include("math");
			Write("math.inf");
			return expr;
		default:
			if (WriteJavaMatchProperty(expr, parent))
				return expr;
			return base.VisitSymbolReference(expr, parent);
		}
	}

	public override CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Slash when expr.Type is CiIntegerType:
			bool floorDiv;
			if (expr.Left is CiRangeType leftRange && leftRange.Min >= 0
			 && expr.Right is CiRangeType rightRange && rightRange.Min >= 0) {
				if (parent > CiPriority.Or)
					Write('(');
				floorDiv = true;
			}
			else {
				Write("int(");
				floorDiv = false;
			}
			expr.Left.Accept(this, CiPriority.Mul);
			Write(floorDiv ? " // " : " / ");
			expr.Right.Accept(this, CiPriority.Primary);
			if (!floorDiv || parent > CiPriority.Or)
				Write(')');
			return expr;

		case CiToken.CondAnd:
			return Write(expr, parent > CiPriority.CondAnd || parent == CiPriority.CondOr, CiPriority.CondAnd, " and ", CiPriority.CondAnd);
		case CiToken.CondOr:
			return Write(expr, parent, CiPriority.CondOr, " or ");

		case CiToken.Assign:
			if (this.AtLineStart) {
				for (CiExpr right = expr.Right; right is CiBinaryExpr rightBinary && rightBinary.IsAssign; right = rightBinary.Right) {
					if (rightBinary.Op != CiToken.Assign) {
						VisitBinaryExpr(rightBinary, CiPriority.Statement);
						WriteLine();
						break;
					}
				}
			}
			expr.Left.Accept(this, CiPriority.Assign);
			Write(" = ");
			{
				(expr.Right is CiBinaryExpr rightBinary && rightBinary.IsAssign && rightBinary.Op != CiToken.Assign ? rightBinary.Left /* TODO: side effect*/ : expr.Right).Accept(this, CiPriority.Assign);
			}
			return expr;
		case CiToken.AddAssign:
		case CiToken.SubAssign:
		case CiToken.MulAssign:
		case CiToken.DivAssign:
		case CiToken.ModAssign:
		case CiToken.ShiftLeftAssign:
		case CiToken.ShiftRightAssign:
		case CiToken.AndAssign:
		case CiToken.OrAssign:
		case CiToken.XorAssign:
			{
				CiExpr right = expr.Right;
				if (right is CiBinaryExpr rightBinary && rightBinary.IsAssign) {
					VisitBinaryExpr(rightBinary, CiPriority.Statement);
					WriteLine();
					right = rightBinary.Left; // TODO: side effect
				}
				expr.Left.Accept(this, CiPriority.Assign);
				Write(' ');
				if (expr.Op == CiToken.DivAssign && expr.Type is CiIntegerType)
					Write('/');
				Write(expr.OpString);
				Write(' ');
				right.Accept(this, CiPriority.Argument);
			}
			return expr;

		case CiToken.Is:
			Write("isinstance(");
			expr.Left.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteName((CiClass) expr.Right);
			Write(')');
			return expr;

		default:
			return base.VisitBinaryExpr(expr, parent);
		}
	}

	protected override void WriteCoerced(CiType type, CiSelectExpr expr, CiPriority parent)
	{
		if (parent > CiPriority.Select)
			Write('(');
		WriteCoerced(type, expr.OnTrue, CiPriority.Select);
		Write(" if ");
		expr.Cond.Accept(this, CiPriority.Select);
		Write(" else ");
		WriteCoerced(type, expr.OnFalse, CiPriority.Select);
		if (parent > CiPriority.Select)
			Write(')');
	}

	static char GetArrayCode(CiNumericType type)
	{
		if (type == CiSystem.IntType)
			return 'i';
		if (type == CiSystem.LongType)
			return 'q';
		if (type == CiSystem.FloatType)
			return 'f';
		if (type == CiSystem.DoubleType)
			return 'd';
		CiRangeType range = (CiRangeType) type;
		if (range.Min < 0) {
			if (range.Min < short.MinValue || range.Max > short.MaxValue)
				return 'i';
			if (range.Min < sbyte.MinValue || range.Max > sbyte.MaxValue)
				return 'h';
			return 'b';
		}
		if (range.Max > ushort.MaxValue)
			return 'i';
		if (range.Max > byte.MaxValue)
			return 'H';
		return 'B';
	}

	void WriteDefaultValue(CiType type)
	{
		if (type is CiNumericType)
			Write('0');
		else if (type == CiSystem.BoolType)
			Write("False");
		else if (type == CiSystem.StringStorageType)
			Write("\"\"");
		else
			Write("None");
	}

	void WriteNewArray(CiType elementType, CiExpr value, CiExpr lengthExpr)
	{
		switch (elementType) {
		case CiClass _:
		case CiArrayStorageType _:
			Write("[ ");
			WriteNewStorage(elementType);
			Write(" for _ in range(");
			lengthExpr.Accept(this, CiPriority.Argument);
			Write(") ]");
			break;
		case CiNumericType number:
			char c = GetArrayCode(number);
			if (c == 'B' && (value == null || value.IsLiteralZero))
				WriteCall("bytearray", lengthExpr);
			else {
				Include("array");
				Write("array.array(\"");
				Write(c);
				Write("\", [ ");
				if (value == null)
					Write('0');
				else
					value.Accept(this, CiPriority.Argument);
				Write(" ]) * ");
				lengthExpr.Accept(this, CiPriority.Mul);
			}
			break;
		default:
			Write("[ ");
			if (value == null)
				WriteDefaultValue(elementType);
			else
				value.Accept(this, CiPriority.Argument);
			Write(" ] * ");
			lengthExpr.Accept(this, CiPriority.Mul);
			break;
		}
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		WriteNewArray(elementType, null, lengthExpr);
	}

	protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		Write(" = ");
		WriteNewArray(array.ElementType, null, array.LengthExpr);
	}

	protected override void WriteNewStorage(CiStorageType storage)
	{
		if (storage.Class == CiSystem.ListClass || storage.Class == CiSystem.StackClass) {
			if (storage.ElementType is CiNumericType number) {
				char c = GetArrayCode(number);
				if (c == 'B')
					Write("bytearray()");
				else {
					Include("array");
					Write("array.array(\"");
					Write(c);
					Write("\")");
				}
			}
			else
				Write("[]");
		}
		else if (storage.Class == CiSystem.QueueClass) {
			Include("collections");
			Write("collections.deque()");
		}
		else if (storage.Class == CiSystem.HashSetClass)
			Write("set()");
		else if (storage.Class == CiSystem.DictionaryClass || storage.Class == CiSystem.SortedDictionaryClass)
			Write("{}");
		else if (storage.Class == CiSystem.OrderedDictionaryClass) {
			Include("collections");
			Write("collections.OrderedDict()");
		}
		else
			throw new NotImplementedException();
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
	}

	void WriteSlice(CiExpr startIndex, CiExpr length)
	{
		Write('[');
		startIndex.Accept(this, CiPriority.Argument);
		Write(':');
		if (length != null)
			WriteAdd(startIndex, length); // TODO: side effect
		Write(']');
	}

	void WriteAssignSorted(CiExpr obj, string byteArray)
	{
		Write(" = ");
		char c = GetArrayCode((CiNumericType) ((CiClassType) obj.Type).ElementType);
		if (c == 'B') {
			Write(byteArray);
			Write('(');
		}
		else {
			Include("array");
			Write("array.array(\"");
			Write(c);
			Write("\", ");
		}
		Write("sorted(");
	}

	void WriteConsoleWrite(CiExpr obj, List<CiExpr> args, bool newLine)
	{
		Write("print(");
		if (args.Count == 1) {
			args[0].Accept(this, CiPriority.Argument);
			if (!newLine)
				Write(", end=\"\"");
		}
		if (obj.IsReferenceTo(CiSystem.ConsoleError)) {
			if (args.Count == 1)
				Write(", ");
			Include("sys");
			Write("file=sys.stderr");
		}
		Write(')');
	}

	void WriteRegexOptions(List<CiExpr> args)
	{
		Include("re");
		WriteRegexOptions(args, ", ", " | ", "", "re.I", "re.M", "re.S");
	}

	void WriteRegexSearch(List<CiExpr> args)
	{
		Write("re.search(");
		args[1].Accept(this, CiPriority.Argument);
		Write(", ");
		args[0].Accept(this, CiPriority.Argument);
		WriteRegexOptions(args);
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.StringContains:
		case CiId.ListContains:
		case CiId.HashSetContains:
		case CiId.DictionaryContainsKey:
		case CiId.SortedDictionaryContainsKey:
		case CiId.OrderedDictionaryContainsKey:
			args[0].Accept(this, CiPriority.Primary);
			Write(" in ");
			obj.Accept(this, CiPriority.Primary);
			break;
		case CiId.StringEndsWith:
			WriteCall(obj, "endswith", args[0]);
			break;
		case CiId.StringIndexOf:
			WriteCall(obj, "find", args[0]);
			break;
		case CiId.StringLastIndexOf:
			WriteCall(obj, "rfind", args[0]);
			break;
		case CiId.StringStartsWith:
			WriteCall(obj, "startswith", args[0]);
			break;
		case CiId.StringSubstring:
			obj.Accept(this, CiPriority.Primary);
			WriteSlice(args[0], args.Count == 2 ? args[1] : null);
			break;
		case CiId.ArrayBinarySearchAll:
		case CiId.ArrayBinarySearchPart:
			Include("bisect");
			WriteCall("bisect.bisect_left", obj, args);
			break;
		case CiId.ArrayCopyTo:
		case CiId.ListCopyTo:
			args[1].Accept(this, CiPriority.Primary);
			WriteSlice(args[2], args[3]);
			Write(" = ");
			obj.Accept(this, CiPriority.Primary);
			WriteSlice(args[0], args[3]);
			break;
		case CiId.ArrayFillAll:
		case CiId.ArrayFillPart:
			obj.Accept(this, CiPriority.Primary);
			if (args.Count == 1) {
				Write("[:] = ");
				CiArrayStorageType array = (CiArrayStorageType) obj.Type;
				WriteNewArray(array.ElementType, args[0], array.LengthExpr);
			}
			else {
				WriteSlice(args[1], args[2]);
				Write(" = ");
				CiClassType klass = (CiClassType) obj.Type;
				WriteNewArray(klass.ElementType, args[0], args[2]); // TODO: side effect
			}
			break;
		case CiId.ArraySortAll:
		case CiId.ListSortAll:
			obj.Accept(this, CiPriority.Assign);
			WriteAssignSorted(obj, "bytearray");
			obj.Accept(this, CiPriority.Argument);
			Write("))");
			break;
		case CiId.ArraySortPart:
		case CiId.ListSortPart:
			obj.Accept(this, CiPriority.Primary);
			WriteSlice(args[0], args[1]);
			WriteAssignSorted(obj, "bytes");
			obj.Accept(this, CiPriority.Primary);
			WriteSlice(args[0], args[1]);
			Write("))");
			break;
		case CiId.ListAdd:
			WriteListAdd(obj, "append", args);
			break;
		case CiId.ListClear:
		case CiId.StackClear:
			if (((CiClassType) obj.Type).ElementType is CiNumericType number && GetArrayCode(number) != 'B') {
				Write("del ");
				obj.Accept(this, CiPriority.Primary);
				Write("[:]");
			}
			else {
				obj.Accept(this, CiPriority.Primary);
				Write(".clear()");
			}
			break;
		case CiId.ListInsert:
			WriteListInsert(obj, "insert", args);
			break;
		case CiId.ListRemoveAt:
		case CiId.DictionaryRemove:
		case CiId.SortedDictionaryRemove:
		case CiId.OrderedDictionaryRemove:
			Write("del ");
			WriteIndexing(obj, args[0]);
			break;
		case CiId.ListRemoveRange:
			Write("del ");
			obj.Accept(this, CiPriority.Primary);
			WriteSlice(args[0], args[1]);
			break;
		case CiId.QueueDequeue:
			obj.Accept(this, CiPriority.Primary);
			Write(".popleft()");
			break;
		case CiId.QueueEnqueue:
		case CiId.StackPush:
			WriteListAppend(obj, args);
			break;
		case CiId.QueuePeek:
			obj.Accept(this, CiPriority.Primary);
			Write("[0]");
			break;
		case CiId.StackPeek:
			obj.Accept(this, CiPriority.Primary);
			Write("[-1]");
			break;
		case CiId.DictionaryAdd:
			WriteDictionaryAdd(obj, args);
			break;
		case CiId.ConsoleWrite:
			WriteConsoleWrite(obj, args, false);
			break;
		case CiId.ConsoleWriteLine:
			WriteConsoleWrite(obj, args, true);
			break;
		case CiId.UTF8GetByteCount:
			Write("len(");
			args[0].Accept(this, CiPriority.Primary);
			Write(".encode(\"utf8\"))");
			break;
		case CiId.UTF8GetBytes:
			Write("cibytes = ");
			args[0].Accept(this, CiPriority.Primary);
			WriteLine(".encode(\"utf8\")");
			args[1].Accept(this, CiPriority.Primary);
			Write('[');
			args[2].Accept(this, CiPriority.Argument);
			Write(':');
			if (!args[2].IsLiteralZero) {
				args[2].Accept(this, CiPriority.Add); // TODO: side effect
				Write(" + ");
			}
			WriteLine("len(cibytes)] = cibytes");
			break;
		case CiId.UTF8GetString:
			args[0].Accept(this, CiPriority.Primary);
			WriteSlice(args[1], args[2]);
			Write(".decode(\"utf8\")");
			break;
		case CiId.EnvironmentGetEnvironmentVariable:
			Include("os");
			WriteCall("os.getenv", args[0]);
			break;
		case CiId.RegexCompile:
			Write("re.compile(");
			args[0].Accept(this, CiPriority.Argument);
			WriteRegexOptions(args);
			Write(')');
			break;
		case CiId.RegexEscape:
			Include("re");
			WriteCall("re.escape", args[0]);
			break;
		case CiId.RegexIsMatchStr:
			if (parent > CiPriority.Equality)
				Write('(');
			WriteRegexSearch(args);
			Write(" is not None");
			if (parent > CiPriority.Equality)
				Write(')');
			break;
		case CiId.RegexIsMatchRegex:
			if (parent > CiPriority.Equality)
				Write('(');
			WriteCall(obj, "search", args[0]);
			Write(" is not None");
			if (parent > CiPriority.Equality)
				Write(')');
			break;
		case CiId.MatchFindStr:
		case CiId.MatchFindRegex:
			if (parent > CiPriority.Equality)
				Write('(');
			obj.Accept(this, CiPriority.Equality);
			Write(" is not None");
			if (parent > CiPriority.Equality)
				Write(')');
			break;
		case CiId.MatchGetCapture:
			WriteCall(obj, "group", args[0]);
			break;
		case CiId.MathMethod:
		case CiId.MathIsFinite:
		case CiId.MathIsNaN:
		case CiId.MathLog2:
			Include("math");
			Write("math.");
			WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathCeiling:
			Include("math");
			WriteCall("math.ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			Include("pyfma");
			WriteCall("pyfma.fma", args[0], args[1], args[2]);
			break;
		case CiId.MathIsInfinity:
			Include("math");
			WriteCall("math.isinf", args[0]);
			break;
		case CiId.MathTruncate:
			Include("math");
			WriteCall("math.trunc", args[0]);
			break;
		default:
			if (obj == null)
				WriteLocalName(method, CiPriority.Primary);
			else if (obj.IsReferenceTo(CiSystem.BasePtr)) {
				WriteName(method.Parent);
				Write('.');
				WriteName(method);
				Write("(self");
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

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("_CiResource.");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override bool VisitPreCall(CiCallExpr call)
	{
		switch (call.Method.Symbol.Id) {
		case CiId.MatchFindStr:
			call.Method.Left.Accept(this, CiPriority.Assign);
			Write(" = ");
			WriteRegexSearch(call.Arguments);
			WriteLine();
			return true;
		case CiId.MatchFindRegex:
			call.Method.Left.Accept(this, CiPriority.Assign);
			Write(" = ");
			WriteCall(call.Arguments[1], "search", call.Arguments[0]);
			WriteLine();
			return true;
		default:
			return false;
		}
	}

	static bool NeedsInit(CiNamedValue def)
		=> (def.Value != null || def.Type.IsFinal) && !def.IsAssignableStorage;

	public override void VisitExpr(CiExpr statement)
	{
		if (!(statement is CiVar def) || NeedsInit(def))
			base.VisitExpr(statement);
	}

	protected override void StartLine()
	{
		base.StartLine();
		this.ChildPass = false;
	}

	protected override void OpenChild()
	{
		WriteLine(':');
		this.Indent++;
		this.ChildPass = true;
	}

	protected override void CloseChild()
	{
		if (this.ChildPass)
			WriteLine("pass");
		this.Indent--;
	}

	public override void VisitAssert(CiAssert statement)
	{
		Write("assert ");
		statement.Cond.Accept(this, CiPriority.Argument);
		if (statement.Message != null) {
			Write(", ");
			statement.Message.Accept(this, CiPriority.Argument);
		}
		WriteLine();
	}

	public override void VisitBreak(CiBreak statement)
	{
		WriteLine(statement.LoopOrSwitch is CiSwitch ? "raise _CiBreak()" : "break");
	}

	protected override string GetIfNot() => "if not ";

	void WriteInclusiveLimit(CiExpr limit, int increment, string incrementString)
	{
		if (limit is CiLiteralLong literal)
			VisitLiteralLong(literal.Value + increment);
		else {
			limit.Accept(this, CiPriority.Add);
			Write(incrementString);
		}
	}

	protected override void WriteForRange(CiVar iter, CiBinaryExpr cond, long rangeStep)
	{
		Write("range(");
		if (rangeStep != 1 || !iter.Value.IsLiteralZero) {
			iter.Value.Accept(this, CiPriority.Argument);
			Write(", ");
		}
		switch (cond.Op) {
		case CiToken.Less:
		case CiToken.Greater:
			cond.Right.Accept(this, CiPriority.Argument);
			break;
		case CiToken.LessOrEqual:
			WriteInclusiveLimit(cond.Right, 1, " + 1");
			break;
		case CiToken.GreaterOrEqual:
			WriteInclusiveLimit(cond.Right, -1, " - 1");
			break;
		default:
			throw new NotImplementedException(cond.Op.ToString());
		}
		if (rangeStep != 1) {
			Write(", ");
			VisitLiteralLong(rangeStep);
		}
		Write(')');
	}

	public override void VisitForeach(CiForeach statement)
	{
		Write("for ");
		WriteName(statement.Element);
		if (statement.Collection.Type is CiClassType dict && dict.Class.TypeParameterCount == 2) {
			Write(", ");
			WriteName(statement.ValueVar);
			Write(" in ");
			if (dict.Class == CiSystem.SortedDictionaryClass) {
				Write("sorted(");
				statement.Collection.Accept(this, CiPriority.Primary);
				Write(".items())");
			}
			else {
				statement.Collection.Accept(this, CiPriority.Primary);
				Write(".items()");
			}
		}
		else {
			Write(" in ");
			statement.Collection.Accept(this, CiPriority.Argument);
		}
		WriteChild(statement.Body);
	}

	protected override void WriteElseIf() => Write("el");

	public override void VisitLock(CiLock statement)
	{
		VisitXcrement<CiPrefixExpr>(statement.Lock, true);
		Write("with ");
		statement.Lock.Accept(this, CiPriority.Argument);
		OpenChild();
		VisitXcrement<CiPostfixExpr>(statement.Lock, true);
		statement.Body.Accept(this);
		CloseChild();
	}

	protected override void WriteResultVar() => Write("result");

	static bool IsVarReference(CiExpr expr) => expr is CiSymbolReference symbol && symbol.Symbol is CiVar;

	void WritePyCaseBody(List<CiStatement> body)
	{
		OpenChild();
		Write(body, CiSwitch.LengthWithoutTrailingBreak(body));
		CloseChild();
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		bool earlyBreak = statement.Cases.Any(kase => CiSwitch.HasEarlyBreak(kase.Body))
			|| CiSwitch.HasEarlyBreak(statement.DefaultBody);
		if (earlyBreak) {
			this.SwitchBreak = true;
			Write("try");
			OpenChild();
		}

		CiExpr value = statement.Value;
		VisitXcrement<CiPrefixExpr>(value, true);
		switch (value) {
		case CiSymbolReference symbol when symbol.Left == null || IsVarReference(symbol.Left):
		case CiPrefixExpr prefix when IsVarReference(prefix.Inner): // ++x, --x, -x, ~x
		case CiBinaryExpr binary when binary.Op == CiToken.LeftBracket && IsVarReference(binary.Left) && binary.Right is CiLiteral:
			break;
		default:
			Write("ci_switch_tmp = ");
			value.Accept(this, CiPriority.Argument);
			WriteLine();
			VisitXcrement<CiPostfixExpr>(value, true);
			value = null;
			break;
		}

		string op = "if ";
		foreach (CiCase kase in statement.Cases) {
			foreach (CiExpr caseValue in kase.Values) {
				Write(op);
				if (value == null)
					Write("ci_switch_tmp");
				else
					value.Accept(this, CiPriority.Equality);
				Write(" == ");
				caseValue.Accept(this, CiPriority.Equality);
				op = " or ";
			}
			WritePyCaseBody(kase.Body);
			op = "elif ";
		}
		if (statement.HasDefault) {
			Write("else");
			WritePyCaseBody(statement.DefaultBody);
		}

		if (earlyBreak) {
			CloseChild();
			Write("except _CiBreak");
			OpenChild();
			CloseChild();
		}
	}

	public override void VisitThrow(CiThrow statement)
	{
		VisitXcrement<CiPrefixExpr>(statement.Message, true);
		Write("raise Exception(");
		statement.Message.Accept(this, CiPriority.Argument);
		WriteLine(')');
		// FIXME: WriteXcrement<CiPostfixExpr>(statement.Message);
	}

	void Write(CiEnum enu)
	{
		Include("enum");
		WriteLine();
		Write("class ");
		WriteName(enu);
		Write(enu is CiEnumFlags ? "(enum.Flag)" : "(enum.Enum)");
		OpenChild();
		Write(enu.Documentation);
		foreach (CiConst konst in enu) {
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			VisitLiteralLong(konst.Value.IntValue);
			WriteLine();
			Write(konst.Documentation);
		}
		CloseChild();
	}

	protected override void WriteConst(CiConst konst)
	{
		if (konst.Visibility != CiVisibility.Private || konst.Type is CiArrayStorageType) {
			WriteLine();
			base.WriteVar(konst);
			WriteLine();
			Write(konst.Documentation);
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
		if (method.CallType == CiCallType.Static)
			WriteLine("@staticmethod");
		Write("def ");
		WriteName(method);
		Write('(');
		if (method.CallType != CiCallType.Static)
			Write("self");
		WriteParameters(method, method.CallType == CiCallType.Static, true);
		this.CurrentMethod = method;
		OpenChild();
		WritePyDoc(method);
		method.Body.Accept(this);
		CloseChild();
		this.CurrentMethod = null;
	}

	static bool NeedsConstructor(CiClass klass) => klass.Constructor != null || klass.OfType<CiField>().Any(NeedsInit);

	static bool InheritsConstructor(CiClass klass)
	{
		while (klass.Parent is CiClass baseClass) {
			if (NeedsConstructor(baseClass))
				return true;
			klass = baseClass;
		}
		return false;
	}

	void Write(CiClass klass)
	{
		WriteLine();
		Write("class ");
		WriteName(klass);
		if (klass.Parent is CiClass baseClass) {
			Write('(');
			WriteName(baseClass);
			Write(')');
		}
		OpenChild();
		Write(klass.Documentation);
		if (NeedsConstructor(klass)) {
			WriteLine();
			Write("def __init__(self)");
			OpenChild();
			if (klass.Constructor != null)
				Write(klass.Constructor.Documentation);
			if (InheritsConstructor(klass)) {
				WriteName(klass.Parent);
				WriteLine(".__init__(self)");
			}
			foreach (CiField field in klass.OfType<CiField>()) {
				if (NeedsInit(field)) {
					Write("self.");
					WriteVar(field);
					WriteLine();
				}
			}
			WriteConstructorBody(klass);
			CloseChild();
		}
		WriteMembers(klass, true);
		CloseChild();
	}

	void WriteResources(Dictionary<string, byte[]> resources)
	{
		if (resources.Count == 0)
			return;
		WriteLine();
		Write("class _CiResource");
		OpenChild();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			WriteResource(name, -1);
			WriteLine(" = (");
			this.Indent++;
			Write("b\"");
			int i = 0;
			foreach (byte b in resources[name]) {
				if (i > 0 && (i & 15) == 0) {
					WriteLine('"');
					Write("b\"");
				}
				Write($"\\x{b:x2}");
				i++;
			}
			WriteLine("\" )");
			this.Indent--;
		}
		CloseChild();
	}

	public override void Write(CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		this.SwitchBreak = false;
		OpenStringWriter();
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes)
			Write(klass);
		CreateFile(this.OutputFile);
		WriteIncludes("import ", "");
		if (this.SwitchBreak) {
			WriteLine();
			WriteLine("class _CiBreak(Exception): pass");
		}
		CloseStringWriter();
		WriteResources(program.Resources);
		CloseFile();
	}
}

}
