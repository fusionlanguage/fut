// GenC.cs - C code generator
//
// Copyright (C) 2011  Piotr Fusik
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
using System.Text;

namespace Foxoft.Ci
{

public class GenC : SourceGenerator
{
	CiMethod CurrentMethod;

	void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		// TODO
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		Write("typedef enum ");
		OpenBlock();
		bool first = true;
		foreach (CiEnumValue value in enu.Values) {
			if (first)
				first = false;
			else
				WriteLine(",");
			Write(value.Documentation);
			Write(enu.Name);
			Write('_');
			WriteUppercaseWithUnderscores(value.Name);
		}
		WriteLine();
		CloseBlock();
		Write(enu.Name);
		WriteLine(";");
	}

	static void InsertPtr(StringBuilder sb, PtrWritability wr)
	{
		sb.Insert(0, '*');
		if (wr != PtrWritability.ReadWrite)
			sb.Insert(0, "const ");
	}

	protected virtual string ToString(CiType type)
	{
		return type.Name;
	}

	string ToString(CiType type, string s)
	{
		StringBuilder sb = new StringBuilder(s);
		bool needParens = false;
		while (type is CiArrayType) {
			CiArrayStorageType stg = type as CiArrayStorageType;
			if (stg != null) {
				if (needParens) {
					sb.Insert(0, '(');
					sb.Append(')');
					needParens = false;
				}
				sb.Append('[');
				sb.Append(stg.Length);
				sb.Append(']');
			}
			else {
				InsertPtr(sb, ((CiArrayPtrType) type).Writability);
				needParens = true;
			}
			type = ((CiArrayType) type).ElementType;
		}

		if (type is CiByteType)
			sb.Insert(0, "unsigned char ");
		else if (type is CiStringPtrType)
			sb.Insert(0, "const char *");
		else if (type is CiStringStorageType) {
			if (needParens) {
				sb.Insert(0, '(');
				sb.Append(')');
			}
			sb.Insert(0, "char ");
			sb.Append('[');
			sb.Append(((CiStringStorageType) type).Length + 1);
			sb.Append(']');
		}
		else {
			if (type is CiClassPtrType)
				InsertPtr(sb, ((CiClassPtrType) type).Writability);
			sb.Insert(0, ' ');
			sb.Insert(0, ToString(type));
		}
		return sb.ToString();
	}

	protected void Write(CiType type, string name)
	{
		Write(ToString(type, name));
	}

	void Write(CiField field)
	{
		Write(field.Documentation);
		Write(field.Type, ToCamelCase(field.Name));
		WriteLine(";");
	}

	void Write(CiClass klass, CiConst def)
	{
		Write(def.Documentation);
		Write("#define ");
		Write(klass.Name);
		Write('_');
		WriteUppercaseWithUnderscores(def.Name);
		Write("  ");
		WriteConst(def.Value);
		WriteLine();
	}

