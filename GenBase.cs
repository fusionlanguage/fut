// SourceGenerator.cs - base class for code generators
//
// Copyright (C) 2011-2018  Piotr Fusik
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

	protected void TerminateStatement()
	{
		if (!this.AtLineStart)
			WriteLine(";");
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

	protected void Write(byte[] array)
	{
		for (int i = 0; i < array.Length; i++) {
			if (i > 0) {
				if ((i & 15) == 0) {
					WriteLine(",");
					Write('\t');
				}
				else
					Write(", ");
			}
			Write(array[i]);
		}
	}

	protected abstract void Write(CiType type, bool promote);

	protected abstract void WriteName(CiSymbol symbol);

	protected virtual void WriteTypeAndName(CiNamedValue value)
	{
		Write(value.Type, true);
		Write(' ');
		WriteName(value);
	}

	protected void WritePromoted(CiExpr[] exprs)
	{
		for (int i = 0; i < exprs.Length; i++) {
			if (i > 0)
				Write(", ");
			WritePromoted(exprs[i], CiPriority.Statement);
		}
	}

	protected void WriteCoerced(CiType type, CiExpr[] exprs)
	{
		for (int i = 0; i < exprs.Length; i++) {
			if (i > 0)
				Write(", ");
			WriteCoerced(type, exprs[i]);
		}
	}

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		CiType type = ((CiArrayStorageType) expr.Type).ElementType;
		Write("{ ");
		WriteCoerced(type, expr.Items);
		Write(" }");
		return expr;
	}

	protected virtual void WriteVar(CiNamedValue def)
	{
		WriteTypeAndName(def);
		CiClass klass = def.Type as CiClass;
		if (klass != null) {
			Write(" = ");
			WriteNew(klass);
		}
		else if (def.Type is CiArrayStorageType && !(def.Value is CiCollection)) {
			WriteInitArray(def, (CiArrayStorageType) def.Type);
			// FIXME: initialized arrays
		}
		else if (def.Value != null) {
			Write(" = ");
			WritePromoted(def.Value, CiPriority.Statement);
		}
	}

	public override CiExpr Visit(CiVar expr, CiPriority parent)
	{
		WriteVar(expr);
		return expr;
	}

	protected virtual void WriteLiteral(object value)
	{
		if (value == null)
			Write("null");
		else if (value is long)
			Write((long) value);
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
		else if (value is double)
			Write(((double) value).ToString("R", CultureInfo.InvariantCulture));
		else
			throw new NotImplementedException(value.GetType().Name);
	}

	public override CiExpr Visit(CiLiteral expr, CiPriority parent)
	{
		WriteLiteral(expr.Value);
		return expr;
	}

	protected virtual void Write(CiSymbolReference expr)
	{
		WriteName(expr.Symbol);
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		WriteName(expr.Symbol);
		return expr;
	}

	protected void WriteNew(CiClass klass)
	{
		Write("new ");
		Write(klass.Name);
		Write("()");
	}

	protected virtual bool WriteNewArray(CiType type)
	{
		Write("new ");
		Write(type.BaseType, false);
		for (;;) {
			CiArrayType array = type as CiArrayType;
			if (array == null)
				break;
			Write('[');
			CiArrayStorageType arrayStorage = array as CiArrayStorageType;
			if (arrayStorage != null)
				arrayStorage.LengthExpr.Accept(this, CiPriority.Statement);
			Write(']');
			type = array.ElementType;
		}
		return true; // inner dimensions allocated
	}

	protected virtual void WriteInt()
	{
		Write("int");
	}

	void WriteArrayElement(CiNamedValue def, int nesting)
	{
		Write(def.Name);
		for (int i = 0; i < nesting; i++) {
			Write("[_i");
			Write(i);
			Write(']');
		}
	}

	protected void WriteInitArray(CiNamedValue def, CiArrayStorageType array)
	{
		Write(" = ");
		bool multiDim = WriteNewArray(array);
		if (multiDim) {
			CiArrayStorageType innerArray = array;
			while (innerArray.ElementType is CiArrayStorageType)
				innerArray = (CiArrayStorageType) innerArray.ElementType;
			if (!(innerArray.ElementType is CiClass))
				return;
		}
		WriteLine(";");
		int nesting = 0;
		for (;;) {
			CiClass klass = array.ElementType as CiClass;
			CiArrayStorageType innerArray = array.ElementType as CiArrayStorageType;
			if (klass == null && innerArray == null)
				break;
			Write("for (");
			WriteInt();
			Write(" _i");
			Write(nesting);
			Write(" = 0; _i");
			Write(nesting);
			Write(" < ");
			array.LengthExpr.Accept(this, CiPriority.Rel);
			Write("; _i");
			Write(nesting);
			Write("++) ");
			OpenBlock();
			nesting++;
			if (klass != null) {
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNew(klass);
				WriteLine(";");
				break;
			}
			if (!multiDim) {
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNewArray(innerArray);
				WriteLine(";");
			}
			array = innerArray;
		}
		while (--nesting >= 0)
			CloseBlock();
	}

	protected abstract void WriteResource(string name, int length);

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
			WritePromoted(expr.Inner, CiPriority.Primary);
			return expr;
		case CiToken.Tilde:
			Write('~');
			WritePromoted(expr.Inner, CiPriority.Primary);
			return expr;
		case CiToken.ExclamationMark:
			Write('!');
			break;
		case CiToken.New:
			CiClassPtrType klass = expr.Type as CiClassPtrType;
			if (klass != null)
				WriteNew(klass.Class);
			else
				WriteNewArray(expr.Type);
			return expr;
		case CiToken.Resource:
			WriteResource((string) ((CiLiteral) expr.Inner).Value, ((CiArrayStorageType) expr.Type).Length);
			return expr;
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
		WritePromoted(expr.Left, left);
		Write(op);
		WritePromoted(expr.Right, right);
		if (parent > left)
			Write(')');
		return expr;
	}

	CiExpr Write(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
	{
		return Write(expr, parent, child, op, child);
	}

	protected virtual void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		Write(expr, parent, CiPriority.Equality, not ? " != " : " == ");
	}

	protected virtual void WritePromoted(CiExpr expr, CiPriority parent)
	{
		expr.Accept(this, parent);
	}

	protected virtual void WriteCoerced(CiType type, CiExpr expr)
	{
		expr.Accept(this, CiPriority.Statement);
	}

	protected virtual void WriteCoercedLiteral(CiType type, CiExpr expr, CiPriority priority)
	{
		expr.Accept(this, priority);
	}

	protected abstract void WriteStringLength(CiExpr expr);

	protected abstract void WriteCharAt(CiBinaryExpr expr);

	protected static bool IsMathReference(CiExpr expr)
	{
		CiSymbolReference symbolReference = expr as CiSymbolReference;
		return symbolReference != null && symbolReference.Symbol == CiSystem.MathClass;
	}

	protected virtual void WriteCall(CiExpr obj, string method, CiExpr[] args)
	{
		obj.Accept(this, CiPriority.Primary);
		Write('.');
		Write(method);
		Write('(');
		WritePromoted(args);
		Write(')');
	}

	protected void WriteIndexing(CiBinaryExpr expr)
	{
		expr.Left.Accept(this, CiPriority.Primary);
		Write('[');
		WritePromoted(expr.Right, CiPriority.Statement);
		Write(']');
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
			WriteEqual(expr, parent, false);
			return expr;
		case CiToken.NotEqual:
			WriteEqual(expr, parent, true);
			return expr;
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
			expr.Left.Accept(this, CiPriority.Assign);
			Write(' ');
			Write(expr.OpString);
			Write(' ');
			if (expr.Left.IntPromotion)
				WritePromoted(expr.Right, CiPriority.Statement);
			else
				WriteCoerced(expr.Left.Type, expr.Right);
			return expr;

		case CiToken.Dot:
			CiSymbolReference rightSymbol = (CiSymbolReference) expr.Right;
			if (rightSymbol.Symbol == CiSystem.StringLength) {
				WriteStringLength(expr.Left);
				return expr;
			}
			expr.Left.Accept(this, CiPriority.Primary);
			Write('.');
			Write(rightSymbol);
			return expr;

		case CiToken.LeftParenthesis:
			CiBinaryExpr leftBinary = expr.Left as CiBinaryExpr;
			if (leftBinary != null && leftBinary.Op == CiToken.Dot) {
				CiSymbolReference symbol = leftBinary.Right as CiSymbolReference;
				if (symbol != null) {
					WriteCall(leftBinary.Left, symbol.Name, expr.RightCollection);
					return expr;
				}
			}
			expr.Left.Accept(this, CiPriority.Primary);
			Write('(');
			WritePromoted(expr.RightCollection);
			Write(')');
			return expr;

		case CiToken.LeftBracket:
			if (expr.Left.Type is CiStringType)
				WriteCharAt(expr);
			else
				WriteIndexing(expr);
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
		WriteCoercedLiteral(expr.Type, expr.OnTrue, CiPriority.Cond);
		Write(" : ");
		WriteCoercedLiteral(expr.Type, expr.OnFalse, CiPriority.Cond);
		if (parent > CiPriority.Cond) Write(')');
		return expr;
	}

	public override void Visit(CiExpr statement)
	{
		statement.Accept(this, CiPriority.Statement);
		TerminateStatement();
	}

	public override void Visit(CiConst statement)
	{
	}

	protected void Write(CiStatement[] statements)
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
			WritePromoted(statement.Value, CiPriority.Statement);
			WriteLine(";");
		}
	}

	public override void Visit(CiSwitch statement)
	{
		CiType coerceTo = statement.Value.IntPromotion ? null : statement.Value.Type;
		Write("switch (");
		statement.Value.Accept(this, CiPriority.Statement);
		WriteLine(") {");
		foreach (CiCase kase in statement.Cases) {
			foreach (CiExpr value in kase.Values) {
				Write("case ");
				if (coerceTo != null)
					WriteCoerced(coerceTo, value);
				else
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
