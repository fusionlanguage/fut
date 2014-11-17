prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CSC := $(if $(WINDIR),c:/Windows/Microsoft.NET/Framework/v3.5/csc.exe,gmcs)

VERSION := 1.0.0
MAKEFLAGS = -r

all: cito.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiException.cs CiTree.cs CiLexer.cs CiParser.cs CiResolver.cs GenBase.cs GenCs.cs CiTo.cs)
	$(CSC) -nologo -out:$@ -o+ $^

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
	$(RM) cito.exe

.PHONY: all install install-cito uninstall clean

.DELETE_ON_ERROR:
