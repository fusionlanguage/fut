// CiTree.cs - Ci object model
//
// Copyright (C) 2011-2019  Piotr Fusik
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
using System.Linq;
using System.Text;

namespace Foxoft.Ci
{

public enum CiVisibility
{
	Private,
	Internal,
	Protected,
	Public
}

public enum CiCallType
{
	Static,
	Normal,
	Abstract,
	Virtual,
	Override,
	Sealed
}

public enum CiPriority
{
	Statement,
	Assign,
	Cond,
	CondOr,
	CondAnd,
	Or,
	Xor,
	And,
	Equality,
	Rel,
	Shift,
	Add,
	Mul,
	Primary
}

public abstract class CiVisitor
{
	public abstract CiExpr Visit(CiCollection expr, CiPriority parent);
	public abstract CiExpr Visit(CiVar expr, CiPriority parent);
	public abstract CiExpr Visit(CiLiteral expr, CiPriority parent);
	public abstract CiExpr Visit(CiInterpolatedString expr, CiPriority parent);
	public abstract CiExpr Visit(CiSymbolReference expr, CiPriority parent);
	public abstract CiExpr Visit(CiPrefixExpr expr, CiPriority parent);
	public abstract CiExpr Visit(CiPostfixExpr expr, CiPriority parent);
	public abstract CiExpr Visit(CiBinaryExpr expr, CiPriority parent);
	public abstract CiExpr Visit(CiCondExpr expr, CiPriority parent);
	public abstract void Visit(CiConst statement);
	public abstract void Visit(CiExpr statement);
	public abstract void Visit(CiBlock statement);
	public abstract void Visit(CiBreak statement);
	public abstract void Visit(CiContinue statement);
	public abstract void Visit(CiDoWhile statement);
	public abstract void Visit(CiFor statement);
	public abstract void Visit(CiIf statement);
	public abstract void Visit(CiNative statement);
	public abstract void Visit(CiReturn statement);
	public abstract void Visit(CiSwitch statement);
	public abstract void Visit(CiThrow statement);
	public abstract void Visit(CiWhile statement);
}

public abstract class CiStatement
{
	public int Line;
	public abstract bool CompletesNormally { get; }
	public virtual void Accept(CiVisitor visitor)
	{
		throw new NotImplementedException(GetType().Name);
	}
}

public abstract class CiExpr : CiStatement
{
	public CiType Type;
	public override bool CompletesNormally { get { return true; } }
	public virtual bool IsIndexing { get { return false; } }
	public virtual CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		throw new NotImplementedException(GetType().Name);
	}
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
	public CiLiteral ToLiteral(object value) { return new CiLiteral(value) { Line = this.Line }; }
}

public class CiCollection : CiExpr
{
	public CiExpr[] Items;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public abstract class CiDocInline
{
	public string Text;
}

public class CiDocText : CiDocInline
{
}

public class CiDocCode : CiDocInline
{
}

public abstract class CiDocBlock
{
}

public class CiDocPara : CiDocBlock
{
	public CiDocInline[] Children;
}

public class CiDocList : CiDocBlock
{
	public CiDocPara[] Items;
}

public class CiCodeDoc
{
	public CiDocPara Summary;
	public CiDocBlock[] Details;
}

public abstract class CiSymbol : CiExpr
{
	public string Name;
	public CiScope Parent;
	public CiCodeDoc Documentation = null;
	public override string ToString() { return this.Name; }
}

public class CiScope : CiSymbol, IEnumerable
{
	public string Filename;
	protected readonly OrderedDictionary Dict = new OrderedDictionary();

	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.Dict.Values.GetEnumerator();
	}

	public int Count { get { return this.Dict.Count; } }

