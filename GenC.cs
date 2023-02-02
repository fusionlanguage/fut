// GenC.cs - C code generator
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
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

public class GenC : GenCCpp
{
	bool StringAssign;
	bool StringSubstring;
	bool StringAppend;
	bool StringIndexOf;
	bool StringLastIndexOf;
	bool StringEndsWith;
	bool StringReplace;
	bool StringFormat;
	bool MatchFind;
	bool MatchPos;
	bool PtrConstruct;
	bool SharedMake;
	bool SharedAddRef;
	bool SharedRelease;
	bool SharedAssign;
	readonly SortedDictionary<string, string> ListFrees = new SortedDictionary<string, string>();
	bool TreeCompareInteger;
	bool TreeCompareString;
	readonly SortedSet<TypeCode> Compares = new SortedSet<TypeCode>();
	readonly SortedSet<TypeCode> Contains = new SortedSet<TypeCode>();
	readonly List<CiVar> VarsToDestruct = new List<CiVar>();
	protected CiClass CurrentClass;

	protected override CiContainerType GetCurrentContainer() => this.CurrentClass;
	protected override string GetTargetName() => "C";

	protected override void WriteSelfDoc(CiMethod method)
	{
		if (method.CallType == CiCallType.Static)
			return;
		Write(" * @param self This <code>");
		WriteName(method.Parent);
		WriteLine("</code>.");
	}

	protected override void IncludeStdInt() => Include("stdint.h");

	protected override void IncludeAssert() => Include("assert.h");

	protected override void IncludeMath() => Include("math.h");

	protected virtual void IncludeStdBool() => Include("stdbool.h");

	public override void VisitLiteralNull() => Write("NULL");

	protected virtual void WritePrintfLongPrefix() => WriteChar('j');

	protected override void WritePrintfWidth(CiInterpolatedPart part)
	{
		base.WritePrintfWidth(part);
		if (IsStringSubstring(part.Argument, out bool _, out CiExpr _, out CiExpr _, out CiExpr _)) {
			Trace.Assert(part.Precision < 0);
			Write(".*");
		}
		if (part.Argument.Type.Id == CiId.LongType)
			WritePrintfLongPrefix();
	}

	protected override void WriteInterpolatedStringArg(CiExpr expr)
	{
		if (IsStringSubstring(expr, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr length)) {
			length.Accept(this, CiPriority.Argument);
			Write(", ");
			if (cast)
				Write("(const char *) ");
			WriteArrayPtrAdd(ptr, offset);
		}
		else if (expr.Type is CiClassType klass && klass.Class.Id != CiId.StringClass) {
			// TODO: abstract, virtual, override
			Write(this.Namespace);
			Write(klass.Class.Name);
			Write("_ToString(");
			WriteClassPtr(klass.Class, expr, CiPriority.Argument);
			WriteChar(')');
		}
		else
			base.WriteInterpolatedStringArg(expr);
	}

