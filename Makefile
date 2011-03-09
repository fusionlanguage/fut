SOURCES = ../../a8/asap/git/pokey.ci ../../a8/asap/git/cpu6502.ci ../../a8/asap/git/asapinfo.ci ../../a8/asap/git/asap.ci

run: $(SOURCES) cito.exe
	./cito.exe -I ../../a8/asap/git/players -l cs -n Sf.Asap -o ../../a8/asap/git/csharp/asapci.cs $(SOURCES)
	./cito.exe -I ../../a8/asap/git/players -l java -n net.sf.asap -o ../../a8/asap/git/java $(SOURCES)
	./cito.exe -I ../../a8/asap/git/players -l js -o asapci.js $(SOURCES)
	./cito.exe -I ../../a8/asap/git/players -l c -o ../../a8/asap/git/asapci.c $(SOURCES)
	./cito.exe -I ../../a8/asap/git/players -l c99 -o asapci99.c $(SOURCES)
	./cito.exe -I ../../a8/asap/git/players -l c99 -D APOKEYSND -o ../../a8/asap/git/win32/rmt/pokey.c ../../a8/asap/git/pokey.ci
	$(MAKE) -C ../../a8/asap/git/csharp
	$(MAKE) -C ../../a8/asap/git/java
	$(MAKE) -C ../../a8/asap/git/win32 apokeysnd.dll asap-sdl.exe asap_dsf.dll bass_asap.dll xbmc_asap.dll

cito.exe: CiTree.cs SymbolTable.cs CiLexer.cs CiDocLexer.cs CiDocParser.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs SourceGenerator.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs CiTo.cs
	csc -nologo -debug -out:$@ $^
# -o+

clean:
	rm cito.exe cito.pdb

.PHONY: clean

.DELETE_ON_ERROR:
