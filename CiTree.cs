// CiTree.cs - Ci object model
//
// Copyright (C) 2011-2014  Piotr Fusik
//
// This file is part of CiTo, see http://cito.sourceforge.net
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
	public abstract void Visit(CiDelete statement);
	public abstract void Visit(CiDoWhile statement);
	public abstract void Visit(CiFor statement);
	public abstract void Visit(CiIf statement);
	public abstract void Visit(CiReturn statement);
	public abstract void Visit(CiSwitch statement);
	public abstract void Visit(CiThrow statement);
	public abstract void Visit(CiWhile statement);
}

public abstract class CiStatement
{
	public int Line;
	public virtual void Accept(CiVisitor visitor)
	{
		throw new NotImplementedException(GetType().Name);
	}
}

public abstract class CiExpr : CiStatement
{
	public CiType Type;
	public virtual CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		throw new NotImplementedException(GetType().Name);
	}
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiCollection : CiExpr
{
	public CiExpr[] Items;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public abstract class CiSymbol : CiExpr
{
	public string Name;
}

public class CiScope : CiSymbol, IEnumerable
{
	public string Filename;
	public CiScope Parent;
	readonly OrderedDictionary Dict = new OrderedDictionary();

	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.Dict.Values.GetEnumerator();
	}

	public int Count { get { return this.Dict.Count; } }

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
		return true;
	}

	public void Add(CiSymbol symbol)
	{
		string name = symbol.Name;
		for (CiScope scope = this; scope != null; scope = scope.Parent) {
			object duplicateObj = scope.Dict[name];
			if (duplicateObj != null) {
				CiSymbol duplicate = (CiSymbol) duplicateObj;
				if (scope == this || (!IsPrivate(duplicateObj as CiMember) && !IsOverrideOf(symbol as CiMethod, duplicateObj as CiMethod))) {
					CiScope symbolScope = symbol as CiScope ?? this;
					CiScope duplicateScope = duplicate as CiScope ?? scope;
					throw new CiException(symbolScope.Filename, symbol.Line,
						string.Format("Duplicate symbol {0}, already defined in {1} line {2}", name, duplicateScope.Filename, duplicate.Line));
				}
			}
		}
		this.Dict.Add(name, symbol);
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
}

