// SourceGenerator.cs - base class for code generators
//
// Copyright (C) 2011  Piotr Fusik
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
using System.IO;

namespace Foxoft.Ci
{

public abstract class SourceGenerator : ICiStatementVisitor
{
	public string OutputPath;
	TextWriter Writer;
	int Indent = 0;
	bool AtLineStart = true;

	protected void CreateFile(string filename)
	{
		this.Writer = File.CreateText(filename);
		this.Writer.NewLine = "\n";
		WriteLine("// Generated automatically with \"cito\". Do not edit.");
	}

	protected void CloseFile()
	{
		this.Writer.Close();
	}

	void StartLine()
	{
		if (this.AtLineStart) {
			for (int i = 0; i < this.Indent; i++)
				this.Writer.Write('\t');
			this.AtLineStart = false;
		}
	}

	protected void Write(char c)
	{
		StartLine();
		this.Writer.Write(c);
	}

	protected void Write(string s)
	{
		StartLine();
		this.Writer.Write(s);
	}

	protected void Write(int i)
	{
		StartLine();
		this.Writer.Write(i);
	}

	protected void Write(string format, params object[] args)
	{
		StartLine();
		this.Writer.Write(format, args);
	}

	protected void WriteLine()
	{
		this.Writer.WriteLine();
		this.AtLineStart = true;
	}

	protected void WriteLine(string s)
	{
		StartLine();
		this.Writer.WriteLine(s);
		this.AtLineStart = true;
	}

	protected void WriteLine(string format, params object[] args)
	{
		StartLine();
		this.Writer.WriteLine(format, args);
		this.AtLineStart = true;
	}

	protected void OpenBlock()
	{
		WriteLine("{");
		this.Indent++;
	}

	protected void CloseBlock()
	{
		this.Indent--;
		WriteLine("}");
	}

	protected void WriteInitializer(CiArrayType type)
	{
		for (; type != null; type = type.ElementType as CiArrayType) {
			Write('[');
			CiArrayStorageType storageType = type as CiArrayStorageType;
			if (storageType != null)
				Write(storageType.Length);
			Write(']');
		}
	}

	protected void WriteContent(Array array)
	{
		for (int i = 0; i < array.Length; i++) {
			if (i > 0) {
				if (i % 16 == 0) {
					WriteLine(",");
					Write('\t');
				}
				else
					Write(", ");
			}
			WriteConst(array.GetValue(i));
		}
	}

	protected virtual void WriteConst(object value)
	{
		if (value is bool)
			Write((bool) value ? "true" : "false");
		else if (value is byte)
			Write((byte) value);
		else if (value is int)
			Write((int) value);
		else if (value is string) {
			Write('"');
			foreach (char c in (string) value) {
				switch (c) {
				case '\t': Write("\\t"); break;
				case '\r': Write("\\r"); break;
				case '\n': Write("\\n"); break;
				case '\\': Write("\\\\"); break;
				case '\"': Write("\\\""); break;
				default: Write(c); break;
				}
			}
			Write('"');
		}
		else if (value is CiEnumValue) {
			CiEnumValue ev = (CiEnumValue) value;
			Write(ev.Type.Name);
			Write('.');
			Write(ev.Name);
		}
		else if (value is Array) {
			Write("{ ");
			WriteContent((Array) value);
			Write(" }");
		}
		else if (value == null)
			Write("null");
		else
			throw new ApplicationException(value.ToString());
	}

	void Write(CiConstAccess expr)
	{
		if (expr.Const.GlobalName != null)
			Write(expr.Const.GlobalName);
		else
			Write(expr.Const.Name);
	}

	protected virtual int GetPriority(CiExpr expr)
	{
		if (expr is CiConstExpr
		 || expr is CiConstAccess
		 || expr is CiVarAccess
		 || expr is CiFieldAccess
		 || expr is CiPropertyAccess
		 || expr is CiArrayAccess
		 || expr is CiFunctionCall
		 || expr is CiMethodCall
		 || expr is CiBinaryResourceExpr)
			return 1;
		if (expr is CiUnaryExpr
		 || expr is CiCondNotExpr
		 || expr is CiPostfixExpr)
			return 2;
		if (expr is CiCoercion)
			return GetPriority((CiExpr) ((CiCoercion) expr).Inner);
		if (expr is CiBinaryExpr) {
			switch (((CiBinaryExpr) expr).Op) {
			case CiToken.Asterisk:
			case CiToken.Slash:
			case CiToken.Mod:
				return 3;
			case CiToken.Plus:
			case CiToken.Minus:
				return 4;
			case CiToken.ShiftLeft:
			case CiToken.ShiftRight:
				return 5;
			case CiToken.Less:
			case CiToken.LessOrEqual:
			case CiToken.Greater:
			case CiToken.GreaterOrEqual:
				return 6;
			case CiToken.Equal:
			case CiToken.NotEqual:
				return 7;
			case CiToken.And:
				return 8;
			case CiToken.Xor:
				return 9;
			case CiToken.Or:
				return 10;
			case CiToken.CondAnd:
				return 11;
			case CiToken.CondOr:
				return 12;
			default:
				throw new ApplicationException();
			}
		}
		if (expr is CiCondExpr)
			return 13;
		throw new ApplicationException();
	}

