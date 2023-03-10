prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
DOTNET_BASE_DIR := $(shell dotnet --info | sed -n 's/ Base Path:   //p')
DOTNET_REF_DIR := $(DOTNET_BASE_DIR)../../packs/Microsoft.NETCore.App.Ref/6.0.14/ref/net6.0
CSC := dotnet '$(DOTNET_BASE_DIR)Roslyn/bincore/csc.dll' -nologo $(patsubst %,'-r:$(DOTNET_REF_DIR)/System.%.dll', Collections Collections.Specialized Console Linq Runtime Text.RegularExpressions Threading)
CFLAGS = -Wall -Werror
SWIFTC = swiftc
ifeq ($(OS),Windows_NT)
DO_BUILD = "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe" -nologo -out:$@ $(CSCFLAGS) $^
CITO = ./cito.exe
JAVACPSEP = ;
SWIFTC += -no-color-diagnostics -sdk '$(SDKROOT)' -Xlinker -noexp -Xlinker -noimplib
else
DO_BUILD = dotnet build
CITO = dotnet run --no-build --
JAVACPSEP = :
CFLAGS += -fsanitize=address -g
SWIFTC += -sanitize=address
endif
CC = clang
CXX = clang++ -std=c++2a
DC = dmd
PYTHON = python3 -B

MAKEFLAGS = -r
ifdef V
DO =
else
DO = @echo $@ &&
endif
DO_SUMMARY = $(DO)perl test/summary.pl $(filter %.txt, $^)
DO_CITO = $(DO)mkdir -p $(@D) && ($(CITO) -o $@ $< || grep '//FAIL:.*\<$(subst .,,$(suffix $@))\>' $<)

all: cito.exe

cito.exe: $(addprefix $(srcdir),AssemblyInfo.cs Transpiled.cs FileResourceSema.cs GenBase.cs GenTyped.cs GenCCpp.cs GenCCppD.cs GenC.cs GenCpp.cs GenCs.cs GenD.cs GenJava.cs GenJs.cs GenPySwift.cs GenPy.cs GenSwift.cs GenTs.cs GenCl.cs CiTo.cs)
	$(DO_BUILD)

Transpiled.cs: Lexer.ci AST.ci Parser.ci ConsoleParser.ci Sema.ci GenBaseBase.ci
	cito -o $@ -n Foxoft.Ci $^

test: test-c test-cpp test-cs test-d test-java test-js test-ts test-py test-swift test-cl test-error
	perl test/summary.pl test/bin/*/*.txt

test-c test-GenC.cs: $(patsubst test/%.ci, test/bin/%/c.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-cpp test-GenCpp.cs: $(patsubst test/%.ci, test/bin/%/cpp.txt, $(wildcard test/*.ci)) test/bin/CiCheck/Test.cpp
	$(DO_SUMMARY)

test-cs test-GenCs.cs: $(patsubst test/%.ci, test/bin/%/cs.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-d test-GenD.cs: $(patsubst test/%.ci, test/bin/%/d.txt, $(wildcard test/*.ci)) test/bin/CiCheck/Test.d
	$(DO_SUMMARY)

test-java test-GenJava.cs: $(patsubst test/%.ci, test/bin/%/java.txt, $(wildcard test/*.ci)) test/bin/CiCheck/CiSema.java
	$(DO_SUMMARY)

test-js test-GenJs.cs: $(patsubst test/%.ci, test/bin/%/js.txt, $(wildcard test/*.ci)) test/bin/CiCheck/js.txt
	$(DO_SUMMARY)

test-ts test-GenTs.cs: $(patsubst test/%.ci, test/bin/%/ts.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-py test-GenPy.cs: $(patsubst test/%.ci, test/bin/%/py.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-swift test-GenSwift.cs: $(patsubst test/%.ci, test/bin/%/swift.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-cl test-GenCl.cs: $(patsubst test/%.ci, test/bin/%/cl.txt, $(wildcard test/*.ci))
	$(DO_SUMMARY)

test-GenCCpp.cs: test-c test-cpp

test-GenPySwift.cs: test-py test-swift

test-error test-Lexer.ci test-Parser.ci test-Sema.ci: $(patsubst test/error/%.ci, test/bin/%/error.txt, $(wildcard test/error/*.ci))
	$(DO_SUMMARY)

test-%.ci: $(addsuffix .txt, $(addprefix test/bin/%/, c cpp cs java js ts py swift cl))
	#

test/bin/%/c.txt: test/bin/%/c.exe
	$(DO)./$< >$@ || grep '//FAIL:.*\<c\>' test/$*.ci

test/bin/%/cpp.txt: test/bin/%/cpp.exe
	$(DO)./$< >$@ || grep '//FAIL:.*\<cpp\>' test/$*.ci

test/bin/%/cs.txt: test/bin/%/cs.dll test/cs.runtimeconfig.json
	$(DO)dotnet exec --runtimeconfig test/cs.runtimeconfig.json $< >$@ || grep '//FAIL:.*\<cs\>' test/$*.ci

test/bin/%/d.txt: test/bin/%/d.exe
	$(DO)./$< >$@ || grep '//FAIL:.*\<d\>' test/$*.ci

test/bin/%/java.txt: test/bin/%/Test.class test/bin/Runner.class
	$(DO)java -cp "test/bin$(JAVACPSEP)$(<D)" Runner >$@ || grep '//FAIL:.*\<java\>' test/$*.ci

test/bin/%/js.txt: test/bin/%/Test.js test/bin/%/Runner.js
	$(DO)(cd $(@D) && node Runner.js >$(@F)) || grep '//FAIL:.*\<js\>' test/$*.ci

test/bin/%/ts.txt: test/bin/%/Test.ts test/node_modules test/tsconfig.json
	$(DO)test/node_modules/.bin/ts-node $< >$@ || grep '//FAIL:.*\<ts\>' test/$*.ci

test/bin/%/py.txt: test/Runner.py test/bin/%/Test.py
	$(DO)PYTHONPATH=$(@D) $(PYTHON) $< >$@ || grep '//FAIL:.*\<py\>' test/$*.ci

test/bin/%/swift.txt: test/bin/%/swift.exe
	$(DO)./$< >$@ || grep '//FAIL:.*\<swift\>' test/$*.ci

test/bin/%/cl.txt: test/bin/%/cl.exe
	$(DO)./$< >$@ || grep '//FAIL:.*\<cl\>' test/$*.ci

test/bin/%/c.exe: test/bin/%/Test.c test/Runner.c
	$(DO)$(CC) -o $@ $(CFLAGS) -Wno-unused-function -I $(<D) $^ `pkg-config --cflags --libs glib-2.0` -lm || grep '//FAIL:.*\<c\>' test/$*.ci

test/bin/%/cpp.exe: test/bin/%/Test.cpp test/Runner.cpp
	$(DO)$(CXX) -o $@ $(CFLAGS) -I $(<D) $^ || grep '//FAIL:.*\<cpp\>' test/$*.ci

test/bin/%/cs.dll: test/bin/%/Test.cs test/Runner.cs
	$(DO)$(CSC) -out:$@ $^ || grep '//FAIL:.*\<cs\>' test/$*.ci

test/bin/%/d.exe: test/bin/%/Test.d test/Runner.d
	$(DO)$(DC) -of$@ $(DFLAGS) -I$(<D) $^ || grep '//FAIL:.*\<d\>' test/$*.ci

test/bin/%/Test.class: test/bin/%/Test.java
	$(DO)javac -d $(@D) -encoding utf8 $(<D)/*.java || grep '//FAIL:.*\<java\>' test/$*.ci

test/bin/%/Runner.js: test/Runner.js
	$(DO)mkdir -p $(@D) && cp $< $@

test/bin/%/swift.exe: test/bin/%/Test.swift test/main.swift
	$(DO)$(SWIFTC) -o $@ $^ || grep '//FAIL:.*\<swift\>' test/$*.ci

test/bin/%/cl.exe: test/bin/%/cl.o test/Runner-cl.cpp
	$(DO)clang++ -o $@ $(CFLAGS) $^ || grep '//FAIL:.*\<cl\>' test/$*.ci

test/bin/%/cl.o: test/bin/%/Test.cl
	$(DO)clang -c -o $@ $(CFLAGS) -Wno-constant-logical-operand -cl-std=CL2.0 -include opencl-c.h $< || grep '//FAIL:.*\<cl\>' test/$*.ci

test/bin/%/Test.c: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.cpp: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.cs: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.d: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.java: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.js: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.ts: test/%.ci cito.exe test/Runner.ts
	$(DO)mkdir -p $(@D) && ($(CITO) -D TS -o $@ $< && cat test/Runner.ts >>$@ || grep '//FAIL:.*\<ts\>' $<)

test/bin/%/Test.py: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.swift: test/%.ci cito.exe
	$(DO_CITO)

test/bin/%/Test.cl: test/%.ci cito.exe
	$(DO_CITO)

test/bin/CiCheck/cpp.txt: test/bin/CiCheck/cpp.exe Lexer.ci AST.ci Parser.ci ConsoleParser.ci Sema.ci GenBaseBase.ci
	$(DO)./$< $(filter %.ci, $^) >$@

test/bin/CiCheck/cpp.exe: test/bin/CiCheck/Test.cpp test/CiCheck.cpp
	$(DO)$(CXX) -o $@ $(CFLAGS) -I $(<D) $^

test/bin/CiCheck/d.txt: test/bin/CiCheck/d.exe Lexer.ci AST.ci Parser.ci ConsoleParser.ci Sema.ci GenBaseBase.ci
	$(DO)./$< $(filter %.ci, $^) >$@

test/bin/CiCheck/d.exe: test/bin/CiCheck/Test.d test/CiCheck.d
	$(DO)$(DC) -of$@ $(DFLAGS) -I$(<D) $^

test/bin/CiCheck/java.txt: test/bin/CiCheck/CiSema.class Lexer.ci AST.ci Parser.ci ConsoleParser.ci Sema.ci GenBaseBase.ci
	$(DO)java -cp "$(<D)" --enable-preview CiCheck $(filter %.ci, $^) >$@

test/bin/CiCheck/CiSema.class: test/bin/CiCheck/CiSema.java test/CiCheck.java
	$(DO)javac -d $(@D) -encoding utf8 --enable-preview -source 19 $(<D)/*.java test/CiCheck.java

test/bin/CiCheck/js.txt: test/CiCheck.js test/bin/CiCheck/Test.js Lexer.ci AST.ci Parser.ci ConsoleParser.ci Sema.ci GenBaseBase.ci
	$(DO)node test/CiCheck.js Lexer.ci AST.ci Parser.ci ConsoleParser.ci Sema.ci >$@

test/bin/CiCheck/Test.cpp test/bin/CiCheck/Test.d test/bin/CiCheck/CiSema.java test/bin/CiCheck/Test.js test/bin/CiCheck/Test.ts: Lexer.ci AST.ci Parser.ci ConsoleParser.ci Sema.ci cito.exe
	$(DO)mkdir -p $(@D) && $(CITO) -o $@ $(filter %.ci, $^)

test/bin/Resource/java.txt: test/bin/Resource/Test.class test/bin/Runner.class
	$(DO)java -cp "test/bin$(JAVACPSEP)$(<D)$(JAVACPSEP)test" Runner >$@

$(addprefix test/bin/Resource/Test., c cpp cs d java js ts py swift cl): test/Resource.ci cito.exe
	$(DO)mkdir -p $(@D) && ($(CITO) -o $@ -I $(<D) $< || grep '//FAIL:.*\<$(subst .,,$(suffix $@))\>' $<)

.PRECIOUS: test/bin/%/Test.c test/bin/%/Test.cpp test/bin/%/Test.cs test/bin/%/Test.d test/bin/%/Test.java test/bin/%/Test.js test/bin/%/Test.ts test/bin/%/Test.d.ts test/bin/%/Test.py test/bin/%/Test.swift test/bin/%/Test.cl

test/bin/Runner.class: test/Runner.java test/bin/Basic/Test.class
	$(DO)javac -d $(@D) -cp test/bin/Basic $<

test/node_modules: test/package.json
	cd $(<D) && npm i --no-package-lock

test/bin/%/error.txt: test/error/%.ci cito.exe
	$(DO)mkdir -p $(@D) && ! $(CITO) -o $(@:%.txt=%.cs) $< 2>$@ && perl -ne 'print "$$ARGV($$.): $$1\n" while m!//(ERROR: .+?)(?=$$| //)!g' $< | diff -u --strip-trailing-cr - $@ && echo PASSED >$@

test-transpile: $(foreach t, $(patsubst test/%.ci, test/bin/%/Test., $(wildcard test/*.ci)), $tc $tcpp $tcs $td $tjava $tjs $tts $tpy $tswift $tcl)

coverage/output.xml:
	$(MAKE) clean cito.exe CSCFLAGS=-debug+
	dotnet-coverage collect -f xml -o $@ "make -j`nproc` test-transpile test-error"

coverage: coverage/output.xml
	reportgenerator -reports:$< -targetdir:coverage

codecov: coverage/output.xml
	./codecov -f $<

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

.PHONY: all test test-c test-cpp test-cs test-d test-java test-js test-ts test-py test-swift test-cl test-error test-transpile coverage/output.xml coverage codecov install install-cito uninstall clean

.DELETE_ON_ERROR:
