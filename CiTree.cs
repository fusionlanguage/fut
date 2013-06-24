// CiTree.cs - Ci object model
//
// Copyright (C) 2011-2013  Piotr Fusik
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
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci
{

public enum CiPriority
{
	CondExpr,
	CondOr,
	CondAnd,
	Or,
	Xor,
	And,
	Equality,
	Ordering,
	Shift,
	Additive,
	Multiplicative,
	Prefix,
	Postfix
}

public abstract class CiDocInline
{
}

public class CiDocText : CiDocInline
{
	public string Text;
}

public class CiDocCode : CiDocInline
{
	public string Text;
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

public interface ICiSymbolVisitor
{
	void Visit(CiEnum symbol);
	void Visit(CiConst symbol);
	void Visit(CiField symbol);
	void Visit(CiMethod symbol);
	void Visit(CiClass symbol);
	void Visit(CiDelegate symbol);
}

public enum CiVisibility
{
	Dead,
	Private,
	Internal,
	Public
}

public abstract class CiSymbol
{
	public CiCodeDoc Documentation;
	public CiVisibility Visibility;
	public string Name;
	public virtual void Accept(ICiSymbolVisitor v) { throw new NotImplementedException(this.ToString()); }
}

public interface ICiTypeVisitor
{
	CiType Visit(CiUnknownType type);
	CiType Visit(CiStringStorageType type);
	CiType Visit(CiClassType type);
	CiType Visit(CiArrayType type);
	CiType Visit(CiArrayStorageType type);
	CiType Visit(CiDelegate type);
}

public class CiType : CiSymbol
{
	public static readonly CiType Null = new CiType { Name = "null" };
	public static readonly CiType Void = new CiType { Name = "void" };
	public virtual Type DotNetType { get { throw new NotSupportedException("No corresponding .NET type"); } }
	public virtual CiType BaseType { get { return this; } }
	public virtual int ArrayLevel { get { return 0; } }
	public virtual CiType Ptr { get { return null; } }
	public virtual CiSymbol LookupMember(string name)
	{
		throw new ParseException("{0} has no members", this.GetType());
	}
	public virtual CiType Accept(ICiTypeVisitor v) { return this; }
	public virtual bool Equals(CiType obj) { return this == obj; }
}

public class CiUnknownType : CiType
{
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
}

public class CiBoolType : CiType
{
	private CiBoolType() { }
	public static readonly CiBoolType Value = new CiBoolType { Name = "bool" };
	public override Type DotNetType { get { return typeof(bool); } }
}

public class CiByteType : CiType
{
	private CiByteType() { }
	public static readonly CiByteType Value = new CiByteType { Name = "byte" };
	public override Type DotNetType { get { return typeof(byte); } }
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "SByte": return CiLibrary.SByteProperty;
		default: throw new ParseException("No member {0} in byte", name);
		}
	}
}

public class CiIntType : CiType
{
	private CiIntType() { }
	public static readonly CiIntType Value = new CiIntType { Name = "int" };
	public override Type DotNetType { get { return typeof(int); } }
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "LowByte": return CiLibrary.LowByteProperty;
		case "MulDiv": return CiLibrary.MulDivMethod;
		default: throw new ParseException("No member {0} in int", name);
		}
	}
}

public abstract class CiStringType : CiType
{
	public override Type DotNetType { get { return typeof(string); } }
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "Length": return CiLibrary.StringLengthProperty;
		case "Substring": return CiLibrary.SubstringMethod;
		// CharAt is available only via bracket indexing
		default: throw new ParseException("No member {0} in string", name);
		}
	}
}

public class CiStringPtrType : CiStringType
{
	private CiStringPtrType() { }
	public static readonly CiStringPtrType Value = new CiStringPtrType { Name = "string" };
}

public class CiStringStorageType : CiStringType
{
	public CiExpr LengthExpr;
	public int Length;
	public override bool Equals(CiType obj)
	{
		CiStringStorageType that = obj as CiStringStorageType;
		return that != null && this.Length == that.Length;
	}
	public override CiType Ptr { get { return CiStringPtrType.Value; } }
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
}

public enum PtrWritability
{
	Unknown,
	ReadOnly,
	ReadWrite
}

public interface ICiPtrType
{
	PtrWritability Writability { get; set; }
	HashSet<ICiPtrType> Sources { get; }
}

public abstract class CiClassType : CiType
{
	public CiClass Class;
	public override CiSymbol LookupMember(string name)
	{
		return this.Class.Members.Lookup(name);
	}
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
}

public class CiClassPtrType : CiClassType, ICiPtrType
{
	PtrWritability _writability = PtrWritability.Unknown;
	public PtrWritability Writability { get { return this._writability; } set { this._writability = value; } }
	readonly HashSet<ICiPtrType> _sources = new HashSet<ICiPtrType>();
	public HashSet<ICiPtrType> Sources { get { return this._sources; } }
	public override bool Equals(CiType obj)
	{
		CiClassPtrType that = obj as CiClassPtrType;
		return that != null && this.Class == that.Class;
	}
}

public class CiClassStorageType : CiClassType
{
	public override bool Equals(CiType obj)
	{
		CiClassStorageType that = obj as CiClassStorageType;
		return that != null && this.Class == that.Class;
	}
	public override CiType Ptr { get { return new CiClassPtrType { Class = this.Class }; } }
}

public abstract class CiArrayType : CiType
{
	public CiType ElementType;
	public override CiType BaseType { get { return this.ElementType.BaseType; } }
	public override int ArrayLevel { get { return 1 + this.ElementType.ArrayLevel; } }
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "CopyTo":
			if (this.ElementType == CiByteType.Value)
				return CiLibrary.ArrayCopyToMethod;
			throw new ParseException("CopyTo available only for byte arrays");
		case "ToString":
			if (this.ElementType == CiByteType.Value)
				return CiLibrary.ArrayToStringMethod;
			throw new ParseException("ToString available only for byte arrays");
		default:
			throw new ParseException("No member {0} in array", name);
		}
	}
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
}

public class CiArrayPtrType : CiArrayType, ICiPtrType
{
	PtrWritability _writability = PtrWritability.Unknown;
	public PtrWritability Writability { get { return this._writability; } set { this._writability = value; } }
	readonly HashSet<ICiPtrType> _sources = new HashSet<ICiPtrType>();
	public HashSet<ICiPtrType> Sources { get { return this._sources; } }
	public static readonly CiArrayPtrType WritableByteArray = new CiArrayPtrType { ElementType = CiByteType.Value };
	public override bool Equals(CiType obj)
	{
		CiArrayPtrType that = obj as CiArrayPtrType;
		return that != null && this.ElementType.Equals(that.ElementType);
	}
}

public class CiArrayStorageType : CiArrayType
{
	public CiExpr LengthExpr;
	public int Length;
	public override CiType Ptr { get { return new CiArrayPtrType { ElementType = this.ElementType }; } }
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "Clear":
			if (this.ElementType == CiByteType.Value || this.ElementType == CiIntType.Value)
				return CiLibrary.ArrayStorageClearMethod;
			throw new ParseException("Clear available only for byte and int arrays");
		case "Length": return new CiConst { Type = CiIntType.Value, Value = this.Length };
		default: return base.LookupMember(name);
		}
	}
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
}

public class CiLibrary
{
	public static readonly CiProperty LowByteProperty = new CiProperty { Name = "LowByte", Type = CiByteType.Value };
	public static readonly CiProperty SByteProperty = new CiProperty { Name = "SByte", Type = CiIntType.Value };
	public static readonly CiMethod MulDivMethod = new CiMethod(
		CiIntType.Value, "MulDiv",
		new CiParam(CiIntType.Value, "numerator"),
		new CiParam(CiIntType.Value, "denominator"));
	public static readonly CiProperty StringLengthProperty = new CiProperty { Name = "Length", Type = CiIntType.Value };
	public static readonly CiMethod CharAtMethod = new CiMethod(
		CiIntType.Value, "CharAt",
		new CiParam(CiIntType.Value, "index"));
	public static readonly CiMethod SubstringMethod = new CiMethod(
		CiStringPtrType.Value, "Substring",
		new CiParam(CiIntType.Value, "startIndex"),
		new CiParam(CiIntType.Value, "length"));
	public static readonly CiMethod ArrayCopyToMethod = new CiMethod(
		CiType.Void, "CopyTo",
		new CiParam(CiIntType.Value, "sourceIndex"),
		new CiParam(CiArrayPtrType.WritableByteArray, "destinationArray"),
		new CiParam(CiIntType.Value, "destinationIndex"),
		new CiParam(CiIntType.Value, "length"));
	public static readonly CiMethod ArrayToStringMethod = new CiMethod(
		CiStringPtrType.Value, "ToString",
		new CiParam(CiIntType.Value, "startIndex"),
		new CiParam(CiIntType.Value, "length"));
	public static readonly CiMethod ArrayStorageClearMethod = new CiMethod(
		CiType.Void, "Clear") { IsMutator = true };
}

