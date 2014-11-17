// SourceGenerator.cs - base class for code generators
//
// Copyright (C) 2011-2014  Piotr Fusik
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

public delegate TextWriter TextWriterFactory(string filename);

public abstract class GenBase : CiVisitor
{
	public string OutputFile;
	public TextWriterFactory CreateTextWriter = CreateFileWriter;
	TextWriter Writer;
	protected int Indent = 0;
	bool AtLineStart = true;

	static TextWriter CreateFileWriter(string filename)
	{
		TextWriter w = File.CreateText(filename);
		w.NewLine = "\n";
		return w;
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

	protected void Write(long i)
	{
		StartLine();
		this.Writer.Write(i);
	}

	protected void WriteLowercase(string s)
	{
		foreach (char c in s)
			this.Writer.Write(char.ToLowerInvariant(c));
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
				this.Writer.Write(char.ToUpperInvariant(c));
			first = false;
		}
	}

	protected void WriteLowercaseWithUnderscores(string s)
	{
		StartLine();
		bool first = true;
		foreach (char c in s) {
			if (char.IsUpper(c)) {
				if (!first)
					this.Writer.Write('_');
				this.Writer.Write(char.ToLowerInvariant(c));
			}
			else
				this.Writer.Write(c);
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
		this.Writer = CreateTextWriter(filename);
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

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		Write("{ ");
		for (int i = 0; i < expr.Items.Length; i++) {
			if (i > 0)
				Write(", ");
			expr.Items[i].Accept(this, CiPriority.Statement);
		}
		Write('}');
		return expr;
	}

	public override CiExpr Visit(CiVar expr, CiPriority parent)
	{
		// TODO: type
		Write(expr.Name);
		if (expr.Value != null) {
			Write(" = ");
			expr.Value.Accept(this, CiPriority.Statement);
		}
		return expr;
	}

	protected virtual void WriteLiteral(object value)
	{
		if (value is long)
			Write((long) value);
		else if (value is string) {
			Write('"');
			foreach (char c in (string) value) {
				switch (c) {
				case '\a': Write("\\a"); break;
				case '\b': Write("\\b"); break;
				case '\f': Write("\\f"); break;
				case '\n': Write("\\n"); break;
				case '\r': Write("\\r"); break;
				case '\t': Write("\\t"); break;
				case '\v': Write("\\v"); break;
				case '\\': Write("\\\\"); break;
				case '\"': Write("\\\""); break;
				default: Write(c); break;
				}
			}
			Write('"');
		}
		else
			throw new ArgumentException(value.GetType().Name);
	}

	public override CiExpr Visit(CiLiteral expr, CiPriority parent)
	{
		WriteLiteral(expr.Value);
		return expr;
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		Write(expr.Name);
		return expr;
	}

	public override CiExpr Visit(CiUnaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment: Write("++"); break;
		case CiToken.Decrement: Write("--"); break;
		case CiToken.Minus: Write('-'); break;
		case CiToken.Tilde: Write('~'); break;
		case CiToken.ExclamationMark: Write('!'); break;
		case CiToken.New: Write("new "); break;
		default: throw new ArgumentException(expr.Op.ToString());
		}
		expr.Inner.Accept(this, CiPriority.Statement); // TODO
		return expr;
	}

