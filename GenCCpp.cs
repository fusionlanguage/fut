// GenCCpp.cs - C/C++ code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
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

namespace Foxoft.Ci
{

public class SystemInclude
{
	internal string Name;
	internal bool Needed = false;
	internal SystemInclude(string name)
	{
		this.Name = name;
	}
}

public abstract class GenCCpp : GenTyped
{
	protected bool IncludeMath;
	protected SystemInclude IncludeStdInt;
	protected readonly Dictionary<CiClass, bool> WrittenClasses = new Dictionary<CiClass, bool>();

	protected void Write(SystemInclude include)
	{
		if (!include.Needed || include.Name == null)
			return;
		Write("#include <");
		Write(include.Name);
		WriteLine(">");
		include.Name = null;
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte:
			this.IncludeStdInt.Needed = true;
			Write("int8_t");
			break;
		case TypeCode.Byte:
			this.IncludeStdInt.Needed = true;
			Write("uint8_t");
			break;
		case TypeCode.Int16:
			this.IncludeStdInt.Needed = true;
			Write("int16_t");
			break;
		case TypeCode.UInt16:
			this.IncludeStdInt.Needed = true;
			Write("uint16_t");
			break;
		case TypeCode.Int32:
			Write("int");
			break;
		case TypeCode.Int64:
			this.IncludeStdInt.Needed = true;
			Write("int64_t");
			break;
		default:
			throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		CiClassPtrType coercedType;
		if (expr.Left.Type is CiClassPtrType leftPtr && leftPtr.IsAssignableFrom(expr.Right.Type))
			coercedType = leftPtr;
		else if (expr.Right.Type is CiClassPtrType rightPtr && rightPtr.IsAssignableFrom(expr.Left.Type))
			coercedType = rightPtr;
		else {
			base.WriteEqual(expr, parent, not);
			return;
		}
		if (parent > CiPriority.Equality)
			Write('(');
		WriteCoerced(coercedType, expr.Left, CiPriority.Equality);
		Write(not ? " != " : " == ");
		WriteCoerced(coercedType, expr.Right, CiPriority.Equality);
		if (parent > CiPriority.Equality)
			Write(')');
	}

	protected virtual void WriteArrayPtr(CiExpr expr, CiPriority parent)
	{
		expr.Accept(this, parent);
	}

	protected void WriteArrayPtrAdd(CiExpr array, CiExpr index)
	{
		if (index is CiLiteral literal && (long) literal.Value == 0)
			WriteArrayPtr(array, CiPriority.Statement);
		else {
			WriteArrayPtr(array, CiPriority.Add);
			Write(" + ");
			index.Accept(this, CiPriority.Add);
		}
	}

	protected override void WriteClassStorageInit(CiClass klass)
	{
	}

	protected abstract void WriteConst(CiConst konst);

	public override void Visit(CiConst konst)
	{
		if (konst.Type is CiArrayType)
			WriteConst(konst);
	}
}

}
