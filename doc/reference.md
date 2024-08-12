# The Fusion Programming Language

Welcome to the description of yet another programming language!
Unlike most languages that you may have learned before, Fusion does _not_ claim
to be the best universal programming language.
Instead, it solves one specific problem: how to create code that can be
_simultaneously_ used with C, C++, C#, D, Java, JavaScript, Python, Swift,
TypeScript and OpenCL.
For example, if you invented a new compression format, you could implement
it in Fusion and have the automatic translator `fut` generate the source code
in the aforementioned languages, allowing programmers of those languages
to use your code.

Fusion is a language for programmers with vast experience in several other
languages. The syntax is akin to C#.
In fact, C# syntax highlighting works quite well for Fusion.
However, Fusion is not a C# clone or a subset of it.
The differences stem from the need to have _completely automated_ translation
to _efficient_ C and JavaScript code in particular.

Fusion is object-oriented because most of the target languages
are object-oriented, and you are probably familiar with this paradigm.
This can be seen as an improvement over C. Nevertheless, C programmers will
find using the object-oriented C output fairly straightforward.

Runtime library dependencies are minimal. The C output is often a self-contained
pair of `.c`/`.h` files containing portable, human-readable C99 code.
Similarly, the outputs in other languages do _not_ rely on anything except
the standard language. There are two exceptions:

1. If the Fusion code uses regular expressions, `List`, `Queue`, `Stack`,
   `HashSet`, `SortedSet`, `Dictionary` or `SortedDictionary`, the C output
   relies on [GLib](https://wiki.gnome.org/Projects/GLib) implementations
   of these.
2. `Math.FusedMultiplyAdd` is implemented in Python
   with [pyfma](https://pypi.org/project/pyfma/).

Memory management is native to the target language.
A garbage collector will be used if available in the target language.
Otherwise (in C and C++), objects and arrays are allocated on the stack
for maximum performance or on the heap for extra flexibility.
Heap allocations use C++ smart pointers.

## Source files

Fusion source files have the `.fu` filename extension.

Source file contents must be UTF-8 encoded with an optional BOM.

Most of the time, whitespace is insignificant in Fusion source code.
Let's continue indentation style flame wars!

There are single-line comments from `//` till the end of line
and `/* multiline comments */`.
Documentation comments are described below.

## Data types

### Boolean type

The boolean type is called `bool` and its literals are `true` and `false`.

The boolean operators are `!` (not), `&&` (and), `||` (or)
and the ternary operator `x ? y : z` (if `x` then `y` else `z`).

### Integers

Most of the time, you will use the 32-bit integer type `int`.
For larger numbers, use the 64-bit `long`.

`int` and `long` are often sufficient for scalar variables. For arrays,
you want to conserve storage space and use smaller types where possible.
Not only reduces it memory footprint, but it's also cache-friendly,
which is essential for good performance.
Small integers are implemented in Fusion in terms of _ranges_ specifying
the lower and upper bounds, both of which are inclusive.
For example:

```csharp
0 .. 100 [1000] arrayOfSmallIntegers;
```

is a definition of an array of a thousand integers between zero
and one hundred. `fut` figures out the best data type in the target language
to represent a range. It is the programmer's responsibility to assign
only the values that are in the given range.
This is _not_ verified during compilation or runtime.
Also, you should avoid overflows because it is _not_ guaranteed that
values will be wrapped to the specified range.

There are aliases for commonly used ranges:

* `byte` is `0 .. 255`
* `short` is `-32768 .. 32767`
* `ushort` is `0 .. 65535`
* `uint` is `0 .. 2147483647`.

Note that a `uint` is _not_ a 32-bit unsigned integer, but a 31-bit one.
As such, it doesn't provide extended range over an `int`.
It serves as documentation that a negative number is not allowed.
`byte` corresponds to `byte` in Java, even though the Java type is _signed_.
To accomplish this, `fut` injects `& 0xff` in every retrieval
of a `byte` value.

Integer literals may be written as:

* decimal (`1234`)
* hexadecimal (`0x12ab`)
* binary (`0b101`)
* octal (`0o777`)

Character literals (such as `'c'`) represent the Unicode codepoint of the
character as an `int` (not `char` because there's no such type in Fusion).
You may also use the following escape sequences:

* `'\''` - apostrophe
* `'\"'` - double quote
* `'\t'` - horizontal tab
* `'\r'` - CR
* `'\n'` - LF
* `'\\'` - backslash

Operations on integers are conducted with the usual binary operators
`+ - * / % & | ^ << >>`, compound assignments (such as `*=`),
incrementations and decrementations (`x++ ++x x-- --x`), negation (`-`),
bitwise complement (`~`) and comparisons (`== != < <= > >=`).

Incrementations and decrementations cannot be _conditional_ in an expression.
That is, they cannot be used on the right side of `&&`, `||`
or the ternary operator. This is because such expressions wouldn't easily
translate to Python or Swift, where incrementations and decrementations
are implemented as `+=` and `-=` statements.

`MinValue` and `MaxValue` constants are defined for all integer types,
for example `int.MaxValue`.

### Floating-point numbers

There are two floating-point types: `float` and `double`.
Use the aforementioned operators, except for bitwise operations.

There's a built-in `Math` class with the following constants:

* `Math.PI`
* `Math.E`
* `Math.PositiveInfinity`
* `Math.NegativeInfinity`
* `Math.NaN`

and static methods:

* `Math.Abs(int a)`
* `Math.Abs(long a)`
* `Math.Abs(float a)`
* `Math.Abs(double a)`
* `Math.Acos(float a)`
* `Math.Acos(double a)`
* `Math.Asin(float a)`
* `Math.Asin(double a)`
* `Math.Atan(float a)`
* `Math.Atan(double a)`
* `Math.Atan2(float y, float x)`
* `Math.Atan2(double y, double x)`
* `Math.Cbrt(float a)`
* `Math.Cbrt(double a)`
* `Math.Ceiling(double a)`
* `Math.Clamp(int value, int min, int max)`
* `Math.Clamp(long value, long min, long max)`
* `Math.Clamp(float value, float min, float max)`
* `Math.Clamp(double value, double min, double max)`
* `Math.Cos(float a)`
* `Math.Cos(double a)`
* `Math.Cosh(float a)`
* `Math.Cosh(double a)`
* `Math.Exp(float a)`
* `Math.Exp(double a)`
* `Math.Floor(double a)`
* `Math.FusedMultiplyAdd(float x, float y, float z)`
* `Math.FusedMultiplyAdd(double x, double y, double z)`
* `Math.IsFinite(double a)`
* `Math.IsInfinity(double a)`
* `Math.IsNaN(double a)`
* `Math.Log(float a)`
* `Math.Log(double a)`
* `Math.Log10(float a)`
* `Math.Log10(double a)`
* `Math.Log2(float a)`
* `Math.Log2(double a)`
* `Math.Max(int a, int b)`
* `Math.Max(long a, long b)`
* `Math.Max(float a, float b)`
* `Math.Max(double a, double b)`
* `Math.Min(int a, int b)`
* `Math.Min(long a, long b)`
* `Math.Min(float a, float b)`
* `Math.Min(double a, double b)`
* `Math.Pow(float x, float y)`
* `Math.Pow(double x, double y)`
* `Math.Round(double a)`
* `Math.Sin(float a)`
* `Math.Sin(double a)`
* `Math.Sinh(float a)`
* `Math.Sinh(double a)`
* `Math.Sqrt(float a)`
* `Math.Sqrt(double a)`
* `Math.Tan(float a)`
* `Math.Tan(double a)`
* `Math.Tanh(float a)`
* `Math.Truncate(double a)`

Conversion from floating-point type to integer must be explicit,
by using one of the above methods that return an integer:

```csharp
double d = 10.5;
int i = Math.Truncate(d); // translated to: (int) d
int j = Math.Ceiling(d);
int k = Math.Floor(d);
int n = Math.Round(d);
```

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

`enum` may be preceded with the keyword `public` to extend the visibility
of the type outside Fusion, that is, make the enumerated type part
of the public interface of the library implemented in Fusion.

When referencing a value of an enumerated type, you need to include
the type name, for example `DayOfWeek.Friday`.
Note to Java programmers: this includes the `case` clauses.

For values which are not mutually exclusive, use `enum*`
and assign integer values:

```csharp
enum* Seasons
{
    Spring = 1 << 0,
    Summer = 1 << 1,
    Fall = 1 << 2,
    Winter = 1 << 3,
    Autumn = Fall,
    Warm = Spring | Summer,
    Cold = Fall | Winter
}
```

`enum*` values can be combined with the `| & ^ ~` operators.
The `HasFlag` method can be used to check if the `enum*`
has all the specified flags:

```csharp
Seasons s = Seasons.Warm;
bool ok = s.HasFlag(Seasons.Summer);
```

Convert an integer to an enumeration with the `FromInt` method:

```csharp
enum Magic
{
    One = 1,
    Answer = 42
}

assert Magic.FromInt(42) == Magic.Answer;
```

### Strings

In Fusion there are three string data types:

* String storage, written as `string()`.
* String reference, written simply as `string`.
* Nullable string reference, written as `string?`.

This distinction enables straightforward translation to C and C++,
which have no garbage collector.
In C++, string storage is represented by `std::string`,
while string reference is C++17 `std::string_view`.
In C, string storage is a `char *` pointing to a `malloc`'ed string,
and string reference is `const char *`.
In the other languages, `String` is used for both string storage and reference.

A string reference can do the following:

* Reference a string storage. The reference becomes invalid
  once the string storage is modified or destroyed.
* Reference a string literal (e.g. `"foo"`). Such references are always valid.
* Nullable references may have the value `null`.

String literals are written in double quotes: `"Hello world"`.
You may use `\n` and the other escape sequences allowed in character literals.

Possible string operations include the following:

* Assignment with `=`. For string storage in C and C++, a copy is made.
* Concatenation with `+` and `+=`.
* Comparison with `==` and `!=`. `fut` translates this to `strcmp` in C
  and `str1.equals(str2)` in Java.
  The two comparison operators can also be used to check nullable references
  for the `null` value - use `str == null` or `str != null`.
  It is not legal to compare two string references if any of them is `null`.
* Length retrieval with `str.Length`.
* _Code unit_ retrieval with `str[index]`.
* `str1.StartsWith(str2)`, `str1.EndsWith(str2)` and `str1.Contains(str2)`
  return a boolean value.
* `str1.IndexOf(str2)`, `str1.LastIndexOf(str2)` return an index
  to the beginning of `str2` within `str1`, or -1 if not found.
* `str.Substring(offset, length)` evaluates to the selected part of the string.
* `str.Substring(offset)` returns the part of the string
  from the specified position until the end of the string.
* `str.Replace(old, new)` returns the contents of `str` with all occurrences
  of `old` substituted with `new`.
* `str.ToLower()` and `str.ToUpper()` return a copy of the string
  with characters changed to respectively lowercase and uppercase
* `Convert.ToBase64String(byteArray, offset, length)` encodes the specified
  array part as a Base64 string.
* `Encoding.UTF8.GetByteCount(str)` calculates the number of bytes needed
  for UTF-8 encoding of the string.
* `Encoding.UTF8.GetBytes(str, byteArray, byteArrayIndex)` writes UTF-8
  to the specified `byte` array, starting from the given index.
* `Encoding.UTF8.GetString(byteArray, offset, length)`
  creates a string from the specified part of a `byte` array.

Different target languages have different character encodings.
`str.Length` and index/offset are defined in terms of _code units_,
which might be 8-bit or 16-bit.
An ASCII character is always encoded as a single code unit, but Unicode
_code points_ can be stored in several code units, typically encoded
as [UTF-8](https://en.wikipedia.org/wiki/UTF-8)
or [UTF-16](https://en.wikipedia.org/wiki/UTF-16).
However, Fusion doesn't enforce any encoding.

#### Interpolated strings

An interpolated string starts with `$"` and contains expressions in braces.
The expressions are replaced with their textual representations.
Example:

```csharp
string name = "John";
int born = 1979;
int now = 2019;
string() s = $"{name} is {now - born} years old";
```

The expressions might be formatted by specifying _field width_ (after a comma)
and/or _format string_ (after a colon).

```csharp
string name = "John";
int i = 15;
double d = 1.5;
string() s = $"{name, 5} {i:X2} {d,5:F2}"; // " John 0F  1.50"
```

If field width is specified, the formatted expression is padded with spaces.
The expression is right-aligned if the width is positive
and left-aligned if the width is negative.

The following format strings are supported:

* `D<n>` or `d<n>` formats an integer in decimal,
  padding with leading zeros to `n` digits.
* `X` or `x` formats an integer in hexadecimal.
  `X` uses uppercase digits, `x` uses lowercase digits.
  The optional number specifies padding with leading zeros.
* `F<n>` or `f<n>` formats a `float` or a `double` with `n` digits
  after the decimal point.
* `E<n>` or `e<n>` formats a `float` or a `double` in exponential notation
  with `n` digits after the decimal point. The exponent symbol `E` or `e`
  matches the format string.

#### Regular expressions

Strings can be tested against
[regular expressions](https://en.wikipedia.org/wiki/Regular_expression):

```csharp
string s = "456";
bool isInteger = Regex.IsMatch(s, "^\\d+$");
```

`Regex.IsMatch` accepts an additional argument of type `RegexOptions`:

* `RegexOptions.IgnoreCase` matches both uppercase and lowercase letters.
* `RegexOptions.Multiline` changes the behavior of `^` and `$` to match at
  the beginning and the end of a line instead of just the beginning and the end
  of the string. Lines are separated by the newline (`'\n'`) characters.
* `RegexOptions.Singleline` changes the behavior of the dot (`.`)
  so that it matches every character.
  Normally it doesn't match the newline (`'\n'`).
* The above options can be combined with the "or" operator (`|`).

To retrieve the location and contents of the match, create a `Match()` object
and call its `Find` method:

```csharp
Match() match;
if (match.Find(s, "^.{81,}$", RegexOptions.Multiline)) {
    Console.WriteLine($"Found long line of {match.Length} characters, "
        + $"starting at {match.Start}, ending at {match.End}:\n{match.Value}");
}
```

The properties `Start`, `End`, `Length` and `Value` can be accessed
only if `Find` returns `true`.
The same restriction applies to the `GetCapture(int group)` method that
retrieves the part of the match corresponding to the parentheses in the pattern.

```csharp
Match() match;
if (match.Find(url, "^(\\w+)://") && match.GetCapture(1) == "https") {
    Console.WriteLine("Secure connection");
}
```

Use `Regex.Escape` to escape a string so that its characters
won't be interpreted as wildcards:

```csharp
string youtube = Regex.Escape("www.youtube.com"); // dots matched literally
bool isYoutubeMovie = Regex.IsMatch(url, youtube + "/watch\\?v=\\w+");
```

For better performance of repeated matches of the same regular expression,
create a `Regex` object and use it:

```csharp
Regex# re = Regex.Compile("\\d+"); // specify RegexOptions if needed
bool containsInteger = re.IsMatch(s);
Match() match;
if (match.Find(s, re)) {
   Console.WriteLine($"Found integer: {match.Value}");
}
```

### Arrays

Arrays are fixed-size collections, where every element has the same type
and can be accessed in O(1) time.
Fusion array types are:

* Array storage, written as `T[n]` where `T` is the element type
  and `n` is the compile-time constant length.
* Dynamic array reference, written as `T[]#`.
* Read-only array reference, written as `T[]`.
* Read-write array reference, written as `T[]!`.
* Nullable dynamic array reference, written as `T[]#?`.
* Nullable read-only array reference, written as `T[]?`.
* Nullable read-write array reference, written as `T[]!?`.

Dynamic array references are allocated on the heap using `new`:

```csharp
int[]# dynamicArray = new int[1000];
```

Dynamic array references can be assigned to other dynamic array references.
The dynamic array is alive as long as there's at least one dynamic
array reference to it.
In C++, dynamic array references are implemented as `std::shared_ptr`.
In C, a custom equivalent of `std::shared_ptr` is generated.

Read-only and read-write array references can point to either array storage
or dynamic arrays. Read-only and read-write references must be used with care
because if the storage or dynamic array gets destroyed, the read-only/read-write
reference becomes a dangling reference and must not be dereferenced.

It is not possible to modify the array via a read-only reference:

```csharp
int[10] arrayStorage;
int[] readOnlyArrayRef = arrayStorage;
readOnlyArrayRef[0] = 42; // fut error
```

Array size is only available for array storage, via `arrayStorage.Length`.

The initial content of arrays (either storage or dynamic) is undefined
unless explicitly specified:

```csharp
int[100] oneHundredZeros = 0;
```

You can fill a part of an array with a single value:

```csharp
array.Fill(value, startIndex, count);
```

or the whole array storage:

```csharp
arrayStorage.Fill(0);
```

Check if the array storage contains an element:

```csharp
bool found = arrayStorage.Contains(42);
```

Array slices can be copied to other arrays and within the same array with
`sourceArray.CopyTo(sourceIndex, destinationArray, destinationIndex, count)`.

Arrays and lists of _numbers_ can be sorted:

```csharp
arrayStorage.Sort();
list.Sort();
arrayRef.Sort(startIndex, count);
```

To retrieve the index of a number in a _sorted_ array, use `BinarySearch`:

```csharp
int index = sortedArray.BinarySearch(value, startIndex, count);
if (index >= startIndex && index < startIndex + count && sortedArray[index] == value)
    Console.WriteLine("found");
else    
    Console.WriteLine("not found");
```

References might be compared - this compares the identity of the arrays,
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
provided to `fut`. For example, `resource<byte[]>("foo.bar")`
is an array consisting of bytes of the file `foo.bar` read while running `fut`.

### Classes

Classes are user-defined compound types.

```csharp
class Animal
{
    // class contents (members) go here
}
```

Note to C++ programmers: do _not_ place a semicolon after the closing brace.

Fusion supports single inheritance. Put _base class_ name after a colon:

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

* _fields_ - the data contained in every _object_
* _methods_ - class-specific code
* _constructor_ - code to be executed on _object creation_
* _constants_ - named compile-time values

Every member has a _visibility_:

* _private_ is the default visibility, meaning the member is only visible
  to other members of the class. There is _no_ `private` keyword.
* `protected` means the member is visible to this class and its subclasses.
* `internal` means the member is visible to the Fusion code compiled with it.
* `public` means the visibility is unrestricted.

#### Fields

Fields are defined by specifying the visibility, type, name,
and optionally, the initial value:

```csharp
class Car
{
    internal string() Model;
    int Year;
    int Seats = 5;
}
```

Languages such as Java and C# initialize every field, even if you don't provide
the initial value. In Fusion fields are uninitialized unless initialized explicitly:

```csharp
class Point
{
    int X = 0;
    int Y = 0;
    ...
}
```

Fields _cannot be_ public. Instead, define _getter/setter_ methods:

```csharp
public class Image
{
    int Width;
    public int GetWidth() { return Width; }
    public int SetWidth!(int value) { Width = value; }

    int Height;
    public int GetHeight() => Height; // syntax sugar
}
```

Fields _cannot be_ static. Shared state poses problems with lifetime
and multithreading.

You can initialize selected fields when creating object storage
or a dynamic object:

```csharp
Rectangle() storage = { Width = 4, Height = 3 };
Shape# dynamic = new Rectangle { Width = 16, Height = 9 };
```

#### Constants

Constants must be assigned a compile-time value, but it can reference other
constants. Constants are implicitly `static`.

```csharp
public class RECOIL
{
    public const int VersionMajor = 6;
    public const int VersionMinor = 3;
    public const int VersionMicro = 4;
    public const string Version = $"{VersionMajor}.{VersionMinor}.{VersionMicro}";
}
```

Constant arrays are also allowed:

```csharp
public class Foo
{
    public const string[] Metasyntactic = { "foo", "bar", "baz", "quux" }; // implicit length
    public const byte[4] SmallPrimes = { 2, 3, 5, 7 }; // explicit length must match
}
```

#### Constructor

Fields can usually be initialized by specifying initial values.
If initialization must be performed by code, define a constructor
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

A public constructor is also required to enable object creation outside Fusion.

Constructors in Fusion _never_ take arguments. This promotes reuse of existing
objects instead of allocating a lot of single-use objects.
If you need to initialize the object with some outside data,
create a method such as `Init`.

#### Methods

Methods are defined by specifying the following in order:

* visibility (`public`, `internal`, `protected` or the default private)
* _call type_ (`static`, `abstract`, `virtual`, `override`, `sealed`
  or the default normal)
* return type (or `void` for no return value)
* method name
* an exclamation mark (`!`) if the method is a _mutator_ (see below)
* a comma-separated parameter list in parentheses
* _method body_, unless the method is `abstract`

Non-static methods have an implicit reference to the object they are working on,
called `this`.

Abstract methods have no body. They must be overridden in a derived class.
The `override` specifier is mandatory (unlike in C++ and Java).
A `sealed` method (`final` in Java terms) is implicitly `override`.
To call a `virtual` or `override` method from a class that overrides
this method, use `base.MethodName(arguments)`.

The method name identifies the method within the class.
Fusion does _not_ support overloading.
It does support _default argument values_, though:

```csharp
int ParseInt(string s, int radix = 10) {
    ...
}
```

A method body is usually a _block_ (sequence of instructions in curly braces).
If the method body consists entirely of the `return` statement,
there's an alternative syntax:

```csharp
public int GetWidth() => Width;
```

A method is called _pure_ if its only effect is the return value
(that is, no state is modified).
`fut` can evaluate such methods at compile time:

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

Similarly to arrays, there are seven types associated with every class `C`:

* `C()` is object storage.
* `C#` is a dynamic object reference.
* `C` is a read-only object reference.
* `C!` is a read-write object reference.
* `C#?` is a nullable dynamic object reference.
* `C?` is a nullable read-only object reference.
* `C!?` is a nullable read-write object reference.

The simplest way to instantiate objects is with object storage:

```csharp
Cat() alik;
```

This translates as follows:

```csharp
Cat alik = new Cat(); // C#
final Cat alik = new Cat(); // Java
const alik = new Cat(); // JavaScript
const alik : Cat = new Cat(); // TypeScript
alik = Cat() # Python
let alik = Cat() // Swift
Cat alik; // C++
Cat alik; // C, potentially followed by construction code
```

Note that in C and C++, object storage variables are placed on the _stack_
for maximum performance.

Array storage of object storage:

```csharp
Wheel()[4] wheels;
```

creates an array of 4 objects of class `Wheel`.

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

Read-only and read-write references translate to raw pointers in C and C++
and therefore become dangling once the object pointed to is destroyed.

Read-only references cannot be used to modify the object,
that is, modify its fields or call a mutator method.

```csharp
Circle() circle;
Shape readOnlyReference = circle;
readOnlyReference.Color = 0x00ff00; // fut error
readOnlyReference.Move(100, 100); // fut error
```

A _mutator method_ of class `C` is a method where `this` is of type `C!`.
In a non-mutator method, `this` is of type `C` (a read-only reference).
Static methods do not have `this`, so the mutator/non-mutator
classification doesn't apply.

To test the runtime type of an object reference, use the `is` operator.
It corresponds to `instanceof` in Java and JavaScript
and `dynamic_cast` in C++.
The following forms are supported:

```csharp
// 1.
bool isDerivedClass = baseReference is Derived;

// 2.
if (baseReference is Derived derivedReadOnlyRef) {
    // use derivedReadOnlyRef here
}

// 3.
if (baseReadWriteOrDynamicReference is Derived! derivedReadWriteRef) {
    // use derivedReadWriteRef here
}

// 4.
if (baseDynamicReference is Derived# derivedDynamicRef) {
    // use derivedDynamicRef here
}
```

The `is` operator can be used in any boolean expressions, but the introduced
derived reference variable can only be used when the `is` operator returns
`true`. That is, the following code is invalid:

```csharp
if (animal is Cat cat) {
}
else {
    cat.Miaow(); // ERROR, "cat" undefined
}
```

For downcasting that always succeeds, use `assert`:

```csharp
assert animal is Cat! cat;
```

This translates to:

```csharp
Cat cat = (Cat) animal; // C#, Java
let cat = animal as! Cat; // Swift
Cat *cat = (Cat *) animal; // C
Cat *cat = static_cast<Cat *>(animal); // C++
const cat = animal; // JavaScript
const cat = animal as Cat; // TypeScript
cat = animal; // Python
```

The `is` operator cannot be used for:

* same type on both sides of the operator (would always return `true`)
* checking if a derived reference is of base type (would always return `true`)
* unrelated types (would always return `false`)
* checking the type of object storage (pointless, `C()` is always an instance
  of class `C`, never a subclass of it)

### Collections

In addition to arrays, Fusion has eight built-in collection types:

* `List<T>` is a resizable array (`std::vector` in C++, `ArrayList` in Java)
* `Queue<T>` is a FIFO (first in, first out) collection
* `Stack<T>` is a LIFO (last in, first out) collection
* `HashSet<T>` is a collection of unique values
* `SortedSet<T>` is a collection of unique sorted values
* `Dictionary<TKey, TValue>` is a dictionary
  (`std::unordered_map` in C++, `HashMap` in Java)
* `SortedDictionary<TKey, TValue>` is a dictionary sorted by key
  (`std::map` in C++, `TreeMap` in Java)
* `OrderedDictionary<TKey, TValue>` is a dictionary that can be iterated
  in insertion order (`LinkedHashMap` in Java)

#### List

A list must specify element type. This can be any Fusion type.

```csharp
List<int>() listOfInts;
List<string()>() listOfStringStorage;
List<string>() listOfStringReferences;
List<Circle()>() listOfCircles; // user-defined class
List<Shape#>() listOfShapes; // dynamic references
List<int[2]>() listOfIntPairs; // array storage
```

`Add` appends a new element at the end of the list.
`Insert` inserts an element at any position.
`AddRange` appends all elements of the passed list.

```csharp
listOfInts.Add(42);
listOfInts.Insert(0, 1337); // insert at the beginning
```

Object or array _storage_ must be added/inserted to the list
without specifying the value.

```csharp
listOfCircles.Add();
listOfIntPairs.Insert(0); // insert at the beginning
```

Use indexing to retrieve or overwrite list elements.

```csharp
Circle! firstCircle = listOfCircles[0];
listOfInts[1] = 5;
```

`Count` returns the number of elements in the list.
`Last()` returns the last element.

You can remove:

* All elements with `Clear()`.
* One element with `RemoveAt(index)`.
* A continuous sequence of elements with `RemoveRange(index, count)`.

`list.Contains(value)` returns `true` if the list contains the specified item.
`list.IndexOf(value)` returns the position of the specified element
or -1 if not found.

`list.CopyTo(sourceIndex, destinationArray, destinationIndex, count)`
copies elements from a list to an array.

`list.All(it => predicate)` returns `true` if all the elements satisfy
the predicate.
`list.Any(it => predicate)` returns `true` if at least one element satisfies
the predicate.

```csharp
List<string()>() names;
names.Add("Alice");
names.Add("Bob");
names.Add("Charlie");
bool isB = names.Any(name => name.StartsWith("B"));
```

#### Queue

A queue is similar to a list. In fact, many target languages don't have
a dedicated queue type and instead employ the same type that's used for a list.
Queue provides four access methods:

* `Enqueue(item)` adds an element at the end of the queue.
* `Dequeue()` removes an element from the beginning of the queue and returns it.
  This operation is only valid if the queue is not empty.
* `Peek()` returns the first element, but doesn't modify the queue.
  Also valid only if the queue is not empty.
* `Clear()` discards all the queue contents.

The `Count` property returns the number of elements in the queue.

#### Stack

Stack provides four access methods:

* `Push(item)` adds an element on top of the stack.
* `Pop()` removes an element on top of the stack and returns it.
  This operation is only valid if the stack is not empty.
* `Peek()` returns the top element, but doesn't modify the stack.
  Also valid only if the stack is not empty.
* `Clear()` discards all the stack contents.

The `Count` property returns the number of elements on the stack.

#### HashSet

A `HashSet` is a collection of unique values: numbers, strings, or enumerations.

* `Add(item)` adds `item` to the collection. It has no effect if the collection
  already contained the item.
* `Remove(item)` removes `item` from the collection.
* `Contains(item)` returns `true` if the collection contains the specified
  element.
* `Clear()` empties the `HashSet`.

The `Count` property returns the number of elements in a `HashSet`.
You can iterate over elements of a `HashSet` with `foreach`, but the iteration
order is not specified.

#### SortedSet

`SortedSet` is the same as `HashSet`, except that `foreach` iterates
in the value order.

#### Dictionary

`Dictionary` provides fast access to a given _value_ associated
with a _key_. The key must be a number, a string or an enumeration.
Value can be of any type.

```csharp
Dictionary<string(), int>() dict;
```

Index the dictionary with a key to insert/overwrite and retrieve elements:

```csharp
dict["foo"] = 42;
dict["foo"] = 1337;
Console.WriteLine(dict["foo"]);
```

Retrieving an element that does not exist is an invalid operation.
Use `ContainsKey` to check for existence - it returns a `bool`.

If the value is object or array _storage_,
create it in the dictionary with `Add(key)`.

`Count` returns the number of key-value pairs.
`Remove(key)` removes one mapping. `Clear()` removes all.

#### SortedDictionary

`SortedDictionary` is the same as `Dictionary`, except that `foreach`
iterates in the key order.

#### OrderedDictionary

`OrderedDictionary` is the same as `Dictionary`, except that `foreach`
iterates in the insertion order.

## Statements

Statements are used in methods and constructors.
Their syntax in Fusion is nearly identical to the languages you already know.

In Fusion there's no empty statement consisting of the sole semicolon.
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

A variable definition may include an initial value:

```csharp
int x = 5;
int[4] array = 0; // initialized with zeros
```

Variable without an initial value is considered uninitialized and must be
assigned before it is read. This also applies to string storage.
For array storage, the array is created,
but its elements are not default-initialized.
For object storage, the fields with initializers are assigned,
and the constructor is called if provided.

Variables have the scope of the enclosing _block_.
It is an error to use a reference pointing
at an array or object outside of its scope.

### Local constants

Constants can be declared not only at the level of classes
(as described above) but also at the level of statements.
Such a definition has a scope of the containing block.
For this reason, local constants do not specify visibility.

### Assignments

Use `=` for assigning variables and fields. Use _op_= for compound assignments.

```csharp
x = 4;
x += 5; // increment by 5
```

Assignments are statements, but not expressions:

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

There is no _comma operator_ in Fusion.

### Returning method result

A method can end its execution with a `return` statement.
`return` must be followed with the returned value,
except for `void` methods, of course.

### Conditional statement

To execute code conditionally, use `if` with an optional `else` clause:

```csharp
if (x == 7)
    DoFoo();
else
    DoBar();
```

### Loops

There are four kinds of loops:

* `while` - checking the condition at the beginning of each run.
* `do/while` - checking the condition after the first run.
* `for` - which contains an initial statement, the condition
  and a statement executed after each run.
* `foreach` - to iterate over array storage, `List`, `HashSet`, `SortedSet`,
  `Dictionary`, `SortedDictionary`, `OrderedDictionary`
  or a string.

```csharp
int[3] array;
array[0] = 5;
array[1] = 10;
array[2] = 15;
foreach (int i in array)
    Console.WriteLine(i);

SortedDictionary<string(), int> dict;
dict["foo"] = 1;
dict["bar"] = 2;
foreach ((string k, int v) in dict)
    Console.WriteLine($"{k} => {v}");

string() s = "42x";
bool allDigits = true;
foreach (int c in s) {
    if (c < '0' || c > '9') {
        allDigits = false;
        break;
    }
}
```

Inside loops you may use the following:

* `break` to leave the loop (the inner one, as there are no loop labels).
* `continue` to skip to the next run.

### Switch statement

The `switch` statement accepts an expression and transfers control
to the corresponding `case` label. It supports integers, enumerations,
strings and object references.

`case` clauses must be correctly terminated.

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
to the next statement: `break`, `continue`, `return` or `throw`.

The `default` clause, if present, must be specified last.

Object references are matched by their _runtime type_:

```csharp
static double CalculateArea(Shape? s)
{
    switch (s) {
    case null:
        return 0;
    case Square sqr:
        return sqr.Size * sqr.Size;
    case Rectangle rect:
        return rect.Width * rect.Height;
    case Circle c:
        return Math.PI * c.Radius * c.Radius;
    default:
        assert false;
    }
}
```

C, C++ and OpenCL do not support `switch` on strings,
so `if`/`else if` with string comparisons are generated.
Object reference matching is not implemented for C and OpenCL.

### Assert statement

The `assert` statement checks if a condition is met at run time.
If it is not, a fatal error occurs.
The optional second argument is a string message.

```java
assert count >= 0;
switch (foo) {
case 1:
    ...
    break;
case 2:
    ...
    break;
default:
    assert false, "foo must be 1 or 2";
}
```

### Exceptions

Fusion can throw exceptions, but cannot handle them at the moment.
The intention is that exceptions will be handled by the code
using the library written in Fusion.

An exception can be thrown with the `throw` statement,
passing an exception class and an optional string argument.
Use `Exception` as a generic class or derive your own classes from it.
It is recommended to do the latter.

Methods potentially throwing exceptions (possibly indirectly via calling
other methods) must specify the exception classes in the `throws` clause,
similar to Java.

```java
public class MyException : Exception
{
    // constructor is auto-generated, do not specify it
}

public class MyObject
{
    public void DoWork() throws MyException
    {
        ...
        throw MyException("Fire"); // no "new"
    }
}
```

The translation of exceptions to C needs an explanation.
The exception class and the string argument are lost in the translation.
The `throw` statement is replaced with `return` with a magic value
representing an error:

* An out-of-range value in a method returning an integer range
  (most often, `-1` for unsigned ranges).
* `NAN` in a method returning a floating-point number.
* `NULL` in a method returning a non-nullable reference.
* `false` in a `void` method. The method will be translated to `bool`,
  and `true` will be returned if the method succeeds.
* Other method types cannot throw exceptions when translating to C.

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

### Environment variables

Environment variables can be retrieved
with `Environment.GetEnvironmentVariable`.
`null` is returned if the variable is not defined.

```csharp
string? homeDir = Environment.GetEnvironmentVariable("HOME");
if (homeDir == null)
    Console.WriteLine("Homeless user");
```

For JavaScript, this is only available in Node.js, not the web browsers.

### Main method

Fusion is a language designed specifically for implementing libraries.
It was _never_ meant for building whole programs.
However, occassionally you might want to experiment with console applications.
For that, you need an entry point in the form of a `Main` method in any of your
public classes. This method should have one of the four possible signatures:

```csharp
public static void Main()
public static void Main(string[] args)
public static int Main()
public static int Main(string[] args)
```

The `args` array contains the command-line arguments, but not the program name.
This array is different from regular array references in that you can:

* Read `args.Length`.
* Run a `foreach (string arg in args)` loop.

The `int` returned from `Main` is a process exit code, which is traditionally
zero for a successful execution and non-zero for an error.

### Locks

Currently there is very limited support for multi-threading. It consists
of the `lock` statement (translated to `synchronized` in Java).
Unlike in C# and Java, not every object can be used for mutual exclusion.
Instead, locks must be explicitly defined as storage of class `Lock`.

```csharp
Lock() mutex; // typically a class field
...
lock (mutex) {
    // code executed by only one thread per this instance of mutex at a time
    ...
}
```

Locks are re-entrant, meaning nested `lock` statements (normally hidden
in called methods) do not cause deadlocks.

### Native blocks

Code which cannot be expressed in Fusion can be written in the target language
using the following syntax:

```java
native {
    printf("Hello, world!\n");
}
```

Generally, native blocks should be used inside `#if` (see below).

Native blocks are allowed:

* as statements in method bodies
* inside `class` (to add class members or decorations to emitted class members)
* at the top level (for `import` / `using` declarations).

## Conditional compilation

Conditional compilation in Fusion is modeled after C#.
Conditional compilation symbols can only be given on the `fut` command line.
Conditional compilation symbols have no assigned value;
they are either present or not.

Example:

```csharp
#if MY_SYMBOL
    MyOptionalFunction();
#endif
```

A more complicated one:

```csharp
#if WINDOWS
    native { DeleteFile(filename); }
#elif LINUX || UNIX
    native { unlink(filename); }
#else
    UNKNOWN OPERATING SYSTEM!
#endif
```

The operators allowed in `#if` and `#elif` are `!`, `&&`, `||`, `==` and `!=`.
You may reference `true`, which is a symbol that is always defined.
`false` is never defined.

## Documentation comments

Documentation comments can describe classes, enumerated types, constants,
methods and their parameters.
They start with three slashes followed by a space and always immediately
precede the documented thing, including a method parameter:

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
Subsequent sentences (if any) give more details.

There are limited formatting options: fixed-width font, paragraphs and bullets.

A `fixed-width font` text (typically code) is delimited with backquotes:

```csharp
/// Returns `true` for an NTSC song and `false` for a PAL song.
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

It is advised to use the following naming conventions in Fusion code:

* Local variables, parameters and local constants start with a lowercase letter,
  capitalize the first letter of the following words - that is, `camelCase`.
* All other identifiers should start with an uppercase letter - that is,
  `PascalCase`.

Generators will translate the above convention
to that which is native to the output language,
for instance, constants as `UPPERCASE_WITH_UNDERSCORES`.
