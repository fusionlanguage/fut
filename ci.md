# The Ć Programming Language

Welcome to the description of yet another programming language.
Unlike most languages that you learned before, Ć does _not_ claim
to be the best universal programming language.
Instead, it solves one specific problem: how to write code that can be
_conveniently_ used from C, C++, Java, C# and JavaScript _at the same time_.
For example, if you invented a new compression format, you can implement
it in Ć and have the automatic translator `cito` generate the source code
in the mentioned languages, so programmers of these languages can use your code.

Ć is a language for programmers with vast experience in several languages.
Ć is intentionally _lacking_ elements of surprise. Ć syntax is akin to C#.
In fact, C# syntax highlighting works quite well for Ć (not perfectly, though).
However, Ć is not a C# clone or a subset of it.
The differences stem from the need to have _completely automated_ translation
to _efficient_ C and JavaScript code.

Ć is object-oriented, because most of the target languages are object-oriented
and you are probably familiar with this paradigm. This can be seen as
an improvement over C, nevertheless the object-oriented C output is rather
straightforward to use for C programmers.

There is no runtime library dependency. The C output is a self-contained pair
of `.c/.h` files containing portable, human-readable C99 code.
Similarly, the outputs in other languages do _not_ rely on anything except
the standard language.

## Source files

Programmers tend to avoid Unicode in filenames.
Therefore Ć source files have the `.ci` filename extension instead of `.ć`.

> In Polish "ci" is pronounced identically to "ć".

Source file contents must be UTF-8 encoded
with an optional (but recommended) BOM.

Most of the time whitespace is insignificant in Ć source code.
Let's continue indentation style flame wars!

There are single-line comments from `//` till the end of line
and `/* multiline comments */`.
Documentation comments are described below.

## Data types

### Boolean type

The boolean type is called `bool` and its literals are `true` and `false`.

Boolean operators are `!` (not), `&&` (and), `||` (or)
and the ternary operator `x ? y : z` (if `x` then `y` else `z`).

### Integers

Most of the time you will use the 32-bit integer type `int`.
For larger numbers, use the 64-bit `long`.
However, JavaScript doesn't support 64-bit integers,
so operations on `long` are subject to precision loss when using JavaScript.

These two types should be sufficient for scalar variables. For arrays,
you want to conserve the storage space and use smaller types where possible.
Not only reduces it memory footprint, but it is cache-friendly
at the same time, which is essential for good performance.
Small integers are implemented in Ć in terms of _ranges_ specifying
the lower and upper bounds, both of which are inclusive.
For example:

```csharp
(0 .. 100)[1000] arrayOfSmallIntegers;
```

is a definition of an array of a thousand integers between zero
and one hundred. `cito` figures out the best data type in the target language
to represent a range. It is programmer's responsibility to assign
only the values that are in the given range.
This is _not_ verified compile-time nor run-time.
Also, you should avoid overflows, because wrapping of values
to the specified range is _not_ guaranteed.

There are aliases to the commonly used ranges:

* `byte` is `0 .. 255`
* `short` is `-32768 .. 32767`
* `ushort` is `0 .. 65535`
* `uint` is `0 .. 2147483647`.

Note that `uint` is _not_ 32-bit unsigned integer, but a 31-bit one.
As such, it doesn't provide an extended range over `int`.
It only serves as a message of "negative number not allowed".
`byte` corresponds to `byte` in Java, even though it is _signed_ in Java.
This is accomplished by `cito` injecting `& 0xff` in every retrieval
of a `byte` value.

Integer literals may be written in decimal or hexadecimal (`0x12ab`) form.
There are no octal nor binary literals in Ć.

Character literals (such as `'x'`) represent the Unicode codepoint
of the character, as an `int` (not `char` because there's no such type in Ć).
You may also use the following escape sequences:

* `'\''` -- apostrophe
* `'\"'` -- double quote
* `'\t'` -- horizontal tab
* `'\r'` -- CR
* `'\n'` -- LF
* `'\a'` -- bell
* `'\b'` -- backspace
* `'\f'` -- form feed
* `'\v'` -- vertical tab
* `'\\'` -- backslash

Operations on integers are conducted with the usual binary operators
`+ - * / % & | ^ << >>`, compound assignments (such as `*=`),
incrementations and decrementations (`x++ ++x x-- --x`), negation (`-`),
bitwise complement (`~`) and comparisons (`== != < <= > >=`).

### Floating-point numbers

There are two floating-point types: `float` and `double`.
Use the aforementioned operators, except for bitwise operations.
There's a built-in `Math` class with the following static methods:

* `Math.Acos(double a)`
* `Math.Asin(double a)`
* `Math.Atan(double a)`
* `Math.Atan2(double y, double x)`
* `Math.Cbrt(double a)`
* `Math.Ceiling(double a)`
* `Math.Cos(double a)`
* `Math.Cosh(double a)`
* `Math.Exp(double a)`
* `Math.Floor(double a)`
* `Math.FusedMultiplyAdd(double x, double y, double z)`
* `Math.Log(double a)`
* `Math.Log10(double a)`
* `Math.Log2(double a)`
* `Math.Pow(double x, double y)`
* `Math.Sin(double a)`
* `Math.Sinh(double a)`
* `Math.Sqrt(double a)`
* `Math.Tan(double a)`
* `Math.Tanh(double a)`
* `Math.Truncate(double a)`

### Enumerations

Enumerations have user-defined values. Example:

```csharp
enum DayOfWeek
{
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday
}
```

There are no conversions between enumerated and integer types.

`enum` may be preceded with the keyword `public` to extend the visibility
of the type outside Ć, that is, make the enumerated type part
of the public interface of the library implemented in Ć.

When referencing a value of an enumerated type, you need to include
the type name, for example `DayOfWeek.Friday`.
Note to Java programmers: this includes the `case` clauses.

### Strings

For text processing please choose Perl and not Ć. Really.

In Ć there are two string data types:

* String storage, written as `string()`.
* String reference, written simply as `string`.

This distinction enables straightforward translation to C and C++,
which have no garbage collector.
In C++, string storage is represented by `std::string`,
while string reference is `std::string_view`.
In C, string storage is a `char *` pointing to a `malloc`'ed string
and string reference is `const char *`.
In the other languages, `String` is used for both string storage and reference.

A string reference can:

* Reference a string storage. The reference gets invalid
  once the string storage is modified or destroyed.
* Reference a string literal (e.g. `"foo"`). Such references are always valid.
* Have the value `null`. This is a special value for reference types,
  meaning "nothing is referenced".

String literals are written in double quotes: `"Hello world"`.
You may use `\n` and the other escape sequences as in character literals.

Possible string operations are:

* Assignment with `=`. For string storage in C and C++,
  a copy is made and the previous value gets invalid.
* Concatenation with `+` and `+=`.
* Comparison with `==` and `!=`. `cito` translates this to `strcmp` in C
  and `str1.equals(str2)` in Java. The two comparison operators can also
  be used to check for `null` value -- use `str == null` or `str != null`.
  It is not legal to compare two string references if any of them is `null`.
* Length retrieval with `str.Length`.
* Character retrieval with `str[index]`.
* `str1.StartsWith(str2)`, `str1.EndsWith(str2)` and `str1.Contains(str2)`
  return a boolean value.
* `str1.IndexOf(str2)`, `str1.LastIndexOf(str2)` return an index
  to the beginning of `str2` within `str1`, or -1 if not found.
* `str1.Substring(offset, length)` evaluates to the selected part of the string.
* `str1.Substring(offset)` returns the part of the string
  from the specified position until the end of the string.
* `Encoding.UTF8.GetString(bytes, offset, length)`
  creates a string from the specified part of a `byte` array.

Different target languages have different character encodings.
`str.Length` and index/offset are defined in terms of _code units_,
which might be 8-bit or 16-bit.
An ASCII character is encoded as a single code unit, but Unicode _code points_
can be stored in several code units, typically encoded
as [UTF-8](https://en.wikipedia.org/wiki/UTF-8)
or [UTF-16](https://en.wikipedia.org/wiki/UTF-16).
However, Ć doesn't enforce any encoding.

Ć also supports string _interpolated strings_.
An interpolated string starts with `$"` and contains expressions in braces.
The expressions are replaced with their string values.
Example:

```csharp
string name = "John";
int born = 1979;
int now = 2019
string() s = $"{name} is {now - born} years old";
```

The expressions might be formatted by specifying _field width_ (after a comma)
and/or _format string_ (after a colon).

```csharp
string name = "John";
int i = 15;
double d = 1.5
string() s = $"{name, 5} {i:X2} {d,5:F2}"; // " John 0F  1.50"
```

If field width is specified, the formatted expression is padded with spaces.
The expression is right-aligned if width is positive
and left-aligned if width is negative.

The following format strings are supported:

* `D<n>` or `d<n>` format an integer in decimal,
  padding with leading zeros to `n` digits.
* `X` or `x` format an integer in hexadecimal.
  `X` uses uppercase digits, `x` uses lowercase digits.
  The optional number specifies padding with leading zeros.
* `F<n>` or `f<n>` format a `float` or `double` with `n` digits
  after the decimal point.
* `E<n>` or `e<n>` format a `float` or `double` in exponential notation
  with `n` digits after the decimal point. The exponent symbol `E` or `e`
  matches the format string.

### Arrays

Arrays are fixed-size collections, where every element has the same type
and can be accessed in O(1) time.
Ć array types are:

* Array storage, written as `T[n]` where `T` is the element type
  and `n` is the compile-time constant length.
* Dynamic array reference, written as `T[]#`.
* Read-only array reference, written as `T[]`.
* Read-write array reference, written as `T[]!`.

Dynamic array references are allocated on the heap using `new`:

```csharp
int[]# dynamicArray = new int[1000];
```

The length specified in the square brackets can be a variable.

Dynamic array references can be assigned to other dynamic array references.
The dynamic array is valid as long as there's at least one dynamic
array reference to it.
In C++ dynamic array references are implemented as `std::shared_ptr`.

Read-only and read-write array reference can point to array storage
and dynamic arrays.
It is not possible to modify the array via a read-only reference:

```csharp
int[10] arrayStorage;
int[] readOnlyArrayRef = arrayStorage;
readOnlyArrayRef[0] = 42; // cito error
```

Array size is only available for array storage, via `array.Length`.
The initial content of arrays (either storage or dynamic) is undefined,
unless explicitly specified:

```csharp
int[100] oneHundredZeros = 0;
```

You can fill array storage anytime:

```csharp
arrayStorage.Fill(0);
```

All reference types (including the dynamic reference) can have the value `null`.
References might be compared -- this compares the identity of the arrays,
not their contents:

```csharp
int[4] x = 0;
int[] rx = x;
int[4] y = 0;
int[] ry = y;
bool referencesEqual = rx == ry; // false, referencing different arrays
```

Multi-dimensional arrays are supported:
```csharp
byte[2][3] multiDimArray;
multiDimArray[1][2] = 1;
```

You may declare constant `byte` arrays, initialized with the contents of a file
provided to `cito`. For example, `resource<byte[]>("foo.bar")`
is an array consisting of bytes of the file `foo.bar` read while running `cito`.

### Classes

Classes are user-defined compound types.

```csharp
class Animal
{
    // class contents (members) goes here
}
```

Note to C++ programmers: do not place a semicolon after the closing brace.

Ć supports single inheritance. Put _base class_ name after a colon:

```csharp
class Cat : Animal
{
    ...
}
```

As with `enum`, placing `public` before `class` makes it part
of the library interface.

Classes can be:

* `static`, meaning they only contain `static` methods and constants.
* `abstract`, meaning they cannot be _instantiated_ and only serve
  as base classes.
* `sealed`, meaning they cannot be derived from.
  This is the C# term for Java's `final`.

Class members can be:

* _fields_ -- the data contained in every _object_
* _methods_ -- class-specific code
* _constructor_ -- code to be executed on _object_ creation
* _constants_ -- named compile-time values

Every member has _visibility_:

* _private_ is the default visibility, there is _no_ `private` keyword.
  This means the member is only visible to other members of the class.
* `protected` means the member is visible to this class and its subclasses.
* `internal` means the member is visible to the Ć code compiled with it.
* `public` means the visibility is unrestricted.

#### Fields

Fields are defined by specifying the visibility, type, name
and optionally the initial value:

```csharp
class Car
{
    int Year;
    internal string() Model;
    int Seats = 5;
}
```

Fields _cannot be_ public. Instead, define _getter/setter_ methods:

```csharp
public class Image
{
    int Width;
    public int GetWidth() { return Width; }
    public int SetWidth(int value) { With = value; }

    int Height;
    public int GetHeight() => Height; // syntax sugar
}
```

Fields _cannot be_ static. Shared state poses problems with lifetime
and multithreading.

#### Constants

Constants must be assigned a compile-time value, but it can reference other
constants. Constants are implicitly `static`.

```csharp
public class RECOIL
{
    public const int VersionMajor = 5;
    public const int VersionMinor = 0;
    public const int VersionMicro = 0;
    public const string Version = VersionMajor + "." + VersionMinor + "." + VersionMicro;
}
```

Constant arrays are also allowed:

```csharp
public class Foo
{
    public const string[] Metasyntactic = { "foo", "bar", "baz", "quux" }; // implicit length
    public const byte[4] SmallPrimes = { 2, 3, 5, 7 }; // length must match
}
```

#### Constructor

Fields can be usually initialized by specifying initial values.
In case initialization must be performed by code, define a constructor
using the class name followed by an empty pair of parentheses:

```csharp
public class Foo
{
    public Foo()
    {
        // initialization here
    }
}
```

A public constructor is also required to enable object creation outside Ć.

Constructors in Ć _do not_ take arguments. This promotes reuse of existing
objects instead of creating a bunch of single-use objects.
If you need to initialize the object with some outside data,
create a method such as `Init`.

#### Methods

Methods are defined by specifying in order:

* visibility (`public`, `internal`, `protected` or the default private)
* _call type_ (`static`, `abstract`, `virtual`, `override`, `sealed`
  or the default normal)
* return type (can be `void` if no return value)
* method name
* an exclamation mark (`!`) if the method is a _mutator_ (see below)
* comma-separated parameter list in parentheses
* _method body_, unless the method is `abstract`

Non-static methods have an implicit reference to the object they are working on,
called `this`.

Abstract methods have no body. They must be overridden in a derived class.
The `override` specifier is mandatory (unlike in C++ and Java).
A `sealed` method (`final` in Java terms) is implicitly `override`.

Method name must identify the method within the class.
Ć does _not_ support overloading.
It does support default argument values, though:

```csharp
int ParseInt(string s, int base = 10) {
    ...
}
```

A method body is usually a _block_ (list of instructions in curly braces).
If the method body is very short and consists entirely of the `return`
statements, there's an alternative syntax:

```csharp
public int GetWidth() => Width;
```

A method is called _pure_ if its only effect is the return value
(that is, no state is modified).
`cito` can evaluate such method compile-time and they can be used
in expressions that must evaluate to compile-time constants:

```csharp
static int Square(int x) => x * x;
int[Square(10)] arrayStorage; // OK, 100 elements

static int FourCC(string s) => s[0] | s[1] << 8 | s[2] << 16 | s[3] << 24;
switch (signature) {
case FourCC("WAVE"): // OK, compile-time constant
    ...
}
```

#### Objects

Once a class is defined, you can _instantiate_ it,
that is, create _objects_ using the class as a template.

Similarly to arrays, there are four types associated with every class `C`:

* `C()` is object storage.
* `C#` is dynamic object reference.
* `C` is read-only object reference.
* `C!` is read-write object reference.

The simplest way to instantiate objects is with object storage:

```csharp
Cat() alik;
```

This translates as follows:

```csharp
Cat alik = new Cat(); // C#
final Cat alik = new Cat(); // Java
var alik = new Cat(); // JavaScript
Cat alik; // C++
Cat alik; // C, potentially followed by construction code
```

Note that in C and C++ the objects are created on the _stack_
for maximum performance.

You can have array storage of object storage:

```csharp
Wheel()[4] wheels;
```

which creates an array of 4 objects of class `Wheel`.

A more powerful (but costly) way is to allocate an object dynamically:

```csharp
Animal# animal;
if (nerd)
    animal = new Cat();
else
    animal = new Dog();
```

This translates to the following C++:

```cpp
std::shared_ptr<Animal> animal;
if (nerd)
    animal = std::make_shared<Cat>();
else
    animal = std::make_shared<Dog>();
```

Read-only references cannot be used to modify the object,
that is, modify its fields or call a mutator method.

```csharp
Circle() circle;
Shape readOnlyReference = circle;
readOnlyReference.Color = 0x00ff00; // cito error
readOnlyReference.Move(100, 100); // cito error
```

A mutator method of class `C` is a method where `this` is of type `C!`.
In a non-mutator method, `this` is of type `C` (a read-only reference).
Static methods do not have `this`, so the mutator/non-mutator
classification doesn't apply.

## Statements

Statements are used in methods and constructors.
Their syntax in Ć is nearly identical to the languages you already know.

In Ć there's no empty statement consisting of the sole semicolon.
You may use an empty block instead: `{ }`.

### Blocks

A block is a sequence of statements wrapped in curly braces.

### Variable definitions

Variables must be defined separately:

```csharp
int x;
int y;
int a, b, c; // syntax error
```

Variable definition may include an initial value:

```csharp
int x = 5;
int[4] array = 0; // initialized with zeros
```

Variables have the scope of the enclosing _block_.
It is an error to use a reference pointing
at an array or object outside of its scope.

### Local constants

Constants can be declared not only at the level of classes
(as described above), but also at the level of statements.
Such a definition has a scope of the containing block.
For this reason local constants do not specify visibility.

### Assignments

Use `=` for assigning variables and fields. Use _op_= for compound assignments.

```csharp
x = 4;
x += 5; // increment by 5
```

Assignments are statements, but not expressions.

```csharp
int c;
while ((c = ReadChar()) != -1) { // syntax error
    ...
}
```

The above code should be refactored to:

```csharp
for (;;) {
    int c = ReadChar();
    if (c == -1)
        break;
    ...
}
```

Chained assignments are supported:

```csharp
x = y = 42;
```

### Expressions

Expressions with a _side effect_ can be used as statements:

```csharp
DoFoo(4, 2); // method call
i++;
i + 2; // ERROR: useless computation
```

There is no comma operator in Ć.

### Returning method result

A method can end its execution with a `return` statement.
`return` must be followed with the returned value,
except for `void` methods of course.

### Conditional statement

To execute code conditionally, use `if` with an optional `else` clause:

```csharp
if (x == 7)
    DoFoo();
else
    DoBar();
```

### Loops

There are three kinds of loops:

* `while` -- checking the condition at the beginning of each run
* `do/while` -- checking the condition after the first run
* `for` -- which contains an initial statement
  and a statement executed after each run.

Inside loops you may use:

* `break` to leave the loop (the inner one, as there are no loop labels).
* `continue` to skip to the next run.

### Switch statement

The `switch` statement accepts an expression and transfers control
to the corresponding `case` label.

`case` clauses must be correctly terminated, consider:

```csharp
switch (x) {
case 1:
    DoFoo();
    // ERROR: something's missing here
case 2:
    DoBar();
    break;
}
```

Correct termination means a statement that doesn't fall
to the next statement: `break`, `continue`, `return`, `throw`
or C#-style `goto case` / `goto default`, which jumps to the next case.

```csharp
switch (x) {
case 1:
    DoFoo();
    goto case 2; // now it's clear what the programmer meant
case 2:
    DoBar();
    break;
}
```

The `default` clause, if present, must be specified last.

### Exceptions

Ć can throw exceptions, but cannot handle them at the moment.
The idea is that the exceptions will be handled by the code
using the library written in Ć.

An exception can be thrown with the `throw` statement with a string argument.
You cannot specify the class of the exception, it's hardcoded in `cito`
(for example `java.lang.Exception`).

Translation of exceptions to C needs an explanation.
The string argument is lost in the translation and the `throw` statement
is replaced with `return` with a magic value representing an error:

* `-1` in a method returning an integer.
* `NULL` in a method returning a pointer.
* `false` in a `void` method. The method will be translated to `bool`
  and `true` will be returned if the method succeeds.

### Standard output

To print on the standard output, use `Console.Write` and `Console.WriteLine`
with a string or number. To print several elements, use interpolated strings.

```csharp
Console.Write("The answer is ");
Console.WriteLine(42);
Console.WriteLine($"Yes, {40 + 2}");
```

Use `Console.Error.Write` and `Console.Error.WriteLine` to target
the standard error stream.

### Native blocks

Code which cannot be expressed in Ć can be written in the target language
using the following syntax:

```java
native {
    printf("Hello, world!\n");
}
```

Generally, native blocks should be used inside `#if` (see below).

Native blocks are allowed as statements in method bodies and at the top level
(for `import` / `using` declarations).

## Conditional compilation

Conditional compilation in Ć is modeled after C#.
Conditional compilation symbols can only be given on the `cito` command line.
Conditional compilation symbols have no assigned value,
they are only present or not.

Example:

```csharp
#if MY_SYMBOL
    MyOptionalFunction();
#endif
```

A more complicated one:

```csharp
#if WINDOWS
    DeleteFile(filename);
#elif LINUX || UNIX
    unlink(filename);
#else
    UNKNOWN OPERATING SYSTEM!
#endif
```

The operators allowed in `#if` and `#elif` are `!`, `&&` and `||`.
You may reference `true`, which is a symbol that is always defined.
`false` should be never defined.

## Documentation comments

Documentation comments can describe classes, enumerated types, constants,
methods and their parameters.
They start with three slashes followed by a space and always immediately
precede the documented thing, including the method parameters:

```csharp
/// Returns the extension of the original module format.
/// For native modules it simply returns their extension.
/// For the SAP format it attempts to detect the original module format.
public string GetOriginalModuleExt(
    /// Contents of the file.
    byte[] module,
    /// Length of the file.
    int moduleLen)
{
    ...
}
```

Documentation comments should be full sentences. The first sentence,
terminated with a period at the end of line, becomes the summary.
Next sentences (if any) give more details.

There are limited formatting options: fixed-width font, paragraphs and bullets.

A `fixed-width font` text (typically code) is delimited with backquotes:

```csharp
/// Returns `true` for NTSC song and `false` for PAL song.
```

In long comments, paragraphs are introduced
with blank documentation comment lines:

```csharp
/// First paragraph.
///
/// Second paragraph.
```

Bullets are introduced with an asterisk followed by space:

```csharp
/// Sets music creation date.
/// Some of the possible formats are:
/// * YYYY
/// * MM/YYYY
/// * DD/MM/YYYY
/// * YYYY-YYYY
///
/// An empty string means the date is unknown.
public void SetDate(string value)
{
    CheckValidText(value);
    Date = value;
}
```

## Naming conventions

It is advised to use the following naming conventions in Ć code:

* Local variables and parameters start with a lowercase letter,
  capitalize the first letter of the following words -- that is, `camelCase`.
* All other identifiers should start with an uppercase letter
  -- that is, `PascalCase`.

Generators will translate the above convention
to the one native to the output language,
for example constants written as `UPPERCASE_WITH_UNDERSCORES`.
