#include <cstdio>

#include "Test.hpp"

int main()
{
	std::puts(Test::run() ? "PASSED" : "FAILED");
}
