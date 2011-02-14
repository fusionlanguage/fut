// CiTree.cs - Ci object model
//
// Copyright (C) 2011  Piotr Fusik
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
using System.Linq;

namespace Foxoft.Ci
{

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

public abstract class CiSymbol
{
	public CiCodeDoc Documentation;
	public bool IsPublic;
	public string Name;
}

public interface ICiTypeVisitor
{
	CiType Visit(CiUnknownType type);
	CiType Visit(CiStringStorageType type);
	CiType Visit(CiClassType type);
	CiType Visit(CiArrayType type);
	CiType Visit(CiArrayStorageType type);
}

public class CiType : CiSymbol
{
	public static readonly CiType Null = new CiType { Name = "null" };
	public static readonly CiType Void = new CiType { Name = "void" };
	public virtual Type DotNetType { get { throw new ApplicationException("No corresponding .NET type"); } }
	public virtual CiType BaseType { get { return this; } }
	public virtual int ArrayLevel { get { return 0; } }
	public virtual CiType Ptr { get { return null; } }
	public virtual CiSymbol LookupMember(string name)
	{
		throw new ParseException("{0} has no members", this.GetType());
	}
	public virtual CiType Accept(ICiTypeVisitor v) { return this; }
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
		case "SByte": return CiIntType.SByteProperty;
		default: throw new ParseException("No member {0} in byte", name);
		}
	}
}

public class CiIntType : CiType
{
	private CiIntType() { }
	public static readonly CiIntType Value = new CiIntType { Name = "int" };
	public override Type DotNetType { get { return typeof(int); } }
	public static readonly CiProperty LowByteProperty = new CiProperty { Name = "LowByte", Type = CiByteType.Value };
	// SByte defined here and not in CiByteType to avoid circular dependency between static initializers of CiByteType and CiIntType
	public static readonly CiProperty SByteProperty = new CiProperty { Name = "SByte", Type = CiIntType.Value };
	public static readonly CiFunction MulDivMethod = new CiFunction {
		Name = "MulDiv",
		ReturnType = CiIntType.Value,
		Params = new CiParam[] {
			new CiParam { Type = CiIntType.Value, Name = "numerator" },
			new CiParam { Type = CiIntType.Value, Name = "denominator" }
		}
	};
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "LowByte": return LowByteProperty;
		case "MulDiv": return MulDivMethod;
		default: throw new ParseException("No member {0} in int", name);
		}
	}
}

public abstract class CiStringType : CiType
{
	public override Type DotNetType { get { return typeof(string); } }
	public static readonly CiProperty LengthProperty = new CiProperty { Name = "Length", Type = CiIntType.Value };
	public static readonly CiFunction CharAtMethod = new CiFunction {
		Name = "CharAt",
		ReturnType = CiIntType.Value,
		Params = new CiParam[] {
			new CiParam { Type = CiIntType.Value, Name = "index" }
		}
	};
	public static readonly CiFunction SubstringMethod = new CiFunction {
		Name = "Substring",
		ReturnType = CiStringPtrType.Value,
		Params = new CiParam[] {
			new CiParam { Type = CiIntType.Value, Name = "startIndex" },
			new CiParam { Type = CiIntType.Value, Name = "length" }
		}
	};
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "Length": return LengthProperty;
		case "Substring": return SubstringMethod;
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
	public override bool Equals(object obj)
	{
		CiStringStorageType that = obj as CiStringStorageType;
		return that != null && this.Length == that.Length;
	}
	public override int GetHashCode()
	{
		return this.Length.GetHashCode();
	}
	public override CiType Ptr { get { return CiStringPtrType.Value; } }
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
}

public abstract class CiClassType : CiType
{
	public CiClass Class;
	public override CiSymbol LookupMember(string name)
	{
		CiField field = this.Class.Fields.SingleOrDefault(f => f.Name == name);
		if (field == null)
			throw new ParseException("No field {0} in class {1}", name, this.Class.Name);
		return field;
	}
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
}

public class CiClassPtrType : CiClassType
{
	public override bool Equals(object obj)
	{
		CiClassPtrType that = obj as CiClassPtrType;
		return that != null && this.Class == that.Class;
	}
	public override int GetHashCode()
	{
		return this.Class.GetHashCode();
	}
}

public class CiClassStorageType : CiClassType
{
	public override bool Equals(object obj)
	{
		CiClassStorageType that = obj as CiClassStorageType;
		return that != null && this.Class == that.Class;
	}
	public override int GetHashCode()
	{
		return this.Class.GetHashCode();
	}
	public override CiType Ptr { get { return new CiClassPtrType { Class = this.Class }; } }
}

public abstract class CiArrayType : CiType
{
	public CiType ElementType;
	public override CiType BaseType { get { return this.ElementType.BaseType; } }
	public override int ArrayLevel { get { return 1 + this.ElementType.ArrayLevel; } }
	public static readonly CiFunction CopyToMethod = new CiFunction {
		Name = "CopyTo",
		ReturnType = CiType.Void,
		Params = new CiParam[] {
			new CiParam { Type = CiIntType.Value, Name = "sourceIndex" },
			new CiParam { Type = CiArrayPtrType.ByteArray, Name = "destinationArray" },
			new CiParam { Type = CiIntType.Value, Name = "destinationIndex" },
			new CiParam { Type = CiIntType.Value, Name = "length" }
		}
	};
	public static readonly CiFunction ToStringMethod = new CiFunction {
		Name = "ToString",
		ReturnType = CiStringPtrType.Value,
		Params = new CiParam[] {
			new CiParam { Type = CiIntType.Value, Name = "startIndex" },
			new CiParam { Type = CiIntType.Value, Name = "length" }
		}
	};
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "CopyTo":
			if (this.ElementType == CiByteType.Value)
				return CopyToMethod;
			throw new ParseException("CopyTo available only for byte arrays");
		case "ToString":
			if (this.ElementType == CiByteType.Value)
				return ToStringMethod;
			throw new ParseException("ToString available only for byte arrays");
		default:
			throw new ParseException("No member {0} in array", name);
		}
	}
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
}

public class CiArrayPtrType : CiArrayType
{
	public static readonly CiArrayPtrType ByteArray = new CiArrayPtrType { ElementType = CiByteType.Value };
	public override bool Equals(object obj)
	{
		CiArrayPtrType that = obj as CiArrayPtrType;
		return that != null && this.ElementType.Equals(that.ElementType);
	}
	public override int GetHashCode()
	{
		return this.ElementType.GetHashCode();
	}
}

public class CiArrayStorageType : CiArrayType
{
	public CiExpr LengthExpr;
	public int Length;
	public override CiType Ptr { get { return new CiArrayPtrType { ElementType = this.ElementType }; } }
	public static readonly CiFunction ClearMethod = new CiFunction {
		Name = "Clear",
		ReturnType = CiType.Void,
		Params = new CiParam[0]
	};
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "Clear": return ClearMethod;
		case "Length": return new CiConst { Value = this.Length };
		default: return base.LookupMember(name);
		}
	}
	public override CiType Accept(ICiTypeVisitor v) { return v.Visit(this); }
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
}

public class CiField : CiSymbol
{
	public CiType Type;
}

public class CiProperty : CiSymbol
{
	public CiType Type;
}

public class CiClass : CiSymbol
{
	public CiField[] Fields;
}

public class CiUnknownClass : CiClass
{
}

public interface ICiStatementVisitor
{
	void Visit(CiBlock statement);
	void Visit(CiConst statement);
	void Visit(CiVar statement);
	void Visit(CiExpr statement);
	void Visit(CiAssign statement);
	void Visit(CiBreak statement);
	void Visit(CiContinue statement);
	void Visit(CiDoWhile statement);
	void Visit(CiFor statement);
	void Visit(CiIf statement);
	void Visit(CiReturn statement);
	void Visit(CiSwitch statement);
	void Visit(CiWhile statement);
}

public interface ICiStatement
{
	void Accept(ICiStatementVisitor v);
}

