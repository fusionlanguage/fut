prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CSC := $(if $(WINDIR),c:/Windows/Microsoft.NET/Framework/v3.5/csc.exe,gmcs)
TESTCSC := $(if $(WINDIR),c:/Windows/Microsoft.NET/Framework/v2.0.50727/csc.exe,gmcs)
MONO := $(if $(WINDIR),,mono)
SLASH := $(if $(WINDIR),\\,/)

VERSION := 1.0.0
MAKEFLAGS = -r

all: cito.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiException.cs CiTree.cs CiLexer.cs CiParser.cs CiResolver.cs GenBase.cs GenCs.cs GenJava.cs GenTyped.cs CiTo.cs)
	$(CSC) -nologo -debug -out:$@ -o+ $^

test: cito.exe
	@cd test; \
		ls *.ci | xargs -L1 -I{} -P7 bash -c \
			"if $(MONO) ../cito.exe -o {}.cs {} && $(TESTCSC) -nologo -out:{}.exe {}.cs Runner.cs && $(MONO) ./{}.exe; then \
				echo PASSED {}; \
			else \
				echo FAILED {}; \
			fi" | \
			perl -e '/^PASSED/ ? $$p++ : print while<>;print "PASSED $$p of $$. tests\n"'
	@export passed=0 total=0; \
		cd test/error; \
		for ci in *.ci; do \
			if $(MONO) ../../cito.exe -o $$ci.cs $$ci; then \
				echo FAILED $$ci; \
			else \
				passed=$$(($$passed+1)); \
			fi; \
			total=$$(($$total+1)); \
		done; \
		echo PASSED $$passed of $$total errors

install: install-cito

install-cito: cito.exe
	mkdir -p $(DESTDIR)$(prefix)/lib/cito $(DESTDIR)$(prefix)/bin
	cp $< $(DESTDIR)$(prefix)/lib/cito/cito.exe
	(echo '#!/bin/sh' && echo 'exec /usr/bin/mono $(DESTDIR)$(prefix)/lib/cito/cito.exe "$$@"') >$(DESTDIR)$(prefix)/bin/cito
	chmod 755 $(DESTDIR)$(prefix)/bin/cito

uninstall:
	$(RM) $(DESTDIR)$(prefix)/bin/cito $(DESTDIR)$(prefix)/lib/cito/cito.exe
	rmdir $(DESTDIR)$(prefix)/lib/cito

clean:
	$(RM) cito.exe test/*.ci.cs test/*.exe

.PHONY: all test install install-cito uninstall clean

.DELETE_ON_ERROR:
