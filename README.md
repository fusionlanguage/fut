[![Travis CI](https://travis-ci.com/pfusik/cito.svg?branch=master)](https://travis-ci.com/github/pfusik/cito)
[![GitHub Actions](https://github.com/fusionlanguage/fut/actions/workflows/test.yml/badge.svg)](https://github.com/fusionlanguage/fut/actions/workflows/test.yml)
[![codecov](https://codecov.io/gh/pfusik/cito/branch/master/graph/badge.svg?token=M7UX4WJKI3)](https://codecov.io/gh/pfusik/cito)

Fusion Programming Language
===========================

Fusion is a programming language which can be translated automatically to
C, C++, C#, D, Java, JavaScript, Python, Swift, TypeScript and OpenCL C.
Instead of writing code in all these languages, you can write it once in Fusion:

```csharp
public class HelloFu
{
    public static string GetMessage()
    {
        return "Hello, world!";
    }
}
```

Then translate into target languages using `cito` on the command line:
```
cito -o hello.c hello.fu
cito -o hello.cpp hello.fu
cito -o hello.cs hello.fu
cito -o hello.d hello.fu
cito -o HelloFu.java hello.fu # Java enforces filenames for public classes
cito -o hello.js hello.fu
cito -o hello.py hello.fu
cito -o hello.swift hello.fu
cito -o hello.ts hello.fu
cito -o hello.d.ts hello.fu # TypeScript declarations only
cito -o hello.cl hello.fu
```

The translated code is lightweight (no virtual machine, emulation nor
dependencies), human-readable and fits well with the target language,
including naming conventions and documentation comments.

Fusion is _not_ a general-purpose programming language.
Instead, it is meant for implementing portable reusable libraries.

See [Getting Started](doc/getting-started.md).
