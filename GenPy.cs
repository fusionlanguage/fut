// GenPy.cs - Python code generator
//
// Copyright (C) 2020  Piotr Fusik
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

public class GenPy : GenBase
{
	bool ChildPass;
	bool SwitchBreak;

	protected override void WriteBanner()
	{
		WriteLine("# Generated automatically with \"cito\". Do not edit.");
	}

	void WritePyDoc(string text)
	{
		foreach (char c in text) {
			if (c == '\n')
				WriteLine();
			else
				Write(c);
		}
	}

	void WritePyDoc(CiDocPara para)
	{
		foreach (CiDocInline inline in para.Children) {
			switch (inline) {
			case CiDocText text:
				WritePyDoc(text.Text);
				break;
			case CiDocCode code:;
				Write('`');
				WritePyDoc(code.Text);
				Write('`');
				break;
			default:
				throw new ArgumentException(inline.GetType().Name);
			}
		}
	}

	void WritePyDoc(CiDocList list)
	{
		WriteLine();
		foreach (CiDocPara item in list.Items) {
			Write(" * ");
			WritePyDoc(item);
			WriteLine();
		}
		WriteLine();
	}

	void WritePyDoc(CiDocBlock block)
	{
		switch (block) {
		case CiDocPara para:
			WritePyDoc(para);
			break;
		case CiDocList list:
			WritePyDoc(list);
			break;
		default:
			throw new ArgumentException(block.GetType().Name);
		}
	}

	void StartPyDoc(CiCodeDoc doc)
	{
		Write("\"\"\"");
		WritePyDoc(doc.Summary);
		if (doc.Details.Length > 0) {
			WriteLine();
			WriteLine();
			foreach (CiDocBlock block in doc.Details)
				WritePyDoc(block);
		}
	}

	void WritePyDoc(CiCodeDoc doc)
	{
		if (doc != null) {
			StartPyDoc(doc);
			WriteLine("\"\"\"");
		}
	}

