// GenCs.cs - C# code generator
//
// Copyright (C) 2011-2016  Piotr Fusik
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

public class GenCs : GenBase
{
	string Namespace;

	public GenCs(string namespace_)
	{
		this.Namespace = namespace_;
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Private:
			break;
		case CiVisibility.Internal:
			Write("internal ");
			break;
		case CiVisibility.Protected:
			Write("protected ");
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	void Write(CiCallType callType)
	{
		switch (callType) {
		case CiCallType.Static:
			Write("static ");
			break;
		case CiCallType.Normal:
			break;
		case CiCallType.Abstract:
			Write("abstract ");
			break;
		case CiCallType.Virtual:
			Write("virtual ");
			break;
		case CiCallType.Override:
			Write("override ");
			break;
		case CiCallType.Sealed:
			Write("sealed ");
			break;
		}
	}

	static TypeCode GetTypeCode(CiIntegerType integer)
	{
		if (integer is CiIntType)
			return TypeCode.Int32;
		if (integer.IsLong)
			return TypeCode.Int64;
		CiRangeType range = (CiRangeType) integer;
		if (range.Min < 0) {
			if (range.Min < short.MinValue || range.Max > short.MaxValue)
				return TypeCode.Int32;
			if (range.Min < sbyte.MinValue || range.Max > sbyte.MaxValue)
				return TypeCode.Int16;
			return TypeCode.SByte;
		}
		if (range.Max > ushort.MaxValue)
			return TypeCode.Int32;
		if (range.Max > byte.MaxValue)
			return TypeCode.UInt16;
		return TypeCode.Byte;
	}

	void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte: Write("sbyte"); break;
		case TypeCode.Byte: Write("byte"); break;
		case TypeCode.Int16: Write("short"); break;
		case TypeCode.UInt16: Write("ushort"); break;
		case TypeCode.Int32: Write("int"); break;
		case TypeCode.Int64: Write("long"); break;
		default: throw new NotImplementedException(typeCode.ToString());
		}
	}

	static TypeCode GetTypeCode(CiType type)
	{
		if (type is CiNumericType) {
			CiIntegerType integer = type as CiIntegerType;
			if (integer != null)
				return GetTypeCode(integer);
			if (type == CiSystem.DoubleType)
				return TypeCode.Double;
			if (type == CiSystem.FloatType)
				return TypeCode.Single;
			throw new NotImplementedException(type.ToString());
		}
		else if (type == CiSystem.BoolType)
			return TypeCode.Boolean;
		else if (type == CiSystem.NullType)
			return TypeCode.Empty;
		else if (type is CiStringType)
			return TypeCode.String;
		return TypeCode.Object;
	}

	protected override void Write(CiType type)
	{
		if (type == null) {
			Write("void");
			return;
		}

		CiArrayType array = type as CiArrayType;
		if (array != null) {
			Write(array.ElementType);
			Write("[]");
			return;
		}

		CiIntegerType integer = type as CiIntegerType;
		if (integer != null) {
			Write(GetTypeCode(integer));
			return;
		}

		Write(type.Name);
	}

	void WriteVar(CiNamedValue def)
	{
		WriteTypeAndName(def);
		CiClass klass = def.Type as CiClass;
		if (klass != null) {
			Write(" = ");
			WriteNew(klass);
		}
		else {
			CiArrayStorageType array = def.Type as CiArrayStorageType;
			if (array != null) {
				Write(" = ");
				WriteNewArray(array.ElementType, new CiLiteral((long) array.Length));
				// FIXME: arrays of object storage, initialized arrays
			}
			else if (def.Value != null) {
				Write(" = ");
				WriteCoerced(def.Type, def.Value);
			}
		}
	}

	public override CiExpr Visit(CiVar expr, CiPriority parent)
	{
		WriteVar(expr);
		return expr;
	}

	static TypeCode GetPromotedTypeCode(CiExpr expr)
	{
		CiBinaryExpr binary = expr as CiBinaryExpr;
		if (binary != null) {
			switch (binary.Op) {
			case CiToken.Plus:
			case CiToken.Minus:
			case CiToken.Asterisk:
			case CiToken.Slash:
			case CiToken.Mod:
				if (binary.Left.Type == CiSystem.DoubleType || binary.Right.Type == CiSystem.DoubleType)
					return TypeCode.Double;
				if (binary.Left.Type == CiSystem.FloatType || binary.Right.Type == CiSystem.FloatType)
					return TypeCode.Single;
				return ((CiIntegerType) binary.Left.Type).IsLong || ((CiIntegerType) binary.Right.Type).IsLong ? TypeCode.Int64 : TypeCode.Int32;
			case CiToken.ShiftLeft:
			case CiToken.ShiftRight:
				return ((CiIntegerType) binary.Left.Type).IsLong ? TypeCode.Int64 : TypeCode.Int32;
			case CiToken.And:
			case CiToken.Or:
			case CiToken.Xor:
				return ((CiIntegerType) binary.Left.Type).IsLong || ((CiIntegerType) binary.Right.Type).IsLong ? TypeCode.Int64 : TypeCode.Int32;
			default:
				break;
			}
		}
		// TODO
		return GetTypeCode(expr.Type);
	}

	static bool IsNarrower(TypeCode left, TypeCode right)
	{
		switch (left) {
		case TypeCode.SByte:
			switch (right) {
			case TypeCode.Byte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Byte:
			switch (right) {
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Int16:
			switch (right) {
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.UInt16:
			switch (right) {
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Int32:
			return right == TypeCode.Int64;
		default:
			return false;
		}
	}

	protected override void WriteCoerced(CiType type, CiExpr expr)
	{
		TypeCode typeCode = GetTypeCode(type);
		if (IsNarrower(typeCode, GetPromotedTypeCode(expr))) {
			Write('(');
			Write(typeCode);
			Write(") ");
			expr.Accept(this, CiPriority.Primary);
		}
		else
			base.WriteCoerced(type, expr);
	}

	protected override void WriteCall(CiExpr obj, string method, CiExpr[] args)
	{
		if (obj.Type is CiArrayType && method == "CopyTo") {
			Write("System.Array.Copy(");
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			Write(args);
			Write(')');
		}
		else if (obj.Type is CiArrayStorageType && method == "Fill") {
			CiLiteral literal = args[0] as CiLiteral;
			if (literal == null || !literal.IsDefaultValue)
				throw new NotImplementedException("Only null, zero and false supported");
			Write("System.Array.Clear(");
			obj.Accept(this, CiPriority.Statement);
			Write(", 0, ");
			Write(((CiArrayStorageType) obj.Type).Length);
			Write(')');
		}
		else
			base.WriteCall(obj, method, args);
	}

	protected override void WriteCondChild(CiCondExpr cond, CiExpr expr)
	{
		if (expr is CiLiteral) {
			TypeCode condTypeCode = GetTypeCode(cond.Type);
			if (IsNarrower(condTypeCode, TypeCode.Int32)) {
				Write('(');
				Write(condTypeCode);
				Write(") ");
				expr.Accept(this, CiPriority.Primary);
				return;
			}
		}
		base.WriteCondChild(cond, expr);
	}

	public override void Visit(CiThrow statement)
	{
		Write("throw new System.Exception(");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine(");");
	}

	void Write(CiEnum enu)
	{
		WriteLine();
		if (enu.IsFlags)
			WriteLine("[System.Flags]");
		WritePublic(enu);
		Write("enum ");
		WriteLine(enu.Name);
		OpenBlock();
		bool first = true;
		foreach (CiConst konst in enu) {
			if (!first)
				WriteLine(",");
			first = false;
			Write(konst.Name);
			if (konst.Value != null) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Statement);
			}
		}
		WriteLine();
		CloseBlock();
	}

	void WriteConsts(IEnumerable<CiConst> konsts)
	{
		foreach (CiConst konst in konsts) {
			Write(konst.Visibility);
			if (konst.Type is CiArrayStorageType)
				Write("static readonly ");
			else
				Write("const ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Statement);
			WriteLine(";");
		}
	}

	void Write(CiClass klass)
	{
		WriteLine();
		WritePublic(klass);
		Write(klass.CallType);
		OpenClass(klass, " : ");

		if (klass.Constructor != null) {
			Write(klass.Constructor.Visibility);
			Write(klass.Name);
			WriteLine("()");
			Visit((CiBlock) klass.Constructor.Body);
		}
		else if (klass.IsPublic && klass.CallType != CiCallType.Static) {
			if (klass.CallType != CiCallType.Sealed)
				Write("protected ");
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			CloseBlock();
		}

		WriteConsts(klass.Consts);

		foreach (CiField field in klass.Fields) {
			Write(field.Visibility);
			if (field.Type is CiClass || field.Type is CiArrayStorageType)
				Write("readonly ");
			WriteVar(field);
			WriteLine(";");
		}

		foreach (CiMethod method in klass.Methods) {
			WriteLine();
			Write(method.Visibility);
			Write(method.CallType);
			WriteTypeAndName(method);
			Write('(');
			bool first = true;
			foreach (CiVar param in method.Parameters) {
				if (!first)
					Write(", ");
				first = false;
				WriteTypeAndName(param);
			}
			Write(')');
			WriteBody(method);
		}

		WriteConsts(klass.ConstArrays);

		CloseBlock();
	}

	public override void Write(CiProgram program)
	{
		CreateFile(this.OutputFile);
		if (this.Namespace != null) {
			Write("namespace ");
			WriteLine(this.Namespace);
			OpenBlock();
		}
		foreach (CiEnum enu in program.OfType<CiEnum>())
			Write(enu);
		foreach (CiClass klass in program.Classes)
			Write(klass);
		if (this.Namespace != null)
			CloseBlock();
		CloseFile();
	}
}

}
