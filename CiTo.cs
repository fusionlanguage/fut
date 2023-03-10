// CiTo.cs - Ci transpiler
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
using System.Globalization;
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
		Console.WriteLine("-l d       Translate to D");
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

	static CiProgram ParseAndResolve(CiConsoleParser parser, CiSystem system, CiScope parent, List<string> files, FileResourceSema sema)
	{
		parser.Program = new CiProgram { Parent = parent, System = system };
		foreach (string file in files) {
			byte[] input = File.ReadAllBytes(file);
			parser.Parse(file, input, input.Length);
		}
		if (parser.HasErrors)
			return null;
		sema.Process(parser.Program);
		if (sema.HasErrors)
			return null;
		return parser.Program;
	}

	static bool Emit(CiProgram program, string lang, string namespace_, string outputFile)
	{
		GenBase gen;
		switch (lang) {
		case "c": gen = new GenC(); break;
		case "cpp": gen = new GenCpp(); break;
		case "cs": gen = new GenCs(); break;
		case "d": gen = new GenD(); break;
		case "java": gen = new GenJava(); break;
		case "js": case "mjs": gen = new GenJs(); break;
		case "py": gen = new GenPy(); break;
		case "swift": gen = new GenSwift(); break;
		case "ts": gen = new GenTs().WithGenFullCode(); break;
		case "d.ts": gen = new GenTs(); break;
		case "cl": gen = new GenCl(); break;
		default: throw new ArgumentException("Unknown language: " + lang);
		}
		gen.Namespace = namespace_;
		gen.OutputFile = outputFile;
		gen.WriteProgram(program);
		return !gen.HasErrors;
	}

	public static int Main(string[] args)
	{
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		CiConsoleParser parser = new CiConsoleParser();
		List<string> inputFiles = new List<string>();
		List<string> referencedFiles = new List<string>();
		FileResourceSema sema = new FileResourceSema();
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
					Console.WriteLine("cito 2.1.2");
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
					parser.AddPreSymbol(symbol);
					break;
				case "-r":
					referencedFiles.Add(args[++i]);
					break;
				case "-I":
					sema.AddResourceDir(args[++i]);
					break;
				default:
					throw new ArgumentException("Unknown option: " + arg);
				}
			}
			else {
				inputFiles.Add(arg);
			}
		}
		if (outputFile == null || inputFiles.Count == 0) {
			Usage();
			return 1;
		}

		CiSystem system = CiSystem.New();
		CiProgram program;
		CiScope parent = system;
		if (referencedFiles.Count > 0) {
			parent = ParseAndResolve(parser, system, parent, referencedFiles, sema);
			if (parent == null)
				return 1;
		}
		program = ParseAndResolve(parser, system, parent, inputFiles, sema);
		if (program == null)
			return 1;

		if (lang != null)
			return Emit(program, lang, namespace_, outputFile) ? 0 : 1;
		for (int i = outputFile.Length; --i >= 0; ) {
			char c = outputFile[i];
			if (c == '.') {
				if (i >= 2
				 && (outputFile[i - 2] == '.' || outputFile[i - 2] == ',')
				 && string.CompareOrdinal(outputFile, i - 1, "d.ts", 0, 4) == 0
				 && (i + 3 == outputFile.Length || outputFile[i + 3] == ','))
					continue;
				string outputBase = outputFile.Substring(0, i + 1);
				foreach (string outputExt in outputFile.Substring(i + 1).Split(',')) {
					if (!Emit(program, outputExt, namespace_, outputBase + outputExt))
						return 1;
				}
				return 0;
			}
			if (c == Path.DirectorySeparatorChar
			 || c == Path.AltDirectorySeparatorChar
			 || c == Path.VolumeSeparatorChar)
				break;
		}
		throw new ArgumentException("Don't know what language to translate to: no extension in '{0}' and no '-l' option");
	}
}

}
