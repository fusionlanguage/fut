import std.stdio;
import Test;

int main()
{
	if (Test.Test.run()) {
		writeln("PASSED");
		return 0;
	}
	else {
		writeln("FAILED");
		return 1;
	}
}
