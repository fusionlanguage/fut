// CiTree.cs - Ci abstract syntax tree
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
using System.Globalization;
using System.Linq;
using System.Text;

namespace Foxoft.Ci
{

public abstract class CiNamedValue : CiSymbol
{
	internal CiExpr TypeExpr;
	internal CiExpr Value;
	public bool IsAssignableStorage() => this.Type is CiStorageType && !(this.Type is CiArrayStorageType) && this.Value is CiLiteralNull;
}

public class CiMember : CiNamedValue
{
	internal CiVisibility Visibility;
	protected CiMember()
	{
	}
	public static CiMember New(CiType type, CiId id, string name) => new CiMember { Visibility = CiVisibility.Public, Type = type, Id = id, Name = name };
	public virtual bool IsStatic() => throw new NotImplementedException(GetType().Name);
}

public class CiVar : CiNamedValue
{
	internal bool IsAssigned = false;
	public static CiVar New(CiType type, string name, CiExpr defaultValue = null) => new CiVar { Type = type, Name = name, Value = defaultValue };
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitVar(this);
		return this;
	}
	public CiVar NextParameter() => (CiVar) this.Next;
}

public class CiConst : CiMember
{
	internal CiMethodBase InMethod;
	internal CiVisitStatus VisitStatus;
	public override void AcceptStatement(CiVisitor visitor) { visitor.VisitConst(this); }
	public override bool IsStatic() => true;
}

public class CiLiteralChar : CiLiteralLong
{
	CiLiteralChar()
	{
	}
	public static CiLiteralChar New(int value, int line) => new CiLiteralChar { Line = line, Type = new CiRangeType(value, value), Value = value };
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralChar((int) this.Value);
		return this;
	}
	public override string ToString()
	{
		switch (this.Value) {
		case '\a': return "'\\a'";
		case '\b': return "'\\b'";
		case '\f': return "'\\f'";
		case '\n': return "'\\n'";
		case '\r': return "'\\r'";
		case '\t': return "'\\t'";
		case '\v': return "'\\v'";
		case '\\': return "'\\\\'";
		case '\'': return "'\\''";
		default: return $"'{(char) this.Value}'";
		}
	}
}

public class CiLiteralDouble : CiLiteral
{
	internal double Value;
	public override bool IsDefaultValue() => BitConverter.DoubleToInt64Bits(this.Value) == 0; // rule out -0.0
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralDouble(this.Value);
		return this;
	}
	public override string GetLiteralString() => this.Value.ToString(CultureInfo.InvariantCulture);
	public override string ToString() => GetLiteralString();
}

public class CiInterpolatedPart
{
	internal string Prefix;
	internal CiExpr Argument;
	internal CiExpr WidthExpr = null;
	internal int Width;
	internal char Format = ' ';
	internal int Precision = -1;
	public CiInterpolatedPart(string prefix, CiExpr arg)
	{
		this.Prefix = prefix;
		this.Argument = arg;
	}
}

public class CiInterpolatedString : CiExpr
{
	internal readonly List<CiInterpolatedPart> Parts = new List<CiInterpolatedPart>();
	internal string Suffix;
	public CiInterpolatedString()
	{
		this.Type = CiSystem.StringStorageType;
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitInterpolatedString(this, parent);
	public override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append("$\"");
		foreach (CiInterpolatedPart part in this.Parts) {
			sb.Append(part.Prefix.Replace("{", "{{"));
			sb.Append('{');
			sb.Append(part.Argument);
			if (part.WidthExpr != null) {
				sb.Append(',');
				sb.Append(part.WidthExpr);
			}
			if (part.Format != ' ') {
				sb.Append(':');
				sb.Append(part.Format);
				if (part.Precision >= 0)
					sb.Append(part.Precision);
			}
			sb.Append('}');
		}
		sb.Append(this.Suffix.Replace("{", "{{"));
		sb.Append('"');
		return sb.ToString();
	}
}

public class CiSymbolReference : CiExpr
{
	internal CiExpr Left;
	internal string Name;
	internal CiSymbol Symbol;
	public override bool IsConstEnum() => this.Symbol.Parent is CiEnum;
	public override int IntValue() => ((CiConst) this.Symbol).Value.IntValue();
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitSymbolReference(this, parent);
	public override bool IsReferenceTo(CiSymbol symbol) => this.Symbol == symbol;
	public override string ToString() => this.Left != null ? $"{this.Left}.{this.Name}" : this.Name;
}

