// GenJsWithTypedArrays.cs - JavaScript with Typed Arrays code generator
//
// Copyright (C) 2011-2018  Piotr Fusik
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
	protected override void WriteNewArray(CiType type)
	{
		CiArrayStorageType array = (CiArrayStorageType) type;
		type = array.ElementType;
		if (!(type is CiNumericType)) {
			base.WriteNewArray(array);
			return;
		}

		string name;
		int shift;
		if (type == CiSystem.IntType) {
			name = "Int32";
			shift = 2;
		}
		else if (type == CiSystem.DoubleType) {
			name = "Float64";
			shift = 3;
		}
		else if (type == CiSystem.FloatType) {
			name = "Float32";
			shift = 2;
		}
		else if (((CiIntegerType) type).IsLong) {
			// TODO: UInt32 if possible?
			name = "Float64"; // no 64-bit integers in JavaScript
			shift = 3;
		}
		else {
			CiRangeType range = (CiRangeType) type;
			if (range.Min < 0) {
				if (range.Min < short.MinValue || range.Max > short.MaxValue) {
					name = "Int32";
					shift = 2;
				}
				else if (range.Min < sbyte.MinValue || range.Max > sbyte.MaxValue) {
					name = "Int16";
					shift = 1;
				}
				else {
					name = "Int8";
					shift = 0;
				}
			}
			else if (range.Max > ushort.MaxValue) {
				name = "Int32";
				shift = 2;
			}
			else if (range.Max > byte.MaxValue) {
				name = "UInt16";
				shift = 1;
			}
			else {
				name = "Uint8";
				shift = 0;
			}
		}

		Write("new ");
		Write(name);
		Write("Array(new ArrayBuffer(");
		if (shift == 0)
			array.LengthExpr.Accept(this, CiPriority.Statement);
		else {
			CiLiteral literalLength = array.LengthExpr as CiLiteral;
			if (literalLength != null)
				Write(((long) literalLength.Value) << shift);
			else {
				array.LengthExpr.Accept(this, CiPriority.Shift);
				Write(" << ");
				Write(shift);
			}
		}
		Write("))");
	}
}

}
