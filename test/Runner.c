#include <stdio.h>

#include "Test.h"

int main()
{
	if (Test_Run()) {
		puts("PASSED");
		return 0;
	}
	else {
		puts("FAILED");
		return 1;
	}
}
