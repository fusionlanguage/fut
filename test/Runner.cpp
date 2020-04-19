#include <cstdio>

#include "Test.hpp"

int main()
{
	if (Test::run()) {
		std::puts("PASSED");
		return 0;
	}
	else {
		std::puts("FAILED");
		return 1;
	}
}
