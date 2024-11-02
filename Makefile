VERSION = 3.2.7

prefix := /usr/local
bindir = $(prefix)/bin
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
FUT_HOST = cpp
CXXFLAGS = -Wall -O2
DOTNET_BASE_DIR := $(shell dotnet --info 2>/dev/null | sed -n 's/ Base Path:   //p')
ifdef DOTNET_BASE_DIR
DOTNET_REF_DIR := $(shell realpath '$(DOTNET_BASE_DIR)../../packs/Microsoft.NETCore.App.Ref'/*/ref/net* | head -1)
CSC := dotnet '$(DOTNET_BASE_DIR)Roslyn/bincore/csc.dll' -nologo $(patsubst %,'-r:$(DOTNET_REF_DIR)/System.%.dll', Collections Collections.Specialized Console Linq Memory Runtime Text.Json Text.RegularExpressions Threading)
endif
JAVAC = javac
TEST_CFLAGS = -Wall -Werror
TEST_CXXFLAGS = -std=c++20 -Wall -Werror
SWIFTC = swiftc
ifeq ($(OS),Windows_NT)
JAVACPSEP = ;
SWIFTC += -Xlinker -noexp -Xlinker -noimplib
else
ifeq ($(shell uname),Linux)
TEST_CFLAGS += -fsanitize=address -g
TEST_CXXFLAGS += -fsanitize=address -g
else ifeq ($(shell uname),Darwin)
TEST_ICUFLAGS = -I /opt/homebrew/opt/icu4c/include -L /opt/homebrew/opt/icu4c/lib
endif
TEST_CXXFLAGS += $(if $(findstring $*, StringToLower StringToLowerUpperMaxMin StringToUpper), $(TEST_ICUFLAGS) -licuuc)
JAVACPSEP = :
SWIFTC += -sanitize=address
endif
DC = dmd
PYTHON = python3 -B
MYPY = mypy

ifdef V
DO =
else
DO = @echo $@ &&
endif
DO_SUMMARY = $(DO)perl test/summary.pl $(filter %.txt, $^)
TARGET_LANG = $(subst mjs,js,$(subst .,,$(suffix $@)))
UC_TARGET_LANG = $(subst a,A,$(subst c,C,$(subst d,D,$(subst f,F,$(subst i,I,$(subst j,J,$(subst l,L,$(subst p,P,$(subst s,S,$(subst t,T,$(subst v,V,$(subst w,W,$(subst y,Y,$(TARGET_LANG))))))))))))))
DO_FUT = $(DO)mkdir -p $(@D) && ($(FUT) -o $@ $(if $(findstring $*, Namespace), -n Ns) -D $(UC_TARGET_LANG) -I $(<D) $(if $(findstring $*, MainArgs MainVoid), $<, $(filter %.fu, $^)) || grep '//FAIL:.*\<$(TARGET_LANG)\>' $<)
SOURCE_FU = Lexer.fu AST.fu Parser.fu ConsoleHost.fu Sema.fu GenBase.fu GenTyped.fu GenCCppD.fu GenCCpp.fu GenC.fu GenCl.fu GenCpp.fu GenCs.fu GenD.fu GenJava.fu GenJs.fu GenTs.fu GenPySwift.fu GenSwift.fu GenPy.fu
TESTS = $(filter-out test/Runner.fu, $(wildcard test/*.fu))
END_RUN_TEST = $(if $(findstring $*, MainArgs), foo bar) >$@ || grep '//FAIL:.*\<$(basename $(@F))\>' test/$*.fu

all: fut libfut.cpp libfut.cs libfut.js

ifeq ($(FUT_HOST),cpp)

FUT = ./fut

ifeq ($(OS),Windows_NT)

fut: fut.exe

fut.exe: fut.cpp libfut.cpp
	$(DO)$(CXX) -o $@ -std=c++20 $(CXXFLAGS) -s -static $^

else

fut: fut.cpp libfut.cpp
	$(DO)$(CXX) -o $@ -std=c++20 $(CXXFLAGS) $^

endif

else ifeq ($(FUT_HOST),cs)

FUT = dotnet run --no-build --

fut: bin/Debug/net6.0/fut.dll

bin/Debug/net6.0/fut.dll: $(addprefix $(srcdir),AssemblyInfo.cs fut.cs libfut.cs)
	dotnet build

else ifeq ($(FUT_HOST),java)

FUT = java --enable-preview -cp java org.fusionlanguage.Fut

fut: java/org/fusionlanguage/Fut.class

java/org/fusionlanguage/Fut.class: Fut.java java/GenBase.java
	$(DO)$(JAVAC) -source 21 --enable-preview -d java Fut.java java/*.java

else ifeq ($(FUT_HOST),node)

FUT = node fut.js

fut: libfut.js

else
$(error FUT_HOST must be "cpp", "cs", "java" or "node")
endif

libfut.cpp libfut.js: $(SOURCE_FU)
	$(DO)$(FUT) -o $@ $^

libfut.cs: $(SOURCE_FU)
	$(DO)$(FUT) -o $@ -n Fusion $^

java/GenBase.java: $(SOURCE_FU)
	$(DO)mkdir -p $(@D) && $(FUT) -o $@ -n org.fusionlanguage $^

test: test-c test-cpp test-cs test-d test-java test-js test-ts test-py test-swift test-cl test-error
	$(DO)perl test/summary.pl test/bin/*/*.txt

test-c test-GenC.fu: $(TESTS:test/%.fu=test/bin/%/c.txt)
	$(DO_SUMMARY)

test-cpp test-GenCpp.fu: $(TESTS:test/%.fu=test/bin/%/cpp.txt) libfut.cpp
	$(DO_SUMMARY)

test-cs test-GenCs.fu: $(TESTS:test/%.fu=test/bin/%/cs.txt) libfut.cs
	$(DO_SUMMARY)

test-d test-GenD.fu: $(TESTS:test/%.fu=test/bin/%/d.txt)
	$(DO_SUMMARY)

test-java test-GenJava.fu: $(TESTS:test/%.fu=test/bin/%/java.txt)
	$(DO_SUMMARY)

test-js test-GenJs.fu: $(TESTS:test/%.fu=test/bin/%/js.txt) libfut.js
	$(DO_SUMMARY)

test-ts test-GenTs.fu: $(TESTS:test/%.fu=test/bin/%/ts.txt)
	$(DO_SUMMARY)

test-py test-GenPy.fu: $(TESTS:test/%.fu=test/bin/%/py.txt)
	$(DO_SUMMARY)

test-swift test-GenSwift.fu: $(TESTS:test/%.fu=test/bin/%/swift.txt)
	$(DO_SUMMARY)

test-cl test-GenCl.fu: $(TESTS:test/%.fu=test/bin/%/cl.txt)
	$(DO_SUMMARY)

test-GenCCpp.fu: test-c test-cpp

test-GenCCppD.fu: test-c test-cpp test-d

test-GenPySwift.fu: test-py test-swift

test-error test-Lexer.fu test-Parser.fu test-Sema.fu: $(patsubst test/error/%.fu, test/bin/%/error.txt, $(wildcard test/error/*.fu))
	$(DO_SUMMARY)

test-%.fu: $(addsuffix .txt, $(addprefix test/bin/%/, c cpp cs d java js ts py swift cl))
	#

test/bin/%/c.txt: test/bin/%/c.exe
	$(DO)./$< $(END_RUN_TEST)

test/bin/%/cpp.txt: test/bin/%/cpp.exe
	$(DO)./$< $(END_RUN_TEST)

test/bin/%/cs.txt: test/bin/%/cs.dll test/cs.runtimeconfig.json
	$(DO)dotnet exec --runtimeconfig test/cs.runtimeconfig.json $< $(END_RUN_TEST)

test/bin/%/d.txt: test/bin/%/d.exe
	$(DO)./$< $(END_RUN_TEST)

test/bin/%/java.txt: test/bin/%/Test.class
	$(DO)java -cp $(<D) Runner $(END_RUN_TEST)

test/bin/%/js.txt: test/bin/%/Test.mjs
	$(DO)node $< $(END_RUN_TEST)

test/bin/%/ts.txt: test/bin/%/Test.ts test/node_modules test/tsconfig.json
	$(DO)test/node_modules/.bin/ts-node $< $(END_RUN_TEST)

test/bin/%/py.txt: test/bin/%/Test.py
ifdef MYPY
	$(DO)($(MYPY) --no-error-summary --allow-redefinition --disable-error-code=assignment --no-strict-optional $< && $(PYTHON) $< $(if $(findstring $*, MainArgs), foo bar)) >$@ || grep '//FAIL:.*\<$(basename $(@F))\>' test/$*.fu
else
	$(DO)$(PYTHON) $< $(END_RUN_TEST)
endif

test/bin/%/swift.txt: test/bin/%/swift.exe
	$(DO)./$< $(END_RUN_TEST)

test/bin/%/cl.txt: test/bin/%/cl.exe
	$(DO)./$< $(END_RUN_TEST)

test/bin/%/c.exe: test/bin/%/Test.c
	$(DO)$(CC) -o $@ $(TEST_CFLAGS) -Wno-unused-function -I $(<D) $^ `pkg-config --cflags --libs glib-2.0` -lm || grep '//FAIL:.*\<c\>' test/$*.fu

test/bin/%/cpp.exe: test/bin/%/Test.cpp
	$(DO)$(CXX) -o $@ $(TEST_CXXFLAGS) -I $(<D) $^ || grep '//FAIL:.*\<cpp\>' test/$*.fu

test/bin/%/cs.dll: test/bin/%/Test.cs
	$(DO)$(CSC) -out:$@ $^ || grep '//FAIL:.*\<cs\>' test/$*.fu

test/bin/%/d.exe: test/bin/%/Test.d
	$(DO)$(DC) -of$@ $(DFLAGS) -I$(<D) $^ || grep '//FAIL:.*\<d\>' test/$*.fu

test/bin/%/Test.class: test/bin/%/Test.java
	$(DO)javac -d $(@D) -encoding utf8 $(<D)/*.java || grep '//FAIL:.*\<java\>' test/$*.fu

test/bin/%/swift.exe: test/bin/%/Test.swift
	$(DO)$(SWIFTC) -o $@ $^ || grep '//FAIL:.*\<swift\>' test/$*.fu

test/bin/%/cl.exe: test/bin/%/cl.o test/Runner-cl.cpp
	$(DO)clang++ -o $@ $(TEST_CFLAGS) $^ || grep '//FAIL:.*\<cl\>' test/$*.fu

test/bin/%/cl.o: test/bin/%/Test.cl
	$(DO)clang -c -o $@ $(TEST_CFLAGS) -Wno-constant-logical-operand -cl-std=CL2.0 -include opencl-c.h $< || grep '//FAIL:.*\<cl\>' test/$*.fu

test/bin/%/Test.c: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.cpp: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.cs: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.d: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.java: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.mjs: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.ts: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.py: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.swift: test/%.fu test/Runner.fu fut
	$(DO_FUT)

test/bin/%/Test.cl: test/%.fu fut
	$(DO_FUT)

test/bin/Namespace/java.txt: test/bin/Namespace/Test.class
	$(DO)java -cp "$(<D)$(JAVACPSEP)test" Ns.Runner >$@

test/bin/Resource/java.txt: test/bin/Resource/Test.class test/lipsum.txt
	$(DO)java -cp "$(<D)$(JAVACPSEP)test" Runner >$@

.PRECIOUS: test/bin/%/Test.c test/bin/%/Test.cpp test/bin/%/Test.cs test/bin/%/Test.d test/bin/%/Test.java test/bin/%/Test.mjs test/bin/%/Test.ts test/bin/%/Test.d.ts test/bin/%/Test.py test/bin/%/Test.swift test/bin/%/Test.cl

test/node_modules: test/package.json
	cd $(<D) && npm i --no-package-lock

test/bin/%/error.txt: test/error/%.fu fut
	$(DO)mkdir -p $(@D) && $(FUT) -o $(@:%.txt=%.cs) $< 2>$@ || test $$? -eq 1 && perl -ne 'print "$$ARGV($$.): $$1\n" while m!//(ERROR: .+?)(?=$$| //)!g' $< | diff -u --strip-trailing-cr - $@ && echo PASSED >$@

test-transpile: $(patsubst test/%.fu, test/$(FUT_HOST)/%/all, $(TESTS)) test/$(FUT_HOST)/fut/all

test/$(FUT_HOST)/%/all: test/%.fu fut
	$(DO)mkdir -p $(@D) && $(FUT) -o $(@D)/Test.c,cpp,cs,d,java,js,d.ts,ts,py,swift,cl $< || true

test/$(FUT_HOST)/fut/all: $(SOURCE_FU) fut
	$(DO)mkdir -p $(@D) && $(FUT) -o $(@D)/Test.cpp,cs,d,java,js,d.ts,ts $(SOURCE_FU)

test/$(FUT_HOST)/Resource/all: test/Resource.fu fut
	$(DO)mkdir -p $(@D) && $(FUT) -o $(@D)/Test.c,cpp,cs,d,java,js,d.ts,ts,py,swift,cl -I $(<D) $<

coverage/output.xml:
	dotnet-coverage collect -f xml -o $@ "make -j`nproc` test-transpile test-error FUT_HOST=cs"

coverage: coverage/output.xml
	reportgenerator -reports:$< -targetdir:coverage

codecov: coverage/output.xml
	./codecov -f $<

host-diff:
	$(MAKE) test-transpile FUT_HOST=cpp
	$(MAKE) test-transpile FUT_HOST=cs
	$(MAKE) test-transpile FUT_HOST=node
	diff -ruI "[0-9][Ee][-+][0-9]\|\.0" test/cpp test/cs
	diff -ruI "[0-9][Ee][-+][0-9]" test/cs test/node

host-diff-java:
	$(MAKE) test-transpile FUT_HOST=java
	diff -ruI "[0-9][Ee][-+][0-9]\|\.0" test/cpp test/java

install: fut
	mkdir -p $(DESTDIR)$(bindir)
	cp $< $(DESTDIR)$(bindir)/

uninstall:
	$(RM) $(DESTDIR)$(bindir)/fut

install-vim: editors/vim/syntax/fusion.vim editors/vim/ftdetect/fusion.vim
	mkdir -p ~/.vim/syntax ~/.vim/ftdetect
	cp editors/vim/syntax/fusion.vim ~/.vim/syntax/
	cp editors/vim/ftdetect/fusion.vim ~/.vim/ftdetect/

uninstall-vim:
	$(RM) ~/.vim/ftdetect/fusion.vim ~/.vim/syntax/fusion.vim

install-nvim install-neovim: editors/vim/syntax/fusion.vim editors/vim/ftdetect/fusion.vim
	mkdir -p ~/.config/nvim/syntax ~/.config/nvim/ftdetect
	cp editors/vim/syntax/fusion.vim ~/.config/nvim/syntax/
	cp editors/vim/ftdetect/fusion.vim ~/.config/nvim/ftdetect/

uninstall-nvim uninstall-neovim:
	$(RM) ~/.config/nvim/ftdetect/fusion.vim ~/.config/nvim/syntax/fusion.vim

srcdist:
	git archive -o ../fut-$(VERSION).tar.gz --prefix=fut-$(VERSION)/ -9 HEAD

deb64: srcdist
	scp ../fut-$(VERSION).tar.gz vm:.
	ssh vm 'rm -rf fut-$(VERSION) && tar xf fut-$(VERSION).tar.gz && cd fut-$(VERSION) && debuild -b -us -uc'
	scp vm:fut_$(VERSION)-1_amd64.deb ..

rpm64: srcdist
	scp ../fut-$(VERSION).tar.gz vm:.
	ssh vm 'rpmbuild -tb fut-$(VERSION).tar.gz'
	scp vm:rpmbuild/RPMS/x86_64/fut-$(VERSION)-1.x86_64.rpm ..

clean:
	$(RM) fut fut.exe
	$(RM) -r test/bin test/cpp test/cs test/node

.PHONY: all test test-c test-cpp test-cs test-d test-java test-js test-ts test-py test-swift test-cl test-error test-transpile \
	coverage/output.xml coverage codecov host-diff host-diff-java install uninstall \
	install-vim uninstall-vim install-nvim install-neovim uninstall-nvim uninstall-neovim srcdist deb64 rpm64 clean

.DELETE_ON_ERROR:

.SUFFIXES:
