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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Foxoft.Ci
{

public class CiResolver : CiVisitor
{
	readonly CiProgram Program;
	readonly IEnumerable<string> SearchDirs;
	CiScope CurrentScope;
	CiMethodBase CurrentMethod;
	readonly HashSet<CiMethod> CurrentPureMethods = new HashSet<CiMethod>();
	readonly Dictionary<CiVar, CiExpr> CurrentPureArguments = new Dictionary<CiVar, CiExpr>();
	readonly CiType Poison = new CiType { Name = "poison" };

	protected override CiContainerType GetCurrentContainer() => this.CurrentScope.GetContainer();

	CiType PoisonError(CiStatement statement, string message)
	{
		ReportError(statement, message);
		return this.Poison;
	}

	byte[] ReadResource(string name, CiStatement statement)
	{
		foreach (string dir in this.SearchDirs) {
			string path = Path.Combine(dir, name);
			if (File.Exists(path))
				return File.ReadAllBytes(path);
		}
		if (File.Exists(name))
			return File.ReadAllBytes(name);
		ReportError(statement, $"File {name} not found");
		return Array.Empty<byte>();
	}

	void ResolveBase(CiClass klass)
	{
		switch (klass.VisitStatus) {
		case CiVisitStatus.NotYet:
			break;
		case CiVisitStatus.InProgress:
			ReportError(klass, $"Circular inheritance for class {klass.Name}");
			return;
		case CiVisitStatus.Done:
			return;
		}
		if (klass.BaseClassName != null) {
			if (Program.TryLookup(klass.BaseClassName) is CiClass baseClass) {
				if (klass.IsPublic && !baseClass.IsPublic)
					ReportError(klass, "Public class cannot derive from an internal class");
				klass.Parent = baseClass;
				klass.VisitStatus = CiVisitStatus.InProgress;
				ResolveBase(baseClass);
			}
			else
				ReportError(klass, $"Base class {klass.BaseClassName} not found");
		}
		this.Program.Classes.Add(klass);
		klass.VisitStatus = CiVisitStatus.Done;
	}

	static void TakePtr(CiExpr expr)
	{
		if (expr.Type is CiArrayStorageType arrayStg)
			arrayStg.PtrTaken = true;
	}

	bool Coerce(CiExpr expr, CiType type)
	{
		if (expr == this.Poison)
			return false;
		if (!type.IsAssignableFrom(expr.Type)) {
			ReportError(expr, $"Cannot coerce {expr.Type} to {type}");
			return false;
		}
		if (expr is CiPrefixExpr prefix && prefix.Op == CiToken.New && !(type is CiDynamicPtrType)) {
			string kind = ((CiDynamicPtrType) expr.Type).Class.Id == CiId.ArrayPtrClass ? "array" : "object";
			ReportError(expr, $"Dynamically allocated {kind} must be assigned to a {expr.Type} reference");
			return false;
		}
		TakePtr(expr);
		return true;
	}

	static CiRangeType Union(CiRangeType left, CiRangeType right)
	{
		if (right == null)
			return left;
		if (right.Min < left.Min) {
			if (right.Max >= left.Max)
				return right;
			return CiRangeType.New(right.Min, left.Max);
		}
		if (right.Max > left.Max)
			return CiRangeType.New(left.Min, right.Max);
		return left;
	}

	CiType TryGetPtr(CiType type)
	{
		if (type.Id == CiId.StringStorageType)
			return this.Program.System.StringPtrType;
		if (type is CiStorageType storage)
			return new CiReadWriteClassType { Class = storage.Class.Id == CiId.ArrayStorageClass ? this.Program.System.ArrayPtrClass : storage.Class, TypeArg0 = storage.TypeArg0, TypeArg1 = storage.TypeArg1 };
		return type;
	}

	CiType GetCommonType(CiExpr left, CiExpr right)
	{
		if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange)
			return Union(leftRange, rightRange);
		CiType ptr = TryGetPtr(left.Type);
		if (ptr.IsAssignableFrom(right.Type))
			return ptr;
		ptr = TryGetPtr(right.Type);
		if (ptr.IsAssignableFrom(left.Type))
			return ptr;
		return PoisonError(left, $"Incompatible types: {left.Type} and {right.Type}");
	}

	CiType GetIntegerType(CiExpr left, CiExpr right)
	{
		CiType type = this.Program.System.PromoteIntegerTypes(left.Type, right.Type);
		Coerce(left, type);
		Coerce(right, type);
		return type;
	}

	CiIntegerType GetShiftType(CiExpr left, CiExpr right)
	{
		CiIntegerType intType = this.Program.System.IntType;
		Coerce(right, intType);
		if (left.Type.Id == CiId.LongType)
			return (CiIntegerType) left.Type;
		Coerce(left, intType);
		return intType;
	}

	CiType GetNumericType(CiExpr left, CiExpr right)
	{
		CiType type = this.Program.System.PromoteNumericTypes(left.Type, right.Type);
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

	static int SaturatedShiftRight(int a, int b) => a >> (b >= 31 || b < 0 ? 31 : b);

	static CiRangeType UnsignedAnd(CiRangeType left, CiRangeType right)
	{
		int leftVariableBits = left.GetVariableBits();
		int rightVariableBits = right.GetVariableBits();
		int min = left.Min & right.Min & ~CiRangeType.GetMask(~left.Min & ~right.Min & (leftVariableBits | rightVariableBits));
		// Calculate upper bound with variable bits set
		int max = (left.Max | leftVariableBits) & (right.Max | rightVariableBits);
		// The upper bound will never exceed the input
		if (max > left.Max)
			max = left.Max;
		if (max > right.Max)
			max = right.Max;
		if (min > max)
			return CiRangeType.New(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return CiRangeType.New(min, max);
	}

	static CiRangeType UnsignedOr(CiRangeType left, CiRangeType right)
	{
		int leftVariableBits = left.GetVariableBits();
		int rightVariableBits = right.GetVariableBits();
		int min = (left.Min & ~leftVariableBits) | (right.Min & ~rightVariableBits);
		int max = left.Max | right.Max | CiRangeType.GetMask(left.Max & right.Max & CiRangeType.GetMask(leftVariableBits | rightVariableBits));
		// The lower bound will never be less than the input
		if (min < left.Min)
			min = left.Min;
		if (min < right.Min)
			min = right.Min;
		if (min > max)
			return CiRangeType.New(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return CiRangeType.New(min, max);
	}

	static CiRangeType UnsignedXor(CiRangeType left, CiRangeType right)
	{
		int variableBits = left.GetVariableBits() | right.GetVariableBits();
		int min = (left.Min ^ right.Min) & ~variableBits;
		int max = (left.Max ^ right.Max) | variableBits;
		if (min > max)
			return CiRangeType.New(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return CiRangeType.New(min, max);
	}

	delegate CiRangeType UnsignedOp(CiRangeType left, CiRangeType right);

	bool IsEnumOp(CiExpr left, CiExpr right)
	{
		if (left.Type is CiEnum) {
			if (left.Type.Id != CiId.BoolType && !(left.Type is CiEnumFlags))
				ReportError(left, $"Define flags enumeration as: enum* {left.Type}");
			Coerce(right, left.Type);
			return true;
		}
		return false;
	}

	static void SplitBySign(CiRangeType source, out CiRangeType negative, out CiRangeType positive)
	{
		if (source.Min >= 0) {
			negative = null;
			positive = source;
		}
		else if (source.Max < 0) {
			negative = source;
			positive = null;
		}
		else {
			negative = CiRangeType.New(source.Min, -1);
			positive = CiRangeType.New(0, source.Max);
		}
	}

	CiType BitwiseOp(CiExpr left, CiExpr right, UnsignedOp op)
	{
		if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange) {
			SplitBySign(leftRange, out CiRangeType leftNegative, out CiRangeType leftPositive);
			SplitBySign(rightRange, out CiRangeType rightNegative, out CiRangeType rightPositive);
			CiRangeType range = null;
			if (leftNegative != null) {
				if (rightNegative != null)
					range = op(leftNegative, rightNegative);
				if (rightPositive != null)
					range = Union(op(leftNegative, rightPositive), range);
			}
			if (leftPositive != null) {
				if (rightNegative != null)
					range = Union(op(leftPositive, rightNegative), range);
				if (rightPositive != null)
					range = Union(op(leftPositive, rightPositive), range);
			}
			return range;
		}
		if (IsEnumOp(left, right))
			return left.Type;
		return GetIntegerType(left, right);
	}

	public override void VisitAggregateInitializer(CiAggregateInitializer expr)
	{
		List<CiExpr> items = expr.Items;
		for (int i = 0; i < items.Count; i++)
			items[i] = Resolve(items[i]);
	}

	void ResolveObjectLiteral(CiClassType klass, CiAggregateInitializer init)
	{
		foreach (CiBinaryExpr field in init.Items) {
			Trace.Assert(field.Op == CiToken.Assign);
			CiSymbolReference symbol = (CiSymbolReference) field.Left;
			Lookup(symbol, klass);
			if (symbol.Symbol is CiField) {
				field.Right = Resolve(field.Right);
				Coerce(field.Right, symbol.Type);
			}
			else
				ReportError(field, "Expected a field");
		}
	}

	public override void VisitVar(CiVar expr)
	{
		CiType type = ResolveType(expr);
		if (expr.Value != null) {
			if (type is CiStorageType storage && expr.Value is CiAggregateInitializer init)
				ResolveObjectLiteral(storage, init);
			else {
				expr.Value = Resolve(expr.Value);
				if (!expr.IsAssignableStorage()) {
					if (type is CiArrayStorageType array)
						type = array.GetElementType();
					Coerce(expr.Value, type);
				}
			}
		}
		this.CurrentScope.Add(expr);
	}

	public override void VisitLiteralLong(long value)
	{
	}

	public override void VisitLiteralChar(int value)
	{
	}

	public override void VisitLiteralDouble(double value)
	{
	}

	public override void VisitLiteralString(string value)
	{
	}

	public override void VisitLiteralNull()
	{
	}

	public override void VisitLiteralFalse()
	{
	}

	public override void VisitLiteralTrue()
	{
	}

	CiLiteral ToLiteralBool(CiExpr expr, bool value)
	{
		CiLiteral result = value ? new CiLiteralTrue() : new CiLiteralFalse();
		result.Line = expr.Line;
		result.Type = this.Program.System.BoolType;
		return result;
	}

	CiLiteralLong ToLiteralLong(CiExpr expr, long value) => this.Program.System.NewLiteralLong(value, expr.Line);

	CiLiteralDouble ToLiteralDouble(CiExpr expr, double value) => new CiLiteralDouble { Line = expr.Line, Type = this.Program.System.DoubleType, Value = value };

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		int partsCount = 0;
		StringBuilder sb = new StringBuilder();
		for (int partsIndex = 0; partsIndex < expr.Parts.Count; partsIndex++) {
			CiInterpolatedPart part = expr.Parts[partsIndex];
			sb.Append(part.Prefix);
			CiExpr arg = Resolve(part.Argument);
			Coerce(arg, this.Program.System.PrintableType);
			switch (arg.Type) {
			case CiIntegerType _:
				if (" DdXx".IndexOf((char) part.Format) < 0)
					ReportError(arg, "Invalid format string");
				break;
			case CiFloatingType _:
				if (" FfEe".IndexOf((char) part.Format) < 0)
					ReportError(arg, "Invalid format string");
				break;
			default:
				if (part.Format != ' ')
					ReportError(arg, "Invalid format string");
				break;
			}
			int width = 0;
			if (part.WidthExpr != null)
				width = FoldConstInt(part.WidthExpr);
			if (arg is CiLiteral literal && !(arg.Type is CiFloatingType)) { // float formatting is runtime-locale-specific
				string stringArg = part.Format == ' ' ? literal.GetLiteralString()
					: ((CiLiteralLong) arg).Value.ToString((char) part.Format + (part.Precision < 0 ? "" : part.Precision.ToString()));
				if (part.WidthExpr != null)
					stringArg = width >= 0 ? stringArg.PadLeft(width) : stringArg.PadRight(-width);
				sb.Append(stringArg);
			}
			else {
				CiInterpolatedPart targetPart = expr.Parts[partsCount++];
				targetPart.Prefix = sb.ToString();
				targetPart.Argument = arg;
				targetPart.WidthExpr = part.WidthExpr;
				targetPart.Width = width;
				targetPart.Format = part.Format;
				targetPart.Precision = part.Precision;
				sb.Clear();
			}
		}
		sb.Append(expr.Suffix);
		if (partsCount == 0)
			return this.Program.System.NewLiteralString(sb.ToString(), expr.Line);
		expr.Type = this.Program.System.StringStorageType;
		expr.Parts.RemoveRange(partsCount, expr.Parts.Count - partsCount);
		expr.Suffix = sb.ToString();
		return expr;
	}

	CiExpr Lookup(CiSymbolReference expr, CiScope scope)
	{
		if (expr.Symbol == null) {
			expr.Symbol = scope.TryLookup(expr.Name);
			if (expr.Symbol == null)
				return PoisonError(expr, $"{expr.Name} not found");
			expr.Type = expr.Symbol.Type;
		}
		if (!(scope is CiEnum) && expr.Symbol is CiConst konst) {
			ResolveConst(konst);
			if (konst.Value is CiLiteral || konst.Value is CiSymbolReference)
				return konst.Value;
		}
		return expr;
	}

	public override CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
	{
		if (expr.Left != null) {
			CiExpr left = Resolve(expr.Left);
			CiSymbolReference leftSymbol = left as CiSymbolReference;
			CiScope scope;
			if (leftSymbol != null && leftSymbol.Symbol.Id == CiId.BasePtr) {
				if (this.CurrentMethod == null || !(this.CurrentMethod.Parent.Parent is CiClass baseClass))
					return PoisonError(expr, "No base class");
				scope = baseClass;
				// TODO: static?
			}
			else if (leftSymbol != null && leftSymbol.Symbol is CiScope obj)
				scope = obj;
			else
				scope = left.Type;
				// if (scope is CiClassType ptr)
				//	scope = ptr.Class;
			CiExpr result = Lookup(expr, scope);
			if (result != expr)
				return result;
			if (expr.Symbol is CiMember member) {
				switch (member.Visibility) {
				case CiVisibility.Private:
					if (member.Parent != this.CurrentMethod.Parent
					 || this.CurrentMethod.Parent != (scope as CiClass ?? ((CiClassType) scope).Class) /* enforced by Java, but not C++/C#/TS */)
						ReportError(expr, $"Cannot access private member {expr.Name}");
					break;
				case CiVisibility.Protected:
					if (leftSymbol != null && leftSymbol.Symbol.Id == CiId.BasePtr)
						break;
					if (!((CiClass) this.CurrentMethod.Parent).IsSameOrBaseOf(scope as CiClass ?? ((CiClassType) scope).Class) /* enforced by C++/C#/TS but not Java */)
						ReportError(expr, $"Cannot access protected member {expr.Name}");
					break;
				case CiVisibility.NumericElementType when left.Type is CiClassType klass:
					if (!(klass.GetElementType() is CiNumericType))
						ReportError(expr, "Method restricted to collections of numbers");
					break;
				case CiVisibility.FinalValueType: // DictionaryAdd
					if (!((CiClassType) left.Type).GetValueType().IsFinal())
						ReportError(expr, "Method restricted to dictionaries with storage values");
					break;
				default:
					switch (expr.Symbol.Id) {
					case CiId.ArrayLength:
						return ToLiteralLong(expr, ((CiArrayStorageType) scope).Length);
					case CiId.StringLength when left is CiLiteralString leftLiteral:
						int length = leftLiteral.GetAsciiLength();
						if (length >= 0)
							return ToLiteralLong(expr, length);
						break;
					default:
						break;
					}
					break;
				}
			}
			return new CiSymbolReference { Line = expr.Line, Left = left, Name = expr.Name, Symbol = expr.Symbol, Type = expr.Type };
		}

		CiExpr resolved = Lookup(expr, this.CurrentScope);
		if (expr.Symbol is CiMember nearMember
		 && nearMember.Visibility == CiVisibility.Private
		 && nearMember.Parent is CiClass memberClass // not local const
		 && memberClass != (this.CurrentScope as CiClass ?? this.CurrentMethod.Parent))
			ReportError(expr, $"Cannot access private member {expr.Name}");
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
					ReportError(expr, "Cannot assign a foreach iteration variable");
					break;
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

	public override CiExpr VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
	{
		CiExpr inner;
		CiType type;
		CiRangeType range;
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			inner = Resolve(expr.Inner);
			CheckLValue(inner);
			Coerce(inner, this.Program.System.DoubleType);
			range = inner.Type as CiRangeType;
			if (range != null) {
				int delta = expr.Op == CiToken.Increment ? 1 : -1;
				type = CiRangeType.New(range.Min + delta, range.Max + delta);
			}
			else
				type = inner.Type;
			expr.Inner = inner;
			expr.Type = type;
			return expr;
		case CiToken.Minus:
			inner = Resolve(expr.Inner);
			Coerce(inner, this.Program.System.DoubleType);
			range = inner.Type as CiRangeType;
			if (range != null)
				type = range = CiRangeType.New(SaturatedNeg(range.Max), SaturatedNeg(range.Min));
			else if (inner is CiLiteralDouble d)
				return ToLiteralDouble(expr, -d.Value);
			else if (inner is CiLiteralLong l)
				return ToLiteralLong(expr, -l.Value);
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
				Coerce(inner, this.Program.System.IntType);
				range = inner.Type as CiRangeType;
				if (range != null)
					type = range = CiRangeType.New(~range.Max, ~range.Min);
				else
					type = inner.Type;
			}
			break;
		case CiToken.ExclamationMark:
			inner = ResolveBool(expr.Inner);
			return new CiPrefixExpr { Line = expr.Line, Op = CiToken.ExclamationMark, Inner = inner, Type = this.Program.System.BoolType };
		case CiToken.New:
			if (expr.Type != null)
				return expr;
			if (expr.Inner is CiBinaryExpr binaryNew && binaryNew.Op == CiToken.LeftBrace) {
				if (!(ToType(binaryNew.Left, true) is CiClassType klass) || klass is CiReadWriteClassType)
					return PoisonError(expr, "Invalid argument to new");
				CiAggregateInitializer init = (CiAggregateInitializer) binaryNew.Right;
				ResolveObjectLiteral(klass, init);
				expr.Type = new CiDynamicPtrType { Line = expr.Line, Class = klass.Class };
				expr.Inner = init;
				return expr;
			}
			type = ToType(expr.Inner, true);
			switch (type) {
			case CiArrayStorageType array:
				expr.Type = new CiDynamicPtrType { Line = expr.Line, Class = this.Program.System.ArrayPtrClass, TypeArg0 = array.GetElementType() };
				expr.Inner = array.LengthExpr;
				return expr;
			case CiStorageType klass:
				expr.Type = new CiDynamicPtrType { Line = expr.Line, Class = klass.Class };
				expr.Inner = null;
				return expr;
			default:
				return PoisonError(expr, "Invalid argument to new");
			}
		case CiToken.Resource:
			if (!(FoldConst(expr.Inner) is CiLiteralString resourceName))
				return PoisonError(expr, "Resource name must be string");
			inner = resourceName;
			string name = resourceName.Value;
			if (!this.Program.Resources.TryGetValue(name, out byte[] content)) {
				content = ReadResource(name, expr);
				this.Program.Resources.Add(name, content);
			}
			type = new CiArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = this.Program.System.ByteType, Length = content.Length };
			range = null;
			break;
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		if (range != null && range.Min == range.Max)
			return ToLiteralLong(expr, range.Min);
		return new CiPrefixExpr { Line = expr.Line, Op = expr.Op, Inner = inner, Type = type };
	}

	public override CiExpr VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent)
	{
		expr.Inner = Resolve(expr.Inner);
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			CheckLValue(expr.Inner);
			Coerce(expr.Inner, this.Program.System.DoubleType);
			expr.Type = expr.Inner.Type;
			return expr;
		default:
			ReportError(expr, $"Unexpected {CiLexer.TokenToString(expr.Op)}");
			return expr;
		}
	}

	CiInterpolatedString ToInterpolatedString(CiExpr expr)
	{
		if (expr is CiInterpolatedString interpolated)
			return interpolated;
		CiInterpolatedString result = new CiInterpolatedString { Line = expr.Line, Type = this.Program.System.StringStorageType };
		if (expr is CiLiteral literal)
			result.Suffix = literal.GetLiteralString();
		else {
			result.AddPart("", expr);
			result.Suffix = "";
		}
		return result;
	}

	CiInterpolatedString Concatenate(CiInterpolatedString left, CiInterpolatedString right)
	{
		CiInterpolatedString result = new CiInterpolatedString { Line = left.Line, Type = this.Program.System.StringStorageType };
		result.Parts.AddRange(left.Parts);
		if (right.Parts.Count == 0)
			result.Suffix = left.Suffix + right.Suffix;
		else {
			result.Parts.AddRange(right.Parts);
			CiInterpolatedPart middle = result.Parts[left.Parts.Count];
			middle.Prefix = left.Suffix + middle.Prefix;
			result.Suffix = right.Suffix;
		}
		return result;
	}

	static CiRangeType NewRangeType(int a, int b, int c, int d)
	{
		if (a > b) {
			int t = a;
			a = b;
			b = t;
		}
		if (c > d) {
			int t = c;
			c = d;
			d = t;
		}
		return CiRangeType.New(a <= c ? a : c, b >= d ? b : d);
	}

	CiExpr ResolveEquality(CiBinaryExpr expr, CiExpr left, CiExpr right)
	{
		if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange) {
			if (leftRange.Min == leftRange.Max && leftRange.Min == rightRange.Min && leftRange.Min == rightRange.Max)
				return ToLiteralBool(expr, expr.Op == CiToken.Equal);
			if (leftRange.Max < rightRange.Min || leftRange.Min > rightRange.Max)
				return ToLiteralBool(expr, expr.Op == CiToken.NotEqual);
		}
		else if (left.Type == right.Type) {
			switch (left) {
			case CiLiteralLong leftLong when right is CiLiteralLong rightLong:
				return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (leftLong.Value == rightLong.Value));
			case CiLiteralDouble leftDouble when right is CiLiteralDouble rightDouble:
				return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (leftDouble.Value == rightDouble.Value));
			case CiLiteralString leftString when right is CiLiteralString rightString:
				return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (leftString.Value == rightString.Value));
			case CiLiteralNull _:
				return ToLiteralBool(expr, expr.Op == CiToken.Equal);
			case CiLiteralFalse _:
				return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (right is CiLiteralFalse));
			case CiLiteralTrue _:
				return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (right is CiLiteralTrue));
			default:
				break;
			}
			if (left.IsConstEnum() && right.IsConstEnum())
				return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (left.IntValue() == right.IntValue()));
		}
		if (!left.Type.IsAssignableFrom(right.Type) && !right.Type.IsAssignableFrom(left.Type))
			return PoisonError(expr, $"Cannot compare {left.Type} with {right.Type}");
		TakePtr(left);
		TakePtr(right);
		return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = this.Program.System.BoolType };
	}

	void CheckComparison(CiExpr left, CiExpr right)
	{
		CiType doubleType = this.Program.System.DoubleType;
		Coerce(left, doubleType);
		Coerce(right, doubleType);
	}

	public override CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
	{
		CiExpr left = Resolve(expr.Left);
		CiExpr right = Resolve(expr.Right);
		CiType type;
		CiRangeType leftRange = left.Type as CiRangeType;
		CiRangeType rightRange = right.Type as CiRangeType;

		switch (expr.Op) {
		case CiToken.LeftBracket:
			if (!(left.Type is CiClassType klass))
				return PoisonError(expr, "Cannot index this object");
			switch (klass.Class.Id) {
			case CiId.StringClass:
				Coerce(right, this.Program.System.IntType);
				if (left is CiLiteralString stringLiteral && right is CiLiteralLong indexLiteral) {
					long i = indexLiteral.Value;
					if (i >= 0 && i <= int.MaxValue) {
						int c = stringLiteral.GetAsciiAt((int) i);
						if (c >= 0)
							return CiLiteralChar.New(c, expr.Line);
					}
				}
				type = this.Program.System.CharType;
				break;
			case CiId.ArrayPtrClass:
			case CiId.ArrayStorageClass:
			case CiId.ListClass:
				Coerce(right, this.Program.System.IntType);
				type = klass.GetElementType();
				break;
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
			case CiId.OrderedDictionaryClass:
				Coerce(right, klass.GetKeyType());
				type = klass.GetValueType();
				break;
			default:
				return PoisonError(expr, "Cannot index this object");
			}
			break;

		case CiToken.Plus:
			if (leftRange != null && rightRange != null) {
				type = CiRangeType.New(
					SaturatedAdd(leftRange.Min, rightRange.Min),
					SaturatedAdd(leftRange.Max, rightRange.Max));
			}
			else if (left.Type is CiStringType || right.Type is CiStringType) {
				Coerce(left, this.Program.System.PrintableType);
				Coerce(right, this.Program.System.PrintableType);
				if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral)
					return this.Program.System.NewLiteralString(leftLiteral.GetLiteralString() + rightLiteral.GetLiteralString(), expr.Line);
				if (left is CiInterpolatedString || right is CiInterpolatedString)
					return Concatenate(ToInterpolatedString(left), ToInterpolatedString(right));
				type = this.Program.System.StringPtrType;
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Minus:
			if (leftRange != null && rightRange != null) {
				type = CiRangeType.New(
					SaturatedSub(leftRange.Min, rightRange.Max),
					SaturatedSub(leftRange.Max, rightRange.Min));
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Asterisk:
			if (leftRange != null && rightRange != null) {
				type = NewRangeType(
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
				type = NewRangeType(
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
					return PoisonError(expr, "Mod zero");
				type = CiRangeType.New(
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
				type = CiRangeType.New(result, result);
			}
			else
				type = GetShiftType(left, right);
			break;
		case CiToken.ShiftRight:
			if (leftRange != null && rightRange != null) {
				if (rightRange.Min < 0)
					rightRange = CiRangeType.New(0, 32);
				type = CiRangeType.New(
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
					return ToLiteralBool(expr, true);
				if (leftRange.Min >= rightRange.Max)
					return ToLiteralBool(expr, false);
			}
			else
				CheckComparison(left, right);
			type = this.Program.System.BoolType;
			break;
		case CiToken.LessOrEqual:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Max <= rightRange.Min)
					return ToLiteralBool(expr, true);
				if (leftRange.Min > rightRange.Max)
					return ToLiteralBool(expr, false);
			}
			else
				CheckComparison(left, right);
			type = this.Program.System.BoolType;
			break;
		case CiToken.Greater:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Min > rightRange.Max)
					return ToLiteralBool(expr, true);
				if (leftRange.Max <= rightRange.Min)
					return ToLiteralBool(expr, false);
			}
			else
				CheckComparison(left, right);
			type = this.Program.System.BoolType;
			break;
		case CiToken.GreaterOrEqual:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Min >= rightRange.Max)
					return ToLiteralBool(expr, true);
				if (leftRange.Max < rightRange.Min)
					return ToLiteralBool(expr, false);
			}
			else
				CheckComparison(left, right);
			type = this.Program.System.BoolType;
			break;

		case CiToken.CondAnd:
			Coerce(left, this.Program.System.BoolType);
			Coerce(right, this.Program.System.BoolType);
			if (left is CiLiteralTrue)
				return right;
			if (left is CiLiteralFalse || right is CiLiteralTrue)
				return left;
			type = this.Program.System.BoolType;
			break;
		case CiToken.CondOr:
			Coerce(left, this.Program.System.BoolType);
			Coerce(right, this.Program.System.BoolType);
			if (left is CiLiteralTrue || right is CiLiteralFalse)
				return left;
			if (left is CiLiteralFalse)
				return right;
			type = this.Program.System.BoolType;
			break;

		case CiToken.Assign:
			CheckLValue(left);
			Coerce(right, left.Type);
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.AddAssign:
			CheckLValue(left);
			if (left.Type.Id == CiId.StringStorageType)
				Coerce(right, this.Program.System.PrintableType);
			else {
				Coerce(left, this.Program.System.DoubleType);
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
			Coerce(left, this.Program.System.DoubleType);
			Coerce(right, left.Type);
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.ModAssign:
		case CiToken.ShiftLeftAssign:
		case CiToken.ShiftRightAssign:
			CheckLValue(left);
			Coerce(left, this.Program.System.IntType);
			Coerce(right, this.Program.System.IntType);
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.AndAssign:
		case CiToken.OrAssign:
		case CiToken.XorAssign:
			CheckLValue(left);
			if (!IsEnumOp(left, right)) {
				Coerce(left, this.Program.System.IntType);
				Coerce(right, this.Program.System.IntType);
			}
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;

		case CiToken.Is:
			if (!(left.Type is CiClassType leftPtr) || left.Type is CiStorageType)
				return PoisonError(expr, "Left hand side of the 'is' operator must be an object reference");
			CiClass klass2;
			switch (right) {
			case CiSymbolReference symbol:
				if (symbol.Symbol is CiClass klass3)
					expr.Right = klass2 = klass3;
				else
					return PoisonError(expr, "Right hand side of the 'is' operator must be a class name");
				break;
			case CiVar def:
				if (!(def.Type is CiClassType rightPtr))
					return PoisonError(expr, "Right hand side of the 'is' operator must be an object reference definition");
				if (rightPtr is CiReadWriteClassType
				 && !(leftPtr is CiDynamicPtrType)
				 && (rightPtr is CiDynamicPtrType || !(leftPtr is CiReadWriteClassType)))
					return PoisonError(expr, $"{leftPtr} cannot be casted to {rightPtr}");
				// TODO: outside assert NotSupported(expr, "'is' operator", "c", "cpp", "js", "py", "swift", "ts", "cl");
				klass2 = rightPtr.Class;
				break;
			default:
				return PoisonError(expr, "Right hand side of the 'is' operator must be a class name");
			}
			if (leftPtr.Class == klass2)
				return PoisonError(expr, $"{left} is {leftPtr}, the 'is' operator would always return 'true'");
			if (!leftPtr.Class.IsSameOrBaseOf(klass2))
				return PoisonError(expr, $"{leftPtr} is not base class of {klass2.Name}, the 'is' operator would always return 'false'");
			expr.Left = left;
			expr.Type = this.Program.System.BoolType;
			return expr;

		case CiToken.Range:
			return PoisonError(expr, "Range within an expression");
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		if (type is CiRangeType range && range.Min == range.Max)
			return ToLiteralLong(expr, range.Min);
		return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = type };
	}

	public override CiExpr VisitSelectExpr(CiSelectExpr expr, CiPriority parent)
	{
		CiExpr cond = ResolveBool(expr.Cond);
		CiExpr onTrue = Resolve(expr.OnTrue);
		CiExpr onFalse = Resolve(expr.OnFalse);
		CiType type = GetCommonType(onTrue, onFalse);
		Coerce(onTrue, type);
		Coerce(onFalse, type);
		if (cond is CiLiteralTrue)
			return onTrue;
		if (cond is CiLiteralFalse)
			return onFalse;
		return new CiSelectExpr { Line = expr.Line, Cond = cond, OnTrue = onTrue, OnFalse = onFalse, Type = type };
	}

	CiType EvalType(CiClassType generic, CiType type)
	{
		if (type.Id == CiId.TypeParam0)
			return generic.TypeArg0;
		if (type.Id == CiId.TypeParam0NotFinal)
			return generic.TypeArg0.IsFinal() ? null : generic.TypeArg0;
		if (type is CiReadWriteClassType array && array.IsArray() && array.GetElementType().Id == CiId.TypeParam0)
			return new CiReadWriteClassType { Class = this.Program.System.ArrayPtrClass, TypeArg0 = generic.TypeArg0 };
		return type;
	}

	bool CanCall(CiExpr obj, CiMethod method, List<CiExpr> arguments)
	{
		CiVar param = method.Parameters.FirstParameter();
		foreach (CiExpr arg in arguments) {
			if (param == null)
				return false;
			CiType type = param.Type;
			if (obj != null && obj.Type is CiClassType generic)
				type = EvalType(generic, type);
			if (!type.IsAssignableFrom(arg.Type))
				return false;
			param = param.NextParameter();
		}
		return param == null || param.Value != null;
	}

	public override CiExpr VisitCallExpr(CiCallExpr expr, CiPriority parent)
	{
		if (!(Resolve(expr.Method) is CiSymbolReference symbol))
			return this.Poison;
		List<CiExpr> arguments;
		int i;
		if (this.CurrentPureArguments.Count == 0) {
			arguments = expr.Arguments;
			for (i = 0; i < arguments.Count; i++) {
				if (!(arguments[i] is CiLambdaExpr))
					arguments[i] = Resolve(arguments[i]);
			}
		}
		else {
			arguments = new List<CiExpr>(expr.Arguments.Count);
			foreach (CiExpr arg in expr.Arguments)
				arguments.Add(Resolve(arg));
		}
		CiMethod method;
		switch (symbol.Symbol) {
		case null:
			return this.Poison;
		case CiMethod m:
			method = m;
			break;
		case CiMethodGroup group:
			method = group.Methods.FirstOrDefault(m => CanCall(symbol.Left, m, arguments))
				?? /* pick first for the error message */ group.Methods[0];
			break;
		default:
			return PoisonError(symbol, "Expected a method");
		}

		// TODO: check static
		i = 0;
		for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
			CiType type = param.Type;
			if (symbol.Left != null && symbol.Left.Type is CiClassType generic) {
				type = EvalType(generic, type);
				if (type == null)
					continue;
			}
			if (i >= arguments.Count) {
				if (param.Value != null)
					break;
				return PoisonError(expr, $"Too few arguments for '{method.Name}'");
			}
			CiExpr arg = arguments[i++];
			if (type.Id == CiId.TypeParam0Predicate && arg is CiLambdaExpr lambda) {
				lambda.First.Type = ((CiClassType) symbol.Left.Type).TypeArg0;
				OpenScope(lambda);
				lambda.Body = Resolve(lambda.Body);
				CloseScope();
				Coerce(lambda.Body, this.Program.System.BoolType);
			}
			else
				Coerce(arg, type);
		}
		if (i < arguments.Count)
			return PoisonError(arguments[i], "Too many arguments");

		if (method.Throws) {
			if (this.CurrentMethod == null)
				return PoisonError(expr, $"Cannot call method '{method.Name}' here because it is marked 'throws'");
			if (!this.CurrentMethod.Throws)
				return PoisonError(expr, "Method marked 'throws' called from a method not marked 'throws'");
		}

		symbol.Symbol = method;

		if (method.CallType == CiCallType.Static
		 && method.Body is CiReturn ret
		 && arguments.All(arg => arg is CiLiteral)
		 && this.CurrentPureMethods.Add(method)) {
			i = 0;
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter())
				this.CurrentPureArguments.Add(param, i < arguments.Count ? arguments[i++] : param.Value);
			CiLiteral literal = Resolve(ret.Value) as CiLiteral;
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter())
				this.CurrentPureArguments.Remove(param);
			this.CurrentPureMethods.Remove(method);
			if (literal != null)
				return literal;
		}

		if (this.CurrentMethod != null)
			this.CurrentMethod.Calls.Add(method);
		if (this.CurrentPureArguments.Count == 0) {
			expr.Method = symbol;
			CiType type = method.Type;
			if (symbol.Left != null && symbol.Left.Type is CiClassType generic)
				type = EvalType(generic, type);
			expr.Type = type;
		}
		return expr;
	}

	public override void VisitLambdaExpr(CiLambdaExpr expr) => throw new NotImplementedException(); // TODO: error message

	public override void VisitConst(CiConst statement)
	{
		ResolveConst(statement);
		this.CurrentScope.Add(statement);
		if (statement.Type is CiArrayStorageType)
			((CiClass) this.CurrentScope.GetContainer()).ConstArrays.Add(statement);
	}

	CiExpr Resolve(CiExpr expr) => expr.Accept(this, CiPriority.Statement);

	public override void VisitExpr(CiExpr statement) => Resolve(statement);

	CiExpr ResolveBool(CiExpr expr)
	{
		expr = Resolve(expr);
		Coerce(expr, this.Program.System.BoolType);
		return expr;
	}

	bool Resolve(List<CiStatement> statements)
	{
		bool reachable = true;
		foreach (CiStatement statement in statements) {
			statement.AcceptStatement(this);
			if (!reachable) {
				ReportError(statement, "Unreachable statement");
				return false;
			}
			reachable = statement.CompletesNormally();
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

	public override void VisitBlock(CiBlock statement)
	{
		OpenScope(statement);
		statement.SetCompletesNormally(Resolve(statement.Statements));
		CloseScope();
	}

	public override void VisitAssert(CiAssert statement)
	{
		statement.Cond = ResolveBool(statement.Cond);
		if (statement.Message != null) {
			statement.Message = Resolve(statement.Message);
			if (!(statement.Message.Type is CiStringType))
				ReportError(statement, "The second argument of 'assert' must be a string");
		}
	}

	public override void VisitBreak(CiBreak statement) => statement.LoopOrSwitch.SetCompletesNormally(true);

	public override void VisitContinue(CiContinue statement)
	{
	}

	void ResolveLoopCond(CiLoop statement)
	{
		if (statement.Cond != null) {
			statement.Cond = ResolveBool(statement.Cond);
			statement.SetCompletesNormally(!(statement.Cond is CiLiteralTrue));
		}
		else
			statement.SetCompletesNormally(false);
	}

	public override void VisitDoWhile(CiDoWhile statement)
	{
		OpenScope(statement);
		ResolveLoopCond(statement);
		statement.Body.AcceptStatement(this);
		CloseScope();
	}

	public override void VisitFor(CiFor statement)
	{
		OpenScope(statement);
		statement.Init?.AcceptStatement(this);
		ResolveLoopCond(statement);
		statement.Advance?.AcceptStatement(this);
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
			case CiBinaryExpr binary when binary.Left.IsReferenceTo(iter) && binary.Right is CiLiteralLong literalStep:
				switch (binary.Op) {
				case CiToken.AddAssign:
					step = literalStep.Value;
					break;
				case CiToken.SubAssign:
					step = -literalStep.Value;
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
		statement.Body.AcceptStatement(this);
		CloseScope();
	}

	public override void VisitForeach(CiForeach statement)
	{
		OpenScope(statement);
		CiVar element = statement.GetVar();
		ResolveType(element);
		Resolve(statement.Collection);
		if (statement.Collection.Type is CiClassType klass) {
			switch (klass.Class.Id) {
			case CiId.StringClass:
				if (statement.Count() != 1 || !element.Type.IsAssignableFrom(this.Program.System.IntType))
					ReportError(statement, "Expected int iterator variable");
				break;
			case CiId.ArrayStorageClass:
			case CiId.ListClass:
			case CiId.HashSetClass:
				if (statement.Count() != 1)
					ReportError(statement, "Expected one iterator variable");
				else if (!element.Type.IsAssignableFrom(klass.GetElementType()))
					ReportError(statement, $"Cannot coerce {klass.GetElementType()} to {element.Type}");
				break;
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
			case CiId.OrderedDictionaryClass:
				if (statement.Count() != 2)
					ReportError(statement, "Expected (TKey key, TValue value) iterator");
				else {
					CiVar value = statement.GetValueVar();
					ResolveType(value);
					if (!element.Type.IsAssignableFrom(klass.GetKeyType()))
						ReportError(statement, $"Cannot coerce {klass.GetKeyType()} to {element.Type}");
					else if (!value.Type.IsAssignableFrom(klass.GetValueType()))
						ReportError(statement, $"Cannot coerce {klass.GetValueType()} to {value.Type}");
				}
				break;
			default:
				ReportError(statement, "'foreach' invalid on {klass}");
				break;
			}
		}
		else
			ReportError(statement, $"'foreach' invalid on {statement.Collection.Type}");
		statement.SetCompletesNormally(true);
		statement.Body.AcceptStatement(this);
		CloseScope();
	}

	public override void VisitIf(CiIf statement)
	{
		statement.Cond = ResolveBool(statement.Cond);
		statement.OnTrue.AcceptStatement(this);
		if (statement.OnFalse != null) {
			statement.OnFalse.AcceptStatement(this);
			statement.SetCompletesNormally(statement.OnTrue.CompletesNormally() || statement.OnFalse.CompletesNormally());
		}
		else
			statement.SetCompletesNormally(true);
	}

	public override void VisitLock(CiLock statement)
	{
		statement.Lock = Resolve(statement.Lock);
		Coerce(statement.Lock, this.Program.System.LockPtrType);
		statement.Body.AcceptStatement(this);
	}

	public override void VisitNative(CiNative statement)
	{
	}

	public override void VisitReturn(CiReturn statement)
	{
		if (this.CurrentMethod.Type.Id == CiId.VoidType) {
			if (statement.Value != null)
				ReportError(statement, "Void method cannot return a value");
		}
		else if (statement.Value == null)
			ReportError(statement, "Missing return value");
		else {
			statement.Value = Resolve(statement.Value);
			Coerce(statement.Value, this.CurrentMethod.Type);
			if (statement.Value is CiSymbolReference symbol
			 && symbol.Symbol is CiVar local
			 && (local.Type.IsFinal() || local.Type.Id == CiId.StringStorageType)
			 && this.CurrentMethod.Type.IsNullable())
				ReportError(statement, "Returning dangling reference to local storage");
		}
	}

	public override void VisitSwitch(CiSwitch statement)
	{
		OpenScope(statement);
		statement.Value = Resolve(statement.Value);
		switch (statement.Value.Type) {
		case CiIntegerType i when i.Id != CiId.LongType:
		case CiEnum _:
		// case CiStringType _: matched by case CiClassType
			break;
		case CiClassType klass when !(klass is CiStorageType):
			break;
		default:
			ReportError(statement.Value, $"Switch on type {statement.Value.Type} - expected int, enum, string or object reference");
			return;
		}
		statement.SetCompletesNormally(false);
		foreach (CiCase kase in statement.Cases) {
			for (int i = 0; i < kase.Values.Count; i++) {
				if (statement.Value.Type is CiClassType switchPtr && switchPtr.Class.Id != CiId.StringClass) {
					if (!(kase.Values[i] is CiVar def) || def.Value != null)
						ReportError(kase.Values[i], "Expected 'case Type name'");
					else if (!(ResolveType(def) is CiClassType casePtr) || casePtr is CiStorageType)
						ReportError(def, "'case' with non-reference type");
					else if (casePtr is CiReadWriteClassType
					 && !(switchPtr is CiDynamicPtrType)
					 && (casePtr is CiDynamicPtrType || !(switchPtr is CiReadWriteClassType)))
						ReportError(def, $"{switchPtr} cannot be casted to {casePtr}");
					else if (switchPtr.Class == casePtr.Class)
						ReportError(def, $"{statement.Value} is {switchPtr}, 'case {casePtr}' would always match");
					else if (!switchPtr.Class.IsSameOrBaseOf(casePtr.Class))
						ReportError(def, $"{switchPtr} is not base class of {casePtr.Class.Name}, 'case {casePtr}' would never match");
					else
						statement.Add(def);
				}
				else {
					kase.Values[i] = FoldConst(kase.Values[i]);
					Coerce(kase.Values[i], statement.Value.Type);
				}
			}
			if (Resolve(kase.Body))
				ReportError(kase.Body.Last(), "Case must end with break, continue, return or throw");
		}
		if (statement.DefaultBody.Count > 0) {
			bool reachable = Resolve(statement.DefaultBody);
			if (reachable)
				ReportError(statement.DefaultBody.Last(), "Default must end with break, continue, return or throw");
		}
		CloseScope();
	}

	public override void VisitThrow(CiThrow statement)
	{
		if (!this.CurrentMethod.Throws)
			ReportError(statement, "'throw' in a method not marked 'throws'");
		statement.Message = Resolve(statement.Message);
		if (!(statement.Message.Type is CiStringType))
			ReportError(statement, "The argument of 'throw' must be a string");
	}

	public override void VisitWhile(CiWhile statement)
	{
		OpenScope(statement);
		ResolveLoopCond(statement);
		statement.Body.AcceptStatement(this);
		CloseScope();
	}

	CiToken GetPtrModifier(ref CiExpr expr)
	{
		if (expr is CiPostfixExpr postfix) {
			switch (postfix.Op) {
			case CiToken.ExclamationMark:
			case CiToken.Hash:
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
			ReportError(expr, $"Unexpected {ptrModifier} on a non-reference type");
	}

	CiExpr FoldConst(CiExpr expr)
	{
		expr = Resolve(expr);
		if (expr is CiLiteral || expr.IsConstEnum())
			return expr;
		ReportError(expr, "Expected constant value");
		return expr;
	}

	int FoldConstInt(CiExpr expr)
	{
		if (FoldConst(expr) is CiLiteralLong literal) {
			long l = literal.Value;
			if (l < int.MinValue || l > int.MaxValue) {
				ReportError(expr, "Only 32-bit ranges supported");
				return 0;
			}
			return (int) l;
		}
		ReportError(expr, "Expected integer");
		return 0;
	}

	static CiClassType CreateClassPtr(CiClass klass, CiToken ptrModifier)
	{
		CiClassType ptr = ptrModifier switch {
				CiToken.EndOfFile => new CiClassType(),
				CiToken.ExclamationMark => new CiReadWriteClassType(),
				CiToken.Hash => new CiDynamicPtrType(),
				_ => throw new NotImplementedException()
			};
		ptr.Class = klass;
		return ptr;
	}

	void FillGenericClass(CiClassType result, CiSymbol klass, CiAggregateInitializer typeArgExprs)
	{
		if (!(klass is CiClass generic)) {
			ReportError(typeArgExprs, $"{klass.Name} is not a class");
			return;
		}
		List<CiType> typeArgs = new List<CiType>();
		foreach (CiExpr typeArgExpr in typeArgExprs.Items)
			typeArgs.Add(ToType(typeArgExpr, false));
		if (typeArgs.Count != generic.TypeParameterCount) {
			ReportError(typeArgExprs, $"Expected {generic.TypeParameterCount} type arguments for {generic.Name}, got {typeArgs.Count}");
			return;
		}
		result.Class = generic;
		result.TypeArg0 = typeArgs[0];
		if (typeArgs.Count == 2)
			result.TypeArg1 = typeArgs[1];
	}

	CiType ToBaseType(CiExpr expr, CiToken ptrModifier)
	{
		switch (expr) {
		case CiSymbolReference symbol:
			// built-in, MyEnum, MyClass, MyClass!, MyClass#
			if (this.Program.TryLookup(symbol.Name) is CiType type) {
				if (type is CiClass klass) {
					if (klass.Id == CiId.MatchClass && ptrModifier != CiToken.EndOfFile)
						ReportError(expr, "Read-write references to the built-in class Match are not supported");
					CiClassType ptr = CreateClassPtr(klass, ptrModifier);
					if (symbol.Left is CiAggregateInitializer typeArgExprs)
						FillGenericClass(ptr, klass, typeArgExprs);
					else
						ptr.Name = klass.Name; // TODO: needed?
					return ptr;
				}
				ExpectNoPtrModifier(expr, ptrModifier);
				return type;
			}
			return PoisonError(expr, $"Type {symbol.Name} not found");

		case CiCallExpr call:
			// string(), MyClass()
			ExpectNoPtrModifier(expr, ptrModifier);
			if (call.Arguments.Count != 0)
				ReportError(call, "Expected empty parentheses for storage type");
			{
				if (call.Method.Left is CiAggregateInitializer typeArgExprs) {
					CiStorageType storage = new CiStorageType();
					FillGenericClass(storage, this.Program.TryLookup(call.Method.Name), typeArgExprs);
					return storage;
				}
			}
			if (call.Method.Name == "string")
				return this.Program.System.StringStorageType;
			{
				if (this.Program.TryLookup(call.Method.Name) is CiClass klass)
					return new CiStorageType { Class = klass };
			}
			return PoisonError(expr, $"Class {call.Method.Name} not found");

		default:
			return PoisonError(expr, "Invalid type");
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
		CiClassType outerArray = null; // leftmost in source
		CiClassType innerArray = null; // rightmost in source
		while (expr is CiBinaryExpr binary && binary.Op == CiToken.LeftBracket) {
			if (binary.Right != null) {
				ExpectNoPtrModifier(expr, ptrModifier);
				CiExpr lengthExpr = Resolve(binary.Right);
				CiArrayStorageType arrayStorage = new CiArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = outerArray, LengthExpr = lengthExpr, Length = 0 };
				if (Coerce(lengthExpr, this.Program.System.IntType) && (!dynamic || binary.Left.IsIndexing())) {
					if (lengthExpr is CiLiteralLong literal) {
						long length = literal.Value;
						if (length < 0)
							ReportError(expr, "Expected non-negative integer");
						else if (length > int.MaxValue)
							ReportError(expr, "Integer too big");
						else
							arrayStorage.Length = (int) length;
					}
					else
						ReportError(lengthExpr, "Expected constant value");
				}
				outerArray = arrayStorage;
			}
			else {
				CiType elementType = outerArray;
				outerArray = CreateClassPtr(this.Program.System.ArrayPtrClass, ptrModifier);
				outerArray.TypeArg0 = elementType;
			}
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
				return PoisonError(expr, "Range min greater than max");
			baseType = CiRangeType.New(min, max);
		}
		else
			baseType = ToBaseType(expr, ptrModifier);
		baseType.Line = expr.Line;

		if (outerArray == null)
			return baseType;
		innerArray.TypeArg0 = baseType;
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
			konst.Value = PoisonError(konst, $"Circular dependency in value of constant {konst.Name}");
			konst.VisitStatus = CiVisitStatus.Done;
			return;
		case CiVisitStatus.Done:
			return;
		}
		konst.VisitStatus = CiVisitStatus.InProgress;
		if (!(this.CurrentScope is CiEnum))
			ResolveType(konst);
		konst.Value = Resolve(konst.Value);
		if (konst.Value is CiAggregateInitializer coll) {
			if (konst.Type is CiClassType array) {
				CiType elementType = array.GetElementType();
				if (array is CiArrayStorageType arrayStg) {
					if (arrayStg.Length != coll.Items.Count)
						ReportError(konst, $"Declared {arrayStg.Length} elements, initialized {coll.Items.Count}");
				}
				else if (array is CiReadWriteClassType)
					ReportError(konst, "Invalid constant type");
				else
					konst.Type = new CiArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = elementType, Length = coll.Items.Count };
				coll.Type = konst.Type;
				foreach (CiExpr item in coll.Items)
					Coerce(item, elementType);
			}
			else
				ReportError(konst, $"Array initializer for scalar constant {konst.Name}");
		}
		else if (this.CurrentScope is CiEnum && konst.Value.Type is CiRangeType && konst.Value is CiLiteral) {
		}
		else if (konst.Value is CiLiteral || konst.Value.IsConstEnum())
			Coerce(konst.Value, konst.Type);
		else if (konst.Value != this.Poison)
			ReportError(konst.Value, $"Value for constant {konst.Name} is not constant");
		konst.InMethod = this.CurrentMethod;
		konst.VisitStatus = CiVisitStatus.Done;
	}

	public override void VisitEnumValue(CiConst konst, CiConst previous)
	{
		if (konst.Value != null) {
			ResolveConst(konst);
			((CiEnum) konst.Parent).HasExplicitValue = true;
		}
		else
			konst.Value = new CiImplicitEnumValue { Value = previous == null ? 0 : previous.Value.IntValue() + 1 };
	}

	void ResolveConsts(CiContainerType container)
	{
		this.CurrentScope = container;
		switch (container) {
		case CiClass klass:
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiConst konst)
					ResolveConst(konst);
			}
			break;
		case CiEnum enu:
			enu.AcceptValues(this);
			break;
		default:
			throw new NotImplementedException(container.ToString());
		}
	}

	void ResolveTypes(CiClass klass)
	{
		this.CurrentScope = klass;
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			switch (symbol) {
			case CiField field:
				CiType type = ResolveType(field);
				if (field.Value != null) {
					field.Value = Resolve(field.Value);
					if (!field.IsAssignableStorage()) {
						if (type is CiArrayStorageType array)
							type = array.GetElementType();
						Coerce(field.Value, type);
					}
				}
				break;
			case CiMethod method:
				if (method.TypeExpr == this.Program.System.VoidType)
					method.Type = this.Program.System.VoidType;
				else
					ResolveType(method);
				for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
					ResolveType(param);
					if (param.Value != null) {
						param.Value = FoldConst(param.Value);
						Coerce(param.Value, param.Type);
					}
				}
				if (method.CallType == CiCallType.Override || method.CallType == CiCallType.Sealed) {
					if (klass.Parent.TryLookup(method.Name) is CiMethod baseMethod) {
						// TODO: check private
						switch (baseMethod.CallType) {
						case CiCallType.Abstract:
						case CiCallType.Virtual:
						case CiCallType.Override:
							break;
						default:
							ReportError(method, "Base method is not abstract or virtual");
							break;
						}
						// TODO: check parameter and return type
						baseMethod.Calls.Add(method);
					}
					else
						ReportError(method, "No method to override");
				}
				break;
			default:
				break;
			}
		}
	}

	void ResolveCode(CiClass klass)
	{
		if (klass.Constructor != null) {
			this.CurrentScope = klass;
			this.CurrentMethod = klass.Constructor;
			klass.Constructor.Body.AcceptStatement(this);
			this.CurrentMethod = null;
		}
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMethod method) {
				if (method.Name == "ToString" && method.CallType != CiCallType.Static && method.Parameters.Count() == 0)
					method.Id = CiId.ClassToString;
				if (method.Body != null) {
					this.CurrentScope = method.Parameters;
					this.CurrentMethod = method;
					if (!(method.Body is CiScope))
						OpenScope(new CiScope()); // don't add "is Derived d" to parameters
					method.Body.AcceptStatement(this);
					if (method.Type.Id != CiId.VoidType && method.Body.CompletesNormally())
						ReportError(method.Body, "Method can complete without a return value");
					this.CurrentMethod = null;
				}
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
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMethod method
			 && (method.Visibility == CiVisibility.Public || method.Visibility == CiVisibility.Protected))
				SetLive(method);
		}
		if (klass.Constructor != null)
			SetLive(klass.Constructor);
	}

	public CiResolver(CiProgram program, IEnumerable<string> searchDirs)
	{
		this.Program = program;
		this.SearchDirs = searchDirs;
		for (CiSymbol type = program.First; type != null; type = type.Next) {
			if (type is CiClass klass)
				ResolveBase(klass);
		}
		for (CiSymbol type = program.First; type != null; type = type.Next)
			ResolveConsts((CiContainerType) type);
		foreach (CiClass klass in program.Classes)
			ResolveTypes(klass);
		foreach (CiClass klass in program.Classes)
			ResolveCode(klass);
		foreach (CiClass klass in program.Classes)
			SetLive(klass);
	}
}

}
