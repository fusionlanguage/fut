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
				// TODO
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
			// Write(klass.Constructor.Body)
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
			Write("const ");
			// TODO: type
			Write(konst.Name);
			Write(" = ");
			// TODO: value
			WriteLine(";");
		}

		foreach (CiField field in klass.Fields) {
			Write(field.Visibility);
			// TODO: type
			Write(field.Name);
			WriteLine(";");
		}

		foreach (CiMethod method in klass.Methods) {
			WriteLine();
			Write(method.Visibility);
			Write(method.CallType);
			// TODO: type
			Write(method.Name);
			Write('(');
			for (int i = 0; i < method.Parameters.Length; i++) {
				CiVar param = method.Parameters[i];
				if (i > 0)
					Write(", ");
				// TODO: type
				Write(param.Name);
				// TODO: default value?
			}
			Write(')');
			// TODO: body
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
		foreach (CiEnum enu in program.Enums)
			Write(enu);
		foreach (CiClass klass in program.Classes)
			Write(klass);
		if (this.Namespace != null)
			CloseBlock();
		CloseFile();
	}
}

}
