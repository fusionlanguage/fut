prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CSC := $(if $(WINDIR),csc,gmcs)
ASCIIDOC = asciidoc -o - $(1) $< | xmllint --valid --nonet -o $@ -
SEVENZIP = 7z a -mx=9 -bd

VERSION := 0.1.0
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

$(srcdir)README.html: $(srcdir)README
	$(call ASCIIDOC,)

www: index.html

index.html: $(srcdir)README
	$(call ASCIIDOC,-a www)

clean:
	$(RM) cito.exe cito.pdb hello.c hello.h hello99.c hello99.h HelloCi.java hello.cs hello.js HelloCi.as hello.d index.html

dist: ../cito-$(VERSION)-bin.zip srcdist

../cito-$(VERSION)-bin.zip: cito.exe $(srcdir)README.html
	$(RM) $@ && $(SEVENZIP) -tzip $@ $(^:%=./%)
# "./" makes 7z don't store paths in the archive

srcdist: $(srcdir)MANIFEST $(srcdir)README.html
	$(RM) ../cito-$(VERSION).tar.gz && tar -c --numeric-owner --owner=0 --group=0 --mode=644 -T MANIFEST --transform=s,,cito-$(VERSION)/, | $(SEVENZIP) -tgzip -si ../cito-$(VERSION).tar.gz

$(srcdir)MANIFEST:
	if test -e $(srcdir).git; then \
		(git ls-tree -r --name-only --full-tree master | grep -vF .gitignore \
			&& echo MANIFEST && echo README.html ) | sort -u >$@; \
	fi

.PHONY: all check install uninstall www clean srcdist $(srcdir)MANIFEST

.DELETE_ON_ERROR:
