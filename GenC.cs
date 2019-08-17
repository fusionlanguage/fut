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
using System.Linq;

namespace Foxoft.Ci
{

public class GenC : GenCCpp
{
	SystemInclude IncludeStdBool;
	bool IncludeString;

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiMethod) {
			Write(symbol.Parent.Name);
			Write('_');
			Write(symbol.Name);
		}
		else if (symbol is CiConst)
			WriteUppercaseWithUnderscores(symbol.Name);
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
			if (baseType == CiSystem.BoolType)
				this.IncludeStdBool.Needed = true;
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
		this.IncludeString = true;
		Write("(int) strlen(");
		expr.Accept(this, CiPriority.Primary);
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			this.IncludeString = true;
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
			this.IncludeString = true;
			Write("memset(");
			obj.Accept(this, CiPriority.Statement);
			Write(", 0, sizeof(");
			obj.Accept(this, CiPriority.Statement);
			Write("))");
		}
		else if (IsMathReference(obj)) {
			this.IncludeMath = true;
			if (method.Name == "Ceiling")
				Write("ceil");
			else
				WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.StringContains) {
			this.IncludeString = true;
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
		WriteName(method);
		Write('(');
		if (method.CallType != CiCallType.Static) {
			CiClass resultClass = (CiClass) method.Parent;
			CiClass klass = (CiClass) this.CurrentMethod.Parent;
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
				this.IncludeString = true;
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
			 this.IncludeString = true;
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
		WriteDefinition(method.Type, () => {
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
				WriteDefinition(method.Type, () => {
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
		else {
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
		return klass.Fields.Any(field => field.Type is CiClass fieldClass && NeedsDestructor(fieldClass))
			|| (klass.Parent is CiClass baseClass && NeedsDestructor(baseClass));
	}

	void WriteXstructorSignature(string name, CiClass klass)
	{
		Write("void ");
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
			if (field.Value != null) {
				Write("self->");
				WriteCamelCase(field.Name);
				Write(" = ");
				field.Value.Accept(this, CiPriority.Statement);
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
		foreach (CiField field in klass.Fields) {
			if (field.Type is CiClass fieldClass && NeedsDestructor(fieldClass)) {
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

	public override void Write(CiProgram program)
	{
		this.WrittenClasses.Clear();
		this.IncludeStdBool = new SystemInclude("stdbool.h");
		this.IncludeStdInt = new SystemInclude("stdint.h");
		string headerFile = Path.ChangeExtension(this.OutputFile, "h");
		using (StringWriter stringWriter = new StringWriter()) {
			this.Writer = stringWriter;
			foreach (CiClass klass in program.Classes)
				WriteSignatures(klass, true);

			CreateFile(headerFile);
			WriteLine("#pragma once");
			Write(this.IncludeStdBool);
			Write(this.IncludeStdInt);
			WriteLine("#ifdef __cplusplus");
			WriteLine("extern \"C\" {");
			WriteLine("#endif");
			WriteTypedefs(program, true);
			this.Writer.Write(stringWriter.GetStringBuilder());
		}
		WriteLine();
		WriteLine("#ifdef __cplusplus");
		WriteLine("}");
		WriteLine("#endif");
		CloseFile();

		this.IncludeMath = false;
		this.IncludeString = false;
		using (StringWriter stringWriter = new StringWriter()) {
			this.Writer = stringWriter;
			foreach (CiClass klass in program.Classes)
				WriteStruct(klass);
			foreach (CiClass klass in program.Classes) {
				WriteConstructor(klass);
				WriteDestructor(klass);
				foreach (CiMethod method in klass.Methods) {
					if (method.CallType == CiCallType.Abstract)
						continue;
					WriteLine();
					WriteSignature(klass, method);
					WriteBody(method);
				}
			}

			CreateFile(this.OutputFile);
			if (this.IncludeMath)
				WriteLine("#include <math.h>");
			Write(this.IncludeStdBool);
			Write(this.IncludeStdInt);
			WriteLine("#include <stdlib.h>");
			if (this.IncludeString)
				WriteLine("#include <string.h>");
			Write("#include \"");
			Write(Path.GetFileName(headerFile));
			WriteLine("\"");
			WriteTypedefs(program, false);
			this.Writer.Write(stringWriter.GetStringBuilder());
		}
		CloseFile();
	}
}

}
