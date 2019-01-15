// GenCpp.cs - C++ code generator
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

public class GenCpp : GenTyped
{
	string Namespace;

	public GenCpp(string namespace_)
	{
		this.Namespace = namespace_;
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte: Write("int8_t"); break;
		case TypeCode.Byte: Write("uint8_t"); break;
		case TypeCode.Int16: Write("int16_t"); break;
		case TypeCode.UInt16: Write("uint16_t"); break;
		case TypeCode.Int32: Write("int"); break;
		case TypeCode.Int64: Write("int64_t"); break;
		default: throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void Write(CiType type, bool promote)
	{
		if (type == null) {
			Write("void");
			return;
		}

		CiIntegerType integer = type as CiIntegerType;
		if (integer != null) {
			Write(GetTypeCode(integer, promote));
			return;
		}

		if (type is CiStringType) {
			Write("std::string"); // FIXME
			return;
		}

		CiArrayType array = type as CiArrayType;
		if (array != null) {
			Write(array.ElementType, false);
			Write("[]"); // FIXME
			return;
		}

		Write(type.Name);
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
			Write("nullptr");
		else
			base.WriteLiteral(value);
	}

	protected override void WriteMemberOp(CiSymbolReference symbol)
	{
		if (symbol.Symbol is CiConst) // FIXME
			Write("::");
		else
			Write('.');
	}

	protected override void WriteCall(CiExpr obj, string method, CiExpr[] args)
	{
		if (IsMathReference(obj)) {
			Write("std::");
			if (method == "Ceiling")
				Write("ceil");
			else
				WriteLowercase(method);
			Write('(');
			WritePromoted(args);
			Write(')');
			return;
		}
		base.WriteCall(obj, method, args);
	}

	protected override void WriteResource(string name, int length)
	{
		// TODO
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		Write("strlen("); // TODO: std::string?
		expr.Accept(this, CiPriority.Primary);
		Write(')');
	}

	public override void Visit(CiThrow statement)
	{
		WriteLine("throw std::exception();");
		// TODO: statement.Message.Accept(this, CiPriority.Statement);
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write("enum class ");
		WriteLine(enu.Name);
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
		WriteLine("};");
	}

	void WriteDeclarations(CiClass klass, CiVisibility visibility, string visibilityKeyword)
	{
		IEnumerable<CiConst> consts = klass.Consts.Where(c => c.Visibility == visibility);
		IEnumerable<CiField> fields = klass.Fields.Where(f => f.Visibility == visibility);
		IEnumerable<CiMethod> methods = klass.Methods.Where(m => m.Visibility == visibility);
		if (!consts.Any() && !fields.Any() && !methods.Any())
			return;

		Write(visibilityKeyword);
		WriteLine(":");
		this.Indent++;

		foreach (CiConst konst in consts) {
			Write("static constexpr ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine(";");
		}

		foreach (CiField field in fields)
		{
			WriteTypeAndName(field); // TODO: field.Value
			WriteLine(";");
		}

		foreach (CiMethod method in methods)
		{
			switch (method.CallType) {
			case CiCallType.Static:
				Write("static ");
				break;
			case CiCallType.Abstract:
			case CiCallType.Virtual:
				Write("virtual ");
				break;
			default:
				break;
			}
			WriteTypeAndName(method);
			WriteParameters(method);
			switch (method.CallType) {
			case CiCallType.Abstract:
				Write(" = 0");
				break;
			case CiCallType.Override:
				Write(" override");
				break;
			case CiCallType.Sealed:
				Write(" final");
				break;
			default:
				break;
			}
			WriteLine(";");
		}

		this.Indent--;
	}

	void Write(CiClass klass)
	{
		WriteLine();
		OpenClass(klass, klass.CallType == CiCallType.Sealed ? " final" : "", " : public ");
		this.Indent--;
		WriteDeclarations(klass, CiVisibility.Public, "public");
		WriteDeclarations(klass, CiVisibility.Protected, "protected");
		WriteDeclarations(klass, CiVisibility.Private, "private");
		WriteLine("};");
	}

	void WriteMethod(CiClass klass, CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		WriteLine();
		Write(method.Type, true);
		Write(' ');
		Write(klass.Name);
		Write("::");
		WriteCamelCase(method.Name);
		WriteParameters(method);
		WriteBody(method);
	}

	public override void Write(CiProgram program)
	{
		string headerFile = Path.ChangeExtension(this.OutputFile, "hpp");
		CreateFile(headerFile);
		WriteLine("#pragma once");
		WriteLine("#include <string>");
		if (this.Namespace != null) {
			Write("namespace ");
			WriteLine(this.Namespace);
			OpenBlock();
		}
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes)
			Write(klass);
		if (this.Namespace != null)
			CloseBlock();
		CloseFile();

		CreateFile(this.OutputFile);
		WriteLine("#include <cmath>");
		Write("#include \"");
		Write(Path.GetFileName(headerFile));
		WriteLine("\"");
		//foreach (CiEnum enu in program.OfType<CiEnum>())
		//	Write(enu);
		//if (program.Resources.Count > 0)
		//	WriteResources(program.Resources);
		foreach (CiClass klass in program.Classes)
			foreach (CiMethod method in klass.Methods)
				WriteMethod(klass, method);
		CloseFile();
	}
}

}
