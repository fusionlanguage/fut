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

	static void InsertPtr(StringBuilder sb, PtrWritability wr)
	{
		sb.Insert(0, '*');
		if (wr != PtrWritability.ReadWrite)
			sb.Insert(0, "const ");
	}

	static string ToString(CiType type, string s)
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
			sb.Insert(0, type.Name);
		}
		return sb.ToString();
	}

	void Write(CiType type, string name)
	{
		Write(ToString(type, name));
	}

	void Write(CiField field)
	{
		Write(field.Documentation);
		Write(field.Type, field.Name);
		WriteLine(";");
	}

	void Write(CiClass klass)
	{
		// topological sorting of class storage fields
		if (klass.WriteStatus == CiWriteStatus.Done)
			return;
		if (klass.WriteStatus == CiWriteStatus.InProgress)
			throw new ResolveException("Circular dependency for class {0}", klass.Name);
		klass.WriteStatus = CiWriteStatus.InProgress;
		foreach (CiField field in klass.Fields) {
			CiType type = field.Type;
			while (type is CiArrayStorageType)
				type = ((CiArrayStorageType) type).ElementType;
			CiClassStorageType stg = field.Type as CiClassStorageType;
			if (stg != null)
				Write(stg.Class);
		}
		klass.WriteStatus = CiWriteStatus.Done;

		WriteLine();
		Write(klass.Documentation);
		Write("typedef struct ");
		OpenBlock();
		foreach (CiField field in klass.Fields)
			Write(field);
		CloseBlock();
		Write(klass.Name);
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

	protected override void Write(CiConstAccess expr)
	{
		Write(expr.Const.Name);
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
			if (stmt.Type is CiArrayStorageType)
				throw new Exception("Cannot initialize array inline");
			Write(" = ");
			Write(stmt.InitialValue);
		}
	}

	public override void Visit(CiAssign assign)
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
					Write(new CiAssign {
						Target = new CiVarAccess { Var = def },
						Op = CiToken.Assign,
						Source = def.InitialValue
					});
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

	void WriteSignature(CiFunction func)
	{
		if (!func.IsPublic)
			Write("static ");
		string s = string.Join(", ",  func.Params.Select(param => ToString(param.Type, param.Name)));
		s = func.Name + "(" + s + ")";
		Write(func.ReturnType, s);
	}

	void Write(CiFunction func)
	{
		WriteLine();
		Write(func.Documentation);
		WriteSignature(func);
		WriteLine();
		Write(func.Body);
	}

	public override void Write(CiProgram prog)
	{
		CreateFile(this.OutputPath);
		WriteLine("#include <stdbool.h>");
		WriteLine("#include <string.h>");
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiEnum)
				Write((CiEnum) symbol);
			else if (symbol is CiClass)
				((CiClass) symbol).WriteStatus = CiWriteStatus.NotYet;
		}
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiClass)
				Write((CiClass) symbol);
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
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiConst && symbol.IsPublic)
				Write((CiConst) symbol);
			else if (symbol is CiFunction) {
				WriteSignature((CiFunction) symbol);
				WriteLine(";");
			}
		}
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiFunction)
				Write((CiFunction) symbol);
		}
		CloseFile();
	}
}

}
