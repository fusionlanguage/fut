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
	public abstract void VisitLambdaExpr(CiLambdaExpr expr);
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
	public abstract void VisitEnumValue(CiConst konst, CiConst previous);
}

public abstract class CiStatement
{
	public int Line;
	public abstract bool CompletesNormally();
	public abstract void Accept(CiVisitor visitor);
}

public abstract class CiExpr : CiStatement
{
	public CiType Type;
	public override bool CompletesNormally() => true;
	public virtual bool IsIndexing() => false;
	public virtual bool IsLiteralZero() => false;
	public virtual bool IsConstEnum() => false;
	public virtual int IntValue() => throw new NotImplementedException(GetType().Name);
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

public abstract class CiSymbol : CiExpr
{
	public CiId Id = CiId.None;
	public string Name;
	public CiSymbol Next;
	public CiScope Parent;
	public CiCodeDoc Documentation = null;
	public override string ToString() => this.Name;
}

public abstract class CiScope : CiSymbol
{
	readonly Dictionary<string, CiSymbol> Dict = new Dictionary<string, CiSymbol>();
	public CiSymbol First = null;
	CiSymbol Last;

	public int Count() => this.Dict.Count;

	public CiVar FirstParameter() => (CiVar) this.First;

	public CiContainerType GetContainer()
	{
		for (CiScope scope = this; scope != null; scope = scope.Parent) {
			if (scope is CiContainerType container)
				return container;
		}
		throw new InvalidOperationException();
	}

	public CiSymbol TryShallowLookup(string name)
	{
		this.Dict.TryGetValue(name, out CiSymbol result);
		return result;
	}

	public virtual CiSymbol TryLookup(string name)
	{
		for (CiScope scope = this; scope != null; scope = scope.Parent) {
			if (scope.Dict.TryGetValue(name, out CiSymbol result))
				return result;
		}
		return null;
	}

	public bool Contains(CiSymbol symbol) => this.Dict.ContainsKey(symbol.Name);

	public void Add(CiSymbol symbol)
	{
		this.Dict.Add(symbol.Name, symbol);
		symbol.Next = null;
		symbol.Parent = this;
		if (this.First == null)
			this.First = symbol;
		else
			this.Last.Next = symbol;
		this.Last = symbol;
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
	public bool IsAssignableStorage() => this.Type is CiStorageType && !(this.Type is CiArrayStorageType) && this.Value is CiLiteralNull;
}

public class CiMember : CiNamedValue
{
	public CiVisibility Visibility;
	public CiMember()
	{
	}
	public CiMember(CiType type, CiId id, string name)
	{
		this.Visibility = CiVisibility.Public;
		this.Type = type;
		this.Id = id;
		this.Name = name;
	}
	public virtual bool IsStatic() => throw new NotImplementedException(GetType().Name);
}

public class CiVar : CiNamedValue
{
	public bool IsAssigned = false;
	public CiVar()
	{
	}
	public CiVar(CiType type, string name, CiExpr defaultValue = null)
	{
		this.Type = type;
		this.Name = name;
		this.Value = defaultValue;
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitVar(this);
		return this;
	}
	public CiVar NextParameter() => (CiVar) this.Next;
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
	public override bool IsStatic() => true;
}

public abstract class CiLiteral : CiExpr
{
	public abstract bool IsDefaultValue();
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
	public override bool IsLiteralZero() => this.Value == 0;
	public override int IntValue() => (int) this.Value;
	public override bool IsDefaultValue() => this.Value == 0;
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
	public override bool IsDefaultValue() => BitConverter.DoubleToInt64Bits(this.Value) == 0; // rule out -0.0
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
	public override bool IsDefaultValue() => false;
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
	public override bool IsDefaultValue() => true;
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
	public override bool IsDefaultValue() => true;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralFalse();
		return this;
	}
	public override string ToString() => "false";
}

public class CiLiteralTrue : CiLiteralBool
{
	public override bool IsDefaultValue() => false;
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
	public override int IntValue() => this.Value;
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
	public override bool IsConstEnum() => this.Symbol.Parent is CiEnum;
	public override int IntValue() => ((CiConst) this.Symbol).Value.IntValue();
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
	public override bool IsConstEnum() => this.Type is CiEnumFlags && this.Inner.IsConstEnum(); // && this.Op == CiToken.Tilde
	public override int IntValue()
	{
		Trace.Assert(this.Op == CiToken.Tilde);
		return ~this.Inner.IntValue();
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

public class CiLambdaExpr : CiScope
{
	public CiExpr Body;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLambdaExpr(this);
		return this;
	}
}

public abstract class CiCondCompletionStatement : CiScope
{
	bool CompletesNormallyValue;
	public override bool CompletesNormally() => this.CompletesNormallyValue;
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
	public override bool CompletesNormally() => !(this.Cond is CiLiteralFalse);
	public override void Accept(CiVisitor visitor) { visitor.VisitAssert(this); }
}

public class CiBreak : CiStatement
{
	public readonly CiCondCompletionStatement LoopOrSwitch;
	public CiBreak(CiCondCompletionStatement loopOrSwitch) { this.LoopOrSwitch = loopOrSwitch; }
	public override bool CompletesNormally() => false;
	public override void Accept(CiVisitor visitor) { visitor.VisitBreak(this); }
}

public class CiContinue : CiStatement
{
	public readonly CiLoop Loop;
	public CiContinue(CiLoop loop) { this.Loop = loop; }
	public override bool CompletesNormally() => false;
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
	public CiVar GetVar() => this.FirstParameter();
	public CiVar GetValueVar() => this.FirstParameter().NextParameter();
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
	public override bool CompletesNormally() => this.Body.CompletesNormally();
	public override void Accept(CiVisitor visitor) { visitor.VisitLock(this); }
}

public class CiNative : CiStatement
{
	public string Content;
	public override bool CompletesNormally() => true;
	public override void Accept(CiVisitor visitor) { visitor.VisitNative(this); }
}

public class CiReturn : CiStatement
{
	public CiExpr Value;
	public override bool CompletesNormally() => false;
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
	public override bool CompletesNormally() => false;
	public override void Accept(CiVisitor visitor) { visitor.VisitThrow(this); }
}

public class CiWhile : CiLoop
{
	public override void Accept(CiVisitor visitor) { visitor.VisitWhile(this); }
}

public class CiField : CiMember
{
	public override bool IsStatic() => false;
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
	CiMethod(CiVisibility visibility, CiCallType callType, CiType type, CiId id, string name)
	{
		this.Visibility = visibility;
		this.CallType = callType;
		this.Type = type;
		this.Id = id;
		this.Name = name;
	}
	public static CiMethod CreateNormal(CiVisibility visibility, CiType type, CiId id, string name, CiVar param0 = null, CiVar param1 = null, CiVar param2 = null, CiVar param3 = null)
	{
		CiMethod result = new CiMethod(visibility, CiCallType.Normal, type, id, name);
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
	public static CiMethod CreateStatic(CiType type, CiId id, string name, CiVar param0, CiVar param1 = null, CiVar param2 = null)
	{
		CiMethod result = CreateNormal(CiVisibility.Public, type, id, name, param0, param1, param2);
		result.CallType = CiCallType.Static;
		return result;
	}
	public static CiMethod CreateMutator(CiVisibility visibility, CiType type, CiId id, string name, CiVar param0 = null, CiVar param1 = null, CiVar param2 = null)
	{
		CiMethod result = CreateNormal(visibility, type, id, name, param0, param1, param2);
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
	public readonly CiMethod[] Methods = new CiMethod[2];
	public CiMethodGroup(CiMethod method0, CiMethod method1)
	{
		this.Visibility = CiVisibility.Public;
		this.Name = method0.Name;
		this.Methods[0] = method0;
		this.Methods[1] = method1;
	}
}

public class CiType : CiScope
{
	public virtual string GetArraySuffix() => "";
	public virtual bool IsAssignableFrom(CiType right) => this == right;
	public virtual bool EqualsType(CiType right) => this == right;
	public virtual bool IsNullable() => false;
	public virtual CiType GetBaseType() => this;
	public virtual CiType GetStorageType() => this;
	public virtual CiType GetPtrOrSelf() => this;
	public virtual bool IsFinal() => false;
	public virtual bool IsArray() => false;
}

public abstract class CiContainerType : CiType
{
	public bool IsPublic;
	public string Filename;
}

public class CiEnum : CiContainerType
{
	public bool HasExplicitValue = false;
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
		Add(new CiVar(GetPtrOrSelf(), "this")); // shadows "this" in base class
	}

	public CiClass(CiCallType callType, CiId id, string name)
	{
		this.CallType = callType;
		this.Id = id;
		this.Name = name;
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

public class CiFloatingType : CiNumericType
{
	public override bool IsAssignableFrom(CiType right) => right is CiNumericType;
}

public class CiStringType : CiType
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
	public override bool IsNullable() => true;
	public override bool IsAssignableFrom(CiType right) => right == CiSystem.NullType || right is CiStringType;
}

public class CiStringStorageType : CiStringType
{
	public override bool IsNullable() => false;
	public override CiType GetPtrOrSelf() => CiSystem.StringPtrType;
	public override bool IsAssignableFrom(CiType right) => right is CiStringType;
}

public class CiClassType : CiType
{
	public CiClass Class;
	public CiType TypeArg0;
	public CiType TypeArg1;
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
	public CiExpr LengthExpr;
	public int Length;
	public bool PtrTaken = false;

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
	public static readonly CiStringType StringPtrType = new CiStringType { Name = "string" };
	public static readonly CiStringStorageType StringStorageType = new CiStringStorageType { Name = "string()" };
	public static readonly CiMember StringLength = new CiMember(UIntType, CiId.StringLength, "Length");
	public static readonly CiMethod StringContains = CiMethod.CreateNormal(CiVisibility.Public, BoolType, CiId.StringContains, "Contains", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringEndsWith = CiMethod.CreateNormal(CiVisibility.Public, BoolType, CiId.StringEndsWith, "EndsWith", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringIndexOf = CiMethod.CreateNormal(CiVisibility.Public, Minus1Type, CiId.StringIndexOf, "IndexOf", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringLastIndexOf = CiMethod.CreateNormal(CiVisibility.Public, Minus1Type, CiId.StringLastIndexOf, "LastIndexOf", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringStartsWith = CiMethod.CreateNormal(CiVisibility.Public, BoolType, CiId.StringStartsWith, "StartsWith", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringSubstring = CiMethod.CreateNormal(CiVisibility.Public, StringStorageType, CiId.StringSubstring, "Substring", new CiVar(IntType, "offset"), new CiVar(IntType, "length", new CiLiteralLong(-1L))); // TODO: UIntType
	public static readonly CiType PrintableType = new CiPrintableType { Name = "printable" };
	public static readonly CiClass ArrayPtrClass = new CiClass(CiCallType.Normal, CiId.ArrayPtrClass, "ArrayPtr") { TypeParameterCount = 1 };
	public static readonly CiClass ArrayStorageClass = new CiClass(CiCallType.Normal, CiId.ArrayStorageClass, "ArrayStorage") { Parent = ArrayPtrClass, TypeParameterCount = 1 };
	public static readonly CiClass ConsoleBase = new CiClass(CiCallType.Static, CiId.None, "ConsoleBase");
	public static readonly CiMember ConsoleError = new CiMember(ConsoleBase, CiId.ConsoleError, "Error");
	static readonly CiClass LockClass = new CiClass(CiCallType.Sealed, CiId.LockClass, "Lock");
	public static readonly CiReadWriteClassType LockPtrType = new CiReadWriteClassType { Class = LockClass };
	public static readonly CiSymbol BasePtr = new CiVar { Name = "base" };

	CiClass AddCollection(CiId id, string name, int typeParameterCount, CiId clearId, CiId countId)
	{
		CiClass result = new CiClass(CiCallType.Normal, id, name) { TypeParameterCount = typeParameterCount };
		result.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, clearId, "Clear"));
		result.Add(new CiMember(UIntType, countId, "Count"));
		Add(result);
		return result;
	}

	void AddDictionary(CiId id, string name, CiId clearId, CiId containsKeyId, CiId countId, CiId removeId)
	{
		CiClass dict = AddCollection(id, name, 2, clearId, countId);
		dict.Add(CiMethod.CreateMutator(CiVisibility.FinalValueType, VoidType, CiId.DictionaryAdd, "Add", new CiVar(TypeParam0, "key")));
		dict.Add(CiMethod.CreateNormal(CiVisibility.Public, BoolType, containsKeyId, "ContainsKey", new CiVar(TypeParam0, "key")));
		dict.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, removeId, "Remove", new CiVar(TypeParam0, "key")));
	}

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
		Add(new CiRangeType(-0x8000, 0x7fff) { Name = "short" });
		Add(new CiRangeType(0, 0xffff) { Name = "ushort" });
		Add(FloatType);
		Add(DoubleType);
		Add(BoolType);
		Add(StringPtrType);
		CiMethod arrayBinarySearchPart = CiMethod.CreateNormal(CiVisibility.NumericElementType, IntType, CiId.ArrayBinarySearchPart, "BinarySearch",
			new CiVar(TypeParam0, "value"), new CiVar(IntType, "startIndex"), new CiVar(IntType, "count"));
		ArrayPtrClass.Add(arrayBinarySearchPart);
		ArrayPtrClass.Add(CiMethod.CreateNormal(CiVisibility.Public, VoidType, CiId.ArrayCopyTo, "CopyTo", new CiVar(IntType, "sourceIndex"),
			new CiVar(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = TypeParam0 }, "destinationArray"), new CiVar(IntType, "destinationIndex"), new CiVar(IntType, "count")));
		CiMethod arrayFillPart = CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.ArrayFillPart, "Fill",
			new CiVar(TypeParam0, "value"), new CiVar(IntType, "startIndex"), new CiVar(IntType, "count"));
		ArrayPtrClass.Add(arrayFillPart);
		CiMethod arraySortPart = CiMethod.CreateMutator(CiVisibility.NumericElementType, VoidType, CiId.ArraySortPart, "Sort", new CiVar(IntType, "startIndex"), new CiVar(IntType, "count"));
		ArrayPtrClass.Add(arraySortPart);
		ArrayStorageClass.Add(new CiMethodGroup(CiMethod.CreateNormal(CiVisibility.NumericElementType, IntType, CiId.ArrayBinarySearchAll, "BinarySearch", new CiVar(TypeParam0, "value")),
			arrayBinarySearchPart) { Visibility = CiVisibility.NumericElementType });
		ArrayStorageClass.Add(new CiMethodGroup(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.ArrayFillAll, "Fill", new CiVar(TypeParam0, "value")),
			arrayFillPart));
		ArrayStorageClass.Add(new CiMember(UIntType, CiId.ArrayLength, "Length"));
		ArrayStorageClass.Add(new CiMethodGroup(
			CiMethod.CreateMutator(CiVisibility.NumericElementType, VoidType, CiId.ArraySortAll, "Sort"),
			arraySortPart) { Visibility = CiVisibility.NumericElementType });

