// CiResolver.cs - Ci symbol resolver
//
// Copyright (C) 2011-2023  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
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

public class CiResolver : CiSema
{
	readonly List<string> ResourceDirs = new List<string>();

	public void AddResourceDir(string path) => this.ResourceDirs.Add(path);

	byte[] ReadResource(string name, CiPrefixExpr expr)
	{
		foreach (string dir in this.ResourceDirs) {
			string path = Path.Combine(dir, name);
			if (File.Exists(path))
				return File.ReadAllBytes(path);
		}
		if (File.Exists(name))
			return File.ReadAllBytes(name);
		ReportError(expr, $"File {name} not found");
		return Array.Empty<byte>();
	}

	protected override int GetResourceLength(string name, CiPrefixExpr expr)
	{
		if (!this.Program.Resources.TryGetValue(name, out byte[] content)) {
			content = ReadResource(name, expr);
			this.Program.Resources.Add(name, content);
		}
		return content.Length;
	}

	protected override void ResolveCode(CiClass klass)
	{
		if (klass.Constructor != null) {
			this.CurrentScope = klass;
			this.CurrentMethod = klass.Constructor;
			klass.Constructor.Body.AcceptStatement(this);
			this.CurrentMethod = null;
		}
		for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
			if (symbol is CiMethod method) {
				if (method.Name == "ToString" && method.CallType != CiCallType.Static && method.Parameters.Count() == 0)
					method.Id = CiId.ClassToString;
				if (method.Body != null) {
					this.CurrentScope = method.Parameters;
					this.CurrentMethod = method;
					if (!(method.Body is CiScope))
						OpenScope(new CiScope()); // don't add "is Derived d" to parameters
					method.Body.AcceptStatement(this);
					if (method.Type.Id != CiId.VoidType && method.Body.CompletesNormally())
						ReportError(method.Body, "Method can complete without a return value");
					this.CurrentMethod = null;
				}
			}
		}
	}
}

}
