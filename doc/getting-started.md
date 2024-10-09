# Getting started with Fusion

## Installing fut

The command-line transpiler `fut` runs on Windows, macOS and Linux.

Download the [release](https://github.com/fusionlanguage/fut/releases/tag/fut-3.2.7)
or [build from sources](building-fut.md).

## Syntax highlighting

To install Fusion syntax highlighting in your IDE or text editor, follow the [instructions](editors.md).

## Hello, world!

Now you are ready to try out your first Fusion code:

```csharp
public static class HelloFu
{
    /// Returns a greeting message.
    public static string GetMessage()
    {
        return "Hello, world!";
    }
}
```

Save the above in `hello.fu`, then issue this command:

    fut -o hello.c,cpp,cs,d,java,js,py,swift,ts,cl hello.fu

This will translate the Fusion code to
C, C++, C#, D, Java, JavaScript, Python, Swift, TypeScript and OpenCL C.
The `fut` command accepts one or more Fusion source files (here, just `hello.fu`)
and outputs the transpiled source files as specified by the mandatory `-o` option.
Here we specified several languages, comma-separated.
In a real-world scenario, you would integrate the `fut` command into your build system.

Now let's look at the outputs. `hello.cs`:

```csharp
public static class HelloFu
{

	/// <summary>Returns a greeting message.</summary>
	public static string GetMessage()
	{
		return "Hello, world!";
	}
}
```

This is virtually identical to the Fusion source, with just documentation comment markup added.

The Java source file is named `HelloFu.java`, after the public class it defines.
This is a requirement of the Java programming language.

```java
public final class HelloFu
{
	private HelloFu()
	{
	}

	/**
	 * Returns a greeting message.
	 */
	public static String getMessage()
	{
		return "Hello, world!";
	}
}
```

Since Java has no concept of static classes, it's emulated with `final` and a private constructor.
The documentation comment is in JavaDoc syntax and `String` is spelled with an uppercase `S`.

The C++ translation consists of two files: `hello.hpp` defines the class and _declares_ the method:

```cpp
#pragma once
#include <string_view>
class HelloFu;

class HelloFu
{
public:
	/**
	 * Returns a greeting message.
	 */
	static std::string_view getMessage();
private:
	HelloFu() = delete;
};
```

while `hello.cpp` _defines_ the method:

```cpp
#include "hello.hpp"

std::string_view HelloFu::getMessage()
{
	return "Hello, world!";
}
```

The class is static because of the deleted constructor.

Similarly, the C output consists of the header file `hello.h`:

```c
#pragma once
#ifdef __cplusplus
extern "C" {
#endif

/**
 * Returns a greeting message.
 */
const char *HelloFu_GetMessage(void);

#ifdef __cplusplus
}
#endif
```

and the implementation file `hello.c`:

```c
#include <stdlib.h>
#include "hello.h"

const char *HelloFu_GetMessage(void)
{
	return "Hello, world!";
}
```

`hello.swift` is in the Apple-centric language Swift:

```swift
public class HelloFu
{

	/// Returns a greeting message.
	public static func getMessage() -> String
	{
		return "Hello, world!"
	}
}
```

Note the different placement of the return type and the lack of semicolon.

The JavaScript output `hello.js` does not specify the return type:

```js
"use strict";

class HelloFu
{

	/**
	 * Returns a greeting message.
	 */
	static getMessage()
	{
		return "Hello, world!";
	}
}
```

TypeScript is a JavaScript derivative, with explicit types and visibility control:

```ts
export class HelloFu
{
	private constructor()
	{
	}

	/**
	 * Returns a greeting message.
	 */
	public static getMessage(): string
	{
		return "Hello, world!";
	}
}
```

`hello.py` in Python:

```python
class HelloFu:

	@staticmethod
	def get_message() -> str:
		"""Returns a greeting message."""
		return "Hello, world!"
```

Finally, there's OpenCL code that can run on a GPU:

```opencl
/**
 * Returns a greeting message.
 */
constant char *HelloFu_GetMessage(void);

constant char *HelloFu_GetMessage(void)
{
	return "Hello, world!";
}
```

As you can see, `fut` simply rewrites your code in different languages.

Now, you may wonder why the "Hello, world" code _does not print_ the message
in the console? That's because Fusion was never intended to be used to write
complete programs. What you write are reusable components aka libraries.
In this minimal example we have a class with one method that returns a string.
All the languages mentioned above can easily call this method.
For example, this is how you could use it from C:

```c
#include <stdio.h>
#include "hello.h"

int main()
{
    puts(HelloFu_GetMessage());
}
```

In Java, you could display the message in a message box.
In Python, you could emit the message to a website.
In C++, you could display the message on an embedded display.
The point is, Fusion abstracts from the user interfaces.

## Language documentation

Fusion is explained in depth in its [reference documentation](reference.md).

## Example projects

It's always good to study the language by looking at projects written
in it - starting with really small ones:

- [a toy ray-tracer](https://github.com/pfusik/ray-fu)
- [encoder of Data Matrix barcodes](https://github.com/pfusik/datamatrix-fu)
- [encoder/decoder of the Quite OK Image format](https://github.com/pfusik/qoi-fu)
- [encoder/decoder of the Quite OK Audio format](https://github.com/pfusik/qoa-fu)
- [decoder of PNG, GIF and JPEG](https://github.com/pfusik/image-fu)

Then there's a [very portable chiptune player](https://asap.sourceforge.net):

![ASAP architecture](https://asap.sourceforge.net/asap-internals.png)

and a [decoder of 500+ retro image formats](https://recoil.sourceforge.net):

![RECOIL architecture](https://recoil.sourceforge.net/recoil-internals.png)

Last, but not least, `fut` itself is implemented in Fusion.

## Community

Please join our [Discussions](https://github.com/fusionlanguage/fut/discussions)
and submit [Issues](https://github.com/fusionlanguage/fut/issues)
and [Pull requests](https://github.com/fusionlanguage/fut/pulls) on GitHub!