public class CiPrefixExpr : CiUnaryExpr
{
	public override bool IsConstEnum() => this.Type is CiEnumFlags && this.Inner.IsConstEnum(); // && this.Op == CiToken.Tilde
	public override int IntValue()
	{
		Trace.Assert(this.Op == CiToken.Tilde);
		return ~this.Inner.IntValue();
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitPrefixExpr(this, parent);
}

public class CiBinaryExpr : CiExpr
{
	internal CiExpr Left;
	internal CiToken Op;
	internal CiExpr Right;
	public override bool IsIndexing() => this.Op == CiToken.LeftBracket;
	public override bool IsConstEnum()
	{
		switch (this.Op) {
		case CiToken.And:
		case CiToken.Or:
		case CiToken.Xor:
			return this.Type is CiEnumFlags && this.Left.IsConstEnum() && this.Right.IsConstEnum();
		default:
			return false;
		}
	}
	public override int IntValue()
	{
		return this.Op switch {
				CiToken.And => this.Left.IntValue() & this.Right.IntValue(),
				CiToken.Or => this.Left.IntValue() | this.Right.IntValue(),
				CiToken.Xor => this.Left.IntValue() ^ this.Right.IntValue(),
				_ => base.IntValue() // throw
			};
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitBinaryExpr(this, parent);
	public bool IsAssign()
	{
		switch (this.Op) {
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
			return true;
		default:
			return false;
		}
	}

	public string GetOpString()
	{
		switch (this.Op) {
		case CiToken.Plus:
			return "+";
		case CiToken.Minus:
			return "-";
		case CiToken.Asterisk:
			return "*";
		case CiToken.Slash:
			return "/";
		case CiToken.Mod:
			return "%";
		case CiToken.ShiftLeft:
			return "<<";
		case CiToken.ShiftRight:
			return ">>";
		case CiToken.Less:
			return "<";
		case CiToken.LessOrEqual:
			return "<=";
		case CiToken.Greater:
			return ">";
		case CiToken.GreaterOrEqual:
			return ">=";
		case CiToken.Equal:
			return "==";
		case CiToken.NotEqual:
			return "!=";
		case CiToken.And:
			return "&";
		case CiToken.Or:
			return "|";
		case CiToken.Xor:
			return "^";
		case CiToken.CondAnd:
			return "&&";
		case CiToken.CondOr:
			return "||";
		case CiToken.Assign:
			return "=";
		case CiToken.AddAssign:
			return "+=";
		case CiToken.SubAssign:
			return "-=";
		case CiToken.MulAssign:
			return "*=";
		case CiToken.DivAssign:
			return "/=";
		case CiToken.ModAssign:
			return "%=";
		case CiToken.ShiftLeftAssign:
			return "<<=";
		case CiToken.ShiftRightAssign:
			return ">>=";
		case CiToken.AndAssign:
			return "&=";
		case CiToken.OrAssign:
			return "|=";
		case CiToken.XorAssign:
			return "^=";
		default:
			throw new ArgumentException(this.Op.ToString());
		}
	}

	public static CiType PromoteIntegerTypes(CiType left, CiType right)
	{
		return left == CiSystem.LongType || right == CiSystem.LongType ? CiSystem.LongType : CiSystem.IntType;
	}

	public static CiType PromoteFloatingTypes(CiType left, CiType right)
	{
		if (left == CiSystem.DoubleType || right == CiSystem.DoubleType)
			return CiSystem.DoubleType;
		if (left == CiSystem.FloatType || right == CiSystem.FloatType
		 || left == CiSystem.FloatIntType || right == CiSystem.FloatIntType)
			return CiSystem.FloatType;
		return null;
	}

	public static CiType PromoteNumericTypes(CiType left, CiType right) => PromoteFloatingTypes(left, right) ?? PromoteIntegerTypes(left, right);

	public override string ToString() => this.Op == CiToken.LeftBracket ? $"{this.Left}[{this.Right}]" : $"({this.Left} {GetOpString()} {this.Right})";
}

public class CiForeach : CiLoop
{
	internal CiExpr Collection;
	public override void AcceptStatement(CiVisitor visitor) { visitor.VisitForeach(this); }
	public CiVar GetVar() => this.FirstParameter();
	public CiVar GetValueVar() => this.FirstParameter().NextParameter();
}

public class CiSwitch : CiCondCompletionStatement
{
	internal CiExpr Value;
	internal readonly List<CiCase> Cases = new List<CiCase>();
	internal readonly List<CiStatement> DefaultBody = new List<CiStatement>();
	public override void AcceptStatement(CiVisitor visitor) { visitor.VisitSwitch(this); }

	public static int LengthWithoutTrailingBreak(List<CiStatement> body)
	{
		int length = body.Count;
		if (length > 0 && body[length - 1] is CiBreak)
			length--;
		return length;
	}

	public bool HasDefault() => LengthWithoutTrailingBreak(this.DefaultBody) > 0;

	static bool HasBreak(CiStatement statement)
	{
		switch (statement) {
		case CiBreak _:
			return true;
		case CiIf ifStatement:
			return HasBreak(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasBreak(ifStatement.OnFalse));
		case CiBlock block:
			return block.Statements.Any(HasBreak);
		default:
			return false;
		}
	}

	public static bool HasEarlyBreak(List<CiStatement> body)
	{
		int length = LengthWithoutTrailingBreak(body);
		for (int i = 0; i < length; i++) {
			if (HasBreak(body[i]))
				return true;
		}
		return false;
	}

	static bool ListHasContinue(List<CiStatement> statements) => statements.Any(HasContinue);

