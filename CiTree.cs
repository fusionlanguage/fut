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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Foxoft.Ci
{

public abstract class CiVisitor
{
	public abstract void VisitAggregateInitializer(CiAggregateInitializer expr);
	public abstract void VisitVar(CiVar expr);
	public abstract void VisitLiteralLong(long value);
	public abstract void VisitLiteralChar(int value);
	public abstract void VisitLiteralDouble(double value);
	public abstract void VisitLiteralString(string value);
	public abstract void VisitLiteralNull();
	public abstract void VisitLiteralFalse();
	public abstract void VisitLiteralTrue();
	public abstract CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent);
	public abstract CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent);
	public abstract CiExpr VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent);
	public abstract CiExpr VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent);
	public abstract CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent);
	public abstract CiExpr VisitSelectExpr(CiSelectExpr expr, CiPriority parent);
	public abstract CiExpr VisitCallExpr(CiCallExpr expr, CiPriority parent);
	public abstract void VisitConst(CiConst statement);
	public abstract void VisitExpr(CiExpr statement);
	public abstract void VisitBlock(CiBlock statement);
	public abstract void VisitAssert(CiAssert statement);
	public abstract void VisitBreak(CiBreak statement);
	public abstract void VisitContinue(CiContinue statement);
	public abstract void VisitDoWhile(CiDoWhile statement);
	public abstract void VisitFor(CiFor statement);
	public abstract void VisitForeach(CiForeach statement);
	public abstract void VisitIf(CiIf statement);
	public abstract void VisitLock(CiLock statement);
	public abstract void VisitNative(CiNative statement);
	public abstract void VisitReturn(CiReturn statement);
	public abstract void VisitSwitch(CiSwitch statement);
	public abstract void VisitThrow(CiThrow statement);
	public abstract void VisitWhile(CiWhile statement);
}

public abstract class CiStatement
{
	public int Line;
	public abstract bool CompletesNormally { get; }
	public virtual void Accept(CiVisitor visitor) => throw new NotImplementedException(GetType().Name);
}

public abstract class CiExpr : CiStatement
{
	public CiType Type;
	public override bool CompletesNormally => true;
	public virtual bool IsIndexing => false;
	public virtual bool IsLiteralZero => false;
	public virtual bool IsConstEnum => false;
	public virtual int IntValue => throw new NotImplementedException(GetType().Name);
	public virtual CiExpr Accept(CiVisitor visitor, CiPriority parent) => throw new NotImplementedException(GetType().Name);
	public override void Accept(CiVisitor visitor) { visitor.VisitExpr(this); }
	public CiLiteral ToLiteralBool(bool value) => value ? new CiLiteralTrue { Line = this.Line } : new CiLiteralFalse { Line = this.Line };
	public CiLiteral ToLiteralLong(long value) => new CiLiteralLong(value) { Line = this.Line };
	public CiLiteral ToLiteralDouble(double value) => new CiLiteralDouble(value) { Line = this.Line };
	public CiLiteral ToLiteralString(string value) => new CiLiteralString(value) { Line = this.Line };
	public virtual bool IsReferenceTo(CiSymbol symbol) => false;
}

public class CiAggregateInitializer : CiExpr
{
	public CiExpr[] Items;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitAggregateInitializer(this);
		return this;
	}
}

public class CiSymbol : CiExpr
{
	public string Name;
	public CiScope Parent;
	public CiCodeDoc Documentation = null;
	public override string ToString() => this.Name;
}

public abstract class CiScope : CiSymbol, IEnumerable<CiSymbol>
{
	protected readonly OrderedDictionary Dict = new OrderedDictionary();

	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.Dict.Values.GetEnumerator();
	}

	IEnumerator<CiSymbol> IEnumerable<CiSymbol>.GetEnumerator()
	{
		return this.Dict.Values.Cast<CiSymbol>().GetEnumerator();
	}

	public int Count => this.Dict.Count;

	public CiContainerType Container
	{
		get
		{
			for (CiScope scope = this; scope != null; scope = scope.Parent) {
				if (scope is CiContainerType container)
					return container;
			}
			throw new InvalidOperationException();
		}
	}

	public virtual CiSymbol TryLookup(string name)
	{
		for (CiScope scope = this; scope != null; scope = scope.Parent) {
			object result = scope.Dict[name];
			if (result != null)
				return (CiSymbol) result;
		}
		return null;
	}

	static bool IsPrivate(CiSymbol symbol)
	{
		return symbol is CiMember member && member.Visibility == CiVisibility.Private;
	}

	static bool IsOverrideOf(CiSymbol derivedSymbol, CiSymbol baseSymbol)
	{
		if (derivedSymbol is CiMethod derived
		 && (derived.CallType == CiCallType.Override || derived.CallType == CiCallType.Sealed)
		 && baseSymbol is CiMethod baseMethod
		 && baseMethod.CallType != CiCallType.Static && baseMethod.CallType != CiCallType.Normal) {
			// TODO: check parameter and return type
			baseMethod.Calls.Add(derived);
			return true;
		}
		return false;
	}

	public void Add(CiSymbol symbol)
	{
		string name = symbol.Name;
		for (CiScope scope = this; scope != null; scope = scope.Parent) {
			if (scope.Dict[name] is CiSymbol duplicate
			 && (scope == this || (!IsPrivate(duplicate) && !IsOverrideOf(symbol, duplicate)))) {
				CiScope symbolScope = symbol as CiScope ?? this;
				CiScope duplicateScope = duplicate as CiScope ?? scope;
				throw new CiException(symbolScope.Container.Filename, symbol.Line,
					$"Duplicate symbol {name}, already defined in {duplicateScope.Container.Filename} line {duplicate.Line}");
			}
		}
		symbol.Parent = this;
		this.Dict.Add(name, symbol);
	}

	public void AddRange(IEnumerable<CiSymbol> symbols)
	{
		foreach (CiSymbol symbol in symbols)
			Add(symbol);
	}

	public bool Encloses(CiSymbol symbol)
	{
		for (CiScope scope = symbol.Parent; scope != null; scope = scope.Parent) {
			if (scope == this)
				return true;
		}
		return false;
	}
}

public abstract class CiNamedValue : CiSymbol
{
	public CiExpr TypeExpr;
	public CiExpr Value;
	public bool IsAssignableStorage => this.Type is CiClass && this.Value is CiLiteralNull;
}

public class CiMember : CiNamedValue
{
	public CiVisibility Visibility;
	public CiMember()
	{
	}
	public CiMember(CiType type, string name)
	{
		this.Visibility = CiVisibility.Public;
		this.Type = type;
		this.Name = name;
	}
	public virtual bool IsStatic => throw new NotImplementedException(this.GetType().Name);
}

public class CiVar : CiNamedValue
{
	public bool IsAssigned = false;
	public CiVar()
	{
	}
	public CiVar(CiType type, string name)
	{
		this.Type = type;
		this.Name = name;
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitVar(this);
		return this;
	}
}

public class CiConst : CiMember
{
	public CiMethodBase InMethod;
	public CiVisitStatus VisitStatus;
	public CiConst()
	{
	}
	public CiConst(string name, int value)
	{
		this.Visibility = CiVisibility.Public;
		this.Name = name;
		this.Value = new CiLiteralLong(value);
		this.Type = this.Value.Type;
		this.VisitStatus = CiVisitStatus.Done;
	}
	public CiConst(string name, double value)
	{
		this.Visibility = CiVisibility.Public;
		this.Name = name;
		this.Value = new CiLiteralDouble(value);
		this.Type = CiSystem.DoubleType;
		this.VisitStatus = CiVisitStatus.Done;
	}
	public override void Accept(CiVisitor visitor) { visitor.VisitConst(this); }
	public override bool IsStatic => true;
}

public abstract class CiLiteral : CiExpr
{
	public abstract bool IsDefaultValue { get; }
	public static readonly CiLiteralFalse False = new CiLiteralFalse();
	public static readonly CiLiteralTrue True = new CiLiteralTrue();
}

public class CiLiteralLong : CiLiteral
{
	public readonly long Value;
	public CiLiteralLong(long value)
	{
		this.Value = value;
		if (value >= int.MinValue && value <= int.MaxValue)
			this.Type = new CiRangeType((int) value, (int) value);
		else
			this.Type = CiSystem.LongType;
	}
	public override bool IsLiteralZero => this.Value == 0;
	public override int IntValue => (int) this.Value;
	public override bool IsDefaultValue => this.Value == 0;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralLong(this.Value);
		return this;
	}
	public override string ToString() => this.Value.ToString();
}

public class CiLiteralChar : CiLiteralLong
{
	public CiLiteralChar(int value) : base(value)
	{
	}
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
	public readonly double Value;
	public CiLiteralDouble(double value)
	{
		this.Value = value;
		this.Type = CiSystem.DoubleType;
	}
	public override bool IsDefaultValue => BitConverter.DoubleToInt64Bits(this.Value) == 0; // rule out -0.0
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralDouble(this.Value);
		return this;
	}
	public override string ToString() => this.Value.ToString(CultureInfo.InvariantCulture);
}

public class CiLiteralString : CiLiteral
{
	public readonly string Value;
	public CiLiteralString(string value)
	{
		this.Value = value;
		this.Type = CiSystem.StringPtrType;
	}
	public override bool IsDefaultValue => false;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralString(this.Value);
		return this;
	}
	public override string ToString() => this.Value;
	public int GetAsciiLength()
	{
		int length = 0;
		bool escaped = false;
		foreach (char c in this.Value) {
			if (c > 127)
				return -1;
			if (!escaped && c == '\\')
				escaped = true;
			else {
				length++;
				escaped = false;
			}
		}
		return length;
	}
	public int GetAsciiAt(int i)
	{
		bool escaped = false;
		foreach (char c in this.Value) {
			if (c > 127)
				return -1;
			if (!escaped && c == '\\')
				escaped = true;
			else if (i == 0)
				return escaped ? CiLexer.GetEscapedChar(c) : c;
			else {
				i--;
				escaped = false;
			}
		}
		return -1;
	}
	public int GetOneAscii()
	{
		switch (this.Value.Length) {
		case 1:
			int c = this.Value[0];
			return c > 127 ? -1 : c;
		case 2:
			return this.Value[0] != '\\' ? -1 : CiLexer.GetEscapedChar(this.Value[1]);
		default:
			return -1;
		}
	}
}

public class CiLiteralNull : CiLiteral
{
	public CiLiteralNull()
	{
		this.Type = CiSystem.NullType;
	}
	public override bool IsDefaultValue => true;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralNull();
		return this;
	}
	public override string ToString() => "null";
}

public abstract class CiLiteralBool : CiLiteral
{
	protected CiLiteralBool()
	{
		this.Type = CiSystem.BoolType;
	}
}

public class CiLiteralFalse : CiLiteralBool
{
	public override bool IsDefaultValue => true;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralFalse();
		return this;
	}
	public override string ToString() => "false";
}

public class CiLiteralTrue : CiLiteralBool
{
	public override bool IsDefaultValue => false;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralTrue();
		return this;
	}
	public override string ToString() => "true";
}

public class CiImplicitEnumValue : CiExpr
{
	public readonly int Value;
	public CiImplicitEnumValue(int value)
	{
		this.Value = value;
	}
	public override int IntValue => this.Value;
}

public class CiInterpolatedPart
{
	public string Prefix;
	public CiExpr Argument;
	public CiExpr WidthExpr;
	public int Width;
	public char Format;
	public int Precision;
	public CiInterpolatedPart(string prefix, CiExpr arg)
	{
		this.Prefix = prefix;
		this.Argument = arg;
		this.WidthExpr = null;
		this.Format = ' ';
		this.Precision = -1;
	}
}

