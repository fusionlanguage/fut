prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
ifdef COMSPEC
CSC = "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/Roslyn/csc.exe" -nologo -out:$@
MONO :=
JAVACPSEP = ;
else
CSC := gmcs
MONO := mono
JAVACPSEP = :
endif
CC = clang -Wall -Wno-tautological-compare -Wno-unused-function -Werror
CXX = clang++ -Wall -Wno-tautological-compare -Werror -std=c++2a

VERSION := 1.0.0
MAKEFLAGS = -r

all: cito.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiException.cs CiTree.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiParser.cs CiResolver.cs GenBase.cs GenTyped.cs GenCCpp.cs GenC.cs GenCpp.cs GenCs.cs GenJava.cs GenJs.cs CiTo.cs)
	$(CSC) $^

test: test-c test-cpp test-cs test-java test-js test-error
	perl test/summary.pl $(wildcard test/bin/*/*.txt)

test-c: $(patsubst test/%.ci, test/bin/%/c.txt, $(wildcard test/*.ci))
	perl test/summary.pl $^

test-cpp: $(patsubst test/%.ci, test/bin/%/cpp.txt, $(wildcard test/*.ci))
	perl test/summary.pl $^

test-cs: $(patsubst test/%.ci, test/bin/%/cs.txt, $(wildcard test/*.ci))
	perl test/summary.pl $^

test-java: $(patsubst test/%.ci, test/bin/%/java.txt, $(wildcard test/*.ci))
	perl test/summary.pl $^

test-js: $(patsubst test/%.ci, test/bin/%/js.txt, $(wildcard test/*.ci))
	perl test/summary.pl $^

test-error: $(patsubst test/error/%.ci, test/bin/%/error.txt, $(wildcard test/error/*.ci))
	perl test/summary.pl $^

test/bin/%/c.txt: test/bin/%/c.exe
	-./$< >$@

test/bin/%/cpp.txt: test/bin/%/cpp.exe
	-./$< >$@

test/bin/%/cs.txt: test/bin/%/cs.exe
	-$(MONO) $< >$@

test/bin/%/java.txt: test/bin/%/Test.class test/bin/Runner.class
	-java -cp "test/bin$(JAVACPSEP)$(<D)" Runner >$@

test/bin/%/js.txt: test/bin/%/Run.js
	-node $< >$@

test/bin/%/c.exe: test/bin/%/Test.c test/Runner.c
	-$(CC) -o $@ -I $(<D) $^

test/bin/%/cpp.exe: test/bin/%/Test.cpp test/Runner.cpp
	-$(CXX) -o $@ -I $(<D) $^

test/bin/%/cs.exe: test/bin/%/Test.cs test/Runner.cs
	-$(CSC) $^

test/bin/%/Test.class: test/bin/%/Test.java
	-javac -d $(@D) $(<D)/*.java

test/bin/%/Run.js: test/bin/%/Test.js
	-cat $< test/Runner.js >$@

test/bin/%/Test.c: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

test/bin/%/Test.cpp: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

test/bin/%/Test.cs: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

test/bin/%/Test.java: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

test/bin/%/Test.js: test/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $@ $<

.PRECIOUS: test/bin/%/Test.c test/bin/%/Test.cpp test/bin/%/Test.cs test/bin/%/Test.java test/bin/%/Test.js

test/bin/Runner.class: test/Runner.java test/bin/Basic/Test.class
	javac -d $(@D) -cp test/bin/Basic $<

test/bin/%/error.txt: test/error/%.ci cito.exe
	-mkdir -p $(@D) && $(MONO) ./cito.exe -o $(@:%.txt=%.cs) $< 2>$@
	-perl -ne 'print "$$ARGV($$.): $$1" if m!//(ERROR: .+)!s' $< | diff -uZ - $@ && echo PASSED >$@

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

.PHONY: all test test-c test-cpp test-cs test-java test-js test-error install install-cito uninstall clean

.DELETE_ON_ERROR:
