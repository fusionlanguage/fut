// CiTo.cs - Ci translator
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

public class CiTo
{
	static void Usage()
	{
		Console.WriteLine("Usage: cito [OPTIONS] -l LANG INPUT.ci");
		Console.WriteLine("Options:");
		Console.WriteLine("-l cs    Translate to C#");
		Console.WriteLine("-l java  Translate to Java");
		Console.WriteLine("-o FILE  Write to the specified file (C#) or directory (Java)");
		Console.WriteLine("--help   This help");
	}

	public static int Main(string[] args)
	{
		string inputPath = null;
		string lang = null;
		string outputPath = null;
		for (int i = 0; i < args.Length; i++) {
			string arg = args[i];
			if (arg[0] == '-') {
				switch (arg) {
				case "--help":
					Usage();
					return 0;
				case "--version":
					Console.WriteLine("cito 0.1.0");
					return 0;
				case "-l":
					lang = args[++i];
					break;
				case "-o":
					outputPath = args[++i];
					break;
				default:
					throw new ApplicationException("Unknown option: " + arg);
				}
			}
			else {
				if (inputPath != null)
					throw new ApplicationException("Only one input file allowed!");
				inputPath = arg;
			}
		}
		if (lang == null || inputPath == null) {
			Usage();
			return 1;
		}
		CiLexer lexer = new CiLexer(File.OpenText(inputPath));
//		lexer.Debug();
		CiParser parser = new CiParser(lexer);
		CiProgram program;
		try {
			program = parser.ParseProgram();
		} catch (ParseException) {
			Console.Error.WriteLine("at line {0}", lexer.InputLineNo);
			throw;
		}
		SourceGenerator gen;
		switch (lang) {
		case "cs": gen = new GenCs(outputPath); break;
		case "java": gen = new GenJava(outputPath); break;
		default: throw new ApplicationException("Unknown language: " + lang);
		}
		gen.Write(program);
		return 0;
	}
}
}
