run: ../../a8/asap/git/asap.ci cito.exe
	./cito.exe -I ../../a8/asap/git/players -l cs -n Sf.Asap $< | dos2unix >../../a8/asap/git/csharp/asapci.cs
	$(MAKE) -C ../../a8/asap/git/csharp
	./cito.exe -I ../../a8/asap/git/players -l java -n net.sf.asap $< | dos2unix >asapci.java
	./cito.exe -I ../../a8/asap/git/players -l js $< | dos2unix >asapci.js
	./cito.exe -I ../../a8/asap/git/players -l c $< | dos2unix >asapci.c

cito.exe: CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenCs.cs GenJava.cs GenJs.cs CiTo.cs
	csc -nologo -o+ -debug -out:$@ $^

clean:
	rm cito.exe cito.pdb

.PHONY: clean

.DELETE_ON_ERROR:
