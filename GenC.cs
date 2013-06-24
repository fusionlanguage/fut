// GenC.cs - C code generator
//
// Copyright (C) 2011-2013  Piotr Fusik
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
using System.Text;

namespace Foxoft.Ci
{

public class GenC : SourceGenerator
{
	CiMethod CurrentMethod;

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

	void Write(CiClass klass, CiConst konst)
	{
		WriteLine();
		Write(konst.Documentation);
		if (konst.Type is CiArrayStorageType) {
			CiArrayStorageType stg = (CiArrayStorageType) konst.Type;
			Write("extern const ");
			Write(konst.Type, klass.Name + "_" + konst.Name);
			WriteLine(";");
			Write("#define ");
			Write(klass.Name);
			Write('_');
			Write(konst.Name);
			Write("_LENGTH  ");
			Write(stg.Length);
		}
		else {
			Write("#define ");
			Write(klass.Name);
			Write('_');
			WriteUppercaseWithUnderscores(konst.Name);
			Write("  ");
			WriteConst(konst.Value);
		}
		WriteLine();
	}

	protected override CiPriority GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiLibrary.SByteProperty || prop == CiLibrary.LowByteProperty)
				return CiPriority.Prefix;
		}
		else if (expr is CiCoercion) {
			CiCoercion c = (CiCoercion) expr;
			if (c.ResultType is CiClassType)
				return c.ResultType is CiClassPtrType ? CiPriority.Prefix : CiPriority.Postfix;
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

	protected override void WriteName(CiConst konst)
	{
		if (konst.Class != null) {
			Write(konst.Class.Name);
			Write('_');
			Write(konst.Name);
		}
		else
			Write(konst.Name);
	}

	protected override void Write(CiVarAccess expr)
	{
		if (expr.Var == this.CurrentMethod.This)
			Write("self");
		else
			base.Write(expr);
	}

	void StartFieldAccess(CiExpr expr)
	{
		WriteChild(CiPriority.Postfix, expr);
		if (expr.Type is CiClassPtrType)
			Write("->");
		else
			Write('.');
	}

	protected override void Write(CiFieldAccess expr)
	{
		StartFieldAccess(expr.Obj);
		WriteCamelCase(expr.Field.Name);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiLibrary.SByteProperty) {
			Write("(signed char) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiLibrary.LowByteProperty) {
			Write("(unsigned char) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiLibrary.StringLengthProperty) {
			Write("(int) strlen(");
			WriteChild(expr, expr.Obj);
			Write(')');
		}
		else
			throw new ArgumentException(expr.Property.Name);
	}

	protected void WriteClearArray(CiExpr expr)
	{
		Write("memset(");
		Write(expr);
		Write(", 0, sizeof(");
		Write(expr);
		Write("))");
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiLibrary.MulDivMethod) {
			Write("(int) ((long long int) ");
			WriteMulDiv(CiPriority.Prefix, expr);
		}
		else if (expr.Method == CiLibrary.CharAtMethod) {
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(']');
		}
		else if (expr.Method == CiLibrary.SubstringMethod) {
			// TODO
			throw new ArgumentException("Substring");
		}
		else if (expr.Method == CiLibrary.ArrayCopyToMethod) {
			Write("memcpy(");
			WriteSum(expr.Arguments[1], expr.Arguments[2]);
			Write(", ");
			WriteSum(expr.Obj, expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[3]);
			Write(')');
		}
		else if (expr.Method == CiLibrary.ArrayToStringMethod) {
			// TODO
			throw new ArgumentException("Array.ToString");
		}
		else if (expr.Method == CiLibrary.ArrayStorageClearMethod) {
			WriteClearArray(expr.Obj);
		}
		else {
			bool first = true;
			if (expr.Method != null) {
				switch (expr.Method.CallType) {
				case CiCallType.Static:
					Write(expr.Method.Class.Name);
					Write('_');
					Write(expr.Method.Name);
					Write('(');
					break;
				case CiCallType.Normal:
					Write(expr.Method.Class.Name);
					Write('_');
					Write(expr.Method.Name);
					Write('(');
					Write(expr.Obj);
					first = false;
					break;
				case CiCallType.Abstract:
				case CiCallType.Virtual:
				case CiCallType.Override:
					CiClass objClass = ((CiClassType) expr.Obj.Type).Class;
					CiClass ptrClass = GetVtblPtrClass(expr.Method.Class);
					CiClass defClass;
					for (defClass = expr.Method.Class; !AddsVirtualMethod(defClass, expr.Method.Name); defClass = defClass.BaseClass)
						;
					if (defClass != ptrClass) {
						Write("((const ");
						Write(defClass.Name);
						Write("Vtbl *) ");
					}
					StartFieldAccess(expr.Obj);
					for (CiClass baseClass = objClass; baseClass != ptrClass; baseClass = baseClass.BaseClass)
						Write("base.");
					Write("vtbl");
					if (defClass != ptrClass)
						Write(')');
					Write("->");
					WriteCamelCase(expr.Method.Name);
					Write('(');
					if (objClass == defClass)
						Write(expr.Obj);
					else {
						Write('&');
						StartFieldAccess(expr.Obj);
						Write("base");
						for (CiClass baseClass = objClass.BaseClass; baseClass != defClass; baseClass = baseClass.BaseClass)
							Write(".base");
					}
					first = false;
					break;
				}
			}
			else {
				// delegate
				Write(expr.Obj);
				Write(".func(");
				Write(expr.Obj);
				Write(".obj");
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
			// if (expr.Method.Throws) Write(" /* throws */");
		}
	}

	void WriteChildWithSuggestedParentheses(CiBinaryExpr parent, CiExpr child, CiPriority suggestedParentPriority, bool assoc)
	{
		if (assoc && GetPriority(parent) == GetPriority(child))
			Write(child);
		else
			WriteChild(suggestedParentPriority, child);
	}

	protected override void Write(CiBinaryExpr expr)
	{
		switch (expr.Op) {
		case CiToken.Equal:
		case CiToken.NotEqual:
		case CiToken.Greater:
			if (expr.Left.Type is CiStringType && !expr.Left.IsConst(null) && !expr.Right.IsConst(null)) {
				Write("strcmp(");
				Write(expr.Left);
				Write(", ");
				Write(expr.Right);
				Write(')');
				WriteOp(expr);
				Write('0');
				break;
			}
			// optimize str.Length == 0, str.Length != 0, str.Length > 0
			CiPropertyAccess pa = expr.Left as CiPropertyAccess;
			if (pa != null && pa.Property == CiLibrary.StringLengthProperty) {
				CiConstExpr ce = expr.Right as CiConstExpr;
				if (ce != null && 0.Equals(ce.Value)) {
					WriteChild(CiPriority.Postfix, pa.Obj);
					Write(expr.Op == CiToken.Equal ? "[0] == '\\0'" : "[0] != '\\0'");
					break;
				}
			}
			base.Write(expr);
			break;
		case CiToken.ShiftLeft:
		case CiToken.ShiftRight:
			WriteChildWithSuggestedParentheses(expr, expr.Left, CiPriority.Multiplicative, true);
			WriteOp(expr);
			WriteChildWithSuggestedParentheses(expr, expr.Right, CiPriority.Multiplicative, false);
			break;
		case CiToken.And:
		case CiToken.Or:
		case CiToken.Xor:
			WriteChildWithSuggestedParentheses(expr, expr.Left, CiPriority.Multiplicative, true);
			WriteOp(expr);
			WriteChildWithSuggestedParentheses(expr, expr.Right, CiPriority.Multiplicative, true);
			break;
		case CiToken.CondOr:
			WriteChildWithSuggestedParentheses(expr, expr.Left, CiPriority.Or, true);
			Write(" || ");
			WriteChildWithSuggestedParentheses(expr, expr.Right, CiPriority.Or, true);
			break;
		default:
			base.Write(expr);
			break;
		}
	}

	protected override void WriteNew(CiType type)
	{
		CiClassStorageType classType = type as CiClassStorageType;
		if (classType != null) {
			Write(classType.Class.Name);
			Write("_New()");
		}
		else {
			CiArrayStorageType arrayType = (CiArrayStorageType) type;
			Write('(');
			Write(arrayType.ElementType, "*");
			Write(") malloc(");
			WriteChild(CiPriority.Multiplicative, arrayType.LengthExpr);
			Write(" * sizeof(");
			Write(arrayType.ElementType, string.Empty);
			Write("))");
		}
	}

	protected override void Write(CiCoercion expr)
	{
		if (expr.ResultType is CiClassType) {
			if (expr.ResultType is CiClassPtrType)
				Write('&');
			WriteChild(expr, (CiExpr) expr.Inner); // TODO: Assign
			CiClass klass = ((CiClassType) expr.Inner.Type).Class;
			if (expr.Inner.Type is CiClassPtrType) {
				Write("->base");
				klass = klass.BaseClass;
			}
			CiClass resultClass = ((CiClassType) expr.ResultType).Class;
			for (; klass != resultClass; klass = klass.BaseClass)
				Write(".base");
		}
		else
			base.Write(expr);
	}

	bool TryWriteCallAndReturn(ICiStatement[] statements, int lastCallIndex, CiExpr returnValue)
	{
		CiMethodCall call = statements[lastCallIndex] as CiMethodCall;
		if (call == null || !call.Method.Throws)
			return false;
		Write(statements, lastCallIndex);
		Write("return ");
		Write(call);
		object errorReturnValue = call.Method.ErrorReturnValue;
		if (!false.Equals(errorReturnValue)) {
			Write(" != ");
			WriteConst(errorReturnValue);
		}
		if (returnValue != null) {
			Write(" ? ");
			Write(returnValue);
			Write(" : ");
			WriteConst(this.CurrentMethod.ErrorReturnValue);
		}
		WriteLine(";");
		return true;
	}

	protected override void Write(ICiStatement[] statements)
	{
		int i = statements.Length - 2;
		if (i >= 0) {
			CiReturn ret = statements[i + 1] as CiReturn;
			if (ret != null && TryWriteCallAndReturn(statements, i, ret.Value))
				return;
		}
		base.Write(statements);
	}

	protected virtual void StartBlock(ICiStatement[] statements)
	{
	}

	void WriteChild(CiMaybeAssign expr)
	{
		if (expr is CiMethodCall)
			Write((CiMethodCall) expr);
		else {
			Write('(');
			base.Visit((CiAssign) expr);
			Write(')');
		}
	}

	void CheckAndThrow(CiMaybeAssign expr, object errorReturnValue)
	{
		Write("if (");
		if (false.Equals(errorReturnValue)) {
			Write('!');
			WriteChild(expr);
		}
		else {
			WriteChild(expr);
			Write(" == ");
			WriteConst(errorReturnValue);
		}
		WriteLine(")");
		this.Indent++;
		Write("return ");
		WriteConst(this.CurrentMethod.ErrorReturnValue);
		this.Indent--;
	}

	public override void Visit(CiExpr expr)
	{
		CiMethodCall call = expr as CiMethodCall;
		if (call != null && call.Method != null && call.Method.Throws)
			CheckAndThrow(call, call.Method.ErrorReturnValue);
		else
			base.Visit(expr);
	}

	protected static bool IsInlineVar(CiVar def)
	{
		if (def.Type is CiClassStorageType) {
			CiClass klass = ((CiClassStorageType) def.Type).Class;
			return !klass.Constructs;
		}
		if (def.InitialValue == null)
			return true;
		if (def.Type is CiArrayStorageType)
			return false;
		if (def.Type is CiStringStorageType)
			return def.InitialValue is CiConstExpr;
		if (def.InitialValue is CiMethodCall && ((CiMethodCall) def.InitialValue).Method.Throws)
			return false;
		return true;
	}

	protected void WriteConstruct(CiClass klass, CiVar stmt)
	{
		Write(klass.Name);
		Write("_Construct(&");
		WriteCamelCase(stmt.Name);
		if (HasVirtualMethods(klass))
			Write(", NULL");
		Write(')');
	}

	public override void Visit(CiVar stmt)
	{
		Write(stmt.Type, stmt.Name);
		if (stmt.Type is CiClassStorageType) {
			CiClass klass = ((CiClassStorageType) stmt.Type).Class;
			if (klass.Constructs) {
				WriteLine(";");
				WriteConstruct(klass, stmt);
			}
		}
		else if (stmt.InitialValue != null) {
			if (stmt.Type is CiArrayStorageType) {
				WriteLine(";");
				WriteClearArray(new CiVarAccess { Var = stmt });
			}
			else if (IsInlineVar(stmt)) {
				Write(" = ");
				Write(stmt.InitialValue);
			}
			else {
				WriteLine(";");
				Visit(new CiAssign {
					Target = new CiVarAccess { Var = stmt },
					Op = CiToken.Assign,
					Source = stmt.InitialValue
				});
			}
		}
	}

	public override void Visit(CiAssign assign)
	{
		if (assign.Target.Type is CiStringStorageType) {
			if (assign.Op == CiToken.Assign) {
				if (assign.Source is CiMethodCall) {
					CiMethodCall mc = (CiMethodCall) assign.Source;
					if (mc.Method == CiLibrary.SubstringMethod
					 || mc.Method == CiLibrary.ArrayToStringMethod) {
						// TODO: make sure no side effects in mc.Arguments[1]
						Write("((char *) memcpy(");
						Write(assign.Target);
						Write(", ");
						WriteSum(mc.Obj, mc.Arguments[0]);
						Write(", ");
						Write(mc.Arguments[1]);
						Write("))[");
						Write(mc.Arguments[1]);
						Write("] = '\\0'");
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
		CiMethodCall call = assign.Source as CiMethodCall;
		if (call != null && call.Method.Throws)
			CheckAndThrow(assign, call.Method.ErrorReturnValue);
		else
			base.Visit(assign);
	}

	public override void Visit(CiDelete stmt)
	{
		Write("free(");
		Write(stmt.Expr);
		WriteLine(");");
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

	protected override void WriteIfOnTrue(CiIf stmt)
	{
		if (stmt.OnFalse != null) {
			// avoid:
			// if (c)
			//    stmtThatThrows; // -> if (method() == ERROR_VALUE) return ERROR_VALUE;
			// else // mismatched if
			//    stmt;
			CiMethodCall call;
			CiAssign assign = stmt.OnTrue as CiAssign;
			if (assign != null)
				call = assign.Source as CiMethodCall;
			else
				call = stmt.OnTrue as CiMethodCall;
			if (call != null && call.Method != null && call.Method.Throws) {
				Write(' ');
				OpenBlock();
				base.WriteIfOnTrue(stmt);
				CloseBlock();
				return;
			}
		}
		base.WriteIfOnTrue(stmt);
	}

	void WriteReturnTrue()
	{
		Write("return ");
		WriteConst(true);
		WriteLine(";");
	}

	public override void Visit(CiReturn stmt)
	{
		if (false.Equals(this.CurrentMethod.ErrorReturnValue))
			WriteReturnTrue();
		else
			base.Visit(stmt);
	}

	protected override void StartCase(ICiStatement stmt)
	{
		// prevent "error: a label can only be part of a statement and a declaration is not a statement"
		if (stmt is CiVar)
			WriteLine(";");
	}

	public override void Visit(CiThrow stmt)
	{
		Write("return ");
		WriteConst(this.CurrentMethod.ErrorReturnValue);
		WriteLine(";");
	}

	void WriteSignature(CiMethod method)
	{
		if (method.Visibility != CiVisibility.Public)
			Write("static ");
		var paramz = method.Signature.Params.Select(param => ToString(param.Type, param.Name));
		if (method.CallType != CiCallType.Static)
			paramz = new string[1] { ToString(method.This.Type, "self") }.Concat(paramz);
		string s = paramz.Any() ? string.Join(", ", paramz.ToArray()) : "void";
		s = method.Class.Name + "_" + method.Name + "(" + s + ")";
		CiType type = method.Signature.ReturnType;
		if (method.Throws && type == CiType.Void)
			type = CiBoolType.Value;
		Write(type, s);
	}

	void Write(CiMethod method)
	{
		if (method.Visibility == CiVisibility.Dead || method.CallType == CiCallType.Abstract)
			return;
		WriteLine();
		this.CurrentMethod = method;
		WriteSignature(method);
		WriteLine();
		OpenBlock();
		ICiStatement[] statements = method.Body.Statements;
		StartBlock(statements);
		if (method.Throws && method.Signature.ReturnType == CiType.Void && method.Body.CompletesNormally) {
			if (!TryWriteCallAndReturn(statements, statements.Length - 1, null)) {
				Write(statements);
				WriteReturnTrue();
			}
		}
		else
			Write(statements);
		CloseBlock();
		this.CurrentMethod = null;
	}

	void WriteConstructorSignature(CiClass klass)
	{
		Write("static void ");
		Write(klass.Name);
		Write("_Construct(");
		Write(klass.Name);
		Write(" *self");
		if (HasVirtualMethods(klass)) {
			Write(", const ");
			Write(GetVtblPtrClass(klass).Name);
			Write("Vtbl *vtbl");
		}
		Write(')');
	}

	void WriteNewSignature(CiClass klass)
	{
		Write(klass.Name);
		Write(" *");
		Write(klass.Name);
		Write("_New(void)");
	}

	void WriteDeleteSignature(CiClass klass)
	{
		Write("void ");
		Write(klass.Name);
		Write("_Delete(");
		Write(klass.Name);
		Write(" *self)");
	}

	static void ForEachStorageField(CiClass klass, Action<CiField, CiClass> action)
	{
		foreach (CiSymbol member in klass.Members) {
			CiField field = member as CiField;
			if (field != null) {
				CiType type = field.Type;
				while (type is CiArrayStorageType)
					type = ((CiArrayStorageType) type).ElementType;
				CiClassStorageType stg = type as CiClassStorageType;
				if (stg != null)
					action(field, stg.Class);
			}
		}
	}

	void WriteNew(CiClass klass)
	{
		WriteNewSignature(klass);
		WriteLine();
		OpenBlock();
		Write(klass.Name);
		Write(" *self = (");
		Write(klass.Name);
		Write(" *) malloc(sizeof(");
		Write(klass.Name);
		WriteLine("));");
		if (klass.Constructs) {
			WriteLine("if (self != NULL)");
			this.Indent++;
			Write(klass.Name);
			Write("_Construct(self");
			if (HasVirtualMethods(klass))
				Write(", NULL");
			WriteLine(");");
			this.Indent--;
		}
		WriteLine("return self;");
		CloseBlock();
	}

	void WriteConstructorNewDelete(CiClass klass)
	{
		if (klass.Constructs) {
			WriteLine();
			this.CurrentMethod = klass.Constructor;
			WriteConstructorSignature(klass);
			WriteLine();
			OpenBlock();
			if (klass.Constructor != null)
				StartBlock(klass.Constructor.Body.Statements);
			CiClass ptrClass = GetVtblPtrClass(klass);
			if (HasVtblValue(klass)) {
				WriteLine("if (vtbl == NULL)");
				this.Indent++;
				Write("vtbl = ");
				CiClass structClass = GetVtblStructClass(klass);
				if (structClass != ptrClass) {
					Write("(const ");
					Write(ptrClass.Name);
					Write("Vtbl *) ");
				}
				Write("&CiVtbl_");
				Write(klass.Name);
				WriteLine(";");
				this.Indent--;
			}
			if (ptrClass == klass)
				WriteLine("self->vtbl = vtbl;");
			if (klass.BaseClass != null && klass.BaseClass.Constructs) {
				Write(klass.BaseClass.Name);
				Write("_Construct(&self->base");
				if (HasVirtualMethods(klass.BaseClass))
					Write(", vtbl");
				WriteLine(");");
			}
			ForEachStorageField(klass, (field, fieldClass) => {
				if (fieldClass.Constructs) {
					Write(fieldClass.Name);
					Write("_Construct(&self->");
					WriteCamelCase(field.Name);
					if (HasVirtualMethods(fieldClass))
						Write(", NULL");
					WriteLine(");");
				}
			});
			if (klass.Constructor != null)
				Write(klass.Constructor.Body.Statements);
			CloseBlock();
			this.CurrentMethod = null;
		}
		if (klass.Visibility == CiVisibility.Public) {
			WriteLine();
			WriteNew(klass);

			WriteLine();
			WriteDeleteSignature(klass);
			WriteLine();
			OpenBlock();
			WriteLine("free(self);");
			CloseBlock();
		}
		else if (klass.IsAllocated) {
			WriteLine();
			Write("static ");
			WriteNew(klass);
		}
	}

	void WriteTypedef(CiClass klass)
	{
		klass.WriteStatus = CiWriteStatus.NotYet;
		klass.HasFields = klass.Members.Any(member => member is CiField);
		bool klassHasInstanceMethods = klass.Members.Any(member => member is CiMethod && ((CiMethod) member).CallType != CiCallType.Static);
		if (klass.BaseClass != null || klass.HasFields || klassHasInstanceMethods) {
			Write("typedef struct ");
			Write(klass.Name);
			Write(' ');
			Write(klass.Name);
			WriteLine(";");
		}
	}

	void Write(CiDelegate del)
	{
		if (del.WriteStatus == CiWriteStatus.Done)
			return;
		if (del.WriteStatus == CiWriteStatus.InProgress)
			throw new ResolveException("Circular dependency for delegate {0}", del.Name);
		del.WriteStatus = CiWriteStatus.InProgress;
		foreach (CiParam param in del.Params) {
			CiDelegate paramDel = param.Type as CiDelegate;
			if (paramDel != null)
				Write(paramDel);
		}
		del.WriteStatus = CiWriteStatus.Done;

		WriteLine();
		Write(del.Documentation);
		WriteLine("typedef struct ");
		OpenBlock();
		WriteLine("void *obj;");
		string[] paramz = del.Params.Select(param => ", " + ToString(param.Type, param.Name)).ToArray();
		string s = "(*func)(void *obj" + string.Concat(paramz) + ")";
		Write(del.ReturnType, s);
		WriteLine(";");
		CloseBlock();
		Write(del.Name);
		WriteLine(";");
	}

	void WriteTypedefs(CiProgram prog, CiVisibility visibility)
	{
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol.Visibility == visibility) {
				if (symbol is CiEnum)
					Write((CiEnum) symbol);
				else if (symbol is CiClass)
					WriteTypedef((CiClass) symbol);
				else if (symbol is CiDelegate)
					((CiDelegate) symbol).WriteStatus = CiWriteStatus.NotYet;
			}
		}
		foreach (CiSymbol symbol in prog.Globals)
			if (symbol.Visibility == visibility && symbol is CiDelegate)
				Write((CiDelegate) symbol);
	}

	static bool AddsVirtualMethods(CiClass klass)
	{
		return klass.Members.OfType<CiMethod>().Any(
			method => method.CallType == CiCallType.Abstract || method.CallType == CiCallType.Virtual);
	}

	static CiClass GetVtblStructClass(CiClass klass)
	{
		while (!AddsVirtualMethods(klass))
			klass = klass.BaseClass;
		return klass;
	}

	static CiClass GetVtblPtrClass(CiClass klass)
	{
		CiClass result = null;
		do {
			if (AddsVirtualMethods(klass))
				result = klass;
			klass = klass.BaseClass;
		} while (klass != null);
		return result;
	}

	static bool HasVirtualMethods(CiClass klass)
	{
		// == return EnumVirtualMethods(klass).Any();
		while (!AddsVirtualMethods(klass)) {
			klass = klass.BaseClass;
			if (klass == null)
				return false;
		}
		return true;
	}

	static IEnumerable<CiMethod> EnumVirtualMethods(CiClass klass)
	{
		IEnumerable<CiMethod> myMethods = klass.Members.OfType<CiMethod>().Where(
			method => method.CallType == CiCallType.Abstract || method.CallType == CiCallType.Virtual);
		if (klass.BaseClass != null)
			return EnumVirtualMethods(klass.BaseClass).Concat(myMethods);
		else
			return myMethods;
	}

	static bool AddsVirtualMethod(CiClass klass, string methodName)
	{
		return klass.Members.OfType<CiMethod>().Any(method =>
			method.Name == methodName && (method.CallType == CiCallType.Abstract || method.CallType == CiCallType.Virtual));
	}

	void WritePtr(CiMethod method, string name)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append("(*");
		sb.Append(name);
		sb.Append(")(");
		sb.Append(method.Class.Name);
		sb.Append(" *self");
		foreach (CiParam param in method.Signature.Params) {
			sb.Append(", ");
			sb.Append(ToString(param.Type, param.Name));
		}
		sb.Append(')');
		CiType type = method.Signature.ReturnType;
		if (method.Throws && type == CiType.Void) // TODO: check subclasses
			type = CiBoolType.Value;
		Write(type, sb.ToString());
	}

	void WriteVtblStruct(CiClass klass)
	{
		if (!AddsVirtualMethods(klass))
			return;
		Write("typedef struct ");
		OpenBlock();
		foreach (CiMethod method in EnumVirtualMethods(klass)) {
			WritePtr(method, ToCamelCase(method.Name));
			WriteLine(";");
		}
		CloseBlock();
		Write(klass.Name);
		WriteLine("Vtbl;");
	}

	void WriteSignatures(CiClass klass, bool pub)
	{
		if (!pub && klass.Constructs) {
			WriteConstructorSignature(klass);
			WriteLine(";");
		}
		if (pub && klass.Visibility == CiVisibility.Public && klass.HasFields) {
			WriteLine();
			WriteNewSignature(klass);
			WriteLine(";");
			WriteDeleteSignature(klass);
			WriteLine(";");
		}
		foreach (CiSymbol member in klass.Members) {
			if ((member.Visibility == CiVisibility.Public) == pub) {
				if (member is CiConst && pub)
					Write(klass, (CiConst) member);
				else if (member.Visibility != CiVisibility.Dead) {
					CiMethod method = member as CiMethod;
					if (method != null && method.CallType != CiCallType.Abstract) {
						if (pub) {
							WriteLine();
							WriteDoc(method);
						}
						WriteSignature(method);
						WriteLine(";");
					}
				}
			}
		}
	}

	static bool HasVtblValue(CiClass klass)
	{
		bool result = false;
		foreach (CiSymbol member in klass.Members) {
			CiMethod method = member as CiMethod;
			if (method != null) {
				switch (method.CallType) {
				case CiCallType.Abstract:
					return false;
				case CiCallType.Virtual:
				case CiCallType.Override:
					result = true;
					break;
				}
			}
		}
		return result;
	}

	void WriteVtblValue(CiClass klass)
	{
		if (!HasVtblValue(klass))
			return;
		CiClass structClass = GetVtblStructClass(klass);
		Write("static const ");
		Write(structClass.Name);
		Write("Vtbl CiVtbl_");
		Write(klass.Name);
		Write(" = ");
		OpenBlock();
		bool first = true;
		foreach (CiMethod method in EnumVirtualMethods(structClass)) {
			CiMethod impl = (CiMethod) klass.Members.Lookup(method.Name);
			if (first)
				first = false;
			else
				WriteLine(",");
			if (impl.CallType == CiCallType.Override) {
				Write('(');
				WritePtr(method, string.Empty);
				Write(") ");
			}
			Write(impl.Class.Name);
			Write('_');
			Write(impl.Name);
		}
		WriteLine();
		this.Indent--;
		WriteLine("};");
	}

	// Common pointer sizes are 32-bit and 64-bit.
	// We assume 64-bit, because this avoids mixing pointers and ints
	// which could add extra alignment if pointers are 64-bit.
	const int SizeOfPointer = 8;

	static int SizeOf(CiClass klass)
	{
		int result = klass.Members.OfType<CiField>().Sum(field => SizeOf(field.Type));
		if (klass.BaseClass != null)
			result += SizeOf(klass.BaseClass);
		if (GetVtblPtrClass(klass) == klass)
			result += SizeOfPointer;
		return result;
	}

	static int SizeOf(CiType type)
	{
		if (type  == CiIntType.Value
		 || type == CiBoolType.Value
		 || type is CiEnum)
			return 4;
		if (type == CiByteType.Value)
			return 1;
		if (type is CiStringStorageType)
			return ((CiStringStorageType) type).Length + 1;
		if (type is CiClassStorageType)
			return SizeOf(((CiClassStorageType) type).Class);
		CiArrayStorageType arrayType = type as CiArrayStorageType;
		if (arrayType != null)
			return arrayType.Length * SizeOf(arrayType.ElementType);
		return SizeOfPointer;
	}

	void WriteStruct(CiClass klass)
	{
		// topological sorting of class hierarchy and class storage fields
		if (klass.WriteStatus == CiWriteStatus.Done)
			return;
		if (klass.WriteStatus == CiWriteStatus.InProgress)
			throw new ResolveException("Circular dependency for class {0}", klass.Name);
		klass.WriteStatus = CiWriteStatus.InProgress;
		klass.Constructs = klass.Constructor != null || HasVirtualMethods(klass);
		if (klass.BaseClass != null) {
			WriteStruct(klass.BaseClass);
			if (klass.BaseClass.Constructs)
				klass.Constructs = true;
		}
		foreach (CiSymbol member in klass.Members) {
			if (member is CiField) {
				CiType type = ((CiField) member).Type;
				while (type is CiArrayStorageType)
					type = ((CiArrayStorageType) type).ElementType;
				CiClassStorageType stg = type as CiClassStorageType;
				if (stg != null) {
					WriteStruct(stg.Class);
					if (stg.Class.Constructs)
						klass.Constructs = true;
				}
			}
		}
		klass.WriteStatus = CiWriteStatus.Done;

		WriteLine();
		WriteVtblStruct(klass);
		if (klass.BaseClass != null || klass.HasFields) {
			Write("struct ");
			Write(klass.Name);
			Write(' ');
			OpenBlock();
			if (klass.BaseClass != null) {
				Write(klass.BaseClass.Name);
				WriteLine(" base;");
			}
			if (GetVtblPtrClass(klass) == klass) {
				Write("const ");
				Write(klass.Name);
				WriteLine("Vtbl *vtbl;");
			}
			IEnumerable<CiField> fields = klass.Members.OfType<CiField>().OrderBy(field => SizeOf(field.Type));
			foreach (CiField field in fields)
				Write(field);
			this.Indent--;
			WriteLine("};");
		}
		WriteSignatures(klass, false);
		WriteVtblValue(klass);
		foreach (CiConst konst in klass.ConstArrays) {
			if (konst.Class != null) {
				if (konst.Visibility != CiVisibility.Public)
					Write("static ");
				Write("const ");
				Write(konst.Type, konst.Class.Name + "_" + konst.Name);
				Write(" = ");
				WriteConst(konst.Value);
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
		if (klass.BaseClass != null || klass.HasFields)
			WriteConstructorNewDelete(klass);
		foreach (CiSymbol member in klass.Members) {
			if (member is CiMethod)
				Write((CiMethod) member);
		}
	}

	void WriteGuard(string directive)
	{
		Write(directive);
		Write(" _");
		foreach (char c in Path.GetFileNameWithoutExtension(this.OutputFile))
			Write(CiLexer.IsLetter(c) ? char.ToUpperInvariant(c) : '_');
		WriteLine("_H_");
	}

	protected virtual void WriteBoolType()
	{
		WriteLine("#include <stdbool.h>");
	}

	public override void Write(CiProgram prog)
	{
		string headerFile = Path.ChangeExtension(this.OutputFile, "h");
		CreateFile(headerFile);
		WriteGuard("#ifndef");
		WriteGuard("#define");
		WriteBoolType();
		WriteLine("#ifdef __cplusplus");
		WriteLine("extern \"C\" {");
		WriteLine("#endif");
		WriteTypedefs(prog, CiVisibility.Public);
		foreach (CiSymbol symbol in prog.Globals) {
			if (symbol is CiClass && symbol.Visibility == CiVisibility.Public)
				WriteSignatures((CiClass) symbol, true);
		}
		WriteLine();
		WriteLine("#ifdef __cplusplus");
		WriteLine("}");
		WriteLine("#endif");
		WriteLine("#endif");
		CloseFile();

		CreateFile(this.OutputFile);
		WriteLine("#include <stdlib.h>");
		WriteLine("#include <string.h>");
		Write("#include \"");
		Write(Path.GetFileName(headerFile));
		WriteLine("\"");
		WriteTypedefs(prog, CiVisibility.Internal);
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
