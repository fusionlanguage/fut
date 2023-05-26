// cito.cpp - Ci transpiler
//
// Copyright (C) 2011-2023  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

#include <cstring>
#include <filesystem>
#include <format>
#include <fstream>
#include <iostream>
#include <memory>
#include <string>
#include <string_view>
#include <vector>

#include "Transpiled.hpp"

static void usage()
{
	std::cout <<
		"Usage: cito [OPTIONS] -o FILE INPUT.ci\n"
		"Options:\n"
		"-l c       Translate to C\n"
		"-l cpp     Translate to C++\n"
		"-l cs      Translate to C#\n"
		"-l d       Translate to D\n"
		"-l java    Translate to Java\n"
		"-l js      Translate to JavaScript\n"
		"-l py      Translate to Python\n"
		"-l swift   Translate to Swift\n"
		"-l ts      Translate to TypeScript\n"
		"-l d.ts    Translate to TypeScript declarations\n"
		"-l cl      Translate to OpenCL C\n"
		"-o FILE    Write to the specified file\n"
		"-n NAME    Specify C++/C# namespace, Java package or C name prefix\n"
		"-D NAME    Define conditional compilation symbol\n"
		"-r FILE.ci Read the specified source file but don't emit code\n"
		"-I DIR     Add directory to resource search path\n"
		"--help     Display this information\n"
		"--version  Display version information\n";
}

static std::string slurp(std::ifstream &stream)
{
	std::ostringstream oss;
	oss << stream.rdbuf();
	return oss.str();
}

class FileResourceSema : public CiSema
{
	std::vector<const char *> resourceDirs;

	void readResource(std::string_view name, const CiPrefixExpr *expr, std::vector<uint8_t> &content)
	{
		std::ifstream stream;
		for (const char *dir : resourceDirs) {
			stream.open(std::filesystem::path(dir) / name, std::ios_base::binary);
			if (stream) {
				std::string input = slurp(stream);
				content.assign(input.begin(), input.end());
				return;
			}
		}
		stream.open(std::string{name}, std::ios_base::binary);
		if (stream) {
			std::string input = slurp(stream);
			content.assign(input.begin(), input.end());
		}
		else
			reportError(expr, std::format("File {} not found", name));
	}

public:
	void addResourceDir(const char *path)
	{
		resourceDirs.push_back(path);
	}

protected:
	int getResourceLength(std::string_view name, const CiPrefixExpr *expr) override
	{
		auto p = this->program->resources.try_emplace(std::string(name));
		std::vector<uint8_t> &content = p.first->second;
		if (p.second)
			readResource(name, expr, content);
		return content.size();
	}
};

class FileGenHost : public GenHost
{
	std::ofstream stream;

public:
	std::ostream *createFile(std::string_view directory, std::string_view filename) override
	{
		if (directory.empty())
			stream.open(std::string{filename}, std::ios_base::binary);
		else
			stream.open(std::filesystem::path(directory) / filename, std::ios_base::binary);
		return &stream;
	}

	void closeFile() override
	{
		stream.close();
	}
};

static bool parseAndResolve(CiConsoleParser *parser, CiProgram *program,
	const std::vector<const char *> &files, FileResourceSema *sema)
{
	parser->program = program;
	for (const char *file : files) {
		std::ifstream stream(file);
		std::string input = slurp(stream);
		parser->parse(file, reinterpret_cast<const uint8_t *>(input.data()), input.size());
	}
	if (parser->hasErrors)
		return false;
	sema->process(program);
	return !sema->hasErrors;
}

static bool emit(CiProgram *program, const char *lang, const char *namespace_, const char *outputFile)
{
	std::string dir;
	std::unique_ptr<GenBase> gen;
	if (strcmp(lang, "c") == 0)
		gen = std::make_unique<GenC>();
	else if (strcmp(lang, "cpp") == 0)
		gen = std::make_unique<GenCpp>();
	else if (strcmp(lang, "cs") == 0)
		gen = std::make_unique<GenCs>();
	else if (strcmp(lang, "d") == 0)
		gen = std::make_unique<GenD>();
	else if (strcmp(lang, "java") == 0) {
		gen = std::make_unique<GenJava>();
		if (!std::filesystem::is_directory(outputFile)) {
			dir = std::filesystem::path(outputFile).parent_path().string();
			outputFile = dir.c_str();
		}
	}
	else if (strcmp(lang, "js") == 0 || strcmp(lang, "mjs") == 0)
		gen = std::make_unique<GenJs>();
	else if (strcmp(lang, "py") == 0)
		gen = std::make_unique<GenPy>();
	else if (strcmp(lang, "swift") == 0)
		gen = std::make_unique<GenSwift>();
	else if (strcmp(lang, "ts") == 0) {
		std::unique_ptr<GenTs> genTs = std::make_unique<GenTs>();
		genTs->withGenFullCode();
		gen = std::move(genTs);
	}
	else if (strcmp(lang, "d.ts") == 0)
		gen = std::make_unique<GenTs>();
	else if (strcmp(lang, "cl") == 0)
		gen = std::make_unique<GenCl>();
	else {
		std::cerr << "cito: unknown language: " << lang << '\n';
		return false;
	}
	gen->namespace_ = namespace_;
	gen->outputFile = outputFile;
	FileGenHost host;
	gen->host = &host;
	gen->writeProgram(program);
	return !gen->hasErrors;
}

