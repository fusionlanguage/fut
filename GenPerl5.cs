// GenPerl5.cs - Perl 5 code generator
//
// Copyright (C) 2013  Piotr Fusik
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

#define USE_INTEGER

using System;
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci
{

public abstract class GenPerl5 : SourceGenerator
{
	string Package;
	CiMethod CurrentMethod;

	protected GenPerl5(string package)
	{
		this.Package = package == null ? string.Empty : package + "::";
	}

	protected override void WriteBanner()
	{
		WriteLine("# Generated automatically with \"cito\". Do not edit.");
	}

	void WriteDoc(string text)
	{
		foreach (char c in text) {
			switch (c) {
			case '<': Write("E<lt>"); break;
			case '>': Write("E<gt>"); break;
			default: Write(c); break;
			}
		}
	}

	void Write(CiDocPara para)
	{
		foreach (CiDocInline inline in para.Children) {
			CiDocText text = inline as CiDocText;
			if (text != null) {
				WriteDoc(text.Text);
				continue;
			}
			CiDocCode code = inline as CiDocCode;
			if (code != null) {
				Write("C<");
				WriteDoc(code.Text);
				Write('>');
				continue;
			}
			throw new ArgumentException(inline.GetType().Name);
		}
		WriteLine();
		WriteLine();
	}

	void Write(CiDocBlock block)
	{
		CiDocList list = block as CiDocList;
		if (list != null) {
			WriteLine("=over");
			WriteLine();
			foreach (CiDocPara item in list.Items) {
				WriteLine("=item *");
				WriteLine();
				Write(item);
			}
			WriteLine("=back");
			WriteLine();
		}
	}

	protected override void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		Write(doc.Summary);
		foreach (CiDocBlock block in doc.Details)
			Write(block);
	}

	void WriteConstDoc(CiSymbol parent, CiSymbol child)
	{
		Write("=head2 C<");
		Write(this.Package);
		Write(parent.Name);
		Write("::");
		WriteUppercaseWithUnderscores(child.Name);
		WriteLine("()>");
		WriteLine();
		Write(child.Documentation);
	}

	void WritePackage(CiSymbol symbol)
	{
		Write("package ");
		Write(this.Package);
		Write(symbol.Name);
		WriteLine(";");
		WriteLine();
	}

	protected override void WriteConst(object value)
	{
		if (value is bool)
			Write((bool) value ? '1' : '0');
		else if (value is byte)
			Write((byte) value);
		else if (value is int)
			Write((int) value);
		else if (value is string) {
			Write('"');
			foreach (char c in (string) value) {
				switch (c) {
				case '\t': Write("\\t"); break;
				case '\r': Write("\\r"); break;
				case '\n': Write("\\n"); break;
				case '\\': Write("\\\\"); break;
				case '\"': Write("\\\""); break;
				case '$': Write("\\$"); break;
				case '@': Write("\\@"); break;
				default: Write(c); break;
				}
			}
			Write('"');
		}
		else if (value is CiEnumValue) {
			CiEnumValue ev = (CiEnumValue) value;
			Write(ev.Type.Name);
			Write("::");
			WriteUppercaseWithUnderscores(ev.Name);
			Write("()");
		}
		else if (value is Array) {
			Write("( ");
			WriteContent((Array) value);
			Write(" )");
		}
		else if (value == null)
			Write("undef");
		else
			throw new ArgumentException(value.ToString());
	}

	void WriteConst(string name, object value)
	{
		Write("sub ");
		WriteUppercaseWithUnderscores(name);
		Write("() { ");
		WriteConst(value);
		WriteLine(" }");
	}

	void Write(CiEnum enu)
	{
		if (enu.Visibility == CiVisibility.Public) {
			Write("=head1 Enum ");
			Write(this.Package);
			WriteLine(enu.Name);
			WriteLine();
			Write(enu.Documentation);
			foreach (CiEnumValue value in enu.Values)
				WriteConstDoc(enu, value);
			WriteLine("=cut");
			WriteLine();
		}
		WritePackage(enu);
		for (int i = 0; i < enu.Values.Length; i++)
			WriteConst(enu.Values[i].Name, i);
		WriteLine();
	}

	protected override CiPriority GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiLibrary.SByteProperty)
				return CiPriority.Additive;
			if (prop == CiLibrary.LowByteProperty)
				return CiPriority.And;
		}
