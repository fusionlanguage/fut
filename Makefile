run: cito.exe asap.ci
	./cito.exe -l cs asap.ci | dos2unix >asapci.cs
	#./cito.exe -l java asap.ci | dos2unix >asapci.java

cito.exe: CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs SourceGenerator.cs GenCs.cs GenJava.cs CiTo.cs
	csc -nologo -o+ -debug -out:$@ $^

clean:
	rm cito.exe cito.pdb

.PHONY: clean

.DELETE_ON_ERROR:
