// CiResolver.cs - Ci symbol resolver
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Foxoft.Ci
{

public class CiResolver : CiVisitor
{
	readonly CiProgram Program;
	readonly IEnumerable<string> SearchDirs;
	readonly string Lang;
	CiScope CurrentScope;
	CiMethodBase CurrentMethod;
	readonly HashSet<CiMethod> CurrentPureMethods = new HashSet<CiMethod>();
	readonly Dictionary<CiVar, CiExpr> CurrentPureArguments = new Dictionary<CiVar, CiExpr>();

	CiException StatementException(CiStatement statement, string message)
	{
		return new CiException(this.CurrentScope.Container.Filename, statement.Line, message);
	}

	CiException StatementException(CiStatement statement, string format, params object[] args)
	{
		return StatementException(statement, string.Format(format, args));
	}

	void NotSupported(CiStatement statement, string feature, params string[] langs)
	{
		if (langs.Contains(this.Lang))
			throw StatementException(statement, "{0} not supported when targeting {1}", feature, this.Lang);
	}

	void NotYet(CiStatement statement, string feature, params string[] langs)
	{
		if (langs.Contains(this.Lang))
			throw StatementException(statement, "{0} not supported yet when targeting {1}", feature, this.Lang);
	}

	string FindFile(string name, CiStatement statement)
	{
		foreach (string dir in this.SearchDirs) {
			string path = Path.Combine(dir, name);
			if (File.Exists(path))
				return path;
		}
		if (File.Exists(name))
			return name;
		throw StatementException(statement, "File {0} not found", name);
	}

	void ResolveBase(CiClass klass)
	{
		switch (klass.VisitStatus) {
		case CiVisitStatus.NotYet:
			break;
		case CiVisitStatus.InProgress:
			throw new CiException(klass, "Circular inheritance for class {0}", klass.Name);
		case CiVisitStatus.Done:
			return;
		}
		if (klass.BaseClassName != null) {
			if (!(Program.TryLookup(klass.BaseClassName) is CiClass baseClass))
				throw new CiException(klass, "Base class {0} not found", klass.BaseClassName);
			if (klass.IsPublic && !baseClass.IsPublic)
				throw new CiException(klass, "Public class cannot derive from an internal class");
			klass.Parent = baseClass;
			klass.VisitStatus = CiVisitStatus.InProgress;
			ResolveBase(baseClass);
		}
		this.Program.Classes.Add(klass);
		klass.VisitStatus = CiVisitStatus.Done;

		klass.AddRange(klass.Consts);
		klass.AddRange(klass.Fields);
		klass.AddRange(klass.Methods);
	}

	static void TakePtr(CiExpr expr)
	{
		if (expr.Type is CiArrayStorageType arrayStg)
			arrayStg.PtrTaken = true;
	}

	void Coerce(CiExpr expr, CiType type)
	{
		if (!type.IsAssignableFrom(expr.Type))
			throw StatementException(expr, "Cannot coerce {0} to {1}", expr.Type, type);
		if (expr is CiPrefixExpr prefix && prefix.Op == CiToken.New && !type.IsDynamicPtr)
			throw StatementException(expr, "Dynamically allocated {0} must be assigned to a {1} reference", expr.Type is CiArrayType ? "array" : "object", expr.Type);
		if (type is CiArrayPtrType)
			TakePtr(expr);
	}

	CiType GetCommonType(CiExpr left, CiExpr right)
	{
		if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange)
			return leftRange.Union(rightRange);
		CiType ptr = left.Type.PtrOrSelf;
		if (ptr.IsAssignableFrom(right.Type))
			return ptr;
		ptr = right.Type.PtrOrSelf;
		if (ptr.IsAssignableFrom(left.Type))
			return ptr;
		throw StatementException(left, "Incompatible types: {0} and {1}", left.Type, right.Type);
	}

	CiType GetIntegerType(CiExpr left, CiExpr right)
	{
		CiType type = CiBinaryExpr.PromoteIntegerTypes(left.Type, right.Type);
		Coerce(left, type);
		Coerce(right, type);
		return type;
	}

	CiIntegerType GetShiftType(CiExpr left, CiExpr right)
	{
		Coerce(right, CiSystem.IntType);
		if (left.Type == CiSystem.LongType)
			return CiSystem.LongType;
		Coerce(left, CiSystem.IntType);
		return CiSystem.IntType;
	}

	CiType GetNumericType(CiExpr left, CiExpr right)
	{
		CiType type = CiBinaryExpr.PromoteNumericTypes(left.Type, right.Type);
		Coerce(left, type);
		Coerce(right, type);
		return type;
	}

	static int SaturatedNeg(int a)
	{
		if (a == int.MinValue)
			return int.MaxValue;
		return -a;
	}

	static int SaturatedAdd(int a, int b)
	{
		int c = a + b;
		if (c >= 0) {
			if (a < 0 && b < 0)
				return int.MinValue;
		}
		else if (a > 0 && b > 0)
			return int.MaxValue;
		return c;
	}

	static int SaturatedSub(int a, int b)
	{
		if (b == int.MinValue)
			return a < 0 ? a ^ b : int.MaxValue;
		return SaturatedAdd(a, -b);
	}

	static int SaturatedMul(int a, int b)
	{
		if (a == 0 || b == 0)
			return 0;
		if (a == int.MinValue)
			return b >> 31 ^ a;
		if (b == int.MinValue)
			return a >> 31 ^ b;
		if (int.MaxValue / Math.Abs(a) < Math.Abs(b))
			return (a ^ b) >> 31 ^ int.MaxValue;
		return a * b;
	}

	static int SaturatedDiv(int a, int b)
	{
		if (a == int.MinValue && b == -1)
			return int.MaxValue;
		return a / b;
	}

	static int SaturatedShiftRight(int a, int b)
	{
		return a >> (b >= 31 || b < 0 ? 31 : b);
	}

	static CiRangeType UnsignedAnd(CiRangeType left, CiRangeType right)
	{
		int leftVariableBits = left.VariableBits;
		int rightVariableBits = right.VariableBits;
		int min = left.Min & right.Min & ~CiRangeType.GetMask(~left.Min & ~right.Min & (leftVariableBits | rightVariableBits));
		// Calculate upper bound with variable bits set
		int max = (left.Max | leftVariableBits) & (right.Max | rightVariableBits);
		// The upper bound will never exceed the input
		if (max > left.Max)
			max = left.Max;
		if (max > right.Max)
			max = right.Max;
		if (min > max)
			return new CiRangeType(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return new CiRangeType(min, max);
	}

	static CiRangeType UnsignedOr(CiRangeType left, CiRangeType right)
	{
		int leftVariableBits = left.VariableBits;
		int rightVariableBits = right.VariableBits;
		int min = (left.Min & ~leftVariableBits) | (right.Min & ~rightVariableBits);
		int max = left.Max | right.Max | CiRangeType.GetMask(left.Max & right.Max & CiRangeType.GetMask(leftVariableBits | rightVariableBits));
		// The lower bound will never be less than the input
		if (min < left.Min)
			min = left.Min;
		if (min < right.Min)
			min = right.Min;
		if (min > max)
			return new CiRangeType(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return new CiRangeType(min, max);
	}

	static CiRangeType UnsignedXor(CiRangeType left, CiRangeType right)
	{
		int variableBits = left.VariableBits | right.VariableBits;
		int min = (left.Min ^ right.Min) & ~variableBits;
		int max = (left.Max ^ right.Max) | variableBits;
		if (min > max)
			return new CiRangeType(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return new CiRangeType(min, max);
	}

	delegate CiRangeType UnsignedOp(CiRangeType left, CiRangeType right);

	bool IsEnumOp(CiExpr left, CiExpr right)
	{
		if (left.Type is CiEnumFlags || left.Type == CiSystem.BoolType) {
			Coerce(right, left.Type);
			return true;
		}
		if (left.Type is CiEnum)
			throw StatementException(left, "Define flags enumeration as: enum* {0}", left.Type);
		return false;
	}

	CiType BitwiseOp(CiExpr left, CiExpr right, UnsignedOp op)
	{
		if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange) {
			leftRange.SplitBySign(out CiRangeType leftNegative, out CiRangeType leftPositive);
			rightRange.SplitBySign(out CiRangeType rightNegative, out CiRangeType rightPositive);
			CiRangeType range = null;
			if (leftNegative != null) {
				if (rightNegative != null)
					range = op(leftNegative, rightNegative);
				if (rightPositive != null)
					range = op(leftNegative, rightPositive).Union(range);
			}
			if (leftPositive != null) {
				if (rightNegative != null)
					range = op(leftPositive, rightNegative).Union(range);
				if (rightPositive != null)
					range = op(leftPositive, rightPositive).Union(range);
			}
			return range;
		}
		if (IsEnumOp(left, right))
			return left.Type;
		return GetIntegerType(left, right);
	}

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		CiExpr[] items = expr.Items;
		for (int i = 0; i < items.Length; i++)
			items[i] = Resolve(items[i]);
		return expr;
	}

	public override CiExpr Visit(CiVar expr, CiPriority parent)
	{
		CiType type = ResolveType(expr);
		if (expr.Value != null) {
			expr.Value = Resolve(expr.Value);
			if (!expr.IsAssignableStorage) {
				if (type is CiArrayStorageType array)
					type = array.ElementType;
				Coerce(expr.Value, type);
			}
		}
		this.CurrentScope.Add(expr);
		return expr;
	}

	public override CiExpr Visit(CiLiteral expr, CiPriority parent)
	{
		return expr;
	}

	public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent)
	{
		List<CiInterpolatedPart> parts = new List<CiInterpolatedPart>();
		StringBuilder sb = new StringBuilder();
		foreach (CiInterpolatedPart part in expr.Parts) {
			sb.Append(part.Prefix);
			CiExpr arg = Resolve(part.Argument);
			switch (arg.Type) {
			case CiIntegerType _:
				if (" DdXx".IndexOf(part.Format) < 0)
					throw StatementException(arg, "Invalid integer format string");
				break;
			case CiFloatingType _:
				if (" FfEe".IndexOf(part.Format) < 0)
					throw StatementException(arg, "Invalid floating-point format string");
				break;
			case CiStringType _:
				if (part.Format != ' ')
					throw StatementException(arg, "Invalid string format string");
				break;
			default:
				throw StatementException(arg, "Only numbers and strings can be interpolated in strings");
			}
			int width = 0;
			if (part.WidthExpr != null)
				width = FoldConstInt(part.WidthExpr);
			if (arg is CiLiteral literalArg && !(arg.Type is CiFloatingType)) { // float formatting is runtime-locale-specific
				string stringArg = part.Format == ' ' ? literalArg.Value.ToString()
					: ((long) literalArg.Value).ToString(part.Format + (part.Precision < 0 ? "" : part.Precision.ToString()));
				if (part.WidthExpr != null)
					stringArg = width >= 0 ? stringArg.PadLeft(width) : stringArg.PadRight(-width);
				sb.Append(stringArg);
			}
			else {
				parts.Add(new CiInterpolatedPart(sb.ToString(), arg) { WidthExpr = part.WidthExpr, Width = width, Format = part.Format, Precision = part.Precision });
				sb.Clear();
			}
		}
		sb.Append(expr.Suffix);
		if (parts.Count == 0)
			return expr.ToLiteralString(sb.ToString());
		expr.Parts = parts.ToArray();
		expr.Suffix = sb.ToString();
		return expr;
	}

	CiExpr Lookup(CiSymbolReference expr, CiScope scope)
	{
		if (expr.Symbol == null) {
			expr.Symbol = scope.TryLookup(expr.Name);
			if (expr.Symbol == null)
				throw StatementException(expr, "{0} not found", expr.Name);
			if (expr.Symbol == CiSystem.RegexClass)
				NotSupported(expr, "Regex", "cl");
			expr.Type = expr.Symbol.Type;
		}
		if (!(scope is CiEnum) && expr.Symbol is CiConst konst) {
			ResolveConst(konst);
			if (konst.Value is CiLiteral || konst.Value is CiSymbolReference)
				return konst.Value;
		}
		return expr;
	}

	static bool IsAscii(string s) => s.All(c => c <= 127);

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left != null) {
			CiExpr left = Resolve(expr.Left);
			CiSymbolReference leftSymbol = left as CiSymbolReference;
			if (leftSymbol != null && leftSymbol.Symbol == CiSystem.BasePtr) {
				// TODO: error handling
				CiClass baseClass = (CiClass) this.CurrentMethod.Parent.Parent;
				expr.Symbol = (CiMethod) baseClass.TryShallowLookup(expr.Name);
			}
			else {
				if (leftSymbol == null || !(leftSymbol.Symbol is CiScope scope))
					scope = left.Type;
				CiExpr result = Lookup(expr, scope);
				if (result != expr)
					return result;
				if (expr.Symbol == CiSystem.ArrayLength) {
					if (scope is CiArrayStorageType array)
						return expr.ToLiteralLong(array.Length);
					throw new NotImplementedException(scope.GetType().Name);
				}
				if (expr.Symbol == CiSystem.StringLength && left is CiLiteral leftLiteral) {
					string s = (string) leftLiteral.Value;
					if (IsAscii(s))
						return expr.ToLiteralLong(s.Length);
				}
			}
			return new CiSymbolReference { Line = expr.Line, Left = left, Name = expr.Name, Symbol = expr.Symbol, Type = expr.Type };
		}

		CiExpr resolved = Lookup(expr, this.CurrentScope);
		if (resolved is CiSymbolReference symbol
		 && symbol.Symbol is CiVar v) {
			if (v.Parent is CiFor loop)
				loop.IsIteratorUsed = true;
			else if (this.CurrentPureArguments.TryGetValue(v, out CiExpr arg))
				return arg;
		}
		return resolved;
	}

	void CheckLValue(CiExpr expr)
	{
		// TODO: check lvalue
		if (expr is CiSymbolReference symbol) {
			if (symbol.Symbol is CiVar def) {
				def.IsAssigned = true;
				switch (symbol.Symbol.Parent) {
				case CiFor forLoop:
					forLoop.IsRange = false;
					break;
				case CiForeach _:
					throw StatementException(expr, "Cannot assign a foreach iteration variable");
				default:
					break;
				}
			}
			for (CiScope scope = this.CurrentScope; !(scope is CiClass); scope = scope.Parent) {
				if (scope is CiFor forLoop
				 && forLoop.IsRange
				 && forLoop.Cond is CiBinaryExpr binaryCond
				 && binaryCond.Right.IsReferenceTo(symbol.Symbol))
					forLoop.IsRange = false;
			}
		}
	}

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		CiExpr inner;
		CiType type;
		CiRangeType range;
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			inner = Resolve(expr.Inner);
			CheckLValue(inner);
			Coerce(inner, CiSystem.DoubleType);
			range = inner.Type as CiRangeType;
			if (range != null) {
				int delta = expr.Op == CiToken.Increment ? 1 : -1;
				type = new CiRangeType(range.Min + delta, range.Max + delta);
			}
			else
				type = inner.Type;
			expr.Inner = inner;
			expr.Type = type;
			return expr;
		case CiToken.Minus:
			inner = Resolve(expr.Inner);
			Coerce(inner, CiSystem.DoubleType);
			range = inner.Type as CiRangeType;
			if (range != null)
				type = range = new CiRangeType(SaturatedNeg(range.Max), SaturatedNeg(range.Min));
			else if (inner is CiLiteral literal)
				return literal.Value is double d ? expr.ToLiteralDouble(-d) : expr.ToLiteralLong(-(long) literal.Value);
			else
				type = inner.Type;
			break;
		case CiToken.Tilde:
			inner = Resolve(expr.Inner);
			if (inner.Type is CiEnumFlags) {
				type = inner.Type;
				range = null;
			}
			else {
				Coerce(inner, CiSystem.IntType);
				range = inner.Type as CiRangeType;
				if (range != null)
					type = range = new CiRangeType(~range.Max, ~range.Min);
				else
					type = inner.Type;
			}
			break;
		case CiToken.ExclamationMark:
			inner = ResolveBool(expr.Inner);
			return new CiPrefixExpr { Line = expr.Line, Op = CiToken.ExclamationMark, Inner = inner, Type = CiSystem.BoolType };
		case CiToken.New:
			if (expr.Type != null)
				return expr;
			type = ToType(expr.Inner, true);
			switch (type) {
			case CiClass klass:
				expr.Type = new CiClassPtrType { Class = klass, Modifier = CiToken.Hash };
				expr.Inner = null;
				return expr;
			case CiArrayStorageType array:
				expr.Type = new CiArrayPtrType { ElementType = array.ElementType, Modifier = CiToken.Hash };
				expr.Inner = array.LengthExpr;
				return expr;
			default:
				throw StatementException(expr, "Invalid argument to new");
			}
		case CiToken.Resource:
			if (!(FoldConst(expr.Inner) is CiLiteral resourceName) || !(resourceName.Value is string name))
				throw StatementException(expr, "Resource name must be string");
			inner = resourceName;
			if (!this.Program.Resources.TryGetValue(name, out byte[] content)) {
				content = File.ReadAllBytes(FindFile(name, expr));
				this.Program.Resources.Add(name, content);
			}
			type = new CiArrayStorageType { ElementType = CiSystem.ByteType, Length = content.Length };
			range = null;
			break;
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		if (range != null && range.Min == range.Max)
			return expr.ToLiteralLong(range.Min);
		return new CiPrefixExpr { Line = expr.Line, Op = expr.Op, Inner = inner, Type = type };
	}

	public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent)
	{
		expr.Inner = Resolve(expr.Inner);
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			CheckLValue(expr.Inner);
			Coerce(expr.Inner, CiSystem.DoubleType);
			expr.Type = expr.Inner.Type;
			return expr;
		case CiToken.ExclamationMark:
			throw StatementException(expr, "Unexpected '!'");
		case CiToken.Hash:
			throw StatementException(expr, "Unexpected '#'");
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
	}

	static CiInterpolatedString ToInterpolatedString(CiExpr expr)
	{
		if (expr is CiInterpolatedString interpolated)
			return interpolated;
		if (expr is CiLiteral literal)
			return new CiInterpolatedString(new CiInterpolatedPart[0],
				Convert.ToString(literal.Value, CultureInfo.InvariantCulture));
		return new CiInterpolatedString(new CiInterpolatedPart[1] { new CiInterpolatedPart("", expr) }, "");
	}

	static CiInterpolatedString Concatenate(CiInterpolatedString left, CiInterpolatedString right)
	{
		if (right.Parts.Length == 0)
			return new CiInterpolatedString(left.Parts, left.Suffix + right.Suffix);
		int i = left.Parts.Length;
		CiInterpolatedPart[] parts = new CiInterpolatedPart[i + right.Parts.Length];
		left.Parts.CopyTo(parts, 0);
		right.Parts.CopyTo(parts, i);
		parts[i].Prefix = left.Suffix + right.Parts[0].Prefix;
		return new CiInterpolatedString(parts, right.Suffix);
	}

	CiExpr ResolveEquality(CiBinaryExpr expr, CiExpr left, CiExpr right)
	{
		if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange) {
			if (leftRange.Min == leftRange.Max && leftRange.Min == rightRange.Min && leftRange.Min == rightRange.Max)
				return expr.ToLiteralBool(expr.Op == CiToken.Equal);
			if (leftRange.Max < rightRange.Min || leftRange.Min > rightRange.Max)
				return expr.ToLiteralBool(expr.Op == CiToken.NotEqual);
		}
		else if (left.Type == right.Type) {
			if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral)
				return expr.ToLiteralBool((expr.Op == CiToken.NotEqual) ^ object.Equals(leftLiteral.Value, rightLiteral.Value));
			if (left.IsConstEnum && right.IsConstEnum)
				return expr.ToLiteralBool((expr.Op == CiToken.NotEqual) ^ (left.IntValue == right.IntValue));
		}
		if (!left.Type.IsAssignableFrom(right.Type) && !right.Type.IsAssignableFrom(left.Type))
			throw StatementException(expr, "Cannot compare {0} with {1}", left.Type, right.Type);
		TakePtr(left);
		TakePtr(right);
		return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = CiSystem.BoolType };
	}

	void CheckComparison(CiExpr left, CiExpr right)
	{
		Coerce(left, CiSystem.DoubleType);
		Coerce(right, CiSystem.DoubleType);
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		CiExpr left = Resolve(expr.Left);
		CiExpr right = Resolve(expr.Right);
		CiType type;
		CiRangeType leftRange = left.Type as CiRangeType;
		CiRangeType rightRange = right.Type as CiRangeType;

		switch (expr.Op) {
		case CiToken.LeftBracket:
			if (left.Type is CiDictionaryType dict) {
				Coerce(right, dict.KeyType);
				type = dict.ValueType;
			}
			else {
				Coerce(right, CiSystem.IntType);
				switch (left.Type) {
				case CiArrayType array:
					type = array.ElementType;
					break;
				case CiStringType _:
					type = CiSystem.CharType;
					if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral) {
						string s = (string) leftLiteral.Value;
						if (IsAscii(s)) {
							long i = (long) rightLiteral.Value;
							if (i >= 0 && i < s.Length)
								return expr.ToLiteralLong(s[(int) i]);
						}
					}
					break;
				default:
					throw StatementException(expr.Left, "Indexed object is neither array or string");
				}
			}
			break;

		case CiToken.Plus:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedAdd(leftRange.Min, rightRange.Min),
					SaturatedAdd(leftRange.Max, rightRange.Max));
			}
			else if (left.Type is CiStringType || right.Type is CiStringType) {
				Coerce(left, CiSystem.PrintableType);
				Coerce(right, CiSystem.PrintableType);
				if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral)
					return expr.ToLiteralString(Convert.ToString(leftLiteral.Value, CultureInfo.InvariantCulture)
						+ Convert.ToString(rightLiteral.Value, CultureInfo.InvariantCulture));
				if (left is CiInterpolatedString || right is CiInterpolatedString)
					return Concatenate(ToInterpolatedString(left), ToInterpolatedString(right));
				NotSupported(expr, "String concatenation", "c", "cl");
				type = CiSystem.StringPtrType;
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Minus:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedSub(leftRange.Min, rightRange.Max),
					SaturatedSub(leftRange.Max, rightRange.Min));
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Asterisk:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedMul(leftRange.Min, rightRange.Min),
					SaturatedMul(leftRange.Min, rightRange.Max),
					SaturatedMul(leftRange.Max, rightRange.Min),
					SaturatedMul(leftRange.Max, rightRange.Max));
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Slash:
			if (leftRange != null && rightRange != null) {
				int denMin = rightRange.Min;
				if (denMin == 0)
					denMin = 1;
				int denMax = rightRange.Max;
				if (denMax == 0)
					denMax = -1;
				type = new CiRangeType(
					SaturatedDiv(leftRange.Min, denMin),
					SaturatedDiv(leftRange.Min, denMax),
					SaturatedDiv(leftRange.Max, denMin),
					SaturatedDiv(leftRange.Max, denMax));
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Mod:
			if (leftRange != null && rightRange != null) {
				int den = ~Math.Min(rightRange.Min, -rightRange.Max); // max(abs(rightRange))-1
				if (den < 0)
					throw StatementException(expr, "Mod zero");
				type = new CiRangeType(
					leftRange.Min >= 0 ? 0 : Math.Max(leftRange.Min, -den),
					leftRange.Max < 0 ? 0 : Math.Min(leftRange.Max, den));
			}
			else
				type = GetIntegerType(left, right);
			break;

		case CiToken.And:
			type = BitwiseOp(left, right, UnsignedAnd);
			break;
		case CiToken.Or:
			type = BitwiseOp(left, right, UnsignedOr);
			break;
		case CiToken.Xor:
			type = BitwiseOp(left, right, UnsignedXor);
			break;

		case CiToken.ShiftLeft:
			if (leftRange != null && rightRange != null && leftRange.Min == leftRange.Max && rightRange.Min == rightRange.Max) {
				// TODO: improve
				int result = leftRange.Min << rightRange.Min;
				type = new CiRangeType(result, result);
			}
			else
				type = GetShiftType(left, right);
			break;
		case CiToken.ShiftRight:
			if (leftRange != null && rightRange != null) {
				if (rightRange.Min < 0)
					rightRange = new CiRangeType(0, 32);
				type = new CiRangeType(
					SaturatedShiftRight(leftRange.Min, leftRange.Min < 0 ? rightRange.Min : rightRange.Max),
					SaturatedShiftRight(leftRange.Max, leftRange.Max < 0 ? rightRange.Max : rightRange.Min));
			}
			else
				type = GetShiftType(left, right);
			break;

		case CiToken.Equal:
		case CiToken.NotEqual:
			return ResolveEquality(expr, left, right);
		case CiToken.Less:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Max < rightRange.Min)
					return CiLiteral.True;
				if (leftRange.Min >= rightRange.Max)
					return CiLiteral.False;
			}
			else
				CheckComparison(left, right);
			type = CiSystem.BoolType;
			break;
		case CiToken.LessOrEqual:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Max <= rightRange.Min)
					return CiLiteral.True;
				if (leftRange.Min > rightRange.Max)
					return CiLiteral.False;
			}
			else
				CheckComparison(left, right);
			type = CiSystem.BoolType;
			break;
		case CiToken.Greater:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Min > rightRange.Max)
					return CiLiteral.True;
				if (leftRange.Max <= rightRange.Min)
					return CiLiteral.False;
			}
			else
				CheckComparison(left, right);
			type = CiSystem.BoolType;
			break;
		case CiToken.GreaterOrEqual:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Min >= rightRange.Max)
					return CiLiteral.True;
				if (leftRange.Max < rightRange.Min)
					return CiLiteral.False;
			}
			else
				CheckComparison(left, right);
			type = CiSystem.BoolType;
			break;

		case CiToken.CondAnd: {
			Coerce(left, CiSystem.BoolType);
			Coerce(right, CiSystem.BoolType);
			if (left is CiLiteral leftLiteral)
				return (bool) leftLiteral.Value ? right : CiLiteral.False;
			if (right is CiLiteral rightLiteral && (bool) rightLiteral.Value)
				return left;
			type = CiSystem.BoolType;
			break;
		}
		case CiToken.CondOr: {
			Coerce(left, CiSystem.BoolType);
			Coerce(right, CiSystem.BoolType);
			if (left is CiLiteral leftLiteral)
				return (bool) leftLiteral.Value ? CiLiteral.True : right;
			if (right is CiLiteral rightLiteral && !(bool) rightLiteral.Value)
				return left;
			type = CiSystem.BoolType;
			break;
		}

		case CiToken.Assign:
			CheckLValue(left);
			Coerce(right, left.Type);
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.AddAssign:
			CheckLValue(left);
			if (left.Type == CiSystem.StringStorageType)
				Coerce(right, CiSystem.PrintableType);
			else {
				Coerce(left, CiSystem.DoubleType);
				Coerce(right, left.Type);
			}
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.SubAssign:
		case CiToken.MulAssign:
		case CiToken.DivAssign:
			CheckLValue(left);
			Coerce(left, CiSystem.DoubleType);
			Coerce(right, left.Type);
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.ModAssign:
		case CiToken.ShiftLeftAssign:
		case CiToken.ShiftRightAssign:
			CheckLValue(left);
			Coerce(left, CiSystem.IntType);
			Coerce(right, CiSystem.IntType);
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.AndAssign:
		case CiToken.OrAssign:
		case CiToken.XorAssign:
			CheckLValue(left);
			if (!IsEnumOp(left, right)) {
				Coerce(left, CiSystem.IntType);
				Coerce(right, CiSystem.IntType);
			}
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.Is:
			if (!(left.Type is CiClassPtrType leftPtr))
				throw StatementException(expr, "Left hand side of the 'is' operator must be an object reference");
			CiClass klass;
			switch (right) {
			case CiSymbolReference symbol:
				klass = symbol.Symbol as CiClass ?? throw StatementException(expr, "Right hand side of the 'is' operator must be a class name");
				NotSupported(expr, "'is' operator", "c", "cl");
				expr.Right = klass;
				break;
			case CiVar def:
				if (!(def.Type is CiClassPtrType rightPtr))
					throw StatementException(expr, "Right hand side of the 'is' operator must be an object reference definition");
				if (!rightPtr.IsModifierAssignableFrom(leftPtr))
					throw StatementException(expr, "{0} cannot be casted to {1}", leftPtr, rightPtr);
				NotSupported(expr, "'is' operator", "c", "cpp", "js", "py", "swift", "ts", "cl");
				klass = rightPtr.Class;
				break;
			default:
				throw StatementException(expr, "Right hand side of the 'is' operator must be a class name");
			}
			if (leftPtr.Class == klass)
				throw StatementException(expr, "{0} is {1}, the 'is' operator would always return 'true'", left, leftPtr);
			if (!leftPtr.Class.IsSameOrBaseOf(klass))
				throw StatementException(expr, "{0} is not base class of {1}, the 'is' operator would always return 'false'", leftPtr, klass.Name);
			expr.Left = left;
			expr.Type = CiSystem.BoolType;
			return expr;

		case CiToken.Range:
			throw StatementException(expr, "Range within an expression");
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		if (type is CiRangeType range && range.Min == range.Max)
			return expr.ToLiteralLong(range.Min);
		return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = type };
	}

	public override CiExpr Visit(CiSelectExpr expr, CiPriority parent)
	{
		CiExpr cond = ResolveBool(expr.Cond);
		CiExpr onTrue = Resolve(expr.OnTrue);
		CiExpr onFalse = Resolve(expr.OnFalse);
		CiType type = GetCommonType(onTrue, onFalse);
		Coerce(onTrue, type);
		Coerce(onFalse, type);
		if (cond is CiLiteral literalCond)
			return (bool) literalCond.Value ? onTrue : onFalse;
		return new CiSelectExpr { Line = expr.Line, Cond = cond, OnTrue = onTrue, OnFalse = onFalse, Type = type };
	}

	static bool CanCall(CiMethod method, CiExpr[] arguments)
	{
		int i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i >= arguments.Length)
				return param.Value != null;
			if (!param.Type.IsAssignableFrom(arguments[i++].Type))
				return false;
		}
		return i == arguments.Length;
	}

	public override CiExpr Visit(CiCallExpr expr, CiPriority parent)
	{
		CiSymbolReference symbol = (CiSymbolReference) Resolve(expr.Method);
		CiExpr[] arguments = this.CurrentPureArguments.Count == 0 ? expr.Arguments : new CiExpr[expr.Arguments.Length];
		int i;
		for (i = 0; i < arguments.Length; i++)
			arguments[i] = Resolve(expr.Arguments[i]);
		CiMethod method = symbol.Symbol switch {
			CiMethod m => m,
			CiMethodGroup group => group.Methods.FirstOrDefault(m => CanCall(m, arguments))
				?? /* pick first for the error message */ group.Methods[0],
			_ => throw StatementException(symbol, "Expected a method")
		};

		// TODO: check static
		i = 0;
		foreach (CiVar param in method.Parameters) {
			if (i >= arguments.Length) {
				if (param.Value != null)
					break;
				throw StatementException(expr, "Too few arguments");
			}
			Coerce(arguments[i++], param.Type);
		}
		if (i < arguments.Length)
			throw StatementException(arguments[i], "Too many arguments");

		if (method.Throws) {
			if (this.CurrentMethod == null)
				throw StatementException(expr, "Cannot call method {0} here because it is marked 'throws'", method.Name);
			if (!this.CurrentMethod.Throws)
				throw StatementException(expr, "Method marked 'throws' called from a method not marked 'throws'");
		}

		symbol.Symbol = method;

		if (method.CallType == CiCallType.Static
		 && method.Body is CiReturn ret
		 && arguments.All(arg => arg is CiLiteral)
		 && this.CurrentPureMethods.Add(method)) {
			i = 0;
			foreach (CiVar param in method.Parameters)
				this.CurrentPureArguments.Add(param, i < arguments.Length ? arguments[i++] : param.Value);
			CiLiteral literal = Resolve(ret.Value) as CiLiteral;
			foreach (CiVar param in method.Parameters)
				this.CurrentPureArguments.Remove(param);
			this.CurrentPureMethods.Remove(method);
			if (literal != null)
				return literal;
		}

		this.CurrentMethod.Calls.Add(method);
		if (this.CurrentPureArguments.Count == 0) {
			expr.Method = symbol;
			expr.Type = method.Type;
		}
		return expr;
	}

	public override void Visit(CiConst statement)
	{
		ResolveConst(statement);
		this.CurrentScope.Add(statement);
		if (statement.Type is CiArrayType)
			((CiClass) this.CurrentScope.Container).ConstArrays.Add(statement);
	}

	CiExpr Resolve(CiExpr expr)
		=> expr.Accept(this, CiPriority.Statement);

	public override void Visit(CiExpr statement)
	{
		Resolve(statement);
	}

	CiExpr ResolveBool(CiExpr expr)
	{
		expr = Resolve(expr);
		Coerce(expr, CiSystem.BoolType);
		return expr;
	}

	bool Resolve(CiStatement[] statements)
	{
		bool reachable = true;
		foreach (CiStatement statement in statements) {
			statement.Accept(this);
			if (!reachable)
				throw StatementException(statement, "Unreachable statement");
			reachable = statement.CompletesNormally;
		}
		return reachable;
	}

	void OpenScope(CiScope scope)
	{
		scope.Parent = this.CurrentScope;
		this.CurrentScope = scope;
	}

	void CloseScope()
	{
		this.CurrentScope = this.CurrentScope.Parent;
	}

	public override void Visit(CiBlock statement)
	{
		OpenScope(statement);
		statement.SetCompletesNormally(Resolve(statement.Statements));
		CloseScope();
	}

	public override void Visit(CiAssert statement)
	{
		statement.Cond = ResolveBool(statement.Cond);
		if (statement.Message != null) {
			statement.Message = Resolve(statement.Message);
			if (!(statement.Message.Type is CiStringType))
				throw StatementException(statement, "The second argument of 'assert' must be a string");
		}
	}

	public override void Visit(CiBreak statement)
	{
		statement.LoopOrSwitch.SetCompletesNormally(true);
	}

	public override void Visit(CiContinue statement)
	{
	}

	void ResolveLoopCond(CiLoop statement)
	{
		if (statement.Cond != null) {
			statement.Cond = ResolveBool(statement.Cond);
			statement.SetCompletesNormally(!(statement.Cond is CiLiteral literal) || !(bool) literal.Value);
		}
		else
			statement.SetCompletesNormally(false);
	}

	public override void Visit(CiDoWhile statement)
	{
		OpenScope(statement);
		ResolveLoopCond(statement);
		statement.Body.Accept(this);
		CloseScope();
	}

	public override void Visit(CiFor statement)
	{
		OpenScope(statement);
		statement.Init?.Accept(this);
		ResolveLoopCond(statement);
		statement.Advance?.Accept(this);
		if (statement.Init is CiVar iter
			&& iter.Type is CiIntegerType
			&& iter.Value != null
			&& statement.Cond is CiBinaryExpr cond
			&& cond.Left.IsReferenceTo(iter)
			&& (cond.Right is CiLiteral || (cond.Right is CiSymbolReference limitSymbol && limitSymbol.Symbol is CiVar))) {
			long step = 0;
			switch (statement.Advance) {
			case CiUnaryExpr unary when unary.Inner.IsReferenceTo(iter):
				switch (unary.Op) {
				case CiToken.Increment:
					step = 1;
					break;
				case CiToken.Decrement:
					step = -1;
					break;
				default:
					break;
				}
				break;
			case CiBinaryExpr binary when binary.Left.IsReferenceTo(iter) && binary.Right is CiLiteral literalStep:
				switch (binary.Op) {
				case CiToken.AddAssign:
					step = (long) literalStep.Value;
					break;
				case CiToken.SubAssign:
					step = -(long) literalStep.Value;
					break;
				default:
					break;
				}
				break;
			default:
				break;
			}
			switch (cond.Op) {
			case CiToken.Less when step > 0:
			case CiToken.LessOrEqual when step > 0:
			case CiToken.Greater when step < 0:
			case CiToken.GreaterOrEqual when step < 0:
				statement.IsRange = true;
				statement.RangeStep = step;
				break;
			default:
				break;
			}
			statement.IsIteratorUsed = false;
		}
		statement.Body.Accept(this);
		CloseScope();
	}

	public override void Visit(CiForeach statement)
	{
		OpenScope(statement);
		CiVar element = statement.Element;
		ResolveType(element);
		statement.Collection.Accept(this);
		if (statement.Collection.Type is CiDictionaryType dict) {
			if (statement.Count != 2)
				throw StatementException(statement, "Expected (TKey key, TValue value) iterator");
			CiVar value = statement.ValueVar;
			ResolveType(value);
			if (!element.Type.IsAssignableFrom(dict.KeyType))
				throw StatementException(statement, "Cannot coerce {0} to {1}", dict.KeyType, element.Type);
			if (!value.Type.IsAssignableFrom(dict.ValueType))
				throw StatementException(statement, "Cannot coerce {0} to {1}", dict.ValueType, value.Type);
		}
		else {
			switch (statement.Collection.Type) {
			case CiArrayStorageType _:
			case CiListType _:
			case CiHashSetType _:
				break;
			default:
				throw StatementException(statement.Collection, "Expected a collection");
			}
			if (statement.Count != 1)
				throw StatementException(statement, "Expected one iterator variable");
			CiType elementType = ((CiCollectionType) statement.Collection.Type).ElementType;
			if (!element.Type.IsAssignableFrom(elementType))
				throw StatementException(statement, "Cannot coerce {0} to {1}", elementType, element.Type);
		}
		statement.SetCompletesNormally(true);
		statement.Body.Accept(this);
		CloseScope();
	}

	public override void Visit(CiIf statement)
	{
		statement.Cond = ResolveBool(statement.Cond);
		statement.OnTrue.Accept(this);
		if (statement.OnFalse != null) {
			statement.OnFalse.Accept(this);
			statement.SetCompletesNormally(statement.OnTrue.CompletesNormally || statement.OnFalse.CompletesNormally);
		}
		else
			statement.SetCompletesNormally(true);
	}

	public override void Visit(CiLock statement)
	{
		statement.Lock = Resolve(statement.Lock);
		Coerce(statement.Lock, CiSystem.LockClass);
		statement.Body.Accept(this);
	}

	public override void Visit(CiNative statement)
	{
	}

	public override void Visit(CiReturn statement)
	{
		if (this.CurrentMethod.Type == CiSystem.VoidType) {
			if (statement.Value != null)
				throw StatementException(statement, "Void method cannot return a value");
		}
		else {
			if (statement.Value == null)
				throw StatementException(statement, "Missing return value");
			statement.Value = Resolve(statement.Value);
			Coerce(statement.Value, this.CurrentMethod.Type);
		}
	}

	public override void Visit(CiSwitch statement)
	{
		OpenScope(statement);
		statement.Value = Resolve(statement.Value);
		switch (statement.Value.Type) {
		case CiRangeType _:
		case CiIntegerType i when i == CiSystem.IntType:
		case CiEnum _:
		case CiStringType _:
			break;
		default:
			throw StatementException(statement.Value, "Switch on type {0} - expected int, enum or string", statement.Value.Type);
		}
		statement.SetCompletesNormally(false);
		foreach (CiCase kase in statement.Cases) {
			for (int i = 0; i < kase.Values.Length; i++) {
				kase.Values[i] = FoldConst(kase.Values[i]);
				Coerce(kase.Values[i], statement.Value.Type);
			}
			if (Resolve(kase.Body))
				throw StatementException(kase.Body.Last(), "Case must end with break, continue, return or throw");
		}
		if (statement.DefaultBody != null) {
			bool reachable = Resolve(statement.DefaultBody);
			if (reachable)
				throw StatementException(statement.DefaultBody.Last(), "Default must end with break, continue, return or throw");
		}
		CloseScope();
	}

	public override void Visit(CiThrow statement)
	{
		if (!this.CurrentMethod.Throws)
			throw StatementException(statement, "'throw' in a method not marked 'throws'");
		statement.Message = Resolve(statement.Message);
		if (!(statement.Message.Type is CiStringType))
			throw StatementException(statement, "The argument of 'throw' must be a string");
	}

	public override void Visit(CiWhile statement)
	{
		OpenScope(statement);
		ResolveLoopCond(statement);
		statement.Body.Accept(this);
		CloseScope();
	}

	CiToken GetPtrModifier(ref CiExpr expr)
	{
		if (expr is CiPostfixExpr postfix) {
			switch (postfix.Op) {
			case CiToken.ExclamationMark:
				expr = postfix.Inner;
				return postfix.Op;
			case CiToken.Hash:
				NotSupported(expr, "Dynamic reference", "cl");
				expr = postfix.Inner;
				return postfix.Op;
			default:
				break;
			}
		}
		return CiToken.EndOfFile; // no modifier
	}

	void ExpectNoPtrModifier(CiExpr expr, CiToken ptrModifier)
	{
		if (ptrModifier != CiToken.EndOfFile)
			throw StatementException(expr, $"Unexpected {ptrModifier} on a non-reference type");
	}

	CiExpr FoldConst(CiExpr expr)
	{
		expr = Resolve(expr);
		if (expr is CiLiteral || expr.IsConstEnum)
			return expr;
		throw StatementException(expr, "Expected constant value");
	}

	int FoldConstInt(CiExpr expr)
	{
		if (FoldConst(expr) is CiLiteral literal && literal.Value is long l) {
			if (l < int.MinValue || l > int.MaxValue)
				throw StatementException(expr, "Only 32-bit ranges supported");
			return (int) l;
		}
		throw StatementException(expr, "Expected integer");
	}

	CiType ToBaseType(CiExpr expr, CiToken ptrModifier)
	{
		switch (expr) {
		case CiSymbolReference symbol:
			// built-in, MyEnum, MyClass, MyClass!, MyClass#
			if (this.Program.TryLookup(symbol.Name) is CiType type) {
				if (type is CiClass klass) {
					if (klass == CiSystem.MatchClass) {
						if (ptrModifier != CiToken.EndOfFile)
							throw StatementException(expr, "Read-write references to the built-in class Match are not supported");
						NotSupported(expr, "Match", "cl");
					}
					return new CiClassPtrType { Name = klass.Name, Class = klass, Modifier = ptrModifier };
				}
				ExpectNoPtrModifier(expr, ptrModifier);
				return type;
			}
			throw StatementException(expr, "Type {0} not found", symbol.Name);

		case CiCallExpr call:
			// string(), MyClass()
			ExpectNoPtrModifier(expr, ptrModifier);
			if (call.Arguments.Length != 0)
				throw StatementException(call, "Expected empty parentheses for storage type");
			if (call.Method.Symbol == CiSystem.ListClass) {
				NotSupported(call, "List", "cl");
				return new CiListType { ElementType = ToType(call.Method.Left, false) };
			}
			if (call.Method.Symbol == CiSystem.StackClass) {
				NotSupported(call, "Stack", "cl");
				return new CiStackType { ElementType = ToType(call.Method.Left, false) };
			}
			if (call.Method.Symbol == CiSystem.HashSetClass) {
				NotSupported(call, "HashSet", "cl");
				return new CiHashSetType { ElementType = ToType(call.Method.Left, false) };
			}
			if (call.Method.Symbol == CiSystem.DictionaryClass) {
				NotSupported(call, "Dictionary", "cl");
				CiExpr[] items = ((CiCollection) call.Method.Left).Items;
				return new CiDictionaryType { KeyType = ToType(items[0], false), ValueType = ToType(items[1], false) };
			}
			if (call.Method.Symbol == CiSystem.SortedDictionaryClass) {
				NotSupported(call, "SortedDictionary", "cl");
				CiExpr[] items = ((CiCollection) call.Method.Left).Items;
				return new CiSortedDictionaryType { KeyType = ToType(items[0], false), ValueType = ToType(items[1], false) };
			}
			if (call.Method.Name == "string") {
				NotSupported(call, "string()", "cl");
				return CiSystem.StringStorageType;
			}
			{
				if (this.Program.TryLookup(call.Method.Name) is CiClass klass) {
					if (klass == CiSystem.MatchClass)
						NotSupported(expr, "Match", "cl");
					else if (klass == CiSystem.LockClass) {
						NotSupported(call, "Lock", "js", "ts", "cl");
						NotYet(call, "Lock", "c");
					}
					return klass;
				}
			}
			throw StatementException(expr, "Class {0} not found", call.Method.Name);

		default:
			throw StatementException(expr, "Invalid type");
		}
	}

	CiType ToType(CiExpr expr, bool dynamic)
	{
		CiExpr minExpr = null;
		if (expr is CiBinaryExpr range && range.Op == CiToken.Range) {
			minExpr = range.Left;
			expr = range.Right;
		}

		CiToken ptrModifier = GetPtrModifier(ref expr);
		CiArrayType outerArray = null; // leftmost in source
		CiArrayType innerArray = null; // rightmost in source
		while (expr is CiBinaryExpr binary && binary.Op == CiToken.LeftBracket) {
			if (binary.Right != null) {
				ExpectNoPtrModifier(expr, ptrModifier);
				CiExpr lengthExpr = Resolve(binary.Right);
				Coerce(lengthExpr, CiSystem.IntType);
				CiArrayStorageType arrayStorage = new CiArrayStorageType { LengthExpr = lengthExpr, ElementType = outerArray };
				if (!dynamic || (binary.Left.IsIndexing)) {
					if (!(lengthExpr is CiLiteral literal))
						throw StatementException(lengthExpr, "Expected constant value");
					long length = (long) literal.Value;
					if (length < 0)
						throw StatementException(expr, "Expected non-negative integer");
					if (length > int.MaxValue)
						throw StatementException(expr, "Integer too big");
					arrayStorage.Length = (int) length;
				}
				outerArray = arrayStorage;
			}
			else
				outerArray = new CiArrayPtrType { Modifier = ptrModifier, ElementType = outerArray };
			innerArray ??= outerArray;
			expr = binary.Left;
			ptrModifier = GetPtrModifier(ref expr);
		}

		CiType baseType;
		if (minExpr != null) {
			ExpectNoPtrModifier(expr, ptrModifier);
			int min = FoldConstInt(minExpr);
			int max = FoldConstInt(expr);
			if (min > max)
				throw StatementException(expr, "Range min greater than max");
			baseType = new CiRangeType(min, max);
		}
		else
			baseType = ToBaseType(expr, ptrModifier);

		if (outerArray == null)
			return baseType;
		innerArray.ElementType = baseType;
		return outerArray;
	}

	CiType ResolveType(CiNamedValue def)
	{
		def.Type = ToType(def.TypeExpr, false);
		return def.Type;
	}

	void ResolveConst(CiConst konst)
	{
		switch (konst.VisitStatus) {
		case CiVisitStatus.NotYet:
			break;
		case CiVisitStatus.InProgress:
			throw StatementException(konst, "Circular dependency in value of constant {0}", konst.Name);
		case CiVisitStatus.Done:
			return;
		}
		konst.VisitStatus = CiVisitStatus.InProgress;
		if (!(this.CurrentScope is CiEnum))
			ResolveType(konst);
		konst.Value = Resolve(konst.Value);
		if (konst.Value is CiCollection coll) {
			if (!(konst.Type is CiArrayType arrayType))
				throw StatementException(konst, "Array initializer for scalar constant {0}", konst.Name);
			foreach (CiExpr item in coll.Items)
				Coerce(item, arrayType.ElementType);
			if (!(arrayType is CiArrayStorageType storageType))
				konst.Type = storageType = new CiArrayStorageType { ElementType = arrayType.ElementType, Length = coll.Items.Length };
			else if (storageType.Length != coll.Items.Length)
				throw StatementException(konst, "Declared {0} elements, initialized {1}", storageType.Length, coll.Items.Length);
			coll.Type = storageType;
		}
		else if (this.CurrentScope is CiEnum && konst.Value.Type is CiRangeType && konst.Value is CiLiteral) {
		}
		else if (konst.Value is CiLiteral || konst.Value.IsConstEnum)
			Coerce(konst.Value, konst.Type);
		else
			throw StatementException(konst.Value, "Value for constant {0} is not constant", konst.Name);
		konst.InMethod = this.CurrentMethod;
		konst.VisitStatus = CiVisitStatus.Done;
	}

	void ResolveConsts(CiContainerType container)
	{
		this.CurrentScope = container;
		switch (container) {
		case CiClass klass:
			foreach (CiConst konst in klass.Consts)
				ResolveConst(konst);
			break;
		case CiEnum enu:
			CiExpr lastValue = null;
			foreach (CiConst konst in enu) {
				if (konst.Value != null)
					ResolveConst(konst);
				else
					konst.Value = new CiImplicitEnumValue(lastValue == null ? 0 : lastValue.IntValue + 1);
				lastValue = konst.Value;
			}
			break;
		default:
			throw new NotImplementedException(container.ToString());
		}
	}

	void ResolveTypes(CiClass klass)
	{
		this.CurrentScope = klass;
		foreach (CiField field in klass.Fields) {
			CiType type = ResolveType(field);
			if (field.Value != null) {
				field.Value = FoldConst(field.Value);
				if (!field.IsAssignableStorage) {
					if (type is CiArrayStorageType array)
						type = array.ElementType;
					Coerce(field.Value, type);
				}
			}
		}
		foreach (CiMethod method in klass.Methods) {
			if (method.TypeExpr == CiSystem.VoidType)
				method.Type = CiSystem.VoidType;
			else
				ResolveType(method);
			foreach (CiVar param in method.Parameters) {
				ResolveType(param);
				if (param.Value != null) {
					param.Value = FoldConst(param.Value);
					Coerce(param.Value, param.Type);
				}
			}
		}
	}

	void ResolveCode(CiClass klass)
	{
		if (klass.Constructor != null) {
			this.CurrentScope = klass;
			this.CurrentMethod = klass.Constructor;
			klass.Constructor.Body.Accept(this);
			this.CurrentMethod = null;
		}
		foreach (CiMethod method in klass.Methods) {
			if (method.Body != null) {
				this.CurrentScope = method.Parameters;
				this.CurrentMethod = method;
				method.Body.Accept(this);
				if (method.Type != CiSystem.VoidType && method.Body.CompletesNormally)
					throw StatementException(method.Body, "Method can complete without a return value");
				this.CurrentMethod = null;
			}
		}
	}

	static void SetLive(CiMethodBase method)
	{
		if (method.IsLive)
			return;
		method.IsLive = true;
		foreach (CiMethod called in method.Calls)
			SetLive(called);
	}

	static void SetLive(CiClass klass)
	{
		if (!klass.IsPublic)
			return;
		foreach (CiMethod method in klass.Methods) {
			if (method.Visibility == CiVisibility.Public || method.Visibility == CiVisibility.Protected)
				SetLive(method);
		}
		if (klass.Constructor != null)
			SetLive(klass.Constructor);
	}

	public CiResolver(CiProgram program, IEnumerable<string> searchDirs, string lang)
	{
		this.Program = program;
		this.SearchDirs = searchDirs;
		this.Lang = lang;
		foreach (CiClass klass in program.OfType<CiClass>())
			ResolveBase(klass);
		foreach (CiContainerType container in program.Cast<CiContainerType>())
			ResolveConsts(container);
		foreach (CiClass klass in program.Classes)
			ResolveTypes(klass);
		foreach (CiClass klass in program.Classes)
			ResolveCode(klass);
		foreach (CiClass klass in program.Classes)
			SetLive(klass);
	}
}

}