	static bool HasContinue(CiStatement statement)
	{
		switch (statement) {
		case CiContinue _:
			return true;
		case CiIf ifStatement:
			return HasContinue(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasContinue(ifStatement.OnFalse));
		case CiSwitch switchStatement:
			return switchStatement.Cases.Any(kase => ListHasContinue(kase.Body)) || ListHasContinue(switchStatement.DefaultBody);
		case CiBlock block:
			return ListHasContinue(block.Statements);
		default:
			return false;
		}
	}

	public static bool HasEarlyBreakAndContinue(List<CiStatement> body) => HasEarlyBreak(body) && ListHasContinue(body);
}

public class CiField : CiMember
{
	public override bool IsStatic() => false;
}

public class CiMethodBase : CiMember
{
	internal bool Throws;
	internal CiStatement Body;
	internal bool IsLive = false;
	public readonly HashSet<CiMethod> Calls = new HashSet<CiMethod>();
}

public class CiMethod : CiMethodBase
{
	internal CiCallType CallType;
	internal bool IsMutator;
	internal readonly CiParameters Parameters = new CiParameters();
	public static CiMethod New(CiVisibility visibility, CiType type, CiId id, string name, CiVar param0 = null, CiVar param1 = null, CiVar param2 = null, CiVar param3 = null)
	{
		CiMethod result = new CiMethod { Visibility = visibility, CallType = CiCallType.Normal, Type = type, Id = id, Name = name };
		if (param0 != null) {
			result.Parameters.Add(param0);
			if (param1 != null) {
				result.Parameters.Add(param1);
				if (param2 != null) {
					result.Parameters.Add(param2);
					if (param3 != null)
						result.Parameters.Add(param3);
				}
			}
		}
		return result;
	}
	public static CiMethod NewStatic(CiType type, CiId id, string name, CiVar param0, CiVar param1 = null, CiVar param2 = null)
	{
		CiMethod result = New(CiVisibility.Public, type, id, name, param0, param1, param2);
		result.CallType = CiCallType.Static;
		return result;
	}
	public static CiMethod NewMutator(CiVisibility visibility, CiType type, CiId id, string name, CiVar param0 = null, CiVar param1 = null, CiVar param2 = null)
	{
		CiMethod result = New(visibility, type, id, name, param0, param1, param2);
		result.IsMutator = true;
		return result;
	}
	public override bool IsStatic() => this.CallType == CiCallType.Static;
	public bool IsAbstractOrVirtual() => this.CallType == CiCallType.Abstract || this.CallType == CiCallType.Virtual;
	public CiMethod GetDeclaringMethod()
	{
		CiMethod method = this;
		while (method.CallType == CiCallType.Override)
			method = (CiMethod) method.Parent.Parent.TryLookup(method.Name);
		return method;
	}
}

public class CiMethodGroup : CiMember
{
	internal readonly CiMethod[] Methods = new CiMethod[2];
	CiMethodGroup()
	{
	}
	public static CiMethodGroup New(CiMethod method0, CiMethod method1)
	{
		CiMethodGroup result = new CiMethodGroup { Visibility = method0.Visibility, Name = method0.Name };
		result.Methods[0] = method0;
		result.Methods[1] = method1;
		return result;
	}
}

public class CiEnum : CiContainerType
{
	internal bool HasExplicitValue = false;
	public void AcceptValues(CiVisitor visitor)
	{
		CiConst previous = null;
		for (CiConst konst = (CiConst) this.First; konst != null; konst = (CiConst) konst.Next) {
			visitor.VisitEnumValue(konst, previous);
			previous = konst;
		}
	}
}

public class CiEnumFlags : CiEnum
{
}

public class CiClass : CiContainerType
{
	internal CiCallType CallType;
	internal int TypeParameterCount = 0;
	internal string BaseClassName;
	internal CiMethodBase Constructor;
	internal readonly List<CiConst> ConstArrays = new List<CiConst>();
	internal CiVisitStatus VisitStatus;
	public override string ToString() => this.Name + "()";
	public override CiType GetPtrOrSelf() => new CiReadWriteClassType { Class = this };
	public bool AddsVirtualMethods()
	{
		for (CiSymbol symbol = this.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMethod method && method.IsAbstractOrVirtual())
				return true;
		}
		return false;
	}

	public CiClass()
	{
		Add(CiVar.New(GetPtrOrSelf(), "this")); // shadows "this" in base class
	}

	public static CiClass New(CiCallType callType, CiId id, string name, int typeParameterCount = 0)
		=> new CiClass { CallType = callType, Id = id, Name = name, TypeParameterCount = typeParameterCount };

	public bool IsSameOrBaseOf(CiClass derived)
	{
		while (derived != this) {
			derived = derived.Parent as CiClass;
			if (derived == null)
				return false;
		}
		return true;
	}
}

public class CiIntegerType : CiNumericType
{
	public override bool IsAssignableFrom(CiType right) => right is CiIntegerType || right == CiSystem.FloatIntType;
}

public class CiRangeType : CiIntegerType
{
	internal readonly int Min;
	internal readonly int Max;

	public CiRangeType(int min, int max)
	{
		if (min > max)
			throw new ArgumentOutOfRangeException();
		this.Min = min;
		this.Max = max;
	}

	public CiRangeType(int a, int b, int c, int d)
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
		this.Min = a <= c ? a : c;
		this.Max = b >= d ? b : d;
	}

