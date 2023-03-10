// GenD.cs - D code generator
//
// Copyright (C) 2011-2023  Piotr Fusik
// Copyright (C) 2023 Adrian Matoga
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
using System.Diagnostics;

namespace Foxoft.Ci
{

public class GenD : GenCCppD
{

	bool HasListInsert;
	bool HasListRemoveAt;
	bool HasSortedDictionaryInsert;
	bool HasSortedDictionaryFind;
	bool HasQueueDequeue;
	bool HasStackPop;
	protected override string GetTargetName() => "D";

	protected override void StartDocLine() => Write("/// ");

	protected override void WriteDocPara(CiDocPara para, bool many)
	{
		if (many) {
			WriteNewLine();
			StartDocLine();
		}
		foreach (CiDocInline inline in para.Children) {
			switch (inline) {
			case CiDocText text:
				WriteXmlDoc(text.Text);
				break;
			case CiDocCode code:
				switch (code.Text) {
				default:
					Write("`");
					WriteXmlDoc(code.Text);
					Write("`");
					break;
				}
				break;
			case CiDocLine _:
				WriteNewLine();
				StartDocLine();
				break;
			default:
				throw new ArgumentException(inline.GetType().Name);
			}
		}
		if (many)
			WriteNewLine();
	}

	protected override void WriteParameterDoc(CiVar param, bool first)
	{
		if (first) {
			StartDocLine();
			WriteLine("Params:");
		}
		StartDocLine();
		WriteName(param);
		Write(" = ");
		WriteDocPara(param.Documentation.Summary, false);
		WriteNewLine();
	}

	protected override void WriteDocList(CiDocList list)
	{
		WriteLine("///");
		WriteLine("/// <ul>");
		foreach (CiDocPara item in list.Items) {
			Write("/// <li>");
			WriteDocPara(item, false);
			WriteLine("</li>");
		}
		WriteLine("/// </ul>");
		Write("///");
	}