int main(int argc, char **argv)
{
	CiConsoleParser parser;
	std::vector<const char *> inputFiles;
	std::vector<const char *> referencedFiles;
	FileResourceSema sema;
	const char *lang = nullptr;
	const char *outputFile = nullptr;
	const char *namespace_ = "";
	for (int i = 1; i < argc; i++) {
		const char *arg = argv[i];
		if (arg[0] != '-')
			inputFiles.push_back(arg);
		else if (strcmp(arg, "--help") == 0) {
			usage();
			return 0;
		}
		else if (strcmp(arg, "--version") == 0) {
			puts("cito 3.0.0 (C++)");
			return 0;
		}
		else if (arg[1] != '\0' && arg[2] == '\0' && i + 1 < argc) {
			switch (arg[1]) {
			case 'l':
				lang = argv[++i];
				break;
			case 'o':
				outputFile = argv[++i];
				break;
			case 'n':
				namespace_ = argv[++i];
				break;
			case 'D': {
					const char *symbol = argv[++i];
					if (strcmp(symbol, "true") == 0 || strcmp(symbol, "false") == 0) {
						std::cerr << "cito: '" << symbol << "' is reserved\n";
						return 1;
					}
					parser.addPreSymbol(symbol);
				}
				break;
			case 'r':
				referencedFiles.push_back(argv[++i]);
				break;
			case 'I':
				sema.addResourceDir(argv[++i]);
				break;
			default:
				std::cerr << "cito: unknown option: " << arg << '\n';
				return 1;
			}
		}
		else {
			std::cerr << "cito: unknown option: " << arg << '\n';
			return 1;
		}
	}
	if (outputFile == nullptr || inputFiles.empty()) {
		usage();
		return 1;
	}

	std::shared_ptr<CiSystem> system = CiSystem::new_();
	CiScope *parent = system.get();
	CiProgram references;
	if (!referencedFiles.empty()) {
		references.parent = parent;
		references.system = system.get();
		if (!parseAndResolve(&parser, &references, referencedFiles, &sema))
			return 1;
		parent = &references;
	}
	CiProgram program;
	program.parent = parent;
	program.system = system.get();
	if (!parseAndResolve(&parser, &program, inputFiles, &sema))
		return 1;

	if (lang != nullptr)
		return emit(&program, lang, namespace_, outputFile) ? 0 : 1;
	for (size_t i = strlen(outputFile); --i >= 0; ) {
		char c = outputFile[i];
		if (c == '.') {
			if (i >= 2
			 && (outputFile[i - 2] == '.' || outputFile[i - 2] == ',')
			 && strncmp(outputFile + i - 1, "d.ts", 4) == 0
			 && (outputFile[i + 3] == '\0' || outputFile[i + 3] == ','))
				continue;
			std::string outputBase { outputFile, i + 1 };
			const char *extBegin = outputFile + i + 1;
			for (;;) {
				const char *extEnd = strchr(extBegin, ',');
				if (extEnd == nullptr)
					break;
				std::string outputExt = std::string(extBegin, extEnd);
				if (!emit(&program, outputExt.c_str(), namespace_, (outputBase + outputExt).c_str()))
					return 1;
				extBegin = extEnd + 1;
			}
			if (!emit(&program, extBegin, namespace_, (outputBase + extBegin).c_str()))
				return 1;
			return 0;
		}
		if (c == '/' || c == '\\' || c == ':')
			break;
	}
	std::cerr << "cito: don't know what language to translate to: no extension in '" << outputFile << "' and no '-l' option\n";
	return 1;
}
