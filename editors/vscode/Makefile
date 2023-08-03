VERSION = 3.0.0

run: ci-$(VERSION).vsix
	'$(LOCALAPPDATA)\Programs\Microsoft VS Code\bin\code' --extensionDevelopmentPath=$(abspath .)

ci-$(VERSION).vsix: package.json ci-logo.png ci-file.svg language-configuration.json tsconfig.json syntaxes/ci.tmLanguage.json src/extension.ts src/parser.js
	$(APPDATA)/npm/vsce package

src/parser.js: ../../Lexer.fu ../../AST.fu ../../Parser.fu
	fut -o $@ $^
