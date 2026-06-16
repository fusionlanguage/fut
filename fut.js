// fut.js - Fusion transpiler
//
// Copyright (C) 2011-2026  Piotr Fusik
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

import fs from "node:fs";
import path from "node:path";
import { FuParser, FuProgram, FuSema, FuSystem, FuConsoleHost, GenC, GenCpp, GenCs, GenD, GenJava, GenJs, GenPy, GenSwift, GenTs, GenCl } from "./libfut.js";

class FileGenHost extends FuConsoleHost
{
	#resourceDirs = []
	#currentFile;

	addResourceDir(path)
	{
		this.#resourceDirs.push(path);
	}

	#readResource(name, expr)
	{
		for (const dir of this.#resourceDirs) {
			try {
				return fs.readFileSync(path.join(dir, name));
			}
			catch {
			}
		}
		try {
			return fs.readFileSync(name);
		}
		catch {
			this.reportStatementError(expr, `File '${name}' not found`);
		}
		return [];
	}

	getResourceLength(name, expr)
	{
		if (this.getResources().hasOwnProperty(name))
			return this.getResources()[name].length;
		const content = this.#readResource(name, expr);
		this.getResources()[name] = content;
		return content.length;
	}

	createFile(directory, filename)
	{
		if (directory != null)
			filename = path.join(directory, filename);
		this.#currentFile = fs.createWriteStream(filename);
		this.#currentFile.on("error", e => {
				console.error(`${filename}: ERROR: ${e.message}`);
				process.exitCode = 1;
			});
		return this.#currentFile;
	}

	closeFile()
	{
		if (this.hasErrors()) {
			const filename = this.#currentFile.path;
			this.#currentFile.close(() => fs.unlinkSync(filename));
		}
		else
			this.#currentFile.close();
	}

	toDirectory(outputFile)
	{
		try {
			if (fs.statSync(outputFile).isDirectory())
				return outputFile;
		}
		catch {
		}
		return path.dirname(outputFile);
	}
}

function usage()
{
	console.log("Usage: node fut.js [OPTIONS] -o FILE INPUT.fu");
	console.log("Options:");
	console.log("-l c       Translate to C");
	console.log("-l cpp     Translate to C++");
	console.log("-l cs      Translate to C#");
	console.log("-l d       Translate to D");
	console.log("-l java    Translate to Java");
	console.log("-l js      Translate to JavaScript");
	console.log("-l py      Translate to Python");
	console.log("-l swift   Translate to Swift");
	console.log("-l ts      Translate to TypeScript");
	console.log("-l d.ts    Translate to TypeScript declarations");
	console.log("-l cl      Translate to OpenCL C");
	console.log("-o FILE    Write to the specified file");
	console.log("-n NAME    Specify C++/C# namespace, Java package or C name prefix");
	console.log("-D NAME    Define conditional compilation symbol");
	console.log("-r FILE.fu Read the specified source file but don't emit code");
	console.log("-I DIR     Add directory to resource search path");
	console.log("--help     Display this information");
	console.log("--version  Display version information");
}

function parseAndResolve(parser, system, parent, files, sema, host)
{
	host.program = new FuProgram();
	host.program.parent = parent;
	host.program.system = system;
	for (const file of files) {
		const input = fs.readFileSync(file);
		parser.parse(file, input, input.length);
	}
	if (host.hasErrors())
		process.exit(1);
	sema.process();
	if (host.hasErrors())
		process.exit(1);
	return host.program;
}

function emitImplicitLang(program, namespace, outputFile, host)
{
	for (let i = outputFile.length; --i >= 0; ) {
		const c = outputFile.charAt(i);
		if (c == ".") {
			if (i >= 2 && /^[.,]d\.ts($|,)/.test(outputFile.slice(i - 2)))
				continue;
			const outputBase = outputFile.slice(0, i + 1);
			for (const outputExt of outputFile.slice(i + 1).split(",")) {
				host.emit(program, outputExt, namespace, outputBase + outputExt);
				if (host.hasErrors()) {
					host.setErrors(false);
					process.exitCode = 1;
				}
			}
			return;
		}
		if (c == "/" || c == "\\" || c == ":")
			break;
	}
	console.error(`fut: ERROR: Don't know what language to translate to: no extension in '${outputFile}' and no '-l' option`);
	process.exitCode = 1;
}

const host = new FileGenHost();
const parser = new FuParser();
const inputFiles = [];
const referencedFiles = [];
let lang = null;
let outputFile = null;
let namespace = "";
for (let i = 2; i < process.argv.length; i++) {
	const arg = process.argv[i];
	if (!arg.startsWith("-"))
		inputFiles.push(arg);
	else if (arg == "--help") {
		usage();
		process.exit(0);
	}
	else if (arg == "--version") {
		console.log("Fusion Transpiler", FuConsoleHost.VERSION, "(Node.js)");
		process.exit(0);
	}
	else if (i + 1 < process.argv.length) {
		switch (arg) {
		case "-l":
			lang = process.argv[++i];
			break;
		case "-o":
			outputFile = process.argv[++i];
			break;
		case "-n":
			namespace = process.argv[++i];
			break;
		case "-D":
			const symbol = process.argv[++i];
			if (symbol == "true" || symbol == "false") {
				console.error(`fut: ERROR: '${symbol}' is reserved`);
				process.exit(1);
			}
			parser.addPreSymbol(symbol);
			break;
		case "-r":
			referencedFiles.push(process.argv[++i]);
			break;
		case "-I":
			host.addResourceDir(process.argv[++i]);
			break;
		default:
			console.error(`fut: ERROR: Unknown option: ${arg}`);
			process.exit(1);
		}
	}
	else {
		console.error(`fut: ERROR: Unknown option: ${arg}`);
		process.exit(1);
	}
}
if (outputFile == null || inputFiles.length == 0) {
	usage();
	process.exit(1);
}

const sema = new FuSema();
parser.setHost(host);
sema.setHost(host);
const system = FuSystem.new();
let parent = system;
try {
	if (referencedFiles.length > 0)
		parent = parseAndResolve(parser, system, parent, referencedFiles, sema, host);
	const program = parseAndResolve(parser, system, parent, inputFiles, sema, host);
	if (lang != null) {
		host.emit(program, lang, namespace, outputFile);
		if (host.hasErrors())
			process.exitCode = 1;
	}
	else
		emitImplicitLang(program, namespace, outputFile, host);
}
catch (e) {
	console.error(`fut: ERROR: ${e.message}`);
	process.exit(1);
}