public class CiInterpolatedString : CiExpr
{
	public CiInterpolatedPart[] Parts;
	public string Suffix;
	public CiInterpolatedString(CiInterpolatedPart[] parts, string suffix)
	{
		this.Type = CiSystem.StringStorageType;
		this.Parts = parts;
		this.Suffix = suffix;
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
	public CiExpr Left;
	public string Name;
	public CiSymbol Symbol;
	public override bool IsConstEnum => this.Symbol.Parent is CiEnum;
	public override int IntValue => ((CiConst) this.Symbol).Value.IntValue;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitSymbolReference(this, parent);
	public override bool IsReferenceTo(CiSymbol symbol) => this.Symbol == symbol;
	public override string ToString() => this.Left != null ? $"{this.Left}.{this.Name}" : this.Name;
}

public abstract class CiUnaryExpr : CiExpr
{
	public CiToken Op;
	public CiExpr Inner;
}

public class CiPrefixExpr : CiUnaryExpr
{
	public override bool IsConstEnum => this.Type is CiEnumFlags && this.Inner.IsConstEnum; // && this.Op == CiToken.Tilde
	public override int IntValue
	{
		get
		{
			Trace.Assert(this.Op == CiToken.Tilde);
			return ~this.Inner.IntValue;
		}
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitPrefixExpr(this, parent);
}

public class CiPostfixExpr : CiUnaryExpr
{
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitPostfixExpr(this, parent);
}

public class CiBinaryExpr : CiExpr
{
	public CiExpr Left;
	public CiToken Op;
	public CiExpr Right;
	public override bool IsIndexing => this.Op == CiToken.LeftBracket;
	public override bool IsConstEnum
	{
		get
		{
			switch (this.Op) {
			case CiToken.And:
			case CiToken.Or:
			case CiToken.Xor:
				return this.Type is CiEnumFlags && this.Left.IsConstEnum && this.Right.IsConstEnum;
			default:
				return false;
			}
		}
	}
	public override int IntValue
	{
		get
		{
			return this.Op switch {
					CiToken.And => this.Left.IntValue & this.Right.IntValue,
					CiToken.Or => this.Left.IntValue | this.Right.IntValue,
					CiToken.Xor => this.Left.IntValue ^ this.Right.IntValue,
					_ => base.IntValue // throw
				};
		}
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitBinaryExpr(this, parent);
	public bool IsAssign
	{
		get
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
	}

	public string OpString
	{
		get
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

	public static CiType PromoteNumericTypes(CiType left, CiType right)
	{
		return PromoteFloatingTypes(left, right) ?? PromoteIntegerTypes(left, right);
	}

	public override string ToString()
	{
		switch (this.Op) {
		case CiToken.LeftBracket:
			return $"{this.Left}[{this.Right}]";
		default:
			return $"({this.Left} {this.OpString} {this.Right})";
		}
	}
}

public class CiSelectExpr : CiExpr
{
	public CiExpr Cond;
	public CiExpr OnTrue;
	public CiExpr OnFalse;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitSelectExpr(this, parent);
	public override string ToString() => $"({this.Cond} ? {this.OnTrue} : {this.OnFalse})";
}

public class CiCallExpr : CiExpr
{
	public CiSymbolReference Method;
	public CiExpr[] Arguments;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitCallExpr(this, parent);
}

public abstract class CiCondCompletionStatement : CiScope
{
	bool CompletesNormallyValue;
	public override bool CompletesNormally => this.CompletesNormallyValue;
	public void SetCompletesNormally(bool value) { this.CompletesNormallyValue = value; }
}

public abstract class CiLoop : CiCondCompletionStatement
{
	public CiExpr Cond;
	public CiStatement Body;
	public bool HasBreak = false;
}

public class CiBlock : CiCondCompletionStatement
{
	public CiStatement[] Statements;
	public override void Accept(CiVisitor visitor) { visitor.VisitBlock(this); }
}

public class CiAssert : CiStatement
{
	public CiExpr Cond;
	public CiExpr Message = null;
	public override bool CompletesNormally => !(this.Cond is CiLiteralFalse);
	public override void Accept(CiVisitor visitor) { visitor.VisitAssert(this); }
}

public class CiBreak : CiStatement
{
	public readonly CiCondCompletionStatement LoopOrSwitch;
	public CiBreak(CiCondCompletionStatement loopOrSwitch) { this.LoopOrSwitch = loopOrSwitch; }
	public override bool CompletesNormally => false;
	public override void Accept(CiVisitor visitor) { visitor.VisitBreak(this); }
}

public class CiContinue : CiStatement
{
	public readonly CiLoop Loop;
	public CiContinue(CiLoop loop) { this.Loop = loop; }
	public override bool CompletesNormally => false;
	public override void Accept(CiVisitor visitor) { visitor.VisitContinue(this); }
}

public class CiDoWhile : CiLoop
{
	public override void Accept(CiVisitor visitor) { visitor.VisitDoWhile(this); }
}

public class CiFor : CiLoop
{
	public CiExpr Init;
	public CiExpr Advance;
	public bool IsRange = false;
	public bool IsIteratorUsed;
	public long RangeStep;
	public override void Accept(CiVisitor visitor) { visitor.VisitFor(this); }
}

public class CiForeach : CiLoop
{
	public CiExpr Collection;
	public override void Accept(CiVisitor visitor) { visitor.VisitForeach(this); }
	public CiVar Element => (CiVar) this.First();
	public CiVar ValueVar => (CiVar) this.ElementAt(1);
}

public class CiIf : CiCondCompletionStatement
{
	public CiExpr Cond;
	public CiStatement OnTrue;
	public CiStatement OnFalse;
	public override void Accept(CiVisitor visitor) { visitor.VisitIf(this); }
}

public class CiLock : CiStatement
{
	public CiExpr Lock;
	public CiStatement Body;
	public override bool CompletesNormally => this.Body.CompletesNormally;
	public override void Accept(CiVisitor visitor) { visitor.VisitLock(this); }
}

public class CiNative : CiStatement
{
	public string Content;
	public override bool CompletesNormally => true;
	public override void Accept(CiVisitor visitor) { visitor.VisitNative(this); }
}

public class CiReturn : CiStatement
{
	public CiExpr Value;
	public override bool CompletesNormally => false;
	public override void Accept(CiVisitor visitor) { visitor.VisitReturn(this); }
}

public class CiCase
{
	public CiExpr[] Values;
	public CiStatement[] Body;
}

public class CiSwitch : CiCondCompletionStatement
{
	public CiExpr Value;
	public CiCase[] Cases;
	public CiStatement[] DefaultBody;
	public override void Accept(CiVisitor visitor) { visitor.VisitSwitch(this); }

	public static int LengthWithoutTrailingBreak(CiStatement[] body)
	{
		int length = body.Length;
		if (length > 0 && body[length - 1] is CiBreak)
			length--;
		return length;
	}

	public bool HasDefault => this.DefaultBody != null && LengthWithoutTrailingBreak(this.DefaultBody) > 0;

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

	public static bool HasEarlyBreak(CiStatement[] body)
	{
		int length = LengthWithoutTrailingBreak(body);
		for (int i = 0; i < length; i++) {
			if (HasBreak(body[i]))
				return true;
		}
		return false;
	}

	static bool HasContinue(CiStatement[] statements) => statements.Any(HasContinue);

	static bool HasContinue(CiStatement statement)
	{
		switch (statement) {
		case CiContinue _:
			return true;
		case CiIf ifStatement:
			return HasContinue(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasContinue(ifStatement.OnFalse));
		case CiSwitch switchStatement:
			return switchStatement.Cases.Any(kase => HasContinue(kase.Body)) || (switchStatement.DefaultBody != null && HasContinue(switchStatement.DefaultBody));
		case CiBlock block:
			return HasContinue(block.Statements);
		default:
			return false;
		}
	}

	public static bool HasEarlyBreakAndContinue(CiStatement[] body) => HasEarlyBreak(body) && HasContinue(body);
}

public class CiThrow : CiStatement
{
	public CiExpr Message;
	public override bool CompletesNormally => false;
	public override void Accept(CiVisitor visitor) { visitor.VisitThrow(this); }
}

public class CiWhile : CiLoop
{
	public override void Accept(CiVisitor visitor) { visitor.VisitWhile(this); }
}

public class CiField : CiMember
{
	public override bool IsStatic => false;
}

public class CiMethodBase : CiMember
{
	public bool Throws;
	public CiStatement Body;
	public bool IsLive = false;
	public readonly HashSet<CiMethod> Calls = new HashSet<CiMethod>();
}

public class CiParameters : CiScope
{
}

public class CiMethod : CiMethodBase
{
	public CiCallType CallType;
	public bool IsMutator;
	public readonly CiParameters Parameters = new CiParameters();
	public CiMethod()
	{
	}
	public CiMethod(CiCallType callType, CiType type, string name, params CiVar[] parameters)
	{
		this.Visibility = CiVisibility.Public;
		this.CallType = callType;
		this.Type = type;
		this.Name = name;
		this.Parameters.AddRange(parameters);
	}
	public override bool IsStatic => this.CallType == CiCallType.Static;
	public bool IsAbstractOrVirtual => this.CallType == CiCallType.Abstract || this.CallType == CiCallType.Virtual;
	public CiMethod DeclaringMethod
	{
		get
		{
			CiMethod method = this;
			while (method.CallType == CiCallType.Override) {
				method = (CiMethod) method.Parent.Parent.TryLookup(method.Name);
			}
			return method;
		}
	}
}

public class CiMethodGroup : CiMember
{
	public readonly CiMethod[] Methods;
	public CiMethodGroup(params CiMethod[] methods)
	{
		this.Visibility = CiVisibility.Public;
		this.Name = methods[0].Name;
		this.Methods = methods;
	}
}

public class CiType : CiScope
{
	public virtual string ArrayString => "";
	public virtual bool IsAssignableFrom(CiType right) => this == right;
	public virtual bool IsPointer => false;
	public virtual CiType BaseType => this;
	public virtual CiType StorageType => this;
	public virtual CiType PtrOrSelf => this;
	public virtual bool IsFinal => false;
	public virtual bool IsClass(CiClass klass) => false;
	public virtual bool IsReadonlyPtr => false;
	public virtual bool IsDynamicPtr => false;
}

public abstract class CiContainerType : CiType
{
	public bool IsPublic;
	public string Filename;
}

public class CiEnum : CiContainerType
{
}

public class CiEnumFlags : CiEnum
{
}

public enum CiVisitStatus
{
	NotYet,
	InProgress,
	Done
}

public class CiClass : CiContainerType
{
	public CiCallType CallType;
	public string BaseClassName;
	public CiMethodBase Constructor;
	public CiConst[] Consts;
	public CiField[] Fields;
	public CiMethod[] Methods;
	public readonly List<CiConst> ConstArrays = new List<CiConst>();
	public CiVisitStatus VisitStatus;
	public override string ToString() => this.Name + "()";
	public override CiType PtrOrSelf => new CiClassPtrType { Class = this, Modifier = CiToken.ExclamationMark };
	public override bool IsFinal => this != CiSystem.MatchClass;
	public override bool IsClass(CiClass klass) => this == klass;
	public bool AddsVirtualMethods => this.Methods.Any(method => method.IsAbstractOrVirtual);

	public CiClass()
	{
		this.Dict.Add("this", new CiVar(this.PtrOrSelf, "this")); // shadows "this" in base class
	}

	public CiClass(CiCallType callType, string name, params CiMethod[] methods)
	{
		this.CallType = callType;
		this.Name = name;
		this.Methods = methods;
		AddRange(methods);
	}

	public CiSymbol TryShallowLookup(string name) => (CiSymbol) this.Dict[name];

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

public abstract class CiNumericType : CiType
{
}

public class CiIntegerType : CiNumericType
{
	public override bool IsAssignableFrom(CiType right) => right is CiIntegerType || right == CiSystem.FloatIntType;
}

public class CiRangeType : CiIntegerType
{
	public readonly int Min;
	public readonly int Max;

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

	public override bool Equals(object obj)
	{
		return obj is CiRangeType that && this.Min == that.Min && this.Max == that.Max;
	}

	public override int GetHashCode()
	{
		return this.Min.GetHashCode() ^ this.Max.GetHashCode();
	}

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

	public int VariableBits => GetMask(this.Min ^ this.Max);

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

public class CiFloatingType : CiNumericType
{
	public override bool IsAssignableFrom(CiType right)
	{
		return right is CiNumericType;
	}
}

public abstract class CiStringType : CiType
{
	public override CiSymbol TryLookup(string name)
	{
		switch (name) {
		case "Contains":
			return CiSystem.StringContains;
		case "EndsWith":
			return CiSystem.StringEndsWith;
		case "IndexOf":
			return CiSystem.StringIndexOf;
		case "LastIndexOf":
			return CiSystem.StringLastIndexOf;
		case "Length":
			return CiSystem.StringLength;
		case "StartsWith":
			return CiSystem.StringStartsWith;
		case "Substring":
			return CiSystem.StringSubstring;
		default:
			return null;
		}
	}
}

public class CiStringPtrType : CiStringType
{
	public override bool IsPointer => true;
	public override bool IsAssignableFrom(CiType right)
	{
		return right == CiSystem.NullType || right is CiStringType;
	}
}

public class CiStringStorageType : CiStringType
{
	public override CiType PtrOrSelf => CiSystem.StringPtrType;
	public override bool IsAssignableFrom(CiType right)
	{
		return right is CiStringType;
	}
}

public class CiClassPtrType : CiType
{
	public override bool IsPointer => true;
	public CiClass Class;
	public CiToken Modifier;
	public override string ToString()
	{
		switch (this.Modifier) {
		case CiToken.EndOfFile:
			return this.Class.Name;
		case CiToken.ExclamationMark:
			return this.Class.Name + '!';
		case CiToken.Hash:
			return this.Class.Name + '#';
		default:
			throw new NotImplementedException(this.Modifier.ToString());
		}
	}

	public bool IsModifierAssignableFrom(CiClassPtrType right)
	{
		switch (this.Modifier) {
		case CiToken.EndOfFile:
			return true;
		case CiToken.ExclamationMark:
			return right.Modifier != CiToken.EndOfFile;
		case CiToken.Hash:
			return right.Modifier == CiToken.Hash;
		default:
			throw new NotImplementedException(this.Modifier.ToString());
		}
	}

	public override bool IsAssignableFrom(CiType right)
	{
		if (right == CiSystem.NullType)
			return true;
		if (right is CiClass klass) {
			if (this.Modifier == CiToken.Hash)
				return false;
		}
		else if (right is CiClassPtrType rightPtr && IsModifierAssignableFrom(rightPtr))
			klass = rightPtr.Class;
		else
			return false;
		return this.Class.IsSameOrBaseOf(klass);
	}

	public override CiType PtrOrSelf => this.Modifier == CiToken.Hash ? this.Class.PtrOrSelf : this;
	public override bool IsClass(CiClass klass) => this.Class == klass;
	public override bool IsReadonlyPtr => this.Modifier == CiToken.EndOfFile;
	public override bool IsDynamicPtr => this.Modifier == CiToken.Hash;

	public override bool Equals(object obj)
	{
		return obj is CiClassPtrType that && this.Class == that.Class && this.Modifier == that.Modifier;
	}

	public override int GetHashCode() => this.Class.GetHashCode() ^ this.Modifier.GetHashCode();
}

public abstract class CiCollectionType : CiType
{
	public CiType ElementType;
}

public abstract class CiArrayType : CiCollectionType
{
	public override string ToString() {
		return this.BaseType + this.ArrayString + this.ElementType.ArrayString;
	}
	public override CiSymbol TryLookup(string name)
	{
		if (name == "CopyTo") {
			return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "CopyTo" ,
				new CiVar(CiSystem.IntType, "sourceIndex"),
				new CiVar(this.PtrOrSelf , "destinationArray"),
				new CiVar(CiSystem.IntType, "destinationIndex"),
				new CiVar(CiSystem.IntType, "count"));
		}
		return null;
	}
	public override CiType BaseType => this.ElementType.BaseType;
	protected CiMethod BinarySearch => new CiMethod(CiCallType.Normal, CiSystem.IntType, "BinarySearch",
		new CiVar(this.ElementType, "value"),
		new CiVar(CiSystem.IntType, "startIndex"),
		new CiVar(CiSystem.IntType, "count"));
	protected CiMethod Fill => new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Fill",
		new CiVar(this.ElementType, "value"),
		new CiVar(CiSystem.IntType, "startIndex"),
		new CiVar(CiSystem.IntType, "count")) { IsMutator = true };
}

public class CiArrayPtrType : CiArrayType
{
	public override bool IsPointer => true;
	public CiToken Modifier;

	public override string ArrayString
	{
		get
		{
			switch (this.Modifier) {
			case CiToken.EndOfFile:
				return "[]";
			case CiToken.ExclamationMark:
				return "[]!";
			case CiToken.Hash:
				return "[]#";
			default:
				throw new NotImplementedException(this.Modifier.ToString());
			}
		}
	}

	public override bool IsAssignableFrom(CiType right)
	{
		if (right == CiSystem.NullType)
			return true;
		if (!(right is CiArrayType array) || !array.ElementType.Equals(this.ElementType))
			return false;
		switch (this.Modifier) {
		case CiToken.EndOfFile:
			return true;
		case CiToken.ExclamationMark:
			return !(array is CiArrayPtrType ptr) || ptr.Modifier != CiToken.EndOfFile;
		case CiToken.Hash:
			return array is CiArrayPtrType dynamicPtr && dynamicPtr.Modifier == CiToken.Hash;
		default:
			throw new NotImplementedException(this.Modifier.ToString());
		}
	}

	public override bool IsReadonlyPtr => this.Modifier == CiToken.EndOfFile;
	public override bool IsDynamicPtr => this.Modifier == CiToken.Hash;

	public override CiSymbol TryLookup(string name)
	{
		if (this.Modifier != CiToken.EndOfFile) {
			switch (name) {
			case "BinarySearch":
				return this.ElementType is CiNumericType ? this.BinarySearch : null;
			case "Fill":
				return this.Fill;
			case "Sort":
				return this.ElementType is CiNumericType ? CiSystem.CollectionSortPart : null;
			default:
				break;
			}
		}
		return base.TryLookup(name);
	}

	public override bool Equals(object obj)
	{
		return obj is CiArrayPtrType that && this.ElementType.Equals(that.ElementType) && this.Modifier == that.Modifier;
	}

	public override int GetHashCode()
	{
		return this.ElementType.GetHashCode() ^ this.Modifier.GetHashCode();
	}
}

public class CiArrayStorageType : CiArrayType
{
	public CiExpr LengthExpr;
	public int Length;
	public bool PtrTaken = false;

	public override string ArrayString => $"[{this.Length}]";
	public override CiSymbol TryLookup(string name)
	{
		switch (name) {
		case "BinarySearch":
			return this.ElementType is CiNumericType
				? new CiMethodGroup(
					new CiMethod(CiCallType.Normal, CiSystem.IntType, "BinarySearch", new CiVar(this.ElementType, "value")),
					this.BinarySearch)
				: null;
		case "Fill":
			return new CiMethodGroup(
				new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Fill", new CiVar(this.ElementType, "value")) { IsMutator = true },
				this.Fill);
		case "Sort":
			return this.ElementType is CiNumericType ? CiSystem.CollectionSort : null;
		case "Length":
			return CiSystem.ArrayLength;
		default:
			return base.TryLookup(name);
		}
	}
	public override bool IsAssignableFrom(CiType right) => false;
	public override CiType StorageType => this.ElementType.StorageType;
	public override CiType PtrOrSelf => new CiArrayPtrType { ElementType = this.ElementType, Modifier = CiToken.ExclamationMark };
	public override bool IsFinal => true;

	public override bool Equals(object obj)
	{
		return obj is CiArrayStorageType that && this.ElementType == that.ElementType && this.Length == that.Length;
	}

	public override int GetHashCode()
	{
		return this.ElementType.GetHashCode() ^ this.Length.GetHashCode();
	}
}

public class CiListType : CiArrayType
{
	public override string ToString() => $"List<{this.ElementType}>";
	public override CiSymbol TryLookup(string name)
	{
		switch (name) {
		case "Add":
			if (this.ElementType.IsFinal)
				return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Add") { IsMutator = true };
			return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Add", new CiVar(this.ElementType, "value")) { IsMutator = true };
		case "Clear":
			return CiSystem.CollectionClear;
		case "Contains":
			return new CiMethod(CiCallType.Normal, CiSystem.BoolType, "Contains", new CiVar(this.ElementType, "value"));
		case "Count":
			return CiSystem.CollectionCount;
		case "Insert":
			if (this.ElementType.IsFinal)
				return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Insert", new CiVar(CiSystem.UIntType, "index")) { IsMutator = true };
			return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Insert", new CiVar(CiSystem.UIntType, "index"), new CiVar(this.ElementType, "value")) { IsMutator = true };
		case "RemoveAt":
			return CiSystem.ListRemoveAt;
		case "RemoveRange":
			return CiSystem.ListRemoveRange;
		case "Sort":
			return this.ElementType is CiNumericType ? CiSystem.CollectionSort : null;
		default:
			return base.TryLookup(name);
		}
	}
	public override CiType PtrOrSelf => new CiArrayPtrType { ElementType = this.ElementType, Modifier = CiToken.ExclamationMark };
	public override bool IsFinal => true;
}

public class CiStackType : CiCollectionType
{
	public override string ToString() => $"Stack<{this.ElementType}>";
	public override CiSymbol TryLookup(string name)
	{
		switch (name) {
		case "Clear":
			return CiSystem.CollectionClear;
		case "Count":
			return CiSystem.CollectionCount;
		case "Peek":
			return new CiMethod(CiCallType.Normal, this.ElementType, "Peek");
		case "Push":
			return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Push", new CiVar(this.ElementType, "value")) { IsMutator = true };
		case "Pop":
			return new CiMethod(CiCallType.Normal, this.ElementType, "Pop") { IsMutator = true };
		default:
			return base.TryLookup(name);
		}
	}
	public override bool IsFinal => true;
}

public class CiHashSetType : CiCollectionType
{
	public override string ToString() => $"HashSet<{this.ElementType}>";
	public override CiSymbol TryLookup(string name)
	{
		switch (name) {
		case "Add":
			return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Add", new CiVar(this.ElementType, "value")) { IsMutator = true };
		case "Clear":
			return CiSystem.CollectionClear;
		case "Contains":
			return new CiMethod(CiCallType.Normal, CiSystem.BoolType, "Contains", new CiVar(this.ElementType, "value"));
		case "Count":
			return CiSystem.CollectionCount;
		case "Remove":
			return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Remove", new CiVar(this.ElementType, "value")) { IsMutator = true };
		default:
			return base.TryLookup(name);
		}
	}
	public override bool IsFinal => true;
}

public class CiDictionaryType : CiType
{
	public CiSymbol Class;
	public CiType KeyType;
	public CiType ValueType;

	public override string ToString() => $"{this.Class.Name}<{this.KeyType}, {this.ValueType}>";
	public override CiSymbol TryLookup(string name)
	{
		switch (name) {
		case "Add":
			if (this.ValueType.IsFinal)
				return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Add", new CiVar(this.KeyType, "key")) { IsMutator = true };
			return null;
		case "Clear":
			return CiSystem.CollectionClear;
		case "ContainsKey":
			return new CiMethod(CiCallType.Normal, CiSystem.BoolType, "ContainsKey", new CiVar(this.KeyType, "key"));
		case "Count":
			return CiSystem.CollectionCount;
		case "Remove":
			return new CiMethod(CiCallType.Normal, CiSystem.VoidType, "Remove", new CiVar(this.KeyType, "key")) { IsMutator = true };
		default:
			return null;
		}
	}
	public override bool IsFinal => true;
}

public class CiGenericTypeDefinition : CiSymbol
{
	public int TypeParameterCount;
}

public class CiPrintableType : CiType
{
	public override bool IsAssignableFrom(CiType right) => right is CiStringType || right is CiNumericType;
}

public class CiSystem : CiScope
{
	public static readonly CiType VoidType = new CiType { Name = "void" };
	public static readonly CiType NullType = new CiType { Name = "null" };
	public static readonly CiIntegerType IntType = new CiIntegerType { Name = "int" };
	public static readonly CiRangeType UIntType = new CiRangeType(0, int.MaxValue) { Name = "uint" };
	public static readonly CiIntegerType LongType = new CiIntegerType { Name = "long" };
	public static readonly CiRangeType ByteType = new CiRangeType(0, 0xff) { Name = "byte" };
	public static readonly CiRangeType ShortType = new CiRangeType(-0x8000, 0x7fff) { Name = "short" };
	public static readonly CiRangeType UShortType = new CiRangeType(0, 0xffff) { Name = "ushort" };
	public static readonly CiRangeType Minus1Type = new CiRangeType(-1, int.MaxValue);
	public static readonly CiFloatingType FloatType = new CiFloatingType { Name = "float" };
	public static readonly CiFloatingType DoubleType = new CiFloatingType { Name = "double" };
	public static readonly CiFloatingType FloatIntType = new CiFloatingType { Name = "float" };
	public static readonly CiRangeType CharType = new CiRangeType(-0x80, 0xffff);
	public static readonly CiEnum BoolType = new CiEnum { Name = "bool" };
	public static readonly CiStringType StringPtrType = new CiStringPtrType { Name = "string" };
	public static readonly CiStringStorageType StringStorageType = new CiStringStorageType { Name = "string()" };
	public static readonly CiMember StringLength = new CiMember(UIntType, "Length");
	public static readonly CiMethod StringContains = new CiMethod(CiCallType.Normal, BoolType, "Contains", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringEndsWith = new CiMethod(CiCallType.Normal, BoolType, "EndsWith", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringIndexOf = new CiMethod(CiCallType.Normal, Minus1Type, "IndexOf", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringLastIndexOf = new CiMethod(CiCallType.Normal, Minus1Type, "LastIndexOf", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringStartsWith = new CiMethod(CiCallType.Normal, BoolType, "StartsWith", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringSubstring = new CiMethod(CiCallType.Normal, StringStorageType, "Substring", new CiVar(IntType, "offset"), new CiVar(IntType, "length") { Value = new CiLiteralLong(-1L) } ); // TODO: UIntType
	public static readonly CiMember ArrayLength = new CiMember(UIntType, "Length");
	public static readonly CiMember CollectionCount = new CiMember(UIntType, "Count");
	public static readonly CiMethod CollectionClear = new CiMethod(CiCallType.Normal, VoidType, "Clear") { IsMutator = true };
	public static readonly CiMethod CollectionSortAll = new CiMethod(CiCallType.Normal, VoidType, "Sort") { IsMutator = true };
	public static readonly CiMethod CollectionSortPart = new CiMethod(CiCallType.Normal, VoidType, "Sort", new CiVar(CiSystem.IntType, "startIndex"), new CiVar(IntType, "count")) { IsMutator = true };
	public static readonly CiMethodGroup CollectionSort = new CiMethodGroup(CollectionSortAll, CollectionSortPart);
	public static readonly CiMethod ListRemoveAt = new CiMethod(CiCallType.Normal, VoidType, "RemoveAt", new CiVar(IntType, "index")) { IsMutator = true };
	public static readonly CiMethod ListRemoveRange = new CiMethod(CiCallType.Normal, VoidType, "RemoveRange", new CiVar(IntType, "index"), new CiVar(IntType, "count")) { IsMutator = true };
	public static readonly CiType PrintableType = new CiPrintableType { Name = "printable" };
	public static readonly CiMethod ConsoleWrite = new CiMethod(CiCallType.Static, VoidType, "Write", new CiVar(PrintableType, "value"));
	public static readonly CiMethod ConsoleWriteLine = new CiMethod(CiCallType.Static, VoidType, "WriteLine", new CiVar(PrintableType, "value") { Value = new CiLiteralString("") });
	public static readonly CiClass ConsoleBase = new CiClass(CiCallType.Static, "ConsoleBase",
		ConsoleWrite,
		ConsoleWriteLine);
	public static readonly CiMember ConsoleError = new CiMember(ConsoleBase, "Error");
	public static readonly CiClass ConsoleClass = new CiClass(CiCallType.Static, "Console");
	public static readonly CiArrayPtrType ReadOnlyByteArrayPtrType = new CiArrayPtrType { ElementType = ByteType, Modifier = CiToken.EndOfFile };
	public static readonly CiArrayPtrType ReadWriteByteArrayPtrType = new CiArrayPtrType { ElementType = ByteType, Modifier = CiToken.ExclamationMark };
	public static readonly CiMethod UTF8GetByteCount = new CiMethod(CiCallType.Normal, IntType, "GetByteCount", new CiVar(StringPtrType, "str"));
	public static readonly CiMethod UTF8GetBytes = new CiMethod(CiCallType.Normal, VoidType, "GetBytes", new CiVar(StringPtrType, "str"), new CiVar(ReadWriteByteArrayPtrType, "bytes"), new CiVar(IntType, "byteIndex"));
	public static readonly CiMethod UTF8GetString = new CiMethod(CiCallType.Normal, StringStorageType, "GetString", new CiVar(ReadOnlyByteArrayPtrType, "bytes"), new CiVar(IntType, "offset"), new CiVar(IntType, "length")); // TODO: UIntType
	public static readonly CiClass UTF8EncodingClass = new CiClass(CiCallType.Sealed, "UTF8Encoding",
		UTF8GetByteCount,
		UTF8GetBytes,
		UTF8GetString);
	public static readonly CiClass EncodingClass = new CiClass(CiCallType.Static, "Encoding");
	public static readonly CiMethod EnvironmentGetEnvironmentVariable = new CiMethod(CiCallType.Static, StringPtrType, "GetEnvironmentVariable", new CiVar(StringPtrType, "name"));
	public static readonly CiClass EnvironmentClass = new CiClass(CiCallType.Static, "Environment", EnvironmentGetEnvironmentVariable);
	public static readonly CiConst RegexOptionsNone = new CiConst("None", 0);
	public static readonly CiConst RegexOptionsIgnoreCase = new CiConst("IgnoreCase", 1);
	public static readonly CiConst RegexOptionsMultiline = new CiConst("Multiline", 2);
	public static readonly CiConst RegexOptionsSingleline = new CiConst("Singleline", 16);
	public static readonly CiEnum RegexOptionsEnum = new CiEnumFlags { Name = "RegexOptions" };
	public static readonly CiMethod RegexCompile = new CiMethod(CiCallType.Static, null /* filled later to avoid cyclic reference */, "Compile", new CiVar(StringPtrType, "pattern"), new CiVar(RegexOptionsEnum, "options") { Value = RegexOptionsNone });
	public static readonly CiMethod RegexEscape = new CiMethod(CiCallType.Static, StringStorageType, "Escape", new CiVar(StringPtrType, "str"));
	public static readonly CiMethod RegexIsMatchStr = new CiMethod(CiCallType.Static, BoolType, "IsMatch", new CiVar(StringPtrType, "input"), new CiVar(StringPtrType, "pattern"), new CiVar(RegexOptionsEnum, "options") { Value = RegexOptionsNone });
	public static readonly CiMethod RegexIsMatchRegex = new CiMethod(CiCallType.Normal, BoolType, "IsMatch", new CiVar(StringPtrType, "input"));
	public static readonly CiClass RegexClass = new CiClass(CiCallType.Sealed, "Regex",
		RegexCompile,
		RegexEscape);
	public static readonly CiMethod MatchFindStr = new CiMethod(CiCallType.Normal, BoolType, "Find", new CiVar(StringPtrType, "input"), new CiVar(StringPtrType, "pattern"), new CiVar(RegexOptionsEnum, "options") { Value = RegexOptionsNone }) { IsMutator = true };
	public static readonly CiMethod MatchFindRegex = new CiMethod(CiCallType.Normal, BoolType, "Find", new CiVar(StringPtrType, "input"), new CiVar(new CiClassPtrType { Class = RegexClass, Modifier = CiToken.EndOfFile }, "pattern")) { IsMutator = true };
	public static readonly CiMember MatchStart = new CiMember(IntType, "Start");
	public static readonly CiMember MatchEnd = new CiMember(IntType, "End");
	public static readonly CiMember MatchLength = new CiMember(UIntType, "Length");
	public static readonly CiMember MatchValue = new CiMember(StringPtrType, "Value");
	public static readonly CiMethod MatchGetCapture = new CiMethod(CiCallType.Normal, StringPtrType, "GetCapture", new CiVar(UIntType, "group"));
	public static readonly CiClass MatchClass = new CiClass(CiCallType.Sealed, "Match",
		MatchGetCapture);
	public static readonly CiMember MathNaN = new CiMember(FloatType, "NaN");
	public static readonly CiMember MathNegativeInfinity = new CiMember(FloatType, "NegativeInfinity");
	public static readonly CiMember MathPositiveInfinity = new CiMember(FloatType, "PositiveInfinity");
	public static readonly CiMethod MathCeiling = new CiMethod(CiCallType.Static, FloatIntType, "Ceiling", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathFusedMultiplyAdd = new CiMethod(CiCallType.Static, FloatType, "FusedMultiplyAdd", new CiVar(DoubleType, "x"), new CiVar(DoubleType, "y"), new CiVar(DoubleType, "z"));
	public static readonly CiMethod MathIsFinite = new CiMethod(CiCallType.Static, BoolType, "IsFinite", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathIsInfinity = new CiMethod(CiCallType.Static, BoolType, "IsInfinity", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathIsNaN = new CiMethod(CiCallType.Static, BoolType, "IsNaN", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathLog2 = new CiMethod(CiCallType.Static, FloatType, "Log2", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathTruncate = new CiMethod(CiCallType.Static, FloatIntType, "Truncate", new CiVar(DoubleType, "a"));
	public static readonly CiClass MathClass = new CiClass(CiCallType.Static, "Math",
		new CiMethod(CiCallType.Static, FloatType, "Acos", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Asin", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Atan", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Atan2", new CiVar(DoubleType, "y"), new CiVar(DoubleType, "x")),
		new CiMethod(CiCallType.Static, FloatType, "Cbrt", new CiVar(DoubleType, "a")),
		MathCeiling,
		new CiMethod(CiCallType.Static, FloatType, "Cos", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Cosh", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Exp", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatIntType, "Floor", new CiVar(DoubleType, "a")),
		MathFusedMultiplyAdd,
		MathIsFinite,
		MathIsInfinity,
		MathIsNaN,
		new CiMethod(CiCallType.Static, FloatType, "Log", new CiVar(DoubleType, "a")),
		MathLog2,
		new CiMethod(CiCallType.Static, FloatType, "Log10", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Pow", new CiVar(DoubleType, "x"), new CiVar(DoubleType, "y")),
		new CiMethod(CiCallType.Static, FloatType, "Sin", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Sinh", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Sqrt", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Tan", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, "Tanh", new CiVar(DoubleType, "a")),
		MathTruncate);
	public static readonly CiGenericTypeDefinition ListClass = new CiGenericTypeDefinition { Name = "List", TypeParameterCount = 1 };
	public static readonly CiGenericTypeDefinition StackClass = new CiGenericTypeDefinition { Name = "Stack", TypeParameterCount = 1 };
	public static readonly CiGenericTypeDefinition HashSetClass = new CiGenericTypeDefinition { Name = "HashSet", TypeParameterCount = 1 };
	public static readonly CiGenericTypeDefinition DictionaryClass = new CiGenericTypeDefinition { Name = "Dictionary", TypeParameterCount = 2 };
	public static readonly CiGenericTypeDefinition SortedDictionaryClass = new CiGenericTypeDefinition { Name = "SortedDictionary", TypeParameterCount = 2 };
	public static readonly CiGenericTypeDefinition OrderedDictionaryClass = new CiGenericTypeDefinition { Name = "OrderedDictionary", TypeParameterCount = 2 };
	public static readonly CiClass LockClass = new CiClass(CiCallType.Sealed, "Lock");
	public static readonly CiSymbol BasePtr = new CiSymbol { Name = "base" };

	static void AddEnumValue(CiEnum enu, CiConst value)
	{
		value.Type = enu;
		enu.Add(value);
	}

	CiSystem()
	{
		Add(IntType);
		Add(UIntType);
		Add(LongType);
		Add(ByteType);
		Add(ShortType);
		Add(UShortType);
		Add(FloatType);
		Add(DoubleType);
		Add(BoolType);
		Add(StringPtrType);
		ConsoleClass.Add(ConsoleError);
		Add(ConsoleClass);
		ConsoleClass.Parent = ConsoleBase;
		EncodingClass.Add(new CiMember(UTF8EncodingClass, "UTF8"));
		Add(EncodingClass);
		Add(EnvironmentClass);
		AddEnumValue(RegexOptionsEnum, RegexOptionsNone);
		AddEnumValue(RegexOptionsEnum, RegexOptionsIgnoreCase);
		AddEnumValue(RegexOptionsEnum, RegexOptionsMultiline);
		AddEnumValue(RegexOptionsEnum, RegexOptionsSingleline);
		Add(RegexOptionsEnum);
		RegexCompile.Type = new CiClassPtrType { Class = RegexClass, Modifier = CiToken.Hash };
		RegexClass.Add(new CiMethodGroup(RegexIsMatchStr, RegexIsMatchRegex));
		Add(RegexClass);
		MatchClass.Add(new CiMethodGroup(MatchFindStr, MatchFindRegex));
		MatchClass.Add(MatchStart);
		MatchClass.Add(MatchEnd);
		MatchClass.Add(MatchLength);
		MatchClass.Add(MatchValue);
		Add(MatchClass);
		MathClass.Add(new CiConst("E", Math.E));
		MathClass.Add(new CiConst("PI", Math.PI));
		MathClass.Add(MathNaN);
		MathClass.Add(MathNegativeInfinity);
		MathClass.Add(MathPositiveInfinity);
		Add(MathClass);
		Add(ListClass);
		Add(StackClass);
		Add(HashSetClass);
		Add(DictionaryClass);
		Add(SortedDictionaryClass);
		Add(OrderedDictionaryClass);
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
