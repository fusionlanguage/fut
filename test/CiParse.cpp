#include <fstream>
#include <iostream>
#include <memory>
#include <string>

#include "Test.hpp"

int main(int argc, char **argv)
{
	std::shared_ptr<CiSystem> system = CiSystem::new_();
	CiProgram program;
	program.parent = system.get();
	program.system = system.get();
	CiConsoleParser parser;
	parser.program = &program;
	std::string input;
	for (int i = 1; i < argc; i++) {
		const char *inputFilename = argv[i];
		std::getline(std::ifstream(inputFilename), input, '\0');
		parser.parse(inputFilename, reinterpret_cast<const uint8_t *>(input.data()), input.size());
	}
	std::cout << "PASSED\n";
}
