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
	public readonly List<CiExpr> Items = new List<CiExpr>();
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitAggregateInitializer(this);
		return this;
	}
}

public class CiSymbol : CiExpr
{
	public CiId Id = CiId.None;
	public string Name;
	public CiScope Parent;
	public CiCodeDoc Documentation = null;
	public override string ToString() => this.Name;
}

public abstract class CiScope : CiSymbol, IEnumerable<CiSymbol>
{
	readonly OrderedDictionary Dict = new OrderedDictionary();

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

	public CiSymbol TryShallowLookup(string name) => (CiSymbol) this.Dict[name];

	public virtual CiSymbol TryLookup(string name)
	{
		for (CiScope scope = this; scope != null; scope = scope.Parent) {
			object result = scope.Dict[name];
			if (result != null)
				return (CiSymbol) result;
		}
		return null;
	}

	public bool Contains(CiSymbol symbol) => this.Dict.Contains(symbol);

	public void Add(CiSymbol symbol)
	{
		symbol.Parent = this;
		this.Dict.Add(symbol.Name, symbol);
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
	public virtual string GetLiteralString() => throw new NotImplementedException();
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
	public override string GetLiteralString() => this.Value.ToString();
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
	public override string GetLiteralString() => this.Value.ToString(CultureInfo.InvariantCulture);
	public override string ToString() => GetLiteralString();
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
	public override string GetLiteralString() => this.Value;
	public override string ToString() => '"' + this.Value + '"';
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
	public readonly List<CiInterpolatedPart> Parts = new List<CiInterpolatedPart>();
	public string Suffix;
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

	public static CiType PromoteNumericTypes(CiType left, CiType right) => PromoteFloatingTypes(left, right) ?? PromoteIntegerTypes(left, right);

	public override string ToString() => this.Op == CiToken.LeftBracket ? $"{this.Left}[{this.Right}]" : $"({this.Left} {this.OpString} {this.Right})";
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
	public readonly List<CiExpr> Arguments = new List<CiExpr>();
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
	public readonly List<CiStatement> Statements = new List<CiStatement>();
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
	public readonly List<CiExpr> Values = new List<CiExpr>();
	public readonly List<CiStatement> Body = new List<CiStatement>();
}

public class CiSwitch : CiCondCompletionStatement
{
	public CiExpr Value;
	public readonly List<CiCase> Cases = new List<CiCase>();
	public readonly List<CiStatement> DefaultBody = new List<CiStatement>();
	public override void Accept(CiVisitor visitor) { visitor.VisitSwitch(this); }

	public static int LengthWithoutTrailingBreak(List<CiStatement> body)
	{
		int length = body.Count;
		if (length > 0 && body[length - 1] is CiBreak)
			length--;
		return length;
	}

	public bool HasDefault => LengthWithoutTrailingBreak(this.DefaultBody) > 0;

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

	static bool HasContinue(List<CiStatement> statements) => statements.Any(HasContinue);

	static bool HasContinue(CiStatement statement)
	{
		switch (statement) {
		case CiContinue _:
			return true;
		case CiIf ifStatement:
			return HasContinue(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasContinue(ifStatement.OnFalse));
		case CiSwitch switchStatement:
			return switchStatement.Cases.Any(kase => HasContinue(kase.Body)) || HasContinue(switchStatement.DefaultBody);
		case CiBlock block:
			return HasContinue(block.Statements);
		default:
			return false;
		}
	}

	public static bool HasEarlyBreakAndContinue(List<CiStatement> body) => HasEarlyBreak(body) && HasContinue(body);
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
	public CiMethod(CiCallType callType, CiType type, CiId id, string name, params CiVar[] parameters)
	{
		this.Visibility = CiVisibility.Public;
		this.CallType = callType;
		this.Type = type;
		this.Id = id;
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
			while (method.CallType == CiCallType.Override)
				method = (CiMethod) method.Parent.Parent.TryLookup(method.Name);
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
	public virtual string ArraySuffix => "";
	public virtual bool IsAssignableFrom(CiType right) => this == right;
	public virtual bool EqualsType(CiType right) => this == right;
	public virtual bool IsNullable => false;
	public virtual CiType BaseType => this;
	public virtual CiType StorageType => this;
	public virtual CiType PtrOrSelf => this;
	public virtual bool IsFinal => false;
	public virtual bool IsClass(CiClass klass) => false;
	public virtual bool IsArray => false;
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
	public int TypeParameterCount = 0;
	public string BaseClassName;
	public CiMethodBase Constructor;
	public readonly List<CiConst> ConstArrays = new List<CiConst>();
	public CiVisitStatus VisitStatus;
	public override string ToString() => this.Name + "()";
	public override CiType PtrOrSelf => new CiReadWriteClassType { Class = this };
	public override bool IsFinal => this != CiSystem.MatchClass;
	public override bool IsClass(CiClass klass) => this == klass;
	public bool AddsVirtualMethods => this.OfType<CiMethod>().Any(method => method.IsAbstractOrVirtual);

	public CiClass()
	{
		Add(new CiVar(this.PtrOrSelf, "this")); // shadows "this" in base class
	}

	public CiClass(CiCallType callType, string name, params CiMember[] members)
	{
		this.CallType = callType;
		this.Name = name;
		AddRange(members);
	}

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
	public override bool IsAssignableFrom(CiType right) => right is CiNumericType;
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
	public override bool IsNullable => true;
	public override bool IsAssignableFrom(CiType right) => right == CiSystem.NullType || right is CiStringType;
}

public class CiStringStorageType : CiStringType
{
	public override CiType PtrOrSelf => CiSystem.StringPtrType;
	public override bool IsAssignableFrom(CiType right) => right is CiStringType;
}

public class CiClassType : CiType
{
	public CiClass Class;
	public CiType TypeArg0;
	public CiType TypeArg1;
	public CiType ElementType => this.TypeArg0;
	public CiType KeyType => this.TypeArg0;
	public CiType ValueType => this.TypeArg1;
	public override bool IsNullable => true;
	public override bool IsArray => this.Class == CiSystem.ArrayPtrClass;
	public override CiType BaseType => this.IsArray ? this.ElementType.BaseType : this;
	public override bool IsClass(CiClass klass) => this.Class == klass;

	public CiType EvalType(CiType type)
	{
		if (type == CiSystem.TypeParam0)
			return this.TypeArg0;
		if (type == CiSystem.TypeParam0NotFinal)
			return this.TypeArg0.IsFinal ? null : this.TypeArg0;
		if (type is CiReadWriteClassType array && array.IsArray && array.ElementType == CiSystem.TypeParam0)
			return new CiReadWriteClassType { Class = CiSystem.ArrayPtrClass, TypeArg0 = this.TypeArg0 };
		return type;
	}

	public override CiSymbol TryLookup(string name) => this.Class.TryLookup(name);

	bool EqualTypeArguments(CiClassType right)
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
			|| (right is CiClassType rightClass && IsAssignableFromClass(rightClass))
			|| (right is CiClass rightClassStorage && this.Class.IsSameOrBaseOf(rightClassStorage));
	}

	public override bool EqualsType(CiType right)
		=> right is CiClassType that // TODO: exact match
			&& this.Class == that.Class && EqualTypeArguments(that);

	public override string ArraySuffix => this.IsArray ? "[]" : "";
	public virtual string ClassSuffix => "";

	public override string ToString()
	{
		if (this.IsArray)
			return this.ElementType.BaseType + this.ArraySuffix + this.ElementType.ArraySuffix;
		switch (this.Class.TypeParameterCount) {
		case 0: return this.Class.Name + this.ClassSuffix;
		case 1: return $"{this.Class.Name}<{this.TypeArg0}>{this.ClassSuffix}";
		case 2: return $"{this.Class.Name}<{this.TypeArg0}, {this.TypeArg1}>{this.ClassSuffix}";
		default: throw new NotImplementedException();
		}
	}
}

public class CiReadWriteClassType : CiClassType
{
	public override bool IsAssignableFrom(CiType right)
	{
		return right == CiSystem.NullType
			|| (right is CiReadWriteClassType rightClass && IsAssignableFromClass(rightClass))
			|| (right is CiClass rightClassStorage && this.Class.IsSameOrBaseOf(rightClassStorage));
	}

	public override string ArraySuffix => this.IsArray ? "[]!" : "";
	public override string ClassSuffix => "!";
}

public class CiStorageType : CiReadWriteClassType
{
	public override bool IsFinal => true;
	public override bool IsNullable => false;
	public override bool IsAssignableFrom(CiType right) => false;
	public override CiType PtrOrSelf => new CiReadWriteClassType { Class = this.Class, TypeArg0 = this.TypeArg0, TypeArg1 = this.TypeArg1 };
	public override string ClassSuffix => "()";
}

public class CiDynamicPtrType : CiReadWriteClassType
{
	public override bool IsAssignableFrom(CiType right)
	{
		return right == CiSystem.NullType
			|| (right is CiDynamicPtrType rightClass && IsAssignableFromClass(rightClass));
	}
	public override CiType PtrOrSelf => new CiReadWriteClassType { Class = this.Class, TypeArg0 = this.TypeArg0 };

	public override string ArraySuffix => this.IsArray ? "[]#" : "";
	public override string ClassSuffix => "#";
}

public class CiArrayStorageType : CiStorageType
{
	public CiExpr LengthExpr;
	public int Length;
	public bool PtrTaken = false;

	public CiArrayStorageType()
	{
		this.Class = CiSystem.ArrayStorageClass;
	}