public class CiConst : CiSymbol, ICiStatement
{
	public CiType Type;
	public object Value;
	public string GlobalName;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiVar : CiSymbol, ICiStatement
{
	public CiType Type;
	public CiExpr InitialValue;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiBinaryResource : CiSymbol
{
	public byte[] Content;
	public CiArrayStorageType Type;
}

public class CiParam : CiVar
{
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
	CiExpr Visit(CiFunctionCall expr);
	CiExpr Visit(CiMethodCall expr);
	CiExpr Visit(CiUnaryExpr expr);
	CiExpr Visit(CiCondNotExpr expr);
	CiExpr Visit(CiPostfixExpr expr);
	CiExpr Visit(CiBinaryExpr expr);
	CiExpr Visit(CiBoolBinaryExpr expr);
	CiExpr Visit(CiCondExpr expr);
	CiExpr Visit(CiBinaryResourceExpr expr);
}

public abstract class CiExpr : CiMaybeAssign
{
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
}

public abstract class CiLValue : CiExpr
{
}

public class CiSymbolAccess : CiExpr
{
	public CiSymbol Symbol;
	public override CiType Type { get { throw new ApplicationException(); } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiConstAccess : CiExpr
{
	public CiConst Const;
	public override CiType Type { get { return this.Const.Type; } }
}

public class CiVarAccess : CiLValue
{
	public CiVar Var;
	public override CiType Type { get { return this.Var.Type; } }
}

public class CiUnknownMemberAccess : CiExpr
{
	public CiExpr Parent;
	public string Name;
	public override CiType Type { get { throw new ApplicationException(); } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiFieldAccess : CiLValue
{
	public CiExpr Obj;
	public CiField Field;
	public override CiType Type { get { return this.Field.Type; } }
}

public class CiPropertyAccess : CiExpr
{
	public CiExpr Obj;
	public CiProperty Property;
	public override CiType Type { get { return this.Property.Type; } }
}

public class CiIndexAccess : CiExpr
{
	public CiExpr Parent;
	public CiExpr Index;
	public override CiType Type { get { throw new ApplicationException(); } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiArrayAccess : CiLValue
{
	public CiExpr Array;
	public CiExpr Index;
	public override CiType Type
	{
		get
		{
			CiArrayType at = (CiArrayType) this.Array.Type;
			return at.ElementType;
		}
	}
}

public class CiFunctionCall : CiExpr, ICiStatement
{
	public string Name;
	public CiFunction Function;
	public CiExpr[] Arguments;
	public override CiType Type { get { return this.Function.ReturnType; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiMethodCall : CiFunctionCall
{
	public CiExpr Obj;
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiUnaryExpr : CiExpr
{
	public CiToken Op;
	public CiExpr Inner;
	public override CiType Type { get { return CiIntType.Value; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiCondNotExpr : CiExpr
{
	public CiExpr Inner;
	public override CiType Type { get { return CiBoolType.Value; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiPostfixExpr : CiExpr, ICiStatement
{
	public CiExpr Inner;
	public CiToken Op;
	public override CiType Type { get { return CiIntType.Value; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiBinaryExpr : CiExpr
{
	public CiExpr Left;
	public CiToken Op;
	public CiExpr Right;
	public override CiType Type { get { return CiIntType.Value; } }
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
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiBinaryResourceExpr : CiExpr
{
	public CiExpr NameExpr;
	public CiBinaryResource Resource;
	public override CiType Type { get { return this.Resource.Type; } }
	public override CiExpr Accept(ICiExprVisitor v) { return v.Visit(this); }
}

public class CiCoercion : CiExpr
{
	public CiType ResultType;
	public CiMaybeAssign Inner;
	public override CiType Type { get { return this.ResultType; } }
}

public class CiAssign : CiMaybeAssign, ICiStatement
{
	public CiExpr Target;
	public CiToken Op;
	public CiMaybeAssign Source;
	public override CiType Type { get { return this.Target.Type; } }
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiBlock : ICiStatement
{
	public ICiStatement[] Statements;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiBreak : ICiStatement
{
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiContinue : ICiStatement
{
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiDoWhile : ICiStatement
{
	public ICiStatement Body;
	public CiExpr Cond;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiFor : ICiStatement
{
	public ICiStatement Init;
	public CiExpr Cond;
	public ICiStatement Advance;
	public ICiStatement Body;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiIf : ICiStatement
{
	public CiExpr Cond;
	public ICiStatement OnTrue;
	public ICiStatement OnFalse;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiReturn : ICiStatement
{
	public CiExpr Value;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiCase
{
	public object Value;
	public ICiStatement[] Body;
}

public class CiSwitch : ICiStatement
{
	public CiExpr Value;
	public CiCase[] Cases;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiWhile : ICiStatement
{
	public CiExpr Cond;
	public ICiStatement Body;
	public void Accept(ICiStatementVisitor v) { v.Visit(this); }
}

public class CiFunction : CiSymbol
{
	public CiType ReturnType;
	public CiParam[] Params;
	public CiBlock Body;
}

public class CiProgram
{
	public SymbolTable Globals;
	public CiConst[] ConstArrays;
	public CiBinaryResource[] BinaryResources;
}

}
