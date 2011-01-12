// CiTree.cs - Ci object model
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

namespace Foxoft.Ci
{

public class CiDocInline
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

public class CiDocBlock
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

public class CiSymbol
{
	public string Name;
}

public class CiType : CiSymbol
{
	public static readonly CiType Bool = new CiType { Name = "bool" };
	public static readonly CiType Byte = new CiType { Name = "byte" };
	public static readonly CiType Int = new CiType { Name = "int" };
	public static readonly CiType Void = new CiType { Name = "void" };
	public virtual CiType BaseType { get { return this; } }
	public virtual int ArrayLevel { get { return 0; } }
}

public class CiStringType : CiType
{
	public static readonly CiStringType Ptr = new CiStringType { Name = "string" };
}

public class CiStringStorageType : CiStringType
{
	public int Length;
}

public class CiClassType : CiType
{
	public CiClass Class;
}

public class CiClassPtrType : CiClassType
{
}

public class CiClassStorageType : CiClassType
{
}

public class CiArrayType : CiType
{
	public CiType ElementType;
	public override CiType BaseType { get { return this.ElementType.BaseType; } }
	public override int ArrayLevel { get { return 1 + this.ElementType.ArrayLevel; } }
}

public class CiArrayPtrType : CiArrayType
{
}

public class CiArrayStorageType : CiArrayType
{
	public int Length;
}

public class CiField
{
	public CiCodeDoc Documentation;
	public bool IsPublic;
	public CiType Type;
	public string Name;
}

public class CiClass : CiSymbol
{
	public CiCodeDoc Documentation;
	public bool IsPublic;
	public CiField[] Fields;
}

public interface ICiStatement
{
}

public class CiConst : CiSymbol, ICiStatement
{
	public CiCodeDoc Documentation;
	public bool IsPublic;
	public CiType Type;
	public object Value;
}

public class CiVar : CiSymbol, ICiStatement
{
	public CiType Type;
	public CiExpr InitialValue;
}

public class CiArg : CiVar
{
//	public CiCodeDoc Documentation;
}

public class CiMaybeAssign
{
}

public class CiExpr : CiMaybeAssign
{
}

public class CiConstExpr : CiExpr
{
	public object Value;
}

public class CiLValue : CiExpr
{
}

public class CiVarAccess : CiLValue
{
	public CiVar Var;
}

public class CiFieldAccess : CiLValue
{
	public CiExpr Obj;
	public CiField Field;
}

public class CiArrayAccess : CiLValue
{
	public CiExpr Array;
	public CiExpr Index;
}

public class CiFunctionCall : CiExpr, ICiStatement
{
	public CiFunction Function;
	public CiExpr[] Arguments;
}

public class CiUnaryExpr : CiExpr
{
	public CiToken Op;
	public CiExpr Inner;
}

public class CiPostfixExpr : CiExpr, ICiStatement
{
	public CiLValue Inner;
	public CiToken Op;
}

public class CiBinaryExpr : CiExpr
{
	public CiExpr Left;
	public CiToken Op;
	public CiExpr Right;
}

public class CiCondExpr : CiExpr
{
	public CiExpr Cond;
	public CiExpr OnTrue;
	public CiExpr OnFalse;
}

public class CiAssign : CiMaybeAssign, ICiStatement
{
	public CiLValue Target;
	public CiToken Op;
	public CiMaybeAssign Source;
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
	public CiVar Init;
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
	public int? Value;
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
	public CiCodeDoc Documentation;
	public bool IsPublic;
	public CiType ReturnType;
	public CiArg[] Arguments;
	public CiBlock Body;
}

public class CiProgram
{
	public string[] NamespaceElements;
	public CiClass[] Classes;
	public CiFunction[] Functions;
}

}
