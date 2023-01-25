VERSION = 2.1.0

run: ci-$(VERSION).vsix
	'$(LOCALAPPDATA)\Programs\Microsoft VS Code\bin\code' --extensionDevelopmentPath=$(abspath .)

ci-$(VERSION).vsix: package.json ci-logo.png ci-file.svg language-configuration.json tsconfig.json syntaxes/ci.tmLanguage.json out/parser.js src/extension.ts
	$(APPDATA)/npm/vsce package

out/parser.js: ../../Lexer.ci ../../AST.ci ../../Parser.ci
	cito -o $@ $^
