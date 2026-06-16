// Fut.java - Fusion transpiler
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

package org.fusionlanguage;

import java.io.BufferedInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Locale;

class FileGenHost extends FuConsoleHost
{
	private final ArrayList<String> resourceDirs = new ArrayList<String>();
	private File filename;
	private FileWriter currentFile;

	public void addResourceDir(String path)
	{
		this.resourceDirs.add(path);
	}

	private ArrayList<Byte> readAllBytes(File path)
	{
		try (BufferedInputStream in = new BufferedInputStream(new FileInputStream(path))) {
			for (final ArrayList<Byte> result = new ArrayList<Byte>();;) {
				int b = in.read();
				if (b < 0)
					return result;
				result.add((byte) b);
			}
		}
		catch (IOException e) {
			throw new RuntimeException(e);
		}
	}

	private ArrayList<Byte> readResource(String name, FuPrefixExpr expr)
	{
		for (String dir : this.resourceDirs) {
			File path = new File(dir, name);
			if (path.isFile())
				return readAllBytes(path);
		}
		File path = new File(name);
		if (path.isFile())
			return readAllBytes(path);
		reportStatementError(expr, String.format("File %s not found", name));
		return new ArrayList<Byte>();
	}

	protected @Override int getResourceLength(String name, FuPrefixExpr expr)
	{
		ArrayList<Byte> content = getResources().get(name);
		if (content == null) {
			content = readResource(name, expr);
			getResources().put(name, content);
		}
		return content.size();
	}

	public @Override Appendable createFile(String directory, String filename)
	{
		this.filename = new File(directory, filename);
		try {
			this.currentFile = new FileWriter(this.filename);
		}
		catch (IOException e) {
			throw new RuntimeException(e); // TODO
		}
		return this.currentFile;
	}

	public @Override void closeFile()
	{
		try {
			this.currentFile.close();
		}
		catch (IOException e) {
			throw new RuntimeException(e); // TODO
		}
		if (hasErrors())
			filename.delete();
	}

	protected @Override String toDirectory(String path)
	{
		File file = new File(path);
		return file.isDirectory() ? path : file.getParent();
	}
}

public class Fut
{
	private static FuProgram parseAndResolve(FuParser parser, FuSystem system, FuScope parent, ArrayList<String> files, FuSema sema, FuConsoleHost host) throws IOException
	{
		new FuProgram().init(parent, system, host);
		for (String file : files) {
			byte[] input = Files.readAllBytes(Paths.get(file));
			parser.parse(file, input, input.length);
		}
		if (host.hasErrors())
			System.exit(1);
		sema.process();
		if (host.hasErrors())
			System.exit(1);
		return host.program;
	}

	public static void main(String[] args)
	{
		Locale.setDefault(Locale.US);
		final FileGenHost host = new FileGenHost();
		final FuParser parser = new FuParser();
		final ArrayList<String> inputFiles = new ArrayList<String>();
		final ArrayList<String> referencedFiles = new ArrayList<String>();
		String lang = null;
		String outputFile = null;
		String namespace = "";
		for (int i = 0; i < args.length; i++) {
			String arg = args[i];
			if (!arg.startsWith("-"))
				inputFiles.add(arg);
			else if (arg.equals("--help")) {
				FuConsoleHost.usage("java -jar fut.jar");
				return;
			}
			else if (arg.equals("--version")) {
				System.out.format("Fusion Transpiler %s (Java)\n", FuConsoleHost.VERSION);
				return;
			}
			else if (arg.length() == 2 && i + 1 < args.length) {
				switch (arg.charAt(1)) {
				case 'l':
					lang = args[++i];
					break;
				case 'o':
					outputFile = args[++i];
					break;
				case 'n':
					namespace = args[++i];
					break;
				case 'D':
					String symbol = args[++i];
					if (symbol.equals("true") || symbol.equals("false")) {
						System.err.format("fut: ERROR: '%s' is reserved\n", symbol);
						System.exit(1);
					}
					parser.addPreSymbol(symbol);
					break;
				case 'r':
					referencedFiles.add(args[++i]);
					break;
				case 'I':
					host.addResourceDir(args[++i]);
					break;
				default:
					System.err.format("fut: ERROR: Unknown option: %s\n", arg);
					System.exit(1);
				}
			}
			else {
				System.err.format("fut: ERROR: Unknown option: %s\n", arg);
				System.exit(1);
			}
		}
		if (outputFile == null || inputFiles.size() == 0) {
			FuConsoleHost.usage("java -jar fut.jar");
			System.exit(1);
		}

		final FuSema sema = new FuSema();
		parser.setHost(host);
		sema.setHost(host);
		final FuSystem system = FuSystem.new_();
		FuScope parent = system;
		try {
			if (!referencedFiles.isEmpty())
				parent = parseAndResolve(parser, system, parent, referencedFiles, sema, host);
			final FuProgram program = parseAndResolve(parser, system, parent, inputFiles, sema, host);

			if (lang != null) {
				host.emit(program, lang, namespace, outputFile);
				if (host.hasErrors())
					System.exit(1);
				return;
			}
			for (int i = outputFile.length(); --i >= 0; ) {
				char c = outputFile.charAt(i);
				if (c == '.') {
					if (i >= 2
					 && (outputFile.charAt(i - 2) == '.' || outputFile.charAt(i - 2) == ',')
					 && outputFile.regionMatches(true, i - 1, "d.ts", 0, 4)
					 && (i + 3 == outputFile.length() || outputFile.charAt(i + 3) == ','))
						continue;
					String outputBase = outputFile.substring(0, i + 1);
					boolean error = false;
					for (String outputExt : outputFile.substring(i + 1).split(",")) {
						host.emit(program, outputExt, namespace, outputBase + outputExt);
						if (host.hasErrors()) {
							host.setErrors(false);
							error = true;
						}
					}
					if (error)
						System.exit(1);
					return;
				}
				if (c == '/' || c == '\\' || c == ':')
					break;
			}
			System.err.format("fut: ERROR: Don't know what language to translate to: no extension in '%s' and no '-l' option\n", outputFile);
			System.exit(1);
		}
		catch (IOException e) {
			System.err.println("fut: ERROR: " + e.getMessage());
			System.exit(1);
		}
	}
}
