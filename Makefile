prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CSC := $(if $(WINDIR),c:/Windows/Microsoft.NET/Framework/v3.5/csc.exe,gmcs)
MONO := $(if $(WINDIR),,mono)
ASCIIDOC = asciidoc -o - $(1) $< | xmllint --valid --nonet -o $@ -
SEVENZIP = 7z a -mx=9 -bd

VERSION := 0.3.0
MAKEFLAGS = -r

all: cito.exe cipad.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenJsWithTypedArrays.cs GenAs.cs GenD.cs GenPerl5.cs GenPerl58.cs CiTo.cs)
	$(CSC) -nologo -out:$@ -o+ $^

cipad.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenAs.cs GenD.cs CiPad.cs ci-logo.ico)
	$(CSC) -nologo -out:$@ -o+ -t:winexe -win32icon:$(filter %.ico,$^) $(filter %.cs,$^) -r:System.Drawing.dll -r:System.Windows.Forms.dll

ci-logo.png: $(srcdir)ci-logo.svg
	convert -background none $< -gravity Center -resize "52x64!" -extent 64x64 -quality 95 $@

$(srcdir)ci-logo.ico: $(srcdir)ci-logo.svg
	convert -background none $< -gravity Center -resize "26x32!" -extent 32x32 $@

check: $(srcdir)hello.ci cito.exe
	$(MONO) ./cito.exe -o hello.c $<
	$(MONO) ./cito.exe -l c99 -o hello99.c $<
	$(MONO) ./cito.exe -o HelloCi.java $<
	$(MONO) ./cito.exe -o hello.cs $<
	$(MONO) ./cito.exe -o hello.js $<
	$(MONO) ./cito.exe -o HelloCi.as $<
	$(MONO) ./cito.exe -o hello.d $<
	$(MONO) ./cito.exe -o hello.pm $<

install: install-cito install-cipad

install-cito: cito.exe
	mkdir -p $(DESTDIR)$(prefix)/lib/cito $(DESTDIR)$(prefix)/bin
	cp $< $(DESTDIR)$(prefix)/lib/cito/cito.exe
	(echo '#!/bin/sh' && echo 'exec /usr/bin/mono $(DESTDIR)$(prefix)/lib/cito/cito.exe "$$@"') >$(DESTDIR)$(prefix)/bin/cito
	chmod 755 $(DESTDIR)$(prefix)/bin/cito

install-cipad: cipad.exe
	mkdir -p $(DESTDIR)$(prefix)/lib/cito $(DESTDIR)$(prefix)/bin
	cp $< $(DESTDIR)$(prefix)/lib/cito/cipad.exe
	(echo '#!/bin/sh' && echo 'exec /usr/bin/mono $(DESTDIR)$(prefix)/lib/cito/cipad.exe "$$@"') >$(DESTDIR)$(prefix)/bin/cipad
	chmod 755 $(DESTDIR)$(prefix)/bin/cipad

uninstall:
	$(RM) $(DESTDIR)$(prefix)/bin/cito $(DESTDIR)$(prefix)/lib/cito/cito.exe $(DESTDIR)$(prefix)/bin/cipad $(DESTDIR)$(prefix)/lib/cito/cipad.exe
	rmdir $(DESTDIR)$(prefix)/lib/cito

$(srcdir)README.html: $(srcdir)README
	$(call ASCIIDOC,)

$(srcdir)INSTALL.html: $(srcdir)INSTALL
	$(call ASCIIDOC,)

$(srcdir)ci.html: $(srcdir)ci.txt
	$(call ASCIIDOC,-a toc)

www: index.html $(srcdir)INSTALL.html $(srcdir)ci.html

index.html: $(srcdir)README
	$(call ASCIIDOC,-a www)

clean:
	$(RM) cito.exe cipad.exe hello.c hello.h hello99.c hello99.h HelloCi.java hello.cs hello.js HelloCi.as hello.d hello.pm index.html

dist: ../cito-$(VERSION)-bin.zip srcdist

../cito-$(VERSION)-bin.zip: cito.exe cipad.exe $(srcdir)COPYING $(srcdir)README.html $(srcdir)ci.html $(srcdir)hello.ci
	$(RM) $@ && $(SEVENZIP) -tzip $@ $(^:%=./%)
# "./" makes 7z don't store paths in the archive

srcdist: $(addprefix $(srcdir),MANIFEST README.html INSTALL.html ci.html ci-logo.ico)
	$(RM) ../cito-$(VERSION).tar.gz && tar -c --numeric-owner --owner=0 --group=0 --mode=644 -T MANIFEST --transform=s,,cito-$(VERSION)/, | $(SEVENZIP) -tgzip -si ../cito-$(VERSION).tar.gz

$(srcdir)MANIFEST:
	if test -e $(srcdir).git; then \
		(git ls-tree -r --name-only --full-tree master | grep -vF .gitignore \
			&& echo MANIFEST && echo README.html && echo INSTALL.html && echo ci.html && echo ci-logo.ico) | sort -u >$@; \
	fi

version:
	@grep -H Version $(srcdir)AssemblyInfo.cs
	@grep -H '"cito ' $(srcdir)CiTo.cs

.PHONY: all check install install-cito install-cipad uninstall www clean srcdist $(srcdir)MANIFEST version

.DELETE_ON_ERROR:
