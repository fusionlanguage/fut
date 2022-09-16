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

	protected override string DocBullet => "/// * ";

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

	static bool NeedsUnwrap(CiExpr expr)
	{
		if (expr.Type == null)
			return false;
		if (expr is CiSymbolReference symbol && expr.Type is CiClassPtrType) {
			if (symbol.Name == "this")
				return false;
			if (symbol.Symbol.Parent is CiForeach forEach
			 && forEach.Collection.Type is CiArrayStorageType array
			 && array.ElementType is CiClass)
				return false;
		}
		return expr.Type.IsPointer;
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

	protected override void WriteClassName(CiClass klass)
	{
		if (klass == CiSystem.LockClass) {
			Include("Foundation");
			Write("NSRecursiveLock");
		}
		else
			base.WriteClassName(klass);
	}

	static bool IsArrayRef(CiArrayStorageType array) => array.PtrTaken || array.ElementType is CiArrayStorageType || array.ElementType is CiClass;

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
		case CiArrayStorageType arrayStg:
			if (IsArrayRef(arrayStg)) {
				this.ArrayRef = true;
				Write("ArrayRef<");
				Write(arrayStg.ElementType);
				Write('>');
			}
			else {
				Write('[');
				Write(arrayStg.ElementType);
				Write(']');
			}
			break;
		case CiClassType klass:
			if (klass.Class == CiSystem.ArrayPtrClass) {
				this.ArrayRef = true;
				Write("ArrayRef<");
				Write(klass.ElementType);
				Write(">?");
			}
			else if (klass.Class == CiSystem.ListClass || klass.Class == CiSystem.QueueClass || klass.Class == CiSystem.StackClass) {
				Write('[');
				Write(klass.ElementType);
				Write(']');
			}
			else if (klass.Class == CiSystem.HashSetClass) {
				Write("Set<");
				Write(klass.ElementType);
				Write('>');
			}
			else if (klass.Class.TypeParameterCount == 2) {
				Write('[');
				Write(klass.KeyType);
				Write(": ");
				Write(klass.ValueType);
				Write(']');
			}
			else
				throw new NotImplementedException();
			break;
		default:
			Write(type.Name);
			break;
		}
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
		if (!value.Type.IsFinal || value.IsAssignableStorage) {
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
		if (klass.Class == CiSystem.ArrayStorageClass || klass.Class == CiSystem.ListClass)
			return klass.ElementType == CiSystem.StringStorageType;
		if (klass.Class.TypeParameterCount == 2)
			return (symbol.Symbol == loop.Element ? klass.KeyType : klass.ValueType) == CiSystem.StringStorageType;
		throw new NotImplementedException();
	}

	void WriteUnwrappedString(CiExpr expr, CiPriority parent, bool substringOk)
	{
		if (!(expr is CiLiteral) && expr.Type == CiSystem.StringPtrType && !IsForeachStringStg(expr)) {
			expr.Accept(this, CiPriority.Primary);
			Write('!');
		}
		else if (!substringOk && expr is CiCallExpr call && call.Method.IsReferenceTo(CiSystem.StringSubstring))
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
			if (type is CiIntegerType && expr is CiCallExpr call && call.Method.IsReferenceTo(CiSystem.MathTruncate))
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
		if (expr.Left != null && expr.Left.IsReferenceTo(CiSystem.MathClass)) {
			Write(expr.Symbol == CiSystem.MathNaN ? "Float.nan"
				: expr.Symbol == CiSystem.MathNegativeInfinity ? "-Float.infinity"
				: expr.Symbol == CiSystem.MathPositiveInfinity ? "Float.infinity"
				: throw new NotImplementedException(expr.ToString()));
			return expr;
		}
		return base.VisitSymbolReference(expr, parent);
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
		if (obj == null) {
			if (method.IsStatic) {
				WriteName(this.CurrentMethod.Parent);
				Write('.');
			}
			WriteName(method);
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.StringContains)
			WriteStringContains(obj, "contains", args);
		else if (method == CiSystem.StringStartsWith)
			WriteStringContains(obj, "hasPrefix", args);
		else if (method == CiSystem.StringEndsWith)
			WriteStringContains(obj, "hasSuffix", args);
		else if (method == CiSystem.StringIndexOf) {
			Include("Foundation");
			this.StringIndexOf = true;
			Write("ciStringIndexOf(");
			WriteUnwrappedString(obj, CiPriority.Argument, true);
			Write(", ");
			WriteUnwrappedString(args[0], CiPriority.Argument, true);
			Write(')');
		}
		else if (method == CiSystem.StringLastIndexOf) {
			Include("Foundation");
			this.StringIndexOf = true;
			Write("ciStringIndexOf(");
			WriteUnwrappedString(obj, CiPriority.Argument, true);
			Write(", ");
			WriteUnwrappedString(args[0], CiPriority.Argument, true);
			Write(", .backwards)");
		}
		else if (method == CiSystem.StringSubstring) {
			if (args[0].IsLiteralZero)
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
		}
		else if (method == CiSystem.CollectionCopyTo) {
			OpenIndexing(args[1]);
			WriteRange(args[2], args[3]);
			Write("] = ");
			OpenIndexing(obj);
			WriteRange(args[0], args[3]);
			Write(']');
		}
		else if (method == CiSystem.ArrayFillAll && obj.Type is CiArrayStorageType array && !IsArrayRef(array)) {
			obj.Accept(this, CiPriority.Assign);
			Write(" = [");
			Write(array.ElementType);
			Write("](repeating: ");
			WriteCoerced(array.ElementType, args[0], CiPriority.Argument);
			Write(", count: ");
			VisitLiteralLong(array.Length);
			Write(')');
		}
		else if (method == CiSystem.ArrayFillPart && obj.Type is CiArrayStorageType array2 && !IsArrayRef(array2)) {
			OpenIndexing(obj);
			WriteRange(args[1], args[2]);
			Write("] = ArraySlice(repeating: ");
			WriteCoerced(array2.ElementType, args[0], CiPriority.Argument);
			Write(", count: ");
			WriteCoerced(CiSystem.IntType, args[2], CiPriority.Argument); // FIXME: side effect
			Write(')');
		}
		else if (method == CiSystem.CollectionSortAll && obj.Type is CiArrayStorageType array3) {
			obj.Accept(this, CiPriority.Primary);
			Write("[0..<");
			VisitLiteralLong(array3.Length);
			Write("].sort()");
		}
		else if (method == CiSystem.CollectionSortPart) {
			OpenIndexing(obj);
			WriteRange(args[0], args[1]);
			Write("].sort()");
		}
		else if (method == CiSystem.ListAdd || method == CiSystem.QueueEnqueue || method == CiSystem.StackPush)
			WriteListAppend(obj, args);
		else if (method == CiSystem.ListInsert) {
			obj.Accept(this, CiPriority.Primary);
			Write(".insert(");
			CiType elementType = ((CiClassType) obj.Type).ElementType;
			if (args.Count == 1)
				WriteNewStorage(elementType);
			else
				WriteCoerced(elementType, args[1], CiPriority.Argument);
			Write(", at: ");
			WriteCoerced(CiSystem.IntType, args[0], CiPriority.Argument);
			Write(')');
		}
		else if (method == CiSystem.ListRemoveAt) {
			obj.Accept(this, CiPriority.Primary);
			Write(".remove(at: ");
			WriteCoerced(CiSystem.IntType, args[0], CiPriority.Argument);
			Write(')');
		}
		else if (method == CiSystem.ListRemoveRange) {
			obj.Accept(this, CiPriority.Primary);
			Write(".removeSubrange(");
			WriteRange(args[0], args[1]);
			Write(')');
		}
		else if (method == CiSystem.QueueDequeue) {
			obj.Accept(this, CiPriority.Primary);
			Write(".removeFirst()");
		}
		else if (method == CiSystem.QueuePeek) {
			obj.Accept(this, CiPriority.Primary);
			Write(".first");
		}
		else if (method == CiSystem.StackPeek) {
			obj.Accept(this, CiPriority.Primary);
			Write(".last");
		}
		else if (method == CiSystem.StackPop) {
			obj.Accept(this, CiPriority.Primary);
			Write(".removeLast()");
		}
		else if (WriteDictionaryAdd(obj, method, args)) {
			// done
		}
		else if (method == CiSystem.DictionaryContainsKey) {
			if (parent > CiPriority.Equality)
				Write('(');
			WriteIndexing(obj, args[0]);
			Write(" != nil");
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else if (method == CiSystem.DictionaryRemove) {
			obj.Accept(this, CiPriority.Primary);
			Write(".removeValue(forKey: ");
			args[0].Accept(this, CiPriority.Argument);
			Write(')');
		}
		else if (method == CiSystem.ConsoleWrite) {
			// TODO: stderr
			Write("print(");
			args[0].Accept(this, CiPriority.Argument);
			Write(", terminator: \"\")");
		}
		else if (method == CiSystem.ConsoleWriteLine) {
			// TODO: stderr
			Write("print");
			WriteArgsInParentheses(method, args);
		}
		else if (method == CiSystem.UTF8GetByteCount) {
			WriteUnwrappedString(args[0], CiPriority.Primary, true);
			Write(".utf8.count");
		}
		else if (method == CiSystem.UTF8GetBytes) {
			if (AddVar("cibytes"))
				Write(this.VarBytesAtIndent[this.Indent] ? "var " : "let ");
			Write("cibytes = [UInt8](");
			WriteUnwrappedString(args[0], CiPriority.Primary, true);
			WriteLine(".utf8)");
			OpenIndexing(args[1]);
			WriteCoerced(CiSystem.IntType, args[2], CiPriority.Shift);
			if (args[2].IsLiteralZero)
				Write("..<");
			else {
				Write(" ..< ");
				WriteCoerced(CiSystem.IntType, args[2], CiPriority.Add); // TODO: side effect
				Write(" + ");
			}
			WriteLine("cibytes.count] = cibytes[...]");
		}
		else if (method == CiSystem.UTF8GetString) {
			Write("String(decoding: ");
			OpenIndexing(args[0]);
			WriteRange(args[1], args[2]);
			Write("], as: UTF8.self)");
		}
		else if (method == CiSystem.EnvironmentGetEnvironmentVariable) {
			Include("Foundation");
			Write("ProcessInfo.processInfo.environment[");
			args[0].Accept(this, CiPriority.Argument);
			Write(']');
		}
		else if (obj.IsReferenceTo(CiSystem.MathClass)) {
			if (method == CiSystem.MathIsFinite) {
				args[0].Accept(this, CiPriority.Primary);
				Write(".isFinite");
			}
			else if (method == CiSystem.MathIsInfinity) {
				args[0].Accept(this, CiPriority.Primary);
				Write(".isInfinite");
			}
			else if (method == CiSystem.MathIsNaN) {
				args[0].Accept(this, CiPriority.Primary);
				Write(".isNaN");
			}
			else {
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
		}
		else {
			if (obj.IsReferenceTo(CiSystem.BasePtr))
				Write("super");
			else
				obj.Accept(this, CiPriority.Primary);
			WriteMemberOp(obj, null);
			if (method == CiSystem.CollectionClear)
				Write("removeAll");
			else if (method == CiSystem.HashSetAdd)
				Write("insert");
			else
				WriteName(method);
			WriteArgsInParentheses(method, args);
		}
	}

	protected override void WriteNewArray(CiArrayStorageType array)
	{
		if (IsArrayRef(array))
			base.WriteNewArray(array);
		else {
			Write('[');
			Write(array.ElementType);
			Write("](repeating: ");
			WriteDefaultValue(array.ElementType);
			Write(", count: ");
			VisitLiteralLong(array.Length);
			Write(')');
		}
	}

	protected override void WriteNewStorage(CiStorageType storage)
	{
		Write(storage);
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
				WriteName(type.First());
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
		if (elementType is CiArrayStorageType) {
			Write("factory: { ");
			WriteNewStorage(elementType);
			Write(" }");
		}
		else if (elementType is CiClass) {
			Write("factory: ");
			WriteName(elementType);
			Write(".init");
		}
		else {
			Write("repeating: ");
			WriteDefaultValue(elementType);
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
			if (expr is CiSymbolReference || expr is CiSelectExpr || expr is CiCallExpr || expr.IsIndexing) {
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
			if (right is CiBinaryExpr rightBinary && rightBinary.IsAssign) {
				VisitBinaryExpr(rightBinary, CiPriority.Statement);
				WriteLine();
				right = rightBinary.Left; // TODO: side effect
			}
			expr.Left.Accept(this, CiPriority.Assign);
			Write(' ');
			Write(expr.OpString);
			Write(' ');
			if (right is CiLiteralNull
			 && expr.Left is CiBinaryExpr leftBinary
			 && leftBinary.Op == CiToken.LeftBracket
			 && leftBinary.Left.Type is CiClassType dict
			 && dict.Class.TypeParameterCount == 2) {
				Write(dict.ValueType);
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
			Write((def.Type is CiClass ? !def.IsAssignableStorage
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
		this.VarBytesAtIndent[this.Indent] = statements.Count(s => s is CiCallExpr call && call.Method.Symbol == CiSystem.UTF8GetBytes) > 1;
		base.Write(statements);
	}

	public override void VisitAssert(CiAssert statement)
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
			statement.Body.Accept(this);
			if (statement.Body.CompletesNormally)
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
		if (statement.Collection.Type is CiClassType dict && dict.Class == CiSystem.SortedDictionaryClass) {
			statement.Collection.Accept(this, CiPriority.Primary);
			Write(dict.KeyType == CiSystem.StringPtrType
				? ".sorted(by: { $0.key! < $1.key! })"
				: ".sorted(by: { $0.key < $1.key })");
		}
		else
			WriteExpr(statement.Collection, CiPriority.Argument);
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
		statement.Body.Accept(this);
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

	void WriteEnum(CiEnum enu)
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
			foreach (CiConst konst in enu) {
				Write(konst.Documentation);
				Write("static let ");
				WriteName(konst);
				Write(" = ");
				Write(enu.Name);
				Write('(');
				int i = konst.Value.IntValue;
				if (i == 0)
					Write("[]");
				else {
					Write("rawValue: ");
					VisitLiteralLong(i);
				}
				WriteLine(')');
			}
		}
		else {
			Write("enum ");
			Write(enu.Name);
			if (enu.Any(symbol => !(((CiConst) symbol).Value is CiImplicitEnumValue)))
				Write(" : Int");
			WriteLine();
			OpenBlock();
			Dictionary<int, CiConst> valueToConst = new Dictionary<int, CiConst>();
			foreach (CiConst konst in enu) {
				Write(konst.Documentation);
				int i = konst.Value.IntValue;
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
		if (field.Type.IsPointer && field.Type != CiSystem.StringPtrType && !field.Type.IsDynamicPtr)
			Write("unowned ");
		WriteVar(field);
		if (field.Value == null && (field.Type is CiNumericType || field.Type is CiEnum || field.Type == CiSystem.StringStorageType)) {
			Write(" = ");
			WriteDefaultValue(field.Type);
		}
		else if (field.IsAssignableStorage) {
			Write(" = ");
			WriteName(field.Type);
			Write("()");
		}
		WriteLine();
	}

	protected override void WriteMethod(CiMethod method)
	{
		WriteLine();
		Write(method.Documentation);
		foreach (CiVar param in method.Parameters) {
			if (param.Documentation != null) {
				Write("/// - parameter ");
				WriteName(param);
				Write(' ');
				Write(param.Documentation.Summary, false);
				WriteLine();
			}
		}
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
			foreach (CiVar param in method.Parameters) {
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
			method.Body.Accept(this);
			this.CurrentMethod = null;
		}
		CloseBlock();
	}

	void WriteClass(CiClass klass)
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
		foreach (CiContainerType type in program) {
			switch (type) {
			case CiEnum enu:
				WriteEnum(enu);
				break;
			case CiClass klass:
				WriteClass(klass);
				break;
			default:
				throw new NotImplementedException(type.Type.ToString());
			}
		}

		CreateFile(this.OutputFile);
		WriteIncludes("import ", "");
		CloseStringWriter();
		WriteLibrary();
		WriteResources(program.Resources);
		CloseFile();
	}
}

}
