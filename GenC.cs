// GenC.cs - C code generator
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
	readonly List<CiExpr> CurrentTemporaries = new List<CiExpr>(); // CiExpr or CiType
	readonly List<CiVar> VarsToDestruct = new List<CiVar>();
	protected CiClass CurrentClass;

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

	protected override void WritePrintfWidth(CiInterpolatedPart part)
	{
		base.WritePrintfWidth(part);
		if (IsStringSubstring(part.Argument, out bool _, out CiExpr _, out CiExpr _, out CiExpr _)) {
			Trace.Assert(part.Precision < 0);
			Write(".*");
		}
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
		else
			base.WriteInterpolatedStringArg(expr);
	}

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		Include("stdarg.h");
		Include("stdio.h");
		this.StringFormat = true;
		Write("CiString_Format(");
		WritePrintf(expr, false);
		return expr;
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
			Write('_');
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
			Write('_');
			Write(symbol.Name);
			break;
		case CiConst _:
			if (symbol.Parent is CiContainerType) {
				Write(this.Namespace);
				Write(symbol.Parent.Name);
				Write('_');
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
			switch (forEach.Collection.Type) {
			case CiArrayStorageType array:
				if (array.ElementType is CiClass klass) {
					if (parent > CiPriority.Add)
						Write('(');
					forEach.Collection.Accept(this, CiPriority.Add);
					Write(" + ");
					WriteCamelCaseNotKeyword(symbol.Name);
					if (parent > CiPriority.Add)
						Write(')');
				}
				else {
					forEach.Collection.Accept(this, CiPriority.Primary);
					Write('[');
					WriteCamelCaseNotKeyword(symbol.Name);
					Write(']');
				}
				return;
			case CiClassType klass2 when klass2.Class == CiSystem.ListClass:
				if (parent == CiPriority.Primary)
					Write('(');
				Write('*');
				WriteCamelCaseNotKeyword(symbol.Name);
				if (parent == CiPriority.Primary)
					Write(')');
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
		Write(')');
	}

	static bool IsDictionaryClassStgIndexing(CiExpr expr)
	{
		return expr is CiBinaryExpr indexing
			&& indexing.Op == CiToken.LeftBracket
			&& indexing.Left.Type is CiClassType dict
			&& dict.Class.TypeParameterCount == 2
			&& dict.ValueType is CiClass;
	}

	public override CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left == null || expr.Symbol is CiConst)
			WriteLocalName(expr.Symbol, parent);
		else if (expr.Symbol == CiSystem.CollectionCount) {
			CiClassType klass = (CiClassType) expr.Left.Type;
			if (klass.Class == CiSystem.ListClass || klass.Class == CiSystem.StackClass) {
				expr.Left.Accept(this, CiPriority.Primary);
				Write("->len");
			}
			else if (klass.Class == CiSystem.QueueClass) {
				expr.Left.Accept(this, CiPriority.Primary);
				if (expr.Left.Type is CiStorageType)
					Write('.');
				else
					Write("->");
				Write("length");
			}
			else if (klass.Class == CiSystem.HashSetClass || klass.Class == CiSystem.DictionaryClass)
				WriteCall("g_hash_table_size", expr.Left);
			else if (klass.Class == CiSystem.SortedDictionaryClass)
				WriteCall("g_tree_nnodes", expr.Left);
			else
				throw new NotImplementedException(klass.ToString());
		}
		else if (expr.Symbol == CiSystem.MatchStart)
			WriteMatchProperty(expr, 0);
		else if (expr.Symbol == CiSystem.MatchEnd)
			WriteMatchProperty(expr, 1);
		else if (expr.Symbol == CiSystem.MatchLength)
			WriteMatchProperty(expr, 2);
		else if (expr.Symbol == CiSystem.MatchValue) {
			Write("g_match_info_fetch(");
			expr.Left.Accept(this, CiPriority.Argument);
			Write(", 0)");
		}
		else if (IsDictionaryClassStgIndexing(expr.Left)) {
			expr.Left.Accept(this, CiPriority.Primary);
			Write("->");
			WriteName(expr.Symbol);
		}
		else
			return base.VisitSymbolReference(expr, parent);
		return expr;
	}

	void WriteGlib(string s)
	{
		Include("glib.h");
		Write(s);
	}

	protected virtual void WriteStringPtrType() => Write("const char *");

	void WriteArrayPrefix(CiType type)
	{
		if (type is CiClassType array && array.IsArray) {
			WriteArrayPrefix(array.ElementType);
			if (!(type is CiArrayStorageType)) {
				if (array.ElementType is CiArrayStorageType)
					Write('(');
				if (!(type is CiReadWriteClassType))
					Write("const ");
				Write('*');
			}
		}
	}

	void WriteDefinition(CiType type, Action symbol, bool promote, bool space)
	{
		CiType baseType = type.BaseType;
		switch (baseType) {
		case CiIntegerType integer:
			Write(GetIntegerTypeCode(integer, promote && type == baseType));
			if (space)
				Write(' ');
			break;
		case CiStringPtrType _:
			WriteStringPtrType();
			break;
		case CiStringStorageType _:
			Write("char *");
			break;
		case CiClassType klass:
			if (klass.Class.TypeParameterCount == 0) {
				if (!(klass is CiReadWriteClassType))
					Write("const ");
				if (klass.Class == CiSystem.RegexClass)
					WriteGlib("GRegex");
				else if (klass.Class == CiSystem.MatchClass)
					WriteGlib("GMatchInfo");
				else
					WriteName(klass.Class);
				Write(" *");
			}
			else if (klass.Class == CiSystem.ListClass || klass.Class == CiSystem.StackClass)
				WriteGlib("GArray *");
			else if (klass.Class == CiSystem.QueueClass) {
				WriteGlib("GQueue ");
				if (!(klass is CiStorageType))
					Write('*');
			}
			else if (klass.Class == CiSystem.HashSetClass || klass.Class == CiSystem.DictionaryClass)
				WriteGlib("GHashTable *");
			else if (klass.Class == CiSystem.SortedDictionaryClass)
				WriteGlib("GTree *");
			else
				throw new NotImplementedException();
			break;
		case CiContainerType _:
			if (baseType == CiSystem.BoolType) {
				IncludeStdBool();
				Write("bool");
			}
			else if (baseType == CiSystem.MatchClass) {
				WriteGlib("GMatchInfo *");
				space = false;
			}
			else if (baseType == CiSystem.LockClass) {
				Include("threads.h");
				Write("mtx_t");
			}
			else
				WriteName(baseType);
			if (space)
				Write(' ');
			break;
		default:
			Write(baseType.Name);
			if (space)
				Write(' ');
			break;
		}
		WriteArrayPrefix(type);
		symbol();
		while (type.IsArray) {
			CiType elementType = ((CiClassType) type).ElementType;
			if (type is CiArrayStorageType arrayStorage) {
				Write('[');
				VisitLiteralLong(arrayStorage.Length);
				Write(']');
			}
			else if (elementType is CiArrayStorageType)
				Write(')');
			type = elementType;
		}
	}

	void WriteSignature(CiMethod method, Action symbol)
	{
		if (method.Type == CiSystem.VoidType && method.Throws) {
			IncludeStdBool();
			Write("bool ");
			symbol();
		}
		else
			WriteDefinition(method.Type, symbol, true, true);
	}

	protected override void Write(CiType type, bool promote)
	{
		WriteDefinition(type, () => {}, promote, type is CiClassType arrayPtr && arrayPtr.Class == CiSystem.ArrayPtrClass);
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteDefinition(value.Type, () => WriteName(value), true, true);
	}

	void WriteXstructorPtr(bool need, CiClass klass, string name)
	{
		if (need) {
			Write("(CiMethodPtr) ");
			WriteName(klass);
			Write('_');
			Write(name);
		}
		else
			Write("NULL");
	}

	void WriteDynamicArrayCast(CiType elementType)
	{
		Write('(');
		WriteDefinition(elementType, () => Write(elementType.IsArray ? "(*)" : "*"), false, true);
		Write(") ");
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		this.SharedMake = true;
		if (parent > CiPriority.Mul)
			Write('(');
		WriteDynamicArrayCast(elementType);
		Write("CiShared_Make(");
		if (lengthExpr != null)
			lengthExpr.Accept(this, CiPriority.Argument);
		else
			Write('1');
		Write(", sizeof(");
		Write(elementType, false);
		Write("), ");
		if (elementType == CiSystem.StringStorageType) {
			this.PtrConstruct = true;
			this.ListFrees["String"] = "free(*(void **) ptr)";
			Write("(CiMethodPtr) CiPtr_Construct, CiList_FreeString");
		}
		else if (elementType is CiDynamicPtrType) {
			this.PtrConstruct = true;
			this.SharedRelease = true;
			this.ListFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
			Write("(CiMethodPtr) CiPtr_Construct, CiList_FreeShared");
		}
		else if (elementType is CiClass klass) {
			WriteXstructorPtr(NeedsConstructor(klass), klass, "Construct");
			Write(", ");
			WriteXstructorPtr(NeedsDestructor(klass), klass, "Destruct");
		}
		else
			Write("NULL, NULL");
		Write(')');
		if (parent > CiPriority.Mul)
			Write(')');
	}

	protected override void WriteNew(CiClass klass, CiPriority parent)
	{
		WriteNewArray(klass, null, parent);
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
			Write(')');
		}
		else if (expr is CiInterpolatedString
				|| (expr is CiCallExpr call && expr.Type == CiSystem.StringStorageType && !call.Method.IsReferenceTo(CiSystem.StringSubstring)))
			expr.Accept(this, CiPriority.Argument);
		else {
			Include("string.h");
			WriteCall("strdup", expr);
		}
	}

	static bool IsHeapAllocated(CiType type) => type == CiSystem.StringStorageType || type is CiDynamicPtrType;

	protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		switch (value) {
		case null:
			if (IsHeapAllocated(array.StorageType))
				Write(" = { NULL }");
			break;
		case CiLiteral literal when literal.IsDefaultValue:
			Write(" = { ");
			literal.Accept(this, CiPriority.Argument);
			Write(" }");
			break;
		default:
			throw new NotImplementedException("Only null, zero and false supported");
		}
	}

	string GetListDestroy(CiType type)
	{
		if (type is CiClassType klass
		 && (klass.Class == CiSystem.ListClass || klass.Class == CiSystem.StackClass)) {
			CiType elementType = klass.ElementType;
			if (elementType == CiSystem.StringStorageType) {
				this.ListFrees["String"] = "free(*(void **) ptr)";
				return "CiList_FreeString";
			}
			if (elementType is CiDynamicPtrType) {
				this.ListFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
				return "CiList_FreeShared";
			}
			if (elementType is CiClass klass2 && NeedsDestructor(klass2))
				return $"(GDestroyNotify) {klass2.Name}_Destruct";
			if (elementType is CiStorageType storage) {
				if (storage.Class == CiSystem.ListClass || storage.Class == CiSystem.StackClass) {
					this.ListFrees["List"] = "g_array_free(*(GArray **) ptr, TRUE)";
					return "CiList_FreeList";
				}
				if (storage.Class == CiSystem.HashSetClass || storage.Class == CiSystem.DictionaryClass) {
					this.ListFrees["Dictionary"] = "g_hash_table_unref(*(GHashTable **) ptr)";
					return "CiList_FreeDictionary";
				}
				if (storage.Class == CiSystem.SortedDictionaryClass) {
					this.ListFrees["SortedDictionary"] = "g_tree_unref(*(GTree **) ptr)";
					return "CiList_FreeSortedDictionary";
				}
			}
		}
		return null;
	}

	string GetDictionaryDestroy(CiType type)
	{
		if (type == CiSystem.StringStorageType || type is CiArrayStorageType)
			return "free";
		if (type is CiDynamicPtrType) {
			this.SharedRelease = true;
			return "CiShared_Release";
		}
		if (type is CiClass klass)
			return NeedsDestructor(klass) ? $"(GDestroyNotify) {klass.Name}_Delete" /* TODO: emit */ : "free";
		if (type is CiStorageType storage) {
			if (storage.Class == CiSystem.ListClass || storage.Class == CiSystem.StackClass)
				return "(GDestroyNotify) g_array_unref";
			if (storage.Class == CiSystem.HashSetClass || storage.Class == CiSystem.DictionaryClass)
				return "(GDestroyNotify) g_hash_table_unref";
			if (storage.Class == CiSystem.SortedDictionaryClass)
				return "(GDestroyNotify) g_tree_unref";
		}
		return "NULL";
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
			Write('(');
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
		Write(')');
	}

	protected override void WriteNewStorage(CiStorageType storage)
	{
		if (storage.Class == CiSystem.ListClass || storage.Class == CiSystem.StackClass) {
			Write("g_array_new(FALSE, FALSE, sizeof(");
			Write(storage.ElementType, false);
			Write("))");
		}
		else if (storage.Class == CiSystem.QueueClass)
			Write("G_QUEUE_INIT");
		else if (storage.Class == CiSystem.HashSetClass)
			WriteNewHashTable(storage.ElementType, "NULL");
		else if (storage.Class == CiSystem.DictionaryClass)
			WriteNewHashTable(storage.KeyType, GetDictionaryDestroy(storage.ValueType));
		else if (storage.Class == CiSystem.SortedDictionaryClass) {
			string valueDestroy = GetDictionaryDestroy(storage.ValueType);
			if (storage.KeyType == CiSystem.StringPtrType && valueDestroy == "NULL")
				Write("g_tree_new((GCompareFunc) strcmp");
			else {
				Write("g_tree_new_full(CiTree_Compare");
				switch (storage.KeyType) {
				case CiIntegerType _:
					this.TreeCompareInteger = true;
					Write("Integer");
					break;
				case CiStringType _:
					this.TreeCompareString = true;
					Write("String");
					break;
				default:
					throw new NotImplementedException(storage.KeyType.ToString());
				}
				Write(", NULL, ");
				Write(GetDictionaryDestroy(storage.KeyType));
				Write(", ");
				Write(valueDestroy);
			}
			Write(')');
		}
		else
			throw new NotImplementedException();
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Value == null && IsHeapAllocated(def.Type))
			Write(" = NULL");
		else
			base.WriteVarInit(def);
	}

	int WriteTemporary(CiType type, CiExpr expr)
	{
		bool assign = expr != null || (type is CiClassType klass && (klass.Class == CiSystem.ListClass || klass.Class == CiSystem.DictionaryClass || klass.Class == CiSystem.SortedDictionaryClass));
		int id = this.CurrentTemporaries.IndexOf(type);
		if (id < 0) {
			id = this.CurrentTemporaries.Count;
			WriteDefinition(type, () => { Write("citemp"); VisitLiteralLong(id); }, false, true);
			if (assign) {
				Write(" = ");
				if (expr != null)
					WriteCoerced(type, expr, CiPriority.Argument);
				else
					WriteNewStorage(type);
			}
			WriteLine(';');
			this.CurrentTemporaries.Add(expr);
		}
		else if (assign) {
			Write("citemp");
			VisitLiteralLong(id);
			Write(" = ");
			if (expr != null)
				WriteCoerced(type, expr, CiPriority.Argument);
			else
				WriteNewStorage(type);
			WriteLine(';');
			this.CurrentTemporaries[id] = expr;
		}
		return id;
	}

	void WriteStorageTemporary(CiExpr expr)
	{
		if (expr is CiCallExpr && expr.Type is CiClass)
			WriteTemporary(expr.Type, expr);
	}

	void WriteTemporaries(CiExpr expr)
	{
		switch (expr) {
		case CiVar def:
			if (def.Value != null)
				WriteTemporaries(def.Value);
			break;
		case CiLiteral _:
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
			if (unary.Inner != null) // new C()
				WriteTemporaries(unary.Inner);
			break;
		case CiBinaryExpr binary:
			WriteTemporaries(binary.Left);
			WriteTemporaries(binary.Right);
			break;
		case CiSelectExpr select:
			WriteTemporaries(select.Cond);
			break;
		case CiCallExpr call:
			if (call.Method.Left != null) {
				WriteTemporaries(call.Method.Left);
				WriteStorageTemporary(call.Method.Left);
			}
			int i = 0;
			foreach (CiVar param in ((CiMethod) call.Method.Symbol).Parameters) {
				if (i >= call.Arguments.Count)
					break;
				CiExpr arg = call.Arguments[i++];
				WriteTemporaries(arg);
				if (param.Type is CiClassType)
					WriteStorageTemporary(arg);
			}
			break;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	static bool IsTemporary(CiExpr expr) => expr is CiCallExpr && expr.Type is CiClass;

	static bool HasTemporaries(CiExpr expr)
	{
		switch (expr) {
		case CiLiteral _:
		case CiSymbol _:
			return false;
		case CiInterpolatedString interp:
			foreach (CiInterpolatedPart part in interp.Parts)
				if (HasTemporaries(part.Argument))
					return true;
			return false;
		case CiSymbolReference symbol:
			return symbol.Left != null && HasTemporaries(symbol.Left);
		case CiUnaryExpr unary:
			return unary.Inner != null && HasTemporaries(unary.Inner);
		case CiBinaryExpr binary:
			return HasTemporaries(binary.Left) || HasTemporaries(binary.Right);
		case CiSelectExpr select:
			return HasTemporaries(select.Cond);
		case CiCallExpr call:
			if (call.Method.Left != null) {
				if (call.Method.Symbol == CiSystem.ListAdd || call.Method.Symbol == CiSystem.ListInsert || call.Method.Symbol == CiSystem.QueueEnqueue || call.Method.Symbol == CiSystem.StackPush)
					return true;
				if (IsTemporary(call.Method.Left) || HasTemporaries(call.Method.Left))
					return true;
			}
			int i = 0;
			foreach (CiVar param in ((CiMethod) call.Method.Symbol).Parameters) {
				if (i >= call.Arguments.Count)
					break;
				CiExpr arg = call.Arguments[i++];
				if (HasTemporaries(arg) || (param.Type is CiClassType && IsTemporary(arg)))
					return true;
			}
			return false;
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	void CleanupTemporaries()
	{
		for (int i = 0; i < this.CurrentTemporaries.Count; i++) {
			if (!(this.CurrentTemporaries[i] is CiType))
				this.CurrentTemporaries[i] = this.CurrentTemporaries[i].Type;
		}
	}

	static bool NeedToDestruct(CiSymbol symbol)
	{
		CiType type = symbol.Type;
		while (type is CiArrayStorageType array)
			type = array.ElementType;
		if (IsHeapAllocated(type))
			return true;
		if (type is CiClass klass)
			return klass == CiSystem.MatchClass || klass == CiSystem.LockClass || NeedsDestructor(klass);
		if (type is CiStorageType storage)
			return storage.Class.TypeParameterCount > 0; // built-in collections
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
			Write(')');
		}
		else if (type == CiSystem.StringPtrType && expr.Type == CiSystem.StringPtrType) {
			Write("(gpointer) ");
			expr.Accept(this, CiPriority.Primary);
		}
		else
			WriteCoerced(type, expr, CiPriority.Argument);
	}

	void WriteGConstPointerCast(CiExpr expr)
	{
		if (expr.Type.IsNullable || expr.Type == CiSystem.StringStorageType)
			expr.Accept(this, CiPriority.Argument);
		else {
			Write("(gconstpointer) ");
			expr.Accept(this, CiPriority.Primary);
		}
	}

	void WriteQueueObject(CiExpr obj)
	{
		if (obj.Type is CiStorageType) {
			Write('&');
			obj.Accept(this, CiPriority.Primary);
		}
		else
			obj.Accept(this, CiPriority.Argument);
	}

	void WriteQueueGet(string function, CiExpr obj, CiPriority parent)
	{
		CiType elementType = ((CiClassType) obj.Type).ElementType;
		bool parenthesis;
		if (elementType is CiIntegerType && elementType != CiSystem.LongType) {
			Write("GPOINTER_TO_INT(");
			parenthesis = true;
		}
		else {
			parenthesis = parent > CiPriority.Mul;
			if (parenthesis)
				Write('(');
			WriteStaticCastType(elementType);
		}
		Write(function);
		Write('(');
		WriteQueueObject(obj);
		Write(')');
		if (parenthesis)
			Write(')');
	}

	void StartDictionaryInsert(CiExpr dict, CiExpr key)
	{
		CiClassType type = (CiClassType) dict.Type;
		Write(type.Class == CiSystem.SortedDictionaryClass ? "g_tree_insert(" : "g_hash_table_insert(");
		dict.Accept(this, CiPriority.Argument);
		Write(", ");
		WriteGPointerCast(type.KeyType, key);
		Write(", ");
	}

	protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left is CiBinaryExpr indexing
		 && indexing.Op == CiToken.LeftBracket
		 && indexing.Left.Type is CiClassType dict
		 && dict.Class.TypeParameterCount == 2) {
			StartDictionaryInsert(indexing.Left, indexing.Right);
			WriteGPointerCast(dict.ValueType, expr.Right);
			Write(')');
		}
		else if (expr.Left.Type == CiSystem.StringStorageType) {
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
				Write(')');
			}
		}
		else if (expr.Left.Type is CiDynamicPtrType) {
			if (expr.Left.Type.IsClass(CiSystem.RegexClass)) {
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
					Write(')');
				}
				else
					expr.Right.Accept(this, CiPriority.Argument);
				Write(')');
			}
		}
		else
			base.WriteAssign(expr, parent);
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		return (def is CiField && (def.Value != null || IsHeapAllocated(def.Type.StorageType) || (def.Type is CiClassType klass && (klass.Class == CiSystem.ListClass || klass.Class == CiSystem.DictionaryClass || klass.Class == CiSystem.SortedDictionaryClass))))
			|| GetThrowingMethod(def.Value) != null
			|| (def.Type.StorageType is CiClass klass2 && (klass2 == CiSystem.LockClass || NeedsConstructor(klass2)))
			|| GetListDestroy(def.Type) != null;
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (!HasInitCode(def))
			return;
		CiType type = def.Type;
		int nesting = 0;
		while (type is CiArrayStorageType array) {
			OpenLoop("int", nesting++, array.Length);
			type = array.ElementType;
		}
		if (type is CiClass klass) {
			if (klass == CiSystem.LockClass) {
				Write("mtx_init(&");
				WriteArrayElement(def, nesting);
				WriteLine(", mtx_plain | mtx_recursive);");
			}
			else if (NeedsConstructor(klass)) {
				WriteName(klass);
				Write("_Construct(&");
				WriteArrayElement(def, nesting);
				WriteLine(");");
			}
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
				WriteLine(';');
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
	}

	void WriteMemberAccess(CiExpr left, CiClass symbolClass)
	{
		if (left.Type is CiClass klass)
			Write('.');
		else {
			Write("->");
			klass = ((CiClassType) left.Type).Class;
		}
		for (; klass != symbolClass; klass = (CiClass) klass.Parent)
			Write("base.");
	}

	protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		WriteMemberAccess(left, (CiClass) symbol.Symbol.Parent);
	}

	protected override void WriteArrayPtr(CiExpr expr, CiPriority parent)
	{
		if (expr.Type is CiClassType list && list.Class == CiSystem.ListClass) {
			Write('(');
			Write(list.ElementType, false);
			Write(" *) ");
			expr.Accept(this, CiPriority.Primary);
			Write("->data");
		}
		else
			expr.Accept(this, parent);
	}

	void WriteClassPtr(CiClass resultClass, CiExpr expr, CiPriority parent)
	{
		if (expr.Type is CiClass klass && klass != CiSystem.MatchClass && !IsDictionaryClassStgIndexing(expr)) {
			Write('&');
			int tempId = this.CurrentTemporaries.IndexOf(expr);
			if (tempId >= 0) {
				Write("citemp");
				VisitLiteralLong(tempId);
			}
			else
				expr.Accept(this, CiPriority.Primary);
		}
		else if (expr.Type is CiClassType klassPtr && klassPtr.Class != resultClass) {
			Write('&');
			expr.Accept(this, CiPriority.Primary);
			Write("->base");
			klass = (CiClass) klassPtr.Class.Parent;
		}
		else {
			expr.Accept(this, parent);
			return;
		}
		for (; klass != resultClass; klass = (CiClass) klass.Parent)
			Write(".base");
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		switch (type) {
		case CiDynamicPtrType dynamic when expr is CiSymbolReference && parent != CiPriority.Equality:
			this.SharedAddRef = true;
			if (dynamic.Class == CiSystem.ArrayPtrClass)
				WriteDynamicArrayCast(dynamic.ElementType);
			else {
				Write('(');
				WriteName(dynamic.Class);
				Write(" *)");
			}
			WriteCall("CiShared_AddRef", expr);
			break;
		case CiClassType klass when klass.Class != CiSystem.ArrayPtrClass:
			if (klass.Class == CiSystem.QueueClass && expr.Type is CiStorageType) {
				Write('&');
				expr.Accept(this, CiPriority.Primary);
			}
			else
				WriteClassPtr(klass.Class, expr, parent);
			break;
		default:
			if (type == CiSystem.StringStorageType)
				WriteStringStorageValue(expr);
			else
				base.WriteCoercedInternal(type, expr, parent);
			break;
		}
	}

	protected virtual void WriteSubstringEqual(bool cast, CiExpr ptr, CiExpr offset, string literal, CiPriority parent, bool not)
	{
		if (parent > CiPriority.Equality)
			Write('(');
		Include("string.h");
		Write("memcmp(");
		WriteArrayPtrAdd(ptr, offset);
		Write(", ");
		VisitLiteralString(literal);
		Write(", ");
		VisitLiteralLong(literal.Length);
		Write(')');
		Write(GetEqOp(not));
		Write('0');
		if (parent > CiPriority.Equality)
			Write(')');
	}

	protected virtual void WriteEqualStringInternal(CiExpr left, CiExpr right, CiPriority parent, bool not)
	{
		if (parent > CiPriority.Equality)
			Write('(');
		Include("string.h");
		WriteCall("strcmp", left, right);
		Write(GetEqOp(not));
		Write('0');
		if (parent > CiPriority.Equality)
			Write(')');
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
						throw new NotImplementedException(); // TODO: evaluate compile-time
					WriteSubstringEqual(cast, ptr, offset, rightValue, parent, not);
				}
				else if (not) {
					if (parent > CiPriority.CondOr)
						Write('(');
					lengthExpr.Accept(this, CiPriority.Equality);
					Write(" != ");
					VisitLiteralLong(rightLength);
					Write(" || ");
					WriteSubstringEqual(cast, ptr, offset, rightValue, CiPriority.CondOr, true);
					if (parent > CiPriority.CondOr)
						Write(')');
				}
				else {
					if (parent > CiPriority.CondAnd || parent == CiPriority.CondOr)
						Write('(');
					lengthExpr.Accept(this, CiPriority.Equality);
					Write(" == ");
					VisitLiteralLong(rightLength);
					Write(" && ");
					WriteSubstringEqual(cast, ptr, offset, rightValue, CiPriority.CondAnd, false);
					if (parent > CiPriority.CondAnd || parent == CiPriority.CondOr)
						Write(')');
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
		Write(typeCode);
		Write("), CiCompare_");
		Write(typeCode);
		Write(')');
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
		Write('\t');
		obj.Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
		Write('[');
		if (args.Count > 1 && !args[1].IsLiteralZero) {
			args[1].Accept(this, CiPriority.Add); // FIXME: side effect in every iteration
			Write(" + ");
		}
		Write("_i] = ");
		args[0].Accept(this, CiPriority.Argument); // FIXME: side effect in every iteration
	}

	void WriteListAddInsert(CiExpr obj, bool insert, string function, List<CiExpr> args)
	{
		CiType elementType = ((CiClassType) obj.Type).ElementType;
		// TODO: don't emit temporary variable if already a var/field of matching type - beware of integer promotions!
		int id = WriteTemporary(elementType, elementType.IsFinal ? null : args[args.Count - 1]);
		if (elementType is CiClass klass && NeedsConstructor(klass)) {
			WriteName(klass);
			Write("_Construct(&citemp");
			VisitLiteralLong(id);
			WriteLine(");");
		}
		Write(function);
		Write('(');
		obj.Accept(this, CiPriority.Argument);
		if (insert) {
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
		}
		Write(", citemp");
		VisitLiteralLong(id);
		Write(')');
		this.CurrentTemporaries[id] = elementType;
	}

	void WriteDictionaryLookup(CiExpr obj, string function, CiExpr key)
	{
		Write(function);
		Write('(');
		obj.Accept(this, CiPriority.Argument);
		Write(", ");
		WriteGConstPointerCast(key);
		Write(')');
	}

	void WriteArgsAndRightParenthesis(CiMethod method, List<CiExpr> args)
	{
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i > 0 || method.CallType != CiCallType.Static)
				Write(", ");
			if (i >= args.Count)
				param.Value.Accept(this, CiPriority.Argument);
			else
				WriteCoerced(param.Type, args[i], CiPriority.Argument);
			i++;
		}
		Write(')');
	}

	void WriteRegexOptions(List<CiExpr> args)
	{
		if (!WriteRegexOptions(args, "", " | ", "", "G_REGEX_CASELESS", "G_REGEX_MULTILINE", "G_REGEX_DOTALL"))
			Write('0');
	}

	void WriteConsoleWrite(CiExpr obj, List<CiExpr> args, bool newLine)
	{
		bool error = obj.IsReferenceTo(CiSystem.ConsoleError);
		Include("stdio.h");
		if (args.Count == 0)
			Write(error ? "putc('\\n', stderr)" : "putchar('\\n')");
		else if (args[0] is CiInterpolatedString interpolated) {
			Write(error ? "fprintf(stderr, " : "printf(");
			WritePrintf(interpolated, newLine);
		}
		else if (args[0].Type is CiNumericType) {
			Write(error ? "fprintf(stderr, " : "printf(");
			Write(args[0].Type is CiIntegerType ? "\"%d" : "\"%g");
			if (newLine)
				Write("\\n");
			Write("\", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(')');
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
				Write(')');
			}
		}
		else
			WriteCall("puts", args[0]);
	}

	protected void WriteCCall(CiExpr obj, CiMethod method, List<CiExpr> args)
	{
		if (obj != null && obj.IsReferenceTo(CiSystem.BasePtr)) {
			WriteName(method);
			Write("(&self->base");
		}
		else {
			CiClass klass = this.CurrentClass;
			CiClass definingClass = (CiClass) method.Parent;
			CiClass declaringClass = definingClass;
			switch (method.CallType) {
			case CiCallType.Override:
				declaringClass = (CiClass) method.DeclaringMethod.Parent;
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
					Write(')');
				Write("->");
				WriteCamelCase(method.Name);
				break;
			default:
				WriteName(method);
				break;
			}
			Write('(');
			if (method.CallType != CiCallType.Static) {
				if (obj != null)
					WriteClassPtr(declaringClass, obj, CiPriority.Argument);
				else if (klass == declaringClass)
					Write("self");
				else {
					Write("&self->base");
					for (klass = (CiClass) klass.Parent; klass != declaringClass; klass = (CiClass) klass.Parent)
						Write(".base");
				}
			}
		}
		WriteArgsAndRightParenthesis(method, args);
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.StringContains:
			Include("string.h");
			if (parent > CiPriority.Equality)
				Write('(');
			if (IsOneAsciiString(args[0], out char c)) {
				Write("strchr(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				VisitLiteralChar(c);
				Write(')');
			}
			else
				WriteCall("strstr", obj, args[0]);
			Write(" != NULL");
			if (parent > CiPriority.Equality)
				Write(')');
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
		case CiId.StringStartsWith:
			if (parent > CiPriority.Equality)
				Write('(');
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
				Write(')');
			break;
		case CiId.StringSubstring:
			if (args.Count != 1)
				throw new NotImplementedException("Substring");
			if (parent > CiPriority.Add)
				Write('(');
			WriteAdd(obj, args[0]);
			if (parent > CiPriority.Add)
				Write(')');
			break;
		case CiId.CollectionClear:
			CiClassType klass = (CiClassType) obj.Type;
			if (klass.Class == CiSystem.ListClass || klass.Class == CiSystem.StackClass) {
				Write("g_array_set_size(");
				obj.Accept(this, CiPriority.Argument);
				Write(", 0)");
			}
			else if (klass.Class == CiSystem.QueueClass) {
				Write("g_queue_clear("); // TODO: g_queue_clear_full
				WriteQueueObject(obj);
				Write(')');
			}
			else if (klass.Class == CiSystem.HashSetClass || klass.Class == CiSystem.DictionaryClass)
				WriteCall("g_hash_table_remove_all", obj);
			else if (klass.Class == CiSystem.SortedDictionaryClass) {
				// TODO: since glib-2.70: WriteCall("g_tree_remove_all", obj);
				Write("g_tree_destroy(g_tree_ref(");
				obj.Accept(this, CiPriority.Argument);
				Write("))");
			}
			else
				throw new NotImplementedException();
			break;
		case CiId.CollectionCopyTo:
			Include("string.h");
			Write("memcpy(");
			WriteArrayPtrAdd(args[1], args[2]);
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			CiType elementType = ((CiClassType) obj.Type).ElementType;
			if (elementType is CiRangeType range
			 && ((range.Min >= 0 && range.Max <= byte.MaxValue)
				|| (range.Min >= sbyte.MinValue && range.Max <= sbyte.MaxValue)))
				args[3].Accept(this, CiPriority.Argument);
			else {
				args[3].Accept(this, CiPriority.Mul);
				Write(" * sizeof(");
				Write(elementType, false);
				Write(')');
			}
			Write(')');
			break;
		case CiId.CollectionSortAll:
			switch (obj.Type) {
			case CiArrayStorageType arrayStorage:
				Write("qsort(");
				WriteArrayPtr(obj, CiPriority.Argument);
				Write(", ");
				VisitLiteralLong(arrayStorage.Length);
				WriteSizeofCompare(arrayStorage.ElementType);
				break;
			case CiClassType list:
				Write("g_array_sort(");
				obj.Accept(this, CiPriority.Argument);
				TypeCode typeCode2 = GetTypeCode(list.ElementType, false);
				Write(", CiCompare_");
				Write(typeCode2);
				Write(')');
				this.Compares.Add(typeCode2);
				break;
			default:
				throw new NotImplementedException();
			}
			break;
		case CiId.CollectionSortPart:
			Write("qsort(");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			args[1].Accept(this, CiPriority.Primary);
			WriteSizeofCompare(((CiClassType) obj.Type).ElementType);
			break;
		case CiId.ArrayBinarySearchAll:
		case CiId.ArrayBinarySearchPart:
			if (parent > CiPriority.Add)
				Write('(');
			Write("(const ");
			CiType elementType2 = ((CiClassType) obj.Type).ElementType;
			Write(elementType2, false);
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
				Write(')');
			break;
		case CiId.ArrayFillAll:
		case CiId.ArrayFillPart:
			if (args[0] is CiLiteral literal && literal.IsDefaultValue) {
				Include("string.h");
				Write("memset(");
				if (args.Count == 1) {
					obj.Accept(this, CiPriority.Argument);
					Write(", 0, sizeof(");
					obj.Accept(this, CiPriority.Argument);
					Write(')');
				}
				else {
					WriteArrayPtrAdd(obj, args[1]);
					Write(", 0, ");
					args[2].Accept(this, CiPriority.Mul);
					Write(" * sizeof(");
					Write(((CiClassType) obj.Type).ElementType, false);
					Write(')');
				}
				Write(')');
			}
			else
				WriteArrayFill(obj, args);
			break;
		case CiId.ListAdd:
		case CiId.StackPush:
			switch (((CiClassType) obj.Type).ElementType) {
			case CiArrayStorageType _:
			case CiClass klass2 when !NeedsConstructor(klass2):
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
		case CiId.ListContains:
			Write("CiArray_Contains_");
			TypeCode typeCode = GetTypeCode(((CiClassType) obj.Type).ElementType, false);
			if (typeCode == TypeCode.String) {
				Include("string.h");
				Write("string((const char * const");
			}
			else {
				Write(typeCode);
				Write("((const ");
				Write(typeCode);
			}
			Write(" *) ");
			obj.Accept(this, CiPriority.Primary);
			Write("->data, ");
			obj.Accept(this, CiPriority.Primary); // TODO: side effect
			Write("->len, ");
			args[0].Accept(this, CiPriority.Argument);
			Write(')');
			this.Contains.Add(typeCode);
			break;
		case CiId.ListInsert:
			WriteListAddInsert(obj, true, "g_array_insert_val", args);
			break;
		case CiId.ListRemoveAt:
			WriteCall("g_array_remove_index", obj, args[0]);
			break;
		case CiId.ListRemoveRange:
			WriteCall("g_array_remove_range", obj, args[0], args[1]);
			break;
		case CiId.QueueDequeue:
			WriteQueueGet("g_queue_pop_head", obj, parent);
			break;
		case CiId.QueueEnqueue:
			Write("g_queue_push_tail(");
			WriteQueueObject(obj);
			Write(", ");
			WriteGPointerCast(((CiClassType) obj.Type).ElementType, args[0]);
			Write(')');
			break;
		case CiId.QueuePeek:
			WriteQueueGet("g_queue_peek_head", obj, parent);
			break;
		case CiId.StackPeek:
			StartArrayIndexing(obj, ((CiClassType) obj.Type).ElementType);
			obj.Accept(this, CiPriority.Primary); // TODO: side effect
			Write("->len - 1)");
			break;
		case CiId.StackPop:
			// FIXME: destroy
			StartArrayIndexing(obj, ((CiClassType) obj.Type).ElementType);
			Write("--");
			obj.Accept(this, CiPriority.Primary); // TODO: side effect
			Write("->len)");
			break;
		case CiId.HashSetAdd:
			Write("g_hash_table_add(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteGPointerCast(((CiClassType) obj.Type).ElementType, args[0]);
			Write(')');
			break;
		case CiId.HashSetContains:
			WriteDictionaryLookup(obj, "g_hash_table_contains", args[0]);
			break;
		case CiId.HashSetRemove:
			WriteDictionaryLookup(obj, "g_hash_table_remove", args[0]);
			break;
		case CiId.DictionaryAdd:
			StartDictionaryInsert(obj, args[0]);
			CiType valueType = ((CiClassType) obj.Type).ValueType;
			switch (valueType) {
			case CiClassType klass3 when klass3.Class == CiSystem.ListClass || klass3.Class == CiSystem.StackClass || klass3.Class == CiSystem.DictionaryClass || klass3.Class == CiSystem.SortedDictionaryClass:
				WriteNewStorage(valueType);
				break;
			case CiClass klass3 when klass3.IsPublic && klass3.Constructor != null && klass3.Constructor.Visibility == CiVisibility.Public:
				WriteName(klass3);
				Write("_New()");
				break;
			default:
				Write("malloc(sizeof(");
				Write(valueType, false);
				Write("))");
				break;
			}
			Write(')');
			break;
		case CiId.DictionaryContainsKey:
			if (((CiClassType) obj.Type).Class == CiSystem.SortedDictionaryClass) {
				Write("g_tree_lookup_extended(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteGConstPointerCast(args[0]);
				Write(", NULL, NULL)");
			}
			else
				WriteDictionaryLookup(obj, "g_hash_table_contains", args[0]);
			break;
		case CiId.DictionaryRemove:
			WriteDictionaryLookup(obj, ((CiClassType) obj.Type).Class == CiSystem.SortedDictionaryClass ? "g_tree_remove" : "g_hash_table_remove", args[0]);
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
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteRegexOptions(args);
			Write(", 0, NULL)");
			break;
		case CiId.RegexEscape:
			WriteGlib("g_regex_escape_string(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", -1)");
			break;
		case CiId.RegexIsMatchStr:
			WriteGlib("g_regex_match_simple(");
			args[1].Accept(this, CiPriority.Argument);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteRegexOptions(args);
			Write(", 0)");
			break;
		case CiId.RegexIsMatchRegex:
			Write("g_regex_match(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(", 0, NULL)");
			break;
		case CiId.MatchFindStr:
			this.MatchFind = true;
			Write("CiMatch_Find(&");
			obj.Accept(this, CiPriority.Primary);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			args[1].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteRegexOptions(args);
			Write(')');
			break;
		case CiId.MatchFindRegex:
			Write("g_regex_match(");
			args[1].Accept(this, CiPriority.Argument);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(", 0, &");
			obj.Accept(this, CiPriority.Primary);
			Write(')');
			break;
		case CiId.MatchGetCapture:
			WriteCall("g_match_info_fetch", obj, args[0]);
			break;
		case CiId.MathMethod:
		case CiId.MathIsFinite:
		case CiId.MathIsNaN:
		case CiId.MathLog2:
			Include("math.h");
			WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathCeiling:
			Include("math.h");
			WriteCall("ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			Include("math.h");
			WriteCall("fma", args[0], args[1], args[2]);
			break;
		case CiId.MathIsInfinity:
			Include("math.h");
			WriteCall("isinf", args[0]);
			break;
		case CiId.MathTruncate:
			Include("math.h");
			WriteCall("trunc", args[0]);
			break;
		default:
			WriteCCall(obj, method, args);
			break;
		}
	}

	void StartArrayIndexing(CiExpr obj, CiType elementType)
	{
		Write("g_array_index(");
		obj.Accept(this, CiPriority.Argument);
		Write(", ");
		Write(elementType, false);
		Write(", ");
	}

	void WriteDictionaryIndexing(string function, CiBinaryExpr expr, CiPriority parent)
	{
		CiClassType klass = (CiClassType) expr.Left.Type;
		if (klass.ValueType is CiIntegerType && klass.ValueType != CiSystem.LongType) {
			Write("GPOINTER_TO_INT(");
			WriteDictionaryLookup(expr.Left, function, expr.Right);
			Write(')');
		}
		else {
			if (parent > CiPriority.Mul)
				Write('(');
			if (klass.ValueType is CiClass || klass.ValueType is CiArrayStorageType)
				WriteDynamicArrayCast(klass.ValueType);
			else {
				WriteStaticCastType(klass.ValueType);
				if (klass.ValueType is CiEnum) {
					Trace.Assert(parent <= CiPriority.Mul, "Should close two parens");
					Write("GPOINTER_TO_INT(");
				}
			}
			WriteDictionaryLookup(expr.Left, function, expr.Right);
			if (parent > CiPriority.Mul || klass.ValueType is CiEnum)
				Write(')');
		}
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left.Type is CiClassType klass) {
			if (klass.Class == CiSystem.ListClass) {
				if (klass.ElementType is CiArrayStorageType) {
					Write('(');
					WriteDynamicArrayCast(klass.ElementType);
					expr.Left.Accept(this, CiPriority.Primary);
					Write("->data)[");
					expr.Right.Accept(this, CiPriority.Argument);
					Write(']');
				}
				else {
					StartArrayIndexing(expr.Left, klass.ElementType);
					expr.Right.Accept(this, CiPriority.Argument);
					Write(')');
				}
				return;
			}
			if (klass.Class == CiSystem.DictionaryClass) {
				WriteDictionaryIndexing("g_hash_table_lookup", expr, parent);
				return;
			}
			if (klass.Class == CiSystem.SortedDictionaryClass) {
				WriteDictionaryIndexing("g_tree_lookup", expr, parent);
				return;
			}
		}
		base.WriteIndexing(expr, parent);
	}

	public override CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Equal:
		case CiToken.NotEqual:
		case CiToken.Greater:
			if (IsStringEmpty(expr, out CiExpr str)) {
				str.Accept(this, CiPriority.Primary);
				Write(expr.Op == CiToken.Equal ? "[0] == '\\0'" : "[0] != '\\0'");
				return expr;
			}
			break;
		case CiToken.AddAssign:
			if (expr.Left.Type == CiSystem.StringStorageType) {
				if (expr.Right is CiInterpolatedString rightInterpolated) {
					this.StringAssign = true;
					Write("CiString_Assign(&");
					expr.Left.Accept(this, CiPriority.Primary);
					Write(", ");
					CiInterpolatedString interpolated = new CiInterpolatedString { Suffix = rightInterpolated.Suffix };
					interpolated.Parts.Add(new CiInterpolatedPart("", expr.Left)); // TODO: side effect
					interpolated.Parts.AddRange(rightInterpolated.Parts);
					VisitInterpolatedString(interpolated, CiPriority.Argument);
				}
				else {
					Include("string.h");
					this.StringAppend = true;
					Write("CiString_Append(&");
					expr.Left.Accept(this, CiPriority.Primary);
					Write(", ");
					expr.Right.Accept(this, CiPriority.Argument);
				}
				Write(')');
				return expr;
			}
			break;
		default:
			break;
		}
		return base.VisitBinaryExpr(expr, parent);
	}

	protected override void WriteResource(string name, int length)
	{
		Write("CiResource_");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
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
				Write(')');
			}
		}
		else if (throwingMethod.Type == CiSystem.VoidType) {
			Write('!');
			source(CiPriority.Primary);
		}
		else {
			source(CiPriority.Equality);
			Write(" == NULL");
		}
		Write(')');
		if (this.VarsToDestruct.Count > 0) {
			Write(' ');
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
			type = array.ElementType;
		}
		bool arrayFree = false;
		if (type is CiClass klass) {
			if (klass == CiSystem.MatchClass)
				Write("g_match_info_free(");
			else if (klass == CiSystem.LockClass)
				Write("mtx_destroy(&");
			else {
				WriteName(klass);
				Write("_Destruct(&");
			}
		}
		else if (type is CiDynamicPtrType) {
			if (type.IsClass(CiSystem.RegexClass))
				Write("g_regex_unref(");
			else {
				this.SharedRelease = true;
				Write("CiShared_Release(");
			}
		}
		else if (type is CiStorageType storage) {
			if (storage.Class == CiSystem.ListClass || storage.Class == CiSystem.StackClass) {
				Write("g_array_free(");
				arrayFree = true;
			}
			else if (storage.Class == CiSystem.QueueClass) {
				Write("g_queue_clear(");
				if (storage is CiStorageType)
					Write('&');
			}
			else if (storage.Class == CiSystem.HashSetClass || storage.Class == CiSystem.DictionaryClass)
				Write("g_hash_table_unref(");
			else if (storage.Class == CiSystem.SortedDictionaryClass)
				Write("g_tree_unref(");
			else
				throw new NotImplementedException(storage.ToString());
		}
		else
			Write("free(");
		WriteLocalName(symbol, CiPriority.Primary);
		for (int i = 0; i < nesting; i++) {
			Write("[_i");
			VisitLiteralLong(i);
			Write(']');
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

	public override void VisitBlock(CiBlock statement)
	{
		OpenBlock();
		int temporariesCount = this.CurrentTemporaries.Count;
		Write(statement.Statements);
		int i = this.VarsToDestruct.Count;
		for (; i > 0; i--) {
			CiVar def = this.VarsToDestruct[i - 1];
			if (def.Parent != statement) // destroy only the variables in this block
				break;
			if (statement.CompletesNormally)
				WriteDestruct(def);
		}
		TrimVarsToDestruct(i);
		this.CurrentTemporaries.RemoveRange(temporariesCount, this.CurrentTemporaries.Count - temporariesCount);
		CloseBlock();
	}

	bool BreakOrContinueNeedsBlock(CiCondCompletionStatement loopOrSwitch)
	{
		int count = this.VarsToDestruct.Count;
		return count > 0 && loopOrSwitch.Encloses(this.VarsToDestruct[count - 1]);
	}

	bool NeedsBlock(CiStatement statement)
	{
		switch (statement) {
		case CiExpr expr:
			return HasTemporaries(expr) || GetThrowingMethod(expr) != null;
		case CiBreak brk:
			return BreakOrContinueNeedsBlock(brk.LoopOrSwitch);
		case CiContinue cont:
			return BreakOrContinueNeedsBlock(cont.Loop);
		case CiReturn ret:
			return this.VarsToDestruct.Count > 0 || (ret.Value != null && HasTemporaries(ret.Value));
		case CiThrow _:
			return this.VarsToDestruct.Count > 0;
		default:
			return false;
		}
	}

	protected override void WriteChild(CiStatement statement)
	{
		if (NeedsBlock(statement)) {
			Write(' ');
			OpenBlock();
			statement.Accept(this);
			CloseBlock();
		}
		else
			base.WriteChild(statement);
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
		WriteTemporaries(statement);
		CiMethod throwingMethod = GetThrowingMethod(statement);
		if (throwingMethod != null)
			WriteForwardThrow(parent => statement.Accept(this, parent), throwingMethod);
		else if (statement is CiCallExpr && statement.Type == CiSystem.StringStorageType) {
			Write("free(");
			statement.Accept(this, CiPriority.Argument);
			WriteLine(");");
		}
		else if (statement is CiCallExpr && statement.Type != CiSystem.VoidType && statement.Type is CiDynamicPtrType) {
			this.SharedRelease = true;
			Write("CiShared_Release(");
			statement.Accept(this, CiPriority.Argument);
			WriteLine(");");
		}
		else
			base.VisitExpr(statement);
		CleanupTemporaries();
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
		if (iter.Type is CiIntegerType && iter.Type != CiSystem.LongType) {
			Write("GPOINTER_TO_INT(");
			Write(value);
			Write(')');
		}
		else {
			WriteStaticCastType(iter.Type);
			Write(value);
		}
		WriteLine(';');
	}

	public override void VisitForeach(CiForeach statement)
	{
		string element = statement.Element.Name;
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
			if (klass.Class == CiSystem.ListClass) {
				Write("for (");
				CiType elementType = klass.ElementType;
				Write(elementType, false);
				Write(" const *");
				WriteCamelCaseNotKeyword(element);
				Write(" = (");
				Write(elementType, false);
				Write(" const *) ");
				statement.Collection.Accept(this, CiPriority.Primary);
				Write("->data, ");
				for (; elementType.IsArray; elementType = ((CiClassType) elementType).ElementType)
					Write('*');
				if (elementType is CiStringType || elementType is CiClassType)
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
			}
			else if (klass.Class == CiSystem.HashSetClass) {
				StartForeachHashTable(statement);
				WriteLine("gpointer cikey;");
				Write("while (g_hash_table_iter_next(&cidictit, &cikey, NULL)) ");
				OpenBlock();
				WriteDictIterVar(statement.Element, "cikey");
				FlattenBlock(statement.Body);
				CloseBlock();
				CloseBlock();
			}
			else if (klass.Class == CiSystem.DictionaryClass) {
				StartForeachHashTable(statement);
				WriteLine("gpointer cikey, civalue;");
				Write("while (g_hash_table_iter_next(&cidictit, &cikey, &civalue)) ");
				OpenBlock();
				WriteDictIterVar(statement.Element, "cikey");
				WriteDictIterVar(statement.ValueVar, "civalue");
				FlattenBlock(statement.Body);
				CloseBlock();
				CloseBlock();
			}
			else if (klass.Class == CiSystem.SortedDictionaryClass) {
				Write("for (GTreeNode *cidictit = g_tree_node_first(");
				statement.Collection.Accept(this, CiPriority.Argument);
				Write("); cidictit != NULL; cidictit = g_tree_node_next(cidictit)) ");
				OpenBlock();
				WriteDictIterVar(statement.Element, "g_tree_node_key(cidictit)");
				WriteDictIterVar(statement.ValueVar, "g_tree_node_value(cidictit)");
				FlattenBlock(statement.Body);
				CloseBlock();
			}
			else
				throw new NotImplementedException();
			break;
		default:
			throw new NotImplementedException(statement.Collection.Type.ToString());
		}
	}

	public override void VisitLock(CiLock statement)
	{
		Write("mtx_lock(&");
		statement.Lock.Accept(this, CiPriority.Primary);
		WriteLine(");");
		// TODO
		statement.Body.Accept(this);
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
		else if (this.VarsToDestruct.Count == 0 || statement.Value is CiLiteral) {
			WriteDestructAll();
			WriteTemporaries(statement.Value);
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
					WriteLine(';');
					return;
				}
				if (symbol.Left == null) {
					// Local variable value doesn't depend on destructed variables
					WriteDestructAll();
					base.VisitReturn(statement);
					return;
				}
			}
			WriteTemporaries(statement.Value);
			WriteDefinition(this.CurrentMethod.Type, () => Write("returnValue"), true, true);
			Write(" = ");
			WriteCoerced(this.CurrentMethod.Type, statement.Value, CiPriority.Argument);
			WriteLine(';');
			WriteDestructAll();
			WriteLine("return returnValue;");
		}
	}

	protected override void WriteCaseBody(List<CiStatement> statements)
	{
		if (statements[0] is CiVar
		 || (statements[0] is CiConst konst && konst.Type is CiArrayStorageType))
			WriteLine(';');
		int varsToDestructCount = this.VarsToDestruct.Count;
		Write(statements);
		TrimVarsToDestruct(varsToDestructCount);
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
		else if (this.CurrentMethod.Type == CiSystem.VoidType)
			Write("false");
		else
			Write("NULL");
	}

	public override void VisitThrow(CiThrow statement)
	{
		WriteDestructAll();
		Write("return ");
		WriteThrowReturnValue();
		WriteLine(';');
	}

	bool TryWriteCallAndReturn(List<CiStatement> statements, int lastCallIndex, CiExpr returnValue)
	{
		if (this.VarsToDestruct.Count > 0)
			return false;
		CiExpr call = statements[lastCallIndex] as CiExpr;
		CiMethod throwingMethod = GetThrowingMethod(call);
		if (throwingMethod == null)
			return false;
		Write(statements, lastCallIndex);
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
				Write(')');
			}
		}
		else if (throwingMethod.Type == CiSystem.VoidType)
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
		WriteLine(';');
		return true;
	}

	protected override void Write(List<CiStatement> statements)
	{
		int i = statements.Count - 2;
		if (i >= 0 && statements[i + 1] is CiReturn ret && TryWriteCallAndReturn(statements, i, ret.Value))
			return;
		base.Write(statements);
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		Write("typedef enum ");
		OpenBlock();
		bool first = true;
		foreach (CiConst konst in enu) {
			if (!first)
				WriteLine(',');
			first = false;
			Write(konst.Documentation);
			WriteName(enu);
			Write('_');
			WriteUppercaseWithUnderscores(konst.Name);
			WriteExplicitEnumValue(konst);
		}
		WriteLine();
		this.Indent--;
		Write("} ");
		WriteName(enu);
		WriteLine(';');
	}

	void WriteTypedef(CiClass klass)
	{
		if (klass.CallType == CiCallType.Static)
			return;
		Write("typedef struct ");
		WriteName(klass);
		Write(' ');
		WriteName(klass);
		WriteLine(';');
	}

	protected void WriteTypedefs(CiProgram program, bool pub)
	{
		foreach (CiContainerType type in program) {
			if (type.IsPublic == pub) {
				switch (type) {
				case CiEnum enu:
					Write(enu);
					break;
				case CiClass klass:
					WriteTypedef(klass);
					break;
				default:
					throw new NotImplementedException(type.ToString());
				}
			}
		}
	}

	void WriteInstanceParameters(CiMethod method)
	{
		Write('(');
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
			Write('_');
			Write(method.Name);
			if (method.CallType != CiCallType.Static)
				WriteInstanceParameters(method);
			else if (method.Parameters.Count == 0)
				Write("(void)");
			else
				WriteParameters(method, false);
		});
	}

	static CiClass GetVtblStructClass(CiClass klass)
	{
		while (!klass.AddsVirtualMethods)
			klass = (CiClass) klass.Parent;
		return klass;
	}

	static CiClass GetVtblPtrClass(CiClass klass)
	{
		for (CiClass result = null;;) {
			if (klass.AddsVirtualMethods)
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
		foreach (CiMethod method in klass.OfType<CiMethod>()) {
			if (method.IsAbstractOrVirtual) {
				WriteSignature(method, () => {
					Write("(*");
					WriteCamelCase(method.Name);
					Write(')');
					WriteInstanceParameters(method);
				});
				WriteLine(';');
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
			WriteLine(';');
		}
		else if (konst.Visibility == CiVisibility.Public) {
			Write("#define ");
			WriteName(konst);
			Write(' ');
			konst.Value.Accept(this, CiPriority.Argument);
			WriteLine();
		}
	}

	protected override void WriteField(CiField field) => throw new NotImplementedException();

	static bool HasVtblValue(CiClass klass)
	{
		if (klass.CallType == CiCallType.Static || klass.CallType == CiCallType.Abstract)
			return false;
		return klass.OfType<CiMethod>().Any(method => method.CallType == CiCallType.Virtual || method.CallType == CiCallType.Override || method.CallType == CiCallType.Sealed);
	}

	protected override bool NeedsConstructor(CiClass klass)
	{
		if (klass == CiSystem.MatchClass)
			return false;
		return base.NeedsConstructor(klass)
			|| HasVtblValue(klass)
			|| (klass.Parent is CiClass baseClass && NeedsConstructor(baseClass));
	}

	static bool NeedsDestructor(CiClass klass)
	{
		return klass.OfType<CiField>().Any(field => NeedToDestruct(field))
			|| (klass.Parent is CiClass baseClass && NeedsDestructor(baseClass));
	}

	void WriteXstructorSignature(string name, CiClass klass)
	{
		Write("static void ");
		WriteName(klass);
		Write('_');
		Write(name);
		Write('(');
		WriteName(klass);
		Write(" *self)");
	}

	protected void WriteSignatures(CiClass klass, bool pub)
	{
		foreach (CiSymbol member in klass) {
			switch (member) {
			case CiConst konst when (konst.Visibility == CiVisibility.Public) == pub:
				if (pub) {
					WriteLine();
					Write(konst.Documentation);
				}
				WriteConst(konst);
				break;
			case CiMethod method when method.IsLive && (method.Visibility == CiVisibility.Public) == pub && method.CallType != CiCallType.Abstract:
				WriteLine();
				WriteDoc(method);
				WriteSignature(method);
				WriteLine(';');
				break;
			default:
				break;
			}
		}
	}

	protected void WriteStruct(CiClass klass)
	{
		if (klass.CallType != CiCallType.Static) {
			// topological sorting of class hierarchy and class storage fields
			if (this.WrittenClasses.TryGetValue(klass, out bool done)) {
				if (done)
					return;
				throw new CiException(klass, $"Circular dependency for class {klass.Name}");
			}
			this.WrittenClasses.Add(klass, false);
			if (klass.Parent is CiClass baseClass)
				WriteStruct(baseClass);
			foreach (CiField field in klass.OfType<CiField>())
				if (field.Type.BaseType is CiClass fieldClass)
					WriteStruct(fieldClass);
			this.WrittenClasses[klass] = true;

			WriteLine();
			if (klass.AddsVirtualMethods)
				WriteVtblStruct(klass);
			Write(klass.Documentation);
			Write("struct ");
			WriteName(klass);
			Write(' ');
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
			foreach (CiField field in klass.OfType<CiField>()) {
				WriteTypeAndName(field);
				WriteLine(';');
			}
			this.Indent--;
			WriteLine("};");
			if (NeedsConstructor(klass)) {
				WriteXstructorSignature("Construct", klass);
				WriteLine(';');
			}
			if (NeedsDestructor(klass)) {
				WriteXstructorSignature("Destruct", klass);
				WriteLine(';');
			}
		}
		WriteSignatures(klass, false);
	}

	void WriteVtbl(CiClass definingClass, CiClass declaringClass)
	{
		if (declaringClass.Parent is CiClass baseClass)
			WriteVtbl(definingClass, baseClass);
		foreach (CiMethod declaredMethod in declaringClass.OfType<CiMethod>()) {
			if (declaredMethod.IsAbstractOrVirtual) {
				CiSymbol definedMethod = definingClass.TryLookup(declaredMethod.Name);
				if (declaredMethod != definedMethod) {
					Write('(');
					WriteSignature(declaredMethod, () => {
						Write("(*)");
						WriteInstanceParameters(declaredMethod);
					});
					Write(") ");
				}
				WriteName(definedMethod);
				WriteLine(',');
			}
		}
	}

	protected void WriteConstructor(CiClass klass)
	{
		if (!NeedsConstructor(klass))
			return;
		this.StringSwitchesWithGoto.Clear();
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
		foreach (CiField field in klass.OfType<CiField>())
			WriteInitCode(field);
		WriteConstructorBody(klass);
		CloseBlock();
	}

	protected void WriteDestructor(CiClass klass)
	{
		if (!NeedsDestructor(klass))
			return;
		WriteLine();
		WriteXstructorSignature("Destruct", klass);
		WriteLine();
		OpenBlock();
		foreach (CiField field in klass.OfType<CiField>().Reverse())
			WriteDestruct(field);
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
			WriteLine(';');

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
			WriteLine(';');
	}

	protected override void WriteMethod(CiMethod method)
	{
		if (!method.IsLive || method.CallType == CiCallType.Abstract)
			return;
		this.StringSwitchesWithGoto.Clear();
		WriteLine();
		WriteSignature(method);
		foreach (CiVar param in method.Parameters) {
			if (NeedToDestruct(param))
				this.VarsToDestruct.Add(param);
		}
		WriteLine();
		this.CurrentMethod = method;
		OpenBlock();
		if (method.Body is CiBlock block) {
			List<CiStatement> statements = block.Statements;
			if (!block.CompletesNormally)
				Write(statements);
			else if (method.Throws && method.Type == CiSystem.VoidType) {
				if (statements.Count == 0 || !TryWriteCallAndReturn(statements, statements.Count - 1, null)) {
					Write(statements);
					WriteDestructAll();
					WriteLine("return true;");
				}
			}
			else {
				Write(statements);
				WriteDestructAll();
			}
		}
		else
			method.Body.Accept(this);
		this.CurrentTemporaries.Clear();
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
			WriteLine("static void CiString_Append(char **str, const char *suffix)");
			OpenBlock();
			WriteLine("size_t suffixLen = strlen(suffix);");
			WriteLine("if (suffixLen == 0)");
			WriteLine("\treturn;");
			WriteLine("size_t prefixLen = strlen(*str);");
			WriteLine("*str = realloc(*str, prefixLen + suffixLen + 1);");
			WriteLine("memcpy(*str + prefixLen, suffix, suffixLen + 1);");
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
			WriteLine('}');
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
		if (this.SharedRelease || this.SharedAssign || this.ListFrees.ContainsKey("Shared")) {
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
			WriteLine(';');
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
			Write(typeCode);
			WriteLine("(const void *pa, const void *pb)");
			OpenBlock();
			Write(typeCode);
			Write(" a = *(const ");
			Write(typeCode);
			WriteLine(" *) pa;");
			Write(typeCode);
			Write(" b = *(const ");
			Write(typeCode);
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
				Write(typeCode);
				Write("(const ");
				Write(typeCode);
				Write(" *a, size_t len, ");
				Write(typeCode);
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
			Write(TypeCode.Byte);
			Write(' ');
			WriteResource(name, -1);
			Write('[');
			VisitLiteralLong(resources[name].Length);
			WriteLine("] = {");
			Write('\t');
			Write(resources[name]);
			WriteLine(" };");
		}
	}

	public override void Write(CiProgram program)
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
		WriteLine('}');
		WriteLine("#endif");
		CloseFile();

		this.Includes = new SortedSet<string>();
		this.StringAssign = false;
		this.StringSubstring = false;
		this.StringAppend = false;
		this.StringIndexOf = false;
		this.StringLastIndexOf = false;
		this.StringEndsWith = false;
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
			WriteStruct(klass);
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
