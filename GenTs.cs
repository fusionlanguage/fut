// GenTs.cs - TypeScript code generator
//
// Copyright (C) 2020-2021  Andy Edwards
// Copyright (C) 2020-2022  Piotr Fusik
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

namespace Foxoft.Ci
{

public class GenTs : GenJs
{
	// GenFullCode = false: only generate TypeScript declarations (.d.ts files)
	// GenFullCode = true: generate full TypeScript code
	bool GenFullCode = false;

	public GenTs WithGenFullCode()
	{
		GenFullCode = true;
		return this;
	}

	protected override bool IsJsPrivate(CiMember member) => false;

	protected override void WriteEnum(CiEnum enu)
	{
		// WARNING: TypeScript enums allow reverse lookup that the Js generator currently
		// doesn't implement
		// https://www.typescriptlang.org/docs/handbook/enums.html#reverse-mappings
		WriteLine();
		Write(enu.Documentation);
		Write("export enum ");
		Write(enu.Name);
		Write(' ');
		OpenBlock();
		foreach (CiConst konst in enu) {
			Write(konst.Documentation);
			WriteUppercaseWithUnderscores(konst.Name);
			WriteExplicitEnumValue(konst);
			WriteLine(',');
		}
		CloseBlock();
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
		Write(": ");
		Write(value.Type);
	}

	void WriteTypeAndName(CiConst konst)
	{
		WriteName(konst);
		Write(": ");
		Write(konst.Type, true);
	}

	void WriteBaseType(CiType type)
	{
		if (type == CiSystem.RegexClass)
			Write("RegExp");
		else if (type == CiSystem.MatchClass)
			Write("RegExpMatchArray");
		else
			Write(type.Name);
	}

	void WriteArrayType(CiType elementType, bool forConst)
	{
		if (elementType.IsNullable)
			Write('(');
		Write(elementType, forConst);
		if (elementType.IsNullable)
			Write(')');
		Write("[]");
	}

	void Write(CiType type, bool forConst = false)
	{
		switch (type) {
		case CiNumericType _:
			Write("number");
			break;
		case CiStringType _:
			Write("string");
			break;
		case CiEnum enu:
			Write(enu == CiSystem.BoolType ? "boolean" : enu.Name);
			break;
		case CiClassType klass:
			switch (klass.Class.Id) {
			case CiId.ArrayPtrClass:
			case CiId.ArrayStorageClass:
				bool isReadonlyPtr = !(klass is CiReadWriteClassType);
				if (klass.ElementType is CiNumericType number) {
					if (klass.Class.Id == CiId.ArrayPtrClass) {
						if (isReadonlyPtr)
							Write("readonly ");
						Write("number[] | ");
					}
					if (isReadonlyPtr)
						Write("Readonly<");
					Write(GetArrayElementType(number));
					Write("Array");
					if (isReadonlyPtr)
						Write('>');
				}
				else {
					if (forConst || isReadonlyPtr)
						Write("readonly ");
					WriteArrayType(klass.ElementType, forConst);
				}
				break;
			case CiId.ListClass:
			case CiId.QueueClass:
			case CiId.StackClass:
				WriteArrayType(klass.ElementType, false);
				break;
			case CiId.HashSetClass:
				Write("Set<");
				Write(klass.ElementType, false);
				Write('>');
				break;
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
			case CiId.OrderedDictionaryClass:
				Write("Partial<Record<");
				Write(klass.KeyType, forConst);
				Write(", ");
				Write(klass.ValueType, forConst);
				Write(">>");
				break;
			default:
				WriteBaseType(klass.Class);
				break;
			}
			break;
		default:
			WriteBaseType(type);
			break;
		}
		if (type.IsNullable)
			Write(" | null");
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Internal:
			break;
		case CiVisibility.Protected:
			Write("protected ");
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	protected override void WriteConst(CiConst konst)
	{
		WriteLine();
		Write(konst.Documentation);
		Write(konst.Visibility);
		Write("static readonly ");
		WriteTypeAndName(konst);
		if (this.GenFullCode)
			WriteVarInit(konst);
		WriteLine(';');
	}

	protected override void WriteField(CiField field)
	{
		Write(field.Visibility);
		WriteTypeAndName(field);
		if (this.GenFullCode)
			WriteVarInit(field);
		WriteLine(';');
	}

	protected override void WriteMethod(CiMethod method)
	{
		WriteDoc(method);
		Write(method.Visibility);
		switch (method.CallType) {
		case CiCallType.Static:
			Write("static ");
			break;
		case CiCallType.Virtual:
			break;
		case CiCallType.Abstract:
			Write("abstract ");
			break;
		case CiCallType.Override:
			break;
		case CiCallType.Normal:
			// no final methods in TS
			break;
		case CiCallType.Sealed:
			// no final methods in TS
			break;
		default:
			throw new NotImplementedException(method.CallType.ToString());
		}
		WriteName(method);
		Write('(');
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i > 0)
				Write(", ");
			WriteName(param);
			if (param.Value != null && !this.GenFullCode)
				Write('?');
			Write(": ");
			Write(param.Type);
			if (param.Value != null && this.GenFullCode)
				WriteVarInit(param);
			i++;
		}
		Write("): ");
		Write(method.Type);
		if (this.GenFullCode)
			WriteBody(method);
		else
			WriteLine(';');
	}

	protected override void WriteClass(CiClass klass)
	{
		Write(klass.Documentation);
		Write("export ");
		switch (klass.CallType) {
		case CiCallType.Normal:
			break;
		case CiCallType.Abstract:
			Write("abstract ");
			break;
		case CiCallType.Static:
		case CiCallType.Sealed:
			// there's no final/sealed keyword, but we accomplish it by marking the constructor private
			break;
		default:
			throw new NotImplementedException(klass.CallType.ToString());
		}
		OpenClass(klass, "", " extends ");

		CiMethodBase constructor = klass.Constructor;
		if (klass.CallType == CiCallType.Static)
			constructor = new CiMethodBase { Visibility = CiVisibility.Private };

		if (constructor != null) {
			Write(constructor.Documentation);
			Write(constructor.Visibility);
			Write("constructor()");
			if (this.GenFullCode) {
				OpenBlock();
				if (klass.BaseClassName != null)
					WriteLine("super();");
				WriteConstructorBody(klass);
				CloseBlock();
			}
			else
				WriteLine(';');
		}

		WriteMembers(klass, this.GenFullCode);

		CloseBlock();
		WriteLine();
	}

	public override void Write(CiProgram program)
	{
		CreateFile(this.OutputFile);
		if (this.GenFullCode)
			WriteTopLevelNatives(program);
		WriteTypes(program);
		if (this.GenFullCode)
			WriteLib(program.Resources);
		CloseFile();
	}
}

}
