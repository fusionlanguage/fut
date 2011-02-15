SOURCES = ../../a8/asap/git/apokeysnd.ci ../../a8/asap/git/asap.ci

run: $(SOURCES) cito.exe
	./cito.exe -I ../../a8/asap/git/players -l cs -n Sf.Asap $(SOURCES) | dos2unix >../../a8/asap/git/csharp/asapci.cs
	$(MAKE) -C ../../a8/asap/git/csharp
	./cito.exe -I ../../a8/asap/git/players -l java -n net.sf.asap $(SOURCES) | dos2unix >asapci.java
	./cito.exe -I ../../a8/asap/git/players -l js $(SOURCES) | dos2unix >asapci.js
	./cito.exe -I ../../a8/asap/git/players -l c $(SOURCES) | dos2unix >asapci.c

cito.exe: CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenCs.cs GenJava.cs GenJs.cs CiTo.cs
	csc -nologo -o+ -debug -out:$@ $^

clean:
	rm cito.exe cito.pdb

.PHONY: clean

.DELETE_ON_ERROR:
