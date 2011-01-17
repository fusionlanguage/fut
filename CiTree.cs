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

public class CiType : CiSymbol
{
	public static readonly CiType Null = new CiType { Name = "null" };
	public static readonly CiType Void = new CiType { Name = "void" };
	public virtual Type DotNetType { get { throw new ApplicationException("No corresponding .NET type"); } }
	public virtual CiType BaseType { get { return this; } }
	public virtual int ArrayLevel { get { return 0; } }
	public virtual CiSymbol LookupMember(string name)
	{
		throw new ParseException("{0} has no members", this.GetType());
	}
	public virtual bool IsAssignableFrom(CiType that) { return this == that; }
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
}

public class CiIntType : CiType
{
	private CiIntType() { }
	public static readonly CiIntType Value = new CiIntType { Name = "int" };
	public override Type DotNetType { get { return typeof(int); } }
	public static readonly CiProperty LowByteProperty = new CiProperty { Name = "LowByte", Type = CiByteType.Value };
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
	public override bool IsAssignableFrom(CiType that)
	{
		return this == that || that == CiByteType.Value;
	}
}

public class CiStringType : CiType
{
	public override Type DotNetType { get { return typeof(string); } }
	public static readonly CiProperty LengthProperty = new CiProperty { Name = "Length", Type = CiIntType.Value };
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "Length": return LengthProperty;
		default: throw new ParseException("No member {0} in string", name);
		}
	}
}

public class CiStringPtrType : CiStringType
{
	private CiStringPtrType() { }
	public static readonly CiStringPtrType Value = new CiStringPtrType { Name = "string" };
	public override bool IsAssignableFrom(CiType that)
	{
		return that is CiStringType || that == CiType.Null;
	}
}

public class CiStringStorageType : CiStringType
{
	public int Length;
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
}

public class CiClassPtrType : CiClassType
{
	public override bool IsAssignableFrom(CiType that)
	{
		return (that is CiClassType && this.Class == ((CiClassType) that).Class)
			|| that == CiType.Null;
	}
}

public class CiClassStorageType : CiClassType
{
}

public abstract class CiArrayType : CiType
{
	public CiType ElementType;
	public override CiType BaseType { get { return this.ElementType.BaseType; } }
	public override int ArrayLevel { get { return 1 + this.ElementType.ArrayLevel; } }
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "CopyTo": return null; // TODO
		default: throw new ParseException("No member {0} in array", name);
		}
	}
}

public class CiArrayPtrType : CiArrayType
{
	public override bool IsAssignableFrom(CiType that)
	{
		// FIXME
		return (that is CiArrayType && this.ElementType == ((CiArrayType) that).ElementType)
			|| that == CiType.Null;
	}
}

public class CiArrayStorageType : CiArrayType
{
	public int Length;
	public static readonly CiFunction ClearMethod = new CiFunction {
		Name = "Clear",
		ReturnType = CiType.Void,
		Params = new CiParam[0]
	};
	public override CiSymbol LookupMember(string name)
	{
		switch (name) {
		case "Clear": return ClearMethod;
		default: return base.LookupMember(name);
		}
	}
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

public interface ICiStatement
{
}

public class CiConst : CiSymbol, ICiStatement
{
	public CiType Type;
	public object Value;
	public string GlobalName;
}

public class CiVar : CiSymbol, ICiStatement
{
	public CiType Type;
	public CiExpr InitialValue;
}

public class CiParam : CiVar
{
}

public abstract class CiMaybeAssign
{
	public abstract CiType Type { get; }
}

public abstract class CiExpr : CiMaybeAssign
{
}

public class CiConstExpr : CiExpr
{
	public object Value;
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
	public CiFunction Function;
	public CiExpr[] Arguments;
	public override CiType Type { get { return this.Function.ReturnType; } }
}

public class CiMethodCall : CiFunctionCall
{
	public CiExpr Obj;
}

public class CiUnaryExpr : CiExpr
{
	public CiToken Op;
	public CiExpr Inner;
	public override CiType Type { get { return CiIntType.Value; } }
}

public class CiCondNotExpr : CiExpr
{
	public CiExpr Inner;
	public override CiType Type { get { return CiBoolType.Value; } }
}

public class CiPostfixExpr : CiExpr, ICiStatement
{
	public CiLValue Inner;
	public CiToken Op;
	public override CiType Type { get { return CiIntType.Value; } }
}

public class CiBinaryExpr : CiExpr
{
	public CiExpr Left;
	public CiToken Op;
	public CiExpr Right;
	public override CiType Type { get { return CiIntType.Value; } }
}

public class CiBoolBinaryExpr : CiBinaryExpr
{
	public override CiType Type { get { return CiBoolType.Value; } }
}

public class CiCondExpr : CiExpr
{
	public CiExpr Cond;
	public CiExpr OnTrue;
	public CiExpr OnFalse;
	public override CiType Type
	{
		get
		{
			if (this.OnTrue.Type.IsAssignableFrom(this.OnFalse.Type))
				return this.OnTrue.Type;
			return this.OnFalse.Type;
		}
	}
}

public class CiAssign : CiMaybeAssign, ICiStatement
{
	public CiLValue Target;
	public CiToken Op;
	public CiMaybeAssign Source;
	public bool CastIntToByte;
	public override CiType Type { get { return this.Target.Type; } }
}

public class CiBlock : ICiStatement
{
	public ICiStatement[] Statements;
}

public class CiBreak : ICiStatement
{
}

public class CiContinue : ICiStatement
{
}

public class CiDoWhile : ICiStatement
{
	public ICiStatement Body;
	public CiExpr Cond;
}

public class CiFor : ICiStatement
{
	public ICiStatement Init;
	public CiExpr Cond;
	public ICiStatement Advance;
	public ICiStatement Body;
}

public class CiIf : ICiStatement
{
	public CiExpr Cond;
	public ICiStatement OnTrue;
	public ICiStatement OnFalse;
}

public class CiReturn : ICiStatement
{
	public CiExpr Value;
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
}

public class CiWhile : ICiStatement
{
	public CiExpr Cond;
	public ICiStatement Body;
}

public class CiFunction : CiSymbol
{
	public CiType ReturnType;
	public CiParam[] Params;
	public CiBlock Body;
}

public class CiProgram
{
	public string[] NamespaceElements;
	public SymbolTable Globals;
	public CiConst[] ConstArrays;
}

}
