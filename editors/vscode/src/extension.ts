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
import { FuParser, FuProgram, FuSystem, FuSema, FuSemaHost, FuSymbolReferenceVisitor, FuStatement, FuSymbol, FuContainerType, FuEnum, FuClass,
	FuMember, FuConst, FuVar, FuParameters, FuField, FuCallType, FuMethod, FuDocText, FuDocCode, FuDocPara, FuDocList, FuCodeDoc } from "./fucheck.js";

class VsCodeHost extends FuSemaHost
{
	#system = FuSystem.new();
	#hasErrors = false;

	reportError(filename: string, line: number, startUtf16Column: number, endUtf16Column: number, message: string): void
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

	reportError(filename: string, line: number, startUtf16Column: number, endUtf16Column: number, message: string): void
	{
		super.reportError(filename, line, startUtf16Column, endUtf16Column, message);
		const fileDiagnostics = this.#diagnostics.get(filename);
		if (fileDiagnostics !== undefined)
			fileDiagnostics.push(new vscode.Diagnostic(new vscode.Range(line, startUtf16Column, line, endUtf16Column), message));
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

	check(document: vscode.TextDocument): void
	{
		if (document.languageId != "fusion")
			return;
		this.#queue.set(document.uri.toString(), document);
		if (this.#timeoutId !== null)
			clearTimeout(this.#timeoutId);
		this.#timeoutId = setTimeout(() => this.#process(), 1000);
	}

	delete(document: vscode.TextDocument): void
	{
		this.#queue.delete(document.uri.toString());
		this.#diagnosticCollection.delete(document.uri);
	}
}

class VsCodeSymbolLocator extends VsCodeHost
{
	protected async findSymbol(document: vscode.TextDocument, position: vscode.Position): Promise<FuSymbol | null>
	{
		const parser = this.createParser();
		const filename = document.uri.toString();
		parser.findName(filename, position.line, position.character);
		const files = await vscode.workspace.findFiles("*.fu");
		if (files.some(uri => uri.toString() == filename))
			await this.parseFolder(files, parser);
		else
			this.parseDocument(document, parser);
		this.doSema();
		return parser.getFoundDefinition();
	}

	static #getSignature(method: FuMethod): string
	{
		let code = method.callType == FuCallType.NORMAL ? "" : FuMethod.callTypeToString(method.callType) + " ";
		code = `${code}${method.type} ${method.name}`;
		if (!method.isStatic() && method.isMutator())
			code += "!";
		code += "(";
		for (let param = method.firstParameter(); param != null;) {
			code = `${code}${param.type} ${param.name}`;
			param = param.nextVar();
			if (param == null)
				break;
			code += ", ";
		}
		return code + ")";
	}

	static #convertDocPara(para: FuDocPara): string
	{
		let markdown = "";
		for (const inline of para.children)
			markdown += inline instanceof FuDocText ? inline.text
				: inline instanceof FuDocCode ? `\`${inline.text}\``
				: "\n";
		return markdown;
	}

	static #convertDocList(list: FuDocList): string
	{
		let markdown = "";
		for (const item of list.items)
			markdown = `${markdown}* ${VsCodeSymbolLocator.#convertDocPara(item)}\n`;
		return markdown;
	}

	async getHover(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.Hover | null>
	{
		const symbol = await this.findSymbol(document, position);
		if (symbol == null)
			return null;
		let code = symbol.name;
		if (symbol instanceof FuClass)
			code = `class ${code}`;
		else if (symbol instanceof FuEnum)
			code = `enum ${code}`;
		else if (symbol instanceof FuConst)
			code = `const ${symbol.type} ${code}`;
		else if (symbol instanceof FuVar)
			code = `(${symbol.parent instanceof FuParameters ? "parameter" : "local variable"}) ${symbol.type} ${code}`;
		else if (symbol instanceof FuMethod)
			code = VsCodeSymbolLocator.#getSignature(symbol);
		else if (symbol instanceof FuField)
			code = `(field) ${symbol.type} ${code}`;
		else if (symbol instanceof FuMember) // property
			code = `${symbol.type} ${code}`;
		const hover = new vscode.MarkdownString().appendCodeblock(code, "fusion");
		if (symbol.documentation != null) {
			const doc: FuCodeDoc = symbol.documentation;
			let markdown = VsCodeSymbolLocator.#convertDocPara(doc.summary);
			for (const block of doc.details)
				markdown = `${markdown}\n\n${block instanceof FuDocList ? VsCodeSymbolLocator.#convertDocList(block) : VsCodeSymbolLocator.#convertDocPara(block)}`;
			hover.appendMarkdown(markdown);
		}
		return new vscode.Hover(hover);
	}
}

class VsCodeReferenceCollector extends FuSymbolReferenceVisitor
{
	provider: VsCodeGotoProvider;

	constructor(provider: VsCodeGotoProvider)
	{
		super();
		this.provider = provider;
	}

	visitFound(reference: FuStatement): void
	{
		this.provider.pushLocation(reference);
	}
}

class VsCodeGotoProvider extends VsCodeSymbolLocator
{
	#locations: vscode.Location[] = [];

	pushLocation(statement: FuStatement): void
	{
		if (statement.loc > 0) {
			const line = this.program.getLine(statement.loc);
			const file = this.program.getSourceFile(line);
			this.#locations.push(new vscode.Location(vscode.Uri.parse(file.filename), new vscode.Position(line - file.line, statement.loc - this.program.lineLocs[line])));
		}
	}

	async findDefinition(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.Location[]>
	{
		const symbol = await this.findSymbol(document, position);
		if (symbol != null)
			this.pushLocation(symbol);
		return this.#locations;
	}

	async findImplementations(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.Location[]>
	{
		const symbol = await this.findSymbol(document, position);
		if (symbol != null) {
			if (symbol instanceof FuClass) {
				for (const subclass of this.program.classes) {
					if (symbol.isSameOrBaseOf(subclass))
						this.pushLocation(subclass);
				}
			}
			else if (symbol instanceof FuMethod && symbol.isAbstractVirtualOrOverride()) {
				for (const subclass of this.program.classes) {
					if (symbol.parent.isSameOrBaseOf(subclass) && subclass.contains(symbol))
						this.pushLocation(subclass.tryLookup(symbol.name, false));
				}
			}
			else
				this.pushLocation(symbol);
		}
		return this.#locations;
	}

	async findReferences(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.Location[]>
	{
		const symbol = await this.findSymbol(document, position);
		if (symbol != null)
			new VsCodeReferenceCollector(this).findReferences(this.program, symbol);
		return this.#locations;
	}
}

class VsCodeSymbolProvider extends VsCodeHost
{
	#createSymbol(source: FuContainerType | FuMember, kind: vscode.SymbolKind): vscode.DocumentSymbol
	{
		const line = this.program.getLine(source.loc);
		const column = source.loc - this.program.lineLocs[line];
		return new vscode.DocumentSymbol(source.name, "", kind,
			new vscode.Range(source.startLine, source.startColumn, source.endLine, source.endColumn),
			new vscode.Range(line, column, line, column + source.getLocLength()));
	}

	provideSymbols(document: vscode.TextDocument): vscode.DocumentSymbol[]
	{
		this.parseDocument(document, this.createParser());
		const symbols : vscode.DocumentSymbol[] = [];
		for (let container = this.program.first; container != null; container = container.next) {
			if (container instanceof FuEnum) {
				const containerSymbol = this.#createSymbol(container, vscode.SymbolKind.Enum);
				for (let member: FuMember = container.getFirstValue(); member != null; member = member.next)
					containerSymbol.children.push(this.#createSymbol(member, vscode.SymbolKind.EnumMember));
				symbols.push(containerSymbol);
			}
			else {
				const containerSymbol = this.#createSymbol(container, vscode.SymbolKind.Class);
				if (container.constructor_ != null)
					containerSymbol.children.push(this.#createSymbol(container.constructor_, vscode.SymbolKind.Constructor));
				for (let member = container.first; member != null; member = member.next) {
					containerSymbol.children.push(this.#createSymbol(member, member instanceof FuMethod ? vscode.SymbolKind.Method :
						member instanceof FuField ? vscode.SymbolKind.Field : vscode.SymbolKind.Constant));
				}
				symbols.push(containerSymbol);
			}
		}
		return symbols;
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
	vscode.languages.registerHoverProvider("fusion", {
			provideHover(document, position, token) {
				return new VsCodeSymbolLocator().getHover(document, position);
			}
		});
	vscode.languages.registerDefinitionProvider("fusion", {
			provideDefinition(document, position, token) {
				return new VsCodeGotoProvider().findDefinition(document, position);
			}
		});
	vscode.languages.registerImplementationProvider("fusion", {
			provideImplementation(document, position, token) {
				return new VsCodeGotoProvider().findImplementations(document, position);
			}
		});
	vscode.languages.registerReferenceProvider("fusion", {
			provideReferences(document, position, context, token) {
				return new VsCodeGotoProvider().findReferences(document, position);
			}
		});
	vscode.languages.registerDocumentSymbolProvider("fusion", {
			provideDocumentSymbols(document, token) {
				return new VsCodeSymbolProvider().provideSymbols(document);
			}
		});
}
