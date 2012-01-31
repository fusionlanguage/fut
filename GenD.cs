// GenD.cs - D code generator
//
// Copyright (C) 2011-2012  Adrian Matoga
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

namespace Foxoft.Ci
{

public class GenD : SourceGenerator, ICiSymbolVisitor
{
	protected CiClass CurrentClass;

	protected override void Write(CiVarAccess expr)
	{
		WriteVarName(expr.Var.Name);
	}

	protected void WriteVarName(string s)
	{
		// FIXME: what is the best way to throw an error 
		// when an identifier is a reserved keyword in a given language?
		if (s == "module")
			s = "mod_ule";
		Write(s);
	}

	protected override void WriteConst(object value)
	{
		if (value is Array) {
			Write("[ ");
			WriteContent((Array) value);
			Write(" ]");
		}
		else
			base.WriteConst(value);
	}

	protected override void WriteName(CiConst konst)
	{
		if (konst.Class != null) {
			if (konst.Class != CurrentClass) {
				Write(konst.Class.Name);
				Write('.');
			}
			Write(konst.Name);
		}
		else
			Write(konst.GlobalName ?? konst.Name);
	}

	void WriteDoc(string text, bool inMacro)
	{
		foreach (char c in text) {
			switch (c) {
			case '&': Write("&amp;"); break;
			case '<': Write("&lt;"); break;
			case '>': Write("&gt;"); break;
			case '\n': WriteLine(); Write("/// "); break;
			default:
				if (inMacro) {
					switch (c) {
					case '$': Write("&#36;"); break;
					case '(': Write("$(LPAREN)"); break;
					case ')': Write("$(RPAREN)"); break;
					default: Write(c); break;
					}
				}
				else
					Write(c);
				break;
			}
		}
	}

	void Write(CiDocPara para)
	{
		foreach (CiDocInline inline in para.Children) {
			CiDocText text = inline as CiDocText;
			if (text != null) {
				WriteDoc(text.Text, false);
				continue;
			}
			// TODO: $(D_CODE x) pastes "<pre>x</pre>" -
			// find some better alternative
			CiDocCode code = inline as CiDocCode;
			if (code != null) {
				WriteDoc(code.Text, true);
				continue;
			}
			throw new ArgumentException(inline.GetType().Name);
		}
	}

	void Write(CiDocBlock block)
	{
		CiDocList list = block as CiDocList;
		if (list != null) {
			WriteLine();
			WriteLine("/// $(UL");
			foreach (CiDocPara item in list.Items) {
				Write("/// $(LI ");
				Write(item);
				WriteLine(")");
			}
			Write("/// )");
			WriteLine();
			Write("/// ");
			return;
		}
		Write((CiDocPara) block);
	}

	protected override void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		Write("/// ");
		Write(doc.Summary);
		WriteLine();
		if (doc.Details.Length > 0) {
			WriteLine("///");
			Write("/// ");
			foreach (CiDocBlock block in doc.Details)
				Write(block);
			WriteLine();
		}
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Dead:
			// TODO: if it isn't called anywhere in known sources, maybe
			// it should be marked as "export"?
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Internal:
			// TODO: maybe we should use "package"
			break;
		case CiVisibility.Public:
			break;
		}
	}

	void ICiSymbolVisitor.Visit(CiEnum enu)
	{
		WriteLine();
		Write(enu.Documentation);
		Write(enu.Visibility);
		Write("enum ");
		WriteLine(enu.Name);
		OpenBlock();
		bool first = true;
		foreach (CiEnumValue value in enu.Values) {
			if (first)
				first = false;
			else
				WriteLine(",");
			Write(value.Documentation);
			Write(value.Name);
		}
		WriteLine();
		CloseBlock();
	}

	void WriteBaseType(CiType type)
	{
		if (type is CiStringType)
			Write("string");
		else if (type is CiByteType)
			Write("ubyte");
		else
			Write(type.Name);
	}

	void Write(CiType type)
	{
		WriteBaseType(type.BaseType);
		for (int i = 0; i < type.ArrayLevel; i++)
			Write("[]");
	}

	bool WriteInit(CiType type)
	{
		CiClassStorageType classType = type as CiClassStorageType;
		if (classType != null) {
			Write(" = new ");
			Write(classType.Class.Name);
			return true;
		}
		CiArrayStorageType arrayType = type as CiArrayStorageType;
		if (arrayType != null) {
			Write(" = new ");
			WriteBaseType(arrayType.BaseType);
			WriteInitializer(arrayType);
			return true;
		}
		return false;
	}
	
	void ICiSymbolVisitor.Visit(CiField field)
	{
		Write(field.Documentation);
		Write(field.Visibility);
		Write(field.Type);
		Write(' ');
		WriteVarName(field.Name);
		WriteLine(";");
	}

	void ICiSymbolVisitor.Visit(CiConst konst)
	{
		if (konst.Visibility != CiVisibility.Public)
			return;
		Write(konst.Documentation);
		Write("public static immutable(");
		Write(konst.Type);
		Write(") ");
		WriteVarName(konst.Name);
		Write(" = ");
		WriteConst(konst.Value);
		WriteLine(";");
	}

	protected override int GetPriority(CiExpr expr)
	{
		// TODO: check if this compatible with D priorities
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiIntType.SByteProperty || prop == CiIntType.LowByteProperty)
				return 2;
		}
		else if (expr is CiCoercion) {
			CiCoercion c = (CiCoercion) expr;
			if (c.ResultType == CiByteType.Value && c.Inner.Type == CiIntType.Value)
				return 2;
		}
		return base.GetPriority(expr);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiIntType.SByteProperty) {
			Write("cast(byte) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiIntType.LowByteProperty) {
			Write("cast(ubyte) ");
			WriteChild(expr, expr.Obj);
		}
		else if (expr.Property == CiStringType.LengthProperty) {
			Write("cast(int) ");
			WriteChild(expr, expr.Obj);
			Write(".length");
		}
		else
			throw new ArgumentException(expr.Property.Name);
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiIntType.MulDivMethod) {
			Write("cast(int) (cast(long) ");
			WriteChild(2, expr.Obj);
			Write(" * ");
			WriteChild(3, expr.Arguments[0]);
			Write(" / ");
			WriteNonAssocChild(3, expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(']');
		}
		else if (expr.Method == CiStringType.SubstringMethod) {
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(" .. (");
			Write(expr.Arguments[0]);
			Write(") + ");
			Write(expr.Arguments[1]);
			Write(']');
		}
		else if (expr.Method == CiArrayType.CopyToMethod) {
			Write(expr.Arguments[1]);
			Write('[');
			Write(expr.Arguments[2]);
			Write(" .. (");
			Write(expr.Arguments[2]);
			Write(") + ");
			Write(expr.Arguments[3]);
			Write("] = ");
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(" .. (");
			Write(expr.Arguments[0]);
			Write(") + ");
			Write(expr.Arguments[3]);
			Write(']');
		}
		else if (expr.Method == CiArrayType.ToStringMethod) {
			Write("toUTF8(cast(char[]) ");
			Write(expr.Obj);
			Write('[');
			Write(expr.Arguments[0]);
			Write(" .. (");
			Write(expr.Arguments[0]);
			Write(") + ");
			Write(expr.Arguments[1]);
			Write("])");
		}
		else if (expr.Method == CiArrayStorageType.ClearMethod) {
			Write(expr.Obj);
			Write("[] = 0");
		}
		else
			base.Write(expr);
	}

	protected override void Write(CiCoercion expr)
	{
		if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
			Write("cast(ubyte) ");
			WriteChild(expr, (CiExpr) expr.Inner); // TODO: Assign
		}
		else
			base.Write(expr);
	}

	public override void Visit(CiVar stmt)
	{
		Write(stmt.Type);
		Write(' ');
		WriteVarName(stmt.Name);
		if (!WriteInit(stmt.Type) && stmt.InitialValue != null) {
			Write(" = ");
			Write(stmt.InitialValue);
		}
	}

	protected override void EndCase(CiCase kase)
	{
		if (kase.Fallthrough) {
			Write("goto ");
			if (kase.FallthroughTo != null) {
				Write("case ");
				Write(kase.FallthroughTo);
			}
			else
				Write("default");
			WriteLine(";");
		}
	}

	protected override void EndSwitch(CiCase[] kases)
	{
		foreach (CiCase kase in kases)
			if (kase.Value == null)
				return;
		WriteLine("default:");
		this.Indent++;
		WriteLine("break;");
		this.Indent--;
	}

	public override void Visit(CiThrow stmt)
	{
		Write("throw new Exception(");
		Write(stmt.Message);
		WriteLine(");");
	}

	void WriteSignature(CiDelegate del, string name)
	{
		Write(del.ReturnType);
		Write(' ');
		WriteVarName(name);
		Write('(');
		bool first = true;
		foreach (CiParam param in del.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Type);
			Write(' ');
			WriteVarName(param.Name);
		}
		Write(')');
	}

	void ICiSymbolVisitor.Visit(CiMethod method)
	{
		WriteLine();
		Write(method.Documentation);
		bool paramsStarted = false;
		foreach (CiParam param in method.Signature.Params) {
			if (param.Documentation != null) {
				if (!paramsStarted) {
					WriteLine("/// Params:");
					paramsStarted = true;
				}
				Write("/// ");
				WriteVarName(param.Name);
				Write(" = ");
				Write(param.Documentation.Summary);
				WriteLine();
			}
		}

		Write(method.Visibility);
		switch (method.CallType) {
		case CiCallType.Static: Write("static "); break;
		case CiCallType.Normal: if (method.Visibility != CiVisibility.Private) Write("final "); break;
		case CiCallType.Abstract: Write("abstract "); break;
		case CiCallType.Virtual: break;
		case CiCallType.Override: Write("override "); break;
		}
		WriteSignature(method.Signature, method.Name);
		if (method.CallType == CiCallType.Abstract)
			WriteLine(";");
		else {
			WriteLine();
			Write(method.Body);
		}
	}

	void ICiSymbolVisitor.Visit(CiClass klass)
	{
		WriteLine();
		Write(klass.Documentation);
		Write(klass.Visibility);
		OpenClass(klass.IsAbstract, klass, " : ");
		CurrentClass = klass;
		bool hasConstructor = klass.Constructor != null;
		foreach (CiSymbol member in klass.Members) {
			if (!hasConstructor) {
				CiField field = member as CiField;
				if (field != null && (field.Type is CiClassStorageType || field.Type is CiArrayStorageType))
					hasConstructor = true;
			}
			member.Accept(this);
		}
		foreach (CiConst konst in klass.ConstArrays) {
			if (konst.Visibility != CiVisibility.Public) {
				Write("static immutable(");
				Write(konst.Type);
				Write(") ");
				Write(konst.Class == CurrentClass ? konst.Name : konst.GlobalName);
				Write(" = ");
				WriteConst(konst.Value);
				WriteLine(";");
			}
		}
		foreach (CiBinaryResource resource in klass.BinaryResources) {
			// FIXME: it's better to declare them as immutable(ubyte)[]
			// or immutable(char)[] and import from binary files,
			// rather than pasting tons of magic numbers in the source.
			Write("static ubyte[] ");
			WriteName(resource);
			Write(" = ");
			WriteConst(resource.Content);
			WriteLine(";");
		}
		if (hasConstructor) {
			WriteLine("this()");
			OpenBlock();
			foreach (CiSymbol member in klass.Members) {
				CiField field = member as CiField;
				if (field != null && (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)) {
					WriteVarName(field.Name);
					WriteInit(field.Type);
					WriteLine(";");						
				}
			} 
			if (klass.Constructor != null)
				Write(klass.Constructor.Body.Statements);
			CloseBlock();
		}
		CloseBlock();
		CurrentClass = null;
	}

	void ICiSymbolVisitor.Visit(CiDelegate del)
	{
		// TODO: test this
		Write(del.Documentation);
		Write(del.Visibility);
		WriteSignature(del, "delegate");
		Write(' ');
		Write(del.Name);
		WriteLine(";");
	}

	public override void Write(CiProgram prog)
	{
		CreateFile(this.OutputFile);
		WriteLine("import std.utf;");
		foreach (CiSymbol symbol in prog.Globals)
			symbol.Accept(this);
		CloseFile();
	}
}

}