	public override string ToString() => this.Min == this.Max ? this.Min.ToString() : $"({this.Min} .. {this.Max})";

	public CiRangeType Union(CiRangeType that)
	{
		if (that == null)
			return this;
		if (that.Min < this.Min) {
			if (that.Max >= this.Max)
				return that;
			return new CiRangeType(that.Min, this.Max);
		}
		if (that.Max > this.Max)
			return new CiRangeType(this.Min, that.Max);
		return this;
	}

	public override bool IsAssignableFrom(CiType right)
	{
		switch (right) {
		case CiRangeType range:
			return this.Min <= range.Max && this.Max >= range.Min;
		case CiIntegerType _:
			return true;
		default:
			return right == CiSystem.FloatIntType;
		}
	}

	public override bool EqualsType(CiType right) => right is CiRangeType that && this.Min == that.Min && this.Max == that.Max;

	public static int GetMask(int v)
	{
		// http://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
		v |= v >> 1;
		v |= v >> 2;
		v |= v >> 4;
		v |= v >> 8;
		v |= v >> 16;
		return v;
	}

	public int GetVariableBits() => GetMask(this.Min ^ this.Max);

	public void SplitBySign(out CiRangeType negative, out CiRangeType positive)
	{
		if (this.Min >= 0) {
			negative = null;
			positive = this;
		}
		else if (this.Max < 0) {
			negative = this;
			positive = null;
		}
		else {
			negative = new CiRangeType(this.Min, -1);
			positive = new CiRangeType(0, this.Max);
		}
	}
}

public class CiClassType : CiType
{
	internal CiClass Class;
	internal CiType TypeArg0;
	internal CiType TypeArg1;
	public CiType GetElementType() => this.TypeArg0;
	public CiType GetKeyType() => this.TypeArg0;
	public CiType GetValueType() => this.TypeArg1;
	public override bool IsNullable() => true;
	public override bool IsArray() => this.Class.Id == CiId.ArrayPtrClass;
	public override CiType GetBaseType() => IsArray() ? GetElementType().GetBaseType() : this;

	public CiType EvalType(CiType type)
	{
		if (type == CiSystem.TypeParam0)
			return this.TypeArg0;
		if (type == CiSystem.TypeParam0NotFinal)
			return this.TypeArg0.IsFinal() ? null : this.TypeArg0;
		if (type is CiReadWriteClassType array && array.IsArray() && array.GetElementType() == CiSystem.TypeParam0)
			return new CiReadWriteClassType { Class = CiSystem.ArrayPtrClass, TypeArg0 = this.TypeArg0 };
		return type;
	}

	public override CiSymbol TryLookup(string name) => this.Class.TryLookup(name);

	protected bool EqualTypeArguments(CiClassType right)
	{
		switch (this.Class.TypeParameterCount) {
		case 0: return true;
		case 1: return this.TypeArg0.EqualsType(right.TypeArg0);
		case 2: return this.TypeArg0.EqualsType(right.TypeArg0) && this.TypeArg1.EqualsType(right.TypeArg1);
		default: throw new NotImplementedException();
		}
	}

	protected bool IsAssignableFromClass(CiClassType right) => this.Class.IsSameOrBaseOf(right.Class) && EqualTypeArguments(right);

	public override bool IsAssignableFrom(CiType right)
	{
		return right == CiSystem.NullType
			|| (right is CiClassType rightClass && IsAssignableFromClass(rightClass));
	}

	public override bool EqualsType(CiType right)
		=> right is CiClassType that // TODO: exact match
			&& this.Class == that.Class && EqualTypeArguments(that);

	public override string GetArraySuffix() => IsArray() ? "[]" : "";
	public virtual string GetClassSuffix() => "";

	public override string ToString()
	{
		if (IsArray())
			return GetElementType().GetBaseType() + GetArraySuffix() + GetElementType().GetArraySuffix();
		switch (this.Class.TypeParameterCount) {
		case 0: return this.Class.Name + GetClassSuffix();
		case 1: return $"{this.Class.Name}<{this.TypeArg0}>{GetClassSuffix()}";
		case 2: return $"{this.Class.Name}<{this.TypeArg0}, {this.TypeArg1}>{GetClassSuffix()}";
		default: throw new NotImplementedException();
		}
	}
}

public class CiReadWriteClassType : CiClassType
{
	public override bool IsAssignableFrom(CiType right)
	{
		return right == CiSystem.NullType
			|| (right is CiReadWriteClassType rightClass && IsAssignableFromClass(rightClass));
	}

	public override string GetArraySuffix() => IsArray() ? "[]!" : "";
	public override string GetClassSuffix() => "!";
}

public class CiStorageType : CiReadWriteClassType
{
	public override bool IsFinal() => this.Class.Id != CiId.MatchClass;
	public override bool IsNullable() => false;
	public override bool IsAssignableFrom(CiType right) => right is CiStorageType rightClass && this.Class == rightClass.Class && EqualTypeArguments(rightClass);
	public override CiType GetPtrOrSelf() => new CiReadWriteClassType { Class = this.Class, TypeArg0 = this.TypeArg0, TypeArg1 = this.TypeArg1 };
	public override string GetClassSuffix() => "()";
}

