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

namespace Foxoft.Ci
{

public class GenC : SourceGenerator
{
	public GenC(string outputPath)
	{
	}

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
			Write(value.Name);
		}
		WriteLine();
		CloseBlock();
		Write(enu.Name);
		WriteLine(";");
	}

	void WriteBaseType(CiType type)
	{
		// TODO
		if (type is CiByteType)
			Write("unsigned char ");
		else if (type is CiStringPtrType)
			Write("const char *");
		else if (type is CiStringStorageType)
			Write("char ");
		else {
			Write(type.Name);
			Write(' ');
			if (type is CiClassPtrType)
				Write('*');
		}
	}

	void Write(CiType type, string name)
	{
		// TODO
		WriteBaseType(type.BaseType);
		Write(name);
		if (type is CiArrayType)
			WriteInitializer((CiArrayType) type);
		if (type.BaseType is CiStringStorageType) {
			Write('[');
			Write(((CiStringStorageType) type.BaseType).Length + 1);
			Write(']');
		}
	}

	void Write(CiField field)
	{
		Write(field.Documentation);
		Write(field.Type, field.Name);
		WriteLine(";");
	}

	void Write(CiClass clazz)
	{
		WriteLine();
		Write(clazz.Documentation);
		Write("typedef struct ");
		OpenBlock();
		foreach (CiField field in clazz.Fields)
			Write(field);
		CloseBlock();
		Write(clazz.Name);
		WriteLine(";");
	}

	void Write(CiConst def)
	{
		Write(def.Documentation);
		Write("#define ");
		Write(def.Name);
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
		return base.GetPriority(expr);
	}

	protected override void WriteConst(object value)
	{
		if (value is CiEnumValue) {
			CiEnumValue ev = (CiEnumValue) value;
			Write(ev.Type.Name);
			Write('_');
			Write(ev.Name);
		}
		else if (value == null)
			Write("NULL");
		else
			base.WriteConst(value);
	}

	protected override void Write(CiFieldAccess expr)
	{
		WriteChild(expr, expr.Obj);
		if (expr.Obj.Type is CiClassPtrType)
			Write("->");
		else
			Write('.');
		Write(expr.Field.Name);
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

	void WriteClearArray(CiExpr expr)
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
		if (expr.Function == CiIntType.MulDivMethod) {
			Write("(int) ((double) (");
			Write(expr.Obj);
			Write(") * (");
			Write(expr.Arguments[0]);
			Write(") / (");
			Write(expr.Arguments[1]);
			Write("))");
		}
		else if (expr.Function == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(']');
		}
		else if (expr.Function == CiStringType.SubstringMethod) {
			// TODO
			throw new ApplicationException();
		}
		else if (expr.Function == CiArrayType.CopyToMethod) {
			Write("memcpy(");
			WriteSum(expr.Arguments[1], expr.Arguments[2]);
			Write(", ");
			WriteSum(expr.Obj, expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[3]);
			Write(')');
		}
		else if (expr.Function == CiArrayType.ToStringMethod) {
			// TODO
			throw new ApplicationException();
		}
		else if (expr.Function == CiArrayStorageType.ClearMethod) {
			WriteClearArray(expr.Obj);
		}
		// TODO
		else
			throw new ApplicationException(expr.Function.Name);
	}

	protected override void WriteInline(CiVar stmt)
	{
		Write(stmt.Type, stmt.Name);
		if (stmt.InitialValue != null) {
			if (stmt.Type is CiArrayStorageType)
				throw new Exception("Cannot initialize array inline");
			Write(" = ");
			Write(stmt.InitialValue);
		}
	}

	protected override void WriteAssignSource(CiAssign assign)
	{
		if (assign.CastIntToByte) {
			Write("(unsigned char) (");
			base.WriteAssignSource(assign);
			Write(')');
		}
		else
			base.WriteAssignSource(assign);
	}

	protected override void WriteInline(CiAssign assign)
	{
		if (assign.Target.Type is CiStringStorageType) {
			if (assign.Op == CiToken.Assign) {
				if (assign.Source is CiMethodCall) {
					CiMethodCall mc = (CiMethodCall) assign.Source;
					if (mc.Function == CiStringType.SubstringMethod
					 || mc.Function == CiArrayType.ToStringMethod) {
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
		base.WriteInline(assign);
	}

	protected override void Write(ICiStatement stmt)
	{
		if (stmt is CiVar) {
			CiVar def = (CiVar) stmt;
			Write(def.Type, def.Name);
			if (def.InitialValue != null && !(def.Type is CiStringStorageType) && !(def.Type is CiArrayStorageType)) {
				Write(" = ");
				Write(def.InitialValue);
			}
			WriteLine(";");
			if (def.InitialValue != null) {
				if (def.Type is CiStringStorageType) {
					WriteInline(new CiAssign {
						Target = new CiVarAccess { Var = def },
						Op = CiToken.Assign,
						Source = def.InitialValue
					});
					WriteLine(";");
				}
				else if (def.Type is CiArrayStorageType) {
					WriteClearArray(new CiVarAccess { Var = def });
					WriteLine(";");
				}
			}
		}
		else
			base.Write(stmt);
	}

	void Write(CiFunction func)
	{
		WriteLine();
		Write(func.Documentation);
		if (!func.IsPublic)
			Write("static ");
		Write(func.ReturnType, func.Name); // TODO
		Write("(");
		bool first = true;
		foreach (CiParam param in func.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Type, param.Name);
		}
		WriteLine(")");
		Write(func.Body);
	}

	public override void Write(CiProgram prog)
	{
		WriteLine("// Generated automatically with \"cito\". Do not edit.");
		WriteLine("#include <stdbool.h>");
		WriteLine("#include <string.h>");
		foreach (CiSymbol symbol in prog.Globals.List) {
			if (symbol is CiEnum)
				Write((CiEnum) symbol);
			else if (symbol is CiClass)
				Write((CiClass) symbol);
		}
		foreach (CiConst konst in prog.ConstArrays) {
			Write("static const ");
			Write(konst.Type, konst.GlobalName);
			Write(" = ");
			WriteConst(konst.Value);
			WriteLine(";");
		}
		foreach (CiBinaryResource resource in prog.BinaryResources) {
			Write("static const unsigned char ");
			WriteName(resource);
			Write('[');
			Write(resource.Content.Length);
			Write("] = ");
			WriteConst(resource.Content);
			WriteLine(";");
		}
		foreach (CiSymbol symbol in prog.Globals.List) {
			if (symbol is CiConst && symbol.IsPublic)
				Write((CiConst) symbol);
			else if (symbol is CiFunction)
				Write((CiFunction) symbol);
		}
	}
}

}