	protected void WriteChild(CiExpr parent, CiExpr child)
	{
		if (GetPriority(child) > GetPriority(parent)) {
			Write('(');
			Write(child);
			Write(')');
		}
		else
			Write(child);
	}

	protected void WriteRightChild(CiExpr parent, CiExpr child)
	{
		if (GetPriority(child) >= GetPriority(parent)) {
			Write('(');
			Write(child);
			Write(')');
		}
		else
			Write(child);
	}

	protected virtual void Write(CiFieldAccess expr)
	{
		WriteChild(expr, expr.Obj);
		Write('.');
		Write(expr.Field.Name);
	}

	protected abstract void Write(CiPropertyAccess expr);

	void Write(CiArrayAccess expr)
	{
		WriteChild(expr, expr.Array);
		Write('[');
		Write(expr.Index);
		Write(']');
	}

	void Write(CiFunctionCall expr)
	{
		Write(expr.Function.Name);
		Write('(');
		bool first = true;
		foreach (CiExpr arg in expr.Arguments)
		{
			if (first)
				first = false;
			else
				Write(", ");
			Write(arg);
		}
		Write(')');
	}

	protected abstract void Write(CiMethodCall expr);

	void Write(CiUnaryExpr expr)
	{
		switch (expr.Op) {
		case CiToken.Increment: Write("++"); break;
		case CiToken.Decrement: Write("--"); break;
		case CiToken.Minus: Write('-'); break;
		case CiToken.Not: Write('~'); break;
		default: throw new ApplicationException();
		}
		WriteChild(expr, expr.Inner);
	}

	void Write(CiCondNotExpr expr)
	{
		Write('!');
		WriteChild(expr, expr.Inner);
	}

	void Write(CiPostfixExpr expr)
	{
		WriteChild(expr, expr.Inner);
		switch (expr.Op) {
		case CiToken.Increment: Write("++"); break;
		case CiToken.Decrement: Write("--"); break;
		default: throw new ApplicationException();
		}
	}

	protected virtual void Write(CiBinaryExpr expr)
	{
		WriteChild(expr, expr.Left);
		switch (expr.Op) {
		case CiToken.Plus: Write(" + "); break;
		case CiToken.Minus: Write(" - "); WriteRightChild(expr, expr.Right); return;
		case CiToken.Asterisk: Write(" * "); break;
		case CiToken.Slash: Write(" / "); WriteRightChild(expr, expr.Right); return;
		case CiToken.Mod: Write(" % "); WriteRightChild(expr, expr.Right); return;
		case CiToken.ShiftLeft: Write(" << "); WriteRightChild(expr, expr.Right); return;
		case CiToken.ShiftRight: Write(" >> "); WriteRightChild(expr, expr.Right); return;
		case CiToken.Less: Write(" < "); break;
		case CiToken.LessOrEqual: Write(" <= "); break;
		case CiToken.Greater: Write(" > "); break;
		case CiToken.GreaterOrEqual: Write(" >= "); break;
		case CiToken.Equal: Write(" == "); break;
		case CiToken.NotEqual: Write(" != "); break;
		case CiToken.And: Write(" & "); break;
		case CiToken.Or: Write(" | "); break;
		case CiToken.Xor: Write(" ^ "); break;
		case CiToken.CondAnd: Write(" && "); break;
		case CiToken.CondOr: Write(" || "); break;
		default:
			throw new ApplicationException();
		}
		WriteChild(expr, expr.Right);
	}

	void Write(CiCondExpr expr)
	{
		WriteChild(expr, expr.Cond);
		Write(" ? ");
		WriteChild(expr, expr.OnTrue);
		Write(" : ");
		WriteChild(expr, expr.OnFalse);
	}

	protected void WriteName(CiBinaryResource resource)
	{
		Write("CiBinaryResource_");
		foreach (char c in resource.Name)
			Write(CiLexer.IsLetter(c) ? c : '_');
	}

	protected virtual void Write(CiBinaryResourceExpr expr)
	{
		WriteName(expr.Resource);
	}

	void WriteInline(CiMaybeAssign expr)
	{
		if (expr is CiExpr)
			Write((CiExpr) expr);
		else
			Visit((CiAssign) expr);
	}

	protected virtual void Write(CiCoercion expr)
	{
		WriteInline(expr.Inner);
	}

