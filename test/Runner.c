#include <stdio.h>

#include "Test.h"

int main()
{
	puts(Test_Run() ? "PASSED" : "FAILED");
	return 0;
}