	public override string ToString() => this.BaseType + this.ArraySuffix + this.ElementType.ArraySuffix;
	public override CiType BaseType => this.ElementType.BaseType;
	public override bool IsArray => true;
	public override string ArraySuffix => $"[{this.Length}]";
	public override bool EqualsType(CiType right) => right is CiArrayStorageType that && this.ElementType.EqualsType(that.ElementType) && this.Length == that.Length;
	public override CiType StorageType => this.ElementType.StorageType;
	public override CiType PtrOrSelf => new CiReadWriteClassType { Class = CiSystem.ArrayPtrClass, TypeArg0 = this.ElementType };
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
	public static readonly CiMethod StringContains = new CiMethod(CiCallType.Normal, BoolType, CiId.StringContains, "Contains", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringEndsWith = new CiMethod(CiCallType.Normal, BoolType, CiId.StringEndsWith, "EndsWith", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringIndexOf = new CiMethod(CiCallType.Normal, Minus1Type, CiId.StringIndexOf, "IndexOf", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringLastIndexOf = new CiMethod(CiCallType.Normal, Minus1Type, CiId.StringLastIndexOf, "LastIndexOf", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringStartsWith = new CiMethod(CiCallType.Normal, BoolType, CiId.StringStartsWith, "StartsWith", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringSubstring = new CiMethod(CiCallType.Normal, StringStorageType, CiId.StringSubstring, "Substring", new CiVar(IntType, "offset"), new CiVar(IntType, "length") { Value = new CiLiteralLong(-1L) } ); // TODO: UIntType
	public static readonly CiType PrintableType = new CiPrintableType { Name = "printable" };
	public static readonly CiMember ArrayLength = new CiMember(UIntType, "Length");
	public static readonly CiMethod ArrayBinarySearchAll = new CiMethod(CiCallType.Normal, IntType, CiId.ArrayBinarySearchAll, "BinarySearch", new CiVar(TypeParam0, "value")) { Visibility = CiVisibility.NumericElementType };
	public static readonly CiMethod ArrayBinarySearchPart = new CiMethod(CiCallType.Normal, IntType, CiId.ArrayBinarySearchPart, "BinarySearch",
		new CiVar(TypeParam0, "value"),
		new CiVar(IntType, "startIndex"),
		new CiVar(IntType, "count")) { Visibility = CiVisibility.NumericElementType };
	public static readonly CiMethod ArrayFillAll = new CiMethod(CiCallType.Normal, VoidType, CiId.ArrayFillAll, "Fill", new CiVar(TypeParam0, "value")) { IsMutator = true };
	public static readonly CiMethod ArrayFillPart = new CiMethod(CiCallType.Normal, VoidType, CiId.ArrayFillPart, "Fill",
		new CiVar(TypeParam0, "value"),
		new CiVar(IntType, "startIndex"),
		new CiVar(IntType, "count")) { IsMutator = true };
	public static readonly CiMethod CollectionSortAll = new CiMethod(CiCallType.Normal, VoidType, CiId.CollectionSortAll, "Sort") { Visibility = CiVisibility.NumericElementType, IsMutator = true };
	public static readonly CiMethod CollectionSortPart = new CiMethod(CiCallType.Normal, VoidType, CiId.CollectionSortPart, "Sort", new CiVar(IntType, "startIndex"), new CiVar(IntType, "count")) { Visibility = CiVisibility.NumericElementType, IsMutator = true };
	public static readonly CiMethodGroup CollectionSort = new CiMethodGroup(CollectionSortAll, CollectionSortPart) { Visibility = CiVisibility.NumericElementType };
	public static readonly CiClass ArrayPtrClass = new CiClass(CiCallType.Normal, "ArrayPtr",
		ArrayBinarySearchPart,
		ArrayFillPart,
		CollectionSortPart) { TypeParameterCount = 1 };
	public static readonly CiMethod CollectionCopyTo = new CiMethod(CiCallType.Normal, VoidType, CiId.CollectionCopyTo, "CopyTo",
		new CiVar(IntType, "sourceIndex"),
		new CiVar(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = TypeParam0 }, "destinationArray"),
		new CiVar(IntType, "destinationIndex"),
		new CiVar(IntType, "count"));
	public static readonly CiClass ArrayStorageClass = new CiClass(CiCallType.Normal, "ArrayStorage",
		new CiMethodGroup(ArrayBinarySearchAll, ArrayBinarySearchPart) { Visibility = CiVisibility.NumericElementType },
		new CiMethodGroup(ArrayFillAll, ArrayFillPart),
		ArrayLength,
		CollectionSort) { Parent = ArrayPtrClass, TypeParameterCount = 1 };
	public static readonly CiClassType ReadOnlyByteArrayPtrType = new CiClassType { Class = ArrayPtrClass, TypeArg0 = ByteType };
	public static readonly CiClassType ReadWriteByteArrayPtrType = new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = ByteType };
	public static readonly CiMember CollectionCount = new CiMember(UIntType, "Count");
	public static readonly CiMethod CollectionClear = new CiMethod(CiCallType.Normal, VoidType, CiId.CollectionClear, "Clear") { IsMutator = true };
	public static readonly CiMethod ListAdd = new CiMethod(CiCallType.Normal, VoidType, CiId.ListAdd, "Add", new CiVar(TypeParam0NotFinal, "value")) { IsMutator = true };
	public static readonly CiMethod ListContains = new CiMethod(CiCallType.Normal, BoolType, CiId.ListContains, "Contains", new CiVar(TypeParam0, "value"));
	public static readonly CiMethod ListInsert = new CiMethod(CiCallType.Normal, VoidType, CiId.ListInsert, "Insert", new CiVar(UIntType, "index"), new CiVar(TypeParam0NotFinal, "value")) { IsMutator = true };
	public static readonly CiMethod ListRemoveAt = new CiMethod(CiCallType.Normal, VoidType, CiId.ListRemoveAt, "RemoveAt", new CiVar(IntType, "index")) { IsMutator = true };
	public static readonly CiMethod ListRemoveRange = new CiMethod(CiCallType.Normal, VoidType, CiId.ListRemoveRange, "RemoveRange", new CiVar(IntType, "index"), new CiVar(IntType, "count")) { IsMutator = true };
	public static readonly CiClass ListClass = new CiClass(CiCallType.Normal, "List",
		ListAdd,
		CollectionClear,
		ListContains,
		CollectionCount,
		CollectionCopyTo,
		ListInsert,
		ListRemoveAt,
		ListRemoveRange,
		CollectionSort) { TypeParameterCount = 1 };
	public static readonly CiMethod QueueDequeue = new CiMethod(CiCallType.Normal, TypeParam0, CiId.QueueDequeue, "Dequeue") { IsMutator = true };
	public static readonly CiMethod QueueEnqueue = new CiMethod(CiCallType.Normal, VoidType, CiId.QueueEnqueue, "Enqueue", new CiVar(TypeParam0, "value")) { IsMutator = true };
	public static readonly CiMethod QueuePeek = new CiMethod(CiCallType.Normal, TypeParam0, CiId.QueuePeek, "Peek");
	public static readonly CiClass QueueClass = new CiClass(CiCallType.Normal, "Queue",
		CollectionClear,
		CollectionCount,
		QueueDequeue,
		QueueEnqueue,
		QueuePeek) { TypeParameterCount = 1 };
	public static readonly CiMethod StackPeek = new CiMethod(CiCallType.Normal, TypeParam0, CiId.StackPeek, "Peek");
	public static readonly CiMethod StackPush = new CiMethod(CiCallType.Normal, VoidType, CiId.StackPush, "Push", new CiVar(TypeParam0, "value")) { IsMutator = true };
	public static readonly CiMethod StackPop = new CiMethod(CiCallType.Normal, TypeParam0, CiId.StackPop, "Pop") { IsMutator = true };
	public static readonly CiClass StackClass = new CiClass(CiCallType.Normal, "Stack",
		CollectionClear,
		CollectionCount,
		StackPeek,
		StackPush,
		StackPop) { TypeParameterCount = 1 };
	public static readonly CiMethod HashSetAdd = new CiMethod(CiCallType.Normal, VoidType, CiId.HashSetAdd, "Add", new CiVar(TypeParam0, "value")) { IsMutator = true };
	public static readonly CiMethod HashSetContains = new CiMethod(CiCallType.Normal, BoolType, CiId.HashSetContains, "Contains", new CiVar(TypeParam0, "value"));
	public static readonly CiMethod HashSetRemove = new CiMethod(CiCallType.Normal, VoidType, CiId.HashSetRemove, "Remove", new CiVar(TypeParam0, "value")) { IsMutator = true };
	public static readonly CiClass HashSetClass = new CiClass(CiCallType.Normal, "HashSet",
		HashSetAdd,
		CollectionClear,
		CollectionCount,
		HashSetContains,
		HashSetRemove) { TypeParameterCount = 1 };
	public static readonly CiMethod DictionaryAdd = new CiMethod(CiCallType.Normal, VoidType, CiId.DictionaryAdd, "Add", new CiVar(TypeParam0, "key")) { Visibility = CiVisibility.FinalValueType, IsMutator = true };
	public static readonly CiMethod DictionaryContainsKey = new CiMethod(CiCallType.Normal, BoolType, CiId.DictionaryContainsKey, "ContainsKey", new CiVar(TypeParam0, "key"));
	public static readonly CiMethod DictionaryRemove = new CiMethod(CiCallType.Normal, VoidType, CiId.DictionaryRemove, "Remove", new CiVar(TypeParam0, "key")) { IsMutator = true };
	public static readonly CiClass DictionaryClass = new CiClass(CiCallType.Normal, "Dictionary",
		DictionaryAdd,
		CollectionClear,
		CollectionCount,
		DictionaryContainsKey,
		DictionaryRemove) { TypeParameterCount = 2 };
	public static readonly CiClass SortedDictionaryClass = new CiClass { Name = "SortedDictionary", TypeParameterCount = 2 };
	public static readonly CiClass OrderedDictionaryClass = new CiClass { Name = "OrderedDictionary", TypeParameterCount = 2 };
	public static readonly CiMethod ConsoleWrite = new CiMethod(CiCallType.Static, VoidType, CiId.ConsoleWrite, "Write", new CiVar(PrintableType, "value"));
	public static readonly CiMethod ConsoleWriteLine = new CiMethod(CiCallType.Static, VoidType, CiId.ConsoleWriteLine, "WriteLine", new CiVar(PrintableType, "value") { Value = new CiLiteralString("") });
	public static readonly CiClass ConsoleBase = new CiClass(CiCallType.Static, "ConsoleBase",
		ConsoleWrite,
		ConsoleWriteLine);
	public static readonly CiMember ConsoleError = new CiMember(ConsoleBase, "Error");
	public static readonly CiClass ConsoleClass = new CiClass(CiCallType.Static, "Console",
		ConsoleError);
	public static readonly CiMethod UTF8GetByteCount = new CiMethod(CiCallType.Normal, IntType, CiId.UTF8GetByteCount, "GetByteCount", new CiVar(StringPtrType, "str"));
	public static readonly CiMethod UTF8GetBytes = new CiMethod(CiCallType.Normal, VoidType, CiId.UTF8GetBytes, "GetBytes", new CiVar(StringPtrType, "str"), new CiVar(ReadWriteByteArrayPtrType, "bytes"), new CiVar(IntType, "byteIndex"));
	public static readonly CiMethod UTF8GetString = new CiMethod(CiCallType.Normal, StringStorageType, CiId.UTF8GetString, "GetString", new CiVar(ReadOnlyByteArrayPtrType, "bytes"), new CiVar(IntType, "offset"), new CiVar(IntType, "length")); // TODO: UIntType
	public static readonly CiClass UTF8EncodingClass = new CiClass(CiCallType.Sealed, "UTF8Encoding",
		UTF8GetByteCount,
		UTF8GetBytes,
		UTF8GetString);
	public static readonly CiClass EncodingClass = new CiClass(CiCallType.Static, "Encoding");
	public static readonly CiMethod EnvironmentGetEnvironmentVariable = new CiMethod(CiCallType.Static, StringPtrType, CiId.EnvironmentGetEnvironmentVariable, "GetEnvironmentVariable", new CiVar(StringPtrType, "name"));
	public static readonly CiClass EnvironmentClass = new CiClass(CiCallType.Static, "Environment", EnvironmentGetEnvironmentVariable);
	public static readonly CiConst RegexOptionsNone = new CiConst("None", 0);
	public static readonly CiEnum RegexOptionsEnum = new CiEnumFlags { Name = "RegexOptions" };
	public static readonly CiMethod RegexCompile = new CiMethod(CiCallType.Static, null /* filled later to avoid cyclic reference */, CiId.RegexCompile, "Compile", new CiVar(StringPtrType, "pattern"), new CiVar(RegexOptionsEnum, "options") { Value = RegexOptionsNone });
	public static readonly CiMethod RegexEscape = new CiMethod(CiCallType.Static, StringStorageType, CiId.RegexEscape, "Escape", new CiVar(StringPtrType, "str"));
	public static readonly CiMethod RegexIsMatchStr = new CiMethod(CiCallType.Static, BoolType, CiId.RegexIsMatchStr, "IsMatch", new CiVar(StringPtrType, "input"), new CiVar(StringPtrType, "pattern"), new CiVar(RegexOptionsEnum, "options") { Value = RegexOptionsNone });
	public static readonly CiMethod RegexIsMatchRegex = new CiMethod(CiCallType.Normal, BoolType, CiId.RegexIsMatchRegex, "IsMatch", new CiVar(StringPtrType, "input"));
	public static readonly CiClass RegexClass = new CiClass(CiCallType.Sealed, "Regex",
		RegexCompile,
		RegexEscape,
		new CiMethodGroup(RegexIsMatchStr, RegexIsMatchRegex));
	public static readonly CiMethod MatchFindStr = new CiMethod(CiCallType.Normal, BoolType, CiId.MatchFindStr, "Find", new CiVar(StringPtrType, "input"), new CiVar(StringPtrType, "pattern"), new CiVar(RegexOptionsEnum, "options") { Value = RegexOptionsNone }) { IsMutator = true };
	public static readonly CiMethod MatchFindRegex = new CiMethod(CiCallType.Normal, BoolType, CiId.MatchFindRegex, "Find", new CiVar(StringPtrType, "input"), new CiVar(new CiClassType { Class = RegexClass }, "pattern")) { IsMutator = true };
	public static readonly CiMember MatchStart = new CiMember(IntType, "Start");
	public static readonly CiMember MatchEnd = new CiMember(IntType, "End");
	public static readonly CiMember MatchLength = new CiMember(UIntType, "Length");
	public static readonly CiMember MatchValue = new CiMember(StringPtrType, "Value");
	public static readonly CiMethod MatchGetCapture = new CiMethod(CiCallType.Normal, StringPtrType, CiId.MatchGetCapture, "GetCapture", new CiVar(UIntType, "group"));
	public static readonly CiClass MatchClass = new CiClass(CiCallType.Sealed, "Match",
		new CiMethodGroup(MatchFindStr, MatchFindRegex),
		MatchStart,
		MatchEnd,
		MatchGetCapture,
		MatchLength,
		MatchValue);
	public static readonly CiMember MathNaN = new CiMember(FloatType, "NaN");
	public static readonly CiMember MathNegativeInfinity = new CiMember(FloatType, "NegativeInfinity");
	public static readonly CiMember MathPositiveInfinity = new CiMember(FloatType, "PositiveInfinity");
	public static readonly CiMethod MathCeiling = new CiMethod(CiCallType.Static, FloatIntType, CiId.MathCeiling, "Ceiling", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathFusedMultiplyAdd = new CiMethod(CiCallType.Static, FloatType, CiId.MathFusedMultiplyAdd, "FusedMultiplyAdd", new CiVar(DoubleType, "x"), new CiVar(DoubleType, "y"), new CiVar(DoubleType, "z"));
	public static readonly CiMethod MathIsFinite = new CiMethod(CiCallType.Static, BoolType, CiId.MathIsFinite, "IsFinite", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathIsInfinity = new CiMethod(CiCallType.Static, BoolType, CiId.MathIsInfinity, "IsInfinity", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathIsNaN = new CiMethod(CiCallType.Static, BoolType, CiId.MathIsNaN, "IsNaN", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathLog2 = new CiMethod(CiCallType.Static, FloatType, CiId.MathLog2, "Log2", new CiVar(DoubleType, "a"));
	public static readonly CiMethod MathTruncate = new CiMethod(CiCallType.Static, FloatIntType, CiId.MathTruncate, "Truncate", new CiVar(DoubleType, "a"));
	public static readonly CiClass MathClass = new CiClass(CiCallType.Static, "Math",
		new CiMethod(CiCallType.Static, FloatType, CiId.MathAcos, "Acos", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathAsin, "Asin", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathAtan, "Atan", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathAtan2, "Atan2", new CiVar(DoubleType, "y"), new CiVar(DoubleType, "x")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathCbrt, "Cbrt", new CiVar(DoubleType, "a")),
		MathCeiling,
		new CiMethod(CiCallType.Static, FloatType, CiId.MathCos, "Cos", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathCosh, "Cosh", new CiVar(DoubleType, "a")),
		new CiConst("E", Math.E),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathExp, "Exp", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatIntType, CiId.MathFloor, "Floor", new CiVar(DoubleType, "a")),
		MathFusedMultiplyAdd,
		MathIsFinite,
		MathIsInfinity,
		MathIsNaN,
		new CiMethod(CiCallType.Static, FloatType, CiId.MathLog, "Log", new CiVar(DoubleType, "a")),
		MathLog2,
		new CiMethod(CiCallType.Static, FloatType, CiId.MathLog10, "Log10", new CiVar(DoubleType, "a")),
		MathNaN,
		MathNegativeInfinity,
		new CiConst("PI", Math.PI),
		MathPositiveInfinity,
		new CiMethod(CiCallType.Static, FloatType, CiId.MathPow, "Pow", new CiVar(DoubleType, "x"), new CiVar(DoubleType, "y")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathSin, "Sin", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathSinh, "Sinh", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathSqrt, "Sqrt", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathTan, "Tan", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, FloatType, CiId.MathTanh, "Tanh", new CiVar(DoubleType, "a")),
		MathTruncate);
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
		ArrayPtrClass.Add(CollectionCopyTo); // cyclic reference
		Add(ListClass);
		Add(QueueClass);
		Add(StackClass);
		Add(HashSetClass);
		Add(DictionaryClass);
		Add(SortedDictionaryClass);
		SortedDictionaryClass.Parent = DictionaryClass;
		Add(OrderedDictionaryClass);
		OrderedDictionaryClass.Parent = DictionaryClass;
		Add(ConsoleClass);
		ConsoleClass.Parent = ConsoleBase;
		EncodingClass.Add(new CiMember(UTF8EncodingClass, "UTF8"));
		Add(EncodingClass);
		Add(EnvironmentClass);
		AddEnumValue(RegexOptionsEnum, RegexOptionsNone);
		AddEnumValue(RegexOptionsEnum, new CiConst("IgnoreCase", 1));
		AddEnumValue(RegexOptionsEnum, new CiConst("Multiline", 2));
		AddEnumValue(RegexOptionsEnum, new CiConst("Singleline", 16));
		Add(RegexOptionsEnum);
		RegexCompile.Type = new CiDynamicPtrType { Class = RegexClass };
		Add(RegexClass);
		Add(MatchClass);
		Add(MathClass);
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
