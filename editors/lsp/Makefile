CXXFLAGS = -Wall -O2
ifeq ($(OS),Windows_NT)
CXXFLAGS += -s -static
EXEEXT = .exe
endif

fusion-lsp$(EXEEXT): main.cpp lsp.cpp
	$(CXX) -o $@ -std=c++20 $(CXXFLAGS) $^

lsp.cpp: lsp.fu ../../Lexer.fu ../../AST.fu ../../Parser.fu ../../Sema.fu ../../SymbolReferenceVisitor.fu
ifeq ($(FUT_HOST),node)
	node ../../fut.js -o $@ $^
else
	fut -o $@ $^
endif
