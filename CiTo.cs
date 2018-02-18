// CiTo.cs - Ci translator
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
		Console.WriteLine("-l cs      Translate to C#");
		Console.WriteLine("-l java    Translate to Java");
		Console.WriteLine("-l js      Translate to JavaScript");
		Console.WriteLine("-l js-ta   Translate to JavaScript with Typed Arrays");
		Console.WriteLine("-o FILE    Write to the specified file");
		Console.WriteLine("-n NAME    Specify C# namespace or Java/ActionScript/Perl package");
		Console.WriteLine("-D NAME    Define conditional compilation symbol");
		Console.WriteLine("--help     Display this information");
		Console.WriteLine("--version  Display version information");
	}

	public static int Main(string[] args)
	{
		CiParser parser = new CiParser();
		List<string> inputFiles = new List<string>();
		string lang = null;
		string outputFile = null;
		string namespace_ = null;
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
				case "-l":
					lang = args[++i];
					break;
				case "-o":
					outputFile = args[++i];
					break;
				case "-n":
					namespace_ = args[++i];
					break;
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
		if (lang == null && outputFile != null) {
			string ext = Path.GetExtension(outputFile);
			if (ext.Length >= 2)
				lang = ext.Substring(1);
		}
		if (lang == null || outputFile == null || inputFiles.Count == 0) {
			Usage();
			return 1;
		}
		GenBase gen;
		switch (lang) {
		case "cs": gen = new GenCs(namespace_); break;
		case "java": gen = new GenJava(namespace_); break;
		case "js": gen = new GenJs(); break;
		case "js-ta": gen = new GenJsWithTypedArrays(); break;
		default: throw new ArgumentException("Unknown language: " + lang);
		}
		gen.OutputFile = outputFile;

		CiProgram program;
		try {
			foreach (string inputFile in inputFiles)
				parser.Parse(inputFile, File.OpenText(inputFile));
			program = parser.Program;
			new CiResolver(program);
		} catch (CiException ex) {
			Console.Error.WriteLine("{0}({1}): ERROR: {2}", ex.Filename, ex.Line, ex.Message);
			return 1;
//			throw;
		}

		gen.Write(program);
		return 0;
	}
}

}
