// GenBase.cs - base class for code generators
//
// Copyright (C) 2011-2023  Piotr Fusik
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

public abstract class GenBase : CiExprVisitor
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
	protected readonly HashSet<CiClass> WrittenClasses = new HashSet<CiClass>();
	protected readonly List<CiExpr> CurrentTemporaries = new List<CiExpr>(); // CiExpr or CiType

	protected override CiContainerType GetCurrentContainer() => (CiClass) this.CurrentMethod.Parent;
	protected abstract string GetTargetName();

	protected void NotSupported(CiStatement statement, string feature)
	{
		ReportError(statement, $"{feature} not supported when targeting {GetTargetName()}");
	}

	protected void NotYet(CiStatement statement, string feature)
	{
		ReportError(statement, $"{feature} not supported yet when targeting {GetTargetName()}");
	}

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

	protected void WriteChar(int c)
	{
		StartLine();
		this.Writer.Write((char) c);
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
			WriteChar('\'');
			switch (c) {
			case '\n': Write("\\n"); break;
			case '\r': Write("\\r"); break;
			case '\t': Write("\\t"); break;
			case '\'': Write("\\'"); break;
			case '\\': Write("\\\\"); break;
			default: WriteChar(c); break;
			}
			WriteChar('\'');
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
			default:
				WriteChar(c);
				break;
			}
		}
	}

	protected virtual void WriteDocPara(CiDocPara para, bool many)
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
			case CiDocLine _:
				WriteLine();
				StartDocLine();
				break;
			default:
				throw new ArgumentException(inline.GetType().Name);
			}
		}
	}

	protected virtual void WriteDocList(CiDocList list)
	{
		WriteLine();
		WriteLine(" * <ul>");
		foreach (CiDocPara item in list.Items) {
			Write(" * <li>");
			WriteDocPara(item, false);
			WriteLine("</li>");
		}
		Write(" * </ul>");
	}

	protected void WriteDocBlock(CiDocBlock block, bool many)
	{
		switch (block) {
		case CiDocPara para:
			WriteDocPara(para, many);
			break;
		case CiDocList list:
			WriteDocList(list);
			break;
		default:
			throw new ArgumentException(block.GetType().Name);
		}
	}

	protected void WriteContent(CiCodeDoc doc)
	{
		StartDocLine();
		WriteDocPara(doc.Summary, false);
		WriteLine();
		if (doc.Details.Count > 0) {
			StartDocLine();
			if (doc.Details.Count == 1)
				WriteDocBlock(doc.Details[0], false);
			else {
				foreach (CiDocBlock block in doc.Details)
					WriteDocBlock(block, true);
			}
			WriteLine();
		}
	}

	protected virtual void WriteDoc(CiCodeDoc doc)
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

	protected virtual void WriteParameterDoc(CiVar param, bool first)
	{
		Write(" * @param ");
		WriteName(param);
		WriteChar(' ');
		WriteDocPara(param.Documentation.Summary, false);
		WriteLine();
	}

	protected void WriteParametersDoc(CiMethod method)
	{
		bool first = true;
		for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
			if (param.Documentation != null) {
				WriteParameterDoc(param, first);
				first = false;
			}
		}
	}

	protected void WriteMethodDoc(CiMethod method)
	{
		if (method.Documentation == null)
			return;
		WriteLine("/**");
		WriteContent(method.Documentation);
		WriteSelfDoc(method);
		WriteParametersDoc(method);
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
				WriteChar('\t');
			}
			else
				Write(", ");
		}
	}

	protected void WriteBytes(byte[] array)
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
		WriteChar('"');
		Write(value);
		WriteChar('"');
	}

	protected abstract void WriteName(CiSymbol symbol);

	protected virtual TypeCode GetIntegerTypeCode(CiIntegerType integer, bool promote)
	{
		if (integer.Id == CiId.LongType)
			return TypeCode.Int64;
		if (promote || integer.Id == CiId.IntType)
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
		switch (type.Id) {
		case CiId.NullType:
			return TypeCode.Empty;
		case CiId.FloatType:
		case CiId.FloatIntType:
			return TypeCode.Single;
		case CiId.DoubleType:
			return TypeCode.Double;
		case CiId.BoolType:
			return TypeCode.Boolean;
		case CiId.StringPtrType:
		case CiId.StringStorageType:
			return TypeCode.String;
		default:
			if (type is CiIntegerType integer)
				return GetIntegerTypeCode(integer, promote);
			return TypeCode.Object;
		}
	}

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
				WriteChar(c);
			WriteChar(c);
		}
	}

	protected virtual void WritePrintfWidth(CiInterpolatedPart part)
	{
		if (part.WidthExpr != null)
			VisitLiteralLong(part.Width);
		if (part.Precision >= 0) {
			WriteChar('.');
			VisitLiteralLong(part.Precision);
		}
	}

	static int GetPrintfFormat(CiType type, int format)
	{
		switch (type) {
		case CiIntegerType _:
			return format == 'x' || format == 'X' ? format : 'd';
		case CiNumericType _:
			return "EefGg".IndexOf((char) format) >= 0 ? format : format == 'F' ? 'f' : 'g';
		// case CiStringType _: - matched by CiClassType
		case CiClassType _:
			return 's';
		default:
			throw new NotImplementedException(type.ToString());
		}
	}

	protected void WritePrintfFormat(CiInterpolatedString expr)
	{
		foreach (CiInterpolatedPart part in expr.Parts) {
			WriteDoubling(part.Prefix, '%');
			WriteChar('%');
			WritePrintfWidth(part);
			WriteChar(GetPrintfFormat(part.Argument.Type, part.Format));
		}
		WriteDoubling(expr.Suffix, '%');
	}

	protected virtual void WriteInterpolatedStringArg(CiExpr expr) => expr.Accept(this, CiPriority.Argument);

	protected void WriteInterpolatedStringArgs(CiInterpolatedString expr)
	{
		foreach (CiInterpolatedPart part in expr.Parts) {
			Write(", ");
			WriteInterpolatedStringArg(part.Argument);
		}
	}

	protected void WritePrintf(CiInterpolatedString expr, bool newLine)
	{
		WriteChar('"');
		WritePrintfFormat(expr);
		if (newLine)
			Write("\\n");
		WriteChar('"');
		WriteInterpolatedStringArgs(expr);
		WriteChar(')');
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
				WriteChar('(');
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".end() - ");
			expr.Left.Accept(this, CiPriority.Primary); // FIXME: side effect
			Write(".start()");
			if (parent > CiPriority.Add)
				WriteChar(')');
			return true;
		case CiId.MatchValue:
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".group()");
			return true;
		default:
			return false;
		}
	}

	public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
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
	}

	protected virtual void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		expr.Accept(this, parent);
	}

	protected virtual void WriteCoerced(CiType type, CiSelectExpr expr, CiPriority parent)
	{
		if (parent > CiPriority.Select)
			WriteChar('(');
		expr.Cond.Accept(this, CiPriority.Select);
		Write(" ? ");
		WriteCoerced(type, expr.OnTrue, CiPriority.Select);
		Write(" : ");
		WriteCoerced(type, expr.OnFalse, CiPriority.Select);
		if (parent > CiPriority.Select)
			WriteChar(')');
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

	protected static bool IsReferenceTo(CiExpr expr, CiId id) => expr is CiSymbolReference symbol && symbol.Symbol.Id == id;

	protected void WriteArgs(CiMethod method, List<CiExpr> args)
	{
		CiVar param = method.Parameters.FirstParameter();
		bool first = true;
		foreach (CiExpr arg in args) {
			if (!first)
				Write(", ");
			first = false;
			WriteStronglyCoerced(param.Type, arg);
			param = param.NextParameter();
		}
	}

	protected void WriteArgsInParentheses(CiMethod method, List<CiExpr> args)
	{
		WriteChar('(');
		WriteArgs(method, args);
		WriteChar(')');
	}

	protected void WriteCall(string function, CiExpr arg0)
	{
		Write(function);
		WriteChar('(');
		arg0.Accept(this, CiPriority.Argument);
		WriteChar(')');
	}

	protected void WriteCall(string function, CiExpr arg0, CiExpr arg1)
	{
		Write(function);
		WriteChar('(');
		arg0.Accept(this, CiPriority.Argument);
		Write(", ");
		arg1.Accept(this, CiPriority.Argument);
		WriteChar(')');
	}

	protected void WriteCall(string function, CiExpr arg0, CiExpr arg1, CiExpr arg2)
	{
		Write(function);
		WriteChar('(');
		arg0.Accept(this, CiPriority.Argument);
		Write(", ");
		arg1.Accept(this, CiPriority.Argument);
		Write(", ");
		arg2.Accept(this, CiPriority.Argument);
		WriteChar(')');
	}

	protected void WriteCall(string function, CiExpr arg0, List<CiExpr> args)
	{
		Write(function);
		WriteChar('(');
		arg0.Accept(this, CiPriority.Argument);
		foreach (CiExpr arg in args) {
			Write(", ");
			arg.Accept(this, CiPriority.Argument);
		}
		WriteChar(')');
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

	protected abstract void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent);

	protected virtual void WriteNewArray(CiArrayStorageType array)
	{
		WriteNewArray(array.GetElementType(), array.LengthExpr, CiPriority.Argument);
	}

	protected abstract void WriteNew(CiReadWriteClassType klass, CiPriority parent);

	protected void WriteNewStorage(CiType type)
	{
		switch (type) {
		case CiArrayStorageType array:
			WriteNewArray(array);
			break;
		case CiStorageType storage:
			WriteNew(storage, CiPriority.Argument);
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

	protected void WriteObjectLiteral(CiAggregateInitializer init, string separator)
	{
		string prefix = " { ";
		foreach (CiBinaryExpr field in init.Items) {
			Write(prefix);
			WriteName(((CiSymbolReference) field.Left).Symbol);
			Write(separator);
			WriteCoerced(field.Left.Type, field.Right, CiPriority.Argument);
			prefix = ", ";
		}
		Write(" }");
	}

	protected virtual void WriteNewWithFields(CiType type, CiAggregateInitializer init) => WriteNew((CiReadWriteClassType) type, CiPriority.Argument);

	protected virtual void WriteCoercedExpr(CiType type, CiExpr expr)
	{
		WriteCoerced(type, expr, CiPriority.Argument);
	}

	protected virtual void WriteStorageInit(CiNamedValue def)
	{
		Write(" = ");
		if (def.Value is CiAggregateInitializer init)
			WriteNewWithFields(def.Type, init);
		else
			WriteNewStorage(def.Type);
	}

	protected virtual void WriteVarInit(CiNamedValue def)
	{
		if (def.IsAssignableStorage()) {
		}
		else if (def.Type is CiArrayStorageType array)
			WriteArrayStorageInit(array, def.Value);
		else if (def.Value != null && !(def.Value is CiAggregateInitializer)) {
			Write(" = ");
			WriteCoercedExpr(def.Type, def.Value);
		}
		else if (def.Type.IsFinal() && !(def.Parent is CiParameters))
			WriteStorageInit(def);
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
			WriteChar(']');
		}
	}

	static CiAggregateInitializer GetAggregateInitializer(CiNamedValue def)
	{
		CiExpr expr = def.Value;
		if (def.Value is CiPrefixExpr unary)
			expr = unary.Inner;
		return expr as CiAggregateInitializer;
	}

	protected virtual void EndStatement() => WriteLine(';');

	void WriteAggregateInitField(CiExpr obj, CiBinaryExpr field)
	{
		CiSymbolReference fieldRef = (CiSymbolReference) field.Left;
		WriteMemberOp(obj, fieldRef);
		WriteName(fieldRef.Symbol);
		Write(" = ");
		WriteCoerced(fieldRef.Type, field.Right, CiPriority.Argument);
		EndStatement();
	}

	protected virtual void WriteInitCode(CiNamedValue def)
	{
		CiAggregateInitializer init = GetAggregateInitializer(def);
		if (init != null) {
			foreach (CiBinaryExpr field in init.Items) {
				WriteLocalName(def, CiPriority.Primary);
				WriteAggregateInitField(def, field);
			}
		}
	}

	protected abstract void WriteResource(string name, int length);

	public override void VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Increment:
			Write("++");
			break;
		case CiToken.Decrement:
			Write("--");
			break;
		case CiToken.Minus:
			WriteChar('-');
			// FIXME: - --foo[bar]
			if (expr.Inner is CiPrefixExpr inner && (inner.Op == CiToken.Minus || inner.Op == CiToken.Decrement))
				WriteChar(' ');
			break;
		case CiToken.Tilde:
			WriteChar('~');
			break;
		case CiToken.ExclamationMark:
			WriteChar('!');
			break;
		case CiToken.New:
			CiDynamicPtrType dynamic = (CiDynamicPtrType) expr.Type;
			if (dynamic.Class.Id == CiId.ArrayPtrClass)
				WriteNewArray(dynamic.GetElementType(), expr.Inner, parent);
			else if (expr.Inner is CiAggregateInitializer init) {
				int tempId = this.CurrentTemporaries.IndexOf(expr);
				if (tempId >= 0) {
					Write("citemp");
					VisitLiteralLong(tempId);
				}
				else
					WriteNewWithFields(dynamic, init);
			}
			else
				WriteNew(dynamic, parent);
			return;
		case CiToken.Resource:
			WriteResource(((CiLiteralString) expr.Inner).Value, ((CiArrayStorageType) expr.Type).Length);
			return;
		default:
			throw new ArgumentException(expr.Op.ToString());
		}
		expr.Inner.Accept(this, CiPriority.Primary);
	}

	public override void VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent)
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

	protected void StartAdd(CiExpr expr)
	{
		if (!expr.IsLiteralZero()) {
			expr.Accept(this, CiPriority.Add);
			Write(" + ");
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
		else if (right.IsLiteralZero()) {
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

	protected virtual void WriteBinaryOperand(CiExpr expr, CiPriority parent, CiBinaryExpr binary)
	{
		expr.Accept(this, parent);
	}

	protected void WriteBinaryExpr(CiBinaryExpr expr, bool parentheses, CiPriority left, string op, CiPriority right)
	{
		if (parentheses)
			WriteChar('(');
		WriteBinaryOperand(expr.Left, left, expr);
		Write(op);
		WriteBinaryOperand(expr.Right, right, expr);
		if (parentheses)
			WriteChar(')');
	}

	protected void WriteBinaryExpr2(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
	{
		WriteBinaryExpr(expr, parent > child, child, op, child);
	}

	protected static string GetEqOp(bool not) => not ? " != " : " == ";

	protected virtual void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		WriteBinaryExpr2(expr, parent, CiPriority.Equality, GetEqOp(not));
	}

	protected virtual void WriteAnd(CiBinaryExpr expr, CiPriority parent)
	{
		WriteBinaryExpr(expr, parent > CiPriority.CondAnd && parent != CiPriority.And, CiPriority.And, " & ", CiPriority.And);
	}

	protected virtual void WriteAssignRight(CiBinaryExpr expr)
	{
		expr.Right.Accept(this, CiPriority.Argument);
	}

	protected virtual void WriteAssign(CiBinaryExpr expr, CiPriority parent)
	{
		if (parent > CiPriority.Assign)
			WriteChar('(');
		expr.Left.Accept(this, CiPriority.Assign);
		Write(" = ");
		WriteAssignRight(expr);
		if (parent > CiPriority.Assign)
			WriteChar(')');
	}

	protected virtual void WriteMemberOp(CiExpr left, CiSymbolReference symbol) => WriteChar('.');

	protected abstract void WriteStringLength(CiExpr expr);

	protected abstract void WriteCharAt(CiBinaryExpr expr);

	protected virtual void WriteNotPromoted(CiType type, CiExpr expr)
	{
		expr.Accept(this, CiPriority.Argument);
	}

	protected void WriteListAdd(CiExpr obj, string method, List<CiExpr> args)
	{
		obj.Accept(this, CiPriority.Primary);
		WriteChar('.');
		Write(method);
		WriteChar('(');
		CiType elementType = ((CiClassType) obj.Type).GetElementType();
		if (args.Count == 0)
			WriteNewStorage(elementType);
		else
			WriteNotPromoted(elementType, args[0]);
		WriteChar(')');
	}

	protected void WriteListInsert(CiExpr obj, string method, List<CiExpr> args, string separator = ", ")
	{
		obj.Accept(this, CiPriority.Primary);
		WriteChar('.');
		Write(method);
		WriteChar('(');
		args[0].Accept(this, CiPriority.Argument);
		Write(separator);
		CiType elementType = ((CiClassType) obj.Type).GetElementType();
		if (args.Count == 1)
			WriteNewStorage(elementType);
		else
			WriteNotPromoted(elementType, args[1]);
		WriteChar(')');
	}

	protected void WriteDictionaryAdd(CiExpr obj, List<CiExpr> args)
	{
		WriteIndexing(obj, args[0]);
		Write(" = ");
		WriteNewStorage(((CiClassType) obj.Type).GetValueType());
	}

	protected bool WriteRegexOptions(List<CiExpr> args, string prefix, string separator, string suffix, string i, string m, string s)
	{
		CiExpr expr = args[args.Count - 1];
		if (!(expr.Type is CiEnum))
			return false;
		RegexOptions options = (RegexOptions) expr.IntValue();
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
		WriteChar('[');
		index.Accept(this, CiPriority.Argument);
		WriteChar(']');
	}

	protected virtual void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		WriteIndexing(expr.Left, expr.Right);
	}

	protected virtual string GetIsOperator() => " is ";

	public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Plus:
			WriteBinaryExpr(expr, parent > CiPriority.Add || IsBitOp(parent), CiPriority.Add, " + ", CiPriority.Add);
			break;
		case CiToken.Minus:
			WriteBinaryExpr(expr, parent > CiPriority.Add || IsBitOp(parent), CiPriority.Add, " - ", CiPriority.Mul);
			break;
		case CiToken.Asterisk:
			WriteBinaryExpr2(expr, parent, CiPriority.Mul, " * ");
			break;
		case CiToken.Slash:
			WriteBinaryExpr(expr, parent > CiPriority.Mul, CiPriority.Mul, " / ", CiPriority.Primary);
			break;
		case CiToken.Mod:
			WriteBinaryExpr(expr, parent > CiPriority.Mul, CiPriority.Mul, " % ", CiPriority.Primary);
			break;
		case CiToken.ShiftLeft:
			WriteBinaryExpr(expr, parent > CiPriority.Shift, CiPriority.Shift, " << ", CiPriority.Mul);
			break;
		case CiToken.ShiftRight:
			WriteBinaryExpr(expr, parent > CiPriority.Shift, CiPriority.Shift, " >> ", CiPriority.Mul);
			break;
		case CiToken.Less:
			WriteBinaryExpr2(expr, parent, CiPriority.Rel, " < ");
			break;
		case CiToken.LessOrEqual:
			WriteBinaryExpr2(expr, parent, CiPriority.Rel, " <= ");
			break;
		case CiToken.Greater:
			WriteBinaryExpr2(expr, parent, CiPriority.Rel, " > ");
			break;
		case CiToken.GreaterOrEqual:
			WriteBinaryExpr2(expr, parent, CiPriority.Rel, " >= ");
			break;
		case CiToken.Equal:
			WriteEqual(expr, parent, false);
			break;
		case CiToken.NotEqual:
			WriteEqual(expr, parent, true);
			break;
		case CiToken.And:
			WriteAnd(expr, parent);
			break;
		case CiToken.Or:
			WriteBinaryExpr2(expr, parent, CiPriority.Or, " | ");
			break;
		case CiToken.Xor:
			WriteBinaryExpr(expr, parent > CiPriority.Xor || parent == CiPriority.Or, CiPriority.Xor, " ^ ", CiPriority.Xor);
			break;
		case CiToken.CondAnd:
			WriteBinaryExpr(expr, parent > CiPriority.CondAnd || parent == CiPriority.CondOr, CiPriority.CondAnd, " && ", CiPriority.CondAnd);
			break;
		case CiToken.CondOr:
			WriteBinaryExpr2(expr, parent, CiPriority.CondOr, " || ");
			break;
		case CiToken.Assign:
			WriteAssign(expr, parent);
			break;
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
				WriteChar('(');
			expr.Left.Accept(this, CiPriority.Assign);
			WriteChar(' ');
			Write(expr.GetOpString());
			WriteChar(' ');
			expr.Right.Accept(this, CiPriority.Argument);
			if (parent > CiPriority.Assign)
				WriteChar(')');
			break;

		case CiToken.LeftBracket:
			if (expr.Left.Type is CiStringType)
				WriteCharAt(expr);
			else
				WriteIndexing(expr, parent);
			break;

		case CiToken.Is:
			if (parent > CiPriority.Rel)
				WriteChar('(');
			expr.Left.Accept(this, CiPriority.Rel);
			Write(GetIsOperator());
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
				WriteChar(')');
			break;

		case CiToken.When:
			expr.Left.Accept(this, CiPriority.Argument);
			Write(" when ");
			expr.Right.Accept(this, CiPriority.Argument);
			break;

		default:
			throw new ArgumentException(expr.Op.ToString());
		}
	}

	public override void VisitSelectExpr(CiSelectExpr expr, CiPriority parent)
	{
		WriteCoerced(expr.Type, expr, parent);
	}

	public override void VisitCallExpr(CiCallExpr expr, CiPriority parent)
	{
		WriteCall(expr.Method.Left, (CiMethod) expr.Method.Symbol, expr.Arguments, parent);
	}

	protected abstract void StartTemporaryVar(CiType type);

	protected virtual void DefineObjectLiteralTemporary(CiUnaryExpr expr)
	{
		if (expr.Inner is CiAggregateInitializer init) {
			int id = this.CurrentTemporaries.IndexOf(expr.Type);
			if (id < 0) {
				id = this.CurrentTemporaries.Count;
				StartTemporaryVar(expr.Type);
				this.CurrentTemporaries.Add(expr);
			}
			else
				this.CurrentTemporaries[id] = expr;
			Write("citemp");
			VisitLiteralLong(id);
			Write(" = ");
			WriteNew((CiDynamicPtrType) expr.Type, CiPriority.Argument);
			EndStatement();
			foreach (CiBinaryExpr field in init.Items) {
				Write("citemp");
				VisitLiteralLong(id);
				WriteAggregateInitField(expr, field);
			}
		}
	}

	protected virtual void DefineIsVar(CiBinaryExpr binary)
	{
		if (binary.Right is CiVar def) {
			WriteVar(def);
			EndStatement();
		}
	}

	protected void WriteTemporaries(CiExpr expr)
	{
		switch (expr) {
		case CiVar def:
			if (def.Value != null) {
				if (def.Value is CiUnaryExpr unary && unary.Inner is CiAggregateInitializer)
					WriteTemporaries(unary.Inner);
				else
					WriteTemporaries(def.Value);
			}
			break;
		case CiAggregateInitializer init:
			foreach (CiBinaryExpr field in init.Items)
				WriteTemporaries(field.Right);
			break;
		case CiLiteral _:
		case CiLambdaExpr _:
			break;
		case CiInterpolatedString interp:
			foreach (CiInterpolatedPart part in interp.Parts)
				WriteTemporaries(part.Argument);
			break;
		case CiSymbolReference symbol:
			if (symbol.Left != null)
				WriteTemporaries(symbol.Left);
			break;
		case CiUnaryExpr unary:
			if (unary.Inner != null) {
				WriteTemporaries(unary.Inner);
				DefineObjectLiteralTemporary(unary);
			}
			break;
		case CiBinaryExpr binary:
			WriteTemporaries(binary.Left);
			if (binary.Op == CiToken.Is)
				DefineIsVar(binary);
			else
				WriteTemporaries(binary.Right);
			break;
		case CiSelectExpr select:
			WriteTemporaries(select.Cond);
			WriteTemporaries(select.OnTrue);
			WriteTemporaries(select.OnFalse);
			break;
		case CiCallExpr call:
			WriteTemporaries(call.Method);
			foreach (CiExpr arg in call.Arguments)
				WriteTemporaries(arg);
			break;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	protected virtual void CleanupTemporary(int i, CiExpr temp)
	{
	}

	protected void CleanupTemporaries()
	{
		for (int i = this.CurrentTemporaries.Count; --i >= 0; ) {
			CiExpr temp = this.CurrentTemporaries[i];
			if (!(temp is CiType)) {
				CleanupTemporary(i, temp);
				this.CurrentTemporaries[i] = temp.Type;
			}
		}
	}

	public override void VisitExpr(CiExpr statement)
	{
		WriteTemporaries(statement);
		statement.Accept(this, CiPriority.Statement);
		WriteLine(';');
		if (statement is CiVar def)
			WriteInitCode(def);
		CleanupTemporaries();
	}

	public override void VisitConst(CiConst statement)
	{
	}

	protected abstract void WriteAssertCast(CiBinaryExpr expr);

	protected abstract void WriteAssert(CiAssert statement);

	public override void VisitAssert(CiAssert statement)
	{
		if (statement.Cond is CiBinaryExpr binary && binary.Op == CiToken.Is && binary.Right is CiVar)
			WriteAssertCast(binary);
		else
			WriteAssert(statement);
	}

	protected void WriteFirstStatements(List<CiStatement> statements, int count)
	{
		for (int i = 0; i < count; i++)
			statements[i].AcceptStatement(this);
	}

	protected virtual void WriteStatements(List<CiStatement> statements)
	{
		WriteFirstStatements(statements, statements.Count);
	}

	public override void VisitBlock(CiBlock statement)
	{
		OpenBlock();
		int temporariesCount = this.CurrentTemporaries.Count;
		WriteStatements(statement.Statements);
		this.CurrentTemporaries.RemoveRange(temporariesCount, this.CurrentTemporaries.Count - temporariesCount);
		CloseBlock();
	}

	protected virtual void WriteChild(CiStatement statement)
	{
		if (statement is CiBlock block) {
			WriteChar(' ');
			VisitBlock(block);
		}
		else {
			WriteLine();
			this.Indent++;
			statement.AcceptStatement(this);
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
		WriteChar(';');
		if (statement.Cond != null) {
			WriteChar(' ');
			statement.Cond.Accept(this, CiPriority.Argument);
		}
		WriteChar(';');
		if (statement.Advance != null) {
			WriteChar(' ');
			statement.Advance.Accept(this, CiPriority.Statement);
		}
		WriteChar(')');
		WriteChild(statement.Body);
	}

	protected virtual bool EmbedIfWhileIsVar(CiExpr expr, bool write) => false;

	void StartIfWhile(string name, CiExpr expr)
	{
		if (!EmbedIfWhileIsVar(expr, false)) // FIXME: IsVar but not object literal
			WriteTemporaries(expr);
		Write(name);
		Write(" (");
		EmbedIfWhileIsVar(expr, true);
		expr.Accept(this, CiPriority.Argument);
		WriteChar(')');
	}

	public override void VisitIf(CiIf statement)
	{
		StartIfWhile("if", statement.Cond);
		WriteChild(statement.OnTrue);
		if (statement.OnFalse != null) {
			Write("else");
			if (statement.OnFalse is CiIf) {
				WriteChar(' ');
				statement.OnFalse.AcceptStatement(this);
			}
			else
				WriteChild(statement.OnFalse);
		}
	}

	public override void VisitNative(CiNative statement) => Write(statement.Content);

	protected virtual void WriteStronglyCoerced(CiType type, CiExpr expr) => WriteCoerced(type, expr, CiPriority.Argument);

	public override void VisitReturn(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return;");
		else {
			WriteTemporaries(statement.Value);
			Write("return ");
			WriteStronglyCoerced(this.CurrentMethod.Type, statement.Value);
			WriteLine(';');
			CleanupTemporaries();
		}
	}

	protected virtual void WriteSwitchValue(CiExpr expr) => expr.Accept(this, CiPriority.Argument);

	protected virtual void WriteSwitchCaseBody(List<CiStatement> statements) => WriteStatements(statements);

	protected virtual void WriteSwitchCase(CiSwitch statement, CiCase kase)
	{
		foreach (CiExpr value in kase.Values) {
			Write("case ");
			WriteCoercedLiteral(statement.Value.Type, value);
			WriteLine(':');
		}
		this.Indent++;
		WriteSwitchCaseBody(kase.Body);
		this.Indent--;
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		Write("switch (");
		WriteSwitchValue(statement.Value);
		WriteLine(") {");
		foreach (CiCase kase in statement.Cases)
			WriteSwitchCase(statement, kase);
		if (statement.DefaultBody.Count > 0) {
			WriteLine("default:");
			this.Indent++;
			WriteSwitchCaseBody(statement.DefaultBody);
			this.Indent--;
		}
		WriteLine('}');
	}

	public override void VisitWhile(CiWhile statement)
	{
		StartIfWhile("while", statement.Cond);
		WriteChild(statement.Body);
	}

	protected virtual void WriteParameter(CiVar param) => WriteTypeAndName(param);

	protected void WriteParameters(CiMethod method, bool first, bool defaultArguments)
	{
		for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
			if (!first)
				Write(", ");
			first = false;
			WriteParameter(param);
			if (defaultArguments)
				WriteVarInit(param);
		}
		WriteChar(')');
	}

	protected void WriteParameters(CiMethod method, bool defaultArguments)
	{
		WriteChar('(');
		WriteParameters(method, true, defaultArguments);
	}

	protected virtual bool HasInitCode(CiNamedValue def) => GetAggregateInitializer(def) != null;

	protected virtual bool NeedsConstructor(CiClass klass)
	{
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiField field && HasInitCode(field))
				return true;
		}
		return klass.Constructor != null;
	}

	protected virtual void WriteInitField(CiField field) => WriteInitCode(field);

	protected void WriteConstructorBody(CiClass klass)
	{
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiField field)
				WriteInitField(field);
		}
		if (klass.Constructor != null) {
			this.CurrentMethod = klass.Constructor;
			WriteStatements(((CiBlock) klass.Constructor.Body).Statements);
			this.CurrentMethod = null;
		}
		this.CurrentTemporaries.Clear();
	}

	protected void FlattenBlock(CiStatement statement)
	{
		if (statement is CiBlock block)
			WriteStatements(block.Statements);
		else
			statement.AcceptStatement(this);
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

	protected void WriteEnumValue(CiConst konst)
	{
		WriteDoc(konst.Documentation);
		WriteName(konst);
		if (!(konst.Value is CiImplicitEnumValue)) {
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Argument);
		}
	}

	public override void VisitEnumValue(CiConst konst, CiConst previous)
	{
		if (previous != null)
			WriteLine(',');
		WriteEnumValue(konst);
	}

	protected abstract void WriteEnum(CiEnum enu);

	protected void StartClass(CiClass klass, string suffix, string extendsClause)
	{
		Write("class ");
		Write(klass.Name);
		Write(suffix);
		if (klass.HasBaseClass()) {
			Write(extendsClause);
			Write(klass.BaseClassName);
		}
	}

	protected void OpenClass(CiClass klass, string suffix, string extendsClause)
	{
		StartClass(klass, suffix, extendsClause);
		WriteLine();
		OpenBlock();
	}

	protected abstract void WriteConst(CiConst konst);

	protected abstract void WriteField(CiField field);

	protected abstract void WriteMethod(CiMethod method);

	protected void WriteMembers(CiClass klass, bool constArrays)
	{
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			switch (symbol) {
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
				throw new NotImplementedException(symbol.Type.ToString());
			}
		}
		if (constArrays) {
			foreach (CiConst konst in klass.ConstArrays)
				WriteConst(konst);
		}
	}

	protected bool WriteBaseClass(CiClass klass, CiProgram program)
	{
		// topological sorting of class hierarchy
		if (!this.WrittenClasses.Add(klass))
			return false;
		if (klass.Parent is CiClass baseClass)
			WriteClass(baseClass, program);
		return true;
	}

	protected abstract void WriteClass(CiClass klass, CiProgram program);

	protected void WriteTypes(CiProgram program)
	{
		for (CiSymbol type = program.First; type != null; type = type.Next) {
			if (type is CiClass klass)
				WriteClass(klass, program);
			else
				WriteEnum((CiEnum) type);
		}
	}

	public abstract void WriteProgram(CiProgram program);
}

}
