// GenSwift.cs - Swift code generator
//
// Copyright (C) 2020  Piotr Fusik
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

public class GenSwift : GenTyped
{
	void WriteCamelCaseNotKeyword(string name)
	{
		switch (name) {
		case "this":
			Write("self");
			break;
		// TODO
		case "as":
		case "catch":
		case "import":
		case "is":
		case "let":
		case "operator":
		case "private":
		case "struct":
		case "super":
		case "try":
		case "var":
			Write(name);
			Write('_');
			break;
		default:
			WriteCamelCase(name);
			break;
		}
	}

	protected override void WriteName(CiSymbol symbol)
	{
		switch (symbol) {
		case CiContainerType _:
			Write(symbol.Name);
			break;
		case CiVar _:
		case CiMember _:
			WriteCamelCaseNotKeyword(symbol.Name);
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte:
			Write("Int8");
			break;
		case TypeCode.Byte:
			Write("UInt8");
			break;
		case TypeCode.Int16:
			Write("Int16");
			break;
		case TypeCode.UInt16:
			Write("UInt16");
			break;
		case TypeCode.Int32:
			Write("Int");
			break;
		case TypeCode.UInt32:
			Write("UInt32");
			break;
		case TypeCode.Int64:
			Write("Int64");
			break;
		default:
			throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void Write(CiType type, bool promote)
	{
		switch (type) {
		case CiIntegerType integer:
			Write(GetTypeCode(integer, promote));
			break;
		case CiFloatingType _:
			Write(type == CiSystem.DoubleType ? "Double" : "Float");
			break;
		case CiStringType _:
			Write("String");
			if (type is CiStringPtrType)
				Write('?');
			break;
		case CiEnum _:
			Write(type == CiSystem.BoolType ? "Bool" : type.Name);
			break;
		case CiClassPtrType classPtr:
			Write(classPtr.Class.Name);
			Write('?');
			break;
		case CiDictionaryType dict:
			Write('[');
			Write(dict.KeyType, true);
			Write(": ");
			Write(dict.ValueType, true);
			Write(']');
			break;
		case CiArrayType array:
			Write('[');
			Write(array.ElementType, false);
			Write(']');
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
		Write(" : ");
		Write(value.Type, true);
	}

	protected override void WriteLiteral(object value)
	{
		if (value == null)
			Write("nil");
		else
			base.WriteLiteral(value);
	}

	public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent)
	{
		Write('"');
		foreach (CiInterpolatedPart part in expr.Parts) {
			foreach (char c in part.Prefix)
				WriteEscapedChar(c);
			if (part.Argument != null) {
				Write("\\(");
				part.Argument.Accept(this, CiPriority.Statement);
				// TODO
				Write(')');
			}
		}
		Write('"');
		return expr;
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		expr.Accept(this, CiPriority.Primary);
		Write(".count");
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj.IsReferenceTo(CiSystem.BasePtr))
			Write("super");
		else
			obj.Accept(this, CiPriority.Primary);
		Write('.');
		WriteName(method);
		WriteArgsInParentheses(method, args);
	}

	protected override void WriteListStorageInit(CiListType list)
	{
		throw new NotImplementedException();
	}

	protected override void WriteDictionaryStorageInit(CiDictionaryType dict)
	{
		throw new NotImplementedException();
	}

	protected override void WriteNew(CiClass klass, CiPriority parent)
	{
		WriteName(klass);
		Write("()");
	}

	protected override bool HasInitCode(CiNamedValue def)
	{
		return false;
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
	}

	protected override void WriteResource(string name, int length)
	{
		Write("TODO");
	}

	public override void Visit(CiExpr statement)
	{
		statement.Accept(this, CiPriority.Statement);
		WriteLine();
	}

	void WriteDefaultValue(CiType type)
	{
		if (type is CiNumericType)
			Write('0');
		else if (type == CiSystem.BoolType)
			Write("false");
		else if (type == CiSystem.StringStorageType)
			Write("\"\"");
		else
			Write("nil");
	}

	protected override void WriteVar(CiNamedValue def)
	{
		switch (def.Type) {
		case CiClass _:
			Write("let ");
			WriteName(def);
			Write(" = ");
			WriteName(def.Type);
			Write("()");
			break;
		case CiArrayStorageType array:
			Write("let ");
			WriteName(def);
			Write(" = Array(repeating: ");
			WriteDefaultValue(array.ElementType);
			Write(", count: ");
			Write(array.Length);
			Write(')');
			break;
		case CiListType list:
			Write("let ");
			WriteName(def);
			Write(" = [");
			Write(list.ElementType, false);
			Write("]()");
			break;
		case CiDictionaryType dict:
			Write("let ");
			WriteName(def);
			Write(" = [");
			Write(dict.KeyType, true);
			Write(": ");
			Write(dict.ValueType, true);
			Write("]()");
			break;
		default:
			Write("var ");
			base.WriteVar(def);
			break;
		}
	}

	public override void Visit(CiAssert statement)
	{
		Write("TODO");
	}

	public override void Visit(CiBreak statement)
	{
		WriteLine("break");
	}

	public override void Visit(CiContinue statement)
	{
		WriteLine("continue");
	}

	public override void Visit(CiDoWhile statement)
	{
		Write("repeat");
		WriteChild(statement.Body);
		Write("while ");
		statement.Cond.Accept(this, CiPriority.Statement);
		WriteLine();
	}

	public override void Visit(CiIf statement)
	{
		Write("if ");
		statement.Cond.Accept(this, CiPriority.Statement);
		WriteChild(statement.OnTrue);
		if (statement.OnFalse != null) {
			Write("else");
			if (statement.OnFalse is CiIf) {
				Write(' ');
				statement.OnFalse.Accept(this);
			}
			else
				WriteChild(statement.OnFalse);
		}
	}

	public override void Visit(CiFor statement)
	{
		if (statement.IsRange) {
			CiVar iter = (CiVar) statement.Init;
			Write("for ");
			WriteName(iter);
			Write(" in ");
			iter.Value.Accept(this, CiPriority.Statement);
			CiBinaryExpr cond = (CiBinaryExpr) statement.Cond;
			switch (cond.Op) {
			case CiToken.Less:
				Write("..<");
				cond.Right.Accept(this, CiPriority.Statement);
				break;
			case CiToken.LessOrEqual:
				Write("..");
				cond.Right.Accept(this, CiPriority.Statement);
				break;
			case CiToken.Greater:
			case CiToken.GreaterOrEqual:
				Write("TODO");
				break;
			default:
				throw new NotImplementedException(cond.Op.ToString());
			}
			if (statement.RangeStep != 1)
				Write("TODO");
			WriteChild(statement.Body);
			return;
		}

		if (statement.Init != null)
			statement.Init.Accept(this);
		Write("while ");
		if (statement.Cond != null)
			statement.Cond.Accept(this, CiPriority.Statement);
		else
			Write("True");
		Write(' ');
		OpenBlock();
		statement.Body.Accept(this);
		if (statement.Advance != null)
			statement.Advance.Accept(this);
		CloseBlock();
	}

	public override void Visit(CiForeach statement)
	{
		Write("for ");
		WriteName(statement.Element);
		Write(" in ");
		statement.Collection.Accept(this, CiPriority.Statement);
		WriteChild(statement.Body);
	}

	public override void Visit(CiReturn statement)
	{
		if (statement.Value == null)
			WriteLine("return");
		else {
			Write("return ");
			statement.Value.Accept(this, CiPriority.Statement);
			WriteLine();
		}
	}

	public override void Visit(CiSwitch statement)
	{
		Write("switch ");
		statement.Value.Accept(this, CiPriority.Statement);
		WriteLine(" {");
		foreach (CiCase kase in statement.Cases) {
			foreach (CiExpr value in kase.Values) {
				Write("case ");
				WriteCoerced(statement.Value.Type, value, CiPriority.Statement);
				WriteLine(':');
			}
			this.Indent++;
			WriteCaseBody(kase.Body);
			this.Indent--;
		}
		if (statement.DefaultBody != null) {
			WriteLine("default:");
			this.Indent++;
			WriteCaseBody(statement.DefaultBody);
			this.Indent--;
		}
		WriteLine('}');
	}

	public override void Visit(CiThrow statement)
	{
		Write("throw ");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine();
		// FIXME: WriteXcrement<CiPostfixExpr>(statement.Message);
	}

	public override void Visit(CiWhile statement)
	{
		Write("while ");
		statement.Cond.Accept(this, CiPriority.Statement);
		WriteChild(statement.Body);
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Internal:
			Write("fileprivate ");
			break;
		case CiVisibility.Protected:
			Write("open ");
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	void Write(CiClass klass)
	{
		WriteLine();
		Write(klass.Documentation);
		WritePublic(klass);
		if (klass.CallType == CiCallType.Sealed)
			Write("final ");
		OpenClass(klass, "", " : ");
		
		foreach (CiField field in klass.Fields) {
			WriteLine();
			Write(field.Visibility);
			WriteVar(field);
			WriteLine();
		}

		foreach (CiMethod method in klass.Methods) {
			WriteLine();
			Write(method.Visibility);
			switch (method.CallType) {
			case CiCallType.Static:
				Write("static ");
				break;
			case CiCallType.Override:
				Write("override ");
				break;
			case CiCallType.Sealed:
				Write("final override ");
				break;
			default:
				break;
			}
			Write("func ");
			WriteName(method);
			WriteParameters(method, true);
			if (method.Throws)
				Write(" throws");
			if (method.Type != null) {
				Write(" -> ");
				Write(method.Type, true);
			}
			WriteBody(method);
		}

		CloseBlock();
	}

	public override void Write(CiProgram program)
	{
		CreateFile(this.OutputFile);
		foreach (CiClass klass in program.Classes)
			Write(klass);
		CloseFile();
	}
}

}