public class CiDynamicPtrType : CiReadWriteClassType
{
	public override bool IsAssignableFrom(CiType right)
	{
		return right == CiSystem.NullType
			|| (right is CiDynamicPtrType rightClass && IsAssignableFromClass(rightClass));
	}
	public override CiType GetPtrOrSelf() => new CiReadWriteClassType { Class = this.Class, TypeArg0 = this.TypeArg0 };

	public override string GetArraySuffix() => IsArray() ? "[]#" : "";
	public override string GetClassSuffix() => "#";
}

public class CiArrayStorageType : CiStorageType
{
	internal CiExpr LengthExpr;
	internal int Length;
	internal bool PtrTaken = false;

	public CiArrayStorageType()
	{
		this.Class = CiSystem.ArrayStorageClass;
	}

	public override string ToString() => GetBaseType() + GetArraySuffix() + GetElementType().GetArraySuffix();
	public override CiType GetBaseType() => GetElementType().GetBaseType();
	public override bool IsArray() => true;
	public override string GetArraySuffix() => $"[{this.Length}]";
	public override bool EqualsType(CiType right) => right is CiArrayStorageType that && GetElementType().EqualsType(that.GetElementType()) && this.Length == that.Length;
	public override CiType GetStorageType() => GetElementType().GetStorageType();
	public override CiType GetPtrOrSelf() => new CiReadWriteClassType { Class = CiSystem.ArrayPtrClass, TypeArg0 = GetElementType() };
}

public class CiStringType : CiClassType
{
	public CiStringType()
	{
		this.Class = CiSystem.StringClass;
	}
}

public class CiStringStorageType : CiStringType
{
	public override bool IsNullable() => false;
	public override CiType GetPtrOrSelf() => CiSystem.StringPtrType;
	public override bool IsAssignableFrom(CiType right) => right is CiStringType;
	public override string GetClassSuffix() => "()";
}

public class CiPrintableType : CiType
{
	public override bool IsAssignableFrom(CiType right) => right is CiStringType || right is CiNumericType;
}

public class CiSystem : CiScope
{
	public static readonly CiType VoidType = new CiType { Name = "void" };
	public static readonly CiType NullType = new CiType { Name = "null" };
	public static readonly CiType TypeParam0 = new CiType { Name = "T" };
	public static readonly CiType TypeParam0NotFinal = new CiType { Name = "T" };
	public static readonly CiType TypeParam0Predicate = new CiType { Name = "Predicate<T>" };
	public static readonly CiIntegerType IntType = new CiIntegerType { Name = "int" };
	public static readonly CiRangeType UIntType = new CiRangeType(0, int.MaxValue) { Name = "uint" };
	public static readonly CiIntegerType LongType = new CiIntegerType { Name = "long" };
	public static readonly CiRangeType ByteType = new CiRangeType(0, 0xff) { Name = "byte" };
	public static readonly CiRangeType Minus1Type = new CiRangeType(-1, int.MaxValue);
	public static readonly CiFloatingType FloatType = new CiFloatingType { Name = "float" };
	public static readonly CiFloatingType DoubleType = new CiFloatingType { Name = "double" };
	public static readonly CiFloatingType FloatIntType = new CiFloatingType { Name = "float" };
	public static readonly CiRangeType CharType = new CiRangeType(-0x80, 0xffff);
	public static readonly CiEnum BoolType = new CiEnum { Name = "bool" };
	public static readonly CiClass StringClass = CiClass.New(CiCallType.Normal, CiId.StringClass, "string");
	public static readonly CiStringType StringPtrType = new CiStringType { Name = "string" };
	public static readonly CiStringStorageType StringStorageType = new CiStringStorageType();
	public static readonly CiType PrintableType = new CiPrintableType { Name = "printable" };
	public static readonly CiClass ArrayPtrClass = CiClass.New(CiCallType.Normal, CiId.ArrayPtrClass, "ArrayPtr", 1);
	public static readonly CiClass ArrayStorageClass = CiClass.New(CiCallType.Normal, CiId.ArrayStorageClass, "ArrayStorage", 1);
	public static readonly CiClass ConsoleBase = CiClass.New(CiCallType.Static, CiId.None, "ConsoleBase");
	public static readonly CiMember ConsoleError = CiMember.New(ConsoleBase, CiId.ConsoleError, "Error");
	static readonly CiClass LockClass = CiClass.New(CiCallType.Sealed, CiId.LockClass, "Lock");
	public static readonly CiReadWriteClassType LockPtrType = new CiReadWriteClassType { Class = LockClass };
	public static readonly CiSymbol BasePtr = CiVar.New(null, "base");

	public static CiLiteralLong NewLiteralLong(long value, int line = 0)
	{
		CiType type = value >= int.MinValue && value <= int.MaxValue ? new CiRangeType((int) value, (int) value) : LongType;
		return new CiLiteralLong { Line = line, Type = type, Value = value };
	}

	public static CiLiteralString NewLiteralString(string value, int line = 0) => new CiLiteralString { Line = line, Type = StringPtrType, Value = value };

