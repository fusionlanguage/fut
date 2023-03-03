import fs from "fs";
import { CiConsoleParser, CiProgram, CiSema, CiSystem } from "./bin/CiCheck/Test.js";
const system = CiSystem.new();
const parser = new CiConsoleParser();
parser.program = new CiProgram();
parser.program.parent = system;
parser.program.system = system;
for (let i = 2; i < process.argv.length; i++) {
	const inputFilename = process.argv[i];
	const input = fs.readFileSync(inputFilename);
	parser.parse(inputFilename, input, input.length);
}
const sema = new CiSema();
sema.process(parser.program);
console.log("PASSED");
