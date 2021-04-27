// GenCs.cs - C# code generator
//
// Copyright (C) 2011-2021  Piotr Fusik
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

public class GenCs : GenTyped
{
	protected override void StartDocLine()
	{
		Write("/// ");
	}

	protected override void Write(CiDocPara para, bool many)
	{
		if (many) {
			WriteLine();
			Write("/// <para>");
		}
		foreach (CiDocInline inline in para.Children) {
			switch (inline) {
			case CiDocText text:
				WriteXmlDoc(text.Text);
				break;
			case CiDocCode code:
				switch (code.Text) {
				case "true":
				case "false":
				case "null":
					Write("<see langword=\"");
					Write(code.Text);
					Write("\" />");
					break;
				default:
					Write("<c>");
					WriteXmlDoc(code.Text);
					Write("</c>");
					break;
				}
				break;
			default:
				throw new ArgumentException(inline.GetType().Name);
			}
		}
		if (many)
			Write("</para>");
	}

	protected override void Write(CiDocList list)
	{
		WriteLine();
		WriteLine("/// <list type=\"bullet\">");
		foreach (CiDocPara item in list.Items) {
			Write("/// <item>");
			Write(item, false);
			WriteLine("</item>");
		}
		Write("/// </list>");
	}

	protected override void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		Write("/// <summary>");
		Write(doc.Summary, false);
		WriteLine("</summary>");
		if (doc.Details.Length > 0) {
			Write("/// <remarks>");
			if (doc.Details.Length == 1)
				Write(doc.Details[0], false);
			else {
				foreach (CiDocBlock block in doc.Details)
					Write(block, true);
			}
			WriteLine("</remarks>");
		}
	}

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiConst konst && konst.InMethod != null)
			Write(konst.InMethod.Name);
		Write(symbol.Name);
		switch (symbol.Name) {
		case "as":
		case "await":
		case "catch":
		case "char":
		case "checked":
		case "decimal":
		case "delegate":
		case "event":
		case "explicit":
		case "extern":
		case "finally":
		case "fixed":
		case "goto":
		case "implicit":
		case "interface":
		case "is":
		case "lock":
		case "namespace":
		case "object":
		case "operator":
		case "out":
		case "params":
		case "private":
		case "readonly":
		case "ref":
		case "sbyte":
		case "sizeof":
		case "stackalloc":
		case "struct":
		case "try":
		case "typeof":
		case "ulong":
		case "unchecked":
		case "unsafe":
		case "using":
		case "volatile":
			Write('_');
			break;
		default:
			break;
		}
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte: Write("sbyte"); break;
		case TypeCode.Byte: Write("byte"); break;
		case TypeCode.Int16: Write("short"); break;
		case TypeCode.UInt16: Write("ushort"); break;
		case TypeCode.Int32: Write("int"); break;
		case TypeCode.Int64: Write("long"); break;
		default: throw new NotImplementedException(typeCode.ToString());
		}
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Private:
			break;
		case CiVisibility.Internal:
			Write("internal ");
			break;
		case CiVisibility.Protected:
			Write("protected ");
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	void Write(CiCallType callType, string sealedString)
	{
		switch (callType) {
		case CiCallType.Static:
			Write("static ");
			break;
		case CiCallType.Normal:
			break;
		case CiCallType.Abstract:
			Write("abstract ");
			break;
		case CiCallType.Virtual:
			Write("virtual ");
			break;
		case CiCallType.Override:
			Write("override ");
			break;
		case CiCallType.Sealed:
			Write(sealedString);
			break;
		}
	}

	void Write(CiDictionaryType dict)
	{
		Include("System.Collections.Generic");
		if (dict is CiSortedDictionaryType)
			Write("Sorted");
		Write("Dictionary<");
		Write(dict.KeyType, false);
		Write(", ");
		Write(dict.ValueType, false);
		Write('>');
	}

	protected override void Write(CiType type, bool promote)
	{
		switch (type) {
		case null:
			Write("void");
			break;
		case CiIntegerType integer:
			Write(GetIntegerTypeCode(integer, promote));
			break;
		case CiStringType _:
			Write("string");
			break;
		case CiListType list:
			Include("System.Collections.Generic");
			Write("List<");
			Write(list.ElementType, false);
			Write('>');
			break;
		case CiDictionaryType dict:
			Write(dict);
			break;
		case CiArrayType array:
			Write(array.ElementType, false);
			Write("[]");
			break;
		default:
			if (type.IsClass(CiSystem.RegexClass) || type.IsClass(CiSystem.MatchClass))
				Include("System.Text.RegularExpressions");
			Write(type.Name);
			break;
		}
	}

	protected override void WriteListStorageInit(CiListType list)
	{
		Include("System.Collections.Generic");
		Write(" = new List<");
		Write(list.ElementType, false);
		Write(">()");
	}

	protected override void WriteDictionaryStorageInit(CiDictionaryType dict)
	{
		Write(" = new ");
		Write(dict);
		Write("()");
	}

	public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent)
	{
		Write("$\"");
		foreach (CiInterpolatedPart part in expr.Parts) {
			WriteEscapingBrace(part.Prefix);
			Write('{');
			part.Argument.Accept(this, CiPriority.Argument);
			if (part.WidthExpr != null) {
				Write(',');
				Write(part.Width);
			}
			if (part.Format != ' ') {
				Write(':');
				Write(part.Format);
				if (part.Precision >= 0)
					Write(part.Precision);
			}
			Write('}');
		}
		WriteEscapingBrace(expr.Suffix);
		Write('"');
		return expr;
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Write("new ");
		Write(elementType.BaseType, false);
		Write('[');
		lengthExpr.Accept(this, CiPriority.Argument);
		Write(']');
		while (elementType is CiArrayType array) {
			Write("[]");
			elementType = array.ElementType;
		}
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		return def.Type is CiArrayStorageType array
			&& (array.ElementType is CiClass || array.ElementType is CiArrayStorageType);
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (!HasInitCode(def))
			return;
		CiArrayStorageType array = (CiArrayStorageType) def.Type;
		int nesting = 0;
		while (array.ElementType is CiArrayStorageType innerArray) {
			OpenLoop("int", nesting++, array.Length);
			WriteArrayElement(def, nesting);
			Write(" = ");
			WriteNewArray(innerArray.ElementType, innerArray.LengthExpr, CiPriority.Argument);
			WriteLine(';');
			array = innerArray;
		}
		if (array.ElementType is CiClass klass) {
			OpenLoop("int", nesting++, array.Length);
			WriteArrayElement(def, nesting);
			Write(" = ");
			WriteNew(klass, CiPriority.Argument);
			WriteLine(';');
		}
		while (--nesting >= 0)
			CloseBlock();
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("CiResource.");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		expr.Accept(this, CiPriority.Primary);
		Write(".Length");
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Symbol == CiSystem.MatchStart) {
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".Index");
		}
		else if (expr.Symbol == CiSystem.MatchEnd) {
			if (parent > CiPriority.Add)
				Write('(');
			expr.Left.Accept(this, CiPriority.Primary);
			Write(".Index + ");
			WriteStringLength(expr.Left); // FIXME: side effect
			if (parent > CiPriority.Add)
				Write(')');
		}
		else if (expr.Left != null && expr.Left.IsReferenceTo(CiSystem.MathClass)) { // NaN, NegativeInfinity, PositiveInfinity
			Write("float.");
			Write(expr.Symbol.Name);
		}
		else
			return base.Visit(expr, parent);
		return expr;
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj == null) {
			WriteName(method);
			WriteArgsInParentheses(method, args);
		}
		else if ((method == CiSystem.StringIndexOf || method == CiSystem.StringLastIndexOf)
			&& IsOneAsciiString(args[0], out char c)) {
			obj.Accept(this, CiPriority.Primary);
			Write('.');
			Write(method.Name);
			Write('(');
			WriteCharLiteral(c);
			Write(')');
		}
		else if (method == CiSystem.UTF8GetString) {
			Include("System.Text");
			Write("Encoding.UTF8.GetString");
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.RegexCompile) {
			Include("System.Text.RegularExpressions");
			Write("new Regex");
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.MatchFindStr) {
			Include("System.Text.RegularExpressions");
			Write('(');
			obj.Accept(this, CiPriority.Assign);
			Write(" = Regex.Match");
			WriteArgsInParentheses(method, args);
			Write(").Success");
		}
		else if (method == CiSystem.MatchFindRegex) {
			Include("System.Text.RegularExpressions");
			Write('(');
			obj.Accept(this, CiPriority.Assign);
			Write(" = ");
			WriteCall(args[1], "Match", args[0]);
			Write(").Success");
		}
		else if (method == CiSystem.MatchGetCapture) {
			obj.Accept(this, CiPriority.Primary);
			Write(".Groups[");
			args[0].Accept(this, CiPriority.Argument);
			Write("].Value");
		}
		else if (obj.Type is CiArrayType array && method.Name == "BinarySearch") {
			Include("System");
			Write("Array.BinarySearch(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			if (args.Length == 3) {
				args[1].Accept(this, CiPriority.Argument);
				Write(", ");
				args[2].Accept(this, CiPriority.Argument);
				Write(", ");
			}
			WriteNotPromoted(array.ElementType, args[0]);
			Write(')');
		}
		else if (obj.Type is CiArrayType && !(obj.Type is CiListType) && method.Name == "CopyTo") {
			Include("System");
			Write("Array.Copy(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteArgs(method, args);
			Write(')');
		}
		else if (obj.Type is CiArrayType && method.Name == "Fill") {
			if (!(args[0] is CiLiteral literal) || !literal.IsDefaultValue)
				throw new NotImplementedException("Only null, zero and false supported");
			Include("System");
			Write("Array.Clear(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			if (args.Length == 1) {
				Write("0, ");
				Write(((CiArrayStorageType) obj.Type).Length);
			}
			else {
				args[1].Accept(this, CiPriority.Argument);
				Write(", ");
				args[2].Accept(this, CiPriority.Argument);
			}
			Write(')');
		}
		else if (obj.Type is CiArrayStorageType && method == CiSystem.CollectionSortAll) {
			Include("System");
			WriteCall("Array.Sort", obj);
		}
		else if (method == CiSystem.CollectionSortPart) {
			if (obj.Type is CiListType) {
				obj.Accept(this, CiPriority.Primary);
				Write(".Sort(");
				WriteArgs(method, args);
				Write(", null)");
			}
			else {
				Include("System");
				WriteCall("Array.Sort", obj, args[0], args[1]);
			}
		}
		else if (WriteListAddInsert(obj, method, args, "Add", "Insert", ", ")) {
			// done
		}
		else if (obj.Type is CiDictionaryType dict && method.Name == "Add") {
			obj.Accept(this, CiPriority.Primary);
			Write(".Add(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteNewStorage(dict.ValueType);
			Write(')');
		}
		else {
			if (method == CiSystem.MathIsFinite || method == CiSystem.MathIsInfinity || method == CiSystem.MathIsNaN)
				Write("double");
			else {
				if (method == CiSystem.ConsoleWrite || method == CiSystem.ConsoleWriteLine
				 || method == CiSystem.EnvironmentGetEnvironmentVariable || obj.IsReferenceTo(CiSystem.MathClass))
					Include("System");
				else if (method == CiSystem.RegexEscape || method == CiSystem.RegexIsMatchStr || method == CiSystem.RegexIsMatchRegex)
					Include("System.Text.RegularExpressions");
				obj.Accept(this, CiPriority.Primary);
			}
			Write('.');
			WriteName(method);
			WriteArgsInParentheses(method, args);
		}
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.AndAssign:
		case CiToken.OrAssign:
		case CiToken.XorAssign:
			if (parent > CiPriority.Assign)
				Write('(');
			expr.Left.Accept(this, CiPriority.Assign);
			Write(' ');
			Write(expr.OpString);
			Write(' ');
			WriteAssignRight(expr);
			if (parent > CiPriority.Assign)
				Write(')');
			return expr;
		default:
			return base.Visit(expr, parent);
		}
	}

	public override void Visit(CiAssert statement)
	{
		if (statement.CompletesNormally) {
			Include("System.Diagnostics");
			Write("Debug.Assert(");
			statement.Cond.Accept(this, CiPriority.Argument);
			if (statement.Message != null) {
				Write(", ");
				statement.Message.Accept(this, CiPriority.Argument);
			}
		}
		else {
			// assert false;
			Include("System");
			Write("throw new NotImplementedException(");
			if (statement.Message != null)
				statement.Message.Accept(this, CiPriority.Argument);
		}
		WriteLine(");");
	}

	public override void Visit(CiForeach statement)
	{
		Write("foreach (");
		if (statement.Count == 2) {
			Write('(');
			WriteTypeAndName(statement.Element);
			Write(", ");
			WriteTypeAndName(statement.ValueVar);
			Write(')');
		}
		else
			WriteTypeAndName(statement.Element);
		Write(" in ");
		statement.Collection.Accept(this, CiPriority.Argument);
		Write(')');
		WriteChild(statement.Body);
	}

	public override void Visit(CiThrow statement)
	{
		Include("System");
		Write("throw new Exception(");
		statement.Message.Accept(this, CiPriority.Argument);
		WriteLine(");");
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		if (enu.IsFlags) {
			Include("System");
			WriteLine("[Flags]");
		}
		WritePublic(enu);
		Write("enum ");
		WriteLine(enu.Name);
		OpenBlock();
		bool first = true;
		foreach (CiConst konst in enu) {
			if (!first)
				WriteLine(',');
			first = false;
			Write(konst.Documentation);
			Write(konst.Name);
			if (konst.Value != null) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Argument);
			}
		}
		WriteLine();
		CloseBlock();
	}

	void WriteConsts(IEnumerable<CiConst> consts)
	{
		foreach (CiConst konst in consts) {
			WriteLine();
			Write(konst.Documentation);
			Write(konst.Visibility);
			if (konst.Type is CiArrayStorageType)
				Write("static readonly ");
			else
				Write("const ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Argument);
			WriteLine(';');
		}
	}

	void Write(CiClass klass)
	{
		WriteLine();
		Write(klass.Documentation);
		WritePublic(klass);
		Write(klass.CallType, "sealed ");
		OpenClass(klass, "", " : ");

		if (NeedsConstructor(klass)) {
			if (klass.Constructor != null) {
				Write(klass.Constructor.Documentation);
				Write(klass.Constructor.Visibility);
			}
			else
				Write("internal ");
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			foreach (CiField field in klass.Fields)
				WriteInitCode(field);
			WriteConstructorBody(klass);
			CloseBlock();
		}

		WriteConsts(klass.Consts);

		foreach (CiField field in klass.Fields) {
			WriteLine();
			Write(field.Documentation);
			Write(field.Visibility);
			if (field.Type.IsFinal)
				Write("readonly ");
			WriteVar(field);
			WriteLine(';');
		}

		foreach (CiMethod method in klass.Methods) {
			WriteLine();
			Write(method.Documentation);
			foreach (CiVar param in method.Parameters) {
				if (param.Documentation != null) {
					Write("/// <param name=\"");
					WriteName(param);
					Write("\">");
					Write(param.Documentation.Summary, false);
					WriteLine("</param>");
				}
			}
			Write(method.Visibility);
			Write(method.CallType, "sealed override ");
			WriteTypeAndName(method);
			WriteParameters(method, true);
			WriteBody(method);
		}

		WriteConsts(klass.ConstArrays);

		CloseBlock();
	}

	void WriteResources(Dictionary<string, byte[]> resources)
	{
		WriteLine();
		WriteLine("internal static class CiResource");
		OpenBlock();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			Write("internal static readonly byte[] ");
			WriteResource(name, -1);
			WriteLine(" = {");
			Write('\t');
			Write(resources[name]);
			WriteLine(" };");
		}
		CloseBlock();
	}

	public override void Write(CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		OpenStringWriter();
		if (this.Namespace != null) {
			Write("namespace ");
			WriteLine(this.Namespace);
			OpenBlock();
		}
		WriteTopLevelNatives(program);
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes)
			Write(klass);
		if (program.Resources.Count > 0)
			WriteResources(program.Resources);
		if (this.Namespace != null)
			CloseBlock();

		CreateFile(this.OutputFile);
		WriteIncludes("using ", ";");
		CloseStringWriter();
		CloseFile();
	}
}

}
