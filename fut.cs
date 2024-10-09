// fut.cs - Fusion transpiler
//
// Copyright (C) 2011-2024  Piotr Fusik
//
// This file is part of Fusion Transpiler,
// see https://github.com/fusionlanguage/fut
//
// Fusion Transpiler is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Fusion Transpiler is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Fusion Transpiler.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

[assembly: AssemblyTitle("fut")]
[assembly: AssemblyDescription("Fusion Transpiler")]

namespace Fusion
{

class FileGenHost : FuConsoleHost
{
	readonly List<string> ResourceDirs = new List<string>();
	string Filename;
	TextWriter CurrentFile;

	public void AddResourceDir(string path) => this.ResourceDirs.Add(path);

	List<byte> ReadResource(string name, FuPrefixExpr expr)
	{
		foreach (string dir in this.ResourceDirs) {
			string path = Path.Combine(dir, name);
			if (File.Exists(path))
				return File.ReadAllBytes(path).ToList();
		}
		if (File.Exists(name))
			return File.ReadAllBytes(name).ToList();
		ReportStatementError(expr, $"File '{name}' not found");
		return new List<byte>();
	}

	internal override int GetResourceLength(string name, FuPrefixExpr expr)
	{
		if (!this.Program.Resources.TryGetValue(name, out List<byte> content)) {
			content = ReadResource(name, expr);
			this.Program.Resources.Add(name, content);
		}
		return content.Count;
	}

	public override TextWriter CreateFile(string directory, string filename)
	{
		if (directory != null)
			filename = Path.Combine(directory, filename);
		this.Filename = filename;
		this.CurrentFile = new StreamWriter(filename);
		return this.CurrentFile;
	}

	public override void CloseFile()
	{
		this.CurrentFile.Close();
		if (this.HasErrors)
			File.Delete(this.Filename);
	}
}

public static class Fut
{
	static void Usage()
	{
		Console.WriteLine("Usage: fut [OPTIONS] -o FILE INPUT.fu");
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
		Console.WriteLine("-r FILE.fu Read the specified source file but don't emit code");
		Console.WriteLine("-I DIR     Add directory to resource search path");
		Console.WriteLine("--help     Display this information");
		Console.WriteLine("--version  Display version information");
	}

	static FuProgram ParseAndResolve(FuParser parser, FuSystem system, FuScope parent, List<string> files, FuSema sema, FuConsoleHost host)
	{
		host.Program = new FuProgram { Parent = parent, System = system };
		foreach (string file in files) {
			byte[] input = File.ReadAllBytes(file);
			parser.Parse(file, input, input.Length);
		}
		if (host.HasErrors)
			return null;
		sema.Process();
		if (host.HasErrors)
			return null;
		return host.Program;
	}

	static void Emit(FuProgram program, string lang, string namespace_, string outputFile, FileGenHost host)
	{
		GenBase gen;
		switch (lang) {
		case "c":
			gen = new GenC();
			break;
		case "cpp":
			gen = new GenCpp();
			break;
		case "cs":
			gen = new GenCs();
			break;
		case "d":
			gen = new GenD();
			break;
		case "java":
			gen = new GenJava();
			if (!Directory.Exists(outputFile))
				outputFile = Path.GetDirectoryName(outputFile);
			break;
		case "js":
		case "mjs":
			gen = new GenJs();
			break;
		case "py":
			gen = new GenPy();
			break;
		case "swift":
			gen = new GenSwift();
			break;
		case "ts":
			gen = new GenTs().WithGenFullCode();
			break;
		case "d.ts":
			gen = new GenTs();
			break;
		case "cl":
			gen = new GenCl();
			break;
		default:
			Console.Error.WriteLine($"fut: ERROR: Unknown language: {lang}");
			host.HasErrors = true;
			return;
		}
		gen.Namespace = namespace_;
		gen.OutputFile = outputFile;
		gen.SetHost(host);
		gen.WriteProgram(program);
	}

	public static int Main(string[] args)
	{
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		FileGenHost host = new FileGenHost();
		FuParser parser = new FuParser();
		List<string> inputFiles = new List<string>();
		List<string> referencedFiles = new List<string>();
		string lang = null;
		string outputFile = null;
		string namespace_ = "";
		for (int i = 0; i < args.Length; i++) {
			string arg = args[i];
			if (arg.Length < 2 || arg[0] != '-')
				inputFiles.Add(arg);
			else if (arg == "--help") {
				Usage();
				return 0;
			}
			else if (arg == "--version") {
				Console.WriteLine("Fusion Transpiler 3.2.7 (C#)");
				return 0;
			}
			else if (arg.Length == 2 && i + 1 < args.Length) {
				switch (arg[1]) {
				case 'l':
					lang = args[++i];
					break;
				case 'o':
					outputFile = args[++i];
					break;
				case 'n':
					namespace_ = args[++i];
					break;
				case 'D':
					string symbol = args[++i];
					if (symbol == "true" || symbol == "false") {
						Console.Error.WriteLine($"fut: ERROR: '{symbol}' is reserved");
						return 1;
					}
					parser.AddPreSymbol(symbol);
					break;
				case 'r':
					referencedFiles.Add(args[++i]);
					break;
				case 'I':
					host.AddResourceDir(args[++i]);
					break;
				default:
					Console.Error.WriteLine($"fut: ERROR: Unknown option: {arg}");
					return 1;
				}
			}
			else {
				Console.Error.WriteLine($"fut: ERROR: Unknown option: {arg}");
				return 1;
			}
		}
		if (outputFile == null || inputFiles.Count == 0) {
			Usage();
			return 1;
		}

		FuSema sema = new FuSema();
		parser.SetHost(host);
		sema.SetHost(host);
		FuSystem system = FuSystem.New();
		FuScope parent = system;
		try {
			if (referencedFiles.Count > 0) {
				parent = ParseAndResolve(parser, system, parent, referencedFiles, sema, host);
				if (parent == null)
					return 1;
			}
			FuProgram program = ParseAndResolve(parser, system, parent, inputFiles, sema, host);
			if (program == null)
				return 1;

			if (lang != null) {
				Emit(program, lang, namespace_, outputFile, host);
				return host.HasErrors ? 1 : 0;
			}
			for (int i = outputFile.Length; --i >= 0; ) {
				char c = outputFile[i];
				if (c == '.') {
					if (i >= 2
					 && (outputFile[i - 2] == '.' || outputFile[i - 2] == ',')
					 && string.CompareOrdinal(outputFile, i - 1, "d.ts", 0, 4) == 0
					 && (i + 3 == outputFile.Length || outputFile[i + 3] == ','))
						continue;
					string outputBase = outputFile.Substring(0, i + 1);
					int exitCode = 0;
					foreach (string outputExt in outputFile.Substring(i + 1).Split(',')) {
						Emit(program, outputExt, namespace_, outputBase + outputExt, host);
						if (host.HasErrors) {
							host.HasErrors = false;
							exitCode = 1;
						}
					}
					return exitCode;
				}
				if (c == Path.DirectorySeparatorChar
				 || c == Path.AltDirectorySeparatorChar
				 || c == Path.VolumeSeparatorChar)
					break;
			}
			Console.Error.WriteLine($"fut: ERROR: Don't know what language to translate to: no extension in '{outputFile}' and no '-l' option");
			return 1;
		}
		catch (IOException e) {
			Console.Error.WriteLine("fut: ERROR: " + e.Message);
			return 1;
		}
	}
}

}
