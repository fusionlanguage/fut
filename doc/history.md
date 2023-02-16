# History of Ć

The story of the Ć programming language starts in 2007.
I had a chiptune player coded as my hobby project.
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

Having said that, back in 2007 I strongly objected rewriting my player from C to Java.
This is because the Java player was not going to _replace_ the C player.
C was better for performance, low memory footprint and no huge downloads.
So it looked like we are going to have two _copies_ of my chiptune player:
one for desktop operating systems, the other for web browsers.
The code was going to be very similar, but only the programmers could keep it in sync manually.

I felt this was fundamentally wrong. It would violate the fundamental rule of programming:
_Don't Repeat Yourself (DRY)_. Maintaining copies of code just to integrate
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

it could be either C, C++, Java or C#.

Differences come into play when you:

- Define functions (called methods in Java and C#) -- `static` means different things.
- Use pointers in C or C++ (these need `*`, `&` and `->`).
- Some things are just spelled differently (for example `NULL` vs `null`,
  there was no `nullptr` in 2007).

I realized that I could use the C preprocessor to emit Java code if I define and consistently
use a couple of macros. If you are curious, the first working version was
[this](https://sourceforge.net/p/asap/code/ci/1339af683b60c0da54a5084673bb167e53679750/tree/java/ASAP.ppjava).
This is an input to the C preprocessor. It defines the macros for Java output,
the classes that the code works on, includes three other source files and finally defines
the public interface of the Java code.
[Here](https://sourceforge.net/p/asap/code/ci/1339af683b60c0da54a5084673bb167e53679750/tree/apokeysnd.c)
is one of the included C source files.

The code worked very well. Users were happy and astonished by high performance of the Java code,
which was practically C code masqueraded as Java.
A few days later I made a port of my chiptune player to Java-enabled mobile phones.

In 2008, Maciek Konecki contributed a
[set of macros](https://sourceforge.net/p/asap/code/ci/8c9e4db0b8d200072a66a5758b9bffa2bb5df61b/tree/csharp/ASAP.ppcs)
that made the player valid C# code. C# is similar to Java, so why not.

In 2009, Flash Player was still very popular and I had requests to port my chiptune player to it.
Flash Player's programming language was called ActionScript, so I had to prepare macros for it.
If you are unfamiliar with ActionScript, its syntax is similar to the much newer TypeScript.
Basically JavaScript with explicit types.
That means that all local variable definitions start with `var`, so a macro is needed.
It was also straightforward to emit JavaScript. The result was the
[anylang.h header file](https://sourceforge.net/p/asap/code/ci/3c2e92f7323ac3154267ab0e9460d7b35a8e7aaf/tree/anylang.h).

It all worked fine, but the
[code](https://sourceforge.net/p/asap/code/ci/3c2e92f7323ac3154267ab0e9460d7b35a8e7aaf/tree/apokeysnd.c)
did not look pretty.

## A programming language is even more powerful

I was happy of being able to produce C, Java, C#, JavaScript and ActionScript from same source code.
However, I wasn't proud how this source code looked like.

I realized that if I want to have good-looking source code, I need to design a programming language
instead of just a set of macros. This is how Ć and its transpiler `cito` came to life in 2011.

The initial goal was very modest: just being able to transpile my chiptune player, but with a clean
source code syntax instead of obscure C macros. I chose C# as the implementation language for `cito`.

_to be continued_
