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

public abstract class CiSymbol
{
	public string Name;
}

public class CiNamedValue : CiSymbol
{
	public CiExpr Type;
	public CiExpr Value;
}

public class CiVar : CiNamedValue
{
}

public abstract class CiType : CiSymbol
{
}

public abstract class CiContainerType : CiType
{
	public string SourceFilename;
	public CiVisibility Visibility;
	public SymbolTable Members;
}

public class CiEnum : CiContainerType
{
	public bool IsFlags;
}

public class CiMember : CiNamedValue
{
	public CiVisibility Visibility;
}

public class CiConst : CiMember
{
}

public class CiField : CiMember
{
}

public class CiMethod : CiMember
{
	public CiCallType CallType;
	public bool IsMutator;
	public CiVar[] Parameters;
	public CiStatement Body;
}

public class CiClass : CiContainerType
{
	public CiCallType CallType;
	public CiSymbolReference BaseClass;
}

public abstract class CiStatement
{
	public int Line;
}

public abstract class CiExpr : CiStatement
{
}

public class CiCollection : CiExpr
{
	public CiExpr[] Items;
}

public class CiLiteral : CiExpr
{
	public object Value;
}

public class CiSymbolReference : CiExpr
{
	public string Name;
	public CiSymbol Symbol;
}

public class CiUnaryExpr : CiExpr
{
	public CiToken Op;
	public CiExpr Inner;
}

public class CiPostfixExpr : CiUnaryExpr
{
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

public abstract class CiLoop : CiStatement
{
	public CiExpr Cond;
	public CiStatement Body;
}

public class CiBlock : CiStatement
{
	public CiStatement[] Statements;
}

public class CiBreak : CiStatement
{
}

public class CiContinue : CiStatement
{
}

public class CiDelete : CiStatement
{
	public CiExpr Expr;
}

public class CiDoWhile : CiLoop
{
}

public class CiFor : CiLoop
{
	// TODO
}

public class CiIf : CiStatement
{
	public CiExpr Cond;
	public CiStatement OnTrue;
	public CiStatement OnFalse;
}

public class CiReturn : CiStatement
{
	public CiExpr Value;
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
}

public class CiThrow : CiStatement
{
	public CiExpr Message;
}

public class CiWhile : CiLoop
{
}

}