#if !USE_INTEGER
		else if (expr is CiBinaryExpr) {
			if (((CiBinaryExpr) expr).Op == CiToken.Slash)
				return CiPriority.Postfix;
		}
#endif
		return base.GetPriority(expr);
	}

	protected override void WriteName(CiConst konst)
	{
		WriteUppercaseWithUnderscores(konst.GlobalName ?? konst.Name);
	}

	protected override void Write(CiVarAccess expr)
	{
		Write('$');
		if (expr.Var == this.CurrentMethod.This)
			Write("self");
		else
			Write(expr.Var.Name);
	}

	protected override void Write(CiFieldAccess expr)
	{
		WriteChild(CiPriority.Postfix, expr.Obj);
		Write("->{");
		WriteLowercaseWithUnderscores(expr.Field.Name);
		Write('}');
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiLibrary.SByteProperty) {
			Write('(');
			WriteChild(CiPriority.Xor, expr.Obj);
			Write(" ^ 128) - 128");
		}
		else if (expr.Property == CiLibrary.LowByteProperty) {
			WriteChild(expr, expr.Obj);
			Write(" & 0xff");
		}
		else if (expr.Property == CiLibrary.StringLengthProperty) {
			Write("length(");
			Write(expr.Obj);
			Write(')');
		}
		else
			throw new ArgumentException(expr.Property.Name);
	}

	protected override void Write(CiArrayAccess expr)
	{
		if (expr.Array is CiConstAccess || expr.Array is CiBinaryResourceExpr)
			Write('$');
		WriteChild(expr, expr.Array);
		if (expr.Array.Type is CiArrayPtrType)
			Write("->");
		Write('[');
		Write(expr.Index);
		Write(']');
	}

	bool WritePerlArray(string sigil, CiMaybeAssign expr)
	{
		bool isVar = expr is CiVarAccess;
		if (isVar || expr is CiConstAccess || expr is CiBinaryResourceExpr) {
			Write(sigil);
			if (isVar)
				Write(((CiVarAccess) expr).Var.Name);
			else
				Write((CiExpr) expr);
			return true;
		}
		return false;
	}

	void WriteSlice(CiExpr array, CiExpr index, CiExpr lenMinus1)
	{
		if (array is CiCoercion && WritePerlArray("@", ((CiCoercion) array).Inner)) {
			// ok: @var, @const, @binaryResource
		}
		else if (array.Type is CiArrayStorageType && WritePerlArray("@", array)) {
			// ok: @var, @const, @binaryResource
		}
		else {
			Write("@{");
			Write(array);
			Write('}');
		}
		Write('[');
		Write(index);
		Write(" .. ");
		WriteSum(index, lenMinus1);
		Write(']');
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiLibrary.MulDivMethod) {
#if USE_INTEGER
			// FIXME: overflow on 32-bit perl
			Write("(");
#else
			Write("int(");
#endif
			WriteMulDiv(CiPriority.Multiplicative, expr);
		}
		else if (expr.Method == CiLibrary.CharAtMethod) {
			Write("ord(substr(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", 1))");
		}
		else if (expr.Method == CiLibrary.SubstringMethod) {
			Write("substr(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiLibrary.ArrayCopyToMethod) {
			CiExpr lenMinus1 = new CiBinaryExpr { Left = expr.Arguments[3], Op = CiToken.Minus, Right = new CiConstExpr(1) };
			WriteSlice(expr.Arguments[1], expr.Arguments[2], lenMinus1);
			Write(" = ");
			WriteSlice(expr.Obj, expr.Arguments[0], lenMinus1);
		}
		else if (expr.Method == CiLibrary.ArrayToStringMethod) {
			CiExpr lenMinus1 = new CiBinaryExpr { Left = expr.Arguments[1], Op = CiToken.Minus, Right = new CiConstExpr(1) };
			Write("pack('U*', ");
			WriteSlice(expr.Obj, expr.Arguments[0], lenMinus1);
			Write(')');
		}
		else if (expr.Method == CiLibrary.ArrayStorageClearMethod) {
			Write('@');
			if (expr.Obj is CiVarAccess)
				Write(((CiVarAccess) expr.Obj).Var.Name);
			else {
				Write('{');
				Write(expr.Obj);
				Write('}');
			}
			Write(" = (0) x ");
			Write(((CiArrayStorageType) expr.Obj.Type).Length);
		}
		else {
			if (expr.Method != null) {
				if (expr.Obj != null) {
					Write(expr.Obj);
					Write("->");
				}
				else {
					Write(this.Package);
					Write(expr.Method.Class.Name);
					Write("::");
				}
				WriteLowercaseWithUnderscores(expr.Method.Name);
			}
			else {
				// delegate call
				Write(expr.Obj);
				Write("->");
			}
			WriteArguments(expr);
		}
	}

	void WriteDefined(CiExpr expr)
	{
		Write("defined(");
		Write(expr);
		Write(')');
	}

	protected override void Write(CiBinaryExpr expr)
	{
		switch (expr.Op) {
		case CiToken.Equal:
		case CiToken.NotEqual:
			if (expr.Left.IsConst(null)) {
				// null != thing -> defined(thing)
				// null == thing -> !defined(thing)
				if (expr.Op == CiToken.Equal)
					Write('!');
				WriteDefined(expr.Right);
			}
			else if (expr.Right.IsConst(null)) {
				// thing != null -> defined(thing)
				// thing == null -> !defined(thing)
				if (expr.Op == CiToken.Equal)
					Write('!');
				WriteDefined(expr.Left);
			}
			else if (expr.Left.Type is CiStringType) {
				WriteChild(expr, expr.Left);
				Write(expr.Op == CiToken.Equal ? " eq " : " ne ");
				WriteChild(expr, expr.Right);
			}
			else
				base.Write(expr);
			break;
#if !USE_INTEGER
		case CiToken.Slash:
			Write("int(");
			WriteChild(3, expr.Left);
			Write(" / ");
			WriteNonAssocChild(3, expr.Right);
			Write(')');
			break;
#endif
		default:
			base.Write(expr);
			break;
		}
	}

	protected override void WriteNew(CiType type)
	{
		CiClassStorageType classType = type as CiClassStorageType;
		if (classType != null) {
			Write(this.Package);
			Write(classType.Class.Name);
			Write("->new()");
		}
		else
			Write("[]"); // new array reference
	}

	protected override void Write(CiCoercion expr)
	{
		if (expr.Inner.Type is CiArrayStorageType && WritePerlArray("\\@", expr.Inner)) {
			// ok: \@var, \@const, \@binaryResource
		}
		else
			base.Write(expr);
	}

	public override void Visit(CiBlock block)
	{
		// Avoid blocks, as they count as loops for last/next.
		// At worst we'll get warning about duplicate "my" declarations.
		Write(block.Statements);
	}

	protected override void WriteChild(ICiStatement stmt)
	{
		Write(' ');
		OpenBlock();
		Write(stmt);
		CloseBlock();
	}

	public override void Visit(CiAssign assign)
	{
		if (assign.Op == CiToken.AddAssign && assign.Target.Type is CiStringStorageType) {
			Write(assign.Target);
			Write(" .= ");
			WriteInline(assign.Source);
		}
		else
			base.Visit(assign);
	}

	protected bool BreakDoWhile = false;

	public override void Visit(CiBreak stmt)
	{
		if (this.BreakDoWhile)
			WriteLine("last DOWHILE;");
		else
			WriteLine("last;");
	}

	public override abstract void Visit(CiContinue stmt);

	protected static bool HasBreak(ICiStatement stmt)
	{
		// note: support stmt==null from ifStmt.OnFalse
		if (stmt is CiBreak)
			return true;
		CiIf ifStmt = stmt as CiIf;
		if (ifStmt != null)
			return HasBreak(ifStmt.OnTrue) || HasBreak(ifStmt.OnFalse);
		CiBlock block = stmt as CiBlock;
		if (block != null)
			return block.Statements.Any(s => HasBreak(s));
		return false;
	}

	protected static bool HasContinue(ICiStatement stmt)
	{
		// note: support stmt==null from ifStmt.OnFalse
		if (stmt is CiContinue)
			return true;
		CiIf ifStmt = stmt as CiIf;
		if (ifStmt != null)
			return HasContinue(ifStmt.OnTrue) || HasContinue(ifStmt.OnFalse);
		CiBlock block = stmt as CiBlock;
		if (block != null)
			return block.Statements.Any(s => HasContinue(s));
		CiSwitch switchStmt = stmt as CiSwitch;
		if (switchStmt != null)
			return switchStmt.Cases.Any(kase => kase.Body.Any(s => HasContinue(s)));
		return false;
	}

	protected virtual void WriteLoopLabel(CiLoop stmt)
	{
	}

	public override void Visit(CiDoWhile stmt)
	{
		bool hasBreak = HasBreak(stmt.Body);
		bool hasContinue = HasContinue(stmt.Body);
		bool oldBreakDoWhile = this.BreakDoWhile;
		if (hasBreak) {
			// { do { ... last; ... } while cond; }
			if (hasContinue) {
				// DOWHILE: { do { { ... last DOWHILE; ... next; ... } } while cond; }
				this.BreakDoWhile = true;
				Write("DOWHILE: ");
			}
			OpenBlock();
		}
		Write("do");
		if (hasContinue) {
			// do { { ... next; ... } } while cond;
			Write(' ');
			OpenBlock();
			WriteLoopLabel(stmt);
			WriteChild(stmt.Body);
			CloseBlock();
		}
		else
			WriteChild(stmt.Body);
		Write("while ");
		Write(stmt.Cond);
		WriteLine(";");
		if (hasBreak) {
			this.BreakDoWhile = oldBreakDoWhile;
			CloseBlock();
		}
	}

	public override void Visit(CiFor stmt)
	{
		bool oldBreakDoWhile = this.BreakDoWhile;
		this.BreakDoWhile = false;
		WriteLoopLabel(stmt);
		base.Visit(stmt);
		this.BreakDoWhile = oldBreakDoWhile;
	}

	public override void Visit(CiWhile stmt)
	{
		bool oldBreakDoWhile = this.BreakDoWhile;
		this.BreakDoWhile = false;
		WriteLoopLabel(stmt);
		base.Visit(stmt);
		this.BreakDoWhile = oldBreakDoWhile;
	}

	public override void Visit(CiIf stmt)
	{
		Write("if (");
		Write(stmt.Cond);
		Write(')');
		WriteChild(stmt.OnTrue);
		if (stmt.OnFalse != null) {
			Write("els");
			if (stmt.OnFalse is CiIf)
				Write(stmt.OnFalse);
			else {
				Write('e');
				WriteChild(stmt.OnFalse);
			}
		}
	}

	public override void Visit(CiVar stmt)
	{
		Write("my ");
		Write(stmt.Type is CiArrayStorageType ? '@' : '$');
		Write(stmt.Name);
		if (stmt.InitialValue != null) {
			Write(" = ");
			if (stmt.Type is CiArrayStorageType) {
				Write("(0) x ");
				Write(((CiArrayStorageType) stmt.Type).Length);
			}
			else
				Write(stmt.InitialValue);
		}
		else if (stmt.Type is CiClassStorageType) {
			Write(" = ");
			WriteNew(stmt.Type);
		}
	}

	protected static int BodyLengthWithoutLastBreak(ICiStatement[] body)
	{
		int length = body.Length;
		if (length > 0 && body[length - 1] is CiBreak)
			return length - 1;
		return length;
	}

	public override abstract void Visit(CiSwitch stmt);

	public override void Visit(CiThrow stmt)
	{
		Write("die ");
		Write(stmt.Message);
		WriteLine(";");
	}

	void WriteDocName(CiParam param)
	{
		if (param.Type is CiArrayType)
			Write("\\@");
		else
			Write('$');
		Write(param.Name);
	}

	void Write(CiMethod method)
	{
		if (method.Visibility == CiVisibility.Public) {
			Write("=head2 C<");
			if (method.CallType == CiCallType.Static) {
				Write(this.Package);
				Write(method.Class.Name);
				Write("::");
			}
			else {
				Write('$');
				WriteLowercase(method.Class.Name);
				Write("-E<gt>");
			}
			WriteLowercaseWithUnderscores(method.Name);
			Write('(');
			bool first = true;
			foreach (CiParam param in method.Signature.Params) {
				if (first)
					first = false;
				else
					Write(", ");
				WriteDocName(param);
			}
			WriteLine(")>");
			WriteLine();
			Write(method.Documentation);
			if (method.Signature.Params.Any(param => param.Documentation != null)) {
				WriteLine("Parameters:");
				WriteLine();
				WriteLine("=over");
				WriteLine();
				foreach (CiParam param in method.Signature.Params) {
					Write("=item ");
					WriteDocName(param);
					WriteLine();
					WriteLine();
					Write(param.Documentation);
				}
				WriteLine("=back");
				WriteLine();
			}
			WriteLine("=cut");
			WriteLine();
		}
		if (method.CallType == CiCallType.Abstract)
			return;
		this.CurrentMethod = method;
		Write("sub ");
		WriteLowercaseWithUnderscores(method.Name);
		Write('(');
		if (method.CallType != CiCallType.Static)
			Write('$');
		foreach (CiParam param in method.Signature.Params)
			Write('$');
		Write(") ");
		OpenBlock();
		if (method.CallType != CiCallType.Static || method.Signature.Params.Length > 0) {
			Write("my (");
			bool first = true;
			if (method.CallType != CiCallType.Static) {
				Write("$self");
				first = false;
			}
			foreach (CiParam param in method.Signature.Params) {
				if (first)
					first = false;
				else
					Write(", ");
				Write('$');
				Write(param.Name);
			}
			WriteLine(") = @_;");
		}
		Write(method.Body.Statements);
		CloseBlock();
		WriteLine();
		this.CurrentMethod = null;
	}

	void WriteConstructor(CiClass klass)
	{
		// TODO: skip constructor if static methods only?

		if (klass.Visibility == CiVisibility.Public) {
			Write("=head2 C<$");
			WriteLowercase(klass.Name);
			Write(" = ");
			Write(this.Package);
			Write(klass.Name);
			WriteLine("-E<gt>new()>");
			WriteLine();
			if (klass.Constructor != null)
				Write(klass.Constructor.Documentation);
			WriteLine("=cut");
			WriteLine();
		}

		IEnumerable<CiField> classStorageFields = klass.Members
			.OfType<CiField>().Where(field => field.Type is CiClassStorageType);
		if (klass.Constructor == null && klass.BaseClass != null && !classStorageFields.Any()) {
			// base constructor does the job
			return;
		}

		Write("sub new($) ");
		OpenBlock();
		if (klass.BaseClass != null)
			WriteLine("my $self = shift()->SUPER::new();");
		else
			WriteLine("my $self = bless {}, shift;");
		foreach (CiField field in classStorageFields) {
			Write("$self->{");
			WriteLowercaseWithUnderscores(field.Name);
			Write("} = ");
			WriteNew(field.Type);
			WriteLine(";");
		}
		if (klass.Constructor != null) {
			this.CurrentMethod = klass.Constructor;
			Write(klass.Constructor.Body.Statements);
			this.CurrentMethod = null;
		}
		WriteLine("return $self;"); // TODO: premature returns
		CloseBlock();
		WriteLine();
	}

	void Write(CiClass klass)
	{
		if (klass.Visibility == CiVisibility.Public) {
			Write("=head1 Class ");
			Write(this.Package);
			WriteLine(klass.Name);
			WriteLine();
			Write(klass.Documentation);
			WriteLine("=cut");
			WriteLine();
		}
		WritePackage(klass);
		if (klass.BaseClass != null) {
			Write("our @ISA = '");
			Write(this.Package);
			Write(klass.BaseClass.Name);
			WriteLine("';");
		}

		foreach (CiSymbol member in klass.Members) {
			if (member.Visibility == CiVisibility.Public) {
				CiConst konst = member as CiConst;
				if (konst != null) {
					WriteConstDoc(klass, konst);
					WriteLine("=cut");
					WriteLine();
					WriteConst(konst.Name, konst.Value);
					WriteLine();
				}
			}
		}
		foreach (CiConst konst in klass.ConstArrays) {
			Write("our @");
			WriteUppercaseWithUnderscores(konst.GlobalName);
			Write(" = ");
			WriteConst(konst.Value);
			WriteLine(";");
			WriteLine();
		}
		foreach (CiBinaryResource resource in klass.BinaryResources) {
			Write("our @");
			WriteName(resource);
			Write(" = ");
			WriteConst(resource.Content);
			WriteLine(";");
			WriteLine();
		}

		WriteConstructor(klass);
		foreach (CiSymbol member in klass.Members)
			if (member is CiMethod)
				Write((CiMethod) member);
	}

	protected virtual void WritePragmas(CiProgram prog)
	{
	}

	public override void Write(CiProgram prog)
	{
		CreateFile(this.OutputFile);
		WritePragmas(prog);
#if USE_INTEGER
		WriteLine("use integer;");
#endif
		WriteLine("use strict;");
		WriteLine();

		// Write enums first, otherwise
		// given (foo) { when (Enum::VALUE()) { ... } }
		// won't work as expected.
		foreach (CiSymbol symbol in prog.Globals)
			if (symbol is CiEnum)
				Write((CiEnum) symbol);
		foreach (CiSymbol symbol in prog.Globals)
			if (symbol is CiClass)
				Write((CiClass) symbol);
		WriteLine("1;");
		CloseFile();
	}
}

}
