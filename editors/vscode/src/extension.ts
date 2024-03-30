// extension.ts - Visual Studio Code extension
//
// Copyright (C) 2023-2024  Piotr Fusik
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

import * as vscode from "vscode";
import { FuParser, FuProgram, FuSystem, FuSema, FuSemaHost } from "./fucheck.js";

class VsCodeHost extends FuSemaHost
{
	#system = FuSystem.new();
	#hasErrors = false;

	reportError(filename: string, startLine: number, startUtf16Column: number, endLine: number, endUtf16Column: number, message: string): void
	{
		this.#hasErrors = true;
	}

	createParser(): FuParser
	{
		this.#hasErrors = false;
		this.program = new FuProgram();
		this.program.parent = this.#system;
		this.program.system = this.#system;
		const parser = new FuParser();
		parser.setHost(this);
		return parser;
	}

	parse(filename: string, input: Uint8Array, parser: FuParser): void
	{
		parser.parse(filename, input, input.length);
	}

	parseDocument(document: vscode.TextDocument, parser: FuParser): void
	{
		this.parse(document.uri.toString(), new TextEncoder().encode(document.getText()), parser);
	}

	async parseFolder(files: vscode.Uri[], parser: FuParser): Promise<void>
	{
		const documents = vscode.workspace.textDocuments;
		for (const uri of files) {
			const filename = uri.toString();
			const doc = documents.find(doc => doc.uri.toString() == filename);
			if (doc === undefined)
				this.parse(filename, await vscode.workspace.fs.readFile(uri), parser);
			else
				this.parseDocument(doc, parser);
		}
	}

	doSema(): void
	{
		if (this.#hasErrors)
			return;
		const sema = new FuSema();
		sema.setHost(this);
		sema.process();
	}
}

class VsCodeDiagnostics extends VsCodeHost
{
	#queue: Map<string, vscode.TextDocument> = new Map();
	#timeoutId: number | null = null;
	#diagnostics: Map<string, vscode.Diagnostic[]> = new Map();
	#diagnosticCollection = vscode.languages.createDiagnosticCollection("fusion");

	reportError(filename: string, startLine: number, startUtf16Column: number, endLine: number, endUtf16Column: number, message: string): void
	{
		super.reportError(filename, startLine, startUtf16Column, endLine, endUtf16Column, message);
		const fileDiagnostics = this.#diagnostics.get(filename);
		if (fileDiagnostics !== undefined)
			fileDiagnostics .push(new vscode.Diagnostic(new vscode.Range(startLine, startUtf16Column, endLine, endUtf16Column), message));
	}

	parse(filename: string, input: Uint8Array, parser: FuParser): void
	{
		this.#diagnostics.set(filename, []);
		parser.parse(filename, input, input.length);
	}

	async #process(): Promise<void>
	{
		this.#timeoutId = null;
		const files = await vscode.workspace.findFiles("*.fu");
		let hasFolder = false;
		for (const uri of files) {
			if (this.#queue.delete(uri.toString()))
				hasFolder = true;
		}
		for (const doc of this.#queue.values()) {
			this.parseDocument(doc, this.createParser());
			this.doSema();
		}
		this.#queue.clear();
		if (hasFolder) {
			await this.parseFolder(files, this.createParser());
			this.doSema();
		}
		for (const [filename, diagnostics] of this.#diagnostics)
			this.#diagnosticCollection.set(vscode.Uri.parse(filename), diagnostics);
		this.#diagnostics.clear();
	}

	check(document: vscode.TextDocument)
	{
		if (document.languageId != "fusion")
			return;
		this.#queue.set(document.uri.toString(), document);
		if (this.#timeoutId !== null)
			clearTimeout(this.#timeoutId);
		this.#timeoutId = setTimeout(() => this.#process(), 1000);
	}

	delete(document: vscode.TextDocument)
	{
		this.#queue.delete(document.uri.toString());
		this.#diagnosticCollection.delete(document.uri);
	}
}

class VsCodeDefinitionProvider extends VsCodeHost
{
	async findDefinition(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.Location | null>
	{
		const parser = this.createParser();
		const filename = document.uri.toString();
		parser.findDefinition(filename, position.line, position.character);
		const files = await vscode.workspace.findFiles("*.fu");
		if (files.some(uri => uri.toString() == filename))
			await this.parseFolder(files, parser);
		else
			this.parseDocument(document, parser);
		this.doSema();
		const definitionFilename: string | null = parser.getFoundDefinitionFilename();
		return definitionFilename == null ? null : new vscode.Location(vscode.Uri.parse(definitionFilename), new vscode.Position(parser.getFoundDefinitionLine(), parser.getFoundDefinitionColumn()));
	}
}

export function activate(context: vscode.ExtensionContext): void
{
	const diagnostics = new VsCodeDiagnostics();
	if (vscode.window.activeTextEditor)
		diagnostics.check(vscode.window.activeTextEditor.document);
	context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => {
			if (editor)
				diagnostics.check(editor.document);
		}));
	context.subscriptions.push(vscode.workspace.onDidChangeTextDocument(e => diagnostics.check(e.document)));
	context.subscriptions.push(vscode.workspace.onDidCloseTextDocument(document => diagnostics.delete(document)));
	vscode.languages.registerDefinitionProvider("fusion", {
			provideDefinition(document, position, token) {
				return new VsCodeDefinitionProvider().findDefinition(document, position);
			}
		});
}
