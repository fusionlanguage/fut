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
using System.IO;

namespace Foxoft.Ci
{

public abstract class GenCCpp : GenTyped
{
	StringWriter StringWriter;
	protected SortedSet<string> Includes;
	protected readonly Dictionary<CiClass, bool> WrittenClasses = new Dictionary<CiClass, bool>();

	protected void OpenStringWriter()
	{
		this.StringWriter = new StringWriter();
		this.StringWriter.NewLine = "\n";
		this.Writer = this.StringWriter;
	}

	protected void CloseStringWriter()
	{
		this.Writer.Write(this.StringWriter.GetStringBuilder());
		this.StringWriter = null;
	}

	protected void Include(string name)
	{
		this.Includes.Add(name);
	}

	protected abstract void IncludeStdInt();

	protected void WriteIncludes()
	{
		foreach (string name in this.Includes) {
			Write("#include <");
			Write(name);
			WriteLine(">");
		}
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte:
			IncludeStdInt();
			Write("int8_t");
			break;
		case TypeCode.Byte:
			IncludeStdInt();
			Write("uint8_t");
			break;
		case TypeCode.Int16:
			IncludeStdInt();
			Write("int16_t");
			break;
		case TypeCode.UInt16:
			IncludeStdInt();
			Write("uint16_t");
			break;
		case TypeCode.Int32:
			Write("int");
			break;
		case TypeCode.Int64:
			IncludeStdInt();
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
