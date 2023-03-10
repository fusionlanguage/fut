import std.stdio;
import std.file;
import test;

int main(string[] args)
{
	CiSystem = CiSystem.new_();
	CiParser parser = new CiParser;
	parser.program = new CiProgram;
	parser.program.parent = system.get();
	parser.program.system = system.get();
	foreach (inputFilename; args[1 .. $]) {
		string input = cast(string) std.file.read(inputFilename);
		parser.parse(inputFilename, input);
	}
	writeln("PASSED");
}
