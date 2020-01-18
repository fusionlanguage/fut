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

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiMember member) {
			Write(member.IsStatic() ? this.CurrentMethod.Parent.Name : "self");
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

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		Write("[ ");
		WriteCoercedLiterals(null, expr.Items);
		Write(" ]");
		return expr;
	}

	static bool IsByte(CiType type)
		=> type is CiRangeType range && range.Min >= 0 && range.Max <= byte.MaxValue;

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
		if (IsByte(elementType) && (value == null || (value is CiLiteral literal && (long) literal.Value == 0))) {
			Write("bytearray(");
			lengthExpr.Accept(this, CiPriority.Statement);
			Write(')');
		}
		else if (elementType is CiClass klass || elementType is CiArrayStorageType) {
			Write("[ ");
			WriteNewStorage(elementType);
			Write(" for i in range(");
			lengthExpr.Accept(this, CiPriority.Statement);
			Write(") ]");
		}
		else {
			Write("[ ");
			if (value == null)
				WriteDefaultValue(elementType); // TODO: storage
			else
				value.Accept(this, CiPriority.Statement);
			Write(" ] * ");
			lengthExpr.Accept(this, CiPriority.Mul);
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
		Write(IsByte(list.ElementType) ? " = bytearray()" : " = []");
	}

	protected override void WriteSortedDictionaryStorageInit(CiSortedDictionaryType dict)
	{
		Include("sortedcontainers");
		Write(" = sortedcontainers.SortedDict()");
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
			if (args.Length == 2) {
				args[0].Accept(this, CiPriority.Add); // TODO: side effect
				Write(" + ");
				args[1].Accept(this, CiPriority.Add);
			}
			Write(']');
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
		else if (method == CiSystem.ConsoleWrite)
			WriteConsoleWrite(obj, args, false);
		else if (method == CiSystem.ConsoleWriteLine)
			WriteConsoleWrite(obj, args, true);
		else if (method == CiSystem.UTF8GetString) {
			args[0].Accept(this, CiPriority.Primary);
			Write('[');
			args[1].Accept(this, CiPriority.Statement);
			Write(':');
			args[1].Accept(this, CiPriority.Add); // TODO: side effect
			Write(" + ");
			args[2].Accept(this, CiPriority.Add);
			Write("].decode(\"utf-8\")");
		}
		else if (method == CiSystem.MathFusedMultiplyAdd) {
			Include("pyfma");
			Write("pyfma.fma");
			WriteArgsInParentheses(method, args);
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
			Write("CiResource.");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	void WriteXcrement<T>(CiExpr expr) where T : CiUnaryExpr
	{
		switch (expr) {
		case CiCollection coll:
			foreach (CiExpr item in coll.Items)
				WriteXcrement<T>(item);
			break;
		case CiVar def:
			if (def.Value != null)
				WriteXcrement<T>(def.Value);
			break;
		case CiLiteral literal:
			break;
		case CiInterpolatedString interp:
			foreach (CiInterpolatedPart part in interp.Parts) {
				if (part.Argument != null)
					WriteXcrement<T>(part.Argument);
			}
			break;
		case CiSymbolReference symbol:
			if (symbol.Left != null)
				WriteXcrement<T>(symbol.Left);
			break;
		case CiUnaryExpr unary:
			WriteXcrement<T>(unary.Inner);
			if ((unary.Op == CiToken.Increment || unary.Op == CiToken.Decrement) && unary is T) {
				unary.Inner.Accept(this, CiPriority.Assign);
				WriteLine(unary.Op == CiToken.Increment ? " += 1" : " -= 1");
			}
			break;
		case CiBinaryExpr binary:
			WriteXcrement<T>(binary.Left);
			// FIXME: CondAnd, CondOr
			WriteXcrement<T>(binary.Right);
			break;
		case CiCondExpr cond:
			WriteXcrement<T>(cond.Cond);
			// FIXME
			WriteXcrement<T>(cond.OnTrue);
			WriteXcrement<T>(cond.OnFalse);
			break;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	static bool NeedsInit(CiNamedValue def)
		=> def.Value != null || def.Type.IsFinal || def.Type.IsDynamicPtr;

	public override void Visit(CiExpr statement)
	{
		if (!(statement is CiVar def) || NeedsInit(def)) {
			WriteXcrement<CiPrefixExpr>(statement);
			if (!(statement is CiUnaryExpr unary) || (unary.Op != CiToken.Increment && unary.Op != CiToken.Decrement)) {
				statement.Accept(this, CiPriority.Statement);
				WriteLine();
			}
			WriteXcrement<CiPostfixExpr>(statement);
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
		// FIXME: check iter not modified in statement.Body
	}

	void EndBody(CiFor statement)
	{
		if (statement.Advance != null)
			statement.Advance.Accept(this);
		if (statement.Cond != null)
			WriteXcrement<CiPrefixExpr>(statement.Cond);
	}

	public override void Visit(CiContinue statement)
	{
		switch (statement.Loop) {
		case CiDoWhile doWhile:
			WriteXcrement<CiPrefixExpr>(doWhile.Cond);
			Write("if ");
			doWhile.Cond.Accept(this, CiPriority.Statement);
			WriteLine(": continue");
			WriteLine("break");
			// FIXME:WriteXcrement<CiPostfixExpr>(doWhile.Cond);
			return;
		case CiFor forLoop when !IsForInRange(forLoop):
			EndBody(forLoop);
			break;
		case CiWhile whileLoop:
			WriteXcrement<CiPrefixExpr>(whileLoop.Cond);
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
		WriteXcrement<CiPrefixExpr>(statement.Cond);
		Write("if not ");
		statement.Cond.Accept(this, CiPriority.Primary);
		WriteLine(": break");
		// FIXME:WriteXcrement<CiPostfixExpr>(statement.Cond);
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
			statement.Init.Accept(this);
		}
		if (statement.Cond != null) {
			WriteXcrement<CiPrefixExpr>(statement.Cond);
			Write("while ");
			statement.Cond.Accept(this, CiPriority.Statement);
		}
		else
			Write("while True");
		OpenChild();
		statement.Body.Accept(this);
		EndBody(statement);
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
		WriteXcrement<CiPrefixExpr>(statement.Cond);
		Write("if ");
		statement.Cond.Accept(this, CiPriority.Statement);
		// FIXME: WriteXcrement<CiPostfixExpr>(statement.Cond);
		WriteChild(statement.OnTrue);
		if (statement.OnFalse != null) {
			Write("el");
			if (statement.OnFalse is CiIf childIf)
				Visit(childIf); // FIXME: Xcrement
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
			WriteXcrement<CiPrefixExpr>(statement.Value);
			Write("return ");
			statement.Value.Accept(this, CiPriority.Statement);
			WriteLine();
			// FIXME: WriteXcrement<CiPostfixExpr>(statement.Value);
		}
	}

	public override void Visit(CiThrow statement)
	{
		WriteXcrement<CiPrefixExpr>(statement.Message);
		Write("raise Exception(");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine(')');
		// FIXME: WriteXcrement<CiPostfixExpr>(statement.Message);
	}

	public override void Visit(CiWhile statement)
	{
		WriteXcrement<CiPrefixExpr>(statement.Cond);
		Write("while ");
		statement.Cond.Accept(this, CiPriority.Statement);
		//FIXME: WriteXcrement<CiPostfixExpr>(statement.Cond);
		WriteChild(statement.Body);
	}

	void Write(CiEnum enu)
	{
		Include("enum");
		WriteLine();
		//TODO: Write(enu.Documentation);
		Write("class ");
		Write(enu.Name);
		Write("(enum.Enum)");
		OpenChild();
		int i = 1;
		foreach (CiConst konst in enu) {
			//TODO: Write(konst.Documentation);
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			if (konst.Value != null)
				konst.Value.Accept(this, CiPriority.Statement);
			else
				Write(i);
			WriteLine();
			i++;
		}
		CloseChild();
	}

	void WriteConsts(IEnumerable<CiConst> consts)
	{
		foreach (CiConst konst in consts) {
			if (konst.Visibility != CiVisibility.Private || konst.Type is CiArrayStorageType) {
				WriteLine();
				//TODO: Write(konst.Documentation);
				base.WriteVar(konst);
				WriteLine();
			}
		}
	}

	void Write(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		WriteLine();
		//TODO: Write(method.Documentation);
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
		//TODO: Write(klass.Documentation);
		Write("class ");
		Write(klass.Name);
		if (klass.BaseClassName != null) {
			Write('(');
			Write(klass.BaseClassName);
			Write(')');
		}
		OpenChild();
		WriteConsts(klass.Consts);
		if (klass.Constructor != null
		 || klass.Fields.Any(NeedsInit)) {
			WriteLine();
			Write("def __init__(self)");
			OpenChild();
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
		Write("class CiResource");
		OpenChild();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			WriteResource(name, -1);
			Write(" = (");
			int i = 0;
			foreach (byte b in resources[name]) {
				if ((i & 15) == 0) {
					if (i > 0)
						Write('"');
					WriteLine();
					Write("b\"");
				}
				Write($"\\x{b:x2}");
				i++;
			}
			WriteLine(" )");
		}
		CloseChild();
	}

	public override void Write(CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		OpenStringWriter();
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes)
			Write(klass);
		CreateFile(this.OutputFile);
		WriteIncludes("import ", "");
		CloseStringWriter();
		WriteResources(program.Resources);
		CloseFile();
	}
}

}
