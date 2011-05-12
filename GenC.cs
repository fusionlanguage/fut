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
using System.IO;
using System.Linq;
using System.Text;

namespace Foxoft.Ci
{

public class GenC : SourceGenerator
{
	CiMethod CurrentMethod;

	protected override void Write(CiCodeDoc doc)
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

	void Write(CiClass klass, CiConst konst)
	{
		Write(konst.Documentation);
		Write("#define ");
		Write(klass.Name);
		Write('_');
		WriteUppercaseWithUnderscores(konst.Name);
		Write("  ");
		WriteConst(konst.Value);
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

	protected override void WriteName(CiConst konst)
	{
		Write(konst.Name);
	}

	protected override void Write(CiVarAccess expr)
	{
		if (expr.Var == this.CurrentMethod.This)
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
			WriteNonAssocChild(3, expr.Arguments[1]);
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
			bool first = true;
			if (expr.Method != null) {
				Write(expr.Method.Class.Name);
				Write('_');
				Write(expr.Method.Name);
				Write('(');
				if (expr.Obj != null) {
					Write(expr.Obj);
					first = false;
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

	void WriteChildWithSuggestedParentheses(CiBinaryExpr parent, CiExpr child, int suggestedParentPriority, bool assoc)
	{
		if (assoc && GetPriority(parent) == GetPriority(child))
			Write(child);
		else
			WriteChild(suggestedParentPriority, child);
	}

	protected override void Write(CiBinaryExpr expr)
	{
		switch (expr.Op) {
		case CiToken.ShiftLeft:
		case CiToken.ShiftRight:
			WriteChildWithSuggestedParentheses(expr, expr.Left, 3, true);
			WriteOp(expr);
			WriteChildWithSuggestedParentheses(expr, expr.Right, 3, false);
			break;
		case CiToken.And:
		case CiToken.Or:
		case CiToken.Xor:
			WriteChildWithSuggestedParentheses(expr, expr.Left, 3, true);
			WriteOp(expr);
			WriteChildWithSuggestedParentheses(expr, expr.Right, 3, true);
			break;
		case CiToken.CondOr:
			WriteChildWithSuggestedParentheses(expr, expr.Left, 11, true);
			Write(" || ");
			WriteChildWithSuggestedParentheses(expr, expr.Right, 10, true);
			break;
		default:
			base.Write(expr);
			break;
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

	public override void Visit(CiVar stmt)
	{
		Write(stmt.Type, stmt.Name);
		if (stmt.InitialValue != null) {
			if (stmt.Type is CiStringStorageType || (stmt.InitialValue is CiMethodCall && ((CiMethodCall) stmt.InitialValue).Method.Throws)) {
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
		else if (stmt.Type is CiClassStorageType) {
			CiClass klass = ((CiClassStorageType) stmt.Type).Class;
			if (klass.Constructor != null || klass.ConstructsFields) {
				WriteLine(";");
				Write(klass.Name);
				Write("_Construct(&");
				WriteCamelCase(stmt.Name);
				Write(')');
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
		if (!method.IsStatic)
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
		if (method.Visibility == CiVisibility.Dead)
			return;
		WriteLine();
		this.CurrentMethod = method;
		Write(method.Documentation);
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
		Write(" *self)");
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

	void ForEachStorageField(CiClass klass, Action<CiField, CiClass> action)
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

	void WriteConstructorNewDelete(CiClass klass)
	{
		bool hasConstructor = klass.Constructor != null || klass.ConstructsFields;
		if (hasConstructor) {
			WriteLine();
			this.CurrentMethod = klass.Constructor;
			WriteConstructorSignature(klass);
			WriteLine();
			OpenBlock();
			if (klass.Constructor != null)
				StartBlock(klass.Constructor.Body.Statements);
			ForEachStorageField(klass, (field, fieldClass) => {
				if (fieldClass.Constructor != null || fieldClass.ConstructsFields) {
					Write(fieldClass.Name);
					Write("_Construct(&self->");
					WriteCamelCase(field.Name);
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
			WriteNewSignature(klass);
			WriteLine();
			OpenBlock();
			Write(klass.Name);
			Write(" *self = (");
			Write(klass.Name);
			Write(" *) malloc(sizeof(");
			Write(klass.Name);
			WriteLine("));");
			if (hasConstructor) {
				WriteLine("if (self != NULL)");
				this.Indent++;
				Write(klass.Name);
				WriteLine("_Construct(self);");
				this.Indent--;
			}
			WriteLine("return self;");
			CloseBlock();

			WriteLine();
			WriteDeleteSignature(klass);
			WriteLine();
			OpenBlock();
			WriteLine("free(self);");
			CloseBlock();
		}
	}

	void WriteTypedef(CiClass klass)
	{
		klass.WriteStatus = CiWriteStatus.NotYet;
		klass.HasFields = klass.Members.Any(member => member is CiField);
		if (!klass.HasFields)
			return;
		Write("typedef struct ");
		Write(klass.Name);
		Write(' ');
		Write(klass.Name);
		WriteLine(";");
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

		WriteLine("typedef struct ");
		OpenBlock();
		WriteLine("void *obj;");
		var paramz = del.Params.Select(param => ", " + ToString(param.Type, param.Name));
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

	void WriteSignatures(CiClass klass, bool pub)
	{
		if (klass.HasFields) {
			if (!pub && (klass.Constructor != null || klass.ConstructsFields)) {
				WriteConstructorSignature(klass);
				WriteLine(";");
			}
			if (pub && klass.Visibility == CiVisibility.Public) {
				WriteNewSignature(klass);
				WriteLine(";");
				WriteDeleteSignature(klass);
				WriteLine(";");
			}
		}
		foreach (CiSymbol member in klass.Members) {
			if ((member.Visibility == CiVisibility.Public) == pub) {
				if (member is CiConst && pub)
					Write(klass, (CiConst) member);
				else if (member is CiMethod && member.Visibility != CiVisibility.Dead) {
					WriteSignature((CiMethod) member);
					WriteLine(";");
				}
			}
		}
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
				if (stg != null) {
					WriteStruct(stg.Class);
					if (stg.Class.Constructor != null || stg.Class.ConstructsFields)
						klass.ConstructsFields = true;
				}
			}
		}
		klass.WriteStatus = CiWriteStatus.Done;

		WriteLine();
		if (klass.HasFields) {
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
		}
		WriteSignatures(klass, false);
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
		if (klass.HasFields)
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
		foreach (char c in Path.GetFileNameWithoutExtension(this.OutputPath))
			Write(CiLexer.IsLetter(c) ? char.ToUpperInvariant(c) : '_');
		WriteLine("_H_");
	}

	protected virtual void WriteBoolType()
	{
		WriteLine("#include <stdbool.h>");
	}

	public override void Write(CiProgram prog)
	{
		string headerPath = Path.ChangeExtension(this.OutputPath, "h");
		CreateFile(headerPath);
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
		WriteLine("#ifdef __cplusplus");
		WriteLine("}");
		WriteLine("#endif");
		WriteLine("#endif");
		CloseFile();

		CreateFile(this.OutputPath);
		WriteLine("#include <stdlib.h>");
		WriteLine("#include <string.h>");
		Write("#include \"");
		Write(Path.GetFileName(headerPath));
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
