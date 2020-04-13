// GenBase.cs - base class for code generators
//
// Copyright (C) 2011-2020  Piotr Fusik
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
using System.Globalization;
using System.IO;

namespace Foxoft.Ci
{

public delegate TextWriter TextWriterFactory(string filename);

public abstract class GenBase : CiVisitor
{
	public string Namespace;
	public string OutputFile;
	public TextWriterFactory CreateTextWriter = CreateFileWriter;
	TextWriter Writer;
	StringWriter StringWriter;
	protected int Indent = 0;
	protected bool AtLineStart = true;
	protected SortedSet<string> Includes;
	protected CiMethodBase CurrentMethod = null;

	static TextWriter CreateFileWriter(string filename)
	{
		TextWriter w = File.CreateText(filename);
		w.NewLine = "\n";
		return w;
	}

	protected virtual void StartLine()
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

	protected void WriteCamelCase(string s)
	{
		StartLine();
		this.Writer.Write(char.ToLowerInvariant(s[0]));
		this.Writer.Write(s.Substring(1));
	}

	protected void WritePascalCase(string s)
	{
		StartLine();
		this.Writer.Write(char.ToUpperInvariant(s[0]));
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

	protected void WriteLine(char c)
	{
		StartLine();
		this.Writer.WriteLine(c);
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

	#region JavaDoc

	protected virtual void StartDocLine()
	{
		Write(" * ");
	}

	protected void WriteDoc(string text)
	{
		foreach (char c in text) {
			switch (c) {
			case '&':
				Write("&amp;");
				break;
			case '<':
				Write("&lt;");
				break;
			case '>':
				Write("&gt;");
				break;
			case '\n':
				WriteLine();
				StartDocLine();
				break;
			default:
				Write(c);
				break;
			}
		}
	}

	void Write(CiDocPara para)
	{
		foreach (CiDocInline inline in para.Children) {
			switch (inline) {
			case CiDocText text:
				WriteDoc(text.Text);
				break;
			case CiDocCode code:;
				Write("<code>");
				WriteDoc(code.Text);
				Write("</code>");
				break;
			default:
				throw new ArgumentException(inline.GetType().Name);
			}
		}
	}

	protected virtual void Write(CiDocList list)
	{
		WriteLine();
		WriteLine(" * <ul>");
		foreach (CiDocPara item in list.Items) {
			Write(" * <li>");
			Write(item);
			WriteLine("</li>");
		}
		WriteLine(" * </ul>");
		Write(" * ");
	}

	protected void Write(CiDocBlock block)
	{
		switch (block) {
		case CiDocPara para:
			Write(para);
			break;
		case CiDocList list:
			Write(list);
			break;
		default:
			throw new ArgumentException(block.GetType().Name);
		}
	}

	void StartDoc(CiCodeDoc doc)
	{
		WriteLine("/**");
		Write(" * ");
		Write(doc.Summary);
		if (doc.Details.Length > 0) {
			WriteLine();
			Write(" * ");
			foreach (CiDocBlock block in doc.Details)
				Write(block);
		}
		WriteLine();
	}

	protected virtual void Write(CiCodeDoc doc)
	{
		if (doc != null) {
			StartDoc(doc);
			WriteLine(" */");
		}
	}

	protected virtual void WriteSelfDoc(CiMethod method)
	{
	}

	protected void WriteDoc(CiMethod method)
	{
		if (method.Documentation == null)
			return;
		StartDoc(method.Documentation);
		WriteSelfDoc(method);
		foreach (CiVar param in method.Parameters) {
			if (param.Documentation != null) {
				Write(" * @param ");
				Write(param.Name);
				Write(' ');
				Write(param.Documentation.Summary);
				WriteLine();
			}
		}
		WriteLine(" */");
	}

	#endregion JavaDoc

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

	protected void OpenStringWriter()
	{
		this.StringWriter = new StringWriter();
		this.StringWriter.NewLine = "\n";
		this.Writer = this.StringWriter;
	}

	protected void CloseStringWriter()
	{
		this.Writer.Write(this.StringWriter.GetStringBuilder());
		this.StringWriter = null;
	}

	protected void Include(string name)
	{
		this.Includes.Add(name);
	}

	protected void WriteIncludes(string prefix, string suffix)
	{
		foreach (string name in this.Includes) {
			Write(prefix);
			Write(name);
			WriteLine(suffix);
		}
	}

	protected void WriteTopLevelNatives(CiProgram program)
	{
		foreach (string content in program.TopLevelNatives)
			Write(content);
	}

	protected void OpenBlock()
	{
		WriteLine('{');
		this.Indent++;
	}

	protected void CloseBlock()
	{
		this.Indent--;
		WriteLine('}');
	}

	protected void WriteComma(int i)
	{
		if (i > 0) {
			if ((i & 15) == 0) {
				WriteLine(',');
				Write('\t');
			}
			else
				Write(", ");
		}
	}

	protected void Write(byte[] array)
	{
		for (int i = 0; i < array.Length; i++) {
			WriteComma(i);
			Write(array[i]);
		}
	}

	protected void WriteEscapedChar(char c)
	{
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

	protected virtual void WriteLiteral(object value)
	{
		switch (value) {
		case null:
			Write("null");
			break;
		case long l:
			Write(l);
			break;
		case bool b:
			Write(b ? "true" : "false");
			break;
		case string s:
			Write('"');
			foreach (char c in s)
				WriteEscapedChar(c);
			Write('"');
			break;
		case double d:
			Write(d.ToString("R", CultureInfo.InvariantCulture));
			break;
		default:
			throw new NotImplementedException(value.GetType().Name);
		}
	}

	public override CiExpr Visit(CiLiteral expr, CiPriority parent)
	{
		WriteLiteral(expr.Value);
		return expr;
	}

	protected abstract void WriteName(CiSymbol symbol);

	protected virtual TypeCode GetIntegerTypeCode(CiIntegerType integer, bool promote)
	{
		if (integer == CiSystem.LongType)
			return TypeCode.Int64;
		if (promote || integer == CiSystem.IntType)
			return TypeCode.Int32;
		CiRangeType range = (CiRangeType) integer;
		if (range.Min < 0) {
			if (range.Min < short.MinValue || range.Max > short.MaxValue)
				return TypeCode.Int32;
			if (range.Min < sbyte.MinValue || range.Max > sbyte.MaxValue)
				return TypeCode.Int16;
			return TypeCode.SByte;
		}
		if (range.Max > ushort.MaxValue)
			return TypeCode.Int32;
		if (range.Max > byte.MaxValue)
			return TypeCode.UInt16;
		return TypeCode.Byte;
	}

	protected TypeCode GetTypeCode(CiType type, bool promote)
	{
		if (type is CiNumericType) {
			if (type is CiIntegerType integer)
				return GetIntegerTypeCode(integer, promote);
			if (type == CiSystem.DoubleType)
				return TypeCode.Double;
			if (type == CiSystem.FloatType || type == CiSystem.FloatIntType)
				return TypeCode.Single;
			throw new NotImplementedException(type.ToString());
		}
		else if (type == CiSystem.BoolType)
			return TypeCode.Boolean;
		else if (type == CiSystem.NullType)
			return TypeCode.Empty;
		else if (type is CiStringType)
			return TypeCode.String;
		return TypeCode.Object;
	}

	protected abstract void WriteTypeAndName(CiNamedValue value);

	protected virtual void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiField)
			Write("this.");
		WriteName(symbol);
	}

	protected void WriteEscapingBrace(string s)
	{
		foreach (char c in s) {
			if (c == '{')
				Write("{{");
			else
				WriteEscapedChar(c);
		}
	}

	void WritePrintfLiteral(string s)
	{
		foreach (char c in s) {
			if (c == '%')
				Write("%%");
			else
				WriteEscapedChar(c);
		}
	}

	protected virtual void WritePrintfWidth(CiInterpolatedPart part)
	{
		if (part.WidthExpr != null)
			Write(part.Width);
		if (part.Precision >= 0) {
			Write('.');
			Write(part.Precision);
		}
	}

	static char GetPrintfFormat(CiType type, char format)
	{
		switch (type) {
		case CiStringType _:
			return 's';
		case CiIntegerType _:
			return format == 'x' || format == 'X' ? format : 'd';
		case CiNumericType _:
			return "EefGg".IndexOf(format) >= 0 ? format : format == 'F' ? 'f' : 'g';
		default:
			throw new NotImplementedException(type.ToString());
		}
	}

	protected void WriteArgs(CiInterpolatedString expr)
	{
		foreach (CiInterpolatedPart part in expr.Parts) {
			Write(", ");
			part.Argument.Accept(this, CiPriority.Statement);
		}
	}

	protected void WritePrintf(CiInterpolatedString expr, bool newLine)
	{
		Write('"');
		foreach (CiInterpolatedPart part in expr.Parts) {
			WritePrintfLiteral(part.Prefix);
			Write('%');
			WritePrintfWidth(part);
			Write(GetPrintfFormat(part.Argument.Type, part.Format));
		}
		WritePrintfLiteral(expr.Suffix);
		if (newLine)
			Write("\\n");
		Write('"');
		WriteArgs(expr);
		Write(')');
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left == null)
			WriteLocalName(expr.Symbol, parent);
		else if (expr.Symbol == CiSystem.StringLength)
			WriteStringLength(expr.Left);
		else {
			expr.Left.Accept(this, CiPriority.Primary);
			WriteMemberOp(expr.Left, expr);
			WriteName(expr.Symbol);
		}
		return expr;
	}

