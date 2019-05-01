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
using System.IO;

namespace Foxoft.Ci
{

public class GenC : GenCCpp
{
	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiMethod) {
			Write(symbol.Parent.Name);
			Write('_');
			Write(symbol.Name);
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
		CiType baseType = type.BaseType;
		switch (baseType) {
		case null:
			Write("void ");
			break;
		case CiIntegerType integer:
			Write(GetTypeCode(integer, type is CiArrayType));
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

	protected override void Write(CiType type, bool promote)
	{
		WriteDefinition(type, () => {});
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteDefinition(value.Type, () => WriteName(value));
	}

	protected override void WriteLiteral(object value)
	{
		if (value == null)
			Write("NULL");
		else
			base.WriteLiteral(value);
	}

	protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		if (symbol.Symbol is CiConst) {
			// FIXME
			Write('_');
			return;
		}
		if (left.Type is CiClass klass)
			Write('.');
		else {
			Write("->");
			klass = ((CiClassPtrType) left.Type).Class;
		}
		for (CiClass symbolClass = (CiClass) symbol.Symbol.Parent; klass != symbolClass; klass = (CiClass) klass.Parent)
			Write("base.");
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
		Write("(int) strlen(");
		expr.Accept(this, CiPriority.Primary);
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			Write("memcpy(");
			WriteArrayPtrAdd(args[1], args[2]);
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			args[3].Accept(this, CiPriority.Mul);
			Write(" * sizeof(");
			obj.Accept(this, CiPriority.Primary);
			Write("[0]))");
		}
		else if (obj.Type is CiArrayStorageType && method.Name == "Fill") {
			if (!(args[0] is CiLiteral literal) || !literal.IsDefaultValue)
				throw new NotImplementedException("Only null, zero and false supported");
			Write("memset(");
			obj.Accept(this, CiPriority.Statement);
			Write(", 0, sizeof(");
			obj.Accept(this, CiPriority.Statement);
			Write("))");
		}
		else if (IsMathReference(obj)) {
			if (method.Name == "Ceiling")
				Write("ceil");
			else
				WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.StringContains) {
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
		// TODO
		else {
			WriteName(method);
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
		return false; // TODO
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
		if (def.Type is CiArrayStorageType && def.Value != null) {
			if (!(def.Value is CiLiteral literal) || !literal.IsDefaultValue)
				throw new NotImplementedException("Only null, zero and false supported");
			Write("memset(");
			WriteName(def);
			Write(", 0, sizeof(");
			WriteName(def);
			WriteLine("));");
		}
	}

	protected override void WriteResource(string name, int length)
	{
		// TODO
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if ((expr.Left.Type is CiStringType && expr.Right.Type != CiSystem.NullType)
		 || (expr.Right.Type is CiStringType && expr.Left.Type != CiSystem.NullType)) {
			if (parent > CiPriority.Equality)
				Write('(');
			 Write("strcmp(");
			 expr.Left.Accept(this, CiPriority.Statement);
			 Write(", ");
			 expr.Right.Accept(this, CiPriority.Statement);
			 Write(") ");
			 Write(not ? '!' : '=');
			 Write("= 0");
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	protected override void WriteCaseBody(CiStatement[] statements)
	{
		if (statements.Length > 0 && statements[0] is CiVar)
			WriteLine(";");
		Write(statements);
	}

	public override void Visit(CiThrow statement)
	{
		WriteLine("return TODO;"); // TODO
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
			WriteUppercaseWithUnderscores(konst.Name);
			if (konst.Value != null) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Statement);
			}
		}
		WriteLine();
		this.Indent--;
		Write("} ");
		WriteLine(enu.Name);
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

	void WriteSignature(CiClass klass, CiMethod method)
	{
		if (method.Visibility == CiVisibility.Private || method.Visibility == CiVisibility.Internal)
			Write("static ");
		WriteDefinition(method.Type, () => {
			Write(klass.Name);
			Write("_");
			Write(method.Name);
			Write('(');
			if (method.CallType == CiCallType.Static)
				WriteParameters(method, true);
			else {
				if (!method.IsMutator)
					Write("const ");
				Write(klass.Name);
				Write(" *self");
				WriteParameters(method, false);
			}
		});
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
		else {
			Write("#define ");
			WriteName(konst);
			Write(' ');
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine("");
		}
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

			Write("struct ");
			Write(klass.Name);
			Write(' ');
			OpenBlock();
			if (klass.Parent is CiClass) {
				Write(klass.Parent.Name);
				WriteLine(" base;");
			}
			foreach (CiField field in klass.Fields) {
				WriteVar(field);
				WriteLine(";");
			}
			this.Indent--;
			WriteLine("};");
		}
		WriteSignatures(klass, false);
	}

	public override void Write(CiProgram program)
	{
		this.WrittenClasses.Clear();
		string headerFile = Path.ChangeExtension(this.OutputFile, "h");
		CreateFile(headerFile);
		WriteLine("#pragma once");
		WriteLine("#include <stdbool.h>");
		WriteLine("#include <stdint.h>");
		WriteLine("#ifdef __cplusplus");
		WriteLine("extern \"C\" {");
		WriteLine("#endif");
		WriteTypedefs(program, true);
		foreach (CiClass klass in program.Classes)
			WriteSignatures(klass, true);
		WriteLine("#ifdef __cplusplus");
		WriteLine("}");
		WriteLine("#endif");
		CloseFile();

		CreateFile(this.OutputFile);
		WriteLine("#include <math.h>");
		WriteLine("#include <stdlib.h>");
		WriteLine("#include <string.h>");
		Write("#include \"");
		Write(Path.GetFileName(headerFile));
		WriteLine("\"");
		WriteTypedefs(program, false);
		foreach (CiClass klass in program.Classes)
			WriteStruct(klass);
		foreach (CiClass klass in program.Classes) {
			foreach (CiMethod method in klass.Methods) {
				if (method.CallType == CiCallType.Abstract)
					continue;
				WriteLine();
				WriteSignature(klass, method);
				WriteBody(method);
			}
		}
		CloseFile();
	}
}

}
