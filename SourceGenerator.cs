// SourceGenerator.cs - base class for code generators
//
// Copyright (C) 2011  Piotr Fusik
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
using System.IO;

namespace Foxoft.Ci
{

public abstract class SourceGenerator
{
	TextWriter writer = Console.Out;
	int indent = 0;

	protected void Write(char c)
	{
		writer.Write(c);
	}

	protected void Write(string s)
	{
		writer.Write(s);
	}

	protected void Write(int i)
	{
		writer.Write(i);
	}

	protected void Write(string format, params object[] args)
	{
		writer.Write(format, args);
	}

	protected void WriteLine()
	{
		writer.WriteLine();
	}

	protected void WriteLine(string s)
	{
		writer.WriteLine(s);
	}

	protected void WriteLine(string format, params object[] args)
	{
		writer.WriteLine(format, args);
	}

	protected void OpenBlock()
	{
		WriteLine(" {");
		indent++;
	}

	protected void StartLine(string s)
	{
		for (int i = 0; i < indent; i++)
			Write('\t');
		Write(s);
	}

	protected void Print(string s)
	{
		StartLine(s);
		WriteLine();
	}

	protected void CloseBlock()
	{
		indent--;
		StartLine("}");
		WriteLine();
	}

	protected void WriteInitializer(CiArrayType type)
	{
		for (; type != null; type = type.ElementType as CiArrayType) {
			Write('[');
			CiArrayStorageType storageType = type as CiArrayStorageType;
			if (storageType != null)
				Write(storageType.Length);
			Write(']');
		}
	}

	protected void Write(CiExpr expr)
	{
		// TODO
	}

	protected void Write(CiBlock block)
	{
		OpenBlock();
		foreach (ICiStatement stmt in block.Statements)
			Write(stmt);
		CloseBlock();
	}

	protected void WriteChild(ICiStatement stmt)
	{
		if (stmt is CiBlock)
			Write((CiBlock) stmt);
		else {
			WriteLine();
			indent++;
			Write(stmt);
			indent--;
		}
	}

	protected abstract void Write(CiVar stmt);

	void Write(CiDoWhile stmt)
	{
		StartLine("do");
		WriteChild(stmt.Body);
		StartLine("while (");
		Write(stmt.Cond);
		WriteLine(");");
	}

	void Write(CiFor stmt)
	{
		StartLine("for (");
		if (stmt.Init != null)
			Write(stmt.Init);
		else
			Write(";");
		if (stmt.Cond != null)
			Write(stmt.Cond);
		if (stmt.Advance != null)
			Write(stmt.Advance);
		Write(";");
		Write(")");
		WriteChild(stmt.Body);
	}

	void Write(CiIf stmt)
	{
		StartLine("if (");
		Write(stmt.Cond);
		Write(")");
		WriteChild(stmt.OnTrue);
		if (stmt.OnFalse != null) {
			StartLine("else");
			WriteChild(stmt.OnFalse);
		}
	}

	void Write(CiReturn stmt)
	{
		if (stmt.Value == null)
			Print("return;");
		else {
			StartLine("return ");
			Write(stmt.Value);
			WriteLine(";");
		}
	}

	void Write(CiWhile stmt)
	{
		StartLine("while (");
		Write(stmt.Cond);
		Write(")");
		WriteChild(stmt.Body);
	}

	protected void Write(ICiStatement stmt)
	{
		if (stmt is CiBlock)
			Write((CiBlock) stmt);
		else if (stmt is CiVar)
			Write((CiVar) stmt);
		else if (stmt is CiBreak)
			Print("break;");
		else if (stmt is CiContinue)
			Print("continue;");
		else if (stmt is CiDoWhile)
			Write((CiDoWhile) stmt);
		else if (stmt is CiFor)
			Write((CiFor) stmt);
		else if (stmt is CiIf)
			Write((CiIf) stmt);
		else if (stmt is CiReturn)
			Write((CiReturn) stmt);
		else if (stmt is CiWhile)
			Write((CiWhile) stmt);
		// TODO
	}

	public abstract void Write(CiProgram prog);
}

}