public class CiUnknownSymbol : CiSymbol
{
}

public class CiEnumValue : CiSymbol
{
	public CiEnum Type;
}

public class CiEnum : CiType
{
	public CiEnumValue[] Values;
	public override CiSymbol LookupMember(string name)
	{
		CiEnumValue value = this.Values.SingleOrDefault(v => v.Name == name);
		if (value == null)
			throw new ParseException("{0} not found in enum {1}", name, this.Name);
		return value;
	}
	public override void Accept(ICiSymbolVisitor v) { v.Visit(this); }
}

public class CiField : CiSymbol
{
	public CiClass Class;
	public CiType Type;
	public override void Accept(ICiSymbolVisitor v) { v.Visit(this); }
}

public class CiProperty : CiSymbol
{
	public CiType Type;
	public override void Accept(ICiSymbolVisitor v) { }
}

public interface ICiStatementVisitor
{
	void Visit(CiBlock statement);
	void Visit(CiConst statement);
	void Visit(CiVar statement);
	void Visit(CiExpr statement);
	void Visit(CiAssign statement);
	void Visit(CiDelete statement);
	void Visit(CiBreak statement);
	void Visit(CiContinue statement);
	void Visit(CiDoWhile statement);
	void Visit(CiFor statement);
	void Visit(CiIf statement);
	void Visit(CiNativeBlock statement);
	void Visit(CiReturn statement);
	void Visit(CiSwitch statement);
	void Visit(CiThrow statement);
	void Visit(CiWhile statement);
}

public interface ICiStatement
{
	bool CompletesNormally { get; }
	void Accept(ICiStatementVisitor v);
}

public class CiConst : CiSymbol, ICiStatement
{
	public CiClass Class;
	public CiType Type;
	public object Value;
	public string GlobalName;
	public bool Is7Bit;
	public bool CurrentlyResolving;
	public bool CompletesNormally { get { return true; } }
	public override void Accept(ICiSymbolVisitor v) { v.Visit(this); }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiVar : CiSymbol, ICiStatement
{
	public CiType Type;
	public CiExpr InitialValue;
	public bool WriteInitialValue; // C89 only
	public bool CompletesNormally { get { return true; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiBinaryResource : CiSymbol
{
	public byte[] Content;
	public CiArrayStorageType Type;
}

public class CiParam : CiVar
{
	public CiParam() { }
	public CiParam(CiType type, string name)
	{
		this.Type = type;
		this.Name = name;
	}
}

public abstract class CiMaybeAssign
{
	public abstract CiType Type { get; }
}

public interface ICiExprVisitor
{
	CiExpr Visit(CiSymbolAccess expr);
	CiExpr Visit(CiUnknownMemberAccess expr);
	CiExpr Visit(CiIndexAccess expr);
	CiExpr Visit(CiMethodCall expr);
	CiExpr Visit(CiUnaryExpr expr);
	CiExpr Visit(CiCondNotExpr expr);
	CiExpr Visit(CiPostfixExpr expr);
	CiExpr Visit(CiBinaryExpr expr);
	CiExpr Visit(CiBoolBinaryExpr expr);
	CiExpr Visit(CiCondExpr expr);
	CiExpr Visit(CiBinaryResourceExpr expr);
	CiExpr Visit(CiNewExpr expr);
}

public abstract class CiExpr : CiMaybeAssign
{
	public virtual bool IsConst(object value) { return false; }
	public abstract bool HasSideEffect { get; }
	public virtual CiExpr Accept(ICiExprVisitor v) { return this; }
}

public class CiConstExpr : CiExpr
{
	public object Value;
	public CiConstExpr(object value)
	{
		this.Value = value;
	}
	public CiConstExpr(int value)
	{
		this.Value = value >= 0 && value <= 255 ? (byte) value : (object) value;
	}
	public override CiType Type
	{
		get
		{
			if (this.Value is bool) return CiBoolType.Value;
			if (this.Value is byte) return CiByteType.Value;
			if (this.Value is int) return CiIntType.Value;
			if (this.Value is string) return CiStringPtrType.Value;
			if (this.Value is CiEnumValue) return ((CiEnumValue) this.Value).Type;
			if (this.Value == null) return CiType.Null;
			throw new NotImplementedException();
		}
	}
	public override bool IsConst(object value) { return object.Equals(this.Value, value); }
	public override bool HasSideEffect { get { return false; } }
}

public abstract class CiLValue : CiExpr
{
}

public class CiSymbolAccess : CiExpr
{
	public CiSymbol Symbol;
	public override CiType Type { get { throw new NotSupportedException(); } }
	public override bool HasSideEffect { get { throw new NotSupportedException(); } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiConstAccess : CiExpr
{
	public CiConst Const;
	public override CiType Type { get { return this.Const.Type; } }
	public override bool HasSideEffect { get { return false; } }
}

public class CiVarAccess : CiLValue
{
	public CiVar Var;
	public override CiType Type { get { return this.Var.Type; } }
	public override bool HasSideEffect { get { return false; } }
}

public class CiUnknownMemberAccess : CiExpr
{
	public CiExpr Parent;
	public string Name;
	public override CiType Type { get { throw new NotSupportedException(); } }
	public override bool HasSideEffect { get { throw new NotSupportedException(); } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiFieldAccess : CiLValue
{
	public CiExpr Obj;
	public CiField Field;
	public override CiType Type { get { return this.Field.Type; } }
	public override bool HasSideEffect { get { return this.Obj.HasSideEffect; } }
}

public class CiPropertyAccess : CiExpr
{
	public CiExpr Obj;
	public CiProperty Property;
	public override CiType Type { get { return this.Property.Type; } }
	public override bool HasSideEffect { get { return this.Obj.HasSideEffect; } }
}

public class CiIndexAccess : CiExpr
{
	public CiExpr Parent;
	public CiExpr Index;
	public override CiType Type { get { throw new NotSupportedException(); } }
	public override bool HasSideEffect { get { throw new NotSupportedException(); } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiArrayAccess : CiLValue
{
	public CiExpr Array;
	public CiExpr Index;
	public override CiType Type { get { return ((CiArrayType) this.Array.Type).ElementType; } }
	public override bool HasSideEffect { get { return this.Array.HasSideEffect || this.Index.HasSideEffect; } }
}

public class CiMethodCall : CiExpr, ICiStatement
{
	public CiExpr Obj;
	public CiMethod Method;
	public CiExpr[] Arguments;
	public CiDelegate Signature { get { return this.Method != null ? this.Method.Signature : (CiDelegate) this.Obj.Type; } }
	public override CiType Type { get { return this.Signature.ReturnType; } }
	public override bool HasSideEffect { get { return true; } }
	public bool CompletesNormally { get { return true; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiUnaryExpr : CiExpr
{
	public CiToken Op;
	public CiExpr Inner;
	public override CiType Type { get { return CiIntType.Value; } }
	public override bool HasSideEffect { get { return this.Op == CiToken.Increment || this.Op == CiToken.Decrement || this.Inner.HasSideEffect; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiCondNotExpr : CiExpr
{
	public CiExpr Inner;
	public override CiType Type { get { return CiBoolType.Value; } }
	public override bool HasSideEffect { get { return this.Inner.HasSideEffect; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiPostfixExpr : CiExpr, ICiStatement
{
	public CiExpr Inner;
	public CiToken Op;
	public override CiType Type { get { return CiIntType.Value; } }
	public override bool HasSideEffect { get { return true; } }
	public bool CompletesNormally { get { return true; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiBinaryExpr : CiExpr
{
	public CiExpr Left;
	public CiToken Op;
	public CiExpr Right;
	public override CiType Type { get { return CiIntType.Value; } }
	public override bool HasSideEffect { get { return this.Left.HasSideEffect || this.Right.HasSideEffect; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiBoolBinaryExpr : CiBinaryExpr
{
	public override CiType Type { get { return CiBoolType.Value; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiCondExpr : CiExpr
{
	public CiExpr Cond;
	public CiType ResultType;
	public CiExpr OnTrue;
	public CiExpr OnFalse;
	public override CiType Type { get { return this.ResultType; } }
	public override bool HasSideEffect { get { return this.Cond.HasSideEffect || this.OnTrue.HasSideEffect || this.OnFalse.HasSideEffect; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiBinaryResourceExpr : CiExpr
{
	public CiExpr NameExpr;
	public CiBinaryResource Resource;
	public override CiType Type { get { return this.Resource.Type; } }
	public override bool HasSideEffect { get { return false; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiNewExpr : CiExpr
{
	public CiType NewType;
	public override CiType Type { get { return this.NewType.Ptr; } }
	public override bool HasSideEffect { get { return true; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiCoercion : CiExpr
{
	public CiType ResultType;
	public CiMaybeAssign Inner;
	public override CiType Type { get { return this.ResultType; } }
	public override bool HasSideEffect { get { return ((CiExpr) this.Inner).HasSideEffect; } } // TODO: Assign
}

public class CiAssign : CiMaybeAssign, ICiStatement
{
	public CiExpr Target;
	public CiToken Op;
	public CiMaybeAssign Source;
	public override CiType Type { get { return this.Target.Type; } }
	public bool CompletesNormally { get { return true; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiDelete : ICiStatement
{
	public CiExpr Expr;
	public bool CompletesNormally { get { return true; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public abstract class CiCondCompletionStatement : ICiStatement
{
	public bool CompletesNormally { get; set; }
	public abstract void Accept(ICiStatementVisitor v);
}

public abstract class CiLoop : CiCondCompletionStatement
{
	public CiExpr Cond;
	public ICiStatement Body;
}

public class CiBlock : CiCondCompletionStatement
{
	public ICiStatement[] Statements;
	public override void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiBreak : ICiStatement
{
	public bool CompletesNormally { get { return false; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiContinue : ICiStatement
{
	public bool CompletesNormally { get { return false; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiDoWhile : CiLoop
{
	public override void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiFor : CiLoop
{
	public SymbolTable Symbols;
	public ICiStatement Init;
	public ICiStatement Advance;
	public override void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiIf : CiCondCompletionStatement
{
	public CiExpr Cond;
	public ICiStatement OnTrue;
	public ICiStatement OnFalse;
	public override void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiNativeBlock : ICiStatement
{
	public string Content;
	public bool CompletesNormally { get { return true; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiReturn : ICiStatement
{
	public CiExpr Value;
	public bool CompletesNormally { get { return false; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiCase
{
	public object[] Values;
	public ICiStatement[] Body;
	public bool Fallthrough;
	public CiExpr FallthroughTo;
}

public class CiSwitch : CiCondCompletionStatement
{
	public CiExpr Value;
	public CiCase[] Cases;
	public ICiStatement[] DefaultBody;
	public override void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiThrow : ICiStatement
{
	public CiExpr Message;
	public bool CompletesNormally { get { return false; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiWhile : CiLoop
{
	public override void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiDelegate : CiType
{
	public CiType ReturnType;
	public CiParam[] Params;
	public CiWriteStatus WriteStatus; // C only
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
	public override void Accept(ICiSymbolVisitor v) { v.Visit(this); }
}

public enum CiCallType
{
	Static,
	Normal,
	Abstract,
	Virtual,
	Override
}

public class CiMethod : CiSymbol
{
	public CiClass Class;
	public CiCallType CallType;
	public CiDelegate Signature;
	public CiParam This;
	public CiBlock Body;
	public bool Throws;
	public object ErrorReturnValue;
	public readonly HashSet<CiMethod> CalledBy = new HashSet<CiMethod>();
	public readonly HashSet<CiMethod> Calls = new HashSet<CiMethod>();
	public bool IsMutator;
	public CiMethod(CiType returnType, string name, params CiParam[] paramz)
	{
		this.Name = name;
		this.CallType = CiCallType.Normal;
		this.Signature = new CiDelegate { Name = name, ReturnType = returnType, Params = paramz };
	}
	public override void Accept(ICiSymbolVisitor v) { v.Visit(this); }
}

public enum CiWriteStatus
{
	NotYet,
	InProgress,
	Done
}

public class CiClass : CiSymbol
{
	public bool IsAbstract;
	public CiClass BaseClass;
	public SymbolTable Members;
	public CiMethod Constructor;
	public CiConst[] ConstArrays;
	public CiBinaryResource[] BinaryResources;
	public bool IsResolved;
	public string SourceFilename;
	public CiWriteStatus WriteStatus; // C, JS only
	public bool HasFields; // C only
	public bool Constructs; // C only
	public bool IsAllocated; // C only
	public override void Accept(ICiSymbolVisitor v) { v.Visit(this); }
}

public class CiUnknownClass : CiClass
{
}

public class CiProgram
{
	public SymbolTable Globals;
}

}
