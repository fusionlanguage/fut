# History of Fusion

The story of the Fusion programming language starts in 2007.
I had a [chiptune player](https://asap.sourceforge.net) coded as my hobby project.
It was written in portable C and worked on several desktop operating systems.
You could surely call it cross-platform. Mind you, this was before the iPhone and Android era.
Users of my player wanted to play chiptunes in a web browser.
Someone announced that he is going to _rewrite my player in Java_
so it can be run as a Java applet (HTML 5 wasn't a thing in 2007).
Let's pause the story for a moment to digress about rewrites.

## "Rewrite it in X"

Rewrites of non-trivial software are a major effort that is often said to not pay off.
There is high risk of introducing new bugs in the process,
even if the intention is to have higher quality code.
When I write this, it is trendy to _rewrite in Rust_. Everyone rewrites everything in Rust.
Personally, I'm a big fan of rewriting old code using modern programming techniques.
I did that several times in my life.
For example, in 1998 I created a [cross-assembler](https://github.com/pfusik/xasm)
in x86 assembly language and in 2005 I reimplemented it in [D](https://dlang.org).
The new code is not tied to Windows and is easier to maintain.
I also routinely reimplement components of commercial software that I work on,
when it feels the right thing to do. This doesn't happen too often, but if your choice
is to spend weeks on debugging some obviously low-quality code, it's better to spend
three days designing and implementing it from scratch.

Having said that, back in 2007 I strongly objected rewriting my chiptune player from C to Java.
This is because the Java player was not going to _replace_ the C player.
C was better for performance, low memory footprint and minimal download size.
So it looked like we are going to have two _copies_ of my chiptune player:
one for desktop operating systems, the other for web browsers.
The code was going to be very similar, but only the programmers would keep it in sync manually.

I felt that this was fundamentally wrong. It would violate the fundamental rule of programming:
_Don't Repeat Yourself (DRY)_. Maintaining full copies of code just to integrate it
with different environments (desktop vs web) is a high price.
I started thinking about how I could port my player to web browsers without having to copy it
as a Java project.

## The power of the C preprocessor

When you see code such as:

```c
if (foo)
    bar();
for (int i = 0; i < n; i++)
    doStuff(i);
```

it can be either C, C++, Java or C#.

Differences come into play when you:

- Define functions (called methods in Java and C#) - the `static` keyword means different things.
- Use pointers in C or C++ (with `*`, `&` and `->` operators).
- Some things are just spelled differently, for example `NULL` vs `null`
  (there was no `nullptr` in 2007).

I realized that I could use the C preprocessor to emit Java code if I define and consistently
use a couple of macros. If you are curious, the first working version was
[this](https://sourceforge.net/p/asap/code/ci/1339af683b60c0da54a5084673bb167e53679750/tree/java/ASAP.ppjava).
This is an input to the C preprocessor. It defines the macros for Java output,
the data structures, includes three other source files and finally defines
the public interface of the Java code.
[Here](https://sourceforge.net/p/asap/code/ci/1339af683b60c0da54a5084673bb167e53679750/tree/apokeysnd.c)
is one of the three C source files it includes.

This approach worked very well. Users were happy and astonished by high performance of the Java code,
which was practically C code masqueraded as Java.
A few days later I made a port of my chiptune player to Java-enabled mobile phones.

In 2008, Maciek Konecki contributed a
[set of macros](https://sourceforge.net/p/asap/code/ci/8c9e4db0b8d200072a66a5758b9bffa2bb5df61b/tree/csharp/ASAP.ppcs)
that made the player valid C# code. C# is similar to Java, so why not.

In 2009, Flash Player was still very popular and I had requests to port my chiptune player to it.
I updated my macros for ActionScript, the Flash Player's programming language.
If you are unfamiliar with ActionScript, its syntax is similar to the much newer TypeScript.
Basically it's JavaScript with explicit types.
That means that all local variable definitions start with `var`, so a macro is needed.
I realized I can also emit JavaScript. The result was the
[anylang.h header file](https://sourceforge.net/p/asap/code/ci/3c2e92f7323ac3154267ab0e9460d7b35a8e7aaf/tree/anylang.h).

It all worked fine, but the
[code](https://sourceforge.net/p/asap/code/ci/3c2e92f7323ac3154267ab0e9460d7b35a8e7aaf/tree/apokeysnd.c)
did not look pretty.

## A programming language is even more powerful

I was happy of being able to produce C, Java, C#, JavaScript and ActionScript from same source code.
The weak point was how this source code looked like.

I realized that if I want to have good-looking source code, I need to design a programming language
instead of just a set of macros. I called this language Ć, which is a letter next to C in the Polish alphabet.
Ć and its transpiler `cito` came to life in 2011.

The initial goal was very modest: just transpile my chiptune player, but with a clean syntax
instead of obscure C macros. I chose C# as the implementation language for `cito`.

In 2011, I released `cito` 0.1.0 with backends to C, C#, Java, JavaScript, ActionScript
and [D](https://dlang.org).
There were two flavors of the C backend: one that emitted C99 and the other that emitted C89,
because at that point Visual Studio did not even support C99.
The D backend was contributed by [Adrian Matoga](https://github.com/epi).

Also in 2011 there was `cito` 0.2.0 with a simple editor called CiPad that was showing
the translations as you typed.

In 2013, I decided to rewrite in Ć my other open-source project with a fancy name: FAIL
(later renamed to [RECOIL](https://recoil.sourceforge.net)).
In the process, the Ć language received class inheritance and dynamic allocation,
in version 0.3.0.
In 0.4.0 it got a Perl backend.

## Second-system syndrome

Ć was good enough for my projects, but generally speaking it was quite a limited language.
For instance, there were no floating-point numbers, just two integer types (`int` and `byte`)
and string storage had a fixed capacity which directly translated into a C array of characters.
I realized that in order for the language to be applicable to a wider variety of projects,
it must be more expressive.
I decided that "version 1.0" will be designed and written from scratch, which I started in 2014.

The result was that "version 1" was largely only in my head, because "version 0" worked very well
for my own purposes. Also, I expected corporations to create a language similar to what I envisioned.
While I was coding a lot _in_ Ć, I wasn't really _developing_ Ć until 2019.

## Version 1.0

In 2019, I decided to materialize my ideas on version 1.0. `cito` got a C++ backend, collections,
interpolated strings and a testsuite run in [Travis CI](https://www.travis-ci.com).
I updated my projects to version 1.0.

In 2020, I added Python, Swift and OpenCL backends.
The latter meant that you could run Ć code on your GPU!
[Andy Edwards](https://github.com/jedwards1211) contributed a TypeScript backend.

There were no binary releases of version 1.
I expected users to build `cito` themselves. It was a developer's tool after all!

## Version 2.0

Starting with 2.0.0, new versions were released as a .NET tool, so if you had a .NET SDK,
you could install `cito` with one command.
I also added [syntax highlighting](editors.md) to a couple of editors.

I started [rewriting Ć in Ć](https://github.com/fusionlanguage/fut/issues/48), part by part.
First the lexer, then the AST, parser, semantic pass and finally the backends.
The lexer, the AST and the parser were incorporated in the Visual Studio Code extension
to report syntax errors.

## Version 3.0

Once the rewrite was complete, I renamed the language to Fusion and its transpiler to `fut`.

`fut` 3.0.0 is self-hosted, transpiling itself to C++, C# and JavaScript. It no longer requires .NET.
3.0.2 additionally transpiles itself to Java.
