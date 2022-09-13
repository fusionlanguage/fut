// GenCpp.cs - C++ code generator
//
// Copyright (C) 2011-2022  Piotr Fusik
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
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

public class GenCpp : GenCCpp
{
	bool UsingStringViewLiterals;

	protected override void IncludeStdInt() => Include("cstdint");

	protected override void IncludeAssert() => Include("cassert");

	protected override void IncludeMath() => Include("cmath");

	public override void VisitLiteralNull() => Write("nullptr");

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		Include("format");
		Write("std::format(\"");
		foreach (CiInterpolatedPart part in expr.Parts) {
			WriteDoubling(part.Prefix, '{');
			Write("{}");
		}
		WriteDoubling(expr.Suffix, '{');
		Write('"');
		WriteArgs(expr);
		Write(')');
		return expr;
	}

	void WriteCamelCaseNotKeyword(string name)
	{
		WriteCamelCase(name);
		switch (name) {
		case "And":
		case "Asm":
		case "Auto":
		case "Bool":
		case "Break":
		case "Byte":
		case "Case":
		case "Catch":
		case "Char":
		case "Class":
		case "Const":
		case "Continue":
		case "Default":
		case "Delete":
		case "Do":
		case "Double":
		case "Else":
		case "Enum":
		case "Explicit":
		case "Export":
		case "Extern":
		case "False":
		case "Float":
		case "For":
		case "Goto":
		case "If":
		case "Inline":
		case "Int":
		case "Long":
		case "Namespace":
		case "New":
		case "Not":
		case "Nullptr":
		case "Operator":
		case "Or":
		case "Override":
		case "Private":
		case "Protected":
		case "Public":
		case "Register":
		case "Return":
		case "Short":
		case "Signed":
		case "Sizeof":
		case "Static":
		case "Struct":
		case "Switch":
		case "Throw":
		case "True":
		case "Try":
		case "Typedef":
		case "Union":
		case "Unsigned":
		case "Using":
		case "Virtual":
		case "Void":
		case "Volatile":
		case "While":
		case "and":
		case "asm":
		case "auto":
		case "catch":
		case "char":
		case "delete":
		case "explicit":
		case "export":
		case "extern":
		case "goto":
		case "inline":
		case "namespace":
		case "not":
		case "nullptr":
		case "operator":
		case "or":
		case "private":
		case "register":
		case "signed":
		case "sizeof":
		case "struct":
		case "try":
		case "typedef":
		case "union":
		case "unsigned":
		case "using":
		case "volatile":
			Write('_');
			break;
		default:
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
			WriteCamelCaseNotKeyword(symbol.Name);
			break;
		case CiMember _:
			if (symbol == CiSystem.CollectionCount)
				Write("size()");
			else
				WriteCamelCaseNotKeyword(symbol.Name);
			break;
		default:
			throw new NotImplementedException(symbol.GetType().Name);
		}
	}

	protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
	{
		if (symbol is CiField)
			Write("this->");
		WriteName(symbol);
	}

	void WriteCollectionType(string name, CiType elementType)
	{
		Include(name);
		Write("std::");
		Write(name);
		Write('<');
		Write(elementType, false);
		Write('>');
	}

	void WriteBaseType(CiType type)
	{
		if (type == CiSystem.MatchClass) {
			Include("regex");
			Write("std::cmatch");
		}
		else if (type == CiSystem.LockClass) {
			Include("mutex");
			Write("std::recursive_mutex");
		}
		else
			Write(type.Name);
	}

	protected override void Write(CiType type, bool promote)
	{
		switch (type) {
		case CiIntegerType integer:
			Write(GetIntegerTypeCode(integer, promote));
			break;
		case CiStringPtrType _:
			Include("string_view");
			Write("std::string_view");
			break;
		case CiStringStorageType _:
			Include("string");
			Write("std::string");
			break;
		case CiArrayPtrType arrayPtr:
			switch (arrayPtr.Modifier) {
			case CiToken.EndOfFile:
				Write(arrayPtr.ElementType, false);
				Write(" const *");
				break;
			case CiToken.ExclamationMark:
				Write(arrayPtr.ElementType, false);
				Write(" *");
				break;
			case CiToken.Hash:
				Include("memory");
				Write("std::shared_ptr<");
				Write(arrayPtr.ElementType, false);
				Write("[]>");
				break;
			default:
				throw new NotImplementedException(arrayPtr.Modifier.ToString());
			}
			break;
		case CiArrayStorageType arrayStorage:
			Include("array");
			Write("std::array<");
			Write(arrayStorage.ElementType, false);
			Write(", ");
			VisitLiteralLong(arrayStorage.Length);
			Write('>');
			break;
		case CiClassType klass:
			string cppType;
			if (klass.Class == CiSystem.ListClass)
				cppType = "vector";
			else if (klass.Class == CiSystem.QueueClass)
				cppType = "queue";
			else if (klass.Class == CiSystem.StackClass)
				cppType = "stack";
			else if (klass.Class == CiSystem.HashSetClass)
				cppType = "unordered_set";
			else if (klass.Class == CiSystem.DictionaryClass)
				cppType = "unordered_map";
			else if (klass.Class == CiSystem.SortedDictionaryClass)
				cppType = "map";
			else
				throw new NotImplementedException();
			Include(cppType);
			if (!(klass is CiReadWriteClassType))
				Write("const ");
			Write("std::");
			Write(cppType);
			Write('<');
			Write(klass.TypeArg0, false);
			if (klass.Class.TypeParameterCount == 2) {
				Write(", ");
				Write(klass.ValueType, false);
			}
			Write('>');
			if (!(klass is CiStorageType))
				Write(" *");
			break;
		case CiClassPtrType classPtr:
			switch (classPtr.Modifier) {
			case CiToken.EndOfFile:
				Write("const ");
				if (classPtr.Class == CiSystem.RegexClass) {
					Include("regex");
					Write("std::regex");
				}
				else
					WriteBaseType(classPtr.Class);
				Write(" *");
				break;
			case CiToken.ExclamationMark:
				Write(classPtr.Class.Name);
				Write(" *");
				break;
			case CiToken.Hash:
				if (classPtr.Class == CiSystem.RegexClass) {
					Include("regex");
					Write("std::regex");
				}
				else {
					Include("memory");
					Write("std::shared_ptr<");
					Write(classPtr.Class.Name);
					Write('>');
				}
				break;
			default:
				throw new NotImplementedException(classPtr.Modifier.ToString());
			}
			break;
		default:
			WriteBaseType(type);
			break;
		}
	}

	protected override void WriteNew(CiClass klass, CiPriority parent)
	{
		Include("memory");
		Write("std::make_shared<");
		Write(klass.Name);
		Write(">()");
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Include("memory");
		Write("std::make_shared<");
		Write(elementType, false);
		Write("[]>(");
		lengthExpr.Accept(this, CiPriority.Argument);
		Write(')');
	}

	protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
	{
		switch (value) {
		case null:
			break;
		case CiLiteral literal when literal.IsDefaultValue:
			Write(" {}");
			break;
		default:
			throw new NotImplementedException("Only null, zero and false supported");
		}
	}

	protected override void WriteVarInit(CiNamedValue def)
	{
		if (def.Value != null && def.Type == CiSystem.StringStorageType) {
			Write('{');
			def.Value.Accept(this, CiPriority.Argument);
			Write('}');
		}
		else if (def.Type is CiStorageType) {
		}
		else
			base.WriteVarInit(def);
	}

	protected override void WriteStaticCast(CiType type, CiExpr expr)
	{
		Write("static_cast<");
		Write(type, false);
		Write(">(");
		GetStaticCastInner(type, expr).Accept(this, CiPriority.Argument);
		Write(')');
	}

	protected override bool HasInitCode(CiNamedValue def) => false;

	protected override void WriteInitCode(CiNamedValue def)
	{
	}

	static bool NeedStringPtrData(CiExpr expr)
	{
		if (expr.Type != CiSystem.StringPtrType)
			return false;
		if (expr is CiCallExpr call && call.Method.Symbol == CiSystem.EnvironmentGetEnvironmentVariable)
			return false;
		return true;
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if (NeedStringPtrData(expr.Left) && expr.Right.Type == CiSystem.NullType) {
			WriteCoerced(CiSystem.StringPtrType, expr.Left, CiPriority.Primary);
			Write(".data()");
			Write(GetEqOp(not));
			Write("nullptr");
		}
		else if (expr.Left.Type == CiSystem.NullType && NeedStringPtrData(expr.Right)) {
			Write("nullptr");
			Write(GetEqOp(not));
			WriteCoerced(CiSystem.StringPtrType, expr.Right, CiPriority.Primary);
			Write(".data() ");
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	static bool IsCppPtr(CiExpr expr)
	{
		if (expr.Type is CiClassPtrType
		 || (expr.Type is CiClassType ptr && ptr.IsPointer && ptr.Class != CiSystem.ArrayPtrClass)) {
			if (expr is CiSymbolReference symbol
			 && symbol.Symbol.Parent is CiForeach loop
			 && loop.Collection.Type is CiArrayStorageType array
			 && array.ElementType is CiClass)
				return false; // C++ reference
			return true; // C++ pointer
		}
		return false;
	}

	protected override void WriteIndexing(CiBinaryExpr expr, CiPriority parent)
	{
		if (expr.Left.Type is CiClassType ptr && ptr.IsPointer && ptr.Class != CiSystem.ArrayPtrClass) {
			Write("(*");
			expr.Left.Accept(this, CiPriority.Primary);
			Write(")[");
			expr.Right.Accept(this, CiPriority.Argument);
			Write(']');
		}
		else
			base.WriteIndexing(expr, parent);
	}

	protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
	{
		if (symbol != null && symbol.Symbol is CiConst) // FIXME
			Write("::");
		else if (IsCppPtr(left))
			Write("->");
		else
			Write('.');
	}

	void StartMethodCall(CiExpr obj)
	{
		obj.Accept(this, CiPriority.Primary);
		WriteMemberOp(obj, null);
	}

	void WriteCollectionObject(CiExpr obj, CiPriority priority)
	{
		if (obj.Type is CiStorageType)
			obj.Accept(this, priority);
		else {
			Write('*');
			obj.Accept(this, CiPriority.Primary);
		}
	}

	void StartStringMethod(CiExpr obj)
	{
		obj.Accept(this, CiPriority.Primary);
		if (obj is CiLiteral) {
			this.UsingStringViewLiterals = true;
			Write("sv");
		}
		Write('.');
	}

	void WriteStringMethod(CiExpr obj, string name, CiMethod method, List<CiExpr> args)
	{
		StartStringMethod(obj);
		Write(name);
		if (IsOneAsciiString(args[0], out char c)) {
			Write('(');
			VisitLiteralChar(c);
			Write(')');
		}
		else
			WriteArgsInParentheses(method, args);
	}

	void WriteRegex(List<CiExpr> args, int argIndex)
	{
		Include("regex");
		Write("std::regex(");
		args[argIndex].Accept(this, CiPriority.Argument);
		WriteRegexOptions(args, ", std::regex::ECMAScript | ", " | ", "", "std::regex::icase", "std::regex::multiline", "std::regex::NOT_SUPPORTED_singleline");
		Write(')');
	}

	void WriteConsoleWrite(CiExpr obj, List<CiExpr> args, bool newLine)
	{
		Include("iostream");
		Write(obj.IsReferenceTo(CiSystem.ConsoleError) ? "std::cerr" : "std::cout");
		if (args.Count == 1) {
			if (args[0] is CiInterpolatedString interpolated) {
				bool uppercase = false;
				bool hex = false;
				char flt = 'G';
				foreach (CiInterpolatedPart part in interpolated.Parts) {
					switch (part.Format) {
					case 'E':
					case 'G':
					case 'X':
						if (!uppercase) {
							Write(" << std::uppercase");
							uppercase = true;
						}
						break;
					case 'e':
					case 'g':
					case 'x':
						if (uppercase) {
							Write(" << std::nouppercase");
							uppercase = false;
						}
						break;
					default:
						break;
					}

					switch (part.Format) {
					case 'E':
					case 'e':
						if (flt != 'E') {
							Write(" << std::scientific");
							flt = 'E';
						}
						break;
					case 'F':
					case 'f':
						if (flt != 'F') {
							Write(" << std::fixed");
							flt = 'F';
						}
						break;
					case 'X':
					case 'x':
						if (!hex) {
							Write(" << std::hex");
							hex = true;
						}
						break;
					default:
						if (hex) {
							Write(" << std::dec");
							hex = false;
						}
						if (flt != 'G') {
							Write(" << std::defaultfloat");
							flt = 'G';
						}
						break;
					}

					if (part.Prefix.Length > 0) {
						Write(" << ");
						VisitLiteralString(part.Prefix);
					}

					Write(" << ");
					part.Argument.Accept(this, CiPriority.Mul);
				}

				if (uppercase)
					Write(" << std::nouppercase");
				if (hex)
					Write(" << std::dec");
				if (flt != 'G')
					Write(" << std::defaultfloat");
				if (interpolated.Suffix.Length > 0) {
					Write(" << ");
					if (newLine) {
						WriteStringLiteralWithNewLine(interpolated.Suffix);
						return;
					}
					VisitLiteralString(interpolated.Suffix);
				}
			}
			else {
				Write(" << ");
				if (newLine && args[0] is CiLiteralString literal) {
					WriteStringLiteralWithNewLine(literal.Value);
					return;
				}
				args[0].Accept(this, CiPriority.Mul);
			}
		}
		if (newLine)
			Write(" << '\\n'");
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		if (obj == null) {
			WriteName(method);
			WriteArgsInParentheses(method, args);
		}
		else if (obj.IsReferenceTo(CiSystem.MathClass)) {
			Include("cmath");
			Write("std::");
			WriteMathCall(method, args);
		}
		else if (method == CiSystem.StringContains) {
			if (parent > CiPriority.Equality)
				Write('(');
			WriteStringMethod(obj, "find", method, args);
			Write(" != std::string::npos");
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else if (method == CiSystem.StringIndexOf) {
			Write("static_cast<int>(");
			WriteStringMethod(obj, "find", method, args);
			Write(')');
		}
		else if (method == CiSystem.StringLastIndexOf) {
			Write("static_cast<int>(");
			WriteStringMethod(obj, "rfind", method, args);
			Write(')');
		}
		else if (method == CiSystem.StringStartsWith)
			WriteStringMethod(obj, "starts_with", method, args);
		else if (method == CiSystem.StringEndsWith)
			WriteStringMethod(obj, "ends_with", method, args);
		else if (method == CiSystem.StringSubstring)
			WriteStringMethod(obj, "substr", method, args);
		else if (method == CiSystem.ArrayBinarySearchAll || method == CiSystem.ArrayBinarySearchPart) {
			Include("algorithm");
			if (parent > CiPriority.Add)
				Write('(');
			Write("std::lower_bound(");
			if (args.Count == 1) {
				StartMethodCall(obj);
				Write("begin(), ");
				StartMethodCall(obj); // FIXME: side effect
				Write("end()");
			}
			else {
				WriteArrayPtrAdd(obj, args[1]);
				Write(", ");
				WriteArrayPtrAdd(obj, args[1]); // FIXME: side effect
				Write(" + ");
				args[2].Accept(this, CiPriority.Add);
			}
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(") - ");
			WriteArrayPtr(obj, CiPriority.Mul);
			if (parent > CiPriority.Add)
				Write(')');
		}
		else if (method == CiSystem.CollectionCopyTo) {
			Include("algorithm");
			Write("std::copy_n(");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			args[3].Accept(this, CiPriority.Argument);
			Write(", ");
			WriteArrayPtrAdd(args[1], args[2]);
			Write(')');
		}
		else if (method == CiSystem.ArrayFillAll) {
			StartMethodCall(obj);
			Write("fill(");
			WriteCoerced(((CiClassType) obj.Type).ElementType, args[0], CiPriority.Argument);
			Write(')');
		}
		else if (method == CiSystem.ArrayFillPart) {
			Include("algorithm");
			Write("std::fill_n(");
			WriteArrayPtrAdd(obj, args[1]);
			Write(", ");
			args[2].Accept(this, CiPriority.Argument);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(')');
		}
		else if (method == CiSystem.CollectionSortAll) {
			Include("algorithm");
			Write("std::sort(");
			StartMethodCall(obj);
			Write("begin(), ");
			StartMethodCall(obj); // FIXME: side effect
			Write("end())");
		}
		else if (method == CiSystem.CollectionSortPart) {
			Include("algorithm");
			Write("std::sort(");
			WriteArrayPtrAdd(obj, args[0]);
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			Write(" + ");
			args[1].Accept(this, CiPriority.Add);
			Write(')');
		}
		else if (method == CiSystem.ListAdd) {
			StartMethodCall(obj);
			if (args.Count == 0)
				Write("emplace_back()");
			else {
				Write("push_back(");
				WriteCoerced(((CiClassType) obj.Type).ElementType, args[0], CiPriority.Argument);
				Write(')');
			}
		}
		else if (method == CiSystem.ListContains) {
			Include("algorithm");
			if (parent > CiPriority.Equality)
				Write('(');
			Write("std::find(");
			StartMethodCall(obj);
			Write("begin(), ");
			StartMethodCall(obj); // FIXME: side effect
			Write("end(), ");
			args[0].Accept(this, CiPriority.Argument);
			Write(") != ");
			StartMethodCall(obj); // FIXME: side effect
			Write("end()");
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else if (method == CiSystem.ListInsert) {
			StartMethodCall(obj);
			if (args.Count == 1) {
				Write("emplace(");
				WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			}
			else {
				Write("insert(");
				WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
				Write(", ");
				WriteCoerced(((CiClassType) obj.Type).ElementType, args[1], CiPriority.Argument);
			}
			Write(')');
		}
		else if (method == CiSystem.ListRemoveAt) {
			StartMethodCall(obj);
			Write("erase(");
			WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			Write(')');
		}
		else if (method == CiSystem.ListRemoveRange) {
			StartMethodCall(obj);
			Write("erase(");
			WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			Write(", ");
			WriteArrayPtrAdd(obj, args[0]); // FIXME: side effect
			Write(" + ");
			args[1].Accept(this, CiPriority.Add);
			Write(')');
		}
		else if (obj.Type is CiClassType klass && (klass.Class == CiSystem.QueueClass || klass.Class == CiSystem.StackClass) && method == CiSystem.CollectionClear) {
			WriteCollectionObject(obj, CiPriority.Assign);
			Write(" = {}");
		}
		else if (method == CiSystem.QueueDequeue) {
			if (parent == CiPriority.Statement) {
				StartMethodCall(obj);
				Write("pop()");
			}
			else {
				// :-)
				CiType elementType = ((CiClassType) obj.Type).ElementType;
				Write("[](");
				WriteCollectionType("queue", elementType);
				Write(" &q) { ");
				Write(elementType, false);
				Write(" front = q.front(); q.pop(); return front; }(");
				WriteCollectionObject(obj, CiPriority.Argument);
				Write(')');
			}
		}
		else if (method == CiSystem.QueueEnqueue)
			WriteCall(obj, "push", args[0]);
		else if (method == CiSystem.QueuePeek) {
			StartMethodCall(obj);
			Write("front()");
		}
		else if (method == CiSystem.StackPeek) {
			StartMethodCall(obj);
			Write("top()");
		}
		else if (method == CiSystem.StackPop && parent != CiPriority.Statement) {
			// :-)
			CiType elementType = ((CiClassType) obj.Type).ElementType;
			Write("[](");
			WriteCollectionType("stack", elementType);
			Write(" &s) { ");
			Write(elementType, false);
			Write(" top = s.top(); s.pop(); return top; }(");
			WriteCollectionObject(obj, CiPriority.Argument);
			Write(')');
		}
		else if (method == CiSystem.HashSetAdd)
			WriteCall(obj, "insert", args[0]);
		else if (method == CiSystem.HashSetRemove || method == CiSystem.DictionaryRemove)
			WriteCall(obj, "erase", args[0]);
		else if (method == CiSystem.DictionaryAdd)
			WriteIndexing(obj, args[0]);
		else if (method == CiSystem.DictionaryContainsKey) {
			if (parent > CiPriority.Equality)
				Write('(');
			StartMethodCall(obj);
			Write("count");
			WriteArgsInParentheses(method, args);
			Write(" != 0");
			if (parent > CiPriority.Equality)
				Write(')');
		}
		else if (method == CiSystem.UTF8GetByteCount) {
			if (args[0] is CiLiteral) {
				if (parent > CiPriority.Add)
					Write('(');
				Write("sizeof(");
				args[0].Accept(this, CiPriority.Argument);
				Write(") - 1");
				if (parent > CiPriority.Add)
					Write(')');
			}
			else
				WriteStringLength(args[0]);
		}
		else if (method == CiSystem.UTF8GetBytes) {
			if (args[0] is CiLiteral) {
				Include("algorithm");
				Write("std::copy_n(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", sizeof(");
				args[0].Accept(this, CiPriority.Argument);
				Write(") - 1, ");
				WriteArrayPtrAdd(args[1], args[2]);
				Write(')');
			}
			else {
				args[0].Accept(this, CiPriority.Primary);
				Write(".copy(reinterpret_cast<char *>("); // cast pointer signedness
				WriteArrayPtrAdd(args[1], args[2]);
				Write("), ");
				args[0].Accept(this, CiPriority.Primary); // FIXME: side effect
				Write(".size())");
			}
		}
		else if (method == CiSystem.RegexCompile)
			WriteRegex(args, 0);
		else if (method == CiSystem.RegexIsMatchStr || method == CiSystem.RegexIsMatchRegex
				|| method == CiSystem.MatchFindStr || method == CiSystem.MatchFindRegex) {
			Write("std::regex_search(");
			if (args[0].Type == CiSystem.StringPtrType && !(args[0] is CiLiteral)) {
				args[0].Accept(this, CiPriority.Primary);
				Write(".begin(), ");
				args[0].Accept(this, CiPriority.Primary); // FIXME: side effect
				Write(".end()");
			}
			else
				args[0].Accept(this, CiPriority.Argument);
			if (method == CiSystem.MatchFindStr || method == CiSystem.MatchFindRegex) {
				Write(", ");
				obj.Accept(this, CiPriority.Argument);
			}
			Write(", ");
			if (method == CiSystem.RegexIsMatchRegex)
				obj.Accept(this, CiPriority.Argument);
			else if (method == CiSystem.MatchFindRegex)
				args[1].Accept(this, CiPriority.Argument);
			else
				WriteRegex(args, 1);
			Write(')');
		}
		else if (method == CiSystem.MatchGetCapture) {
			StartMethodCall(obj);
			WriteCall("str", args[0]);
		}
		else if (method == CiSystem.ConsoleWrite)
			WriteConsoleWrite(obj, args, false);
		else if (method == CiSystem.ConsoleWriteLine)
			WriteConsoleWrite(obj, args, true);
		else if (method == CiSystem.UTF8GetString) {
			Include("string_view");
			Write("std::string_view(reinterpret_cast<const char *>(");
			WriteArrayPtrAdd(args[0], args[1]);
			Write("), ");
			args[2].Accept(this, CiPriority.Argument);
			Write(')');
		}
		else if (method == CiSystem.EnvironmentGetEnvironmentVariable) {
			Include("cstdlib");
			WriteCall("std::getenv", args[0]);
		}
		else {
			if (obj.IsReferenceTo(CiSystem.BasePtr)) {
				WriteName((CiClass) method.Parent);
				Write("::");
			}
			else {
				obj.Accept(this, CiPriority.Primary);
				if (method.CallType == CiCallType.Static)
					Write("::");
				else if (IsCppPtr(obj))
					Write("->");
				else
					Write('.');
			}
			WriteName(method);
			WriteArgsInParentheses(method, args);
		}
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("CiResource::");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override void WriteArrayPtr(CiExpr expr, CiPriority parent)
	{
		switch (expr.Type) {
		case CiArrayStorageType _:
		case CiStringPtrType _:
			expr.Accept(this, CiPriority.Primary);
			Write(".data()");
			break;
		case CiArrayPtrType arrayPtr when arrayPtr.Modifier == CiToken.Hash:
			expr.Accept(this, CiPriority.Primary);
			Write(".get()");
			break;
		case CiClassType klass when klass.Class == CiSystem.ListClass:
			StartMethodCall(expr);
			Write("begin()");
			break;
		default:
			expr.Accept(this, parent);
			break;
		}
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		switch (type) {
		case CiClassPtrType leftClass when leftClass.Modifier != CiToken.Hash:
			switch (expr.Type) {
			case CiClass _:
			case CiClassPtrType _ when !IsCppPtr(expr):
				Write('&');
				if (expr is CiCallExpr) {
					Write("static_cast<");
					if (leftClass.Modifier == CiToken.EndOfFile)
						Write("const ");
					WriteName(leftClass.Class);
					Write(" &>(");
					expr.Accept(this, CiPriority.Argument);
					Write(')');
				}
				else
					expr.Accept(this, CiPriority.Primary);
				return;
			case CiClassPtrType rightPtr when rightPtr.Modifier == CiToken.Hash:
				expr.Accept(this, CiPriority.Primary);
				Write(".get()");
				return;
			default:
				break;
			}
			break;
		case CiClassType _ when expr.Type is CiStorageType:
			Write('&');
			expr.Accept(this, CiPriority.Primary);
			return;
		case CiArrayPtrType leftArray when leftArray.Modifier != CiToken.Hash:
			WriteArrayPtr(expr, CiPriority.Argument);
			return;
		case CiStringPtrType _ when expr.Type == CiSystem.NullType:
			Include("string_view");
			Write("std::string_view(nullptr, 0)");
			return;
		default:
			break;
		}
		base.WriteCoercedInternal(type, expr, parent);
	}

	protected override void WriteEqualString(CiExpr left, CiExpr right, CiPriority parent, bool not)
	{
		left.Accept(this, CiPriority.Equality);
		Write(GetEqOp(not));
		right.Accept(this, CiPriority.Equality);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		StartStringMethod(expr);
		Write("length()");
	}

	void WriteMatchProperty(CiSymbolReference expr, string name)
	{
		StartMethodCall(expr.Left);
		Write(name);
		Write("()");
	}

	public override CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Symbol == CiSystem.MatchStart)
			WriteMatchProperty(expr, "position");
		else if (expr.Symbol == CiSystem.MatchEnd) {
			if (parent > CiPriority.Add)
				Write('(');
			WriteMatchProperty(expr, "position");
			Write(" + ");
			WriteMatchProperty(expr, "length"); // FIXME: side effect
			if (parent > CiPriority.Add)
				Write(')');
		}
		else if (expr.Symbol == CiSystem.MatchLength)
			WriteMatchProperty(expr, "length");
		else if (expr.Symbol == CiSystem.MatchValue)
			WriteMatchProperty(expr, "str");
		else
			return base.VisitSymbolReference(expr, parent);
		return expr;
	}

	public override CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		switch (expr.Op) {
		case CiToken.Equal:
		case CiToken.NotEqual:
		case CiToken.Greater:
			if (IsStringEmpty(expr, out CiExpr str)) {
				if (expr.Op != CiToken.Equal)
					Write('!');
				str.Accept(this, CiPriority.Primary);
				Write(".empty()");
				return expr;
			}
			break;
		case CiToken.Assign when expr.Left.Type == CiSystem.StringStorageType && parent == CiPriority.Statement && IsTrimSubstring(expr) is CiExpr length:
			WriteCall(expr.Left, "resize", length);
			return expr;
		case CiToken.Is:
			Write("dynamic_cast<const ");
			Write(((CiClass) expr.Right).Name);
			Write(" *>(");
			expr.Left.Accept(this, CiPriority.Argument);
			Write(')');
			return expr;
		default:
			break;
		}
		return base.VisitBinaryExpr(expr, parent);
	}

	protected override void WriteConst(CiConst konst)
	{
		Write("static constexpr ");
		WriteTypeAndName(konst);
		Write(" = ");
		konst.Value.Accept(this, CiPriority.Argument);
		WriteLine(';');
	}

	public override void VisitForeach(CiForeach statement)
	{
		CiVar element = statement.Element;
		Write("for (");
		if (statement.Count == 2) {
			Write("const auto &[");
			Write(element.Name);
			Write(", ");
			Write(statement.ValueVar.Name);
			Write(']');
		}
		else if (statement.Collection.Type is CiArrayStorageType array
		 && array.ElementType is CiClass klass
		 && element.Type is CiClassPtrType ptr) {
			if (ptr.Modifier == CiToken.EndOfFile)
				Write("const ");
			Write(klass.Name);
			Write(" &");
			Write(element.Name);
		}
		else
			WriteTypeAndName(element);
		Write(" : ");
		statement.Collection.Accept(this, CiPriority.Argument);
		Write(')');
		WriteChild(statement.Body);
	}

	public override void VisitLock(CiLock statement)
	{
		OpenBlock();
		Write("const std::lock_guard<std::recursive_mutex> lock(");
		statement.Lock.Accept(this, CiPriority.Argument);
		WriteLine(");");
		FlattenBlock(statement.Body);
		CloseBlock();
	}

	protected override void WriteReturnValue(CiExpr value)
	{
		if (this.CurrentMethod.Type == CiSystem.StringStorageType
		 && value.Type == CiSystem.StringPtrType
		 && !(value is CiLiteral)) {
			Write("std::string(");
			base.WriteReturnValue(value);
			Write(')');
		}
		else if (this.CurrentMethod.Type == CiSystem.StringStorageType
			&& IsStringSubstring(value, out bool cast, out CiExpr ptr, out CiExpr offset, out CiExpr length)
			&& ptr.Type != CiSystem.StringStorageType) {
			Write("std::string(");
			if (cast)
				Write("reinterpret_cast<const char *>(");
			WriteArrayPtrAdd(ptr, offset);
			if (cast)
				Write(')');
			Write(", ");
			length.Accept(this, CiPriority.Argument);
			Write(')');
		}
		else
			base.WriteReturnValue(value);
	}

	protected override void WriteCaseBody(List<CiStatement> statements)
	{
		bool block = false;
		foreach (CiStatement statement in statements) {
			if (!block && statement is CiVar) {
				OpenBlock();
				block = true;
			}
			statement.Accept(this);
		}
		if (block)
			CloseBlock();
	}

	public override void VisitThrow(CiThrow statement)
	{
		Include("exception");
		WriteLine("throw std::exception();");
		// TODO: statement.Message.Accept(this, CiPriority.Argument);
	}

	void OpenNamespace()
	{
		if (this.Namespace == null)
			return;
		WriteLine();
		Write("namespace ");
		WriteLine(this.Namespace);
		WriteLine('{');
	}

	void CloseNamespace()
	{
		if (this.Namespace != null)
			WriteLine('}');
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		Write("enum class ");
		WriteLine(enu.Name);
		OpenBlock();
		bool first = true;
		foreach (CiConst konst in enu) {
			if (!first)
				WriteLine(',');
			first = false;
			Write(konst.Documentation);
			WriteCamelCase(konst.Name);
			WriteExplicitEnumValue(konst);
		}
		WriteLine();
		this.Indent--;
		WriteLine("};");
		if (enu is CiEnumFlags) {
			Write("CI_ENUM_FLAG_OPERATORS(");
			Write(enu.Name);
			WriteLine(')');
		}
	}

	static CiVisibility GetConstructorVisibility(CiClass klass)
	{
		switch (klass.CallType) {
		case CiCallType.Static:
			return CiVisibility.Private;
		case CiCallType.Abstract:
			return CiVisibility.Protected;
		default:
			return CiVisibility.Public;
		}
	}

	static bool HasMembersOfVisibility(CiClass klass, CiVisibility visibility)
	{
		return klass.OfType<CiMember>().Any(m => m.Visibility == visibility);
	}

	protected override void WriteField(CiField field)
	{
		Write(field.Documentation);
		WriteVar(field);
		WriteLine(';');
	}

	void WriteParametersAndConst(CiMethod method, bool defaultArguments)
	{
		WriteParameters(method, defaultArguments);
		if (method.CallType != CiCallType.Static && !method.IsMutator)
			Write(" const");
	}

	void WriteDeclarations(CiClass klass, CiVisibility visibility, string visibilityKeyword)
	{
		bool constructor = GetConstructorVisibility(klass) == visibility;
		bool destructor = visibility == CiVisibility.Public && klass.AddsVirtualMethods;
		if (!constructor && !destructor && !HasMembersOfVisibility(klass, visibility))
			return;

		Write(visibilityKeyword);
		WriteLine(':');
		this.Indent++;

		if (constructor) {
			if (klass.Constructor != null)
				Write(klass.Constructor.Documentation);
			Write(klass.Name);
			Write("()");
			if (klass.CallType == CiCallType.Static)
				Write(" = delete");
			else if (klass.Constructor == null)
				Write(" = default");
			WriteLine(';');
		}

		if (destructor) {
			Write("virtual ~");
			Write(klass.Name);
			WriteLine("() = default;");
		}

		foreach (CiMember member in klass.OfType<CiMember>()) {
			if (member.Visibility != visibility)
				continue;
			switch (member) {
			case CiConst konst:
				Write(konst.Documentation);
				WriteConst(konst);
				break;
			case CiField field:
				WriteField(field);
				break;
			case CiMethod method:
				WriteDoc(method);
				switch (method.CallType) {
				case CiCallType.Static:
					Write("static ");
					break;
				case CiCallType.Abstract:
				case CiCallType.Virtual:
					Write("virtual ");
					break;
				default:
					break;
				}
				WriteTypeAndName(method);
				WriteParametersAndConst(method, true);
				switch (method.CallType) {
				case CiCallType.Abstract:
					Write(" = 0");
					break;
				case CiCallType.Override:
					Write(" override");
					break;
				case CiCallType.Sealed:
					Write(" final");
					break;
				default:
					break;
				}
				WriteLine(';');
				break;
			default:
				throw new NotImplementedException(member.Type.ToString());
			}
		}

		this.Indent--;
	}

	void Write(CiClass klass)
	{
		// topological sorting of class hierarchy and class storage fields
		if (klass == null)
			return;
		if (this.WrittenClasses.TryGetValue(klass, out bool done)) {
			if (done)
				return;
			throw new CiException(klass, $"Circular dependency for class {klass.Name}");
		}
		this.WrittenClasses.Add(klass, false);
		Write(klass.Parent as CiClass);
		foreach (CiField field in klass.OfType<CiField>())
			Write(field.Type.BaseType as CiClass);
		this.WrittenClasses[klass] = true;

		WriteLine();
		Write(klass.Documentation);
		OpenClass(klass, klass.CallType == CiCallType.Sealed ? " final" : "", " : public ");
		this.Indent--;
		WriteDeclarations(klass, CiVisibility.Public, "public");
		WriteDeclarations(klass, CiVisibility.Protected, "protected");
		WriteDeclarations(klass, CiVisibility.Internal, "public");
		WriteDeclarations(klass, CiVisibility.Private, "private");
		WriteLine("};");
	}

	void WriteConstructor(CiClass klass)
	{
		if (klass.Constructor == null)
			return;
		this.StringSwitchesWithGoto.Clear();
		Write(klass.Name);
		Write("::");
		Write(klass.Name);
		WriteLine("()");
		OpenBlock();
		WriteConstructorBody(klass);
		CloseBlock();
	}

	protected override void WriteMethod(CiMethod method)
	{
		if (method.CallType == CiCallType.Abstract)
			return;
		this.StringSwitchesWithGoto.Clear();
		WriteLine();
		Write(method.Type, true);
		Write(' ');
		Write(method.Parent.Name);
		Write("::");
		WriteCamelCase(method.Name);
		WriteParametersAndConst(method, false);
		WriteBody(method);
	}

	void WriteResources(Dictionary<string, byte[]> resources, bool define)
	{
		if (resources.Count == 0)
			return;
		WriteLine();
		WriteLine("namespace");
		OpenBlock();
		WriteLine("namespace CiResource");
		OpenBlock();
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			if (!define)
				Write("extern ");
			Include("array");
			Include("cstdint");
			Write("const std::array<uint8_t, ");
			VisitLiteralLong(resources[name].Length);
			Write("> ");
			WriteResource(name, -1);
			if (define) {
				WriteLine(" = {");
				Write('\t');
				Write(resources[name]);
				Write(" }");
			}
			WriteLine(';');
		}
		CloseBlock();
		CloseBlock();
	}

	public override void Write(CiProgram program)
	{
		this.WrittenClasses.Clear();
		string headerFile = Path.ChangeExtension(this.OutputFile, "hpp");
		SortedSet<string> headerIncludes = new SortedSet<string>();
		this.Includes = headerIncludes;
		OpenStringWriter();
		if (program.Any(c => c is CiEnumFlags)) {
			Include("type_traits");
			WriteLine("#define CI_ENUM_FLAG_OPERATORS(T) \\");
			WriteLine("\tinline constexpr T operator~(T a) { return static_cast<T>(~static_cast<std::underlying_type_t<T>>(a)); } \\");
			WriteLine("\tinline constexpr T operator&(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) & static_cast<std::underlying_type_t<T>>(b)); } \\");
			WriteLine("\tinline constexpr T operator|(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) | static_cast<std::underlying_type_t<T>>(b)); } \\");
			WriteLine("\tinline constexpr T operator^(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) ^ static_cast<std::underlying_type_t<T>>(b)); } \\");
			WriteLine("\tinline constexpr T &operator&=(T &a, T b) { return (a = a & b); } \\");
			WriteLine("\tinline constexpr T &operator|=(T &a, T b) { return (a = a | b); } \\");
			WriteLine("\tinline constexpr T &operator^=(T &a, T b) { return (a = a ^ b); }");
		}
		OpenNamespace();
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes) {
			Write("class ");
			Write(klass.Name);
			WriteLine(';');
		}
		foreach (CiClass klass in program.Classes)
			Write(klass);
		CloseNamespace();

		CreateFile(headerFile);
		WriteLine("#pragma once");
		WriteIncludes();
		CloseStringWriter();
		CloseFile();

		this.Includes = new SortedSet<string>();
		OpenStringWriter();
		WriteResources(program.Resources, false);
		OpenNamespace();
		foreach (CiClass klass in program.Classes) {
			WriteConstructor(klass);
			WriteMethods(klass);
		}
		WriteResources(program.Resources, true);
		CloseNamespace();

		CreateFile(this.OutputFile);
		WriteTopLevelNatives(program);
		this.Includes.ExceptWith(headerIncludes);
		WriteIncludes();
		Write("#include \"");
		Write(Path.GetFileName(headerFile));
		WriteLine("\"");
		if (this.UsingStringViewLiterals)
			WriteLine("using namespace std::string_view_literals;");
		CloseStringWriter();
		CloseFile();
	}
}

}