	void WritePyDoc(CiMethod method)
	{
		if (method.Documentation == null)
			return;
		StartPyDoc(method.Documentation);
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
				WritePyDoc(param.Documentation.Summary);
				WriteLine();
			}
		}
		WriteLine("\"\"\"");
	}

	protected override void WriteLiteral(object value)
	{
		switch (value) {
		case null:
			Write("None");
			break;
		case bool b:
			Write(b ? "True" : "False");
			break;
		default:
			base.WriteLiteral(value);
			break;
		}
	}

	void WriteNameNotKeyword(string name)
	{
		switch (name) {
		case "this":
			Write("self");
			break;
		case "and":
		case "as":
		case "async":
		case "await":
		case "def":
		case "del":
		case "elif":
		case "except":
		case "finally":
		case "from":
		case "global":
		case "import":
		case "is":
		case "lambda":
		case "len":
		case "nonlocal":
		case "not":
		case "or":
		case "pass":
		case "raise":
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

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiMember member) {
			if (member.IsStatic())
				WriteName(this.CurrentMethod.Parent);
			else
				Write("self");
			Write('.');
		}
		WriteName(symbol);
	}

	public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent)
	{
		Write("f\"");
		foreach (CiInterpolatedPart part in expr.Parts) {
			foreach (char c in part.Prefix) {
				if (c == '{')
					Write("{{");
				else
					WriteEscapedChar(c);
			}
			if (part.Argument != null) {
				Write('{');
				part.Argument.Accept(this, CiPriority.Statement);
				if (part.WidthExpr != null || part.Precision >= 0 || (part.Format != ' ' && part.Format != 'D'))
					Write(':');
				if (part.WidthExpr != null) {
					if (part.Width >= 0) {
						if (!(part.Argument.Type is CiNumericType))
							Write('>');
						Write(part.Width);
					}
					else {
						Write('<');
						Write(-part.Width);
					}
				}
				if (part.Precision >= 0) {
					Write(part.Argument.Type is CiIntegerType ? '0' : '.');
					Write(part.Precision);
				}
				if (part.Format != ' ' && part.Format != 'D')
					Write(part.Format);
				Write('}');
			}
		}
		Write('"');
		return expr;
	}

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			expr.Inner.Accept(this, parent);
			return expr;
		case CiToken.ExclamationMark:
			Write("not ");
			expr.Inner.Accept(this, CiPriority.Primary);
			return expr;
		default:
			return base.Visit(expr, parent);
		}
	}

	public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			expr.Inner.Accept(this, parent);
			return expr;
		default:
			return base.Visit(expr, parent);
		}
	}

	static bool IsPtr(CiExpr expr) => expr.Type is CiClassPtrType || expr.Type is CiArrayPtrType;

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		string op = IsPtr(expr.Left) || IsPtr(expr.Right)
			? not ? " is not " : " is "
			: not ? " != " : " == ";
		WriteComparison(expr, parent, CiPriority.Equality, op);
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		Write("ord(");
		WriteIndexing(expr, CiPriority.Statement);
		Write(')');
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		Write("len(");
		expr.Accept(this, CiPriority.Statement);
		Write(')');
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Symbol == CiSystem.CollectionCount) {
			WriteStringLength(expr.Left);
			return expr;
		}
		return base.Visit(expr, parent);
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
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
						Visit(rightBinary, CiPriority.Statement);
						WriteLine();
						break;
					}
				}
			}
			expr.Left.Accept(this, CiPriority.Assign);
			Write(" = ");
			{
				(expr.Right is CiBinaryExpr rightBinary && rightBinary.IsAssign && rightBinary.Op != CiToken.Assign? rightBinary.Left /* TODO: side effect*/ : expr.Right).Accept(this, CiPriority.Assign);
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
					Visit(rightBinary, CiPriority.Statement);
					WriteLine();
					right = rightBinary.Left; // TODO: side effect
				}
				expr.Left.Accept(this, CiPriority.Assign);
				Write(' ');
				if (expr.Op == CiToken.DivAssign && expr.Type is CiIntegerType)
					Write('/');
				Write(expr.OpString);
				Write(' ');
				right.Accept(this, CiPriority.Statement);
			}
			return expr;
		default:
			return base.Visit(expr, parent);
		}
	}

	protected override void WriteCoerced(CiType type, CiCondExpr expr, CiPriority parent)
	{
		if (parent > CiPriority.Cond)
			Write('(');
		WriteCoerced(type, expr.OnTrue, CiPriority.Cond);
		Write(" if ");
		expr.Cond.Accept(this, CiPriority.Cond);
		Write(" else ");
		WriteCoerced(type, expr.OnFalse, CiPriority.Cond);
		if (parent > CiPriority.Cond)
			Write(')');
	}

	protected override void WriteNew(CiClass klass, CiPriority parent)
	{
		WriteName(klass);
		Write("()");
	}

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		Write("[ ");
		WriteCoercedLiterals(null, expr.Items);
		Write(" ]");
		return expr;
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
			lengthExpr.Accept(this, CiPriority.Statement);
			Write(") ]");
			break;
		case CiNumericType number:
			char c = GetArrayCode(number);
			if (c == 'B' && (value == null || (value is CiLiteral literal && (long) literal.Value == 0))) {
				Write("bytearray(");
				lengthExpr.Accept(this, CiPriority.Statement);
				Write(')');
			}
			else {
				Include("array");
				Write("array.array(\"");
				Write(GetArrayCode(number));
				Write("\", [ ");
				if (value == null)
					Write('0');
				else
					value.Accept(this, CiPriority.Statement);
				Write(" ]) * ");
				lengthExpr.Accept(this, CiPriority.Mul);
			}
			break;
		default:
			Write("[ ");
			if (value == null)
				WriteDefaultValue(elementType);
			else
				value.Accept(this, CiPriority.Statement);
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

	protected override void WriteListStorageInit(CiListType list)
	{
		if (list.ElementType is CiNumericType number) {
			char c = GetArrayCode(number);
			if (c == 'B')
				Write(" = bytearray()");
			else {
				Include("array");
				Write(" = array.array(\"");
				Write(c);
				Write("\")");
			}
		}
		else
			Write(" = []");
	}

	protected override void WriteSortedDictionaryStorageInit(CiSortedDictionaryType dict)
	{
		Write(" = {}");
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Value == null && def.Type.IsDynamicPtr)
			Write(" = None");
		else
			base.WriteVarInit(def);
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
	}

	void WriteConsoleWrite(CiExpr obj, CiExpr[] args, bool newLine)
	{
		Write("print(");
		if (args.Length == 1) {
			args[0].Accept(this, CiPriority.Statement);
			if (!newLine)
				Write(", end=\"\"");
		}
		if (obj.IsReferenceTo(CiSystem.ConsoleError)) {
			if (args.Length == 1)
				Write(", ");
			Include("sys");
			Write("file=sys.stderr");
		}
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (method == CiSystem.StringContains) {
			args[0].Accept(this, CiPriority.Primary);
			Write(" in ");
			obj.Accept(this, CiPriority.Primary);
		}
		else if (method == CiSystem.StringSubstring) {
			obj.Accept(this, CiPriority.Primary);
			Write('[');
			args[0].Accept(this, CiPriority.Statement);
			Write(':');
			if (args.Length == 2)
				WriteAdd(args[0], args[1]); // TODO: side effect
			Write(']');
		}
		else if (obj.Type is CiListType list && method == CiSystem.CollectionClear && list.ElementType is CiNumericType number && GetArrayCode(number) != 'B') {
			Write("del ");
			obj.Accept(this, CiPriority.Primary);
			Write("[:]");
		}
		else if (method == CiSystem.ListRemoveAt) {
			Write("del ");
			obj.Accept(this, CiPriority.Primary);
			Write('[');
			args[0].Accept(this, CiPriority.Statement);
			Write(']');
		}
		else if (method == CiSystem.ListRemoveRange) {
			Write("del ");
			obj.Accept(this, CiPriority.Primary);
			Write('[');
			args[0].Accept(this, CiPriority.Statement);
			Write(':');
			WriteAdd(args[0], args[1]); // TODO: side effect
			Write(']');
		}
		else if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			args[1].Accept(this, CiPriority.Primary);
			Write('[');
			args[2].Accept(this, CiPriority.Statement);
			Write(':');
			WriteAdd(args[2], args[3]); // TODO: side effect
			Write("] = ");
			obj.Accept(this, CiPriority.Primary);
			Write('[');
			args[0].Accept(this, CiPriority.Statement);
			Write(':');
			WriteAdd(args[0], args[3]); // TODO: side effect twice
			Write(']');
		}
		else if (obj.Type is CiArrayStorageType array && method.Name == "Fill") {
			obj.Accept(this, CiPriority.Primary);
			Write("[:] = ");
			WriteNewArray(array.ElementType, args[0], array.LengthExpr);
		}
		else if (obj.Type is CiSortedDictionaryType && method.Name == "ContainsKey") {
			args[0].Accept(this, CiPriority.Primary);
			Write(" in ");
			obj.Accept(this, CiPriority.Primary);
		}
		else if (obj.Type is CiSortedDictionaryType && method.Name == "Remove") {
			Write("del ");
			obj.Accept(this, CiPriority.Primary);
			Write('[');
			args[0].Accept(this, CiPriority.Statement);
			Write(']');
		}
		else if (method == CiSystem.ConsoleWrite)
			WriteConsoleWrite(obj, args, false);
		else if (method == CiSystem.ConsoleWriteLine)
			WriteConsoleWrite(obj, args, true);
		else if (method == CiSystem.UTF8GetString) {
			args[0].Accept(this, CiPriority.Primary);
			Write('[');
			args[1].Accept(this, CiPriority.Statement);
			Write(':');
			WriteAdd(args[1], args[2]); // TODO: side effect
			Write("].decode(\"utf-8\")");
		}
		else if (method == CiSystem.MathFusedMultiplyAdd) {
			Include("pyfma");
			Write("pyfma.fma");
			WriteArgsInParentheses(method, args);
		}
		else if (obj.IsReferenceTo(CiSystem.BasePtr)) {
			WriteName(method.Parent);
			Write('.');
			WriteName(method);
			Write("(self");
			if (args.Length > 0) {
				Write(", ");
				WriteArgs(method, args);
			}
			Write(')');
		}
		else {
			if (obj.IsReferenceTo(CiSystem.MathClass)) {
				Include("math");
				Write("math");
			}
			else
				obj.Accept(this, CiPriority.Primary);
			Write('.');
			if (method == CiSystem.StringIndexOf)
				Write("find");
			else if (method == CiSystem.StringLastIndexOf)
				Write("rfind");
			else if (method == CiSystem.StringStartsWith)
				Write("startswith");
			else if (method == CiSystem.StringEndsWith)
				Write("endswith");
			else if (obj.Type is CiListType && method.Name == "Add")
				Write("append");
			else if (method == CiSystem.MathCeiling)
				Write("ceil");
			else if (method == CiSystem.MathTruncate)
				Write("trunc");
			else
				WriteName(method);
			WriteArgsInParentheses(method, args);
		}
	}

	protected override void WriteNearCall(CiMethod method, CiExpr[] args)
	{
		WriteLocalName(method, CiPriority.Primary);
		WriteArgsInParentheses(method, args);
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("_CiResource.");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	bool VisitXcrement<T>(CiExpr expr, bool write) where T : CiUnaryExpr
	{
		bool seen;
		switch (expr) {
		case CiVar def:
			return def.Value != null && VisitXcrement<T>(def.Value, write);
		case CiLiteral literal:
			return false;
		case CiInterpolatedString interp:
			seen = false;
			foreach (CiInterpolatedPart part in interp.Parts) {
				if (part.Argument != null)
					seen |= VisitXcrement<T>(part.Argument, write);
			}
			return seen;
		case CiSymbolReference symbol:
			return symbol.Left != null && VisitXcrement<T>(symbol.Left, write);
		case CiUnaryExpr unary:
			seen = VisitXcrement<T>(unary.Inner, write);
			if ((unary.Op == CiToken.Increment || unary.Op == CiToken.Decrement) && unary is T) {
				if (write) {
					unary.Inner.Accept(this, CiPriority.Assign);
					WriteLine(unary.Op == CiToken.Increment ? " += 1" : " -= 1");
				}
				seen = true;
			}
			return seen;
		case CiBinaryExpr binary:
			seen = VisitXcrement<T>(binary.Left, write);
			// XXX: assert not seen on the right side of CondAnd, CondOr
			seen |= VisitXcrement<T>(binary.Right, write);
			return seen;
		case CiCondExpr cond:
			seen = VisitXcrement<T>(cond.Cond, write);
			// XXX: assert not seen in OnTrue and OnFalse
			// seen |= VisitXcrement<T>(cond.OnTrue, write);
			// seen |= VisitXcrement<T>(cond.OnFalse, write);
			return seen;
		case CiCallExpr call:
			seen = VisitXcrement<T>(call.Method, write);
			foreach (CiExpr item in call.Arguments)
				seen |= VisitXcrement<T>(item, write);
			return seen;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	static bool NeedsInit(CiNamedValue def)
		=> def.Value != null || def.Type.IsFinal || def.Type.IsDynamicPtr;

	public override void Visit(CiExpr statement)
	{
		if (!(statement is CiVar def) || NeedsInit(def)) {
			VisitXcrement<CiPrefixExpr>(statement, true);
			if (!(statement is CiUnaryExpr unary) || (unary.Op != CiToken.Increment && unary.Op != CiToken.Decrement)) {
				statement.Accept(this, CiPriority.Statement);
				WriteLine();
			}
			VisitXcrement<CiPostfixExpr>(statement, true);
		}
	}

	public override void Visit(CiBlock statement)
	{
		Write(statement.Statements);
	}

	protected override void StartLine()
	{
		base.StartLine();
		this.ChildPass = false;
	}

	void OpenChild()
	{
		WriteLine(':');
		this.Indent++;
		this.ChildPass = true;
	}

	void CloseChild()
	{
		if (this.ChildPass)
			WriteLine("pass");
		this.Indent--;
	}

	protected override void WriteChild(CiStatement statement)
	{
		OpenChild();
		statement.Accept(this);
		CloseChild();
	}

	public override void Visit(CiAssert statement)
	{
		Write("assert ");
		statement.Cond.Accept(this, CiPriority.Statement);
		if (statement.Message != null) {
			Write(", ");
			statement.Message.Accept(this, CiPriority.Statement);
		}
		WriteLine();
	}

	public override void Visit(CiBreak statement)
	{
		WriteLine(statement.LoopOrSwitch is CiSwitch ? "raise _CiBreak()" : "break");
	}

	bool OpenCond(string statement, CiExpr cond, CiPriority parent)
	{
		VisitXcrement<CiPrefixExpr>(cond, true);
		Write(statement);
		cond.Accept(this, parent);
		OpenChild();
		return VisitXcrement<CiPostfixExpr>(cond, true);
	}

	void EndBody(CiFor statement)
	{
		if (statement.Advance != null)
			statement.Advance.Accept(this);
		if (statement.Cond != null)
			VisitXcrement<CiPrefixExpr>(statement.Cond, true);
	}

	public override void Visit(CiContinue statement)
	{
		switch (statement.Loop) {
		case CiDoWhile doWhile:
			OpenCond("if ", doWhile.Cond, CiPriority.Statement);
			WriteLine("continue");
			CloseChild();
			VisitXcrement<CiPostfixExpr>(doWhile.Cond, true);
			WriteLine("break");
			return;
		case CiFor forLoop when !forLoop.IsRange:
			EndBody(forLoop);
			break;
		case CiWhile whileLoop:
			VisitXcrement<CiPrefixExpr>(whileLoop.Cond, true);
			break;
		default:
			break;
		}
		WriteLine("continue");
	}

	public override void Visit(CiDoWhile statement)
	{
		Write("while True");
		OpenChild();
		statement.Body.Accept(this);
		OpenCond("if not ", statement.Cond, CiPriority.Primary);
		WriteLine("break");
		CloseChild();
		VisitXcrement<CiPostfixExpr>(statement.Cond, true);
		this.Indent--;
	}

	void WriteInclusiveLimit(CiExpr limit, int increment, string incrementString)
	{
		if (limit is CiLiteral literal)
			Write((long) literal.Value + increment);
		else {
			limit.Accept(this, CiPriority.Add);
			Write(incrementString);
		}
	}

	void CloseWhile(CiLoop loop)
	{
		CloseChild();
		if (loop.Cond != null && VisitXcrement<CiPostfixExpr>(loop.Cond, false)) {
			if (loop.HasBreak) {
				Write("else");
				OpenChild();
				VisitXcrement<CiPostfixExpr>(loop.Cond, true);
				CloseChild();
			}
			else
				VisitXcrement<CiPostfixExpr>(loop.Cond, true);
		}
	}

	public override void Visit(CiFor statement)
	{
		if (statement.IsRange) {
			CiVar iter = (CiVar) statement.Init;
			Write("for ");
			WriteName(iter);
			Write(" in range(");
			if (statement.RangeStep != 1 || !(iter.Value is CiLiteral start) || (long) start.Value != 0) {
				iter.Value.Accept(this, CiPriority.Statement);
				Write(", ");
			}
			CiBinaryExpr cond = (CiBinaryExpr) statement.Cond;
			switch (cond.Op) {
			case CiToken.Less:
			case CiToken.Greater:
				cond.Right.Accept(this, CiPriority.Statement);
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
			if (statement.RangeStep != 1) {
				Write(", ");
				Write(statement.RangeStep);
			}
			Write(')');
			WriteChild(statement.Body);
			return;
		}

		if (statement.Init != null)
			statement.Init.Accept(this);
		if (statement.Cond != null)
			OpenCond("while ", statement.Cond, CiPriority.Statement);
		else {
			Write("while True");
			OpenChild();
		}
		statement.Body.Accept(this);
		EndBody(statement);
		CloseWhile(statement);
	}

	public override void Visit(CiForeach statement)
	{
		Write("for ");
		WriteName(statement.Element);
		Write(" in ");
		statement.Collection.Accept(this, CiPriority.Statement);
		WriteChild(statement.Body);
	}

	public override void Visit(CiIf statement)
	{
		bool condPostXcrement = OpenCond("if ", statement.Cond, CiPriority.Statement);
		statement.OnTrue.Accept(this);
		CloseChild();
		if (statement.OnFalse == null && condPostXcrement && !statement.OnTrue.CompletesNormally)
			VisitXcrement<CiPostfixExpr>(statement.Cond, true);
		else if (statement.OnFalse != null || condPostXcrement) {
			Write("el");
			if (!condPostXcrement && statement.OnFalse is CiIf childIf && !VisitXcrement<CiPrefixExpr>(childIf.Cond, false))
				Visit(childIf);
			else {
				Write("se");
				OpenChild();
				VisitXcrement<CiPostfixExpr>(statement.Cond, true);
				if (statement.OnFalse != null)
					statement.OnFalse.Accept(this);
				CloseChild();
			}
		}
	}

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return");
		else {
			VisitXcrement<CiPrefixExpr>(statement.Value, true);
			if (VisitXcrement<CiPostfixExpr>(statement.Value, false)) {
				Write("result = "); // FIXME: name clash? only matters if return ... result++, unlikely
				statement.Value.Accept(this, CiPriority.Statement);
				WriteLine();
				VisitXcrement<CiPostfixExpr>(statement.Value, true);
				WriteLine("return result");
			}
			else {
				Write("return ");
				statement.Value.Accept(this, CiPriority.Statement);
				WriteLine();
			}
		}
	}

	static bool IsVarReference(CiExpr expr) => expr is CiSymbolReference symbol && symbol.Symbol is CiVar;

	static int LengthWithoutTrailingBreak(CiStatement[] body)
	{
		int length = body.Length;
		if (length > 0 && body[length - 1] is CiBreak)
			length--;
		return length;
	}

	static bool HasBreak(CiStatement statement)
	{
		switch (statement) {
		case CiBreak brk:
			return true;
		case CiIf ifStatement:
			return HasBreak(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasBreak(ifStatement.OnFalse));
		case CiBlock block:
			return block.Statements.Any(HasBreak);
		default:
			return false;
		}
	}

	static bool HasEarlyBreak(CiStatement[] body)
	{
		int length = LengthWithoutTrailingBreak(body);
		for (int i = 0; i < length; i++) {
			if (HasBreak(body[i]))
				return true;
		}
		return false;
	}

	void WritePyCaseBody(CiStatement[] body)
	{
		OpenChild();
		Write(body, LengthWithoutTrailingBreak(body));
		CloseChild();
	}

	public override void Visit(CiSwitch statement)
	{
		bool earlyBreak = statement.Cases.Any(kase => HasEarlyBreak(kase.Body))
			|| (statement.DefaultBody != null && HasEarlyBreak(statement.DefaultBody));
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
			value.Accept(this, CiPriority.Statement);
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
		if (statement.DefaultBody != null && LengthWithoutTrailingBreak(statement.DefaultBody) > 0) {
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

	public override void Visit(CiThrow statement)
	{
		VisitXcrement<CiPrefixExpr>(statement.Message, true);
		Write("raise Exception(");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine(')');
		// FIXME: WriteXcrement<CiPostfixExpr>(statement.Message);
	}

	public override void Visit(CiWhile statement)
	{
		OpenCond("while ", statement.Cond, CiPriority.Statement);
		statement.Body.Accept(this);
		VisitXcrement<CiPrefixExpr>(statement.Cond, true);
		CloseWhile(statement);
	}

	void Write(CiEnum enu)
	{
		Include("enum");
		WriteLine();
		Write("class ");
		WriteName(enu);
		Write("(enum.Enum)");
		OpenChild();
		WritePyDoc(enu.Documentation);
		int i = 1;
		foreach (CiConst konst in enu) {
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			if (konst.Value != null)
				konst.Value.Accept(this, CiPriority.Statement);
			else
				Write(i);
			WriteLine();
			WritePyDoc(konst.Documentation);
			i++;
		}
		CloseChild();
	}

	void WriteConsts(IEnumerable<CiConst> consts)
	{
		foreach (CiConst konst in consts) {
			if (konst.Visibility != CiVisibility.Private || konst.Type is CiArrayStorageType) {
				WriteLine();
				base.WriteVar(konst);
				WriteLine();
				WritePyDoc(konst.Documentation);
			}
		}
	}

	static bool NeedsConstructor(CiClass klass)
		=> klass.Constructor != null || klass.Fields.Any(NeedsInit);

	static bool InheritsConstructor(CiClass klass)
	{
		while (klass.Parent is CiClass baseClass) {
			if (NeedsConstructor(baseClass))
				return true;
			klass = baseClass;
		}
		return false;
	}

	void Write(CiMethod method)
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
		WritePyDoc(klass.Documentation);
		WriteConsts(klass.Consts);
		if (NeedsConstructor(klass)) {
			WriteLine();
			Write("def __init__(self)");
			OpenChild();
			if (InheritsConstructor(klass)) {
				WriteName(klass.Parent);
				WriteLine(".__init__(self)");
			}
			foreach (CiField field in klass.Fields) {
				if (NeedsInit(field)) {
					Write("self.");
					WriteVar(field);
					WriteLine();
				}
			}
			WriteConstructorBody(klass);
			CloseChild();
		}
		foreach (CiMethod method in klass.Methods)
			Write(method);
		WriteConsts(klass.ConstArrays);
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
