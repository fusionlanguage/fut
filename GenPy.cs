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

	protected override void WriteBanner()
	{
		WriteLine("# Generated automatically with \"cito\". Do not edit.");
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
			WriteLowercaseWithUnderscores(symbol.Name);
		else if (symbol.Name == "this")
			Write("self");
		else
			Write(symbol.Name);
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
	}

	protected override void WriteThisForField()
	{
		Write("self.");
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
				if (part.WidthExpr != null) {
					Write(',');
					Write(part.Width);
				}
				if (part.Format != ' ') {
					// TODO
				}
				Write('}');
			}
		}
		Write('"');
		return expr;
	}

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.ExclamationMark:
			Write("not ");
			expr.Inner.Accept(this, CiPriority.Primary);
			return expr;
		default:
			return base.Visit(expr, parent);
		}
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		WriteIndexing(expr, CiPriority.Statement);
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
			if (parent > CiPriority.Or)
				Write('(');
			expr.Left.Accept(this, CiPriority.Mul);
			Write(" // ");
			expr.Right.Accept(this, CiPriority.Primary);
			if (parent > CiPriority.Or)
				Write(')');
			return expr;
		case CiToken.CondAnd:
			return Write(expr, parent > CiPriority.CondAnd || parent == CiPriority.CondOr, CiPriority.CondAnd, " and ", CiPriority.CondAnd);
		case CiToken.CondOr:
			return Write(expr, parent, CiPriority.CondOr, " or ");
		case CiToken.DivAssign when expr.Type is CiIntegerType:
			if (parent > CiPriority.Assign)
				Write('(');
			expr.Left.Accept(this, CiPriority.Assign);
			Write(" //= ");
			expr.Right.Accept(this, CiPriority.Statement);
			if (parent > CiPriority.Assign)
				Write(')');
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
		Write(klass.Name);
		Write("()");
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
		Write("[ ");
		if (value == null)
			WriteDefaultValue(elementType); // TODO: storage
		else
			value.Accept(this, CiPriority.Statement);
		Write(" ] * ");
		lengthExpr.Accept(this, CiPriority.Mul);
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
		Write(" = []");
	}

	protected override void WriteSortedDictionaryStorageInit(CiSortedDictionaryType dict)
	{
		Write(" = SortedDict()");
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		// TODO
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (method == CiSystem.ListRemoveAt) {
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
			args[0].Accept(this, CiPriority.Add); // TODO: side effect
			Write(" + ");
			args[1].Accept(this, CiPriority.Add);
			Write(']');
		}
		else if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			args[1].Accept(this, CiPriority.Primary);
			Write('[');
			args[2].Accept(this, CiPriority.Statement);
			Write(':');
			args[2].Accept(this, CiPriority.Add); // TODO: side effect
			Write(" + ");
			args[3].Accept(this, CiPriority.Add);
			Write("] = ");
			obj.Accept(this, CiPriority.Primary);
			Write('[');
			args[0].Accept(this, CiPriority.Statement);
			Write(':');
			args[0].Accept(this, CiPriority.Add); // TODO: side effect
			Write(" + ");
			args[3].Accept(this, CiPriority.Add); // TODO: side effect
			Write(']');
		}
		else if (method == CiSystem.ConsoleWriteLine) {
			Write("print(");
			if (args.Length == 1)
				args[0].Accept(this, CiPriority.Statement);
			if (obj.IsReferenceTo(CiSystem.ConsoleError)) {
				if (args.Length == 1)
					Write(", ");
				Write("file=sys.stderr");
			}
			Write(')');
		}
		else if (method == CiSystem.StringContains) {
			args[0].Accept(this, CiPriority.Primary);
			Write(" in ");
			obj.Accept(this, CiPriority.Primary);
		}
		else if (method == CiSystem.StringSubstring) {
			obj.Accept(this, CiPriority.Primary);
			Write('[');
			args[0].Accept(this, CiPriority.Statement);
			Write(':');
			if (args.Length == 2) {
				args[0].Accept(this, CiPriority.Add); // TODO: side effect
				Write(" + ");
				args[1].Accept(this, CiPriority.Add);
			}
			Write(']');
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
			else if (obj.Type is CiListType list && method.Name == "Add")
				Write("append");
			else if (obj.IsReferenceTo(CiSystem.MathClass) && method.Name == "Ceiling")
				Write("ceil");
			else if (obj.IsReferenceTo(CiSystem.MathClass) && method.Name == "Truncate")
				Write("trunc");
			else
				WriteName(method);
			WriteArgsInParentheses(method, args);
		}
	}

	protected override void WriteNearCall(CiMethod method, CiExpr[] args)
	{
		Write(method.IsStatic() ? this.CurrentMethod.Parent.Name : "self");
		Write('.');
		WriteName(method);
		WriteArgsInParentheses(method, args);
	}

	protected override void WriteResource(string name, int length)
	{
		Write("TODO");
	}

	public override void Visit(CiExpr statement)
	{
		statement.Accept(this, CiPriority.Statement);
		WriteLine();
		if (statement is CiVar def)
			WriteInitCode(def);
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

	public override void Visit(CiBreak statement)
	{
		WriteLine("break");
	}

	static bool IsForInRange(CiFor statement)
	{
		return statement.Init is CiVar iter
			&& iter.Type is CiIntegerType
			&& iter.Value != null
			&& statement.Cond is CiBinaryExpr cond
			&& cond.Op == CiToken.Less
			&& cond.Left.IsReferenceTo(iter)
			&& cond.Right is CiLiteral limit
			&& statement.Advance is CiUnaryExpr adv
			&& adv.Op == CiToken.Increment
			&& adv.Inner.IsReferenceTo(iter);
	}

	public override void Visit(CiContinue statement)
	{
		if (statement.Loop is CiFor loop
		 && loop.Advance != null
		 && !IsForInRange(loop)) {
			loop.Advance.Accept(this, CiPriority.Statement);
			WriteLine();
		}
		WriteLine("continue");
	}

	public override void Visit(CiDoWhile statement)
	{
		Write("while True");
		OpenChild();
		statement.Body.Accept(this);
		Write("if not ");
		statement.Cond.Accept(this, CiPriority.Statement);
		WriteLine(": break");
		this.Indent--;
	}

	public override void Visit(CiFor statement)
	{
		if (statement.Init != null) {
			if (IsForInRange(statement)) {
				CiVar iter = (CiVar) statement.Init;
				Write("for ");
				WriteName(iter);
				Write(" in range(");
				if (!(iter.Value is CiLiteral start) || (long) start.Value != 0) {
					iter.Value.Accept(this, CiPriority.Statement);
					Write(", ");
				}
				CiLiteral limit = (CiLiteral) ((CiBinaryExpr) statement.Cond).Right;
				Write((long) limit.Value);
				Write(')');
				WriteChild(statement.Body);
				return;
			}
			statement.Init.Accept(this, CiPriority.Statement);
			WriteLine();
		}
		Write("while ");
		if (statement.Cond != null)
			statement.Cond.Accept(this, CiPriority.Statement);
		else
			Write("True");
		OpenChild();
		statement.Body.Accept(this);
		if (statement.Advance != null) {
			statement.Advance.Accept(this, CiPriority.Statement);
			WriteLine();
		}
		CloseChild();
	}

	public override void Visit(CiForeach statement)
	{
		Write("for ");
		Write(statement.Element.Name);
		Write(" in ");
		statement.Collection.Accept(this, CiPriority.Statement);
		WriteChild(statement.Body);
	}

	public override void Visit(CiIf statement)
	{
		Write("if ");
		statement.Cond.Accept(this, CiPriority.Statement);
		WriteChild(statement.OnTrue);
		if (statement.OnFalse != null) {
			Write("el");
			if (statement.OnFalse is CiIf childIf)
				Visit(childIf);
			else {
				Write("se");
				WriteChild(statement.OnFalse);
			}
		}
	}

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return");
		else {
			Write("return ");
			statement.Value.Accept(this, CiPriority.Statement);
			WriteLine();
		}
	}

	public override void Visit(CiThrow statement)
	{
		Write("raise Exception(");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine(')');
	}

	public override void Visit(CiWhile statement)
	{
		Write("while ");
		statement.Cond.Accept(this, CiPriority.Statement);
		WriteChild(statement.Body);
	}

	void Write(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		Write("def ");
		WriteLowercaseWithUnderscores(method.Name);
		Write('(');
		bool first;
		if (method.CallType == CiCallType.Static)
			first = true;
		else {
			Write("self");
			first = false;
		}
		WriteParameters(method, first, true);
		this.CurrentMethod = method;
		WriteChild(method.Body);
		this.CurrentMethod = null;
	}

	void Write(CiClass klass)
	{
		WriteLine();
		Write("class ");
		Write(klass.Name);
		if (klass.BaseClassName != null) {
			Write('(');
			Write(klass.BaseClassName);
			Write(')');
		}
		OpenChild();
		if (klass.Constructor != null) {
			Write("def __init__(self)");
			WriteChild(klass.Constructor.Body);
		}
		foreach (CiMethod method in klass.Methods)
			Write(method);
		CloseChild();
	}

	public override void Write(CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		OpenStringWriter();
		foreach (CiClass klass in program.Classes)
			Write(klass);
		CreateFile(this.OutputFile);
		WriteIncludes("import ", "");
//		foreach (CiEnum enu in program.OfType<CiEnum>())
//			Write(enu);
		CloseStringWriter();
		CloseFile();
	}
}

}
