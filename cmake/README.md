This directory contains CMake modules for finding and executing
[Fusion](https://fusion-lang.org) in CMake projects. The provided
example project (cf. [CMakeLists.txt](CMakeLists.txt)) can be built as
follows:

```sh
mkdir build && cd build
cmake ..
cmake --build .
cmake --build . --target test
```

This builds a C++ library for computing Fibonacci numbers as well as a
test binary, which is executed in the `test` target. Furthermore, if
[SWIG](https://www.swig.org) and [Lua](https://www.lua.org) are
installed, Lua bindings for the Fusion-transpiled C++ library are
automatically generated. The binding can be loaded as follows:

```lua
require("fibonacci_lua")
fibonacci_lua.Fibonacci_fib(20)
```

Note: Static class members present a special problem for Lua. Hence,
SWIG generates wrappers that try to work around some of these issues
(cf. https://www.swig.org/Doc4.0/Lua.html#Lua_nn14). Specifically, the
static member function `fib` of class `Fibonacci` is wrapped as a
global function with name `Fibonacci_fib`.
