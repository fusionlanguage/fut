import * as vscode from "vscode";
import { CiParser, CiProgram, CiSystem } from "./parser.js";

class VsCodeParser extends CiParser
{
	private system = CiSystem.new();
	diagnostics: vscode.Diagnostic[] = [];

	protected reportError(message: string) : void
	{
		this.diagnostics.push(new vscode.Diagnostic(new vscode.Range(this.line - 1, this.tokenColumn - 1, this.line - 1, this.column - 1), message));
	}

	updateDiagnostics(document: vscode.TextDocument, diagnosticCollection: vscode.DiagnosticCollection): void
	{
		if (document.languageId != "ci")
			return;
		this.diagnostics.length = 0;
		this.program = new CiProgram();
		this.program.parent = this.system;
		this.program.system = this.system;
		const input = new TextEncoder().encode(document.getText());
		this.parse(document.fileName, input, input.length);
		diagnosticCollection.set(document.uri, this.diagnostics);
	}
}

export function activate(context: vscode.ExtensionContext): void {
	const parser = new VsCodeParser();
	const diagnosticCollection = vscode.languages.createDiagnosticCollection("ci");
	if (vscode.window.activeTextEditor)
		parser.updateDiagnostics(vscode.window.activeTextEditor.document, diagnosticCollection);
	context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => {
			if (editor)
				parser.updateDiagnostics(editor.document, diagnosticCollection);
		}));
	context.subscriptions.push(vscode.workspace.onDidChangeTextDocument(e => parser.updateDiagnostics(e.document, diagnosticCollection)));
}
