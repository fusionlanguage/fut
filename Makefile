all: cito.exe

cito.exe: AssemblyInfo.cs CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenAs.cs GenD.cs CiTo.cs
	csc -nologo -out:$@ -o+ $^

check: hello.ci cito.exe
	./cito.exe -o hello.c hello.ci
	./cito.exe -l c99 -o hello99.c hello.ci
	./cito.exe -o HelloCi.java hello.ci
	./cito.exe -o hello.cs hello.ci
	./cito.exe -o hello.js hello.ci
	./cito.exe -o HelloCi.as hello.ci
	./cito.exe -o hello.d hello.ci

install: /bin/cito.exe

/bin/cito.exe: cito.exe
	cp $< $@

www: index.html

index.html: README
	asciidoc -o - -a www $< | xmllint --valid --nonet -o $@ -

clean:
	rm -f cito.exe cito.pdb hello.c hello.h hello99.c hello99.h HelloCi.java hello.cs hello.js HelloCi.as hello.d

.PHONY: all check install www clean

.DELETE_ON_ERROR:
