prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
ifdef COMSPEC
CSC = c:/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe -nologo -out:$@ $(subst /,\,$^)
MONO :=
JAVACPSEP = ;
else
CSC := gmcs
MONO := mono
JAVACPSEP = :
endif
CXX = clang++ -std=c++17

VERSION := 1.0.0
MAKEFLAGS = -r

all: cito.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiException.cs CiTree.cs CiLexer.cs CiParser.cs CiResolver.cs GenBase.cs GenTyped.cs GenCpp.cs GenCs.cs GenJava.cs GenJs.cs CiTo.cs)
	$(CSC)

test-error: cito.exe
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

test: $(patsubst test/%.ci, test/bin/%/cpp.txt, $(wildcard test/*.ci)) \
      $(patsubst test/%.ci, test/bin/%/cs.txt, $(wildcard test/*.ci)) \
      $(patsubst test/%.ci, test/bin/%/java.txt, $(wildcard test/*.ci)) \
      $(patsubst test/%.ci, test/bin/%/js.txt, $(wildcard test/*.ci))
	perl test/summary.pl $^

test/bin/%/cpp.txt: test/bin/%/cpp.exe
	-./$< >$@

test/bin/%/cs.txt: test/bin/%/cs.exe
	-$(MONO) $< >$@

test/bin/%/java.txt: test/bin/%/Test.class test/bin/Runner.class
	-java -cp "test/bin$(JAVACPSEP)$(<D)" Runner >$@

test/bin/%/js.txt: test/bin/%/Run.js
	-node $< >$@

test/bin/%/cpp.exe: test/bin/%/Test.cpp test/Runner.cpp
	-$(CXX) -o $@ -I $(<D) $^

test/bin/%/cs.exe: test/bin/%/Test.cs test/Runner.cs
	-$(CSC)

test/bin/%/Test.class: test/bin/%/Test.java
	-javac -d $(@D) $(<D)/*.java

test/bin/%/Run.js: test/bin/%/Test.js
	-cat $< test/Runner.js >$@

test/bin/%/Test.cpp: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

test/bin/%/Test.cs: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

test/bin/%/Test.java: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

test/bin/%/Test.js: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

.PRECIOUS: test/bin/%/Test.cpp test/bin/%/Test.cs test/bin/%/Test.java test/bin/%/Test.js

test/bin/Runner.class: test/Runner.java test/bin/Basic/Test.class
	javac -d $(@D) -cp test/bin/Basic $<

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
	$(RM) -r test/bin

.PHONY: all test install install-cito uninstall clean

.DELETE_ON_ERROR:
