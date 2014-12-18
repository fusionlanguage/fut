// GenCs.cs - C# code generator
//
// Copyright (C) 2011-2014  Piotr Fusik
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
using System.Linq;

namespace Foxoft.Ci
{

public class GenCs : GenBase
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

	void Write(CiCallType callType)
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
			Write("sealed ");
			break;
		}
	}

	protected override void Write(CiType type)
	{
		if (type == null) {
			Write("void");
			return;
		}

		CiArrayType array = type as CiArrayType;
		if (array != null) {
			Write(array.ElementType);
			Write("[]");
			return;
		}

		CiIntegerType integer = type as CiIntegerType;
		if (integer != null) {
			switch (integer.TypeCode) {
			case TypeCode.SByte: Write("sbyte"); break;
			case TypeCode.Byte: Write("byte"); break;
			case TypeCode.Int16: Write("short"); break;
			case TypeCode.UInt16: Write("ushort"); break;
			case TypeCode.Int32: Write("int"); break;
			case TypeCode.Int64: Write("long"); break;
			default: throw new NotImplementedException(integer.TypeCode.ToString());
			}
			return;
		}

		Write(type.Name);
	}

	void WriteVar(CiNamedValue def)
	{
		WriteTypeAndName(def);
		if (def.Type is CiClass) {
			Write(" = new ");
			Write(def.Type.Name);
			Write("()");
		}
		else {
			CiArrayStorageType array = def.Type as CiArrayStorageType;
			if (array != null) {
				Write(" = new ");
				Write(array.ElementType); // FIXME: array types, arrays of object storage, initialized arrays
				Write('[');
				Write(array.Length);
				Write(']');
			}
			else if (def.Value != null) {
				Write(" = ");
				def.Value.Accept(this, CiPriority.Statement);
			}
		}
	}

	public override CiExpr Visit(CiVar expr, CiPriority parent)
	{
		WriteVar(expr);
		return expr;
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

	void Write(CiClass klass)
	{
		WriteLine();
		WritePublic(klass);
		Write(klass.CallType);
		OpenClass(klass, " : ");

		if (klass.Constructor != null) {
			Write(klass.Constructor.Visibility);
			Write(klass.Name);
			WriteLine("()");
			Visit((CiBlock) klass.Constructor.Body);
		}
		else if (klass.IsPublic && klass.CallType != CiCallType.Static) {
			if (klass.CallType != CiCallType.Sealed)
				Write("protected ");
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			CloseBlock();
		}

		foreach (CiConst konst in klass.Consts) {
			Write(konst.Visibility);
			if (konst.Type is CiArrayStorageType)
				Write("readonly ");
			else
				Write("const ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine(";");
		}

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
			Write(method.CallType);
			WriteTypeAndName(method);
			Write('(');
			bool first = true;
			foreach (CiVar param in method.Parameters) {
				if (!first)
					Write(", ");
				first = false;
				WriteTypeAndName(param);
			}
			Write(')');
			WriteBody(method);
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
		if (this.Namespace != null)
			CloseBlock();
		CloseFile();
	}
}

}
