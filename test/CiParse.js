const fs = require("fs");
const system = CiSystem.new();
const parser = new CiParser();
parser.program = new CiProgram();
parser.program.parent = system;
parser.program.system = system;
for (let i = 2; i < process.argv.length; i++) {
	const inputFilename = process.argv[i];
	const input = fs.readFileSync(inputFilename);
	parser.parse(inputFilename, input, input.length);
}
console.log("PASSED");
