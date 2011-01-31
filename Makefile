run: ../../a8/asap/git/asap.ci cito.exe
	./cito.exe -I ../../a8/asap/git/players -l cs $< | dos2unix >../../a8/asap/git/csharp/asapci.cs
	$(MAKE) -C ../../a8/asap/git/csharp
	#./cito.exe -l java asap.ci | dos2unix >asapci.java

cito.exe: CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs SourceGenerator.cs GenCs.cs GenJava.cs CiTo.cs
	csc -nologo -o+ -debug -out:$@ $^

clean:
	rm cito.exe cito.pdb

.PHONY: clean

.DELETE_ON_ERROR:
