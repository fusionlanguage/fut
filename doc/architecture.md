# Architecture of fut

This document describes the internal architecture of `fut`, the Fusion
transpiler. It is aimed at contributors who want to fix bugs, add features to
the language, or add a new target language. For user-facing documentation see
[Getting Started](getting-started.md) and the [language reference](reference.md).

## Overview

`fut` reads one or more Fusion (`.fu`) source files and emits equivalent,
idiomatic source code in one or more target languages (C, C++, C#, D, Java,
JavaScript, TypeScript, Python, Swift and OpenCL C). It is a pure
source-to-source translator: there is no runtime, virtual machine or bundled
library — the generated code is meant to be read, compiled and shipped like
hand-written code.

The transpiler itself is **written in Fusion** and transpiled to C++, C# and
JavaScript to build the actual `fut` executable. See
[Self-hosting and bootstrapping](#self-hosting-and-bootstrapping) below.

## The compilation pipeline

A single run flows through four stages. The first three are shared by every
target language; only the last one is language-specific.

```
.fu source ──▶ Lexer ──▶ Parser ──▶ Sema ──▶ Gen<Lang> ──▶ target source
              (tokens)   (AST)     (resolved AST)          (one per -o ext)
```

1. **Lexing** (`Lexer.fu`) — `FuLexer` turns raw UTF-8 bytes into a stream of
   `FuToken` values. It also handles literals, documentation comments and the
   `#if` / `#elif` / `#else` / `#endif` preprocessor, evaluated against symbols
   supplied with the `-D` option.

2. **Parsing** (`Parser.fu`) — `FuParser` extends `FuLexer` and builds the
   abstract syntax tree. Its entry point is `Parse(filename, input, length)`,
   which is called once per input file; all files contribute to the same
   program. Parsing is purely syntactic — names are not yet resolved and
   expressions are not yet typed.

3. **Semantic analysis** (`Sema.fu`) — `FuSema.Process()` walks the parsed
   program and resolves it: it links symbol references to declarations, infers
   and checks types, resolves overloads, evaluates constant expressions,
   enforces visibility and object-lifetime (storage / pointer / dynamic) rules,
   and reports errors. After this stage every `FuExpr` has a `Type` and the AST
   is fully resolved and ready to generate from.

4. **Code generation** (`GenBase.fu` and the `Gen*.fu` files) — the selected
   generator walks the resolved AST and writes target source. One generator
   instance runs per output extension.

The driver that wires these stages together lives in the host program (see
[The CLI entry point](#the-cli-entry-point)).

## The AST

The AST types are defined in `AST.fu`. A few enums drive the rest of the
compiler:

- `FuToken` — lexical tokens (in `Lexer.fu`).
- `FuId` — identifies built-in types and members (e.g. `IntType`, `StringClass`,
  `ListClass`, `StringLength`) so `Sema` and the generators can special-case
  them without string comparisons.
- `FuVisibility`, `FuCallType`, `FuPriority` — visibility levels, method
  dispatch kinds, and operator precedence used when emitting parentheses.

The node hierarchy is rooted at `FuStatement`, with `FuExpr` for expressions:

- **Statements**: `FuBlock`, `FuIf`, `FuSwitch`, `FuReturn`, `FuThrow`,
  `FuAssert`, `FuLock`, `FuNative`, and the loops (`FuFor`, `FuForeach`,
  `FuWhile`, `FuDoWhile`, all deriving from `FuLoop`).
- **Expressions**: literals (`FuLiteral` and subclasses), `FuBinaryExpr`,
  `FuPrefixExpr` / `FuPostfixExpr`, `FuSelectExpr` (ternary), `FuCallExpr`,
  `FuSymbolReference`, `FuInterpolatedString`, `FuLambdaExpr`,
  `FuAggregateInitializer`.
- **Symbols and types**: `FuSymbol` is the base for named entities. Members are
  `FuField`, `FuConst`, `FuMethod`, `FuProperty`; containers are `FuClass` and
  `FuEnum`. The type system includes `FuClassType` and its read-write / owning
  variants (`FuStorageType`, `FuDynamicPtrType`, `FuArrayStorageType`) that
  encode Fusion's object-lifetime model, plus numeric types (`FuRangeType`,
  `FuFloatingType`).

Two container nodes tie everything together:

- `FuSystem` — the synthetic root scope holding all built-in types and methods
  (`string`, `List`, `Dictionary`, `Math`, `Regex`, and so on). It is created
  once and used as the parent scope of the user program.
- `FuProgram` — the resolved program: the collection of top-level classes and
  enums, source-file / line bookkeeping used for error locations, and embedded
  resources.

## The visitor pattern and the generator hierarchy

Code generation uses the visitor pattern. `FuVisitor` (in `AST.fu`) declares one
`Visit*` method per concrete statement and expression node; each node implements
`AcceptStatement` / `Accept` to dispatch back to the visitor.

`GenBase` is the abstract `FuVisitor` implementation shared by all backends. It
owns the output `TextWriter` (via a `GenHost`), indentation, precedence-aware
parenthesization, and default traversal logic. Each concrete generator overrides
the pieces that differ for its language. The public entry point of a generator
is `WriteProgram(program, outputFile, namespace)`; helpers like
`GetTargetName`, `NotSupported`/`NotYet`, and `Write` support consistent
output and diagnostics.

Backends are organized so related languages share code:

```
GenBase                       (FuVisitor: shared writer, indentation, traversal)
├── GenTyped                  (statically-typed languages)
│   ├── GenCCppD              (C / C++ / D family)
│   │   ├── GenCCpp
│   │   │   ├── GenC
│   │   │   │   └── GenCl     (OpenCL C)
│   │   │   └── GenCpp
│   │   └── GenD
│   ├── GenCs                 (C#)
│   └── GenJava
├── GenJsNoModule
│   └── GenJs                 (JavaScript)
│       └── GenTs             (TypeScript)
└── GenPySwift
    ├── GenPy                 (Python)
    └── GenSwift
```

When adding language behavior, prefer overriding at the highest shared level
that is correct (e.g. something common to C, C++ and D belongs in `GenCCppD`),
and specialize downward only where the languages actually differ.

## The CLI entry point

There is no `fut.fu`. The command-line driver is a thin, hand-maintained host
written directly in each host language: `fut.cpp`, `fut.cs`, `fut.js` and
`Fut.java`. It is intentionally small because everything interesting lives in
the transpiled library (`libfut.*`).

The host is responsible for:

- parsing command-line options (`-o`, `-l`, `-n`, `-D`, `-r`, `-I`, `--help`,
  `--version`);
- reading input files and feeding them to the shared `parseAndResolve` logic,
  which runs the parser over every file and then `FuSema.Process()`;
- constructing `FuSystem` and the `FuProgram` (optionally a second program for
  `-r` referenced files that are resolved but not emitted);
- selecting a generator and emitting output.

Generator selection is centralized in Fusion in `ConsoleHost.fu`
(`FuConsoleHost.Emit`), which maps a language string to the right `Gen*` class.
When the `-o` filename lists several comma-separated extensions
(e.g. `-o hello.c,cpp,cs,d,java,js,py,swift,ts,cl`), the host calls `Emit` once
per extension, so a single invocation produces every requested language.

## Self-hosting and bootstrapping

Because `fut` is written in Fusion, building it would require an existing `fut`
— a chicken-and-egg problem. It is solved by committing generated transpilations
of the compiler to the repository:

- `libfut.cpp` — the compiler library transpiled to C++
- `libfut.cs` — transpiled to C#
- `libfut.js` — transpiled to JavaScript

Combined with the appropriate hand-written host (`fut.cpp`, `fut.cs`,
`Fut.java`, `fut.js`), these let you build a working `fut` from a clean checkout
with no prior `fut` installed. The `Makefile`'s `FUT_HOST` variable
(`cpp`, `cs`, `java`, `node`) selects which host to build; see
[Building fut](building-fut.md).

Once a `fut` binary exists, it is used to regenerate `libfut.*` from the Fusion
sources (`Makefile` target `libfut.cpp libfut.js: $(SOURCE_FU)`). The
`SOURCE_FU` list defines the canonical compile order of the compiler's own
source files. **When you change the compiler, remember to regenerate and commit
the `libfut.*` artifacts** so the next bootstrap picks up your changes.

The `host-diff` targets guard self-hosting correctness: they transpile the test
suite with the C++, C# and Node.js hosts and require byte-identical output,
so the three ports can never silently diverge.

## Where things live

| Path | Purpose |
| --- | --- |
| `Lexer.fu` | Tokenizer and preprocessor (`FuLexer`, `FuToken`) |
| `AST.fu` | AST node definitions, `FuVisitor`, `FuSystem`, `FuProgram` |
| `Parser.fu` | Syntactic parser (`FuParser`) |
| `Sema.fu` | Name resolution, type checking, lifetime checks (`FuSema`) |
| `GenBase.fu` | Shared generator base and `GenHost` |
| `GenTyped.fu` | Base for statically-typed backends |
| `GenC*.fu`, `GenD.fu`, `GenCs.fu`, `GenJava.fu` | C-family, D, C#, Java backends |
| `GenJs.fu`, `GenTs.fu` | JavaScript and TypeScript backends |
| `GenPySwift.fu`, `GenPy.fu`, `GenSwift.fu` | Python and Swift backends |
| `ConsoleHost.fu` | Language-to-generator dispatch (`FuConsoleHost.Emit`) |
| `fut.cpp` / `fut.cs` / `Fut.java` / `fut.js` | Hand-written CLI hosts |
| `libfut.cpp` / `libfut.cs` / `libfut.js` | Committed bootstrap transpilations |
| `test/` | Executable cross-language tests and `error/` diagnostics tests |

## Adding a new target language

At a high level:

1. Create `GenXxx.fu` extending the closest existing generator (`GenTyped` for a
   statically-typed language, `GenBase`/`GenPySwift`/`GenJsNoModule` otherwise).
   Override `GetTargetName`, `WriteProgram` and the `Visit*` / `Write*`
   methods that differ. Use `NotSupported` / `NotYet` for constructs you cannot
   (yet) emit.
2. Add the new source file to `SOURCE_FU` in the `Makefile`.
3. Register the language string → generator mapping in `FuConsoleHost.Emit`
   (`ConsoleHost.fu`), and add the extension to the CLI usage text.
4. Add build/run rules and a `test-xxx` target in the `Makefile`, and a CI job in
   `.github/workflows/test.yml`.
5. Regenerate and commit `libfut.*`.

## Testing

Tests live under `test/`. Each `test/*.fu` is transpiled to every target
language, compiled and executed. It passes if `Test.Run()` returns `true`.
Expected failures are annotated with `//FAIL:` followed by the target language
code(s). Files under `test/error/` assert that specific invalid programs produce
the expected diagnostics (the `//ERROR:` annotations in the sources). See
[Building fut](building-fut.md) for how to run `make test`.
