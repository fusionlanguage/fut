// GenC.cs - C code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
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
	bool PtrConstruct;
	bool SharedMake;
	bool SharedAddRef;
	bool SharedRelease;
	bool SharedAssign;
	readonly List<CiVar> VarsToDestruct = new List<CiVar>();
	CiClass CurrentClass;

	protected override void WriteSelfDoc(CiMethod method)
	{
		if (method.CallType == CiCallType.Static)
			return;
		Write(" * @param self This <code>");
		Write(method.Parent.Name);
		WriteLine("</code>.");
	}

	protected override void IncludeStdInt()
	{
		Include("stdint.h");
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

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiMethod) {
			Write(symbol.Parent.Name);
			Write('_');
			Write(symbol.Name);
		}
		else if (symbol is CiConst) {
			if (symbol.Parent is CiClass) {
				Write(symbol.Parent.Name);
				Write('_');
			}
			WriteUppercaseWithUnderscores(symbol.Name);
		}
		else if (symbol is CiMember)
			WriteCamelCase(symbol.Name);
		else if (symbol.Name == "this")
			Write("self");
		else
			Write(symbol.Name);
	}

	void WriteSelfForField(CiClass fieldClass)
	{
		Write("self->");
		for (CiClass klass = this.CurrentClass; klass != fieldClass; klass = (CiClass) klass.Parent)
			Write("base.");
	}

	protected override void WriteLocalName(CiSymbol symbol)
	{
		if (symbol is CiField)
			WriteSelfForField((CiClass) symbol.Parent);
		WriteName(symbol);
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left == null)
			WriteLocalName(expr.Symbol);
		else
			base.Visit(expr, parent);
		return expr;
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

	void WriteDefinition(CiType type, Action symbol, bool promote)
	{
		if (type == null) {
			Write("void ");
			symbol();
			return;
		}
		CiType baseType = type.BaseType;
		switch (baseType) {
		case CiIntegerType integer:
			Write(GetTypeCode(integer, promote && type == baseType));
			Write(' ');
			break;
		case CiStringPtrType _:
			Write("const char *");
			break;
		case CiStringStorageType _:
			Write("char *");
			break;
		case CiClassPtrType classPtr:
			switch (classPtr.Modifier) {
			case CiToken.EndOfFile:
				Write("const ");
				Write(classPtr.Class.Name);
				Write(" *");
				break;
			case CiToken.ExclamationMark:
			case CiToken.Hash:
				Write(classPtr.Class.Name);
				Write(" *");
				break;
			default:
				throw new NotImplementedException(classPtr.Modifier.ToString());
			}
			break;
		default:
			if (baseType == CiSystem.BoolType)
				Include("stdbool.h");
			Write(baseType.Name);
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
			WriteDefinition(method.Type, symbol, true);
	}

	protected override void Write(CiType type, bool promote)
	{
		WriteDefinition(type, () => {}, promote);
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteDefinition(value.Type, () => WriteName(value), true);
	}

	static bool IsDynamicPtr(CiType type)
	{
		return (type is CiClassPtrType classPtr && classPtr.Modifier == CiToken.Hash)
			|| (type is CiArrayPtrType arrayPtr && arrayPtr.Modifier == CiToken.Hash);
	}

	void WriteXstructorPtr(bool need, CiClass klass, string name)
	{
		if (need) {
			Write("(CiMethodPtr) ");
			Write(klass.Name);
			Write('_');
			Write(name);
		}
		else
			Write("NULL");
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		this.SharedMake = true;
		if (parent > CiPriority.Mul)
			Write('(');
		Write('(');
		WriteDefinition(elementType, () => Write(elementType is CiArrayType ? "(*)" : "*"), false);
		Write(") CiShared_Make(");
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
		else if (IsDynamicPtr(elementType)) {
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
		Include("string.h");
		if (IsStringSubstring(expr, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr length)) {
			this.StringSubstring = true;
			Write("CiString_Substring(");
			if (cast)
				Write("(const char *) ");
			WriteArrayPtrAdd(ptr, offset);
			Write(", ");
			length.Accept(this, CiPriority.Statement);
		}
		else {
			Write("strdup(");
			expr.Accept(this, CiPriority.Statement);
		}
		Write(')');
	}

	protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		switch (value) {
		case null:
			if (array.StorageType is CiStringStorageType || IsDynamicPtr(array.StorageType))
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
		Write(" TODO");
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Type == CiSystem.StringStorageType) {
			Write(" = ");
			if (def.Value == null)
				Write("NULL");
			else
				WriteStringStorageValue(def.Value);
		}
		else if (def.Value == null && IsDynamicPtr(def.Type))
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
			|| IsDynamicPtr(type)
			|| (type is CiClass klass && NeedsDestructor(klass));
	}

	protected override void WriteVar(CiNamedValue def)
	{
		base.WriteVar(def);
		if (NeedToDestruct(def))
			this.VarsToDestruct.Add((CiVar) def);
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		return (def is CiField && (def.Value != null || def.Type.StorageType == CiSystem.StringStorageType || IsDynamicPtr(def.Type)))
			|| GetThrowingMethod(def.Value) != null
			|| (def.Type.StorageType is CiClass klass && NeedsConstructor(klass));
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
				Write(klass.Name);
				Write("_Construct(&");
				WriteArrayElement(def, nesting);
				WriteLine(");");
			}
		}
		else {
			if (def is CiField) {
				WriteArrayElement(def, nesting);
				WriteVarInit(def);
				WriteLine(';');
			}
			CiMethod throwingMethod = GetThrowingMethod(def.Value);
			if (throwingMethod != null)
				WriteForwardThrow(parent => WriteArrayElement(def, nesting), throwingMethod);
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
		if (symbol.Symbol is CiConst) {
			// FIXME
			Write('_');
		}
		else
			WriteMemberAccess(left, (CiClass) symbol.Symbol.Parent);
	}

	void WriteClassPtr(CiClass resultClass, CiExpr expr, CiPriority parent)
	{
		if (expr.Type is CiClass klass) {
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
		if (type is CiClassPtrType resultPtr)
			WriteClassPtr(resultPtr.Class, expr, parent);
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
			}
			else {
				Write("strcmp(");
				expr.Left.Accept(this, CiPriority.Statement);
				Write(", ");
				expr.Right.Accept(this, CiPriority.Statement);
			}
			Write(") ");
			Write(not ? '!' : '=');
			Write("= 0");
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		Include("string.h");
		Write("(int) strlen(");
		expr.Accept(this, CiPriority.Statement);
		Write(')');
	}

	void WriteStringMethod(string name, CiExpr obj, CiExpr[] args)
	{
		Include("string.h");
		Write("CiString_");
		Write(name);
		Write('(');
		obj.Accept(this, CiPriority.Statement);
		Write(", ");
		args[0].Accept(this, CiPriority.Statement);
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

	void WriteConsoleWrite(CiExpr obj, CiExpr[] args, bool newLine)
	{
		bool error = obj is CiSymbolReference symbol && symbol.Symbol == CiSystem.ConsoleError;
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
		else if (newLine && !error) {
			Write("puts(");
			args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else {
			Write("fputs(");
			args[0].Accept(this, CiPriority.Statement);
			Write(error ? ", stderr)" : ", stdout)");
		}
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj.Type is CiArrayType array && method.Name == "CopyTo") {
			Include("string.h");
			Write("memcpy(");
			WriteArrayPtrAdd(args[1], args[2]);
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			if (array.IsByteArray())
				args[3].Accept(this, CiPriority.Statement);
			else {
				args[3].Accept(this, CiPriority.Mul);
				Write(" * sizeof(");
				obj.Accept(this, CiPriority.Primary);
				Write("[0])");
			}
			Write(')');
		}
		else if (obj.Type is CiArrayStorageType && method.Name == "Fill") {
			if (!(args[0] is CiLiteral literal) || !literal.IsDefaultValue)
				throw new NotImplementedException("Only null, zero and false supported");
			Include("string.h");
			Write("memset(");
			obj.Accept(this, CiPriority.Statement);
			Write(", 0, sizeof(");
			obj.Accept(this, CiPriority.Statement);
			Write("))");
		}
		else if (method == CiSystem.ConsoleWrite)
			WriteConsoleWrite(obj, args, false);
		else if (method == CiSystem.ConsoleWriteLine)
			WriteConsoleWrite(obj, args, true);
		else if (IsMathReference(obj)) {
			Include("math.h");
			WriteMathCall(method, args);
		}
		else if (method == CiSystem.StringContains) {
			Include("string.h");
			if (parent > CiPriority.Equality)
				Write('(');
			if (IsOneAsciiString(args[0], out char c)) {
				Write("strchr(");
				obj.Accept(this, CiPriority.Statement);
				Write(", ");
				WriteCharLiteral(c);
			}
			else {
				Write("strstr(");
				obj.Accept(this, CiPriority.Statement);
				Write(", ");
				args[0].Accept(this, CiPriority.Statement);
			}
			Write(") != NULL");
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
		// TODO
		else {
			CiClass definingClass = (CiClass) method.Parent;
			CiClass declaringClass = definingClass;
			switch (method.CallType) {
			case CiCallType.Override:
				declaringClass = (CiClass) method.DeclaringMethod.Parent;
				goto case CiCallType.Abstract;
			case CiCallType.Abstract:
			case CiCallType.Virtual:
				if (!(obj.Type is CiClass klass))
					klass = ((CiClassPtrType) obj.Type).Class;
				CiClass ptrClass = GetVtblPtrClass(klass);
				CiClass structClass = GetVtblStructClass(definingClass);
				if (structClass != ptrClass) {
					Write("((const ");
					Write(structClass.Name);
					Write("Vtbl *) ");
				}
				obj.Accept(this, CiPriority.Primary);
				WriteMemberAccess(obj, ptrClass);
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
			if (method.CallType != CiCallType.Static)
				WriteClassPtr(declaringClass, obj, CiPriority.Statement);
			WriteArgsAndRightParenthesis(method, args);
		}
	}

	protected override void WriteNearCall(CiMethod method, CiExpr[] args)
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
			CiClass ptrClass = GetVtblPtrClass(klass);
			CiClass structClass = GetVtblStructClass(definingClass);
			if (structClass != ptrClass) {
				Write("((const ");
				Write(structClass.Name);
				Write("Vtbl *) ");
			}
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
			if (klass == declaringClass)
				Write("self");
			else {
				Write("&self->base");
				for (klass = (CiClass) klass.Parent; klass != declaringClass; klass = (CiClass) klass.Parent)
					Write(".base");
			}
		}
		WriteArgsAndRightParenthesis(method, args);
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Equal:
		case CiToken.NotEqual:
		case CiToken.Greater:
			if (expr.Left is CiSymbolReference property && property.Symbol == CiSystem.StringLength
			 && expr.Right is CiLiteral literal && (long) literal.Value == 0) {
				property.Left.Accept(this, CiPriority.Primary);
				Write(expr.Op == CiToken.Equal ? "[0] == '\\0'" : "[0] != '\\0'");
				return expr;
			}
			break;
		case CiToken.Assign:
			if (expr.Left.Type == CiSystem.StringStorageType) {
				if (parent == CiPriority.Statement
				 && IsTrimSubstring(expr) is CiExpr length) {
					expr.Left.Accept(this, CiPriority.Primary);
					Write('[');
					length.Accept(this, CiPriority.Statement);
					Write("] = '\\0'");
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
			else if (IsDynamicPtr(expr.Left.Type)) {
				this.SharedAssign = true;
				Write("CiShared_Assign((void **) &");
				expr.Left.Accept(this, CiPriority.Primary);
				Write(", ");
				expr.Right.Accept(this, CiPriority.Statement);
				Write(')');
				return expr;
			}
			break;
		case CiToken.AddAssign:
			if (expr.Left.Type == CiSystem.StringStorageType) {
				Include("string.h");
				this.StringAppend = true;
				Write("CiString_Append(&");
				expr.Left.Accept(this, CiPriority.Primary);
				Write(", ");
				expr.Right.Accept(this, CiPriority.Statement);
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
		if (!(statement is CiBinaryExpr binary))
			return null;
		switch (binary.Op) {
		case CiToken.Assign:
			return GetThrowingMethod(binary.Right);
		case CiToken.LeftParenthesis:
			CiSymbolReference symbol = (CiSymbolReference) binary.Left;
			CiMethod method = (CiMethod) symbol.Symbol;
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
			Include("math.h");
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
			Write(klass.Name);
			Write("_Destruct(&");
		}
		else if (IsDynamicPtr(type)) {
			this.SharedRelease = true;
			Write("CiShared_Release(");
		}
		else
			Write("free(");
		WriteLocalName(symbol);
		for (int i = 0; i < nesting; i++) {
			Write("[_i");
			Write(i);
			Write(']');
		}
		WriteLine(");");
		this.Indent -= nesting;
	}

	void WriteDestructAll()
	{
		for (int i = this.VarsToDestruct.Count; --i >= 0; )
			WriteDestruct(this.VarsToDestruct[i]);
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
			if (def.Parent != statement)
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
		else
			base.Visit(statement);
	}

	public override void Visit(CiForeach statement)
	{
		Write("TODO: foreach");
	}

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null && this.CurrentMethod.Throws) {
			WriteDestructAll();
			WriteLine("return true;");
		}
		else if (this.VarsToDestruct.Count == 0 || statement.Value == null || statement.Value is CiLiteral) {
			WriteDestructAll();
			base.Visit(statement);
		}
		else {
			WriteDefinition(this.CurrentMethod.Type, () => Write("returnValue"), true);
			Write(" = ");
			statement.Value.Accept(this, CiPriority.Statement);
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
			Include("math.h");
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
			call.Accept(this, CiPriority.Cond);
			break;
		case CiIntegerType _:
			call.Accept(this, CiPriority.Equality);
			Write(" != -1");
			break;
		case CiNumericType _:
			Include("math.h");
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
			returnValue.Accept(this, CiPriority.Cond);
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
			Write(enu.Name);
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
		Write(enu.Name);
		WriteLine(';');
	}

	void WriteTypedef(CiClass klass)
	{
		if (klass.CallType == CiCallType.Static)
			return;
		Write("typedef struct ");
		Write(klass.Name);
		Write(' ');
		Write(klass.Name);
		WriteLine(';');
	}

	void WriteTypedefs(CiProgram program, bool pub)
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
		Write(method.Parent.Name);
		Write(" *self");
		WriteParameters(method, false, false);
	}

	void WriteSignature(CiClass klass, CiMethod method)
	{
		if (!klass.IsPublic || method.Visibility != CiVisibility.Public)
			Write("static ");
		WriteSignature(method, () => {
			Write(klass.Name);
			Write("_");
			Write(method.Name);
			if (method.CallType != CiCallType.Static)
				WriteInstanceParameters(method);
			else if (method.Parameters.Count == 0)
				Write("(void)");
			else
				WriteParameters(method, false);
		});
	}

	protected override void WriteMethodBody(CiBlock block)
	{
		if (this.CurrentMethod.Throws && this.CurrentMethod.Type == null && block.CompletesNormally) {
			OpenBlock();
			CiStatement[] statements = block.Statements;
			if (statements.Length == 0 || !TryWriteCallAndReturn(statements, statements.Length - 1, null)) {
				Write(statements);
				WriteDestructAll();
				this.VarsToDestruct.Clear();
				WriteLine("return true;");
			}
			CloseBlock();
		}
		else
			base.WriteMethodBody(block);
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
		Write(klass.Name);
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
		Write(klass.Name);
		Write('_');
		Write(name);
		Write('(');
		Write(klass.Name);
		Write(" *self)");
	}

	void WriteSignatures(CiClass klass, bool pub)
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

	void WriteStruct(CiClass klass)
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
			Write(klass.Name);
			Write(' ');
			OpenBlock();
			if (GetVtblPtrClass(klass) == klass) {
				Write("const ");
				Write(klass.Name);
				WriteLine("Vtbl *vtbl;");
			}
			if (klass.Parent is CiClass) {
				Write(klass.Parent.Name);
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

	void WriteConstructor(CiClass klass)
	{
		if (!NeedsConstructor(klass))
			return;
		WriteLine();
		WriteXstructorSignature("Construct", klass);
		WriteLine();
		OpenBlock();
		if (klass.Parent is CiClass baseClass && NeedsConstructor(baseClass)) {
			Write(baseClass.Name);
			WriteLine("_Construct(&self->base);");
		}
		if (HasVtblValue(klass)) {
			CiClass structClass = GetVtblStructClass(klass);
			Write("static const ");
			Write(structClass.Name);
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
				Write(ptrClass.Name);
				Write("Vtbl *) ");
			}
			WriteLine("&vtbl;");
		}
		foreach (CiField field in klass.Fields)
			WriteInitCode(field);
		WriteConstructorBody(klass);
		CloseBlock();
	}

	void WriteDestructor(CiClass klass)
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
			Write(baseClass.Name);
			WriteLine("_Destruct(&self->base);");
		}
		CloseBlock();
	}

	void WriteNewDelete(CiClass klass, bool define)
	{
		if (!klass.IsPublic || klass.Constructor == null || klass.Constructor.Visibility != CiVisibility.Public)
			return;

		WriteLine();
		Write(klass.Name);
		Write(" *");
		Write(klass.Name);
		Write("_New(void)");
		if (define) {
			WriteLine();
			OpenBlock();
			Write(klass.Name);
			Write(" *self = (");
			Write(klass.Name);
			Write(" *) malloc(sizeof(");
			Write(klass.Name);
			WriteLine("));");
			if (NeedsConstructor(klass)) {
				WriteLine("if (self != NULL)");
				this.Indent++;
				Write(klass.Name);
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
		Write(klass.Name);
		Write("_Delete(");
		Write(klass.Name);
		Write(" *self)");
		if (define) {
			WriteLine();
			OpenBlock();
			if (NeedsDestructor(klass)) {
				WriteLine("if (self == NULL)");
				this.Indent++;
				WriteLine("return;");
				this.Indent--;
				Write(klass.Name);
				WriteLine("_Destruct(self);");
			}
			WriteLine("free(self);");
			CloseBlock();
		}
		else
			WriteLine(';');
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
			WriteLine("static void CiShared_AddRef(void *ptr)");
			OpenBlock();
			WriteLine("if (ptr != NULL)");
			WriteLine("\t((CiShared *) ptr)[-1].refCount++;");
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
	}

	void WriteResources(Dictionary<string, byte[]> resources)
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
		this.PtrConstruct = false;
		this.SharedMake = false;
		this.SharedAddRef = false;
		this.SharedRelease = false;
		this.SharedAssign = false;
		OpenStringWriter();
		foreach (CiClass klass in program.Classes)
			WriteStruct(klass);
		WriteResources(program.Resources);
		foreach (CiClass klass in program.Classes) {
			this.CurrentClass = klass;
			WriteConstructor(klass);
			WriteDestructor(klass);
			WriteNewDelete(klass, true);
			foreach (CiMethod method in klass.Methods) {
				if (!method.IsLive || method.CallType == CiCallType.Abstract)
					continue;
				WriteLine();
				WriteSignature(klass, method);
				WriteBody(method);
			}
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
