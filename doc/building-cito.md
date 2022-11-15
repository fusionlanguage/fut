# How to compile and test cito

`cito` is currently written in C#.

You can build it with [.NET 7.0 or 6.0 SDK](https://dotnet.microsoft.com/en-us/download)
on Windows/macOS/Linux or .NET Framework on Windows only.

On Windows, the .NET SDK is included in Visual Studio 2022.

Build cito with the following command line:

    dotnet build

Alternatively, if you have GNU Make, build using:

    make

On Windows, this will build for .NET Framework.
On other platforms it is equivalent to `dotnet build`.

## Testing

To run cito tests, you will need:
* GNU Make
* perl
* GNU diff
* Clang C and C++ compilers
* [Java compiler](https://www.oracle.com/java/technologies/downloads/)
* [Node.js](https://nodejs.org/)
* Python
* [Swift](https://swift.org/)
* [GLib](https://wiki.gnome.org/Projects/GLib)

To get GNU Make, perl, GNU diff, Clang, Python and GLib on Windows,
install [MSYS2](https://www.msys2.org/), start "MSYS2 MinGW 64-bit"
and add packages with:

    pacman -S make perl diffutils mingw-w64-x86_64-clang mingw-w64-x86_64-python mingw-w64-x86_64-glib2

On macOS:

    brew install node glib pkg-config

Run the tests with:

    make test

The `-jN` option is supported and strongly recommended.
