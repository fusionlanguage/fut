// GenJsWithTypedArrays.cs - JavaScript with Typed Arrays code generator
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

namespace Foxoft.Ci
{

public class GenJsWithTypedArrays : GenJs
{
	protected override void WriteNew(CiType type)
	{
		CiArrayStorageType arrayType = type as CiArrayStorageType;
		if (arrayType != null) {
			if (arrayType.ElementType == CiByteType.Value) {
				Write("new Uint8Array(new ArrayBuffer(");
				if (arrayType.LengthExpr != null)
					Write(arrayType.LengthExpr);
				else
					Write(arrayType.Length);
				Write("))");
				return;
			}
			if (arrayType.ElementType == CiIntType.Value) {
				Write("new Int32Array(new ArrayBuffer(");
				if (arrayType.LengthExpr != null) {
					WriteChild(CiPriority.Shift, arrayType.LengthExpr);
					Write(" << 2");
				}
				else
					Write(arrayType.Length << 2);
				Write("))");
				return;
			}
		}
		base.WriteNew(type);
	}

	protected override void WriteInitArrayStorageVar(CiVar stmt)
	{
		CiType type = ((CiArrayStorageType) stmt.Type).ElementType;
		if (type != CiByteType.Value && type != CiIntType.Value)
			base.WriteInitArrayStorageVar(stmt);
	}
}

}
