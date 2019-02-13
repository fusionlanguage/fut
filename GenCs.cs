// GenCs.cs - C# code generator
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
using System.Linq;

namespace Foxoft.Ci
{

public class GenCs : GenTyped
{
	string Namespace;

	public GenCs(string namespace_)
	{
		this.Namespace = namespace_;
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
			Write("string");
			return;
		}

		CiArrayType array = type as CiArrayType;
		if (array != null) {
			Write(array.ElementType, false);
			Write("[]");
			return;
		}

		Write(type.Name);
	}

	protected override void WriteName(CiSymbol symbol)
	{
		Write(symbol.Name);
	}

	protected override void WriteNewArray(CiType type)
	{
		Write("new ");
		Write(type.BaseType, false);
		CiArrayStorageType storage = (CiArrayStorageType) type;
		Write('[');
		WritePromoted(storage.LengthExpr, CiPriority.Statement);
		Write(']');
		for (CiArrayType array = storage; ; ) {
			array = array.ElementType as CiArrayType;
			if (array == null)
				break;
			Write("[]");
		}
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

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args)
	{
		if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			Write("System.Array.Copy(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			WritePromoted(args);
			Write(')');
		}
		else if (obj.Type is CiArrayStorageType && method.Name == "Fill") {
			CiLiteral literal = args[0] as CiLiteral;
			if (literal == null || !literal.IsDefaultValue)
				throw new NotImplementedException("Only null, zero and false supported");
			Write("System.Array.Clear(");
			obj.Accept(this, CiPriority.Statement);
			Write(", 0, ");
			Write(((CiArrayStorageType) obj.Type).Length);
			Write(')');
		}
		else if (obj.Type == CiSystem.UTF8EncodingClass && method.Name == "GetString") {
			Write("System.Text.Encoding.UTF8.GetString(");
			WritePromoted(args);
			Write(')');
		}
		else {
			if (IsMathReference(obj))
				Write("System.");
			base.WriteCall(obj, method, args);
		}
	}

	protected override void WriteFallthrough(CiExpr expr)
	{
		if (expr is CiGotoDefault)
			WriteLine("goto default;");
		else {
			Write("goto case ");
			expr.Accept(this, CiPriority.Statement);
			WriteLine(";");
		}
	}

	public override void Visit(CiThrow statement)
	{
		Write("throw new System.Exception(");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine(");");
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		if (enu.IsFlags)
			WriteLine("[System.Flags]");
		WritePublic(enu);
		Write("enum ");
		WriteLine(enu.Name);
		OpenBlock();
		bool first = true;
		foreach (CiConst konst in enu) {
			if (!first)
				WriteLine(",");
			first = false;
			Write(konst.Name);
			if (konst.Value != null) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Statement);
			}
		}
		WriteLine();
		CloseBlock();
	}

	void WriteConsts(IEnumerable<CiConst> konsts)
	{
		foreach (CiConst konst in konsts) {
			Write(konst.Visibility);
			if (konst.Type is CiArrayStorageType)
				Write("static readonly ");
			else
				Write("const ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine(";");
		}
	}

	void Write(CiClass klass)
	{
		WriteLine();
		WritePublic(klass);
		Write(klass.CallType, "sealed ");
		OpenClass(klass, "", " : ");

		if (klass.Constructor != null
		 || (klass.IsPublic && klass.CallType != CiCallType.Static)
		 || klass.Fields.Any(field => HasInitCode(field))) {
			if (klass.Constructor != null)
				Write(klass.Constructor.Visibility);
			else
				Write("internal ");
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			foreach (CiField field in klass.Fields)
				WriteInitCode(field);
			if (klass.Constructor != null)
				Write(((CiBlock) klass.Constructor.Body).Statements);
			CloseBlock();
		}

		WriteConsts(klass.Consts);

		foreach (CiField field in klass.Fields) {
			Write(field.Visibility);
			if (field.Type is CiClass || field.Type is CiArrayStorageType)
				Write("readonly ");
			WriteVar(field);
			WriteLine(";");
		}

		foreach (CiMethod method in klass.Methods) {
			WriteLine();
			Write(method.Visibility);
			Write(method.CallType, "sealed override ");
			WriteTypeAndName(method);
			WriteParameters(method);
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
		CreateFile(this.OutputFile);
		if (this.Namespace != null) {
			Write("namespace ");
			WriteLine(this.Namespace);
			OpenBlock();
		}
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes)
			Write(klass);
		if (program.Resources.Count > 0)
			WriteResources(program.Resources);
		if (this.Namespace != null)
			CloseBlock();
		CloseFile();
	}
}

}
