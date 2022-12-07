# Getting started with Ć

## Installing cito

The transpiler `cito` runs on Windows, macOS and Linux.

First, install [.NET SDK](https://dotnet.microsoft.com/en-us/download), version 7.0 or 6.0.
If you are on Windows, .NET SDK is included in Visual Studio 2022.
On macOS you might use:

    brew install dotnet-sdk

Then issue this command:

    dotnet tool install -g cito

If it displays instructions on how to configure your PATH variable, please follow them.

## Syntax highlighting

To install Ć syntax highlighting in your IDE or text editor, follow the [instructions](editors.md).

## Hello, world!

Now you are ready to try out your first Ć code:

```csharp
public static class HelloCi
{
    /// Returns a greeting message.
    public static string GetMessage()
    {
        return "Hello, world!";
    }
}
```

Save the above in `hello.ci`, then issue this command:

    cito -o hello.c,cpp,cs,java,js,py,swift,ts,cl hello.ci

This will translate the Ć code to C, C++, C#, Java, JavaScript, Python, Swift, TypeScript and OpenCL C.
The `cito` command accepts one or more Ć source files (here, just `hello.ci`)
and outputs the transpiled source files as specified by the mandatory `-o` option.
Here we specified several languages, comma-separated.
In a real-world scenario, you would integrate the `cito` command into your build system.

Now let's look at the outputs. `hello.cs`:

```csharp
public static class HelloCi
{

	/// <summary>Returns a greeting message.</summary>
	public static string GetMessage()
	{
		return "Hello, world!";
	}
}
```

This is virtually identical to the Ć source, with just documentation comment markup added.

The Java source file is named `HelloCi.java`, after the public class it defines.
This is a requirement of the Java programming language.

```java
public final class HelloCi
{
	private HelloCi()
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

Since Java has no concept of static classes, this is emulated with `final` and a private constructor.
The documentation comment is in JavaDoc syntax and `String` is spelled with an uppercase `S`.

The C++ translation consists of two files: `hello.hpp` defines the class and _declares_ the method:

```cpp
#pragma once
#include <string_view>
class HelloCi;

class HelloCi
{
public:
	/**
	 * Returns a greeting message.
	 */
	static std::string_view getMessage();
private:
	HelloCi() = delete;
};
```

while `hello.cpp` _defines_ the method:

```cpp
#include "hello.hpp"

std::string_view HelloCi::getMessage()
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
const char *HelloCi_GetMessage(void);

#ifdef __cplusplus
}
#endif
```

and the implementation file `hello.c`:

```c
#include <stdlib.h>
#include "hello.h"

const char *HelloCi_GetMessage(void)
{
	return "Hello, world!";
}
```

`hello.swift` is in the Apple-centric language Swift:

```swift
public class HelloCi
{

	/// Returns a greeting message.
	public static func getMessage() -> String?
	{
		return "Hello, world!"
	}
}
```

Note the different placement of the return type and the lack of semicolon.

The JavaScript output `hello.js` does not specify the return type:

```js
"use strict";

class HelloCi
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
export class HelloCi
{
	private constructor()
	{
	}

	/**
	 * Returns a greeting message.
	 */
	public static getMessage(): string | null
	{
		return "Hello, world!";
	}
}
```

`hello.py` in Python:

```python
class HelloCi:

	@staticmethod
	def get_message():
		"""Returns a greeting message."""
		return "Hello, world!"
```

Finally, there's OpenCL code that can run on a GPU:

```opencl
/**
 * Returns a greeting message.
 */
constant char *HelloCi_GetMessage(void);

constant char *HelloCi_GetMessage(void)
{
	return "Hello, world!";
}
```

As you can see, `cito` simply rewrites your code in different languages.

Now, you may wonder why the code _does not print_ the message in the console?
That's because Ć was never intended to be used to write complete programs.
What you write are reusable components aka libraries.
In this minimal example we have a class with one method that returns a string.
All the languages mentioned above can easily call this method.
For example, this is how you could use it from C:

```c
#include <stdio.h>
#include "hello.h"

int main()
{
    puts(HelloCi_GetMessage());
}
```

In Java, you could display the message in a message box.
In Python, you could emit the message to a website.
In C++, you could display the message on an embedded display.
The point is, Ć abstracts from the user interfaces.

## Language documentation

Ć is explained in depth in its [reference documentation](reference.md).

## Example projects

It's always good to study the language by looking at projects written
in it - starting with really small ones:

- [a toy ray-tracer](https://github.com/pfusik/ray-ci)
- [encoder of Data Matrix barcodes](https://github.com/pfusik/datamatrix-ci)
- [encoder/decoder of the Quite OK Image format](https://github.com/pfusik/qoi-ci)
- [decoder of PNG, GIF and JPEG](https://github.com/pfusik/image-ci)

Then there's a [very portable chiptune player](https://asap.sourceforge.net):

![ASAP architecture](https://asap.sourceforge.net/asap-internals.png)

and a [decoder of 500+ retro image formats](https://recoil.sourceforge.net):

![RECOIL architecture](https://recoil.sourceforge.net/recoil-internals.png)

`cito` itself was initially written in C#, but is getting [rewritten in Ć](https://github.com/pfusik/cito/issues/48).

## Community

Please join our Discussions and submit Issues and Pull requests on GitHub!
