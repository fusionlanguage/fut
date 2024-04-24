# Fusion Syntax Highlighting

## Visual Studio Code

Install the [Fusion](https://marketplace.visualstudio.com/items?itemName=fusionlanguage.fusion)
extension from the VS Code Extension Marketplace.

## Vim

Install syntax highlighting in your home directory:

    make install-vim

## Neovim

Install syntax highlighting in your home directory:

    make install-nvim

## SciTE

[SciTE](https://www.scintilla.org/SciTE.html) is a fast programmer's text editor for Windows/macOS/Linux.

Windows: Copy `fusion.properties` to the installation directory.

macOS:

    cp editors/scite/fusion.properties ~/Library/Containers/org.scintilla.SciTE/Data/Library/Application\ Support/org.scintilla.SciTE/

Linux:

    sudo apt install scite
    sudo cp editors/scite/fusion.properties /usr/share/scite/

## Notepad++

    copy editors\notepad-plus-plus\Fusion.udl.xml %AppData%\"Notepad++"\userDefineLangs\