		CiClass listClass = AddCollection(CiId.ListClass, "List", 1, CiId.ListClear, CiId.ListCount);
		listClass.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.ListAdd, "Add", new CiVar(TypeParam0NotFinal, "value")));
		listClass.Add(CiMethod.CreateNormal(CiVisibility.Public, BoolType, CiId.ListAny, "Any", new CiVar(TypeParam0Predicate, "predicate")));
		listClass.Add(CiMethod.CreateNormal(CiVisibility.Public, BoolType, CiId.ListContains, "Contains", new CiVar(TypeParam0, "value")));
		listClass.Add(CiMethod.CreateNormal(CiVisibility.Public, VoidType, CiId.ListCopyTo, "CopyTo", new CiVar(IntType, "sourceIndex"),
			new CiVar(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = TypeParam0 }, "destinationArray"), new CiVar(IntType, "destinationIndex"), new CiVar(IntType, "count")));
		listClass.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.ListInsert, "Insert", new CiVar(UIntType, "index"), new CiVar(TypeParam0NotFinal, "value")));
		listClass.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.ListRemoveAt, "RemoveAt", new CiVar(IntType, "index")));
		listClass.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.ListRemoveRange, "RemoveRange", new CiVar(IntType, "index"), new CiVar(IntType, "count")));
		listClass.Add(new CiMethodGroup(
			CiMethod.CreateMutator(CiVisibility.NumericElementType, VoidType, CiId.ListSortAll, "Sort"),
			CiMethod.CreateMutator(CiVisibility.NumericElementType, VoidType, CiId.ListSortPart, "Sort", new CiVar(IntType, "startIndex"), new CiVar(IntType, "count"))));
		CiClass queueClass = AddCollection(CiId.QueueClass, "Queue", 1, CiId.QueueClear, CiId.QueueCount);
		queueClass.Add(CiMethod.CreateMutator(CiVisibility.Public, TypeParam0, CiId.QueueDequeue, "Dequeue"));
		queueClass.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.QueueEnqueue, "Enqueue", new CiVar(TypeParam0, "value")));
		queueClass.Add(CiMethod.CreateNormal(CiVisibility.Public, TypeParam0, CiId.QueuePeek, "Peek"));
		CiClass stackClass = AddCollection(CiId.StackClass, "Stack", 1, CiId.StackClear, CiId.StackCount);
		stackClass.Add(CiMethod.CreateNormal(CiVisibility.Public, TypeParam0, CiId.StackPeek, "Peek"));
		stackClass.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.StackPush, "Push", new CiVar(TypeParam0, "value")));
		stackClass.Add(CiMethod.CreateMutator(CiVisibility.Public, TypeParam0, CiId.StackPop, "Pop"));
		CiClass hashSetClass = AddCollection(CiId.HashSetClass, "HashSet", 1, CiId.HashSetClear, CiId.HashSetCount);
		hashSetClass.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.HashSetAdd, "Add", new CiVar(TypeParam0, "value")));
		hashSetClass.Add(CiMethod.CreateNormal(CiVisibility.Public, BoolType, CiId.HashSetContains, "Contains", new CiVar(TypeParam0, "value")));
		hashSetClass.Add(CiMethod.CreateMutator(CiVisibility.Public, VoidType, CiId.HashSetRemove, "Remove", new CiVar(TypeParam0, "value")));
		AddDictionary(CiId.DictionaryClass, "Dictionary", CiId.DictionaryClear, CiId.DictionaryContainsKey, CiId.DictionaryCount, CiId.DictionaryRemove);
		AddDictionary(CiId.SortedDictionaryClass, "SortedDictionary", CiId.SortedDictionaryClear, CiId.SortedDictionaryContainsKey, CiId.SortedDictionaryCount, CiId.SortedDictionaryRemove);
		AddDictionary(CiId.OrderedDictionaryClass, "OrderedDictionary", CiId.OrderedDictionaryClear, CiId.OrderedDictionaryContainsKey, CiId.OrderedDictionaryCount, CiId.OrderedDictionaryRemove);

		ConsoleBase.Add(CiMethod.CreateStatic(VoidType, CiId.ConsoleWrite, "Write", new CiVar(PrintableType, "value")));
		ConsoleBase.Add(CiMethod.CreateStatic(VoidType, CiId.ConsoleWriteLine, "WriteLine", new CiVar(PrintableType, "value", new CiLiteralString(""))));
		CiClass consoleClass = new CiClass(CiCallType.Static, CiId.None, "Console");
		consoleClass.Add(ConsoleError);
		Add(consoleClass);
		consoleClass.Parent = ConsoleBase;
		CiClass utf8EncodingClass = new CiClass(CiCallType.Sealed, CiId.None, "UTF8Encoding");
		utf8EncodingClass.Add(CiMethod.CreateNormal(CiVisibility.Public, IntType, CiId.UTF8GetByteCount, "GetByteCount", new CiVar(StringPtrType, "str")));
		utf8EncodingClass.Add(CiMethod.CreateNormal(CiVisibility.Public, VoidType, CiId.UTF8GetBytes, "GetBytes",
			new CiVar(StringPtrType, "str"), new CiVar(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = ByteType }, "bytes"), new CiVar(IntType, "byteIndex")));
		utf8EncodingClass.Add(CiMethod.CreateNormal(CiVisibility.Public, StringStorageType, CiId.UTF8GetString, "GetString",
			new CiVar(new CiClassType { Class = ArrayPtrClass, TypeArg0 = ByteType }, "bytes"), new CiVar(IntType, "offset"), new CiVar(IntType, "length"))); // TODO: UIntType
		CiClass encodingClass = new CiClass(CiCallType.Static, CiId.None, "Encoding");
		encodingClass.Add(new CiMember(utf8EncodingClass, CiId.None, "UTF8"));
		Add(encodingClass);
		CiClass environmentClass = new CiClass(CiCallType.Static, CiId.None, "Environment");
		environmentClass.Add(CiMethod.CreateStatic(StringPtrType, CiId.EnvironmentGetEnvironmentVariable, "GetEnvironmentVariable", new CiVar(StringPtrType, "name")));
		Add(environmentClass);
		CiEnum regexOptionsEnum = new CiEnumFlags { Name = "RegexOptions" };
		CiConst regexOptionsNone = new CiConst("None", 0);
		AddEnumValue(regexOptionsEnum, regexOptionsNone);
		AddEnumValue(regexOptionsEnum, new CiConst("IgnoreCase", 1));
		AddEnumValue(regexOptionsEnum, new CiConst("Multiline", 2));
		AddEnumValue(regexOptionsEnum, new CiConst("Singleline", 16));
		Add(regexOptionsEnum);
		CiClass regexClass = new CiClass(CiCallType.Sealed, CiId.RegexClass, "Regex");
		regexClass.Add(CiMethod.CreateStatic(StringStorageType, CiId.RegexEscape, "Escape", new CiVar(StringPtrType, "str")));
		regexClass.Add(new CiMethodGroup(
				CiMethod.CreateStatic(BoolType, CiId.RegexIsMatchStr, "IsMatch", new CiVar(StringPtrType, "input"), new CiVar(StringPtrType, "pattern"), new CiVar(regexOptionsEnum, "options", regexOptionsNone)),
				CiMethod.CreateNormal(CiVisibility.Public, BoolType, CiId.RegexIsMatchRegex, "IsMatch", new CiVar(StringPtrType, "input"))));
		regexClass.Add(CiMethod.CreateStatic(new CiDynamicPtrType { Class = regexClass }, CiId.RegexCompile, "Compile", new CiVar(StringPtrType, "pattern"), new CiVar(regexOptionsEnum, "options", regexOptionsNone)));
		Add(regexClass);
		CiClass matchClass = new CiClass(CiCallType.Sealed, CiId.MatchClass, "Match");
		matchClass.Add(new CiMethodGroup(
				CiMethod.CreateMutator(CiVisibility.Public, BoolType, CiId.MatchFindStr, "Find", new CiVar(StringPtrType, "input"), new CiVar(StringPtrType, "pattern"), new CiVar(regexOptionsEnum, "options", regexOptionsNone)),
				CiMethod.CreateMutator(CiVisibility.Public, BoolType, CiId.MatchFindRegex, "Find", new CiVar(StringPtrType, "input"), new CiVar(new CiClassType { Class = regexClass }, "pattern"))));
		matchClass.Add(new CiMember(IntType, CiId.MatchStart, "Start"));
		matchClass.Add(new CiMember(IntType, CiId.MatchEnd, "End"));
		matchClass.Add(CiMethod.CreateNormal(CiVisibility.Public, StringPtrType, CiId.MatchGetCapture, "GetCapture", new CiVar(UIntType, "group")));
		matchClass.Add(new CiMember(UIntType, CiId.MatchLength, "Length"));
		matchClass.Add(new CiMember(StringPtrType, CiId.MatchValue, "Value"));
		Add(matchClass);

		CiClass mathClass = new CiClass(CiCallType.Static, CiId.None, "Math");
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Acos", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Asin", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Atan", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Atan2", new CiVar(DoubleType, "y"), new CiVar(DoubleType, "x")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Cbrt", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatIntType, CiId.MathCeiling, "Ceiling", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Cos", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Cosh", new CiVar(DoubleType, "a")));
		mathClass.Add(new CiConst("E", Math.E));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Exp", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatIntType, CiId.MathMethod, "Floor", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathFusedMultiplyAdd, "FusedMultiplyAdd", new CiVar(DoubleType, "x"), new CiVar(DoubleType, "y"), new CiVar(DoubleType, "z")));
		mathClass.Add(CiMethod.CreateStatic(BoolType, CiId.MathIsFinite, "IsFinite", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(BoolType, CiId.MathIsInfinity, "IsInfinity", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(BoolType, CiId.MathIsNaN, "IsNaN", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Log", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathLog2, "Log2", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Log10", new CiVar(DoubleType, "a")));
		mathClass.Add(new CiMember(FloatType, CiId.MathNaN, "NaN"));
		mathClass.Add(new CiMember(FloatType, CiId.MathNegativeInfinity, "NegativeInfinity"));
		mathClass.Add(new CiConst("PI", Math.PI));
		mathClass.Add(new CiMember(FloatType, CiId.MathPositiveInfinity, "PositiveInfinity"));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Pow", new CiVar(DoubleType, "x"), new CiVar(DoubleType, "y")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Sin", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Sinh", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Sqrt", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Tan", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatType, CiId.MathMethod, "Tanh", new CiVar(DoubleType, "a")));
		mathClass.Add(CiMethod.CreateStatic(FloatIntType, CiId.MathTruncate, "Truncate", new CiVar(DoubleType, "a")));
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
