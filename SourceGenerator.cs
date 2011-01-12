// SourceGenerator.cs - base class for code generators
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

	public abstract void Write(CiProgram prog);
}

}
