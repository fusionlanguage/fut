// CiResolver.cs - Ci symbol resolver
//
// Copyright (C) 2011-2023  Piotr Fusik
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

public class CiResolver : CiSema
{
	readonly List<string> SearchDirs;
	readonly HashSet<CiMethod> CurrentPureMethods = new HashSet<CiMethod>();
	readonly Dictionary<CiVar, CiExpr> CurrentPureArguments = new Dictionary<CiVar, CiExpr>();

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

	protected override void VisitVar(CiVar expr)
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

	protected override CiExpr VisitInterpolatedString(CiInterpolatedString expr)
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

	protected override CiExpr VisitSymbolReference(CiSymbolReference expr)
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

	protected override CiExpr VisitPrefixExpr(CiPrefixExpr expr)
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
			return ResolveNew(expr);
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

	CiExpr ResolveIs(CiBinaryExpr expr, CiExpr left, CiExpr right)
	{
		if (!(left.Type is CiClassType leftPtr) || left.Type is CiStorageType)
			return PoisonError(expr, "Left hand side of the 'is' operator must be an object reference");
		CiClass klass;
		switch (right) {
		case CiSymbolReference symbol:
			if (symbol.Symbol is CiClass klass2)
				expr.Right = klass = klass2;
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
			klass = rightPtr.Class;
			break;
		default:
			return PoisonError(expr, "Right hand side of the 'is' operator must be a class name");
		}
		if (klass.IsSameOrBaseOf(leftPtr.Class))
			return PoisonError(expr, $"{leftPtr} is {klass.Name}, the 'is' operator would always return 'true'");
		if (!leftPtr.Class.IsSameOrBaseOf(klass))
			return PoisonError(expr, $"{leftPtr} is not base class of {klass.Name}, the 'is' operator would always return 'false'");
		expr.Left = left;
		expr.Type = this.Program.System.BoolType;
		return expr;
	}

	protected override CiExpr VisitBinaryExpr(CiBinaryExpr expr)
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
				type = this.Program.System.StringStorageType;
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
		case CiToken.Or:
		case CiToken.Xor:
			type = BitwiseOp(left, expr.Op, right);
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
			return ResolveIs(expr, left, right);

		case CiToken.Range:
			return PoisonError(expr, "Range within an expression");
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		if (type is CiRangeType range && range.Min == range.Max)
			return ToLiteralLong(expr, range.Min);
		return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = type };
	}

	protected override CiExpr VisitCallExpr(CiCallExpr expr)
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
			return PoisonError(arguments[i], $"Too many arguments for '{method.Name}'");

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

	public override void VisitConst(CiConst statement)
	{
		ResolveConst(statement);
		this.CurrentScope.Add(statement);
		if (statement.Type is CiArrayStorageType)
			((CiClass) this.CurrentScope.GetContainer()).ConstArrays.Add(statement);
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

	public CiResolver(CiProgram program, List<string> searchDirs)
	{
		this.Program = program;
		this.SearchDirs = searchDirs;
		for (CiSymbol type = program.First; type != null; type = type.Next) {
			if (type is CiClass klass)
				ResolveBase(klass);
		}
		foreach (CiClass klass in program.Classes)
			CheckBaseCycle(klass);
		for (CiSymbol type = program.First; type != null; type = type.Next)
			ResolveConsts((CiContainerType) type);
		foreach (CiClass klass in program.Classes)
			ResolveTypes(klass);
		foreach (CiClass klass in program.Classes)
			ResolveCode(klass);
		foreach (CiClass klass in program.Classes)
			MarkClassLive(klass);
	}
}

}
