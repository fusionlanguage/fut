prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CSC := $(if $(WINDIR),c:/Windows/Microsoft.NET/Framework/v3.5/csc.exe,gmcs)
ASCIIDOC = asciidoc -o - $(1) $< | xmllint --valid --nonet -o $@ -
SEVENZIP = 7z a -mx=9 -bd

VERSION := 0.2.0
MAKEFLAGS = -r

all: cito.exe cipad.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenAs.cs GenD.cs CiTo.cs)
	$(CSC) -nologo -out:$@ -o+ $^

cipad.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenAs.cs GenD.cs CiPad.cs ci-logo.ico)
	$(CSC) -nologo -out:$@ -o+ -t:winexe -win32icon:$(filter %.ico,$^) $(filter %.cs,$^) -r:System.Drawing.dll -r:System.Windows.Forms.dll

ci-logo.png: $(srcdir)ci-logo.svg
	convert -background none $< -resize "52x64!" -extent 64x64 $@

$(srcdir)ci-logo.ico: $(srcdir)ci-logo.svg
	convert -background none $< -resize "26x32!" -extent 32x32 $@

check: $(srcdir)hello.ci cito.exe
	./cito.exe -o hello.c $<
	./cito.exe -l c99 -o hello99.c $<
	./cito.exe -o HelloCi.java $<
	./cito.exe -o hello.cs $<
	./cito.exe -o hello.js $<
	./cito.exe -o HelloCi.as $<
	./cito.exe -o hello.d $<

install: cito.exe cipad.exe
	cp cito.exe $(DESTDIR)$(prefix)/bin/cito.exe
	cp cipad.exe $(DESTDIR)$(prefix)/bin/cipad.exe

uninstall:
	$(RM) $(DESTDIR)$(prefix)/bin/cito.exe $(DESTDIR)$(prefix)/bin/cipad.exe

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
	$(RM) cito.exe cipad.exe hello.c hello.h hello99.c hello99.h HelloCi.java hello.cs hello.js HelloCi.as hello.d index.html

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

.PHONY: all check install uninstall www clean srcdist $(srcdir)MANIFEST version

.DELETE_ON_ERROR:
