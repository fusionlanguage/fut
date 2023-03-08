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

public abstract class GenBase : GenBaseBase
{
	protected SortedSet<string> Includes;

	static TextWriter CreateTextWriter(string filename) => File.CreateText(filename);

	protected void CreateFile(string filename)
	{
		this.Writer = CreateTextWriter(filename);
		WriteBanner();
	}

	protected void CloseFile() => this.Writer.Close();

	protected void OpenStringWriter()
	{
		this.Writer = this.StringWriter = new StringWriter();
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

	protected virtual void DefineObjectLiteralTemporary(CiUnaryExpr expr)
	{
		if (expr.Inner is CiAggregateInitializer init) {
			EnsureChildBlock();
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

	public override void VisitExpr(CiExpr statement)
	{
		WriteTemporaries(statement);
		statement.Accept(this, CiPriority.Statement);
		WriteCharLine(';');
		if (statement is CiVar def)
			WriteInitCode(def);
		CleanupTemporaries();
	}

	void WriteIf(CiIf statement)
	{
		Write("if (");
		StartIfWhile(statement.Cond);
		WriteChild(statement.OnTrue);
		if (statement.OnFalse != null) {
			Write("else");
			if (statement.OnFalse is CiIf elseIf) {
				bool wasInChildBlock = this.InChildBlock;
				this.AtLineStart = true;
				this.AtChildStart = true;
				this.InChildBlock = false;
				if (!EmbedIfWhileIsVar(elseIf.Cond, false)) // FIXME: IsVar but not object literal
					WriteTemporaries(elseIf.Cond);
				if (this.InChildBlock) {
					WriteIf(elseIf);
					CloseBlock();
				}
				else {
					this.AtLineStart = false;
					this.AtChildStart = false;
					WriteChar(' ');
					WriteIf(elseIf);
				}
				this.InChildBlock = wasInChildBlock;
			}
			else
				WriteChild(statement.OnFalse);
		}
	}

	public override void VisitIf(CiIf statement)
	{
		if (!EmbedIfWhileIsVar(statement.Cond, false)) // FIXME: IsVar but not object literal
			WriteTemporaries(statement.Cond);
		WriteIf(statement);
	}

	public override void VisitReturn(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return;");
		else {
			WriteTemporaries(statement.Value);
			Write("return ");
			WriteStronglyCoerced(this.CurrentMethod.Type, statement.Value);
			WriteCharLine(';');
			CleanupTemporaries();
		}
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		WriteTemporaries(statement.Value);
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
		WriteCharLine('}');
	}

	public override void VisitWhile(CiWhile statement)
	{
		if (!EmbedIfWhileIsVar(statement.Cond, false)) // FIXME: IsVar but not object literal
			WriteTemporaries(statement.Cond);
		Write("while (");
		StartIfWhile(statement.Cond);
		WriteChild(statement.Body);
	}

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
}

}
