import * as vscode from "vscode";
import { FuParser, FuProgram, FuSystem, FuParserHost } from "./parser.js";

class VsCodeHost extends FuParserHost
{
	#system = FuSystem.new();
	#diagnostics: vscode.Diagnostic[] = [];

	reportError(filename: string, startLine: number, startColumn: number, endLine: number, endColumn: number, message: string) : void
	{
		this.#diagnostics.push(new vscode.Diagnostic(new vscode.Range(startLine - 1, startColumn - 1, endLine - 1, endColumn - 1), message));
	}

	updateDiagnostics(document: vscode.TextDocument, diagnosticCollection: vscode.DiagnosticCollection): void
	{
		if (document.languageId != "fusion")
			return;
		this.#diagnostics.length = 0;
		const parser = new FuParser();
		parser.setHost(this);
		parser.program = new FuProgram();
		parser.program.parent = this.#system;
		parser.program.system = this.#system;
		const input = new TextEncoder().encode(document.getText());
		parser.parse(document.fileName, input, input.length);
		diagnosticCollection.set(document.uri, this.#diagnostics);
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
}