	protected void Write(CiExpr expr)
	{
		if (expr is CiConstExpr)
			WriteConst(((CiConstExpr) expr).Value);
		else if (expr is CiConstAccess)
			Write((CiConstAccess) expr);
		else if (expr is CiVarAccess)
			Write(((CiVarAccess) expr).Var.Name);
		else if (expr is CiFieldAccess)
			Write((CiFieldAccess) expr);
		else if (expr is CiPropertyAccess)
			Write((CiPropertyAccess) expr);
		else if (expr is CiArrayAccess)
			Write((CiArrayAccess) expr);
		else if (expr is CiFunctionCall) {
			if (expr is CiMethodCall)
				Write((CiMethodCall) expr);
			else
				Write((CiFunctionCall) expr);
		}
		else if (expr is CiUnaryExpr)
			Write((CiUnaryExpr) expr);
		else if (expr is CiCondNotExpr)
			Write((CiCondNotExpr) expr);
		else if (expr is CiPostfixExpr)
			Write((CiPostfixExpr) expr);
		else if (expr is CiBinaryExpr)
			Write((CiBinaryExpr) expr);
		else if (expr is CiCondExpr)
			Write((CiCondExpr) expr);
		else if (expr is CiBinaryResourceExpr)
			Write((CiBinaryResourceExpr) expr);
		else if (expr is CiCoercion)
			Write((CiCoercion) expr);
		else
			throw new ApplicationException(expr.ToString());
	}

	void ICiStatementVisitor.Visit(CiBlock block)
	{
		OpenBlock();
		foreach (ICiStatement stmt in block.Statements)
			Write(stmt);
		CloseBlock();
	}

	protected void WriteChild(ICiStatement stmt)
	{
		if (stmt is CiBlock) {
			Write(' ');
			Write((CiBlock) stmt);
		}
		else {
			WriteLine();
			this.Indent++;
			Write(stmt);
			this.Indent--;
		}
	}

	void ICiStatementVisitor.Visit(CiExpr expr)
	{
		Write(expr);
	}

	public abstract void Visit(CiVar stmt);

	public virtual void Visit(CiAssign assign)
	{
		Write(assign.Target);
		switch (assign.Op) {
		case CiToken.Assign: Write(" = "); break;
		case CiToken.AddAssign: Write(" += "); break;
		case CiToken.SubAssign: Write(" -= "); break;
		case CiToken.MulAssign: Write(" *= "); break;
		case CiToken.DivAssign: Write(" /= "); break;
		case CiToken.ModAssign: Write(" %= "); break;
		case CiToken.ShiftLeftAssign: Write(" <<= "); break;
		case CiToken.ShiftRightAssign: Write(" >>= "); break;
		case CiToken.AndAssign: Write(" &= "); break;
		case CiToken.OrAssign: Write(" |= "); break;
		case CiToken.XorAssign: Write(" ^= "); break;
		default:
			throw new ApplicationException();
		}
		WriteInline(assign.Source);
	}

	void ICiStatementVisitor.Visit(CiBreak stmt)
	{
		WriteLine("break;");
	}

	void ICiStatementVisitor.Visit(CiConst stmt)
	{
	}

	void ICiStatementVisitor.Visit(CiContinue stmt)
	{
		WriteLine("continue;");
	}

	void ICiStatementVisitor.Visit(CiDoWhile stmt)
	{
		Write("do");
		WriteChild(stmt.Body);
		Write("while (");
		Write(stmt.Cond);
		WriteLine(");");
	}

	void ICiStatementVisitor.Visit(CiFor stmt)
	{
		Write("for (");
		if (stmt.Init != null)
			stmt.Init.Accept(this);
		Write(';');
		if (stmt.Cond != null) {
			Write(' ');
			Write(stmt.Cond);
		}
		Write(';');
		if (stmt.Advance != null) {
			Write(' ');
			stmt.Advance.Accept(this);
		}
		Write(')');
		WriteChild(stmt.Body);
	}

	void ICiStatementVisitor.Visit(CiIf stmt)
	{
		Write("if (");
		Write(stmt.Cond);
		Write(')');
		WriteChild(stmt.OnTrue);
		if (stmt.OnFalse != null) {
			Write("else");
			if (stmt.OnFalse is CiIf) {
				Write(' ');
				Write(stmt.OnFalse);
			}
			else
				WriteChild(stmt.OnFalse);
		}
	}

	void ICiStatementVisitor.Visit(CiReturn stmt)
	{
		if (stmt.Value == null)
			WriteLine("return;");
		else {
			Write("return ");
			Write(stmt.Value);
			WriteLine(";");
		}
	}

	void ICiStatementVisitor.Visit(CiSwitch stmt)
	{
		Write("switch (");
		Write(stmt.Value);
		Write(") ");
		OpenBlock();
		foreach (CiCase caze in stmt.Cases) {
			if (caze.Value != null) {
				Write("case ");
				WriteConst(caze.Value);
			}
			else
				Write("default");
			WriteLine(":");
			this.Indent++;
			foreach (ICiStatement child in caze.Body)
				Write(child);
			this.Indent--;
		}
		CloseBlock();
	}

	void ICiStatementVisitor.Visit(CiWhile stmt)
	{
		Write("while (");
		Write(stmt.Cond);
		Write(')');
		WriteChild(stmt.Body);
	}

	protected virtual void Write(ICiStatement stmt)
	{
		stmt.Accept(this);
		if (stmt is CiMaybeAssign || stmt is CiVar)
			WriteLine(";");
	}

	public abstract void Write(CiProgram prog);
}

}