	protected override int GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiIntType.SByteProperty || prop == CiIntType.LowByteProperty)
				return 2;
		}
		else if (expr is CiCoercion) {
			CiCoercion c = (CiCoercion) expr;
			if (c.ResultType is CiClassPtrType && c.Inner.Type is CiClassStorageType)
				return 2;
		}
		return base.GetPriority(expr);
	}

	protected override void WriteConst(object value)
	{
		if (value is CiEnumValue) {
			CiEnumValue ev = (CiEnumValue) value;
			Write(ev.Type.Name);
			Write('_');
			WriteUppercaseWithUnderscores(ev.Name);
		}
		else if (value == null)
			Write("NULL");
		else
			base.WriteConst(value);
	}

	protected override void Write(CiConstAccess expr)
	{
		Write(expr.Const.Name);
	}

	protected override void Write(CiVarAccess expr)
	{
		if (expr == this.CurrentMethod.This)
			Write("self");
		else
			base.Write(expr);
	}

	protected override void Write(CiFieldAccess expr)
	{
		WriteChild(expr, expr.Obj);
		if (expr.Obj.Type is CiClassPtrType)
			Write("->");
		else
			Write('.');
		WriteCamelCase(expr.Field.Name);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiIntType.SByteProperty) {
			Write("(signed char) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiIntType.LowByteProperty) {
			Write("(unsigned char) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiStringType.LengthProperty) {
			Write("strlen(");
			WriteChild(expr, expr.Obj);
			Write(')');
		}
		// TODO
		else
			throw new ApplicationException(expr.Property.Name);
	}

	protected void WriteClearArray(CiExpr expr)
	{
		Write("memset(");
		Write(expr);
		Write(", 0, sizeof(");
		Write(expr);
		Write("))");
	}

	void WriteSum(CiExpr left, CiExpr right)
	{
		Write(new CiBinaryExpr { Left = left, Op = CiToken.Plus, Right = right });
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiIntType.MulDivMethod) {
			Write("(int) ((double) ");
			WriteChild(2, expr.Obj);
			Write(" * ");
			WriteChild(3, expr.Arguments[0]);
			Write(" / ");
			WriteRightChild(3, expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(']');
		}
		else if (expr.Method == CiStringType.SubstringMethod) {
			// TODO
			throw new ApplicationException();
		}
		else if (expr.Method == CiArrayType.CopyToMethod) {
			Write("memcpy(");
			WriteSum(expr.Arguments[1], expr.Arguments[2]);
			Write(", ");
			WriteSum(expr.Obj, expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[3]);
			Write(')');
		}
		else if (expr.Method == CiArrayType.ToStringMethod) {
			// TODO
			throw new ApplicationException();
		}
		else if (expr.Method == CiArrayStorageType.ClearMethod) {
			WriteClearArray(expr.Obj);
		}
		else {
			Write(expr.Method.Class.Name);
			Write('_');
			Write(expr.Method.Name);
			Write('(');
			bool first = true;
			if (expr.Obj != null) {
				Write(expr.Obj);
				first = false;
			}
			foreach (CiExpr arg in expr.Arguments)
			{
				if (first)
					first = false;
				else
					Write(", ");
				Write(arg);
			}
			Write(')');
		}
	}

	protected override void Write(CiCoercion expr)
	{
		if (expr.ResultType is CiClassPtrType && expr.Inner.Type is CiClassStorageType) {
			Write('&');
			WriteChild(expr, (CiExpr) expr.Inner); // TODO: Assign
		}
		else
			base.Write(expr);
	}

	public override void Visit(CiVar stmt)
	{
		Write(stmt.Type, stmt.Name);
		if (stmt.InitialValue != null) {
			if (stmt.Type is CiStringStorageType) {
				WriteLine(";");
				Visit(new CiAssign {
					Target = new CiVarAccess { Var = stmt },
					Op = CiToken.Assign,
					Source = stmt.InitialValue
				});
			}
			else if (stmt.Type is CiArrayStorageType) {
				WriteLine(";");
				WriteClearArray(new CiVarAccess { Var = stmt });
			}
			else {
				Write(" = ");
				Write(stmt.InitialValue);
			}
		}
	}

	public override void Visit(CiAssign assign)
	{
		if (assign.Target.Type is CiStringStorageType) {
			if (assign.Op == CiToken.Assign) {
				if (assign.Source is CiMethodCall) {
					CiMethodCall mc = (CiMethodCall) assign.Source;
					if (mc.Method == CiStringType.SubstringMethod
					 || mc.Method == CiArrayType.ToStringMethod) {
						Write("String_Substring(");
						Write(assign.Target);
						Write(", ");
						WriteSum(mc.Obj, mc.Arguments[0]);
						Write(", ");
						Write(mc.Arguments[1]);
						Write(')');
						return;
					}
				}
				if (assign.Source is CiConstExpr) {
					string s = ((CiConstExpr) assign.Source).Value as string;
					if (s != null && s.Length == 0) {
						Write(assign.Target);
						Write("[0] = '\\0'");
						return;
					}
				}
				Write("strcpy(");
				Write(assign.Target);
				Write(", ");
				// TODO: not an assignment
				Write((CiExpr) assign.Source);
				Write(')');
				return;
			}
			if (assign.Op == CiToken.AddAssign) {
				Write("strcat(");
				Write(assign.Target);
				Write(", ");
				// TODO: not an assignment
				Write((CiExpr) assign.Source);
				Write(')');
				return;
			}
		}
		base.Visit(assign);
	}

	public override void Visit(CiConst stmt)
	{
		if (stmt.Type is CiArrayType) {
			Write("static const ");
			Write(stmt.Type, stmt.Name);
			Write(" = ");
			WriteConst(stmt.Value);
			WriteLine(";");
		}
	}

	public override void Visit(CiReturn stmt)
	{
		if (false.Equals(this.CurrentMethod.ErrorReturnValue)) {
			Write("return ");
			WriteConst(true);
			WriteLine(";");
		}
		else
			base.Visit(stmt);
	}

	public override void Visit(CiThrow stmt)
	{
		Write("return ");
		WriteConst(this.CurrentMethod.ErrorReturnValue);
		WriteLine(";");
	}

	void WriteSignature(CiMethod method)
	{
		if (!method.IsPublic)
			Write("static ");
		var paramz = method.Params.Select(param => ToString(param.Type, param.Name));
		if (!method.IsStatic)
			paramz = new string[1] { ToString(method.This.Type, "self") }.Concat(paramz);
		string s = string.Join(", ", paramz);
		s = method.Class.Name + "_" + method.Name + "(" + s + ")";
		CiType type = method.ReturnType;
		if (method.Throws && type == CiType.Void)
			type = CiBoolType.Value;
		Write(type, s);
	}

	void Write(CiMethod method)
	{
		WriteLine();
		this.CurrentMethod = method;
		Write(method.Documentation);
		WriteSignature(method);
		WriteLine();
		Write(method.Body);
		this.CurrentMethod = null;
	}

	void WriteTypedef(CiClass klass)
	{
		klass.WriteStatus = CiWriteStatus.NotYet;
		Write("typedef struct ");
		Write(klass.Name);
		Write(' ');
		Write(klass.Name);
		WriteLine(";");
	}

	void WriteStruct(CiClass klass)
	{
		// topological sorting of class storage fields
		if (klass.WriteStatus == CiWriteStatus.Done)
			return;
		if (klass.WriteStatus == CiWriteStatus.InProgress)
			throw new ResolveException("Circular dependency for class {0}", klass.Name);
		klass.WriteStatus = CiWriteStatus.InProgress;
		foreach (CiSymbol member in klass.Members) {
			if (member is CiField) {
				CiType type = ((CiField) member).Type;
				while (type is CiArrayStorageType)
					type = ((CiArrayStorageType) type).ElementType;
				CiClassStorageType stg = type as CiClassStorageType;
				if (stg != null)
					WriteStruct(stg.Class);
			}
		}
		klass.WriteStatus = CiWriteStatus.Done;

		WriteLine();
		Write(klass.Documentation);
		Write("struct ");
		Write(klass.Name);
		Write(' ');
		OpenBlock();
		foreach (CiSymbol member in klass.Members) {
			if (member is CiField)
				Write((CiField) member);
		}
		this.Indent--;
		WriteLine("};");
		foreach (CiSymbol member in klass.Members) {
			if (member is CiConst && member.IsPublic)
				Write(klass, (CiConst) member);
			else if (member is CiMethod) {
				WriteSignature((CiMethod) member);
				WriteLine(";");
			}
		}
		foreach (CiBinaryResource resource in klass.BinaryResources) {
			Write("static const unsigned char ");
			WriteName(resource);
			Write('[');
			Write(resource.Content.Length);
			Write("] = ");
			WriteConst(resource.Content);
			WriteLine(";");
		}
	}

	void WriteCode(CiClass klass)
	{
		foreach (CiSymbol member in klass.Members) {
			if (member is CiMethod)
				Write((CiMethod) member);
		}
	}

	protected virtual void WriteBoolType()
	{
		WriteLine("#include <stdbool.h>");
	}

	public override void Write(CiProgram prog)
	{
		CreateFile(this.OutputPath);
		WriteLine("#include <string.h>");
		WriteBoolType();
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiEnum)
				Write((CiEnum) symbol);
			else if (symbol is CiClass)
				WriteTypedef((CiClass) symbol);
		}
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiClass)
				WriteStruct((CiClass) symbol);
		}
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiClass)
				WriteCode((CiClass) symbol);
		}
		CloseFile();
	}
}

}
