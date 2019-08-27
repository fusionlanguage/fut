// GenC.cs - C code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
//
// This file is part of CiTo, see http://cito.sourceforge.net
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
	readonly List<CiVar> VarsToDestruct = new List<CiVar>();

	protected override void IncludeStdInt()
	{
		Include("stdint.h");
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

	void WriteDefinition(CiType type, Action symbol)
	{
		if (type == null) {
			Write("void ");
			symbol();
			return;
		}
		CiType baseType = type.BaseType;
		switch (baseType) {
		case CiIntegerType integer:
			Write(GetTypeCode(integer, type == baseType));
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
			WriteDefinition(method.Type, symbol);
	}

	protected override void Write(CiType type, bool promote)
	{
		WriteDefinition(type, () => {});
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteDefinition(value.Type, () => WriteName(value));
	}

	static bool IsStringSubstring(CiExpr expr, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr length)
	{
		if (expr is CiBinaryExpr call
		 && call.Op == CiToken.LeftParenthesis
		 && call.Left is CiBinaryExpr leftBinary
		 && leftBinary.Op == CiToken.Dot) {
			CiMethod method = (CiMethod) ((CiSymbolReference) leftBinary.Right).Symbol;
			CiExpr[] args = call.RightCollection;
			if (method == CiSystem.StringSubstring) {
				cast = false;
				ptr = leftBinary.Left;
				offset = args[0];
				length = args[1];
				return true;
			}
			if (method == CiSystem.UTF8GetString) {
				cast = true;
				ptr = args[0];
				offset = args[1];
				length = args[2];
				return true;
			}
		}
		cast = false;
		ptr = null;
		offset = null;
		length = null;
		return false;
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

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Type == CiSystem.StringStorageType) {
			Write(" = ");
			if (def.Value == null)
				Write("NULL");
			else
				WriteStringStorageValue(def.Value);
		}
		else
			base.WriteVarInit(def);
	}

	protected override void WriteVar(CiNamedValue def)
	{
		base.WriteVar(def);
		if (def.Type == CiSystem.StringStorageType || def.Type is CiClass)
			this.VarsToDestruct.Add((CiVar) def);
	}

	protected override void WriteLiteral(object value)
	{
		if (value == null)
			Write("NULL");
		else
			base.WriteLiteral(value);
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

	protected override void WriteStringLength(CiExpr expr)
	{
		Include("string.h");
		Write("(int) strlen(");
		expr.Accept(this, CiPriority.Primary);
		Write(')');
	}

	void WriteStringMethod(string name, CiExpr obj, CiExpr[] args)
	{
		Include("string.h");
		Write("CiString_");
		Write(name);
		Write('(');
		obj.Accept(this, CiPriority.Primary);
		Write(", ");
		args[0].Accept(this, CiPriority.Primary);
		Write(')');
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
		else if (IsMathReference(obj)) {
			Include("math.h");
			if (method.Name == "Ceiling")
				Write("ceil");
			else
				WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.StringContains) {
			Include("string.h");
			if (parent > CiPriority.Equality)
				Write('(');
			Write("strstr(");
			obj.Accept(this, CiPriority.Primary);
			Write(", ");
			args[0].Accept(this, CiPriority.Primary);
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
			this.StringStartsWith = true;
			WriteStringMethod("StartsWith", obj, args);
		}
		else if (method == CiSystem.StringEndsWith) {
			this.StringEndsWith = true;
			WriteStringMethod("EndsWith", obj, args);
		}
		// TODO
		else {
			switch (method.CallType) {
			case CiCallType.Abstract:
			case CiCallType.Virtual:
			case CiCallType.Override:
				if (!(obj.Type is CiClass klass))
					klass = ((CiClassPtrType) obj.Type).Class;
				CiClass ptrClass = GetVtblPtrClass(klass);
				CiClass structClass = GetVtblStructClass((CiClass) method.Parent);
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
			if (method.CallType != CiCallType.Static) {
				WriteClassPtr((CiClass) method.Parent, obj, CiPriority.Statement);
				if (args.Length > 0)
					Write(", ");
			}
			WriteArgs(method, args);
			Write(')');
		}
	}

	protected override void WriteNearCall(CiMethod method, CiExpr[] args)
	{
		CiClass klass = (CiClass) this.CurrentMethod.Parent;
		switch (method.CallType) {
		case CiCallType.Abstract:
		case CiCallType.Virtual:
		case CiCallType.Override:
			CiClass ptrClass = GetVtblPtrClass(klass);
			CiClass structClass = GetVtblStructClass((CiClass) method.Parent);
			if (structClass != ptrClass) {
				Write("((const ");
				Write(structClass.Name);
				Write("Vtbl *) ");
			}
			Write("self->");
			for (CiClass baseClass = klass; baseClass != ptrClass; baseClass = (CiClass) baseClass.Parent)
				Write("base.");
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
			CiClass resultClass = (CiClass) method.Parent;
			if (klass == resultClass)
				Write("self");
			else {
				Write("&self->base");
				for (klass = (CiClass) klass.Parent; klass != resultClass; klass = (CiClass) klass.Parent)
					Write(".base");
			}
			if (args.Length > 0)
				Write(", ");
		}
		WriteArgs(method, args);
		Write(')');
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left.Type == CiSystem.StringStorageType) {
			switch (expr.Op) {
			case CiToken.Assign:
				this.StringAssign = true;
				Write("CiString_Assign(&");
				expr.Left.Accept(this, CiPriority.Primary);
				Write(", ");
				WriteStringStorageValue(expr.Right);
				Write(')');
				return expr;
			case CiToken.AddAssign:
				Include("string.h");
				this.StringAppend = true;
				Write("CiString_Append(&");
				expr.Left.Accept(this, CiPriority.Primary);
				Write(", ");
				expr.Right.Accept(this, CiPriority.Statement);
				Write(')');
				return expr;
			default:
				break;
			}
		}
		return base.Visit(expr, parent);
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Symbol is CiField)
			Write("self->");
		WriteName(expr.Symbol);
		return expr;
	}

	protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		// TODO
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		return def.Value != null
			|| (def.Type is CiClass klass && NeedsConstructor(klass));
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (def.Type is CiClass klass) {
			if (NeedsConstructor(klass)) {
				Write(klass.Name);
				Write("_Construct(&");
				Write(def.Name);
				WriteLine(");");
			}
		}
		else if (def.Type is CiArrayStorageType array) {
			if (def.Value != null) {
				if (!(def.Value is CiLiteral literal) || !literal.IsDefaultValue)
					throw new NotImplementedException("Only null, zero and false supported");
				Include("string.h");
				Write("memset(");
				WriteName(def);
				Write(", 0, sizeof(");
				WriteName(def);
				WriteLine("));");
			}
			else if (array.ElementType is CiClass elementClass) {
				Write("for (size_t _i = 0; _i < ");
				Write(array.Length);
				WriteLine("; _i++)");
				Write('\t');
				Write(elementClass.Name);
				Write("_Construct(");
				Write(def.Name);
				WriteLine(" + _i);");
			}
		}
		else {
			CiMethod throwingMethod = GetThrowingMethod(def.Value);
			if (throwingMethod != null)
				WriteForwardThrow(parent => Write(def.Name), throwingMethod);
		}
	}

	protected override void WriteResource(string name, int length)
	{
		Write("CiResource_");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
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

	static CiMethod GetThrowingMethod(CiStatement statement)
	{
		if (!(statement is CiBinaryExpr binary))
			return null;
		switch (binary.Op) {
		case CiToken.Assign:
			return GetThrowingMethod(binary.Right);
		case CiToken.LeftParenthesis:
			CiSymbolReference symbol = (CiSymbolReference) (binary.Left is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.Dot ? leftBinary.Right : binary.Left);
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

	void WriteDestruct(CiVar def)
	{
		if (def.Type == CiSystem.StringStorageType) {
			Write("free(");
			Write(def.Name);
			WriteLine(");");
		}
		else if (def.Type is CiClass klass) {
			Write(klass.Name);
			Write("_Destruct(&");
			Write(def.Name);
			WriteLine(");");
		}
	}

	void WriteDestructAll()
	{
		for (int i = this.VarsToDestruct.Count; --i >= 0; )
			WriteDestruct(this.VarsToDestruct[i]);
	}

	void WriteDestructLoop(CiLoop loop)
	{
		if (!(loop.Body is CiBlock block))
			return;
		for (int i = this.VarsToDestruct.Count; --i >= 0; ) {
			CiVar def = this.VarsToDestruct[i];
			if (!block.Encloses(def))
				break;
			WriteDestruct(def);
		}
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
		this.VarsToDestruct.RemoveRange(i, this.VarsToDestruct.Count - i);
		CloseBlock();
	}

	bool NeedsBlock(CiStatement statement)
	{
		switch (statement) {
		case CiContinue cont:
			int count = this.VarsToDestruct.Count;
			return count > 0 && cont.What.Body is CiBlock block && block.Encloses(this.VarsToDestruct[count - 1]);
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

	public override void Visit(CiContinue statement)
	{
		WriteDestructLoop(statement.What);
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

	public override void Visit(CiReturn statement)
	{
		WriteDestructAll(); // TODO: referenced in the return value
		if (statement.Value == null && this.CurrentMethod.Throws)
			WriteLine("return true;");
		else
			base.Visit(statement);
	}

	protected override void WriteCaseBody(CiStatement[] statements)
	{
		if (statements[0] is CiVar
		 || (statements[0] is CiConst konst && konst.Type is CiArrayType))
			WriteLine(";");
		Write(statements);
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
		WriteLine(";");
	}

	bool TryWriteCallAndReturn(CiStatement[] statements, int lastCallIndex, CiExpr returnValue)
	{
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
		WriteLine(";");
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
		Write("typedef enum ");
		OpenBlock();
		bool first = true;
		foreach (CiConst konst in enu) {
			if (!first)
				WriteLine(",");
			first = false;
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
		WriteLine(";");
	}

	void WriteTypedef(CiClass klass)
	{
		if (klass.CallType == CiCallType.Static)
			return;
		Write("typedef struct ");
		Write(klass.Name);
		Write(' ');
		Write(klass.Name);
		WriteLine(";");
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
		WriteParameters(method, false);
	}

	void WriteSignature(CiClass klass, CiMethod method)
	{
		if (method.Visibility == CiVisibility.Private || method.Visibility == CiVisibility.Internal)
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
				WriteParameters(method);
		});
	}

	protected override void WriteMethodBody(CiBlock block)
	{
		if (this.CurrentMethod.Throws && this.CurrentMethod.Type == null && block.CompletesNormally) {
			OpenBlock();
			CiStatement[] statements = block.Statements;
			if (!TryWriteCallAndReturn(statements, statements.Length - 1, null)) {
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

	static bool AddsVirtualMethods(CiClass klass)
	{
		return klass.Methods.Any(method => method.CallType == CiCallType.Abstract || method.CallType == CiCallType.Virtual);
	}

	static CiClass GetVtblStructClass(CiClass klass)
	{
		while (!AddsVirtualMethods(klass))
			klass = (CiClass) klass.Parent;
		return klass;
	}

	static CiClass GetVtblPtrClass(CiClass klass)
	{
		for (CiClass result = null;;) {
			if (AddsVirtualMethods(klass))
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
			if (method.CallType == CiCallType.Abstract || method.CallType == CiCallType.Virtual) {
				WriteSignature(method, () => {
					Write("(*");
					WriteCamelCase(method.Name);
					Write(')');
					WriteInstanceParameters(method);
				});
				WriteLine(";");
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
			WriteLine(";");
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
		return klass.Methods.Any(method => method.CallType == CiCallType.Virtual || method.CallType == CiCallType.Override);
	}

	protected override bool NeedsConstructor(CiClass klass)
	{
		return base.NeedsConstructor(klass)
			|| HasVtblValue(klass)
			|| (klass.Parent is CiClass baseClass && NeedsConstructor(baseClass));
	}

	bool NeedsDestructor(CiClass klass)
	{
		return klass.Fields.Any(field => field.Type == CiSystem.StringStorageType || (field.Type is CiClass fieldClass && NeedsDestructor(fieldClass)))
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
		foreach (CiConst konst in klass.Consts)
			if ((konst.Visibility == CiVisibility.Public) == pub)
				WriteConst(konst);
		foreach (CiMethod method in klass.Methods) {
			if ((method.Visibility == CiVisibility.Public) == pub && method.CallType != CiCallType.Abstract) {
				WriteSignature(klass, method);
				WriteLine(";");
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
			if (AddsVirtualMethods(klass))
				WriteVtblStruct(klass);
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
				WriteLine(";");
			}
			this.Indent--;
			WriteLine("};");
		}
		if (NeedsConstructor(klass)) {
			WriteXstructorSignature("Construct", klass);
			WriteLine(";");
		}
		if (NeedsDestructor(klass)) {
			WriteXstructorSignature("Destruct", klass);
			WriteLine(";");
		}
		WriteSignatures(klass, false);
	}

	void WriteVtbl(CiClass definingClass, CiClass declaringClass)
	{
		if (declaringClass.Parent is CiClass baseClass)
			WriteVtbl(definingClass, baseClass);
		foreach (CiMethod declaredMethod in declaringClass.Methods) {
			if (declaredMethod.CallType == CiCallType.Abstract || declaredMethod.CallType == CiCallType.Virtual) {
				CiSymbol definedMethod = definingClass.TryLookup(declaredMethod.Name);
				if (declaredMethod != definedMethod) {
					Write('(');
					WriteDefinition(declaredMethod.Type, () => {
						Write("(*)");
						WriteInstanceParameters(declaredMethod);
					});
					Write(") ");
				}
				WriteName(definedMethod);
				WriteLine(",");
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
			Write("self->");
			CiClass ptrClass = GetVtblPtrClass(klass);
			for (CiClass tempClass = klass; tempClass != ptrClass; tempClass = (CiClass) tempClass.Parent)
				Write("base.");
			Write("vtbl = ");
			if (ptrClass != structClass) {
				Write("(const ");
				Write(ptrClass.Name);
				Write("Vtbl *) ");
			}
			WriteLine("&vtbl;");
		}
		foreach (CiField field in klass.Fields) {
			if (field.Value != null || field.Type == CiSystem.StringStorageType) {
				Write("self->");
				WriteCamelCase(field.Name);
				WriteVarInit(field);
				WriteLine(";");
			}
			else if (field.Type is CiClass fieldClass && NeedsConstructor(fieldClass)) {
				Write(fieldClass.Name);
				Write("_Construct(&self->");
				WriteCamelCase(field.Name);
				WriteLine(");");
			}
		}
		if (klass.Constructor != null)
			Write(((CiBlock) klass.Constructor.Body).Statements);
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
		foreach (CiField field in klass.Fields.Reverse()) {
			if (field.Type == CiSystem.StringStorageType) {
				Write("free(self->");
				WriteCamelCase(field.Name);
				WriteLine(");");
			}
			else if (field.Type is CiClass fieldClass && NeedsDestructor(fieldClass)) {
				Write(fieldClass.Name);
				Write("_Destruct(&self->");
				WriteCamelCase(field.Name);
				WriteLine(");");
			}
		}
		if (klass.Parent is CiClass baseClass && NeedsConstructor(baseClass)) {
			Write(baseClass.Name);
			WriteLine("_Destruct(&self->base);");
		}
		CloseBlock();
	}

	void WriteNewDelete(CiClass klass, bool define)
	{
		if (!klass.IsPublic || klass.Constructor == null || klass.Constructor.Visibility != CiVisibility.Public)
			return;

		if (define)
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
			WriteLine(";");

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
			WriteLine(";");
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
			Write(" }");
			WriteLine(";");
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
		WriteLine("}");
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
		OpenStringWriter();
		foreach (CiClass klass in program.Classes)
			WriteStruct(klass);
		WriteResources(program.Resources);
		foreach (CiClass klass in program.Classes) {
			WriteConstructor(klass);
			WriteDestructor(klass);
			WriteNewDelete(klass, true);
			foreach (CiMethod method in klass.Methods) {
				if (method.CallType == CiCallType.Abstract)
					continue;
				WriteLine();
				WriteSignature(klass, method);
				WriteBody(method);
			}
		}

		CreateFile(this.OutputFile);
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
