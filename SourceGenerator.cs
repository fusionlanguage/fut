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
	protected int Indent = 0;
	bool AtLineStart = true;

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

	protected string ToCamelCase(string s)
	{
		return char.ToLowerInvariant(s[0]) + s.Substring(1);
	}

	protected void WriteCamelCase(string s)
	{
		StartLine();
		this.Writer.Write(char.ToLowerInvariant(s[0]));
		this.Writer.Write(s.Substring(1));
	}

	protected void WriteUppercaseWithUnderscores(string s)
	{
		StartLine();
		bool first = true;
		foreach (char c in s) {
			if (char.IsUpper(c) && !first) {
				this.Writer.Write('_');
				this.Writer.Write(c);
			}
			else
				this.Writer.Write(char.ToUpper(c));
			first = false;
		}
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

	protected virtual void WriteBanner()
	{
		WriteLine("// Generated automatically with \"cito\". Do not edit.");
	}

	protected void CreateFile(string filename)
	{
		this.Writer = File.CreateText(filename);
		this.Writer.NewLine = "\n";
		WriteBanner();
	}

	protected void CloseFile()
	{
		this.Writer.Close();
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

	protected virtual int GetPriority(CiExpr expr)
	{
		if (expr is CiConstExpr
		 || expr is CiConstAccess
		 || expr is CiVarAccess
		 || expr is CiFieldAccess
		 || expr is CiPropertyAccess
		 || expr is CiArrayAccess
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

	protected void WriteChild(int parentPriority, CiExpr child)
	{
		if (GetPriority(child) > parentPriority) {
			Write('(');
			Write(child);
			Write(')');
		}
		else
			Write(child);
	}

	protected void WriteChild(CiExpr parent, CiExpr child)
	{
		WriteChild(GetPriority(parent), child);
	}

	protected void WriteRightChild(int parentPriority, CiExpr child)
	{
		if (GetPriority(child) >= parentPriority) {
			Write('(');
			Write(child);
			Write(')');
		}
		else
			Write(child);
	}

	protected void WriteRightChild(CiExpr parent, CiExpr child)
	{
		WriteRightChild(GetPriority(parent), child);
	}

	protected virtual void WriteName(CiConst konst)
	{
		if (konst.GlobalName != null)
			Write(konst.GlobalName);
		else
			Write(konst.Name);
	}

	protected virtual void Write(CiVarAccess expr)
	{
		Write(expr.Var.Name);
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

	protected virtual void WriteName(CiMethod method)
	{
		Write(method.Name);
	}

	protected virtual void Write(CiMethodCall expr)
	{
		if (expr.Obj != null) {
			Write(expr.Obj);
			Write('.');
		}
		WriteName(expr.Method);
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

	protected virtual void WriteName(CiBinaryResource resource)
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
			WriteName(((CiConstAccess) expr).Const);
		else if (expr is CiVarAccess)
			Write((CiVarAccess) expr);
		else if (expr is CiFieldAccess)
			Write((CiFieldAccess) expr);
		else if (expr is CiPropertyAccess)
			Write((CiPropertyAccess) expr);
		else if (expr is CiArrayAccess)
			Write((CiArrayAccess) expr);
		else if (expr is CiMethodCall)
			Write((CiMethodCall) expr);
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

	protected void Write(ICiStatement[] statements, int length)
	{
		for (int i = 0; i < length; i++)
			Write(statements[i]);
	}

	protected virtual void Write(ICiStatement[] statements)
	{
		Write(statements, statements.Length);
	}

	public virtual void Visit(CiBlock block)
	{
		OpenBlock();
		Write(block.Statements);
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

	public virtual void Visit(CiExpr expr)
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

	public virtual void Visit(CiConst stmt)
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

	public virtual void Visit(CiReturn stmt)
	{
		if (stmt.Value == null)
			WriteLine("return;");
		else {
			Write("return ");
			Write(stmt.Value);
			WriteLine(";");
		}
	}

	protected virtual void StartSwitch(CiCase[] kases)
	{
	}

	protected virtual void StartCase(ICiStatement stmt)
	{
	}

	void ICiStatementVisitor.Visit(CiSwitch stmt)
	{
		Write("switch (");
		Write(stmt.Value);
		Write(") ");
		OpenBlock();
		StartSwitch(stmt.Cases);
		foreach (CiCase kase in stmt.Cases) {
			if (kase.Value != null) {
				Write("case ");
				WriteConst(kase.Value);
			}
			else
				Write("default");
			WriteLine(":");
			if (kase.Body.Length > 0) {
				this.Indent++;
				StartCase(kase.Body[0]);
				Write(kase.Body);
				this.Indent--;
			}
		}
		CloseBlock();
	}

	public abstract void Visit(CiThrow stmt);

	void ICiStatementVisitor.Visit(CiWhile stmt)
	{
		Write("while (");
		Write(stmt.Cond);
		Write(')');
		WriteChild(stmt.Body);
	}

	protected void Write(ICiStatement stmt)
	{
		stmt.Accept(this);
		if (stmt is CiMaybeAssign || stmt is CiVar)
			WriteLine(";");
	}

	public abstract void Write(CiProgram prog);
}

}
