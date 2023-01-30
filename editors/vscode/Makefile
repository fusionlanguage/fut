VERSION = 2.1.1

run: ci-$(VERSION).vsix
	'$(LOCALAPPDATA)\Programs\Microsoft VS Code\bin\code' --extensionDevelopmentPath=$(abspath .)

ci-$(VERSION).vsix: package.json ci-logo.png ci-file.svg language-configuration.json tsconfig.json syntaxes/ci.tmLanguage.json src/extension.ts src/parser.js
	$(APPDATA)/npm/vsce package

src/parser.js: ../../Lexer.ci ../../AST.ci ../../Parser.ci
	cito -o $@ $^
