prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CSC := $(if $(WINDIR),c:/Windows/Microsoft.NET/Framework/v3.5/csc.exe,gmcs)
MONO := $(if $(WINDIR),,mono)
SLASH := $(if $(WINDIR),\\,/)

VERSION := 1.0.0
MAKEFLAGS = -r

all: cito.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiException.cs CiTree.cs CiLexer.cs CiParser.cs CiResolver.cs GenBase.cs GenCs.cs CiTo.cs)
	$(CSC) -nologo -debug -out:$@ -o+ $^

test: cito.exe
	@export passed=0 total=0; \
		cd test; \
		for ci in *.ci; do \
			if $(MONO) ../cito.exe -o $$ci.cs $$ci && $(CSC) -nologo -out:$$ci.exe $$ci.cs Runner.cs && $(MONO) ./$$ci.exe; then \
				passed=$$(($$passed+1)); \
			else \
				echo FAILED $$ci; \
			fi; \
			total=$$(($$total+1)); \
		done; \
		echo PASSED $$passed of $$total tests
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