	CiClass AddCollection(CiId id, string name, int typeParameterCount, CiId clearId, CiId countId)
	{
		CiClass result = CiClass.New(CiCallType.Normal, id, name, typeParameterCount);
		result.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, clearId, "Clear"));
		result.Add(CiMember.New(UIntType, countId, "Count"));
		Add(result);
		return result;
	}

	void AddDictionary(CiId id, string name, CiId clearId, CiId containsKeyId, CiId countId, CiId removeId)
	{
		CiClass dict = AddCollection(id, name, 2, clearId, countId);
		dict.Add(CiMethod.NewMutator(CiVisibility.FinalValueType, VoidType, CiId.DictionaryAdd, "Add", CiVar.New(TypeParam0, "key")));
		dict.Add(CiMethod.New(CiVisibility.Public, BoolType, containsKeyId, "ContainsKey", CiVar.New(TypeParam0, "key")));
		dict.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, removeId, "Remove", CiVar.New(TypeParam0, "key")));
	}

	static void AddEnumValue(CiEnum enu, CiConst value)
	{
		value.Type = enu;
		enu.Add(value);
	}

	static CiConst NewConstInt(string name, int value)
	{
		CiConst result = new CiConst { Visibility = CiVisibility.Public, Name = name, Value = NewLiteralLong(value), VisitStatus = CiVisitStatus.Done };
		result.Type = result.Value.Type;
		return result;
	}

	static CiConst NewConstDouble(string name, double value)
		=> new CiConst { Visibility = CiVisibility.Public, Name = name, Value = new CiLiteralDouble { Value = value, Type = DoubleType }, Type = DoubleType, VisitStatus = CiVisitStatus.Done };

	CiSystem()
	{
		Add(IntType);
		Add(UIntType);
		Add(LongType);
		Add(ByteType);
		Add(new CiRangeType(-0x8000, 0x7fff) { Name = "short" });
		Add(new CiRangeType(0, 0xffff) { Name = "ushort" });
		Add(FloatType);
		Add(DoubleType);
		Add(BoolType);
		StringClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.StringContains, "Contains", CiVar.New(StringPtrType, "value")));
		StringClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.StringEndsWith, "EndsWith", CiVar.New(StringPtrType, "value")));
		StringClass.Add(CiMethod.New(CiVisibility.Public, Minus1Type, CiId.StringIndexOf, "IndexOf", CiVar.New(StringPtrType, "value")));
		StringClass.Add(CiMethod.New(CiVisibility.Public, Minus1Type, CiId.StringLastIndexOf, "LastIndexOf", CiVar.New(StringPtrType, "value")));
		StringClass.Add(CiMember.New(UIntType, CiId.StringLength, "Length"));
		StringClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.StringStartsWith, "StartsWith", CiVar.New(StringPtrType, "value")));
		StringClass.Add(CiMethod.New(CiVisibility.Public, StringStorageType, CiId.StringSubstring, "Substring", CiVar.New(IntType, "offset"), CiVar.New(IntType, "length", NewLiteralLong(-1)))); // TODO: UIntType
		Add(StringPtrType);
		CiMethod arrayBinarySearchPart = CiMethod.New(CiVisibility.NumericElementType, IntType, CiId.ArrayBinarySearchPart, "BinarySearch",
			CiVar.New(TypeParam0, "value"), CiVar.New(IntType, "startIndex"), CiVar.New(IntType, "count"));
		ArrayPtrClass.Add(arrayBinarySearchPart);
		ArrayPtrClass.Add(CiMethod.New(CiVisibility.Public, VoidType, CiId.ArrayCopyTo, "CopyTo", CiVar.New(IntType, "sourceIndex"),
			CiVar.New(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = TypeParam0 }, "destinationArray"), CiVar.New(IntType, "destinationIndex"), CiVar.New(IntType, "count")));
		CiMethod arrayFillPart = CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ArrayFillPart, "Fill",
			CiVar.New(TypeParam0, "value"), CiVar.New(IntType, "startIndex"), CiVar.New(IntType, "count"));
		ArrayPtrClass.Add(arrayFillPart);
		CiMethod arraySortPart = CiMethod.NewMutator(CiVisibility.NumericElementType, VoidType, CiId.ArraySortPart, "Sort", CiVar.New(IntType, "startIndex"), CiVar.New(IntType, "count"));
		ArrayPtrClass.Add(arraySortPart);
		ArrayStorageClass.Parent = ArrayPtrClass;
		ArrayStorageClass.Add(CiMethodGroup.New(CiMethod.New(CiVisibility.NumericElementType, IntType, CiId.ArrayBinarySearchAll, "BinarySearch", CiVar.New(TypeParam0, "value")),
			arrayBinarySearchPart));
		ArrayStorageClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ArrayFillAll, "Fill", CiVar.New(TypeParam0, "value")),
			arrayFillPart));
		ArrayStorageClass.Add(CiMember.New(UIntType, CiId.ArrayLength, "Length"));
		ArrayStorageClass.Add(CiMethodGroup.New(
			CiMethod.NewMutator(CiVisibility.NumericElementType, VoidType, CiId.ArraySortAll, "Sort"),
			arraySortPart));

		CiClass listClass = AddCollection(CiId.ListClass, "List", 1, CiId.ListClear, CiId.ListCount);
		listClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ListAdd, "Add", CiVar.New(TypeParam0NotFinal, "value")));
		listClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.ListAny, "Any", CiVar.New(TypeParam0Predicate, "predicate")));
		listClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.ListContains, "Contains", CiVar.New(TypeParam0, "value")));
		listClass.Add(CiMethod.New(CiVisibility.Public, VoidType, CiId.ListCopyTo, "CopyTo", CiVar.New(IntType, "sourceIndex"),
			CiVar.New(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = TypeParam0 }, "destinationArray"), CiVar.New(IntType, "destinationIndex"), CiVar.New(IntType, "count")));
		listClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ListInsert, "Insert", CiVar.New(UIntType, "index"), CiVar.New(TypeParam0NotFinal, "value")));
		listClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ListRemoveAt, "RemoveAt", CiVar.New(IntType, "index")));
		listClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ListRemoveRange, "RemoveRange", CiVar.New(IntType, "index"), CiVar.New(IntType, "count")));
		listClass.Add(CiMethodGroup.New(
			CiMethod.NewMutator(CiVisibility.NumericElementType, VoidType, CiId.ListSortAll, "Sort"),
			CiMethod.NewMutator(CiVisibility.NumericElementType, VoidType, CiId.ListSortPart, "Sort", CiVar.New(IntType, "startIndex"), CiVar.New(IntType, "count"))));
		CiClass queueClass = AddCollection(CiId.QueueClass, "Queue", 1, CiId.QueueClear, CiId.QueueCount);
		queueClass.Add(CiMethod.NewMutator(CiVisibility.Public, TypeParam0, CiId.QueueDequeue, "Dequeue"));
		queueClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.QueueEnqueue, "Enqueue", CiVar.New(TypeParam0, "value")));
		queueClass.Add(CiMethod.New(CiVisibility.Public, TypeParam0, CiId.QueuePeek, "Peek"));
		CiClass stackClass = AddCollection(CiId.StackClass, "Stack", 1, CiId.StackClear, CiId.StackCount);
		stackClass.Add(CiMethod.New(CiVisibility.Public, TypeParam0, CiId.StackPeek, "Peek"));
		stackClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.StackPush, "Push", CiVar.New(TypeParam0, "value")));
		stackClass.Add(CiMethod.NewMutator(CiVisibility.Public, TypeParam0, CiId.StackPop, "Pop"));
		CiClass hashSetClass = AddCollection(CiId.HashSetClass, "HashSet", 1, CiId.HashSetClear, CiId.HashSetCount);
		hashSetClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.HashSetAdd, "Add", CiVar.New(TypeParam0, "value")));
		hashSetClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.HashSetContains, "Contains", CiVar.New(TypeParam0, "value")));
		hashSetClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.HashSetRemove, "Remove", CiVar.New(TypeParam0, "value")));
		AddDictionary(CiId.DictionaryClass, "Dictionary", CiId.DictionaryClear, CiId.DictionaryContainsKey, CiId.DictionaryCount, CiId.DictionaryRemove);
		AddDictionary(CiId.SortedDictionaryClass, "SortedDictionary", CiId.SortedDictionaryClear, CiId.SortedDictionaryContainsKey, CiId.SortedDictionaryCount, CiId.SortedDictionaryRemove);
		AddDictionary(CiId.OrderedDictionaryClass, "OrderedDictionary", CiId.OrderedDictionaryClear, CiId.OrderedDictionaryContainsKey, CiId.OrderedDictionaryCount, CiId.OrderedDictionaryRemove);

		ConsoleBase.Add(CiMethod.NewStatic(VoidType, CiId.ConsoleWrite, "Write", CiVar.New(PrintableType, "value")));
		ConsoleBase.Add(CiMethod.NewStatic(VoidType, CiId.ConsoleWriteLine, "WriteLine", CiVar.New(PrintableType, "value", NewLiteralString(""))));
		CiClass consoleClass = CiClass.New(CiCallType.Static, CiId.None, "Console");
		consoleClass.Add(ConsoleError);
		Add(consoleClass);
		consoleClass.Parent = ConsoleBase;
		CiClass utf8EncodingClass = CiClass.New(CiCallType.Sealed, CiId.None, "UTF8Encoding");
		utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, IntType, CiId.UTF8GetByteCount, "GetByteCount", CiVar.New(StringPtrType, "str")));
		utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, VoidType, CiId.UTF8GetBytes, "GetBytes",
			CiVar.New(StringPtrType, "str"), CiVar.New(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = ByteType }, "bytes"), CiVar.New(IntType, "byteIndex")));
		utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, StringStorageType, CiId.UTF8GetString, "GetString",
			CiVar.New(new CiClassType { Class = ArrayPtrClass, TypeArg0 = ByteType }, "bytes"), CiVar.New(IntType, "offset"), CiVar.New(IntType, "length"))); // TODO: UIntType
		CiClass encodingClass = CiClass.New(CiCallType.Static, CiId.None, "Encoding");
		encodingClass.Add(CiMember.New(utf8EncodingClass, CiId.None, "UTF8"));
		Add(encodingClass);
		CiClass environmentClass = CiClass.New(CiCallType.Static, CiId.None, "Environment");
		environmentClass.Add(CiMethod.NewStatic(StringPtrType, CiId.EnvironmentGetEnvironmentVariable, "GetEnvironmentVariable", CiVar.New(StringPtrType, "name")));
		Add(environmentClass);
		CiEnum regexOptionsEnum = new CiEnumFlags { Name = "RegexOptions" };
		CiConst regexOptionsNone = NewConstInt("None", 0);
		AddEnumValue(regexOptionsEnum, regexOptionsNone);
		AddEnumValue(regexOptionsEnum, NewConstInt("IgnoreCase", 1));
		AddEnumValue(regexOptionsEnum, NewConstInt("Multiline", 2));
		AddEnumValue(regexOptionsEnum, NewConstInt("Singleline", 16));
		Add(regexOptionsEnum);
		CiClass regexClass = CiClass.New(CiCallType.Sealed, CiId.RegexClass, "Regex");
		regexClass.Add(CiMethod.NewStatic(StringStorageType, CiId.RegexEscape, "Escape", CiVar.New(StringPtrType, "str")));
		regexClass.Add(CiMethodGroup.New(
				CiMethod.NewStatic(BoolType, CiId.RegexIsMatchStr, "IsMatch", CiVar.New(StringPtrType, "input"), CiVar.New(StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)),
				CiMethod.New(CiVisibility.Public, BoolType, CiId.RegexIsMatchRegex, "IsMatch", CiVar.New(StringPtrType, "input"))));
		regexClass.Add(CiMethod.NewStatic(new CiDynamicPtrType { Class = regexClass }, CiId.RegexCompile, "Compile", CiVar.New(StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)));
		Add(regexClass);
		CiClass matchClass = CiClass.New(CiCallType.Sealed, CiId.MatchClass, "Match");
		matchClass.Add(CiMethodGroup.New(
				CiMethod.NewMutator(CiVisibility.Public, BoolType, CiId.MatchFindStr, "Find", CiVar.New(StringPtrType, "input"), CiVar.New(StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)),
				CiMethod.NewMutator(CiVisibility.Public, BoolType, CiId.MatchFindRegex, "Find", CiVar.New(StringPtrType, "input"), CiVar.New(new CiClassType { Class = regexClass }, "pattern"))));
		matchClass.Add(CiMember.New(IntType, CiId.MatchStart, "Start"));
		matchClass.Add(CiMember.New(IntType, CiId.MatchEnd, "End"));
		matchClass.Add(CiMethod.New(CiVisibility.Public, StringPtrType, CiId.MatchGetCapture, "GetCapture", CiVar.New(UIntType, "group")));
		matchClass.Add(CiMember.New(UIntType, CiId.MatchLength, "Length"));
		matchClass.Add(CiMember.New(StringPtrType, CiId.MatchValue, "Value"));
		Add(matchClass);

		CiClass mathClass = CiClass.New(CiCallType.Static, CiId.None, "Math");
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Acos", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Asin", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Atan", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Atan2", CiVar.New(DoubleType, "y"), CiVar.New(DoubleType, "x")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Cbrt", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatIntType, CiId.MathCeiling, "Ceiling", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Cos", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Cosh", CiVar.New(DoubleType, "a")));
		mathClass.Add(NewConstDouble("E", Math.E));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Exp", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatIntType, CiId.MathMethod, "Floor", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathFusedMultiplyAdd, "FusedMultiplyAdd", CiVar.New(DoubleType, "x"), CiVar.New(DoubleType, "y"), CiVar.New(DoubleType, "z")));
		mathClass.Add(CiMethod.NewStatic(BoolType, CiId.MathIsFinite, "IsFinite", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(BoolType, CiId.MathIsInfinity, "IsInfinity", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(BoolType, CiId.MathIsNaN, "IsNaN", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Log", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathLog2, "Log2", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Log10", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMember.New(FloatType, CiId.MathNaN, "NaN"));
		mathClass.Add(CiMember.New(FloatType, CiId.MathNegativeInfinity, "NegativeInfinity"));
		mathClass.Add(NewConstDouble("PI", Math.PI));
		mathClass.Add(CiMember.New(FloatType, CiId.MathPositiveInfinity, "PositiveInfinity"));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Pow", CiVar.New(DoubleType, "x"), CiVar.New(DoubleType, "y")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Sin", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Sinh", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Sqrt", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Tan", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Tanh", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatIntType, CiId.MathTruncate, "Truncate", CiVar.New(DoubleType, "a")));
		Add(mathClass);

		Add(LockClass);
		Add(BasePtr);
	}

	public static readonly CiSystem Value = new CiSystem();
}

public class CiProgram : CiScope
{
	public readonly List<string> TopLevelNatives = new List<string>();
	public readonly List<CiClass> Classes = new List<CiClass>();
	public readonly Dictionary<string, byte[]> Resources = new Dictionary<string, byte[]>();
}

}
