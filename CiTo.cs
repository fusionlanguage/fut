// CiTo.cs - Ci translator
//
// Copyright (C) 2011-2014  Piotr Fusik
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
using System.Reflection;

[assembly: AssemblyTitle("CiTo")]
[assembly: AssemblyDescription("Ci Translator")]

namespace Foxoft.Ci
{

public static class CiTo
{
	static void Usage()
	{
		Console.WriteLine("Usage: cito [OPTIONS] -o FILE INPUT.ci");
		Console.WriteLine("Options:");
		Console.WriteLine("-D NAME    Define conditional compilation symbol");
		Console.WriteLine("--help     Display this information");
		Console.WriteLine("--version  Display version information");
	}

	public static int Main(string[] args)
	{
		CiParser parser = new CiParser();
		List<string> inputFiles = new List<string>();
		for (int i = 0; i < args.Length; i++) {
			string arg = args[i];
			if (arg[0] == '-') {
				switch (arg) {
				case "--help":
					Usage();
					return 0;
				case "--version":
					Console.WriteLine("cito 1.0.0");
					return 0;
				case "-D":
					string symbol = args[++i];
					if (symbol == "true" || symbol == "false")
						throw new ArgumentException(symbol + " is reserved");
					parser.PreSymbols.Add(symbol);
					break;
				default:
					throw new ArgumentException("Unknown option: " + arg);
				}
			}
			else {
				inputFiles.Add(arg);
			}
		}
		if (inputFiles.Count == 0) {
			Usage();
			return 1;
		}

		foreach (string inputFile in inputFiles) {
			try {
				parser.Parse(inputFile, File.OpenText(inputFile));
			} catch (Exception ex) {
				Console.Error.WriteLine("{0}({1}): ERROR: {2}", inputFile, parser.Line, ex.Message);
				return 1;
//				throw;
			}
		}

		return 0;
	}
}
}