	protected override void WriteDoc(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		StartDocLine();
		WriteDocPara(doc.Summary, false);
		WriteNewLine();
		if (doc.Details.Count > 0) {
			StartDocLine();
			if (doc.Details.Count == 1)
				WriteDocBlock(doc.Details[0], false);
			else {
				foreach (CiDocBlock block in doc.Details)
					WriteDocBlock(block, true);
			}
			WriteNewLine();
		}
	}

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiContainerType) {
			Write(symbol.Name);
			return;
		}
		string camelCaseName = Char.ToLowerInvariant(symbol.Name[0]) + symbol.Name.Substring(1);
		Write(camelCaseName);
		switch (camelCaseName) {
			case "abstract":
			case "alias":
			case "align":
			case "asm":
			case "assert":
			case "auto":
			case "body":
			case "bool":
			case "break":
			case "byte":

			case "case":
			case "cast":
			case "catch":
			case "cdouble":
			case "cent":
			case "cfloat":
			case "char":
			case "class":
			case "const":
			case "continue":
			case "creal":

			case "dchar":
			case "debug":
			case "default":
			case "delegate":
			case "delete":
			case "deprecated":
			case "do":
			case "double":

			case "else":
			case "enum":
			case "export":
			case "extern":

			case "false":
			case "final":
			case "finally":
			case "float":
			case "for":
			case "foreach":
			case "foreach_reverse":
			case "function":

			case "goto":

			case "idouble":
			case "if":
			case "ifloat":
			case "immutable":
			case "import":
			case "in":
			case "inout":
			case "int":
			case "interface":
			case "invariant":
			case "ireal":
			case "is":

			case "lazy":
			case "long":

			case "macro":
			case "mixin":
			case "module":

			case "new":
			case "nothrow":
			case "null":

			case "out":
			case "override":

			case "package":
			case "pragma":
			case "private":
			case "protected":
			case "public":
			case "pure":

			case "real":
			case "ref":
			case "return":

			case "scope":
			case "shared":
			case "short":
			case "sizeof":
			case "static":
			case "string":
			case "struct":
			case "super":
			case "switch":
			case "synchronized":

			case "template":
			case "throw":
			case "true":
			case "try":
			case "typeid":
			case "typeof":

			case "ubyte":
			case "ucent":
			case "uint":
			case "ulong":
			case "union":
			case "unittest":
			case "ushort":

			case "version":
			case "void":

			case "wchar":
			case "while":
			case "with":

			case "__FILE__":
			case "__FILE_FULL_PATH__":
			case "__MODULE__":
			case "__LINE__":
			case "__FUNCTION__":
			case "__PRETTY_FUNCTION__":

			case "__gshared":
			case "__traits":
			case "__vector":
			case "__parameters":
			WriteChar('_');
			break;
		default:
			break;
		}
	}

	protected override int GetLiteralChars() => 0x10000;

	protected override void WriteTypeCode(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte: Write("byte"); break;
		case TypeCode.Byte: Write("ubyte"); break;
		case TypeCode.Int16: Write("short"); break;
		case TypeCode.UInt16: Write("ushort"); break;
		case TypeCode.Int32: Write("int"); break;
		case TypeCode.UInt32: Write("uint"); break;
		case TypeCode.Int64: Write("long"); break;
		case TypeCode.UInt64: Write("ulong"); break;
		case TypeCode.Boolean: Write("bool"); break;
		default: throw new NotImplementedException(typeCode.ToString());
		}
	}

	void WriteVisibility(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Protected:
			Write("protected ");
			break;
		case CiVisibility.Public:
			break;
		}
	}

	void WriteCallType(CiCallType callType, string sealedString)
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
			break;
		case CiCallType.Override:
			Write("override ");
			break;
		case CiCallType.Sealed:
			Write(sealedString);
			break;
		}
	}


	protected static bool IsCreateWithNew(CiType type)
	{
		if (type is CiClassType klass) {
			if (klass is CiStorageType stg)
				return stg.Class.Id != CiId.ArrayStorageClass;
			return true;
		}
		return false;
	}

	static bool IsStructPtr(CiType type) => type is CiClassType ptr
		&& (ptr.Class.Id == CiId.ListClass || ptr.Class.Id == CiId.StackClass || ptr.Class.Id == CiId.QueueClass);

	protected void WriteElementType(CiType type)
	{
		WriteType(type, false);
		if (IsStructPtr(type))
			WriteChar('*');
	}

	protected override void WriteType(CiType type, bool promote)
	{
		switch (type) {
		case CiIntegerType integer:
			WriteTypeCode(GetIntegerTypeCode(integer, promote));
			break;
		case CiClassType klass:
			switch (klass.Class.Id) {
			case CiId.StringClass:
				Write("string");
				break;
			case CiId.ArrayStorageClass:
			case CiId.ArrayPtrClass:
				WriteElementType(klass.GetElementType());
				WriteChar('[');
				if (klass is CiArrayStorageType arrayStorage)
					VisitLiteralLong(arrayStorage.Length);
				WriteChar(']');
				break;
			case CiId.ListClass:
			case CiId.StackClass:
				Include("std.container.array");
				Write("Array!(");
				WriteElementType(klass.GetElementType());
				Write(")");
				break;
			case CiId.QueueClass:
				Include("std.container.dlist");
				Write("DList!(");
				WriteElementType(klass.GetElementType());
				Write(")");
				break;
			case CiId.HashSetClass:
				Write("bool[");
				WriteElementType(klass.GetElementType());
				WriteChar(']');
				break;
			case CiId.DictionaryClass:
				WriteElementType(klass.GetValueType());
				WriteChar('[');
				WriteType(klass.GetKeyType(), false);
				WriteChar(']');
				break;
			case CiId.SortedSetClass:
				Include("std.container.rbtree");
				Write("RedBlackTree!(");
				WriteElementType(klass.GetElementType());
				WriteChar(')');
				break;
			case CiId.SortedDictionaryClass:
				Include("std.container.rbtree");
				Include("std.typecons");
				Write("RedBlackTree!(Tuple!(");
				WriteElementType(klass.GetKeyType());
				Write(", ");
				WriteElementType(klass.GetValueType());
				Write("), \"a[0] < b[0]\")");
				break;
			case CiId.OrderedDictionaryClass:
				Include("std.typecons");
				Write("Tuple!(Array!(");
				WriteElementType(klass.GetValueType());
				Write("), \"data\", size_t[");
				WriteType(klass.GetKeyType(), false);
				Write("], \"dict\")");
				break;
			case CiId.TextWriterClass:
				Include("std.stdio");
				Write("File");
				break;
			case CiId.RegexClass:
				Include("std.regex");
				Write("Regex!char");
				break;
			case CiId.MatchClass:
				Include("std.regex");
				Write("Captures!string");
				break;
			case CiId.LockClass:
				Write("Object");
				break;
			default:
				Write(klass.Class.Name);
				break;
			}
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteType(value.Type, true);
		if (IsStructPtr(value.Type))
			WriteChar('*');
		WriteChar(' ');
		WriteName(value);
	}

	public override void VisitAggregateInitializer(CiAggregateInitializer expr)
	{
		CiType type = ((CiArrayStorageType) expr.Type).GetElementType();
		Write("[ ");
		WriteCoercedLiterals(type, expr.Items);
		Write(" ]");
	}

	protected override void WriteStaticCast(CiType type, CiExpr expr)
	{
		Write("cast(");
		WriteType(type, false);
		Write(")(");
		GetStaticCastInner(type, expr).Accept(this, CiPriority.Argument);
		WriteChar(')');
	}

	protected override void WriteCoercedLiteral(CiType type, CiExpr literal)
	{
		if (literal is CiLiteralChar && type is CiRangeType range && range.Max <= 0xff)
			WriteStaticCast(type, literal);
		else
			literal.Accept(this, CiPriority.Argument);
	}

	public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		Include("std.format");
		Write("format(");
		WritePrintf(expr, false);
	}

	protected override void WriteStorageInit(CiNamedValue def)
	{
		Write(" = ");
		WriteNewStorage(def.Type);
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Type is CiArrayStorageType)
			return;
		base.WriteVarInit(def);
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		if (def.Value != null && !(def.Value is CiLiteral))
			return true;
		CiType type = def.Type;
		if (type is CiArrayStorageType array) {
			while (array.GetElementType() is CiArrayStorageType innerArray)
				array = innerArray;
			type = array.GetElementType();
		}
		return type is CiStorageType;
	}

	protected override void WriteInitField(CiField field)
	{
		WriteInitCode(field);
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (!HasInitCode(def))
			return;
		if (def.Type is CiArrayStorageType array) {
			int nesting = 0;
			while (array.GetElementType() is CiArrayStorageType innerArray) {
				OpenLoop("size_t", nesting++, array.Length);
				array = innerArray;
			}
			if (array.GetElementType() is CiStorageType klass) {
				OpenLoop("size_t", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNew(klass, CiPriority.Argument);
				WriteLine(";");
			}
			while (--nesting >= 0)
				CloseBlock();
		} else {
			if (def.Type is CiReadWriteClassType klass) {
				switch (klass.Class.Id) {
				case CiId.StringClass:
				case CiId.ArrayStorageClass:
				case CiId.ArrayPtrClass:
				case CiId.DictionaryClass:
				case CiId.HashSetClass:
				case CiId.SortedDictionaryClass:
				case CiId.OrderedDictionaryClass:
				case CiId.LockClass:
					break;
				case CiId.RegexClass:
				case CiId.MatchClass:
					break;
				default:
					if (def.Parent is CiClass) {
						WriteName(def);
						Write(" = ");
						if (def.Value == null)
							WriteNew(klass, CiPriority.Argument);
						else
							WriteCoercedExpr(def.Type, def.Value);
						WriteLine(";");
					}
					base.WriteInitCode(def);
					break;
				}
			}
		}
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Write("new ");
		WriteType(elementType, false);
		WriteChar('[');
		lengthExpr.Accept(this, CiPriority.Argument);
		WriteChar(']');
	}

	protected void WriteStaticInitializer(CiType type)
	{
		WriteChar('(');
		WriteType(type, false);
		Write(").init");
	}

	protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
	{
		if (IsCreateWithNew(klass)) {
			Write("new ");
			WriteType(klass, false);
		} else
			WriteStaticInitializer(klass);
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("CiResource.");
		foreach (char c in name)
			WriteChar(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override void WriteStringLength(CiExpr expr) => WritePostfix(expr, ".length");

	public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.ConsoleError:
			Write("stderr");
			break;
		case CiId.ListCount:
		case CiId.StackCount:
		case CiId.HashSetCount:
		case CiId.DictionaryCount:
		case CiId.SortedSetCount:
		case CiId.SortedDictionaryCount:
			WriteStringLength(expr.Left);
			break;
		case CiId.QueueCount:
			Include("std.range");
			WriteClassReference(expr.Left);
			Write("[].walkLength");
			break;
		case CiId.MatchStart:
			WritePostfix(expr.Left, ".pre.length");
			break;
		case CiId.MatchEnd:
		 	if (parent > CiPriority.Add)
		 		WriteChar('(');
			WritePostfix(expr.Left, ".pre.length + ");
			WritePostfix(expr.Left, ".hit.length"); // FIXME: side effect
			if (parent > CiPriority.Add)
				WriteChar(')');
			break;
		case CiId.MatchLength:
			WritePostfix(expr.Left, ".hit.length");
			break;
		case CiId.MatchValue:
			WritePostfix(expr.Left, ".hit");
			break;
		case CiId.MathNaN:
			Write("double.nan");
		 	break;
		case CiId.MathNegativeInfinity:
			Write("-double.infinity");
		 	break;
		case CiId.MathPositiveInfinity:
		 	Write("double.infinity");
		 	break;
		default:
			if (expr.Symbol.Parent is CiForeach forEach
			&& forEach.Collection.Type is CiClassType dict
			&& dict.Class.Id == CiId.OrderedDictionaryClass)
				throw new NotImplementedException();
			else
				base.VisitSymbolReference(expr, parent);
			break;
		}
	}

	void WriteWrite(List<CiExpr> args, bool newLine)
	{
		Include("std.stdio");
		if (args.Count == 0)
			Write("writeln()");
		else if (args[0] is CiInterpolatedString interpolated) {
			Write(newLine ? "writefln(" : "writef(");
			WritePrintf(interpolated, false);
		}
		else {
			Write(newLine ? "writeln(" : "write(");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
		}
	}

	protected static string GetMathMethodName(CiMethod method)
	{
		switch (method.Id) {
		case CiId.MathCeiling:
			return "ceil";
		case CiId.MathFusedMultiplyAdd:
			return "fma";
		case CiId.MathIsFinite:
			return "isFinite";
		case CiId.MathIsInfinity:
			return "isInfinity";
		case CiId.MathIsNaN:
			return "isNaN";
		case CiId.MathTruncate:
			return "trunc";
		default:
			return method.Name.ToLower();
		}
	}

	protected void WriteInsertedArg(CiType type, List<CiExpr> args, int index = 0)
	{
		if (args.Count <= index)
			WriteNew((CiReadWriteClassType) type, CiPriority.Argument);
		else
			WriteCoercedExpr(type, args[index]);
		WriteChar(')');
	}

	protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.DoubleTryParse:
			Include("std.conv");
			Write("() { try { ");
			WritePostfix(obj, " = ");
			WritePostfix(args[0], ".to!double; return true; } catch (ConvException e) return false; }()");
			break;
		case CiId.StringSubstring:
			obj.Accept(this, CiPriority.Primary);
			WriteChar('[');
			WritePostfix(args[0], " .. $]");
			if (args.Count > 1) {
				Write("[0 .. ");
				args[1].Accept(this, CiPriority.Argument);
				WriteChar(']');
			}
			break;
		case CiId.ArrayBinarySearchAll:
		case CiId.ArrayBinarySearchPart:
			Include("std.range");
			Write("() { size_t cibegin = ");
			if (args.Count == 3)
				args[1].Accept(this, CiPriority.Argument);
			else
				WriteChar('0');
			Write("; auto cisearch = ");
			WriteClassReference(obj);
			WriteChar('[');
			if (args.Count == 3) {
				Write("cibegin .. cibegin + ");
				args[2].Accept(this, CiPriority.Add);
			}
			Write("].assumeSorted.trisect(");
			WriteNotPromoted(((CiClassType) obj.Type).GetElementType(), args[0]);
			Write("); return cisearch[1].length ? cibegin + cisearch[0].length : -1; }()");
			break;
		case CiId.ArrayCopyTo:
		case CiId.ListCopyTo:
			Include("std.algorithm");
			WriteClassReference(obj);
			WriteChar('[');
			args[0].Accept(this, CiPriority.Argument);
			Write(" .. $][0 .. ");
			args[3].Accept(this, CiPriority.Argument);
			Write("].copy(");
			args[1].Accept(this, CiPriority.Argument);
			WriteChar('[');
			args[2].Accept(this, CiPriority.Argument);
			Write(" .. $])");
			break;
		case CiId.ArrayFillAll:
		case CiId.ArrayFillPart:
			Include("std.algorithm");
			WriteClassReference(obj);
			WriteChar('[');
			if (args.Count == 3) {
				args[1].Accept(this, CiPriority.Argument);
				Write(" .. $][0 .. ");
				args[2].Accept(this, CiPriority.Argument);
			}
			Write("].fill(");
			WriteNotPromoted(((CiClassType) obj.Type).GetElementType(), args[0]);
			WriteChar(')');
			break;
		case CiId.ArraySortAll:
		case CiId.ArraySortPart:
		case CiId.ListSortAll:
		case CiId.ListSortPart:
			Include("std.algorithm");
			WriteClassReference(obj);
			WriteChar('[');
			if (args.Count == 2) {
				args[0].Accept(this, CiPriority.Argument);
				Write(" .. $][0 .. ");
				args[1].Accept(this, CiPriority.Argument);
			}
			Write("].sort");
			break;
		case CiId.ListAdd:
		case CiId.QueueEnqueue:
			WritePostfix(obj, ".insertBack(");
			WriteInsertedArg(((CiClassType) obj.Type).GetElementType(), args);
			break;
		case CiId.ListAddRange:
			WriteClassReference(obj);
			Write(" ~= ");
			WriteClassReference(args[0]);
			Write("[]");
			break;
		case CiId.ListAll:
			Include("std.algorithm");
			WriteClassReference(obj);
			Write("[].all!(");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.ListAny:
			Include("std.algorithm");
			WriteClassReference(obj);
			Write("[].any!(");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.ListContains:
			Include("std.algorithm");
			WriteClassReference(obj);
			Write("[].canFind(");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.ListInsert:
			this.HasListInsert = true;
			WritePostfix(obj, ".insertInPlace(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteInsertedArg(((CiClassType) obj.Type).GetElementType(), args, 1);
			break;
		case CiId.ListLast:
			WritePostfix(obj, ".back");
			break;
		case CiId.ListRemoveAt:
		case CiId.ListRemoveRange:
			this.HasListRemoveAt = true;
			WritePostfix(obj, ".removeAt");
			WriteArgsInParentheses(method, args);
			break;
		case CiId.ListIndexOf:
			Include("std.algorithm");
			WriteClassReference(obj);
			Write("[].countUntil");
			WriteArgsInParentheses(method, args);
			break;
		case CiId.DictionaryAdd:
			if (obj.Type is CiClassType klass && klass.Class.Id == CiId.SortedDictionaryClass) {
				HasSortedDictionaryInsert = true;
				WritePostfix(obj, ".replace(");
			} else
				WritePostfix(obj, ".require(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteInsertedArg(((CiClassType) obj.Type).GetValueType(), args, 1);
			break;
		case CiId.SortedSetAdd:
			WritePostfix(obj, ".insert(");
			WriteInsertedArg(((CiClassType) obj.Type).GetElementType(), args, 0);
			break;
		case CiId.SortedSetRemove:
			WritePostfix(obj, ".removeKey");
			WriteArgsInParentheses(method, args);
			break;
		case CiId.HashSetAdd:
			WritePostfix(obj, ".require(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", true)");
			break;
		case CiId.DictionaryClear:
		case CiId.HashSetClear:
			WritePostfix(obj, ".clear()");
			break;
		case CiId.DictionaryContainsKey:
		case CiId.HashSetContains:
			WriteChar('(');
			args[0].Accept(this, CiPriority.Rel);
			Write(" in ");
			obj.Accept(this, CiPriority.Primary);
			WriteChar(')');
			break;
		case CiId.SortedDictionaryRemove:
			WriteClassReference(obj);
			Write(".removeKey(tuple(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteStaticInitializer(((CiClassType) obj.Type).GetValueType());
			Write("))");
			break;
		case CiId.SortedDictionaryContainsKey:
			Write("tuple(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteStaticInitializer(((CiClassType) obj.Type).GetValueType());
			Write(") in ");
			WriteClassReference(obj);
			break;
		case CiId.QueueDequeue:
			HasQueueDequeue = true;
			goto default;
		case CiId.QueuePeek:
			WritePostfix(obj, ".front");
			break;
		case CiId.StackPeek:
			WritePostfix(obj, ".back");
			break;
		case CiId.StackPush:
			WriteClassReference(obj);
			Write(" ~= ");
			args[0].Accept(this, CiPriority.Assign);
			break;
		case CiId.StackPop:
			HasStackPop = true;
			goto default;
		case CiId.StringContains:
			Include("std.algorithm");
			WritePostfix(obj, ".canFind");
			WriteArgsInParentheses(method, args);
			break;
		case CiId.StringEndsWith:
		case CiId.StringStartsWith:
		case CiId.StringIndexOf:
		case CiId.StringLastIndexOf:
		case CiId.StringReplace:
			Include("std.string");
			goto default;
		case CiId.TextWriterWrite:
		case CiId.TextWriterWriteLine:
			WritePostfix(obj, ".");
			WriteWrite(args, method.Id == CiId.TextWriterWriteLine);
			break;
		case CiId.TextWriterWriteChar:
			WritePostfix(obj, ".write(");
			if (args[0] is CiLiteralChar)
				Write("cast(char) ");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.ConsoleWrite:
		case CiId.ConsoleWriteLine:
			WriteWrite(args, method.Id == CiId.ConsoleWriteLine);
			break;
		case CiId.EnvironmentGetEnvironmentVariable:
			Include("std.process");
			Write("environment.get");
			WriteArgsInParentheses(method, args);
			break;
		case CiId.UTF8GetByteCount:
			WritePostfix(args[0], ".length");
			break;
		case CiId.UTF8GetBytes:
			Include("std.string");
			Include("std.algorithm");
			WritePostfix(args[0], ".representation.copy(");
			WritePostfix(args[1], "[");
			args[2].Accept(this, CiPriority.Argument);
			Write(" .. $])");
			break;
		case CiId.UTF8GetString:
			Write("cast(string) (");
			WritePostfix(args[0], "[");
			args[1].Accept(this, CiPriority.Argument);
			Write(" .. $][0 .. ");
			args[2].Accept(this, CiPriority.Argument);
			Write("])");
			break;
		case CiId.RegexCompile:
			Include("std.regex");
			Write("regex(");
			args[0].Accept(this, CiPriority.Argument);
			WriteRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
			WriteChar(')');
			break;
		case CiId.RegexIsMatchRegex:
			Include("std.regex");
			WritePostfix(args[0], ".matchFirst(");
			(args.Count > 1 ? args[1] : obj).Accept(this, CiPriority.Argument);
			WriteChar(')');
			break;
		case CiId.RegexIsMatchStr:
			Include("std.regex");
			WritePostfix(args[0], ".matchFirst(");
			if (GetRegexOptions(args) != System.Text.RegularExpressions.RegexOptions.None)
				Write("regex(");
			(args.Count > 1 ? args[1] : obj).Accept(this, CiPriority.Argument);
			WriteRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
			WriteChar(')');
			break;
		case CiId.RegexEscape:
			Include("std.regex");
			Include("std.conv");
			args[0].Accept(this, CiPriority.Argument);
			Write(".escaper.to!string");
			break;
		case CiId.MatchFindStr:
			Include("std.regex");
			Write("(");
			obj.Accept(this, CiPriority.Assign);
			Write(" = ");
			args[0].Accept(this, CiPriority.Primary);
			Write(".matchFirst(");
			if (GetRegexOptions(args) != System.Text.RegularExpressions.RegexOptions.None)
				Write("regex(");
			args[1].Accept(this, CiPriority.Argument);
			WriteRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
			Write("))");
			break;
		case CiId.MatchFindRegex:
			Include("std.regex");
			Write("(");
			obj.Accept(this, CiPriority.Assign);
			Write(" = ");
			WriteMethodCall(args[0], "matchFirst", args[1]);
			WriteChar(')');
			break;
		case CiId.MatchGetCapture:
			WritePostfix(obj, "[");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(']');
			break;
		case CiId.MathMethod:
		case CiId.MathAbs:
		case CiId.MathCeiling:
		case CiId.MathFusedMultiplyAdd:
		case CiId.MathLog2:
		case CiId.MathTruncate:
		case CiId.MathIsFinite:
		case CiId.MathIsInfinity:
		case CiId.MathIsNaN:
		case CiId.MathRound:
			Include("std.math");
			Write(GetMathMethodName(method));
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathClamp:
		case CiId.MathMinDouble:
		case CiId.MathMinInt:
		case CiId.MathMaxDouble:
		case CiId.MathMaxInt:
			Include("std.algorithm");
			Write(GetMathMethodName(method));
			WriteArgsInParentheses(method, args);
			break;
		default:
			if (obj != null) {
				if (IsReferenceTo(obj, CiId.BasePtr))
					Write("super.");
				else {
					WriteClassReference(obj);
					WriteChar('.');
				}
			}
			WriteName(method);
			WriteArgsInParentheses(method, args);
			break;
		}
	}

	protected void WriteClassReference(CiExpr expr, CiPriority priority = CiPriority.Primary)
	{
		if (IsStructPtr(expr.Type)) {
			Write("(*");
			expr.Accept(this, priority);
			WriteChar(')');
		}
		else
			expr.Accept(this, priority);
	}

	protected override void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
	{
		WriteClassReference(expr.Left);
		CiClassType klass = (CiClassType) expr.Left.Type;
		switch (klass.Class.Id) {
		case CiId.ArrayPtrClass:
		case CiId.ArrayStorageClass:
		case CiId.DictionaryClass:
		case CiId.ListClass:
			WriteChar('[');
			expr.Right.Accept(this, CiPriority.Argument);
			WriteChar(']');
			break;
		case CiId.SortedDictionaryClass:
			if (parent != CiPriority.Assign) {
				HasSortedDictionaryFind = true;
				Write(".find(");
				WriteStronglyCoerced(klass.GetKeyType(), expr.Right);
				Write(")");
				return;
			} else
				throw new InvalidOperationException();
		default:
			throw new ArgumentOutOfRangeException();
		}
	}


	static bool IsPtrTo(CiExpr ptr, CiExpr other) => ptr.Type is CiClassType klass && klass.Class.Id != CiId.StringClass && klass.IsAssignableFrom(other.Type);

	protected  bool IsIsComparable(CiExpr expr) {
		if (expr is CiLiteralNull)
			return true;
		if (expr.Type is CiClassType klass) {
			switch (klass.Class.Id) {
			case CiId.ArrayPtrClass:
				return true;
			default:
				break;
			}
		}
		return false;
	}

	protected string GetEqOp(CiExpr left, CiExpr right, bool not) {
		var op = (IsIsComparable(left) || IsIsComparable(right))
			? (not ? " !is " : " is ")
			: (not ? " != " : " == ");
		return op;
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		CiType coercedType;
		if (IsPtrTo(expr.Left, expr.Right))
			coercedType = expr.Left.Type;
		else if (IsPtrTo(expr.Right, expr.Left))
			coercedType = expr.Right.Type;
		else {
			base.WriteEqual(expr, parent, not);
			return;
		}
		if (parent > CiPriority.Equality)
			WriteChar('(');
		WriteCoerced(coercedType, expr.Left, CiPriority.Equality);
		Write(GetEqOp(expr.Left, expr.Right, not));
		WriteCoerced(coercedType, expr.Right, CiPriority.Equality);
		if (parent > CiPriority.Equality)
			WriteChar(')');
	}

	protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left is CiBinaryExpr indexing
		 && indexing.Op == CiToken.LeftBracket
		 && indexing.Left.Type is CiClassType dict) {
			switch (dict.Class.Id) {
			case CiId.SortedDictionaryClass:
				HasSortedDictionaryInsert = true;
				WritePostfix(indexing.Left, ".replace(");
				indexing.Right.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteNotPromoted(expr.Type, expr.Right);
				Write(")");
				return;
			throw new NotImplementedException();
			default:
				break;
			}
		}
		base.WriteAssign(expr, parent);
	}

	void WriteIsVar(CiExpr expr, CiVar def, CiPriority parent)
	{
		CiPriority thisPriority = def.Name == "_" ? CiPriority.Primary : CiPriority.Assign;
		if (parent > thisPriority)
			WriteChar('(');
		if (def.Name != "_") {
			WriteName(def);
			Write(" = ");
		}
		Write("cast(");
		WriteType(def.Type, true);
		Write(") ");
		expr.Accept(this, CiPriority.Primary);
		if (parent > thisPriority)
			WriteChar(')');
	}

	public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Is:
			if (parent >= CiPriority.Or && parent <= CiPriority.Mul)
				parent = CiPriority.Primary;
			if (parent > CiPriority.Equality)
				WriteChar('(');
			if (expr.Right is CiSymbolReference symbol) {
				Write("cast(");
				Write(symbol.Symbol.Name);
				Write(") ");
				expr.Left.Accept(this, CiPriority.Primary);
			}
			else
				WriteIsVar(expr.Left, (CiVar) expr.Right, CiPriority.Equality);
			Write(" !is null");
			if (parent > CiPriority.Equality)
				WriteChar(')');
			return;
		case CiToken.Plus:
			if (expr.Type.Id == CiId.StringStorageType) {
				expr.Left.Accept(this, CiPriority.Assign);
				Write(" ~ ");
				expr.Right.Accept(this, CiPriority.Assign);
				return;
			}
			break;
		case CiToken.AddAssign:
			if (expr.Left.Type.Id == CiId.StringStorageType) {
				expr.Left.Accept(this, CiPriority.Assign);
				Write(" ~= ");
				WriteAssignRight(expr);
				return;
			}
			break;
		default:
			break;
		}
		base.VisitBinaryExpr(expr, parent);
	}

	public override void VisitLambdaExpr(CiLambdaExpr expr)
	{
		WriteName(expr.First);
		Write(" => ");
		expr.Body.Accept(this, CiPriority.Statement);
	}

	protected override void WriteAssert(CiAssert statement)
	{
		Write("assert(");
		statement.Cond.Accept(this, CiPriority.Argument);
		if (statement.Message != null) {
			Write(", ");
			statement.Message.Accept(this, CiPriority.Argument);
		}
		WriteLine(");");
	}

	public override void VisitForeach(CiForeach statement)
	{
		Write("foreach (");
		if (statement.Collection.Type is CiClassType dict && dict.Class.TypeParameterCount == 2) {
			WriteTypeAndName(statement.GetVar());
			Write(", ");
			WriteTypeAndName(statement.GetValueVar());
		}
		else
			WriteTypeAndName(statement.GetVar());
		Write("; ");
		WriteClassReference(statement.Collection);
		if (statement.Collection.Type is CiClassType set && set.Class.Id == CiId.HashSetClass)
			Write(".byKey");
		WriteChar(')');
		WriteChild(statement.Body);
	}

	public override void VisitLock(CiLock statement)
	{
		Write("synchronized (");
		statement.Lock.Accept(this, CiPriority.Argument);
		WriteChar(')');
		WriteChild(statement.Body);
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		WriteTemporaries(statement.Value);
		if (statement.IsTypeMatching()) {
			WriteSwitchWhenVars(statement, false);
			int gotoId = GetSwitchGoto(statement);
			string op = "if (";
			foreach (CiCase kase in statement.Cases) {
				foreach (CiExpr value in kase.Values) {
					Write(op);
					switch (value) {
					case CiVar def:
						WriteIsVar(statement.Value, def, CiPriority.Equality); // FIXME: side effect in every if
						Write(" !is null");
						break;
					case CiLiteralNull _:
						statement.Value.Accept(this, CiPriority.Equality); // FIXME: side effect in every if
						Write(" is null");
						break;
					case CiBinaryExpr when1 when when1.Op == CiToken.When:
						CiVar whenVar = (CiVar) when1.Left;
						WriteIsVar(statement.Value, whenVar, CiPriority.Equality); // FIXME: side effect in every if
						Write(" !is null && ");
						when1.Right.Accept(this, CiPriority.CondAnd);
						break;
					default:
						throw new NotImplementedException(value.GetType().Name);
					}
					op = " || ";
				}
				WriteChar(')');
				WriteIfCaseBody(kase.Body, gotoId < 0);
				op = "else if (";
			}
			EndSwitchAsIfs(statement, gotoId);
		} else {
			Write("switch (");
			WriteSwitchValue(statement.Value);
			WriteLine(") {");
			foreach (CiCase kase in statement.Cases)
				WriteSwitchCase(statement, kase);
			WriteLine("default:");
			this.Indent++;
			if (statement.DefaultBody.Count > 0)
				WriteSwitchCaseBody(statement.DefaultBody);
			else
				WriteLine("assert(false);");
			this.Indent--;
			WriteCharLine('}');
		}
	}

	public override void VisitThrow(CiThrow statement)
	{
		Include("std.exception");
		Write("throw new Exception(");
		statement.Message.Accept(this, CiPriority.Argument);
		WriteLine(");");
	}

	protected override void WriteEnum(CiEnum enu)
	{
		WriteNewLine();
		WriteDoc(enu.Documentation);
		WritePublic(enu);
		Write("enum ");
		Write(enu.Name);
		OpenBlock();
		enu.AcceptValues(this);
		WriteNewLine();
		CloseBlock();
	}

	protected override void WriteConst(CiConst konst)
	{
		WriteDoc(konst.Documentation);
		Write("static ");
		WriteTypeAndName(konst);
		Write(" = ");
		WriteCoercedExpr(konst.Type, konst.Value);
		WriteLine(";");
	}

	protected override void WriteField(CiField field)
	{
		WriteNewLine();
		WriteDoc(field.Documentation);
		WriteVisibility(field.Visibility);
		WriteTypeAndName(field);
		if (field.Value != null && field.Value is CiLiteral) {
			Write(" = ");
			WriteCoercedExpr(field.Type, field.Value);
		}
		WriteLine(";");
	}

	protected override void WriteMethod(CiMethod method)
	{
		if (method.Id == CiId.ClassToString && method.CallType == CiCallType.Abstract)
			return;
		WriteNewLine();
		WriteDoc(method.Documentation);
		WriteParametersDoc(method);
		WriteVisibility(method.Visibility);
		if (method.Id == CiId.ClassToString)
			Write("override ");
		else
			WriteCallType(method.CallType, "final override ");
		WriteTypeAndName(method);
		WriteParameters(method, true);
		WriteBody(method);
	}

	protected override void WriteClass(CiClass klass, CiProgram program)
	{
		WriteNewLine();
		WriteDoc(klass.Documentation);
		if (klass.CallType == CiCallType.Sealed)
			Write("final ");
		OpenClass(klass, "", " : ");
		if (NeedsConstructor(klass)) {
			if (klass.Constructor != null) {
				WriteDoc(klass.Constructor.Documentation);
				WriteVisibility(klass.Constructor.Visibility);
			}
			else
				Write("private ");
			WriteLine("this()");
			OpenBlock();
			WriteConstructorBody(klass);
			CloseBlock();
		}
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (!(symbol is CiMember member))
				continue;
			switch (symbol) {
			case CiConst konst:
				WriteConst(konst);
				break;
			case CiField field:
				WriteField(field);
				break;
			case CiMethod method:
				WriteMethod(method);
				this.CurrentTemporaries.Clear();
				break;
			case CiVar _: // "this"
				break;
			default:
				throw new NotImplementedException(symbol.Type.ToString());
			}
		}
		CloseBlock();
	}

	protected static bool IsLong(CiSymbolReference expr)
	{
		switch (expr.Symbol.Id) {
		case CiId.ArrayLength:
		case CiId.StringLength:
		case CiId.ListCount:
			return true;
		default:
			return false;
		}
	}

	protected void WriteArrayPtr(CiExpr expr, CiPriority parent)
	{
		switch (expr.Type) {
		case CiArrayStorageType _:
		case CiStringType _:
			WritePostfix(expr, ".ptr");
			break;
		default:
			expr.Accept(this, parent);
			break;
		}
	}


	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		if (type is CiRangeType left && (expr is CiLiteralLong || expr.Type is CiIntegerType || (expr.Type is CiRangeType range && !type.IsAssignableFrom(range)))) {
			WriteStaticCast(type, expr);
			return;
		} else if (type is CiIntegerType && expr is CiSymbolReference symref && IsLong(symref)) {
			WriteStaticCast(type, expr);
			return;
		} else if (type is CiFloatingType && !(expr.Type is CiFloatingType)) {
			WriteStaticCast(type, expr);
			return;
		} else if (type is CiClassType klass && !(klass is CiDynamicPtrType) && !(klass is CiStorageType)) {
			switch (expr.Type) {
			case CiArrayStorageType _:
				base.WriteCoercedInternal(type, expr, CiPriority.Primary);
				Write("[]");
				return;
			default:
				break;
			}
		}
		base.WriteCoercedInternal(type, expr, parent);
	}

	void WriteResources(Dictionary<string, byte[]> resources)
	{
		WriteNewLine();
		WriteLine("private static struct CiResource");
		OpenBlock();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			Write("private static ubyte[] ");
			WriteResource(name, -1);
			WriteLine(" = [");
			WriteChar('\t');
			WriteBytes(resources[name]);
			WriteLine(" ];");
		}
		CloseBlock();
	}

	public override void WriteProgram(CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		this.HasListInsert = false;
		this.HasListRemoveAt = false;

		OpenStringWriter();
		if (this.Namespace != null) {
			Write("struct ");
			WriteLine(this.Namespace);
			OpenBlock();
			WriteLine("static:");
		}
		WriteTopLevelNatives(program);
		WriteTypes(program);
		if (program.Resources.Count > 0)
			WriteResources(program.Resources);
		if (this.Namespace != null)
			CloseBlock();

		if (this.HasListInsert || this.HasListRemoveAt || this.HasStackPop)
			Include("std.container.array");
		if (this.HasSortedDictionaryFind || this.HasSortedDictionaryInsert)
			Include("std.container.rbtree");
		if (this.HasSortedDictionaryFind || this.HasSortedDictionaryInsert)
			Include("std.typecons");
		if (this.HasQueueDequeue) {
			Include("std.container.dlist");
		}

		CreateFile(this.OutputFile);
		WriteIncludes("import ", ";");
		if (this.HasListInsert) {
			WriteNewLine();
			WriteLine("private void insertInPlace(T, U...)(Array!T* arr, size_t pos, auto ref U stuff)");
			OpenBlock();
			WriteLine("arr.insertAfter((*arr)[0 .. pos], stuff);");
			CloseBlock();
		}
		if (this.HasListRemoveAt) {
			WriteNewLine();
			WriteLine("private void removeAt(T)(Array!T* arr, size_t pos, size_t count = 1)");
			OpenBlock();
			WriteLine("arr.linearRemove((*arr)[pos .. pos + count]);");
			CloseBlock();
		}
		if (this.HasSortedDictionaryFind) {
			WriteNewLine();
			WriteLine("private U find(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key)");
			OpenBlock();
			WriteLine("return dict.equalRange(tuple(key, U.init)).front[1];");
			CloseBlock();
		}
		if (this.HasSortedDictionaryInsert) {
			WriteNewLine();
			WriteLine("private void replace(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key, lazy U value)");
			OpenBlock();
			WriteLine("dict.removeKey(tuple(key, U.init));");
			WriteLine("dict.insert(tuple(key, value));");
			CloseBlock();
		}
		if (this.HasQueueDequeue) {
			WriteNewLine();
			WriteLine("private T dequeue(T)(ref DList!T q)");
			OpenBlock();
			WriteLine("scope(exit) q.removeFront(); return q.front;");
			CloseBlock();
		}
		if (this.HasStackPop) {
			WriteNewLine();
			WriteLine("private T pop(T)(ref Array!T stack)");
			OpenBlock();
			WriteLine("scope(exit) stack.removeBack(); return stack.back;");
			CloseBlock();
		}
		CloseStringWriter();
		CloseFile();
	}
}

}
