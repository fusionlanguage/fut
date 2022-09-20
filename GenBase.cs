// GenBase.cs - base class for code generators
//
// Copyright (C) 2011-2022  Piotr Fusik
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
using System.Text.RegularExpressions;

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

	public override void VisitLiteralLong(long i) => this.Writer.Write(i);

	protected virtual int GetLiteralChars() => 0;

	public override void VisitLiteralChar(int c)
	{
		if (c < GetLiteralChars()) {
			Write('\'');
			switch (c) {
			case '\n': Write("\\n"); break;
			case '\r': Write("\\r"); break;
			case '\t': Write("\\t"); break;
			case '\'': Write("\\'"); break;
			case '\\': Write("\\\\"); break;
			default: Write((char) c); break;
			}
			Write('\'');
		}
		else
			this.Writer.Write(c);
	}

	protected void WriteLowercase(string s)
	{
		StartLine();
		foreach (char c in s)
			this.Writer.Write(char.ToLowerInvariant(c));
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

	#region JavaDoc

	protected virtual void StartDocLine() => Write(" * ");

	protected void WriteXmlDoc(string text)
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

	protected virtual void Write(CiDocPara para, bool many)
	{
		if (many) {
			WriteLine();
			Write(" * <p>");
		}
		foreach (CiDocInline inline in para.Children) {
			switch (inline) {
			case CiDocText text:
				WriteXmlDoc(text.Text);
				break;
			case CiDocCode code:
				Write("<code>");
				WriteXmlDoc(code.Text);
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
			Write(item, false);
			WriteLine("</li>");
		}
		Write(" * </ul>");
	}

	protected void Write(CiDocBlock block, bool many)
	{
		switch (block) {
		case CiDocPara para:
			Write(para, many);
			break;
		case CiDocList list:
			Write(list);
			break;
		default:
			throw new ArgumentException(block.GetType().Name);
		}
	}

	protected void WriteContent(CiCodeDoc doc)
	{
		StartDocLine();
		Write(doc.Summary, false);
		WriteLine();
		if (doc.Details.Count > 0) {
			StartDocLine();
			if (doc.Details.Count == 1)
				Write(doc.Details[0], false);
			else {
				foreach (CiDocBlock block in doc.Details)
					Write(block, true);
			}
			WriteLine();
		}
	}

	protected virtual void Write(CiCodeDoc doc)
	{
		if (doc != null) {
			WriteLine("/**");
			WriteContent(doc);
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
		WriteLine("/**");
		WriteContent(method.Documentation);
		WriteSelfDoc(method);
		foreach (CiVar param in method.Parameters) {
			if (param.Documentation != null) {
				Write(" * @param ");
				WriteName(param);
				Write(' ');
				Write(param.Documentation.Summary, false);
				WriteLine();
			}
		}
		WriteLine(" */");
	}

	#endregion JavaDoc

	protected virtual void WriteBanner() => WriteLine("// Generated automatically with \"cito\". Do not edit.");

	protected void CreateFile(string filename)
	{
		this.Writer = CreateTextWriter(filename);
		WriteBanner();
	}

	protected void CloseFile() => this.Writer.Close();

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

	protected void Include(string name) => this.Includes.Add(name);

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
			VisitLiteralLong(array[i]);
		}
	}

	public override void VisitLiteralDouble(double value)
	{
		string s = value.ToString("R", CultureInfo.InvariantCulture);
		Write(s);
		foreach (char c in s) {
			switch (c) {
			case '-':
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
				break;
			default:
				return;
			}
		}
		Write(".0"); // it looked like an integer
	}

	public override void VisitLiteralNull() => Write("null");

	public override void VisitLiteralFalse() => Write("false");

	public override void VisitLiteralTrue() => Write("true");

	public override void VisitLiteralString(string value)
	{
		Write('"');
		Write(value);
		Write('"');
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

	protected virtual void WriteClassName(CiClass klass) => WriteName(klass);

	protected abstract void WriteTypeAndName(CiNamedValue value);

	protected virtual void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiField)
			Write("this.");
		WriteName(symbol);
	}

	protected void WriteDoubling(string s, char doubled)
	{
		foreach (char c in s) {
			if (c == doubled)
				Write(c);
			Write(c);
		}
	}

	protected virtual void WritePrintfWidth(CiInterpolatedPart part)
	{
		if (part.WidthExpr != null)
			VisitLiteralLong(part.Width);
		if (part.Precision >= 0) {
			Write('.');
			VisitLiteralLong(part.Precision);
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

	protected virtual void WriteInterpolatedStringArg(CiExpr expr) => expr.Accept(this, CiPriority.Argument);

	protected void WriteArgs(CiInterpolatedString expr)
	{
		foreach (CiInterpolatedPart part in expr.Parts) {
			Write(", ");
			WriteInterpolatedStringArg(part.Argument);
		}
	}

	protected void WritePrintf(CiInterpolatedString expr, bool newLine)
	{
		Write('"');
		foreach (CiInterpolatedPart part in expr.Parts) {
			WriteDoubling(part.Prefix, '%');
			Write('%');
			WritePrintfWidth(part);
			Write(GetPrintfFormat(part.Argument.Type, part.Format));
		}
		WriteDoubling(expr.Suffix, '%');
		if (newLine)
			Write("\\n");
		Write('"');
		WriteArgs(expr);
		Write(')');
	}

	protected bool WriteJavaMatchProperty(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.MatchStart:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".start()");
			return true;
		case CiId.MatchEnd:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".end()");
			return true;
		case CiId.MatchLength:
			if (parent > CiPriority.Add)
				Write('(');
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".end() - ");
			expr.Left.Accept(this, CiPriority.Primary); // FIXME: side effect
			Write(".start()");
			if (parent > CiPriority.Add)
				Write(')');
			return true;
		case CiId.MatchValue:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".group()");
			return true;
		default:
			return false;
		}
	}

	public override CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left == null)
			WriteLocalName(expr.Symbol, parent);
		else if (expr.Symbol.Id == CiId.StringLength)
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

	protected virtual void WriteCoerced(CiType type, CiSelectExpr expr, CiPriority parent)
	{
		if (parent > CiPriority.Select)
			Write('(');
		expr.Cond.Accept(this, CiPriority.Select);
		Write(" ? ");
		WriteCoerced(type, expr.OnTrue, CiPriority.Select);
		Write(" : ");
		WriteCoerced(type, expr.OnFalse, CiPriority.Select);
		if (parent > CiPriority.Select)
			Write(')');
	}

	protected void WriteCoerced(CiType type, CiExpr expr, CiPriority parent)
	{
		if (expr is CiSelectExpr select)
			WriteCoerced(type, select, parent);
		else
			WriteCoercedInternal(type, expr, parent);
	}

	protected virtual void WriteCoercedLiteral(CiType type, CiExpr literal)
	{
		literal.Accept(this, CiPriority.Argument);
	}

	protected void WriteCoercedLiterals(CiType type, List<CiExpr> exprs)
	{
		for (int i = 0; i < exprs.Count; i++) {
			WriteComma(i);
			WriteCoercedLiteral(type, exprs[i]);
		}
	}

	protected void WriteArgs(CiMethod method, List<CiExpr> args)
	{
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i >= args.Count)
				break;
			if (i > 0)
				Write(", ");
			WriteCoerced(param.Type, args[i++], CiPriority.Argument);
		}
	}

	protected void WriteArgsInParentheses(CiMethod method, List<CiExpr> args)
	{
		Write('(');
		WriteArgs(method, args);
		Write(')');
	}

	protected void WriteCall(string function, CiExpr arg0)
	{
		Write(function);
		Write('(');
		arg0.Accept(this, CiPriority.Argument);
		Write(')');
	}

	protected void WriteCall(string function, CiExpr arg0, CiExpr arg1)
	{
		Write(function);
		Write('(');
		arg0.Accept(this, CiPriority.Argument);
		Write(", ");
		arg1.Accept(this, CiPriority.Argument);
		Write(')');
	}

	protected void WriteCall(string function, CiExpr arg0, CiExpr arg1, CiExpr arg2)
	{
		Write(function);
		Write('(');
		arg0.Accept(this, CiPriority.Argument);
		Write(", ");
		arg1.Accept(this, CiPriority.Argument);
		Write(", ");
		arg2.Accept(this, CiPriority.Argument);
		Write(')');
	}

	protected void WriteCall(string function, CiExpr arg0, List<CiExpr> args)
	{
		Write(function);
		Write('(');
		arg0.Accept(this, CiPriority.Argument);
		foreach (CiExpr arg in args) {
			Write(", ");
			arg.Accept(this, CiPriority.Argument);
		}
		Write(')');
	}

	protected void WriteCall(CiExpr obj, string method, CiExpr arg0)
	{
		obj.Accept(this, CiPriority.Primary);
		WriteMemberOp(obj, null);
		WriteCall(method, arg0);
	}

	protected void WriteCall(CiExpr obj, string method, CiExpr arg0, CiExpr arg1)
	{
		obj.Accept(this, CiPriority.Primary);
		WriteMemberOp(obj, null);
		WriteCall(method, arg0, arg1);
	}

	protected virtual void WriteNew(CiClass klass, CiPriority parent)
	{
		Write("new ");
		WriteClassName(klass);
		Write("()");
	}

	protected abstract void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent);

	protected virtual void WriteNewArray(CiArrayStorageType array)
	{
		WriteNewArray(array.ElementType, array.LengthExpr, CiPriority.Argument);
	}

	protected abstract void WriteNewStorage(CiStorageType storage);

	protected void WriteNewStorage(CiType type)
	{
		switch (type) {
		case CiClass klass:
			WriteNew(klass, CiPriority.Argument);
			break;
		case CiArrayStorageType array:
			WriteNewArray(array);
			break;
		case CiStorageType storage:
			WriteNewStorage(storage);
			break;
		default:
			throw new NotImplementedException();
		}
	}

	protected virtual void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		Write(" = ");
		WriteNewArray(array);
	}

	protected virtual void WriteCoercedExpr(CiType type, CiExpr expr)
	{
		WriteCoerced(type, expr, CiPriority.Argument);
	}

	protected virtual void WriteVarInit(CiNamedValue def)
	{
		if (def.IsAssignableStorage) {
		}
		else if (def.Type is CiArrayStorageType array && !(def.Value is CiAggregateInitializer))
			WriteArrayStorageInit(array, def.Value);
		else if (def.Value != null) {
			Write(" = ");
			WriteCoercedExpr(def.Type, def.Value);
		}
		else if (def.Type.IsFinal && !(def.Parent is CiParameters)) {
			Write(" = ");
			WriteNewStorage(def.Type);
		}
	}

	protected virtual void WriteVar(CiNamedValue def)
	{
		WriteTypeAndName(def);
		WriteVarInit(def);
	}

	public override void VisitVar(CiVar expr) => WriteVar(expr);

	protected void OpenLoop(string intString, int nesting, int count)
	{
		Write("for (");
		Write(intString);
		Write(" _i");
		VisitLiteralLong(nesting);
		Write(" = 0; _i");
		VisitLiteralLong(nesting);
		Write(" < ");
		VisitLiteralLong(count);
		Write("; _i");
		VisitLiteralLong(nesting);
		Write("++) ");
		OpenBlock();
	}

	protected void WriteArrayElement(CiNamedValue def, int nesting)
	{
		WriteLocalName(def, CiPriority.Primary);
		for (int i = 0; i < nesting; i++) {
			Write("[_i");
			VisitLiteralLong(i);
			Write(']');
		}
	}

	protected abstract void WriteInitCode(CiNamedValue def);

	protected abstract void WriteResource(string name, int length);

	public override CiExpr VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
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
			CiClassType klass = (CiClassType) expr.Type;
			if (klass.Class == CiSystem.ArrayPtrClass)
				WriteNewArray(klass.ElementType, expr.Inner, parent);
			else
				WriteNew(klass.Class, parent);
			return expr;
		case CiToken.Resource:
			WriteResource(((CiLiteralString) expr.Inner).Value, ((CiArrayStorageType) expr.Type).Length);
			return expr;
		default:
			throw new ArgumentException(expr.Op.ToString());
		}
		expr.Inner.Accept(this, CiPriority.Primary);
		return expr;
	}

	public override CiExpr VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent)
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
		if (left is CiLiteralLong leftLiteral) {
			long leftValue = leftLiteral.Value;
			if (leftValue == 0) {
				right.Accept(this, CiPriority.Argument);
				return;
			}
			if (right is CiLiteralLong rightLiteral) {
				VisitLiteralLong(leftValue + rightLiteral.Value);
				return;
			}
		}
		else if (right.IsLiteralZero) {
			left.Accept(this, CiPriority.Argument);
			return;
		}
		left.Accept(this, CiPriority.Add);
		Write(" + ");
		right.Accept(this, CiPriority.Add);
	}

	protected void WriteStartEnd(CiExpr startIndex, CiExpr length)
	{
		startIndex.Accept(this, CiPriority.Argument);
		Write(", ");
		WriteAdd(startIndex, length); // TODO: side effect
	}

	protected virtual void Write(CiExpr expr, CiPriority parent, CiBinaryExpr binary)
	{
		expr.Accept(this, parent);
	}

	protected CiExpr Write(CiBinaryExpr expr, bool parentheses, CiPriority left, string op, CiPriority right)
	{
		if (parentheses)
			Write('(');
		Write(expr.Left, left, expr);
		Write(op);
		Write(expr.Right, right, expr);
		if (parentheses)
			Write(')');
		return expr;
	}

	protected CiExpr Write(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
	{
		return Write(expr, parent > child, child, op, child);
	}

	protected static string GetEqOp(bool not) => not ? " != " : " == ";

	protected virtual void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		Write(expr, parent, CiPriority.Equality, GetEqOp(not));
	}

	protected virtual void WriteAnd(CiBinaryExpr expr, CiPriority parent)
	{
		Write(expr, parent > CiPriority.CondAnd && parent != CiPriority.And, CiPriority.And, " & ", CiPriority.And);
	}

	protected virtual void WriteAssignRight(CiBinaryExpr expr)
	{
		expr.Right.Accept(this, CiPriority.Argument);
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

	protected virtual void WriteMemberOp(CiExpr left, CiSymbolReference symbol) => Write('.');

	protected abstract void WriteStringLength(CiExpr expr);

	protected abstract void WriteCharAt(CiBinaryExpr expr);

	protected virtual void WriteNotPromoted(CiType type, CiExpr expr)
	{
		expr.Accept(this, CiPriority.Argument);
	}

	protected void WriteListAdd(CiExpr obj, string method, List<CiExpr> args)
	{
		obj.Accept(this, CiPriority.Primary);
		Write('.');
		Write(method);
		Write('(');
		CiType elementType = ((CiClassType) obj.Type).ElementType;
		if (args.Count == 0)
			WriteNewStorage(elementType);
		else
			WriteNotPromoted(elementType, args[0]);
		Write(')');
	}

	protected void WriteListInsert(CiExpr obj, string method, List<CiExpr> args, string separator = ", ")
	{
		obj.Accept(this, CiPriority.Primary);
		Write('.');
		Write(method);
		Write('(');
		args[0].Accept(this, CiPriority.Argument);
		Write(separator);
		CiType elementType = ((CiClassType) obj.Type).ElementType;
		if (args.Count == 1)
			WriteNewStorage(elementType);
		else
			WriteNotPromoted(elementType, args[1]);
		Write(')');
	}

	protected void WriteDictionaryAdd(CiExpr obj, List<CiExpr> args)
	{
		WriteIndexing(obj, args[0]);
		Write(" = ");
		WriteNewStorage(((CiClassType) obj.Type).ValueType);
	}

	protected bool WriteRegexOptions(List<CiExpr> args, string prefix, string separator, string suffix, string i, string m, string s)
	{
		CiExpr expr = args[args.Count - 1];
		if (expr.Type != CiSystem.RegexOptionsEnum)
			return false;
		RegexOptions options = (RegexOptions) expr.IntValue;
		if (options == RegexOptions.None)
			return false;
		Write(prefix);
		if (options.HasFlag(RegexOptions.IgnoreCase))
			Write(i);
		if (options.HasFlag(RegexOptions.Multiline)) {
			if (options.HasFlag(RegexOptions.IgnoreCase))
				Write(separator);
			Write(m);
		}
		if (options.HasFlag(RegexOptions.Singleline)) {
			if (options != RegexOptions.Singleline)
				Write(separator);
			Write(s);
		}
		Write(suffix);
		return true;
	}

	protected abstract void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent);

	protected void WriteIndexing(CiExpr collection, CiExpr index)
	{
		collection.Accept(this, CiPriority.Primary);
		Write('[');
		index.Accept(this, CiPriority.Argument);
		Write(']');
	}

	protected virtual void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		WriteIndexing(expr.Left, expr.Right);
	}

	protected virtual string IsOperator => " is ";

	public override CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
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
			expr.Right.Accept(this, CiPriority.Argument);
			if (parent > CiPriority.Assign)
				Write(')');
			return expr;

		case CiToken.LeftBracket:
			if (expr.Left.Type is CiStringType)
				WriteCharAt(expr);
			else
				WriteIndexing(expr, parent);
			return expr;

		case CiToken.Is:
			if (parent > CiPriority.Rel)
				Write('(');
			expr.Left.Accept(this, CiPriority.Rel);
			Write(IsOperator);
			switch (expr.Right) {
			case CiClass klass:
				WriteName(klass);
				break;
			case CiVar def:
				WriteTypeAndName(def);
				break;
			default:
				throw new NotImplementedException(expr.Right.ToString());
			}
			if (parent > CiPriority.Rel)
				Write(')');
			return expr;

		default:
			throw new ArgumentException(expr.Op.ToString());
		}
	}

	public override CiExpr VisitSelectExpr(CiSelectExpr expr, CiPriority parent)
	{
		WriteCoerced(expr.Type, expr, parent);
		return expr;
	}

	public override CiExpr VisitCallExpr(CiCallExpr expr, CiPriority parent)
	{
		WriteCall(expr.Method.Left, (CiMethod) expr.Method.Symbol, expr.Arguments, parent);
		return expr;
	}

	public override void VisitExpr(CiExpr statement)
	{
		statement.Accept(this, CiPriority.Statement);
		WriteLine(';');
		if (statement is CiVar def)
			WriteInitCode(def);
	}

	public override void VisitConst(CiConst statement)
	{
	}

	protected void Write(List<CiStatement> statements, int length)
	{
		for (int i = 0; i < length; i++)
			statements[i].Accept(this);
	}

	protected virtual void Write(List<CiStatement> statements)
	{
		Write(statements, statements.Count);
	}

	public override void VisitBlock(CiBlock statement)
	{
		OpenBlock();
		Write(statement.Statements);
		CloseBlock();
	}

	protected virtual void WriteChild(CiStatement statement)
	{
		if (statement is CiBlock block) {
			Write(' ');
			VisitBlock(block);
		}
		else {
			WriteLine();
			this.Indent++;
			statement.Accept(this);
			this.Indent--;
		}
	}

	public override void VisitBreak(CiBreak statement) => WriteLine("break;");

	public override void VisitContinue(CiContinue statement) => WriteLine("continue;");

	public override void VisitDoWhile(CiDoWhile statement)
	{
		Write("do");
		WriteChild(statement.Body);
		Write("while (");
		statement.Cond.Accept(this, CiPriority.Argument);
		WriteLine(");");
	}

	public override void VisitFor(CiFor statement)
	{
		Write("for (");
		statement.Init?.Accept(this, CiPriority.Statement);
		Write(';');
		if (statement.Cond != null) {
			Write(' ');
			statement.Cond.Accept(this, CiPriority.Argument);
		}
		Write(';');
		if (statement.Advance != null) {
			Write(' ');
			statement.Advance.Accept(this, CiPriority.Statement);
		}
		Write(')');
		WriteChild(statement.Body);
	}

	public override void VisitIf(CiIf statement)
	{
		Write("if (");
		statement.Cond.Accept(this, CiPriority.Argument);
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

	public override void VisitNative(CiNative statement) => Write(statement.Content);

	protected virtual void WriteReturnValue(CiExpr value) => WriteCoerced(this.CurrentMethod.Type, value, CiPriority.Argument);

	public override void VisitReturn(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return;");
		else {
			Write("return ");
			WriteReturnValue(statement.Value);
			WriteLine(';');
		}
	}

	protected virtual void WriteCaseBody(List<CiStatement> statements) => Write(statements);

	public override void VisitSwitch(CiSwitch statement)
	{
		Write("switch (");
		statement.Value.Accept(this, CiPriority.Argument);
		WriteLine(") {");
		foreach (CiCase kase in statement.Cases) {
			foreach (CiExpr value in kase.Values) {
				Write("case ");
				WriteCoercedLiteral(statement.Value.Type, value);
				WriteLine(':');
			}
			this.Indent++;
			WriteCaseBody(kase.Body);
			this.Indent--;
		}
		if (statement.DefaultBody.Count > 0) {
			WriteLine("default:");
			this.Indent++;
			WriteCaseBody(statement.DefaultBody);
			this.Indent--;
		}
		WriteLine('}');
	}

	public override void VisitWhile(CiWhile statement)
	{
		Write("while (");
		statement.Cond.Accept(this, CiPriority.Argument);
		Write(')');
		WriteChild(statement.Body);
	}

	protected virtual void WriteParameter(CiVar param) => WriteTypeAndName(param);

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

	protected void FlattenBlock(CiStatement statement)
	{
		if (statement is CiBlock block)
			Write(block.Statements);
		else
			statement.Accept(this);
	}

	protected void WriteBody(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			WriteLine(';');
		else {
			WriteLine();
			this.CurrentMethod = method;
			OpenBlock();
			FlattenBlock(method.Body);
			CloseBlock();
			this.CurrentMethod = null;
		}
	}

	protected void WritePublic(CiContainerType container)
	{
		if (container.IsPublic)
			Write("public ");
	}

	protected void WriteExplicitEnumValue(CiConst konst)
	{
		if (!(konst.Value is CiImplicitEnumValue)) {
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Argument);
		}
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

	protected abstract void WriteConst(CiConst konst);

	protected abstract void WriteField(CiField field);

	protected abstract void WriteMethod(CiMethod method);

	protected void WriteMembers(CiClass klass, bool constArrays)
	{
		foreach (CiSymbol member in klass) {
			switch (member) {
			case CiConst konst:
				WriteConst(konst);
				break;
			case CiField field:
				WriteField(field);
				break;
			case CiMethod method:
				WriteMethod(method);
				break;
			case CiVar _: // "this"
				break;
			default:
				throw new NotImplementedException(member.Type.ToString());
			}
		}
		if (constArrays) {
			foreach (CiConst konst in klass.ConstArrays)
				WriteConst(konst);
		}
	}

	public abstract void Write(CiProgram program);
}

}
