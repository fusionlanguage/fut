all: test install

test: hello.ci cito.exe
	./cito.exe -l c -o hello.c hello.ci
	./cito.exe -l c99 -o hello99.c hello.ci
	./cito.exe -l java -o . hello.ci
	./cito.exe -l cs -o hello.cs hello.ci
	./cito.exe -l js -o hello.js hello.ci
	./cito.exe -l as -o . hello.ci
	./cito.exe -l d -o hello.d hello.ci

install: /cygdrive/c/bin/cito.exe

/cygdrive/c/bin/cito.exe: cito.exe
	cp $< $@

cito.exe: CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenAs.cs GenD.cs CiTo.cs
	csc -nologo -out:$@ -o+ $^

clean:
	rm cito.exe cito.pdb

.PHONY: all test install clean

.DELETE_ON_ERROR:
