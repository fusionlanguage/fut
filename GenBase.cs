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
using System.Globalization;
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

	protected abstract void Write(CiType type);

	protected void WriteTypeAndName(CiNamedValue value)
	{
		Write(value.Type);
		Write(' ');
		Write(value.Name);
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
		WriteTypeAndName(expr);
		if (expr.Value != null) {
			Write(" = ");
			expr.Value.Accept(this, CiPriority.Statement);
		}
		return expr;
	}

	protected virtual void WriteLiteral(object value)
	{
		if (value == null)
			Write("null");
		else if (value is bool)
			Write((bool) value ? "true" : "false");
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
		else // long, double
			Write(((IConvertible) value).ToString(CultureInfo.InvariantCulture));
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

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment:
			Write("++");
			break;
		case CiToken.Decrement:
			Write("--");
			break;
		case CiToken.Minus:
			Write('-');
			CiPrefixExpr inner = expr.Inner as CiPrefixExpr;
			// FIXME: - --foo[bar]
			if (inner != null && (inner.Op == CiToken.Minus || inner.Op == CiToken.Decrement))
				Write(' ');
			break;
		case CiToken.Tilde:
			Write('~');
			break;
		case CiToken.ExclamationMark:
			Write('!');
			break;
		case CiToken.New:
			Write("new ");
			break;
		default:
			throw new ArgumentException(expr.Op.ToString());
		}
		expr.Inner.Accept(this, CiPriority.Primary);
		return expr;
	}

	public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent)
	{
		expr.Inner.Accept(this, CiPriority.Primary);
		switch (expr.Op) {
		case CiToken.Increment:
			Write("++");
			break;
		case CiToken.Decrement:
			Write("--");
			break;
		default:
			throw new ArgumentException(expr.Op.ToString());
		}
		return expr;
	}

	CiExpr Write(CiBinaryExpr expr, CiPriority parent, CiPriority left, string op, CiPriority right)
	{
		if (parent > left)
			Write('(');
		expr.Left.Accept(this, left);
		Write(op);
		expr.Right.Accept(this, right);
		if (parent > left)
			Write(')');
		return expr;
	}

	CiExpr Write(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
	{
		return Write(expr, parent, child, op, child);
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Plus:
			return Write(expr, parent, CiPriority.Add, " + ");
		case CiToken.Minus:
			return Write(expr, parent, CiPriority.Add, " - ", CiPriority.Mul);
		case CiToken.Asterisk:
			return Write(expr, parent, CiPriority.Mul, " * ");
		case CiToken.Slash:
			return Write(expr, parent, CiPriority.Mul, " / ", CiPriority.Primary);
		case CiToken.Mod:
			return Write(expr, parent, CiPriority.Mul, " % ", CiPriority.Primary);
		case CiToken.ShiftLeft:
			return Write(expr, parent, CiPriority.Shift, " << ", CiPriority.Add);
		case CiToken.ShiftRight:
			return Write(expr, parent, CiPriority.Shift, " >> ", CiPriority.Add);
		case CiToken.Less:
			return Write(expr, parent, CiPriority.Rel, " < ");
		case CiToken.LessOrEqual:
			return Write(expr, parent, CiPriority.Rel, " <= ");
		case CiToken.Greater:
			return Write(expr, parent, CiPriority.Rel, " > ");
		case CiToken.GreaterOrEqual:
			return Write(expr, parent, CiPriority.Rel, " >= ");
		case CiToken.Equal:
			return Write(expr, parent, CiPriority.Equality, " == ");
		case CiToken.NotEqual:
			return Write(expr, parent, CiPriority.Equality, " != ");
		case CiToken.And:
			return Write(expr, parent, CiPriority.And, " & ");
		case CiToken.Or:
			return Write(expr, parent, CiPriority.Or, " | ");
		case CiToken.Xor:
			return Write(expr, parent, CiPriority.Xor, " ^ ");
		case CiToken.CondAnd:
			return Write(expr, parent, CiPriority.CondAnd, " && ");
		case CiToken.CondOr:
			return Write(expr, parent, CiPriority.CondOr, " || ");
		case CiToken.Assign:
			return Write(expr, parent, CiPriority.Assign, " = ", CiPriority.Statement);
		case CiToken.AddAssign:
			return Write(expr, parent, CiPriority.Assign, " += ", CiPriority.Statement);
		case CiToken.SubAssign:
			return Write(expr, parent, CiPriority.Assign, " -= ", CiPriority.Statement);
		case CiToken.MulAssign:
			return Write(expr, parent, CiPriority.Assign, " *= ", CiPriority.Statement);
		case CiToken.DivAssign:
			return Write(expr, parent, CiPriority.Assign, " /= ", CiPriority.Statement);
		case CiToken.ModAssign:
			return Write(expr, parent, CiPriority.Assign, " %= ", CiPriority.Statement);
		case CiToken.ShiftLeftAssign:
			return Write(expr, parent, CiPriority.Assign, " <<= ", CiPriority.Statement);
		case CiToken.ShiftRightAssign:
			return Write(expr, parent, CiPriority.Assign, " >>= ", CiPriority.Statement);
		case CiToken.AndAssign:
			return Write(expr, parent, CiPriority.Assign, " &= ", CiPriority.Statement);
		case CiToken.OrAssign:
			return Write(expr, parent, CiPriority.Assign, " |= ", CiPriority.Statement);
		case CiToken.XorAssign:
			return Write(expr, parent, CiPriority.Assign, " ^= ", CiPriority.Statement);
		case CiToken.Dot:
			return Write(expr, parent, CiPriority.Primary, ".");

		case CiToken.LeftParenthesis:
			expr.Left.Accept(this, CiPriority.Primary);
			Write('(');
			CiExpr[] args = expr.RightCollection;
			for (int i = 0; i < args.Length; i++) {
				if (i > 0)
					Write(", ");
				args[i].Accept(this, CiPriority.Statement);
			}
			Write(')');
			return expr;

		case CiToken.LeftBracket:
			expr.Left.Accept(this, CiPriority.Primary);
			Write('[');
			expr.Right.Accept(this, CiPriority.Statement);
			Write(']');
			return expr;

		default:
			throw new ArgumentException(expr.Op.ToString());
		}
	}

	public override CiExpr Visit(CiCondExpr expr, CiPriority parent)
	{
		if (parent > CiPriority.Cond) Write('(');
		expr.Cond.Accept(this, CiPriority.Cond);
		Write(" ? ");
		expr.OnTrue.Accept(this, CiPriority.Cond);
		Write(" : ");
		expr.OnFalse.Accept(this, CiPriority.Cond);
		if (parent > CiPriority.Cond) Write(')');
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
