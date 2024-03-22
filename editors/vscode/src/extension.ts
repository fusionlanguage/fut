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
	#diagnostics: vscode.Diagnostic[] = [];

	reportError(filename: string, startLine: number, startUtf16Column: number, endLine: number, endUtf16Column: number, message: string) : void
	{
		this.#diagnostics.push(new vscode.Diagnostic(new vscode.Range(startLine, startUtf16Column, endLine, endUtf16Column), message));
	}

	#process(document: vscode.TextDocument, parser: FuParser)
	{
		this.#diagnostics.length = 0;
		parser.setHost(this);
		this.program = new FuProgram();
		this.program.parent = this.#system;
		this.program.system = this.#system;
		const input = new TextEncoder().encode(document.getText());
		parser.parse(document.fileName, input, input.length);
		if (this.#diagnostics.length == 0) {
			const sema = new FuSema();
			sema.setHost(this);
			sema.process();
		}
	}

	updateDiagnostics(document: vscode.TextDocument, diagnosticCollection: vscode.DiagnosticCollection): void
	{
		if (document.languageId != "fusion")
			return;
		this.#process(document, new FuParser());
		diagnosticCollection.set(document.uri, this.#diagnostics);
	}

	findDefinition(document: vscode.TextDocument, position: vscode.Position): vscode.Location | null
	{
		const parser = new FuParser();
		parser.findDefinition(document.fileName, position.line, position.character);
		this.#process(document, parser);
		const filename: string | null = parser.getFoundDefinitionFilename();
		return filename == null ? null : new vscode.Location(document.uri /* FIXME */, new vscode.Position(parser.getFoundDefinitionLine(), parser.getFoundDefinitionColumn()));
	}
}

export function activate(context: vscode.ExtensionContext): void {
	const host = new VsCodeHost();
	const diagnosticCollection = vscode.languages.createDiagnosticCollection("fusion");
	if (vscode.window.activeTextEditor)
		host.updateDiagnostics(vscode.window.activeTextEditor.document, diagnosticCollection);
	context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => {
			if (editor)
				host.updateDiagnostics(editor.document, diagnosticCollection);
		}));
	context.subscriptions.push(vscode.workspace.onDidChangeTextDocument(e => host.updateDiagnostics(e.document, diagnosticCollection)));
	vscode.languages.registerDefinitionProvider("fusion", {
			provideDefinition(document, position, token) {
				return host.findDefinition(document, position);
			}
		});
}
