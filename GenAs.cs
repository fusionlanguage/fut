// GenAs.cs - ActionScript code generator
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
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

enum GenAsMethod
{
	CopyArray,
	CopyByteArray,
	FillArray,
	FillByteArray,
	Count
}

public class GenAs : GenBase
{
	string Namespace;
	string OutputDirectory;
	readonly string[][] Library = new string[(int) GenAsMethod.Count][];

	public GenAs(string namespace_)
	{
		this.Namespace = namespace_;
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Private:
			Write("private ");
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

	static bool IsByte(CiType type)
	{
		CiRangeType range = type as CiRangeType;
		return range != null && range.Min >= 0 && range.Max <= byte.MaxValue;
	}

	static bool IsByteArray(CiType type)
	{
		CiArrayType array = type as CiArrayType;
		return array != null && IsByte(array.ElementType);
	}

	protected override void Write(CiType type, bool promote)
	{
		Write(" : ");

		if (type == null)
			Write("void");
		else if (type is CiNumericType) {
			CiIntegerType integer = type as CiIntegerType;
			Write(integer == null || integer.IsLong ? "Number" : "int");
		}
		else if (type == CiSystem.BoolType)
			Write("Boolean");
		else if (type is CiStringType)
			Write("String");
		else if (type is CiEnum)
			Write("int");
		else if (type is CiArrayType)
			Write(IsByteArray(type) ? "ByteArray" : "Array");
		else
			Write(type.Name);		
	}

	protected override void WriteName(CiSymbol symbol)
	{
		if (symbol is CiConst)
			WriteUppercaseWithUnderscores(symbol.Name);
		else if (symbol is CiMember)
			WriteCamelCase(symbol.Name);
		else
			Write(symbol.Name);
	}

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		WriteName(value);
		Write(value.Type, true);
	}

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		Write("[ ");
		WriteCoerced(null, expr.Items);
		Write(" ]");
		return expr;
	}

	protected override void WriteVar(CiNamedValue def)
	{
		Write(def.Type is CiClass || def.Type is CiArrayStorageType ? "const " : "var ");
		base.WriteVar(def);
	}

	protected override void WriteNewArray(CiType type, CiExpr lengthExpr)
	{
		if (IsByte(type))
			Write("new ByteArray()");
		else {
			Write("new Array(");
			lengthExpr.Accept(this, CiPriority.Statement);
			Write(')');
		}
	}

	protected override void WriteResource(string name, int length)
	{
		if (length >= 0) // reference as opposed to definition
			Write("new Ci.");
		foreach (char c in name)
			Write(CiLexer.IsLetterOrDigit(c) ? c : '_');
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		expr.Accept(this, CiPriority.Primary);
		Write(".length");
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		expr.Left.Accept(this, CiPriority.Primary);
		Write(".charCodeAt(");
		WritePromoted(expr.Right, CiPriority.Statement);
		Write(')');
	}

	protected override void WriteCall(CiExpr obj, string method, CiExpr[] args)
	{
		if (obj.Type is CiArrayType && method == "CopyTo") {
			if (IsByteArray(obj.Type)) {
				if (this.Library[(int) GenAsMethod.CopyByteArray] == null) {
					this.Library[(int) GenAsMethod.CopyByteArray] = new string[] {
						"copyByteArray(sa : ByteArray, soffset : int, da : ByteArray, doffset : int, length : int) : void",
						"for (var i : int = 0; i < length; i++)",
						"\tda[doffset + i] = sa[soffset + i];"
					};
				}
				Write("Ci.copyByteArray(");
			}
			else {
				if (this.Library[(int) GenAsMethod.CopyArray] == null) {
					this.Library[(int) GenAsMethod.CopyArray] = new string[] {
						"copyArray(sa : Array, soffset : int, da : Array, doffset : int, length : int) : void",
						"for (var i : int = 0; i < length; i++)",
						"\tda[doffset + i] = sa[soffset + i];"
					};
				}
				Write("Ci.copyArray(");
			}
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			WritePromoted(args);
			Write(')');
		}
		else if (obj.Type is CiArrayStorageType && method == "Fill") {
			if (IsByteArray(obj.Type)) {
				if (this.Library[(int) GenAsMethod.FillByteArray] == null) {
					this.Library[(int) GenAsMethod.FillByteArray] = new string[] {
						"fillByteArray(a : ByteArray, length : int, value : int) : void",
						"for (var i : int = 0; i < length; i++)",
						"\ta[i] = value;"
					};
				}
				Write("Ci.fillByteArray(");
			}
			else {
				if (this.Library[(int) GenAsMethod.FillArray] == null) {
					this.Library[(int) GenAsMethod.FillArray] = new string[] {
						"fillArray(a : Array, length : int, value : *) : void",
						"for (var i : int = 0; i < length; i++)",
						"\ta[i] = value;"
					};
				}
				Write("Ci.fillArray(");
			}
			obj.Accept(this, CiPriority.Statement);
			Write(", ");
			((CiArrayStorageType) obj.Type).LengthExpr.Accept(this, CiPriority.Statement);
			Write(", ");
			args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
		else {
			obj.Accept(this, CiPriority.Primary);
			Write('.');
			WriteCamelCase(method);
			Write('(');
			WritePromoted(args);
			Write(')');
		}
	}

	public override void Visit(CiThrow statement)
	{
		Write("throw ");
		statement.Message.Accept(this, CiPriority.Statement);
		WriteLine(";");
	}

	void CreateAsFile(string className)
	{
		string dir = Path.GetDirectoryName(this.OutputFile);
		CreateFile(Path.Combine(dir, className + ".as"));
		if (this.Namespace != null) {
			Write("package ");
			WriteLine(this.Namespace);
		}
		else
			WriteLine("package");
		OpenBlock();
	}

	void CloseAsFile()
	{
		CloseBlock(); // class
		CloseBlock(); // package
		CloseFile();
	}

	void WritePublicOrInternal(CiContainerType type)
	{
		Write(type.IsPublic ? "public " : "internal ");
	}

	void Write(CiEnum enu)
	{
		CreateAsFile(enu.Name);
		WritePublicOrInternal(enu);
		Write("final class ");
		WriteLine(enu.Name);
		OpenBlock();
		int i = 0;
		foreach (CiConst konst in enu) {
			Write("public static const ");
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" : int = ");
			if (konst.Value != null)
				konst.Value.Accept(this, CiPriority.Statement);
			else
				Write(i);
			WriteLine(";");
			i++;
		}
		CloseAsFile();
	}

	void WriteConsts(IEnumerable<CiConst> konsts)
	{
		foreach (CiConst konst in konsts) {
			Write(konst.Visibility);
			Write("static const ");
			WriteTypeAndName(konst);
			Write(" = ");
			if (IsByteArray(konst.Type)) {
				WriteLine("new ByteArray();");
				OpenBlock();
				foreach (CiExpr elem in ((CiCollection) konst.Value).Items) {
					WriteUppercaseWithUnderscores(konst.Name);
					Write(".writeByte(");
					elem.Accept(this, CiPriority.Statement);
					WriteLine(");");
				}
				CloseBlock();
			}
			else {
				konst.Value.Accept(this, CiPriority.Statement);
				WriteLine(";");
			}
		}
	}

	void Write(CiClass klass)
	{
		CreateAsFile(klass.Name);
		WriteLine("import flash.utils.ByteArray;");
		WriteLine();
		WritePublicOrInternal(klass);
		switch (klass.CallType) {
		case CiCallType.Normal:
			break;
		case CiCallType.Abstract:
			Write("/* abstract */ ");
			break;
		case CiCallType.Static:
		case CiCallType.Sealed:
			Write("final ");
			break;
		default:
			throw new NotImplementedException(klass.CallType.ToString());
		}
		OpenClass(klass, " extends ");

		if (klass.Constructor != null) {
			Write("public function ");
			Write(klass.Name);
			WriteLine("()");
			Visit((CiBlock) klass.Constructor.Body);
		}

		WriteConsts(klass.Consts);

		foreach (CiField field in klass.Fields) {
			Write(field.Visibility);
			WriteVar(field);
			WriteLine(";");
		}

		foreach (CiMethod method in klass.Methods) {
			WriteLine();
			Write(method.Visibility);
			switch (method.CallType) {
			case CiCallType.Static:
				Write("static ");
				break;
			case CiCallType.Virtual:
			case CiCallType.Abstract:
				break;
			case CiCallType.Override:
				Write("override ");
				break;
			case CiCallType.Normal:
				if (method.Visibility != CiVisibility.Private)
					Write("final ");
				break;
			case CiCallType.Sealed:
				Write("final ");
				break;
			default:
				throw new NotImplementedException(method.CallType.ToString());
			}
			Write("function ");
			WriteCamelCase(method.Name);
			Write('(');
			bool first = true;
			foreach (CiVar param in method.Parameters) {
				if (!first)
					Write(", ");
				first = false;
				WriteTypeAndName(param);
			}
			Write(')');
			Write(method.Type, true);
			if (method.CallType == CiCallType.Abstract) {
				OpenBlock();
				WriteLine("throw \"Abstract method called\";");
				CloseBlock();
			}
			else
				WriteBody(method);
		}

		WriteConsts(klass.ConstArrays);

		CloseAsFile();
	}

	void WriteLib(Dictionary<string, byte[]> resources)
	{
		CreateAsFile("Ci");
		WriteLine("import flash.utils.ByteArray;");
		WriteLine();
		WriteLine("internal final class Ci");
		OpenBlock();
		foreach (string[] method in this.Library) {
			if (method != null) {
				WriteLine();
				Write("internal static function ");
				WriteLine(method[0]);
				OpenBlock();
				for (int i = 1; i < method.Length; i++)
					WriteLine(method[i]);
				CloseBlock();
			}
		}
		foreach (string name in resources.Keys.OrderBy(k => k)) {
			WriteLine();
			Write("[Embed(source=\"/");
			Write(name);
			WriteLine("\", mimeType=\"application/octet-stream\")]");
			Write("internal static const ");
			WriteResource(name, -1);
			WriteLine(" : Class;");
		}
		CloseAsFile();
	}

	public override void Write(CiProgram program)
	{
		Array.Clear(this.Library, 0, this.Library.Length);
		if (Directory.Exists(this.OutputFile))
			this.OutputDirectory = this.OutputFile;
		else
			this.OutputDirectory = Path.GetDirectoryName(this.OutputFile);
		foreach (CiContainerType type in program) {
			CiClass klass = type as CiClass;
			if (klass != null)
				Write(klass);
			else
				Write((CiEnum) type);
		}
		if (program.Resources.Count > 0 || this.Library.Any(l => l != null))
			WriteLib(program.Resources);
	}
}

}