	protected virtual void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		expr.Accept(this, parent);
	}

	protected virtual void WriteCoerced(CiType type, CiCondExpr expr, CiPriority parent)
	{
		if (parent > CiPriority.Cond)
			Write('(');
		expr.Cond.Accept(this, CiPriority.Cond);
		Write(" ? ");
		WriteCoerced(type, expr.OnTrue, CiPriority.Cond);
		Write(" : ");
		WriteCoerced(type, expr.OnFalse, CiPriority.Cond);
		if (parent > CiPriority.Cond)
			Write(')');
	}

	protected void WriteCoerced(CiType type, CiExpr expr, CiPriority parent)
	{
		if (expr is CiCondExpr cond)
			WriteCoerced(type, cond, parent);
		else
			WriteCoercedInternal(type, expr, parent);
	}

	protected virtual void WriteCoercedLiteral(CiType type, object value)
	{
		WriteLiteral(value);
	}

	protected void WriteCoercedLiterals(CiType type, CiExpr[] exprs)
	{
		for (int i = 0; i < exprs.Length; i++) {
			WriteComma(i);
			WriteCoercedLiteral(type, ((CiLiteral) exprs[i]).Value);
		}
	}

	protected void WriteArgs(CiMethod method, CiExpr[] args)
	{
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i >= args.Length)
				break;
			if (i > 0)
				Write(", ");
			WriteCoerced(param.Type, args[i++], CiPriority.Statement);
		}
	}

	protected void WriteArgsInParentheses(CiMethod method, CiExpr[] args)
	{
		Write('(');
		WriteArgs(method, args);
		Write(')');
	}

	protected virtual void WriteNew(CiClass klass, CiPriority parent)
	{
		Write("new ");
		Write(klass.Name);
		Write("()");
	}

	protected abstract void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent);

	protected void WriteNewArray(CiArrayStorageType array)
	{
		WriteNewArray(array.ElementType, array.LengthExpr, CiPriority.Statement);
	}

	protected virtual void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		Write(" = ");
		WriteNewArray(array);
	}

	protected void WriteNewStorage(CiType type)
	{
		if (type is CiClass klass)
			WriteNew(klass, CiPriority.Statement);
		else if (type is CiArrayStorageType array)
			WriteNewArray(array);
	}

	protected abstract void WriteListStorageInit(CiListType list);

	protected abstract void WriteDictionaryStorageInit(CiDictionaryType dict);

	protected virtual void WriteVarInit(CiNamedValue def)
	{
		if (def.Type is CiClass klass) {
			Write(" = ");
			WriteNew(klass, CiPriority.Statement);
		}
		else if (def.Type is CiArrayStorageType array && !(def.Value is CiCollection))
			WriteArrayStorageInit(array, def.Value);
		else if (def.Type is CiListType list)
			WriteListStorageInit(list);
		else if (def.Type is CiDictionaryType dict)
			WriteDictionaryStorageInit(dict);
		else if (def.Value != null) {
			Write(" = ");
			WriteCoerced(def.Type, def.Value, CiPriority.Statement);
		}
	}

	protected virtual void WriteVar(CiNamedValue def)
	{
		WriteTypeAndName(def);
		WriteVarInit(def);
	}

	public override CiExpr Visit(CiVar expr, CiPriority parent)
	{
		WriteVar(expr);
		return expr;
	}

	protected void OpenLoop(string intString, int nesting, int count)
	{
		Write("for (");
		Write(intString);
		Write(" _i");
		Write(nesting);
		Write(" = 0; _i");
		Write(nesting);
		Write(" < ");
		Write(count);
		Write("; _i");
		Write(nesting);
		Write("++) ");
		OpenBlock();
	}

	protected void WriteArrayElement(CiNamedValue def, int nesting)
	{
		WriteLocalName(def, CiPriority.Primary);
		for (int i = 0; i < nesting; i++) {
			Write("[_i");
			Write(i);
			Write(']');
		}
	}

	protected abstract void WriteInitCode(CiNamedValue def);

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
			// FIXME: - --foo[bar]
			if (expr.Inner is CiPrefixExpr inner && (inner.Op == CiToken.Minus || inner.Op == CiToken.Decrement))
				Write(' ');
			break;
		case CiToken.Tilde:
			Write('~');
			break;
		case CiToken.ExclamationMark:
			Write('!');
			break;
		case CiToken.New:
			if (expr.Type is CiClassPtrType klass)
				WriteNew(klass.Class, parent);
			else
				WriteNewArray(((CiArrayType) expr.Type).ElementType, expr.Inner, parent);
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

	static bool IsBitOp(CiPriority parent)
	{
		switch (parent) {
		case CiPriority.Or:
		case CiPriority.Xor:
		case CiPriority.And:
		case CiPriority.Shift:
			return true;
		default:
			return false;
		}
	}

	protected void WriteAdd(CiExpr left, CiExpr right)
	{
		if (left is CiLiteral leftLiteral) {
			long leftValue = (long) leftLiteral.Value;
			if (leftValue == 0) {
				right.Accept(this, CiPriority.Statement);
				return;
			}
			if (right is CiLiteral rightLiteral) {
				Write(leftValue + (long) rightLiteral.Value);
				return;
			}
		}
		else if (right is CiLiteral rightLiteral2 && (long) rightLiteral2.Value == 0) {
			left.Accept(this, CiPriority.Statement);
			return;
		}
		left.Accept(this, CiPriority.Add);
		Write(" + ");
		right.Accept(this, CiPriority.Add);
	}

	protected CiExpr Write(CiBinaryExpr expr, bool parentheses, CiPriority left, string op, CiPriority right)
	{
		if (parentheses)
			Write('(');
		expr.Left.Accept(this, left);
		Write(op);
		expr.Right.Accept(this, right);
		if (parentheses)
			Write(')');
		return expr;
	}

	protected CiExpr Write(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
	{
		return Write(expr, parent > child, child, op, child);
	}

	protected virtual void WriteComparison(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
	{
		Write(expr, parent, child, op);
	}

	protected virtual void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		WriteComparison(expr, parent, CiPriority.Equality, not ? " != " : " == ");
	}

	protected virtual void WriteAnd(CiBinaryExpr expr, CiPriority parent)
	{
		Write(expr, parent > CiPriority.CondAnd && parent != CiPriority.And, CiPriority.And, " & ", CiPriority.And);
	}

	protected virtual void WriteAssignRight(CiBinaryExpr expr)
	{
		expr.Right.Accept(this, CiPriority.Statement);
	}

	protected virtual void WriteAssign(CiBinaryExpr expr, CiPriority parent)
	{
		if (parent > CiPriority.Assign)
			Write('(');
		expr.Left.Accept(this, CiPriority.Assign);
		Write(" = ");
		WriteAssignRight(expr);
		if (parent > CiPriority.Assign)
			Write(')');
	}

	protected virtual void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		Write('.');
	}

	protected abstract void WriteStringLength(CiExpr expr);

	protected abstract void WriteCharAt(CiBinaryExpr expr);

	protected abstract void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent);

	protected virtual void WriteNearCall(CiMethod method, CiExpr[] args)
	{
		WriteName(method);
		WriteArgsInParentheses(method, args);
	}

	protected virtual void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		expr.Left.Accept(this, CiPriority.Primary);
		Write('[');
		expr.Right.Accept(this, CiPriority.Statement);
		Write(']');
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Plus:
			return Write(expr, parent > CiPriority.Add || IsBitOp(parent), CiPriority.Add, " + ", CiPriority.Add);
		case CiToken.Minus:
			return Write(expr, parent > CiPriority.Add || IsBitOp(parent), CiPriority.Add, " - ", CiPriority.Mul);
		case CiToken.Asterisk:
			return Write(expr, parent, CiPriority.Mul, " * ");
		case CiToken.Slash:
			return Write(expr, parent > CiPriority.Mul, CiPriority.Mul, " / ", CiPriority.Primary);
		case CiToken.Mod:
			return Write(expr, parent > CiPriority.Mul, CiPriority.Mul, " % ", CiPriority.Primary);
		case CiToken.ShiftLeft:
			return Write(expr, parent > CiPriority.Shift, CiPriority.Shift, " << ", CiPriority.Mul);
		case CiToken.ShiftRight:
			return Write(expr, parent > CiPriority.Shift, CiPriority.Shift, " >> ", CiPriority.Mul);
		case CiToken.Less:
			WriteComparison(expr, parent, CiPriority.Rel, " < ");
			return expr;
		case CiToken.LessOrEqual:
			WriteComparison(expr, parent, CiPriority.Rel, " <= ");
			return expr;
		case CiToken.Greater:
			WriteComparison(expr, parent, CiPriority.Rel, " > ");
			return expr;
		case CiToken.GreaterOrEqual:
			WriteComparison(expr, parent, CiPriority.Rel, " >= ");
			return expr;
		case CiToken.Equal:
			WriteEqual(expr, parent, false);
			return expr;
		case CiToken.NotEqual:
			WriteEqual(expr, parent, true);
			return expr;
		case CiToken.And:
			WriteAnd(expr, parent);
			return expr;
		case CiToken.Or:
			return Write(expr, parent, CiPriority.Or, " | ");
		case CiToken.Xor:
			return Write(expr, parent > CiPriority.Xor || parent == CiPriority.Or, CiPriority.Xor, " ^ ", CiPriority.Xor);
		case CiToken.CondAnd:
			return Write(expr, parent > CiPriority.CondAnd || parent == CiPriority.CondOr, CiPriority.CondAnd, " && ", CiPriority.CondAnd);
		case CiToken.CondOr:
			return Write(expr, parent, CiPriority.CondOr, " || ");
		case CiToken.Assign:
			WriteAssign(expr, parent);
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
			if (parent > CiPriority.Assign)
				Write('(');
			expr.Left.Accept(this, CiPriority.Assign);
			Write(' ');
			Write(expr.OpString);
			Write(' ');
			expr.Right.Accept(this, CiPriority.Statement);
			if (parent > CiPriority.Assign)
				Write(')');
			return expr;

		case CiToken.LeftBracket:
			if (expr.Left.Type is CiStringType)
				WriteCharAt(expr);
			else
				WriteIndexing(expr, parent);
			return expr;

		default:
			throw new ArgumentException(expr.Op.ToString());
		}
	}

	public override CiExpr Visit(CiCondExpr expr, CiPriority parent)
	{
		WriteCoerced(expr.Type, expr, parent);
		return expr;
	}

	public override CiExpr Visit(CiCallExpr expr, CiPriority parent)
	{
		if (expr.Method.Left != null)
			WriteCall(expr.Method.Left, (CiMethod) expr.Method.Symbol, expr.Arguments, parent);
		else
			WriteNearCall((CiMethod) expr.Method.Symbol, expr.Arguments);
		return expr;
	}

	public override void Visit(CiExpr statement)
	{
		statement.Accept(this, CiPriority.Statement);
		WriteLine(';');
		if (statement is CiVar def)
			WriteInitCode(def);
	}

	public override void Visit(CiConst statement)
	{
	}

	protected void Write(CiStatement[] statements, int length)
	{
		for (int i = 0; i < length; i++)
			statements[i].Accept(this);
	}

	protected virtual void Write(CiStatement[] statements)
	{
		Write(statements, statements.Length);
	}

	public override void Visit(CiBlock statement)
	{
		OpenBlock();
		Write(statement.Statements);
		CloseBlock();
	}

	protected virtual void WriteChild(CiStatement statement)
	{
		if (statement is CiBlock block) {
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

	public override void Visit(CiNative statement)
	{
		Write(statement.Content);
	}

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return;");
		else {
			Write("return ");
			WriteCoerced(this.CurrentMethod.Type, statement.Value, CiPriority.Statement);
			WriteLine(';');
		}
	}

	protected virtual void WriteCaseBody(CiStatement[] statements)
	{
		Write(statements);
	}

	public override void Visit(CiSwitch statement)
	{
		Write("switch (");
		statement.Value.Accept(this, CiPriority.Statement);
		WriteLine(") {");
		foreach (CiCase kase in statement.Cases) {
			foreach (CiExpr value in kase.Values) {
				Write("case ");
				WriteCoerced(statement.Value.Type, value, CiPriority.Statement);
				WriteLine(':');
			}
			this.Indent++;
			WriteCaseBody(kase.Body);
			this.Indent--;
		}
		if (statement.DefaultBody != null) {
			WriteLine("default:");
			this.Indent++;
			WriteCaseBody(statement.DefaultBody);
			this.Indent--;
		}
		WriteLine('}');
	}

	public override void Visit(CiWhile statement)
	{
		Write("while (");
		statement.Cond.Accept(this, CiPriority.Statement);
		Write(')');
		WriteChild(statement.Body);
	}

	protected virtual void WriteParameter(CiVar param)
	{
		WriteTypeAndName(param);
	}

	protected void WriteParameters(CiMethod method, bool first, bool defaultArguments)
	{
		foreach (CiVar param in method.Parameters) {
			if (!first)
				Write(", ");
			first = false;
			WriteParameter(param);
			if (defaultArguments)
				WriteVarInit(param);
		}
		Write(')');
	}

	protected void WriteParameters(CiMethod method, bool defaultArguments)
	{
		Write('(');
		WriteParameters(method, true, defaultArguments);
	}

	protected void WriteConstructorBody(CiClass klass)
	{
		if (klass.Constructor != null) {
			this.CurrentMethod = klass.Constructor;
			Write(((CiBlock) klass.Constructor.Body).Statements);
			this.CurrentMethod = null;
		}
	}

	protected void WriteBody(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			WriteLine(';');
		else {
			WriteLine();
			this.CurrentMethod = method;
			OpenBlock();
			if (method.Body is CiBlock block)
				Write(block.Statements);
			else
				method.Body.Accept(this);
			CloseBlock();
			this.CurrentMethod = null;
		}
	}

	protected void WritePublic(CiContainerType container)
	{
		if (container.IsPublic)
			Write("public ");
	}

	protected void OpenClass(CiClass klass, string suffix, string extendsClause)
	{
		Write("class ");
		Write(klass.Name);
		Write(suffix);
		if (klass.BaseClassName != null) {
			Write(extendsClause);
			Write(klass.BaseClassName);
		}
		WriteLine();
		OpenBlock();
	}

	public abstract void Write(CiProgram program);
}

}
