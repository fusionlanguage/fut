[![Build Status](https://travis-ci.org/pfusik/cito.svg?branch=master)](https://travis-ci.org/pfusik/cito)

Ć Programming Language
======================

Ć is a programming language which can be translated automatically to
C, C++, C#, Java, JavaScript, Python, Swift, TypeScript and OpenCL C.
Instead of writing code in all these languages, you can write it once in Ć:

```csharp
public class HelloCi
{
    public static string GetMessage()
    {
        return "Hello, world!";
    }
}
```

Then translate into target languages using `cito` on the command line:
```
cito -o hello.c hello.ci
cito -o hello.cpp hello.ci
cito -o hello.cs hello.ci
cito -o HelloCi.java hello.ci # Java enforces filenames for public classes
cito -o hello.js hello.ci
cito -o hello.py hello.ci
cito -o hello.swift hello.ci
cito -o hello.ts hello.ci
cito -o hello.d.ts hello.ci # TypeScript declarations only
cito -o hello.cl hello.ci
```

The translated code is lightweight (no virtual machine, emulation nor
dependencies), human-readable and fits well the target language,
including naming conventions and documentation comments.

Ć is _not_ a general-purpose programming language.
Instead, it is meant for implementing portable reusable libraries.
See the complete [language reference](ci.md).

For build instructions, see the [INSTALL](INSTALL) file.
