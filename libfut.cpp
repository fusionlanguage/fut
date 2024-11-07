// Generated automatically with "fut". Do not edit.
#include <algorithm>
#include <cassert>
#include <charconv>
#include <cmath>
#include <cstdlib>
#include <format>
#include "libfut.hpp"

template <class T, class... Args>
bool FuNumber_TryParse(T &number, std::string_view s, Args... args)
{
	const char *end = s.data() + s.size();
	auto result = std::from_chars(s.data(), end, number, args...);
	return result.ec == std::errc{} && result.ptr == end;
}

static std::string FuString_Replace(std::string_view s, std::string_view oldValue, std::string_view newValue)
{
	std::string result;
	result.reserve(s.size());
	for (std::string_view::size_type i = 0;;) {
		auto j = s.find(oldValue, i);
		if (j == std::string::npos) {
			result.append(s, i);
			return result;
		}
		result.append(s, i, j - i);
		result.append(newValue);
		i = j + oldValue.size();
	}
}

void FuParserHost::reportStatementError(const FuStatement * statement, std::string_view message)
{
	int line = this->program->getLine(statement->loc);
	int column = statement->loc - this->program->lineLocs[line];
	const FuSourceFile * file = this->program->getSourceFile(line);
	reportError(file->filename, line - file->line, column, column + statement->getLocLength(), message);
}

void FuLexer::setHost(FuParserHost * host)
{
	this->host = host;
}

void FuLexer::addPreSymbol(std::string_view symbol)
{
	this->preSymbols.emplace(symbol);
}

void FuLexer::open(std::string_view filename, uint8_t const * input, int inputLength)
{
	this->input = input;
	this->inputLength = inputLength;
	this->nextOffset = 0;
	this->host->program->sourceFiles.emplace_back();
	this->host->program->sourceFiles.back().filename = filename;
	this->host->program->sourceFiles.back().line = std::ssize(this->host->program->lineLocs);
	this->host->program->lineLocs.push_back(this->loc);
	fillNextChar();
	if (this->nextChar == 65279)
		fillNextChar();
	nextToken();
}

void FuLexer::reportError(std::string_view message) const
{
	const FuSourceFile * file = &static_cast<const FuSourceFile &>(this->host->program->sourceFiles.back());
	int line = std::ssize(this->host->program->lineLocs) - file->line - 1;
	int lineLoc = this->host->program->lineLocs.back();
	this->host->reportError(file->filename, line, this->tokenLoc - lineLoc, this->loc - lineLoc, message);
}

int FuLexer::readByte()
{
	if (this->nextOffset >= this->inputLength)
		return -1;
	return this->input[this->nextOffset++];
}

int FuLexer::readContinuationByte(int hi)
{
	int b = readByte();
	if (hi != 65533) {
		if (b >= 128 && b <= 191)
			return (hi << 6) + b - 128;
		reportError("Invalid UTF-8");
	}
	return 65533;
}

void FuLexer::fillNextChar()
{
	this->charOffset = this->nextOffset;
	int b = readByte();
	if (b >= 128) {
		if (b < 194 || b > 244) {
			reportError("Invalid UTF-8");
			b = 65533;
		}
		else if (b < 224)
			b = readContinuationByte(b - 192);
		else if (b < 240) {
			b = readContinuationByte(b - 224);
			b = readContinuationByte(b);
		}
		else {
			b = readContinuationByte(b - 240);
			b = readContinuationByte(b);
			b = readContinuationByte(b);
		}
	}
	this->nextChar = b;
}

int FuLexer::peekChar() const
{
	return this->nextChar;
}

bool FuLexer::isLetterOrDigit(int c)
{
	if (c >= 'a' && c <= 'z')
		return true;
	if (c >= 'A' && c <= 'Z')
		return true;
	if (c >= '0' && c <= '9')
		return true;
	return c == '_';
}

int FuLexer::readChar()
{
	int c = this->nextChar;
	switch (c) {
	case '\t':
	case ' ':
		this->loc++;
		break;
	case '\n':
		this->host->program->lineLocs.push_back(this->loc);
		this->atLineStart = true;
		break;
	default:
		this->loc += c < 65536 ? 1 : 2;
		this->atLineStart = false;
		break;
	}
	fillNextChar();
	return c;
}

bool FuLexer::eatChar(int c)
{
	if (peekChar() == c) {
		readChar();
		return true;
	}
	return false;
}

void FuLexer::skipWhitespace()
{
	while (peekChar() == '\t' || peekChar() == ' ' || peekChar() == '\r')
		readChar();
}

FuToken FuLexer::readIntegerLiteral(int bits)
{
	bool invalidDigit = false;
	bool tooBig = false;
	bool needDigit = true;
	for (int64_t i = 0;; readChar()) {
		int c = peekChar();
		if (c >= '0' && c <= '9')
			c -= '0';
		else if (c >= 'A' && c <= 'Z')
			c -= 55;
		else if (c >= 'a' && c <= 'z')
			c -= 87;
		else if (c == '_') {
			needDigit = true;
			continue;
		}
		else {
			this->longValue = i;
			if (invalidDigit || needDigit)
				reportError("Invalid integer");
			else if (tooBig)
				reportError("Integer too big");
			return FuToken::literalLong;
		}
		if (c >= 1 << bits)
			invalidDigit = true;
		else if (i >> (64 - bits) != 0)
			tooBig = true;
		else
			i = (i << bits) + c;
		needDigit = false;
	}
}

FuToken FuLexer::readFloatLiteral(bool needDigit)
{
	bool underscoreE = false;
	bool exponent = false;
	for (;;) {
		int c = peekChar();
		switch (c) {
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
			readChar();
			needDigit = false;
			break;
		case 'E':
		case 'e':
			if (exponent) {
				reportError("Invalid floating-point number");
				return FuToken::literalDouble;
			}
			if (needDigit)
				underscoreE = true;
			readChar();
			c = peekChar();
			if (c == '+' || c == '-')
				readChar();
			exponent = true;
			needDigit = true;
			break;
		case '_':
			readChar();
			needDigit = true;
			break;
		default:
			if (underscoreE || needDigit || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
				reportError("Invalid floating-point number");
			return FuToken::literalDouble;
		}
	}
}

FuToken FuLexer::readNumberLiteral(int64_t i)
{
	bool leadingZero = false;
	bool tooBig = false;
	for (bool needDigit = false;; readChar()) {
		int c = peekChar();
		switch (c) {
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
			c -= '0';
			break;
		case '.':
			readChar();
			return readFloatLiteral(true);
		case 'e':
		case 'E':
			return readFloatLiteral(needDigit);
		case '_':
			needDigit = true;
			continue;
		default:
			this->longValue = i;
			if (leadingZero)
				reportError("Leading zeros are not permitted, octal numbers must begin with 0o");
			if (needDigit || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
				reportError("Invalid integer");
			else if (tooBig)
				reportError("Integer too big");
			return FuToken::literalLong;
		}
		if (i == 0)
			leadingZero = true;
		if (i > (c < 8 ? 922337203685477580 : 922337203685477579))
			tooBig = true;
		else
			i = 10 * i + c;
		needDigit = false;
	}
}

int FuLexer::getEscapedChar(int c)
{
	switch (c) {
	case '"':
		return '"';
	case '\'':
		return '\'';
	case '\\':
		return '\\';
	case 'n':
		return '\n';
	case 'r':
		return '\r';
	case 't':
		return '\t';
	default:
		return -1;
	}
}

int FuLexer::readCharLiteral()
{
	int c = readChar();
	if (c < 32) {
		reportError("Invalid character in literal");
		return 65533;
	}
	if (c != '\\')
		return c;
	c = getEscapedChar(readChar());
	if (c < 0) {
		reportError("Unknown escape sequence");
		return 65533;
	}
	return c;
}

FuToken FuLexer::readString(bool interpolated)
{
	for (int offset = this->charOffset;;) {
		switch (peekChar()) {
		case -1:
			reportError("Unterminated string literal");
			return FuToken::endOfFile;
		case '\n':
			reportError("Unterminated string literal");
			this->stringValue = "";
			return FuToken::literalString;
		case '"':
			{
				int endOffset = this->charOffset;
				readChar();
				this->stringValue = std::string_view(reinterpret_cast<const char *>(this->input + offset), endOffset - offset);
			}
			return FuToken::literalString;
		case '{':
			if (interpolated) {
				int endOffset = this->charOffset;
				readChar();
				if (eatChar('{'))
					break;
				if (!this->skippingUnmet) {
					this->stringValue = std::string_view(reinterpret_cast<const char *>(this->input + offset), endOffset - offset);
					return FuToken::interpolatedString;
				}
				for (;;) {
					FuToken token = readPreToken();
					if (token == FuToken::rightBrace)
						break;
					if (token == FuToken::endOfFile) {
						reportError("Unterminated string literal");
						return FuToken::endOfFile;
					}
				}
			}
			else
				readChar();
			break;
		default:
			readCharLiteral();
			break;
		}
	}
}

bool FuLexer::endWord(int c)
{
	return eatChar(c) && !isLetterOrDigit(peekChar());
}

std::string FuLexer::getLexeme() const
{
	return std::string(reinterpret_cast<const char *>(this->input + this->lexemeOffset), this->charOffset - this->lexemeOffset);
}

FuToken FuLexer::readPreToken()
{
	for (;;) {
		bool atLineStart = this->atLineStart;
		this->tokenLoc = this->loc;
		this->lexemeOffset = this->charOffset;
		int c = readChar();
		switch (c) {
		case -1:
			return FuToken::endOfFile;
		case '\t':
		case '\r':
		case ' ':
			break;
		case '\n':
			if (this->lineMode)
				return FuToken::endOfLine;
			break;
		case '#':
			if (!atLineStart)
				return FuToken::hash;
			switch (peekChar()) {
			case 'i':
				readChar();
				return endWord('f') ? FuToken::preIf : FuToken::preUnknown;
			case 'e':
				readChar();
				switch (peekChar()) {
				case 'l':
					readChar();
					switch (peekChar()) {
					case 'i':
						readChar();
						return endWord('f') ? FuToken::preElIf : FuToken::preUnknown;
					case 's':
						readChar();
						return endWord('e') ? FuToken::preElse : FuToken::preUnknown;
					default:
						return FuToken::preUnknown;
					}
				case 'n':
					readChar();
					return eatChar('d') && eatChar('i') && endWord('f') ? FuToken::preEndIf : FuToken::preUnknown;
				default:
					return FuToken::preUnknown;
				}
			default:
				return FuToken::preUnknown;
			}
		case ';':
			return FuToken::semicolon;
		case '.':
			if (eatChar('.'))
				return FuToken::range;
			return FuToken::dot;
		case ',':
			return FuToken::comma;
		case '(':
			return FuToken::leftParenthesis;
		case ')':
			return FuToken::rightParenthesis;
		case '[':
			return FuToken::leftBracket;
		case ']':
			return FuToken::rightBracket;
		case '{':
			return FuToken::leftBrace;
		case '}':
			return FuToken::rightBrace;
		case '~':
			return FuToken::tilde;
		case '?':
			return FuToken::questionMark;
		case ':':
			return FuToken::colon;
		case '+':
			if (eatChar('+'))
				return FuToken::increment;
			if (eatChar('='))
				return FuToken::addAssign;
			return FuToken::plus;
		case '-':
			if (eatChar('-'))
				return FuToken::decrement;
			if (eatChar('='))
				return FuToken::subAssign;
			return FuToken::minus;
		case '*':
			if (eatChar('='))
				return FuToken::mulAssign;
			return FuToken::asterisk;
		case '/':
			if (eatChar('/')) {
				c = readChar();
				if (c == '/' && !this->skippingUnmet) {
					skipWhitespace();
					switch (peekChar()) {
					case '\n':
						return FuToken::docBlank;
					case '*':
						readChar();
						skipWhitespace();
						return FuToken::docBullet;
					default:
						return FuToken::docRegular;
					}
				}
				while (c != '\n' && c >= 0)
					c = readChar();
				if (c == '\n' && this->lineMode)
					return FuToken::endOfLine;
				break;
			}
			if (eatChar('*')) {
				int startLine = std::ssize(this->host->program->lineLocs);
				do {
					c = readChar();
					if (c < 0) {
						reportError(std::format("Unterminated multi-line comment, started in line {}", startLine));
						return FuToken::endOfFile;
					}
				}
				while (c != '*' || peekChar() != '/');
				readChar();
				break;
			}
			if (eatChar('='))
				return FuToken::divAssign;
			return FuToken::slash;
		case '%':
			if (eatChar('='))
				return FuToken::modAssign;
			return FuToken::mod;
		case '&':
			if (eatChar('&'))
				return FuToken::condAnd;
			if (eatChar('='))
				return FuToken::andAssign;
			return FuToken::and_;
		case '|':
			if (eatChar('|'))
				return FuToken::condOr;
			if (eatChar('='))
				return FuToken::orAssign;
			return FuToken::or_;
		case '^':
			if (eatChar('='))
				return FuToken::xorAssign;
			return FuToken::xor_;
		case '=':
			if (eatChar('='))
				return FuToken::equal;
			if (eatChar('>'))
				return FuToken::fatArrow;
			return FuToken::assign;
		case '!':
			if (eatChar('='))
				return FuToken::notEqual;
			return FuToken::exclamationMark;
		case '<':
			if (eatChar('<')) {
				if (eatChar('='))
					return FuToken::shiftLeftAssign;
				return FuToken::shiftLeft;
			}
			if (eatChar('='))
				return FuToken::lessOrEqual;
			return FuToken::less;
		case '>':
			if (this->parsingTypeArg)
				return FuToken::rightAngle;
			if (eatChar('>')) {
				if (eatChar('='))
					return FuToken::shiftRightAssign;
				return FuToken::shiftRight;
			}
			if (eatChar('='))
				return FuToken::greaterOrEqual;
			return FuToken::greater;
		case '\'':
			if (peekChar() == '\'') {
				reportError("Empty character literal");
				this->longValue = 0;
			}
			else
				this->longValue = readCharLiteral();
			if (!eatChar('\''))
				reportError("Unterminated character literal");
			return FuToken::literalChar;
		case '"':
			return readString(false);
		case '$':
			if (eatChar('"'))
				return readString(true);
			reportError("Expected interpolated string");
			break;
		case '0':
			switch (peekChar()) {
			case 'B':
			case 'b':
				readChar();
				return readIntegerLiteral(1);
			case 'O':
			case 'o':
				readChar();
				return readIntegerLiteral(3);
			case 'X':
			case 'x':
				readChar();
				return readIntegerLiteral(4);
			default:
				return readNumberLiteral(0);
			}
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
			return readNumberLiteral(c - '0');
		default:
			if (!isLetterOrDigit(c)) {
				reportError("Invalid character");
				continue;
			}
			while (isLetterOrDigit(peekChar()))
				readChar();
			this->stringValue = getLexeme();
			if (this->stringValue == "abstract")
				return FuToken::abstract;
			else if (this->stringValue == "assert")
				return FuToken::assert;
			else if (this->stringValue == "break")
				return FuToken::break_;
			else if (this->stringValue == "case")
				return FuToken::case_;
			else if (this->stringValue == "class")
				return FuToken::class_;
			else if (this->stringValue == "const")
				return FuToken::const_;
			else if (this->stringValue == "continue")
				return FuToken::continue_;
			else if (this->stringValue == "default")
				return FuToken::default_;
			else if (this->stringValue == "do")
				return FuToken::do_;
			else if (this->stringValue == "else")
				return FuToken::else_;
			else if (this->stringValue == "enum")
				return FuToken::enum_;
			else if (this->stringValue == "false")
				return FuToken::false_;
			else if (this->stringValue == "for")
				return FuToken::for_;
			else if (this->stringValue == "foreach")
				return FuToken::foreach;
			else if (this->stringValue == "if")
				return FuToken::if_;
			else if (this->stringValue == "in")
				return FuToken::in;
			else if (this->stringValue == "internal")
				return FuToken::internal;
			else if (this->stringValue == "is")
				return FuToken::is;
			else if (this->stringValue == "lock")
				return FuToken::lock_;
			else if (this->stringValue == "native")
				return FuToken::native;
			else if (this->stringValue == "new")
				return FuToken::new_;
			else if (this->stringValue == "null")
				return FuToken::null;
			else if (this->stringValue == "override")
				return FuToken::override_;
			else if (this->stringValue == "protected")
				return FuToken::protected_;
			else if (this->stringValue == "public")
				return FuToken::public_;
			else if (this->stringValue == "resource")
				return FuToken::resource;
			else if (this->stringValue == "return")
				return FuToken::return_;
			else if (this->stringValue == "sealed")
				return FuToken::sealed;
			else if (this->stringValue == "static")
				return FuToken::static_;
			else if (this->stringValue == "switch")
				return FuToken::switch_;
			else if (this->stringValue == "throw")
				return FuToken::throw_;
			else if (this->stringValue == "throws")
				return FuToken::throws;
			else if (this->stringValue == "true")
				return FuToken::true_;
			else if (this->stringValue == "virtual")
				return FuToken::virtual_;
			else if (this->stringValue == "void")
				return FuToken::void_;
			else if (this->stringValue == "when")
				return FuToken::when;
			else if (this->stringValue == "while")
				return FuToken::while_;
			else
				return FuToken::id;
		}
	}
}

void FuLexer::nextPreToken()
{
	this->currentToken = readPreToken();
}

bool FuLexer::see(FuToken token) const
{
	return this->currentToken == token;
}

std::string_view FuLexer::tokenToString(FuToken token)
{
	switch (token) {
	case FuToken::endOfFile:
		return "end-of-file";
	case FuToken::id:
		return "identifier";
	case FuToken::literalLong:
		return "integer constant";
	case FuToken::literalDouble:
		return "floating-point constant";
	case FuToken::literalChar:
		return "character constant";
	case FuToken::literalString:
		return "string constant";
	case FuToken::interpolatedString:
		return "interpolated string";
	case FuToken::semicolon:
		return "';'";
	case FuToken::dot:
		return "'.'";
	case FuToken::comma:
		return "','";
	case FuToken::leftParenthesis:
		return "'('";
	case FuToken::rightParenthesis:
		return "')'";
	case FuToken::leftBracket:
		return "'['";
	case FuToken::rightBracket:
		return "']'";
	case FuToken::leftBrace:
		return "'{'";
	case FuToken::rightBrace:
		return "'}'";
	case FuToken::plus:
		return "'+'";
	case FuToken::minus:
		return "'-'";
	case FuToken::asterisk:
		return "'*'";
	case FuToken::slash:
		return "'/'";
	case FuToken::mod:
		return "'%'";
	case FuToken::and_:
		return "'&'";
	case FuToken::or_:
		return "'|'";
	case FuToken::xor_:
		return "'^'";
	case FuToken::tilde:
		return "'~'";
	case FuToken::shiftLeft:
		return "'<<'";
	case FuToken::shiftRight:
		return "'>>'";
	case FuToken::equal:
		return "'=='";
	case FuToken::notEqual:
		return "'!='";
	case FuToken::less:
		return "'<'";
	case FuToken::lessOrEqual:
		return "'<='";
	case FuToken::greater:
		return "'>'";
	case FuToken::greaterOrEqual:
		return "'>='";
	case FuToken::rightAngle:
		return "'>'";
	case FuToken::condAnd:
		return "'&&'";
	case FuToken::condOr:
		return "'||'";
	case FuToken::exclamationMark:
		return "'!'";
	case FuToken::hash:
		return "'#'";
	case FuToken::assign:
		return "'='";
	case FuToken::addAssign:
		return "'+='";
	case FuToken::subAssign:
		return "'-='";
	case FuToken::mulAssign:
		return "'*='";
	case FuToken::divAssign:
		return "'/='";
	case FuToken::modAssign:
		return "'%='";
	case FuToken::andAssign:
		return "'&='";
	case FuToken::orAssign:
		return "'|='";
	case FuToken::xorAssign:
		return "'^='";
	case FuToken::shiftLeftAssign:
		return "'<<='";
	case FuToken::shiftRightAssign:
		return "'>>='";
	case FuToken::increment:
		return "'++'";
	case FuToken::decrement:
		return "'--'";
	case FuToken::questionMark:
		return "'?'";
	case FuToken::colon:
		return "':'";
	case FuToken::fatArrow:
		return "'=>'";
	case FuToken::range:
		return "'..'";
	case FuToken::docRegular:
	case FuToken::docBullet:
	case FuToken::docBlank:
		return "'///'";
	case FuToken::abstract:
		return "'abstract'";
	case FuToken::assert:
		return "'assert'";
	case FuToken::break_:
		return "'break'";
	case FuToken::case_:
		return "'case'";
	case FuToken::class_:
		return "'class'";
	case FuToken::const_:
		return "'const'";
	case FuToken::continue_:
		return "'continue'";
	case FuToken::default_:
		return "'default'";
	case FuToken::do_:
		return "'do'";
	case FuToken::else_:
		return "'else'";
	case FuToken::enum_:
		return "'enum'";
	case FuToken::false_:
		return "'false'";
	case FuToken::for_:
		return "'for'";
	case FuToken::foreach:
		return "'foreach'";
	case FuToken::if_:
		return "'if'";
	case FuToken::in:
		return "'in'";
	case FuToken::internal:
		return "'internal'";
	case FuToken::is:
		return "'is'";
	case FuToken::lock_:
		return "'lock'";
	case FuToken::native:
		return "'native'";
	case FuToken::new_:
		return "'new'";
	case FuToken::null:
		return "'null'";
	case FuToken::override_:
		return "'override'";
	case FuToken::protected_:
		return "'protected'";
	case FuToken::public_:
		return "'public'";
	case FuToken::resource:
		return "'resource'";
	case FuToken::return_:
		return "'return'";
	case FuToken::sealed:
		return "'sealed'";
	case FuToken::static_:
		return "'static'";
	case FuToken::switch_:
		return "'switch'";
	case FuToken::throw_:
		return "'throw'";
	case FuToken::throws:
		return "'throws'";
	case FuToken::true_:
		return "'true'";
	case FuToken::virtual_:
		return "'virtual'";
	case FuToken::void_:
		return "'void'";
	case FuToken::when:
		return "'when'";
	case FuToken::while_:
		return "'while'";
	case FuToken::endOfLine:
		return "end-of-line";
	case FuToken::preUnknown:
		return "unknown preprocessor directive";
	case FuToken::preIf:
		return "'#if'";
	case FuToken::preElIf:
		return "'#elif'";
	case FuToken::preElse:
		return "'#else'";
	case FuToken::preEndIf:
		return "'#endif'";
	default:
		std::abort();
	}
}

bool FuLexer::check(FuToken expected) const
{
	if (see(expected))
		return true;
	reportError(std::format("Expected {}, got {}", tokenToString(expected), tokenToString(this->currentToken)));
	return false;
}

bool FuLexer::eatPre(FuToken token)
{
	if (see(token)) {
		nextPreToken();
		return true;
	}
	return false;
}

bool FuLexer::parsePrePrimary()
{
	if (eatPre(FuToken::exclamationMark))
		return !parsePrePrimary();
	if (eatPre(FuToken::leftParenthesis)) {
		bool result = parsePreOr();
		check(FuToken::rightParenthesis);
		nextPreToken();
		return result;
	}
	if (see(FuToken::id)) {
		bool result = this->preSymbols.contains(this->stringValue);
		nextPreToken();
		return result;
	}
	if (eatPre(FuToken::false_))
		return false;
	if (eatPre(FuToken::true_))
		return true;
	reportError("Invalid preprocessor expression");
	return false;
}

bool FuLexer::parsePreEquality()
{
	bool result = parsePrePrimary();
	for (;;) {
		if (eatPre(FuToken::equal))
			result = result == parsePrePrimary();
		else if (eatPre(FuToken::notEqual))
			result ^= parsePrePrimary();
		else
			return result;
	}
}

bool FuLexer::parsePreAnd()
{
	bool result = parsePreEquality();
	while (eatPre(FuToken::condAnd))
		result &= parsePreEquality();
	return result;
}

bool FuLexer::parsePreOr()
{
	bool result = parsePreAnd();
	while (eatPre(FuToken::condOr))
		result |= parsePreAnd();
	return result;
}

bool FuLexer::parsePreExpr()
{
	this->lineMode = true;
	nextPreToken();
	bool result = parsePreOr();
	check(FuToken::endOfLine);
	this->lineMode = false;
	return result;
}

void FuLexer::expectEndOfLine(std::string_view directive)
{
	this->lineMode = true;
	FuToken token = readPreToken();
	if (token != FuToken::endOfLine && token != FuToken::endOfFile)
		reportError(std::format("Unexpected characters after '{}'", directive));
	this->lineMode = false;
}

bool FuLexer::popPreElse(std::string_view directive)
{
	if (std::ssize(this->preElseStack) == 0) {
		reportError(std::format("'{}' with no matching '#if'", directive));
		return false;
	}
	if ([](std::stack<bool> &s) { bool top = s.top(); s.pop(); return top; }(this->preElseStack) && directive != "#endif")
		reportError(std::format("'{}' after '#else'", directive));
	return true;
}

void FuLexer::skipUnmet(FuPreState state)
{
	this->skippingUnmet = true;
	for (;;) {
		switch (readPreToken()) {
		case FuToken::endOfFile:
			reportError("Expected '#endif', got end-of-file");
			return;
		case FuToken::preIf:
			parsePreExpr();
			skipUnmet(FuPreState::already);
			break;
		case FuToken::preElIf:
			if (state == FuPreState::alreadyElse)
				reportError("'#elif' after '#else'");
			if (parsePreExpr() && state == FuPreState::notYet) {
				this->preElseStack.push(false);
				return;
			}
			break;
		case FuToken::preElse:
			if (state == FuPreState::alreadyElse)
				reportError("'#else' after '#else'");
			expectEndOfLine("#else");
			if (state == FuPreState::notYet) {
				this->preElseStack.push(true);
				return;
			}
			state = FuPreState::alreadyElse;
			break;
		case FuToken::preEndIf:
			expectEndOfLine("#endif");
			return;
		default:
			break;
		}
	}
}

FuToken FuLexer::readToken()
{
	for (;;) {
		this->skippingUnmet = false;
		FuToken token = readPreToken();
		bool matched;
		switch (token) {
		case FuToken::endOfFile:
			if (std::ssize(this->preElseStack) != 0)
				reportError("Expected '#endif', got end-of-file");
			return FuToken::endOfFile;
		case FuToken::preIf:
			if (parsePreExpr())
				this->preElseStack.push(false);
			else
				skipUnmet(FuPreState::notYet);
			break;
		case FuToken::preElIf:
			matched = popPreElse("#elif");
			parsePreExpr();
			if (matched)
				skipUnmet(FuPreState::already);
			break;
		case FuToken::preElse:
			matched = popPreElse("#else");
			expectEndOfLine("#else");
			if (matched)
				skipUnmet(FuPreState::alreadyElse);
			break;
		case FuToken::preEndIf:
			popPreElse("#endif");
			expectEndOfLine("#endif");
			break;
		default:
			return token;
		}
	}
}

FuToken FuLexer::nextToken()
{
	FuToken token = this->currentToken;
	this->currentToken = readToken();
	return token;
}

bool FuLexer::eat(FuToken token)
{
	if (see(token)) {
		nextToken();
		return true;
	}
	return false;
}

bool FuLexer::expect(FuToken expected)
{
	bool found = check(expected);
	nextToken();
	return found;
}

void FuLexer::expectOrSkip(FuToken expected)
{
	if (check(expected))
		nextToken();
	else {
		do
			nextToken();
		while (!see(FuToken::endOfFile) && !eat(expected));
	}
}

void FuVisitor::visitOptionalStatement(const FuStatement * statement)
{
	if (statement != nullptr)
		statement->acceptStatement(this);
}

int FuStatement::getLocLength() const
{
	return 0;
}

bool FuExpr::completesNormally() const
{
	return true;
}

std::string FuExpr::toString() const
{
	std::abort();
}

bool FuExpr::isIndexing() const
{
	return false;
}

bool FuExpr::isLiteralZero() const
{
	return false;
}

bool FuExpr::isConstEnum() const
{
	return false;
}

int FuExpr::intValue() const
{
	std::abort();
}

void FuExpr::accept(FuVisitor * visitor, FuPriority parent) const
{
	std::abort();
}

void FuExpr::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitExpr(this);
}

bool FuExpr::isReferenceTo(const FuSymbol * symbol) const
{
	return false;
}

bool FuExpr::isNewString(bool substringOffset) const
{
	return false;
}

bool FuExpr::isUnique() const
{
	return false;
}

void FuExpr::setShared() const
{
}

int FuName::getLocLength() const
{
	return std::ssize(this->name);
}

const FuSymbol * FuSymbol::getSymbol() const
{
	return this;
}

std::string FuSymbol::toString() const
{
	return this->name;
}

int FuScope::count() const
{
	return std::ssize(this->dict);
}

bool FuScope::contains(const FuSymbol * symbol) const
{
	return this->dict.count(symbol->name) != 0;
}

std::shared_ptr<FuSymbol> FuScope::tryLookup(std::string_view name, bool global) const
{
	for (const FuScope * scope = this; scope != nullptr && (global || !(dynamic_cast<const FuProgram *>(scope) || dynamic_cast<const FuSystem *>(scope))); scope = scope->parent) {
		if (scope->dict.count(name) != 0)
			return scope->dict.find(name)->second;
	}
	return nullptr;
}

void FuScope::addToList(std::shared_ptr<FuSymbol> symbol)
{
	symbol->next = nullptr;
	symbol->parent = this;
	if (this->first == nullptr)
		this->first = symbol.get();
	else
		this->last->next = symbol.get();
	this->last = symbol.get();
}

void FuScope::add(std::shared_ptr<FuSymbol> symbol)
{
	this->dict[symbol->name] = symbol;
	addToList(symbol);
}

bool FuScope::encloses(const FuSymbol * symbol) const
{
	for (const FuScope * scope = symbol->parent; scope != nullptr; scope = scope->parent) {
		if (scope == this)
			return true;
	}
	return false;
}

void FuAggregateInitializer::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitAggregateInitializer(this);
}

std::string FuLiteral::getLiteralString() const
{
	std::abort();
}

int FuLiteralNull::getLocLength() const
{
	return 4;
}

bool FuLiteralNull::isDefaultValue() const
{
	return true;
}

void FuLiteralNull::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitLiteralNull();
}

bool FuLiteralNull::isUnique() const
{
	return true;
}

std::string FuLiteralNull::toString() const
{
	return "null";
}

int FuLiteralFalse::getLocLength() const
{
	return 5;
}

bool FuLiteralFalse::isDefaultValue() const
{
	return true;
}

void FuLiteralFalse::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitLiteralFalse();
}

std::string FuLiteralFalse::toString() const
{
	return "false";
}

int FuLiteralTrue::getLocLength() const
{
	return 4;
}

bool FuLiteralTrue::isDefaultValue() const
{
	return false;
}

void FuLiteralTrue::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitLiteralTrue();
}

std::string FuLiteralTrue::toString() const
{
	return "true";
}

bool FuLiteralLong::isLiteralZero() const
{
	return this->value == 0;
}

int FuLiteralLong::intValue() const
{
	return static_cast<int>(this->value);
}

bool FuLiteralLong::isDefaultValue() const
{
	return this->value == 0;
}

void FuLiteralLong::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitLiteralLong(this->value);
}

std::string FuLiteralLong::getLiteralString() const
{
	return std::format("{}", this->value);
}

std::string FuLiteralLong::toString() const
{
	return std::format("{}", this->value);
}

std::shared_ptr<FuLiteralChar> FuLiteralChar::new_(int value, int loc)
{
	std::shared_ptr<FuLiteralChar> futemp0 = std::make_shared<FuLiteralChar>();
	futemp0->loc = loc;
	futemp0->type = FuRangeType::new_(value, value);
	futemp0->value = value;
	return futemp0;
}

int FuLiteralChar::getLocLength() const
{
	return this->value >= 65536 || this->value == '\n' || this->value == '\r' || this->value == '\t' || this->value == '\\' || this->value == '\'' ? 4 : 3;
}

void FuLiteralChar::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitLiteralChar(static_cast<int>(this->value));
}

bool FuLiteralDouble::isDefaultValue() const
{
	return this->value == 0 && 1.0 / this->value > 0;
}

void FuLiteralDouble::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitLiteralDouble(this->value);
}

std::string FuLiteralDouble::getLiteralString() const
{
	return std::format("{}", this->value);
}

std::string FuLiteralDouble::toString() const
{
	return std::format("{}", this->value);
}

bool FuLiteralString::isDefaultValue() const
{
	return false;
}

int FuLiteralString::getLocLength() const
{
	return std::ssize(this->value) + 2;
}

void FuLiteralString::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitLiteralString(this->value);
}

std::string FuLiteralString::getLiteralString() const
{
	return this->value;
}

std::string FuLiteralString::toString() const
{
	return std::format("\"{}\"", this->value);
}

int FuLiteralString::getAsciiLength() const
{
	int length = 0;
	bool escaped = false;
	for (int c : this->value) {
		if (c < 0 || c > 127)
			return -1;
		if (!escaped && c == '\\')
			escaped = true;
		else {
			length++;
			escaped = false;
		}
	}
	return length;
}

int FuLiteralString::getAsciiAt(int i) const
{
	bool escaped = false;
	for (int c : this->value) {
		if (c < 0 || c > 127)
			return -1;
		if (!escaped && c == '\\')
			escaped = true;
		else if (i == 0)
			return escaped ? FuLexer::getEscapedChar(c) : c;
		else {
			i--;
			escaped = false;
		}
	}
	return -1;
}

int FuLiteralString::getOneAscii() const
{
	switch (std::ssize(this->value)) {
	case 1:
		{
			int c = this->value[0];
			return c >= 0 && c <= 127 ? c : -1;
		}
	case 2:
		return this->value[0] == '\\' ? FuLexer::getEscapedChar(this->value[1]) : -1;
	default:
		return -1;
	}
}

void FuInterpolatedString::addPart(std::string_view prefix, std::shared_ptr<FuExpr> arg, std::shared_ptr<FuExpr> widthExpr, int format, int precision)
{
	this->parts.emplace_back();
	FuInterpolatedPart * part = &static_cast<FuInterpolatedPart &>(this->parts.back());
	part->prefix = prefix;
	part->argument = arg;
	part->widthExpr = widthExpr;
	part->format = format;
	part->precision = precision;
}

int FuInterpolatedString::getLocLength() const
{
	return 2;
}

void FuInterpolatedString::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitInterpolatedString(this, parent);
}

bool FuInterpolatedString::isNewString(bool substringOffset) const
{
	return true;
}

int FuImplicitEnumValue::intValue() const
{
	return this->value;
}

bool FuSymbolReference::isConstEnum() const
{
	return dynamic_cast<const FuEnum *>(this->symbol->parent);
}

int FuSymbolReference::intValue() const
{
	const FuConst * konst = static_cast<const FuConst *>(this->symbol);
	return konst->value->intValue();
}

void FuSymbolReference::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitSymbolReference(this, parent);
}

bool FuSymbolReference::isReferenceTo(const FuSymbol * symbol) const
{
	return this->symbol == symbol;
}

bool FuSymbolReference::isNewString(bool substringOffset) const
{
	return this->symbol->id == FuId::matchValue;
}

void FuSymbolReference::setShared() const
{
	const FuNamedValue * varOrField;
	FuDynamicPtrType * dynamic;
	if ((varOrField = dynamic_cast<const FuNamedValue *>(this->symbol)) && (dynamic = dynamic_cast<FuDynamicPtrType *>(varOrField->type.get())))
		dynamic->unique = false;
}

const FuSymbol * FuSymbolReference::getSymbol() const
{
	return this->symbol;
}

std::string FuSymbolReference::toString() const
{
	return std::string(this->left != nullptr ? std::format("{}.{}", this->left->toString(), this->name) : this->name);
}

int FuUnaryExpr::getLocLength() const
{
	switch (this->op) {
	case FuToken::increment:
	case FuToken::decrement:
		return 2;
	case FuToken::minus:
	case FuToken::tilde:
	case FuToken::exclamationMark:
	case FuToken::hash:
	case FuToken::questionMark:
		return 1;
	case FuToken::new_:
		return 3;
	case FuToken::resource:
		return 8;
	default:
		std::abort();
	}
}

bool FuPrefixExpr::isConstEnum() const
{
	return dynamic_cast<const FuEnumFlags *>(this->type.get()) && this->inner->isConstEnum();
}

int FuPrefixExpr::intValue() const
{
	assert(this->op == FuToken::tilde);
	return ~this->inner->intValue();
}

void FuPrefixExpr::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitPrefixExpr(this, parent);
}

bool FuPrefixExpr::isUnique() const
{
	return this->op == FuToken::new_ && !dynamic_cast<const FuAggregateInitializer *>(this->inner.get());
}

void FuPostfixExpr::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitPostfixExpr(this, parent);
}

int FuBinaryExpr::getLocLength() const
{
	switch (this->op) {
	case FuToken::plus:
	case FuToken::minus:
	case FuToken::asterisk:
	case FuToken::slash:
	case FuToken::mod:
	case FuToken::less:
	case FuToken::greater:
	case FuToken::and_:
	case FuToken::or_:
	case FuToken::xor_:
	case FuToken::assign:
	case FuToken::leftBracket:
	case FuToken::leftBrace:
		return 1;
	case FuToken::shiftLeft:
	case FuToken::shiftRight:
	case FuToken::lessOrEqual:
	case FuToken::greaterOrEqual:
	case FuToken::equal:
	case FuToken::notEqual:
	case FuToken::condAnd:
	case FuToken::condOr:
	case FuToken::addAssign:
	case FuToken::subAssign:
	case FuToken::mulAssign:
	case FuToken::divAssign:
	case FuToken::modAssign:
	case FuToken::andAssign:
	case FuToken::orAssign:
	case FuToken::xorAssign:
	case FuToken::range:
	case FuToken::is:
		return 2;
	case FuToken::shiftLeftAssign:
	case FuToken::shiftRightAssign:
		return 3;
	case FuToken::when:
		return 0;
	default:
		std::abort();
	}
}

bool FuBinaryExpr::isIndexing() const
{
	return this->op == FuToken::leftBracket;
}

bool FuBinaryExpr::isConstEnum() const
{
	switch (this->op) {
	case FuToken::and_:
	case FuToken::or_:
	case FuToken::xor_:
		return dynamic_cast<const FuEnumFlags *>(this->type.get()) && this->left->isConstEnum() && this->right->isConstEnum();
	default:
		return false;
	}
}

int FuBinaryExpr::intValue() const
{
	switch (this->op) {
	case FuToken::and_:
		return this->left->intValue() & this->right->intValue();
	case FuToken::or_:
		return this->left->intValue() | this->right->intValue();
	case FuToken::xor_:
		return this->left->intValue() ^ this->right->intValue();
	default:
		std::abort();
	}
}

void FuBinaryExpr::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitBinaryExpr(this, parent);
}

bool FuBinaryExpr::isNewString(bool substringOffset) const
{
	return this->op == FuToken::plus && this->type->id == FuId::stringStorageType;
}

bool FuBinaryExpr::isRel() const
{
	switch (this->op) {
	case FuToken::equal:
	case FuToken::notEqual:
	case FuToken::less:
	case FuToken::lessOrEqual:
	case FuToken::greater:
	case FuToken::greaterOrEqual:
		return true;
	default:
		return false;
	}
}

bool FuBinaryExpr::isAssign() const
{
	switch (this->op) {
	case FuToken::assign:
	case FuToken::addAssign:
	case FuToken::subAssign:
	case FuToken::mulAssign:
	case FuToken::divAssign:
	case FuToken::modAssign:
	case FuToken::shiftLeftAssign:
	case FuToken::shiftRightAssign:
	case FuToken::andAssign:
	case FuToken::orAssign:
	case FuToken::xorAssign:
		return true;
	default:
		return false;
	}
}

std::string_view FuBinaryExpr::getOpString() const
{
	switch (this->op) {
	case FuToken::plus:
		return "+";
	case FuToken::minus:
		return "-";
	case FuToken::asterisk:
		return "*";
	case FuToken::slash:
		return "/";
	case FuToken::mod:
		return "%";
	case FuToken::shiftLeft:
		return "<<";
	case FuToken::shiftRight:
		return ">>";
	case FuToken::less:
		return "<";
	case FuToken::lessOrEqual:
		return "<=";
	case FuToken::greater:
		return ">";
	case FuToken::greaterOrEqual:
		return ">=";
	case FuToken::equal:
		return "==";
	case FuToken::notEqual:
		return "!=";
	case FuToken::and_:
		return "&";
	case FuToken::or_:
		return "|";
	case FuToken::xor_:
		return "^";
	case FuToken::condAnd:
		return "&&";
	case FuToken::condOr:
		return "||";
	case FuToken::assign:
		return "=";
	case FuToken::addAssign:
		return "+=";
	case FuToken::subAssign:
		return "-=";
	case FuToken::mulAssign:
		return "*=";
	case FuToken::divAssign:
		return "/=";
	case FuToken::modAssign:
		return "%=";
	case FuToken::shiftLeftAssign:
		return "<<=";
	case FuToken::shiftRightAssign:
		return ">>=";
	case FuToken::andAssign:
		return "&=";
	case FuToken::orAssign:
		return "|=";
	case FuToken::xorAssign:
		return "^=";
	default:
		std::abort();
	}
}

std::string FuBinaryExpr::toString() const
{
	return std::string(this->op == FuToken::leftBracket ? std::format("{}[{}]", this->left->toString(), this->right->toString()) : std::format("({} {} {})", this->left->toString(), getOpString(), this->right->toString()));
}

int FuSelectExpr::getLocLength() const
{
	return 1;
}

void FuSelectExpr::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitSelectExpr(this, parent);
}

bool FuSelectExpr::isUnique() const
{
	return this->onTrue->isUnique() && this->onFalse->isUnique();
}

void FuSelectExpr::setShared() const
{
	this->onTrue->setShared();
	this->onFalse->setShared();
}

std::string FuSelectExpr::toString() const
{
	return std::format("({} ? {} : {})", this->cond->toString(), this->onTrue->toString(), this->onFalse->toString());
}

void FuCallExpr::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitCallExpr(this, parent);
}

bool FuCallExpr::isNewString(bool substringOffset) const
{
	return this->type->id == FuId::stringStorageType && this->method->symbol->id != FuId::listLast && this->method->symbol->id != FuId::queuePeek && this->method->symbol->id != FuId::stackPeek && (substringOffset || this->method->symbol->id != FuId::stringSubstring || std::ssize(this->arguments) != 1);
}

void FuLambdaExpr::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitLambdaExpr(this);
}

bool FuCondCompletionStatement::completesNormally() const
{
	return this->completesNormallyValue;
}

void FuCondCompletionStatement::setCompletesNormally(bool value)
{
	this->completesNormallyValue = value;
}

void FuBlock::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitBlock(this);
}

int FuAssert::getLocLength() const
{
	return 6;
}

bool FuAssert::completesNormally() const
{
	return !dynamic_cast<const FuLiteralFalse *>(this->cond.get());
}

void FuAssert::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitAssert(this);
}

int FuBreak::getLocLength() const
{
	return 5;
}

bool FuBreak::completesNormally() const
{
	return false;
}

void FuBreak::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitBreak(this);
}

int FuContinue::getLocLength() const
{
	return 8;
}

bool FuContinue::completesNormally() const
{
	return false;
}

void FuContinue::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitContinue(this);
}

int FuDoWhile::getLocLength() const
{
	return 2;
}

void FuDoWhile::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitDoWhile(this);
}

int FuFor::getLocLength() const
{
	return 3;
}

void FuFor::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitFor(this);
}

int FuForeach::getLocLength() const
{
	return 7;
}

void FuForeach::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitForeach(this);
}

FuVar * FuForeach::getVar() const
{
	FuVar * result = static_cast<FuVar *>(this->first);
	return result;
}

FuVar * FuForeach::getValueVar() const
{
	return this->getVar()->nextVar();
}

int FuIf::getLocLength() const
{
	return 2;
}

void FuIf::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitIf(this);
}

int FuLock::getLocLength() const
{
	return 4;
}

bool FuLock::completesNormally() const
{
	return this->body->completesNormally();
}

void FuLock::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitLock(this);
}

int FuNative::getLocLength() const
{
	return 6;
}

bool FuNative::completesNormally() const
{
	return true;
}

void FuNative::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitNative(this);
}

const FuMember * FuNative::getFollowingMember() const
{
	for (const FuSymbol * symbol = this->next; symbol != nullptr; symbol = symbol->next) {
		if (const FuMember *member = dynamic_cast<const FuMember *>(symbol))
			return member;
	}
	return nullptr;
}

int FuReturn::getLocLength() const
{
	return 6;
}

bool FuReturn::completesNormally() const
{
	return false;
}

void FuReturn::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitReturn(this);
}

int FuSwitch::getLocLength() const
{
	return 6;
}

void FuSwitch::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitSwitch(this);
}

bool FuSwitch::isTypeMatching() const
{
	const FuClassType * klass;
	return (klass = dynamic_cast<const FuClassType *>(this->value->type.get())) && klass->class_->id != FuId::stringClass;
}

bool FuSwitch::hasWhen() const
{
	return std::any_of(this->cases.begin(), this->cases.end(), [](const FuCase &kase) { return std::any_of(kase.values.begin(), kase.values.end(), [](const std::shared_ptr<FuExpr> &value) { const FuBinaryExpr * when1;
	return (when1 = dynamic_cast<const FuBinaryExpr *>(value.get())) && when1->op == FuToken::when; }); });
}

int FuSwitch::lengthWithoutTrailingBreak(const std::vector<std::shared_ptr<FuStatement>> * body)
{
	int length = std::ssize(*body);
	if (length > 0 && dynamic_cast<const FuBreak *>((*body)[length - 1].get()))
		length--;
	return length;
}

bool FuSwitch::hasDefault() const
{
	return lengthWithoutTrailingBreak(&this->defaultBody) > 0;
}

bool FuSwitch::hasBreak(const FuStatement * statement)
{
	if (dynamic_cast<const FuBreak *>(statement))
		return true;
	else if (const FuIf *ifStatement = dynamic_cast<const FuIf *>(statement))
		return hasBreak(ifStatement->onTrue.get()) || (ifStatement->onFalse != nullptr && hasBreak(ifStatement->onFalse.get()));
	else if (const FuBlock *block = dynamic_cast<const FuBlock *>(statement))
		return std::any_of(block->statements.begin(), block->statements.end(), [](const std::shared_ptr<FuStatement> &child) { return hasBreak(child.get()); });
	else
		return false;
}

bool FuSwitch::hasEarlyBreak(const std::vector<std::shared_ptr<FuStatement>> * body)
{
	int length = lengthWithoutTrailingBreak(body);
	for (int i = 0; i < length; i++) {
		if (hasBreak((*body)[i].get()))
			return true;
	}
	return false;
}

bool FuSwitch::listHasContinue(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	return std::any_of(statements->begin(), statements->end(), [](const std::shared_ptr<FuStatement> &statement) { return hasContinue(statement.get()); });
}

bool FuSwitch::hasContinue(const FuStatement * statement)
{
	if (dynamic_cast<const FuContinue *>(statement))
		return true;
	else if (const FuIf *ifStatement = dynamic_cast<const FuIf *>(statement))
		return hasContinue(ifStatement->onTrue.get()) || (ifStatement->onFalse != nullptr && hasContinue(ifStatement->onFalse.get()));
	else if (const FuSwitch *switchStatement = dynamic_cast<const FuSwitch *>(statement))
		return std::any_of(switchStatement->cases.begin(), switchStatement->cases.end(), [](const FuCase &kase) { return listHasContinue(&kase.body); }) || listHasContinue(&switchStatement->defaultBody);
	else if (const FuBlock *block = dynamic_cast<const FuBlock *>(statement))
		return listHasContinue(&block->statements);
	else
		return false;
}

bool FuSwitch::hasEarlyBreakAndContinue(const std::vector<std::shared_ptr<FuStatement>> * body)
{
	return hasEarlyBreak(body) && listHasContinue(body);
}

int FuThrow::getLocLength() const
{
	return 5;
}

bool FuThrow::completesNormally() const
{
	return false;
}

void FuThrow::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitThrow(this);
}

int FuWhile::getLocLength() const
{
	return 5;
}

void FuWhile::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitWhile(this);
}

std::string FuType::getArraySuffix() const
{
	return "";
}

bool FuType::isAssignableFrom(const FuType * right) const
{
	return this == right;
}

bool FuType::equalsType(const FuType * right) const
{
	return this == right;
}

bool FuType::isArray() const
{
	return false;
}

bool FuType::isFinal() const
{
	return false;
}

const FuType * FuType::getBaseType() const
{
	return this;
}

const FuType * FuType::getStorageType() const
{
	return this;
}

const FuClassType * FuType::asClassType() const
{
	const FuClassType * klass = static_cast<const FuClassType *>(this);
	return klass;
}

bool FuIntegerType::isAssignableFrom(const FuType * right) const
{
	return dynamic_cast<const FuIntegerType *>(right) || right->id == FuId::floatIntType;
}

void FuRangeType::addMinMaxValue(std::shared_ptr<FuRangeType> target, std::string_view name, int value)
{
	std::shared_ptr<FuRangeType> futemp0 = std::make_shared<FuRangeType>();
	futemp0->min = value;
	futemp0->max = value;
	std::shared_ptr<FuRangeType> type = target->min == target->max ? target : futemp0;
	std::shared_ptr<FuLiteralLong> futemp1 = std::make_shared<FuLiteralLong>();
	futemp1->type = type;
	futemp1->value = value;
	std::shared_ptr<FuConst> futemp2 = std::make_shared<FuConst>();
	futemp2->visibility = FuVisibility::public_;
	futemp2->name = name;
	futemp2->value = futemp1;
	futemp2->visitStatus = FuVisitStatus::done;
	target->add(futemp2);
}

std::shared_ptr<FuRangeType> FuRangeType::new_(int min, int max)
{
	assert(min <= max);
	std::shared_ptr<FuRangeType> result = std::make_shared<FuRangeType>();
	result->id = min >= 0 && max <= 255 ? FuId::byteRange : min >= -128 && max <= 127 ? FuId::sByteRange : min >= -32768 && max <= 32767 ? FuId::shortRange : min >= 0 && max <= 65535 ? FuId::uShortRange : FuId::intType;
	result->min = min;
	result->max = max;
	addMinMaxValue(result, "MinValue", min);
	addMinMaxValue(result, "MaxValue", max);
	return result;
}

std::string FuRangeType::toString() const
{
	return std::string(this->min == this->max ? std::format("{}", this->min) : std::format("({} .. {})", this->min, this->max));
}

bool FuRangeType::isAssignableFrom(const FuType * right) const
{
	if (const FuRangeType *range = dynamic_cast<const FuRangeType *>(right))
		return this->min <= range->max && this->max >= range->min;
	else if (dynamic_cast<const FuIntegerType *>(right))
		return true;
	else
		return right->id == FuId::floatIntType;
}

bool FuRangeType::equalsType(const FuType * right) const
{
	const FuRangeType * that;
	return (that = dynamic_cast<const FuRangeType *>(right)) && this->min == that->min && this->max == that->max;
}

int FuRangeType::getMask(int v)
{
	v |= v >> 1;
	v |= v >> 2;
	v |= v >> 4;
	v |= v >> 8;
	v |= v >> 16;
	return v;
}

int FuRangeType::getVariableBits() const
{
	return getMask(this->min ^ this->max);
}

bool FuFloatingType::isAssignableFrom(const FuType * right) const
{
	return dynamic_cast<const FuNumericType *>(right);
}

bool FuNamedValue::isAssignableStorage() const
{
	return dynamic_cast<const FuStorageType *>(this->type.get()) && !dynamic_cast<const FuArrayStorageType *>(this->type.get()) && dynamic_cast<const FuLiteralNull *>(this->value.get());
}
FuMember::FuMember()
{
}

std::shared_ptr<FuVar> FuVar::new_(std::shared_ptr<FuType> type, std::string_view name, std::shared_ptr<FuExpr> defaultValue)
{
	std::shared_ptr<FuVar> futemp0 = std::make_shared<FuVar>();
	futemp0->type = type;
	futemp0->name = name;
	futemp0->value = defaultValue;
	return futemp0;
}

void FuVar::accept(FuVisitor * visitor, FuPriority parent) const
{
	visitor->visitVar(this);
}

FuVar * FuVar::nextVar() const
{
	FuVar * def = static_cast<FuVar *>(this->next);
	return def;
}

void FuConst::acceptStatement(FuVisitor * visitor) const
{
	visitor->visitConst(this);
}

bool FuConst::isStatic() const
{
	return true;
}

bool FuField::isStatic() const
{
	return false;
}

bool FuProperty::isStatic() const
{
	return false;
}

std::shared_ptr<FuProperty> FuProperty::new_(std::shared_ptr<FuType> type, FuId id, std::string_view name)
{
	std::shared_ptr<FuProperty> futemp0 = std::make_shared<FuProperty>();
	futemp0->visibility = FuVisibility::public_;
	futemp0->type = type;
	futemp0->id = id;
	futemp0->name = name;
	return futemp0;
}

bool FuStaticProperty::isStatic() const
{
	return true;
}

std::shared_ptr<FuStaticProperty> FuStaticProperty::new_(std::shared_ptr<FuType> type, FuId id, std::string_view name)
{
	std::shared_ptr<FuStaticProperty> futemp0 = std::make_shared<FuStaticProperty>();
	futemp0->visibility = FuVisibility::public_;
	futemp0->type = type;
	futemp0->id = id;
	futemp0->name = name;
	return futemp0;
}

bool FuMethodBase::isStatic() const
{
	return false;
}

void FuMethodBase::addThis(const FuClass * klass, bool isMutator)
{
	std::shared_ptr<FuClassType> type = isMutator ? std::make_shared<FuReadWriteClassType>() : std::make_shared<FuClassType>();
	type->class_ = klass;
	this->parameters.add(FuVar::new_(type, "this"));
}

bool FuMethodBase::isMutator() const
{
	return dynamic_cast<const FuReadWriteClassType *>(this->parameters.first->type.get());
}

std::shared_ptr<FuMethod> FuMethod::new_(const FuClass * klass, FuVisibility visibility, FuCallType callType, std::shared_ptr<FuType> type, FuId id, std::string_view name, bool isMutator, std::shared_ptr<FuVar> param0, std::shared_ptr<FuVar> param1, std::shared_ptr<FuVar> param2, std::shared_ptr<FuVar> param3)
{
	std::shared_ptr<FuMethod> result = std::make_shared<FuMethod>();
	result->visibility = visibility;
	result->callType = callType;
	result->type = type;
	result->id = id;
	result->name = name;
	if (callType != FuCallType::static_)
		result->addThis(klass, isMutator);
	if (param0 != nullptr) {
		result->parameters.add(param0);
		if (param1 != nullptr) {
			result->parameters.add(param1);
			if (param2 != nullptr) {
				result->parameters.add(param2);
				if (param3 != nullptr)
					result->parameters.add(param3);
			}
		}
	}
	return result;
}

bool FuMethod::isStatic() const
{
	return this->callType == FuCallType::static_;
}

bool FuMethod::isAbstractOrVirtual() const
{
	return this->callType == FuCallType::abstract || this->callType == FuCallType::virtual_;
}

bool FuMethod::isAbstractVirtualOrOverride() const
{
	return this->callType == FuCallType::abstract || this->callType == FuCallType::virtual_ || this->callType == FuCallType::override_;
}

std::string_view FuMethod::callTypeToString(FuCallType callType)
{
	switch (callType) {
	case FuCallType::static_:
		return "static";
	case FuCallType::abstract:
		return "abstract";
	case FuCallType::virtual_:
		return "virtual";
	case FuCallType::override_:
		return "override";
	case FuCallType::sealed:
		return "sealed";
	default:
		std::abort();
	}
}

FuVar * FuMethod::firstParameter() const
{
	FuVar * first = static_cast<FuVar *>(this->parameters.first);
	return isStatic() ? first : first->nextVar();
}

int FuMethod::getParametersCount() const
{
	int c = this->parameters.count();
	return isStatic() ? c : c - 1;
}

const FuMethod * FuMethod::getDeclaringMethod() const
{
	const FuMethod * method = this;
	while (method->callType == FuCallType::override_) {
		const FuMethod * baseMethod = static_cast<const FuMethod *>(method->parent->parent->tryLookup(method->name, false).get());
		method = baseMethod;
	}
	return method;
}
FuMethodGroup::FuMethodGroup()
{
}

bool FuMethodGroup::isStatic() const
{
	std::abort();
}

std::shared_ptr<FuMethodGroup> FuMethodGroup::new_(std::shared_ptr<FuMethod> method0, std::shared_ptr<FuMethod> method1)
{
	std::shared_ptr<FuMethodGroup> result = std::make_shared<FuMethodGroup>();
	result->visibility = method0->visibility;
	result->name = method0->name;
	result->methods[0] = method0;
	result->methods[1] = method1;
	return result;
}

const FuSymbol * FuEnum::getFirstValue() const
{
	const FuSymbol * symbol = this->first;
	while (!dynamic_cast<const FuConst *>(symbol))
		symbol = symbol->next;
	return symbol;
}

void FuEnum::acceptValues(FuVisitor * visitor) const
{
	const FuConst * previous = nullptr;
	for (const FuSymbol * symbol = this->first; symbol != nullptr; symbol = symbol->next) {
		if (const FuConst *konst = dynamic_cast<const FuConst *>(symbol)) {
			visitor->visitEnumValue(konst, previous);
			previous = konst;
		}
	}
}

bool FuClass::hasBaseClass() const
{
	return !this->baseClass.name.empty();
}

bool FuClass::addsVirtualMethods() const
{
	for (const FuSymbol * symbol = this->first; symbol != nullptr; symbol = symbol->next) {
		const FuMethod * method;
		if ((method = dynamic_cast<const FuMethod *>(symbol)) && method->isAbstractOrVirtual())
			return true;
	}
	return false;
}

std::shared_ptr<FuClass> FuClass::new_(FuCallType callType, FuId id, std::string_view name, int typeParameterCount)
{
	std::shared_ptr<FuClass> futemp0 = std::make_shared<FuClass>();
	futemp0->callType = callType;
	futemp0->id = id;
	futemp0->name = name;
	futemp0->typeParameterCount = typeParameterCount;
	return futemp0;
}

void FuClass::addMethod(std::shared_ptr<FuType> type, FuId id, std::string_view name, bool isMutator, std::shared_ptr<FuVar> param0, std::shared_ptr<FuVar> param1, std::shared_ptr<FuVar> param2, std::shared_ptr<FuVar> param3)
{
	add(FuMethod::new_(this, FuVisibility::public_, FuCallType::normal, type, id, name, isMutator, param0, param1, param2, param3));
}

void FuClass::addStaticMethod(std::shared_ptr<FuType> type, FuId id, std::string_view name, std::shared_ptr<FuVar> param0, std::shared_ptr<FuVar> param1, std::shared_ptr<FuVar> param2)
{
	add(FuMethod::new_(this, FuVisibility::public_, FuCallType::static_, type, id, name, false, param0, param1, param2));
}

void FuClass::addNative(std::shared_ptr<FuNative> nat)
{
	addToList(nat);
	this->natives.push_back(nat);
}

bool FuClass::isSameOrBaseOf(const FuClass * derived) const
{
	while (derived != this) {
		if (const FuClass *parent = dynamic_cast<const FuClass *>(derived->parent))
			derived = parent;
		else
			return false;
	}
	return true;
}

bool FuClass::hasToString() const
{
	const FuMethod * method;
	return (method = dynamic_cast<const FuMethod *>(tryLookup("ToString", false).get())) && method->id == FuId::classToString;
}

bool FuClass::addsToString() const
{
	const FuMethod * method;
	return this->dict.count("ToString") != 0 && (method = dynamic_cast<const FuMethod *>(this->dict.find("ToString")->second.get())) && method->id == FuId::classToString && method->callType != FuCallType::override_ && method->callType != FuCallType::sealed;
}

std::shared_ptr<FuType> FuClassType::getElementType() const
{
	return this->typeArg0;
}

const FuType * FuClassType::getKeyType() const
{
	return this->typeArg0.get();
}

std::shared_ptr<FuType> FuClassType::getValueType() const
{
	return this->typeArg1;
}

bool FuClassType::isArray() const
{
	return this->class_->id == FuId::arrayPtrClass || this->class_->id == FuId::arrayStorageClass;
}

const FuType * FuClassType::getBaseType() const
{
	return isArray() ? getElementType()->getBaseType() : this;
}

bool FuClassType::equalTypeArguments(const FuClassType * right) const
{
	switch (this->class_->typeParameterCount) {
	case 0:
		return true;
	case 1:
		return this->typeArg0->equalsType(right->typeArg0.get());
	case 2:
		return this->typeArg0->equalsType(right->typeArg0.get()) && this->typeArg1->equalsType(right->typeArg1.get());
	default:
		std::abort();
	}
}

bool FuClassType::isAssignableFromClass(const FuClassType * right) const
{
	return this->class_->isSameOrBaseOf(right->class_) && equalTypeArguments(right);
}

bool FuClassType::isAssignableFrom(const FuType * right) const
{
	const FuClassType * rightClass;
	return (this->nullable && right->id == FuId::nullType) || ((rightClass = dynamic_cast<const FuClassType *>(right)) && isAssignableFromClass(rightClass));
}

bool FuClassType::equalsTypeInternal(const FuClassType * that) const
{
	return this->nullable == that->nullable && this->class_ == that->class_ && equalTypeArguments(that);
}

bool FuClassType::equalsType(const FuType * right) const
{
	const FuClassType * that;
	return (that = dynamic_cast<const FuClassType *>(right)) && !dynamic_cast<const FuReadWriteClassType *>(right) && equalsTypeInternal(that);
}

std::string FuClassType::getArraySuffix() const
{
	return std::string(isArray() ? "[]" : "");
}

std::string_view FuClassType::getClassSuffix() const
{
	return "";
}

std::string_view FuClassType::getNullableSuffix() const
{
	return this->nullable ? "?" : "";
}

std::string FuClassType::toString() const
{
	if (isArray())
		return std::format("{}{}{}{}", getElementType()->getBaseType()->toString(), getArraySuffix(), getNullableSuffix(), getElementType()->getArraySuffix());
	switch (this->class_->typeParameterCount) {
	case 0:
		return std::format("{}{}{}", this->class_->name, getClassSuffix(), getNullableSuffix());
	case 1:
		return std::format("{}<{}>{}{}", this->class_->name, this->typeArg0->toString(), getClassSuffix(), getNullableSuffix());
	case 2:
		return std::format("{}<{}, {}>{}{}", this->class_->name, this->typeArg0->toString(), this->typeArg1->toString(), getClassSuffix(), getNullableSuffix());
	default:
		std::abort();
	}
}

bool FuReadWriteClassType::isAssignableFrom(const FuType * right) const
{
	const FuReadWriteClassType * rightClass;
	return (this->nullable && right->id == FuId::nullType) || ((rightClass = dynamic_cast<const FuReadWriteClassType *>(right)) && isAssignableFromClass(rightClass));
}

bool FuReadWriteClassType::equalsType(const FuType * right) const
{
	const FuReadWriteClassType * that;
	return (that = dynamic_cast<const FuReadWriteClassType *>(right)) && !dynamic_cast<const FuOwningType *>(right) && equalsTypeInternal(that);
}

std::string FuReadWriteClassType::getArraySuffix() const
{
	return std::string(isArray() ? "[]!" : "");
}

std::string_view FuReadWriteClassType::getClassSuffix() const
{
	return "!";
}

bool FuStorageType::isFinal() const
{
	return this->class_->id != FuId::matchClass;
}

bool FuStorageType::isAssignableFrom(const FuType * right) const
{
	const FuStorageType * rightClass;
	return (rightClass = dynamic_cast<const FuStorageType *>(right)) && this->class_ == rightClass->class_ && equalTypeArguments(rightClass);
}

bool FuStorageType::equalsType(const FuType * right) const
{
	const FuStorageType * that;
	return (that = dynamic_cast<const FuStorageType *>(right)) && equalsTypeInternal(that);
}

std::string_view FuStorageType::getClassSuffix() const
{
	return "()";
}

bool FuDynamicPtrType::isAssignableFrom(const FuType * right) const
{
	const FuDynamicPtrType * rightClass;
	return (this->nullable && right->id == FuId::nullType) || ((rightClass = dynamic_cast<const FuDynamicPtrType *>(right)) && isAssignableFromClass(rightClass));
}

bool FuDynamicPtrType::equalsType(const FuType * right) const
{
	const FuDynamicPtrType * that;
	return (that = dynamic_cast<const FuDynamicPtrType *>(right)) && equalsTypeInternal(that);
}

std::string FuDynamicPtrType::getArraySuffix() const
{
	return std::string(isArray() ? "[]#" : "");
}

std::string_view FuDynamicPtrType::getClassSuffix() const
{
	return "#";
}

const FuType * FuArrayStorageType::getBaseType() const
{
	return getElementType()->getBaseType();
}

bool FuArrayStorageType::isArray() const
{
	return true;
}

std::string FuArrayStorageType::getArraySuffix() const
{
	return std::format("[{}]", this->length);
}

bool FuArrayStorageType::equalsType(const FuType * right) const
{
	const FuArrayStorageType * that;
	return (that = dynamic_cast<const FuArrayStorageType *>(right)) && getElementType()->equalsType(that->getElementType().get()) && this->length == that->length;
}

const FuType * FuArrayStorageType::getStorageType() const
{
	return getElementType()->getStorageType();
}

bool FuStringStorageType::isAssignableFrom(const FuType * right) const
{
	return dynamic_cast<const FuStringType *>(right);
}

std::string_view FuStringStorageType::getClassSuffix() const
{
	return "()";
}

bool FuPrintableType::isAssignableFrom(const FuType * right) const
{
	if (dynamic_cast<const FuNumericType *>(right) || dynamic_cast<const FuStringType *>(right))
		return true;
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(right))
		return klass->class_->hasToString();
	else
		return false;
}
FuSystem::FuSystem()
{
	this->voidType->id = FuId::voidType;
	this->voidType->name = "void";
	this->nullType->id = FuId::nullType;
	this->nullType->name = "null";
	this->nullType->nullable = true;
	this->typeParam0->id = FuId::typeParam0;
	this->typeParam0->name = "T";
	this->intType->id = FuId::intType;
	this->intType->name = "int";
	this->nIntType->id = FuId::nIntType;
	this->nIntType->name = "nint";
	this->longType->id = FuId::longType;
	this->longType->name = "long";
	this->floatType->id = FuId::floatType;
	this->floatType->name = "float";
	this->doubleType->id = FuId::doubleType;
	this->doubleType->name = "double";
	this->boolType->id = FuId::boolType;
	this->boolType->name = "bool";
	this->stringPtrType->id = FuId::stringPtrType;
	this->stringPtrType->name = "string";
	this->stringNullablePtrType->id = FuId::stringPtrType;
	this->stringNullablePtrType->name = "string";
	this->stringNullablePtrType->nullable = true;
	this->stringStorageType->id = FuId::stringStorageType;
	this->printableType->name = "printable";
	this->parent = nullptr;
	std::shared_ptr<FuSymbol> basePtr = FuVar::new_(nullptr, "base");
	basePtr->id = FuId::basePtr;
	add(basePtr);
	addMinMaxValue(this->intType.get(), -2147483648, 2147483647);
	this->intType->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::intTryParse, "TryParse", true, FuVar::new_(this->stringPtrType, "value"), FuVar::new_(this->intType, "radix", newLiteralLong(0))));
	add(this->intType);
	this->uIntType->name = "uint";
	add(this->uIntType);
	add(this->nIntType);
	addMinMaxValue(this->longType.get(), (-9223372036854775807 - 1), 9223372036854775807);
	this->longType->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::longTryParse, "TryParse", true, FuVar::new_(this->stringPtrType, "value"), FuVar::new_(this->intType, "radix", newLiteralLong(0))));
	add(this->longType);
	this->byteType->name = "byte";
	add(this->byteType);
	std::shared_ptr<FuRangeType> shortType = FuRangeType::new_(-32768, 32767);
	shortType->name = "short";
	add(shortType);
	std::shared_ptr<FuRangeType> ushortType = FuRangeType::new_(0, 65535);
	ushortType->name = "ushort";
	add(ushortType);
	add(this->floatType);
	this->doubleType->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::doubleTryParse, "TryParse", true, FuVar::new_(this->stringPtrType, "value")));
	add(this->doubleType);
	add(this->boolType);
	this->stringClass->addMethod(this->boolType, FuId::stringContains, "Contains", false, FuVar::new_(this->stringPtrType, "value"));
	this->stringClass->addMethod(this->boolType, FuId::stringEndsWith, "EndsWith", false, FuVar::new_(this->stringPtrType, "value"));
	this->stringClass->addMethod(this->nIntType, FuId::stringIndexOf, "IndexOf", false, FuVar::new_(this->stringPtrType, "value"));
	this->stringClass->addMethod(this->nIntType, FuId::stringLastIndexOf, "LastIndexOf", false, FuVar::new_(this->stringPtrType, "value"));
	this->stringClass->add(FuProperty::new_(this->nIntType, FuId::stringLength, "Length"));
	this->stringClass->addMethod(this->stringStorageType, FuId::stringReplace, "Replace", false, FuVar::new_(this->stringPtrType, "oldValue"), FuVar::new_(this->stringPtrType, "newValue"));
	this->stringClass->addMethod(this->boolType, FuId::stringStartsWith, "StartsWith", false, FuVar::new_(this->stringPtrType, "value"));
	this->stringClass->addMethod(this->stringStorageType, FuId::stringSubstring, "Substring", false, FuVar::new_(this->nIntType, "offset"), FuVar::new_(this->nIntType, "length", newLiteralLong(-1)));
	this->stringClass->addMethod(this->stringStorageType, FuId::stringToLower, "ToLower", false);
	this->stringClass->addMethod(this->stringStorageType, FuId::stringToUpper, "ToUpper", false);
	this->stringPtrType->class_ = this->stringClass.get();
	add(this->stringPtrType);
	this->stringNullablePtrType->class_ = this->stringClass.get();
	this->stringStorageType->class_ = this->stringClass.get();
	std::shared_ptr<FuMethod> arrayBinarySearchPart = FuMethod::new_(nullptr, FuVisibility::numericElementType, FuCallType::normal, this->nIntType, FuId::arrayBinarySearchPart, "BinarySearch", false, FuVar::new_(this->typeParam0, "value"), FuVar::new_(this->nIntType, "startIndex"), FuVar::new_(this->nIntType, "count"));
	this->arrayPtrClass->add(arrayBinarySearchPart);
	std::shared_ptr<FuReadWriteClassType> futemp0 = std::make_shared<FuReadWriteClassType>();
	futemp0->class_ = this->arrayPtrClass.get();
	futemp0->typeArg0 = this->typeParam0;
	this->arrayPtrClass->addMethod(this->voidType, FuId::arrayCopyTo, "CopyTo", false, FuVar::new_(this->nIntType, "sourceIndex"), FuVar::new_(futemp0, "destinationArray"), FuVar::new_(this->nIntType, "destinationIndex"), FuVar::new_(this->nIntType, "count"));
	std::shared_ptr<FuMethod> arrayFillPart = FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->voidType, FuId::arrayFillPart, "Fill", true, FuVar::new_(this->typeParam0, "value"), FuVar::new_(this->nIntType, "startIndex"), FuVar::new_(this->nIntType, "count"));
	this->arrayPtrClass->add(arrayFillPart);
	std::shared_ptr<FuMethod> arraySortPart = FuMethod::new_(nullptr, FuVisibility::numericElementType, FuCallType::normal, this->voidType, FuId::arraySortPart, "Sort", true, FuVar::new_(this->nIntType, "startIndex"), FuVar::new_(this->nIntType, "count"));
	this->arrayPtrClass->add(arraySortPart);
	this->arrayStorageClass->parent = this->arrayPtrClass.get();
	this->arrayStorageClass->add(FuMethodGroup::new_(FuMethod::new_(this->arrayStorageClass.get(), FuVisibility::numericElementType, FuCallType::normal, this->nIntType, FuId::arrayBinarySearchAll, "BinarySearch", false, FuVar::new_(this->typeParam0, "value")), arrayBinarySearchPart));
	this->arrayStorageClass->addMethod(this->boolType, FuId::arrayContains, "Contains", false, FuVar::new_(this->typeParam0, "value"));
	this->arrayStorageClass->add(FuMethodGroup::new_(FuMethod::new_(this->arrayStorageClass.get(), FuVisibility::public_, FuCallType::normal, this->voidType, FuId::arrayFillAll, "Fill", true, FuVar::new_(this->typeParam0, "value")), arrayFillPart));
	this->arrayStorageClass->add(FuProperty::new_(this->nIntType, FuId::arrayLength, "Length"));
	this->arrayStorageClass->add(FuMethodGroup::new_(FuMethod::new_(this->arrayStorageClass.get(), FuVisibility::numericElementType, FuCallType::normal, this->voidType, FuId::arraySortAll, "Sort", true), arraySortPart));
	std::shared_ptr<FuClass> exceptionClass = FuClass::new_(FuCallType::normal, FuId::exceptionClass, "Exception");
	exceptionClass->isPublic = true;
	add(exceptionClass);
	std::shared_ptr<FuType> typeParam0NotFinal = std::make_shared<FuType>();
	typeParam0NotFinal->id = FuId::typeParam0NotFinal;
	typeParam0NotFinal->name = "T";
	std::shared_ptr<FuType> typeParam0Predicate = std::make_shared<FuType>();
	typeParam0Predicate->id = FuId::typeParam0Predicate;
	typeParam0Predicate->name = "Predicate<T>";
	FuClass * listClass = addCollection(FuId::listClass, "List", 1, FuId::listClear, FuId::listCount);
	listClass->addMethod(this->voidType, FuId::listAdd, "Add", true, FuVar::new_(typeParam0NotFinal, "value"));
	std::shared_ptr<FuClassType> futemp1 = std::make_shared<FuClassType>();
	futemp1->class_ = listClass;
	futemp1->typeArg0 = this->typeParam0;
	listClass->addMethod(this->voidType, FuId::listAddRange, "AddRange", true, FuVar::new_(futemp1, "source"));
	listClass->addMethod(this->boolType, FuId::listAll, "All", false, FuVar::new_(typeParam0Predicate, "predicate"));
	listClass->addMethod(this->boolType, FuId::listAny, "Any", false, FuVar::new_(typeParam0Predicate, "predicate"));
	listClass->addMethod(this->boolType, FuId::listContains, "Contains", false, FuVar::new_(this->typeParam0, "value"));
	std::shared_ptr<FuReadWriteClassType> futemp2 = std::make_shared<FuReadWriteClassType>();
	futemp2->class_ = this->arrayPtrClass.get();
	futemp2->typeArg0 = this->typeParam0;
	listClass->addMethod(this->voidType, FuId::listCopyTo, "CopyTo", false, FuVar::new_(this->nIntType, "sourceIndex"), FuVar::new_(futemp2, "destinationArray"), FuVar::new_(this->nIntType, "destinationIndex"), FuVar::new_(this->nIntType, "count"));
	listClass->addMethod(this->nIntType, FuId::listIndexOf, "IndexOf", false, FuVar::new_(this->typeParam0, "value"));
	listClass->addMethod(this->voidType, FuId::listInsert, "Insert", true, FuVar::new_(this->nIntType, "index"), FuVar::new_(typeParam0NotFinal, "value"));
	listClass->addMethod(this->typeParam0, FuId::listLast, "Last", false);
	listClass->addMethod(this->voidType, FuId::listRemoveAt, "RemoveAt", true, FuVar::new_(this->nIntType, "index"));
	listClass->addMethod(this->voidType, FuId::listRemoveRange, "RemoveRange", true, FuVar::new_(this->nIntType, "index"), FuVar::new_(this->nIntType, "count"));
	listClass->add(FuMethodGroup::new_(FuMethod::new_(listClass, FuVisibility::numericElementType, FuCallType::normal, this->voidType, FuId::listSortAll, "Sort", true), FuMethod::new_(listClass, FuVisibility::numericElementType, FuCallType::normal, this->voidType, FuId::listSortPart, "Sort", true, FuVar::new_(this->nIntType, "startIndex"), FuVar::new_(this->nIntType, "count"))));
	FuClass * queueClass = addCollection(FuId::queueClass, "Queue", 1, FuId::queueClear, FuId::queueCount);
	queueClass->addMethod(this->typeParam0, FuId::queueDequeue, "Dequeue", true);
	queueClass->addMethod(this->voidType, FuId::queueEnqueue, "Enqueue", true, FuVar::new_(this->typeParam0, "value"));
	queueClass->addMethod(this->typeParam0, FuId::queuePeek, "Peek", false);
	FuClass * stackClass = addCollection(FuId::stackClass, "Stack", 1, FuId::stackClear, FuId::stackCount);
	stackClass->addMethod(this->typeParam0, FuId::stackPeek, "Peek", false);
	stackClass->addMethod(this->voidType, FuId::stackPush, "Push", true, FuVar::new_(this->typeParam0, "value"));
	stackClass->addMethod(this->typeParam0, FuId::stackPop, "Pop", true);
	FuClass * priorityQueueClass = addCollection(FuId::priorityQueueClass, "PriorityQueue", 1, FuId::priorityQueueClear, FuId::priorityQueueCount);
	priorityQueueClass->addMethod(this->typeParam0, FuId::priorityQueueDequeue, "Dequeue", true);
	priorityQueueClass->addMethod(this->voidType, FuId::priorityQueueEnqueue, "Enqueue", true, FuVar::new_(this->typeParam0, "value"));
	priorityQueueClass->addMethod(this->typeParam0, FuId::priorityQueuePeek, "Peek", false);
	addSet(FuId::hashSetClass, "HashSet", FuId::hashSetAdd, FuId::hashSetClear, FuId::hashSetContains, FuId::hashSetCount, FuId::hashSetRemove);
	addSet(FuId::sortedSetClass, "SortedSet", FuId::sortedSetAdd, FuId::sortedSetClear, FuId::sortedSetContains, FuId::sortedSetCount, FuId::sortedSetRemove);
	const FuClass * dictionaryClass = addDictionary(FuId::dictionaryClass, "Dictionary", FuId::dictionaryClear, FuId::dictionaryContainsKey, FuId::dictionaryCount, FuId::dictionaryRemove);
	addDictionary(FuId::sortedDictionaryClass, "SortedDictionary", FuId::sortedDictionaryClear, FuId::sortedDictionaryContainsKey, FuId::sortedDictionaryCount, FuId::sortedDictionaryRemove);
	addDictionary(FuId::orderedDictionaryClass, "OrderedDictionary", FuId::orderedDictionaryClear, FuId::orderedDictionaryContainsKey, FuId::orderedDictionaryCount, FuId::orderedDictionaryRemove);
	std::shared_ptr<FuClass> textWriterClass = FuClass::new_(FuCallType::normal, FuId::textWriterClass, "TextWriter");
	textWriterClass->addMethod(this->voidType, FuId::textWriterWrite, "Write", true, FuVar::new_(this->printableType, "value"));
	textWriterClass->addMethod(this->voidType, FuId::textWriterWriteChar, "WriteChar", true, FuVar::new_(this->intType, "c"));
	textWriterClass->addMethod(this->voidType, FuId::textWriterWriteCodePoint, "WriteCodePoint", true, FuVar::new_(this->intType, "c"));
	textWriterClass->addMethod(this->voidType, FuId::textWriterWriteLine, "WriteLine", true, FuVar::new_(this->printableType, "value", newLiteralString("")));
	add(textWriterClass);
	std::shared_ptr<FuClass> consoleClass = FuClass::new_(FuCallType::static_, FuId::none, "Console");
	consoleClass->addStaticMethod(this->voidType, FuId::consoleWrite, "Write", FuVar::new_(this->printableType, "value"));
	consoleClass->addStaticMethod(this->voidType, FuId::consoleWriteLine, "WriteLine", FuVar::new_(this->printableType, "value", newLiteralString("")));
	std::shared_ptr<FuStorageType> futemp3 = std::make_shared<FuStorageType>();
	futemp3->class_ = textWriterClass.get();
	consoleClass->add(FuStaticProperty::new_(futemp3, FuId::consoleError, "Error"));
	add(consoleClass);
	std::shared_ptr<FuClass> stringWriterClass = FuClass::new_(FuCallType::sealed, FuId::stringWriterClass, "StringWriter");
	stringWriterClass->addMethod(this->voidType, FuId::stringWriterClear, "Clear", true);
	stringWriterClass->addMethod(this->stringPtrType, FuId::stringWriterToString, "ToString", false);
	add(stringWriterClass);
	stringWriterClass->parent = textWriterClass.get();
	std::shared_ptr<FuClass> convertClass = FuClass::new_(FuCallType::static_, FuId::none, "Convert");
	std::shared_ptr<FuClassType> futemp4 = std::make_shared<FuClassType>();
	futemp4->class_ = this->arrayPtrClass.get();
	futemp4->typeArg0 = this->byteType;
	convertClass->addStaticMethod(this->stringStorageType, FuId::convertToBase64String, "ToBase64String", FuVar::new_(futemp4, "bytes"), FuVar::new_(this->nIntType, "offset"), FuVar::new_(this->nIntType, "length"));
	add(convertClass);
	std::shared_ptr<FuClass> utf8EncodingClass = FuClass::new_(FuCallType::sealed, FuId::none, "UTF8Encoding");
	utf8EncodingClass->addMethod(this->nIntType, FuId::uTF8GetByteCount, "GetByteCount", false, FuVar::new_(this->stringPtrType, "str"));
	std::shared_ptr<FuReadWriteClassType> futemp5 = std::make_shared<FuReadWriteClassType>();
	futemp5->class_ = this->arrayPtrClass.get();
	futemp5->typeArg0 = this->byteType;
	utf8EncodingClass->addMethod(this->voidType, FuId::uTF8GetBytes, "GetBytes", false, FuVar::new_(this->stringPtrType, "str"), FuVar::new_(futemp5, "bytes"), FuVar::new_(this->nIntType, "byteIndex"));
	std::shared_ptr<FuClassType> futemp6 = std::make_shared<FuClassType>();
	futemp6->class_ = this->arrayPtrClass.get();
	futemp6->typeArg0 = this->byteType;
	utf8EncodingClass->addMethod(this->stringStorageType, FuId::uTF8GetString, "GetString", false, FuVar::new_(futemp6, "bytes"), FuVar::new_(this->nIntType, "offset"), FuVar::new_(this->nIntType, "length"));
	std::shared_ptr<FuClass> encodingClass = FuClass::new_(FuCallType::static_, FuId::none, "Encoding");
	encodingClass->add(FuStaticProperty::new_(utf8EncodingClass, FuId::none, "UTF8"));
	add(encodingClass);
	std::shared_ptr<FuClass> environmentClass = FuClass::new_(FuCallType::static_, FuId::none, "Environment");
	environmentClass->addStaticMethod(this->stringNullablePtrType, FuId::environmentGetEnvironmentVariable, "GetEnvironmentVariable", FuVar::new_(this->stringPtrType, "name"));
	add(environmentClass);
	this->regexOptionsEnum = newEnum(true);
	this->regexOptionsEnum->isPublic = true;
	this->regexOptionsEnum->id = FuId::regexOptionsEnum;
	this->regexOptionsEnum->name = "RegexOptions";
	std::shared_ptr<FuConst> regexOptionsNone = newConstLong("None", 0);
	addEnumValue(this->regexOptionsEnum, regexOptionsNone);
	addEnumValue(this->regexOptionsEnum, newConstLong("IgnoreCase", 1));
	addEnumValue(this->regexOptionsEnum, newConstLong("Multiline", 2));
	addEnumValue(this->regexOptionsEnum, newConstLong("Singleline", 16));
	add(this->regexOptionsEnum);
	std::shared_ptr<FuClass> regexClass = FuClass::new_(FuCallType::sealed, FuId::regexClass, "Regex");
	regexClass->addStaticMethod(this->stringStorageType, FuId::regexEscape, "Escape", FuVar::new_(this->stringPtrType, "str"));
	regexClass->add(FuMethodGroup::new_(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::static_, this->boolType, FuId::regexIsMatchStr, "IsMatch", false, FuVar::new_(this->stringPtrType, "input"), FuVar::new_(this->stringPtrType, "pattern"), FuVar::new_(this->regexOptionsEnum, "options", regexOptionsNone)), FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::regexIsMatchRegex, "IsMatch", false, FuVar::new_(this->stringPtrType, "input"))));
	std::shared_ptr<FuDynamicPtrType> futemp7 = std::make_shared<FuDynamicPtrType>();
	futemp7->class_ = regexClass.get();
	regexClass->addStaticMethod(futemp7, FuId::regexCompile, "Compile", FuVar::new_(this->stringPtrType, "pattern"), FuVar::new_(this->regexOptionsEnum, "options", regexOptionsNone));
	add(regexClass);
	std::shared_ptr<FuClass> matchClass = FuClass::new_(FuCallType::sealed, FuId::matchClass, "Match");
	std::shared_ptr<FuClassType> futemp8 = std::make_shared<FuClassType>();
	futemp8->class_ = regexClass.get();
	matchClass->add(FuMethodGroup::new_(FuMethod::new_(matchClass.get(), FuVisibility::public_, FuCallType::normal, this->boolType, FuId::matchFindStr, "Find", true, FuVar::new_(this->stringPtrType, "input"), FuVar::new_(this->stringPtrType, "pattern"), FuVar::new_(this->regexOptionsEnum, "options", regexOptionsNone)), FuMethod::new_(matchClass.get(), FuVisibility::public_, FuCallType::normal, this->boolType, FuId::matchFindRegex, "Find", true, FuVar::new_(this->stringPtrType, "input"), FuVar::new_(futemp8, "pattern"))));
	matchClass->add(FuProperty::new_(this->nIntType, FuId::matchStart, "Start"));
	matchClass->add(FuProperty::new_(this->nIntType, FuId::matchEnd, "End"));
	matchClass->addMethod(this->stringStorageType, FuId::matchGetCapture, "GetCapture", false, FuVar::new_(this->uIntType, "group"));
	matchClass->add(FuProperty::new_(this->nIntType, FuId::matchLength, "Length"));
	matchClass->add(FuProperty::new_(this->stringStorageType, FuId::matchValue, "Value"));
	add(matchClass);
	std::shared_ptr<FuClass> jsonElementClass = FuClass::new_(FuCallType::sealed, FuId::jsonElementClass, "JsonElement");
	std::shared_ptr<FuDynamicPtrType> jsonElementPtr = std::make_shared<FuDynamicPtrType>();
	jsonElementPtr->class_ = jsonElementClass.get();
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::static_, jsonElementPtr, FuId::jsonElementParse, "Parse", false, FuVar::new_(this->stringPtrType, "value")));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::jsonElementIsObject, "IsObject", false));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::jsonElementIsArray, "IsArray", false));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::jsonElementIsString, "IsString", false));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::jsonElementIsNumber, "IsNumber", false));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::jsonElementIsBoolean, "IsBoolean", false));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::jsonElementIsNull, "IsNull", false));
	std::shared_ptr<FuClassType> futemp9 = std::make_shared<FuClassType>();
	futemp9->class_ = dictionaryClass;
	futemp9->typeArg0 = this->stringStorageType;
	futemp9->typeArg1 = jsonElementPtr;
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, futemp9, FuId::jsonElementGetObject, "GetObject", false));
	std::shared_ptr<FuClassType> futemp10 = std::make_shared<FuClassType>();
	futemp10->class_ = listClass;
	futemp10->typeArg0 = jsonElementPtr;
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, futemp10, FuId::jsonElementGetArray, "GetArray", false));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->stringPtrType, FuId::jsonElementGetString, "GetString", false));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->doubleType, FuId::jsonElementGetDouble, "GetDouble", false));
	jsonElementClass->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::jsonElementGetBoolean, "GetBoolean", false));
	add(jsonElementClass);
	std::shared_ptr<FuNumericType> numericType = std::make_shared<FuNumericType>();
	numericType->id = FuId::numericType;
	numericType->name = "numeric";
	std::shared_ptr<FuFloatingType> floatingType = std::make_shared<FuFloatingType>();
	floatingType->id = FuId::floatingType;
	floatingType->name = "float";
	std::shared_ptr<FuFloatingType> floatIntType = std::make_shared<FuFloatingType>();
	floatIntType->id = FuId::floatIntType;
	floatIntType->name = "float";
	std::shared_ptr<FuClass> mathClass = FuClass::new_(FuCallType::static_, FuId::none, "Math");
	mathClass->addStaticMethod(numericType, FuId::mathAbs, "Abs", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Acos", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Asin", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Atan", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Atan2", FuVar::new_(this->doubleType, "y"), FuVar::new_(this->doubleType, "x"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Cbrt", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatIntType, FuId::mathCeiling, "Ceiling", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(numericType, FuId::mathClamp, "Clamp", FuVar::new_(this->doubleType, "value"), FuVar::new_(this->doubleType, "min"), FuVar::new_(this->doubleType, "max"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Cos", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Cosh", FuVar::new_(this->doubleType, "a"));
	mathClass->add(newConstDouble("E", 2.718281828459045));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Exp", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatIntType, FuId::mathMethod, "Floor", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathFusedMultiplyAdd, "FusedMultiplyAdd", FuVar::new_(this->doubleType, "x"), FuVar::new_(this->doubleType, "y"), FuVar::new_(this->doubleType, "z"));
	mathClass->addStaticMethod(this->boolType, FuId::mathIsFinite, "IsFinite", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(this->boolType, FuId::mathIsInfinity, "IsInfinity", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(this->boolType, FuId::mathIsNaN, "IsNaN", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Log", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathLog2, "Log2", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Log10", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(numericType, FuId::mathMax, "Max", FuVar::new_(this->doubleType, "a"), FuVar::new_(this->doubleType, "b"));
	mathClass->addStaticMethod(numericType, FuId::mathMin, "Min", FuVar::new_(this->doubleType, "a"), FuVar::new_(this->doubleType, "b"));
	mathClass->add(FuStaticProperty::new_(this->floatType, FuId::mathNaN, "NaN"));
	mathClass->add(FuStaticProperty::new_(this->floatType, FuId::mathNegativeInfinity, "NegativeInfinity"));
	mathClass->add(newConstDouble("PI", 3.141592653589793));
	mathClass->add(FuStaticProperty::new_(this->floatType, FuId::mathPositiveInfinity, "PositiveInfinity"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Pow", FuVar::new_(this->doubleType, "x"), FuVar::new_(this->doubleType, "y"));
	mathClass->addStaticMethod(floatIntType, FuId::mathRound, "Round", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Sin", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Sinh", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Sqrt", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Tan", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatingType, FuId::mathMethod, "Tanh", FuVar::new_(this->doubleType, "a"));
	mathClass->addStaticMethod(floatIntType, FuId::mathTruncate, "Truncate", FuVar::new_(this->doubleType, "a"));
	add(mathClass);
	std::shared_ptr<FuClass> lockClass = FuClass::new_(FuCallType::sealed, FuId::lockClass, "Lock");
	add(lockClass);
	this->lockPtrType->class_ = lockClass.get();
}

std::shared_ptr<FuLiteralLong> FuSystem::newLiteralLong(int64_t value, int loc) const
{
	std::shared_ptr<FuType> type = value >= -2147483648 && value <= 2147483647 ? FuRangeType::new_(static_cast<int>(value), static_cast<int>(value)) : this->longType;
	std::shared_ptr<FuLiteralLong> futemp0 = std::make_shared<FuLiteralLong>();
	futemp0->loc = loc;
	futemp0->type = type;
	futemp0->value = value;
	return futemp0;
}

std::shared_ptr<FuLiteralString> FuSystem::newLiteralString(std::string_view value, int loc) const
{
	std::shared_ptr<FuLiteralString> futemp0 = std::make_shared<FuLiteralString>();
	futemp0->loc = loc;
	futemp0->type = this->stringPtrType;
	futemp0->value = value;
	return futemp0;
}

std::shared_ptr<FuType> FuSystem::promoteIntegerTypes(const FuType * left, const FuType * right) const
{
	return left == this->longType.get() || right == this->longType.get() ? this->longType : left == this->nIntType.get() || right == this->nIntType.get() ? this->nIntType : this->intType;
}

std::shared_ptr<FuType> FuSystem::promoteFloatingTypes(const FuType * left, const FuType * right) const
{
	if (left->id == FuId::doubleType || right->id == FuId::doubleType)
		return this->doubleType;
	if (left->id == FuId::floatType || right->id == FuId::floatType || left->id == FuId::floatIntType || right->id == FuId::floatIntType)
		return this->floatType;
	return nullptr;
}

std::shared_ptr<FuType> FuSystem::promoteNumericTypes(std::shared_ptr<FuType> left, std::shared_ptr<FuType> right) const
{
	std::shared_ptr<FuType> result = promoteFloatingTypes(left.get(), right.get());
	return result != nullptr ? result : promoteIntegerTypes(left.get(), right.get());
}

std::shared_ptr<FuEnum> FuSystem::newEnum(bool flags) const
{
	std::shared_ptr<FuEnum> enu = flags ? std::make_shared<FuEnumFlags>() : std::make_shared<FuEnum>();
	enu->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::static_, enu, FuId::enumFromInt, "FromInt", false, FuVar::new_(this->intType, "value")));
	if (flags)
		enu->add(FuMethod::new_(nullptr, FuVisibility::public_, FuCallType::normal, this->boolType, FuId::enumHasFlag, "HasFlag", false, FuVar::new_(enu, "flag")));
	return enu;
}

FuClass * FuSystem::addCollection(FuId id, std::string_view name, int typeParameterCount, FuId clearId, FuId countId)
{
	std::shared_ptr<FuClass> result = FuClass::new_(FuCallType::normal, id, name, typeParameterCount);
	result->addMethod(this->voidType, clearId, "Clear", true);
	result->add(FuProperty::new_(this->nIntType, countId, "Count"));
	add(result);
	return result.get();
}

void FuSystem::addSet(FuId id, std::string_view name, FuId addId, FuId clearId, FuId containsId, FuId countId, FuId removeId)
{
	FuClass * set = addCollection(id, name, 1, clearId, countId);
	set->addMethod(this->voidType, addId, "Add", true, FuVar::new_(this->typeParam0, "value"));
	set->addMethod(this->boolType, containsId, "Contains", false, FuVar::new_(this->typeParam0, "value"));
	set->addMethod(this->voidType, removeId, "Remove", true, FuVar::new_(this->typeParam0, "value"));
}

const FuClass * FuSystem::addDictionary(FuId id, std::string_view name, FuId clearId, FuId containsKeyId, FuId countId, FuId removeId)
{
	FuClass * dict = addCollection(id, name, 2, clearId, countId);
	dict->add(FuMethod::new_(dict, FuVisibility::finalValueType, FuCallType::normal, this->voidType, FuId::dictionaryAdd, "Add", true, FuVar::new_(this->typeParam0, "key")));
	dict->addMethod(this->boolType, containsKeyId, "ContainsKey", false, FuVar::new_(this->typeParam0, "key"));
	dict->addMethod(this->voidType, removeId, "Remove", true, FuVar::new_(this->typeParam0, "key"));
	return dict;
}

void FuSystem::addEnumValue(std::shared_ptr<FuEnum> enu, std::shared_ptr<FuConst> value)
{
	value->type = enu;
	enu->add(value);
}

std::shared_ptr<FuConst> FuSystem::newConstLong(std::string_view name, int64_t value) const
{
	std::shared_ptr<FuConst> result = std::make_shared<FuConst>();
	result->visibility = FuVisibility::public_;
	result->name = name;
	result->value = newLiteralLong(value);
	result->visitStatus = FuVisitStatus::done;
	result->type = result->value->type;
	return result;
}

std::shared_ptr<FuConst> FuSystem::newConstDouble(std::string_view name, double value) const
{
	std::shared_ptr<FuLiteralDouble> futemp0 = std::make_shared<FuLiteralDouble>();
	futemp0->value = value;
	futemp0->type = this->doubleType;
	std::shared_ptr<FuConst> futemp1 = std::make_shared<FuConst>();
	futemp1->visibility = FuVisibility::public_;
	futemp1->name = name;
	futemp1->value = futemp0;
	futemp1->type = this->doubleType;
	futemp1->visitStatus = FuVisitStatus::done;
	return futemp1;
}

void FuSystem::addMinMaxValue(FuIntegerType * target, int64_t min, int64_t max) const
{
	target->add(newConstLong("MinValue", min));
	target->add(newConstLong("MaxValue", max));
}

std::shared_ptr<FuSystem> FuSystem::new_()
{
	return std::make_shared<FuSystem>();
}

int FuProgram::getLine(int loc) const
{
	int l = 0;
	int r = std::ssize(this->lineLocs) - 1;
	while (l < r) {
		int m = (l + r + 1) >> 1;
		if (loc < this->lineLocs[m])
			r = m - 1;
		else
			l = m;
	}
	return l;
}

const FuSourceFile * FuProgram::getSourceFile(int line) const
{
	int l = 0;
	int r = std::ssize(this->sourceFiles) - 1;
	while (l < r) {
		int m = (l + r + 1) >> 1;
		if (line < this->sourceFiles[m].line)
			r = m - 1;
		else
			l = m;
	}
	return &this->sourceFiles[l];
}

void FuParser::findName(std::string_view filename, int line, int column)
{
	this->findNameFilename = filename;
	this->findNameLine = line;
	this->findNameColumn = column;
	this->foundName = nullptr;
}

const FuSymbol * FuParser::getFoundDefinition() const
{
	return this->foundName == nullptr ? nullptr : this->foundName->getSymbol();
}

bool FuParser::docParseLine(FuDocPara * para)
{
	if (std::ssize(para->children) > 0)
		para->children.push_back(std::make_shared<FuDocLine>());
	this->lexemeOffset = this->charOffset;
	for (int lastNonWhitespace = 0;;) {
		switch (peekChar()) {
		case -1:
		case '\n':
		case '\r':
			{
				std::shared_ptr<FuDocText> futemp0 = std::make_shared<FuDocText>();
				futemp0->text = getLexeme();
				para->children.push_back(futemp0);
				return lastNonWhitespace == '.';
			}
		case '\t':
		case ' ':
			readChar();
			break;
		case '`':
			if (this->charOffset > this->lexemeOffset) {
				std::shared_ptr<FuDocText> futemp1 = std::make_shared<FuDocText>();
				futemp1->text = getLexeme();
				para->children.push_back(futemp1);
			}
			readChar();
			this->lexemeOffset = this->charOffset;
			for (;;) {
				int c = peekChar();
				if (c == '`') {
					std::shared_ptr<FuDocCode> futemp2 = std::make_shared<FuDocCode>();
					futemp2->text = getLexeme();
					para->children.push_back(futemp2);
					readChar();
					break;
				}
				if (c < 0 || c == '\n') {
					reportError("Unterminated code in documentation comment");
					break;
				}
				readChar();
			}
			this->lexemeOffset = this->charOffset;
			lastNonWhitespace = '`';
			break;
		default:
			lastNonWhitespace = readChar();
			break;
		}
	}
}

void FuParser::docParsePara(FuDocPara * para)
{
	do {
		docParseLine(para);
		nextToken();
	}
	while (see(FuToken::docRegular));
}

std::shared_ptr<FuCodeDoc> FuParser::parseDoc()
{
	if (!see(FuToken::docRegular))
		return nullptr;
	std::shared_ptr<FuCodeDoc> doc = std::make_shared<FuCodeDoc>();
	bool period;
	do {
		period = docParseLine(&doc->summary);
		nextToken();
	}
	while (!period && see(FuToken::docRegular));
	for (;;) {
		switch (this->currentToken) {
		case FuToken::docRegular:
			{
				std::shared_ptr<FuDocPara> para = std::make_shared<FuDocPara>();
				docParsePara(para.get());
				doc->details.push_back(para);
				break;
			}
		case FuToken::docBullet:
			{
				std::shared_ptr<FuDocList> list = std::make_shared<FuDocList>();
				do {
					list->items.emplace_back();
					docParsePara(&static_cast<FuDocPara &>(list->items.back()));
				}
				while (see(FuToken::docBullet));
				doc->details.push_back(list);
				break;
			}
		case FuToken::docBlank:
			nextToken();
			break;
		default:
			return doc;
		}
	}
}

void FuParser::checkXcrementParent()
{
	if (this->xcrementParent.data() != nullptr) {
		std::string_view op = see(FuToken::increment) ? "++" : "--";
		reportError(std::format("{} not allowed on the right side of {}", op, this->xcrementParent));
	}
}

std::shared_ptr<FuLiteralDouble> FuParser::parseDouble()
{
	double d;
	if (!FuNumber_TryParse(d, FuString_Replace(getLexeme(), "_", "")))
		reportError("Invalid floating-point number");
	std::shared_ptr<FuLiteralDouble> result = std::make_shared<FuLiteralDouble>();
	result->loc = this->tokenLoc;
	result->type = this->host->program->system->doubleType;
	result->value = d;
	nextToken();
	return result;
}

bool FuParser::seeDigit() const
{
	int c = peekChar();
	return c >= '0' && c <= '9';
}

std::shared_ptr<FuInterpolatedString> FuParser::parseInterpolatedString()
{
	std::shared_ptr<FuInterpolatedString> result = std::make_shared<FuInterpolatedString>();
	result->loc = this->tokenLoc;
	do {
		std::string prefix{FuString_Replace(this->stringValue, "{{", "{")};
		nextToken();
		std::shared_ptr<FuExpr> arg = parseExpr();
		std::shared_ptr<FuExpr> width = eat(FuToken::comma) ? parseExpr() : nullptr;
		int format = ' ';
		int precision = -1;
		if (see(FuToken::colon)) {
			format = readChar();
			if (seeDigit()) {
				precision = readChar() - '0';
				if (seeDigit())
					precision = precision * 10 + readChar() - '0';
			}
			nextToken();
		}
		result->addPart(prefix, arg, width, format, precision);
		check(FuToken::rightBrace);
	}
	while (readString(true) == FuToken::interpolatedString);
	result->suffix = FuString_Replace(this->stringValue, "{{", "{");
	nextToken();
	return result;
}

std::shared_ptr<FuExpr> FuParser::parseParenthesized()
{
	expect(FuToken::leftParenthesis);
	std::shared_ptr<FuExpr> result = parseExpr();
	expect(FuToken::rightParenthesis);
	return result;
}

bool FuParser::isFindName() const
{
	const FuSourceFile * file = &static_cast<const FuSourceFile &>(this->host->program->sourceFiles.back());
	if (std::ssize(this->host->program->lineLocs) - file->line - 1 == this->findNameLine && file->filename == this->findNameFilename) {
		int loc = this->host->program->lineLocs.back() + this->findNameColumn;
		return loc >= this->tokenLoc && loc <= this->loc;
	}
	return false;
}

bool FuParser::parseName(FuName * result)
{
	if (isFindName())
		this->foundName = result;
	result->loc = this->tokenLoc;
	result->name = this->stringValue;
	return expect(FuToken::id);
}

void FuParser::parseCollection(std::vector<std::shared_ptr<FuExpr>> * result, FuToken closing)
{
	if (!see(closing)) {
		do
			result->push_back(parseExpr());
		while (eat(FuToken::comma));
	}
	expectOrSkip(closing);
}

std::shared_ptr<FuExpr> FuParser::parsePrimaryExpr(bool type)
{
	std::shared_ptr<FuExpr> result;
	switch (this->currentToken) {
	case FuToken::increment:
	case FuToken::decrement:
		checkXcrementParent();
		{
			std::shared_ptr<FuPrefixExpr> futemp0 = std::make_shared<FuPrefixExpr>();
			futemp0->loc = this->tokenLoc;
			futemp0->op = nextToken();
			futemp0->inner = parsePrimaryExpr(false);
			return futemp0;
		}
	case FuToken::minus:
	case FuToken::tilde:
	case FuToken::exclamationMark:
		{
			std::shared_ptr<FuPrefixExpr> futemp1 = std::make_shared<FuPrefixExpr>();
			futemp1->loc = this->tokenLoc;
			futemp1->op = nextToken();
			futemp1->inner = parsePrimaryExpr(false);
			return futemp1;
		}
	case FuToken::new_:
		{
			std::shared_ptr<FuPrefixExpr> newResult = std::make_shared<FuPrefixExpr>();
			newResult->loc = this->tokenLoc;
			newResult->op = nextToken();
			result = parseType();
			if (eat(FuToken::leftBrace)) {
				std::shared_ptr<FuBinaryExpr> futemp2 = std::make_shared<FuBinaryExpr>();
				futemp2->loc = this->tokenLoc;
				futemp2->left = result;
				futemp2->op = FuToken::leftBrace;
				futemp2->right = parseObjectLiteral();
				result = futemp2;
			}
			newResult->inner = result;
			return newResult;
		}
	case FuToken::literalLong:
		result = this->host->program->system->newLiteralLong(this->longValue, this->tokenLoc);
		nextToken();
		break;
	case FuToken::literalDouble:
		result = parseDouble();
		break;
	case FuToken::literalChar:
		result = FuLiteralChar::new_(static_cast<int>(this->longValue), this->tokenLoc);
		nextToken();
		break;
	case FuToken::literalString:
		result = this->host->program->system->newLiteralString(this->stringValue, this->tokenLoc);
		nextToken();
		break;
	case FuToken::false_:
		{
			std::shared_ptr<FuLiteralFalse> futemp3 = std::make_shared<FuLiteralFalse>();
			futemp3->loc = this->tokenLoc;
			futemp3->type = this->host->program->system->boolType;
			result = futemp3;
			nextToken();
			break;
		}
	case FuToken::true_:
		{
			std::shared_ptr<FuLiteralTrue> futemp4 = std::make_shared<FuLiteralTrue>();
			futemp4->loc = this->tokenLoc;
			futemp4->type = this->host->program->system->boolType;
			result = futemp4;
			nextToken();
			break;
		}
	case FuToken::null:
		{
			std::shared_ptr<FuLiteralNull> futemp5 = std::make_shared<FuLiteralNull>();
			futemp5->loc = this->tokenLoc;
			futemp5->type = this->host->program->system->nullType;
			result = futemp5;
			nextToken();
			break;
		}
	case FuToken::interpolatedString:
		result = parseInterpolatedString();
		break;
	case FuToken::leftParenthesis:
		result = parseParenthesized();
		break;
	case FuToken::id:
		{
			std::shared_ptr<FuSymbolReference> symbol = std::make_shared<FuSymbolReference>();
			parseName(symbol.get());
			if (eat(FuToken::fatArrow)) {
				std::shared_ptr<FuLambdaExpr> lambda = std::make_shared<FuLambdaExpr>();
				lambda->loc = symbol->loc;
				lambda->add(FuVar::new_(nullptr, symbol->name));
				lambda->body = parseExpr();
				return lambda;
			}
			if (type && eat(FuToken::less)) {
				std::shared_ptr<FuAggregateInitializer> typeArgs = std::make_shared<FuAggregateInitializer>();
				typeArgs->loc = this->tokenLoc;
				bool saveTypeArg = this->parsingTypeArg;
				this->parsingTypeArg = true;
				do
					typeArgs->items.push_back(parseType());
				while (eat(FuToken::comma));
				expect(FuToken::rightAngle);
				this->parsingTypeArg = saveTypeArg;
				symbol->left = typeArgs;
			}
			result = symbol;
			break;
		}
	case FuToken::resource:
		{
			int loc = this->tokenLoc;
			nextToken();
			if (eat(FuToken::less) && this->stringValue == "byte" && eat(FuToken::id) && eat(FuToken::leftBracket) && eat(FuToken::rightBracket) && eat(FuToken::greater)) {
				std::shared_ptr<FuPrefixExpr> futemp6 = std::make_shared<FuPrefixExpr>();
				futemp6->loc = loc;
				futemp6->op = FuToken::resource;
				futemp6->inner = parseParenthesized();
				result = futemp6;
			}
			else {
				reportError("Expected 'resource<byte[]>'");
				result = nullptr;
			}
			break;
		}
	default:
		reportError("Invalid expression");
		result = nullptr;
		break;
	}
	for (;;) {
		switch (this->currentToken) {
		case FuToken::dot:
			nextToken();
			{
				std::shared_ptr<FuSymbolReference> path = std::make_shared<FuSymbolReference>();
				path->left = result;
				parseName(path.get());
				result = path;
				break;
			}
		case FuToken::leftParenthesis:
			nextToken();
			if (std::shared_ptr<FuSymbolReference>method = std::dynamic_pointer_cast<FuSymbolReference>(result)) {
				std::shared_ptr<FuCallExpr> call = std::make_shared<FuCallExpr>();
				call->loc = this->tokenLoc;
				call->method = method;
				parseCollection(&call->arguments, FuToken::rightParenthesis);
				result = call;
			}
			else
				reportError("Expected a method");
			break;
		case FuToken::leftBracket:
			{
				std::shared_ptr<FuBinaryExpr> futemp7 = std::make_shared<FuBinaryExpr>();
				futemp7->loc = this->tokenLoc;
				futemp7->left = result;
				futemp7->op = nextToken();
				futemp7->right = see(FuToken::rightBracket) ? nullptr : parseExpr();
				result = futemp7;
				expect(FuToken::rightBracket);
				break;
			}
		case FuToken::increment:
		case FuToken::decrement:
			checkXcrementParent();
			{
				std::shared_ptr<FuPostfixExpr> futemp8 = std::make_shared<FuPostfixExpr>();
				futemp8->loc = this->tokenLoc;
				futemp8->inner = result;
				futemp8->op = nextToken();
				result = futemp8;
				break;
			}
		case FuToken::exclamationMark:
		case FuToken::hash:
			{
				std::shared_ptr<FuPostfixExpr> futemp9 = std::make_shared<FuPostfixExpr>();
				futemp9->loc = this->tokenLoc;
				futemp9->inner = result;
				futemp9->op = nextToken();
				result = futemp9;
				break;
			}
		case FuToken::questionMark:
			if (!type)
				return result;
			{
				std::shared_ptr<FuPostfixExpr> futemp10 = std::make_shared<FuPostfixExpr>();
				futemp10->loc = this->tokenLoc;
				futemp10->inner = result;
				futemp10->op = nextToken();
				result = futemp10;
				break;
			}
		default:
			return result;
		}
	}
}

std::shared_ptr<FuExpr> FuParser::parseMulExpr()
{
	std::shared_ptr<FuExpr> left = parsePrimaryExpr(false);
	for (;;) {
		switch (this->currentToken) {
		case FuToken::asterisk:
		case FuToken::slash:
		case FuToken::mod:
			{
				std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
				futemp0->loc = this->tokenLoc;
				futemp0->left = left;
				futemp0->op = nextToken();
				futemp0->right = parsePrimaryExpr(false);
				left = futemp0;
				break;
			}
		default:
			return left;
		}
	}
}

std::shared_ptr<FuExpr> FuParser::parseAddExpr()
{
	std::shared_ptr<FuExpr> left = parseMulExpr();
	while (see(FuToken::plus) || see(FuToken::minus)) {
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = nextToken();
		futemp0->right = parseMulExpr();
		left = futemp0;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseShiftExpr()
{
	std::shared_ptr<FuExpr> left = parseAddExpr();
	while (see(FuToken::shiftLeft) || see(FuToken::shiftRight)) {
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = nextToken();
		futemp0->right = parseAddExpr();
		left = futemp0;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseRelExpr()
{
	std::shared_ptr<FuExpr> left = parseShiftExpr();
	for (;;) {
		switch (this->currentToken) {
		case FuToken::less:
		case FuToken::lessOrEqual:
		case FuToken::greater:
		case FuToken::greaterOrEqual:
			{
				std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
				futemp0->loc = this->tokenLoc;
				futemp0->left = left;
				futemp0->op = nextToken();
				futemp0->right = parseShiftExpr();
				left = futemp0;
				break;
			}
		case FuToken::is:
			{
				std::shared_ptr<FuBinaryExpr> isExpr = std::make_shared<FuBinaryExpr>();
				isExpr->loc = this->tokenLoc;
				isExpr->left = left;
				isExpr->op = nextToken();
				isExpr->right = parsePrimaryExpr(false);
				if (see(FuToken::id)) {
					std::shared_ptr<FuVar> def = parseVar(isExpr->right, false);
					def->isAssigned = true;
					isExpr->right = def;
				}
				return isExpr;
			}
		default:
			return left;
		}
	}
}

std::shared_ptr<FuExpr> FuParser::parseEqualityExpr()
{
	std::shared_ptr<FuExpr> left = parseRelExpr();
	while (see(FuToken::equal) || see(FuToken::notEqual)) {
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = nextToken();
		futemp0->right = parseRelExpr();
		left = futemp0;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseAndExpr()
{
	std::shared_ptr<FuExpr> left = parseEqualityExpr();
	while (see(FuToken::and_)) {
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = nextToken();
		futemp0->right = parseEqualityExpr();
		left = futemp0;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseXorExpr()
{
	std::shared_ptr<FuExpr> left = parseAndExpr();
	while (see(FuToken::xor_)) {
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = nextToken();
		futemp0->right = parseAndExpr();
		left = futemp0;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseOrExpr()
{
	std::shared_ptr<FuExpr> left = parseXorExpr();
	while (see(FuToken::or_)) {
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = nextToken();
		futemp0->right = parseXorExpr();
		left = futemp0;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseCondAndExpr()
{
	std::shared_ptr<FuExpr> left = parseOrExpr();
	while (see(FuToken::condAnd)) {
		std::string_view saveXcrementParent = this->xcrementParent;
		this->xcrementParent = "&&";
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = nextToken();
		futemp0->right = parseOrExpr();
		left = futemp0;
		this->xcrementParent = saveXcrementParent;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseCondOrExpr()
{
	std::shared_ptr<FuExpr> left = parseCondAndExpr();
	while (see(FuToken::condOr)) {
		std::string_view saveXcrementParent = this->xcrementParent;
		this->xcrementParent = "||";
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = nextToken();
		futemp0->right = parseCondAndExpr();
		left = futemp0;
		this->xcrementParent = saveXcrementParent;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseExpr()
{
	std::shared_ptr<FuExpr> left = parseCondOrExpr();
	if (see(FuToken::questionMark)) {
		std::shared_ptr<FuSelectExpr> result = std::make_shared<FuSelectExpr>();
		result->loc = this->tokenLoc;
		result->cond = left;
		nextToken();
		std::string_view saveXcrementParent = this->xcrementParent;
		this->xcrementParent = "?";
		result->onTrue = parseExpr();
		expect(FuToken::colon);
		result->onFalse = parseExpr();
		this->xcrementParent = saveXcrementParent;
		return result;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseType()
{
	std::shared_ptr<FuExpr> left = parsePrimaryExpr(true);
	if (eat(FuToken::range)) {
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = this->tokenLoc;
		futemp0->left = left;
		futemp0->op = FuToken::range;
		futemp0->right = parsePrimaryExpr(true);
		return futemp0;
	}
	return left;
}

std::shared_ptr<FuExpr> FuParser::parseConstInitializer()
{
	if (eat(FuToken::leftBrace)) {
		std::shared_ptr<FuAggregateInitializer> result = std::make_shared<FuAggregateInitializer>();
		result->loc = this->tokenLoc;
		parseCollection(&result->items, FuToken::rightBrace);
		return result;
	}
	return parseExpr();
}

std::shared_ptr<FuAggregateInitializer> FuParser::parseObjectLiteral()
{
	std::shared_ptr<FuAggregateInitializer> result = std::make_shared<FuAggregateInitializer>();
	result->loc = this->tokenLoc;
	do {
		int loc = this->tokenLoc;
		std::shared_ptr<FuSymbolReference> field = std::make_shared<FuSymbolReference>();
		parseName(field.get());
		expect(FuToken::assign);
		std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
		futemp0->loc = loc;
		futemp0->left = field;
		futemp0->op = FuToken::assign;
		futemp0->right = parseExpr();
		result->items.push_back(futemp0);
	}
	while (eat(FuToken::comma));
	expect(FuToken::rightBrace);
	return result;
}

std::shared_ptr<FuExpr> FuParser::parseInitializer()
{
	if (!eat(FuToken::assign))
		return nullptr;
	if (eat(FuToken::leftBrace))
		return parseObjectLiteral();
	return parseExpr();
}

void FuParser::addSymbol(FuScope * scope, std::shared_ptr<FuSymbol> symbol)
{
	if (scope->contains(symbol.get()))
		this->host->reportStatementError(symbol.get(), "Duplicate symbol");
	else
		scope->add(symbol);
}

std::shared_ptr<FuVar> FuParser::parseVar(std::shared_ptr<FuExpr> type, bool initializer)
{
	std::shared_ptr<FuVar> result = std::make_shared<FuVar>();
	result->typeExpr = type;
	parseName(result.get());
	result->value = initializer ? parseInitializer() : nullptr;
	return result;
}

std::shared_ptr<FuConst> FuParser::parseConst(FuVisibility visibility)
{
	expect(FuToken::const_);
	std::shared_ptr<FuConst> konst = std::make_shared<FuConst>();
	konst->visibility = visibility;
	konst->typeExpr = parseType();
	konst->visitStatus = FuVisitStatus::notYet;
	parseName(konst.get());
	expect(FuToken::assign);
	konst->value = parseConstInitializer();
	closeMember(FuToken::semicolon, konst.get());
	return konst;
}

std::shared_ptr<FuExpr> FuParser::parseAssign(bool allowVar)
{
	std::shared_ptr<FuExpr> left = allowVar ? parseType() : parseExpr();
	switch (this->currentToken) {
	case FuToken::assign:
	case FuToken::addAssign:
	case FuToken::subAssign:
	case FuToken::mulAssign:
	case FuToken::divAssign:
	case FuToken::modAssign:
	case FuToken::andAssign:
	case FuToken::orAssign:
	case FuToken::xorAssign:
	case FuToken::shiftLeftAssign:
	case FuToken::shiftRightAssign:
		{
			std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
			futemp0->loc = this->tokenLoc;
			futemp0->left = left;
			futemp0->op = nextToken();
			futemp0->right = parseAssign(false);
			return futemp0;
		}
	case FuToken::id:
		if (allowVar)
			return parseVar(left, true);
		return left;
	default:
		return left;
	}
}

std::shared_ptr<FuBlock> FuParser::parseBlock(FuMethodBase * method)
{
	std::shared_ptr<FuBlock> result = std::make_shared<FuBlock>();
	result->loc = this->tokenLoc;
	expect(FuToken::leftBrace);
	while (!see(FuToken::rightBrace) && !see(FuToken::endOfFile))
		result->statements.push_back(parseStatement());
	closeMember(FuToken::rightBrace, method);
	return result;
}

std::shared_ptr<FuAssert> FuParser::parseAssert()
{
	std::shared_ptr<FuAssert> result = std::make_shared<FuAssert>();
	result->loc = this->tokenLoc;
	expect(FuToken::assert);
	result->cond = parseExpr();
	if (eat(FuToken::comma))
		result->message = parseExpr();
	expect(FuToken::semicolon);
	return result;
}

std::shared_ptr<FuBreak> FuParser::parseBreak()
{
	if (this->currentLoopOrSwitch == nullptr)
		reportError("'break' outside loop or 'switch'");
	std::shared_ptr<FuBreak> result = std::make_shared<FuBreak>();
	result->loc = this->tokenLoc;
	result->loopOrSwitch = this->currentLoopOrSwitch;
	expect(FuToken::break_);
	expect(FuToken::semicolon);
	if (FuLoop *loop = dynamic_cast<FuLoop *>(this->currentLoopOrSwitch))
		loop->hasBreak = true;
	return result;
}

std::shared_ptr<FuContinue> FuParser::parseContinue()
{
	if (this->currentLoop == nullptr)
		reportError("'continue' outside loop");
	std::shared_ptr<FuContinue> result = std::make_shared<FuContinue>();
	result->loc = this->tokenLoc;
	result->loop = this->currentLoop;
	expect(FuToken::continue_);
	expect(FuToken::semicolon);
	return result;
}

void FuParser::parseLoopBody(FuLoop * loop)
{
	const FuLoop * outerLoop = this->currentLoop;
	FuCondCompletionStatement * outerLoopOrSwitch = this->currentLoopOrSwitch;
	this->currentLoop = loop;
	this->currentLoopOrSwitch = loop;
	loop->body = parseStatement();
	this->currentLoopOrSwitch = outerLoopOrSwitch;
	this->currentLoop = outerLoop;
}

std::shared_ptr<FuDoWhile> FuParser::parseDoWhile()
{
	std::shared_ptr<FuDoWhile> result = std::make_shared<FuDoWhile>();
	result->loc = this->tokenLoc;
	expect(FuToken::do_);
	parseLoopBody(result.get());
	expect(FuToken::while_);
	result->cond = parseParenthesized();
	expect(FuToken::semicolon);
	return result;
}

std::shared_ptr<FuFor> FuParser::parseFor()
{
	std::shared_ptr<FuFor> result = std::make_shared<FuFor>();
	result->loc = this->tokenLoc;
	expect(FuToken::for_);
	expect(FuToken::leftParenthesis);
	if (!see(FuToken::semicolon))
		result->init = parseAssign(true);
	expect(FuToken::semicolon);
	if (!see(FuToken::semicolon))
		result->cond = parseExpr();
	expect(FuToken::semicolon);
	if (!see(FuToken::rightParenthesis))
		result->advance = parseAssign(false);
	expect(FuToken::rightParenthesis);
	parseLoopBody(result.get());
	return result;
}

void FuParser::parseForeachIterator(FuForeach * result)
{
	addSymbol(result, parseVar(parseType(), false));
}

std::shared_ptr<FuForeach> FuParser::parseForeach()
{
	std::shared_ptr<FuForeach> result = std::make_shared<FuForeach>();
	result->loc = this->tokenLoc;
	expect(FuToken::foreach);
	expect(FuToken::leftParenthesis);
	if (eat(FuToken::leftParenthesis)) {
		parseForeachIterator(result.get());
		expect(FuToken::comma);
		parseForeachIterator(result.get());
		expect(FuToken::rightParenthesis);
	}
	else
		parseForeachIterator(result.get());
	expect(FuToken::in);
	result->collection = parseExpr();
	expect(FuToken::rightParenthesis);
	parseLoopBody(result.get());
	return result;
}

std::shared_ptr<FuIf> FuParser::parseIf()
{
	std::shared_ptr<FuIf> result = std::make_shared<FuIf>();
	result->loc = this->tokenLoc;
	expect(FuToken::if_);
	result->cond = parseParenthesized();
	result->onTrue = parseStatement();
	if (eat(FuToken::else_))
		result->onFalse = parseStatement();
	return result;
}

std::shared_ptr<FuLock> FuParser::parseLock()
{
	std::shared_ptr<FuLock> result = std::make_shared<FuLock>();
	result->loc = this->tokenLoc;
	expect(FuToken::lock_);
	result->lock = parseParenthesized();
	result->body = parseStatement();
	return result;
}

std::shared_ptr<FuNative> FuParser::parseNative()
{
	std::shared_ptr<FuNative> result = std::make_shared<FuNative>();
	result->loc = this->tokenLoc;
	expect(FuToken::native);
	if (see(FuToken::literalString))
		result->content = this->stringValue;
	else {
		int offset = this->charOffset;
		expect(FuToken::leftBrace);
		int nesting = 1;
		for (;;) {
			if (see(FuToken::endOfFile)) {
				expect(FuToken::rightBrace);
				return result;
			}
			if (see(FuToken::leftBrace))
				nesting++;
			else if (see(FuToken::rightBrace)) {
				if (--nesting == 0)
					break;
			}
			nextToken();
		}
		assert(this->input[this->charOffset - 1] == '}');
		result->content = std::string_view(reinterpret_cast<const char *>(this->input + offset), this->charOffset - 1 - offset);
	}
	nextToken();
	return result;
}

int FuParser::getCurrentLine() const
{
	return std::ssize(this->host->program->lineLocs) - this->host->program->sourceFiles.back().line - 1;
}

int FuParser::getTokenColumn() const
{
	return this->tokenLoc - this->host->program->lineLocs.back();
}

void FuParser::setMemberEnd(FuMember * member) const
{
	member->endLine = getCurrentLine();
	member->endColumn = this->loc - this->host->program->lineLocs.back();
}

void FuParser::closeMember(FuToken expected, FuMember * member)
{
	if (member != nullptr)
		setMemberEnd(member);
	expect(expected);
}

void FuParser::closeContainer(FuContainerType * type)
{
	type->endLine = getCurrentLine();
	type->endColumn = this->loc - this->host->program->lineLocs.back();
	expect(FuToken::rightBrace);
}

std::shared_ptr<FuReturn> FuParser::parseReturn(FuMethod * method)
{
	std::shared_ptr<FuReturn> result = std::make_shared<FuReturn>();
	result->loc = this->tokenLoc;
	nextToken();
	if (!see(FuToken::semicolon))
		result->value = parseExpr();
	closeMember(FuToken::semicolon, method);
	return result;
}

std::shared_ptr<FuSwitch> FuParser::parseSwitch()
{
	std::shared_ptr<FuSwitch> result = std::make_shared<FuSwitch>();
	result->loc = this->tokenLoc;
	expect(FuToken::switch_);
	result->value = parseParenthesized();
	expect(FuToken::leftBrace);
	FuCondCompletionStatement * outerLoopOrSwitch = this->currentLoopOrSwitch;
	this->currentLoopOrSwitch = result.get();
	while (eat(FuToken::case_)) {
		result->cases.emplace_back();
		FuCase * kase = &static_cast<FuCase &>(result->cases.back());
		do {
			std::shared_ptr<FuExpr> expr = parseExpr();
			if (see(FuToken::id))
				expr = parseVar(expr, false);
			if (eat(FuToken::when)) {
				std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
				futemp0->loc = this->tokenLoc;
				futemp0->left = expr;
				futemp0->op = FuToken::when;
				futemp0->right = parseExpr();
				expr = futemp0;
			}
			kase->values.push_back(expr);
			expect(FuToken::colon);
		}
		while (eat(FuToken::case_));
		if (see(FuToken::default_)) {
			reportError("Please remove 'case' before 'default'");
			break;
		}
		while (!see(FuToken::endOfFile)) {
			kase->body.push_back(parseStatement());
			switch (this->currentToken) {
			case FuToken::case_:
			case FuToken::default_:
			case FuToken::rightBrace:
				break;
			default:
				continue;
			}
			break;
		}
	}
	if (std::ssize(result->cases) == 0)
		reportError("Switch with no cases");
	if (eat(FuToken::default_)) {
		expect(FuToken::colon);
		do {
			if (see(FuToken::endOfFile))
				break;
			result->defaultBody.push_back(parseStatement());
		}
		while (!see(FuToken::rightBrace));
	}
	expect(FuToken::rightBrace);
	this->currentLoopOrSwitch = outerLoopOrSwitch;
	return result;
}

std::shared_ptr<FuThrow> FuParser::parseThrow()
{
	std::shared_ptr<FuThrow> result = std::make_shared<FuThrow>();
	result->loc = this->tokenLoc;
	expect(FuToken::throw_);
	result->class_ = std::make_shared<FuSymbolReference>();
	parseName(result->class_.get());
	expectOrSkip(FuToken::leftParenthesis);
	result->message = see(FuToken::rightParenthesis) ? nullptr : parseExpr();
	expect(FuToken::rightParenthesis);
	expect(FuToken::semicolon);
	return result;
}

std::shared_ptr<FuWhile> FuParser::parseWhile()
{
	std::shared_ptr<FuWhile> result = std::make_shared<FuWhile>();
	result->loc = this->tokenLoc;
	expect(FuToken::while_);
	result->cond = parseParenthesized();
	parseLoopBody(result.get());
	return result;
}

std::shared_ptr<FuStatement> FuParser::parseStatement()
{
	switch (this->currentToken) {
	case FuToken::leftBrace:
		return parseBlock(nullptr);
	case FuToken::assert:
		return parseAssert();
	case FuToken::break_:
		return parseBreak();
	case FuToken::const_:
		return parseConst(FuVisibility::private_);
	case FuToken::continue_:
		return parseContinue();
	case FuToken::do_:
		return parseDoWhile();
	case FuToken::for_:
		return parseFor();
	case FuToken::foreach:
		return parseForeach();
	case FuToken::if_:
		return parseIf();
	case FuToken::lock_:
		return parseLock();
	case FuToken::native:
		return parseNative();
	case FuToken::return_:
		return parseReturn(nullptr);
	case FuToken::switch_:
		return parseSwitch();
	case FuToken::throw_:
		return parseThrow();
	case FuToken::while_:
		return parseWhile();
	default:
		{
			std::shared_ptr<FuExpr> expr = parseAssign(true);
			expect(FuToken::semicolon);
			return expr;
		}
	}
}

FuCallType FuParser::parseCallType()
{
	switch (this->currentToken) {
	case FuToken::static_:
		nextToken();
		return FuCallType::static_;
	case FuToken::abstract:
		nextToken();
		return FuCallType::abstract;
	case FuToken::virtual_:
		nextToken();
		return FuCallType::virtual_;
	case FuToken::override_:
		nextToken();
		return FuCallType::override_;
	case FuToken::sealed:
		nextToken();
		return FuCallType::sealed;
	default:
		return FuCallType::normal;
	}
}

void FuParser::parseMethod(FuClass * klass, std::shared_ptr<FuMethod> method)
{
	addSymbol(klass, method);
	method->parameters.parent = klass;
	if (method->callType != FuCallType::static_)
		method->addThis(klass, eat(FuToken::exclamationMark));
	expectOrSkip(FuToken::leftParenthesis);
	if (!see(FuToken::rightParenthesis)) {
		do {
			std::shared_ptr<FuCodeDoc> doc = parseDoc();
			std::shared_ptr<FuVar> param = parseVar(parseType(), true);
			param->documentation = doc;
			addSymbol(&method->parameters, param);
		}
		while (eat(FuToken::comma));
	}
	expect(FuToken::rightParenthesis);
	std::shared_ptr<FuCodeDoc> throwsDoc = parseDoc();
	if (eat(FuToken::throws)) {
		do {
			std::shared_ptr<FuThrowsDeclaration> decl = std::make_shared<FuThrowsDeclaration>();
			if (throwsDoc == nullptr)
				decl->documentation = parseDoc();
			else if (std::ssize(method->throws) > 0) {
				reportError("Exception documentation must follow the 'throws' keyword");
				decl->documentation = nullptr;
			}
			else
				decl->documentation = throwsDoc;
			parseName(decl.get());
			method->throws.push_back(decl);
		}
		while (eat(FuToken::comma));
	}
	if (method->callType == FuCallType::abstract)
		closeMember(FuToken::semicolon, method.get());
	else if (see(FuToken::fatArrow))
		method->body = parseReturn(method.get());
	else if (check(FuToken::leftBrace))
		method->body = parseBlock(method.get());
}

void FuParser::reportFormerError(int line, int column, int length, std::string_view message) const
{
	this->host->reportError(this->host->program->sourceFiles.back().filename, line, column, column + length, message);
}

void FuParser::reportCallTypeError(int line, int column, std::string_view kind, FuCallType callType) const
{
	std::string_view callTypeString = FuMethod::callTypeToString(callType);
	reportFormerError(line, column, std::ssize(callTypeString), std::format("{} cannot be {}", kind, callTypeString));
}

void FuParser::parseClass(std::shared_ptr<FuCodeDoc> doc, int line, int column, bool isPublic, FuCallType callType)
{
	expect(FuToken::class_);
	std::shared_ptr<FuClass> klass = std::make_shared<FuClass>();
	klass->documentation = doc;
	klass->startLine = line;
	klass->startColumn = column;
	klass->isPublic = isPublic;
	klass->callType = callType;
	if (parseName(klass.get()))
		addSymbol(this->host->program, klass);
	if (eat(FuToken::colon))
		parseName(&klass->baseClass);
	expect(FuToken::leftBrace);
	while (!see(FuToken::rightBrace) && !see(FuToken::endOfFile)) {
		doc = parseDoc();
		line = getCurrentLine();
		column = getTokenColumn();
		FuVisibility visibility;
		switch (this->currentToken) {
		case FuToken::internal:
			visibility = FuVisibility::internal;
			nextToken();
			break;
		case FuToken::protected_:
			visibility = FuVisibility::protected_;
			nextToken();
			break;
		case FuToken::public_:
			visibility = FuVisibility::public_;
			nextToken();
			break;
		case FuToken::native:
			klass->addNative(parseNative());
			continue;
		default:
			visibility = FuVisibility::private_;
			break;
		}
		if (see(FuToken::const_)) {
			std::shared_ptr<FuConst> konst = parseConst(visibility);
			konst->startLine = line;
			konst->startColumn = column;
			konst->documentation = doc;
			addSymbol(klass.get(), konst);
			continue;
		}
		int callTypeLine = getCurrentLine();
		int callTypeColumn = getTokenColumn();
		callType = parseCallType();
		std::shared_ptr<FuExpr> type = eat(FuToken::void_) ? this->host->program->system->voidType : parseType();
		const FuCallExpr * call;
		if (see(FuToken::leftBrace) && (call = dynamic_cast<const FuCallExpr *>(type.get()))) {
			if (call->method->name != klass->name)
				reportError("Method with no return type");
			else {
				if (klass->callType == FuCallType::static_)
					reportError("Constructor in a static class");
				if (callType != FuCallType::normal)
					reportCallTypeError(callTypeLine, callTypeColumn, "Constructor", callType);
				if (std::ssize(call->arguments) != 0)
					reportError("Constructor parameters not supported");
				if (klass->constructor != nullptr)
					reportError(std::format("Duplicate constructor, already defined in line {}", this->host->program->getLine(klass->constructor->loc) + 1));
			}
			if (visibility == FuVisibility::private_)
				visibility = FuVisibility::internal;
			std::shared_ptr<FuMethodBase> futemp0 = std::make_shared<FuMethodBase>();
			futemp0->startLine = line;
			futemp0->startColumn = column;
			futemp0->loc = call->loc;
			futemp0->documentation = doc;
			futemp0->visibility = visibility;
			futemp0->parent = klass.get();
			futemp0->type = this->host->program->system->voidType;
			futemp0->name = klass->name;
			klass->constructor = futemp0;
			klass->constructor->parameters.parent = klass.get();
			klass->constructor->addThis(klass.get(), true);
			klass->constructor->body = parseBlock(klass->constructor.get());
			continue;
		}
		bool foundName = isFindName();
		int loc = this->tokenLoc;
		std::string name{this->stringValue};
		if (!expect(FuToken::id))
			continue;
		if (see(FuToken::leftParenthesis) || see(FuToken::exclamationMark)) {
			if (callType == FuCallType::static_ || klass->callType == FuCallType::abstract) {
			}
			else if (klass->callType == FuCallType::static_)
				reportError("Only static methods allowed in a static class");
			else if (callType == FuCallType::abstract)
				reportFormerError(callTypeLine, callTypeColumn, 8, "Abstract methods allowed only in an abstract class");
			else if (klass->callType == FuCallType::sealed && callType == FuCallType::virtual_)
				reportFormerError(callTypeLine, callTypeColumn, 7, "Virtual methods disallowed in a sealed class");
			if (visibility == FuVisibility::private_ && callType != FuCallType::static_ && callType != FuCallType::normal)
				reportCallTypeError(callTypeLine, callTypeColumn, "Private method", callType);
			std::shared_ptr<FuMethod> method = std::make_shared<FuMethod>();
			method->startLine = line;
			method->startColumn = column;
			method->loc = loc;
			method->documentation = doc;
			method->visibility = visibility;
			method->callType = callType;
			method->typeExpr = type;
			method->name = name;
			parseMethod(klass.get(), method);
			if (foundName)
				this->foundName = method.get();
			continue;
		}
		if (visibility == FuVisibility::public_)
			reportFormerError(line, column, 6, "Field cannot be public");
		if (callType != FuCallType::normal)
			reportCallTypeError(callTypeLine, callTypeColumn, "Field", callType);
		if (type == this->host->program->system->voidType)
			reportError("Field cannot be void");
		std::shared_ptr<FuField> field = std::make_shared<FuField>();
		field->startLine = line;
		field->startColumn = column;
		field->loc = loc;
		field->documentation = doc;
		field->visibility = visibility;
		field->typeExpr = type;
		field->name = name;
		field->value = parseInitializer();
		addSymbol(klass.get(), field);
		closeMember(FuToken::semicolon, field.get());
		if (foundName)
			this->foundName = field.get();
	}
	closeContainer(klass.get());
}

void FuParser::parseEnum(std::shared_ptr<FuCodeDoc> doc, int line, int column, bool isPublic)
{
	expect(FuToken::enum_);
	bool flags = eat(FuToken::asterisk);
	std::shared_ptr<FuEnum> enu = this->host->program->system->newEnum(flags);
	enu->documentation = doc;
	enu->startLine = line;
	enu->startColumn = column;
	enu->isPublic = isPublic;
	if (parseName(enu.get()))
		addSymbol(this->host->program, enu);
	expect(FuToken::leftBrace);
	do {
		std::shared_ptr<FuConst> konst = std::make_shared<FuConst>();
		konst->visibility = FuVisibility::public_;
		konst->documentation = parseDoc();
		konst->type = enu;
		konst->visitStatus = FuVisitStatus::notYet;
		konst->startLine = getCurrentLine();
		konst->startColumn = getTokenColumn();
		parseName(konst.get());
		if (eat(FuToken::assign))
			konst->value = parseExpr();
		else if (flags)
			reportError("enum* symbol must be assigned a value");
		addSymbol(enu.get(), konst);
		setMemberEnd(konst.get());
	}
	while (eat(FuToken::comma));
	closeContainer(enu.get());
}

void FuParser::parse(std::string_view filename, uint8_t const * input, int inputLength)
{
	open(filename, input, inputLength);
	while (!see(FuToken::endOfFile)) {
		std::shared_ptr<FuCodeDoc> doc = parseDoc();
		int line = getCurrentLine();
		int column = getTokenColumn();
		bool isPublic = eat(FuToken::public_);
		switch (this->currentToken) {
		case FuToken::class_:
			parseClass(doc, line, column, isPublic, FuCallType::normal);
			break;
		case FuToken::static_:
		case FuToken::abstract:
		case FuToken::sealed:
			parseClass(doc, line, column, isPublic, parseCallType());
			break;
		case FuToken::enum_:
			parseEnum(doc, line, column, isPublic);
			break;
		case FuToken::native:
			this->host->program->topLevelNatives.push_back(parseNative()->content);
			break;
		default:
			reportError("Expected class or enum");
			nextToken();
			break;
		}
	}
}

void FuConsoleHost::reportError(std::string_view filename, int line, int startUtf16Column, int endUtf16Column, std::string_view message)
{
	this->hasErrors = true;
	std::cerr << filename << "(" << (line + 1) << "): ERROR: " << message << '\n';
}

int FuSemaHost::getResourceLength(std::string_view name, const FuPrefixExpr * expr)
{
	return 0;
}
FuSema::FuSema()
{
	this->poison->name = "poison";
}

void FuSema::setHost(FuSemaHost * host)
{
	this->host = host;
}

void FuSema::reportError(const FuStatement * statement, std::string_view message) const
{
	this->host->reportStatementError(statement, message);
}

std::shared_ptr<FuType> FuSema::poisonError(const FuStatement * statement, std::string_view message) const
{
	reportError(statement, message);
	return this->poison;
}

void FuSema::resolveBase(FuClass * klass)
{
	if (klass->hasBaseClass()) {
		this->currentScope = klass;
		if (FuClass *baseClass = dynamic_cast<FuClass *>(this->host->program->tryLookup(klass->baseClass.name, true).get())) {
			if (klass->isPublic && !baseClass->isPublic)
				reportError(&klass->baseClass, "Public class cannot derive from an internal class");
			klass->baseClass.symbol = baseClass;
			baseClass->hasSubclasses = true;
			klass->parent = baseClass;
		}
		else
			reportError(&klass->baseClass, std::format("Base class '{}' not found", klass->baseClass.name));
	}
	this->host->program->classes.push_back(klass);
}

void FuSema::checkBaseCycle(FuClass * klass)
{
	const FuSymbol * hare = klass;
	const FuSymbol * tortoise = klass;
	do {
		hare = hare->parent;
		if (hare == nullptr)
			return;
		if (hare->id == FuId::exceptionClass)
			klass->id = FuId::exceptionClass;
		hare = hare->parent;
		if (hare == nullptr)
			return;
		if (hare->id == FuId::exceptionClass)
			klass->id = FuId::exceptionClass;
		tortoise = tortoise->parent;
	}
	while (tortoise != hare);
	this->currentScope = klass;
	reportError(klass, std::format("Circular inheritance for class '{}'", klass->name));
}

void FuSema::takePtr(const FuExpr * expr)
{
	if (FuArrayStorageType *arrayStg = dynamic_cast<FuArrayStorageType *>(expr->type.get()))
		arrayStg->ptrTaken = true;
}

bool FuSema::coerce(const FuExpr * expr, const FuType * type) const
{
	if (expr == this->poison.get() || type == this->poison.get())
		return false;
	if (!type->isAssignableFrom(expr->type.get())) {
		reportError(expr, std::format("Cannot convert '{}' to '{}'", expr->type->toString(), type->toString()));
		return false;
	}
	const FuPrefixExpr * prefix;
	if ((prefix = dynamic_cast<const FuPrefixExpr *>(expr)) && prefix->op == FuToken::new_ && !dynamic_cast<const FuDynamicPtrType *>(type)) {
		const FuDynamicPtrType * newType = static_cast<const FuDynamicPtrType *>(expr->type.get());
		std::string_view kind = newType->class_->id == FuId::arrayPtrClass ? "array" : "object";
		reportError(expr, std::format("Dynamically allocated {} must be assigned to a '{}' reference", kind, expr->type->toString()));
		return false;
	}
	takePtr(expr);
	return true;
}

bool FuSema::coercePermanent(const FuExpr * expr, const FuType * type) const
{
	bool ok = coerce(expr, type);
	if (ok && type->id == FuId::stringPtrType && expr->isNewString(true)) {
		reportError(expr, "New string must be assigned to 'string()'");
		return false;
	}
	return ok;
}

std::shared_ptr<FuExpr> FuSema::visitInterpolatedString(std::shared_ptr<FuInterpolatedString> expr)
{
	int partsCount = 0;
	std::string s{""};
	for (int partsIndex = 0; partsIndex < std::ssize(expr->parts); partsIndex++) {
		const FuInterpolatedPart * part = &expr->parts[partsIndex];
		s += part->prefix;
		std::shared_ptr<FuExpr> arg = visitExpr(part->argument);
		if (coerce(arg.get(), this->host->program->system->printableType.get())) {
			if (dynamic_cast<const FuIntegerType *>(arg->type.get())) {
				switch (part->format) {
				case ' ':
					{
						const FuLiteralLong * literalLong;
						if ((literalLong = dynamic_cast<const FuLiteralLong *>(arg.get())) && part->widthExpr == nullptr) {
							s += std::format("{}", literalLong->value);
							continue;
						}
						break;
					}
				case 'D':
				case 'd':
				case 'X':
				case 'x':
					if (part->widthExpr != nullptr && part->precision >= 0)
						reportError(part->widthExpr.get(), "Cannot format an integer with both width and precision");
					break;
				default:
					reportError(arg.get(), "Invalid format");
					break;
				}
			}
			else if (dynamic_cast<const FuFloatingType *>(arg->type.get())) {
				switch (part->format) {
				case ' ':
				case 'F':
				case 'f':
				case 'E':
				case 'e':
					break;
				default:
					reportError(arg.get(), "Invalid format");
					break;
				}
			}
			else {
				if (part->format != ' ')
					reportError(arg.get(), "Invalid format");
				else {
					const FuLiteralString * literalString;
					if ((literalString = dynamic_cast<const FuLiteralString *>(arg.get())) && part->widthExpr == nullptr) {
						s += literalString->value;
						continue;
					}
				}
			}
		}
		FuInterpolatedPart * targetPart = &expr->parts[partsCount++];
		targetPart->prefix = s;
		targetPart->argument = arg;
		targetPart->widthExpr = part->widthExpr;
		targetPart->width = part->widthExpr != nullptr ? foldConstInt(part->widthExpr) : 0;
		targetPart->format = part->format;
		targetPart->precision = part->precision;
		s = "";
	}
	s += expr->suffix;
	if (partsCount == 0)
		return this->host->program->system->newLiteralString(s, expr->loc);
	expr->type = this->host->program->system->stringStorageType;
	expr->parts.erase(expr->parts.begin() + partsCount, expr->parts.begin() + partsCount + (std::ssize(expr->parts) - partsCount));
	expr->suffix = s;
	return expr;
}

std::shared_ptr<FuExpr> FuSema::lookup(std::shared_ptr<FuSymbolReference> expr, const FuScope * scope)
{
	if (expr->symbol == nullptr) {
		expr->symbol = scope->tryLookup(expr->name, expr->left == nullptr).get();
		if (expr->symbol == nullptr)
			return poisonError(expr.get(), std::format("'{}' not found", expr->name));
		expr->type = expr->symbol->type;
	}
	FuConst * konst;
	if (!dynamic_cast<const FuEnum *>(scope) && (konst = dynamic_cast<FuConst *>(expr->symbol))) {
		resolveConst(konst);
		if (dynamic_cast<const FuLiteral *>(konst->value.get()) || dynamic_cast<const FuSymbolReference *>(konst->value.get())) {
			const FuLiteralLong * intValue;
			if (dynamic_cast<const FuFloatingType *>(konst->type.get()) && (intValue = dynamic_cast<const FuLiteralLong *>(konst->value.get())))
				return toLiteralDouble(expr.get(), intValue->value);
			return konst->value;
		}
	}
	return expr;
}

FuContainerType * FuSema::getCurrentContainer() const
{
	for (FuScope * scope = this->currentScope; scope != nullptr; scope = scope->parent) {
		if (FuContainerType *container = dynamic_cast<FuContainerType *>(scope))
			return container;
	}
	std::abort();
}

std::shared_ptr<FuExpr> FuSema::visitSymbolReference(std::shared_ptr<FuSymbolReference> expr)
{
	if (expr->left == nullptr) {
		std::shared_ptr<FuExpr> resolved = lookup(expr, this->currentScope);
		if (const FuMember *nearMember = dynamic_cast<const FuMember *>(expr->symbol)) {
			const FuClass * memberClass;
			if (nearMember->visibility == FuVisibility::private_ && (memberClass = dynamic_cast<const FuClass *>(nearMember->parent)) && memberClass != getCurrentContainer())
				reportError(expr.get(), std::format("Cannot access private member '{}'", expr->name));
			if (!nearMember->isStatic() && (this->currentMethod == nullptr || this->currentMethod->isStatic()))
				reportError(expr.get(), std::format("Cannot use instance member '{}' from static context", expr->name));
		}
		if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(resolved.get())) {
			if (const FuVar *v = dynamic_cast<const FuVar *>(symbol->symbol)) {
				if (FuFor *loop = dynamic_cast<FuFor *>(v->parent))
					loop->isIndVarUsed = true;
				else if (this->currentPureArguments.count(v) != 0)
					return this->currentPureArguments.find(v)->second;
			}
			else if (symbol->symbol->id == FuId::regexOptionsEnum)
				this->host->program->regexOptionsEnum = true;
		}
		return resolved;
	}
	std::shared_ptr<FuExpr> left = visitExpr(expr->left);
	if (left == this->poison)
		return left;
	const FuScope * scope;
	const FuSymbolReference * baseSymbol;
	bool isBase = (baseSymbol = dynamic_cast<const FuSymbolReference *>(left.get())) && baseSymbol->symbol->id == FuId::basePtr;
	if (isBase) {
		if (this->currentMethod == nullptr)
			return poisonError(left.get(), "'base' invalid outside methods");
		if (this->currentMethod->isStatic())
			return poisonError(left.get(), "'base' invalid in static context");
		const FuClass * baseClass;
		if (!(baseClass = dynamic_cast<const FuClass *>(this->currentMethod->parent->parent)))
			return poisonError(left.get(), "No base class");
		scope = baseClass;
	}
	else {
		const FuSymbolReference * leftSymbol;
		const FuScope * obj;
		if ((leftSymbol = dynamic_cast<const FuSymbolReference *>(left.get())) && (obj = dynamic_cast<const FuScope *>(leftSymbol->symbol)))
			scope = obj;
		else {
			scope = left->type.get();
			if (const FuClassType *klass = dynamic_cast<const FuClassType *>(scope))
				scope = klass->class_;
		}
	}
	std::shared_ptr<FuExpr> result = lookup(expr, scope);
	if (const FuMember *member = dynamic_cast<const FuMember *>(expr->symbol)) {
		switch (member->visibility) {
		case FuVisibility::private_:
			if (member->parent != this->currentMethod->parent || this->currentMethod->parent != scope)
				reportError(expr.get(), std::format("Cannot access private member '{}'", expr->name));
			break;
		case FuVisibility::protected_:
			if (isBase)
				break;
			{
				const FuClass * currentClass = static_cast<const FuClass *>(this->currentMethod->parent);
				const FuClass * scopeClass = static_cast<const FuClass *>(scope);
				if (!currentClass->isSameOrBaseOf(scopeClass))
					reportError(expr.get(), std::format("Cannot access protected member '{}'", expr->name));
				break;
			}
		case FuVisibility::numericElementType:
			{
				const FuClassType * klass;
				if ((klass = dynamic_cast<const FuClassType *>(left->type.get())) && !dynamic_cast<const FuNumericType *>(klass->getElementType().get()))
					reportError(expr.get(), "Method restricted to collections of numbers");
				break;
			}
		case FuVisibility::finalValueType:
			if (!left->type->asClassType()->getValueType()->isFinal())
				reportError(expr.get(), "Method restricted to dictionaries with storage values");
			break;
		default:
			switch (expr->symbol->id) {
			case FuId::arrayLength:
				if (const FuArrayStorageType *arrayStorage = dynamic_cast<const FuArrayStorageType *>(left->type.get()))
					return toLiteralLong(expr.get(), arrayStorage->length);
				break;
			case FuId::stringLength:
				if (const FuLiteralString *leftLiteral = dynamic_cast<const FuLiteralString *>(left.get())) {
					int length = leftLiteral->getAsciiLength();
					if (length >= 0)
						return toLiteralLong(expr.get(), length);
				}
				break;
			default:
				break;
			}
			break;
		}
		if (!dynamic_cast<const FuMethodGroup *>(member)) {
			const FuSymbolReference * leftType;
			if ((leftType = dynamic_cast<const FuSymbolReference *>(left.get())) && dynamic_cast<const FuType *>(leftType->symbol)) {
				if (!member->isStatic())
					reportError(expr.get(), std::format("Cannot use instance member '{}' without an object", expr->name));
			}
			else if (member->isStatic())
				reportError(expr.get(), std::format("'{}' is static", expr->name));
		}
	}
	if (result != expr)
		return result;
	std::shared_ptr<FuSymbolReference> futemp0 = std::make_shared<FuSymbolReference>();
	futemp0->loc = expr->loc;
	futemp0->left = left;
	futemp0->name = expr->name;
	futemp0->symbol = expr->symbol;
	futemp0->type = expr->type;
	return futemp0;
}

std::shared_ptr<FuRangeType> FuSema::union_(std::shared_ptr<FuRangeType> left, std::shared_ptr<FuRangeType> right)
{
	if (right == nullptr)
		return left;
	if (right->min < left->min) {
		if (right->max >= left->max)
			return right;
		return FuRangeType::new_(right->min, left->max);
	}
	if (right->max > left->max)
		return FuRangeType::new_(left->min, right->max);
	return left;
}

std::shared_ptr<FuType> FuSema::getIntegerType(const FuExpr * left, const FuExpr * right) const
{
	std::shared_ptr<FuType> type = this->host->program->system->promoteIntegerTypes(left->type.get(), right->type.get());
	coerce(left, type.get());
	coerce(right, type.get());
	return type;
}

std::shared_ptr<FuType> FuSema::getShiftType(const FuExpr * left, const FuExpr * right) const
{
	std::shared_ptr<FuIntegerType> intType = this->host->program->system->intType;
	coerce(left, intType.get());
	coerce(right, intType.get());
	return dynamic_cast<const FuRangeType *>(left->type.get()) ? intType : left->type;
}

std::shared_ptr<FuType> FuSema::getNumericType(const FuExpr * left, const FuExpr * right) const
{
	std::shared_ptr<FuType> type = this->host->program->system->promoteNumericTypes(left->type, right->type);
	coerce(left, type.get());
	coerce(right, type.get());
	return type;
}

int FuSema::saturatedNeg(int a)
{
	if (a == -2147483648)
		return 2147483647;
	return -a;
}

int FuSema::saturatedAdd(int a, int b)
{
	int c = a + b;
	if (c >= 0) {
		if (a < 0 && b < 0)
			return -2147483648;
	}
	else if (a > 0 && b > 0)
		return 2147483647;
	return c;
}

int FuSema::saturatedSub(int a, int b)
{
	if (b == -2147483648)
		return a < 0 ? a ^ b : 2147483647;
	return saturatedAdd(a, -b);
}

int FuSema::saturatedMul(int a, int b)
{
	if (a == 0 || b == 0)
		return 0;
	if (a == -2147483648)
		return b >> 31 ^ a;
	if (b == -2147483648)
		return a >> 31 ^ b;
	if (2147483647 / std::abs(a) < std::abs(b))
		return (a ^ b) >> 31 ^ 2147483647;
	return a * b;
}

int FuSema::saturatedDiv(int a, int b)
{
	if (a == -2147483648 && b == -1)
		return 2147483647;
	return a / b;
}

int FuSema::saturatedShiftRight(int a, int b)
{
	return a >> (b >= 31 || b < 0 ? 31 : b);
}

std::shared_ptr<FuRangeType> FuSema::bitwiseUnsignedOp(const FuRangeType * left, FuToken op, const FuRangeType * right)
{
	int leftVariableBits = left->getVariableBits();
	int rightVariableBits = right->getVariableBits();
	int min;
	int max;
	switch (op) {
	case FuToken::and_:
		min = left->min & right->min & ~FuRangeType::getMask(~left->min & ~right->min & (leftVariableBits | rightVariableBits));
		max = (left->max | leftVariableBits) & (right->max | rightVariableBits);
		if (max > left->max)
			max = left->max;
		if (max > right->max)
			max = right->max;
		break;
	case FuToken::or_:
		min = (left->min & ~leftVariableBits) | (right->min & ~rightVariableBits);
		max = left->max | right->max | FuRangeType::getMask(left->max & right->max & FuRangeType::getMask(leftVariableBits | rightVariableBits));
		if (min < left->min)
			min = left->min;
		if (min < right->min)
			min = right->min;
		break;
	case FuToken::xor_:
		{
			int variableBits = leftVariableBits | rightVariableBits;
			min = (left->min ^ right->min) & ~variableBits;
			max = (left->max ^ right->max) | variableBits;
			break;
		}
	default:
		std::abort();
	}
	if (min > max)
		return FuRangeType::new_(max, min);
	return FuRangeType::new_(min, max);
}

bool FuSema::isEnumOp(const FuExpr * left, const FuExpr * right) const
{
	if (dynamic_cast<const FuEnum *>(left->type.get())) {
		if (left->type->id != FuId::boolType && !dynamic_cast<const FuEnumFlags *>(left->type.get()))
			reportError(left, std::format("Define flags enumeration as 'enum* {}'", left->type->toString()));
		coerce(right, left->type.get());
		return true;
	}
	return false;
}

std::shared_ptr<FuType> FuSema::bitwiseOp(const FuExpr * left, FuToken op, const FuExpr * right) const
{
	std::shared_ptr<FuRangeType> leftRange;
	std::shared_ptr<FuRangeType> rightRange;
	if ((leftRange = std::dynamic_pointer_cast<FuRangeType>(left->type)) && (rightRange = std::dynamic_pointer_cast<FuRangeType>(right->type))) {
		std::shared_ptr<FuRangeType> range = nullptr;
		std::shared_ptr<FuRangeType> rightNegative;
		std::shared_ptr<FuRangeType> rightPositive;
		if (rightRange->min >= 0) {
			rightNegative = nullptr;
			rightPositive = rightRange;
		}
		else if (rightRange->max < 0) {
			rightNegative = rightRange;
			rightPositive = nullptr;
		}
		else {
			rightNegative = FuRangeType::new_(rightRange->min, -1);
			rightPositive = FuRangeType::new_(0, rightRange->max);
		}
		if (leftRange->min < 0) {
			const FuRangeType * leftNegative = leftRange->max < 0 ? leftRange.get() : FuRangeType::new_(leftRange->min, -1).get();
			if (rightNegative != nullptr)
				range = bitwiseUnsignedOp(leftNegative, op, rightNegative.get());
			if (rightPositive != nullptr)
				range = union_(bitwiseUnsignedOp(leftNegative, op, rightPositive.get()), range);
		}
		if (leftRange->max >= 0) {
			const FuRangeType * leftPositive = leftRange->min >= 0 ? leftRange.get() : FuRangeType::new_(0, leftRange->max).get();
			if (rightNegative != nullptr)
				range = union_(bitwiseUnsignedOp(leftPositive, op, rightNegative.get()), range);
			if (rightPositive != nullptr)
				range = union_(bitwiseUnsignedOp(leftPositive, op, rightPositive.get()), range);
		}
		return range;
	}
	if (isEnumOp(left, right))
		return left->type;
	return getIntegerType(left, right);
}

std::shared_ptr<FuRangeType> FuSema::newRangeType(int a, int b, int c, int d)
{
	if (a > b) {
		int t = a;
		a = b;
		b = t;
	}
	if (c > d) {
		int t = c;
		c = d;
		d = t;
	}
	return FuRangeType::new_(a <= c ? a : c, b >= d ? b : d);
}

std::shared_ptr<FuLiteral> FuSema::toLiteralBool(const FuExpr * expr, bool value) const
{
	std::shared_ptr<FuLiteral> result = value ? std::static_pointer_cast<FuLiteral>(std::make_shared<FuLiteralTrue>()) : std::static_pointer_cast<FuLiteral>(std::make_shared<FuLiteralFalse>());
	result->loc = expr->loc;
	result->type = this->host->program->system->boolType;
	return result;
}

std::shared_ptr<FuLiteralLong> FuSema::toLiteralLong(const FuExpr * expr, int64_t value) const
{
	return this->host->program->system->newLiteralLong(value, expr->loc);
}

std::shared_ptr<FuLiteralDouble> FuSema::toLiteralDouble(const FuExpr * expr, double value) const
{
	std::shared_ptr<FuLiteralDouble> futemp0 = std::make_shared<FuLiteralDouble>();
	futemp0->loc = expr->loc;
	futemp0->type = this->host->program->system->doubleType;
	futemp0->value = value;
	return futemp0;
}

void FuSema::checkLValue(const FuExpr * expr) const
{
	const FuBinaryExpr * indexing;
	if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr)) {
		if (FuVar *def = dynamic_cast<FuVar *>(symbol->symbol)) {
			def->isAssigned = true;
			if (FuFor *forLoop = dynamic_cast<FuFor *>(symbol->symbol->parent))
				forLoop->isRange = false;
			else if (dynamic_cast<const FuForeach *>(symbol->symbol->parent))
				reportError(expr, "Cannot assign a foreach iteration variable");
			for (FuScope * scope = this->currentScope; !dynamic_cast<const FuClass *>(scope); scope = scope->parent) {
				FuFor * forLoop;
				const FuBinaryExpr * binaryCond;
				if ((forLoop = dynamic_cast<FuFor *>(scope)) && forLoop->isRange && (binaryCond = dynamic_cast<const FuBinaryExpr *>(forLoop->cond.get())) && binaryCond->right->isReferenceTo(symbol->symbol))
					forLoop->isRange = false;
			}
		}
		else if (dynamic_cast<const FuField *>(symbol->symbol)) {
			if (symbol->left == nullptr) {
				if (!this->currentMethod->isMutator())
					reportError(expr, "Cannot modify field in a non-mutating method");
			}
			else {
				if (dynamic_cast<const FuStorageType *>(symbol->left->type.get())) {
				}
				else if (dynamic_cast<const FuReadWriteClassType *>(symbol->left->type.get())) {
				}
				else if (dynamic_cast<const FuClassType *>(symbol->left->type.get()))
					reportError(expr, "Cannot modify field through a read-only reference");
				else
					std::abort();
			}
		}
		else
			reportError(expr, "Cannot modify this");
	}
	else if ((indexing = dynamic_cast<const FuBinaryExpr *>(expr)) && indexing->op == FuToken::leftBracket) {
		if (dynamic_cast<const FuStorageType *>(indexing->left->type.get())) {
		}
		else if (dynamic_cast<const FuReadWriteClassType *>(indexing->left->type.get())) {
		}
		else if (dynamic_cast<const FuClassType *>(indexing->left->type.get()))
			reportError(expr, "Cannot modify collection through a read-only reference");
		else
			std::abort();
	}
	else
		reportError(expr, "Cannot modify this");
}

std::shared_ptr<FuInterpolatedString> FuSema::concatenate(const FuInterpolatedString * left, const FuInterpolatedString * right) const
{
	std::shared_ptr<FuInterpolatedString> result = std::make_shared<FuInterpolatedString>();
	result->loc = left->loc;
	result->type = this->host->program->system->stringStorageType;
	result->parts.insert(result->parts.end(), left->parts.begin(), left->parts.end());
	if (std::ssize(right->parts) == 0)
		result->suffix = left->suffix + right->suffix;
	else {
		result->parts.insert(result->parts.end(), right->parts.begin(), right->parts.end());
		FuInterpolatedPart * middle = &result->parts[std::ssize(left->parts)];
		middle->prefix = left->suffix + middle->prefix;
		result->suffix = right->suffix;
	}
	return result;
}

std::shared_ptr<FuInterpolatedString> FuSema::toInterpolatedString(std::shared_ptr<FuExpr> expr) const
{
	if (std::shared_ptr<FuInterpolatedString>interpolated = std::dynamic_pointer_cast<FuInterpolatedString>(expr))
		return interpolated;
	std::shared_ptr<FuInterpolatedString> result = std::make_shared<FuInterpolatedString>();
	result->loc = expr->loc;
	result->type = this->host->program->system->stringStorageType;
	if (const FuLiteral *literal = dynamic_cast<const FuLiteral *>(expr.get()))
		result->suffix = literal->getLiteralString();
	else {
		result->addPart("", expr);
		result->suffix = "";
	}
	return result;
}

void FuSema::checkComparison(const FuExpr * left, const FuExpr * right) const
{
	if (dynamic_cast<const FuEnum *>(left->type.get()))
		coerce(right, left->type.get());
	else {
		const FuType * doubleType = this->host->program->system->doubleType.get();
		coerce(left, doubleType);
		coerce(right, doubleType);
	}
}

void FuSema::openScope(FuScope * scope)
{
	scope->parent = this->currentScope;
	this->currentScope = scope;
}

void FuSema::closeScope()
{
	this->currentScope = this->currentScope->parent;
}

std::shared_ptr<FuExpr> FuSema::resolveNew(std::shared_ptr<FuPrefixExpr> expr)
{
	if (expr->type != nullptr)
		return expr;
	std::shared_ptr<FuType> type;
	const FuBinaryExpr * binaryNew;
	if ((binaryNew = dynamic_cast<const FuBinaryExpr *>(expr->inner.get())) && binaryNew->op == FuToken::leftBrace) {
		type = toType(binaryNew->left, true);
		const FuClassType * klass;
		if (!(klass = dynamic_cast<const FuClassType *>(type.get())) || dynamic_cast<const FuReadWriteClassType *>(klass))
			return poisonError(expr.get(), "Invalid argument to new");
		std::shared_ptr<FuAggregateInitializer> init = std::static_pointer_cast<FuAggregateInitializer>(binaryNew->right);
		resolveObjectLiteral(klass, init.get());
		std::shared_ptr<FuDynamicPtrType> futemp0 = std::make_shared<FuDynamicPtrType>();
		futemp0->loc = expr->loc;
		futemp0->class_ = klass->class_;
		expr->type = futemp0;
		expr->inner = init;
		return expr;
	}
	type = toType(expr->inner, true);
	if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(type.get())) {
		std::shared_ptr<FuDynamicPtrType> futemp0 = std::make_shared<FuDynamicPtrType>();
		futemp0->loc = expr->loc;
		futemp0->class_ = this->host->program->system->arrayPtrClass.get();
		futemp0->typeArg0 = array->getElementType();
		expr->type = futemp0;
		expr->inner = array->lengthExpr;
		return expr;
	}
	else if (const FuStorageType *klass = dynamic_cast<const FuStorageType *>(type.get())) {
		std::shared_ptr<FuDynamicPtrType> futemp1 = std::make_shared<FuDynamicPtrType>();
		futemp1->loc = expr->loc;
		futemp1->class_ = klass->class_;
		futemp1->typeArg0 = klass->typeArg0;
		futemp1->typeArg1 = klass->typeArg1;
		expr->type = futemp1;
		expr->inner = nullptr;
		return expr;
	}
	else
		return poisonError(expr.get(), "Invalid argument to new");
}

std::shared_ptr<FuExpr> FuSema::visitPrefixExpr(std::shared_ptr<FuPrefixExpr> expr)
{
	std::shared_ptr<FuExpr> inner;
	std::shared_ptr<FuType> type;
	switch (expr->op) {
	case FuToken::increment:
	case FuToken::decrement:
		inner = visitExpr(expr->inner);
		if (inner == this->poison)
			return inner;
		checkLValue(inner.get());
		coerce(inner.get(), this->host->program->system->doubleType.get());
		if (const FuRangeType *xcrementRange = dynamic_cast<const FuRangeType *>(inner->type.get())) {
			int delta = expr->op == FuToken::increment ? 1 : -1;
			type = FuRangeType::new_(xcrementRange->min + delta, xcrementRange->max + delta);
		}
		else
			type = inner->type;
		expr->inner = inner;
		expr->type = type;
		return expr;
	case FuToken::minus:
		inner = visitExpr(expr->inner);
		if (inner == this->poison)
			return inner;
		coerce(inner.get(), this->host->program->system->doubleType.get());
		if (const FuRangeType *negRange = dynamic_cast<const FuRangeType *>(inner->type.get())) {
			if (negRange->min == negRange->max)
				return toLiteralLong(expr.get(), -negRange->min);
			type = FuRangeType::new_(saturatedNeg(negRange->max), saturatedNeg(negRange->min));
		}
		else if (const FuLiteralDouble *d = dynamic_cast<const FuLiteralDouble *>(inner.get()))
			return toLiteralDouble(expr.get(), -d->value);
		else if (const FuLiteralLong *l = dynamic_cast<const FuLiteralLong *>(inner.get()))
			return toLiteralLong(expr.get(), -l->value);
		else
			type = inner->type;
		break;
	case FuToken::tilde:
		inner = visitExpr(expr->inner);
		if (inner == this->poison)
			return inner;
		if (dynamic_cast<const FuEnumFlags *>(inner->type.get()))
			type = inner->type;
		else {
			coerce(inner.get(), this->host->program->system->intType.get());
			if (const FuRangeType *notRange = dynamic_cast<const FuRangeType *>(inner->type.get()))
				type = FuRangeType::new_(~notRange->max, ~notRange->min);
			else
				type = inner->type;
		}
		break;
	case FuToken::exclamationMark:
		inner = resolveBool(expr->inner);
		type = this->host->program->system->boolType;
		break;
	case FuToken::new_:
		return resolveNew(expr);
	case FuToken::resource:
		{
			std::shared_ptr<FuLiteralString> resourceName;
			if (!(resourceName = std::dynamic_pointer_cast<FuLiteralString>(foldConst(expr->inner))))
				return poisonError(expr->inner.get(), "Resource name must be a string");
			inner = resourceName;
			std::shared_ptr<FuArrayStorageType> futemp0 = std::make_shared<FuArrayStorageType>();
			futemp0->class_ = this->host->program->system->arrayStorageClass.get();
			futemp0->typeArg0 = this->host->program->system->byteType;
			futemp0->length = this->host->getResourceLength(resourceName->value, expr.get());
			type = futemp0;
			break;
		}
	default:
		std::abort();
	}
	std::shared_ptr<FuPrefixExpr> futemp1 = std::make_shared<FuPrefixExpr>();
	futemp1->loc = expr->loc;
	futemp1->op = expr->op;
	futemp1->inner = inner;
	futemp1->type = type;
	return futemp1;
}

std::shared_ptr<FuExpr> FuSema::visitPostfixExpr(std::shared_ptr<FuPostfixExpr> expr)
{
	expr->inner = visitExpr(expr->inner);
	switch (expr->op) {
	case FuToken::increment:
	case FuToken::decrement:
		checkLValue(expr->inner.get());
		coerce(expr->inner.get(), this->host->program->system->doubleType.get());
		expr->type = expr->inner->type;
		return expr;
	default:
		return poisonError(expr.get(), std::format("Unexpected {}", FuLexer::tokenToString(expr->op)));
	}
}

bool FuSema::canCompareEqual(const FuType * left, const FuType * right)
{
	if (dynamic_cast<const FuNumericType *>(left))
		return dynamic_cast<const FuNumericType *>(right);
	else if (dynamic_cast<const FuEnum *>(left))
		return left == right;
	else if (const FuClassType *leftClass = dynamic_cast<const FuClassType *>(left)) {
		if (left->nullable && right->id == FuId::nullType)
			return true;
		if ((dynamic_cast<const FuStorageType *>(left) && dynamic_cast<const FuOwningType *>(right)) || (dynamic_cast<const FuDynamicPtrType *>(left) && dynamic_cast<const FuStorageType *>(right)))
			return false;
		const FuClassType * rightClass;
		return (rightClass = dynamic_cast<const FuClassType *>(right)) && (leftClass->class_->isSameOrBaseOf(rightClass->class_) || rightClass->class_->isSameOrBaseOf(leftClass->class_)) && leftClass->equalTypeArguments(rightClass);
	}
	else
		return left->id == FuId::nullType && right->nullable;
}

std::shared_ptr<FuExpr> FuSema::resolveEquality(const FuBinaryExpr * expr, std::shared_ptr<FuExpr> left, std::shared_ptr<FuExpr> right) const
{
	if (!canCompareEqual(left->type.get(), right->type.get()))
		return poisonError(expr, std::format("Cannot compare '{}' with '{}'", left->type->toString(), right->type->toString()));
	const FuRangeType * leftRange;
	const FuRangeType * rightRange;
	if ((leftRange = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightRange = dynamic_cast<const FuRangeType *>(right->type.get()))) {
		if (leftRange->min == leftRange->max && leftRange->min == rightRange->min && leftRange->min == rightRange->max)
			return toLiteralBool(expr, expr->op == FuToken::equal);
		if (leftRange->max < rightRange->min || leftRange->min > rightRange->max)
			return toLiteralBool(expr, expr->op == FuToken::notEqual);
	}
	else {
		const FuLiteralLong * leftLong;
		const FuLiteralLong * rightLong;
		const FuLiteralDouble * leftDouble;
		const FuLiteralDouble * rightDouble;
		const FuLiteralString * leftString;
		const FuLiteralString * rightString;
		if ((leftLong = dynamic_cast<const FuLiteralLong *>(left.get())) && (rightLong = dynamic_cast<const FuLiteralLong *>(right.get())))
			return toLiteralBool(expr, (expr->op == FuToken::notEqual) ^ (leftLong->value == rightLong->value));
		else if ((leftDouble = dynamic_cast<const FuLiteralDouble *>(left.get())) && (rightDouble = dynamic_cast<const FuLiteralDouble *>(right.get())))
			return toLiteralBool(expr, (expr->op == FuToken::notEqual) ^ (leftDouble->value == rightDouble->value));
		else if ((leftString = dynamic_cast<const FuLiteralString *>(left.get())) && (rightString = dynamic_cast<const FuLiteralString *>(right.get())))
			return toLiteralBool(expr, (expr->op == FuToken::notEqual) ^ (leftString->value == rightString->value));
		else if ((dynamic_cast<const FuLiteralNull *>(left.get()) && dynamic_cast<const FuLiteralNull *>(right.get())) || (dynamic_cast<const FuLiteralFalse *>(left.get()) && dynamic_cast<const FuLiteralFalse *>(right.get())) || (dynamic_cast<const FuLiteralTrue *>(left.get()) && dynamic_cast<const FuLiteralTrue *>(right.get())))
			return toLiteralBool(expr, expr->op == FuToken::equal);
		else if ((dynamic_cast<const FuLiteralFalse *>(left.get()) && dynamic_cast<const FuLiteralTrue *>(right.get())) || (dynamic_cast<const FuLiteralTrue *>(left.get()) && dynamic_cast<const FuLiteralFalse *>(right.get())))
			return toLiteralBool(expr, expr->op == FuToken::notEqual);
		if (left->isConstEnum() && right->isConstEnum())
			return toLiteralBool(expr, (expr->op == FuToken::notEqual) ^ (left->intValue() == right->intValue()));
	}
	takePtr(left.get());
	takePtr(right.get());
	std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
	futemp0->loc = expr->loc;
	futemp0->left = left;
	futemp0->op = expr->op;
	futemp0->right = right;
	futemp0->type = this->host->program->system->boolType;
	return futemp0;
}

void FuSema::setSharedAssign(const FuExpr * left, const FuExpr * right) const
{
	if (dynamic_cast<const FuDynamicPtrType *>(left->type.get()) && !right->isUnique()) {
		left->setShared();
		right->setShared();
	}
}

void FuSema::checkIsHierarchy(const FuClassType * leftPtr, const FuExpr * left, const FuClass * rightClass, const FuExpr * expr, std::string_view op, std::string_view alwaysMessage, std::string_view neverMessage) const
{
	if (rightClass->isSameOrBaseOf(leftPtr->class_))
		reportError(expr, std::format("'{}' is '{}', '{} {}' would {}", left->toString(), leftPtr->class_->name, op, rightClass->name, alwaysMessage));
	else if (!leftPtr->class_->isSameOrBaseOf(rightClass))
		reportError(expr, std::format("'{}' is not base class of '{}', '{} {}' would {}", leftPtr->class_->name, rightClass->name, op, rightClass->name, neverMessage));
}

void FuSema::checkIsVar(const FuExpr * left, const FuVar * def, const FuExpr * expr, std::string_view op, std::string_view alwaysMessage, std::string_view neverMessage) const
{
	FuClassType * rightPtr;
	if (!(rightPtr = dynamic_cast<FuClassType *>(def->type.get())) || dynamic_cast<const FuStorageType *>(rightPtr))
		reportError(def->typeExpr.get(), std::format("'{}' with non-reference type", op));
	else {
		const FuClassType * leftPtr = static_cast<const FuClassType *>(left->type.get());
		if (dynamic_cast<const FuReadWriteClassType *>(rightPtr) && !dynamic_cast<const FuDynamicPtrType *>(leftPtr) && (dynamic_cast<const FuDynamicPtrType *>(rightPtr) || !dynamic_cast<const FuReadWriteClassType *>(leftPtr)))
			reportError(def->typeExpr.get(), std::format("'{}' cannot be casted to '{}'", leftPtr->toString(), rightPtr->toString()));
		else {
			checkIsHierarchy(leftPtr, left, rightPtr->class_, expr, op, alwaysMessage, neverMessage);
			if (FuDynamicPtrType *dynamic = dynamic_cast<FuDynamicPtrType *>(rightPtr)) {
				left->setShared();
				dynamic->unique = false;
			}
		}
	}
}

std::shared_ptr<FuExpr> FuSema::resolveIs(std::shared_ptr<FuBinaryExpr> expr, std::shared_ptr<FuExpr> left, const FuExpr * right) const
{
	const FuClassType * leftPtr;
	if (!(leftPtr = dynamic_cast<const FuClassType *>(left->type.get())) || dynamic_cast<const FuStorageType *>(left->type.get()))
		return poisonError(expr.get(), "Left hand side of the 'is' operator must be an object reference");
	const FuSymbolReference * symbol;
	const FuClass * klass;
	if ((symbol = dynamic_cast<const FuSymbolReference *>(right)) && (klass = dynamic_cast<const FuClass *>(symbol->symbol)))
		checkIsHierarchy(leftPtr, left.get(), klass, expr.get(), "is", "always equal 'true'", "always equal 'false'");
	else if (const FuVar *def = dynamic_cast<const FuVar *>(right))
		checkIsVar(left.get(), def, expr.get(), "is", "always equal 'true'", "always equal 'false'");
	else
		return poisonError(expr.get(), "Right hand side of the 'is' operator must be a class name");
	expr->left = left;
	expr->type = this->host->program->system->boolType;
	return expr;
}

std::shared_ptr<FuExpr> FuSema::visitBinaryExpr(std::shared_ptr<FuBinaryExpr> expr)
{
	std::shared_ptr<FuExpr> left = visitExpr(expr->left);
	std::shared_ptr<FuExpr> right = visitExpr(expr->right);
	if (left == this->poison || left->type == this->poison || right == this->poison || right->type == this->poison)
		return this->poison;
	std::shared_ptr<FuType> type;
	switch (expr->op) {
	case FuToken::leftBracket:
		{
			const FuClassType * klass;
			if (!(klass = dynamic_cast<const FuClassType *>(left->type.get())))
				return poisonError(expr.get(), "Cannot index this object");
			switch (klass->class_->id) {
			case FuId::stringClass:
				coerce(right.get(), this->host->program->system->intType.get());
				{
					const FuRangeType * stringIndexRange;
					if ((stringIndexRange = dynamic_cast<const FuRangeType *>(right->type.get())) && stringIndexRange->max < 0)
						reportError(right.get(), "Negative index");
					else {
						const FuLiteralString * stringLiteral;
						const FuLiteralLong * indexLiteral;
						if ((stringLiteral = dynamic_cast<const FuLiteralString *>(left.get())) && (indexLiteral = dynamic_cast<const FuLiteralLong *>(right.get()))) {
							int64_t i = indexLiteral->value;
							if (i >= 0 && i <= 2147483647) {
								int c = stringLiteral->getAsciiAt(static_cast<int>(i));
								if (c >= 0)
									return FuLiteralChar::new_(c, expr->loc);
							}
						}
					}
					type = this->host->program->system->charType;
					break;
				}
			case FuId::arrayPtrClass:
			case FuId::arrayStorageClass:
			case FuId::listClass:
				coerce(right.get(), this->host->program->system->intType.get());
				if (const FuRangeType *indexRange = dynamic_cast<const FuRangeType *>(right->type.get())) {
					if (indexRange->max < 0)
						reportError(right.get(), "Negative index");
					else {
						const FuArrayStorageType * array;
						if ((array = dynamic_cast<const FuArrayStorageType *>(klass)) && indexRange->min >= array->length)
							reportError(right.get(), "Array index out of bounds");
					}
				}
				type = klass->getElementType();
				break;
			case FuId::dictionaryClass:
			case FuId::sortedDictionaryClass:
			case FuId::orderedDictionaryClass:
				coerce(right.get(), klass->getKeyType());
				type = klass->getValueType();
				break;
			default:
				return poisonError(expr.get(), "Cannot index this object");
			}
			break;
		}
	case FuToken::plus:
		{
			const FuRangeType * leftAdd;
			const FuRangeType * rightAdd;
			if ((leftAdd = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightAdd = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				type = FuRangeType::new_(saturatedAdd(leftAdd->min, rightAdd->min), saturatedAdd(leftAdd->max, rightAdd->max));
			}
			else if (dynamic_cast<const FuStringType *>(left->type.get())) {
				coerce(right.get(), this->host->program->system->stringPtrType.get());
				const FuLiteral * leftLiteral;
				const FuLiteral * rightLiteral;
				if ((leftLiteral = dynamic_cast<const FuLiteral *>(left.get())) && (rightLiteral = dynamic_cast<const FuLiteral *>(right.get())))
					return this->host->program->system->newLiteralString(leftLiteral->getLiteralString() + rightLiteral->getLiteralString(), expr->loc);
				if (dynamic_cast<const FuInterpolatedString *>(left.get()) || dynamic_cast<const FuInterpolatedString *>(right.get()))
					return concatenate(toInterpolatedString(left).get(), toInterpolatedString(right).get());
				type = this->host->program->system->stringStorageType;
			}
			else
				type = getNumericType(left.get(), right.get());
			break;
		}
	case FuToken::minus:
		{
			const FuRangeType * leftSub;
			const FuRangeType * rightSub;
			if ((leftSub = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightSub = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				type = FuRangeType::new_(saturatedSub(leftSub->min, rightSub->max), saturatedSub(leftSub->max, rightSub->min));
			}
			else
				type = getNumericType(left.get(), right.get());
			break;
		}
	case FuToken::asterisk:
		{
			const FuRangeType * leftMul;
			const FuRangeType * rightMul;
			if ((leftMul = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightMul = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				type = newRangeType(saturatedMul(leftMul->min, rightMul->min), saturatedMul(leftMul->min, rightMul->max), saturatedMul(leftMul->max, rightMul->min), saturatedMul(leftMul->max, rightMul->max));
			}
			else
				type = getNumericType(left.get(), right.get());
			break;
		}
	case FuToken::slash:
		{
			const FuRangeType * leftDiv;
			const FuRangeType * rightDiv;
			if ((leftDiv = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightDiv = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				int denMin = rightDiv->min;
				if (denMin == 0)
					denMin = 1;
				int denMax = rightDiv->max;
				if (denMax == 0)
					denMax = -1;
				type = newRangeType(saturatedDiv(leftDiv->min, denMin), saturatedDiv(leftDiv->min, denMax), saturatedDiv(leftDiv->max, denMin), saturatedDiv(leftDiv->max, denMax));
			}
			else
				type = getNumericType(left.get(), right.get());
			break;
		}
	case FuToken::mod:
		{
			const FuRangeType * leftMod;
			const FuRangeType * rightMod;
			if ((leftMod = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightMod = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				int den = ~(std::min)(rightMod->min, -rightMod->max);
				if (den < 0)
					return poisonError(expr.get(), "Mod zero");
				type = FuRangeType::new_(leftMod->min >= 0 ? 0 : (std::max)(leftMod->min, -den), leftMod->max < 0 ? 0 : (std::min)(leftMod->max, den));
			}
			else
				type = getIntegerType(left.get(), right.get());
			break;
		}
	case FuToken::and_:
	case FuToken::or_:
	case FuToken::xor_:
		type = bitwiseOp(left.get(), expr->op, right.get());
		break;
	case FuToken::shiftLeft:
		{
			const FuRangeType * leftShl;
			const FuRangeType * rightShl;
			if ((leftShl = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightShl = dynamic_cast<const FuRangeType *>(right->type.get())) && leftShl->min == leftShl->max && rightShl->min == rightShl->max) {
				int result = leftShl->min << rightShl->min;
				type = FuRangeType::new_(result, result);
			}
			else
				type = getShiftType(left.get(), right.get());
			break;
		}
	case FuToken::shiftRight:
		{
			const FuRangeType * leftShr;
			const FuRangeType * rightShr;
			if ((leftShr = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightShr = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				if (rightShr->min < 0)
					rightShr = FuRangeType::new_(0, 32).get();
				type = FuRangeType::new_(saturatedShiftRight(leftShr->min, leftShr->min < 0 ? rightShr->min : rightShr->max), saturatedShiftRight(leftShr->max, leftShr->max < 0 ? rightShr->max : rightShr->min));
			}
			else
				type = getShiftType(left.get(), right.get());
			break;
		}
	case FuToken::equal:
	case FuToken::notEqual:
		return resolveEquality(expr.get(), left, right);
	case FuToken::less:
		{
			const FuRangeType * leftLess;
			const FuRangeType * rightLess;
			if ((leftLess = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightLess = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				if (leftLess->max < rightLess->min)
					return toLiteralBool(expr.get(), true);
				if (leftLess->min >= rightLess->max)
					return toLiteralBool(expr.get(), false);
			}
			else
				checkComparison(left.get(), right.get());
			type = this->host->program->system->boolType;
			break;
		}
	case FuToken::lessOrEqual:
		{
			const FuRangeType * leftLeq;
			const FuRangeType * rightLeq;
			if ((leftLeq = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightLeq = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				if (leftLeq->max <= rightLeq->min)
					return toLiteralBool(expr.get(), true);
				if (leftLeq->min > rightLeq->max)
					return toLiteralBool(expr.get(), false);
			}
			else
				checkComparison(left.get(), right.get());
			type = this->host->program->system->boolType;
			break;
		}
	case FuToken::greater:
		{
			const FuRangeType * leftGreater;
			const FuRangeType * rightGreater;
			if ((leftGreater = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightGreater = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				if (leftGreater->min > rightGreater->max)
					return toLiteralBool(expr.get(), true);
				if (leftGreater->max <= rightGreater->min)
					return toLiteralBool(expr.get(), false);
			}
			else
				checkComparison(left.get(), right.get());
			type = this->host->program->system->boolType;
			break;
		}
	case FuToken::greaterOrEqual:
		{
			const FuRangeType * leftGeq;
			const FuRangeType * rightGeq;
			if ((leftGeq = dynamic_cast<const FuRangeType *>(left->type.get())) && (rightGeq = dynamic_cast<const FuRangeType *>(right->type.get()))) {
				if (leftGeq->min >= rightGeq->max)
					return toLiteralBool(expr.get(), true);
				if (leftGeq->max < rightGeq->min)
					return toLiteralBool(expr.get(), false);
			}
			else
				checkComparison(left.get(), right.get());
			type = this->host->program->system->boolType;
			break;
		}
	case FuToken::condAnd:
		coerce(left.get(), this->host->program->system->boolType.get());
		coerce(right.get(), this->host->program->system->boolType.get());
		if (dynamic_cast<const FuLiteralTrue *>(left.get()))
			return right;
		if (dynamic_cast<const FuLiteralFalse *>(left.get()) || dynamic_cast<const FuLiteralTrue *>(right.get()))
			return left;
		type = this->host->program->system->boolType;
		break;
	case FuToken::condOr:
		coerce(left.get(), this->host->program->system->boolType.get());
		coerce(right.get(), this->host->program->system->boolType.get());
		if (dynamic_cast<const FuLiteralTrue *>(left.get()) || dynamic_cast<const FuLiteralFalse *>(right.get()))
			return left;
		if (dynamic_cast<const FuLiteralFalse *>(left.get()))
			return right;
		type = this->host->program->system->boolType;
		break;
	case FuToken::assign:
		checkLValue(left.get());
		coercePermanent(right.get(), left->type.get());
		{
			const FuSymbolReference * symbol;
			const FuNamedValue * storageDef;
			if (dynamic_cast<const FuStorageType *>(left->type.get()) && dynamic_cast<const FuSymbolReference *>(right.get()) && !((symbol = dynamic_cast<const FuSymbolReference *>(left.get())) && (storageDef = dynamic_cast<const FuNamedValue *>(symbol->symbol)) && storageDef->isAssignableStorage()))
				reportError(right.get(), "Cannot copy object storage");
			setSharedAssign(left.get(), right.get());
			expr->left = left;
			expr->right = right;
			expr->type = left->type;
			return expr;
		}
	case FuToken::addAssign:
		checkLValue(left.get());
		if (left->type->id == FuId::stringStorageType)
			coerce(right.get(), this->host->program->system->stringPtrType.get());
		else {
			coerce(left.get(), this->host->program->system->doubleType.get());
			coerce(right.get(), left->type.get());
		}
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case FuToken::subAssign:
	case FuToken::mulAssign:
	case FuToken::divAssign:
		checkLValue(left.get());
		coerce(left.get(), this->host->program->system->doubleType.get());
		coerce(right.get(), left->type.get());
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case FuToken::modAssign:
	case FuToken::shiftLeftAssign:
	case FuToken::shiftRightAssign:
		checkLValue(left.get());
		coerce(left.get(), this->host->program->system->intType.get());
		coerce(right.get(), this->host->program->system->intType.get());
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case FuToken::andAssign:
	case FuToken::orAssign:
	case FuToken::xorAssign:
		checkLValue(left.get());
		if (!isEnumOp(left.get(), right.get())) {
			coerce(left.get(), this->host->program->system->intType.get());
			coerce(right.get(), this->host->program->system->intType.get());
		}
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case FuToken::is:
		return resolveIs(expr, left, right.get());
	case FuToken::range:
		return poisonError(expr.get(), "Range within an expression");
	default:
		std::abort();
	}
	const FuRangeType * range;
	if ((range = dynamic_cast<const FuRangeType *>(type.get())) && range->min == range->max)
		return toLiteralLong(expr.get(), range->min);
	std::shared_ptr<FuBinaryExpr> futemp0 = std::make_shared<FuBinaryExpr>();
	futemp0->loc = expr->loc;
	futemp0->left = left;
	futemp0->op = expr->op;
	futemp0->right = right;
	futemp0->type = type;
	return futemp0;
}

std::shared_ptr<FuType> FuSema::tryGetPtr(std::shared_ptr<FuType> type, bool nullable) const
{
	if (type->id == FuId::stringStorageType)
		return nullable ? this->host->program->system->stringNullablePtrType : this->host->program->system->stringPtrType;
	if (const FuStorageType *storage = dynamic_cast<const FuStorageType *>(type.get())) {
		std::shared_ptr<FuReadWriteClassType> futemp0 = std::make_shared<FuReadWriteClassType>();
		futemp0->class_ = storage->class_->id == FuId::arrayStorageClass ? this->host->program->system->arrayPtrClass.get() : storage->class_;
		futemp0->nullable = nullable;
		futemp0->typeArg0 = storage->typeArg0;
		futemp0->typeArg1 = storage->typeArg1;
		return futemp0;
	}
	const FuClassType * ptr;
	if (nullable && (ptr = dynamic_cast<const FuClassType *>(type.get())) && !ptr->nullable) {
		std::shared_ptr<FuClassType> result;
		if (dynamic_cast<const FuDynamicPtrType *>(type.get()))
			result = std::make_shared<FuDynamicPtrType>();
		else if (dynamic_cast<const FuReadWriteClassType *>(type.get()))
			result = std::make_shared<FuReadWriteClassType>();
		else
			result = std::make_shared<FuClassType>();
		result->class_ = ptr->class_;
		result->nullable = true;
		result->typeArg0 = ptr->typeArg0;
		result->typeArg1 = ptr->typeArg1;
		return result;
	}
	return type;
}

const FuClass * FuSema::getLowestCommonAncestor(const FuClass * left, const FuClass * right)
{
	for (;;) {
		if (left->isSameOrBaseOf(right))
			return left;
		if (const FuClass *parent = dynamic_cast<const FuClass *>(left->parent))
			left = parent;
		else
			return nullptr;
	}
}

std::shared_ptr<FuType> FuSema::getCommonType(const FuExpr * left, const FuExpr * right) const
{
	std::shared_ptr<FuRangeType> leftRange;
	std::shared_ptr<FuRangeType> rightRange;
	if ((leftRange = std::dynamic_pointer_cast<FuRangeType>(left->type)) && (rightRange = std::dynamic_pointer_cast<FuRangeType>(right->type)))
		return union_(leftRange, rightRange);
	bool nullable = left->type->nullable || right->type->nullable;
	std::shared_ptr<FuType> ptr = tryGetPtr(left->type, nullable);
	if (ptr->isAssignableFrom(right->type.get()))
		return ptr;
	ptr = tryGetPtr(right->type, nullable);
	if (ptr->isAssignableFrom(left->type.get()))
		return ptr;
	const FuClassType * leftClass;
	const FuClassType * rightClass;
	if ((leftClass = dynamic_cast<const FuClassType *>(left->type.get())) && (rightClass = dynamic_cast<const FuClassType *>(right->type.get())) && leftClass->equalTypeArguments(rightClass)) {
		const FuClass * klass = getLowestCommonAncestor(leftClass->class_, rightClass->class_);
		if (klass != nullptr) {
			std::shared_ptr<FuClassType> result;
			if (!dynamic_cast<const FuReadWriteClassType *>(leftClass) || !dynamic_cast<const FuReadWriteClassType *>(rightClass))
				result = std::make_shared<FuClassType>();
			else if (dynamic_cast<const FuDynamicPtrType *>(leftClass) && dynamic_cast<const FuDynamicPtrType *>(rightClass))
				result = std::make_shared<FuDynamicPtrType>();
			else
				result = std::make_shared<FuReadWriteClassType>();
			result->class_ = klass;
			result->nullable = nullable;
			result->typeArg0 = leftClass->typeArg0;
			result->typeArg1 = leftClass->typeArg1;
			return result;
		}
	}
	return poisonError(left, std::format("Incompatible types: '{}' and '{}'", left->type->toString(), right->type->toString()));
}

std::shared_ptr<FuExpr> FuSema::visitSelectExpr(const FuSelectExpr * expr)
{
	std::shared_ptr<FuExpr> cond = resolveBool(expr->cond);
	std::shared_ptr<FuExpr> onTrue = visitExpr(expr->onTrue);
	std::shared_ptr<FuExpr> onFalse = visitExpr(expr->onFalse);
	if (onTrue == this->poison || onTrue->type == this->poison || onFalse == this->poison || onFalse->type == this->poison)
		return this->poison;
	std::shared_ptr<FuType> type = getCommonType(onTrue.get(), onFalse.get());
	coerce(onTrue.get(), type.get());
	coerce(onFalse.get(), type.get());
	if (dynamic_cast<const FuLiteralTrue *>(cond.get()))
		return onTrue;
	if (dynamic_cast<const FuLiteralFalse *>(cond.get()))
		return onFalse;
	std::shared_ptr<FuSelectExpr> futemp0 = std::make_shared<FuSelectExpr>();
	futemp0->loc = expr->loc;
	futemp0->cond = cond;
	futemp0->onTrue = onTrue;
	futemp0->onFalse = onFalse;
	futemp0->type = type;
	return futemp0;
}

std::shared_ptr<FuType> FuSema::evalType(const FuClassType * generic, std::shared_ptr<FuType> type) const
{
	if (type->id == FuId::typeParam0)
		return generic->typeArg0;
	if (type->id == FuId::typeParam0NotFinal)
		return generic->typeArg0->isFinal() ? nullptr : generic->typeArg0;
	const FuClassType * collection;
	if ((collection = dynamic_cast<const FuClassType *>(type.get())) && collection->class_->typeParameterCount == 1 && collection->typeArg0->id == FuId::typeParam0) {
		std::shared_ptr<FuClassType> result = dynamic_cast<const FuReadWriteClassType *>(type.get()) ? std::make_shared<FuReadWriteClassType>() : std::make_shared<FuClassType>();
		result->class_ = collection->class_;
		result->typeArg0 = generic->typeArg0;
		return result;
	}
	return type;
}

bool FuSema::canCall(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * arguments) const
{
	const FuVar * param = method->firstParameter();
	for (const std::shared_ptr<FuExpr> &arg : *arguments) {
		if (param == nullptr)
			return false;
		std::shared_ptr<FuType> type = param->type;
		const FuClassType * generic;
		if (obj != nullptr && (generic = dynamic_cast<const FuClassType *>(obj->type.get())))
			type = evalType(generic, type);
		if (!type->isAssignableFrom(arg->type.get()))
			return false;
		param = param->nextVar();
	}
	return param == nullptr || param->value != nullptr;
}

bool FuSema::methodHasThrows(const FuMethodBase * method, const FuClass * exception)
{
	return std::any_of(method->throws.begin(), method->throws.end(), [&](const std::shared_ptr<FuThrowsDeclaration> &symbol) { const FuClass * klass;
	return (klass = dynamic_cast<const FuClass *>(symbol->symbol)) && klass->isSameOrBaseOf(exception); });
}

std::shared_ptr<FuExpr> FuSema::resolveCallWithArguments(std::shared_ptr<FuCallExpr> expr, const std::vector<std::shared_ptr<FuExpr>> * arguments)
{
	std::shared_ptr<FuSymbolReference> symbol;
	if (!(symbol = std::dynamic_pointer_cast<FuSymbolReference>(visitExpr(expr->method))))
		return this->poison;
	FuMethod * method;
	if (symbol->symbol == nullptr)
		return this->poison;
	else if (FuMethod *m = dynamic_cast<FuMethod *>(symbol->symbol))
		method = m;
	else if (const FuMethodGroup *group = dynamic_cast<const FuMethodGroup *>(symbol->symbol)) {
		method = group->methods[0].get();
		if (!canCall(symbol->left.get(), method, arguments))
			method = group->methods[1].get();
	}
	else
		return poisonError(symbol.get(), "Expected a method");
	if (!method->isStatic() && method->isMutator()) {
		if (symbol->left == nullptr) {
			if (!this->currentMethod->isMutator())
				reportError(expr->method.get(), std::format("Cannot call mutating method '{}' from a non-mutating method", method->name));
		}
		else {
			const FuSymbolReference * baseRef;
			if ((baseRef = dynamic_cast<const FuSymbolReference *>(symbol->left.get())) && baseRef->symbol->id == FuId::basePtr) {
			}
			else if (!dynamic_cast<const FuReadWriteClassType *>(symbol->left->type.get())) {
				switch (method->id) {
				case FuId::intTryParse:
				case FuId::longTryParse:
				case FuId::doubleTryParse:
					{
						const FuSymbolReference * varRef;
						FuVar * def;
						if ((varRef = dynamic_cast<const FuSymbolReference *>(symbol->left.get())) && (def = dynamic_cast<FuVar *>(varRef->symbol)))
							def->isAssigned = true;
						break;
					}
				default:
					reportError(symbol->left.get(), std::format("Cannot call mutating method '{}' on a read-only reference", method->name));
					break;
				}
			}
		}
	}
	int i = 0;
	for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
		std::shared_ptr<FuType> type = param->type;
		const FuClassType * generic;
		if (symbol->left != nullptr && (generic = dynamic_cast<const FuClassType *>(symbol->left->type.get()))) {
			type = evalType(generic, type);
			if (type == nullptr)
				continue;
		}
		if (i >= std::ssize(*arguments)) {
			if (param->value != nullptr)
				break;
			return poisonError(expr.get(), std::format("Too few arguments for '{}'", method->name));
		}
		FuExpr * arg = (*arguments)[i++].get();
		FuLambdaExpr * lambda;
		if (type->id == FuId::typeParam0Predicate && (lambda = dynamic_cast<FuLambdaExpr *>(arg))) {
			lambda->first->type = symbol->left->type->asClassType()->typeArg0;
			openScope(lambda);
			lambda->body = visitExpr(lambda->body);
			closeScope();
			coerce(lambda->body.get(), this->host->program->system->boolType.get());
			coerce(lambda->body.get(), this->host->program->system->boolType.get());
		}
		else {
			coerce(arg, type.get());
			if (dynamic_cast<const FuDynamicPtrType *>(type.get()))
				arg->setShared();
		}
	}
	if (i < std::ssize(*arguments))
		return poisonError((*arguments)[i].get(), std::format("Too many arguments for '{}'", method->name));
	for (const std::shared_ptr<FuThrowsDeclaration> &exceptionDecl : method->throws) {
		if (this->currentMethod == nullptr) {
			reportError(expr->method.get(), std::format("Cannot call method '{}' here because it is marked 'throws'", method->name));
			break;
		}
		const FuClass * exception;
		if ((exception = dynamic_cast<const FuClass *>(exceptionDecl->symbol)) && !methodHasThrows(this->currentMethod, exception))
			reportError(expr->method.get(), std::format("Method marked 'throws {}' called from a method without it", exception->name));
	}
	symbol->symbol = method;
	FuReturn * ret;
	if (method->isStatic() && (ret = dynamic_cast<FuReturn *>(method->body.get())) && std::all_of(arguments->begin(), arguments->end(), [](const std::shared_ptr<FuExpr> &arg) { return dynamic_cast<const FuLiteral *>(arg.get()); }) && !this->currentPureMethods.contains(method)) {
		this->currentPureMethods.insert(method);
		i = 0;
		for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
			if (i < std::ssize(*arguments))
				this->currentPureArguments[param] = (*arguments)[i++];
			else
				this->currentPureArguments[param] = param->value;
		}
		FuScope * callSite = this->currentScope;
		ret->parent = &method->parameters;
		this->currentScope = ret;
		std::shared_ptr<FuExpr> result = visitExpr(ret->value);
		this->currentScope = callSite;
		for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar())
			this->currentPureArguments.erase(param);
		this->currentPureMethods.erase(method);
		if (dynamic_cast<const FuLiteral *>(result.get()))
			return result;
	}
	if (this->currentMethod != nullptr)
		this->currentMethod->calls.insert(method);
	if (std::ssize(this->currentPureArguments) == 0) {
		expr->method = symbol;
		std::shared_ptr<FuType> type = method->type;
		const FuClassType * generic;
		if (symbol->left != nullptr && (generic = dynamic_cast<const FuClassType *>(symbol->left->type.get())))
			type = evalType(generic, type);
		else if (std::any_of(arguments->begin(), arguments->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type == nullptr; })) {
		}
		else if (type->id == FuId::floatingType)
			type = std::any_of(arguments->begin(), arguments->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::doubleType; }) ? this->host->program->system->doubleType : this->host->program->system->floatType;
		else if (type->id == FuId::numericType) {
			type = std::any_of(arguments->begin(), arguments->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::doubleType; }) ? this->host->program->system->doubleType : std::any_of(arguments->begin(), arguments->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::floatType; }) ? std::static_pointer_cast<FuType>(this->host->program->system->floatType) : std::static_pointer_cast<FuType>(std::any_of(arguments->begin(), arguments->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::longType; }) ? this->host->program->system->longType : this->host->program->system->intType);
		}
		expr->type = type;
	}
	return expr;
}

std::shared_ptr<FuExpr> FuSema::visitCallExpr(std::shared_ptr<FuCallExpr> expr)
{
	if (std::ssize(this->currentPureArguments) == 0) {
		std::vector<std::shared_ptr<FuExpr>> * arguments = &expr->arguments;
		for (int i = 0; i < std::ssize(*arguments); i++) {
			if (!dynamic_cast<const FuLambdaExpr *>((*arguments)[i].get()))
				(*arguments)[i] = visitExpr((*arguments)[i]);
		}
		return resolveCallWithArguments(expr, arguments);
	}
	else {
		std::vector<std::shared_ptr<FuExpr>> arguments;
		for (const std::shared_ptr<FuExpr> &arg : expr->arguments)
			arguments.push_back(visitExpr(arg));
		return resolveCallWithArguments(expr, &arguments);
	}
}

void FuSema::resolveObjectLiteral(const FuClassType * klass, const FuAggregateInitializer * init)
{
	for (const std::shared_ptr<FuExpr> &item : init->items) {
		FuBinaryExpr * field = static_cast<FuBinaryExpr *>(item.get());
		assert(field->op == FuToken::assign);
		std::shared_ptr<FuSymbolReference> symbol = std::static_pointer_cast<FuSymbolReference>(field->left);
		lookup(symbol, klass->class_);
		if (dynamic_cast<const FuField *>(symbol->symbol)) {
			field->right = visitExpr(field->right);
			coerce(field->right.get(), symbol->type.get());
			setSharedAssign(field->left.get(), field->right.get());
		}
		else
			reportError(field->left.get(), "Expected a field");
	}
}

void FuSema::initUnique(const FuNamedValue * varOrField)
{
	if (FuDynamicPtrType *dynamic = dynamic_cast<FuDynamicPtrType *>(varOrField->type.get())) {
		if (varOrField->value == nullptr || varOrField->value->isUnique())
			dynamic->unique = true;
		else
			varOrField->value->setShared();
	}
}

void FuSema::visitVar(std::shared_ptr<FuVar> expr)
{
	const FuType * type = resolveType(expr.get()).get();
	if (expr->value != nullptr) {
		const FuStorageType * storage;
		const FuAggregateInitializer * init;
		if ((storage = dynamic_cast<const FuStorageType *>(type)) && (init = dynamic_cast<const FuAggregateInitializer *>(expr->value.get())))
			resolveObjectLiteral(storage, init);
		else {
			expr->value = visitExpr(expr->value);
			if (!expr->isAssignableStorage()) {
				if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(type)) {
					type = array->getElementType().get();
					const FuLiteral * literal;
					if (!(literal = dynamic_cast<const FuLiteral *>(expr->value.get())) || !literal->isDefaultValue())
						reportError(expr->value.get(), "Only null, zero and false supported as an array initializer");
				}
				else if (dynamic_cast<const FuStorageType *>(type) && dynamic_cast<const FuSymbolReference *>(expr->value.get()))
					reportError(expr->value.get(), "Cannot copy object storage");
				coercePermanent(expr->value.get(), type);
			}
		}
	}
	initUnique(expr.get());
	this->currentScope->add(expr);
}

std::shared_ptr<FuExpr> FuSema::visitExpr(std::shared_ptr<FuExpr> expr)
{
	if (FuAggregateInitializer *aggregate = dynamic_cast<FuAggregateInitializer *>(expr.get())) {
		std::vector<std::shared_ptr<FuExpr>> * items = &aggregate->items;
		for (int i = 0; i < std::ssize(*items); i++)
			(*items)[i] = visitExpr((*items)[i]);
		return expr;
	}
	else if (dynamic_cast<const FuLiteral *>(expr.get()))
		return expr;
	else if (std::shared_ptr<FuInterpolatedString>interpolated = std::dynamic_pointer_cast<FuInterpolatedString>(expr))
		return visitInterpolatedString(interpolated);
	else if (std::shared_ptr<FuSymbolReference>symbol = std::dynamic_pointer_cast<FuSymbolReference>(expr))
		return visitSymbolReference(symbol);
	else if (std::shared_ptr<FuPrefixExpr>prefix = std::dynamic_pointer_cast<FuPrefixExpr>(expr))
		return visitPrefixExpr(prefix);
	else if (std::shared_ptr<FuPostfixExpr>postfix = std::dynamic_pointer_cast<FuPostfixExpr>(expr))
		return visitPostfixExpr(postfix);
	else if (std::shared_ptr<FuBinaryExpr>binary = std::dynamic_pointer_cast<FuBinaryExpr>(expr))
		return visitBinaryExpr(binary);
	else if (const FuSelectExpr *select = dynamic_cast<const FuSelectExpr *>(expr.get()))
		return visitSelectExpr(select);
	else if (std::shared_ptr<FuCallExpr>call = std::dynamic_pointer_cast<FuCallExpr>(expr))
		return visitCallExpr(call);
	else if (dynamic_cast<const FuLambdaExpr *>(expr.get())) {
		reportError(expr.get(), "Unexpected lambda expression");
		return expr;
	}
	else if (std::shared_ptr<FuVar>def = std::dynamic_pointer_cast<FuVar>(expr)) {
		visitVar(def);
		return expr;
	}
	else {
		if (expr == this->poison)
			return expr;
		std::abort();
	}
}

std::shared_ptr<FuExpr> FuSema::resolveBool(std::shared_ptr<FuExpr> expr)
{
	expr = visitExpr(expr);
	coerce(expr.get(), this->host->program->system->boolType.get());
	return expr;
}

std::shared_ptr<FuClassType> FuSema::createClassPtr(const FuClass * klass, FuToken ptrModifier, bool nullable)
{
	std::shared_ptr<FuClassType> ptr;
	switch (ptrModifier) {
	case FuToken::endOfFile:
		ptr = std::make_shared<FuClassType>();
		break;
	case FuToken::exclamationMark:
		ptr = std::make_shared<FuReadWriteClassType>();
		break;
	case FuToken::hash:
		ptr = std::make_shared<FuDynamicPtrType>();
		break;
	default:
		std::abort();
	}
	ptr->class_ = klass;
	ptr->nullable = nullable;
	return ptr;
}

void FuSema::fillGenericClass(FuClassType * result, const FuClass * klass, const FuAggregateInitializer * typeArgExprs)
{
	std::vector<std::shared_ptr<FuType>> typeArgs;
	for (const std::shared_ptr<FuExpr> &typeArgExpr : typeArgExprs->items)
		typeArgs.push_back(toType(typeArgExpr, false));
	if (std::ssize(typeArgs) != klass->typeParameterCount) {
		reportError(typeArgExprs, std::format("Expected {} type arguments for '{}', got {}", klass->typeParameterCount, klass->name, std::ssize(typeArgs)));
		return;
	}
	result->class_ = klass;
	result->typeArg0 = typeArgs[0];
	if (std::ssize(typeArgs) == 2)
		result->typeArg1 = typeArgs[1];
}

bool FuSema::expectNoPtrModifier(const FuExpr * expr, FuToken ptrModifier, bool nullable) const
{
	if (ptrModifier != FuToken::endOfFile)
		reportError(expr, std::format("Unexpected {} on a non-reference type", FuLexer::tokenToString(ptrModifier)));
	if (nullable) {
		reportError(expr, "Nullable value types not supported");
		return false;
	}
	return ptrModifier == FuToken::endOfFile;
}

std::shared_ptr<FuType> FuSema::toBaseType(FuExpr * expr, FuToken ptrModifier, bool nullable)
{
	if (FuSymbolReference *symbol = dynamic_cast<FuSymbolReference *>(expr)) {
		if (std::shared_ptr<FuType>type = std::dynamic_pointer_cast<FuType>(this->host->program->tryLookup(symbol->name, true))) {
			symbol->symbol = type.get();
			if (const FuClass *klass = dynamic_cast<const FuClass *>(type.get())) {
				if (klass->id == FuId::matchClass && ptrModifier != FuToken::endOfFile)
					reportError(expr, "Read-write references to the built-in class Match are not supported");
				std::shared_ptr<FuClassType> ptr = createClassPtr(klass, ptrModifier, nullable);
				if (const FuAggregateInitializer *typeArgExprs = dynamic_cast<const FuAggregateInitializer *>(symbol->left.get()))
					fillGenericClass(ptr.get(), klass, typeArgExprs);
				else if (symbol->left != nullptr)
					return poisonError(expr, "Invalid type");
				else
					ptr->name = klass->name;
				return ptr;
			}
			else if (symbol->left != nullptr)
				return poisonError(expr, "Invalid type");
			if (type->id == FuId::stringPtrType && nullable) {
				type = this->host->program->system->stringNullablePtrType;
				nullable = false;
			}
			return expectNoPtrModifier(expr, ptrModifier, nullable) ? type : this->poison;
		}
		return poisonError(expr, std::format("Type '{}' not found", symbol->name));
	}
	else if (const FuCallExpr *call = dynamic_cast<const FuCallExpr *>(expr)) {
		if (!expectNoPtrModifier(expr, ptrModifier, nullable))
			return this->poison;
		if (std::ssize(call->arguments) != 0)
			return poisonError(call, "Expected empty parentheses for storage type");
		if (const FuAggregateInitializer *typeArgExprs2 = dynamic_cast<const FuAggregateInitializer *>(call->method->left.get())) {
			if (const FuClass *klass = dynamic_cast<const FuClass *>(this->host->program->tryLookup(call->method->name, true).get())) {
				std::shared_ptr<FuStorageType> storage = std::make_shared<FuStorageType>();
				fillGenericClass(storage.get(), klass, typeArgExprs2);
				return storage;
			}
			return poisonError(typeArgExprs2, std::format("'{}' is not a class", call->method->name));
		}
		else if (call->method->left != nullptr)
			return poisonError(expr, "Invalid type");
		if (call->method->name == "string")
			return this->host->program->system->stringStorageType;
		if (FuClass *klass2 = dynamic_cast<FuClass *>(this->host->program->tryLookup(call->method->name, true).get())) {
			call->method->symbol = klass2;
			std::shared_ptr<FuStorageType> futemp0 = std::make_shared<FuStorageType>();
			futemp0->class_ = klass2;
			return futemp0;
		}
		return poisonError(expr, std::format("Class '{}' not found", call->method->name));
	}
	else
		return poisonError(expr, "Invalid type");
}

std::shared_ptr<FuType> FuSema::toType(std::shared_ptr<FuExpr> expr, bool dynamic)
{
	std::shared_ptr<FuExpr> minExpr = nullptr;
	const FuBinaryExpr * range;
	if ((range = dynamic_cast<const FuBinaryExpr *>(expr.get())) && range->op == FuToken::range) {
		minExpr = range->left;
		expr = range->right;
	}
	bool nullable;
	FuToken ptrModifier;
	std::shared_ptr<FuClassType> outerArray = nullptr;
	FuClassType * innerArray = nullptr;
	for (;;) {
		const FuPostfixExpr * question;
		if ((question = dynamic_cast<const FuPostfixExpr *>(expr.get())) && question->op == FuToken::questionMark) {
			expr = question->inner;
			nullable = true;
		}
		else
			nullable = false;
		const FuPostfixExpr * postfix;
		if ((postfix = dynamic_cast<const FuPostfixExpr *>(expr.get())) && (postfix->op == FuToken::exclamationMark || postfix->op == FuToken::hash)) {
			expr = postfix->inner;
			ptrModifier = postfix->op;
		}
		else
			ptrModifier = FuToken::endOfFile;
		const FuBinaryExpr * binary;
		if ((binary = dynamic_cast<const FuBinaryExpr *>(expr.get())) && binary->op == FuToken::leftBracket) {
			if (binary->right != nullptr) {
				if (!expectNoPtrModifier(expr.get(), ptrModifier, nullable))
					return this->poison;
				std::shared_ptr<FuExpr> lengthExpr = visitExpr(binary->right);
				std::shared_ptr<FuArrayStorageType> arrayStorage = std::make_shared<FuArrayStorageType>();
				arrayStorage->class_ = this->host->program->system->arrayStorageClass.get();
				arrayStorage->typeArg0 = outerArray;
				arrayStorage->lengthExpr = lengthExpr;
				arrayStorage->length = 0;
				if (coerce(lengthExpr.get(), this->host->program->system->intType.get()) && (!dynamic || binary->left->isIndexing())) {
					if (const FuLiteralLong *literal = dynamic_cast<const FuLiteralLong *>(lengthExpr.get())) {
						int64_t length = literal->value;
						if (length < 0)
							reportError(expr.get(), "Expected non-negative integer");
						else if (length > 2147483647)
							reportError(expr.get(), "Integer too big");
						else
							arrayStorage->length = static_cast<int>(length);
					}
					else
						reportError(lengthExpr.get(), "Expected constant value");
				}
				outerArray = arrayStorage;
			}
			else {
				std::shared_ptr<FuType> elementType = outerArray;
				outerArray = createClassPtr(this->host->program->system->arrayPtrClass.get(), ptrModifier, nullable);
				outerArray->typeArg0 = elementType;
			}
			if (innerArray == nullptr)
				innerArray = outerArray.get();
			expr = binary->left;
		}
		else
			break;
	}
	std::shared_ptr<FuType> baseType;
	if (minExpr != nullptr) {
		if (!expectNoPtrModifier(expr.get(), ptrModifier, nullable))
			return this->poison;
		int min = foldConstInt(minExpr);
		int max = foldConstInt(expr);
		if (min > max)
			return poisonError(expr.get(), "Range min greater than max");
		baseType = FuRangeType::new_(min, max);
	}
	else
		baseType = toBaseType(expr.get(), ptrModifier, nullable);
	if (outerArray == nullptr)
		return baseType;
	innerArray->typeArg0 = baseType;
	return outerArray;
}

std::shared_ptr<FuType> FuSema::resolveType(FuNamedValue * def)
{
	def->type = toType(def->typeExpr, false);
	return def->type;
}

void FuSema::visitAssert(FuAssert * statement)
{
	statement->cond = resolveBool(statement->cond);
	if (statement->message != nullptr) {
		statement->message = visitExpr(statement->message);
		if (!dynamic_cast<const FuStringType *>(statement->message->type.get()))
			reportError(statement->message.get(), "The second argument of 'assert' must be a string");
	}
}

bool FuSema::resolveStatements(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	bool reachable = true;
	for (const std::shared_ptr<FuStatement> &statement : *statements) {
		if (std::shared_ptr<FuConst>konst = std::dynamic_pointer_cast<FuConst>(statement)) {
			resolveConst(konst.get());
			this->currentScope->add(konst);
			if (dynamic_cast<const FuArrayStorageType *>(konst->type.get())) {
				FuClass * klass = static_cast<FuClass *>(getCurrentContainer());
				FuConst * last = nullptr;
				for (FuConst * previous : klass->constArrays) {
					if (previous->name == konst->name && previous->inMethod == konst->inMethod)
						last = previous;
				}
				if (last != nullptr) {
					if (last->inMethodIndex == 0)
						last->inMethodIndex = 1;
					konst->inMethodIndex = last->inMethodIndex + 1;
				}
				klass->constArrays.push_back(konst.get());
			}
		}
		else
			visitStatement(statement);
		if (!reachable) {
			reportError(statement.get(), "Unreachable statement");
			return false;
		}
		reachable = statement->completesNormally();
	}
	return reachable;
}

void FuSema::checkInitialized(const FuVar * def)
{
	if (def->type == this->poison || def->isAssigned)
		return;
	if (dynamic_cast<const FuStorageType *>(def->type.get()) ? !dynamic_cast<const FuArrayStorageType *>(def->type.get()) && dynamic_cast<const FuLiteralNull *>(def->value.get()) : def->value == nullptr)
		reportError(def, "Uninitialized variable");
}

void FuSema::visitBlock(FuBlock * statement)
{
	openScope(statement);
	statement->setCompletesNormally(resolveStatements(&statement->statements));
	for (const FuSymbol * symbol = statement->first; symbol != nullptr; symbol = symbol->next) {
		if (const FuVar *def = dynamic_cast<const FuVar *>(symbol))
			checkInitialized(def);
	}
	closeScope();
}

void FuSema::resolveLoopCond(FuLoop * statement)
{
	if (statement->cond != nullptr) {
		statement->cond = resolveBool(statement->cond);
		statement->setCompletesNormally(!dynamic_cast<const FuLiteralTrue *>(statement->cond.get()));
	}
	else
		statement->setCompletesNormally(false);
}

void FuSema::visitDoWhile(FuDoWhile * statement)
{
	openScope(statement);
	resolveLoopCond(statement);
	visitStatement(statement->body);
	closeScope();
}

void FuSema::visitFor(FuFor * statement)
{
	openScope(statement);
	if (statement->init != nullptr)
		visitStatement(statement->init);
	resolveLoopCond(statement);
	if (statement->advance != nullptr)
		visitStatement(statement->advance);
	const FuVar * indVar;
	const FuBinaryExpr * cond;
	const FuSymbolReference * limitSymbol;
	if ((indVar = dynamic_cast<const FuVar *>(statement->init.get())) && dynamic_cast<const FuIntegerType *>(indVar->type.get()) && indVar->value != nullptr && (cond = dynamic_cast<const FuBinaryExpr *>(statement->cond.get())) && cond->left->isReferenceTo(indVar) && (dynamic_cast<const FuLiteral *>(cond->right.get()) || ((limitSymbol = dynamic_cast<const FuSymbolReference *>(cond->right.get())) && dynamic_cast<const FuVar *>(limitSymbol->symbol))) && statement->advance != nullptr) {
		int64_t step = 0;
		const FuUnaryExpr * unary;
		const FuBinaryExpr * binary;
		const FuLiteralLong * literalStep;
		if ((unary = dynamic_cast<const FuUnaryExpr *>(statement->advance.get())) && unary->inner != nullptr && unary->inner->isReferenceTo(indVar)) {
			switch (unary->op) {
			case FuToken::increment:
				step = 1;
				break;
			case FuToken::decrement:
				step = -1;
				break;
			default:
				break;
			}
		}
		else if ((binary = dynamic_cast<const FuBinaryExpr *>(statement->advance.get())) && binary->left->isReferenceTo(indVar) && (literalStep = dynamic_cast<const FuLiteralLong *>(binary->right.get()))) {
			switch (binary->op) {
			case FuToken::addAssign:
				step = literalStep->value;
				break;
			case FuToken::subAssign:
				step = -literalStep->value;
				break;
			default:
				break;
			}
		}
		if ((step > 0 && (cond->op == FuToken::less || cond->op == FuToken::lessOrEqual)) || (step < 0 && (cond->op == FuToken::greater || cond->op == FuToken::greaterOrEqual))) {
			statement->isRange = true;
			statement->rangeStep = step;
			statement->isIndVarUsed = false;
		}
	}
	visitStatement(statement->body);
	if (const FuVar *initVar = dynamic_cast<const FuVar *>(statement->init.get()))
		checkInitialized(initVar);
	closeScope();
}

void FuSema::visitForeach(FuForeach * statement)
{
	openScope(statement);
	FuVar * element = statement->getVar();
	resolveType(element);
	if (visitExpr(statement->collection) != this->poison) {
		if (const FuClassType *klass = dynamic_cast<const FuClassType *>(statement->collection->type.get())) {
			switch (klass->class_->id) {
			case FuId::stringClass:
				if (statement->count() != 1 || !element->type->isAssignableFrom(this->host->program->system->intType.get()))
					reportError(element->typeExpr.get(), "Expected 'int' iterator variable");
				break;
			case FuId::arrayStorageClass:
			case FuId::listClass:
			case FuId::hashSetClass:
			case FuId::sortedSetClass:
				if (statement->count() != 1)
					reportError(statement->getValueVar(), "Expected one iterator variable");
				else if (!element->type->isAssignableFrom(klass->getElementType().get()))
					reportError(element->typeExpr.get(), std::format("Cannot convert '{}' to '{}'", klass->getElementType()->toString(), element->type->toString()));
				break;
			case FuId::dictionaryClass:
			case FuId::sortedDictionaryClass:
			case FuId::orderedDictionaryClass:
				if (statement->count() != 2)
					reportError(element, "Expected '(TKey key, TValue value)' iterator");
				else {
					FuVar * value = statement->getValueVar();
					resolveType(value);
					if (!element->type->isAssignableFrom(klass->getKeyType()))
						reportError(element, std::format("Cannot convert '{}' to '{}'", klass->getKeyType()->toString(), element->type->toString()));
					if (!value->type->isAssignableFrom(klass->getValueType().get()))
						reportError(value, std::format("Cannot convert '{}' to '{}'", klass->getValueType()->toString(), value->type->toString()));
				}
				break;
			default:
				reportError(statement->collection.get(), std::format("'foreach' invalid on '{}'", klass->class_->name));
				break;
			}
		}
		else
			reportError(statement->collection.get(), std::format("'foreach' invalid on '{}'", statement->collection->type->toString()));
	}
	statement->setCompletesNormally(true);
	visitStatement(statement->body);
	closeScope();
}

void FuSema::visitIf(FuIf * statement)
{
	statement->cond = resolveBool(statement->cond);
	visitStatement(statement->onTrue);
	if (statement->onFalse != nullptr) {
		visitStatement(statement->onFalse);
		statement->setCompletesNormally(statement->onTrue->completesNormally() || statement->onFalse->completesNormally());
	}
	else
		statement->setCompletesNormally(true);
}

void FuSema::visitLock(FuLock * statement)
{
	statement->lock = visitExpr(statement->lock);
	coerce(statement->lock.get(), this->host->program->system->lockPtrType.get());
	visitStatement(statement->body);
}

void FuSema::visitReturn(FuReturn * statement)
{
	if (this->currentMethod->type->id == FuId::voidType) {
		if (statement->value != nullptr)
			reportError(statement->value.get(), "Void method cannot return a value");
	}
	else if (statement->value == nullptr)
		reportError(statement, "Missing return value");
	else {
		openScope(statement);
		statement->value = visitExpr(statement->value);
		coercePermanent(statement->value.get(), this->currentMethod->type.get());
		if (dynamic_cast<const FuDynamicPtrType *>(this->currentMethod->type.get()))
			statement->value->setShared();
		const FuSymbolReference * symbol;
		const FuVar * local;
		if ((symbol = dynamic_cast<const FuSymbolReference *>(statement->value.get())) && (local = dynamic_cast<const FuVar *>(symbol->symbol)) && ((local->type->isFinal() && !dynamic_cast<const FuStorageType *>(this->currentMethod->type.get())) || (local->type->id == FuId::stringStorageType && this->currentMethod->type->id != FuId::stringStorageType)))
			reportError(symbol, "Returning dangling reference to local storage");
		closeScope();
	}
}

void FuSema::resolveCaseType(FuSwitch * statement, const FuClassType * switchPtr, std::shared_ptr<FuExpr> value)
{
	const FuSymbolReference * symbol;
	const FuClass * klass;
	if (dynamic_cast<const FuLiteralNull *>(visitExpr(value).get())) {
	}
	else if ((symbol = dynamic_cast<const FuSymbolReference *>(visitExpr(value).get())) && (klass = dynamic_cast<const FuClass *>(symbol->symbol)))
		checkIsHierarchy(switchPtr, statement->value.get(), klass, value.get(), "case", "always match", "never match");
	else if (const FuVar *def = dynamic_cast<const FuVar *>(visitExpr(value).get()))
		checkIsVar(statement->value.get(), def, def, "case", "always match", "never match");
	else
		reportError(value.get(), "Expected 'case Class'");
}

void FuSema::visitSwitch(FuSwitch * statement)
{
	openScope(statement);
	statement->value = visitExpr(statement->value);
	if (statement->value != this->poison && statement->value->type != this->poison) {
		const FuIntegerType * i;
		const FuClassType * klass;
		if ((i = dynamic_cast<const FuIntegerType *>(statement->value->type.get())) && i->id != FuId::longType) {
		}
		else if (dynamic_cast<const FuEnum *>(statement->value->type.get())) {
		}
		else if ((klass = dynamic_cast<const FuClassType *>(statement->value->type.get())) && !dynamic_cast<const FuStorageType *>(klass)) {
		}
		else
			reportError(statement->value.get(), std::format("'switch' on type '{}' - expected 'int', 'enum', 'string' or object reference", statement->value->type->toString()));
	}
	statement->setCompletesNormally(false);
	for (FuCase &kase : statement->cases) {
		if (statement->value != this->poison) {
			for (int i = 0; i < std::ssize(kase.values); i++) {
				std::shared_ptr<FuExpr> value = kase.values[i];
				const FuClassType * switchPtr;
				if ((switchPtr = dynamic_cast<const FuClassType *>(statement->value->type.get())) && switchPtr->class_->id != FuId::stringClass) {
					FuBinaryExpr * when1;
					if ((when1 = dynamic_cast<FuBinaryExpr *>(value.get())) && when1->op == FuToken::when) {
						resolveCaseType(statement, switchPtr, when1->left);
						when1->right = resolveBool(when1->right);
					}
					else
						resolveCaseType(statement, switchPtr, value);
				}
				else {
					FuBinaryExpr * when1;
					if ((when1 = dynamic_cast<FuBinaryExpr *>(value.get())) && when1->op == FuToken::when) {
						when1->left = foldConst(when1->left);
						coerce(when1->left.get(), statement->value->type.get());
						when1->right = resolveBool(when1->right);
					}
					else {
						kase.values[i] = foldConst(value);
						coerce(kase.values[i].get(), statement->value->type.get());
					}
				}
			}
		}
		if (resolveStatements(&kase.body))
			reportError(kase.body.back().get(), "'case' must end with 'break', 'continue', 'return' or 'throw'");
	}
	if (std::ssize(statement->defaultBody) > 0) {
		bool reachable = resolveStatements(&statement->defaultBody);
		if (reachable)
			reportError(statement->defaultBody.back().get(), "'default' must end with 'break', 'continue', 'return' or 'throw'");
	}
	closeScope();
}

void FuSema::resolveException(std::shared_ptr<FuSymbolReference> symbol)
{
	if (dynamic_cast<const FuSymbolReference *>(visitSymbolReference(symbol).get()) && dynamic_cast<const FuClass *>(symbol->symbol) && symbol->symbol->id == FuId::exceptionClass) {
	}
	else
		reportError(symbol.get(), "Expected an exception class");
}

void FuSema::visitThrow(FuThrow * statement)
{
	resolveException(statement->class_);
	const FuClass * klass;
	if ((klass = dynamic_cast<const FuClass *>(statement->class_->symbol)) && !methodHasThrows(this->currentMethod, klass))
		reportError(statement, std::format("Method must be marked 'throws {}'", klass->name));
	if (statement->message != nullptr) {
		statement->message = visitExpr(statement->message);
		if (!dynamic_cast<const FuStringType *>(statement->message->type.get()))
			reportError(statement->message.get(), "Exception accepts a string argument");
	}
}

void FuSema::visitWhile(FuWhile * statement)
{
	openScope(statement);
	resolveLoopCond(statement);
	visitStatement(statement->body);
	closeScope();
}

void FuSema::visitStatement(std::shared_ptr<FuStatement> statement)
{
	if (FuAssert *asrt = dynamic_cast<FuAssert *>(statement.get()))
		visitAssert(asrt);
	else if (FuBlock *block = dynamic_cast<FuBlock *>(statement.get()))
		visitBlock(block);
	else if (const FuBreak *brk = dynamic_cast<const FuBreak *>(statement.get()))
		brk->loopOrSwitch->setCompletesNormally(true);
	else if (dynamic_cast<const FuContinue *>(statement.get()) || dynamic_cast<const FuNative *>(statement.get())) {
	}
	else if (FuDoWhile *doWhile = dynamic_cast<FuDoWhile *>(statement.get()))
		visitDoWhile(doWhile);
	else if (FuFor *forLoop = dynamic_cast<FuFor *>(statement.get()))
		visitFor(forLoop);
	else if (FuForeach *foreachLoop = dynamic_cast<FuForeach *>(statement.get()))
		visitForeach(foreachLoop);
	else if (FuIf *ifStatement = dynamic_cast<FuIf *>(statement.get()))
		visitIf(ifStatement);
	else if (FuLock *lockStatement = dynamic_cast<FuLock *>(statement.get()))
		visitLock(lockStatement);
	else if (FuReturn *ret = dynamic_cast<FuReturn *>(statement.get()))
		visitReturn(ret);
	else if (FuSwitch *switchStatement = dynamic_cast<FuSwitch *>(statement.get()))
		visitSwitch(switchStatement);
	else if (FuThrow *throwStatement = dynamic_cast<FuThrow *>(statement.get()))
		visitThrow(throwStatement);
	else if (FuWhile *whileStatement = dynamic_cast<FuWhile *>(statement.get()))
		visitWhile(whileStatement);
	else if (std::shared_ptr<FuExpr>expr = std::dynamic_pointer_cast<FuExpr>(statement))
		visitExpr(expr);
	else
		std::abort();
}

std::shared_ptr<FuExpr> FuSema::foldConst(std::shared_ptr<FuExpr> expr)
{
	expr = visitExpr(expr);
	if (dynamic_cast<const FuLiteral *>(expr.get()) || expr->isConstEnum())
		return expr;
	reportError(expr.get(), "Expected constant value");
	return expr;
}

int FuSema::foldConstInt(std::shared_ptr<FuExpr> expr)
{
	if (std::shared_ptr<FuLiteralLong>literal = std::dynamic_pointer_cast<FuLiteralLong>(foldConst(expr))) {
		int64_t l = literal->value;
		if (l < -2147483648 || l > 2147483647) {
			reportError(expr.get(), "Only 32-bit ranges supported");
			return 0;
		}
		return static_cast<int>(l);
	}
	reportError(expr.get(), "Expected integer");
	return 0;
}

void FuSema::resolveConst(FuConst * konst)
{
	switch (konst->visitStatus) {
	case FuVisitStatus::notYet:
		break;
	case FuVisitStatus::inProgress:
		konst->value = poisonError(konst, std::format("Circular dependency in value of constant '{}'", konst->name));
		konst->visitStatus = FuVisitStatus::done;
		return;
	case FuVisitStatus::done:
		return;
	}
	konst->visitStatus = FuVisitStatus::inProgress;
	if (!dynamic_cast<const FuEnum *>(this->currentScope))
		resolveType(konst);
	konst->value = visitExpr(konst->value);
	if (FuAggregateInitializer *coll = dynamic_cast<FuAggregateInitializer *>(konst->value.get())) {
		if (const FuClassType *array = dynamic_cast<const FuClassType *>(konst->type.get())) {
			std::shared_ptr<FuType> elementType = array->getElementType();
			if (const FuArrayStorageType *arrayStg = dynamic_cast<const FuArrayStorageType *>(array)) {
				if (arrayStg->length != std::ssize(coll->items))
					reportError(konst, std::format("Declared {} elements, initialized {}", arrayStg->length, std::ssize(coll->items)));
			}
			else if (dynamic_cast<const FuReadWriteClassType *>(array))
				reportError(konst, "Invalid constant type");
			else {
				std::shared_ptr<FuArrayStorageType> futemp0 = std::make_shared<FuArrayStorageType>();
				futemp0->class_ = this->host->program->system->arrayStorageClass.get();
				futemp0->typeArg0 = elementType;
				futemp0->length = std::ssize(coll->items);
				konst->type = futemp0;
			}
			coll->type = konst->type;
			for (const std::shared_ptr<FuExpr> &item : coll->items)
				coerce(item.get(), elementType.get());
		}
		else
			reportError(konst, std::format("Array initializer for scalar constant '{}'", konst->name));
	}
	else if (dynamic_cast<const FuEnum *>(this->currentScope) && dynamic_cast<const FuRangeType *>(konst->value->type.get()) && dynamic_cast<const FuLiteral *>(konst->value.get())) {
	}
	else if (dynamic_cast<const FuLiteral *>(konst->value.get()) || konst->value->isConstEnum())
		coerce(konst->value.get(), konst->type.get());
	else if (konst->value != this->poison)
		reportError(konst->value.get(), std::format("Value for constant '{}' is not constant", konst->name));
	konst->inMethod = this->currentMethod;
	konst->visitStatus = FuVisitStatus::done;
}

void FuSema::resolveConsts(FuContainerType * container)
{
	this->currentScope = container;
	if (const FuClass *klass = dynamic_cast<const FuClass *>(container))
		for (FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
			if (FuConst *konst = dynamic_cast<FuConst *>(symbol))
				resolveConst(konst);
		}
	else if (FuEnum *enu = dynamic_cast<FuEnum *>(container)) {
		const FuConst * previous = nullptr;
		for (FuSymbol * symbol = enu->first; symbol != nullptr; symbol = symbol->next) {
			if (FuConst *konst = dynamic_cast<FuConst *>(symbol)) {
				if (konst->value != nullptr) {
					resolveConst(konst);
					enu->hasExplicitValue = true;
				}
				else {
					std::shared_ptr<FuImplicitEnumValue> futemp0 = std::make_shared<FuImplicitEnumValue>();
					futemp0->value = previous == nullptr ? 0 : previous->value->intValue() + 1;
					konst->value = futemp0;
				}
				previous = konst;
			}
		}
	}
	else
		std::abort();
}

void FuSema::resolveTypes(FuClass * klass)
{
	this->currentScope = klass;
	for (FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (FuField *field = dynamic_cast<FuField *>(symbol)) {
			resolveType(field);
			if (field->visibility != FuVisibility::protected_)
				initUnique(field);
		}
		else if (FuMethod *method = dynamic_cast<FuMethod *>(symbol)) {
			if (method->typeExpr == this->host->program->system->voidType)
				method->type = this->host->program->system->voidType;
			else
				resolveType(method);
			if (method->name == "ToString" && method->callType != FuCallType::static_ && method->parameters.count() == 1)
				method->id = FuId::classToString;
			for (FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
				resolveType(param);
				if (param->value != nullptr) {
					param->value = foldConst(param->value);
					coerce(param->value.get(), param->type.get());
				}
			}
			if (method->name == "Main") {
				if (method->visibility != FuVisibility::public_ || method->callType != FuCallType::static_)
					reportError(method, "'Main' method must be 'public static'");
				if (method->type->id != FuId::voidType && method->type->id != FuId::intType)
					reportError(method->typeExpr.get(), "'Main' method must return 'void' or 'int'");
				switch (method->getParametersCount()) {
				case 0:
					break;
				case 1:
					{
						const FuVar * args = method->firstParameter();
						FuClassType * argsType;
						if ((argsType = dynamic_cast<FuClassType *>(args->type.get())) && argsType->isArray() && !dynamic_cast<const FuReadWriteClassType *>(argsType) && !argsType->nullable) {
							const FuType * argsElement = argsType->getElementType().get();
							if (argsElement->id == FuId::stringPtrType && !argsElement->nullable && args->value == nullptr) {
								argsType->id = FuId::mainArgsType;
								argsType->class_ = this->host->program->system->arrayStorageClass.get();
								break;
							}
						}
						reportError(args, "'Main' method parameter must be 'string[]'");
						break;
					}
				default:
					reportError(method, "'Main' method must have no parameters or one 'string[]' parameter");
					break;
				}
				if (this->host->program->main != nullptr)
					reportError(method, "Duplicate 'Main' method");
				else {
					method->id = FuId::main;
					this->host->program->main = method;
				}
			}
			for (const std::shared_ptr<FuThrowsDeclaration> &exception : method->throws)
				resolveException(exception);
		}
	}
}

void FuSema::resolveCode(FuClass * klass)
{
	if (klass->constructor != nullptr) {
		this->currentScope = &klass->constructor->parameters;
		this->currentMethod = klass->constructor.get();
		visitStatement(klass->constructor->body);
		this->currentMethod = nullptr;
	}
	for (FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (FuField *field = dynamic_cast<FuField *>(symbol)) {
			if (field->value != nullptr) {
				field->value = visitExpr(field->value);
				if (!field->isAssignableStorage()) {
					const FuArrayStorageType * array;
					coercePermanent(field->value.get(), (array = dynamic_cast<const FuArrayStorageType *>(field->type.get())) ? array->getElementType().get() : field->type.get());
				}
			}
		}
		else if (FuMethod *method = dynamic_cast<FuMethod *>(symbol)) {
			if (method->body != nullptr) {
				if (method->callType == FuCallType::override_ || method->callType == FuCallType::sealed) {
					if (FuMethod *baseMethod = dynamic_cast<FuMethod *>(klass->parent->tryLookup(method->name, false).get())) {
						if (!baseMethod->isAbstractVirtualOrOverride())
							reportError(method, "Base method is not abstract or virtual");
						else if (method->isMutator() != baseMethod->isMutator()) {
							if (method->isMutator())
								reportError(method, "Mutating method cannot override a non-mutating method");
							else
								reportError(method, "Non-mutating method cannot override a mutating method");
						}
						if (!method->type->equalsType(baseMethod->type.get()))
							reportError(method->typeExpr.get(), "Base method has a different return type");
						const FuVar * baseParam = baseMethod->firstParameter();
						for (const FuVar * param = method->firstParameter();; param = param->nextVar()) {
							if (param == nullptr) {
								if (baseParam != nullptr)
									reportError(method, "Fewer parameters than the overridden method");
								break;
							}
							if (baseParam == nullptr) {
								reportError(param, "More parameters than the overridden method");
								break;
							}
							if (!param->type->equalsType(baseParam->type.get())) {
								reportError(param->typeExpr.get(), "Base method has a different parameter type");
								break;
							}
							baseParam = baseParam->nextVar();
						}
						baseMethod->calls.insert(method);
					}
					else
						reportError(method, "No method to override");
				}
				this->currentScope = &method->parameters;
				this->currentMethod = method;
				visitStatement(method->body);
				if (method->type->id != FuId::voidType && method->body->completesNormally())
					reportError(method, "Method can complete without a return value");
				this->currentMethod = nullptr;
			}
		}
	}
}

void FuSema::markMethodLive(FuMethodBase * method)
{
	if (method->isLive)
		return;
	method->isLive = true;
	for (FuMethod * called : method->calls)
		markMethodLive(called);
}

void FuSema::markClassLive(const FuClass * klass)
{
	if (klass->isPublic) {
		for (FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
			FuMethod * method;
			if ((method = dynamic_cast<FuMethod *>(symbol)) && (method->visibility == FuVisibility::public_ || method->visibility == FuVisibility::protected_))
				markMethodLive(method);
		}
	}
	if (klass->constructor != nullptr)
		markMethodLive(klass->constructor.get());
}

void FuSema::process()
{
	const FuProgram * program = this->host->program;
	for (FuSymbol * type = program->first; type != nullptr; type = type->next) {
		if (FuClass *klass = dynamic_cast<FuClass *>(type))
			resolveBase(klass);
	}
	for (FuClass * klass : program->classes)
		checkBaseCycle(klass);
	for (FuSymbol * type = program->first; type != nullptr; type = type->next) {
		FuContainerType * container = static_cast<FuContainerType *>(type);
		resolveConsts(container);
	}
	for (FuClass * klass : program->classes)
		resolveTypes(klass);
	for (FuClass * klass : program->classes)
		resolveCode(klass);
	for (const FuClass * klass : program->classes)
		markClassLive(klass);
}

void GenBase::setHost(GenHost * host)
{
	this->host = host;
}

void GenBase::reportError(const FuStatement * statement, std::string_view message) const
{
	this->host->reportStatementError(statement, message);
}

void GenBase::notSupported(const FuStatement * statement, std::string_view feature) const
{
	reportError(statement, std::format("{} not supported when targeting {}", feature, getTargetName()));
}

void GenBase::notYet(const FuStatement * statement, std::string_view feature) const
{
	reportError(statement, std::format("{} not supported yet when targeting {}", feature, getTargetName()));
}

void GenBase::startLine()
{
	if (this->atLineStart) {
		if (this->atChildStart) {
			this->atChildStart = false;
			*this->writer << '\n';
			this->indent++;
		}
		for (int i = 0; i < this->indent; i++)
			*this->writer << '\t';
		this->atLineStart = false;
	}
}

void GenBase::writeChar(int c)
{
	startLine();
	if (c < 0x80)
		*this->writer << static_cast<char>(c);
	else if (c < 0x800)
		*this->writer << static_cast<char>(0xc0 | c >> 6) << static_cast<char>(0x80 | (c & 0x3f));
	else if (c < 0x10000)
		*this->writer << static_cast<char>(0xe0 | c >> 12) << static_cast<char>(0x80 | (c >> 6 & 0x3f)) << static_cast<char>(0x80 | (c & 0x3f));
	else
		*this->writer << static_cast<char>(0xf0 | c >> 18) << static_cast<char>(0x80 | (c >> 12 & 0x3f)) << static_cast<char>(0x80 | (c >> 6 & 0x3f)) << static_cast<char>(0x80 | (c & 0x3f));
}

void GenBase::write(std::string_view s)
{
	startLine();
	*this->writer << s;
}

void GenBase::visitLiteralNull()
{
	write("null");
}

void GenBase::visitLiteralFalse()
{
	write("false");
}

void GenBase::visitLiteralTrue()
{
	write("true");
}

void GenBase::visitLiteralLong(int64_t i)
{
	*this->writer << i;
}

int GenBase::getLiteralChars() const
{
	return 0;
}

void GenBase::visitLiteralChar(int c)
{
	if (c < getLiteralChars()) {
		writeChar('\'');
		switch (c) {
		case '\n':
			write("\\n");
			break;
		case '\r':
			write("\\r");
			break;
		case '\t':
			write("\\t");
			break;
		case '\'':
			write("\\'");
			break;
		case '\\':
			write("\\\\");
			break;
		default:
			writeChar(c);
			break;
		}
		writeChar('\'');
	}
	else
		*this->writer << c;
}

void GenBase::visitLiteralDouble(double value)
{
	std::string s{std::format("{}", value)};
	write(s);
	for (int c : s) {
		switch (c) {
		case '-':
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
			break;
		default:
			return;
		}
	}
	write(".0");
}

void GenBase::visitLiteralString(std::string_view value)
{
	writeChar('"');
	write(value);
	writeChar('"');
}

void GenBase::writeLowercaseChar(int c)
{
	if (c >= 'A' && c <= 'Z')
		c += 32;
	*this->writer << static_cast<char>(c);
}

void GenBase::writeUppercaseChar(int c)
{
	if (c >= 'a' && c <= 'z')
		c -= 32;
	*this->writer << static_cast<char>(c);
}

void GenBase::writeLowercase(std::string_view s)
{
	startLine();
	for (int c : s)
		writeLowercaseChar(c);
}

void GenBase::writeCamelCase(std::string_view s)
{
	startLine();
	writeLowercaseChar(s[0]);
	*this->writer << s.substr(1);
}

void GenBase::writePascalCase(std::string_view s)
{
	startLine();
	writeUppercaseChar(s[0]);
	*this->writer << s.substr(1);
}

void GenBase::writeUppercaseWithUnderscores(std::string_view s)
{
	startLine();
	bool first = true;
	for (int c : s) {
		if (!first && c >= 'A' && c <= 'Z') {
			*this->writer << '_';
			*this->writer << static_cast<char>(c);
		}
		else
			writeUppercaseChar(c);
		first = false;
	}
}

void GenBase::writeLowercaseWithUnderscores(std::string_view s)
{
	startLine();
	bool first = true;
	for (int c : s) {
		if (c >= 'A' && c <= 'Z') {
			if (!first)
				*this->writer << '_';
			writeLowercaseChar(c);
		}
		else
			*this->writer << static_cast<char>(c);
		first = false;
	}
}

void GenBase::writeNewLine()
{
	*this->writer << '\n';
	this->atLineStart = true;
}

void GenBase::writeCharLine(int c)
{
	writeChar(c);
	writeNewLine();
}

void GenBase::writeLine(std::string_view s)
{
	write(s);
	writeNewLine();
}

void GenBase::writeUppercaseConstName(const FuConst * konst)
{
	if (konst->inMethod != nullptr) {
		writeUppercaseWithUnderscores(konst->inMethod->name);
		writeChar('_');
	}
	writeUppercaseWithUnderscores(konst->name);
	if (konst->inMethodIndex > 0) {
		writeChar('_');
		visitLiteralLong(konst->inMethodIndex);
	}
}

void GenBase::writeBanner()
{
	writeLine("// Generated automatically with \"fut\". Do not edit.");
}

void GenBase::createFile(std::string_view directory, std::string_view filename)
{
	this->writer = this->host->createFile(directory, filename);
	writeBanner();
}

void GenBase::createOutputFile()
{
	createFile(std::string_view(), this->outputFile);
}

void GenBase::closeFile()
{
	this->host->closeFile();
}

void GenBase::openStringWriter()
{
	this->writer = &this->stringWriter;
}

void GenBase::closeStringWriter()
{
	*this->writer << this->stringWriter.str();
	this->stringWriter.str(std::string());
}

void GenBase::include(std::string_view name)
{
	if (!(this->includes.count(std::string(name)) != 0))
		this->includes[std::string(name)] = this->inHeaderFile;
}

void GenBase::writeIncludes(std::string_view prefix, std::string_view suffix)
{
	for (const auto &[name, inHeaderFile] : this->includes) {
		if (inHeaderFile == this->inHeaderFile) {
			write(prefix);
			write(name);
			writeLine(suffix);
		}
	}
	if (!this->inHeaderFile)
		this->includes.clear();
}

void GenBase::startDocLine()
{
	write(" * ");
}

void GenBase::writeXmlDoc(std::string_view text)
{
	for (int c : text) {
		switch (c) {
		case '&':
			write("&amp;");
			break;
		case '<':
			write("&lt;");
			break;
		case '>':
			write("&gt;");
			break;
		default:
			writeChar(c);
			break;
		}
	}
}

void GenBase::writeDocCode(std::string_view s)
{
	writeXmlDoc(s);
}

void GenBase::writeDocPara(const FuDocPara * para, bool many)
{
	if (many) {
		writeNewLine();
		write(" * <p>");
	}
	for (const std::shared_ptr<FuDocInline> &inline_ : para->children) {
		if (const FuDocText *text = dynamic_cast<const FuDocText *>(inline_.get()))
			writeXmlDoc(text->text);
		else if (const FuDocCode *code = dynamic_cast<const FuDocCode *>(inline_.get())) {
			write("<code>");
			writeDocCode(code->text);
			write("</code>");
		}
		else if (dynamic_cast<const FuDocLine *>(inline_.get())) {
			writeNewLine();
			startDocLine();
		}
		else
			std::abort();
	}
}

void GenBase::writeDocList(const FuDocList * list)
{
	writeNewLine();
	writeLine(" * <ul>");
	for (const FuDocPara &item : list->items) {
		write(" * <li>");
		writeDocPara(&item, false);
		writeLine("</li>");
	}
	write(" * </ul>");
}

void GenBase::writeDocBlock(const FuDocBlock * block, bool many)
{
	if (const FuDocPara *para = dynamic_cast<const FuDocPara *>(block))
		writeDocPara(para, many);
	else if (const FuDocList *list = dynamic_cast<const FuDocList *>(block))
		writeDocList(list);
	else
		std::abort();
}

void GenBase::writeContent(const FuCodeDoc * doc)
{
	startDocLine();
	writeDocPara(&doc->summary, false);
	writeNewLine();
	if (std::ssize(doc->details) > 0) {
		startDocLine();
		if (std::ssize(doc->details) == 1)
			writeDocBlock(doc->details[0].get(), false);
		else {
			for (const std::shared_ptr<FuDocBlock> &block : doc->details)
				writeDocBlock(block.get(), true);
		}
		writeNewLine();
	}
}

void GenBase::writeDoc(const FuCodeDoc * doc)
{
	if (doc != nullptr) {
		writeLine("/**");
		writeContent(doc);
		writeLine(" */");
	}
}

void GenBase::writeSelfDoc(const FuMethod * method)
{
}

void GenBase::writeParameterDoc(const FuVar * param, bool first)
{
	write(" * @param ");
	writeName(param);
	writeChar(' ');
	writeDocPara(&param->documentation->summary, false);
	writeNewLine();
}

void GenBase::writeReturnDoc(const FuMethod * method)
{
}

void GenBase::writeThrowsDoc(const FuThrowsDeclaration * decl)
{
	write(" * @throws ");
	writeExceptionClass(decl->symbol);
	writeChar(' ');
	writeDocPara(&decl->documentation->summary, false);
	writeNewLine();
}

void GenBase::writeParametersAndThrowsDoc(const FuMethod * method)
{
	bool first = true;
	for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
		if (param->documentation != nullptr) {
			writeParameterDoc(param, first);
			first = false;
		}
	}
	writeReturnDoc(method);
	for (const std::shared_ptr<FuThrowsDeclaration> &decl : method->throws) {
		if (decl->documentation != nullptr)
			writeThrowsDoc(decl.get());
	}
}

void GenBase::writeMethodDoc(const FuMethod * method)
{
	if (method->documentation == nullptr)
		return;
	writeLine("/**");
	writeContent(method->documentation.get());
	writeSelfDoc(method);
	writeParametersAndThrowsDoc(method);
	writeLine(" */");
}

void GenBase::writeTopLevelNatives(const FuProgram * program)
{
	for (std::string_view content : program->topLevelNatives)
		write(content);
}

void GenBase::openBlock()
{
	writeCharLine('{');
	this->indent++;
}

void GenBase::closeBlock()
{
	this->indent--;
	writeCharLine('}');
}

void GenBase::endStatement()
{
	writeCharLine(';');
}

void GenBase::writeComma(int i)
{
	if (i > 0) {
		if ((i & 15) == 0) {
			writeCharLine(',');
			writeChar('\t');
		}
		else
			write(", ");
	}
}

void GenBase::writeBytes(const std::vector<uint8_t> * content)
{
	int i = 0;
	for (int b : *content) {
		writeComma(i++);
		visitLiteralLong(b);
	}
}

FuId GenBase::getTypeId(const FuType * type, bool promote) const
{
	return promote && dynamic_cast<const FuRangeType *>(type) ? FuId::intType : type->id;
}

void GenBase::writeLocalName(const FuSymbol * symbol, FuPriority parent)
{
	if (dynamic_cast<const FuField *>(symbol))
		write("this.");
	writeName(symbol);
}

void GenBase::writeDoubling(std::string_view s, int doubled)
{
	for (int c : s) {
		if (c == doubled)
			writeChar(c);
		writeChar(c);
	}
}

void GenBase::writePrintfWidth(const FuInterpolatedPart * part)
{
	if (part->widthExpr != nullptr)
		visitLiteralLong(part->width);
	if (part->precision >= 0) {
		writeChar('.');
		visitLiteralLong(part->precision);
	}
}

int GenBase::getPrintfFormat(const FuType * type, int format)
{
	if (dynamic_cast<const FuIntegerType *>(type))
		return format == 'x' || format == 'X' ? format : 'd';
	else if (dynamic_cast<const FuNumericType *>(type)) {
		switch (format) {
		case 'E':
		case 'e':
		case 'f':
		case 'G':
		case 'g':
			return format;
		case 'F':
			return 'f';
		default:
			return 'g';
		}
	}
	else if (dynamic_cast<const FuClassType *>(type))
		return 's';
	else
		std::abort();
}

void GenBase::writePrintfFormat(const FuInterpolatedString * expr)
{
	for (const FuInterpolatedPart &part : expr->parts) {
		writeDoubling(part.prefix, '%');
		writeChar('%');
		writePrintfWidth(&part);
		writeChar(getPrintfFormat(part.argument->type.get(), part.format));
	}
	writeDoubling(expr->suffix, '%');
}

void GenBase::writePyFormat(const FuInterpolatedPart * part)
{
	if (part->widthExpr != nullptr || part->precision >= 0 || (part->format != ' ' && part->format != 'D'))
		writeChar(':');
	if (part->widthExpr != nullptr) {
		if (part->width >= 0) {
			if (!dynamic_cast<const FuNumericType *>(part->argument->type.get()))
				writeChar('>');
			visitLiteralLong(part->width);
		}
		else {
			writeChar('<');
			visitLiteralLong(-part->width);
		}
	}
	if (part->precision >= 0) {
		writeChar(dynamic_cast<const FuIntegerType *>(part->argument->type.get()) ? '0' : '.');
		visitLiteralLong(part->precision);
	}
	if (part->format != ' ' && part->format != 'D')
		writeChar(part->format);
	writeChar('}');
}

void GenBase::writeInterpolatedStringArg(const FuExpr * expr)
{
	expr->accept(this, FuPriority::argument);
}

void GenBase::writeInterpolatedStringArgs(const FuInterpolatedString * expr)
{
	for (const FuInterpolatedPart &part : expr->parts) {
		write(", ");
		writeInterpolatedStringArg(part.argument.get());
	}
}

void GenBase::writePrintf(const FuInterpolatedString * expr, bool newLine)
{
	writeChar('"');
	writePrintfFormat(expr);
	if (newLine)
		write("\\n");
	writeChar('"');
	writeInterpolatedStringArgs(expr);
	writeChar(')');
}

void GenBase::writePostfix(const FuExpr * obj, std::string_view s)
{
	obj->accept(this, FuPriority::primary);
	write(s);
}

void GenBase::writeCall(std::string_view function, const FuExpr * arg0, const FuExpr * arg1, const FuExpr * arg2)
{
	write(function);
	writeChar('(');
	arg0->accept(this, FuPriority::argument);
	if (arg1 != nullptr) {
		write(", ");
		arg1->accept(this, FuPriority::argument);
		if (arg2 != nullptr) {
			write(", ");
			arg2->accept(this, FuPriority::argument);
		}
	}
	writeChar(')');
}

void GenBase::writeMemberOp(const FuExpr * left, const FuSymbolReference * symbol)
{
	writeChar('.');
}

void GenBase::writeMethodCall(const FuExpr * obj, std::string_view method, const FuExpr * arg0, const FuExpr * arg1)
{
	obj->accept(this, FuPriority::primary);
	writeMemberOp(obj, nullptr);
	writeCall(method, arg0, arg1);
}

void GenBase::writeInParentheses(const std::vector<std::shared_ptr<FuExpr>> * args)
{
	writeChar('(');
	bool first = true;
	for (const std::shared_ptr<FuExpr> &arg : *args) {
		if (!first)
			write(", ");
		arg->accept(this, FuPriority::argument);
		first = false;
	}
	writeChar(')');
}

void GenBase::writeSelectValues(const FuType * type, const FuSelectExpr * expr)
{
	writeCoerced(type, expr->onTrue.get(), FuPriority::select);
	write(" : ");
	writeCoerced(type, expr->onFalse.get(), FuPriority::select);
}

void GenBase::writeCoercedSelect(const FuType * type, const FuSelectExpr * expr, FuPriority parent)
{
	if (parent > FuPriority::select)
		writeChar('(');
	expr->cond->accept(this, FuPriority::selectCond);
	write(" ? ");
	writeSelectValues(type, expr);
	if (parent > FuPriority::select)
		writeChar(')');
}

void GenBase::writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	expr->accept(this, parent);
}

void GenBase::writeCoerced(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	if (const FuSelectExpr *select = dynamic_cast<const FuSelectExpr *>(expr))
		writeCoercedSelect(type, select, parent);
	else
		writeCoercedInternal(type, expr, parent);
}

void GenBase::writeCoercedExpr(const FuType * type, const FuExpr * expr)
{
	writeCoerced(type, expr, FuPriority::argument);
}

void GenBase::writeStronglyCoerced(const FuType * type, const FuExpr * expr)
{
	writeCoerced(type, expr, FuPriority::argument);
}

void GenBase::writeCoercedLiteral(const FuType * type, const FuExpr * expr)
{
	expr->accept(this, FuPriority::argument);
}

void GenBase::writeCoercedLiterals(const FuType * type, const std::vector<std::shared_ptr<FuExpr>> * exprs)
{
	for (int i = 0; i < std::ssize(*exprs); i++) {
		writeComma(i);
		writeCoercedLiteral(type, (*exprs)[i].get());
	}
}

void GenBase::writeCoercedArgs(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	const FuVar * param = method->firstParameter();
	bool first = true;
	for (const std::shared_ptr<FuExpr> &arg : *args) {
		if (!first)
			write(", ");
		first = false;
		writeStronglyCoerced(param->type.get(), arg.get());
		param = param->nextVar();
	}
}

void GenBase::writeCoercedArgsInParentheses(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	writeChar('(');
	writeCoercedArgs(method, args);
	writeChar(')');
}

void GenBase::writeNewArrayStorage(const FuArrayStorageType * array)
{
	writeNewArray(array->getElementType().get(), array->lengthExpr.get(), FuPriority::argument);
}

void GenBase::writeNewStorage(const FuType * type)
{
	if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(type))
		writeNewArrayStorage(array);
	else if (const FuStorageType *storage = dynamic_cast<const FuStorageType *>(type))
		writeNew(storage, FuPriority::argument);
	else
		std::abort();
}

void GenBase::writeArrayStorageInit(const FuArrayStorageType * array, const FuExpr * value)
{
	write(" = ");
	writeNewArrayStorage(array);
}

void GenBase::writeNewWithFields(const FuReadWriteClassType * type, const FuAggregateInitializer * init)
{
	writeNew(type, FuPriority::argument);
}

void GenBase::writeStorageInit(const FuNamedValue * def)
{
	write(" = ");
	if (const FuAggregateInitializer *init = dynamic_cast<const FuAggregateInitializer *>(def->value.get())) {
		const FuReadWriteClassType * klass = static_cast<const FuReadWriteClassType *>(def->type.get());
		writeNewWithFields(klass, init);
	}
	else
		writeNewStorage(def->type.get());
}

void GenBase::writeVarInit(const FuNamedValue * def)
{
	if (def->isAssignableStorage()) {
	}
	else if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(def->type.get()))
		writeArrayStorageInit(array, def->value.get());
	else if (def->value != nullptr && !dynamic_cast<const FuAggregateInitializer *>(def->value.get())) {
		write(" = ");
		writeCoercedExpr(def->type.get(), def->value.get());
	}
	else if (def->type->isFinal() && !dynamic_cast<const FuParameters *>(def->parent))
		writeStorageInit(def);
}

void GenBase::writeVar(const FuNamedValue * def)
{
	writeTypeAndName(def);
	writeVarInit(def);
}

void GenBase::visitVar(const FuVar * expr)
{
	writeVar(expr);
}

void GenBase::writeObjectLiteral(const FuAggregateInitializer * init, std::string_view separator)
{
	std::string_view prefix = " { ";
	for (const std::shared_ptr<FuExpr> &item : init->items) {
		write(prefix);
		const FuBinaryExpr * assign = static_cast<const FuBinaryExpr *>(item.get());
		const FuSymbolReference * field = static_cast<const FuSymbolReference *>(assign->left.get());
		writeName(field->symbol);
		write(separator);
		writeCoerced(assign->left->type.get(), assign->right.get(), FuPriority::argument);
		prefix = ", ";
	}
	write(" }");
}

const FuAggregateInitializer * GenBase::getAggregateInitializer(const FuNamedValue * def)
{
	const FuExpr * expr = def->value.get();
	if (const FuPrefixExpr *unary = dynamic_cast<const FuPrefixExpr *>(expr))
		expr = unary->inner.get();
	const FuAggregateInitializer * init;
	return (init = dynamic_cast<const FuAggregateInitializer *>(expr)) ? init : nullptr;
}

void GenBase::writeAggregateInitField(const FuExpr * obj, const FuExpr * item)
{
	const FuBinaryExpr * assign = static_cast<const FuBinaryExpr *>(item);
	const FuSymbolReference * field = static_cast<const FuSymbolReference *>(assign->left.get());
	writeMemberOp(obj, field);
	writeName(field->symbol);
	write(" = ");
	writeCoerced(field->type.get(), assign->right.get(), FuPriority::argument);
	endStatement();
}

void GenBase::writeInitCode(const FuNamedValue * def)
{
	const FuAggregateInitializer * init = getAggregateInitializer(def);
	if (init != nullptr) {
		for (const std::shared_ptr<FuExpr> &item : init->items) {
			writeLocalName(def, FuPriority::primary);
			writeAggregateInitField(def, item.get());
		}
	}
}

void GenBase::defineIsVar(const FuBinaryExpr * binary)
{
	if (const FuVar *def = dynamic_cast<const FuVar *>(binary->right.get())) {
		ensureChildBlock();
		writeVar(def);
		endStatement();
	}
}

void GenBase::writeArrayElement(const FuNamedValue * def, int nesting)
{
	writeLocalName(def, FuPriority::primary);
	for (int i = 0; i < nesting; i++) {
		write("[_i");
		visitLiteralLong(i);
		writeChar(']');
	}
}

void GenBase::openLoop(std::string_view intString, int nesting, int count)
{
	write("for (");
	write(intString);
	write(" _i");
	visitLiteralLong(nesting);
	write(" = 0; _i");
	visitLiteralLong(nesting);
	write(" < ");
	visitLiteralLong(count);
	write("; _i");
	visitLiteralLong(nesting);
	write("++) ");
	openBlock();
}

void GenBase::writeTemporaryName(int id)
{
	write("futemp");
	visitLiteralLong(id);
}

bool GenBase::tryWriteTemporary(const FuExpr * expr)
{
	int id = [](const std::vector<const FuExpr *> &v, const FuExpr * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->currentTemporaries, expr);
	if (id < 0)
		return false;
	writeTemporaryName(id);
	return true;
}

void GenBase::writeResourceName(std::string_view name)
{
	for (int c : name)
		writeChar(FuLexer::isLetterOrDigit(c) ? c : '_');
}

void GenBase::visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::increment:
		write("++");
		break;
	case FuToken::decrement:
		write("--");
		break;
	case FuToken::minus:
		writeChar('-');
		{
			const FuPrefixExpr * inner;
			if ((inner = dynamic_cast<const FuPrefixExpr *>(expr->inner.get())) && (inner->op == FuToken::minus || inner->op == FuToken::decrement))
				writeChar(' ');
			break;
		}
	case FuToken::tilde:
		writeChar('~');
		break;
	case FuToken::exclamationMark:
		writeChar('!');
		break;
	case FuToken::new_:
		{
			const FuDynamicPtrType * dynamic = static_cast<const FuDynamicPtrType *>(expr->type.get());
			if (dynamic->class_->id == FuId::arrayPtrClass)
				writeNewArray(dynamic->getElementType().get(), expr->inner.get(), parent);
			else if (const FuAggregateInitializer *init = dynamic_cast<const FuAggregateInitializer *>(expr->inner.get())) {
				if (!tryWriteTemporary(expr))
					writeNewWithFields(dynamic, init);
			}
			else
				writeNew(dynamic, parent);
			return;
		}
	case FuToken::resource:
		{
			const FuLiteralString * name = static_cast<const FuLiteralString *>(expr->inner.get());
			const FuArrayStorageType * array = static_cast<const FuArrayStorageType *>(expr->type.get());
			writeResource(name->value, array->length);
			return;
		}
	default:
		std::abort();
	}
	expr->inner->accept(this, FuPriority::primary);
}

void GenBase::visitPostfixExpr(const FuPostfixExpr * expr, FuPriority parent)
{
	expr->inner->accept(this, FuPriority::primary);
	switch (expr->op) {
	case FuToken::increment:
		write("++");
		break;
	case FuToken::decrement:
		write("--");
		break;
	default:
		std::abort();
	}
}

bool GenBase::isWholeArray(const FuExpr * array, const FuExpr * offset, const FuExpr * length) const
{
	const FuArrayStorageType * arrayStorage;
	const FuLiteralLong * literalLength;
	return (arrayStorage = dynamic_cast<const FuArrayStorageType *>(array->type.get())) && offset->isLiteralZero() && (literalLength = dynamic_cast<const FuLiteralLong *>(length)) && arrayStorage->length == literalLength->value;
}

void GenBase::startAdd(const FuExpr * expr)
{
	if (!expr->isLiteralZero()) {
		expr->accept(this, FuPriority::add);
		write(" + ");
	}
}

void GenBase::writeAdd(const FuExpr * left, const FuExpr * right)
{
	if (const FuLiteralLong *leftLiteral = dynamic_cast<const FuLiteralLong *>(left)) {
		int64_t leftValue = leftLiteral->value;
		if (leftValue == 0) {
			right->accept(this, FuPriority::argument);
			return;
		}
		if (const FuLiteralLong *rightLiteral = dynamic_cast<const FuLiteralLong *>(right)) {
			visitLiteralLong(leftValue + rightLiteral->value);
			return;
		}
	}
	else if (right->isLiteralZero()) {
		left->accept(this, FuPriority::argument);
		return;
	}
	left->accept(this, FuPriority::add);
	write(" + ");
	right->accept(this, FuPriority::add);
}

void GenBase::writeStartEnd(const FuExpr * startIndex, const FuExpr * length)
{
	startIndex->accept(this, FuPriority::argument);
	write(", ");
	writeAdd(startIndex, length);
}

bool GenBase::isBitOp(FuPriority parent)
{
	switch (parent) {
	case FuPriority::or_:
	case FuPriority::xor_:
	case FuPriority::and_:
	case FuPriority::shift:
		return true;
	default:
		return false;
	}
}

void GenBase::writeBinaryOperand(const FuExpr * expr, FuPriority parent, const FuBinaryExpr * binary)
{
	expr->accept(this, parent);
}

void GenBase::writeBinaryExpr(const FuBinaryExpr * expr, bool parentheses, FuPriority left, std::string_view op, FuPriority right)
{
	if (parentheses)
		writeChar('(');
	writeBinaryOperand(expr->left.get(), left, expr);
	write(op);
	writeBinaryOperand(expr->right.get(), right, expr);
	if (parentheses)
		writeChar(')');
}

void GenBase::writeBinaryExpr2(const FuBinaryExpr * expr, FuPriority parent, FuPriority child, std::string_view op)
{
	writeBinaryExpr(expr, parent > child, child, op, child);
}

std::string_view GenBase::getEqOp(bool not_)
{
	return not_ ? " != " : " == ";
}

void GenBase::writeEqualOperand(const FuExpr * expr, const FuExpr * other)
{
	expr->accept(this, FuPriority::equality);
}

void GenBase::writeEqualExpr(const FuExpr * left, const FuExpr * right, FuPriority parent, std::string_view op)
{
	if (parent > FuPriority::condAnd)
		writeChar('(');
	writeEqualOperand(left, right);
	write(op);
	writeEqualOperand(right, left);
	if (parent > FuPriority::condAnd)
		writeChar(')');
}

void GenBase::writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	writeEqualExpr(left, right, parent, getEqOp(not_));
}

void GenBase::writeRel(const FuBinaryExpr * expr, FuPriority parent, std::string_view op)
{
	writeBinaryExpr(expr, parent > FuPriority::condAnd, FuPriority::rel, op, FuPriority::rel);
}

void GenBase::writeAnd(const FuBinaryExpr * expr, FuPriority parent)
{
	writeBinaryExpr(expr, parent > FuPriority::condAnd && parent != FuPriority::and_, FuPriority::and_, " & ", FuPriority::and_);
}

void GenBase::writeAssignRight(const FuBinaryExpr * expr)
{
	writeCoerced(expr->left->type.get(), expr->right.get(), FuPriority::argument);
}

void GenBase::writeAssign(const FuBinaryExpr * expr, FuPriority parent)
{
	if (parent > FuPriority::assign)
		writeChar('(');
	expr->left->accept(this, FuPriority::assign);
	write(" = ");
	writeAssignRight(expr);
	if (parent > FuPriority::assign)
		writeChar(')');
}

void GenBase::writeOpAssignRight(const FuBinaryExpr * expr)
{
	expr->right->accept(this, FuPriority::argument);
}

void GenBase::writeIndexing(const FuExpr * collection, const FuExpr * index)
{
	collection->accept(this, FuPriority::primary);
	writeChar('[');
	index->accept(this, FuPriority::argument);
	writeChar(']');
}

void GenBase::writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	writeIndexing(expr->left.get(), expr->right.get());
}

std::string_view GenBase::getIsOperator() const
{
	return " is ";
}

void GenBase::visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::plus:
		writeBinaryExpr(expr, parent > FuPriority::add || isBitOp(parent), FuPriority::add, " + ", FuPriority::add);
		break;
	case FuToken::minus:
		writeBinaryExpr(expr, parent > FuPriority::add || isBitOp(parent), FuPriority::add, " - ", FuPriority::mul);
		break;
	case FuToken::asterisk:
		writeBinaryExpr(expr, parent > FuPriority::mul, FuPriority::mul, " * ", FuPriority::primary);
		break;
	case FuToken::slash:
		writeBinaryExpr(expr, parent > FuPriority::mul, FuPriority::mul, " / ", FuPriority::primary);
		break;
	case FuToken::mod:
		writeBinaryExpr(expr, parent > FuPriority::mul, FuPriority::mul, " % ", FuPriority::primary);
		break;
	case FuToken::shiftLeft:
		writeBinaryExpr(expr, parent > FuPriority::shift, FuPriority::shift, " << ", FuPriority::mul);
		break;
	case FuToken::shiftRight:
		writeBinaryExpr(expr, parent > FuPriority::shift, FuPriority::shift, " >> ", FuPriority::mul);
		break;
	case FuToken::equal:
		writeEqual(expr->left.get(), expr->right.get(), parent, false);
		break;
	case FuToken::notEqual:
		writeEqual(expr->left.get(), expr->right.get(), parent, true);
		break;
	case FuToken::less:
		writeRel(expr, parent, " < ");
		break;
	case FuToken::lessOrEqual:
		writeRel(expr, parent, " <= ");
		break;
	case FuToken::greater:
		writeRel(expr, parent, " > ");
		break;
	case FuToken::greaterOrEqual:
		writeRel(expr, parent, " >= ");
		break;
	case FuToken::and_:
		writeAnd(expr, parent);
		break;
	case FuToken::or_:
		writeBinaryExpr2(expr, parent, FuPriority::or_, " | ");
		break;
	case FuToken::xor_:
		writeBinaryExpr(expr, parent > FuPriority::xor_ || parent == FuPriority::or_, FuPriority::xor_, " ^ ", FuPriority::xor_);
		break;
	case FuToken::condAnd:
		writeBinaryExpr(expr, parent > FuPriority::condAnd || parent == FuPriority::condOr, FuPriority::condAnd, " && ", FuPriority::condAnd);
		break;
	case FuToken::condOr:
		writeBinaryExpr2(expr, parent, FuPriority::condOr, " || ");
		break;
	case FuToken::assign:
		writeAssign(expr, parent);
		break;
	case FuToken::addAssign:
	case FuToken::subAssign:
	case FuToken::mulAssign:
	case FuToken::divAssign:
	case FuToken::modAssign:
	case FuToken::shiftLeftAssign:
	case FuToken::shiftRightAssign:
	case FuToken::andAssign:
	case FuToken::orAssign:
	case FuToken::xorAssign:
		if (parent > FuPriority::assign)
			writeChar('(');
		expr->left->accept(this, FuPriority::assign);
		writeChar(' ');
		write(expr->getOpString());
		writeChar(' ');
		writeOpAssignRight(expr);
		if (parent > FuPriority::assign)
			writeChar(')');
		break;
	case FuToken::leftBracket:
		if (dynamic_cast<const FuStringType *>(expr->left->type.get()))
			writeCharAt(expr);
		else
			writeIndexingExpr(expr, parent);
		break;
	case FuToken::is:
		if (parent > FuPriority::rel)
			writeChar('(');
		expr->left->accept(this, FuPriority::rel);
		write(getIsOperator());
		if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr->right.get()))
			writeName(symbol->symbol);
		else if (const FuVar *def = dynamic_cast<const FuVar *>(expr->right.get()))
			writeTypeAndName(def);
		else
			std::abort();
		if (parent > FuPriority::rel)
			writeChar(')');
		break;
	default:
		std::abort();
	}
}

void GenBase::writeArrayLength(const FuExpr * expr, FuPriority parent)
{
	writePostfix(expr, ".length");
}

bool GenBase::isReferenceTo(const FuExpr * expr, FuId id)
{
	const FuSymbolReference * symbol;
	return (symbol = dynamic_cast<const FuSymbolReference *>(expr)) && symbol->symbol->id == id;
}

bool GenBase::writeJavaMatchProperty(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::matchStart:
		writePostfix(expr->left.get(), ".start()");
		return true;
	case FuId::matchEnd:
		writePostfix(expr->left.get(), ".end()");
		return true;
	case FuId::matchLength:
		if (parent > FuPriority::add)
			writeChar('(');
		writePostfix(expr->left.get(), ".end() - ");
		writePostfix(expr->left.get(), ".start()");
		if (parent > FuPriority::add)
			writeChar(')');
		return true;
	case FuId::matchValue:
		writePostfix(expr->left.get(), ".group()");
		return true;
	default:
		return false;
	}
}

void GenBase::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	if (expr->left == nullptr)
		writeLocalName(expr->symbol, parent);
	else if (expr->symbol->id == FuId::stringLength)
		writeStringLength(expr->left.get());
	else if (expr->symbol->id == FuId::arrayLength)
		writeArrayLength(expr->left.get(), parent);
	else {
		expr->left->accept(this, FuPriority::primary);
		writeMemberOp(expr->left.get(), expr);
		writeName(expr->symbol);
	}
}

void GenBase::writeNotPromoted(const FuType * type, const FuExpr * expr)
{
	expr->accept(this, FuPriority::argument);
}

void GenBase::writeEnumAsInt(const FuExpr * expr, FuPriority parent)
{
	expr->accept(this, parent);
}

void GenBase::writeEnumHasFlag(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	if (parent > FuPriority::equality)
		writeChar('(');
	int i = (*args)[0]->intValue();
	if ((i & (i - 1)) == 0 && i != 0) {
		writeChar('(');
		writeEnumAsInt(obj, FuPriority::and_);
		write(" & ");
		writeEnumAsInt((*args)[0].get(), FuPriority::and_);
		write(") != 0");
	}
	else {
		write("(~");
		writeEnumAsInt(obj, FuPriority::primary);
		write(" & ");
		writeEnumAsInt((*args)[0].get(), FuPriority::and_);
		write(") == 0");
	}
	if (parent > FuPriority::equality)
		writeChar(')');
}

void GenBase::writeTryParseRadix(const std::vector<std::shared_ptr<FuExpr>> * args)
{
	write(", ");
	if (std::ssize(*args) == 2)
		(*args)[1]->accept(this, FuPriority::argument);
	else
		write("10");
}

void GenBase::writeListAdd(const FuExpr * obj, std::string_view method, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	obj->accept(this, FuPriority::primary);
	writeChar('.');
	write(method);
	writeChar('(');
	const FuType * elementType = obj->type->asClassType()->getElementType().get();
	if (std::ssize(*args) == 0)
		writeNewStorage(elementType);
	else
		writeNotPromoted(elementType, (*args)[0].get());
	writeChar(')');
}

void GenBase::writeListInsert(const FuExpr * obj, std::string_view method, const std::vector<std::shared_ptr<FuExpr>> * args, std::string_view separator)
{
	obj->accept(this, FuPriority::primary);
	writeChar('.');
	write(method);
	writeChar('(');
	(*args)[0]->accept(this, FuPriority::argument);
	write(separator);
	const FuType * elementType = obj->type->asClassType()->getElementType().get();
	if (std::ssize(*args) == 1)
		writeNewStorage(elementType);
	else
		writeNotPromoted(elementType, (*args)[1].get());
	writeChar(')');
}

void GenBase::writeDictionaryAdd(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	writeIndexing(obj, (*args)[0].get());
	write(" = ");
	writeNewStorage(obj->type->asClassType()->getValueType().get());
}

void GenBase::writeClampAsMinMax(const std::vector<std::shared_ptr<FuExpr>> * args)
{
	(*args)[0]->accept(this, FuPriority::argument);
	write(", ");
	(*args)[1]->accept(this, FuPriority::argument);
	write("), ");
	(*args)[2]->accept(this, FuPriority::argument);
	writeChar(')');
}

RegexOptions GenBase::getRegexOptions(const std::vector<std::shared_ptr<FuExpr>> * args) const
{
	const FuExpr * expr = args->back().get();
	if (dynamic_cast<const FuEnum *>(expr->type.get()))
		return static_cast<RegexOptions>(expr->intValue());
	return RegexOptions::none;
}

bool GenBase::writeRegexOptions(const std::vector<std::shared_ptr<FuExpr>> * args, std::string_view prefix, std::string_view separator, std::string_view suffix, std::string_view i, std::string_view m, std::string_view s)
{
	RegexOptions options = getRegexOptions(args);
	if (options == RegexOptions::none)
		return false;
	write(prefix);
	if ((static_cast<int>(options) & static_cast<int>(RegexOptions::ignoreCase)) != 0)
		write(i);
	if ((static_cast<int>(options) & static_cast<int>(RegexOptions::multiline)) != 0) {
		if ((static_cast<int>(options) & static_cast<int>(RegexOptions::ignoreCase)) != 0)
			write(separator);
		write(m);
	}
	if ((static_cast<int>(options) & static_cast<int>(RegexOptions::singleline)) != 0) {
		if (options != RegexOptions::singleline)
			write(separator);
		write(s);
	}
	write(suffix);
	return true;
}

void GenBase::visitCallExpr(const FuCallExpr * expr, FuPriority parent)
{
	const FuMethod * method = static_cast<const FuMethod *>(expr->method->symbol);
	writeCallExpr(expr->method->left.get(), method, &expr->arguments, parent);
}

void GenBase::visitSelectExpr(const FuSelectExpr * expr, FuPriority parent)
{
	writeCoercedSelect(expr->type.get(), expr, parent);
}

void GenBase::ensureChildBlock()
{
	if (this->atChildStart) {
		this->atLineStart = false;
		this->atChildStart = false;
		writeChar(' ');
		openBlock();
		this->inChildBlock = true;
	}
}

bool GenBase::hasTemporaries(const FuExpr * expr)
{
	if (const FuAggregateInitializer *init = dynamic_cast<const FuAggregateInitializer *>(expr))
		return std::any_of(init->items.begin(), init->items.end(), [](const std::shared_ptr<FuExpr> &item) { return hasTemporaries(item.get()); });
	else if (dynamic_cast<const FuLiteral *>(expr) || dynamic_cast<const FuLambdaExpr *>(expr))
		return false;
	else if (const FuInterpolatedString *interp = dynamic_cast<const FuInterpolatedString *>(expr))
		return std::any_of(interp->parts.begin(), interp->parts.end(), [](const FuInterpolatedPart &part) { return hasTemporaries(part.argument.get()); });
	else if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr))
		return symbol->left != nullptr && hasTemporaries(symbol->left.get());
	else if (const FuUnaryExpr *unary = dynamic_cast<const FuUnaryExpr *>(expr))
		return unary->inner != nullptr && (hasTemporaries(unary->inner.get()) || dynamic_cast<const FuAggregateInitializer *>(unary->inner.get()));
	else if (const FuBinaryExpr *binary = dynamic_cast<const FuBinaryExpr *>(expr))
		return hasTemporaries(binary->left.get()) || (binary->op == FuToken::is ? !!dynamic_cast<const FuVar *>(binary->right.get()) : hasTemporaries(binary->right.get()));
	else if (const FuSelectExpr *select = dynamic_cast<const FuSelectExpr *>(expr))
		return hasTemporaries(select->cond.get()) || hasTemporaries(select->onTrue.get()) || hasTemporaries(select->onFalse.get());
	else if (const FuCallExpr *call = dynamic_cast<const FuCallExpr *>(expr))
		return hasTemporaries(call->method.get()) || std::any_of(call->arguments.begin(), call->arguments.end(), [](const std::shared_ptr<FuExpr> &arg) { return hasTemporaries(arg.get()); });
	else
		std::abort();
}

void GenBase::defineObjectLiteralTemporary(const FuUnaryExpr * expr)
{
	if (const FuAggregateInitializer *init = dynamic_cast<const FuAggregateInitializer *>(expr->inner.get())) {
		ensureChildBlock();
		int id = [](const std::vector<const FuExpr *> &v, const FuExpr * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->currentTemporaries, expr->type.get());
		if (id < 0) {
			id = std::ssize(this->currentTemporaries);
			startTemporaryVar(expr->type.get());
			this->currentTemporaries.push_back(expr);
		}
		else
			this->currentTemporaries[id] = expr;
		writeTemporaryName(id);
		write(" = ");
		const FuDynamicPtrType * dynamic = static_cast<const FuDynamicPtrType *>(expr->type.get());
		writeNew(dynamic, FuPriority::argument);
		endStatement();
		for (const std::shared_ptr<FuExpr> &item : init->items) {
			writeTemporaryName(id);
			writeAggregateInitField(expr, item.get());
		}
	}
}

void GenBase::writeTemporariesNotSubstring(const FuExpr * expr)
{
	writeTemporaries(expr);
}

void GenBase::writeOwningTemporary(const FuExpr * expr)
{
}

void GenBase::writeArgTemporary(const FuMethod * method, const FuVar * param, const FuExpr * arg)
{
}

void GenBase::writeTemporaries(const FuExpr * expr)
{
	if (const FuVar *def = dynamic_cast<const FuVar *>(expr)) {
		if (def->value != nullptr) {
			const FuUnaryExpr * unary;
			if ((unary = dynamic_cast<const FuUnaryExpr *>(def->value.get())) && dynamic_cast<const FuAggregateInitializer *>(unary->inner.get()))
				writeTemporaries(unary->inner.get());
			else
				writeTemporaries(def->value.get());
		}
	}
	else if (const FuAggregateInitializer *init = dynamic_cast<const FuAggregateInitializer *>(expr))
		for (const std::shared_ptr<FuExpr> &item : init->items) {
			const FuBinaryExpr * assign = static_cast<const FuBinaryExpr *>(item.get());
			writeTemporaries(assign->right.get());
		}
	else if (dynamic_cast<const FuLiteral *>(expr) || dynamic_cast<const FuLambdaExpr *>(expr)) {
	}
	else if (const FuInterpolatedString *interp = dynamic_cast<const FuInterpolatedString *>(expr))
		for (const FuInterpolatedPart &part : interp->parts)
			writeTemporariesNotSubstring(part.argument.get());
	else if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr)) {
		if (symbol->left != nullptr) {
			writeTemporaries(symbol->left.get());
			writeOwningTemporary(symbol->left.get());
		}
	}
	else if (const FuUnaryExpr *unary = dynamic_cast<const FuUnaryExpr *>(expr)) {
		if (unary->inner != nullptr) {
			writeTemporaries(unary->inner.get());
			defineObjectLiteralTemporary(unary);
		}
	}
	else if (const FuBinaryExpr *binary = dynamic_cast<const FuBinaryExpr *>(expr)) {
		writeTemporariesNotSubstring(binary->left.get());
		if (binary->op == FuToken::is)
			defineIsVar(binary);
		else {
			writeTemporaries(binary->right.get());
			if (binary->op != FuToken::assign)
				writeOwningTemporary(binary->right.get());
		}
	}
	else if (const FuSelectExpr *select = dynamic_cast<const FuSelectExpr *>(expr)) {
		writeTemporaries(select->cond.get());
		writeTemporaries(select->onTrue.get());
		writeTemporaries(select->onFalse.get());
	}
	else if (const FuCallExpr *call = dynamic_cast<const FuCallExpr *>(expr)) {
		writeTemporaries(call->method.get());
		const FuMethod * method = static_cast<const FuMethod *>(call->method->symbol);
		const FuVar * param = method->firstParameter();
		for (const std::shared_ptr<FuExpr> &arg : call->arguments) {
			writeTemporaries(arg.get());
			writeArgTemporary(method, param, arg.get());
			param = param->nextVar();
		}
	}
	else
		std::abort();
}

void GenBase::cleanupTemporary(int i, const FuExpr * temp)
{
}

void GenBase::cleanupTemporaries()
{
	for (int i = std::ssize(this->currentTemporaries); --i >= 0;) {
		const FuExpr * temp = this->currentTemporaries[i];
		if (!dynamic_cast<const FuType *>(temp)) {
			cleanupTemporary(i, temp);
			this->currentTemporaries[i] = temp->type.get();
		}
	}
}

void GenBase::visitExpr(const FuExpr * statement)
{
	writeTemporaries(statement);
	statement->accept(this, FuPriority::statement);
	writeCharLine(';');
	if (const FuVar *def = dynamic_cast<const FuVar *>(statement))
		writeInitCode(def);
	cleanupTemporaries();
}

void GenBase::visitConst(const FuConst * statement)
{
}

void GenBase::visitAssert(const FuAssert * statement)
{
	const FuBinaryExpr * binary;
	if ((binary = dynamic_cast<const FuBinaryExpr *>(statement->cond.get())) && binary->op == FuToken::is && dynamic_cast<const FuVar *>(binary->right.get()))
		writeAssertCast(binary);
	else
		writeAssert(statement);
}

void GenBase::writeFirstStatements(const std::vector<std::shared_ptr<FuStatement>> * statements, int count)
{
	for (int i = 0; i < count; i++)
		(*statements)[i]->acceptStatement(this);
}

void GenBase::writeStatements(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	writeFirstStatements(statements, std::ssize(*statements));
}

void GenBase::cleanupBlock(const FuBlock * statement)
{
}

void GenBase::visitBlock(const FuBlock * statement)
{
	if (this->atChildStart) {
		this->atLineStart = false;
		this->atChildStart = false;
		writeChar(' ');
	}
	openBlock();
	int temporariesCount = std::ssize(this->currentTemporaries);
	writeStatements(&statement->statements);
	cleanupBlock(statement);
	this->currentTemporaries.erase(this->currentTemporaries.begin() + temporariesCount, this->currentTemporaries.begin() + temporariesCount + (std::ssize(this->currentTemporaries) - temporariesCount));
	closeBlock();
}

void GenBase::writeChild(FuStatement * statement)
{
	bool wasInChildBlock = this->inChildBlock;
	this->atLineStart = true;
	this->atChildStart = true;
	this->inChildBlock = false;
	statement->acceptStatement(this);
	if (this->inChildBlock)
		closeBlock();
	else if (!dynamic_cast<const FuBlock *>(statement))
		this->indent--;
	this->inChildBlock = wasInChildBlock;
}

void GenBase::startBreakGoto()
{
	write("goto fuafterswitch");
}

void GenBase::visitBreak(const FuBreak * statement)
{
	if (const FuSwitch *switchStatement = dynamic_cast<const FuSwitch *>(statement->loopOrSwitch)) {
		int gotoId = [](const std::vector<const FuSwitch *> &v, const FuSwitch * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->switchesWithGoto, switchStatement);
		if (gotoId >= 0) {
			startBreakGoto();
			visitLiteralLong(gotoId);
			writeCharLine(';');
			return;
		}
	}
	writeLine("break;");
}

void GenBase::visitContinue(const FuContinue * statement)
{
	writeLine("continue;");
}

void GenBase::visitDoWhile(const FuDoWhile * statement)
{
	write("do");
	writeChild(statement->body.get());
	write("while (");
	statement->cond->accept(this, FuPriority::argument);
	writeLine(");");
}

void GenBase::visitFor(const FuFor * statement)
{
	if (statement->cond != nullptr)
		writeTemporaries(statement->cond.get());
	write("for (");
	if (statement->init != nullptr)
		statement->init->accept(this, FuPriority::statement);
	writeChar(';');
	if (statement->cond != nullptr) {
		writeChar(' ');
		statement->cond->accept(this, FuPriority::argument);
	}
	writeChar(';');
	if (statement->advance != nullptr) {
		writeChar(' ');
		statement->advance->accept(this, FuPriority::statement);
	}
	writeChar(')');
	writeChild(statement->body.get());
}

bool GenBase::embedIfWhileIsVar(const FuExpr * expr, bool write)
{
	return false;
}

void GenBase::startIfWhile(const FuExpr * expr)
{
	embedIfWhileIsVar(expr, true);
	expr->accept(this, FuPriority::argument);
	writeChar(')');
}

void GenBase::startIf(const FuExpr * expr)
{
	write("if (");
	startIfWhile(expr);
}

void GenBase::writeIf(const FuIf * statement)
{
	startIf(statement->cond.get());
	writeChild(statement->onTrue.get());
	if (statement->onFalse != nullptr) {
		write("else");
		if (FuIf *elseIf = dynamic_cast<FuIf *>(statement->onFalse.get())) {
			bool wasInChildBlock = this->inChildBlock;
			this->atLineStart = true;
			this->atChildStart = true;
			this->inChildBlock = false;
			if (!embedIfWhileIsVar(elseIf->cond.get(), false))
				writeTemporaries(elseIf->cond.get());
			if (this->inChildBlock) {
				writeIf(elseIf);
				closeBlock();
			}
			else {
				this->atLineStart = false;
				this->atChildStart = false;
				writeChar(' ');
				writeIf(elseIf);
			}
			this->inChildBlock = wasInChildBlock;
		}
		else
			writeChild(statement->onFalse.get());
	}
}

void GenBase::visitIf(const FuIf * statement)
{
	if (!embedIfWhileIsVar(statement->cond.get(), false))
		writeTemporaries(statement->cond.get());
	writeIf(statement);
}

void GenBase::visitNative(const FuNative * statement)
{
	write(statement->content);
}

void GenBase::visitReturn(const FuReturn * statement)
{
	if (statement->value == nullptr)
		writeLine("return;");
	else {
		writeTemporaries(statement->value.get());
		write("return ");
		writeStronglyCoerced(this->currentMethod->type.get(), statement->value.get());
		writeCharLine(';');
		cleanupTemporaries();
	}
}

void GenBase::defineVar(const FuExpr * value)
{
	if (const FuVar *def = dynamic_cast<const FuVar *>(value)) {
		writeVar(def);
		endStatement();
	}
}

void GenBase::writeSwitchCaseTypeVar(const FuExpr * value)
{
}

void GenBase::writeSwitchValue(const FuExpr * expr)
{
	expr->accept(this, FuPriority::argument);
}

void GenBase::writeSwitchCaseValue(const FuSwitch * statement, const FuExpr * value)
{
	const FuBinaryExpr * when1;
	if ((when1 = dynamic_cast<const FuBinaryExpr *>(value)) && when1->op == FuToken::when) {
		writeSwitchCaseValue(statement, when1->left.get());
		write(" when ");
		when1->right->accept(this, FuPriority::argument);
	}
	else
		writeCoercedLiteral(statement->value->type.get(), value);
}

void GenBase::writeSwitchCaseBody(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	writeStatements(statements);
}

void GenBase::writeSwitchCase(const FuSwitch * statement, const FuCase * kase)
{
	for (const std::shared_ptr<FuExpr> &value : kase->values) {
		write("case ");
		writeSwitchCaseValue(statement, value.get());
		writeCharLine(':');
	}
	this->indent++;
	writeSwitchCaseBody(&kase->body);
	this->indent--;
}

void GenBase::startSwitch(const FuSwitch * statement)
{
	write("switch (");
	writeSwitchValue(statement->value.get());
	writeLine(") {");
	for (const FuCase &kase : statement->cases)
		writeSwitchCase(statement, &kase);
}

void GenBase::writeSwitchCaseCond(const FuSwitch * statement, const FuExpr * value, FuPriority parent)
{
	const FuBinaryExpr * when1;
	if ((when1 = dynamic_cast<const FuBinaryExpr *>(value)) && when1->op == FuToken::when) {
		if (parent > FuPriority::selectCond)
			writeChar('(');
		writeSwitchCaseCond(statement, when1->left.get(), FuPriority::condAnd);
		write(" && ");
		when1->right->accept(this, FuPriority::condAnd);
		if (parent > FuPriority::selectCond)
			writeChar(')');
	}
	else
		writeEqual(statement->value.get(), value, parent, false);
}

void GenBase::writeIfCaseBody(const std::vector<std::shared_ptr<FuStatement>> * body, bool doWhile, const FuSwitch * statement, const FuCase * kase)
{
	int length = FuSwitch::lengthWithoutTrailingBreak(body);
	if (doWhile && FuSwitch::hasEarlyBreak(body)) {
		this->indent++;
		writeNewLine();
		write("do ");
		openBlock();
		writeFirstStatements(body, length);
		closeBlock();
		writeLine("while (false);");
		this->indent--;
	}
	else if (length != 1 || dynamic_cast<const FuIf *>((*body)[0].get()) || dynamic_cast<const FuSwitch *>((*body)[0].get())) {
		writeChar(' ');
		openBlock();
		writeFirstStatements(body, length);
		closeBlock();
	}
	else
		writeChild((*body)[0].get());
}

void GenBase::writeSwitchAsIfs(const FuSwitch * statement, bool doWhile)
{
	for (const FuCase &kase : statement->cases) {
		for (const std::shared_ptr<FuExpr> &value : kase.values) {
			const FuBinaryExpr * when1;
			if ((when1 = dynamic_cast<const FuBinaryExpr *>(value.get())) && when1->op == FuToken::when) {
				defineVar(when1->left.get());
				writeTemporaries(when1);
			}
			else
				writeSwitchCaseTypeVar(value.get());
		}
	}
	std::string_view op = "if (";
	for (const FuCase &kase : statement->cases) {
		FuPriority parent = std::ssize(kase.values) == 1 ? FuPriority::argument : FuPriority::condOr;
		for (const std::shared_ptr<FuExpr> &value : kase.values) {
			write(op);
			writeSwitchCaseCond(statement, value.get(), parent);
			op = " || ";
		}
		writeChar(')');
		writeIfCaseBody(&kase.body, doWhile, statement, &kase);
		op = "else if (";
	}
	if (statement->hasDefault()) {
		write("else");
		writeIfCaseBody(&statement->defaultBody, doWhile, statement, nullptr);
	}
}

void GenBase::visitSwitch(const FuSwitch * statement)
{
	writeTemporaries(statement->value.get());
	startSwitch(statement);
	if (std::ssize(statement->defaultBody) > 0) {
		writeLine("default:");
		this->indent++;
		writeSwitchCaseBody(&statement->defaultBody);
		this->indent--;
	}
	writeCharLine('}');
}

void GenBase::writeException()
{
	write("Exception");
}

void GenBase::writeExceptionClass(const FuSymbol * klass)
{
	if (klass->name == "Exception")
		writeException();
	else
		writeName(klass);
}

void GenBase::writeThrowNoMessage()
{
}

void GenBase::writeThrowArgument(const FuThrow * statement)
{
	writeExceptionClass(statement->class_->symbol);
	writeChar('(');
	if (statement->message != nullptr)
		statement->message->accept(this, FuPriority::argument);
	else
		writeThrowNoMessage();
	writeChar(')');
}

void GenBase::visitThrow(const FuThrow * statement)
{
	write("throw new ");
	writeThrowArgument(statement);
	writeCharLine(';');
}

void GenBase::visitWhile(const FuWhile * statement)
{
	if (!embedIfWhileIsVar(statement->cond.get(), false))
		writeTemporaries(statement->cond.get());
	write("while (");
	startIfWhile(statement->cond.get());
	writeChild(statement->body.get());
}

void GenBase::flattenBlock(FuStatement * statement)
{
	if (const FuBlock *block = dynamic_cast<const FuBlock *>(statement))
		writeStatements(&block->statements);
	else
		statement->acceptStatement(this);
}

bool GenBase::hasInitCode(const FuNamedValue * def) const
{
	return getAggregateInitializer(def) != nullptr;
}

bool GenBase::needsConstructor(const FuClass * klass) const
{
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const FuField * field;
		if ((field = dynamic_cast<const FuField *>(symbol)) && hasInitCode(field))
			return true;
	}
	return klass->constructor != nullptr;
}

void GenBase::writeInitField(const FuField * field)
{
	writeInitCode(field);
}

void GenBase::writeConstructorBody(const FuClass * klass)
{
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const FuField *field = dynamic_cast<const FuField *>(symbol))
			writeInitField(field);
	}
	if (klass->constructor != nullptr) {
		this->currentMethod = klass->constructor.get();
		const FuBlock * block = static_cast<const FuBlock *>(klass->constructor->body.get());
		writeStatements(&block->statements);
		this->currentMethod = nullptr;
	}
	this->switchesWithGoto.clear();
	this->currentTemporaries.clear();
}

void GenBase::writeParameter(const FuVar * param)
{
	writeTypeAndName(param);
}

void GenBase::writeRemainingParameters(const FuMethod * method, bool first, bool defaultArguments)
{
	for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
		if (!first)
			write(", ");
		first = false;
		writeParameter(param);
		if (defaultArguments)
			writeVarInit(param);
	}
	writeChar(')');
}

void GenBase::writeParameters(const FuMethod * method, bool defaultArguments)
{
	writeChar('(');
	writeRemainingParameters(method, true, defaultArguments);
}

bool GenBase::isShortMethod(const FuMethod * method) const
{
	return false;
}

void GenBase::writeBody(const FuMethod * method)
{
	if (method->callType == FuCallType::abstract)
		writeCharLine(';');
	else {
		this->currentMethod = method;
		if (isShortMethod(method)) {
			write(" => ");
			const FuReturn * ret = static_cast<const FuReturn *>(method->body.get());
			writeCoerced(method->type.get(), ret->value.get(), FuPriority::argument);
			writeCharLine(';');
		}
		else {
			writeNewLine();
			openBlock();
			flattenBlock(method->body.get());
			closeBlock();
		}
		this->currentMethod = nullptr;
	}
}

void GenBase::writePublic(const FuContainerType * container)
{
	if (container->isPublic)
		write("public ");
}

void GenBase::writeEnumValue(const FuConst * konst)
{
	writeDoc(konst->documentation.get());
	writeName(konst);
	if (!dynamic_cast<const FuImplicitEnumValue *>(konst->value.get())) {
		write(" = ");
		konst->value->accept(this, FuPriority::argument);
	}
}

void GenBase::visitEnumValue(const FuConst * konst, const FuConst * previous)
{
	if (previous != nullptr)
		writeCharLine(',');
	writeEnumValue(konst);
}

void GenBase::writeRegexOptionsEnum(const FuProgram * program)
{
	if (program->regexOptionsEnum)
		writeEnum(program->system->regexOptionsEnum.get());
}

void GenBase::startClass(const FuClass * klass, std::string_view suffix, std::string_view extendsClause)
{
	write("class ");
	write(klass->name);
	write(suffix);
	if (klass->hasBaseClass()) {
		write(extendsClause);
		writeExceptionClass(klass->parent);
	}
}

void GenBase::openClass(const FuClass * klass, std::string_view suffix, std::string_view extendsClause)
{
	startClass(klass, suffix, extendsClause);
	writeNewLine();
	openBlock();
}

void GenBase::writeMembers(const FuClass * klass, bool constArrays)
{
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const FuConst *konst = dynamic_cast<const FuConst *>(symbol))
			writeConst(konst);
		else if (const FuField *field = dynamic_cast<const FuField *>(symbol))
			writeField(field);
		else if (const FuMethod *method = dynamic_cast<const FuMethod *>(symbol)) {
			writeMethod(method);
			this->switchesWithGoto.clear();
			this->currentTemporaries.clear();
		}
		else if (const FuNative *nat = dynamic_cast<const FuNative *>(symbol))
			visitNative(nat);
		else
			std::abort();
	}
	if (constArrays) {
		for (const FuConst * konst : klass->constArrays)
			writeConst(konst);
	}
}

bool GenBase::writeBaseClass(const FuClass * klass, const FuProgram * program)
{
	if (klass->name == "Exception")
		return false;
	if (this->writtenClasses.contains(klass))
		return false;
	this->writtenClasses.insert(klass);
	if (const FuClass *baseClass = dynamic_cast<const FuClass *>(klass->parent))
		writeClass(baseClass, program);
	return true;
}

void GenBase::writeTypes(const FuProgram * program)
{
	writeRegexOptionsEnum(program);
	for (const FuSymbol * type = program->first; type != nullptr; type = type->next) {
		if (const FuClass *klass = dynamic_cast<const FuClass *>(type))
			writeClass(klass, program);
		else if (const FuEnum *enu = dynamic_cast<const FuEnum *>(type))
			writeEnum(enu);
		else
			std::abort();
	}
}

void GenTyped::writeCoercedLiteral(const FuType * type, const FuExpr * expr)
{
	expr->accept(this, FuPriority::argument);
	if (type != nullptr && type->id == FuId::floatType && dynamic_cast<const FuLiteralDouble *>(expr))
		writeChar('f');
}

void GenTyped::writeTypeAndName(const FuNamedValue * value)
{
	writeType(value->type.get(), true);
	writeChar(' ');
	writeName(value);
}

void GenTyped::visitAggregateInitializer(const FuAggregateInitializer * expr)
{
	write("{ ");
	writeCoercedLiterals(expr->type->asClassType()->getElementType().get(), &expr->items);
	write(" }");
}

void GenTyped::writeArrayStorageLength(const FuExpr * expr)
{
	const FuArrayStorageType * array = static_cast<const FuArrayStorageType *>(expr->type.get());
	visitLiteralLong(array->length);
}

void GenTyped::writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent)
{
	write("new ");
	writeType(elementType->getBaseType(), false);
	writeChar('[');
	lengthExpr->accept(this, FuPriority::argument);
	writeChar(']');
	while (elementType->isArray()) {
		writeChar('[');
		if (const FuArrayStorageType *arrayStorage = dynamic_cast<const FuArrayStorageType *>(elementType))
			arrayStorage->lengthExpr->accept(this, FuPriority::argument);
		writeChar(']');
		elementType = elementType->asClassType()->getElementType().get();
	}
}

int GenTyped::getOneAscii(const FuExpr * expr) const
{
	const FuLiteralString * literal;
	return (literal = dynamic_cast<const FuLiteralString *>(expr)) ? literal->getOneAscii() : -1;
}

void GenTyped::writeCharMethodCall(const FuExpr * obj, std::string_view method, const FuExpr * arg)
{
	obj->accept(this, FuPriority::primary);
	writeChar('.');
	write(method);
	writeChar('(');
	if (!dynamic_cast<const FuLiteralChar *>(arg))
		write("(char) ");
	arg->accept(this, FuPriority::primary);
	writeChar(')');
}

bool GenTyped::isNarrower(FuId left, FuId right)
{
	switch (left) {
	case FuId::sByteRange:
		switch (right) {
		case FuId::byteRange:
		case FuId::shortRange:
		case FuId::uShortRange:
		case FuId::intType:
		case FuId::nIntType:
		case FuId::longType:
			return true;
		default:
			return false;
		}
	case FuId::byteRange:
		switch (right) {
		case FuId::sByteRange:
		case FuId::shortRange:
		case FuId::uShortRange:
		case FuId::intType:
		case FuId::nIntType:
		case FuId::longType:
			return true;
		default:
			return false;
		}
	case FuId::shortRange:
		switch (right) {
		case FuId::uShortRange:
		case FuId::intType:
		case FuId::nIntType:
		case FuId::longType:
			return true;
		default:
			return false;
		}
	case FuId::uShortRange:
		switch (right) {
		case FuId::shortRange:
		case FuId::intType:
		case FuId::nIntType:
		case FuId::longType:
			return true;
		default:
			return false;
		}
	case FuId::intType:
		return right == FuId::longType;
	default:
		return false;
	}
}

const FuExpr * GenTyped::getStaticCastInner(const FuType * type, const FuExpr * expr) const
{
	const FuBinaryExpr * binary;
	const FuLiteralLong * rightMask;
	if ((binary = dynamic_cast<const FuBinaryExpr *>(expr)) && binary->op == FuToken::and_ && (rightMask = dynamic_cast<const FuLiteralLong *>(binary->right.get())) && dynamic_cast<const FuIntegerType *>(type)) {
		int64_t mask;
		switch (type->id) {
		case FuId::byteRange:
		case FuId::sByteRange:
			mask = 255;
			break;
		case FuId::shortRange:
		case FuId::uShortRange:
			mask = 65535;
			break;
		case FuId::intType:
			mask = 4294967295;
			break;
		default:
			return expr;
		}
		if ((rightMask->value & mask) == mask)
			return binary->left.get();
	}
	return expr;
}

void GenTyped::writeStaticCastType(const FuType * type)
{
	writeChar('(');
	writeType(type, false);
	write(") ");
}

void GenTyped::writeStaticCast(const FuType * type, const FuExpr * expr)
{
	writeStaticCastType(type);
	getStaticCastInner(type, expr)->accept(this, FuPriority::primary);
}

void GenTyped::writeNotPromoted(const FuType * type, const FuExpr * expr)
{
	if (dynamic_cast<const FuIntegerType *>(type) && isNarrower(type->id, getTypeId(expr->type.get(), true)))
		writeStaticCast(type, expr);
	else
		writeCoercedLiteral(type, expr);
}

bool GenTyped::isPromoted(const FuExpr * expr) const
{
	const FuBinaryExpr * binary;
	return !((binary = dynamic_cast<const FuBinaryExpr *>(expr)) && (binary->op == FuToken::leftBracket || binary->isAssign()));
}

void GenTyped::writeAssignRight(const FuBinaryExpr * expr)
{
	if (expr->left->isIndexing()) {
		if (dynamic_cast<const FuLiteralLong *>(expr->right.get())) {
			writeCoercedLiteral(expr->left->type.get(), expr->right.get());
			return;
		}
		FuId leftTypeId = expr->left->type->id;
		FuId rightTypeId = getTypeId(expr->right->type.get(), isPromoted(expr->right.get()));
		if (leftTypeId == FuId::sByteRange && rightTypeId == FuId::sByteRange) {
			expr->right->accept(this, FuPriority::assign);
			return;
		}
		if (isNarrower(leftTypeId, rightTypeId)) {
			writeStaticCast(expr->left->type.get(), expr->right.get());
			return;
		}
	}
	GenBase::writeAssignRight(expr);
}

void GenTyped::writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	if (dynamic_cast<const FuIntegerType *>(type) && type->id != FuId::longType && expr->type->id == FuId::longType)
		writeStaticCast(type, expr);
	else if (type->id == FuId::floatType && expr->type->id == FuId::doubleType) {
		if (const FuLiteralDouble *literal = dynamic_cast<const FuLiteralDouble *>(expr)) {
			visitLiteralDouble(literal->value);
			writeChar('f');
		}
		else
			writeStaticCast(type, expr);
	}
	else if (dynamic_cast<const FuIntegerType *>(type) && expr->type->id == FuId::floatIntType) {
		const FuCallExpr * call;
		if ((call = dynamic_cast<const FuCallExpr *>(expr)) && call->method->symbol->id == FuId::mathTruncate) {
			expr = call->arguments[0].get();
			if (const FuLiteralDouble *literal = dynamic_cast<const FuLiteralDouble *>(expr)) {
				visitLiteralLong(static_cast<int64_t>(literal->value));
				return;
			}
		}
		writeStaticCast(type, expr);
	}
	else
		GenBase::writeCoercedInternal(type, expr, parent);
}

void GenTyped::writeCharAt(const FuBinaryExpr * expr)
{
	writeIndexing(expr->left.get(), expr->right.get());
}

void GenTyped::startTemporaryVar(const FuType * type)
{
	writeType(type, true);
	writeChar(' ');
}

void GenTyped::writeAssertCast(const FuBinaryExpr * expr)
{
	const FuVar * def = static_cast<const FuVar *>(expr->right.get());
	writeTypeAndName(def);
	write(" = ");
	writeStaticCast(def->type.get(), expr->left.get());
	writeCharLine(';');
}

void GenTyped::writeExceptionConstructor(const FuClass * klass, std::string_view s)
{
	write("public ");
	write(klass->name);
	writeLine(s);
}

void GenCCppD::visitLiteralLong(int64_t i)
{
	if (i == (-9223372036854775807 - 1))
		write("(-9223372036854775807 - 1)");
	else
		GenBase::visitLiteralLong(i);
}

bool GenCCppD::isPtrTo(const FuExpr * ptr, const FuExpr * other)
{
	const FuClassType * klass;
	return (klass = dynamic_cast<const FuClassType *>(ptr->type.get())) && klass->class_->id != FuId::stringClass && klass->isAssignableFrom(other->type.get());
}

void GenCCppD::writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	const FuType * coercedType;
	if (isPtrTo(left, right))
		coercedType = left->type.get();
	else if (isPtrTo(right, left))
		coercedType = right->type.get();
	else {
		GenBase::writeEqual(left, right, parent, not_);
		return;
	}
	if (parent > FuPriority::equality)
		writeChar('(');
	writeCoerced(coercedType, left, FuPriority::equality);
	write(getEqOp(not_));
	writeCoerced(coercedType, right, FuPriority::equality);
	if (parent > FuPriority::equality)
		writeChar(')');
}

void GenCCppD::visitConst(const FuConst * statement)
{
	if (dynamic_cast<const FuArrayStorageType *>(statement->type.get()))
		writeConst(statement);
}

void GenCCppD::writeSwitchAsIfsWithGoto(const FuSwitch * statement)
{
	if (std::any_of(statement->cases.begin(), statement->cases.end(), [](const FuCase &kase) { return FuSwitch::hasEarlyBreakAndContinue(&kase.body); }) || FuSwitch::hasEarlyBreakAndContinue(&statement->defaultBody)) {
		int gotoId = std::ssize(this->switchesWithGoto);
		this->switchesWithGoto.push_back(statement);
		writeSwitchAsIfs(statement, false);
		write("fuafterswitch");
		visitLiteralLong(gotoId);
		writeLine(": ;");
	}
	else
		writeSwitchAsIfs(statement, true);
}

void GenCCppD::writeThrowNoMessage()
{
	write("\"\"");
}

void GenCCpp::writeDocCode(std::string_view s)
{
	if (s == "null")
		visitLiteralNull();
	else
		writeXmlDoc(s);
}

void GenCCpp::writeCIncludes()
{
	writeIncludes("#include <", ">");
}

int GenCCpp::getLiteralChars() const
{
	return 127;
}

void GenCCpp::writeNumericType(FuId id)
{
	switch (id) {
	case FuId::sByteRange:
		includeStdInt();
		write("int8_t");
		break;
	case FuId::byteRange:
		includeStdInt();
		write("uint8_t");
		break;
	case FuId::shortRange:
		includeStdInt();
		write("int16_t");
		break;
	case FuId::uShortRange:
		includeStdInt();
		write("uint16_t");
		break;
	case FuId::intType:
		write("int");
		break;
	case FuId::nIntType:
		includeStdDef();
		write("ptrdiff_t");
		break;
	case FuId::longType:
		includeStdInt();
		write("int64_t");
		break;
	case FuId::floatType:
		write("float");
		break;
	case FuId::doubleType:
		write("double");
		break;
	default:
		std::abort();
	}
}

void GenCCpp::writeArrayLength(const FuExpr * expr, FuPriority parent)
{
	if (parent > FuPriority::add)
		writeChar('(');
	write("argc - 1");
	if (parent > FuPriority::add)
		writeChar(')');
}

void GenCCpp::writeArgsIndexing(const FuExpr * index)
{
	write("argv[");
	if (const FuLiteralLong *literal = dynamic_cast<const FuLiteralLong *>(index))
		visitLiteralLong(1 + literal->value);
	else {
		write("1 + ");
		index->accept(this, FuPriority::add);
	}
	writeChar(']');
}

void GenCCpp::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::mathNaN:
		includeMath();
		write("NAN");
		break;
	case FuId::mathNegativeInfinity:
		includeMath();
		write("-INFINITY");
		break;
	case FuId::mathPositiveInfinity:
		includeMath();
		write("INFINITY");
		break;
	default:
		GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

const FuExpr * GenCCpp::isStringEmpty(const FuBinaryExpr * expr)
{
	const FuSymbolReference * symbol;
	if ((symbol = dynamic_cast<const FuSymbolReference *>(expr->left.get())) && symbol->symbol->id == FuId::stringLength && expr->right->isLiteralZero())
		return symbol->left.get();
	return nullptr;
}

void GenCCpp::writeArrayPtrAdd(const FuExpr * array, const FuExpr * index)
{
	if (index->isLiteralZero())
		writeArrayPtr(array, FuPriority::argument);
	else {
		writeArrayPtr(array, FuPriority::add);
		write(" + ");
		index->accept(this, FuPriority::mul);
	}
}

const FuCallExpr * GenCCpp::isStringSubstring(const FuExpr * expr)
{
	if (const FuCallExpr *call = dynamic_cast<const FuCallExpr *>(expr)) {
		FuId id = call->method->symbol->id;
		if ((id == FuId::stringSubstring && std::ssize(call->arguments) == 2) || id == FuId::uTF8GetString)
			return call;
	}
	return nullptr;
}

bool GenCCpp::isUTF8GetString(const FuCallExpr * call)
{
	return call->method->symbol->id == FuId::uTF8GetString;
}

const FuExpr * GenCCpp::isTrimSubstring(const FuBinaryExpr * expr)
{
	const FuCallExpr * call = isStringSubstring(expr->right.get());
	const FuSymbolReference * leftSymbol;
	if (call != nullptr && !isUTF8GetString(call) && (leftSymbol = dynamic_cast<const FuSymbolReference *>(expr->left.get())) && call->method->left->isReferenceTo(leftSymbol->symbol) && call->arguments[0]->isLiteralZero())
		return call->arguments[1].get();
	return nullptr;
}

void GenCCpp::writeStringLiteralWithNewLine(std::string_view s)
{
	writeChar('"');
	write(s);
	write("\\n\"");
}

void GenCCpp::writeUnreachable(const FuAssert * statement)
{
	write("abort();");
	if (statement->message != nullptr) {
		write(" // ");
		statement->message->accept(this, FuPriority::argument);
	}
	writeNewLine();
}

void GenCCpp::writeAssert(const FuAssert * statement)
{
	if (statement->completesNormally()) {
		writeTemporaries(statement->cond.get());
		includeAssert();
		write("assert(");
		if (statement->message == nullptr)
			statement->cond->accept(this, FuPriority::argument);
		else {
			statement->cond->accept(this, FuPriority::condAnd);
			write(" && ");
			statement->message->accept(this, FuPriority::argument);
		}
		writeLine(");");
	}
	else
		writeUnreachable(statement);
}

void GenCCpp::visitReturn(const FuReturn * statement)
{
	if (statement->value == nullptr && this->currentMethod->id == FuId::main)
		writeLine("return 0;");
	else
		GenBase::visitReturn(statement);
}

void GenCCpp::visitSwitch(const FuSwitch * statement)
{
	if (dynamic_cast<const FuStringType *>(statement->value->type.get()) || statement->hasWhen())
		writeSwitchAsIfsWithGoto(statement);
	else
		GenBase::visitSwitch(statement);
}

void GenCCpp::writeMethods(const FuClass * klass)
{
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const FuMethod *method = dynamic_cast<const FuMethod *>(symbol)) {
			writeMethod(method);
			this->currentTemporaries.clear();
		}
	}
}

void GenCCpp::writeClass(const FuClass * klass, const FuProgram * program)
{
	if (!writeBaseClass(klass, program))
		return;
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const FuField * field;
		const FuStorageType * storage;
		if ((field = dynamic_cast<const FuField *>(symbol)) && (storage = dynamic_cast<const FuStorageType *>(field->type->getBaseType())) && storage->class_->id == FuId::none)
			writeClass(storage->class_, program);
	}
	writeClassInternal(klass);
}

std::string GenCCpp::changeExtension(std::string_view path, std::string_view ext)
{
	int extIndex = std::ssize(path);
	for (int i = extIndex; --i >= 0 && path[i] != '/' && path[i] != '\\';) {
		if (path[i] == '.') {
			extIndex = i;
			break;
		}
	}
	return std::string(path.data(), extIndex) + std::string(ext);
}

void GenCCpp::createHeaderFile(std::string_view headerExt)
{
	createFile(std::string_view(), changeExtension(this->outputFile, headerExt));
	writeLine("#pragma once");
	writeCIncludes();
}

std::string GenCCpp::getFilenameWithoutExtension(std::string_view path)
{
	int pathLength = std::ssize(path);
	int extIndex = pathLength;
	int i = pathLength;
	while (--i >= 0 && path[i] != '/' && path[i] != '\\') {
		if (path[i] == '.' && extIndex == pathLength)
			extIndex = i;
	}
	i++;
	return std::string(path.data() + i, extIndex - i);
}

void GenCCpp::createImplementationFile(const FuProgram * program, std::string_view headerExt)
{
	createOutputFile();
	writeTopLevelNatives(program);
	writeCIncludes();
	write("#include \"");
	write(getFilenameWithoutExtension(this->outputFile));
	write(headerExt);
	writeCharLine('"');
}

std::string_view GenC::getTargetName() const
{
	return "C";
}

void GenC::writeSelfDoc(const FuMethod * method)
{
	if (method->callType == FuCallType::static_)
		return;
	write(" * @param self This <code>");
	writeName(method->parent);
	writeLine("</code>.");
}

void GenC::writeReturnDoc(const FuMethod * method)
{
	if (std::ssize(method->throws) == 0)
		return;
	write(" * @return <code>");
	writeThrowReturnValue(method->type.get(), false);
	writeLine("</code> on error.");
}

void GenC::writeThrowsDoc(const FuThrowsDeclaration * decl)
{
}

void GenC::includeStdInt()
{
	include("stdint.h");
}

void GenC::includeStdDef()
{
	include("stddef.h");
}

void GenC::includeAssert()
{
	include("assert.h");
}

void GenC::includeMath()
{
	include("math.h");
}

void GenC::includeStdBool()
{
	include("stdbool.h");
}

void GenC::visitLiteralNull()
{
	write("NULL");
}

void GenC::writePrintfLongPrefix()
{
	write("ll");
}

void GenC::writePrintfWidth(const FuInterpolatedPart * part)
{
	GenBase::writePrintfWidth(part);
	if (isStringSubstring(part->argument.get()) != nullptr) {
		assert(part->precision < 0);
		write(".*");
	}
	if (part->argument->type->id == FuId::nIntType)
		writeChar('t');
	else if (part->argument->type->id == FuId::longType)
		writePrintfLongPrefix();
}

void GenC::writeInterpolatedStringArgBase(const FuExpr * expr)
{
	if (expr->type->id == FuId::longType) {
		write("(long long) ");
		expr->accept(this, FuPriority::primary);
	}
	else
		expr->accept(this, FuPriority::argument);
}

void GenC::writeStringPtrAdd(const FuCallExpr * call, bool cast)
{
	if (isUTF8GetString(call)) {
		if (cast)
			write("(const char *) ");
		writeArrayPtrAdd(call->arguments[0].get(), call->arguments[1].get());
	}
	else
		writeArrayPtrAdd(call->method->left.get(), call->arguments[0].get());
}

bool GenC::isDictionaryClassStgIndexing(const FuExpr * expr)
{
	const FuBinaryExpr * indexing;
	const FuClassType * dict;
	return (indexing = dynamic_cast<const FuBinaryExpr *>(expr)) && indexing->op == FuToken::leftBracket && (dict = dynamic_cast<const FuClassType *>(indexing->left->type.get())) && dict->class_->typeParameterCount == 2 && dynamic_cast<const FuStorageType *>(dict->getValueType().get());
}

void GenC::startTemporaryVar(const FuType * type)
{
	startDefinition(type, true, true);
}

void GenC::writeUpcast(const FuClass * resultClass, const FuSymbol * klass)
{
	for (; klass != resultClass; klass = klass->parent)
		write(".base");
}

void GenC::writeClassPtr(const FuClass * resultClass, const FuExpr * expr, FuPriority parent)
{
	const FuStorageType * storage;
	const FuClassType * ptr;
	if ((storage = dynamic_cast<const FuStorageType *>(expr->type.get())) && storage->class_->id == FuId::none && !isDictionaryClassStgIndexing(expr)) {
		writeChar('&');
		expr->accept(this, FuPriority::primary);
		writeUpcast(resultClass, storage->class_);
	}
	else if ((ptr = dynamic_cast<const FuClassType *>(expr->type.get())) && ptr->class_ != resultClass) {
		writeChar('&');
		writePostfix(expr, "->base");
		writeUpcast(resultClass, ptr->class_->parent);
	}
	else
		expr->accept(this, parent);
}

void GenC::writeInterpolatedStringArg(const FuExpr * expr)
{
	const FuCallExpr * call = isStringSubstring(expr);
	if (call != nullptr) {
		getStringSubstringLength(call)->accept(this, FuPriority::argument);
		write(", ");
		writeStringPtrAdd(call, true);
	}
	else {
		const FuClassType * klass;
		if ((klass = dynamic_cast<const FuClassType *>(expr->type.get())) && klass->class_->id != FuId::stringClass) {
			write(this->namespace_);
			write(klass->class_->name);
			write("_ToString(");
			writeClassPtr(klass->class_, expr, FuPriority::argument);
			writeChar(')');
		}
		else
			writeInterpolatedStringArgBase(expr);
	}
}

void GenC::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	if (tryWriteTemporary(expr))
		return;
	include("stdarg.h");
	include("stdio.h");
	this->stringFormat = true;
	write("FuString_Format(");
	writePrintf(expr, false);
}

void GenC::writeCamelCaseNotKeyword(std::string_view name)
{
	if (name == "this")
		write("self");
	else if (name == "Asm" || name == "Assert" || name == "Auto" || name == "Bool" || name == "Break" || name == "Byte" || name == "Case" || name == "Char" || name == "Class" || name == "Const" || name == "Continue" || name == "Default" || name == "Do" || name == "Double" || name == "Else" || name == "Enum" || name == "Extern" || name == "False" || name == "Float" || name == "For" || name == "Foreach" || name == "Goto" || name == "If" || name == "Inline" || name == "Int" || name == "Long" || name == "Register" || name == "Restrict" || name == "Return" || name == "Short" || name == "Signed" || name == "Sizeof" || name == "Static" || name == "Struct" || name == "Switch" || name == "True" || name == "Typedef" || name == "Typeof" || name == "Union" || name == "Unsigned" || name == "Void" || name == "Volatile" || name == "While" || name == "asm" || name == "auto" || name == "char" || name == "extern" || name == "goto" || name == "inline" || name == "register" || name == "restrict" || name == "signed" || name == "sizeof" || name == "struct" || name == "typedef" || name == "typeof" || name == "union" || name == "unsigned" || name == "volatile") {
		writeCamelCase(name);
		writeChar('_');
	}
	else
		writeCamelCase(name);
}

void GenC::writeName(const FuSymbol * symbol)
{
	if (dynamic_cast<const FuContainerType *>(symbol)) {
		write(this->namespace_);
		write(symbol->name);
	}
	else if (dynamic_cast<const FuMethod *>(symbol)) {
		write(this->namespace_);
		write(symbol->parent->name);
		writeChar('_');
		write(symbol->name);
	}
	else if (dynamic_cast<const FuConst *>(symbol)) {
		if (dynamic_cast<const FuContainerType *>(symbol->parent)) {
			write(this->namespace_);
			write(symbol->parent->name);
			writeChar('_');
		}
		writeUppercaseWithUnderscores(symbol->name);
	}
	else
		writeCamelCaseNotKeyword(symbol->name);
}

void GenC::writeForeachArrayIndexing(const FuForeach * forEach, const FuSymbol * symbol)
{
	if (forEach->collection->type->id == FuId::mainArgsType)
		write("argv");
	else
		forEach->collection->accept(this, FuPriority::primary);
	writeChar('[');
	writeCamelCaseNotKeyword(symbol->name);
	writeChar(']');
}

void GenC::writeSelfForField(const FuSymbol * fieldClass)
{
	assert(dynamic_cast<const FuClass *>(fieldClass));
	write("self->");
	for (const FuSymbol * klass = this->currentClass; klass != fieldClass; klass = klass->parent)
		write("base.");
}

void GenC::writeLocalName(const FuSymbol * symbol, FuPriority parent)
{
	if (const FuForeach *forEach = dynamic_cast<const FuForeach *>(symbol->parent)) {
		const FuClassType * klass = static_cast<const FuClassType *>(forEach->collection->type.get());
		if (klass->class_->id == FuId::stringClass || (klass->class_->id == FuId::listClass && !dynamic_cast<const FuStorageType *>(klass->getElementType().get()))) {
			if (parent == FuPriority::primary)
				writeChar('(');
			writeChar('*');
			writeCamelCaseNotKeyword(symbol->name);
			if (parent == FuPriority::primary)
				writeChar(')');
			return;
		}
		else if (klass->class_->id == FuId::arrayStorageClass) {
			if (dynamic_cast<const FuStorageType *>(klass->getElementType().get())) {
				if (parent > FuPriority::add)
					writeChar('(');
				forEach->collection->accept(this, FuPriority::add);
				write(" + ");
				writeCamelCaseNotKeyword(symbol->name);
				if (parent > FuPriority::add)
					writeChar(')');
			}
			else
				writeForeachArrayIndexing(forEach, symbol);
			return;
		}
	}
	if (dynamic_cast<const FuField *>(symbol))
		writeSelfForField(symbol->parent);
	writeName(symbol);
}

void GenC::writeMatchProperty(const FuSymbolReference * expr, int which)
{
	this->matchPos = true;
	write("FuMatch_GetPos(");
	expr->left->accept(this, FuPriority::argument);
	write(", ");
	visitLiteralLong(which);
	writeChar(')');
}

void GenC::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::stringLength:
		writeStringLength(expr->left.get());
		break;
	case FuId::consoleError:
		include("stdio.h");
		write("stderr");
		break;
	case FuId::listCount:
	case FuId::stackCount:
		writePostfix(expr->left.get(), "->len");
		break;
	case FuId::queueCount:
		expr->left->accept(this, FuPriority::primary);
		if (dynamic_cast<const FuStorageType *>(expr->left->type.get()))
			writeChar('.');
		else
			write("->");
		write("length");
		break;
	case FuId::hashSetCount:
	case FuId::dictionaryCount:
		writeCall("g_hash_table_size", expr->left.get());
		break;
	case FuId::sortedSetCount:
	case FuId::sortedDictionaryCount:
		writeCall("g_tree_nnodes", expr->left.get());
		break;
	case FuId::matchStart:
		writeMatchProperty(expr, 0);
		break;
	case FuId::matchEnd:
		writeMatchProperty(expr, 1);
		break;
	case FuId::matchLength:
		writeMatchProperty(expr, 2);
		break;
	case FuId::matchValue:
		if (!tryWriteTemporary(expr)) {
			write("g_match_info_fetch(");
			expr->left->accept(this, FuPriority::argument);
			write(", 0)");
		}
		break;
	default:
		if (expr->left == nullptr || dynamic_cast<const FuConst *>(expr->symbol))
			writeLocalName(expr->symbol, parent);
		else if (isDictionaryClassStgIndexing(expr->left.get())) {
			writePostfix(expr->left.get(), "->");
			writeName(expr->symbol);
		}
		else {
			const FuSymbolReference * symbol;
			const FuForeach * forEach;
			const FuArrayStorageType * array;
			if ((symbol = dynamic_cast<const FuSymbolReference *>(expr->left.get())) && (forEach = dynamic_cast<const FuForeach *>(symbol->symbol->parent)) && (array = dynamic_cast<const FuArrayStorageType *>(forEach->collection->type.get()))) {
				writeForeachArrayIndexing(forEach, symbol->symbol);
				writeMemberAccess(array->getElementType().get(), expr->symbol->parent);
				writeName(expr->symbol);
			}
			else
				GenCCpp::visitSymbolReference(expr, parent);
		}
		break;
	}
}

void GenC::writeGlib(std::string_view s)
{
	include("glib.h");
	write(s);
}

void GenC::writeStringPtrType()
{
	write("const char *");
}

void GenC::writeClassType(const FuClassType * klass, bool space)
{
	switch (klass->class_->id) {
	case FuId::none:
		if (!dynamic_cast<const FuReadWriteClassType *>(klass))
			write("const ");
		writeName(klass->class_);
		if (!dynamic_cast<const FuStorageType *>(klass))
			write(" *");
		else if (space)
			writeChar(' ');
		break;
	case FuId::stringClass:
		if (klass->id == FuId::stringStorageType)
			write("char *");
		else
			writeStringPtrType();
		break;
	case FuId::listClass:
	case FuId::stackClass:
		writeGlib("GArray *");
		break;
	case FuId::queueClass:
		writeGlib("GQueue ");
		if (!dynamic_cast<const FuStorageType *>(klass))
			writeChar('*');
		break;
	case FuId::hashSetClass:
	case FuId::dictionaryClass:
		writeGlib("GHashTable *");
		break;
	case FuId::sortedSetClass:
	case FuId::sortedDictionaryClass:
		writeGlib("GTree *");
		break;
	case FuId::textWriterClass:
		include("stdio.h");
		write("FILE *");
		break;
	case FuId::stringWriterClass:
		writeGlib("GString *");
		break;
	case FuId::regexClass:
		if (!dynamic_cast<const FuReadWriteClassType *>(klass))
			write("const ");
		writeGlib("GRegex *");
		break;
	case FuId::matchClass:
		if (!dynamic_cast<const FuReadWriteClassType *>(klass))
			write("const ");
		writeGlib("GMatchInfo *");
		break;
	case FuId::lockClass:
		notYet(klass, "Lock");
		include("threads.h");
		write("mtx_t ");
		break;
	default:
		notSupported(klass, klass->class_->name);
		break;
	}
}

void GenC::writeArrayPrefix(const FuType * type)
{
	const FuClassType * array;
	if ((array = dynamic_cast<const FuClassType *>(type)) && array->isArray()) {
		writeArrayPrefix(array->getElementType().get());
		if (!dynamic_cast<const FuArrayStorageType *>(type)) {
			if (dynamic_cast<const FuArrayStorageType *>(array->getElementType().get()))
				writeChar('(');
			if (!dynamic_cast<const FuReadWriteClassType *>(type))
				write("const ");
			writeChar('*');
		}
	}
}

void GenC::startDefinition(const FuType * type, bool promote, bool space)
{
	const FuType * baseType = type->getBaseType();
	if (dynamic_cast<const FuIntegerType *>(baseType)) {
		writeNumericType(getTypeId(baseType, promote && type == baseType));
		if (space)
			writeChar(' ');
	}
	else if (dynamic_cast<const FuEnum *>(baseType)) {
		if (baseType->id == FuId::boolType) {
			includeStdBool();
			write("bool");
		}
		else
			writeName(baseType);
		if (space)
			writeChar(' ');
	}
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(baseType))
		writeClassType(klass, space);
	else {
		write(baseType->name);
		if (space)
			writeChar(' ');
	}
	writeArrayPrefix(type);
}

void GenC::endDefinition(const FuType * type)
{
	while (type->isArray()) {
		const FuType * elementType = type->asClassType()->getElementType().get();
		if (const FuArrayStorageType *arrayStorage = dynamic_cast<const FuArrayStorageType *>(type)) {
			writeChar('[');
			visitLiteralLong(arrayStorage->length);
			writeChar(']');
		}
		else if (dynamic_cast<const FuArrayStorageType *>(elementType))
			writeChar(')');
		type = elementType;
	}
}

void GenC::writeReturnType(const FuMethod * method)
{
	if (method->type->id == FuId::voidType && std::ssize(method->throws) > 0) {
		includeStdBool();
		write("bool ");
	}
	else
		startDefinition(method->type.get(), true, true);
}

void GenC::writeType(const FuType * type, bool promote)
{
	const FuClassType * arrayPtr;
	startDefinition(type, promote, (arrayPtr = dynamic_cast<const FuClassType *>(type)) && arrayPtr->class_->id == FuId::arrayPtrClass);
	endDefinition(type);
}

void GenC::writeTypeAndName(const FuNamedValue * value)
{
	startDefinition(value->type.get(), true, true);
	writeName(value);
	endDefinition(value->type.get());
}

void GenC::writeDynamicArrayCast(const FuType * elementType)
{
	writeChar('(');
	startDefinition(elementType, false, true);
	write(elementType->isArray() ? "(*)" : "*");
	endDefinition(elementType);
	write(") ");
}

void GenC::writeXstructorPtr(bool need, const FuClass * klass, std::string_view name)
{
	if (need) {
		write("(FuMethodPtr) ");
		writeName(klass);
		writeChar('_');
		write(name);
	}
	else
		write("NULL");
}

bool GenC::isHeapAllocated(const FuType * type)
{
	const FuStorageType * storage;
	return type->id == FuId::stringStorageType || dynamic_cast<const FuDynamicPtrType *>(type) || ((storage = dynamic_cast<const FuStorageType *>(type)) && storage->class_->id == FuId::matchClass);
}

bool GenC::needToDestructType(const FuType * type)
{
	if (isHeapAllocated(type))
		return true;
	if (const FuStorageType *storage = dynamic_cast<const FuStorageType *>(type)) {
		switch (storage->class_->id) {
		case FuId::listClass:
		case FuId::queueClass:
		case FuId::stackClass:
		case FuId::hashSetClass:
		case FuId::sortedSetClass:
		case FuId::dictionaryClass:
		case FuId::sortedDictionaryClass:
		case FuId::stringWriterClass:
		case FuId::matchClass:
		case FuId::lockClass:
			return true;
		default:
			return needsDestructor(storage->class_);
		}
	}
	return false;
}

bool GenC::needToDestruct(const FuSymbol * symbol)
{
	const FuType * type = symbol->type.get();
	while (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(type))
		type = array->getElementType().get();
	return needToDestructType(type);
}

bool GenC::needsDestructor(const FuClass * klass)
{
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const FuField * field;
		if ((field = dynamic_cast<const FuField *>(symbol)) && needToDestruct(field))
			return true;
	}
	const FuClass * baseClass;
	return (baseClass = dynamic_cast<const FuClass *>(klass->parent)) && needsDestructor(baseClass);
}

void GenC::writeXstructorPtrs(const FuClass * klass)
{
	writeXstructorPtr(needsConstructor(klass), klass, "Construct");
	write(", ");
	writeXstructorPtr(needsDestructor(klass), klass, "Destruct");
}

void GenC::writeListFreeName(FuId id)
{
	write("FuList_Free");
	switch (id) {
	case FuId::none:
		write("Shared");
		break;
	case FuId::stringClass:
		write("String");
		break;
	case FuId::listClass:
		write("List");
		break;
	case FuId::queueClass:
		write("Queue");
		break;
	case FuId::dictionaryClass:
		write("HashTable");
		break;
	case FuId::sortedDictionaryClass:
		write("Tree");
		break;
	case FuId::regexClass:
		write("Regex");
		break;
	case FuId::matchClass:
		write("Match");
		break;
	default:
		std::abort();
	}
}

void GenC::addListFree(FuId id)
{
	this->listFrees.insert(id);
	writeListFreeName(id);
}

void GenC::writeListFree(const FuClassType * elementType)
{
	switch (elementType->class_->id) {
	case FuId::none:
	case FuId::arrayPtrClass:
		if (dynamic_cast<const FuDynamicPtrType *>(elementType)) {
			this->sharedRelease = true;
			addListFree(FuId::none);
		}
		else {
			write("(GDestroyNotify) ");
			writeName(elementType->class_);
			write("_Destruct");
		}
		break;
	case FuId::stringClass:
		addListFree(FuId::stringClass);
		break;
	case FuId::listClass:
	case FuId::stackClass:
		addListFree(FuId::listClass);
		break;
	case FuId::queueClass:
		addListFree(FuId::queueClass);
		break;
	case FuId::hashSetClass:
	case FuId::dictionaryClass:
		addListFree(FuId::dictionaryClass);
		break;
	case FuId::sortedSetClass:
	case FuId::sortedDictionaryClass:
		addListFree(FuId::sortedDictionaryClass);
		break;
	case FuId::stringWriterClass:
		addListFree(FuId::stringWriterClass);
		break;
	case FuId::regexClass:
		addListFree(FuId::regexClass);
		break;
	case FuId::matchClass:
		addListFree(FuId::matchClass);
		break;
	default:
		std::abort();
	}
}

void GenC::writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent)
{
	this->sharedMake = true;
	if (parent > FuPriority::mul)
		writeChar('(');
	writeDynamicArrayCast(elementType);
	write("FuShared_Make(");
	lengthExpr->accept(this, FuPriority::argument);
	write(", sizeof(");
	writeType(elementType, false);
	write("), ");
	if (dynamic_cast<const FuStringStorageType *>(elementType)) {
		this->ptrConstruct = true;
		write("(FuMethodPtr) FuPtr_Construct, ");
		addListFree(FuId::stringClass);
	}
	else if (const FuStorageType *storage = dynamic_cast<const FuStorageType *>(elementType))
		writeXstructorPtrs(storage->class_);
	else if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(elementType)) {
		this->ptrConstruct = true;
		write("(FuMethodPtr) FuPtr_Construct, ");
		writeListFree(dynamic);
	}
	else
		write("NULL, NULL");
	writeChar(')');
	if (parent > FuPriority::mul)
		writeChar(')');
}

const FuExpr * GenC::getStringSubstringLength(const FuCallExpr * call)
{
	return call->arguments[isUTF8GetString(call) ? 2 : 1].get();
}

void GenC::writeStringStorageValue(const FuExpr * expr)
{
	if (expr->isNewString(false))
		expr->accept(this, FuPriority::argument);
	else {
		include("string.h");
		writeCall("strdup", expr);
	}
}

void GenC::writeArrayStorageInit(const FuArrayStorageType * array, const FuExpr * value)
{
	const FuLiteral * literal;
	if (value == nullptr) {
		if (isHeapAllocated(array->getStorageType()))
			write(" = { NULL }");
	}
	else if ((literal = dynamic_cast<const FuLiteral *>(value)) && literal->isDefaultValue()) {
		write(" = { ");
		literal->accept(this, FuPriority::argument);
		write(" }");
	}
	else
		std::abort();
}

bool GenC::isUnique(const FuDynamicPtrType * dynamic)
{
	return dynamic->unique && dynamic->class_->id == FuId::arrayPtrClass && !hasDictionaryDestroy(dynamic->getElementType().get());
}

bool GenC::writeDestructMethodName(const FuClassType * klass)
{
	switch (klass->class_->id) {
	case FuId::none:
	case FuId::arrayPtrClass:
	case FuId::jsonElementClass:
		if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(klass)) {
			if (isUnique(dynamic))
				write("free");
			else {
				this->sharedRelease = true;
				write("FuShared_Release");
			}
			return false;
		}
		writeName(klass->class_);
		write("_Destruct");
		return true;
	case FuId::stringClass:
		write("free");
		return false;
	case FuId::listClass:
	case FuId::stackClass:
		write("g_array_unref");
		return false;
	case FuId::queueClass:
		write(hasDictionaryDestroy(klass->getElementType().get()) ? "g_queue_clear_full" : "g_queue_clear");
		return true;
	case FuId::hashSetClass:
	case FuId::dictionaryClass:
		write("g_hash_table_unref");
		return false;
	case FuId::sortedSetClass:
	case FuId::sortedDictionaryClass:
		write("g_tree_unref");
		return false;
	case FuId::stringWriterClass:
		write("g_string_free");
		return false;
	case FuId::regexClass:
		write("g_regex_unref");
		return false;
	case FuId::matchClass:
		write("g_match_info_unref");
		return false;
	case FuId::lockClass:
		write("mtx_destroy");
		return true;
	default:
		std::abort();
	}
}

void GenC::startDestructCall(const FuClassType * klass)
{
	bool addressOf = writeDestructMethodName(klass);
	writeChar('(');
	if (addressOf)
		writeChar('&');
}

bool GenC::hasDictionaryDestroy(const FuType * type)
{
	return dynamic_cast<const FuOwningType *>(type) || dynamic_cast<const FuStringStorageType *>(type);
}

void GenC::writeDictionaryDestroy(const FuType * type)
{
	if (type == nullptr)
		write("NULL");
	else if (dynamic_cast<const FuStringStorageType *>(type) || dynamic_cast<const FuArrayStorageType *>(type))
		write("free");
	else if (const FuOwningType *owning = dynamic_cast<const FuOwningType *>(type))
		do {
			if (owning->class_->id == FuId::none) {
				if (dynamic_cast<const FuStorageType *>(type)) {
					if (needsDestructor(owning->class_)) {
						write("(GDestroyNotify) ");
						writeName(owning->class_);
						write("_Delete");
					}
					else
						write("free");
					break;
				}
			}
			else
				write("(GDestroyNotify) ");
			writeDestructMethodName(owning);
		}
		while (false);
	else
		write("NULL");
}

void GenC::writeHashEqual(const FuType * keyType)
{
	write(dynamic_cast<const FuStringType *>(keyType) ? "g_str_hash, g_str_equal" : "NULL, NULL");
}

void GenC::writeNewHashTable(const FuType * keyType, const FuType * valueType)
{
	write("g_hash_table_new");
	if (hasDictionaryDestroy(keyType) || hasDictionaryDestroy(valueType)) {
		write("_full(");
		writeHashEqual(keyType);
		write(", ");
		writeDictionaryDestroy(keyType);
		write(", ");
		writeDictionaryDestroy(valueType);
	}
	else {
		writeChar('(');
		writeHashEqual(keyType);
	}
	writeChar(')');
}

void GenC::writeNewTree(const FuType * keyType, const FuType * valueType)
{
	if (keyType->id == FuId::stringPtrType && !hasDictionaryDestroy(valueType))
		write("g_tree_new((GCompareFunc) strcmp");
	else {
		write("g_tree_new_full(FuTree_Compare");
		if (dynamic_cast<const FuIntegerType *>(keyType)) {
			this->treeCompareInteger = true;
			write("Integer");
		}
		else if (dynamic_cast<const FuStringType *>(keyType)) {
			this->treeCompareString = true;
			write("String");
		}
		else
			notSupported(keyType, keyType->toString());
		write(", NULL, ");
		writeDictionaryDestroy(keyType);
		write(", ");
		writeDictionaryDestroy(valueType);
	}
	writeChar(')');
}

void GenC::writeNew(const FuReadWriteClassType * klass, FuPriority parent)
{
	switch (klass->class_->id) {
	case FuId::listClass:
	case FuId::stackClass:
		write("g_array_new(FALSE, FALSE, sizeof(");
		writeType(klass->getElementType().get(), false);
		write("))");
		break;
	case FuId::queueClass:
		write("G_QUEUE_INIT");
		break;
	case FuId::hashSetClass:
		writeNewHashTable(klass->getElementType().get(), nullptr);
		break;
	case FuId::sortedSetClass:
		writeNewTree(klass->getElementType().get(), nullptr);
		break;
	case FuId::dictionaryClass:
		writeNewHashTable(klass->getKeyType(), klass->getValueType().get());
		break;
	case FuId::sortedDictionaryClass:
		writeNewTree(klass->getKeyType(), klass->getValueType().get());
		break;
	case FuId::stringWriterClass:
		write("g_string_new(NULL)");
		break;
	default:
		this->sharedMake = true;
		if (parent > FuPriority::mul)
			writeChar('(');
		writeStaticCastType(klass);
		write("FuShared_Make(1, sizeof(");
		writeName(klass->class_);
		write("), ");
		writeXstructorPtrs(klass->class_);
		writeChar(')');
		if (parent > FuPriority::mul)
			writeChar(')');
		break;
	}
}

bool GenC::isCollection(const FuClass * klass)
{
	switch (klass->id) {
	case FuId::listClass:
	case FuId::queueClass:
	case FuId::stackClass:
	case FuId::hashSetClass:
	case FuId::sortedSetClass:
	case FuId::dictionaryClass:
	case FuId::sortedDictionaryClass:
	case FuId::stringWriterClass:
		return true;
	default:
		return false;
	}
}

void GenC::writeStorageInit(const FuNamedValue * def)
{
	if (isCollection(def->type->asClassType()->class_))
		GenBase::writeStorageInit(def);
}

void GenC::writeVarInit(const FuNamedValue * def)
{
	if (def->value == nullptr && isHeapAllocated(def->type.get()))
		write(" = NULL");
	else
		GenBase::writeVarInit(def);
}

void GenC::writeAssignTemporary(const FuType * type, const FuExpr * expr)
{
	write(" = ");
	if (expr != nullptr)
		writeCoerced(type, expr, FuPriority::argument);
	else
		writeNewStorage(type);
}

int GenC::writeCTemporary(const FuType * type, const FuExpr * expr)
{
	ensureChildBlock();
	const FuStorageType * storage;
	bool assign = expr != nullptr || ((storage = dynamic_cast<const FuStorageType *>(type)) && isCollection(storage->class_));
	int id = [](const std::vector<const FuExpr *> &v, const FuExpr * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->currentTemporaries, type);
	if (id < 0) {
		id = std::ssize(this->currentTemporaries);
		startDefinition(type, false, true);
		writeTemporaryName(id);
		endDefinition(type);
		if (assign)
			writeAssignTemporary(type, expr);
		writeCharLine(';');
		this->currentTemporaries.push_back(expr);
	}
	else if (assign) {
		writeTemporaryName(id);
		writeAssignTemporary(type, expr);
		writeCharLine(';');
		this->currentTemporaries[id] = expr;
	}
	return id;
}

bool GenC::needsOwningTemporary(const FuExpr * expr)
{
	return expr->isNewString(false) || (dynamic_cast<const FuCallExpr *>(expr) && dynamic_cast<const FuOwningType *>(expr->type.get()));
}

void GenC::writeOwningTemporary(const FuExpr * expr)
{
	if (needsOwningTemporary(expr))
		writeCTemporary(expr->type.get(), expr);
}

void GenC::writeTemporariesNotSubstring(const FuExpr * expr)
{
	writeTemporaries(expr);
	if (isStringSubstring(expr) == nullptr)
		writeOwningTemporary(expr);
}

void GenC::writeArgTemporary(const FuMethod * method, const FuVar * param, const FuExpr * arg)
{
	if (method->id != FuId::consoleWrite && method->id != FuId::consoleWriteLine && param->type->id != FuId::typeParam0NotFinal && !dynamic_cast<const FuOwningType *>(param->type.get()))
		writeOwningTemporary(arg);
}

bool GenC::hasTemporariesToDestruct() const
{
	return std::any_of(this->currentTemporaries.begin(), this->currentTemporaries.end(), [](const FuExpr * temp) { return !dynamic_cast<const FuType *>(temp); });
}

void GenC::cleanupTemporary(int i, const FuExpr * temp)
{
	const FuPrefixExpr * dynamicObjectLiteral;
	if (!needToDestructType(temp->type.get()) || ((dynamicObjectLiteral = dynamic_cast<const FuPrefixExpr *>(temp)) && dynamic_cast<const FuAggregateInitializer *>(dynamicObjectLiteral->inner.get())))
		return;
	startDestructCall(temp->type->asClassType());
	writeTemporaryName(i);
	writeLine(");");
}

void GenC::writeVar(const FuNamedValue * def)
{
	GenBase::writeVar(def);
	if (needToDestruct(def)) {
		const FuVar * local = static_cast<const FuVar *>(def);
		this->varsToDestruct.push_back(local);
	}
}

void GenC::writeGPointerCast(const FuType * type, const FuExpr * expr)
{
	if (dynamic_cast<const FuNumericType *>(type) || dynamic_cast<const FuEnum *>(type))
		writeCall("GINT_TO_POINTER", expr);
	else if (type->id == FuId::stringPtrType && expr->type->id == FuId::stringPtrType) {
		write("(gpointer) ");
		expr->accept(this, FuPriority::primary);
	}
	else
		writeCoerced(type, expr, FuPriority::argument);
}

void GenC::writeAddressOf(const FuExpr * expr)
{
	writeChar('&');
	expr->accept(this, FuPriority::primary);
}

void GenC::writeGConstPointerCast(const FuExpr * expr)
{
	if (dynamic_cast<const FuStorageType *>(expr->type.get()))
		writeAddressOf(expr);
	else if (dynamic_cast<const FuClassType *>(expr->type.get()))
		expr->accept(this, FuPriority::argument);
	else {
		write("(gconstpointer) ");
		expr->accept(this, FuPriority::primary);
	}
}

void GenC::writeGPointerToInt(const FuType * type)
{
	write(type->id == FuId::nIntType ? "GPOINTER_TO_SIZE(" : "GPOINTER_TO_INT(");
}

void GenC::writeUnstorage(const FuExpr * obj)
{
	if (dynamic_cast<const FuStorageType *>(obj->type.get()))
		writeAddressOf(obj);
	else
		obj->accept(this, FuPriority::argument);
}

void GenC::writeQueueGet(std::string_view function, const FuExpr * obj, FuPriority parent)
{
	const FuType * elementType = obj->type->asClassType()->getElementType().get();
	bool parenthesis;
	if (parent == FuPriority::statement)
		parenthesis = false;
	else if (dynamic_cast<const FuIntegerType *>(elementType) && elementType->id != FuId::longType) {
		writeGPointerToInt(elementType);
		parenthesis = true;
	}
	else {
		parenthesis = parent > FuPriority::mul;
		if (parenthesis)
			writeChar('(');
		writeStaticCastType(elementType);
	}
	write(function);
	writeChar('(');
	writeUnstorage(obj);
	writeChar(')');
	if (parenthesis)
		writeChar(')');
}

void GenC::startDictionaryInsert(const FuExpr * dict, const FuExpr * key)
{
	const FuClassType * type = static_cast<const FuClassType *>(dict->type.get());
	write(type->class_->id == FuId::sortedDictionaryClass ? "g_tree_insert(" : "g_hash_table_insert(");
	dict->accept(this, FuPriority::argument);
	write(", ");
	writeGPointerCast(type->getKeyType(), key);
	write(", ");
}

void GenC::writeAssign(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuBinaryExpr * indexing;
	const FuClassType * dict;
	if ((indexing = dynamic_cast<const FuBinaryExpr *>(expr->left.get())) && indexing->op == FuToken::leftBracket && (dict = dynamic_cast<const FuClassType *>(indexing->left->type.get())) && dict->class_->typeParameterCount == 2) {
		startDictionaryInsert(indexing->left.get(), indexing->right.get());
		writeGPointerCast(dict->getValueType().get(), expr->right.get());
		writeChar(')');
	}
	else if (expr->left->type->id == FuId::stringStorageType) {
		const FuExpr * length = isTrimSubstring(expr);
		if (length != nullptr && parent == FuPriority::statement) {
			writeIndexing(expr->left.get(), length);
			write(" = '\\0'");
		}
		else {
			this->stringAssign = true;
			write("FuString_Assign(&");
			expr->left->accept(this, FuPriority::primary);
			write(", ");
			writeStringStorageValue(expr->right.get());
			writeChar(')');
		}
	}
	else if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(expr->left->type.get())) {
		if (dynamic->class_->id == FuId::regexClass) {
			GenBase::writeAssign(expr, parent);
		}
		else if (isUnique(dynamic)) {
			ensureChildBlock();
			write("free(");
			expr->left->accept(this, FuPriority::argument);
			writeLine(");");
			GenBase::writeAssign(expr, parent);
		}
		else {
			this->sharedAssign = true;
			write("FuShared_Assign((void **) &");
			expr->left->accept(this, FuPriority::primary);
			write(", ");
			if (dynamic_cast<const FuSymbolReference *>(expr->right.get())) {
				this->sharedAddRef = true;
				writeCall("FuShared_AddRef", expr->right.get());
			}
			else
				expr->right->accept(this, FuPriority::argument);
			writeChar(')');
		}
	}
	else
		GenBase::writeAssign(expr, parent);
}

const FuMethod * GenC::getThrowingMethod(const FuExpr * expr)
{
	const FuBinaryExpr * binary;
	if ((binary = dynamic_cast<const FuBinaryExpr *>(expr)) && binary->op == FuToken::assign)
		return getThrowingMethod(binary->right.get());
	else if (const FuCallExpr *call = dynamic_cast<const FuCallExpr *>(expr)) {
		const FuMethod * method = static_cast<const FuMethod *>(call->method->symbol);
		return std::ssize(method->throws) > 0 ? method : nullptr;
	}
	else
		return nullptr;
}

bool GenC::hasListDestroy(const FuType * type)
{
	const FuStorageType * list;
	return (list = dynamic_cast<const FuStorageType *>(type)) && (list->class_->id == FuId::listClass || list->class_->id == FuId::stackClass) && needToDestructType(list->getElementType().get());
}

bool GenC::hasInitCode(const FuNamedValue * def) const
{
	if (def->isAssignableStorage())
		return false;
	const FuClassType * klass;
	const FuStorageType * storage;
	return (dynamic_cast<const FuField *>(def) && (def->value != nullptr || isHeapAllocated(def->type->getStorageType()) || ((klass = dynamic_cast<const FuClassType *>(def->type.get())) && (klass->class_->id == FuId::listClass || klass->class_->id == FuId::stackClass || klass->class_->id == FuId::dictionaryClass || klass->class_->id == FuId::sortedDictionaryClass)))) || (def->value != nullptr && getThrowingMethod(def->value.get()) != nullptr) || ((storage = dynamic_cast<const FuStorageType *>(def->type->getStorageType())) && (storage->class_->id == FuId::lockClass || needsConstructor(storage->class_))) || hasListDestroy(def->type.get()) || GenBase::hasInitCode(def);
}

FuPriority GenC::startForwardThrow(const FuMethod * throwingMethod)
{
	write("if (");
	switch (throwingMethod->type->id) {
	case FuId::floatType:
	case FuId::doubleType:
		includeMath();
		write("isnan(");
		return FuPriority::argument;
	case FuId::voidType:
		writeChar('!');
		return FuPriority::primary;
	default:
		return FuPriority::equality;
	}
}

void GenC::writeDestruct(const FuSymbol * symbol)
{
	if (!needToDestruct(symbol))
		return;
	ensureChildBlock();
	const FuType * type = symbol->type.get();
	int nesting = 0;
	while (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(type)) {
		includeStdDef();
		write("for (ptrdiff_t _i");
		visitLiteralLong(nesting);
		write(" = ");
		visitLiteralLong(array->length - 1);
		write("; _i");
		visitLiteralLong(nesting);
		write(" >= 0; _i");
		visitLiteralLong(nesting);
		writeLine("--)");
		this->indent++;
		nesting++;
		type = array->getElementType().get();
	}
	const FuClassType * klass = static_cast<const FuClassType *>(type);
	startDestructCall(klass);
	writeLocalName(symbol, FuPriority::primary);
	for (int i = 0; i < nesting; i++) {
		write("[_i");
		visitLiteralLong(i);
		writeChar(']');
	}
	if (klass->class_->id == FuId::queueClass && hasDictionaryDestroy(klass->getElementType().get())) {
		write(", ");
		writeDictionaryDestroy(klass->getElementType().get());
	}
	else if (klass->class_->id == FuId::stringWriterClass)
		write(", TRUE");
	writeLine(");");
	this->indent -= nesting;
}

void GenC::writeDestructAll(const FuVar * exceptVar)
{
	for (int i = std::ssize(this->varsToDestruct); --i >= 0;) {
		const FuVar * def = this->varsToDestruct[i];
		if (def != exceptVar)
			writeDestruct(def);
	}
}

void GenC::writeRangeThrowReturnValue(const FuRangeType * range)
{
	visitLiteralLong(range->min - 1);
}

void GenC::writeThrowReturnValue(const FuType * type, bool include)
{
	switch (type->id) {
	case FuId::voidType:
		write("false");
		break;
	case FuId::floatType:
	case FuId::doubleType:
		if (include)
			includeMath();
		write("NAN");
		break;
	default:
		if (const FuRangeType *range = dynamic_cast<const FuRangeType *>(type))
			writeRangeThrowReturnValue(range);
		else
			write("NULL");
		break;
	}
}

void GenC::writeThrow()
{
	writeDestructAll();
	write("return ");
	writeThrowReturnValue(this->currentMethod->type.get(), true);
	writeCharLine(';');
}

void GenC::endForwardThrow(const FuMethod * throwingMethod)
{
	switch (throwingMethod->type->id) {
	case FuId::floatType:
	case FuId::doubleType:
		writeChar(')');
		break;
	case FuId::voidType:
		break;
	default:
		write(" == ");
		if (const FuRangeType *range = dynamic_cast<const FuRangeType *>(throwingMethod->type.get()))
			writeRangeThrowReturnValue(range);
		else
			visitLiteralNull();
		break;
	}
	writeChar(')');
	if (std::ssize(this->varsToDestruct) > 0) {
		writeChar(' ');
		openBlock();
		writeThrow();
		closeBlock();
	}
	else {
		writeNewLine();
		this->indent++;
		writeThrow();
		this->indent--;
	}
}

void GenC::writeInitCode(const FuNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	const FuType * type = def->type.get();
	int nesting = 0;
	while (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(type)) {
		openLoop("size_t", nesting++, array->length);
		type = array->getElementType().get();
	}
	const FuStorageType * lok;
	if ((lok = dynamic_cast<const FuStorageType *>(type)) && lok->class_->id == FuId::lockClass) {
		write("mtx_init(&");
		writeArrayElement(def, nesting);
		writeLine(", mtx_plain | mtx_recursive);");
	}
	else {
		const FuStorageType * storage;
		if ((storage = dynamic_cast<const FuStorageType *>(type)) && needsConstructor(storage->class_)) {
			writeName(storage->class_);
			write("_Construct(&");
			writeArrayElement(def, nesting);
			writeLine(");");
		}
		else {
			if (dynamic_cast<const FuField *>(def)) {
				writeArrayElement(def, nesting);
				if (nesting > 0) {
					write(" = ");
					if (isHeapAllocated(type))
						write("NULL");
					else
						def->value->accept(this, FuPriority::argument);
				}
				else
					writeVarInit(def);
				writeCharLine(';');
			}
			if (def->value != nullptr) {
				const FuMethod * throwingMethod = getThrowingMethod(def->value.get());
				if (throwingMethod != nullptr) {
					startForwardThrow(throwingMethod);
					writeArrayElement(def, nesting);
					endForwardThrow(throwingMethod);
				}
			}
		}
	}
	if (hasListDestroy(type)) {
		write("g_array_set_clear_func(");
		writeArrayElement(def, nesting);
		write(", ");
		writeListFree(type->asClassType()->getElementType()->asClassType());
		writeLine(");");
	}
	while (--nesting >= 0)
		closeBlock();
	GenBase::writeInitCode(def);
}

void GenC::writeMemberAccess(const FuType * leftType, const FuSymbol * symbolClass)
{
	if (dynamic_cast<const FuStorageType *>(leftType))
		writeChar('.');
	else
		write("->");
	for (const FuSymbol * klass = leftType->asClassType()->class_; klass != symbolClass; klass = klass->parent)
		write("base.");
}

void GenC::writeMemberOp(const FuExpr * left, const FuSymbolReference * symbol)
{
	writeMemberAccess(left->type.get(), symbol->symbol->parent);
}

void GenC::writeArrayPtr(const FuExpr * expr, FuPriority parent)
{
	const FuClassType * list;
	if ((list = dynamic_cast<const FuClassType *>(expr->type.get())) && list->class_->id == FuId::listClass) {
		writeChar('(');
		writeType(list->getElementType().get(), false);
		write(" *) ");
		writePostfix(expr, "->data");
	}
	else
		expr->accept(this, parent);
}

void GenC::writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	const FuClassType * klass;
	if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(type)) {
		const FuPrefixExpr * prefix;
		if (isUnique(dynamic) && (prefix = dynamic_cast<const FuPrefixExpr *>(expr))) {
			assert(prefix->op == FuToken::new_);
			const FuDynamicPtrType * newClass = static_cast<const FuDynamicPtrType *>(prefix->type.get());
			writeDynamicArrayCast(newClass->getElementType().get());
			write("malloc(");
			prefix->inner->accept(this, FuPriority::mul);
			write(" * sizeof(");
			writeType(newClass->getElementType().get(), false);
			write("))");
		}
		else if (dynamic_cast<const FuSymbolReference *>(expr) && parent != FuPriority::equality) {
			if (dynamic->class_->id == FuId::arrayPtrClass)
				writeDynamicArrayCast(dynamic->getElementType().get());
			else {
				writeChar('(');
				writeName(dynamic->class_);
				write(" *) ");
			}
			this->sharedAddRef = true;
			writeCall("FuShared_AddRef", expr);
		}
		else if (dynamic->class_->id != FuId::arrayPtrClass)
			writeClassPtr(dynamic->class_, expr, parent);
		else
			GenTyped::writeCoercedInternal(type, expr, parent);
	}
	else if ((klass = dynamic_cast<const FuClassType *>(type)) && klass->class_->id != FuId::stringClass && klass->class_->id != FuId::arrayPtrClass && !dynamic_cast<const FuStorageType *>(klass)) {
		if (klass->class_->id == FuId::queueClass && dynamic_cast<const FuStorageType *>(expr->type.get()))
			writeAddressOf(expr);
		else
			writeClassPtr(klass->class_, expr, parent);
	}
	else {
		if (type->id == FuId::stringStorageType)
			writeStringStorageValue(expr);
		else if (expr->type->id == FuId::stringStorageType)
			expr->accept(this, parent);
		else
			GenTyped::writeCoercedInternal(type, expr, parent);
	}
}

void GenC::writeSubstringEqual(const FuCallExpr * call, std::string_view literal, FuPriority parent, bool not_)
{
	if (parent > FuPriority::equality)
		writeChar('(');
	include("string.h");
	write("memcmp(");
	writeStringPtrAdd(call, false);
	write(", ");
	visitLiteralString(literal);
	write(", ");
	visitLiteralLong(std::ssize(literal));
	writeChar(')');
	write(getEqOp(not_));
	writeChar('0');
	if (parent > FuPriority::equality)
		writeChar(')');
}

void GenC::writeEqualStringInternal(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	if (parent > FuPriority::equality)
		writeChar('(');
	include("string.h");
	writeCall("strcmp", left, right);
	write(getEqOp(not_));
	writeChar('0');
	if (parent > FuPriority::equality)
		writeChar(')');
}

void GenC::writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	if (dynamic_cast<const FuStringType *>(left->type.get()) && dynamic_cast<const FuStringType *>(right->type.get())) {
		const FuCallExpr * call = isStringSubstring(left);
		const FuLiteralString * literal;
		if (call != nullptr && (literal = dynamic_cast<const FuLiteralString *>(right))) {
			const FuExpr * lengthExpr = getStringSubstringLength(call);
			int rightLength = literal->getAsciiLength();
			if (rightLength >= 0) {
				std::string_view rightValue = literal->value;
				if (const FuLiteralLong *leftLength = dynamic_cast<const FuLiteralLong *>(lengthExpr)) {
					if (leftLength->value != rightLength)
						notYet(left, "String comparison with unmatched length");
					writeSubstringEqual(call, rightValue, parent, not_);
				}
				else if (not_) {
					if (parent > FuPriority::condOr)
						writeChar('(');
					lengthExpr->accept(this, FuPriority::equality);
					write(" != ");
					visitLiteralLong(rightLength);
					if (rightLength > 0) {
						write(" || ");
						writeSubstringEqual(call, rightValue, FuPriority::condOr, true);
					}
					if (parent > FuPriority::condOr)
						writeChar(')');
				}
				else {
					if (parent > FuPriority::condAnd || parent == FuPriority::condOr)
						writeChar('(');
					lengthExpr->accept(this, FuPriority::equality);
					write(" == ");
					visitLiteralLong(rightLength);
					if (rightLength > 0) {
						write(" && ");
						writeSubstringEqual(call, rightValue, FuPriority::condAnd, false);
					}
					if (parent > FuPriority::condAnd || parent == FuPriority::condOr)
						writeChar(')');
				}
				return;
			}
		}
		writeEqualStringInternal(left, right, parent, not_);
	}
	else
		GenCCppD::writeEqual(left, right, parent, not_);
}

void GenC::writeStringLength(const FuExpr * expr)
{
	includeStdDef();
	include("string.h");
	writeCall("(ptrdiff_t) strlen", expr);
}

void GenC::writeStringMethod(std::string_view name, const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	include("string.h");
	write("FuString_");
	writeCall(name, obj, (*args)[0].get());
}

void GenC::writeSizeofCompare(const FuType * elementType)
{
	write(", sizeof(");
	FuId typeId = elementType->id;
	writeNumericType(typeId);
	write("), FuCompare_");
	writeNumericType(typeId);
	writeChar(')');
	this->compares.insert(typeId);
}

void GenC::writeArrayFill(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	write("for (size_t _i = 0; _i < ");
	if (std::ssize(*args) == 1)
		writeArrayStorageLength(obj);
	else
		(*args)[2]->accept(this, FuPriority::rel);
	writeLine("; _i++)");
	writeChar('\t');
	obj->accept(this, FuPriority::primary);
	writeChar('[');
	if (std::ssize(*args) > 1)
		startAdd((*args)[1].get());
	write("_i] = ");
	(*args)[0]->accept(this, FuPriority::argument);
}

void GenC::writeListAddInsert(const FuExpr * obj, bool insert, std::string_view function, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	const FuType * elementType = obj->type->asClassType()->getElementType().get();
	int id = writeCTemporary(elementType, elementType->isFinal() ? nullptr : args->back().get());
	const FuStorageType * storage;
	if ((storage = dynamic_cast<const FuStorageType *>(elementType)) && needsConstructor(storage->class_)) {
		writeName(storage->class_);
		write("_Construct(&futemp");
		visitLiteralLong(id);
		writeLine(");");
	}
	write(function);
	writeChar('(');
	obj->accept(this, FuPriority::argument);
	if (insert) {
		write(", ");
		(*args)[0]->accept(this, FuPriority::argument);
	}
	write(", futemp");
	visitLiteralLong(id);
	writeChar(')');
	this->currentTemporaries[id] = elementType;
}

void GenC::writeDictionaryLookup(const FuExpr * obj, std::string_view function, const FuExpr * key)
{
	write(function);
	writeChar('(');
	obj->accept(this, FuPriority::argument);
	write(", ");
	writeGConstPointerCast(key);
	writeChar(')');
}

void GenC::writeArgsAndRightParenthesis(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	int i = 0;
	for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
		if (i > 0 || method->callType != FuCallType::static_)
			write(", ");
		if (i >= std::ssize(*args))
			param->value->accept(this, FuPriority::argument);
		else
			writeCoerced(param->type.get(), (*args)[i].get(), FuPriority::argument);
		i++;
	}
	writeChar(')');
}

void GenC::writeCRegexOptions(const std::vector<std::shared_ptr<FuExpr>> * args)
{
	if (!writeRegexOptions(args, "", " | ", "", "G_REGEX_CASELESS", "G_REGEX_MULTILINE", "G_REGEX_DOTALL"))
		writeChar('0');
}

void GenC::writePrintfNotInterpolated(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine)
{
	write("\"%");
	if (const FuIntegerType *intType = dynamic_cast<const FuIntegerType *>((*args)[0]->type.get())) {
		if (intType->id == FuId::longType)
			writePrintfLongPrefix();
		writeChar('d');
	}
	else if (dynamic_cast<const FuFloatingType *>((*args)[0]->type.get()))
		writeChar('g');
	else
		writeChar('s');
	if (newLine)
		write("\\n");
	write("\", ");
	writeInterpolatedStringArgBase((*args)[0].get());
	writeChar(')');
}

void GenC::writeTextWriterWrite(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine)
{
	if (std::ssize(*args) == 0) {
		if (obj->type->asClassType()->class_->id == FuId::stringWriterClass) {
			write("g_string_append_c(");
			obj->accept(this, FuPriority::argument);
			write(", '\\n'");
		}
		else {
			write("putc('\\n', ");
			obj->accept(this, FuPriority::argument);
		}
		writeChar(')');
	}
	else if (const FuInterpolatedString *interpolated = dynamic_cast<const FuInterpolatedString *>((*args)[0].get())) {
		write(obj->type->asClassType()->class_->id == FuId::stringWriterClass ? "g_string_append_printf(" : "fprintf(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		writePrintf(interpolated, newLine);
	}
	else if (dynamic_cast<const FuNumericType *>((*args)[0]->type.get())) {
		write(obj->type->asClassType()->class_->id == FuId::stringWriterClass ? "g_string_append_printf(" : "fprintf(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		writePrintfNotInterpolated(args, newLine);
	}
	else if (!newLine) {
		if (obj->type->asClassType()->class_->id == FuId::stringWriterClass)
			writeCall("g_string_append", obj, (*args)[0].get());
		else
			writeCall("fputs", (*args)[0].get(), obj);
	}
	else if (const FuLiteralString *literal = dynamic_cast<const FuLiteralString *>((*args)[0].get())) {
		if (obj->type->asClassType()->class_->id == FuId::stringWriterClass) {
			write("g_string_append(");
			obj->accept(this, FuPriority::argument);
			write(", ");
			writeStringLiteralWithNewLine(literal->value);
		}
		else {
			write("fputs(");
			writeStringLiteralWithNewLine(literal->value);
			write(", ");
			obj->accept(this, FuPriority::argument);
		}
		writeChar(')');
	}
	else {
		write(obj->type->asClassType()->class_->id == FuId::stringWriterClass ? "g_string_append_printf(" : "fprintf(");
		obj->accept(this, FuPriority::argument);
		write(", \"%s\\n\", ");
		(*args)[0]->accept(this, FuPriority::argument);
		writeChar(')');
	}
}

void GenC::writeConsoleWrite(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine)
{
	include("stdio.h");
	if (std::ssize(*args) == 0)
		write("putchar('\\n')");
	else if (const FuInterpolatedString *interpolated = dynamic_cast<const FuInterpolatedString *>((*args)[0].get())) {
		write("printf(");
		writePrintf(interpolated, newLine);
	}
	else if (dynamic_cast<const FuNumericType *>((*args)[0]->type.get())) {
		write("printf(");
		writePrintfNotInterpolated(args, newLine);
	}
	else if (!newLine) {
		write("fputs(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", stdout)");
	}
	else
		writeCall("puts", (*args)[0].get());
}

const FuClass * GenC::getVtblStructClass(const FuClass * klass)
{
	while (!klass->addsVirtualMethods()) {
		const FuClass * baseClass = static_cast<const FuClass *>(klass->parent);
		klass = baseClass;
	}
	return klass;
}

const FuClass * GenC::getVtblPtrClass(const FuClass * klass)
{
	for (const FuClass * result = nullptr;;) {
		if (klass->addsVirtualMethods())
			result = klass;
		const FuClass * baseClass;
		if (!(baseClass = dynamic_cast<const FuClass *>(klass->parent)))
			return result;
		klass = baseClass;
	}
}

void GenC::writeCCall(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	const FuClass * klass = this->currentClass;
	const FuClass * declaringClass = static_cast<const FuClass *>(method->parent);
	if (isReferenceTo(obj, FuId::basePtr)) {
		writeName(method);
		write("(&self->base");
		writeUpcast(declaringClass, klass->parent);
	}
	else {
		if (method->isAbstractVirtualOrOverride()) {
			const FuClass * definingClass = declaringClass;
			if (method->callType == FuCallType::override_) {
				const FuClass * declaringClass1 = static_cast<const FuClass *>(method->getDeclaringMethod()->parent);
				declaringClass = declaringClass1;
			}
			if (obj != nullptr)
				klass = obj->type->asClassType()->class_;
			const FuClass * ptrClass = getVtblPtrClass(klass);
			const FuClass * structClass = getVtblStructClass(definingClass);
			if (structClass != ptrClass) {
				write("((const ");
				writeName(structClass);
				write("Vtbl *) ");
			}
			if (obj != nullptr) {
				obj->accept(this, FuPriority::primary);
				writeMemberAccess(obj->type.get(), ptrClass);
			}
			else
				writeSelfForField(ptrClass);
			write("vtbl");
			if (structClass != ptrClass)
				writeChar(')');
			write("->");
			writeCamelCase(method->name);
		}
		else
			writeName(method);
		writeChar('(');
		if (method->callType != FuCallType::static_) {
			if (obj != nullptr)
				writeClassPtr(declaringClass, obj, FuPriority::argument);
			else if (klass == declaringClass)
				write("self");
			else {
				write("&self->base");
				writeUpcast(declaringClass, klass->parent);
			}
		}
	}
	writeArgsAndRightParenthesis(method, args);
}

void GenC::writeTryParse(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	includeStdBool();
	write("_TryParse(&");
	obj->accept(this, FuPriority::primary);
	write(", ");
	(*args)[0]->accept(this, FuPriority::argument);
	if (dynamic_cast<const FuIntegerType *>(obj->type.get()))
		writeTryParseRadix(args);
	writeChar(')');
}

void GenC::writeStringSubstringStart(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	assert(std::ssize(*args) == 1);
	if (parent > FuPriority::add)
		writeChar('(');
	writeAdd(obj, (*args)[0].get());
	if (parent > FuPriority::add)
		writeChar(')');
}

void GenC::startArrayContains(const FuExpr * obj)
{
	write("FuArray_Contains_");
	FuId typeId = obj->type->asClassType()->getElementType()->id;
	switch (typeId) {
	case FuId::none:
		write("object((const void * const *) ");
		break;
	case FuId::stringStorageType:
	case FuId::stringPtrType:
		typeId = FuId::stringPtrType;
		include("string.h");
		write("string((const char * const *) ");
		break;
	default:
		writeNumericType(typeId);
		write("((const ");
		writeNumericType(typeId);
		write(" *) ");
		break;
	}
	this->contains.insert(typeId);
}

void GenC::startArrayIndexing(const FuExpr * obj, const FuType * elementType)
{
	write("g_array_index(");
	obj->accept(this, FuPriority::argument);
	write(", ");
	writeType(elementType, false);
	write(", ");
}

void GenC::writeMathFloating(std::string_view function, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	includeMath();
	writeLowercase(function);
	if (!std::any_of(args->begin(), args->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::doubleType; }))
		writeChar('f');
	writeInParentheses(args);
}

bool GenC::writeMathClampMaxMin(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	if (std::any_of(args->begin(), args->end(), [](const std::shared_ptr<FuExpr> &arg) { return dynamic_cast<const FuFloatingType *>(arg->type.get()); }))
		return true;
	if (std::any_of(args->begin(), args->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::longType; })) {
		this->longFunctions.insert(method->id);
		write("FuLong_");
	}
	else {
		this->intFunctions.insert(method->id);
		write("FuInt_");
	}
	write(method->name);
	writeInParentheses(args);
	return false;
}

void GenC::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::none:
	case FuId::classToString:
		writeCCall(obj, method, args);
		break;
	case FuId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case FuId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case FuId::intTryParse:
		this->intTryParse = true;
		write("FuInt");
		writeTryParse(obj, args);
		break;
	case FuId::longTryParse:
		this->longTryParse = true;
		write("FuLong");
		writeTryParse(obj, args);
		break;
	case FuId::doubleTryParse:
		this->doubleTryParse = true;
		write("FuDouble");
		writeTryParse(obj, args);
		break;
	case FuId::stringContains:
		include("string.h");
		if (parent > FuPriority::equality)
			writeChar('(');
		{
			int c = getOneAscii((*args)[0].get());
			if (c >= 0) {
				write("strchr(");
				obj->accept(this, FuPriority::argument);
				write(", ");
				visitLiteralChar(c);
				writeChar(')');
			}
			else
				writeCall("strstr", obj, (*args)[0].get());
			write(" != NULL");
			if (parent > FuPriority::equality)
				writeChar(')');
			break;
		}
	case FuId::stringEndsWith:
		this->stringEndsWith = true;
		writeStringMethod("EndsWith", obj, args);
		break;
	case FuId::stringIndexOf:
		this->stringIndexOf = true;
		includeStdDef();
		writeStringMethod("IndexOf", obj, args);
		break;
	case FuId::stringLastIndexOf:
		this->stringLastIndexOf = true;
		includeStdDef();
		writeStringMethod("LastIndexOf", obj, args);
		break;
	case FuId::stringReplace:
		include("string.h");
		this->stringAppend = true;
		this->stringReplace = true;
		writeCall("FuString_Replace", obj, (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::stringStartsWith:
		if (parent > FuPriority::equality)
			writeChar('(');
		{
			int c2 = getOneAscii((*args)[0].get());
			if (c2 >= 0) {
				writePostfix(obj, "[0] == ");
				visitLiteralChar(c2);
			}
			else {
				include("string.h");
				write("strncmp(");
				obj->accept(this, FuPriority::argument);
				write(", ");
				(*args)[0]->accept(this, FuPriority::argument);
				write(", strlen(");
				(*args)[0]->accept(this, FuPriority::argument);
				write(")) == 0");
			}
			if (parent > FuPriority::equality)
				writeChar(')');
			break;
		}
	case FuId::stringSubstring:
		if (std::ssize(*args) == 1)
			writeStringSubstringStart(obj, args, parent);
		else {
			include("string.h");
			this->stringSubstring = true;
			write("FuString_Substring(");
			writeArrayPtrAdd(obj, (*args)[0].get());
			write(", ");
			(*args)[1]->accept(this, FuPriority::argument);
			writeChar(')');
		}
		break;
	case FuId::stringToLower:
		writeGlib("g_utf8_strdown(");
		obj->accept(this, FuPriority::argument);
		write(", -1)");
		break;
	case FuId::stringToUpper:
		writeGlib("g_utf8_strup(");
		obj->accept(this, FuPriority::argument);
		write(", -1)");
		break;
	case FuId::arrayBinarySearchAll:
	case FuId::arrayBinarySearchPart:
		if (parent > FuPriority::add)
			writeChar('(');
		write("(const ");
		{
			const FuType * elementType = obj->type->asClassType()->getElementType().get();
			writeType(elementType, false);
			write(" *) bsearch(&");
			(*args)[0]->accept(this, FuPriority::primary);
			write(", ");
			if (std::ssize(*args) == 1)
				writeArrayPtr(obj, FuPriority::argument);
			else
				writeArrayPtrAdd(obj, (*args)[1].get());
			write(", ");
			if (std::ssize(*args) == 1)
				writeArrayStorageLength(obj);
			else
				(*args)[2]->accept(this, FuPriority::argument);
			writeSizeofCompare(elementType);
			write(" - ");
			writeArrayPtr(obj, FuPriority::mul);
			if (parent > FuPriority::add)
				writeChar(')');
			break;
		}
	case FuId::arrayContains:
		startArrayContains(obj);
		obj->accept(this, FuPriority::argument);
		write(", ");
		writeArrayStorageLength(obj);
		write(", ");
		writeUnstorage((*args)[0].get());
		writeChar(')');
		break;
	case FuId::arrayCopyTo:
	case FuId::listCopyTo:
		include("string.h");
		{
			const FuType * elementType2 = obj->type->asClassType()->getElementType().get();
			if (isHeapAllocated(elementType2))
				notYet(obj, "CopyTo for this type");
			write("memcpy(");
			writeArrayPtrAdd((*args)[1].get(), (*args)[2].get());
			write(", ");
			writeArrayPtrAdd(obj, (*args)[0].get());
			write(", ");
			if (elementType2->id == FuId::sByteRange || elementType2->id == FuId::byteRange)
				(*args)[3]->accept(this, FuPriority::argument);
			else {
				(*args)[3]->accept(this, FuPriority::mul);
				write(" * sizeof(");
				writeType(elementType2, false);
				writeChar(')');
			}
			writeChar(')');
			break;
		}
	case FuId::arrayFillAll:
	case FuId::arrayFillPart:
		{
			const FuLiteral * literal;
			if ((literal = dynamic_cast<const FuLiteral *>((*args)[0].get())) && literal->isDefaultValue()) {
				include("string.h");
				write("memset(");
				if (std::ssize(*args) == 1) {
					obj->accept(this, FuPriority::argument);
					write(", 0, sizeof(");
					obj->accept(this, FuPriority::argument);
					writeChar(')');
				}
				else {
					writeArrayPtrAdd(obj, (*args)[1].get());
					write(", 0, ");
					(*args)[2]->accept(this, FuPriority::mul);
					write(" * sizeof(");
					writeType(obj->type->asClassType()->getElementType().get(), false);
					writeChar(')');
				}
				writeChar(')');
			}
			else
				writeArrayFill(obj, args);
			break;
		}
	case FuId::arraySortAll:
		write("qsort(");
		writeArrayPtr(obj, FuPriority::argument);
		write(", ");
		writeArrayStorageLength(obj);
		writeSizeofCompare(obj->type->asClassType()->getElementType().get());
		break;
	case FuId::arraySortPart:
	case FuId::listSortPart:
		write("qsort(");
		writeArrayPtrAdd(obj, (*args)[0].get());
		write(", ");
		(*args)[1]->accept(this, FuPriority::argument);
		writeSizeofCompare(obj->type->asClassType()->getElementType().get());
		break;
	case FuId::listAdd:
	case FuId::stackPush:
		{
			const FuStorageType * storage;
			if ((storage = dynamic_cast<const FuStorageType *>(obj->type->asClassType()->getElementType().get())) && (storage->class_->id == FuId::arrayStorageClass || (storage->class_->id == FuId::none && !needsConstructor(storage->class_)))) {
				write("g_array_set_size(");
				obj->accept(this, FuPriority::argument);
				write(", ");
				writePostfix(obj, "->len + 1)");
			}
			else
				writeListAddInsert(obj, false, "g_array_append_val", args);
			break;
		}
	case FuId::listClear:
	case FuId::stackClear:
		write("g_array_set_size(");
		obj->accept(this, FuPriority::argument);
		write(", 0)");
		break;
	case FuId::listContains:
		startArrayContains(obj);
		writePostfix(obj, "->data, ");
		writePostfix(obj, "->len, ");
		writeUnstorage((*args)[0].get());
		writeChar(')');
		break;
	case FuId::listInsert:
		writeListAddInsert(obj, true, "g_array_insert_val", args);
		break;
	case FuId::listLast:
	case FuId::stackPeek:
		startArrayIndexing(obj, obj->type->asClassType()->getElementType().get());
		writePostfix(obj, "->len - 1)");
		break;
	case FuId::listRemoveAt:
		writeCall("g_array_remove_index", obj, (*args)[0].get());
		break;
	case FuId::listRemoveRange:
		writeCall("g_array_remove_range", obj, (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::listSortAll:
		write("g_array_sort(");
		obj->accept(this, FuPriority::argument);
		{
			FuId typeId2 = obj->type->asClassType()->getElementType()->id;
			write(", FuCompare_");
			writeNumericType(typeId2);
			writeChar(')');
			this->compares.insert(typeId2);
			break;
		}
	case FuId::queueClear:
		{
			const FuType * elementType3 = obj->type->asClassType()->getElementType().get();
			if (hasDictionaryDestroy(elementType3)) {
				write("g_queue_clear_full(");
				writeUnstorage(obj);
				write(", ");
				writeDictionaryDestroy(elementType3);
			}
			else {
				write("g_queue_clear(");
				writeUnstorage(obj);
			}
			writeChar(')');
			break;
		}
	case FuId::queueDequeue:
		writeQueueGet("g_queue_pop_head", obj, parent);
		break;
	case FuId::queueEnqueue:
		write("g_queue_push_tail(");
		writeUnstorage(obj);
		write(", ");
		writeGPointerCast(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		writeChar(')');
		break;
	case FuId::queuePeek:
		writeQueueGet("g_queue_peek_head", obj, parent);
		break;
	case FuId::stackPop:
		if (parent == FuPriority::statement)
			writePostfix(obj, "->len--");
		else {
			startArrayIndexing(obj, obj->type->asClassType()->getElementType().get());
			write("--");
			writePostfix(obj, "->len)");
		}
		break;
	case FuId::hashSetAdd:
		write("g_hash_table_add(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		writeGPointerCast(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		writeChar(')');
		break;
	case FuId::hashSetClear:
	case FuId::dictionaryClear:
		writeCall("g_hash_table_remove_all", obj);
		break;
	case FuId::hashSetContains:
	case FuId::dictionaryContainsKey:
		writeDictionaryLookup(obj, "g_hash_table_contains", (*args)[0].get());
		break;
	case FuId::hashSetRemove:
	case FuId::dictionaryRemove:
		writeDictionaryLookup(obj, "g_hash_table_remove", (*args)[0].get());
		break;
	case FuId::sortedSetAdd:
		write("g_tree_insert(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		writeGPointerCast(obj->type->asClassType()->getKeyType(), (*args)[0].get());
		write(", NULL)");
		break;
	case FuId::dictionaryAdd:
		startDictionaryInsert(obj, (*args)[0].get());
		{
			const FuClassType * valueType = obj->type->asClassType()->getValueType()->asClassType();
			switch (valueType->class_->id) {
			case FuId::listClass:
			case FuId::stackClass:
			case FuId::hashSetClass:
			case FuId::sortedSetClass:
			case FuId::dictionaryClass:
			case FuId::sortedDictionaryClass:
			case FuId::stringWriterClass:
				writeNewStorage(valueType);
				break;
			default:
				if (valueType->class_->isPublic && valueType->class_->constructor != nullptr && valueType->class_->constructor->visibility == FuVisibility::public_) {
					writeName(valueType->class_);
					write("_New()");
				}
				else {
					write("malloc(sizeof(");
					writeType(valueType, false);
					write("))");
				}
				break;
			}
			writeChar(')');
			break;
		}
	case FuId::sortedSetClear:
	case FuId::sortedDictionaryClear:
		write("g_tree_destroy(g_tree_ref(");
		obj->accept(this, FuPriority::argument);
		write("))");
		break;
	case FuId::sortedSetContains:
	case FuId::sortedDictionaryContainsKey:
		write("g_tree_lookup_extended(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		writeGConstPointerCast((*args)[0].get());
		write(", NULL, NULL)");
		break;
	case FuId::sortedSetRemove:
	case FuId::sortedDictionaryRemove:
		writeDictionaryLookup(obj, "g_tree_remove", (*args)[0].get());
		break;
	case FuId::textWriterWrite:
		writeTextWriterWrite(obj, args, false);
		break;
	case FuId::textWriterWriteChar:
		if (obj->type->asClassType()->class_->id == FuId::stringWriterClass)
			writeCall("g_string_append_c", obj, (*args)[0].get());
		else
			writeCall("putc", (*args)[0].get(), obj);
		break;
	case FuId::textWriterWriteCodePoint:
		if (obj->type->asClassType()->class_->id != FuId::stringWriterClass)
			notSupported(obj, method->name);
		else
			writeCall("g_string_append_unichar", obj, (*args)[0].get());
		break;
	case FuId::textWriterWriteLine:
		writeTextWriterWrite(obj, args, true);
		break;
	case FuId::consoleWrite:
		writeConsoleWrite(args, false);
		break;
	case FuId::consoleWriteLine:
		writeConsoleWrite(args, true);
		break;
	case FuId::stringWriterClear:
		write("g_string_truncate(");
		obj->accept(this, FuPriority::argument);
		write(", 0)");
		break;
	case FuId::stringWriterToString:
		writePostfix(obj, "->str");
		break;
	case FuId::convertToBase64String:
		writeGlib("g_base64_encode(");
		writeArrayPtrAdd((*args)[0].get(), (*args)[1].get());
		write(", ");
		(*args)[2]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::uTF8GetByteCount:
		writeStringLength((*args)[0].get());
		break;
	case FuId::uTF8GetBytes:
		include("string.h");
		write("memcpy(");
		writeArrayPtrAdd((*args)[1].get(), (*args)[2].get());
		write(", ");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", strlen(");
		(*args)[0]->accept(this, FuPriority::argument);
		write("))");
		break;
	case FuId::uTF8GetString:
		include("string.h");
		this->stringSubstring = true;
		write("FuString_Substring((const char *) ");
		writeArrayPtrAdd((*args)[0].get(), (*args)[1].get());
		write(", ");
		(*args)[2]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::environmentGetEnvironmentVariable:
		writeCall("getenv", (*args)[0].get());
		break;
	case FuId::regexCompile:
		writeGlib("g_regex_new(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		writeCRegexOptions(args);
		write(", 0, NULL)");
		break;
	case FuId::regexEscape:
		writeGlib("g_regex_escape_string(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", -1)");
		break;
	case FuId::regexIsMatchStr:
		writeGlib("g_regex_match_simple(");
		(*args)[1]->accept(this, FuPriority::argument);
		write(", ");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		writeCRegexOptions(args);
		write(", 0)");
		break;
	case FuId::regexIsMatchRegex:
		write("g_regex_match(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", 0, NULL)");
		break;
	case FuId::matchFindStr:
		this->matchFind = true;
		write("FuMatch_Find(&");
		obj->accept(this, FuPriority::primary);
		write(", ");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		(*args)[1]->accept(this, FuPriority::argument);
		write(", ");
		writeCRegexOptions(args);
		writeChar(')');
		break;
	case FuId::matchFindRegex:
		write("g_regex_match(");
		(*args)[1]->accept(this, FuPriority::argument);
		write(", ");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", 0, &");
		obj->accept(this, FuPriority::primary);
		writeChar(')');
		break;
	case FuId::matchGetCapture:
		writeCall("g_match_info_fetch", obj, (*args)[0].get());
		break;
	case FuId::mathMethod:
	case FuId::mathLog2:
		writeMathFloating(method->name, args);
		break;
	case FuId::mathAbs:
		switch ((*args)[0]->type->id) {
		case FuId::longType:
			writeCall("llabs", (*args)[0].get());
			break;
		case FuId::floatType:
			includeMath();
			writeCall("fabsf", (*args)[0].get());
			break;
		case FuId::floatIntType:
		case FuId::doubleType:
			includeMath();
			writeCall("fabs", (*args)[0].get());
			break;
		default:
			writeCall("abs", (*args)[0].get());
			break;
		}
		break;
	case FuId::mathCeiling:
		writeMathFloating("ceil", args);
		break;
	case FuId::mathClamp:
		if (writeMathClampMaxMin(method, args)) {
			includeMath();
			write(std::any_of(args->begin(), args->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::doubleType; }) ? "fmin(fmax(" : "fminf(fmaxf(");
			writeClampAsMinMax(args);
		}
		break;
	case FuId::mathFusedMultiplyAdd:
		writeMathFloating("fma", args);
		break;
	case FuId::mathIsFinite:
		includeMath();
		writeCall("isfinite", (*args)[0].get());
		break;
	case FuId::mathIsInfinity:
		includeMath();
		writeCall("isinf", (*args)[0].get());
		break;
	case FuId::mathIsNaN:
		includeMath();
		writeCall("isnan", (*args)[0].get());
		break;
	case FuId::mathMax:
	case FuId::mathMin:
		if (writeMathClampMaxMin(method, args)) {
			writeChar('f');
			writeMathFloating(method->name, args);
		}
		break;
	case FuId::mathRound:
		writeMathFloating("round", args);
		break;
	case FuId::mathTruncate:
		writeMathFloating("trunc", args);
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenC::visitCallExpr(const FuCallExpr * expr, FuPriority parent)
{
	if (!tryWriteTemporary(expr))
		GenBase::visitCallExpr(expr, parent);
}

void GenC::writeDictionaryIndexing(std::string_view function, const FuBinaryExpr * expr, FuPriority parent)
{
	const FuType * valueType = expr->left->type->asClassType()->getValueType().get();
	if (dynamic_cast<const FuIntegerType *>(valueType) && valueType->id != FuId::longType) {
		writeGPointerToInt(valueType);
		writeDictionaryLookup(expr->left.get(), function, expr->right.get());
		writeChar(')');
	}
	else {
		if (parent > FuPriority::mul)
			writeChar('(');
		const FuStorageType * storage;
		if ((storage = dynamic_cast<const FuStorageType *>(valueType)) && (storage->class_->id == FuId::none || storage->class_->id == FuId::arrayStorageClass))
			writeDynamicArrayCast(valueType);
		else {
			writeStaticCastType(valueType);
			if (dynamic_cast<const FuEnum *>(valueType)) {
				assert(parent <= FuPriority::mul && "Should close two parens");
				write("GPOINTER_TO_INT(");
			}
		}
		writeDictionaryLookup(expr->left.get(), function, expr->right.get());
		if (parent > FuPriority::mul || dynamic_cast<const FuEnum *>(valueType))
			writeChar(')');
	}
}

void GenC::writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	if (const FuClassType *klass = dynamic_cast<const FuClassType *>(expr->left->type.get())) {
		switch (klass->class_->id) {
		case FuId::arrayStorageClass:
			if (klass->id == FuId::mainArgsType) {
				writeArgsIndexing(expr->right.get());
				return;
			}
			break;
		case FuId::listClass:
			if (dynamic_cast<const FuArrayStorageType *>(klass->getElementType().get())) {
				writeChar('(');
				writeDynamicArrayCast(klass->getElementType().get());
				writePostfix(expr->left.get(), "->data)[");
				expr->right->accept(this, FuPriority::argument);
				writeChar(']');
			}
			else {
				startArrayIndexing(expr->left.get(), klass->getElementType().get());
				expr->right->accept(this, FuPriority::argument);
				writeChar(')');
			}
			return;
		case FuId::dictionaryClass:
			writeDictionaryIndexing("g_hash_table_lookup", expr, parent);
			return;
		case FuId::sortedDictionaryClass:
			writeDictionaryIndexing("g_tree_lookup", expr, parent);
			return;
		default:
			break;
		}
	}
	GenBase::writeIndexingExpr(expr, parent);
}

void GenC::visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::plus:
		if (expr->type->id == FuId::stringStorageType) {
			if (tryWriteTemporary(expr))
				return;
			this->stringFormat = true;
			include("stdarg.h");
			include("stdio.h");
			write("FuString_Format(\"%s%s\", ");
			expr->left->accept(this, FuPriority::argument);
			write(", ");
			expr->right->accept(this, FuPriority::argument);
			writeChar(')');
			return;
		}
		break;
	case FuToken::equal:
	case FuToken::notEqual:
	case FuToken::greater:
		{
			const FuExpr * str = isStringEmpty(expr);
			if (str != nullptr) {
				writePostfix(str, expr->op == FuToken::equal ? "[0] == '\\0'" : "[0] != '\\0'");
				return;
			}
			break;
		}
	case FuToken::addAssign:
		if (expr->left->type->id == FuId::stringStorageType) {
			if (const FuInterpolatedString *rightInterpolated = dynamic_cast<const FuInterpolatedString *>(expr->right.get())) {
				this->stringAssign = true;
				write("FuString_Assign(&");
				expr->left->accept(this, FuPriority::primary);
				this->stringFormat = true;
				include("stdarg.h");
				include("stdio.h");
				write(", FuString_Format(\"%s");
				writePrintfFormat(rightInterpolated);
				write("\", ");
				expr->left->accept(this, FuPriority::argument);
				writeInterpolatedStringArgs(rightInterpolated);
				writeChar(')');
			}
			else {
				include("string.h");
				this->stringAppend = true;
				write("FuString_Append(&");
				expr->left->accept(this, FuPriority::primary);
				write(", ");
				expr->right->accept(this, FuPriority::argument);
			}
			writeChar(')');
			return;
		}
		break;
	case FuToken::is:
		notSupported(expr, "'is' operator");
		break;
	default:
		break;
	}
	GenBase::visitBinaryExpr(expr, parent);
}

void GenC::writeResource(std::string_view name, int length)
{
	write("FuResource_");
	writeResourceName(name);
}

void GenC::visitLambdaExpr(const FuLambdaExpr * expr)
{
	notSupported(expr, "Lambda expression");
}

void GenC::writeDestructLoopOrSwitch(const FuCondCompletionStatement * loopOrSwitch)
{
	for (int i = std::ssize(this->varsToDestruct); --i >= 0;) {
		const FuVar * def = this->varsToDestruct[i];
		if (!loopOrSwitch->encloses(def))
			break;
		writeDestruct(def);
	}
}

void GenC::trimVarsToDestruct(int i)
{
	this->varsToDestruct.erase(this->varsToDestruct.begin() + i, this->varsToDestruct.begin() + i + (std::ssize(this->varsToDestruct) - i));
}

void GenC::cleanupBlock(const FuBlock * statement)
{
	int i = std::ssize(this->varsToDestruct);
	for (; i > 0; i--) {
		const FuVar * def = this->varsToDestruct[i - 1];
		if (def->parent != statement)
			break;
		if (statement->completesNormally())
			writeDestruct(def);
	}
	trimVarsToDestruct(i);
}

void GenC::visitBlock(const FuBlock * statement)
{
	bool wasConditionVarInScope = this->conditionVarInScope;
	GenBase::visitBlock(statement);
	this->conditionVarInScope = wasConditionVarInScope;
}

void GenC::visitBreak(const FuBreak * statement)
{
	writeDestructLoopOrSwitch(statement->loopOrSwitch);
	GenBase::visitBreak(statement);
}

void GenC::visitContinue(const FuContinue * statement)
{
	writeDestructLoopOrSwitch(statement->loop);
	GenBase::visitContinue(statement);
}

void GenC::visitExpr(const FuExpr * statement)
{
	const FuMethod * throwingMethod = getThrowingMethod(statement);
	if (throwingMethod != nullptr) {
		writeTemporaries(statement);
		ensureChildBlock();
		statement->accept(this, startForwardThrow(throwingMethod));
		endForwardThrow(throwingMethod);
		cleanupTemporaries();
	}
	else if (needsOwningTemporary(statement)) {
		const FuClassType * klass = static_cast<const FuClassType *>(statement->type.get());
		writeTemporaries(statement);
		writeDestructMethodName(klass);
		writeChar('(');
		statement->accept(this, FuPriority::argument);
		if (klass->class_->id == FuId::stringWriterClass)
			write(", TRUE");
		writeLine(");");
		cleanupTemporaries();
	}
	else
		GenBase::visitExpr(statement);
}

void GenC::startForeachHashTable(const FuForeach * statement)
{
	openBlock();
	writeLine("GHashTableIter fudictit;");
	write("g_hash_table_iter_init(&fudictit, ");
	statement->collection->accept(this, FuPriority::argument);
	writeLine(");");
}

void GenC::writeDictIterVar(const FuNamedValue * iter, std::string_view value)
{
	writeTypeAndName(iter);
	write(" = ");
	if (dynamic_cast<const FuIntegerType *>(iter->type.get()) && iter->type->id != FuId::longType) {
		writeGPointerToInt(iter->type.get());
		write(value);
		writeChar(')');
	}
	else {
		writeStaticCastType(iter->type.get());
		write(value);
	}
	writeCharLine(';');
}

void GenC::visitForeach(const FuForeach * statement)
{
	if (const FuClassType *klass = dynamic_cast<const FuClassType *>(statement->collection->type.get())) {
		std::string_view element = statement->getVar()->name;
		switch (klass->class_->id) {
		case FuId::stringClass:
			write("for (");
			writeStringPtrType();
			writeCamelCaseNotKeyword(element);
			write(" = ");
			statement->collection->accept(this, FuPriority::argument);
			write("; *");
			writeCamelCaseNotKeyword(element);
			write(" != '\\0'; ");
			writeCamelCaseNotKeyword(element);
			write("++)");
			writeChild(statement->body.get());
			break;
		case FuId::arrayStorageClass:
			write("for (int ");
			writeCamelCaseNotKeyword(element);
			if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(klass)) {
				write(" = 0; ");
				writeCamelCaseNotKeyword(element);
				write(" < ");
				visitLiteralLong(array->length);
			}
			else {
				write(" = 1; ");
				writeCamelCaseNotKeyword(element);
				write(" < argc");
			}
			write("; ");
			writeCamelCaseNotKeyword(element);
			write("++)");
			writeChild(statement->body.get());
			break;
		case FuId::listClass:
			write("for (");
			{
				const FuType * elementType = klass->getElementType().get();
				writeType(elementType, false);
				write(" const *");
				writeCamelCaseNotKeyword(element);
				write(" = (");
				writeType(elementType, false);
				write(" const *) ");
				writePostfix(statement->collection.get(), "->data, ");
				for (; elementType->isArray(); elementType = elementType->asClassType()->getElementType().get())
					writeChar('*');
				if (dynamic_cast<const FuClassType *>(elementType) && !dynamic_cast<const FuStorageType *>(elementType))
					write("* const ");
				write("*fuend = ");
				writeCamelCaseNotKeyword(element);
				write(" + ");
				writePostfix(statement->collection.get(), "->len; ");
				writeCamelCaseNotKeyword(element);
				write(" < fuend; ");
				writeCamelCaseNotKeyword(element);
				write("++)");
				writeChild(statement->body.get());
				break;
			}
		case FuId::hashSetClass:
			startForeachHashTable(statement);
			writeLine("gpointer fukey;");
			write("while (g_hash_table_iter_next(&fudictit, &fukey, NULL)) ");
			openBlock();
			writeDictIterVar(statement->getVar(), "fukey");
			flattenBlock(statement->body.get());
			closeBlock();
			closeBlock();
			break;
		case FuId::sortedSetClass:
			write("for (GTreeNode *fusetit = g_tree_node_first(");
			statement->collection->accept(this, FuPriority::argument);
			write("); fusetit != NULL; fusetit = g_tree_node_next(fusetit)) ");
			openBlock();
			writeDictIterVar(statement->getVar(), "g_tree_node_key(fusetit)");
			flattenBlock(statement->body.get());
			closeBlock();
			break;
		case FuId::dictionaryClass:
			startForeachHashTable(statement);
			writeLine("gpointer fukey, fuvalue;");
			write("while (g_hash_table_iter_next(&fudictit, &fukey, &fuvalue)) ");
			openBlock();
			writeDictIterVar(statement->getVar(), "fukey");
			writeDictIterVar(statement->getValueVar(), "fuvalue");
			flattenBlock(statement->body.get());
			closeBlock();
			closeBlock();
			break;
		case FuId::sortedDictionaryClass:
			write("for (GTreeNode *fudictit = g_tree_node_first(");
			statement->collection->accept(this, FuPriority::argument);
			write("); fudictit != NULL; fudictit = g_tree_node_next(fudictit)) ");
			openBlock();
			writeDictIterVar(statement->getVar(), "g_tree_node_key(fudictit)");
			writeDictIterVar(statement->getValueVar(), "g_tree_node_value(fudictit)");
			flattenBlock(statement->body.get());
			closeBlock();
			break;
		default:
			notSupported(statement->collection.get(), klass->class_->name);
			break;
		}
	}
	else
		notSupported(statement->collection.get(), statement->collection->type->toString());
}

void GenC::startIf(const FuExpr * expr)
{
	if (hasTemporariesToDestruct()) {
		if (!this->conditionVarInScope) {
			this->conditionVarInScope = true;
			write("bool ");
		}
		write("fucondition = ");
		expr->accept(this, FuPriority::argument);
		writeCharLine(';');
		cleanupTemporaries();
		write("if (fucondition)");
	}
	else
		GenBase::startIf(expr);
}

void GenC::visitLock(const FuLock * statement)
{
	write("mtx_lock(&");
	statement->lock->accept(this, FuPriority::primary);
	writeLine(");");
	statement->body->acceptStatement(this);
	write("mtx_unlock(&");
	statement->lock->accept(this, FuPriority::primary);
	writeLine(");");
}

void GenC::visitReturn(const FuReturn * statement)
{
	const FuSymbolReference * symbol;
	const FuVar * local;
	if (statement->value == nullptr || dynamic_cast<const FuLiteral *>(statement->value.get())) {
		writeDestructAll();
		if (statement->value == nullptr && std::ssize(this->currentMethod->throws) > 0)
			writeLine("return true;");
		else
			GenCCpp::visitReturn(statement);
	}
	else if ((symbol = dynamic_cast<const FuSymbolReference *>(statement->value.get())) && (local = dynamic_cast<const FuVar *>(symbol->symbol))) {
		if (std::find(this->varsToDestruct.begin(), this->varsToDestruct.end(), local) != this->varsToDestruct.end()) {
			writeDestructAll(local);
			write("return ");
			const FuClassType * resultPtr;
			if ((resultPtr = dynamic_cast<const FuClassType *>(this->currentMethod->type.get())) && !dynamic_cast<const FuStorageType *>(resultPtr))
				writeClassPtr(resultPtr->class_, symbol, FuPriority::argument);
			else
				symbol->accept(this, FuPriority::argument);
			writeCharLine(';');
		}
		else {
			writeDestructAll();
			GenCCpp::visitReturn(statement);
		}
	}
	else {
		writeTemporaries(statement->value.get());
		const FuMethod * throwingMethod = dynamic_cast<const FuNumericType *>(this->currentMethod->type.get()) ? getThrowingMethod(statement->value.get()) : nullptr;
		const FuRangeType * methodRange;
		const FuRangeType * throwingRange;
		if (throwingMethod != nullptr && ((methodRange = dynamic_cast<const FuRangeType *>(this->currentMethod->type.get())) ? (throwingRange = dynamic_cast<const FuRangeType *>(throwingMethod->type.get())) && methodRange->min == throwingRange->min : !!dynamic_cast<const FuFloatingType *>(throwingMethod->type.get())))
			throwingMethod = nullptr;
		if (throwingMethod == nullptr && std::ssize(this->varsToDestruct) == 0 && !hasTemporariesToDestruct()) {
			writeDestructAll();
			GenCCpp::visitReturn(statement);
		}
		else {
			ensureChildBlock();
			startDefinition(this->currentMethod->type.get(), true, true);
			write("returnValue = ");
			writeCoerced(this->currentMethod->type.get(), statement->value.get(), FuPriority::argument);
			writeCharLine(';');
			cleanupTemporaries();
			writeDestructAll();
			if (throwingMethod != nullptr) {
				startForwardThrow(throwingMethod);
				write("returnValue");
				endForwardThrow(throwingMethod);
			}
			writeLine("return returnValue;");
		}
	}
}

void GenC::writeSwitchCaseBody(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	const FuConst * konst;
	if (dynamic_cast<const FuVar *>((*statements)[0].get()) || ((konst = dynamic_cast<const FuConst *>((*statements)[0].get())) && dynamic_cast<const FuArrayStorageType *>(konst->type.get())))
		writeCharLine(';');
	int varsToDestructCount = std::ssize(this->varsToDestruct);
	writeStatements(statements);
	trimVarsToDestruct(varsToDestructCount);
}

void GenC::visitSwitch(const FuSwitch * statement)
{
	if (statement->isTypeMatching())
		notSupported(statement, "Type-matching 'switch'");
	else
		GenCCpp::visitSwitch(statement);
}

void GenC::visitThrow(const FuThrow * statement)
{
	writeThrow();
}

bool GenC::tryWriteCallAndReturn(const std::vector<std::shared_ptr<FuStatement>> * statements, int lastCallIndex, const FuExpr * returnValue)
{
	if (std::ssize(this->varsToDestruct) > 0)
		return false;
	for (int i = 0; i < lastCallIndex; i++) {
		const FuVar * def;
		if ((def = dynamic_cast<const FuVar *>((*statements)[i].get())) && needToDestruct(def))
			return false;
	}
	const FuExpr * call;
	if (!(call = dynamic_cast<const FuExpr *>((*statements)[lastCallIndex].get())))
		return false;
	const FuMethod * throwingMethod = getThrowingMethod(call);
	if (throwingMethod == nullptr)
		return false;
	writeFirstStatements(statements, lastCallIndex);
	write("return ");
	if (dynamic_cast<const FuNumericType *>(throwingMethod->type.get())) {
		if (const FuRangeType *range = dynamic_cast<const FuRangeType *>(throwingMethod->type.get())) {
			call->accept(this, FuPriority::equality);
			write(" != ");
			writeRangeThrowReturnValue(range);
		}
		else {
			includeMath();
			writeCall("!isnan", call);
		}
	}
	else if (throwingMethod->type->id == FuId::voidType)
		call->accept(this, FuPriority::select);
	else {
		call->accept(this, FuPriority::equality);
		write(" != NULL");
	}
	if (returnValue != nullptr) {
		write(" ? ");
		returnValue->accept(this, FuPriority::select);
		write(" : ");
		writeThrowReturnValue(this->currentMethod->type.get(), true);
	}
	writeCharLine(';');
	return true;
}

void GenC::writeStatements(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	int i = std::ssize(*statements) - 2;
	const FuReturn * ret;
	if (i >= 0 && (ret = dynamic_cast<const FuReturn *>((*statements)[i + 1].get())) && tryWriteCallAndReturn(statements, i, ret->value.get()))
		return;
	GenBase::writeStatements(statements);
}

void GenC::writeEnum(const FuEnum * enu)
{
	writeNewLine();
	writeDoc(enu->documentation.get());
	write("typedef enum ");
	openBlock();
	enu->acceptValues(this);
	writeNewLine();
	this->indent--;
	write("} ");
	writeName(enu);
	writeCharLine(';');
}

void GenC::writeTypedef(const FuClass * klass)
{
	if (klass->callType == FuCallType::static_ || klass->id == FuId::exceptionClass)
		return;
	write("typedef struct ");
	writeName(klass);
	writeChar(' ');
	writeName(klass);
	writeCharLine(';');
}

void GenC::writeTypedefs(const FuProgram * program, bool pub)
{
	for (const FuSymbol * type = program->first; type != nullptr; type = type->next) {
		if (const FuClass *klass = dynamic_cast<const FuClass *>(type)) {
			if (klass->isPublic == pub)
				writeTypedef(klass);
		}
		else if (const FuEnum *enu = dynamic_cast<const FuEnum *>(type)) {
			if (enu->isPublic == pub)
				writeEnum(enu);
		}
		else
			std::abort();
	}
}

void GenC::writeInstanceParameters(const FuMethod * method)
{
	writeChar('(');
	if (!method->isMutator())
		write("const ");
	writeName(method->parent);
	write(" *self");
	writeRemainingParameters(method, false, false);
}

void GenC::writeSignature(const FuMethod * method)
{
	const FuClass * klass = static_cast<const FuClass *>(method->parent);
	if (!klass->isPublic || method->visibility != FuVisibility::public_)
		write("static ");
	writeReturnType(method);
	writeName(klass);
	writeChar('_');
	write(method->name);
	if (method->callType != FuCallType::static_)
		writeInstanceParameters(method);
	else if (method->parameters.count() == 0)
		write("(void)");
	else
		writeParameters(method, false);
}

void GenC::writeVtblFields(const FuClass * klass)
{
	if (const FuClass *baseClass = dynamic_cast<const FuClass *>(klass->parent))
		writeVtblFields(baseClass);
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const FuMethod * method;
		if ((method = dynamic_cast<const FuMethod *>(symbol)) && method->isAbstractOrVirtual()) {
			writeReturnType(method);
			write("(*");
			writeCamelCase(method->name);
			writeChar(')');
			writeInstanceParameters(method);
			writeCharLine(';');
		}
	}
}

void GenC::writeVtblStruct(const FuClass * klass)
{
	write("typedef struct ");
	openBlock();
	writeVtblFields(klass);
	this->indent--;
	write("} ");
	writeName(klass);
	writeLine("Vtbl;");
}

std::string_view GenC::getConst(const FuArrayStorageType * array) const
{
	return "const ";
}

void GenC::writeConst(const FuConst * konst)
{
	if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(konst->type.get())) {
		write("static ");
		write(getConst(array));
		writeTypeAndName(konst);
		write(" = ");
		konst->value->accept(this, FuPriority::argument);
		writeCharLine(';');
	}
	else if (konst->visibility == FuVisibility::public_) {
		write("#define ");
		writeName(konst);
		writeChar(' ');
		konst->value->accept(this, FuPriority::argument);
		writeNewLine();
	}
}

void GenC::writeField(const FuField * field)
{
	std::abort();
}

bool GenC::hasVtblValue(const FuClass * klass)
{
	if (klass->callType == FuCallType::static_ || klass->callType == FuCallType::abstract)
		return false;
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const FuMethod *method = dynamic_cast<const FuMethod *>(symbol)) {
			switch (method->callType) {
			case FuCallType::virtual_:
			case FuCallType::override_:
			case FuCallType::sealed:
				return true;
			default:
				break;
			}
		}
	}
	return false;
}

bool GenC::needsConstructor(const FuClass * klass) const
{
	if (klass->id == FuId::matchClass)
		return false;
	const FuClass * baseClass;
	return GenBase::needsConstructor(klass) || hasVtblValue(klass) || ((baseClass = dynamic_cast<const FuClass *>(klass->parent)) && needsConstructor(baseClass));
}

void GenC::writeXstructorSignature(std::string_view name, const FuClass * klass)
{
	write("static void ");
	writeName(klass);
	writeChar('_');
	write(name);
	writeChar('(');
	writeName(klass);
	write(" *self)");
}

void GenC::writeSignatures(const FuClass * klass, bool pub)
{
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const FuConst * konst;
		const FuMethod * method;
		if ((konst = dynamic_cast<const FuConst *>(symbol)) && (klass->isPublic && konst->visibility == FuVisibility::public_) == pub) {
			if (pub) {
				writeNewLine();
				writeDoc(konst->documentation.get());
			}
			writeConst(konst);
		}
		else if ((method = dynamic_cast<const FuMethod *>(symbol)) && method->isLive && (klass->isPublic && method->visibility == FuVisibility::public_) == pub && method->callType != FuCallType::abstract && method->id != FuId::main) {
			writeNewLine();
			writeMethodDoc(method);
			writeSignature(method);
			writeCharLine(';');
		}
	}
}

void GenC::writeClassInternal(const FuClass * klass)
{
	if (klass->id == FuId::exceptionClass)
		return;
	this->currentClass = klass;
	if (klass->callType != FuCallType::static_) {
		writeNewLine();
		if (klass->addsVirtualMethods())
			writeVtblStruct(klass);
		writeDoc(klass->documentation.get());
		write("struct ");
		writeName(klass);
		writeChar(' ');
		openBlock();
		if (getVtblPtrClass(klass) == klass) {
			write("const ");
			writeName(klass);
			writeLine("Vtbl *vtbl;");
		}
		if (dynamic_cast<const FuClass *>(klass->parent)) {
			writeName(klass->parent);
			writeLine(" base;");
		}
		for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
			if (const FuField *field = dynamic_cast<const FuField *>(symbol)) {
				writeDoc(field->documentation.get());
				writeTypeAndName(field);
				writeCharLine(';');
			}
			else if (const FuNative *nat = dynamic_cast<const FuNative *>(symbol))
				visitNative(nat);
		}
		this->indent--;
		writeLine("};");
		if (needsConstructor(klass)) {
			writeXstructorSignature("Construct", klass);
			writeCharLine(';');
		}
		if (needsDestructor(klass)) {
			writeXstructorSignature("Destruct", klass);
			writeCharLine(';');
		}
	}
	writeSignatures(klass, false);
}

void GenC::writeVtbl(const FuClass * definingClass, const FuClass * declaringClass)
{
	if (const FuClass *baseClass = dynamic_cast<const FuClass *>(declaringClass->parent))
		writeVtbl(definingClass, baseClass);
	for (const FuSymbol * symbol = declaringClass->first; symbol != nullptr; symbol = symbol->next) {
		const FuMethod * declaredMethod;
		if ((declaredMethod = dynamic_cast<const FuMethod *>(symbol)) && declaredMethod->isAbstractOrVirtual()) {
			const FuSymbol * definedMethod = definingClass->tryLookup(declaredMethod->name, false).get();
			if (declaredMethod != definedMethod) {
				writeChar('(');
				writeReturnType(declaredMethod);
				write("(*)");
				writeInstanceParameters(declaredMethod);
				write(") ");
			}
			writeName(definedMethod);
			writeCharLine(',');
		}
	}
}

void GenC::writeConstructor(const FuClass * klass)
{
	if (!needsConstructor(klass))
		return;
	writeNewLine();
	writeXstructorSignature("Construct", klass);
	writeNewLine();
	openBlock();
	const FuClass * baseClass;
	if ((baseClass = dynamic_cast<const FuClass *>(klass->parent)) && needsConstructor(baseClass)) {
		writeName(baseClass);
		writeLine("_Construct(&self->base);");
	}
	if (hasVtblValue(klass)) {
		const FuClass * structClass = getVtblStructClass(klass);
		write("static const ");
		writeName(structClass);
		write("Vtbl vtbl = ");
		openBlock();
		writeVtbl(klass, structClass);
		this->indent--;
		writeLine("};");
		const FuClass * ptrClass = getVtblPtrClass(klass);
		writeSelfForField(ptrClass);
		write("vtbl = ");
		if (ptrClass != structClass) {
			write("(const ");
			writeName(ptrClass);
			write("Vtbl *) ");
		}
		writeLine("&vtbl;");
	}
	this->conditionVarInScope = false;
	writeConstructorBody(klass);
	closeBlock();
}

void GenC::writeDestructFields(const FuSymbol * symbol)
{
	if (symbol != nullptr) {
		writeDestructFields(symbol->next);
		if (const FuField *field = dynamic_cast<const FuField *>(symbol))
			writeDestruct(field);
	}
}

void GenC::writeDestructor(const FuClass * klass)
{
	if (!needsDestructor(klass))
		return;
	writeNewLine();
	writeXstructorSignature("Destruct", klass);
	writeNewLine();
	openBlock();
	writeDestructFields(klass->first);
	const FuClass * baseClass;
	if ((baseClass = dynamic_cast<const FuClass *>(klass->parent)) && needsDestructor(baseClass)) {
		writeName(baseClass);
		writeLine("_Destruct(&self->base);");
	}
	closeBlock();
}

void GenC::writeNewDelete(const FuClass * klass, bool define)
{
	if (!klass->isPublic || klass->constructor == nullptr || klass->constructor->visibility != FuVisibility::public_)
		return;
	writeNewLine();
	writeName(klass);
	write(" *");
	writeName(klass);
	write("_New(void)");
	if (define) {
		writeNewLine();
		openBlock();
		writeName(klass);
		write(" *self = (");
		writeName(klass);
		write(" *) malloc(sizeof(");
		writeName(klass);
		writeLine("));");
		if (needsConstructor(klass)) {
			writeLine("if (self != NULL)");
			this->indent++;
			writeName(klass);
			writeLine("_Construct(self);");
			this->indent--;
		}
		writeLine("return self;");
		closeBlock();
		writeNewLine();
	}
	else
		writeCharLine(';');
	write("void ");
	writeName(klass);
	write("_Delete(");
	writeName(klass);
	write(" *self)");
	if (define) {
		writeNewLine();
		openBlock();
		if (needsDestructor(klass)) {
			writeLine("if (self == NULL)");
			this->indent++;
			writeLine("return;");
			this->indent--;
			writeName(klass);
			writeLine("_Destruct(self);");
		}
		writeLine("free(self);");
		closeBlock();
	}
	else
		writeCharLine(';');
}

bool GenC::canThrow(const FuType * type)
{
	switch (type->id) {
	case FuId::voidType:
	case FuId::floatType:
	case FuId::doubleType:
		return true;
	default:
		if (dynamic_cast<const FuRangeType *>(type))
			return true;
		else if (dynamic_cast<const FuStorageType *>(type))
			return false;
		else if (dynamic_cast<const FuClassType *>(type))
			return !type->nullable;
		else
			return false;
	}
}

void GenC::writeMethod(const FuMethod * method)
{
	if (!method->isLive || method->callType == FuCallType::abstract)
		return;
	if (std::ssize(method->throws) > 0 && !canThrow(method->type.get()))
		notSupported(method, "Throwing from this method type");
	writeNewLine();
	if (method->id == FuId::main) {
		write("int main(");
		write(method->parameters.count() == 1 ? "int argc, char **argv)" : "void)");
	}
	else {
		writeSignature(method);
		for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
			if (needToDestruct(param))
				this->varsToDestruct.push_back(param);
		}
	}
	writeNewLine();
	this->currentMethod = method;
	this->conditionVarInScope = false;
	openBlock();
	if (const FuBlock *block = dynamic_cast<const FuBlock *>(method->body.get())) {
		const std::vector<std::shared_ptr<FuStatement>> * statements = &block->statements;
		if (!block->completesNormally())
			writeStatements(statements);
		else if (std::ssize(method->throws) > 0 && method->type->id == FuId::voidType) {
			if (std::ssize(*statements) == 0 || !tryWriteCallAndReturn(statements, std::ssize(*statements) - 1, nullptr)) {
				writeStatements(statements);
				writeDestructAll();
				writeLine("return true;");
			}
		}
		else {
			writeStatements(statements);
			writeDestructAll();
		}
	}
	else
		method->body->acceptStatement(this);
	this->varsToDestruct.clear();
	closeBlock();
	this->currentMethod = nullptr;
}

void GenC::writeIntMaxMin(std::string_view klassName, std::string_view method, std::string_view type, int op)
{
	writeNewLine();
	write("static ");
	write(type);
	write(" Fu");
	write(klassName);
	writeChar('_');
	write(method);
	writeChar('(');
	write(type);
	write(" x, ");
	write(type);
	writeLine(" y)");
	openBlock();
	write("return x ");
	writeChar(op);
	writeLine(" y ? x : y;");
	closeBlock();
}

void GenC::writeIntLibrary(std::string_view klassName, std::string_view type, const std::set<FuId> * methods)
{
	if (methods->contains(FuId::mathMin))
		writeIntMaxMin(klassName, "Min", type, '<');
	if (methods->contains(FuId::mathMax))
		writeIntMaxMin(klassName, "Max", type, '>');
	if (methods->contains(FuId::mathClamp)) {
		writeNewLine();
		write("static ");
		write(type);
		write(" Fu");
		write(klassName);
		write("_Clamp(");
		write(type);
		write(" x, ");
		write(type);
		write(" minValue, ");
		write(type);
		writeLine(" maxValue)");
		openBlock();
		writeLine("return x < minValue ? minValue : x > maxValue ? maxValue : x;");
		closeBlock();
	}
}

void GenC::writeTryParseLibrary(std::string_view signature, std::string_view call)
{
	writeNewLine();
	write("static bool Fu");
	writeLine(signature);
	openBlock();
	writeLine("if (*str == '\\0')");
	writeLine("\treturn false;");
	writeLine("char *end;");
	write("*result = strto");
	write(call);
	writeLine(");");
	writeLine("return *end == '\\0';");
	closeBlock();
}

void GenC::writeLibrary()
{
	writeIntLibrary("Int", "int", &this->intFunctions);
	writeIntLibrary("Long", "int64_t", &this->longFunctions);
	if (this->intTryParse)
		writeTryParseLibrary("Int_TryParse(int *result, const char *str, int base)", "l(str, &end, base");
	if (this->longTryParse)
		writeTryParseLibrary("Long_TryParse(int64_t *result, const char *str, int base)", "ll(str, &end, base");
	if (this->doubleTryParse)
		writeTryParseLibrary("Double_TryParse(double *result, const char *str)", "d(str, &end");
	if (this->stringAssign) {
		writeNewLine();
		writeLine("static void FuString_Assign(char **str, char *value)");
		openBlock();
		writeLine("free(*str);");
		writeLine("*str = value;");
		closeBlock();
	}
	if (this->stringSubstring) {
		writeNewLine();
		writeLine("static char *FuString_Substring(const char *str, size_t len)");
		openBlock();
		writeLine("char *p = malloc(len + 1);");
		writeLine("memcpy(p, str, len);");
		writeLine("p[len] = '\\0';");
		writeLine("return p;");
		closeBlock();
	}
	if (this->stringAppend) {
		writeNewLine();
		writeLine("static void FuString_AppendSubstring(char **str, const char *suffix, size_t suffixLen)");
		openBlock();
		writeLine("if (suffixLen == 0)");
		writeLine("\treturn;");
		writeLine("size_t prefixLen = *str == NULL ? 0 : strlen(*str);");
		writeLine("*str = realloc(*str, prefixLen + suffixLen + 1);");
		writeLine("memcpy(*str + prefixLen, suffix, suffixLen);");
		writeLine("(*str)[prefixLen + suffixLen] = '\\0';");
		closeBlock();
		writeNewLine();
		writeLine("static void FuString_Append(char **str, const char *suffix)");
		openBlock();
		writeLine("FuString_AppendSubstring(str, suffix, strlen(suffix));");
		closeBlock();
	}
	if (this->stringIndexOf) {
		writeNewLine();
		writeLine("static ptrdiff_t FuString_IndexOf(const char *str, const char *needle)");
		openBlock();
		writeLine("const char *p = strstr(str, needle);");
		writeLine("return p == NULL ? -1 : p - str;");
		closeBlock();
	}
	if (this->stringLastIndexOf) {
		writeNewLine();
		writeLine("static ptrdiff_t FuString_LastIndexOf(const char *str, const char *needle)");
		openBlock();
		writeLine("if (needle[0] == '\\0')");
		writeLine("\treturn (ptrdiff_t) strlen(str);");
		writeLine("ptrdiff_t result = -1;");
		writeLine("const char *p = strstr(str, needle);");
		write("while (p != NULL) ");
		openBlock();
		writeLine("result = p - str;");
		writeLine("p = strstr(p + 1, needle);");
		closeBlock();
		writeLine("return result;");
		closeBlock();
	}
	if (this->stringEndsWith) {
		writeNewLine();
		writeLine("static bool FuString_EndsWith(const char *str, const char *suffix)");
		openBlock();
		writeLine("size_t strLen = strlen(str);");
		writeLine("size_t suffixLen = strlen(suffix);");
		writeLine("return strLen >= suffixLen && memcmp(str + strLen - suffixLen, suffix, suffixLen) == 0;");
		closeBlock();
	}
	if (this->stringReplace) {
		writeNewLine();
		writeLine("static char *FuString_Replace(const char *s, const char *oldValue, const char *newValue)");
		openBlock();
		write("for (char *result = NULL;;) ");
		openBlock();
		writeLine("const char *p = strstr(s, oldValue);");
		writeLine("if (p == NULL) {");
		writeLine("\tFuString_Append(&result, s);");
		writeLine("\treturn result == NULL ? strdup(\"\") : result;");
		writeCharLine('}');
		writeLine("FuString_AppendSubstring(&result, s, p - s);");
		writeLine("FuString_Append(&result, newValue);");
		writeLine("s = p + strlen(oldValue);");
		closeBlock();
		closeBlock();
	}
	if (this->stringFormat) {
		writeNewLine();
		writeLine("static char *FuString_Format(const char *format, ...)");
		openBlock();
		writeLine("va_list args1;");
		writeLine("va_start(args1, format);");
		writeLine("va_list args2;");
		writeLine("va_copy(args2, args1);");
		writeLine("size_t len = vsnprintf(NULL, 0, format, args1) + 1;");
		writeLine("va_end(args1);");
		writeLine("char *str = malloc(len);");
		writeLine("vsnprintf(str, len, format, args2);");
		writeLine("va_end(args2);");
		writeLine("return str;");
		closeBlock();
	}
	if (this->matchFind) {
		writeNewLine();
		writeLine("static bool FuMatch_Find(GMatchInfo **match_info, const char *input, const char *pattern, GRegexCompileFlags options)");
		openBlock();
		writeLine("GRegex *regex = g_regex_new(pattern, options, 0, NULL);");
		writeLine("bool result = g_regex_match(regex, input, 0, match_info);");
		writeLine("g_regex_unref(regex);");
		writeLine("return result;");
		closeBlock();
	}
	if (this->matchPos) {
		writeNewLine();
		writeLine("static int FuMatch_GetPos(const GMatchInfo *match_info, int which)");
		openBlock();
		writeLine("int start;");
		writeLine("int end;");
		writeLine("g_match_info_fetch_pos(match_info, 0, &start, &end);");
		writeLine("switch (which) {");
		writeLine("case 0:");
		writeLine("\treturn start;");
		writeLine("case 1:");
		writeLine("\treturn end;");
		writeLine("default:");
		writeLine("\treturn end - start;");
		writeCharLine('}');
		closeBlock();
	}
	if (this->ptrConstruct) {
		writeNewLine();
		writeLine("static void FuPtr_Construct(void **ptr)");
		openBlock();
		writeLine("*ptr = NULL;");
		closeBlock();
	}
	if (this->sharedMake || this->sharedAddRef || this->sharedRelease) {
		writeNewLine();
		writeLine("typedef void (*FuMethodPtr)(void *);");
		writeLine("typedef struct {");
		this->indent++;
		writeLine("size_t count;");
		writeLine("size_t unitSize;");
		writeLine("size_t refCount;");
		writeLine("FuMethodPtr destructor;");
		this->indent--;
		writeLine("} FuShared;");
	}
	if (this->sharedMake) {
		writeNewLine();
		writeLine("static void *FuShared_Make(size_t count, size_t unitSize, FuMethodPtr constructor, FuMethodPtr destructor)");
		openBlock();
		writeLine("FuShared *self = (FuShared *) malloc(sizeof(FuShared) + count * unitSize);");
		writeLine("self->count = count;");
		writeLine("self->unitSize = unitSize;");
		writeLine("self->refCount = 1;");
		writeLine("self->destructor = destructor;");
		write("if (constructor != NULL) ");
		openBlock();
		writeLine("for (size_t i = 0; i < count; i++)");
		writeLine("\tconstructor((char *) (self + 1) + i * unitSize);");
		closeBlock();
		writeLine("return self + 1;");
		closeBlock();
	}
	if (this->sharedAddRef) {
		writeNewLine();
		writeLine("static void *FuShared_AddRef(void *ptr)");
		openBlock();
		writeLine("if (ptr != NULL)");
		writeLine("\t((FuShared *) ptr)[-1].refCount++;");
		writeLine("return ptr;");
		closeBlock();
	}
	if (this->sharedRelease || this->sharedAssign) {
		writeNewLine();
		writeLine("static void FuShared_Release(void *ptr)");
		openBlock();
		writeLine("if (ptr == NULL)");
		writeLine("\treturn;");
		writeLine("FuShared *self = (FuShared *) ptr - 1;");
		writeLine("if (--self->refCount != 0)");
		writeLine("\treturn;");
		write("if (self->destructor != NULL) ");
		openBlock();
		writeLine("for (size_t i = self->count; i > 0;)");
		writeLine("\tself->destructor((char *) ptr + --i * self->unitSize);");
		closeBlock();
		writeLine("free(self);");
		closeBlock();
	}
	if (this->sharedAssign) {
		writeNewLine();
		writeLine("static void FuShared_Assign(void **ptr, void *value)");
		openBlock();
		writeLine("FuShared_Release(*ptr);");
		writeLine("*ptr = value;");
		closeBlock();
	}
	for (FuId id : this->listFrees) {
		writeNewLine();
		write("static void ");
		writeListFreeName(id);
		writeLine("(void *ptr)");
		openBlock();
		switch (id) {
		case FuId::none:
			write("FuShared_Release(*(void **)");
			break;
		case FuId::stringClass:
			write("free(*(char **)");
			break;
		case FuId::listClass:
			write("g_array_unref(*(GArray **)");
			break;
		case FuId::queueClass:
			write("g_queue_clear((GQueue *)");
			break;
		case FuId::dictionaryClass:
			write("g_hash_table_unref(*(GHashTable **)");
			break;
		case FuId::sortedDictionaryClass:
			write("g_tree_unref(*(GTree **)");
			break;
		case FuId::regexClass:
			write("g_regex_unref(*(GRegex **)");
			break;
		case FuId::matchClass:
			write("g_match_info_unref(*(GMatchInfo **)");
			break;
		default:
			std::abort();
		}
		writeLine(" ptr);");
		closeBlock();
	}
	if (this->treeCompareInteger) {
		writeNewLine();
		write("static int FuTree_CompareInteger(gconstpointer pa, gconstpointer pb, gpointer user_data)");
		openBlock();
		writeLine("gintptr a = (gintptr) pa;");
		writeLine("gintptr b = (gintptr) pb;");
		writeLine("return (a > b) - (a < b);");
		closeBlock();
	}
	if (this->treeCompareString) {
		writeNewLine();
		write("static int FuTree_CompareString(gconstpointer a, gconstpointer b, gpointer user_data)");
		openBlock();
		writeLine("return strcmp((const char *) a, (const char *) b);");
		closeBlock();
	}
	for (FuId typeId : this->compares) {
		writeNewLine();
		write("static int FuCompare_");
		writeNumericType(typeId);
		writeLine("(const void *pa, const void *pb)");
		openBlock();
		writeNumericType(typeId);
		write(" a = *(const ");
		writeNumericType(typeId);
		writeLine(" *) pa;");
		writeNumericType(typeId);
		write(" b = *(const ");
		writeNumericType(typeId);
		writeLine(" *) pb;");
		switch (typeId) {
		case FuId::byteRange:
		case FuId::sByteRange:
		case FuId::shortRange:
		case FuId::uShortRange:
			writeLine("return a - b;");
			break;
		default:
			writeLine("return (a > b) - (a < b);");
			break;
		}
		closeBlock();
	}
	for (FuId typeId : this->contains) {
		writeNewLine();
		write("static bool FuArray_Contains_");
		if (typeId == FuId::none)
			write("object(const void * const *a, size_t len, const void *");
		else if (typeId == FuId::stringPtrType)
			write("string(const char * const *a, size_t len, const char *");
		else {
			writeNumericType(typeId);
			write("(const ");
			writeNumericType(typeId);
			write(" *a, size_t len, ");
			writeNumericType(typeId);
		}
		writeLine(" value)");
		openBlock();
		writeLine("for (size_t i = 0; i < len; i++)");
		if (typeId == FuId::stringPtrType)
			writeLine("\tif (strcmp(a[i], value) == 0)");
		else
			writeLine("\tif (a[i] == value)");
		writeLine("\t\treturn true;");
		writeLine("return false;");
		closeBlock();
	}
}

void GenC::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	if (std::ssize(*resources) == 0)
		return;
	writeNewLine();
	for (const auto &[name, content] : *resources) {
		write("static const ");
		writeNumericType(FuId::byteRange);
		writeChar(' ');
		writeResource(name, -1);
		writeChar('[');
		visitLiteralLong(std::ssize(content));
		writeLine("] = {");
		writeChar('\t');
		writeBytes(&content);
		writeLine(" };");
	}
}

void GenC::writeProgram(const FuProgram * program)
{
	this->writtenClasses.clear();
	this->inHeaderFile = true;
	openStringWriter();
	for (const FuClass * klass : program->classes) {
		writeNewDelete(klass, false);
		writeSignatures(klass, true);
	}
	createHeaderFile(".h");
	writeLine("#ifdef __cplusplus");
	writeLine("extern \"C\" {");
	writeLine("#endif");
	writeTypedefs(program, true);
	closeStringWriter();
	writeNewLine();
	writeLine("#ifdef __cplusplus");
	writeCharLine('}');
	writeLine("#endif");
	closeFile();
	this->inHeaderFile = false;
	this->intFunctions.clear();
	this->longFunctions.clear();
	this->intTryParse = false;
	this->longTryParse = false;
	this->doubleTryParse = false;
	this->stringAssign = false;
	this->stringSubstring = false;
	this->stringAppend = false;
	this->stringIndexOf = false;
	this->stringLastIndexOf = false;
	this->stringEndsWith = false;
	this->stringReplace = false;
	this->stringFormat = false;
	this->matchFind = false;
	this->matchPos = false;
	this->ptrConstruct = false;
	this->sharedMake = false;
	this->sharedAddRef = false;
	this->sharedRelease = false;
	this->sharedAssign = false;
	this->listFrees.clear();
	this->treeCompareInteger = false;
	this->treeCompareString = false;
	this->compares.clear();
	this->contains.clear();
	openStringWriter();
	for (const FuClass * klass : program->classes)
		writeClass(klass, program);
	writeResources(&program->resources);
	for (const FuClass * klass : program->classes) {
		this->currentClass = klass;
		writeConstructor(klass);
		writeDestructor(klass);
		writeNewDelete(klass, true);
		writeMethods(klass);
	}
	include("stdlib.h");
	createImplementationFile(program, ".h");
	writeLibrary();
	writeRegexOptionsEnum(program);
	writeTypedefs(program, false);
	closeStringWriter();
	closeFile();
}

std::string_view GenCl::getTargetName() const
{
	return "OpenCL C";
}

void GenCl::includeStdBool()
{
}

void GenCl::includeMath()
{
}

void GenCl::writeNumericType(FuId id)
{
	switch (id) {
	case FuId::sByteRange:
		write("char");
		break;
	case FuId::byteRange:
		write("uchar");
		break;
	case FuId::shortRange:
		write("short");
		break;
	case FuId::uShortRange:
		write("ushort");
		break;
	case FuId::intType:
		write("int");
		break;
	case FuId::nIntType:
		write("ptrdiff_t");
		break;
	case FuId::longType:
		write("long");
		break;
	default:
		std::abort();
	}
}

void GenCl::writeStringPtrType()
{
	write("constant char *");
}

void GenCl::writeClassType(const FuClassType * klass, bool space)
{
	switch (klass->class_->id) {
	case FuId::none:
		if (dynamic_cast<const FuDynamicPtrType *>(klass))
			notSupported(klass, "Dynamic reference");
		else
			GenC::writeClassType(klass, space);
		break;
	case FuId::stringClass:
		if (klass->id == FuId::stringStorageType)
			notSupported(klass, "string()");
		else
			writeStringPtrType();
		break;
	default:
		notSupported(klass, klass->class_->name);
		break;
	}
}

void GenCl::writePrintfLongPrefix()
{
	writeChar('l');
}

void GenCl::writeInterpolatedStringArgBase(const FuExpr * expr)
{
	expr->accept(this, FuPriority::argument);
}

void GenCl::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	notSupported(expr, "Interpolated strings");
}

void GenCl::writeCamelCaseNotKeyword(std::string_view name)
{
	if (name == "Constant" || name == "Global" || name == "Kernel" || name == "Local" || name == "Private" || name == "constant" || name == "global" || name == "kernel" || name == "local" || name == "private") {
		writeCamelCase(name);
		writeChar('_');
	}
	else
		GenC::writeCamelCaseNotKeyword(name);
}

std::string_view GenCl::getConst(const FuArrayStorageType * array) const
{
	return array->ptrTaken ? "const " : "constant ";
}

void GenCl::writeSubstringEqual(const FuCallExpr * call, std::string_view literal, FuPriority parent, bool not_)
{
	if (not_)
		writeChar('!');
	if (isUTF8GetString(call)) {
		this->bytesEqualsString = true;
		write("FuBytes_Equals(");
		writeArrayPtrAdd(call->arguments[0].get(), call->arguments[1].get());
	}
	else {
		this->stringStartsWith = true;
		write("FuString_StartsWith(");
		writeArrayPtrAdd(call->method->left.get(), call->arguments[0].get());
	}
	write(", ");
	visitLiteralString(literal);
	writeChar(')');
}

void GenCl::writeEqualStringInternal(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	this->stringEquals = true;
	if (not_)
		writeChar('!');
	writeCall("FuString_Equals", left, right);
}

void GenCl::writeStringLength(const FuExpr * expr)
{
	this->stringLength = true;
	writeCall("strlen", expr);
}

void GenCl::writeConsoleWrite(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine)
{
	write("printf(");
	if (std::ssize(*args) == 0)
		write("\"\\n\")");
	else if (const FuInterpolatedString *interpolated = dynamic_cast<const FuInterpolatedString *>((*args)[0].get()))
		writePrintf(interpolated, newLine);
	else
		writePrintfNotInterpolated(args, newLine);
}

void GenCl::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::none:
	case FuId::classToString:
		writeCCall(obj, method, args);
		break;
	case FuId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case FuId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case FuId::stringStartsWith:
		{
			int c = getOneAscii((*args)[0].get());
			if (c >= 0) {
				if (parent > FuPriority::equality)
					writeChar('(');
				writePostfix(obj, "[0] == ");
				visitLiteralChar(c);
				if (parent > FuPriority::equality)
					writeChar(')');
			}
			else {
				this->stringStartsWith = true;
				writeCall("FuString_StartsWith", obj, (*args)[0].get());
			}
			break;
		}
	case FuId::stringSubstring:
		if (std::ssize(*args) == 1)
			writeStringSubstringStart(obj, args, parent);
		else
			notSupported(obj, "Substring");
		break;
	case FuId::arrayCopyTo:
		write("for (size_t _i = 0; _i < ");
		(*args)[3]->accept(this, FuPriority::rel);
		writeLine("; _i++)");
		writeChar('\t');
		(*args)[1]->accept(this, FuPriority::primary);
		writeChar('[');
		startAdd((*args)[2].get());
		write("_i] = ");
		obj->accept(this, FuPriority::primary);
		writeChar('[');
		startAdd((*args)[0].get());
		write("_i]");
		break;
	case FuId::arrayFillAll:
	case FuId::arrayFillPart:
		writeArrayFill(obj, args);
		break;
	case FuId::consoleWrite:
		writeConsoleWrite(args, false);
		break;
	case FuId::consoleWriteLine:
		writeConsoleWrite(args, true);
		break;
	case FuId::uTF8GetByteCount:
		writeStringLength((*args)[0].get());
		break;
	case FuId::uTF8GetBytes:
		write("for (size_t _i = 0; ");
		(*args)[0]->accept(this, FuPriority::primary);
		writeLine("[_i] != '\\0'; _i++)");
		writeChar('\t');
		(*args)[1]->accept(this, FuPriority::primary);
		writeChar('[');
		startAdd((*args)[2].get());
		write("_i] = ");
		writePostfix((*args)[0].get(), "[_i]");
		break;
	case FuId::mathMethod:
	case FuId::mathClamp:
	case FuId::mathIsFinite:
	case FuId::mathIsNaN:
	case FuId::mathLog2:
	case FuId::mathRound:
		writeLowercase(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathAbs:
		if (dynamic_cast<const FuFloatingType *>((*args)[0]->type.get()))
			writeChar('f');
		writeCall("abs", (*args)[0].get());
		break;
	case FuId::mathCeiling:
		writeCall("ceil", (*args)[0].get());
		break;
	case FuId::mathFusedMultiplyAdd:
		writeCall("fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case FuId::mathIsInfinity:
		writeCall("isinf", (*args)[0].get());
		break;
	case FuId::mathMax:
		if (dynamic_cast<const FuFloatingType *>((*args)[0]->type.get()) || dynamic_cast<const FuFloatingType *>((*args)[1]->type.get()))
			writeChar('f');
		writeCall("max", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::mathMin:
		if (dynamic_cast<const FuFloatingType *>((*args)[0]->type.get()) || dynamic_cast<const FuFloatingType *>((*args)[1]->type.get()))
			writeChar('f');
		writeCall("min", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::mathTruncate:
		writeCall("trunc", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenCl::writeAssert(const FuAssert * statement)
{
}

void GenCl::writeSwitchCaseBody(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	if (std::all_of(statements->begin(), statements->end(), [](const std::shared_ptr<FuStatement> &statement) { return dynamic_cast<const FuAssert *>(statement.get()); }))
		writeCharLine(';');
	else
		GenC::writeSwitchCaseBody(statements);
}

void GenCl::writeLibrary()
{
	if (this->stringLength) {
		writeNewLine();
		writeLine("static ptrdiff_t strlen(constant char *str)");
		openBlock();
		writeLine("ptrdiff_t len = 0;");
		writeLine("while (str[len] != '\\0')");
		writeLine("\tlen++;");
		writeLine("return len;");
		closeBlock();
	}
	if (this->stringEquals) {
		writeNewLine();
		writeLine("static bool FuString_Equals(constant char *str1, constant char *str2)");
		openBlock();
		writeLine("for (size_t i = 0; str1[i] == str2[i]; i++) {");
		writeLine("\tif (str1[i] == '\\0')");
		writeLine("\t\treturn true;");
		writeCharLine('}');
		writeLine("return false;");
		closeBlock();
	}
	if (this->stringStartsWith) {
		writeNewLine();
		writeLine("static bool FuString_StartsWith(constant char *str1, constant char *str2)");
		openBlock();
		writeLine("for (int i = 0; str2[i] != '\\0'; i++) {");
		writeLine("\tif (str1[i] != str2[i])");
		writeLine("\t\treturn false;");
		writeCharLine('}');
		writeLine("return true;");
		closeBlock();
	}
	if (this->bytesEqualsString) {
		writeNewLine();
		writeLine("static bool FuBytes_Equals(const uchar *mem, constant char *str)");
		openBlock();
		writeLine("for (size_t i = 0; str[i] != '\\0'; i++) {");
		writeLine("\tif (mem[i] != str[i])");
		writeLine("\t\treturn false;");
		writeCharLine('}');
		writeLine("return true;");
		closeBlock();
	}
}

void GenCl::writeProgram(const FuProgram * program)
{
	this->writtenClasses.clear();
	this->stringLength = false;
	this->stringEquals = false;
	this->stringStartsWith = false;
	this->bytesEqualsString = false;
	openStringWriter();
	for (const FuClass * klass : program->classes) {
		this->currentClass = klass;
		writeConstructor(klass);
		writeDestructor(klass);
		writeMethods(klass);
	}
	createOutputFile();
	writeTopLevelNatives(program);
	writeRegexOptionsEnum(program);
	writeTypedefs(program, true);
	for (const FuClass * klass : program->classes)
		writeSignatures(klass, true);
	writeTypedefs(program, false);
	for (const FuClass * klass : program->classes)
		writeClass(klass, program);
	writeResources(&program->resources);
	writeLibrary();
	closeStringWriter();
	closeFile();
}

std::string_view GenCpp::getTargetName() const
{
	return "C++";
}

void GenCpp::includeStdInt()
{
	include("cstdint");
}

void GenCpp::includeStdDef()
{
	include("cstddef");
}

void GenCpp::includeAssert()
{
	include("cassert");
}

void GenCpp::includeMath()
{
	include("cmath");
}

void GenCpp::visitLiteralNull()
{
	write("nullptr");
}

void GenCpp::startMethodCall(const FuExpr * obj)
{
	obj->accept(this, FuPriority::primary);
	writeMemberOp(obj, nullptr);
}

void GenCpp::writeInterpolatedStringArg(const FuExpr * expr)
{
	const FuClassType * klass;
	if ((klass = dynamic_cast<const FuClassType *>(expr->type.get())) && klass->class_->id != FuId::stringClass) {
		startMethodCall(expr);
		write("toString()");
	}
	else
		GenBase::writeInterpolatedStringArg(expr);
}

void GenCpp::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	include("format");
	write("std::format(\"");
	for (const FuInterpolatedPart &part : expr->parts) {
		writeDoubling(part.prefix, '{');
		writeChar('{');
		writePyFormat(&part);
	}
	writeDoubling(expr->suffix, '{');
	writeChar('"');
	writeInterpolatedStringArgs(expr);
	writeChar(')');
}

void GenCpp::writeCamelCaseNotKeyword(std::string_view name)
{
	writeCamelCase(name);
	if (name == "And" || name == "Asm" || name == "Auto" || name == "Bool" || name == "Break" || name == "Byte" || name == "Case" || name == "Catch" || name == "Char" || name == "Class" || name == "Const" || name == "Continue" || name == "Default" || name == "Delete" || name == "Do" || name == "Double" || name == "Else" || name == "Enum" || name == "Explicit" || name == "Export" || name == "Extern" || name == "False" || name == "Float" || name == "For" || name == "Goto" || name == "If" || name == "Inline" || name == "Int" || name == "Long" || name == "Namespace" || name == "New" || name == "Not" || name == "Nullptr" || name == "Operator" || name == "Or" || name == "Override" || name == "Private" || name == "Protected" || name == "Public" || name == "Register" || name == "Return" || name == "Short" || name == "Signed" || name == "Sizeof" || name == "Static" || name == "Struct" || name == "Switch" || name == "Throw" || name == "True" || name == "Try" || name == "Typedef" || name == "Union" || name == "Unsigned" || name == "Using" || name == "Virtual" || name == "Void" || name == "Volatile" || name == "While" || name == "Xor" || name == "and" || name == "asm" || name == "auto" || name == "catch" || name == "char" || name == "delete" || name == "explicit" || name == "export" || name == "extern" || name == "goto" || name == "inline" || name == "namespace" || name == "not" || name == "nullptr" || name == "operator" || name == "or" || name == "private" || name == "register" || name == "signed" || name == "sizeof" || name == "struct" || name == "try" || name == "typedef" || name == "union" || name == "unsigned" || name == "using" || name == "volatile" || name == "xor")
		writeChar('_');
}

void GenCpp::writeName(const FuSymbol * symbol)
{
	if (dynamic_cast<const FuContainerType *>(symbol))
		write(symbol->name);
	else if (dynamic_cast<const FuVar *>(symbol) || dynamic_cast<const FuMember *>(symbol))
		writeCamelCaseNotKeyword(symbol->name);
	else
		std::abort();
}

void GenCpp::writeLocalName(const FuSymbol * symbol, FuPriority parent)
{
	if (dynamic_cast<const FuField *>(symbol))
		write("this->");
	writeName(symbol);
}

void GenCpp::writeSharedUnique(std::string_view prefix, bool unique, std::string_view suffix)
{
	include("memory");
	write(prefix);
	write(unique ? "unique" : "shared");
	write(suffix);
}

void GenCpp::writeCollectionType(const FuClassType * klass)
{
	const FuType * elementType = klass->typeArg0.get();
	std::string_view cppType;
	switch (klass->class_->id) {
	case FuId::arrayStorageClass:
		cppType = "array";
		break;
	case FuId::listClass:
		cppType = "vector";
		break;
	case FuId::queueClass:
		cppType = "queue";
		break;
	case FuId::stackClass:
		cppType = "stack";
		break;
	case FuId::priorityQueueClass:
		include("queue");
		write("std::priority_queue<");
		writeType(elementType, false);
		write(", std::vector<");
		writeType(elementType, false);
		write(">, std::greater<");
		writeType(elementType, false);
		write(">>");
		return;
	case FuId::hashSetClass:
		cppType = "unordered_set";
		break;
	case FuId::sortedSetClass:
		cppType = "set";
		break;
	case FuId::dictionaryClass:
		cppType = "unordered_map";
		break;
	case FuId::sortedDictionaryClass:
		cppType = "map";
		break;
	default:
		notSupported(klass, klass->class_->name);
		return;
	}
	include(cppType);
	write("std::");
	write(cppType);
	writeChar('<');
	writeType(elementType, false);
	if (const FuArrayStorageType *arrayStorage = dynamic_cast<const FuArrayStorageType *>(klass)) {
		write(", ");
		visitLiteralLong(arrayStorage->length);
	}
	else if (klass->class_->typeParameterCount == 2) {
		write(", ");
		writeType(klass->getValueType().get(), false);
	}
	writeChar('>');
}

void GenCpp::writeClassType(const FuClassType * klass)
{
	if (!dynamic_cast<const FuReadWriteClassType *>(klass))
		write("const ");
	if (klass->class_->typeParameterCount == 0) {
		switch (klass->class_->id) {
		case FuId::textWriterClass:
			include("iostream");
			write("std::ostream");
			break;
		case FuId::stringWriterClass:
			include("sstream");
			write("std::ostringstream");
			break;
		case FuId::regexClass:
			include("regex");
			write("std::regex");
			break;
		case FuId::matchClass:
			include("regex");
			write("std::cmatch");
			break;
		case FuId::lockClass:
			include("mutex");
			write("std::recursive_mutex");
			break;
		default:
			write(klass->class_->name);
			break;
		}
	}
	else
		writeCollectionType(klass);
}

void GenCpp::writeType(const FuType * type, bool promote)
{
	if (dynamic_cast<const FuIntegerType *>(type))
		writeNumericType(getTypeId(type, promote));
	else if (dynamic_cast<const FuStringStorageType *>(type)) {
		include("string");
		write("std::string");
	}
	else if (dynamic_cast<const FuStringType *>(type)) {
		include("string_view");
		write("std::string_view");
	}
	else if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(type)) {
		switch (dynamic->class_->id) {
		case FuId::regexClass:
			include("regex");
			write("std::regex");
			break;
		case FuId::arrayPtrClass:
			writeSharedUnique("std::", dynamic->unique, "_ptr<");
			writeType(dynamic->getElementType().get(), false);
			write("[]>");
			break;
		default:
			writeSharedUnique("std::", dynamic->unique, "_ptr<");
			writeClassType(dynamic);
			writeChar('>');
			break;
		}
	}
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		if (klass->class_->id == FuId::arrayPtrClass) {
			writeType(klass->getElementType().get(), false);
			if (!dynamic_cast<const FuReadWriteClassType *>(klass))
				write(" const");
		}
		else
			writeClassType(klass);
		if (!dynamic_cast<const FuStorageType *>(klass))
			write(" *");
	}
	else
		write(type->name);
}

void GenCpp::writeNewUniqueArray(bool unique, const FuType * elementType, const FuExpr * lengthExpr)
{
	writeSharedUnique("std::make_", unique, "<");
	writeType(elementType, false);
	writeCall("[]>", lengthExpr);
}

void GenCpp::writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent)
{
	writeNewUniqueArray(false, elementType, lengthExpr);
}

void GenCpp::writeNewUnique(bool unique, const FuReadWriteClassType * klass)
{
	writeSharedUnique("std::make_", unique, "<");
	writeClassType(klass);
	write(">()");
}

void GenCpp::writeNew(const FuReadWriteClassType * klass, FuPriority parent)
{
	writeNewUnique(false, klass);
}

void GenCpp::writeStorageInit(const FuNamedValue * def)
{
}

void GenCpp::writeVarInit(const FuNamedValue * def)
{
	if (def->value != nullptr && def->type->id == FuId::stringStorageType) {
		writeChar('{');
		def->value->accept(this, FuPriority::argument);
		writeChar('}');
	}
	else if (dynamic_cast<const FuArrayStorageType *>(def->type.get())) {
		const FuLiteral * literal;
		if (def->value == nullptr) {
		}
		else if ((literal = dynamic_cast<const FuLiteral *>(def->value.get())) && literal->isDefaultValue())
			write(" {}");
		else
			std::abort();
	}
	else
		GenBase::writeVarInit(def);
}

bool GenCpp::isSharedPtr(const FuExpr * expr)
{
	if (dynamic_cast<const FuDynamicPtrType *>(expr->type.get()))
		return true;
	const FuSymbolReference * symbol;
	const FuForeach * loop;
	return (symbol = dynamic_cast<const FuSymbolReference *>(expr)) && (loop = dynamic_cast<const FuForeach *>(symbol->symbol->parent)) && dynamic_cast<const FuDynamicPtrType *>(loop->collection->type->asClassType()->getElementType().get());
}

void GenCpp::writeStaticCast(const FuType * type, const FuExpr * expr)
{
	if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(type)) {
		write("std::static_pointer_cast<");
		write(dynamic->class_->name);
	}
	else {
		write("static_cast<");
		writeType(type, false);
	}
	write(">(");
	if (dynamic_cast<const FuStorageType *>(expr->type.get())) {
		writeChar('&');
		expr->accept(this, FuPriority::primary);
	}
	else if (!dynamic_cast<const FuDynamicPtrType *>(type) && isSharedPtr(expr))
		writePostfix(expr, ".get()");
	else
		getStaticCastInner(type, expr)->accept(this, FuPriority::argument);
	writeChar(')');
}

bool GenCpp::needStringPtrData(const FuExpr * expr)
{
	const FuCallExpr * call;
	if ((call = dynamic_cast<const FuCallExpr *>(expr)) && call->method->symbol->id == FuId::environmentGetEnvironmentVariable)
		return false;
	return expr->type->id == FuId::stringPtrType;
}

void GenCpp::writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	if (needStringPtrData(left) && right->type->id == FuId::nullType) {
		writePostfix(left, ".data()");
		write(getEqOp(not_));
		write("nullptr");
	}
	else if (left->type->id == FuId::nullType && needStringPtrData(right)) {
		write("nullptr");
		write(getEqOp(not_));
		writePostfix(right, ".data()");
	}
	else
		GenCCppD::writeEqual(left, right, parent, not_);
}

bool GenCpp::isClassPtr(const FuType * type)
{
	const FuClassType * ptr;
	return (ptr = dynamic_cast<const FuClassType *>(type)) && !dynamic_cast<const FuStorageType *>(type) && ptr->class_->id != FuId::stringClass && ptr->class_->id != FuId::arrayPtrClass;
}

bool GenCpp::isCppPtr(const FuExpr * expr)
{
	if (isClassPtr(expr->type.get())) {
		const FuSymbolReference * symbol;
		const FuForeach * loop;
		if ((symbol = dynamic_cast<const FuSymbolReference *>(expr)) && (loop = dynamic_cast<const FuForeach *>(symbol->symbol->parent)) && dynamic_cast<const FuStorageType *>((symbol->symbol == loop->getVar() ? loop->collection->type->asClassType()->typeArg0 : loop->collection->type->asClassType()->typeArg1).get()))
			return false;
		return true;
	}
	return false;
}

void GenCpp::writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuClassType * klass = static_cast<const FuClassType *>(expr->left->type.get());
	if (parent != FuPriority::assign) {
		switch (klass->class_->id) {
		case FuId::arrayStorageClass:
			if (klass->id == FuId::mainArgsType) {
				include("string_view");
				write("std::string_view(");
				writeArgsIndexing(expr->right.get());
				writeChar(')');
				return;
			}
			break;
		case FuId::dictionaryClass:
		case FuId::sortedDictionaryClass:
		case FuId::orderedDictionaryClass:
			startMethodCall(expr->left.get());
			write("find(");
			writeStronglyCoerced(klass->getKeyType(), expr->right.get());
			write(")->second");
			return;
		default:
			break;
		}
	}
	if (isClassPtr(expr->left->type.get())) {
		write("(*");
		expr->left->accept(this, FuPriority::primary);
		writeChar(')');
	}
	else
		expr->left->accept(this, FuPriority::primary);
	writeChar('[');
	switch (klass->class_->id) {
	case FuId::arrayPtrClass:
	case FuId::arrayStorageClass:
	case FuId::listClass:
		expr->right->accept(this, FuPriority::argument);
		break;
	default:
		writeStronglyCoerced(klass->getKeyType(), expr->right.get());
		break;
	}
	writeChar(']');
}

void GenCpp::writeMemberOp(const FuExpr * left, const FuSymbolReference * symbol)
{
	if (symbol != nullptr && dynamic_cast<const FuConst *>(symbol->symbol))
		write("::");
	else if (isCppPtr(left))
		write("->");
	else
		writeChar('.');
}

void GenCpp::writeEnumAsInt(const FuExpr * expr, FuPriority parent)
{
	writeCall("static_cast<int>", expr);
}

void GenCpp::writeCollectionObject(const FuExpr * obj, FuPriority priority)
{
	if (isCppPtr(obj)) {
		writeChar('*');
		obj->accept(this, FuPriority::primary);
	}
	else
		obj->accept(this, priority);
}

void GenCpp::writePop(const FuExpr * obj, FuPriority parent, int p, std::string_view front)
{
	if (parent == FuPriority::statement) {
		startMethodCall(obj);
		write("pop()");
	}
	else {
		const FuClassType * klass = obj->type->asClassType();
		write("[](");
		writeCollectionType(klass);
		write(" &");
		writeChar(p);
		write(") { ");
		writeType(klass->getElementType().get(), false);
		writeChar(' ');
		write(front);
		write(" = ");
		writeChar(p);
		writeChar('.');
		write(front);
		write("(); ");
		writeChar(p);
		write(".pop(); return ");
		write(front);
		write("; }(");
		writeCollectionObject(obj, FuPriority::argument);
		writeChar(')');
	}
}

void GenCpp::writeBeginEnd(const FuExpr * obj)
{
	startMethodCall(obj);
	write("begin(), ");
	startMethodCall(obj);
	write("end()");
}

void GenCpp::writePtrRange(const FuExpr * obj, const FuExpr * index, const FuExpr * count)
{
	writeArrayPtrAdd(obj, index);
	write(", ");
	writeArrayPtrAdd(obj, index);
	write(" + ");
	count->accept(this, FuPriority::mul);
}

void GenCpp::writeNotRawStringLiteral(const FuExpr * obj, FuPriority priority)
{
	obj->accept(this, priority);
	if (dynamic_cast<const FuLiteralString *>(obj)) {
		include("string_view");
		this->usingStringViewLiterals = true;
		write("sv");
	}
}

void GenCpp::writeStringMethod(const FuExpr * obj, std::string_view name, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	writeNotRawStringLiteral(obj, FuPriority::primary);
	writeChar('.');
	write(name);
	int c = getOneAscii((*args)[0].get());
	if (c >= 0) {
		writeChar('(');
		visitLiteralChar(c);
		writeChar(')');
	}
	else
		writeCoercedArgsInParentheses(method, args);
}

void GenCpp::writeAllAnyContains(std::string_view function, const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	include("algorithm");
	write("std::");
	write(function);
	writeChar('(');
	writeBeginEnd(obj);
	write(", ");
	if ((*args)[0]->type == nullptr)
		(*args)[0]->accept(this, FuPriority::argument);
	else
		writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), FuPriority::argument);
	writeChar(')');
}

void GenCpp::writeCollectionMethod(const FuExpr * obj, std::string_view name, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	startMethodCall(obj);
	write(name);
	writeChar('(');
	writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), FuPriority::argument);
	writeChar(')');
}

void GenCpp::writeRegex(const std::vector<std::shared_ptr<FuExpr>> * args, int argIndex)
{
	include("regex");
	write("std::regex(");
	(*args)[argIndex]->accept(this, FuPriority::argument);
	writeRegexOptions(args, ", std::regex::ECMAScript | ", " | ", "", "std::regex::icase", "std::regex::multiline", "std::regex::NOT_SUPPORTED_singleline");
	writeChar(')');
}

void GenCpp::writeWrite(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine)
{
	include("iostream");
	if (std::ssize(*args) == 1) {
		if (const FuInterpolatedString *interpolated = dynamic_cast<const FuInterpolatedString *>((*args)[0].get())) {
			bool uppercase = false;
			bool hex = false;
			int flt = 'G';
			for (const FuInterpolatedPart &part : interpolated->parts) {
				switch (part.format) {
				case 'E':
				case 'G':
				case 'X':
					if (!uppercase) {
						write(" << std::uppercase");
						uppercase = true;
					}
					break;
				case 'e':
				case 'g':
				case 'x':
					if (uppercase) {
						write(" << std::nouppercase");
						uppercase = false;
					}
					break;
				default:
					break;
				}
				switch (part.format) {
				case 'E':
				case 'e':
					if (flt != 'E') {
						write(" << std::scientific");
						flt = 'E';
					}
					break;
				case 'F':
				case 'f':
					if (flt != 'F') {
						write(" << std::fixed");
						flt = 'F';
					}
					break;
				case 'X':
				case 'x':
					if (!hex) {
						write(" << std::hex");
						hex = true;
					}
					break;
				default:
					if (hex) {
						write(" << std::dec");
						hex = false;
					}
					if (flt != 'G') {
						write(" << std::defaultfloat");
						flt = 'G';
					}
					break;
				}
				if (!part.prefix.empty()) {
					write(" << ");
					visitLiteralString(part.prefix);
				}
				write(" << ");
				part.argument->accept(this, FuPriority::mul);
			}
			if (uppercase)
				write(" << std::nouppercase");
			if (hex)
				write(" << std::dec");
			if (flt != 'G')
				write(" << std::defaultfloat");
			if (!interpolated->suffix.empty()) {
				write(" << ");
				if (newLine) {
					writeStringLiteralWithNewLine(interpolated->suffix);
					return;
				}
				visitLiteralString(interpolated->suffix);
			}
		}
		else {
			write(" << ");
			const FuLiteralString * literal;
			if (newLine && (literal = dynamic_cast<const FuLiteralString *>((*args)[0].get()))) {
				writeStringLiteralWithNewLine(literal->value);
				return;
			}
			else if (dynamic_cast<const FuLiteralChar *>((*args)[0].get()))
				writeCall("static_cast<int>", (*args)[0].get());
			else
				(*args)[0]->accept(this, FuPriority::mul);
		}
	}
	if (newLine)
		write(" << '\\n'");
}

void GenCpp::writeRegexArgument(const FuExpr * expr)
{
	if (dynamic_cast<const FuDynamicPtrType *>(expr->type.get()))
		expr->accept(this, FuPriority::argument);
	else {
		writeChar('*');
		expr->accept(this, FuPriority::primary);
	}
}

void GenCpp::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::none:
	case FuId::classToString:
	case FuId::listClear:
	case FuId::hashSetClear:
	case FuId::sortedSetClear:
	case FuId::dictionaryClear:
	case FuId::sortedDictionaryClear:
		if (obj != nullptr) {
			if (isReferenceTo(obj, FuId::basePtr)) {
				writeName(method->parent);
				write("::");
			}
			else {
				obj->accept(this, FuPriority::primary);
				if (method->callType == FuCallType::static_)
					write("::");
				else
					writeMemberOp(obj, nullptr);
			}
		}
		writeName(method);
		writeCoercedArgsInParentheses(method, args);
		break;
	case FuId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case FuId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case FuId::intTryParse:
	case FuId::longTryParse:
	case FuId::doubleTryParse:
		include("charconv");
		include("string_view");
		this->numberTryParse = true;
		writeCall("FuNumber_TryParse", obj, (*args)[0].get(), std::ssize(*args) == 2 ? (*args)[1].get() : nullptr);
		break;
	case FuId::stringContains:
		if (parent > FuPriority::equality)
			writeChar('(');
		writeStringMethod(obj, "find", method, args);
		write(" != std::string::npos");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::stringEndsWith:
		writeStringMethod(obj, "ends_with", method, args);
		break;
	case FuId::stringIndexOf:
		write("static_cast<ptrdiff_t>(");
		writeStringMethod(obj, "find", method, args);
		writeChar(')');
		break;
	case FuId::stringLastIndexOf:
		write("static_cast<ptrdiff_t>(");
		writeStringMethod(obj, "rfind", method, args);
		writeChar(')');
		break;
	case FuId::stringReplace:
		this->stringReplace = true;
		writeCall("FuString_Replace", obj, (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::stringStartsWith:
		writeStringMethod(obj, "starts_with", method, args);
		break;
	case FuId::stringSubstring:
		writeStringMethod(obj, "substr", method, args);
		break;
	case FuId::stringToLower:
		this->stringToLower = true;
		writeCall("FuString_ToLower", obj);
		break;
	case FuId::stringToUpper:
		this->stringToUpper = true;
		writeCall("FuString_ToUpper", obj);
		break;
	case FuId::arrayBinarySearchAll:
	case FuId::arrayBinarySearchPart:
		include("algorithm");
		if (parent > FuPriority::add)
			writeChar('(');
		write("std::lower_bound(");
		if (std::ssize(*args) == 1)
			writeBeginEnd(obj);
		else
			writePtrRange(obj, (*args)[1].get(), (*args)[2].get());
		write(", ");
		(*args)[0]->accept(this, FuPriority::argument);
		write(") - ");
		writeArrayPtr(obj, FuPriority::mul);
		if (parent > FuPriority::add)
			writeChar(')');
		break;
	case FuId::arrayContains:
	case FuId::listContains:
		if (parent > FuPriority::equality)
			writeChar('(');
		writeAllAnyContains("find", obj, args);
		write(" != ");
		startMethodCall(obj);
		write("end()");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::arrayCopyTo:
	case FuId::listCopyTo:
		include("algorithm");
		write("std::copy_n(");
		writeArrayPtrAdd(obj, (*args)[0].get());
		write(", ");
		(*args)[3]->accept(this, FuPriority::argument);
		write(", ");
		writeArrayPtrAdd((*args)[1].get(), (*args)[2].get());
		writeChar(')');
		break;
	case FuId::arrayFillAll:
		writeCollectionMethod(obj, "fill", args);
		break;
	case FuId::arrayFillPart:
		include("algorithm");
		write("std::fill_n(");
		writeArrayPtrAdd(obj, (*args)[1].get());
		write(", ");
		(*args)[2]->accept(this, FuPriority::argument);
		write(", ");
		writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), FuPriority::argument);
		writeChar(')');
		break;
	case FuId::arraySortAll:
	case FuId::listSortAll:
		include("algorithm");
		write("std::sort(");
		writeBeginEnd(obj);
		writeChar(')');
		break;
	case FuId::arraySortPart:
	case FuId::listSortPart:
		include("algorithm");
		write("std::sort(");
		writePtrRange(obj, (*args)[0].get(), (*args)[1].get());
		writeChar(')');
		break;
	case FuId::listAdd:
		if (std::ssize(*args) == 0) {
			startMethodCall(obj);
			write("emplace_back()");
		}
		else
			writeCollectionMethod(obj, "push_back", args);
		break;
	case FuId::listAddRange:
		startMethodCall(obj);
		write("insert(");
		startMethodCall(obj);
		write("end(), ");
		writeBeginEnd((*args)[0].get());
		writeChar(')');
		break;
	case FuId::listAll:
		writeAllAnyContains("all_of", obj, args);
		break;
	case FuId::listAny:
		include("algorithm");
		writeAllAnyContains("any_of", obj, args);
		break;
	case FuId::listIndexOf:
		{
			const FuClassType * klass = obj->type->asClassType();
			write("[](const ");
			writeCollectionType(klass);
			write(" &v, ");
			writeType(klass->getElementType().get(), false);
			include("algorithm");
			write(" value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(");
			writeCollectionObject(obj, FuPriority::argument);
			write(", ");
			writeCoerced(klass->getElementType().get(), (*args)[0].get(), FuPriority::argument);
			writeChar(')');
		}
		break;
	case FuId::listInsert:
		startMethodCall(obj);
		if (std::ssize(*args) == 1) {
			write("emplace(");
			writeArrayPtrAdd(obj, (*args)[0].get());
		}
		else {
			write("insert(");
			writeArrayPtrAdd(obj, (*args)[0].get());
			write(", ");
			writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[1].get(), FuPriority::argument);
		}
		writeChar(')');
		break;
	case FuId::listLast:
		startMethodCall(obj);
		write("back()");
		break;
	case FuId::listRemoveAt:
		startMethodCall(obj);
		write("erase(");
		writeArrayPtrAdd(obj, (*args)[0].get());
		writeChar(')');
		break;
	case FuId::listRemoveRange:
		startMethodCall(obj);
		write("erase(");
		writePtrRange(obj, (*args)[0].get(), (*args)[1].get());
		writeChar(')');
		break;
	case FuId::queueClear:
	case FuId::stackClear:
	case FuId::priorityQueueClear:
		writeCollectionObject(obj, FuPriority::assign);
		write(" = {}");
		break;
	case FuId::queueDequeue:
		writePop(obj, parent, 'q', "front");
		break;
	case FuId::queueEnqueue:
	case FuId::priorityQueueEnqueue:
		writeMethodCall(obj, "push", (*args)[0].get());
		break;
	case FuId::queuePeek:
		startMethodCall(obj);
		write("front()");
		break;
	case FuId::stackPeek:
	case FuId::priorityQueuePeek:
		startMethodCall(obj);
		write("top()");
		break;
	case FuId::stackPop:
		writePop(obj, parent, 's', "top");
		break;
	case FuId::stackPush:
		writeCollectionMethod(obj, "push", args);
		break;
	case FuId::priorityQueueDequeue:
		writePop(obj, parent, 'q', "top");
		break;
	case FuId::hashSetAdd:
	case FuId::sortedSetAdd:
		writeCollectionMethod(obj, obj->type->asClassType()->getElementType()->id == FuId::stringStorageType && (*args)[0]->type->id == FuId::stringPtrType ? "emplace" : "insert", args);
		break;
	case FuId::hashSetContains:
	case FuId::sortedSetContains:
		writeCollectionMethod(obj, "contains", args);
		break;
	case FuId::hashSetRemove:
	case FuId::sortedSetRemove:
	case FuId::dictionaryRemove:
	case FuId::sortedDictionaryRemove:
		writeMethodCall(obj, "erase", (*args)[0].get());
		break;
	case FuId::dictionaryAdd:
		writeIndexing(obj, (*args)[0].get());
		break;
	case FuId::dictionaryContainsKey:
	case FuId::sortedDictionaryContainsKey:
		if (parent > FuPriority::equality)
			writeChar('(');
		startMethodCall(obj);
		write("count(");
		writeStronglyCoerced(obj->type->asClassType()->getKeyType(), (*args)[0].get());
		write(") != 0");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::textWriterWrite:
		writeCollectionObject(obj, FuPriority::shift);
		writeWrite(args, false);
		break;
	case FuId::textWriterWriteChar:
		writeCollectionObject(obj, FuPriority::shift);
		write(" << ");
		{
			const FuLiteralChar * literalChar;
			if ((literalChar = dynamic_cast<const FuLiteralChar *>((*args)[0].get())) && literalChar->value < 127)
				(*args)[0]->accept(this, FuPriority::mul);
			else
				writeCall("static_cast<char>", (*args)[0].get());
			break;
		}
	case FuId::textWriterWriteCodePoint:
		{
			const FuLiteralChar * literalChar2;
			if ((literalChar2 = dynamic_cast<const FuLiteralChar *>((*args)[0].get())) && literalChar2->value < 127) {
				writeCollectionObject(obj, FuPriority::shift);
				write(" << ");
				(*args)[0]->accept(this, FuPriority::mul);
			}
			else {
				write("if (");
				(*args)[0]->accept(this, FuPriority::rel);
				writeLine(" < 0x80)");
				writeChar('\t');
				writeCollectionObject(obj, FuPriority::shift);
				write(" << ");
				writeCall("static_cast<char>", (*args)[0].get());
				writeCharLine(';');
				write("else if (");
				(*args)[0]->accept(this, FuPriority::rel);
				writeLine(" < 0x800)");
				writeChar('\t');
				writeCollectionObject(obj, FuPriority::shift);
				write(" << static_cast<char>(0xc0 | ");
				(*args)[0]->accept(this, FuPriority::shift);
				write(" >> 6) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, FuPriority::and_);
				writeLine(" & 0x3f));");
				write("else if (");
				(*args)[0]->accept(this, FuPriority::rel);
				writeLine(" < 0x10000)");
				writeChar('\t');
				writeCollectionObject(obj, FuPriority::shift);
				write(" << static_cast<char>(0xe0 | ");
				(*args)[0]->accept(this, FuPriority::shift);
				write(" >> 12) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, FuPriority::shift);
				write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, FuPriority::and_);
				writeLine(" & 0x3f));");
				writeLine("else");
				writeChar('\t');
				writeCollectionObject(obj, FuPriority::shift);
				write(" << static_cast<char>(0xf0 | ");
				(*args)[0]->accept(this, FuPriority::shift);
				write(" >> 18) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, FuPriority::shift);
				write(" >> 12 & 0x3f)) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, FuPriority::shift);
				write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, FuPriority::and_);
				write(" & 0x3f))");
			}
			break;
		}
	case FuId::textWriterWriteLine:
		writeCollectionObject(obj, FuPriority::shift);
		writeWrite(args, true);
		break;
	case FuId::stringWriterClear:
		include("string");
		startMethodCall(obj);
		write("str(std::string())");
		break;
	case FuId::consoleWrite:
		write("std::cout");
		writeWrite(args, false);
		break;
	case FuId::consoleWriteLine:
		write("std::cout");
		writeWrite(args, true);
		break;
	case FuId::stringWriterToString:
		startMethodCall(obj);
		write("str()");
		break;
	case FuId::uTF8GetByteCount:
		if (dynamic_cast<const FuLiteral *>((*args)[0].get())) {
			if (parent > FuPriority::add)
				writeChar('(');
			write("sizeof(");
			(*args)[0]->accept(this, FuPriority::argument);
			write(") - 1");
			if (parent > FuPriority::add)
				writeChar(')');
		}
		else
			writeStringLength((*args)[0].get());
		break;
	case FuId::uTF8GetBytes:
		if (dynamic_cast<const FuLiteral *>((*args)[0].get())) {
			include("algorithm");
			write("std::copy_n(");
			(*args)[0]->accept(this, FuPriority::argument);
			write(", sizeof(");
			(*args)[0]->accept(this, FuPriority::argument);
			write(") - 1, ");
			writeArrayPtrAdd((*args)[1].get(), (*args)[2].get());
			writeChar(')');
		}
		else {
			writePostfix((*args)[0].get(), ".copy(reinterpret_cast<char *>(");
			writeArrayPtrAdd((*args)[1].get(), (*args)[2].get());
			write("), ");
			writePostfix((*args)[0].get(), ".size())");
		}
		break;
	case FuId::uTF8GetString:
		include("string_view");
		write("std::string_view(reinterpret_cast<const char *>(");
		writeArrayPtrAdd((*args)[0].get(), (*args)[1].get());
		write("), ");
		(*args)[2]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::environmentGetEnvironmentVariable:
		include("cstdlib");
		write("std::getenv(");
		if ((*args)[0]->type->id == FuId::stringStorageType)
			writePostfix((*args)[0].get(), ".c_str()");
		else if (dynamic_cast<const FuLiteralString *>((*args)[0].get()))
			(*args)[0]->accept(this, FuPriority::argument);
		else {
			include("string");
			write("std::string(");
			(*args)[0]->accept(this, FuPriority::argument);
			write(").c_str()");
		}
		writeChar(')');
		break;
	case FuId::regexCompile:
		writeRegex(args, 0);
		break;
	case FuId::regexIsMatchStr:
	case FuId::regexIsMatchRegex:
	case FuId::matchFindStr:
	case FuId::matchFindRegex:
		write("std::regex_search(");
		if ((*args)[0]->type->id == FuId::stringStorageType)
			writePostfix((*args)[0].get(), ".c_str()");
		else if ((*args)[0]->type->id == FuId::stringPtrType && !dynamic_cast<const FuLiteral *>((*args)[0].get()))
			writeBeginEnd((*args)[0].get());
		else
			(*args)[0]->accept(this, FuPriority::argument);
		if (method->id == FuId::matchFindStr || method->id == FuId::matchFindRegex) {
			write(", ");
			obj->accept(this, FuPriority::argument);
		}
		write(", ");
		if (method->id == FuId::regexIsMatchRegex)
			writeRegexArgument(obj);
		else if (method->id == FuId::matchFindRegex)
			writeRegexArgument((*args)[1].get());
		else
			writeRegex(args, 1);
		writeChar(')');
		break;
	case FuId::matchGetCapture:
		startMethodCall(obj);
		writeCall("str", (*args)[0].get());
		break;
	case FuId::mathMethod:
	case FuId::mathAbs:
	case FuId::mathIsFinite:
	case FuId::mathIsNaN:
	case FuId::mathLog2:
	case FuId::mathRound:
		includeMath();
		write("std::");
		writeLowercase(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathCeiling:
		includeMath();
		writeCall("std::ceil", (*args)[0].get());
		break;
	case FuId::mathClamp:
		include("algorithm");
		writeCall("std::clamp", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case FuId::mathFusedMultiplyAdd:
		includeMath();
		writeCall("std::fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case FuId::mathIsInfinity:
		includeMath();
		writeCall("std::isinf", (*args)[0].get());
		break;
	case FuId::mathMax:
		include("algorithm");
		writeCall("(std::max)", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::mathMin:
		include("algorithm");
		writeCall("(std::min)", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::mathTruncate:
		includeMath();
		writeCall("std::trunc", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenCpp::writeResource(std::string_view name, int length)
{
	write("FuResource::");
	writeResourceName(name);
}

void GenCpp::writeArrayPtr(const FuExpr * expr, FuPriority parent)
{
	const FuClassType * klass;
	if (dynamic_cast<const FuArrayStorageType *>(expr->type.get()) || dynamic_cast<const FuStringType *>(expr->type.get()))
		writePostfix(expr, ".data()");
	else if (dynamic_cast<const FuDynamicPtrType *>(expr->type.get()))
		writePostfix(expr, ".get()");
	else if ((klass = dynamic_cast<const FuClassType *>(expr->type.get())) && klass->class_->id == FuId::listClass) {
		startMethodCall(expr);
		write("begin()");
	}
	else
		expr->accept(this, parent);
}

void GenCpp::writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	if (dynamic_cast<const FuStorageType *>(type)) {
	}
	else if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(type)) {
		const FuPrefixExpr * prefix;
		if (dynamic->unique && (prefix = dynamic_cast<const FuPrefixExpr *>(expr))) {
			assert(prefix->op == FuToken::new_);
			const FuDynamicPtrType * newClass = static_cast<const FuDynamicPtrType *>(prefix->type.get());
			if (newClass->class_->id == FuId::arrayPtrClass)
				writeNewUniqueArray(true, newClass->getElementType().get(), prefix->inner.get());
			else
				writeNewUnique(true, newClass);
			return;
		}
	}
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		if (klass->class_->id == FuId::stringClass) {
			if (expr->type->id == FuId::nullType) {
				include("string_view");
				write("std::string_view()");
			}
			else
				expr->accept(this, parent);
			return;
		}
		if (klass->class_->id == FuId::arrayPtrClass) {
			writeArrayPtr(expr, parent);
			return;
		}
		if (isSharedPtr(expr)) {
			if (klass->class_->id == FuId::regexClass) {
				writeChar('&');
				expr->accept(this, FuPriority::primary);
			}
			else
				writePostfix(expr, ".get()");
			return;
		}
		if (dynamic_cast<const FuClassType *>(expr->type.get()) && !isCppPtr(expr)) {
			writeChar('&');
			if (dynamic_cast<const FuCallExpr *>(expr)) {
				write("static_cast<");
				if (!dynamic_cast<const FuReadWriteClassType *>(klass))
					write("const ");
				writeName(klass->class_);
				writeCall(" &>", expr);
			}
			else
				expr->accept(this, FuPriority::primary);
			return;
		}
	}
	GenTyped::writeCoercedInternal(type, expr, parent);
}

void GenCpp::writeSelectValues(const FuType * type, const FuSelectExpr * expr)
{
	const FuClassType * trueClass;
	const FuClassType * falseClass;
	if ((trueClass = dynamic_cast<const FuClassType *>(expr->onTrue->type.get())) && (falseClass = dynamic_cast<const FuClassType *>(expr->onFalse->type.get())) && !trueClass->class_->isSameOrBaseOf(falseClass->class_) && !falseClass->class_->isSameOrBaseOf(trueClass->class_)) {
		writeStaticCast(type, expr->onTrue.get());
		write(" : ");
		writeStaticCast(type, expr->onFalse.get());
	}
	else
		GenBase::writeSelectValues(type, expr);
}

void GenCpp::writeStringLength(const FuExpr * expr)
{
	write("std::ssize(");
	writeNotRawStringLiteral(expr, FuPriority::argument);
	writeChar(')');
}

void GenCpp::writeMatchProperty(const FuSymbolReference * expr, std::string_view name)
{
	startMethodCall(expr->left.get());
	write(name);
	write("()");
}

void GenCpp::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::consoleError:
		write("std::cerr");
		break;
	case FuId::listCount:
	case FuId::queueCount:
	case FuId::stackCount:
	case FuId::priorityQueueCount:
	case FuId::hashSetCount:
	case FuId::sortedSetCount:
	case FuId::dictionaryCount:
	case FuId::sortedDictionaryCount:
	case FuId::orderedDictionaryCount:
		write("std::ssize(");
		writeCollectionObject(expr->left.get(), FuPriority::argument);
		writeChar(')');
		break;
	case FuId::matchStart:
		writeMatchProperty(expr, "position");
		break;
	case FuId::matchEnd:
		if (parent > FuPriority::add)
			writeChar('(');
		writeMatchProperty(expr, "position");
		write(" + ");
		writeMatchProperty(expr, "length");
		if (parent > FuPriority::add)
			writeChar(')');
		break;
	case FuId::matchLength:
		writeMatchProperty(expr, "length");
		break;
	case FuId::matchValue:
		writeMatchProperty(expr, "str");
		break;
	default:
		GenCCpp::visitSymbolReference(expr, parent);
		break;
	}
}

void GenCpp::writeGtRawPtr(const FuExpr * expr)
{
	write(">(");
	if (isSharedPtr(expr))
		writePostfix(expr, ".get()");
	else
		expr->accept(this, FuPriority::argument);
	writeChar(')');
}

void GenCpp::writeIsVar(const FuExpr * expr, const FuVar * def, FuPriority parent)
{
	if (parent > FuPriority::assign)
		writeChar('(');
	writeName(def);
	write(" = ");
	if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(def->type.get())) {
		write("std::dynamic_pointer_cast<");
		write(dynamic->class_->name);
		writeCall(">", expr);
	}
	else {
		write("dynamic_cast<");
		writeType(def->type.get(), true);
		writeGtRawPtr(expr);
	}
	if (parent > FuPriority::assign)
		writeChar(')');
}

void GenCpp::visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::plus:
		if (expr->type->id == FuId::stringStorageType) {
			if (parent > FuPriority::add)
				writeChar('(');
			writeStronglyCoerced(expr->type.get(), expr->left.get());
			write(" + ");
			writeStronglyCoerced(expr->type.get(), expr->right.get());
			if (parent > FuPriority::add)
				writeChar(')');
			return;
		}
		break;
	case FuToken::equal:
	case FuToken::notEqual:
	case FuToken::greater:
		{
			const FuExpr * str = isStringEmpty(expr);
			if (str != nullptr) {
				if (expr->op != FuToken::equal)
					writeChar('!');
				writePostfix(str, ".empty()");
				return;
			}
			break;
		}
	case FuToken::assign:
		{
			const FuExpr * length = isTrimSubstring(expr);
			if (length != nullptr && expr->left->type->id == FuId::stringStorageType && parent == FuPriority::statement) {
				writeMethodCall(expr->left.get(), "resize", length);
				return;
			}
			break;
		}
	case FuToken::is:
		if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr->right.get())) {
			if (parent == FuPriority::select || (parent >= FuPriority::or_ && parent <= FuPriority::mul))
				write("!!");
			write("dynamic_cast<const ");
			write(symbol->symbol->name);
			write(" *");
			writeGtRawPtr(expr->left.get());
			return;
		}
		else if (const FuVar *def = dynamic_cast<const FuVar *>(expr->right.get())) {
			writeIsVar(expr->left.get(), def, parent);
			return;
		}
		else
			std::abort();
	default:
		break;
	}
	GenBase::visitBinaryExpr(expr, parent);
}

bool GenCpp::hasLambdaCapture(const FuExpr * expr, const FuLambdaExpr * lambda)
{
	if (const FuAggregateInitializer *init = dynamic_cast<const FuAggregateInitializer *>(expr))
		return std::any_of(init->items.begin(), init->items.end(), [&](const std::shared_ptr<FuExpr> &item) { return hasLambdaCapture(item.get(), lambda); });
	else if (dynamic_cast<const FuLiteral *>(expr))
		return false;
	else if (const FuInterpolatedString *interp = dynamic_cast<const FuInterpolatedString *>(expr))
		return std::any_of(interp->parts.begin(), interp->parts.end(), [&](const FuInterpolatedPart &part) { return hasLambdaCapture(part.argument.get(), lambda); });
	else if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr)) {
		if (symbol->left != nullptr)
			return hasLambdaCapture(symbol->left.get(), lambda);
		if (const FuMember *member = dynamic_cast<const FuMember *>(symbol->symbol))
			return !member->isStatic();
		return dynamic_cast<const FuVar *>(symbol->symbol) && !lambda->encloses(symbol->symbol);
	}
	else if (const FuUnaryExpr *unary = dynamic_cast<const FuUnaryExpr *>(expr))
		return unary->inner != nullptr && hasLambdaCapture(unary->inner.get(), lambda);
	else if (const FuBinaryExpr *binary = dynamic_cast<const FuBinaryExpr *>(expr)) {
		if (hasLambdaCapture(binary->left.get(), lambda))
			return true;
		return binary->op != FuToken::is && hasLambdaCapture(binary->right.get(), lambda);
	}
	else if (const FuSelectExpr *select = dynamic_cast<const FuSelectExpr *>(expr))
		return hasLambdaCapture(select->cond.get(), lambda) || hasLambdaCapture(select->onTrue.get(), lambda) || hasLambdaCapture(select->onFalse.get(), lambda);
	else if (const FuCallExpr *call = dynamic_cast<const FuCallExpr *>(expr))
		return hasLambdaCapture(call->method.get(), lambda) || std::any_of(call->arguments.begin(), call->arguments.end(), [&](const std::shared_ptr<FuExpr> &arg) { return hasLambdaCapture(arg.get(), lambda); });
	else if (const FuLambdaExpr *inner = dynamic_cast<const FuLambdaExpr *>(expr))
		return hasLambdaCapture(inner->body.get(), lambda);
	else
		std::abort();
}

void GenCpp::visitLambdaExpr(const FuLambdaExpr * expr)
{
	writeChar('[');
	if (hasLambdaCapture(expr->body.get(), expr))
		writeChar('&');
	write("](");
	if (dynamic_cast<const FuOwningType *>(expr->first->type.get()) || expr->first->type->id == FuId::stringStorageType) {
		write("const ");
		writeType(expr->first->type.get(), false);
		write(" &");
	}
	else {
		writeType(expr->first->type.get(), false);
		writeChar(' ');
	}
	writeName(expr->first);
	write(") { ");
	writeTemporaries(expr->body.get());
	write("return ");
	expr->body->accept(this, FuPriority::argument);
	write("; }");
}

void GenCpp::writeUnreachable(const FuAssert * statement)
{
	include("cstdlib");
	write("std::");
	GenCCpp::writeUnreachable(statement);
}

void GenCpp::writeConst(const FuConst * konst)
{
	write("static constexpr ");
	writeTypeAndName(konst);
	write(" = ");
	konst->value->accept(this, FuPriority::argument);
	writeCharLine(';');
}

void GenCpp::visitForeach(const FuForeach * statement)
{
	const FuVar * element = statement->getVar();
	write("for (");
	const FuClassType * collectionType = static_cast<const FuClassType *>(statement->collection->type.get());
	if (collectionType->class_->id == FuId::stringClass) {
		writeTypeAndName(element);
		write(" : ");
		writeNotRawStringLiteral(statement->collection.get(), FuPriority::argument);
	}
	else {
		if (statement->count() == 2) {
			write("const auto &[");
			writeCamelCaseNotKeyword(element->name);
			write(", ");
			writeCamelCaseNotKeyword(statement->getValueVar()->name);
			writeChar(']');
		}
		else {
			if (const FuStorageType *storage = dynamic_cast<const FuStorageType *>(collectionType->getElementType().get())) {
				if (!dynamic_cast<const FuReadWriteClassType *>(element->type.get()))
					write("const ");
				write(storage->class_->name);
				write(" &");
				writeCamelCaseNotKeyword(element->name);
			}
			else if (const FuDynamicPtrType *dynamic = dynamic_cast<const FuDynamicPtrType *>(collectionType->getElementType().get())) {
				write("const ");
				writeType(dynamic, true);
				write(" &");
				writeCamelCaseNotKeyword(element->name);
			}
			else
				writeTypeAndName(element);
		}
		write(" : ");
		if (collectionType->id == FuId::mainArgsType) {
			include("span");
			write("std::span(argv + 1, argc - 1)");
		}
		else
			writeCollectionObject(statement->collection.get(), FuPriority::argument);
	}
	writeChar(')');
	writeChild(statement->body.get());
}

bool GenCpp::embedIfWhileIsVar(const FuExpr * expr, bool write)
{
	const FuBinaryExpr * binary;
	const FuVar * def;
	if ((binary = dynamic_cast<const FuBinaryExpr *>(expr)) && binary->op == FuToken::is && (def = dynamic_cast<const FuVar *>(binary->right.get()))) {
		if (write)
			writeType(def->type.get(), true);
		return true;
	}
	return false;
}

void GenCpp::visitLock(const FuLock * statement)
{
	openBlock();
	write("const std::lock_guard<std::recursive_mutex> lock(");
	statement->lock->accept(this, FuPriority::argument);
	writeLine(");");
	flattenBlock(statement->body.get());
	closeBlock();
}

void GenCpp::writeStronglyCoerced(const FuType * type, const FuExpr * expr)
{
	if (type->id == FuId::stringStorageType && expr->type->id == FuId::stringPtrType && !dynamic_cast<const FuLiteral *>(expr)) {
		writeCall("std::string", expr);
	}
	else {
		const FuCallExpr * call = isStringSubstring(expr);
		if (call != nullptr && type->id == FuId::stringStorageType && (isUTF8GetString(call) ? call->arguments[0] : call->method->left)->type->id != FuId::stringStorageType) {
			write("std::string(");
			if (isUTF8GetString(call)) {
				write("reinterpret_cast<const char *>(");
				writeArrayPtrAdd(call->arguments[0].get(), call->arguments[1].get());
				write("), ");
				call->arguments[2]->accept(this, FuPriority::argument);
			}
			else {
				writeArrayPtrAdd(call->method->left.get(), call->arguments[0].get());
				write(", ");
				call->arguments[1]->accept(this, FuPriority::argument);
			}
			writeChar(')');
		}
		else
			GenBase::writeStronglyCoerced(type, expr);
	}
}

void GenCpp::writeSwitchCaseCond(const FuSwitch * statement, const FuExpr * value, FuPriority parent)
{
	const FuSymbolReference * symbol;
	if ((symbol = dynamic_cast<const FuSymbolReference *>(value)) && dynamic_cast<const FuClass *>(symbol->symbol)) {
		write("dynamic_cast<const ");
		write(symbol->symbol->name);
		write(" *");
		writeGtRawPtr(statement->value.get());
	}
	else if (const FuVar *def = dynamic_cast<const FuVar *>(value)) {
		if (parent == FuPriority::argument)
			writeType(def->type.get(), true);
		writeIsVar(statement->value.get(), def, parent);
	}
	else
		GenBase::writeSwitchCaseCond(statement, value, parent);
}

bool GenCpp::isIsVar(const FuExpr * expr)
{
	const FuBinaryExpr * binary;
	return (binary = dynamic_cast<const FuBinaryExpr *>(expr)) && binary->op == FuToken::is && dynamic_cast<const FuVar *>(binary->right.get());
}

bool GenCpp::hasVariables(const FuStatement * statement) const
{
	if (dynamic_cast<const FuVar *>(statement))
		return true;
	else if (const FuAssert *asrt = dynamic_cast<const FuAssert *>(statement))
		return isIsVar(asrt->cond.get());
	else if (dynamic_cast<const FuBlock *>(statement) || dynamic_cast<const FuBreak *>(statement) || dynamic_cast<const FuConst *>(statement) || dynamic_cast<const FuContinue *>(statement) || dynamic_cast<const FuLock *>(statement) || dynamic_cast<const FuNative *>(statement) || dynamic_cast<const FuThrow *>(statement))
		return false;
	else if (const FuIf *ifStatement = dynamic_cast<const FuIf *>(statement))
		return hasTemporaries(ifStatement->cond.get()) && !isIsVar(ifStatement->cond.get());
	else if (const FuLoop *loop = dynamic_cast<const FuLoop *>(statement))
		return loop->cond != nullptr && hasTemporaries(loop->cond.get());
	else if (const FuReturn *ret = dynamic_cast<const FuReturn *>(statement))
		return ret->value != nullptr && hasTemporaries(ret->value.get());
	else if (const FuSwitch *switch_ = dynamic_cast<const FuSwitch *>(statement))
		return hasTemporaries(switch_->value.get());
	else if (const FuExpr *expr = dynamic_cast<const FuExpr *>(statement))
		return hasTemporaries(expr);
	else
		std::abort();
}

void GenCpp::writeSwitchCaseBody(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	bool block = false;
	for (const std::shared_ptr<FuStatement> &statement : *statements) {
		if (!block && hasVariables(statement.get())) {
			openBlock();
			block = true;
		}
		statement->acceptStatement(this);
	}
	if (block)
		closeBlock();
}

void GenCpp::visitSwitch(const FuSwitch * statement)
{
	if (statement->isTypeMatching())
		writeSwitchAsIfsWithGoto(statement);
	else
		GenCCpp::visitSwitch(statement);
}

void GenCpp::writeException()
{
	include("stdexcept");
	write("std::runtime_error");
}

void GenCpp::visitThrow(const FuThrow * statement)
{
	write("throw ");
	writeThrowArgument(statement);
	writeCharLine(';');
}

void GenCpp::openNamespace()
{
	if (this->namespace_.empty())
		return;
	writeNewLine();
	write("namespace ");
	writeLine(this->namespace_);
	writeCharLine('{');
}

void GenCpp::closeNamespace()
{
	if (!this->namespace_.empty())
		writeCharLine('}');
}

void GenCpp::writeEnum(const FuEnum * enu)
{
	writeNewLine();
	writeDoc(enu->documentation.get());
	write("enum class ");
	writeLine(enu->name);
	openBlock();
	enu->acceptValues(this);
	writeNewLine();
	this->indent--;
	writeLine("};");
	if (dynamic_cast<const FuEnumFlags *>(enu)) {
		include("type_traits");
		this->hasEnumFlags = true;
		write("FU_ENUM_FLAG_OPERATORS(");
		write(enu->name);
		writeCharLine(')');
	}
}

FuVisibility GenCpp::getConstructorVisibility(const FuClass * klass)
{
	switch (klass->callType) {
	case FuCallType::static_:
		return FuVisibility::private_;
	case FuCallType::abstract:
		return FuVisibility::protected_;
	default:
		return FuVisibility::public_;
	}
}

bool GenCpp::hasMembersOfVisibility(const FuClass * klass, FuVisibility visibility)
{
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const FuMember * member;
		if ((member = dynamic_cast<const FuMember *>(symbol)) && member->visibility == visibility)
			return true;
	}
	return false;
}

void GenCpp::writeField(const FuField * field)
{
	writeDoc(field->documentation.get());
	writeVar(field);
	writeCharLine(';');
}

void GenCpp::writeParametersAndConst(const FuMethod * method, bool defaultArguments)
{
	writeParameters(method, defaultArguments);
	if (method->callType != FuCallType::static_ && !method->isMutator())
		write(" const");
}

void GenCpp::writeDeclarations(const FuClass * klass, FuVisibility visibility, std::string_view visibilityKeyword)
{
	bool constructor = getConstructorVisibility(klass) == visibility;
	bool destructor = visibility == FuVisibility::public_ && (klass->hasSubclasses || klass->addsVirtualMethods());
	bool trailingNative = visibility == FuVisibility::private_ && dynamic_cast<const FuNative *>(klass->last);
	if (!constructor && !destructor && !trailingNative && !hasMembersOfVisibility(klass, visibility))
		return;
	write(visibilityKeyword);
	writeCharLine(':');
	this->indent++;
	if (constructor) {
		if (klass->id == FuId::exceptionClass) {
			write("using ");
			if (klass->baseClass.name == "Exception")
				write("std::runtime_error::runtime_error");
			else {
				write(klass->baseClass.name);
				write("::");
				write(klass->baseClass.name);
			}
		}
		else {
			if (klass->constructor != nullptr)
				writeDoc(klass->constructor->documentation.get());
			write(klass->name);
			write("()");
			if (klass->callType == FuCallType::static_)
				write(" = delete");
			else if (!needsConstructor(klass))
				write(" = default");
		}
		writeCharLine(';');
	}
	if (destructor) {
		write("virtual ~");
		write(klass->name);
		writeLine("() = default;");
	}
	for (const FuSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const FuConst *konst = dynamic_cast<const FuConst *>(symbol)) {
			if (konst->visibility != visibility)
				continue;
			writeDoc(konst->documentation.get());
			writeConst(konst);
		}
		else if (const FuField *field = dynamic_cast<const FuField *>(symbol)) {
			if (field->visibility == visibility)
				writeField(field);
		}
		else if (const FuMethod *method = dynamic_cast<const FuMethod *>(symbol)) {
			if (method->visibility != visibility || method->id == FuId::main)
				continue;
			writeMethodDoc(method);
			switch (method->callType) {
			case FuCallType::static_:
				write("static ");
				break;
			case FuCallType::abstract:
			case FuCallType::virtual_:
				write("virtual ");
				break;
			default:
				break;
			}
			writeTypeAndName(method);
			writeParametersAndConst(method, true);
			switch (method->callType) {
			case FuCallType::abstract:
				write(" = 0");
				break;
			case FuCallType::override_:
				write(" override");
				break;
			case FuCallType::sealed:
				write(" final");
				break;
			default:
				break;
			}
			writeCharLine(';');
		}
		else if (const FuNative *nat = dynamic_cast<const FuNative *>(symbol)) {
			const FuMember * followingMember = nat->getFollowingMember();
			if (visibility == (followingMember != nullptr ? followingMember->visibility : FuVisibility::private_))
				visitNative(nat);
		}
		else
			std::abort();
	}
	this->indent--;
}

void GenCpp::writeClassInternal(const FuClass * klass)
{
	writeNewLine();
	writeDoc(klass->documentation.get());
	openClass(klass, klass->callType == FuCallType::sealed ? " final" : "", " : public ");
	this->indent--;
	writeDeclarations(klass, FuVisibility::public_, "public");
	writeDeclarations(klass, FuVisibility::protected_, "protected");
	writeDeclarations(klass, FuVisibility::internal, "public");
	writeDeclarations(klass, FuVisibility::private_, "private");
	writeLine("};");
}

void GenCpp::writeConstructor(const FuClass * klass)
{
	if (!needsConstructor(klass))
		return;
	write(klass->name);
	write("::");
	write(klass->name);
	writeLine("()");
	openBlock();
	writeConstructorBody(klass);
	closeBlock();
}

void GenCpp::writeMethod(const FuMethod * method)
{
	if (method->callType == FuCallType::abstract)
		return;
	writeNewLine();
	if (method->id == FuId::main) {
		write("int main(");
		if (method->parameters.count() == 1)
			write("int argc, char **argv");
		writeChar(')');
	}
	else {
		writeType(method->type.get(), true);
		writeChar(' ');
		write(method->parent->name);
		write("::");
		writeCamelCaseNotKeyword(method->name);
		writeParametersAndConst(method, false);
	}
	writeBody(method);
}

void GenCpp::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources, bool define)
{
	if (std::ssize(*resources) == 0)
		return;
	writeNewLine();
	writeLine("namespace");
	openBlock();
	writeLine("namespace FuResource");
	openBlock();
	for (const auto &[name, content] : *resources) {
		if (!define)
			write("extern ");
		include("array");
		include("cstdint");
		write("const std::array<uint8_t, ");
		visitLiteralLong(std::ssize(content));
		write("> ");
		writeResourceName(name);
		if (define) {
			writeLine(" = {");
			writeChar('\t');
			writeBytes(&content);
			write(" }");
		}
		writeCharLine(';');
	}
	closeBlock();
	closeBlock();
}

void GenCpp::writeProgram(const FuProgram * program)
{
	this->writtenClasses.clear();
	this->inHeaderFile = true;
	this->usingStringViewLiterals = false;
	this->hasEnumFlags = false;
	this->numberTryParse = false;
	this->stringReplace = false;
	this->stringToLower = false;
	this->stringToUpper = false;
	openStringWriter();
	openNamespace();
	writeRegexOptionsEnum(program);
	for (const FuSymbol * type = program->first; type != nullptr; type = type->next) {
		if (const FuEnum *enu = dynamic_cast<const FuEnum *>(type))
			writeEnum(enu);
		else {
			write("class ");
			write(type->name);
			writeCharLine(';');
		}
	}
	for (const FuClass * klass : program->classes)
		writeClass(klass, program);
	closeNamespace();
	createHeaderFile(".hpp");
	if (this->hasEnumFlags) {
		writeLine("#define FU_ENUM_FLAG_OPERATORS(T) \\");
		writeLine("\tinline constexpr T operator~(T a) { return static_cast<T>(~static_cast<std::underlying_type_t<T>>(a)); } \\");
		writeLine("\tinline constexpr T operator&(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) & static_cast<std::underlying_type_t<T>>(b)); } \\");
		writeLine("\tinline constexpr T operator|(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) | static_cast<std::underlying_type_t<T>>(b)); } \\");
		writeLine("\tinline constexpr T operator^(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) ^ static_cast<std::underlying_type_t<T>>(b)); } \\");
		writeLine("\tinline constexpr T &operator&=(T &a, T b) { return (a = a & b); } \\");
		writeLine("\tinline constexpr T &operator|=(T &a, T b) { return (a = a | b); } \\");
		writeLine("\tinline constexpr T &operator^=(T &a, T b) { return (a = a ^ b); }");
	}
	closeStringWriter();
	closeFile();
	this->inHeaderFile = false;
	openStringWriter();
	writeResources(&program->resources, false);
	openNamespace();
	for (const FuClass * klass : program->classes) {
		writeConstructor(klass);
		writeMethods(klass);
	}
	writeResources(&program->resources, true);
	closeNamespace();
	if (this->stringReplace) {
		include("string");
		include("string_view");
	}
	createImplementationFile(program, ".hpp");
	if (this->usingStringViewLiterals)
		writeLine("using namespace std::string_view_literals;");
	if (this->numberTryParse) {
		writeNewLine();
		writeLine("template <class T, class... Args>");
		writeLine("bool FuNumber_TryParse(T &number, std::string_view s, Args... args)");
		openBlock();
		writeLine("const char *end = s.data() + s.size();");
		writeLine("auto result = std::from_chars(s.data(), end, number, args...);");
		writeLine("return result.ec == std::errc{} && result.ptr == end;");
		closeBlock();
	}
	if (this->stringReplace) {
		writeNewLine();
		writeLine("static std::string FuString_Replace(std::string_view s, std::string_view oldValue, std::string_view newValue)");
		openBlock();
		writeLine("std::string result;");
		writeLine("result.reserve(s.size());");
		writeLine("for (std::string_view::size_type i = 0;;) {");
		writeLine("\tauto j = s.find(oldValue, i);");
		writeLine("\tif (j == std::string::npos) {");
		writeLine("\t\tresult.append(s, i);");
		writeLine("\t\treturn result;");
		writeLine("\t}");
		writeLine("\tresult.append(s, i, j - i);");
		writeLine("\tresult.append(newValue);");
		writeLine("\ti = j + oldValue.size();");
		writeCharLine('}');
		closeBlock();
	}
	if (this->stringToLower || this->stringToUpper) {
		writeNewLine();
		writeLine("#ifdef _WIN32");
		writeNewLine();
		writeLine("#include <Windows.h>");
		writeNewLine();
		writeLine("static std::string FuString_Win32LCMap(std::string_view s, DWORD flags)");
		openBlock();
		writeLine("int size = MultiByteToWideChar(CP_UTF8, 0, s.data(), (int) s.size(), nullptr, 0);");
		writeLine("std::wstring wide(size, 0);");
		writeLine("MultiByteToWideChar(CP_UTF8, 0, s.data(), (int) s.size(), wide.data(), size);");
		writeLine("size = LCMapStringEx(LOCALE_NAME_SYSTEM_DEFAULT, LCMAP_LINGUISTIC_CASING | flags, wide.data(), size, nullptr, 0, nullptr, nullptr, 0);");
		writeLine("std::wstring wideResult(size, 0);");
		writeLine("LCMapStringEx(LOCALE_NAME_SYSTEM_DEFAULT, LCMAP_LINGUISTIC_CASING | flags, wide.data(), wide.size(), wideResult.data(), size, nullptr, nullptr, 0);");
		writeLine("int resultSize = WideCharToMultiByte(CP_UTF8, 0, wideResult.data(), size, nullptr, 0, nullptr, nullptr);");
		writeLine("std::string result(resultSize, 0);");
		writeLine("WideCharToMultiByte(CP_UTF8, 0, wideResult.data(), size, result.data(), resultSize, nullptr, nullptr);");
		writeLine("return result;");
		closeBlock();
		if (this->stringToLower) {
			writeNewLine();
			writeLine("static std::string FuString_ToLower(std::string_view s)");
			openBlock();
			writeLine("return FuString_Win32LCMap(s, LCMAP_LOWERCASE);");
			closeBlock();
		}
		if (this->stringToUpper) {
			writeNewLine();
			writeLine("static std::string FuString_ToUpper(std::string_view s)");
			openBlock();
			writeLine("return FuString_Win32LCMap(s, LCMAP_UPPERCASE);");
			closeBlock();
		}
		writeNewLine();
		writeLine("#else");
		writeNewLine();
		writeLine("#include <unicode/unistr.h>");
		if (this->stringToLower) {
			writeNewLine();
			writeLine("static std::string FuString_ToLower(std::string_view s)");
			openBlock();
			writeLine("std::string result;");
			writeLine("return icu::UnicodeString::fromUTF8(s).toLower().toUTF8String(result);");
			closeBlock();
		}
		if (this->stringToUpper) {
			writeNewLine();
			writeLine("static std::string FuString_ToUpper(std::string_view s)");
			openBlock();
			writeLine("std::string result;");
			writeLine("return icu::UnicodeString::fromUTF8(s).toUpper().toUTF8String(result);");
			closeBlock();
		}
		writeNewLine();
		writeLine("#endif");
	}
	closeStringWriter();
	closeFile();
}

std::string_view GenCs::getTargetName() const
{
	return "C++";
}

void GenCs::startDocLine()
{
	write("/// ");
}

void GenCs::writeDocPara(const FuDocPara * para, bool many)
{
	if (many) {
		writeNewLine();
		write("/// <para>");
	}
	for (const std::shared_ptr<FuDocInline> &inline_ : para->children) {
		if (const FuDocText *text = dynamic_cast<const FuDocText *>(inline_.get()))
			writeXmlDoc(text->text);
		else if (const FuDocCode *code = dynamic_cast<const FuDocCode *>(inline_.get())) {
			if (code->text == "true" || code->text == "false" || code->text == "null") {
				write("<see langword=\"");
				write(code->text);
				write("\" />");
			}
			else {
				write("<c>");
				writeXmlDoc(code->text);
				write("</c>");
			}
		}
		else if (dynamic_cast<const FuDocLine *>(inline_.get())) {
			writeNewLine();
			startDocLine();
		}
		else
			std::abort();
	}
	if (many)
		write("</para>");
}

void GenCs::writeDocList(const FuDocList * list)
{
	writeNewLine();
	writeLine("/// <list type=\"bullet\">");
	for (const FuDocPara &item : list->items) {
		write("/// <item>");
		writeDocPara(&item, false);
		writeLine("</item>");
	}
	write("/// </list>");
}

void GenCs::writeDoc(const FuCodeDoc * doc)
{
	if (doc == nullptr)
		return;
	write("/// <summary>");
	writeDocPara(&doc->summary, false);
	writeLine("</summary>");
	if (std::ssize(doc->details) > 0) {
		write("/// <remarks>");
		if (std::ssize(doc->details) == 1)
			writeDocBlock(doc->details[0].get(), false);
		else {
			for (const std::shared_ptr<FuDocBlock> &block : doc->details)
				writeDocBlock(block.get(), true);
		}
		writeLine("</remarks>");
	}
}

void GenCs::writeName(const FuSymbol * symbol)
{
	const FuConst * konst;
	if ((konst = dynamic_cast<const FuConst *>(symbol)) && konst->inMethod != nullptr) {
		write(konst->inMethod->name);
		writeChar('_');
		write(symbol->name);
		if (konst->inMethodIndex > 0)
			visitLiteralLong(konst->inMethodIndex);
		return;
	}
	write(symbol->name);
	if (symbol->name == "as" || symbol->name == "await" || symbol->name == "catch" || symbol->name == "char" || symbol->name == "checked" || symbol->name == "decimal" || symbol->name == "delegate" || symbol->name == "event" || symbol->name == "explicit" || symbol->name == "extern" || symbol->name == "finally" || symbol->name == "fixed" || symbol->name == "goto" || symbol->name == "implicit" || symbol->name == "interface" || symbol->name == "is" || symbol->name == "lock" || symbol->name == "namespace" || symbol->name == "object" || symbol->name == "operator" || symbol->name == "out" || symbol->name == "params" || symbol->name == "private" || symbol->name == "readonly" || symbol->name == "ref" || symbol->name == "sbyte" || symbol->name == "sizeof" || symbol->name == "stackalloc" || symbol->name == "struct" || symbol->name == "try" || symbol->name == "typeof" || symbol->name == "ulong" || symbol->name == "unchecked" || symbol->name == "unsafe" || symbol->name == "using" || symbol->name == "volatile")
		writeChar('_');
}

int GenCs::getLiteralChars() const
{
	return 65536;
}

void GenCs::writeVisibility(FuVisibility visibility)
{
	switch (visibility) {
	case FuVisibility::private_:
		break;
	case FuVisibility::internal:
		write("internal ");
		break;
	case FuVisibility::protected_:
		write("protected ");
		break;
	case FuVisibility::public_:
		write("public ");
		break;
	default:
		std::abort();
	}
}

void GenCs::writeCallType(FuCallType callType, std::string_view sealedString)
{
	switch (callType) {
	case FuCallType::static_:
		write("static ");
		break;
	case FuCallType::normal:
		break;
	case FuCallType::abstract:
		write("abstract ");
		break;
	case FuCallType::virtual_:
		write("virtual ");
		break;
	case FuCallType::override_:
		write("override ");
		break;
	case FuCallType::sealed:
		write(sealedString);
		break;
	}
}

void GenCs::writeElementType(const FuType * elementType)
{
	include("System.Collections.Generic");
	writeChar('<');
	writeType(elementType, false);
	writeChar('>');
}

void GenCs::writeType(const FuType * type, bool promote)
{
	if (dynamic_cast<const FuIntegerType *>(type)) {
		switch (getTypeId(type, promote)) {
		case FuId::sByteRange:
			write("sbyte");
			break;
		case FuId::byteRange:
			write("byte");
			break;
		case FuId::shortRange:
			write("short");
			break;
		case FuId::uShortRange:
			write("ushort");
			break;
		case FuId::intType:
		case FuId::nIntType:
			write("int");
			break;
		case FuId::longType:
			write("long");
			break;
		default:
			std::abort();
		}
	}
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		switch (klass->class_->id) {
		case FuId::stringClass:
			write("string");
			break;
		case FuId::arrayPtrClass:
		case FuId::arrayStorageClass:
			writeType(klass->getElementType().get(), false);
			write("[]");
			break;
		case FuId::listClass:
		case FuId::queueClass:
		case FuId::stackClass:
		case FuId::hashSetClass:
		case FuId::sortedSetClass:
			write(klass->class_->name);
			writeElementType(klass->getElementType().get());
			break;
		case FuId::priorityQueueClass:
			include("System.Collections.Generic");
			write("PriorityQueue<");
			writeType(klass->getElementType().get(), false);
			write(", ");
			writeType(klass->getElementType().get(), false);
			writeChar('>');
			break;
		case FuId::dictionaryClass:
		case FuId::sortedDictionaryClass:
			include("System.Collections.Generic");
			write(klass->class_->name);
			writeChar('<');
			writeType(klass->getKeyType(), false);
			write(", ");
			writeType(klass->getValueType().get(), false);
			writeChar('>');
			break;
		case FuId::orderedDictionaryClass:
			include("System.Collections.Specialized");
			write("OrderedDictionary");
			break;
		case FuId::textWriterClass:
		case FuId::stringWriterClass:
			include("System.IO");
			write(klass->class_->name);
			break;
		case FuId::regexClass:
		case FuId::matchClass:
			include("System.Text.RegularExpressions");
			write(klass->class_->name);
			break;
		case FuId::jsonElementClass:
			include("System.Text.Json");
			write("JsonElement");
			break;
		case FuId::lockClass:
			write("object");
			break;
		default:
			write(klass->class_->name);
			break;
		}
	}
	else
		write(type->name);
}

void GenCs::writeNewWithFields(const FuReadWriteClassType * type, const FuAggregateInitializer * init)
{
	write("new ");
	writeType(type, false);
	writeObjectLiteral(init, " = ");
}

void GenCs::writeCoercedLiteral(const FuType * type, const FuExpr * expr)
{
	const FuRangeType * range;
	if (dynamic_cast<const FuLiteralChar *>(expr) && (range = dynamic_cast<const FuRangeType *>(type)) && range->max <= 255)
		writeStaticCast(type, expr);
	else
		GenTyped::writeCoercedLiteral(type, expr);
}

bool GenCs::isPromoted(const FuExpr * expr) const
{
	return GenTyped::isPromoted(expr) || dynamic_cast<const FuLiteralChar *>(expr);
}

void GenCs::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	write("$\"");
	for (const FuInterpolatedPart &part : expr->parts) {
		writeDoubling(part.prefix, '{');
		writeChar('{');
		part.argument->accept(this, FuPriority::argument);
		if (part.widthExpr != nullptr) {
			writeChar(',');
			visitLiteralLong(part.width);
		}
		if (part.format != ' ') {
			writeChar(':');
			writeChar(part.format);
			if (part.precision >= 0)
				visitLiteralLong(part.precision);
		}
		writeChar('}');
	}
	writeDoubling(expr->suffix, '{');
	writeChar('"');
}

void GenCs::writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent)
{
	write("new ");
	writeType(elementType->getBaseType(), false);
	writeChar('[');
	lengthExpr->accept(this, FuPriority::argument);
	writeChar(']');
	const FuClassType * array;
	while ((array = dynamic_cast<const FuClassType *>(elementType)) && array->isArray()) {
		write("[]");
		elementType = array->getElementType().get();
	}
}

void GenCs::writeNew(const FuReadWriteClassType * klass, FuPriority parent)
{
	write("new ");
	writeType(klass, false);
	write("()");
}

bool GenCs::hasInitCode(const FuNamedValue * def) const
{
	const FuArrayStorageType * array;
	return (array = dynamic_cast<const FuArrayStorageType *>(def->type.get())) && dynamic_cast<const FuStorageType *>(array->getElementType().get());
}

void GenCs::writeInitCode(const FuNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	const FuArrayStorageType * array = static_cast<const FuArrayStorageType *>(def->type.get());
	int nesting = 0;
	while (const FuArrayStorageType *innerArray = dynamic_cast<const FuArrayStorageType *>(array->getElementType().get())) {
		openLoop("int", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		writeNewArray(innerArray->getElementType().get(), innerArray->lengthExpr.get(), FuPriority::argument);
		writeCharLine(';');
		array = innerArray;
	}
	if (const FuStorageType *klass = dynamic_cast<const FuStorageType *>(array->getElementType().get())) {
		openLoop("int", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		writeNew(klass, FuPriority::argument);
		writeCharLine(';');
	}
	while (--nesting >= 0)
		closeBlock();
}

void GenCs::writeResource(std::string_view name, int length)
{
	write("FuResource.");
	writeResourceName(name);
}

void GenCs::writeStringLength(const FuExpr * expr)
{
	writePostfix(expr, ".Length");
}

void GenCs::writeArrayLength(const FuExpr * expr, FuPriority parent)
{
	writePostfix(expr, ".Length");
}

void GenCs::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::consoleError:
		include("System");
		write("Console.Error");
		break;
	case FuId::matchStart:
		writePostfix(expr->left.get(), ".Index");
		break;
	case FuId::matchEnd:
		if (parent > FuPriority::add)
			writeChar('(');
		writePostfix(expr->left.get(), ".Index + ");
		writeStringLength(expr->left.get());
		if (parent > FuPriority::add)
			writeChar(')');
		break;
	case FuId::mathNaN:
	case FuId::mathNegativeInfinity:
	case FuId::mathPositiveInfinity:
		write("float.");
		write(expr->symbol->name);
		break;
	default:
		{
			const FuForeach * forEach;
			const FuClassType * dict;
			if ((forEach = dynamic_cast<const FuForeach *>(expr->symbol->parent)) && (dict = dynamic_cast<const FuClassType *>(forEach->collection->type.get())) && dict->class_->id == FuId::orderedDictionaryClass) {
				if (parent == FuPriority::primary)
					writeChar('(');
				const FuVar * element = forEach->getVar();
				if (expr->symbol == element) {
					writeStaticCastType(dict->getKeyType());
					writeName(element);
					write(".Key");
				}
				else {
					writeStaticCastType(dict->getValueType().get());
					writeName(element);
					write(".Value");
				}
				if (parent == FuPriority::primary)
					writeChar(')');
			}
			else
				GenBase::visitSymbolReference(expr, parent);
			break;
		}
	}
}

void GenCs::writeJsonElementIs(const FuExpr * obj, std::string_view name, FuPriority parent)
{
	if (parent > FuPriority::equality)
		writeChar('(');
	writePostfix(obj, ".ValueKind == JsonValueKind.");
	write(name);
	if (parent > FuPriority::equality)
		writeChar(')');
}

void GenCs::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case FuId::intTryParse:
	case FuId::longTryParse:
	case FuId::doubleTryParse:
		write(obj->type->name);
		write(".TryParse(");
		(*args)[0]->accept(this, FuPriority::argument);
		if (std::ssize(*args) == 2) {
			const FuLiteralLong * radix;
			if (!(radix = dynamic_cast<const FuLiteralLong *>((*args)[1].get())) || radix->value != 16)
				notSupported((*args)[1].get(), "Radix");
			include("System.Globalization");
			write(", NumberStyles.HexNumber, null");
		}
		write(", out ");
		obj->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::stringIndexOf:
	case FuId::stringLastIndexOf:
		obj->accept(this, FuPriority::primary);
		writeChar('.');
		write(method->name);
		writeChar('(');
		{
			int c = getOneAscii((*args)[0].get());
			if (c >= 0)
				visitLiteralChar(c);
			else
				(*args)[0]->accept(this, FuPriority::argument);
			writeChar(')');
			break;
		}
	case FuId::arrayBinarySearchAll:
	case FuId::arrayBinarySearchPart:
		include("System");
		write("Array.BinarySearch(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		if (std::ssize(*args) == 3) {
			(*args)[1]->accept(this, FuPriority::argument);
			write(", ");
			(*args)[2]->accept(this, FuPriority::argument);
			write(", ");
		}
		writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		writeChar(')');
		break;
	case FuId::arrayContains:
		include("System.Linq");
		writeMethodCall(obj, "Contains", (*args)[0].get());
		break;
	case FuId::arrayCopyTo:
		include("System");
		write("Array.Copy(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		writeCoercedArgs(method, args);
		writeChar(')');
		break;
	case FuId::arrayFillAll:
	case FuId::arrayFillPart:
		include("System");
		{
			const FuLiteral * literal;
			if ((literal = dynamic_cast<const FuLiteral *>((*args)[0].get())) && literal->isDefaultValue()) {
				write("Array.Clear(");
				obj->accept(this, FuPriority::argument);
				if (std::ssize(*args) == 1) {
					write(", 0, ");
					writeArrayStorageLength(obj);
				}
			}
			else {
				write("Array.Fill(");
				obj->accept(this, FuPriority::argument);
				write(", ");
				writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
			}
			if (std::ssize(*args) == 3) {
				write(", ");
				(*args)[1]->accept(this, FuPriority::argument);
				write(", ");
				(*args)[2]->accept(this, FuPriority::argument);
			}
			writeChar(')');
			break;
		}
	case FuId::arraySortAll:
		include("System");
		writeCall("Array.Sort", obj);
		break;
	case FuId::arraySortPart:
		include("System");
		writeCall("Array.Sort", obj, (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::listAdd:
		writeListAdd(obj, "Add", args);
		break;
	case FuId::listAll:
		writeMethodCall(obj, "TrueForAll", (*args)[0].get());
		break;
	case FuId::listAny:
		writeMethodCall(obj, "Exists", (*args)[0].get());
		break;
	case FuId::listInsert:
		writeListInsert(obj, "Insert", args);
		break;
	case FuId::listLast:
		writePostfix(obj, "[^1]");
		break;
	case FuId::listSortPart:
		writePostfix(obj, ".Sort(");
		writeCoercedArgs(method, args);
		write(", null)");
		break;
	case FuId::priorityQueueEnqueue:
		writeMethodCall(obj, "Enqueue", (*args)[0].get(), (*args)[0].get());
		break;
	case FuId::dictionaryAdd:
		writePostfix(obj, ".Add(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		writeNewStorage(obj->type->asClassType()->getValueType().get());
		writeChar(')');
		break;
	case FuId::orderedDictionaryContainsKey:
		writeMethodCall(obj, "Contains", (*args)[0].get());
		break;
	case FuId::textWriterWrite:
	case FuId::textWriterWriteLine:
	case FuId::consoleWrite:
	case FuId::consoleWriteLine:
		include("System");
		obj->accept(this, FuPriority::primary);
		writeChar('.');
		write(method->name);
		writeChar('(');
		if (std::ssize(*args) != 0) {
			if (dynamic_cast<const FuLiteralChar *>((*args)[0].get())) {
				write("(int) ");
				(*args)[0]->accept(this, FuPriority::primary);
			}
			else
				(*args)[0]->accept(this, FuPriority::argument);
		}
		writeChar(')');
		break;
	case FuId::stringWriterClear:
		writePostfix(obj, ".GetStringBuilder().Clear()");
		break;
	case FuId::textWriterWriteChar:
		writeCharMethodCall(obj, "Write", (*args)[0].get());
		break;
	case FuId::textWriterWriteCodePoint:
		writePostfix(obj, ".Write(");
		{
			const FuLiteralChar * literalChar;
			if ((literalChar = dynamic_cast<const FuLiteralChar *>((*args)[0].get())) && literalChar->value < 65536)
				(*args)[0]->accept(this, FuPriority::argument);
			else {
				include("System.Text");
				writeCall("new Rune", (*args)[0].get());
			}
			writeChar(')');
			break;
		}
	case FuId::environmentGetEnvironmentVariable:
		include("System");
		obj->accept(this, FuPriority::primary);
		writeChar('.');
		write(method->name);
		writeInParentheses(args);
		break;
	case FuId::uTF8GetByteCount:
		include("System.Text");
		writeCall("Encoding.UTF8.GetByteCount", (*args)[0].get());
		break;
	case FuId::uTF8GetBytes:
		include("System.Text");
		write("Encoding.UTF8.GetBytes(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", 0, ");
		writePostfix((*args)[0].get(), ".Length, ");
		(*args)[1]->accept(this, FuPriority::argument);
		write(", ");
		(*args)[2]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::uTF8GetString:
		include("System.Text");
		write("Encoding.UTF8.GetString");
		writeInParentheses(args);
		break;
	case FuId::regexCompile:
		include("System.Text.RegularExpressions");
		write("new Regex");
		writeInParentheses(args);
		break;
	case FuId::regexEscape:
	case FuId::regexIsMatchStr:
	case FuId::regexIsMatchRegex:
		include("System.Text.RegularExpressions");
		obj->accept(this, FuPriority::primary);
		writeChar('.');
		write(method->name);
		writeInParentheses(args);
		break;
	case FuId::matchFindStr:
		include("System.Text.RegularExpressions");
		writeChar('(');
		obj->accept(this, FuPriority::assign);
		write(" = Regex.Match");
		writeInParentheses(args);
		write(").Success");
		break;
	case FuId::matchFindRegex:
		include("System.Text.RegularExpressions");
		writeChar('(');
		obj->accept(this, FuPriority::assign);
		write(" = ");
		writeMethodCall((*args)[1].get(), "Match", (*args)[0].get());
		write(").Success");
		break;
	case FuId::matchGetCapture:
		writePostfix(obj, ".Groups[");
		(*args)[0]->accept(this, FuPriority::argument);
		write("].Value");
		break;
	case FuId::jsonElementParse:
		write("JsonDocument.Parse(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(").RootElement");
		break;
	case FuId::jsonElementIsObject:
		writeJsonElementIs(obj, "Object", parent);
		break;
	case FuId::jsonElementIsArray:
		writeJsonElementIs(obj, "Array", parent);
		break;
	case FuId::jsonElementIsString:
		writeJsonElementIs(obj, "String", parent);
		break;
	case FuId::jsonElementIsNumber:
		writeJsonElementIs(obj, "Number", parent);
		break;
	case FuId::jsonElementIsBoolean:
		if (parent > FuPriority::condOr)
			writeChar('(');
		writePostfix(obj, ".ValueKind == JsonValueKind.True || ");
		writePostfix(obj, ".ValueKind == JsonValueKind.False");
		if (parent > FuPriority::condOr)
			writeChar(')');
		break;
	case FuId::jsonElementIsNull:
		writeJsonElementIs(obj, "Null", parent);
		break;
	case FuId::jsonElementGetObject:
		include("System.Linq");
		writePostfix(obj, ".EnumerateObject().ToDictionary(p => p.Name, p => p.Value)");
		break;
	case FuId::jsonElementGetArray:
		include("System.Linq");
		writePostfix(obj, ".EnumerateArray().ToList()");
		break;
	case FuId::mathMethod:
	case FuId::mathCeiling:
	case FuId::mathFusedMultiplyAdd:
	case FuId::mathLog2:
	case FuId::mathRound:
	case FuId::mathTruncate:
		include("System");
		write("Math");
		if (!std::any_of(args->begin(), args->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::doubleType; }))
			writeChar('F');
		writeChar('.');
		write(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathAbs:
	case FuId::mathClamp:
	case FuId::mathMax:
	case FuId::mathMin:
		include("System");
		write("Math.");
		write(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathIsFinite:
	case FuId::mathIsInfinity:
	case FuId::mathIsNaN:
		write("double.");
		writeCall(method->name, (*args)[0].get());
		break;
	default:
		if (obj != nullptr) {
			obj->accept(this, FuPriority::primary);
			writeChar('.');
		}
		writeName(method);
		writeCoercedArgsInParentheses(method, args);
		break;
	}
}

void GenCs::writeOrderedDictionaryIndexing(const FuBinaryExpr * expr)
{
	if (expr->right->type->id == FuId::intType || dynamic_cast<const FuRangeType *>(expr->right->type.get())) {
		writePostfix(expr->left.get(), "[(object) ");
		expr->right->accept(this, FuPriority::primary);
		writeChar(']');
	}
	else
		GenBase::writeIndexingExpr(expr, FuPriority::and_);
}

void GenCs::writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuClassType * dict;
	if ((dict = dynamic_cast<const FuClassType *>(expr->left->type.get())) && dict->class_->id == FuId::orderedDictionaryClass) {
		if (parent == FuPriority::primary)
			writeChar('(');
		writeStaticCastType(expr->type.get());
		writeOrderedDictionaryIndexing(expr);
		if (parent == FuPriority::primary)
			writeChar(')');
	}
	else
		GenBase::writeIndexingExpr(expr, parent);
}

void GenCs::writeAssign(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuBinaryExpr * indexing;
	const FuClassType * dict;
	if ((indexing = dynamic_cast<const FuBinaryExpr *>(expr->left.get())) && indexing->op == FuToken::leftBracket && (dict = dynamic_cast<const FuClassType *>(indexing->left->type.get())) && dict->class_->id == FuId::orderedDictionaryClass) {
		writeOrderedDictionaryIndexing(indexing);
		write(" = ");
		writeAssignRight(expr);
	}
	else
		GenBase::writeAssign(expr, parent);
}

void GenCs::visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::andAssign:
	case FuToken::orAssign:
	case FuToken::xorAssign:
		if (parent > FuPriority::assign)
			writeChar('(');
		expr->left->accept(this, FuPriority::assign);
		writeChar(' ');
		write(expr->getOpString());
		writeChar(' ');
		writeAssignRight(expr);
		if (parent > FuPriority::assign)
			writeChar(')');
		break;
	default:
		GenBase::visitBinaryExpr(expr, parent);
		break;
	}
}

void GenCs::visitLambdaExpr(const FuLambdaExpr * expr)
{
	writeName(expr->first);
	write(" => ");
	expr->body->accept(this, FuPriority::statement);
}

void GenCs::defineObjectLiteralTemporary(const FuUnaryExpr * expr)
{
}

void GenCs::defineIsVar(const FuBinaryExpr * binary)
{
}

void GenCs::writeAssert(const FuAssert * statement)
{
	if (statement->completesNormally()) {
		include("System.Diagnostics");
		write("Debug.Assert(");
		statement->cond->accept(this, FuPriority::argument);
		if (statement->message != nullptr) {
			write(", ");
			statement->message->accept(this, FuPriority::argument);
		}
	}
	else {
		include("System");
		write("throw new NotImplementedException(");
		if (statement->message != nullptr)
			statement->message->accept(this, FuPriority::argument);
	}
	writeLine(");");
}

void GenCs::visitForeach(const FuForeach * statement)
{
	write("foreach (");
	const FuClassType * dict;
	if ((dict = dynamic_cast<const FuClassType *>(statement->collection->type.get())) && dict->class_->typeParameterCount == 2) {
		if (dict->class_->id == FuId::orderedDictionaryClass) {
			include("System.Collections");
			write("DictionaryEntry ");
			writeName(statement->getVar());
		}
		else {
			writeChar('(');
			writeTypeAndName(statement->getVar());
			write(", ");
			writeTypeAndName(statement->getValueVar());
			writeChar(')');
		}
	}
	else
		writeTypeAndName(statement->getVar());
	write(" in ");
	statement->collection->accept(this, FuPriority::argument);
	writeChar(')');
	writeChild(statement->body.get());
}

void GenCs::visitLock(const FuLock * statement)
{
	writeCall("lock ", statement->lock.get());
	writeChild(statement->body.get());
}

void GenCs::writeException()
{
	include("System");
	write("Exception");
}

void GenCs::writeEnum(const FuEnum * enu)
{
	writeNewLine();
	writeDoc(enu->documentation.get());
	if (dynamic_cast<const FuEnumFlags *>(enu)) {
		include("System");
		writeLine("[Flags]");
	}
	writePublic(enu);
	write("enum ");
	writeLine(enu->name);
	openBlock();
	enu->acceptValues(this);
	writeNewLine();
	closeBlock();
}

void GenCs::writeRegexOptionsEnum(const FuProgram * program)
{
	if (program->regexOptionsEnum)
		include("System.Text.RegularExpressions");
}

void GenCs::writeConst(const FuConst * konst)
{
	writeNewLine();
	writeDoc(konst->documentation.get());
	writeVisibility(konst->visibility);
	write(dynamic_cast<const FuArrayStorageType *>(konst->type.get()) ? "static readonly " : "const ");
	writeTypeAndName(konst);
	write(" = ");
	writeCoercedExpr(konst->type.get(), konst->value.get());
	writeCharLine(';');
}

void GenCs::writeField(const FuField * field)
{
	writeNewLine();
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	if (field->type->isFinal() && !field->isAssignableStorage())
		write("readonly ");
	writeVar(field);
	writeCharLine(';');
}

void GenCs::writeParameterDoc(const FuVar * param, bool first)
{
	write("/// <param name=\"");
	writeName(param);
	write("\">");
	writeDocPara(&param->documentation->summary, false);
	writeLine("</param>");
}

void GenCs::writeThrowsDoc(const FuThrowsDeclaration * decl)
{
	write("/// <exception cref=\"");
	writeExceptionClass(decl->symbol);
	write("\">");
	writeDocPara(&decl->documentation->summary, false);
	writeLine("</exception>");
}

bool GenCs::isShortMethod(const FuMethod * method) const
{
	return dynamic_cast<const FuReturn *>(method->body.get());
}

void GenCs::writeMethod(const FuMethod * method)
{
	if (method->id == FuId::classToString && method->callType == FuCallType::abstract)
		return;
	writeNewLine();
	writeDoc(method->documentation.get());
	writeParametersAndThrowsDoc(method);
	writeVisibility(method->visibility);
	if (method->id == FuId::classToString)
		write("override ");
	else
		writeCallType(method->callType, "sealed override ");
	writeTypeAndName(method);
	writeParameters(method, true);
	writeBody(method);
}

void GenCs::writeClass(const FuClass * klass, const FuProgram * program)
{
	writeNewLine();
	writeDoc(klass->documentation.get());
	writePublic(klass);
	writeCallType(klass->callType, "sealed ");
	openClass(klass, "", " : ");
	if (needsConstructor(klass)) {
		if (klass->constructor != nullptr) {
			writeDoc(klass->constructor->documentation.get());
			writeVisibility(klass->constructor->visibility);
		}
		else
			write("internal ");
		write(klass->name);
		writeLine("()");
		openBlock();
		writeConstructorBody(klass);
		closeBlock();
	}
	else if (klass->id == FuId::exceptionClass) {
		writeExceptionConstructor(klass, "() { }");
		writeExceptionConstructor(klass, "(String message) : base(message) { }");
		writeExceptionConstructor(klass, "(String message, Exception innerException) : base(message, innerException) { }");
	}
	writeMembers(klass, true);
	closeBlock();
}

void GenCs::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	writeNewLine();
	writeLine("internal static class FuResource");
	openBlock();
	for (const auto &[name, content] : *resources) {
		write("internal static readonly byte[] ");
		writeResourceName(name);
		writeLine(" = {");
		writeChar('\t');
		writeBytes(&content);
		writeLine(" };");
	}
	closeBlock();
}

void GenCs::writeProgram(const FuProgram * program)
{
	openStringWriter();
	if (!this->namespace_.empty()) {
		write("namespace ");
		writeLine(this->namespace_);
		openBlock();
	}
	writeTopLevelNatives(program);
	writeTypes(program);
	if (std::ssize(program->resources) > 0)
		writeResources(&program->resources);
	if (!this->namespace_.empty())
		closeBlock();
	createOutputFile();
	writeIncludes("using ", ";");
	closeStringWriter();
	closeFile();
}

std::string_view GenD::getTargetName() const
{
	return "D";
}

void GenD::startDocLine()
{
	write("/// ");
}

void GenD::writeDocPara(const FuDocPara * para, bool many)
{
	if (many) {
		writeNewLine();
		startDocLine();
	}
	for (const std::shared_ptr<FuDocInline> &inline_ : para->children) {
		if (const FuDocText *text = dynamic_cast<const FuDocText *>(inline_.get()))
			writeXmlDoc(text->text);
		else if (const FuDocCode *code = dynamic_cast<const FuDocCode *>(inline_.get())) {
			writeChar('`');
			writeXmlDoc(code->text);
			writeChar('`');
		}
		else if (dynamic_cast<const FuDocLine *>(inline_.get())) {
			writeNewLine();
			startDocLine();
		}
		else
			std::abort();
	}
	if (many)
		writeNewLine();
}

void GenD::writeParameterDoc(const FuVar * param, bool first)
{
	if (first) {
		startDocLine();
		writeLine("Params:");
	}
	startDocLine();
	writeName(param);
	write(" = ");
	writeDocPara(&param->documentation->summary, false);
	writeNewLine();
}

void GenD::writeThrowsDoc(const FuThrowsDeclaration * decl)
{
	write("/// Throws: ");
	writeExceptionClass(decl->symbol);
	writeChar(' ');
	writeDocPara(&decl->documentation->summary, false);
	writeNewLine();
}

void GenD::writeDocList(const FuDocList * list)
{
	writeLine("///");
	writeLine("/// <ul>");
	for (const FuDocPara &item : list->items) {
		write("/// <li>");
		writeDocPara(&item, false);
		writeLine("</li>");
	}
	writeLine("/// </ul>");
	write("///");
}

void GenD::writeDoc(const FuCodeDoc * doc)
{
	if (doc == nullptr)
		return;
	startDocLine();
	writeDocPara(&doc->summary, false);
	writeNewLine();
	if (std::ssize(doc->details) > 0) {
		startDocLine();
		if (std::ssize(doc->details) == 1)
			writeDocBlock(doc->details[0].get(), false);
		else {
			for (const std::shared_ptr<FuDocBlock> &block : doc->details)
				writeDocBlock(block.get(), true);
		}
		writeNewLine();
	}
}

void GenD::writeName(const FuSymbol * symbol)
{
	if (dynamic_cast<const FuContainerType *>(symbol)) {
		write(symbol->name);
		return;
	}
	writeCamelCase(symbol->name);
	if (symbol->name == "Abstract" || symbol->name == "Alias" || symbol->name == "Align" || symbol->name == "Asm" || symbol->name == "Assert" || symbol->name == "Auto" || symbol->name == "Body" || symbol->name == "Bool" || symbol->name == "Break" || symbol->name == "Byte" || symbol->name == "Case" || symbol->name == "Cast" || symbol->name == "Catch" || symbol->name == "Cdouble" || symbol->name == "Cent" || symbol->name == "Cfloat" || symbol->name == "Char" || symbol->name == "Class" || symbol->name == "Const" || symbol->name == "Continue" || symbol->name == "Creal" || symbol->name == "Dchar" || symbol->name == "Debug" || symbol->name == "Default" || symbol->name == "Delegate" || symbol->name == "Delete" || symbol->name == "Deprecated" || symbol->name == "Do" || symbol->name == "Double" || symbol->name == "Else" || symbol->name == "Enum" || symbol->name == "Export" || symbol->name == "Extern" || symbol->name == "False" || symbol->name == "Final" || symbol->name == "Finally" || symbol->name == "Float" || symbol->name == "For" || symbol->name == "Foreach" || symbol->name == "Foreach_reverse" || symbol->name == "Function" || symbol->name == "Goto" || symbol->name == "Idouble" || symbol->name == "If" || symbol->name == "IfLoat" || symbol->name == "Immutable" || symbol->name == "Import" || symbol->name == "In" || symbol->name == "Inout" || symbol->name == "Int" || symbol->name == "Interface" || symbol->name == "Invariant" || symbol->name == "Ireal" || symbol->name == "Is" || symbol->name == "Lazy" || symbol->name == "Long" || symbol->name == "Macro" || symbol->name == "Mixin" || symbol->name == "Module" || symbol->name == "New" || symbol->name == "Nothrow" || symbol->name == "Null" || symbol->name == "Out" || symbol->name == "Override" || symbol->name == "Package" || symbol->name == "Pragma" || symbol->name == "Private" || symbol->name == "Protected" || symbol->name == "Public" || symbol->name == "Pure" || symbol->name == "Real" || symbol->name == "Ref" || symbol->name == "Return" || symbol->name == "Scope" || symbol->name == "Shared" || symbol->name == "Short" || symbol->name == "Sizeof" || symbol->name == "Static" || symbol->name == "String" || symbol->name == "Struct" || symbol->name == "Super" || symbol->name == "Switch" || symbol->name == "Synchronized" || symbol->name == "Template" || symbol->name == "Throw" || symbol->name == "True" || symbol->name == "Try" || symbol->name == "Typeid" || symbol->name == "Typeof" || symbol->name == "Ubyte" || symbol->name == "Ucent" || symbol->name == "Uint" || symbol->name == "Ulong" || symbol->name == "Union" || symbol->name == "Unittest" || symbol->name == "Ushort" || symbol->name == "Version" || symbol->name == "Void" || symbol->name == "Wchar" || symbol->name == "While" || symbol->name == "With" || symbol->name == "alias" || symbol->name == "align" || symbol->name == "asm" || symbol->name == "auto" || symbol->name == "body" || symbol->name == "cast" || symbol->name == "catch" || symbol->name == "cdouble" || symbol->name == "cent" || symbol->name == "cfloat" || symbol->name == "char" || symbol->name == "creal" || symbol->name == "dchar" || symbol->name == "debug" || symbol->name == "delegate" || symbol->name == "delete" || symbol->name == "deprecated" || symbol->name == "export" || symbol->name == "extern" || symbol->name == "final" || symbol->name == "finally" || symbol->name == "foreach_reverse" || symbol->name == "function" || symbol->name == "goto" || symbol->name == "idouble" || symbol->name == "ifloat" || symbol->name == "immutable" || symbol->name == "import" || symbol->name == "in" || symbol->name == "inout" || symbol->name == "interface" || symbol->name == "invariant" || symbol->name == "ireal" || symbol->name == "lazy" || symbol->name == "macro" || symbol->name == "mixin" || symbol->name == "module" || symbol->name == "nothrow" || symbol->name == "out" || symbol->name == "package" || symbol->name == "pragma" || symbol->name == "private" || symbol->name == "pure" || symbol->name == "real" || symbol->name == "ref" || symbol->name == "scope" || symbol->name == "shared" || symbol->name == "sizeof" || symbol->name == "struct" || symbol->name == "super" || symbol->name == "synchronized" || symbol->name == "template" || symbol->name == "try" || symbol->name == "typeid" || symbol->name == "typeof" || symbol->name == "ubyte" || symbol->name == "ucent" || symbol->name == "uint" || symbol->name == "ulong" || symbol->name == "union" || symbol->name == "unittest" || symbol->name == "ushort" || symbol->name == "version" || symbol->name == "wchar" || symbol->name == "with" || symbol->name == "__FILE__" || symbol->name == "__FILE_FULL_PATH__" || symbol->name == "__MODULE__" || symbol->name == "__LINE__" || symbol->name == "__FUNCTION__" || symbol->name == "__PRETTY_FUNCTION__" || symbol->name == "__gshared" || symbol->name == "__traits" || symbol->name == "__vector" || symbol->name == "__parameters")
		writeChar('_');
}

int GenD::getLiteralChars() const
{
	return 65536;
}

void GenD::writeVisibility(FuVisibility visibility)
{
	switch (visibility) {
	case FuVisibility::private_:
		write("private ");
		break;
	case FuVisibility::internal:
	case FuVisibility::public_:
		break;
	case FuVisibility::protected_:
		write("protected ");
		break;
	default:
		std::abort();
	}
}

void GenD::writeCallType(FuCallType callType, std::string_view sealedString)
{
	switch (callType) {
	case FuCallType::static_:
		write("static ");
		break;
	case FuCallType::normal:
		break;
	case FuCallType::abstract:
		write("abstract ");
		break;
	case FuCallType::virtual_:
		break;
	case FuCallType::override_:
		write("override ");
		break;
	case FuCallType::sealed:
		write(sealedString);
		break;
	}
}

bool GenD::isCreateWithNew(const FuType * type)
{
	if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		if (const FuStorageType *stg = dynamic_cast<const FuStorageType *>(klass))
			return stg->class_->id != FuId::arrayStorageClass;
		return true;
	}
	return false;
}

bool GenD::isTransitiveConst(const FuClassType * array)
{
	while (!dynamic_cast<const FuReadWriteClassType *>(array)) {
		const FuClassType * element;
		if (!(element = dynamic_cast<const FuClassType *>(array->getElementType().get())))
			return true;
		if (element->class_->id != FuId::arrayPtrClass)
			return false;
		array = element;
	}
	return false;
}

bool GenD::isJsonElementList(const FuClassType * list)
{
	const FuClassType * json;
	return (json = dynamic_cast<const FuClassType *>(list->getElementType().get())) && json->class_->id == FuId::jsonElementClass;
}

bool GenD::isStructPtr(const FuType * type)
{
	const FuClassType * ptr;
	return (ptr = dynamic_cast<const FuClassType *>(type)) && (ptr->class_->id == FuId::listClass || ptr->class_->id == FuId::stackClass || ptr->class_->id == FuId::queueClass) && !isJsonElementList(ptr);
}

void GenD::writeElementType(const FuType * type)
{
	writeType(type, false);
	if (isStructPtr(type))
		writeChar('*');
}

void GenD::writeType(const FuType * type, bool promote)
{
	if (dynamic_cast<const FuIntegerType *>(type)) {
		switch (getTypeId(type, promote)) {
		case FuId::sByteRange:
			write("byte");
			break;
		case FuId::byteRange:
			write("ubyte");
			break;
		case FuId::shortRange:
			write("short");
			break;
		case FuId::uShortRange:
			write("ushort");
			break;
		case FuId::intType:
			write("int");
			break;
		case FuId::nIntType:
			write("ptrdiff_t");
			break;
		case FuId::longType:
			write("long");
			break;
		default:
			std::abort();
		}
	}
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		switch (klass->class_->id) {
		case FuId::stringClass:
			write("string");
			break;
		case FuId::arrayStorageClass:
		case FuId::arrayPtrClass:
			if (promote && isTransitiveConst(klass)) {
				write("const(");
				writeElementType(klass->getElementType().get());
				writeChar(')');
			}
			else
				writeElementType(klass->getElementType().get());
			writeChar('[');
			if (const FuArrayStorageType *arrayStorage = dynamic_cast<const FuArrayStorageType *>(klass))
				visitLiteralLong(arrayStorage->length);
			writeChar(']');
			break;
		case FuId::listClass:
		case FuId::stackClass:
			if (isJsonElementList(klass)) {
				include("std.json");
				write("JSONValue[]");
			}
			else {
				include("std.container.array");
				write("Array!(");
				writeElementType(klass->getElementType().get());
				writeChar(')');
			}
			break;
		case FuId::queueClass:
			include("std.container.dlist");
			write("DList!(");
			writeElementType(klass->getElementType().get());
			writeChar(')');
			break;
		case FuId::hashSetClass:
			write("bool[");
			writeElementType(klass->getElementType().get());
			writeChar(']');
			break;
		case FuId::dictionaryClass:
			writeElementType(klass->getValueType().get());
			writeChar('[');
			writeType(klass->getKeyType(), false);
			writeChar(']');
			break;
		case FuId::sortedSetClass:
			include("std.container.rbtree");
			write("RedBlackTree!(");
			writeElementType(klass->getElementType().get());
			writeChar(')');
			break;
		case FuId::sortedDictionaryClass:
			include("std.container.rbtree");
			include("std.typecons");
			write("RedBlackTree!(Tuple!(");
			writeElementType(klass->getKeyType());
			write(", ");
			writeElementType(klass->getValueType().get());
			write("), \"a[0] < b[0]\")");
			break;
		case FuId::orderedDictionaryClass:
			include("std.typecons");
			write("Tuple!(Array!(");
			writeElementType(klass->getValueType().get());
			write("), \"data\", size_t[");
			writeType(klass->getKeyType(), false);
			write("], \"dict\")");
			break;
		case FuId::textWriterClass:
			include("std.stdio");
			write("File");
			break;
		case FuId::regexClass:
			include("std.regex");
			write("Regex!char");
			break;
		case FuId::matchClass:
			include("std.regex");
			write("Captures!string");
			break;
		case FuId::jsonElementClass:
			include("std.json");
			write("JSONValue");
			break;
		case FuId::lockClass:
			write("Object");
			break;
		default:
			write(klass->class_->name);
			break;
		}
	}
	else
		write(type->name);
}

void GenD::writeTypeAndName(const FuNamedValue * value)
{
	writeType(value->type.get(), true);
	if (isStructPtr(value->type.get()))
		writeChar('*');
	writeChar(' ');
	writeName(value);
}

void GenD::visitAggregateInitializer(const FuAggregateInitializer * expr)
{
	write("[ ");
	writeCoercedLiterals(expr->type->asClassType()->getElementType().get(), &expr->items);
	write(" ]");
}

void GenD::writeStaticCast(const FuType * type, const FuExpr * expr)
{
	write("cast(");
	writeType(type, false);
	write(")(");
	getStaticCastInner(type, expr)->accept(this, FuPriority::argument);
	writeChar(')');
}

void GenD::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	include("std.format");
	write("format(");
	writePrintf(expr, false);
}

void GenD::writeStorageInit(const FuNamedValue * def)
{
	write(" = ");
	writeNewStorage(def->type.get());
}

void GenD::writeVarInit(const FuNamedValue * def)
{
	if (dynamic_cast<const FuArrayStorageType *>(def->type.get()))
		return;
	GenBase::writeVarInit(def);
}

bool GenD::hasInitCode(const FuNamedValue * def) const
{
	if (def->value != nullptr && !dynamic_cast<const FuLiteral *>(def->value.get()))
		return true;
	const FuType * type = def->type.get();
	if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(type)) {
		while (const FuArrayStorageType *innerArray = dynamic_cast<const FuArrayStorageType *>(array->getElementType().get()))
			array = innerArray;
		type = array->getElementType().get();
	}
	return dynamic_cast<const FuStorageType *>(type);
}

void GenD::writeInitField(const FuField * field)
{
	writeInitCode(field);
}

void GenD::writeInitCode(const FuNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(def->type.get())) {
		int nesting = 0;
		while (const FuArrayStorageType *innerArray = dynamic_cast<const FuArrayStorageType *>(array->getElementType().get())) {
			openLoop("size_t", nesting++, array->length);
			array = innerArray;
		}
		if (const FuStorageType *klass = dynamic_cast<const FuStorageType *>(array->getElementType().get())) {
			openLoop("size_t", nesting++, array->length);
			writeArrayElement(def, nesting);
			write(" = ");
			writeNew(klass, FuPriority::argument);
			writeCharLine(';');
		}
		while (--nesting >= 0)
			closeBlock();
	}
	else {
		if (const FuReadWriteClassType *klass = dynamic_cast<const FuReadWriteClassType *>(def->type.get())) {
			switch (klass->class_->id) {
			case FuId::stringClass:
			case FuId::arrayStorageClass:
			case FuId::arrayPtrClass:
			case FuId::dictionaryClass:
			case FuId::hashSetClass:
			case FuId::sortedDictionaryClass:
			case FuId::orderedDictionaryClass:
			case FuId::regexClass:
			case FuId::matchClass:
			case FuId::lockClass:
				break;
			default:
				if (dynamic_cast<const FuClass *>(def->parent)) {
					writeName(def);
					write(" = ");
					if (def->value == nullptr)
						writeNew(klass, FuPriority::argument);
					else
						writeCoercedExpr(def->type.get(), def->value.get());
					writeCharLine(';');
				}
				GenBase::writeInitCode(def);
				break;
			}
		}
	}
}

void GenD::writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent)
{
	write("new ");
	writeType(elementType, false);
	writeChar('[');
	lengthExpr->accept(this, FuPriority::argument);
	writeChar(']');
}

void GenD::writeStaticInitializer(const FuType * type)
{
	writeChar('(');
	writeType(type, false);
	write(").init");
}

void GenD::writeNew(const FuReadWriteClassType * klass, FuPriority parent)
{
	if (isCreateWithNew(klass)) {
		write("new ");
		writeType(klass, false);
	}
	else
		writeStaticInitializer(klass);
}

void GenD::writeResource(std::string_view name, int length)
{
	write("FuResource.");
	writeResourceName(name);
}

void GenD::writeStringLength(const FuExpr * expr)
{
	writePostfix(expr, ".length");
}

void GenD::writeClassReference(const FuExpr * expr, FuPriority priority)
{
	if (isStructPtr(expr->type.get())) {
		write("(*");
		expr->accept(this, priority);
		writeChar(')');
	}
	else
		expr->accept(this, priority);
}

void GenD::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::consoleError:
		write("stderr");
		break;
	case FuId::listCount:
	case FuId::stackCount:
	case FuId::hashSetCount:
	case FuId::dictionaryCount:
	case FuId::sortedSetCount:
	case FuId::sortedDictionaryCount:
		writeStringLength(expr->left.get());
		break;
	case FuId::queueCount:
		include("std.range");
		writeClassReference(expr->left.get());
		write("[].walkLength");
		break;
	case FuId::matchStart:
		writePostfix(expr->left.get(), ".pre.length");
		break;
	case FuId::matchEnd:
		if (parent > FuPriority::add)
			writeChar('(');
		writePostfix(expr->left.get(), ".pre.length + ");
		writePostfix(expr->left.get(), ".hit.length");
		if (parent > FuPriority::add)
			writeChar(')');
		break;
	case FuId::matchLength:
		writePostfix(expr->left.get(), ".hit.length");
		break;
	case FuId::matchValue:
		writePostfix(expr->left.get(), ".hit");
		break;
	case FuId::mathNaN:
		write("double.nan");
		break;
	case FuId::mathNegativeInfinity:
		write("-double.infinity");
		break;
	case FuId::mathPositiveInfinity:
		write("double.infinity");
		break;
	default:
		GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

void GenD::writeWrite(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine)
{
	include("std.stdio");
	if (std::ssize(*args) == 0)
		write("writeln()");
	else if (const FuInterpolatedString *interpolated = dynamic_cast<const FuInterpolatedString *>((*args)[0].get())) {
		write(newLine ? "writefln(" : "writef(");
		writePrintf(interpolated, false);
	}
	else
		writeCall(newLine ? "writeln" : "write", (*args)[0].get());
}

void GenD::writeSlice(const FuExpr * obj, const FuExpr * offset, const FuExpr * length)
{
	writeClassReference(obj, FuPriority::primary);
	writeChar('[');
	if (!offset->isLiteralZero() || length != nullptr) {
		offset->accept(this, FuPriority::argument);
		write(" .. ");
		if (length == nullptr)
			writeChar('$');
		else if (dynamic_cast<const FuLiteralLong *>(offset))
			writeAdd(offset, length);
		else {
			write("$][0 .. ");
			length->accept(this, FuPriority::argument);
		}
	}
	writeChar(']');
}

void GenD::writeInsertedArg(const FuType * type, const std::vector<std::shared_ptr<FuExpr>> * args, int index)
{
	if (std::ssize(*args) <= index) {
		const FuReadWriteClassType * klass = static_cast<const FuReadWriteClassType *>(type);
		writeNew(klass, FuPriority::argument);
	}
	else
		writeCoercedExpr(type, (*args)[index].get());
	writeChar(')');
}

void GenD::writeJsonElementIs(const FuExpr * obj, std::string_view name, FuPriority parent)
{
	if (parent > FuPriority::equality)
		writeChar('(');
	writePostfix(obj, ".type == JSONType.");
	write(name);
	if (parent > FuPriority::equality)
		writeChar(')');
}

void GenD::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case FuId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case FuId::intTryParse:
	case FuId::longTryParse:
	case FuId::doubleTryParse:
		include("std.conv");
		write("() { try { ");
		writePostfix(obj, " = ");
		writePostfix((*args)[0].get(), ".to!");
		write(obj->type->name);
		if (std::ssize(*args) == 2) {
			writeChar('(');
			(*args)[1]->accept(this, FuPriority::argument);
			writeChar(')');
		}
		write("; return true; } catch (ConvException e) return false; }()");
		break;
	case FuId::stringContains:
		include("std.algorithm");
		writeMethodCall(obj, "canFind", (*args)[0].get());
		break;
	case FuId::stringEndsWith:
		include("std.string");
		writeMethodCall(obj, "endsWith", (*args)[0].get());
		break;
	case FuId::stringIndexOf:
		include("std.string");
		writeMethodCall(obj, "indexOf", (*args)[0].get());
		break;
	case FuId::stringLastIndexOf:
		include("std.string");
		writeMethodCall(obj, "lastIndexOf", (*args)[0].get());
		break;
	case FuId::stringReplace:
		include("std.string");
		writeMethodCall(obj, "replace", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::stringStartsWith:
		include("std.string");
		writeMethodCall(obj, "startsWith", (*args)[0].get());
		break;
	case FuId::stringSubstring:
		writeSlice(obj, (*args)[0].get(), std::ssize(*args) == 2 ? (*args)[1].get() : nullptr);
		break;
	case FuId::stringToLower:
		include("std.uni");
		writePostfix(obj, ".toLower()");
		break;
	case FuId::stringToUpper:
		include("std.uni");
		writePostfix(obj, ".toUpper()");
		break;
	case FuId::arrayBinarySearchAll:
	case FuId::arrayBinarySearchPart:
		include("std.range");
		write("() { size_t fubegin = ");
		if (std::ssize(*args) == 3)
			(*args)[1]->accept(this, FuPriority::argument);
		else
			writeChar('0');
		write("; auto fusearch = ");
		writeClassReference(obj);
		writeChar('[');
		if (std::ssize(*args) == 3) {
			write("fubegin .. fubegin + ");
			(*args)[2]->accept(this, FuPriority::add);
		}
		write("].assumeSorted.trisect(");
		writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		write("); return fusearch[1].length ? fubegin + fusearch[0].length : -1; }()");
		break;
	case FuId::arrayContains:
	case FuId::listContains:
		include("std.algorithm");
		writeClassReference(obj);
		writeCall("[].canFind", (*args)[0].get());
		break;
	case FuId::arrayCopyTo:
	case FuId::listCopyTo:
		include("std.algorithm");
		writeSlice(obj, (*args)[0].get(), (*args)[3].get());
		write(".copy(");
		writeSlice((*args)[1].get(), (*args)[2].get(), nullptr);
		writeChar(')');
		break;
	case FuId::arrayFillAll:
	case FuId::arrayFillPart:
		include("std.algorithm");
		if (std::ssize(*args) == 3)
			writeSlice(obj, (*args)[1].get(), (*args)[2].get());
		else {
			writeClassReference(obj);
			write("[]");
		}
		write(".fill(");
		writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		writeChar(')');
		break;
	case FuId::arraySortAll:
	case FuId::arraySortPart:
	case FuId::listSortAll:
	case FuId::listSortPart:
		include("std.algorithm");
		if (std::ssize(*args) == 2)
			writeSlice(obj, (*args)[0].get(), (*args)[1].get());
		else {
			writeClassReference(obj);
			write("[]");
		}
		write(".sort");
		break;
	case FuId::listAdd:
	case FuId::queueEnqueue:
		writePostfix(obj, ".insertBack(");
		writeInsertedArg(obj->type->asClassType()->getElementType().get(), args);
		break;
	case FuId::listAddRange:
		writeClassReference(obj);
		write(" ~= ");
		writeClassReference((*args)[0].get());
		write("[]");
		break;
	case FuId::listAll:
		include("std.algorithm");
		writeClassReference(obj);
		writeCall("[].all!", (*args)[0].get());
		break;
	case FuId::listAny:
		include("std.algorithm");
		writeClassReference(obj);
		write("[].any!(");
		(*args)[0]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::listInsert:
		this->hasListInsert = true;
		writePostfix(obj, ".insertInPlace(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		writeInsertedArg(obj->type->asClassType()->getElementType().get(), args, 1);
		break;
	case FuId::listLast:
		writePostfix(obj, ".back");
		break;
	case FuId::listRemoveAt:
	case FuId::listRemoveRange:
		this->hasListRemoveAt = true;
		writePostfix(obj, ".removeAt");
		writeInParentheses(args);
		break;
	case FuId::listIndexOf:
		include("std.algorithm");
		writeClassReference(obj);
		writeCall("[].countUntil", (*args)[0].get());
		break;
	case FuId::queueDequeue:
		this->hasQueueDequeue = true;
		include("std.container.dlist");
		writeClassReference(obj);
		write(".dequeue()");
		break;
	case FuId::queuePeek:
		writePostfix(obj, ".front");
		break;
	case FuId::stackPeek:
		writePostfix(obj, ".back");
		break;
	case FuId::stackPush:
		writeClassReference(obj);
		write(" ~= ");
		(*args)[0]->accept(this, FuPriority::assign);
		break;
	case FuId::stackPop:
		this->hasStackPop = true;
		writeClassReference(obj);
		write(".pop()");
		break;
	case FuId::hashSetAdd:
		writePostfix(obj, ".require(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", true)");
		break;
	case FuId::hashSetClear:
	case FuId::sortedSetClear:
	case FuId::dictionaryClear:
	case FuId::sortedDictionaryClear:
		writePostfix(obj, ".clear()");
		break;
	case FuId::hashSetContains:
	case FuId::sortedSetContains:
	case FuId::dictionaryContainsKey:
		writeChar('(');
		(*args)[0]->accept(this, FuPriority::rel);
		write(" in ");
		obj->accept(this, FuPriority::primary);
		writeChar(')');
		break;
	case FuId::sortedSetAdd:
		writePostfix(obj, ".insert(");
		writeInsertedArg(obj->type->asClassType()->getElementType().get(), args, 0);
		break;
	case FuId::sortedSetRemove:
		writeMethodCall(obj, "removeKey", (*args)[0].get());
		break;
	case FuId::dictionaryAdd:
		if (obj->type->asClassType()->class_->id == FuId::sortedDictionaryClass) {
			this->hasSortedDictionaryInsert = true;
			writePostfix(obj, ".replace(");
		}
		else
			writePostfix(obj, ".require(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		writeInsertedArg(obj->type->asClassType()->getValueType().get(), args, 1);
		break;
	case FuId::sortedDictionaryContainsKey:
		write("tuple(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		writeStaticInitializer(obj->type->asClassType()->getValueType().get());
		write(") in ");
		writeClassReference(obj);
		break;
	case FuId::sortedDictionaryRemove:
		writeClassReference(obj);
		write(".removeKey(tuple(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		writeStaticInitializer(obj->type->asClassType()->getValueType().get());
		write("))");
		break;
	case FuId::textWriterWrite:
	case FuId::textWriterWriteLine:
		writePostfix(obj, ".");
		writeWrite(args, method->id == FuId::textWriterWriteLine);
		break;
	case FuId::textWriterWriteChar:
		writePostfix(obj, ".write(");
		if (!dynamic_cast<const FuLiteralChar *>((*args)[0].get()))
			write("cast(char) ");
		(*args)[0]->accept(this, FuPriority::primary);
		writeChar(')');
		break;
	case FuId::textWriterWriteCodePoint:
		writePostfix(obj, ".write(cast(dchar) ");
		(*args)[0]->accept(this, FuPriority::primary);
		writeChar(')');
		break;
	case FuId::consoleWrite:
	case FuId::consoleWriteLine:
		writeWrite(args, method->id == FuId::consoleWriteLine);
		break;
	case FuId::environmentGetEnvironmentVariable:
		include("std.process");
		writeCall("environment.get", (*args)[0].get());
		break;
	case FuId::convertToBase64String:
		include("std.base64");
		write("Base64.encode(");
		if (isWholeArray((*args)[0].get(), (*args)[1].get(), (*args)[2].get()))
			(*args)[0]->accept(this, FuPriority::argument);
		else
			writeSlice((*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		writeChar(')');
		break;
	case FuId::uTF8GetByteCount:
		writePostfix((*args)[0].get(), ".length");
		break;
	case FuId::uTF8GetBytes:
		include("std.string");
		include("std.algorithm");
		writePostfix((*args)[0].get(), ".representation.copy(");
		writeSlice((*args)[1].get(), (*args)[2].get(), nullptr);
		writeChar(')');
		break;
	case FuId::uTF8GetString:
		write("cast(string) ");
		writeSlice((*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case FuId::regexCompile:
		include("std.regex");
		write("regex(");
		(*args)[0]->accept(this, FuPriority::argument);
		writeRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
		writeChar(')');
		break;
	case FuId::regexEscape:
		include("std.regex");
		include("std.conv");
		writePostfix((*args)[0].get(), ".escaper.to!string");
		break;
	case FuId::regexIsMatchRegex:
		include("std.regex");
		writePostfix((*args)[0].get(), ".matchFirst(");
		(std::ssize(*args) > 1 ? (*args)[1].get() : obj)->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::regexIsMatchStr:
		include("std.regex");
		writePostfix((*args)[0].get(), ".matchFirst(");
		if (getRegexOptions(args) != RegexOptions::none)
			write("regex(");
		(std::ssize(*args) > 1 ? (*args)[1].get() : obj)->accept(this, FuPriority::argument);
		writeRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
		writeChar(')');
		break;
	case FuId::matchFindStr:
		include("std.regex");
		writeChar('(');
		obj->accept(this, FuPriority::assign);
		write(" = ");
		(*args)[0]->accept(this, FuPriority::primary);
		write(".matchFirst(");
		if (getRegexOptions(args) != RegexOptions::none)
			write("regex(");
		(*args)[1]->accept(this, FuPriority::argument);
		writeRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
		write("))");
		break;
	case FuId::matchFindRegex:
		include("std.regex");
		writeChar('(');
		obj->accept(this, FuPriority::assign);
		write(" = ");
		writeMethodCall((*args)[0].get(), "matchFirst", (*args)[1].get());
		writeChar(')');
		break;
	case FuId::matchGetCapture:
		writeIndexing(obj, (*args)[0].get());
		break;
	case FuId::jsonElementParse:
		writeCall("parseJSON", (*args)[0].get());
		break;
	case FuId::jsonElementIsObject:
		writeJsonElementIs(obj, "object", parent);
		break;
	case FuId::jsonElementIsArray:
		writeJsonElementIs(obj, "array", parent);
		break;
	case FuId::jsonElementIsString:
		writeJsonElementIs(obj, "string", parent);
		break;
	case FuId::jsonElementIsNumber:
		writeJsonElementIs(obj, "float_", parent);
		break;
	case FuId::jsonElementIsBoolean:
		if (parent > FuPriority::condOr)
			writeChar('(');
		writePostfix(obj, ".type == JSONType.true_ || ");
		writePostfix(obj, ".type == JSONType.false_");
		if (parent > FuPriority::condOr)
			writeChar(')');
		break;
	case FuId::jsonElementIsNull:
		writeJsonElementIs(obj, "null_", parent);
		break;
	case FuId::jsonElementGetObject:
		writePostfix(obj, ".object");
		break;
	case FuId::jsonElementGetArray:
		writePostfix(obj, ".array");
		break;
	case FuId::jsonElementGetString:
		writePostfix(obj, ".str");
		break;
	case FuId::jsonElementGetDouble:
		writePostfix(obj, ".get!double");
		break;
	case FuId::jsonElementGetBoolean:
		writePostfix(obj, ".boolean");
		break;
	case FuId::mathMethod:
	case FuId::mathAbs:
	case FuId::mathIsFinite:
	case FuId::mathIsInfinity:
	case FuId::mathIsNaN:
	case FuId::mathLog2:
	case FuId::mathRound:
		include("std.math");
		writeCamelCase(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathCeiling:
		include("std.math");
		writeCall("ceil", (*args)[0].get());
		break;
	case FuId::mathClamp:
	case FuId::mathMax:
	case FuId::mathMin:
		include("std.algorithm");
		writeLowercase(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathFusedMultiplyAdd:
		include("std.math");
		writeCall("fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case FuId::mathTruncate:
		include("std.math");
		writeCall("trunc", (*args)[0].get());
		break;
	default:
		if (obj != nullptr) {
			if (isReferenceTo(obj, FuId::basePtr))
				write("super.");
			else {
				writeClassReference(obj);
				writeChar('.');
			}
		}
		writeName(method);
		writeCoercedArgsInParentheses(method, args);
		break;
	}
}

void GenD::writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	writeClassReference(expr->left.get());
	const FuClassType * klass = static_cast<const FuClassType *>(expr->left->type.get());
	switch (klass->class_->id) {
	case FuId::arrayPtrClass:
	case FuId::arrayStorageClass:
	case FuId::dictionaryClass:
	case FuId::listClass:
		writeChar('[');
		expr->right->accept(this, FuPriority::argument);
		writeChar(']');
		break;
	case FuId::sortedDictionaryClass:
		assert(parent != FuPriority::assign);
		this->hasSortedDictionaryFind = true;
		include("std.container.rbtree");
		include("std.typecons");
		write(".find(");
		writeStronglyCoerced(klass->getKeyType(), expr->right.get());
		writeChar(')');
		break;
	case FuId::orderedDictionaryClass:
		notSupported(expr, "OrderedDictionary");
		break;
	default:
		std::abort();
	}
}

bool GenD::isIsComparable(const FuExpr * expr)
{
	const FuClassType * klass;
	return dynamic_cast<const FuLiteralNull *>(expr) || ((klass = dynamic_cast<const FuClassType *>(expr->type.get())) && klass->class_->id == FuId::arrayPtrClass);
}

void GenD::writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	if (isIsComparable(left) || isIsComparable(right))
		writeEqualExpr(left, right, parent, not_ ? " !is " : " is ");
	else
		GenCCppD::writeEqual(left, right, parent, not_);
}

void GenD::writeAssign(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuBinaryExpr * indexing;
	const FuClassType * dict;
	if ((indexing = dynamic_cast<const FuBinaryExpr *>(expr->left.get())) && indexing->op == FuToken::leftBracket && (dict = dynamic_cast<const FuClassType *>(indexing->left->type.get()))) {
		switch (dict->class_->id) {
		case FuId::sortedDictionaryClass:
			this->hasSortedDictionaryInsert = true;
			writePostfix(indexing->left.get(), ".replace(");
			indexing->right->accept(this, FuPriority::argument);
			write(", ");
			writeNotPromoted(expr->type.get(), expr->right.get());
			writeChar(')');
			return;
		default:
			break;
		}
	}
	GenBase::writeAssign(expr, parent);
}

void GenD::writeIsVar(const FuExpr * left, const FuExpr * right, FuPriority parent)
{
	if (parent > FuPriority::equality)
		writeChar('(');
	const FuSymbolReference * symbol;
	const FuClass * klass;
	if ((symbol = dynamic_cast<const FuSymbolReference *>(right)) && (klass = dynamic_cast<const FuClass *>(symbol->symbol))) {
		write("cast(");
		write(klass->name);
		write(") ");
		left->accept(this, FuPriority::primary);
	}
	else if (const FuVar *def = dynamic_cast<const FuVar *>(right)) {
		writeChar('(');
		writeName(def);
		write(" = cast(");
		write(def->type->name);
		write(") ");
		left->accept(this, FuPriority::primary);
		writeChar(')');
	}
	else
		std::abort();
	write(" !is null");
	if (parent > FuPriority::equality)
		writeChar(')');
}

void GenD::visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::is:
		writeIsVar(expr->left.get(), expr->right.get(), parent >= FuPriority::or_ && parent <= FuPriority::mul ? FuPriority::primary : parent);
		return;
	case FuToken::plus:
		if (expr->type->id == FuId::stringStorageType) {
			expr->left->accept(this, FuPriority::assign);
			write(" ~ ");
			expr->right->accept(this, FuPriority::assign);
			return;
		}
		break;
	case FuToken::addAssign:
		if (expr->left->type->id == FuId::stringStorageType) {
			expr->left->accept(this, FuPriority::assign);
			write(" ~= ");
			writeAssignRight(expr);
			return;
		}
		break;
	default:
		break;
	}
	GenBase::visitBinaryExpr(expr, parent);
}

void GenD::visitLambdaExpr(const FuLambdaExpr * expr)
{
	writeName(expr->first);
	write(" => ");
	expr->body->accept(this, FuPriority::statement);
}

void GenD::writeAssert(const FuAssert * statement)
{
	write("assert(");
	statement->cond->accept(this, FuPriority::argument);
	if (statement->message != nullptr) {
		write(", ");
		statement->message->accept(this, FuPriority::argument);
	}
	writeLine(");");
}

void GenD::visitForeach(const FuForeach * statement)
{
	write("foreach (");
	const FuClassType * dict;
	if ((dict = dynamic_cast<const FuClassType *>(statement->collection->type.get())) && dict->class_->typeParameterCount == 2) {
		writeTypeAndName(statement->getVar());
		write(", ");
		writeTypeAndName(statement->getValueVar());
	}
	else
		writeTypeAndName(statement->getVar());
	write("; ");
	writeClassReference(statement->collection.get());
	const FuClassType * set;
	if ((set = dynamic_cast<const FuClassType *>(statement->collection->type.get())) && set->class_->id == FuId::hashSetClass)
		write(".byKey");
	writeChar(')');
	writeChild(statement->body.get());
}

void GenD::visitLock(const FuLock * statement)
{
	writeCall("synchronized ", statement->lock.get());
	writeChild(statement->body.get());
}

void GenD::writeSwitchCaseTypeVar(const FuExpr * value)
{
	defineVar(value);
}

void GenD::writeSwitchCaseCond(const FuSwitch * statement, const FuExpr * value, FuPriority parent)
{
	const FuSymbolReference * symbol;
	if ((symbol = dynamic_cast<const FuSymbolReference *>(value)) && dynamic_cast<const FuClass *>(symbol->symbol))
		writeIsVar(statement->value.get(), value, parent);
	else if (dynamic_cast<const FuVar *>(value))
		writeIsVar(statement->value.get(), value, parent);
	else
		GenBase::writeSwitchCaseCond(statement, value, parent);
}

void GenD::visitSwitch(const FuSwitch * statement)
{
	writeTemporaries(statement->value.get());
	if (statement->isTypeMatching() || statement->hasWhen())
		writeSwitchAsIfsWithGoto(statement);
	else {
		startSwitch(statement);
		writeLine("default:");
		this->indent++;
		if (std::ssize(statement->defaultBody) > 0)
			writeSwitchCaseBody(&statement->defaultBody);
		else
			writeLine("assert(false);");
		this->indent--;
		writeCharLine('}');
	}
}

void GenD::writeEnum(const FuEnum * enu)
{
	writeNewLine();
	writeDoc(enu->documentation.get());
	writePublic(enu);
	write("enum ");
	write(enu->name);
	openBlock();
	enu->acceptValues(this);
	writeNewLine();
	closeBlock();
}

void GenD::writeConst(const FuConst * konst)
{
	writeDoc(konst->documentation.get());
	write("static immutable ");
	writeTypeAndName(konst);
	write(" = ");
	writeCoercedExpr(konst->type.get(), konst->value.get());
	writeCharLine(';');
}

void GenD::writeField(const FuField * field)
{
	writeNewLine();
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	writeTypeAndName(field);
	if (dynamic_cast<const FuLiteral *>(field->value.get())) {
		write(" = ");
		writeCoercedExpr(field->type.get(), field->value.get());
	}
	writeCharLine(';');
}

bool GenD::isShortMethod(const FuMethod * method) const
{
	const FuReturn * ret;
	return (ret = dynamic_cast<const FuReturn *>(method->body.get())) && !hasTemporaries(ret->value.get());
}

void GenD::writeMethod(const FuMethod * method)
{
	if (method->id == FuId::classToString && method->callType == FuCallType::abstract)
		return;
	writeNewLine();
	writeDoc(method->documentation.get());
	writeParametersAndThrowsDoc(method);
	writeVisibility(method->visibility);
	if (method->id == FuId::classToString)
		write("override ");
	else
		writeCallType(method->callType, "final override ");
	writeTypeAndName(method);
	writeParameters(method, true);
	writeBody(method);
}

void GenD::writeClass(const FuClass * klass, const FuProgram * program)
{
	writeNewLine();
	writeDoc(klass->documentation.get());
	if (klass->callType == FuCallType::sealed)
		write("final ");
	openClass(klass, "", " : ");
	if (needsConstructor(klass)) {
		if (klass->constructor != nullptr) {
			writeDoc(klass->constructor->documentation.get());
			writeVisibility(klass->constructor->visibility);
		}
		else
			write("private ");
		writeLine("this()");
		openBlock();
		writeConstructorBody(klass);
		closeBlock();
	}
	else if (klass->id == FuId::exceptionClass) {
		include("std.exception");
		writeLine("mixin basicExceptionCtors;");
	}
	writeMembers(klass, false);
	closeBlock();
}

bool GenD::isLong(const FuSymbolReference * expr)
{
	switch (expr->symbol->id) {
	case FuId::arrayLength:
	case FuId::stringLength:
	case FuId::listCount:
		return true;
	default:
		return false;
	}
}

void GenD::writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	if (dynamic_cast<const FuRangeType *>(type))
		writeStaticCast(type, expr);
	else {
		const FuSymbolReference * symref;
		if (dynamic_cast<const FuIntegerType *>(type) && (symref = dynamic_cast<const FuSymbolReference *>(expr)) && isLong(symref))
			writeStaticCast(type, expr);
		else if (dynamic_cast<const FuFloatingType *>(type) && !dynamic_cast<const FuFloatingType *>(expr->type.get()))
			writeStaticCast(type, expr);
		else if (dynamic_cast<const FuClassType *>(type) && !dynamic_cast<const FuArrayStorageType *>(type) && dynamic_cast<const FuArrayStorageType *>(expr->type.get())) {
			GenTyped::writeCoercedInternal(type, expr, FuPriority::primary);
			write("[]");
		}
		else
			GenTyped::writeCoercedInternal(type, expr, parent);
	}
}

void GenD::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	writeNewLine();
	writeLine("private static struct FuResource");
	openBlock();
	for (const auto &[name, content] : *resources) {
		write("private static immutable ubyte[] ");
		writeResourceName(name);
		writeLine(" = [");
		writeChar('\t');
		writeBytes(&content);
		writeLine(" ];");
	}
	closeBlock();
}

void GenD::writeMain(const FuMethod * main)
{
	writeNewLine();
	writeType(main->type.get(), true);
	if (main->parameters.count() == 1) {
		write(" main(string[] args) => ");
		writeName(main->parent);
		writeLine(".main(args[1 .. $]);");
	}
	else {
		write(" main() => ");
		if (!this->namespace_.empty()) {
			write(this->namespace_);
			writeChar('.');
		}
		writeName(main->parent);
		writeLine(".main();");
	}
}

void GenD::writeProgram(const FuProgram * program)
{
	this->hasListInsert = false;
	this->hasListRemoveAt = false;
	this->hasQueueDequeue = false;
	this->hasStackPop = false;
	this->hasSortedDictionaryInsert = false;
	this->hasSortedDictionaryFind = false;
	openStringWriter();
	if (!this->namespace_.empty()) {
		write("struct ");
		writeLine(this->namespace_);
		openBlock();
		writeLine("static:");
	}
	writeTopLevelNatives(program);
	writeTypes(program);
	if (std::ssize(program->resources) > 0)
		writeResources(&program->resources);
	if (!this->namespace_.empty())
		closeBlock();
	createOutputFile();
	if (this->hasListInsert || this->hasListRemoveAt || this->hasStackPop)
		include("std.container.array");
	if (this->hasSortedDictionaryInsert) {
		include("std.container.rbtree");
		include("std.typecons");
	}
	writeIncludes("import ", ";");
	if (this->hasListInsert) {
		writeNewLine();
		writeLine("private void insertInPlace(T, U...)(Array!T* arr, size_t pos, auto ref U stuff)");
		openBlock();
		writeLine("arr.insertAfter((*arr)[0 .. pos], stuff);");
		closeBlock();
	}
	if (this->hasListRemoveAt) {
		writeNewLine();
		writeLine("private void removeAt(T)(Array!T* arr, size_t pos, size_t count = 1)");
		openBlock();
		writeLine("arr.linearRemove((*arr)[pos .. pos + count]);");
		closeBlock();
	}
	if (this->hasQueueDequeue) {
		writeNewLine();
		writeLine("private T dequeue(T)(ref DList!T q)");
		openBlock();
		writeLine("scope(exit) q.removeFront(); return q.front;");
		closeBlock();
	}
	if (this->hasStackPop) {
		writeNewLine();
		writeLine("private T pop(T)(ref Array!T stack)");
		openBlock();
		writeLine("scope(exit) stack.removeBack(); return stack.back;");
		closeBlock();
	}
	if (this->hasSortedDictionaryFind) {
		writeNewLine();
		writeLine("private U find(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key)");
		openBlock();
		writeLine("return dict.equalRange(tuple(key, U.init)).front[1];");
		closeBlock();
	}
	if (this->hasSortedDictionaryInsert) {
		writeNewLine();
		writeLine("private void replace(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key, lazy U value)");
		openBlock();
		writeLine("dict.removeKey(tuple(key, U.init));");
		writeLine("dict.insert(tuple(key, value));");
		closeBlock();
	}
	closeStringWriter();
	if (program->main != nullptr)
		writeMain(program->main);
	closeFile();
}

std::string_view GenJava::getTargetName() const
{
	return "Java";
}

void GenJava::visitLiteralLong(int64_t value)
{
	GenBase::visitLiteralLong(value);
	if (value < -2147483648 || value > 2147483647)
		writeChar('L');
}

int GenJava::getLiteralChars() const
{
	return 65536;
}

void GenJava::writeToString(const FuExpr * expr, FuPriority parent)
{
	switch (expr->type->id) {
	case FuId::longType:
		write("Long");
		break;
	case FuId::floatType:
		write("Float");
		break;
	case FuId::doubleType:
	case FuId::floatIntType:
		write("Double");
		break;
	case FuId::stringPtrType:
	case FuId::stringStorageType:
		expr->accept(this, parent);
		return;
	default:
		if (dynamic_cast<const FuIntegerType *>(expr->type.get()))
			write("Integer");
		else if (dynamic_cast<const FuClassType *>(expr->type.get())) {
			writePostfix(expr, ".toString()");
			return;
		}
		else
			std::abort();
		break;
	}
	writeCall(".toString", expr);
}

void GenJava::writePrintfWidth(const FuInterpolatedPart * part)
{
	if (part->precision >= 0 && dynamic_cast<const FuIntegerType *>(part->argument->type.get())) {
		writeChar('0');
		visitLiteralLong(part->precision);
	}
	else
		GenBase::writePrintfWidth(part);
}

void GenJava::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	if (expr->suffix.empty() && std::ssize(expr->parts) == 1 && expr->parts[0].prefix.empty() && expr->parts[0].widthExpr == nullptr && expr->parts[0].format == ' ')
		writeToString(expr->parts[0].argument.get(), parent);
	else {
		write("String.format(");
		writePrintf(expr, false);
	}
}

void GenJava::writeCamelCaseNotKeyword(std::string_view name)
{
	writeCamelCase(name);
	if (name == "Abstract" || name == "Assert" || name == "Boolean" || name == "Break" || name == "Byte" || name == "Case" || name == "Catch" || name == "Char" || name == "Class" || name == "Const" || name == "Continue" || name == "Default" || name == "Do" || name == "Double" || name == "Else" || name == "Enum" || name == "Extends" || name == "False" || name == "Final" || name == "Finally" || name == "Float" || name == "For" || name == "Foreach" || name == "Goto" || name == "If" || name == "Implements" || name == "Import" || name == "Instanceof" || name == "Int" || name == "Interface" || name == "Long" || name == "Native" || name == "New" || name == "Null" || name == "Package" || name == "Private" || name == "Protected" || name == "Public" || name == "Return" || name == "Short" || name == "Static" || name == "Strictfp" || name == "String" || name == "Super" || name == "Switch" || name == "Synchronized" || name == "Transient" || name == "Throw" || name == "Throws" || name == "True" || name == "Try" || name == "Void" || name == "Volatile" || name == "While" || name == "Yield" || name == "boolean" || name == "catch" || name == "char" || name == "extends" || name == "final" || name == "finally" || name == "goto" || name == "implements" || name == "import" || name == "instanceof" || name == "interface" || name == "package" || name == "private" || name == "strictfp" || name == "super" || name == "synchronized" || name == "transient" || name == "try" || name == "volatile" || name == "yield")
		writeChar('_');
}

void GenJava::writeName(const FuSymbol * symbol)
{
	if (dynamic_cast<const FuContainerType *>(symbol))
		write(symbol->name);
	else if (const FuConst *konst = dynamic_cast<const FuConst *>(symbol))
		writeUppercaseConstName(konst);
	else if (dynamic_cast<const FuVar *>(symbol)) {
		const FuForeach * forEach;
		if ((forEach = dynamic_cast<const FuForeach *>(symbol->parent)) && forEach->count() == 2) {
			const FuVar * element = forEach->getVar();
			writeCamelCaseNotKeyword(element->name);
			write(symbol == element ? ".getKey()" : ".getValue()");
		}
		else
			writeCamelCaseNotKeyword(symbol->name);
	}
	else if (dynamic_cast<const FuMember *>(symbol))
		writeCamelCaseNotKeyword(symbol->name);
	else
		std::abort();
}

void GenJava::writeVisibility(FuVisibility visibility)
{
	switch (visibility) {
	case FuVisibility::private_:
		write("private ");
		break;
	case FuVisibility::internal:
		break;
	case FuVisibility::protected_:
		write("protected ");
		break;
	case FuVisibility::public_:
		write("public ");
		break;
	default:
		std::abort();
	}
}

FuId GenJava::getTypeId(const FuType * type, bool promote) const
{
	FuId id = GenBase::getTypeId(type, promote);
	switch (id) {
	case FuId::byteRange:
		return FuId::sByteRange;
	case FuId::uShortRange:
		return FuId::intType;
	default:
		return id;
	}
}

bool GenJava::isJavaEnum(const FuEnum * enu)
{
	for (const FuSymbol * symbol = enu->first; symbol != nullptr; symbol = symbol->next) {
		const FuConst * konst;
		if ((konst = dynamic_cast<const FuConst *>(symbol)) && !dynamic_cast<const FuImplicitEnumValue *>(konst->value.get()))
			return false;
	}
	return true;
}

void GenJava::writeCollectionType(std::string_view name, const FuType * elementType)
{
	include("java.util." + std::string(name));
	write(name);
	writeChar('<');
	writeJavaType(elementType, false, true);
	writeChar('>');
}

void GenJava::writeDictType(std::string_view name, const FuClassType * dict)
{
	write(name);
	writeChar('<');
	writeJavaType(dict->getKeyType(), false, true);
	write(", ");
	writeJavaType(dict->getValueType().get(), false, true);
	writeChar('>');
}

void GenJava::writeJavaType(const FuType * type, bool promote, bool needClass)
{
	if (dynamic_cast<const FuNumericType *>(type)) {
		switch (getTypeId(type, promote)) {
		case FuId::sByteRange:
			write(needClass ? "Byte" : "byte");
			break;
		case FuId::shortRange:
			write(needClass ? "Short" : "short");
			break;
		case FuId::intType:
		case FuId::nIntType:
			write(needClass ? "Integer" : "int");
			break;
		case FuId::longType:
			write(needClass ? "Long" : "long");
			break;
		case FuId::floatType:
			write(needClass ? "Float" : "float");
			break;
		case FuId::doubleType:
			write(needClass ? "Double" : "double");
			break;
		default:
			std::abort();
		}
	}
	else if (const FuEnum *enu = dynamic_cast<const FuEnum *>(type))
		write(enu->id == FuId::boolType ? needClass ? "Boolean" : "boolean" : isJavaEnum(enu) ? enu->name : needClass ? "Integer" : "int");
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		switch (klass->class_->id) {
		case FuId::stringClass:
			write("String");
			break;
		case FuId::arrayPtrClass:
		case FuId::arrayStorageClass:
			writeType(klass->getElementType().get(), false);
			write("[]");
			break;
		case FuId::listClass:
			writeCollectionType("ArrayList", klass->getElementType().get());
			break;
		case FuId::queueClass:
			writeCollectionType("ArrayDeque", klass->getElementType().get());
			break;
		case FuId::stackClass:
			writeCollectionType("Stack", klass->getElementType().get());
			break;
		case FuId::priorityQueueClass:
			writeCollectionType("PriorityQueue", klass->getElementType().get());
			break;
		case FuId::hashSetClass:
			writeCollectionType("HashSet", klass->getElementType().get());
			break;
		case FuId::sortedSetClass:
			writeCollectionType("TreeSet", klass->getElementType().get());
			break;
		case FuId::dictionaryClass:
			include("java.util.HashMap");
			writeDictType("HashMap", klass);
			break;
		case FuId::sortedDictionaryClass:
			include("java.util.TreeMap");
			writeDictType("TreeMap", klass);
			break;
		case FuId::orderedDictionaryClass:
			include("java.util.LinkedHashMap");
			writeDictType("LinkedHashMap", klass);
			break;
		case FuId::textWriterClass:
			write("Appendable");
			break;
		case FuId::stringWriterClass:
			include("java.io.StringWriter");
			write("StringWriter");
			break;
		case FuId::regexClass:
			include("java.util.regex.Pattern");
			write("Pattern");
			break;
		case FuId::matchClass:
			include("java.util.regex.Matcher");
			write("Matcher");
			break;
		case FuId::lockClass:
			write("Object");
			break;
		default:
			write(klass->class_->name);
			break;
		}
	}
	else
		write(type->name);
}

void GenJava::writeType(const FuType * type, bool promote)
{
	writeJavaType(type, promote, false);
}

void GenJava::writeNew(const FuReadWriteClassType * klass, FuPriority parent)
{
	write("new ");
	writeType(klass, false);
	write("()");
}

void GenJava::writeResource(std::string_view name, int length)
{
	write("FuResource.getByteArray(");
	visitLiteralString(name);
	write(", ");
	visitLiteralLong(length);
	writeChar(')');
}

bool GenJava::isUnsignedByte(const FuType * type)
{
	const FuRangeType * range;
	return type->id == FuId::byteRange && (range = dynamic_cast<const FuRangeType *>(type)) && range->max > 127;
}

bool GenJava::isUnsignedByteIndexing(const FuExpr * expr)
{
	return expr->isIndexing() && isUnsignedByte(expr->type.get());
}

void GenJava::writeIndexingInternal(const FuBinaryExpr * expr)
{
	if (expr->left->type->isArray())
		GenBase::writeIndexingExpr(expr, FuPriority::and_);
	else
		writeMethodCall(expr->left.get(), "get", expr->right.get());
}

void GenJava::visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent)
{
	if ((expr->op == FuToken::increment || expr->op == FuToken::decrement) && isUnsignedByteIndexing(expr->inner.get())) {
		if (parent > FuPriority::and_)
			writeChar('(');
		write(expr->op == FuToken::increment ? "++" : "--");
		const FuBinaryExpr * indexing = static_cast<const FuBinaryExpr *>(expr->inner.get());
		writeIndexingInternal(indexing);
		if (parent != FuPriority::statement)
			write(" & 0xff");
		if (parent > FuPriority::and_)
			writeChar(')');
	}
	else
		GenBase::visitPrefixExpr(expr, parent);
}

void GenJava::visitPostfixExpr(const FuPostfixExpr * expr, FuPriority parent)
{
	if ((expr->op == FuToken::increment || expr->op == FuToken::decrement) && isUnsignedByteIndexing(expr->inner.get())) {
		if (parent > FuPriority::and_)
			writeChar('(');
		const FuBinaryExpr * indexing = static_cast<const FuBinaryExpr *>(expr->inner.get());
		writeIndexingInternal(indexing);
		write(expr->op == FuToken::increment ? "++" : "--");
		if (parent != FuPriority::statement)
			write(" & 0xff");
		if (parent > FuPriority::and_)
			writeChar(')');
	}
	else
		GenBase::visitPostfixExpr(expr, parent);
}

void GenJava::writeSByteLiteral(const FuLiteralLong * literal)
{
	if (literal->value >= 128)
		write("(byte) ");
	literal->accept(this, FuPriority::primary);
}

void GenJava::writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	if ((dynamic_cast<const FuStringType *>(left->type.get()) && right->type->id != FuId::nullType) || (dynamic_cast<const FuStringType *>(right->type.get()) && left->type->id != FuId::nullType)) {
		if (not_)
			writeChar('!');
		writeMethodCall(left, "equals", right);
	}
	else {
		const FuLiteralLong * rightLiteral;
		if (isUnsignedByteIndexing(left) && (rightLiteral = dynamic_cast<const FuLiteralLong *>(right)) && rightLiteral->type->id == FuId::byteRange) {
			if (parent > FuPriority::equality)
				writeChar('(');
			const FuBinaryExpr * indexing = static_cast<const FuBinaryExpr *>(left);
			writeIndexingInternal(indexing);
			write(getEqOp(not_));
			writeSByteLiteral(rightLiteral);
			if (parent > FuPriority::equality)
				writeChar(')');
		}
		else
			GenBase::writeEqual(left, right, parent, not_);
	}
}

void GenJava::writeCoercedLiteral(const FuType * type, const FuExpr * expr)
{
	if (isUnsignedByte(type)) {
		const FuLiteralLong * literal = static_cast<const FuLiteralLong *>(expr);
		writeSByteLiteral(literal);
	}
	else
		GenTyped::writeCoercedLiteral(type, expr);
}

void GenJava::writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	const FuCallExpr * call;
	if (type->id == FuId::floatType && (call = dynamic_cast<const FuCallExpr *>(expr)) && call->method->symbol->type->id == FuId::floatingType)
		writeStaticCast(type, expr);
	else
		GenTyped::writeCoercedInternal(type, expr, parent);
}

void GenJava::writeRel(const FuBinaryExpr * expr, FuPriority parent, std::string_view op)
{
	const FuEnum * enu;
	if ((enu = dynamic_cast<const FuEnum *>(expr->left->type.get())) && isJavaEnum(enu)) {
		if (parent > FuPriority::condAnd)
			writeChar('(');
		writeMethodCall(expr->left.get(), "compareTo", expr->right.get());
		write(op);
		writeChar('0');
		if (parent > FuPriority::condAnd)
			writeChar(')');
	}
	else
		GenBase::writeRel(expr, parent, op);
}

void GenJava::writeAnd(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuLiteralLong * rightLiteral;
	if (isUnsignedByteIndexing(expr->left.get()) && (rightLiteral = dynamic_cast<const FuLiteralLong *>(expr->right.get()))) {
		if (parent > FuPriority::condAnd && parent != FuPriority::and_)
			writeChar('(');
		const FuBinaryExpr * indexing = static_cast<const FuBinaryExpr *>(expr->left.get());
		writeIndexingInternal(indexing);
		write(" & ");
		visitLiteralLong(255 & rightLiteral->value);
		if (parent > FuPriority::condAnd && parent != FuPriority::and_)
			writeChar(')');
	}
	else
		GenBase::writeAnd(expr, parent);
}

void GenJava::writeStringLength(const FuExpr * expr)
{
	writePostfix(expr, ".length()");
}

void GenJava::writeCharAt(const FuBinaryExpr * expr)
{
	writeMethodCall(expr->left.get(), "charAt", expr->right.get());
}

void GenJava::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::consoleError:
		write("System.err");
		break;
	case FuId::listCount:
	case FuId::queueCount:
	case FuId::stackCount:
	case FuId::priorityQueueCount:
	case FuId::hashSetCount:
	case FuId::sortedSetCount:
	case FuId::dictionaryCount:
	case FuId::sortedDictionaryCount:
	case FuId::orderedDictionaryCount:
		expr->left->accept(this, FuPriority::primary);
		writeMemberOp(expr->left.get(), expr);
		write("size()");
		break;
	case FuId::mathNaN:
		write("Float.NaN");
		break;
	case FuId::mathNegativeInfinity:
		write("Float.NEGATIVE_INFINITY");
		break;
	case FuId::mathPositiveInfinity:
		write("Float.POSITIVE_INFINITY");
		break;
	default:
		if (!writeJavaMatchProperty(expr, parent))
			GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

void GenJava::writeArrayBinarySearchFill(const FuExpr * obj, std::string_view method, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	include("java.util.Arrays");
	write("Arrays.");
	write(method);
	writeChar('(');
	obj->accept(this, FuPriority::argument);
	write(", ");
	if (std::ssize(*args) == 3) {
		writeStartEnd((*args)[1].get(), (*args)[2].get());
		write(", ");
	}
	writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
	writeChar(')');
}

void GenJava::writeWrite(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine)
{
	const FuInterpolatedString * interpolated;
	if (std::ssize(*args) == 1 && (interpolated = dynamic_cast<const FuInterpolatedString *>((*args)[0].get()))) {
		write(".format(");
		writePrintf(interpolated, newLine);
	}
	else {
		write(".print");
		if (newLine)
			write("ln");
		writeCoercedArgsInParentheses(method, args);
	}
}

void GenJava::writeCompileRegex(const std::vector<std::shared_ptr<FuExpr>> * args, int argIndex)
{
	include("java.util.regex.Pattern");
	write("Pattern.compile(");
	(*args)[argIndex]->accept(this, FuPriority::argument);
	writeRegexOptions(args, ", ", " | ", "", "Pattern.CASE_INSENSITIVE", "Pattern.MULTILINE", "Pattern.DOTALL");
	writeChar(')');
}

void GenJava::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::none:
	case FuId::classToString:
	case FuId::stringContains:
	case FuId::stringEndsWith:
	case FuId::stringIndexOf:
	case FuId::stringLastIndexOf:
	case FuId::stringReplace:
	case FuId::stringStartsWith:
	case FuId::listClear:
	case FuId::listContains:
	case FuId::listIndexOf:
	case FuId::queueClear:
	case FuId::stackClear:
	case FuId::stackPeek:
	case FuId::stackPush:
	case FuId::stackPop:
	case FuId::priorityQueueClear:
	case FuId::hashSetAdd:
	case FuId::hashSetClear:
	case FuId::hashSetContains:
	case FuId::hashSetRemove:
	case FuId::sortedSetAdd:
	case FuId::sortedSetClear:
	case FuId::sortedSetContains:
	case FuId::sortedSetRemove:
	case FuId::dictionaryClear:
	case FuId::dictionaryContainsKey:
	case FuId::dictionaryRemove:
	case FuId::sortedDictionaryClear:
	case FuId::sortedDictionaryContainsKey:
	case FuId::sortedDictionaryRemove:
	case FuId::orderedDictionaryClear:
	case FuId::orderedDictionaryContainsKey:
	case FuId::orderedDictionaryRemove:
	case FuId::stringWriterToString:
	case FuId::mathMethod:
	case FuId::mathAbs:
	case FuId::mathMax:
	case FuId::mathMin:
		if (obj != nullptr) {
			if (isReferenceTo(obj, FuId::basePtr))
				write("super");
			else
				obj->accept(this, FuPriority::primary);
			writeChar('.');
		}
		writeName(method);
		writeCoercedArgsInParentheses(method, args);
		break;
	case FuId::enumFromInt:
		(*args)[0]->accept(this, parent);
		break;
	case FuId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case FuId::doubleTryParse:
		include("java.util.function.DoubleSupplier");
		write("!Double.isNaN(");
		obj->accept(this, FuPriority::assign);
		write(" = ((DoubleSupplier) () -> { try { return Double.parseDouble(");
		(*args)[0]->accept(this, FuPriority::argument);
		write("); } catch (NumberFormatException e) { return Double.NaN; } }).getAsDouble())");
		break;
	case FuId::stringSubstring:
		writePostfix(obj, ".substring(");
		(*args)[0]->accept(this, FuPriority::argument);
		if (std::ssize(*args) == 2) {
			write(", ");
			writeAdd((*args)[0].get(), (*args)[1].get());
		}
		writeChar(')');
		break;
	case FuId::stringToLower:
		writePostfix(obj, ".toLowerCase()");
		break;
	case FuId::stringToUpper:
		writePostfix(obj, ".toUpperCase()");
		break;
	case FuId::arrayBinarySearchAll:
	case FuId::arrayBinarySearchPart:
		writeArrayBinarySearchFill(obj, "binarySearch", args);
		break;
	case FuId::arrayCopyTo:
		write("System.arraycopy(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		writeCoercedArgs(method, args);
		writeChar(')');
		break;
	case FuId::arrayFillAll:
	case FuId::arrayFillPart:
		writeArrayBinarySearchFill(obj, "fill", args);
		break;
	case FuId::arraySortAll:
		include("java.util.Arrays");
		writeCall("Arrays.sort", obj);
		break;
	case FuId::arraySortPart:
		include("java.util.Arrays");
		write("Arrays.sort(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		writeChar(')');
		break;
	case FuId::listAdd:
		writeListAdd(obj, "add", args);
		break;
	case FuId::listAddRange:
		writeMethodCall(obj, "addAll", (*args)[0].get());
		break;
	case FuId::listAll:
		writeMethodCall(obj, "stream().allMatch", (*args)[0].get());
		break;
	case FuId::listAny:
		writeMethodCall(obj, "stream().anyMatch", (*args)[0].get());
		break;
	case FuId::listCopyTo:
		write("for (int _i = 0; _i < ");
		(*args)[3]->accept(this, FuPriority::rel);
		writeLine("; _i++)");
		write("\t");
		(*args)[1]->accept(this, FuPriority::primary);
		writeChar('[');
		startAdd((*args)[2].get());
		write("_i] = ");
		writePostfix(obj, ".get(");
		startAdd((*args)[0].get());
		write("_i)");
		break;
	case FuId::listInsert:
		writeListInsert(obj, "add", args);
		break;
	case FuId::listLast:
		writePostfix(obj, ".get(");
		writePostfix(obj, ".size() - 1)");
		break;
	case FuId::listRemoveAt:
		writeMethodCall(obj, "remove", (*args)[0].get());
		break;
	case FuId::listRemoveRange:
		writePostfix(obj, ".subList(");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		write(").clear()");
		break;
	case FuId::listSortAll:
		writePostfix(obj, ".sort(null)");
		break;
	case FuId::listSortPart:
		writePostfix(obj, ".subList(");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		write(").sort(null)");
		break;
	case FuId::queueDequeue:
	case FuId::priorityQueueDequeue:
		writePostfix(obj, ".remove()");
		break;
	case FuId::queueEnqueue:
	case FuId::priorityQueueEnqueue:
		writeMethodCall(obj, "add", (*args)[0].get());
		break;
	case FuId::queuePeek:
	case FuId::priorityQueuePeek:
		writePostfix(obj, ".element()");
		break;
	case FuId::dictionaryAdd:
		writePostfix(obj, ".put(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		writeNewStorage(obj->type->asClassType()->getValueType().get());
		writeChar(')');
		break;
	case FuId::textWriterWrite:
		if (isReferenceTo(obj, FuId::consoleError)) {
			write("System.err");
			writeWrite(method, args, false);
		}
		else if (obj->type->asClassType()->class_->id == FuId::stringWriterClass) {
			writePostfix(obj, ".append(");
			writeToString((*args)[0].get(), FuPriority::argument);
			writeChar(')');
		}
		else {
			write("try { ");
			writePostfix(obj, ".append(");
			writeToString((*args)[0].get(), FuPriority::argument);
			include("java.io.IOException");
			write("); } catch (IOException e) { throw new RuntimeException(e); }");
		}
		break;
	case FuId::textWriterWriteChar:
		if (isReferenceTo(obj, FuId::consoleError))
			writeCharMethodCall(obj, "print", (*args)[0].get());
		else if (obj->type->asClassType()->class_->id == FuId::stringWriterClass)
			writeCharMethodCall(obj, "append", (*args)[0].get());
		else {
			write("try { ");
			writeCharMethodCall(obj, "append", (*args)[0].get());
			include("java.io.IOException");
			write("; } catch (IOException e) { throw new RuntimeException(e); }");
		}
		break;
	case FuId::textWriterWriteCodePoint:
		if (isReferenceTo(obj, FuId::consoleError)) {
			writeCall("System.err.print(Character.toChars", (*args)[0].get());
			writeChar(')');
		}
		else if (obj->type->asClassType()->class_->id == FuId::stringWriterClass) {
			writeMethodCall(obj, "append(Character.toString", (*args)[0].get());
			writeChar(')');
		}
		else {
			write("try { ");
			writeMethodCall(obj, "append(Character.toString", (*args)[0].get());
			include("java.io.IOException");
			write("); } catch (IOException e) { throw new RuntimeException(e); }");
		}
		break;
	case FuId::textWriterWriteLine:
		if (isReferenceTo(obj, FuId::consoleError)) {
			write("System.err");
			writeWrite(method, args, true);
		}
		else {
			write("try { ");
			writePostfix(obj, ".append(");
			if (std::ssize(*args) == 0)
				write("'\\n'");
			else if (const FuInterpolatedString *interpolated = dynamic_cast<const FuInterpolatedString *>((*args)[0].get())) {
				write("String.format(");
				writePrintf(interpolated, true);
			}
			else {
				writeToString((*args)[0].get(), FuPriority::argument);
				write(").append('\\n'");
			}
			include("java.io.IOException");
			write("); } catch (IOException e) { throw new RuntimeException(e); }");
		}
		break;
	case FuId::stringWriterClear:
		writePostfix(obj, ".getBuffer().setLength(0)");
		break;
	case FuId::consoleWrite:
		write("System.out");
		writeWrite(method, args, false);
		break;
	case FuId::consoleWriteLine:
		write("System.out");
		writeWrite(method, args, true);
		break;
	case FuId::convertToBase64String:
		include("java.util.Base64");
		if (isWholeArray((*args)[0].get(), (*args)[1].get(), (*args)[2].get()))
			writeCall("Base64.getEncoder().encodeToString", (*args)[0].get());
		else {
			include("java.nio.ByteBuffer");
			writeCall("new String(Base64.getEncoder().encode(ByteBuffer.wrap", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
			write(").array())");
		}
		break;
	case FuId::uTF8GetByteCount:
		include("java.nio.charset.StandardCharsets");
		writePostfix((*args)[0].get(), ".getBytes(StandardCharsets.UTF_8).length");
		break;
	case FuId::uTF8GetBytes:
		include("java.nio.ByteBuffer");
		include("java.nio.CharBuffer");
		include("java.nio.charset.StandardCharsets");
		write("StandardCharsets.UTF_8.newEncoder().encode(CharBuffer.wrap(");
		(*args)[0]->accept(this, FuPriority::argument);
		write("), ByteBuffer.wrap(");
		(*args)[1]->accept(this, FuPriority::argument);
		write(", ");
		(*args)[2]->accept(this, FuPriority::argument);
		write(", ");
		writePostfix((*args)[1].get(), ".length");
		if (!(*args)[2]->isLiteralZero()) {
			write(" - ");
			(*args)[2]->accept(this, FuPriority::mul);
		}
		write("), true)");
		break;
	case FuId::uTF8GetString:
		include("java.nio.charset.StandardCharsets");
		write("new String(");
		writeCoercedArgs(method, args);
		write(", StandardCharsets.UTF_8)");
		break;
	case FuId::environmentGetEnvironmentVariable:
		writeCall("System.getenv", (*args)[0].get());
		break;
	case FuId::regexCompile:
		writeCompileRegex(args, 0);
		break;
	case FuId::regexEscape:
		include("java.util.regex.Pattern");
		writeCall("Pattern.quote", (*args)[0].get());
		break;
	case FuId::regexIsMatchStr:
		writeCompileRegex(args, 1);
		writeCall(".matcher", (*args)[0].get());
		write(".find()");
		break;
	case FuId::regexIsMatchRegex:
		writeMethodCall(obj, "matcher", (*args)[0].get());
		write(".find()");
		break;
	case FuId::matchFindStr:
	case FuId::matchFindRegex:
		writeChar('(');
		obj->accept(this, FuPriority::assign);
		write(" = ");
		if (method->id == FuId::matchFindStr)
			writeCompileRegex(args, 1);
		else
			(*args)[1]->accept(this, FuPriority::primary);
		writeCall(".matcher", (*args)[0].get());
		write(").find()");
		break;
	case FuId::matchGetCapture:
		writeMethodCall(obj, "group", (*args)[0].get());
		break;
	case FuId::mathCeiling:
		writeCall("Math.ceil", (*args)[0].get());
		break;
	case FuId::mathClamp:
		write("Math.min(Math.max(");
		writeClampAsMinMax(args);
		break;
	case FuId::mathFusedMultiplyAdd:
		writeCall("Math.fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case FuId::mathIsFinite:
		writeCall("Double.isFinite", (*args)[0].get());
		break;
	case FuId::mathIsInfinity:
		writeCall("Double.isInfinite", (*args)[0].get());
		break;
	case FuId::mathIsNaN:
		writeCall("Double.isNaN", (*args)[0].get());
		break;
	case FuId::mathLog2:
		if (parent > FuPriority::mul)
			writeChar('(');
		writeCall("Math.log", (*args)[0].get());
		write(" * 1.4426950408889635");
		if (parent > FuPriority::mul)
			writeChar(')');
		break;
	case FuId::mathRound:
		writeCall("Math.rint", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenJava::writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	if (parent != FuPriority::assign && isUnsignedByte(expr->type.get())) {
		if (parent > FuPriority::and_)
			writeChar('(');
		writeIndexingInternal(expr);
		write(" & 0xff");
		if (parent > FuPriority::and_)
			writeChar(')');
	}
	else
		writeIndexingInternal(expr);
}

bool GenJava::isPromoted(const FuExpr * expr) const
{
	return GenTyped::isPromoted(expr) || isUnsignedByteIndexing(expr);
}

void GenJava::writeAssignRight(const FuBinaryExpr * expr)
{
	const FuBinaryExpr * rightBinary;
	if (!isUnsignedByteIndexing(expr->left.get()) && (rightBinary = dynamic_cast<const FuBinaryExpr *>(expr->right.get())) && rightBinary->isAssign() && isUnsignedByte(expr->right->type.get())) {
		writeChar('(');
		GenTyped::writeAssignRight(expr);
		write(") & 0xff");
	}
	else
		GenTyped::writeAssignRight(expr);
}

void GenJava::writeAssign(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuBinaryExpr * indexing;
	const FuClassType * klass;
	if ((indexing = dynamic_cast<const FuBinaryExpr *>(expr->left.get())) && indexing->op == FuToken::leftBracket && (klass = dynamic_cast<const FuClassType *>(indexing->left->type.get())) && !klass->isArray()) {
		writePostfix(indexing->left.get(), klass->class_->id == FuId::listClass ? ".set(" : ".put(");
		indexing->right->accept(this, FuPriority::argument);
		write(", ");
		writeNotPromoted(expr->type.get(), expr->right.get());
		writeChar(')');
	}
	else
		GenBase::writeAssign(expr, parent);
}

std::string_view GenJava::getIsOperator() const
{
	return " instanceof ";
}

void GenJava::writeVar(const FuNamedValue * def)
{
	if (def->type->isFinal() && !def->isAssignableStorage())
		write("final ");
	GenBase::writeVar(def);
}

bool GenJava::hasInitCode(const FuNamedValue * def) const
{
	return (dynamic_cast<const FuArrayStorageType *>(def->type.get()) && dynamic_cast<const FuStorageType *>(def->type->getStorageType())) || GenBase::hasInitCode(def);
}

void GenJava::writeInitCode(const FuNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(def->type.get())) {
		int nesting = 0;
		while (const FuArrayStorageType *innerArray = dynamic_cast<const FuArrayStorageType *>(array->getElementType().get())) {
			openLoop("int", nesting++, array->length);
			array = innerArray;
		}
		openLoop("int", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		const FuStorageType * storage = static_cast<const FuStorageType *>(array->getElementType().get());
		writeNew(storage, FuPriority::argument);
		writeCharLine(';');
		while (--nesting >= 0)
			closeBlock();
	}
	else
		GenBase::writeInitCode(def);
}

void GenJava::visitLambdaExpr(const FuLambdaExpr * expr)
{
	writeName(expr->first);
	write(" -> ");
	expr->body->accept(this, FuPriority::statement);
}

void GenJava::defineIsVar(const FuBinaryExpr * binary)
{
}

void GenJava::writeAssert(const FuAssert * statement)
{
	if (statement->completesNormally()) {
		write("assert ");
		statement->cond->accept(this, FuPriority::argument);
		if (statement->message != nullptr) {
			write(" : ");
			statement->message->accept(this, FuPriority::argument);
		}
	}
	else {
		write("throw new AssertionError(");
		if (statement->message != nullptr)
			statement->message->accept(this, FuPriority::argument);
		writeChar(')');
	}
	writeCharLine(';');
}

void GenJava::startBreakGoto()
{
	write("break fuswitch");
}

void GenJava::visitForeach(const FuForeach * statement)
{
	write("for (");
	const FuClassType * klass = static_cast<const FuClassType *>(statement->collection->type.get());
	switch (klass->class_->id) {
	case FuId::stringClass:
		write("int _i = 0; _i < ");
		writeStringLength(statement->collection.get());
		write("; _i++) ");
		openBlock();
		writeTypeAndName(statement->getVar());
		write(" = ");
		statement->collection->accept(this, FuPriority::primary);
		writeLine(".charAt(_i);");
		flattenBlock(statement->body.get());
		closeBlock();
		return;
	case FuId::dictionaryClass:
	case FuId::sortedDictionaryClass:
	case FuId::orderedDictionaryClass:
		include("java.util.Map");
		writeDictType("Map.Entry", klass);
		writeChar(' ');
		write(statement->getVar()->name);
		write(" : ");
		writePostfix(statement->collection.get(), ".entrySet()");
		break;
	default:
		writeTypeAndName(statement->getVar());
		write(" : ");
		statement->collection->accept(this, FuPriority::argument);
		break;
	}
	writeChar(')');
	writeChild(statement->body.get());
}

bool GenJava::isTryParse(FuId id)
{
	return id == FuId::intTryParse || id == FuId::longTryParse || id == FuId::doubleTryParse;
}

void GenJava::visitIf(const FuIf * statement)
{
	const FuPrefixExpr * not_;
	const FuCallExpr * call;
	if (statement->onFalse == nullptr && (not_ = dynamic_cast<const FuPrefixExpr *>(statement->cond.get())) && not_->op == FuToken::exclamationMark && (call = dynamic_cast<const FuCallExpr *>(not_->inner.get())) && isTryParse(call->method->symbol->id)) {
		write("try ");
		openBlock();
		call->method->left->accept(this, FuPriority::assign);
		write(" = ");
		switch (call->method->symbol->id) {
		case FuId::intTryParse:
			write("Integer.parseInt");
			break;
		case FuId::longTryParse:
			write("Long.parseLong");
			break;
		case FuId::doubleTryParse:
			write("Double.parseDouble");
			break;
		default:
			std::abort();
		}
		writeChar('(');
		call->arguments[0]->accept(this, FuPriority::argument);
		if (std::ssize(call->arguments) == 2) {
			write(", ");
			call->arguments[1]->accept(this, FuPriority::argument);
		}
		writeLine(");");
		closeBlock();
		write("catch (NumberFormatException e) ");
		openBlock();
		if (!dynamic_cast<const FuReturn *>(statement->onTrue.get()) && !dynamic_cast<const FuThrow *>(statement->onTrue.get())) {
			call->method->left->accept(this, FuPriority::assign);
			writeLine(" = 0;");
		}
		statement->onTrue->acceptStatement(this);
		closeBlock();
	}
	else
		GenBase::visitIf(statement);
}

void GenJava::visitLock(const FuLock * statement)
{
	writeCall("synchronized ", statement->lock.get());
	writeChild(statement->body.get());
}

void GenJava::visitReturn(const FuReturn * statement)
{
	if (statement->value != nullptr && this->currentMethod->id == FuId::main) {
		if (!statement->value->isLiteralZero()) {
			ensureChildBlock();
			write("System.exit(");
			statement->value->accept(this, FuPriority::argument);
			writeLine(");");
		}
		writeLine("return;");
	}
	else
		GenBase::visitReturn(statement);
}

void GenJava::writeSwitchValue(const FuExpr * expr)
{
	if (isUnsignedByteIndexing(expr)) {
		const FuBinaryExpr * indexing = static_cast<const FuBinaryExpr *>(expr);
		writeIndexingInternal(indexing);
	}
	else
		GenBase::writeSwitchValue(expr);
}

void GenJava::writeSwitchCaseValue(const FuSwitch * statement, const FuExpr * value)
{
	if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(value)) {
		const FuEnum * enu;
		if ((enu = dynamic_cast<const FuEnum *>(symbol->symbol->parent)) && isJavaEnum(enu)) {
			writeUppercaseWithUnderscores(symbol->name);
			return;
		}
		if (const FuClass *klass = dynamic_cast<const FuClass *>(symbol->symbol)) {
			write(klass->name);
			write(" _");
			return;
		}
	}
	GenBase::writeSwitchCaseValue(statement, value);
}

void GenJava::visitSwitch(const FuSwitch * statement)
{
	if (!statement->isTypeMatching() && statement->hasWhen()) {
		if (std::any_of(statement->cases.begin(), statement->cases.end(), [](const FuCase &kase) { return FuSwitch::hasEarlyBreakAndContinue(&kase.body); }) || FuSwitch::hasEarlyBreakAndContinue(&statement->defaultBody)) {
			write("fuswitch");
			visitLiteralLong(std::ssize(this->switchesWithGoto));
			write(": ");
			this->switchesWithGoto.push_back(statement);
			writeSwitchAsIfs(statement, false);
		}
		else
			writeSwitchAsIfs(statement, true);
	}
	else
		GenBase::visitSwitch(statement);
}

void GenJava::createJavaFile(std::string_view className)
{
	createFile(this->outputFile, std::string(className) + ".java");
	if (!this->namespace_.empty()) {
		write("package ");
		write(this->namespace_);
		writeCharLine(';');
	}
}

void GenJava::visitEnumValue(const FuConst * konst, const FuConst * previous)
{
	writeDoc(konst->documentation.get());
	write("int ");
	writeUppercaseWithUnderscores(konst->name);
	write(" = ");
	if (const FuImplicitEnumValue *imp = dynamic_cast<const FuImplicitEnumValue *>(konst->value.get()))
		visitLiteralLong(imp->value);
	else
		konst->value->accept(this, FuPriority::argument);
	writeCharLine(';');
}

void GenJava::writeEnum(const FuEnum * enu)
{
	createJavaFile(enu->name);
	writeNewLine();
	writeDoc(enu->documentation.get());
	writePublic(enu);
	bool javaEnum = isJavaEnum(enu);
	write(javaEnum ? "enum " : "interface ");
	writeLine(enu->name);
	openBlock();
	if (javaEnum) {
		for (const FuSymbol * symbol = enu->getFirstValue();;) {
			writeDoc(symbol->documentation.get());
			writeUppercaseWithUnderscores(symbol->name);
			symbol = symbol->next;
			if (symbol == nullptr)
				break;
			writeCharLine(',');
		}
		writeNewLine();
	}
	else
		enu->acceptValues(this);
	closeBlock();
	closeFile();
}

void GenJava::writeSignature(const FuMethod * method, int paramCount)
{
	writeNewLine();
	writeMethodDoc(method);
	writeVisibility(method->visibility);
	switch (method->callType) {
	case FuCallType::static_:
		write("static ");
		break;
	case FuCallType::virtual_:
		break;
	case FuCallType::abstract:
		write("abstract ");
		break;
	case FuCallType::override_:
		write("@Override ");
		break;
	case FuCallType::normal:
		if (method->visibility != FuVisibility::private_)
			write("final ");
		break;
	case FuCallType::sealed:
		write("final @Override ");
		break;
	default:
		std::abort();
	}
	if (method->id == FuId::main)
		write("void main");
	else
		writeTypeAndName(method);
	writeChar('(');
	if (method->id == FuId::main && paramCount == 0)
		write("String[] args");
	else {
		const FuVar * param = method->firstParameter();
		for (int i = 0; i < paramCount; i++) {
			if (i > 0)
				write(", ");
			writeTypeAndName(param);
			param = param->nextVar();
		}
	}
	writeChar(')');
	std::string_view separator = " throws ";
	for (const std::shared_ptr<FuThrowsDeclaration> &exception : method->throws) {
		write(separator);
		writeExceptionClass(exception->symbol);
		separator = ", ";
	}
}

void GenJava::writeOverloads(const FuMethod * method, int paramCount)
{
	if (paramCount + 1 < method->getParametersCount())
		writeOverloads(method, paramCount + 1);
	writeSignature(method, paramCount);
	writeNewLine();
	openBlock();
	if (method->type->id != FuId::voidType)
		write("return ");
	writeName(method);
	writeChar('(');
	const FuVar * param = method->firstParameter();
	for (int i = 0; i < paramCount; i++) {
		writeName(param);
		write(", ");
		param = param->nextVar();
	}
	param->value->accept(this, FuPriority::argument);
	writeLine(");");
	closeBlock();
}

void GenJava::writeConst(const FuConst * konst)
{
	writeNewLine();
	writeDoc(konst->documentation.get());
	writeVisibility(konst->visibility);
	write("static final ");
	writeTypeAndName(konst);
	write(" = ");
	writeCoercedExpr(konst->type.get(), konst->value.get());
	writeCharLine(';');
}

void GenJava::writeField(const FuField * field)
{
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	writeVar(field);
	writeCharLine(';');
}

void GenJava::writeMethod(const FuMethod * method)
{
	writeSignature(method, method->getParametersCount());
	writeBody(method);
	int i = 0;
	for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
		if (param->value != nullptr) {
			writeOverloads(method, i);
			break;
		}
		i++;
	}
}

void GenJava::writeClass(const FuClass * klass, const FuProgram * program)
{
	openStringWriter();
	writeDoc(klass->documentation.get());
	writePublic(klass);
	switch (klass->callType) {
	case FuCallType::normal:
		break;
	case FuCallType::abstract:
		write("abstract ");
		break;
	case FuCallType::static_:
	case FuCallType::sealed:
		write("final ");
		break;
	default:
		std::abort();
	}
	openClass(klass, "", " extends ");
	if (klass->callType == FuCallType::static_) {
		write("private ");
		write(klass->name);
		writeLine("()");
		openBlock();
		closeBlock();
	}
	else if (needsConstructor(klass)) {
		if (klass->constructor != nullptr) {
			writeDoc(klass->constructor->documentation.get());
			writeVisibility(klass->constructor->visibility);
		}
		write(klass->name);
		writeLine("()");
		openBlock();
		writeConstructorBody(klass);
		closeBlock();
	}
	else if (klass->id == FuId::exceptionClass) {
		writeExceptionConstructor(klass, "() { }");
		writeExceptionConstructor(klass, "(String message) { super(message); }");
		writeExceptionConstructor(klass, "(String message, Throwable cause) { super(message, cause); }");
		writeExceptionConstructor(klass, "(Throwable cause) { super(cause); }");
	}
	writeMembers(klass, true);
	closeBlock();
	createJavaFile(klass->name);
	writeTopLevelNatives(program);
	writeIncludes("import ", ";");
	writeNewLine();
	closeStringWriter();
	closeFile();
}

void GenJava::writeResources()
{
	createJavaFile("FuResource");
	writeLine("import java.io.DataInputStream;");
	writeLine("import java.io.IOException;");
	writeNewLine();
	write("class FuResource");
	writeNewLine();
	openBlock();
	writeLine("static byte[] getByteArray(String name, int length)");
	openBlock();
	write("DataInputStream dis = new DataInputStream(");
	writeLine("FuResource.class.getResourceAsStream(name));");
	writeLine("byte[] result = new byte[length];");
	write("try ");
	openBlock();
	write("try ");
	openBlock();
	writeLine("dis.readFully(result);");
	closeBlock();
	write("finally ");
	openBlock();
	writeLine("dis.close();");
	closeBlock();
	closeBlock();
	write("catch (IOException e) ");
	openBlock();
	writeLine("throw new RuntimeException();");
	closeBlock();
	writeLine("return result;");
	closeBlock();
	closeBlock();
	closeFile();
}

void GenJava::writeProgram(const FuProgram * program)
{
	writeTypes(program);
	if (std::ssize(program->resources) > 0)
		writeResources();
}

std::string_view GenJsNoModule::getTargetName() const
{
	return "JavaScript";
}

void GenJsNoModule::writeCamelCaseNotKeyword(std::string_view name)
{
	writeCamelCase(name);
	if (name == "Arguments" || name == "Constructor" || name == "arguments" || name == "await" || name == "catch" || name == "debugger" || name == "delete" || name == "export" || name == "extends" || name == "finally" || name == "function" || name == "implements" || name == "import" || name == "instanceof" || name == "interface" || name == "let" || name == "package" || name == "private" || name == "super" || name == "try" || name == "typeof" || name == "var" || name == "with" || name == "yield")
		writeChar('_');
}

void GenJsNoModule::writeName(const FuSymbol * symbol)
{
	if (dynamic_cast<const FuContainerType *>(symbol))
		write(symbol->name);
	else if (const FuConst *konst = dynamic_cast<const FuConst *>(symbol)) {
		if (konst->visibility == FuVisibility::private_)
			writeChar('#');
		writeUppercaseConstName(konst);
	}
	else if (dynamic_cast<const FuVar *>(symbol))
		writeCamelCaseNotKeyword(symbol->name);
	else if (const FuMember *member = dynamic_cast<const FuMember *>(symbol)) {
		if (member->visibility == FuVisibility::private_) {
			writeChar('#');
			writeCamelCase(symbol->name);
			if (symbol->name == "Constructor")
				writeChar('_');
		}
		else
			writeCamelCaseNotKeyword(symbol->name);
	}
	else
		std::abort();
}

void GenJsNoModule::writeTypeAndName(const FuNamedValue * value)
{
	writeName(value);
}

void GenJsNoModule::writeArrayElementType(const FuType * type)
{
	switch (type->id) {
	case FuId::sByteRange:
		write("Int8");
		break;
	case FuId::byteRange:
		write("Uint8");
		break;
	case FuId::shortRange:
		write("Int16");
		break;
	case FuId::uShortRange:
		write("Uint16");
		break;
	case FuId::intType:
	case FuId::nIntType:
		write("Int32");
		break;
	case FuId::longType:
		write("BigInt64");
		break;
	case FuId::floatType:
		write("Float32");
		break;
	case FuId::doubleType:
		write("Float64");
		break;
	default:
		std::abort();
	}
}

void GenJsNoModule::visitAggregateInitializer(const FuAggregateInitializer * expr)
{
	const FuArrayStorageType * array = static_cast<const FuArrayStorageType *>(expr->type.get());
	bool numeric = false;
	if (const FuNumericType *number = dynamic_cast<const FuNumericType *>(array->getElementType().get())) {
		write("new ");
		writeArrayElementType(number);
		write("Array(");
		numeric = true;
	}
	write("[ ");
	writeCoercedLiterals(nullptr, &expr->items);
	write(" ]");
	if (numeric)
		writeChar(')');
}

void GenJsNoModule::writeNew(const FuReadWriteClassType * klass, FuPriority parent)
{
	switch (klass->class_->id) {
	case FuId::listClass:
	case FuId::queueClass:
	case FuId::stackClass:
		write("[]");
		break;
	case FuId::hashSetClass:
	case FuId::sortedSetClass:
		write("new Set()");
		break;
	case FuId::dictionaryClass:
	case FuId::sortedDictionaryClass:
		write("{}");
		break;
	case FuId::orderedDictionaryClass:
		write("new Map()");
		break;
	case FuId::lockClass:
		notSupported(klass, "Lock");
		break;
	default:
		write("new ");
		if (klass->class_->id == FuId::stringWriterClass)
			this->stringWriter = true;
		write(klass->class_->name);
		write("()");
		break;
	}
}

void GenJsNoModule::writeNewWithFields(const FuReadWriteClassType * type, const FuAggregateInitializer * init)
{
	write("Object.assign(");
	writeNew(type, FuPriority::argument);
	writeChar(',');
	writeObjectLiteral(init, ": ");
	writeChar(')');
}

void GenJsNoModule::writeVar(const FuNamedValue * def)
{
	write(def->type->isFinal() && !def->isAssignableStorage() ? "const " : "let ");
	GenBase::writeVar(def);
}

void GenJsNoModule::writeInterpolatedLiteral(std::string_view s)
{
	int i = 0;
	for (int c : s) {
		i++;
		if (c == '`' || (c == '$' && i < std::ssize(s) && s[i] == '{'))
			writeChar('\\');
		writeChar(c);
	}
}

void GenJsNoModule::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	writeChar('`');
	for (const FuInterpolatedPart &part : expr->parts) {
		writeInterpolatedLiteral(part.prefix);
		write("${");
		if (part.width != 0 || part.format != ' ') {
			if (dynamic_cast<const FuLiteralLong *>(part.argument.get()) || dynamic_cast<const FuPrefixExpr *>(part.argument.get())) {
				writeChar('(');
				part.argument->accept(this, FuPriority::primary);
				writeChar(')');
			}
			else
				part.argument->accept(this, FuPriority::primary);
			if (dynamic_cast<const FuNumericType *>(part.argument->type.get())) {
				switch (part.format) {
				case 'E':
					write(".toExponential(");
					if (part.precision >= 0)
						visitLiteralLong(part.precision);
					write(").toUpperCase()");
					break;
				case 'e':
					write(".toExponential(");
					if (part.precision >= 0)
						visitLiteralLong(part.precision);
					writeChar(')');
					break;
				case 'F':
				case 'f':
					write(".toFixed(");
					if (part.precision >= 0)
						visitLiteralLong(part.precision);
					writeChar(')');
					break;
				case 'X':
					write(".toString(16).toUpperCase()");
					break;
				case 'x':
					write(".toString(16)");
					break;
				default:
					write(".toString()");
					break;
				}
				if (part.precision >= 0) {
					switch (part.format) {
					case 'D':
					case 'd':
					case 'X':
					case 'x':
						write(".padStart(");
						visitLiteralLong(part.precision);
						write(", \"0\")");
						break;
					default:
						break;
					}
				}
			}
			if (part.width > 0) {
				write(".padStart(");
				visitLiteralLong(part.width);
				writeChar(')');
			}
			else if (part.width < 0) {
				write(".padEnd(");
				visitLiteralLong(-part.width);
				writeChar(')');
			}
		}
		else
			part.argument->accept(this, FuPriority::argument);
		writeChar('}');
	}
	writeInterpolatedLiteral(expr->suffix);
	writeChar('`');
}

void GenJsNoModule::writeLocalName(const FuSymbol * symbol, FuPriority parent)
{
	if (const FuMember *member = dynamic_cast<const FuMember *>(symbol)) {
		if (!member->isStatic())
			write("this");
		else if (this->currentMethod != nullptr)
			write(this->currentMethod->parent->name);
		else if (const FuConst *konst = dynamic_cast<const FuConst *>(symbol)) {
			konst->value->accept(this, parent);
			return;
		}
		else
			std::abort();
		writeChar('.');
	}
	writeName(symbol);
	const FuForeach * forEach;
	if ((forEach = dynamic_cast<const FuForeach *>(symbol->parent)) && dynamic_cast<const FuStringType *>(forEach->collection->type.get()))
		write(".codePointAt(0)");
}

void GenJsNoModule::writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	if (dynamic_cast<const FuNumericType *>(type)) {
		if (type->id == FuId::longType) {
			if (dynamic_cast<const FuLiteralLong *>(expr)) {
				expr->accept(this, FuPriority::primary);
				writeChar('n');
				return;
			}
			if (expr->type->id != FuId::longType) {
				writeCall("BigInt", expr);
				return;
			}
		}
		else if (expr->type->id == FuId::longType) {
			writeCall("Number", expr);
			return;
		}
	}
	expr->accept(this, parent);
}

void GenJsNoModule::writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent)
{
	write("new ");
	if (dynamic_cast<const FuNumericType *>(elementType))
		writeArrayElementType(elementType);
	writeCall("Array", lengthExpr);
}

bool GenJsNoModule::hasInitCode(const FuNamedValue * def) const
{
	const FuArrayStorageType * array;
	return (array = dynamic_cast<const FuArrayStorageType *>(def->type.get())) && dynamic_cast<const FuStorageType *>(array->getElementType().get());
}

void GenJsNoModule::writeInitCode(const FuNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	const FuArrayStorageType * array = static_cast<const FuArrayStorageType *>(def->type.get());
	int nesting = 0;
	while (const FuArrayStorageType *innerArray = dynamic_cast<const FuArrayStorageType *>(array->getElementType().get())) {
		openLoop("let", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		writeNewArray(innerArray->getElementType().get(), innerArray->lengthExpr.get(), FuPriority::argument);
		writeCharLine(';');
		array = innerArray;
	}
	if (const FuStorageType *klass = dynamic_cast<const FuStorageType *>(array->getElementType().get())) {
		openLoop("let", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		writeNew(klass, FuPriority::argument);
		writeCharLine(';');
	}
	while (--nesting >= 0)
		closeBlock();
}

void GenJsNoModule::writeResource(std::string_view name, int length)
{
	write("Fu.");
	writeResourceName(name);
}

void GenJsNoModule::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::consoleError:
		write("process.stderr");
		break;
	case FuId::listCount:
	case FuId::queueCount:
	case FuId::stackCount:
		writePostfix(expr->left.get(), ".length");
		break;
	case FuId::hashSetCount:
	case FuId::sortedSetCount:
	case FuId::orderedDictionaryCount:
		writePostfix(expr->left.get(), ".size");
		break;
	case FuId::dictionaryCount:
	case FuId::sortedDictionaryCount:
		writeCall("Object.keys", expr->left.get());
		write(".length");
		break;
	case FuId::matchStart:
		writePostfix(expr->left.get(), ".index");
		break;
	case FuId::matchEnd:
		if (parent > FuPriority::add)
			writeChar('(');
		writePostfix(expr->left.get(), ".index + ");
		writePostfix(expr->left.get(), "[0].length");
		if (parent > FuPriority::add)
			writeChar(')');
		break;
	case FuId::matchLength:
		writePostfix(expr->left.get(), "[0].length");
		break;
	case FuId::matchValue:
		writePostfix(expr->left.get(), "[0]");
		break;
	case FuId::mathNaN:
		write("NaN");
		break;
	case FuId::mathNegativeInfinity:
		write("-Infinity");
		break;
	case FuId::mathPositiveInfinity:
		write("Infinity");
		break;
	default:
		GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

void GenJsNoModule::writeStringLength(const FuExpr * expr)
{
	writePostfix(expr, ".length");
}

void GenJsNoModule::writeCharAt(const FuBinaryExpr * expr)
{
	writeMethodCall(expr->left.get(), "charCodeAt", expr->right.get());
}

void GenJsNoModule::writeBinaryOperand(const FuExpr * expr, FuPriority parent, const FuBinaryExpr * binary)
{
	writeCoerced(binary->isRel() ? expr->type.get() : binary->type.get(), expr, parent);
}

void GenJsNoModule::writeSlice(const FuExpr * array, const FuExpr * offset, const FuExpr * length, FuPriority parent, std::string_view method)
{
	if (isWholeArray(array, offset, length))
		array->accept(this, parent);
	else {
		array->accept(this, FuPriority::primary);
		writeChar('.');
		write(method);
		writeChar('(');
		writeStartEnd(offset, length);
		writeChar(')');
	}
}

bool GenJsNoModule::isIdentifier(std::string_view s)
{
	if (s.empty() || s[0] < 'A')
		return false;
	for (int c : s) {
		if (!FuLexer::isLetterOrDigit(c))
			return false;
	}
	return true;
}

void GenJsNoModule::writeNewRegex(const std::vector<std::shared_ptr<FuExpr>> * args, int argIndex)
{
	const FuExpr * pattern = (*args)[argIndex].get();
	if (const FuLiteralString *literal = dynamic_cast<const FuLiteralString *>(pattern)) {
		writeChar('/');
		bool escaped = false;
		for (int c : literal->value) {
			switch (c) {
			case '\\':
				if (!escaped) {
					escaped = true;
					continue;
				}
				escaped = false;
				break;
			case '"':
			case '\'':
				escaped = false;
				break;
			case '/':
				escaped = true;
				break;
			default:
				break;
			}
			if (escaped) {
				writeChar('\\');
				escaped = false;
			}
			writeChar(c);
		}
		writeChar('/');
		writeRegexOptions(args, "", "", "", "i", "m", "s");
	}
	else {
		write("new RegExp(");
		pattern->accept(this, FuPriority::argument);
		writeRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
		writeChar(')');
	}
}

void GenJsNoModule::writeTypeofEquals(const FuExpr * obj, std::string_view name, FuPriority parent)
{
	if (parent > FuPriority::equality)
		writeChar('(');
	write("typeof(");
	obj->accept(this, FuPriority::argument);
	write(") == \"");
	write(name);
	writeChar('"');
	if (parent > FuPriority::equality)
		writeChar(')');
}

bool GenJsNoModule::hasLong(const std::vector<std::shared_ptr<FuExpr>> * args)
{
	return std::any_of(args->begin(), args->end(), [](const std::shared_ptr<FuExpr> &arg) { return arg->type->id == FuId::longType; });
}

void GenJsNoModule::writeMathMaxMin(const FuMethod * method, std::string_view name, int op, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	if (hasLong(args)) {
		write("((x, y) => x ");
		writeChar(op);
		write(" y ? x : y)");
		writeInParentheses(args);
	}
	else
		writeCall(name, (*args)[0].get(), (*args)[1].get());
}

void GenJsNoModule::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::none:
	case FuId::classToString:
	case FuId::stringEndsWith:
	case FuId::stringIndexOf:
	case FuId::stringLastIndexOf:
	case FuId::stringStartsWith:
	case FuId::arraySortAll:
	case FuId::listIndexOf:
	case FuId::stackPush:
	case FuId::stackPop:
	case FuId::hashSetAdd:
	case FuId::hashSetClear:
	case FuId::sortedSetAdd:
	case FuId::sortedSetClear:
	case FuId::orderedDictionaryClear:
	case FuId::stringWriterClear:
	case FuId::stringWriterToString:
	case FuId::mathMethod:
	case FuId::mathLog2:
	case FuId::mathRound:
		if (obj == nullptr)
			writeLocalName(method, FuPriority::primary);
		else {
			if (isReferenceTo(obj, FuId::basePtr))
				write("super");
			else
				obj->accept(this, FuPriority::primary);
			writeChar('.');
			writeName(method);
		}
		writeCoercedArgsInParentheses(method, args);
		break;
	case FuId::enumFromInt:
		(*args)[0]->accept(this, parent);
		break;
	case FuId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case FuId::intTryParse:
		write("!isNaN(");
		obj->accept(this, FuPriority::assign);
		write(" = parseInt(");
		(*args)[0]->accept(this, FuPriority::argument);
		writeTryParseRadix(args);
		write("))");
		break;
	case FuId::longTryParse:
		if (std::ssize(*args) != 1)
			notSupported((*args)[1].get(), "Radix");
		write("(() => { try { ");
		obj->accept(this, FuPriority::assign);
		write("  = BigInt(");
		(*args)[0]->accept(this, FuPriority::argument);
		write("); return true; } catch { return false; }})()");
		break;
	case FuId::doubleTryParse:
		write("!isNaN(");
		obj->accept(this, FuPriority::assign);
		write(" = parseFloat(");
		(*args)[0]->accept(this, FuPriority::argument);
		write("))");
		break;
	case FuId::stringContains:
	case FuId::arrayContains:
	case FuId::listContains:
		writeMethodCall(obj, "includes", (*args)[0].get());
		break;
	case FuId::stringReplace:
		writeMethodCall(obj, "replaceAll", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::stringSubstring:
		writePostfix(obj, ".substring(");
		(*args)[0]->accept(this, FuPriority::argument);
		if (std::ssize(*args) == 2) {
			write(", ");
			writeAdd((*args)[0].get(), (*args)[1].get());
		}
		writeChar(')');
		break;
	case FuId::stringToLower:
		writePostfix(obj, ".toLowerCase()");
		break;
	case FuId::stringToUpper:
		writePostfix(obj, ".toUpperCase()");
		break;
	case FuId::arrayFillAll:
	case FuId::arrayFillPart:
		writePostfix(obj, ".fill(");
		(*args)[0]->accept(this, FuPriority::argument);
		if (std::ssize(*args) == 3) {
			write(", ");
			writeStartEnd((*args)[1].get(), (*args)[2].get());
		}
		writeChar(')');
		break;
	case FuId::arrayCopyTo:
	case FuId::listCopyTo:
		(*args)[1]->accept(this, FuPriority::primary);
		if (dynamic_cast<const FuNumericType *>(obj->type->asClassType()->getElementType().get())) {
			write(".set(");
			writeSlice(obj, (*args)[0].get(), (*args)[3].get(), FuPriority::argument, method->id == FuId::arrayCopyTo ? "subarray" : "slice");
			if (!(*args)[2]->isLiteralZero()) {
				write(", ");
				(*args)[2]->accept(this, FuPriority::argument);
			}
		}
		else {
			write(".splice(");
			(*args)[2]->accept(this, FuPriority::argument);
			write(", ");
			(*args)[3]->accept(this, FuPriority::argument);
			write(", ...");
			writeSlice(obj, (*args)[0].get(), (*args)[3].get(), FuPriority::primary, "slice");
		}
		writeChar(')');
		break;
	case FuId::arraySortPart:
		writePostfix(obj, ".subarray(");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		write(").sort()");
		break;
	case FuId::listAdd:
		writeListAdd(obj, "push", args);
		break;
	case FuId::listAddRange:
		writePostfix(obj, ".push(...");
		(*args)[0]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::listAll:
		writeMethodCall(obj, "every", (*args)[0].get());
		break;
	case FuId::listAny:
		writeMethodCall(obj, "some", (*args)[0].get());
		break;
	case FuId::listClear:
	case FuId::queueClear:
	case FuId::stackClear:
		writePostfix(obj, ".length = 0");
		break;
	case FuId::listInsert:
		writeListInsert(obj, "splice", args, ", 0, ");
		break;
	case FuId::listLast:
	case FuId::stackPeek:
		writePostfix(obj, ".at(-1)");
		break;
	case FuId::listRemoveAt:
		writePostfix(obj, ".splice(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", 1)");
		break;
	case FuId::listRemoveRange:
		writeMethodCall(obj, "splice", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::listSortAll:
		writePostfix(obj, ".sort((a, b) => a - b)");
		break;
	case FuId::listSortPart:
		writePostfix(obj, ".splice(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		(*args)[1]->accept(this, FuPriority::argument);
		write(", ...");
		writeSlice(obj, (*args)[0].get(), (*args)[1].get(), FuPriority::primary, "slice");
		write(".sort((a, b) => a - b))");
		break;
	case FuId::queueDequeue:
		writePostfix(obj, ".shift()");
		break;
	case FuId::queueEnqueue:
		writeMethodCall(obj, "push", (*args)[0].get());
		break;
	case FuId::queuePeek:
		writePostfix(obj, "[0]");
		break;
	case FuId::hashSetContains:
	case FuId::sortedSetContains:
	case FuId::orderedDictionaryContainsKey:
		writeMethodCall(obj, "has", (*args)[0].get());
		break;
	case FuId::hashSetRemove:
	case FuId::sortedSetRemove:
	case FuId::orderedDictionaryRemove:
		writeMethodCall(obj, "delete", (*args)[0].get());
		break;
	case FuId::dictionaryAdd:
		writeDictionaryAdd(obj, args);
		break;
	case FuId::dictionaryClear:
	case FuId::sortedDictionaryClear:
		write("for (const key in ");
		obj->accept(this, FuPriority::argument);
		writeCharLine(')');
		write("\tdelete ");
		writePostfix(obj, "[key];");
		break;
	case FuId::dictionaryContainsKey:
	case FuId::sortedDictionaryContainsKey:
		writeMethodCall(obj, "hasOwnProperty", (*args)[0].get());
		break;
	case FuId::dictionaryRemove:
	case FuId::sortedDictionaryRemove:
		write("delete ");
		writeIndexing(obj, (*args)[0].get());
		break;
	case FuId::textWriterWrite:
		writePostfix(obj, ".write(");
		if (dynamic_cast<const FuStringType *>((*args)[0]->type.get()))
			(*args)[0]->accept(this, FuPriority::argument);
		else
			writeCall("String", (*args)[0].get());
		writeChar(')');
		break;
	case FuId::textWriterWriteChar:
		writeMethodCall(obj, "write(String.fromCharCode", (*args)[0].get());
		writeChar(')');
		break;
	case FuId::textWriterWriteCodePoint:
		writeMethodCall(obj, "write(String.fromCodePoint", (*args)[0].get());
		writeChar(')');
		break;
	case FuId::textWriterWriteLine:
		if (isReferenceTo(obj, FuId::consoleError)) {
			write("console.error(");
			if (std::ssize(*args) == 0)
				write("\"\"");
			else
				(*args)[0]->accept(this, FuPriority::argument);
			writeChar(')');
		}
		else {
			writePostfix(obj, ".write(");
			if (std::ssize(*args) != 0) {
				(*args)[0]->accept(this, FuPriority::add);
				write(" + ");
			}
			write("\"\\n\")");
		}
		break;
	case FuId::consoleWrite:
		write("process.stdout.write(");
		if (dynamic_cast<const FuStringType *>((*args)[0]->type.get()))
			(*args)[0]->accept(this, FuPriority::argument);
		else
			writeCall("String", (*args)[0].get());
		writeChar(')');
		break;
	case FuId::consoleWriteLine:
		write("console.log(");
		if (std::ssize(*args) == 0)
			write("\"\"");
		else
			(*args)[0]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::convertToBase64String:
		write("btoa(String.fromCodePoint(...");
		writeSlice((*args)[0].get(), (*args)[1].get(), (*args)[2].get(), FuPriority::primary, "subarray");
		write("))");
		break;
	case FuId::uTF8GetByteCount:
		write("new TextEncoder().encode(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(").length");
		break;
	case FuId::uTF8GetBytes:
		write("new TextEncoder().encodeInto(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		if ((*args)[2]->isLiteralZero())
			(*args)[1]->accept(this, FuPriority::argument);
		else
			writeMethodCall((*args)[1].get(), "subarray", (*args)[2].get());
		writeChar(')');
		break;
	case FuId::uTF8GetString:
		write("new TextDecoder().decode(");
		writeSlice((*args)[0].get(), (*args)[1].get(), (*args)[2].get(), FuPriority::argument, "subarray");
		writeChar(')');
		break;
	case FuId::environmentGetEnvironmentVariable:
		{
			const FuLiteralString * literal;
			if ((literal = dynamic_cast<const FuLiteralString *>((*args)[0].get())) && isIdentifier(literal->value)) {
				write("process.env.");
				write(literal->value);
			}
			else {
				write("process.env[");
				(*args)[0]->accept(this, FuPriority::argument);
				writeChar(']');
			}
			break;
		}
	case FuId::regexCompile:
		writeNewRegex(args, 0);
		break;
	case FuId::regexEscape:
		writePostfix((*args)[0].get(), ".replace(/[-\\/\\\\^$*+?.()|[\\]{}]/g, '\\\\$&')");
		break;
	case FuId::regexIsMatchStr:
		writeNewRegex(args, 1);
		writeCall(".test", (*args)[0].get());
		break;
	case FuId::regexIsMatchRegex:
		writeMethodCall(obj, "test", (*args)[0].get());
		break;
	case FuId::matchFindStr:
	case FuId::matchFindRegex:
		if (parent > FuPriority::equality)
			writeChar('(');
		writeChar('(');
		obj->accept(this, FuPriority::assign);
		write(" = ");
		if (method->id == FuId::matchFindStr)
			writeNewRegex(args, 1);
		else
			(*args)[1]->accept(this, FuPriority::primary);
		writeCall(".exec", (*args)[0].get());
		write(") != null");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::matchGetCapture:
		writeIndexing(obj, (*args)[0].get());
		break;
	case FuId::jsonElementParse:
		writeCall("JSON.parse", (*args)[0].get());
		break;
	case FuId::jsonElementIsObject:
		if (parent > FuPriority::equality)
			writeChar('(');
		writePostfix(obj, "?.constructor == Object");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::jsonElementIsArray:
		writeCall("Array.isArray", obj);
		break;
	case FuId::jsonElementIsString:
		writeTypeofEquals(obj, "string", parent);
		break;
	case FuId::jsonElementIsNumber:
		writeTypeofEquals(obj, "number", parent);
		break;
	case FuId::jsonElementIsBoolean:
		writeTypeofEquals(obj, "boolean", parent);
		break;
	case FuId::jsonElementIsNull:
		if (parent > FuPriority::equality)
			writeChar('(');
		obj->accept(this, FuPriority::equality);
		write(" === null");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::jsonElementGetObject:
	case FuId::jsonElementGetArray:
	case FuId::jsonElementGetString:
	case FuId::jsonElementGetDouble:
	case FuId::jsonElementGetBoolean:
		obj->accept(this, parent);
		break;
	case FuId::mathAbs:
		writeCall((*args)[0]->type->id == FuId::longType ? "(x => x < 0n ? -x : x)" : "Math.abs", (*args)[0].get());
		break;
	case FuId::mathCeiling:
		writeCall("Math.ceil", (*args)[0].get());
		break;
	case FuId::mathClamp:
		if (hasLong(args)) {
			write("((x, min, max) => x < min ? min : x > max ? max : x)");
			writeInParentheses(args);
		}
		else {
			write("Math.min(Math.max(");
			writeClampAsMinMax(args);
		}
		break;
	case FuId::mathFusedMultiplyAdd:
		if (parent > FuPriority::add)
			writeChar('(');
		(*args)[0]->accept(this, FuPriority::mul);
		write(" * ");
		(*args)[1]->accept(this, FuPriority::mul);
		write(" + ");
		(*args)[2]->accept(this, FuPriority::add);
		if (parent > FuPriority::add)
			writeChar(')');
		break;
	case FuId::mathIsFinite:
	case FuId::mathIsNaN:
		writeCamelCase(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathIsInfinity:
		if (parent > FuPriority::equality)
			writeChar('(');
		writeCall("Math.abs", (*args)[0].get());
		write(" == Infinity");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::mathMax:
		writeMathMaxMin(method, "Math.max", '>', args);
		break;
	case FuId::mathMin:
		writeMathMaxMin(method, "Math.min", '<', args);
		break;
	case FuId::mathTruncate:
		writeCall("Math.trunc", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenJsNoModule::writeAssign(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuBinaryExpr * indexing;
	const FuClassType * dict;
	if ((indexing = dynamic_cast<const FuBinaryExpr *>(expr->left.get())) && indexing->op == FuToken::leftBracket && (dict = dynamic_cast<const FuClassType *>(indexing->left->type.get())) && dict->class_->id == FuId::orderedDictionaryClass)
		writeMethodCall(indexing->left.get(), "set", indexing->right.get(), expr->right.get());
	else
		GenBase::writeAssign(expr, parent);
}

void GenJsNoModule::writeOpAssignRight(const FuBinaryExpr * expr)
{
	writeCoerced(expr->left->type.get(), expr->right.get(), FuPriority::argument);
}

void GenJsNoModule::writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuClassType * dict;
	if ((dict = dynamic_cast<const FuClassType *>(expr->left->type.get())) && dict->class_->id == FuId::orderedDictionaryClass)
		writeMethodCall(expr->left.get(), "get", expr->right.get());
	else
		GenBase::writeIndexingExpr(expr, parent);
}

std::string_view GenJsNoModule::getIsOperator() const
{
	return " instanceof ";
}

void GenJsNoModule::writeBoolAndOr(const FuBinaryExpr * expr)
{
	write("!!");
	GenBase::visitBinaryExpr(expr, FuPriority::primary);
}

void GenJsNoModule::writeBoolAndOrAssign(const FuBinaryExpr * expr, FuPriority parent)
{
	expr->right->accept(this, parent);
	writeCharLine(')');
	writeChar('\t');
	expr->left->accept(this, FuPriority::assign);
}

void GenJsNoModule::writeIsVar(const FuExpr * expr, std::string_view name, const FuSymbol * klass, FuPriority parent)
{
	if (parent > FuPriority::rel)
		writeChar('(');
	if (name.data() != nullptr) {
		writeChar('(');
		writeCamelCaseNotKeyword(name);
		write(" = ");
		expr->accept(this, FuPriority::argument);
		writeChar(')');
	}
	else
		expr->accept(this, FuPriority::rel);
	write(" instanceof ");
	write(klass->name);
	if (parent > FuPriority::rel)
		writeChar(')');
}

void GenJsNoModule::visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuVar * def;
	if (expr->op == FuToken::slash && dynamic_cast<const FuIntegerType *>(expr->type.get()) && expr->type->id != FuId::longType) {
		if (parent > FuPriority::or_)
			writeChar('(');
		expr->left->accept(this, FuPriority::mul);
		write(" / ");
		expr->right->accept(this, FuPriority::primary);
		write(" | 0");
		if (parent > FuPriority::or_)
			writeChar(')');
	}
	else if (expr->op == FuToken::divAssign && dynamic_cast<const FuIntegerType *>(expr->type.get()) && expr->type->id != FuId::longType) {
		if (parent > FuPriority::assign)
			writeChar('(');
		expr->left->accept(this, FuPriority::assign);
		write(" = ");
		expr->left->accept(this, FuPriority::mul);
		write(" / ");
		expr->right->accept(this, FuPriority::primary);
		write(" | 0");
		if (parent > FuPriority::assign)
			writeChar(')');
	}
	else if ((expr->op == FuToken::and_ && expr->type->id == FuId::boolType) || (expr->op == FuToken::or_ && expr->type->id == FuId::boolType))
		writeBoolAndOr(expr);
	else if (expr->op == FuToken::xor_ && expr->type->id == FuId::boolType)
		writeEqual(expr->left.get(), expr->right.get(), parent, true);
	else if (expr->op == FuToken::andAssign && expr->type->id == FuId::boolType) {
		write("if (!");
		writeBoolAndOrAssign(expr, FuPriority::primary);
		write(" = false");
	}
	else if (expr->op == FuToken::orAssign && expr->type->id == FuId::boolType) {
		write("if (");
		writeBoolAndOrAssign(expr, FuPriority::argument);
		write(" = true");
	}
	else if (expr->op == FuToken::xorAssign && expr->type->id == FuId::boolType) {
		expr->left->accept(this, FuPriority::assign);
		write(" = ");
		writeEqual(expr->left.get(), expr->right.get(), FuPriority::argument, true);
	}
	else if (expr->op == FuToken::is && (def = dynamic_cast<const FuVar *>(expr->right.get())))
		writeIsVar(expr->left.get(), def->name, def->type.get(), parent);
	else
		GenBase::visitBinaryExpr(expr, parent);
}

void GenJsNoModule::visitLambdaExpr(const FuLambdaExpr * expr)
{
	writeName(expr->first);
	write(" => ");
	if (hasTemporaries(expr->body.get())) {
		openBlock();
		writeTemporaries(expr->body.get());
		write("return ");
		expr->body->accept(this, FuPriority::argument);
		writeCharLine(';');
		closeBlock();
	}
	else
		expr->body->accept(this, FuPriority::statement);
}

void GenJsNoModule::startTemporaryVar(const FuType * type)
{
	std::abort();
}

void GenJsNoModule::defineObjectLiteralTemporary(const FuUnaryExpr * expr)
{
}

void GenJsNoModule::writeAsType(const FuVar * def)
{
}

void GenJsNoModule::writeVarCast(const FuVar * def, const FuExpr * value)
{
	write(def->isAssigned ? "let " : "const ");
	writeCamelCaseNotKeyword(def->name);
	write(" = ");
	value->accept(this, FuPriority::argument);
	writeAsType(def);
	writeCharLine(';');
}

void GenJsNoModule::writeAssertCast(const FuBinaryExpr * expr)
{
	const FuVar * def = static_cast<const FuVar *>(expr->right.get());
	writeVarCast(def, expr->left.get());
}

void GenJsNoModule::writeAssert(const FuAssert * statement)
{
	if (statement->completesNormally()) {
		writeTemporaries(statement->cond.get());
		write("console.assert(");
		statement->cond->accept(this, FuPriority::argument);
		if (statement->message != nullptr) {
			write(", ");
			statement->message->accept(this, FuPriority::argument);
		}
	}
	else {
		write("throw new Error(");
		if (statement->message != nullptr)
			statement->message->accept(this, FuPriority::argument);
	}
	writeLine(");");
}

void GenJsNoModule::startBreakGoto()
{
	write("break fuswitch");
}

void GenJsNoModule::visitForeach(const FuForeach * statement)
{
	write("for (const ");
	const FuClassType * klass = static_cast<const FuClassType *>(statement->collection->type.get());
	switch (klass->class_->id) {
	case FuId::stringClass:
	case FuId::arrayStorageClass:
	case FuId::listClass:
	case FuId::hashSetClass:
		writeName(statement->getVar());
		write(" of ");
		statement->collection->accept(this, FuPriority::argument);
		break;
	case FuId::sortedSetClass:
		writeName(statement->getVar());
		write(" of ");
		if (const FuNumericType *number = dynamic_cast<const FuNumericType *>(klass->getElementType().get())) {
			write("new ");
			writeArrayElementType(number);
			write("Array(");
		}
		else if (dynamic_cast<const FuEnum *>(klass->getElementType().get()))
			write("new Int32Array(");
		else
			write("Array.from(");
		statement->collection->accept(this, FuPriority::argument);
		write(").sort()");
		break;
	case FuId::dictionaryClass:
	case FuId::sortedDictionaryClass:
	case FuId::orderedDictionaryClass:
		writeChar('[');
		writeName(statement->getVar());
		write(", ");
		writeName(statement->getValueVar());
		write("] of ");
		if (klass->class_->id == FuId::orderedDictionaryClass)
			statement->collection->accept(this, FuPriority::argument);
		else {
			writeCall("Object.entries", statement->collection.get());
			if (dynamic_cast<const FuStringType *>(statement->getVar()->type.get())) {
				if (klass->class_->id == FuId::sortedDictionaryClass)
					write(".sort((a, b) => a[0].localeCompare(b[0]))");
			}
			else if (dynamic_cast<const FuNumericType *>(statement->getVar()->type.get()) || dynamic_cast<const FuEnum *>(statement->getVar()->type.get())) {
				write(".map(e => [+e[0], e[1]])");
				if (klass->class_->id == FuId::sortedDictionaryClass)
					write(".sort((a, b) => a[0] - b[0])");
			}
			else
				std::abort();
		}
		break;
	default:
		std::abort();
	}
	writeChar(')');
	writeChild(statement->body.get());
}

void GenJsNoModule::visitLock(const FuLock * statement)
{
	notSupported(statement, "'lock'");
}

void GenJsNoModule::writeSwitchCaseCond(const FuSwitch * statement, const FuExpr * value, FuPriority parent)
{
	const FuSymbolReference * symbol;
	if ((symbol = dynamic_cast<const FuSymbolReference *>(value)) && dynamic_cast<const FuClass *>(symbol->symbol))
		writeIsVar(statement->value.get(), std::string_view(), symbol->symbol, parent);
	else if (const FuVar *def = dynamic_cast<const FuVar *>(value))
		writeIsVar(statement->value.get(), parent == FuPriority::condAnd ? def->name : std::string_view(), def->type.get(), parent);
	else
		GenBase::writeSwitchCaseCond(statement, value, parent);
}

void GenJsNoModule::writeIfCaseBody(const std::vector<std::shared_ptr<FuStatement>> * body, bool doWhile, const FuSwitch * statement, const FuCase * kase)
{
	const FuVar * caseVar;
	if (kase != nullptr && (caseVar = dynamic_cast<const FuVar *>(kase->values[0].get()))) {
		writeChar(' ');
		openBlock();
		writeVarCast(caseVar, statement->value.get());
		writeFirstStatements(&kase->body, FuSwitch::lengthWithoutTrailingBreak(&kase->body));
		closeBlock();
	}
	else
		GenBase::writeIfCaseBody(body, doWhile, statement, kase);
}

void GenJsNoModule::visitSwitch(const FuSwitch * statement)
{
	if (statement->isTypeMatching() || statement->hasWhen()) {
		if (std::any_of(statement->cases.begin(), statement->cases.end(), [](const FuCase &kase) { return FuSwitch::hasEarlyBreak(&kase.body); }) || FuSwitch::hasEarlyBreak(&statement->defaultBody)) {
			write("fuswitch");
			visitLiteralLong(std::ssize(this->switchesWithGoto));
			this->switchesWithGoto.push_back(statement);
			write(": ");
			openBlock();
			writeSwitchAsIfs(statement, false);
			closeBlock();
		}
		else
			writeSwitchAsIfs(statement, false);
	}
	else
		GenBase::visitSwitch(statement);
}

void GenJsNoModule::writeException()
{
	write("Error");
}

void GenJsNoModule::startContainerType(const FuContainerType * container)
{
	writeNewLine();
	writeDoc(container->documentation.get());
}

void GenJsNoModule::visitEnumValue(const FuConst * konst, const FuConst * previous)
{
	if (previous != nullptr)
		writeCharLine(',');
	writeDoc(konst->documentation.get());
	writeUppercaseWithUnderscores(konst->name);
	write(" : ");
	visitLiteralLong(konst->value->intValue());
}

void GenJsNoModule::writeEnum(const FuEnum * enu)
{
	startContainerType(enu);
	write("const ");
	write(enu->name);
	write(" = ");
	openBlock();
	enu->acceptValues(this);
	writeNewLine();
	closeBlock();
}

void GenJsNoModule::writeConst(const FuConst * konst)
{
	if (konst->visibility != FuVisibility::private_ || dynamic_cast<const FuArrayStorageType *>(konst->type.get())) {
		writeNewLine();
		writeDoc(konst->documentation.get());
		write("static ");
		writeName(konst);
		write(" = ");
		konst->value->accept(this, FuPriority::argument);
		writeCharLine(';');
	}
}

void GenJsNoModule::writeField(const FuField * field)
{
	writeDoc(field->documentation.get());
	GenBase::writeVar(field);
	writeCharLine(';');
}

void GenJsNoModule::writeMethod(const FuMethod * method)
{
	if (method->callType == FuCallType::abstract)
		return;
	writeNewLine();
	writeMethodDoc(method);
	if (method->callType == FuCallType::static_)
		write("static ");
	writeName(method);
	writeParameters(method, true);
	writeBody(method);
}

void GenJsNoModule::openJsClass(const FuClass * klass)
{
	openClass(klass, "", " extends ");
	if (klass->id == FuId::exceptionClass) {
		write("name = \"");
		write(klass->name);
		writeLine("\";");
	}
}

void GenJsNoModule::writeConstructor(const FuClass * klass)
{
	writeLine("constructor()");
	openBlock();
	if (dynamic_cast<const FuClass *>(klass->parent))
		writeLine("super();");
	writeConstructorBody(klass);
	closeBlock();
}

void GenJsNoModule::writeClass(const FuClass * klass, const FuProgram * program)
{
	if (!writeBaseClass(klass, program))
		return;
	startContainerType(klass);
	openJsClass(klass);
	if (needsConstructor(klass)) {
		if (klass->constructor != nullptr)
			writeDoc(klass->constructor->documentation.get());
		writeConstructor(klass);
	}
	writeMembers(klass, true);
	closeBlock();
}

void GenJsNoModule::writeMain(const FuMethod * main)
{
	writeNewLine();
	if (main->type->id == FuId::intType)
		write("process.exit(");
	write(main->parent->name);
	write(".main(");
	if (main->parameters.count() == 1)
		write("process.argv.slice(2)");
	if (main->type->id == FuId::intType)
		writeChar(')');
	writeCharLine(')');
}

void GenJsNoModule::writeLib(const FuProgram * program)
{
	if (this->stringWriter) {
		writeNewLine();
		writeLine("class StringWriter");
		openBlock();
		writeLine("#buf = \"\";");
		writeNewLine();
		writeLine("write(s)");
		openBlock();
		writeLine("this.#buf += s;");
		closeBlock();
		writeNewLine();
		writeLine("clear()");
		openBlock();
		writeLine("this.#buf = \"\";");
		closeBlock();
		writeNewLine();
		writeLine("toString()");
		openBlock();
		writeLine("return this.#buf;");
		closeBlock();
		closeBlock();
	}
	if (std::ssize(program->resources) > 0) {
		writeNewLine();
		writeLine("class Fu");
		openBlock();
		for (const auto &[name, content] : program->resources) {
			write("static ");
			writeResourceName(name);
			writeLine(" = new Uint8Array([");
			writeChar('\t');
			writeBytes(&content);
			writeLine(" ]);");
		}
		writeNewLine();
		closeBlock();
	}
	if (program->main != nullptr)
		writeMain(program->main);
}

void GenJsNoModule::writeUseStrict()
{
	writeNewLine();
	writeLine("\"use strict\";");
}

void GenJsNoModule::writeProgram(const FuProgram * program)
{
	createOutputFile();
	writeUseStrict();
	writeTopLevelNatives(program);
	writeTypes(program);
	writeLib(program);
	closeFile();
}

void GenJs::startContainerType(const FuContainerType * container)
{
	GenJsNoModule::startContainerType(container);
	if (container->isPublic)
		write("export ");
}

void GenJs::writeUseStrict()
{
}

std::string_view GenTs::getTargetName() const
{
	return "TypeScript";
}

const GenTs * GenTs::withGenFullCode()
{
	this->genFullCode = true;
	return this;
}

void GenTs::visitEnumValue(const FuConst * konst, const FuConst * previous)
{
	writeEnumValue(konst);
	writeCharLine(',');
}

void GenTs::writeEnum(const FuEnum * enu)
{
	startContainerType(enu);
	write("enum ");
	write(enu->name);
	writeChar(' ');
	openBlock();
	enu->acceptValues(this);
	closeBlock();
}

void GenTs::writeTypeAndName(const FuNamedValue * value)
{
	writeName(value);
	write(": ");
	writeType(value->type.get());
}

void GenTs::writeType(const FuType * type, bool readOnly)
{
	if (dynamic_cast<const FuNumericType *>(type))
		write(type->id == FuId::longType ? "bigint" : "number");
	else if (const FuEnum *enu = dynamic_cast<const FuEnum *>(type))
		write(enu->id == FuId::boolType ? "boolean" : enu->name);
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		readOnly |= !dynamic_cast<const FuReadWriteClassType *>(klass);
		if (klass->class_->id == FuId::stringClass)
			write("string");
		else if ((klass->class_->id == FuId::arrayPtrClass && !dynamic_cast<const FuNumericType *>(klass->getElementType().get())) || (klass->class_->id == FuId::arrayStorageClass && !dynamic_cast<const FuNumericType *>(klass->getElementType().get())) || klass->class_->id == FuId::listClass || klass->class_->id == FuId::queueClass || klass->class_->id == FuId::stackClass) {
			if (readOnly)
				write("readonly ");
			if (klass->getElementType()->nullable)
				writeChar('(');
			writeType(klass->getElementType().get());
			if (klass->getElementType()->nullable)
				writeChar(')');
			write("[]");
		}
		else {
			if (readOnly && klass->class_->typeParameterCount > 0)
				write("Readonly<");
			switch (klass->class_->id) {
			case FuId::arrayPtrClass:
			case FuId::arrayStorageClass:
				writeArrayElementType(klass->getElementType().get());
				write("Array");
				break;
			case FuId::hashSetClass:
			case FuId::sortedSetClass:
				write("Set<");
				writeType(klass->getElementType().get(), false);
				writeChar('>');
				break;
			case FuId::dictionaryClass:
			case FuId::sortedDictionaryClass:
				if (dynamic_cast<const FuEnum *>(klass->getKeyType()))
					write("Partial<");
				write("Record<");
				writeType(klass->getKeyType());
				write(", ");
				writeType(klass->getValueType().get());
				writeChar('>');
				if (dynamic_cast<const FuEnum *>(klass->getKeyType()))
					writeChar('>');
				break;
			case FuId::orderedDictionaryClass:
				write("Map<");
				writeType(klass->getKeyType());
				write(", ");
				writeType(klass->getValueType().get());
				writeChar('>');
				break;
			case FuId::regexClass:
				write("RegExp");
				break;
			case FuId::matchClass:
				write("RegExpMatchArray");
				break;
			case FuId::jsonElementClass:
				write("any");
				break;
			default:
				write(klass->class_->name);
				break;
			}
			if (readOnly && klass->class_->typeParameterCount > 0)
				writeChar('>');
		}
		if (type->nullable)
			write(" | null");
	}
	else
		write(type->name);
}

void GenTs::writeAsType(const FuVar * def)
{
	write(" as ");
	write(def->type->name);
}

void GenTs::writeBinaryOperand(const FuExpr * expr, FuPriority parent, const FuBinaryExpr * binary)
{
	const FuType * type = binary->type.get();
	if (dynamic_cast<const FuNumericType *>(expr->type.get()) && binary->isRel()) {
		type = this->system->promoteNumericTypes(binary->left->type, binary->right->type).get();
	}
	writeCoerced(type, expr, parent);
}

void GenTs::writeEqualOperand(const FuExpr * expr, const FuExpr * other)
{
	if (dynamic_cast<const FuNumericType *>(expr->type.get()))
		writeCoerced(this->system->promoteNumericTypes(expr->type, other->type).get(), expr, FuPriority::equality);
	else
		expr->accept(this, FuPriority::equality);
}

void GenTs::writeBoolAndOr(const FuBinaryExpr * expr)
{
	write("[ ");
	expr->left->accept(this, FuPriority::argument);
	write(", ");
	expr->right->accept(this, FuPriority::argument);
	write(" ].");
	write(expr->op == FuToken::and_ ? "every" : "some");
	write("(Boolean)");
}

void GenTs::defineIsVar(const FuBinaryExpr * binary)
{
	if (const FuVar *def = dynamic_cast<const FuVar *>(binary->right.get())) {
		ensureChildBlock();
		write("let ");
		writeName(def);
		write(": ");
		writeType(binary->left->type.get());
		endStatement();
	}
}

void GenTs::writeVisibility(FuVisibility visibility)
{
	switch (visibility) {
	case FuVisibility::private_:
	case FuVisibility::internal:
		break;
	case FuVisibility::protected_:
		write("protected ");
		break;
	case FuVisibility::public_:
		write("public ");
		break;
	default:
		std::abort();
	}
}

void GenTs::writeConst(const FuConst * konst)
{
	writeNewLine();
	writeDoc(konst->documentation.get());
	writeVisibility(konst->visibility);
	write("static readonly ");
	writeName(konst);
	write(": ");
	writeType(konst->type.get(), true);
	if (this->genFullCode) {
		write(" = ");
		konst->value->accept(this, FuPriority::argument);
	}
	writeCharLine(';');
}

void GenTs::writeField(const FuField * field)
{
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	if (field->type->isFinal() && !field->isAssignableStorage())
		write("readonly ");
	writeTypeAndName(field);
	if (this->genFullCode)
		writeVarInit(field);
	writeCharLine(';');
}

void GenTs::writeMethod(const FuMethod * method)
{
	writeNewLine();
	writeMethodDoc(method);
	writeVisibility(method->visibility);
	switch (method->callType) {
	case FuCallType::static_:
		write("static ");
		break;
	case FuCallType::virtual_:
		break;
	case FuCallType::abstract:
		write("abstract ");
		break;
	case FuCallType::override_:
		break;
	case FuCallType::normal:
		break;
	case FuCallType::sealed:
		break;
	default:
		std::abort();
	}
	writeName(method);
	writeChar('(');
	int i = 0;
	for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
		if (i > 0)
			write(", ");
		writeName(param);
		if (param->value != nullptr && !this->genFullCode)
			writeChar('?');
		write(": ");
		writeType(param->type.get());
		if (param->value != nullptr && this->genFullCode)
			writeVarInit(param);
		i++;
	}
	write("): ");
	writeType(method->type.get());
	if (this->genFullCode)
		writeBody(method);
	else
		writeCharLine(';');
}

void GenTs::writeClass(const FuClass * klass, const FuProgram * program)
{
	if (!writeBaseClass(klass, program))
		return;
	startContainerType(klass);
	switch (klass->callType) {
	case FuCallType::normal:
		break;
	case FuCallType::abstract:
		write("abstract ");
		break;
	case FuCallType::static_:
	case FuCallType::sealed:
		break;
	default:
		std::abort();
	}
	openJsClass(klass);
	if (needsConstructor(klass) || klass->callType == FuCallType::static_) {
		if (klass->constructor != nullptr) {
			writeDoc(klass->constructor->documentation.get());
			writeVisibility(klass->constructor->visibility);
		}
		else if (klass->callType == FuCallType::static_)
			write("private ");
		if (this->genFullCode)
			writeConstructor(klass);
		else
			writeLine("constructor();");
	}
	writeMembers(klass, this->genFullCode);
	closeBlock();
}

void GenTs::writeProgram(const FuProgram * program)
{
	this->system = program->system;
	createOutputFile();
	if (this->genFullCode)
		writeTopLevelNatives(program);
	writeTypes(program);
	if (this->genFullCode)
		writeLib(program);
	closeFile();
}

void GenPySwift::writeDocPara(const FuDocPara * para, bool many)
{
	if (many) {
		writeNewLine();
		startDocLine();
		writeNewLine();
		startDocLine();
	}
	for (const std::shared_ptr<FuDocInline> &inline_ : para->children) {
		if (const FuDocText *text = dynamic_cast<const FuDocText *>(inline_.get()))
			write(text->text);
		else if (const FuDocCode *code = dynamic_cast<const FuDocCode *>(inline_.get())) {
			writeChar('`');
			writeDocCode(code->text);
			writeChar('`');
		}
		else if (dynamic_cast<const FuDocLine *>(inline_.get())) {
			writeNewLine();
			startDocLine();
		}
		else
			std::abort();
	}
}

void GenPySwift::writeDocList(const FuDocList * list)
{
	writeNewLine();
	for (const FuDocPara &item : list->items) {
		write(getDocBullet());
		writeDocPara(&item, false);
		writeNewLine();
	}
	startDocLine();
}

void GenPySwift::writeLocalName(const FuSymbol * symbol, FuPriority parent)
{
	if (const FuMember *member = dynamic_cast<const FuMember *>(symbol)) {
		if (member->isStatic())
			writeName(this->currentMethod->parent);
		else
			write("self");
		writeChar('.');
	}
	writeName(symbol);
}

void GenPySwift::visitAggregateInitializer(const FuAggregateInitializer * expr)
{
	write("[ ");
	writeCoercedLiterals(expr->type->asClassType()->getElementType().get(), &expr->items);
	write(" ]");
}

void GenPySwift::visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::increment:
	case FuToken::decrement:
		expr->inner->accept(this, parent);
		break;
	default:
		GenBase::visitPrefixExpr(expr, parent);
		break;
	}
}

void GenPySwift::visitPostfixExpr(const FuPostfixExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::increment:
	case FuToken::decrement:
		expr->inner->accept(this, parent);
		break;
	default:
		GenBase::visitPostfixExpr(expr, parent);
		break;
	}
}

bool GenPySwift::isPtr(const FuExpr * expr)
{
	const FuClassType * klass;
	return (klass = dynamic_cast<const FuClassType *>(expr->type.get())) && klass->class_->id != FuId::stringClass && !dynamic_cast<const FuStorageType *>(klass);
}

void GenPySwift::writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_)
{
	if (isPtr(left) || isPtr(right))
		writeEqualExpr(left, right, parent, getReferenceEqOp(not_));
	else
		GenBase::writeEqual(left, right, parent, not_);
}

void GenPySwift::writeExpr(const FuExpr * expr, FuPriority parent)
{
	expr->accept(this, parent);
}

void GenPySwift::writeListAppend(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	writePostfix(obj, ".append(");
	const FuType * elementType = obj->type->asClassType()->getElementType().get();
	if (std::ssize(*args) == 0)
		writeNewStorage(elementType);
	else
		writeCoerced(elementType, (*args)[0].get(), FuPriority::argument);
	writeChar(')');
}

bool GenPySwift::visitPreCall(const FuCallExpr * call)
{
	return false;
}

bool GenPySwift::visitXcrement(const FuExpr * expr, bool postfix, bool write)
{
	bool seen;
	if (const FuVar *def = dynamic_cast<const FuVar *>(expr))
		return def->value != nullptr && visitXcrement(def->value.get(), postfix, write);
	else if (dynamic_cast<const FuAggregateInitializer *>(expr) || dynamic_cast<const FuLiteral *>(expr) || dynamic_cast<const FuLambdaExpr *>(expr))
		return false;
	else if (const FuInterpolatedString *interp = dynamic_cast<const FuInterpolatedString *>(expr)) {
		seen = false;
		for (const FuInterpolatedPart &part : interp->parts)
			seen |= visitXcrement(part.argument.get(), postfix, write);
		return seen;
	}
	else if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr))
		return symbol->left != nullptr && visitXcrement(symbol->left.get(), postfix, write);
	else if (const FuUnaryExpr *unary = dynamic_cast<const FuUnaryExpr *>(expr)) {
		if (unary->inner == nullptr)
			return false;
		seen = visitXcrement(unary->inner.get(), postfix, write);
		if ((unary->op == FuToken::increment || unary->op == FuToken::decrement) && postfix == !!dynamic_cast<const FuPostfixExpr *>(unary)) {
			if (write) {
				writeExpr(unary->inner.get(), FuPriority::assign);
				writeLine(unary->op == FuToken::increment ? " += 1" : " -= 1");
			}
			seen = true;
		}
		return seen;
	}
	else if (const FuBinaryExpr *binary = dynamic_cast<const FuBinaryExpr *>(expr)) {
		seen = visitXcrement(binary->left.get(), postfix, write);
		if (binary->op == FuToken::is)
			return seen;
		if (binary->op == FuToken::condAnd || binary->op == FuToken::condOr)
			assert(!visitXcrement(binary->right.get(), postfix, false));
		else
			seen |= visitXcrement(binary->right.get(), postfix, write);
		return seen;
	}
	else if (const FuSelectExpr *select = dynamic_cast<const FuSelectExpr *>(expr)) {
		seen = visitXcrement(select->cond.get(), postfix, write);
		assert(!visitXcrement(select->onTrue.get(), postfix, false));
		assert(!visitXcrement(select->onFalse.get(), postfix, false));
		return seen;
	}
	else if (const FuCallExpr *call = dynamic_cast<const FuCallExpr *>(expr)) {
		seen = visitXcrement(call->method.get(), postfix, write);
		for (const std::shared_ptr<FuExpr> &arg : call->arguments)
			seen |= visitXcrement(arg.get(), postfix, write);
		if (!postfix)
			seen |= visitPreCall(call);
		return seen;
	}
	else
		std::abort();
}

void GenPySwift::visitExpr(const FuExpr * statement)
{
	visitXcrement(statement, false, true);
	const FuUnaryExpr * unary;
	if (!(unary = dynamic_cast<const FuUnaryExpr *>(statement)) || (unary->op != FuToken::increment && unary->op != FuToken::decrement)) {
		writeExpr(statement, FuPriority::statement);
		writeNewLine();
		if (const FuVar *def = dynamic_cast<const FuVar *>(statement))
			writeInitCode(def);
	}
	visitXcrement(statement, true, true);
	cleanupTemporaries();
}

void GenPySwift::endStatement()
{
	writeNewLine();
}

void GenPySwift::writeChild(FuStatement * statement)
{
	openChild();
	statement->acceptStatement(this);
	closeChild();
}

void GenPySwift::visitBlock(const FuBlock * statement)
{
	writeStatements(&statement->statements);
}

bool GenPySwift::openCond(std::string_view statement, const FuExpr * cond, FuPriority parent)
{
	visitXcrement(cond, false, true);
	write(statement);
	writeExpr(cond, parent);
	openChild();
	return visitXcrement(cond, true, true);
}

void GenPySwift::writeContinueDoWhile(const FuExpr * cond)
{
	openCond("if ", cond, FuPriority::argument);
	writeLine("continue");
	closeChild();
	visitXcrement(cond, true, true);
	writeLine("break");
}

bool GenPySwift::needCondXcrement(const FuLoop * loop)
{
	return loop->cond != nullptr;
}

void GenPySwift::endBody(const FuLoop * loop)
{
	if (const FuFor *forLoop = dynamic_cast<const FuFor *>(loop)) {
		if (forLoop->isRange)
			return;
		visitOptionalStatement(forLoop->advance.get());
	}
	if (needCondXcrement(loop))
		visitXcrement(loop->cond.get(), false, true);
}

void GenPySwift::visitContinue(const FuContinue * statement)
{
	if (const FuDoWhile *doWhile = dynamic_cast<const FuDoWhile *>(statement->loop))
		writeContinueDoWhile(doWhile->cond.get());
	else {
		endBody(statement->loop);
		writeLine("continue");
	}
}

void GenPySwift::openWhileTrue()
{
	write("while ");
	visitLiteralTrue();
	openChild();
}

void GenPySwift::visitDoWhile(const FuDoWhile * statement)
{
	openWhileTrue();
	statement->body->acceptStatement(this);
	if (statement->body->completesNormally()) {
		openCond(getIfNot(), statement->cond.get(), FuPriority::primary);
		writeLine("break");
		closeChild();
		visitXcrement(statement->cond.get(), true, true);
	}
	closeChild();
}

void GenPySwift::openWhile(const FuLoop * loop)
{
	openCond("while ", loop->cond.get(), FuPriority::argument);
}

void GenPySwift::closeWhile(const FuLoop * loop)
{
	loop->body->acceptStatement(this);
	if (loop->body->completesNormally())
		endBody(loop);
	closeChild();
	if (needCondXcrement(loop)) {
		if (loop->hasBreak && visitXcrement(loop->cond.get(), true, false)) {
			write("else");
			openChild();
			visitXcrement(loop->cond.get(), true, true);
			closeChild();
		}
		else
			visitXcrement(loop->cond.get(), true, true);
	}
}

void GenPySwift::visitFor(const FuFor * statement)
{
	if (statement->isRange) {
		const FuVar * indVar = static_cast<const FuVar *>(statement->init.get());
		write("for ");
		if (statement->isIndVarUsed)
			writeName(indVar);
		else
			writeChar('_');
		write(" in ");
		const FuBinaryExpr * cond = static_cast<const FuBinaryExpr *>(statement->cond.get());
		writeForRange(indVar, cond, statement->rangeStep);
		writeChild(statement->body.get());
	}
	else {
		visitOptionalStatement(statement->init.get());
		if (statement->cond != nullptr)
			openWhile(statement);
		else
			openWhileTrue();
		closeWhile(statement);
	}
}

void GenPySwift::visitIf(const FuIf * statement)
{
	bool condPostXcrement = openCond("if ", statement->cond.get(), FuPriority::argument);
	statement->onTrue->acceptStatement(this);
	closeChild();
	if (statement->onFalse == nullptr && condPostXcrement && !statement->onTrue->completesNormally())
		visitXcrement(statement->cond.get(), true, true);
	else if (statement->onFalse != nullptr || condPostXcrement) {
		FuIf * childIf;
		if (!condPostXcrement && (childIf = dynamic_cast<FuIf *>(statement->onFalse.get())) && !visitXcrement(childIf->cond.get(), false, false)) {
			writeElseIf();
			visitIf(childIf);
		}
		else {
			write("else");
			openChild();
			visitXcrement(statement->cond.get(), true, true);
			visitOptionalStatement(statement->onFalse.get());
			closeChild();
		}
	}
}

void GenPySwift::visitReturn(const FuReturn * statement)
{
	if (statement->value == nullptr)
		writeLine("return");
	else {
		visitXcrement(statement->value.get(), false, true);
		writeTemporaries(statement->value.get());
		if (visitXcrement(statement->value.get(), true, false)) {
			writeResultVar();
			write(" = ");
			writeCoercedExpr(this->currentMethod->type.get(), statement->value.get());
			writeNewLine();
			visitXcrement(statement->value.get(), true, true);
			writeLine("return result");
		}
		else {
			write("return ");
			writeCoercedExpr(this->currentMethod->type.get(), statement->value.get());
			writeNewLine();
		}
		cleanupTemporaries();
	}
}

void GenPySwift::visitWhile(const FuWhile * statement)
{
	openWhile(statement);
	closeWhile(statement);
}

std::string_view GenSwift::getTargetName() const
{
	return "Swift";
}

void GenSwift::startDocLine()
{
	write("/// ");
}

void GenSwift::writeDocCode(std::string_view s)
{
	write(s == "null" ? "nil" : s);
}

std::string_view GenSwift::getDocBullet() const
{
	return "/// * ";
}

void GenSwift::writeDoc(const FuCodeDoc * doc)
{
	if (doc != nullptr)
		writeContent(doc);
}

void GenSwift::writeCamelCaseNotKeyword(std::string_view name)
{
	if (name == "this")
		write("self");
	else if (name == "As" || name == "Associatedtype" || name == "Await" || name == "Break" || name == "Case" || name == "Catch" || name == "Class" || name == "Continue" || name == "Default" || name == "Defer" || name == "Deinit" || name == "Do" || name == "Else" || name == "Enum" || name == "Extension" || name == "Fallthrough" || name == "False" || name == "Fileprivate" || name == "For" || name == "Foreach" || name == "Func" || name == "Guard" || name == "If" || name == "Import" || name == "In" || name == "Init" || name == "Inout" || name == "Int" || name == "Internal" || name == "Is" || name == "Let" || name == "Nil" || name == "Operator" || name == "Private" || name == "Protocol" || name == "Public" || name == "Repeat" || name == "Rethrows" || name == "Return" || name == "Self" || name == "Static" || name == "Struct" || name == "Switch" || name == "Subscript" || name == "Super" || name == "Throw" || name == "Throws" || name == "True" || name == "Try" || name == "Typealias" || name == "Var" || name == "Void" || name == "Where" || name == "While" || name == "as" || name == "associatedtype" || name == "await" || name == "catch" || name == "defer" || name == "deinit" || name == "extension" || name == "fallthrough" || name == "fileprivate" || name == "func" || name == "guard" || name == "import" || name == "init" || name == "inout" || name == "is" || name == "let" || name == "nil" || name == "operator" || name == "private" || name == "protocol" || name == "repeat" || name == "rethrows" || name == "self" || name == "struct" || name == "subscript" || name == "super" || name == "try" || name == "typealias" || name == "var" || name == "where") {
		writeCamelCase(name);
		writeChar('_');
	}
	else
		writeCamelCase(name);
}

void GenSwift::writeName(const FuSymbol * symbol)
{
	const FuConst * konst;
	if (dynamic_cast<const FuContainerType *>(symbol))
		write(symbol->name);
	else if ((konst = dynamic_cast<const FuConst *>(symbol)) && konst->inMethod != nullptr) {
		writeCamelCase(konst->inMethod->name);
		writePascalCase(symbol->name);
		if (konst->inMethodIndex > 0)
			visitLiteralLong(konst->inMethodIndex);
	}
	else if (dynamic_cast<const FuVar *>(symbol) || dynamic_cast<const FuMember *>(symbol))
		writeCamelCaseNotKeyword(symbol->name);
	else
		std::abort();
}

void GenSwift::writeLocalName(const FuSymbol * symbol, FuPriority parent)
{
	const FuForeach * forEach;
	if ((forEach = dynamic_cast<const FuForeach *>(symbol->parent)) && dynamic_cast<const FuStringType *>(forEach->collection->type.get())) {
		write("Int(");
		writeCamelCaseNotKeyword(symbol->name);
		write(".value)");
	}
	else
		GenPySwift::writeLocalName(symbol, parent);
}

void GenSwift::writeMemberOp(const FuExpr * left, const FuSymbolReference * symbol)
{
	if (left->type != nullptr && left->type->nullable)
		writeChar('!');
	writeChar('.');
}

void GenSwift::openIndexing(const FuExpr * collection)
{
	collection->accept(this, FuPriority::primary);
	if (collection->type->nullable)
		writeChar('!');
	writeChar('[');
}

bool GenSwift::isArrayRef(const FuArrayStorageType * array)
{
	return array->ptrTaken || dynamic_cast<const FuStorageType *>(array->getElementType().get());
}

void GenSwift::writeArrayRef(const FuType * elementType)
{
	this->arrayRef = true;
	write("ArrayRef<");
	writeType(elementType);
	writeChar('>');
}

void GenSwift::writeClassName(const FuClassType * klass)
{
	switch (klass->class_->id) {
	case FuId::stringClass:
		write("String");
		break;
	case FuId::arrayPtrClass:
		writeArrayRef(klass->getElementType().get());
		break;
	case FuId::arrayStorageClass:
	case FuId::listClass:
	case FuId::queueClass:
	case FuId::stackClass:
		writeChar('[');
		writeType(klass->getElementType().get());
		writeChar(']');
		break;
	case FuId::hashSetClass:
	case FuId::sortedSetClass:
		write("Set<");
		writeType(klass->getElementType().get());
		writeChar('>');
		break;
	case FuId::dictionaryClass:
	case FuId::sortedDictionaryClass:
		writeChar('[');
		writeType(klass->getKeyType());
		write(": ");
		writeType(klass->getValueType().get());
		writeChar(']');
		break;
	case FuId::orderedDictionaryClass:
		notSupported(klass, "OrderedDictionary");
		break;
	case FuId::jsonElementClass:
		write("Any");
		break;
	case FuId::lockClass:
		include("Foundation");
		write("NSRecursiveLock");
		break;
	default:
		write(klass->class_->name);
		break;
	}
}

void GenSwift::writeType(const FuType * type)
{
	if (dynamic_cast<const FuNumericType *>(type)) {
		switch (type->id) {
		case FuId::sByteRange:
			write("Int8");
			break;
		case FuId::byteRange:
			write("UInt8");
			break;
		case FuId::shortRange:
			write("Int16");
			break;
		case FuId::uShortRange:
			write("UInt16");
			break;
		case FuId::intType:
		case FuId::nIntType:
			write("Int");
			break;
		case FuId::longType:
			write("Int64");
			break;
		case FuId::floatType:
			write("Float");
			break;
		case FuId::doubleType:
			write("Double");
			break;
		default:
			std::abort();
		}
	}
	else if (dynamic_cast<const FuEnum *>(type))
		write(type->id == FuId::boolType ? "Bool" : type->name);
	else if (const FuArrayStorageType *arrayStg = dynamic_cast<const FuArrayStorageType *>(type)) {
		if (isArrayRef(arrayStg))
			writeArrayRef(arrayStg->getElementType().get());
		else {
			writeChar('[');
			writeType(arrayStg->getElementType().get());
			writeChar(']');
		}
	}
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		writeClassName(klass);
		if (klass->nullable)
			writeChar('?');
	}
	else
		std::abort();
}

void GenSwift::writeTypeAndName(const FuNamedValue * value)
{
	writeName(value);
	if (!value->type->isFinal() || value->isAssignableStorage()) {
		write(" : ");
		writeType(value->type.get());
	}
}

void GenSwift::visitLiteralNull()
{
	write("nil");
}

void GenSwift::writeUnwrapped(const FuExpr * expr, FuPriority parent, bool substringOk)
{
	if (expr->type->nullable) {
		expr->accept(this, FuPriority::primary);
		writeChar('!');
	}
	else {
		const FuCallExpr * call;
		if (!substringOk && (call = dynamic_cast<const FuCallExpr *>(expr)) && call->method->symbol->id == FuId::stringSubstring)
			writeCall("String", expr);
		else
			expr->accept(this, parent);
	}
}

void GenSwift::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	if (std::any_of(expr->parts.begin(), expr->parts.end(), [](const FuInterpolatedPart &part) { return part.widthExpr != nullptr || part.format != ' ' || part.precision >= 0; })) {
		include("Foundation");
		write("String(format: ");
		writePrintf(expr, false);
	}
	else {
		writeChar('"');
		for (const FuInterpolatedPart &part : expr->parts) {
			write(part.prefix);
			write("\\(");
			writeUnwrapped(part.argument.get(), FuPriority::argument, true);
			writeChar(')');
		}
		write(expr->suffix);
		writeChar('"');
	}
}

void GenSwift::writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent)
{
	const FuBinaryExpr * binary;
	if (dynamic_cast<const FuNumericType *>(type) && !dynamic_cast<const FuLiteral *>(expr) && getTypeId(type, false) != getTypeId(expr->type.get(), (binary = dynamic_cast<const FuBinaryExpr *>(expr)) && binary->op != FuToken::leftBracket)) {
		writeType(type);
		writeChar('(');
		const FuCallExpr * call;
		if (dynamic_cast<const FuIntegerType *>(type) && (call = dynamic_cast<const FuCallExpr *>(expr)) && call->method->symbol->id == FuId::mathTruncate)
			call->arguments[0]->accept(this, FuPriority::argument);
		else
			expr->accept(this, FuPriority::argument);
		writeChar(')');
	}
	else if (!type->nullable)
		writeUnwrapped(expr, parent, false);
	else
		expr->accept(this, parent);
}

void GenSwift::writeStringLength(const FuExpr * expr)
{
	writeUnwrapped(expr, FuPriority::primary, true);
	write(".count");
}

void GenSwift::writeArrayLength(const FuExpr * expr, FuPriority parent)
{
	writePostfix(expr, ".count");
}

void GenSwift::writeCharAt(const FuBinaryExpr * expr)
{
	this->stringCharAt = true;
	write("fuStringCharAt(");
	writeUnwrapped(expr->left.get(), FuPriority::argument, false);
	write(", ");
	expr->right->accept(this, FuPriority::argument);
	writeChar(')');
}

void GenSwift::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::mathNaN:
		write("Float.nan");
		break;
	case FuId::mathNegativeInfinity:
		write("-Float.infinity");
		break;
	case FuId::mathPositiveInfinity:
		write("Float.infinity");
		break;
	default:
		GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

std::string_view GenSwift::getReferenceEqOp(bool not_) const
{
	return not_ ? " !== " : " === ";
}

void GenSwift::writeStringContains(const FuExpr * obj, std::string_view name, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	writeUnwrapped(obj, FuPriority::primary, true);
	writeChar('.');
	write(name);
	writeChar('(');
	writeUnwrapped((*args)[0].get(), FuPriority::argument, true);
	writeChar(')');
}

void GenSwift::writeRange(const FuExpr * startIndex, const FuExpr * length)
{
	writeCoerced(this->system->intType.get(), startIndex, FuPriority::shift);
	write("..<");
	writeAdd(startIndex, length);
}

bool GenSwift::addVar(std::string_view name)
{
	std::unordered_set<std::string_view> * vars = &this->varsAtIndent[this->indent];
	if (vars->contains(name))
		return false;
	vars->insert(name);
	return true;
}

void GenSwift::writeJsonElementIs(const FuExpr * obj, std::string_view name, FuPriority parent)
{
	if (parent > FuPriority::equality)
		writeChar('(');
	obj->accept(this, FuPriority::equality);
	write(" is ");
	write(name);
	if (parent > FuPriority::equality)
		writeChar(')');
}

void GenSwift::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::none:
	case FuId::arrayContains:
	case FuId::listContains:
	case FuId::listSortAll:
	case FuId::hashSetContains:
	case FuId::hashSetRemove:
	case FuId::sortedSetContains:
	case FuId::sortedSetRemove:
		if (obj == nullptr) {
			if (method->isStatic()) {
				writeName(this->currentMethod->parent);
				writeChar('.');
			}
		}
		else if (isReferenceTo(obj, FuId::basePtr))
			write("super.");
		else {
			obj->accept(this, FuPriority::primary);
			writeMemberOp(obj, nullptr);
		}
		writeName(method);
		writeCoercedArgsInParentheses(method, args);
		break;
	case FuId::classToString:
		obj->accept(this, FuPriority::primary);
		writeMemberOp(obj, nullptr);
		write("description");
		break;
	case FuId::enumFromInt:
		write(method->type->name);
		write("(rawValue: ");
		(*args)[0]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::enumHasFlag:
		writeMethodCall(obj, "contains", (*args)[0].get());
		break;
	case FuId::stringContains:
		writeStringContains(obj, "contains", args);
		break;
	case FuId::stringEndsWith:
		writeStringContains(obj, "hasSuffix", args);
		break;
	case FuId::stringIndexOf:
		include("Foundation");
		this->stringIndexOf = true;
		write("fuStringIndexOf(");
		writeUnwrapped(obj, FuPriority::argument, true);
		write(", ");
		writeUnwrapped((*args)[0].get(), FuPriority::argument, true);
		writeChar(')');
		break;
	case FuId::stringLastIndexOf:
		include("Foundation");
		this->stringIndexOf = true;
		write("fuStringIndexOf(");
		writeUnwrapped(obj, FuPriority::argument, true);
		write(", ");
		writeUnwrapped((*args)[0].get(), FuPriority::argument, true);
		write(", .backwards)");
		break;
	case FuId::stringReplace:
		writeUnwrapped(obj, FuPriority::primary, true);
		write(".replacingOccurrences(of: ");
		writeUnwrapped((*args)[0].get(), FuPriority::argument, true);
		write(", with: ");
		writeUnwrapped((*args)[1].get(), FuPriority::argument, true);
		writeChar(')');
		break;
	case FuId::stringStartsWith:
		writeStringContains(obj, "hasPrefix", args);
		break;
	case FuId::stringSubstring:
		if ((*args)[0]->isLiteralZero())
			writeUnwrapped(obj, FuPriority::primary, true);
		else {
			this->stringSubstring = true;
			write("fuStringSubstring(");
			writeUnwrapped(obj, FuPriority::argument, false);
			write(", ");
			writeCoerced(this->system->intType.get(), (*args)[0].get(), FuPriority::argument);
			writeChar(')');
		}
		if (std::ssize(*args) == 2) {
			write(".prefix(");
			writeCoerced(this->system->intType.get(), (*args)[1].get(), FuPriority::argument);
			writeChar(')');
		}
		break;
	case FuId::stringToLower:
		writePostfix(obj, ".lowercased()");
		break;
	case FuId::stringToUpper:
		writePostfix(obj, ".uppercased()");
		break;
	case FuId::arrayCopyTo:
	case FuId::listCopyTo:
		openIndexing((*args)[1].get());
		writeRange((*args)[2].get(), (*args)[3].get());
		write("] = ");
		openIndexing(obj);
		writeRange((*args)[0].get(), (*args)[3].get());
		writeChar(']');
		break;
	case FuId::arrayFillAll:
		obj->accept(this, FuPriority::assign);
		{
			const FuArrayStorageType * array;
			if ((array = dynamic_cast<const FuArrayStorageType *>(obj->type.get())) && !isArrayRef(array)) {
				write(" = [");
				writeType(array->getElementType().get());
				write("](repeating: ");
				writeCoerced(array->getElementType().get(), (*args)[0].get(), FuPriority::argument);
				write(", count: ");
				visitLiteralLong(array->length);
				writeChar(')');
			}
			else {
				write(".fill");
				writeCoercedArgsInParentheses(method, args);
			}
			break;
		}
	case FuId::arrayFillPart:
		{
			const FuArrayStorageType * array2;
			if ((array2 = dynamic_cast<const FuArrayStorageType *>(obj->type.get())) && !isArrayRef(array2)) {
				openIndexing(obj);
				writeRange((*args)[1].get(), (*args)[2].get());
				write("] = ArraySlice(repeating: ");
				writeCoerced(array2->getElementType().get(), (*args)[0].get(), FuPriority::argument);
				write(", count: ");
				writeCoerced(this->system->intType.get(), (*args)[2].get(), FuPriority::argument);
				writeChar(')');
			}
			else {
				obj->accept(this, FuPriority::primary);
				writeMemberOp(obj, nullptr);
				write("fill");
				writeCoercedArgsInParentheses(method, args);
			}
			break;
		}
	case FuId::arraySortAll:
		writePostfix(obj, "[0..<");
		{
			const FuArrayStorageType * array3 = static_cast<const FuArrayStorageType *>(obj->type.get());
			visitLiteralLong(array3->length);
			write("].sort()");
			break;
		}
	case FuId::arraySortPart:
	case FuId::listSortPart:
		openIndexing(obj);
		writeRange((*args)[0].get(), (*args)[1].get());
		write("].sort()");
		break;
	case FuId::listAdd:
	case FuId::queueEnqueue:
	case FuId::stackPush:
		writeListAppend(obj, args);
		break;
	case FuId::listAddRange:
		obj->accept(this, FuPriority::assign);
		write(" += ");
		(*args)[0]->accept(this, FuPriority::argument);
		break;
	case FuId::listAll:
		writePostfix(obj, ".allSatisfy ");
		(*args)[0]->accept(this, FuPriority::argument);
		break;
	case FuId::listAny:
		writePostfix(obj, ".contains ");
		(*args)[0]->accept(this, FuPriority::argument);
		break;
	case FuId::listClear:
	case FuId::queueClear:
	case FuId::stackClear:
	case FuId::hashSetClear:
	case FuId::sortedSetClear:
	case FuId::dictionaryClear:
	case FuId::sortedDictionaryClear:
		writePostfix(obj, ".removeAll()");
		break;
	case FuId::listIndexOf:
		if (parent > FuPriority::rel)
			writeChar('(');
		writePostfix(obj, ".firstIndex(of: ");
		(*args)[0]->accept(this, FuPriority::argument);
		write(") ?? -1");
		if (parent > FuPriority::rel)
			writeChar(')');
		break;
	case FuId::listInsert:
		writePostfix(obj, ".insert(");
		{
			const FuType * elementType = obj->type->asClassType()->getElementType().get();
			if (std::ssize(*args) == 1)
				writeNewStorage(elementType);
			else
				writeCoerced(elementType, (*args)[1].get(), FuPriority::argument);
			write(", at: ");
			writeCoerced(this->system->intType.get(), (*args)[0].get(), FuPriority::argument);
			writeChar(')');
			break;
		}
	case FuId::listLast:
	case FuId::stackPeek:
		writePostfix(obj, ".last");
		break;
	case FuId::listRemoveAt:
		writePostfix(obj, ".remove(at: ");
		writeCoerced(this->system->intType.get(), (*args)[0].get(), FuPriority::argument);
		writeChar(')');
		break;
	case FuId::listRemoveRange:
		writePostfix(obj, ".removeSubrange(");
		writeRange((*args)[0].get(), (*args)[1].get());
		writeChar(')');
		break;
	case FuId::queueDequeue:
		writePostfix(obj, ".removeFirst()");
		break;
	case FuId::queuePeek:
		writePostfix(obj, ".first");
		break;
	case FuId::stackPop:
		writePostfix(obj, ".removeLast()");
		break;
	case FuId::hashSetAdd:
	case FuId::sortedSetAdd:
		writePostfix(obj, ".insert(");
		writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), FuPriority::argument);
		writeChar(')');
		break;
	case FuId::dictionaryAdd:
		writeDictionaryAdd(obj, args);
		break;
	case FuId::dictionaryContainsKey:
	case FuId::sortedDictionaryContainsKey:
		if (parent > FuPriority::equality)
			writeChar('(');
		writeIndexing(obj, (*args)[0].get());
		write(" != nil");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::dictionaryRemove:
	case FuId::sortedDictionaryRemove:
		writePostfix(obj, ".removeValue(forKey: ");
		(*args)[0]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::consoleWrite:
		write("print(");
		writeUnwrapped((*args)[0].get(), FuPriority::argument, true);
		write(", terminator: \"\")");
		break;
	case FuId::consoleWriteLine:
		write("print(");
		if (std::ssize(*args) == 1)
			writeUnwrapped((*args)[0].get(), FuPriority::argument, true);
		writeChar(')');
		break;
	case FuId::convertToBase64String:
		write("Data(");
		openIndexing((*args)[0].get());
		writeRange((*args)[1].get(), (*args)[2].get());
		write("]).base64EncodedString()");
		break;
	case FuId::uTF8GetByteCount:
		writeUnwrapped((*args)[0].get(), FuPriority::primary, true);
		write(".utf8.count");
		break;
	case FuId::uTF8GetBytes:
		if (addVar("fubytes"))
			write(this->varBytesAtIndent[this->indent] ? "var " : "let ");
		write("fubytes = [UInt8](");
		writeUnwrapped((*args)[0].get(), FuPriority::primary, true);
		writeLine(".utf8)");
		openIndexing((*args)[1].get());
		writeCoerced(this->system->intType.get(), (*args)[2].get(), FuPriority::shift);
		if ((*args)[2]->isLiteralZero())
			write("..<");
		else {
			write(" ..< ");
			writeCoerced(this->system->intType.get(), (*args)[2].get(), FuPriority::add);
			write(" + ");
		}
		writeLine("fubytes.count] = fubytes[...]");
		break;
	case FuId::uTF8GetString:
		write("String(decoding: ");
		openIndexing((*args)[0].get());
		writeRange((*args)[1].get(), (*args)[2].get());
		write("], as: UTF8.self)");
		break;
	case FuId::environmentGetEnvironmentVariable:
		include("Foundation");
		write("ProcessInfo.processInfo.environment[");
		writeUnwrapped((*args)[0].get(), FuPriority::argument, false);
		writeChar(']');
		break;
	case FuId::jsonElementParse:
		include("Foundation");
		write("try! JSONSerialization.jsonObject(with: ");
		writePostfix((*args)[0].get(), ".data(using: .utf8)!, options: .fragmentsAllowed)");
		break;
	case FuId::jsonElementIsObject:
		writeJsonElementIs(obj, "[String: Any]", parent);
		break;
	case FuId::jsonElementIsArray:
		writeJsonElementIs(obj, "[Any]", parent);
		break;
	case FuId::jsonElementIsString:
		writeJsonElementIs(obj, "String", parent);
		break;
	case FuId::jsonElementIsNumber:
		writeJsonElementIs(obj, "Double", parent);
		break;
	case FuId::jsonElementIsBoolean:
		writeJsonElementIs(obj, "Bool", parent);
		break;
	case FuId::jsonElementIsNull:
		writeJsonElementIs(obj, "NSNull", parent);
		break;
	case FuId::jsonElementGetObject:
	case FuId::jsonElementGetArray:
	case FuId::jsonElementGetString:
	case FuId::jsonElementGetDouble:
	case FuId::jsonElementGetBoolean:
		if (parent > FuPriority::equality)
			writeChar('(');
		obj->accept(this, FuPriority::equality);
		write(" as! ");
		writeType(method->type.get());
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::mathMethod:
	case FuId::mathLog2:
		include("Foundation");
		writeCamelCase(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathAbs:
	case FuId::mathMax:
	case FuId::mathMin:
		writeCamelCase(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathCeiling:
		include("Foundation");
		writeCall("ceil", (*args)[0].get());
		break;
	case FuId::mathClamp:
		write("min(max(");
		writeClampAsMinMax(args);
		break;
	case FuId::mathFusedMultiplyAdd:
		include("Foundation");
		writeCall("fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case FuId::mathIsFinite:
		writePostfix((*args)[0].get(), ".isFinite");
		break;
	case FuId::mathIsInfinity:
		writePostfix((*args)[0].get(), ".isInfinite");
		break;
	case FuId::mathIsNaN:
		writePostfix((*args)[0].get(), ".isNaN");
		break;
	case FuId::mathRound:
		writePostfix((*args)[0].get(), ".rounded()");
		break;
	case FuId::mathTruncate:
		include("Foundation");
		writeCall("trunc", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenSwift::writeNewArrayStorage(const FuArrayStorageType * array)
{
	if (isArrayRef(array))
		GenBase::writeNewArrayStorage(array);
	else {
		writeChar('[');
		writeType(array->getElementType().get());
		write("](repeating: ");
		writeDefaultValue(array->getElementType().get());
		write(", count: ");
		visitLiteralLong(array->length);
		writeChar(')');
	}
}

void GenSwift::writeNew(const FuReadWriteClassType * klass, FuPriority parent)
{
	writeClassName(klass);
	write("()");
}

void GenSwift::writeDefaultValue(const FuType * type)
{
	if (dynamic_cast<const FuNumericType *>(type))
		writeChar('0');
	else if (const FuEnum *enu = dynamic_cast<const FuEnum *>(type)) {
		if (enu->id == FuId::boolType)
			write("false");
		else {
			writeName(enu);
			writeChar('.');
			writeName(enu->getFirstValue());
		}
	}
	else if (dynamic_cast<const FuStringType *>(type) && !type->nullable)
		write("\"\"");
	else if (const FuArrayStorageType *array = dynamic_cast<const FuArrayStorageType *>(type))
		writeNewArrayStorage(array);
	else
		write("nil");
}

void GenSwift::writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent)
{
	writeArrayRef(elementType);
	writeChar('(');
	if (dynamic_cast<const FuArrayStorageType *>(elementType)) {
		write("factory: { ");
		writeNewStorage(elementType);
		write(" }");
	}
	else if (const FuStorageType *klass = dynamic_cast<const FuStorageType *>(elementType)) {
		write("factory: ");
		writeName(klass->class_);
		write(".init");
	}
	else {
		write("repeating: ");
		writeDefaultValue(elementType);
	}
	write(", count: ");
	lengthExpr->accept(this, FuPriority::argument);
	writeChar(')');
}

void GenSwift::visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent)
{
	if (expr->op == FuToken::tilde && dynamic_cast<const FuEnumFlags *>(expr->type.get())) {
		write(expr->type->name);
		write("(rawValue: ~");
		writePostfix(expr->inner.get(), ".rawValue)");
	}
	else
		GenPySwift::visitPrefixExpr(expr, parent);
}

void GenSwift::writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	openIndexing(expr->left.get());
	const FuClassType * klass = static_cast<const FuClassType *>(expr->left->type.get());
	const FuType * indexType;
	switch (klass->class_->id) {
	case FuId::arrayPtrClass:
	case FuId::arrayStorageClass:
	case FuId::listClass:
		indexType = this->system->intType.get();
		break;
	default:
		indexType = klass->getKeyType();
		break;
	}
	writeCoerced(indexType, expr->right.get(), FuPriority::argument);
	writeChar(']');
	const FuClassType * dict;
	if (parent != FuPriority::assign && (dict = dynamic_cast<const FuClassType *>(expr->left->type.get())) && dict->class_->typeParameterCount == 2)
		writeChar('!');
}

void GenSwift::writeBinaryOperand(const FuExpr * expr, FuPriority parent, const FuBinaryExpr * binary)
{
	if (expr->type->id != FuId::boolType) {
		if (binary->op == FuToken::plus && binary->type->id == FuId::stringStorageType) {
			writeUnwrapped(expr, parent, true);
			return;
		}
		if (binary->op == FuToken::plus || binary->op == FuToken::minus || binary->op == FuToken::asterisk || binary->op == FuToken::slash || binary->op == FuToken::mod || binary->op == FuToken::and_ || binary->op == FuToken::or_ || binary->op == FuToken::xor_ || (binary->op == FuToken::shiftLeft && expr == binary->left.get()) || (binary->op == FuToken::shiftRight && expr == binary->left.get())) {
			if (!dynamic_cast<const FuLiteral *>(expr)) {
				const FuType * type = this->system->promoteNumericTypes(binary->left->type, binary->right->type).get();
				if (type != expr->type.get()) {
					writeCoerced(type, expr, parent);
					return;
				}
			}
		}
		else if (binary->op == FuToken::equal || binary->op == FuToken::notEqual || binary->op == FuToken::less || binary->op == FuToken::lessOrEqual || binary->op == FuToken::greater || binary->op == FuToken::greaterOrEqual) {
			const FuType * typeComp = this->system->promoteFloatingTypes(binary->left->type.get(), binary->right->type.get()).get();
			if (typeComp != nullptr && typeComp != expr->type.get()) {
				writeCoerced(typeComp, expr, parent);
				return;
			}
		}
	}
	expr->accept(this, parent);
}

void GenSwift::writeEnumFlagsAnd(const FuExpr * left, std::string_view method, std::string_view notMethod, const FuExpr * right)
{
	const FuPrefixExpr * negation;
	if ((negation = dynamic_cast<const FuPrefixExpr *>(right)) && negation->op == FuToken::tilde)
		writeMethodCall(left, notMethod, negation->inner.get());
	else
		writeMethodCall(left, method, right);
}

const FuExpr * GenSwift::writeAssignNested(const FuBinaryExpr * expr)
{
	const FuBinaryExpr * rightBinary;
	if ((rightBinary = dynamic_cast<const FuBinaryExpr *>(expr->right.get())) && rightBinary->isAssign()) {
		visitBinaryExpr(rightBinary, FuPriority::statement);
		writeNewLine();
		return rightBinary->left.get();
	}
	return expr->right.get();
}

void GenSwift::writeSwiftAssign(const FuBinaryExpr * expr, const FuExpr * right)
{
	expr->left->accept(this, FuPriority::assign);
	writeChar(' ');
	write(expr->getOpString());
	writeChar(' ');
	const FuBinaryExpr * leftBinary;
	const FuClassType * dict;
	if (dynamic_cast<const FuLiteralNull *>(right) && (leftBinary = dynamic_cast<const FuBinaryExpr *>(expr->left.get())) && leftBinary->op == FuToken::leftBracket && (dict = dynamic_cast<const FuClassType *>(leftBinary->left->type.get())) && dict->class_->typeParameterCount == 2) {
		writeType(dict->getValueType().get());
		write(".none");
	}
	else
		writeCoerced(expr->type.get(), right, FuPriority::argument);
}

void GenSwift::visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	const FuExpr * right;
	switch (expr->op) {
	case FuToken::shiftLeft:
		writeBinaryExpr(expr, parent > FuPriority::mul, FuPriority::primary, " << ", FuPriority::primary);
		break;
	case FuToken::shiftRight:
		writeBinaryExpr(expr, parent > FuPriority::mul, FuPriority::primary, " >> ", FuPriority::primary);
		break;
	case FuToken::and_:
		if (expr->type->id == FuId::boolType)
			writeCall("{ a, b in a && b }", expr->left.get(), expr->right.get());
		else if (dynamic_cast<const FuEnumFlags *>(expr->type.get()))
			writeEnumFlagsAnd(expr->left.get(), "intersection", "subtracting", expr->right.get());
		else
			writeBinaryExpr(expr, parent > FuPriority::mul, FuPriority::mul, " & ", FuPriority::primary);
		break;
	case FuToken::or_:
		if (expr->type->id == FuId::boolType)
			writeCall("{ a, b in a || b }", expr->left.get(), expr->right.get());
		else if (dynamic_cast<const FuEnumFlags *>(expr->type.get()))
			writeMethodCall(expr->left.get(), "union", expr->right.get());
		else
			writeBinaryExpr(expr, parent > FuPriority::add, FuPriority::add, " | ", FuPriority::mul);
		break;
	case FuToken::xor_:
		if (expr->type->id == FuId::boolType)
			writeEqual(expr->left.get(), expr->right.get(), parent, true);
		else if (dynamic_cast<const FuEnumFlags *>(expr->type.get()))
			writeMethodCall(expr->left.get(), "symmetricDifference", expr->right.get());
		else
			writeBinaryExpr(expr, parent > FuPriority::add, FuPriority::add, " ^ ", FuPriority::mul);
		break;
	case FuToken::assign:
	case FuToken::addAssign:
	case FuToken::subAssign:
	case FuToken::mulAssign:
	case FuToken::divAssign:
	case FuToken::modAssign:
	case FuToken::shiftLeftAssign:
	case FuToken::shiftRightAssign:
		writeSwiftAssign(expr, writeAssignNested(expr));
		break;
	case FuToken::andAssign:
		right = writeAssignNested(expr);
		if (expr->type->id == FuId::boolType) {
			write("if ");
			const FuPrefixExpr * negation;
			if ((negation = dynamic_cast<const FuPrefixExpr *>(right)) && negation->op == FuToken::exclamationMark) {
				negation->inner->accept(this, FuPriority::argument);
			}
			else {
				writeChar('!');
				right->accept(this, FuPriority::primary);
			}
			openChild();
			expr->left->accept(this, FuPriority::assign);
			writeLine(" = false");
			this->indent--;
			writeChar('}');
		}
		else if (dynamic_cast<const FuEnumFlags *>(expr->type.get()))
			writeEnumFlagsAnd(expr->left.get(), "formIntersection", "subtract", right);
		else
			writeSwiftAssign(expr, right);
		break;
	case FuToken::orAssign:
		right = writeAssignNested(expr);
		if (expr->type->id == FuId::boolType) {
			write("if ");
			right->accept(this, FuPriority::argument);
			openChild();
			expr->left->accept(this, FuPriority::assign);
			writeLine(" = true");
			this->indent--;
			writeChar('}');
		}
		else if (dynamic_cast<const FuEnumFlags *>(expr->type.get()))
			writeMethodCall(expr->left.get(), "formUnion", right);
		else
			writeSwiftAssign(expr, right);
		break;
	case FuToken::xorAssign:
		right = writeAssignNested(expr);
		if (expr->type->id == FuId::boolType) {
			expr->left->accept(this, FuPriority::assign);
			write(" = ");
			expr->left->accept(this, FuPriority::equality);
			write(" != ");
			expr->right->accept(this, FuPriority::equality);
		}
		else if (dynamic_cast<const FuEnumFlags *>(expr->type.get()))
			writeMethodCall(expr->left.get(), "formSymmetricDifference", right);
		else
			writeSwiftAssign(expr, right);
		break;
	default:
		GenBase::visitBinaryExpr(expr, parent);
		break;
	}
}

void GenSwift::writeResource(std::string_view name, int length)
{
	write("FuResource.");
	writeResourceName(name);
}

bool GenSwift::throws(const FuExpr * expr)
{
	if (dynamic_cast<const FuVar *>(expr) || dynamic_cast<const FuLiteral *>(expr) || dynamic_cast<const FuLambdaExpr *>(expr))
		return false;
	else if (const FuAggregateInitializer *init = dynamic_cast<const FuAggregateInitializer *>(expr))
		return std::any_of(init->items.begin(), init->items.end(), [](const std::shared_ptr<FuExpr> &field) { return throws(field.get()); });
	else if (const FuInterpolatedString *interp = dynamic_cast<const FuInterpolatedString *>(expr))
		return std::any_of(interp->parts.begin(), interp->parts.end(), [](const FuInterpolatedPart &part) { return throws(part.argument.get()); });
	else if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr))
		return symbol->left != nullptr && throws(symbol->left.get());
	else if (const FuUnaryExpr *unary = dynamic_cast<const FuUnaryExpr *>(expr))
		return unary->inner != nullptr && throws(unary->inner.get());
	else if (const FuBinaryExpr *binary = dynamic_cast<const FuBinaryExpr *>(expr))
		return throws(binary->left.get()) || throws(binary->right.get());
	else if (const FuSelectExpr *select = dynamic_cast<const FuSelectExpr *>(expr))
		return throws(select->cond.get()) || throws(select->onTrue.get()) || throws(select->onFalse.get());
	else if (const FuCallExpr *call = dynamic_cast<const FuCallExpr *>(expr)) {
		const FuMethod * method = static_cast<const FuMethod *>(call->method->symbol);
		return std::ssize(method->throws) > 0 || (call->method->left != nullptr && throws(call->method->left.get())) || std::any_of(call->arguments.begin(), call->arguments.end(), [](const std::shared_ptr<FuExpr> &arg) { return throws(arg.get()); });
	}
	else
		std::abort();
}

void GenSwift::writeExpr(const FuExpr * expr, FuPriority parent)
{
	if (throws(expr))
		write("try ");
	GenPySwift::writeExpr(expr, parent);
}

void GenSwift::writeCoercedExpr(const FuType * type, const FuExpr * expr)
{
	if (throws(expr))
		write("try ");
	GenBase::writeCoercedExpr(type, expr);
}

void GenSwift::startTemporaryVar(const FuType * type)
{
	write("var ");
}

void GenSwift::visitExpr(const FuExpr * statement)
{
	writeTemporaries(statement);
	const FuCallExpr * call;
	if ((call = dynamic_cast<const FuCallExpr *>(statement)) && statement->type->id != FuId::voidType)
		write("_ = ");
	GenPySwift::visitExpr(statement);
}

void GenSwift::initVarsAtIndent()
{
	while (std::ssize(this->varsAtIndent) <= this->indent) {
		this->varsAtIndent.emplace_back();
		this->varBytesAtIndent.push_back(false);
	}
	this->varsAtIndent[this->indent].clear();
	this->varBytesAtIndent[this->indent] = false;
}

void GenSwift::openChild()
{
	writeChar(' ');
	openBlock();
	initVarsAtIndent();
}

void GenSwift::closeChild()
{
	closeBlock();
}

void GenSwift::writeVar(const FuNamedValue * def)
{
	if (dynamic_cast<const FuField *>(def) || addVar(def->name)) {
		const FuArrayStorageType * array;
		const FuStorageType * stg;
		const FuVar * local;
		write(((array = dynamic_cast<const FuArrayStorageType *>(def->type.get())) ? isArrayRef(array) : (stg = dynamic_cast<const FuStorageType *>(def->type.get())) ? stg->class_->typeParameterCount == 0 && !def->isAssignableStorage() : (local = dynamic_cast<const FuVar *>(def)) && !local->isAssigned) ? "let " : "var ");
		GenBase::writeVar(def);
	}
	else {
		writeName(def);
		writeVarInit(def);
	}
}

bool GenSwift::needsVarBytes(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	int count = 0;
	for (const std::shared_ptr<FuStatement> &statement : *statements) {
		const FuCallExpr * call;
		if ((call = dynamic_cast<const FuCallExpr *>(statement.get())) && call->method->symbol->id == FuId::uTF8GetBytes) {
			if (++count == 2)
				return true;
		}
	}
	return false;
}

void GenSwift::writeStatements(const std::vector<std::shared_ptr<FuStatement>> * statements)
{
	this->varBytesAtIndent[this->indent] = needsVarBytes(statements);
	GenBase::writeStatements(statements);
}

void GenSwift::visitLambdaExpr(const FuLambdaExpr * expr)
{
	write("{ ");
	writeName(expr->first);
	write(" in ");
	expr->body->accept(this, FuPriority::statement);
	write(" }");
}

void GenSwift::writeAssertCast(const FuBinaryExpr * expr)
{
	write("let ");
	const FuVar * def = static_cast<const FuVar *>(expr->right.get());
	writeCamelCaseNotKeyword(def->name);
	write(" = ");
	expr->left->accept(this, FuPriority::equality);
	write(" as! ");
	writeLine(def->type->name);
}

void GenSwift::writeAssert(const FuAssert * statement)
{
	write("assert(");
	writeExpr(statement->cond.get(), FuPriority::argument);
	if (statement->message != nullptr) {
		write(", ");
		writeExpr(statement->message.get(), FuPriority::argument);
	}
	writeCharLine(')');
}

void GenSwift::visitBreak(const FuBreak * statement)
{
	writeLine("break");
}

bool GenSwift::needCondXcrement(const FuLoop * loop)
{
	return loop->cond != nullptr && (!loop->hasBreak || !visitXcrement(loop->cond.get(), true, false));
}

std::string_view GenSwift::getIfNot() const
{
	return "if !";
}

void GenSwift::writeContinueDoWhile(const FuExpr * cond)
{
	visitXcrement(cond, false, true);
	writeLine("continue");
}

void GenSwift::visitDoWhile(const FuDoWhile * statement)
{
	if (visitXcrement(statement->cond.get(), true, false))
		GenPySwift::visitDoWhile(statement);
	else {
		write("repeat");
		openChild();
		statement->body->acceptStatement(this);
		if (statement->body->completesNormally())
			visitXcrement(statement->cond.get(), false, true);
		closeChild();
		write("while ");
		writeExpr(statement->cond.get(), FuPriority::argument);
		writeNewLine();
	}
}

void GenSwift::writeElseIf()
{
	write("else ");
}

void GenSwift::openWhile(const FuLoop * loop)
{
	if (needCondXcrement(loop))
		GenPySwift::openWhile(loop);
	else {
		write("while true");
		openChild();
		visitXcrement(loop->cond.get(), false, true);
		write("let fuDoLoop = ");
		loop->cond->accept(this, FuPriority::argument);
		writeNewLine();
		visitXcrement(loop->cond.get(), true, true);
		write("if !fuDoLoop");
		openChild();
		writeLine("break");
		closeChild();
	}
}

void GenSwift::writeForRange(const FuVar * indVar, const FuBinaryExpr * cond, int64_t rangeStep)
{
	if (rangeStep == 1) {
		writeExpr(indVar->value.get(), FuPriority::shift);
		switch (cond->op) {
		case FuToken::less:
			write("..<");
			cond->right->accept(this, FuPriority::shift);
			break;
		case FuToken::lessOrEqual:
			write("...");
			cond->right->accept(this, FuPriority::shift);
			break;
		default:
			std::abort();
		}
	}
	else {
		write("stride(from: ");
		writeExpr(indVar->value.get(), FuPriority::argument);
		switch (cond->op) {
		case FuToken::less:
		case FuToken::greater:
			write(", to: ");
			writeExpr(cond->right.get(), FuPriority::argument);
			break;
		case FuToken::lessOrEqual:
		case FuToken::greaterOrEqual:
			write(", through: ");
			writeExpr(cond->right.get(), FuPriority::argument);
			break;
		default:
			std::abort();
		}
		write(", by: ");
		visitLiteralLong(rangeStep);
		writeChar(')');
	}
}

void GenSwift::visitForeach(const FuForeach * statement)
{
	write("for ");
	if (statement->count() == 2) {
		writeChar('(');
		writeName(statement->getVar());
		write(", ");
		writeName(statement->getValueVar());
		writeChar(')');
	}
	else
		writeName(statement->getVar());
	write(" in ");
	const FuClassType * klass = static_cast<const FuClassType *>(statement->collection->type.get());
	switch (klass->class_->id) {
	case FuId::stringClass:
		writePostfix(statement->collection.get(), ".unicodeScalars");
		break;
	case FuId::sortedSetClass:
		writePostfix(statement->collection.get(), ".sorted()");
		break;
	case FuId::sortedDictionaryClass:
		writePostfix(statement->collection.get(), klass->getKeyType()->nullable ? ".sorted(by: { $0.key! < $1.key! })" : ".sorted(by: { $0.key < $1.key })");
		break;
	default:
		writeExpr(statement->collection.get(), FuPriority::argument);
		break;
	}
	writeChild(statement->body.get());
}

void GenSwift::visitLock(const FuLock * statement)
{
	statement->lock->accept(this, FuPriority::primary);
	writeLine(".lock()");
	write("do");
	openChild();
	write("defer { ");
	statement->lock->accept(this, FuPriority::primary);
	writeLine(".unlock() }");
	statement->body->acceptStatement(this);
	closeChild();
}

void GenSwift::writeResultVar()
{
	write("let result : ");
	writeType(this->currentMethod->type.get());
}

void GenSwift::writeSwiftCaseValue(const FuSwitch * statement, const FuExpr * value)
{
	const FuSymbolReference * symbol;
	const FuClass * klass;
	const FuBinaryExpr * when1;
	if ((symbol = dynamic_cast<const FuSymbolReference *>(value)) && (klass = dynamic_cast<const FuClass *>(symbol->symbol))) {
		write("is ");
		write(klass->name);
	}
	else if (const FuVar *def = dynamic_cast<const FuVar *>(value)) {
		write("let ");
		writeCamelCaseNotKeyword(def->name);
		write(" as ");
		writeType(def->type.get());
	}
	else if ((when1 = dynamic_cast<const FuBinaryExpr *>(value)) && when1->op == FuToken::when) {
		writeSwiftCaseValue(statement, when1->left.get());
		write(" where ");
		writeExpr(when1->right.get(), FuPriority::argument);
	}
	else
		writeCoerced(statement->value->type.get(), value, FuPriority::argument);
}

void GenSwift::writeSwiftSwitchCaseBody(const FuSwitch * statement, const std::vector<std::shared_ptr<FuStatement>> * body)
{
	this->indent++;
	visitXcrement(statement->value.get(), true, true);
	initVarsAtIndent();
	writeSwitchCaseBody(body);
	this->indent--;
}

void GenSwift::visitSwitch(const FuSwitch * statement)
{
	visitXcrement(statement->value.get(), false, true);
	write("switch ");
	writeExpr(statement->value.get(), FuPriority::argument);
	writeLine(" {");
	for (const FuCase &kase : statement->cases) {
		write("case ");
		for (int i = 0; i < std::ssize(kase.values); i++) {
			writeComma(i);
			writeSwiftCaseValue(statement, kase.values[i].get());
		}
		writeCharLine(':');
		writeSwiftSwitchCaseBody(statement, &kase.body);
	}
	if (std::ssize(statement->defaultBody) > 0) {
		writeLine("default:");
		writeSwiftSwitchCaseBody(statement, &statement->defaultBody);
	}
	writeCharLine('}');
}

void GenSwift::writeException()
{
	write("Error");
}

void GenSwift::visitThrow(const FuThrow * statement)
{
	if (statement->message != nullptr)
		visitXcrement(statement->message.get(), false, true);
	write("throw ");
	if (statement->class_->name == "Exception") {
		this->throwException = true;
		write("FuError.error(");
		if (statement->message != nullptr)
			writeExpr(statement->message.get(), FuPriority::argument);
		else
			write("\"\"");
	}
	else {
		write(statement->class_->name);
		writeChar('(');
	}
	writeCharLine(')');
}

void GenSwift::writeReadOnlyParameter(const FuVar * param)
{
	write("fuParam");
	writePascalCase(param->name);
}

void GenSwift::writeParameter(const FuVar * param)
{
	write("_ ");
	if (param->isAssigned)
		writeReadOnlyParameter(param);
	else
		writeName(param);
	write(" : ");
	writeType(param->type.get());
}

void GenSwift::visitEnumValue(const FuConst * konst, const FuConst * previous)
{
	writeDoc(konst->documentation.get());
	write("static let ");
	writeName(konst);
	write(" = ");
	write(konst->parent->name);
	writeChar('(');
	int i = konst->value->intValue();
	if (i == 0)
		write("[]");
	else {
		write("rawValue: ");
		visitLiteralLong(i);
	}
	writeCharLine(')');
}

void GenSwift::writeEnum(const FuEnum * enu)
{
	writeNewLine();
	writeDoc(enu->documentation.get());
	writePublic(enu);
	if (dynamic_cast<const FuEnumFlags *>(enu)) {
		write("struct ");
		write(enu->name);
		writeLine(" : OptionSet");
		openBlock();
		writeLine("let rawValue : Int");
		enu->acceptValues(this);
	}
	else {
		write("enum ");
		write(enu->name);
		if (enu->hasExplicitValue)
			write(" : Int");
		writeNewLine();
		openBlock();
		std::unordered_map<int, const FuConst *> valueToConst;
		for (const FuSymbol * symbol = enu->first; symbol != nullptr; symbol = symbol->next) {
			if (const FuConst *konst = dynamic_cast<const FuConst *>(symbol)) {
				writeDoc(konst->documentation.get());
				int i = konst->value->intValue();
				if (valueToConst.count(i) != 0) {
					write("static let ");
					writeName(konst);
					write(" = ");
					writeName(valueToConst.find(i)->second);
				}
				else {
					write("case ");
					writeName(konst);
					if (!dynamic_cast<const FuImplicitEnumValue *>(konst->value.get())) {
						write(" = ");
						visitLiteralLong(i);
					}
					valueToConst[i] = konst;
				}
				writeNewLine();
			}
		}
	}
	closeBlock();
}

void GenSwift::writeVisibility(FuVisibility visibility)
{
	switch (visibility) {
	case FuVisibility::private_:
		write("private ");
		break;
	case FuVisibility::internal:
		write("fileprivate ");
		break;
	case FuVisibility::protected_:
	case FuVisibility::public_:
		write("public ");
		break;
	default:
		std::abort();
	}
}

void GenSwift::writeConst(const FuConst * konst)
{
	writeNewLine();
	writeDoc(konst->documentation.get());
	writeVisibility(konst->visibility);
	write("static let ");
	writeName(konst);
	write(" = ");
	if (konst->type->id == FuId::intType || dynamic_cast<const FuEnum *>(konst->type.get()) || konst->type->id == FuId::stringPtrType)
		konst->value->accept(this, FuPriority::argument);
	else {
		writeType(konst->type.get());
		writeChar('(');
		konst->value->accept(this, FuPriority::argument);
		writeChar(')');
	}
	writeNewLine();
}

void GenSwift::writeField(const FuField * field)
{
	writeNewLine();
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	const FuClassType * klass;
	if ((klass = dynamic_cast<const FuClassType *>(field->type.get())) && klass->class_->id != FuId::stringClass && !dynamic_cast<const FuOwningType *>(klass))
		write("unowned ");
	writeVar(field);
	if (field->value == nullptr && (dynamic_cast<const FuNumericType *>(field->type.get()) || dynamic_cast<const FuEnum *>(field->type.get()) || field->type->id == FuId::stringStorageType)) {
		write(" = ");
		writeDefaultValue(field->type.get());
	}
	else if (field->isAssignableStorage()) {
		write(" = ");
		writeName(field->type->asClassType()->class_);
		write("()");
	}
	writeNewLine();
}

void GenSwift::writeParameterDoc(const FuVar * param, bool first)
{
	write("/// - Parameter ");
	writeName(param);
	write(": ");
	writeDocPara(&param->documentation->summary, false);
	writeNewLine();
}

void GenSwift::writeThrowsDoc(const FuThrowsDeclaration * decl)
{
	write("/// - Throws: `");
	writeExceptionClass(decl->symbol);
	write("` ");
	writeDocPara(&decl->documentation->summary, false);
	writeNewLine();
}

void GenSwift::writeMethod(const FuMethod * method)
{
	writeNewLine();
	writeDoc(method->documentation.get());
	writeParametersAndThrowsDoc(method);
	switch (method->callType) {
	case FuCallType::static_:
		writeVisibility(method->visibility);
		write("static ");
		break;
	case FuCallType::normal:
		writeVisibility(method->visibility);
		break;
	case FuCallType::abstract:
	case FuCallType::virtual_:
		write(method->visibility == FuVisibility::internal ? "fileprivate " : "open ");
		break;
	case FuCallType::override_:
		write(method->visibility == FuVisibility::internal ? "fileprivate " : "open ");
		write("override ");
		break;
	case FuCallType::sealed:
		writeVisibility(method->visibility);
		write("final override ");
		break;
	}
	if (method->id == FuId::classToString)
		write("var description : String");
	else {
		write("func ");
		writeName(method);
		writeParameters(method, true);
		if (std::ssize(method->throws) > 0)
			write(" throws");
		if (method->type->id != FuId::voidType) {
			write(" -> ");
			writeType(method->type.get());
		}
	}
	writeNewLine();
	openBlock();
	if (method->callType == FuCallType::abstract)
		writeLine("preconditionFailure(\"Abstract method called\")");
	else {
		for (const FuVar * param = method->firstParameter(); param != nullptr; param = param->nextVar()) {
			if (param->isAssigned) {
				write("var ");
				writeTypeAndName(param);
				write(" = ");
				writeReadOnlyParameter(param);
				writeNewLine();
			}
		}
		initVarsAtIndent();
		this->currentMethod = method;
		method->body->acceptStatement(this);
		this->currentMethod = nullptr;
	}
	closeBlock();
}

void GenSwift::writeClass(const FuClass * klass, const FuProgram * program)
{
	writeNewLine();
	writeDoc(klass->documentation.get());
	writePublic(klass);
	if (klass->callType == FuCallType::sealed)
		write("final ");
	startClass(klass, "", " : ");
	if (klass->addsToString()) {
		write(klass->hasBaseClass() ? ", " : " : ");
		write("CustomStringConvertible");
	}
	writeNewLine();
	openBlock();
	if (needsConstructor(klass)) {
		if (klass->constructor != nullptr) {
			writeDoc(klass->constructor->documentation.get());
			writeVisibility(klass->constructor->visibility);
		}
		else
			write("fileprivate ");
		if (klass->hasBaseClass())
			write("override ");
		writeLine("init()");
		openBlock();
		initVarsAtIndent();
		writeConstructorBody(klass);
		closeBlock();
	}
	writeMembers(klass, true);
	closeBlock();
}

void GenSwift::writeLibrary()
{
	if (this->throwException) {
		writeNewLine();
		writeLine("public enum FuError : Error");
		openBlock();
		writeLine("case error(String)");
		closeBlock();
	}
	if (this->arrayRef) {
		writeNewLine();
		writeLine("public class ArrayRef<T> : Sequence");
		openBlock();
		writeLine("var array : [T]");
		writeNewLine();
		writeLine("init(_ array : [T])");
		openBlock();
		writeLine("self.array = array");
		closeBlock();
		writeNewLine();
		writeLine("init(repeating: T, count: Int)");
		openBlock();
		writeLine("self.array = [T](repeating: repeating, count: count)");
		closeBlock();
		writeNewLine();
		writeLine("init(factory: () -> T, count: Int)");
		openBlock();
		writeLine("self.array = (1...count).map({_ in factory() })");
		closeBlock();
		writeNewLine();
		writeLine("subscript(index: Int) -> T");
		openBlock();
		writeLine("get");
		openBlock();
		writeLine("return array[index]");
		closeBlock();
		writeLine("set(value)");
		openBlock();
		writeLine("array[index] = value");
		closeBlock();
		closeBlock();
		writeLine("subscript(bounds: Range<Int>) -> ArraySlice<T>");
		openBlock();
		writeLine("get");
		openBlock();
		writeLine("return array[bounds]");
		closeBlock();
		writeLine("set(value)");
		openBlock();
		writeLine("array[bounds] = value");
		closeBlock();
		closeBlock();
		writeNewLine();
		writeLine("func fill(_ value: T)");
		openBlock();
		writeLine("array = [T](repeating: value, count: array.count)");
		closeBlock();
		writeNewLine();
		writeLine("func fill(_ value: T, _ startIndex : Int, _ count : Int)");
		openBlock();
		writeLine("array[startIndex ..< startIndex + count] = ArraySlice(repeating: value, count: count)");
		closeBlock();
		writeNewLine();
		writeLine("public func makeIterator() -> IndexingIterator<Array<T>>");
		openBlock();
		writeLine("return array.makeIterator()");
		closeBlock();
		closeBlock();
	}
	if (this->stringCharAt) {
		writeNewLine();
		writeLine("fileprivate func fuStringCharAt(_ s: String, _ offset: Int) -> Int");
		openBlock();
		writeLine("return Int(s.unicodeScalars[s.index(s.startIndex, offsetBy: offset)].value)");
		closeBlock();
	}
	if (this->stringIndexOf) {
		writeNewLine();
		writeLine("fileprivate func fuStringIndexOf<S1 : StringProtocol, S2 : StringProtocol>(_ haystack: S1, _ needle: S2, _ options: String.CompareOptions = .literal) -> Int");
		openBlock();
		writeLine("guard let index = haystack.range(of: needle, options: options) else { return -1 }");
		writeLine("return haystack.distance(from: haystack.startIndex, to: index.lowerBound)");
		closeBlock();
	}
	if (this->stringSubstring) {
		writeNewLine();
		writeLine("fileprivate func fuStringSubstring(_ s: String, _ offset: Int) -> Substring");
		openBlock();
		writeLine("return s[s.index(s.startIndex, offsetBy: offset)...]");
		closeBlock();
	}
}

void GenSwift::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	if (std::ssize(*resources) == 0)
		return;
	this->arrayRef = true;
	writeNewLine();
	writeLine("fileprivate final class FuResource");
	openBlock();
	for (const auto &[name, content] : *resources) {
		write("static let ");
		writeResourceName(name);
		writeLine(" = ArrayRef<UInt8>([");
		writeChar('\t');
		writeBytes(&content);
		writeLine(" ])");
	}
	closeBlock();
}

void GenSwift::writeMain(const FuMethod * main)
{
	writeNewLine();
	if (main->type->id == FuId::intType)
		write("exit(Int32(");
	write(main->parent->name);
	write(".main(");
	if (main->parameters.count() == 1)
		write("Array(CommandLine.arguments[1...])");
	if (main->type->id == FuId::intType)
		write("))");
	writeCharLine(')');
}

void GenSwift::writeProgram(const FuProgram * program)
{
	this->system = program->system;
	this->throwException = false;
	this->arrayRef = false;
	this->stringCharAt = false;
	this->stringIndexOf = false;
	this->stringSubstring = false;
	openStringWriter();
	writeTypes(program);
	createOutputFile();
	writeTopLevelNatives(program);
	if (program->main != nullptr && program->main->type->id == FuId::intType)
		include("Foundation");
	writeIncludes("import ", "");
	closeStringWriter();
	writeLibrary();
	writeResources(&program->resources);
	if (program->main != nullptr)
		writeMain(program->main);
	closeFile();
}

std::string_view GenPy::getTargetName() const
{
	return "Python";
}

void GenPy::writeBanner()
{
	writeLine("# Generated automatically with \"fut\". Do not edit.");
}

void GenPy::startDocLine()
{
}

void GenPy::writeDocCode(std::string_view s)
{
	if (s == "true")
		write("True");
	else if (s == "false")
		write("False");
	else if (s == "null")
		write("None");
	else
		write(s);
}

std::string_view GenPy::getDocBullet() const
{
	return " * ";
}

void GenPy::startDoc(const FuCodeDoc * doc)
{
	write("\"\"\"");
	writeDocPara(&doc->summary, false);
	if (std::ssize(doc->details) > 0) {
		writeNewLine();
		for (const std::shared_ptr<FuDocBlock> &block : doc->details) {
			writeNewLine();
			writeDocBlock(block.get(), false);
		}
	}
}

void GenPy::writeDoc(const FuCodeDoc * doc)
{
	if (doc != nullptr) {
		startDoc(doc);
		writeLine("\"\"\"");
	}
}

void GenPy::writeParameterDoc(const FuVar * param, bool first)
{
	if (first) {
		writeNewLine();
		writeNewLine();
	}
	write(":param ");
	writeName(param);
	write(": ");
	writeDocPara(&param->documentation->summary, false);
	writeNewLine();
}

void GenPy::writeThrowsDoc(const FuThrowsDeclaration * decl)
{
	write(":raises ");
	writeExceptionClass(decl->symbol);
	write(": ");
	writeDocPara(&decl->documentation->summary, false);
	writeNewLine();
}

void GenPy::writePyDoc(const FuMethod * method)
{
	if (method->documentation == nullptr)
		return;
	startDoc(method->documentation.get());
	writeParametersAndThrowsDoc(method);
	writeLine("\"\"\"");
}

void GenPy::visitLiteralNull()
{
	write("None");
}

void GenPy::visitLiteralFalse()
{
	write("False");
}

void GenPy::visitLiteralTrue()
{
	write("True");
}

void GenPy::writeNameNotKeyword(std::string_view name)
{
	if (name == "this")
		write("self");
	else if (name == "And" || name == "Array" || name == "As" || name == "Assert" || name == "Async" || name == "Await" || name == "Bool" || name == "Break" || name == "Class" || name == "Continue" || name == "Def" || name == "Del" || name == "Dict" || name == "Elif" || name == "Else" || name == "Enum" || name == "Except" || name == "Finally" || name == "For" || name == "From" || name == "Global" || name == "If" || name == "Import" || name == "In" || name == "Is" || name == "Lambda" || name == "Len" || name == "List" || name == "Math" || name == "Nonlocal" || name == "Not" || name == "Or" || name == "Pass" || name == "Pyfma" || name == "Raise" || name == "Re" || name == "Return" || name == "Str" || name == "Sys" || name == "Try" || name == "While" || name == "With" || name == "Yield" || name == "and" || name == "array" || name == "as" || name == "async" || name == "await" || name == "def" || name == "del" || name == "dict" || name == "elif" || name == "enum" || name == "except" || name == "finally" || name == "from" || name == "global" || name == "import" || name == "is" || name == "json" || name == "lambda" || name == "len" || name == "list" || name == "math" || name == "nonlocal" || name == "not" || name == "or" || name == "pass" || name == "pyfma" || name == "raise" || name == "re" || name == "str" || name == "sys" || name == "try" || name == "with" || name == "yield") {
		writeCamelCase(name);
		writeChar('_');
	}
	else
		writeLowercaseWithUnderscores(name);
}

void GenPy::writeName(const FuSymbol * symbol)
{
	if (const FuContainerType *container = dynamic_cast<const FuContainerType *>(symbol)) {
		if (!container->isPublic)
			writeChar('_');
		write(symbol->name);
	}
	else if (const FuConst *konst = dynamic_cast<const FuConst *>(symbol)) {
		if (konst->visibility != FuVisibility::public_)
			writeChar('_');
		writeUppercaseConstName(konst);
	}
	else if (dynamic_cast<const FuVar *>(symbol))
		writeNameNotKeyword(symbol->name);
	else if (const FuMember *member = dynamic_cast<const FuMember *>(symbol)) {
		if (member->id == FuId::classToString)
			write("__str__");
		else if (member->visibility == FuVisibility::public_)
			writeNameNotKeyword(symbol->name);
		else {
			writeChar('_');
			writeLowercaseWithUnderscores(symbol->name);
		}
	}
	else
		std::abort();
}

void GenPy::writePyClassAnnotation(const FuContainerType * type)
{
	if (this->writtenTypes.contains(type))
		writeName(type);
	else {
		writeChar('"');
		writeName(type);
		writeChar('"');
	}
}

void GenPy::writeCollectionTypeAnnotation(std::string_view name, const FuClassType * klass)
{
	write(name);
	writeChar('[');
	writeTypeAnnotation(klass->getElementType().get(), klass->class_->id == FuId::arrayStorageClass);
	if (klass->class_->typeParameterCount == 2) {
		write(", ");
		writeTypeAnnotation(klass->getValueType().get());
	}
	writeChar(']');
}

void GenPy::writeTypeAnnotation(const FuType * type, bool nullable)
{
	if (dynamic_cast<const FuIntegerType *>(type))
		write("int");
	else if (dynamic_cast<const FuFloatingType *>(type))
		write("float");
	else if (const FuEnum *enu = dynamic_cast<const FuEnum *>(type)) {
		if (enu->id == FuId::boolType)
			write("bool");
		else
			writePyClassAnnotation(enu);
	}
	else if (const FuClassType *klass = dynamic_cast<const FuClassType *>(type)) {
		nullable = nullable ? !dynamic_cast<const FuStorageType *>(klass) : klass->nullable;
		switch (klass->class_->id) {
		case FuId::none:
			if (nullable && !this->writtenTypes.contains(klass->class_)) {
				writeChar('"');
				writeName(klass->class_);
				write(" | None\"");
				return;
			}
			writePyClassAnnotation(klass->class_);
			break;
		case FuId::stringClass:
			write("str");
			nullable = klass->nullable;
			break;
		case FuId::arrayPtrClass:
		case FuId::arrayStorageClass:
		case FuId::listClass:
		case FuId::stackClass:
			{
				const FuNumericType * number;
				if (!(number = dynamic_cast<const FuNumericType *>(klass->getElementType().get())))
					writeCollectionTypeAnnotation("list", klass);
				else if (number->id == FuId::byteRange) {
					write("bytearray");
					if (klass->class_->id == FuId::arrayPtrClass && !dynamic_cast<const FuReadWriteClassType *>(klass))
						write(" | bytes");
				}
				else {
					include("array");
					write("array.array");
				}
				break;
			}
		case FuId::queueClass:
			include("collections");
			writeCollectionTypeAnnotation("collections.deque", klass);
			break;
		case FuId::priorityQueueClass:
			writeCollectionTypeAnnotation("list", klass);
			break;
		case FuId::hashSetClass:
		case FuId::sortedSetClass:
			writeCollectionTypeAnnotation("set", klass);
			break;
		case FuId::dictionaryClass:
		case FuId::sortedDictionaryClass:
			writeCollectionTypeAnnotation("dict", klass);
			break;
		case FuId::orderedDictionaryClass:
			include("collections");
			writeCollectionTypeAnnotation("collections.OrderedDict", klass);
			break;
		case FuId::textWriterClass:
			include("io");
			write("io.TextIOBase");
			break;
		case FuId::stringWriterClass:
			include("io");
			write("io.StringIO");
			break;
		case FuId::regexClass:
			include("re");
			write("re.Pattern");
			break;
		case FuId::matchClass:
			include("re");
			write("re.Match");
			break;
		case FuId::jsonElementClass:
			write("dict | list | str | float | bool | None");
			break;
		case FuId::lockClass:
			include("threading");
			write("threading.RLock");
			break;
		default:
			std::abort();
		}
		if (nullable)
			write(" | None");
	}
	else
		std::abort();
}

void GenPy::writeTypeAndName(const FuNamedValue * value)
{
	writeName(value);
	write(": ");
	writeTypeAnnotation(value->type.get());
}

void GenPy::writeLocalName(const FuSymbol * symbol, FuPriority parent)
{
	const FuForeach * forEach;
	if ((forEach = dynamic_cast<const FuForeach *>(symbol->parent)) && dynamic_cast<const FuStringType *>(forEach->collection->type.get())) {
		write("ord(");
		writeNameNotKeyword(symbol->name);
		writeChar(')');
	}
	else
		GenPySwift::writeLocalName(symbol, parent);
}

int GenPy::getArrayCode(const FuType * type)
{
	switch (type->id) {
	case FuId::sByteRange:
		return 'b';
	case FuId::byteRange:
		return 'B';
	case FuId::shortRange:
		return 'h';
	case FuId::uShortRange:
		return 'H';
	case FuId::intType:
		return 'i';
	case FuId::nIntType:
	case FuId::longType:
		return 'q';
	case FuId::floatType:
		return 'f';
	case FuId::doubleType:
		return 'd';
	default:
		std::abort();
	}
}

void GenPy::visitAggregateInitializer(const FuAggregateInitializer * expr)
{
	const FuArrayStorageType * array = static_cast<const FuArrayStorageType *>(expr->type.get());
	if (const FuNumericType *number = dynamic_cast<const FuNumericType *>(array->getElementType().get())) {
		int c = getArrayCode(number);
		if (c == 'B')
			write("bytes(");
		else {
			include("array");
			write("array.array(\"");
			writeChar(c);
			write("\", ");
		}
		GenPySwift::visitAggregateInitializer(expr);
		writeChar(')');
	}
	else
		GenPySwift::visitAggregateInitializer(expr);
}

void GenPy::visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent)
{
	write("f\"");
	for (const FuInterpolatedPart &part : expr->parts) {
		writeDoubling(part.prefix, '{');
		writeChar('{');
		part.argument->accept(this, FuPriority::argument);
		writePyFormat(&part);
	}
	writeDoubling(expr->suffix, '{');
	writeChar('"');
}

void GenPy::visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent)
{
	if (expr->op == FuToken::exclamationMark) {
		if (parent > FuPriority::condAnd)
			writeChar('(');
		write("not ");
		expr->inner->accept(this, FuPriority::or_);
		if (parent > FuPriority::condAnd)
			writeChar(')');
	}
	else
		GenPySwift::visitPrefixExpr(expr, parent);
}

std::string_view GenPy::getReferenceEqOp(bool not_) const
{
	return not_ ? " is not " : " is ";
}

void GenPy::writeCharAt(const FuBinaryExpr * expr)
{
	write("ord(");
	writeIndexingExpr(expr, FuPriority::argument);
	writeChar(')');
}

void GenPy::writeStringLength(const FuExpr * expr)
{
	writeCall("len", expr);
}

void GenPy::writeArrayLength(const FuExpr * expr, FuPriority parent)
{
	writeCall("len", expr);
}

void GenPy::visitSymbolReference(const FuSymbolReference * expr, FuPriority parent)
{
	switch (expr->symbol->id) {
	case FuId::consoleError:
		include("sys");
		write("sys.stderr");
		break;
	case FuId::listCount:
	case FuId::queueCount:
	case FuId::stackCount:
	case FuId::priorityQueueCount:
	case FuId::hashSetCount:
	case FuId::sortedSetCount:
	case FuId::dictionaryCount:
	case FuId::sortedDictionaryCount:
	case FuId::orderedDictionaryCount:
		writeStringLength(expr->left.get());
		break;
	case FuId::mathNaN:
		include("math");
		write("math.nan");
		break;
	case FuId::mathNegativeInfinity:
		include("math");
		write("-math.inf");
		break;
	case FuId::mathPositiveInfinity:
		include("math");
		write("math.inf");
		break;
	default:
		if (!writeJavaMatchProperty(expr, parent))
			GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

void GenPy::visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent)
{
	switch (expr->op) {
	case FuToken::slash:
		if (dynamic_cast<const FuIntegerType *>(expr->type.get())) {
			bool floorDiv;
			const FuRangeType * leftRange;
			const FuRangeType * rightRange;
			if ((leftRange = dynamic_cast<const FuRangeType *>(expr->left.get())) && leftRange->min >= 0 && (rightRange = dynamic_cast<const FuRangeType *>(expr->right.get())) && rightRange->min >= 0) {
				if (parent > FuPriority::or_)
					writeChar('(');
				floorDiv = true;
			}
			else {
				write("int(");
				floorDiv = false;
			}
			expr->left->accept(this, FuPriority::mul);
			write(floorDiv ? " // " : " / ");
			expr->right->accept(this, FuPriority::primary);
			if (!floorDiv || parent > FuPriority::or_)
				writeChar(')');
		}
		else
			GenBase::visitBinaryExpr(expr, parent);
		break;
	case FuToken::condAnd:
		writeBinaryExpr(expr, parent > FuPriority::condAnd || parent == FuPriority::condOr, FuPriority::condAnd, " and ", FuPriority::condAnd);
		break;
	case FuToken::condOr:
		writeBinaryExpr2(expr, parent, FuPriority::condOr, " or ");
		break;
	case FuToken::assign:
		if (this->atLineStart) {
			const FuBinaryExpr * rightBinary;
			for (const FuExpr * right = expr->right.get(); (rightBinary = dynamic_cast<const FuBinaryExpr *>(right)) && rightBinary->isAssign(); right = rightBinary->right.get()) {
				if (rightBinary->op != FuToken::assign) {
					visitBinaryExpr(rightBinary, FuPriority::statement);
					writeNewLine();
					break;
				}
			}
		}
		expr->left->accept(this, FuPriority::assign);
		write(" = ");
		{
			const FuBinaryExpr * rightBinary;
			((rightBinary = dynamic_cast<const FuBinaryExpr *>(expr->right.get())) && rightBinary->isAssign() && rightBinary->op != FuToken::assign ? rightBinary->left : expr->right)->accept(this, FuPriority::assign);
		}
		break;
	case FuToken::addAssign:
	case FuToken::subAssign:
	case FuToken::mulAssign:
	case FuToken::divAssign:
	case FuToken::modAssign:
	case FuToken::shiftLeftAssign:
	case FuToken::shiftRightAssign:
	case FuToken::andAssign:
	case FuToken::orAssign:
	case FuToken::xorAssign:
		{
			const FuExpr * right = expr->right.get();
			const FuBinaryExpr * rightBinary;
			if ((rightBinary = dynamic_cast<const FuBinaryExpr *>(right)) && rightBinary->isAssign()) {
				visitBinaryExpr(rightBinary, FuPriority::statement);
				writeNewLine();
				right = rightBinary->left.get();
			}
			expr->left->accept(this, FuPriority::assign);
			writeChar(' ');
			if (expr->op == FuToken::divAssign && dynamic_cast<const FuIntegerType *>(expr->type.get()))
				writeChar('/');
			write(expr->getOpString());
			writeChar(' ');
			right->accept(this, FuPriority::argument);
		}
		break;
	case FuToken::is:
		if (const FuSymbolReference *symbol = dynamic_cast<const FuSymbolReference *>(expr->right.get())) {
			write("isinstance(");
			expr->left->accept(this, FuPriority::argument);
			write(", ");
			writeName(symbol->symbol);
			writeChar(')');
		}
		else
			notSupported(expr, "'is' with a variable");
		break;
	default:
		GenBase::visitBinaryExpr(expr, parent);
		break;
	}
}

void GenPy::writeCoercedSelect(const FuType * type, const FuSelectExpr * expr, FuPriority parent)
{
	if (parent > FuPriority::select)
		writeChar('(');
	writeCoerced(type, expr->onTrue.get(), FuPriority::select);
	write(" if ");
	expr->cond->accept(this, FuPriority::selectCond);
	write(" else ");
	writeCoerced(type, expr->onFalse.get(), FuPriority::select);
	if (parent > FuPriority::select)
		writeChar(')');
}

void GenPy::writeDefaultValue(const FuType * type)
{
	if (dynamic_cast<const FuNumericType *>(type))
		writeChar('0');
	else if (const FuEnum *enu = dynamic_cast<const FuEnum *>(type)) {
		if (type->id == FuId::boolType)
			visitLiteralFalse();
		else {
			writeName(enu);
			writeChar('.');
			writeUppercaseWithUnderscores(enu->getFirstValue()->name);
		}
	}
	else if ((type->id == FuId::stringPtrType && !type->nullable) || type->id == FuId::stringStorageType)
		write("\"\"");
	else
		write("None");
}

void GenPy::writePyNewArray(const FuType * elementType, const FuExpr * value, const FuExpr * lengthExpr)
{
	if (dynamic_cast<const FuStorageType *>(elementType)) {
		write("[ ");
		writeNewStorage(elementType);
		write(" for _ in range(");
		lengthExpr->accept(this, FuPriority::argument);
		write(") ]");
	}
	else if (dynamic_cast<const FuNumericType *>(elementType)) {
		int c = getArrayCode(elementType);
		if (c == 'B' && (value == nullptr || value->isLiteralZero()))
			writeCall("bytearray", lengthExpr);
		else {
			include("array");
			write("array.array(\"");
			writeChar(c);
			write("\", [ ");
			if (value == nullptr)
				writeChar('0');
			else
				value->accept(this, FuPriority::argument);
			write(" ]) * ");
			lengthExpr->accept(this, FuPriority::mul);
		}
	}
	else {
		write("[ ");
		if (value == nullptr)
			writeDefaultValue(elementType);
		else
			value->accept(this, FuPriority::argument);
		write(" ] * ");
		lengthExpr->accept(this, FuPriority::mul);
	}
}

void GenPy::writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent)
{
	writePyNewArray(elementType, nullptr, lengthExpr);
}

void GenPy::writeArrayStorageInit(const FuArrayStorageType * array, const FuExpr * value)
{
	write(" = ");
	writePyNewArray(array->getElementType().get(), nullptr, array->lengthExpr.get());
}

void GenPy::writeNew(const FuReadWriteClassType * klass, FuPriority parent)
{
	switch (klass->class_->id) {
	case FuId::listClass:
	case FuId::stackClass:
		if (const FuNumericType *number = dynamic_cast<const FuNumericType *>(klass->getElementType().get())) {
			int c = getArrayCode(number);
			if (c == 'B')
				write("bytearray()");
			else {
				include("array");
				write("array.array(\"");
				writeChar(c);
				write("\")");
			}
		}
		else
			write("[]");
		break;
	case FuId::queueClass:
		include("collections");
		write("collections.deque()");
		break;
	case FuId::priorityQueueClass:
		write("[]");
		break;
	case FuId::hashSetClass:
	case FuId::sortedSetClass:
		write("set()");
		break;
	case FuId::dictionaryClass:
	case FuId::sortedDictionaryClass:
		write("{}");
		break;
	case FuId::orderedDictionaryClass:
		include("collections");
		write("collections.OrderedDict()");
		break;
	case FuId::stringWriterClass:
		include("io");
		write("io.StringIO()");
		break;
	case FuId::lockClass:
		include("threading");
		write("threading.RLock()");
		break;
	default:
		writeName(klass->class_);
		write("()");
		break;
	}
}

void GenPy::writeContains(const FuExpr * haystack, const FuExpr * needle)
{
	needle->accept(this, FuPriority::rel);
	write(" in ");
	haystack->accept(this, FuPriority::rel);
}

void GenPy::writeSlice(const FuExpr * startIndex, const FuExpr * length)
{
	writeChar('[');
	startIndex->accept(this, FuPriority::argument);
	writeChar(':');
	if (length != nullptr)
		writeAdd(startIndex, length);
	writeChar(']');
}

void GenPy::writeAssignSorted(const FuExpr * obj, std::string_view byteArray)
{
	write(" = ");
	int c = getArrayCode(obj->type->asClassType()->getElementType().get());
	if (c == 'B') {
		write(byteArray);
		writeChar('(');
	}
	else {
		include("array");
		write("array.array(\"");
		writeChar(c);
		write("\", ");
	}
	write("sorted(");
}

void GenPy::writeAllAny(std::string_view function, const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args)
{
	write(function);
	writeChar('(');
	const FuLambdaExpr * lambda = static_cast<const FuLambdaExpr *>((*args)[0].get());
	lambda->body->accept(this, FuPriority::argument);
	write(" for ");
	writeName(lambda->first);
	write(" in ");
	obj->accept(this, FuPriority::argument);
	writeChar(')');
}

void GenPy::writePyRegexOptions(const std::vector<std::shared_ptr<FuExpr>> * args)
{
	include("re");
	writeRegexOptions(args, ", ", " | ", "", "re.I", "re.M", "re.S");
}

void GenPy::writeRegexSearch(const std::vector<std::shared_ptr<FuExpr>> * args)
{
	write("re.search(");
	(*args)[1]->accept(this, FuPriority::argument);
	write(", ");
	(*args)[0]->accept(this, FuPriority::argument);
	writePyRegexOptions(args);
	writeChar(')');
}

void GenPy::writeJsonElementIs(const FuExpr * obj, std::string_view name)
{
	write("isinstance(");
	obj->accept(this, FuPriority::argument);
	write(", ");
	write(name);
	writeChar(')');
}

void GenPy::writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent)
{
	switch (method->id) {
	case FuId::enumFromInt:
		writeName(method->type.get());
		writeInParentheses(args);
		break;
	case FuId::enumHasFlag:
	case FuId::stringContains:
	case FuId::arrayContains:
	case FuId::listContains:
	case FuId::hashSetContains:
	case FuId::sortedSetContains:
	case FuId::dictionaryContainsKey:
	case FuId::sortedDictionaryContainsKey:
	case FuId::orderedDictionaryContainsKey:
		writeContains(obj, (*args)[0].get());
		break;
	case FuId::stringEndsWith:
		writeMethodCall(obj, "endswith", (*args)[0].get());
		break;
	case FuId::stringIndexOf:
		writeMethodCall(obj, "find", (*args)[0].get());
		break;
	case FuId::stringLastIndexOf:
		writeMethodCall(obj, "rfind", (*args)[0].get());
		break;
	case FuId::stringStartsWith:
		writeMethodCall(obj, "startswith", (*args)[0].get());
		break;
	case FuId::stringSubstring:
		obj->accept(this, FuPriority::primary);
		writeSlice((*args)[0].get(), std::ssize(*args) == 2 ? (*args)[1].get() : nullptr);
		break;
	case FuId::stringToLower:
		writePostfix(obj, ".lower()");
		break;
	case FuId::stringToUpper:
		writePostfix(obj, ".upper()");
		break;
	case FuId::arrayBinarySearchAll:
		include("bisect");
		writeCall("bisect.bisect_left", obj, (*args)[0].get());
		break;
	case FuId::arrayBinarySearchPart:
		include("bisect");
		write("bisect.bisect_left(");
		obj->accept(this, FuPriority::argument);
		write(", ");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", ");
		(*args)[1]->accept(this, FuPriority::argument);
		write(", ");
		(*args)[2]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::arrayCopyTo:
	case FuId::listCopyTo:
		(*args)[1]->accept(this, FuPriority::primary);
		writeSlice((*args)[2].get(), (*args)[3].get());
		write(" = ");
		obj->accept(this, FuPriority::primary);
		writeSlice((*args)[0].get(), (*args)[3].get());
		break;
	case FuId::arrayFillAll:
	case FuId::arrayFillPart:
		obj->accept(this, FuPriority::primary);
		if (std::ssize(*args) == 1) {
			write("[:] = ");
			const FuArrayStorageType * array = static_cast<const FuArrayStorageType *>(obj->type.get());
			writePyNewArray(array->getElementType().get(), (*args)[0].get(), array->lengthExpr.get());
		}
		else {
			writeSlice((*args)[1].get(), (*args)[2].get());
			write(" = ");
			writePyNewArray(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), (*args)[2].get());
		}
		break;
	case FuId::arraySortAll:
	case FuId::listSortAll:
		obj->accept(this, FuPriority::assign);
		writeAssignSorted(obj, "bytearray");
		obj->accept(this, FuPriority::argument);
		write("))");
		break;
	case FuId::arraySortPart:
	case FuId::listSortPart:
		obj->accept(this, FuPriority::primary);
		writeSlice((*args)[0].get(), (*args)[1].get());
		writeAssignSorted(obj, "bytes");
		obj->accept(this, FuPriority::primary);
		writeSlice((*args)[0].get(), (*args)[1].get());
		write("))");
		break;
	case FuId::listAdd:
		writeListAdd(obj, "append", args);
		break;
	case FuId::listAddRange:
		obj->accept(this, FuPriority::assign);
		write(" += ");
		(*args)[0]->accept(this, FuPriority::argument);
		break;
	case FuId::listAll:
		writeAllAny("all", obj, args);
		break;
	case FuId::listAny:
		writeAllAny("any", obj, args);
		break;
	case FuId::listClear:
	case FuId::stackClear:
		{
			const FuNumericType * number;
			if ((number = dynamic_cast<const FuNumericType *>(obj->type->asClassType()->getElementType().get())) && getArrayCode(number) != 'B') {
				write("del ");
				writePostfix(obj, "[:]");
			}
			else
				writePostfix(obj, ".clear()");
			break;
		}
	case FuId::listIndexOf:
		if (parent > FuPriority::select)
			writeChar('(');
		writeMethodCall(obj, "index", (*args)[0].get());
		write(" if ");
		writeContains(obj, (*args)[0].get());
		write(" else -1");
		if (parent > FuPriority::select)
			writeChar(')');
		break;
	case FuId::listInsert:
		writeListInsert(obj, "insert", args);
		break;
	case FuId::listLast:
	case FuId::stackPeek:
		writePostfix(obj, "[-1]");
		break;
	case FuId::listRemoveAt:
	case FuId::dictionaryRemove:
	case FuId::sortedDictionaryRemove:
	case FuId::orderedDictionaryRemove:
		write("del ");
		writeIndexing(obj, (*args)[0].get());
		break;
	case FuId::listRemoveRange:
		write("del ");
		obj->accept(this, FuPriority::primary);
		writeSlice((*args)[0].get(), (*args)[1].get());
		break;
	case FuId::queueDequeue:
		writePostfix(obj, ".popleft()");
		break;
	case FuId::queueEnqueue:
	case FuId::stackPush:
		writeListAppend(obj, args);
		break;
	case FuId::queuePeek:
	case FuId::priorityQueuePeek:
		writePostfix(obj, "[0]");
		break;
	case FuId::priorityQueueClear:
		writePostfix(obj, ".clear()");
		break;
	case FuId::priorityQueueEnqueue:
		include("heapq");
		writeCall("heapq.heappush", obj, (*args)[0].get());
		break;
	case FuId::priorityQueueDequeue:
		include("heapq");
		writeCall("heapq.heappop", obj);
		break;
	case FuId::dictionaryAdd:
		writeDictionaryAdd(obj, args);
		break;
	case FuId::textWriterWrite:
		write("print(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", end=\"\", file=");
		obj->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::textWriterWriteChar:
	case FuId::textWriterWriteCodePoint:
		writeMethodCall(obj, "write(chr", (*args)[0].get());
		writeChar(')');
		break;
	case FuId::textWriterWriteLine:
		write("print(");
		if (std::ssize(*args) == 1) {
			(*args)[0]->accept(this, FuPriority::argument);
			write(", ");
		}
		write("file=");
		obj->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::consoleWrite:
		write("print(");
		(*args)[0]->accept(this, FuPriority::argument);
		write(", end=\"\")");
		break;
	case FuId::consoleWriteLine:
		write("print(");
		if (std::ssize(*args) == 1)
			(*args)[0]->accept(this, FuPriority::argument);
		writeChar(')');
		break;
	case FuId::stringWriterClear:
		writePostfix(obj, ".seek(0)");
		writeNewLine();
		writePostfix(obj, ".truncate(0)");
		break;
	case FuId::stringWriterToString:
		writePostfix(obj, ".getvalue()");
		break;
	case FuId::convertToBase64String:
		include("base64");
		write("base64.b64encode(");
		(*args)[0]->accept(this, FuPriority::primary);
		writeSlice((*args)[1].get(), (*args)[2].get());
		write(").decode(\"utf8\")");
		break;
	case FuId::uTF8GetByteCount:
		write("len(");
		writePostfix((*args)[0].get(), ".encode(\"utf8\"))");
		break;
	case FuId::uTF8GetBytes:
		write("fubytes = ");
		(*args)[0]->accept(this, FuPriority::primary);
		writeLine(".encode(\"utf8\")");
		(*args)[1]->accept(this, FuPriority::primary);
		writeChar('[');
		(*args)[2]->accept(this, FuPriority::argument);
		writeChar(':');
		startAdd((*args)[2].get());
		writeLine("len(fubytes)] = fubytes");
		break;
	case FuId::uTF8GetString:
		(*args)[0]->accept(this, FuPriority::primary);
		writeSlice((*args)[1].get(), (*args)[2].get());
		write(".decode(\"utf8\")");
		break;
	case FuId::environmentGetEnvironmentVariable:
		include("os");
		writeCall("os.getenv", (*args)[0].get());
		break;
	case FuId::regexCompile:
		write("re.compile(");
		(*args)[0]->accept(this, FuPriority::argument);
		writePyRegexOptions(args);
		writeChar(')');
		break;
	case FuId::regexEscape:
		include("re");
		writeCall("re.escape", (*args)[0].get());
		break;
	case FuId::regexIsMatchStr:
		if (parent > FuPriority::equality)
			writeChar('(');
		writeRegexSearch(args);
		write(" is not None");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::regexIsMatchRegex:
		if (parent > FuPriority::equality)
			writeChar('(');
		writeMethodCall(obj, "search", (*args)[0].get());
		write(" is not None");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::matchFindStr:
	case FuId::matchFindRegex:
		if (parent > FuPriority::equality)
			writeChar('(');
		obj->accept(this, FuPriority::equality);
		write(" is not None");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::matchGetCapture:
		writeMethodCall(obj, "group", (*args)[0].get());
		break;
	case FuId::jsonElementParse:
		include("json");
		writeCall("json.loads", (*args)[0].get());
		break;
	case FuId::jsonElementIsObject:
		writeJsonElementIs(obj, "dict");
		break;
	case FuId::jsonElementIsArray:
		writeJsonElementIs(obj, "list");
		break;
	case FuId::jsonElementIsString:
		writeJsonElementIs(obj, "str");
		break;
	case FuId::jsonElementIsNumber:
		writeJsonElementIs(obj, "float");
		break;
	case FuId::jsonElementIsBoolean:
		writeJsonElementIs(obj, "bool");
		break;
	case FuId::jsonElementIsNull:
		if (parent > FuPriority::equality)
			writeChar('(');
		obj->accept(this, FuPriority::equality);
		write(" is None");
		if (parent > FuPriority::equality)
			writeChar(')');
		break;
	case FuId::jsonElementGetObject:
	case FuId::jsonElementGetArray:
	case FuId::jsonElementGetString:
	case FuId::jsonElementGetDouble:
	case FuId::jsonElementGetBoolean:
		obj->accept(this, parent);
		break;
	case FuId::mathMethod:
	case FuId::mathIsFinite:
	case FuId::mathIsNaN:
	case FuId::mathLog2:
		include("math");
		write("math.");
		writeLowercase(method->name);
		writeInParentheses(args);
		break;
	case FuId::mathAbs:
		writeCall("abs", (*args)[0].get());
		break;
	case FuId::mathCeiling:
		include("math");
		writeCall("math.ceil", (*args)[0].get());
		break;
	case FuId::mathClamp:
		write("min(max(");
		writeClampAsMinMax(args);
		break;
	case FuId::mathFusedMultiplyAdd:
		include("pyfma");
		writeCall("pyfma.fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case FuId::mathIsInfinity:
		include("math");
		writeCall("math.isinf", (*args)[0].get());
		break;
	case FuId::mathMax:
		writeCall("max", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::mathMin:
		writeCall("min", (*args)[0].get(), (*args)[1].get());
		break;
	case FuId::mathRound:
		writeCall("round", (*args)[0].get());
		break;
	case FuId::mathTruncate:
		include("math");
		writeCall("math.trunc", (*args)[0].get());
		break;
	default:
		if (obj == nullptr)
			writeLocalName(method, FuPriority::primary);
		else if (isReferenceTo(obj, FuId::basePtr)) {
			writeName(method->parent);
			writeChar('.');
			writeName(method);
			write("(self");
			if (std::ssize(*args) > 0) {
				write(", ");
				writeCoercedArgs(method, args);
			}
			writeChar(')');
			break;
		}
		else {
			obj->accept(this, FuPriority::primary);
			writeChar('.');
			writeName(method);
		}
		writeCoercedArgsInParentheses(method, args);
		break;
	}
}

void GenPy::writeResource(std::string_view name, int length)
{
	write("_FuResource.");
	writeResourceName(name);
}

bool GenPy::visitPreCall(const FuCallExpr * call)
{
	switch (call->method->symbol->id) {
	case FuId::matchFindStr:
		call->method->left->accept(this, FuPriority::assign);
		write(" = ");
		writeRegexSearch(&call->arguments);
		writeNewLine();
		return true;
	case FuId::matchFindRegex:
		call->method->left->accept(this, FuPriority::assign);
		write(" = ");
		writeMethodCall(call->arguments[1].get(), "search", call->arguments[0].get());
		writeNewLine();
		return true;
	default:
		return false;
	}
}

void GenPy::startTemporaryVar(const FuType * type)
{
}

bool GenPy::hasInitCode(const FuNamedValue * def) const
{
	return (def->value != nullptr || def->type->isFinal()) && !def->isAssignableStorage();
}

void GenPy::visitExpr(const FuExpr * statement)
{
	const FuVar * def;
	if (!(def = dynamic_cast<const FuVar *>(statement)) || hasInitCode(def)) {
		writeTemporaries(statement);
		GenPySwift::visitExpr(statement);
	}
}

void GenPy::startLine()
{
	GenBase::startLine();
	this->childPass = false;
}

void GenPy::openChild()
{
	writeCharLine(':');
	this->indent++;
	this->childPass = true;
}

void GenPy::closeChild()
{
	if (this->childPass)
		writeLine("pass");
	this->indent--;
}

void GenPy::visitLambdaExpr(const FuLambdaExpr * expr)
{
	std::abort();
}

void GenPy::writeAssertCast(const FuBinaryExpr * expr)
{
	const FuVar * def = static_cast<const FuVar *>(expr->right.get());
	writeTypeAndName(def);
	write(" = ");
	expr->left->accept(this, FuPriority::argument);
	writeNewLine();
}

void GenPy::writeAssert(const FuAssert * statement)
{
	write("assert ");
	statement->cond->accept(this, FuPriority::argument);
	if (statement->message != nullptr) {
		write(", ");
		statement->message->accept(this, FuPriority::argument);
	}
	writeNewLine();
}

void GenPy::visitBreak(const FuBreak * statement)
{
	writeLine(dynamic_cast<const FuSwitch *>(statement->loopOrSwitch) ? "raise _CiBreak()" : "break");
}

std::string_view GenPy::getIfNot() const
{
	return "if not ";
}

void GenPy::writeInclusiveLimit(const FuExpr * limit, int increment, std::string_view incrementString)
{
	if (const FuLiteralLong *literal = dynamic_cast<const FuLiteralLong *>(limit))
		visitLiteralLong(literal->value + increment);
	else {
		limit->accept(this, FuPriority::add);
		write(incrementString);
	}
}

void GenPy::writeForRange(const FuVar * indVar, const FuBinaryExpr * cond, int64_t rangeStep)
{
	write("range(");
	if (rangeStep != 1 || !indVar->value->isLiteralZero()) {
		indVar->value->accept(this, FuPriority::argument);
		write(", ");
	}
	switch (cond->op) {
	case FuToken::less:
	case FuToken::greater:
		cond->right->accept(this, FuPriority::argument);
		break;
	case FuToken::lessOrEqual:
		writeInclusiveLimit(cond->right.get(), 1, " + 1");
		break;
	case FuToken::greaterOrEqual:
		writeInclusiveLimit(cond->right.get(), -1, " - 1");
		break;
	default:
		std::abort();
	}
	if (rangeStep != 1) {
		write(", ");
		visitLiteralLong(rangeStep);
	}
	writeChar(')');
}

void GenPy::visitForeach(const FuForeach * statement)
{
	write("for ");
	writeName(statement->getVar());
	const FuClassType * klass = static_cast<const FuClassType *>(statement->collection->type.get());
	if (klass->class_->typeParameterCount == 2) {
		write(", ");
		writeName(statement->getValueVar());
		write(" in ");
		if (klass->class_->id == FuId::sortedDictionaryClass) {
			write("sorted(");
			writePostfix(statement->collection.get(), ".items())");
		}
		else
			writePostfix(statement->collection.get(), ".items()");
	}
	else {
		write(" in ");
		if (klass->class_->id == FuId::sortedSetClass)
			writeCall("sorted", statement->collection.get());
		else
			statement->collection->accept(this, FuPriority::argument);
	}
	writeChild(statement->body.get());
}

void GenPy::writeElseIf()
{
	write("el");
}

void GenPy::visitLock(const FuLock * statement)
{
	visitXcrement(statement->lock.get(), false, true);
	write("with ");
	statement->lock->accept(this, FuPriority::argument);
	openChild();
	visitXcrement(statement->lock.get(), true, true);
	statement->body->acceptStatement(this);
	closeChild();
}

void GenPy::writeResultVar()
{
	write("result");
}

void GenPy::writePyCaseValue(const FuExpr * value)
{
	const FuSymbolReference * symbol;
	const FuClass * klass;
	const FuBinaryExpr * when1;
	if ((symbol = dynamic_cast<const FuSymbolReference *>(value)) && (klass = dynamic_cast<const FuClass *>(symbol->symbol))) {
		writeName(klass);
		write("()");
	}
	else if (const FuVar *def = dynamic_cast<const FuVar *>(value)) {
		writeName(def->type->asClassType()->class_);
		write("() as ");
		writeNameNotKeyword(def->name);
	}
	else if ((when1 = dynamic_cast<const FuBinaryExpr *>(value)) && when1->op == FuToken::when) {
		writePyCaseValue(when1->left.get());
		write(" if ");
		when1->right->accept(this, FuPriority::argument);
	}
	else
		value->accept(this, FuPriority::or_);
}

void GenPy::writePyCaseBody(const FuSwitch * statement, const std::vector<std::shared_ptr<FuStatement>> * body)
{
	openChild();
	visitXcrement(statement->value.get(), true, true);
	writeFirstStatements(body, FuSwitch::lengthWithoutTrailingBreak(body));
	closeChild();
}

void GenPy::visitSwitch(const FuSwitch * statement)
{
	bool earlyBreak = std::any_of(statement->cases.begin(), statement->cases.end(), [](const FuCase &kase) { return FuSwitch::hasEarlyBreak(&kase.body); }) || FuSwitch::hasEarlyBreak(&statement->defaultBody);
	if (earlyBreak) {
		this->switchBreak = true;
		write("try");
		openChild();
	}
	visitXcrement(statement->value.get(), false, true);
	write("match ");
	statement->value->accept(this, FuPriority::argument);
	openChild();
	for (const FuCase &kase : statement->cases) {
		std::string_view op = "case ";
		for (const std::shared_ptr<FuExpr> &caseValue : kase.values) {
			write(op);
			writePyCaseValue(caseValue.get());
			op = " | ";
		}
		writePyCaseBody(statement, &kase.body);
	}
	if (statement->hasDefault()) {
		write("case _");
		writePyCaseBody(statement, &statement->defaultBody);
	}
	closeChild();
	if (earlyBreak) {
		closeChild();
		write("except _CiBreak");
		openChild();
		closeChild();
	}
}

void GenPy::visitThrow(const FuThrow * statement)
{
	if (statement->message != nullptr)
		visitXcrement(statement->message.get(), false, true);
	write("raise ");
	writeThrowArgument(statement);
	writeNewLine();
}

void GenPy::writePyClass(const FuContainerType * type)
{
	writeNewLine();
	write("class ");
	writeName(type);
}

void GenPy::visitEnumValue(const FuConst * konst, const FuConst * previous)
{
	writeUppercaseWithUnderscores(konst->name);
	write(" = ");
	visitLiteralLong(konst->value->intValue());
	writeNewLine();
	writeDoc(konst->documentation.get());
}

void GenPy::writeEnum(const FuEnum * enu)
{
	writePyClass(enu);
	include("enum");
	write(dynamic_cast<const FuEnumFlags *>(enu) ? "(enum.Flag)" : "(enum.Enum)");
	openChild();
	writeDoc(enu->documentation.get());
	enu->acceptValues(this);
	closeChild();
	this->writtenTypes.insert(enu);
}

void GenPy::writeConst(const FuConst * konst)
{
	if (konst->visibility != FuVisibility::private_ || dynamic_cast<const FuArrayStorageType *>(konst->type.get())) {
		writeNewLine();
		writeName(konst);
		write(" = ");
		konst->value->accept(this, FuPriority::argument);
		writeNewLine();
		writeDoc(konst->documentation.get());
	}
}

void GenPy::writeField(const FuField * field)
{
	writeTypeAndName(field);
	writeNewLine();
}

void GenPy::writeMethod(const FuMethod * method)
{
	writeNewLine();
	switch (method->callType) {
	case FuCallType::static_:
		writeLine("@staticmethod");
		break;
	case FuCallType::abstract:
		include("abc");
		writeLine("@abc.abstractmethod");
		break;
	default:
		break;
	}
	write("def ");
	writeName(method);
	if (method->callType == FuCallType::static_)
		writeParameters(method, true);
	else {
		write("(self");
		writeRemainingParameters(method, false, true);
	}
	write(" -> ");
	if (method->type->id == FuId::voidType)
		write("None");
	else
		writeTypeAnnotation(method->type.get());
	this->currentMethod = method;
	openChild();
	writePyDoc(method);
	if (method->body != nullptr)
		method->body->acceptStatement(this);
	closeChild();
	this->currentMethod = nullptr;
}

bool GenPy::inheritsConstructor(const FuClass * klass) const
{
	while (const FuClass *baseClass = dynamic_cast<const FuClass *>(klass->parent)) {
		if (needsConstructor(baseClass))
			return true;
		klass = baseClass;
	}
	return false;
}

void GenPy::writeInitField(const FuField * field)
{
	if (hasInitCode(field)) {
		write("self.");
		writeName(field);
		writeVarInit(field);
		writeNewLine();
		writeInitCode(field);
	}
}

void GenPy::writeClass(const FuClass * klass, const FuProgram * program)
{
	if (!writeBaseClass(klass, program))
		return;
	writePyClass(klass);
	if (const FuClass *baseClass = dynamic_cast<const FuClass *>(klass->parent)) {
		writeChar('(');
		writeName(baseClass);
		writeChar(')');
	}
	else if (klass->callType == FuCallType::abstract) {
		include("abc");
		write("(abc.ABC)");
	}
	openChild();
	writeDoc(klass->documentation.get());
	if (needsConstructor(klass)) {
		writeNewLine();
		write("def __init__(self)");
		openChild();
		if (klass->constructor != nullptr)
			writeDoc(klass->constructor->documentation.get());
		if (inheritsConstructor(klass)) {
			writeName(klass->parent);
			writeLine(".__init__(self)");
		}
		writeConstructorBody(klass);
		closeChild();
	}
	writeMembers(klass, true);
	closeChild();
	this->writtenTypes.insert(klass);
}

void GenPy::writeResourceByte(int b)
{
	write(std::format("\\x{:02x}", b));
}

void GenPy::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	if (std::ssize(*resources) == 0)
		return;
	writeNewLine();
	write("class _FuResource");
	openChild();
	for (const auto &[name, content] : *resources) {
		writeResourceName(name);
		writeLine(" = (");
		this->indent++;
		write("b\"");
		int i = 0;
		for (int b : content) {
			if (i > 0 && (i & 15) == 0) {
				writeCharLine('"');
				write("b\"");
			}
			writeResourceByte(b);
			i++;
		}
		writeLine("\" )");
		this->indent--;
	}
	closeChild();
}

void GenPy::writeMain(const FuMethod * main)
{
	writeNewLine();
	writeLine("if __name__ == '__main__':");
	writeChar('\t');
	if (main->type->id == FuId::intType)
		write("sys.exit(");
	writeName(main->parent);
	write(".main(");
	if (main->parameters.count() == 1)
		write("sys.argv[1:]");
	if (main->type->id == FuId::intType)
		writeChar(')');
	writeCharLine(')');
}

void GenPy::writeProgram(const FuProgram * program)
{
	this->writtenTypes.clear();
	this->switchBreak = false;
	openStringWriter();
	writeTypes(program);
	createOutputFile();
	writeTopLevelNatives(program);
	if (program->main != nullptr && (program->main->type->id == FuId::intType || program->main->parameters.count() == 1))
		include("sys");
	writeIncludes("import ", "");
	if (this->switchBreak) {
		writeNewLine();
		writeLine("class _CiBreak(Exception): pass");
	}
	closeStringWriter();
	writeResources(&program->resources);
	if (program->main != nullptr)
		writeMain(program->main);
	closeFile();
}
