Ć Programming Language
======================

ifdef::www[]
http://sourceforge.net/projects/cito/files/cito/[Download] |
http://sourceforge.net/p/cito/code[Browse source code (Git)] |
http://sourceforge.net/projects/cito/[SourceForge project page]
endif::www[]

`cito` automatically translates the link:ci.html[Ć programming language]
to C, Java, C#, JavaScript, ActionScript, Perl and http://dlang.org/[D].
Ć is a new language, aimed at crafting 'portable programming libraries', with syntax akin to C#.
The translated code is lightweight (no virtual machine, emulation nor large runtime), human-readable
and fits well the target language (including naming conventions and documentation comments).

Current version of Ć doesn't support standalone programs or even console output,
so your "Hello world" could be the following 'library':
-------------------
public class HelloCi
{
    public static string GetMessage()
    {
        return "Hello, world!";
    }
}
-------------------

See http://cito.sourceforge.net/hello.html[here] for a slightly bigger example and its translations.
See http://asap.sourceforge.net/[ASAP] and http://fail.sourceforge.net/[FAIL] for real programs written in Ć.

How to install
--------------

`cito` is written in C#.

On Windows you need .NET Framework 3.5.
http://sourceforge.net/projects/cito/files/cito/0.4.0/cito-0.4.0-bin.zip/download[Download cito binaries]
and extract `cito.exe` and `cipad.exe` to a directory in your `PATH` environment variable.

On other platforms install http://www.mono-project.com[Mono]. Mono runs .NET executables:
-------------------
mono cito.exe
-------------------

For your convenience, create Mono wrapper scripts such as:
-------------------
#!/bin/sh
exec /usr/bin/mono /usr/local/lib/cito/cito.exe "$@"
-------------------
so that you can type `cito` instead of `mono cito.exe`.

If you want to compile `cito` from source code, see http://cito.sourceforge.net/INSTALL.html[compilation instructions].

How to use
----------

Call `cito` from command prompt passing source files (with the extension `.ci`) and one of the destination files
(its filename extension determines the target language), for example:
-------------------
cito -o hello.c hello.ci
cito -o HelloCi.java hello.ci
-------------------

`cito` only generates source code. It is your responsibility to compile it.

`cipad` does live translation as you type Ć code. It has a small built-in sample.

Contact
-------

Please subscribe https://lists.sourceforge.net/lists/listinfo/cito-users[our mailing list].
Any comments are welcome!

History
-------

cito 0.4.0 (2013-05-18)::

- Perl 5 back-end.
- Dynamic object allocation.
- `default` clause must be the last in `switch`.
- String concatenation in D.
- Java fix for code such as `cond ? 1 : 0`.
- C fix for code such as `if (cond) methodThatThrows(); else stmt();`
- `cipad` opens files, has font selection and improved UI.

cito 0.3.0 (2013-02-15)::

- Class inheritance with virtual methods.
- Dynamic array allocation.
- Extended constant folding.
- JavaScript Typed Arrays.
- Installs Mono wrappers for `cito` and `cipad`.

cito 0.2.0 (2011-08-03)::

- Created `cipad` - a simple Ć editor with on-the-fly translation.
- Changed syntax of arrays of storage: `MyClass[]()` -> `MyClass()[]`, `string[](8)` -> `string(8)[]`.
- Fixes for the `byte` type in ActionScript and C#.
- String comparisons in C and Java.
- Fixed translation of delegates to C in `cito` compiled on .NET < 4.
- Fixed errors with the new and 64-bit D compilers.
- Some optimizations for string handling in C.
- Documentation translated to English.

cito 0.1.0 (2011-05-24)::

Initial release.

Authors
-------

Piotr Fusik::
Created Ć and tools.

Adrian Matoga::
Created the D back-end.

ifdef::www[]
image::http://sflogo.sourceforge.net/sflogo.php?group_id=389251&amp;type=10["Get Ć Translator at SourceForge.net. Fast, secure and Free Open Source software downloads",width=80,height=15,link="http://sourceforge.net/projects/cito"]
endif::www[]
