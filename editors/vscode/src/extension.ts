import * as vscode from "vscode";

export function activate(context: vscode.ExtensionContext) {
//	vscode.window.showInformationMessage("Hello, world!");
	const diagnosticCollection = vscode.languages.createDiagnosticCollection("ci");
	if (vscode.window.activeTextEditor) {
		updateDiagnostics(vscode.window.activeTextEditor.document, diagnosticCollection);
	}
	context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => {
		if (editor) {
			updateDiagnostics(editor.document, diagnosticCollection);
		}
	}));
}

function updateDiagnostics(document: vscode.TextDocument, diagnosticCollection: vscode.DiagnosticCollection): void {
	if (document.languageId != "ci")
		return;
	const diagnostics = [];
	diagnostics.push(new vscode.Diagnostic(new vscode.Range(0, 0, 1, 0), "hello"));
	diagnosticCollection.set(document.uri, diagnostics);
}