	public CiClass ParentClass
	{
		get
		{
			for (CiScope scope = this; scope != null; scope = scope.Parent) {
				if (scope is CiClass klass)
					return klass;
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

	static bool IsPrivate(CiMember member)
	{
		return member != null && member.Visibility == CiVisibility.Private;
	}

	static bool IsOverrideOf(CiMethod derived, CiMethod baseMethod)
	{
		if (derived == null || baseMethod == null)
			return false;
		if (derived.CallType != CiCallType.Override && derived.CallType != CiCallType.Sealed)
			return false;
		if (baseMethod.CallType == CiCallType.Static || baseMethod.CallType == CiCallType.Normal)
			return false;
		// TODO: check parameter and return type
		baseMethod.Calls.Add(derived);
		return true;
	}

	public void Add(CiSymbol symbol)
	{
		string name = symbol.Name;
		for (CiScope scope = this; scope != null; scope = scope.Parent) {
			if (scope.Dict[name] is CiSymbol duplicate
			 && (scope == this || (!IsPrivate(duplicate as CiMember) && !IsOverrideOf(symbol as CiMethod, duplicate as CiMethod)))) {
				CiScope symbolScope = symbol as CiScope ?? this;
				CiScope duplicateScope = duplicate as CiScope ?? scope;
				throw new CiException(symbolScope.Filename, symbol.Line,
					string.Format("Duplicate symbol {0}, already defined in {1} line {2}", name, duplicateScope.Filename, duplicate.Line));
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
}

public class CiMember : CiNamedValue
{
	public CiVisibility Visibility;
	public virtual bool IsStatic() { throw new NotImplementedException(this.GetType().Name); }
}

public class CiVar : CiNamedValue
{
	public CiVar()
	{
	}
	public CiVar(CiType type, string name)
	{
		this.Type = type;
		this.Name = name;
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public class CiConst : CiMember
{
	public CiMethodBase InMethod;
	public CiVisitStatus VisitStatus;
	public CiConst()
	{
	}
	public CiConst(string name, object value)
	{
		this.Name = name;
		this.Value = new CiLiteral(value);
		this.Type = this.Value.Type;
		this.VisitStatus = CiVisitStatus.Done;
	}
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
	public override bool IsStatic() { return true; }
}

public class CiLiteral : CiExpr
{
	public readonly object Value;

	public CiLiteral(object value)
	{
		switch (value) {
		case null:
			this.Type = CiSystem.NullType;
			break;
		case long l:
			if (l >= int.MinValue && l <= int.MaxValue)
				this.Type = new CiRangeType((int) l, (int) l);
			else
				this.Type = CiSystem.LongType;
			break;
		case bool _:
			this.Type = CiSystem.BoolType;
			break;
		case double _:
			this.Type = CiSystem.DoubleType;
			break;
		case string _:
			this.Type = CiSystem.StringPtrType;
			break;
		default:
			throw new NotImplementedException(value.GetType().Name);
		}
		this.Value = value;
	}

	public bool IsDefaultValue
	{
		get
		{
			switch (this.Value) {
			case null:
				return true;
			case long l:
				return l == 0;
			case bool b:
				return b == false;
			case double d:
				return BitConverter.DoubleToInt64Bits(d) == 0; // rule out -0.0
			case string _:
				return false;
			default:
				throw new NotImplementedException(this.Value.GetType().Name);
			}
		}
	}

	public static readonly CiLiteral False = new CiLiteral(false);
	public static readonly CiLiteral True = new CiLiteral(true);
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
	public override string ToString() { return this.Value == null ? "null" : this.Value.ToString(); }
}

public class CiInterpolatedPart
{
	public string Prefix;
	public CiExpr Argument;
	public CiExpr WidthExpr;
	public int Width;
	public char Format;
}

public class CiInterpolatedString : CiExpr
{
	public CiInterpolatedPart[] Parts;
	public CiInterpolatedString()
	{
		this.Type = CiSystem.StringStorageType;
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
	public override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append("$\"");
		foreach (CiInterpolatedPart part in this.Parts) {
			sb.Append(part.Prefix.Replace("{", "{{"));
			if (part.Argument != null) {
				sb.Append('{');
				sb.Append(part.Argument);
				if (part.WidthExpr != null) {
					sb.Append(',');
					sb.Append(part.WidthExpr);
				}
				if (part.Format != ' ') {
					sb.Append(':');
					sb.Append(part.Format);
				}
				sb.Append('}');
			}
		}
		sb.Append('"');
		return sb.ToString();
	}
}

public class CiSymbolReference : CiExpr
{
	public string Name;
	public CiSymbol Symbol;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
	public override string ToString() { return this.Name; }
}

public abstract class CiUnaryExpr : CiExpr
{
	public CiToken Op;
	public CiExpr Inner;
}

public class CiPrefixExpr : CiUnaryExpr
{
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public class CiPostfixExpr : CiUnaryExpr
{
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public class CiBinaryExpr : CiExpr
{
	public CiExpr Left;
	public CiToken Op;
	public CiExpr Right;
	public CiExpr[] RightCollection { get { return ((CiCollection) this.Right).Items; } }
	public override bool IsIndexing { get { return this.Op == CiToken.LeftBracket; } }
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
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
			case CiToken.LeftParenthesis:
				return "(";
			default:
				throw new ArgumentException(this.Op.ToString());
			}
		}
	}
	public override string ToString()
	{
		switch (this.Op) {
		case CiToken.Dot:
			return this.Left + "." + this.Right;
		case CiToken.LeftBracket:
			return this.Left + "[" + this.Right + "]";
		default:
			return "(" + this.Left + " " + this.OpString + " " + this.Right + ")";
		}
	}
}

public class CiCondExpr : CiExpr
{
	public CiExpr Cond;
	public CiExpr OnTrue;
	public CiExpr OnFalse;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
	public override string ToString() { return "(" + this.Cond + " ? " + this.OnTrue + " : " + this.OnFalse + ")"; }
}

public abstract class CiCondCompletionStatement : CiScope
{
	bool CompletesNormallyValue;
	public override bool CompletesNormally { get { return this.CompletesNormallyValue; } }
	public void SetCompletesNormally(bool value) { this.CompletesNormallyValue = value; }
}

public abstract class CiLoop : CiCondCompletionStatement
{
	public CiExpr Cond;
	public CiStatement Body;
}

public class CiBlock : CiCondCompletionStatement
{
	public CiStatement[] Statements;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiBreak : CiStatement
{
	public readonly CiCondCompletionStatement LoopOrSwitch;
	public CiBreak(CiCondCompletionStatement loopOrSwitch) { this.LoopOrSwitch = loopOrSwitch; }
	public override bool CompletesNormally { get { return false; } }
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiContinue : CiStatement
{
	public readonly CiLoop Loop;
	public CiContinue(CiLoop loop) { this.Loop = loop; }
	public override bool CompletesNormally { get { return false; } }
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiDoWhile : CiLoop
{
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiFor : CiLoop
{
	public CiExpr Init;
	public CiExpr Advance;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiIf : CiCondCompletionStatement
{
	public CiExpr Cond;
	public CiStatement OnTrue;
	public CiStatement OnFalse;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiNative : CiStatement
{
	public string Content;
	public override bool CompletesNormally { get { return true; } }
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiReturn : CiStatement
{
	public CiExpr Value;
	public override bool CompletesNormally { get { return false; } }
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiGotoDefault : CiExpr
{
}

public class CiCase
{
	public CiExpr[] Values;
	public CiStatement[] Body;
	public CiExpr Fallthrough;
}

public class CiSwitch : CiCondCompletionStatement
{
	public CiExpr Value;
	public CiCase[] Cases;
	public CiStatement[] DefaultBody;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiThrow : CiStatement
{
	public CiExpr Message;
	public override bool CompletesNormally { get { return false; } }
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiWhile : CiLoop
{
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiField : CiMember
{
	public override bool IsStatic() { return false; }
}

public class CiMethodBase : CiMember
{
	public bool Throws;
	public CiStatement Body;
	public bool IsLive = false;
	public readonly HashSet<CiMethod> Calls = new HashSet<CiMethod>();
}

public class CiMethod : CiMethodBase
{
	public CiCallType CallType;
	public bool IsMutator;
	public readonly CiScope Parameters = new CiScope();
	public CiMethod()
	{
	}
	public CiMethod(CiCallType callType, CiType type, string name, params CiVar[] parameters)
	{
		this.CallType = callType;
		this.Type = type;
		this.Name = name;
		this.Parameters.AddRange(parameters);
	}
	public override bool IsStatic() { return this.CallType == CiCallType.Static; }
	public bool IsAbstractOrVirtual() { return this.CallType == CiCallType.Abstract || this.CallType == CiCallType.Virtual; }
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

public class CiType : CiScope
{
	public virtual string ArrayString { get { return ""; } }
	public virtual bool IsAssignableFrom(CiType right) { return this == right; }
	public virtual CiType BaseType { get { return this; } }
	public virtual CiType StorageType { get { return this; } }
	public virtual CiType PtrOrSelf { get { return this; } }
}

public abstract class CiContainerType : CiType
{
	public bool IsPublic;
}

public class CiEnum : CiContainerType
{
	public bool IsFlags;
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
	public override string ToString() { return this.Name + "()"; }
	public override bool IsAssignableFrom(CiType right) { return false; }
	public override CiType PtrOrSelf { get { return new CiClassPtrType { Class = this, Modifier = CiToken.ExclamationMark }; } }
	public bool AddsVirtualMethods() { return this.Methods.Any(method => method.IsAbstractOrVirtual()); }
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
}

public class CiNumericType : CiType
{
}

public class CiIntegerType : CiNumericType
{
	public override bool IsAssignableFrom(CiType right) { return right is CiIntegerType; }
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

	public override string ToString() { return "(" + this.Min + " .. " + this.Max + ")"; }

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
		return right is CiRangeType range && this.Min <= range.Min && this.Max >= range.Max;
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

	public int VariableBits { get { return GetMask(this.Min ^ this.Max); } }

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
}

public class CiFloatingType : CiNumericType
{
	public override bool IsAssignableFrom(CiType right)
	{
		return right is CiNumericType;
	}
}

public class CiStringPtrType : CiStringType
{
	public override bool IsAssignableFrom(CiType right)
	{
		return right == CiSystem.NullType || right is CiStringType;
	}
}

public class CiStringStorageType : CiStringType
{
	public override string ToString() { return "string()"; }
	public override CiType PtrOrSelf { get { return CiSystem.StringPtrType; } }
	public override bool IsAssignableFrom(CiType right)
	{
		return right is CiStringType;
	}
}

public class CiClassPtrType : CiType
{
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

	public override CiSymbol TryLookup(string name)
	{
		return this.Class.TryLookup(name);
	}

	public override bool IsAssignableFrom(CiType right)
	{
		if (right == CiSystem.NullType)
			return true;
		CiClass klass = right as CiClass;
		if (klass == null) {
			if (!(right is CiClassPtrType ptr))
				return false;
			klass = ptr.Class;
		}
		while (klass != Class) {
			klass = klass.Parent as CiClass;
			if (klass == null)
				return false;
		}
		// TODO: modifiers
		return true;
	}

	public override CiType PtrOrSelf
	{
		get {
			return this.Modifier == CiToken.Hash ? this.Class.PtrOrSelf : this;
		}
	}

	public override bool Equals(object obj)
	{
		return obj is CiClassPtrType that && this.Class == that.Class && this.Modifier == that.Modifier;
	}

	public override int GetHashCode()
	{
		return this.Class.GetHashCode() ^ this.Modifier.GetHashCode();
	}
}

public abstract class CiArrayType : CiType
{
	public CiType ElementType;
	public override string ToString() {
		return this.BaseType + this.ArrayString + this.ElementType.ArrayString;
	}
	public override CiSymbol TryLookup(string name)
	{
		if (name == "CopyTo") {
			return new CiMethod(CiCallType.Normal, null, "CopyTo" ,
				new CiVar(CiSystem.IntType, "sourceIndex"),
				new CiVar(this.PtrOrSelf , "destinationArray"),
				new CiVar(CiSystem.IntType, "destinationIndex"),
				new CiVar(CiSystem.IntType, "length"));
		}
		return null;
	}
	public override CiType BaseType { get { return this.ElementType.BaseType; } }
	public bool IsByteArray() {
		return this.ElementType is CiRangeType range
			&& ((range.Min >= 0 && range.Max <= byte.MaxValue)
				|| (range.Min >= sbyte.MinValue && range.Max <= sbyte.MaxValue));
	}
}

public class CiArrayPtrType : CiArrayType
{
	public CiToken Modifier;

	public override string ArrayString
	{
		get {
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

	public override string ArrayString { get { return "[" + this.Length + "]"; } }
	public override CiSymbol TryLookup(string name)
	{
		switch (name) {
		case "Fill":
			return new CiMethod(CiCallType.Normal, null, "Fill", new CiVar(this.ElementType, "value"));
		case "Length":
			return CiSystem.ArrayLength;
		default:
			return base.TryLookup(name);
		}
	}
	public override bool IsAssignableFrom(CiType right) { return false; }
	public override CiType StorageType { get { return this.ElementType.StorageType; } }
	public override CiType PtrOrSelf { get { return new CiArrayPtrType { ElementType = this.ElementType, Modifier = CiToken.ExclamationMark }; } }

	public override bool Equals(object obj)
	{
		return obj is CiArrayStorageType that && this.ElementType == that.ElementType && this.Length == that.Length;
	}

	public override int GetHashCode()
	{
		return this.ElementType.GetHashCode() ^ this.Length.GetHashCode();
	}
}

public class CiSystem : CiScope
{
	public static readonly CiType NullType = new CiType();
	public static readonly CiIntegerType IntType = new CiIntegerType { Name = "int" };
	public static readonly CiRangeType UIntType = new CiRangeType(0, int.MaxValue) { Name = "uint" };
	public static readonly CiIntegerType LongType = new CiIntegerType { Name = "long" };
	public static readonly CiRangeType ByteType = new CiRangeType(0, 0xff) { Name = "byte" };
	public static readonly CiRangeType ShortType = new CiRangeType(-0x8000, 0x7fff) { Name = "short" };
	public static readonly CiRangeType UShortType = new CiRangeType(0, 0xffff) { Name = "ushort" };
	public static readonly CiRangeType Minus1Type = new CiRangeType(-1, int.MaxValue);
	public static readonly CiFloatingType FloatType = new CiFloatingType { Name = "float" };
	public static readonly CiFloatingType DoubleType = new CiFloatingType { Name = "double" };
	public static readonly CiRangeType CharType = new CiRangeType(-0x80, 0xffff);
	public static readonly CiEnum BoolType = new CiEnum { Name = "bool" };
	public static readonly CiStringType StringPtrType = new CiStringPtrType { Name = "string" };
	public static readonly CiStringStorageType StringStorageType = new CiStringStorageType();
	public static readonly CiMember StringLength = new CiMember { Name = "Length", Type = UIntType };
	public static readonly CiMethod StringContains = new CiMethod(CiCallType.Normal, BoolType, "Contains", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringEndsWith = new CiMethod(CiCallType.Normal, BoolType, "EndsWith", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringIndexOf = new CiMethod(CiCallType.Normal, Minus1Type, "IndexOf", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringLastIndexOf = new CiMethod(CiCallType.Normal, Minus1Type, "LastIndexOf", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringStartsWith = new CiMethod(CiCallType.Normal, BoolType, "StartsWith", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod StringSubstring = new CiMethod(CiCallType.Normal, StringStorageType, "Substring", new CiVar(IntType, "offset"), new CiVar(IntType, "length") { Value = new CiLiteral(-1L) } ); // TODO: UIntType
	public static readonly CiMember ArrayLength = new CiMember { Name = "Length", Type = UIntType };
	public static readonly CiMethod ConsoleWrite = new CiMethod(CiCallType.Static, null, "Write", new CiVar(StringPtrType, "value"));
	public static readonly CiMethod ConsoleWriteLine = new CiMethod(CiCallType.Static, null, "WriteLine", new CiVar(StringPtrType, "value") { Value = new CiLiteral("") });
	public static readonly CiClass ConsoleClass = new CiClass(CiCallType.Static, "Console",
		ConsoleWrite,
		ConsoleWriteLine);
	public static readonly CiArrayPtrType ByteArrayPtrType = new CiArrayPtrType { ElementType = ByteType, Modifier = CiToken.EndOfFile };
	public static readonly CiMethod UTF8GetString = new CiMethod(CiCallType.Normal, StringStorageType, "GetString", new CiVar(ByteArrayPtrType, "bytes"), new CiVar(IntType, "offset"), new CiVar(IntType, "length")); // TODO: UIntType
	public static readonly CiClass UTF8EncodingClass = new CiClass(CiCallType.Normal, "UTF8Encoding", UTF8GetString);
	public static readonly CiClass EncodingClass = new CiClass(CiCallType.Static, "Encoding");
	public static readonly CiClass MathClass = new CiClass(CiCallType.Static, "Math",
		new CiMethod(CiCallType.Static, DoubleType, "Acos", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Asin", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Atan", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Atan2", new CiVar(DoubleType, "y"), new CiVar(DoubleType, "x")),
		new CiMethod(CiCallType.Static, DoubleType, "Cbrt", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Ceiling", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Cos", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Cosh", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Exp", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Floor", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "FusedMultiplyAdd", new CiVar(DoubleType, "x"), new CiVar(DoubleType, "y"), new CiVar(DoubleType, "z")),
		new CiMethod(CiCallType.Static, DoubleType, "Log", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Log2", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Log10", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Pow", new CiVar(DoubleType, "x"), new CiVar(DoubleType, "y")),
		new CiMethod(CiCallType.Static, DoubleType, "Sin", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Sinh", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Sqrt", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Tan", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Tanh", new CiVar(DoubleType, "a")),
		new CiMethod(CiCallType.Static, DoubleType, "Truncate", new CiVar(DoubleType, "a")));

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
		Add(ConsoleClass);
		EncodingClass.Add(new CiMember { Name = "UTF8", Type = UTF8EncodingClass });
		Add(EncodingClass);
		MathClass.Add(new CiConst("E", Math.E));
		MathClass.Add(new CiConst("PI", Math.PI));
		Add(MathClass);
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
