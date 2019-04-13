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
	protected override void Write(CiType type, bool promote)
	{
		switch (type) {
		case null:
			Write("void");
			break;
		case CiIntegerType integer:
			Write(GetTypeCode(integer, promote));
			break;
		case CiStringPtrType _:
			Write("const char *");
			break;
		case CiStringStorageType _:
			Write("char *");
			break;
		case CiArrayPtrType arrayPtr:
			switch (arrayPtr.Modifier) {
			case CiToken.EndOfFile:
				Write(arrayPtr.ElementType, false);
				Write(" const *");
				break;
			case CiToken.ExclamationMark:
			case CiToken.Hash:
				Write(arrayPtr.ElementType, false);
				Write(" *");
				break;
			default:
				throw new NotImplementedException(arrayPtr.Modifier.ToString());
			}
			break;
		case CiArrayStorageType arrayStorage:
			Write("std::array<"); // TODO
			Write(arrayStorage.ElementType, false);
			Write(", ");
			Write(arrayStorage.Length);
			Write('>');
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
			Write(type.Name);
			break;
		}
	}

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiMember)
			WriteCamelCase(symbol.Name);
		else
			Write(symbol.Name);
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
		if (left.Type is CiClassPtrType)
			Write("->");
		else
			Write('.');
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		Write("(int) strlen(");
		expr.Accept(this, CiPriority.Primary);
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (IsMathReference(obj)) {
			if (method.Name == "Ceiling")
				Write("ceil");
			else
				WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
		}
		// TODO
		else {
			switch (obj.Type) {
			case CiClass klass:
				Write(klass.Name);
				break;
			case CiClassPtrType ptr:
				Write(ptr.Class.Name);
				break;
			default:
				throw new NotImplementedException(obj.Type.ToString());
			}
			Write('_');
			Write(method.Name);
			Write('(');
			obj.Accept(this, CiPriority.Primary);
			if (args.Length > 0) {
				Write(", ");
				WriteArgs(method, args);
			}
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

	protected override void WriteResource(string name, int length)
	{
		// TODO
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
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
			WriteCamelCase(konst.Name);
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
		WriteLine();
		Write(method.Type, true);
		Write(' ');
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
	}

	void WriteSignatures(CiClass klass, bool pub)
	{
		foreach (CiMethod method in klass.Methods) {
			if ((method.Visibility == CiVisibility.Public) == pub && method.CallType != CiCallType.Abstract) {
				WriteSignature(klass, method);
				WriteLine(";");
			}
		}
	}

	void WriteStruct(CiClass klass)
	{
		// topological sorting of class hierarchy and class storage fields
		if (klass == null)
			return;
		if (klass.CallType != CiCallType.Static) {
			if (this.WrittenClasses.TryGetValue(klass, out bool done)) {
				if (done)
					return;
				throw new CiException(klass, "Circular dependency for class {0}", klass.Name);
			}
			this.WrittenClasses.Add(klass, false);
			WriteStruct(klass.Parent as CiClass);
			foreach (CiField field in klass.Fields)
				WriteStruct(field.Type.BaseType as CiClass);
			this.WrittenClasses[klass] = true;

			Write("struct ");
			Write(klass.Name);
			Write(' ');
			OpenBlock();
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
				WriteSignature(klass, method);
				WriteBody(method);
			}
		}
		CloseFile();
	}
}

}
