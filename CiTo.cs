// CiTo.cs - Ci translator
//
// Copyright (C) 2011-2013  Piotr Fusik
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
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("CiTo")]
[assembly: AssemblyDescription("Ci Translator")]

namespace Foxoft.Ci
{

public sealed class CiTo
{
	CiTo()
	{
	}

	static void Usage()
	{
		Console.WriteLine("Usage: cito [OPTIONS] -o FILE INPUT.ci");
		Console.WriteLine("Options:");
		Console.WriteLine("-l c       Translate to C89");
		Console.WriteLine("-l c99     Translate to C99");
		Console.WriteLine("-l java    Translate to Java");
		Console.WriteLine("-l cs      Translate to C#");
		Console.WriteLine("-l js      Translate to JavaScript");
		Console.WriteLine("-l js-ta   Translate to JavaScript with Typed Arrays");
		Console.WriteLine("-l as      Translate to ActionScript 3");
		Console.WriteLine("-l d       Translate to D");
		Console.WriteLine("-l pm      Translate to Perl 5 Module");
		Console.WriteLine("-l pm510   Translate to Perl 5.10+ Module");
		Console.WriteLine("-o FILE    Write to the specified file");
		Console.WriteLine("-n NAME    Specify C# namespace or Java/ActionScript/Perl package");
		Console.WriteLine("-D NAME    Define conditional compilation symbol");
		Console.WriteLine("-I DIR     Add directory to BinaryResource search path");
		Console.WriteLine("--help     Display this information");
		Console.WriteLine("--version  Display version information");
	}

	public static int Main(string[] args)
	{
		HashSet<string> preSymbols = new HashSet<string>();
		preSymbols.Add("true");
		List<string> inputFiles = new List<string>();
		List<string> searchDirs = new List<string>();
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
					Console.WriteLine("cito 0.4.0");
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
					preSymbols.Add(symbol);
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
		if (lang == null && outputFile != null) {
			string ext = Path.GetExtension(outputFile);
			if (ext.Length >= 2)
				lang = ext.Substring(1);
		}
		if (lang == null || outputFile == null || inputFiles.Count == 0) {
			Usage();
			return 1;
		}

		CiParser parser = new CiParser();
		parser.PreSymbols = preSymbols;
		foreach (string inputFile in inputFiles) {
			try {
				parser.Parse(inputFile, File.OpenText(inputFile));
			} catch (Exception ex) {
				Console.Error.WriteLine("{0}({1}): ERROR: {2}", inputFile, parser.InputLineNo, ex.Message);
				parser.PrintMacroStack();
				if (parser.CurrentMethod != null)
					Console.Error.WriteLine("   in method {0}", parser.CurrentMethod.Name);
				return 1;
//				throw;
			}
		}
		CiProgram program = parser.Program;

		CiResolver resolver = new CiResolver();
		resolver.SearchDirs = searchDirs;
		try {
			resolver.Resolve(program);
		} catch (Exception ex) {
			if (resolver.CurrentClass != null) {
				Console.Error.Write(resolver.CurrentClass.SourceFilename);
				Console.Error.Write(": ");
			}
			Console.Error.WriteLine("ERROR: {0}", ex.Message);
			if (resolver.CurrentMethod != null)
				Console.Error.WriteLine("   in method {0}", resolver.CurrentMethod.Name);
			return 1;
//			throw;
		}

		SourceGenerator gen;
		switch (lang) {
		case "c": gen = new GenC89(); break;
		case "c99": gen = new GenC(); break;
		case "java": gen = new GenJava(namespace_); break;
		case "cs": gen = new GenCs(namespace_); break;
		case "js": gen = new GenJs(); break;
		case "js-ta": gen = new GenJsWithTypedArrays(); break;
		case "as": gen = new GenAs(namespace_); break;
		case "d": gen = new GenD(); break;
		case "pm": gen = new GenPerl58(namespace_); break;
		case "pm510": gen = new GenPerl510(namespace_); break;
		default: throw new ArgumentException("Unknown language: " + lang);
		}
		gen.OutputFile = outputFile;
		gen.Write(program);
		return 0;
	}
}
}
