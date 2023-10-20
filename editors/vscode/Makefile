VERSION = 3.0.2

run: fusion-$(VERSION).vsix
	'$(LOCALAPPDATA)\Programs\Microsoft VS Code\bin\code' --extensionDevelopmentPath=$(abspath .)

fusion-$(VERSION).vsix: package.json fu-logo.png fu-file.svg language-configuration.json tsconfig.json syntaxes/fusion.tmLanguage.json src/extension.ts src/parser.js node_modules
	$(APPDATA)/npm/vsce package

src/parser.js: ../../Lexer.fu ../../AST.fu ../../Parser.fu
	fut -o $@ $^

node_modules: package.json
	npm i --no-package-lock