public class CiVar : CiNamedValue
{
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public class CiConst : CiMember
{
	public CiVisitStatus VisitStatus;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiLiteral : CiExpr
{
	public readonly object Value;
	public CiLiteral(object value)
	{
		if (value is long) {
			long i = (long) value;
			this.Type = new CiRangeType(i, i);
		}
		else if (value is double)
			this.Type = CiSystem.DoubleType;
		else if (value is string)
			this.Type = CiSystem.StringPtrType;
		else
			throw new NotImplementedException(value.GetType().Name);
		this.Value = value;
	}
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public class CiSymbolReference : CiExpr
{
	public string Name;
	public CiSymbol Symbol;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
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
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public class CiCondExpr : CiExpr
{
	public CiExpr Cond;
	public CiExpr OnTrue;
	public CiExpr OnFalse;
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent) { return visitor.Visit(this, parent); }
}

public abstract class CiLoop : CiStatement
{
	public CiExpr Cond;
	public CiStatement Body;
}

public class CiBlock : CiScope
{
	public CiStatement[] Statements;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiBreak : CiStatement
{
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiContinue : CiStatement
{
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiDelete : CiStatement
{
	public CiExpr Expr;
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

public class CiIf : CiStatement
{
	public CiExpr Cond;
	public CiStatement OnTrue;
	public CiStatement OnFalse;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiReturn : CiStatement
{
	public CiExpr Value;
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

public class CiSwitch : CiStatement
{
	public CiExpr Value;
	public CiCase[] Cases;
	public CiStatement[] DefaultBody;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiThrow : CiStatement
{
	public CiExpr Message;
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiWhile : CiLoop
{
	public override void Accept(CiVisitor visitor) { visitor.Visit(this); }
}

public class CiField : CiMember
{
}

public class CiMethodBase : CiMember
{
	public CiStatement Body;
}

public class CiMethod : CiMethodBase
{
	public CiCallType CallType;
	public bool IsMutator;
	public readonly CiScope Parameters = new CiScope();
	public bool Throws;
}

public abstract class CiType : CiScope
{
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
	public CiBlock Destructor;
	public CiConst[] Consts;
	public CiField[] Fields;
	public CiMethod[] Methods;
	public CiVisitStatus VisitStatus;
}

public class CiNumericType : CiType
{
}

public abstract class CiIntegerType : CiNumericType
{
	public abstract TypeCode TypeCode { get; }
}

public class CiIntType : CiIntegerType
{
	public override TypeCode TypeCode { get { return TypeCode.Int32; } }
}

public class CiLongType : CiIntegerType
{
	public override TypeCode TypeCode { get { return TypeCode.Int64; } }
}

public class CiRangeType : CiIntegerType
{
	public readonly long Min;
	public readonly long Max;
	public CiRangeType(long min, long max)
	{
		this.Min = min;
		this.Max = max;
	}
	public CiRangeType(long a, long b, long c, long d)
	{
		if (a > b) {
			long t = a;
			a = b;
			b = t;
		}
		if (c > d) {
			long t = c;
			c = d;
			d = t;
		}
		this.Min = a <= c ? a : c;
		this.Max = b >= d ? b : d;
	}
	public override TypeCode TypeCode
	{
		get
		{
			if (this.Min < int.MinValue || this.Max > int.MaxValue)
				return TypeCode.Int64;
			if (this.Min < 0) {
				if (this.Min < short.MinValue || this.Max > short.MaxValue)
					return TypeCode.Int32;
				if (this.Min < sbyte.MinValue || this.Max > sbyte.MaxValue)
					return TypeCode.Int16;
				return TypeCode.SByte;
			}
			if (this.Max > ushort.MaxValue)
				return TypeCode.Int32;
			if (this.Max > byte.MaxValue)
				return TypeCode.UInt16;
			return TypeCode.Byte;
		}
	}
}

public class CiStringType : CiType
{
	public override CiSymbol TryLookup(string name)
	{
		if (name == "Length")
			return CiSystem.StringLength;
		return null;
	}
}

public class CiClassPtrType : CiType
{
	public CiClass Class;
	public bool Mutable;
}

public abstract class CiArrayType : CiType
{
	public CiType ElementType;
}

public class CiArrayPtrType : CiArrayType
{
	public bool Mutable;
}

public class CiArrayStorageType : CiArrayType
{
	public int Length;
	public override CiSymbol TryLookup(string name)
	{
		// TODO: length
		return null;
	}
}

public class CiSystem : CiScope
{
	public static readonly CiIntType IntType = new CiIntType { Name = "int" };
	public static readonly CiRangeType UIntType = new CiRangeType(0, int.MaxValue) { Name = "uint" };
	public static readonly CiLongType LongType = new CiLongType { Name = "long" };
	public static readonly CiRangeType ByteType = new CiRangeType(0, 0xff) { Name = "byte" };
	public static readonly CiRangeType ShortType = new CiRangeType(-0x8000, 0x7fff) { Name = "short" };
	public static readonly CiRangeType UShortType = new CiRangeType(0, 0xffff) { Name = "ushort" };
	public static readonly CiNumericType FloatType = new CiNumericType { Name = "float" };
	public static readonly CiNumericType DoubleType = new CiNumericType { Name = "double" };
	public static readonly CiConst False = new CiConst { Name = "false" };
	public static readonly CiConst True = new CiConst { Name = "true" };
	public static readonly CiEnum BoolType = new CiEnum { Name = "bool" };
	public static readonly CiStringType StringPtrType = new CiStringType { Name = "string" };
	public static readonly CiStringType StringStorageType = new CiStringType();
	public static readonly CiMember StringLength = new CiMember { Name = "Length", Type = UIntType };

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
		BoolType.Add(False);
		BoolType.Add(True);
		Add(BoolType);
		Add(StringPtrType);
	}

	public static readonly CiSystem Value = new CiSystem();
}

public class CiProgram : CiScope
{
	public readonly List<CiClass> Classes = new List<CiClass>();
}

}
