// GenSwift.cs - Swift code generator
//
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
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci
{

public class GenSwift : GenPySwift
{
	bool Throw;
	bool ArrayRef;
	bool StringCharAt;
	bool StringIndexOf;
	bool StringSubstring;
	readonly List<HashSet<string>> VarsAtIndent = new List<HashSet<string>>();
	readonly List<bool> VarBytesAtIndent = new List<bool>();

	protected override void StartDocLine()
	{
		Write("/// ");
	}

	protected override string GetDocBullet() => "/// * ";

	protected override void Write(CiCodeDoc doc)
	{
		if (doc != null)
			WriteContent(doc);
	}

	void WriteCamelCaseNotKeyword(string name)
	{
		switch (name) {
		case "this":
			Write("self");
			break;
		case "As":
		case "Associatedtype":
		case "Await":
		case "Break":
		case "Case":
		case "Catch":
		case "Class":
		case "Continue":
		case "Default":
		case "Defer":
		case "Deinit":
		case "Do":
		case "Else":
		case "Enum":
		case "Extension":
		case "Fallthrough":
		case "False":
		case "Fileprivate":
		case "For":
		case "Foreach":
		case "Func":
		case "Guard":
		case "If":
		case "Import":
		case "In":
		case "Init":
		case "Inout":
		case "Int":
		case "Internal":
		case "Is":
		case "Let":
		case "Nil":
		case "Operator":
		case "Private":
		case "Protocol":
		case "Public":
		case "Repeat":
		case "Rethrows":
		case "Return":
		case "Self":
		case "Static":
		case "Struct":
		case "Switch":
		case "Subscript":
		case "Super":
		case "Throw":
		case "Throws":
		case "True":
		case "Try":
		case "Typealias":
		case "Var":
		case "Void":
		case "Where":
		case "While":
		case "as":
		case "associatedtype":
		case "await":
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
			WriteCamelCase(name);
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
		if (symbol.Parent is CiForeach forEach && forEach.Collection.Type is CiStringType) {
			Write("Int(");
			WriteCamelCaseNotKeyword(symbol.Name);
			Write(".value)");
		}
		else
			base.WriteLocalName(symbol, parent);
	}

	static bool NeedsUnwrap(CiExpr expr)
	{
		if (expr.Type == null)
			return false;
		if (expr is CiSymbolReference symbol && expr.Type is CiClassType) {
			if (symbol.Name == "this")
				return false;
			if (symbol.Symbol.Parent is CiForeach forEach
			 && forEach.Collection.Type is CiArrayStorageType array
			 && array.GetElementType() is CiStorageType)
				return false;
		}
		return expr.Type.IsNullable();
	}

	protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		if (NeedsUnwrap(left))
			Write('!');
		Write('.');
	}

	void OpenIndexing(CiExpr collection)
	{
		collection.Accept(this, CiPriority.Primary);
		if (!(collection.Type is CiStorageType))
			Write('!');
		Write('[');
	}

	static bool IsArrayRef(CiArrayStorageType array) => array.PtrTaken || array.GetElementType() is CiStorageType;

	void WriteClassName(CiClassType klass)
	{
		switch (klass.Class.Id) {
		case CiId.StringClass:
			Write("String");
			break;
		case CiId.ArrayPtrClass:
			this.ArrayRef = true;
			Write("ArrayRef<");
			Write(klass.GetElementType());
			Write('>');
			break;
		case CiId.ListClass:
		case CiId.QueueClass:
		case CiId.StackClass:
			Write('[');
			Write(klass.GetElementType());
			Write(']');
			break;
		case CiId.HashSetClass:
			Write("Set<");
			Write(klass.GetElementType());
			Write('>');
			break;
		case CiId.DictionaryClass:
		case CiId.SortedDictionaryClass:
			Write('[');
			Write(klass.GetKeyType());
			Write(": ");
			Write(klass.GetValueType());
			Write(']');
			break;
		case CiId.LockClass:
			Include("Foundation");
			Write("NSRecursiveLock");
			break;
		default:
			Write(klass.Class.Name);
			break;
		}
	}

	void Write(CiType type)
	{
		switch (type) {
		case CiIntegerType integer:
			switch (GetIntegerTypeCode(integer, false)) {
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
				throw new NotImplementedException(integer.ToString());
			}
			break;
		case CiFloatingType _:
			Write(type == CiSystem.DoubleType ? "Double" : "Float");
			break;
		case CiEnum _:
			Write(type == CiSystem.BoolType ? "Bool" : type.Name);
			break;
		case CiArrayStorageType arrayStg:
			if (IsArrayRef(arrayStg)) {
				this.ArrayRef = true;
				Write("ArrayRef<");
				Write(arrayStg.GetElementType());
				Write('>');
			}
			else {
				Write('[');
				Write(arrayStg.GetElementType());
				Write(']');
			}
			break;
		case CiClassType klass:
			WriteClassName(klass);
			if (klass.IsNullable())
				Write('?');
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
		if (!value.Type.IsFinal() || value.IsAssignableStorage()) {
			Write(" : ");
			Write(value.Type);
		}
	}

	public override void VisitLiteralNull() => Write("nil");

	static bool IsForeachStringStg(CiExpr expr)
	{
		if (!(expr is CiSymbolReference symbol) || !(symbol.Symbol.Parent is CiForeach loop))
			return false;
		CiClassType klass = (CiClassType) loop.Collection.Type;
		switch (klass.Class.Id) {
		case CiId.ArrayStorageClass:
		case CiId.ListClass:
		case CiId.HashSetClass:
			return klass.GetElementType() == CiSystem.StringStorageType;
		case CiId.DictionaryClass:
		case CiId.SortedDictionaryClass:
			return (symbol.Symbol == loop.GetVar() ? klass.GetKeyType() : klass.GetValueType()) == CiSystem.StringStorageType;
		default:
			throw new NotImplementedException(klass.Class.Name);
		}
	}

	void WriteUnwrappedString(CiExpr expr, CiPriority parent, bool substringOk)
	{
		if (!(expr is CiLiteral) && expr.Type == CiSystem.StringPtrType && !IsForeachStringStg(expr)) {
			expr.Accept(this, CiPriority.Primary);
			Write('!');
		}
		else if (!substringOk && expr is CiCallExpr call && call.Method.Symbol.Id == CiId.StringSubstring)
			WriteCall("String", expr);
		else
			expr.Accept(this, parent);
	}

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		if (expr.Parts.Any(part => part.WidthExpr != null || part.Format != ' ' || part.Precision >= 0)) {
			Include("Foundation");
			Write("String(format: ");
			WritePrintf(expr, false);
		}
		else {
			Write('"');
			foreach (CiInterpolatedPart part in expr.Parts) {
				Write(part.Prefix);
				Write("\\(");
				WriteUnwrappedString(part.Argument, CiPriority.Argument, true);
				Write(')');
			}
			Write(expr.Suffix);
			Write('"');
		}
		return expr;
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		if (type is CiNumericType && !(expr is CiLiteral)
		 && GetTypeCode(type, false) != GetTypeCode(expr.Type, expr is CiBinaryExpr binary && binary.Op != CiToken.LeftBracket)) {
			Write(type);
			Write('(');
			if (type is CiIntegerType && expr is CiCallExpr call && call.Method.Symbol.Id == CiId.MathTruncate)
				call.Arguments[0].Accept(this, CiPriority.Argument);
			else
				expr.Accept(this, CiPriority.Argument);
			Write(')');
		}
		else if (type == CiSystem.StringStorageType)
			WriteUnwrappedString(expr, parent, false);
		else
			expr.Accept(this, parent);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		WriteUnwrappedString(expr, CiPriority.Primary, true);
		Write(".count");
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		this.StringCharAt = true;
		Write("ciStringCharAt(");
		WriteUnwrappedString(expr.Left, CiPriority.Argument, false);
		Write(", ");
		expr.Right.Accept(this, CiPriority.Argument);
		Write(')');
	}

	public override CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		switch (expr.Symbol.Id) {
		case CiId.MathNaN:
			Write("Float.nan");
			return expr;
		case CiId.MathNegativeInfinity:
			Write("-Float.infinity");
			return expr;
		case CiId.MathPositiveInfinity:
			Write("Float.infinity");
			return expr;
		default:
			return base.VisitSymbolReference(expr, parent);
		}
	}

	protected override string GetReferenceEqOp(bool not) => not ? " !== " : " === ";

	void WriteStringContains(CiExpr obj, string name, List<CiExpr> args)
	{
		WriteUnwrappedString(obj, CiPriority.Primary, true);
		Write('.');
		Write(name);
		Write('(');
		WriteUnwrappedString(args[0], CiPriority.Argument, true);
		Write(')');
	}

	void WriteRange(CiExpr startIndex, CiExpr length)
	{
		WriteCoerced(CiSystem.IntType, startIndex, CiPriority.Shift);
		Write("..<");
		WriteAdd(startIndex, length); // TODO: side effect
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.StringContains:
			WriteStringContains(obj, "contains", args);
			break;
		case CiId.StringEndsWith:
			WriteStringContains(obj, "hasSuffix", args);
			break;
		case CiId.StringIndexOf:
			Include("Foundation");
			this.StringIndexOf = true;
			Write("ciStringIndexOf(");
			WriteUnwrappedString(obj, CiPriority.Argument, true);
			Write(", ");
			WriteUnwrappedString(args[0], CiPriority.Argument, true);
			Write(')');
			break;
		case CiId.StringLastIndexOf:
			Include("Foundation");
			this.StringIndexOf = true;
			Write("ciStringIndexOf(");
			WriteUnwrappedString(obj, CiPriority.Argument, true);
			Write(", ");
			WriteUnwrappedString(args[0], CiPriority.Argument, true);
			Write(", .backwards)");
			break;
		case CiId.StringStartsWith:
			WriteStringContains(obj, "hasPrefix", args);
			break;
		case CiId.StringSubstring:
			if (args[0].IsLiteralZero())
				WriteUnwrappedString(obj, CiPriority.Primary, true);
			else {
				this.StringSubstring = true;
				Write("ciStringSubstring(");
				WriteUnwrappedString(obj, CiPriority.Argument, false);
				Write(", ");
				WriteCoerced(CiSystem.IntType, args[0], CiPriority.Argument);
				Write(')');
			}
			if (args.Count == 2) {
				Write(".prefix(");
				WriteCoerced(CiSystem.IntType, args[1], CiPriority.Argument);
				Write(')');
			}
			break;
		case CiId.ArrayCopyTo:
		case CiId.ListCopyTo:
			OpenIndexing(args[1]);
			WriteRange(args[2], args[3]);
			Write("] = ");
			OpenIndexing(obj);
			WriteRange(args[0], args[3]);
			Write(']');
			break;
		case CiId.ArrayFillAll when obj.Type is CiArrayStorageType array && !IsArrayRef(array):
			obj.Accept(this, CiPriority.Assign);
			Write(" = [");
			Write(array.GetElementType());
			Write("](repeating: ");
			WriteCoerced(array.GetElementType(), args[0], CiPriority.Argument);
			Write(", count: ");
			VisitLiteralLong(array.Length);
			Write(')');
			break;
		case CiId.ArrayFillPart when obj.Type is CiArrayStorageType array && !IsArrayRef(array):
			OpenIndexing(obj);
			WriteRange(args[1], args[2]);
			Write("] = ArraySlice(repeating: ");
			WriteCoerced(array.GetElementType(), args[0], CiPriority.Argument);
			Write(", count: ");
			WriteCoerced(CiSystem.IntType, args[2], CiPriority.Argument); // FIXME: side effect
			Write(')');
			break;
		case CiId.ArraySortAll:
			obj.Accept(this, CiPriority.Primary);
			Write("[0..<");
			VisitLiteralLong(((CiArrayStorageType) obj.Type).Length);
			Write("].sort()");
			break;
		case CiId.ArraySortPart:
		case CiId.ListSortPart:
			OpenIndexing(obj);
			WriteRange(args[0], args[1]);
			Write("].sort()");
			break;
		case CiId.ListAdd:
		case CiId.QueueEnqueue:
		case CiId.StackPush:
			WriteListAppend(obj, args);
			break;
		case CiId.ListAny:
			obj.Accept(this, CiPriority.Primary);
			Write(".contains ");
			args[0].Accept(this, CiPriority.Argument);
			break;
		case CiId.ListClear:
		case CiId.QueueClear:
		case CiId.StackClear:
		case CiId.HashSetClear:
		case CiId.DictionaryClear:
		case CiId.SortedDictionaryClear:
			obj.Accept(this, CiPriority.Primary);
			Write(".removeAll()");
			break;
		case CiId.ListInsert:
			obj.Accept(this, CiPriority.Primary);
			Write(".insert(");
			CiType elementType = ((CiClassType) obj.Type).GetElementType();
			if (args.Count == 1)
				WriteNewStorage(elementType);
			else
				WriteCoerced(elementType, args[1], CiPriority.Argument);
			Write(", at: ");
			WriteCoerced(CiSystem.IntType, args[0], CiPriority.Argument);
			Write(')');
			break;
		case CiId.ListRemoveAt:
			obj.Accept(this, CiPriority.Primary);
			Write(".remove(at: ");
			WriteCoerced(CiSystem.IntType, args[0], CiPriority.Argument);
			Write(')');
			break;
		case CiId.ListRemoveRange:
			obj.Accept(this, CiPriority.Primary);
			Write(".removeSubrange(");
			WriteRange(args[0], args[1]);
			Write(')');
			break;
		case CiId.QueueDequeue:
			obj.Accept(this, CiPriority.Primary);
			Write(".removeFirst()");
			break;
		case CiId.QueuePeek:
			obj.Accept(this, CiPriority.Primary);
			Write(".first");
			break;
		case CiId.StackPeek:
			obj.Accept(this, CiPriority.Primary);
			Write(".last");
			break;
		case CiId.StackPop:
			obj.Accept(this, CiPriority.Primary);
			Write(".removeLast()");
			break;
		case CiId.HashSetAdd:
			obj.Accept(this, CiPriority.Primary);
			Write(".insert(");
			WriteCoerced(((CiClassType) obj.Type).GetElementType(), args[0], CiPriority.Argument);
			Write(')');
			break;
		case CiId.DictionaryAdd:
			WriteDictionaryAdd(obj, args);
			break;
		case CiId.DictionaryContainsKey:
		case CiId.SortedDictionaryContainsKey:
			if (parent > CiPriority.Equality)
				Write('(');
			WriteIndexing(obj, args[0]);
			Write(" != nil");
			if (parent > CiPriority.Equality)
				Write(')');
			break;
		case CiId.DictionaryRemove:
		case CiId.SortedDictionaryRemove:
			obj.Accept(this, CiPriority.Primary);
			Write(".removeValue(forKey: ");
			args[0].Accept(this, CiPriority.Argument);
			Write(')');
			break;
		case CiId.ConsoleWrite:
			// TODO: stderr
			Write("print(");
			WriteUnwrappedString(args[0], CiPriority.Argument, true);
			Write(", terminator: \"\")");
			break;
		case CiId.ConsoleWriteLine:
			// TODO: stderr
			Write("print(");
			if (args.Count == 1)
				WriteUnwrappedString(args[0], CiPriority.Argument, true);
			Write(')');
			break;
		case CiId.UTF8GetByteCount:
			WriteUnwrappedString(args[0], CiPriority.Primary, true);
			Write(".utf8.count");
			break;
		case CiId.UTF8GetBytes:
			if (AddVar("cibytes"))
				Write(this.VarBytesAtIndent[this.Indent] ? "var " : "let ");
			Write("cibytes = [UInt8](");
			WriteUnwrappedString(args[0], CiPriority.Primary, true);
			WriteLine(".utf8)");
			OpenIndexing(args[1]);
			WriteCoerced(CiSystem.IntType, args[2], CiPriority.Shift);
			if (args[2].IsLiteralZero())
				Write("..<");
			else {
				Write(" ..< ");
				WriteCoerced(CiSystem.IntType, args[2], CiPriority.Add); // TODO: side effect
				Write(" + ");
			}
			WriteLine("cibytes.count] = cibytes[...]");
			break;
		case CiId.UTF8GetString:
			Write("String(decoding: ");
			OpenIndexing(args[0]);
			WriteRange(args[1], args[2]);
			Write("], as: UTF8.self)");
			break;
		case CiId.EnvironmentGetEnvironmentVariable:
			Include("Foundation");
			Write("ProcessInfo.processInfo.environment[");
			WriteUnwrappedString(args[0], CiPriority.Argument, false);
			Write(']');
			break;
		case CiId.MathMethod:
		case CiId.MathLog2:
			Include("Foundation");
			WriteCamelCase(method.Name);
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathCeiling:
			Include("Foundation");
			WriteCall("ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			Include("Foundation");
			WriteCall("fma", args[0], args[1], args[2]);
			break;
		case CiId.MathIsFinite:
			args[0].Accept(this, CiPriority.Primary);
			Write(".isFinite");
			break;
		case CiId.MathIsInfinity:
			args[0].Accept(this, CiPriority.Primary);
			Write(".isInfinite");
			break;
		case CiId.MathIsNaN:
			args[0].Accept(this, CiPriority.Primary);
			Write(".isNaN");
			break;
		case CiId.MathTruncate:
			Include("Foundation");
			WriteCall("trunc", args[0]);
			break;
		default:
			if (obj == null) {
				if (method.IsStatic()) {
					WriteName(this.CurrentMethod.Parent);
					Write('.');
				}
			}
			else if (obj.IsReferenceTo(CiSystem.BasePtr))
				Write("super.");
			else {
				obj.Accept(this, CiPriority.Primary);
				WriteMemberOp(obj, null);
			}
			WriteName(method);
			WriteArgsInParentheses(method, args);
			break;
		}
	}

	protected override void WriteNewArray(CiArrayStorageType array)
	{
		if (IsArrayRef(array))
			base.WriteNewArray(array);
		else {
			Write('[');
			Write(array.GetElementType());
			Write("](repeating: ");
			WriteDefaultValue(array.GetElementType());
			Write(", count: ");
			VisitLiteralLong(array.Length);
			Write(')');
		}
	}

	protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
	{
		WriteClassName(klass);
		Write("()");
	}

	void WriteDefaultValue(CiType type)
	{
		if (type is CiNumericType)
			Write('0');
		else if (type is CiEnum) {
			if (type == CiSystem.BoolType)
				Write("false");
			else {
				WriteName(type);
				Write('.');
				WriteName(type.First);
			}
		}
		else if (type == CiSystem.StringStorageType)
			Write("\"\"");
		else if (type is CiArrayStorageType array)
			WriteNewArray(array);
		else
			Write("nil");
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		this.ArrayRef = true;
		Write("ArrayRef<");
		Write(elementType);
		Write(">(");
		switch (elementType) {
		case CiArrayStorageType _:
			Write("factory: { ");
			WriteNewStorage(elementType);
			Write(" }");
			break;
		case CiStorageType klass:
			Write("factory: ");
			WriteName(klass.Class);
			Write(".init");
			break;
		default:
			Write("repeating: ");
			WriteDefaultValue(elementType);
			break;
		}
		Write(", count: ");
		lengthExpr.Accept(this, CiPriority.Argument);
		Write(')');
	}

	public override CiExpr VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
	{
		if (expr.Op == CiToken.Tilde && expr.Type is CiEnumFlags) {
			Write(expr.Type.Name);
			Write("(rawValue: ~");
			expr.Inner.Accept(this, CiPriority.Primary);
			Write(".rawValue)");
			return expr;
		}
		return base.VisitPrefixExpr(expr, parent);
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		OpenIndexing(expr.Left);
		if (expr.Right.Type is CiEnum)
			expr.Right.Accept(this, CiPriority.Argument);
		else
			WriteCoerced(CiSystem.IntType, expr.Right, CiPriority.Argument);
		Write(']');
		if (parent != CiPriority.Assign && expr.Left.Type is CiClassType dict && dict.Class.TypeParameterCount == 2)
			Write('!');
	}

	protected override void Write(CiExpr expr, CiPriority parent, CiBinaryExpr binary)
	{
		if (binary.Op == CiToken.Plus && binary.Type == CiSystem.StringPtrType) {
			WriteUnwrappedString(expr, parent, true);
			return;
		}
		CiType type;
		switch (binary.Op) {
		case CiToken.Plus:
		case CiToken.Minus:
		case CiToken.Asterisk:
		case CiToken.Slash:
		case CiToken.Mod:
		case CiToken.And:
		case CiToken.Or:
		case CiToken.Xor:
		case CiToken.ShiftLeft when expr == binary.Left:
		case CiToken.ShiftRight when expr == binary.Left:
			if (expr is CiSymbolReference || expr is CiSelectExpr || expr is CiCallExpr || expr.IsIndexing()) {
				type = CiBinaryExpr.PromoteNumericTypes(binary.Left.Type, binary.Right.Type);
				if (type != expr.Type) {
					WriteCoerced(type, expr, parent);
					return;
				}
			}
			break;
		case CiToken.Less:
		case CiToken.LessOrEqual:
		case CiToken.Greater:
		case CiToken.GreaterOrEqual:
		case CiToken.Equal:
		case CiToken.NotEqual:
			type = CiBinaryExpr.PromoteFloatingTypes(binary.Left.Type, binary.Right.Type);
			if (type != null && type != expr.Type) {
				WriteCoerced(type, expr, parent);
				return;
			}
			break;
		default:
			break;
		}
		expr.Accept(this, parent);
	}

	protected override void WriteAnd(CiBinaryExpr expr, CiPriority parent)
	{
		Write(expr, parent > CiPriority.Mul, CiPriority.Mul, " & ", CiPriority.Primary);
	}

	void WriteEnumFlagsAnd(CiBinaryExpr expr, string method, string notMethod)
	{
		if (expr.Right is CiPrefixExpr negation && negation.Op == CiToken.Tilde)
			WriteCall(expr.Left, notMethod, negation.Inner);
		else
			WriteCall(expr.Left, method, expr.Right);
	}

	public override CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Type is CiEnumFlags) {
			switch (expr.Op) {
			case CiToken.And:
				WriteEnumFlagsAnd(expr, "intersection", "subtracting");
				return expr;
			case CiToken.Or:
				WriteCall(expr.Left, "union", expr.Right);
				return expr;
			case CiToken.Xor:
				WriteCall(expr.Left, "symmetricDifference", expr.Right);
				return expr;
			case CiToken.AndAssign:
				WriteEnumFlagsAnd(expr, "formIntersection", "subtract");
				return expr;
			case CiToken.OrAssign:
				WriteCall(expr.Left, "formUnion", expr.Right);
				return expr;
			case CiToken.XorAssign:
				WriteCall(expr.Left, "formSymmetricDifference", expr.Right);
				return expr;
			default:
				break;
			}
		}
		switch (expr.Op) {
		case CiToken.ShiftLeft:
			return Write(expr, parent > CiPriority.Mul, CiPriority.Primary, " << ", CiPriority.Primary);
		case CiToken.ShiftRight:
			return Write(expr, parent > CiPriority.Mul, CiPriority.Primary, " >> ", CiPriority.Primary);
		case CiToken.Or:
			return Write(expr, parent > CiPriority.Add, CiPriority.Add, " | ", CiPriority.Mul);
		case CiToken.Xor:
			return Write(expr, parent > CiPriority.Add, CiPriority.Add, " ^ ", CiPriority.Mul);
		case CiToken.Assign:
		case CiToken.AddAssign:
		case CiToken.SubAssign:
		case CiToken.MulAssign:
		case CiToken.DivAssign:
		case CiToken.ModAssign:
		case CiToken.ShiftLeftAssign:
		case CiToken.ShiftRightAssign:
		case CiToken.AndAssign:
		case CiToken.OrAssign:
		case CiToken.XorAssign:
			CiExpr right = expr.Right;
			if (right is CiBinaryExpr rightBinary && rightBinary.IsAssign()) {
				VisitBinaryExpr(rightBinary, CiPriority.Statement);
				WriteLine();
				right = rightBinary.Left; // TODO: side effect
			}
			expr.Left.Accept(this, CiPriority.Assign);
			Write(' ');
			Write(expr.GetOpString());
			Write(' ');
			if (right is CiLiteralNull
			 && expr.Left is CiBinaryExpr leftBinary
			 && leftBinary.Op == CiToken.LeftBracket
			 && leftBinary.Left.Type is CiClassType dict
			 && dict.Class.TypeParameterCount == 2) {
				Write(dict.GetValueType());
				Write(".none");
			}
			else
				WriteCoerced(expr.Type, right, CiPriority.Argument);
			return expr;
		default:
			return base.VisitBinaryExpr(expr, parent);
		}
	}

	protected override void WriteInitCode(CiNamedValue def)
	{
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("CiResource.");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	static bool Throws(CiExpr expr)
	{
		switch (expr) {
		case CiVar _:
		case CiLiteral _:
		case CiLambdaExpr _:
			return false;
		case CiInterpolatedString interp:
			foreach (CiInterpolatedPart part in interp.Parts) {
				if (Throws(part.Argument))
					return true;
			}
			return false;
		case CiSymbolReference symbol:
			return symbol.Left != null && Throws(symbol.Left);
		case CiUnaryExpr unary:
			return unary.Inner != null /* new C() */ && Throws(unary.Inner);
		case CiBinaryExpr binary:
			return Throws(binary.Left) || (binary.Op != CiToken.Is && Throws(binary.Right));
		case CiSelectExpr select:
			return Throws(select.Cond) || Throws(select.OnTrue) || Throws(select.OnFalse);
		case CiCallExpr call:
			foreach (CiExpr arg in call.Arguments) {
				if (Throws(arg))
					return true;
			}
			return ((CiMethod) call.Method.Symbol).Throws || (call.Method.Left != null && Throws(call.Method.Left));
		default:
			throw new NotImplementedException(expr.GetType().Name);
		}
	}

	protected override void WriteExpr(CiExpr expr, CiPriority parent)
	{
		if (Throws(expr))
			Write("try ");
		base.WriteExpr(expr, parent);
	}

	protected override void WriteCoercedExpr(CiType type, CiExpr expr)
	{
		if (Throws(expr))
			Write("try ");
		base.WriteCoercedExpr(type, expr);
	}

	public override void VisitExpr(CiExpr statement)
	{
		if (statement is CiCallExpr call && statement.Type != CiSystem.VoidType)
			Write("_ = ");
		base.VisitExpr(statement);
	}

	void InitVarsAtIndent()
	{
		while (this.VarsAtIndent.Count <= this.Indent) {
			this.VarsAtIndent.Add(new HashSet<string>());
			this.VarBytesAtIndent.Add(false);
		}
		this.VarsAtIndent[this.Indent].Clear();
		this.VarBytesAtIndent[this.Indent] = false;
	}

	bool AddVar(string name) => this.VarsAtIndent[this.Indent].Add(name);

	protected override void OpenChild()
	{
		Write(' ');
		OpenBlock();
		InitVarsAtIndent();
	}

	protected override void CloseChild() => CloseBlock();

	protected override void WriteVar(CiNamedValue def)
	{
		if (def is CiField || AddVar(def.Name)) {
			Write((def.Type is CiClass ? !def.IsAssignableStorage()
				: def.Type is CiArrayStorageType array ? IsArrayRef(array)
				: (def is CiVar local && !local.IsAssigned && !(def.Type is CiStorageType))) ? "let " : "var ");
			base.WriteVar(def);
		}
		else {
			WriteName(def);
			WriteVarInit(def);
		}
	}

	protected override void Write(List<CiStatement> statements)
	{
		// Encoding.UTF8.GetBytes returns void, so it can only be called as a statement
		this.VarBytesAtIndent[this.Indent] = statements.Count(s => s is CiCallExpr call && call.Method.Symbol.Id == CiId.UTF8GetBytes) > 1;
		base.Write(statements);
	}

	public override void VisitLambdaExpr(CiLambdaExpr expr)
	{
		Write("{ ");
		WriteName(expr.First);
		Write(" in ");
		expr.Body.Accept(this, CiPriority.Statement);
		Write(" }");
	}

	protected override void WriteAssertCast(CiBinaryExpr expr)
	{
		Write("let ");
		CiVar def = (CiVar) expr.Right;
		WriteCamelCaseNotKeyword(def.Name);
		Write(" = ");
		expr.Left.Accept(this, CiPriority.Equality /* TODO? */);
		Write(" as! ");
		Write(def.Type.Name);
		WriteLine('?');
	}

	protected override void WriteAssert(CiAssert statement)
	{
		Write("assert(");
		WriteExpr(statement.Cond, CiPriority.Argument);
		if (statement.Message != null) {
			Write(", ");
			WriteExpr(statement.Message, CiPriority.Argument);
		}
		WriteLine(')');
	}

	public override void VisitBreak(CiBreak statement) => WriteLine("break");

	protected override bool NeedCondXcrement(CiLoop loop)
		=> loop.Cond != null && (!loop.HasBreak || !VisitXcrement<CiPostfixExpr>(loop.Cond, false));

	protected override string GetIfNot() => "if !";

	protected override void WriteContinueDoWhile(CiExpr cond)
	{
		VisitXcrement<CiPrefixExpr>(cond, true);
		WriteLine("continue");
	}

	public override void VisitDoWhile(CiDoWhile statement)
	{
		if (VisitXcrement<CiPostfixExpr>(statement.Cond, false))
			base.VisitDoWhile(statement);
		else {
			Write("repeat");
			OpenChild();
			statement.Body.AcceptStatement(this);
			if (statement.Body.CompletesNormally())
				VisitXcrement<CiPrefixExpr>(statement.Cond, true);
			CloseChild();
			Write("while ");
			WriteExpr(statement.Cond, CiPriority.Argument);
			WriteLine();
		}
	}

	protected override void WriteElseIf() => Write("else ");

	protected override void OpenWhile(CiLoop loop)
	{
		if (NeedCondXcrement(loop))
			base.OpenWhile(loop);
		else {
			Write("while true");
			OpenChild();
			VisitXcrement<CiPrefixExpr>(loop.Cond, true);
			Write("let ciDoLoop = ");
			loop.Cond.Accept(this, CiPriority.Argument);
			WriteLine();
			VisitXcrement<CiPostfixExpr>(loop.Cond, true);
			Write("if !ciDoLoop");
			OpenChild();
			WriteLine("break");
			CloseChild();
		}
	}

	protected override void WriteForRange(CiVar iter, CiBinaryExpr cond, long rangeStep)
	{
		if (rangeStep == 1) {
			WriteExpr(iter.Value, CiPriority.Shift);
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
			WriteExpr(iter.Value, CiPriority.Argument);
			switch (cond.Op) {
			case CiToken.Less:
			case CiToken.Greater:
				Write(", to: ");
				WriteExpr(cond.Right, CiPriority.Argument);
				break;
			case CiToken.LessOrEqual:
			case CiToken.GreaterOrEqual:
				Write(", through: ");
				WriteExpr(cond.Right, CiPriority.Argument);
				break;
			default:
				throw new NotImplementedException(cond.Op.ToString());
			}
			Write(", by: ");
			VisitLiteralLong(rangeStep);
			Write(')');
		}
	}

	public override void VisitForeach(CiForeach statement)
	{
		Write("for ");
		if (statement.Count() == 2) {
			Write('(');
			WriteName(statement.GetVar());
			Write(", ");
			WriteName(statement.GetValueVar());
			Write(')');
		}
		else
			WriteName(statement.GetVar());
		Write(" in ");
		CiClassType klass = (CiClassType) statement.Collection.Type;
		switch (klass.Class.Id) {
		case CiId.StringClass:
			statement.Collection.Accept(this, CiPriority.Primary);
			Write(".unicodeScalars");
			break;
		case CiId.SortedDictionaryClass:
			statement.Collection.Accept(this, CiPriority.Primary);
			Write(klass.GetKeyType() == CiSystem.StringPtrType
				? ".sorted(by: { $0.key! < $1.key! })"
				: ".sorted(by: { $0.key < $1.key })");
			break;
		default:
			WriteExpr(statement.Collection, CiPriority.Argument);
			break;
		}
		WriteChild(statement.Body);
	}

	public override void VisitLock(CiLock statement)
	{
		statement.Lock.Accept(this, CiPriority.Primary);
		WriteLine(".lock()");
		Write("do");
		OpenChild();
		Write("defer { ");
		statement.Lock.Accept(this, CiPriority.Primary);
		WriteLine(".unlock() }");
		statement.Body.AcceptStatement(this);
		CloseChild();
	}

	protected override void WriteResultVar()
	{
		Write("let result : ");
		Write(this.CurrentMethod.Type);
	}

	void WriteSwiftCaseBody(CiSwitch statement, List<CiStatement> body)
	{
		this.Indent++;
		VisitXcrement<CiPostfixExpr>(statement.Value, true);
		InitVarsAtIndent();
		WriteCaseBody(body);
		this.Indent--;
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		VisitXcrement<CiPrefixExpr>(statement.Value, true);
		Write("switch ");
		WriteExpr(statement.Value, CiPriority.Argument);
		WriteLine(" {");
		foreach (CiCase kase in statement.Cases) {
			Write("case ");
			for (int i = 0; i < kase.Values.Count; i++) {
				WriteComma(i);
				if (kase.Values[i] is CiVar def) {
					Write("let ");
					WriteCamelCaseNotKeyword(def.Name);
					Write(" as ");
					Write(def.Type);
				}
				else
					WriteCoerced(statement.Value.Type, kase.Values[i], CiPriority.Argument);
			}
			WriteLine(':');
			WriteSwiftCaseBody(statement, kase.Body);
		}
		if (statement.DefaultBody.Count > 0) {
			WriteLine("default:");
			WriteSwiftCaseBody(statement, statement.DefaultBody);
		}
		WriteLine('}');
	}

	public override void VisitThrow(CiThrow statement)
	{
		this.Throw = true;
		VisitXcrement<CiPrefixExpr>(statement.Message, true);
		Write("throw CiError.error(");
		WriteExpr(statement.Message, CiPriority.Argument);
		WriteLine(')');
	}

	void WriteReadOnlyParameter(CiVar param)
	{
		Write("ciParam");
		WritePascalCase(param.Name);
	}

	protected override void WriteParameter(CiVar param)
	{
		Write("_ ");
		if (param.IsAssigned)
			WriteReadOnlyParameter(param);
		else
			WriteName(param);
		Write(" : ");
		Write(param.Type);
	}

	public override void VisitEnumValue(CiConst konst, CiConst previous)
	{
		Write(konst.Documentation);
		Write("static let ");
		WriteName(konst);
		Write(" = ");
		Write(konst.Parent.Name);
		Write('(');
		int i = konst.Value.IntValue();
		if (i == 0)
			Write("[]");
		else {
			Write("rawValue: ");
			VisitLiteralLong(i);
		}
		WriteLine(')');
	}

	protected override void WriteEnum(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		WritePublic(enu);
		if (enu is CiEnumFlags) {
			Write("struct ");
			Write(enu.Name);
			WriteLine(" : OptionSet");
			OpenBlock();
			WriteLine("let rawValue : Int");
			enu.AcceptValues(this);
		}
		else {
			Write("enum ");
			Write(enu.Name);
			if (enu.HasExplicitValue)
				Write(" : Int");
			WriteLine();
			OpenBlock();
			Dictionary<int, CiConst> valueToConst = new Dictionary<int, CiConst>();
			for (CiConst konst = (CiConst) enu.First; konst != null; konst = (CiConst) konst.Next) {
				Write(konst.Documentation);
				int i = konst.Value.IntValue();
				if (valueToConst.TryGetValue(i, out CiConst duplicate)) {
					Write("static let ");
					WriteName(konst);
					Write(" = ");
					WriteName(duplicate);
				}
				else {
					Write("case ");
					WriteName(konst);
					if (!(konst.Value is CiImplicitEnumValue)) {
						Write(" = ");
						VisitLiteralLong(i);
					}
					valueToConst.Add(i, konst);
				}
				WriteLine();
			}
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
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	protected override bool HasInitCode(CiNamedValue def) => throw new NotImplementedException();

	protected override void WriteConst(CiConst konst)
	{
		WriteLine();
		Write(konst.Documentation);
		Write(konst.Visibility);
		Write("static let ");
		WriteName(konst);
		Write(" = ");
		if (konst.Type == CiSystem.IntType || konst.Type is CiEnum || konst.Type == CiSystem.StringPtrType)
			konst.Value.Accept(this, CiPriority.Argument);
		else {
			Write(konst.Type);
			Write('(');
			konst.Value.Accept(this, CiPriority.Argument);
			Write(')');
		}
		WriteLine();
	}

	protected override void WriteField(CiField field)
	{
		WriteLine();
		Write(field.Documentation);
		Write(field.Visibility);
		if (field.Type is CiClassType klass && klass.Class.Id != CiId.StringClass && !(klass is CiDynamicPtrType) && !(klass is CiStorageType))
			Write("unowned ");
		WriteVar(field);
		if (field.Value == null && (field.Type is CiNumericType || field.Type is CiEnum || field.Type == CiSystem.StringStorageType)) {
			Write(" = ");
			WriteDefaultValue(field.Type);
		}
		else if (field.IsAssignableStorage()) {
			Write(" = ");
			WriteName(((CiStorageType) field.Type).Class);
			Write("()");
		}
		WriteLine();
	}

	protected override void WriteParameterDoc(CiVar param, bool first)
	{
		Write("/// - parameter ");
		WriteName(param);
		Write(' ');
		Write(param.Documentation.Summary, false);
		WriteLine();
	}

	protected override void WriteMethod(CiMethod method)
	{
		WriteLine();
		Write(method.Documentation);
		WriteParametersDoc(method);
		switch (method.CallType) {
		case CiCallType.Static:
			Write(method.Visibility);
			Write("static ");
			break;
		case CiCallType.Normal:
			Write(method.Visibility);
			break;
		case CiCallType.Abstract:
		case CiCallType.Virtual:
			Write(method.Visibility == CiVisibility.Internal ? "fileprivate " : "open ");
			break;
		case CiCallType.Override:
			Write(method.Visibility == CiVisibility.Internal ? "fileprivate " : "open ");
			Write("override ");
			break;
		case CiCallType.Sealed:
			Write(method.Visibility);
			Write("final override ");
			break;
		}
		Write("func ");
		WriteName(method);
		WriteParameters(method, true);
		if (method.Throws)
			Write(" throws");
		if (method.Type != CiSystem.VoidType) {
			Write(" -> ");
			Write(method.Type);
		}
		WriteLine();
		OpenBlock();
		if (method.CallType == CiCallType.Abstract)
			WriteLine("preconditionFailure(\"Abstract method called\")");
		else {
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (param.IsAssigned) {
					Write("var ");
					WriteTypeAndName(param);
					Write(" = ");
					WriteReadOnlyParameter(param);
					WriteLine();
				}
			}
			InitVarsAtIndent();
			this.CurrentMethod = method;
			method.Body.AcceptStatement(this);
			this.CurrentMethod = null;
		}
		CloseBlock();
	}

	protected override void WriteClass(CiClass klass, CiProgram program)
	{
		WriteLine();
		Write(klass.Documentation);
		WritePublic(klass);
		if (klass.CallType == CiCallType.Sealed)
			Write("final ");
		OpenClass(klass, "", " : ");

		if (klass.Constructor != null) {
			Write(klass.Constructor.Documentation);
			Write(klass.Constructor.Visibility);
			if (klass.BaseClassName != null)
				Write("override ");
			WriteLine("init()");
			OpenBlock();
			InitVarsAtIndent();
			WriteConstructorBody(klass);
			CloseBlock();
		}

		WriteMembers(klass, true);

		CloseBlock();
	}

	void WriteLibrary()
	{
		if (this.Throw) {
			WriteLine();
			WriteLine("public enum CiError : Error");
			OpenBlock();
			WriteLine("case error(String)");
			CloseBlock();
		}
		if (this.ArrayRef) {
			WriteLine();
			WriteLine("public class ArrayRef<T> : Sequence");
			OpenBlock();
			WriteLine("var array : [T]");
			WriteLine();
			WriteLine("init(_ array : [T])");
			OpenBlock();
			WriteLine("self.array = array");
			CloseBlock();
			WriteLine();
			WriteLine("init(repeating: T, count: Int)");
			OpenBlock();
			WriteLine("self.array = [T](repeating: repeating, count: count)");
			CloseBlock();
			WriteLine();
			WriteLine("init(factory: () -> T, count: Int)");
			OpenBlock();
			WriteLine("self.array = (1...count).map({_ in factory() })");
			CloseBlock();
			WriteLine();
			WriteLine("subscript(index: Int) -> T");
			OpenBlock();
			WriteLine("get");
			OpenBlock();
			WriteLine("return array[index]");
			CloseBlock();
			WriteLine("set(value)");
			OpenBlock();
			WriteLine("array[index] = value");
			CloseBlock();
			CloseBlock();
			WriteLine("subscript(bounds: Range<Int>) -> ArraySlice<T>");
			OpenBlock();
			WriteLine("get");
			OpenBlock();
			WriteLine("return array[bounds]");
			CloseBlock();
			WriteLine("set(value)");
			OpenBlock();
			WriteLine("array[bounds] = value");
			CloseBlock();
			CloseBlock();
			WriteLine();
			WriteLine("func fill(_ value: T)");
			OpenBlock();
			WriteLine("array = [T](repeating: value, count: array.count)");
			CloseBlock();
			WriteLine();
			WriteLine("func fill(_ value: T, _ startIndex : Int, _ count : Int)");
			OpenBlock();
			WriteLine("array[startIndex ..< startIndex + count] = ArraySlice(repeating: value, count: count)");
			CloseBlock();
			WriteLine();
			WriteLine("public func makeIterator() -> IndexingIterator<Array<T>>");
			OpenBlock();
			WriteLine("return array.makeIterator()");
			CloseBlock();
			CloseBlock();
		}
		if (this.StringCharAt) {
			WriteLine();
			WriteLine("fileprivate func ciStringCharAt(_ s: String, _ offset: Int) -> Int");
			OpenBlock();
			WriteLine("return Int(s.unicodeScalars[s.index(s.startIndex, offsetBy: offset)].value)");
			CloseBlock();
		}
		if (this.StringIndexOf) {
			WriteLine();
			WriteLine("fileprivate func ciStringIndexOf<S1 : StringProtocol, S2 : StringProtocol>(_ haystack: S1, _ needle: S2, _ options: String.CompareOptions = .literal) -> Int");
			OpenBlock();
			WriteLine("guard let index = haystack.range(of: needle, options: options) else { return -1 }");
			WriteLine("return haystack.distance(from: haystack.startIndex, to: index.lowerBound)");
			CloseBlock();
		}
		if (this.StringSubstring) {
			WriteLine();
			WriteLine("fileprivate func ciStringSubstring(_ s: String, _ offset: Int) -> Substring");
			OpenBlock();
			WriteLine("return s[s.index(s.startIndex, offsetBy: offset)...]");
			CloseBlock();
		}
	}

	void WriteResources(Dictionary<string, byte[]> resources)
	{
		if (resources.Count == 0)
			return;
		this.ArrayRef = true;
		WriteLine();
		WriteLine("fileprivate final class CiResource");
		OpenBlock();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			Write("static let ");
			WriteResource(name, -1);
			WriteLine(" = ArrayRef<UInt8>([");
			Write('\t');
			Write(resources[name]);
			WriteLine(" ])");
		}
		CloseBlock();
	}

	public override void Write(CiProgram program)
	{
		this.Includes = new SortedSet<string>();
		this.Throw = false;
		this.ArrayRef = false;
		this.StringCharAt = false;
		this.StringIndexOf = false;
		this.StringSubstring = false;
		OpenStringWriter();
		WriteTypes(program);

		CreateFile(this.OutputFile);
		WriteIncludes("import ", "");
		CloseStringWriter();
		WriteLibrary();
		WriteResources(program.Resources);
		CloseFile();
	}
}

}