	public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		Include("stdarg.h");
		Include("stdio.h");
		this.StringFormat = true;
		Write("CiString_Format(");
		WritePrintf(expr, false);
	}

	protected virtual void WriteCamelCaseNotKeyword(string name)
	{
		switch (name) {
		case "this":
			Write("self");
			break;
		case "Asm":
		case "Assert":
		case "Auto":
		case "Bool":
		case "Break":
		case "Byte":
		case "Case":
		case "Char":
		case "Class":
		case "Const":
		case "Continue":
		case "Default":
		case "Do":
		case "Double":
		case "Else":
		case "Enum":
		case "Extern":
		case "False":
		case "Float":
		case "For":
		case "Foreach":
		case "Goto":
		case "If":
		case "Inline":
		case "Int":
		case "Long":
		case "Register":
		case "Restrict":
		case "Return":
		case "Short":
		case "Signed":
		case "Sizeof":
		case "Static":
		case "Struct":
		case "Switch":
		case "True":
		case "Typedef":
		case "Typeof": // gcc extension
		case "Union":
		case "Unsigned":
		case "Void":
		case "Volatile":
		case "While":
		case "asm":
		case "auto":
		case "char":
		case "extern":
		case "goto":
		case "inline":
		case "register":
		case "restrict":
		case "signed":
		case "sizeof":
		case "struct":
		case "typedef":
		case "typeof": // gcc extension
		case "union":
		case "unsigned":
		case "volatile":
			WriteCamelCase(name);
			WriteChar('_');
			break;
		default:
			WriteCamelCase(name);
			break;
		}
	}

	protected override void WriteName(CiSymbol symbol)
	{
		switch (symbol) {
		case CiContainerType _:
			Write(this.Namespace);
			Write(symbol.Name);
			break;
		case CiMethod _:
			Write(this.Namespace);
			Write(symbol.Parent.Name);
			WriteChar('_');
			Write(symbol.Name);
			break;
		case CiConst _:
			if (symbol.Parent is CiContainerType) {
				Write(this.Namespace);
				Write(symbol.Parent.Name);
				WriteChar('_');
			}
			WriteUppercaseWithUnderscores(symbol.Name);
			break;
		default:
			WriteCamelCaseNotKeyword(symbol.Name);
			break;
		}
	}

	void WriteSelfForField(CiClass fieldClass)
	{
		Write("self->");
		for (CiClass klass = this.CurrentClass; klass != fieldClass; klass = (CiClass) klass.Parent)
			Write("base.");
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol.Parent is CiForeach forEach) {
			CiClassType klass = (CiClassType) forEach.Collection.Type;
			switch (klass.Class.Id) {
			case CiId.StringClass:
			case CiId.ListClass:
				if (parent == CiPriority.Primary)
					WriteChar('(');
				WriteChar('*');
				WriteCamelCaseNotKeyword(symbol.Name);
				if (parent == CiPriority.Primary)
					WriteChar(')');
				return;
			case CiId.ArrayStorageClass:
				if (klass.GetElementType() is CiStorageType) {
					if (parent > CiPriority.Add)
						WriteChar('(');
					forEach.Collection.Accept(this, CiPriority.Add);
					Write(" + ");
					WriteCamelCaseNotKeyword(symbol.Name);
					if (parent > CiPriority.Add)
						WriteChar(')');
				}
				else {
					forEach.Collection.Accept(this, CiPriority.Primary);
					WriteChar('[');
					WriteCamelCaseNotKeyword(symbol.Name);
					WriteChar(']');
				}
				return;
			default:
				break;
			}
		}
		if (symbol is CiField)
			WriteSelfForField((CiClass) symbol.Parent);
		WriteName(symbol);
	}

	void WriteMatchProperty(CiSymbolReference expr, int which)
	{
		this.MatchPos = true;
		Write("CiMatch_GetPos(");
		expr.Left.Accept(this, CiPriority.Argument);
		Write(", ");
		VisitLiteralLong(which);
		WriteChar(')');
	}

	static bool IsDictionaryClassStgIndexing(CiExpr expr)
	{
		return expr is CiBinaryExpr indexing
			&& indexing.Op == CiToken.LeftBracket
			&& indexing.Left.Type is CiClassType dict
			&& dict.Class.TypeParameterCount == 2
			&& dict.GetValueType() is CiStorageType;
	}

	public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.ListCount:
		case CiId.StackCount:
			expr.Left.Accept(this, CiPriority.Primary);
			Write("->len");
			break;
		case CiId.QueueCount:
			expr.Left.Accept(this, CiPriority.Primary);
			if (expr.Left.Type is CiStorageType)
				WriteChar('.');
			else
				Write("->");
			Write("length");
			break;
		case CiId.HashSetCount:
		case CiId.DictionaryCount:
			WriteCall("g_hash_table_size", expr.Left);
			break;
		case CiId.SortedDictionaryCount:
			WriteCall("g_tree_nnodes", expr.Left);
			break;
		case CiId.MatchStart:
			WriteMatchProperty(expr, 0);
			break;
		case CiId.MatchEnd:
			WriteMatchProperty(expr, 1);
			break;
		case CiId.MatchLength:
			WriteMatchProperty(expr, 2);
			break;
		case CiId.MatchValue:
			Write("g_match_info_fetch(");
			expr.Left.Accept(this, CiPriority.Argument);
			Write(", 0)");
			break;
		default:
			if (expr.Left == null || expr.Symbol is CiConst)
				WriteLocalName(expr.Symbol, parent);
			else if (IsDictionaryClassStgIndexing(expr.Left)) {
				expr.Left.Accept(this, CiPriority.Primary);
				Write("->");
				WriteName(expr.Symbol);
			}
			else
				base.VisitSymbolReference(expr, parent);
			break;
		}
	}

	void WriteGlib(string s)
	{
		Include("glib.h");
		Write(s);
	}

	protected virtual void WriteStringPtrType() => Write("const char *");

	protected virtual void WriteClassType(CiClassType klass, bool space)
	{
		switch (klass.Class.Id) {
		case CiId.None:
			if (!(klass is CiReadWriteClassType))
				Write("const ");
			WriteName(klass.Class);
			if (!(klass is CiStorageType))
				Write(" *");
			else if (space)
				WriteChar(' ');
			break;
		case CiId.StringClass:
			if (klass.IsNullable())
				WriteStringPtrType();
			else
				Write("char *");
			break;
		case CiId.ListClass:
		case CiId.StackClass:
			WriteGlib("GArray *");
			break;
		case CiId.QueueClass:
			WriteGlib("GQueue ");
			if (!(klass is CiStorageType))
				WriteChar('*');
			break;
		case CiId.HashSetClass:
		case CiId.DictionaryClass:
			WriteGlib("GHashTable *");
			break;
		case CiId.SortedDictionaryClass:
			WriteGlib("GTree *");
			break;
		case CiId.RegexClass:
			if (!(klass is CiReadWriteClassType))
				Write("const ");
			WriteGlib("GRegex *");
			break;
		case CiId.MatchClass:
			if (!(klass is CiReadWriteClassType))
				Write("const ");
			WriteGlib("GMatchInfo *");
			break;
		case CiId.LockClass:
			NotYet(klass, "Lock");
			Include("threads.h");
			Write("mtx_t ");
			break;
		default:
			NotSupported(klass, klass.Class.Name);
			break;
		}
	}

	void WriteArrayPrefix(CiType type)
	{
		if (type is CiClassType array && array.IsArray()) {
			WriteArrayPrefix(array.GetElementType());
			if (!(type is CiArrayStorageType)) {
				if (array.GetElementType() is CiArrayStorageType)
					WriteChar('(');
				if (!(type is CiReadWriteClassType))
					Write("const ");
				WriteChar('*');
			}
		}
	}

	void WriteDefinition(CiType type, Action symbol, bool promote, bool space)
	{
		CiType baseType = type.GetBaseType();
		switch (baseType) {
		case CiIntegerType integer:
			WriteTypeCode(GetIntegerTypeCode(integer, promote && type == baseType));
			if (space)
				WriteChar(' ');
			break;
		case CiEnum _:
			if (baseType.Id == CiId.BoolType) {
				IncludeStdBool();
				Write("bool");
			}
			else
				WriteName(baseType);
			if (space)
				WriteChar(' ');
			break;
		case CiClassType klass:
			WriteClassType(klass, space);
			break;
		default:
			Write(baseType.Name);
			if (space)
				WriteChar(' ');
			break;
		}
		WriteArrayPrefix(type);
		symbol();
		while (type.IsArray()) {
			CiType elementType = ((CiClassType) type).GetElementType();
			if (type is CiArrayStorageType arrayStorage) {
				WriteChar('[');
				VisitLiteralLong(arrayStorage.Length);
				WriteChar(']');
			}
			else if (elementType is CiArrayStorageType)
				WriteChar(')');
			type = elementType;
		}
	}

	void WriteSignature(CiMethod method, Action symbol)
	{
		if (method.Type.Id == CiId.VoidType && method.Throws) {
			IncludeStdBool();
			Write("bool ");
			symbol();
		}
		else
			WriteDefinition(method.Type, symbol, true, true);
	}

	protected override void WriteType(CiType type, bool promote)
	{
		WriteDefinition(type, () => {}, promote, type is CiClassType arrayPtr && arrayPtr.Class.Id == CiId.ArrayPtrClass);
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteDefinition(value.Type, () => WriteName(value), true, true);
	}

	void WriteDynamicArrayCast(CiType elementType)
	{
		WriteChar('(');
		WriteDefinition(elementType, () => Write(elementType.IsArray() ? "(*)" : "*"), false, true);
		Write(") ");
	}

	void WriteXstructorPtr(bool need, CiClass klass, string name)
	{
		if (need) {
			Write("(CiMethodPtr) ");
			WriteName(klass);
			WriteChar('_');
			Write(name);
		}
		else
			Write("NULL");
	}

	void WriteXstructorPtrs(CiClass klass)
	{
		WriteXstructorPtr(NeedsConstructor(klass), klass, "Construct");
		Write(", ");
		WriteXstructorPtr(NeedsDestructor(klass), klass, "Destruct");
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		this.SharedMake = true;
		if (parent > CiPriority.Mul)
			WriteChar('(');
		WriteDynamicArrayCast(elementType);
		Write("CiShared_Make(");
		lengthExpr.Accept(this, CiPriority.Argument);
		Write(", sizeof(");
		WriteType(elementType, false);
		Write("), ");
		switch (elementType) {
		case CiStringStorageType _:
			this.PtrConstruct = true;
			this.ListFrees["String"] = "free(*(void **) ptr)";
			Write("(CiMethodPtr) CiPtr_Construct, CiList_FreeString");
			break;
		case CiStorageType storage:
			WriteXstructorPtrs(storage.Class);
			break;
		case CiDynamicPtrType _:
			this.PtrConstruct = true;
			this.SharedRelease = true;
			this.ListFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
			Write("(CiMethodPtr) CiPtr_Construct, CiList_FreeShared");
			break;
		default:
			Write("NULL, NULL");
			break;
		}
		WriteChar(')');
		if (parent > CiPriority.Mul)
			WriteChar(')');
	}

	static bool IsNewString(CiExpr expr)
	{
		return expr is CiInterpolatedString
			|| (expr is CiCallExpr call && expr.Type.Id == CiId.StringStorageType && (call.Method.Symbol.Id != CiId.StringSubstring || call.Arguments.Count == 2));
	}

	void WriteStringStorageValue(CiExpr expr)
	{
		if (IsStringSubstring(expr, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr length)) {
			Include("string.h");
			this.StringSubstring = true;
			Write("CiString_Substring(");
			if (cast)
				Write("(const char *) ");
			WriteArrayPtrAdd(ptr, offset);
			Write(", ");
			length.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}
		else if (IsNewString(expr))
			expr.Accept(this, CiPriority.Argument);
		else {
			Include("string.h");
			WriteCall("strdup", expr);
		}
	}

	static bool IsHeapAllocated(CiType type) => type.Id == CiId.StringStorageType || type is CiDynamicPtrType;

	protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		switch (value) {
		case null:
			if (IsHeapAllocated(array.GetStorageType()))
				Write(" = { NULL }");
			break;
		case CiLiteral literal when literal.IsDefaultValue():
			Write(" = { ");
			literal.Accept(this, CiPriority.Argument);
			Write(" }");
			break;
		default:
			NotSupported(value, "Array initializer");
			break;
		}
	}

	string GetListDestroy(CiType type)
	{
		if (type is CiClassType klass
		 && (klass.Class.Id == CiId.ListClass || klass.Class.Id == CiId.StackClass)) {
			CiType elementType = klass.GetElementType();
			if (elementType.Id == CiId.StringStorageType) {
				this.ListFrees["String"] = "free(*(void **) ptr)";
				return "CiList_FreeString";
			}
			if (elementType is CiDynamicPtrType) {
				this.SharedRelease = true;
				this.ListFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
				return "CiList_FreeShared";
			}
			if (elementType is CiStorageType storage) {
				switch (storage.Class.Id) {
				case CiId.ListClass:
				case CiId.StackClass:
					this.ListFrees["List"] = "g_array_free(*(GArray **) ptr, TRUE)";
					return "CiList_FreeList";
				case CiId.HashSetClass:
				case CiId.DictionaryClass:
					this.ListFrees["Dictionary"] = "g_hash_table_unref(*(GHashTable **) ptr)";
					return "CiList_FreeDictionary";
				case CiId.SortedDictionaryClass:
					this.ListFrees["SortedDictionary"] = "g_tree_unref(*(GTree **) ptr)";
					return "CiList_FreeSortedDictionary";
				default:
					if (NeedsDestructor(storage.Class))
						return $"(GDestroyNotify) {storage.Class.Name}_Destruct";
					break;
				}
			}
		}
		return null;
	}

	string GetDictionaryDestroy(CiType type)
	{
		switch (type) {
		case CiStringStorageType _:
		case CiArrayStorageType _:
			return "free";
		case CiStorageType storage:
			switch (storage.Class.Id) {
			case CiId.ListClass:
			case CiId.StackClass:
				return "(GDestroyNotify) g_array_unref";
			case CiId.HashSetClass:
			case CiId.DictionaryClass:
				return "(GDestroyNotify) g_hash_table_unref";
			case CiId.SortedDictionaryClass:
				return "(GDestroyNotify) g_tree_unref";
			default:
				return NeedsDestructor(storage.Class) ? $"(GDestroyNotify) {storage.Class.Name}_Delete" /* TODO: emit */ : "free";
			}
		case CiDynamicPtrType _:
			this.SharedRelease = true;
			return "CiShared_Release";
		default:
			return "NULL";
		}
	}

	void WriteHashEqual(CiType keyType)
	{
		Write(keyType is CiStringType ? "g_str_hash, g_str_equal" : "NULL, NULL");
	}

	void WriteNewHashTable(CiType keyType, string valueDestroy)
	{
		Write("g_hash_table_new");
		string keyDestroy = GetDictionaryDestroy(keyType);
		if (keyDestroy == "NULL" && valueDestroy == "NULL") {
			WriteChar('(');
			WriteHashEqual(keyType);
		}
		else {
			Write("_full(");
			WriteHashEqual(keyType);
			Write(", ");
			Write(keyDestroy);
			Write(", ");
			Write(valueDestroy);
		}
		WriteChar(')');
	}

	protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
	{
		switch (klass.Class.Id) {
		case CiId.ListClass:
		case CiId.StackClass:
			Write("g_array_new(FALSE, FALSE, sizeof(");
			WriteType(klass.GetElementType(), false);
			Write("))");
			break;
		case CiId.QueueClass:
			Write("G_QUEUE_INIT");
			break;
		case CiId.HashSetClass:
			WriteNewHashTable(klass.GetElementType(), "NULL");
			break;
		case CiId.DictionaryClass:
			WriteNewHashTable(klass.GetKeyType(), GetDictionaryDestroy(klass.GetValueType()));
			break;
		case CiId.SortedDictionaryClass:
			string valueDestroy = GetDictionaryDestroy(klass.GetValueType());
			if (klass.GetKeyType().Id == CiId.StringPtrType && valueDestroy == "NULL")
				Write("g_tree_new((GCompareFunc) strcmp");
			else {
				Write("g_tree_new_full(CiTree_Compare");
				switch (klass.GetKeyType()) {
				case CiIntegerType _:
					this.TreeCompareInteger = true;
					Write("Integer");
					break;
				case CiStringType _:
					this.TreeCompareString = true;
					Write("String");
					break;
				default:
					NotSupported(klass.GetKeyType(), klass.GetKeyType().ToString());
					break;
				}
				Write(", NULL, ");
				Write(GetDictionaryDestroy(klass.GetKeyType()));
				Write(", ");
				Write(valueDestroy);
			}
			WriteChar(')');
			break;
		default:
			this.SharedMake = true;
			if (parent > CiPriority.Mul)
				WriteChar('(');
			WriteStaticCastType(klass);
			Write("CiShared_Make(1, sizeof(");
			WriteName(klass.Class);
			Write("), ");
			WriteXstructorPtrs(klass.Class);
			WriteChar(')');
			if (parent > CiPriority.Mul)
				WriteChar(')');
			break;
		}
	}

	protected override void WriteStorageInit(CiNamedValue def)
	{
		if (((CiStorageType) def.Type).Class.TypeParameterCount > 0) // built-in collections
			base.WriteStorageInit(def);
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Value == null && IsHeapAllocated(def.Type))
			Write(" = NULL");
		else
			base.WriteVarInit(def);
	}

	void WriteAssignTemporary(CiType type, CiExpr expr)
	{
		Write(" = ");
		if (expr != null)
			WriteCoerced(type, expr, CiPriority.Argument);
		else
			WriteNewStorage(type);
	}

	int WriteCTemporary(CiType type, CiExpr expr)
	{
		EnsureChildBlock();
		bool assign = expr != null || (type is CiClassType klass && (klass.Class.Id == CiId.ListClass || klass.Class.Id == CiId.DictionaryClass || klass.Class.Id == CiId.SortedDictionaryClass));
		int id = this.CurrentTemporaries.IndexOf(type);
		if (id < 0) {
			id = this.CurrentTemporaries.Count;
			WriteDefinition(type, () => { Write("citemp"); VisitLiteralLong(id); }, false, true);
			if (assign)
				WriteAssignTemporary(type, expr);
			WriteCharLine(';');
			this.CurrentTemporaries.Add(expr);
		}
		else if (assign) {
			Write("citemp");
			VisitLiteralLong(id);
			WriteAssignTemporary(type, expr);
			WriteCharLine(';');
			this.CurrentTemporaries[id] = expr;
		}
		return id;
	}

	void WriteStorageTemporary(CiExpr expr)
	{
		if (IsNewString(expr)
		 || (expr is CiCallExpr && expr.Type is CiStorageType))
			WriteCTemporary(expr.Type, expr);
	}

	void WriteCTemporaries(CiExpr expr)
	{
		switch (expr) {
		case CiVar def:
			if (def.Value != null)
				WriteCTemporaries(def.Value);
			break;
		case CiAggregateInitializer init:
			foreach (CiBinaryExpr field in init.Items)
				WriteCTemporaries(field.Right);
			break;
		case CiLiteral _:
		case CiLambdaExpr _:
		case CiConst _:
			break;
		case CiInterpolatedString interp:
			foreach (CiInterpolatedPart part in interp.Parts)
				WriteCTemporaries(part.Argument);
			break;
		case CiSymbolReference symbol:
			if (symbol.Left != null)
				WriteCTemporaries(symbol.Left);
			break;
		case CiUnaryExpr unary:
			if (unary.Inner != null) // new C()
				WriteCTemporaries(unary.Inner);
			break;
		case CiBinaryExpr binary:
			WriteCTemporaries(binary.Left);
			if (!IsStringSubstring(binary.Left, out bool _, out CiExpr _, out CiExpr _, out CiExpr _))
				WriteStorageTemporary(binary.Left);
			WriteCTemporaries(binary.Right);
			if (binary.Op != CiToken.Assign)
				WriteStorageTemporary(binary.Right);
			break;
		case CiSelectExpr select:
			WriteCTemporaries(select.Cond);
			break;
		case CiCallExpr call:
			if (call.Method.Left != null) {
				WriteCTemporaries(call.Method.Left);
				WriteStorageTemporary(call.Method.Left);
			}
			CiVar param = ((CiMethod) call.Method.Symbol).Parameters.FirstParameter();
			foreach (CiExpr arg in call.Arguments) {
				WriteCTemporaries(arg);
				if (call.Method.Symbol.Id != CiId.ConsoleWrite && call.Method.Symbol.Id != CiId.ConsoleWriteLine && !(param.Type is CiStorageType))
					WriteStorageTemporary(arg);
				param = param.NextParameter();
			}
			break;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	bool HasTemporariesToDestruct(CiExpr expr)
	{
		switch (expr) {
		case CiAggregateInitializer init:
			return init.Items.Any(field => HasTemporariesToDestruct(field));
		case CiLiteral _:
		case CiLambdaExpr _:
			return false;
		case CiInterpolatedString interp:
			return interp.Parts.Any(part => HasTemporariesToDestruct(part.Argument));
		case CiSymbolReference symbol:
			return symbol.Left != null && HasTemporariesToDestruct(symbol.Left);
		case CiUnaryExpr unary:
			return unary.Inner != null && HasTemporariesToDestruct(unary.Inner);
		case CiBinaryExpr binary:
			return HasTemporariesToDestruct(binary.Left) || (binary.Op != CiToken.Is && HasTemporariesToDestruct(binary.Right));
		case CiSelectExpr select:
			return HasTemporariesToDestruct(select.Cond);
		case CiCallExpr call:
			return (call.Method.Left != null && (HasTemporariesToDestruct(call.Method.Left) || IsNewString(call.Method.Left)))
				|| call.Arguments.Any(arg => HasTemporariesToDestruct(arg) || IsNewString(arg));
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	protected override void CleanupTemporary(int i, CiExpr temp)
	{
		if (temp.Type.Id == CiId.StringStorageType) {
			Write("free(citemp");
			VisitLiteralLong(i);
			WriteLine(");");
		}
	}

	static bool NeedToDestruct(CiSymbol symbol)
	{
		CiType type = symbol.Type;
		while (type is CiArrayStorageType array)
			type = array.GetElementType();
		if (IsHeapAllocated(type))
			return true;
		if (type is CiStorageType storage)
			return NeedsDestructor(storage.Class) || storage.Class.TypeParameterCount > 0 /* built-in collections */ || storage.Id == CiId.MatchClass || storage.Id == CiId.LockClass;
		return false;
	}

	protected override void WriteVar(CiNamedValue def)
	{
		base.WriteVar(def);
		if (NeedToDestruct(def))
			this.VarsToDestruct.Add((CiVar) def);
	}

	void WriteGPointerCast(CiType type, CiExpr expr)
	{
		if (type is CiNumericType || type is CiEnum) {
			Write("GINT_TO_POINTER(");
			expr.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}
		else if (type.Id == CiId.StringPtrType && expr.Type.Id == CiId.StringPtrType) {
			Write("(gpointer) ");
			expr.Accept(this, CiPriority.Primary);
		}
		else
			WriteCoerced(type, expr, CiPriority.Argument);
	}

	void WriteGConstPointerCast(CiExpr expr)
	{
		if (expr.Type.IsNullable() || expr.Type.Id == CiId.StringStorageType)
			expr.Accept(this, CiPriority.Argument);
		else {
			Write("(gconstpointer) ");
			expr.Accept(this, CiPriority.Primary);
		}
	}

	void WriteQueueObject(CiExpr obj)
	{
		if (obj.Type is CiStorageType) {
			WriteChar('&');
			obj.Accept(this, CiPriority.Primary);
		}
		else
			obj.Accept(this, CiPriority.Argument);
	}

	void WriteQueueGet(string function, CiExpr obj, CiPriority parent)
	{
		CiType elementType = ((CiClassType) obj.Type).GetElementType();
		bool parenthesis;
		if (elementType is CiIntegerType && elementType.Id != CiId.LongType) {
			Write("GPOINTER_TO_INT(");
			parenthesis = true;
		}
		else {
			parenthesis = parent > CiPriority.Mul;
			if (parenthesis)
				WriteChar('(');
			WriteStaticCastType(elementType);
		}
		Write(function);
		WriteChar('(');
		WriteQueueObject(obj);
		WriteChar(')');
		if (parenthesis)
			WriteChar(')');
	}

	void StartDictionaryInsert(CiExpr dict, CiExpr key)
	{
		CiClassType type = (CiClassType) dict.Type;
		Write(type.Class.Id == CiId.SortedDictionaryClass ? "g_tree_insert(" : "g_hash_table_insert(");
		dict.Accept(this, CiPriority.Argument);
		Write(", ");
		WriteGPointerCast(type.GetKeyType(), key);
		Write(", ");
	}

	protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left is CiBinaryExpr indexing
		 && indexing.Op == CiToken.LeftBracket
		 && indexing.Left.Type is CiClassType dict
		 && dict.Class.TypeParameterCount == 2) {
			StartDictionaryInsert(indexing.Left, indexing.Right);
			WriteGPointerCast(dict.GetValueType(), expr.Right);
			WriteChar(')');
		}
		else if (expr.Left.Type.Id == CiId.StringStorageType) {
			if (parent == CiPriority.Statement
			 && IsTrimSubstring(expr) is CiExpr length) {
				WriteIndexing(expr.Left, length);
				Write(" = '\\0'");
			}
			else {
				this.StringAssign = true;
				Write("CiString_Assign(&");
				expr.Left.Accept(this, CiPriority.Primary);
				Write(", ");
				WriteStringStorageValue(expr.Right);
				WriteChar(')');
			}
		}
		else if (expr.Left.Type is CiDynamicPtrType dynamic) {
			if (dynamic.Class.Id == CiId.RegexClass) {
				// TODO: only if previously assigned non-null
				// Write("g_regex_unref(");
				// expr.Left.Accept(this, CiPriority.Argument);
				// WriteLine(");");
				base.WriteAssign(expr, parent);
			}
			else {
				this.SharedAssign = true;
				Write("CiShared_Assign((void **) &");
				expr.Left.Accept(this, CiPriority.Primary);
				Write(", ");
				if (expr.Right is CiSymbolReference) {
					this.SharedAddRef = true;
					Write("CiShared_AddRef(");
					expr.Right.Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
				else
					expr.Right.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
		}
		else
			base.WriteAssign(expr, parent);
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		if (def.IsAssignableStorage())
			return false;
		return (def is CiField && (def.Value != null || IsHeapAllocated(def.Type.GetStorageType()) || (def.Type is CiClassType klass && (klass.Class.Id == CiId.ListClass || klass.Class.Id == CiId.DictionaryClass || klass.Class.Id == CiId.SortedDictionaryClass))))
			|| GetThrowingMethod(def.Value) != null
			|| (def.Type.GetStorageType() is CiStorageType storage && (storage.Class.Id == CiId.LockClass || NeedsConstructor(storage.Class)))
			|| GetListDestroy(def.Type) != null
			|| base.HasInitCode(def);
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (!HasInitCode(def))
			return;
		CiType type = def.Type;
		int nesting = 0;
		while (type is CiArrayStorageType array) {
			OpenLoop("int", nesting++, array.Length);
			type = array.GetElementType();
		}
		if (type is CiStorageType lok && lok.Class.Id == CiId.LockClass) {
			Write("mtx_init(&");
			WriteArrayElement(def, nesting);
			WriteLine(", mtx_plain | mtx_recursive);");
		}
		else if (type is CiStorageType storage && NeedsConstructor(storage.Class)) {
			WriteName(storage.Class);
			Write("_Construct(&");
			WriteArrayElement(def, nesting);
			WriteLine(");");
		}
		else {
			if (def is CiField) {
				WriteArrayElement(def, nesting);
				if (nesting > 0) {
					Write(" = ");
					if (IsHeapAllocated(type))
						Write("NULL");
					else
						def.Value.Accept(this, CiPriority.Argument);
				}
				else
					WriteVarInit(def);
				WriteCharLine(';');
			}
			CiMethod throwingMethod = GetThrowingMethod(def.Value);
			if (throwingMethod != null)
				WriteForwardThrow(parent => WriteArrayElement(def, nesting), throwingMethod);
		}
		if (GetListDestroy(type) is string destroy) {
			Write("g_array_set_clear_func(");
			WriteArrayElement(def, nesting);
			Write(", ");
			Write(destroy);
			WriteLine(");");
		}
		while (--nesting >= 0)
			CloseBlock();
		base.WriteInitCode(def);
	}

	void WriteMemberAccess(CiExpr left, CiClass symbolClass)
	{
		if (left.Type is CiStorageType)
			WriteChar('.');
		else
			Write("->");
		for (CiClass klass = ((CiClassType) left.Type).Class; klass != symbolClass; klass = (CiClass) klass.Parent)
			Write("base.");
	}

	protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		WriteMemberAccess(left, (CiClass) symbol.Symbol.Parent);
	}

	protected override void WriteArrayPtr(CiExpr expr, CiPriority parent)
	{
		if (expr.Type is CiClassType list && list.Class.Id == CiId.ListClass) {
			WriteChar('(');
			WriteType(list.GetElementType(), false);
			Write(" *) ");
			expr.Accept(this, CiPriority.Primary);
			Write("->data");
		}
		else
			expr.Accept(this, parent);
	}

	void WriteTemporaryOrExpr(CiExpr expr, CiPriority parent)
	{
		int tempId = this.CurrentTemporaries.IndexOf(expr);
		if (tempId >= 0) {
			Write("citemp");
			VisitLiteralLong(tempId);
		}
		else
			expr.Accept(this, parent);
	}

	void WriteUpcast(CiClass resultClass, CiSymbol klass)
	{
		for (; klass != resultClass; klass = klass.Parent)
			Write(".base");
	}

	void WriteClassPtr(CiClass resultClass, CiExpr expr, CiPriority parent)
	{
		switch (expr.Type) {
		case CiStorageType storage when storage.Class.Id == CiId.None && !IsDictionaryClassStgIndexing(expr):
			WriteChar('&');
			WriteTemporaryOrExpr(expr, CiPriority.Primary);
			WriteUpcast(resultClass, storage.Class);
			break;
		case CiClassType ptr when ptr.Class != resultClass:
			WriteChar('&');
			expr.Accept(this, CiPriority.Primary);
			Write("->base");
			WriteUpcast(resultClass, ptr.Class.Parent);
			break;
		default:
			expr.Accept(this, parent);
			break;
		}
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		switch (type) {
		case CiDynamicPtrType dynamic when expr is CiSymbolReference && parent != CiPriority.Equality:
			this.SharedAddRef = true;
			if (dynamic.Class.Id == CiId.ArrayPtrClass)
				WriteDynamicArrayCast(dynamic.GetElementType());
			else {
				WriteChar('(');
				WriteName(dynamic.Class);
				Write(" *) ");
			}
			WriteCall("CiShared_AddRef", expr);
			break;
		case CiClassType klass when klass.Class.Id != CiId.StringClass && klass.Class.Id != CiId.ArrayPtrClass && !(klass is CiStorageType):
			if (klass.Class.Id == CiId.QueueClass && expr.Type is CiStorageType) {
				WriteChar('&');
				expr.Accept(this, CiPriority.Primary);
			}
			else
				WriteClassPtr(klass.Class, expr, parent);
			break;
		default:
			if (type.Id == CiId.StringStorageType)
				WriteStringStorageValue(expr);
			else if (expr.Type.Id == CiId.StringStorageType)
				WriteTemporaryOrExpr(expr, parent);
			else
				base.WriteCoercedInternal(type, expr, parent);
			break;
		}
	}

	protected virtual void WriteSubstringEqual(bool cast, CiExpr ptr, CiExpr offset, string literal, CiPriority parent, bool not)
	{
		if (parent > CiPriority.Equality)
			WriteChar('(');
		Include("string.h");
		Write("memcmp(");
		WriteArrayPtrAdd(ptr, offset);
		Write(", ");
		VisitLiteralString(literal);
		Write(", ");
		VisitLiteralLong(literal.Length);
		WriteChar(')');
		Write(GetEqOp(not));
		WriteChar('0');
		if (parent > CiPriority.Equality)
			WriteChar(')');
	}

	protected virtual void WriteEqualStringInternal(CiExpr left, CiExpr right, CiPriority parent, bool not)
	{
		if (parent > CiPriority.Equality)
			WriteChar('(');
		Include("string.h");
		Write("strcmp("); // TODO: WriteCall("strcmp", left, right);
		WriteTemporaryOrExpr(left, CiPriority.Argument);
		Write(", ");
		WriteTemporaryOrExpr(right, CiPriority.Argument);
		WriteChar(')');
		Write(GetEqOp(not));
		WriteChar('0');
		if (parent > CiPriority.Equality)
			WriteChar(')');
	}

	protected override void WriteEqualString(CiExpr left, CiExpr right, CiPriority parent, bool not)
	{
		if (IsStringSubstring(left, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr lengthExpr)
		 && right is CiLiteralString literal) {
			int rightLength = literal.GetAsciiLength();
			if (rightLength >= 0) {
				string rightValue = literal.Value;
				if (lengthExpr is CiLiteralLong leftLength) {
					if (leftLength.Value != rightLength)
						NotYet(left, "String comparison with unmatched length"); // TODO: evaluate compile-time
					WriteSubstringEqual(cast, ptr, offset, rightValue, parent, not);
				}
				else if (not) {
					if (parent > CiPriority.CondOr)
						WriteChar('(');
					lengthExpr.Accept(this, CiPriority.Equality);
					Write(" != ");
					VisitLiteralLong(rightLength);
					Write(" || ");
					WriteSubstringEqual(cast, ptr, offset, rightValue, CiPriority.CondOr, true);
					if (parent > CiPriority.CondOr)
						WriteChar(')');
				}
				else {
					if (parent > CiPriority.CondAnd || parent == CiPriority.CondOr)
						WriteChar('(');
					lengthExpr.Accept(this, CiPriority.Equality);
					Write(" == ");
					VisitLiteralLong(rightLength);
					Write(" && ");
					WriteSubstringEqual(cast, ptr, offset, rightValue, CiPriority.CondAnd, false);
					if (parent > CiPriority.CondAnd || parent == CiPriority.CondOr)
						WriteChar(')');
				}
				return;
			}
		}
		WriteEqualStringInternal(left, right, parent, not);
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if (expr.Left.Type is CiStringType && expr.Right.Type is CiStringType)
			WriteEqualString(expr.Left, expr.Right, parent, not);
		else
			base.WriteEqual(expr, parent, not);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		Include("string.h");
		WriteCall("(int) strlen", expr);
	}

	void WriteStringMethod(string name, CiExpr obj, List<CiExpr> args)
	{
		Include("string.h");
		Write("CiString_");
		WriteCall(name, obj, args[0]);
	}

	void WriteSizeofCompare(CiType elementType)
	{
		Write(", sizeof(");
		TypeCode typeCode = GetTypeCode(elementType, false);
		WriteTypeCode(typeCode);
		Write("), CiCompare_");
		WriteTypeCode(typeCode);
		WriteChar(')');
		this.Compares.Add(typeCode);
	}

	protected void WriteArrayFill(CiExpr obj, List<CiExpr> args)
	{
		Write("for (int _i = 0; _i < ");
		if (args.Count == 1)
			VisitLiteralLong(((CiArrayStorageType) obj.Type).Length);
		else
			args[2].Accept(this, CiPriority.Rel); // FIXME: side effect in every iteration
		WriteLine("; _i++)");
		WriteChar('\t');
		obj.Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
		WriteChar('[');
		if (args.Count > 1)
			StartAdd(args[1]); // FIXME: side effect in every iteration
		Write("_i] = ");
		args[0].Accept(this, CiPriority.Argument); // FIXME: side effect in every iteration
	}

	void WriteListAddInsert(CiExpr obj, bool insert, string function, List<CiExpr> args)
	{
		CiType elementType = ((CiClassType) obj.Type).GetElementType();
		// TODO: don't emit temporary variable if already a var/field of matching type - beware of integer promotions!
		int id = WriteCTemporary(elementType, elementType.IsFinal() ? null : args[args.Count - 1]);
		if (elementType is CiStorageType storage && NeedsConstructor(storage.Class)) {
			WriteName(storage.Class);
			Write("_Construct(&citemp");
			VisitLiteralLong(id);
			WriteLine(");");
		}
		Write(function);
		WriteChar('(');
		obj.Accept(this, CiPriority.Argument);
		if (insert) {
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
		}
		Write(", citemp");
		VisitLiteralLong(id);
		WriteChar(')');
		this.CurrentTemporaries[id] = elementType;
	}

	void WriteDictionaryLookup(CiExpr obj, string function, CiExpr key)
	{
		Write(function);
		WriteChar('(');
		obj.Accept(this, CiPriority.Argument);
		Write(", ");
		WriteGConstPointerCast(key);
		WriteChar(')');
	}

	void WriteArgsAndRightParenthesis(CiMethod method, List<CiExpr> args)
	{
		int i = 0;
		for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
			if (i > 0 || method.CallType != CiCallType.Static)
				Write(", ");
			if (i >= args.Count)
				param.Value.Accept(this, CiPriority.Argument);
			else
				WriteCoerced(param.Type, args[i], CiPriority.Argument);
			i++;
		}
		WriteChar(')');
	}

	void WriteRegexOptions(List<CiExpr> args)
	{
		if (!WriteRegexOptions(args, "", " | ", "", "G_REGEX_CASELESS", "G_REGEX_MULTILINE", "G_REGEX_DOTALL"))
			WriteChar('0');
	}

	protected void WritePrintfNotInterpolated(List<CiExpr> args, bool newLine)
	{
		Write("\"%");
		switch (args[0].Type) {
		case CiIntegerType intType:
			if (intType.Id == CiId.LongType)
				WritePrintfLongPrefix();
			WriteChar('d');
			break;
		case CiFloatingType _:
			WriteChar('g');
			break;
		default:
			WriteChar('s');
			break;
		}
		if (newLine)
			Write("\\n");
		Write("\", ");
		args[0].Accept(this, CiPriority.Argument);
		WriteChar(')');
	}

	void WriteConsoleWrite(CiExpr obj, List<CiExpr> args, bool newLine)
	{
		bool error = IsReferenceTo(obj, CiId.ConsoleError);
		Include("stdio.h");
		if (args.Count == 0)
			Write(error ? "putc('\\n', stderr)" : "putchar('\\n')");
		else if (args[0] is CiInterpolatedString interpolated) {
			Write(error ? "fprintf(stderr, " : "printf(");
			WritePrintf(interpolated, newLine);
		}
		else if (args[0].Type is CiNumericType) {
			Write(error ? "fprintf(stderr, " : "printf(");
			WritePrintfNotInterpolated(args, newLine);
		}
		else if (!newLine) {
			Write("fputs(");
			args[0].Accept(this, CiPriority.Argument);
			Write(error ? ", stderr)" : ", stdout)");
		}
		else if (error) {
			if (args[0] is CiLiteralString literal) {
				Write("fputs(");
				WriteStringLiteralWithNewLine(literal.Value);
				Write(", stderr)");
			}
			else {
				Write("fprintf(stderr, \"%s\\n\", ");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
		}
		else
			WriteCall("puts", args[0]);
	}

	protected void WriteCCall(CiExpr obj, CiMethod method, List<CiExpr> args)
	{
		CiClass klass = this.CurrentClass;
		CiClass declaringClass = (CiClass) method.Parent;
		if (IsReferenceTo(obj, CiId.BasePtr)) {
			WriteName(method);
			Write("(&self->base");
			WriteUpcast(declaringClass, klass.Parent);
		}
		else {
			CiClass definingClass = declaringClass;
			switch (method.CallType) {
			case CiCallType.Override:
				declaringClass = (CiClass) method.GetDeclaringMethod().Parent;
				goto case CiCallType.Abstract;
			case CiCallType.Abstract:
			case CiCallType.Virtual:
				if (obj != null)
					klass = obj.Type as CiClass ?? ((CiClassType) obj.Type).Class;
				CiClass ptrClass = GetVtblPtrClass(klass);
				CiClass structClass = GetVtblStructClass(definingClass);
				if (structClass != ptrClass) {
					Write("((const ");
					WriteName(structClass);
					Write("Vtbl *) ");
				}
				if (obj != null) {
					obj.Accept(this, CiPriority.Primary);
					WriteMemberAccess(obj, ptrClass);
				}
				else
					WriteSelfForField(ptrClass);
				Write("vtbl");
				if (structClass != ptrClass)
					WriteChar(')');
				Write("->");
				WriteCamelCase(method.Name);
				break;
			default:
				WriteName(method);
				break;
			}
			WriteChar('(');
			if (method.CallType != CiCallType.Static) {
				if (obj != null)
					WriteClassPtr(declaringClass, obj, CiPriority.Argument);
				else if (klass == declaringClass)
					Write("self");
				else {
					Write("&self->base");
					WriteUpcast(declaringClass, klass.Parent);
				}
			}
		}
		WriteArgsAndRightParenthesis(method, args);
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.None:
		case CiId.ClassToString:
			WriteCCall(obj, method, args);
			break;
		case CiId.StringContains:
			Include("string.h");
			if (parent > CiPriority.Equality)
				WriteChar('(');
			if (IsOneAsciiString(args[0], out char c)) {
				Write("strchr(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				VisitLiteralChar(c);
				WriteChar(')');
			}
			else
				WriteCall("strstr", obj, args[0]);
			Write(" != NULL");
			if (parent > CiPriority.Equality)
				WriteChar(')');
			break;
		case CiId.StringEndsWith:
			this.StringEndsWith = true;
			WriteStringMethod("EndsWith", obj, args);
			break;
		case CiId.StringIndexOf:
			this.StringIndexOf = true;
			WriteStringMethod("IndexOf", obj, args);
			break;
		case CiId.StringLastIndexOf:
			this.StringLastIndexOf = true;
			WriteStringMethod("LastIndexOf", obj, args);
			break;
		case CiId.StringReplace:
			Include("string.h");
			this.StringAppend = true;
			this.StringReplace = true;
			WriteCall("CiString_Replace", obj, args[0], args[1]);
			break;
		case CiId.StringStartsWith:
			if (parent > CiPriority.Equality)
				WriteChar('(');
			if (IsOneAsciiString(args[0], out char c2)) {
				obj.Accept(this, CiPriority.Primary);
				Write("[0] == ");
				VisitLiteralChar(c2);
			}
			else {
				Include("string.h");
				Write("strncmp(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				args[0].Accept(this, CiPriority.Argument);
				Write(", strlen(");
				args[0].Accept(this, CiPriority.Argument); // TODO: side effect
				Write(")) == 0");
			}
			if (parent > CiPriority.Equality)
				WriteChar(')');
			break;
		case CiId.StringSubstring:
			if (args.Count == 1) {
				if (parent > CiPriority.Add)
					WriteChar('(');
				WriteAdd(obj, args[0]);
				if (parent > CiPriority.Add)
					WriteChar(')');
			}
			else
				NotSupported(obj, "Substring");
			break;
		case CiId.ArrayBinarySearchAll:
		case CiId.ArrayBinarySearchPart:
			if (parent > CiPriority.Add)
				WriteChar('(');
			Write("(const ");
			CiType elementType2 = ((CiClassType) obj.Type).GetElementType();
			WriteType(elementType2, false);
			Write(" *) bsearch(&");
			args[0].Accept(this, CiPriority.Primary); // TODO: not lvalue, promoted
			Write(", ");
			if (args.Count == 1)
				WriteArrayPtr(obj, CiPriority.Argument);
			else
				WriteArrayPtrAdd(obj, args[1]);
			Write(", ");
			if (args.Count == 1)
				VisitLiteralLong(((CiArrayStorageType) obj.Type).Length);
			else
				args[2].Accept(this, CiPriority.Primary);
			WriteSizeofCompare(elementType2);
			Write(" - ");
			WriteArrayPtr(obj, CiPriority.Mul);
			if (parent > CiPriority.Add)
				WriteChar(')');
			break;
		case CiId.ArrayCopyTo:
		case CiId.ListCopyTo:
			Include("string.h");
			CiType elementType = ((CiClassType) obj.Type).GetElementType();
			if (IsHeapAllocated(elementType))
				NotYet(obj, "CopyTo for this type"); // TODO
			Write("memcpy(");
			WriteArrayPtrAdd(args[1], args[2]);
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			if (elementType is CiRangeType range
			 && ((range.Min >= 0 && range.Max <= byte.MaxValue)
				|| (range.Min >= sbyte.MinValue && range.Max <= sbyte.MaxValue)))
				args[3].Accept(this, CiPriority.Argument);
			else {
				args[3].Accept(this, CiPriority.Mul);
				Write(" * sizeof(");
				WriteType(elementType, false);
				WriteChar(')');
			}
			WriteChar(')');
			break;
		case CiId.ArrayFillAll:
		case CiId.ArrayFillPart:
			// TODO: IsHeapAllocated
			if (args[0] is CiLiteral literal && literal.IsDefaultValue()) {
				Include("string.h");
				Write("memset(");
				if (args.Count == 1) {
					obj.Accept(this, CiPriority.Argument);
					Write(", 0, sizeof(");
					obj.Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
				else {
					WriteArrayPtrAdd(obj, args[1]);
					Write(", 0, ");
					args[2].Accept(this, CiPriority.Mul);
					Write(" * sizeof(");
					WriteType(((CiClassType) obj.Type).GetElementType(), false);
					WriteChar(')');
				}
				WriteChar(')');
			}
			else
				WriteArrayFill(obj, args);
			break;
		case CiId.ArraySortAll:
			Write("qsort(");
			WriteArrayPtr(obj, CiPriority.Argument);
			Write(", ");
			CiArrayStorageType arrayStorage = (CiArrayStorageType) obj.Type;
			VisitLiteralLong(arrayStorage.Length);
			WriteSizeofCompare(arrayStorage.GetElementType());
			break;
		case CiId.ArraySortPart:
		case CiId.ListSortPart:
			Write("qsort(");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			args[1].Accept(this, CiPriority.Primary);
			WriteSizeofCompare(((CiClassType) obj.Type).GetElementType());
			break;
		case CiId.ListAdd:
		case CiId.StackPush:
			switch (((CiClassType) obj.Type).GetElementType()) {
			case CiArrayStorageType _:
			case CiStorageType storage when storage.Class.Id == CiId.None && !NeedsConstructor(storage.Class):
				Write("g_array_set_size(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				obj.Accept(this, CiPriority.Primary); // TODO: side effect
				Write("->len + 1)");
				break;
			default:
				WriteListAddInsert(obj, false, "g_array_append_val", args);
				break;
			}
			break;
		case CiId.ListClear:
		case CiId.StackClear:
			Write("g_array_set_size(");
			obj.Accept(this, CiPriority.Argument);
			Write(", 0)");
			break;
		case CiId.ListContains:
			Write("CiArray_Contains_");
			TypeCode typeCode = GetTypeCode(((CiClassType) obj.Type).GetElementType(), false);
			if (typeCode == TypeCode.String) {
				Include("string.h");
				Write("string((const char * const");
			}
			else {
				WriteTypeCode(typeCode);
				Write("((const ");
				WriteTypeCode(typeCode);
			}
			Write(" *) ");
			obj.Accept(this, CiPriority.Primary);
			Write("->data, ");
			obj.Accept(this, CiPriority.Primary); // TODO: side effect
			Write("->len, ");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
			this.Contains.Add(typeCode);
			break;
		case CiId.ListInsert:
			WriteListAddInsert(obj, true, "g_array_insert_val", args);
			break;
		case CiId.ListLast:
		case CiId.StackPeek:
			StartArrayIndexing(obj, ((CiClassType) obj.Type).GetElementType());
			obj.Accept(this, CiPriority.Primary); // TODO: side effect
			Write("->len - 1)");
			break;
		case CiId.ListRemoveAt:
			WriteCall("g_array_remove_index", obj, args[0]);
			break;
		case CiId.ListRemoveRange:
			WriteCall("g_array_remove_range", obj, args[0], args[1]);
			break;
		case CiId.ListSortAll:
			Write("g_array_sort(");
			obj.Accept(this, CiPriority.Argument);
			TypeCode typeCode2 = GetTypeCode(((CiClassType) obj.Type).GetElementType(), false);
			Write(", CiCompare_");
			WriteTypeCode(typeCode2);
			WriteChar(')');
			this.Compares.Add(typeCode2);
			break;
		case CiId.QueueClear:
			Write("g_queue_clear("); // TODO: g_queue_clear_full
			WriteQueueObject(obj);
			WriteChar(')');
			break;
		case CiId.QueueDequeue:
			WriteQueueGet("g_queue_pop_head", obj, parent);
			break;
		case CiId.QueueEnqueue:
			Write("g_queue_push_tail(");
			WriteQueueObject(obj);
			Write(", ");
			WriteGPointerCast(((CiClassType) obj.Type).GetElementType(), args[0]);
			WriteChar(')');
			break;
		case CiId.QueuePeek:
			WriteQueueGet("g_queue_peek_head", obj, parent);
			break;
		case CiId.StackPop:
			// FIXME: destroy
			StartArrayIndexing(obj, ((CiClassType) obj.Type).GetElementType());
			Write("--");
			obj.Accept(this, CiPriority.Primary); // TODO: side effect
			Write("->len)");
			break;
		case CiId.HashSetAdd:
			Write("g_hash_table_add(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteGPointerCast(((CiClassType) obj.Type).GetElementType(), args[0]);
			WriteChar(')');
			break;
		case CiId.HashSetClear:
			WriteCall("g_hash_table_remove_all", obj);
			break;
		case CiId.HashSetContains:
			WriteDictionaryLookup(obj, "g_hash_table_contains", args[0]);
			break;
		case CiId.HashSetRemove:
			WriteDictionaryLookup(obj, "g_hash_table_remove", args[0]);
			break;
		case CiId.DictionaryAdd:
			StartDictionaryInsert(obj, args[0]);
			CiStorageType valueType = (CiStorageType) ((CiClassType) obj.Type).GetValueType();
			switch (valueType.Class.Id) {
			case CiId.ListClass:
			case CiId.StackClass:
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
				WriteNewStorage(valueType);
				break;
			default:
				if (valueType.Class.IsPublic && valueType.Class.Constructor != null && valueType.Class.Constructor.Visibility == CiVisibility.Public) { // FIXME: construct fields if no public constructor
					WriteName(valueType.Class);
					Write("_New()");
				}
				else {
					Write("malloc(sizeof(");
					WriteType(valueType, false);
					Write("))");
				}
				break;
			}
			WriteChar(')');
			break;
		case CiId.DictionaryClear:
			WriteCall("g_hash_table_remove_all", obj);
			break;
		case CiId.DictionaryContainsKey:
			WriteDictionaryLookup(obj, "g_hash_table_contains", args[0]);
			break;
		case CiId.DictionaryRemove:
			WriteDictionaryLookup(obj, "g_hash_table_remove", args[0]);
			break;
		case CiId.SortedDictionaryClear:
			// TODO: since glib-2.70: WriteCall("g_tree_remove_all", obj);
			Write("g_tree_destroy(g_tree_ref(");
			obj.Accept(this, CiPriority.Argument);
			Write("))");
			break;
		case CiId.SortedDictionaryContainsKey:
			Write("g_tree_lookup_extended(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteGConstPointerCast(args[0]);
			Write(", NULL, NULL)");
			break;
		case CiId.SortedDictionaryRemove:
			WriteDictionaryLookup(obj, "g_tree_remove", args[0]);
			break;
		case CiId.ConsoleWrite:
			WriteConsoleWrite(obj, args, false);
			break;
		case CiId.ConsoleWriteLine:
			WriteConsoleWrite(obj, args, true);
			break;
		case CiId.UTF8GetByteCount:
			WriteStringLength(args[0]);
			break;
		case CiId.UTF8GetBytes:
			Include("string.h");
			Write("memcpy("); // NOT strcpy because without the NUL terminator
			WriteArrayPtrAdd(args[1], args[2]);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(", strlen(");
			args[0].Accept(this, CiPriority.Argument); // FIXME: side effect
			Write("))");
			break;
		case CiId.EnvironmentGetEnvironmentVariable:
			WriteCall("getenv", args[0]);
			break;
		case CiId.RegexCompile:
			WriteGlib("g_regex_new(");
			WriteTemporaryOrExpr(args[0], CiPriority.Argument);
			Write(", ");
			WriteRegexOptions(args);
			Write(", 0, NULL)");
			break;
		case CiId.RegexEscape:
			WriteGlib("g_regex_escape_string(");
			WriteTemporaryOrExpr(args[0], CiPriority.Argument);
			Write(", -1)");
			break;
		case CiId.RegexIsMatchStr:
			WriteGlib("g_regex_match_simple(");
			WriteTemporaryOrExpr(args[1], CiPriority.Argument);
			Write(", ");
			WriteTemporaryOrExpr(args[0], CiPriority.Argument);
			Write(", ");
			WriteRegexOptions(args);
			Write(", 0)");
			break;
		case CiId.RegexIsMatchRegex:
			Write("g_regex_match(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteTemporaryOrExpr(args[0], CiPriority.Argument);
			Write(", 0, NULL)");
			break;
		case CiId.MatchFindStr:
			this.MatchFind = true;
			Write("CiMatch_Find(&");
			obj.Accept(this, CiPriority.Primary);
			Write(", ");
			WriteTemporaryOrExpr(args[0], CiPriority.Argument);
			Write(", ");
			WriteTemporaryOrExpr(args[1], CiPriority.Argument);
			Write(", ");
			WriteRegexOptions(args);
			WriteChar(')');
			break;
		case CiId.MatchFindRegex:
			Write("g_regex_match(");
			args[1].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteTemporaryOrExpr(args[0], CiPriority.Argument);
			Write(", 0, &");
			obj.Accept(this, CiPriority.Primary);
			WriteChar(')');
			break;
		case CiId.MatchGetCapture:
			WriteCall("g_match_info_fetch", obj, args[0]);
			break;
		case CiId.MathMethod:
		case CiId.MathIsFinite:
		case CiId.MathIsNaN:
		case CiId.MathLog2:
			IncludeMath();
			WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathAbs:
			switch (args[0].Type.Id) {
			case CiId.LongType:
				WriteCall("llabs", args[0]);
				break;
			case CiId.FloatType:
				IncludeMath();
				WriteCall("fabsf", args[0]);
				break;
			case CiId.FloatIntType:
			case CiId.DoubleType:
				IncludeMath();
				WriteCall("fabs", args[0]);
				break;
			default:
				WriteCall("abs", args[0]);
				break;
			}
			break;
		case CiId.MathCeiling:
			IncludeMath();
			WriteCall("ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			IncludeMath();
			WriteCall("fma", args[0], args[1], args[2]);
			break;
		case CiId.MathIsInfinity:
			IncludeMath();
			WriteCall("isinf", args[0]);
			break;
		case CiId.MathMaxDouble:
			IncludeMath();
			WriteCall("fmax", args[0], args[1]);
			break;
		case CiId.MathMinDouble:
			IncludeMath();
			WriteCall("fmin", args[0], args[1]);
			break;
		case CiId.MathRound:
			IncludeMath();
			WriteCall("round", args[0]);
			break;
		case CiId.MathTruncate:
			IncludeMath();
			WriteCall("trunc", args[0]);
			break;
		default:
			NotSupported(obj, method.Name);
			break;
		}
	}

	void StartArrayIndexing(CiExpr obj, CiType elementType)
	{
		Write("g_array_index(");
		obj.Accept(this, CiPriority.Argument);
		Write(", ");
		WriteType(elementType, false);
		Write(", ");
	}

	void WriteDictionaryIndexing(string function, CiBinaryExpr expr, CiPriority parent)
	{
		CiClassType klass = (CiClassType) expr.Left.Type;
		if (klass.GetValueType() is CiIntegerType && klass.GetValueType().Id != CiId.LongType) {
			Write("GPOINTER_TO_INT(");
			WriteDictionaryLookup(expr.Left, function, expr.Right);
			WriteChar(')');
		}
		else {
			if (parent > CiPriority.Mul)
				WriteChar('(');
			if (klass.GetValueType() is CiStorageType storage && (storage.Class.Id == CiId.None || storage.Class.Id == CiId.ArrayStorageClass))
				WriteDynamicArrayCast(klass.GetValueType());
			else {
				WriteStaticCastType(klass.GetValueType());
				if (klass.GetValueType() is CiEnum) {
					Trace.Assert(parent <= CiPriority.Mul, "Should close two parens");
					Write("GPOINTER_TO_INT(");
				}
			}
			WriteDictionaryLookup(expr.Left, function, expr.Right);
			if (parent > CiPriority.Mul || klass.GetValueType() is CiEnum)
				WriteChar(')');
		}
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left.Type is CiClassType klass) {
			switch (klass.Class.Id) {
			case CiId.ListClass:
				if (klass.GetElementType() is CiArrayStorageType) {
					WriteChar('(');
					WriteDynamicArrayCast(klass.GetElementType());
					expr.Left.Accept(this, CiPriority.Primary);
					Write("->data)[");
					expr.Right.Accept(this, CiPriority.Argument);
					WriteChar(']');
				}
				else {
					StartArrayIndexing(expr.Left, klass.GetElementType());
					expr.Right.Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
				return;
			case CiId.DictionaryClass:
				WriteDictionaryIndexing("g_hash_table_lookup", expr, parent);
				return;
			case CiId.SortedDictionaryClass:
				WriteDictionaryIndexing("g_tree_lookup", expr, parent);
				return;
			default:
				break;
			}
		}
		base.WriteIndexing(expr, parent);
	}

	public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Plus:
			if (expr.Type.Id == CiId.StringStorageType)
				NotSupported(expr, "String concatenation");
			break;
		case CiToken.Equal:
		case CiToken.NotEqual:
		case CiToken.Greater:
			if (IsStringEmpty(expr, out CiExpr str)) {
				str.Accept(this, CiPriority.Primary);
				Write(expr.Op == CiToken.Equal ? "[0] == '\\0'" : "[0] != '\\0'");
				return;
			}
			break;
		case CiToken.AddAssign:
			if (expr.Left.Type.Id == CiId.StringStorageType) {
				if (expr.Right is CiInterpolatedString rightInterpolated) {
					this.StringAssign = true;
					Write("CiString_Assign(&");
					expr.Left.Accept(this, CiPriority.Primary);
					this.StringFormat = true;
					Include("stdarg.h");
					Include("stdio.h");
					Write(", CiString_Format(\"%s");
					WritePrintfFormat(rightInterpolated);
					Write("\", ");
					expr.Left.Accept(this, CiPriority.Argument); // TODO: side effect
					WriteInterpolatedStringArgs(rightInterpolated);
					WriteChar(')');
				}
				else {
					Include("string.h");
					this.StringAppend = true;
					Write("CiString_Append(&");
					expr.Left.Accept(this, CiPriority.Primary);
					Write(", ");
					expr.Right.Accept(this, CiPriority.Argument);
				}
				WriteChar(')');
				return;
			}
			break;
		case CiToken.Is:
			NotSupported(expr, "'is' operator");
			break;
		default:
			break;
		}
		base.VisitBinaryExpr(expr, parent);
	}

	protected override void WriteResource(string name, int length)
	{
		Write("CiResource_");
		foreach (char c in name)
			WriteChar(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	public override void VisitLambdaExpr(CiLambdaExpr expr)
	{
		NotSupported(expr, "Lambda expression");
	}

	static CiMethod GetThrowingMethod(CiExpr expr)
	{
		switch (expr) {
		case CiBinaryExpr binary when binary.Op == CiToken.Assign:
			return GetThrowingMethod(binary.Right);
		case CiCallExpr call:
			CiMethod method = (CiMethod) call.Method.Symbol;
			return method.Throws ? method : null;
		default:
			return null;
		}
	}

	void WriteForwardThrow(Action<CiPriority> source, CiMethod throwingMethod)
	{
		Write("if (");
		if (throwingMethod.Type is CiNumericType) {
			if (throwingMethod.Type is CiIntegerType) {
				source(CiPriority.Equality);
				Write(" == -1");
			}
			else {
				IncludeMath();
				Write("isnan(");
				source(CiPriority.Argument);
				WriteChar(')');
			}
		}
		else if (throwingMethod.Type.Id == CiId.VoidType) {
			WriteChar('!');
			source(CiPriority.Primary);
		}
		else {
			source(CiPriority.Equality);
			Write(" == NULL");
		}
		WriteChar(')');
		if (this.VarsToDestruct.Count > 0) {
			WriteChar(' ');
			OpenBlock();
			VisitThrow(null);
			CloseBlock();
		}
		else {
			WriteLine();
			this.Indent++;
			VisitThrow(null);
			this.Indent--;
		}
	}

	void WriteDestruct(CiSymbol symbol)
	{
		if (!NeedToDestruct(symbol))
			return;
		EnsureChildBlock();
		CiType type = symbol.Type;
		int nesting = 0;
		while (type is CiArrayStorageType array) {
			Write("for (int _i");
			VisitLiteralLong(nesting);
			Write(" = ");
			VisitLiteralLong(array.Length - 1);
			Write("; _i");
			VisitLiteralLong(nesting);
			Write(" >= 0; _i");
			VisitLiteralLong(nesting);
			WriteLine("--)");
			this.Indent++;
			nesting++;
			type = array.GetElementType();
		}
		bool arrayFree = false;
		switch (type) {
		case CiDynamicPtrType dynamic:
			if (dynamic.Class.Id == CiId.RegexClass)
				Write("g_regex_unref(");
			else {
				this.SharedRelease = true;
				Write("CiShared_Release(");
			}
			break;
		case CiStorageType storage:
			switch (storage.Class.Id) {
			case CiId.ListClass:
			case CiId.StackClass:
				Write("g_array_free(");
				arrayFree = true;
				break;
			case CiId.QueueClass:
				Write("g_queue_clear(&");
				break;
			case CiId.HashSetClass:
			case CiId.DictionaryClass:
				Write("g_hash_table_unref(");
				break;
			case CiId.SortedDictionaryClass:
				Write("g_tree_unref(");
				break;
			case CiId.MatchClass:
				Write("g_match_info_free(");
				break;
			case CiId.LockClass:
				Write("mtx_destroy(&");
				break;
			default:
				WriteName(storage.Class);
				Write("_Destruct(&");
				break;
			}
			break;
		default:
			Write("free(");
			break;
		}
		WriteLocalName(symbol, CiPriority.Primary);
		for (int i = 0; i < nesting; i++) {
			Write("[_i");
			VisitLiteralLong(i);
			WriteChar(']');
		}
		if (arrayFree)
			Write(", TRUE");
		WriteLine(");");
		this.Indent -= nesting;
	}

	void WriteDestructAll(CiSymbol exceptSymbol = null)
	{
		for (int i = this.VarsToDestruct.Count; --i >= 0; ) {
			CiSymbol symbol = this.VarsToDestruct[i];
			if (symbol != exceptSymbol)
				WriteDestruct(symbol);
		}
	}

	void WriteDestructLoopOrSwitch(CiCondCompletionStatement loopOrSwitch)
	{
		for (int i = this.VarsToDestruct.Count; --i >= 0; ) {
			CiVar def = this.VarsToDestruct[i];
			if (!loopOrSwitch.Encloses(def))
				break;
			WriteDestruct(def);
		}
	}

	void TrimVarsToDestruct(int i)
	{
		this.VarsToDestruct.RemoveRange(i, this.VarsToDestruct.Count - i);
	}

	protected override void CleanupBlock(CiBlock statement)
	{
		int i = this.VarsToDestruct.Count;
		for (; i > 0; i--) {
			CiVar def = this.VarsToDestruct[i - 1];
			if (def.Parent != statement) // destroy only the variables in this block
				break;
			if (statement.CompletesNormally())
				WriteDestruct(def);
		}
		TrimVarsToDestruct(i);
	}

	public override void VisitBreak(CiBreak statement)
	{
		WriteDestructLoopOrSwitch(statement.LoopOrSwitch);
		base.VisitBreak(statement);
	}

	public override void VisitContinue(CiContinue statement)
	{
		WriteDestructLoopOrSwitch(statement.Loop);
		base.VisitContinue(statement);
	}

	public override void VisitExpr(CiExpr statement)
	{
		WriteCTemporaries(statement);
		CiMethod throwingMethod = GetThrowingMethod(statement);
		if (throwingMethod != null) {
			EnsureChildBlock();
			WriteForwardThrow(parent => statement.Accept(this, parent), throwingMethod);
			CleanupTemporaries();
		}
		else if (statement is CiCallExpr && statement.Type.Id == CiId.StringStorageType) {
			Write("free(");
			statement.Accept(this, CiPriority.Argument);
			WriteLine(");");
			CleanupTemporaries();
		}
		else if (statement is CiCallExpr && statement.Type is CiDynamicPtrType) {
			this.SharedRelease = true;
			Write("CiShared_Release(");
			statement.Accept(this, CiPriority.Argument);
			WriteLine(");");
			CleanupTemporaries();
		}
		else
			base.VisitExpr(statement);
	}

	void StartForeachHashTable(CiForeach statement)
	{
		OpenBlock();
		WriteLine("GHashTableIter cidictit;");
		Write("g_hash_table_iter_init(&cidictit, ");
		statement.Collection.Accept(this, CiPriority.Argument);
		WriteLine(");");
	}

	void WriteDictIterVar(CiNamedValue iter, string value)
	{
		WriteTypeAndName(iter);
		Write(" = ");
		if (iter.Type is CiIntegerType && iter.Type.Id != CiId.LongType) {
			Write("GPOINTER_TO_INT(");
			Write(value);
			WriteChar(')');
		}
		else {
			WriteStaticCastType(iter.Type);
			Write(value);
		}
		WriteCharLine(';');
	}

	public override void VisitForeach(CiForeach statement)
	{
		string element = statement.GetVar().Name;
		switch (statement.Collection.Type) {
		case CiArrayStorageType array:
			Write("for (int ");
			WriteCamelCaseNotKeyword(element);
			Write(" = 0; ");
			WriteCamelCaseNotKeyword(element);
			Write(" < ");
			VisitLiteralLong(array.Length);
			Write("; ");
			WriteCamelCaseNotKeyword(element);
			Write("++)");
			WriteChild(statement.Body);
			break;
		case CiClassType klass:
			switch (klass.Class.Id) {
			case CiId.StringClass:
				Write("for (");
				WriteStringPtrType();
				WriteCamelCaseNotKeyword(element);
				Write(" = ");
				statement.Collection.Accept(this, CiPriority.Argument);
				Write("; *");
				WriteCamelCaseNotKeyword(element);
				Write(" != '\\0'; ");
				WriteCamelCaseNotKeyword(element);
				Write("++)");
				WriteChild(statement.Body);
				break;
			case CiId.ListClass:
				Write("for (");
				CiType elementType = klass.GetElementType();
				WriteType(elementType, false);
				Write(" const *");
				WriteCamelCaseNotKeyword(element);
				Write(" = (");
				WriteType(elementType, false);
				Write(" const *) ");
				statement.Collection.Accept(this, CiPriority.Primary);
				Write("->data, ");
				for (; elementType.IsArray(); elementType = ((CiClassType) elementType).GetElementType())
					WriteChar('*');
				if (elementType is CiClassType)
					Write("* const ");
				Write("*ciend = ");
				WriteCamelCaseNotKeyword(element);
				Write(" + ");
				statement.Collection.Accept(this, CiPriority.Primary); // TODO: side effect
				Write("->len; ");
				WriteCamelCaseNotKeyword(element);
				Write(" < ciend; ");
				WriteCamelCaseNotKeyword(element);
				Write("++)");
				WriteChild(statement.Body);
				break;
			case CiId.HashSetClass:
				StartForeachHashTable(statement);
				WriteLine("gpointer cikey;");
				Write("while (g_hash_table_iter_next(&cidictit, &cikey, NULL)) ");
				OpenBlock();
				WriteDictIterVar(statement.GetVar(), "cikey");
				FlattenBlock(statement.Body);
				CloseBlock();
				CloseBlock();
				break;
			case CiId.DictionaryClass:
				StartForeachHashTable(statement);
				WriteLine("gpointer cikey, civalue;");
				Write("while (g_hash_table_iter_next(&cidictit, &cikey, &civalue)) ");
				OpenBlock();
				WriteDictIterVar(statement.GetVar(), "cikey");
				WriteDictIterVar(statement.GetValueVar(), "civalue");
				FlattenBlock(statement.Body);
				CloseBlock();
				CloseBlock();
				break;
			case CiId.SortedDictionaryClass:
				Write("for (GTreeNode *cidictit = g_tree_node_first(");
				statement.Collection.Accept(this, CiPriority.Argument);
				Write("); cidictit != NULL; cidictit = g_tree_node_next(cidictit)) ");
				OpenBlock();
				WriteDictIterVar(statement.GetVar(), "g_tree_node_key(cidictit)");
				WriteDictIterVar(statement.GetValueVar(), "g_tree_node_value(cidictit)");
				FlattenBlock(statement.Body);
				CloseBlock();
				break;
			default:
				NotSupported(statement.Collection, klass.Class.Name);
				break;
			}
			break;
		default:
			NotSupported(statement.Collection, statement.Collection.Type.ToString());
			break;
		}
	}

	public override void VisitLock(CiLock statement)
	{
		Write("mtx_lock(&");
		statement.Lock.Accept(this, CiPriority.Primary);
		WriteLine(");");
		// TODO
		statement.Body.AcceptStatement(this);
		Write("mtx_unlock(&");
		statement.Lock.Accept(this, CiPriority.Primary);
		WriteLine(");");
	}

	public override void VisitReturn(CiReturn statement)
	{
		if (statement.Value == null) {
			WriteDestructAll();
			WriteLine(this.CurrentMethod.Throws ? "return true;" : "return;");
		}
		else if (statement.Value is CiLiteral || (this.VarsToDestruct.Count == 0 && !HasTemporariesToDestruct(statement.Value))) {
			WriteDestructAll();
			WriteCTemporaries(statement.Value);
			base.VisitReturn(statement);
		}
		else {
			if (statement.Value is CiSymbolReference symbol) {
				if (this.VarsToDestruct.Contains(symbol.Symbol)) {
					// Optimization: avoid copy
					WriteDestructAll(symbol.Symbol);
					Write("return ");
					if (this.CurrentMethod.Type is CiClassType resultPtr)
						WriteClassPtr(resultPtr.Class, symbol, CiPriority.Argument); // upcast, but don't AddRef
					else
						symbol.Accept(this, CiPriority.Argument);
					WriteCharLine(';');
					return;
				}
				if (symbol.Left == null) {
					// Local variable value doesn't depend on destructed variables
					WriteDestructAll();
					base.VisitReturn(statement);
					return;
				}
			}
			WriteCTemporaries(statement.Value);
			EnsureChildBlock();
			WriteDefinition(this.CurrentMethod.Type, () => Write("returnValue"), true, true);
			Write(" = ");
			WriteCoerced(this.CurrentMethod.Type, statement.Value, CiPriority.Argument);
			WriteCharLine(';');
			CleanupTemporaries();
			WriteDestructAll();
			WriteLine("return returnValue;");
		}
	}

	protected override void WriteSwitchCaseBody(List<CiStatement> statements)
	{
		if (statements[0] is CiVar
		 || (statements[0] is CiConst konst && konst.Type is CiArrayStorageType))
			WriteCharLine(';');
		int varsToDestructCount = this.VarsToDestruct.Count;
		WriteStatements(statements);
		TrimVarsToDestruct(varsToDestructCount);
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		if (statement.IsTypeMatching())
			NotSupported(statement, "Type-matching 'switch'");
		else
			base.VisitSwitch(statement);
	}

	void WriteThrowReturnValue()
	{
		if (this.CurrentMethod.Type is CiNumericType) {
			if (this.CurrentMethod.Type is CiIntegerType)
				Write("-1");
			else {
				IncludeMath();
				Write("NAN");
			}
		}
		else if (this.CurrentMethod.Type.Id == CiId.VoidType)
			Write("false");
		else
			Write("NULL");
	}

	public override void VisitThrow(CiThrow statement)
	{
		WriteDestructAll();
		Write("return ");
		WriteThrowReturnValue();
		WriteCharLine(';');
	}

	bool TryWriteCallAndReturn(List<CiStatement> statements, int lastCallIndex, CiExpr returnValue)
	{
		if (this.VarsToDestruct.Count > 0)
			return false;
		for (int i = 0; i < lastCallIndex; i++) {
			if (statements[i] is CiVar def && NeedToDestruct(def))
				return false;
		}
		CiExpr call = statements[lastCallIndex] as CiExpr;
		CiMethod throwingMethod = GetThrowingMethod(call);
		if (throwingMethod == null)
			return false;

		WriteFirstStatements(statements, lastCallIndex);
		Write("return ");
		if (throwingMethod.Type is CiNumericType) {
			if (throwingMethod.Type is CiIntegerType) {
				call.Accept(this, CiPriority.Equality);
				Write(" != -1");
			}
			else {
				IncludeMath();
				Write("!isnan(");
				call.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
		}
		else if (throwingMethod.Type.Id == CiId.VoidType)
			call.Accept(this, CiPriority.Select);
		else {
			call.Accept(this, CiPriority.Equality);
			Write(" != NULL");
		}
		if (returnValue != null) {
			Write(" ? ");
			returnValue.Accept(this, CiPriority.Select);
			Write(" : ");
			WriteThrowReturnValue();
		}
		WriteCharLine(';');
		return true;
	}

	protected override void WriteStatements(List<CiStatement> statements)
	{
		int i = statements.Count - 2;
		if (i >= 0 && statements[i + 1] is CiReturn ret && TryWriteCallAndReturn(statements, i, ret.Value))
			return;
		base.WriteStatements(statements);
	}

	protected override void WriteEnum(CiEnum enu)
	{
		WriteLine();
		WriteDoc(enu.Documentation);
		Write("typedef enum ");
		OpenBlock();
		enu.AcceptValues(this);
		WriteLine();
		this.Indent--;
		Write("} ");
		WriteName(enu);
		WriteCharLine(';');
	}

	void WriteTypedef(CiClass klass)
	{
		if (klass.CallType == CiCallType.Static)
			return;
		Write("typedef struct ");
		WriteName(klass);
		WriteChar(' ');
		WriteName(klass);
		WriteCharLine(';');
	}

	protected void WriteTypedefs(CiProgram program, bool pub)
	{
		for (CiSymbol type = program.First; type != null; type = type.Next) {
			if (((CiContainerType) type).IsPublic == pub) {
				if (type is CiClass klass)
					WriteTypedef(klass);
				else
					WriteEnum((CiEnum) type);
			}
		}
	}

	void WriteInstanceParameters(CiMethod method)
	{
		WriteChar('(');
		if (!method.IsMutator)
			Write("const ");
		WriteName(method.Parent);
		Write(" *self");
		WriteParameters(method, false, false);
	}

	void WriteSignature(CiMethod method)
	{
		CiClass klass = (CiClass) method.Parent;
		if (!klass.IsPublic || method.Visibility != CiVisibility.Public)
			Write("static ");
		WriteSignature(method, () => {
			WriteName(klass);
			WriteChar('_');
			Write(method.Name);
			if (method.CallType != CiCallType.Static)
				WriteInstanceParameters(method);
			else if (method.Parameters.Count() == 0)
				Write("(void)");
			else
				WriteParameters(method, false);
		});
	}

	static CiClass GetVtblStructClass(CiClass klass)
	{
		while (!klass.AddsVirtualMethods())
			klass = (CiClass) klass.Parent;
		return klass;
	}

	static CiClass GetVtblPtrClass(CiClass klass)
	{
		for (CiClass result = null;;) {
			if (klass.AddsVirtualMethods())
				result = klass;
			if (!(klass.Parent is CiClass baseClass))
				return result;
			klass = baseClass;
		}
	}

	void WriteVtblFields(CiClass klass)
	{
		if (klass.Parent is CiClass baseClass)
			WriteVtblFields(baseClass);
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMethod method && method.IsAbstractOrVirtual()) {
				WriteSignature(method, () => {
					Write("(*");
					WriteCamelCase(method.Name);
					WriteChar(')');
					WriteInstanceParameters(method);
				});
				WriteCharLine(';');
			}
		}
	}

	void WriteVtblStruct(CiClass klass)
	{
		Write("typedef struct ");
		OpenBlock();
		WriteVtblFields(klass);
		this.Indent--;
		Write("} ");
		WriteName(klass);
		WriteLine("Vtbl;");
	}

	protected virtual string GetConst(CiArrayStorageType array) => "const ";

	protected override void WriteConst(CiConst konst)
	{
		if (konst.Type is CiArrayStorageType array) {
			Write("static ");
			Write(GetConst(array));
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Argument);
			WriteCharLine(';');
		}
		else if (konst.Visibility == CiVisibility.Public) {
			Write("#define ");
			WriteName(konst);
			WriteChar(' ');
			konst.Value.Accept(this, CiPriority.Argument);
			WriteLine();
		}
	}

	protected override void WriteField(CiField field) => throw new NotImplementedException();

	static bool HasVtblValue(CiClass klass)
	{
		if (klass.CallType == CiCallType.Static || klass.CallType == CiCallType.Abstract)
			return false;
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMethod method) {
				switch (method.CallType) {
				case CiCallType.Virtual:
				case CiCallType.Override:
				case CiCallType.Sealed:
					return true;
				default:
					break;
				}
			}
		}
		return false;
	}

	protected override bool NeedsConstructor(CiClass klass)
	{
		if (klass.Id == CiId.MatchClass)
			return false;
		return base.NeedsConstructor(klass)
			|| HasVtblValue(klass)
			|| (klass.Parent is CiClass baseClass && NeedsConstructor(baseClass));
	}

	static bool NeedsDestructor(CiClass klass)
	{
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiField field && NeedToDestruct(field))
				return true;
		}
		return klass.Parent is CiClass baseClass && NeedsDestructor(baseClass);
	}

	void WriteXstructorSignature(string name, CiClass klass)
	{
		Write("static void ");
		WriteName(klass);
		WriteChar('_');
		Write(name);
		WriteChar('(');
		WriteName(klass);
		Write(" *self)");
	}

	protected void WriteSignatures(CiClass klass, bool pub)
	{
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			switch (symbol) {
			case CiConst konst when (konst.Visibility == CiVisibility.Public) == pub:
				if (pub) {
					WriteLine();
					WriteDoc(konst.Documentation);
				}
				WriteConst(konst);
				break;
			case CiMethod method when method.IsLive && (method.Visibility == CiVisibility.Public) == pub && method.CallType != CiCallType.Abstract:
				WriteLine();
				WriteMethodDoc(method);
				WriteSignature(method);
				WriteCharLine(';');
				break;
			default:
				break;
			}
		}
	}

	protected override void WriteClass(CiClass klass)
	{
		if (klass.CallType != CiCallType.Static) {
			WriteLine();
			if (klass.AddsVirtualMethods())
				WriteVtblStruct(klass);
			WriteDoc(klass.Documentation);
			Write("struct ");
			WriteName(klass);
			WriteChar(' ');
			OpenBlock();
			if (GetVtblPtrClass(klass) == klass) {
				Write("const ");
				WriteName(klass);
				WriteLine("Vtbl *vtbl;");
			}
			if (klass.Parent is CiClass) {
				WriteName(klass.Parent);
				WriteLine(" base;");
			}
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiField field) {
					WriteDoc(field.Documentation);
					WriteTypeAndName(field);
					WriteCharLine(';');
				}
			}
			this.Indent--;
			WriteLine("};");
			if (NeedsConstructor(klass)) {
				WriteXstructorSignature("Construct", klass);
				WriteCharLine(';');
			}
			if (NeedsDestructor(klass)) {
				WriteXstructorSignature("Destruct", klass);
				WriteCharLine(';');
			}
		}
		WriteSignatures(klass, false);
	}

	void WriteVtbl(CiClass definingClass, CiClass declaringClass)
	{
		if (declaringClass.Parent is CiClass baseClass)
			WriteVtbl(definingClass, baseClass);
		for (CiSymbol symbol = declaringClass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMethod declaredMethod && declaredMethod.IsAbstractOrVirtual()) {
				CiSymbol definedMethod = definingClass.TryLookup(declaredMethod.Name);
				if (declaredMethod != definedMethod) {
					WriteChar('(');
					WriteSignature(declaredMethod, () => {
						Write("(*)");
						WriteInstanceParameters(declaredMethod);
					});
					Write(") ");
				}
				WriteName(definedMethod);
				WriteCharLine(',');
			}
		}
	}

	protected void WriteConstructor(CiClass klass)
	{
		if (!NeedsConstructor(klass))
			return;
		this.SwitchesWithGoto.Clear();
		WriteLine();
		WriteXstructorSignature("Construct", klass);
		WriteLine();
		OpenBlock();
		if (klass.Parent is CiClass baseClass && NeedsConstructor(baseClass)) {
			WriteName(baseClass);
			WriteLine("_Construct(&self->base);");
		}
		if (HasVtblValue(klass)) {
			CiClass structClass = GetVtblStructClass(klass);
			Write("static const ");
			WriteName(structClass);
			Write("Vtbl vtbl = ");
			OpenBlock();
			WriteVtbl(klass, structClass);
			this.Indent--;
			WriteLine("};");
			CiClass ptrClass = GetVtblPtrClass(klass);
			WriteSelfForField(ptrClass);
			Write("vtbl = ");
			if (ptrClass != structClass) {
				Write("(const ");
				WriteName(ptrClass);
				Write("Vtbl *) ");
			}
			WriteLine("&vtbl;");
		}
		WriteConstructorBody(klass);
		CloseBlock();
	}

	void WriteDestructFields(CiSymbol symbol)
	{
		if (symbol != null) {
			WriteDestructFields(symbol.Next);
			if (symbol is CiField field)
				WriteDestruct(field);
		}
	}

	protected void WriteDestructor(CiClass klass)
	{
		if (!NeedsDestructor(klass))
			return;
		WriteLine();
		WriteXstructorSignature("Destruct", klass);
		WriteLine();
		OpenBlock();
		WriteDestructFields(klass.First);
		if (klass.Parent is CiClass baseClass && NeedsDestructor(baseClass)) {
			WriteName(baseClass);
			WriteLine("_Destruct(&self->base);");
		}
		CloseBlock();
	}

	void WriteNewDelete(CiClass klass, bool define)
	{
		if (!klass.IsPublic || klass.Constructor == null || klass.Constructor.Visibility != CiVisibility.Public)
			return;

		WriteLine();
		WriteName(klass);
		Write(" *");
		WriteName(klass);
		Write("_New(void)");
		if (define) {
			WriteLine();
			OpenBlock();
			WriteName(klass);
			Write(" *self = (");
			WriteName(klass);
			Write(" *) malloc(sizeof(");
			WriteName(klass);
			WriteLine("));");
			if (NeedsConstructor(klass)) {
				WriteLine("if (self != NULL)");
				this.Indent++;
				WriteName(klass);
				WriteLine("_Construct(self);");
				this.Indent--;
			}
			WriteLine("return self;");
			CloseBlock();
			WriteLine();
		}
		else
			WriteCharLine(';');

		Write("void ");
		WriteName(klass);
		Write("_Delete(");
		WriteName(klass);
		Write(" *self)");
		if (define) {
			WriteLine();
			OpenBlock();
			if (NeedsDestructor(klass)) {
				WriteLine("if (self == NULL)");
				this.Indent++;
				WriteLine("return;");
				this.Indent--;
				WriteName(klass);
				WriteLine("_Destruct(self);");
			}
			WriteLine("free(self);");
			CloseBlock();
		}
		else
			WriteCharLine(';');
	}

	protected override void WriteMethod(CiMethod method)
	{
		if (!method.IsLive || method.CallType == CiCallType.Abstract)
			return;
		this.SwitchesWithGoto.Clear();
		WriteLine();
		WriteSignature(method);
		for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
			if (NeedToDestruct(param))
				this.VarsToDestruct.Add(param);
		}
		WriteLine();
		this.CurrentMethod = method;
		OpenBlock();
		if (method.Body is CiBlock block) {
			List<CiStatement> statements = block.Statements;
			if (!block.CompletesNormally())
				WriteStatements(statements);
			else if (method.Throws && method.Type.Id == CiId.VoidType) {
				if (statements.Count == 0 || !TryWriteCallAndReturn(statements, statements.Count - 1, null)) {
					WriteStatements(statements);
					WriteDestructAll();
					WriteLine("return true;");
				}
			}
			else {
				WriteStatements(statements);
				WriteDestructAll();
			}
		}
		else
			method.Body.AcceptStatement(this);
		this.VarsToDestruct.Clear();
		CloseBlock();
		this.CurrentMethod = null;
	}

	void WriteLibrary()
	{
		if (this.StringAssign) {
			WriteLine();
			WriteLine("static void CiString_Assign(char **str, char *value)");
			OpenBlock();
			WriteLine("free(*str);");
			WriteLine("*str = value;");
			CloseBlock();
		}
		if (this.StringSubstring) {
			WriteLine();
			WriteLine("static char *CiString_Substring(const char *str, int len)");
			OpenBlock();
			WriteLine("char *p = malloc(len + 1);");
			WriteLine("memcpy(p, str, len);");
			WriteLine("p[len] = '\\0';");
			WriteLine("return p;");
			CloseBlock();
		}
		if (this.StringAppend) {
			WriteLine();
			WriteLine("static void CiString_AppendSubstring(char **str, const char *suffix, size_t suffixLen)");
			OpenBlock();
			WriteLine("if (suffixLen == 0)");
			WriteLine("\treturn;");
			WriteLine("size_t prefixLen = *str == NULL ? 0 : strlen(*str);");
			WriteLine("*str = realloc(*str, prefixLen + suffixLen + 1);");
			WriteLine("memcpy(*str + prefixLen, suffix, suffixLen);");
			WriteLine("(*str)[prefixLen + suffixLen] = '\\0';");
			CloseBlock();
			WriteLine();
			WriteLine("static void CiString_Append(char **str, const char *suffix)");
			OpenBlock();
			WriteLine("CiString_AppendSubstring(str, suffix, strlen(suffix));");
			CloseBlock();
		}
		if (this.StringIndexOf) {
			WriteLine();
			WriteLine("static int CiString_IndexOf(const char *str, const char *needle)");
			OpenBlock();
			WriteLine("const char *p = strstr(str, needle);");
			WriteLine("return p == NULL ? -1 : (int) (p - str);");
			CloseBlock();
		}
		if (this.StringLastIndexOf) {
			WriteLine();
			WriteLine("static int CiString_LastIndexOf(const char *str, const char *needle)");
			OpenBlock();
			WriteLine("if (needle[0] == '\\0')");
			WriteLine("\treturn (int) strlen(str);");
			WriteLine("int result = -1;");
			WriteLine("const char *p = strstr(str, needle);");
			Write("while (p != NULL) ");
			OpenBlock();
			WriteLine("result = (int) (p - str);");
			WriteLine("p = strstr(p + 1, needle);");
			CloseBlock();
			WriteLine("return result;");
			CloseBlock();
		}
		if (this.StringEndsWith) {
			WriteLine();
			WriteLine("static bool CiString_EndsWith(const char *str, const char *suffix)");
			OpenBlock();
			WriteLine("size_t strLen = strlen(str);");
			WriteLine("size_t suffixLen = strlen(suffix);");
			WriteLine("return strLen >= suffixLen && memcmp(str + strLen - suffixLen, suffix, suffixLen) == 0;");
			CloseBlock();
		}
		if (this.StringReplace) {
			WriteLine();
			WriteLine("static char *CiString_Replace(const char *s, const char *oldValue, const char *newValue)");
			OpenBlock();
			Write("for (char *result = NULL;;) ");
			OpenBlock();
			WriteLine("const char *p = strstr(s, oldValue);");
			WriteLine("if (p == NULL) {");
			WriteLine("\tCiString_Append(&result, s);");
			WriteLine("\treturn result == NULL ? strdup(\"\") : result;");
			WriteCharLine('}');
			WriteLine("CiString_AppendSubstring(&result, s, p - s);");
			WriteLine("CiString_Append(&result, newValue);");
			WriteLine("s = p + strlen(oldValue);");
			CloseBlock();
			CloseBlock();
		}
		if (this.StringFormat) {
			WriteLine();
			WriteLine("static char *CiString_Format(const char *format, ...)");
			OpenBlock();
			WriteLine("va_list args1;");
			WriteLine("va_start(args1, format);");
			WriteLine("va_list args2;");
			WriteLine("va_copy(args2, args1);");
			WriteLine("size_t len = vsnprintf(NULL, 0, format, args1) + 1;");
			WriteLine("va_end(args1);");
			WriteLine("char *str = malloc(len);");
			WriteLine("vsnprintf(str, len, format, args2);");
			WriteLine("va_end(args2);");
			WriteLine("return str;");
			CloseBlock();
		}
		if (this.MatchFind) {
			WriteLine();
			WriteLine("static bool CiMatch_Find(GMatchInfo **match_info, const char *input, const char *pattern, GRegexCompileFlags options)");
			OpenBlock();
			WriteLine("GRegex *regex = g_regex_new(pattern, options, 0, NULL);");
			WriteLine("bool result = g_regex_match(regex, input, 0, match_info);");
			WriteLine("g_regex_unref(regex);");
			WriteLine("return result;");
			CloseBlock();
		}
		if (this.MatchPos) {
			WriteLine();
			WriteLine("static int CiMatch_GetPos(const GMatchInfo *match_info, int which)");
			OpenBlock();
			WriteLine("int start;");
			WriteLine("int end;");
			WriteLine("g_match_info_fetch_pos(match_info, 0, &start, &end);");
			WriteLine("switch (which) {");
			WriteLine("case 0:");
			WriteLine("\treturn start;");
			WriteLine("case 1:");
			WriteLine("\treturn end;");
			WriteLine("default:");
			WriteLine("\treturn end - start;");
			WriteCharLine('}');
			CloseBlock();
		}
		if (this.PtrConstruct) {
			WriteLine();
			WriteLine("static void CiPtr_Construct(void **ptr)");
			OpenBlock();
			WriteLine("*ptr = NULL;");
			CloseBlock();
		}
		if (this.SharedMake || this.SharedAddRef || this.SharedRelease) {
			WriteLine();
			WriteLine("typedef void (*CiMethodPtr)(void *);");
			WriteLine("typedef struct {");
			this.Indent++;
			WriteLine("size_t count;");
			WriteLine("size_t unitSize;");
			WriteLine("size_t refCount;");
			WriteLine("CiMethodPtr destructor;");
			this.Indent--;
			WriteLine("} CiShared;");
		}
		if (this.SharedMake) {
			WriteLine();
			WriteLine("static void *CiShared_Make(size_t count, size_t unitSize, CiMethodPtr constructor, CiMethodPtr destructor)");
			OpenBlock();
			WriteLine("CiShared *self = (CiShared *) malloc(sizeof(CiShared) + count * unitSize);");
			WriteLine("self->count = count;");
			WriteLine("self->unitSize = unitSize;");
			WriteLine("self->refCount = 1;");
			WriteLine("self->destructor = destructor;");
			Write("if (constructor != NULL) ");
			OpenBlock();
			WriteLine("for (size_t i = 0; i < count; i++)");
			WriteLine("\tconstructor((char *) (self + 1) + i * unitSize);");
			CloseBlock();
			WriteLine("return self + 1;");
			CloseBlock();
		}
		if (this.SharedAddRef) {
			WriteLine();
			WriteLine("static void *CiShared_AddRef(void *ptr)");
			OpenBlock();
			WriteLine("if (ptr != NULL)");
			WriteLine("\t((CiShared *) ptr)[-1].refCount++;");
			WriteLine("return ptr;");
			CloseBlock();
		}
		if (this.SharedRelease || this.SharedAssign) {
			WriteLine();
			WriteLine("static void CiShared_Release(void *ptr)");
			OpenBlock();
			WriteLine("if (ptr == NULL)");
			WriteLine("\treturn;");
			WriteLine("CiShared *self = (CiShared *) ptr - 1;");
			WriteLine("if (--self->refCount != 0)");
			WriteLine("\treturn;");
			Write("if (self->destructor != NULL) ");
			OpenBlock();
			WriteLine("for (size_t i = self->count; i > 0;)");
			WriteLine("\tself->destructor((char *) ptr + --i * self->unitSize);");
			CloseBlock();
			WriteLine("free(self);");
			CloseBlock();
		}
		if (this.SharedAssign) {
			WriteLine();
			WriteLine("static void CiShared_Assign(void **ptr, void *value)");
			OpenBlock();
			WriteLine("CiShared_Release(*ptr);");
			WriteLine("*ptr = value;");
			CloseBlock();
		}
		foreach (KeyValuePair<string, string> nameContent in this.ListFrees) {
			WriteLine();
			Write("static void CiList_Free");
			Write(nameContent.Key);
			WriteLine("(void *ptr)");
			OpenBlock();
			Write(nameContent.Value);
			WriteCharLine(';');
			CloseBlock();
		}
		if (this.TreeCompareInteger) {
			WriteLine();
			Write("static int CiTree_CompareInteger(gconstpointer pa, gconstpointer pb, gpointer user_data)");
			OpenBlock();
			WriteLine("gintptr a = (gintptr) pa;");
			WriteLine("gintptr b = (gintptr) pb;");
			WriteLine("return (a > b) - (a < b);");
			CloseBlock();
		}
		if (this.TreeCompareString) {
			WriteLine();
			Write("static int CiTree_CompareString(gconstpointer a, gconstpointer b, gpointer user_data)");
			OpenBlock();
			WriteLine("return strcmp((const char *) a, (const char *) b);");
			CloseBlock();
		}
		foreach (TypeCode typeCode in this.Compares) {
			WriteLine();
			Write("static int CiCompare_");
			WriteTypeCode(typeCode);
			WriteLine("(const void *pa, const void *pb)");
			OpenBlock();
			WriteTypeCode(typeCode);
			Write(" a = *(const ");
			WriteTypeCode(typeCode);
			WriteLine(" *) pa;");
			WriteTypeCode(typeCode);
			Write(" b = *(const ");
			WriteTypeCode(typeCode);
			WriteLine(" *) pb;");
			switch (typeCode) {
			case TypeCode.Byte:
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
				// subtraction can't overflow int
				WriteLine("return a - b;");
				break;
			default:
				WriteLine("return (a > b) - (a < b);");
				break;
			}
			CloseBlock();
		}
		foreach (TypeCode typeCode in this.Contains) {
			WriteLine();
			Write("static bool CiArray_Contains_");
			if (typeCode == TypeCode.String)
				Write("string(const char * const *a, size_t len, const char *");
			else {
				WriteTypeCode(typeCode);
				Write("(const ");
				WriteTypeCode(typeCode);
				Write(" *a, size_t len, ");
				WriteTypeCode(typeCode);
			}
			WriteLine(" value)");
			OpenBlock();
			WriteLine("for (size_t i = 0; i < len; i++)");
			if (typeCode == TypeCode.String)
				WriteLine("\tif (strcmp(a[i], value) == 0)");
			else
				WriteLine("\tif (a[i] == value)");
			WriteLine("\t\treturn true;");
			WriteLine("return false;");
			CloseBlock();
		}
	}

	protected void WriteResources(Dictionary<string, byte[]> resources)
	{
		if (resources.Count == 0)
			return;
		WriteLine();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			Write("static const ");
			WriteTypeCode(TypeCode.Byte);
			WriteChar(' ');
			WriteResource(name, -1);
			WriteChar('[');
			VisitLiteralLong(resources[name].Length);
			WriteLine("] = {");
			WriteChar('\t');
			WriteBytes(resources[name]);
			WriteLine(" };");
		}
	}

	public override void WriteProgram(CiProgram program)
	{
		this.WrittenClasses.Clear();
		string headerFile = Path.ChangeExtension(this.OutputFile, "h");
		SortedSet<string> headerIncludes = new SortedSet<string>();
		this.Includes = headerIncludes;
		OpenStringWriter();
		foreach (CiClass klass in program.Classes) {
			WriteNewDelete(klass, false);
			WriteSignatures(klass, true);
		}

		CreateFile(headerFile);
		WriteLine("#pragma once");
		WriteIncludes();
		WriteLine("#ifdef __cplusplus");
		WriteLine("extern \"C\" {");
		WriteLine("#endif");
		WriteTypedefs(program, true);
		CloseStringWriter();
		WriteLine();
		WriteLine("#ifdef __cplusplus");
		WriteCharLine('}');
		WriteLine("#endif");
		CloseFile();

		this.Includes = new SortedSet<string>();
		this.StringAssign = false;
		this.StringSubstring = false;
		this.StringAppend = false;
		this.StringIndexOf = false;
		this.StringLastIndexOf = false;
		this.StringEndsWith = false;
		this.StringReplace = false;
		this.StringFormat = false;
		this.MatchFind = false;
		this.MatchPos = false;
		this.PtrConstruct = false;
		this.SharedMake = false;
		this.SharedAddRef = false;
		this.SharedRelease = false;
		this.SharedAssign = false;
		this.ListFrees.Clear();
		this.TreeCompareInteger = false;
		this.TreeCompareString = false;
		this.Compares.Clear();
		this.Contains.Clear();
		OpenStringWriter();
		foreach (CiClass klass in program.Classes)
			WriteClass(klass, program);
		WriteResources(program.Resources);
		foreach (CiClass klass in program.Classes) {
			this.CurrentClass = klass;
			WriteConstructor(klass);
			WriteDestructor(klass);
			WriteNewDelete(klass, true);
			WriteMethods(klass);
		}

		CreateFile(this.OutputFile);
		WriteTopLevelNatives(program);
		this.Includes.ExceptWith(headerIncludes);
		this.Includes.Add("stdlib.h");
		WriteIncludes();
		Write("#include \"");
		Write(Path.GetFileName(headerFile));
		WriteLine("\"");
		WriteLibrary();
		WriteTypedefs(program, false);
		CloseStringWriter();
		CloseFile();
	}
}

}
