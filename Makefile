prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CSC := $(if $(WINDIR),csc,gmcs)

MAKEFLAGS = -r

all: cito.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenAs.cs GenD.cs CiTo.cs)
	$(CSC) -nologo -out:$@ -o+ $^

check: $(srcdir)hello.ci cito.exe
	./cito.exe -o hello.c $<
	./cito.exe -l c99 -o hello99.c $<
	./cito.exe -o HelloCi.java $<
	./cito.exe -o hello.cs $<
	./cito.exe -o hello.js $<
	./cito.exe -o HelloCi.as $<
	./cito.exe -o hello.d $<

install: cito.exe
	cp $< $(DESTDIR)$(prefix)/bin/cito.exe

uninstall:
	$(RM) $(DESTDIR)$(prefix)/bin/cito.exe

www: index.html

index.html: $(srcdir)README
	asciidoc -o - -a www $< | xmllint --valid --nonet -o $@ -

clean:
	$(RM) cito.exe cito.pdb hello.c hello.h hello99.c hello99.h HelloCi.java hello.cs hello.js HelloCi.as hello.d index.html

.PHONY: all check install uninstall www clean

.DELETE_ON_ERROR:
