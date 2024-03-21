VERSION = 3.1.2

run: fusion-$(VERSION).vsix
	'$(LOCALAPPDATA)\Programs\Microsoft VS Code\bin\code' --extensionDevelopmentPath=$(abspath .)

fusion-$(VERSION).vsix: package.json fu-logo.png fu-file.svg language-configuration.json tsconfig.json syntaxes/fusion.tmLanguage.json src/extension.ts src/fucheck.js node_modules
	vsce package

src/fucheck.js: ../../Lexer.fu ../../AST.fu ../../Parser.fu ../../Sema.fu
	fut -o $@ $^

node_modules: package.json
	npm i --no-package-lock
