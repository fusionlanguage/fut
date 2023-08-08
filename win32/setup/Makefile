VERSION = 3.0.0
DO_SIGN = signtool sign -d "Fusion Transpiler $(VERSION)" -n "Open Source Developer, Piotr Fusik" -tr http://time.certum.pl -fd sha256 -td sha256 $^ && touch $@

signed-msi: fut-$(VERSION).msi
	$(DO_SIGN)

fut-$(VERSION).msi: fut.wxs signed license.rtf fu.ico dialog.png banner.png
	wix build -arch x64 -ext WixToolset.UI.wixext -b ../.. -o $@ $<

signed: ../../fut.exe
	$(DO_SIGN)

../../fut.exe:
	$(MAKE) -C ../.. fut.exe
