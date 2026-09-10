CXXFLAGS = -Wall -O2

ifeq ($(OS),Windows_NT)
fusion-lsp.exe: main.cpp lsp.cpp
	$(CXX) -o $@ -std=c++20 $(CXXFLAGS) -s -static $^
else
fusion-lsp: main.cpp lsp.cpp
	$(CXX) -o $@ -std=c++20 $(CXXFLAGS) $^
endif

lsp.cpp: lsp.fu ../../Lexer.fu ../../AST.fu ../../Parser.fu ../../Sema.fu ../../SymbolReferenceVisitor.fu
ifeq ($(FUT_HOST),node)
	node ../../fut.js -o $@ $^
else
	fut -o $@ $^
endif
