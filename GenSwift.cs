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
using System.Collections.Generic;
using System.Linq;

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
		case "as":
		case "associatedtype":
		case "catch":
		case "defer":
		case "deinit":
		case "extension":
		case "fallthrough":
		case "fileprivate":
		case "func":
		case "guard":
		case "import":
		case "init":
		case "inout":
		case "is":
		case "let":
		case "nil":
		case "operator":
		case "private":
		case "protocol":
		case "repeat":
		case "rethrows":
		case "self":
		case "struct":
		case "subscript":
		case "super":
		case "try":
		case "typealias":
		case "var":
		case "where":
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
		case CiConst konst when konst.InMethod != null:
			WriteCamelCase(konst.InMethod.Name);
			WritePascalCase(symbol.Name);
			break;
		case CiVar _:
		case CiMember _:
			WriteCamelCaseNotKeyword(symbol.Name);
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiField)
			Write("self.");
		WriteName(symbol);
	}

	protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		if (left.Type is CiClassPtrType)
			Write('!');
		Write('.');
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
		if (!value.Type.IsFinal) {
			Write(" : ");
			Write(value.Type, true);
		}
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
		if (expr.Type == CiSystem.StringPtrType)
			Write('!');
		Write(".count");
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if (expr.Left.Type is CiClassPtrType || expr.Right.Type is CiClassPtrType)
			WriteComparison(expr, parent, CiPriority.Equality, not ? " !== " : " === ");
		else
			base.WriteEqual(expr, parent, not);
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj.Type is CiArrayType && method.Name == "CopyTo") {
			args[1].Accept(this, CiPriority.Primary);
			Write('[');
			args[2].Accept(this, CiPriority.Shift);
			Write("..<");
			WriteAdd(args[2], args[3]); // TODO: side effect
			Write("] = ");
			obj.Accept(this, CiPriority.Primary);
			Write('[');
			args[0].Accept(this, CiPriority.Shift);
			Write("..<");
			WriteAdd(args[0], args[3]); // TODO: side effect
			Write(']');
		}
		else if (obj.Type is CiArrayStorageType array && method.Name == "Fill") {
			if (!(args[0] is CiLiteral literal) || !literal.IsDefaultValue)
				throw new NotImplementedException("Only null, zero and false supported");
			obj.Accept(this, CiPriority.Primary);
			Write(" = ");
			WriteNewArray(array);
		}
		else if (obj.Type is CiListType && method.Name == "Insert") {
			obj.Accept(this, CiPriority.Primary);
			Write(".insert(");
			args[1].Accept(this, CiPriority.Statement);
			Write(", at: ");
			args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else if (method == CiSystem.ListRemoveAt) {
			obj.Accept(this, CiPriority.Primary);
			Write(".remove(at: ");
			args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else if (method == CiSystem.ListRemoveRange) {
			obj.Accept(this, CiPriority.Primary);
			Write(".removeSubrange(");
			args[0].Accept(this, CiPriority.Statement);
			Write("..<");
			WriteAdd(args[0], args[1]); // TODO: side effect
			Write(')');
		}
		else if (obj.Type is CiDictionaryType && method.Name == "Remove") {
			obj.Accept(this, CiPriority.Primary);
			Write(".removeValue(forKey: ");
			args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else if (method == CiSystem.ConsoleWrite) {
			// TODO: stderr
			Write("print(");
			args[0].Accept(this, CiPriority.Statement);
			Write(", terminator: \"\")");
		}
		else if (method == CiSystem.ConsoleWriteLine) {
			// TODO: stderr
			Write("print");
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.UTF8GetString) {
			Write("String(bytes: ");
			args[0].Accept(this, CiPriority.Primary);
			Write('[');
			args[1].Accept(this, CiPriority.Shift);
			Write("..<");
			WriteAdd(args[1], args[2]); // TODO: side effect
			Write("], encoding: .utf8)!");
		}
		else if (obj.IsReferenceTo(CiSystem.MathClass)) {
			Include("Foundation");
			if (method == CiSystem.MathCeiling)
				Write("ceil");
			else if (method == CiSystem.MathFusedMultiplyAdd)
				Write("fma");
			else if (method == CiSystem.MathTruncate)
				Write("trunc");
			else
				WriteName(method);
			WriteArgsInParentheses(method, args);
		}
		else {
			if (obj.IsReferenceTo(CiSystem.BasePtr))
				Write("super");
			else
				obj.Accept(this, CiPriority.Primary);
			WriteMemberOp(obj, null);
			if (method == CiSystem.StringStartsWith)
				Write("hasPrefix");
			else if (method == CiSystem.StringEndsWith)
				Write("hasSuffix");
			else if (method == CiSystem.CollectionClear)
				Write("removeAll");
			else if (obj.Type is CiListType && method.Name == "Add")
				Write("append");
			else
				WriteName(method);
			WriteArgsInParentheses(method, args);
		}
	}

	protected override void WriteListStorageInit(CiListType list)
	{
		Write(" = [");
		Write(list.ElementType, false);
		Write("]()");
	}

	protected override void WriteDictionaryStorageInit(CiDictionaryType dict)
	{
		Write(" = [");
		Write(dict.KeyType, true);
		Write(": ");
		Write(dict.ValueType, true);
		Write("]()");
	}

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		CiType type = ((CiArrayStorageType) expr.Type).ElementType;
		Write("[ ");
		WriteCoercedLiterals(type, expr.Items);
		Write(" ]");
		return expr;
	}

	protected override void WriteNew(CiClass klass, CiPriority parent)
	{
		WriteName(klass);
		Write("()");
	}

	static bool IsClassStorage(CiType type)
	{
		while (type is CiArrayStorageType array)
			type = array.ElementType;
		return type is CiClass;
	}

	void WriteDefaultValue(CiType type)
	{
		if (type is CiNumericType)
			Write('0');
		else if (type == CiSystem.BoolType)
			Write("false");
		else if (type == CiSystem.StringStorageType)
			Write("\"\"");
		else if (type is CiArrayStorageType array)
			WriteNewArray(array);
		else
			Write("nil");
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		if (IsClassStorage(elementType)) {
			Write("(1...");
			lengthExpr.Accept(this, CiPriority.Shift);
			Write(").map({ _ in ");
			WriteNewStorage(elementType);
			Write(" })");
		}
		else {
			Write('[');
			Write(elementType, false);
			Write("](repeating: ");
			WriteDefaultValue(elementType);
			Write(", count: ");
			lengthExpr.Accept(this, CiPriority.Statement);
			Write(')');
		}
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

	protected override void WriteChild(CiStatement statement)
	{
		Write(' ');
		OpenBlock();
		if (statement is CiBlock block)
			Write(block.Statements);
		else
			statement.Accept(this);
		CloseBlock();
	}

	public override void Visit(CiExpr statement)
	{
		statement.Accept(this, CiPriority.Statement);
		WriteLine();
	}

	protected override void WriteVar(CiNamedValue def)
	{
		Write(IsClassStorage(def.Type) ? "let " : "var ");
		base.WriteVar(def);
	}

	public override void Visit(CiAssert statement)
	{
		Write("assert(");
		statement.Cond.Accept(this, CiPriority.Statement);
		if (statement.Message != null) {
			Write(", ");
			statement.Message.Accept(this, CiPriority.Statement);
		}
		WriteLine(')');
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
			CiBinaryExpr cond = (CiBinaryExpr) statement.Cond;
			if (statement.RangeStep == 1) {
				iter.Value.Accept(this, CiPriority.Shift);
				switch (cond.Op) {
				case CiToken.Less:
					Write("..<");
					cond.Right.Accept(this, CiPriority.Shift);
					break;
				case CiToken.LessOrEqual:
					Write("...");
					cond.Right.Accept(this, CiPriority.Shift);
					break;
				default:
					throw new NotImplementedException(cond.Op.ToString());
				}
			}
			else {
				Write("stride(from: ");
				iter.Value.Accept(this, CiPriority.Statement);
				switch (cond.Op) {
				case CiToken.Less:
				case CiToken.Greater:
					Write(", to: ");
					cond.Right.Accept(this, CiPriority.Statement);
					break;
				case CiToken.LessOrEqual:
				case CiToken.GreaterOrEqual:
					Write(", through: ");
					cond.Right.Accept(this, CiPriority.Statement);
					break;
				default:
					throw new NotImplementedException(cond.Op.ToString());
				}
				Write(", by: ");
				Write(statement.RangeStep);
				Write(')');
			}
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
		if (statement.Count == 2) {
			Write('(');
			WriteName(statement.Element);
			Write(", ");
			WriteName(statement.ValueVar);
			Write(')');
		}
		else
			WriteName(statement.Element);
		Write(" in ");
		if (statement.Collection.Type is CiSortedDictionaryType) {
			statement.Collection.Accept(this, CiPriority.Primary);
			Write(".sorted(by: { $0.key < $1.key })");
		}
		else
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
	}

	public override void Visit(CiWhile statement)
	{
		Write("while ");
		statement.Cond.Accept(this, CiPriority.Statement);
		WriteChild(statement.Body);
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		WritePublic(enu);
		Write("enum ");
		WriteLine(enu.Name);
		OpenBlock();
		foreach (CiConst konst in enu) {
			Write("case ");
			WriteName(konst);
			/* TODO if (konst.Value != null) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Statement);
			} */
			WriteLine();
		}
		CloseBlock();
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

	void WriteConsts(IEnumerable<CiConst> consts)
	{
		foreach (CiConst konst in consts) {
			WriteLine();
			Write(konst.Visibility);
			Write("static let ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine();
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

		if (klass.Constructor != null) {
			WriteLine("init()");
			OpenBlock();
			WriteConstructorBody(klass);
			CloseBlock();
		}

		WriteConsts(klass.Consts);

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
			if (method.CallType == CiCallType.Abstract) {
				WriteLine();
				OpenBlock();
				WriteLine("preconditionFailure(\"Abstract method called\")");
				CloseBlock();
			}
			else {
				WriteBody(method);
			}
		}

		WriteConsts(klass.ConstArrays);

		CloseBlock();
	}

	public override void Write(CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		OpenStringWriter();
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes)
			Write(klass);

		CreateFile(this.OutputFile);
		WriteIncludes("import ", "");
		CloseStringWriter();
		CloseFile();
	}
}

}