	public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent)
	{
		expr.Inner.Accept(this, CiPriority.Statement); // TODO
		switch (expr.Op) {
		case CiToken.Increment: Write("++"); break;
		case CiToken.Decrement: Write("--"); break;
		default: throw new ArgumentException(expr.Op.ToString());
		}
		return expr;
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		expr.Left.Accept(this, CiPriority.Statement); // TODO
		switch (expr.Op) {
		case CiToken.Plus: Write(" + "); break;
		case CiToken.Minus: Write(" - "); break;
		case CiToken.Asterisk: Write(" * "); break;
		case CiToken.Slash: Write(" / "); break;
		case CiToken.Mod: Write(" % "); break;
		case CiToken.ShiftLeft: Write(" << "); break;
		case CiToken.ShiftRight: Write(" >> "); break;
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
		case CiToken.Dot: Write('.'); break; // TODO
		case CiToken.LeftParenthesis:
			Write('(');
			CiExpr[] args = ((CiCollection) expr.Right).Items;
			for (int i = 0; i < args.Length; i++) {
				if (i > 0)
					Write(", ");
				args[i].Accept(this, CiPriority.Statement);
			}
			Write(')');
			return expr;
		case CiToken.LeftBracket:
			Write('[');
			expr.Right.Accept(this, CiPriority.Statement);
			Write(']');
			return expr;
		default: throw new ArgumentException(expr.Op.ToString());
		}
		expr.Right.Accept(this, CiPriority.Statement); // TODO
		return expr;
	}

	public override CiExpr Visit(CiCondExpr expr, CiPriority parent)
	{
		// TODO
		expr.Cond.Accept(this, CiPriority.Statement);
		Write(" ? ");
		expr.OnTrue.Accept(this, CiPriority.Statement);
		Write(" : ");
		expr.OnFalse.Accept(this, CiPriority.Statement);
		return expr;
	}

	public override void Visit(CiExpr statement)
	{
		statement.Accept(this, CiPriority.Statement);
		WriteLine(";");
	}

	public override void Visit(CiConst statement)
	{
	}

	void Write(CiStatement[] statements)
	{
		foreach (CiStatement statement in statements)
			statement.Accept(this);
	}

	public override void Visit(CiBlock statement)
	{
		OpenBlock();
		Write(statement.Statements);
		CloseBlock();
	}

	protected void WriteChild(CiStatement statement)
	{
		CiBlock block = statement as CiBlock;
		if (block != null) {
			Write(' ');
			Visit(block);
		}
		else {
			WriteLine();
			this.Indent++;
			statement.Accept(this);
			this.Indent--;
		}
	}

	public override void Visit(CiBreak statement)
	{
		WriteLine("break;");
	}

	public override void Visit(CiContinue statement)
	{
		WriteLine("continue;");
	}

	public override void Visit(CiDelete stmt)
	{
		// do nothing - assume automatic garbage collector
	}

	public override void Visit(CiDoWhile statement)
	{
		Write("do");
		WriteChild(statement.Body);
		Write("while (");
		statement.Cond.Accept(this, CiPriority.Statement);
		WriteLine(");");
	}

	public override void Visit(CiFor statement)
	{
		Write("for (");
		if (statement.Init != null)
			statement.Init.Accept(this, CiPriority.Statement);
		Write(';');
		if (statement.Cond != null) {
			Write(' ');
			statement.Cond.Accept(this, CiPriority.Statement);
		}
		Write(';');
		if (statement.Advance != null) {
			Write(' ');
			statement.Advance.Accept(this, CiPriority.Statement);
		}
		Write(')');
		WriteChild(statement.Body);
	}

	public override void Visit(CiIf statement)
	{
		Write("if (");
		statement.Cond.Accept(this, CiPriority.Statement);
		Write(')');
		WriteChild(statement.OnTrue);
		if (statement.OnFalse != null) {
			Write("else");
			if (statement.OnFalse is CiIf) {
				Write(' ');
				statement.OnFalse.Accept(this);
			}
			else
				WriteChild(statement.OnFalse);
		}
	}

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return;");
		else {
			Write("return ");
			statement.Value.Accept(this, CiPriority.Statement);
			WriteLine(";");
		}
	}

	public override void Visit(CiSwitch statement)
	{
		Write("switch (");
		statement.Value.Accept(this, CiPriority.Statement);
		WriteLine(") {");
		foreach (CiCase kase in statement.Cases) {
			foreach (CiExpr value in kase.Values) {
				Write("case ");
				value.Accept(this, CiPriority.Statement);
				WriteLine(":");
			}
			this.Indent++;
			Write(kase.Body);
			this.Indent--;
		}
		if (statement.DefaultBody != null) {
			WriteLine("default:");
			this.Indent++;
			Write(statement.DefaultBody);
			this.Indent--;
		}
		WriteLine("}");
	}

	public override void Visit(CiWhile statement)
	{
		Write("while (");
		statement.Cond.Accept(this, CiPriority.Statement);
		Write(')');
		WriteChild(statement.Body);
	}

	protected void WritePublic(CiContainerType container)
	{
		if (container.IsPublic)
			Write("public ");
	}

	protected void OpenClass(CiClass klass, string extendsClause)
	{
		Write("class ");
		Write(klass.Name);
		if (klass.BaseClassName != null) {
			Write(extendsClause);
			Write(klass.BaseClassName);
		}
		WriteLine();
		OpenBlock();
	}

	protected void WriteBody(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			WriteLine(";");
		else {
			WriteLine();
			CiBlock block = method.Body as CiBlock;
			if (block != null)
				Visit(block);
			else {
				OpenBlock();
				method.Body.Accept(this);
				CloseBlock();
			}
		}
	}

	public abstract void Write(CiProgram program);
}

}
