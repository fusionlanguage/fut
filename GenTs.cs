// GenTs.cs - TypeScript code generator
//
// Copyright (C) 2020       Andy Edwards
// Copyright (C) 2020-2021  Piotr Fusik
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
using System.Linq;

namespace Foxoft.Ci
{

public class GenTs : GenJs
{
	protected readonly Dictionary<CiClass, bool> WrittenClasses = new Dictionary<CiClass, bool>();

	// GenFullCode = false: only generate TypeScript declarations (.d.ts files)
	// GenFullCode = true: generate full TypeScript code
	bool GenFullCode = false;

	public GenTs WithGenFullCode()
	{
		GenFullCode = true;
		return this;
	}

	void Write(CiEnum enu)
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
		int i = 0;
		foreach (CiConst konst in enu) {
			Write(konst.Documentation);
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			if (konst.Value != null)
				konst.Value.Accept(this, CiPriority.Argument);
			else
				Write(i);
			WriteLine(',');
			i++;
		}
		CloseBlock();
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
		Write(": ");
		Write(value.Type);
	}

	protected void WriteTypeAndName(CiConst konst)
	{
		WriteName(konst);
		Write(": ");
		Write(konst.Type, true);
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
		case CiDictionaryType dict:
			Write("Record<");
			Write(dict.KeyType, forConst);
			Write(", ");
			Write(dict.ValueType, forConst);
			Write('>');
			break;
		case CiListType list:
			Write(list.ElementType, forConst);
			Write("[]");
			break;
		case CiArrayType array:
			CiType elementType = array.ElementType;
			if (elementType is CiNumericType number) {
				if (!(array is CiArrayStorageType)) {
					if (array.IsReadonlyPtr)
						Write("readonly ");
					Write("number[] | ");
				}
				if (array.IsReadonlyPtr)
					Write("Readonly<");
				Write(GetArrayElementType(number));
				Write("Array");
				if (array.IsReadonlyPtr)
					Write('>');
			}
			else {
				if (forConst || array.IsReadonlyPtr)
					Write("readonly ");
				if (elementType is CiArrayType)
					Write('(');
				Write(elementType, forConst);
				if (elementType is CiArrayType)
					Write(')');
				Write("[]");
			}
			break;
		default:
			CiType baseType = type is CiClassPtrType classPtr ? classPtr.Class : type;
			if (baseType == CiSystem.RegexClass)
				Write("RegExp");
			else if (baseType == CiSystem.MatchClass)
				Write("RegExpMatchArray");
			else
				Write(type.Name);
			break;
		}
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

	void Write(CiClass klass, CiMethod method)
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

	void WriteConsts(IEnumerable<CiConst> consts)
	{
		foreach (CiConst konst in consts) {
			WriteLine();
			Write(konst.Documentation);
			Write(konst.Visibility);
			Write("static readonly ");
			WriteTypeAndName(konst);
			if (this.GenFullCode)
				WriteVarInit(konst);
			WriteLine(';');
		}
	}

	void Write(CiClass klass)
	{
		// topological sorting of class hierarchy and class storage fields
		if (klass == null)
			return;
		if (this.WrittenClasses.TryGetValue(klass, out bool done)) {
			if (done)
				return;
			throw new CiException(klass, "Circular dependency for class {0}", klass.Name);
		}
		this.WrittenClasses.Add(klass, false);
		Write(klass.Parent as CiClass);
		foreach (CiField field in klass.Fields)
			Write(field.Type.BaseType as CiClass);
		this.WrittenClasses[klass] = true;

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
		if (klass.CallType == CiCallType.Static) {
			if (constructor == null)
				constructor = new CiMethodBase();
			constructor.Visibility = CiVisibility.Private;
		}

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

		WriteConsts(klass.Consts);

		foreach (CiField field in klass.Fields) {
			Write(field.Visibility);
			WriteTypeAndName(field);
			if (this.GenFullCode)
				WriteVarInit(field);
			WriteLine(';');
		}

		foreach (CiMethod method in klass.Methods)
			Write(klass, method);

		if (this.GenFullCode)
			WriteConsts(klass.ConstArrays);
		CloseBlock();
		WriteLine();
	}

	public override void Write(CiProgram program)
	{
		CreateFile(this.OutputFile);
		if (this.GenFullCode)
			WriteTopLevelNatives(program);
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.OfType<CiClass>()) // TODO: topological sort of class hierarchy
			Write(klass);
		if (this.GenFullCode && (program.Resources.Count > 0 || this.Library.Any(l => l != null)))
			WriteLib(program.Resources);
		CloseFile();
	}
}

}