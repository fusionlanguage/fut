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

Then translate into target languages using `fut` on the command line:
```
fut -o hello.c hello.fu
fut -o hello.cpp hello.fu
fut -o hello.cs hello.fu
fut -o hello.d hello.fu
fut -o HelloFu.java hello.fu # Java enforces filenames for public classes
fut -o hello.js hello.fu
fut -o hello.py hello.fu
fut -o hello.swift hello.fu
fut -o hello.ts hello.fu
fut -o hello.d.ts hello.fu # TypeScript declarations only
fut -o hello.cl hello.fu
```

The translated code is lightweight (no virtual machine, emulation nor
dependencies), human-readable and fits well with the target language,
including naming conventions and documentation comments.

Fusion is _not_ a general-purpose programming language.
Instead, it is meant for implementing portable reusable libraries.

See [Getting Started](doc/getting-started.md).
