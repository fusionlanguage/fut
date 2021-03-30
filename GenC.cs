// GenC.cs - C code generator
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
	bool StringStartsWith;
	bool StringEndsWith;
	bool StringFormat;
	bool MatchFind;
	bool MatchPos;
	bool PtrConstruct;
	bool PtrFree;
	bool SharedMake;
	bool SharedAddRef;
	bool SharedRelease;
	bool SharedReleaseIndirect;
	bool SharedAssign;
	bool TreeCompareInteger;
	bool TreeCompareString;
	readonly SortedSet<TypeCode> Compares = new SortedSet<TypeCode>();
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

	protected override void IncludeStdInt()
	{
		Include("stdint.h");
	}

	protected override void IncludeAssert()
	{
		Include("assert.h");
	}

	protected override void IncludeMath()
	{
		Include("math.h");
	}

	protected virtual void IncludeStdBool()
	{
		Include("stdbool.h");
	}

	protected override void WriteLiteral(object value)
	{
		if (value == null)
			Write("NULL");
		else
			base.WriteLiteral(value);
	}

	public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent)
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
		if (symbol.Parent is CiForeach forEach && forEach.Collection.Type is CiArrayType array) {
			if (array is CiListType) {
				if (parent == CiPriority.Primary)
					Write('(');
				Write('*');
				WriteCamelCaseNotKeyword(symbol.Name);
				if (parent == CiPriority.Primary)
					Write(')');
			}
			else if (array.ElementType is CiClass klass) {
				if (parent > CiPriority.Add)
					Write('(');
				forEach.Collection.Accept(this, CiPriority.Primary);
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
		}
		if (symbol is CiField)
			WriteSelfForField((CiClass) symbol.Parent);
		WriteName(symbol);
	}

	void WriteMatchProperty(CiSymbolReference expr, int which)
	{
		this.MatchPos = true;
		Write("CiMatch_GetPos(");
		expr.Left.Accept(this, CiPriority.Statement);
		Write(", ");
		Write(which);
		Write(')');
	}

	static bool IsDictionaryClassStgIndexing(CiExpr expr)
	{
		return expr is CiBinaryExpr indexing
			&& indexing.Op == CiToken.LeftBracket
			&& indexing.Left.Type is CiDictionaryType dict
			&& dict.ValueType is CiClass;
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left == null || expr.Symbol is CiConst)
			WriteLocalName(expr.Symbol, parent);
		else if (expr.Symbol == CiSystem.CollectionCount) {
			switch (expr.Left.Type) {
			case CiListType _:
				expr.Left.Accept(this, CiPriority.Primary);
				Write("->len");
				break;
			case CiSortedDictionaryType _:
				WriteCall("g_tree_nnodes", expr.Left);
				break;
			case CiDictionaryType _:
				WriteCall("g_hash_table_size", expr.Left);
				break;
			default:
				throw new NotImplementedException(expr.Left.Type.ToString());
			}
		}
		else if (expr.Symbol == CiSystem.MatchStart)
			WriteMatchProperty(expr, 0);
		else if (expr.Symbol == CiSystem.MatchEnd)
			WriteMatchProperty(expr, 1);
		else if (expr.Symbol == CiSystem.MatchLength)
			WriteMatchProperty(expr, 2);
		else if (expr.Symbol == CiSystem.MatchValue) {
			Write("g_match_info_fetch(");
			expr.Left.Accept(this, CiPriority.Statement);
			Write(", 0)");
		}
		else if (IsDictionaryClassStgIndexing(expr.Left)) {
			expr.Left.Accept(this, CiPriority.Primary);
			Write("->");
			WriteName(expr.Symbol);
		}
		else
			return base.Visit(expr, parent);
		return expr;
	}

	protected virtual void WriteStringPtrType()
	{
		Write("const char *");
	}

	void WriteArrayPrefix(CiType type)
	{
		if (type is CiArrayType array) {
			WriteArrayPrefix(array.ElementType);
			if (type is CiArrayPtrType arrayPtr) {
				if (array.ElementType is CiArrayStorageType)
					Write('(');
				switch (arrayPtr.Modifier) {
				case CiToken.EndOfFile:
					Write("const *");
					break;
				case CiToken.ExclamationMark:
				case CiToken.Hash:
					Write('*');
					break;
				default:
					throw new NotImplementedException(arrayPtr.Modifier.ToString());
				}
			}
		}
	}

	void WriteDefinition(CiType type, Action symbol, bool promote, bool space)
	{
		if (type == null) {
			Write("void ");
			symbol();
			return;
		}
		if (type is CiListType) {
			Include("glib.h");
			Write("GArray *");
			symbol();
			return;
		}
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
		case CiClassPtrType classPtr:
			if (classPtr.Modifier == CiToken.EndOfFile)
				Write("const ");
			if (classPtr.Class == CiSystem.RegexClass) {
				Include("glib.h");
				Write("GRegex");
			}
			else if (classPtr.Class == CiSystem.MatchClass) {
				Include("glib.h");
				Write("GMatchInfo");
			}
			else
				WriteName(classPtr.Class);
			Write(" *");
			break;
		case CiSortedDictionaryType _:
			Include("glib.h");
			Write("GTree *");
			break;
		case CiDictionaryType _:
			Include("glib.h");
			Write("GHashTable *");
			break;
		case CiContainerType _:
			if (baseType == CiSystem.BoolType) {
				IncludeStdBool();
				Write("bool");
			}
			else if (baseType == CiSystem.MatchClass) {
				Include("glib.h");
				Write("GMatchInfo *");
				space = false;
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
		while (type is CiArrayType array) {
			if (type is CiArrayStorageType arrayStorage) {
				Write('[');
				Write(arrayStorage.Length);
				Write(']');
			}
			else if (array.ElementType is CiArrayStorageType)
				Write(')');
			type = array.ElementType;
		}
	}

	void WriteSignature(CiMethod method, Action symbol)
	{
		if (method.Type == null) {
			Write(method.Throws ? "bool " : "void ");
			symbol();
		}
		else
			WriteDefinition(method.Type, symbol, true, true);
	}

	protected override void Write(CiType type, bool promote)
	{
		WriteDefinition(type, () => {}, promote, type is CiArrayPtrType);
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
		WriteDefinition(elementType, () => Write(elementType is CiArrayType ? "(*)" : "*"), false, true);
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
			lengthExpr.Accept(this, CiPriority.Statement);
		else
			Write('1');
		Write(", sizeof(");
		Write(elementType, false);
		Write("), ");
		if (elementType == CiSystem.StringStorageType) {
			this.PtrConstruct = true;
			Write("(CiMethodPtr) CiPtr_Construct, free");
		}
		else if (elementType.IsDynamicPtr) {
			this.PtrConstruct = true;
			this.SharedRelease = true;
			Write("(CiMethodPtr) CiPtr_Construct, CiShared_Release");
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
			length.Accept(this, CiPriority.Statement);
			Write(')');
		}
		else if (expr is CiInterpolatedString
				|| (expr is CiCallExpr call && expr.Type == CiSystem.StringStorageType && !call.Method.IsReferenceTo(CiSystem.StringSubstring)))
			expr.Accept(this, CiPriority.Statement);
		else {
			Include("string.h");
			WriteCall("strdup", expr);
		}
	}

	protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		switch (value) {
		case null:
			if (array.StorageType == CiSystem.StringStorageType || array.StorageType.IsDynamicPtr)
				Write(" = { NULL }");
			break;
		case CiLiteral literal when literal.IsDefaultValue:
			Write(" = { ");
			WriteLiteral(literal.Value);
			Write(" }");
			break;
		default:
			throw new NotImplementedException("Only null, zero and false supported");
		}
	}

	protected override void WriteListStorageInit(CiListType list)
	{
		Write(" = g_array_new(FALSE, FALSE, sizeof(");
		Write(list.ElementType, false);
		Write("))");
	}

	string GetListDestroy(CiType type)
	{
		if (type is CiListType list) {
			if (list.ElementType == CiSystem.StringStorageType) {
				this.PtrFree = true;
				return "CiPtr_Free";
			}
			if (list.ElementType.IsDynamicPtr) {
				this.SharedReleaseIndirect = true;
				return "CiShared_ReleaseIndirect";
			}
			if (list.ElementType is CiClass klass && NeedsDestructor(klass))
				return "(GDestroyNotify) " + klass.Name + "_Destruct";
		}
		return null;
	}

	string GetDictionaryDestroy(CiType type)
	{
		if (type == CiSystem.StringStorageType || type is CiArrayStorageType)
			return "free";
		if (type.IsDynamicPtr) {
			this.SharedRelease = true;
			return "CiShared_Release";
		}
		if (type is CiClass klass)
			return NeedsDestructor(klass) ? "(GDestroyNotify) " + klass.Name + "_Delete" /* TODO: emit */ : "free";
		return "NULL";
	}

	void WriteDictionaryHashEqual(CiDictionaryType dict)
	{
		Write(dict.KeyType is CiStringType ? "g_str_hash, g_str_equal" : "NULL, NULL");
	}

	protected override void WriteDictionaryStorageInit(CiDictionaryType dict)
	{
		string keyDestroy = GetDictionaryDestroy(dict.KeyType);
		string valueDestroy = GetDictionaryDestroy(dict.ValueType);
		if (dict is CiSortedDictionaryType) {
			if (dict.KeyType == CiSystem.StringPtrType && valueDestroy == "NULL")
				Write(" = g_tree_new((GCompareFunc) strcmp");
			else {
				Write(" = g_tree_new_full(CiTree_Compare");
				switch (dict.KeyType) {
				case CiIntegerType _:
					this.TreeCompareInteger = true;
					Write("Integer");
					break;
				case CiStringType _:
					this.TreeCompareString = true;
					Write("String");
					break;
				default:
					throw new NotImplementedException(dict.KeyType.ToString());
				}
				Write(", NULL, ");
				Write(keyDestroy);
				Write(", ");
				Write(valueDestroy);
			}
		}
		else {
			Write(" = g_hash_table_new");
			if (keyDestroy == "NULL" && valueDestroy == "NULL") {
				Write('(');
				WriteDictionaryHashEqual(dict);
			}
			else {
				Write("_full(");
				WriteDictionaryHashEqual(dict);
				Write(", ");
				Write(keyDestroy);
				Write(", ");
				Write(valueDestroy);
			}
		}
		Write(')');
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Value == null && (def.Type == CiSystem.StringStorageType || def.Type.IsDynamicPtr))
			Write(" = NULL");
		else
			base.WriteVarInit(def);
	}

	static bool NeedToDestruct(CiSymbol symbol)
	{
		CiType type = symbol.Type;
		while (type is CiArrayStorageType array)
			type = array.ElementType;
		return type == CiSystem.StringStorageType
			|| type.IsDynamicPtr
			|| type is CiListType
			|| type is CiDictionaryType
			|| (type is CiClass klass && (klass == CiSystem.MatchClass || NeedsDestructor(klass)));
	}

	protected override void WriteVar(CiNamedValue def)
	{
		base.WriteVar(def);
		if (NeedToDestruct(def))
			this.VarsToDestruct.Add((CiVar) def);
	}

	void WriteGPointerCast(CiExpr expr)
	{
		if (expr.Type == CiSystem.StringStorageType || expr.Type.IsDynamicPtr)
			expr.Accept(this, CiPriority.Statement);
		else if (expr.Type is CiClass) {
			Write('&');
			expr.Accept(this, CiPriority.Primary);
		}
		else {
			Write("(gpointer) ");
			expr.Accept(this, CiPriority.Primary);
		}
	}

	void WriteGConstPointerCast(CiExpr expr)
	{
		switch (expr.Type) {
		case CiStringType _:
		case CiClassPtrType _:
		case CiArrayPtrType _:
			expr.Accept(this, CiPriority.Statement);
			break;
		default:
			Write("(gconstpointer) ");
			expr.Accept(this, CiPriority.Primary);
			break;
		}
	}

	void StartDictionaryInsert(CiExpr dict, CiExpr key)
	{
		Write(dict.Type is CiSortedDictionaryType ? "g_tree_insert(" : "g_hash_table_insert(");
		dict.Accept(this, CiPriority.Statement);
		Write(", ");
		WriteGPointerCast(key);
		Write(", ");
	}

	protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left is CiBinaryExpr indexing
		 && indexing.Op == CiToken.LeftBracket
		 && indexing.Left.Type is CiDictionaryType) {
			StartDictionaryInsert(indexing.Left, indexing.Right);
			WriteGPointerCast(expr.Right);
			Write(')');
		}
		else
			base.WriteAssign(expr, parent);
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		return (def is CiField && (def.Value != null || def.Type.StorageType == CiSystem.StringStorageType || def.Type.IsDynamicPtr || def.Type is CiListType || def.Type is CiDictionaryType))
			|| GetThrowingMethod(def.Value) != null
			|| (def.Type.StorageType is CiClass klass && NeedsConstructor(klass))
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
			if (NeedsConstructor(klass)) {
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
					if (type == CiSystem.StringStorageType || type.IsDynamicPtr)
						Write("NULL");
					else
						def.Value.Accept(this, CiPriority.Statement);
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
			klass = ((CiClassPtrType) left.Type).Class;
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
		if (expr.Type is CiListType list) {
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
			expr.Accept(this, CiPriority.Primary);
		}
		else if (expr.Type is CiClassPtrType klassPtr && klassPtr.Class != resultClass) {
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
		if (type == CiSystem.StringStorageType)
			WriteStringStorageValue(expr);
		else if (type is CiClassPtrType resultPtr) {
			if (resultPtr.Modifier == CiToken.Hash && expr is CiSymbolReference && parent != CiPriority.Equality) {
				this.SharedAddRef = true;
				Write('(');
				WriteName(resultPtr.Class);
				WriteCall(" *) CiShared_AddRef", expr);
			}
			else
				WriteClassPtr(resultPtr.Class, expr, parent);
		}
		else if (type is CiArrayPtrType arrayPtr && arrayPtr.Modifier == CiToken.Hash && expr is CiSymbolReference && parent != CiPriority.Equality) {
			this.SharedAddRef = true;
			WriteDynamicArrayCast(arrayPtr.ElementType);
			WriteCall("CiShared_AddRef", expr);
		}
		else
			base.WriteCoercedInternal(type, expr, parent);
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if ((expr.Left.Type is CiStringType && expr.Right.Type != CiSystem.NullType)
		 || (expr.Right.Type is CiStringType && expr.Left.Type != CiSystem.NullType)) {
			Include("string.h");
			if (parent > CiPriority.Equality)
				Write('(');
			if (IsStringSubstring(expr.Left, out bool _, out CiExpr ptr, out CiExpr offset, out CiExpr lengthExpr) && lengthExpr is CiLiteral literalLength && expr.Right is CiLiteral literal) {
				long length = (long) literalLength.Value;
				string right = (string) literal.Value;
				if (length != right.Length)
					throw new NotImplementedException(); // TODO: evaluate compile-time
				Write("memcmp(");
				WriteArrayPtrAdd(ptr, offset);
				Write(", ");
				expr.Right.Accept(this, CiPriority.Statement);
				Write(", ");
				Write(length);
				Write(')');
			}
			else
				WriteCall("strcmp", expr.Left, expr.Right);
			Write(GetEqOp(not));
			Write('0');
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		Include("string.h");
		WriteCall("(int) strlen", expr);
	}

	void WriteStringMethod(string name, CiExpr obj, CiExpr[] args)
	{
		Include("string.h");
		Write("CiString_");
		WriteCall(name, obj, args[0]);
	}

	void WriteListAddInsert(CiExpr obj, bool insert, string function, CiExpr[] args)
	{
		// TODO: don't emit temporary variable if already a var/field of matching type - beware of integer promotions!
		OpenBlock();
		CiType elementType = ((CiListType) obj.Type).ElementType;
		WriteDefinition(elementType, () => Write("cival"), false, true);
		switch (elementType) {
		case CiClass klass:
			if (NeedsConstructor(klass)) {
				WriteLine(';');
				WriteName(klass);
				Write("_Construct(&cival)");
			}
			break;
		case CiArrayStorageType _:
			break;
		default:
			Write(" = ");
			WriteCoerced(elementType, args[args.Length - 1], CiPriority.Statement);
			break;
		}
		WriteLine(';');
		Write(function);
		Write('(');
		obj.Accept(this, CiPriority.Statement);
		if (insert) {
			Write(", ");
			args[0].Accept(this, CiPriority.Statement);
		}
		WriteLine(", cival);");
		CloseBlock();
	}

	void WriteDictionaryLookup(CiExpr obj, string function, CiExpr key)
	{
		Write(function);
		Write('(');
		obj.Accept(this, CiPriority.Statement);
		Write(", ");
		WriteGConstPointerCast(key);
		Write(')');
	}

	void WriteArgsAndRightParenthesis(CiMethod method, CiExpr[] args)
	{
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i > 0 || method.CallType != CiCallType.Static)
				Write(", ");
			if (i >= args.Length)
				param.Value.Accept(this, CiPriority.Statement);
			else
				WriteCoerced(param.Type, args[i], CiPriority.Statement);
			i++;
		}
		Write(')');
	}

	void WriteRegexOptions(CiExpr[] args)
	{
		if (!WriteRegexOptions(args, "", " | ", "", "G_REGEX_CASELESS", "G_REGEX_MULTILINE", "G_REGEX_DOTALL"))
			Write('0');
	}

	void WriteConsoleWrite(CiExpr obj, CiExpr[] args, bool newLine)
	{
		bool error = obj.IsReferenceTo(CiSystem.ConsoleError);
		Include("stdio.h");
		if (args.Length == 0)
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
			args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else if (newLine && !error)
			WriteCall("puts", args[0]);
		else {
			Write("fputs(");
			args[0].Accept(this, CiPriority.Statement);
			Write(error ? ", stderr)" : ", stdout)");
		}
	}

	protected void WriteCCall(CiExpr obj, CiMethod method, CiExpr[] args)
	{
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
				klass = obj.Type as CiClass ?? ((CiClassPtrType) obj.Type).Class;
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
				WriteClassPtr(declaringClass, obj, CiPriority.Statement);
			else if (klass == declaringClass)
				Write("self");
			else {
				Write("&self->base");
				for (klass = (CiClass) klass.Parent; klass != declaringClass; klass = (CiClass) klass.Parent)
					Write(".base");
			}
		}
		WriteArgsAndRightParenthesis(method, args);
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj == null)
			WriteCCall(null, method, args);
		else if (method == CiSystem.StringContains) {
			Include("string.h");
			if (parent > CiPriority.Equality)
				Write('(');
			if (IsOneAsciiString(args[0], out char c)) {
				Write("strchr(");
				obj.Accept(this, CiPriority.Statement);
				Write(", ");
				WriteCharLiteral(c);
				Write(')');
			}
			else
				WriteCall("strstr", obj, args[0]);
			Write(" != NULL");
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else if (method == CiSystem.StringIndexOf) {
			this.StringIndexOf = true;
			WriteStringMethod("IndexOf", obj, args);
		}
		else if (method == CiSystem.StringLastIndexOf) {
			this.StringLastIndexOf = true;
			WriteStringMethod("LastIndexOf", obj, args);
		}
		else if (method == CiSystem.StringStartsWith) {
			if (IsOneAsciiString(args[0], out char c)) {
				if (parent > CiPriority.Equality)
					Write('(');
				obj.Accept(this, CiPriority.Primary);
				Write("[0] == ");
				WriteCharLiteral(c);
				if (parent > CiPriority.Equality)
					Write(')');
			}
			else {
				this.StringStartsWith = true;
				WriteStringMethod("StartsWith", obj, args);
			}
		}
		else if (method == CiSystem.StringEndsWith) {
			this.StringEndsWith = true;
			WriteStringMethod("EndsWith", obj, args);
		}
		else if (method == CiSystem.StringSubstring && args.Length == 1) {
			if (parent > CiPriority.Add)
				Write('(');
			obj.Accept(this, CiPriority.Add);
			Write(" + ");
			args[0].Accept(this, CiPriority.Add);
			if (parent > CiPriority.Add)
				Write(')');
		}
		else if (obj.Type is CiArrayType array && method.Name == "BinarySearch") {
			if (parent > CiPriority.Add)
				Write('(');
			Write("(const ");
			Write(array.ElementType, false);
			Write(" *) bsearch(&");
			args[0].Accept(this, CiPriority.Statement); // TODO: not lvalue, promoted
			Write(", ");
			if (args.Length == 1)
				WriteArrayPtr(obj, CiPriority.Statement);
			else
				WriteArrayPtrAdd(obj, args[1]);
			Write(", ");
			if (args.Length == 1)
				Write(((CiArrayStorageType) array).Length);
			else
				args[2].Accept(this, CiPriority.Primary);
			Write(", sizeof(");
			TypeCode typeCode = GetTypeCode(array.ElementType, false);
			Write(typeCode);
			Write("), CiCompare_");
			Write(typeCode);
			Write(')');
			this.Compares.Add(typeCode);
			Write(" - ");
			WriteArrayPtr(obj, CiPriority.Mul);
			if (parent > CiPriority.Add)
				Write(')');
		}
		else if (obj.Type is CiArrayType array2 && method.Name == "CopyTo") {
			Include("string.h");
			Write("memcpy(");
			WriteArrayPtrAdd(args[1], args[2]);
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			if (array2.ElementType is CiRangeType range
			 && ((range.Min >= 0 && range.Max <= byte.MaxValue)
				|| (range.Min >= sbyte.MinValue && range.Max <= sbyte.MaxValue)))
				args[3].Accept(this, CiPriority.Statement);
			else {
				args[3].Accept(this, CiPriority.Mul);
				Write(" * sizeof(");
				Write(array2.ElementType, false);
				Write(')');
			}
			Write(')');
		}
		else if (obj.Type is CiArrayType array3 && method.Name == "Fill") {
			if (!(args[0] is CiLiteral literal) || !literal.IsDefaultValue)
				throw new NotImplementedException("Only null, zero and false supported");
			Include("string.h");
			Write("memset(");
			if (args.Length == 1) {
				obj.Accept(this, CiPriority.Statement);
				Write(", 0, sizeof(");
				obj.Accept(this, CiPriority.Statement);
				Write(')');
			}
			else {
				WriteArrayPtrAdd(obj, args[1]);
				Write(", 0, ");
				args[2].Accept(this, CiPriority.Mul);
				Write(" * sizeof(");
				Write(array3.ElementType, false);
				Write(')');
			}
			Write(')');
		}
		else if (method == CiSystem.CollectionSortAll) {
			TypeCode typeCode = GetTypeCode(((CiArrayType) obj.Type).ElementType, false);
			if (obj.Type is CiArrayStorageType arrayStorage) {
				Write("qsort(");
				WriteArrayPtr(obj, CiPriority.Statement);
				Write(", ");
				Write(arrayStorage.Length);
				Write(", sizeof(");
				Write(typeCode);
				Write(')');
			}
			else {
				Write("g_array_sort(");
				obj.Accept(this, CiPriority.Statement);
			}
			Write(", CiCompare_");
			Write(typeCode);
			Write(')');
			this.Compares.Add(typeCode);
		}
		else if (method == CiSystem.CollectionSortPart) {
			Write("qsort(");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			args[1].Accept(this, CiPriority.Primary);
			Write(", sizeof(");
			TypeCode typeCode = GetTypeCode(((CiArrayType) obj.Type).ElementType, false);
			Write(typeCode);
			Write("), CiCompare_");
			Write(typeCode);
			Write(')');
			this.Compares.Add(typeCode);
		}
		else if (obj.Type is CiListType list && method.Name == "Add") {
			if (list.ElementType is CiArrayStorageType || (list.ElementType is CiClass klass && !NeedsConstructor(klass))) {
				Write("g_array_set_size(");
				obj.Accept(this, CiPriority.Statement);
				Write(", ");
				obj.Accept(this, CiPriority.Primary); // TODO: side effect
				Write("->len + 1)");
			}
			else
				WriteListAddInsert(obj, false, "g_array_append_val", args);
		}
		else if (method == CiSystem.CollectionClear) {
			switch (obj.Type) {
			case CiListType _:
				Write("g_array_set_size(");
				obj.Accept(this, CiPriority.Statement);
				Write(", 0)");
				break;
			case CiSortedDictionaryType _:
				// TODO: since glib-2.70: WriteCall("g_tree_remove_all", obj);
				Write("g_tree_destroy(g_tree_ref(");
				obj.Accept(this, CiPriority.Statement);
				Write("))");
				break;
			case CiDictionaryType _:
				WriteCall("g_hash_table_remove_all", obj);
				break;
			default:
				throw new NotImplementedException(obj.Type.ToString());
			}
		}
		else if (obj.Type is CiListType && method.Name == "Insert")
			WriteListAddInsert(obj, true, "g_array_insert_val", args);
		else if (method == CiSystem.ListRemoveAt)
			WriteCall("g_array_remove_index", obj, args[0]);
		else if (method == CiSystem.ListRemoveRange)
			WriteCall("g_array_remove_range", obj, args[0], args[1]);
		else if (obj.Type is CiDictionaryType dict && method.Name == "Add") {
			StartDictionaryInsert(obj, args[0]);
			if (dict.ValueType is CiClass klass && klass.IsPublic && klass.Constructor != null && klass.Constructor.Visibility == CiVisibility.Public) {
				WriteName(klass);
				Write("_New()");
			}
			else {
				Write("malloc(sizeof(");
				Write(dict.ValueType, false);
				Write("))");
			}
			Write(')');
		}
		else if (obj.Type is CiDictionaryType && method.Name == "ContainsKey") {
			if (obj.Type is CiSortedDictionaryType) {
				Write("g_tree_lookup_extended(");
				obj.Accept(this, CiPriority.Statement);
				Write(", ");
				WriteGConstPointerCast(args[0]);
				Write(", NULL, NULL)");
			}
			else
				WriteDictionaryLookup(obj, "g_hash_table_contains", args[0]);
		}
		else if (obj.Type is CiDictionaryType && method.Name == "Remove")
			WriteDictionaryLookup(obj, obj.Type is CiSortedDictionaryType ? "g_tree_remove" : "g_hash_table_remove", args[0]);
		else if (method == CiSystem.RegexCompile) {
			Include("glib.h");
			Write("g_regex_new(");
			args[0].Accept(this, CiPriority.Statement);
			Write(", ");
			WriteRegexOptions(args);
			Write(", 0, NULL)");
		}
		else if (method == CiSystem.RegexEscape) {
			Include("glib.h");
			Write("g_regex_escape_string(");
			args[0].Accept(this, CiPriority.Statement);
			Write(", -1)");
		}
		else if (method == CiSystem.RegexIsMatchStr) {
			Include("glib.h");
			Write("g_regex_match_simple(");
			args[1].Accept(this, CiPriority.Statement);
			Write(", ");
			args[0].Accept(this, CiPriority.Statement);
			Write(", ");
			WriteRegexOptions(args);
			Write(", 0)");
		}
		else if (method == CiSystem.RegexIsMatchRegex) {
			Write("g_regex_match(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			args[0].Accept(this, CiPriority.Statement);
			Write(", 0, NULL)");
		}
		else if (method == CiSystem.MatchFindStr) {
			this.MatchFind = true;
			Write("CiMatch_Find(&");
			obj.Accept(this, CiPriority.Primary);
			Write(", ");
			args[0].Accept(this, CiPriority.Statement);
			Write(", ");
			args[1].Accept(this, CiPriority.Statement);
			Write(", ");
			WriteRegexOptions(args);
			Write(')');
		}
		else if (method == CiSystem.MatchFindRegex) {
			Write("g_regex_match(");
			args[1].Accept(this, CiPriority.Statement);
			Write(", ");
			args[0].Accept(this, CiPriority.Statement);
			Write(", 0, &");
			obj.Accept(this, CiPriority.Primary);
			Write(')');
		}
		else if (method == CiSystem.MatchGetCapture)
			WriteCall("g_match_info_fetch", obj, args[0]);
		else if (method == CiSystem.ConsoleWrite)
			WriteConsoleWrite(obj, args, false);
		else if (method == CiSystem.ConsoleWriteLine)
			WriteConsoleWrite(obj, args, true);
		else if (method == CiSystem.EnvironmentGetEnvironmentVariable)
			WriteCall("getenv", args[0]);
		else if (obj.IsReferenceTo(CiSystem.MathClass)) {
			Include("math.h");
			WriteMathCall(method, args);
		}
		// TODO
		else if (obj.IsReferenceTo(CiSystem.BasePtr)) {
			WriteName(method);
			Write("(&self->base");
			WriteArgsAndRightParenthesis(method, args);
		}
		else
			WriteCCall(obj, method, args);
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Left.Type) {
		case CiListType list:
			if (list.ElementType is CiArrayStorageType) {
				Write('(');
				WriteDynamicArrayCast(list.ElementType);
				expr.Left.Accept(this, CiPriority.Primary);
				Write("->data)[");
				expr.Right.Accept(this, CiPriority.Statement);
				Write(']');
			}
			else {
				Write("g_array_index(");
				expr.Left.Accept(this, CiPriority.Statement);
				Write(", ");
				Write(list.ElementType, false);
				Write(", ");
				expr.Right.Accept(this, CiPriority.Statement);
				Write(')');
			}
			break;
		case CiDictionaryType dict:
			string function = dict is CiSortedDictionaryType ? "g_tree_lookup" : "g_hash_table_lookup";
			if (dict.ValueType is CiIntegerType && dict.ValueType != CiSystem.LongType) {
				Write("GPOINTER_TO_INT(");
				WriteDictionaryLookup(expr.Left, function, expr.Right);
				Write(')');
			}
			else {
				if (parent > CiPriority.Mul)
					Write('(');
				if (dict.ValueType is CiClass || dict.ValueType is CiArrayStorageType)
					WriteDynamicArrayCast(dict.ValueType);
				else {
					Write('(');
					Write(dict.ValueType, false);
					Write(") ");
				}
				WriteDictionaryLookup(expr.Left, function, expr.Right);
				if (parent > CiPriority.Mul)
					Write(')');
			}
			break;
		default:
			base.WriteIndexing(expr, parent);
			break;
		}
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
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
		case CiToken.Assign:
			if (expr.Left.Type == CiSystem.StringStorageType) {
				if (parent == CiPriority.Statement
				 && IsTrimSubstring(expr) is CiExpr length) {
					WriteIndexing(expr.Left, length);
					Write(" = '\\0'");
					return expr;
				}
				this.StringAssign = true;
				Write("CiString_Assign(&");
				expr.Left.Accept(this, CiPriority.Primary);
				Write(", ");
				WriteStringStorageValue(expr.Right);
				Write(')');
				return expr;
			}
			else if (expr.Left.Type.IsDynamicPtr) {
				if (expr.Left.Type.IsClass(CiSystem.RegexClass)) {
					// TODO: only if previously assigned non-null
					// Write("g_regex_unref(");
					// expr.Left.Accept(this, CiPriority.Statement);
					// WriteLine(");");
				}
				else {
					this.SharedAssign = true;
					Write("CiShared_Assign((void **) &");
					expr.Left.Accept(this, CiPriority.Primary);
					Write(", ");
					if (expr.Right is CiSymbolReference) {
						this.SharedAddRef = true;
						Write("CiShared_AddRef(");
						expr.Right.Accept(this, CiPriority.Statement);
						Write(')');
					}
					else
						expr.Right.Accept(this, CiPriority.Statement);
					Write(')');
					return expr;
				}
			}
			break;
		case CiToken.AddAssign:
			if (expr.Left.Type == CiSystem.StringStorageType) {
				if (expr.Right is CiInterpolatedString rightInterpolated) {
					this.StringAssign = true;
					Write("CiString_Assign(&");
					expr.Left.Accept(this, CiPriority.Primary);
					Write(", ");
					CiInterpolatedPart[] parts = new CiInterpolatedPart[1 + rightInterpolated.Parts.Length];
					parts[0] = new CiInterpolatedPart("", expr.Left); // TODO: side effect
					rightInterpolated.Parts.CopyTo(parts, 1);
					Visit(new CiInterpolatedString(parts, rightInterpolated.Suffix), CiPriority.Statement);
				}
				else {
					Include("string.h");
					this.StringAppend = true;
					Write("CiString_Append(&");
					expr.Left.Accept(this, CiPriority.Primary);
					Write(", ");
					expr.Right.Accept(this, CiPriority.Statement);
				}
				Write(')');
				return expr;
			}
			break;
		default:
			break;
		}

		return base.Visit(expr, parent);
	}

	protected override void WriteResource(string name, int length)
	{
		Write("CiResource_");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	static CiMethod GetThrowingMethod(CiStatement statement)
	{
		switch (statement) {
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
		switch (throwingMethod.Type) {
		case null:
			Write('!');
			source(CiPriority.Primary);
			break;
		case CiIntegerType _:
			source(CiPriority.Equality);
			Write(" == -1");
			break;
		case CiNumericType _:
			IncludeMath();
			Write("isnan(");
			source(CiPriority.Statement);
			Write(')');
			break;
		default:
			source(CiPriority.Equality);
			Write(" == NULL");
			break;
		}
		Write(')');
		if (this.VarsToDestruct.Count > 0) {
			Write(' ');
			OpenBlock();
			Visit((CiThrow) null);
			CloseBlock();
		}
		else {
			WriteLine();
			this.Indent++;
			Visit((CiThrow) null);
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
			Write(nesting);
			Write(" = ");
			Write(array.Length - 1);
			Write("; _i");
			Write(nesting);
			Write(" >= 0; _i");
			Write(nesting);
			WriteLine("--)");
			this.Indent++;
			nesting++;
			type = array.ElementType;
		}
		if (type is CiClass klass) {
			if (klass == CiSystem.MatchClass)
				Write("g_match_info_free(");
			else {
				WriteName(klass);
				Write("_Destruct(&");
			}
		}
		else if (type.IsDynamicPtr) {
			if (type.IsClass(CiSystem.RegexClass))
				Write("g_regex_unref(");
			else {
				this.SharedRelease = true;
				Write("CiShared_Release(");
			}
		}
		else if (type is CiListType)
			Write("g_array_free(");
		else if (type is CiDictionaryType)
			Write(type is CiSortedDictionaryType ? "g_tree_unref(" : "g_hash_table_unref(");
		else
			Write("free(");
		WriteLocalName(symbol, CiPriority.Primary);
		for (int i = 0; i < nesting; i++) {
			Write("[_i");
			Write(i);
			Write(']');
		}
		if (type is CiListType)
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

	public override void Visit(CiBlock statement)
	{
		OpenBlock();
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
		case CiBreak brk:
			return BreakOrContinueNeedsBlock(brk.LoopOrSwitch);
		case CiContinue cont:
			return BreakOrContinueNeedsBlock(cont.Loop);
		case CiReturn _:
		case CiThrow _:
			return this.VarsToDestruct.Count > 0;
		default:
			return GetThrowingMethod(statement) != null;
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

	public override void Visit(CiBreak statement)
	{
		WriteDestructLoopOrSwitch(statement.LoopOrSwitch);
		base.Visit(statement);
	}

	public override void Visit(CiContinue statement)
	{
		WriteDestructLoopOrSwitch(statement.Loop);
		base.Visit(statement);
	}

	public override void Visit(CiExpr statement)
	{
		CiMethod throwingMethod = GetThrowingMethod(statement);
		if (throwingMethod != null)
			WriteForwardThrow(parent => statement.Accept(this, parent), throwingMethod);
		else if (statement is CiCallExpr && statement.Type == CiSystem.StringStorageType) {
			Write("free(");
			statement.Accept(this, CiPriority.Statement);
			WriteLine(");");
		}
		else if (statement is CiCallExpr && statement.Type != null && statement.Type.IsDynamicPtr) {
			this.SharedRelease = true;
			Write("CiShared_Release(");
			statement.Accept(this, CiPriority.Statement);
			WriteLine(");");
		}
		else
			base.Visit(statement);
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
			Write('(');
			Write(iter.Type, false);
			Write(") ");
			Write(value);
		}
		WriteLine(';');
	}

	public override void Visit(CiForeach statement)
	{
		string element = statement.Element.Name;
		switch (statement.Collection.Type) {
		case CiArrayStorageType array:
			Write("for (int ");
			WriteCamelCaseNotKeyword(element);
			Write(" = 0; ");
			WriteCamelCaseNotKeyword(element);
			Write(" < ");
			Write(array.Length);
			Write("; ");
			WriteCamelCaseNotKeyword(element);
			Write("++)");
			WriteChild(statement.Body);
			break;
		case CiListType list:
			Write("for (");
			CiType elementType = list.ElementType;
			Write(elementType, false);
			Write(" const *");
			WriteCamelCaseNotKeyword(element);
			Write(" = (");
			Write(elementType, false);
			Write(" const *) ");
			statement.Collection.Accept(this, CiPriority.Primary);
			Write("->data, ");
			for (; elementType is CiArrayType array; elementType = array.ElementType)
				Write('*');
			if (elementType is CiStringType || elementType is CiClassPtrType)
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
		case CiSortedDictionaryType dict:
			OpenBlock();
			Write("for (GTreeNode *cidictit = g_tree_node_first(");
			statement.Collection.Accept(this, CiPriority.Statement);
			Write("); cidictit != NULL; cidictit = g_tree_node_next(cidictit)) ");
			OpenBlock();
			WriteDictIterVar(statement.Element, "g_tree_node_key(cidictit)");
			WriteDictIterVar(statement.ValueVar, "g_tree_node_value(cidictit)");
			FlattenBlock(statement.Body);
			CloseBlock();
			CloseBlock();
			break;
		case CiDictionaryType dict:
			OpenBlock();
			WriteLine("GHashTableIter cidictit;");
			Write("g_hash_table_iter_init(&cidictit, ");
			statement.Collection.Accept(this, CiPriority.Statement);
			WriteLine(");");
			WriteLine("gpointer cikey, civalue;");
			Write("while (g_hash_table_iter_next(&cidictit, &cikey, &civalue)) ");
			OpenBlock();
			WriteDictIterVar(statement.Element, "cikey");
			WriteDictIterVar(statement.ValueVar, "civalue");
			FlattenBlock(statement.Body);
			CloseBlock();
			CloseBlock();
			break;
		default:
			throw new NotImplementedException(statement.Collection.Type.ToString());
		}
	}

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null) {
			WriteDestructAll();
			WriteLine(this.CurrentMethod.Throws ? "return true;" : "return;");
		}
		else if (this.VarsToDestruct.Count == 0 || statement.Value is CiLiteral) {
			WriteDestructAll();
			base.Visit(statement);
		}
		else {
			if (statement.Value is CiSymbolReference symbol) {
				if (this.VarsToDestruct.Contains(symbol.Symbol)) {
					// Optimization: avoid copy
					WriteDestructAll(symbol.Symbol);
					Write("return ");
					if (this.CurrentMethod.Type is CiClassPtrType resultPtr)
						WriteClassPtr(resultPtr.Class, symbol, CiPriority.Statement); // upcast, but don't AddRef
					else
						symbol.Accept(this, CiPriority.Statement);
					WriteLine(';');
					return;
				}
				if (symbol.Left == null) {
					// Local variable value doesn't depend on destructed variables
					WriteDestructAll();
					base.Visit(statement);
					return;
				}
			}
			WriteDefinition(this.CurrentMethod.Type, () => Write("returnValue"), true, true);
			Write(" = ");
			WriteCoerced(this.CurrentMethod.Type, statement.Value, CiPriority.Statement);
			WriteLine(';');
			WriteDestructAll();
			WriteLine("return returnValue;");
		}
	}

	protected override void WriteCaseBody(CiStatement[] statements)
	{
		if (statements[0] is CiVar
		 || (statements[0] is CiConst konst && konst.Type is CiArrayType))
			WriteLine(';');
		int varsToDestructCount = this.VarsToDestruct.Count;
		Write(statements);
		TrimVarsToDestruct(varsToDestructCount);
	}

	void WriteThrowReturnValue()
	{
		switch (this.CurrentMethod.Type) {
		case null:
			Write("false");
			break;
		case CiIntegerType _:
			Write("-1");
			break;
		case CiNumericType _:
			IncludeMath();
			Write("NAN");
			break;
		default:
			Write("NULL");
			break;
		}
	}

	public override void Visit(CiThrow statement)
	{
		WriteDestructAll();
		Write("return ");
		WriteThrowReturnValue();
		WriteLine(';');
	}

	bool TryWriteCallAndReturn(CiStatement[] statements, int lastCallIndex, CiExpr returnValue)
	{
		if (this.VarsToDestruct.Count > 0)
			return false;
		CiExpr call = statements[lastCallIndex] as CiExpr;
		CiMethod throwingMethod = GetThrowingMethod(call);
		if (throwingMethod == null)
			return false;
		Write(statements, lastCallIndex);
		Write("return ");
		switch (throwingMethod.Type) {
		case null:
			call.Accept(this, CiPriority.Select);
			break;
		case CiIntegerType _:
			call.Accept(this, CiPriority.Equality);
			Write(" != -1");
			break;
		case CiNumericType _:
			IncludeMath();
			Write("!isnan(");
			call.Accept(this, CiPriority.Statement);
			Write(')');
			break;
		default:
			call.Accept(this, CiPriority.Equality);
			Write(" != NULL");
			break;
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

	protected override void Write(CiStatement[] statements)
	{
		int i = statements.Length - 2;
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
			if (konst.Value != null) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Statement);
			}
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

	void WriteSignature(CiClass klass, CiMethod method)
	{
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
		foreach (CiMethod method in klass.Methods) {
			if (method.IsAbstractOrVirtual()) {
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

	protected override void WriteConst(CiConst konst)
	{
		if (konst.Type is CiArrayType) {
			Write("static const ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine(';');
		}
		else if (konst.Visibility == CiVisibility.Public) {
			Write("#define ");
			WriteName(konst);
			Write(' ');
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine();
		}
	}

	static bool HasVtblValue(CiClass klass)
	{
		if (klass.CallType == CiCallType.Static || klass.CallType == CiCallType.Abstract)
			return false;
		return klass.Methods.Any(method => method.CallType == CiCallType.Virtual || method.CallType == CiCallType.Override || method.CallType == CiCallType.Sealed);
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
		return klass.Fields.Any(field => NeedToDestruct(field))
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
		foreach (CiConst konst in klass.Consts) {
			if ((konst.Visibility == CiVisibility.Public) == pub) {
				if (pub) {
					WriteLine();
					Write(konst.Documentation);
				}
				WriteConst(konst);
			}
		}
		foreach (CiMethod method in klass.Methods) {
			if (method.IsLive && (method.Visibility == CiVisibility.Public) == pub && method.CallType != CiCallType.Abstract) {
				WriteLine();
				WriteDoc(method);
				WriteSignature(klass, method);
				WriteLine(';');
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
				throw new CiException(klass, "Circular dependency for class {0}", klass.Name);
			}
			this.WrittenClasses.Add(klass, false);
			if (klass.Parent is CiClass baseClass)
				WriteStruct(baseClass);
			foreach (CiField field in klass.Fields)
				if (field.Type.BaseType is CiClass fieldClass)
					WriteStruct(fieldClass);
			this.WrittenClasses[klass] = true;

			WriteLine();
			if (klass.AddsVirtualMethods())
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
			foreach (CiField field in klass.Fields) {
				WriteTypeAndName(field);
				WriteLine(';');
			}
			this.Indent--;
			WriteLine("};");
		}
		if (NeedsConstructor(klass)) {
			WriteXstructorSignature("Construct", klass);
			WriteLine(';');
		}
		if (NeedsDestructor(klass)) {
			WriteXstructorSignature("Destruct", klass);
			WriteLine(';');
		}
		WriteSignatures(klass, false);
	}

	void WriteVtbl(CiClass definingClass, CiClass declaringClass)
	{
		if (declaringClass.Parent is CiClass baseClass)
			WriteVtbl(definingClass, baseClass);
		foreach (CiMethod declaredMethod in declaringClass.Methods) {
			if (declaredMethod.IsAbstractOrVirtual()) {
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
		foreach (CiField field in klass.Fields)
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
		foreach (CiField field in klass.Fields.Reverse())
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

	protected void Write(CiClass klass, CiMethod method)
	{
		if (!method.IsLive || method.CallType == CiCallType.Abstract)
			return;
		WriteLine();
		WriteSignature(klass, method);
		foreach (CiVar param in method.Parameters) {
			if (NeedToDestruct(param))
				this.VarsToDestruct.Add(param);
		}
		WriteLine();
		this.CurrentMethod = method;
		OpenBlock();
		if (method.Body is CiBlock block) {
			CiStatement[] statements = block.Statements;
			if (!block.CompletesNormally)
				Write(statements);
			else if (method.Throws && method.Type == null) {
				if (statements.Length == 0 || !TryWriteCallAndReturn(statements, statements.Length - 1, null)) {
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
		if (this.StringStartsWith) {
			WriteLine();
			WriteLine("static bool CiString_StartsWith(const char *str, const char *prefix)");
			OpenBlock();
			WriteLine("return memcmp(str, prefix, strlen(prefix)) == 0;");
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
		if (this.PtrFree) {
			WriteLine();
			WriteLine("static void CiPtr_Free(void *ptr)");
			OpenBlock();
			WriteLine("free(*(void **) ptr);");
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
		if (this.SharedRelease || this.SharedReleaseIndirect || this.SharedAssign) {
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
		if (this.SharedReleaseIndirect) {
			WriteLine();
			WriteLine("static void CiShared_ReleaseIndirect(void *ptr)");
			OpenBlock();
			WriteLine("CiShared_Release(*(void **) ptr);");
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
			Write("static int CiTree_CompareInteger(gconstpointer a, gconstpointer b, gpointer user_data)");
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
	}

	protected void WriteResources(Dictionary<string, byte[]> resources)
	{
		if (resources.Count == 0)
			return;
		WriteLine();
		Include("stdint.h");
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			Write("static const uint8_t ");
			WriteResource(name, -1);
			Write('[');
			Write(resources[name].Length);
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
		this.StringStartsWith = false;
		this.StringEndsWith = false;
		this.StringFormat = false;
		this.MatchFind = false;
		this.MatchPos = false;
		this.PtrConstruct = false;
		this.PtrFree = false;
		this.SharedMake = false;
		this.SharedAddRef = false;
		this.SharedRelease = false;
		this.SharedReleaseIndirect = false;
		this.SharedAssign = false;
		this.TreeCompareInteger = false;
		this.TreeCompareString = false;
		this.Compares.Clear();
		OpenStringWriter();
		foreach (CiClass klass in program.Classes)
			WriteStruct(klass);
		WriteResources(program.Resources);
		foreach (CiClass klass in program.Classes) {
			this.CurrentClass = klass;
			WriteConstructor(klass);
			WriteDestructor(klass);
			WriteNewDelete(klass, true);
			foreach (CiMethod method in klass.Methods)
				Write(klass, method);
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
