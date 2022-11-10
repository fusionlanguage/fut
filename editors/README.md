# Ć Syntax Highlighting

## Visual Studio Code

Install the extension "Ć" from the VS Code Extension Marketplace.

## SciTE

[SciTE](https://www.scintilla.org/SciTE.html) is a fast programmer's text editor for Windows/macOS/Linux.

### Windows

Copy `ci.properties` to the installation directory.

### macOS

    cp editors/scite/ci.properties ~/Library/Containers/org.scintilla.SciTE/Data/Library/Application\ Support/org.scintilla.SciTE/

### Linux

    sudo apt install scite
    sudo cp editors/scite/ci.properties /usr/share/scite/

## Notepad++

    copy editors\notepad-plus-plus\ci.udl.xml %AppData%\"Notepad++"\userDefineLangs\
