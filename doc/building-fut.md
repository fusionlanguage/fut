# How to compile and test fut

`fut` is implemented in Fusion.
To solve the egg-and-chicken problem, its transpilations to C++, C#
and JavaScript are included with the source code.

## Building a C++ fut

You need a C++20 compiler, such as GCC 13 or Clang 16.
Build with:

    make

## Building a C# fut

You need [.NET 6.0 or newer SDK](https://dotnet.microsoft.com/en-us/download).
On Windows, it is included in Visual Studio 2022.
Build with:

    make FUT_HOST=cs

## Building a Java fut

You need JDK 21.
Build with:

    make java/GenBase.java
    make FUT_HOST=java

## Building a Node.js fut

You need [Node.js](nodejs.org).
Build with:

    make FUT_HOST=node

## Testing

To run `fut` tests, you will need:
* GNU Make
* perl
* GNU diff
* C and C++ compilers
* Java compiler
* Node.js
* Python
* mypy static type checker
* [Swift](https://swift.org/)
* [GLib](https://wiki.gnome.org/Projects/GLib)

To get GNU Make, perl, GNU diff, Clang, Node.js, Python, mypy and GLib on Windows,
install [MSYS2](https://www.msys2.org/), start "MSYS2 MinGW 64-bit"
and add packages with:

    pacman -S make perl diffutils mingw-w64-x86_64-gcc mingw-w64-x86_64-clang mingw-w64-x86_64-nodejs mingw-w64-x86_64-python mingw-w64-x86_64-mypy mingw-w64-x86_64-glib2

On macOS:

    brew install node glib pkg-config

Run the tests with:

    make test

The `-jN` option is supported and strongly recommended.
