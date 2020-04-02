prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CFLAGS = -Wall -Wno-tautological-compare -Werror
ifeq ($(OS),Windows_NT)
CSC = "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/Roslyn/csc.exe" -nologo
DO_BUILD = $(CSC) -out:$@ $^
CITO = ./cito.exe
MONO =
JAVACPSEP = ;
else
CSC = mcs
DO_BUILD = dotnet build
CITO = dotnet run --
MONO = mono
JAVACPSEP = :
CFLAGS += -fsanitize=address -g
endif
CC = clang
CXX = clang++ -std=c++2a
PYTHON = python3

VERSION := 1.0.0
MAKEFLAGS = -r
ifdef V
DO = 
else
DO = @echo $@ && 
endif
DO_SUMMARY = $(DO)perl test/summary.pl $^
DO_CITO = $(DO)mkdir -p $(@D) && ($(CITO) -o $@ $< || grep '//FAIL:.*\<$(subst .,,$(suffix $@))\>' $<)

all: cito.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs CiException.cs CiTree.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiParser.cs CiResolver.cs GenBase.cs GenTyped.cs GenCCpp.cs GenC.cs GenCpp.cs GenCs.cs GenJava.cs GenJs.cs GenPy.cs GenSwift.cs CiTo.cs)
	$(DO_BUILD)

test: test-c test-cpp test-cs test-java test-js test-py test-error
	perl test/summary.pl test/bin/*/*.txt

test-c: $(patsubst test/%.ci, test/bin/%/c.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-cpp: $(patsubst test/%.ci, test/bin/%/cpp.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-cs: $(patsubst test/%.ci, test/bin/%/cs.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-java: $(patsubst test/%.ci, test/bin/%/java.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-js: $(patsubst test/%.ci, test/bin/%/js.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-py: $(patsubst test/%.ci, test/bin/%/py.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-swift: $(patsubst test/%.ci, test/bin/%/swift.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-error: $(patsubst test/error/%.ci, test/bin/%/error.txt, $(wildcard test/error/*.ci))
	$(DO_SUMMARY)

test/bin/%/c.txt: test/bin/%/c.exe
	$(DO)./$< >$@ || grep '//FAIL:.*\<c\>' test/$*.ci

test/bin/%/cpp.txt: test/bin/%/cpp.exe
	$(DO)./$< >$@ || grep '//FAIL:.*\<cpp\>' test/$*.ci

test/bin/%/cs.txt: test/bin/%/cs.exe
	$(DO)$(MONO) $< >$@ || grep '//FAIL:.*\<cs\>' test/$*.ci

test/bin/%/java.txt: test/bin/%/Test.class test/bin/Runner.class
	$(DO)java -cp "test/bin$(JAVACPSEP)$(<D)" Runner >$@ || grep '//FAIL:.*\<java\>' test/$*.ci

test/bin/%/js.txt: test/bin/%/Run.js
	$(DO)node $< >$@ || grep '//FAIL:.*\<js\>' test/$*.ci

test/bin/%/py.txt: test/Runner.py test/bin/%/Test.py
	$(DO)PYTHONPATH=$(@D) $(PYTHON) $< >$@ || grep '//FAIL:.*\<py\>' test/$*.ci

test/bin/%/swift.txt: test/bin/%/Test.swift
	# TODO

test/bin/%/c.exe: test/bin/%/Test.c test/Runner.c
	$(DO)$(CC) -o $@ $(CFLAGS) -Wno-unused-function -I $(<D) $^ -lm || grep '//FAIL:.*\<c\>' test/$*.ci

test/bin/%/cpp.exe: test/bin/%/Test.cpp test/Runner.cpp
	$(DO)$(CXX) -o $@ $(CFLAGS) -I $(<D) $^ || grep '//FAIL:.*\<cpp\>' test/$*.ci

test/bin/%/cs.exe: test/bin/%/Test.cs test/Runner.cs
	$(DO)$(CSC) -out:$@ $^ || grep '//FAIL:.*\<cs\>' test/$*.ci

test/bin/%/Test.class: test/bin/%/Test.java
	$(DO)javac -d $(@D) $(<D)/*.java || grep '//FAIL:.*\<java\>' test/$*.ci

test/bin/%/Run.js: test/bin/%/Test.js
	$(DO)cat $< test/Runner.js >$@ || grep '//FAIL:.*\<js\>' test/$*.ci

test/bin/%/Test.c: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.cpp: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.cs: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.java: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.js: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.py: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.swift: test/%.ci cito.exe
	$(DO_CITO)

.PRECIOUS: test/bin/%/Test.c test/bin/%/Test.cpp test/bin/%/Test.cs test/bin/%/Test.java test/bin/%/Test.js test/bin/%/Test.py test/bin/%/Test.swift

test/bin/Runner.class: test/Runner.java test/bin/Basic/Test.class
	$(DO)javac -d $(@D) -cp test/bin/Basic $<

test/bin/%/error.txt: test/error/%.ci cito.exe
	mkdir -p $(@D) && ! $(CITO) -o $(@:%.txt=%.cs) $< 2>$@
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

.PHONY: all test test-c test-cpp test-cs test-java test-js test-py test-swift test-error install install-cito uninstall clean

.DELETE_ON_ERROR:
