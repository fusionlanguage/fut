// CiTo.cs - Ci translator
//
// Copyright (C) 2011-2022  Piotr Fusik
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
		Console.WriteLine("-l c       Translate to C");
		Console.WriteLine("-l cpp     Translate to C++");
		Console.WriteLine("-l cs      Translate to C#");
		Console.WriteLine("-l java    Translate to Java");
		Console.WriteLine("-l js      Translate to JavaScript");
		Console.WriteLine("-l py      Translate to Python");
		Console.WriteLine("-l swift   Translate to Swift");
		Console.WriteLine("-l ts      Translate to TypeScript");
		Console.WriteLine("-l d.ts    Translate to TypeScript declarations");
		Console.WriteLine("-l cl      Translate to OpenCL C");
		Console.WriteLine("-o FILE    Write to the specified file");
		Console.WriteLine("-n NAME    Specify C++/C# namespace, Java package or C name prefix");
		Console.WriteLine("-D NAME    Define conditional compilation symbol");
		Console.WriteLine("-r FILE.ci Read the specified source file but don't emit code");
		Console.WriteLine("-I DIR     Add directory to resource search path");
		Console.WriteLine("--help     Display this information");
		Console.WriteLine("--version  Display version information");
	}

	static CiProgram ParseAndResolve(CiParser parser, CiScope parent, List<string> files, List<string> searchDirs, string lang)
	{
		parser.Program = new CiProgram { Parent = parent };
		foreach (string file in files)
			parser.Parse(file, File.ReadAllBytes(file));
		new CiResolver(parser.Program, searchDirs, lang);
		return parser.Program;
	}

	public static int Main(string[] args)
	{
		CiParser parser = new CiParser();
		List<string> inputFiles = new List<string>();
		List<string> referencedFiles = new List<string>();
		List<string> searchDirs = new List<string>();
		List<string> langs =  new List<string>();
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
					while (!args[i + 1].StartsWith('-')){
						var lang = args[++i];
						langs.Add(lang);
					};
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
				case "-r":
					referencedFiles.Add(args[++i]);
					break;
				case "-I":
					searchDirs.Add(args[++i]);
					break;
				default:
					throw new ArgumentException("Unknown option: " + arg);
				}
			}
			else {
				inputFiles.Add(arg);
			}
		}
		if (langs.Count==0 && outputFile != null) {
			if (outputFile.EndsWith(".d.ts"))
				langs.Add( "d.ts");
			else {
				string ext = Path.GetExtension(outputFile);
				if (ext.Length >= 2) // have an extension?
					langs.Add(  ext.Substring(1)); // skip the dot
			}
		}
		if (langs.Count==0 || outputFile == null || inputFiles.Count == 0) {
			Usage();
			return 1;
		}
		try {
			foreach (var lang in langs) {
				GenBase gen;
				switch (lang) {
				case "c": gen = new GenC(); break;
				case "cpp": gen = new GenCpp(); break;
				case "cs": gen = new GenCs(); break;
				case "java": gen = new GenJava(); break;
				case "js": gen = new GenJs(); break;
				case "py": gen = new GenPy(); break;
				case "swift": gen = new GenSwift(); break;
				case "ts": gen = new GenTs().WithGenFullCode(); break;
				case "d.ts": gen = new GenTs(); break;
				case "cl": gen = new GenCl(); break;
				default: throw new ArgumentException("Unknown language: " + lang);
				}
				gen.Namespace = namespace_;
				gen.OutputFile = Path.ChangeExtension(outputFile,lang);

				CiScope parent = CiSystem.Value;
					if (referencedFiles.Count > 0)
						parent = ParseAndResolve(parser, parent, referencedFiles, searchDirs, lang);
					CiProgram	program = ParseAndResolve(parser, parent, inputFiles, searchDirs, lang);
			

				gen.Write(program);
			}
		}
		catch (CiException ex) {
			Console.Error.WriteLine("{0}({1}): ERROR: {2}", ex.Filename, ex.Line, ex.Message);
			return 1;
//			throw;
		}
		return 0;
	}
}

}
