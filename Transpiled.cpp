// Generated automatically with "cito". Do not edit.
#include <algorithm>
#include <cassert>
#include <cmath>
#include <cstdlib>
#include <format>
#include "Transpiled.hpp"

static std::string CiString_replace(std::string_view s, std::string_view oldValue, std::string_view newValue)
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

void CiLexer::addPreSymbol(std::string_view symbol)
{
	this->preSymbols.emplace(symbol);
}

void CiLexer::open(std::string_view filename, uint8_t const * input, int inputLength)
{
	this->filename = filename;
	this->input = input;
	this->inputLength = inputLength;
	this->nextOffset = 0;
	this->line = 1;
	this->column = 1;
	fillNextChar();
	if (this->nextChar == 65279)
		fillNextChar();
	nextToken();
}

int CiLexer::readByte()
{
	if (this->nextOffset >= this->inputLength)
		return -1;
	return this->input[this->nextOffset++];
}

int CiLexer::readContinuationByte(int hi)
{
	int b = readByte();
	if (hi != 65533) {
		if (b >= 128 && b <= 191)
			return (hi << 6) + b - 128;
		reportError("Invalid UTF-8");
	}
	return 65533;
}

void CiLexer::fillNextChar()
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

int CiLexer::peekChar() const
{
	return this->nextChar;
}

bool CiLexer::isLetterOrDigit(int c)
{
	if (c >= 'a' && c <= 'z')
		return true;
	if (c >= 'A' && c <= 'Z')
		return true;
	if (c >= '0' && c <= '9')
		return true;
	return c == '_';
}

int CiLexer::readChar()
{
	int c = this->nextChar;
	switch (c) {
	case '\t':
	case ' ':
		this->column++;
		break;
	case '\n':
		this->line++;
		this->column = 1;
		this->atLineStart = true;
		break;
	default:
		this->column++;
		this->atLineStart = false;
		break;
	}
	fillNextChar();
	return c;
}

bool CiLexer::eatChar(int c)
{
	if (peekChar() == c) {
		readChar();
		return true;
	}
	return false;
}

void CiLexer::skipWhitespace()
{
	while (peekChar() == '\t' || peekChar() == ' ' || peekChar() == '\r')
		readChar();
}

CiToken CiLexer::readIntegerLiteral(int bits)
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
			return CiToken::literalLong;
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

CiToken CiLexer::readFloatLiteral(bool needDigit)
{
	bool underscoreE = false;
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
		case '.':
			readChar();
			needDigit = false;
			break;
		case 'E':
		case 'e':
			if (needDigit)
				underscoreE = true;
			readChar();
			c = peekChar();
			if (c == '+' || c == '-')
				readChar();
			needDigit = true;
			break;
		case '_':
			readChar();
			needDigit = true;
			break;
		default:
			if (underscoreE || needDigit || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
				reportError("Invalid floating-point number");
			return CiToken::literalDouble;
		}
	}
}

CiToken CiLexer::readNumberLiteral(int64_t i)
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
			return CiToken::literalLong;
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

int CiLexer::getEscapedChar(int c)
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

int CiLexer::readCharLiteral()
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

CiToken CiLexer::readString(bool interpolated)
{
	for (int offset = this->charOffset;; readCharLiteral()) {
		switch (peekChar()) {
		case -1:
			reportError("Unterminated string literal");
			return CiToken::endOfFile;
		case '\n':
			reportError("Unterminated string literal");
			this->stringValue = "";
			return CiToken::literalString;
		case '"':
			{
				int endOffset = this->charOffset;
				readChar();
				this->stringValue = std::string_view(reinterpret_cast<const char *>(this->input + offset), endOffset - offset);
			}
			return CiToken::literalString;
		case '{':
			if (interpolated) {
				int endOffset = this->charOffset;
				readChar();
				if (peekChar() != '{') {
					this->stringValue = std::string_view(reinterpret_cast<const char *>(this->input + offset), endOffset - offset);
					return CiToken::interpolatedString;
				}
			}
			break;
		default:
			break;
		}
	}
}

std::string CiLexer::getLexeme() const
{
	return std::string(reinterpret_cast<const char *>(this->input + this->lexemeOffset), this->charOffset - this->lexemeOffset);
}

void CiLexer::readId(int c)
{
	if (isLetterOrDigit(c)) {
		while (isLetterOrDigit(peekChar()))
			readChar();
		this->stringValue = getLexeme();
	}
	else {
		reportError("Invalid character");
		this->stringValue = "";
	}
}

CiToken CiLexer::readPreToken()
{
	for (;;) {
		bool atLineStart = this->atLineStart;
		this->tokenColumn = this->column;
		this->lexemeOffset = this->charOffset;
		int c = readChar();
		switch (c) {
		case -1:
			return CiToken::endOfFile;
		case '\t':
		case '\r':
		case ' ':
			break;
		case '\n':
			if (this->lineMode)
				return CiToken::endOfLine;
			break;
		case '#':
			if (!atLineStart)
				return CiToken::hash;
			this->lexemeOffset = this->charOffset;
			readId(readChar());
			if (this->stringValue == "if")
				return CiToken::preIf;
			else if (this->stringValue == "elif")
				return CiToken::preElIf;
			else if (this->stringValue == "else")
				return CiToken::preElse;
			else if (this->stringValue == "endif")
				return CiToken::preEndIf;
			else {
				reportError("Unknown preprocessor directive");
				continue;
			}
		case ';':
			return CiToken::semicolon;
		case '.':
			if (eatChar('.'))
				return CiToken::range;
			return CiToken::dot;
		case ',':
			return CiToken::comma;
		case '(':
			return CiToken::leftParenthesis;
		case ')':
			return CiToken::rightParenthesis;
		case '[':
			return CiToken::leftBracket;
		case ']':
			return CiToken::rightBracket;
		case '{':
			return CiToken::leftBrace;
		case '}':
			return CiToken::rightBrace;
		case '~':
			return CiToken::tilde;
		case '?':
			return CiToken::questionMark;
		case ':':
			return CiToken::colon;
		case '+':
			if (eatChar('+'))
				return CiToken::increment;
			if (eatChar('='))
				return CiToken::addAssign;
			return CiToken::plus;
		case '-':
			if (eatChar('-'))
				return CiToken::decrement;
			if (eatChar('='))
				return CiToken::subAssign;
			return CiToken::minus;
		case '*':
			if (eatChar('='))
				return CiToken::mulAssign;
			return CiToken::asterisk;
		case '/':
			if (eatChar('/')) {
				c = readChar();
				if (c == '/' && this->enableDocComments) {
					skipWhitespace();
					switch (peekChar()) {
					case '\n':
						return CiToken::docBlank;
					case '*':
						readChar();
						skipWhitespace();
						return CiToken::docBullet;
					default:
						return CiToken::docRegular;
					}
				}
				while (c != '\n' && c >= 0)
					c = readChar();
				if (c == '\n' && this->lineMode)
					return CiToken::endOfLine;
				break;
			}
			if (eatChar('*')) {
				int startLine = this->line;
				do {
					c = readChar();
					if (c < 0) {
						reportError(std::format("Unterminated multi-line comment, started in line {}", startLine));
						return CiToken::endOfFile;
					}
				}
				while (c != '*' || peekChar() != '/');
				readChar();
				break;
			}
			if (eatChar('='))
				return CiToken::divAssign;
			return CiToken::slash;
		case '%':
			if (eatChar('='))
				return CiToken::modAssign;
			return CiToken::mod;
		case '&':
			if (eatChar('&'))
				return CiToken::condAnd;
			if (eatChar('='))
				return CiToken::andAssign;
			return CiToken::and_;
		case '|':
			if (eatChar('|'))
				return CiToken::condOr;
			if (eatChar('='))
				return CiToken::orAssign;
			return CiToken::or_;
		case '^':
			if (eatChar('='))
				return CiToken::xorAssign;
			return CiToken::xor_;
		case '=':
			if (eatChar('='))
				return CiToken::equal;
			if (eatChar('>'))
				return CiToken::fatArrow;
			return CiToken::assign;
		case '!':
			if (eatChar('='))
				return CiToken::notEqual;
			return CiToken::exclamationMark;
		case '<':
			if (eatChar('<')) {
				if (eatChar('='))
					return CiToken::shiftLeftAssign;
				return CiToken::shiftLeft;
			}
			if (eatChar('='))
				return CiToken::lessOrEqual;
			return CiToken::less;
		case '>':
			if (this->parsingTypeArg)
				return CiToken::rightAngle;
			if (eatChar('>')) {
				if (eatChar('='))
					return CiToken::shiftRightAssign;
				return CiToken::shiftRight;
			}
			if (eatChar('='))
				return CiToken::greaterOrEqual;
			return CiToken::greater;
		case '\'':
			if (peekChar() == '\'')
				reportError("Empty character literal");
			else
				this->longValue = readCharLiteral();
			if (!eatChar('\''))
				reportError("Unterminated character literal");
			return CiToken::literalChar;
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
			readId(c);
			if (this->stringValue == "")
				continue;
			else if (this->stringValue == "abstract")
				return CiToken::abstract;
			else if (this->stringValue == "assert")
				return CiToken::assert;
			else if (this->stringValue == "break")
				return CiToken::break_;
			else if (this->stringValue == "case")
				return CiToken::case_;
			else if (this->stringValue == "class")
				return CiToken::class_;
			else if (this->stringValue == "const")
				return CiToken::const_;
			else if (this->stringValue == "continue")
				return CiToken::continue_;
			else if (this->stringValue == "default")
				return CiToken::default_;
			else if (this->stringValue == "do")
				return CiToken::do_;
			else if (this->stringValue == "else")
				return CiToken::else_;
			else if (this->stringValue == "enum")
				return CiToken::enum_;
			else if (this->stringValue == "false")
				return CiToken::false_;
			else if (this->stringValue == "for")
				return CiToken::for_;
			else if (this->stringValue == "foreach")
				return CiToken::foreach;
			else if (this->stringValue == "if")
				return CiToken::if_;
			else if (this->stringValue == "in")
				return CiToken::in;
			else if (this->stringValue == "internal")
				return CiToken::internal;
			else if (this->stringValue == "is")
				return CiToken::is;
			else if (this->stringValue == "lock")
				return CiToken::lock_;
			else if (this->stringValue == "native")
				return CiToken::native;
			else if (this->stringValue == "new")
				return CiToken::new_;
			else if (this->stringValue == "null")
				return CiToken::null;
			else if (this->stringValue == "override")
				return CiToken::override_;
			else if (this->stringValue == "protected")
				return CiToken::protected_;
			else if (this->stringValue == "public")
				return CiToken::public_;
			else if (this->stringValue == "resource")
				return CiToken::resource;
			else if (this->stringValue == "return")
				return CiToken::return_;
			else if (this->stringValue == "sealed")
				return CiToken::sealed;
			else if (this->stringValue == "static")
				return CiToken::static_;
			else if (this->stringValue == "switch")
				return CiToken::switch_;
			else if (this->stringValue == "throw")
				return CiToken::throw_;
			else if (this->stringValue == "throws")
				return CiToken::throws;
			else if (this->stringValue == "true")
				return CiToken::true_;
			else if (this->stringValue == "virtual")
				return CiToken::virtual_;
			else if (this->stringValue == "void")
				return CiToken::void_;
			else if (this->stringValue == "when")
				return CiToken::when;
			else if (this->stringValue == "while")
				return CiToken::while_;
			else
				return CiToken::id;
		}
	}
}

void CiLexer::nextPreToken()
{
	this->currentToken = readPreToken();
}

bool CiLexer::see(CiToken token) const
{
	return this->currentToken == token;
}

std::string_view CiLexer::tokenToString(CiToken token)
{
	switch (token) {
	case CiToken::endOfFile:
		return "end-of-file";
	case CiToken::id:
		return "identifier";
	case CiToken::literalLong:
		return "integer constant";
	case CiToken::literalDouble:
		return "floating-point constant";
	case CiToken::literalChar:
		return "character constant";
	case CiToken::literalString:
		return "string constant";
	case CiToken::interpolatedString:
		return "interpolated string";
	case CiToken::semicolon:
		return "';'";
	case CiToken::dot:
		return "'.'";
	case CiToken::comma:
		return "','";
	case CiToken::leftParenthesis:
		return "'('";
	case CiToken::rightParenthesis:
		return "')'";
	case CiToken::leftBracket:
		return "'['";
	case CiToken::rightBracket:
		return "']'";
	case CiToken::leftBrace:
		return "'{'";
	case CiToken::rightBrace:
		return "'}'";
	case CiToken::plus:
		return "'+'";
	case CiToken::minus:
		return "'-'";
	case CiToken::asterisk:
		return "'*'";
	case CiToken::slash:
		return "'/'";
	case CiToken::mod:
		return "'%'";
	case CiToken::and_:
		return "'&'";
	case CiToken::or_:
		return "'|'";
	case CiToken::xor_:
		return "'^'";
	case CiToken::tilde:
		return "'~'";
	case CiToken::shiftLeft:
		return "'<<'";
	case CiToken::shiftRight:
		return "'>>'";
	case CiToken::equal:
		return "'=='";
	case CiToken::notEqual:
		return "'!='";
	case CiToken::less:
		return "'<'";
	case CiToken::lessOrEqual:
		return "'<='";
	case CiToken::greater:
		return "'>'";
	case CiToken::greaterOrEqual:
		return "'>='";
	case CiToken::rightAngle:
		return "'>'";
	case CiToken::condAnd:
		return "'&&'";
	case CiToken::condOr:
		return "'||'";
	case CiToken::exclamationMark:
		return "'!'";
	case CiToken::hash:
		return "'#'";
	case CiToken::assign:
		return "'='";
	case CiToken::addAssign:
		return "'+='";
	case CiToken::subAssign:
		return "'-='";
	case CiToken::mulAssign:
		return "'*='";
	case CiToken::divAssign:
		return "'/='";
	case CiToken::modAssign:
		return "'%='";
	case CiToken::andAssign:
		return "'&='";
	case CiToken::orAssign:
		return "'|='";
	case CiToken::xorAssign:
		return "'^='";
	case CiToken::shiftLeftAssign:
		return "'<<='";
	case CiToken::shiftRightAssign:
		return "'>>='";
	case CiToken::increment:
		return "'++'";
	case CiToken::decrement:
		return "'--'";
	case CiToken::questionMark:
		return "'?'";
	case CiToken::colon:
		return "':'";
	case CiToken::fatArrow:
		return "'=>'";
	case CiToken::range:
		return "'..'";
	case CiToken::docRegular:
	case CiToken::docBullet:
	case CiToken::docBlank:
		return "'///'";
	case CiToken::abstract:
		return "'abstract'";
	case CiToken::assert:
		return "'assert'";
	case CiToken::break_:
		return "'break'";
	case CiToken::case_:
		return "'case'";
	case CiToken::class_:
		return "'class'";
	case CiToken::const_:
		return "'const'";
	case CiToken::continue_:
		return "'continue'";
	case CiToken::default_:
		return "'default'";
	case CiToken::do_:
		return "'do'";
	case CiToken::else_:
		return "'else'";
	case CiToken::enum_:
		return "'enum'";
	case CiToken::false_:
		return "'false'";
	case CiToken::for_:
		return "'for'";
	case CiToken::foreach:
		return "'foreach'";
	case CiToken::if_:
		return "'if'";
	case CiToken::in:
		return "'in'";
	case CiToken::internal:
		return "'internal'";
	case CiToken::is:
		return "'is'";
	case CiToken::lock_:
		return "'lock'";
	case CiToken::native:
		return "'native'";
	case CiToken::new_:
		return "'new'";
	case CiToken::null:
		return "'null'";
	case CiToken::override_:
		return "'override'";
	case CiToken::protected_:
		return "'protected'";
	case CiToken::public_:
		return "'public'";
	case CiToken::resource:
		return "'resource'";
	case CiToken::return_:
		return "'return'";
	case CiToken::sealed:
		return "'sealed'";
	case CiToken::static_:
		return "'static'";
	case CiToken::switch_:
		return "'switch'";
	case CiToken::throw_:
		return "'throw'";
	case CiToken::throws:
		return "'throws'";
	case CiToken::true_:
		return "'true'";
	case CiToken::virtual_:
		return "'virtual'";
	case CiToken::void_:
		return "'void'";
	case CiToken::when:
		return "'when'";
	case CiToken::while_:
		return "'while'";
	case CiToken::endOfLine:
		return "end-of-line";
	case CiToken::preIf:
		return "'#if'";
	case CiToken::preElIf:
		return "'#elif'";
	case CiToken::preElse:
		return "'#else'";
	case CiToken::preEndIf:
		return "'#endif'";
	default:
		std::abort();
	}
}

bool CiLexer::check(CiToken expected)
{
	if (see(expected))
		return true;
	reportError(std::format("Expected {}, got {}", tokenToString(expected), tokenToString(this->currentToken)));
	return false;
}

bool CiLexer::eatPre(CiToken token)
{
	if (see(token)) {
		nextPreToken();
		return true;
	}
	return false;
}

bool CiLexer::parsePrePrimary()
{
	if (eatPre(CiToken::exclamationMark))
		return !parsePrePrimary();
	if (eatPre(CiToken::leftParenthesis)) {
		bool result = parsePreOr();
		check(CiToken::rightParenthesis);
		nextPreToken();
		return result;
	}
	if (see(CiToken::id)) {
		bool result = this->preSymbols.contains(this->stringValue);
		nextPreToken();
		return result;
	}
	if (eatPre(CiToken::false_))
		return false;
	if (eatPre(CiToken::true_))
		return true;
	reportError("Invalid preprocessor expression");
	return false;
}

bool CiLexer::parsePreEquality()
{
	bool result = parsePrePrimary();
	for (;;) {
		if (eatPre(CiToken::equal))
			result = result == parsePrePrimary();
		else if (eatPre(CiToken::notEqual))
			result ^= parsePrePrimary();
		else
			return result;
	}
}

bool CiLexer::parsePreAnd()
{
	bool result = parsePreEquality();
	while (eatPre(CiToken::condAnd))
		result &= parsePreEquality();
	return result;
}

bool CiLexer::parsePreOr()
{
	bool result = parsePreAnd();
	while (eatPre(CiToken::condOr))
		result |= parsePreAnd();
	return result;
}

bool CiLexer::parsePreExpr()
{
	this->lineMode = true;
	nextPreToken();
	bool result = parsePreOr();
	check(CiToken::endOfLine);
	this->lineMode = false;
	return result;
}

void CiLexer::expectEndOfLine(std::string_view directive)
{
	this->lineMode = true;
	CiToken token = readPreToken();
	if (token != CiToken::endOfLine && token != CiToken::endOfFile)
		reportError(std::format("Unexpected characters after '{}'", directive));
	this->lineMode = false;
}

bool CiLexer::popPreElse(std::string_view directive)
{
	if (this->preElseStack.size() == 0) {
		reportError(std::format("'{}' with no matching '#if'", directive));
		return false;
	}
	if ([](std::stack<bool> &s) { bool top = s.top(); s.pop(); return top; }(this->preElseStack) && directive != "#endif")
		reportError(std::format("'{}' after '#else'", directive));
	return true;
}

void CiLexer::skipUnmet(CiPreState state)
{
	this->enableDocComments = false;
	for (;;) {
		switch (readPreToken()) {
		case CiToken::endOfFile:
			reportError("Expected '#endif', got end-of-file");
			return;
		case CiToken::preIf:
			parsePreExpr();
			skipUnmet(CiPreState::already);
			break;
		case CiToken::preElIf:
			if (state == CiPreState::alreadyElse)
				reportError("'#elif' after '#else'");
			if (parsePreExpr() && state == CiPreState::notYet) {
				this->preElseStack.push(false);
				return;
			}
			break;
		case CiToken::preElse:
			if (state == CiPreState::alreadyElse)
				reportError("'#else' after '#else'");
			expectEndOfLine("#else");
			if (state == CiPreState::notYet) {
				this->preElseStack.push(true);
				return;
			}
			state = CiPreState::alreadyElse;
			break;
		case CiToken::preEndIf:
			expectEndOfLine("#endif");
			return;
		default:
			break;
		}
	}
}

CiToken CiLexer::readToken()
{
	for (;;) {
		this->enableDocComments = true;
		CiToken token = readPreToken();
		bool matched;
		switch (token) {
		case CiToken::endOfFile:
			if (this->preElseStack.size() != 0)
				reportError("Expected '#endif', got end-of-file");
			return CiToken::endOfFile;
		case CiToken::preIf:
			if (parsePreExpr())
				this->preElseStack.push(false);
			else
				skipUnmet(CiPreState::notYet);
			break;
		case CiToken::preElIf:
			matched = popPreElse("#elif");
			parsePreExpr();
			if (matched)
				skipUnmet(CiPreState::already);
			break;
		case CiToken::preElse:
			matched = popPreElse("#else");
			expectEndOfLine("#else");
			if (matched)
				skipUnmet(CiPreState::alreadyElse);
			break;
		case CiToken::preEndIf:
			popPreElse("#endif");
			expectEndOfLine("#endif");
			break;
		default:
			return token;
		}
	}
}

CiToken CiLexer::nextToken()
{
	CiToken token = this->currentToken;
	this->currentToken = readToken();
	return token;
}

bool CiLexer::eat(CiToken token)
{
	if (see(token)) {
		nextToken();
		return true;
	}
	return false;
}

bool CiLexer::expect(CiToken expected)
{
	bool found = check(expected);
	nextToken();
	return found;
}

void CiLexer::expectOrSkip(CiToken expected)
{
	if (check(expected))
		nextToken();
	else {
		do
			nextToken();
		while (!see(CiToken::endOfFile) && !eat(expected));
	}
}

void CiVisitor::visitOptionalStatement(const CiStatement * statement)
{
	if (statement != nullptr)
		statement->acceptStatement(this);
}

void CiVisitor::reportError(const CiStatement * statement, std::string_view message)
{
	std::cerr << getCurrentContainer()->filename << "(" << statement->line << "): ERROR: " << message << '\n';
	this->hasErrors = true;
}

bool CiExpr::completesNormally() const
{
	return true;
}

std::string CiExpr::toString() const
{
	std::abort();
}

bool CiExpr::isIndexing() const
{
	return false;
}

bool CiExpr::isLiteralZero() const
{
	return false;
}

bool CiExpr::isConstEnum() const
{
	return false;
}

int CiExpr::intValue() const
{
	std::abort();
}

void CiExpr::accept(CiVisitor * visitor, CiPriority parent) const
{
	std::abort();
}

void CiExpr::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitExpr(this);
}

bool CiExpr::isReferenceTo(const CiSymbol * symbol) const
{
	return false;
}

std::string CiSymbol::toString() const
{
	return this->name;
}

int CiScope::count() const
{
	return this->dict.size();
}

CiVar * CiScope::firstParameter() const
{
	CiVar * result = static_cast<CiVar *>(this->first);
	return result;
}

CiContainerType * CiScope::getContainer()
{
	for (CiScope * scope = this; scope != nullptr; scope = scope->parent) {
		if (CiContainerType *container = dynamic_cast<CiContainerType *>(scope))
			return container;
	}
	std::abort();
}

bool CiScope::contains(const CiSymbol * symbol) const
{
	return this->dict.count(symbol->name) != 0;
}

std::shared_ptr<CiSymbol> CiScope::tryLookup(std::string_view name, bool global) const
{
	for (const CiScope * scope = this; scope != nullptr && (global || !(dynamic_cast<const CiProgram *>(scope) || dynamic_cast<const CiSystem *>(scope))); scope = scope->parent) {
		if (scope->dict.count(name) != 0)
			return scope->dict.find(name)->second;
	}
	return nullptr;
}

void CiScope::add(std::shared_ptr<CiSymbol> symbol)
{
	this->dict[symbol->name] = symbol;
	symbol->next = nullptr;
	symbol->parent = this;
	if (this->first == nullptr)
		this->first = symbol.get();
	else
		this->last->next = symbol.get();
	this->last = symbol.get();
}

bool CiScope::encloses(const CiSymbol * symbol) const
{
	for (const CiScope * scope = symbol->parent; scope != nullptr; scope = scope->parent) {
		if (scope == this)
			return true;
	}
	return false;
}

void CiAggregateInitializer::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitAggregateInitializer(this);
}

std::string CiLiteral::getLiteralString() const
{
	std::abort();
}

bool CiLiteralNull::isDefaultValue() const
{
	return true;
}

void CiLiteralNull::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitLiteralNull();
}

std::string CiLiteralNull::toString() const
{
	return "null";
}

bool CiLiteralFalse::isDefaultValue() const
{
	return true;
}

void CiLiteralFalse::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitLiteralFalse();
}

std::string CiLiteralFalse::toString() const
{
	return "false";
}

bool CiLiteralTrue::isDefaultValue() const
{
	return false;
}

void CiLiteralTrue::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitLiteralTrue();
}

std::string CiLiteralTrue::toString() const
{
	return "true";
}

bool CiLiteralLong::isLiteralZero() const
{
	return this->value == 0;
}

int CiLiteralLong::intValue() const
{
	return static_cast<int>(this->value);
}

bool CiLiteralLong::isDefaultValue() const
{
	return this->value == 0;
}

void CiLiteralLong::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitLiteralLong(this->value);
}

std::string CiLiteralLong::getLiteralString() const
{
	return std::format("{}", this->value);
}

std::string CiLiteralLong::toString() const
{
	return std::format("{}", this->value);
}

std::shared_ptr<CiLiteralChar> CiLiteralChar::new_(int value, int line)
{
	std::shared_ptr<CiLiteralChar> citemp0 = std::make_shared<CiLiteralChar>();
	citemp0->line = line;
	citemp0->type = CiRangeType::new_(value, value);
	citemp0->value = value;
	return citemp0;
}

void CiLiteralChar::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitLiteralChar(static_cast<int>(this->value));
}

bool CiLiteralDouble::isDefaultValue() const
{
	return this->value == 0 && 1.0f / this->value > 0;
}

void CiLiteralDouble::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitLiteralDouble(this->value);
}

std::string CiLiteralDouble::getLiteralString() const
{
	return std::format("{}", this->value);
}

std::string CiLiteralDouble::toString() const
{
	return std::format("{}", this->value);
}

bool CiLiteralString::isDefaultValue() const
{
	return false;
}

void CiLiteralString::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitLiteralString(this->value);
}

std::string CiLiteralString::getLiteralString() const
{
	return this->value;
}

std::string CiLiteralString::toString() const
{
	return std::format("\"{}\"", this->value);
}

int CiLiteralString::getAsciiLength() const
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

int CiLiteralString::getAsciiAt(int i) const
{
	bool escaped = false;
	for (int c : this->value) {
		if (c < 0 || c > 127)
			return -1;
		if (!escaped && c == '\\')
			escaped = true;
		else if (i == 0)
			return escaped ? CiLexer::getEscapedChar(c) : c;
		else {
			i--;
			escaped = false;
		}
	}
	return -1;
}

int CiLiteralString::getOneAscii() const
{
	switch (this->value.length()) {
	case 1:
		{
			int c = this->value[0];
			return c >= 0 && c <= 127 ? c : -1;
		}
	case 2:
		return this->value[0] == '\\' ? CiLexer::getEscapedChar(this->value[1]) : -1;
	default:
		return -1;
	}
}

void CiInterpolatedString::addPart(std::string_view prefix, std::shared_ptr<CiExpr> arg, std::shared_ptr<CiExpr> widthExpr, int format, int precision)
{
	this->parts.emplace_back();
	CiInterpolatedPart * part = &static_cast<CiInterpolatedPart &>(this->parts.back());
	part->prefix = prefix;
	part->argument = arg;
	part->widthExpr = widthExpr;
	part->format = format;
	part->precision = precision;
}

void CiInterpolatedString::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitInterpolatedString(this, parent);
}

int CiImplicitEnumValue::intValue() const
{
	return this->value;
}

bool CiSymbolReference::isConstEnum() const
{
	return dynamic_cast<const CiEnum *>(this->symbol->parent);
}

int CiSymbolReference::intValue() const
{
	const CiConst * konst = static_cast<const CiConst *>(this->symbol);
	return konst->value->intValue();
}

void CiSymbolReference::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitSymbolReference(this, parent);
}

bool CiSymbolReference::isReferenceTo(const CiSymbol * symbol) const
{
	return this->symbol == symbol;
}

std::string CiSymbolReference::toString() const
{
	return std::string(this->left != nullptr ? std::format("{}.{}", this->left->toString(), this->name) : this->name);
}

bool CiPrefixExpr::isConstEnum() const
{
	return dynamic_cast<const CiEnumFlags *>(this->type.get()) && this->inner->isConstEnum();
}

int CiPrefixExpr::intValue() const
{
	assert(this->op == CiToken::tilde);
	return ~this->inner->intValue();
}

void CiPrefixExpr::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitPrefixExpr(this, parent);
}

void CiPostfixExpr::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitPostfixExpr(this, parent);
}

bool CiBinaryExpr::isIndexing() const
{
	return this->op == CiToken::leftBracket;
}

bool CiBinaryExpr::isConstEnum() const
{
	switch (this->op) {
	case CiToken::and_:
	case CiToken::or_:
	case CiToken::xor_:
		return dynamic_cast<const CiEnumFlags *>(this->type.get()) && this->left->isConstEnum() && this->right->isConstEnum();
	default:
		return false;
	}
}

int CiBinaryExpr::intValue() const
{
	switch (this->op) {
	case CiToken::and_:
		return this->left->intValue() & this->right->intValue();
	case CiToken::or_:
		return this->left->intValue() | this->right->intValue();
	case CiToken::xor_:
		return this->left->intValue() ^ this->right->intValue();
	default:
		std::abort();
	}
}

void CiBinaryExpr::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitBinaryExpr(this, parent);
}

bool CiBinaryExpr::isAssign() const
{
	switch (this->op) {
	case CiToken::assign:
	case CiToken::addAssign:
	case CiToken::subAssign:
	case CiToken::mulAssign:
	case CiToken::divAssign:
	case CiToken::modAssign:
	case CiToken::shiftLeftAssign:
	case CiToken::shiftRightAssign:
	case CiToken::andAssign:
	case CiToken::orAssign:
	case CiToken::xorAssign:
		return true;
	default:
		return false;
	}
}

std::string_view CiBinaryExpr::getOpString() const
{
	switch (this->op) {
	case CiToken::plus:
		return "+";
	case CiToken::minus:
		return "-";
	case CiToken::asterisk:
		return "*";
	case CiToken::slash:
		return "/";
	case CiToken::mod:
		return "%";
	case CiToken::shiftLeft:
		return "<<";
	case CiToken::shiftRight:
		return ">>";
	case CiToken::less:
		return "<";
	case CiToken::lessOrEqual:
		return "<=";
	case CiToken::greater:
		return ">";
	case CiToken::greaterOrEqual:
		return ">=";
	case CiToken::equal:
		return "==";
	case CiToken::notEqual:
		return "!=";
	case CiToken::and_:
		return "&";
	case CiToken::or_:
		return "|";
	case CiToken::xor_:
		return "^";
	case CiToken::condAnd:
		return "&&";
	case CiToken::condOr:
		return "||";
	case CiToken::assign:
		return "=";
	case CiToken::addAssign:
		return "+=";
	case CiToken::subAssign:
		return "-=";
	case CiToken::mulAssign:
		return "*=";
	case CiToken::divAssign:
		return "/=";
	case CiToken::modAssign:
		return "%=";
	case CiToken::shiftLeftAssign:
		return "<<=";
	case CiToken::shiftRightAssign:
		return ">>=";
	case CiToken::andAssign:
		return "&=";
	case CiToken::orAssign:
		return "|=";
	case CiToken::xorAssign:
		return "^=";
	default:
		std::abort();
	}
}

std::string CiBinaryExpr::toString() const
{
	return std::string(this->op == CiToken::leftBracket ? std::format("{}[{}]", this->left->toString(), this->right->toString()) : std::format("({} {} {})", this->left->toString(), getOpString(), this->right->toString()));
}

void CiSelectExpr::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitSelectExpr(this, parent);
}

std::string CiSelectExpr::toString() const
{
	return std::format("({} ? {} : {})", this->cond->toString(), this->onTrue->toString(), this->onFalse->toString());
}

void CiCallExpr::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitCallExpr(this, parent);
}

void CiLambdaExpr::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitLambdaExpr(this);
}

bool CiCondCompletionStatement::completesNormally() const
{
	return this->completesNormallyValue;
}

void CiCondCompletionStatement::setCompletesNormally(bool value)
{
	this->completesNormallyValue = value;
}

void CiBlock::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitBlock(this);
}

bool CiAssert::completesNormally() const
{
	return !dynamic_cast<const CiLiteralFalse *>(this->cond.get());
}

void CiAssert::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitAssert(this);
}

bool CiBreak::completesNormally() const
{
	return false;
}

void CiBreak::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitBreak(this);
}

bool CiContinue::completesNormally() const
{
	return false;
}

void CiContinue::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitContinue(this);
}

void CiDoWhile::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitDoWhile(this);
}

void CiFor::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitFor(this);
}

void CiForeach::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitForeach(this);
}

CiVar * CiForeach::getVar() const
{
	return this->firstParameter();
}

CiVar * CiForeach::getValueVar() const
{
	return this->firstParameter()->nextParameter();
}

void CiIf::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitIf(this);
}

bool CiLock::completesNormally() const
{
	return this->body->completesNormally();
}

void CiLock::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitLock(this);
}

bool CiNative::completesNormally() const
{
	return true;
}

void CiNative::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitNative(this);
}

bool CiReturn::completesNormally() const
{
	return false;
}

void CiReturn::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitReturn(this);
}

void CiSwitch::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitSwitch(this);
}

bool CiSwitch::isTypeMatching() const
{
	const CiClassType * klass;
	return (klass = dynamic_cast<const CiClassType *>(this->value->type.get())) && klass->class_->id != CiId::stringClass;
}

bool CiSwitch::hasWhen() const
{
	return std::any_of(this->cases.begin(), this->cases.end(), [](const CiCase &kase) { return std::any_of(kase.values.begin(), kase.values.end(), [](const std::shared_ptr<CiExpr> &value) { const CiBinaryExpr * when1;
	return (when1 = dynamic_cast<const CiBinaryExpr *>(value.get())) && when1->op == CiToken::when; }); });
}

int CiSwitch::lengthWithoutTrailingBreak(const std::vector<std::shared_ptr<CiStatement>> * body)
{
	int length = body->size();
	if (length > 0 && dynamic_cast<const CiBreak *>((*body)[length - 1].get()))
		length--;
	return length;
}

bool CiSwitch::hasDefault() const
{
	return lengthWithoutTrailingBreak(&this->defaultBody) > 0;
}

bool CiSwitch::hasBreak(const CiStatement * statement)
{
	if (dynamic_cast<const CiBreak *>(statement))
		return true;
	else if (const CiIf *ifStatement = dynamic_cast<const CiIf *>(statement))
		return hasBreak(ifStatement->onTrue.get()) || (ifStatement->onFalse != nullptr && hasBreak(ifStatement->onFalse.get()));
	else if (const CiBlock *block = dynamic_cast<const CiBlock *>(statement))
		return std::any_of(block->statements.begin(), block->statements.end(), [](const std::shared_ptr<CiStatement> &child) { return hasBreak(child.get()); });
	else
		return false;
}

bool CiSwitch::hasEarlyBreak(const std::vector<std::shared_ptr<CiStatement>> * body)
{
	int length = lengthWithoutTrailingBreak(body);
	for (int i = 0; i < length; i++) {
		if (hasBreak((*body)[i].get()))
			return true;
	}
	return false;
}

bool CiSwitch::listHasContinue(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	return std::any_of(statements->begin(), statements->end(), [](const std::shared_ptr<CiStatement> &statement) { return hasContinue(statement.get()); });
}

bool CiSwitch::hasContinue(const CiStatement * statement)
{
	if (dynamic_cast<const CiContinue *>(statement))
		return true;
	else if (const CiIf *ifStatement = dynamic_cast<const CiIf *>(statement))
		return hasContinue(ifStatement->onTrue.get()) || (ifStatement->onFalse != nullptr && hasContinue(ifStatement->onFalse.get()));
	else if (const CiSwitch *switchStatement = dynamic_cast<const CiSwitch *>(statement))
		return std::any_of(switchStatement->cases.begin(), switchStatement->cases.end(), [](const CiCase &kase) { return listHasContinue(&kase.body); }) || listHasContinue(&switchStatement->defaultBody);
	else if (const CiBlock *block = dynamic_cast<const CiBlock *>(statement))
		return listHasContinue(&block->statements);
	else
		return false;
}

bool CiSwitch::hasEarlyBreakAndContinue(const std::vector<std::shared_ptr<CiStatement>> * body)
{
	return hasEarlyBreak(body) && listHasContinue(body);
}

bool CiThrow::completesNormally() const
{
	return false;
}

void CiThrow::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitThrow(this);
}

void CiWhile::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitWhile(this);
}

std::string CiType::getArraySuffix() const
{
	return "";
}

bool CiType::isAssignableFrom(const CiType * right) const
{
	return this == right;
}

bool CiType::equalsType(const CiType * right) const
{
	return this == right;
}

bool CiType::isArray() const
{
	return false;
}

bool CiType::isFinal() const
{
	return false;
}

const CiType * CiType::getBaseType() const
{
	return this;
}

const CiType * CiType::getStorageType() const
{
	return this;
}

const CiClassType * CiType::asClassType() const
{
	const CiClassType * klass = static_cast<const CiClassType *>(this);
	return klass;
}

bool CiIntegerType::isAssignableFrom(const CiType * right) const
{
	return dynamic_cast<const CiIntegerType *>(right) || right->id == CiId::floatIntType;
}

void CiRangeType::addMinMaxValue(std::shared_ptr<CiRangeType> target, std::string_view name, int value)
{
	std::shared_ptr<CiRangeType> citemp0 = std::make_shared<CiRangeType>();
	citemp0->min = value;
	citemp0->max = value;
	std::shared_ptr<CiRangeType> type = target->min == target->max ? target : citemp0;
	std::shared_ptr<CiLiteralLong> citemp1 = std::make_shared<CiLiteralLong>();
	citemp1->type = type;
	citemp1->value = value;
	std::shared_ptr<CiConst> citemp2 = std::make_shared<CiConst>();
	citemp2->visibility = CiVisibility::public_;
	citemp2->name = name;
	citemp2->value = citemp1;
	citemp2->visitStatus = CiVisitStatus::done;
	target->add(citemp2);
}

std::shared_ptr<CiRangeType> CiRangeType::new_(int min, int max)
{
	assert(min <= max);
	std::shared_ptr<CiRangeType> result = std::make_shared<CiRangeType>();
	result->id = min >= 0 && max <= 255 ? CiId::byteRange : min >= -128 && max <= 127 ? CiId::sByteRange : min >= -32768 && max <= 32767 ? CiId::shortRange : min >= 0 && max <= 65535 ? CiId::uShortRange : CiId::intType;
	result->min = min;
	result->max = max;
	addMinMaxValue(result, "MinValue", min);
	addMinMaxValue(result, "MaxValue", max);
	return result;
}

std::string CiRangeType::toString() const
{
	return std::string(this->min == this->max ? std::format("{}", this->min) : std::format("({} .. {})", this->min, this->max));
}

bool CiRangeType::isAssignableFrom(const CiType * right) const
{
	if (const CiRangeType *range = dynamic_cast<const CiRangeType *>(right))
		return this->min <= range->max && this->max >= range->min;
	else if (dynamic_cast<const CiIntegerType *>(right))
		return true;
	else
		return right->id == CiId::floatIntType;
}

bool CiRangeType::equalsType(const CiType * right) const
{
	const CiRangeType * that;
	return (that = dynamic_cast<const CiRangeType *>(right)) && this->min == that->min && this->max == that->max;
}

int CiRangeType::getMask(int v)
{
	v |= v >> 1;
	v |= v >> 2;
	v |= v >> 4;
	v |= v >> 8;
	v |= v >> 16;
	return v;
}

int CiRangeType::getVariableBits() const
{
	return getMask(this->min ^ this->max);
}

bool CiFloatingType::isAssignableFrom(const CiType * right) const
{
	return dynamic_cast<const CiNumericType *>(right);
}

bool CiNamedValue::isAssignableStorage() const
{
	return dynamic_cast<const CiStorageType *>(this->type.get()) && !dynamic_cast<const CiArrayStorageType *>(this->type.get()) && dynamic_cast<const CiLiteralNull *>(this->value.get());
}
CiMember::CiMember()
{
}

std::shared_ptr<CiVar> CiVar::new_(std::shared_ptr<CiType> type, std::string_view name, std::shared_ptr<CiExpr> defaultValue)
{
	std::shared_ptr<CiVar> citemp0 = std::make_shared<CiVar>();
	citemp0->type = type;
	citemp0->name = name;
	citemp0->value = defaultValue;
	return citemp0;
}

void CiVar::accept(CiVisitor * visitor, CiPriority parent) const
{
	visitor->visitVar(this);
}

CiVar * CiVar::nextParameter() const
{
	CiVar * def = static_cast<CiVar *>(this->next);
	return def;
}

void CiConst::acceptStatement(CiVisitor * visitor) const
{
	visitor->visitConst(this);
}

bool CiConst::isStatic() const
{
	return true;
}

bool CiField::isStatic() const
{
	return false;
}

bool CiProperty::isStatic() const
{
	return false;
}

std::shared_ptr<CiProperty> CiProperty::new_(std::shared_ptr<CiType> type, CiId id, std::string_view name)
{
	std::shared_ptr<CiProperty> citemp0 = std::make_shared<CiProperty>();
	citemp0->visibility = CiVisibility::public_;
	citemp0->type = type;
	citemp0->id = id;
	citemp0->name = name;
	return citemp0;
}

bool CiStaticProperty::isStatic() const
{
	return true;
}

std::shared_ptr<CiStaticProperty> CiStaticProperty::new_(std::shared_ptr<CiType> type, CiId id, std::string_view name)
{
	std::shared_ptr<CiStaticProperty> citemp0 = std::make_shared<CiStaticProperty>();
	citemp0->visibility = CiVisibility::public_;
	citemp0->type = type;
	citemp0->id = id;
	citemp0->name = name;
	return citemp0;
}

bool CiMethodBase::isStatic() const
{
	return false;
}

std::shared_ptr<CiMethod> CiMethod::new_(CiVisibility visibility, std::shared_ptr<CiType> type, CiId id, std::string_view name, std::shared_ptr<CiVar> param0, std::shared_ptr<CiVar> param1, std::shared_ptr<CiVar> param2, std::shared_ptr<CiVar> param3)
{
	std::shared_ptr<CiMethod> result = std::make_shared<CiMethod>();
	result->visibility = visibility;
	result->callType = CiCallType::normal;
	result->type = type;
	result->id = id;
	result->name = name;
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

std::shared_ptr<CiMethod> CiMethod::newStatic(std::shared_ptr<CiType> type, CiId id, std::string_view name, std::shared_ptr<CiVar> param0, std::shared_ptr<CiVar> param1, std::shared_ptr<CiVar> param2)
{
	std::shared_ptr<CiMethod> result = new_(CiVisibility::public_, type, id, name, param0, param1, param2);
	result->callType = CiCallType::static_;
	return result;
}

std::shared_ptr<CiMethod> CiMethod::newMutator(CiVisibility visibility, std::shared_ptr<CiType> type, CiId id, std::string_view name, std::shared_ptr<CiVar> param0, std::shared_ptr<CiVar> param1, std::shared_ptr<CiVar> param2)
{
	std::shared_ptr<CiMethod> result = new_(visibility, type, id, name, param0, param1, param2);
	result->isMutator = true;
	return result;
}

bool CiMethod::isStatic() const
{
	return this->callType == CiCallType::static_;
}

bool CiMethod::isAbstractOrVirtual() const
{
	return this->callType == CiCallType::abstract || this->callType == CiCallType::virtual_;
}

const CiMethod * CiMethod::getDeclaringMethod() const
{
	const CiMethod * method = this;
	while (method->callType == CiCallType::override_) {
		const CiMethod * baseMethod = static_cast<const CiMethod *>(method->parent->parent->tryLookup(method->name, false).get());
		method = baseMethod;
	}
	return method;
}

bool CiMethod::isToString() const
{
	return this->name == "ToString" && this->callType != CiCallType::static_ && this->parameters.count() == 0;
}
CiMethodGroup::CiMethodGroup()
{
}

bool CiMethodGroup::isStatic() const
{
	std::abort();
}

std::shared_ptr<CiMethodGroup> CiMethodGroup::new_(std::shared_ptr<CiMethod> method0, std::shared_ptr<CiMethod> method1)
{
	std::shared_ptr<CiMethodGroup> result = std::make_shared<CiMethodGroup>();
	result->visibility = method0->visibility;
	result->name = method0->name;
	result->methods[0] = method0;
	result->methods[1] = method1;
	return result;
}

const CiSymbol * CiEnum::getFirstValue() const
{
	const CiSymbol * symbol = this->first;
	while (!dynamic_cast<const CiConst *>(symbol))
		symbol = symbol->next;
	return symbol;
}

void CiEnum::acceptValues(CiVisitor * visitor) const
{
	const CiConst * previous = nullptr;
	for (const CiSymbol * symbol = this->first; symbol != nullptr; symbol = symbol->next) {
		if (const CiConst *konst = dynamic_cast<const CiConst *>(symbol)) {
			visitor->visitEnumValue(konst, previous);
			previous = konst;
		}
	}
}
CiClass::CiClass()
{
	std::shared_ptr<CiReadWriteClassType> citemp0 = std::make_shared<CiReadWriteClassType>();
	citemp0->class_ = this;
	add(CiVar::new_(citemp0, "this"));
}

bool CiClass::hasBaseClass() const
{
	return !this->baseClassName.empty();
}

bool CiClass::addsVirtualMethods() const
{
	for (const CiSymbol * symbol = this->first; symbol != nullptr; symbol = symbol->next) {
		const CiMethod * method;
		if ((method = dynamic_cast<const CiMethod *>(symbol)) && method->isAbstractOrVirtual())
			return true;
	}
	return false;
}

std::shared_ptr<CiClass> CiClass::new_(CiCallType callType, CiId id, std::string_view name, int typeParameterCount)
{
	std::shared_ptr<CiClass> citemp0 = std::make_shared<CiClass>();
	citemp0->callType = callType;
	citemp0->id = id;
	citemp0->name = name;
	citemp0->typeParameterCount = typeParameterCount;
	return citemp0;
}

bool CiClass::isSameOrBaseOf(const CiClass * derived) const
{
	while (derived != this) {
		if (const CiClass *parent = dynamic_cast<const CiClass *>(derived->parent))
			derived = parent;
		else
			return false;
	}
	return true;
}

bool CiClass::hasToString() const
{
	const CiMethod * method;
	return (method = dynamic_cast<const CiMethod *>(tryLookup("ToString", false).get())) && method->isToString();
}

bool CiClass::addsToString() const
{
	const CiMethod * method;
	return this->dict.count("ToString") != 0 && (method = dynamic_cast<const CiMethod *>(this->dict.find("ToString")->second.get())) && method->isToString() && method->callType != CiCallType::override_ && method->callType != CiCallType::sealed;
}

std::shared_ptr<CiType> CiClassType::getElementType() const
{
	return this->typeArg0;
}

const CiType * CiClassType::getKeyType() const
{
	return this->typeArg0.get();
}

std::shared_ptr<CiType> CiClassType::getValueType() const
{
	return this->typeArg1;
}

bool CiClassType::isArray() const
{
	return this->class_->id == CiId::arrayPtrClass;
}

const CiType * CiClassType::getBaseType() const
{
	return isArray() ? getElementType()->getBaseType() : this;
}

bool CiClassType::equalTypeArguments(const CiClassType * right) const
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

bool CiClassType::isAssignableFromClass(const CiClassType * right) const
{
	return this->class_->isSameOrBaseOf(right->class_) && equalTypeArguments(right);
}

bool CiClassType::isAssignableFrom(const CiType * right) const
{
	const CiClassType * rightClass;
	return (this->nullable && right->id == CiId::nullType) || ((rightClass = dynamic_cast<const CiClassType *>(right)) && isAssignableFromClass(rightClass));
}

bool CiClassType::equalsTypeInternal(const CiClassType * that) const
{
	return this->nullable == that->nullable && this->class_ == that->class_ && equalTypeArguments(that);
}

bool CiClassType::equalsType(const CiType * right) const
{
	const CiClassType * that;
	return (that = dynamic_cast<const CiClassType *>(right)) && !dynamic_cast<const CiReadWriteClassType *>(right) && equalsTypeInternal(that);
}

std::string CiClassType::getArraySuffix() const
{
	return std::string(isArray() ? "[]" : "");
}

std::string_view CiClassType::getClassSuffix() const
{
	return "";
}

std::string_view CiClassType::getNullableSuffix() const
{
	return this->nullable ? "?" : "";
}

std::string CiClassType::toString() const
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

bool CiReadWriteClassType::isAssignableFrom(const CiType * right) const
{
	const CiReadWriteClassType * rightClass;
	return (this->nullable && right->id == CiId::nullType) || ((rightClass = dynamic_cast<const CiReadWriteClassType *>(right)) && isAssignableFromClass(rightClass));
}

bool CiReadWriteClassType::equalsType(const CiType * right) const
{
	const CiReadWriteClassType * that;
	return (that = dynamic_cast<const CiReadWriteClassType *>(right)) && !dynamic_cast<const CiStorageType *>(right) && !dynamic_cast<const CiDynamicPtrType *>(right) && equalsTypeInternal(that);
}

std::string CiReadWriteClassType::getArraySuffix() const
{
	return std::string(isArray() ? "[]!" : "");
}

std::string_view CiReadWriteClassType::getClassSuffix() const
{
	return "!";
}

bool CiStorageType::isFinal() const
{
	return this->class_->id != CiId::matchClass;
}

bool CiStorageType::isAssignableFrom(const CiType * right) const
{
	const CiStorageType * rightClass;
	return (rightClass = dynamic_cast<const CiStorageType *>(right)) && this->class_ == rightClass->class_ && equalTypeArguments(rightClass);
}

bool CiStorageType::equalsType(const CiType * right) const
{
	const CiStorageType * that;
	return (that = dynamic_cast<const CiStorageType *>(right)) && equalsTypeInternal(that);
}

std::string_view CiStorageType::getClassSuffix() const
{
	return "()";
}

bool CiDynamicPtrType::isAssignableFrom(const CiType * right) const
{
	const CiDynamicPtrType * rightClass;
	return (this->nullable && right->id == CiId::nullType) || ((rightClass = dynamic_cast<const CiDynamicPtrType *>(right)) && isAssignableFromClass(rightClass));
}

bool CiDynamicPtrType::equalsType(const CiType * right) const
{
	const CiDynamicPtrType * that;
	return (that = dynamic_cast<const CiDynamicPtrType *>(right)) && equalsTypeInternal(that);
}

std::string CiDynamicPtrType::getArraySuffix() const
{
	return std::string(isArray() ? "[]#" : "");
}

std::string_view CiDynamicPtrType::getClassSuffix() const
{
	return "#";
}

const CiType * CiArrayStorageType::getBaseType() const
{
	return getElementType()->getBaseType();
}

bool CiArrayStorageType::isArray() const
{
	return true;
}

std::string CiArrayStorageType::getArraySuffix() const
{
	return std::format("[{}]", this->length);
}

bool CiArrayStorageType::equalsType(const CiType * right) const
{
	const CiArrayStorageType * that;
	return (that = dynamic_cast<const CiArrayStorageType *>(right)) && getElementType()->equalsType(that->getElementType().get()) && this->length == that->length;
}

const CiType * CiArrayStorageType::getStorageType() const
{
	return getElementType()->getStorageType();
}

bool CiStringStorageType::isAssignableFrom(const CiType * right) const
{
	return dynamic_cast<const CiStringType *>(right);
}

std::string_view CiStringStorageType::getClassSuffix() const
{
	return "()";
}

bool CiPrintableType::isAssignableFrom(const CiType * right) const
{
	if (dynamic_cast<const CiNumericType *>(right) || dynamic_cast<const CiStringType *>(right))
		return true;
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(right))
		return klass->class_->hasToString();
	else
		return false;
}
CiSystem::CiSystem()
{
	this->voidType->id = CiId::voidType;
	this->voidType->name = "void";
	this->nullType->id = CiId::nullType;
	this->nullType->name = "null";
	this->nullType->nullable = true;
	this->typeParam0->id = CiId::typeParam0;
	this->typeParam0->name = "T";
	this->intType->id = CiId::intType;
	this->intType->name = "int";
	this->longType->id = CiId::longType;
	this->longType->name = "long";
	this->floatType->id = CiId::floatType;
	this->floatType->name = "float";
	this->doubleType->id = CiId::doubleType;
	this->doubleType->name = "double";
	this->boolType->id = CiId::boolType;
	this->boolType->name = "bool";
	this->stringPtrType->id = CiId::stringPtrType;
	this->stringPtrType->name = "string";
	this->stringNullablePtrType->id = CiId::stringPtrType;
	this->stringNullablePtrType->name = "string";
	this->stringNullablePtrType->nullable = true;
	this->stringStorageType->id = CiId::stringStorageType;
	this->printableType->name = "printable";
	this->parent = nullptr;
	std::shared_ptr<CiSymbol> basePtr = CiVar::new_(nullptr, "base");
	basePtr->id = CiId::basePtr;
	add(basePtr);
	addMinMaxValue(this->intType.get(), -2147483648, 2147483647);
	this->intType->add(CiMethod::newMutator(CiVisibility::public_, this->boolType, CiId::intTryParse, "TryParse", CiVar::new_(this->stringPtrType, "value"), CiVar::new_(this->intType, "radix", newLiteralLong(0))));
	add(this->intType);
	this->uIntType->name = "uint";
	add(this->uIntType);
	addMinMaxValue(this->longType.get(), (-9223372036854775807 - 1), 9223372036854775807);
	this->longType->add(CiMethod::newMutator(CiVisibility::public_, this->boolType, CiId::longTryParse, "TryParse", CiVar::new_(this->stringPtrType, "value"), CiVar::new_(this->intType, "radix", newLiteralLong(0))));
	add(this->longType);
	this->byteType->name = "byte";
	add(this->byteType);
	std::shared_ptr<CiRangeType> shortType = CiRangeType::new_(-32768, 32767);
	shortType->name = "short";
	add(shortType);
	std::shared_ptr<CiRangeType> ushortType = CiRangeType::new_(0, 65535);
	ushortType->name = "ushort";
	add(ushortType);
	std::shared_ptr<CiRangeType> minus1Type = CiRangeType::new_(-1, 2147483647);
	add(this->floatType);
	this->doubleType->add(CiMethod::newMutator(CiVisibility::public_, this->boolType, CiId::doubleTryParse, "TryParse", CiVar::new_(this->stringPtrType, "value")));
	add(this->doubleType);
	add(this->boolType);
	this->stringClass->add(CiMethod::new_(CiVisibility::public_, this->boolType, CiId::stringContains, "Contains", CiVar::new_(this->stringPtrType, "value")));
	this->stringClass->add(CiMethod::new_(CiVisibility::public_, this->boolType, CiId::stringEndsWith, "EndsWith", CiVar::new_(this->stringPtrType, "value")));
	this->stringClass->add(CiMethod::new_(CiVisibility::public_, minus1Type, CiId::stringIndexOf, "IndexOf", CiVar::new_(this->stringPtrType, "value")));
	this->stringClass->add(CiMethod::new_(CiVisibility::public_, minus1Type, CiId::stringLastIndexOf, "LastIndexOf", CiVar::new_(this->stringPtrType, "value")));
	this->stringClass->add(CiProperty::new_(this->uIntType, CiId::stringLength, "Length"));
	this->stringClass->add(CiMethod::new_(CiVisibility::public_, this->stringStorageType, CiId::stringReplace, "Replace", CiVar::new_(this->stringPtrType, "oldValue"), CiVar::new_(this->stringPtrType, "newValue")));
	this->stringClass->add(CiMethod::new_(CiVisibility::public_, this->boolType, CiId::stringStartsWith, "StartsWith", CiVar::new_(this->stringPtrType, "value")));
	this->stringClass->add(CiMethod::new_(CiVisibility::public_, this->stringStorageType, CiId::stringSubstring, "Substring", CiVar::new_(this->intType, "offset"), CiVar::new_(this->intType, "length", newLiteralLong(-1))));
	this->stringPtrType->class_ = this->stringClass.get();
	add(this->stringPtrType);
	this->stringNullablePtrType->class_ = this->stringClass.get();
	this->stringStorageType->class_ = this->stringClass.get();
	std::shared_ptr<CiMethod> arrayBinarySearchPart = CiMethod::new_(CiVisibility::numericElementType, this->intType, CiId::arrayBinarySearchPart, "BinarySearch", CiVar::new_(this->typeParam0, "value"), CiVar::new_(this->intType, "startIndex"), CiVar::new_(this->intType, "count"));
	this->arrayPtrClass->add(arrayBinarySearchPart);
	std::shared_ptr<CiReadWriteClassType> citemp0 = std::make_shared<CiReadWriteClassType>();
	citemp0->class_ = this->arrayPtrClass.get();
	citemp0->typeArg0 = this->typeParam0;
	this->arrayPtrClass->add(CiMethod::new_(CiVisibility::public_, this->voidType, CiId::arrayCopyTo, "CopyTo", CiVar::new_(this->intType, "sourceIndex"), CiVar::new_(citemp0, "destinationArray"), CiVar::new_(this->intType, "destinationIndex"), CiVar::new_(this->intType, "count")));
	std::shared_ptr<CiMethod> arrayFillPart = CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::arrayFillPart, "Fill", CiVar::new_(this->typeParam0, "value"), CiVar::new_(this->intType, "startIndex"), CiVar::new_(this->intType, "count"));
	this->arrayPtrClass->add(arrayFillPart);
	std::shared_ptr<CiMethod> arraySortPart = CiMethod::newMutator(CiVisibility::numericElementType, this->voidType, CiId::arraySortPart, "Sort", CiVar::new_(this->intType, "startIndex"), CiVar::new_(this->intType, "count"));
	this->arrayPtrClass->add(arraySortPart);
	this->arrayStorageClass->parent = this->arrayPtrClass.get();
	this->arrayStorageClass->add(CiMethodGroup::new_(CiMethod::new_(CiVisibility::numericElementType, this->intType, CiId::arrayBinarySearchAll, "BinarySearch", CiVar::new_(this->typeParam0, "value")), arrayBinarySearchPart));
	this->arrayStorageClass->add(CiMethodGroup::new_(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::arrayFillAll, "Fill", CiVar::new_(this->typeParam0, "value")), arrayFillPart));
	this->arrayStorageClass->add(CiProperty::new_(this->uIntType, CiId::arrayLength, "Length"));
	this->arrayStorageClass->add(CiMethodGroup::new_(CiMethod::newMutator(CiVisibility::numericElementType, this->voidType, CiId::arraySortAll, "Sort"), arraySortPart));
	std::shared_ptr<CiType> typeParam0NotFinal = std::make_shared<CiType>();
	typeParam0NotFinal->id = CiId::typeParam0NotFinal;
	typeParam0NotFinal->name = "T";
	std::shared_ptr<CiType> typeParam0Predicate = std::make_shared<CiType>();
	typeParam0Predicate->id = CiId::typeParam0Predicate;
	typeParam0Predicate->name = "Predicate<T>";
	CiClass * listClass = addCollection(CiId::listClass, "List", 1, CiId::listClear, CiId::listCount);
	listClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::listAdd, "Add", CiVar::new_(typeParam0NotFinal, "value")));
	std::shared_ptr<CiClassType> citemp1 = std::make_shared<CiClassType>();
	citemp1->class_ = listClass;
	citemp1->typeArg0 = this->typeParam0;
	listClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::listAddRange, "AddRange", CiVar::new_(citemp1, "source")));
	listClass->add(CiMethod::new_(CiVisibility::public_, this->boolType, CiId::listAll, "All", CiVar::new_(typeParam0Predicate, "predicate")));
	listClass->add(CiMethod::new_(CiVisibility::public_, this->boolType, CiId::listAny, "Any", CiVar::new_(typeParam0Predicate, "predicate")));
	listClass->add(CiMethod::new_(CiVisibility::public_, this->boolType, CiId::listContains, "Contains", CiVar::new_(this->typeParam0, "value")));
	std::shared_ptr<CiReadWriteClassType> citemp2 = std::make_shared<CiReadWriteClassType>();
	citemp2->class_ = this->arrayPtrClass.get();
	citemp2->typeArg0 = this->typeParam0;
	listClass->add(CiMethod::new_(CiVisibility::public_, this->voidType, CiId::listCopyTo, "CopyTo", CiVar::new_(this->intType, "sourceIndex"), CiVar::new_(citemp2, "destinationArray"), CiVar::new_(this->intType, "destinationIndex"), CiVar::new_(this->intType, "count")));
	listClass->add(CiMethod::newMutator(CiVisibility::public_, this->intType, CiId::listIndexOf, "IndexOf", CiVar::new_(this->typeParam0, "value")));
	listClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::listInsert, "Insert", CiVar::new_(this->uIntType, "index"), CiVar::new_(typeParam0NotFinal, "value")));
	listClass->add(CiMethod::newMutator(CiVisibility::public_, this->typeParam0, CiId::listLast, "Last"));
	listClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::listRemoveAt, "RemoveAt", CiVar::new_(this->intType, "index")));
	listClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::listRemoveRange, "RemoveRange", CiVar::new_(this->intType, "index"), CiVar::new_(this->intType, "count")));
	listClass->add(CiMethodGroup::new_(CiMethod::newMutator(CiVisibility::numericElementType, this->voidType, CiId::listSortAll, "Sort"), CiMethod::newMutator(CiVisibility::numericElementType, this->voidType, CiId::listSortPart, "Sort", CiVar::new_(this->intType, "startIndex"), CiVar::new_(this->intType, "count"))));
	CiClass * queueClass = addCollection(CiId::queueClass, "Queue", 1, CiId::queueClear, CiId::queueCount);
	queueClass->add(CiMethod::newMutator(CiVisibility::public_, this->typeParam0, CiId::queueDequeue, "Dequeue"));
	queueClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::queueEnqueue, "Enqueue", CiVar::new_(this->typeParam0, "value")));
	queueClass->add(CiMethod::new_(CiVisibility::public_, this->typeParam0, CiId::queuePeek, "Peek"));
	CiClass * stackClass = addCollection(CiId::stackClass, "Stack", 1, CiId::stackClear, CiId::stackCount);
	stackClass->add(CiMethod::new_(CiVisibility::public_, this->typeParam0, CiId::stackPeek, "Peek"));
	stackClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::stackPush, "Push", CiVar::new_(this->typeParam0, "value")));
	stackClass->add(CiMethod::newMutator(CiVisibility::public_, this->typeParam0, CiId::stackPop, "Pop"));
	addSet(CiId::hashSetClass, "HashSet", CiId::hashSetAdd, CiId::hashSetClear, CiId::hashSetContains, CiId::hashSetCount, CiId::hashSetRemove);
	addSet(CiId::sortedSetClass, "SortedSet", CiId::sortedSetAdd, CiId::sortedSetClear, CiId::sortedSetContains, CiId::sortedSetCount, CiId::sortedSetRemove);
	addDictionary(CiId::dictionaryClass, "Dictionary", CiId::dictionaryClear, CiId::dictionaryContainsKey, CiId::dictionaryCount, CiId::dictionaryRemove);
	addDictionary(CiId::sortedDictionaryClass, "SortedDictionary", CiId::sortedDictionaryClear, CiId::sortedDictionaryContainsKey, CiId::sortedDictionaryCount, CiId::sortedDictionaryRemove);
	addDictionary(CiId::orderedDictionaryClass, "OrderedDictionary", CiId::orderedDictionaryClear, CiId::orderedDictionaryContainsKey, CiId::orderedDictionaryCount, CiId::orderedDictionaryRemove);
	std::shared_ptr<CiClass> textWriterClass = CiClass::new_(CiCallType::normal, CiId::textWriterClass, "TextWriter");
	textWriterClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::textWriterWrite, "Write", CiVar::new_(this->printableType, "value")));
	textWriterClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::textWriterWriteChar, "WriteChar", CiVar::new_(this->intType, "c")));
	textWriterClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::textWriterWriteCodePoint, "WriteCodePoint", CiVar::new_(this->intType, "c")));
	textWriterClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::textWriterWriteLine, "WriteLine", CiVar::new_(this->printableType, "value", newLiteralString(""))));
	add(textWriterClass);
	std::shared_ptr<CiClass> consoleClass = CiClass::new_(CiCallType::static_, CiId::none, "Console");
	consoleClass->add(CiMethod::newStatic(this->voidType, CiId::consoleWrite, "Write", CiVar::new_(this->printableType, "value")));
	consoleClass->add(CiMethod::newStatic(this->voidType, CiId::consoleWriteLine, "WriteLine", CiVar::new_(this->printableType, "value", newLiteralString(""))));
	std::shared_ptr<CiStorageType> citemp3 = std::make_shared<CiStorageType>();
	citemp3->class_ = textWriterClass.get();
	consoleClass->add(CiStaticProperty::new_(citemp3, CiId::consoleError, "Error"));
	add(consoleClass);
	std::shared_ptr<CiClass> stringWriterClass = CiClass::new_(CiCallType::sealed, CiId::stringWriterClass, "StringWriter");
	stringWriterClass->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, CiId::stringWriterClear, "Clear"));
	stringWriterClass->add(CiMethod::new_(CiVisibility::public_, this->stringPtrType, CiId::stringWriterToString, "ToString"));
	add(stringWriterClass);
	stringWriterClass->parent = textWriterClass.get();
	std::shared_ptr<CiClass> utf8EncodingClass = CiClass::new_(CiCallType::sealed, CiId::none, "UTF8Encoding");
	utf8EncodingClass->add(CiMethod::new_(CiVisibility::public_, this->intType, CiId::uTF8GetByteCount, "GetByteCount", CiVar::new_(this->stringPtrType, "str")));
	std::shared_ptr<CiReadWriteClassType> citemp4 = std::make_shared<CiReadWriteClassType>();
	citemp4->class_ = this->arrayPtrClass.get();
	citemp4->typeArg0 = this->byteType;
	utf8EncodingClass->add(CiMethod::new_(CiVisibility::public_, this->voidType, CiId::uTF8GetBytes, "GetBytes", CiVar::new_(this->stringPtrType, "str"), CiVar::new_(citemp4, "bytes"), CiVar::new_(this->intType, "byteIndex")));
	std::shared_ptr<CiClassType> citemp5 = std::make_shared<CiClassType>();
	citemp5->class_ = this->arrayPtrClass.get();
	citemp5->typeArg0 = this->byteType;
	utf8EncodingClass->add(CiMethod::new_(CiVisibility::public_, this->stringStorageType, CiId::uTF8GetString, "GetString", CiVar::new_(citemp5, "bytes"), CiVar::new_(this->intType, "offset"), CiVar::new_(this->intType, "length")));
	std::shared_ptr<CiClass> encodingClass = CiClass::new_(CiCallType::static_, CiId::none, "Encoding");
	encodingClass->add(CiStaticProperty::new_(utf8EncodingClass, CiId::none, "UTF8"));
	add(encodingClass);
	std::shared_ptr<CiClass> environmentClass = CiClass::new_(CiCallType::static_, CiId::none, "Environment");
	environmentClass->add(CiMethod::newStatic(this->stringNullablePtrType, CiId::environmentGetEnvironmentVariable, "GetEnvironmentVariable", CiVar::new_(this->stringPtrType, "name")));
	add(environmentClass);
	this->regexOptionsEnum = newEnum(true);
	this->regexOptionsEnum->isPublic = true;
	this->regexOptionsEnum->id = CiId::regexOptionsEnum;
	this->regexOptionsEnum->name = "RegexOptions";
	std::shared_ptr<CiConst> regexOptionsNone = newConstLong("None", 0);
	addEnumValue(this->regexOptionsEnum, regexOptionsNone);
	addEnumValue(this->regexOptionsEnum, newConstLong("IgnoreCase", 1));
	addEnumValue(this->regexOptionsEnum, newConstLong("Multiline", 2));
	addEnumValue(this->regexOptionsEnum, newConstLong("Singleline", 16));
	add(this->regexOptionsEnum);
	std::shared_ptr<CiClass> regexClass = CiClass::new_(CiCallType::sealed, CiId::regexClass, "Regex");
	regexClass->add(CiMethod::newStatic(this->stringStorageType, CiId::regexEscape, "Escape", CiVar::new_(this->stringPtrType, "str")));
	regexClass->add(CiMethodGroup::new_(CiMethod::newStatic(this->boolType, CiId::regexIsMatchStr, "IsMatch", CiVar::new_(this->stringPtrType, "input"), CiVar::new_(this->stringPtrType, "pattern"), CiVar::new_(this->regexOptionsEnum, "options", regexOptionsNone)), CiMethod::new_(CiVisibility::public_, this->boolType, CiId::regexIsMatchRegex, "IsMatch", CiVar::new_(this->stringPtrType, "input"))));
	std::shared_ptr<CiDynamicPtrType> citemp6 = std::make_shared<CiDynamicPtrType>();
	citemp6->class_ = regexClass.get();
	regexClass->add(CiMethod::newStatic(citemp6, CiId::regexCompile, "Compile", CiVar::new_(this->stringPtrType, "pattern"), CiVar::new_(this->regexOptionsEnum, "options", regexOptionsNone)));
	add(regexClass);
	std::shared_ptr<CiClass> matchClass = CiClass::new_(CiCallType::sealed, CiId::matchClass, "Match");
	std::shared_ptr<CiClassType> citemp7 = std::make_shared<CiClassType>();
	citemp7->class_ = regexClass.get();
	matchClass->add(CiMethodGroup::new_(CiMethod::newMutator(CiVisibility::public_, this->boolType, CiId::matchFindStr, "Find", CiVar::new_(this->stringPtrType, "input"), CiVar::new_(this->stringPtrType, "pattern"), CiVar::new_(this->regexOptionsEnum, "options", regexOptionsNone)), CiMethod::newMutator(CiVisibility::public_, this->boolType, CiId::matchFindRegex, "Find", CiVar::new_(this->stringPtrType, "input"), CiVar::new_(citemp7, "pattern"))));
	matchClass->add(CiProperty::new_(this->intType, CiId::matchStart, "Start"));
	matchClass->add(CiProperty::new_(this->intType, CiId::matchEnd, "End"));
	matchClass->add(CiMethod::new_(CiVisibility::public_, this->stringPtrType, CiId::matchGetCapture, "GetCapture", CiVar::new_(this->uIntType, "group")));
	matchClass->add(CiProperty::new_(this->uIntType, CiId::matchLength, "Length"));
	matchClass->add(CiProperty::new_(this->stringPtrType, CiId::matchValue, "Value"));
	add(matchClass);
	std::shared_ptr<CiFloatingType> floatIntType = std::make_shared<CiFloatingType>();
	floatIntType->id = CiId::floatIntType;
	floatIntType->name = "float";
	std::shared_ptr<CiClass> mathClass = CiClass::new_(CiCallType::static_, CiId::none, "Math");
	mathClass->add(CiMethodGroup::new_(CiMethod::newStatic(this->intType, CiId::mathAbs, "Abs", CiVar::new_(this->longType, "a")), CiMethod::newStatic(this->floatType, CiId::mathAbs, "Abs", CiVar::new_(this->doubleType, "a"))));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Acos", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Asin", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Atan", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Atan2", CiVar::new_(this->doubleType, "y"), CiVar::new_(this->doubleType, "x")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Cbrt", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(floatIntType, CiId::mathCeiling, "Ceiling", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethodGroup::new_(CiMethod::newStatic(this->intType, CiId::mathClamp, "Clamp", CiVar::new_(this->longType, "value"), CiVar::new_(this->longType, "min"), CiVar::new_(this->longType, "max")), CiMethod::newStatic(this->floatType, CiId::mathClamp, "Clamp", CiVar::new_(this->doubleType, "value"), CiVar::new_(this->doubleType, "min"), CiVar::new_(this->doubleType, "max"))));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Cos", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Cosh", CiVar::new_(this->doubleType, "a")));
	mathClass->add(newConstDouble("E", 2.718281828459045));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Exp", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(floatIntType, CiId::mathMethod, "Floor", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathFusedMultiplyAdd, "FusedMultiplyAdd", CiVar::new_(this->doubleType, "x"), CiVar::new_(this->doubleType, "y"), CiVar::new_(this->doubleType, "z")));
	mathClass->add(CiMethod::newStatic(this->boolType, CiId::mathIsFinite, "IsFinite", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->boolType, CiId::mathIsInfinity, "IsInfinity", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->boolType, CiId::mathIsNaN, "IsNaN", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Log", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathLog2, "Log2", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Log10", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethodGroup::new_(CiMethod::newStatic(this->intType, CiId::mathMaxInt, "Max", CiVar::new_(this->longType, "a"), CiVar::new_(this->longType, "b")), CiMethod::newStatic(this->floatType, CiId::mathMaxDouble, "Max", CiVar::new_(this->doubleType, "a"), CiVar::new_(this->doubleType, "b"))));
	mathClass->add(CiMethodGroup::new_(CiMethod::newStatic(this->intType, CiId::mathMinInt, "Min", CiVar::new_(this->longType, "a"), CiVar::new_(this->longType, "b")), CiMethod::newStatic(this->floatType, CiId::mathMinDouble, "Min", CiVar::new_(this->doubleType, "a"), CiVar::new_(this->doubleType, "b"))));
	mathClass->add(CiStaticProperty::new_(this->floatType, CiId::mathNaN, "NaN"));
	mathClass->add(CiStaticProperty::new_(this->floatType, CiId::mathNegativeInfinity, "NegativeInfinity"));
	mathClass->add(newConstDouble("PI", 3.141592653589793));
	mathClass->add(CiStaticProperty::new_(this->floatType, CiId::mathPositiveInfinity, "PositiveInfinity"));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Pow", CiVar::new_(this->doubleType, "x"), CiVar::new_(this->doubleType, "y")));
	mathClass->add(CiMethod::newStatic(floatIntType, CiId::mathRound, "Round", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Sin", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Sinh", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Sqrt", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Tan", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(this->floatType, CiId::mathMethod, "Tanh", CiVar::new_(this->doubleType, "a")));
	mathClass->add(CiMethod::newStatic(floatIntType, CiId::mathTruncate, "Truncate", CiVar::new_(this->doubleType, "a")));
	add(mathClass);
	std::shared_ptr<CiClass> lockClass = CiClass::new_(CiCallType::sealed, CiId::lockClass, "Lock");
	add(lockClass);
	this->lockPtrType->class_ = lockClass.get();
}

std::shared_ptr<CiLiteralLong> CiSystem::newLiteralLong(int64_t value, int line) const
{
	std::shared_ptr<CiType> type = value >= -2147483648 && value <= 2147483647 ? CiRangeType::new_(static_cast<int>(value), static_cast<int>(value)) : this->longType;
	std::shared_ptr<CiLiteralLong> citemp0 = std::make_shared<CiLiteralLong>();
	citemp0->line = line;
	citemp0->type = type;
	citemp0->value = value;
	return citemp0;
}

std::shared_ptr<CiLiteralString> CiSystem::newLiteralString(std::string_view value, int line) const
{
	std::shared_ptr<CiLiteralString> citemp0 = std::make_shared<CiLiteralString>();
	citemp0->line = line;
	citemp0->type = this->stringPtrType;
	citemp0->value = value;
	return citemp0;
}

std::shared_ptr<CiType> CiSystem::promoteIntegerTypes(const CiType * left, const CiType * right) const
{
	return left == this->longType.get() || right == this->longType.get() ? this->longType : this->intType;
}

std::shared_ptr<CiType> CiSystem::promoteFloatingTypes(const CiType * left, const CiType * right) const
{
	if (left->id == CiId::doubleType || right->id == CiId::doubleType)
		return this->doubleType;
	if (left->id == CiId::floatType || right->id == CiId::floatType || left->id == CiId::floatIntType || right->id == CiId::floatIntType)
		return this->floatType;
	return nullptr;
}

std::shared_ptr<CiType> CiSystem::promoteNumericTypes(std::shared_ptr<CiType> left, std::shared_ptr<CiType> right) const
{
	std::shared_ptr<CiType> result = promoteFloatingTypes(left.get(), right.get());
	return result != nullptr ? result : promoteIntegerTypes(left.get(), right.get());
}

std::shared_ptr<CiEnum> CiSystem::newEnum(bool flags) const
{
	std::shared_ptr<CiEnum> enu = flags ? std::make_shared<CiEnumFlags>() : std::make_shared<CiEnum>();
	enu->add(CiMethod::newStatic(enu, CiId::enumFromInt, "FromInt", CiVar::new_(this->intType, "value")));
	if (flags)
		enu->add(CiMethod::new_(CiVisibility::public_, this->boolType, CiId::enumHasFlag, "HasFlag", CiVar::new_(enu, "flag")));
	return enu;
}

CiClass * CiSystem::addCollection(CiId id, std::string_view name, int typeParameterCount, CiId clearId, CiId countId)
{
	std::shared_ptr<CiClass> result = CiClass::new_(CiCallType::normal, id, name, typeParameterCount);
	result->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, clearId, "Clear"));
	result->add(CiProperty::new_(this->uIntType, countId, "Count"));
	add(result);
	return result.get();
}

void CiSystem::addSet(CiId id, std::string_view name, CiId addId, CiId clearId, CiId containsId, CiId countId, CiId removeId)
{
	CiClass * set = addCollection(id, name, 1, clearId, countId);
	set->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, addId, "Add", CiVar::new_(this->typeParam0, "value")));
	set->add(CiMethod::new_(CiVisibility::public_, this->boolType, containsId, "Contains", CiVar::new_(this->typeParam0, "value")));
	set->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, removeId, "Remove", CiVar::new_(this->typeParam0, "value")));
}

void CiSystem::addDictionary(CiId id, std::string_view name, CiId clearId, CiId containsKeyId, CiId countId, CiId removeId)
{
	CiClass * dict = addCollection(id, name, 2, clearId, countId);
	dict->add(CiMethod::newMutator(CiVisibility::finalValueType, this->voidType, CiId::dictionaryAdd, "Add", CiVar::new_(this->typeParam0, "key")));
	dict->add(CiMethod::new_(CiVisibility::public_, this->boolType, containsKeyId, "ContainsKey", CiVar::new_(this->typeParam0, "key")));
	dict->add(CiMethod::newMutator(CiVisibility::public_, this->voidType, removeId, "Remove", CiVar::new_(this->typeParam0, "key")));
}

void CiSystem::addEnumValue(std::shared_ptr<CiEnum> enu, std::shared_ptr<CiConst> value)
{
	value->type = enu;
	enu->add(value);
}

std::shared_ptr<CiConst> CiSystem::newConstLong(std::string_view name, int64_t value) const
{
	std::shared_ptr<CiConst> result = std::make_shared<CiConst>();
	result->visibility = CiVisibility::public_;
	result->name = name;
	result->value = newLiteralLong(value);
	result->visitStatus = CiVisitStatus::done;
	result->type = result->value->type;
	return result;
}

std::shared_ptr<CiConst> CiSystem::newConstDouble(std::string_view name, double value) const
{
	std::shared_ptr<CiLiteralDouble> citemp0 = std::make_shared<CiLiteralDouble>();
	citemp0->value = value;
	citemp0->type = this->doubleType;
	std::shared_ptr<CiConst> citemp1 = std::make_shared<CiConst>();
	citemp1->visibility = CiVisibility::public_;
	citemp1->name = name;
	citemp1->value = citemp0;
	citemp1->type = this->doubleType;
	citemp1->visitStatus = CiVisitStatus::done;
	return citemp1;
}

void CiSystem::addMinMaxValue(CiIntegerType * target, int64_t min, int64_t max) const
{
	target->add(newConstLong("MinValue", min));
	target->add(newConstLong("MaxValue", max));
}

std::shared_ptr<CiSystem> CiSystem::new_()
{
	return std::make_shared<CiSystem>();
}

bool CiParser::docParseLine(CiDocPara * para)
{
	if (para->children.size() > 0)
		para->children.push_back(std::make_shared<CiDocLine>());
	this->lexemeOffset = this->charOffset;
	for (int lastNonWhitespace = 0;;) {
		switch (peekChar()) {
		case -1:
		case '\n':
		case '\r':
			{
				std::shared_ptr<CiDocText> citemp0 = std::make_shared<CiDocText>();
				citemp0->text = getLexeme();
				para->children.push_back(citemp0);
				return lastNonWhitespace == '.';
			}
		case '\t':
		case ' ':
			readChar();
			break;
		case '`':
			if (this->charOffset > this->lexemeOffset) {
				std::shared_ptr<CiDocText> citemp1 = std::make_shared<CiDocText>();
				citemp1->text = getLexeme();
				para->children.push_back(citemp1);
			}
			readChar();
			this->lexemeOffset = this->charOffset;
			for (;;) {
				int c = peekChar();
				if (c == '`') {
					std::shared_ptr<CiDocCode> citemp2 = std::make_shared<CiDocCode>();
					citemp2->text = getLexeme();
					para->children.push_back(citemp2);
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

void CiParser::docParsePara(CiDocPara * para)
{
	do {
		docParseLine(para);
		nextToken();
	}
	while (see(CiToken::docRegular));
}

std::shared_ptr<CiCodeDoc> CiParser::parseDoc()
{
	if (!see(CiToken::docRegular))
		return nullptr;
	std::shared_ptr<CiCodeDoc> doc = std::make_shared<CiCodeDoc>();
	bool period;
	do {
		period = docParseLine(&doc->summary);
		nextToken();
	}
	while (!period && see(CiToken::docRegular));
	for (;;) {
		switch (this->currentToken) {
		case CiToken::docRegular:
			{
				std::shared_ptr<CiDocPara> para = std::make_shared<CiDocPara>();
				docParsePara(para.get());
				doc->details.push_back(para);
				break;
			}
		case CiToken::docBullet:
			{
				std::shared_ptr<CiDocList> list = std::make_shared<CiDocList>();
				do {
					list->items.emplace_back();
					docParsePara(&static_cast<CiDocPara &>(list->items.back()));
				}
				while (see(CiToken::docBullet));
				doc->details.push_back(list);
				break;
			}
		case CiToken::docBlank:
			nextToken();
			break;
		default:
			return doc;
		}
	}
}

void CiParser::checkXcrementParent()
{
	if (this->xcrementParent.data() != nullptr) {
		std::string_view op = see(CiToken::increment) ? "++" : "--";
		reportError(std::format("{} not allowed on the right side of {}", op, this->xcrementParent));
	}
}

std::shared_ptr<CiLiteralDouble> CiParser::parseDouble()
{
	double d;
	if (![&] { char *ciend; d = std::strtod(CiString_replace(getLexeme(), "_", "").data(), &ciend); return *ciend == '\0'; }())
		reportError("Invalid floating-point number");
	std::shared_ptr<CiLiteralDouble> result = std::make_shared<CiLiteralDouble>();
	result->line = this->line;
	result->type = this->program->system->doubleType;
	result->value = d;
	nextToken();
	return result;
}

bool CiParser::seeDigit() const
{
	int c = peekChar();
	return c >= '0' && c <= '9';
}

std::shared_ptr<CiInterpolatedString> CiParser::parseInterpolatedString()
{
	std::shared_ptr<CiInterpolatedString> result = std::make_shared<CiInterpolatedString>();
	result->line = this->line;
	do {
		std::string prefix{CiString_replace(this->stringValue, "{{", "{")};
		nextToken();
		std::shared_ptr<CiExpr> arg = parseExpr();
		std::shared_ptr<CiExpr> width = eat(CiToken::comma) ? parseExpr() : nullptr;
		int format = ' ';
		int precision = -1;
		if (see(CiToken::colon)) {
			format = readChar();
			if (seeDigit()) {
				precision = readChar() - '0';
				if (seeDigit())
					precision = precision * 10 + readChar() - '0';
			}
			nextToken();
		}
		result->addPart(prefix, arg, width, format, precision);
		check(CiToken::rightBrace);
	}
	while (readString(true) == CiToken::interpolatedString);
	result->suffix = CiString_replace(this->stringValue, "{{", "{");
	nextToken();
	return result;
}

std::shared_ptr<CiExpr> CiParser::parseParenthesized()
{
	expect(CiToken::leftParenthesis);
	std::shared_ptr<CiExpr> result = parseExpr();
	expect(CiToken::rightParenthesis);
	return result;
}

std::shared_ptr<CiSymbolReference> CiParser::parseSymbolReference(std::shared_ptr<CiExpr> left)
{
	std::shared_ptr<CiSymbolReference> result = std::make_shared<CiSymbolReference>();
	result->line = this->line;
	result->left = left;
	result->name = this->stringValue;
	nextToken();
	return result;
}

void CiParser::parseCollection(std::vector<std::shared_ptr<CiExpr>> * result, CiToken closing)
{
	if (!see(closing)) {
		do
			result->push_back(parseExpr());
		while (eat(CiToken::comma));
	}
	expectOrSkip(closing);
}

std::shared_ptr<CiExpr> CiParser::parsePrimaryExpr(bool type)
{
	std::shared_ptr<CiExpr> result;
	switch (this->currentToken) {
	case CiToken::increment:
	case CiToken::decrement:
		checkXcrementParent();
		{
			std::shared_ptr<CiPrefixExpr> citemp0 = std::make_shared<CiPrefixExpr>();
			citemp0->line = this->line;
			citemp0->op = nextToken();
			citemp0->inner = parsePrimaryExpr(false);
			return citemp0;
		}
	case CiToken::minus:
	case CiToken::tilde:
	case CiToken::exclamationMark:
		{
			std::shared_ptr<CiPrefixExpr> citemp1 = std::make_shared<CiPrefixExpr>();
			citemp1->line = this->line;
			citemp1->op = nextToken();
			citemp1->inner = parsePrimaryExpr(false);
			return citemp1;
		}
	case CiToken::new_:
		{
			std::shared_ptr<CiPrefixExpr> newResult = std::make_shared<CiPrefixExpr>();
			newResult->line = this->line;
			newResult->op = nextToken();
			result = parseType();
			if (eat(CiToken::leftBrace)) {
				std::shared_ptr<CiBinaryExpr> citemp2 = std::make_shared<CiBinaryExpr>();
				citemp2->line = this->line;
				citemp2->left = result;
				citemp2->op = CiToken::leftBrace;
				citemp2->right = parseObjectLiteral();
				result = citemp2;
			}
			newResult->inner = result;
			return newResult;
		}
	case CiToken::literalLong:
		result = this->program->system->newLiteralLong(this->longValue, this->line);
		nextToken();
		break;
	case CiToken::literalDouble:
		result = parseDouble();
		break;
	case CiToken::literalChar:
		result = CiLiteralChar::new_(static_cast<int>(this->longValue), this->line);
		nextToken();
		break;
	case CiToken::literalString:
		result = this->program->system->newLiteralString(this->stringValue, this->line);
		nextToken();
		break;
	case CiToken::false_:
		{
			std::shared_ptr<CiLiteralFalse> citemp3 = std::make_shared<CiLiteralFalse>();
			citemp3->line = this->line;
			citemp3->type = this->program->system->boolType;
			result = citemp3;
			nextToken();
			break;
		}
	case CiToken::true_:
		{
			std::shared_ptr<CiLiteralTrue> citemp4 = std::make_shared<CiLiteralTrue>();
			citemp4->line = this->line;
			citemp4->type = this->program->system->boolType;
			result = citemp4;
			nextToken();
			break;
		}
	case CiToken::null:
		{
			std::shared_ptr<CiLiteralNull> citemp5 = std::make_shared<CiLiteralNull>();
			citemp5->line = this->line;
			citemp5->type = this->program->system->nullType;
			result = citemp5;
			nextToken();
			break;
		}
	case CiToken::interpolatedString:
		result = parseInterpolatedString();
		break;
	case CiToken::leftParenthesis:
		result = parseParenthesized();
		break;
	case CiToken::id:
		{
			std::shared_ptr<CiSymbolReference> symbol = parseSymbolReference(nullptr);
			if (eat(CiToken::fatArrow)) {
				std::shared_ptr<CiLambdaExpr> lambda = std::make_shared<CiLambdaExpr>();
				lambda->line = symbol->line;
				lambda->add(CiVar::new_(nullptr, symbol->name));
				lambda->body = parseExpr();
				return lambda;
			}
			if (type && eat(CiToken::less)) {
				std::shared_ptr<CiAggregateInitializer> typeArgs = std::make_shared<CiAggregateInitializer>();
				bool saveTypeArg = this->parsingTypeArg;
				this->parsingTypeArg = true;
				do
					typeArgs->items.push_back(parseType());
				while (eat(CiToken::comma));
				expect(CiToken::rightAngle);
				this->parsingTypeArg = saveTypeArg;
				symbol->left = typeArgs;
			}
			result = symbol;
			break;
		}
	case CiToken::resource:
		nextToken();
		if (eat(CiToken::less) && this->stringValue == "byte" && eat(CiToken::id) && eat(CiToken::leftBracket) && eat(CiToken::rightBracket) && eat(CiToken::greater)) {
			std::shared_ptr<CiPrefixExpr> citemp6 = std::make_shared<CiPrefixExpr>();
			citemp6->line = this->line;
			citemp6->op = CiToken::resource;
			citemp6->inner = parseParenthesized();
			result = citemp6;
		}
		else {
			reportError("Expected 'resource<byte[]>'");
			result = nullptr;
		}
		break;
	default:
		reportError("Invalid expression");
		result = nullptr;
		break;
	}
	for (;;) {
		switch (this->currentToken) {
		case CiToken::dot:
			nextToken();
			result = parseSymbolReference(result);
			break;
		case CiToken::leftParenthesis:
			nextToken();
			if (std::shared_ptr<CiSymbolReference>method = std::dynamic_pointer_cast<CiSymbolReference>(result)) {
				std::shared_ptr<CiCallExpr> call = std::make_shared<CiCallExpr>();
				call->line = this->line;
				call->method = method;
				parseCollection(&call->arguments, CiToken::rightParenthesis);
				result = call;
			}
			else
				reportError("Expected a method");
			break;
		case CiToken::leftBracket:
			{
				std::shared_ptr<CiBinaryExpr> citemp7 = std::make_shared<CiBinaryExpr>();
				citemp7->line = this->line;
				citemp7->left = result;
				citemp7->op = nextToken();
				citemp7->right = see(CiToken::rightBracket) ? nullptr : parseExpr();
				result = citemp7;
				expect(CiToken::rightBracket);
				break;
			}
		case CiToken::increment:
		case CiToken::decrement:
			checkXcrementParent();
			{
				std::shared_ptr<CiPostfixExpr> citemp8 = std::make_shared<CiPostfixExpr>();
				citemp8->line = this->line;
				citemp8->inner = result;
				citemp8->op = nextToken();
				result = citemp8;
				break;
			}
		case CiToken::exclamationMark:
		case CiToken::hash:
			{
				std::shared_ptr<CiPostfixExpr> citemp9 = std::make_shared<CiPostfixExpr>();
				citemp9->line = this->line;
				citemp9->inner = result;
				citemp9->op = nextToken();
				result = citemp9;
				break;
			}
		case CiToken::questionMark:
			if (!type)
				return result;
			{
				std::shared_ptr<CiPostfixExpr> citemp10 = std::make_shared<CiPostfixExpr>();
				citemp10->line = this->line;
				citemp10->inner = result;
				citemp10->op = nextToken();
				result = citemp10;
				break;
			}
		default:
			return result;
		}
	}
}

std::shared_ptr<CiExpr> CiParser::parseMulExpr()
{
	std::shared_ptr<CiExpr> left = parsePrimaryExpr(false);
	for (;;) {
		switch (this->currentToken) {
		case CiToken::asterisk:
		case CiToken::slash:
		case CiToken::mod:
			{
				std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
				citemp0->line = this->line;
				citemp0->left = left;
				citemp0->op = nextToken();
				citemp0->right = parsePrimaryExpr(false);
				left = citemp0;
				break;
			}
		default:
			return left;
		}
	}
}

std::shared_ptr<CiExpr> CiParser::parseAddExpr()
{
	std::shared_ptr<CiExpr> left = parseMulExpr();
	while (see(CiToken::plus) || see(CiToken::minus)) {
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = nextToken();
		citemp0->right = parseMulExpr();
		left = citemp0;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseShiftExpr()
{
	std::shared_ptr<CiExpr> left = parseAddExpr();
	while (see(CiToken::shiftLeft) || see(CiToken::shiftRight)) {
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = nextToken();
		citemp0->right = parseAddExpr();
		left = citemp0;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseRelExpr()
{
	std::shared_ptr<CiExpr> left = parseShiftExpr();
	for (;;) {
		switch (this->currentToken) {
		case CiToken::less:
		case CiToken::lessOrEqual:
		case CiToken::greater:
		case CiToken::greaterOrEqual:
			{
				std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
				citemp0->line = this->line;
				citemp0->left = left;
				citemp0->op = nextToken();
				citemp0->right = parseShiftExpr();
				left = citemp0;
				break;
			}
		case CiToken::is:
			{
				std::shared_ptr<CiBinaryExpr> isExpr = std::make_shared<CiBinaryExpr>();
				isExpr->line = this->line;
				isExpr->left = left;
				isExpr->op = nextToken();
				isExpr->right = parsePrimaryExpr(false);
				if (see(CiToken::id)) {
					std::shared_ptr<CiVar> citemp1 = std::make_shared<CiVar>();
					citemp1->line = this->line;
					citemp1->typeExpr = isExpr->right;
					citemp1->name = this->stringValue;
					isExpr->right = citemp1;
					nextToken();
				}
				return isExpr;
			}
		default:
			return left;
		}
	}
}

std::shared_ptr<CiExpr> CiParser::parseEqualityExpr()
{
	std::shared_ptr<CiExpr> left = parseRelExpr();
	while (see(CiToken::equal) || see(CiToken::notEqual)) {
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = nextToken();
		citemp0->right = parseRelExpr();
		left = citemp0;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseAndExpr()
{
	std::shared_ptr<CiExpr> left = parseEqualityExpr();
	while (see(CiToken::and_)) {
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = nextToken();
		citemp0->right = parseEqualityExpr();
		left = citemp0;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseXorExpr()
{
	std::shared_ptr<CiExpr> left = parseAndExpr();
	while (see(CiToken::xor_)) {
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = nextToken();
		citemp0->right = parseAndExpr();
		left = citemp0;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseOrExpr()
{
	std::shared_ptr<CiExpr> left = parseXorExpr();
	while (see(CiToken::or_)) {
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = nextToken();
		citemp0->right = parseXorExpr();
		left = citemp0;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseCondAndExpr()
{
	std::shared_ptr<CiExpr> left = parseOrExpr();
	while (see(CiToken::condAnd)) {
		std::string_view saveXcrementParent = this->xcrementParent;
		this->xcrementParent = "&&";
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = nextToken();
		citemp0->right = parseOrExpr();
		left = citemp0;
		this->xcrementParent = saveXcrementParent;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseCondOrExpr()
{
	std::shared_ptr<CiExpr> left = parseCondAndExpr();
	while (see(CiToken::condOr)) {
		std::string_view saveXcrementParent = this->xcrementParent;
		this->xcrementParent = "||";
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = nextToken();
		citemp0->right = parseCondAndExpr();
		left = citemp0;
		this->xcrementParent = saveXcrementParent;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseExpr()
{
	std::shared_ptr<CiExpr> left = parseCondOrExpr();
	if (see(CiToken::questionMark)) {
		std::shared_ptr<CiSelectExpr> result = std::make_shared<CiSelectExpr>();
		result->line = this->line;
		result->cond = left;
		nextToken();
		std::string_view saveXcrementParent = this->xcrementParent;
		this->xcrementParent = "?";
		result->onTrue = parseExpr();
		expect(CiToken::colon);
		result->onFalse = parseExpr();
		this->xcrementParent = saveXcrementParent;
		return result;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseType()
{
	std::shared_ptr<CiExpr> left = parsePrimaryExpr(true);
	if (eat(CiToken::range)) {
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = this->line;
		citemp0->left = left;
		citemp0->op = CiToken::range;
		citemp0->right = parsePrimaryExpr(true);
		return citemp0;
	}
	return left;
}

std::shared_ptr<CiExpr> CiParser::parseConstInitializer()
{
	if (eat(CiToken::leftBrace)) {
		std::shared_ptr<CiAggregateInitializer> result = std::make_shared<CiAggregateInitializer>();
		result->line = this->line;
		parseCollection(&result->items, CiToken::rightBrace);
		return result;
	}
	return parseExpr();
}

std::shared_ptr<CiAggregateInitializer> CiParser::parseObjectLiteral()
{
	std::shared_ptr<CiAggregateInitializer> result = std::make_shared<CiAggregateInitializer>();
	result->line = this->line;
	do {
		int line = this->line;
		std::shared_ptr<CiExpr> field = parseSymbolReference(nullptr);
		expect(CiToken::assign);
		std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
		citemp0->line = line;
		citemp0->left = field;
		citemp0->op = CiToken::assign;
		citemp0->right = parseExpr();
		result->items.push_back(citemp0);
	}
	while (eat(CiToken::comma));
	expect(CiToken::rightBrace);
	return result;
}

std::shared_ptr<CiExpr> CiParser::parseInitializer()
{
	if (!eat(CiToken::assign))
		return nullptr;
	if (eat(CiToken::leftBrace))
		return parseObjectLiteral();
	return parseExpr();
}

void CiParser::addSymbol(CiScope * scope, std::shared_ptr<CiSymbol> symbol)
{
	if (scope->contains(symbol.get()))
		reportError("Duplicate symbol");
	else
		scope->add(symbol);
}

std::shared_ptr<CiVar> CiParser::parseVar(std::shared_ptr<CiExpr> type)
{
	std::shared_ptr<CiVar> result = std::make_shared<CiVar>();
	result->line = this->line;
	result->typeExpr = type;
	result->name = this->stringValue;
	nextToken();
	result->value = parseInitializer();
	return result;
}

std::shared_ptr<CiConst> CiParser::parseConst()
{
	expect(CiToken::const_);
	std::shared_ptr<CiConst> konst = std::make_shared<CiConst>();
	konst->line = this->line;
	konst->typeExpr = parseType();
	konst->name = this->stringValue;
	nextToken();
	expect(CiToken::assign);
	konst->value = parseConstInitializer();
	expect(CiToken::semicolon);
	return konst;
}

std::shared_ptr<CiExpr> CiParser::parseAssign(bool allowVar)
{
	std::shared_ptr<CiExpr> left = allowVar ? parseType() : parseExpr();
	switch (this->currentToken) {
	case CiToken::assign:
	case CiToken::addAssign:
	case CiToken::subAssign:
	case CiToken::mulAssign:
	case CiToken::divAssign:
	case CiToken::modAssign:
	case CiToken::andAssign:
	case CiToken::orAssign:
	case CiToken::xorAssign:
	case CiToken::shiftLeftAssign:
	case CiToken::shiftRightAssign:
		{
			std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
			citemp0->line = this->line;
			citemp0->left = left;
			citemp0->op = nextToken();
			citemp0->right = parseAssign(false);
			return citemp0;
		}
	case CiToken::id:
		if (allowVar)
			return parseVar(left);
		return left;
	default:
		return left;
	}
}

std::shared_ptr<CiBlock> CiParser::parseBlock()
{
	std::shared_ptr<CiBlock> result = std::make_shared<CiBlock>();
	result->line = this->line;
	expect(CiToken::leftBrace);
	while (!see(CiToken::rightBrace) && !see(CiToken::endOfFile))
		result->statements.push_back(parseStatement());
	expect(CiToken::rightBrace);
	return result;
}

std::shared_ptr<CiAssert> CiParser::parseAssert()
{
	std::shared_ptr<CiAssert> result = std::make_shared<CiAssert>();
	result->line = this->line;
	expect(CiToken::assert);
	result->cond = parseExpr();
	if (eat(CiToken::comma))
		result->message = parseExpr();
	expect(CiToken::semicolon);
	return result;
}

std::shared_ptr<CiBreak> CiParser::parseBreak()
{
	if (this->currentLoopOrSwitch == nullptr)
		reportError("break outside loop or switch");
	std::shared_ptr<CiBreak> result = std::make_shared<CiBreak>();
	result->line = this->line;
	result->loopOrSwitch = this->currentLoopOrSwitch;
	expect(CiToken::break_);
	expect(CiToken::semicolon);
	if (CiLoop *loop = dynamic_cast<CiLoop *>(this->currentLoopOrSwitch))
		loop->hasBreak = true;
	return result;
}

std::shared_ptr<CiContinue> CiParser::parseContinue()
{
	if (this->currentLoop == nullptr)
		reportError("continue outside loop");
	std::shared_ptr<CiContinue> result = std::make_shared<CiContinue>();
	result->line = this->line;
	result->loop = this->currentLoop;
	expect(CiToken::continue_);
	expect(CiToken::semicolon);
	return result;
}

void CiParser::parseLoopBody(CiLoop * loop)
{
	const CiLoop * outerLoop = this->currentLoop;
	CiCondCompletionStatement * outerLoopOrSwitch = this->currentLoopOrSwitch;
	this->currentLoop = loop;
	this->currentLoopOrSwitch = loop;
	loop->body = parseStatement();
	this->currentLoopOrSwitch = outerLoopOrSwitch;
	this->currentLoop = outerLoop;
}

std::shared_ptr<CiDoWhile> CiParser::parseDoWhile()
{
	std::shared_ptr<CiDoWhile> result = std::make_shared<CiDoWhile>();
	result->line = this->line;
	expect(CiToken::do_);
	parseLoopBody(result.get());
	expect(CiToken::while_);
	result->cond = parseParenthesized();
	expect(CiToken::semicolon);
	return result;
}

std::shared_ptr<CiFor> CiParser::parseFor()
{
	std::shared_ptr<CiFor> result = std::make_shared<CiFor>();
	result->line = this->line;
	expect(CiToken::for_);
	expect(CiToken::leftParenthesis);
	if (!see(CiToken::semicolon))
		result->init = parseAssign(true);
	expect(CiToken::semicolon);
	if (!see(CiToken::semicolon))
		result->cond = parseExpr();
	expect(CiToken::semicolon);
	if (!see(CiToken::rightParenthesis))
		result->advance = parseAssign(false);
	expect(CiToken::rightParenthesis);
	parseLoopBody(result.get());
	return result;
}

void CiParser::parseForeachIterator(CiForeach * result)
{
	std::shared_ptr<CiVar> citemp0 = std::make_shared<CiVar>();
	citemp0->line = this->line;
	citemp0->typeExpr = parseType();
	citemp0->name = this->stringValue;
	addSymbol(result, citemp0);
	nextToken();
}

std::shared_ptr<CiForeach> CiParser::parseForeach()
{
	std::shared_ptr<CiForeach> result = std::make_shared<CiForeach>();
	result->line = this->line;
	expect(CiToken::foreach);
	expect(CiToken::leftParenthesis);
	if (eat(CiToken::leftParenthesis)) {
		parseForeachIterator(result.get());
		expect(CiToken::comma);
		parseForeachIterator(result.get());
		expect(CiToken::rightParenthesis);
	}
	else
		parseForeachIterator(result.get());
	expect(CiToken::in);
	result->collection = parseExpr();
	expect(CiToken::rightParenthesis);
	parseLoopBody(result.get());
	return result;
}

std::shared_ptr<CiIf> CiParser::parseIf()
{
	std::shared_ptr<CiIf> result = std::make_shared<CiIf>();
	result->line = this->line;
	expect(CiToken::if_);
	result->cond = parseParenthesized();
	result->onTrue = parseStatement();
	if (eat(CiToken::else_))
		result->onFalse = parseStatement();
	return result;
}

std::shared_ptr<CiLock> CiParser::parseLock()
{
	std::shared_ptr<CiLock> result = std::make_shared<CiLock>();
	result->line = this->line;
	expect(CiToken::lock_);
	result->lock = parseParenthesized();
	result->body = parseStatement();
	return result;
}

std::shared_ptr<CiNative> CiParser::parseNative()
{
	std::shared_ptr<CiNative> result = std::make_shared<CiNative>();
	result->line = this->line;
	expect(CiToken::native);
	if (see(CiToken::literalString))
		result->content = this->stringValue;
	else {
		int offset = this->charOffset;
		expect(CiToken::leftBrace);
		int nesting = 1;
		for (;;) {
			if (see(CiToken::endOfFile)) {
				expect(CiToken::rightBrace);
				return result;
			}
			if (see(CiToken::leftBrace))
				nesting++;
			else if (see(CiToken::rightBrace)) {
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

std::shared_ptr<CiReturn> CiParser::parseReturn()
{
	std::shared_ptr<CiReturn> result = std::make_shared<CiReturn>();
	result->line = this->line;
	nextToken();
	if (!see(CiToken::semicolon))
		result->value = parseExpr();
	expect(CiToken::semicolon);
	return result;
}

std::shared_ptr<CiSwitch> CiParser::parseSwitch()
{
	std::shared_ptr<CiSwitch> result = std::make_shared<CiSwitch>();
	result->line = this->line;
	expect(CiToken::switch_);
	result->value = parseParenthesized();
	expect(CiToken::leftBrace);
	CiCondCompletionStatement * outerLoopOrSwitch = this->currentLoopOrSwitch;
	this->currentLoopOrSwitch = result.get();
	while (eat(CiToken::case_)) {
		result->cases.emplace_back();
		CiCase * kase = &static_cast<CiCase &>(result->cases.back());
		do {
			std::shared_ptr<CiExpr> expr = parseExpr();
			if (see(CiToken::id))
				expr = parseVar(expr);
			if (eat(CiToken::when)) {
				std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
				citemp0->line = this->line;
				citemp0->left = expr;
				citemp0->op = CiToken::when;
				citemp0->right = parseExpr();
				expr = citemp0;
			}
			kase->values.push_back(expr);
			expect(CiToken::colon);
		}
		while (eat(CiToken::case_));
		if (see(CiToken::default_)) {
			reportError("Please remove 'case' before 'default'");
			break;
		}
		while (!see(CiToken::endOfFile)) {
			kase->body.push_back(parseStatement());
			switch (this->currentToken) {
			case CiToken::case_:
			case CiToken::default_:
			case CiToken::rightBrace:
				break;
			default:
				continue;
			}
			break;
		}
	}
	if (result->cases.size() == 0)
		reportError("Switch with no cases");
	if (eat(CiToken::default_)) {
		expect(CiToken::colon);
		do {
			if (see(CiToken::endOfFile))
				break;
			result->defaultBody.push_back(parseStatement());
		}
		while (!see(CiToken::rightBrace));
	}
	expect(CiToken::rightBrace);
	this->currentLoopOrSwitch = outerLoopOrSwitch;
	return result;
}

std::shared_ptr<CiThrow> CiParser::parseThrow()
{
	std::shared_ptr<CiThrow> result = std::make_shared<CiThrow>();
	result->line = this->line;
	expect(CiToken::throw_);
	result->message = parseExpr();
	expect(CiToken::semicolon);
	return result;
}

std::shared_ptr<CiWhile> CiParser::parseWhile()
{
	std::shared_ptr<CiWhile> result = std::make_shared<CiWhile>();
	result->line = this->line;
	expect(CiToken::while_);
	result->cond = parseParenthesized();
	parseLoopBody(result.get());
	return result;
}

std::shared_ptr<CiStatement> CiParser::parseStatement()
{
	switch (this->currentToken) {
	case CiToken::leftBrace:
		return parseBlock();
	case CiToken::assert:
		return parseAssert();
	case CiToken::break_:
		return parseBreak();
	case CiToken::const_:
		return parseConst();
	case CiToken::continue_:
		return parseContinue();
	case CiToken::do_:
		return parseDoWhile();
	case CiToken::for_:
		return parseFor();
	case CiToken::foreach:
		return parseForeach();
	case CiToken::if_:
		return parseIf();
	case CiToken::lock_:
		return parseLock();
	case CiToken::native:
		return parseNative();
	case CiToken::return_:
		return parseReturn();
	case CiToken::switch_:
		return parseSwitch();
	case CiToken::throw_:
		return parseThrow();
	case CiToken::while_:
		return parseWhile();
	default:
		{
			std::shared_ptr<CiExpr> expr = parseAssign(true);
			expect(CiToken::semicolon);
			return expr;
		}
	}
}

CiCallType CiParser::parseCallType()
{
	switch (this->currentToken) {
	case CiToken::static_:
		nextToken();
		return CiCallType::static_;
	case CiToken::abstract:
		nextToken();
		return CiCallType::abstract;
	case CiToken::virtual_:
		nextToken();
		return CiCallType::virtual_;
	case CiToken::override_:
		nextToken();
		return CiCallType::override_;
	case CiToken::sealed:
		nextToken();
		return CiCallType::sealed;
	default:
		return CiCallType::normal;
	}
}

void CiParser::parseMethod(CiMethod * method)
{
	method->isMutator = eat(CiToken::exclamationMark);
	expect(CiToken::leftParenthesis);
	if (!see(CiToken::rightParenthesis)) {
		do {
			std::shared_ptr<CiCodeDoc> doc = parseDoc();
			std::shared_ptr<CiVar> param = parseVar(parseType());
			param->documentation = doc;
			addSymbol(&method->parameters, param);
		}
		while (eat(CiToken::comma));
	}
	expect(CiToken::rightParenthesis);
	method->throws = eat(CiToken::throws);
	if (method->callType == CiCallType::abstract)
		expect(CiToken::semicolon);
	else if (see(CiToken::fatArrow))
		method->body = parseReturn();
	else if (check(CiToken::leftBrace))
		method->body = parseBlock();
}

std::string_view CiParser::callTypeToString(CiCallType callType)
{
	switch (callType) {
	case CiCallType::static_:
		return "static";
	case CiCallType::normal:
		return "normal";
	case CiCallType::abstract:
		return "abstract";
	case CiCallType::virtual_:
		return "virtual";
	case CiCallType::override_:
		return "override";
	case CiCallType::sealed:
		return "sealed";
	default:
		std::abort();
	}
}

void CiParser::parseClass(std::shared_ptr<CiCodeDoc> doc, bool isPublic, CiCallType callType)
{
	expect(CiToken::class_);
	std::shared_ptr<CiClass> klass = std::make_shared<CiClass>();
	klass->filename = this->filename;
	klass->line = this->line;
	klass->documentation = doc;
	klass->isPublic = isPublic;
	klass->callType = callType;
	klass->name = this->stringValue;
	if (expect(CiToken::id))
		addSymbol(this->program, klass);
	if (eat(CiToken::colon)) {
		klass->baseClassName = this->stringValue;
		expect(CiToken::id);
	}
	expect(CiToken::leftBrace);
	while (!see(CiToken::rightBrace) && !see(CiToken::endOfFile)) {
		doc = parseDoc();
		CiVisibility visibility;
		switch (this->currentToken) {
		case CiToken::internal:
			visibility = CiVisibility::internal;
			nextToken();
			break;
		case CiToken::protected_:
			visibility = CiVisibility::protected_;
			nextToken();
			break;
		case CiToken::public_:
			visibility = CiVisibility::public_;
			nextToken();
			break;
		case CiToken::semicolon:
			reportError("Semicolon in class definition");
			nextToken();
			continue;
		default:
			visibility = CiVisibility::private_;
			break;
		}
		if (see(CiToken::const_)) {
			std::shared_ptr<CiConst> konst = parseConst();
			konst->documentation = doc;
			konst->visibility = visibility;
			addSymbol(klass.get(), konst);
			continue;
		}
		callType = parseCallType();
		std::shared_ptr<CiExpr> type = eat(CiToken::void_) ? this->program->system->voidType : parseType();
		const CiCallExpr * call;
		if (see(CiToken::leftBrace) && (call = dynamic_cast<const CiCallExpr *>(type.get()))) {
			if (call->method->name != klass->name)
				reportError("Method with no return type");
			else {
				if (klass->callType == CiCallType::static_)
					reportError("Constructor in a static class");
				if (callType != CiCallType::normal)
					reportError(std::format("Constructor cannot be {}", callTypeToString(callType)));
				if (call->arguments.size() != 0)
					reportError("Constructor parameters not supported");
				if (klass->constructor != nullptr)
					reportError(std::format("Duplicate constructor, already defined in line {}", klass->constructor->line));
			}
			if (visibility == CiVisibility::private_)
				visibility = CiVisibility::internal;
			std::shared_ptr<CiMethodBase> citemp0 = std::make_shared<CiMethodBase>();
			citemp0->line = call->line;
			citemp0->documentation = doc;
			citemp0->visibility = visibility;
			citemp0->parent = klass.get();
			citemp0->type = this->program->system->voidType;
			citemp0->name = klass->name;
			citemp0->isMutator = true;
			citemp0->body = parseBlock();
			klass->constructor = citemp0;
			continue;
		}
		int line = this->line;
		std::string name{this->stringValue};
		if (!expect(CiToken::id))
			continue;
		if (see(CiToken::leftParenthesis) || see(CiToken::exclamationMark)) {
			if (callType == CiCallType::static_ || klass->callType == CiCallType::abstract) {
			}
			else if (klass->callType == CiCallType::static_)
				reportError("Only static methods allowed in a static class");
			else if (callType == CiCallType::abstract)
				reportError("Abstract methods allowed only in an abstract class");
			else if (klass->callType == CiCallType::sealed && callType == CiCallType::virtual_)
				reportError("Virtual methods disallowed in a sealed class");
			if (visibility == CiVisibility::private_ && callType != CiCallType::static_ && callType != CiCallType::normal)
				reportError(std::format("{} method cannot be private", callTypeToString(callType)));
			std::shared_ptr<CiMethod> method = std::make_shared<CiMethod>();
			method->line = line;
			method->documentation = doc;
			method->visibility = visibility;
			method->callType = callType;
			method->typeExpr = type;
			method->name = name;
			addSymbol(klass.get(), method);
			method->parameters.parent = klass.get();
			parseMethod(method.get());
			continue;
		}
		if (visibility == CiVisibility::public_)
			reportError("Field cannot be public");
		if (callType != CiCallType::normal)
			reportError(std::format("Field cannot be {}", callTypeToString(callType)));
		if (type == this->program->system->voidType)
			reportError("Field cannot be void");
		std::shared_ptr<CiField> field = std::make_shared<CiField>();
		field->line = line;
		field->documentation = doc;
		field->visibility = visibility;
		field->typeExpr = type;
		field->name = name;
		field->value = parseInitializer();
		addSymbol(klass.get(), field);
		expect(CiToken::semicolon);
	}
	expect(CiToken::rightBrace);
}

void CiParser::parseEnum(std::shared_ptr<CiCodeDoc> doc, bool isPublic)
{
	expect(CiToken::enum_);
	bool flags = eat(CiToken::asterisk);
	std::shared_ptr<CiEnum> enu = this->program->system->newEnum(flags);
	enu->filename = this->filename;
	enu->line = this->line;
	enu->documentation = doc;
	enu->isPublic = isPublic;
	enu->name = this->stringValue;
	if (expect(CiToken::id))
		addSymbol(this->program, enu);
	expect(CiToken::leftBrace);
	do {
		std::shared_ptr<CiConst> konst = std::make_shared<CiConst>();
		konst->visibility = CiVisibility::public_;
		konst->documentation = parseDoc();
		konst->line = this->line;
		konst->name = this->stringValue;
		konst->type = enu;
		expect(CiToken::id);
		if (eat(CiToken::assign))
			konst->value = parseExpr();
		else if (flags)
			reportError("enum* symbol must be assigned a value");
		addSymbol(enu.get(), konst);
	}
	while (eat(CiToken::comma));
	expect(CiToken::rightBrace);
}

void CiParser::parse(std::string_view filename, uint8_t const * input, int inputLength)
{
	open(filename, input, inputLength);
	while (!see(CiToken::endOfFile)) {
		std::shared_ptr<CiCodeDoc> doc = parseDoc();
		bool isPublic = eat(CiToken::public_);
		switch (this->currentToken) {
		case CiToken::class_:
			parseClass(doc, isPublic, CiCallType::normal);
			break;
		case CiToken::static_:
		case CiToken::abstract:
		case CiToken::sealed:
			parseClass(doc, isPublic, parseCallType());
			break;
		case CiToken::enum_:
			parseEnum(doc, isPublic);
			break;
		case CiToken::native:
			this->program->topLevelNatives.push_back(parseNative()->content);
			break;
		default:
			reportError("Expected class or enum");
			nextToken();
			break;
		}
	}
}

void CiConsoleParser::reportError(std::string_view message)
{
	std::cerr << this->filename << "(" << this->line << "): ERROR: " << message << '\n';
	this->hasErrors = true;
}
CiSema::CiSema()
{
	this->poison->name = "poison";
}

const CiContainerType * CiSema::getCurrentContainer() const
{
	return this->currentScope->getContainer();
}

void CiSema::reportError(const CiStatement * statement, std::string_view message)
{
	std::cerr << getCurrentContainer()->filename << "(" << statement->line << "): ERROR: " << message << '\n';
	this->hasErrors = true;
}

std::shared_ptr<CiType> CiSema::poisonError(const CiStatement * statement, std::string_view message)
{
	reportError(statement, message);
	return this->poison;
}

void CiSema::resolveBase(CiClass * klass)
{
	if (klass->hasBaseClass()) {
		this->currentScope = klass;
		if (CiClass *baseClass = dynamic_cast<CiClass *>(this->program->tryLookup(klass->baseClassName, true).get())) {
			if (klass->isPublic && !baseClass->isPublic)
				reportError(klass, "Public class cannot derive from an internal class");
			baseClass->hasSubclasses = true;
			klass->parent = baseClass;
		}
		else
			reportError(klass, std::format("Base class {} not found", klass->baseClassName));
	}
	this->program->classes.push_back(klass);
}

void CiSema::checkBaseCycle(CiClass * klass)
{
	const CiSymbol * hare = klass;
	const CiSymbol * tortoise = klass;
	do {
		hare = hare->parent;
		if (hare == nullptr)
			return;
		hare = hare->parent;
		if (hare == nullptr)
			return;
		tortoise = tortoise->parent;
	}
	while (tortoise != hare);
	this->currentScope = klass;
	reportError(klass, std::format("Circular inheritance for class {}", klass->name));
}

void CiSema::takePtr(const CiExpr * expr)
{
	if (CiArrayStorageType *arrayStg = dynamic_cast<CiArrayStorageType *>(expr->type.get()))
		arrayStg->ptrTaken = true;
}

bool CiSema::coerce(const CiExpr * expr, const CiType * type)
{
	if (expr == this->poison.get())
		return false;
	if (!type->isAssignableFrom(expr->type.get())) {
		reportError(expr, std::format("Cannot coerce {} to {}", expr->type->toString(), type->toString()));
		return false;
	}
	const CiPrefixExpr * prefix;
	if ((prefix = dynamic_cast<const CiPrefixExpr *>(expr)) && prefix->op == CiToken::new_ && !dynamic_cast<const CiDynamicPtrType *>(type)) {
		const CiDynamicPtrType * newType = static_cast<const CiDynamicPtrType *>(expr->type.get());
		std::string_view kind = newType->class_->id == CiId::arrayPtrClass ? "array" : "object";
		reportError(expr, std::format("Dynamically allocated {} must be assigned to a {} reference", kind, expr->type->toString()));
		return false;
	}
	takePtr(expr);
	return true;
}

std::shared_ptr<CiExpr> CiSema::visitInterpolatedString(std::shared_ptr<CiInterpolatedString> expr)
{
	int partsCount = 0;
	std::string s{""};
	for (int partsIndex = 0; partsIndex < expr->parts.size(); partsIndex++) {
		const CiInterpolatedPart * part = &expr->parts[partsIndex];
		s += part->prefix;
		std::shared_ptr<CiExpr> arg = visitExpr(part->argument);
		coerce(arg.get(), this->program->system->printableType.get());
		if (dynamic_cast<const CiIntegerType *>(arg->type.get())) {
			switch (part->format) {
			case ' ':
				{
					const CiLiteralLong * literalLong;
					if ((literalLong = dynamic_cast<const CiLiteralLong *>(arg.get())) && part->widthExpr == nullptr) {
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
				reportError(arg.get(), "Invalid format string");
				break;
			}
		}
		else if (dynamic_cast<const CiFloatingType *>(arg->type.get())) {
			switch (part->format) {
			case ' ':
			case 'F':
			case 'f':
			case 'E':
			case 'e':
				break;
			default:
				reportError(arg.get(), "Invalid format string");
				break;
			}
		}
		else {
			if (part->format != ' ')
				reportError(arg.get(), "Invalid format string");
			else {
				const CiLiteralString * literalString;
				if ((literalString = dynamic_cast<const CiLiteralString *>(arg.get())) && part->widthExpr == nullptr) {
					s += literalString->value;
					continue;
				}
			}
		}
		CiInterpolatedPart * targetPart = &expr->parts[partsCount++];
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
		return this->program->system->newLiteralString(s, expr->line);
	expr->type = this->program->system->stringStorageType;
	expr->parts.erase(expr->parts.begin() + partsCount, expr->parts.begin() + partsCount + (expr->parts.size() - partsCount));
	expr->suffix = s;
	return expr;
}

std::shared_ptr<CiExpr> CiSema::lookup(std::shared_ptr<CiSymbolReference> expr, const CiScope * scope)
{
	if (expr->symbol == nullptr) {
		expr->symbol = scope->tryLookup(expr->name, expr->left == nullptr).get();
		if (expr->symbol == nullptr)
			return poisonError(expr.get(), std::format("{} not found", expr->name));
		expr->type = expr->symbol->type;
	}
	CiConst * konst;
	if (!dynamic_cast<const CiEnum *>(scope) && (konst = dynamic_cast<CiConst *>(expr->symbol))) {
		resolveConst(konst);
		if (dynamic_cast<const CiLiteral *>(konst->value.get()) || dynamic_cast<const CiSymbolReference *>(konst->value.get()))
			return konst->value;
	}
	return expr;
}

std::shared_ptr<CiExpr> CiSema::visitSymbolReference(std::shared_ptr<CiSymbolReference> expr)
{
	if (expr->left == nullptr) {
		std::shared_ptr<CiExpr> resolved = lookup(expr, this->currentScope);
		if (const CiMember *nearMember = dynamic_cast<const CiMember *>(expr->symbol)) {
			const CiClass * memberClass;
			if (nearMember->visibility == CiVisibility::private_ && (memberClass = dynamic_cast<const CiClass *>(nearMember->parent)) && memberClass != getCurrentContainer())
				reportError(expr.get(), std::format("Cannot access private member {}", expr->name));
			if (!nearMember->isStatic() && (this->currentMethod == nullptr || this->currentMethod->isStatic()))
				reportError(expr.get(), std::format("Cannot use instance member {} from static context", expr->name));
		}
		if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(resolved.get())) {
			if (const CiVar *v = dynamic_cast<const CiVar *>(symbol->symbol)) {
				if (CiFor *loop = dynamic_cast<CiFor *>(v->parent))
					loop->isIteratorUsed = true;
				else if (this->currentPureArguments.count(v) != 0)
					return this->currentPureArguments.find(v)->second;
			}
			else if (symbol->symbol->id == CiId::regexOptionsEnum)
				this->program->regexOptionsEnum = true;
		}
		return resolved;
	}
	std::shared_ptr<CiExpr> left = visitExpr(expr->left);
	if (left == this->poison)
		return left;
	const CiScope * scope;
	const CiSymbolReference * baseSymbol;
	bool isBase = (baseSymbol = dynamic_cast<const CiSymbolReference *>(left.get())) && baseSymbol->symbol->id == CiId::basePtr;
	if (isBase) {
		const CiClass * baseClass;
		if (this->currentMethod == nullptr || !(baseClass = dynamic_cast<const CiClass *>(this->currentMethod->parent->parent)))
			return poisonError(expr.get(), "No base class");
		scope = baseClass;
	}
	else {
		const CiSymbolReference * leftSymbol;
		const CiScope * obj;
		if ((leftSymbol = dynamic_cast<const CiSymbolReference *>(left.get())) && (obj = dynamic_cast<const CiScope *>(leftSymbol->symbol)))
			scope = obj;
		else {
			scope = left->type.get();
			if (const CiClassType *klass = dynamic_cast<const CiClassType *>(scope))
				scope = klass->class_;
		}
	}
	std::shared_ptr<CiExpr> result = lookup(expr, scope);
	if (result != expr)
		return result;
	if (const CiMember *member = dynamic_cast<const CiMember *>(expr->symbol)) {
		switch (member->visibility) {
		case CiVisibility::private_:
			if (member->parent != this->currentMethod->parent || this->currentMethod->parent != scope)
				reportError(expr.get(), std::format("Cannot access private member {}", expr->name));
			break;
		case CiVisibility::protected_:
			if (isBase)
				break;
			{
				const CiClass * currentClass = static_cast<const CiClass *>(this->currentMethod->parent);
				const CiClass * scopeClass = static_cast<const CiClass *>(scope);
				if (!currentClass->isSameOrBaseOf(scopeClass))
					reportError(expr.get(), std::format("Cannot access protected member {}", expr->name));
				break;
			}
		case CiVisibility::numericElementType:
			{
				const CiClassType * klass;
				if ((klass = dynamic_cast<const CiClassType *>(left->type.get())) && !dynamic_cast<const CiNumericType *>(klass->getElementType().get()))
					reportError(expr.get(), "Method restricted to collections of numbers");
				break;
			}
		case CiVisibility::finalValueType:
			if (!left->type->asClassType()->getValueType()->isFinal())
				reportError(expr.get(), "Method restricted to dictionaries with storage values");
			break;
		default:
			switch (expr->symbol->id) {
			case CiId::arrayLength:
				{
					const CiArrayStorageType * arrayStorage = static_cast<const CiArrayStorageType *>(left->type.get());
					return toLiteralLong(expr.get(), arrayStorage->length);
				}
			case CiId::stringLength:
				if (const CiLiteralString *leftLiteral = dynamic_cast<const CiLiteralString *>(left.get())) {
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
		if (!dynamic_cast<const CiMethodGroup *>(member)) {
			const CiSymbolReference * leftContainer;
			if ((leftContainer = dynamic_cast<const CiSymbolReference *>(left.get())) && dynamic_cast<const CiContainerType *>(leftContainer->symbol)) {
				if (!member->isStatic())
					reportError(expr.get(), std::format("Cannot use instance member {} without an object", expr->name));
			}
			else if (member->isStatic())
				reportError(expr.get(), std::format("{} is static", expr->name));
		}
	}
	std::shared_ptr<CiSymbolReference> citemp0 = std::make_shared<CiSymbolReference>();
	citemp0->line = expr->line;
	citemp0->left = left;
	citemp0->name = expr->name;
	citemp0->symbol = expr->symbol;
	citemp0->type = expr->type;
	return citemp0;
}

std::shared_ptr<CiRangeType> CiSema::union_(std::shared_ptr<CiRangeType> left, std::shared_ptr<CiRangeType> right)
{
	if (right == nullptr)
		return left;
	if (right->min < left->min) {
		if (right->max >= left->max)
			return right;
		return CiRangeType::new_(right->min, left->max);
	}
	if (right->max > left->max)
		return CiRangeType::new_(left->min, right->max);
	return left;
}

std::shared_ptr<CiType> CiSema::getIntegerType(const CiExpr * left, const CiExpr * right)
{
	std::shared_ptr<CiType> type = this->program->system->promoteIntegerTypes(left->type.get(), right->type.get());
	coerce(left, type.get());
	coerce(right, type.get());
	return type;
}

std::shared_ptr<CiIntegerType> CiSema::getShiftType(const CiExpr * left, const CiExpr * right)
{
	std::shared_ptr<CiIntegerType> intType = this->program->system->intType;
	coerce(right, intType.get());
	if (left->type->id == CiId::longType) {
		std::shared_ptr<CiIntegerType> longType = std::static_pointer_cast<CiIntegerType>(left->type);
		return longType;
	}
	coerce(left, intType.get());
	return intType;
}

std::shared_ptr<CiType> CiSema::getNumericType(const CiExpr * left, const CiExpr * right)
{
	std::shared_ptr<CiType> type = this->program->system->promoteNumericTypes(left->type, right->type);
	coerce(left, type.get());
	coerce(right, type.get());
	return type;
}

int CiSema::saturatedNeg(int a)
{
	if (a == -2147483648)
		return 2147483647;
	return -a;
}

int CiSema::saturatedAdd(int a, int b)
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

int CiSema::saturatedSub(int a, int b)
{
	if (b == -2147483648)
		return a < 0 ? a ^ b : 2147483647;
	return saturatedAdd(a, -b);
}

int CiSema::saturatedMul(int a, int b)
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

int CiSema::saturatedDiv(int a, int b)
{
	if (a == -2147483648 && b == -1)
		return 2147483647;
	return a / b;
}

int CiSema::saturatedShiftRight(int a, int b)
{
	return a >> (b >= 31 || b < 0 ? 31 : b);
}

std::shared_ptr<CiRangeType> CiSema::bitwiseUnsignedOp(const CiRangeType * left, CiToken op, const CiRangeType * right)
{
	int leftVariableBits = left->getVariableBits();
	int rightVariableBits = right->getVariableBits();
	int min;
	int max;
	switch (op) {
	case CiToken::and_:
		min = left->min & right->min & ~CiRangeType::getMask(~left->min & ~right->min & (leftVariableBits | rightVariableBits));
		max = (left->max | leftVariableBits) & (right->max | rightVariableBits);
		if (max > left->max)
			max = left->max;
		if (max > right->max)
			max = right->max;
		break;
	case CiToken::or_:
		min = (left->min & ~leftVariableBits) | (right->min & ~rightVariableBits);
		max = left->max | right->max | CiRangeType::getMask(left->max & right->max & CiRangeType::getMask(leftVariableBits | rightVariableBits));
		if (min < left->min)
			min = left->min;
		if (min < right->min)
			min = right->min;
		break;
	case CiToken::xor_:
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
		return CiRangeType::new_(max, min);
	return CiRangeType::new_(min, max);
}

bool CiSema::isEnumOp(const CiExpr * left, const CiExpr * right)
{
	if (dynamic_cast<const CiEnum *>(left->type.get())) {
		if (left->type->id != CiId::boolType && !dynamic_cast<const CiEnumFlags *>(left->type.get()))
			reportError(left, std::format("Define flags enumeration as: enum* {}", left->type->toString()));
		coerce(right, left->type.get());
		return true;
	}
	return false;
}

std::shared_ptr<CiType> CiSema::bitwiseOp(const CiExpr * left, CiToken op, const CiExpr * right)
{
	std::shared_ptr<CiRangeType> leftRange;
	std::shared_ptr<CiRangeType> rightRange;
	if ((leftRange = std::dynamic_pointer_cast<CiRangeType>(left->type)) && (rightRange = std::dynamic_pointer_cast<CiRangeType>(right->type))) {
		std::shared_ptr<CiRangeType> range = nullptr;
		std::shared_ptr<CiRangeType> rightNegative;
		std::shared_ptr<CiRangeType> rightPositive;
		if (rightRange->min >= 0) {
			rightNegative = nullptr;
			rightPositive = rightRange;
		}
		else if (rightRange->max < 0) {
			rightNegative = rightRange;
			rightPositive = nullptr;
		}
		else {
			rightNegative = CiRangeType::new_(rightRange->min, -1);
			rightPositive = CiRangeType::new_(0, rightRange->max);
		}
		if (leftRange->min < 0) {
			const CiRangeType * leftNegative = leftRange->max < 0 ? leftRange.get() : CiRangeType::new_(leftRange->min, -1).get();
			if (rightNegative != nullptr)
				range = bitwiseUnsignedOp(leftNegative, op, rightNegative.get());
			if (rightPositive != nullptr)
				range = union_(bitwiseUnsignedOp(leftNegative, op, rightPositive.get()), range);
		}
		if (leftRange->max >= 0) {
			const CiRangeType * leftPositive = leftRange->min >= 0 ? leftRange.get() : CiRangeType::new_(0, leftRange->max).get();
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

std::shared_ptr<CiRangeType> CiSema::newRangeType(int a, int b, int c, int d)
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
	return CiRangeType::new_(a <= c ? a : c, b >= d ? b : d);
}

std::shared_ptr<CiLiteral> CiSema::toLiteralBool(const CiExpr * expr, bool value) const
{
	std::shared_ptr<CiLiteral> result = value ? std::static_pointer_cast<CiLiteral>(std::make_shared<CiLiteralTrue>()) : std::static_pointer_cast<CiLiteral>(std::make_shared<CiLiteralFalse>());
	result->line = expr->line;
	result->type = this->program->system->boolType;
	return result;
}

std::shared_ptr<CiLiteralLong> CiSema::toLiteralLong(const CiExpr * expr, int64_t value) const
{
	return this->program->system->newLiteralLong(value, expr->line);
}

std::shared_ptr<CiLiteralDouble> CiSema::toLiteralDouble(const CiExpr * expr, double value) const
{
	std::shared_ptr<CiLiteralDouble> citemp0 = std::make_shared<CiLiteralDouble>();
	citemp0->line = expr->line;
	citemp0->type = this->program->system->doubleType;
	citemp0->value = value;
	return citemp0;
}

void CiSema::checkLValue(const CiExpr * expr)
{
	const CiBinaryExpr * indexing;
	if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr)) {
		if (CiVar *def = dynamic_cast<CiVar *>(symbol->symbol)) {
			def->isAssigned = true;
			if (CiFor *forLoop = dynamic_cast<CiFor *>(symbol->symbol->parent))
				forLoop->isRange = false;
			else if (dynamic_cast<const CiForeach *>(symbol->symbol->parent))
				reportError(expr, "Cannot assign a foreach iteration variable");
			for (CiScope * scope = this->currentScope; !dynamic_cast<const CiClass *>(scope); scope = scope->parent) {
				CiFor * forLoop;
				const CiBinaryExpr * binaryCond;
				if ((forLoop = dynamic_cast<CiFor *>(scope)) && forLoop->isRange && (binaryCond = dynamic_cast<const CiBinaryExpr *>(forLoop->cond.get())) && binaryCond->right->isReferenceTo(symbol->symbol))
					forLoop->isRange = false;
			}
		}
		else if (dynamic_cast<const CiField *>(symbol->symbol)) {
			if (symbol->left == nullptr) {
				if (!this->currentMethod->isMutator)
					reportError(expr, "Cannot modify field in a non-mutating method");
			}
			else {
				if (dynamic_cast<const CiStorageType *>(symbol->left->type.get())) {
				}
				else if (dynamic_cast<const CiReadWriteClassType *>(symbol->left->type.get())) {
				}
				else if (dynamic_cast<const CiClassType *>(symbol->left->type.get()))
					reportError(expr, "Cannot modify field through a read-only reference");
				else
					std::abort();
			}
		}
		else
			reportError(expr, "Cannot modify this");
	}
	else if ((indexing = dynamic_cast<const CiBinaryExpr *>(expr)) && indexing->op == CiToken::leftBracket) {
		if (dynamic_cast<const CiStorageType *>(indexing->left->type.get())) {
		}
		else if (dynamic_cast<const CiReadWriteClassType *>(indexing->left->type.get())) {
		}
		else if (dynamic_cast<const CiClassType *>(indexing->left->type.get()))
			reportError(expr, "Cannot modify array through a read-only reference");
		else
			std::abort();
	}
	else
		reportError(expr, "Cannot modify this");
}

std::shared_ptr<CiInterpolatedString> CiSema::concatenate(const CiInterpolatedString * left, const CiInterpolatedString * right) const
{
	std::shared_ptr<CiInterpolatedString> result = std::make_shared<CiInterpolatedString>();
	result->line = left->line;
	result->type = this->program->system->stringStorageType;
	result->parts.insert(result->parts.end(), left->parts.begin(), left->parts.end());
	if (right->parts.size() == 0)
		result->suffix = left->suffix + right->suffix;
	else {
		result->parts.insert(result->parts.end(), right->parts.begin(), right->parts.end());
		CiInterpolatedPart * middle = &result->parts[left->parts.size()];
		middle->prefix = left->suffix + middle->prefix;
		result->suffix = right->suffix;
	}
	return result;
}

std::shared_ptr<CiInterpolatedString> CiSema::toInterpolatedString(std::shared_ptr<CiExpr> expr) const
{
	if (std::shared_ptr<CiInterpolatedString>interpolated = std::dynamic_pointer_cast<CiInterpolatedString>(expr))
		return interpolated;
	std::shared_ptr<CiInterpolatedString> result = std::make_shared<CiInterpolatedString>();
	result->line = expr->line;
	result->type = this->program->system->stringStorageType;
	if (const CiLiteral *literal = dynamic_cast<const CiLiteral *>(expr.get()))
		result->suffix = literal->getLiteralString();
	else {
		result->addPart("", expr);
		result->suffix = "";
	}
	return result;
}

void CiSema::checkComparison(const CiExpr * left, const CiExpr * right)
{
	if (dynamic_cast<const CiEnum *>(left->type.get()))
		coerce(right, left->type.get());
	else {
		const CiType * doubleType = this->program->system->doubleType.get();
		coerce(left, doubleType);
		coerce(right, doubleType);
	}
}

void CiSema::openScope(CiScope * scope)
{
	scope->parent = this->currentScope;
	this->currentScope = scope;
}

void CiSema::closeScope()
{
	this->currentScope = this->currentScope->parent;
}

std::shared_ptr<CiExpr> CiSema::resolveNew(std::shared_ptr<CiPrefixExpr> expr)
{
	if (expr->type != nullptr)
		return expr;
	std::shared_ptr<CiType> type;
	const CiBinaryExpr * binaryNew;
	if ((binaryNew = dynamic_cast<const CiBinaryExpr *>(expr->inner.get())) && binaryNew->op == CiToken::leftBrace) {
		type = toType(binaryNew->left, true);
		const CiClassType * klass;
		if (!(klass = dynamic_cast<const CiClassType *>(type.get())) || dynamic_cast<const CiReadWriteClassType *>(klass))
			return poisonError(expr.get(), "Invalid argument to new");
		std::shared_ptr<CiAggregateInitializer> init = std::static_pointer_cast<CiAggregateInitializer>(binaryNew->right);
		resolveObjectLiteral(klass, init.get());
		std::shared_ptr<CiDynamicPtrType> citemp0 = std::make_shared<CiDynamicPtrType>();
		citemp0->line = expr->line;
		citemp0->class_ = klass->class_;
		expr->type = citemp0;
		expr->inner = init;
		return expr;
	}
	type = toType(expr->inner, true);
	if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(type.get())) {
		std::shared_ptr<CiDynamicPtrType> citemp0 = std::make_shared<CiDynamicPtrType>();
		citemp0->line = expr->line;
		citemp0->class_ = this->program->system->arrayPtrClass.get();
		citemp0->typeArg0 = array->getElementType();
		expr->type = citemp0;
		expr->inner = array->lengthExpr;
		return expr;
	}
	else if (const CiStorageType *klass = dynamic_cast<const CiStorageType *>(type.get())) {
		std::shared_ptr<CiDynamicPtrType> citemp1 = std::make_shared<CiDynamicPtrType>();
		citemp1->line = expr->line;
		citemp1->class_ = klass->class_;
		expr->type = citemp1;
		expr->inner = nullptr;
		return expr;
	}
	else
		return poisonError(expr.get(), "Invalid argument to new");
}

int CiSema::getResourceLength(std::string_view name, const CiPrefixExpr * expr)
{
	return 0;
}

std::shared_ptr<CiExpr> CiSema::visitPrefixExpr(std::shared_ptr<CiPrefixExpr> expr)
{
	std::shared_ptr<CiExpr> inner;
	std::shared_ptr<CiType> type;
	switch (expr->op) {
	case CiToken::increment:
	case CiToken::decrement:
		inner = visitExpr(expr->inner);
		checkLValue(inner.get());
		coerce(inner.get(), this->program->system->doubleType.get());
		if (const CiRangeType *xcrementRange = dynamic_cast<const CiRangeType *>(inner->type.get())) {
			int delta = expr->op == CiToken::increment ? 1 : -1;
			type = CiRangeType::new_(xcrementRange->min + delta, xcrementRange->max + delta);
		}
		else
			type = inner->type;
		expr->inner = inner;
		expr->type = type;
		return expr;
	case CiToken::minus:
		inner = visitExpr(expr->inner);
		coerce(inner.get(), this->program->system->doubleType.get());
		if (const CiRangeType *negRange = dynamic_cast<const CiRangeType *>(inner->type.get())) {
			if (negRange->min == negRange->max)
				return toLiteralLong(expr.get(), -negRange->min);
			type = CiRangeType::new_(saturatedNeg(negRange->max), saturatedNeg(negRange->min));
		}
		else if (const CiLiteralDouble *d = dynamic_cast<const CiLiteralDouble *>(inner.get()))
			return toLiteralDouble(expr.get(), -d->value);
		else if (const CiLiteralLong *l = dynamic_cast<const CiLiteralLong *>(inner.get()))
			return toLiteralLong(expr.get(), -l->value);
		else
			type = inner->type;
		break;
	case CiToken::tilde:
		inner = visitExpr(expr->inner);
		if (dynamic_cast<const CiEnumFlags *>(inner->type.get()))
			type = inner->type;
		else {
			coerce(inner.get(), this->program->system->intType.get());
			if (const CiRangeType *notRange = dynamic_cast<const CiRangeType *>(inner->type.get()))
				type = CiRangeType::new_(~notRange->max, ~notRange->min);
			else
				type = inner->type;
		}
		break;
	case CiToken::exclamationMark:
		inner = resolveBool(expr->inner);
		{
			std::shared_ptr<CiPrefixExpr> citemp0 = std::make_shared<CiPrefixExpr>();
			citemp0->line = expr->line;
			citemp0->op = CiToken::exclamationMark;
			citemp0->inner = inner;
			citemp0->type = this->program->system->boolType;
			return citemp0;
		}
	case CiToken::new_:
		return resolveNew(expr);
	case CiToken::resource:
		{
			std::shared_ptr<CiLiteralString> resourceName;
			if (!(resourceName = std::dynamic_pointer_cast<CiLiteralString>(foldConst(expr->inner))))
				return poisonError(expr.get(), "Resource name must be string");
			inner = resourceName;
			std::shared_ptr<CiArrayStorageType> citemp1 = std::make_shared<CiArrayStorageType>();
			citemp1->class_ = this->program->system->arrayStorageClass.get();
			citemp1->typeArg0 = this->program->system->byteType;
			citemp1->length = getResourceLength(resourceName->value, expr.get());
			type = citemp1;
			break;
		}
	default:
		std::abort();
	}
	std::shared_ptr<CiPrefixExpr> citemp2 = std::make_shared<CiPrefixExpr>();
	citemp2->line = expr->line;
	citemp2->op = expr->op;
	citemp2->inner = inner;
	citemp2->type = type;
	return citemp2;
}

std::shared_ptr<CiExpr> CiSema::visitPostfixExpr(std::shared_ptr<CiPostfixExpr> expr)
{
	expr->inner = visitExpr(expr->inner);
	switch (expr->op) {
	case CiToken::increment:
	case CiToken::decrement:
		checkLValue(expr->inner.get());
		coerce(expr->inner.get(), this->program->system->doubleType.get());
		expr->type = expr->inner->type;
		return expr;
	default:
		return poisonError(expr.get(), std::format("Unexpected {}", CiLexer::tokenToString(expr->op)));
	}
}

bool CiSema::canCompareEqual(const CiType * left, const CiType * right)
{
	if (dynamic_cast<const CiNumericType *>(left))
		return dynamic_cast<const CiNumericType *>(right);
	else if (dynamic_cast<const CiEnum *>(left))
		return left == right;
	else if (const CiClassType *leftClass = dynamic_cast<const CiClassType *>(left)) {
		if (left->nullable && right->id == CiId::nullType)
			return true;
		if ((dynamic_cast<const CiStorageType *>(left) && (dynamic_cast<const CiStorageType *>(right) || dynamic_cast<const CiDynamicPtrType *>(right))) || (dynamic_cast<const CiDynamicPtrType *>(left) && dynamic_cast<const CiStorageType *>(right)))
			return false;
		const CiClassType * rightClass;
		return (rightClass = dynamic_cast<const CiClassType *>(right)) && (leftClass->class_->isSameOrBaseOf(rightClass->class_) || rightClass->class_->isSameOrBaseOf(leftClass->class_)) && leftClass->equalTypeArguments(rightClass);
	}
	else
		return left->id == CiId::nullType && right->nullable;
}

std::shared_ptr<CiExpr> CiSema::resolveEquality(const CiBinaryExpr * expr, std::shared_ptr<CiExpr> left, std::shared_ptr<CiExpr> right)
{
	if (!canCompareEqual(left->type.get(), right->type.get()))
		return poisonError(expr, std::format("Cannot compare {} with {}", left->type->toString(), right->type->toString()));
	const CiRangeType * leftRange;
	const CiRangeType * rightRange;
	if ((leftRange = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightRange = dynamic_cast<const CiRangeType *>(right->type.get()))) {
		if (leftRange->min == leftRange->max && leftRange->min == rightRange->min && leftRange->min == rightRange->max)
			return toLiteralBool(expr, expr->op == CiToken::equal);
		if (leftRange->max < rightRange->min || leftRange->min > rightRange->max)
			return toLiteralBool(expr, expr->op == CiToken::notEqual);
	}
	else {
		const CiLiteralLong * leftLong;
		const CiLiteralLong * rightLong;
		const CiLiteralDouble * leftDouble;
		const CiLiteralDouble * rightDouble;
		const CiLiteralString * leftString;
		const CiLiteralString * rightString;
		if ((leftLong = dynamic_cast<const CiLiteralLong *>(left.get())) && (rightLong = dynamic_cast<const CiLiteralLong *>(right.get())))
			return toLiteralBool(expr, (expr->op == CiToken::notEqual) ^ (leftLong->value == rightLong->value));
		else if ((leftDouble = dynamic_cast<const CiLiteralDouble *>(left.get())) && (rightDouble = dynamic_cast<const CiLiteralDouble *>(right.get())))
			return toLiteralBool(expr, (expr->op == CiToken::notEqual) ^ (leftDouble->value == rightDouble->value));
		else if ((leftString = dynamic_cast<const CiLiteralString *>(left.get())) && (rightString = dynamic_cast<const CiLiteralString *>(right.get())))
			return toLiteralBool(expr, (expr->op == CiToken::notEqual) ^ (leftString->value == rightString->value));
		else if (dynamic_cast<const CiLiteralNull *>(left.get()))
			return toLiteralBool(expr, expr->op == CiToken::equal);
		else if (dynamic_cast<const CiLiteralFalse *>(left.get()))
			return toLiteralBool(expr, (expr->op == CiToken::notEqual) ^ !!dynamic_cast<const CiLiteralFalse *>(right.get()));
		else if (dynamic_cast<const CiLiteralTrue *>(left.get()))
			return toLiteralBool(expr, (expr->op == CiToken::notEqual) ^ !!dynamic_cast<const CiLiteralTrue *>(right.get()));
		if (left->isConstEnum() && right->isConstEnum())
			return toLiteralBool(expr, (expr->op == CiToken::notEqual) ^ (left->intValue() == right->intValue()));
	}
	takePtr(left.get());
	takePtr(right.get());
	std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
	citemp0->line = expr->line;
	citemp0->left = left;
	citemp0->op = expr->op;
	citemp0->right = right;
	citemp0->type = this->program->system->boolType;
	return citemp0;
}

std::shared_ptr<CiExpr> CiSema::resolveIs(std::shared_ptr<CiBinaryExpr> expr, std::shared_ptr<CiExpr> left, const CiExpr * right)
{
	const CiClassType * leftPtr;
	if (!(leftPtr = dynamic_cast<const CiClassType *>(left->type.get())) || dynamic_cast<const CiStorageType *>(left->type.get()))
		return poisonError(expr.get(), "Left hand side of the 'is' operator must be an object reference");
	const CiClass * klass;
	if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(right)) {
		if (const CiClass *klass2 = dynamic_cast<const CiClass *>(symbol->symbol))
			klass = klass2;
		else
			return poisonError(expr.get(), "Right hand side of the 'is' operator must be a class name");
	}
	else if (const CiVar *def = dynamic_cast<const CiVar *>(right)) {
		const CiClassType * rightPtr;
		if (!(rightPtr = dynamic_cast<const CiClassType *>(def->type.get())))
			return poisonError(expr.get(), "Right hand side of the 'is' operator must be an object reference definition");
		if (dynamic_cast<const CiReadWriteClassType *>(rightPtr) && !dynamic_cast<const CiDynamicPtrType *>(leftPtr) && (dynamic_cast<const CiDynamicPtrType *>(rightPtr) || !dynamic_cast<const CiReadWriteClassType *>(leftPtr)))
			return poisonError(expr.get(), std::format("{} cannot be casted to {}", leftPtr->toString(), rightPtr->toString()));
		klass = rightPtr->class_;
	}
	else
		return poisonError(expr.get(), "Right hand side of the 'is' operator must be a class name");
	if (klass->isSameOrBaseOf(leftPtr->class_))
		return poisonError(expr.get(), std::format("{} is {}, the 'is' operator would always return 'true'", leftPtr->toString(), klass->name));
	if (!leftPtr->class_->isSameOrBaseOf(klass))
		return poisonError(expr.get(), std::format("{} is not base class of {}, the 'is' operator would always return 'false'", leftPtr->toString(), klass->name));
	expr->left = left;
	expr->type = this->program->system->boolType;
	return expr;
}

std::shared_ptr<CiExpr> CiSema::visitBinaryExpr(std::shared_ptr<CiBinaryExpr> expr)
{
	std::shared_ptr<CiExpr> left = visitExpr(expr->left);
	std::shared_ptr<CiExpr> right = visitExpr(expr->right);
	if (left == this->poison || right == this->poison)
		return this->poison;
	std::shared_ptr<CiType> type;
	switch (expr->op) {
	case CiToken::leftBracket:
		{
			const CiClassType * klass;
			if (!(klass = dynamic_cast<const CiClassType *>(left->type.get())))
				return poisonError(expr.get(), "Cannot index this object");
			switch (klass->class_->id) {
			case CiId::stringClass:
				coerce(right.get(), this->program->system->intType.get());
				{
					const CiLiteralString * stringLiteral;
					const CiLiteralLong * indexLiteral;
					if ((stringLiteral = dynamic_cast<const CiLiteralString *>(left.get())) && (indexLiteral = dynamic_cast<const CiLiteralLong *>(right.get()))) {
						int64_t i = indexLiteral->value;
						if (i >= 0 && i <= 2147483647) {
							int c = stringLiteral->getAsciiAt(static_cast<int>(i));
							if (c >= 0)
								return CiLiteralChar::new_(c, expr->line);
						}
					}
					type = this->program->system->charType;
					break;
				}
			case CiId::arrayPtrClass:
			case CiId::arrayStorageClass:
			case CiId::listClass:
				coerce(right.get(), this->program->system->intType.get());
				type = klass->getElementType();
				break;
			case CiId::dictionaryClass:
			case CiId::sortedDictionaryClass:
			case CiId::orderedDictionaryClass:
				coerce(right.get(), klass->getKeyType());
				type = klass->getValueType();
				break;
			default:
				return poisonError(expr.get(), "Cannot index this object");
			}
			break;
		}
	case CiToken::plus:
		{
			const CiRangeType * leftAdd;
			const CiRangeType * rightAdd;
			if ((leftAdd = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightAdd = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				type = CiRangeType::new_(saturatedAdd(leftAdd->min, rightAdd->min), saturatedAdd(leftAdd->max, rightAdd->max));
			}
			else if (dynamic_cast<const CiStringType *>(left->type.get()) || dynamic_cast<const CiStringType *>(right->type.get())) {
				coerce(left.get(), this->program->system->printableType.get());
				coerce(right.get(), this->program->system->printableType.get());
				const CiLiteral * leftLiteral;
				const CiLiteral * rightLiteral;
				if ((leftLiteral = dynamic_cast<const CiLiteral *>(left.get())) && (rightLiteral = dynamic_cast<const CiLiteral *>(right.get())))
					return this->program->system->newLiteralString(leftLiteral->getLiteralString() + rightLiteral->getLiteralString(), expr->line);
				if (dynamic_cast<const CiInterpolatedString *>(left.get()) || dynamic_cast<const CiInterpolatedString *>(right.get()))
					return concatenate(toInterpolatedString(left).get(), toInterpolatedString(right).get());
				type = this->program->system->stringStorageType;
			}
			else
				type = getNumericType(left.get(), right.get());
			break;
		}
	case CiToken::minus:
		{
			const CiRangeType * leftSub;
			const CiRangeType * rightSub;
			if ((leftSub = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightSub = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				type = CiRangeType::new_(saturatedSub(leftSub->min, rightSub->max), saturatedSub(leftSub->max, rightSub->min));
			}
			else
				type = getNumericType(left.get(), right.get());
			break;
		}
	case CiToken::asterisk:
		{
			const CiRangeType * leftMul;
			const CiRangeType * rightMul;
			if ((leftMul = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightMul = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				type = newRangeType(saturatedMul(leftMul->min, rightMul->min), saturatedMul(leftMul->min, rightMul->max), saturatedMul(leftMul->max, rightMul->min), saturatedMul(leftMul->max, rightMul->max));
			}
			else
				type = getNumericType(left.get(), right.get());
			break;
		}
	case CiToken::slash:
		{
			const CiRangeType * leftDiv;
			const CiRangeType * rightDiv;
			if ((leftDiv = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightDiv = dynamic_cast<const CiRangeType *>(right->type.get()))) {
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
	case CiToken::mod:
		{
			const CiRangeType * leftMod;
			const CiRangeType * rightMod;
			if ((leftMod = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightMod = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				int den = ~std::min(rightMod->min, -rightMod->max);
				if (den < 0)
					return poisonError(expr.get(), "Mod zero");
				type = CiRangeType::new_(leftMod->min >= 0 ? 0 : std::max(leftMod->min, -den), leftMod->max < 0 ? 0 : std::min(leftMod->max, den));
			}
			else
				type = getIntegerType(left.get(), right.get());
			break;
		}
	case CiToken::and_:
	case CiToken::or_:
	case CiToken::xor_:
		type = bitwiseOp(left.get(), expr->op, right.get());
		break;
	case CiToken::shiftLeft:
		{
			const CiRangeType * leftShl;
			const CiRangeType * rightShl;
			if ((leftShl = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightShl = dynamic_cast<const CiRangeType *>(right->type.get())) && leftShl->min == leftShl->max && rightShl->min == rightShl->max) {
				int result = leftShl->min << rightShl->min;
				type = CiRangeType::new_(result, result);
			}
			else
				type = getShiftType(left.get(), right.get());
			break;
		}
	case CiToken::shiftRight:
		{
			const CiRangeType * leftShr;
			const CiRangeType * rightShr;
			if ((leftShr = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightShr = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				if (rightShr->min < 0)
					rightShr = CiRangeType::new_(0, 32).get();
				type = CiRangeType::new_(saturatedShiftRight(leftShr->min, leftShr->min < 0 ? rightShr->min : rightShr->max), saturatedShiftRight(leftShr->max, leftShr->max < 0 ? rightShr->max : rightShr->min));
			}
			else
				type = getShiftType(left.get(), right.get());
			break;
		}
	case CiToken::equal:
	case CiToken::notEqual:
		return resolveEquality(expr.get(), left, right);
	case CiToken::less:
		{
			const CiRangeType * leftLess;
			const CiRangeType * rightLess;
			if ((leftLess = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightLess = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				if (leftLess->max < rightLess->min)
					return toLiteralBool(expr.get(), true);
				if (leftLess->min >= rightLess->max)
					return toLiteralBool(expr.get(), false);
			}
			else
				checkComparison(left.get(), right.get());
			type = this->program->system->boolType;
			break;
		}
	case CiToken::lessOrEqual:
		{
			const CiRangeType * leftLeq;
			const CiRangeType * rightLeq;
			if ((leftLeq = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightLeq = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				if (leftLeq->max <= rightLeq->min)
					return toLiteralBool(expr.get(), true);
				if (leftLeq->min > rightLeq->max)
					return toLiteralBool(expr.get(), false);
			}
			else
				checkComparison(left.get(), right.get());
			type = this->program->system->boolType;
			break;
		}
	case CiToken::greater:
		{
			const CiRangeType * leftGreater;
			const CiRangeType * rightGreater;
			if ((leftGreater = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightGreater = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				if (leftGreater->min > rightGreater->max)
					return toLiteralBool(expr.get(), true);
				if (leftGreater->max <= rightGreater->min)
					return toLiteralBool(expr.get(), false);
			}
			else
				checkComparison(left.get(), right.get());
			type = this->program->system->boolType;
			break;
		}
	case CiToken::greaterOrEqual:
		{
			const CiRangeType * leftGeq;
			const CiRangeType * rightGeq;
			if ((leftGeq = dynamic_cast<const CiRangeType *>(left->type.get())) && (rightGeq = dynamic_cast<const CiRangeType *>(right->type.get()))) {
				if (leftGeq->min >= rightGeq->max)
					return toLiteralBool(expr.get(), true);
				if (leftGeq->max < rightGeq->min)
					return toLiteralBool(expr.get(), false);
			}
			else
				checkComparison(left.get(), right.get());
			type = this->program->system->boolType;
			break;
		}
	case CiToken::condAnd:
		coerce(left.get(), this->program->system->boolType.get());
		coerce(right.get(), this->program->system->boolType.get());
		if (dynamic_cast<const CiLiteralTrue *>(left.get()))
			return right;
		if (dynamic_cast<const CiLiteralFalse *>(left.get()) || dynamic_cast<const CiLiteralTrue *>(right.get()))
			return left;
		type = this->program->system->boolType;
		break;
	case CiToken::condOr:
		coerce(left.get(), this->program->system->boolType.get());
		coerce(right.get(), this->program->system->boolType.get());
		if (dynamic_cast<const CiLiteralTrue *>(left.get()) || dynamic_cast<const CiLiteralFalse *>(right.get()))
			return left;
		if (dynamic_cast<const CiLiteralFalse *>(left.get()))
			return right;
		type = this->program->system->boolType;
		break;
	case CiToken::assign:
		checkLValue(left.get());
		coerce(right.get(), left->type.get());
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case CiToken::addAssign:
		checkLValue(left.get());
		if (left->type->id == CiId::stringStorageType)
			coerce(right.get(), this->program->system->printableType.get());
		else {
			coerce(left.get(), this->program->system->doubleType.get());
			coerce(right.get(), left->type.get());
		}
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case CiToken::subAssign:
	case CiToken::mulAssign:
	case CiToken::divAssign:
		checkLValue(left.get());
		coerce(left.get(), this->program->system->doubleType.get());
		coerce(right.get(), left->type.get());
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case CiToken::modAssign:
	case CiToken::shiftLeftAssign:
	case CiToken::shiftRightAssign:
		checkLValue(left.get());
		coerce(left.get(), this->program->system->intType.get());
		coerce(right.get(), this->program->system->intType.get());
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case CiToken::andAssign:
	case CiToken::orAssign:
	case CiToken::xorAssign:
		checkLValue(left.get());
		if (!isEnumOp(left.get(), right.get())) {
			coerce(left.get(), this->program->system->intType.get());
			coerce(right.get(), this->program->system->intType.get());
		}
		expr->left = left;
		expr->right = right;
		expr->type = left->type;
		return expr;
	case CiToken::is:
		return resolveIs(expr, left, right.get());
	case CiToken::range:
		return poisonError(expr.get(), "Range within an expression");
	default:
		std::abort();
	}
	const CiRangeType * range;
	if ((range = dynamic_cast<const CiRangeType *>(type.get())) && range->min == range->max)
		return toLiteralLong(expr.get(), range->min);
	std::shared_ptr<CiBinaryExpr> citemp0 = std::make_shared<CiBinaryExpr>();
	citemp0->line = expr->line;
	citemp0->left = left;
	citemp0->op = expr->op;
	citemp0->right = right;
	citemp0->type = type;
	return citemp0;
}

std::shared_ptr<CiType> CiSema::tryGetPtr(std::shared_ptr<CiType> type, bool nullable) const
{
	if (type->id == CiId::stringStorageType)
		return nullable ? this->program->system->stringNullablePtrType : this->program->system->stringPtrType;
	if (const CiStorageType *storage = dynamic_cast<const CiStorageType *>(type.get())) {
		std::shared_ptr<CiReadWriteClassType> citemp0 = std::make_shared<CiReadWriteClassType>();
		citemp0->class_ = storage->class_->id == CiId::arrayStorageClass ? this->program->system->arrayPtrClass.get() : storage->class_;
		citemp0->nullable = nullable;
		citemp0->typeArg0 = storage->typeArg0;
		citemp0->typeArg1 = storage->typeArg1;
		return citemp0;
	}
	const CiClassType * ptr;
	if (nullable && (ptr = dynamic_cast<const CiClassType *>(type.get())) && !ptr->nullable) {
		std::shared_ptr<CiClassType> result;
		if (dynamic_cast<const CiDynamicPtrType *>(type.get()))
			result = std::make_shared<CiDynamicPtrType>();
		else if (dynamic_cast<const CiReadWriteClassType *>(type.get()))
			result = std::make_shared<CiReadWriteClassType>();
		else
			result = std::make_shared<CiClassType>();
		result->class_ = ptr->class_;
		result->nullable = true;
		result->typeArg0 = ptr->typeArg0;
		result->typeArg1 = ptr->typeArg1;
		return result;
	}
	return type;
}

const CiClass * CiSema::getLowestCommonAncestor(const CiClass * left, const CiClass * right)
{
	for (;;) {
		if (left->isSameOrBaseOf(right))
			return left;
		if (const CiClass *parent = dynamic_cast<const CiClass *>(left->parent))
			left = parent;
		else
			return nullptr;
	}
}

std::shared_ptr<CiType> CiSema::getCommonType(const CiExpr * left, const CiExpr * right)
{
	std::shared_ptr<CiRangeType> leftRange;
	std::shared_ptr<CiRangeType> rightRange;
	if ((leftRange = std::dynamic_pointer_cast<CiRangeType>(left->type)) && (rightRange = std::dynamic_pointer_cast<CiRangeType>(right->type)))
		return union_(leftRange, rightRange);
	bool nullable = left->type->nullable || right->type->nullable;
	std::shared_ptr<CiType> ptr = tryGetPtr(left->type, nullable);
	if (ptr->isAssignableFrom(right->type.get()))
		return ptr;
	ptr = tryGetPtr(right->type, nullable);
	if (ptr->isAssignableFrom(left->type.get()))
		return ptr;
	const CiClassType * leftClass;
	const CiClassType * rightClass;
	if ((leftClass = dynamic_cast<const CiClassType *>(left->type.get())) && (rightClass = dynamic_cast<const CiClassType *>(right->type.get())) && leftClass->equalTypeArguments(rightClass)) {
		const CiClass * klass = getLowestCommonAncestor(leftClass->class_, rightClass->class_);
		if (klass != nullptr) {
			std::shared_ptr<CiClassType> result;
			if (!dynamic_cast<const CiReadWriteClassType *>(leftClass) || !dynamic_cast<const CiReadWriteClassType *>(rightClass))
				result = std::make_shared<CiClassType>();
			else if (dynamic_cast<const CiDynamicPtrType *>(leftClass) && dynamic_cast<const CiDynamicPtrType *>(rightClass))
				result = std::make_shared<CiDynamicPtrType>();
			else
				result = std::make_shared<CiReadWriteClassType>();
			result->class_ = klass;
			result->nullable = nullable;
			result->typeArg0 = leftClass->typeArg0;
			result->typeArg1 = leftClass->typeArg1;
			return result;
		}
	}
	return poisonError(left, std::format("Incompatible types: {} and {}", left->type->toString(), right->type->toString()));
}

std::shared_ptr<CiExpr> CiSema::visitSelectExpr(const CiSelectExpr * expr)
{
	std::shared_ptr<CiExpr> cond = resolveBool(expr->cond);
	std::shared_ptr<CiExpr> onTrue = visitExpr(expr->onTrue);
	std::shared_ptr<CiExpr> onFalse = visitExpr(expr->onFalse);
	if (onTrue == this->poison || onFalse == this->poison)
		return this->poison;
	std::shared_ptr<CiType> type = getCommonType(onTrue.get(), onFalse.get());
	coerce(onTrue.get(), type.get());
	coerce(onFalse.get(), type.get());
	if (dynamic_cast<const CiLiteralTrue *>(cond.get()))
		return onTrue;
	if (dynamic_cast<const CiLiteralFalse *>(cond.get()))
		return onFalse;
	std::shared_ptr<CiSelectExpr> citemp0 = std::make_shared<CiSelectExpr>();
	citemp0->line = expr->line;
	citemp0->cond = cond;
	citemp0->onTrue = onTrue;
	citemp0->onFalse = onFalse;
	citemp0->type = type;
	return citemp0;
}

std::shared_ptr<CiType> CiSema::evalType(const CiClassType * generic, std::shared_ptr<CiType> type) const
{
	if (type->id == CiId::typeParam0)
		return generic->typeArg0;
	if (type->id == CiId::typeParam0NotFinal)
		return generic->typeArg0->isFinal() ? nullptr : generic->typeArg0;
	const CiClassType * collection;
	if ((collection = dynamic_cast<const CiClassType *>(type.get())) && collection->class_->typeParameterCount == 1 && collection->typeArg0->id == CiId::typeParam0) {
		std::shared_ptr<CiClassType> result = dynamic_cast<const CiReadWriteClassType *>(type.get()) ? std::make_shared<CiReadWriteClassType>() : std::make_shared<CiClassType>();
		result->class_ = collection->class_;
		result->typeArg0 = generic->typeArg0;
		return result;
	}
	return type;
}

bool CiSema::canCall(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * arguments) const
{
	const CiVar * param = method->parameters.firstParameter();
	for (const std::shared_ptr<CiExpr> &arg : *arguments) {
		if (param == nullptr)
			return false;
		std::shared_ptr<CiType> type = param->type;
		const CiClassType * generic;
		if (obj != nullptr && (generic = dynamic_cast<const CiClassType *>(obj->type.get())))
			type = evalType(generic, type);
		if (!type->isAssignableFrom(arg->type.get()))
			return false;
		param = param->nextParameter();
	}
	return param == nullptr || param->value != nullptr;
}

std::shared_ptr<CiExpr> CiSema::resolveCallWithArguments(std::shared_ptr<CiCallExpr> expr, const std::vector<std::shared_ptr<CiExpr>> * arguments)
{
	std::shared_ptr<CiSymbolReference> symbol;
	if (!(symbol = std::dynamic_pointer_cast<CiSymbolReference>(visitExpr(expr->method))))
		return this->poison;
	CiMethod * method;
	if (symbol->symbol == nullptr)
		return this->poison;
	else if (CiMethod *m = dynamic_cast<CiMethod *>(symbol->symbol))
		method = m;
	else if (const CiMethodGroup *group = dynamic_cast<const CiMethodGroup *>(symbol->symbol)) {
		method = group->methods[0].get();
		if (!canCall(symbol->left.get(), method, arguments))
			method = group->methods[1].get();
	}
	else
		return poisonError(symbol.get(), "Expected a method");
	int i = 0;
	for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
		std::shared_ptr<CiType> type = param->type;
		const CiClassType * generic;
		if (symbol->left != nullptr && (generic = dynamic_cast<const CiClassType *>(symbol->left->type.get()))) {
			type = evalType(generic, type);
			if (type == nullptr)
				continue;
		}
		if (i >= arguments->size()) {
			if (param->value != nullptr)
				break;
			return poisonError(expr.get(), std::format("Too few arguments for '{}'", method->name));
		}
		CiExpr * arg = (*arguments)[i++].get();
		CiLambdaExpr * lambda;
		if (type->id == CiId::typeParam0Predicate && (lambda = dynamic_cast<CiLambdaExpr *>(arg))) {
			lambda->first->type = symbol->left->type->asClassType()->typeArg0;
			openScope(lambda);
			lambda->body = visitExpr(lambda->body);
			closeScope();
			coerce(lambda->body.get(), this->program->system->boolType.get());
		}
		else
			coerce(arg, type.get());
	}
	if (i < arguments->size())
		return poisonError((*arguments)[i].get(), std::format("Too many arguments for '{}'", method->name));
	if (method->throws) {
		if (this->currentMethod == nullptr)
			return poisonError(expr.get(), std::format("Cannot call method '{}' here because it is marked 'throws'", method->name));
		if (!this->currentMethod->throws)
			return poisonError(expr.get(), "Method marked 'throws' called from a method not marked 'throws'");
	}
	symbol->symbol = method;
	const CiReturn * ret;
	if (method->callType == CiCallType::static_ && (ret = dynamic_cast<const CiReturn *>(method->body.get())) && std::all_of(arguments->begin(), arguments->end(), [](const std::shared_ptr<CiExpr> &arg) { return dynamic_cast<const CiLiteral *>(arg.get()); }) && !this->currentPureMethods.contains(method)) {
		this->currentPureMethods.insert(method);
		i = 0;
		for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
			if (i < arguments->size())
				this->currentPureArguments[param] = (*arguments)[i++];
			else
				this->currentPureArguments[param] = param->value;
		}
		std::shared_ptr<CiExpr> result = visitExpr(ret->value);
		for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter())
			this->currentPureArguments.erase(param);
		this->currentPureMethods.erase(method);
		if (dynamic_cast<const CiLiteral *>(result.get()))
			return result;
	}
	if (this->currentMethod != nullptr)
		this->currentMethod->calls.insert(method);
	if (this->currentPureArguments.size() == 0) {
		expr->method = symbol;
		std::shared_ptr<CiType> type = method->type;
		const CiClassType * generic;
		if (symbol->left != nullptr && (generic = dynamic_cast<const CiClassType *>(symbol->left->type.get())))
			type = evalType(generic, type);
		expr->type = type;
	}
	return expr;
}

std::shared_ptr<CiExpr> CiSema::visitCallExpr(std::shared_ptr<CiCallExpr> expr)
{
	if (this->currentPureArguments.size() == 0) {
		std::vector<std::shared_ptr<CiExpr>> * arguments = &expr->arguments;
		for (int i = 0; i < arguments->size(); i++) {
			if (!dynamic_cast<const CiLambdaExpr *>((*arguments)[i].get()))
				(*arguments)[i] = visitExpr((*arguments)[i]);
		}
		return resolveCallWithArguments(expr, arguments);
	}
	else {
		std::vector<std::shared_ptr<CiExpr>> arguments;
		for (const std::shared_ptr<CiExpr> &arg : expr->arguments)
			arguments.push_back(visitExpr(arg));
		return resolveCallWithArguments(expr, &arguments);
	}
}

void CiSema::resolveObjectLiteral(const CiClassType * klass, const CiAggregateInitializer * init)
{
	for (const std::shared_ptr<CiExpr> &item : init->items) {
		CiBinaryExpr * field = static_cast<CiBinaryExpr *>(item.get());
		assert(field->op == CiToken::assign);
		std::shared_ptr<CiSymbolReference> symbol = std::static_pointer_cast<CiSymbolReference>(field->left);
		lookup(symbol, klass->class_);
		if (dynamic_cast<const CiField *>(symbol->symbol)) {
			field->right = visitExpr(field->right);
			coerce(field->right.get(), symbol->type.get());
		}
		else
			reportError(field, "Expected a field");
	}
}

void CiSema::visitVar(std::shared_ptr<CiVar> expr)
{
	const CiType * type = resolveType(expr.get()).get();
	if (expr->value != nullptr) {
		const CiStorageType * storage;
		const CiAggregateInitializer * init;
		if ((storage = dynamic_cast<const CiStorageType *>(type)) && (init = dynamic_cast<const CiAggregateInitializer *>(expr->value.get())))
			resolveObjectLiteral(storage, init);
		else {
			expr->value = visitExpr(expr->value);
			if (!expr->isAssignableStorage()) {
				if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(type)) {
					type = array->getElementType().get();
					const CiLiteral * literal;
					if (!(literal = dynamic_cast<const CiLiteral *>(expr->value.get())) || !literal->isDefaultValue())
						reportError(expr->value.get(), "Only null, zero and false supported as an array initializer");
				}
				coerce(expr->value.get(), type);
			}
		}
	}
	this->currentScope->add(expr);
}

std::shared_ptr<CiExpr> CiSema::visitExpr(std::shared_ptr<CiExpr> expr)
{
	if (CiAggregateInitializer *aggregate = dynamic_cast<CiAggregateInitializer *>(expr.get())) {
		std::vector<std::shared_ptr<CiExpr>> * items = &aggregate->items;
		for (int i = 0; i < items->size(); i++)
			(*items)[i] = visitExpr((*items)[i]);
		return expr;
	}
	else if (dynamic_cast<const CiLiteral *>(expr.get()))
		return expr;
	else if (std::shared_ptr<CiInterpolatedString>interpolated = std::dynamic_pointer_cast<CiInterpolatedString>(expr))
		return visitInterpolatedString(interpolated);
	else if (std::shared_ptr<CiSymbolReference>symbol = std::dynamic_pointer_cast<CiSymbolReference>(expr))
		return visitSymbolReference(symbol);
	else if (std::shared_ptr<CiPrefixExpr>prefix = std::dynamic_pointer_cast<CiPrefixExpr>(expr))
		return visitPrefixExpr(prefix);
	else if (std::shared_ptr<CiPostfixExpr>postfix = std::dynamic_pointer_cast<CiPostfixExpr>(expr))
		return visitPostfixExpr(postfix);
	else if (std::shared_ptr<CiBinaryExpr>binary = std::dynamic_pointer_cast<CiBinaryExpr>(expr))
		return visitBinaryExpr(binary);
	else if (const CiSelectExpr *select = dynamic_cast<const CiSelectExpr *>(expr.get()))
		return visitSelectExpr(select);
	else if (std::shared_ptr<CiCallExpr>call = std::dynamic_pointer_cast<CiCallExpr>(expr))
		return visitCallExpr(call);
	else if (dynamic_cast<const CiLambdaExpr *>(expr.get())) {
		reportError(expr.get(), "Unexpected lambda expression");
		return expr;
	}
	else if (std::shared_ptr<CiVar>def = std::dynamic_pointer_cast<CiVar>(expr)) {
		visitVar(def);
		return expr;
	}
	else
		std::abort();
}

std::shared_ptr<CiExpr> CiSema::resolveBool(std::shared_ptr<CiExpr> expr)
{
	expr = visitExpr(expr);
	coerce(expr.get(), this->program->system->boolType.get());
	return expr;
}

std::shared_ptr<CiClassType> CiSema::createClassPtr(const CiClass * klass, CiToken ptrModifier, bool nullable)
{
	std::shared_ptr<CiClassType> ptr;
	switch (ptrModifier) {
	case CiToken::endOfFile:
		ptr = std::make_shared<CiClassType>();
		break;
	case CiToken::exclamationMark:
		ptr = std::make_shared<CiReadWriteClassType>();
		break;
	case CiToken::hash:
		ptr = std::make_shared<CiDynamicPtrType>();
		break;
	default:
		std::abort();
	}
	ptr->class_ = klass;
	ptr->nullable = nullable;
	return ptr;
}

void CiSema::fillGenericClass(CiClassType * result, const CiClass * klass, const CiAggregateInitializer * typeArgExprs)
{
	std::vector<std::shared_ptr<CiType>> typeArgs;
	for (const std::shared_ptr<CiExpr> &typeArgExpr : typeArgExprs->items)
		typeArgs.push_back(toType(typeArgExpr, false));
	if (typeArgs.size() != klass->typeParameterCount) {
		reportError(result, std::format("Expected {} type arguments for {}, got {}", klass->typeParameterCount, klass->name, typeArgs.size()));
		return;
	}
	result->class_ = klass;
	result->typeArg0 = typeArgs[0];
	if (typeArgs.size() == 2)
		result->typeArg1 = typeArgs[1];
}

void CiSema::expectNoPtrModifier(const CiExpr * expr, CiToken ptrModifier, bool nullable)
{
	if (ptrModifier != CiToken::endOfFile)
		reportError(expr, std::format("Unexpected {} on a non-reference type", CiLexer::tokenToString(ptrModifier)));
	if (nullable)
		reportError(expr, "Nullable value types not supported");
}

std::shared_ptr<CiType> CiSema::toBaseType(const CiExpr * expr, CiToken ptrModifier, bool nullable)
{
	if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr)) {
		if (std::shared_ptr<CiType>type = std::dynamic_pointer_cast<CiType>(this->program->tryLookup(symbol->name, true))) {
			if (const CiClass *klass = dynamic_cast<const CiClass *>(type.get())) {
				if (klass->id == CiId::matchClass && ptrModifier != CiToken::endOfFile)
					reportError(expr, "Read-write references to the built-in class Match are not supported");
				std::shared_ptr<CiClassType> ptr = createClassPtr(klass, ptrModifier, nullable);
				if (const CiAggregateInitializer *typeArgExprs = dynamic_cast<const CiAggregateInitializer *>(symbol->left.get()))
					fillGenericClass(ptr.get(), klass, typeArgExprs);
				else if (symbol->left != nullptr)
					return poisonError(expr, "Invalid type");
				else
					ptr->name = klass->name;
				return ptr;
			}
			else if (symbol->left != nullptr)
				return poisonError(expr, "Invalid type");
			if (type->id == CiId::stringPtrType && nullable) {
				type = this->program->system->stringNullablePtrType;
				nullable = false;
			}
			expectNoPtrModifier(expr, ptrModifier, nullable);
			return type;
		}
		return poisonError(expr, std::format("Type {} not found", symbol->name));
	}
	else if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr)) {
		expectNoPtrModifier(expr, ptrModifier, nullable);
		if (call->arguments.size() != 0)
			reportError(call, "Expected empty parentheses for storage type");
		if (const CiAggregateInitializer *typeArgExprs2 = dynamic_cast<const CiAggregateInitializer *>(call->method->left.get())) {
			std::shared_ptr<CiStorageType> storage = std::make_shared<CiStorageType>();
			storage->line = call->line;
			if (const CiClass *klass = dynamic_cast<const CiClass *>(this->program->tryLookup(call->method->name, true).get())) {
				fillGenericClass(storage.get(), klass, typeArgExprs2);
				return storage;
			}
			return poisonError(typeArgExprs2, std::format("{} is not a class", call->method->name));
		}
		else if (call->method->left != nullptr)
			return poisonError(expr, "Invalid type");
		if (call->method->name == "string")
			return this->program->system->stringStorageType;
		if (const CiClass *klass2 = dynamic_cast<const CiClass *>(this->program->tryLookup(call->method->name, true).get())) {
			std::shared_ptr<CiStorageType> citemp0 = std::make_shared<CiStorageType>();
			citemp0->class_ = klass2;
			return citemp0;
		}
		return poisonError(expr, std::format("Class {} not found", call->method->name));
	}
	else
		return poisonError(expr, "Invalid type");
}

std::shared_ptr<CiType> CiSema::toType(std::shared_ptr<CiExpr> expr, bool dynamic)
{
	std::shared_ptr<CiExpr> minExpr = nullptr;
	const CiBinaryExpr * range;
	if ((range = dynamic_cast<const CiBinaryExpr *>(expr.get())) && range->op == CiToken::range) {
		minExpr = range->left;
		expr = range->right;
	}
	bool nullable;
	CiToken ptrModifier;
	std::shared_ptr<CiClassType> outerArray = nullptr;
	CiClassType * innerArray = nullptr;
	for (;;) {
		const CiPostfixExpr * question;
		if ((question = dynamic_cast<const CiPostfixExpr *>(expr.get())) && question->op == CiToken::questionMark) {
			expr = question->inner;
			nullable = true;
		}
		else
			nullable = false;
		const CiPostfixExpr * postfix;
		if ((postfix = dynamic_cast<const CiPostfixExpr *>(expr.get())) && (postfix->op == CiToken::exclamationMark || postfix->op == CiToken::hash)) {
			expr = postfix->inner;
			ptrModifier = postfix->op;
		}
		else
			ptrModifier = CiToken::endOfFile;
		const CiBinaryExpr * binary;
		if ((binary = dynamic_cast<const CiBinaryExpr *>(expr.get())) && binary->op == CiToken::leftBracket) {
			if (binary->right != nullptr) {
				expectNoPtrModifier(expr.get(), ptrModifier, nullable);
				std::shared_ptr<CiExpr> lengthExpr = visitExpr(binary->right);
				std::shared_ptr<CiArrayStorageType> arrayStorage = std::make_shared<CiArrayStorageType>();
				arrayStorage->class_ = this->program->system->arrayStorageClass.get();
				arrayStorage->typeArg0 = outerArray;
				arrayStorage->lengthExpr = lengthExpr;
				arrayStorage->length = 0;
				if (coerce(lengthExpr.get(), this->program->system->intType.get()) && (!dynamic || binary->left->isIndexing())) {
					if (const CiLiteralLong *literal = dynamic_cast<const CiLiteralLong *>(lengthExpr.get())) {
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
				std::shared_ptr<CiType> elementType = outerArray;
				outerArray = createClassPtr(this->program->system->arrayPtrClass.get(), ptrModifier, nullable);
				outerArray->typeArg0 = elementType;
			}
			if (innerArray == nullptr)
				innerArray = outerArray.get();
			expr = binary->left;
		}
		else
			break;
	}
	std::shared_ptr<CiType> baseType;
	if (minExpr != nullptr) {
		expectNoPtrModifier(expr.get(), ptrModifier, nullable);
		int min = foldConstInt(minExpr);
		int max = foldConstInt(expr);
		if (min > max)
			return poisonError(expr.get(), "Range min greater than max");
		baseType = CiRangeType::new_(min, max);
	}
	else
		baseType = toBaseType(expr.get(), ptrModifier, nullable);
	baseType->line = expr->line;
	if (outerArray == nullptr)
		return baseType;
	innerArray->typeArg0 = baseType;
	return outerArray;
}

std::shared_ptr<CiType> CiSema::resolveType(CiNamedValue * def)
{
	def->type = toType(def->typeExpr, false);
	return def->type;
}

void CiSema::visitAssert(CiAssert * statement)
{
	statement->cond = resolveBool(statement->cond);
	if (statement->message != nullptr) {
		statement->message = visitExpr(statement->message);
		if (!dynamic_cast<const CiStringType *>(statement->message->type.get()))
			reportError(statement, "The second argument of 'assert' must be a string");
	}
}

bool CiSema::resolveStatements(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	bool reachable = true;
	for (const std::shared_ptr<CiStatement> &statement : *statements) {
		if (std::shared_ptr<CiConst>konst = std::dynamic_pointer_cast<CiConst>(statement)) {
			resolveConst(konst.get());
			this->currentScope->add(konst);
			if (dynamic_cast<const CiArrayStorageType *>(konst->type.get())) {
				CiClass * klass = static_cast<CiClass *>(this->currentScope->getContainer());
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

void CiSema::visitBlock(CiBlock * statement)
{
	openScope(statement);
	statement->setCompletesNormally(resolveStatements(&statement->statements));
	closeScope();
}

void CiSema::resolveLoopCond(CiLoop * statement)
{
	if (statement->cond != nullptr) {
		statement->cond = resolveBool(statement->cond);
		statement->setCompletesNormally(!dynamic_cast<const CiLiteralTrue *>(statement->cond.get()));
	}
	else
		statement->setCompletesNormally(false);
}

void CiSema::visitDoWhile(CiDoWhile * statement)
{
	openScope(statement);
	resolveLoopCond(statement);
	visitStatement(statement->body);
	closeScope();
}

void CiSema::visitFor(CiFor * statement)
{
	openScope(statement);
	if (statement->init != nullptr)
		visitStatement(statement->init);
	resolveLoopCond(statement);
	if (statement->advance != nullptr)
		visitStatement(statement->advance);
	const CiVar * iter;
	const CiBinaryExpr * cond;
	const CiSymbolReference * limitSymbol;
	if ((iter = dynamic_cast<const CiVar *>(statement->init.get())) && dynamic_cast<const CiIntegerType *>(iter->type.get()) && iter->value != nullptr && (cond = dynamic_cast<const CiBinaryExpr *>(statement->cond.get())) && cond->left->isReferenceTo(iter) && (dynamic_cast<const CiLiteral *>(cond->right.get()) || ((limitSymbol = dynamic_cast<const CiSymbolReference *>(cond->right.get())) && dynamic_cast<const CiVar *>(limitSymbol->symbol)))) {
		int64_t step = 0;
		const CiUnaryExpr * unary;
		const CiBinaryExpr * binary;
		const CiLiteralLong * literalStep;
		if ((unary = dynamic_cast<const CiUnaryExpr *>(statement->advance.get())) && unary->inner != nullptr && unary->inner->isReferenceTo(iter)) {
			switch (unary->op) {
			case CiToken::increment:
				step = 1;
				break;
			case CiToken::decrement:
				step = -1;
				break;
			default:
				break;
			}
		}
		else if ((binary = dynamic_cast<const CiBinaryExpr *>(statement->advance.get())) && binary->left->isReferenceTo(iter) && (literalStep = dynamic_cast<const CiLiteralLong *>(binary->right.get()))) {
			switch (binary->op) {
			case CiToken::addAssign:
				step = literalStep->value;
				break;
			case CiToken::subAssign:
				step = -literalStep->value;
				break;
			default:
				break;
			}
		}
		if ((step > 0 && (cond->op == CiToken::less || cond->op == CiToken::lessOrEqual)) || (step < 0 && (cond->op == CiToken::greater || cond->op == CiToken::greaterOrEqual))) {
			statement->isRange = true;
			statement->rangeStep = step;
		}
		statement->isIteratorUsed = false;
	}
	visitStatement(statement->body);
	closeScope();
}

void CiSema::visitForeach(CiForeach * statement)
{
	openScope(statement);
	CiVar * element = statement->getVar();
	resolveType(element);
	visitExpr(statement->collection);
	if (const CiClassType *klass = dynamic_cast<const CiClassType *>(statement->collection->type.get())) {
		switch (klass->class_->id) {
		case CiId::stringClass:
			if (statement->count() != 1 || !element->type->isAssignableFrom(this->program->system->intType.get()))
				reportError(statement, "Expected int iterator variable");
			break;
		case CiId::arrayStorageClass:
		case CiId::listClass:
		case CiId::hashSetClass:
		case CiId::sortedSetClass:
			if (statement->count() != 1)
				reportError(statement, "Expected one iterator variable");
			else if (!element->type->isAssignableFrom(klass->getElementType().get()))
				reportError(statement, std::format("Cannot coerce {} to {}", klass->getElementType()->toString(), element->type->toString()));
			break;
		case CiId::dictionaryClass:
		case CiId::sortedDictionaryClass:
		case CiId::orderedDictionaryClass:
			if (statement->count() != 2)
				reportError(statement, "Expected (TKey key, TValue value) iterator");
			else {
				CiVar * value = statement->getValueVar();
				resolveType(value);
				if (!element->type->isAssignableFrom(klass->getKeyType()))
					reportError(statement, std::format("Cannot coerce {} to {}", klass->getKeyType()->toString(), element->type->toString()));
				else if (!value->type->isAssignableFrom(klass->getValueType().get()))
					reportError(statement, std::format("Cannot coerce {} to {}", klass->getValueType()->toString(), value->type->toString()));
			}
			break;
		default:
			reportError(statement, std::format("'foreach' invalid on {}", klass->class_->name));
			break;
		}
	}
	else
		reportError(statement, std::format("'foreach' invalid on {}", statement->collection->type->toString()));
	statement->setCompletesNormally(true);
	visitStatement(statement->body);
	closeScope();
}

void CiSema::visitIf(CiIf * statement)
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

void CiSema::visitLock(CiLock * statement)
{
	statement->lock = visitExpr(statement->lock);
	coerce(statement->lock.get(), this->program->system->lockPtrType.get());
	visitStatement(statement->body);
}

void CiSema::visitReturn(CiReturn * statement)
{
	if (this->currentMethod->type->id == CiId::voidType) {
		if (statement->value != nullptr)
			reportError(statement, "Void method cannot return a value");
	}
	else if (statement->value == nullptr)
		reportError(statement, "Missing return value");
	else {
		statement->value = visitExpr(statement->value);
		coerce(statement->value.get(), this->currentMethod->type.get());
		const CiSymbolReference * symbol;
		const CiVar * local;
		if ((symbol = dynamic_cast<const CiSymbolReference *>(statement->value.get())) && (local = dynamic_cast<const CiVar *>(symbol->symbol)) && ((local->type->isFinal() && !dynamic_cast<const CiStorageType *>(this->currentMethod->type.get())) || (local->type->id == CiId::stringStorageType && this->currentMethod->type->id != CiId::stringStorageType)))
			reportError(statement, "Returning dangling reference to local storage");
	}
}

void CiSema::visitSwitch(CiSwitch * statement)
{
	openScope(statement);
	statement->value = visitExpr(statement->value);
	const CiIntegerType * i;
	const CiClassType * klass;
	if (((i = dynamic_cast<const CiIntegerType *>(statement->value->type.get())) && i->id != CiId::longType) || dynamic_cast<const CiEnum *>(statement->value->type.get())) {
	}
	else if ((klass = dynamic_cast<const CiClassType *>(statement->value->type.get())) && !dynamic_cast<const CiStorageType *>(klass)) {
	}
	else {
		reportError(statement->value.get(), std::format("Switch on type {} - expected int, enum, string or object reference", statement->value->type->toString()));
		return;
	}
	statement->setCompletesNormally(false);
	for (CiCase &kase : statement->cases) {
		for (int i = 0; i < kase.values.size(); i++) {
			const CiClassType * switchPtr;
			if ((switchPtr = dynamic_cast<const CiClassType *>(statement->value->type.get())) && switchPtr->class_->id != CiId::stringClass) {
				std::shared_ptr<CiExpr> value = kase.values[i];
				const CiBinaryExpr * when1;
				if ((when1 = dynamic_cast<const CiBinaryExpr *>(value.get())) && when1->op == CiToken::when)
					value = when1->left;
				if (dynamic_cast<const CiLiteralNull *>(value.get())) {
				}
				else {
					std::shared_ptr<CiVar> def;
					if (!(def = std::dynamic_pointer_cast<CiVar>(value)) || def->value != nullptr)
						reportError(kase.values[i].get(), "Expected 'case Type name'");
					else {
						const CiClassType * casePtr;
						if (!(casePtr = dynamic_cast<const CiClassType *>(resolveType(def.get()).get())) || dynamic_cast<const CiStorageType *>(casePtr))
							reportError(def.get(), "'case' with non-reference type");
						else if (dynamic_cast<const CiReadWriteClassType *>(casePtr) && !dynamic_cast<const CiDynamicPtrType *>(switchPtr) && (dynamic_cast<const CiDynamicPtrType *>(casePtr) || !dynamic_cast<const CiReadWriteClassType *>(switchPtr)))
							reportError(def.get(), std::format("{} cannot be casted to {}", switchPtr->toString(), casePtr->toString()));
						else if (casePtr->class_->isSameOrBaseOf(switchPtr->class_))
							reportError(def.get(), std::format("{} is {}, 'case {}' would always match", statement->value->toString(), switchPtr->toString(), casePtr->toString()));
						else if (!switchPtr->class_->isSameOrBaseOf(casePtr->class_))
							reportError(def.get(), std::format("{} is not base class of {}, 'case {}' would never match", switchPtr->toString(), casePtr->class_->name, casePtr->toString()));
						else {
							statement->add(def);
							CiBinaryExpr * when2;
							if ((when2 = dynamic_cast<CiBinaryExpr *>(kase.values[i].get())) && when2->op == CiToken::when)
								when2->right = resolveBool(when2->right);
						}
					}
				}
			}
			else {
				CiBinaryExpr * when1;
				if ((when1 = dynamic_cast<CiBinaryExpr *>(kase.values[i].get())) && when1->op == CiToken::when) {
					when1->left = foldConst(when1->left);
					coerce(when1->left.get(), statement->value->type.get());
					when1->right = resolveBool(when1->right);
				}
				else {
					kase.values[i] = foldConst(kase.values[i]);
					coerce(kase.values[i].get(), statement->value->type.get());
				}
			}
		}
		if (resolveStatements(&kase.body))
			reportError(kase.body.back().get(), "Case must end with break, continue, return or throw");
	}
	if (statement->defaultBody.size() > 0) {
		bool reachable = resolveStatements(&statement->defaultBody);
		if (reachable)
			reportError(statement->defaultBody.back().get(), "Default must end with break, continue, return or throw");
	}
	closeScope();
}

void CiSema::visitThrow(CiThrow * statement)
{
	if (!this->currentMethod->throws)
		reportError(statement, "'throw' in a method not marked 'throws'");
	statement->message = visitExpr(statement->message);
	if (!dynamic_cast<const CiStringType *>(statement->message->type.get()))
		reportError(statement, "The argument of 'throw' must be a string");
}

void CiSema::visitWhile(CiWhile * statement)
{
	openScope(statement);
	resolveLoopCond(statement);
	visitStatement(statement->body);
	closeScope();
}

void CiSema::visitStatement(std::shared_ptr<CiStatement> statement)
{
	if (CiAssert *asrt = dynamic_cast<CiAssert *>(statement.get()))
		visitAssert(asrt);
	else if (CiBlock *block = dynamic_cast<CiBlock *>(statement.get()))
		visitBlock(block);
	else if (const CiBreak *brk = dynamic_cast<const CiBreak *>(statement.get()))
		brk->loopOrSwitch->setCompletesNormally(true);
	else if (dynamic_cast<const CiContinue *>(statement.get()) || dynamic_cast<const CiNative *>(statement.get())) {
	}
	else if (CiDoWhile *doWhile = dynamic_cast<CiDoWhile *>(statement.get()))
		visitDoWhile(doWhile);
	else if (CiFor *forLoop = dynamic_cast<CiFor *>(statement.get()))
		visitFor(forLoop);
	else if (CiForeach *foreachLoop = dynamic_cast<CiForeach *>(statement.get()))
		visitForeach(foreachLoop);
	else if (CiIf *ifStatement = dynamic_cast<CiIf *>(statement.get()))
		visitIf(ifStatement);
	else if (CiLock *lockStatement = dynamic_cast<CiLock *>(statement.get()))
		visitLock(lockStatement);
	else if (CiReturn *ret = dynamic_cast<CiReturn *>(statement.get()))
		visitReturn(ret);
	else if (CiSwitch *switchStatement = dynamic_cast<CiSwitch *>(statement.get()))
		visitSwitch(switchStatement);
	else if (CiThrow *throwStatement = dynamic_cast<CiThrow *>(statement.get()))
		visitThrow(throwStatement);
	else if (CiWhile *whileStatement = dynamic_cast<CiWhile *>(statement.get()))
		visitWhile(whileStatement);
	else if (std::shared_ptr<CiExpr>expr = std::dynamic_pointer_cast<CiExpr>(statement))
		visitExpr(expr);
	else
		std::abort();
}

std::shared_ptr<CiExpr> CiSema::foldConst(std::shared_ptr<CiExpr> expr)
{
	expr = visitExpr(expr);
	if (dynamic_cast<const CiLiteral *>(expr.get()) || expr->isConstEnum())
		return expr;
	reportError(expr.get(), "Expected constant value");
	return expr;
}

int CiSema::foldConstInt(std::shared_ptr<CiExpr> expr)
{
	if (const CiLiteralLong *literal = dynamic_cast<const CiLiteralLong *>(foldConst(expr).get())) {
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

void CiSema::resolveTypes(CiClass * klass)
{
	this->currentScope = klass;
	for (CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (CiField *field = dynamic_cast<CiField *>(symbol)) {
			CiType * type = resolveType(field).get();
			if (field->value != nullptr) {
				field->value = visitExpr(field->value);
				if (!field->isAssignableStorage()) {
					const CiArrayStorageType * array;
					coerce(field->value.get(), (array = dynamic_cast<const CiArrayStorageType *>(type)) ? array->getElementType().get() : type);
				}
			}
		}
		else if (CiMethod *method = dynamic_cast<CiMethod *>(symbol)) {
			if (method->typeExpr == this->program->system->voidType)
				method->type = this->program->system->voidType;
			else
				resolveType(method);
			for (CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
				resolveType(param);
				if (param->value != nullptr) {
					param->value = foldConst(param->value);
					coerce(param->value.get(), param->type.get());
				}
			}
			if (method->callType == CiCallType::override_ || method->callType == CiCallType::sealed) {
				if (CiMethod *baseMethod = dynamic_cast<CiMethod *>(klass->parent->tryLookup(method->name, false).get())) {
					switch (baseMethod->callType) {
					case CiCallType::abstract:
					case CiCallType::virtual_:
					case CiCallType::override_:
						break;
					default:
						reportError(method, "Base method is not abstract or virtual");
						break;
					}
					if (!method->type->equalsType(baseMethod->type.get()))
						reportError(method, "Base method has a different return type");
					if (method->isMutator != baseMethod->isMutator) {
						if (method->isMutator)
							reportError(method, "Mutating method cannot override a non-mutating method");
						else
							reportError(method, "Non-mutating method cannot override a mutating method");
					}
					const CiVar * baseParam = baseMethod->parameters.firstParameter();
					for (const CiVar * param = method->parameters.firstParameter();; param = param->nextParameter()) {
						if (param == nullptr) {
							if (baseParam != nullptr)
								reportError(method, "Fewer parameters than the overridden method");
							break;
						}
						if (baseParam == nullptr) {
							reportError(method, "More parameters than the overridden method");
							break;
						}
						if (!param->type->equalsType(baseParam->type.get())) {
							reportError(method, "Base method has a different parameter type");
							break;
						}
						baseParam = baseParam->nextParameter();
					}
					baseMethod->calls.insert(method);
				}
				else
					reportError(method, "No method to override");
			}
		}
	}
}

void CiSema::resolveConst(CiConst * konst)
{
	switch (konst->visitStatus) {
	case CiVisitStatus::notYet:
		break;
	case CiVisitStatus::inProgress:
		konst->value = poisonError(konst, std::format("Circular dependency in value of constant {}", konst->name));
		konst->visitStatus = CiVisitStatus::done;
		return;
	case CiVisitStatus::done:
		return;
	}
	konst->visitStatus = CiVisitStatus::inProgress;
	if (!dynamic_cast<const CiEnum *>(this->currentScope))
		resolveType(konst);
	konst->value = visitExpr(konst->value);
	if (CiAggregateInitializer *coll = dynamic_cast<CiAggregateInitializer *>(konst->value.get())) {
		if (const CiClassType *array = dynamic_cast<const CiClassType *>(konst->type.get())) {
			std::shared_ptr<CiType> elementType = array->getElementType();
			if (const CiArrayStorageType *arrayStg = dynamic_cast<const CiArrayStorageType *>(array)) {
				if (arrayStg->length != coll->items.size())
					reportError(konst, std::format("Declared {} elements, initialized {}", arrayStg->length, coll->items.size()));
			}
			else if (dynamic_cast<const CiReadWriteClassType *>(array))
				reportError(konst, "Invalid constant type");
			else {
				std::shared_ptr<CiArrayStorageType> citemp0 = std::make_shared<CiArrayStorageType>();
				citemp0->class_ = this->program->system->arrayStorageClass.get();
				citemp0->typeArg0 = elementType;
				citemp0->length = coll->items.size();
				konst->type = citemp0;
			}
			coll->type = konst->type;
			for (const std::shared_ptr<CiExpr> &item : coll->items)
				coerce(item.get(), elementType.get());
		}
		else
			reportError(konst, std::format("Array initializer for scalar constant {}", konst->name));
	}
	else if (dynamic_cast<const CiEnum *>(this->currentScope) && dynamic_cast<const CiRangeType *>(konst->value->type.get()) && dynamic_cast<const CiLiteral *>(konst->value.get())) {
	}
	else if (dynamic_cast<const CiLiteral *>(konst->value.get()) || konst->value->isConstEnum())
		coerce(konst->value.get(), konst->type.get());
	else if (konst->value != this->poison)
		reportError(konst->value.get(), std::format("Value for constant {} is not constant", konst->name));
	konst->inMethod = this->currentMethod;
	konst->visitStatus = CiVisitStatus::done;
}

void CiSema::resolveConsts(CiContainerType * container)
{
	this->currentScope = container;
	if (const CiClass *klass = dynamic_cast<const CiClass *>(container))
		for (CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
			if (CiConst *konst = dynamic_cast<CiConst *>(symbol))
				resolveConst(konst);
		}
	else if (CiEnum *enu = dynamic_cast<CiEnum *>(container)) {
		const CiConst * previous = nullptr;
		for (CiSymbol * symbol = enu->first; symbol != nullptr; symbol = symbol->next) {
			if (CiConst *konst = dynamic_cast<CiConst *>(symbol)) {
				if (konst->value != nullptr) {
					resolveConst(konst);
					enu->hasExplicitValue = true;
				}
				else {
					std::shared_ptr<CiImplicitEnumValue> citemp0 = std::make_shared<CiImplicitEnumValue>();
					citemp0->value = previous == nullptr ? 0 : previous->value->intValue() + 1;
					konst->value = citemp0;
				}
				previous = konst;
			}
		}
	}
	else
		std::abort();
}

void CiSema::resolveCode(CiClass * klass)
{
	if (klass->constructor != nullptr) {
		this->currentScope = klass;
		this->currentMethod = klass->constructor.get();
		visitStatement(klass->constructor->body);
		this->currentMethod = nullptr;
	}
	for (CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (CiMethod *method = dynamic_cast<CiMethod *>(symbol)) {
			if (method->name == "ToString" && method->callType != CiCallType::static_ && method->parameters.count() == 0)
				method->id = CiId::classToString;
			if (method->body != nullptr) {
				this->currentScope = &method->parameters;
				this->currentMethod = method;
				if (!dynamic_cast<const CiScope *>(method->body.get()))
					openScope(&method->methodScope);
				visitStatement(method->body);
				if (method->type->id != CiId::voidType && method->body->completesNormally())
					reportError(method->body.get(), "Method can complete without a return value");
				this->currentMethod = nullptr;
			}
		}
	}
}

void CiSema::markMethodLive(CiMethodBase * method)
{
	if (method->isLive)
		return;
	method->isLive = true;
	for (CiMethod * called : method->calls)
		markMethodLive(called);
}

void CiSema::markClassLive(const CiClass * klass)
{
	if (!klass->isPublic)
		return;
	for (CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		CiMethod * method;
		if ((method = dynamic_cast<CiMethod *>(symbol)) && (method->visibility == CiVisibility::public_ || method->visibility == CiVisibility::protected_))
			markMethodLive(method);
	}
	if (klass->constructor != nullptr)
		markMethodLive(klass->constructor.get());
}

void CiSema::process(CiProgram * program)
{
	this->program = program;
	for (CiSymbol * type = program->first; type != nullptr; type = type->next) {
		if (CiClass *klass = dynamic_cast<CiClass *>(type))
			resolveBase(klass);
	}
	for (CiClass * klass : program->classes)
		checkBaseCycle(klass);
	for (CiSymbol * type = program->first; type != nullptr; type = type->next) {
		CiContainerType * container = static_cast<CiContainerType *>(type);
		resolveConsts(container);
	}
	for (CiClass * klass : program->classes)
		resolveTypes(klass);
	for (CiClass * klass : program->classes)
		resolveCode(klass);
	for (const CiClass * klass : program->classes)
		markClassLive(klass);
}

const CiContainerType * GenBase::getCurrentContainer() const
{
	const CiClass * klass = static_cast<const CiClass *>(this->currentMethod->parent);
	return klass;
}

void GenBase::notSupported(const CiStatement * statement, std::string_view feature)
{
	reportError(statement, std::format("{} not supported when targeting {}", feature, getTargetName()));
}

void GenBase::notYet(const CiStatement * statement, std::string_view feature)
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

void GenBase::writeBanner()
{
	writeLine("// Generated automatically with \"cito\". Do not edit.");
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

void GenBase::writeDocPara(const CiDocPara * para, bool many)
{
	if (many) {
		writeNewLine();
		write(" * <p>");
	}
	for (const std::shared_ptr<CiDocInline> &inline_ : para->children) {
		if (const CiDocText *text = dynamic_cast<const CiDocText *>(inline_.get()))
			writeXmlDoc(text->text);
		else if (const CiDocCode *code = dynamic_cast<const CiDocCode *>(inline_.get())) {
			write("<code>");
			writeXmlDoc(code->text);
			write("</code>");
		}
		else if (dynamic_cast<const CiDocLine *>(inline_.get())) {
			writeNewLine();
			startDocLine();
		}
		else
			std::abort();
	}
}

void GenBase::writeDocList(const CiDocList * list)
{
	writeNewLine();
	writeLine(" * <ul>");
	for (const CiDocPara &item : list->items) {
		write(" * <li>");
		writeDocPara(&item, false);
		writeLine("</li>");
	}
	write(" * </ul>");
}

void GenBase::writeDocBlock(const CiDocBlock * block, bool many)
{
	if (const CiDocPara *para = dynamic_cast<const CiDocPara *>(block))
		writeDocPara(para, many);
	else if (const CiDocList *list = dynamic_cast<const CiDocList *>(block))
		writeDocList(list);
	else
		std::abort();
}

void GenBase::writeContent(const CiCodeDoc * doc)
{
	startDocLine();
	writeDocPara(&doc->summary, false);
	writeNewLine();
	if (doc->details.size() > 0) {
		startDocLine();
		if (doc->details.size() == 1)
			writeDocBlock(doc->details[0].get(), false);
		else {
			for (const std::shared_ptr<CiDocBlock> &block : doc->details)
				writeDocBlock(block.get(), true);
		}
		writeNewLine();
	}
}

void GenBase::writeDoc(const CiCodeDoc * doc)
{
	if (doc != nullptr) {
		writeLine("/**");
		writeContent(doc);
		writeLine(" */");
	}
}

void GenBase::writeSelfDoc(const CiMethod * method)
{
}

void GenBase::writeParameterDoc(const CiVar * param, bool first)
{
	write(" * @param ");
	writeName(param);
	writeChar(' ');
	writeDocPara(&param->documentation->summary, false);
	writeNewLine();
}

void GenBase::writeParametersDoc(const CiMethod * method)
{
	bool first = true;
	for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
		if (param->documentation != nullptr) {
			writeParameterDoc(param, first);
			first = false;
		}
	}
}

void GenBase::writeMethodDoc(const CiMethod * method)
{
	if (method->documentation == nullptr)
		return;
	writeLine("/**");
	writeContent(method->documentation.get());
	writeSelfDoc(method);
	writeParametersDoc(method);
	writeLine(" */");
}

void GenBase::writeTopLevelNatives(const CiProgram * program)
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

CiId GenBase::getTypeId(const CiType * type, bool promote) const
{
	return promote && dynamic_cast<const CiRangeType *>(type) ? CiId::intType : type->id;
}

void GenBase::writeLocalName(const CiSymbol * symbol, CiPriority parent)
{
	if (dynamic_cast<const CiField *>(symbol))
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

void GenBase::writePrintfWidth(const CiInterpolatedPart * part)
{
	if (part->widthExpr != nullptr)
		visitLiteralLong(part->width);
	if (part->precision >= 0) {
		writeChar('.');
		visitLiteralLong(part->precision);
	}
}

int GenBase::getPrintfFormat(const CiType * type, int format)
{
	if (dynamic_cast<const CiIntegerType *>(type))
		return format == 'x' || format == 'X' ? format : 'd';
	else if (dynamic_cast<const CiNumericType *>(type)) {
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
	else if (dynamic_cast<const CiClassType *>(type))
		return 's';
	else
		std::abort();
}

void GenBase::writePrintfFormat(const CiInterpolatedString * expr)
{
	for (const CiInterpolatedPart &part : expr->parts) {
		writeDoubling(part.prefix, '%');
		writeChar('%');
		writePrintfWidth(&part);
		writeChar(getPrintfFormat(part.argument->type.get(), part.format));
	}
	writeDoubling(expr->suffix, '%');
}

void GenBase::writePyFormat(const CiInterpolatedPart * part)
{
	if (part->widthExpr != nullptr || part->precision >= 0 || (part->format != ' ' && part->format != 'D'))
		writeChar(':');
	if (part->widthExpr != nullptr) {
		if (part->width >= 0) {
			if (!dynamic_cast<const CiNumericType *>(part->argument->type.get()))
				writeChar('>');
			visitLiteralLong(part->width);
		}
		else {
			writeChar('<');
			visitLiteralLong(-part->width);
		}
	}
	if (part->precision >= 0) {
		writeChar(dynamic_cast<const CiIntegerType *>(part->argument->type.get()) ? '0' : '.');
		visitLiteralLong(part->precision);
	}
	if (part->format != ' ' && part->format != 'D')
		writeChar(part->format);
	writeChar('}');
}

void GenBase::writeInterpolatedStringArg(const CiExpr * expr)
{
	expr->accept(this, CiPriority::argument);
}

void GenBase::writeInterpolatedStringArgs(const CiInterpolatedString * expr)
{
	for (const CiInterpolatedPart &part : expr->parts) {
		write(", ");
		writeInterpolatedStringArg(part.argument.get());
	}
}

void GenBase::writePrintf(const CiInterpolatedString * expr, bool newLine)
{
	writeChar('"');
	writePrintfFormat(expr);
	if (newLine)
		write("\\n");
	writeChar('"');
	writeInterpolatedStringArgs(expr);
	writeChar(')');
}

void GenBase::writePostfix(const CiExpr * obj, std::string_view s)
{
	obj->accept(this, CiPriority::primary);
	write(s);
}

void GenBase::writeCall(std::string_view function, const CiExpr * arg0, const CiExpr * arg1, const CiExpr * arg2)
{
	write(function);
	writeChar('(');
	arg0->accept(this, CiPriority::argument);
	if (arg1 != nullptr) {
		write(", ");
		arg1->accept(this, CiPriority::argument);
		if (arg2 != nullptr) {
			write(", ");
			arg2->accept(this, CiPriority::argument);
		}
	}
	writeChar(')');
}

void GenBase::writeMemberOp(const CiExpr * left, const CiSymbolReference * symbol)
{
	writeChar('.');
}

void GenBase::writeMethodCall(const CiExpr * obj, std::string_view method, const CiExpr * arg0, const CiExpr * arg1)
{
	obj->accept(this, CiPriority::primary);
	writeMemberOp(obj, nullptr);
	writeCall(method, arg0, arg1);
}

void GenBase::writeSelectValues(const CiType * type, const CiSelectExpr * expr)
{
	writeCoerced(type, expr->onTrue.get(), CiPriority::select);
	write(" : ");
	writeCoerced(type, expr->onFalse.get(), CiPriority::select);
}

void GenBase::writeCoercedSelect(const CiType * type, const CiSelectExpr * expr, CiPriority parent)
{
	if (parent > CiPriority::select)
		writeChar('(');
	expr->cond->accept(this, CiPriority::selectCond);
	write(" ? ");
	writeSelectValues(type, expr);
	if (parent > CiPriority::select)
		writeChar(')');
}

void GenBase::writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent)
{
	expr->accept(this, parent);
}

void GenBase::writeCoerced(const CiType * type, const CiExpr * expr, CiPriority parent)
{
	if (const CiSelectExpr *select = dynamic_cast<const CiSelectExpr *>(expr))
		writeCoercedSelect(type, select, parent);
	else
		writeCoercedInternal(type, expr, parent);
}

void GenBase::writeCoercedExpr(const CiType * type, const CiExpr * expr)
{
	writeCoerced(type, expr, CiPriority::argument);
}

void GenBase::writeStronglyCoerced(const CiType * type, const CiExpr * expr)
{
	writeCoerced(type, expr, CiPriority::argument);
}

void GenBase::writeCoercedLiteral(const CiType * type, const CiExpr * literal)
{
	literal->accept(this, CiPriority::argument);
}

void GenBase::writeCoercedLiterals(const CiType * type, const std::vector<std::shared_ptr<CiExpr>> * exprs)
{
	for (int i = 0; i < exprs->size(); i++) {
		writeComma(i);
		writeCoercedLiteral(type, (*exprs)[i].get());
	}
}

void GenBase::writeArgs(const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	const CiVar * param = method->parameters.firstParameter();
	bool first = true;
	for (const std::shared_ptr<CiExpr> &arg : *args) {
		if (!first)
			write(", ");
		first = false;
		writeStronglyCoerced(param->type.get(), arg.get());
		param = param->nextParameter();
	}
}

void GenBase::writeArgsInParentheses(const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	writeChar('(');
	writeArgs(method, args);
	writeChar(')');
}

void GenBase::writeNewArrayStorage(const CiArrayStorageType * array)
{
	writeNewArray(array->getElementType().get(), array->lengthExpr.get(), CiPriority::argument);
}

void GenBase::writeNewStorage(const CiType * type)
{
	if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(type))
		writeNewArrayStorage(array);
	else if (const CiStorageType *storage = dynamic_cast<const CiStorageType *>(type))
		writeNew(storage, CiPriority::argument);
	else
		std::abort();
}

void GenBase::writeArrayStorageInit(const CiArrayStorageType * array, const CiExpr * value)
{
	write(" = ");
	writeNewArrayStorage(array);
}

void GenBase::writeNewWithFields(const CiReadWriteClassType * type, const CiAggregateInitializer * init)
{
	writeNew(type, CiPriority::argument);
}

void GenBase::writeStorageInit(const CiNamedValue * def)
{
	write(" = ");
	if (const CiAggregateInitializer *init = dynamic_cast<const CiAggregateInitializer *>(def->value.get())) {
		const CiReadWriteClassType * klass = static_cast<const CiReadWriteClassType *>(def->type.get());
		writeNewWithFields(klass, init);
	}
	else
		writeNewStorage(def->type.get());
}

void GenBase::writeVarInit(const CiNamedValue * def)
{
	if (def->isAssignableStorage()) {
	}
	else if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(def->type.get()))
		writeArrayStorageInit(array, def->value.get());
	else if (def->value != nullptr && !dynamic_cast<const CiAggregateInitializer *>(def->value.get())) {
		write(" = ");
		writeCoercedExpr(def->type.get(), def->value.get());
	}
	else if (def->type->isFinal() && !dynamic_cast<const CiParameters *>(def->parent))
		writeStorageInit(def);
}

void GenBase::writeVar(const CiNamedValue * def)
{
	writeTypeAndName(def);
	writeVarInit(def);
}

void GenBase::visitVar(const CiVar * expr)
{
	writeVar(expr);
}

void GenBase::writeObjectLiteral(const CiAggregateInitializer * init, std::string_view separator)
{
	std::string_view prefix = " { ";
	for (const std::shared_ptr<CiExpr> &item : init->items) {
		write(prefix);
		const CiBinaryExpr * assign = static_cast<const CiBinaryExpr *>(item.get());
		const CiSymbolReference * field = static_cast<const CiSymbolReference *>(assign->left.get());
		writeName(field->symbol);
		write(separator);
		writeCoerced(assign->left->type.get(), assign->right.get(), CiPriority::argument);
		prefix = ", ";
	}
	write(" }");
}

const CiAggregateInitializer * GenBase::getAggregateInitializer(const CiNamedValue * def)
{
	const CiExpr * expr = def->value.get();
	if (const CiPrefixExpr *unary = dynamic_cast<const CiPrefixExpr *>(expr))
		expr = unary->inner.get();
	const CiAggregateInitializer * init;
	return (init = dynamic_cast<const CiAggregateInitializer *>(expr)) ? init : nullptr;
}

void GenBase::writeAggregateInitField(const CiExpr * obj, const CiExpr * item)
{
	const CiBinaryExpr * assign = static_cast<const CiBinaryExpr *>(item);
	const CiSymbolReference * field = static_cast<const CiSymbolReference *>(assign->left.get());
	writeMemberOp(obj, field);
	writeName(field->symbol);
	write(" = ");
	writeCoerced(field->type.get(), assign->right.get(), CiPriority::argument);
	endStatement();
}

void GenBase::writeInitCode(const CiNamedValue * def)
{
	const CiAggregateInitializer * init = getAggregateInitializer(def);
	if (init != nullptr) {
		for (const std::shared_ptr<CiExpr> &item : init->items) {
			writeLocalName(def, CiPriority::primary);
			writeAggregateInitField(def, item.get());
		}
	}
}

void GenBase::defineIsVar(const CiBinaryExpr * binary)
{
	if (const CiVar *def = dynamic_cast<const CiVar *>(binary->right.get())) {
		ensureChildBlock();
		writeVar(def);
		endStatement();
	}
}

void GenBase::writeArrayElement(const CiNamedValue * def, int nesting)
{
	writeLocalName(def, CiPriority::primary);
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

void GenBase::writeResourceName(std::string_view name)
{
	for (int c : name)
		writeChar(CiLexer::isLetterOrDigit(c) ? c : '_');
}

void GenBase::visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::increment:
		write("++");
		break;
	case CiToken::decrement:
		write("--");
		break;
	case CiToken::minus:
		writeChar('-');
		{
			const CiPrefixExpr * inner;
			if ((inner = dynamic_cast<const CiPrefixExpr *>(expr->inner.get())) && (inner->op == CiToken::minus || inner->op == CiToken::decrement))
				writeChar(' ');
			break;
		}
	case CiToken::tilde:
		writeChar('~');
		break;
	case CiToken::exclamationMark:
		writeChar('!');
		break;
	case CiToken::new_:
		{
			const CiDynamicPtrType * dynamic = static_cast<const CiDynamicPtrType *>(expr->type.get());
			if (dynamic->class_->id == CiId::arrayPtrClass)
				writeNewArray(dynamic->getElementType().get(), expr->inner.get(), parent);
			else if (const CiAggregateInitializer *init = dynamic_cast<const CiAggregateInitializer *>(expr->inner.get())) {
				int tempId = [](const std::vector<const CiExpr *> &v, const CiExpr * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->currentTemporaries, expr);
				if (tempId >= 0) {
					write("citemp");
					visitLiteralLong(tempId);
				}
				else
					writeNewWithFields(dynamic, init);
			}
			else
				writeNew(dynamic, parent);
			return;
		}
	case CiToken::resource:
		{
			const CiLiteralString * name = static_cast<const CiLiteralString *>(expr->inner.get());
			const CiArrayStorageType * array = static_cast<const CiArrayStorageType *>(expr->type.get());
			writeResource(name->value, array->length);
			return;
		}
	default:
		std::abort();
	}
	expr->inner->accept(this, CiPriority::primary);
}

void GenBase::visitPostfixExpr(const CiPostfixExpr * expr, CiPriority parent)
{
	expr->inner->accept(this, CiPriority::primary);
	switch (expr->op) {
	case CiToken::increment:
		write("++");
		break;
	case CiToken::decrement:
		write("--");
		break;
	default:
		std::abort();
	}
}

void GenBase::startAdd(const CiExpr * expr)
{
	if (!expr->isLiteralZero()) {
		expr->accept(this, CiPriority::add);
		write(" + ");
	}
}

void GenBase::writeAdd(const CiExpr * left, const CiExpr * right)
{
	if (const CiLiteralLong *leftLiteral = dynamic_cast<const CiLiteralLong *>(left)) {
		int64_t leftValue = leftLiteral->value;
		if (leftValue == 0) {
			right->accept(this, CiPriority::argument);
			return;
		}
		if (const CiLiteralLong *rightLiteral = dynamic_cast<const CiLiteralLong *>(right)) {
			visitLiteralLong(leftValue + rightLiteral->value);
			return;
		}
	}
	else if (right->isLiteralZero()) {
		left->accept(this, CiPriority::argument);
		return;
	}
	left->accept(this, CiPriority::add);
	write(" + ");
	right->accept(this, CiPriority::add);
}

void GenBase::writeStartEnd(const CiExpr * startIndex, const CiExpr * length)
{
	startIndex->accept(this, CiPriority::argument);
	write(", ");
	writeAdd(startIndex, length);
}

bool GenBase::isBitOp(CiPriority parent)
{
	switch (parent) {
	case CiPriority::or_:
	case CiPriority::xor_:
	case CiPriority::and_:
	case CiPriority::shift:
		return true;
	default:
		return false;
	}
}

void GenBase::writeBinaryOperand(const CiExpr * expr, CiPriority parent, const CiBinaryExpr * binary)
{
	expr->accept(this, parent);
}

void GenBase::writeBinaryExpr(const CiBinaryExpr * expr, bool parentheses, CiPriority left, std::string_view op, CiPriority right)
{
	if (parentheses)
		writeChar('(');
	writeBinaryOperand(expr->left.get(), left, expr);
	write(op);
	writeBinaryOperand(expr->right.get(), right, expr);
	if (parentheses)
		writeChar(')');
}

void GenBase::writeBinaryExpr2(const CiBinaryExpr * expr, CiPriority parent, CiPriority child, std::string_view op)
{
	writeBinaryExpr(expr, parent > child, child, op, child);
}

void GenBase::writeRel(const CiBinaryExpr * expr, CiPriority parent, std::string_view op)
{
	writeBinaryExpr(expr, parent > CiPriority::condAnd, CiPriority::rel, op, CiPriority::rel);
}

std::string_view GenBase::getEqOp(bool not_)
{
	return not_ ? " != " : " == ";
}

void GenBase::writeEqualOperand(const CiExpr * expr, const CiExpr * other)
{
	expr->accept(this, CiPriority::equality);
}

void GenBase::writeEqualExpr(const CiExpr * left, const CiExpr * right, CiPriority parent, std::string_view op)
{
	if (parent > CiPriority::condAnd)
		writeChar('(');
	writeEqualOperand(left, right);
	write(op);
	writeEqualOperand(right, left);
	if (parent > CiPriority::condAnd)
		writeChar(')');
}

void GenBase::writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	writeEqualExpr(left, right, parent, getEqOp(not_));
}

void GenBase::writeAnd(const CiBinaryExpr * expr, CiPriority parent)
{
	writeBinaryExpr(expr, parent > CiPriority::condAnd && parent != CiPriority::and_, CiPriority::and_, " & ", CiPriority::and_);
}

void GenBase::writeAssignRight(const CiBinaryExpr * expr)
{
	writeCoerced(expr->left->type.get(), expr->right.get(), CiPriority::argument);
}

void GenBase::writeAssign(const CiBinaryExpr * expr, CiPriority parent)
{
	if (parent > CiPriority::assign)
		writeChar('(');
	expr->left->accept(this, CiPriority::assign);
	write(" = ");
	writeAssignRight(expr);
	if (parent > CiPriority::assign)
		writeChar(')');
}

void GenBase::writeIndexing(const CiExpr * collection, const CiExpr * index)
{
	collection->accept(this, CiPriority::primary);
	writeChar('[');
	index->accept(this, CiPriority::argument);
	writeChar(']');
}

void GenBase::writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	writeIndexing(expr->left.get(), expr->right.get());
}

std::string_view GenBase::getIsOperator() const
{
	return " is ";
}

void GenBase::visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::plus:
		writeBinaryExpr(expr, parent > CiPriority::add || isBitOp(parent), CiPriority::add, " + ", CiPriority::add);
		break;
	case CiToken::minus:
		writeBinaryExpr(expr, parent > CiPriority::add || isBitOp(parent), CiPriority::add, " - ", CiPriority::mul);
		break;
	case CiToken::asterisk:
		writeBinaryExpr2(expr, parent, CiPriority::mul, " * ");
		break;
	case CiToken::slash:
		writeBinaryExpr(expr, parent > CiPriority::mul, CiPriority::mul, " / ", CiPriority::primary);
		break;
	case CiToken::mod:
		writeBinaryExpr(expr, parent > CiPriority::mul, CiPriority::mul, " % ", CiPriority::primary);
		break;
	case CiToken::shiftLeft:
		writeBinaryExpr(expr, parent > CiPriority::shift, CiPriority::shift, " << ", CiPriority::mul);
		break;
	case CiToken::shiftRight:
		writeBinaryExpr(expr, parent > CiPriority::shift, CiPriority::shift, " >> ", CiPriority::mul);
		break;
	case CiToken::less:
		writeRel(expr, parent, " < ");
		break;
	case CiToken::lessOrEqual:
		writeRel(expr, parent, " <= ");
		break;
	case CiToken::greater:
		writeRel(expr, parent, " > ");
		break;
	case CiToken::greaterOrEqual:
		writeRel(expr, parent, " >= ");
		break;
	case CiToken::equal:
		writeEqual(expr->left.get(), expr->right.get(), parent, false);
		break;
	case CiToken::notEqual:
		writeEqual(expr->left.get(), expr->right.get(), parent, true);
		break;
	case CiToken::and_:
		writeAnd(expr, parent);
		break;
	case CiToken::or_:
		writeBinaryExpr2(expr, parent, CiPriority::or_, " | ");
		break;
	case CiToken::xor_:
		writeBinaryExpr(expr, parent > CiPriority::xor_ || parent == CiPriority::or_, CiPriority::xor_, " ^ ", CiPriority::xor_);
		break;
	case CiToken::condAnd:
		writeBinaryExpr(expr, parent > CiPriority::condAnd || parent == CiPriority::condOr, CiPriority::condAnd, " && ", CiPriority::condAnd);
		break;
	case CiToken::condOr:
		writeBinaryExpr2(expr, parent, CiPriority::condOr, " || ");
		break;
	case CiToken::assign:
		writeAssign(expr, parent);
		break;
	case CiToken::addAssign:
	case CiToken::subAssign:
	case CiToken::mulAssign:
	case CiToken::divAssign:
	case CiToken::modAssign:
	case CiToken::shiftLeftAssign:
	case CiToken::shiftRightAssign:
	case CiToken::andAssign:
	case CiToken::orAssign:
	case CiToken::xorAssign:
		if (parent > CiPriority::assign)
			writeChar('(');
		expr->left->accept(this, CiPriority::assign);
		writeChar(' ');
		write(expr->getOpString());
		writeChar(' ');
		expr->right->accept(this, CiPriority::argument);
		if (parent > CiPriority::assign)
			writeChar(')');
		break;
	case CiToken::leftBracket:
		if (dynamic_cast<const CiStringType *>(expr->left->type.get()))
			writeCharAt(expr);
		else
			writeIndexingExpr(expr, parent);
		break;
	case CiToken::is:
		if (parent > CiPriority::rel)
			writeChar('(');
		expr->left->accept(this, CiPriority::rel);
		write(getIsOperator());
		if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr->right.get()))
			writeName(symbol->symbol);
		else if (const CiVar *def = dynamic_cast<const CiVar *>(expr->right.get()))
			writeTypeAndName(def);
		else
			std::abort();
		if (parent > CiPriority::rel)
			writeChar(')');
		break;
	case CiToken::when:
		expr->left->accept(this, CiPriority::argument);
		write(" when ");
		expr->right->accept(this, CiPriority::argument);
		break;
	default:
		std::abort();
	}
}

bool GenBase::isReferenceTo(const CiExpr * expr, CiId id)
{
	const CiSymbolReference * symbol;
	return (symbol = dynamic_cast<const CiSymbolReference *>(expr)) && symbol->symbol->id == id;
}

bool GenBase::writeJavaMatchProperty(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::matchStart:
		writePostfix(expr->left.get(), ".start()");
		return true;
	case CiId::matchEnd:
		writePostfix(expr->left.get(), ".end()");
		return true;
	case CiId::matchLength:
		if (parent > CiPriority::add)
			writeChar('(');
		writePostfix(expr->left.get(), ".end() - ");
		writePostfix(expr->left.get(), ".start()");
		if (parent > CiPriority::add)
			writeChar(')');
		return true;
	case CiId::matchValue:
		writePostfix(expr->left.get(), ".group()");
		return true;
	default:
		return false;
	}
}

void GenBase::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	if (expr->left == nullptr)
		writeLocalName(expr->symbol, parent);
	else if (expr->symbol->id == CiId::stringLength)
		writeStringLength(expr->left.get());
	else {
		expr->left->accept(this, CiPriority::primary);
		writeMemberOp(expr->left.get(), expr);
		writeName(expr->symbol);
	}
}

void GenBase::writeNotPromoted(const CiType * type, const CiExpr * expr)
{
	expr->accept(this, CiPriority::argument);
}

void GenBase::writeEnumAsInt(const CiExpr * expr, CiPriority parent)
{
	expr->accept(this, parent);
}

void GenBase::writeEnumHasFlag(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	if (parent > CiPriority::equality)
		writeChar('(');
	int i = (*args)[0]->intValue();
	if ((i & (i - 1)) == 0 && i != 0) {
		writeChar('(');
		writeEnumAsInt(obj, CiPriority::and_);
		write(" & ");
		writeEnumAsInt((*args)[0].get(), CiPriority::and_);
		write(") != 0");
	}
	else {
		write("(~");
		writeEnumAsInt(obj, CiPriority::primary);
		write(" & ");
		writeEnumAsInt((*args)[0].get(), CiPriority::and_);
		write(") == 0");
	}
	if (parent > CiPriority::equality)
		writeChar(')');
}

void GenBase::writeTryParseRadix(const std::vector<std::shared_ptr<CiExpr>> * args)
{
	write(", ");
	if (args->size() == 2)
		(*args)[1]->accept(this, CiPriority::argument);
	else
		write("10");
}

void GenBase::writeListAdd(const CiExpr * obj, std::string_view method, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	obj->accept(this, CiPriority::primary);
	writeChar('.');
	write(method);
	writeChar('(');
	const CiType * elementType = obj->type->asClassType()->getElementType().get();
	if (args->size() == 0)
		writeNewStorage(elementType);
	else
		writeNotPromoted(elementType, (*args)[0].get());
	writeChar(')');
}

void GenBase::writeListInsert(const CiExpr * obj, std::string_view method, const std::vector<std::shared_ptr<CiExpr>> * args, std::string_view separator)
{
	obj->accept(this, CiPriority::primary);
	writeChar('.');
	write(method);
	writeChar('(');
	(*args)[0]->accept(this, CiPriority::argument);
	write(separator);
	const CiType * elementType = obj->type->asClassType()->getElementType().get();
	if (args->size() == 1)
		writeNewStorage(elementType);
	else
		writeNotPromoted(elementType, (*args)[1].get());
	writeChar(')');
}

void GenBase::writeDictionaryAdd(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	writeIndexing(obj, (*args)[0].get());
	write(" = ");
	writeNewStorage(obj->type->asClassType()->getValueType().get());
}

void GenBase::writeClampAsMinMax(const std::vector<std::shared_ptr<CiExpr>> * args)
{
	(*args)[0]->accept(this, CiPriority::argument);
	write(", ");
	(*args)[1]->accept(this, CiPriority::argument);
	write("), ");
	(*args)[2]->accept(this, CiPriority::argument);
	writeChar(')');
}

RegexOptions GenBase::getRegexOptions(const std::vector<std::shared_ptr<CiExpr>> * args) const
{
	const CiExpr * expr = args->back().get();
	if (dynamic_cast<const CiEnum *>(expr->type.get()))
		return static_cast<RegexOptions>(expr->intValue());
	return RegexOptions::none;
}

bool GenBase::writeRegexOptions(const std::vector<std::shared_ptr<CiExpr>> * args, std::string_view prefix, std::string_view separator, std::string_view suffix, std::string_view i, std::string_view m, std::string_view s)
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

void GenBase::visitCallExpr(const CiCallExpr * expr, CiPriority parent)
{
	const CiMethod * method = static_cast<const CiMethod *>(expr->method->symbol);
	writeCallExpr(expr->method->left.get(), method, &expr->arguments, parent);
}

void GenBase::visitSelectExpr(const CiSelectExpr * expr, CiPriority parent)
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

void GenBase::defineObjectLiteralTemporary(const CiUnaryExpr * expr)
{
	if (const CiAggregateInitializer *init = dynamic_cast<const CiAggregateInitializer *>(expr->inner.get())) {
		ensureChildBlock();
		int id = [](const std::vector<const CiExpr *> &v, const CiExpr * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->currentTemporaries, expr->type.get());
		if (id < 0) {
			id = this->currentTemporaries.size();
			startTemporaryVar(expr->type.get());
			this->currentTemporaries.push_back(expr);
		}
		else
			this->currentTemporaries[id] = expr;
		write("citemp");
		visitLiteralLong(id);
		write(" = ");
		const CiDynamicPtrType * dynamic = static_cast<const CiDynamicPtrType *>(expr->type.get());
		writeNew(dynamic, CiPriority::argument);
		endStatement();
		for (const std::shared_ptr<CiExpr> &item : init->items) {
			write("citemp");
			visitLiteralLong(id);
			writeAggregateInitField(expr, item.get());
		}
	}
}

void GenBase::writeTemporaries(const CiExpr * expr)
{
	if (const CiVar *def = dynamic_cast<const CiVar *>(expr)) {
		if (def->value != nullptr) {
			const CiUnaryExpr * unary;
			if ((unary = dynamic_cast<const CiUnaryExpr *>(def->value.get())) && dynamic_cast<const CiAggregateInitializer *>(unary->inner.get()))
				writeTemporaries(unary->inner.get());
			else
				writeTemporaries(def->value.get());
		}
	}
	else if (const CiAggregateInitializer *init = dynamic_cast<const CiAggregateInitializer *>(expr))
		for (const std::shared_ptr<CiExpr> &item : init->items) {
			const CiBinaryExpr * assign = static_cast<const CiBinaryExpr *>(item.get());
			writeTemporaries(assign->right.get());
		}
	else if (dynamic_cast<const CiLiteral *>(expr) || dynamic_cast<const CiLambdaExpr *>(expr)) {
	}
	else if (const CiInterpolatedString *interp = dynamic_cast<const CiInterpolatedString *>(expr))
		for (const CiInterpolatedPart &part : interp->parts)
			writeTemporaries(part.argument.get());
	else if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr)) {
		if (symbol->left != nullptr)
			writeTemporaries(symbol->left.get());
	}
	else if (const CiUnaryExpr *unary = dynamic_cast<const CiUnaryExpr *>(expr)) {
		if (unary->inner != nullptr) {
			writeTemporaries(unary->inner.get());
			defineObjectLiteralTemporary(unary);
		}
	}
	else if (const CiBinaryExpr *binary = dynamic_cast<const CiBinaryExpr *>(expr)) {
		writeTemporaries(binary->left.get());
		if (binary->op == CiToken::is)
			defineIsVar(binary);
		else
			writeTemporaries(binary->right.get());
	}
	else if (const CiSelectExpr *select = dynamic_cast<const CiSelectExpr *>(expr)) {
		writeTemporaries(select->cond.get());
		writeTemporaries(select->onTrue.get());
		writeTemporaries(select->onFalse.get());
	}
	else if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr)) {
		writeTemporaries(call->method.get());
		for (const std::shared_ptr<CiExpr> &arg : call->arguments)
			writeTemporaries(arg.get());
	}
	else
		std::abort();
}

void GenBase::cleanupTemporary(int i, const CiExpr * temp)
{
}

void GenBase::cleanupTemporaries()
{
	for (int i = this->currentTemporaries.size(); --i >= 0;) {
		const CiExpr * temp = this->currentTemporaries[i];
		if (!dynamic_cast<const CiType *>(temp)) {
			cleanupTemporary(i, temp);
			this->currentTemporaries[i] = temp->type.get();
		}
	}
}

void GenBase::visitExpr(const CiExpr * statement)
{
	writeTemporaries(statement);
	statement->accept(this, CiPriority::statement);
	writeCharLine(';');
	if (const CiVar *def = dynamic_cast<const CiVar *>(statement))
		writeInitCode(def);
	cleanupTemporaries();
}

void GenBase::visitConst(const CiConst * statement)
{
}

void GenBase::visitAssert(const CiAssert * statement)
{
	const CiBinaryExpr * binary;
	if ((binary = dynamic_cast<const CiBinaryExpr *>(statement->cond.get())) && binary->op == CiToken::is && dynamic_cast<const CiVar *>(binary->right.get()))
		writeAssertCast(binary);
	else
		writeAssert(statement);
}

void GenBase::writeFirstStatements(const std::vector<std::shared_ptr<CiStatement>> * statements, int count)
{
	for (int i = 0; i < count; i++)
		(*statements)[i]->acceptStatement(this);
}

void GenBase::writeStatements(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	writeFirstStatements(statements, statements->size());
}

void GenBase::cleanupBlock(const CiBlock * statement)
{
}

void GenBase::visitBlock(const CiBlock * statement)
{
	if (this->atChildStart) {
		this->atLineStart = false;
		this->atChildStart = false;
		writeChar(' ');
	}
	openBlock();
	int temporariesCount = this->currentTemporaries.size();
	writeStatements(&statement->statements);
	cleanupBlock(statement);
	this->currentTemporaries.erase(this->currentTemporaries.begin() + temporariesCount, this->currentTemporaries.begin() + temporariesCount + (this->currentTemporaries.size() - temporariesCount));
	closeBlock();
}

void GenBase::writeChild(CiStatement * statement)
{
	bool wasInChildBlock = this->inChildBlock;
	this->atLineStart = true;
	this->atChildStart = true;
	this->inChildBlock = false;
	statement->acceptStatement(this);
	if (this->inChildBlock)
		closeBlock();
	else if (!dynamic_cast<const CiBlock *>(statement))
		this->indent--;
	this->inChildBlock = wasInChildBlock;
}

void GenBase::visitBreak(const CiBreak * statement)
{
	writeLine("break;");
}

void GenBase::visitContinue(const CiContinue * statement)
{
	writeLine("continue;");
}

void GenBase::visitDoWhile(const CiDoWhile * statement)
{
	write("do");
	writeChild(statement->body.get());
	write("while (");
	statement->cond->accept(this, CiPriority::argument);
	writeLine(");");
}

void GenBase::visitFor(const CiFor * statement)
{
	if (statement->cond != nullptr)
		writeTemporaries(statement->cond.get());
	write("for (");
	if (statement->init != nullptr)
		statement->init->accept(this, CiPriority::statement);
	writeChar(';');
	if (statement->cond != nullptr) {
		writeChar(' ');
		statement->cond->accept(this, CiPriority::argument);
	}
	writeChar(';');
	if (statement->advance != nullptr) {
		writeChar(' ');
		statement->advance->accept(this, CiPriority::statement);
	}
	writeChar(')');
	writeChild(statement->body.get());
}

bool GenBase::embedIfWhileIsVar(const CiExpr * expr, bool write)
{
	return false;
}

void GenBase::startIfWhile(const CiExpr * expr)
{
	embedIfWhileIsVar(expr, true);
	expr->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenBase::writeIf(const CiIf * statement)
{
	write("if (");
	startIfWhile(statement->cond.get());
	writeChild(statement->onTrue.get());
	if (statement->onFalse != nullptr) {
		write("else");
		if (CiIf *elseIf = dynamic_cast<CiIf *>(statement->onFalse.get())) {
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

void GenBase::visitIf(const CiIf * statement)
{
	if (!embedIfWhileIsVar(statement->cond.get(), false))
		writeTemporaries(statement->cond.get());
	writeIf(statement);
}

void GenBase::visitNative(const CiNative * statement)
{
	write(statement->content);
}

void GenBase::visitReturn(const CiReturn * statement)
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

void GenBase::defineVar(const CiExpr * value)
{
	const CiVar * def;
	if ((def = dynamic_cast<const CiVar *>(value)) && def->name != "_") {
		writeVar(def);
		endStatement();
	}
}

void GenBase::writeSwitchCaseTypeVar(const CiExpr * value)
{
}

void GenBase::writeSwitchValue(const CiExpr * expr)
{
	expr->accept(this, CiPriority::argument);
}

void GenBase::writeSwitchCaseBody(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	writeStatements(statements);
}

void GenBase::writeSwitchCase(const CiSwitch * statement, const CiCase * kase)
{
	for (const std::shared_ptr<CiExpr> &value : kase->values) {
		write("case ");
		writeCoercedLiteral(statement->value->type.get(), value.get());
		writeCharLine(':');
	}
	this->indent++;
	writeSwitchCaseBody(&kase->body);
	this->indent--;
}

void GenBase::startSwitch(const CiSwitch * statement)
{
	write("switch (");
	writeSwitchValue(statement->value.get());
	writeLine(") {");
	for (const CiCase &kase : statement->cases)
		writeSwitchCase(statement, &kase);
}

void GenBase::writeSwitchCaseCond(const CiSwitch * statement, const CiExpr * value, CiPriority parent)
{
	const CiBinaryExpr * when1;
	if ((when1 = dynamic_cast<const CiBinaryExpr *>(value)) && when1->op == CiToken::when) {
		if (parent > CiPriority::selectCond)
			writeChar('(');
		writeSwitchCaseCond(statement, when1->left.get(), CiPriority::condAnd);
		write(" && ");
		when1->right->accept(this, CiPriority::condAnd);
		if (parent > CiPriority::selectCond)
			writeChar(')');
	}
	else
		writeEqual(statement->value.get(), value, parent, false);
}

void GenBase::writeIfCaseBody(const std::vector<std::shared_ptr<CiStatement>> * body, bool doWhile, const CiSwitch * statement, const CiCase * kase)
{
	int length = CiSwitch::lengthWithoutTrailingBreak(body);
	if (doWhile && CiSwitch::hasEarlyBreak(body)) {
		this->indent++;
		writeNewLine();
		write("do ");
		openBlock();
		writeFirstStatements(body, length);
		closeBlock();
		writeLine("while (0);");
		this->indent--;
	}
	else if (length != 1 || dynamic_cast<const CiIf *>((*body)[0].get()) || dynamic_cast<const CiSwitch *>((*body)[0].get())) {
		writeChar(' ');
		openBlock();
		writeFirstStatements(body, length);
		closeBlock();
	}
	else
		writeChild((*body)[0].get());
}

void GenBase::writeSwitchAsIfs(const CiSwitch * statement, bool doWhile)
{
	for (const CiCase &kase : statement->cases) {
		for (const std::shared_ptr<CiExpr> &value : kase.values) {
			const CiBinaryExpr * when1;
			if ((when1 = dynamic_cast<const CiBinaryExpr *>(value.get())) && when1->op == CiToken::when) {
				defineVar(when1->left.get());
				writeTemporaries(when1);
			}
			else
				writeSwitchCaseTypeVar(value.get());
		}
	}
	std::string_view op = "if (";
	for (const CiCase &kase : statement->cases) {
		CiPriority parent = kase.values.size() == 1 ? CiPriority::argument : CiPriority::condOr;
		for (const std::shared_ptr<CiExpr> &value : kase.values) {
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

void GenBase::visitSwitch(const CiSwitch * statement)
{
	writeTemporaries(statement->value.get());
	startSwitch(statement);
	if (statement->defaultBody.size() > 0) {
		writeLine("default:");
		this->indent++;
		writeSwitchCaseBody(&statement->defaultBody);
		this->indent--;
	}
	writeCharLine('}');
}

void GenBase::visitWhile(const CiWhile * statement)
{
	if (!embedIfWhileIsVar(statement->cond.get(), false))
		writeTemporaries(statement->cond.get());
	write("while (");
	startIfWhile(statement->cond.get());
	writeChild(statement->body.get());
}

void GenBase::flattenBlock(CiStatement * statement)
{
	if (const CiBlock *block = dynamic_cast<const CiBlock *>(statement))
		writeStatements(&block->statements);
	else
		statement->acceptStatement(this);
}

bool GenBase::hasInitCode(const CiNamedValue * def) const
{
	return getAggregateInitializer(def) != nullptr;
}

bool GenBase::needsConstructor(const CiClass * klass) const
{
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const CiField * field;
		if ((field = dynamic_cast<const CiField *>(symbol)) && hasInitCode(field))
			return true;
	}
	return klass->constructor != nullptr;
}

void GenBase::writeInitField(const CiField * field)
{
	writeInitCode(field);
}

void GenBase::writeConstructorBody(const CiClass * klass)
{
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const CiField *field = dynamic_cast<const CiField *>(symbol))
			writeInitField(field);
	}
	if (klass->constructor != nullptr) {
		this->currentMethod = klass->constructor.get();
		const CiBlock * block = static_cast<const CiBlock *>(klass->constructor->body.get());
		writeStatements(&block->statements);
		this->currentMethod = nullptr;
	}
	this->currentTemporaries.clear();
}

void GenBase::writeParameter(const CiVar * param)
{
	writeTypeAndName(param);
}

void GenBase::writeRemainingParameters(const CiMethod * method, bool first, bool defaultArguments)
{
	for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
		if (!first)
			write(", ");
		first = false;
		writeParameter(param);
		if (defaultArguments)
			writeVarInit(param);
	}
	writeChar(')');
}

void GenBase::writeParameters(const CiMethod * method, bool defaultArguments)
{
	writeChar('(');
	writeRemainingParameters(method, true, defaultArguments);
}

void GenBase::writeBody(const CiMethod * method)
{
	if (method->callType == CiCallType::abstract)
		writeCharLine(';');
	else {
		writeNewLine();
		this->currentMethod = method;
		openBlock();
		flattenBlock(method->body.get());
		closeBlock();
		this->currentMethod = nullptr;
	}
}

void GenBase::writePublic(const CiContainerType * container)
{
	if (container->isPublic)
		write("public ");
}

void GenBase::writeEnumValue(const CiConst * konst)
{
	writeDoc(konst->documentation.get());
	writeName(konst);
	if (!dynamic_cast<const CiImplicitEnumValue *>(konst->value.get())) {
		write(" = ");
		konst->value->accept(this, CiPriority::argument);
	}
}

void GenBase::visitEnumValue(const CiConst * konst, const CiConst * previous)
{
	if (previous != nullptr)
		writeCharLine(',');
	writeEnumValue(konst);
}

void GenBase::writeRegexOptionsEnum(const CiProgram * program)
{
	if (program->regexOptionsEnum)
		writeEnum(program->system->regexOptionsEnum.get());
}

void GenBase::startClass(const CiClass * klass, std::string_view suffix, std::string_view extendsClause)
{
	write("class ");
	write(klass->name);
	write(suffix);
	if (klass->hasBaseClass()) {
		write(extendsClause);
		write(klass->baseClassName);
	}
}

void GenBase::openClass(const CiClass * klass, std::string_view suffix, std::string_view extendsClause)
{
	startClass(klass, suffix, extendsClause);
	writeNewLine();
	openBlock();
}

void GenBase::writeMembers(const CiClass * klass, bool constArrays)
{
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const CiConst *konst = dynamic_cast<const CiConst *>(symbol))
			writeConst(konst);
		else if (const CiField *field = dynamic_cast<const CiField *>(symbol))
			writeField(field);
		else if (const CiMethod *method = dynamic_cast<const CiMethod *>(symbol)) {
			writeMethod(method);
			this->currentTemporaries.clear();
		}
		else if (dynamic_cast<const CiVar *>(symbol)) {
		}
		else
			std::abort();
	}
	if (constArrays) {
		for (const CiConst * konst : klass->constArrays)
			writeConst(konst);
	}
}

bool GenBase::writeBaseClass(const CiClass * klass, const CiProgram * program)
{
	if (this->writtenClasses.contains(klass))
		return false;
	this->writtenClasses.insert(klass);
	if (const CiClass *baseClass = dynamic_cast<const CiClass *>(klass->parent))
		writeClass(baseClass, program);
	return true;
}

void GenBase::writeTypes(const CiProgram * program)
{
	writeRegexOptionsEnum(program);
	for (const CiSymbol * type = program->first; type != nullptr; type = type->next) {
		if (const CiClass *klass = dynamic_cast<const CiClass *>(type))
			writeClass(klass, program);
		else if (const CiEnum *enu = dynamic_cast<const CiEnum *>(type))
			writeEnum(enu);
		else
			std::abort();
	}
}

void GenTyped::writeTypeAndName(const CiNamedValue * value)
{
	writeType(value->type.get(), true);
	writeChar(' ');
	writeName(value);
}

void GenTyped::visitLiteralDouble(double value)
{
	GenBase::visitLiteralDouble(value);
	float f = static_cast<float>(value);
	if (f == value)
		writeChar('f');
}

void GenTyped::visitAggregateInitializer(const CiAggregateInitializer * expr)
{
	write("{ ");
	writeCoercedLiterals(expr->type->asClassType()->getElementType().get(), &expr->items);
	write(" }");
}

void GenTyped::writeArrayStorageLength(const CiExpr * expr)
{
	const CiArrayStorageType * array = static_cast<const CiArrayStorageType *>(expr->type.get());
	visitLiteralLong(array->length);
}

void GenTyped::writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent)
{
	write("new ");
	writeType(elementType->getBaseType(), false);
	writeChar('[');
	lengthExpr->accept(this, CiPriority::argument);
	writeChar(']');
	while (elementType->isArray()) {
		writeChar('[');
		if (const CiArrayStorageType *arrayStorage = dynamic_cast<const CiArrayStorageType *>(elementType))
			arrayStorage->lengthExpr->accept(this, CiPriority::argument);
		writeChar(']');
		elementType = elementType->asClassType()->getElementType().get();
	}
}

int GenTyped::getOneAscii(const CiExpr * expr) const
{
	const CiLiteralString * literal;
	return (literal = dynamic_cast<const CiLiteralString *>(expr)) ? literal->getOneAscii() : -1;
}

bool GenTyped::isNarrower(CiId left, CiId right)
{
	switch (left) {
	case CiId::sByteRange:
		switch (right) {
		case CiId::byteRange:
		case CiId::shortRange:
		case CiId::uShortRange:
		case CiId::intType:
		case CiId::longType:
			return true;
		default:
			return false;
		}
	case CiId::byteRange:
		switch (right) {
		case CiId::sByteRange:
		case CiId::shortRange:
		case CiId::uShortRange:
		case CiId::intType:
		case CiId::longType:
			return true;
		default:
			return false;
		}
	case CiId::shortRange:
		switch (right) {
		case CiId::uShortRange:
		case CiId::intType:
		case CiId::longType:
			return true;
		default:
			return false;
		}
	case CiId::uShortRange:
		switch (right) {
		case CiId::shortRange:
		case CiId::intType:
		case CiId::longType:
			return true;
		default:
			return false;
		}
	case CiId::intType:
		return right == CiId::longType;
	default:
		return false;
	}
}

const CiExpr * GenTyped::getStaticCastInner(const CiType * type, const CiExpr * expr) const
{
	const CiBinaryExpr * binary;
	const CiLiteralLong * rightMask;
	if ((binary = dynamic_cast<const CiBinaryExpr *>(expr)) && binary->op == CiToken::and_ && (rightMask = dynamic_cast<const CiLiteralLong *>(binary->right.get())) && dynamic_cast<const CiIntegerType *>(type)) {
		int64_t mask;
		switch (type->id) {
		case CiId::byteRange:
		case CiId::sByteRange:
			mask = 255;
			break;
		case CiId::shortRange:
		case CiId::uShortRange:
			mask = 65535;
			break;
		case CiId::intType:
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

void GenTyped::writeStaticCastType(const CiType * type)
{
	writeChar('(');
	writeType(type, false);
	write(") ");
}

void GenTyped::writeStaticCast(const CiType * type, const CiExpr * expr)
{
	writeStaticCastType(type);
	getStaticCastInner(type, expr)->accept(this, CiPriority::primary);
}

void GenTyped::writeNotPromoted(const CiType * type, const CiExpr * expr)
{
	if (dynamic_cast<const CiIntegerType *>(type) && isNarrower(type->id, getTypeId(expr->type.get(), true)))
		writeStaticCast(type, expr);
	else
		expr->accept(this, CiPriority::argument);
}

bool GenTyped::isPromoted(const CiExpr * expr) const
{
	const CiBinaryExpr * binary;
	return !((binary = dynamic_cast<const CiBinaryExpr *>(expr)) && (binary->op == CiToken::leftBracket || binary->isAssign()));
}

void GenTyped::writeAssignRight(const CiBinaryExpr * expr)
{
	if (expr->left->isIndexing()) {
		if (dynamic_cast<const CiLiteralLong *>(expr->right.get())) {
			writeCoercedLiteral(expr->left->type.get(), expr->right.get());
			return;
		}
		CiId leftTypeId = expr->left->type->id;
		CiId rightTypeId = getTypeId(expr->right->type.get(), isPromoted(expr->right.get()));
		if (leftTypeId == CiId::sByteRange && rightTypeId == CiId::sByteRange) {
			expr->right->accept(this, CiPriority::assign);
			return;
		}
		if (isNarrower(leftTypeId, rightTypeId)) {
			writeStaticCast(expr->left->type.get(), expr->right.get());
			return;
		}
	}
	GenBase::writeAssignRight(expr);
}

void GenTyped::writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent)
{
	if (dynamic_cast<const CiIntegerType *>(type) && type->id != CiId::longType && expr->type->id == CiId::longType)
		writeStaticCast(type, expr);
	else if (type->id == CiId::floatType && expr->type->id == CiId::doubleType) {
		if (const CiLiteralDouble *literal = dynamic_cast<const CiLiteralDouble *>(expr)) {
			GenBase::visitLiteralDouble(literal->value);
			writeChar('f');
		}
		else
			writeStaticCast(type, expr);
	}
	else if (dynamic_cast<const CiIntegerType *>(type) && expr->type->id == CiId::floatIntType) {
		const CiCallExpr * call;
		if ((call = dynamic_cast<const CiCallExpr *>(expr)) && call->method->symbol->id == CiId::mathTruncate) {
			expr = call->arguments[0].get();
			if (const CiLiteralDouble *literal = dynamic_cast<const CiLiteralDouble *>(expr)) {
				visitLiteralLong(static_cast<int64_t>(literal->value));
				return;
			}
		}
		writeStaticCast(type, expr);
	}
	else
		GenBase::writeCoercedInternal(type, expr, parent);
}

void GenTyped::writeCharAt(const CiBinaryExpr * expr)
{
	writeIndexing(expr->left.get(), expr->right.get());
}

void GenTyped::startTemporaryVar(const CiType * type)
{
	writeType(type, true);
	writeChar(' ');
}

void GenTyped::writeAssertCast(const CiBinaryExpr * expr)
{
	const CiVar * def = static_cast<const CiVar *>(expr->right.get());
	writeTypeAndName(def);
	write(" = ");
	writeStaticCast(def->type.get(), expr->left.get());
	writeCharLine(';');
}

void GenCCppD::visitLiteralLong(int64_t i)
{
	if (i == (-9223372036854775807 - 1))
		write("(-9223372036854775807 - 1)");
	else
		GenBase::visitLiteralLong(i);
}

bool GenCCppD::isPtrTo(const CiExpr * ptr, const CiExpr * other)
{
	const CiClassType * klass;
	return (klass = dynamic_cast<const CiClassType *>(ptr->type.get())) && klass->class_->id != CiId::stringClass && klass->isAssignableFrom(other->type.get());
}

void GenCCppD::writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	const CiType * coercedType;
	if (isPtrTo(left, right))
		coercedType = left->type.get();
	else if (isPtrTo(right, left))
		coercedType = right->type.get();
	else {
		GenBase::writeEqual(left, right, parent, not_);
		return;
	}
	if (parent > CiPriority::equality)
		writeChar('(');
	writeCoerced(coercedType, left, CiPriority::equality);
	write(getEqOp(not_));
	writeCoerced(coercedType, right, CiPriority::equality);
	if (parent > CiPriority::equality)
		writeChar(')');
}

void GenCCppD::visitConst(const CiConst * statement)
{
	if (dynamic_cast<const CiArrayStorageType *>(statement->type.get()))
		writeConst(statement);
}

void GenCCppD::visitBreak(const CiBreak * statement)
{
	if (const CiSwitch *switchStatement = dynamic_cast<const CiSwitch *>(statement->loopOrSwitch)) {
		int gotoId = [](const std::vector<const CiSwitch *> &v, const CiSwitch * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->switchesWithGoto, switchStatement);
		if (gotoId >= 0) {
			write("goto ciafterswitch");
			visitLiteralLong(gotoId);
			writeCharLine(';');
			return;
		}
	}
	GenBase::visitBreak(statement);
}

void GenCCppD::writeSwitchAsIfsWithGoto(const CiSwitch * statement)
{
	if (std::any_of(statement->cases.begin(), statement->cases.end(), [](const CiCase &kase) { return CiSwitch::hasEarlyBreakAndContinue(&kase.body); }) || CiSwitch::hasEarlyBreakAndContinue(&statement->defaultBody)) {
		int gotoId = this->switchesWithGoto.size();
		this->switchesWithGoto.push_back(statement);
		writeSwitchAsIfs(statement, false);
		write("ciafterswitch");
		visitLiteralLong(gotoId);
		writeLine(": ;");
	}
	else
		writeSwitchAsIfs(statement, true);
}

void GenCCpp::writeCIncludes()
{
	writeIncludes("#include <", ">");
}

int GenCCpp::getLiteralChars() const
{
	return 127;
}

void GenCCpp::writeNumericType(CiId id)
{
	switch (id) {
	case CiId::sByteRange:
		includeStdInt();
		write("int8_t");
		break;
	case CiId::byteRange:
		includeStdInt();
		write("uint8_t");
		break;
	case CiId::shortRange:
		includeStdInt();
		write("int16_t");
		break;
	case CiId::uShortRange:
		includeStdInt();
		write("uint16_t");
		break;
	case CiId::intType:
		write("int");
		break;
	case CiId::longType:
		includeStdInt();
		write("int64_t");
		break;
	case CiId::floatType:
		write("float");
		break;
	case CiId::doubleType:
		write("double");
		break;
	default:
		std::abort();
	}
}

void GenCCpp::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::mathNaN:
		includeMath();
		write("NAN");
		break;
	case CiId::mathNegativeInfinity:
		includeMath();
		write("-INFINITY");
		break;
	case CiId::mathPositiveInfinity:
		includeMath();
		write("INFINITY");
		break;
	default:
		GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

const CiExpr * GenCCpp::isStringEmpty(const CiBinaryExpr * expr)
{
	const CiSymbolReference * symbol;
	if ((symbol = dynamic_cast<const CiSymbolReference *>(expr->left.get())) && symbol->symbol->id == CiId::stringLength && expr->right->isLiteralZero())
		return symbol->left.get();
	return nullptr;
}

void GenCCpp::writeArrayPtrAdd(const CiExpr * array, const CiExpr * index)
{
	if (index->isLiteralZero())
		writeArrayPtr(array, CiPriority::argument);
	else {
		writeArrayPtr(array, CiPriority::add);
		write(" + ");
		index->accept(this, CiPriority::mul);
	}
}

const CiCallExpr * GenCCpp::isStringSubstring(const CiExpr * expr)
{
	if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr)) {
		CiId id = call->method->symbol->id;
		if ((id == CiId::stringSubstring && call->arguments.size() == 2) || id == CiId::uTF8GetString)
			return call;
	}
	return nullptr;
}

bool GenCCpp::isUTF8GetString(const CiCallExpr * call)
{
	return call->method->symbol->id == CiId::uTF8GetString;
}

const CiExpr * GenCCpp::getStringSubstringPtr(const CiCallExpr * call)
{
	return isUTF8GetString(call) ? call->arguments[0].get() : call->method->left.get();
}

const CiExpr * GenCCpp::getStringSubstringOffset(const CiCallExpr * call)
{
	return call->arguments[isUTF8GetString(call) ? 1 : 0].get();
}

const CiExpr * GenCCpp::getStringSubstringLength(const CiCallExpr * call)
{
	return call->arguments[isUTF8GetString(call) ? 2 : 1].get();
}

void GenCCpp::writeStringPtrAdd(const CiCallExpr * call)
{
	writeArrayPtrAdd(getStringSubstringPtr(call), getStringSubstringOffset(call));
}

const CiExpr * GenCCpp::isTrimSubstring(const CiBinaryExpr * expr)
{
	const CiCallExpr * call = isStringSubstring(expr->right.get());
	const CiSymbolReference * leftSymbol;
	if (call != nullptr && !isUTF8GetString(call) && (leftSymbol = dynamic_cast<const CiSymbolReference *>(expr->left.get())) && getStringSubstringPtr(call)->isReferenceTo(leftSymbol->symbol) && getStringSubstringOffset(call)->isLiteralZero())
		return getStringSubstringLength(call);
	return nullptr;
}

void GenCCpp::writeStringLiteralWithNewLine(std::string_view s)
{
	writeChar('"');
	write(s);
	write("\\n\"");
}

void GenCCpp::writeUnreachable(const CiAssert * statement)
{
	write("abort();");
	if (statement->message != nullptr) {
		write(" // ");
		statement->message->accept(this, CiPriority::argument);
	}
	writeNewLine();
}

void GenCCpp::writeAssert(const CiAssert * statement)
{
	if (statement->completesNormally()) {
		writeTemporaries(statement->cond.get());
		includeAssert();
		write("assert(");
		if (statement->message == nullptr)
			statement->cond->accept(this, CiPriority::argument);
		else {
			statement->cond->accept(this, CiPriority::condAnd);
			write(" && ");
			statement->message->accept(this, CiPriority::argument);
		}
		writeLine(");");
	}
	else
		writeUnreachable(statement);
}

void GenCCpp::visitSwitch(const CiSwitch * statement)
{
	if (dynamic_cast<const CiStringType *>(statement->value->type.get()) || statement->hasWhen())
		writeSwitchAsIfsWithGoto(statement);
	else
		GenBase::visitSwitch(statement);
}

void GenCCpp::writeMethods(const CiClass * klass)
{
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const CiMethod *method = dynamic_cast<const CiMethod *>(symbol)) {
			writeMethod(method);
			this->currentTemporaries.clear();
		}
	}
}

void GenCCpp::writeClass(const CiClass * klass, const CiProgram * program)
{
	if (!writeBaseClass(klass, program))
		return;
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const CiField * field;
		const CiStorageType * storage;
		if ((field = dynamic_cast<const CiField *>(symbol)) && (storage = dynamic_cast<const CiStorageType *>(field->type->getBaseType())) && storage->class_->id == CiId::none)
			writeClass(storage->class_, program);
	}
	writeClassInternal(klass);
}

std::string GenCCpp::changeExtension(std::string_view path, std::string_view ext)
{
	int extIndex = path.length();
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
	int pathLength = path.length();
	int extIndex = pathLength;
	int i = pathLength;
	while (--i >= 0 && path[i] != '/' && path[i] != '\\') {
		if (path[i] == '.' && extIndex == pathLength)
			extIndex = i;
	}
	i++;
	return std::string(path.data() + i, extIndex - i);
}

void GenCCpp::createImplementationFile(const CiProgram * program, std::string_view headerExt)
{
	createOutputFile();
	writeTopLevelNatives(program);
	writeCIncludes();
	write("#include \"");
	write(getFilenameWithoutExtension(this->outputFile));
	write(headerExt);
	writeCharLine('"');
}

const CiContainerType * GenC::getCurrentContainer() const
{
	return this->currentClass;
}

std::string_view GenC::getTargetName() const
{
	return "C";
}

void GenC::writeSelfDoc(const CiMethod * method)
{
	if (method->callType == CiCallType::static_)
		return;
	write(" * @param self This <code>");
	writeName(method->parent);
	writeLine("</code>.");
}

void GenC::includeStdInt()
{
	include("stdint.h");
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

void GenC::writePrintfWidth(const CiInterpolatedPart * part)
{
	GenBase::writePrintfWidth(part);
	if (isStringSubstring(part->argument.get()) != nullptr) {
		assert(part->precision < 0);
		write(".*");
	}
	if (part->argument->type->id == CiId::longType)
		writePrintfLongPrefix();
}

void GenC::writeInterpolatedStringArgBase(const CiExpr * expr)
{
	if (expr->type->id == CiId::longType) {
		write("(long long) ");
		expr->accept(this, CiPriority::primary);
	}
	else
		expr->accept(this, CiPriority::argument);
}

void GenC::writeStringPtrAddCast(const CiCallExpr * call)
{
	if (isUTF8GetString(call))
		write("(const char *) ");
	writeStringPtrAdd(call);
}

bool GenC::isDictionaryClassStgIndexing(const CiExpr * expr)
{
	const CiBinaryExpr * indexing;
	const CiClassType * dict;
	return (indexing = dynamic_cast<const CiBinaryExpr *>(expr)) && indexing->op == CiToken::leftBracket && (dict = dynamic_cast<const CiClassType *>(indexing->left->type.get())) && dict->class_->typeParameterCount == 2 && dynamic_cast<const CiStorageType *>(dict->getValueType().get());
}

void GenC::writeTemporaryOrExpr(const CiExpr * expr, CiPriority parent)
{
	int tempId = [](const std::vector<const CiExpr *> &v, const CiExpr * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->currentTemporaries, expr);
	if (tempId >= 0) {
		write("citemp");
		visitLiteralLong(tempId);
	}
	else
		expr->accept(this, parent);
}

void GenC::writeUpcast(const CiClass * resultClass, const CiSymbol * klass)
{
	for (; klass != resultClass; klass = klass->parent)
		write(".base");
}

void GenC::writeClassPtr(const CiClass * resultClass, const CiExpr * expr, CiPriority parent)
{
	const CiStorageType * storage;
	const CiClassType * ptr;
	if ((storage = dynamic_cast<const CiStorageType *>(expr->type.get())) && storage->class_->id == CiId::none && !isDictionaryClassStgIndexing(expr)) {
		writeChar('&');
		writeTemporaryOrExpr(expr, CiPriority::primary);
		writeUpcast(resultClass, storage->class_);
	}
	else if ((ptr = dynamic_cast<const CiClassType *>(expr->type.get())) && ptr->class_ != resultClass) {
		writeChar('&');
		writePostfix(expr, "->base");
		writeUpcast(resultClass, ptr->class_->parent);
	}
	else
		expr->accept(this, parent);
}

void GenC::writeInterpolatedStringArg(const CiExpr * expr)
{
	const CiCallExpr * call = isStringSubstring(expr);
	if (call != nullptr) {
		getStringSubstringLength(call)->accept(this, CiPriority::argument);
		write(", ");
		writeStringPtrAddCast(call);
	}
	else {
		const CiClassType * klass;
		if ((klass = dynamic_cast<const CiClassType *>(expr->type.get())) && klass->class_->id != CiId::stringClass) {
			write(this->namespace_);
			write(klass->class_->name);
			write("_ToString(");
			writeClassPtr(klass->class_, expr, CiPriority::argument);
			writeChar(')');
		}
		else
			writeInterpolatedStringArgBase(expr);
	}
}

void GenC::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
{
	include("stdarg.h");
	include("stdio.h");
	this->stringFormat = true;
	write("CiString_Format(");
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

void GenC::writeName(const CiSymbol * symbol)
{
	if (dynamic_cast<const CiContainerType *>(symbol)) {
		write(this->namespace_);
		write(symbol->name);
	}
	else if (dynamic_cast<const CiMethod *>(symbol)) {
		write(this->namespace_);
		write(symbol->parent->name);
		writeChar('_');
		write(symbol->name);
	}
	else if (dynamic_cast<const CiConst *>(symbol)) {
		if (dynamic_cast<const CiContainerType *>(symbol->parent)) {
			write(this->namespace_);
			write(symbol->parent->name);
			writeChar('_');
		}
		writeUppercaseWithUnderscores(symbol->name);
	}
	else
		writeCamelCaseNotKeyword(symbol->name);
}

void GenC::writeSelfForField(const CiSymbol * fieldClass)
{
	write("self->");
	for (const CiSymbol * klass = this->currentClass; klass != fieldClass; klass = klass->parent)
		write("base.");
}

void GenC::writeLocalName(const CiSymbol * symbol, CiPriority parent)
{
	if (const CiForeach *forEach = dynamic_cast<const CiForeach *>(symbol->parent)) {
		const CiClassType * klass = static_cast<const CiClassType *>(forEach->collection->type.get());
		switch (klass->class_->id) {
		case CiId::stringClass:
		case CiId::listClass:
			if (parent == CiPriority::primary)
				writeChar('(');
			writeChar('*');
			writeCamelCaseNotKeyword(symbol->name);
			if (parent == CiPriority::primary)
				writeChar(')');
			return;
		case CiId::arrayStorageClass:
			if (dynamic_cast<const CiStorageType *>(klass->getElementType().get())) {
				if (parent > CiPriority::add)
					writeChar('(');
				forEach->collection->accept(this, CiPriority::add);
				write(" + ");
				writeCamelCaseNotKeyword(symbol->name);
				if (parent > CiPriority::add)
					writeChar(')');
			}
			else {
				forEach->collection->accept(this, CiPriority::primary);
				writeChar('[');
				writeCamelCaseNotKeyword(symbol->name);
				writeChar(']');
			}
			return;
		default:
			break;
		}
	}
	if (dynamic_cast<const CiField *>(symbol))
		writeSelfForField(symbol->parent);
	writeName(symbol);
}

void GenC::writeMatchProperty(const CiSymbolReference * expr, int which)
{
	this->matchPos = true;
	write("CiMatch_GetPos(");
	expr->left->accept(this, CiPriority::argument);
	write(", ");
	visitLiteralLong(which);
	writeChar(')');
}

void GenC::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::consoleError:
		include("stdio.h");
		write("stderr");
		break;
	case CiId::listCount:
	case CiId::stackCount:
		writePostfix(expr->left.get(), "->len");
		break;
	case CiId::queueCount:
		expr->left->accept(this, CiPriority::primary);
		if (dynamic_cast<const CiStorageType *>(expr->left->type.get()))
			writeChar('.');
		else
			write("->");
		write("length");
		break;
	case CiId::hashSetCount:
	case CiId::dictionaryCount:
		writeCall("g_hash_table_size", expr->left.get());
		break;
	case CiId::sortedSetCount:
	case CiId::sortedDictionaryCount:
		writeCall("g_tree_nnodes", expr->left.get());
		break;
	case CiId::matchStart:
		writeMatchProperty(expr, 0);
		break;
	case CiId::matchEnd:
		writeMatchProperty(expr, 1);
		break;
	case CiId::matchLength:
		writeMatchProperty(expr, 2);
		break;
	case CiId::matchValue:
		write("g_match_info_fetch(");
		expr->left->accept(this, CiPriority::argument);
		write(", 0)");
		break;
	default:
		if (expr->left == nullptr || dynamic_cast<const CiConst *>(expr->symbol))
			writeLocalName(expr->symbol, parent);
		else if (isDictionaryClassStgIndexing(expr->left.get())) {
			writePostfix(expr->left.get(), "->");
			writeName(expr->symbol);
		}
		else
			GenCCpp::visitSymbolReference(expr, parent);
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

void GenC::writeClassType(const CiClassType * klass, bool space)
{
	switch (klass->class_->id) {
	case CiId::none:
		if (!dynamic_cast<const CiReadWriteClassType *>(klass))
			write("const ");
		writeName(klass->class_);
		if (!dynamic_cast<const CiStorageType *>(klass))
			write(" *");
		else if (space)
			writeChar(' ');
		break;
	case CiId::stringClass:
		if (klass->id == CiId::stringStorageType)
			write("char *");
		else
			writeStringPtrType();
		break;
	case CiId::listClass:
	case CiId::stackClass:
		writeGlib("GArray *");
		break;
	case CiId::queueClass:
		writeGlib("GQueue ");
		if (!dynamic_cast<const CiStorageType *>(klass))
			writeChar('*');
		break;
	case CiId::hashSetClass:
	case CiId::dictionaryClass:
		writeGlib("GHashTable *");
		break;
	case CiId::sortedSetClass:
	case CiId::sortedDictionaryClass:
		writeGlib("GTree *");
		break;
	case CiId::textWriterClass:
		include("stdio.h");
		write("FILE *");
		break;
	case CiId::regexClass:
		if (!dynamic_cast<const CiReadWriteClassType *>(klass))
			write("const ");
		writeGlib("GRegex *");
		break;
	case CiId::matchClass:
		if (!dynamic_cast<const CiReadWriteClassType *>(klass))
			write("const ");
		writeGlib("GMatchInfo *");
		break;
	case CiId::lockClass:
		notYet(klass, "Lock");
		include("threads.h");
		write("mtx_t ");
		break;
	default:
		notSupported(klass, klass->class_->name);
		break;
	}
}

void GenC::writeArrayPrefix(const CiType * type)
{
	const CiClassType * array;
	if ((array = dynamic_cast<const CiClassType *>(type)) && array->isArray()) {
		writeArrayPrefix(array->getElementType().get());
		if (!dynamic_cast<const CiArrayStorageType *>(type)) {
			if (dynamic_cast<const CiArrayStorageType *>(array->getElementType().get()))
				writeChar('(');
			if (!dynamic_cast<const CiReadWriteClassType *>(type))
				write("const ");
			writeChar('*');
		}
	}
}

void GenC::startDefinition(const CiType * type, bool promote, bool space)
{
	const CiType * baseType = type->getBaseType();
	if (dynamic_cast<const CiIntegerType *>(baseType)) {
		writeNumericType(getTypeId(baseType, promote && type == baseType));
		if (space)
			writeChar(' ');
	}
	else if (dynamic_cast<const CiEnum *>(baseType)) {
		if (baseType->id == CiId::boolType) {
			includeStdBool();
			write("bool");
		}
		else
			writeName(baseType);
		if (space)
			writeChar(' ');
	}
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(baseType))
		writeClassType(klass, space);
	else {
		write(baseType->name);
		if (space)
			writeChar(' ');
	}
	writeArrayPrefix(type);
}

void GenC::endDefinition(const CiType * type)
{
	while (type->isArray()) {
		const CiType * elementType = type->asClassType()->getElementType().get();
		if (const CiArrayStorageType *arrayStorage = dynamic_cast<const CiArrayStorageType *>(type)) {
			writeChar('[');
			visitLiteralLong(arrayStorage->length);
			writeChar(']');
		}
		else if (dynamic_cast<const CiArrayStorageType *>(elementType))
			writeChar(')');
		type = elementType;
	}
}

void GenC::writeReturnType(const CiMethod * method)
{
	if (method->type->id == CiId::voidType && method->throws) {
		includeStdBool();
		write("bool ");
	}
	else
		startDefinition(method->type.get(), true, true);
}

void GenC::writeType(const CiType * type, bool promote)
{
	const CiClassType * arrayPtr;
	startDefinition(type, promote, (arrayPtr = dynamic_cast<const CiClassType *>(type)) && arrayPtr->class_->id == CiId::arrayPtrClass);
	endDefinition(type);
}

void GenC::writeTypeAndName(const CiNamedValue * value)
{
	startDefinition(value->type.get(), true, true);
	writeName(value);
	endDefinition(value->type.get());
}

void GenC::writeDynamicArrayCast(const CiType * elementType)
{
	writeChar('(');
	startDefinition(elementType, false, true);
	write(elementType->isArray() ? "(*)" : "*");
	endDefinition(elementType);
	write(") ");
}

void GenC::writeXstructorPtr(bool need, const CiClass * klass, std::string_view name)
{
	if (need) {
		write("(CiMethodPtr) ");
		writeName(klass);
		writeChar('_');
		write(name);
	}
	else
		write("NULL");
}

bool GenC::isHeapAllocated(const CiType * type)
{
	return type->id == CiId::stringStorageType || dynamic_cast<const CiDynamicPtrType *>(type);
}

bool GenC::needToDestructType(const CiType * type)
{
	if (isHeapAllocated(type))
		return true;
	if (const CiStorageType *storage = dynamic_cast<const CiStorageType *>(type)) {
		switch (storage->class_->id) {
		case CiId::listClass:
		case CiId::stackClass:
		case CiId::hashSetClass:
		case CiId::sortedSetClass:
		case CiId::dictionaryClass:
		case CiId::sortedDictionaryClass:
		case CiId::matchClass:
		case CiId::lockClass:
			return true;
		default:
			return needsDestructor(storage->class_);
		}
	}
	return false;
}

bool GenC::needToDestruct(const CiSymbol * symbol)
{
	const CiType * type = symbol->type.get();
	while (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(type))
		type = array->getElementType().get();
	return needToDestructType(type);
}

bool GenC::needsDestructor(const CiClass * klass)
{
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const CiField * field;
		if ((field = dynamic_cast<const CiField *>(symbol)) && needToDestruct(field))
			return true;
	}
	const CiClass * baseClass;
	return (baseClass = dynamic_cast<const CiClass *>(klass->parent)) && needsDestructor(baseClass);
}

void GenC::writeXstructorPtrs(const CiClass * klass)
{
	writeXstructorPtr(needsConstructor(klass), klass, "Construct");
	write(", ");
	writeXstructorPtr(needsDestructor(klass), klass, "Destruct");
}

void GenC::writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent)
{
	this->sharedMake = true;
	if (parent > CiPriority::mul)
		writeChar('(');
	writeDynamicArrayCast(elementType);
	write("CiShared_Make(");
	lengthExpr->accept(this, CiPriority::argument);
	write(", sizeof(");
	writeType(elementType, false);
	write("), ");
	if (dynamic_cast<const CiStringStorageType *>(elementType)) {
		this->ptrConstruct = true;
		this->listFrees["String"] = "free(*(void **) ptr)";
		write("(CiMethodPtr) CiPtr_Construct, CiList_FreeString");
	}
	else if (const CiStorageType *storage = dynamic_cast<const CiStorageType *>(elementType))
		writeXstructorPtrs(storage->class_);
	else if (dynamic_cast<const CiDynamicPtrType *>(elementType)) {
		this->ptrConstruct = true;
		this->sharedRelease = true;
		this->listFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
		write("(CiMethodPtr) CiPtr_Construct, CiList_FreeShared");
	}
	else
		write("NULL, NULL");
	writeChar(')');
	if (parent > CiPriority::mul)
		writeChar(')');
}

bool GenC::isNewString(const CiExpr * expr)
{
	const CiCallExpr * call;
	return dynamic_cast<const CiInterpolatedString *>(expr) || ((call = dynamic_cast<const CiCallExpr *>(expr)) && expr->type->id == CiId::stringStorageType && (call->method->symbol->id != CiId::stringSubstring || call->arguments.size() == 2));
}

void GenC::writeStringStorageValue(const CiExpr * expr)
{
	const CiCallExpr * call = isStringSubstring(expr);
	if (call != nullptr) {
		include("string.h");
		this->stringSubstring = true;
		write("CiString_Substring(");
		writeStringPtrAddCast(call);
		write(", ");
		getStringSubstringLength(call)->accept(this, CiPriority::argument);
		writeChar(')');
	}
	else if (isNewString(expr))
		expr->accept(this, CiPriority::argument);
	else {
		include("string.h");
		writeCall("strdup", expr);
	}
}

void GenC::writeArrayStorageInit(const CiArrayStorageType * array, const CiExpr * value)
{
	const CiLiteral * literal;
	if (value == nullptr) {
		if (isHeapAllocated(array->getStorageType()))
			write(" = { NULL }");
	}
	else if ((literal = dynamic_cast<const CiLiteral *>(value)) && literal->isDefaultValue()) {
		write(" = { ");
		literal->accept(this, CiPriority::argument);
		write(" }");
	}
	else
		std::abort();
}

std::string GenC::getDictionaryDestroy(const CiType * type)
{
	if (dynamic_cast<const CiStringStorageType *>(type) || dynamic_cast<const CiArrayStorageType *>(type))
		return "free";
	else if (const CiStorageType *storage = dynamic_cast<const CiStorageType *>(type)) {
		switch (storage->class_->id) {
		case CiId::listClass:
		case CiId::stackClass:
			return "(GDestroyNotify) g_array_unref";
		case CiId::hashSetClass:
		case CiId::dictionaryClass:
			return "(GDestroyNotify) g_hash_table_unref";
		case CiId::sortedSetClass:
		case CiId::sortedDictionaryClass:
			return "(GDestroyNotify) g_tree_unref";
		default:
			return std::string(needsDestructor(storage->class_) ? std::format("(GDestroyNotify) {}_Delete", storage->class_->name) : "free");
		}
	}
	else if (dynamic_cast<const CiDynamicPtrType *>(type)) {
		this->sharedRelease = true;
		return "CiShared_Release";
	}
	else
		return "NULL";
}

void GenC::writeHashEqual(const CiType * keyType)
{
	write(dynamic_cast<const CiStringType *>(keyType) ? "g_str_hash, g_str_equal" : "NULL, NULL");
}

void GenC::writeNewHashTable(const CiType * keyType, std::string_view valueDestroy)
{
	write("g_hash_table_new");
	std::string keyDestroy{getDictionaryDestroy(keyType)};
	if (keyDestroy == "NULL" && valueDestroy == "NULL") {
		writeChar('(');
		writeHashEqual(keyType);
	}
	else {
		write("_full(");
		writeHashEqual(keyType);
		write(", ");
		write(keyDestroy);
		write(", ");
		write(valueDestroy);
	}
	writeChar(')');
}

void GenC::writeNewTree(const CiType * keyType, std::string_view valueDestroy)
{
	if (keyType->id == CiId::stringPtrType && valueDestroy == "NULL")
		write("g_tree_new((GCompareFunc) strcmp");
	else {
		write("g_tree_new_full(CiTree_Compare");
		if (dynamic_cast<const CiIntegerType *>(keyType)) {
			this->treeCompareInteger = true;
			write("Integer");
		}
		else if (dynamic_cast<const CiStringType *>(keyType)) {
			this->treeCompareString = true;
			write("String");
		}
		else
			notSupported(keyType, keyType->toString());
		write(", NULL, ");
		write(getDictionaryDestroy(keyType));
		write(", ");
		write(valueDestroy);
	}
	writeChar(')');
}

void GenC::writeNew(const CiReadWriteClassType * klass, CiPriority parent)
{
	switch (klass->class_->id) {
	case CiId::listClass:
	case CiId::stackClass:
		write("g_array_new(FALSE, FALSE, sizeof(");
		writeType(klass->getElementType().get(), false);
		write("))");
		break;
	case CiId::queueClass:
		write("G_QUEUE_INIT");
		break;
	case CiId::hashSetClass:
		writeNewHashTable(klass->getElementType().get(), "NULL");
		break;
	case CiId::sortedSetClass:
		writeNewTree(klass->getElementType().get(), "NULL");
		break;
	case CiId::dictionaryClass:
		writeNewHashTable(klass->getKeyType(), getDictionaryDestroy(klass->getValueType().get()));
		break;
	case CiId::sortedDictionaryClass:
		writeNewTree(klass->getKeyType(), getDictionaryDestroy(klass->getValueType().get()));
		break;
	default:
		this->sharedMake = true;
		if (parent > CiPriority::mul)
			writeChar('(');
		writeStaticCastType(klass);
		write("CiShared_Make(1, sizeof(");
		writeName(klass->class_);
		write("), ");
		writeXstructorPtrs(klass->class_);
		writeChar(')');
		if (parent > CiPriority::mul)
			writeChar(')');
		break;
	}
}

void GenC::writeStorageInit(const CiNamedValue * def)
{
	if (def->type->asClassType()->class_->typeParameterCount > 0)
		GenBase::writeStorageInit(def);
}

void GenC::writeVarInit(const CiNamedValue * def)
{
	if (def->value == nullptr && isHeapAllocated(def->type.get()))
		write(" = NULL");
	else
		GenBase::writeVarInit(def);
}

void GenC::writeAssignTemporary(const CiType * type, const CiExpr * expr)
{
	write(" = ");
	if (expr != nullptr)
		writeCoerced(type, expr, CiPriority::argument);
	else
		writeNewStorage(type);
}

int GenC::writeCTemporary(const CiType * type, const CiExpr * expr)
{
	ensureChildBlock();
	const CiClassType * klass;
	bool assign = expr != nullptr || ((klass = dynamic_cast<const CiClassType *>(type)) && (klass->class_->id == CiId::listClass || klass->class_->id == CiId::dictionaryClass || klass->class_->id == CiId::sortedDictionaryClass));
	int id = [](const std::vector<const CiExpr *> &v, const CiExpr * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->currentTemporaries, type);
	if (id < 0) {
		id = this->currentTemporaries.size();
		startDefinition(type, false, true);
		write("citemp");
		visitLiteralLong(id);
		endDefinition(type);
		if (assign)
			writeAssignTemporary(type, expr);
		writeCharLine(';');
		this->currentTemporaries.push_back(expr);
	}
	else if (assign) {
		write("citemp");
		visitLiteralLong(id);
		writeAssignTemporary(type, expr);
		writeCharLine(';');
		this->currentTemporaries[id] = expr;
	}
	return id;
}

void GenC::writeStorageTemporary(const CiExpr * expr)
{
	if (isNewString(expr) || (dynamic_cast<const CiCallExpr *>(expr) && dynamic_cast<const CiStorageType *>(expr->type.get())))
		writeCTemporary(expr->type.get(), expr);
}

void GenC::writeCTemporaries(const CiExpr * expr)
{
	if (const CiVar *def = dynamic_cast<const CiVar *>(expr)) {
		if (def->value != nullptr)
			writeCTemporaries(def->value.get());
	}
	else if (const CiAggregateInitializer *init = dynamic_cast<const CiAggregateInitializer *>(expr))
		for (const std::shared_ptr<CiExpr> &item : init->items) {
			const CiBinaryExpr * assign = static_cast<const CiBinaryExpr *>(item.get());
			writeCTemporaries(assign->right.get());
		}
	else if (dynamic_cast<const CiLiteral *>(expr) || dynamic_cast<const CiLambdaExpr *>(expr)) {
	}
	else if (const CiInterpolatedString *interp = dynamic_cast<const CiInterpolatedString *>(expr))
		for (const CiInterpolatedPart &part : interp->parts)
			writeCTemporaries(part.argument.get());
	else if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr)) {
		if (symbol->left != nullptr)
			writeCTemporaries(symbol->left.get());
	}
	else if (const CiUnaryExpr *unary = dynamic_cast<const CiUnaryExpr *>(expr)) {
		if (unary->inner != nullptr)
			writeCTemporaries(unary->inner.get());
	}
	else if (const CiBinaryExpr *binary = dynamic_cast<const CiBinaryExpr *>(expr)) {
		writeCTemporaries(binary->left.get());
		if (isStringSubstring(binary->left.get()) == nullptr)
			writeStorageTemporary(binary->left.get());
		writeCTemporaries(binary->right.get());
		if (binary->op != CiToken::assign)
			writeStorageTemporary(binary->right.get());
	}
	else if (const CiSelectExpr *select = dynamic_cast<const CiSelectExpr *>(expr))
		writeCTemporaries(select->cond.get());
	else if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr)) {
		if (call->method->left != nullptr) {
			writeCTemporaries(call->method->left.get());
			writeStorageTemporary(call->method->left.get());
		}
		const CiMethod * method = static_cast<const CiMethod *>(call->method->symbol);
		const CiVar * param = method->parameters.firstParameter();
		for (const std::shared_ptr<CiExpr> &arg : call->arguments) {
			writeCTemporaries(arg.get());
			if (call->method->symbol->id != CiId::consoleWrite && call->method->symbol->id != CiId::consoleWriteLine && !dynamic_cast<const CiStorageType *>(param->type.get()))
				writeStorageTemporary(arg.get());
			param = param->nextParameter();
		}
	}
	else
		std::abort();
}

bool GenC::hasTemporariesToDestruct(const CiExpr * expr)
{
	if (const CiAggregateInitializer *init = dynamic_cast<const CiAggregateInitializer *>(expr))
		return std::any_of(init->items.begin(), init->items.end(), [](const std::shared_ptr<CiExpr> &field) { return hasTemporariesToDestruct(field.get()); });
	else if (dynamic_cast<const CiLiteral *>(expr) || dynamic_cast<const CiLambdaExpr *>(expr))
		return false;
	else if (const CiInterpolatedString *interp = dynamic_cast<const CiInterpolatedString *>(expr))
		return std::any_of(interp->parts.begin(), interp->parts.end(), [](const CiInterpolatedPart &part) { return hasTemporariesToDestruct(part.argument.get()); });
	else if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr))
		return symbol->left != nullptr && hasTemporariesToDestruct(symbol->left.get());
	else if (const CiUnaryExpr *unary = dynamic_cast<const CiUnaryExpr *>(expr))
		return unary->inner != nullptr && hasTemporariesToDestruct(unary->inner.get());
	else if (const CiBinaryExpr *binary = dynamic_cast<const CiBinaryExpr *>(expr))
		return hasTemporariesToDestruct(binary->left.get()) || (binary->op != CiToken::is && hasTemporariesToDestruct(binary->right.get()));
	else if (const CiSelectExpr *select = dynamic_cast<const CiSelectExpr *>(expr))
		return hasTemporariesToDestruct(select->cond.get());
	else if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr))
		return (call->method->left != nullptr && (hasTemporariesToDestruct(call->method->left.get()) || isNewString(call->method->left.get()))) || std::any_of(call->arguments.begin(), call->arguments.end(), [](const std::shared_ptr<CiExpr> &arg) { return hasTemporariesToDestruct(arg.get()) || isNewString(arg.get()); });
	else
		std::abort();
}

void GenC::cleanupTemporary(int i, const CiExpr * temp)
{
	if (temp->type->id == CiId::stringStorageType) {
		write("free(citemp");
		visitLiteralLong(i);
		writeLine(");");
	}
}

void GenC::writeVar(const CiNamedValue * def)
{
	GenBase::writeVar(def);
	if (needToDestruct(def)) {
		const CiVar * local = static_cast<const CiVar *>(def);
		this->varsToDestruct.push_back(local);
	}
}

void GenC::writeGPointerCast(const CiType * type, const CiExpr * expr)
{
	if (dynamic_cast<const CiNumericType *>(type) || dynamic_cast<const CiEnum *>(type)) {
		write("GINT_TO_POINTER(");
		expr->accept(this, CiPriority::argument);
		writeChar(')');
	}
	else if (type->id == CiId::stringPtrType && expr->type->id == CiId::stringPtrType) {
		write("(gpointer) ");
		expr->accept(this, CiPriority::primary);
	}
	else
		writeCoerced(type, expr, CiPriority::argument);
}

void GenC::writeGConstPointerCast(const CiExpr * expr)
{
	if (dynamic_cast<const CiClassType *>(expr->type.get()) && !dynamic_cast<const CiStorageType *>(expr->type.get()))
		expr->accept(this, CiPriority::argument);
	else {
		write("(gconstpointer) ");
		expr->accept(this, CiPriority::primary);
	}
}

void GenC::writeQueueObject(const CiExpr * obj)
{
	if (dynamic_cast<const CiStorageType *>(obj->type.get())) {
		writeChar('&');
		obj->accept(this, CiPriority::primary);
	}
	else
		obj->accept(this, CiPriority::argument);
}

void GenC::writeQueueGet(std::string_view function, const CiExpr * obj, CiPriority parent)
{
	const CiType * elementType = obj->type->asClassType()->getElementType().get();
	bool parenthesis;
	if (dynamic_cast<const CiIntegerType *>(elementType) && elementType->id != CiId::longType) {
		write("GPOINTER_TO_INT(");
		parenthesis = true;
	}
	else {
		parenthesis = parent > CiPriority::mul;
		if (parenthesis)
			writeChar('(');
		writeStaticCastType(elementType);
	}
	write(function);
	writeChar('(');
	writeQueueObject(obj);
	writeChar(')');
	if (parenthesis)
		writeChar(')');
}

void GenC::startDictionaryInsert(const CiExpr * dict, const CiExpr * key)
{
	const CiClassType * type = static_cast<const CiClassType *>(dict->type.get());
	write(type->class_->id == CiId::sortedDictionaryClass ? "g_tree_insert(" : "g_hash_table_insert(");
	dict->accept(this, CiPriority::argument);
	write(", ");
	writeGPointerCast(type->getKeyType(), key);
	write(", ");
}

void GenC::writeAssign(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiBinaryExpr * indexing;
	const CiClassType * dict;
	if ((indexing = dynamic_cast<const CiBinaryExpr *>(expr->left.get())) && indexing->op == CiToken::leftBracket && (dict = dynamic_cast<const CiClassType *>(indexing->left->type.get())) && dict->class_->typeParameterCount == 2) {
		startDictionaryInsert(indexing->left.get(), indexing->right.get());
		writeGPointerCast(dict->getValueType().get(), expr->right.get());
		writeChar(')');
	}
	else if (expr->left->type->id == CiId::stringStorageType) {
		const CiExpr * length = isTrimSubstring(expr);
		if (length != nullptr && parent == CiPriority::statement) {
			writeIndexing(expr->left.get(), length);
			write(" = '\\0'");
		}
		else {
			this->stringAssign = true;
			write("CiString_Assign(&");
			expr->left->accept(this, CiPriority::primary);
			write(", ");
			writeStringStorageValue(expr->right.get());
			writeChar(')');
		}
	}
	else if (const CiDynamicPtrType *dynamic = dynamic_cast<const CiDynamicPtrType *>(expr->left->type.get())) {
		if (dynamic->class_->id == CiId::regexClass) {
			GenBase::writeAssign(expr, parent);
		}
		else {
			this->sharedAssign = true;
			write("CiShared_Assign((void **) &");
			expr->left->accept(this, CiPriority::primary);
			write(", ");
			if (dynamic_cast<const CiSymbolReference *>(expr->right.get())) {
				this->sharedAddRef = true;
				write("CiShared_AddRef(");
				expr->right->accept(this, CiPriority::argument);
				writeChar(')');
			}
			else
				expr->right->accept(this, CiPriority::argument);
			writeChar(')');
		}
	}
	else
		GenBase::writeAssign(expr, parent);
}

const CiMethod * GenC::getThrowingMethod(const CiExpr * expr)
{
	const CiBinaryExpr * binary;
	if ((binary = dynamic_cast<const CiBinaryExpr *>(expr)) && binary->op == CiToken::assign)
		return getThrowingMethod(binary->right.get());
	else if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr)) {
		const CiMethod * method = static_cast<const CiMethod *>(call->method->symbol);
		return method->throws ? method : nullptr;
	}
	else
		return nullptr;
}

bool GenC::hasListDestroy(const CiType * type)
{
	const CiStorageType * list;
	return (list = dynamic_cast<const CiStorageType *>(type)) && (list->class_->id == CiId::listClass || list->class_->id == CiId::stackClass) && needToDestructType(list->getElementType().get());
}

bool GenC::hasInitCode(const CiNamedValue * def) const
{
	if (def->isAssignableStorage())
		return false;
	const CiClassType * klass;
	const CiStorageType * storage;
	return (dynamic_cast<const CiField *>(def) && (def->value != nullptr || isHeapAllocated(def->type->getStorageType()) || ((klass = dynamic_cast<const CiClassType *>(def->type.get())) && (klass->class_->id == CiId::listClass || klass->class_->id == CiId::dictionaryClass || klass->class_->id == CiId::sortedDictionaryClass)))) || getThrowingMethod(def->value.get()) != nullptr || ((storage = dynamic_cast<const CiStorageType *>(def->type->getStorageType())) && (storage->class_->id == CiId::lockClass || needsConstructor(storage->class_))) || hasListDestroy(def->type.get()) || GenBase::hasInitCode(def);
}

CiPriority GenC::startForwardThrow(const CiMethod * throwingMethod)
{
	write("if (");
	switch (throwingMethod->type->id) {
	case CiId::floatType:
	case CiId::doubleType:
		includeMath();
		write("isnan(");
		return CiPriority::argument;
	case CiId::voidType:
		writeChar('!');
		return CiPriority::primary;
	default:
		return CiPriority::equality;
	}
}

void GenC::writeDestruct(const CiSymbol * symbol)
{
	if (!needToDestruct(symbol))
		return;
	ensureChildBlock();
	const CiType * type = symbol->type.get();
	int nesting = 0;
	while (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(type)) {
		write("for (int _i");
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
	bool arrayFree = false;
	if (const CiDynamicPtrType *dynamic = dynamic_cast<const CiDynamicPtrType *>(type)) {
		if (dynamic->class_->id == CiId::regexClass)
			write("g_regex_unref(");
		else {
			this->sharedRelease = true;
			write("CiShared_Release(");
		}
	}
	else if (const CiStorageType *storage = dynamic_cast<const CiStorageType *>(type)) {
		switch (storage->class_->id) {
		case CiId::listClass:
		case CiId::stackClass:
			write("g_array_free(");
			arrayFree = true;
			break;
		case CiId::queueClass:
			write("g_queue_clear(&");
			break;
		case CiId::hashSetClass:
		case CiId::dictionaryClass:
			write("g_hash_table_unref(");
			break;
		case CiId::sortedSetClass:
		case CiId::sortedDictionaryClass:
			write("g_tree_unref(");
			break;
		case CiId::matchClass:
			write("g_match_info_free(");
			break;
		case CiId::lockClass:
			write("mtx_destroy(&");
			break;
		default:
			writeName(storage->class_);
			write("_Destruct(&");
			break;
		}
	}
	else
		write("free(");
	writeLocalName(symbol, CiPriority::primary);
	for (int i = 0; i < nesting; i++) {
		write("[_i");
		visitLiteralLong(i);
		writeChar(']');
	}
	if (arrayFree)
		write(", TRUE");
	writeLine(");");
	this->indent -= nesting;
}

void GenC::writeDestructAll(const CiVar * exceptVar)
{
	for (int i = this->varsToDestruct.size(); --i >= 0;) {
		const CiVar * def = this->varsToDestruct[i];
		if (def != exceptVar)
			writeDestruct(def);
	}
}

void GenC::writeThrowReturnValue()
{
	if (dynamic_cast<const CiNumericType *>(this->currentMethod->type.get())) {
		if (dynamic_cast<const CiIntegerType *>(this->currentMethod->type.get()))
			write("-1");
		else {
			includeMath();
			write("NAN");
		}
	}
	else if (this->currentMethod->type->id == CiId::voidType)
		write("false");
	else
		write("NULL");
}

void GenC::writeThrow()
{
	writeDestructAll();
	write("return ");
	writeThrowReturnValue();
	writeCharLine(';');
}

void GenC::endForwardThrow(const CiMethod * throwingMethod)
{
	switch (throwingMethod->type->id) {
	case CiId::floatType:
	case CiId::doubleType:
		writeChar(')');
		break;
	case CiId::voidType:
		break;
	default:
		write(dynamic_cast<const CiIntegerType *>(throwingMethod->type.get()) ? " == -1" : " == NULL");
		break;
	}
	writeChar(')');
	if (this->varsToDestruct.size() > 0) {
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

void GenC::writeInitCode(const CiNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	const CiType * type = def->type.get();
	int nesting = 0;
	while (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(type)) {
		openLoop("int", nesting++, array->length);
		type = array->getElementType().get();
	}
	const CiStorageType * lok;
	if ((lok = dynamic_cast<const CiStorageType *>(type)) && lok->class_->id == CiId::lockClass) {
		write("mtx_init(&");
		writeArrayElement(def, nesting);
		writeLine(", mtx_plain | mtx_recursive);");
	}
	else {
		const CiStorageType * storage;
		if ((storage = dynamic_cast<const CiStorageType *>(type)) && needsConstructor(storage->class_)) {
			writeName(storage->class_);
			write("_Construct(&");
			writeArrayElement(def, nesting);
			writeLine(");");
		}
		else {
			if (dynamic_cast<const CiField *>(def)) {
				writeArrayElement(def, nesting);
				if (nesting > 0) {
					write(" = ");
					if (isHeapAllocated(type))
						write("NULL");
					else
						def->value->accept(this, CiPriority::argument);
				}
				else
					writeVarInit(def);
				writeCharLine(';');
			}
			const CiMethod * throwingMethod = getThrowingMethod(def->value.get());
			if (throwingMethod != nullptr) {
				startForwardThrow(throwingMethod);
				writeArrayElement(def, nesting);
				endForwardThrow(throwingMethod);
			}
		}
	}
	if (hasListDestroy(type)) {
		write("g_array_set_clear_func(");
		writeArrayElement(def, nesting);
		write(", ");
		if (dynamic_cast<const CiStringStorageType *>(type->asClassType()->getElementType().get())) {
			this->listFrees["String"] = "free(*(void **) ptr)";
			write("CiList_FreeString");
		}
		else if (dynamic_cast<const CiDynamicPtrType *>(type->asClassType()->getElementType().get())) {
			this->sharedRelease = true;
			this->listFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
			write("CiList_FreeShared");
		}
		else if (const CiStorageType *storage = dynamic_cast<const CiStorageType *>(type->asClassType()->getElementType().get())) {
			switch (storage->class_->id) {
			case CiId::listClass:
			case CiId::stackClass:
				this->listFrees["List"] = "g_array_free(*(GArray **) ptr, TRUE)";
				write("CiList_FreeList");
				break;
			case CiId::hashSetClass:
			case CiId::dictionaryClass:
				this->listFrees["HashTable"] = "g_hash_table_unref(*(GHashTable **) ptr)";
				write("CiList_FreeHashTable");
				break;
			case CiId::sortedSetClass:
			case CiId::sortedDictionaryClass:
				this->listFrees["Tree"] = "g_tree_unref(*(GTree **) ptr)";
				write("CiList_FreeTree");
				break;
			default:
				write("(GDestroyNotify) ");
				writeName(storage->class_);
				write("_Destruct");
				break;
			}
		}
		else
			std::abort();
		writeLine(");");
	}
	while (--nesting >= 0)
		closeBlock();
	GenBase::writeInitCode(def);
}

void GenC::writeMemberAccess(const CiExpr * left, const CiSymbol * symbolClass)
{
	if (dynamic_cast<const CiStorageType *>(left->type.get()))
		writeChar('.');
	else
		write("->");
	for (const CiSymbol * klass = left->type->asClassType()->class_; klass != symbolClass; klass = klass->parent)
		write("base.");
}

void GenC::writeMemberOp(const CiExpr * left, const CiSymbolReference * symbol)
{
	writeMemberAccess(left, symbol->symbol->parent);
}

void GenC::writeArrayPtr(const CiExpr * expr, CiPriority parent)
{
	const CiClassType * list;
	if ((list = dynamic_cast<const CiClassType *>(expr->type.get())) && list->class_->id == CiId::listClass) {
		writeChar('(');
		writeType(list->getElementType().get(), false);
		write(" *) ");
		writePostfix(expr, "->data");
	}
	else
		expr->accept(this, parent);
}

void GenC::writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent)
{
	const CiDynamicPtrType * dynamic;
	const CiClassType * klass;
	if ((dynamic = dynamic_cast<const CiDynamicPtrType *>(type)) && dynamic_cast<const CiSymbolReference *>(expr) && parent != CiPriority::equality) {
		this->sharedAddRef = true;
		if (dynamic->class_->id == CiId::arrayPtrClass)
			writeDynamicArrayCast(dynamic->getElementType().get());
		else {
			writeChar('(');
			writeName(dynamic->class_);
			write(" *) ");
		}
		writeCall("CiShared_AddRef", expr);
	}
	else if ((klass = dynamic_cast<const CiClassType *>(type)) && klass->class_->id != CiId::stringClass && klass->class_->id != CiId::arrayPtrClass && !dynamic_cast<const CiStorageType *>(klass)) {
		if (klass->class_->id == CiId::queueClass && dynamic_cast<const CiStorageType *>(expr->type.get())) {
			writeChar('&');
			expr->accept(this, CiPriority::primary);
		}
		else
			writeClassPtr(klass->class_, expr, parent);
	}
	else {
		if (type->id == CiId::stringStorageType)
			writeStringStorageValue(expr);
		else if (expr->type->id == CiId::stringStorageType)
			writeTemporaryOrExpr(expr, parent);
		else
			GenTyped::writeCoercedInternal(type, expr, parent);
	}
}

void GenC::writeSubstringEqual(const CiCallExpr * call, std::string_view literal, CiPriority parent, bool not_)
{
	if (parent > CiPriority::equality)
		writeChar('(');
	include("string.h");
	write("memcmp(");
	writeStringPtrAdd(call);
	write(", ");
	visitLiteralString(literal);
	write(", ");
	visitLiteralLong(literal.length());
	writeChar(')');
	write(getEqOp(not_));
	writeChar('0');
	if (parent > CiPriority::equality)
		writeChar(')');
}

void GenC::writeEqualStringInternal(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	if (parent > CiPriority::equality)
		writeChar('(');
	include("string.h");
	write("strcmp(");
	writeTemporaryOrExpr(left, CiPriority::argument);
	write(", ");
	writeTemporaryOrExpr(right, CiPriority::argument);
	writeChar(')');
	write(getEqOp(not_));
	writeChar('0');
	if (parent > CiPriority::equality)
		writeChar(')');
}

void GenC::writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	if (dynamic_cast<const CiStringType *>(left->type.get()) && dynamic_cast<const CiStringType *>(right->type.get())) {
		const CiCallExpr * call = isStringSubstring(left);
		const CiLiteralString * literal;
		if (call != nullptr && (literal = dynamic_cast<const CiLiteralString *>(right))) {
			const CiExpr * lengthExpr = getStringSubstringLength(call);
			int rightLength = literal->getAsciiLength();
			if (rightLength >= 0) {
				std::string_view rightValue = literal->value;
				if (const CiLiteralLong *leftLength = dynamic_cast<const CiLiteralLong *>(lengthExpr)) {
					if (leftLength->value != rightLength)
						notYet(left, "String comparison with unmatched length");
					writeSubstringEqual(call, rightValue, parent, not_);
				}
				else if (not_) {
					if (parent > CiPriority::condOr)
						writeChar('(');
					lengthExpr->accept(this, CiPriority::equality);
					write(" != ");
					visitLiteralLong(rightLength);
					write(" || ");
					writeSubstringEqual(call, rightValue, CiPriority::condOr, true);
					if (parent > CiPriority::condOr)
						writeChar(')');
				}
				else {
					if (parent > CiPriority::condAnd || parent == CiPriority::condOr)
						writeChar('(');
					lengthExpr->accept(this, CiPriority::equality);
					write(" == ");
					visitLiteralLong(rightLength);
					write(" && ");
					writeSubstringEqual(call, rightValue, CiPriority::condAnd, false);
					if (parent > CiPriority::condAnd || parent == CiPriority::condOr)
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

void GenC::writeStringLength(const CiExpr * expr)
{
	include("string.h");
	writeCall("(int) strlen", expr);
}

void GenC::writeStringMethod(std::string_view name, const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	include("string.h");
	write("CiString_");
	writeCall(name, obj, (*args)[0].get());
}

void GenC::writeSizeofCompare(const CiType * elementType)
{
	write(", sizeof(");
	CiId typeId = elementType->id;
	writeNumericType(typeId);
	write("), CiCompare_");
	writeNumericType(typeId);
	writeChar(')');
	this->compares.insert(typeId);
}

void GenC::writeArrayFill(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	write("for (int _i = 0; _i < ");
	if (args->size() == 1)
		writeArrayStorageLength(obj);
	else
		(*args)[2]->accept(this, CiPriority::rel);
	writeLine("; _i++)");
	writeChar('\t');
	obj->accept(this, CiPriority::primary);
	writeChar('[');
	if (args->size() > 1)
		startAdd((*args)[1].get());
	write("_i] = ");
	(*args)[0]->accept(this, CiPriority::argument);
}

void GenC::writeListAddInsert(const CiExpr * obj, bool insert, std::string_view function, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	const CiType * elementType = obj->type->asClassType()->getElementType().get();
	int id = writeCTemporary(elementType, elementType->isFinal() ? nullptr : args->back().get());
	const CiStorageType * storage;
	if ((storage = dynamic_cast<const CiStorageType *>(elementType)) && needsConstructor(storage->class_)) {
		writeName(storage->class_);
		write("_Construct(&citemp");
		visitLiteralLong(id);
		writeLine(");");
	}
	write(function);
	writeChar('(');
	obj->accept(this, CiPriority::argument);
	if (insert) {
		write(", ");
		(*args)[0]->accept(this, CiPriority::argument);
	}
	write(", citemp");
	visitLiteralLong(id);
	writeChar(')');
	this->currentTemporaries[id] = elementType;
}

void GenC::writeDictionaryLookup(const CiExpr * obj, std::string_view function, const CiExpr * key)
{
	write(function);
	writeChar('(');
	obj->accept(this, CiPriority::argument);
	write(", ");
	writeGConstPointerCast(key);
	writeChar(')');
}

void GenC::writeArgsAndRightParenthesis(const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	int i = 0;
	for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
		if (i > 0 || method->callType != CiCallType::static_)
			write(", ");
		if (i >= args->size())
			param->value->accept(this, CiPriority::argument);
		else
			writeCoerced(param->type.get(), (*args)[i].get(), CiPriority::argument);
		i++;
	}
	writeChar(')');
}

void GenC::writeCRegexOptions(const std::vector<std::shared_ptr<CiExpr>> * args)
{
	if (!writeRegexOptions(args, "", " | ", "", "G_REGEX_CASELESS", "G_REGEX_MULTILINE", "G_REGEX_DOTALL"))
		writeChar('0');
}

void GenC::writePrintfNotInterpolated(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine)
{
	write("\"%");
	if (const CiIntegerType *intType = dynamic_cast<const CiIntegerType *>((*args)[0]->type.get())) {
		if (intType->id == CiId::longType)
			writePrintfLongPrefix();
		writeChar('d');
	}
	else if (dynamic_cast<const CiFloatingType *>((*args)[0]->type.get()))
		writeChar('g');
	else
		writeChar('s');
	if (newLine)
		write("\\n");
	write("\", ");
	writeInterpolatedStringArgBase((*args)[0].get());
	writeChar(')');
}

void GenC::writeTextWriterWrite(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine)
{
	if (args->size() == 0) {
		write("putc('\\n', ");
		obj->accept(this, CiPriority::argument);
		writeChar(')');
	}
	else if (const CiInterpolatedString *interpolated = dynamic_cast<const CiInterpolatedString *>((*args)[0].get())) {
		write("fprintf(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writePrintf(interpolated, newLine);
	}
	else if (dynamic_cast<const CiNumericType *>((*args)[0]->type.get())) {
		write("fprintf(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writePrintfNotInterpolated(args, newLine);
	}
	else if (!newLine) {
		write("fputs(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		obj->accept(this, CiPriority::argument);
		writeChar(')');
	}
	else if (const CiLiteralString *literal = dynamic_cast<const CiLiteralString *>((*args)[0].get())) {
		write("fputs(");
		writeStringLiteralWithNewLine(literal->value);
		write(", ");
		obj->accept(this, CiPriority::argument);
		writeChar(')');
	}
	else {
		write("fprintf(");
		obj->accept(this, CiPriority::argument);
		write(", \"%s\\n\", ");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
	}
}

void GenC::writeConsoleWrite(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine)
{
	include("stdio.h");
	if (args->size() == 0)
		write("putchar('\\n')");
	else if (const CiInterpolatedString *interpolated = dynamic_cast<const CiInterpolatedString *>((*args)[0].get())) {
		write("printf(");
		writePrintf(interpolated, newLine);
	}
	else if (dynamic_cast<const CiNumericType *>((*args)[0]->type.get())) {
		write("printf(");
		writePrintfNotInterpolated(args, newLine);
	}
	else if (!newLine) {
		write("fputs(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", stdout)");
	}
	else
		writeCall("puts", (*args)[0].get());
}

const CiClass * GenC::getVtblStructClass(const CiClass * klass)
{
	while (!klass->addsVirtualMethods()) {
		const CiClass * baseClass = static_cast<const CiClass *>(klass->parent);
		klass = baseClass;
	}
	return klass;
}

const CiClass * GenC::getVtblPtrClass(const CiClass * klass)
{
	for (const CiClass * result = nullptr;;) {
		if (klass->addsVirtualMethods())
			result = klass;
		const CiClass * baseClass;
		if (!(baseClass = dynamic_cast<const CiClass *>(klass->parent)))
			return result;
		klass = baseClass;
	}
}

void GenC::writeCCall(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	const CiClass * klass = this->currentClass;
	const CiClass * declaringClass = static_cast<const CiClass *>(method->parent);
	if (isReferenceTo(obj, CiId::basePtr)) {
		writeName(method);
		write("(&self->base");
		writeUpcast(declaringClass, klass->parent);
	}
	else {
		const CiClass * definingClass = declaringClass;
		switch (method->callType) {
		case CiCallType::abstract:
		case CiCallType::virtual_:
		case CiCallType::override_:
			if (method->callType == CiCallType::override_) {
				const CiClass * declaringClass1 = static_cast<const CiClass *>(method->getDeclaringMethod()->parent);
				declaringClass = declaringClass1;
			}
			if (obj != nullptr)
				klass = obj->type->asClassType()->class_;
			{
				const CiClass * ptrClass = getVtblPtrClass(klass);
				const CiClass * structClass = getVtblStructClass(definingClass);
				if (structClass != ptrClass) {
					write("((const ");
					writeName(structClass);
					write("Vtbl *) ");
				}
				if (obj != nullptr) {
					obj->accept(this, CiPriority::primary);
					writeMemberAccess(obj, ptrClass);
				}
				else
					writeSelfForField(ptrClass);
				write("vtbl");
				if (structClass != ptrClass)
					writeChar(')');
				write("->");
				writeCamelCase(method->name);
				break;
			}
		default:
			writeName(method);
			break;
		}
		writeChar('(');
		if (method->callType != CiCallType::static_) {
			if (obj != nullptr)
				writeClassPtr(declaringClass, obj, CiPriority::argument);
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

void GenC::writeTryParse(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	includeStdBool();
	write("_TryParse(&");
	obj->accept(this, CiPriority::primary);
	write(", ");
	(*args)[0]->accept(this, CiPriority::argument);
	if (dynamic_cast<const CiIntegerType *>(obj->type.get()))
		writeTryParseRadix(args);
	writeChar(')');
}

void GenC::writeStringSubstring(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	if (args->size() == 1) {
		if (parent > CiPriority::add)
			writeChar('(');
		writeAdd(obj, (*args)[0].get());
		if (parent > CiPriority::add)
			writeChar(')');
	}
	else
		notSupported(obj, "Substring");
}

void GenC::startArrayIndexing(const CiExpr * obj, const CiType * elementType)
{
	write("g_array_index(");
	obj->accept(this, CiPriority::argument);
	write(", ");
	writeType(elementType, false);
	write(", ");
}

void GenC::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::none:
	case CiId::classToString:
		writeCCall(obj, method, args);
		break;
	case CiId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case CiId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case CiId::intTryParse:
		this->intTryParse = true;
		write("CiInt");
		writeTryParse(obj, args);
		break;
	case CiId::longTryParse:
		this->longTryParse = true;
		write("CiLong");
		writeTryParse(obj, args);
		break;
	case CiId::doubleTryParse:
		this->doubleTryParse = true;
		write("CiDouble");
		writeTryParse(obj, args);
		break;
	case CiId::stringContains:
		include("string.h");
		if (parent > CiPriority::equality)
			writeChar('(');
		{
			int c = getOneAscii((*args)[0].get());
			if (c >= 0) {
				write("strchr(");
				obj->accept(this, CiPriority::argument);
				write(", ");
				visitLiteralChar(c);
				writeChar(')');
			}
			else
				writeCall("strstr", obj, (*args)[0].get());
			write(" != NULL");
			if (parent > CiPriority::equality)
				writeChar(')');
			break;
		}
	case CiId::stringEndsWith:
		this->stringEndsWith = true;
		writeStringMethod("EndsWith", obj, args);
		break;
	case CiId::stringIndexOf:
		this->stringIndexOf = true;
		writeStringMethod("IndexOf", obj, args);
		break;
	case CiId::stringLastIndexOf:
		this->stringLastIndexOf = true;
		writeStringMethod("LastIndexOf", obj, args);
		break;
	case CiId::stringReplace:
		include("string.h");
		this->stringAppend = true;
		this->stringReplace = true;
		writeCall("CiString_Replace", obj, (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::stringStartsWith:
		if (parent > CiPriority::equality)
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
				obj->accept(this, CiPriority::argument);
				write(", ");
				(*args)[0]->accept(this, CiPriority::argument);
				write(", strlen(");
				(*args)[0]->accept(this, CiPriority::argument);
				write(")) == 0");
			}
			if (parent > CiPriority::equality)
				writeChar(')');
			break;
		}
	case CiId::stringSubstring:
		writeStringSubstring(obj, args, parent);
		break;
	case CiId::arrayBinarySearchAll:
	case CiId::arrayBinarySearchPart:
		if (parent > CiPriority::add)
			writeChar('(');
		write("(const ");
		{
			const CiType * elementType2 = obj->type->asClassType()->getElementType().get();
			writeType(elementType2, false);
			write(" *) bsearch(&");
			(*args)[0]->accept(this, CiPriority::primary);
			write(", ");
			if (args->size() == 1)
				writeArrayPtr(obj, CiPriority::argument);
			else
				writeArrayPtrAdd(obj, (*args)[1].get());
			write(", ");
			if (args->size() == 1)
				writeArrayStorageLength(obj);
			else
				(*args)[2]->accept(this, CiPriority::argument);
			writeSizeofCompare(elementType2);
			write(" - ");
			writeArrayPtr(obj, CiPriority::mul);
			if (parent > CiPriority::add)
				writeChar(')');
			break;
		}
	case CiId::arrayCopyTo:
	case CiId::listCopyTo:
		include("string.h");
		{
			const CiType * elementType = obj->type->asClassType()->getElementType().get();
			if (isHeapAllocated(elementType))
				notYet(obj, "CopyTo for this type");
			write("memcpy(");
			writeArrayPtrAdd((*args)[1].get(), (*args)[2].get());
			write(", ");
			writeArrayPtrAdd(obj, (*args)[0].get());
			write(", ");
			if (elementType->id == CiId::sByteRange || elementType->id == CiId::byteRange)
				(*args)[3]->accept(this, CiPriority::argument);
			else {
				(*args)[3]->accept(this, CiPriority::mul);
				write(" * sizeof(");
				writeType(elementType, false);
				writeChar(')');
			}
			writeChar(')');
			break;
		}
	case CiId::arrayFillAll:
	case CiId::arrayFillPart:
		{
			const CiLiteral * literal;
			if ((literal = dynamic_cast<const CiLiteral *>((*args)[0].get())) && literal->isDefaultValue()) {
				include("string.h");
				write("memset(");
				if (args->size() == 1) {
					obj->accept(this, CiPriority::argument);
					write(", 0, sizeof(");
					obj->accept(this, CiPriority::argument);
					writeChar(')');
				}
				else {
					writeArrayPtrAdd(obj, (*args)[1].get());
					write(", 0, ");
					(*args)[2]->accept(this, CiPriority::mul);
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
	case CiId::arraySortAll:
		write("qsort(");
		writeArrayPtr(obj, CiPriority::argument);
		write(", ");
		writeArrayStorageLength(obj);
		writeSizeofCompare(obj->type->asClassType()->getElementType().get());
		break;
	case CiId::arraySortPart:
	case CiId::listSortPart:
		write("qsort(");
		writeArrayPtrAdd(obj, (*args)[0].get());
		write(", ");
		(*args)[1]->accept(this, CiPriority::argument);
		writeSizeofCompare(obj->type->asClassType()->getElementType().get());
		break;
	case CiId::listAdd:
	case CiId::stackPush:
		const CiStorageType * storage;
		if (dynamic_cast<const CiArrayStorageType *>(obj->type->asClassType()->getElementType().get()) || ((storage = dynamic_cast<const CiStorageType *>(obj->type->asClassType()->getElementType().get())) && storage->class_->id == CiId::none && !needsConstructor(storage->class_))) {
			write("g_array_set_size(");
			obj->accept(this, CiPriority::argument);
			write(", ");
			writePostfix(obj, "->len + 1)");
		}
		else
			writeListAddInsert(obj, false, "g_array_append_val", args);
		break;
	case CiId::listClear:
	case CiId::stackClear:
		write("g_array_set_size(");
		obj->accept(this, CiPriority::argument);
		write(", 0)");
		break;
	case CiId::listContains:
		write("CiArray_Contains_");
		{
			CiId typeId = obj->type->asClassType()->getElementType()->id;
			switch (typeId) {
			case CiId::none:
				write("object(");
				break;
			case CiId::stringStorageType:
			case CiId::stringPtrType:
				typeId = CiId::stringPtrType;
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
			writePostfix(obj, "->data, ");
			writePostfix(obj, "->len, ");
			(*args)[0]->accept(this, CiPriority::argument);
			writeChar(')');
			this->contains.insert(typeId);
			break;
		}
	case CiId::listInsert:
		writeListAddInsert(obj, true, "g_array_insert_val", args);
		break;
	case CiId::listLast:
	case CiId::stackPeek:
		startArrayIndexing(obj, obj->type->asClassType()->getElementType().get());
		writePostfix(obj, "->len - 1)");
		break;
	case CiId::listRemoveAt:
		writeCall("g_array_remove_index", obj, (*args)[0].get());
		break;
	case CiId::listRemoveRange:
		writeCall("g_array_remove_range", obj, (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::listSortAll:
		write("g_array_sort(");
		obj->accept(this, CiPriority::argument);
		{
			CiId typeId2 = obj->type->asClassType()->getElementType()->id;
			write(", CiCompare_");
			writeNumericType(typeId2);
			writeChar(')');
			this->compares.insert(typeId2);
			break;
		}
	case CiId::queueClear:
		write("g_queue_clear(");
		writeQueueObject(obj);
		writeChar(')');
		break;
	case CiId::queueDequeue:
		writeQueueGet("g_queue_pop_head", obj, parent);
		break;
	case CiId::queueEnqueue:
		write("g_queue_push_tail(");
		writeQueueObject(obj);
		write(", ");
		writeGPointerCast(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		writeChar(')');
		break;
	case CiId::queuePeek:
		writeQueueGet("g_queue_peek_head", obj, parent);
		break;
	case CiId::stackPop:
		startArrayIndexing(obj, obj->type->asClassType()->getElementType().get());
		write("--");
		writePostfix(obj, "->len)");
		break;
	case CiId::hashSetAdd:
		write("g_hash_table_add(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writeGPointerCast(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		writeChar(')');
		break;
	case CiId::hashSetClear:
	case CiId::dictionaryClear:
		writeCall("g_hash_table_remove_all", obj);
		break;
	case CiId::hashSetContains:
	case CiId::dictionaryContainsKey:
		writeDictionaryLookup(obj, "g_hash_table_contains", (*args)[0].get());
		break;
	case CiId::hashSetRemove:
	case CiId::dictionaryRemove:
		writeDictionaryLookup(obj, "g_hash_table_remove", (*args)[0].get());
		break;
	case CiId::sortedSetAdd:
		write("g_tree_insert(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writeGPointerCast(obj->type->asClassType()->getKeyType(), (*args)[0].get());
		write(", NULL)");
		break;
	case CiId::dictionaryAdd:
		startDictionaryInsert(obj, (*args)[0].get());
		{
			const CiClassType * valueType = obj->type->asClassType()->getValueType()->asClassType();
			switch (valueType->class_->id) {
			case CiId::listClass:
			case CiId::stackClass:
			case CiId::dictionaryClass:
			case CiId::sortedDictionaryClass:
				writeNewStorage(valueType);
				break;
			default:
				if (valueType->class_->isPublic && valueType->class_->constructor != nullptr && valueType->class_->constructor->visibility == CiVisibility::public_) {
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
	case CiId::sortedSetClear:
	case CiId::sortedDictionaryClear:
		write("g_tree_destroy(g_tree_ref(");
		obj->accept(this, CiPriority::argument);
		write("))");
		break;
	case CiId::sortedSetContains:
	case CiId::sortedDictionaryContainsKey:
		write("g_tree_lookup_extended(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writeGConstPointerCast((*args)[0].get());
		write(", NULL, NULL)");
		break;
	case CiId::sortedSetRemove:
	case CiId::sortedDictionaryRemove:
		writeDictionaryLookup(obj, "g_tree_remove", (*args)[0].get());
		break;
	case CiId::textWriterWrite:
		writeTextWriterWrite(obj, args, false);
		break;
	case CiId::textWriterWriteChar:
		writeCall("putc", (*args)[0].get(), obj);
		break;
	case CiId::textWriterWriteLine:
		writeTextWriterWrite(obj, args, true);
		break;
	case CiId::consoleWrite:
		writeConsoleWrite(args, false);
		break;
	case CiId::consoleWriteLine:
		writeConsoleWrite(args, true);
		break;
	case CiId::uTF8GetByteCount:
		writeStringLength((*args)[0].get());
		break;
	case CiId::uTF8GetBytes:
		include("string.h");
		write("memcpy(");
		writeArrayPtrAdd((*args)[1].get(), (*args)[2].get());
		write(", ");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", strlen(");
		(*args)[0]->accept(this, CiPriority::argument);
		write("))");
		break;
	case CiId::environmentGetEnvironmentVariable:
		writeCall("getenv", (*args)[0].get());
		break;
	case CiId::regexCompile:
		writeGlib("g_regex_new(");
		writeTemporaryOrExpr((*args)[0].get(), CiPriority::argument);
		write(", ");
		writeCRegexOptions(args);
		write(", 0, NULL)");
		break;
	case CiId::regexEscape:
		writeGlib("g_regex_escape_string(");
		writeTemporaryOrExpr((*args)[0].get(), CiPriority::argument);
		write(", -1)");
		break;
	case CiId::regexIsMatchStr:
		writeGlib("g_regex_match_simple(");
		writeTemporaryOrExpr((*args)[1].get(), CiPriority::argument);
		write(", ");
		writeTemporaryOrExpr((*args)[0].get(), CiPriority::argument);
		write(", ");
		writeCRegexOptions(args);
		write(", 0)");
		break;
	case CiId::regexIsMatchRegex:
		write("g_regex_match(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writeTemporaryOrExpr((*args)[0].get(), CiPriority::argument);
		write(", 0, NULL)");
		break;
	case CiId::matchFindStr:
		this->matchFind = true;
		write("CiMatch_Find(&");
		obj->accept(this, CiPriority::primary);
		write(", ");
		writeTemporaryOrExpr((*args)[0].get(), CiPriority::argument);
		write(", ");
		writeTemporaryOrExpr((*args)[1].get(), CiPriority::argument);
		write(", ");
		writeCRegexOptions(args);
		writeChar(')');
		break;
	case CiId::matchFindRegex:
		write("g_regex_match(");
		(*args)[1]->accept(this, CiPriority::argument);
		write(", ");
		writeTemporaryOrExpr((*args)[0].get(), CiPriority::argument);
		write(", 0, &");
		obj->accept(this, CiPriority::primary);
		writeChar(')');
		break;
	case CiId::matchGetCapture:
		writeCall("g_match_info_fetch", obj, (*args)[0].get());
		break;
	case CiId::mathMethod:
	case CiId::mathIsFinite:
	case CiId::mathIsNaN:
	case CiId::mathLog2:
		includeMath();
		writeLowercase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathAbs:
		switch ((*args)[0]->type->id) {
		case CiId::longType:
			writeCall("llabs", (*args)[0].get());
			break;
		case CiId::floatType:
			includeMath();
			writeCall("fabsf", (*args)[0].get());
			break;
		case CiId::floatIntType:
		case CiId::doubleType:
			includeMath();
			writeCall("fabs", (*args)[0].get());
			break;
		default:
			writeCall("abs", (*args)[0].get());
			break;
		}
		break;
	case CiId::mathCeiling:
		includeMath();
		writeCall("ceil", (*args)[0].get());
		break;
	case CiId::mathFusedMultiplyAdd:
		includeMath();
		writeCall("fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case CiId::mathIsInfinity:
		includeMath();
		writeCall("isinf", (*args)[0].get());
		break;
	case CiId::mathMaxDouble:
		includeMath();
		writeCall("fmax", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::mathMinDouble:
		includeMath();
		writeCall("fmin", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::mathRound:
		includeMath();
		writeCall("round", (*args)[0].get());
		break;
	case CiId::mathTruncate:
		includeMath();
		writeCall("trunc", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenC::writeDictionaryIndexing(std::string_view function, const CiBinaryExpr * expr, CiPriority parent)
{
	const CiType * valueType = expr->left->type->asClassType()->getValueType().get();
	if (dynamic_cast<const CiIntegerType *>(valueType) && valueType->id != CiId::longType) {
		write("GPOINTER_TO_INT(");
		writeDictionaryLookup(expr->left.get(), function, expr->right.get());
		writeChar(')');
	}
	else {
		if (parent > CiPriority::mul)
			writeChar('(');
		const CiStorageType * storage;
		if ((storage = dynamic_cast<const CiStorageType *>(valueType)) && (storage->class_->id == CiId::none || storage->class_->id == CiId::arrayStorageClass))
			writeDynamicArrayCast(valueType);
		else {
			writeStaticCastType(valueType);
			if (dynamic_cast<const CiEnum *>(valueType)) {
				assert(parent <= CiPriority::mul && "Should close two parens");
				write("GPOINTER_TO_INT(");
			}
		}
		writeDictionaryLookup(expr->left.get(), function, expr->right.get());
		if (parent > CiPriority::mul || dynamic_cast<const CiEnum *>(valueType))
			writeChar(')');
	}
}

void GenC::writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	if (const CiClassType *klass = dynamic_cast<const CiClassType *>(expr->left->type.get())) {
		switch (klass->class_->id) {
		case CiId::listClass:
			if (dynamic_cast<const CiArrayStorageType *>(klass->getElementType().get())) {
				writeChar('(');
				writeDynamicArrayCast(klass->getElementType().get());
				writePostfix(expr->left.get(), "->data)[");
				expr->right->accept(this, CiPriority::argument);
				writeChar(']');
			}
			else {
				startArrayIndexing(expr->left.get(), klass->getElementType().get());
				expr->right->accept(this, CiPriority::argument);
				writeChar(')');
			}
			return;
		case CiId::dictionaryClass:
			writeDictionaryIndexing("g_hash_table_lookup", expr, parent);
			return;
		case CiId::sortedDictionaryClass:
			writeDictionaryIndexing("g_tree_lookup", expr, parent);
			return;
		default:
			break;
		}
	}
	GenBase::writeIndexingExpr(expr, parent);
}

void GenC::visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::plus:
		if (expr->type->id == CiId::stringStorageType)
			notSupported(expr, "String concatenation");
		break;
	case CiToken::equal:
	case CiToken::notEqual:
	case CiToken::greater:
		{
			const CiExpr * str = isStringEmpty(expr);
			if (str != nullptr) {
				writePostfix(str, expr->op == CiToken::equal ? "[0] == '\\0'" : "[0] != '\\0'");
				return;
			}
			break;
		}
	case CiToken::addAssign:
		if (expr->left->type->id == CiId::stringStorageType) {
			if (const CiInterpolatedString *rightInterpolated = dynamic_cast<const CiInterpolatedString *>(expr->right.get())) {
				this->stringAssign = true;
				write("CiString_Assign(&");
				expr->left->accept(this, CiPriority::primary);
				this->stringFormat = true;
				include("stdarg.h");
				include("stdio.h");
				write(", CiString_Format(\"%s");
				writePrintfFormat(rightInterpolated);
				write("\", ");
				expr->left->accept(this, CiPriority::argument);
				writeInterpolatedStringArgs(rightInterpolated);
				writeChar(')');
			}
			else {
				include("string.h");
				this->stringAppend = true;
				write("CiString_Append(&");
				expr->left->accept(this, CiPriority::primary);
				write(", ");
				expr->right->accept(this, CiPriority::argument);
			}
			writeChar(')');
			return;
		}
		break;
	case CiToken::is:
		notSupported(expr, "'is' operator");
		break;
	default:
		break;
	}
	GenBase::visitBinaryExpr(expr, parent);
}

void GenC::writeResource(std::string_view name, int length)
{
	write("CiResource_");
	writeResourceName(name);
}

void GenC::visitLambdaExpr(const CiLambdaExpr * expr)
{
	notSupported(expr, "Lambda expression");
}

void GenC::writeDestructLoopOrSwitch(const CiCondCompletionStatement * loopOrSwitch)
{
	for (int i = this->varsToDestruct.size(); --i >= 0;) {
		const CiVar * def = this->varsToDestruct[i];
		if (!loopOrSwitch->encloses(def))
			break;
		writeDestruct(def);
	}
}

void GenC::trimVarsToDestruct(int i)
{
	this->varsToDestruct.erase(this->varsToDestruct.begin() + i, this->varsToDestruct.begin() + i + (this->varsToDestruct.size() - i));
}

void GenC::cleanupBlock(const CiBlock * statement)
{
	int i = this->varsToDestruct.size();
	for (; i > 0; i--) {
		const CiVar * def = this->varsToDestruct[i - 1];
		if (def->parent != statement)
			break;
		if (statement->completesNormally())
			writeDestruct(def);
	}
	trimVarsToDestruct(i);
}

void GenC::visitBreak(const CiBreak * statement)
{
	writeDestructLoopOrSwitch(statement->loopOrSwitch);
	GenCCppD::visitBreak(statement);
}

void GenC::visitContinue(const CiContinue * statement)
{
	writeDestructLoopOrSwitch(statement->loop);
	GenBase::visitContinue(statement);
}

void GenC::visitExpr(const CiExpr * statement)
{
	writeCTemporaries(statement);
	const CiMethod * throwingMethod = getThrowingMethod(statement);
	if (throwingMethod != nullptr) {
		ensureChildBlock();
		statement->accept(this, startForwardThrow(throwingMethod));
		endForwardThrow(throwingMethod);
		cleanupTemporaries();
	}
	else if (dynamic_cast<const CiCallExpr *>(statement) && statement->type->id == CiId::stringStorageType) {
		write("free(");
		statement->accept(this, CiPriority::argument);
		writeLine(");");
		cleanupTemporaries();
	}
	else if (dynamic_cast<const CiCallExpr *>(statement) && dynamic_cast<const CiDynamicPtrType *>(statement->type.get())) {
		this->sharedRelease = true;
		write("CiShared_Release(");
		statement->accept(this, CiPriority::argument);
		writeLine(");");
		cleanupTemporaries();
	}
	else
		GenBase::visitExpr(statement);
}

void GenC::startForeachHashTable(const CiForeach * statement)
{
	openBlock();
	writeLine("GHashTableIter cidictit;");
	write("g_hash_table_iter_init(&cidictit, ");
	statement->collection->accept(this, CiPriority::argument);
	writeLine(");");
}

void GenC::writeDictIterVar(const CiNamedValue * iter, std::string_view value)
{
	writeTypeAndName(iter);
	write(" = ");
	if (dynamic_cast<const CiIntegerType *>(iter->type.get()) && iter->type->id != CiId::longType) {
		write("GPOINTER_TO_INT(");
		write(value);
		writeChar(')');
	}
	else {
		writeStaticCastType(iter->type.get());
		write(value);
	}
	writeCharLine(';');
}

void GenC::visitForeach(const CiForeach * statement)
{
	std::string_view element = statement->getVar()->name;
	if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(statement->collection->type.get())) {
		write("for (int ");
		writeCamelCaseNotKeyword(element);
		write(" = 0; ");
		writeCamelCaseNotKeyword(element);
		write(" < ");
		visitLiteralLong(array->length);
		write("; ");
		writeCamelCaseNotKeyword(element);
		write("++)");
		writeChild(statement->body.get());
	}
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(statement->collection->type.get())) {
		switch (klass->class_->id) {
		case CiId::stringClass:
			write("for (");
			writeStringPtrType();
			writeCamelCaseNotKeyword(element);
			write(" = ");
			statement->collection->accept(this, CiPriority::argument);
			write("; *");
			writeCamelCaseNotKeyword(element);
			write(" != '\\0'; ");
			writeCamelCaseNotKeyword(element);
			write("++)");
			writeChild(statement->body.get());
			break;
		case CiId::listClass:
			write("for (");
			{
				const CiType * elementType = klass->getElementType().get();
				writeType(elementType, false);
				write(" const *");
				writeCamelCaseNotKeyword(element);
				write(" = (");
				writeType(elementType, false);
				write(" const *) ");
				writePostfix(statement->collection.get(), "->data, ");
				for (; elementType->isArray(); elementType = elementType->asClassType()->getElementType().get())
					writeChar('*');
				if (dynamic_cast<const CiClassType *>(elementType))
					write("* const ");
				write("*ciend = ");
				writeCamelCaseNotKeyword(element);
				write(" + ");
				writePostfix(statement->collection.get(), "->len; ");
				writeCamelCaseNotKeyword(element);
				write(" < ciend; ");
				writeCamelCaseNotKeyword(element);
				write("++)");
				writeChild(statement->body.get());
				break;
			}
		case CiId::hashSetClass:
			startForeachHashTable(statement);
			writeLine("gpointer cikey;");
			write("while (g_hash_table_iter_next(&cidictit, &cikey, NULL)) ");
			openBlock();
			writeDictIterVar(statement->getVar(), "cikey");
			flattenBlock(statement->body.get());
			closeBlock();
			closeBlock();
			break;
		case CiId::sortedSetClass:
			write("for (GTreeNode *cisetit = g_tree_node_first(");
			statement->collection->accept(this, CiPriority::argument);
			write("); cisetit != NULL; cisetit = g_tree_node_next(cisetit)) ");
			openBlock();
			writeDictIterVar(statement->getVar(), "g_tree_node_key(cisetit)");
			flattenBlock(statement->body.get());
			closeBlock();
			break;
		case CiId::dictionaryClass:
			startForeachHashTable(statement);
			writeLine("gpointer cikey, civalue;");
			write("while (g_hash_table_iter_next(&cidictit, &cikey, &civalue)) ");
			openBlock();
			writeDictIterVar(statement->getVar(), "cikey");
			writeDictIterVar(statement->getValueVar(), "civalue");
			flattenBlock(statement->body.get());
			closeBlock();
			closeBlock();
			break;
		case CiId::sortedDictionaryClass:
			write("for (GTreeNode *cidictit = g_tree_node_first(");
			statement->collection->accept(this, CiPriority::argument);
			write("); cidictit != NULL; cidictit = g_tree_node_next(cidictit)) ");
			openBlock();
			writeDictIterVar(statement->getVar(), "g_tree_node_key(cidictit)");
			writeDictIterVar(statement->getValueVar(), "g_tree_node_value(cidictit)");
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

void GenC::visitLock(const CiLock * statement)
{
	write("mtx_lock(&");
	statement->lock->accept(this, CiPriority::primary);
	writeLine(");");
	statement->body->acceptStatement(this);
	write("mtx_unlock(&");
	statement->lock->accept(this, CiPriority::primary);
	writeLine(");");
}

void GenC::visitReturn(const CiReturn * statement)
{
	if (statement->value == nullptr) {
		writeDestructAll();
		writeLine(this->currentMethod->throws ? "return true;" : "return;");
	}
	else if (dynamic_cast<const CiLiteral *>(statement->value.get()) || (this->varsToDestruct.size() == 0 && !hasTemporariesToDestruct(statement->value.get()))) {
		writeDestructAll();
		writeCTemporaries(statement->value.get());
		GenBase::visitReturn(statement);
	}
	else {
		const CiSymbolReference * symbol;
		const CiVar * local;
		if ((symbol = dynamic_cast<const CiSymbolReference *>(statement->value.get())) && (local = dynamic_cast<const CiVar *>(symbol->symbol))) {
			if (std::find(this->varsToDestruct.begin(), this->varsToDestruct.end(), local) != this->varsToDestruct.end()) {
				writeDestructAll(local);
				write("return ");
				if (const CiClassType *resultPtr = dynamic_cast<const CiClassType *>(this->currentMethod->type.get()))
					writeClassPtr(resultPtr->class_, symbol, CiPriority::argument);
				else
					symbol->accept(this, CiPriority::argument);
				writeCharLine(';');
				return;
			}
			writeDestructAll();
			GenBase::visitReturn(statement);
			return;
		}
		writeCTemporaries(statement->value.get());
		ensureChildBlock();
		startDefinition(this->currentMethod->type.get(), true, true);
		write("returnValue = ");
		writeCoerced(this->currentMethod->type.get(), statement->value.get(), CiPriority::argument);
		writeCharLine(';');
		cleanupTemporaries();
		writeDestructAll();
		writeLine("return returnValue;");
	}
}

void GenC::writeSwitchCaseBody(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	const CiConst * konst;
	if (dynamic_cast<const CiVar *>((*statements)[0].get()) || ((konst = dynamic_cast<const CiConst *>((*statements)[0].get())) && dynamic_cast<const CiArrayStorageType *>(konst->type.get())))
		writeCharLine(';');
	int varsToDestructCount = this->varsToDestruct.size();
	writeStatements(statements);
	trimVarsToDestruct(varsToDestructCount);
}

void GenC::visitSwitch(const CiSwitch * statement)
{
	if (statement->isTypeMatching())
		notSupported(statement, "Type-matching 'switch'");
	else
		GenCCpp::visitSwitch(statement);
}

void GenC::visitThrow(const CiThrow * statement)
{
	writeThrow();
}

bool GenC::tryWriteCallAndReturn(const std::vector<std::shared_ptr<CiStatement>> * statements, int lastCallIndex, const CiExpr * returnValue)
{
	if (this->varsToDestruct.size() > 0)
		return false;
	for (int i = 0; i < lastCallIndex; i++) {
		const CiVar * def;
		if ((def = dynamic_cast<const CiVar *>((*statements)[i].get())) && needToDestruct(def))
			return false;
	}
	const CiExpr * call;
	if (!(call = dynamic_cast<const CiExpr *>((*statements)[lastCallIndex].get())))
		return false;
	const CiMethod * throwingMethod = getThrowingMethod(call);
	if (throwingMethod == nullptr)
		return false;
	writeFirstStatements(statements, lastCallIndex);
	write("return ");
	if (dynamic_cast<const CiNumericType *>(throwingMethod->type.get())) {
		if (dynamic_cast<const CiIntegerType *>(throwingMethod->type.get())) {
			call->accept(this, CiPriority::equality);
			write(" != -1");
		}
		else {
			includeMath();
			write("!isnan(");
			call->accept(this, CiPriority::argument);
			writeChar(')');
		}
	}
	else if (throwingMethod->type->id == CiId::voidType)
		call->accept(this, CiPriority::select);
	else {
		call->accept(this, CiPriority::equality);
		write(" != NULL");
	}
	if (returnValue != nullptr) {
		write(" ? ");
		returnValue->accept(this, CiPriority::select);
		write(" : ");
		writeThrowReturnValue();
	}
	writeCharLine(';');
	return true;
}

void GenC::writeStatements(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	int i = statements->size() - 2;
	const CiReturn * ret;
	if (i >= 0 && (ret = dynamic_cast<const CiReturn *>((*statements)[i + 1].get())) && tryWriteCallAndReturn(statements, i, ret->value.get()))
		return;
	GenBase::writeStatements(statements);
}

void GenC::writeEnum(const CiEnum * enu)
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

void GenC::writeTypedef(const CiClass * klass)
{
	if (klass->callType == CiCallType::static_)
		return;
	write("typedef struct ");
	writeName(klass);
	writeChar(' ');
	writeName(klass);
	writeCharLine(';');
}

void GenC::writeTypedefs(const CiProgram * program, bool pub)
{
	for (const CiSymbol * type = program->first; type != nullptr; type = type->next) {
		if (const CiClass *klass = dynamic_cast<const CiClass *>(type)) {
			if (klass->isPublic == pub)
				writeTypedef(klass);
		}
		else if (const CiEnum *enu = dynamic_cast<const CiEnum *>(type)) {
			if (enu->isPublic == pub)
				writeEnum(enu);
		}
		else
			std::abort();
	}
}

void GenC::writeInstanceParameters(const CiMethod * method)
{
	writeChar('(');
	if (!method->isMutator)
		write("const ");
	writeName(method->parent);
	write(" *self");
	writeRemainingParameters(method, false, false);
}

void GenC::writeSignature(const CiMethod * method)
{
	const CiClass * klass = static_cast<const CiClass *>(method->parent);
	if (!klass->isPublic || method->visibility != CiVisibility::public_)
		write("static ");
	writeReturnType(method);
	writeName(klass);
	writeChar('_');
	write(method->name);
	if (method->callType != CiCallType::static_)
		writeInstanceParameters(method);
	else if (method->parameters.count() == 0)
		write("(void)");
	else
		writeParameters(method, false);
}

void GenC::writeVtblFields(const CiClass * klass)
{
	if (const CiClass *baseClass = dynamic_cast<const CiClass *>(klass->parent))
		writeVtblFields(baseClass);
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const CiMethod * method;
		if ((method = dynamic_cast<const CiMethod *>(symbol)) && method->isAbstractOrVirtual()) {
			writeReturnType(method);
			write("(*");
			writeCamelCase(method->name);
			writeChar(')');
			writeInstanceParameters(method);
			writeCharLine(';');
		}
	}
}

void GenC::writeVtblStruct(const CiClass * klass)
{
	write("typedef struct ");
	openBlock();
	writeVtblFields(klass);
	this->indent--;
	write("} ");
	writeName(klass);
	writeLine("Vtbl;");
}

std::string_view GenC::getConst(const CiArrayStorageType * array) const
{
	return "const ";
}

void GenC::writeConst(const CiConst * konst)
{
	if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(konst->type.get())) {
		write("static ");
		write(getConst(array));
		writeTypeAndName(konst);
		write(" = ");
		konst->value->accept(this, CiPriority::argument);
		writeCharLine(';');
	}
	else if (konst->visibility == CiVisibility::public_) {
		write("#define ");
		writeName(konst);
		writeChar(' ');
		konst->value->accept(this, CiPriority::argument);
		writeNewLine();
	}
}

void GenC::writeField(const CiField * field)
{
	std::abort();
}

bool GenC::hasVtblValue(const CiClass * klass)
{
	if (klass->callType == CiCallType::static_ || klass->callType == CiCallType::abstract)
		return false;
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (const CiMethod *method = dynamic_cast<const CiMethod *>(symbol)) {
			switch (method->callType) {
			case CiCallType::virtual_:
			case CiCallType::override_:
			case CiCallType::sealed:
				return true;
			default:
				break;
			}
		}
	}
	return false;
}

bool GenC::needsConstructor(const CiClass * klass) const
{
	if (klass->id == CiId::matchClass)
		return false;
	const CiClass * baseClass;
	return GenBase::needsConstructor(klass) || hasVtblValue(klass) || ((baseClass = dynamic_cast<const CiClass *>(klass->parent)) && needsConstructor(baseClass));
}

void GenC::writeXstructorSignature(std::string_view name, const CiClass * klass)
{
	write("static void ");
	writeName(klass);
	writeChar('_');
	write(name);
	writeChar('(');
	writeName(klass);
	write(" *self)");
}

void GenC::writeSignatures(const CiClass * klass, bool pub)
{
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const CiConst * konst;
		const CiMethod * method;
		if ((konst = dynamic_cast<const CiConst *>(symbol)) && (konst->visibility == CiVisibility::public_) == pub) {
			if (pub) {
				writeNewLine();
				writeDoc(konst->documentation.get());
			}
			writeConst(konst);
		}
		else if ((method = dynamic_cast<const CiMethod *>(symbol)) && method->isLive && (method->visibility == CiVisibility::public_) == pub && method->callType != CiCallType::abstract) {
			writeNewLine();
			writeMethodDoc(method);
			writeSignature(method);
			writeCharLine(';');
		}
	}
}

void GenC::writeClassInternal(const CiClass * klass)
{
	this->currentClass = klass;
	if (klass->callType != CiCallType::static_) {
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
		if (dynamic_cast<const CiClass *>(klass->parent)) {
			writeName(klass->parent);
			writeLine(" base;");
		}
		for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
			if (const CiField *field = dynamic_cast<const CiField *>(symbol)) {
				writeDoc(field->documentation.get());
				writeTypeAndName(field);
				writeCharLine(';');
			}
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

void GenC::writeVtbl(const CiClass * definingClass, const CiClass * declaringClass)
{
	if (const CiClass *baseClass = dynamic_cast<const CiClass *>(declaringClass->parent))
		writeVtbl(definingClass, baseClass);
	for (const CiSymbol * symbol = declaringClass->first; symbol != nullptr; symbol = symbol->next) {
		const CiMethod * declaredMethod;
		if ((declaredMethod = dynamic_cast<const CiMethod *>(symbol)) && declaredMethod->isAbstractOrVirtual()) {
			const CiSymbol * definedMethod = definingClass->tryLookup(declaredMethod->name, false).get();
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

void GenC::writeConstructor(const CiClass * klass)
{
	if (!needsConstructor(klass))
		return;
	this->switchesWithGoto.clear();
	writeNewLine();
	writeXstructorSignature("Construct", klass);
	writeNewLine();
	openBlock();
	const CiClass * baseClass;
	if ((baseClass = dynamic_cast<const CiClass *>(klass->parent)) && needsConstructor(baseClass)) {
		writeName(baseClass);
		writeLine("_Construct(&self->base);");
	}
	if (hasVtblValue(klass)) {
		const CiClass * structClass = getVtblStructClass(klass);
		write("static const ");
		writeName(structClass);
		write("Vtbl vtbl = ");
		openBlock();
		writeVtbl(klass, structClass);
		this->indent--;
		writeLine("};");
		const CiClass * ptrClass = getVtblPtrClass(klass);
		writeSelfForField(ptrClass);
		write("vtbl = ");
		if (ptrClass != structClass) {
			write("(const ");
			writeName(ptrClass);
			write("Vtbl *) ");
		}
		writeLine("&vtbl;");
	}
	writeConstructorBody(klass);
	closeBlock();
}

void GenC::writeDestructFields(const CiSymbol * symbol)
{
	if (symbol != nullptr) {
		writeDestructFields(symbol->next);
		if (const CiField *field = dynamic_cast<const CiField *>(symbol))
			writeDestruct(field);
	}
}

void GenC::writeDestructor(const CiClass * klass)
{
	if (!needsDestructor(klass))
		return;
	writeNewLine();
	writeXstructorSignature("Destruct", klass);
	writeNewLine();
	openBlock();
	writeDestructFields(klass->first);
	const CiClass * baseClass;
	if ((baseClass = dynamic_cast<const CiClass *>(klass->parent)) && needsDestructor(baseClass)) {
		writeName(baseClass);
		writeLine("_Destruct(&self->base);");
	}
	closeBlock();
}

void GenC::writeNewDelete(const CiClass * klass, bool define)
{
	if (!klass->isPublic || klass->constructor == nullptr || klass->constructor->visibility != CiVisibility::public_)
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

void GenC::writeMethod(const CiMethod * method)
{
	if (!method->isLive || method->callType == CiCallType::abstract)
		return;
	this->switchesWithGoto.clear();
	writeNewLine();
	writeSignature(method);
	for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
		if (needToDestruct(param))
			this->varsToDestruct.push_back(param);
	}
	writeNewLine();
	this->currentMethod = method;
	openBlock();
	if (const CiBlock *block = dynamic_cast<const CiBlock *>(method->body.get())) {
		const std::vector<std::shared_ptr<CiStatement>> * statements = &block->statements;
		if (!block->completesNormally())
			writeStatements(statements);
		else if (method->throws && method->type->id == CiId::voidType) {
			if (statements->size() == 0 || !tryWriteCallAndReturn(statements, statements->size() - 1, nullptr)) {
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

void GenC::writeTryParseLibrary(std::string_view signature, std::string_view call)
{
	writeNewLine();
	write("static bool Ci");
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
	if (this->intTryParse)
		writeTryParseLibrary("Int_TryParse(int *result, const char *str, int base)", "l(str, &end, base");
	if (this->longTryParse)
		writeTryParseLibrary("Long_TryParse(int64_t *result, const char *str, int base)", "ll(str, &end, base");
	if (this->doubleTryParse)
		writeTryParseLibrary("Double_TryParse(double *result, const char *str)", "d(str, &end");
	if (this->stringAssign) {
		writeNewLine();
		writeLine("static void CiString_Assign(char **str, char *value)");
		openBlock();
		writeLine("free(*str);");
		writeLine("*str = value;");
		closeBlock();
	}
	if (this->stringSubstring) {
		writeNewLine();
		writeLine("static char *CiString_Substring(const char *str, int len)");
		openBlock();
		writeLine("char *p = malloc(len + 1);");
		writeLine("memcpy(p, str, len);");
		writeLine("p[len] = '\\0';");
		writeLine("return p;");
		closeBlock();
	}
	if (this->stringAppend) {
		writeNewLine();
		writeLine("static void CiString_AppendSubstring(char **str, const char *suffix, size_t suffixLen)");
		openBlock();
		writeLine("if (suffixLen == 0)");
		writeLine("\treturn;");
		writeLine("size_t prefixLen = *str == NULL ? 0 : strlen(*str);");
		writeLine("*str = realloc(*str, prefixLen + suffixLen + 1);");
		writeLine("memcpy(*str + prefixLen, suffix, suffixLen);");
		writeLine("(*str)[prefixLen + suffixLen] = '\\0';");
		closeBlock();
		writeNewLine();
		writeLine("static void CiString_Append(char **str, const char *suffix)");
		openBlock();
		writeLine("CiString_AppendSubstring(str, suffix, strlen(suffix));");
		closeBlock();
	}
	if (this->stringIndexOf) {
		writeNewLine();
		writeLine("static int CiString_IndexOf(const char *str, const char *needle)");
		openBlock();
		writeLine("const char *p = strstr(str, needle);");
		writeLine("return p == NULL ? -1 : (int) (p - str);");
		closeBlock();
	}
	if (this->stringLastIndexOf) {
		writeNewLine();
		writeLine("static int CiString_LastIndexOf(const char *str, const char *needle)");
		openBlock();
		writeLine("if (needle[0] == '\\0')");
		writeLine("\treturn (int) strlen(str);");
		writeLine("int result = -1;");
		writeLine("const char *p = strstr(str, needle);");
		write("while (p != NULL) ");
		openBlock();
		writeLine("result = (int) (p - str);");
		writeLine("p = strstr(p + 1, needle);");
		closeBlock();
		writeLine("return result;");
		closeBlock();
	}
	if (this->stringEndsWith) {
		writeNewLine();
		writeLine("static bool CiString_EndsWith(const char *str, const char *suffix)");
		openBlock();
		writeLine("size_t strLen = strlen(str);");
		writeLine("size_t suffixLen = strlen(suffix);");
		writeLine("return strLen >= suffixLen && memcmp(str + strLen - suffixLen, suffix, suffixLen) == 0;");
		closeBlock();
	}
	if (this->stringReplace) {
		writeNewLine();
		writeLine("static char *CiString_Replace(const char *s, const char *oldValue, const char *newValue)");
		openBlock();
		write("for (char *result = NULL;;) ");
		openBlock();
		writeLine("const char *p = strstr(s, oldValue);");
		writeLine("if (p == NULL) {");
		writeLine("\tCiString_Append(&result, s);");
		writeLine("\treturn result == NULL ? strdup(\"\") : result;");
		writeCharLine('}');
		writeLine("CiString_AppendSubstring(&result, s, p - s);");
		writeLine("CiString_Append(&result, newValue);");
		writeLine("s = p + strlen(oldValue);");
		closeBlock();
		closeBlock();
	}
	if (this->stringFormat) {
		writeNewLine();
		writeLine("static char *CiString_Format(const char *format, ...)");
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
		writeLine("static bool CiMatch_Find(GMatchInfo **match_info, const char *input, const char *pattern, GRegexCompileFlags options)");
		openBlock();
		writeLine("GRegex *regex = g_regex_new(pattern, options, 0, NULL);");
		writeLine("bool result = g_regex_match(regex, input, 0, match_info);");
		writeLine("g_regex_unref(regex);");
		writeLine("return result;");
		closeBlock();
	}
	if (this->matchPos) {
		writeNewLine();
		writeLine("static int CiMatch_GetPos(const GMatchInfo *match_info, int which)");
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
		writeLine("static void CiPtr_Construct(void **ptr)");
		openBlock();
		writeLine("*ptr = NULL;");
		closeBlock();
	}
	if (this->sharedMake || this->sharedAddRef || this->sharedRelease) {
		writeNewLine();
		writeLine("typedef void (*CiMethodPtr)(void *);");
		writeLine("typedef struct {");
		this->indent++;
		writeLine("size_t count;");
		writeLine("size_t unitSize;");
		writeLine("size_t refCount;");
		writeLine("CiMethodPtr destructor;");
		this->indent--;
		writeLine("} CiShared;");
	}
	if (this->sharedMake) {
		writeNewLine();
		writeLine("static void *CiShared_Make(size_t count, size_t unitSize, CiMethodPtr constructor, CiMethodPtr destructor)");
		openBlock();
		writeLine("CiShared *self = (CiShared *) malloc(sizeof(CiShared) + count * unitSize);");
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
		writeLine("static void *CiShared_AddRef(void *ptr)");
		openBlock();
		writeLine("if (ptr != NULL)");
		writeLine("\t((CiShared *) ptr)[-1].refCount++;");
		writeLine("return ptr;");
		closeBlock();
	}
	if (this->sharedRelease || this->sharedAssign) {
		writeNewLine();
		writeLine("static void CiShared_Release(void *ptr)");
		openBlock();
		writeLine("if (ptr == NULL)");
		writeLine("\treturn;");
		writeLine("CiShared *self = (CiShared *) ptr - 1;");
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
		writeLine("static void CiShared_Assign(void **ptr, void *value)");
		openBlock();
		writeLine("CiShared_Release(*ptr);");
		writeLine("*ptr = value;");
		closeBlock();
	}
	for (const auto &[name, content] : this->listFrees) {
		writeNewLine();
		write("static void CiList_Free");
		write(name);
		writeLine("(void *ptr)");
		openBlock();
		write(content);
		writeCharLine(';');
		closeBlock();
	}
	if (this->treeCompareInteger) {
		writeNewLine();
		write("static int CiTree_CompareInteger(gconstpointer pa, gconstpointer pb, gpointer user_data)");
		openBlock();
		writeLine("gintptr a = (gintptr) pa;");
		writeLine("gintptr b = (gintptr) pb;");
		writeLine("return (a > b) - (a < b);");
		closeBlock();
	}
	if (this->treeCompareString) {
		writeNewLine();
		write("static int CiTree_CompareString(gconstpointer a, gconstpointer b, gpointer user_data)");
		openBlock();
		writeLine("return strcmp((const char *) a, (const char *) b);");
		closeBlock();
	}
	for (CiId typeId : this->compares) {
		writeNewLine();
		write("static int CiCompare_");
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
		case CiId::byteRange:
		case CiId::sByteRange:
		case CiId::shortRange:
		case CiId::uShortRange:
			writeLine("return a - b;");
			break;
		default:
			writeLine("return (a > b) - (a < b);");
			break;
		}
		closeBlock();
	}
	for (CiId typeId : this->contains) {
		writeNewLine();
		write("static bool CiArray_Contains_");
		if (typeId == CiId::none)
			write("object(const void * const *a, size_t len, const void *");
		else if (typeId == CiId::stringPtrType)
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
		if (typeId == CiId::stringPtrType)
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
	if (resources->size() == 0)
		return;
	writeNewLine();
	for (const auto &[name, content] : *resources) {
		write("static const ");
		writeNumericType(CiId::byteRange);
		writeChar(' ');
		writeResource(name, -1);
		writeChar('[');
		visitLiteralLong(content.size());
		writeLine("] = {");
		writeChar('\t');
		writeBytes(&content);
		writeLine(" };");
	}
}

void GenC::writeProgram(const CiProgram * program)
{
	this->writtenClasses.clear();
	this->inHeaderFile = true;
	openStringWriter();
	for (const CiClass * klass : program->classes) {
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
	for (const CiClass * klass : program->classes)
		writeClass(klass, program);
	writeResources(&program->resources);
	for (const CiClass * klass : program->classes) {
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

void GenCl::writeNumericType(CiId id)
{
	switch (id) {
	case CiId::sByteRange:
		write("char");
		break;
	case CiId::byteRange:
		write("uchar");
		break;
	case CiId::shortRange:
		write("short");
		break;
	case CiId::uShortRange:
		write("ushort");
		break;
	case CiId::intType:
		write("int");
		break;
	case CiId::longType:
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

void GenCl::writeClassType(const CiClassType * klass, bool space)
{
	switch (klass->class_->id) {
	case CiId::none:
		if (dynamic_cast<const CiDynamicPtrType *>(klass))
			notSupported(klass, "Dynamic reference");
		else
			GenC::writeClassType(klass, space);
		break;
	case CiId::stringClass:
		if (klass->id == CiId::stringStorageType)
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

void GenCl::writeInterpolatedStringArgBase(const CiExpr * expr)
{
	expr->accept(this, CiPriority::argument);
}

void GenCl::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
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

std::string_view GenCl::getConst(const CiArrayStorageType * array) const
{
	return array->ptrTaken ? "const " : "constant ";
}

void GenCl::writeSubstringEqual(const CiCallExpr * call, std::string_view literal, CiPriority parent, bool not_)
{
	if (not_)
		writeChar('!');
	if (isUTF8GetString(call)) {
		this->bytesEqualsString = true;
		write("CiBytes_Equals(");
	}
	else {
		this->stringStartsWith = true;
		write("CiString_StartsWith(");
	}
	writeStringPtrAdd(call);
	write(", ");
	visitLiteralString(literal);
	writeChar(')');
}

void GenCl::writeEqualStringInternal(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	this->stringEquals = true;
	if (not_)
		writeChar('!');
	writeCall("CiString_Equals", left, right);
}

void GenCl::writeStringLength(const CiExpr * expr)
{
	this->stringLength = true;
	writeCall("strlen", expr);
}

void GenCl::writeConsoleWrite(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine)
{
	write("printf(");
	if (args->size() == 0)
		write("\"\\n\")");
	else if (const CiInterpolatedString *interpolated = dynamic_cast<const CiInterpolatedString *>((*args)[0].get()))
		writePrintf(interpolated, newLine);
	else
		writePrintfNotInterpolated(args, newLine);
}

void GenCl::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::none:
	case CiId::classToString:
		writeCCall(obj, method, args);
		break;
	case CiId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case CiId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case CiId::stringStartsWith:
		{
			int c = getOneAscii((*args)[0].get());
			if (c >= 0) {
				if (parent > CiPriority::equality)
					writeChar('(');
				writePostfix(obj, "[0] == ");
				visitLiteralChar(c);
				if (parent > CiPriority::equality)
					writeChar(')');
			}
			else {
				this->stringStartsWith = true;
				writeCall("CiString_StartsWith", obj, (*args)[0].get());
			}
			break;
		}
	case CiId::stringSubstring:
		writeStringSubstring(obj, args, parent);
		break;
	case CiId::arrayCopyTo:
		write("for (size_t _i = 0; _i < ");
		(*args)[3]->accept(this, CiPriority::rel);
		writeLine("; _i++)");
		writeChar('\t');
		(*args)[1]->accept(this, CiPriority::primary);
		writeChar('[');
		startAdd((*args)[2].get());
		write("_i] = ");
		obj->accept(this, CiPriority::primary);
		writeChar('[');
		startAdd((*args)[0].get());
		write("_i]");
		break;
	case CiId::arrayFillAll:
	case CiId::arrayFillPart:
		writeArrayFill(obj, args);
		break;
	case CiId::consoleWrite:
		writeConsoleWrite(args, false);
		break;
	case CiId::consoleWriteLine:
		writeConsoleWrite(args, true);
		break;
	case CiId::uTF8GetByteCount:
		writeStringLength((*args)[0].get());
		break;
	case CiId::uTF8GetBytes:
		write("for (size_t _i = 0; ");
		(*args)[0]->accept(this, CiPriority::primary);
		writeLine("[_i] != '\\0'; _i++)");
		writeChar('\t');
		(*args)[1]->accept(this, CiPriority::primary);
		writeChar('[');
		startAdd((*args)[2].get());
		write("_i] = ");
		writePostfix((*args)[0].get(), "[_i]");
		break;
	case CiId::mathMethod:
	case CiId::mathClamp:
	case CiId::mathIsFinite:
	case CiId::mathIsNaN:
	case CiId::mathLog2:
	case CiId::mathMaxInt:
	case CiId::mathMinInt:
	case CiId::mathRound:
		writeLowercase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathAbs:
		if (dynamic_cast<const CiFloatingType *>((*args)[0]->type.get()))
			writeChar('f');
		writeCall("abs", (*args)[0].get());
		break;
	case CiId::mathCeiling:
		writeCall("ceil", (*args)[0].get());
		break;
	case CiId::mathFusedMultiplyAdd:
		writeCall("fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case CiId::mathIsInfinity:
		writeCall("isinf", (*args)[0].get());
		break;
	case CiId::mathMaxDouble:
		writeCall("fmax", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::mathMinDouble:
		writeCall("fmin", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::mathTruncate:
		writeCall("trunc", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenCl::writeAssert(const CiAssert * statement)
{
}

void GenCl::writeSwitchCaseBody(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	if (std::all_of(statements->begin(), statements->end(), [](const std::shared_ptr<CiStatement> &statement) { return dynamic_cast<const CiAssert *>(statement.get()); }))
		writeCharLine(';');
	else
		GenC::writeSwitchCaseBody(statements);
}

void GenCl::writeLibrary()
{
	if (this->stringLength) {
		writeNewLine();
		writeLine("static int strlen(constant char *str)");
		openBlock();
		writeLine("int len = 0;");
		writeLine("while (str[len] != '\\0')");
		writeLine("\tlen++;");
		writeLine("return len;");
		closeBlock();
	}
	if (this->stringEquals) {
		writeNewLine();
		writeLine("static bool CiString_Equals(constant char *str1, constant char *str2)");
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
		writeLine("static bool CiString_StartsWith(constant char *str1, constant char *str2)");
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
		writeLine("static bool CiBytes_Equals(const uchar *mem, constant char *str)");
		openBlock();
		writeLine("for (int i = 0; str[i] != '\\0'; i++) {");
		writeLine("\tif (mem[i] != str[i])");
		writeLine("\t\treturn false;");
		writeCharLine('}');
		writeLine("return true;");
		closeBlock();
	}
}

void GenCl::writeProgram(const CiProgram * program)
{
	this->writtenClasses.clear();
	this->stringLength = false;
	this->stringEquals = false;
	this->stringStartsWith = false;
	this->bytesEqualsString = false;
	openStringWriter();
	for (const CiClass * klass : program->classes) {
		this->currentClass = klass;
		writeConstructor(klass);
		writeDestructor(klass);
		writeMethods(klass);
	}
	createOutputFile();
	writeTopLevelNatives(program);
	writeRegexOptionsEnum(program);
	writeTypedefs(program, true);
	for (const CiClass * klass : program->classes)
		writeSignatures(klass, true);
	writeTypedefs(program, false);
	for (const CiClass * klass : program->classes)
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

void GenCpp::startMethodCall(const CiExpr * obj)
{
	obj->accept(this, CiPriority::primary);
	writeMemberOp(obj, nullptr);
}

void GenCpp::writeInterpolatedStringArg(const CiExpr * expr)
{
	const CiClassType * klass;
	if ((klass = dynamic_cast<const CiClassType *>(expr->type.get())) && klass->class_->id != CiId::stringClass) {
		startMethodCall(expr);
		write("toString()");
	}
	else
		GenBase::writeInterpolatedStringArg(expr);
}

void GenCpp::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
{
	include("format");
	write("std::format(\"");
	for (const CiInterpolatedPart &part : expr->parts) {
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

void GenCpp::writeName(const CiSymbol * symbol)
{
	if (dynamic_cast<const CiContainerType *>(symbol))
		write(symbol->name);
	else if (dynamic_cast<const CiVar *>(symbol) || dynamic_cast<const CiMember *>(symbol))
		writeCamelCaseNotKeyword(symbol->name);
	else
		std::abort();
}

void GenCpp::writeLocalName(const CiSymbol * symbol, CiPriority parent)
{
	if (dynamic_cast<const CiField *>(symbol))
		write("this->");
	writeName(symbol);
}

void GenCpp::writeCollectionType(std::string_view name, const CiType * elementType)
{
	include(name);
	write("std::");
	write(name);
	writeChar('<');
	writeType(elementType, false);
	writeChar('>');
}

void GenCpp::writeType(const CiType * type, bool promote)
{
	if (dynamic_cast<const CiIntegerType *>(type))
		writeNumericType(getTypeId(type, promote));
	else if (const CiDynamicPtrType *dynamic = dynamic_cast<const CiDynamicPtrType *>(type)) {
		switch (dynamic->class_->id) {
		case CiId::regexClass:
			include("regex");
			write("std::regex");
			break;
		case CiId::arrayPtrClass:
			include("memory");
			write("std::shared_ptr<");
			writeType(dynamic->getElementType().get(), false);
			write("[]>");
			break;
		default:
			include("memory");
			write("std::shared_ptr<");
			write(dynamic->class_->name);
			writeChar('>');
			break;
		}
	}
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(type))
		do {
			if (klass->class_->typeParameterCount == 0) {
				if (klass->class_->id == CiId::stringClass) {
					std::string_view cppType = klass->id == CiId::stringStorageType ? "string" : "string_view";
					include(cppType);
					write("std::");
					write(cppType);
					break;
				}
				if (!dynamic_cast<const CiReadWriteClassType *>(klass))
					write("const ");
				switch (klass->class_->id) {
				case CiId::textWriterClass:
					include("iostream");
					write("std::ostream");
					break;
				case CiId::stringWriterClass:
					include("sstream");
					write("std::ostringstream");
					break;
				case CiId::regexClass:
					include("regex");
					write("std::regex");
					break;
				case CiId::matchClass:
					include("regex");
					write("std::cmatch");
					break;
				case CiId::lockClass:
					include("mutex");
					write("std::recursive_mutex");
					break;
				default:
					write(klass->class_->name);
					break;
				}
			}
			else if (klass->class_->id == CiId::arrayPtrClass) {
				writeType(klass->getElementType().get(), false);
				if (!dynamic_cast<const CiReadWriteClassType *>(klass))
					write(" const");
			}
			else {
				std::string_view cppType;
				switch (klass->class_->id) {
				case CiId::arrayStorageClass:
					cppType = "array";
					break;
				case CiId::listClass:
					cppType = "vector";
					break;
				case CiId::queueClass:
					cppType = "queue";
					break;
				case CiId::stackClass:
					cppType = "stack";
					break;
				case CiId::hashSetClass:
					cppType = "unordered_set";
					break;
				case CiId::sortedSetClass:
					cppType = "set";
					break;
				case CiId::dictionaryClass:
					cppType = "unordered_map";
					break;
				case CiId::sortedDictionaryClass:
					cppType = "map";
					break;
				default:
					notSupported(type, klass->class_->name);
					cppType = "NOT_SUPPORTED";
					break;
				}
				include(cppType);
				if (!dynamic_cast<const CiReadWriteClassType *>(klass))
					write("const ");
				write("std::");
				write(cppType);
				writeChar('<');
				writeType(klass->typeArg0.get(), false);
				if (const CiArrayStorageType *arrayStorage = dynamic_cast<const CiArrayStorageType *>(klass)) {
					write(", ");
					visitLiteralLong(arrayStorage->length);
				}
				else if (klass->class_->typeParameterCount == 2) {
					write(", ");
					writeType(klass->getValueType().get(), false);
				}
				writeChar('>');
			}
			if (!dynamic_cast<const CiStorageType *>(klass))
				write(" *");
		}
		while (0);
	else
		write(type->name);
}

void GenCpp::writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent)
{
	include("memory");
	write("std::make_shared<");
	writeType(elementType, false);
	write("[]>(");
	lengthExpr->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenCpp::writeNew(const CiReadWriteClassType * klass, CiPriority parent)
{
	include("memory");
	write("std::make_shared<");
	write(klass->class_->name);
	write(">()");
}

void GenCpp::writeStorageInit(const CiNamedValue * def)
{
}

void GenCpp::writeVarInit(const CiNamedValue * def)
{
	if (def->value != nullptr && def->type->id == CiId::stringStorageType) {
		writeChar('{');
		def->value->accept(this, CiPriority::argument);
		writeChar('}');
	}
	else if (dynamic_cast<const CiArrayStorageType *>(def->type.get())) {
		const CiLiteral * literal;
		if (def->value == nullptr) {
		}
		else if ((literal = dynamic_cast<const CiLiteral *>(def->value.get())) && literal->isDefaultValue())
			write(" {}");
		else
			std::abort();
	}
	else
		GenBase::writeVarInit(def);
}

bool GenCpp::isSharedPtr(const CiExpr * expr)
{
	if (dynamic_cast<const CiDynamicPtrType *>(expr->type.get()))
		return true;
	const CiSymbolReference * symbol;
	const CiForeach * loop;
	return (symbol = dynamic_cast<const CiSymbolReference *>(expr)) && (loop = dynamic_cast<const CiForeach *>(symbol->symbol->parent)) && dynamic_cast<const CiDynamicPtrType *>(loop->collection->type->asClassType()->getElementType().get());
}

void GenCpp::writeStaticCast(const CiType * type, const CiExpr * expr)
{
	if (const CiDynamicPtrType *dynamic = dynamic_cast<const CiDynamicPtrType *>(type)) {
		write("std::static_pointer_cast<");
		write(dynamic->class_->name);
	}
	else {
		write("static_cast<");
		writeType(type, false);
	}
	write(">(");
	if (dynamic_cast<const CiStorageType *>(expr->type.get())) {
		writeChar('&');
		expr->accept(this, CiPriority::primary);
	}
	else if (!dynamic_cast<const CiDynamicPtrType *>(type) && isSharedPtr(expr))
		writePostfix(expr, ".get()");
	else
		getStaticCastInner(type, expr)->accept(this, CiPriority::argument);
	writeChar(')');
}

bool GenCpp::needStringPtrData(const CiExpr * expr)
{
	const CiCallExpr * call;
	if ((call = dynamic_cast<const CiCallExpr *>(expr)) && call->method->symbol->id == CiId::environmentGetEnvironmentVariable)
		return false;
	return expr->type->id == CiId::stringPtrType;
}

void GenCpp::writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	if (needStringPtrData(left) && right->type->id == CiId::nullType) {
		writePostfix(left, ".data()");
		write(getEqOp(not_));
		write("nullptr");
	}
	else if (left->type->id == CiId::nullType && needStringPtrData(right)) {
		write("nullptr");
		write(getEqOp(not_));
		writePostfix(right, ".data()");
	}
	else
		GenCCppD::writeEqual(left, right, parent, not_);
}

bool GenCpp::isClassPtr(const CiType * type)
{
	const CiClassType * ptr;
	return (ptr = dynamic_cast<const CiClassType *>(type)) && !dynamic_cast<const CiStorageType *>(type) && ptr->class_->id != CiId::stringClass && ptr->class_->id != CiId::arrayPtrClass;
}

bool GenCpp::isCppPtr(const CiExpr * expr)
{
	if (isClassPtr(expr->type.get())) {
		const CiSymbolReference * symbol;
		const CiForeach * loop;
		if ((symbol = dynamic_cast<const CiSymbolReference *>(expr)) && (loop = dynamic_cast<const CiForeach *>(symbol->symbol->parent)) && dynamic_cast<const CiStorageType *>((symbol->symbol == loop->getVar() ? loop->collection->type->asClassType()->typeArg0 : loop->collection->type->asClassType()->typeArg1).get()))
			return false;
		return true;
	}
	return false;
}

void GenCpp::writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiClassType * klass = static_cast<const CiClassType *>(expr->left->type.get());
	if (parent != CiPriority::assign) {
		switch (klass->class_->id) {
		case CiId::dictionaryClass:
		case CiId::sortedDictionaryClass:
		case CiId::orderedDictionaryClass:
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
		expr->left->accept(this, CiPriority::primary);
		writeChar(')');
	}
	else
		expr->left->accept(this, CiPriority::primary);
	writeChar('[');
	switch (klass->class_->id) {
	case CiId::arrayPtrClass:
	case CiId::arrayStorageClass:
	case CiId::listClass:
		expr->right->accept(this, CiPriority::argument);
		break;
	default:
		writeStronglyCoerced(klass->getKeyType(), expr->right.get());
		break;
	}
	writeChar(']');
}

void GenCpp::writeMemberOp(const CiExpr * left, const CiSymbolReference * symbol)
{
	if (symbol != nullptr && dynamic_cast<const CiConst *>(symbol->symbol))
		write("::");
	else if (isCppPtr(left))
		write("->");
	else
		writeChar('.');
}

void GenCpp::writeEnumAsInt(const CiExpr * expr, CiPriority parent)
{
	write("static_cast<int>(");
	expr->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenCpp::writeCollectionObject(const CiExpr * obj, CiPriority priority)
{
	if (isCppPtr(obj)) {
		writeChar('*');
		obj->accept(this, CiPriority::primary);
	}
	else
		obj->accept(this, priority);
}

void GenCpp::writeBeginEnd(const CiExpr * obj)
{
	startMethodCall(obj);
	write("begin(), ");
	startMethodCall(obj);
	write("end()");
}

void GenCpp::writePtrRange(const CiExpr * obj, const CiExpr * index, const CiExpr * count)
{
	writeArrayPtrAdd(obj, index);
	write(", ");
	writeArrayPtrAdd(obj, index);
	write(" + ");
	count->accept(this, CiPriority::mul);
}

void GenCpp::writeNotRawStringLiteral(const CiExpr * obj, CiPriority priority)
{
	obj->accept(this, priority);
	if (dynamic_cast<const CiLiteralString *>(obj)) {
		include("string_view");
		this->usingStringViewLiterals = true;
		write("sv");
	}
}

void GenCpp::writeStringMethod(const CiExpr * obj, std::string_view name, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	writeNotRawStringLiteral(obj, CiPriority::primary);
	writeChar('.');
	write(name);
	int c = getOneAscii((*args)[0].get());
	if (c >= 0) {
		writeChar('(');
		visitLiteralChar(c);
		writeChar(')');
	}
	else
		writeArgsInParentheses(method, args);
}

void GenCpp::writeAllAnyContains(std::string_view function, const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	include("algorithm");
	write("std::");
	write(function);
	writeChar('(');
	writeBeginEnd(obj);
	write(", ");
	(*args)[0]->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenCpp::writeCString(const CiExpr * expr)
{
	if (dynamic_cast<const CiLiteralString *>(expr))
		expr->accept(this, CiPriority::argument);
	else
		writePostfix(expr, ".data()");
}

void GenCpp::writeRegex(const std::vector<std::shared_ptr<CiExpr>> * args, int argIndex)
{
	include("regex");
	write("std::regex(");
	(*args)[argIndex]->accept(this, CiPriority::argument);
	writeRegexOptions(args, ", std::regex::ECMAScript | ", " | ", "", "std::regex::icase", "std::regex::multiline", "std::regex::NOT_SUPPORTED_singleline");
	writeChar(')');
}

void GenCpp::writeWrite(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine)
{
	include("iostream");
	if (args->size() == 1) {
		if (const CiInterpolatedString *interpolated = dynamic_cast<const CiInterpolatedString *>((*args)[0].get())) {
			bool uppercase = false;
			bool hex = false;
			int flt = 'G';
			for (const CiInterpolatedPart &part : interpolated->parts) {
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
				part.argument->accept(this, CiPriority::mul);
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
			const CiLiteralString * literal;
			if (newLine && (literal = dynamic_cast<const CiLiteralString *>((*args)[0].get()))) {
				writeStringLiteralWithNewLine(literal->value);
				return;
			}
			else if (dynamic_cast<const CiLiteralChar *>((*args)[0].get()))
				writeCall("static_cast<int>", (*args)[0].get());
			else
				(*args)[0]->accept(this, CiPriority::mul);
		}
	}
	if (newLine)
		write(" << '\\n'");
}

void GenCpp::writeRegexArgument(const CiExpr * expr)
{
	if (dynamic_cast<const CiDynamicPtrType *>(expr->type.get()))
		expr->accept(this, CiPriority::argument);
	else {
		writeChar('*');
		expr->accept(this, CiPriority::primary);
	}
}

void GenCpp::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::none:
	case CiId::classToString:
	case CiId::listClear:
	case CiId::stackPush:
	case CiId::hashSetClear:
	case CiId::hashSetContains:
	case CiId::sortedSetClear:
	case CiId::sortedSetContains:
	case CiId::dictionaryClear:
	case CiId::sortedDictionaryClear:
		if (obj != nullptr) {
			if (isReferenceTo(obj, CiId::basePtr)) {
				writeName(method->parent);
				write("::");
			}
			else {
				obj->accept(this, CiPriority::primary);
				if (method->callType == CiCallType::static_)
					write("::");
				else
					writeMemberOp(obj, nullptr);
			}
		}
		writeName(method);
		writeArgsInParentheses(method, args);
		break;
	case CiId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case CiId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case CiId::intTryParse:
	case CiId::longTryParse:
		include("cstdlib");
		write("[&] { char *ciend; ");
		obj->accept(this, CiPriority::assign);
		write(" = std::strtol");
		if (method->id == CiId::longTryParse)
			writeChar('l');
		writeChar('(');
		writeCString((*args)[0].get());
		write(", &ciend");
		writeTryParseRadix(args);
		write("); return *ciend == '\\0'; }()");
		break;
	case CiId::doubleTryParse:
		include("cstdlib");
		write("[&] { char *ciend; ");
		obj->accept(this, CiPriority::assign);
		write(" = std::strtod(");
		writeCString((*args)[0].get());
		write(", &ciend); return *ciend == '\\0'; }()");
		break;
	case CiId::stringContains:
		if (parent > CiPriority::equality)
			writeChar('(');
		writeStringMethod(obj, "find", method, args);
		write(" != std::string::npos");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::stringEndsWith:
		writeStringMethod(obj, "ends_with", method, args);
		break;
	case CiId::stringIndexOf:
		write("static_cast<int>(");
		writeStringMethod(obj, "find", method, args);
		writeChar(')');
		break;
	case CiId::stringLastIndexOf:
		write("static_cast<int>(");
		writeStringMethod(obj, "rfind", method, args);
		writeChar(')');
		break;
	case CiId::stringReplace:
		this->stringReplace = true;
		writeCall("CiString_replace", obj, (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::stringStartsWith:
		writeStringMethod(obj, "starts_with", method, args);
		break;
	case CiId::stringSubstring:
		writeStringMethod(obj, "substr", method, args);
		break;
	case CiId::arrayBinarySearchAll:
	case CiId::arrayBinarySearchPart:
		include("algorithm");
		if (parent > CiPriority::add)
			writeChar('(');
		write("std::lower_bound(");
		if (args->size() == 1)
			writeBeginEnd(obj);
		else
			writePtrRange(obj, (*args)[1].get(), (*args)[2].get());
		write(", ");
		(*args)[0]->accept(this, CiPriority::argument);
		write(") - ");
		writeArrayPtr(obj, CiPriority::mul);
		if (parent > CiPriority::add)
			writeChar(')');
		break;
	case CiId::arrayCopyTo:
	case CiId::listCopyTo:
		include("algorithm");
		write("std::copy_n(");
		writeArrayPtrAdd(obj, (*args)[0].get());
		write(", ");
		(*args)[3]->accept(this, CiPriority::argument);
		write(", ");
		writeArrayPtrAdd((*args)[1].get(), (*args)[2].get());
		writeChar(')');
		break;
	case CiId::arrayFillAll:
		startMethodCall(obj);
		write("fill(");
		writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), CiPriority::argument);
		writeChar(')');
		break;
	case CiId::arrayFillPart:
		include("algorithm");
		write("std::fill_n(");
		writeArrayPtrAdd(obj, (*args)[1].get());
		write(", ");
		(*args)[2]->accept(this, CiPriority::argument);
		write(", ");
		writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), CiPriority::argument);
		writeChar(')');
		break;
	case CiId::arraySortAll:
	case CiId::listSortAll:
		include("algorithm");
		write("std::sort(");
		writeBeginEnd(obj);
		writeChar(')');
		break;
	case CiId::arraySortPart:
	case CiId::listSortPart:
		include("algorithm");
		write("std::sort(");
		writePtrRange(obj, (*args)[0].get(), (*args)[1].get());
		writeChar(')');
		break;
	case CiId::listAdd:
		startMethodCall(obj);
		if (args->size() == 0)
			write("emplace_back()");
		else {
			write("push_back(");
			writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), CiPriority::argument);
			writeChar(')');
		}
		break;
	case CiId::listAddRange:
		startMethodCall(obj);
		write("insert(");
		startMethodCall(obj);
		write("end(), ");
		writeBeginEnd((*args)[0].get());
		writeChar(')');
		break;
	case CiId::listAll:
		writeAllAnyContains("all_of", obj, args);
		break;
	case CiId::listAny:
		include("algorithm");
		writeAllAnyContains("any_of", obj, args);
		break;
	case CiId::listContains:
		if (parent > CiPriority::equality)
			writeChar('(');
		writeAllAnyContains("find", obj, args);
		write(" != ");
		startMethodCall(obj);
		write("end()");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::listIndexOf:
		{
			const CiType * elementType = obj->type->asClassType()->getElementType().get();
			write("[](const ");
			writeCollectionType("vector", elementType);
			write(" &v, ");
			writeType(elementType, false);
			include("algorithm");
			write(" value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(");
			writeCollectionObject(obj, CiPriority::argument);
			write(", ");
			writeCoerced(elementType, (*args)[0].get(), CiPriority::argument);
			writeChar(')');
		}
		break;
	case CiId::listInsert:
		startMethodCall(obj);
		if (args->size() == 1) {
			write("emplace(");
			writeArrayPtrAdd(obj, (*args)[0].get());
		}
		else {
			write("insert(");
			writeArrayPtrAdd(obj, (*args)[0].get());
			write(", ");
			writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[1].get(), CiPriority::argument);
		}
		writeChar(')');
		break;
	case CiId::listLast:
		startMethodCall(obj);
		write("back()");
		break;
	case CiId::listRemoveAt:
		startMethodCall(obj);
		write("erase(");
		writeArrayPtrAdd(obj, (*args)[0].get());
		writeChar(')');
		break;
	case CiId::listRemoveRange:
		startMethodCall(obj);
		write("erase(");
		writePtrRange(obj, (*args)[0].get(), (*args)[1].get());
		writeChar(')');
		break;
	case CiId::queueClear:
	case CiId::stackClear:
		writeCollectionObject(obj, CiPriority::assign);
		write(" = {}");
		break;
	case CiId::queueDequeue:
		if (parent == CiPriority::statement) {
			startMethodCall(obj);
			write("pop()");
		}
		else {
			const CiType * elementType = obj->type->asClassType()->getElementType().get();
			write("[](");
			writeCollectionType("queue", elementType);
			write(" &q) { ");
			writeType(elementType, false);
			write(" front = q.front(); q.pop(); return front; }(");
			writeCollectionObject(obj, CiPriority::argument);
			writeChar(')');
		}
		break;
	case CiId::queueEnqueue:
		writeMethodCall(obj, "push", (*args)[0].get());
		break;
	case CiId::queuePeek:
		startMethodCall(obj);
		write("front()");
		break;
	case CiId::stackPeek:
		startMethodCall(obj);
		write("top()");
		break;
	case CiId::stackPop:
		if (parent == CiPriority::statement) {
			startMethodCall(obj);
			write("pop()");
		}
		else {
			const CiType * elementType = obj->type->asClassType()->getElementType().get();
			write("[](");
			writeCollectionType("stack", elementType);
			write(" &s) { ");
			writeType(elementType, false);
			write(" top = s.top(); s.pop(); return top; }(");
			writeCollectionObject(obj, CiPriority::argument);
			writeChar(')');
		}
		break;
	case CiId::hashSetAdd:
	case CiId::sortedSetAdd:
		writeMethodCall(obj, obj->type->asClassType()->getElementType()->id == CiId::stringStorageType && (*args)[0]->type->id == CiId::stringPtrType ? "emplace" : "insert", (*args)[0].get());
		break;
	case CiId::hashSetRemove:
	case CiId::sortedSetRemove:
	case CiId::dictionaryRemove:
	case CiId::sortedDictionaryRemove:
		writeMethodCall(obj, "erase", (*args)[0].get());
		break;
	case CiId::dictionaryAdd:
		writeIndexing(obj, (*args)[0].get());
		break;
	case CiId::dictionaryContainsKey:
	case CiId::sortedDictionaryContainsKey:
		if (parent > CiPriority::equality)
			writeChar('(');
		startMethodCall(obj);
		write("count(");
		writeStronglyCoerced(obj->type->asClassType()->getKeyType(), (*args)[0].get());
		write(") != 0");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::textWriterWrite:
		writeCollectionObject(obj, CiPriority::shift);
		writeWrite(args, false);
		break;
	case CiId::textWriterWriteChar:
		writeCollectionObject(obj, CiPriority::shift);
		write(" << ");
		{
			const CiLiteralChar * literalChar;
			if ((literalChar = dynamic_cast<const CiLiteralChar *>((*args)[0].get())) && literalChar->value < 127)
				(*args)[0]->accept(this, CiPriority::mul);
			else
				writeCall("static_cast<char>", (*args)[0].get());
			break;
		}
	case CiId::textWriterWriteCodePoint:
		{
			const CiLiteralChar * literalChar2;
			if ((literalChar2 = dynamic_cast<const CiLiteralChar *>((*args)[0].get())) && literalChar2->value < 127) {
				writeCollectionObject(obj, CiPriority::shift);
				write(" << ");
				(*args)[0]->accept(this, CiPriority::mul);
			}
			else {
				write("if (");
				(*args)[0]->accept(this, CiPriority::rel);
				writeLine(" < 0x80)");
				writeChar('\t');
				writeCollectionObject(obj, CiPriority::shift);
				write(" << ");
				writeCall("static_cast<char>", (*args)[0].get());
				writeCharLine(';');
				write("else if (");
				(*args)[0]->accept(this, CiPriority::rel);
				writeLine(" < 0x800)");
				writeChar('\t');
				writeCollectionObject(obj, CiPriority::shift);
				write(" << static_cast<char>(0xc0 | ");
				(*args)[0]->accept(this, CiPriority::shift);
				write(" >> 6) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, CiPriority::and_);
				writeLine(" & 0x3f));");
				write("else if (");
				(*args)[0]->accept(this, CiPriority::rel);
				writeLine(" < 0x10000)");
				writeChar('\t');
				writeCollectionObject(obj, CiPriority::shift);
				write(" << static_cast<char>(0xe0 | ");
				(*args)[0]->accept(this, CiPriority::shift);
				write(" >> 12) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, CiPriority::shift);
				write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, CiPriority::and_);
				writeLine(" & 0x3f));");
				writeLine("else");
				writeChar('\t');
				writeCollectionObject(obj, CiPriority::shift);
				write(" << static_cast<char>(0xf0 | ");
				(*args)[0]->accept(this, CiPriority::shift);
				write(" >> 18) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, CiPriority::shift);
				write(" >> 12 & 0x3f)) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, CiPriority::shift);
				write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
				(*args)[0]->accept(this, CiPriority::and_);
				write(" & 0x3f))");
			}
			break;
		}
	case CiId::textWriterWriteLine:
		writeCollectionObject(obj, CiPriority::shift);
		writeWrite(args, true);
		break;
	case CiId::stringWriterClear:
		include("string");
		startMethodCall(obj);
		write("str(std::string())");
		break;
	case CiId::consoleWrite:
		write("std::cout");
		writeWrite(args, false);
		break;
	case CiId::consoleWriteLine:
		write("std::cout");
		writeWrite(args, true);
		break;
	case CiId::stringWriterToString:
		startMethodCall(obj);
		write("str()");
		break;
	case CiId::uTF8GetByteCount:
		if (dynamic_cast<const CiLiteral *>((*args)[0].get())) {
			if (parent > CiPriority::add)
				writeChar('(');
			write("sizeof(");
			(*args)[0]->accept(this, CiPriority::argument);
			write(") - 1");
			if (parent > CiPriority::add)
				writeChar(')');
		}
		else
			writeStringLength((*args)[0].get());
		break;
	case CiId::uTF8GetBytes:
		if (dynamic_cast<const CiLiteral *>((*args)[0].get())) {
			include("algorithm");
			write("std::copy_n(");
			(*args)[0]->accept(this, CiPriority::argument);
			write(", sizeof(");
			(*args)[0]->accept(this, CiPriority::argument);
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
	case CiId::uTF8GetString:
		include("string_view");
		write("std::string_view(reinterpret_cast<const char *>(");
		writeArrayPtrAdd((*args)[0].get(), (*args)[1].get());
		write("), ");
		(*args)[2]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::environmentGetEnvironmentVariable:
		include("cstdlib");
		write("std::getenv(");
		writeCString((*args)[0].get());
		writeChar(')');
		break;
	case CiId::regexCompile:
		writeRegex(args, 0);
		break;
	case CiId::regexIsMatchStr:
	case CiId::regexIsMatchRegex:
	case CiId::matchFindStr:
	case CiId::matchFindRegex:
		write("std::regex_search(");
		if ((*args)[0]->type->id == CiId::stringPtrType && !dynamic_cast<const CiLiteral *>((*args)[0].get()))
			writeBeginEnd((*args)[0].get());
		else
			(*args)[0]->accept(this, CiPriority::argument);
		if (method->id == CiId::matchFindStr || method->id == CiId::matchFindRegex) {
			write(", ");
			obj->accept(this, CiPriority::argument);
		}
		write(", ");
		if (method->id == CiId::regexIsMatchRegex)
			writeRegexArgument(obj);
		else if (method->id == CiId::matchFindRegex)
			writeRegexArgument((*args)[1].get());
		else
			writeRegex(args, 1);
		writeChar(')');
		break;
	case CiId::matchGetCapture:
		startMethodCall(obj);
		writeCall("str", (*args)[0].get());
		break;
	case CiId::mathMethod:
	case CiId::mathAbs:
	case CiId::mathIsFinite:
	case CiId::mathIsNaN:
	case CiId::mathLog2:
	case CiId::mathRound:
		includeMath();
		write("std::");
		writeLowercase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathCeiling:
		includeMath();
		writeCall("std::ceil", (*args)[0].get());
		break;
	case CiId::mathClamp:
		include("algorithm");
		writeCall("std::clamp", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case CiId::mathFusedMultiplyAdd:
		includeMath();
		writeCall("std::fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case CiId::mathIsInfinity:
		includeMath();
		writeCall("std::isinf", (*args)[0].get());
		break;
	case CiId::mathMaxInt:
	case CiId::mathMaxDouble:
		include("algorithm");
		writeCall("std::max", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::mathMinInt:
	case CiId::mathMinDouble:
		include("algorithm");
		writeCall("std::min", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::mathTruncate:
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
	write("CiResource::");
	writeResourceName(name);
}

void GenCpp::writeArrayPtr(const CiExpr * expr, CiPriority parent)
{
	const CiClassType * klass;
	if (dynamic_cast<const CiArrayStorageType *>(expr->type.get()) || dynamic_cast<const CiStringType *>(expr->type.get()))
		writePostfix(expr, ".data()");
	else if (dynamic_cast<const CiDynamicPtrType *>(expr->type.get()))
		writePostfix(expr, ".get()");
	else if ((klass = dynamic_cast<const CiClassType *>(expr->type.get())) && klass->class_->id == CiId::listClass) {
		startMethodCall(expr);
		write("begin()");
	}
	else
		expr->accept(this, parent);
}

void GenCpp::writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent)
{
	const CiClassType * klass;
	if ((klass = dynamic_cast<const CiClassType *>(type)) && !dynamic_cast<const CiDynamicPtrType *>(klass) && !dynamic_cast<const CiStorageType *>(klass)) {
		if (klass->class_->id == CiId::stringClass) {
			if (expr->type->id == CiId::nullType) {
				include("string_view");
				write("std::string_view()");
			}
			else
				expr->accept(this, parent);
			return;
		}
		if (klass->class_->id == CiId::arrayPtrClass) {
			writeArrayPtr(expr, parent);
			return;
		}
		if (isSharedPtr(expr)) {
			if (klass->class_->id == CiId::regexClass) {
				writeChar('&');
				expr->accept(this, CiPriority::primary);
			}
			else
				writePostfix(expr, ".get()");
			return;
		}
		if (dynamic_cast<const CiClassType *>(expr->type.get()) && !isCppPtr(expr)) {
			writeChar('&');
			if (dynamic_cast<const CiCallExpr *>(expr)) {
				write("static_cast<");
				if (!dynamic_cast<const CiReadWriteClassType *>(klass))
					write("const ");
				writeName(klass->class_);
				write(" &>(");
				expr->accept(this, CiPriority::argument);
				writeChar(')');
			}
			else
				expr->accept(this, CiPriority::primary);
			return;
		}
	}
	GenTyped::writeCoercedInternal(type, expr, parent);
}

void GenCpp::writeSelectValues(const CiType * type, const CiSelectExpr * expr)
{
	const CiClassType * trueClass;
	const CiClassType * falseClass;
	if ((trueClass = dynamic_cast<const CiClassType *>(expr->onTrue->type.get())) && (falseClass = dynamic_cast<const CiClassType *>(expr->onFalse->type.get())) && !trueClass->class_->isSameOrBaseOf(falseClass->class_) && !falseClass->class_->isSameOrBaseOf(trueClass->class_)) {
		writeStaticCast(type, expr->onTrue.get());
		write(" : ");
		writeStaticCast(type, expr->onFalse.get());
	}
	else
		GenBase::writeSelectValues(type, expr);
}

void GenCpp::writeStringLength(const CiExpr * expr)
{
	writeNotRawStringLiteral(expr, CiPriority::primary);
	write(".length()");
}

void GenCpp::writeMatchProperty(const CiSymbolReference * expr, std::string_view name)
{
	startMethodCall(expr->left.get());
	write(name);
	write("()");
}

void GenCpp::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::consoleError:
		write("std::cerr");
		break;
	case CiId::listCount:
	case CiId::queueCount:
	case CiId::stackCount:
	case CiId::hashSetCount:
	case CiId::sortedSetCount:
	case CiId::dictionaryCount:
	case CiId::sortedDictionaryCount:
	case CiId::orderedDictionaryCount:
		expr->left->accept(this, CiPriority::primary);
		writeMemberOp(expr->left.get(), expr);
		write("size()");
		break;
	case CiId::matchStart:
		writeMatchProperty(expr, "position");
		break;
	case CiId::matchEnd:
		if (parent > CiPriority::add)
			writeChar('(');
		writeMatchProperty(expr, "position");
		write(" + ");
		writeMatchProperty(expr, "length");
		if (parent > CiPriority::add)
			writeChar(')');
		break;
	case CiId::matchLength:
		writeMatchProperty(expr, "length");
		break;
	case CiId::matchValue:
		writeMatchProperty(expr, "str");
		break;
	default:
		GenCCpp::visitSymbolReference(expr, parent);
		break;
	}
}

void GenCpp::writeGtRawPtr(const CiExpr * expr)
{
	write(">(");
	if (isSharedPtr(expr))
		writePostfix(expr, ".get()");
	else
		expr->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenCpp::writeIsVar(const CiExpr * expr, const CiVar * def, CiPriority parent)
{
	if (def->name != "_") {
		if (parent > CiPriority::assign)
			writeChar('(');
		writeName(def);
		write(" = ");
	}
	if (const CiDynamicPtrType *dynamic = dynamic_cast<const CiDynamicPtrType *>(def->type.get())) {
		write("std::dynamic_pointer_cast<");
		write(dynamic->class_->name);
		writeCall(">", expr);
	}
	else {
		write("dynamic_cast<");
		writeType(def->type.get(), true);
		writeGtRawPtr(expr);
	}
	if (def->name != "_" && parent > CiPriority::assign)
		writeChar(')');
}

void GenCpp::visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::plus:
		if (expr->type->id == CiId::stringStorageType) {
			if (parent > CiPriority::add)
				writeChar('(');
			writeStronglyCoerced(expr->type.get(), expr->left.get());
			write(" + ");
			writeStronglyCoerced(expr->type.get(), expr->right.get());
			if (parent > CiPriority::add)
				writeChar(')');
			return;
		}
		break;
	case CiToken::equal:
	case CiToken::notEqual:
	case CiToken::greater:
		{
			const CiExpr * str = isStringEmpty(expr);
			if (str != nullptr) {
				if (expr->op != CiToken::equal)
					writeChar('!');
				writePostfix(str, ".empty()");
				return;
			}
			break;
		}
	case CiToken::assign:
		{
			const CiExpr * length = isTrimSubstring(expr);
			if (length != nullptr && expr->left->type->id == CiId::stringStorageType && parent == CiPriority::statement) {
				writeMethodCall(expr->left.get(), "resize", length);
				return;
			}
			break;
		}
	case CiToken::is:
		if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr->right.get())) {
			if (parent >= CiPriority::or_ && parent <= CiPriority::mul)
				write("!!");
			write("dynamic_cast<const ");
			write(symbol->symbol->name);
			write(" *");
			writeGtRawPtr(expr->left.get());
			return;
		}
		else if (const CiVar *def = dynamic_cast<const CiVar *>(expr->right.get())) {
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

void GenCpp::visitLambdaExpr(const CiLambdaExpr * expr)
{
	write("[](const ");
	writeType(expr->first->type.get(), false);
	write(" &");
	writeName(expr->first);
	write(") { ");
	writeTemporaries(expr->body.get());
	write("return ");
	expr->body->accept(this, CiPriority::argument);
	write("; }");
}

void GenCpp::writeUnreachable(const CiAssert * statement)
{
	include("cstdlib");
	write("std::");
	GenCCpp::writeUnreachable(statement);
}

void GenCpp::writeConst(const CiConst * konst)
{
	write("static constexpr ");
	writeTypeAndName(konst);
	write(" = ");
	konst->value->accept(this, CiPriority::argument);
	writeCharLine(';');
}

void GenCpp::visitForeach(const CiForeach * statement)
{
	const CiVar * element = statement->getVar();
	write("for (");
	if (statement->count() == 2) {
		write("const auto &[");
		writeCamelCaseNotKeyword(element->name);
		write(", ");
		writeCamelCaseNotKeyword(statement->getValueVar()->name);
		writeChar(']');
	}
	else {
		if (const CiStorageType *storage = dynamic_cast<const CiStorageType *>(statement->collection->type->asClassType()->getElementType().get())) {
			if (!dynamic_cast<const CiReadWriteClassType *>(element->type.get()))
				write("const ");
			write(storage->class_->name);
			write(" &");
			writeCamelCaseNotKeyword(element->name);
		}
		else if (const CiDynamicPtrType *dynamic = dynamic_cast<const CiDynamicPtrType *>(statement->collection->type->asClassType()->getElementType().get())) {
			write("const ");
			writeType(dynamic, true);
			write(" &");
			writeCamelCaseNotKeyword(element->name);
		}
		else
			writeTypeAndName(element);
	}
	write(" : ");
	if (dynamic_cast<const CiStringType *>(statement->collection->type.get()))
		writeNotRawStringLiteral(statement->collection.get(), CiPriority::argument);
	else
		writeCollectionObject(statement->collection.get(), CiPriority::argument);
	writeChar(')');
	writeChild(statement->body.get());
}

bool GenCpp::embedIfWhileIsVar(const CiExpr * expr, bool write)
{
	const CiBinaryExpr * binary;
	const CiVar * def;
	if ((binary = dynamic_cast<const CiBinaryExpr *>(expr)) && binary->op == CiToken::is && (def = dynamic_cast<const CiVar *>(binary->right.get()))) {
		if (write)
			writeType(def->type.get(), true);
		return true;
	}
	return false;
}

void GenCpp::visitLock(const CiLock * statement)
{
	openBlock();
	write("const std::lock_guard<std::recursive_mutex> lock(");
	statement->lock->accept(this, CiPriority::argument);
	writeLine(");");
	flattenBlock(statement->body.get());
	closeBlock();
}

void GenCpp::writeStronglyCoerced(const CiType * type, const CiExpr * expr)
{
	if (type->id == CiId::stringStorageType && expr->type->id == CiId::stringPtrType && !dynamic_cast<const CiLiteral *>(expr)) {
		write("std::string(");
		expr->accept(this, CiPriority::argument);
		writeChar(')');
	}
	else {
		const CiCallExpr * call = isStringSubstring(expr);
		if (call != nullptr && type->id == CiId::stringStorageType && getStringSubstringPtr(call)->type->id != CiId::stringStorageType) {
			write("std::string(");
			bool cast = isUTF8GetString(call);
			if (cast)
				write("reinterpret_cast<const char *>(");
			writeStringPtrAdd(call);
			if (cast)
				writeChar(')');
			write(", ");
			getStringSubstringLength(call)->accept(this, CiPriority::argument);
			writeChar(')');
		}
		else
			GenBase::writeStronglyCoerced(type, expr);
	}
}

void GenCpp::writeSwitchCaseCond(const CiSwitch * statement, const CiExpr * value, CiPriority parent)
{
	if (const CiVar *def = dynamic_cast<const CiVar *>(value)) {
		if (parent == CiPriority::argument && def->name != "_")
			writeType(def->type.get(), true);
		writeIsVar(statement->value.get(), def, parent);
	}
	else
		GenBase::writeSwitchCaseCond(statement, value, parent);
}

bool GenCpp::hasTemporaries(const CiExpr * expr)
{
	if (const CiAggregateInitializer *init = dynamic_cast<const CiAggregateInitializer *>(expr))
		return std::any_of(init->items.begin(), init->items.end(), [](const std::shared_ptr<CiExpr> &item) { return hasTemporaries(item.get()); });
	else if (dynamic_cast<const CiLiteral *>(expr) || dynamic_cast<const CiLambdaExpr *>(expr))
		return false;
	else if (const CiInterpolatedString *interp = dynamic_cast<const CiInterpolatedString *>(expr))
		return std::any_of(interp->parts.begin(), interp->parts.end(), [](const CiInterpolatedPart &part) { return hasTemporaries(part.argument.get()); });
	else if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr))
		return symbol->left != nullptr && hasTemporaries(symbol->left.get());
	else if (const CiUnaryExpr *unary = dynamic_cast<const CiUnaryExpr *>(expr))
		return unary->inner != nullptr && (hasTemporaries(unary->inner.get()) || dynamic_cast<const CiAggregateInitializer *>(unary->inner.get()));
	else if (const CiBinaryExpr *binary = dynamic_cast<const CiBinaryExpr *>(expr)) {
		if (hasTemporaries(binary->left.get()))
			return true;
		if (binary->op == CiToken::is)
			return dynamic_cast<const CiVar *>(binary->right.get());
		return hasTemporaries(binary->right.get());
	}
	else if (const CiSelectExpr *select = dynamic_cast<const CiSelectExpr *>(expr))
		return hasTemporaries(select->cond.get()) || hasTemporaries(select->onTrue.get()) || hasTemporaries(select->onFalse.get());
	else if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr))
		return hasTemporaries(call->method.get()) || std::any_of(call->arguments.begin(), call->arguments.end(), [](const std::shared_ptr<CiExpr> &arg) { return hasTemporaries(arg.get()); });
	else
		std::abort();
}

bool GenCpp::isIsVar(const CiExpr * expr)
{
	const CiBinaryExpr * binary;
	return (binary = dynamic_cast<const CiBinaryExpr *>(expr)) && binary->op == CiToken::is && dynamic_cast<const CiVar *>(binary->right.get());
}

bool GenCpp::hasVariables(const CiStatement * statement) const
{
	if (dynamic_cast<const CiVar *>(statement))
		return true;
	else if (const CiAssert *asrt = dynamic_cast<const CiAssert *>(statement))
		return isIsVar(asrt->cond.get());
	else if (dynamic_cast<const CiBlock *>(statement) || dynamic_cast<const CiBreak *>(statement) || dynamic_cast<const CiConst *>(statement) || dynamic_cast<const CiContinue *>(statement) || dynamic_cast<const CiLock *>(statement) || dynamic_cast<const CiNative *>(statement) || dynamic_cast<const CiThrow *>(statement))
		return false;
	else if (const CiIf *ifStatement = dynamic_cast<const CiIf *>(statement))
		return hasTemporaries(ifStatement->cond.get()) && !isIsVar(ifStatement->cond.get());
	else if (const CiLoop *loop = dynamic_cast<const CiLoop *>(statement))
		return loop->cond != nullptr && hasTemporaries(loop->cond.get());
	else if (const CiReturn *ret = dynamic_cast<const CiReturn *>(statement))
		return ret->value != nullptr && hasTemporaries(ret->value.get());
	else if (const CiSwitch *switch_ = dynamic_cast<const CiSwitch *>(statement))
		return hasTemporaries(switch_->value.get());
	else if (const CiExpr *expr = dynamic_cast<const CiExpr *>(statement))
		return hasTemporaries(expr);
	else
		std::abort();
}

void GenCpp::writeSwitchCaseBody(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	bool block = false;
	for (const std::shared_ptr<CiStatement> &statement : *statements) {
		if (!block && hasVariables(statement.get())) {
			openBlock();
			block = true;
		}
		statement->acceptStatement(this);
	}
	if (block)
		closeBlock();
}

void GenCpp::visitSwitch(const CiSwitch * statement)
{
	if (statement->isTypeMatching())
		writeSwitchAsIfsWithGoto(statement);
	else
		GenCCpp::visitSwitch(statement);
}

void GenCpp::visitThrow(const CiThrow * statement)
{
	include("exception");
	writeLine("throw std::exception();");
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

void GenCpp::writeEnum(const CiEnum * enu)
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
	if (dynamic_cast<const CiEnumFlags *>(enu)) {
		include("type_traits");
		this->hasEnumFlags = true;
		write("CI_ENUM_FLAG_OPERATORS(");
		write(enu->name);
		writeCharLine(')');
	}
}

CiVisibility GenCpp::getConstructorVisibility(const CiClass * klass)
{
	switch (klass->callType) {
	case CiCallType::static_:
		return CiVisibility::private_;
	case CiCallType::abstract:
		return CiVisibility::protected_;
	default:
		return CiVisibility::public_;
	}
}

bool GenCpp::hasMembersOfVisibility(const CiClass * klass, CiVisibility visibility)
{
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const CiMember * member;
		if ((member = dynamic_cast<const CiMember *>(symbol)) && member->visibility == visibility)
			return true;
	}
	return false;
}

void GenCpp::writeField(const CiField * field)
{
	writeDoc(field->documentation.get());
	writeVar(field);
	writeCharLine(';');
}

void GenCpp::writeParametersAndConst(const CiMethod * method, bool defaultArguments)
{
	writeParameters(method, defaultArguments);
	if (method->callType != CiCallType::static_ && !method->isMutator)
		write(" const");
}

void GenCpp::writeDeclarations(const CiClass * klass, CiVisibility visibility, std::string_view visibilityKeyword)
{
	bool constructor = getConstructorVisibility(klass) == visibility;
	bool destructor = visibility == CiVisibility::public_ && (klass->hasSubclasses || klass->addsVirtualMethods());
	if (!constructor && !destructor && !hasMembersOfVisibility(klass, visibility))
		return;
	write(visibilityKeyword);
	writeCharLine(':');
	this->indent++;
	if (constructor) {
		if (klass->constructor != nullptr)
			writeDoc(klass->constructor->documentation.get());
		write(klass->name);
		write("()");
		if (klass->callType == CiCallType::static_)
			write(" = delete");
		else if (!needsConstructor(klass))
			write(" = default");
		writeCharLine(';');
	}
	if (destructor) {
		write("virtual ~");
		write(klass->name);
		writeLine("() = default;");
	}
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		const CiMember * member;
		if (!(member = dynamic_cast<const CiMember *>(symbol)) || member->visibility != visibility)
			continue;
		if (const CiConst *konst = dynamic_cast<const CiConst *>(member)) {
			writeDoc(konst->documentation.get());
			writeConst(konst);
		}
		else if (const CiField *field = dynamic_cast<const CiField *>(member))
			writeField(field);
		else if (const CiMethod *method = dynamic_cast<const CiMethod *>(member)) {
			writeMethodDoc(method);
			switch (method->callType) {
			case CiCallType::static_:
				write("static ");
				break;
			case CiCallType::abstract:
			case CiCallType::virtual_:
				write("virtual ");
				break;
			default:
				break;
			}
			writeTypeAndName(method);
			writeParametersAndConst(method, true);
			switch (method->callType) {
			case CiCallType::abstract:
				write(" = 0");
				break;
			case CiCallType::override_:
				write(" override");
				break;
			case CiCallType::sealed:
				write(" final");
				break;
			default:
				break;
			}
			writeCharLine(';');
		}
		else
			std::abort();
	}
	this->indent--;
}

void GenCpp::writeClassInternal(const CiClass * klass)
{
	writeNewLine();
	writeDoc(klass->documentation.get());
	openClass(klass, klass->callType == CiCallType::sealed ? " final" : "", " : public ");
	this->indent--;
	writeDeclarations(klass, CiVisibility::public_, "public");
	writeDeclarations(klass, CiVisibility::protected_, "protected");
	writeDeclarations(klass, CiVisibility::internal, "public");
	writeDeclarations(klass, CiVisibility::private_, "private");
	writeLine("};");
}

void GenCpp::writeConstructor(const CiClass * klass)
{
	if (!needsConstructor(klass))
		return;
	this->switchesWithGoto.clear();
	write(klass->name);
	write("::");
	write(klass->name);
	writeLine("()");
	openBlock();
	writeConstructorBody(klass);
	closeBlock();
}

void GenCpp::writeMethod(const CiMethod * method)
{
	if (method->callType == CiCallType::abstract)
		return;
	this->switchesWithGoto.clear();
	writeNewLine();
	writeType(method->type.get(), true);
	writeChar(' ');
	write(method->parent->name);
	write("::");
	writeCamelCaseNotKeyword(method->name);
	writeParametersAndConst(method, false);
	writeBody(method);
}

void GenCpp::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources, bool define)
{
	if (resources->size() == 0)
		return;
	writeNewLine();
	writeLine("namespace");
	openBlock();
	writeLine("namespace CiResource");
	openBlock();
	for (const auto &[name, content] : *resources) {
		if (!define)
			write("extern ");
		include("array");
		include("cstdint");
		write("const std::array<uint8_t, ");
		visitLiteralLong(content.size());
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

void GenCpp::writeProgram(const CiProgram * program)
{
	this->writtenClasses.clear();
	this->inHeaderFile = true;
	this->usingStringViewLiterals = false;
	this->hasEnumFlags = false;
	this->stringReplace = false;
	openStringWriter();
	openNamespace();
	writeRegexOptionsEnum(program);
	for (const CiSymbol * type = program->first; type != nullptr; type = type->next) {
		if (const CiEnum *enu = dynamic_cast<const CiEnum *>(type))
			writeEnum(enu);
		else {
			write("class ");
			write(type->name);
			writeCharLine(';');
		}
	}
	for (const CiClass * klass : program->classes)
		writeClass(klass, program);
	closeNamespace();
	createHeaderFile(".hpp");
	if (this->hasEnumFlags) {
		writeLine("#define CI_ENUM_FLAG_OPERATORS(T) \\");
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
	for (const CiClass * klass : program->classes) {
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
	if (this->stringReplace) {
		writeNewLine();
		writeLine("static std::string CiString_replace(std::string_view s, std::string_view oldValue, std::string_view newValue)");
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

void GenCs::writeDocPara(const CiDocPara * para, bool many)
{
	if (many) {
		writeNewLine();
		write("/// <para>");
	}
	for (const std::shared_ptr<CiDocInline> &inline_ : para->children) {
		if (const CiDocText *text = dynamic_cast<const CiDocText *>(inline_.get()))
			writeXmlDoc(text->text);
		else if (const CiDocCode *code = dynamic_cast<const CiDocCode *>(inline_.get())) {
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
		else if (dynamic_cast<const CiDocLine *>(inline_.get())) {
			writeNewLine();
			startDocLine();
		}
		else
			std::abort();
	}
	if (many)
		write("</para>");
}

void GenCs::writeDocList(const CiDocList * list)
{
	writeNewLine();
	writeLine("/// <list type=\"bullet\">");
	for (const CiDocPara &item : list->items) {
		write("/// <item>");
		writeDocPara(&item, false);
		writeLine("</item>");
	}
	write("/// </list>");
}

void GenCs::writeDoc(const CiCodeDoc * doc)
{
	if (doc == nullptr)
		return;
	write("/// <summary>");
	writeDocPara(&doc->summary, false);
	writeLine("</summary>");
	if (doc->details.size() > 0) {
		write("/// <remarks>");
		if (doc->details.size() == 1)
			writeDocBlock(doc->details[0].get(), false);
		else {
			for (const std::shared_ptr<CiDocBlock> &block : doc->details)
				writeDocBlock(block.get(), true);
		}
		writeLine("</remarks>");
	}
}

void GenCs::writeName(const CiSymbol * symbol)
{
	const CiConst * konst;
	if ((konst = dynamic_cast<const CiConst *>(symbol)) && konst->inMethod != nullptr)
		write(konst->inMethod->name);
	write(symbol->name);
	if (symbol->name == "as" || symbol->name == "await" || symbol->name == "catch" || symbol->name == "char" || symbol->name == "checked" || symbol->name == "decimal" || symbol->name == "delegate" || symbol->name == "event" || symbol->name == "explicit" || symbol->name == "extern" || symbol->name == "finally" || symbol->name == "fixed" || symbol->name == "goto" || symbol->name == "implicit" || symbol->name == "interface" || symbol->name == "is" || symbol->name == "lock" || symbol->name == "namespace" || symbol->name == "object" || symbol->name == "operator" || symbol->name == "out" || symbol->name == "params" || symbol->name == "private" || symbol->name == "readonly" || symbol->name == "ref" || symbol->name == "sbyte" || symbol->name == "sizeof" || symbol->name == "stackalloc" || symbol->name == "struct" || symbol->name == "try" || symbol->name == "typeof" || symbol->name == "ulong" || symbol->name == "unchecked" || symbol->name == "unsafe" || symbol->name == "using" || symbol->name == "volatile")
		writeChar('_');
}

int GenCs::getLiteralChars() const
{
	return 65536;
}

void GenCs::writeVisibility(CiVisibility visibility)
{
	switch (visibility) {
	case CiVisibility::private_:
		break;
	case CiVisibility::internal:
		write("internal ");
		break;
	case CiVisibility::protected_:
		write("protected ");
		break;
	case CiVisibility::public_:
		write("public ");
		break;
	default:
		std::abort();
	}
}

void GenCs::writeCallType(CiCallType callType, std::string_view sealedString)
{
	switch (callType) {
	case CiCallType::static_:
		write("static ");
		break;
	case CiCallType::normal:
		break;
	case CiCallType::abstract:
		write("abstract ");
		break;
	case CiCallType::virtual_:
		write("virtual ");
		break;
	case CiCallType::override_:
		write("override ");
		break;
	case CiCallType::sealed:
		write(sealedString);
		break;
	}
}

void GenCs::writeElementType(const CiType * elementType)
{
	include("System.Collections.Generic");
	writeChar('<');
	writeType(elementType, false);
	writeChar('>');
}

void GenCs::writeType(const CiType * type, bool promote)
{
	if (dynamic_cast<const CiIntegerType *>(type)) {
		switch (getTypeId(type, promote)) {
		case CiId::sByteRange:
			write("sbyte");
			break;
		case CiId::byteRange:
			write("byte");
			break;
		case CiId::shortRange:
			write("short");
			break;
		case CiId::uShortRange:
			write("ushort");
			break;
		case CiId::intType:
			write("int");
			break;
		case CiId::longType:
			write("long");
			break;
		default:
			std::abort();
		}
	}
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(type)) {
		switch (klass->class_->id) {
		case CiId::stringClass:
			write("string");
			break;
		case CiId::arrayPtrClass:
		case CiId::arrayStorageClass:
			writeType(klass->getElementType().get(), false);
			write("[]");
			break;
		case CiId::listClass:
		case CiId::queueClass:
		case CiId::stackClass:
		case CiId::hashSetClass:
		case CiId::sortedSetClass:
			write(klass->class_->name);
			writeElementType(klass->getElementType().get());
			break;
		case CiId::dictionaryClass:
		case CiId::sortedDictionaryClass:
			include("System.Collections.Generic");
			write(klass->class_->name);
			writeChar('<');
			writeType(klass->getKeyType(), false);
			write(", ");
			writeType(klass->getValueType().get(), false);
			writeChar('>');
			break;
		case CiId::orderedDictionaryClass:
			include("System.Collections.Specialized");
			write("OrderedDictionary");
			break;
		case CiId::textWriterClass:
		case CiId::stringWriterClass:
			include("System.IO");
			write(klass->class_->name);
			break;
		case CiId::regexClass:
		case CiId::matchClass:
			include("System.Text.RegularExpressions");
			write(klass->class_->name);
			break;
		case CiId::lockClass:
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

void GenCs::writeNewWithFields(const CiReadWriteClassType * type, const CiAggregateInitializer * init)
{
	write("new ");
	writeType(type, false);
	writeObjectLiteral(init, " = ");
}

void GenCs::writeCoercedLiteral(const CiType * type, const CiExpr * literal)
{
	const CiRangeType * range;
	if (dynamic_cast<const CiLiteralChar *>(literal) && (range = dynamic_cast<const CiRangeType *>(type)) && range->max <= 255)
		writeStaticCast(type, literal);
	else
		literal->accept(this, CiPriority::argument);
}

bool GenCs::isPromoted(const CiExpr * expr) const
{
	return GenTyped::isPromoted(expr) || dynamic_cast<const CiLiteralChar *>(expr);
}

void GenCs::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
{
	write("$\"");
	for (const CiInterpolatedPart &part : expr->parts) {
		writeDoubling(part.prefix, '{');
		writeChar('{');
		part.argument->accept(this, CiPriority::argument);
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

void GenCs::writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent)
{
	write("new ");
	writeType(elementType->getBaseType(), false);
	writeChar('[');
	lengthExpr->accept(this, CiPriority::argument);
	writeChar(']');
	const CiClassType * array;
	while ((array = dynamic_cast<const CiClassType *>(elementType)) && array->isArray()) {
		write("[]");
		elementType = array->getElementType().get();
	}
}

void GenCs::writeNew(const CiReadWriteClassType * klass, CiPriority parent)
{
	write("new ");
	writeType(klass, false);
	write("()");
}

bool GenCs::hasInitCode(const CiNamedValue * def) const
{
	const CiArrayStorageType * array;
	return (array = dynamic_cast<const CiArrayStorageType *>(def->type.get())) && dynamic_cast<const CiStorageType *>(array->getElementType().get());
}

void GenCs::writeInitCode(const CiNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	const CiArrayStorageType * array = static_cast<const CiArrayStorageType *>(def->type.get());
	int nesting = 0;
	while (const CiArrayStorageType *innerArray = dynamic_cast<const CiArrayStorageType *>(array->getElementType().get())) {
		openLoop("int", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		writeNewArray(innerArray->getElementType().get(), innerArray->lengthExpr.get(), CiPriority::argument);
		writeCharLine(';');
		array = innerArray;
	}
	if (const CiStorageType *klass = dynamic_cast<const CiStorageType *>(array->getElementType().get())) {
		openLoop("int", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		writeNew(klass, CiPriority::argument);
		writeCharLine(';');
	}
	while (--nesting >= 0)
		closeBlock();
}

void GenCs::writeResource(std::string_view name, int length)
{
	write("CiResource.");
	writeResourceName(name);
}

void GenCs::writeStringLength(const CiExpr * expr)
{
	writePostfix(expr, ".Length");
}

void GenCs::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::consoleError:
		include("System");
		write("Console.Error");
		break;
	case CiId::matchStart:
		writePostfix(expr->left.get(), ".Index");
		break;
	case CiId::matchEnd:
		if (parent > CiPriority::add)
			writeChar('(');
		writePostfix(expr->left.get(), ".Index + ");
		writeStringLength(expr->left.get());
		if (parent > CiPriority::add)
			writeChar(')');
		break;
	case CiId::mathNaN:
	case CiId::mathNegativeInfinity:
	case CiId::mathPositiveInfinity:
		write("float.");
		write(expr->symbol->name);
		break;
	default:
		{
			const CiForeach * forEach;
			const CiClassType * dict;
			if ((forEach = dynamic_cast<const CiForeach *>(expr->symbol->parent)) && (dict = dynamic_cast<const CiClassType *>(forEach->collection->type.get())) && dict->class_->id == CiId::orderedDictionaryClass) {
				if (parent == CiPriority::primary)
					writeChar('(');
				const CiVar * element = forEach->getVar();
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
				if (parent == CiPriority::primary)
					writeChar(')');
			}
			else
				GenBase::visitSymbolReference(expr, parent);
			break;
		}
	}
}

void GenCs::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case CiId::intTryParse:
	case CiId::longTryParse:
	case CiId::doubleTryParse:
		write(obj->type->name);
		write(".TryParse(");
		(*args)[0]->accept(this, CiPriority::argument);
		if (args->size() == 2) {
			const CiLiteralLong * radix;
			if (!(radix = dynamic_cast<const CiLiteralLong *>((*args)[1].get())) || radix->value != 16)
				notSupported((*args)[1].get(), "Radix");
			include("System.Globalization");
			write(", NumberStyles.HexNumber, null");
		}
		write(", out ");
		obj->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::stringIndexOf:
	case CiId::stringLastIndexOf:
		obj->accept(this, CiPriority::primary);
		writeChar('.');
		write(method->name);
		writeChar('(');
		{
			int c = getOneAscii((*args)[0].get());
			if (c >= 0)
				visitLiteralChar(c);
			else
				(*args)[0]->accept(this, CiPriority::argument);
			writeChar(')');
			break;
		}
	case CiId::arrayBinarySearchAll:
	case CiId::arrayBinarySearchPart:
		include("System");
		write("Array.BinarySearch(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		if (args->size() == 3) {
			(*args)[1]->accept(this, CiPriority::argument);
			write(", ");
			(*args)[2]->accept(this, CiPriority::argument);
			write(", ");
		}
		writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		writeChar(')');
		break;
	case CiId::arrayCopyTo:
		include("System");
		write("Array.Copy(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writeArgs(method, args);
		writeChar(')');
		break;
	case CiId::arrayFillAll:
	case CiId::arrayFillPart:
		include("System");
		{
			const CiLiteral * literal;
			if ((literal = dynamic_cast<const CiLiteral *>((*args)[0].get())) && literal->isDefaultValue()) {
				write("Array.Clear(");
				obj->accept(this, CiPriority::argument);
				if (args->size() == 1) {
					write(", 0, ");
					writeArrayStorageLength(obj);
				}
			}
			else {
				write("Array.Fill(");
				obj->accept(this, CiPriority::argument);
				write(", ");
				writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
			}
			if (args->size() == 3) {
				write(", ");
				(*args)[1]->accept(this, CiPriority::argument);
				write(", ");
				(*args)[2]->accept(this, CiPriority::argument);
			}
			writeChar(')');
			break;
		}
	case CiId::arraySortAll:
		include("System");
		writeCall("Array.Sort", obj);
		break;
	case CiId::arraySortPart:
		include("System");
		writeCall("Array.Sort", obj, (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::listAdd:
		writeListAdd(obj, "Add", args);
		break;
	case CiId::listAll:
		include("System.Linq");
		writeMethodCall(obj, "All", (*args)[0].get());
		break;
	case CiId::listAny:
		include("System.Linq");
		writeMethodCall(obj, "Any", (*args)[0].get());
		break;
	case CiId::listInsert:
		writeListInsert(obj, "Insert", args);
		break;
	case CiId::listLast:
		writePostfix(obj, "[^1]");
		break;
	case CiId::listSortPart:
		writePostfix(obj, ".Sort(");
		writeArgs(method, args);
		write(", null)");
		break;
	case CiId::dictionaryAdd:
		writePostfix(obj, ".Add(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		writeNewStorage(obj->type->asClassType()->getValueType().get());
		writeChar(')');
		break;
	case CiId::orderedDictionaryContainsKey:
		writeMethodCall(obj, "Contains", (*args)[0].get());
		break;
	case CiId::textWriterWrite:
	case CiId::textWriterWriteLine:
	case CiId::consoleWrite:
	case CiId::consoleWriteLine:
		include("System");
		obj->accept(this, CiPriority::primary);
		writeChar('.');
		write(method->name);
		writeChar('(');
		if (args->size() != 0) {
			if (dynamic_cast<const CiLiteralChar *>((*args)[0].get())) {
				write("(int) ");
				(*args)[0]->accept(this, CiPriority::primary);
			}
			else
				(*args)[0]->accept(this, CiPriority::argument);
		}
		writeChar(')');
		break;
	case CiId::stringWriterClear:
		writePostfix(obj, ".GetStringBuilder().Clear()");
		break;
	case CiId::textWriterWriteChar:
		writePostfix(obj, ".Write(");
		if (dynamic_cast<const CiLiteralChar *>((*args)[0].get()))
			(*args)[0]->accept(this, CiPriority::argument);
		else {
			write("(char) ");
			(*args)[0]->accept(this, CiPriority::primary);
		}
		writeChar(')');
		break;
	case CiId::textWriterWriteCodePoint:
		writePostfix(obj, ".Write(");
		{
			const CiLiteralChar * literalChar;
			if ((literalChar = dynamic_cast<const CiLiteralChar *>((*args)[0].get())) && literalChar->value < 65536)
				(*args)[0]->accept(this, CiPriority::argument);
			else {
				include("System.Text");
				writeCall("new Rune", (*args)[0].get());
			}
			writeChar(')');
			break;
		}
	case CiId::environmentGetEnvironmentVariable:
		include("System");
		obj->accept(this, CiPriority::primary);
		writeChar('.');
		write(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::uTF8GetByteCount:
		include("System.Text");
		write("Encoding.UTF8.GetByteCount(");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::uTF8GetBytes:
		include("System.Text");
		write("Encoding.UTF8.GetBytes(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", 0, ");
		writePostfix((*args)[0].get(), ".Length, ");
		(*args)[1]->accept(this, CiPriority::argument);
		write(", ");
		(*args)[2]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::uTF8GetString:
		include("System.Text");
		write("Encoding.UTF8.GetString");
		writeArgsInParentheses(method, args);
		break;
	case CiId::regexCompile:
		include("System.Text.RegularExpressions");
		write("new Regex");
		writeArgsInParentheses(method, args);
		break;
	case CiId::regexEscape:
	case CiId::regexIsMatchStr:
	case CiId::regexIsMatchRegex:
		include("System.Text.RegularExpressions");
		obj->accept(this, CiPriority::primary);
		writeChar('.');
		write(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::matchFindStr:
		include("System.Text.RegularExpressions");
		writeChar('(');
		obj->accept(this, CiPriority::assign);
		write(" = Regex.Match");
		writeArgsInParentheses(method, args);
		write(").Success");
		break;
	case CiId::matchFindRegex:
		include("System.Text.RegularExpressions");
		writeChar('(');
		obj->accept(this, CiPriority::assign);
		write(" = ");
		writeMethodCall((*args)[1].get(), "Match", (*args)[0].get());
		write(").Success");
		break;
	case CiId::matchGetCapture:
		writePostfix(obj, ".Groups[");
		(*args)[0]->accept(this, CiPriority::argument);
		write("].Value");
		break;
	case CiId::mathMethod:
	case CiId::mathAbs:
	case CiId::mathCeiling:
	case CiId::mathClamp:
	case CiId::mathFusedMultiplyAdd:
	case CiId::mathLog2:
	case CiId::mathMaxInt:
	case CiId::mathMaxDouble:
	case CiId::mathMinInt:
	case CiId::mathMinDouble:
	case CiId::mathRound:
	case CiId::mathTruncate:
		include("System");
		write("Math.");
		write(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathIsFinite:
	case CiId::mathIsInfinity:
	case CiId::mathIsNaN:
		write("double.");
		writeCall(method->name, (*args)[0].get());
		break;
	default:
		if (obj != nullptr) {
			obj->accept(this, CiPriority::primary);
			writeChar('.');
		}
		writeName(method);
		writeArgsInParentheses(method, args);
		break;
	}
}

void GenCs::writeOrderedDictionaryIndexing(const CiBinaryExpr * expr)
{
	if (expr->right->type->id == CiId::intType || dynamic_cast<const CiRangeType *>(expr->right->type.get())) {
		writePostfix(expr->left.get(), "[(object) ");
		expr->right->accept(this, CiPriority::primary);
		writeChar(']');
	}
	else
		GenBase::writeIndexingExpr(expr, CiPriority::and_);
}

void GenCs::writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiClassType * dict;
	if ((dict = dynamic_cast<const CiClassType *>(expr->left->type.get())) && dict->class_->id == CiId::orderedDictionaryClass) {
		if (parent == CiPriority::primary)
			writeChar('(');
		writeStaticCastType(expr->type.get());
		writeOrderedDictionaryIndexing(expr);
		if (parent == CiPriority::primary)
			writeChar(')');
	}
	else
		GenBase::writeIndexingExpr(expr, parent);
}

void GenCs::writeAssign(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiBinaryExpr * indexing;
	const CiClassType * dict;
	if ((indexing = dynamic_cast<const CiBinaryExpr *>(expr->left.get())) && indexing->op == CiToken::leftBracket && (dict = dynamic_cast<const CiClassType *>(indexing->left->type.get())) && dict->class_->id == CiId::orderedDictionaryClass) {
		writeOrderedDictionaryIndexing(indexing);
		write(" = ");
		writeAssignRight(expr);
	}
	else
		GenBase::writeAssign(expr, parent);
}

void GenCs::visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::andAssign:
	case CiToken::orAssign:
	case CiToken::xorAssign:
		if (parent > CiPriority::assign)
			writeChar('(');
		expr->left->accept(this, CiPriority::assign);
		writeChar(' ');
		write(expr->getOpString());
		writeChar(' ');
		writeAssignRight(expr);
		if (parent > CiPriority::assign)
			writeChar(')');
		break;
	default:
		GenBase::visitBinaryExpr(expr, parent);
		break;
	}
}

void GenCs::visitLambdaExpr(const CiLambdaExpr * expr)
{
	writeName(expr->first);
	write(" => ");
	expr->body->accept(this, CiPriority::statement);
}

void GenCs::defineObjectLiteralTemporary(const CiUnaryExpr * expr)
{
}

void GenCs::defineIsVar(const CiBinaryExpr * binary)
{
}

void GenCs::writeAssert(const CiAssert * statement)
{
	if (statement->completesNormally()) {
		include("System.Diagnostics");
		write("Debug.Assert(");
		statement->cond->accept(this, CiPriority::argument);
		if (statement->message != nullptr) {
			write(", ");
			statement->message->accept(this, CiPriority::argument);
		}
	}
	else {
		include("System");
		write("throw new NotImplementedException(");
		if (statement->message != nullptr)
			statement->message->accept(this, CiPriority::argument);
	}
	writeLine(");");
}

void GenCs::visitForeach(const CiForeach * statement)
{
	write("foreach (");
	const CiClassType * dict;
	if ((dict = dynamic_cast<const CiClassType *>(statement->collection->type.get())) && dict->class_->typeParameterCount == 2) {
		if (dict->class_->id == CiId::orderedDictionaryClass) {
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
	statement->collection->accept(this, CiPriority::argument);
	writeChar(')');
	writeChild(statement->body.get());
}

void GenCs::visitLock(const CiLock * statement)
{
	write("lock (");
	statement->lock->accept(this, CiPriority::argument);
	writeChar(')');
	writeChild(statement->body.get());
}

void GenCs::visitThrow(const CiThrow * statement)
{
	include("System");
	write("throw new Exception(");
	statement->message->accept(this, CiPriority::argument);
	writeLine(");");
}

void GenCs::writeEnum(const CiEnum * enu)
{
	writeNewLine();
	writeDoc(enu->documentation.get());
	if (dynamic_cast<const CiEnumFlags *>(enu)) {
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

void GenCs::writeRegexOptionsEnum(const CiProgram * program)
{
	if (program->regexOptionsEnum)
		include("System.Text.RegularExpressions");
}

void GenCs::writeConst(const CiConst * konst)
{
	writeNewLine();
	writeDoc(konst->documentation.get());
	writeVisibility(konst->visibility);
	write(dynamic_cast<const CiArrayStorageType *>(konst->type.get()) ? "static readonly " : "const ");
	writeTypeAndName(konst);
	write(" = ");
	writeCoercedExpr(konst->type.get(), konst->value.get());
	writeCharLine(';');
}

void GenCs::writeField(const CiField * field)
{
	writeNewLine();
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	if (field->type->isFinal() && !field->isAssignableStorage())
		write("readonly ");
	writeVar(field);
	writeCharLine(';');
}

void GenCs::writeParameterDoc(const CiVar * param, bool first)
{
	write("/// <param name=\"");
	writeName(param);
	write("\">");
	writeDocPara(&param->documentation->summary, false);
	writeLine("</param>");
}

void GenCs::writeMethod(const CiMethod * method)
{
	if (method->id == CiId::classToString && method->callType == CiCallType::abstract)
		return;
	writeNewLine();
	writeDoc(method->documentation.get());
	writeParametersDoc(method);
	writeVisibility(method->visibility);
	if (method->id == CiId::classToString)
		write("override ");
	else
		writeCallType(method->callType, "sealed override ");
	writeTypeAndName(method);
	writeParameters(method, true);
	if (const CiReturn *ret = dynamic_cast<const CiReturn *>(method->body.get())) {
		write(" => ");
		writeCoerced(method->type.get(), ret->value.get(), CiPriority::argument);
		writeCharLine(';');
	}
	else
		writeBody(method);
}

void GenCs::writeClass(const CiClass * klass, const CiProgram * program)
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
	writeMembers(klass, true);
	closeBlock();
}

void GenCs::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	writeNewLine();
	writeLine("internal static class CiResource");
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

void GenCs::writeProgram(const CiProgram * program)
{
	openStringWriter();
	if (!this->namespace_.empty()) {
		write("namespace ");
		writeLine(this->namespace_);
		openBlock();
	}
	writeTopLevelNatives(program);
	writeTypes(program);
	if (program->resources.size() > 0)
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

void GenD::writeDocPara(const CiDocPara * para, bool many)
{
	if (many) {
		writeNewLine();
		startDocLine();
	}
	for (const std::shared_ptr<CiDocInline> &inline_ : para->children) {
		if (const CiDocText *text = dynamic_cast<const CiDocText *>(inline_.get()))
			writeXmlDoc(text->text);
		else if (const CiDocCode *code = dynamic_cast<const CiDocCode *>(inline_.get())) {
			writeChar('`');
			writeXmlDoc(code->text);
			writeChar('`');
		}
		else if (dynamic_cast<const CiDocLine *>(inline_.get())) {
			writeNewLine();
			startDocLine();
		}
		else
			std::abort();
	}
	if (many)
		writeNewLine();
}

void GenD::writeParameterDoc(const CiVar * param, bool first)
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

void GenD::writeDocList(const CiDocList * list)
{
	writeLine("///");
	writeLine("/// <ul>");
	for (const CiDocPara &item : list->items) {
		write("/// <li>");
		writeDocPara(&item, false);
		writeLine("</li>");
	}
	writeLine("/// </ul>");
	write("///");
}

void GenD::writeDoc(const CiCodeDoc * doc)
{
	if (doc == nullptr)
		return;
	startDocLine();
	writeDocPara(&doc->summary, false);
	writeNewLine();
	if (doc->details.size() > 0) {
		startDocLine();
		if (doc->details.size() == 1)
			writeDocBlock(doc->details[0].get(), false);
		else {
			for (const std::shared_ptr<CiDocBlock> &block : doc->details)
				writeDocBlock(block.get(), true);
		}
		writeNewLine();
	}
}

void GenD::writeName(const CiSymbol * symbol)
{
	if (dynamic_cast<const CiContainerType *>(symbol)) {
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

void GenD::writeVisibility(CiVisibility visibility)
{
	switch (visibility) {
	case CiVisibility::private_:
		write("private ");
		break;
	case CiVisibility::internal:
	case CiVisibility::public_:
		break;
	case CiVisibility::protected_:
		write("protected ");
		break;
	default:
		std::abort();
	}
}

void GenD::writeCallType(CiCallType callType, std::string_view sealedString)
{
	switch (callType) {
	case CiCallType::static_:
		write("static ");
		break;
	case CiCallType::normal:
		break;
	case CiCallType::abstract:
		write("abstract ");
		break;
	case CiCallType::virtual_:
		break;
	case CiCallType::override_:
		write("override ");
		break;
	case CiCallType::sealed:
		write(sealedString);
		break;
	}
}

bool GenD::isCreateWithNew(const CiType * type)
{
	if (const CiClassType *klass = dynamic_cast<const CiClassType *>(type)) {
		if (const CiStorageType *stg = dynamic_cast<const CiStorageType *>(klass))
			return stg->class_->id != CiId::arrayStorageClass;
		return true;
	}
	return false;
}

bool GenD::isStructPtr(const CiType * type)
{
	const CiClassType * ptr;
	return (ptr = dynamic_cast<const CiClassType *>(type)) && (ptr->class_->id == CiId::listClass || ptr->class_->id == CiId::stackClass || ptr->class_->id == CiId::queueClass);
}

void GenD::writeElementType(const CiType * type)
{
	writeType(type, false);
	if (isStructPtr(type))
		writeChar('*');
}

void GenD::writeType(const CiType * type, bool promote)
{
	if (dynamic_cast<const CiIntegerType *>(type)) {
		switch (getTypeId(type, promote)) {
		case CiId::sByteRange:
			write("byte");
			break;
		case CiId::byteRange:
			write("ubyte");
			break;
		case CiId::shortRange:
			write("short");
			break;
		case CiId::uShortRange:
			write("ushort");
			break;
		case CiId::intType:
			write("int");
			break;
		case CiId::longType:
			write("long");
			break;
		default:
			std::abort();
		}
	}
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(type)) {
		switch (klass->class_->id) {
		case CiId::stringClass:
			write("string");
			break;
		case CiId::arrayStorageClass:
		case CiId::arrayPtrClass:
			writeElementType(klass->getElementType().get());
			writeChar('[');
			if (const CiArrayStorageType *arrayStorage = dynamic_cast<const CiArrayStorageType *>(klass))
				visitLiteralLong(arrayStorage->length);
			writeChar(']');
			break;
		case CiId::listClass:
		case CiId::stackClass:
			include("std.container.array");
			write("Array!(");
			writeElementType(klass->getElementType().get());
			writeChar(')');
			break;
		case CiId::queueClass:
			include("std.container.dlist");
			write("DList!(");
			writeElementType(klass->getElementType().get());
			writeChar(')');
			break;
		case CiId::hashSetClass:
			write("bool[");
			writeElementType(klass->getElementType().get());
			writeChar(']');
			break;
		case CiId::dictionaryClass:
			writeElementType(klass->getValueType().get());
			writeChar('[');
			writeType(klass->getKeyType(), false);
			writeChar(']');
			break;
		case CiId::sortedSetClass:
			include("std.container.rbtree");
			write("RedBlackTree!(");
			writeElementType(klass->getElementType().get());
			writeChar(')');
			break;
		case CiId::sortedDictionaryClass:
			include("std.container.rbtree");
			include("std.typecons");
			write("RedBlackTree!(Tuple!(");
			writeElementType(klass->getKeyType());
			write(", ");
			writeElementType(klass->getValueType().get());
			write("), \"a[0] < b[0]\")");
			break;
		case CiId::orderedDictionaryClass:
			include("std.typecons");
			write("Tuple!(Array!(");
			writeElementType(klass->getValueType().get());
			write("), \"data\", size_t[");
			writeType(klass->getKeyType(), false);
			write("], \"dict\")");
			break;
		case CiId::textWriterClass:
			include("std.stdio");
			write("File");
			break;
		case CiId::regexClass:
			include("std.regex");
			write("Regex!char");
			break;
		case CiId::matchClass:
			include("std.regex");
			write("Captures!string");
			break;
		case CiId::lockClass:
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

void GenD::writeTypeAndName(const CiNamedValue * value)
{
	writeType(value->type.get(), true);
	if (isStructPtr(value->type.get()))
		writeChar('*');
	writeChar(' ');
	writeName(value);
}

void GenD::visitAggregateInitializer(const CiAggregateInitializer * expr)
{
	write("[ ");
	writeCoercedLiterals(expr->type->asClassType()->getElementType().get(), &expr->items);
	write(" ]");
}

void GenD::writeStaticCast(const CiType * type, const CiExpr * expr)
{
	write("cast(");
	writeType(type, false);
	write(")(");
	getStaticCastInner(type, expr)->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenD::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
{
	include("std.format");
	write("format(");
	writePrintf(expr, false);
}

void GenD::writeStorageInit(const CiNamedValue * def)
{
	write(" = ");
	writeNewStorage(def->type.get());
}

void GenD::writeVarInit(const CiNamedValue * def)
{
	if (dynamic_cast<const CiArrayStorageType *>(def->type.get()))
		return;
	GenBase::writeVarInit(def);
}

bool GenD::hasInitCode(const CiNamedValue * def) const
{
	if (def->value != nullptr && !dynamic_cast<const CiLiteral *>(def->value.get()))
		return true;
	const CiType * type = def->type.get();
	if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(type)) {
		while (const CiArrayStorageType *innerArray = dynamic_cast<const CiArrayStorageType *>(array->getElementType().get()))
			array = innerArray;
		type = array->getElementType().get();
	}
	return dynamic_cast<const CiStorageType *>(type);
}

void GenD::writeInitField(const CiField * field)
{
	writeInitCode(field);
}

void GenD::writeInitCode(const CiNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(def->type.get())) {
		int nesting = 0;
		while (const CiArrayStorageType *innerArray = dynamic_cast<const CiArrayStorageType *>(array->getElementType().get())) {
			openLoop("size_t", nesting++, array->length);
			array = innerArray;
		}
		if (const CiStorageType *klass = dynamic_cast<const CiStorageType *>(array->getElementType().get())) {
			openLoop("size_t", nesting++, array->length);
			writeArrayElement(def, nesting);
			write(" = ");
			writeNew(klass, CiPriority::argument);
			writeCharLine(';');
		}
		while (--nesting >= 0)
			closeBlock();
	}
	else {
		if (const CiReadWriteClassType *klass = dynamic_cast<const CiReadWriteClassType *>(def->type.get())) {
			switch (klass->class_->id) {
			case CiId::stringClass:
			case CiId::arrayStorageClass:
			case CiId::arrayPtrClass:
			case CiId::dictionaryClass:
			case CiId::hashSetClass:
			case CiId::sortedDictionaryClass:
			case CiId::orderedDictionaryClass:
			case CiId::lockClass:
				break;
			case CiId::regexClass:
			case CiId::matchClass:
				break;
			default:
				if (dynamic_cast<const CiClass *>(def->parent)) {
					writeName(def);
					write(" = ");
					if (def->value == nullptr)
						writeNew(klass, CiPriority::argument);
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

void GenD::writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent)
{
	write("new ");
	writeType(elementType, false);
	writeChar('[');
	lengthExpr->accept(this, CiPriority::argument);
	writeChar(']');
}

void GenD::writeStaticInitializer(const CiType * type)
{
	writeChar('(');
	writeType(type, false);
	write(").init");
}

void GenD::writeNew(const CiReadWriteClassType * klass, CiPriority parent)
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
	write("CiResource.");
	writeResourceName(name);
}

void GenD::writeStringLength(const CiExpr * expr)
{
	writePostfix(expr, ".length");
}

void GenD::writeClassReference(const CiExpr * expr, CiPriority priority)
{
	if (isStructPtr(expr->type.get())) {
		write("(*");
		expr->accept(this, priority);
		writeChar(')');
	}
	else
		expr->accept(this, priority);
}

void GenD::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::consoleError:
		write("stderr");
		break;
	case CiId::listCount:
	case CiId::stackCount:
	case CiId::hashSetCount:
	case CiId::dictionaryCount:
	case CiId::sortedSetCount:
	case CiId::sortedDictionaryCount:
		writeStringLength(expr->left.get());
		break;
	case CiId::queueCount:
		include("std.range");
		writeClassReference(expr->left.get());
		write("[].walkLength");
		break;
	case CiId::matchStart:
		writePostfix(expr->left.get(), ".pre.length");
		break;
	case CiId::matchEnd:
		if (parent > CiPriority::add)
			writeChar('(');
		writePostfix(expr->left.get(), ".pre.length + ");
		writePostfix(expr->left.get(), ".hit.length");
		if (parent > CiPriority::add)
			writeChar(')');
		break;
	case CiId::matchLength:
		writePostfix(expr->left.get(), ".hit.length");
		break;
	case CiId::matchValue:
		writePostfix(expr->left.get(), ".hit");
		break;
	case CiId::mathNaN:
		write("double.nan");
		break;
	case CiId::mathNegativeInfinity:
		write("-double.infinity");
		break;
	case CiId::mathPositiveInfinity:
		write("double.infinity");
		break;
	default:
		const CiForeach * forEach;
		const CiClassType * dict;
		assert(!((forEach = dynamic_cast<const CiForeach *>(expr->symbol->parent)) && (dict = dynamic_cast<const CiClassType *>(forEach->collection->type.get())) && dict->class_->id == CiId::orderedDictionaryClass));
		GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

void GenD::writeWrite(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine)
{
	include("std.stdio");
	if (args->size() == 0)
		write("writeln()");
	else if (const CiInterpolatedString *interpolated = dynamic_cast<const CiInterpolatedString *>((*args)[0].get())) {
		write(newLine ? "writefln(" : "writef(");
		writePrintf(interpolated, false);
	}
	else {
		write(newLine ? "writeln(" : "write(");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
	}
}

void GenD::writeInsertedArg(const CiType * type, const std::vector<std::shared_ptr<CiExpr>> * args, int index)
{
	if (args->size() <= index) {
		const CiReadWriteClassType * klass = static_cast<const CiReadWriteClassType *>(type);
		writeNew(klass, CiPriority::argument);
	}
	else
		writeCoercedExpr(type, (*args)[index].get());
	writeChar(')');
}

void GenD::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::enumFromInt:
		writeStaticCast(method->type.get(), (*args)[0].get());
		break;
	case CiId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case CiId::intTryParse:
	case CiId::longTryParse:
	case CiId::doubleTryParse:
		include("std.conv");
		write("() { try { ");
		writePostfix(obj, " = ");
		writePostfix((*args)[0].get(), ".to!");
		write(obj->type->name);
		if (args->size() == 2) {
			writeChar('(');
			(*args)[1]->accept(this, CiPriority::argument);
			writeChar(')');
		}
		write("; return true; } catch (ConvException e) return false; }()");
		break;
	case CiId::stringContains:
		include("std.algorithm");
		writePostfix(obj, ".canFind");
		writeArgsInParentheses(method, args);
		break;
	case CiId::stringEndsWith:
		include("std.string");
		writeMethodCall(obj, "endsWith", (*args)[0].get());
		break;
	case CiId::stringIndexOf:
		include("std.string");
		writeMethodCall(obj, "indexOf", (*args)[0].get());
		break;
	case CiId::stringLastIndexOf:
		include("std.string");
		writeMethodCall(obj, "lastIndexOf", (*args)[0].get());
		break;
	case CiId::stringReplace:
		include("std.string");
		writeMethodCall(obj, "replace", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::stringStartsWith:
		include("std.string");
		writeMethodCall(obj, "startsWith", (*args)[0].get());
		break;
	case CiId::stringSubstring:
		obj->accept(this, CiPriority::primary);
		writeChar('[');
		writePostfix((*args)[0].get(), " .. $]");
		if (args->size() > 1) {
			write("[0 .. ");
			(*args)[1]->accept(this, CiPriority::argument);
			writeChar(']');
		}
		break;
	case CiId::arrayBinarySearchAll:
	case CiId::arrayBinarySearchPart:
		include("std.range");
		write("() { size_t cibegin = ");
		if (args->size() == 3)
			(*args)[1]->accept(this, CiPriority::argument);
		else
			writeChar('0');
		write("; auto cisearch = ");
		writeClassReference(obj);
		writeChar('[');
		if (args->size() == 3) {
			write("cibegin .. cibegin + ");
			(*args)[2]->accept(this, CiPriority::add);
		}
		write("].assumeSorted.trisect(");
		writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		write("); return cisearch[1].length ? cibegin + cisearch[0].length : -1; }()");
		break;
	case CiId::arrayCopyTo:
	case CiId::listCopyTo:
		include("std.algorithm");
		writeClassReference(obj);
		writeChar('[');
		(*args)[0]->accept(this, CiPriority::argument);
		write(" .. $][0 .. ");
		(*args)[3]->accept(this, CiPriority::argument);
		write("].copy(");
		(*args)[1]->accept(this, CiPriority::argument);
		writeChar('[');
		(*args)[2]->accept(this, CiPriority::argument);
		write(" .. $])");
		break;
	case CiId::arrayFillAll:
	case CiId::arrayFillPart:
		include("std.algorithm");
		writeClassReference(obj);
		writeChar('[');
		if (args->size() == 3) {
			(*args)[1]->accept(this, CiPriority::argument);
			write(" .. $][0 .. ");
			(*args)[2]->accept(this, CiPriority::argument);
		}
		write("].fill(");
		writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
		writeChar(')');
		break;
	case CiId::arraySortAll:
	case CiId::arraySortPart:
	case CiId::listSortAll:
	case CiId::listSortPart:
		include("std.algorithm");
		writeClassReference(obj);
		writeChar('[');
		if (args->size() == 2) {
			(*args)[0]->accept(this, CiPriority::argument);
			write(" .. $][0 .. ");
			(*args)[1]->accept(this, CiPriority::argument);
		}
		write("].sort");
		break;
	case CiId::listAdd:
	case CiId::queueEnqueue:
		writePostfix(obj, ".insertBack(");
		writeInsertedArg(obj->type->asClassType()->getElementType().get(), args);
		break;
	case CiId::listAddRange:
		writeClassReference(obj);
		write(" ~= ");
		writeClassReference((*args)[0].get());
		write("[]");
		break;
	case CiId::listAll:
		include("std.algorithm");
		writeClassReference(obj);
		write("[].all!(");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::listAny:
		include("std.algorithm");
		writeClassReference(obj);
		write("[].any!(");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::listContains:
		include("std.algorithm");
		writeClassReference(obj);
		write("[].canFind(");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::listInsert:
		this->hasListInsert = true;
		writePostfix(obj, ".insertInPlace(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		writeInsertedArg(obj->type->asClassType()->getElementType().get(), args, 1);
		break;
	case CiId::listLast:
		writePostfix(obj, ".back");
		break;
	case CiId::listRemoveAt:
	case CiId::listRemoveRange:
		this->hasListRemoveAt = true;
		writePostfix(obj, ".removeAt");
		writeArgsInParentheses(method, args);
		break;
	case CiId::listIndexOf:
		include("std.algorithm");
		writeClassReference(obj);
		write("[].countUntil");
		writeArgsInParentheses(method, args);
		break;
	case CiId::queueDequeue:
		this->hasQueueDequeue = true;
		include("std.container.dlist");
		writeClassReference(obj);
		write(".dequeue()");
		break;
	case CiId::queuePeek:
		writePostfix(obj, ".front");
		break;
	case CiId::stackPeek:
		writePostfix(obj, ".back");
		break;
	case CiId::stackPush:
		writeClassReference(obj);
		write(" ~= ");
		(*args)[0]->accept(this, CiPriority::assign);
		break;
	case CiId::stackPop:
		this->hasStackPop = true;
		writeClassReference(obj);
		write(".pop()");
		break;
	case CiId::hashSetAdd:
		writePostfix(obj, ".require(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", true)");
		break;
	case CiId::hashSetClear:
	case CiId::dictionaryClear:
		writePostfix(obj, ".clear()");
		break;
	case CiId::hashSetContains:
	case CiId::dictionaryContainsKey:
		writeChar('(');
		(*args)[0]->accept(this, CiPriority::rel);
		write(" in ");
		obj->accept(this, CiPriority::primary);
		writeChar(')');
		break;
	case CiId::sortedSetAdd:
		writePostfix(obj, ".insert(");
		writeInsertedArg(obj->type->asClassType()->getElementType().get(), args, 0);
		break;
	case CiId::sortedSetRemove:
		writePostfix(obj, ".removeKey");
		writeArgsInParentheses(method, args);
		break;
	case CiId::dictionaryAdd:
		if (obj->type->asClassType()->class_->id == CiId::sortedDictionaryClass) {
			this->hasSortedDictionaryInsert = true;
			writePostfix(obj, ".replace(");
		}
		else
			writePostfix(obj, ".require(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		writeInsertedArg(obj->type->asClassType()->getValueType().get(), args, 1);
		break;
	case CiId::sortedDictionaryContainsKey:
		write("tuple(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		writeStaticInitializer(obj->type->asClassType()->getValueType().get());
		write(") in ");
		writeClassReference(obj);
		break;
	case CiId::sortedDictionaryRemove:
		writeClassReference(obj);
		write(".removeKey(tuple(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		writeStaticInitializer(obj->type->asClassType()->getValueType().get());
		write("))");
		break;
	case CiId::textWriterWrite:
	case CiId::textWriterWriteLine:
		writePostfix(obj, ".");
		writeWrite(args, method->id == CiId::textWriterWriteLine);
		break;
	case CiId::textWriterWriteChar:
		writePostfix(obj, ".write(");
		if (dynamic_cast<const CiLiteralChar *>((*args)[0].get()))
			write("cast(char) ");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::consoleWrite:
	case CiId::consoleWriteLine:
		writeWrite(args, method->id == CiId::consoleWriteLine);
		break;
	case CiId::environmentGetEnvironmentVariable:
		include("std.process");
		write("environment.get");
		writeArgsInParentheses(method, args);
		break;
	case CiId::uTF8GetByteCount:
		writePostfix((*args)[0].get(), ".length");
		break;
	case CiId::uTF8GetBytes:
		include("std.string");
		include("std.algorithm");
		writePostfix((*args)[0].get(), ".representation.copy(");
		writePostfix((*args)[1].get(), "[");
		(*args)[2]->accept(this, CiPriority::argument);
		write(" .. $])");
		break;
	case CiId::uTF8GetString:
		write("cast(string) (");
		writePostfix((*args)[0].get(), "[");
		(*args)[1]->accept(this, CiPriority::argument);
		write(" .. $][0 .. ");
		(*args)[2]->accept(this, CiPriority::argument);
		write("])");
		break;
	case CiId::regexCompile:
		include("std.regex");
		write("regex(");
		(*args)[0]->accept(this, CiPriority::argument);
		writeRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
		writeChar(')');
		break;
	case CiId::regexEscape:
		include("std.regex");
		include("std.conv");
		(*args)[0]->accept(this, CiPriority::argument);
		write(".escaper.to!string");
		break;
	case CiId::regexIsMatchRegex:
		include("std.regex");
		writePostfix((*args)[0].get(), ".matchFirst(");
		(args->size() > 1 ? (*args)[1].get() : obj)->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::regexIsMatchStr:
		include("std.regex");
		writePostfix((*args)[0].get(), ".matchFirst(");
		if (getRegexOptions(args) != RegexOptions::none)
			write("regex(");
		(args->size() > 1 ? (*args)[1].get() : obj)->accept(this, CiPriority::argument);
		writeRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
		writeChar(')');
		break;
	case CiId::matchFindStr:
		include("std.regex");
		writeChar('(');
		obj->accept(this, CiPriority::assign);
		write(" = ");
		(*args)[0]->accept(this, CiPriority::primary);
		write(".matchFirst(");
		if (getRegexOptions(args) != RegexOptions::none)
			write("regex(");
		(*args)[1]->accept(this, CiPriority::argument);
		writeRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
		write("))");
		break;
	case CiId::matchFindRegex:
		include("std.regex");
		writeChar('(');
		obj->accept(this, CiPriority::assign);
		write(" = ");
		writeMethodCall((*args)[0].get(), "matchFirst", (*args)[1].get());
		writeChar(')');
		break;
	case CiId::matchGetCapture:
		writeIndexing(obj, (*args)[0].get());
		break;
	case CiId::mathMethod:
	case CiId::mathAbs:
	case CiId::mathIsFinite:
	case CiId::mathIsInfinity:
	case CiId::mathIsNaN:
	case CiId::mathLog2:
	case CiId::mathRound:
		include("std.math");
		writeCamelCase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathCeiling:
		include("std.math");
		writeCall("ceil", (*args)[0].get());
		break;
	case CiId::mathClamp:
	case CiId::mathMaxInt:
	case CiId::mathMaxDouble:
	case CiId::mathMinInt:
	case CiId::mathMinDouble:
		include("std.algorithm");
		writeLowercase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathFusedMultiplyAdd:
		include("std.math");
		writeCall("fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case CiId::mathTruncate:
		include("std.math");
		writeCall("trunc", (*args)[0].get());
		break;
	default:
		if (obj != nullptr) {
			if (isReferenceTo(obj, CiId::basePtr))
				write("super.");
			else {
				writeClassReference(obj);
				writeChar('.');
			}
		}
		writeName(method);
		writeArgsInParentheses(method, args);
		break;
	}
}

void GenD::writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	writeClassReference(expr->left.get());
	const CiClassType * klass = static_cast<const CiClassType *>(expr->left->type.get());
	switch (klass->class_->id) {
	case CiId::arrayPtrClass:
	case CiId::arrayStorageClass:
	case CiId::dictionaryClass:
	case CiId::listClass:
		writeChar('[');
		expr->right->accept(this, CiPriority::argument);
		writeChar(']');
		break;
	case CiId::sortedDictionaryClass:
		assert(parent != CiPriority::assign);
		this->hasSortedDictionaryFind = true;
		include("std.container.rbtree");
		include("std.typecons");
		write(".find(");
		writeStronglyCoerced(klass->getKeyType(), expr->right.get());
		writeChar(')');
		return;
	default:
		std::abort();
	}
}

bool GenD::isIsComparable(const CiExpr * expr)
{
	const CiClassType * klass;
	return dynamic_cast<const CiLiteralNull *>(expr) || ((klass = dynamic_cast<const CiClassType *>(expr->type.get())) && klass->class_->id == CiId::arrayPtrClass);
}

void GenD::writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	if (isIsComparable(left) || isIsComparable(right))
		writeEqualExpr(left, right, parent, not_ ? " !is " : " is ");
	else
		GenCCppD::writeEqual(left, right, parent, not_);
}

void GenD::writeAssign(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiBinaryExpr * indexing;
	const CiClassType * dict;
	if ((indexing = dynamic_cast<const CiBinaryExpr *>(expr->left.get())) && indexing->op == CiToken::leftBracket && (dict = dynamic_cast<const CiClassType *>(indexing->left->type.get()))) {
		switch (dict->class_->id) {
		case CiId::sortedDictionaryClass:
			this->hasSortedDictionaryInsert = true;
			writePostfix(indexing->left.get(), ".replace(");
			indexing->right->accept(this, CiPriority::argument);
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

void GenD::writeIsVar(const CiExpr * expr, const CiVar * def, CiPriority parent)
{
	CiPriority thisPriority = def->name == "_" ? CiPriority::primary : CiPriority::assign;
	if (parent > thisPriority)
		writeChar('(');
	if (def->name != "_") {
		writeName(def);
		write(" = ");
	}
	write("cast(");
	writeType(def->type.get(), true);
	write(") ");
	expr->accept(this, CiPriority::primary);
	if (parent > thisPriority)
		writeChar(')');
}

void GenD::visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::is:
		if (parent >= CiPriority::or_ && parent <= CiPriority::mul)
			parent = CiPriority::primary;
		if (parent > CiPriority::equality)
			writeChar('(');
		if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr->right.get())) {
			write("cast(");
			write(symbol->symbol->name);
			write(") ");
			expr->left->accept(this, CiPriority::primary);
		}
		else if (const CiVar *def = dynamic_cast<const CiVar *>(expr->right.get()))
			writeIsVar(expr->left.get(), def, CiPriority::equality);
		else
			std::abort();
		write(" !is null");
		if (parent > CiPriority::equality)
			writeChar(')');
		return;
	case CiToken::plus:
		if (expr->type->id == CiId::stringStorageType) {
			expr->left->accept(this, CiPriority::assign);
			write(" ~ ");
			expr->right->accept(this, CiPriority::assign);
			return;
		}
		break;
	case CiToken::addAssign:
		if (expr->left->type->id == CiId::stringStorageType) {
			expr->left->accept(this, CiPriority::assign);
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

void GenD::visitLambdaExpr(const CiLambdaExpr * expr)
{
	writeName(expr->first);
	write(" => ");
	expr->body->accept(this, CiPriority::statement);
}

void GenD::writeAssert(const CiAssert * statement)
{
	write("assert(");
	statement->cond->accept(this, CiPriority::argument);
	if (statement->message != nullptr) {
		write(", ");
		statement->message->accept(this, CiPriority::argument);
	}
	writeLine(");");
}

void GenD::visitForeach(const CiForeach * statement)
{
	write("foreach (");
	const CiClassType * dict;
	if ((dict = dynamic_cast<const CiClassType *>(statement->collection->type.get())) && dict->class_->typeParameterCount == 2) {
		writeTypeAndName(statement->getVar());
		write(", ");
		writeTypeAndName(statement->getValueVar());
	}
	else
		writeTypeAndName(statement->getVar());
	write("; ");
	writeClassReference(statement->collection.get());
	const CiClassType * set;
	if ((set = dynamic_cast<const CiClassType *>(statement->collection->type.get())) && set->class_->id == CiId::hashSetClass)
		write(".byKey");
	writeChar(')');
	writeChild(statement->body.get());
}

void GenD::visitLock(const CiLock * statement)
{
	write("synchronized (");
	statement->lock->accept(this, CiPriority::argument);
	writeChar(')');
	writeChild(statement->body.get());
}

void GenD::writeSwitchCaseTypeVar(const CiExpr * value)
{
	defineVar(value);
}

void GenD::writeSwitchCaseCond(const CiSwitch * statement, const CiExpr * value, CiPriority parent)
{
	if (const CiVar *def = dynamic_cast<const CiVar *>(value)) {
		writeIsVar(statement->value.get(), def, CiPriority::equality);
		write(" !is null");
	}
	else
		GenBase::writeSwitchCaseCond(statement, value, parent);
}

void GenD::visitSwitch(const CiSwitch * statement)
{
	writeTemporaries(statement->value.get());
	if (statement->isTypeMatching() || statement->hasWhen())
		writeSwitchAsIfsWithGoto(statement);
	else {
		startSwitch(statement);
		writeLine("default:");
		this->indent++;
		if (statement->defaultBody.size() > 0)
			writeSwitchCaseBody(&statement->defaultBody);
		else
			writeLine("assert(false);");
		this->indent--;
		writeCharLine('}');
	}
}

void GenD::visitThrow(const CiThrow * statement)
{
	include("std.exception");
	write("throw new Exception(");
	statement->message->accept(this, CiPriority::argument);
	writeLine(");");
}

void GenD::writeEnum(const CiEnum * enu)
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

void GenD::writeConst(const CiConst * konst)
{
	writeDoc(konst->documentation.get());
	write("static ");
	writeTypeAndName(konst);
	write(" = ");
	writeCoercedExpr(konst->type.get(), konst->value.get());
	writeCharLine(';');
}

void GenD::writeField(const CiField * field)
{
	writeNewLine();
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	writeTypeAndName(field);
	if (dynamic_cast<const CiLiteral *>(field->value.get())) {
		write(" = ");
		writeCoercedExpr(field->type.get(), field->value.get());
	}
	writeCharLine(';');
}

void GenD::writeMethod(const CiMethod * method)
{
	if (method->id == CiId::classToString && method->callType == CiCallType::abstract)
		return;
	writeNewLine();
	writeDoc(method->documentation.get());
	writeParametersDoc(method);
	writeVisibility(method->visibility);
	if (method->id == CiId::classToString)
		write("override ");
	else
		writeCallType(method->callType, "final override ");
	writeTypeAndName(method);
	writeParameters(method, true);
	writeBody(method);
}

void GenD::writeClass(const CiClass * klass, const CiProgram * program)
{
	writeNewLine();
	writeDoc(klass->documentation.get());
	if (klass->callType == CiCallType::sealed)
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
	for (const CiSymbol * symbol = klass->first; symbol != nullptr; symbol = symbol->next) {
		if (!dynamic_cast<const CiMember *>(symbol))
			continue;
		if (const CiConst *konst = dynamic_cast<const CiConst *>(symbol))
			writeConst(konst);
		else if (const CiField *field = dynamic_cast<const CiField *>(symbol))
			writeField(field);
		else if (const CiMethod *method = dynamic_cast<const CiMethod *>(symbol)) {
			writeMethod(method);
			this->currentTemporaries.clear();
		}
		else if (dynamic_cast<const CiVar *>(symbol)) {
		}
		else
			std::abort();
	}
	closeBlock();
}

bool GenD::isLong(const CiSymbolReference * expr)
{
	switch (expr->symbol->id) {
	case CiId::arrayLength:
	case CiId::stringLength:
	case CiId::listCount:
		return true;
	default:
		return false;
	}
}

void GenD::writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent)
{
	if (dynamic_cast<const CiRangeType *>(type))
		writeStaticCast(type, expr);
	else {
		const CiSymbolReference * symref;
		if (dynamic_cast<const CiIntegerType *>(type) && (symref = dynamic_cast<const CiSymbolReference *>(expr)) && isLong(symref))
			writeStaticCast(type, expr);
		else if (dynamic_cast<const CiFloatingType *>(type) && !dynamic_cast<const CiFloatingType *>(expr->type.get()))
			writeStaticCast(type, expr);
		else if (dynamic_cast<const CiClassType *>(type) && !dynamic_cast<const CiArrayStorageType *>(type) && dynamic_cast<const CiArrayStorageType *>(expr->type.get())) {
			GenTyped::writeCoercedInternal(type, expr, CiPriority::primary);
			write("[]");
		}
		else
			GenTyped::writeCoercedInternal(type, expr, parent);
	}
}

void GenD::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	writeNewLine();
	writeLine("private static struct CiResource");
	openBlock();
	for (const auto &[name, content] : *resources) {
		write("private static ubyte[] ");
		writeResourceName(name);
		writeLine(" = [");
		writeChar('\t');
		writeBytes(&content);
		writeLine(" ];");
	}
	closeBlock();
}

void GenD::writeProgram(const CiProgram * program)
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
	if (program->resources.size() > 0)
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

void GenJava::writePrintfWidth(const CiInterpolatedPart * part)
{
	if (part->precision >= 0 && dynamic_cast<const CiIntegerType *>(part->argument->type.get())) {
		writeChar('0');
		visitLiteralLong(part->precision);
	}
	else
		GenBase::writePrintfWidth(part);
}

void GenJava::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
{
	if (expr->suffix.empty() && expr->parts.size() == 1 && expr->parts[0].prefix.empty() && expr->parts[0].widthExpr == nullptr && expr->parts[0].format == ' ') {
		const CiExpr * arg = expr->parts[0].argument.get();
		switch (arg->type->id) {
		case CiId::longType:
			write("Long");
			break;
		case CiId::floatType:
			write("Float");
			break;
		case CiId::doubleType:
		case CiId::floatIntType:
			write("Double");
			break;
		case CiId::stringPtrType:
		case CiId::stringStorageType:
			arg->accept(this, parent);
			return;
		default:
			if (dynamic_cast<const CiIntegerType *>(arg->type.get()))
				write("Integer");
			else if (dynamic_cast<const CiClassType *>(arg->type.get())) {
				writePostfix(arg, ".toString()");
				return;
			}
			else
				std::abort();
			break;
		}
		writeCall(".toString", arg);
	}
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

void GenJava::writeName(const CiSymbol * symbol)
{
	if (dynamic_cast<const CiContainerType *>(symbol))
		write(symbol->name);
	else if (const CiConst *konst = dynamic_cast<const CiConst *>(symbol)) {
		if (konst->inMethod != nullptr) {
			writeUppercaseWithUnderscores(konst->inMethod->name);
			writeChar('_');
		}
		writeUppercaseWithUnderscores(symbol->name);
	}
	else if (dynamic_cast<const CiVar *>(symbol)) {
		const CiForeach * forEach;
		if ((forEach = dynamic_cast<const CiForeach *>(symbol->parent)) && forEach->count() == 2) {
			const CiVar * element = forEach->getVar();
			writeCamelCaseNotKeyword(element->name);
			write(symbol == element ? ".getKey()" : ".getValue()");
		}
		else
			writeCamelCaseNotKeyword(symbol->name);
	}
	else if (dynamic_cast<const CiMember *>(symbol))
		writeCamelCaseNotKeyword(symbol->name);
	else
		std::abort();
}

void GenJava::writeVisibility(CiVisibility visibility)
{
	switch (visibility) {
	case CiVisibility::private_:
		write("private ");
		break;
	case CiVisibility::internal:
		break;
	case CiVisibility::protected_:
		write("protected ");
		break;
	case CiVisibility::public_:
		write("public ");
		break;
	default:
		std::abort();
	}
}

CiId GenJava::getTypeId(const CiType * type, bool promote) const
{
	CiId id = GenBase::getTypeId(type, promote);
	switch (id) {
	case CiId::byteRange:
		return CiId::sByteRange;
	case CiId::uShortRange:
		return CiId::intType;
	default:
		return id;
	}
}

void GenJava::writeCollectionType(std::string_view name, const CiType * elementType)
{
	include("java.util." + std::string(name));
	write(name);
	writeChar('<');
	writeJavaType(elementType, false, true);
	writeChar('>');
}

void GenJava::writeDictType(std::string_view name, const CiClassType * dict)
{
	write(name);
	writeChar('<');
	writeJavaType(dict->getKeyType(), false, true);
	write(", ");
	writeJavaType(dict->getValueType().get(), false, true);
	writeChar('>');
}

void GenJava::writeJavaType(const CiType * type, bool promote, bool needClass)
{
	if (dynamic_cast<const CiNumericType *>(type)) {
		switch (getTypeId(type, promote)) {
		case CiId::sByteRange:
			write(needClass ? "Byte" : "byte");
			break;
		case CiId::shortRange:
			write(needClass ? "Short" : "short");
			break;
		case CiId::intType:
			write(needClass ? "Integer" : "int");
			break;
		case CiId::longType:
			write(needClass ? "Long" : "long");
			break;
		case CiId::floatType:
			write(needClass ? "Float" : "float");
			break;
		case CiId::doubleType:
			write(needClass ? "Double" : "double");
			break;
		default:
			std::abort();
		}
	}
	else if (const CiEnum *enu = dynamic_cast<const CiEnum *>(type))
		write(enu->id == CiId::boolType ? needClass ? "Boolean" : "boolean" : needClass ? "Integer" : "int");
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(type)) {
		switch (klass->class_->id) {
		case CiId::stringClass:
			write("String");
			break;
		case CiId::arrayPtrClass:
		case CiId::arrayStorageClass:
			writeType(klass->getElementType().get(), false);
			write("[]");
			break;
		case CiId::listClass:
			writeCollectionType("ArrayList", klass->getElementType().get());
			break;
		case CiId::queueClass:
			writeCollectionType("ArrayDeque", klass->getElementType().get());
			break;
		case CiId::stackClass:
			writeCollectionType("Stack", klass->getElementType().get());
			break;
		case CiId::hashSetClass:
			writeCollectionType("HashSet", klass->getElementType().get());
			break;
		case CiId::sortedSetClass:
			writeCollectionType("TreeSet", klass->getElementType().get());
			break;
		case CiId::dictionaryClass:
			include("java.util.HashMap");
			writeDictType("HashMap", klass);
			break;
		case CiId::sortedDictionaryClass:
			include("java.util.TreeMap");
			writeDictType("TreeMap", klass);
			break;
		case CiId::orderedDictionaryClass:
			include("java.util.LinkedHashMap");
			writeDictType("LinkedHashMap", klass);
			break;
		case CiId::regexClass:
			include("java.util.regex.Pattern");
			write("Pattern");
			break;
		case CiId::matchClass:
			include("java.util.regex.Matcher");
			write("Matcher");
			break;
		case CiId::lockClass:
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

void GenJava::writeType(const CiType * type, bool promote)
{
	writeJavaType(type, promote, false);
}

void GenJava::writeNew(const CiReadWriteClassType * klass, CiPriority parent)
{
	write("new ");
	writeType(klass, false);
	write("()");
}

void GenJava::writeResource(std::string_view name, int length)
{
	write("CiResource.getByteArray(");
	visitLiteralString(name);
	write(", ");
	visitLiteralLong(length);
	writeChar(')');
}

bool GenJava::isUnsignedByte(const CiType * type)
{
	const CiRangeType * range;
	return type->id == CiId::byteRange && (range = dynamic_cast<const CiRangeType *>(type)) && range->max > 127;
}

bool GenJava::isUnsignedByteIndexing(const CiExpr * expr)
{
	return expr->isIndexing() && isUnsignedByte(expr->type.get());
}

void GenJava::writeIndexingInternal(const CiBinaryExpr * expr)
{
	if (expr->left->type->isArray())
		GenBase::writeIndexingExpr(expr, CiPriority::and_);
	else
		writeMethodCall(expr->left.get(), "get", expr->right.get());
}

void GenJava::visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent)
{
	if ((expr->op == CiToken::increment || expr->op == CiToken::decrement) && isUnsignedByteIndexing(expr->inner.get())) {
		if (parent > CiPriority::and_)
			writeChar('(');
		write(expr->op == CiToken::increment ? "++" : "--");
		const CiBinaryExpr * indexing = static_cast<const CiBinaryExpr *>(expr->inner.get());
		writeIndexingInternal(indexing);
		if (parent != CiPriority::statement)
			write(" & 0xff");
		if (parent > CiPriority::and_)
			writeChar(')');
	}
	else
		GenBase::visitPrefixExpr(expr, parent);
}

void GenJava::visitPostfixExpr(const CiPostfixExpr * expr, CiPriority parent)
{
	if ((expr->op == CiToken::increment || expr->op == CiToken::decrement) && isUnsignedByteIndexing(expr->inner.get())) {
		if (parent > CiPriority::and_)
			writeChar('(');
		const CiBinaryExpr * indexing = static_cast<const CiBinaryExpr *>(expr->inner.get());
		writeIndexingInternal(indexing);
		write(expr->op == CiToken::increment ? "++" : "--");
		if (parent != CiPriority::statement)
			write(" & 0xff");
		if (parent > CiPriority::and_)
			writeChar(')');
	}
	else
		GenBase::visitPostfixExpr(expr, parent);
}

void GenJava::writeSByteLiteral(const CiLiteralLong * literal)
{
	visitLiteralLong((literal->value ^ 128) - 128);
}

void GenJava::writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	if ((dynamic_cast<const CiStringType *>(left->type.get()) && right->type->id != CiId::nullType) || (dynamic_cast<const CiStringType *>(right->type.get()) && left->type->id != CiId::nullType)) {
		if (not_)
			writeChar('!');
		writeMethodCall(left, "equals", right);
	}
	else {
		const CiLiteralLong * rightLiteral;
		if (isUnsignedByteIndexing(left) && (rightLiteral = dynamic_cast<const CiLiteralLong *>(right)) && rightLiteral->type->id == CiId::byteRange) {
			if (parent > CiPriority::equality)
				writeChar('(');
			const CiBinaryExpr * indexing = static_cast<const CiBinaryExpr *>(left);
			writeIndexingInternal(indexing);
			write(getEqOp(not_));
			writeSByteLiteral(rightLiteral);
			if (parent > CiPriority::equality)
				writeChar(')');
		}
		else
			GenBase::writeEqual(left, right, parent, not_);
	}
}

void GenJava::writeCoercedLiteral(const CiType * type, const CiExpr * expr)
{
	if (isUnsignedByte(type)) {
		const CiLiteralLong * literal = static_cast<const CiLiteralLong *>(expr);
		writeSByteLiteral(literal);
	}
	else
		expr->accept(this, CiPriority::argument);
}

void GenJava::writeAnd(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiLiteralLong * rightLiteral;
	if (isUnsignedByteIndexing(expr->left.get()) && (rightLiteral = dynamic_cast<const CiLiteralLong *>(expr->right.get()))) {
		if (parent > CiPriority::condAnd && parent != CiPriority::and_)
			writeChar('(');
		const CiBinaryExpr * indexing = static_cast<const CiBinaryExpr *>(expr->left.get());
		writeIndexingInternal(indexing);
		write(" & ");
		visitLiteralLong(255 & rightLiteral->value);
		if (parent > CiPriority::condAnd && parent != CiPriority::and_)
			writeChar(')');
	}
	else
		GenBase::writeAnd(expr, parent);
}

void GenJava::writeStringLength(const CiExpr * expr)
{
	writePostfix(expr, ".length()");
}

void GenJava::writeCharAt(const CiBinaryExpr * expr)
{
	writeMethodCall(expr->left.get(), "charAt", expr->right.get());
}

void GenJava::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::consoleError:
		write("System.err");
		break;
	case CiId::listCount:
	case CiId::queueCount:
	case CiId::stackCount:
	case CiId::hashSetCount:
	case CiId::sortedSetCount:
	case CiId::dictionaryCount:
	case CiId::sortedDictionaryCount:
	case CiId::orderedDictionaryCount:
		expr->left->accept(this, CiPriority::primary);
		writeMemberOp(expr->left.get(), expr);
		write("size()");
		break;
	case CiId::mathNaN:
		write("Float.NaN");
		break;
	case CiId::mathNegativeInfinity:
		write("Float.NEGATIVE_INFINITY");
		break;
	case CiId::mathPositiveInfinity:
		write("Float.POSITIVE_INFINITY");
		break;
	default:
		if (!writeJavaMatchProperty(expr, parent))
			GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

void GenJava::writeArrayBinarySearchFill(const CiExpr * obj, std::string_view method, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	include("java.util.Arrays");
	write("Arrays.");
	write(method);
	writeChar('(');
	obj->accept(this, CiPriority::argument);
	write(", ");
	if (args->size() == 3) {
		writeStartEnd((*args)[1].get(), (*args)[2].get());
		write(", ");
	}
	writeNotPromoted(obj->type->asClassType()->getElementType().get(), (*args)[0].get());
	writeChar(')');
}

void GenJava::writeWrite(const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine)
{
	const CiInterpolatedString * interpolated;
	if (args->size() == 1 && (interpolated = dynamic_cast<const CiInterpolatedString *>((*args)[0].get()))) {
		write(".format(");
		writePrintf(interpolated, newLine);
	}
	else {
		write(".print");
		if (newLine)
			write("ln");
		writeArgsInParentheses(method, args);
	}
}

void GenJava::writeCompileRegex(const std::vector<std::shared_ptr<CiExpr>> * args, int argIndex)
{
	include("java.util.regex.Pattern");
	write("Pattern.compile(");
	(*args)[argIndex]->accept(this, CiPriority::argument);
	writeRegexOptions(args, ", ", " | ", "", "Pattern.CASE_INSENSITIVE", "Pattern.MULTILINE", "Pattern.DOTALL");
	writeChar(')');
}

void GenJava::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::none:
	case CiId::classToString:
	case CiId::stringContains:
	case CiId::stringEndsWith:
	case CiId::stringIndexOf:
	case CiId::stringLastIndexOf:
	case CiId::stringReplace:
	case CiId::stringStartsWith:
	case CiId::listClear:
	case CiId::listContains:
	case CiId::listIndexOf:
	case CiId::queueClear:
	case CiId::stackClear:
	case CiId::stackPeek:
	case CiId::stackPush:
	case CiId::stackPop:
	case CiId::hashSetAdd:
	case CiId::hashSetClear:
	case CiId::hashSetContains:
	case CiId::hashSetRemove:
	case CiId::sortedSetAdd:
	case CiId::sortedSetClear:
	case CiId::sortedSetContains:
	case CiId::sortedSetRemove:
	case CiId::dictionaryClear:
	case CiId::dictionaryContainsKey:
	case CiId::dictionaryRemove:
	case CiId::sortedDictionaryClear:
	case CiId::sortedDictionaryContainsKey:
	case CiId::sortedDictionaryRemove:
	case CiId::orderedDictionaryClear:
	case CiId::orderedDictionaryContainsKey:
	case CiId::orderedDictionaryRemove:
	case CiId::stringWriterToString:
	case CiId::mathMethod:
	case CiId::mathAbs:
	case CiId::mathMaxInt:
	case CiId::mathMaxDouble:
	case CiId::mathMinInt:
	case CiId::mathMinDouble:
		if (obj != nullptr) {
			if (isReferenceTo(obj, CiId::basePtr))
				write("super");
			else
				obj->accept(this, CiPriority::primary);
			writeChar('.');
		}
		writeName(method);
		writeArgsInParentheses(method, args);
		break;
	case CiId::enumFromInt:
		(*args)[0]->accept(this, parent);
		break;
	case CiId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case CiId::doubleTryParse:
		include("java.util.function.DoubleSupplier");
		write("!Double.isNaN(");
		obj->accept(this, CiPriority::assign);
		write(" = ((DoubleSupplier) () -> { try { return Double.parseDouble(");
		(*args)[0]->accept(this, CiPriority::argument);
		write("); } catch (NumberFormatException e) { return Double.NaN; } }).getAsDouble())");
		break;
	case CiId::stringSubstring:
		writePostfix(obj, ".substring(");
		(*args)[0]->accept(this, CiPriority::argument);
		if (args->size() == 2) {
			write(", ");
			writeAdd((*args)[0].get(), (*args)[1].get());
		}
		writeChar(')');
		break;
	case CiId::arrayBinarySearchAll:
	case CiId::arrayBinarySearchPart:
		writeArrayBinarySearchFill(obj, "binarySearch", args);
		break;
	case CiId::arrayCopyTo:
		write("System.arraycopy(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writeArgs(method, args);
		writeChar(')');
		break;
	case CiId::arrayFillAll:
	case CiId::arrayFillPart:
		writeArrayBinarySearchFill(obj, "fill", args);
		break;
	case CiId::arraySortAll:
		include("java.util.Arrays");
		writeCall("Arrays.sort", obj);
		break;
	case CiId::arraySortPart:
		include("java.util.Arrays");
		write("Arrays.sort(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		writeChar(')');
		break;
	case CiId::listAdd:
		writeListAdd(obj, "add", args);
		break;
	case CiId::listAddRange:
		writeMethodCall(obj, "addAll", (*args)[0].get());
		break;
	case CiId::listAll:
		writeMethodCall(obj, "stream().allMatch", (*args)[0].get());
		break;
	case CiId::listAny:
		writeMethodCall(obj, "stream().anyMatch", (*args)[0].get());
		break;
	case CiId::listCopyTo:
		write("for (int _i = 0; _i < ");
		(*args)[3]->accept(this, CiPriority::rel);
		writeLine("; _i++)");
		write("\t");
		(*args)[1]->accept(this, CiPriority::primary);
		writeChar('[');
		startAdd((*args)[2].get());
		write("_i] = ");
		writePostfix(obj, ".get(");
		startAdd((*args)[0].get());
		write("_i)");
		break;
	case CiId::listInsert:
		writeListInsert(obj, "add", args);
		break;
	case CiId::listLast:
		writePostfix(obj, ".get(");
		writePostfix(obj, ".size() - 1)");
		break;
	case CiId::listRemoveAt:
		writeMethodCall(obj, "remove", (*args)[0].get());
		break;
	case CiId::listRemoveRange:
		writePostfix(obj, ".subList(");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		write(").clear()");
		break;
	case CiId::listSortAll:
		writePostfix(obj, ".sort(null)");
		break;
	case CiId::listSortPart:
		writePostfix(obj, ".subList(");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		write(").sort(null)");
		break;
	case CiId::queueDequeue:
		writePostfix(obj, ".remove()");
		break;
	case CiId::queueEnqueue:
		writeMethodCall(obj, "add", (*args)[0].get());
		break;
	case CiId::queuePeek:
		writePostfix(obj, ".element()");
		break;
	case CiId::dictionaryAdd:
		writePostfix(obj, ".put(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		writeNewStorage(obj->type->asClassType()->getValueType().get());
		writeChar(')');
		break;
	case CiId::textWriterWrite:
		obj->accept(this, CiPriority::primary);
		writeWrite(method, args, false);
		break;
	case CiId::textWriterWriteChar:
		writeMethodCall(obj, "write", (*args)[0].get());
		break;
	case CiId::textWriterWriteLine:
		obj->accept(this, CiPriority::primary);
		writeWrite(method, args, true);
		break;
	case CiId::stringWriterClear:
		writePostfix(obj, ".getBuffer().setLength(0)");
		break;
	case CiId::consoleWrite:
		write("System.out");
		writeWrite(method, args, false);
		break;
	case CiId::consoleWriteLine:
		write("System.out");
		writeWrite(method, args, true);
		break;
	case CiId::uTF8GetByteCount:
		include("java.nio.charset.StandardCharsets");
		writePostfix((*args)[0].get(), ".getBytes(StandardCharsets.UTF_8).length");
		break;
	case CiId::uTF8GetBytes:
		include("java.nio.ByteBuffer");
		include("java.nio.CharBuffer");
		include("java.nio.charset.StandardCharsets");
		write("StandardCharsets.UTF_8.newEncoder().encode(CharBuffer.wrap(");
		(*args)[0]->accept(this, CiPriority::argument);
		write("), ByteBuffer.wrap(");
		(*args)[1]->accept(this, CiPriority::argument);
		write(", ");
		(*args)[2]->accept(this, CiPriority::argument);
		write(", ");
		writePostfix((*args)[1].get(), ".length");
		if (!(*args)[2]->isLiteralZero()) {
			write(" - ");
			(*args)[2]->accept(this, CiPriority::mul);
		}
		write("), true)");
		break;
	case CiId::uTF8GetString:
		include("java.nio.charset.StandardCharsets");
		write("new String(");
		writeArgs(method, args);
		write(", StandardCharsets.UTF_8)");
		break;
	case CiId::environmentGetEnvironmentVariable:
		writeCall("System.getenv", (*args)[0].get());
		break;
	case CiId::regexCompile:
		writeCompileRegex(args, 0);
		break;
	case CiId::regexEscape:
		include("java.util.regex.Pattern");
		writeCall("Pattern.quote", (*args)[0].get());
		break;
	case CiId::regexIsMatchStr:
		writeCompileRegex(args, 1);
		writeCall(".matcher", (*args)[0].get());
		write(".find()");
		break;
	case CiId::regexIsMatchRegex:
		writeMethodCall(obj, "matcher", (*args)[0].get());
		write(".find()");
		break;
	case CiId::matchFindStr:
	case CiId::matchFindRegex:
		writeChar('(');
		obj->accept(this, CiPriority::assign);
		write(" = ");
		if (method->id == CiId::matchFindStr)
			writeCompileRegex(args, 1);
		else
			(*args)[1]->accept(this, CiPriority::primary);
		writeCall(".matcher", (*args)[0].get());
		write(").find()");
		break;
	case CiId::matchGetCapture:
		writeMethodCall(obj, "group", (*args)[0].get());
		break;
	case CiId::mathCeiling:
		writeCall("Math.ceil", (*args)[0].get());
		break;
	case CiId::mathClamp:
		write("Math.min(Math.max(");
		writeClampAsMinMax(args);
		break;
	case CiId::mathFusedMultiplyAdd:
		writeCall("Math.fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case CiId::mathIsFinite:
		writeCall("Double.isFinite", (*args)[0].get());
		break;
	case CiId::mathIsInfinity:
		writeCall("Double.isInfinite", (*args)[0].get());
		break;
	case CiId::mathIsNaN:
		writeCall("Double.isNaN", (*args)[0].get());
		break;
	case CiId::mathLog2:
		if (parent > CiPriority::mul)
			writeChar('(');
		writeCall("Math.log", (*args)[0].get());
		write(" * 1.4426950408889635");
		if (parent > CiPriority::mul)
			writeChar(')');
		break;
	case CiId::mathRound:
		writeCall("Math.rint", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenJava::writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	if (parent != CiPriority::assign && isUnsignedByte(expr->type.get())) {
		if (parent > CiPriority::and_)
			writeChar('(');
		writeIndexingInternal(expr);
		write(" & 0xff");
		if (parent > CiPriority::and_)
			writeChar(')');
	}
	else
		writeIndexingInternal(expr);
}

bool GenJava::isPromoted(const CiExpr * expr) const
{
	return GenTyped::isPromoted(expr) || isUnsignedByteIndexing(expr);
}

void GenJava::writeAssignRight(const CiBinaryExpr * expr)
{
	const CiBinaryExpr * rightBinary;
	if (!isUnsignedByteIndexing(expr->left.get()) && (rightBinary = dynamic_cast<const CiBinaryExpr *>(expr->right.get())) && rightBinary->isAssign() && isUnsignedByte(expr->right->type.get())) {
		writeChar('(');
		GenTyped::writeAssignRight(expr);
		write(") & 0xff");
	}
	else
		GenTyped::writeAssignRight(expr);
}

void GenJava::writeAssign(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiBinaryExpr * indexing;
	const CiClassType * klass;
	if ((indexing = dynamic_cast<const CiBinaryExpr *>(expr->left.get())) && indexing->op == CiToken::leftBracket && (klass = dynamic_cast<const CiClassType *>(indexing->left->type.get())) && !klass->isArray()) {
		writePostfix(indexing->left.get(), klass->class_->id == CiId::listClass ? ".set(" : ".put(");
		indexing->right->accept(this, CiPriority::argument);
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

void GenJava::writeVar(const CiNamedValue * def)
{
	if (def->type->isFinal() && !def->isAssignableStorage())
		write("final ");
	GenBase::writeVar(def);
}

bool GenJava::hasInitCode(const CiNamedValue * def) const
{
	return (dynamic_cast<const CiArrayStorageType *>(def->type.get()) && dynamic_cast<const CiStorageType *>(def->type->getStorageType())) || GenBase::hasInitCode(def);
}

void GenJava::writeInitCode(const CiNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(def->type.get())) {
		int nesting = 0;
		while (const CiArrayStorageType *innerArray = dynamic_cast<const CiArrayStorageType *>(array->getElementType().get())) {
			openLoop("int", nesting++, array->length);
			array = innerArray;
		}
		openLoop("int", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		const CiStorageType * storage = static_cast<const CiStorageType *>(array->getElementType().get());
		writeNew(storage, CiPriority::argument);
		writeCharLine(';');
		while (--nesting >= 0)
			closeBlock();
	}
	else
		GenBase::writeInitCode(def);
}

void GenJava::visitLambdaExpr(const CiLambdaExpr * expr)
{
	writeName(expr->first);
	write(" -> ");
	expr->body->accept(this, CiPriority::statement);
}

void GenJava::defineIsVar(const CiBinaryExpr * binary)
{
}

void GenJava::writeAssert(const CiAssert * statement)
{
	if (statement->completesNormally()) {
		write("assert ");
		statement->cond->accept(this, CiPriority::argument);
		if (statement->message != nullptr) {
			write(" : ");
			statement->message->accept(this, CiPriority::argument);
		}
	}
	else {
		write("throw new AssertionError(");
		if (statement->message != nullptr)
			statement->message->accept(this, CiPriority::argument);
		writeChar(')');
	}
	writeCharLine(';');
}

void GenJava::visitForeach(const CiForeach * statement)
{
	write("for (");
	const CiClassType * klass = static_cast<const CiClassType *>(statement->collection->type.get());
	switch (klass->class_->id) {
	case CiId::stringClass:
		write("int _i = 0; _i < ");
		writeStringLength(statement->collection.get());
		write("; _i++) ");
		openBlock();
		writeTypeAndName(statement->getVar());
		write(" = ");
		statement->collection->accept(this, CiPriority::primary);
		writeLine(".charAt(_i);");
		flattenBlock(statement->body.get());
		closeBlock();
		return;
	case CiId::dictionaryClass:
	case CiId::sortedDictionaryClass:
	case CiId::orderedDictionaryClass:
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
		statement->collection->accept(this, CiPriority::argument);
		break;
	}
	writeChar(')');
	writeChild(statement->body.get());
}

void GenJava::visitLock(const CiLock * statement)
{
	write("synchronized (");
	statement->lock->accept(this, CiPriority::argument);
	writeChar(')');
	writeChild(statement->body.get());
}

void GenJava::writeSwitchValue(const CiExpr * expr)
{
	if (isUnsignedByteIndexing(expr)) {
		const CiBinaryExpr * indexing = static_cast<const CiBinaryExpr *>(expr);
		writeIndexingInternal(indexing);
	}
	else
		GenBase::writeSwitchValue(expr);
}

bool GenJava::writeSwitchCaseVar(const CiExpr * expr)
{
	expr->accept(this, CiPriority::argument);
	const CiVar * def;
	if ((def = dynamic_cast<const CiVar *>(expr)) && def->name == "_") {
		visitLiteralLong(this->switchCaseDiscards++);
		return true;
	}
	return false;
}

void GenJava::writeSwitchCase(const CiSwitch * statement, const CiCase * kase)
{
	if (statement->isTypeMatching()) {
		for (const std::shared_ptr<CiExpr> &expr : kase->values) {
			write("case ");
			bool discard;
			if (const CiBinaryExpr *when1 = dynamic_cast<const CiBinaryExpr *>(expr.get())) {
				discard = writeSwitchCaseVar(when1->left.get());
				write(" when ");
				when1->right->accept(this, CiPriority::argument);
			}
			else
				discard = writeSwitchCaseVar(expr.get());
			writeCharLine(':');
			this->indent++;
			writeSwitchCaseBody(&kase->body);
			this->indent--;
			if (discard)
				this->switchCaseDiscards--;
		}
	}
	else
		GenBase::writeSwitchCase(statement, kase);
}

void GenJava::visitThrow(const CiThrow * statement)
{
	write("throw new Exception(");
	statement->message->accept(this, CiPriority::argument);
	writeLine(");");
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

void GenJava::visitEnumValue(const CiConst * konst, const CiConst * previous)
{
	writeDoc(konst->documentation.get());
	write("int ");
	writeUppercaseWithUnderscores(konst->name);
	write(" = ");
	if (const CiImplicitEnumValue *imp = dynamic_cast<const CiImplicitEnumValue *>(konst->value.get()))
		visitLiteralLong(imp->value);
	else
		konst->value->accept(this, CiPriority::argument);
	writeCharLine(';');
}

void GenJava::writeEnum(const CiEnum * enu)
{
	createJavaFile(enu->name);
	writeNewLine();
	writeDoc(enu->documentation.get());
	writePublic(enu);
	write("interface ");
	writeLine(enu->name);
	openBlock();
	enu->acceptValues(this);
	closeBlock();
	closeFile();
}

void GenJava::writeSignature(const CiMethod * method, int paramCount)
{
	writeNewLine();
	writeMethodDoc(method);
	writeVisibility(method->visibility);
	switch (method->callType) {
	case CiCallType::static_:
		write("static ");
		break;
	case CiCallType::virtual_:
		break;
	case CiCallType::abstract:
		write("abstract ");
		break;
	case CiCallType::override_:
		write("@Override ");
		break;
	case CiCallType::normal:
		if (method->visibility != CiVisibility::private_)
			write("final ");
		break;
	case CiCallType::sealed:
		write("final @Override ");
		break;
	default:
		std::abort();
	}
	writeTypeAndName(method);
	writeChar('(');
	const CiVar * param = method->parameters.firstParameter();
	for (int i = 0; i < paramCount; i++) {
		if (i > 0)
			write(", ");
		writeTypeAndName(param);
		param = param->nextParameter();
	}
	writeChar(')');
	if (method->throws)
		write(" throws Exception");
}

void GenJava::writeOverloads(const CiMethod * method, int paramCount)
{
	if (paramCount + 1 < method->parameters.count())
		writeOverloads(method, paramCount + 1);
	writeSignature(method, paramCount);
	writeNewLine();
	openBlock();
	if (method->type->id != CiId::voidType)
		write("return ");
	writeName(method);
	writeChar('(');
	const CiVar * param = method->parameters.firstParameter();
	for (int i = 0; i < paramCount; i++) {
		writeName(param);
		write(", ");
		param = param->nextParameter();
	}
	param->value->accept(this, CiPriority::argument);
	writeLine(");");
	closeBlock();
}

void GenJava::writeConst(const CiConst * konst)
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

void GenJava::writeField(const CiField * field)
{
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	writeVar(field);
	writeCharLine(';');
}

void GenJava::writeMethod(const CiMethod * method)
{
	writeSignature(method, method->parameters.count());
	writeBody(method);
	int i = 0;
	for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
		if (param->value != nullptr) {
			writeOverloads(method, i);
			break;
		}
		i++;
	}
}

void GenJava::writeClass(const CiClass * klass, const CiProgram * program)
{
	openStringWriter();
	writeDoc(klass->documentation.get());
	writePublic(klass);
	switch (klass->callType) {
	case CiCallType::normal:
		break;
	case CiCallType::abstract:
		write("abstract ");
		break;
	case CiCallType::static_:
	case CiCallType::sealed:
		write("final ");
		break;
	default:
		std::abort();
	}
	openClass(klass, "", " extends ");
	if (klass->callType == CiCallType::static_) {
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
	createJavaFile("CiResource");
	writeLine("import java.io.DataInputStream;");
	writeLine("import java.io.IOException;");
	writeNewLine();
	write("class CiResource");
	writeNewLine();
	openBlock();
	writeLine("static byte[] getByteArray(String name, int length)");
	openBlock();
	write("DataInputStream dis = new DataInputStream(");
	writeLine("CiResource.class.getResourceAsStream(name));");
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

void GenJava::writeProgram(const CiProgram * program)
{
	this->switchCaseDiscards = 0;
	writeTypes(program);
	if (program->resources.size() > 0)
		writeResources();
}

std::string_view GenJsNoModule::getTargetName() const
{
	return "JavaScript";
}

void GenJsNoModule::writeCamelCaseNotKeyword(std::string_view name)
{
	writeCamelCase(name);
	if (name == "Constructor" || name == "arguments" || name == "await" || name == "catch" || name == "debugger" || name == "delete" || name == "export" || name == "extends" || name == "finally" || name == "function" || name == "implements" || name == "import" || name == "instanceof" || name == "interface" || name == "let" || name == "package" || name == "private" || name == "super" || name == "try" || name == "typeof" || name == "var" || name == "with" || name == "yield")
		writeChar('_');
}

bool GenJsNoModule::isJsPrivate(const CiMember * member) const
{
	return member->visibility == CiVisibility::private_;
}

void GenJsNoModule::writeName(const CiSymbol * symbol)
{
	if (dynamic_cast<const CiContainerType *>(symbol))
		write(symbol->name);
	else if (const CiConst *konst = dynamic_cast<const CiConst *>(symbol)) {
		if (konst->inMethod != nullptr) {
			writeUppercaseWithUnderscores(konst->inMethod->name);
			writeChar('_');
		}
		writeUppercaseWithUnderscores(symbol->name);
	}
	else if (dynamic_cast<const CiVar *>(symbol))
		writeCamelCaseNotKeyword(symbol->name);
	else if (const CiMember *member = dynamic_cast<const CiMember *>(symbol)) {
		if (isJsPrivate(member)) {
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

void GenJsNoModule::writeTypeAndName(const CiNamedValue * value)
{
	writeName(value);
}

void GenJsNoModule::writeArrayElementType(const CiType * type)
{
	switch (type->id) {
	case CiId::sByteRange:
		write("Int8");
		break;
	case CiId::byteRange:
		write("Uint8");
		break;
	case CiId::shortRange:
		write("Int16");
		break;
	case CiId::uShortRange:
		write("Uint16");
		break;
	case CiId::intType:
		write("Int32");
		break;
	case CiId::longType:
		write("BigInt64");
		break;
	case CiId::floatType:
		write("Float32");
		break;
	case CiId::doubleType:
		write("Float64");
		break;
	default:
		std::abort();
	}
}

void GenJsNoModule::visitAggregateInitializer(const CiAggregateInitializer * expr)
{
	const CiArrayStorageType * array = static_cast<const CiArrayStorageType *>(expr->type.get());
	bool numeric = false;
	if (const CiNumericType *number = dynamic_cast<const CiNumericType *>(array->getElementType().get())) {
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

void GenJsNoModule::writeNew(const CiReadWriteClassType * klass, CiPriority parent)
{
	switch (klass->class_->id) {
	case CiId::listClass:
	case CiId::queueClass:
	case CiId::stackClass:
		write("[]");
		break;
	case CiId::hashSetClass:
	case CiId::sortedSetClass:
		write("new Set()");
		break;
	case CiId::dictionaryClass:
	case CiId::sortedDictionaryClass:
		write("{}");
		break;
	case CiId::orderedDictionaryClass:
		write("new Map()");
		break;
	case CiId::lockClass:
		notSupported(klass, "Lock");
		break;
	default:
		write("new ");
		if (klass->class_->id == CiId::stringWriterClass)
			this->stringWriter = true;
		write(klass->class_->name);
		write("()");
		break;
	}
}

void GenJsNoModule::writeNewWithFields(const CiReadWriteClassType * type, const CiAggregateInitializer * init)
{
	write("Object.assign(");
	writeNew(type, CiPriority::argument);
	writeChar(',');
	writeObjectLiteral(init, ": ");
	writeChar(')');
}

void GenJsNoModule::writeVar(const CiNamedValue * def)
{
	write(def->type->isFinal() && !def->isAssignableStorage() ? "const " : "let ");
	GenBase::writeVar(def);
}

void GenJsNoModule::writeInterpolatedLiteral(std::string_view s)
{
	int i = 0;
	for (int c : s) {
		i++;
		if (c == '`' || (c == '$' && i < s.length() && s[i] == '{'))
			writeChar('\\');
		writeChar(c);
	}
}

void GenJsNoModule::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
{
	writeChar('`');
	for (const CiInterpolatedPart &part : expr->parts) {
		writeInterpolatedLiteral(part.prefix);
		write("${");
		if (part.width != 0 || part.format != ' ') {
			if (dynamic_cast<const CiLiteralLong *>(part.argument.get()) || dynamic_cast<const CiPrefixExpr *>(part.argument.get())) {
				writeChar('(');
				part.argument->accept(this, CiPriority::primary);
				writeChar(')');
			}
			else
				part.argument->accept(this, CiPriority::primary);
			if (dynamic_cast<const CiNumericType *>(part.argument->type.get())) {
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
			part.argument->accept(this, CiPriority::argument);
		writeChar('}');
	}
	writeInterpolatedLiteral(expr->suffix);
	writeChar('`');
}

void GenJsNoModule::writeLocalName(const CiSymbol * symbol, CiPriority parent)
{
	if (const CiMember *member = dynamic_cast<const CiMember *>(symbol)) {
		if (!member->isStatic())
			write("this");
		else if (this->currentMethod != nullptr)
			write(this->currentMethod->parent->name);
		else if (const CiConst *konst = dynamic_cast<const CiConst *>(symbol)) {
			konst->value->accept(this, parent);
			return;
		}
		else
			std::abort();
		writeChar('.');
	}
	writeName(symbol);
	const CiForeach * forEach;
	if ((forEach = dynamic_cast<const CiForeach *>(symbol->parent)) && dynamic_cast<const CiStringType *>(forEach->collection->type.get()))
		write(".codePointAt(0)");
}

void GenJsNoModule::writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent)
{
	if (dynamic_cast<const CiNumericType *>(type)) {
		if (type->id == CiId::longType) {
			if (dynamic_cast<const CiLiteralLong *>(expr)) {
				expr->accept(this, CiPriority::primary);
				writeChar('n');
				return;
			}
			if (expr->type->id != CiId::longType) {
				writeCall("BigInt", expr);
				return;
			}
		}
		else if (expr->type->id == CiId::longType) {
			writeCall("Number", expr);
			return;
		}
	}
	expr->accept(this, parent);
}

void GenJsNoModule::writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent)
{
	write("new ");
	if (dynamic_cast<const CiNumericType *>(elementType))
		writeArrayElementType(elementType);
	writeCall("Array", lengthExpr);
}

bool GenJsNoModule::hasInitCode(const CiNamedValue * def) const
{
	const CiArrayStorageType * array;
	return (array = dynamic_cast<const CiArrayStorageType *>(def->type.get())) && dynamic_cast<const CiStorageType *>(array->getElementType().get());
}

void GenJsNoModule::writeInitCode(const CiNamedValue * def)
{
	if (!hasInitCode(def))
		return;
	const CiArrayStorageType * array = static_cast<const CiArrayStorageType *>(def->type.get());
	int nesting = 0;
	while (const CiArrayStorageType *innerArray = dynamic_cast<const CiArrayStorageType *>(array->getElementType().get())) {
		openLoop("let", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		writeNewArray(innerArray->getElementType().get(), innerArray->lengthExpr.get(), CiPriority::argument);
		writeCharLine(';');
		array = innerArray;
	}
	if (const CiStorageType *klass = dynamic_cast<const CiStorageType *>(array->getElementType().get())) {
		openLoop("let", nesting++, array->length);
		writeArrayElement(def, nesting);
		write(" = ");
		writeNew(klass, CiPriority::argument);
		writeCharLine(';');
	}
	while (--nesting >= 0)
		closeBlock();
}

void GenJsNoModule::writeResource(std::string_view name, int length)
{
	write("Ci.");
	writeResourceName(name);
}

void GenJsNoModule::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::consoleError:
		write("process.stderr");
		break;
	case CiId::listCount:
	case CiId::queueCount:
	case CiId::stackCount:
		writePostfix(expr->left.get(), ".length");
		break;
	case CiId::hashSetCount:
	case CiId::sortedSetCount:
	case CiId::orderedDictionaryCount:
		writePostfix(expr->left.get(), ".size");
		break;
	case CiId::dictionaryCount:
	case CiId::sortedDictionaryCount:
		writeCall("Object.keys", expr->left.get());
		write(".length");
		break;
	case CiId::matchStart:
		writePostfix(expr->left.get(), ".index");
		break;
	case CiId::matchEnd:
		if (parent > CiPriority::add)
			writeChar('(');
		writePostfix(expr->left.get(), ".index + ");
		writePostfix(expr->left.get(), "[0].length");
		if (parent > CiPriority::add)
			writeChar(')');
		break;
	case CiId::matchLength:
		writePostfix(expr->left.get(), "[0].length");
		break;
	case CiId::matchValue:
		writePostfix(expr->left.get(), "[0]");
		break;
	case CiId::mathNaN:
		write("NaN");
		break;
	case CiId::mathNegativeInfinity:
		write("-Infinity");
		break;
	case CiId::mathPositiveInfinity:
		write("Infinity");
		break;
	default:
		GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

void GenJsNoModule::writeStringLength(const CiExpr * expr)
{
	writePostfix(expr, ".length");
}

void GenJsNoModule::writeCharAt(const CiBinaryExpr * expr)
{
	writeMethodCall(expr->left.get(), "charCodeAt", expr->right.get());
}

void GenJsNoModule::writeBinaryOperand(const CiExpr * expr, CiPriority parent, const CiBinaryExpr * binary)
{
	writeCoerced(binary->type.get(), expr, parent);
}

bool GenJsNoModule::isIdentifier(std::string_view s)
{
	if (s.empty() || s[0] < 'A')
		return false;
	for (int c : s) {
		if (!CiLexer::isLetterOrDigit(c))
			return false;
	}
	return true;
}

void GenJsNoModule::writeNewRegex(const std::vector<std::shared_ptr<CiExpr>> * args, int argIndex)
{
	const CiExpr * pattern = (*args)[argIndex].get();
	if (const CiLiteralString *literal = dynamic_cast<const CiLiteralString *>(pattern)) {
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
		pattern->accept(this, CiPriority::argument);
		writeRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
		writeChar(')');
	}
}

bool GenJsNoModule::hasLong(const std::vector<std::shared_ptr<CiExpr>> * args)
{
	return std::any_of(args->begin(), args->end(), [](const std::shared_ptr<CiExpr> &arg) { return arg->type->id == CiId::longType; });
}

void GenJsNoModule::writeMathMaxMin(const CiMethod * method, std::string_view name, int op, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	if (hasLong(args)) {
		write("((x, y) => x ");
		writeChar(op);
		write(" y ? x : y)");
		writeArgsInParentheses(method, args);
	}
	else
		writeCall(name, (*args)[0].get(), (*args)[1].get());
}

void GenJsNoModule::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::none:
	case CiId::classToString:
	case CiId::stringEndsWith:
	case CiId::stringIndexOf:
	case CiId::stringLastIndexOf:
	case CiId::stringStartsWith:
	case CiId::arraySortAll:
	case CiId::listIndexOf:
	case CiId::stackPush:
	case CiId::stackPop:
	case CiId::hashSetAdd:
	case CiId::hashSetClear:
	case CiId::sortedSetAdd:
	case CiId::sortedSetClear:
	case CiId::orderedDictionaryClear:
	case CiId::stringWriterClear:
	case CiId::stringWriterToString:
	case CiId::mathMethod:
	case CiId::mathLog2:
	case CiId::mathMaxDouble:
	case CiId::mathMinDouble:
	case CiId::mathRound:
		if (obj == nullptr)
			writeLocalName(method, CiPriority::primary);
		else {
			if (isReferenceTo(obj, CiId::basePtr))
				write("super");
			else
				obj->accept(this, CiPriority::primary);
			writeChar('.');
			writeName(method);
		}
		writeArgsInParentheses(method, args);
		break;
	case CiId::enumFromInt:
		(*args)[0]->accept(this, parent);
		break;
	case CiId::enumHasFlag:
		writeEnumHasFlag(obj, args, parent);
		break;
	case CiId::intTryParse:
		write("!isNaN(");
		obj->accept(this, CiPriority::assign);
		write(" = parseInt(");
		(*args)[0]->accept(this, CiPriority::argument);
		writeTryParseRadix(args);
		write("))");
		break;
	case CiId::longTryParse:
		if (args->size() != 1)
			notSupported((*args)[1].get(), "Radix");
		write("(() => { try { ");
		obj->accept(this, CiPriority::assign);
		write("  = BigInt(");
		(*args)[0]->accept(this, CiPriority::argument);
		write("); return true; } catch { return false; }})()");
		break;
	case CiId::doubleTryParse:
		write("!isNaN(");
		obj->accept(this, CiPriority::assign);
		write(" = parseFloat(");
		(*args)[0]->accept(this, CiPriority::argument);
		write("))");
		break;
	case CiId::stringContains:
	case CiId::listContains:
		writeMethodCall(obj, "includes", (*args)[0].get());
		break;
	case CiId::stringReplace:
		writeMethodCall(obj, "replaceAll", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::stringSubstring:
		writePostfix(obj, ".substring(");
		(*args)[0]->accept(this, CiPriority::argument);
		if (args->size() == 2) {
			write(", ");
			writeAdd((*args)[0].get(), (*args)[1].get());
		}
		writeChar(')');
		break;
	case CiId::arrayFillAll:
	case CiId::arrayFillPart:
		writePostfix(obj, ".fill(");
		(*args)[0]->accept(this, CiPriority::argument);
		if (args->size() == 3) {
			write(", ");
			writeStartEnd((*args)[1].get(), (*args)[2].get());
		}
		writeChar(')');
		break;
	case CiId::arrayCopyTo:
	case CiId::listCopyTo:
		(*args)[1]->accept(this, CiPriority::primary);
		{
			const CiArrayStorageType * sourceStorage;
			const CiLiteralLong * literalLength;
			bool wholeSource = (sourceStorage = dynamic_cast<const CiArrayStorageType *>(obj->type.get())) && (*args)[0]->isLiteralZero() && (literalLength = dynamic_cast<const CiLiteralLong *>((*args)[3].get())) && literalLength->value == sourceStorage->length;
			if (dynamic_cast<const CiNumericType *>(obj->type->asClassType()->getElementType().get())) {
				write(".set(");
				if (wholeSource)
					obj->accept(this, CiPriority::argument);
				else {
					writePostfix(obj, method->id == CiId::arrayCopyTo ? ".subarray(" : ".slice(");
					writeStartEnd((*args)[0].get(), (*args)[3].get());
					writeChar(')');
				}
				if (!(*args)[2]->isLiteralZero()) {
					write(", ");
					(*args)[2]->accept(this, CiPriority::argument);
				}
			}
			else {
				write(".splice(");
				(*args)[2]->accept(this, CiPriority::argument);
				write(", ");
				(*args)[3]->accept(this, CiPriority::argument);
				write(", ...");
				obj->accept(this, CiPriority::primary);
				if (!wholeSource) {
					write(".slice(");
					writeStartEnd((*args)[0].get(), (*args)[3].get());
					writeChar(')');
				}
			}
			writeChar(')');
			break;
		}
	case CiId::arraySortPart:
		writePostfix(obj, ".subarray(");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		write(").sort()");
		break;
	case CiId::listAdd:
		writeListAdd(obj, "push", args);
		break;
	case CiId::listAddRange:
		writePostfix(obj, ".push(...");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::listAll:
		writeMethodCall(obj, "every", (*args)[0].get());
		break;
	case CiId::listAny:
		writeMethodCall(obj, "some", (*args)[0].get());
		break;
	case CiId::listClear:
	case CiId::queueClear:
	case CiId::stackClear:
		writePostfix(obj, ".length = 0");
		break;
	case CiId::listInsert:
		writeListInsert(obj, "splice", args, ", 0, ");
		break;
	case CiId::listLast:
	case CiId::stackPeek:
		writePostfix(obj, ".at(-1)");
		break;
	case CiId::listRemoveAt:
		writePostfix(obj, ".splice(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", 1)");
		break;
	case CiId::listRemoveRange:
		writeMethodCall(obj, "splice", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::listSortAll:
		writePostfix(obj, ".sort((a, b) => a - b)");
		break;
	case CiId::listSortPart:
		writePostfix(obj, ".splice(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		(*args)[1]->accept(this, CiPriority::argument);
		write(", ...");
		writePostfix(obj, ".slice(");
		writeStartEnd((*args)[0].get(), (*args)[1].get());
		write(").sort((a, b) => a - b))");
		break;
	case CiId::queueDequeue:
		writePostfix(obj, ".shift()");
		break;
	case CiId::queueEnqueue:
		writeMethodCall(obj, "push", (*args)[0].get());
		break;
	case CiId::queuePeek:
		writePostfix(obj, "[0]");
		break;
	case CiId::hashSetContains:
	case CiId::sortedSetContains:
	case CiId::orderedDictionaryContainsKey:
		writeMethodCall(obj, "has", (*args)[0].get());
		break;
	case CiId::hashSetRemove:
	case CiId::sortedSetRemove:
	case CiId::orderedDictionaryRemove:
		writeMethodCall(obj, "delete", (*args)[0].get());
		break;
	case CiId::dictionaryAdd:
		writeDictionaryAdd(obj, args);
		break;
	case CiId::dictionaryClear:
	case CiId::sortedDictionaryClear:
		write("for (const key in ");
		obj->accept(this, CiPriority::argument);
		writeCharLine(')');
		write("\tdelete ");
		writePostfix(obj, "[key];");
		break;
	case CiId::dictionaryContainsKey:
	case CiId::sortedDictionaryContainsKey:
		writeMethodCall(obj, "hasOwnProperty", (*args)[0].get());
		break;
	case CiId::dictionaryRemove:
	case CiId::sortedDictionaryRemove:
		write("delete ");
		writeIndexing(obj, (*args)[0].get());
		break;
	case CiId::textWriterWrite:
		writePostfix(obj, ".write(");
		if (dynamic_cast<const CiStringType *>((*args)[0]->type.get()))
			(*args)[0]->accept(this, CiPriority::argument);
		else
			writeCall("String", (*args)[0].get());
		writeChar(')');
		break;
	case CiId::textWriterWriteChar:
		writeMethodCall(obj, "write(String.fromCharCode", (*args)[0].get());
		writeChar(')');
		break;
	case CiId::textWriterWriteCodePoint:
		writeMethodCall(obj, "write(String.fromCodePoint", (*args)[0].get());
		writeChar(')');
		break;
	case CiId::textWriterWriteLine:
		if (isReferenceTo(obj, CiId::consoleError)) {
			write("console.error(");
			if (args->size() == 0)
				write("\"\"");
			else
				(*args)[0]->accept(this, CiPriority::argument);
			writeChar(')');
		}
		else {
			writePostfix(obj, ".write(");
			if (args->size() != 0) {
				(*args)[0]->accept(this, CiPriority::add);
				write(" + ");
			}
			write("\"\\n\")");
		}
		break;
	case CiId::consoleWrite:
		write("process.stdout.write(");
		if (dynamic_cast<const CiStringType *>((*args)[0]->type.get()))
			(*args)[0]->accept(this, CiPriority::argument);
		else
			writeCall("String", (*args)[0].get());
		writeChar(')');
		break;
	case CiId::consoleWriteLine:
		write("console.log(");
		if (args->size() == 0)
			write("\"\"");
		else
			(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::uTF8GetByteCount:
		write("new TextEncoder().encode(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(").length");
		break;
	case CiId::uTF8GetBytes:
		write("new TextEncoder().encodeInto(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		if ((*args)[2]->isLiteralZero())
			(*args)[1]->accept(this, CiPriority::argument);
		else
			writeMethodCall((*args)[1].get(), "subarray", (*args)[2].get());
		writeChar(')');
		break;
	case CiId::uTF8GetString:
		write("new TextDecoder().decode(");
		writePostfix((*args)[0].get(), ".subarray(");
		(*args)[1]->accept(this, CiPriority::argument);
		write(", ");
		writeAdd((*args)[1].get(), (*args)[2].get());
		write("))");
		break;
	case CiId::environmentGetEnvironmentVariable:
		{
			const CiLiteralString * literal;
			if ((literal = dynamic_cast<const CiLiteralString *>((*args)[0].get())) && isIdentifier(literal->value)) {
				write("process.env.");
				write(literal->value);
			}
			else {
				write("process.env[");
				(*args)[0]->accept(this, CiPriority::argument);
				writeChar(']');
			}
			break;
		}
	case CiId::regexCompile:
		writeNewRegex(args, 0);
		break;
	case CiId::regexEscape:
		writePostfix((*args)[0].get(), ".replace(/[-\\/\\\\^$*+?.()|[\\]{}]/g, '\\\\$&')");
		break;
	case CiId::regexIsMatchStr:
		writeNewRegex(args, 1);
		writeCall(".test", (*args)[0].get());
		break;
	case CiId::regexIsMatchRegex:
		writeMethodCall(obj, "test", (*args)[0].get());
		break;
	case CiId::matchFindStr:
	case CiId::matchFindRegex:
		if (parent > CiPriority::equality)
			writeChar('(');
		writeChar('(');
		obj->accept(this, CiPriority::assign);
		write(" = ");
		if (method->id == CiId::matchFindStr)
			writeNewRegex(args, 1);
		else
			(*args)[1]->accept(this, CiPriority::primary);
		writeCall(".exec", (*args)[0].get());
		write(") != null");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::matchGetCapture:
		writeIndexing(obj, (*args)[0].get());
		break;
	case CiId::mathAbs:
		writeCall((*args)[0]->type->id == CiId::longType ? "(x => x < 0n ? -x : x)" : "Math.abs", (*args)[0].get());
		break;
	case CiId::mathCeiling:
		writeCall("Math.ceil", (*args)[0].get());
		break;
	case CiId::mathClamp:
		if (method->type->id == CiId::intType && hasLong(args)) {
			write("((x, min, max) => x < min ? min : x > max ? max : x)");
			writeArgsInParentheses(method, args);
		}
		else {
			write("Math.min(Math.max(");
			writeClampAsMinMax(args);
		}
		break;
	case CiId::mathFusedMultiplyAdd:
		if (parent > CiPriority::add)
			writeChar('(');
		(*args)[0]->accept(this, CiPriority::mul);
		write(" * ");
		(*args)[1]->accept(this, CiPriority::mul);
		write(" + ");
		(*args)[2]->accept(this, CiPriority::add);
		if (parent > CiPriority::add)
			writeChar(')');
		break;
	case CiId::mathIsFinite:
	case CiId::mathIsNaN:
		writeCamelCase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathIsInfinity:
		if (parent > CiPriority::equality)
			writeChar('(');
		writeCall("Math.abs", (*args)[0].get());
		write(" == Infinity");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::mathMaxInt:
		writeMathMaxMin(method, "Math.max", '>', args);
		break;
	case CiId::mathMinInt:
		writeMathMaxMin(method, "Math.min", '<', args);
		break;
	case CiId::mathTruncate:
		writeCall("Math.trunc", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenJsNoModule::writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiClassType * dict;
	if ((dict = dynamic_cast<const CiClassType *>(expr->left->type.get())) && dict->class_->id == CiId::orderedDictionaryClass)
		writeMethodCall(expr->left.get(), "get", expr->right.get());
	else
		GenBase::writeIndexingExpr(expr, parent);
}

void GenJsNoModule::writeAssign(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiBinaryExpr * indexing;
	const CiClassType * dict;
	if ((indexing = dynamic_cast<const CiBinaryExpr *>(expr->left.get())) && indexing->op == CiToken::leftBracket && (dict = dynamic_cast<const CiClassType *>(indexing->left->type.get())) && dict->class_->id == CiId::orderedDictionaryClass)
		writeMethodCall(indexing->left.get(), "set", indexing->right.get(), expr->right.get());
	else
		GenBase::writeAssign(expr, parent);
}

std::string_view GenJsNoModule::getIsOperator() const
{
	return " instanceof ";
}

void GenJsNoModule::writeBoolAndOr(const CiBinaryExpr * expr)
{
	write("!!");
	GenBase::visitBinaryExpr(expr, CiPriority::primary);
}

void GenJsNoModule::writeBoolAndOrAssign(const CiBinaryExpr * expr, CiPriority parent)
{
	expr->right->accept(this, parent);
	writeCharLine(')');
	writeChar('\t');
	expr->left->accept(this, CiPriority::assign);
}

void GenJsNoModule::writeIsVar(const CiExpr * expr, const CiVar * def, bool assign, CiPriority parent)
{
	if (parent > CiPriority::rel)
		writeChar('(');
	if (assign) {
		writeChar('(');
		writeCamelCaseNotKeyword(def->name);
		write(" = ");
		expr->accept(this, CiPriority::argument);
		writeChar(')');
	}
	else
		expr->accept(this, CiPriority::rel);
	write(" instanceof ");
	write(def->type->name);
	if (parent > CiPriority::rel)
		writeChar(')');
}

void GenJsNoModule::visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiVar * def;
	if (expr->op == CiToken::slash && dynamic_cast<const CiIntegerType *>(expr->type.get()) && expr->type->id != CiId::longType) {
		if (parent > CiPriority::or_)
			writeChar('(');
		expr->left->accept(this, CiPriority::mul);
		write(" / ");
		expr->right->accept(this, CiPriority::primary);
		write(" | 0");
		if (parent > CiPriority::or_)
			writeChar(')');
	}
	else if (expr->op == CiToken::divAssign && dynamic_cast<const CiIntegerType *>(expr->type.get()) && expr->type->id != CiId::longType) {
		if (parent > CiPriority::assign)
			writeChar('(');
		expr->left->accept(this, CiPriority::assign);
		write(" = ");
		expr->left->accept(this, CiPriority::mul);
		write(" / ");
		expr->right->accept(this, CiPriority::primary);
		write(" | 0");
		if (parent > CiPriority::assign)
			writeChar(')');
	}
	else if ((expr->op == CiToken::and_ && expr->type->id == CiId::boolType) || (expr->op == CiToken::or_ && expr->type->id == CiId::boolType))
		writeBoolAndOr(expr);
	else if (expr->op == CiToken::xor_ && expr->type->id == CiId::boolType)
		writeEqual(expr->left.get(), expr->right.get(), parent, true);
	else if (expr->op == CiToken::andAssign && expr->type->id == CiId::boolType) {
		write("if (!");
		writeBoolAndOrAssign(expr, CiPriority::primary);
		write(" = false");
	}
	else if (expr->op == CiToken::orAssign && expr->type->id == CiId::boolType) {
		write("if (");
		writeBoolAndOrAssign(expr, CiPriority::argument);
		write(" = true");
	}
	else if (expr->op == CiToken::xorAssign && expr->type->id == CiId::boolType) {
		expr->left->accept(this, CiPriority::assign);
		write(" = ");
		writeEqual(expr->left.get(), expr->right.get(), CiPriority::argument, true);
	}
	else if (expr->op == CiToken::is && (def = dynamic_cast<const CiVar *>(expr->right.get())))
		writeIsVar(expr->left.get(), def, true, parent);
	else
		GenBase::visitBinaryExpr(expr, parent);
}

void GenJsNoModule::visitLambdaExpr(const CiLambdaExpr * expr)
{
	writeName(expr->first);
	write(" => ");
	expr->body->accept(this, CiPriority::statement);
}

void GenJsNoModule::startTemporaryVar(const CiType * type)
{
	std::abort();
}

void GenJsNoModule::defineObjectLiteralTemporary(const CiUnaryExpr * expr)
{
}

void GenJsNoModule::writeAsType(const CiVar * def)
{
}

void GenJsNoModule::writeVarCast(const CiVar * def, const CiExpr * value)
{
	write("const ");
	writeCamelCaseNotKeyword(def->name);
	write(" = ");
	value->accept(this, CiPriority::argument);
	writeAsType(def);
	writeCharLine(';');
}

void GenJsNoModule::writeAssertCast(const CiBinaryExpr * expr)
{
	const CiVar * def = static_cast<const CiVar *>(expr->right.get());
	writeVarCast(def, expr->left.get());
}

void GenJsNoModule::writeAssert(const CiAssert * statement)
{
	if (statement->completesNormally()) {
		write("console.assert(");
		statement->cond->accept(this, CiPriority::argument);
		if (statement->message != nullptr) {
			write(", ");
			statement->message->accept(this, CiPriority::argument);
		}
	}
	else {
		write("throw new Error(");
		if (statement->message != nullptr)
			statement->message->accept(this, CiPriority::argument);
	}
	writeLine(");");
}

void GenJsNoModule::visitBreak(const CiBreak * statement)
{
	if (const CiSwitch *switchStatement = dynamic_cast<const CiSwitch *>(statement->loopOrSwitch)) {
		int label = [](const std::vector<const CiSwitch *> &v, const CiSwitch * value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(this->switchesWithLabel, switchStatement);
		if (label >= 0) {
			write("break ciswitch");
			visitLiteralLong(label);
			writeCharLine(';');
			return;
		}
	}
	GenBase::visitBreak(statement);
}

void GenJsNoModule::visitForeach(const CiForeach * statement)
{
	write("for (const ");
	const CiClassType * klass = static_cast<const CiClassType *>(statement->collection->type.get());
	switch (klass->class_->id) {
	case CiId::stringClass:
	case CiId::arrayStorageClass:
	case CiId::listClass:
	case CiId::hashSetClass:
		writeName(statement->getVar());
		write(" of ");
		statement->collection->accept(this, CiPriority::argument);
		break;
	case CiId::sortedSetClass:
		writeName(statement->getVar());
		write(" of Array.from(");
		statement->collection->accept(this, CiPriority::argument);
		write(").sort(");
		if (dynamic_cast<const CiNumericType *>(klass->getElementType().get()))
			write("(a, b) => a - b");
		writeChar(')');
		break;
	case CiId::dictionaryClass:
	case CiId::sortedDictionaryClass:
	case CiId::orderedDictionaryClass:
		writeChar('[');
		writeName(statement->getVar());
		write(", ");
		writeName(statement->getValueVar());
		write("] of ");
		if (klass->class_->id == CiId::orderedDictionaryClass)
			statement->collection->accept(this, CiPriority::argument);
		else {
			writeCall("Object.entries", statement->collection.get());
			if (dynamic_cast<const CiStringType *>(statement->getVar()->type.get())) {
				if (klass->class_->id == CiId::sortedDictionaryClass)
					write(".sort((a, b) => a[0].localeCompare(b[0]))");
			}
			else if (dynamic_cast<const CiNumericType *>(statement->getVar()->type.get())) {
				write(".map(e => [+e[0], e[1]])");
				if (klass->class_->id == CiId::sortedDictionaryClass)
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

void GenJsNoModule::visitLock(const CiLock * statement)
{
	notSupported(statement, "'lock'");
}

void GenJsNoModule::writeSwitchCaseCond(const CiSwitch * statement, const CiExpr * value, CiPriority parent)
{
	if (const CiVar *def = dynamic_cast<const CiVar *>(value))
		writeIsVar(statement->value.get(), def, parent == CiPriority::condAnd && def->name != "_", parent);
	else
		GenBase::writeSwitchCaseCond(statement, value, parent);
}

void GenJsNoModule::writeIfCaseBody(const std::vector<std::shared_ptr<CiStatement>> * body, bool doWhile, const CiSwitch * statement, const CiCase * kase)
{
	const CiVar * caseVar;
	if (kase != nullptr && (caseVar = dynamic_cast<const CiVar *>(kase->values[0].get())) && caseVar->name != "_") {
		writeChar(' ');
		openBlock();
		writeVarCast(caseVar, statement->value.get());
		writeFirstStatements(&kase->body, CiSwitch::lengthWithoutTrailingBreak(&kase->body));
		closeBlock();
	}
	else
		GenBase::writeIfCaseBody(body, doWhile, statement, kase);
}

void GenJsNoModule::visitSwitch(const CiSwitch * statement)
{
	if (statement->isTypeMatching() || statement->hasWhen()) {
		if (std::any_of(statement->cases.begin(), statement->cases.end(), [](const CiCase &kase) { return CiSwitch::hasEarlyBreak(&kase.body); }) || CiSwitch::hasEarlyBreak(&statement->defaultBody)) {
			write("ciswitch");
			visitLiteralLong(this->switchesWithLabel.size());
			this->switchesWithLabel.push_back(statement);
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

void GenJsNoModule::visitThrow(const CiThrow * statement)
{
	write("throw ");
	statement->message->accept(this, CiPriority::argument);
	writeCharLine(';');
}

void GenJsNoModule::startContainerType(const CiContainerType * container)
{
	writeNewLine();
	writeDoc(container->documentation.get());
}

void GenJsNoModule::visitEnumValue(const CiConst * konst, const CiConst * previous)
{
	if (previous != nullptr)
		writeCharLine(',');
	writeDoc(konst->documentation.get());
	writeUppercaseWithUnderscores(konst->name);
	write(" : ");
	visitLiteralLong(konst->value->intValue());
}

void GenJsNoModule::writeEnum(const CiEnum * enu)
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

void GenJsNoModule::writeConst(const CiConst * konst)
{
	if (konst->visibility != CiVisibility::private_ || dynamic_cast<const CiArrayStorageType *>(konst->type.get())) {
		writeNewLine();
		writeDoc(konst->documentation.get());
		write("static ");
		writeName(konst);
		write(" = ");
		konst->value->accept(this, CiPriority::argument);
		writeCharLine(';');
	}
}

void GenJsNoModule::writeField(const CiField * field)
{
	writeDoc(field->documentation.get());
	GenBase::writeVar(field);
	writeCharLine(';');
}

void GenJsNoModule::writeMethod(const CiMethod * method)
{
	if (method->callType == CiCallType::abstract)
		return;
	this->switchesWithLabel.clear();
	writeNewLine();
	writeMethodDoc(method);
	if (method->callType == CiCallType::static_)
		write("static ");
	writeName(method);
	writeParameters(method, true);
	writeBody(method);
}

void GenJsNoModule::writeConstructor(const CiClass * klass)
{
	this->switchesWithLabel.clear();
	writeLine("constructor()");
	openBlock();
	if (dynamic_cast<const CiClass *>(klass->parent))
		writeLine("super();");
	writeConstructorBody(klass);
	closeBlock();
}

void GenJsNoModule::writeClass(const CiClass * klass, const CiProgram * program)
{
	if (!writeBaseClass(klass, program))
		return;
	startContainerType(klass);
	openClass(klass, "", " extends ");
	if (needsConstructor(klass)) {
		if (klass->constructor != nullptr)
			writeDoc(klass->constructor->documentation.get());
		writeConstructor(klass);
	}
	writeMembers(klass, true);
	closeBlock();
}

void GenJsNoModule::writeLib(const std::map<std::string, std::vector<uint8_t>> * resources)
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
	if (resources->size() == 0)
		return;
	writeNewLine();
	writeLine("class Ci");
	openBlock();
	for (const auto &[name, content] : *resources) {
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

void GenJsNoModule::writeUseStrict()
{
	writeNewLine();
	writeLine("\"use strict\";");
}

void GenJsNoModule::writeProgram(const CiProgram * program)
{
	createOutputFile();
	writeTopLevelNatives(program);
	writeTypes(program);
	writeLib(&program->resources);
	closeFile();
}

void GenJs::startContainerType(const CiContainerType * container)
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

bool GenTs::isJsPrivate(const CiMember * member) const
{
	return false;
}

void GenTs::visitEnumValue(const CiConst * konst, const CiConst * previous)
{
	writeEnumValue(konst);
	writeCharLine(',');
}

void GenTs::writeEnum(const CiEnum * enu)
{
	startContainerType(enu);
	write("enum ");
	write(enu->name);
	writeChar(' ');
	openBlock();
	enu->acceptValues(this);
	closeBlock();
}

void GenTs::writeTypeAndName(const CiNamedValue * value)
{
	writeName(value);
	write(": ");
	writeType(value->type.get());
}

void GenTs::writeType(const CiType * type, bool readOnly)
{
	if (dynamic_cast<const CiNumericType *>(type))
		write(type->id == CiId::longType ? "bigint" : "number");
	else if (const CiEnum *enu = dynamic_cast<const CiEnum *>(type))
		write(enu->id == CiId::boolType ? "boolean" : enu->name);
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(type)) {
		readOnly |= !dynamic_cast<const CiReadWriteClassType *>(klass);
		if (klass->class_->id == CiId::stringClass)
			write("string");
		else if ((klass->class_->id == CiId::arrayPtrClass && !dynamic_cast<const CiNumericType *>(klass->getElementType().get())) || (klass->class_->id == CiId::arrayStorageClass && !dynamic_cast<const CiNumericType *>(klass->getElementType().get())) || klass->class_->id == CiId::listClass || klass->class_->id == CiId::queueClass || klass->class_->id == CiId::stackClass) {
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
			case CiId::arrayPtrClass:
			case CiId::arrayStorageClass:
				writeArrayElementType(klass->getElementType().get());
				write("Array");
				break;
			case CiId::hashSetClass:
			case CiId::sortedSetClass:
				write("Set<");
				writeType(klass->getElementType().get(), false);
				writeChar('>');
				break;
			case CiId::dictionaryClass:
			case CiId::sortedDictionaryClass:
				if (dynamic_cast<const CiEnum *>(klass->getKeyType()))
					write("Partial<");
				write("Record<");
				writeType(klass->getKeyType());
				write(", ");
				writeType(klass->getValueType().get());
				writeChar('>');
				if (dynamic_cast<const CiEnum *>(klass->getKeyType()))
					writeChar('>');
				break;
			case CiId::orderedDictionaryClass:
				write("Map<");
				writeType(klass->getKeyType());
				write(", ");
				writeType(klass->getValueType().get());
				writeChar('>');
				break;
			case CiId::regexClass:
				write("RegExp");
				break;
			case CiId::matchClass:
				write("RegExpMatchArray");
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

void GenTs::writeAsType(const CiVar * def)
{
	write(" as ");
	write(def->type->name);
}

void GenTs::writeBinaryOperand(const CiExpr * expr, CiPriority parent, const CiBinaryExpr * binary)
{
	const CiType * type = binary->type.get();
	if (dynamic_cast<const CiNumericType *>(expr->type.get())) {
		switch (binary->op) {
		case CiToken::equal:
		case CiToken::notEqual:
		case CiToken::less:
		case CiToken::lessOrEqual:
		case CiToken::greater:
		case CiToken::greaterOrEqual:
			type = this->system->promoteNumericTypes(binary->left->type, binary->right->type).get();
			break;
		default:
			break;
		}
	}
	writeCoerced(type, expr, parent);
}

void GenTs::writeEqualOperand(const CiExpr * expr, const CiExpr * other)
{
	if (dynamic_cast<const CiNumericType *>(expr->type.get()))
		writeCoerced(this->system->promoteNumericTypes(expr->type, other->type).get(), expr, CiPriority::equality);
	else
		expr->accept(this, CiPriority::equality);
}

void GenTs::writeBoolAndOr(const CiBinaryExpr * expr)
{
	write("[ ");
	expr->left->accept(this, CiPriority::argument);
	write(", ");
	expr->right->accept(this, CiPriority::argument);
	write(" ].");
	write(expr->op == CiToken::and_ ? "every" : "some");
	write("(Boolean)");
}

void GenTs::defineIsVar(const CiBinaryExpr * binary)
{
	if (const CiVar *def = dynamic_cast<const CiVar *>(binary->right.get())) {
		ensureChildBlock();
		write("let ");
		writeName(def);
		write(": ");
		writeType(binary->left->type.get());
		endStatement();
	}
}

void GenTs::writeVisibility(CiVisibility visibility)
{
	switch (visibility) {
	case CiVisibility::private_:
		write("private ");
		break;
	case CiVisibility::internal:
		break;
	case CiVisibility::protected_:
		write("protected ");
		break;
	case CiVisibility::public_:
		write("public ");
		break;
	default:
		std::abort();
	}
}

void GenTs::writeConst(const CiConst * konst)
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
		konst->value->accept(this, CiPriority::argument);
	}
	writeCharLine(';');
}

void GenTs::writeField(const CiField * field)
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

void GenTs::writeMethod(const CiMethod * method)
{
	writeNewLine();
	writeMethodDoc(method);
	writeVisibility(method->visibility);
	switch (method->callType) {
	case CiCallType::static_:
		write("static ");
		break;
	case CiCallType::virtual_:
		break;
	case CiCallType::abstract:
		write("abstract ");
		break;
	case CiCallType::override_:
		break;
	case CiCallType::normal:
		break;
	case CiCallType::sealed:
		break;
	default:
		std::abort();
	}
	writeName(method);
	writeChar('(');
	int i = 0;
	for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
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

void GenTs::writeClass(const CiClass * klass, const CiProgram * program)
{
	if (!writeBaseClass(klass, program))
		return;
	startContainerType(klass);
	switch (klass->callType) {
	case CiCallType::normal:
		break;
	case CiCallType::abstract:
		write("abstract ");
		break;
	case CiCallType::static_:
	case CiCallType::sealed:
		break;
	default:
		std::abort();
	}
	openClass(klass, "", " extends ");
	if (needsConstructor(klass) || klass->callType == CiCallType::static_) {
		if (klass->constructor != nullptr) {
			writeDoc(klass->constructor->documentation.get());
			writeVisibility(klass->constructor->visibility);
		}
		else if (klass->callType == CiCallType::static_)
			write("private ");
		if (this->genFullCode)
			writeConstructor(klass);
		else
			writeLine("constructor();");
	}
	writeMembers(klass, this->genFullCode);
	closeBlock();
}

void GenTs::writeProgram(const CiProgram * program)
{
	this->system = program->system;
	createOutputFile();
	if (this->genFullCode)
		writeTopLevelNatives(program);
	writeTypes(program);
	if (this->genFullCode)
		writeLib(&program->resources);
	closeFile();
}

void GenPySwift::writeDocPara(const CiDocPara * para, bool many)
{
	if (many) {
		writeNewLine();
		startDocLine();
		writeNewLine();
		startDocLine();
	}
	for (const std::shared_ptr<CiDocInline> &inline_ : para->children) {
		if (const CiDocText *text = dynamic_cast<const CiDocText *>(inline_.get()))
			write(text->text);
		else if (const CiDocCode *code = dynamic_cast<const CiDocCode *>(inline_.get())) {
			writeChar('`');
			write(code->text);
			writeChar('`');
		}
		else if (dynamic_cast<const CiDocLine *>(inline_.get())) {
			writeNewLine();
			startDocLine();
		}
		else
			std::abort();
	}
}

void GenPySwift::writeDocList(const CiDocList * list)
{
	writeNewLine();
	for (const CiDocPara &item : list->items) {
		write(getDocBullet());
		writeDocPara(&item, false);
		writeNewLine();
	}
	startDocLine();
}

void GenPySwift::writeLocalName(const CiSymbol * symbol, CiPriority parent)
{
	if (const CiMember *member = dynamic_cast<const CiMember *>(symbol)) {
		if (member->isStatic())
			writeName(this->currentMethod->parent);
		else
			write("self");
		writeChar('.');
	}
	writeName(symbol);
}

void GenPySwift::visitAggregateInitializer(const CiAggregateInitializer * expr)
{
	write("[ ");
	writeCoercedLiterals(expr->type->asClassType()->getElementType().get(), &expr->items);
	write(" ]");
}

void GenPySwift::visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::increment:
	case CiToken::decrement:
		expr->inner->accept(this, parent);
		break;
	default:
		GenBase::visitPrefixExpr(expr, parent);
		break;
	}
}

void GenPySwift::visitPostfixExpr(const CiPostfixExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::increment:
	case CiToken::decrement:
		expr->inner->accept(this, parent);
		break;
	default:
		GenBase::visitPostfixExpr(expr, parent);
		break;
	}
}

bool GenPySwift::isPtr(const CiExpr * expr)
{
	const CiClassType * klass;
	return (klass = dynamic_cast<const CiClassType *>(expr->type.get())) && klass->class_->id != CiId::stringClass && !dynamic_cast<const CiStorageType *>(klass);
}

void GenPySwift::writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_)
{
	if (isPtr(left) || isPtr(right))
		writeEqualExpr(left, right, parent, getReferenceEqOp(not_));
	else
		GenBase::writeEqual(left, right, parent, not_);
}

void GenPySwift::writeExpr(const CiExpr * expr, CiPriority parent)
{
	expr->accept(this, parent);
}

void GenPySwift::writeListAppend(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	writePostfix(obj, ".append(");
	const CiType * elementType = obj->type->asClassType()->getElementType().get();
	if (args->size() == 0)
		writeNewStorage(elementType);
	else
		writeCoerced(elementType, (*args)[0].get(), CiPriority::argument);
	writeChar(')');
}

bool GenPySwift::visitPreCall(const CiCallExpr * call)
{
	return false;
}

bool GenPySwift::visitXcrement(const CiExpr * expr, bool postfix, bool write)
{
	bool seen;
	if (const CiVar *def = dynamic_cast<const CiVar *>(expr))
		return def->value != nullptr && visitXcrement(def->value.get(), postfix, write);
	else if (dynamic_cast<const CiAggregateInitializer *>(expr) || dynamic_cast<const CiLiteral *>(expr) || dynamic_cast<const CiLambdaExpr *>(expr))
		return false;
	else if (const CiInterpolatedString *interp = dynamic_cast<const CiInterpolatedString *>(expr)) {
		seen = false;
		for (const CiInterpolatedPart &part : interp->parts)
			seen |= visitXcrement(part.argument.get(), postfix, write);
		return seen;
	}
	else if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr))
		return symbol->left != nullptr && visitXcrement(symbol->left.get(), postfix, write);
	else if (const CiUnaryExpr *unary = dynamic_cast<const CiUnaryExpr *>(expr)) {
		if (unary->inner == nullptr)
			return false;
		seen = visitXcrement(unary->inner.get(), postfix, write);
		if ((unary->op == CiToken::increment || unary->op == CiToken::decrement) && postfix == !!dynamic_cast<const CiPostfixExpr *>(unary)) {
			if (write) {
				writeExpr(unary->inner.get(), CiPriority::assign);
				writeLine(unary->op == CiToken::increment ? " += 1" : " -= 1");
			}
			seen = true;
		}
		return seen;
	}
	else if (const CiBinaryExpr *binary = dynamic_cast<const CiBinaryExpr *>(expr)) {
		seen = visitXcrement(binary->left.get(), postfix, write);
		if (binary->op == CiToken::is)
			return seen;
		if (binary->op == CiToken::condAnd || binary->op == CiToken::condOr)
			assert(!visitXcrement(binary->right.get(), postfix, false));
		else
			seen |= visitXcrement(binary->right.get(), postfix, write);
		return seen;
	}
	else if (const CiSelectExpr *select = dynamic_cast<const CiSelectExpr *>(expr)) {
		seen = visitXcrement(select->cond.get(), postfix, write);
		assert(!visitXcrement(select->onTrue.get(), postfix, false));
		assert(!visitXcrement(select->onFalse.get(), postfix, false));
		return seen;
	}
	else if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr)) {
		seen = visitXcrement(call->method.get(), postfix, write);
		for (const std::shared_ptr<CiExpr> &arg : call->arguments)
			seen |= visitXcrement(arg.get(), postfix, write);
		if (!postfix)
			seen |= visitPreCall(call);
		return seen;
	}
	else
		std::abort();
}

void GenPySwift::visitExpr(const CiExpr * statement)
{
	visitXcrement(statement, false, true);
	const CiUnaryExpr * unary;
	if (!(unary = dynamic_cast<const CiUnaryExpr *>(statement)) || (unary->op != CiToken::increment && unary->op != CiToken::decrement)) {
		writeExpr(statement, CiPriority::statement);
		writeNewLine();
		if (const CiVar *def = dynamic_cast<const CiVar *>(statement))
			writeInitCode(def);
	}
	visitXcrement(statement, true, true);
	cleanupTemporaries();
}

void GenPySwift::endStatement()
{
	writeNewLine();
}

void GenPySwift::writeChild(CiStatement * statement)
{
	openChild();
	statement->acceptStatement(this);
	closeChild();
}

void GenPySwift::visitBlock(const CiBlock * statement)
{
	writeStatements(&statement->statements);
}

bool GenPySwift::openCond(std::string_view statement, const CiExpr * cond, CiPriority parent)
{
	visitXcrement(cond, false, true);
	write(statement);
	writeExpr(cond, parent);
	openChild();
	return visitXcrement(cond, true, true);
}

void GenPySwift::writeContinueDoWhile(const CiExpr * cond)
{
	openCond("if ", cond, CiPriority::argument);
	writeLine("continue");
	closeChild();
	visitXcrement(cond, true, true);
	writeLine("break");
}

bool GenPySwift::needCondXcrement(const CiLoop * loop)
{
	return loop->cond != nullptr;
}

void GenPySwift::endBody(const CiLoop * loop)
{
	if (const CiFor *forLoop = dynamic_cast<const CiFor *>(loop)) {
		if (forLoop->isRange)
			return;
		visitOptionalStatement(forLoop->advance.get());
	}
	if (needCondXcrement(loop))
		visitXcrement(loop->cond.get(), false, true);
}

void GenPySwift::visitContinue(const CiContinue * statement)
{
	if (const CiDoWhile *doWhile = dynamic_cast<const CiDoWhile *>(statement->loop))
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

void GenPySwift::visitDoWhile(const CiDoWhile * statement)
{
	openWhileTrue();
	statement->body->acceptStatement(this);
	if (statement->body->completesNormally()) {
		openCond(getIfNot(), statement->cond.get(), CiPriority::primary);
		writeLine("break");
		closeChild();
		visitXcrement(statement->cond.get(), true, true);
	}
	closeChild();
}

void GenPySwift::openWhile(const CiLoop * loop)
{
	openCond("while ", loop->cond.get(), CiPriority::argument);
}

void GenPySwift::closeWhile(const CiLoop * loop)
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

void GenPySwift::visitFor(const CiFor * statement)
{
	if (statement->isRange) {
		const CiVar * iter = static_cast<const CiVar *>(statement->init.get());
		write("for ");
		if (statement->isIteratorUsed)
			writeName(iter);
		else
			writeChar('_');
		write(" in ");
		const CiBinaryExpr * cond = static_cast<const CiBinaryExpr *>(statement->cond.get());
		writeForRange(iter, cond, statement->rangeStep);
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

void GenPySwift::visitIf(const CiIf * statement)
{
	bool condPostXcrement = openCond("if ", statement->cond.get(), CiPriority::argument);
	statement->onTrue->acceptStatement(this);
	closeChild();
	if (statement->onFalse == nullptr && condPostXcrement && !statement->onTrue->completesNormally())
		visitXcrement(statement->cond.get(), true, true);
	else if (statement->onFalse != nullptr || condPostXcrement) {
		CiIf * childIf;
		if (!condPostXcrement && (childIf = dynamic_cast<CiIf *>(statement->onFalse.get())) && !visitXcrement(childIf->cond.get(), false, false)) {
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

void GenPySwift::visitReturn(const CiReturn * statement)
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

void GenPySwift::visitWhile(const CiWhile * statement)
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

std::string_view GenSwift::getDocBullet() const
{
	return "/// * ";
}

void GenSwift::writeDoc(const CiCodeDoc * doc)
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

void GenSwift::writeName(const CiSymbol * symbol)
{
	const CiConst * konst;
	if (dynamic_cast<const CiContainerType *>(symbol))
		write(symbol->name);
	else if ((konst = dynamic_cast<const CiConst *>(symbol)) && konst->inMethod != nullptr) {
		writeCamelCase(konst->inMethod->name);
		writePascalCase(symbol->name);
	}
	else if (dynamic_cast<const CiVar *>(symbol) || dynamic_cast<const CiMember *>(symbol))
		writeCamelCaseNotKeyword(symbol->name);
	else
		std::abort();
}

void GenSwift::writeLocalName(const CiSymbol * symbol, CiPriority parent)
{
	const CiForeach * forEach;
	if ((forEach = dynamic_cast<const CiForeach *>(symbol->parent)) && dynamic_cast<const CiStringType *>(forEach->collection->type.get())) {
		write("Int(");
		writeCamelCaseNotKeyword(symbol->name);
		write(".value)");
	}
	else
		GenPySwift::writeLocalName(symbol, parent);
}

void GenSwift::writeMemberOp(const CiExpr * left, const CiSymbolReference * symbol)
{
	if (left->type != nullptr && left->type->nullable)
		writeChar('!');
	writeChar('.');
}

void GenSwift::openIndexing(const CiExpr * collection)
{
	collection->accept(this, CiPriority::primary);
	if (collection->type->nullable)
		writeChar('!');
	writeChar('[');
}

bool GenSwift::isArrayRef(const CiArrayStorageType * array)
{
	return array->ptrTaken || dynamic_cast<const CiStorageType *>(array->getElementType().get());
}

void GenSwift::writeClassName(const CiClassType * klass)
{
	switch (klass->class_->id) {
	case CiId::stringClass:
		write("String");
		break;
	case CiId::arrayPtrClass:
		this->arrayRef = true;
		write("ArrayRef<");
		writeType(klass->getElementType().get());
		writeChar('>');
		break;
	case CiId::listClass:
	case CiId::queueClass:
	case CiId::stackClass:
		writeChar('[');
		writeType(klass->getElementType().get());
		writeChar(']');
		break;
	case CiId::hashSetClass:
	case CiId::sortedSetClass:
		write("Set<");
		writeType(klass->getElementType().get());
		writeChar('>');
		break;
	case CiId::dictionaryClass:
	case CiId::sortedDictionaryClass:
		writeChar('[');
		writeType(klass->getKeyType());
		write(": ");
		writeType(klass->getValueType().get());
		writeChar(']');
		break;
	case CiId::orderedDictionaryClass:
		notSupported(klass, "OrderedDictionary");
		break;
	case CiId::lockClass:
		include("Foundation");
		write("NSRecursiveLock");
		break;
	default:
		write(klass->class_->name);
		break;
	}
}

void GenSwift::writeType(const CiType * type)
{
	if (dynamic_cast<const CiNumericType *>(type)) {
		switch (type->id) {
		case CiId::sByteRange:
			write("Int8");
			break;
		case CiId::byteRange:
			write("UInt8");
			break;
		case CiId::shortRange:
			write("Int16");
			break;
		case CiId::uShortRange:
			write("UInt16");
			break;
		case CiId::intType:
			write("Int");
			break;
		case CiId::longType:
			write("Int64");
			break;
		case CiId::floatType:
			write("Float");
			break;
		case CiId::doubleType:
			write("Double");
			break;
		default:
			std::abort();
		}
	}
	else if (dynamic_cast<const CiEnum *>(type))
		write(type->id == CiId::boolType ? "Bool" : type->name);
	else if (const CiArrayStorageType *arrayStg = dynamic_cast<const CiArrayStorageType *>(type)) {
		if (isArrayRef(arrayStg)) {
			this->arrayRef = true;
			write("ArrayRef<");
			writeType(arrayStg->getElementType().get());
			writeChar('>');
		}
		else {
			writeChar('[');
			writeType(arrayStg->getElementType().get());
			writeChar(']');
		}
	}
	else if (const CiClassType *klass = dynamic_cast<const CiClassType *>(type)) {
		writeClassName(klass);
		if (klass->nullable)
			writeChar('?');
	}
	else
		write(type->name);
}

void GenSwift::writeTypeAndName(const CiNamedValue * value)
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

void GenSwift::writeUnwrapped(const CiExpr * expr, CiPriority parent, bool substringOk)
{
	if (expr->type->nullable) {
		expr->accept(this, CiPriority::primary);
		writeChar('!');
	}
	else {
		const CiCallExpr * call;
		if (!substringOk && (call = dynamic_cast<const CiCallExpr *>(expr)) && call->method->symbol->id == CiId::stringSubstring)
			writeCall("String", expr);
		else
			expr->accept(this, parent);
	}
}

void GenSwift::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
{
	if (std::any_of(expr->parts.begin(), expr->parts.end(), [](const CiInterpolatedPart &part) { return part.widthExpr != nullptr || part.format != ' ' || part.precision >= 0; })) {
		include("Foundation");
		write("String(format: ");
		writePrintf(expr, false);
	}
	else {
		writeChar('"');
		for (const CiInterpolatedPart &part : expr->parts) {
			write(part.prefix);
			write("\\(");
			writeUnwrapped(part.argument.get(), CiPriority::argument, true);
			writeChar(')');
		}
		write(expr->suffix);
		writeChar('"');
	}
}

void GenSwift::writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent)
{
	const CiBinaryExpr * binary;
	if (dynamic_cast<const CiNumericType *>(type) && !dynamic_cast<const CiLiteral *>(expr) && getTypeId(type, false) != getTypeId(expr->type.get(), (binary = dynamic_cast<const CiBinaryExpr *>(expr)) && binary->op != CiToken::leftBracket)) {
		writeType(type);
		writeChar('(');
		const CiCallExpr * call;
		if (dynamic_cast<const CiIntegerType *>(type) && (call = dynamic_cast<const CiCallExpr *>(expr)) && call->method->symbol->id == CiId::mathTruncate)
			call->arguments[0]->accept(this, CiPriority::argument);
		else
			expr->accept(this, CiPriority::argument);
		writeChar(')');
	}
	else if (!type->nullable)
		writeUnwrapped(expr, parent, false);
	else
		expr->accept(this, parent);
}

void GenSwift::writeStringLength(const CiExpr * expr)
{
	writeUnwrapped(expr, CiPriority::primary, true);
	write(".count");
}

void GenSwift::writeCharAt(const CiBinaryExpr * expr)
{
	this->stringCharAt = true;
	write("ciStringCharAt(");
	writeUnwrapped(expr->left.get(), CiPriority::argument, false);
	write(", ");
	expr->right->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenSwift::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::mathNaN:
		write("Float.nan");
		break;
	case CiId::mathNegativeInfinity:
		write("-Float.infinity");
		break;
	case CiId::mathPositiveInfinity:
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

void GenSwift::writeStringContains(const CiExpr * obj, std::string_view name, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	writeUnwrapped(obj, CiPriority::primary, true);
	writeChar('.');
	write(name);
	writeChar('(');
	writeUnwrapped((*args)[0].get(), CiPriority::argument, true);
	writeChar(')');
}

void GenSwift::writeRange(const CiExpr * startIndex, const CiExpr * length)
{
	writeCoerced(this->system->intType.get(), startIndex, CiPriority::shift);
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

void GenSwift::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::none:
	case CiId::listContains:
	case CiId::listSortAll:
	case CiId::hashSetContains:
	case CiId::hashSetRemove:
	case CiId::sortedSetContains:
	case CiId::sortedSetRemove:
		if (obj == nullptr) {
			if (method->isStatic()) {
				writeName(this->currentMethod->parent);
				writeChar('.');
			}
		}
		else if (isReferenceTo(obj, CiId::basePtr))
			write("super.");
		else {
			obj->accept(this, CiPriority::primary);
			writeMemberOp(obj, nullptr);
		}
		writeName(method);
		writeArgsInParentheses(method, args);
		break;
	case CiId::classToString:
		obj->accept(this, CiPriority::primary);
		writeMemberOp(obj, nullptr);
		write("description");
		break;
	case CiId::enumFromInt:
		write(method->type->name);
		write("(rawValue: ");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::enumHasFlag:
		writeMethodCall(obj, "contains", (*args)[0].get());
		break;
	case CiId::stringContains:
		writeStringContains(obj, "contains", args);
		break;
	case CiId::stringEndsWith:
		writeStringContains(obj, "hasSuffix", args);
		break;
	case CiId::stringIndexOf:
		include("Foundation");
		this->stringIndexOf = true;
		write("ciStringIndexOf(");
		writeUnwrapped(obj, CiPriority::argument, true);
		write(", ");
		writeUnwrapped((*args)[0].get(), CiPriority::argument, true);
		writeChar(')');
		break;
	case CiId::stringLastIndexOf:
		include("Foundation");
		this->stringIndexOf = true;
		write("ciStringIndexOf(");
		writeUnwrapped(obj, CiPriority::argument, true);
		write(", ");
		writeUnwrapped((*args)[0].get(), CiPriority::argument, true);
		write(", .backwards)");
		break;
	case CiId::stringReplace:
		writeUnwrapped(obj, CiPriority::primary, true);
		write(".replacingOccurrences(of: ");
		writeUnwrapped((*args)[0].get(), CiPriority::argument, true);
		write(", with: ");
		writeUnwrapped((*args)[1].get(), CiPriority::argument, true);
		writeChar(')');
		break;
	case CiId::stringStartsWith:
		writeStringContains(obj, "hasPrefix", args);
		break;
	case CiId::stringSubstring:
		if ((*args)[0]->isLiteralZero())
			writeUnwrapped(obj, CiPriority::primary, true);
		else {
			this->stringSubstring = true;
			write("ciStringSubstring(");
			writeUnwrapped(obj, CiPriority::argument, false);
			write(", ");
			writeCoerced(this->system->intType.get(), (*args)[0].get(), CiPriority::argument);
			writeChar(')');
		}
		if (args->size() == 2) {
			write(".prefix(");
			writeCoerced(this->system->intType.get(), (*args)[1].get(), CiPriority::argument);
			writeChar(')');
		}
		break;
	case CiId::arrayCopyTo:
	case CiId::listCopyTo:
		openIndexing((*args)[1].get());
		writeRange((*args)[2].get(), (*args)[3].get());
		write("] = ");
		openIndexing(obj);
		writeRange((*args)[0].get(), (*args)[3].get());
		writeChar(']');
		break;
	case CiId::arrayFillAll:
		obj->accept(this, CiPriority::assign);
		{
			const CiArrayStorageType * array;
			if ((array = dynamic_cast<const CiArrayStorageType *>(obj->type.get())) && !isArrayRef(array)) {
				write(" = [");
				writeType(array->getElementType().get());
				write("](repeating: ");
				writeCoerced(array->getElementType().get(), (*args)[0].get(), CiPriority::argument);
				write(", count: ");
				visitLiteralLong(array->length);
				writeChar(')');
			}
			else {
				write(".fill");
				writeArgsInParentheses(method, args);
			}
			break;
		}
	case CiId::arrayFillPart:
		{
			const CiArrayStorageType * array2;
			if ((array2 = dynamic_cast<const CiArrayStorageType *>(obj->type.get())) && !isArrayRef(array2)) {
				openIndexing(obj);
				writeRange((*args)[1].get(), (*args)[2].get());
				write("] = ArraySlice(repeating: ");
				writeCoerced(array2->getElementType().get(), (*args)[0].get(), CiPriority::argument);
				write(", count: ");
				writeCoerced(this->system->intType.get(), (*args)[2].get(), CiPriority::argument);
				writeChar(')');
			}
			else {
				obj->accept(this, CiPriority::primary);
				writeMemberOp(obj, nullptr);
				write("fill");
				writeArgsInParentheses(method, args);
			}
			break;
		}
	case CiId::arraySortAll:
		writePostfix(obj, "[0..<");
		{
			const CiArrayStorageType * array3 = static_cast<const CiArrayStorageType *>(obj->type.get());
			visitLiteralLong(array3->length);
			write("].sort()");
			break;
		}
	case CiId::arraySortPart:
	case CiId::listSortPart:
		openIndexing(obj);
		writeRange((*args)[0].get(), (*args)[1].get());
		write("].sort()");
		break;
	case CiId::listAdd:
	case CiId::queueEnqueue:
	case CiId::stackPush:
		writeListAppend(obj, args);
		break;
	case CiId::listAddRange:
		obj->accept(this, CiPriority::assign);
		write(" += ");
		(*args)[0]->accept(this, CiPriority::argument);
		break;
	case CiId::listAll:
		writePostfix(obj, ".allSatisfy ");
		(*args)[0]->accept(this, CiPriority::argument);
		break;
	case CiId::listAny:
		writePostfix(obj, ".contains ");
		(*args)[0]->accept(this, CiPriority::argument);
		break;
	case CiId::listClear:
	case CiId::queueClear:
	case CiId::stackClear:
	case CiId::hashSetClear:
	case CiId::sortedSetClear:
	case CiId::dictionaryClear:
	case CiId::sortedDictionaryClear:
		writePostfix(obj, ".removeAll()");
		break;
	case CiId::listIndexOf:
		if (parent > CiPriority::rel)
			writeChar('(');
		writePostfix(obj, ".firstIndex(of: ");
		(*args)[0]->accept(this, CiPriority::argument);
		write(") ?? -1");
		if (parent > CiPriority::rel)
			writeChar(')');
		break;
	case CiId::listInsert:
		writePostfix(obj, ".insert(");
		{
			const CiType * elementType = obj->type->asClassType()->getElementType().get();
			if (args->size() == 1)
				writeNewStorage(elementType);
			else
				writeCoerced(elementType, (*args)[1].get(), CiPriority::argument);
			write(", at: ");
			writeCoerced(this->system->intType.get(), (*args)[0].get(), CiPriority::argument);
			writeChar(')');
			break;
		}
	case CiId::listLast:
	case CiId::stackPeek:
		writePostfix(obj, ".last");
		break;
	case CiId::listRemoveAt:
		writePostfix(obj, ".remove(at: ");
		writeCoerced(this->system->intType.get(), (*args)[0].get(), CiPriority::argument);
		writeChar(')');
		break;
	case CiId::listRemoveRange:
		writePostfix(obj, ".removeSubrange(");
		writeRange((*args)[0].get(), (*args)[1].get());
		writeChar(')');
		break;
	case CiId::queueDequeue:
		writePostfix(obj, ".removeFirst()");
		break;
	case CiId::queuePeek:
		writePostfix(obj, ".first");
		break;
	case CiId::stackPop:
		writePostfix(obj, ".removeLast()");
		break;
	case CiId::hashSetAdd:
	case CiId::sortedSetAdd:
		writePostfix(obj, ".insert(");
		writeCoerced(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), CiPriority::argument);
		writeChar(')');
		break;
	case CiId::dictionaryAdd:
		writeDictionaryAdd(obj, args);
		break;
	case CiId::dictionaryContainsKey:
	case CiId::sortedDictionaryContainsKey:
		if (parent > CiPriority::equality)
			writeChar('(');
		writeIndexing(obj, (*args)[0].get());
		write(" != nil");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::dictionaryRemove:
	case CiId::sortedDictionaryRemove:
		writePostfix(obj, ".removeValue(forKey: ");
		(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::consoleWrite:
		write("print(");
		writeUnwrapped((*args)[0].get(), CiPriority::argument, true);
		write(", terminator: \"\")");
		break;
	case CiId::consoleWriteLine:
		write("print(");
		if (args->size() == 1)
			writeUnwrapped((*args)[0].get(), CiPriority::argument, true);
		writeChar(')');
		break;
	case CiId::uTF8GetByteCount:
		writeUnwrapped((*args)[0].get(), CiPriority::primary, true);
		write(".utf8.count");
		break;
	case CiId::uTF8GetBytes:
		if (addVar("cibytes"))
			write(this->varBytesAtIndent[this->indent] ? "var " : "let ");
		write("cibytes = [UInt8](");
		writeUnwrapped((*args)[0].get(), CiPriority::primary, true);
		writeLine(".utf8)");
		openIndexing((*args)[1].get());
		writeCoerced(this->system->intType.get(), (*args)[2].get(), CiPriority::shift);
		if ((*args)[2]->isLiteralZero())
			write("..<");
		else {
			write(" ..< ");
			writeCoerced(this->system->intType.get(), (*args)[2].get(), CiPriority::add);
			write(" + ");
		}
		writeLine("cibytes.count] = cibytes[...]");
		break;
	case CiId::uTF8GetString:
		write("String(decoding: ");
		openIndexing((*args)[0].get());
		writeRange((*args)[1].get(), (*args)[2].get());
		write("], as: UTF8.self)");
		break;
	case CiId::environmentGetEnvironmentVariable:
		include("Foundation");
		write("ProcessInfo.processInfo.environment[");
		writeUnwrapped((*args)[0].get(), CiPriority::argument, false);
		writeChar(']');
		break;
	case CiId::mathMethod:
	case CiId::mathLog2:
		include("Foundation");
		writeCamelCase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathAbs:
	case CiId::mathMaxInt:
	case CiId::mathMaxDouble:
	case CiId::mathMinInt:
	case CiId::mathMinDouble:
		writeCamelCase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathCeiling:
		include("Foundation");
		writeCall("ceil", (*args)[0].get());
		break;
	case CiId::mathClamp:
		write("min(max(");
		writeClampAsMinMax(args);
		break;
	case CiId::mathFusedMultiplyAdd:
		include("Foundation");
		writeCall("fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case CiId::mathIsFinite:
		writePostfix((*args)[0].get(), ".isFinite");
		break;
	case CiId::mathIsInfinity:
		writePostfix((*args)[0].get(), ".isInfinite");
		break;
	case CiId::mathIsNaN:
		writePostfix((*args)[0].get(), ".isNaN");
		break;
	case CiId::mathRound:
		writePostfix((*args)[0].get(), ".rounded()");
		break;
	case CiId::mathTruncate:
		include("Foundation");
		writeCall("trunc", (*args)[0].get());
		break;
	default:
		notSupported(obj, method->name);
		break;
	}
}

void GenSwift::writeNewArrayStorage(const CiArrayStorageType * array)
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

void GenSwift::writeNew(const CiReadWriteClassType * klass, CiPriority parent)
{
	writeClassName(klass);
	write("()");
}

void GenSwift::writeDefaultValue(const CiType * type)
{
	if (dynamic_cast<const CiNumericType *>(type))
		writeChar('0');
	else if (const CiEnum *enu = dynamic_cast<const CiEnum *>(type)) {
		if (enu->id == CiId::boolType)
			write("false");
		else {
			writeName(enu);
			writeChar('.');
			writeName(enu->getFirstValue());
		}
	}
	else if (dynamic_cast<const CiStringType *>(type) && !type->nullable)
		write("\"\"");
	else if (const CiArrayStorageType *array = dynamic_cast<const CiArrayStorageType *>(type))
		writeNewArrayStorage(array);
	else
		write("nil");
}

void GenSwift::writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent)
{
	this->arrayRef = true;
	write("ArrayRef<");
	writeType(elementType);
	write(">(");
	if (dynamic_cast<const CiArrayStorageType *>(elementType)) {
		write("factory: { ");
		writeNewStorage(elementType);
		write(" }");
	}
	else if (const CiStorageType *klass = dynamic_cast<const CiStorageType *>(elementType)) {
		write("factory: ");
		writeName(klass->class_);
		write(".init");
	}
	else {
		write("repeating: ");
		writeDefaultValue(elementType);
	}
	write(", count: ");
	lengthExpr->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenSwift::visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent)
{
	if (expr->op == CiToken::tilde && dynamic_cast<const CiEnumFlags *>(expr->type.get())) {
		write(expr->type->name);
		write("(rawValue: ~");
		writePostfix(expr->inner.get(), ".rawValue)");
	}
	else
		GenPySwift::visitPrefixExpr(expr, parent);
}

void GenSwift::writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	openIndexing(expr->left.get());
	const CiClassType * klass = static_cast<const CiClassType *>(expr->left->type.get());
	const CiType * indexType;
	switch (klass->class_->id) {
	case CiId::arrayPtrClass:
	case CiId::arrayStorageClass:
	case CiId::listClass:
		indexType = this->system->intType.get();
		break;
	default:
		indexType = klass->getKeyType();
		break;
	}
	writeCoerced(indexType, expr->right.get(), CiPriority::argument);
	writeChar(']');
	const CiClassType * dict;
	if (parent != CiPriority::assign && (dict = dynamic_cast<const CiClassType *>(expr->left->type.get())) && dict->class_->typeParameterCount == 2)
		writeChar('!');
}

void GenSwift::writeBinaryOperand(const CiExpr * expr, CiPriority parent, const CiBinaryExpr * binary)
{
	if (expr->type->id != CiId::boolType) {
		if (binary->op == CiToken::plus && binary->type->id == CiId::stringStorageType) {
			writeUnwrapped(expr, parent, true);
			return;
		}
		if (binary->op == CiToken::plus || binary->op == CiToken::minus || binary->op == CiToken::asterisk || binary->op == CiToken::slash || binary->op == CiToken::mod || binary->op == CiToken::and_ || binary->op == CiToken::or_ || binary->op == CiToken::xor_ || (binary->op == CiToken::shiftLeft && expr == binary->left.get()) || (binary->op == CiToken::shiftRight && expr == binary->left.get())) {
			if (!dynamic_cast<const CiLiteral *>(expr)) {
				const CiType * type = this->system->promoteNumericTypes(binary->left->type, binary->right->type).get();
				if (type != expr->type.get()) {
					writeCoerced(type, expr, parent);
					return;
				}
			}
		}
		else if (binary->op == CiToken::less || binary->op == CiToken::lessOrEqual || binary->op == CiToken::greater || binary->op == CiToken::greaterOrEqual || binary->op == CiToken::equal || binary->op == CiToken::notEqual) {
			const CiType * typeComp = this->system->promoteFloatingTypes(binary->left->type.get(), binary->right->type.get()).get();
			if (typeComp != nullptr && typeComp != expr->type.get()) {
				writeCoerced(typeComp, expr, parent);
				return;
			}
		}
	}
	expr->accept(this, parent);
}

void GenSwift::writeEnumFlagsAnd(const CiExpr * left, std::string_view method, std::string_view notMethod, const CiExpr * right)
{
	const CiPrefixExpr * negation;
	if ((negation = dynamic_cast<const CiPrefixExpr *>(right)) && negation->op == CiToken::tilde)
		writeMethodCall(left, notMethod, negation->inner.get());
	else
		writeMethodCall(left, method, right);
}

const CiExpr * GenSwift::writeAssignNested(const CiBinaryExpr * expr)
{
	const CiBinaryExpr * rightBinary;
	if ((rightBinary = dynamic_cast<const CiBinaryExpr *>(expr->right.get())) && rightBinary->isAssign()) {
		visitBinaryExpr(rightBinary, CiPriority::statement);
		writeNewLine();
		return rightBinary->left.get();
	}
	return expr->right.get();
}

void GenSwift::writeSwiftAssign(const CiBinaryExpr * expr, const CiExpr * right)
{
	expr->left->accept(this, CiPriority::assign);
	writeChar(' ');
	write(expr->getOpString());
	writeChar(' ');
	const CiBinaryExpr * leftBinary;
	const CiClassType * dict;
	if (dynamic_cast<const CiLiteralNull *>(right) && (leftBinary = dynamic_cast<const CiBinaryExpr *>(expr->left.get())) && leftBinary->op == CiToken::leftBracket && (dict = dynamic_cast<const CiClassType *>(leftBinary->left->type.get())) && dict->class_->typeParameterCount == 2) {
		writeType(dict->getValueType().get());
		write(".none");
	}
	else
		writeCoerced(expr->type.get(), right, CiPriority::argument);
}

void GenSwift::visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	const CiExpr * right;
	switch (expr->op) {
	case CiToken::shiftLeft:
		writeBinaryExpr(expr, parent > CiPriority::mul, CiPriority::primary, " << ", CiPriority::primary);
		break;
	case CiToken::shiftRight:
		writeBinaryExpr(expr, parent > CiPriority::mul, CiPriority::primary, " >> ", CiPriority::primary);
		break;
	case CiToken::and_:
		if (expr->type->id == CiId::boolType)
			writeCall("{ a, b in a && b }", expr->left.get(), expr->right.get());
		else if (dynamic_cast<const CiEnumFlags *>(expr->type.get()))
			writeEnumFlagsAnd(expr->left.get(), "intersection", "subtracting", expr->right.get());
		else
			writeBinaryExpr(expr, parent > CiPriority::mul, CiPriority::mul, " & ", CiPriority::primary);
		break;
	case CiToken::or_:
		if (expr->type->id == CiId::boolType)
			writeCall("{ a, b in a || b }", expr->left.get(), expr->right.get());
		else if (dynamic_cast<const CiEnumFlags *>(expr->type.get()))
			writeMethodCall(expr->left.get(), "union", expr->right.get());
		else
			writeBinaryExpr(expr, parent > CiPriority::add, CiPriority::add, " | ", CiPriority::mul);
		break;
	case CiToken::xor_:
		if (expr->type->id == CiId::boolType)
			writeEqual(expr->left.get(), expr->right.get(), parent, true);
		else if (dynamic_cast<const CiEnumFlags *>(expr->type.get()))
			writeMethodCall(expr->left.get(), "symmetricDifference", expr->right.get());
		else
			writeBinaryExpr(expr, parent > CiPriority::add, CiPriority::add, " ^ ", CiPriority::mul);
		break;
	case CiToken::assign:
	case CiToken::addAssign:
	case CiToken::subAssign:
	case CiToken::mulAssign:
	case CiToken::divAssign:
	case CiToken::modAssign:
	case CiToken::shiftLeftAssign:
	case CiToken::shiftRightAssign:
		writeSwiftAssign(expr, writeAssignNested(expr));
		break;
	case CiToken::andAssign:
		right = writeAssignNested(expr);
		if (expr->type->id == CiId::boolType) {
			write("if ");
			const CiPrefixExpr * negation;
			if ((negation = dynamic_cast<const CiPrefixExpr *>(right)) && negation->op == CiToken::exclamationMark) {
				negation->inner->accept(this, CiPriority::argument);
			}
			else {
				writeChar('!');
				right->accept(this, CiPriority::primary);
			}
			openChild();
			expr->left->accept(this, CiPriority::assign);
			writeLine(" = false");
			this->indent--;
			writeChar('}');
		}
		else if (dynamic_cast<const CiEnumFlags *>(expr->type.get()))
			writeEnumFlagsAnd(expr->left.get(), "formIntersection", "subtract", right);
		else
			writeSwiftAssign(expr, right);
		break;
	case CiToken::orAssign:
		right = writeAssignNested(expr);
		if (expr->type->id == CiId::boolType) {
			write("if ");
			right->accept(this, CiPriority::argument);
			openChild();
			expr->left->accept(this, CiPriority::assign);
			writeLine(" = true");
			this->indent--;
			writeChar('}');
		}
		else if (dynamic_cast<const CiEnumFlags *>(expr->type.get()))
			writeMethodCall(expr->left.get(), "formUnion", right);
		else
			writeSwiftAssign(expr, right);
		break;
	case CiToken::xorAssign:
		right = writeAssignNested(expr);
		if (expr->type->id == CiId::boolType) {
			expr->left->accept(this, CiPriority::assign);
			write(" = ");
			expr->left->accept(this, CiPriority::equality);
			write(" != ");
			expr->right->accept(this, CiPriority::equality);
		}
		else if (dynamic_cast<const CiEnumFlags *>(expr->type.get()))
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
	write("CiResource.");
	writeResourceName(name);
}

bool GenSwift::throws(const CiExpr * expr)
{
	if (dynamic_cast<const CiVar *>(expr) || dynamic_cast<const CiLiteral *>(expr) || dynamic_cast<const CiLambdaExpr *>(expr))
		return false;
	else if (const CiAggregateInitializer *init = dynamic_cast<const CiAggregateInitializer *>(expr))
		return std::any_of(init->items.begin(), init->items.end(), [](const std::shared_ptr<CiExpr> &field) { return throws(field.get()); });
	else if (const CiInterpolatedString *interp = dynamic_cast<const CiInterpolatedString *>(expr))
		return std::any_of(interp->parts.begin(), interp->parts.end(), [](const CiInterpolatedPart &part) { return throws(part.argument.get()); });
	else if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr))
		return symbol->left != nullptr && throws(symbol->left.get());
	else if (const CiUnaryExpr *unary = dynamic_cast<const CiUnaryExpr *>(expr))
		return unary->inner != nullptr && throws(unary->inner.get());
	else if (const CiBinaryExpr *binary = dynamic_cast<const CiBinaryExpr *>(expr))
		return throws(binary->left.get()) || throws(binary->right.get());
	else if (const CiSelectExpr *select = dynamic_cast<const CiSelectExpr *>(expr))
		return throws(select->cond.get()) || throws(select->onTrue.get()) || throws(select->onFalse.get());
	else if (const CiCallExpr *call = dynamic_cast<const CiCallExpr *>(expr)) {
		const CiMethod * method = static_cast<const CiMethod *>(call->method->symbol);
		return method->throws || (call->method->left != nullptr && throws(call->method->left.get())) || std::any_of(call->arguments.begin(), call->arguments.end(), [](const std::shared_ptr<CiExpr> &arg) { return throws(arg.get()); });
	}
	else
		std::abort();
}

void GenSwift::writeExpr(const CiExpr * expr, CiPriority parent)
{
	if (throws(expr))
		write("try ");
	GenPySwift::writeExpr(expr, parent);
}

void GenSwift::writeCoercedExpr(const CiType * type, const CiExpr * expr)
{
	if (throws(expr))
		write("try ");
	GenBase::writeCoercedExpr(type, expr);
}

void GenSwift::startTemporaryVar(const CiType * type)
{
	write("var ");
}

void GenSwift::visitExpr(const CiExpr * statement)
{
	writeTemporaries(statement);
	const CiCallExpr * call;
	if ((call = dynamic_cast<const CiCallExpr *>(statement)) && statement->type->id != CiId::voidType)
		write("_ = ");
	GenPySwift::visitExpr(statement);
}

void GenSwift::initVarsAtIndent()
{
	while (this->varsAtIndent.size() <= this->indent) {
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

void GenSwift::writeVar(const CiNamedValue * def)
{
	if (dynamic_cast<const CiField *>(def) || addVar(def->name)) {
		const CiArrayStorageType * array;
		const CiStorageType * stg;
		const CiVar * local;
		write(((array = dynamic_cast<const CiArrayStorageType *>(def->type.get())) ? isArrayRef(array) : (stg = dynamic_cast<const CiStorageType *>(def->type.get())) ? stg->class_->typeParameterCount == 0 && !def->isAssignableStorage() : (local = dynamic_cast<const CiVar *>(def)) && !local->isAssigned) ? "let " : "var ");
		GenBase::writeVar(def);
	}
	else {
		writeName(def);
		writeVarInit(def);
	}
}

bool GenSwift::needsVarBytes(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	int count = 0;
	for (const std::shared_ptr<CiStatement> &statement : *statements) {
		const CiCallExpr * call;
		if ((call = dynamic_cast<const CiCallExpr *>(statement.get())) && call->method->symbol->id == CiId::uTF8GetBytes) {
			if (++count == 2)
				return true;
		}
	}
	return false;
}

void GenSwift::writeStatements(const std::vector<std::shared_ptr<CiStatement>> * statements)
{
	this->varBytesAtIndent[this->indent] = needsVarBytes(statements);
	GenBase::writeStatements(statements);
}

void GenSwift::visitLambdaExpr(const CiLambdaExpr * expr)
{
	write("{ ");
	writeName(expr->first);
	write(" in ");
	expr->body->accept(this, CiPriority::statement);
	write(" }");
}

void GenSwift::writeAssertCast(const CiBinaryExpr * expr)
{
	write("let ");
	const CiVar * def = static_cast<const CiVar *>(expr->right.get());
	writeCamelCaseNotKeyword(def->name);
	write(" = ");
	expr->left->accept(this, CiPriority::equality);
	write(" as! ");
	writeLine(def->type->name);
}

void GenSwift::writeAssert(const CiAssert * statement)
{
	write("assert(");
	writeExpr(statement->cond.get(), CiPriority::argument);
	if (statement->message != nullptr) {
		write(", ");
		writeExpr(statement->message.get(), CiPriority::argument);
	}
	writeCharLine(')');
}

void GenSwift::visitBreak(const CiBreak * statement)
{
	writeLine("break");
}

bool GenSwift::needCondXcrement(const CiLoop * loop)
{
	return loop->cond != nullptr && (!loop->hasBreak || !visitXcrement(loop->cond.get(), true, false));
}

std::string_view GenSwift::getIfNot() const
{
	return "if !";
}

void GenSwift::writeContinueDoWhile(const CiExpr * cond)
{
	visitXcrement(cond, false, true);
	writeLine("continue");
}

void GenSwift::visitDoWhile(const CiDoWhile * statement)
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
		writeExpr(statement->cond.get(), CiPriority::argument);
		writeNewLine();
	}
}

void GenSwift::writeElseIf()
{
	write("else ");
}

void GenSwift::openWhile(const CiLoop * loop)
{
	if (needCondXcrement(loop))
		GenPySwift::openWhile(loop);
	else {
		write("while true");
		openChild();
		visitXcrement(loop->cond.get(), false, true);
		write("let ciDoLoop = ");
		loop->cond->accept(this, CiPriority::argument);
		writeNewLine();
		visitXcrement(loop->cond.get(), true, true);
		write("if !ciDoLoop");
		openChild();
		writeLine("break");
		closeChild();
	}
}

void GenSwift::writeForRange(const CiVar * iter, const CiBinaryExpr * cond, int64_t rangeStep)
{
	if (rangeStep == 1) {
		writeExpr(iter->value.get(), CiPriority::shift);
		switch (cond->op) {
		case CiToken::less:
			write("..<");
			cond->right->accept(this, CiPriority::shift);
			break;
		case CiToken::lessOrEqual:
			write("...");
			cond->right->accept(this, CiPriority::shift);
			break;
		default:
			std::abort();
		}
	}
	else {
		write("stride(from: ");
		writeExpr(iter->value.get(), CiPriority::argument);
		switch (cond->op) {
		case CiToken::less:
		case CiToken::greater:
			write(", to: ");
			writeExpr(cond->right.get(), CiPriority::argument);
			break;
		case CiToken::lessOrEqual:
		case CiToken::greaterOrEqual:
			write(", through: ");
			writeExpr(cond->right.get(), CiPriority::argument);
			break;
		default:
			std::abort();
		}
		write(", by: ");
		visitLiteralLong(rangeStep);
		writeChar(')');
	}
}

void GenSwift::visitForeach(const CiForeach * statement)
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
	const CiClassType * klass = static_cast<const CiClassType *>(statement->collection->type.get());
	switch (klass->class_->id) {
	case CiId::stringClass:
		writePostfix(statement->collection.get(), ".unicodeScalars");
		break;
	case CiId::sortedSetClass:
		writePostfix(statement->collection.get(), ".sorted()");
		break;
	case CiId::sortedDictionaryClass:
		writePostfix(statement->collection.get(), klass->getKeyType()->nullable ? ".sorted(by: { $0.key! < $1.key! })" : ".sorted(by: { $0.key < $1.key })");
		break;
	default:
		writeExpr(statement->collection.get(), CiPriority::argument);
		break;
	}
	writeChild(statement->body.get());
}

void GenSwift::visitLock(const CiLock * statement)
{
	statement->lock->accept(this, CiPriority::primary);
	writeLine(".lock()");
	write("do");
	openChild();
	write("defer { ");
	statement->lock->accept(this, CiPriority::primary);
	writeLine(".unlock() }");
	statement->body->acceptStatement(this);
	closeChild();
}

void GenSwift::writeResultVar()
{
	write("let result : ");
	writeType(this->currentMethod->type.get());
}

void GenSwift::writeSwitchCaseVar(const CiVar * def)
{
	if (def->name == "_")
		write("is ");
	else {
		write("let ");
		writeCamelCaseNotKeyword(def->name);
		write(" as ");
	}
	writeType(def->type.get());
}

void GenSwift::writeSwiftSwitchCaseBody(const CiSwitch * statement, const std::vector<std::shared_ptr<CiStatement>> * body)
{
	this->indent++;
	visitXcrement(statement->value.get(), true, true);
	initVarsAtIndent();
	writeSwitchCaseBody(body);
	this->indent--;
}

void GenSwift::visitSwitch(const CiSwitch * statement)
{
	visitXcrement(statement->value.get(), false, true);
	write("switch ");
	writeExpr(statement->value.get(), CiPriority::argument);
	writeLine(" {");
	for (const CiCase &kase : statement->cases) {
		write("case ");
		for (int i = 0; i < kase.values.size(); i++) {
			writeComma(i);
			const CiBinaryExpr * when1;
			if ((when1 = dynamic_cast<const CiBinaryExpr *>(kase.values[i].get())) && when1->op == CiToken::when) {
				if (const CiVar *whenVar = dynamic_cast<const CiVar *>(when1->left.get()))
					writeSwitchCaseVar(whenVar);
				else
					writeCoerced(statement->value->type.get(), when1->left.get(), CiPriority::argument);
				write(" where ");
				writeExpr(when1->right.get(), CiPriority::argument);
			}
			else if (const CiVar *def = dynamic_cast<const CiVar *>(kase.values[i].get()))
				writeSwitchCaseVar(def);
			else
				writeCoerced(statement->value->type.get(), kase.values[i].get(), CiPriority::argument);
		}
		writeCharLine(':');
		writeSwiftSwitchCaseBody(statement, &kase.body);
	}
	if (statement->defaultBody.size() > 0) {
		writeLine("default:");
		writeSwiftSwitchCaseBody(statement, &statement->defaultBody);
	}
	writeCharLine('}');
}

void GenSwift::visitThrow(const CiThrow * statement)
{
	this->throw_ = true;
	visitXcrement(statement->message.get(), false, true);
	write("throw CiError.error(");
	writeExpr(statement->message.get(), CiPriority::argument);
	writeCharLine(')');
}

void GenSwift::writeReadOnlyParameter(const CiVar * param)
{
	write("ciParam");
	writePascalCase(param->name);
}

void GenSwift::writeParameter(const CiVar * param)
{
	write("_ ");
	if (param->isAssigned)
		writeReadOnlyParameter(param);
	else
		writeName(param);
	write(" : ");
	writeType(param->type.get());
}

void GenSwift::visitEnumValue(const CiConst * konst, const CiConst * previous)
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

void GenSwift::writeEnum(const CiEnum * enu)
{
	writeNewLine();
	writeDoc(enu->documentation.get());
	writePublic(enu);
	if (dynamic_cast<const CiEnumFlags *>(enu)) {
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
		std::unordered_map<int, const CiConst *> valueToConst;
		for (const CiSymbol * symbol = enu->first; symbol != nullptr; symbol = symbol->next) {
			if (const CiConst *konst = dynamic_cast<const CiConst *>(symbol)) {
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
					if (!dynamic_cast<const CiImplicitEnumValue *>(konst->value.get())) {
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

void GenSwift::writeVisibility(CiVisibility visibility)
{
	switch (visibility) {
	case CiVisibility::private_:
		write("private ");
		break;
	case CiVisibility::internal:
		write("fileprivate ");
		break;
	case CiVisibility::protected_:
	case CiVisibility::public_:
		write("public ");
		break;
	default:
		std::abort();
	}
}

void GenSwift::writeConst(const CiConst * konst)
{
	writeNewLine();
	writeDoc(konst->documentation.get());
	writeVisibility(konst->visibility);
	write("static let ");
	writeName(konst);
	write(" = ");
	if (konst->type->id == CiId::intType || dynamic_cast<const CiEnum *>(konst->type.get()) || konst->type->id == CiId::stringPtrType)
		konst->value->accept(this, CiPriority::argument);
	else {
		writeType(konst->type.get());
		writeChar('(');
		konst->value->accept(this, CiPriority::argument);
		writeChar(')');
	}
	writeNewLine();
}

void GenSwift::writeField(const CiField * field)
{
	writeNewLine();
	writeDoc(field->documentation.get());
	writeVisibility(field->visibility);
	const CiClassType * klass;
	if ((klass = dynamic_cast<const CiClassType *>(field->type.get())) && klass->class_->id != CiId::stringClass && !dynamic_cast<const CiDynamicPtrType *>(klass) && !dynamic_cast<const CiStorageType *>(klass))
		write("unowned ");
	writeVar(field);
	if (field->value == nullptr && (dynamic_cast<const CiNumericType *>(field->type.get()) || dynamic_cast<const CiEnum *>(field->type.get()) || field->type->id == CiId::stringStorageType)) {
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

void GenSwift::writeParameterDoc(const CiVar * param, bool first)
{
	write("/// - parameter ");
	writeName(param);
	writeChar(' ');
	writeDocPara(&param->documentation->summary, false);
	writeNewLine();
}

void GenSwift::writeMethod(const CiMethod * method)
{
	writeNewLine();
	writeDoc(method->documentation.get());
	writeParametersDoc(method);
	switch (method->callType) {
	case CiCallType::static_:
		writeVisibility(method->visibility);
		write("static ");
		break;
	case CiCallType::normal:
		writeVisibility(method->visibility);
		break;
	case CiCallType::abstract:
	case CiCallType::virtual_:
		write(method->visibility == CiVisibility::internal ? "fileprivate " : "open ");
		break;
	case CiCallType::override_:
		write(method->visibility == CiVisibility::internal ? "fileprivate " : "open ");
		write("override ");
		break;
	case CiCallType::sealed:
		writeVisibility(method->visibility);
		write("final override ");
		break;
	}
	if (method->id == CiId::classToString)
		write("var description : String");
	else {
		write("func ");
		writeName(method);
		writeParameters(method, true);
		if (method->throws)
			write(" throws");
		if (method->type->id != CiId::voidType) {
			write(" -> ");
			writeType(method->type.get());
		}
	}
	writeNewLine();
	openBlock();
	if (method->callType == CiCallType::abstract)
		writeLine("preconditionFailure(\"Abstract method called\")");
	else {
		for (const CiVar * param = method->parameters.firstParameter(); param != nullptr; param = param->nextParameter()) {
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

void GenSwift::writeClass(const CiClass * klass, const CiProgram * program)
{
	writeNewLine();
	writeDoc(klass->documentation.get());
	writePublic(klass);
	if (klass->callType == CiCallType::sealed)
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
	if (this->throw_) {
		writeNewLine();
		writeLine("public enum CiError : Error");
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
		writeLine("fileprivate func ciStringCharAt(_ s: String, _ offset: Int) -> Int");
		openBlock();
		writeLine("return Int(s.unicodeScalars[s.index(s.startIndex, offsetBy: offset)].value)");
		closeBlock();
	}
	if (this->stringIndexOf) {
		writeNewLine();
		writeLine("fileprivate func ciStringIndexOf<S1 : StringProtocol, S2 : StringProtocol>(_ haystack: S1, _ needle: S2, _ options: String.CompareOptions = .literal) -> Int");
		openBlock();
		writeLine("guard let index = haystack.range(of: needle, options: options) else { return -1 }");
		writeLine("return haystack.distance(from: haystack.startIndex, to: index.lowerBound)");
		closeBlock();
	}
	if (this->stringSubstring) {
		writeNewLine();
		writeLine("fileprivate func ciStringSubstring(_ s: String, _ offset: Int) -> Substring");
		openBlock();
		writeLine("return s[s.index(s.startIndex, offsetBy: offset)...]");
		closeBlock();
	}
}

void GenSwift::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	if (resources->size() == 0)
		return;
	this->arrayRef = true;
	writeNewLine();
	writeLine("fileprivate final class CiResource");
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

void GenSwift::writeProgram(const CiProgram * program)
{
	this->system = program->system;
	this->throw_ = false;
	this->arrayRef = false;
	this->stringCharAt = false;
	this->stringIndexOf = false;
	this->stringSubstring = false;
	openStringWriter();
	writeTypes(program);
	createOutputFile();
	writeTopLevelNatives(program);
	writeIncludes("import ", "");
	closeStringWriter();
	writeLibrary();
	writeResources(&program->resources);
	closeFile();
}

std::string_view GenPy::getTargetName() const
{
	return "Python";
}

void GenPy::writeBanner()
{
	writeLine("# Generated automatically with \"cito\". Do not edit.");
}

void GenPy::startDocLine()
{
}

std::string_view GenPy::getDocBullet() const
{
	return " * ";
}

void GenPy::startDoc(const CiCodeDoc * doc)
{
	write("\"\"\"");
	writeDocPara(&doc->summary, false);
	if (doc->details.size() > 0) {
		writeNewLine();
		for (const std::shared_ptr<CiDocBlock> &block : doc->details) {
			writeNewLine();
			writeDocBlock(block.get(), false);
		}
	}
}

void GenPy::writeDoc(const CiCodeDoc * doc)
{
	if (doc != nullptr) {
		startDoc(doc);
		writeLine("\"\"\"");
	}
}

void GenPy::writeParameterDoc(const CiVar * param, bool first)
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

void GenPy::writePyDoc(const CiMethod * method)
{
	if (method->documentation == nullptr)
		return;
	startDoc(method->documentation.get());
	writeParametersDoc(method);
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
	else if (name == "and" || name == "array" || name == "as" || name == "async" || name == "await" || name == "def" || name == "del" || name == "elif" || name == "enum" || name == "except" || name == "finally" || name == "from" || name == "global" || name == "import" || name == "is" || name == "lambda" || name == "len" || name == "math" || name == "nonlocal" || name == "not" || name == "or" || name == "pass" || name == "pyfma" || name == "raise" || name == "re" || name == "sys" || name == "try" || name == "with" || name == "yield") {
		write(name);
		writeChar('_');
	}
	else
		writeLowercaseWithUnderscores(name);
}

void GenPy::writeName(const CiSymbol * symbol)
{
	if (const CiContainerType *container = dynamic_cast<const CiContainerType *>(symbol)) {
		if (!container->isPublic)
			writeChar('_');
		write(symbol->name);
	}
	else if (const CiConst *konst = dynamic_cast<const CiConst *>(symbol)) {
		if (konst->visibility != CiVisibility::public_)
			writeChar('_');
		if (konst->inMethod != nullptr) {
			writeUppercaseWithUnderscores(konst->inMethod->name);
			writeChar('_');
		}
		writeUppercaseWithUnderscores(symbol->name);
	}
	else if (dynamic_cast<const CiVar *>(symbol))
		writeNameNotKeyword(symbol->name);
	else if (const CiMember *member = dynamic_cast<const CiMember *>(symbol)) {
		if (member->id == CiId::classToString)
			write("__str__");
		else if (member->visibility == CiVisibility::public_)
			writeNameNotKeyword(symbol->name);
		else {
			writeChar('_');
			writeLowercaseWithUnderscores(symbol->name);
		}
	}
	else
		std::abort();
}

void GenPy::writeTypeAndName(const CiNamedValue * value)
{
	writeName(value);
}

void GenPy::writeLocalName(const CiSymbol * symbol, CiPriority parent)
{
	const CiForeach * forEach;
	if ((forEach = dynamic_cast<const CiForeach *>(symbol->parent)) && dynamic_cast<const CiStringType *>(forEach->collection->type.get())) {
		write("ord(");
		writeNameNotKeyword(symbol->name);
		writeChar(')');
	}
	else
		GenPySwift::writeLocalName(symbol, parent);
}

int GenPy::getArrayCode(const CiType * type)
{
	switch (type->id) {
	case CiId::sByteRange:
		return 'b';
	case CiId::byteRange:
		return 'B';
	case CiId::shortRange:
		return 'h';
	case CiId::uShortRange:
		return 'H';
	case CiId::intType:
		return 'i';
	case CiId::longType:
		return 'q';
	case CiId::floatType:
		return 'f';
	case CiId::doubleType:
		return 'd';
	default:
		std::abort();
	}
}

void GenPy::visitAggregateInitializer(const CiAggregateInitializer * expr)
{
	const CiArrayStorageType * array = static_cast<const CiArrayStorageType *>(expr->type.get());
	if (const CiNumericType *number = dynamic_cast<const CiNumericType *>(array->getElementType().get())) {
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

void GenPy::visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent)
{
	write("f\"");
	for (const CiInterpolatedPart &part : expr->parts) {
		writeDoubling(part.prefix, '{');
		writeChar('{');
		part.argument->accept(this, CiPriority::argument);
		writePyFormat(&part);
	}
	writeDoubling(expr->suffix, '{');
	writeChar('"');
}

void GenPy::visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent)
{
	if (expr->op == CiToken::exclamationMark) {
		if (parent > CiPriority::condAnd)
			writeChar('(');
		write("not ");
		expr->inner->accept(this, CiPriority::or_);
		if (parent > CiPriority::condAnd)
			writeChar(')');
	}
	else
		GenPySwift::visitPrefixExpr(expr, parent);
}

std::string_view GenPy::getReferenceEqOp(bool not_) const
{
	return not_ ? " is not " : " is ";
}

void GenPy::writeCharAt(const CiBinaryExpr * expr)
{
	write("ord(");
	writeIndexingExpr(expr, CiPriority::argument);
	writeChar(')');
}

void GenPy::writeStringLength(const CiExpr * expr)
{
	writeCall("len", expr);
}

void GenPy::visitSymbolReference(const CiSymbolReference * expr, CiPriority parent)
{
	switch (expr->symbol->id) {
	case CiId::consoleError:
		include("sys");
		write("sys.stderr");
		break;
	case CiId::listCount:
	case CiId::queueCount:
	case CiId::stackCount:
	case CiId::hashSetCount:
	case CiId::sortedSetCount:
	case CiId::dictionaryCount:
	case CiId::sortedDictionaryCount:
	case CiId::orderedDictionaryCount:
		writeStringLength(expr->left.get());
		break;
	case CiId::mathNaN:
		include("math");
		write("math.nan");
		break;
	case CiId::mathNegativeInfinity:
		include("math");
		write("-math.inf");
		break;
	case CiId::mathPositiveInfinity:
		include("math");
		write("math.inf");
		break;
	default:
		if (!writeJavaMatchProperty(expr, parent))
			GenBase::visitSymbolReference(expr, parent);
		break;
	}
}

void GenPy::visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent)
{
	switch (expr->op) {
	case CiToken::slash:
		if (dynamic_cast<const CiIntegerType *>(expr->type.get())) {
			bool floorDiv;
			const CiRangeType * leftRange;
			const CiRangeType * rightRange;
			if ((leftRange = dynamic_cast<const CiRangeType *>(expr->left.get())) && leftRange->min >= 0 && (rightRange = dynamic_cast<const CiRangeType *>(expr->right.get())) && rightRange->min >= 0) {
				if (parent > CiPriority::or_)
					writeChar('(');
				floorDiv = true;
			}
			else {
				write("int(");
				floorDiv = false;
			}
			expr->left->accept(this, CiPriority::mul);
			write(floorDiv ? " // " : " / ");
			expr->right->accept(this, CiPriority::primary);
			if (!floorDiv || parent > CiPriority::or_)
				writeChar(')');
		}
		else
			GenBase::visitBinaryExpr(expr, parent);
		break;
	case CiToken::condAnd:
		writeBinaryExpr(expr, parent > CiPriority::condAnd || parent == CiPriority::condOr, CiPriority::condAnd, " and ", CiPriority::condAnd);
		break;
	case CiToken::condOr:
		writeBinaryExpr2(expr, parent, CiPriority::condOr, " or ");
		break;
	case CiToken::assign:
		if (this->atLineStart) {
			const CiBinaryExpr * rightBinary;
			for (const CiExpr * right = expr->right.get(); (rightBinary = dynamic_cast<const CiBinaryExpr *>(right)) && rightBinary->isAssign(); right = rightBinary->right.get()) {
				if (rightBinary->op != CiToken::assign) {
					visitBinaryExpr(rightBinary, CiPriority::statement);
					writeNewLine();
					break;
				}
			}
		}
		expr->left->accept(this, CiPriority::assign);
		write(" = ");
		{
			const CiBinaryExpr * rightBinary;
			((rightBinary = dynamic_cast<const CiBinaryExpr *>(expr->right.get())) && rightBinary->isAssign() && rightBinary->op != CiToken::assign ? rightBinary->left : expr->right)->accept(this, CiPriority::assign);
		}
		break;
	case CiToken::addAssign:
	case CiToken::subAssign:
	case CiToken::mulAssign:
	case CiToken::divAssign:
	case CiToken::modAssign:
	case CiToken::shiftLeftAssign:
	case CiToken::shiftRightAssign:
	case CiToken::andAssign:
	case CiToken::orAssign:
	case CiToken::xorAssign:
		{
			const CiExpr * right = expr->right.get();
			const CiBinaryExpr * rightBinary;
			if ((rightBinary = dynamic_cast<const CiBinaryExpr *>(right)) && rightBinary->isAssign()) {
				visitBinaryExpr(rightBinary, CiPriority::statement);
				writeNewLine();
				right = rightBinary->left.get();
			}
			expr->left->accept(this, CiPriority::assign);
			writeChar(' ');
			if (expr->op == CiToken::divAssign && dynamic_cast<const CiIntegerType *>(expr->type.get()))
				writeChar('/');
			write(expr->getOpString());
			writeChar(' ');
			right->accept(this, CiPriority::argument);
		}
		break;
	case CiToken::is:
		if (const CiSymbolReference *symbol = dynamic_cast<const CiSymbolReference *>(expr->right.get())) {
			write("isinstance(");
			expr->left->accept(this, CiPriority::argument);
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

void GenPy::writeCoercedSelect(const CiType * type, const CiSelectExpr * expr, CiPriority parent)
{
	if (parent > CiPriority::select)
		writeChar('(');
	writeCoerced(type, expr->onTrue.get(), CiPriority::select);
	write(" if ");
	expr->cond->accept(this, CiPriority::selectCond);
	write(" else ");
	writeCoerced(type, expr->onFalse.get(), CiPriority::select);
	if (parent > CiPriority::select)
		writeChar(')');
}

void GenPy::writeDefaultValue(const CiType * type)
{
	if (dynamic_cast<const CiNumericType *>(type))
		writeChar('0');
	else if (type->id == CiId::boolType)
		write("False");
	else if (type->id == CiId::stringStorageType)
		write("\"\"");
	else
		write("None");
}

void GenPy::writePyNewArray(const CiType * elementType, const CiExpr * value, const CiExpr * lengthExpr)
{
	if (dynamic_cast<const CiStorageType *>(elementType)) {
		write("[ ");
		writeNewStorage(elementType);
		write(" for _ in range(");
		lengthExpr->accept(this, CiPriority::argument);
		write(") ]");
	}
	else if (dynamic_cast<const CiNumericType *>(elementType)) {
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
				value->accept(this, CiPriority::argument);
			write(" ]) * ");
			lengthExpr->accept(this, CiPriority::mul);
		}
	}
	else {
		write("[ ");
		if (value == nullptr)
			writeDefaultValue(elementType);
		else
			value->accept(this, CiPriority::argument);
		write(" ] * ");
		lengthExpr->accept(this, CiPriority::mul);
	}
}

void GenPy::writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent)
{
	writePyNewArray(elementType, nullptr, lengthExpr);
}

void GenPy::writeArrayStorageInit(const CiArrayStorageType * array, const CiExpr * value)
{
	write(" = ");
	writePyNewArray(array->getElementType().get(), nullptr, array->lengthExpr.get());
}

void GenPy::writeNew(const CiReadWriteClassType * klass, CiPriority parent)
{
	switch (klass->class_->id) {
	case CiId::listClass:
	case CiId::stackClass:
		if (const CiNumericType *number = dynamic_cast<const CiNumericType *>(klass->getElementType().get())) {
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
	case CiId::queueClass:
		include("collections");
		write("collections.deque()");
		break;
	case CiId::hashSetClass:
	case CiId::sortedSetClass:
		write("set()");
		break;
	case CiId::dictionaryClass:
	case CiId::sortedDictionaryClass:
		write("{}");
		break;
	case CiId::orderedDictionaryClass:
		include("collections");
		write("collections.OrderedDict()");
		break;
	case CiId::stringWriterClass:
		include("io");
		write("io.StringIO()");
		break;
	case CiId::lockClass:
		include("threading");
		write("threading.RLock()");
		break;
	default:
		writeName(klass->class_);
		write("()");
		break;
	}
}

void GenPy::writeContains(const CiExpr * haystack, const CiExpr * needle)
{
	needle->accept(this, CiPriority::rel);
	write(" in ");
	haystack->accept(this, CiPriority::rel);
}

void GenPy::writeSlice(const CiExpr * startIndex, const CiExpr * length)
{
	writeChar('[');
	startIndex->accept(this, CiPriority::argument);
	writeChar(':');
	if (length != nullptr)
		writeAdd(startIndex, length);
	writeChar(']');
}

void GenPy::writeAssignSorted(const CiExpr * obj, std::string_view byteArray)
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

void GenPy::writeAllAny(std::string_view function, const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args)
{
	write(function);
	writeChar('(');
	const CiLambdaExpr * lambda = static_cast<const CiLambdaExpr *>((*args)[0].get());
	lambda->body->accept(this, CiPriority::argument);
	write(" for ");
	writeName(lambda->first);
	write(" in ");
	obj->accept(this, CiPriority::argument);
	writeChar(')');
}

void GenPy::writePyRegexOptions(const std::vector<std::shared_ptr<CiExpr>> * args)
{
	include("re");
	writeRegexOptions(args, ", ", " | ", "", "re.I", "re.M", "re.S");
}

void GenPy::writeRegexSearch(const std::vector<std::shared_ptr<CiExpr>> * args)
{
	write("re.search(");
	(*args)[1]->accept(this, CiPriority::argument);
	write(", ");
	(*args)[0]->accept(this, CiPriority::argument);
	writePyRegexOptions(args);
	writeChar(')');
}

void GenPy::writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent)
{
	switch (method->id) {
	case CiId::enumFromInt:
		writeName(method->type.get());
		writeArgsInParentheses(method, args);
		break;
	case CiId::enumHasFlag:
	case CiId::stringContains:
	case CiId::listContains:
	case CiId::hashSetContains:
	case CiId::sortedSetContains:
	case CiId::dictionaryContainsKey:
	case CiId::sortedDictionaryContainsKey:
	case CiId::orderedDictionaryContainsKey:
		writeContains(obj, (*args)[0].get());
		break;
	case CiId::stringEndsWith:
		writeMethodCall(obj, "endswith", (*args)[0].get());
		break;
	case CiId::stringIndexOf:
		writeMethodCall(obj, "find", (*args)[0].get());
		break;
	case CiId::stringLastIndexOf:
		writeMethodCall(obj, "rfind", (*args)[0].get());
		break;
	case CiId::stringStartsWith:
		writeMethodCall(obj, "startswith", (*args)[0].get());
		break;
	case CiId::stringSubstring:
		obj->accept(this, CiPriority::primary);
		writeSlice((*args)[0].get(), args->size() == 2 ? (*args)[1].get() : nullptr);
		break;
	case CiId::arrayBinarySearchAll:
		include("bisect");
		writeCall("bisect.bisect_left", obj, (*args)[0].get());
		break;
	case CiId::arrayBinarySearchPart:
		include("bisect");
		write("bisect.bisect_left(");
		obj->accept(this, CiPriority::argument);
		write(", ");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", ");
		(*args)[1]->accept(this, CiPriority::argument);
		write(", ");
		(*args)[2]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::arrayCopyTo:
	case CiId::listCopyTo:
		(*args)[1]->accept(this, CiPriority::primary);
		writeSlice((*args)[2].get(), (*args)[3].get());
		write(" = ");
		obj->accept(this, CiPriority::primary);
		writeSlice((*args)[0].get(), (*args)[3].get());
		break;
	case CiId::arrayFillAll:
	case CiId::arrayFillPart:
		obj->accept(this, CiPriority::primary);
		if (args->size() == 1) {
			write("[:] = ");
			const CiArrayStorageType * array = static_cast<const CiArrayStorageType *>(obj->type.get());
			writePyNewArray(array->getElementType().get(), (*args)[0].get(), array->lengthExpr.get());
		}
		else {
			writeSlice((*args)[1].get(), (*args)[2].get());
			write(" = ");
			writePyNewArray(obj->type->asClassType()->getElementType().get(), (*args)[0].get(), (*args)[2].get());
		}
		break;
	case CiId::arraySortAll:
	case CiId::listSortAll:
		obj->accept(this, CiPriority::assign);
		writeAssignSorted(obj, "bytearray");
		obj->accept(this, CiPriority::argument);
		write("))");
		break;
	case CiId::arraySortPart:
	case CiId::listSortPart:
		obj->accept(this, CiPriority::primary);
		writeSlice((*args)[0].get(), (*args)[1].get());
		writeAssignSorted(obj, "bytes");
		obj->accept(this, CiPriority::primary);
		writeSlice((*args)[0].get(), (*args)[1].get());
		write("))");
		break;
	case CiId::listAdd:
		writeListAdd(obj, "append", args);
		break;
	case CiId::listAddRange:
		obj->accept(this, CiPriority::assign);
		write(" += ");
		(*args)[0]->accept(this, CiPriority::argument);
		break;
	case CiId::listAll:
		writeAllAny("all", obj, args);
		break;
	case CiId::listAny:
		writeAllAny("any", obj, args);
		break;
	case CiId::listClear:
	case CiId::stackClear:
		{
			const CiNumericType * number;
			if ((number = dynamic_cast<const CiNumericType *>(obj->type->asClassType()->getElementType().get())) && getArrayCode(number) != 'B') {
				write("del ");
				writePostfix(obj, "[:]");
			}
			else
				writePostfix(obj, ".clear()");
			break;
		}
	case CiId::listIndexOf:
		if (parent > CiPriority::select)
			writeChar('(');
		writeMethodCall(obj, "index", (*args)[0].get());
		write(" if ");
		writeContains(obj, (*args)[0].get());
		write(" else -1");
		if (parent > CiPriority::select)
			writeChar(')');
		break;
	case CiId::listInsert:
		writeListInsert(obj, "insert", args);
		break;
	case CiId::listLast:
	case CiId::stackPeek:
		writePostfix(obj, "[-1]");
		break;
	case CiId::listRemoveAt:
	case CiId::dictionaryRemove:
	case CiId::sortedDictionaryRemove:
	case CiId::orderedDictionaryRemove:
		write("del ");
		writeIndexing(obj, (*args)[0].get());
		break;
	case CiId::listRemoveRange:
		write("del ");
		obj->accept(this, CiPriority::primary);
		writeSlice((*args)[0].get(), (*args)[1].get());
		break;
	case CiId::queueDequeue:
		writePostfix(obj, ".popleft()");
		break;
	case CiId::queueEnqueue:
	case CiId::stackPush:
		writeListAppend(obj, args);
		break;
	case CiId::queuePeek:
		writePostfix(obj, "[0]");
		break;
	case CiId::dictionaryAdd:
		writeDictionaryAdd(obj, args);
		break;
	case CiId::textWriterWrite:
		write("print(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", end=\"\", file=");
		obj->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::textWriterWriteChar:
		writeMethodCall(obj, "write(chr", (*args)[0].get());
		writeChar(')');
		break;
	case CiId::textWriterWriteLine:
		write("print(");
		if (args->size() == 1) {
			(*args)[0]->accept(this, CiPriority::argument);
			write(", ");
		}
		write("file=");
		obj->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::consoleWrite:
		write("print(");
		(*args)[0]->accept(this, CiPriority::argument);
		write(", end=\"\")");
		break;
	case CiId::consoleWriteLine:
		write("print(");
		if (args->size() == 1)
			(*args)[0]->accept(this, CiPriority::argument);
		writeChar(')');
		break;
	case CiId::stringWriterClear:
		writePostfix(obj, ".seek(0)");
		writeNewLine();
		writePostfix(obj, ".truncate(0)");
		break;
	case CiId::stringWriterToString:
		writePostfix(obj, ".getvalue()");
		break;
	case CiId::uTF8GetByteCount:
		write("len(");
		writePostfix((*args)[0].get(), ".encode(\"utf8\"))");
		break;
	case CiId::uTF8GetBytes:
		write("cibytes = ");
		(*args)[0]->accept(this, CiPriority::primary);
		writeLine(".encode(\"utf8\")");
		(*args)[1]->accept(this, CiPriority::primary);
		writeChar('[');
		(*args)[2]->accept(this, CiPriority::argument);
		writeChar(':');
		startAdd((*args)[2].get());
		writeLine("len(cibytes)] = cibytes");
		break;
	case CiId::uTF8GetString:
		(*args)[0]->accept(this, CiPriority::primary);
		writeSlice((*args)[1].get(), (*args)[2].get());
		write(".decode(\"utf8\")");
		break;
	case CiId::environmentGetEnvironmentVariable:
		include("os");
		writeCall("os.getenv", (*args)[0].get());
		break;
	case CiId::regexCompile:
		write("re.compile(");
		(*args)[0]->accept(this, CiPriority::argument);
		writePyRegexOptions(args);
		writeChar(')');
		break;
	case CiId::regexEscape:
		include("re");
		writeCall("re.escape", (*args)[0].get());
		break;
	case CiId::regexIsMatchStr:
		if (parent > CiPriority::equality)
			writeChar('(');
		writeRegexSearch(args);
		write(" is not None");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::regexIsMatchRegex:
		if (parent > CiPriority::equality)
			writeChar('(');
		writeMethodCall(obj, "search", (*args)[0].get());
		write(" is not None");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::matchFindStr:
	case CiId::matchFindRegex:
		if (parent > CiPriority::equality)
			writeChar('(');
		obj->accept(this, CiPriority::equality);
		write(" is not None");
		if (parent > CiPriority::equality)
			writeChar(')');
		break;
	case CiId::matchGetCapture:
		writeMethodCall(obj, "group", (*args)[0].get());
		break;
	case CiId::mathMethod:
	case CiId::mathIsFinite:
	case CiId::mathIsNaN:
	case CiId::mathLog2:
		include("math");
		write("math.");
		writeLowercase(method->name);
		writeArgsInParentheses(method, args);
		break;
	case CiId::mathAbs:
		writeCall("abs", (*args)[0].get());
		break;
	case CiId::mathCeiling:
		include("math");
		writeCall("math.ceil", (*args)[0].get());
		break;
	case CiId::mathClamp:
		write("min(max(");
		writeClampAsMinMax(args);
		break;
	case CiId::mathFusedMultiplyAdd:
		include("pyfma");
		writeCall("pyfma.fma", (*args)[0].get(), (*args)[1].get(), (*args)[2].get());
		break;
	case CiId::mathIsInfinity:
		include("math");
		writeCall("math.isinf", (*args)[0].get());
		break;
	case CiId::mathMaxInt:
	case CiId::mathMaxDouble:
		writeCall("max", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::mathMinInt:
	case CiId::mathMinDouble:
		writeCall("min", (*args)[0].get(), (*args)[1].get());
		break;
	case CiId::mathRound:
		writeCall("round", (*args)[0].get());
		break;
	case CiId::mathTruncate:
		include("math");
		writeCall("math.trunc", (*args)[0].get());
		break;
	default:
		if (obj == nullptr)
			writeLocalName(method, CiPriority::primary);
		else if (isReferenceTo(obj, CiId::basePtr)) {
			writeName(method->parent);
			writeChar('.');
			writeName(method);
			write("(self");
			if (args->size() > 0) {
				write(", ");
				writeArgs(method, args);
			}
			writeChar(')');
			break;
		}
		else {
			obj->accept(this, CiPriority::primary);
			writeChar('.');
			writeName(method);
		}
		writeArgsInParentheses(method, args);
		break;
	}
}

void GenPy::writeResource(std::string_view name, int length)
{
	write("_CiResource.");
	writeResourceName(name);
}

bool GenPy::visitPreCall(const CiCallExpr * call)
{
	switch (call->method->symbol->id) {
	case CiId::matchFindStr:
		call->method->left->accept(this, CiPriority::assign);
		write(" = ");
		writeRegexSearch(&call->arguments);
		writeNewLine();
		return true;
	case CiId::matchFindRegex:
		call->method->left->accept(this, CiPriority::assign);
		write(" = ");
		writeMethodCall(call->arguments[1].get(), "search", call->arguments[0].get());
		writeNewLine();
		return true;
	default:
		return false;
	}
}

void GenPy::startTemporaryVar(const CiType * type)
{
}

bool GenPy::hasInitCode(const CiNamedValue * def) const
{
	return (def->value != nullptr || def->type->isFinal()) && !def->isAssignableStorage();
}

void GenPy::visitExpr(const CiExpr * statement)
{
	const CiVar * def;
	if (!(def = dynamic_cast<const CiVar *>(statement)) || hasInitCode(def)) {
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

void GenPy::visitLambdaExpr(const CiLambdaExpr * expr)
{
	std::abort();
}

void GenPy::writeAssertCast(const CiBinaryExpr * expr)
{
	const CiVar * def = static_cast<const CiVar *>(expr->right.get());
	write(def->name);
	write(" = ");
	expr->left->accept(this, CiPriority::argument);
	writeNewLine();
}

void GenPy::writeAssert(const CiAssert * statement)
{
	write("assert ");
	statement->cond->accept(this, CiPriority::argument);
	if (statement->message != nullptr) {
		write(", ");
		statement->message->accept(this, CiPriority::argument);
	}
	writeNewLine();
}

void GenPy::visitBreak(const CiBreak * statement)
{
	writeLine(dynamic_cast<const CiSwitch *>(statement->loopOrSwitch) ? "raise _CiBreak()" : "break");
}

std::string_view GenPy::getIfNot() const
{
	return "if not ";
}

void GenPy::writeInclusiveLimit(const CiExpr * limit, int increment, std::string_view incrementString)
{
	if (const CiLiteralLong *literal = dynamic_cast<const CiLiteralLong *>(limit))
		visitLiteralLong(literal->value + increment);
	else {
		limit->accept(this, CiPriority::add);
		write(incrementString);
	}
}

void GenPy::writeForRange(const CiVar * iter, const CiBinaryExpr * cond, int64_t rangeStep)
{
	write("range(");
	if (rangeStep != 1 || !iter->value->isLiteralZero()) {
		iter->value->accept(this, CiPriority::argument);
		write(", ");
	}
	switch (cond->op) {
	case CiToken::less:
	case CiToken::greater:
		cond->right->accept(this, CiPriority::argument);
		break;
	case CiToken::lessOrEqual:
		writeInclusiveLimit(cond->right.get(), 1, " + 1");
		break;
	case CiToken::greaterOrEqual:
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

void GenPy::visitForeach(const CiForeach * statement)
{
	write("for ");
	writeName(statement->getVar());
	const CiClassType * klass = static_cast<const CiClassType *>(statement->collection->type.get());
	if (klass->class_->typeParameterCount == 2) {
		write(", ");
		writeName(statement->getValueVar());
		write(" in ");
		if (klass->class_->id == CiId::sortedDictionaryClass) {
			write("sorted(");
			writePostfix(statement->collection.get(), ".items())");
		}
		else
			writePostfix(statement->collection.get(), ".items()");
	}
	else {
		write(" in ");
		if (klass->class_->id == CiId::sortedSetClass)
			writeCall("sorted", statement->collection.get());
		else
			statement->collection->accept(this, CiPriority::argument);
	}
	writeChild(statement->body.get());
}

void GenPy::writeElseIf()
{
	write("el");
}

void GenPy::visitLock(const CiLock * statement)
{
	visitXcrement(statement->lock.get(), false, true);
	write("with ");
	statement->lock->accept(this, CiPriority::argument);
	openChild();
	visitXcrement(statement->lock.get(), true, true);
	statement->body->acceptStatement(this);
	closeChild();
}

void GenPy::writeResultVar()
{
	write("result");
}

void GenPy::writeSwitchCaseVar(const CiVar * def)
{
	writeName(def->type->asClassType()->class_);
	write("()");
	if (def->name != "_") {
		write(" as ");
		writeNameNotKeyword(def->name);
	}
}

void GenPy::writePyCaseBody(const CiSwitch * statement, const std::vector<std::shared_ptr<CiStatement>> * body)
{
	openChild();
	visitXcrement(statement->value.get(), true, true);
	writeFirstStatements(body, CiSwitch::lengthWithoutTrailingBreak(body));
	closeChild();
}

void GenPy::visitSwitch(const CiSwitch * statement)
{
	bool earlyBreak = std::any_of(statement->cases.begin(), statement->cases.end(), [](const CiCase &kase) { return CiSwitch::hasEarlyBreak(&kase.body); }) || CiSwitch::hasEarlyBreak(&statement->defaultBody);
	if (earlyBreak) {
		this->switchBreak = true;
		write("try");
		openChild();
	}
	visitXcrement(statement->value.get(), false, true);
	write("match ");
	statement->value->accept(this, CiPriority::argument);
	openChild();
	for (const CiCase &kase : statement->cases) {
		std::string_view op = "case ";
		for (const std::shared_ptr<CiExpr> &caseValue : kase.values) {
			write(op);
			if (const CiVar *def = dynamic_cast<const CiVar *>(caseValue.get()))
				writeSwitchCaseVar(def);
			else if (const CiBinaryExpr *when1 = dynamic_cast<const CiBinaryExpr *>(caseValue.get())) {
				if (const CiVar *whenVar = dynamic_cast<const CiVar *>(when1->left.get()))
					writeSwitchCaseVar(whenVar);
				else
					when1->left->accept(this, CiPriority::argument);
				write(" if ");
				when1->right->accept(this, CiPriority::argument);
			}
			else
				caseValue->accept(this, CiPriority::or_);
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

void GenPy::visitThrow(const CiThrow * statement)
{
	visitXcrement(statement->message.get(), false, true);
	write("raise Exception(");
	statement->message->accept(this, CiPriority::argument);
	writeCharLine(')');
}

void GenPy::visitEnumValue(const CiConst * konst, const CiConst * previous)
{
	writeUppercaseWithUnderscores(konst->name);
	write(" = ");
	visitLiteralLong(konst->value->intValue());
	writeNewLine();
	writeDoc(konst->documentation.get());
}

void GenPy::writeEnum(const CiEnum * enu)
{
	include("enum");
	writeNewLine();
	write("class ");
	writeName(enu);
	write(dynamic_cast<const CiEnumFlags *>(enu) ? "(enum.Flag)" : "(enum.Enum)");
	openChild();
	writeDoc(enu->documentation.get());
	enu->acceptValues(this);
	closeChild();
}

void GenPy::writeConst(const CiConst * konst)
{
	if (konst->visibility != CiVisibility::private_ || dynamic_cast<const CiArrayStorageType *>(konst->type.get())) {
		writeNewLine();
		writeName(konst);
		write(" = ");
		konst->value->accept(this, CiPriority::argument);
		writeNewLine();
		writeDoc(konst->documentation.get());
	}
}

void GenPy::writeField(const CiField * field)
{
}

void GenPy::writeMethod(const CiMethod * method)
{
	if (method->callType == CiCallType::abstract)
		return;
	writeNewLine();
	if (method->callType == CiCallType::static_)
		writeLine("@staticmethod");
	write("def ");
	writeName(method);
	if (method->callType == CiCallType::static_)
		writeParameters(method, true);
	else {
		write("(self");
		writeRemainingParameters(method, false, true);
	}
	this->currentMethod = method;
	openChild();
	writePyDoc(method);
	method->body->acceptStatement(this);
	closeChild();
	this->currentMethod = nullptr;
}

bool GenPy::inheritsConstructor(const CiClass * klass) const
{
	while (const CiClass *baseClass = dynamic_cast<const CiClass *>(klass->parent)) {
		if (needsConstructor(baseClass))
			return true;
		klass = baseClass;
	}
	return false;
}

void GenPy::writeInitField(const CiField * field)
{
	if (hasInitCode(field)) {
		write("self.");
		writeVar(field);
		writeNewLine();
		writeInitCode(field);
	}
}

void GenPy::writeClass(const CiClass * klass, const CiProgram * program)
{
	if (!writeBaseClass(klass, program))
		return;
	writeNewLine();
	write("class ");
	writeName(klass);
	if (const CiClass *baseClass = dynamic_cast<const CiClass *>(klass->parent)) {
		writeChar('(');
		writeName(baseClass);
		writeChar(')');
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
}

void GenPy::writeResourceByte(int b)
{
	write(std::format("\\x{:02x}", b));
}

void GenPy::writeResources(const std::map<std::string, std::vector<uint8_t>> * resources)
{
	if (resources->size() == 0)
		return;
	writeNewLine();
	write("class _CiResource");
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

void GenPy::writeProgram(const CiProgram * program)
{
	this->switchBreak = false;
	openStringWriter();
	writeTypes(program);
	createOutputFile();
	writeTopLevelNatives(program);
	writeIncludes("import ", "");
	if (this->switchBreak) {
		writeNewLine();
		writeLine("class _CiBreak(Exception): pass");
	}
	closeStringWriter();
	writeResources(&program->resources);
	closeFile();
}
