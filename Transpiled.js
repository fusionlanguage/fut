// Generated automatically with "cito". Do not edit.

export const RegexOptions = {
	NONE : 0,
	IGNORE_CASE : 1,
	MULTILINE : 2,
	SINGLELINE : 16
}

export const CiToken = {
	END_OF_FILE : 0,
	ID : 1,
	LITERAL_LONG : 2,
	LITERAL_DOUBLE : 3,
	LITERAL_CHAR : 4,
	LITERAL_STRING : 5,
	INTERPOLATED_STRING : 6,
	SEMICOLON : 7,
	DOT : 8,
	COMMA : 9,
	LEFT_PARENTHESIS : 10,
	RIGHT_PARENTHESIS : 11,
	LEFT_BRACKET : 12,
	RIGHT_BRACKET : 13,
	LEFT_BRACE : 14,
	RIGHT_BRACE : 15,
	PLUS : 16,
	MINUS : 17,
	ASTERISK : 18,
	SLASH : 19,
	MOD : 20,
	AND : 21,
	OR : 22,
	XOR : 23,
	TILDE : 24,
	SHIFT_LEFT : 25,
	SHIFT_RIGHT : 26,
	EQUAL : 27,
	NOT_EQUAL : 28,
	LESS : 29,
	LESS_OR_EQUAL : 30,
	GREATER : 31,
	GREATER_OR_EQUAL : 32,
	RIGHT_ANGLE : 33,
	COND_AND : 34,
	COND_OR : 35,
	EXCLAMATION_MARK : 36,
	HASH : 37,
	ASSIGN : 38,
	ADD_ASSIGN : 39,
	SUB_ASSIGN : 40,
	MUL_ASSIGN : 41,
	DIV_ASSIGN : 42,
	MOD_ASSIGN : 43,
	AND_ASSIGN : 44,
	OR_ASSIGN : 45,
	XOR_ASSIGN : 46,
	SHIFT_LEFT_ASSIGN : 47,
	SHIFT_RIGHT_ASSIGN : 48,
	INCREMENT : 49,
	DECREMENT : 50,
	QUESTION_MARK : 51,
	COLON : 52,
	FAT_ARROW : 53,
	RANGE : 54,
	DOC_REGULAR : 55,
	DOC_BULLET : 56,
	DOC_BLANK : 57,
	ABSTRACT : 58,
	ASSERT : 59,
	BREAK : 60,
	CASE : 61,
	CLASS : 62,
	CONST : 63,
	CONTINUE : 64,
	DEFAULT : 65,
	DO : 66,
	ELSE : 67,
	ENUM : 68,
	FALSE : 69,
	FOR : 70,
	FOREACH : 71,
	IF : 72,
	IN : 73,
	INTERNAL : 74,
	IS : 75,
	LOCK_ : 76,
	NATIVE : 77,
	NEW : 78,
	NULL : 79,
	OVERRIDE : 80,
	PROTECTED : 81,
	PUBLIC : 82,
	RESOURCE : 83,
	RETURN : 84,
	SEALED : 85,
	STATIC : 86,
	SWITCH : 87,
	THROW : 88,
	THROWS : 89,
	TRUE : 90,
	VIRTUAL : 91,
	VOID : 92,
	WHEN : 93,
	WHILE : 94,
	END_OF_LINE : 95,
	PRE_IF : 96,
	PRE_EL_IF : 97,
	PRE_ELSE : 98,
	PRE_END_IF : 99
}

const CiPreState = {
	NOT_YET : 0,
	ALREADY : 1,
	ALREADY_ELSE : 2
}

export class CiLexer
{
	input;
	#inputLength;
	#nextOffset;
	charOffset;
	#nextChar;
	filename;
	line;
	column;
	tokenColumn;
	lexemeOffset;
	currentToken;
	longValue;
	stringValue;
	#preSymbols = new Set();
	#atLineStart = true;
	#lineMode = false;
	#enableDocComments = true;
	parsingTypeArg = false;
	#preElseStack = [];

	addPreSymbol(symbol)
	{
		this.#preSymbols.add(symbol);
	}

	open(filename, input, inputLength)
	{
		this.filename = filename;
		this.input = input;
		this.#inputLength = inputLength;
		this.#nextOffset = 0;
		this.line = 1;
		this.column = 1;
		this.#fillNextChar();
		if (this.#nextChar == 65279)
			this.#fillNextChar();
		this.nextToken();
	}

	#readByte()
	{
		if (this.#nextOffset >= this.#inputLength)
			return -1;
		return this.input[this.#nextOffset++];
	}

	#readContinuationByte(hi)
	{
		let b = this.#readByte();
		if (hi != 65533) {
			if (b >= 128 && b <= 191)
				return (hi << 6) + b - 128;
			this.reportError("Invalid UTF-8");
		}
		return 65533;
	}

	#fillNextChar()
	{
		this.charOffset = this.#nextOffset;
		let b = this.#readByte();
		if (b >= 128) {
			if (b < 194 || b > 244) {
				this.reportError("Invalid UTF-8");
				b = 65533;
			}
			else if (b < 224)
				b = this.#readContinuationByte(b - 192);
			else if (b < 240) {
				b = this.#readContinuationByte(b - 224);
				b = this.#readContinuationByte(b);
			}
			else {
				b = this.#readContinuationByte(b - 240);
				b = this.#readContinuationByte(b);
				b = this.#readContinuationByte(b);
			}
		}
		this.#nextChar = b;
	}

	peekChar()
	{
		return this.#nextChar;
	}

	static isLetterOrDigit(c)
	{
		if (c >= 97 && c <= 122)
			return true;
		if (c >= 65 && c <= 90)
			return true;
		if (c >= 48 && c <= 57)
			return true;
		return c == 95;
	}

	readChar()
	{
		let c = this.#nextChar;
		switch (c) {
		case 9:
		case 32:
			this.column++;
			break;
		case 10:
			this.line++;
			this.column = 1;
			this.#atLineStart = true;
			break;
		default:
			this.column++;
			this.#atLineStart = false;
			break;
		}
		this.#fillNextChar();
		return c;
	}

	#eatChar(c)
	{
		if (this.peekChar() == c) {
			this.readChar();
			return true;
		}
		return false;
	}

	#skipWhitespace()
	{
		while (this.peekChar() == 9 || this.peekChar() == 32 || this.peekChar() == 13)
			this.readChar();
	}

	#readIntegerLiteral(bits)
	{
		let invalidDigit = false;
		let tooBig = false;
		let needDigit = true;
		for (let i = 0n;; this.readChar()) {
			let c = this.peekChar();
			if (c >= 48 && c <= 57)
				c -= 48;
			else if (c >= 65 && c <= 90)
				c -= 55;
			else if (c >= 97 && c <= 122)
				c -= 87;
			else if (c == 95) {
				needDigit = true;
				continue;
			}
			else {
				this.longValue = i;
				if (invalidDigit || needDigit)
					this.reportError("Invalid integer");
				else if (tooBig)
					this.reportError("Integer too big");
				return CiToken.LITERAL_LONG;
			}
			if (c >= 1 << bits)
				invalidDigit = true;
			else if (i >> BigInt(64 - bits) != 0)
				tooBig = true;
			else
				i = (i << BigInt(bits)) + BigInt(c);
			needDigit = false;
		}
	}

	#readFloatLiteral(needDigit)
	{
		let underscoreE = false;
		let exponent = false;
		for (;;) {
			let c = this.peekChar();
			switch (c) {
			case 48:
			case 49:
			case 50:
			case 51:
			case 52:
			case 53:
			case 54:
			case 55:
			case 56:
			case 57:
				this.readChar();
				needDigit = false;
				break;
			case 69:
			case 101:
				if (exponent) {
					this.reportError("Invalid floating-point number");
					return CiToken.LITERAL_DOUBLE;
				}
				if (needDigit)
					underscoreE = true;
				this.readChar();
				c = this.peekChar();
				if (c == 43 || c == 45)
					this.readChar();
				exponent = true;
				needDigit = true;
				break;
			case 95:
				this.readChar();
				needDigit = true;
				break;
			default:
				if (underscoreE || needDigit || (c >= 65 && c <= 90) || (c >= 97 && c <= 122))
					this.reportError("Invalid floating-point number");
				return CiToken.LITERAL_DOUBLE;
			}
		}
	}

	#readNumberLiteral(i)
	{
		let leadingZero = false;
		let tooBig = false;
		for (let needDigit = false;; this.readChar()) {
			let c = this.peekChar();
			switch (c) {
			case 48:
			case 49:
			case 50:
			case 51:
			case 52:
			case 53:
			case 54:
			case 55:
			case 56:
			case 57:
				c -= 48;
				break;
			case 46:
				this.readChar();
				return this.#readFloatLiteral(true);
			case 101:
			case 69:
				return this.#readFloatLiteral(needDigit);
			case 95:
				needDigit = true;
				continue;
			default:
				this.longValue = i;
				if (leadingZero)
					this.reportError("Leading zeros are not permitted, octal numbers must begin with 0o");
				if (needDigit || (c >= 65 && c <= 90) || (c >= 97 && c <= 122))
					this.reportError("Invalid integer");
				else if (tooBig)
					this.reportError("Integer too big");
				return CiToken.LITERAL_LONG;
			}
			if (i == 0)
				leadingZero = true;
			if (i > (c < 8 ? 922337203685477580n : 922337203685477579n))
				tooBig = true;
			else
				i = 10n * i + BigInt(c);
			needDigit = false;
		}
	}

	static getEscapedChar(c)
	{
		switch (c) {
		case 34:
			return 34;
		case 39:
			return 39;
		case 92:
			return 92;
		case 110:
			return 10;
		case 114:
			return 13;
		case 116:
			return 9;
		default:
			return -1;
		}
	}

	#readCharLiteral()
	{
		let c = this.readChar();
		if (c < 32) {
			this.reportError("Invalid character in literal");
			return 65533;
		}
		if (c != 92)
			return c;
		c = CiLexer.getEscapedChar(this.readChar());
		if (c < 0) {
			this.reportError("Unknown escape sequence");
			return 65533;
		}
		return c;
	}

	readString(interpolated)
	{
		for (let offset = this.charOffset;; this.#readCharLiteral()) {
			switch (this.peekChar()) {
			case -1:
				this.reportError("Unterminated string literal");
				return CiToken.END_OF_FILE;
			case 10:
				this.reportError("Unterminated string literal");
				this.stringValue = "";
				return CiToken.LITERAL_STRING;
			case 34:
				{
					let endOffset = this.charOffset;
					this.readChar();
					this.stringValue = new TextDecoder().decode(this.input.subarray(offset, offset + endOffset - offset));
				}
				return CiToken.LITERAL_STRING;
			case 123:
				if (interpolated) {
					let endOffset = this.charOffset;
					this.readChar();
					if (this.peekChar() != 123) {
						this.stringValue = new TextDecoder().decode(this.input.subarray(offset, offset + endOffset - offset));
						return CiToken.INTERPOLATED_STRING;
					}
				}
				break;
			default:
				break;
			}
		}
	}

	getLexeme()
	{
		return new TextDecoder().decode(this.input.subarray(this.lexemeOffset, this.lexemeOffset + this.charOffset - this.lexemeOffset));
	}

	#readId(c)
	{
		if (CiLexer.isLetterOrDigit(c)) {
			while (CiLexer.isLetterOrDigit(this.peekChar()))
				this.readChar();
			this.stringValue = this.getLexeme();
		}
		else {
			this.reportError("Invalid character");
			this.stringValue = "";
		}
	}

	#readPreToken()
	{
		for (;;) {
			let atLineStart = this.#atLineStart;
			this.tokenColumn = this.column;
			this.lexemeOffset = this.charOffset;
			let c = this.readChar();
			switch (c) {
			case -1:
				return CiToken.END_OF_FILE;
			case 9:
			case 13:
			case 32:
				break;
			case 10:
				if (this.#lineMode)
					return CiToken.END_OF_LINE;
				break;
			case 35:
				if (!atLineStart)
					return CiToken.HASH;
				this.lexemeOffset = this.charOffset;
				this.#readId(this.readChar());
				switch (this.stringValue) {
				case "if":
					return CiToken.PRE_IF;
				case "elif":
					return CiToken.PRE_EL_IF;
				case "else":
					return CiToken.PRE_ELSE;
				case "endif":
					return CiToken.PRE_END_IF;
				default:
					this.reportError("Unknown preprocessor directive");
					continue;
				}
			case 59:
				return CiToken.SEMICOLON;
			case 46:
				if (this.#eatChar(46))
					return CiToken.RANGE;
				return CiToken.DOT;
			case 44:
				return CiToken.COMMA;
			case 40:
				return CiToken.LEFT_PARENTHESIS;
			case 41:
				return CiToken.RIGHT_PARENTHESIS;
			case 91:
				return CiToken.LEFT_BRACKET;
			case 93:
				return CiToken.RIGHT_BRACKET;
			case 123:
				return CiToken.LEFT_BRACE;
			case 125:
				return CiToken.RIGHT_BRACE;
			case 126:
				return CiToken.TILDE;
			case 63:
				return CiToken.QUESTION_MARK;
			case 58:
				return CiToken.COLON;
			case 43:
				if (this.#eatChar(43))
					return CiToken.INCREMENT;
				if (this.#eatChar(61))
					return CiToken.ADD_ASSIGN;
				return CiToken.PLUS;
			case 45:
				if (this.#eatChar(45))
					return CiToken.DECREMENT;
				if (this.#eatChar(61))
					return CiToken.SUB_ASSIGN;
				return CiToken.MINUS;
			case 42:
				if (this.#eatChar(61))
					return CiToken.MUL_ASSIGN;
				return CiToken.ASTERISK;
			case 47:
				if (this.#eatChar(47)) {
					c = this.readChar();
					if (c == 47 && this.#enableDocComments) {
						this.#skipWhitespace();
						switch (this.peekChar()) {
						case 10:
							return CiToken.DOC_BLANK;
						case 42:
							this.readChar();
							this.#skipWhitespace();
							return CiToken.DOC_BULLET;
						default:
							return CiToken.DOC_REGULAR;
						}
					}
					while (c != 10 && c >= 0)
						c = this.readChar();
					if (c == 10 && this.#lineMode)
						return CiToken.END_OF_LINE;
					break;
				}
				if (this.#eatChar(42)) {
					let startLine = this.line;
					do {
						c = this.readChar();
						if (c < 0) {
							this.reportError(`Unterminated multi-line comment, started in line ${startLine}`);
							return CiToken.END_OF_FILE;
						}
					}
					while (c != 42 || this.peekChar() != 47);
					this.readChar();
					break;
				}
				if (this.#eatChar(61))
					return CiToken.DIV_ASSIGN;
				return CiToken.SLASH;
			case 37:
				if (this.#eatChar(61))
					return CiToken.MOD_ASSIGN;
				return CiToken.MOD;
			case 38:
				if (this.#eatChar(38))
					return CiToken.COND_AND;
				if (this.#eatChar(61))
					return CiToken.AND_ASSIGN;
				return CiToken.AND;
			case 124:
				if (this.#eatChar(124))
					return CiToken.COND_OR;
				if (this.#eatChar(61))
					return CiToken.OR_ASSIGN;
				return CiToken.OR;
			case 94:
				if (this.#eatChar(61))
					return CiToken.XOR_ASSIGN;
				return CiToken.XOR;
			case 61:
				if (this.#eatChar(61))
					return CiToken.EQUAL;
				if (this.#eatChar(62))
					return CiToken.FAT_ARROW;
				return CiToken.ASSIGN;
			case 33:
				if (this.#eatChar(61))
					return CiToken.NOT_EQUAL;
				return CiToken.EXCLAMATION_MARK;
			case 60:
				if (this.#eatChar(60)) {
					if (this.#eatChar(61))
						return CiToken.SHIFT_LEFT_ASSIGN;
					return CiToken.SHIFT_LEFT;
				}
				if (this.#eatChar(61))
					return CiToken.LESS_OR_EQUAL;
				return CiToken.LESS;
			case 62:
				if (this.parsingTypeArg)
					return CiToken.RIGHT_ANGLE;
				if (this.#eatChar(62)) {
					if (this.#eatChar(61))
						return CiToken.SHIFT_RIGHT_ASSIGN;
					return CiToken.SHIFT_RIGHT;
				}
				if (this.#eatChar(61))
					return CiToken.GREATER_OR_EQUAL;
				return CiToken.GREATER;
			case 39:
				if (this.peekChar() == 39) {
					this.reportError("Empty character literal");
					this.longValue = 0n;
				}
				else
					this.longValue = BigInt(this.#readCharLiteral());
				if (!this.#eatChar(39))
					this.reportError("Unterminated character literal");
				return CiToken.LITERAL_CHAR;
			case 34:
				return this.readString(false);
			case 36:
				if (this.#eatChar(34))
					return this.readString(true);
				this.reportError("Expected interpolated string");
				break;
			case 48:
				switch (this.peekChar()) {
				case 66:
				case 98:
					this.readChar();
					return this.#readIntegerLiteral(1);
				case 79:
				case 111:
					this.readChar();
					return this.#readIntegerLiteral(3);
				case 88:
				case 120:
					this.readChar();
					return this.#readIntegerLiteral(4);
				default:
					return this.#readNumberLiteral(0n);
				}
			case 49:
			case 50:
			case 51:
			case 52:
			case 53:
			case 54:
			case 55:
			case 56:
			case 57:
				return this.#readNumberLiteral(BigInt(c - 48));
			default:
				this.#readId(c);
				switch (this.stringValue) {
				case "":
					continue;
				case "abstract":
					return CiToken.ABSTRACT;
				case "assert":
					return CiToken.ASSERT;
				case "break":
					return CiToken.BREAK;
				case "case":
					return CiToken.CASE;
				case "class":
					return CiToken.CLASS;
				case "const":
					return CiToken.CONST;
				case "continue":
					return CiToken.CONTINUE;
				case "default":
					return CiToken.DEFAULT;
				case "do":
					return CiToken.DO;
				case "else":
					return CiToken.ELSE;
				case "enum":
					return CiToken.ENUM;
				case "false":
					return CiToken.FALSE;
				case "for":
					return CiToken.FOR;
				case "foreach":
					return CiToken.FOREACH;
				case "if":
					return CiToken.IF;
				case "in":
					return CiToken.IN;
				case "internal":
					return CiToken.INTERNAL;
				case "is":
					return CiToken.IS;
				case "lock":
					return CiToken.LOCK_;
				case "native":
					return CiToken.NATIVE;
				case "new":
					return CiToken.NEW;
				case "null":
					return CiToken.NULL;
				case "override":
					return CiToken.OVERRIDE;
				case "protected":
					return CiToken.PROTECTED;
				case "public":
					return CiToken.PUBLIC;
				case "resource":
					return CiToken.RESOURCE;
				case "return":
					return CiToken.RETURN;
				case "sealed":
					return CiToken.SEALED;
				case "static":
					return CiToken.STATIC;
				case "switch":
					return CiToken.SWITCH;
				case "throw":
					return CiToken.THROW;
				case "throws":
					return CiToken.THROWS;
				case "true":
					return CiToken.TRUE;
				case "virtual":
					return CiToken.VIRTUAL;
				case "void":
					return CiToken.VOID;
				case "when":
					return CiToken.WHEN;
				case "while":
					return CiToken.WHILE;
				default:
					return CiToken.ID;
				}
			}
		}
	}

	#nextPreToken()
	{
		this.currentToken = this.#readPreToken();
	}

	see(token)
	{
		return this.currentToken == token;
	}

	static tokenToString(token)
	{
		switch (token) {
		case CiToken.END_OF_FILE:
			return "end-of-file";
		case CiToken.ID:
			return "identifier";
		case CiToken.LITERAL_LONG:
			return "integer constant";
		case CiToken.LITERAL_DOUBLE:
			return "floating-point constant";
		case CiToken.LITERAL_CHAR:
			return "character constant";
		case CiToken.LITERAL_STRING:
			return "string constant";
		case CiToken.INTERPOLATED_STRING:
			return "interpolated string";
		case CiToken.SEMICOLON:
			return "';'";
		case CiToken.DOT:
			return "'.'";
		case CiToken.COMMA:
			return "','";
		case CiToken.LEFT_PARENTHESIS:
			return "'('";
		case CiToken.RIGHT_PARENTHESIS:
			return "')'";
		case CiToken.LEFT_BRACKET:
			return "'['";
		case CiToken.RIGHT_BRACKET:
			return "']'";
		case CiToken.LEFT_BRACE:
			return "'{'";
		case CiToken.RIGHT_BRACE:
			return "'}'";
		case CiToken.PLUS:
			return "'+'";
		case CiToken.MINUS:
			return "'-'";
		case CiToken.ASTERISK:
			return "'*'";
		case CiToken.SLASH:
			return "'/'";
		case CiToken.MOD:
			return "'%'";
		case CiToken.AND:
			return "'&'";
		case CiToken.OR:
			return "'|'";
		case CiToken.XOR:
			return "'^'";
		case CiToken.TILDE:
			return "'~'";
		case CiToken.SHIFT_LEFT:
			return "'<<'";
		case CiToken.SHIFT_RIGHT:
			return "'>>'";
		case CiToken.EQUAL:
			return "'=='";
		case CiToken.NOT_EQUAL:
			return "'!='";
		case CiToken.LESS:
			return "'<'";
		case CiToken.LESS_OR_EQUAL:
			return "'<='";
		case CiToken.GREATER:
			return "'>'";
		case CiToken.GREATER_OR_EQUAL:
			return "'>='";
		case CiToken.RIGHT_ANGLE:
			return "'>'";
		case CiToken.COND_AND:
			return "'&&'";
		case CiToken.COND_OR:
			return "'||'";
		case CiToken.EXCLAMATION_MARK:
			return "'!'";
		case CiToken.HASH:
			return "'#'";
		case CiToken.ASSIGN:
			return "'='";
		case CiToken.ADD_ASSIGN:
			return "'+='";
		case CiToken.SUB_ASSIGN:
			return "'-='";
		case CiToken.MUL_ASSIGN:
			return "'*='";
		case CiToken.DIV_ASSIGN:
			return "'/='";
		case CiToken.MOD_ASSIGN:
			return "'%='";
		case CiToken.AND_ASSIGN:
			return "'&='";
		case CiToken.OR_ASSIGN:
			return "'|='";
		case CiToken.XOR_ASSIGN:
			return "'^='";
		case CiToken.SHIFT_LEFT_ASSIGN:
			return "'<<='";
		case CiToken.SHIFT_RIGHT_ASSIGN:
			return "'>>='";
		case CiToken.INCREMENT:
			return "'++'";
		case CiToken.DECREMENT:
			return "'--'";
		case CiToken.QUESTION_MARK:
			return "'?'";
		case CiToken.COLON:
			return "':'";
		case CiToken.FAT_ARROW:
			return "'=>'";
		case CiToken.RANGE:
			return "'..'";
		case CiToken.DOC_REGULAR:
		case CiToken.DOC_BULLET:
		case CiToken.DOC_BLANK:
			return "'///'";
		case CiToken.ABSTRACT:
			return "'abstract'";
		case CiToken.ASSERT:
			return "'assert'";
		case CiToken.BREAK:
			return "'break'";
		case CiToken.CASE:
			return "'case'";
		case CiToken.CLASS:
			return "'class'";
		case CiToken.CONST:
			return "'const'";
		case CiToken.CONTINUE:
			return "'continue'";
		case CiToken.DEFAULT:
			return "'default'";
		case CiToken.DO:
			return "'do'";
		case CiToken.ELSE:
			return "'else'";
		case CiToken.ENUM:
			return "'enum'";
		case CiToken.FALSE:
			return "'false'";
		case CiToken.FOR:
			return "'for'";
		case CiToken.FOREACH:
			return "'foreach'";
		case CiToken.IF:
			return "'if'";
		case CiToken.IN:
			return "'in'";
		case CiToken.INTERNAL:
			return "'internal'";
		case CiToken.IS:
			return "'is'";
		case CiToken.LOCK_:
			return "'lock'";
		case CiToken.NATIVE:
			return "'native'";
		case CiToken.NEW:
			return "'new'";
		case CiToken.NULL:
			return "'null'";
		case CiToken.OVERRIDE:
			return "'override'";
		case CiToken.PROTECTED:
			return "'protected'";
		case CiToken.PUBLIC:
			return "'public'";
		case CiToken.RESOURCE:
			return "'resource'";
		case CiToken.RETURN:
			return "'return'";
		case CiToken.SEALED:
			return "'sealed'";
		case CiToken.STATIC:
			return "'static'";
		case CiToken.SWITCH:
			return "'switch'";
		case CiToken.THROW:
			return "'throw'";
		case CiToken.THROWS:
			return "'throws'";
		case CiToken.TRUE:
			return "'true'";
		case CiToken.VIRTUAL:
			return "'virtual'";
		case CiToken.VOID:
			return "'void'";
		case CiToken.WHEN:
			return "'when'";
		case CiToken.WHILE:
			return "'while'";
		case CiToken.END_OF_LINE:
			return "end-of-line";
		case CiToken.PRE_IF:
			return "'#if'";
		case CiToken.PRE_EL_IF:
			return "'#elif'";
		case CiToken.PRE_ELSE:
			return "'#else'";
		case CiToken.PRE_END_IF:
			return "'#endif'";
		default:
			throw new Error();
		}
	}

	check(expected)
	{
		if (this.see(expected))
			return true;
		this.reportError(`Expected ${CiLexer.tokenToString(expected)}, got ${CiLexer.tokenToString(this.currentToken)}`);
		return false;
	}

	#eatPre(token)
	{
		if (this.see(token)) {
			this.#nextPreToken();
			return true;
		}
		return false;
	}

	#parsePrePrimary()
	{
		if (this.#eatPre(CiToken.EXCLAMATION_MARK))
			return !this.#parsePrePrimary();
		if (this.#eatPre(CiToken.LEFT_PARENTHESIS)) {
			let result = this.#parsePreOr();
			this.check(CiToken.RIGHT_PARENTHESIS);
			this.#nextPreToken();
			return result;
		}
		if (this.see(CiToken.ID)) {
			let result = this.#preSymbols.has(this.stringValue);
			this.#nextPreToken();
			return result;
		}
		if (this.#eatPre(CiToken.FALSE))
			return false;
		if (this.#eatPre(CiToken.TRUE))
			return true;
		this.reportError("Invalid preprocessor expression");
		return false;
	}

	#parsePreEquality()
	{
		let result = this.#parsePrePrimary();
		for (;;) {
			if (this.#eatPre(CiToken.EQUAL))
				result = result == this.#parsePrePrimary();
			else if (this.#eatPre(CiToken.NOT_EQUAL))
				result = result != this.#parsePrePrimary();
			else
				return result;
		}
	}

	#parsePreAnd()
	{
		let result = this.#parsePreEquality();
		while (this.#eatPre(CiToken.COND_AND))
			if (!this.#parsePreEquality())
				result = false;
		return result;
	}

	#parsePreOr()
	{
		let result = this.#parsePreAnd();
		while (this.#eatPre(CiToken.COND_OR))
			if (this.#parsePreAnd())
				result = true;
		return result;
	}

	#parsePreExpr()
	{
		this.#lineMode = true;
		this.#nextPreToken();
		let result = this.#parsePreOr();
		this.check(CiToken.END_OF_LINE);
		this.#lineMode = false;
		return result;
	}

	#expectEndOfLine(directive)
	{
		this.#lineMode = true;
		let token = this.#readPreToken();
		if (token != CiToken.END_OF_LINE && token != CiToken.END_OF_FILE)
			this.reportError(`Unexpected characters after '${directive}'`);
		this.#lineMode = false;
	}

	#popPreElse(directive)
	{
		if (this.#preElseStack.length == 0) {
			this.reportError(`'${directive}' with no matching '#if'`);
			return false;
		}
		if (this.#preElseStack.pop() && directive != "#endif")
			this.reportError(`'${directive}' after '#else'`);
		return true;
	}

	#skipUnmet(state)
	{
		this.#enableDocComments = false;
		for (;;) {
			switch (this.#readPreToken()) {
			case CiToken.END_OF_FILE:
				this.reportError("Expected '#endif', got end-of-file");
				return;
			case CiToken.PRE_IF:
				this.#parsePreExpr();
				this.#skipUnmet(CiPreState.ALREADY);
				break;
			case CiToken.PRE_EL_IF:
				if (state == CiPreState.ALREADY_ELSE)
					this.reportError("'#elif' after '#else'");
				if (this.#parsePreExpr() && state == CiPreState.NOT_YET) {
					this.#preElseStack.push(false);
					return;
				}
				break;
			case CiToken.PRE_ELSE:
				if (state == CiPreState.ALREADY_ELSE)
					this.reportError("'#else' after '#else'");
				this.#expectEndOfLine("#else");
				if (state == CiPreState.NOT_YET) {
					this.#preElseStack.push(true);
					return;
				}
				state = CiPreState.ALREADY_ELSE;
				break;
			case CiToken.PRE_END_IF:
				this.#expectEndOfLine("#endif");
				return;
			default:
				break;
			}
		}
	}

	#readToken()
	{
		for (;;) {
			this.#enableDocComments = true;
			let token = this.#readPreToken();
			let matched;
			switch (token) {
			case CiToken.END_OF_FILE:
				if (this.#preElseStack.length != 0)
					this.reportError("Expected '#endif', got end-of-file");
				return CiToken.END_OF_FILE;
			case CiToken.PRE_IF:
				if (this.#parsePreExpr())
					this.#preElseStack.push(false);
				else
					this.#skipUnmet(CiPreState.NOT_YET);
				break;
			case CiToken.PRE_EL_IF:
				matched = this.#popPreElse("#elif");
				this.#parsePreExpr();
				if (matched)
					this.#skipUnmet(CiPreState.ALREADY);
				break;
			case CiToken.PRE_ELSE:
				matched = this.#popPreElse("#else");
				this.#expectEndOfLine("#else");
				if (matched)
					this.#skipUnmet(CiPreState.ALREADY_ELSE);
				break;
			case CiToken.PRE_END_IF:
				this.#popPreElse("#endif");
				this.#expectEndOfLine("#endif");
				break;
			default:
				return token;
			}
		}
	}

	nextToken()
	{
		let token = this.currentToken;
		this.currentToken = this.#readToken();
		return token;
	}

	eat(token)
	{
		if (this.see(token)) {
			this.nextToken();
			return true;
		}
		return false;
	}

	expect(expected)
	{
		let found = this.check(expected);
		this.nextToken();
		return found;
	}

	expectOrSkip(expected)
	{
		if (this.check(expected))
			this.nextToken();
		else {
			do
				this.nextToken();
			while (!this.see(CiToken.END_OF_FILE) && !this.eat(expected));
		}
	}
}

export const CiVisibility = {
	PRIVATE : 0,
	INTERNAL : 1,
	PROTECTED : 2,
	PUBLIC : 3,
	NUMERIC_ELEMENT_TYPE : 4,
	FINAL_VALUE_TYPE : 5
}

export const CiCallType = {
	STATIC : 0,
	NORMAL : 1,
	ABSTRACT : 2,
	VIRTUAL : 3,
	OVERRIDE : 4,
	SEALED : 5
}

export const CiPriority = {
	STATEMENT : 0,
	ARGUMENT : 1,
	ASSIGN : 2,
	SELECT : 3,
	SELECT_COND : 4,
	COND_OR : 5,
	COND_AND : 6,
	OR : 7,
	XOR : 8,
	AND : 9,
	EQUALITY : 10,
	REL : 11,
	SHIFT : 12,
	ADD : 13,
	MUL : 14,
	PRIMARY : 15
}

export const CiId = {
	NONE : 0,
	VOID_TYPE : 1,
	NULL_TYPE : 2,
	BASE_PTR : 3,
	TYPE_PARAM0 : 4,
	TYPE_PARAM0_NOT_FINAL : 5,
	TYPE_PARAM0_PREDICATE : 6,
	S_BYTE_RANGE : 7,
	BYTE_RANGE : 8,
	SHORT_RANGE : 9,
	U_SHORT_RANGE : 10,
	INT_TYPE : 11,
	LONG_TYPE : 12,
	FLOAT_TYPE : 13,
	DOUBLE_TYPE : 14,
	FLOAT_INT_TYPE : 15,
	BOOL_TYPE : 16,
	STRING_CLASS : 17,
	STRING_PTR_TYPE : 18,
	STRING_STORAGE_TYPE : 19,
	ARRAY_PTR_CLASS : 20,
	ARRAY_STORAGE_CLASS : 21,
	LIST_CLASS : 22,
	QUEUE_CLASS : 23,
	STACK_CLASS : 24,
	HASH_SET_CLASS : 25,
	SORTED_SET_CLASS : 26,
	DICTIONARY_CLASS : 27,
	SORTED_DICTIONARY_CLASS : 28,
	ORDERED_DICTIONARY_CLASS : 29,
	TEXT_WRITER_CLASS : 30,
	STRING_WRITER_CLASS : 31,
	REGEX_OPTIONS_ENUM : 32,
	REGEX_CLASS : 33,
	MATCH_CLASS : 34,
	LOCK_CLASS : 35,
	STRING_LENGTH : 36,
	ARRAY_LENGTH : 37,
	CONSOLE_ERROR : 38,
	CLASS_TO_STRING : 39,
	MATCH_START : 40,
	MATCH_END : 41,
	MATCH_LENGTH : 42,
	MATCH_VALUE : 43,
	MATH_NA_N : 44,
	MATH_NEGATIVE_INFINITY : 45,
	MATH_POSITIVE_INFINITY : 46,
	ENUM_FROM_INT : 47,
	ENUM_HAS_FLAG : 48,
	INT_TRY_PARSE : 49,
	LONG_TRY_PARSE : 50,
	DOUBLE_TRY_PARSE : 51,
	STRING_CONTAINS : 52,
	STRING_ENDS_WITH : 53,
	STRING_INDEX_OF : 54,
	STRING_LAST_INDEX_OF : 55,
	STRING_REPLACE : 56,
	STRING_STARTS_WITH : 57,
	STRING_SUBSTRING : 58,
	ARRAY_BINARY_SEARCH_ALL : 59,
	ARRAY_BINARY_SEARCH_PART : 60,
	ARRAY_CONTAINS : 61,
	ARRAY_COPY_TO : 62,
	ARRAY_FILL_ALL : 63,
	ARRAY_FILL_PART : 64,
	ARRAY_SORT_ALL : 65,
	ARRAY_SORT_PART : 66,
	LIST_ADD : 67,
	LIST_ADD_RANGE : 68,
	LIST_ALL : 69,
	LIST_ANY : 70,
	LIST_CLEAR : 71,
	LIST_CONTAINS : 72,
	LIST_COPY_TO : 73,
	LIST_COUNT : 74,
	LIST_INDEX_OF : 75,
	LIST_INSERT : 76,
	LIST_LAST : 77,
	LIST_REMOVE_AT : 78,
	LIST_REMOVE_RANGE : 79,
	LIST_SORT_ALL : 80,
	LIST_SORT_PART : 81,
	QUEUE_CLEAR : 82,
	QUEUE_COUNT : 83,
	QUEUE_DEQUEUE : 84,
	QUEUE_ENQUEUE : 85,
	QUEUE_PEEK : 86,
	STACK_CLEAR : 87,
	STACK_COUNT : 88,
	STACK_PEEK : 89,
	STACK_PUSH : 90,
	STACK_POP : 91,
	HASH_SET_ADD : 92,
	HASH_SET_CLEAR : 93,
	HASH_SET_CONTAINS : 94,
	HASH_SET_COUNT : 95,
	HASH_SET_REMOVE : 96,
	SORTED_SET_ADD : 97,
	SORTED_SET_CLEAR : 98,
	SORTED_SET_CONTAINS : 99,
	SORTED_SET_COUNT : 100,
	SORTED_SET_REMOVE : 101,
	DICTIONARY_ADD : 102,
	DICTIONARY_CLEAR : 103,
	DICTIONARY_CONTAINS_KEY : 104,
	DICTIONARY_COUNT : 105,
	DICTIONARY_REMOVE : 106,
	SORTED_DICTIONARY_CLEAR : 107,
	SORTED_DICTIONARY_CONTAINS_KEY : 108,
	SORTED_DICTIONARY_COUNT : 109,
	SORTED_DICTIONARY_REMOVE : 110,
	ORDERED_DICTIONARY_CLEAR : 111,
	ORDERED_DICTIONARY_CONTAINS_KEY : 112,
	ORDERED_DICTIONARY_COUNT : 113,
	ORDERED_DICTIONARY_REMOVE : 114,
	TEXT_WRITER_WRITE : 115,
	TEXT_WRITER_WRITE_CHAR : 116,
	TEXT_WRITER_WRITE_CODE_POINT : 117,
	TEXT_WRITER_WRITE_LINE : 118,
	CONSOLE_WRITE : 119,
	CONSOLE_WRITE_LINE : 120,
	STRING_WRITER_CLEAR : 121,
	STRING_WRITER_TO_STRING : 122,
	U_T_F8_GET_BYTE_COUNT : 123,
	U_T_F8_GET_BYTES : 124,
	U_T_F8_GET_STRING : 125,
	ENVIRONMENT_GET_ENVIRONMENT_VARIABLE : 126,
	REGEX_COMPILE : 127,
	REGEX_ESCAPE : 128,
	REGEX_IS_MATCH_STR : 129,
	REGEX_IS_MATCH_REGEX : 130,
	MATCH_FIND_STR : 131,
	MATCH_FIND_REGEX : 132,
	MATCH_GET_CAPTURE : 133,
	MATH_METHOD : 134,
	MATH_ABS : 135,
	MATH_CEILING : 136,
	MATH_CLAMP : 137,
	MATH_FUSED_MULTIPLY_ADD : 138,
	MATH_IS_FINITE : 139,
	MATH_IS_INFINITY : 140,
	MATH_IS_NA_N : 141,
	MATH_LOG2 : 142,
	MATH_MAX_INT : 143,
	MATH_MAX_DOUBLE : 144,
	MATH_MIN_INT : 145,
	MATH_MIN_DOUBLE : 146,
	MATH_ROUND : 147,
	MATH_TRUNCATE : 148
}

class CiDocInline
{
}

class CiDocText extends CiDocInline
{
	text;
}

class CiDocCode extends CiDocInline
{
	text;
}

class CiDocLine extends CiDocInline
{
}

export class CiDocBlock
{
}

export class CiDocPara extends CiDocBlock
{
	children = [];
}

export class CiDocList extends CiDocBlock
{
	items = [];
}

export class CiCodeDoc
{
	summary = new CiDocPara();
	details = [];
}

export class CiVisitor
{
	hasErrors = false;

	visitOptionalStatement(statement)
	{
		if (statement != null)
			statement.acceptStatement(this);
	}

	reportError(statement, message)
	{
		console.error(`${this.getCurrentContainer().filename}(${statement.line}): ERROR: ${message}`);
		this.hasErrors = true;
	}
}

export class CiStatement
{
	line;
}

export class CiExpr extends CiStatement
{
	type;

	completesNormally()
	{
		return true;
	}

	toString()
	{
		throw new Error();
	}

	isIndexing()
	{
		return false;
	}

	isLiteralZero()
	{
		return false;
	}

	isConstEnum()
	{
		return false;
	}

	intValue()
	{
		throw new Error();
	}

	accept(visitor, parent)
	{
		throw new Error();
	}

	acceptStatement(visitor)
	{
		visitor.visitExpr(this);
	}

	isReferenceTo(symbol)
	{
		return false;
	}
}

export class CiSymbol extends CiExpr
{
	id = CiId.NONE;
	name;
	next;
	parent;
	documentation = null;

	toString()
	{
		return this.name;
	}
}

export class CiScope extends CiSymbol
{
	dict = {};
	first = null;
	#last;

	count()
	{
		return Object.keys(this.dict).length;
	}

	firstParameter()
	{
		const result = this.first;
		return result;
	}

	getContainer()
	{
		for (let scope = this; scope != null; scope = scope.parent) {
			let container;
			if ((container = scope) instanceof CiContainerType)
				return container;
		}
		throw new Error();
	}

	contains(symbol)
	{
		return this.dict.hasOwnProperty(symbol.name);
	}

	tryLookup(name, global)
	{
		for (let scope = this; scope != null && (global || !(scope instanceof CiProgram || scope instanceof CiSystem)); scope = scope.parent) {
			if (scope.dict.hasOwnProperty(name))
				return scope.dict[name];
		}
		return null;
	}

	add(symbol)
	{
		this.dict[symbol.name] = symbol;
		symbol.next = null;
		symbol.parent = this;
		if (this.first == null)
			this.first = symbol;
		else
			this.#last.next = symbol;
		this.#last = symbol;
	}

	encloses(symbol)
	{
		for (let scope = symbol.parent; scope != null; scope = scope.parent) {
			if (scope == this)
				return true;
		}
		return false;
	}
}

export class CiAggregateInitializer extends CiExpr
{
	items = [];

	accept(visitor, parent)
	{
		visitor.visitAggregateInitializer(this);
	}
}

export class CiLiteral extends CiExpr
{

	getLiteralString()
	{
		throw new Error();
	}
}

export class CiLiteralNull extends CiLiteral
{

	isDefaultValue()
	{
		return true;
	}

	accept(visitor, parent)
	{
		visitor.visitLiteralNull();
	}

	toString()
	{
		return "null";
	}
}

export class CiLiteralFalse extends CiLiteral
{

	isDefaultValue()
	{
		return true;
	}

	accept(visitor, parent)
	{
		visitor.visitLiteralFalse();
	}

	toString()
	{
		return "false";
	}
}

export class CiLiteralTrue extends CiLiteral
{

	isDefaultValue()
	{
		return false;
	}

	accept(visitor, parent)
	{
		visitor.visitLiteralTrue();
	}

	toString()
	{
		return "true";
	}
}

export class CiLiteralLong extends CiLiteral
{
	value;

	isLiteralZero()
	{
		return this.value == 0;
	}

	intValue()
	{
		return Number(this.value);
	}

	isDefaultValue()
	{
		return this.value == 0;
	}

	accept(visitor, parent)
	{
		visitor.visitLiteralLong(this.value);
	}

	getLiteralString()
	{
		return `${this.value}`;
	}

	toString()
	{
		return `${this.value}`;
	}
}

export class CiLiteralChar extends CiLiteralLong
{

	static new(value, line)
	{
		return Object.assign(new CiLiteralChar(), { line: line, type: CiRangeType.new(value, value), value: BigInt(value) });
	}

	accept(visitor, parent)
	{
		visitor.visitLiteralChar(Number(this.value));
	}
}

export class CiLiteralDouble extends CiLiteral
{
	value;

	isDefaultValue()
	{
		return this.value == 0 && 1.0 / this.value > 0;
	}

	accept(visitor, parent)
	{
		visitor.visitLiteralDouble(this.value);
	}

	getLiteralString()
	{
		return `${this.value}`;
	}

	toString()
	{
		return `${this.value}`;
	}
}

export class CiLiteralString extends CiLiteral
{
	value;

	isDefaultValue()
	{
		return false;
	}

	accept(visitor, parent)
	{
		visitor.visitLiteralString(this.value);
	}

	getLiteralString()
	{
		return this.value;
	}

	toString()
	{
		return `\"${this.value}\"`;
	}

	getAsciiLength()
	{
		let length = 0;
		let escaped = false;
		for (const c of this.value) {
			if (c.codePointAt(0) < 0 || c.codePointAt(0) > 127)
				return -1;
			if (!escaped && c.codePointAt(0) == 92)
				escaped = true;
			else {
				length++;
				escaped = false;
			}
		}
		return length;
	}

	getAsciiAt(i)
	{
		let escaped = false;
		for (const c of this.value) {
			if (c.codePointAt(0) < 0 || c.codePointAt(0) > 127)
				return -1;
			if (!escaped && c.codePointAt(0) == 92)
				escaped = true;
			else if (i == 0)
				return escaped ? CiLexer.getEscapedChar(c.codePointAt(0)) : c.codePointAt(0);
			else {
				i--;
				escaped = false;
			}
		}
		return -1;
	}

	getOneAscii()
	{
		switch (this.value.length) {
		case 1:
			let c = this.value.charCodeAt(0);
			return c >= 0 && c <= 127 ? c : -1;
		case 2:
			return this.value.charCodeAt(0) == 92 ? CiLexer.getEscapedChar(this.value.charCodeAt(1)) : -1;
		default:
			return -1;
		}
	}
}

export class CiInterpolatedPart
{
	prefix;
	argument;
	widthExpr;
	width;
	format;
	precision;
}

export class CiInterpolatedString extends CiExpr
{
	parts = [];
	suffix;

	addPart(prefix, arg, widthExpr = null, format = 32, precision = -1)
	{
		this.parts.push(new CiInterpolatedPart());
		let part = this.parts.at(-1);
		part.prefix = prefix;
		part.argument = arg;
		part.widthExpr = widthExpr;
		part.format = format;
		part.precision = precision;
	}

	accept(visitor, parent)
	{
		visitor.visitInterpolatedString(this, parent);
	}
}

export class CiImplicitEnumValue extends CiExpr
{
	value;

	intValue()
	{
		return this.value;
	}
}

export class CiSymbolReference extends CiExpr
{
	left;
	name;
	symbol;

	isConstEnum()
	{
		return this.symbol.parent instanceof CiEnum;
	}

	intValue()
	{
		const konst = this.symbol;
		return konst.value.intValue();
	}

	accept(visitor, parent)
	{
		visitor.visitSymbolReference(this, parent);
	}

	isReferenceTo(symbol)
	{
		return this.symbol == symbol;
	}

	toString()
	{
		return this.left != null ? `${this.left}.${this.name}` : this.name;
	}
}

export class CiUnaryExpr extends CiExpr
{
	op;
	inner;
}

export class CiPrefixExpr extends CiUnaryExpr
{

	isConstEnum()
	{
		return this.type instanceof CiEnumFlags && this.inner.isConstEnum();
	}

	intValue()
	{
		console.assert(this.op == CiToken.TILDE);
		return ~this.inner.intValue();
	}

	accept(visitor, parent)
	{
		visitor.visitPrefixExpr(this, parent);
	}
}

export class CiPostfixExpr extends CiUnaryExpr
{

	accept(visitor, parent)
	{
		visitor.visitPostfixExpr(this, parent);
	}
}

export class CiBinaryExpr extends CiExpr
{
	left;
	op;
	right;

	isIndexing()
	{
		return this.op == CiToken.LEFT_BRACKET;
	}

	isConstEnum()
	{
		switch (this.op) {
		case CiToken.AND:
		case CiToken.OR:
		case CiToken.XOR:
			return this.type instanceof CiEnumFlags && this.left.isConstEnum() && this.right.isConstEnum();
		default:
			return false;
		}
	}

	intValue()
	{
		switch (this.op) {
		case CiToken.AND:
			return this.left.intValue() & this.right.intValue();
		case CiToken.OR:
			return this.left.intValue() | this.right.intValue();
		case CiToken.XOR:
			return this.left.intValue() ^ this.right.intValue();
		default:
			throw new Error();
		}
	}

	accept(visitor, parent)
	{
		visitor.visitBinaryExpr(this, parent);
	}

	isRel()
	{
		switch (this.op) {
		case CiToken.EQUAL:
		case CiToken.NOT_EQUAL:
		case CiToken.LESS:
		case CiToken.LESS_OR_EQUAL:
		case CiToken.GREATER:
		case CiToken.GREATER_OR_EQUAL:
			return true;
		default:
			return false;
		}
	}

	isAssign()
	{
		switch (this.op) {
		case CiToken.ASSIGN:
		case CiToken.ADD_ASSIGN:
		case CiToken.SUB_ASSIGN:
		case CiToken.MUL_ASSIGN:
		case CiToken.DIV_ASSIGN:
		case CiToken.MOD_ASSIGN:
		case CiToken.SHIFT_LEFT_ASSIGN:
		case CiToken.SHIFT_RIGHT_ASSIGN:
		case CiToken.AND_ASSIGN:
		case CiToken.OR_ASSIGN:
		case CiToken.XOR_ASSIGN:
			return true;
		default:
			return false;
		}
	}

	getOpString()
	{
		switch (this.op) {
		case CiToken.PLUS:
			return "+";
		case CiToken.MINUS:
			return "-";
		case CiToken.ASTERISK:
			return "*";
		case CiToken.SLASH:
			return "/";
		case CiToken.MOD:
			return "%";
		case CiToken.SHIFT_LEFT:
			return "<<";
		case CiToken.SHIFT_RIGHT:
			return ">>";
		case CiToken.LESS:
			return "<";
		case CiToken.LESS_OR_EQUAL:
			return "<=";
		case CiToken.GREATER:
			return ">";
		case CiToken.GREATER_OR_EQUAL:
			return ">=";
		case CiToken.EQUAL:
			return "==";
		case CiToken.NOT_EQUAL:
			return "!=";
		case CiToken.AND:
			return "&";
		case CiToken.OR:
			return "|";
		case CiToken.XOR:
			return "^";
		case CiToken.COND_AND:
			return "&&";
		case CiToken.COND_OR:
			return "||";
		case CiToken.ASSIGN:
			return "=";
		case CiToken.ADD_ASSIGN:
			return "+=";
		case CiToken.SUB_ASSIGN:
			return "-=";
		case CiToken.MUL_ASSIGN:
			return "*=";
		case CiToken.DIV_ASSIGN:
			return "/=";
		case CiToken.MOD_ASSIGN:
			return "%=";
		case CiToken.SHIFT_LEFT_ASSIGN:
			return "<<=";
		case CiToken.SHIFT_RIGHT_ASSIGN:
			return ">>=";
		case CiToken.AND_ASSIGN:
			return "&=";
		case CiToken.OR_ASSIGN:
			return "|=";
		case CiToken.XOR_ASSIGN:
			return "^=";
		default:
			throw new Error();
		}
	}

	toString()
	{
		return this.op == CiToken.LEFT_BRACKET ? `${this.left}[${this.right}]` : `(${this.left} ${this.getOpString()} ${this.right})`;
	}
}

export class CiSelectExpr extends CiExpr
{
	cond;
	onTrue;
	onFalse;

	accept(visitor, parent)
	{
		visitor.visitSelectExpr(this, parent);
	}

	toString()
	{
		return `(${this.cond} ? ${this.onTrue} : ${this.onFalse})`;
	}
}

export class CiCallExpr extends CiExpr
{
	method;
	arguments = [];

	accept(visitor, parent)
	{
		visitor.visitCallExpr(this, parent);
	}
}

export class CiLambdaExpr extends CiScope
{
	body;

	accept(visitor, parent)
	{
		visitor.visitLambdaExpr(this);
	}
}

export class CiCondCompletionStatement extends CiScope
{
	#completesNormallyValue;

	completesNormally()
	{
		return this.#completesNormallyValue;
	}

	setCompletesNormally(value)
	{
		this.#completesNormallyValue = value;
	}
}

export class CiBlock extends CiCondCompletionStatement
{
	statements = [];

	acceptStatement(visitor)
	{
		visitor.visitBlock(this);
	}
}

export class CiAssert extends CiStatement
{
	cond;
	message = null;

	completesNormally()
	{
		return !(this.cond instanceof CiLiteralFalse);
	}

	acceptStatement(visitor)
	{
		visitor.visitAssert(this);
	}
}

export class CiLoop extends CiCondCompletionStatement
{
	cond;
	body;
	hasBreak = false;
}

export class CiBreak extends CiStatement
{
	loopOrSwitch;

	completesNormally()
	{
		return false;
	}

	acceptStatement(visitor)
	{
		visitor.visitBreak(this);
	}
}

export class CiContinue extends CiStatement
{
	loop;

	completesNormally()
	{
		return false;
	}

	acceptStatement(visitor)
	{
		visitor.visitContinue(this);
	}
}

export class CiDoWhile extends CiLoop
{

	acceptStatement(visitor)
	{
		visitor.visitDoWhile(this);
	}
}

export class CiFor extends CiLoop
{
	init;
	advance;
	isRange = false;
	isIteratorUsed;
	rangeStep;

	acceptStatement(visitor)
	{
		visitor.visitFor(this);
	}
}

export class CiForeach extends CiLoop
{
	collection;

	acceptStatement(visitor)
	{
		visitor.visitForeach(this);
	}

	getVar()
	{
		return this.firstParameter();
	}

	getValueVar()
	{
		return this.firstParameter().nextParameter();
	}
}

export class CiIf extends CiCondCompletionStatement
{
	cond;
	onTrue;
	onFalse;

	acceptStatement(visitor)
	{
		visitor.visitIf(this);
	}
}

export class CiLock extends CiStatement
{
	lock;
	body;

	completesNormally()
	{
		return this.body.completesNormally();
	}

	acceptStatement(visitor)
	{
		visitor.visitLock(this);
	}
}

export class CiNative extends CiStatement
{
	content;

	completesNormally()
	{
		return true;
	}

	acceptStatement(visitor)
	{
		visitor.visitNative(this);
	}
}

export class CiReturn extends CiStatement
{
	value;

	completesNormally()
	{
		return false;
	}

	acceptStatement(visitor)
	{
		visitor.visitReturn(this);
	}
}

export class CiCase
{
	values = [];
	body = [];
}

export class CiSwitch extends CiCondCompletionStatement
{
	value;
	cases = [];
	defaultBody = [];

	acceptStatement(visitor)
	{
		visitor.visitSwitch(this);
	}

	isTypeMatching()
	{
		let klass;
		return (klass = this.value.type) instanceof CiClassType && klass.class.id != CiId.STRING_CLASS;
	}

	hasWhen()
	{
		return this.cases.some(kase => kase.values.some(value => {
			let when1;
			return (when1 = value) instanceof CiBinaryExpr && when1.op == CiToken.WHEN;
		}
		));
	}

	static lengthWithoutTrailingBreak(body)
	{
		let length = body.length;
		if (length > 0 && body[length - 1] instanceof CiBreak)
			length--;
		return length;
	}

	hasDefault()
	{
		return CiSwitch.lengthWithoutTrailingBreak(this.defaultBody) > 0;
	}

	static #hasBreak(statement)
	{
		if (statement instanceof CiBreak)
			return true;
		else if (statement instanceof CiIf) {
			const ifStatement = statement;
			return CiSwitch.#hasBreak(ifStatement.onTrue) || (ifStatement.onFalse != null && CiSwitch.#hasBreak(ifStatement.onFalse));
		}
		else if (statement instanceof CiBlock) {
			const block = statement;
			return block.statements.some(child => CiSwitch.#hasBreak(child));
		}
		else
			return false;
	}

	static hasEarlyBreak(body)
	{
		let length = CiSwitch.lengthWithoutTrailingBreak(body);
		for (let i = 0; i < length; i++) {
			if (CiSwitch.#hasBreak(body[i]))
				return true;
		}
		return false;
	}

	static #listHasContinue(statements)
	{
		return statements.some(statement => CiSwitch.#hasContinue(statement));
	}

	static #hasContinue(statement)
	{
		if (statement instanceof CiContinue)
			return true;
		else if (statement instanceof CiIf) {
			const ifStatement = statement;
			return CiSwitch.#hasContinue(ifStatement.onTrue) || (ifStatement.onFalse != null && CiSwitch.#hasContinue(ifStatement.onFalse));
		}
		else if (statement instanceof CiSwitch) {
			const switchStatement = statement;
			return switchStatement.cases.some(kase => CiSwitch.#listHasContinue(kase.body)) || CiSwitch.#listHasContinue(switchStatement.defaultBody);
		}
		else if (statement instanceof CiBlock) {
			const block = statement;
			return CiSwitch.#listHasContinue(block.statements);
		}
		else
			return false;
	}

	static hasEarlyBreakAndContinue(body)
	{
		return CiSwitch.hasEarlyBreak(body) && CiSwitch.#listHasContinue(body);
	}
}

export class CiThrow extends CiStatement
{
	message;

	completesNormally()
	{
		return false;
	}

	acceptStatement(visitor)
	{
		visitor.visitThrow(this);
	}
}

export class CiWhile extends CiLoop
{

	acceptStatement(visitor)
	{
		visitor.visitWhile(this);
	}
}

export class CiParameters extends CiScope
{
}

export class CiType extends CiScope
{
	nullable = false;

	getArraySuffix()
	{
		return "";
	}

	isAssignableFrom(right)
	{
		return this == right;
	}

	equalsType(right)
	{
		return this == right;
	}

	isArray()
	{
		return false;
	}

	isFinal()
	{
		return false;
	}

	getBaseType()
	{
		return this;
	}

	getStorageType()
	{
		return this;
	}

	asClassType()
	{
		const klass = this;
		return klass;
	}
}

export class CiNumericType extends CiType
{
}

export class CiIntegerType extends CiNumericType
{

	isAssignableFrom(right)
	{
		return right instanceof CiIntegerType || right.id == CiId.FLOAT_INT_TYPE;
	}
}

export class CiRangeType extends CiIntegerType
{
	min;
	max;

	static #addMinMaxValue(target, name, value)
	{
		let type = target.min == target.max ? target : Object.assign(new CiRangeType(), { min: value, max: value });
		target.add(Object.assign(new CiConst(), { visibility: CiVisibility.PUBLIC, name: name, value: Object.assign(new CiLiteralLong(), { type: type, value: BigInt(value) }), visitStatus: CiVisitStatus.DONE }));
	}

	static new(min, max)
	{
		console.assert(min <= max);
		let result = Object.assign(new CiRangeType(), { id: min >= 0 && max <= 255 ? CiId.BYTE_RANGE : min >= -128 && max <= 127 ? CiId.S_BYTE_RANGE : min >= -32768 && max <= 32767 ? CiId.SHORT_RANGE : min >= 0 && max <= 65535 ? CiId.U_SHORT_RANGE : CiId.INT_TYPE, min: min, max: max });
		CiRangeType.#addMinMaxValue(result, "MinValue", min);
		CiRangeType.#addMinMaxValue(result, "MaxValue", max);
		return result;
	}

	toString()
	{
		return this.min == this.max ? `${this.min}` : `(${this.min} .. ${this.max})`;
	}

	isAssignableFrom(right)
	{
		if (right instanceof CiRangeType) {
			const range = right;
			return this.min <= range.max && this.max >= range.min;
		}
		else if (right instanceof CiIntegerType)
			return true;
		else
			return right.id == CiId.FLOAT_INT_TYPE;
	}

	equalsType(right)
	{
		let that;
		return (that = right) instanceof CiRangeType && this.min == that.min && this.max == that.max;
	}

	static getMask(v)
	{
		v |= v >> 1;
		v |= v >> 2;
		v |= v >> 4;
		v |= v >> 8;
		v |= v >> 16;
		return v;
	}

	getVariableBits()
	{
		return CiRangeType.getMask(this.min ^ this.max);
	}
}

export class CiFloatingType extends CiNumericType
{

	isAssignableFrom(right)
	{
		return right instanceof CiNumericType;
	}
}

export class CiNamedValue extends CiSymbol
{
	typeExpr;
	value;

	isAssignableStorage()
	{
		return this.type instanceof CiStorageType && !(this.type instanceof CiArrayStorageType) && this.value instanceof CiLiteralNull;
	}
}

export class CiMember extends CiNamedValue
{
	constructor()
	{
		super();
	}
	visibility;
}

export class CiVar extends CiNamedValue
{
	isAssigned = false;

	static new(type, name, defaultValue = null)
	{
		return Object.assign(new CiVar(), { type: type, name: name, value: defaultValue });
	}

	accept(visitor, parent)
	{
		visitor.visitVar(this);
	}

	nextParameter()
	{
		const def = this.next;
		return def;
	}
}

export const CiVisitStatus = {
	NOT_YET : 0,
	IN_PROGRESS : 1,
	DONE : 2
}

export class CiConst extends CiMember
{
	inMethod;
	visitStatus;

	acceptStatement(visitor)
	{
		visitor.visitConst(this);
	}

	isStatic()
	{
		return true;
	}
}

export class CiField extends CiMember
{

	isStatic()
	{
		return false;
	}
}

export class CiProperty extends CiMember
{

	isStatic()
	{
		return false;
	}

	static new(type, id, name)
	{
		return Object.assign(new CiProperty(), { visibility: CiVisibility.PUBLIC, type: type, id: id, name: name });
	}
}

export class CiStaticProperty extends CiMember
{

	isStatic()
	{
		return true;
	}

	static new(type, id, name)
	{
		return Object.assign(new CiStaticProperty(), { visibility: CiVisibility.PUBLIC, type: type, id: id, name: name });
	}
}

export class CiMethodBase extends CiMember
{
	isMutator = false;
	throws;
	body;
	isLive = false;
	calls = new Set();

	isStatic()
	{
		return false;
	}
}

export class CiMethod extends CiMethodBase
{
	callType;
	parameters = new CiParameters();
	methodScope = new CiScope();

	static new(visibility, type, id, name, param0 = null, param1 = null, param2 = null, param3 = null)
	{
		let result = Object.assign(new CiMethod(), { visibility: visibility, callType: CiCallType.NORMAL, type: type, id: id, name: name });
		if (param0 != null) {
			result.parameters.add(param0);
			if (param1 != null) {
				result.parameters.add(param1);
				if (param2 != null) {
					result.parameters.add(param2);
					if (param3 != null)
						result.parameters.add(param3);
				}
			}
		}
		return result;
	}

	static newStatic(type, id, name, param0, param1 = null, param2 = null)
	{
		let result = CiMethod.new(CiVisibility.PUBLIC, type, id, name, param0, param1, param2);
		result.callType = CiCallType.STATIC;
		return result;
	}

	static newMutator(visibility, type, id, name, param0 = null, param1 = null, param2 = null)
	{
		let result = CiMethod.new(visibility, type, id, name, param0, param1, param2);
		result.isMutator = true;
		return result;
	}

	isStatic()
	{
		return this.callType == CiCallType.STATIC;
	}

	isAbstractOrVirtual()
	{
		return this.callType == CiCallType.ABSTRACT || this.callType == CiCallType.VIRTUAL;
	}

	getDeclaringMethod()
	{
		let method = this;
		while (method.callType == CiCallType.OVERRIDE) {
			const baseMethod = method.parent.parent.tryLookup(method.name, false);
			method = baseMethod;
		}
		return method;
	}

	isToString()
	{
		return this.name == "ToString" && this.callType != CiCallType.STATIC && this.parameters.count() == 0;
	}
}

export class CiMethodGroup extends CiMember
{
	constructor()
	{
		super();
	}
	methods = new Array(2);

	isStatic()
	{
		throw new Error();
	}

	static new(method0, method1)
	{
		let result = Object.assign(new CiMethodGroup(), { visibility: method0.visibility, name: method0.name });
		result.methods[0] = method0;
		result.methods[1] = method1;
		return result;
	}
}

export class CiContainerType extends CiType
{
	isPublic;
	filename;
}

export class CiEnum extends CiContainerType
{
	hasExplicitValue = false;

	getFirstValue()
	{
		let symbol = this.first;
		while (!(symbol instanceof CiConst))
			symbol = symbol.next;
		return symbol;
	}

	acceptValues(visitor)
	{
		let previous = null;
		for (let symbol = this.first; symbol != null; symbol = symbol.next) {
			let konst;
			if ((konst = symbol) instanceof CiConst) {
				visitor.visitEnumValue(konst, previous);
				previous = konst;
			}
		}
	}
}

export class CiEnumFlags extends CiEnum
{
}

export class CiClass extends CiContainerType
{
	constructor()
	{
		super();
		this.add(CiVar.new(Object.assign(new CiReadWriteClassType(), { class: this }), "this"));
	}
	callType;
	typeParameterCount = 0;
	hasSubclasses = false;
	baseClassName = "";
	constructor_;
	constArrays = [];

	hasBaseClass()
	{
		return this.baseClassName.length > 0;
	}

	addsVirtualMethods()
	{
		for (let symbol = this.first; symbol != null; symbol = symbol.next) {
			let method;
			if ((method = symbol) instanceof CiMethod && method.isAbstractOrVirtual())
				return true;
		}
		return false;
	}

	static new(callType, id, name, typeParameterCount = 0)
	{
		return Object.assign(new CiClass(), { callType: callType, id: id, name: name, typeParameterCount: typeParameterCount });
	}

	isSameOrBaseOf(derived)
	{
		while (derived != this) {
			let parent;
			if ((parent = derived.parent) instanceof CiClass)
				derived = parent;
			else
				return false;
		}
		return true;
	}

	hasToString()
	{
		let method;
		return (method = this.tryLookup("ToString", false)) instanceof CiMethod && method.isToString();
	}

	addsToString()
	{
		let method;
		return this.dict.hasOwnProperty("ToString") && (method = this.dict["ToString"]) instanceof CiMethod && method.isToString() && method.callType != CiCallType.OVERRIDE && method.callType != CiCallType.SEALED;
	}
}

export class CiClassType extends CiType
{
	class;
	typeArg0;
	typeArg1;

	getElementType()
	{
		return this.typeArg0;
	}

	getKeyType()
	{
		return this.typeArg0;
	}

	getValueType()
	{
		return this.typeArg1;
	}

	isArray()
	{
		return this.class.id == CiId.ARRAY_PTR_CLASS;
	}

	getBaseType()
	{
		return this.isArray() ? this.getElementType().getBaseType() : this;
	}

	equalTypeArguments(right)
	{
		switch (this.class.typeParameterCount) {
		case 0:
			return true;
		case 1:
			return this.typeArg0.equalsType(right.typeArg0);
		case 2:
			return this.typeArg0.equalsType(right.typeArg0) && this.typeArg1.equalsType(right.typeArg1);
		default:
			throw new Error();
		}
	}

	isAssignableFromClass(right)
	{
		return this.class.isSameOrBaseOf(right.class) && this.equalTypeArguments(right);
	}

	isAssignableFrom(right)
	{
		let rightClass;
		return (this.nullable && right.id == CiId.NULL_TYPE) || ((rightClass = right) instanceof CiClassType && this.isAssignableFromClass(rightClass));
	}

	equalsTypeInternal(that)
	{
		return this.nullable == that.nullable && this.class == that.class && this.equalTypeArguments(that);
	}

	equalsType(right)
	{
		let that;
		return (that = right) instanceof CiClassType && !(right instanceof CiReadWriteClassType) && this.equalsTypeInternal(that);
	}

	getArraySuffix()
	{
		return this.isArray() ? "[]" : "";
	}

	getClassSuffix()
	{
		return "";
	}

	#getNullableSuffix()
	{
		return this.nullable ? "?" : "";
	}

	toString()
	{
		if (this.isArray())
			return `${this.getElementType().getBaseType()}${this.getArraySuffix()}${this.#getNullableSuffix()}${this.getElementType().getArraySuffix()}`;
		switch (this.class.typeParameterCount) {
		case 0:
			return `${this.class.name}${this.getClassSuffix()}${this.#getNullableSuffix()}`;
		case 1:
			return `${this.class.name}<${this.typeArg0}>${this.getClassSuffix()}${this.#getNullableSuffix()}`;
		case 2:
			return `${this.class.name}<${this.typeArg0}, ${this.typeArg1}>${this.getClassSuffix()}${this.#getNullableSuffix()}`;
		default:
			throw new Error();
		}
	}
}

export class CiReadWriteClassType extends CiClassType
{

	isAssignableFrom(right)
	{
		let rightClass;
		return (this.nullable && right.id == CiId.NULL_TYPE) || ((rightClass = right) instanceof CiReadWriteClassType && this.isAssignableFromClass(rightClass));
	}

	equalsType(right)
	{
		let that;
		return (that = right) instanceof CiReadWriteClassType && !(right instanceof CiStorageType) && !(right instanceof CiDynamicPtrType) && this.equalsTypeInternal(that);
	}

	getArraySuffix()
	{
		return this.isArray() ? "[]!" : "";
	}

	getClassSuffix()
	{
		return "!";
	}
}

export class CiStorageType extends CiReadWriteClassType
{

	isFinal()
	{
		return this.class.id != CiId.MATCH_CLASS;
	}

	isAssignableFrom(right)
	{
		let rightClass;
		return (rightClass = right) instanceof CiStorageType && this.class == rightClass.class && this.equalTypeArguments(rightClass);
	}

	equalsType(right)
	{
		let that;
		return (that = right) instanceof CiStorageType && this.equalsTypeInternal(that);
	}

	getClassSuffix()
	{
		return "()";
	}
}

export class CiDynamicPtrType extends CiReadWriteClassType
{

	isAssignableFrom(right)
	{
		let rightClass;
		return (this.nullable && right.id == CiId.NULL_TYPE) || ((rightClass = right) instanceof CiDynamicPtrType && this.isAssignableFromClass(rightClass));
	}

	equalsType(right)
	{
		let that;
		return (that = right) instanceof CiDynamicPtrType && this.equalsTypeInternal(that);
	}

	getArraySuffix()
	{
		return this.isArray() ? "[]#" : "";
	}

	getClassSuffix()
	{
		return "#";
	}
}

export class CiArrayStorageType extends CiStorageType
{
	lengthExpr;
	length;
	ptrTaken = false;

	getBaseType()
	{
		return this.getElementType().getBaseType();
	}

	isArray()
	{
		return true;
	}

	getArraySuffix()
	{
		return `[${this.length}]`;
	}

	equalsType(right)
	{
		let that;
		return (that = right) instanceof CiArrayStorageType && this.getElementType().equalsType(that.getElementType()) && this.length == that.length;
	}

	getStorageType()
	{
		return this.getElementType().getStorageType();
	}
}

export class CiStringType extends CiClassType
{
}

export class CiStringStorageType extends CiStringType
{

	isAssignableFrom(right)
	{
		return right instanceof CiStringType;
	}

	getClassSuffix()
	{
		return "()";
	}
}

export class CiPrintableType extends CiType
{

	isAssignableFrom(right)
	{
		if (right instanceof CiNumericType || right instanceof CiStringType)
			return true;
		else if (right instanceof CiClassType) {
			const klass = right;
			return klass.class.hasToString();
		}
		else
			return false;
	}
}

export class CiSystem extends CiScope
{
	constructor()
	{
		super();
		this.parent = null;
		let basePtr = CiVar.new(null, "base");
		basePtr.id = CiId.BASE_PTR;
		this.add(basePtr);
		this.#addMinMaxValue(this.intType, -2147483648n, 2147483647n);
		this.intType.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.boolType, CiId.INT_TRY_PARSE, "TryParse", CiVar.new(this.stringPtrType, "value"), CiVar.new(this.intType, "radix", this.newLiteralLong(0n))));
		this.add(this.intType);
		this.#uIntType.name = "uint";
		this.add(this.#uIntType);
		this.#addMinMaxValue(this.longType, -9223372036854775808n, 9223372036854775807n);
		this.longType.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.boolType, CiId.LONG_TRY_PARSE, "TryParse", CiVar.new(this.stringPtrType, "value"), CiVar.new(this.intType, "radix", this.newLiteralLong(0n))));
		this.add(this.longType);
		this.byteType.name = "byte";
		this.add(this.byteType);
		let shortType = CiRangeType.new(-32768, 32767);
		shortType.name = "short";
		this.add(shortType);
		let ushortType = CiRangeType.new(0, 65535);
		ushortType.name = "ushort";
		this.add(ushortType);
		let minus1Type = CiRangeType.new(-1, 2147483647);
		this.add(this.#floatType);
		this.doubleType.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.boolType, CiId.DOUBLE_TRY_PARSE, "TryParse", CiVar.new(this.stringPtrType, "value")));
		this.add(this.doubleType);
		this.add(this.boolType);
		this.#stringClass.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.STRING_CONTAINS, "Contains", CiVar.new(this.stringPtrType, "value")));
		this.#stringClass.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.STRING_ENDS_WITH, "EndsWith", CiVar.new(this.stringPtrType, "value")));
		this.#stringClass.add(CiMethod.new(CiVisibility.PUBLIC, minus1Type, CiId.STRING_INDEX_OF, "IndexOf", CiVar.new(this.stringPtrType, "value")));
		this.#stringClass.add(CiMethod.new(CiVisibility.PUBLIC, minus1Type, CiId.STRING_LAST_INDEX_OF, "LastIndexOf", CiVar.new(this.stringPtrType, "value")));
		this.#stringClass.add(CiProperty.new(this.#uIntType, CiId.STRING_LENGTH, "Length"));
		this.#stringClass.add(CiMethod.new(CiVisibility.PUBLIC, this.stringStorageType, CiId.STRING_REPLACE, "Replace", CiVar.new(this.stringPtrType, "oldValue"), CiVar.new(this.stringPtrType, "newValue")));
		this.#stringClass.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.STRING_STARTS_WITH, "StartsWith", CiVar.new(this.stringPtrType, "value")));
		this.#stringClass.add(CiMethod.new(CiVisibility.PUBLIC, this.stringStorageType, CiId.STRING_SUBSTRING, "Substring", CiVar.new(this.intType, "offset"), CiVar.new(this.intType, "length", this.newLiteralLong(-1n))));
		this.stringPtrType.class = this.#stringClass;
		this.add(this.stringPtrType);
		this.stringNullablePtrType.class = this.#stringClass;
		this.stringStorageType.class = this.#stringClass;
		let arrayBinarySearchPart = CiMethod.new(CiVisibility.NUMERIC_ELEMENT_TYPE, this.intType, CiId.ARRAY_BINARY_SEARCH_PART, "BinarySearch", CiVar.new(this.#typeParam0, "value"), CiVar.new(this.intType, "startIndex"), CiVar.new(this.intType, "count"));
		this.arrayPtrClass.add(arrayBinarySearchPart);
		this.arrayPtrClass.add(CiMethod.new(CiVisibility.PUBLIC, this.voidType, CiId.ARRAY_COPY_TO, "CopyTo", CiVar.new(this.intType, "sourceIndex"), CiVar.new(Object.assign(new CiReadWriteClassType(), { class: this.arrayPtrClass, typeArg0: this.#typeParam0 }), "destinationArray"), CiVar.new(this.intType, "destinationIndex"), CiVar.new(this.intType, "count")));
		let arrayFillPart = CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.ARRAY_FILL_PART, "Fill", CiVar.new(this.#typeParam0, "value"), CiVar.new(this.intType, "startIndex"), CiVar.new(this.intType, "count"));
		this.arrayPtrClass.add(arrayFillPart);
		let arraySortPart = CiMethod.newMutator(CiVisibility.NUMERIC_ELEMENT_TYPE, this.voidType, CiId.ARRAY_SORT_PART, "Sort", CiVar.new(this.intType, "startIndex"), CiVar.new(this.intType, "count"));
		this.arrayPtrClass.add(arraySortPart);
		this.arrayStorageClass.parent = this.arrayPtrClass;
		this.arrayStorageClass.add(CiMethodGroup.new(CiMethod.new(CiVisibility.NUMERIC_ELEMENT_TYPE, this.intType, CiId.ARRAY_BINARY_SEARCH_ALL, "BinarySearch", CiVar.new(this.#typeParam0, "value")), arrayBinarySearchPart));
		this.arrayStorageClass.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.ARRAY_CONTAINS, "Contains", CiVar.new(this.#typeParam0, "value")));
		this.arrayStorageClass.add(CiMethodGroup.new(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.ARRAY_FILL_ALL, "Fill", CiVar.new(this.#typeParam0, "value")), arrayFillPart));
		this.arrayStorageClass.add(CiProperty.new(this.#uIntType, CiId.ARRAY_LENGTH, "Length"));
		this.arrayStorageClass.add(CiMethodGroup.new(CiMethod.newMutator(CiVisibility.NUMERIC_ELEMENT_TYPE, this.voidType, CiId.ARRAY_SORT_ALL, "Sort"), arraySortPart));
		let typeParam0NotFinal = Object.assign(new CiType(), { id: CiId.TYPE_PARAM0_NOT_FINAL, name: "T" });
		let typeParam0Predicate = Object.assign(new CiType(), { id: CiId.TYPE_PARAM0_PREDICATE, name: "Predicate<T>" });
		let listClass = this.#addCollection(CiId.LIST_CLASS, "List", 1, CiId.LIST_CLEAR, CiId.LIST_COUNT);
		listClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.LIST_ADD, "Add", CiVar.new(typeParam0NotFinal, "value")));
		listClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.LIST_ADD_RANGE, "AddRange", CiVar.new(Object.assign(new CiClassType(), { class: listClass, typeArg0: this.#typeParam0 }), "source")));
		listClass.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.LIST_ALL, "All", CiVar.new(typeParam0Predicate, "predicate")));
		listClass.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.LIST_ANY, "Any", CiVar.new(typeParam0Predicate, "predicate")));
		listClass.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.LIST_CONTAINS, "Contains", CiVar.new(this.#typeParam0, "value")));
		listClass.add(CiMethod.new(CiVisibility.PUBLIC, this.voidType, CiId.LIST_COPY_TO, "CopyTo", CiVar.new(this.intType, "sourceIndex"), CiVar.new(Object.assign(new CiReadWriteClassType(), { class: this.arrayPtrClass, typeArg0: this.#typeParam0 }), "destinationArray"), CiVar.new(this.intType, "destinationIndex"), CiVar.new(this.intType, "count")));
		listClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.intType, CiId.LIST_INDEX_OF, "IndexOf", CiVar.new(this.#typeParam0, "value")));
		listClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.LIST_INSERT, "Insert", CiVar.new(this.#uIntType, "index"), CiVar.new(typeParam0NotFinal, "value")));
		listClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.#typeParam0, CiId.LIST_LAST, "Last"));
		listClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.LIST_REMOVE_AT, "RemoveAt", CiVar.new(this.intType, "index")));
		listClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.LIST_REMOVE_RANGE, "RemoveRange", CiVar.new(this.intType, "index"), CiVar.new(this.intType, "count")));
		listClass.add(CiMethodGroup.new(CiMethod.newMutator(CiVisibility.NUMERIC_ELEMENT_TYPE, this.voidType, CiId.LIST_SORT_ALL, "Sort"), CiMethod.newMutator(CiVisibility.NUMERIC_ELEMENT_TYPE, this.voidType, CiId.LIST_SORT_PART, "Sort", CiVar.new(this.intType, "startIndex"), CiVar.new(this.intType, "count"))));
		let queueClass = this.#addCollection(CiId.QUEUE_CLASS, "Queue", 1, CiId.QUEUE_CLEAR, CiId.QUEUE_COUNT);
		queueClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.#typeParam0, CiId.QUEUE_DEQUEUE, "Dequeue"));
		queueClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.QUEUE_ENQUEUE, "Enqueue", CiVar.new(this.#typeParam0, "value")));
		queueClass.add(CiMethod.new(CiVisibility.PUBLIC, this.#typeParam0, CiId.QUEUE_PEEK, "Peek"));
		let stackClass = this.#addCollection(CiId.STACK_CLASS, "Stack", 1, CiId.STACK_CLEAR, CiId.STACK_COUNT);
		stackClass.add(CiMethod.new(CiVisibility.PUBLIC, this.#typeParam0, CiId.STACK_PEEK, "Peek"));
		stackClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.STACK_PUSH, "Push", CiVar.new(this.#typeParam0, "value")));
		stackClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.#typeParam0, CiId.STACK_POP, "Pop"));
		this.#addSet(CiId.HASH_SET_CLASS, "HashSet", CiId.HASH_SET_ADD, CiId.HASH_SET_CLEAR, CiId.HASH_SET_CONTAINS, CiId.HASH_SET_COUNT, CiId.HASH_SET_REMOVE);
		this.#addSet(CiId.SORTED_SET_CLASS, "SortedSet", CiId.SORTED_SET_ADD, CiId.SORTED_SET_CLEAR, CiId.SORTED_SET_CONTAINS, CiId.SORTED_SET_COUNT, CiId.SORTED_SET_REMOVE);
		this.#addDictionary(CiId.DICTIONARY_CLASS, "Dictionary", CiId.DICTIONARY_CLEAR, CiId.DICTIONARY_CONTAINS_KEY, CiId.DICTIONARY_COUNT, CiId.DICTIONARY_REMOVE);
		this.#addDictionary(CiId.SORTED_DICTIONARY_CLASS, "SortedDictionary", CiId.SORTED_DICTIONARY_CLEAR, CiId.SORTED_DICTIONARY_CONTAINS_KEY, CiId.SORTED_DICTIONARY_COUNT, CiId.SORTED_DICTIONARY_REMOVE);
		this.#addDictionary(CiId.ORDERED_DICTIONARY_CLASS, "OrderedDictionary", CiId.ORDERED_DICTIONARY_CLEAR, CiId.ORDERED_DICTIONARY_CONTAINS_KEY, CiId.ORDERED_DICTIONARY_COUNT, CiId.ORDERED_DICTIONARY_REMOVE);
		let textWriterClass = CiClass.new(CiCallType.NORMAL, CiId.TEXT_WRITER_CLASS, "TextWriter");
		textWriterClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.TEXT_WRITER_WRITE, "Write", CiVar.new(this.printableType, "value")));
		textWriterClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.TEXT_WRITER_WRITE_CHAR, "WriteChar", CiVar.new(this.intType, "c")));
		textWriterClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.TEXT_WRITER_WRITE_CODE_POINT, "WriteCodePoint", CiVar.new(this.intType, "c")));
		textWriterClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.TEXT_WRITER_WRITE_LINE, "WriteLine", CiVar.new(this.printableType, "value", this.newLiteralString(""))));
		this.add(textWriterClass);
		let consoleClass = CiClass.new(CiCallType.STATIC, CiId.NONE, "Console");
		consoleClass.add(CiMethod.newStatic(this.voidType, CiId.CONSOLE_WRITE, "Write", CiVar.new(this.printableType, "value")));
		consoleClass.add(CiMethod.newStatic(this.voidType, CiId.CONSOLE_WRITE_LINE, "WriteLine", CiVar.new(this.printableType, "value", this.newLiteralString(""))));
		consoleClass.add(CiStaticProperty.new(Object.assign(new CiStorageType(), { class: textWriterClass }), CiId.CONSOLE_ERROR, "Error"));
		this.add(consoleClass);
		let stringWriterClass = CiClass.new(CiCallType.SEALED, CiId.STRING_WRITER_CLASS, "StringWriter");
		stringWriterClass.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, CiId.STRING_WRITER_CLEAR, "Clear"));
		stringWriterClass.add(CiMethod.new(CiVisibility.PUBLIC, this.stringPtrType, CiId.STRING_WRITER_TO_STRING, "ToString"));
		this.add(stringWriterClass);
		stringWriterClass.parent = textWriterClass;
		let utf8EncodingClass = CiClass.new(CiCallType.SEALED, CiId.NONE, "UTF8Encoding");
		utf8EncodingClass.add(CiMethod.new(CiVisibility.PUBLIC, this.intType, CiId.U_T_F8_GET_BYTE_COUNT, "GetByteCount", CiVar.new(this.stringPtrType, "str")));
		utf8EncodingClass.add(CiMethod.new(CiVisibility.PUBLIC, this.voidType, CiId.U_T_F8_GET_BYTES, "GetBytes", CiVar.new(this.stringPtrType, "str"), CiVar.new(Object.assign(new CiReadWriteClassType(), { class: this.arrayPtrClass, typeArg0: this.byteType }), "bytes"), CiVar.new(this.intType, "byteIndex")));
		utf8EncodingClass.add(CiMethod.new(CiVisibility.PUBLIC, this.stringStorageType, CiId.U_T_F8_GET_STRING, "GetString", CiVar.new(Object.assign(new CiClassType(), { class: this.arrayPtrClass, typeArg0: this.byteType }), "bytes"), CiVar.new(this.intType, "offset"), CiVar.new(this.intType, "length")));
		let encodingClass = CiClass.new(CiCallType.STATIC, CiId.NONE, "Encoding");
		encodingClass.add(CiStaticProperty.new(utf8EncodingClass, CiId.NONE, "UTF8"));
		this.add(encodingClass);
		let environmentClass = CiClass.new(CiCallType.STATIC, CiId.NONE, "Environment");
		environmentClass.add(CiMethod.newStatic(this.stringNullablePtrType, CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE, "GetEnvironmentVariable", CiVar.new(this.stringPtrType, "name")));
		this.add(environmentClass);
		this.regexOptionsEnum = this.newEnum(true);
		this.regexOptionsEnum.isPublic = true;
		this.regexOptionsEnum.id = CiId.REGEX_OPTIONS_ENUM;
		this.regexOptionsEnum.name = "RegexOptions";
		let regexOptionsNone = this.#newConstLong("None", 0n);
		CiSystem.#addEnumValue(this.regexOptionsEnum, regexOptionsNone);
		CiSystem.#addEnumValue(this.regexOptionsEnum, this.#newConstLong("IgnoreCase", 1n));
		CiSystem.#addEnumValue(this.regexOptionsEnum, this.#newConstLong("Multiline", 2n));
		CiSystem.#addEnumValue(this.regexOptionsEnum, this.#newConstLong("Singleline", 16n));
		this.add(this.regexOptionsEnum);
		let regexClass = CiClass.new(CiCallType.SEALED, CiId.REGEX_CLASS, "Regex");
		regexClass.add(CiMethod.newStatic(this.stringStorageType, CiId.REGEX_ESCAPE, "Escape", CiVar.new(this.stringPtrType, "str")));
		regexClass.add(CiMethodGroup.new(CiMethod.newStatic(this.boolType, CiId.REGEX_IS_MATCH_STR, "IsMatch", CiVar.new(this.stringPtrType, "input"), CiVar.new(this.stringPtrType, "pattern"), CiVar.new(this.regexOptionsEnum, "options", regexOptionsNone)), CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.REGEX_IS_MATCH_REGEX, "IsMatch", CiVar.new(this.stringPtrType, "input"))));
		regexClass.add(CiMethod.newStatic(Object.assign(new CiDynamicPtrType(), { class: regexClass }), CiId.REGEX_COMPILE, "Compile", CiVar.new(this.stringPtrType, "pattern"), CiVar.new(this.regexOptionsEnum, "options", regexOptionsNone)));
		this.add(regexClass);
		let matchClass = CiClass.new(CiCallType.SEALED, CiId.MATCH_CLASS, "Match");
		matchClass.add(CiMethodGroup.new(CiMethod.newMutator(CiVisibility.PUBLIC, this.boolType, CiId.MATCH_FIND_STR, "Find", CiVar.new(this.stringPtrType, "input"), CiVar.new(this.stringPtrType, "pattern"), CiVar.new(this.regexOptionsEnum, "options", regexOptionsNone)), CiMethod.newMutator(CiVisibility.PUBLIC, this.boolType, CiId.MATCH_FIND_REGEX, "Find", CiVar.new(this.stringPtrType, "input"), CiVar.new(Object.assign(new CiClassType(), { class: regexClass }), "pattern"))));
		matchClass.add(CiProperty.new(this.intType, CiId.MATCH_START, "Start"));
		matchClass.add(CiProperty.new(this.intType, CiId.MATCH_END, "End"));
		matchClass.add(CiMethod.new(CiVisibility.PUBLIC, this.stringPtrType, CiId.MATCH_GET_CAPTURE, "GetCapture", CiVar.new(this.#uIntType, "group")));
		matchClass.add(CiProperty.new(this.#uIntType, CiId.MATCH_LENGTH, "Length"));
		matchClass.add(CiProperty.new(this.stringPtrType, CiId.MATCH_VALUE, "Value"));
		this.add(matchClass);
		let floatIntType = Object.assign(new CiFloatingType(), { id: CiId.FLOAT_INT_TYPE, name: "float" });
		let mathClass = CiClass.new(CiCallType.STATIC, CiId.NONE, "Math");
		mathClass.add(CiMethodGroup.new(CiMethod.newStatic(this.intType, CiId.MATH_ABS, "Abs", CiVar.new(this.longType, "a")), CiMethod.newStatic(this.#floatType, CiId.MATH_ABS, "Abs", CiVar.new(this.doubleType, "a"))));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Acos", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Asin", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Atan", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Atan2", CiVar.new(this.doubleType, "y"), CiVar.new(this.doubleType, "x")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Cbrt", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(floatIntType, CiId.MATH_CEILING, "Ceiling", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethodGroup.new(CiMethod.newStatic(this.intType, CiId.MATH_CLAMP, "Clamp", CiVar.new(this.longType, "value"), CiVar.new(this.longType, "min"), CiVar.new(this.longType, "max")), CiMethod.newStatic(this.#floatType, CiId.MATH_CLAMP, "Clamp", CiVar.new(this.doubleType, "value"), CiVar.new(this.doubleType, "min"), CiVar.new(this.doubleType, "max"))));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Cos", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Cosh", CiVar.new(this.doubleType, "a")));
		mathClass.add(this.#newConstDouble("E", 2.718281828459045));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Exp", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(floatIntType, CiId.MATH_METHOD, "Floor", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_FUSED_MULTIPLY_ADD, "FusedMultiplyAdd", CiVar.new(this.doubleType, "x"), CiVar.new(this.doubleType, "y"), CiVar.new(this.doubleType, "z")));
		mathClass.add(CiMethod.newStatic(this.boolType, CiId.MATH_IS_FINITE, "IsFinite", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.boolType, CiId.MATH_IS_INFINITY, "IsInfinity", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.boolType, CiId.MATH_IS_NA_N, "IsNaN", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Log", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_LOG2, "Log2", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Log10", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethodGroup.new(CiMethod.newStatic(this.intType, CiId.MATH_MAX_INT, "Max", CiVar.new(this.longType, "a"), CiVar.new(this.longType, "b")), CiMethod.newStatic(this.#floatType, CiId.MATH_MAX_DOUBLE, "Max", CiVar.new(this.doubleType, "a"), CiVar.new(this.doubleType, "b"))));
		mathClass.add(CiMethodGroup.new(CiMethod.newStatic(this.intType, CiId.MATH_MIN_INT, "Min", CiVar.new(this.longType, "a"), CiVar.new(this.longType, "b")), CiMethod.newStatic(this.#floatType, CiId.MATH_MIN_DOUBLE, "Min", CiVar.new(this.doubleType, "a"), CiVar.new(this.doubleType, "b"))));
		mathClass.add(CiStaticProperty.new(this.#floatType, CiId.MATH_NA_N, "NaN"));
		mathClass.add(CiStaticProperty.new(this.#floatType, CiId.MATH_NEGATIVE_INFINITY, "NegativeInfinity"));
		mathClass.add(this.#newConstDouble("PI", 3.141592653589793));
		mathClass.add(CiStaticProperty.new(this.#floatType, CiId.MATH_POSITIVE_INFINITY, "PositiveInfinity"));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Pow", CiVar.new(this.doubleType, "x"), CiVar.new(this.doubleType, "y")));
		mathClass.add(CiMethod.newStatic(floatIntType, CiId.MATH_ROUND, "Round", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Sin", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Sinh", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Sqrt", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Tan", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(this.#floatType, CiId.MATH_METHOD, "Tanh", CiVar.new(this.doubleType, "a")));
		mathClass.add(CiMethod.newStatic(floatIntType, CiId.MATH_TRUNCATE, "Truncate", CiVar.new(this.doubleType, "a")));
		this.add(mathClass);
		let lockClass = CiClass.new(CiCallType.SEALED, CiId.LOCK_CLASS, "Lock");
		this.add(lockClass);
		this.lockPtrType.class = lockClass;
	}
	voidType = Object.assign(new CiType(), { id: CiId.VOID_TYPE, name: "void" });
	nullType = Object.assign(new CiType(), { id: CiId.NULL_TYPE, name: "null", nullable: true });
	#typeParam0 = Object.assign(new CiType(), { id: CiId.TYPE_PARAM0, name: "T" });
	intType = Object.assign(new CiIntegerType(), { id: CiId.INT_TYPE, name: "int" });
	#uIntType = CiRangeType.new(0, 2147483647);
	longType = Object.assign(new CiIntegerType(), { id: CiId.LONG_TYPE, name: "long" });
	byteType = CiRangeType.new(0, 255);
	#floatType = Object.assign(new CiFloatingType(), { id: CiId.FLOAT_TYPE, name: "float" });
	doubleType = Object.assign(new CiFloatingType(), { id: CiId.DOUBLE_TYPE, name: "double" });
	charType = CiRangeType.new(-128, 65535);
	boolType = Object.assign(new CiEnum(), { id: CiId.BOOL_TYPE, name: "bool" });
	#stringClass = CiClass.new(CiCallType.NORMAL, CiId.STRING_CLASS, "string");
	stringPtrType = Object.assign(new CiStringType(), { id: CiId.STRING_PTR_TYPE, name: "string" });
	stringNullablePtrType = Object.assign(new CiStringType(), { id: CiId.STRING_PTR_TYPE, name: "string", nullable: true });
	stringStorageType = Object.assign(new CiStringStorageType(), { id: CiId.STRING_STORAGE_TYPE });
	printableType = Object.assign(new CiPrintableType(), { name: "printable" });
	arrayPtrClass = CiClass.new(CiCallType.NORMAL, CiId.ARRAY_PTR_CLASS, "ArrayPtr", 1);
	arrayStorageClass = CiClass.new(CiCallType.NORMAL, CiId.ARRAY_STORAGE_CLASS, "ArrayStorage", 1);
	regexOptionsEnum;
	lockPtrType = new CiReadWriteClassType();

	newLiteralLong(value, line = 0)
	{
		let type = value >= -2147483648 && value <= 2147483647 ? CiRangeType.new(Number(value), Number(value)) : this.longType;
		return Object.assign(new CiLiteralLong(), { line: line, type: type, value: value });
	}

	newLiteralString(value, line = 0)
	{
		return Object.assign(new CiLiteralString(), { line: line, type: this.stringPtrType, value: value });
	}

	promoteIntegerTypes(left, right)
	{
		return left == this.longType || right == this.longType ? this.longType : this.intType;
	}

	promoteFloatingTypes(left, right)
	{
		if (left.id == CiId.DOUBLE_TYPE || right.id == CiId.DOUBLE_TYPE)
			return this.doubleType;
		if (left.id == CiId.FLOAT_TYPE || right.id == CiId.FLOAT_TYPE || left.id == CiId.FLOAT_INT_TYPE || right.id == CiId.FLOAT_INT_TYPE)
			return this.#floatType;
		return null;
	}

	promoteNumericTypes(left, right)
	{
		let result = this.promoteFloatingTypes(left, right);
		return result != null ? result : this.promoteIntegerTypes(left, right);
	}

	newEnum(flags)
	{
		let enu = flags ? new CiEnumFlags() : new CiEnum();
		enu.add(CiMethod.newStatic(enu, CiId.ENUM_FROM_INT, "FromInt", CiVar.new(this.intType, "value")));
		if (flags)
			enu.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, CiId.ENUM_HAS_FLAG, "HasFlag", CiVar.new(enu, "flag")));
		return enu;
	}

	#addCollection(id, name, typeParameterCount, clearId, countId)
	{
		let result = CiClass.new(CiCallType.NORMAL, id, name, typeParameterCount);
		result.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, clearId, "Clear"));
		result.add(CiProperty.new(this.#uIntType, countId, "Count"));
		this.add(result);
		return result;
	}

	#addSet(id, name, addId, clearId, containsId, countId, removeId)
	{
		let set = this.#addCollection(id, name, 1, clearId, countId);
		set.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, addId, "Add", CiVar.new(this.#typeParam0, "value")));
		set.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, containsId, "Contains", CiVar.new(this.#typeParam0, "value")));
		set.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, removeId, "Remove", CiVar.new(this.#typeParam0, "value")));
	}

	#addDictionary(id, name, clearId, containsKeyId, countId, removeId)
	{
		let dict = this.#addCollection(id, name, 2, clearId, countId);
		dict.add(CiMethod.newMutator(CiVisibility.FINAL_VALUE_TYPE, this.voidType, CiId.DICTIONARY_ADD, "Add", CiVar.new(this.#typeParam0, "key")));
		dict.add(CiMethod.new(CiVisibility.PUBLIC, this.boolType, containsKeyId, "ContainsKey", CiVar.new(this.#typeParam0, "key")));
		dict.add(CiMethod.newMutator(CiVisibility.PUBLIC, this.voidType, removeId, "Remove", CiVar.new(this.#typeParam0, "key")));
	}

	static #addEnumValue(enu, value)
	{
		value.type = enu;
		enu.add(value);
	}

	#newConstLong(name, value)
	{
		let result = Object.assign(new CiConst(), { visibility: CiVisibility.PUBLIC, name: name, value: this.newLiteralLong(value), visitStatus: CiVisitStatus.DONE });
		result.type = result.value.type;
		return result;
	}

	#newConstDouble(name, value)
	{
		return Object.assign(new CiConst(), { visibility: CiVisibility.PUBLIC, name: name, value: Object.assign(new CiLiteralDouble(), { value: value, type: this.doubleType }), type: this.doubleType, visitStatus: CiVisitStatus.DONE });
	}

	#addMinMaxValue(target, min, max)
	{
		target.add(this.#newConstLong("MinValue", min));
		target.add(this.#newConstLong("MaxValue", max));
	}

	static new()
	{
		return new CiSystem();
	}
}

export class CiProgram extends CiScope
{
	system;
	topLevelNatives = [];
	classes = [];
	resources = {};
	regexOptionsEnum = false;
}

export class CiParser extends CiLexer
{
	program;
	#xcrementParent = null;
	#currentLoop = null;
	#currentLoopOrSwitch = null;

	#docParseLine(para)
	{
		if (para.children.length > 0)
			para.children.push(new CiDocLine());
		this.lexemeOffset = this.charOffset;
		for (let lastNonWhitespace = 0;;) {
			switch (this.peekChar()) {
			case -1:
			case 10:
			case 13:
				para.children.push(Object.assign(new CiDocText(), { text: this.getLexeme() }));
				return lastNonWhitespace == 46;
			case 9:
			case 32:
				this.readChar();
				break;
			case 96:
				if (this.charOffset > this.lexemeOffset)
					para.children.push(Object.assign(new CiDocText(), { text: this.getLexeme() }));
				this.readChar();
				this.lexemeOffset = this.charOffset;
				for (;;) {
					let c = this.peekChar();
					if (c == 96) {
						para.children.push(Object.assign(new CiDocCode(), { text: this.getLexeme() }));
						this.readChar();
						break;
					}
					if (c < 0 || c == 10) {
						this.reportError("Unterminated code in documentation comment");
						break;
					}
					this.readChar();
				}
				this.lexemeOffset = this.charOffset;
				lastNonWhitespace = 96;
				break;
			default:
				lastNonWhitespace = this.readChar();
				break;
			}
		}
	}

	#docParsePara(para)
	{
		do {
			this.#docParseLine(para);
			this.nextToken();
		}
		while (this.see(CiToken.DOC_REGULAR));
	}

	#parseDoc()
	{
		if (!this.see(CiToken.DOC_REGULAR))
			return null;
		let doc = new CiCodeDoc();
		let period;
		do {
			period = this.#docParseLine(doc.summary);
			this.nextToken();
		}
		while (!period && this.see(CiToken.DOC_REGULAR));
		for (;;) {
			switch (this.currentToken) {
			case CiToken.DOC_REGULAR:
				let para = new CiDocPara();
				this.#docParsePara(para);
				doc.details.push(para);
				break;
			case CiToken.DOC_BULLET:
				let list = new CiDocList();
				do {
					list.items.push(new CiDocPara());
					this.#docParsePara(list.items.at(-1));
				}
				while (this.see(CiToken.DOC_BULLET));
				doc.details.push(list);
				break;
			case CiToken.DOC_BLANK:
				this.nextToken();
				break;
			default:
				return doc;
			}
		}
	}

	#checkXcrementParent()
	{
		if (this.#xcrementParent != null) {
			let op = this.see(CiToken.INCREMENT) ? "++" : "--";
			this.reportError(`${op} not allowed on the right side of ${this.#xcrementParent}`);
		}
	}

	#parseDouble()
	{
		let d;
		if (!!isNaN(d = parseFloat(this.getLexeme().replaceAll("_", ""))))
			this.reportError("Invalid floating-point number");
		let result = Object.assign(new CiLiteralDouble(), { line: this.line, type: this.program.system.doubleType, value: d });
		this.nextToken();
		return result;
	}

	#seeDigit()
	{
		let c = this.peekChar();
		return c >= 48 && c <= 57;
	}

	#parseInterpolatedString()
	{
		let result = Object.assign(new CiInterpolatedString(), { line: this.line });
		do {
			let prefix = this.stringValue.replaceAll("{{", "{");
			this.nextToken();
			let arg = this.#parseExpr();
			let width = this.eat(CiToken.COMMA) ? this.#parseExpr() : null;
			let format = 32;
			let precision = -1;
			if (this.see(CiToken.COLON)) {
				format = this.readChar();
				if (this.#seeDigit()) {
					precision = this.readChar() - 48;
					if (this.#seeDigit())
						precision = precision * 10 + this.readChar() - 48;
				}
				this.nextToken();
			}
			result.addPart(prefix, arg, width, format, precision);
			this.check(CiToken.RIGHT_BRACE);
		}
		while (this.readString(true) == CiToken.INTERPOLATED_STRING);
		result.suffix = this.stringValue.replaceAll("{{", "{");
		this.nextToken();
		return result;
	}

	#parseParenthesized()
	{
		this.expect(CiToken.LEFT_PARENTHESIS);
		let result = this.#parseExpr();
		this.expect(CiToken.RIGHT_PARENTHESIS);
		return result;
	}

	#parseSymbolReference(left)
	{
		this.check(CiToken.ID);
		let result = Object.assign(new CiSymbolReference(), { line: this.line, left: left, name: this.stringValue });
		this.nextToken();
		return result;
	}

	#parseCollection(result, closing)
	{
		if (!this.see(closing)) {
			do
				result.push(this.#parseExpr());
			while (this.eat(CiToken.COMMA));
		}
		this.expectOrSkip(closing);
	}

	#parsePrimaryExpr(type)
	{
		let result;
		switch (this.currentToken) {
		case CiToken.INCREMENT:
		case CiToken.DECREMENT:
			this.#checkXcrementParent();
			return Object.assign(new CiPrefixExpr(), { line: this.line, op: this.nextToken(), inner: this.#parsePrimaryExpr(false) });
		case CiToken.MINUS:
		case CiToken.TILDE:
		case CiToken.EXCLAMATION_MARK:
			return Object.assign(new CiPrefixExpr(), { line: this.line, op: this.nextToken(), inner: this.#parsePrimaryExpr(false) });
		case CiToken.NEW:
			let newResult = Object.assign(new CiPrefixExpr(), { line: this.line, op: this.nextToken() });
			result = this.#parseType();
			if (this.eat(CiToken.LEFT_BRACE))
				result = Object.assign(new CiBinaryExpr(), { line: this.line, left: result, op: CiToken.LEFT_BRACE, right: this.#parseObjectLiteral() });
			newResult.inner = result;
			return newResult;
		case CiToken.LITERAL_LONG:
			result = this.program.system.newLiteralLong(this.longValue, this.line);
			this.nextToken();
			break;
		case CiToken.LITERAL_DOUBLE:
			result = this.#parseDouble();
			break;
		case CiToken.LITERAL_CHAR:
			result = CiLiteralChar.new(Number(this.longValue), this.line);
			this.nextToken();
			break;
		case CiToken.LITERAL_STRING:
			result = this.program.system.newLiteralString(this.stringValue, this.line);
			this.nextToken();
			break;
		case CiToken.FALSE:
			result = Object.assign(new CiLiteralFalse(), { line: this.line, type: this.program.system.boolType });
			this.nextToken();
			break;
		case CiToken.TRUE:
			result = Object.assign(new CiLiteralTrue(), { line: this.line, type: this.program.system.boolType });
			this.nextToken();
			break;
		case CiToken.NULL:
			result = Object.assign(new CiLiteralNull(), { line: this.line, type: this.program.system.nullType });
			this.nextToken();
			break;
		case CiToken.INTERPOLATED_STRING:
			result = this.#parseInterpolatedString();
			break;
		case CiToken.LEFT_PARENTHESIS:
			result = this.#parseParenthesized();
			break;
		case CiToken.ID:
			let symbol = this.#parseSymbolReference(null);
			if (this.eat(CiToken.FAT_ARROW)) {
				let lambda = Object.assign(new CiLambdaExpr(), { line: symbol.line });
				lambda.add(CiVar.new(null, symbol.name));
				lambda.body = this.#parseExpr();
				return lambda;
			}
			if (type && this.eat(CiToken.LESS)) {
				let typeArgs = new CiAggregateInitializer();
				let saveTypeArg = this.parsingTypeArg;
				this.parsingTypeArg = true;
				do
					typeArgs.items.push(this.#parseType());
				while (this.eat(CiToken.COMMA));
				this.expect(CiToken.RIGHT_ANGLE);
				this.parsingTypeArg = saveTypeArg;
				symbol.left = typeArgs;
			}
			result = symbol;
			break;
		case CiToken.RESOURCE:
			this.nextToken();
			if (this.eat(CiToken.LESS) && this.stringValue == "byte" && this.eat(CiToken.ID) && this.eat(CiToken.LEFT_BRACKET) && this.eat(CiToken.RIGHT_BRACKET) && this.eat(CiToken.GREATER))
				result = Object.assign(new CiPrefixExpr(), { line: this.line, op: CiToken.RESOURCE, inner: this.#parseParenthesized() });
			else {
				this.reportError("Expected 'resource<byte[]>'");
				result = null;
			}
			break;
		default:
			this.reportError("Invalid expression");
			result = null;
			break;
		}
		for (;;) {
			switch (this.currentToken) {
			case CiToken.DOT:
				this.nextToken();
				result = this.#parseSymbolReference(result);
				break;
			case CiToken.LEFT_PARENTHESIS:
				this.nextToken();
				let method;
				if ((method = result) instanceof CiSymbolReference) {
					let call = Object.assign(new CiCallExpr(), { line: this.line, method: method });
					this.#parseCollection(call.arguments, CiToken.RIGHT_PARENTHESIS);
					result = call;
				}
				else
					this.reportError("Expected a method");
				break;
			case CiToken.LEFT_BRACKET:
				result = Object.assign(new CiBinaryExpr(), { line: this.line, left: result, op: this.nextToken(), right: this.see(CiToken.RIGHT_BRACKET) ? null : this.#parseExpr() });
				this.expect(CiToken.RIGHT_BRACKET);
				break;
			case CiToken.INCREMENT:
			case CiToken.DECREMENT:
				this.#checkXcrementParent();
				result = Object.assign(new CiPostfixExpr(), { line: this.line, inner: result, op: this.nextToken() });
				break;
			case CiToken.EXCLAMATION_MARK:
			case CiToken.HASH:
				result = Object.assign(new CiPostfixExpr(), { line: this.line, inner: result, op: this.nextToken() });
				break;
			case CiToken.QUESTION_MARK:
				if (!type)
					return result;
				result = Object.assign(new CiPostfixExpr(), { line: this.line, inner: result, op: this.nextToken() });
				break;
			default:
				return result;
			}
		}
	}

	#parseMulExpr()
	{
		let left = this.#parsePrimaryExpr(false);
		for (;;) {
			switch (this.currentToken) {
			case CiToken.ASTERISK:
			case CiToken.SLASH:
			case CiToken.MOD:
				left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parsePrimaryExpr(false) });
				break;
			default:
				return left;
			}
		}
	}

	#parseAddExpr()
	{
		let left = this.#parseMulExpr();
		while (this.see(CiToken.PLUS) || this.see(CiToken.MINUS))
			left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseMulExpr() });
		return left;
	}

	#parseShiftExpr()
	{
		let left = this.#parseAddExpr();
		while (this.see(CiToken.SHIFT_LEFT) || this.see(CiToken.SHIFT_RIGHT))
			left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseAddExpr() });
		return left;
	}

	#parseRelExpr()
	{
		let left = this.#parseShiftExpr();
		for (;;) {
			switch (this.currentToken) {
			case CiToken.LESS:
			case CiToken.LESS_OR_EQUAL:
			case CiToken.GREATER:
			case CiToken.GREATER_OR_EQUAL:
				left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseShiftExpr() });
				break;
			case CiToken.IS:
				let isExpr = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parsePrimaryExpr(false) });
				if (this.see(CiToken.ID)) {
					isExpr.right = Object.assign(new CiVar(), { line: this.line, typeExpr: isExpr.right, name: this.stringValue });
					this.nextToken();
				}
				return isExpr;
			default:
				return left;
			}
		}
	}

	#parseEqualityExpr()
	{
		let left = this.#parseRelExpr();
		while (this.see(CiToken.EQUAL) || this.see(CiToken.NOT_EQUAL))
			left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseRelExpr() });
		return left;
	}

	#parseAndExpr()
	{
		let left = this.#parseEqualityExpr();
		while (this.see(CiToken.AND))
			left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseEqualityExpr() });
		return left;
	}

	#parseXorExpr()
	{
		let left = this.#parseAndExpr();
		while (this.see(CiToken.XOR))
			left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseAndExpr() });
		return left;
	}

	#parseOrExpr()
	{
		let left = this.#parseXorExpr();
		while (this.see(CiToken.OR))
			left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseXorExpr() });
		return left;
	}

	#parseCondAndExpr()
	{
		let left = this.#parseOrExpr();
		while (this.see(CiToken.COND_AND)) {
			let saveXcrementParent = this.#xcrementParent;
			this.#xcrementParent = "&&";
			left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseOrExpr() });
			this.#xcrementParent = saveXcrementParent;
		}
		return left;
	}

	#parseCondOrExpr()
	{
		let left = this.#parseCondAndExpr();
		while (this.see(CiToken.COND_OR)) {
			let saveXcrementParent = this.#xcrementParent;
			this.#xcrementParent = "||";
			left = Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseCondAndExpr() });
			this.#xcrementParent = saveXcrementParent;
		}
		return left;
	}

	#parseExpr()
	{
		let left = this.#parseCondOrExpr();
		if (this.see(CiToken.QUESTION_MARK)) {
			let result = Object.assign(new CiSelectExpr(), { line: this.line, cond: left });
			this.nextToken();
			let saveXcrementParent = this.#xcrementParent;
			this.#xcrementParent = "?";
			result.onTrue = this.#parseExpr();
			this.expect(CiToken.COLON);
			result.onFalse = this.#parseExpr();
			this.#xcrementParent = saveXcrementParent;
			return result;
		}
		return left;
	}

	#parseType()
	{
		let left = this.#parsePrimaryExpr(true);
		if (this.eat(CiToken.RANGE))
			return Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: CiToken.RANGE, right: this.#parsePrimaryExpr(true) });
		return left;
	}

	#parseConstInitializer()
	{
		if (this.eat(CiToken.LEFT_BRACE)) {
			let result = Object.assign(new CiAggregateInitializer(), { line: this.line });
			this.#parseCollection(result.items, CiToken.RIGHT_BRACE);
			return result;
		}
		return this.#parseExpr();
	}

	#parseObjectLiteral()
	{
		let result = Object.assign(new CiAggregateInitializer(), { line: this.line });
		do {
			let line = this.line;
			let field = this.#parseSymbolReference(null);
			this.expect(CiToken.ASSIGN);
			result.items.push(Object.assign(new CiBinaryExpr(), { line: line, left: field, op: CiToken.ASSIGN, right: this.#parseExpr() }));
		}
		while (this.eat(CiToken.COMMA));
		this.expect(CiToken.RIGHT_BRACE);
		return result;
	}

	#parseInitializer()
	{
		if (!this.eat(CiToken.ASSIGN))
			return null;
		if (this.eat(CiToken.LEFT_BRACE))
			return this.#parseObjectLiteral();
		return this.#parseExpr();
	}

	#addSymbol(scope, symbol)
	{
		if (scope.contains(symbol))
			this.reportError("Duplicate symbol");
		else
			scope.add(symbol);
	}

	#parseVar(type)
	{
		let result = Object.assign(new CiVar(), { line: this.line, typeExpr: type, name: this.stringValue });
		this.nextToken();
		result.value = this.#parseInitializer();
		return result;
	}

	#parseConst(visibility)
	{
		this.expect(CiToken.CONST);
		let konst = Object.assign(new CiConst(), { line: this.line, visibility: visibility, typeExpr: this.#parseType(), name: this.stringValue });
		this.nextToken();
		this.expect(CiToken.ASSIGN);
		konst.value = this.#parseConstInitializer();
		this.expect(CiToken.SEMICOLON);
		return konst;
	}

	#parseAssign(allowVar)
	{
		let left = allowVar ? this.#parseType() : this.#parseExpr();
		switch (this.currentToken) {
		case CiToken.ASSIGN:
		case CiToken.ADD_ASSIGN:
		case CiToken.SUB_ASSIGN:
		case CiToken.MUL_ASSIGN:
		case CiToken.DIV_ASSIGN:
		case CiToken.MOD_ASSIGN:
		case CiToken.AND_ASSIGN:
		case CiToken.OR_ASSIGN:
		case CiToken.XOR_ASSIGN:
		case CiToken.SHIFT_LEFT_ASSIGN:
		case CiToken.SHIFT_RIGHT_ASSIGN:
			return Object.assign(new CiBinaryExpr(), { line: this.line, left: left, op: this.nextToken(), right: this.#parseAssign(false) });
		case CiToken.ID:
			if (allowVar)
				return this.#parseVar(left);
			return left;
		default:
			return left;
		}
	}

	#parseBlock()
	{
		let result = Object.assign(new CiBlock(), { line: this.line });
		this.expect(CiToken.LEFT_BRACE);
		while (!this.see(CiToken.RIGHT_BRACE) && !this.see(CiToken.END_OF_FILE))
			result.statements.push(this.#parseStatement());
		this.expect(CiToken.RIGHT_BRACE);
		return result;
	}

	#parseAssert()
	{
		let result = Object.assign(new CiAssert(), { line: this.line });
		this.expect(CiToken.ASSERT);
		result.cond = this.#parseExpr();
		if (this.eat(CiToken.COMMA))
			result.message = this.#parseExpr();
		this.expect(CiToken.SEMICOLON);
		return result;
	}

	#parseBreak()
	{
		if (this.#currentLoopOrSwitch == null)
			this.reportError("break outside loop or switch");
		let result = Object.assign(new CiBreak(), { line: this.line, loopOrSwitch: this.#currentLoopOrSwitch });
		this.expect(CiToken.BREAK);
		this.expect(CiToken.SEMICOLON);
		let loop;
		if ((loop = this.#currentLoopOrSwitch) instanceof CiLoop)
			loop.hasBreak = true;
		return result;
	}

	#parseContinue()
	{
		if (this.#currentLoop == null)
			this.reportError("continue outside loop");
		let result = Object.assign(new CiContinue(), { line: this.line, loop: this.#currentLoop });
		this.expect(CiToken.CONTINUE);
		this.expect(CiToken.SEMICOLON);
		return result;
	}

	#parseLoopBody(loop)
	{
		let outerLoop = this.#currentLoop;
		let outerLoopOrSwitch = this.#currentLoopOrSwitch;
		this.#currentLoop = loop;
		this.#currentLoopOrSwitch = loop;
		loop.body = this.#parseStatement();
		this.#currentLoopOrSwitch = outerLoopOrSwitch;
		this.#currentLoop = outerLoop;
	}

	#parseDoWhile()
	{
		let result = Object.assign(new CiDoWhile(), { line: this.line });
		this.expect(CiToken.DO);
		this.#parseLoopBody(result);
		this.expect(CiToken.WHILE);
		result.cond = this.#parseParenthesized();
		this.expect(CiToken.SEMICOLON);
		return result;
	}

	#parseFor()
	{
		let result = Object.assign(new CiFor(), { line: this.line });
		this.expect(CiToken.FOR);
		this.expect(CiToken.LEFT_PARENTHESIS);
		if (!this.see(CiToken.SEMICOLON))
			result.init = this.#parseAssign(true);
		this.expect(CiToken.SEMICOLON);
		if (!this.see(CiToken.SEMICOLON))
			result.cond = this.#parseExpr();
		this.expect(CiToken.SEMICOLON);
		if (!this.see(CiToken.RIGHT_PARENTHESIS))
			result.advance = this.#parseAssign(false);
		this.expect(CiToken.RIGHT_PARENTHESIS);
		this.#parseLoopBody(result);
		return result;
	}

	#parseForeachIterator(result)
	{
		this.#addSymbol(result, Object.assign(new CiVar(), { line: this.line, typeExpr: this.#parseType(), name: this.stringValue }));
		this.nextToken();
	}

	#parseForeach()
	{
		let result = Object.assign(new CiForeach(), { line: this.line });
		this.expect(CiToken.FOREACH);
		this.expect(CiToken.LEFT_PARENTHESIS);
		if (this.eat(CiToken.LEFT_PARENTHESIS)) {
			this.#parseForeachIterator(result);
			this.expect(CiToken.COMMA);
			this.#parseForeachIterator(result);
			this.expect(CiToken.RIGHT_PARENTHESIS);
		}
		else
			this.#parseForeachIterator(result);
		this.expect(CiToken.IN);
		result.collection = this.#parseExpr();
		this.expect(CiToken.RIGHT_PARENTHESIS);
		this.#parseLoopBody(result);
		return result;
	}

	#parseIf()
	{
		let result = Object.assign(new CiIf(), { line: this.line });
		this.expect(CiToken.IF);
		result.cond = this.#parseParenthesized();
		result.onTrue = this.#parseStatement();
		if (this.eat(CiToken.ELSE))
			result.onFalse = this.#parseStatement();
		return result;
	}

	#parseLock()
	{
		let result = Object.assign(new CiLock(), { line: this.line });
		this.expect(CiToken.LOCK_);
		result.lock = this.#parseParenthesized();
		result.body = this.#parseStatement();
		return result;
	}

	#parseNative()
	{
		let result = Object.assign(new CiNative(), { line: this.line });
		this.expect(CiToken.NATIVE);
		if (this.see(CiToken.LITERAL_STRING))
			result.content = this.stringValue;
		else {
			let offset = this.charOffset;
			this.expect(CiToken.LEFT_BRACE);
			let nesting = 1;
			for (;;) {
				if (this.see(CiToken.END_OF_FILE)) {
					this.expect(CiToken.RIGHT_BRACE);
					return result;
				}
				if (this.see(CiToken.LEFT_BRACE))
					nesting++;
				else if (this.see(CiToken.RIGHT_BRACE)) {
					if (--nesting == 0)
						break;
				}
				this.nextToken();
			}
			console.assert(this.input[this.charOffset - 1] == 125);
			result.content = new TextDecoder().decode(this.input.subarray(offset, offset + this.charOffset - 1 - offset));
		}
		this.nextToken();
		return result;
	}

	#parseReturn()
	{
		let result = Object.assign(new CiReturn(), { line: this.line });
		this.nextToken();
		if (!this.see(CiToken.SEMICOLON))
			result.value = this.#parseExpr();
		this.expect(CiToken.SEMICOLON);
		return result;
	}

	#parseSwitch()
	{
		let result = Object.assign(new CiSwitch(), { line: this.line });
		this.expect(CiToken.SWITCH);
		result.value = this.#parseParenthesized();
		this.expect(CiToken.LEFT_BRACE);
		let outerLoopOrSwitch = this.#currentLoopOrSwitch;
		this.#currentLoopOrSwitch = result;
		while (this.eat(CiToken.CASE)) {
			result.cases.push(new CiCase());
			let kase = result.cases.at(-1);
			do {
				let expr = this.#parseExpr();
				if (this.see(CiToken.ID))
					expr = this.#parseVar(expr);
				if (this.eat(CiToken.WHEN))
					expr = Object.assign(new CiBinaryExpr(), { line: this.line, left: expr, op: CiToken.WHEN, right: this.#parseExpr() });
				kase.values.push(expr);
				this.expect(CiToken.COLON);
			}
			while (this.eat(CiToken.CASE));
			if (this.see(CiToken.DEFAULT)) {
				this.reportError("Please remove 'case' before 'default'");
				break;
			}
			while (!this.see(CiToken.END_OF_FILE)) {
				kase.body.push(this.#parseStatement());
				switch (this.currentToken) {
				case CiToken.CASE:
				case CiToken.DEFAULT:
				case CiToken.RIGHT_BRACE:
					break;
				default:
					continue;
				}
				break;
			}
		}
		if (result.cases.length == 0)
			this.reportError("Switch with no cases");
		if (this.eat(CiToken.DEFAULT)) {
			this.expect(CiToken.COLON);
			do {
				if (this.see(CiToken.END_OF_FILE))
					break;
				result.defaultBody.push(this.#parseStatement());
			}
			while (!this.see(CiToken.RIGHT_BRACE));
		}
		this.expect(CiToken.RIGHT_BRACE);
		this.#currentLoopOrSwitch = outerLoopOrSwitch;
		return result;
	}

	#parseThrow()
	{
		let result = Object.assign(new CiThrow(), { line: this.line });
		this.expect(CiToken.THROW);
		result.message = this.#parseExpr();
		this.expect(CiToken.SEMICOLON);
		return result;
	}

	#parseWhile()
	{
		let result = Object.assign(new CiWhile(), { line: this.line });
		this.expect(CiToken.WHILE);
		result.cond = this.#parseParenthesized();
		this.#parseLoopBody(result);
		return result;
	}

	#parseStatement()
	{
		switch (this.currentToken) {
		case CiToken.LEFT_BRACE:
			return this.#parseBlock();
		case CiToken.ASSERT:
			return this.#parseAssert();
		case CiToken.BREAK:
			return this.#parseBreak();
		case CiToken.CONST:
			return this.#parseConst(CiVisibility.PRIVATE);
		case CiToken.CONTINUE:
			return this.#parseContinue();
		case CiToken.DO:
			return this.#parseDoWhile();
		case CiToken.FOR:
			return this.#parseFor();
		case CiToken.FOREACH:
			return this.#parseForeach();
		case CiToken.IF:
			return this.#parseIf();
		case CiToken.LOCK_:
			return this.#parseLock();
		case CiToken.NATIVE:
			return this.#parseNative();
		case CiToken.RETURN:
			return this.#parseReturn();
		case CiToken.SWITCH:
			return this.#parseSwitch();
		case CiToken.THROW:
			return this.#parseThrow();
		case CiToken.WHILE:
			return this.#parseWhile();
		default:
			let expr = this.#parseAssign(true);
			this.expect(CiToken.SEMICOLON);
			return expr;
		}
	}

	#parseCallType()
	{
		switch (this.currentToken) {
		case CiToken.STATIC:
			this.nextToken();
			return CiCallType.STATIC;
		case CiToken.ABSTRACT:
			this.nextToken();
			return CiCallType.ABSTRACT;
		case CiToken.VIRTUAL:
			this.nextToken();
			return CiCallType.VIRTUAL;
		case CiToken.OVERRIDE:
			this.nextToken();
			return CiCallType.OVERRIDE;
		case CiToken.SEALED:
			this.nextToken();
			return CiCallType.SEALED;
		default:
			return CiCallType.NORMAL;
		}
	}

	#parseMethod(method)
	{
		method.isMutator = this.eat(CiToken.EXCLAMATION_MARK);
		this.expect(CiToken.LEFT_PARENTHESIS);
		if (!this.see(CiToken.RIGHT_PARENTHESIS)) {
			do {
				let doc = this.#parseDoc();
				let param = this.#parseVar(this.#parseType());
				param.documentation = doc;
				this.#addSymbol(method.parameters, param);
			}
			while (this.eat(CiToken.COMMA));
		}
		this.expect(CiToken.RIGHT_PARENTHESIS);
		method.throws = this.eat(CiToken.THROWS);
		if (method.callType == CiCallType.ABSTRACT)
			this.expect(CiToken.SEMICOLON);
		else if (this.see(CiToken.FAT_ARROW))
			method.body = this.#parseReturn();
		else if (this.check(CiToken.LEFT_BRACE))
			method.body = this.#parseBlock();
	}

	static #callTypeToString(callType)
	{
		switch (callType) {
		case CiCallType.STATIC:
			return "static";
		case CiCallType.NORMAL:
			return "normal";
		case CiCallType.ABSTRACT:
			return "abstract";
		case CiCallType.VIRTUAL:
			return "virtual";
		case CiCallType.OVERRIDE:
			return "override";
		case CiCallType.SEALED:
			return "sealed";
		default:
			throw new Error();
		}
	}

	#parseClass(doc, isPublic, callType)
	{
		this.expect(CiToken.CLASS);
		let klass = Object.assign(new CiClass(), { filename: this.filename, line: this.line, documentation: doc, isPublic: isPublic, callType: callType, name: this.stringValue });
		if (this.expect(CiToken.ID))
			this.#addSymbol(this.program, klass);
		if (this.eat(CiToken.COLON)) {
			klass.baseClassName = this.stringValue;
			this.expect(CiToken.ID);
		}
		this.expect(CiToken.LEFT_BRACE);
		while (!this.see(CiToken.RIGHT_BRACE) && !this.see(CiToken.END_OF_FILE)) {
			doc = this.#parseDoc();
			let visibility;
			switch (this.currentToken) {
			case CiToken.INTERNAL:
				visibility = CiVisibility.INTERNAL;
				this.nextToken();
				break;
			case CiToken.PROTECTED:
				visibility = CiVisibility.PROTECTED;
				this.nextToken();
				break;
			case CiToken.PUBLIC:
				visibility = CiVisibility.PUBLIC;
				this.nextToken();
				break;
			default:
				visibility = CiVisibility.PRIVATE;
				break;
			}
			if (this.see(CiToken.CONST)) {
				let konst = this.#parseConst(visibility);
				konst.documentation = doc;
				this.#addSymbol(klass, konst);
				continue;
			}
			callType = this.#parseCallType();
			let type = this.eat(CiToken.VOID) ? this.program.system.voidType : this.#parseType();
			let call;
			if (this.see(CiToken.LEFT_BRACE) && (call = type) instanceof CiCallExpr) {
				if (call.method.name != klass.name)
					this.reportError("Method with no return type");
				else {
					if (klass.callType == CiCallType.STATIC)
						this.reportError("Constructor in a static class");
					if (callType != CiCallType.NORMAL)
						this.reportError(`Constructor cannot be ${CiParser.#callTypeToString(callType)}`);
					if (call.arguments.length != 0)
						this.reportError("Constructor parameters not supported");
					if (klass.constructor_ != null)
						this.reportError(`Duplicate constructor, already defined in line ${klass.constructor_.line}`);
				}
				if (visibility == CiVisibility.PRIVATE)
					visibility = CiVisibility.INTERNAL;
				klass.constructor_ = Object.assign(new CiMethodBase(), { line: call.line, documentation: doc, visibility: visibility, parent: klass, type: this.program.system.voidType, name: klass.name, isMutator: true, body: this.#parseBlock() });
				continue;
			}
			let line = this.line;
			let name = this.stringValue;
			if (!this.expect(CiToken.ID))
				continue;
			if (this.see(CiToken.LEFT_PARENTHESIS) || this.see(CiToken.EXCLAMATION_MARK)) {
				if (callType == CiCallType.STATIC || klass.callType == CiCallType.ABSTRACT) {
				}
				else if (klass.callType == CiCallType.STATIC)
					this.reportError("Only static methods allowed in a static class");
				else if (callType == CiCallType.ABSTRACT)
					this.reportError("Abstract methods allowed only in an abstract class");
				else if (klass.callType == CiCallType.SEALED && callType == CiCallType.VIRTUAL)
					this.reportError("Virtual methods disallowed in a sealed class");
				if (visibility == CiVisibility.PRIVATE && callType != CiCallType.STATIC && callType != CiCallType.NORMAL)
					this.reportError(`${CiParser.#callTypeToString(callType)} method cannot be private`);
				let method = Object.assign(new CiMethod(), { line: line, documentation: doc, visibility: visibility, callType: callType, typeExpr: type, name: name });
				this.#addSymbol(klass, method);
				method.parameters.parent = klass;
				this.#parseMethod(method);
				continue;
			}
			if (visibility == CiVisibility.PUBLIC)
				this.reportError("Field cannot be public");
			if (callType != CiCallType.NORMAL)
				this.reportError(`Field cannot be ${CiParser.#callTypeToString(callType)}`);
			if (type == this.program.system.voidType)
				this.reportError("Field cannot be void");
			let field = Object.assign(new CiField(), { line: line, documentation: doc, visibility: visibility, typeExpr: type, name: name, value: this.#parseInitializer() });
			this.#addSymbol(klass, field);
			this.expect(CiToken.SEMICOLON);
		}
		this.expect(CiToken.RIGHT_BRACE);
	}

	#parseEnum(doc, isPublic)
	{
		this.expect(CiToken.ENUM);
		let flags = this.eat(CiToken.ASTERISK);
		let enu = this.program.system.newEnum(flags);
		enu.filename = this.filename;
		enu.line = this.line;
		enu.documentation = doc;
		enu.isPublic = isPublic;
		enu.name = this.stringValue;
		if (this.expect(CiToken.ID))
			this.#addSymbol(this.program, enu);
		this.expect(CiToken.LEFT_BRACE);
		do {
			let konst = Object.assign(new CiConst(), { visibility: CiVisibility.PUBLIC, documentation: this.#parseDoc(), line: this.line, name: this.stringValue, type: enu });
			this.expect(CiToken.ID);
			if (this.eat(CiToken.ASSIGN))
				konst.value = this.#parseExpr();
			else if (flags)
				this.reportError("enum* symbol must be assigned a value");
			this.#addSymbol(enu, konst);
		}
		while (this.eat(CiToken.COMMA));
		this.expect(CiToken.RIGHT_BRACE);
	}

	parse(filename, input, inputLength)
	{
		this.open(filename, input, inputLength);
		while (!this.see(CiToken.END_OF_FILE)) {
			let doc = this.#parseDoc();
			let isPublic = this.eat(CiToken.PUBLIC);
			switch (this.currentToken) {
			case CiToken.CLASS:
				this.#parseClass(doc, isPublic, CiCallType.NORMAL);
				break;
			case CiToken.STATIC:
			case CiToken.ABSTRACT:
			case CiToken.SEALED:
				this.#parseClass(doc, isPublic, this.#parseCallType());
				break;
			case CiToken.ENUM:
				this.#parseEnum(doc, isPublic);
				break;
			case CiToken.NATIVE:
				this.program.topLevelNatives.push(this.#parseNative().content);
				break;
			default:
				this.reportError("Expected class or enum");
				this.nextToken();
				break;
			}
		}
	}
}

export class CiConsoleParser extends CiParser
{
	hasErrors = false;

	reportError(message)
	{
		console.error(`${this.filename}(${this.line}): ERROR: ${message}`);
		this.hasErrors = true;
	}
}

export class CiSema
{
	program;
	hasErrors = false;
	#currentMethod = null;
	#currentScope;
	#currentPureMethods = new Set();
	#currentPureArguments = {};
	#poison = Object.assign(new CiType(), { name: "poison" });

	#getCurrentContainer()
	{
		return this.#currentScope.getContainer();
	}

	reportError(statement, message)
	{
		console.error(`${this.#getCurrentContainer().filename}(${statement.line}): ERROR: ${message}`);
		this.hasErrors = true;
	}

	#poisonError(statement, message)
	{
		this.reportError(statement, message);
		return this.#poison;
	}

	#resolveBase(klass)
	{
		if (klass.hasBaseClass()) {
			this.#currentScope = klass;
			let baseClass;
			if ((baseClass = this.program.tryLookup(klass.baseClassName, true)) instanceof CiClass) {
				if (klass.isPublic && !baseClass.isPublic)
					this.reportError(klass, "Public class cannot derive from an internal class");
				baseClass.hasSubclasses = true;
				klass.parent = baseClass;
			}
			else
				this.reportError(klass, `Base class ${klass.baseClassName} not found`);
		}
		this.program.classes.push(klass);
	}

	#checkBaseCycle(klass)
	{
		let hare = klass;
		let tortoise = klass;
		do {
			hare = hare.parent;
			if (hare == null)
				return;
			hare = hare.parent;
			if (hare == null)
				return;
			tortoise = tortoise.parent;
		}
		while (tortoise != hare);
		this.#currentScope = klass;
		this.reportError(klass, `Circular inheritance for class ${klass.name}`);
	}

	static #takePtr(expr)
	{
		let arrayStg;
		if ((arrayStg = expr.type) instanceof CiArrayStorageType)
			arrayStg.ptrTaken = true;
	}

	#coerce(expr, type)
	{
		if (expr == this.#poison)
			return false;
		if (!type.isAssignableFrom(expr.type)) {
			this.reportError(expr, `Cannot coerce ${expr.type} to ${type}`);
			return false;
		}
		let prefix;
		if ((prefix = expr) instanceof CiPrefixExpr && prefix.op == CiToken.NEW && !(type instanceof CiDynamicPtrType)) {
			const newType = expr.type;
			let kind = newType.class.id == CiId.ARRAY_PTR_CLASS ? "array" : "object";
			this.reportError(expr, `Dynamically allocated ${kind} must be assigned to a ${expr.type} reference`);
			return false;
		}
		CiSema.#takePtr(expr);
		return true;
	}

	#visitInterpolatedString(expr)
	{
		let partsCount = 0;
		let s = "";
		for (let partsIndex = 0; partsIndex < expr.parts.length; partsIndex++) {
			let part = expr.parts[partsIndex];
			s += part.prefix;
			let arg = this.#visitExpr(part.argument);
			this.#coerce(arg, this.program.system.printableType);
			if (arg.type instanceof CiIntegerType) {
				switch (part.format) {
				case 32:
					let literalLong;
					if ((literalLong = arg) instanceof CiLiteralLong && part.widthExpr == null) {
						s += `${literalLong.value}`;
						continue;
					}
					break;
				case 68:
				case 100:
				case 88:
				case 120:
					if (part.widthExpr != null && part.precision >= 0)
						this.reportError(part.widthExpr, "Cannot format an integer with both width and precision");
					break;
				default:
					this.reportError(arg, "Invalid format");
					break;
				}
			}
			else if (arg.type instanceof CiFloatingType) {
				switch (part.format) {
				case 32:
				case 70:
				case 102:
				case 69:
				case 101:
					break;
				default:
					this.reportError(arg, "Invalid format");
					break;
				}
			}
			else {
				if (part.format != 32)
					this.reportError(arg, "Invalid format");
				else {
					let literalString;
					if ((literalString = arg) instanceof CiLiteralString && part.widthExpr == null) {
						s += literalString.value;
						continue;
					}
				}
			}
			let targetPart = expr.parts[partsCount++];
			targetPart.prefix = s;
			targetPart.argument = arg;
			targetPart.widthExpr = part.widthExpr;
			targetPart.width = part.widthExpr != null ? this.#foldConstInt(part.widthExpr) : 0;
			targetPart.format = part.format;
			targetPart.precision = part.precision;
			s = "";
		}
		s += expr.suffix;
		if (partsCount == 0)
			return this.program.system.newLiteralString(s, expr.line);
		expr.type = this.program.system.stringStorageType;
		expr.parts.splice(partsCount, expr.parts.length - partsCount);
		expr.suffix = s;
		return expr;
	}

	#lookup(expr, scope)
	{
		if (expr.symbol == null) {
			expr.symbol = scope.tryLookup(expr.name, expr.left == null);
			if (expr.symbol == null)
				return this.#poisonError(expr, `${expr.name} not found`);
			expr.type = expr.symbol.type;
		}
		let konst;
		if (!(scope instanceof CiEnum) && (konst = expr.symbol) instanceof CiConst) {
			this.#resolveConst(konst);
			if (konst.value instanceof CiLiteral || konst.value instanceof CiSymbolReference)
				return konst.value;
		}
		return expr;
	}

	#visitSymbolReference(expr)
	{
		if (expr.left == null) {
			let resolved = this.#lookup(expr, this.#currentScope);
			let nearMember;
			if ((nearMember = expr.symbol) instanceof CiMember) {
				let memberClass;
				if (nearMember.visibility == CiVisibility.PRIVATE && (memberClass = nearMember.parent) instanceof CiClass && memberClass != this.#getCurrentContainer())
					this.reportError(expr, `Cannot access private member ${expr.name}`);
				if (!nearMember.isStatic() && (this.#currentMethod == null || this.#currentMethod.isStatic()))
					this.reportError(expr, `Cannot use instance member ${expr.name} from static context`);
			}
			let symbol;
			if ((symbol = resolved) instanceof CiSymbolReference) {
				let v;
				if ((v = symbol.symbol) instanceof CiVar) {
					let loop;
					if ((loop = v.parent) instanceof CiFor)
						loop.isIteratorUsed = true;
					else if (this.#currentPureArguments.hasOwnProperty(v))
						return this.#currentPureArguments[v];
				}
				else if (symbol.symbol.id == CiId.REGEX_OPTIONS_ENUM)
					this.program.regexOptionsEnum = true;
			}
			return resolved;
		}
		let left = this.#visitExpr(expr.left);
		if (left == this.#poison)
			return left;
		let scope;
		let baseSymbol;
		let isBase = (baseSymbol = left) instanceof CiSymbolReference && baseSymbol.symbol.id == CiId.BASE_PTR;
		if (isBase) {
			let baseClass;
			if (this.#currentMethod == null || !((baseClass = this.#currentMethod.parent.parent) instanceof CiClass))
				return this.#poisonError(expr, "No base class");
			scope = baseClass;
		}
		else {
			let leftSymbol;
			let obj;
			if ((leftSymbol = left) instanceof CiSymbolReference && (obj = leftSymbol.symbol) instanceof CiScope)
				scope = obj;
			else {
				scope = left.type;
				let klass;
				if ((klass = scope) instanceof CiClassType)
					scope = klass.class;
			}
		}
		let result = this.#lookup(expr, scope);
		if (result != expr)
			return result;
		let member;
		if ((member = expr.symbol) instanceof CiMember) {
			switch (member.visibility) {
			case CiVisibility.PRIVATE:
				if (member.parent != this.#currentMethod.parent || this.#currentMethod.parent != scope)
					this.reportError(expr, `Cannot access private member ${expr.name}`);
				break;
			case CiVisibility.PROTECTED:
				if (isBase)
					break;
				const currentClass = this.#currentMethod.parent;
				const scopeClass = scope;
				if (!currentClass.isSameOrBaseOf(scopeClass))
					this.reportError(expr, `Cannot access protected member ${expr.name}`);
				break;
			case CiVisibility.NUMERIC_ELEMENT_TYPE:
				let klass;
				if ((klass = left.type) instanceof CiClassType && !(klass.getElementType() instanceof CiNumericType))
					this.reportError(expr, "Method restricted to collections of numbers");
				break;
			case CiVisibility.FINAL_VALUE_TYPE:
				if (!left.type.asClassType().getValueType().isFinal())
					this.reportError(expr, "Method restricted to dictionaries with storage values");
				break;
			default:
				switch (expr.symbol.id) {
				case CiId.ARRAY_LENGTH:
					const arrayStorage = left.type;
					return this.#toLiteralLong(expr, BigInt(arrayStorage.length));
				case CiId.STRING_LENGTH:
					let leftLiteral;
					if ((leftLiteral = left) instanceof CiLiteralString) {
						let length = leftLiteral.getAsciiLength();
						if (length >= 0)
							return this.#toLiteralLong(expr, BigInt(length));
					}
					break;
				default:
					break;
				}
				break;
			}
			if (!(member instanceof CiMethodGroup)) {
				let leftContainer;
				if ((leftContainer = left) instanceof CiSymbolReference && leftContainer.symbol instanceof CiContainerType) {
					if (!member.isStatic())
						this.reportError(expr, `Cannot use instance member ${expr.name} without an object`);
				}
				else if (member.isStatic())
					this.reportError(expr, `${expr.name} is static`);
			}
		}
		return Object.assign(new CiSymbolReference(), { line: expr.line, left: left, name: expr.name, symbol: expr.symbol, type: expr.type });
	}

	static #union(left, right)
	{
		if (right == null)
			return left;
		if (right.min < left.min) {
			if (right.max >= left.max)
				return right;
			return CiRangeType.new(right.min, left.max);
		}
		if (right.max > left.max)
			return CiRangeType.new(left.min, right.max);
		return left;
	}

	#getIntegerType(left, right)
	{
		let type = this.program.system.promoteIntegerTypes(left.type, right.type);
		this.#coerce(left, type);
		this.#coerce(right, type);
		return type;
	}

	#getShiftType(left, right)
	{
		let intType = this.program.system.intType;
		this.#coerce(right, intType);
		if (left.type.id == CiId.LONG_TYPE) {
			const longType = left.type;
			return longType;
		}
		this.#coerce(left, intType);
		return intType;
	}

	#getNumericType(left, right)
	{
		let type = this.program.system.promoteNumericTypes(left.type, right.type);
		this.#coerce(left, type);
		this.#coerce(right, type);
		return type;
	}

	static #saturatedNeg(a)
	{
		if (a == -2147483648)
			return 2147483647;
		return -a;
	}

	static #saturatedAdd(a, b)
	{
		let c = a + b;
		if (c >= 0) {
			if (a < 0 && b < 0)
				return -2147483648;
		}
		else if (a > 0 && b > 0)
			return 2147483647;
		return c;
	}

	static #saturatedSub(a, b)
	{
		if (b == -2147483648)
			return a < 0 ? a ^ b : 2147483647;
		return CiSema.#saturatedAdd(a, -b);
	}

	static #saturatedMul(a, b)
	{
		if (a == 0 || b == 0)
			return 0;
		if (a == -2147483648)
			return b >> 31 ^ a;
		if (b == -2147483648)
			return a >> 31 ^ b;
		if ((2147483647 / Math.abs(a) | 0) < Math.abs(b))
			return (a ^ b) >> 31 ^ 2147483647;
		return a * b;
	}

	static #saturatedDiv(a, b)
	{
		if (a == -2147483648 && b == -1)
			return 2147483647;
		return a / b | 0;
	}

	static #saturatedShiftRight(a, b)
	{
		return a >> (b >= 31 || b < 0 ? 31 : b);
	}

	static #bitwiseUnsignedOp(left, op, right)
	{
		let leftVariableBits = left.getVariableBits();
		let rightVariableBits = right.getVariableBits();
		let min;
		let max;
		switch (op) {
		case CiToken.AND:
			min = left.min & right.min & ~CiRangeType.getMask(~left.min & ~right.min & (leftVariableBits | rightVariableBits));
			max = (left.max | leftVariableBits) & (right.max | rightVariableBits);
			if (max > left.max)
				max = left.max;
			if (max > right.max)
				max = right.max;
			break;
		case CiToken.OR:
			min = (left.min & ~leftVariableBits) | (right.min & ~rightVariableBits);
			max = left.max | right.max | CiRangeType.getMask(left.max & right.max & CiRangeType.getMask(leftVariableBits | rightVariableBits));
			if (min < left.min)
				min = left.min;
			if (min < right.min)
				min = right.min;
			break;
		case CiToken.XOR:
			let variableBits = leftVariableBits | rightVariableBits;
			min = (left.min ^ right.min) & ~variableBits;
			max = (left.max ^ right.max) | variableBits;
			break;
		default:
			throw new Error();
		}
		if (min > max)
			return CiRangeType.new(max, min);
		return CiRangeType.new(min, max);
	}

	#isEnumOp(left, right)
	{
		if (left.type instanceof CiEnum) {
			if (left.type.id != CiId.BOOL_TYPE && !(left.type instanceof CiEnumFlags))
				this.reportError(left, `Define flags enumeration as: enum* ${left.type}`);
			this.#coerce(right, left.type);
			return true;
		}
		return false;
	}

	#bitwiseOp(left, op, right)
	{
		let leftRange;
		let rightRange;
		if ((leftRange = left.type) instanceof CiRangeType && (rightRange = right.type) instanceof CiRangeType) {
			let range = null;
			let rightNegative;
			let rightPositive;
			if (rightRange.min >= 0) {
				rightNegative = null;
				rightPositive = rightRange;
			}
			else if (rightRange.max < 0) {
				rightNegative = rightRange;
				rightPositive = null;
			}
			else {
				rightNegative = CiRangeType.new(rightRange.min, -1);
				rightPositive = CiRangeType.new(0, rightRange.max);
			}
			if (leftRange.min < 0) {
				let leftNegative = leftRange.max < 0 ? leftRange : CiRangeType.new(leftRange.min, -1);
				if (rightNegative != null)
					range = CiSema.#bitwiseUnsignedOp(leftNegative, op, rightNegative);
				if (rightPositive != null)
					range = CiSema.#union(CiSema.#bitwiseUnsignedOp(leftNegative, op, rightPositive), range);
			}
			if (leftRange.max >= 0) {
				let leftPositive = leftRange.min >= 0 ? leftRange : CiRangeType.new(0, leftRange.max);
				if (rightNegative != null)
					range = CiSema.#union(CiSema.#bitwiseUnsignedOp(leftPositive, op, rightNegative), range);
				if (rightPositive != null)
					range = CiSema.#union(CiSema.#bitwiseUnsignedOp(leftPositive, op, rightPositive), range);
			}
			return range;
		}
		if (this.#isEnumOp(left, right))
			return left.type;
		return this.#getIntegerType(left, right);
	}

	static #newRangeType(a, b, c, d)
	{
		if (a > b) {
			let t = a;
			a = b;
			b = t;
		}
		if (c > d) {
			let t = c;
			c = d;
			d = t;
		}
		return CiRangeType.new(a <= c ? a : c, b >= d ? b : d);
	}

	#toLiteralBool(expr, value)
	{
		let result = value ? new CiLiteralTrue() : new CiLiteralFalse();
		result.line = expr.line;
		result.type = this.program.system.boolType;
		return result;
	}

	#toLiteralLong(expr, value)
	{
		return this.program.system.newLiteralLong(value, expr.line);
	}

	#toLiteralDouble(expr, value)
	{
		return Object.assign(new CiLiteralDouble(), { line: expr.line, type: this.program.system.doubleType, value: value });
	}

	#checkLValue(expr)
	{
		let indexing;
		if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			if (symbol.symbol instanceof CiVar) {
				const def = symbol.symbol;
				def.isAssigned = true;
				if (symbol.symbol.parent instanceof CiFor) {
					const forLoop = symbol.symbol.parent;
					forLoop.isRange = false;
				}
				else if (symbol.symbol.parent instanceof CiForeach)
					this.reportError(expr, "Cannot assign a foreach iteration variable");
				for (let scope = this.#currentScope; !(scope instanceof CiClass); scope = scope.parent) {
					let forLoop;
					let binaryCond;
					if ((forLoop = scope) instanceof CiFor && forLoop.isRange && (binaryCond = forLoop.cond) instanceof CiBinaryExpr && binaryCond.right.isReferenceTo(symbol.symbol))
						forLoop.isRange = false;
				}
			}
			else if (symbol.symbol instanceof CiField) {
				if (symbol.left == null) {
					if (!this.#currentMethod.isMutator)
						this.reportError(expr, "Cannot modify field in a non-mutating method");
				}
				else {
					if (symbol.left.type instanceof CiStorageType) {
					}
					else if (symbol.left.type instanceof CiReadWriteClassType) {
					}
					else if (symbol.left.type instanceof CiClassType)
						this.reportError(expr, "Cannot modify field through a read-only reference");
					else
						throw new Error();
				}
			}
			else
				this.reportError(expr, "Cannot modify this");
		}
		else if ((indexing = expr) instanceof CiBinaryExpr && indexing.op == CiToken.LEFT_BRACKET) {
			if (indexing.left.type instanceof CiStorageType) {
			}
			else if (indexing.left.type instanceof CiReadWriteClassType) {
			}
			else if (indexing.left.type instanceof CiClassType)
				this.reportError(expr, "Cannot modify array through a read-only reference");
			else
				throw new Error();
		}
		else
			this.reportError(expr, "Cannot modify this");
	}

	#concatenate(left, right)
	{
		let result = Object.assign(new CiInterpolatedString(), { line: left.line, type: this.program.system.stringStorageType });
		result.parts.push(...left.parts);
		if (right.parts.length == 0)
			result.suffix = left.suffix + right.suffix;
		else {
			result.parts.push(...right.parts);
			let middle = result.parts[left.parts.length];
			middle.prefix = left.suffix + middle.prefix;
			result.suffix = right.suffix;
		}
		return result;
	}

	#toInterpolatedString(expr)
	{
		let interpolated;
		if ((interpolated = expr) instanceof CiInterpolatedString)
			return interpolated;
		let result = Object.assign(new CiInterpolatedString(), { line: expr.line, type: this.program.system.stringStorageType });
		let literal;
		if ((literal = expr) instanceof CiLiteral)
			result.suffix = literal.getLiteralString();
		else {
			result.addPart("", expr);
			result.suffix = "";
		}
		return result;
	}

	#checkComparison(left, right)
	{
		if (left.type instanceof CiEnum)
			this.#coerce(right, left.type);
		else {
			let doubleType = this.program.system.doubleType;
			this.#coerce(left, doubleType);
			this.#coerce(right, doubleType);
		}
	}

	#openScope(scope)
	{
		scope.parent = this.#currentScope;
		this.#currentScope = scope;
	}

	#closeScope()
	{
		this.#currentScope = this.#currentScope.parent;
	}

	#resolveNew(expr)
	{
		if (expr.type != null)
			return expr;
		let type;
		let binaryNew;
		if ((binaryNew = expr.inner) instanceof CiBinaryExpr && binaryNew.op == CiToken.LEFT_BRACE) {
			type = this.#toType(binaryNew.left, true);
			let klass;
			if (!((klass = type) instanceof CiClassType) || klass instanceof CiReadWriteClassType)
				return this.#poisonError(expr, "Invalid argument to new");
			const init = binaryNew.right;
			this.#resolveObjectLiteral(klass, init);
			expr.type = Object.assign(new CiDynamicPtrType(), { line: expr.line, class: klass.class });
			expr.inner = init;
			return expr;
		}
		type = this.#toType(expr.inner, true);
		if (type instanceof CiArrayStorageType) {
			const array = type;
			expr.type = Object.assign(new CiDynamicPtrType(), { line: expr.line, class: this.program.system.arrayPtrClass, typeArg0: array.getElementType() });
			expr.inner = array.lengthExpr;
			return expr;
		}
		else if (type instanceof CiStorageType) {
			const klass = type;
			expr.type = Object.assign(new CiDynamicPtrType(), { line: expr.line, class: klass.class });
			expr.inner = null;
			return expr;
		}
		else
			return this.#poisonError(expr, "Invalid argument to new");
	}

	getResourceLength(name, expr)
	{
		return 0;
	}

	#visitPrefixExpr(expr)
	{
		let inner;
		let type;
		switch (expr.op) {
		case CiToken.INCREMENT:
		case CiToken.DECREMENT:
			inner = this.#visitExpr(expr.inner);
			this.#checkLValue(inner);
			this.#coerce(inner, this.program.system.doubleType);
			let xcrementRange;
			if ((xcrementRange = inner.type) instanceof CiRangeType) {
				let delta = expr.op == CiToken.INCREMENT ? 1 : -1;
				type = CiRangeType.new(xcrementRange.min + delta, xcrementRange.max + delta);
			}
			else
				type = inner.type;
			expr.inner = inner;
			expr.type = type;
			return expr;
		case CiToken.MINUS:
			inner = this.#visitExpr(expr.inner);
			this.#coerce(inner, this.program.system.doubleType);
			let negRange;
			if ((negRange = inner.type) instanceof CiRangeType) {
				if (negRange.min == negRange.max)
					return this.#toLiteralLong(expr, BigInt(-negRange.min));
				type = CiRangeType.new(CiSema.#saturatedNeg(negRange.max), CiSema.#saturatedNeg(negRange.min));
			}
			else {
				let d;
				if ((d = inner) instanceof CiLiteralDouble)
					return this.#toLiteralDouble(expr, -d.value);
				else {
					let l;
					if ((l = inner) instanceof CiLiteralLong)
						return this.#toLiteralLong(expr, -l.value);
					else
						type = inner.type;
				}
			}
			break;
		case CiToken.TILDE:
			inner = this.#visitExpr(expr.inner);
			if (inner.type instanceof CiEnumFlags)
				type = inner.type;
			else {
				this.#coerce(inner, this.program.system.intType);
				let notRange;
				if ((notRange = inner.type) instanceof CiRangeType)
					type = CiRangeType.new(~notRange.max, ~notRange.min);
				else
					type = inner.type;
			}
			break;
		case CiToken.EXCLAMATION_MARK:
			inner = this.#resolveBool(expr.inner);
			return Object.assign(new CiPrefixExpr(), { line: expr.line, op: CiToken.EXCLAMATION_MARK, inner: inner, type: this.program.system.boolType });
		case CiToken.NEW:
			return this.#resolveNew(expr);
		case CiToken.RESOURCE:
			let resourceName;
			if (!((resourceName = this.#foldConst(expr.inner)) instanceof CiLiteralString))
				return this.#poisonError(expr, "Resource name must be a string");
			inner = resourceName;
			type = Object.assign(new CiArrayStorageType(), { class: this.program.system.arrayStorageClass, typeArg0: this.program.system.byteType, length: this.getResourceLength(resourceName.value, expr) });
			break;
		default:
			throw new Error();
		}
		return Object.assign(new CiPrefixExpr(), { line: expr.line, op: expr.op, inner: inner, type: type });
	}

	#visitPostfixExpr(expr)
	{
		expr.inner = this.#visitExpr(expr.inner);
		switch (expr.op) {
		case CiToken.INCREMENT:
		case CiToken.DECREMENT:
			this.#checkLValue(expr.inner);
			this.#coerce(expr.inner, this.program.system.doubleType);
			expr.type = expr.inner.type;
			return expr;
		default:
			return this.#poisonError(expr, `Unexpected ${CiLexer.tokenToString(expr.op)}`);
		}
	}

	static #canCompareEqual(left, right)
	{
		if (left instanceof CiNumericType)
			return right instanceof CiNumericType;
		else if (left instanceof CiEnum)
			return left == right;
		else if (left instanceof CiClassType) {
			const leftClass = left;
			if (left.nullable && right.id == CiId.NULL_TYPE)
				return true;
			if ((left instanceof CiStorageType && (right instanceof CiStorageType || right instanceof CiDynamicPtrType)) || (left instanceof CiDynamicPtrType && right instanceof CiStorageType))
				return false;
			let rightClass;
			return (rightClass = right) instanceof CiClassType && (leftClass.class.isSameOrBaseOf(rightClass.class) || rightClass.class.isSameOrBaseOf(leftClass.class)) && leftClass.equalTypeArguments(rightClass);
		}
		else
			return left.id == CiId.NULL_TYPE && right.nullable;
	}

	#resolveEquality(expr, left, right)
	{
		if (!CiSema.#canCompareEqual(left.type, right.type))
			return this.#poisonError(expr, `Cannot compare ${left.type} with ${right.type}`);
		let leftRange;
		let rightRange;
		if ((leftRange = left.type) instanceof CiRangeType && (rightRange = right.type) instanceof CiRangeType) {
			if (leftRange.min == leftRange.max && leftRange.min == rightRange.min && leftRange.min == rightRange.max)
				return this.#toLiteralBool(expr, expr.op == CiToken.EQUAL);
			if (leftRange.max < rightRange.min || leftRange.min > rightRange.max)
				return this.#toLiteralBool(expr, expr.op == CiToken.NOT_EQUAL);
		}
		else {
			let leftLong;
			let rightLong;
			let leftDouble;
			let rightDouble;
			let leftString;
			let rightString;
			if ((leftLong = left) instanceof CiLiteralLong && (rightLong = right) instanceof CiLiteralLong)
				return this.#toLiteralBool(expr, (expr.op == CiToken.NOT_EQUAL) != (leftLong.value == rightLong.value));
			else if ((leftDouble = left) instanceof CiLiteralDouble && (rightDouble = right) instanceof CiLiteralDouble)
				return this.#toLiteralBool(expr, (expr.op == CiToken.NOT_EQUAL) != (leftDouble.value == rightDouble.value));
			else if ((leftString = left) instanceof CiLiteralString && (rightString = right) instanceof CiLiteralString)
				return this.#toLiteralBool(expr, (expr.op == CiToken.NOT_EQUAL) != (leftString.value == rightString.value));
			else if ((left instanceof CiLiteralNull && right instanceof CiLiteralNull) || (left instanceof CiLiteralFalse && right instanceof CiLiteralFalse) || (left instanceof CiLiteralTrue && right instanceof CiLiteralTrue))
				return this.#toLiteralBool(expr, expr.op == CiToken.EQUAL);
			else if ((left instanceof CiLiteralFalse && right instanceof CiLiteralTrue) || (left instanceof CiLiteralTrue && right instanceof CiLiteralFalse))
				return this.#toLiteralBool(expr, expr.op == CiToken.NOT_EQUAL);
			if (left.isConstEnum() && right.isConstEnum())
				return this.#toLiteralBool(expr, (expr.op == CiToken.NOT_EQUAL) != (left.intValue() == right.intValue()));
		}
		CiSema.#takePtr(left);
		CiSema.#takePtr(right);
		return Object.assign(new CiBinaryExpr(), { line: expr.line, left: left, op: expr.op, right: right, type: this.program.system.boolType });
	}

	#resolveIs(expr, left, right)
	{
		let leftPtr;
		if (!((leftPtr = left.type) instanceof CiClassType) || left.type instanceof CiStorageType)
			return this.#poisonError(expr, "Left hand side of the 'is' operator must be an object reference");
		let klass;
		if (right instanceof CiSymbolReference) {
			const symbol = right;
			let klass2;
			if ((klass2 = symbol.symbol) instanceof CiClass)
				klass = klass2;
			else
				return this.#poisonError(expr, "Right hand side of the 'is' operator must be a class name");
		}
		else if (right instanceof CiVar) {
			const def = right;
			let rightPtr;
			if (!((rightPtr = def.type) instanceof CiClassType))
				return this.#poisonError(expr, "Right hand side of the 'is' operator must be an object reference definition");
			if (rightPtr instanceof CiReadWriteClassType && !(leftPtr instanceof CiDynamicPtrType) && (rightPtr instanceof CiDynamicPtrType || !(leftPtr instanceof CiReadWriteClassType)))
				return this.#poisonError(expr, `${leftPtr} cannot be casted to ${rightPtr}`);
			klass = rightPtr.class;
		}
		else
			return this.#poisonError(expr, "Right hand side of the 'is' operator must be a class name");
		if (klass.isSameOrBaseOf(leftPtr.class))
			return this.#poisonError(expr, `${leftPtr} is ${klass.name}, the 'is' operator would always return 'true'`);
		if (!leftPtr.class.isSameOrBaseOf(klass))
			return this.#poisonError(expr, `${leftPtr} is not base class of ${klass.name}, the 'is' operator would always return 'false'`);
		expr.left = left;
		expr.type = this.program.system.boolType;
		return expr;
	}

	#visitBinaryExpr(expr)
	{
		let left = this.#visitExpr(expr.left);
		let right = this.#visitExpr(expr.right);
		if (left == this.#poison || right == this.#poison)
			return this.#poison;
		let type;
		switch (expr.op) {
		case CiToken.LEFT_BRACKET:
			let klass;
			if (!((klass = left.type) instanceof CiClassType))
				return this.#poisonError(expr, "Cannot index this object");
			switch (klass.class.id) {
			case CiId.STRING_CLASS:
				this.#coerce(right, this.program.system.intType);
				let stringLiteral;
				let indexLiteral;
				if ((stringLiteral = left) instanceof CiLiteralString && (indexLiteral = right) instanceof CiLiteralLong) {
					let i = indexLiteral.value;
					if (i >= 0 && i <= 2147483647) {
						let c = stringLiteral.getAsciiAt(Number(i));
						if (c >= 0)
							return CiLiteralChar.new(c, expr.line);
					}
				}
				type = this.program.system.charType;
				break;
			case CiId.ARRAY_PTR_CLASS:
			case CiId.ARRAY_STORAGE_CLASS:
			case CiId.LIST_CLASS:
				this.#coerce(right, this.program.system.intType);
				type = klass.getElementType();
				break;
			case CiId.DICTIONARY_CLASS:
			case CiId.SORTED_DICTIONARY_CLASS:
			case CiId.ORDERED_DICTIONARY_CLASS:
				this.#coerce(right, klass.getKeyType());
				type = klass.getValueType();
				break;
			default:
				return this.#poisonError(expr, "Cannot index this object");
			}
			break;
		case CiToken.PLUS:
			let leftAdd;
			let rightAdd;
			if ((leftAdd = left.type) instanceof CiRangeType && (rightAdd = right.type) instanceof CiRangeType) {
				type = CiRangeType.new(CiSema.#saturatedAdd(leftAdd.min, rightAdd.min), CiSema.#saturatedAdd(leftAdd.max, rightAdd.max));
			}
			else if (left.type instanceof CiStringType || right.type instanceof CiStringType) {
				this.#coerce(left, this.program.system.printableType);
				this.#coerce(right, this.program.system.printableType);
				let leftLiteral;
				let rightLiteral;
				if ((leftLiteral = left) instanceof CiLiteral && (rightLiteral = right) instanceof CiLiteral)
					return this.program.system.newLiteralString(leftLiteral.getLiteralString() + rightLiteral.getLiteralString(), expr.line);
				if (left instanceof CiInterpolatedString || right instanceof CiInterpolatedString)
					return this.#concatenate(this.#toInterpolatedString(left), this.#toInterpolatedString(right));
				type = this.program.system.stringStorageType;
			}
			else
				type = this.#getNumericType(left, right);
			break;
		case CiToken.MINUS:
			let leftSub;
			let rightSub;
			if ((leftSub = left.type) instanceof CiRangeType && (rightSub = right.type) instanceof CiRangeType) {
				type = CiRangeType.new(CiSema.#saturatedSub(leftSub.min, rightSub.max), CiSema.#saturatedSub(leftSub.max, rightSub.min));
			}
			else
				type = this.#getNumericType(left, right);
			break;
		case CiToken.ASTERISK:
			let leftMul;
			let rightMul;
			if ((leftMul = left.type) instanceof CiRangeType && (rightMul = right.type) instanceof CiRangeType) {
				type = CiSema.#newRangeType(CiSema.#saturatedMul(leftMul.min, rightMul.min), CiSema.#saturatedMul(leftMul.min, rightMul.max), CiSema.#saturatedMul(leftMul.max, rightMul.min), CiSema.#saturatedMul(leftMul.max, rightMul.max));
			}
			else
				type = this.#getNumericType(left, right);
			break;
		case CiToken.SLASH:
			let leftDiv;
			let rightDiv;
			if ((leftDiv = left.type) instanceof CiRangeType && (rightDiv = right.type) instanceof CiRangeType) {
				let denMin = rightDiv.min;
				if (denMin == 0)
					denMin = 1;
				let denMax = rightDiv.max;
				if (denMax == 0)
					denMax = -1;
				type = CiSema.#newRangeType(CiSema.#saturatedDiv(leftDiv.min, denMin), CiSema.#saturatedDiv(leftDiv.min, denMax), CiSema.#saturatedDiv(leftDiv.max, denMin), CiSema.#saturatedDiv(leftDiv.max, denMax));
			}
			else
				type = this.#getNumericType(left, right);
			break;
		case CiToken.MOD:
			let leftMod;
			let rightMod;
			if ((leftMod = left.type) instanceof CiRangeType && (rightMod = right.type) instanceof CiRangeType) {
				let den = ~Math.min(rightMod.min, -rightMod.max);
				if (den < 0)
					return this.#poisonError(expr, "Mod zero");
				type = CiRangeType.new(leftMod.min >= 0 ? 0 : Math.max(leftMod.min, -den), leftMod.max < 0 ? 0 : Math.min(leftMod.max, den));
			}
			else
				type = this.#getIntegerType(left, right);
			break;
		case CiToken.AND:
		case CiToken.OR:
		case CiToken.XOR:
			type = this.#bitwiseOp(left, expr.op, right);
			break;
		case CiToken.SHIFT_LEFT:
			let leftShl;
			let rightShl;
			if ((leftShl = left.type) instanceof CiRangeType && (rightShl = right.type) instanceof CiRangeType && leftShl.min == leftShl.max && rightShl.min == rightShl.max) {
				let result = leftShl.min << rightShl.min;
				type = CiRangeType.new(result, result);
			}
			else
				type = this.#getShiftType(left, right);
			break;
		case CiToken.SHIFT_RIGHT:
			let leftShr;
			let rightShr;
			if ((leftShr = left.type) instanceof CiRangeType && (rightShr = right.type) instanceof CiRangeType) {
				if (rightShr.min < 0)
					rightShr = CiRangeType.new(0, 32);
				type = CiRangeType.new(CiSema.#saturatedShiftRight(leftShr.min, leftShr.min < 0 ? rightShr.min : rightShr.max), CiSema.#saturatedShiftRight(leftShr.max, leftShr.max < 0 ? rightShr.max : rightShr.min));
			}
			else
				type = this.#getShiftType(left, right);
			break;
		case CiToken.EQUAL:
		case CiToken.NOT_EQUAL:
			return this.#resolveEquality(expr, left, right);
		case CiToken.LESS:
			let leftLess;
			let rightLess;
			if ((leftLess = left.type) instanceof CiRangeType && (rightLess = right.type) instanceof CiRangeType) {
				if (leftLess.max < rightLess.min)
					return this.#toLiteralBool(expr, true);
				if (leftLess.min >= rightLess.max)
					return this.#toLiteralBool(expr, false);
			}
			else
				this.#checkComparison(left, right);
			type = this.program.system.boolType;
			break;
		case CiToken.LESS_OR_EQUAL:
			let leftLeq;
			let rightLeq;
			if ((leftLeq = left.type) instanceof CiRangeType && (rightLeq = right.type) instanceof CiRangeType) {
				if (leftLeq.max <= rightLeq.min)
					return this.#toLiteralBool(expr, true);
				if (leftLeq.min > rightLeq.max)
					return this.#toLiteralBool(expr, false);
			}
			else
				this.#checkComparison(left, right);
			type = this.program.system.boolType;
			break;
		case CiToken.GREATER:
			let leftGreater;
			let rightGreater;
			if ((leftGreater = left.type) instanceof CiRangeType && (rightGreater = right.type) instanceof CiRangeType) {
				if (leftGreater.min > rightGreater.max)
					return this.#toLiteralBool(expr, true);
				if (leftGreater.max <= rightGreater.min)
					return this.#toLiteralBool(expr, false);
			}
			else
				this.#checkComparison(left, right);
			type = this.program.system.boolType;
			break;
		case CiToken.GREATER_OR_EQUAL:
			let leftGeq;
			let rightGeq;
			if ((leftGeq = left.type) instanceof CiRangeType && (rightGeq = right.type) instanceof CiRangeType) {
				if (leftGeq.min >= rightGeq.max)
					return this.#toLiteralBool(expr, true);
				if (leftGeq.max < rightGeq.min)
					return this.#toLiteralBool(expr, false);
			}
			else
				this.#checkComparison(left, right);
			type = this.program.system.boolType;
			break;
		case CiToken.COND_AND:
			this.#coerce(left, this.program.system.boolType);
			this.#coerce(right, this.program.system.boolType);
			if (left instanceof CiLiteralTrue)
				return right;
			if (left instanceof CiLiteralFalse || right instanceof CiLiteralTrue)
				return left;
			type = this.program.system.boolType;
			break;
		case CiToken.COND_OR:
			this.#coerce(left, this.program.system.boolType);
			this.#coerce(right, this.program.system.boolType);
			if (left instanceof CiLiteralTrue || right instanceof CiLiteralFalse)
				return left;
			if (left instanceof CiLiteralFalse)
				return right;
			type = this.program.system.boolType;
			break;
		case CiToken.ASSIGN:
			this.#checkLValue(left);
			this.#coerce(right, left.type);
			expr.left = left;
			expr.right = right;
			expr.type = left.type;
			return expr;
		case CiToken.ADD_ASSIGN:
			this.#checkLValue(left);
			if (left.type.id == CiId.STRING_STORAGE_TYPE)
				this.#coerce(right, this.program.system.printableType);
			else {
				this.#coerce(left, this.program.system.doubleType);
				this.#coerce(right, left.type);
			}
			expr.left = left;
			expr.right = right;
			expr.type = left.type;
			return expr;
		case CiToken.SUB_ASSIGN:
		case CiToken.MUL_ASSIGN:
		case CiToken.DIV_ASSIGN:
			this.#checkLValue(left);
			this.#coerce(left, this.program.system.doubleType);
			this.#coerce(right, left.type);
			expr.left = left;
			expr.right = right;
			expr.type = left.type;
			return expr;
		case CiToken.MOD_ASSIGN:
		case CiToken.SHIFT_LEFT_ASSIGN:
		case CiToken.SHIFT_RIGHT_ASSIGN:
			this.#checkLValue(left);
			this.#coerce(left, this.program.system.intType);
			this.#coerce(right, this.program.system.intType);
			expr.left = left;
			expr.right = right;
			expr.type = left.type;
			return expr;
		case CiToken.AND_ASSIGN:
		case CiToken.OR_ASSIGN:
		case CiToken.XOR_ASSIGN:
			this.#checkLValue(left);
			if (!this.#isEnumOp(left, right)) {
				this.#coerce(left, this.program.system.intType);
				this.#coerce(right, this.program.system.intType);
			}
			expr.left = left;
			expr.right = right;
			expr.type = left.type;
			return expr;
		case CiToken.IS:
			return this.#resolveIs(expr, left, right);
		case CiToken.RANGE:
			return this.#poisonError(expr, "Range within an expression");
		default:
			throw new Error();
		}
		let range;
		if ((range = type) instanceof CiRangeType && range.min == range.max)
			return this.#toLiteralLong(expr, BigInt(range.min));
		return Object.assign(new CiBinaryExpr(), { line: expr.line, left: left, op: expr.op, right: right, type: type });
	}

	#tryGetPtr(type, nullable)
	{
		if (type.id == CiId.STRING_STORAGE_TYPE)
			return nullable ? this.program.system.stringNullablePtrType : this.program.system.stringPtrType;
		let storage;
		if ((storage = type) instanceof CiStorageType)
			return Object.assign(new CiReadWriteClassType(), { class: storage.class.id == CiId.ARRAY_STORAGE_CLASS ? this.program.system.arrayPtrClass : storage.class, nullable: nullable, typeArg0: storage.typeArg0, typeArg1: storage.typeArg1 });
		let ptr;
		if (nullable && (ptr = type) instanceof CiClassType && !ptr.nullable) {
			let result;
			if (type instanceof CiDynamicPtrType)
				result = new CiDynamicPtrType();
			else if (type instanceof CiReadWriteClassType)
				result = new CiReadWriteClassType();
			else
				result = new CiClassType();
			result.class = ptr.class;
			result.nullable = true;
			result.typeArg0 = ptr.typeArg0;
			result.typeArg1 = ptr.typeArg1;
			return result;
		}
		return type;
	}

	static #getLowestCommonAncestor(left, right)
	{
		for (;;) {
			if (left.isSameOrBaseOf(right))
				return left;
			let parent;
			if ((parent = left.parent) instanceof CiClass)
				left = parent;
			else
				return null;
		}
	}

	#getCommonType(left, right)
	{
		let leftRange;
		let rightRange;
		if ((leftRange = left.type) instanceof CiRangeType && (rightRange = right.type) instanceof CiRangeType)
			return CiSema.#union(leftRange, rightRange);
		let nullable = left.type.nullable || right.type.nullable;
		let ptr = this.#tryGetPtr(left.type, nullable);
		if (ptr.isAssignableFrom(right.type))
			return ptr;
		ptr = this.#tryGetPtr(right.type, nullable);
		if (ptr.isAssignableFrom(left.type))
			return ptr;
		let leftClass;
		let rightClass;
		if ((leftClass = left.type) instanceof CiClassType && (rightClass = right.type) instanceof CiClassType && leftClass.equalTypeArguments(rightClass)) {
			let klass = CiSema.#getLowestCommonAncestor(leftClass.class, rightClass.class);
			if (klass != null) {
				let result;
				if (!(leftClass instanceof CiReadWriteClassType) || !(rightClass instanceof CiReadWriteClassType))
					result = new CiClassType();
				else if (leftClass instanceof CiDynamicPtrType && rightClass instanceof CiDynamicPtrType)
					result = new CiDynamicPtrType();
				else
					result = new CiReadWriteClassType();
				result.class = klass;
				result.nullable = nullable;
				result.typeArg0 = leftClass.typeArg0;
				result.typeArg1 = leftClass.typeArg1;
				return result;
			}
		}
		return this.#poisonError(left, `Incompatible types: ${left.type} and ${right.type}`);
	}

	#visitSelectExpr(expr)
	{
		let cond = this.#resolveBool(expr.cond);
		let onTrue = this.#visitExpr(expr.onTrue);
		let onFalse = this.#visitExpr(expr.onFalse);
		if (onTrue == this.#poison || onFalse == this.#poison)
			return this.#poison;
		let type = this.#getCommonType(onTrue, onFalse);
		this.#coerce(onTrue, type);
		this.#coerce(onFalse, type);
		if (cond instanceof CiLiteralTrue)
			return onTrue;
		if (cond instanceof CiLiteralFalse)
			return onFalse;
		return Object.assign(new CiSelectExpr(), { line: expr.line, cond: cond, onTrue: onTrue, onFalse: onFalse, type: type });
	}

	#evalType(generic, type)
	{
		if (type.id == CiId.TYPE_PARAM0)
			return generic.typeArg0;
		if (type.id == CiId.TYPE_PARAM0_NOT_FINAL)
			return generic.typeArg0.isFinal() ? null : generic.typeArg0;
		let collection;
		if ((collection = type) instanceof CiClassType && collection.class.typeParameterCount == 1 && collection.typeArg0.id == CiId.TYPE_PARAM0) {
			let result = type instanceof CiReadWriteClassType ? new CiReadWriteClassType() : new CiClassType();
			result.class = collection.class;
			result.typeArg0 = generic.typeArg0;
			return result;
		}
		return type;
	}

	#canCall(obj, method, arguments_)
	{
		let param = method.parameters.firstParameter();
		for (const arg of arguments_) {
			if (param == null)
				return false;
			let type = param.type;
			let generic;
			if (obj != null && (generic = obj.type) instanceof CiClassType)
				type = this.#evalType(generic, type);
			if (!type.isAssignableFrom(arg.type))
				return false;
			param = param.nextParameter();
		}
		return param == null || param.value != null;
	}

	#resolveCallWithArguments(expr, arguments_)
	{
		let symbol;
		if (!((symbol = this.#visitExpr(expr.method)) instanceof CiSymbolReference))
			return this.#poison;
		let method;
		if (symbol.symbol == null)
			return this.#poison;
		else if (symbol.symbol instanceof CiMethod) {
			const m = symbol.symbol;
			method = m;
		}
		else if (symbol.symbol instanceof CiMethodGroup) {
			const group = symbol.symbol;
			method = group.methods[0];
			if (!this.#canCall(symbol.left, method, arguments_))
				method = group.methods[1];
		}
		else
			return this.#poisonError(symbol, "Expected a method");
		let i = 0;
		for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
			let type = param.type;
			let generic;
			if (symbol.left != null && (generic = symbol.left.type) instanceof CiClassType) {
				type = this.#evalType(generic, type);
				if (type == null)
					continue;
			}
			if (i >= arguments_.length) {
				if (param.value != null)
					break;
				return this.#poisonError(expr, `Too few arguments for '${method.name}'`);
			}
			let arg = arguments_[i++];
			let lambda;
			if (type.id == CiId.TYPE_PARAM0_PREDICATE && (lambda = arg) instanceof CiLambdaExpr) {
				lambda.first.type = symbol.left.type.asClassType().typeArg0;
				this.#openScope(lambda);
				lambda.body = this.#visitExpr(lambda.body);
				this.#closeScope();
				this.#coerce(lambda.body, this.program.system.boolType);
			}
			else
				this.#coerce(arg, type);
		}
		if (i < arguments_.length)
			return this.#poisonError(arguments_[i], `Too many arguments for '${method.name}'`);
		if (method.throws) {
			if (this.#currentMethod == null)
				return this.#poisonError(expr, `Cannot call method '${method.name}' here because it is marked 'throws'`);
			if (!this.#currentMethod.throws)
				return this.#poisonError(expr, "Method marked 'throws' called from a method not marked 'throws'");
		}
		symbol.symbol = method;
		let ret;
		if (method.callType == CiCallType.STATIC && (ret = method.body) instanceof CiReturn && arguments_.every(arg => arg instanceof CiLiteral) && !this.#currentPureMethods.has(method)) {
			this.#currentPureMethods.add(method);
			i = 0;
			for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
				if (i < arguments_.length)
					this.#currentPureArguments[param] = arguments_[i++];
				else
					this.#currentPureArguments[param] = param.value;
			}
			let result = this.#visitExpr(ret.value);
			for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter())
				delete this.#currentPureArguments[param];
			this.#currentPureMethods.delete(method);
			if (result instanceof CiLiteral)
				return result;
		}
		if (this.#currentMethod != null)
			this.#currentMethod.calls.add(method);
		if (Object.keys(this.#currentPureArguments).length == 0) {
			expr.method = symbol;
			let type = method.type;
			let generic;
			if (symbol.left != null && (generic = symbol.left.type) instanceof CiClassType)
				type = this.#evalType(generic, type);
			expr.type = type;
		}
		return expr;
	}

	#visitCallExpr(expr)
	{
		if (Object.keys(this.#currentPureArguments).length == 0) {
			let arguments_ = expr.arguments;
			for (let i = 0; i < arguments_.length; i++) {
				if (!(arguments_[i] instanceof CiLambdaExpr))
					arguments_[i] = this.#visitExpr(arguments_[i]);
			}
			return this.#resolveCallWithArguments(expr, arguments_);
		}
		else {
			const arguments_ = [];
			for (const arg of expr.arguments)
				arguments_.push(this.#visitExpr(arg));
			return this.#resolveCallWithArguments(expr, arguments_);
		}
	}

	#resolveObjectLiteral(klass, init)
	{
		for (const item of init.items) {
			const field = item;
			console.assert(field.op == CiToken.ASSIGN);
			const symbol = field.left;
			this.#lookup(symbol, klass.class);
			if (symbol.symbol instanceof CiField) {
				field.right = this.#visitExpr(field.right);
				this.#coerce(field.right, symbol.type);
			}
			else
				this.reportError(field, "Expected a field");
		}
	}

	#visitVar(expr)
	{
		let type = this.#resolveType(expr);
		if (expr.value != null) {
			let storage;
			let init;
			if ((storage = type) instanceof CiStorageType && (init = expr.value) instanceof CiAggregateInitializer)
				this.#resolveObjectLiteral(storage, init);
			else {
				expr.value = this.#visitExpr(expr.value);
				if (!expr.isAssignableStorage()) {
					let array;
					if ((array = type) instanceof CiArrayStorageType) {
						type = array.getElementType();
						let literal;
						if (!((literal = expr.value) instanceof CiLiteral) || !literal.isDefaultValue())
							this.reportError(expr.value, "Only null, zero and false supported as an array initializer");
					}
					this.#coerce(expr.value, type);
				}
			}
		}
		this.#currentScope.add(expr);
	}

	#visitExpr(expr)
	{
		if (expr instanceof CiAggregateInitializer) {
			const aggregate = expr;
			let items = aggregate.items;
			for (let i = 0; i < items.length; i++)
				items[i] = this.#visitExpr(items[i]);
			return expr;
		}
		else if (expr instanceof CiLiteral)
			return expr;
		else if (expr instanceof CiInterpolatedString) {
			const interpolated = expr;
			return this.#visitInterpolatedString(interpolated);
		}
		else if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			return this.#visitSymbolReference(symbol);
		}
		else if (expr instanceof CiPrefixExpr) {
			const prefix = expr;
			return this.#visitPrefixExpr(prefix);
		}
		else if (expr instanceof CiPostfixExpr) {
			const postfix = expr;
			return this.#visitPostfixExpr(postfix);
		}
		else if (expr instanceof CiBinaryExpr) {
			const binary = expr;
			return this.#visitBinaryExpr(binary);
		}
		else if (expr instanceof CiSelectExpr) {
			const select = expr;
			return this.#visitSelectExpr(select);
		}
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			return this.#visitCallExpr(call);
		}
		else if (expr instanceof CiLambdaExpr) {
			this.reportError(expr, "Unexpected lambda expression");
			return expr;
		}
		else if (expr instanceof CiVar) {
			const def = expr;
			this.#visitVar(def);
			return expr;
		}
		else
			throw new Error();
	}

	#resolveBool(expr)
	{
		expr = this.#visitExpr(expr);
		this.#coerce(expr, this.program.system.boolType);
		return expr;
	}

	static #createClassPtr(klass, ptrModifier, nullable)
	{
		let ptr;
		switch (ptrModifier) {
		case CiToken.END_OF_FILE:
			ptr = new CiClassType();
			break;
		case CiToken.EXCLAMATION_MARK:
			ptr = new CiReadWriteClassType();
			break;
		case CiToken.HASH:
			ptr = new CiDynamicPtrType();
			break;
		default:
			throw new Error();
		}
		ptr.class = klass;
		ptr.nullable = nullable;
		return ptr;
	}

	#fillGenericClass(result, klass, typeArgExprs)
	{
		const typeArgs = [];
		for (const typeArgExpr of typeArgExprs.items)
			typeArgs.push(this.#toType(typeArgExpr, false));
		if (typeArgs.length != klass.typeParameterCount) {
			this.reportError(result, `Expected ${klass.typeParameterCount} type arguments for ${klass.name}, got ${typeArgs.length}`);
			return;
		}
		result.class = klass;
		result.typeArg0 = typeArgs[0];
		if (typeArgs.length == 2)
			result.typeArg1 = typeArgs[1];
	}

	#expectNoPtrModifier(expr, ptrModifier, nullable)
	{
		if (ptrModifier != CiToken.END_OF_FILE)
			this.reportError(expr, `Unexpected ${CiLexer.tokenToString(ptrModifier)} on a non-reference type`);
		if (nullable)
			this.reportError(expr, "Nullable value types not supported");
	}

	#toBaseType(expr, ptrModifier, nullable)
	{
		if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			let type;
			if ((type = this.program.tryLookup(symbol.name, true)) instanceof CiType) {
				let klass;
				if ((klass = type) instanceof CiClass) {
					if (klass.id == CiId.MATCH_CLASS && ptrModifier != CiToken.END_OF_FILE)
						this.reportError(expr, "Read-write references to the built-in class Match are not supported");
					let ptr = CiSema.#createClassPtr(klass, ptrModifier, nullable);
					let typeArgExprs;
					if ((typeArgExprs = symbol.left) instanceof CiAggregateInitializer)
						this.#fillGenericClass(ptr, klass, typeArgExprs);
					else if (symbol.left != null)
						return this.#poisonError(expr, "Invalid type");
					else
						ptr.name = klass.name;
					return ptr;
				}
				else if (symbol.left != null)
					return this.#poisonError(expr, "Invalid type");
				if (type.id == CiId.STRING_PTR_TYPE && nullable) {
					type = this.program.system.stringNullablePtrType;
					nullable = false;
				}
				this.#expectNoPtrModifier(expr, ptrModifier, nullable);
				return type;
			}
			return this.#poisonError(expr, `Type ${symbol.name} not found`);
		}
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			this.#expectNoPtrModifier(expr, ptrModifier, nullable);
			if (call.arguments.length != 0)
				this.reportError(call, "Expected empty parentheses for storage type");
			let typeArgExprs2;
			if ((typeArgExprs2 = call.method.left) instanceof CiAggregateInitializer) {
				let storage = Object.assign(new CiStorageType(), { line: call.line });
				let klass;
				if ((klass = this.program.tryLookup(call.method.name, true)) instanceof CiClass) {
					this.#fillGenericClass(storage, klass, typeArgExprs2);
					return storage;
				}
				return this.#poisonError(typeArgExprs2, `${call.method.name} is not a class`);
			}
			else if (call.method.left != null)
				return this.#poisonError(expr, "Invalid type");
			if (call.method.name == "string")
				return this.program.system.stringStorageType;
			let klass2;
			if ((klass2 = this.program.tryLookup(call.method.name, true)) instanceof CiClass)
				return Object.assign(new CiStorageType(), { class: klass2 });
			return this.#poisonError(expr, `Class ${call.method.name} not found`);
		}
		else
			return this.#poisonError(expr, "Invalid type");
	}

	#toType(expr, dynamic)
	{
		let minExpr = null;
		let range;
		if ((range = expr) instanceof CiBinaryExpr && range.op == CiToken.RANGE) {
			minExpr = range.left;
			expr = range.right;
		}
		let nullable;
		let ptrModifier;
		let outerArray = null;
		let innerArray = null;
		for (;;) {
			let question;
			if ((question = expr) instanceof CiPostfixExpr && question.op == CiToken.QUESTION_MARK) {
				expr = question.inner;
				nullable = true;
			}
			else
				nullable = false;
			let postfix;
			if ((postfix = expr) instanceof CiPostfixExpr && (postfix.op == CiToken.EXCLAMATION_MARK || postfix.op == CiToken.HASH)) {
				expr = postfix.inner;
				ptrModifier = postfix.op;
			}
			else
				ptrModifier = CiToken.END_OF_FILE;
			let binary;
			if ((binary = expr) instanceof CiBinaryExpr && binary.op == CiToken.LEFT_BRACKET) {
				if (binary.right != null) {
					this.#expectNoPtrModifier(expr, ptrModifier, nullable);
					let lengthExpr = this.#visitExpr(binary.right);
					let arrayStorage = Object.assign(new CiArrayStorageType(), { class: this.program.system.arrayStorageClass, typeArg0: outerArray, lengthExpr: lengthExpr, length: 0 });
					if (this.#coerce(lengthExpr, this.program.system.intType) && (!dynamic || binary.left.isIndexing())) {
						let literal;
						if ((literal = lengthExpr) instanceof CiLiteralLong) {
							let length = literal.value;
							if (length < 0)
								this.reportError(expr, "Expected non-negative integer");
							else if (length > 2147483647)
								this.reportError(expr, "Integer too big");
							else
								arrayStorage.length = Number(length);
						}
						else
							this.reportError(lengthExpr, "Expected constant value");
					}
					outerArray = arrayStorage;
				}
				else {
					let elementType = outerArray;
					outerArray = CiSema.#createClassPtr(this.program.system.arrayPtrClass, ptrModifier, nullable);
					outerArray.typeArg0 = elementType;
				}
				if (innerArray == null)
					innerArray = outerArray;
				expr = binary.left;
			}
			else
				break;
		}
		let baseType;
		if (minExpr != null) {
			this.#expectNoPtrModifier(expr, ptrModifier, nullable);
			let min = this.#foldConstInt(minExpr);
			let max = this.#foldConstInt(expr);
			if (min > max)
				return this.#poisonError(expr, "Range min greater than max");
			baseType = CiRangeType.new(min, max);
		}
		else
			baseType = this.#toBaseType(expr, ptrModifier, nullable);
		baseType.line = expr.line;
		if (outerArray == null)
			return baseType;
		innerArray.typeArg0 = baseType;
		return outerArray;
	}

	#resolveType(def)
	{
		def.type = this.#toType(def.typeExpr, false);
		return def.type;
	}

	#visitAssert(statement)
	{
		statement.cond = this.#resolveBool(statement.cond);
		if (statement.message != null) {
			statement.message = this.#visitExpr(statement.message);
			if (!(statement.message.type instanceof CiStringType))
				this.reportError(statement, "The second argument of 'assert' must be a string");
		}
	}

	#resolveStatements(statements)
	{
		let reachable = true;
		for (const statement of statements) {
			let konst;
			if ((konst = statement) instanceof CiConst) {
				this.#resolveConst(konst);
				this.#currentScope.add(konst);
				if (konst.type instanceof CiArrayStorageType) {
					const klass = this.#currentScope.getContainer();
					klass.constArrays.push(konst);
				}
			}
			else
				this.#visitStatement(statement);
			if (!reachable) {
				this.reportError(statement, "Unreachable statement");
				return false;
			}
			reachable = statement.completesNormally();
		}
		return reachable;
	}

	#visitBlock(statement)
	{
		this.#openScope(statement);
		statement.setCompletesNormally(this.#resolveStatements(statement.statements));
		this.#closeScope();
	}

	#resolveLoopCond(statement)
	{
		if (statement.cond != null) {
			statement.cond = this.#resolveBool(statement.cond);
			statement.setCompletesNormally(!(statement.cond instanceof CiLiteralTrue));
		}
		else
			statement.setCompletesNormally(false);
	}

	#visitDoWhile(statement)
	{
		this.#openScope(statement);
		this.#resolveLoopCond(statement);
		this.#visitStatement(statement.body);
		this.#closeScope();
	}

	#visitFor(statement)
	{
		this.#openScope(statement);
		if (statement.init != null)
			this.#visitStatement(statement.init);
		this.#resolveLoopCond(statement);
		if (statement.advance != null)
			this.#visitStatement(statement.advance);
		let iter;
		let cond;
		let limitSymbol;
		if ((iter = statement.init) instanceof CiVar && iter.type instanceof CiIntegerType && iter.value != null && (cond = statement.cond) instanceof CiBinaryExpr && cond.left.isReferenceTo(iter) && (cond.right instanceof CiLiteral || ((limitSymbol = cond.right) instanceof CiSymbolReference && limitSymbol.symbol instanceof CiVar))) {
			let step = 0n;
			let unary;
			let binary;
			let literalStep;
			if ((unary = statement.advance) instanceof CiUnaryExpr && unary.inner != null && unary.inner.isReferenceTo(iter)) {
				switch (unary.op) {
				case CiToken.INCREMENT:
					step = 1n;
					break;
				case CiToken.DECREMENT:
					step = -1n;
					break;
				default:
					break;
				}
			}
			else if ((binary = statement.advance) instanceof CiBinaryExpr && binary.left.isReferenceTo(iter) && (literalStep = binary.right) instanceof CiLiteralLong) {
				switch (binary.op) {
				case CiToken.ADD_ASSIGN:
					step = literalStep.value;
					break;
				case CiToken.SUB_ASSIGN:
					step = -literalStep.value;
					break;
				default:
					break;
				}
			}
			if ((step > 0 && (cond.op == CiToken.LESS || cond.op == CiToken.LESS_OR_EQUAL)) || (step < 0 && (cond.op == CiToken.GREATER || cond.op == CiToken.GREATER_OR_EQUAL))) {
				statement.isRange = true;
				statement.rangeStep = step;
			}
			statement.isIteratorUsed = false;
		}
		this.#visitStatement(statement.body);
		this.#closeScope();
	}

	#visitForeach(statement)
	{
		this.#openScope(statement);
		let element = statement.getVar();
		this.#resolveType(element);
		this.#visitExpr(statement.collection);
		let klass;
		if ((klass = statement.collection.type) instanceof CiClassType) {
			switch (klass.class.id) {
			case CiId.STRING_CLASS:
				if (statement.count() != 1 || !element.type.isAssignableFrom(this.program.system.intType))
					this.reportError(statement, "Expected int iterator variable");
				break;
			case CiId.ARRAY_STORAGE_CLASS:
			case CiId.LIST_CLASS:
			case CiId.HASH_SET_CLASS:
			case CiId.SORTED_SET_CLASS:
				if (statement.count() != 1)
					this.reportError(statement, "Expected one iterator variable");
				else if (!element.type.isAssignableFrom(klass.getElementType()))
					this.reportError(statement, `Cannot coerce ${klass.getElementType()} to ${element.type}`);
				break;
			case CiId.DICTIONARY_CLASS:
			case CiId.SORTED_DICTIONARY_CLASS:
			case CiId.ORDERED_DICTIONARY_CLASS:
				if (statement.count() != 2)
					this.reportError(statement, "Expected (TKey key, TValue value) iterator");
				else {
					let value = statement.getValueVar();
					this.#resolveType(value);
					if (!element.type.isAssignableFrom(klass.getKeyType()))
						this.reportError(statement, `Cannot coerce ${klass.getKeyType()} to ${element.type}`);
					else if (!value.type.isAssignableFrom(klass.getValueType()))
						this.reportError(statement, `Cannot coerce ${klass.getValueType()} to ${value.type}`);
				}
				break;
			default:
				this.reportError(statement, `'foreach' invalid on ${klass.class.name}`);
				break;
			}
		}
		else
			this.reportError(statement, `'foreach' invalid on ${statement.collection.type}`);
		statement.setCompletesNormally(true);
		this.#visitStatement(statement.body);
		this.#closeScope();
	}

	#visitIf(statement)
	{
		statement.cond = this.#resolveBool(statement.cond);
		this.#visitStatement(statement.onTrue);
		if (statement.onFalse != null) {
			this.#visitStatement(statement.onFalse);
			statement.setCompletesNormally(statement.onTrue.completesNormally() || statement.onFalse.completesNormally());
		}
		else
			statement.setCompletesNormally(true);
	}

	#visitLock(statement)
	{
		statement.lock = this.#visitExpr(statement.lock);
		this.#coerce(statement.lock, this.program.system.lockPtrType);
		this.#visitStatement(statement.body);
	}

	#visitReturn(statement)
	{
		if (this.#currentMethod.type.id == CiId.VOID_TYPE) {
			if (statement.value != null)
				this.reportError(statement, "Void method cannot return a value");
		}
		else if (statement.value == null)
			this.reportError(statement, "Missing return value");
		else {
			statement.value = this.#visitExpr(statement.value);
			this.#coerce(statement.value, this.#currentMethod.type);
			let symbol;
			let local;
			if ((symbol = statement.value) instanceof CiSymbolReference && (local = symbol.symbol) instanceof CiVar && ((local.type.isFinal() && !(this.#currentMethod.type instanceof CiStorageType)) || (local.type.id == CiId.STRING_STORAGE_TYPE && this.#currentMethod.type.id != CiId.STRING_STORAGE_TYPE)))
				this.reportError(statement, "Returning dangling reference to local storage");
		}
	}

	#visitSwitch(statement)
	{
		this.#openScope(statement);
		statement.value = this.#visitExpr(statement.value);
		let i;
		let klass;
		if (((i = statement.value.type) instanceof CiIntegerType && i.id != CiId.LONG_TYPE) || statement.value.type instanceof CiEnum) {
		}
		else if ((klass = statement.value.type) instanceof CiClassType && !(klass instanceof CiStorageType)) {
		}
		else {
			this.reportError(statement.value, `Switch on type ${statement.value.type} - expected int, enum, string or object reference`);
			return;
		}
		statement.setCompletesNormally(false);
		for (const kase of statement.cases) {
			for (let i = 0; i < kase.values.length; i++) {
				let switchPtr;
				if ((switchPtr = statement.value.type) instanceof CiClassType && switchPtr.class.id != CiId.STRING_CLASS) {
					let value = kase.values[i];
					let when1;
					if ((when1 = value) instanceof CiBinaryExpr && when1.op == CiToken.WHEN)
						value = when1.left;
					if (value instanceof CiLiteralNull) {
					}
					else {
						let def;
						if (!((def = value) instanceof CiVar) || def.value != null)
							this.reportError(kase.values[i], "Expected 'case Type name'");
						else {
							let casePtr;
							if (!((casePtr = this.#resolveType(def)) instanceof CiClassType) || casePtr instanceof CiStorageType)
								this.reportError(def, "'case' with non-reference type");
							else if (casePtr instanceof CiReadWriteClassType && !(switchPtr instanceof CiDynamicPtrType) && (casePtr instanceof CiDynamicPtrType || !(switchPtr instanceof CiReadWriteClassType)))
								this.reportError(def, `${switchPtr} cannot be casted to ${casePtr}`);
							else if (casePtr.class.isSameOrBaseOf(switchPtr.class))
								this.reportError(def, `${statement.value} is ${switchPtr}, 'case ${casePtr}' would always match`);
							else if (!switchPtr.class.isSameOrBaseOf(casePtr.class))
								this.reportError(def, `${switchPtr} is not base class of ${casePtr.class.name}, 'case ${casePtr}' would never match`);
							else {
								statement.add(def);
								let when2;
								if ((when2 = kase.values[i]) instanceof CiBinaryExpr && when2.op == CiToken.WHEN)
									when2.right = this.#resolveBool(when2.right);
							}
						}
					}
				}
				else {
					let when1;
					if ((when1 = kase.values[i]) instanceof CiBinaryExpr && when1.op == CiToken.WHEN) {
						when1.left = this.#foldConst(when1.left);
						this.#coerce(when1.left, statement.value.type);
						when1.right = this.#resolveBool(when1.right);
					}
					else {
						kase.values[i] = this.#foldConst(kase.values[i]);
						this.#coerce(kase.values[i], statement.value.type);
					}
				}
			}
			if (this.#resolveStatements(kase.body))
				this.reportError(kase.body.at(-1), "Case must end with break, continue, return or throw");
		}
		if (statement.defaultBody.length > 0) {
			let reachable = this.#resolveStatements(statement.defaultBody);
			if (reachable)
				this.reportError(statement.defaultBody.at(-1), "Default must end with break, continue, return or throw");
		}
		this.#closeScope();
	}

	#visitThrow(statement)
	{
		if (!this.#currentMethod.throws)
			this.reportError(statement, "'throw' in a method not marked 'throws'");
		statement.message = this.#visitExpr(statement.message);
		if (!(statement.message.type instanceof CiStringType))
			this.reportError(statement, "The argument of 'throw' must be a string");
	}

	#visitWhile(statement)
	{
		this.#openScope(statement);
		this.#resolveLoopCond(statement);
		this.#visitStatement(statement.body);
		this.#closeScope();
	}

	#visitStatement(statement)
	{
		if (statement instanceof CiAssert) {
			const asrt = statement;
			this.#visitAssert(asrt);
		}
		else if (statement instanceof CiBlock) {
			const block = statement;
			this.#visitBlock(block);
		}
		else if (statement instanceof CiBreak) {
			const brk = statement;
			brk.loopOrSwitch.setCompletesNormally(true);
		}
		else if (statement instanceof CiContinue || statement instanceof CiNative) {
		}
		else if (statement instanceof CiDoWhile) {
			const doWhile = statement;
			this.#visitDoWhile(doWhile);
		}
		else if (statement instanceof CiFor) {
			const forLoop = statement;
			this.#visitFor(forLoop);
		}
		else if (statement instanceof CiForeach) {
			const foreachLoop = statement;
			this.#visitForeach(foreachLoop);
		}
		else if (statement instanceof CiIf) {
			const ifStatement = statement;
			this.#visitIf(ifStatement);
		}
		else if (statement instanceof CiLock) {
			const lockStatement = statement;
			this.#visitLock(lockStatement);
		}
		else if (statement instanceof CiReturn) {
			const ret = statement;
			this.#visitReturn(ret);
		}
		else if (statement instanceof CiSwitch) {
			const switchStatement = statement;
			this.#visitSwitch(switchStatement);
		}
		else if (statement instanceof CiThrow) {
			const throwStatement = statement;
			this.#visitThrow(throwStatement);
		}
		else if (statement instanceof CiWhile) {
			const whileStatement = statement;
			this.#visitWhile(whileStatement);
		}
		else if (statement instanceof CiExpr) {
			const expr = statement;
			this.#visitExpr(expr);
		}
		else
			throw new Error();
	}

	#foldConst(expr)
	{
		expr = this.#visitExpr(expr);
		if (expr instanceof CiLiteral || expr.isConstEnum())
			return expr;
		this.reportError(expr, "Expected constant value");
		return expr;
	}

	#foldConstInt(expr)
	{
		let literal;
		if ((literal = this.#foldConst(expr)) instanceof CiLiteralLong) {
			let l = literal.value;
			if (l < -2147483648 || l > 2147483647) {
				this.reportError(expr, "Only 32-bit ranges supported");
				return 0;
			}
			return Number(l);
		}
		this.reportError(expr, "Expected integer");
		return 0;
	}

	#resolveConst(konst)
	{
		switch (konst.visitStatus) {
		case CiVisitStatus.NOT_YET:
			break;
		case CiVisitStatus.IN_PROGRESS:
			konst.value = this.#poisonError(konst, `Circular dependency in value of constant ${konst.name}`);
			konst.visitStatus = CiVisitStatus.DONE;
			return;
		case CiVisitStatus.DONE:
			return;
		}
		konst.visitStatus = CiVisitStatus.IN_PROGRESS;
		if (!(this.#currentScope instanceof CiEnum))
			this.#resolveType(konst);
		konst.value = this.#visitExpr(konst.value);
		let coll;
		if ((coll = konst.value) instanceof CiAggregateInitializer) {
			let array;
			if ((array = konst.type) instanceof CiClassType) {
				let elementType = array.getElementType();
				let arrayStg;
				if ((arrayStg = array) instanceof CiArrayStorageType) {
					if (arrayStg.length != coll.items.length)
						this.reportError(konst, `Declared ${arrayStg.length} elements, initialized ${coll.items.length}`);
				}
				else if (array instanceof CiReadWriteClassType)
					this.reportError(konst, "Invalid constant type");
				else
					konst.type = Object.assign(new CiArrayStorageType(), { class: this.program.system.arrayStorageClass, typeArg0: elementType, length: coll.items.length });
				coll.type = konst.type;
				for (const item of coll.items)
					this.#coerce(item, elementType);
			}
			else
				this.reportError(konst, `Array initializer for scalar constant ${konst.name}`);
		}
		else if (this.#currentScope instanceof CiEnum && konst.value.type instanceof CiRangeType && konst.value instanceof CiLiteral) {
		}
		else if (konst.value instanceof CiLiteral || konst.value.isConstEnum())
			this.#coerce(konst.value, konst.type);
		else if (konst.value != this.#poison)
			this.reportError(konst.value, `Value for constant ${konst.name} is not constant`);
		konst.inMethod = this.#currentMethod;
		konst.visitStatus = CiVisitStatus.DONE;
	}

	#resolveConsts(container)
	{
		this.#currentScope = container;
		if (container instanceof CiClass) {
			const klass = container;
			for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
				let konst;
				if ((konst = symbol) instanceof CiConst)
					this.#resolveConst(konst);
			}
		}
		else if (container instanceof CiEnum) {
			const enu = container;
			let previous = null;
			for (let symbol = enu.first; symbol != null; symbol = symbol.next) {
				let konst;
				if ((konst = symbol) instanceof CiConst) {
					if (konst.value != null) {
						this.#resolveConst(konst);
						enu.hasExplicitValue = true;
					}
					else
						konst.value = Object.assign(new CiImplicitEnumValue(), { value: previous == null ? 0 : previous.value.intValue() + 1 });
					previous = konst;
				}
			}
		}
		else
			throw new Error();
	}

	#resolveTypes(klass)
	{
		this.#currentScope = klass;
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			if (symbol instanceof CiField) {
				const field = symbol;
				let type = this.#resolveType(field);
				if (field.value != null) {
					field.value = this.#visitExpr(field.value);
					if (!field.isAssignableStorage()) {
						let array;
						this.#coerce(field.value, (array = type) instanceof CiArrayStorageType ? array.getElementType() : type);
					}
				}
			}
			else if (symbol instanceof CiMethod) {
				const method = symbol;
				if (method.typeExpr == this.program.system.voidType)
					method.type = this.program.system.voidType;
				else
					this.#resolveType(method);
				for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
					this.#resolveType(param);
					if (param.value != null) {
						param.value = this.#foldConst(param.value);
						this.#coerce(param.value, param.type);
					}
				}
			}
		}
	}

	#resolveCode(klass)
	{
		if (klass.constructor_ != null) {
			this.#currentScope = klass;
			this.#currentMethod = klass.constructor_;
			this.#visitStatement(klass.constructor_.body);
			this.#currentMethod = null;
		}
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let method;
			if ((method = symbol) instanceof CiMethod) {
				if (method.name == "ToString" && method.callType != CiCallType.STATIC && method.parameters.count() == 0)
					method.id = CiId.CLASS_TO_STRING;
				if (method.body != null) {
					if (method.callType == CiCallType.OVERRIDE || method.callType == CiCallType.SEALED) {
						let baseMethod;
						if ((baseMethod = klass.parent.tryLookup(method.name, false)) instanceof CiMethod) {
							switch (baseMethod.callType) {
							case CiCallType.ABSTRACT:
							case CiCallType.VIRTUAL:
							case CiCallType.OVERRIDE:
								break;
							default:
								this.reportError(method, "Base method is not abstract or virtual");
								break;
							}
							if (!method.type.equalsType(baseMethod.type))
								this.reportError(method, "Base method has a different return type");
							if (method.isMutator != baseMethod.isMutator) {
								if (method.isMutator)
									this.reportError(method, "Mutating method cannot override a non-mutating method");
								else
									this.reportError(method, "Non-mutating method cannot override a mutating method");
							}
							let baseParam = baseMethod.parameters.firstParameter();
							for (let param = method.parameters.firstParameter();; param = param.nextParameter()) {
								if (param == null) {
									if (baseParam != null)
										this.reportError(method, "Fewer parameters than the overridden method");
									break;
								}
								if (baseParam == null) {
									this.reportError(method, "More parameters than the overridden method");
									break;
								}
								if (!param.type.equalsType(baseParam.type)) {
									this.reportError(method, "Base method has a different parameter type");
									break;
								}
								baseParam = baseParam.nextParameter();
							}
							baseMethod.calls.add(method);
						}
						else
							this.reportError(method, "No method to override");
					}
					this.#currentScope = method.parameters;
					this.#currentMethod = method;
					if (!(method.body instanceof CiScope))
						this.#openScope(method.methodScope);
					this.#visitStatement(method.body);
					if (method.type.id != CiId.VOID_TYPE && method.body.completesNormally())
						this.reportError(method.body, "Method can complete without a return value");
					this.#currentMethod = null;
				}
			}
		}
	}

	static #markMethodLive(method)
	{
		if (method.isLive)
			return;
		method.isLive = true;
		for (const called of method.calls)
			CiSema.#markMethodLive(called);
	}

	static #markClassLive(klass)
	{
		if (klass.isPublic) {
			for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
				let method;
				if ((method = symbol) instanceof CiMethod && (method.visibility == CiVisibility.PUBLIC || method.visibility == CiVisibility.PROTECTED))
					CiSema.#markMethodLive(method);
			}
		}
		if (klass.constructor_ != null)
			CiSema.#markMethodLive(klass.constructor_);
	}

	process(program)
	{
		this.program = program;
		for (let type = program.first; type != null; type = type.next) {
			let klass;
			if ((klass = type) instanceof CiClass)
				this.#resolveBase(klass);
		}
		for (const klass of program.classes)
			this.#checkBaseCycle(klass);
		for (let type = program.first; type != null; type = type.next) {
			const container = type;
			this.#resolveConsts(container);
		}
		for (const klass of program.classes)
			this.#resolveTypes(klass);
		for (const klass of program.classes)
			this.#resolveCode(klass);
		for (const klass of program.classes)
			CiSema.#markClassLive(klass);
	}
}

export class GenHost
{
}

export class GenBase extends CiVisitor
{
	namespace;
	outputFile;
	host;
	#writer;
	#stringWriter = new StringWriter();
	indent = 0;
	atLineStart = true;
	#atChildStart = false;
	#inChildBlock = false;
	inHeaderFile = false;
	#includes = {};
	currentMethod = null;
	writtenClasses = new Set();
	currentTemporaries = [];

	getCurrentContainer()
	{
		const klass = this.currentMethod.parent;
		return klass;
	}

	notSupported(statement, feature)
	{
		this.reportError(statement, `${feature} not supported when targeting ${this.getTargetName()}`);
	}

	notYet(statement, feature)
	{
		this.reportError(statement, `${feature} not supported yet when targeting ${this.getTargetName()}`);
	}

	startLine()
	{
		if (this.atLineStart) {
			if (this.#atChildStart) {
				this.#atChildStart = false;
				this.#writer.write(String.fromCharCode(10));
				this.indent++;
			}
			for (let i = 0; i < this.indent; i++)
				this.#writer.write(String.fromCharCode(9));
			this.atLineStart = false;
		}
	}

	writeChar(c)
	{
		this.startLine();
		this.#writer.write(String.fromCodePoint(c));
	}

	write(s)
	{
		this.startLine();
		this.#writer.write(s);
	}

	visitLiteralNull()
	{
		this.write("null");
	}

	visitLiteralFalse()
	{
		this.write("false");
	}

	visitLiteralTrue()
	{
		this.write("true");
	}

	visitLiteralLong(i)
	{
		this.#writer.write(String(i));
	}

	getLiteralChars()
	{
		return 0;
	}

	visitLiteralChar(c)
	{
		if (c < this.getLiteralChars()) {
			this.writeChar(39);
			switch (c) {
			case 10:
				this.write("\\n");
				break;
			case 13:
				this.write("\\r");
				break;
			case 9:
				this.write("\\t");
				break;
			case 39:
				this.write("\\'");
				break;
			case 92:
				this.write("\\\\");
				break;
			default:
				this.writeChar(c);
				break;
			}
			this.writeChar(39);
		}
		else
			this.#writer.write(String(c));
	}

	visitLiteralDouble(value)
	{
		let s = `${value}`;
		this.write(s);
		for (const c of s) {
			switch (c.codePointAt(0)) {
			case 45:
			case 48:
			case 49:
			case 50:
			case 51:
			case 52:
			case 53:
			case 54:
			case 55:
			case 56:
			case 57:
				break;
			default:
				return;
			}
		}
		this.write(".0");
	}

	visitLiteralString(value)
	{
		this.writeChar(34);
		this.write(value);
		this.writeChar(34);
	}

	#writeLowercaseChar(c)
	{
		if (c >= 65 && c <= 90)
			c += 32;
		this.#writer.write(String.fromCharCode(c));
	}

	#writeUppercaseChar(c)
	{
		if (c >= 97 && c <= 122)
			c -= 32;
		this.#writer.write(String.fromCharCode(c));
	}

	writeLowercase(s)
	{
		this.startLine();
		for (const c of s)
			this.#writeLowercaseChar(c.codePointAt(0));
	}

	writeCamelCase(s)
	{
		this.startLine();
		this.#writeLowercaseChar(s.charCodeAt(0));
		this.#writer.write(s.substring(1));
	}

	writePascalCase(s)
	{
		this.startLine();
		this.#writeUppercaseChar(s.charCodeAt(0));
		this.#writer.write(s.substring(1));
	}

	writeUppercaseWithUnderscores(s)
	{
		this.startLine();
		let first = true;
		for (const c of s) {
			if (!first && c.codePointAt(0) >= 65 && c.codePointAt(0) <= 90) {
				this.#writer.write(String.fromCharCode(95));
				this.#writer.write(String.fromCharCode(c.codePointAt(0)));
			}
			else
				this.#writeUppercaseChar(c.codePointAt(0));
			first = false;
		}
	}

	writeLowercaseWithUnderscores(s)
	{
		this.startLine();
		let first = true;
		for (const c of s) {
			if (c.codePointAt(0) >= 65 && c.codePointAt(0) <= 90) {
				if (!first)
					this.#writer.write(String.fromCharCode(95));
				this.#writeLowercaseChar(c.codePointAt(0));
			}
			else
				this.#writer.write(String.fromCharCode(c.codePointAt(0)));
			first = false;
		}
	}

	writeNewLine()
	{
		this.#writer.write(String.fromCharCode(10));
		this.atLineStart = true;
	}

	writeCharLine(c)
	{
		this.writeChar(c);
		this.writeNewLine();
	}

	writeLine(s)
	{
		this.write(s);
		this.writeNewLine();
	}

	writeBanner()
	{
		this.writeLine("// Generated automatically with \"cito\". Do not edit.");
	}

	createFile(directory, filename)
	{
		this.#writer = this.host.createFile(directory, filename);
		this.writeBanner();
	}

	createOutputFile()
	{
		this.createFile(null, this.outputFile);
	}

	closeFile()
	{
		if (!this.host.closeFile(this.hasErrors))
			this.hasErrors = true;
	}

	openStringWriter()
	{
		this.#writer = this.#stringWriter;
	}

	closeStringWriter()
	{
		this.#writer.write(this.#stringWriter.toString());
		this.#stringWriter.clear();
	}

	include(name)
	{
		if (!this.#includes.hasOwnProperty(name))
			this.#includes[name] = this.inHeaderFile;
	}

	writeIncludes(prefix, suffix)
	{
		for (const [name, inHeaderFile] of Object.entries(this.#includes).sort((a, b) => a[0].localeCompare(b[0]))) {
			if (inHeaderFile == this.inHeaderFile) {
				this.write(prefix);
				this.write(name);
				this.writeLine(suffix);
			}
		}
		if (!this.inHeaderFile)
			for (const key in this.#includes)
				delete this.#includes[key];;
	}

	startDocLine()
	{
		this.write(" * ");
	}

	writeXmlDoc(text)
	{
		for (const c of text) {
			switch (c.codePointAt(0)) {
			case 38:
				this.write("&amp;");
				break;
			case 60:
				this.write("&lt;");
				break;
			case 62:
				this.write("&gt;");
				break;
			default:
				this.writeChar(c.codePointAt(0));
				break;
			}
		}
	}

	writeDocPara(para, many)
	{
		if (many) {
			this.writeNewLine();
			this.write(" * <p>");
		}
		for (const inline of para.children) {
			if (inline instanceof CiDocText) {
				const text = inline;
				this.writeXmlDoc(text.text);
			}
			else if (inline instanceof CiDocCode) {
				const code = inline;
				this.write("<code>");
				this.writeXmlDoc(code.text);
				this.write("</code>");
			}
			else if (inline instanceof CiDocLine) {
				this.writeNewLine();
				this.startDocLine();
			}
			else
				throw new Error();
		}
	}

	writeDocList(list)
	{
		this.writeNewLine();
		this.writeLine(" * <ul>");
		for (const item of list.items) {
			this.write(" * <li>");
			this.writeDocPara(item, false);
			this.writeLine("</li>");
		}
		this.write(" * </ul>");
	}

	writeDocBlock(block, many)
	{
		if (block instanceof CiDocPara) {
			const para = block;
			this.writeDocPara(para, many);
		}
		else if (block instanceof CiDocList) {
			const list = block;
			this.writeDocList(list);
		}
		else
			throw new Error();
	}

	writeContent(doc)
	{
		this.startDocLine();
		this.writeDocPara(doc.summary, false);
		this.writeNewLine();
		if (doc.details.length > 0) {
			this.startDocLine();
			if (doc.details.length == 1)
				this.writeDocBlock(doc.details[0], false);
			else {
				for (const block of doc.details)
					this.writeDocBlock(block, true);
			}
			this.writeNewLine();
		}
	}

	writeDoc(doc)
	{
		if (doc != null) {
			this.writeLine("/**");
			this.writeContent(doc);
			this.writeLine(" */");
		}
	}

	writeSelfDoc(method)
	{
	}

	writeParameterDoc(param, first)
	{
		this.write(" * @param ");
		this.writeName(param);
		this.writeChar(32);
		this.writeDocPara(param.documentation.summary, false);
		this.writeNewLine();
	}

	writeParametersDoc(method)
	{
		let first = true;
		for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
			if (param.documentation != null) {
				this.writeParameterDoc(param, first);
				first = false;
			}
		}
	}

	writeMethodDoc(method)
	{
		if (method.documentation == null)
			return;
		this.writeLine("/**");
		this.writeContent(method.documentation);
		this.writeSelfDoc(method);
		this.writeParametersDoc(method);
		this.writeLine(" */");
	}

	writeTopLevelNatives(program)
	{
		for (const content of program.topLevelNatives)
			this.write(content);
	}

	openBlock()
	{
		this.writeCharLine(123);
		this.indent++;
	}

	closeBlock()
	{
		this.indent--;
		this.writeCharLine(125);
	}

	endStatement()
	{
		this.writeCharLine(59);
	}

	writeComma(i)
	{
		if (i > 0) {
			if ((i & 15) == 0) {
				this.writeCharLine(44);
				this.writeChar(9);
			}
			else
				this.write(", ");
		}
	}

	writeBytes(content)
	{
		let i = 0;
		for (const b of content) {
			this.writeComma(i++);
			this.visitLiteralLong(BigInt(b));
		}
	}

	getTypeId(type, promote)
	{
		return promote && type instanceof CiRangeType ? CiId.INT_TYPE : type.id;
	}

	writeLocalName(symbol, parent)
	{
		if (symbol instanceof CiField)
			this.write("this.");
		this.writeName(symbol);
	}

	writeDoubling(s, doubled)
	{
		for (const c of s) {
			if (c.codePointAt(0) == doubled)
				this.writeChar(c.codePointAt(0));
			this.writeChar(c.codePointAt(0));
		}
	}

	writePrintfWidth(part)
	{
		if (part.widthExpr != null)
			this.visitLiteralLong(BigInt(part.width));
		if (part.precision >= 0) {
			this.writeChar(46);
			this.visitLiteralLong(BigInt(part.precision));
		}
	}

	static #getPrintfFormat(type, format)
	{
		if (type instanceof CiIntegerType)
			return format == 120 || format == 88 ? format : 100;
		else if (type instanceof CiNumericType) {
			switch (format) {
			case 69:
			case 101:
			case 102:
			case 71:
			case 103:
				return format;
			case 70:
				return 102;
			default:
				return 103;
			}
		}
		else if (type instanceof CiClassType)
			return 115;
		else
			throw new Error();
	}

	writePrintfFormat(expr)
	{
		for (const part of expr.parts) {
			this.writeDoubling(part.prefix, 37);
			this.writeChar(37);
			this.writePrintfWidth(part);
			this.writeChar(GenBase.#getPrintfFormat(part.argument.type, part.format));
		}
		this.writeDoubling(expr.suffix, 37);
	}

	writePyFormat(part)
	{
		if (part.widthExpr != null || part.precision >= 0 || (part.format != 32 && part.format != 68))
			this.writeChar(58);
		if (part.widthExpr != null) {
			if (part.width >= 0) {
				if (!(part.argument.type instanceof CiNumericType))
					this.writeChar(62);
				this.visitLiteralLong(BigInt(part.width));
			}
			else {
				this.writeChar(60);
				this.visitLiteralLong(BigInt(-part.width));
			}
		}
		if (part.precision >= 0) {
			this.writeChar(part.argument.type instanceof CiIntegerType ? 48 : 46);
			this.visitLiteralLong(BigInt(part.precision));
		}
		if (part.format != 32 && part.format != 68)
			this.writeChar(part.format);
		this.writeChar(125);
	}

	writeInterpolatedStringArg(expr)
	{
		expr.accept(this, CiPriority.ARGUMENT);
	}

	writeInterpolatedStringArgs(expr)
	{
		for (const part of expr.parts) {
			this.write(", ");
			this.writeInterpolatedStringArg(part.argument);
		}
	}

	writePrintf(expr, newLine)
	{
		this.writeChar(34);
		this.writePrintfFormat(expr);
		if (newLine)
			this.write("\\n");
		this.writeChar(34);
		this.writeInterpolatedStringArgs(expr);
		this.writeChar(41);
	}

	writePostfix(obj, s)
	{
		obj.accept(this, CiPriority.PRIMARY);
		this.write(s);
	}

	writeCall(function_, arg0, arg1 = null, arg2 = null)
	{
		this.write(function_);
		this.writeChar(40);
		arg0.accept(this, CiPriority.ARGUMENT);
		if (arg1 != null) {
			this.write(", ");
			arg1.accept(this, CiPriority.ARGUMENT);
			if (arg2 != null) {
				this.write(", ");
				arg2.accept(this, CiPriority.ARGUMENT);
			}
		}
		this.writeChar(41);
	}

	writeMemberOp(left, symbol)
	{
		this.writeChar(46);
	}

	writeMethodCall(obj, method, arg0, arg1 = null)
	{
		obj.accept(this, CiPriority.PRIMARY);
		this.writeMemberOp(obj, null);
		this.writeCall(method, arg0, arg1);
	}

	writeSelectValues(type, expr)
	{
		this.writeCoerced(type, expr.onTrue, CiPriority.SELECT);
		this.write(" : ");
		this.writeCoerced(type, expr.onFalse, CiPriority.SELECT);
	}

	writeCoercedSelect(type, expr, parent)
	{
		if (parent > CiPriority.SELECT)
			this.writeChar(40);
		expr.cond.accept(this, CiPriority.SELECT_COND);
		this.write(" ? ");
		this.writeSelectValues(type, expr);
		if (parent > CiPriority.SELECT)
			this.writeChar(41);
	}

	writeCoercedInternal(type, expr, parent)
	{
		expr.accept(this, parent);
	}

	writeCoerced(type, expr, parent)
	{
		let select;
		if ((select = expr) instanceof CiSelectExpr)
			this.writeCoercedSelect(type, select, parent);
		else
			this.writeCoercedInternal(type, expr, parent);
	}

	writeCoercedExpr(type, expr)
	{
		this.writeCoerced(type, expr, CiPriority.ARGUMENT);
	}

	writeStronglyCoerced(type, expr)
	{
		this.writeCoerced(type, expr, CiPriority.ARGUMENT);
	}

	writeCoercedLiteral(type, expr)
	{
		expr.accept(this, CiPriority.ARGUMENT);
	}

	writeCoercedLiterals(type, exprs)
	{
		for (let i = 0; i < exprs.length; i++) {
			this.writeComma(i);
			this.writeCoercedLiteral(type, exprs[i]);
		}
	}

	writeArgs(method, args)
	{
		let param = method.parameters.firstParameter();
		let first = true;
		for (const arg of args) {
			if (!first)
				this.write(", ");
			first = false;
			this.writeStronglyCoerced(param.type, arg);
			param = param.nextParameter();
		}
	}

	writeArgsInParentheses(method, args)
	{
		this.writeChar(40);
		this.writeArgs(method, args);
		this.writeChar(41);
	}

	writeNewArrayStorage(array)
	{
		this.writeNewArray(array.getElementType(), array.lengthExpr, CiPriority.ARGUMENT);
	}

	writeNewStorage(type)
	{
		if (type instanceof CiArrayStorageType) {
			const array = type;
			this.writeNewArrayStorage(array);
		}
		else if (type instanceof CiStorageType) {
			const storage = type;
			this.writeNew(storage, CiPriority.ARGUMENT);
		}
		else
			throw new Error();
	}

	writeArrayStorageInit(array, value)
	{
		this.write(" = ");
		this.writeNewArrayStorage(array);
	}

	writeNewWithFields(type, init)
	{
		this.writeNew(type, CiPriority.ARGUMENT);
	}

	writeStorageInit(def)
	{
		this.write(" = ");
		let init;
		if ((init = def.value) instanceof CiAggregateInitializer) {
			const klass = def.type;
			this.writeNewWithFields(klass, init);
		}
		else
			this.writeNewStorage(def.type);
	}

	writeVarInit(def)
	{
		if (def.isAssignableStorage()) {
		}
		else {
			let array;
			if ((array = def.type) instanceof CiArrayStorageType)
				this.writeArrayStorageInit(array, def.value);
			else if (def.value != null && !(def.value instanceof CiAggregateInitializer)) {
				this.write(" = ");
				this.writeCoercedExpr(def.type, def.value);
			}
			else if (def.type.isFinal() && !(def.parent instanceof CiParameters))
				this.writeStorageInit(def);
		}
	}

	writeVar(def)
	{
		this.writeTypeAndName(def);
		this.writeVarInit(def);
	}

	visitVar(expr)
	{
		this.writeVar(expr);
	}

	writeObjectLiteral(init, separator)
	{
		let prefix = " { ";
		for (const item of init.items) {
			this.write(prefix);
			const assign = item;
			const field = assign.left;
			this.writeName(field.symbol);
			this.write(separator);
			this.writeCoerced(assign.left.type, assign.right, CiPriority.ARGUMENT);
			prefix = ", ";
		}
		this.write(" }");
	}

	static #getAggregateInitializer(def)
	{
		let expr = def.value;
		let unary;
		if ((unary = expr) instanceof CiPrefixExpr)
			expr = unary.inner;
		let init;
		return (init = expr) instanceof CiAggregateInitializer ? init : null;
	}

	#writeAggregateInitField(obj, item)
	{
		const assign = item;
		const field = assign.left;
		this.writeMemberOp(obj, field);
		this.writeName(field.symbol);
		this.write(" = ");
		this.writeCoerced(field.type, assign.right, CiPriority.ARGUMENT);
		this.endStatement();
	}

	writeInitCode(def)
	{
		let init = GenBase.#getAggregateInitializer(def);
		if (init != null) {
			for (const item of init.items) {
				this.writeLocalName(def, CiPriority.PRIMARY);
				this.#writeAggregateInitField(def, item);
			}
		}
	}

	defineIsVar(binary)
	{
		let def;
		if ((def = binary.right) instanceof CiVar) {
			this.ensureChildBlock();
			this.writeVar(def);
			this.endStatement();
		}
	}

	writeArrayElement(def, nesting)
	{
		this.writeLocalName(def, CiPriority.PRIMARY);
		for (let i = 0; i < nesting; i++) {
			this.write("[_i");
			this.visitLiteralLong(BigInt(i));
			this.writeChar(93);
		}
	}

	openLoop(intString, nesting, count)
	{
		this.write("for (");
		this.write(intString);
		this.write(" _i");
		this.visitLiteralLong(BigInt(nesting));
		this.write(" = 0; _i");
		this.visitLiteralLong(BigInt(nesting));
		this.write(" < ");
		this.visitLiteralLong(BigInt(count));
		this.write("; _i");
		this.visitLiteralLong(BigInt(nesting));
		this.write("++) ");
		this.openBlock();
	}

	writeResourceName(name)
	{
		for (const c of name)
			this.writeChar(CiLexer.isLetterOrDigit(c.codePointAt(0)) ? c.codePointAt(0) : 95);
	}

	visitPrefixExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.INCREMENT:
			this.write("++");
			break;
		case CiToken.DECREMENT:
			this.write("--");
			break;
		case CiToken.MINUS:
			this.writeChar(45);
			let inner;
			if ((inner = expr.inner) instanceof CiPrefixExpr && (inner.op == CiToken.MINUS || inner.op == CiToken.DECREMENT))
				this.writeChar(32);
			break;
		case CiToken.TILDE:
			this.writeChar(126);
			break;
		case CiToken.EXCLAMATION_MARK:
			this.writeChar(33);
			break;
		case CiToken.NEW:
			const dynamic = expr.type;
			if (dynamic.class.id == CiId.ARRAY_PTR_CLASS)
				this.writeNewArray(dynamic.getElementType(), expr.inner, parent);
			else {
				let init;
				if ((init = expr.inner) instanceof CiAggregateInitializer) {
					let tempId = this.currentTemporaries.indexOf(expr);
					if (tempId >= 0) {
						this.write("citemp");
						this.visitLiteralLong(BigInt(tempId));
					}
					else
						this.writeNewWithFields(dynamic, init);
				}
				else
					this.writeNew(dynamic, parent);
			}
			return;
		case CiToken.RESOURCE:
			const name = expr.inner;
			const array = expr.type;
			this.writeResource(name.value, array.length);
			return;
		default:
			throw new Error();
		}
		expr.inner.accept(this, CiPriority.PRIMARY);
	}

	visitPostfixExpr(expr, parent)
	{
		expr.inner.accept(this, CiPriority.PRIMARY);
		switch (expr.op) {
		case CiToken.INCREMENT:
			this.write("++");
			break;
		case CiToken.DECREMENT:
			this.write("--");
			break;
		default:
			throw new Error();
		}
	}

	startAdd(expr)
	{
		if (!expr.isLiteralZero()) {
			expr.accept(this, CiPriority.ADD);
			this.write(" + ");
		}
	}

	writeAdd(left, right)
	{
		let leftLiteral;
		if ((leftLiteral = left) instanceof CiLiteralLong) {
			let leftValue = leftLiteral.value;
			if (leftValue == 0) {
				right.accept(this, CiPriority.ARGUMENT);
				return;
			}
			let rightLiteral;
			if ((rightLiteral = right) instanceof CiLiteralLong) {
				this.visitLiteralLong(leftValue + rightLiteral.value);
				return;
			}
		}
		else if (right.isLiteralZero()) {
			left.accept(this, CiPriority.ARGUMENT);
			return;
		}
		left.accept(this, CiPriority.ADD);
		this.write(" + ");
		right.accept(this, CiPriority.ADD);
	}

	writeStartEnd(startIndex, length)
	{
		startIndex.accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		this.writeAdd(startIndex, length);
	}

	static #isBitOp(parent)
	{
		switch (parent) {
		case CiPriority.OR:
		case CiPriority.XOR:
		case CiPriority.AND:
		case CiPriority.SHIFT:
			return true;
		default:
			return false;
		}
	}

	writeBinaryOperand(expr, parent, binary)
	{
		expr.accept(this, parent);
	}

	writeBinaryExpr(expr, parentheses, left, op, right)
	{
		if (parentheses)
			this.writeChar(40);
		this.writeBinaryOperand(expr.left, left, expr);
		this.write(op);
		this.writeBinaryOperand(expr.right, right, expr);
		if (parentheses)
			this.writeChar(41);
	}

	writeBinaryExpr2(expr, parent, child, op)
	{
		this.writeBinaryExpr(expr, parent > child, child, op, child);
	}

	static getEqOp(not)
	{
		return not ? " != " : " == ";
	}

	writeEqualOperand(expr, other)
	{
		expr.accept(this, CiPriority.EQUALITY);
	}

	writeEqualExpr(left, right, parent, op)
	{
		if (parent > CiPriority.COND_AND)
			this.writeChar(40);
		this.writeEqualOperand(left, right);
		this.write(op);
		this.writeEqualOperand(right, left);
		if (parent > CiPriority.COND_AND)
			this.writeChar(41);
	}

	writeEqual(left, right, parent, not)
	{
		this.writeEqualExpr(left, right, parent, GenBase.getEqOp(not));
	}

	#writeRel(expr, parent, op)
	{
		this.writeBinaryExpr(expr, parent > CiPriority.COND_AND, CiPriority.REL, op, CiPriority.REL);
	}

	writeAnd(expr, parent)
	{
		this.writeBinaryExpr(expr, parent > CiPriority.COND_AND && parent != CiPriority.AND, CiPriority.AND, " & ", CiPriority.AND);
	}

	writeAssignRight(expr)
	{
		this.writeCoerced(expr.left.type, expr.right, CiPriority.ARGUMENT);
	}

	writeAssign(expr, parent)
	{
		if (parent > CiPriority.ASSIGN)
			this.writeChar(40);
		expr.left.accept(this, CiPriority.ASSIGN);
		this.write(" = ");
		this.writeAssignRight(expr);
		if (parent > CiPriority.ASSIGN)
			this.writeChar(41);
	}

	writeIndexing(collection, index)
	{
		collection.accept(this, CiPriority.PRIMARY);
		this.writeChar(91);
		index.accept(this, CiPriority.ARGUMENT);
		this.writeChar(93);
	}

	writeIndexingExpr(expr, parent)
	{
		this.writeIndexing(expr.left, expr.right);
	}

	getIsOperator()
	{
		return " is ";
	}

	visitBinaryExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.PLUS:
			this.writeBinaryExpr(expr, parent > CiPriority.ADD || GenBase.#isBitOp(parent), CiPriority.ADD, " + ", CiPriority.ADD);
			break;
		case CiToken.MINUS:
			this.writeBinaryExpr(expr, parent > CiPriority.ADD || GenBase.#isBitOp(parent), CiPriority.ADD, " - ", CiPriority.MUL);
			break;
		case CiToken.ASTERISK:
			this.writeBinaryExpr(expr, parent > CiPriority.MUL, CiPriority.MUL, " * ", CiPriority.PRIMARY);
			break;
		case CiToken.SLASH:
			this.writeBinaryExpr(expr, parent > CiPriority.MUL, CiPriority.MUL, " / ", CiPriority.PRIMARY);
			break;
		case CiToken.MOD:
			this.writeBinaryExpr(expr, parent > CiPriority.MUL, CiPriority.MUL, " % ", CiPriority.PRIMARY);
			break;
		case CiToken.SHIFT_LEFT:
			this.writeBinaryExpr(expr, parent > CiPriority.SHIFT, CiPriority.SHIFT, " << ", CiPriority.MUL);
			break;
		case CiToken.SHIFT_RIGHT:
			this.writeBinaryExpr(expr, parent > CiPriority.SHIFT, CiPriority.SHIFT, " >> ", CiPriority.MUL);
			break;
		case CiToken.EQUAL:
			this.writeEqual(expr.left, expr.right, parent, false);
			break;
		case CiToken.NOT_EQUAL:
			this.writeEqual(expr.left, expr.right, parent, true);
			break;
		case CiToken.LESS:
			this.#writeRel(expr, parent, " < ");
			break;
		case CiToken.LESS_OR_EQUAL:
			this.#writeRel(expr, parent, " <= ");
			break;
		case CiToken.GREATER:
			this.#writeRel(expr, parent, " > ");
			break;
		case CiToken.GREATER_OR_EQUAL:
			this.#writeRel(expr, parent, " >= ");
			break;
		case CiToken.AND:
			this.writeAnd(expr, parent);
			break;
		case CiToken.OR:
			this.writeBinaryExpr2(expr, parent, CiPriority.OR, " | ");
			break;
		case CiToken.XOR:
			this.writeBinaryExpr(expr, parent > CiPriority.XOR || parent == CiPriority.OR, CiPriority.XOR, " ^ ", CiPriority.XOR);
			break;
		case CiToken.COND_AND:
			this.writeBinaryExpr(expr, parent > CiPriority.COND_AND || parent == CiPriority.COND_OR, CiPriority.COND_AND, " && ", CiPriority.COND_AND);
			break;
		case CiToken.COND_OR:
			this.writeBinaryExpr2(expr, parent, CiPriority.COND_OR, " || ");
			break;
		case CiToken.ASSIGN:
			this.writeAssign(expr, parent);
			break;
		case CiToken.ADD_ASSIGN:
		case CiToken.SUB_ASSIGN:
		case CiToken.MUL_ASSIGN:
		case CiToken.DIV_ASSIGN:
		case CiToken.MOD_ASSIGN:
		case CiToken.SHIFT_LEFT_ASSIGN:
		case CiToken.SHIFT_RIGHT_ASSIGN:
		case CiToken.AND_ASSIGN:
		case CiToken.OR_ASSIGN:
		case CiToken.XOR_ASSIGN:
			if (parent > CiPriority.ASSIGN)
				this.writeChar(40);
			expr.left.accept(this, CiPriority.ASSIGN);
			this.writeChar(32);
			this.write(expr.getOpString());
			this.writeChar(32);
			expr.right.accept(this, CiPriority.ARGUMENT);
			if (parent > CiPriority.ASSIGN)
				this.writeChar(41);
			break;
		case CiToken.LEFT_BRACKET:
			if (expr.left.type instanceof CiStringType)
				this.writeCharAt(expr);
			else
				this.writeIndexingExpr(expr, parent);
			break;
		case CiToken.IS:
			if (parent > CiPriority.REL)
				this.writeChar(40);
			expr.left.accept(this, CiPriority.REL);
			this.write(this.getIsOperator());
			if (expr.right instanceof CiSymbolReference) {
				const symbol = expr.right;
				this.writeName(symbol.symbol);
			}
			else if (expr.right instanceof CiVar) {
				const def = expr.right;
				this.writeTypeAndName(def);
			}
			else
				throw new Error();
			if (parent > CiPriority.REL)
				this.writeChar(41);
			break;
		case CiToken.WHEN:
			expr.left.accept(this, CiPriority.ARGUMENT);
			this.write(" when ");
			expr.right.accept(this, CiPriority.ARGUMENT);
			break;
		default:
			throw new Error();
		}
	}

	static isReferenceTo(expr, id)
	{
		let symbol;
		return (symbol = expr) instanceof CiSymbolReference && symbol.symbol.id == id;
	}

	writeJavaMatchProperty(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.MATCH_START:
			this.writePostfix(expr.left, ".start()");
			return true;
		case CiId.MATCH_END:
			this.writePostfix(expr.left, ".end()");
			return true;
		case CiId.MATCH_LENGTH:
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			this.writePostfix(expr.left, ".end() - ");
			this.writePostfix(expr.left, ".start()");
			if (parent > CiPriority.ADD)
				this.writeChar(41);
			return true;
		case CiId.MATCH_VALUE:
			this.writePostfix(expr.left, ".group()");
			return true;
		default:
			return false;
		}
	}

	visitSymbolReference(expr, parent)
	{
		if (expr.left == null)
			this.writeLocalName(expr.symbol, parent);
		else if (expr.symbol.id == CiId.STRING_LENGTH)
			this.writeStringLength(expr.left);
		else {
			expr.left.accept(this, CiPriority.PRIMARY);
			this.writeMemberOp(expr.left, expr);
			this.writeName(expr.symbol);
		}
	}

	writeNotPromoted(type, expr)
	{
		expr.accept(this, CiPriority.ARGUMENT);
	}

	writeEnumAsInt(expr, parent)
	{
		expr.accept(this, parent);
	}

	writeEnumHasFlag(obj, args, parent)
	{
		if (parent > CiPriority.EQUALITY)
			this.writeChar(40);
		let i = args[0].intValue();
		if ((i & (i - 1)) == 0 && i != 0) {
			this.writeChar(40);
			this.writeEnumAsInt(obj, CiPriority.AND);
			this.write(" & ");
			this.writeEnumAsInt(args[0], CiPriority.AND);
			this.write(") != 0");
		}
		else {
			this.write("(~");
			this.writeEnumAsInt(obj, CiPriority.PRIMARY);
			this.write(" & ");
			this.writeEnumAsInt(args[0], CiPriority.AND);
			this.write(") == 0");
		}
		if (parent > CiPriority.EQUALITY)
			this.writeChar(41);
	}

	writeTryParseRadix(args)
	{
		this.write(", ");
		if (args.length == 2)
			args[1].accept(this, CiPriority.ARGUMENT);
		else
			this.write("10");
	}

	writeListAdd(obj, method, args)
	{
		obj.accept(this, CiPriority.PRIMARY);
		this.writeChar(46);
		this.write(method);
		this.writeChar(40);
		let elementType = obj.type.asClassType().getElementType();
		if (args.length == 0)
			this.writeNewStorage(elementType);
		else
			this.writeNotPromoted(elementType, args[0]);
		this.writeChar(41);
	}

	writeListInsert(obj, method, args, separator = ", ")
	{
		obj.accept(this, CiPriority.PRIMARY);
		this.writeChar(46);
		this.write(method);
		this.writeChar(40);
		args[0].accept(this, CiPriority.ARGUMENT);
		this.write(separator);
		let elementType = obj.type.asClassType().getElementType();
		if (args.length == 1)
			this.writeNewStorage(elementType);
		else
			this.writeNotPromoted(elementType, args[1]);
		this.writeChar(41);
	}

	writeDictionaryAdd(obj, args)
	{
		this.writeIndexing(obj, args[0]);
		this.write(" = ");
		this.writeNewStorage(obj.type.asClassType().getValueType());
	}

	writeClampAsMinMax(args)
	{
		args[0].accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		args[1].accept(this, CiPriority.ARGUMENT);
		this.write("), ");
		args[2].accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	getRegexOptions(args)
	{
		let expr = args.at(-1);
		if (expr.type instanceof CiEnum)
			return expr.intValue();
		return RegexOptions.NONE;
	}

	writeRegexOptions(args, prefix, separator, suffix, i, m, s)
	{
		let options = this.getRegexOptions(args);
		if (options == RegexOptions.NONE)
			return false;
		this.write(prefix);
		if ((options & RegexOptions.IGNORE_CASE) != 0)
			this.write(i);
		if ((options & RegexOptions.MULTILINE) != 0) {
			if ((options & RegexOptions.IGNORE_CASE) != 0)
				this.write(separator);
			this.write(m);
		}
		if ((options & RegexOptions.SINGLELINE) != 0) {
			if (options != RegexOptions.SINGLELINE)
				this.write(separator);
			this.write(s);
		}
		this.write(suffix);
		return true;
	}

	visitCallExpr(expr, parent)
	{
		const method = expr.method.symbol;
		this.writeCallExpr(expr.method.left, method, expr.arguments, parent);
	}

	visitSelectExpr(expr, parent)
	{
		this.writeCoercedSelect(expr.type, expr, parent);
	}

	ensureChildBlock()
	{
		if (this.#atChildStart) {
			this.atLineStart = false;
			this.#atChildStart = false;
			this.writeChar(32);
			this.openBlock();
			this.#inChildBlock = true;
		}
	}

	static hasTemporaries(expr)
	{
		if (expr instanceof CiAggregateInitializer) {
			const init = expr;
			return init.items.some(item => GenBase.hasTemporaries(item));
		}
		else if (expr instanceof CiLiteral || expr instanceof CiLambdaExpr)
			return false;
		else if (expr instanceof CiInterpolatedString) {
			const interp = expr;
			return interp.parts.some(part => GenBase.hasTemporaries(part.argument));
		}
		else if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			return symbol.left != null && GenBase.hasTemporaries(symbol.left);
		}
		else if (expr instanceof CiUnaryExpr) {
			const unary = expr;
			return unary.inner != null && (GenBase.hasTemporaries(unary.inner) || unary.inner instanceof CiAggregateInitializer);
		}
		else if (expr instanceof CiBinaryExpr) {
			const binary = expr;
			if (GenBase.hasTemporaries(binary.left))
				return true;
			if (binary.op == CiToken.IS)
				return binary.right instanceof CiVar;
			return GenBase.hasTemporaries(binary.right);
		}
		else if (expr instanceof CiSelectExpr) {
			const select = expr;
			return GenBase.hasTemporaries(select.cond) || GenBase.hasTemporaries(select.onTrue) || GenBase.hasTemporaries(select.onFalse);
		}
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			return GenBase.hasTemporaries(call.method) || call.arguments.some(arg => GenBase.hasTemporaries(arg));
		}
		else
			throw new Error();
	}

	defineObjectLiteralTemporary(expr)
	{
		let init;
		if ((init = expr.inner) instanceof CiAggregateInitializer) {
			this.ensureChildBlock();
			let id = this.currentTemporaries.indexOf(expr.type);
			if (id < 0) {
				id = this.currentTemporaries.length;
				this.startTemporaryVar(expr.type);
				this.currentTemporaries.push(expr);
			}
			else
				this.currentTemporaries[id] = expr;
			this.write("citemp");
			this.visitLiteralLong(BigInt(id));
			this.write(" = ");
			const dynamic = expr.type;
			this.writeNew(dynamic, CiPriority.ARGUMENT);
			this.endStatement();
			for (const item of init.items) {
				this.write("citemp");
				this.visitLiteralLong(BigInt(id));
				this.#writeAggregateInitField(expr, item);
			}
		}
	}

	writeTemporaries(expr)
	{
		if (expr instanceof CiVar) {
			const def = expr;
			if (def.value != null) {
				let unary;
				if ((unary = def.value) instanceof CiUnaryExpr && unary.inner instanceof CiAggregateInitializer)
					this.writeTemporaries(unary.inner);
				else
					this.writeTemporaries(def.value);
			}
		}
		else if (expr instanceof CiAggregateInitializer) {
			const init = expr;
			for (const item of init.items) {
				const assign = item;
				this.writeTemporaries(assign.right);
			}
		}
		else if (expr instanceof CiLiteral || expr instanceof CiLambdaExpr) {
		}
		else if (expr instanceof CiInterpolatedString) {
			const interp = expr;
			for (const part of interp.parts)
				this.writeTemporaries(part.argument);
		}
		else if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			if (symbol.left != null)
				this.writeTemporaries(symbol.left);
		}
		else if (expr instanceof CiUnaryExpr) {
			const unary = expr;
			if (unary.inner != null) {
				this.writeTemporaries(unary.inner);
				this.defineObjectLiteralTemporary(unary);
			}
		}
		else if (expr instanceof CiBinaryExpr) {
			const binary = expr;
			this.writeTemporaries(binary.left);
			if (binary.op == CiToken.IS)
				this.defineIsVar(binary);
			else
				this.writeTemporaries(binary.right);
		}
		else if (expr instanceof CiSelectExpr) {
			const select = expr;
			this.writeTemporaries(select.cond);
			this.writeTemporaries(select.onTrue);
			this.writeTemporaries(select.onFalse);
		}
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			this.writeTemporaries(call.method);
			for (const arg of call.arguments)
				this.writeTemporaries(arg);
		}
		else
			throw new Error();
	}

	cleanupTemporary(i, temp)
	{
	}

	cleanupTemporaries()
	{
		for (let i = this.currentTemporaries.length; --i >= 0;) {
			let temp = this.currentTemporaries[i];
			if (!(temp instanceof CiType)) {
				this.cleanupTemporary(i, temp);
				this.currentTemporaries[i] = temp.type;
			}
		}
	}

	visitExpr(statement)
	{
		this.writeTemporaries(statement);
		statement.accept(this, CiPriority.STATEMENT);
		this.writeCharLine(59);
		let def;
		if ((def = statement) instanceof CiVar)
			this.writeInitCode(def);
		this.cleanupTemporaries();
	}

	visitConst(statement)
	{
	}

	visitAssert(statement)
	{
		let binary;
		if ((binary = statement.cond) instanceof CiBinaryExpr && binary.op == CiToken.IS && binary.right instanceof CiVar)
			this.writeAssertCast(binary);
		else
			this.writeAssert(statement);
	}

	writeFirstStatements(statements, count)
	{
		for (let i = 0; i < count; i++)
			statements[i].acceptStatement(this);
	}

	writeStatements(statements)
	{
		this.writeFirstStatements(statements, statements.length);
	}

	cleanupBlock(statement)
	{
	}

	visitBlock(statement)
	{
		if (this.#atChildStart) {
			this.atLineStart = false;
			this.#atChildStart = false;
			this.writeChar(32);
		}
		this.openBlock();
		let temporariesCount = this.currentTemporaries.length;
		this.writeStatements(statement.statements);
		this.cleanupBlock(statement);
		this.currentTemporaries.splice(temporariesCount, this.currentTemporaries.length - temporariesCount);
		this.closeBlock();
	}

	writeChild(statement)
	{
		let wasInChildBlock = this.#inChildBlock;
		this.atLineStart = true;
		this.#atChildStart = true;
		this.#inChildBlock = false;
		statement.acceptStatement(this);
		if (this.#inChildBlock)
			this.closeBlock();
		else if (!(statement instanceof CiBlock))
			this.indent--;
		this.#inChildBlock = wasInChildBlock;
	}

	visitBreak(statement)
	{
		this.writeLine("break;");
	}

	visitContinue(statement)
	{
		this.writeLine("continue;");
	}

	visitDoWhile(statement)
	{
		this.write("do");
		this.writeChild(statement.body);
		this.write("while (");
		statement.cond.accept(this, CiPriority.ARGUMENT);
		this.writeLine(");");
	}

	visitFor(statement)
	{
		if (statement.cond != null)
			this.writeTemporaries(statement.cond);
		this.write("for (");
		if (statement.init != null)
			statement.init.accept(this, CiPriority.STATEMENT);
		this.writeChar(59);
		if (statement.cond != null) {
			this.writeChar(32);
			statement.cond.accept(this, CiPriority.ARGUMENT);
		}
		this.writeChar(59);
		if (statement.advance != null) {
			this.writeChar(32);
			statement.advance.accept(this, CiPriority.STATEMENT);
		}
		this.writeChar(41);
		this.writeChild(statement.body);
	}

	embedIfWhileIsVar(expr, write)
	{
		return false;
	}

	#startIfWhile(expr)
	{
		this.embedIfWhileIsVar(expr, true);
		expr.accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	#writeIf(statement)
	{
		this.write("if (");
		this.#startIfWhile(statement.cond);
		this.writeChild(statement.onTrue);
		if (statement.onFalse != null) {
			this.write("else");
			let elseIf;
			if ((elseIf = statement.onFalse) instanceof CiIf) {
				let wasInChildBlock = this.#inChildBlock;
				this.atLineStart = true;
				this.#atChildStart = true;
				this.#inChildBlock = false;
				if (!this.embedIfWhileIsVar(elseIf.cond, false))
					this.writeTemporaries(elseIf.cond);
				if (this.#inChildBlock) {
					this.#writeIf(elseIf);
					this.closeBlock();
				}
				else {
					this.atLineStart = false;
					this.#atChildStart = false;
					this.writeChar(32);
					this.#writeIf(elseIf);
				}
				this.#inChildBlock = wasInChildBlock;
			}
			else
				this.writeChild(statement.onFalse);
		}
	}

	visitIf(statement)
	{
		if (!this.embedIfWhileIsVar(statement.cond, false))
			this.writeTemporaries(statement.cond);
		this.#writeIf(statement);
	}

	visitNative(statement)
	{
		this.write(statement.content);
	}

	visitReturn(statement)
	{
		if (statement.value == null)
			this.writeLine("return;");
		else {
			this.writeTemporaries(statement.value);
			this.write("return ");
			this.writeStronglyCoerced(this.currentMethod.type, statement.value);
			this.writeCharLine(59);
			this.cleanupTemporaries();
		}
	}

	defineVar(value)
	{
		let def;
		if ((def = value) instanceof CiVar && def.name != "_") {
			this.writeVar(def);
			this.endStatement();
		}
	}

	writeSwitchCaseTypeVar(value)
	{
	}

	writeSwitchValue(expr)
	{
		expr.accept(this, CiPriority.ARGUMENT);
	}

	writeSwitchCaseBody(statements)
	{
		this.writeStatements(statements);
	}

	writeSwitchCase(statement, kase)
	{
		for (const value of kase.values) {
			this.write("case ");
			this.writeCoercedLiteral(statement.value.type, value);
			this.writeCharLine(58);
		}
		this.indent++;
		this.writeSwitchCaseBody(kase.body);
		this.indent--;
	}

	startSwitch(statement)
	{
		this.write("switch (");
		this.writeSwitchValue(statement.value);
		this.writeLine(") {");
		for (const kase of statement.cases)
			this.writeSwitchCase(statement, kase);
	}

	writeSwitchCaseCond(statement, value, parent)
	{
		let when1;
		if ((when1 = value) instanceof CiBinaryExpr && when1.op == CiToken.WHEN) {
			if (parent > CiPriority.SELECT_COND)
				this.writeChar(40);
			this.writeSwitchCaseCond(statement, when1.left, CiPriority.COND_AND);
			this.write(" && ");
			when1.right.accept(this, CiPriority.COND_AND);
			if (parent > CiPriority.SELECT_COND)
				this.writeChar(41);
		}
		else
			this.writeEqual(statement.value, value, parent, false);
	}

	writeIfCaseBody(body, doWhile, statement, kase)
	{
		let length = CiSwitch.lengthWithoutTrailingBreak(body);
		if (doWhile && CiSwitch.hasEarlyBreak(body)) {
			this.indent++;
			this.writeNewLine();
			this.write("do ");
			this.openBlock();
			this.writeFirstStatements(body, length);
			this.closeBlock();
			this.writeLine("while (0);");
			this.indent--;
		}
		else if (length != 1 || body[0] instanceof CiIf || body[0] instanceof CiSwitch) {
			this.writeChar(32);
			this.openBlock();
			this.writeFirstStatements(body, length);
			this.closeBlock();
		}
		else
			this.writeChild(body[0]);
	}

	writeSwitchAsIfs(statement, doWhile)
	{
		for (const kase of statement.cases) {
			for (const value of kase.values) {
				let when1;
				if ((when1 = value) instanceof CiBinaryExpr && when1.op == CiToken.WHEN) {
					this.defineVar(when1.left);
					this.writeTemporaries(when1);
				}
				else
					this.writeSwitchCaseTypeVar(value);
			}
		}
		let op = "if (";
		for (const kase of statement.cases) {
			let parent = kase.values.length == 1 ? CiPriority.ARGUMENT : CiPriority.COND_OR;
			for (const value of kase.values) {
				this.write(op);
				this.writeSwitchCaseCond(statement, value, parent);
				op = " || ";
			}
			this.writeChar(41);
			this.writeIfCaseBody(kase.body, doWhile, statement, kase);
			op = "else if (";
		}
		if (statement.hasDefault()) {
			this.write("else");
			this.writeIfCaseBody(statement.defaultBody, doWhile, statement, null);
		}
	}

	visitSwitch(statement)
	{
		this.writeTemporaries(statement.value);
		this.startSwitch(statement);
		if (statement.defaultBody.length > 0) {
			this.writeLine("default:");
			this.indent++;
			this.writeSwitchCaseBody(statement.defaultBody);
			this.indent--;
		}
		this.writeCharLine(125);
	}

	visitWhile(statement)
	{
		if (!this.embedIfWhileIsVar(statement.cond, false))
			this.writeTemporaries(statement.cond);
		this.write("while (");
		this.#startIfWhile(statement.cond);
		this.writeChild(statement.body);
	}

	flattenBlock(statement)
	{
		let block;
		if ((block = statement) instanceof CiBlock)
			this.writeStatements(block.statements);
		else
			statement.acceptStatement(this);
	}

	hasInitCode(def)
	{
		return GenBase.#getAggregateInitializer(def) != null;
	}

	needsConstructor(klass)
	{
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let field;
			if ((field = symbol) instanceof CiField && this.hasInitCode(field))
				return true;
		}
		return klass.constructor_ != null;
	}

	writeInitField(field)
	{
		this.writeInitCode(field);
	}

	writeConstructorBody(klass)
	{
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let field;
			if ((field = symbol) instanceof CiField)
				this.writeInitField(field);
		}
		if (klass.constructor_ != null) {
			this.currentMethod = klass.constructor_;
			const block = klass.constructor_.body;
			this.writeStatements(block.statements);
			this.currentMethod = null;
		}
		this.currentTemporaries.length = 0;
	}

	writeParameter(param)
	{
		this.writeTypeAndName(param);
	}

	writeRemainingParameters(method, first, defaultArguments)
	{
		for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
			if (!first)
				this.write(", ");
			first = false;
			this.writeParameter(param);
			if (defaultArguments)
				this.writeVarInit(param);
		}
		this.writeChar(41);
	}

	writeParameters(method, defaultArguments)
	{
		this.writeChar(40);
		this.writeRemainingParameters(method, true, defaultArguments);
	}

	writeBody(method)
	{
		if (method.callType == CiCallType.ABSTRACT)
			this.writeCharLine(59);
		else {
			this.writeNewLine();
			this.currentMethod = method;
			this.openBlock();
			this.flattenBlock(method.body);
			this.closeBlock();
			this.currentMethod = null;
		}
	}

	writePublic(container)
	{
		if (container.isPublic)
			this.write("public ");
	}

	writeEnumValue(konst)
	{
		this.writeDoc(konst.documentation);
		this.writeName(konst);
		if (!(konst.value instanceof CiImplicitEnumValue)) {
			this.write(" = ");
			konst.value.accept(this, CiPriority.ARGUMENT);
		}
	}

	visitEnumValue(konst, previous)
	{
		if (previous != null)
			this.writeCharLine(44);
		this.writeEnumValue(konst);
	}

	writeRegexOptionsEnum(program)
	{
		if (program.regexOptionsEnum)
			this.writeEnum(program.system.regexOptionsEnum);
	}

	startClass(klass, suffix, extendsClause)
	{
		this.write("class ");
		this.write(klass.name);
		this.write(suffix);
		if (klass.hasBaseClass()) {
			this.write(extendsClause);
			this.write(klass.baseClassName);
		}
	}

	openClass(klass, suffix, extendsClause)
	{
		this.startClass(klass, suffix, extendsClause);
		this.writeNewLine();
		this.openBlock();
	}

	writeMembers(klass, constArrays)
	{
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			if (symbol instanceof CiConst) {
				const konst = symbol;
				this.writeConst(konst);
			}
			else if (symbol instanceof CiField) {
				const field = symbol;
				this.writeField(field);
			}
			else if (symbol instanceof CiMethod) {
				const method = symbol;
				this.writeMethod(method);
				this.currentTemporaries.length = 0;
			}
			else if (symbol instanceof CiVar) {
			}
			else
				throw new Error();
		}
		if (constArrays) {
			for (const konst of klass.constArrays)
				this.writeConst(konst);
		}
	}

	writeBaseClass(klass, program)
	{
		if (this.writtenClasses.has(klass))
			return false;
		this.writtenClasses.add(klass);
		let baseClass;
		if ((baseClass = klass.parent) instanceof CiClass)
			this.writeClass(baseClass, program);
		return true;
	}

	writeTypes(program)
	{
		this.writeRegexOptionsEnum(program);
		for (let type = program.first; type != null; type = type.next) {
			if (type instanceof CiClass) {
				const klass = type;
				this.writeClass(klass, program);
			}
			else if (type instanceof CiEnum) {
				const enu = type;
				this.writeEnum(enu);
			}
			else
				throw new Error();
		}
	}
}

export class GenTyped extends GenBase
{

	writeCoercedLiteral(type, expr)
	{
		expr.accept(this, CiPriority.ARGUMENT);
		if (type != null && type.id == CiId.FLOAT_TYPE && expr instanceof CiLiteralDouble)
			this.writeChar(102);
	}

	writeTypeAndName(value)
	{
		this.writeType(value.type, true);
		this.writeChar(32);
		this.writeName(value);
	}

	visitAggregateInitializer(expr)
	{
		this.write("{ ");
		this.writeCoercedLiterals(expr.type.asClassType().getElementType(), expr.items);
		this.write(" }");
	}

	writeArrayStorageLength(expr)
	{
		const array = expr.type;
		this.visitLiteralLong(BigInt(array.length));
	}

	writeNewArray(elementType, lengthExpr, parent)
	{
		this.write("new ");
		this.writeType(elementType.getBaseType(), false);
		this.writeChar(91);
		lengthExpr.accept(this, CiPriority.ARGUMENT);
		this.writeChar(93);
		while (elementType.isArray()) {
			this.writeChar(91);
			let arrayStorage;
			if ((arrayStorage = elementType) instanceof CiArrayStorageType)
				arrayStorage.lengthExpr.accept(this, CiPriority.ARGUMENT);
			this.writeChar(93);
			elementType = elementType.asClassType().getElementType();
		}
	}

	getOneAscii(expr)
	{
		let literal;
		return (literal = expr) instanceof CiLiteralString ? literal.getOneAscii() : -1;
	}

	writeCharMethodCall(obj, method, arg)
	{
		obj.accept(this, CiPriority.PRIMARY);
		this.writeChar(46);
		this.write(method);
		this.writeChar(40);
		if (!(arg instanceof CiLiteralChar))
			this.write("(char) ");
		arg.accept(this, CiPriority.PRIMARY);
		this.writeChar(41);
	}

	static isNarrower(left, right)
	{
		switch (left) {
		case CiId.S_BYTE_RANGE:
			switch (right) {
			case CiId.BYTE_RANGE:
			case CiId.SHORT_RANGE:
			case CiId.U_SHORT_RANGE:
			case CiId.INT_TYPE:
			case CiId.LONG_TYPE:
				return true;
			default:
				return false;
			}
		case CiId.BYTE_RANGE:
			switch (right) {
			case CiId.S_BYTE_RANGE:
			case CiId.SHORT_RANGE:
			case CiId.U_SHORT_RANGE:
			case CiId.INT_TYPE:
			case CiId.LONG_TYPE:
				return true;
			default:
				return false;
			}
		case CiId.SHORT_RANGE:
			switch (right) {
			case CiId.U_SHORT_RANGE:
			case CiId.INT_TYPE:
			case CiId.LONG_TYPE:
				return true;
			default:
				return false;
			}
		case CiId.U_SHORT_RANGE:
			switch (right) {
			case CiId.SHORT_RANGE:
			case CiId.INT_TYPE:
			case CiId.LONG_TYPE:
				return true;
			default:
				return false;
			}
		case CiId.INT_TYPE:
			return right == CiId.LONG_TYPE;
		default:
			return false;
		}
	}

	getStaticCastInner(type, expr)
	{
		let binary;
		let rightMask;
		if ((binary = expr) instanceof CiBinaryExpr && binary.op == CiToken.AND && (rightMask = binary.right) instanceof CiLiteralLong && type instanceof CiIntegerType) {
			let mask;
			switch (type.id) {
			case CiId.BYTE_RANGE:
			case CiId.S_BYTE_RANGE:
				mask = 255n;
				break;
			case CiId.SHORT_RANGE:
			case CiId.U_SHORT_RANGE:
				mask = 65535n;
				break;
			case CiId.INT_TYPE:
				mask = 4294967295n;
				break;
			default:
				return expr;
			}
			if ((rightMask.value & mask) == mask)
				return binary.left;
		}
		return expr;
	}

	writeStaticCastType(type)
	{
		this.writeChar(40);
		this.writeType(type, false);
		this.write(") ");
	}

	writeStaticCast(type, expr)
	{
		this.writeStaticCastType(type);
		this.getStaticCastInner(type, expr).accept(this, CiPriority.PRIMARY);
	}

	writeNotPromoted(type, expr)
	{
		if (type instanceof CiIntegerType && GenTyped.isNarrower(type.id, this.getTypeId(expr.type, true)))
			this.writeStaticCast(type, expr);
		else
			this.writeCoercedLiteral(type, expr);
	}

	isPromoted(expr)
	{
		let binary;
		return !((binary = expr) instanceof CiBinaryExpr && (binary.op == CiToken.LEFT_BRACKET || binary.isAssign()));
	}

	writeAssignRight(expr)
	{
		if (expr.left.isIndexing()) {
			if (expr.right instanceof CiLiteralLong) {
				this.writeCoercedLiteral(expr.left.type, expr.right);
				return;
			}
			let leftTypeId = expr.left.type.id;
			let rightTypeId = this.getTypeId(expr.right.type, this.isPromoted(expr.right));
			if (leftTypeId == CiId.S_BYTE_RANGE && rightTypeId == CiId.S_BYTE_RANGE) {
				expr.right.accept(this, CiPriority.ASSIGN);
				return;
			}
			if (GenTyped.isNarrower(leftTypeId, rightTypeId)) {
				this.writeStaticCast(expr.left.type, expr.right);
				return;
			}
		}
		super.writeAssignRight(expr);
	}

	writeCoercedInternal(type, expr, parent)
	{
		if (type instanceof CiIntegerType && type.id != CiId.LONG_TYPE && expr.type.id == CiId.LONG_TYPE)
			this.writeStaticCast(type, expr);
		else if (type.id == CiId.FLOAT_TYPE && expr.type.id == CiId.DOUBLE_TYPE) {
			let literal;
			if ((literal = expr) instanceof CiLiteralDouble) {
				this.visitLiteralDouble(literal.value);
				this.writeChar(102);
			}
			else
				this.writeStaticCast(type, expr);
		}
		else if (type instanceof CiIntegerType && expr.type.id == CiId.FLOAT_INT_TYPE) {
			let call;
			if ((call = expr) instanceof CiCallExpr && call.method.symbol.id == CiId.MATH_TRUNCATE) {
				expr = call.arguments[0];
				let literal;
				if ((literal = expr) instanceof CiLiteralDouble) {
					this.visitLiteralLong(BigInt(Math.trunc(literal.value)));
					return;
				}
			}
			this.writeStaticCast(type, expr);
		}
		else
			super.writeCoercedInternal(type, expr, parent);
	}

	writeCharAt(expr)
	{
		this.writeIndexing(expr.left, expr.right);
	}

	startTemporaryVar(type)
	{
		this.writeType(type, true);
		this.writeChar(32);
	}

	writeAssertCast(expr)
	{
		const def = expr.right;
		this.writeTypeAndName(def);
		this.write(" = ");
		this.writeStaticCast(def.type, expr.left);
		this.writeCharLine(59);
	}
}

export class GenCCppD extends GenTyped
{
	switchesWithGoto = [];

	visitLiteralLong(i)
	{
		if (i == -9223372036854775808)
			this.write("(-9223372036854775807 - 1)");
		else
			super.visitLiteralLong(i);
	}

	static #isPtrTo(ptr, other)
	{
		let klass;
		return (klass = ptr.type) instanceof CiClassType && klass.class.id != CiId.STRING_CLASS && klass.isAssignableFrom(other.type);
	}

	writeEqual(left, right, parent, not)
	{
		let coercedType;
		if (GenCCppD.#isPtrTo(left, right))
			coercedType = left.type;
		else if (GenCCppD.#isPtrTo(right, left))
			coercedType = right.type;
		else {
			super.writeEqual(left, right, parent, not);
			return;
		}
		if (parent > CiPriority.EQUALITY)
			this.writeChar(40);
		this.writeCoerced(coercedType, left, CiPriority.EQUALITY);
		this.write(GenCCppD.getEqOp(not));
		this.writeCoerced(coercedType, right, CiPriority.EQUALITY);
		if (parent > CiPriority.EQUALITY)
			this.writeChar(41);
	}

	visitConst(statement)
	{
		if (statement.type instanceof CiArrayStorageType)
			this.writeConst(statement);
	}

	visitBreak(statement)
	{
		let switchStatement;
		if ((switchStatement = statement.loopOrSwitch) instanceof CiSwitch) {
			let gotoId = this.switchesWithGoto.indexOf(switchStatement);
			if (gotoId >= 0) {
				this.write("goto ciafterswitch");
				this.visitLiteralLong(BigInt(gotoId));
				this.writeCharLine(59);
				return;
			}
		}
		super.visitBreak(statement);
	}

	writeSwitchAsIfsWithGoto(statement)
	{
		if (statement.cases.some(kase => CiSwitch.hasEarlyBreakAndContinue(kase.body)) || CiSwitch.hasEarlyBreakAndContinue(statement.defaultBody)) {
			let gotoId = this.switchesWithGoto.length;
			this.switchesWithGoto.push(statement);
			this.writeSwitchAsIfs(statement, false);
			this.write("ciafterswitch");
			this.visitLiteralLong(BigInt(gotoId));
			this.writeLine(": ;");
		}
		else
			this.writeSwitchAsIfs(statement, true);
	}
}

export class GenCCpp extends GenCCppD
{

	#writeCIncludes()
	{
		this.writeIncludes("#include <", ">");
	}

	getLiteralChars()
	{
		return 127;
	}

	writeNumericType(id)
	{
		switch (id) {
		case CiId.S_BYTE_RANGE:
			this.includeStdInt();
			this.write("int8_t");
			break;
		case CiId.BYTE_RANGE:
			this.includeStdInt();
			this.write("uint8_t");
			break;
		case CiId.SHORT_RANGE:
			this.includeStdInt();
			this.write("int16_t");
			break;
		case CiId.U_SHORT_RANGE:
			this.includeStdInt();
			this.write("uint16_t");
			break;
		case CiId.INT_TYPE:
			this.write("int");
			break;
		case CiId.LONG_TYPE:
			this.includeStdInt();
			this.write("int64_t");
			break;
		case CiId.FLOAT_TYPE:
			this.write("float");
			break;
		case CiId.DOUBLE_TYPE:
			this.write("double");
			break;
		default:
			throw new Error();
		}
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.MATH_NA_N:
			this.includeMath();
			this.write("NAN");
			break;
		case CiId.MATH_NEGATIVE_INFINITY:
			this.includeMath();
			this.write("-INFINITY");
			break;
		case CiId.MATH_POSITIVE_INFINITY:
			this.includeMath();
			this.write("INFINITY");
			break;
		default:
			super.visitSymbolReference(expr, parent);
			break;
		}
	}

	static isStringEmpty(expr)
	{
		let symbol;
		if ((symbol = expr.left) instanceof CiSymbolReference && symbol.symbol.id == CiId.STRING_LENGTH && expr.right.isLiteralZero())
			return symbol.left;
		return null;
	}

	writeArrayPtrAdd(array, index)
	{
		if (index.isLiteralZero())
			this.writeArrayPtr(array, CiPriority.ARGUMENT);
		else {
			this.writeArrayPtr(array, CiPriority.ADD);
			this.write(" + ");
			index.accept(this, CiPriority.MUL);
		}
	}

	static isStringSubstring(expr)
	{
		let call;
		if ((call = expr) instanceof CiCallExpr) {
			let id = call.method.symbol.id;
			if ((id == CiId.STRING_SUBSTRING && call.arguments.length == 2) || id == CiId.U_T_F8_GET_STRING)
				return call;
		}
		return null;
	}

	static isUTF8GetString(call)
	{
		return call.method.symbol.id == CiId.U_T_F8_GET_STRING;
	}

	static getStringSubstringPtr(call)
	{
		return GenCCpp.isUTF8GetString(call) ? call.arguments[0] : call.method.left;
	}

	static getStringSubstringOffset(call)
	{
		return call.arguments[GenCCpp.isUTF8GetString(call) ? 1 : 0];
	}

	static getStringSubstringLength(call)
	{
		return call.arguments[GenCCpp.isUTF8GetString(call) ? 2 : 1];
	}

	writeStringPtrAdd(call)
	{
		this.writeArrayPtrAdd(GenCCpp.getStringSubstringPtr(call), GenCCpp.getStringSubstringOffset(call));
	}

	static isTrimSubstring(expr)
	{
		let call = GenCCpp.isStringSubstring(expr.right);
		let leftSymbol;
		if (call != null && !GenCCpp.isUTF8GetString(call) && (leftSymbol = expr.left) instanceof CiSymbolReference && GenCCpp.getStringSubstringPtr(call).isReferenceTo(leftSymbol.symbol) && GenCCpp.getStringSubstringOffset(call).isLiteralZero())
			return GenCCpp.getStringSubstringLength(call);
		return null;
	}

	writeStringLiteralWithNewLine(s)
	{
		this.writeChar(34);
		this.write(s);
		this.write("\\n\"");
	}

	writeUnreachable(statement)
	{
		this.write("abort();");
		if (statement.message != null) {
			this.write(" // ");
			statement.message.accept(this, CiPriority.ARGUMENT);
		}
		this.writeNewLine();
	}

	writeAssert(statement)
	{
		if (statement.completesNormally()) {
			this.writeTemporaries(statement.cond);
			this.includeAssert();
			this.write("assert(");
			if (statement.message == null)
				statement.cond.accept(this, CiPriority.ARGUMENT);
			else {
				statement.cond.accept(this, CiPriority.COND_AND);
				this.write(" && ");
				statement.message.accept(this, CiPriority.ARGUMENT);
			}
			this.writeLine(");");
		}
		else
			this.writeUnreachable(statement);
	}

	visitSwitch(statement)
	{
		if (statement.value.type instanceof CiStringType || statement.hasWhen())
			this.writeSwitchAsIfsWithGoto(statement);
		else
			super.visitSwitch(statement);
	}

	writeMethods(klass)
	{
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let method;
			if ((method = symbol) instanceof CiMethod) {
				this.writeMethod(method);
				this.currentTemporaries.length = 0;
			}
		}
	}

	writeClass(klass, program)
	{
		if (!this.writeBaseClass(klass, program))
			return;
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let field;
			let storage;
			if ((field = symbol) instanceof CiField && (storage = field.type.getBaseType()) instanceof CiStorageType && storage.class.id == CiId.NONE)
				this.writeClass(storage.class, program);
		}
		this.writeClassInternal(klass);
	}

	static #changeExtension(path, ext)
	{
		let extIndex = path.length;
		for (let i = extIndex; --i >= 0 && path.charCodeAt(i) != 47 && path.charCodeAt(i) != 92;) {
			if (path.charCodeAt(i) == 46) {
				extIndex = i;
				break;
			}
		}
		return path.substring(0, extIndex) + ext;
	}

	createHeaderFile(headerExt)
	{
		this.createFile(null, GenCCpp.#changeExtension(this.outputFile, headerExt));
		this.writeLine("#pragma once");
		this.#writeCIncludes();
	}

	static #getFilenameWithoutExtension(path)
	{
		let pathLength = path.length;
		let extIndex = pathLength;
		let i = pathLength;
		while (--i >= 0 && path.charCodeAt(i) != 47 && path.charCodeAt(i) != 92) {
			if (path.charCodeAt(i) == 46 && extIndex == pathLength)
				extIndex = i;
		}
		i++;
		return path.substring(i, i + extIndex - i);
	}

	createImplementationFile(program, headerExt)
	{
		this.createOutputFile();
		this.writeTopLevelNatives(program);
		this.#writeCIncludes();
		this.write("#include \"");
		this.write(GenCCpp.#getFilenameWithoutExtension(this.outputFile));
		this.write(headerExt);
		this.writeCharLine(34);
	}
}

export class GenC extends GenCCpp
{
	#intTryParse;
	#longTryParse;
	#doubleTryParse;
	#stringAssign;
	#stringSubstring;
	#stringAppend;
	#stringIndexOf;
	#stringLastIndexOf;
	#stringEndsWith;
	#stringReplace;
	#stringFormat;
	#matchFind;
	#matchPos;
	#ptrConstruct;
	#sharedMake;
	#sharedAddRef;
	#sharedRelease;
	#sharedAssign;
	#listFrees = {};
	#treeCompareInteger;
	#treeCompareString;
	#compares = new Set();
	#contains = new Set();
	#varsToDestruct = [];
	currentClass;

	getCurrentContainer()
	{
		return this.currentClass;
	}

	getTargetName()
	{
		return "C";
	}

	writeSelfDoc(method)
	{
		if (method.callType == CiCallType.STATIC)
			return;
		this.write(" * @param self This <code>");
		this.writeName(method.parent);
		this.writeLine("</code>.");
	}

	includeStdInt()
	{
		this.include("stdint.h");
	}

	includeAssert()
	{
		this.include("assert.h");
	}

	includeMath()
	{
		this.include("math.h");
	}

	includeStdBool()
	{
		this.include("stdbool.h");
	}

	visitLiteralNull()
	{
		this.write("NULL");
	}

	writePrintfLongPrefix()
	{
		this.write("ll");
	}

	writePrintfWidth(part)
	{
		super.writePrintfWidth(part);
		if (GenC.isStringSubstring(part.argument) != null) {
			console.assert(part.precision < 0);
			this.write(".*");
		}
		if (part.argument.type.id == CiId.LONG_TYPE)
			this.writePrintfLongPrefix();
	}

	writeInterpolatedStringArgBase(expr)
	{
		if (expr.type.id == CiId.LONG_TYPE) {
			this.write("(long long) ");
			expr.accept(this, CiPriority.PRIMARY);
		}
		else
			expr.accept(this, CiPriority.ARGUMENT);
	}

	#writeStringPtrAddCast(call)
	{
		if (GenC.isUTF8GetString(call))
			this.write("(const char *) ");
		this.writeStringPtrAdd(call);
	}

	static #isDictionaryClassStgIndexing(expr)
	{
		let indexing;
		let dict;
		return (indexing = expr) instanceof CiBinaryExpr && indexing.op == CiToken.LEFT_BRACKET && (dict = indexing.left.type) instanceof CiClassType && dict.class.typeParameterCount == 2 && dict.getValueType() instanceof CiStorageType;
	}

	#writeTemporaryOrExpr(expr, parent)
	{
		let tempId = this.currentTemporaries.indexOf(expr);
		if (tempId >= 0) {
			this.write("citemp");
			this.visitLiteralLong(BigInt(tempId));
		}
		else
			expr.accept(this, parent);
	}

	#writeUpcast(resultClass, klass)
	{
		for (; klass != resultClass; klass = klass.parent)
			this.write(".base");
	}

	#writeClassPtr(resultClass, expr, parent)
	{
		let storage;
		let ptr;
		if ((storage = expr.type) instanceof CiStorageType && storage.class.id == CiId.NONE && !GenC.#isDictionaryClassStgIndexing(expr)) {
			this.writeChar(38);
			this.#writeTemporaryOrExpr(expr, CiPriority.PRIMARY);
			this.#writeUpcast(resultClass, storage.class);
		}
		else if ((ptr = expr.type) instanceof CiClassType && ptr.class != resultClass) {
			this.writeChar(38);
			this.writePostfix(expr, "->base");
			this.#writeUpcast(resultClass, ptr.class.parent);
		}
		else
			expr.accept(this, parent);
	}

	writeInterpolatedStringArg(expr)
	{
		let call = GenC.isStringSubstring(expr);
		if (call != null) {
			GenC.getStringSubstringLength(call).accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeStringPtrAddCast(call);
		}
		else {
			let klass;
			if ((klass = expr.type) instanceof CiClassType && klass.class.id != CiId.STRING_CLASS) {
				this.write(this.namespace);
				this.write(klass.class.name);
				this.write("_ToString(");
				this.#writeClassPtr(klass.class, expr, CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			else
				this.writeInterpolatedStringArgBase(expr);
		}
	}

	visitInterpolatedString(expr, parent)
	{
		this.include("stdarg.h");
		this.include("stdio.h");
		this.#stringFormat = true;
		this.write("CiString_Format(");
		this.writePrintf(expr, false);
	}

	writeCamelCaseNotKeyword(name)
	{
		switch (name) {
		case "this":
			this.write("self");
			break;
		case "Asm":
		case "Assert":
		case "Auto":
		case "Bool":
		case "Break":
		case "Byte":
		case "Case":
		case "Char":
		case "Class":
		case "Const":
		case "Continue":
		case "Default":
		case "Do":
		case "Double":
		case "Else":
		case "Enum":
		case "Extern":
		case "False":
		case "Float":
		case "For":
		case "Foreach":
		case "Goto":
		case "If":
		case "Inline":
		case "Int":
		case "Long":
		case "Register":
		case "Restrict":
		case "Return":
		case "Short":
		case "Signed":
		case "Sizeof":
		case "Static":
		case "Struct":
		case "Switch":
		case "True":
		case "Typedef":
		case "Typeof":
		case "Union":
		case "Unsigned":
		case "Void":
		case "Volatile":
		case "While":
		case "asm":
		case "auto":
		case "char":
		case "extern":
		case "goto":
		case "inline":
		case "register":
		case "restrict":
		case "signed":
		case "sizeof":
		case "struct":
		case "typedef":
		case "typeof":
		case "union":
		case "unsigned":
		case "volatile":
			this.writeCamelCase(name);
			this.writeChar(95);
			break;
		default:
			this.writeCamelCase(name);
			break;
		}
	}

	writeName(symbol)
	{
		if (symbol instanceof CiContainerType) {
			this.write(this.namespace);
			this.write(symbol.name);
		}
		else if (symbol instanceof CiMethod) {
			this.write(this.namespace);
			this.write(symbol.parent.name);
			this.writeChar(95);
			this.write(symbol.name);
		}
		else if (symbol instanceof CiConst) {
			if (symbol.parent instanceof CiContainerType) {
				this.write(this.namespace);
				this.write(symbol.parent.name);
				this.writeChar(95);
			}
			this.writeUppercaseWithUnderscores(symbol.name);
		}
		else
			this.writeCamelCaseNotKeyword(symbol.name);
	}

	#writeSelfForField(fieldClass)
	{
		this.write("self->");
		for (let klass = this.currentClass; klass != fieldClass; klass = klass.parent)
			this.write("base.");
	}

	writeLocalName(symbol, parent)
	{
		let forEach;
		if ((forEach = symbol.parent) instanceof CiForeach) {
			const klass = forEach.collection.type;
			switch (klass.class.id) {
			case CiId.STRING_CLASS:
			case CiId.LIST_CLASS:
				if (parent == CiPriority.PRIMARY)
					this.writeChar(40);
				this.writeChar(42);
				this.writeCamelCaseNotKeyword(symbol.name);
				if (parent == CiPriority.PRIMARY)
					this.writeChar(41);
				return;
			case CiId.ARRAY_STORAGE_CLASS:
				if (klass.getElementType() instanceof CiStorageType) {
					if (parent > CiPriority.ADD)
						this.writeChar(40);
					forEach.collection.accept(this, CiPriority.ADD);
					this.write(" + ");
					this.writeCamelCaseNotKeyword(symbol.name);
					if (parent > CiPriority.ADD)
						this.writeChar(41);
				}
				else {
					forEach.collection.accept(this, CiPriority.PRIMARY);
					this.writeChar(91);
					this.writeCamelCaseNotKeyword(symbol.name);
					this.writeChar(93);
				}
				return;
			default:
				break;
			}
		}
		if (symbol instanceof CiField)
			this.#writeSelfForField(symbol.parent);
		this.writeName(symbol);
	}

	#writeMatchProperty(expr, which)
	{
		this.#matchPos = true;
		this.write("CiMatch_GetPos(");
		expr.left.accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		this.visitLiteralLong(BigInt(which));
		this.writeChar(41);
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.CONSOLE_ERROR:
			this.include("stdio.h");
			this.write("stderr");
			break;
		case CiId.LIST_COUNT:
		case CiId.STACK_COUNT:
			this.writePostfix(expr.left, "->len");
			break;
		case CiId.QUEUE_COUNT:
			expr.left.accept(this, CiPriority.PRIMARY);
			if (expr.left.type instanceof CiStorageType)
				this.writeChar(46);
			else
				this.write("->");
			this.write("length");
			break;
		case CiId.HASH_SET_COUNT:
		case CiId.DICTIONARY_COUNT:
			this.writeCall("g_hash_table_size", expr.left);
			break;
		case CiId.SORTED_SET_COUNT:
		case CiId.SORTED_DICTIONARY_COUNT:
			this.writeCall("g_tree_nnodes", expr.left);
			break;
		case CiId.MATCH_START:
			this.#writeMatchProperty(expr, 0);
			break;
		case CiId.MATCH_END:
			this.#writeMatchProperty(expr, 1);
			break;
		case CiId.MATCH_LENGTH:
			this.#writeMatchProperty(expr, 2);
			break;
		case CiId.MATCH_VALUE:
			this.write("g_match_info_fetch(");
			expr.left.accept(this, CiPriority.ARGUMENT);
			this.write(", 0)");
			break;
		default:
			if (expr.left == null || expr.symbol instanceof CiConst)
				this.writeLocalName(expr.symbol, parent);
			else if (GenC.#isDictionaryClassStgIndexing(expr.left)) {
				this.writePostfix(expr.left, "->");
				this.writeName(expr.symbol);
			}
			else
				super.visitSymbolReference(expr, parent);
			break;
		}
	}

	#writeGlib(s)
	{
		this.include("glib.h");
		this.write(s);
	}

	writeStringPtrType()
	{
		this.write("const char *");
	}

	writeClassType(klass, space)
	{
		switch (klass.class.id) {
		case CiId.NONE:
			if (!(klass instanceof CiReadWriteClassType))
				this.write("const ");
			this.writeName(klass.class);
			if (!(klass instanceof CiStorageType))
				this.write(" *");
			else if (space)
				this.writeChar(32);
			break;
		case CiId.STRING_CLASS:
			if (klass.id == CiId.STRING_STORAGE_TYPE)
				this.write("char *");
			else
				this.writeStringPtrType();
			break;
		case CiId.LIST_CLASS:
		case CiId.STACK_CLASS:
			this.#writeGlib("GArray *");
			break;
		case CiId.QUEUE_CLASS:
			this.#writeGlib("GQueue ");
			if (!(klass instanceof CiStorageType))
				this.writeChar(42);
			break;
		case CiId.HASH_SET_CLASS:
		case CiId.DICTIONARY_CLASS:
			this.#writeGlib("GHashTable *");
			break;
		case CiId.SORTED_SET_CLASS:
		case CiId.SORTED_DICTIONARY_CLASS:
			this.#writeGlib("GTree *");
			break;
		case CiId.TEXT_WRITER_CLASS:
			this.include("stdio.h");
			this.write("FILE *");
			break;
		case CiId.REGEX_CLASS:
			if (!(klass instanceof CiReadWriteClassType))
				this.write("const ");
			this.#writeGlib("GRegex *");
			break;
		case CiId.MATCH_CLASS:
			if (!(klass instanceof CiReadWriteClassType))
				this.write("const ");
			this.#writeGlib("GMatchInfo *");
			break;
		case CiId.LOCK_CLASS:
			this.notYet(klass, "Lock");
			this.include("threads.h");
			this.write("mtx_t ");
			break;
		default:
			this.notSupported(klass, klass.class.name);
			break;
		}
	}

	#writeArrayPrefix(type)
	{
		let array;
		if ((array = type) instanceof CiClassType && array.isArray()) {
			this.#writeArrayPrefix(array.getElementType());
			if (!(type instanceof CiArrayStorageType)) {
				if (array.getElementType() instanceof CiArrayStorageType)
					this.writeChar(40);
				if (!(type instanceof CiReadWriteClassType))
					this.write("const ");
				this.writeChar(42);
			}
		}
	}

	#startDefinition(type, promote, space)
	{
		let baseType = type.getBaseType();
		if (baseType instanceof CiIntegerType) {
			this.writeNumericType(this.getTypeId(baseType, promote && type == baseType));
			if (space)
				this.writeChar(32);
		}
		else if (baseType instanceof CiEnum) {
			if (baseType.id == CiId.BOOL_TYPE) {
				this.includeStdBool();
				this.write("bool");
			}
			else
				this.writeName(baseType);
			if (space)
				this.writeChar(32);
		}
		else if (baseType instanceof CiClassType) {
			const klass = baseType;
			this.writeClassType(klass, space);
		}
		else {
			this.write(baseType.name);
			if (space)
				this.writeChar(32);
		}
		this.#writeArrayPrefix(type);
	}

	#endDefinition(type)
	{
		while (type.isArray()) {
			let elementType = type.asClassType().getElementType();
			let arrayStorage;
			if ((arrayStorage = type) instanceof CiArrayStorageType) {
				this.writeChar(91);
				this.visitLiteralLong(BigInt(arrayStorage.length));
				this.writeChar(93);
			}
			else if (elementType instanceof CiArrayStorageType)
				this.writeChar(41);
			type = elementType;
		}
	}

	#writeReturnType(method)
	{
		if (method.type.id == CiId.VOID_TYPE && method.throws) {
			this.includeStdBool();
			this.write("bool ");
		}
		else
			this.#startDefinition(method.type, true, true);
	}

	writeType(type, promote)
	{
		let arrayPtr;
		this.#startDefinition(type, promote, (arrayPtr = type) instanceof CiClassType && arrayPtr.class.id == CiId.ARRAY_PTR_CLASS);
		this.#endDefinition(type);
	}

	writeTypeAndName(value)
	{
		this.#startDefinition(value.type, true, true);
		this.writeName(value);
		this.#endDefinition(value.type);
	}

	#writeDynamicArrayCast(elementType)
	{
		this.writeChar(40);
		this.#startDefinition(elementType, false, true);
		this.write(elementType.isArray() ? "(*)" : "*");
		this.#endDefinition(elementType);
		this.write(") ");
	}

	#writeXstructorPtr(need, klass, name)
	{
		if (need) {
			this.write("(CiMethodPtr) ");
			this.writeName(klass);
			this.writeChar(95);
			this.write(name);
		}
		else
			this.write("NULL");
	}

	static #isHeapAllocated(type)
	{
		return type.id == CiId.STRING_STORAGE_TYPE || type instanceof CiDynamicPtrType;
	}

	static #needToDestructType(type)
	{
		if (GenC.#isHeapAllocated(type))
			return true;
		let storage;
		if ((storage = type) instanceof CiStorageType) {
			switch (storage.class.id) {
			case CiId.LIST_CLASS:
			case CiId.STACK_CLASS:
			case CiId.HASH_SET_CLASS:
			case CiId.SORTED_SET_CLASS:
			case CiId.DICTIONARY_CLASS:
			case CiId.SORTED_DICTIONARY_CLASS:
			case CiId.MATCH_CLASS:
			case CiId.LOCK_CLASS:
				return true;
			default:
				return GenC.#needsDestructor(storage.class);
			}
		}
		return false;
	}

	static #needToDestruct(symbol)
	{
		let type = symbol.type;
		let array;
		while ((array = type) instanceof CiArrayStorageType)
			type = array.getElementType();
		return GenC.#needToDestructType(type);
	}

	static #needsDestructor(klass)
	{
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let field;
			if ((field = symbol) instanceof CiField && GenC.#needToDestruct(field))
				return true;
		}
		let baseClass;
		return (baseClass = klass.parent) instanceof CiClass && GenC.#needsDestructor(baseClass);
	}

	#writeXstructorPtrs(klass)
	{
		this.#writeXstructorPtr(this.needsConstructor(klass), klass, "Construct");
		this.write(", ");
		this.#writeXstructorPtr(GenC.#needsDestructor(klass), klass, "Destruct");
	}

	writeNewArray(elementType, lengthExpr, parent)
	{
		this.#sharedMake = true;
		if (parent > CiPriority.MUL)
			this.writeChar(40);
		this.#writeDynamicArrayCast(elementType);
		this.write("CiShared_Make(");
		lengthExpr.accept(this, CiPriority.ARGUMENT);
		this.write(", sizeof(");
		this.writeType(elementType, false);
		this.write("), ");
		if (elementType instanceof CiStringStorageType) {
			this.#ptrConstruct = true;
			this.#listFrees["String"] = "free(*(void **) ptr)";
			this.write("(CiMethodPtr) CiPtr_Construct, CiList_FreeString");
		}
		else if (elementType instanceof CiStorageType) {
			const storage = elementType;
			this.#writeXstructorPtrs(storage.class);
		}
		else if (elementType instanceof CiDynamicPtrType) {
			this.#ptrConstruct = true;
			this.#sharedRelease = true;
			this.#listFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
			this.write("(CiMethodPtr) CiPtr_Construct, CiList_FreeShared");
		}
		else
			this.write("NULL, NULL");
		this.writeChar(41);
		if (parent > CiPriority.MUL)
			this.writeChar(41);
	}

	static #isNewString(expr)
	{
		let call;
		return expr instanceof CiInterpolatedString || ((call = expr) instanceof CiCallExpr && expr.type.id == CiId.STRING_STORAGE_TYPE && (call.method.symbol.id != CiId.STRING_SUBSTRING || call.arguments.length == 2));
	}

	#writeStringStorageValue(expr)
	{
		let call = GenC.isStringSubstring(expr);
		if (call != null) {
			this.include("string.h");
			this.#stringSubstring = true;
			this.write("CiString_Substring(");
			this.#writeStringPtrAddCast(call);
			this.write(", ");
			GenC.getStringSubstringLength(call).accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
		}
		else if (GenC.#isNewString(expr))
			expr.accept(this, CiPriority.ARGUMENT);
		else {
			this.include("string.h");
			this.writeCall("strdup", expr);
		}
	}

	writeArrayStorageInit(array, value)
	{
		let literal;
		if (value == null) {
			if (GenC.#isHeapAllocated(array.getStorageType()))
				this.write(" = { NULL }");
		}
		else if ((literal = value) instanceof CiLiteral && literal.isDefaultValue()) {
			this.write(" = { ");
			literal.accept(this, CiPriority.ARGUMENT);
			this.write(" }");
		}
		else
			throw new Error();
	}

	#getDictionaryDestroy(type)
	{
		if (type instanceof CiStringStorageType || type instanceof CiArrayStorageType)
			return "free";
		else if (type instanceof CiStorageType) {
			const storage = type;
			switch (storage.class.id) {
			case CiId.LIST_CLASS:
			case CiId.STACK_CLASS:
				return "(GDestroyNotify) g_array_unref";
			case CiId.HASH_SET_CLASS:
			case CiId.DICTIONARY_CLASS:
				return "(GDestroyNotify) g_hash_table_unref";
			case CiId.SORTED_SET_CLASS:
			case CiId.SORTED_DICTIONARY_CLASS:
				return "(GDestroyNotify) g_tree_unref";
			default:
				return GenC.#needsDestructor(storage.class) ? `(GDestroyNotify) ${storage.class.name}_Delete` : "free";
			}
		}
		else if (type instanceof CiDynamicPtrType) {
			this.#sharedRelease = true;
			return "CiShared_Release";
		}
		else
			return "NULL";
	}

	#writeHashEqual(keyType)
	{
		this.write(keyType instanceof CiStringType ? "g_str_hash, g_str_equal" : "NULL, NULL");
	}

	#writeNewHashTable(keyType, valueDestroy)
	{
		this.write("g_hash_table_new");
		let keyDestroy = this.#getDictionaryDestroy(keyType);
		if (keyDestroy == "NULL" && valueDestroy == "NULL") {
			this.writeChar(40);
			this.#writeHashEqual(keyType);
		}
		else {
			this.write("_full(");
			this.#writeHashEqual(keyType);
			this.write(", ");
			this.write(keyDestroy);
			this.write(", ");
			this.write(valueDestroy);
		}
		this.writeChar(41);
	}

	#writeNewTree(keyType, valueDestroy)
	{
		if (keyType.id == CiId.STRING_PTR_TYPE && valueDestroy == "NULL")
			this.write("g_tree_new((GCompareFunc) strcmp");
		else {
			this.write("g_tree_new_full(CiTree_Compare");
			if (keyType instanceof CiIntegerType) {
				this.#treeCompareInteger = true;
				this.write("Integer");
			}
			else if (keyType instanceof CiStringType) {
				this.#treeCompareString = true;
				this.write("String");
			}
			else
				this.notSupported(keyType, keyType.toString());
			this.write(", NULL, ");
			this.write(this.#getDictionaryDestroy(keyType));
			this.write(", ");
			this.write(valueDestroy);
		}
		this.writeChar(41);
	}

	writeNew(klass, parent)
	{
		switch (klass.class.id) {
		case CiId.LIST_CLASS:
		case CiId.STACK_CLASS:
			this.write("g_array_new(FALSE, FALSE, sizeof(");
			this.writeType(klass.getElementType(), false);
			this.write("))");
			break;
		case CiId.QUEUE_CLASS:
			this.write("G_QUEUE_INIT");
			break;
		case CiId.HASH_SET_CLASS:
			this.#writeNewHashTable(klass.getElementType(), "NULL");
			break;
		case CiId.SORTED_SET_CLASS:
			this.#writeNewTree(klass.getElementType(), "NULL");
			break;
		case CiId.DICTIONARY_CLASS:
			this.#writeNewHashTable(klass.getKeyType(), this.#getDictionaryDestroy(klass.getValueType()));
			break;
		case CiId.SORTED_DICTIONARY_CLASS:
			this.#writeNewTree(klass.getKeyType(), this.#getDictionaryDestroy(klass.getValueType()));
			break;
		default:
			this.#sharedMake = true;
			if (parent > CiPriority.MUL)
				this.writeChar(40);
			this.writeStaticCastType(klass);
			this.write("CiShared_Make(1, sizeof(");
			this.writeName(klass.class);
			this.write("), ");
			this.#writeXstructorPtrs(klass.class);
			this.writeChar(41);
			if (parent > CiPriority.MUL)
				this.writeChar(41);
			break;
		}
	}

	writeStorageInit(def)
	{
		if (def.type.asClassType().class.typeParameterCount > 0)
			super.writeStorageInit(def);
	}

	writeVarInit(def)
	{
		if (def.value == null && GenC.#isHeapAllocated(def.type))
			this.write(" = NULL");
		else
			super.writeVarInit(def);
	}

	#writeAssignTemporary(type, expr)
	{
		this.write(" = ");
		if (expr != null)
			this.writeCoerced(type, expr, CiPriority.ARGUMENT);
		else
			this.writeNewStorage(type);
	}

	#writeCTemporary(type, expr)
	{
		this.ensureChildBlock();
		let klass;
		let assign = expr != null || ((klass = type) instanceof CiClassType && (klass.class.id == CiId.LIST_CLASS || klass.class.id == CiId.DICTIONARY_CLASS || klass.class.id == CiId.SORTED_DICTIONARY_CLASS));
		let id = this.currentTemporaries.indexOf(type);
		if (id < 0) {
			id = this.currentTemporaries.length;
			this.#startDefinition(type, false, true);
			this.write("citemp");
			this.visitLiteralLong(BigInt(id));
			this.#endDefinition(type);
			if (assign)
				this.#writeAssignTemporary(type, expr);
			this.writeCharLine(59);
			this.currentTemporaries.push(expr);
		}
		else if (assign) {
			this.write("citemp");
			this.visitLiteralLong(BigInt(id));
			this.#writeAssignTemporary(type, expr);
			this.writeCharLine(59);
			this.currentTemporaries[id] = expr;
		}
		return id;
	}

	#writeStorageTemporary(expr)
	{
		if (GenC.#isNewString(expr) || (expr instanceof CiCallExpr && expr.type instanceof CiStorageType))
			this.#writeCTemporary(expr.type, expr);
	}

	#writeCTemporaries(expr)
	{
		if (expr instanceof CiVar) {
			const def = expr;
			if (def.value != null)
				this.#writeCTemporaries(def.value);
		}
		else if (expr instanceof CiAggregateInitializer) {
			const init = expr;
			for (const item of init.items) {
				const assign = item;
				this.#writeCTemporaries(assign.right);
			}
		}
		else if (expr instanceof CiLiteral || expr instanceof CiLambdaExpr) {
		}
		else if (expr instanceof CiInterpolatedString) {
			const interp = expr;
			for (const part of interp.parts)
				this.#writeCTemporaries(part.argument);
		}
		else if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			if (symbol.left != null)
				this.#writeCTemporaries(symbol.left);
		}
		else if (expr instanceof CiUnaryExpr) {
			const unary = expr;
			if (unary.inner != null)
				this.#writeCTemporaries(unary.inner);
		}
		else if (expr instanceof CiBinaryExpr) {
			const binary = expr;
			this.#writeCTemporaries(binary.left);
			if (GenC.isStringSubstring(binary.left) == null)
				this.#writeStorageTemporary(binary.left);
			this.#writeCTemporaries(binary.right);
			if (binary.op != CiToken.ASSIGN)
				this.#writeStorageTemporary(binary.right);
		}
		else if (expr instanceof CiSelectExpr) {
			const select = expr;
			this.#writeCTemporaries(select.cond);
		}
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			if (call.method.left != null) {
				this.#writeCTemporaries(call.method.left);
				this.#writeStorageTemporary(call.method.left);
			}
			const method = call.method.symbol;
			let param = method.parameters.firstParameter();
			for (const arg of call.arguments) {
				this.#writeCTemporaries(arg);
				if (call.method.symbol.id != CiId.CONSOLE_WRITE && call.method.symbol.id != CiId.CONSOLE_WRITE_LINE && !(param.type instanceof CiStorageType))
					this.#writeStorageTemporary(arg);
				param = param.nextParameter();
			}
		}
		else
			throw new Error();
	}

	static #hasTemporariesToDestruct(expr)
	{
		if (expr instanceof CiAggregateInitializer) {
			const init = expr;
			return init.items.some(field => GenC.#hasTemporariesToDestruct(field));
		}
		else if (expr instanceof CiLiteral || expr instanceof CiLambdaExpr)
			return false;
		else if (expr instanceof CiInterpolatedString) {
			const interp = expr;
			return interp.parts.some(part => GenC.#hasTemporariesToDestruct(part.argument));
		}
		else if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			return symbol.left != null && GenC.#hasTemporariesToDestruct(symbol.left);
		}
		else if (expr instanceof CiUnaryExpr) {
			const unary = expr;
			return unary.inner != null && GenC.#hasTemporariesToDestruct(unary.inner);
		}
		else if (expr instanceof CiBinaryExpr) {
			const binary = expr;
			return GenC.#hasTemporariesToDestruct(binary.left) || (binary.op != CiToken.IS && GenC.#hasTemporariesToDestruct(binary.right));
		}
		else if (expr instanceof CiSelectExpr) {
			const select = expr;
			return GenC.#hasTemporariesToDestruct(select.cond);
		}
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			return (call.method.left != null && (GenC.#hasTemporariesToDestruct(call.method.left) || GenC.#isNewString(call.method.left))) || call.arguments.some(arg => GenC.#hasTemporariesToDestruct(arg) || GenC.#isNewString(arg));
		}
		else
			throw new Error();
	}

	cleanupTemporary(i, temp)
	{
		if (temp.type.id == CiId.STRING_STORAGE_TYPE) {
			this.write("free(citemp");
			this.visitLiteralLong(BigInt(i));
			this.writeLine(");");
		}
	}

	writeVar(def)
	{
		super.writeVar(def);
		if (GenC.#needToDestruct(def)) {
			const local = def;
			this.#varsToDestruct.push(local);
		}
	}

	#writeGPointerCast(type, expr)
	{
		if (type instanceof CiNumericType || type instanceof CiEnum) {
			this.write("GINT_TO_POINTER(");
			expr.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
		}
		else if (type.id == CiId.STRING_PTR_TYPE && expr.type.id == CiId.STRING_PTR_TYPE) {
			this.write("(gpointer) ");
			expr.accept(this, CiPriority.PRIMARY);
		}
		else
			this.writeCoerced(type, expr, CiPriority.ARGUMENT);
	}

	#writeGConstPointerCast(expr)
	{
		if (expr.type instanceof CiClassType && !(expr.type instanceof CiStorageType))
			expr.accept(this, CiPriority.ARGUMENT);
		else {
			this.write("(gconstpointer) ");
			expr.accept(this, CiPriority.PRIMARY);
		}
	}

	#writeQueueObject(obj)
	{
		if (obj.type instanceof CiStorageType) {
			this.writeChar(38);
			obj.accept(this, CiPriority.PRIMARY);
		}
		else
			obj.accept(this, CiPriority.ARGUMENT);
	}

	#writeQueueGet(function_, obj, parent)
	{
		let elementType = obj.type.asClassType().getElementType();
		let parenthesis;
		if (parent == CiPriority.STATEMENT)
			parenthesis = false;
		else if (elementType instanceof CiIntegerType && elementType.id != CiId.LONG_TYPE) {
			this.write("GPOINTER_TO_INT(");
			parenthesis = true;
		}
		else {
			parenthesis = parent > CiPriority.MUL;
			if (parenthesis)
				this.writeChar(40);
			this.writeStaticCastType(elementType);
		}
		this.write(function_);
		this.writeChar(40);
		this.#writeQueueObject(obj);
		this.writeChar(41);
		if (parenthesis)
			this.writeChar(41);
	}

	#startDictionaryInsert(dict, key)
	{
		const type = dict.type;
		this.write(type.class.id == CiId.SORTED_DICTIONARY_CLASS ? "g_tree_insert(" : "g_hash_table_insert(");
		dict.accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		this.#writeGPointerCast(type.getKeyType(), key);
		this.write(", ");
	}

	writeAssign(expr, parent)
	{
		let indexing;
		let dict;
		if ((indexing = expr.left) instanceof CiBinaryExpr && indexing.op == CiToken.LEFT_BRACKET && (dict = indexing.left.type) instanceof CiClassType && dict.class.typeParameterCount == 2) {
			this.#startDictionaryInsert(indexing.left, indexing.right);
			this.#writeGPointerCast(dict.getValueType(), expr.right);
			this.writeChar(41);
		}
		else if (expr.left.type.id == CiId.STRING_STORAGE_TYPE) {
			let length = GenC.isTrimSubstring(expr);
			if (length != null && parent == CiPriority.STATEMENT) {
				this.writeIndexing(expr.left, length);
				this.write(" = '\\0'");
			}
			else {
				this.#stringAssign = true;
				this.write("CiString_Assign(&");
				expr.left.accept(this, CiPriority.PRIMARY);
				this.write(", ");
				this.#writeStringStorageValue(expr.right);
				this.writeChar(41);
			}
		}
		else {
			let dynamic;
			if ((dynamic = expr.left.type) instanceof CiDynamicPtrType) {
				if (dynamic.class.id == CiId.REGEX_CLASS) {
					super.writeAssign(expr, parent);
				}
				else {
					this.#sharedAssign = true;
					this.write("CiShared_Assign((void **) &");
					expr.left.accept(this, CiPriority.PRIMARY);
					this.write(", ");
					if (expr.right instanceof CiSymbolReference) {
						this.#sharedAddRef = true;
						this.write("CiShared_AddRef(");
						expr.right.accept(this, CiPriority.ARGUMENT);
						this.writeChar(41);
					}
					else
						expr.right.accept(this, CiPriority.ARGUMENT);
					this.writeChar(41);
				}
			}
			else
				super.writeAssign(expr, parent);
		}
	}

	static #getThrowingMethod(expr)
	{
		let binary;
		if ((binary = expr) instanceof CiBinaryExpr && binary.op == CiToken.ASSIGN)
			return GenC.#getThrowingMethod(binary.right);
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			const method = call.method.symbol;
			return method.throws ? method : null;
		}
		else
			return null;
	}

	static #hasListDestroy(type)
	{
		let list;
		return (list = type) instanceof CiStorageType && (list.class.id == CiId.LIST_CLASS || list.class.id == CiId.STACK_CLASS) && GenC.#needToDestructType(list.getElementType());
	}

	hasInitCode(def)
	{
		if (def.isAssignableStorage())
			return false;
		let klass;
		let storage;
		return (def instanceof CiField && (def.value != null || GenC.#isHeapAllocated(def.type.getStorageType()) || ((klass = def.type) instanceof CiClassType && (klass.class.id == CiId.LIST_CLASS || klass.class.id == CiId.DICTIONARY_CLASS || klass.class.id == CiId.SORTED_DICTIONARY_CLASS)))) || GenC.#getThrowingMethod(def.value) != null || ((storage = def.type.getStorageType()) instanceof CiStorageType && (storage.class.id == CiId.LOCK_CLASS || this.needsConstructor(storage.class))) || GenC.#hasListDestroy(def.type) || super.hasInitCode(def);
	}

	#startForwardThrow(throwingMethod)
	{
		this.write("if (");
		switch (throwingMethod.type.id) {
		case CiId.FLOAT_TYPE:
		case CiId.DOUBLE_TYPE:
			this.includeMath();
			this.write("isnan(");
			return CiPriority.ARGUMENT;
		case CiId.VOID_TYPE:
			this.writeChar(33);
			return CiPriority.PRIMARY;
		default:
			return CiPriority.EQUALITY;
		}
	}

	#writeDestruct(symbol)
	{
		if (!GenC.#needToDestruct(symbol))
			return;
		this.ensureChildBlock();
		let type = symbol.type;
		let nesting = 0;
		let array;
		while ((array = type) instanceof CiArrayStorageType) {
			this.write("for (int _i");
			this.visitLiteralLong(BigInt(nesting));
			this.write(" = ");
			this.visitLiteralLong(BigInt(array.length - 1));
			this.write("; _i");
			this.visitLiteralLong(BigInt(nesting));
			this.write(" >= 0; _i");
			this.visitLiteralLong(BigInt(nesting));
			this.writeLine("--)");
			this.indent++;
			nesting++;
			type = array.getElementType();
		}
		let arrayFree = false;
		if (type instanceof CiDynamicPtrType) {
			const dynamic = type;
			if (dynamic.class.id == CiId.REGEX_CLASS)
				this.write("g_regex_unref(");
			else {
				this.#sharedRelease = true;
				this.write("CiShared_Release(");
			}
		}
		else if (type instanceof CiStorageType) {
			const storage = type;
			switch (storage.class.id) {
			case CiId.LIST_CLASS:
			case CiId.STACK_CLASS:
				this.write("g_array_free(");
				arrayFree = true;
				break;
			case CiId.QUEUE_CLASS:
				this.write("g_queue_clear(&");
				break;
			case CiId.HASH_SET_CLASS:
			case CiId.DICTIONARY_CLASS:
				this.write("g_hash_table_unref(");
				break;
			case CiId.SORTED_SET_CLASS:
			case CiId.SORTED_DICTIONARY_CLASS:
				this.write("g_tree_unref(");
				break;
			case CiId.MATCH_CLASS:
				this.write("g_match_info_free(");
				break;
			case CiId.LOCK_CLASS:
				this.write("mtx_destroy(&");
				break;
			default:
				this.writeName(storage.class);
				this.write("_Destruct(&");
				break;
			}
		}
		else
			this.write("free(");
		this.writeLocalName(symbol, CiPriority.PRIMARY);
		for (let i = 0; i < nesting; i++) {
			this.write("[_i");
			this.visitLiteralLong(BigInt(i));
			this.writeChar(93);
		}
		if (arrayFree)
			this.write(", TRUE");
		this.writeLine(");");
		this.indent -= nesting;
	}

	#writeDestructAll(exceptVar = null)
	{
		for (let i = this.#varsToDestruct.length; --i >= 0;) {
			let def = this.#varsToDestruct[i];
			if (def != exceptVar)
				this.#writeDestruct(def);
		}
	}

	#writeThrowReturnValue()
	{
		if (this.currentMethod.type instanceof CiNumericType) {
			if (this.currentMethod.type instanceof CiIntegerType)
				this.write("-1");
			else {
				this.includeMath();
				this.write("NAN");
			}
		}
		else if (this.currentMethod.type.id == CiId.VOID_TYPE)
			this.write("false");
		else
			this.write("NULL");
	}

	#writeThrow()
	{
		this.#writeDestructAll();
		this.write("return ");
		this.#writeThrowReturnValue();
		this.writeCharLine(59);
	}

	#endForwardThrow(throwingMethod)
	{
		switch (throwingMethod.type.id) {
		case CiId.FLOAT_TYPE:
		case CiId.DOUBLE_TYPE:
			this.writeChar(41);
			break;
		case CiId.VOID_TYPE:
			break;
		default:
			this.write(throwingMethod.type instanceof CiIntegerType ? " == -1" : " == NULL");
			break;
		}
		this.writeChar(41);
		if (this.#varsToDestruct.length > 0) {
			this.writeChar(32);
			this.openBlock();
			this.#writeThrow();
			this.closeBlock();
		}
		else {
			this.writeNewLine();
			this.indent++;
			this.#writeThrow();
			this.indent--;
		}
	}

	writeInitCode(def)
	{
		if (!this.hasInitCode(def))
			return;
		let type = def.type;
		let nesting = 0;
		let array;
		while ((array = type) instanceof CiArrayStorageType) {
			this.openLoop("int", nesting++, array.length);
			type = array.getElementType();
		}
		let lok;
		if ((lok = type) instanceof CiStorageType && lok.class.id == CiId.LOCK_CLASS) {
			this.write("mtx_init(&");
			this.writeArrayElement(def, nesting);
			this.writeLine(", mtx_plain | mtx_recursive);");
		}
		else {
			let storage;
			if ((storage = type) instanceof CiStorageType && this.needsConstructor(storage.class)) {
				this.writeName(storage.class);
				this.write("_Construct(&");
				this.writeArrayElement(def, nesting);
				this.writeLine(");");
			}
			else {
				if (def instanceof CiField) {
					this.writeArrayElement(def, nesting);
					if (nesting > 0) {
						this.write(" = ");
						if (GenC.#isHeapAllocated(type))
							this.write("NULL");
						else
							def.value.accept(this, CiPriority.ARGUMENT);
					}
					else
						this.writeVarInit(def);
					this.writeCharLine(59);
				}
				let throwingMethod = GenC.#getThrowingMethod(def.value);
				if (throwingMethod != null) {
					this.#startForwardThrow(throwingMethod);
					this.writeArrayElement(def, nesting);
					this.#endForwardThrow(throwingMethod);
				}
			}
		}
		if (GenC.#hasListDestroy(type)) {
			this.write("g_array_set_clear_func(");
			this.writeArrayElement(def, nesting);
			this.write(", ");
			if (type.asClassType().getElementType() instanceof CiStringStorageType) {
				this.#listFrees["String"] = "free(*(void **) ptr)";
				this.write("CiList_FreeString");
			}
			else if (type.asClassType().getElementType() instanceof CiDynamicPtrType) {
				this.#sharedRelease = true;
				this.#listFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
				this.write("CiList_FreeShared");
			}
			else if (type.asClassType().getElementType() instanceof CiStorageType) {
				const storage = type.asClassType().getElementType();
				switch (storage.class.id) {
				case CiId.LIST_CLASS:
				case CiId.STACK_CLASS:
					this.#listFrees["List"] = "g_array_free(*(GArray **) ptr, TRUE)";
					this.write("CiList_FreeList");
					break;
				case CiId.HASH_SET_CLASS:
				case CiId.DICTIONARY_CLASS:
					this.#listFrees["HashTable"] = "g_hash_table_unref(*(GHashTable **) ptr)";
					this.write("CiList_FreeHashTable");
					break;
				case CiId.SORTED_SET_CLASS:
				case CiId.SORTED_DICTIONARY_CLASS:
					this.#listFrees["Tree"] = "g_tree_unref(*(GTree **) ptr)";
					this.write("CiList_FreeTree");
					break;
				default:
					this.write("(GDestroyNotify) ");
					this.writeName(storage.class);
					this.write("_Destruct");
					break;
				}
			}
			else
				throw new Error();
			this.writeLine(");");
		}
		while (--nesting >= 0)
			this.closeBlock();
		super.writeInitCode(def);
	}

	#writeMemberAccess(left, symbolClass)
	{
		if (left.type instanceof CiStorageType)
			this.writeChar(46);
		else
			this.write("->");
		for (let klass = left.type.asClassType().class; klass != symbolClass; klass = klass.parent)
			this.write("base.");
	}

	writeMemberOp(left, symbol)
	{
		this.#writeMemberAccess(left, symbol.symbol.parent);
	}

	writeArrayPtr(expr, parent)
	{
		let list;
		if ((list = expr.type) instanceof CiClassType && list.class.id == CiId.LIST_CLASS) {
			this.writeChar(40);
			this.writeType(list.getElementType(), false);
			this.write(" *) ");
			this.writePostfix(expr, "->data");
		}
		else
			expr.accept(this, parent);
	}

	writeCoercedInternal(type, expr, parent)
	{
		let dynamic;
		let klass;
		if ((dynamic = type) instanceof CiDynamicPtrType && expr instanceof CiSymbolReference && parent != CiPriority.EQUALITY) {
			this.#sharedAddRef = true;
			if (dynamic.class.id == CiId.ARRAY_PTR_CLASS)
				this.#writeDynamicArrayCast(dynamic.getElementType());
			else {
				this.writeChar(40);
				this.writeName(dynamic.class);
				this.write(" *) ");
			}
			this.writeCall("CiShared_AddRef", expr);
		}
		else if ((klass = type) instanceof CiClassType && klass.class.id != CiId.STRING_CLASS && klass.class.id != CiId.ARRAY_PTR_CLASS && !(klass instanceof CiStorageType)) {
			if (klass.class.id == CiId.QUEUE_CLASS && expr.type instanceof CiStorageType) {
				this.writeChar(38);
				expr.accept(this, CiPriority.PRIMARY);
			}
			else
				this.#writeClassPtr(klass.class, expr, parent);
		}
		else {
			if (type.id == CiId.STRING_STORAGE_TYPE)
				this.#writeStringStorageValue(expr);
			else if (expr.type.id == CiId.STRING_STORAGE_TYPE)
				this.#writeTemporaryOrExpr(expr, parent);
			else
				super.writeCoercedInternal(type, expr, parent);
		}
	}

	writeSubstringEqual(call, literal, parent, not)
	{
		if (parent > CiPriority.EQUALITY)
			this.writeChar(40);
		this.include("string.h");
		this.write("memcmp(");
		this.writeStringPtrAdd(call);
		this.write(", ");
		this.visitLiteralString(literal);
		this.write(", ");
		this.visitLiteralLong(BigInt(literal.length));
		this.writeChar(41);
		this.write(GenC.getEqOp(not));
		this.writeChar(48);
		if (parent > CiPriority.EQUALITY)
			this.writeChar(41);
	}

	writeEqualStringInternal(left, right, parent, not)
	{
		if (parent > CiPriority.EQUALITY)
			this.writeChar(40);
		this.include("string.h");
		this.write("strcmp(");
		this.#writeTemporaryOrExpr(left, CiPriority.ARGUMENT);
		this.write(", ");
		this.#writeTemporaryOrExpr(right, CiPriority.ARGUMENT);
		this.writeChar(41);
		this.write(GenC.getEqOp(not));
		this.writeChar(48);
		if (parent > CiPriority.EQUALITY)
			this.writeChar(41);
	}

	writeEqual(left, right, parent, not)
	{
		if (left.type instanceof CiStringType && right.type instanceof CiStringType) {
			let call = GenC.isStringSubstring(left);
			let literal;
			if (call != null && (literal = right) instanceof CiLiteralString) {
				let lengthExpr = GenC.getStringSubstringLength(call);
				let rightLength = literal.getAsciiLength();
				if (rightLength >= 0) {
					let rightValue = literal.value;
					let leftLength;
					if ((leftLength = lengthExpr) instanceof CiLiteralLong) {
						if (leftLength.value != rightLength)
							this.notYet(left, "String comparison with unmatched length");
						this.writeSubstringEqual(call, rightValue, parent, not);
					}
					else if (not) {
						if (parent > CiPriority.COND_OR)
							this.writeChar(40);
						lengthExpr.accept(this, CiPriority.EQUALITY);
						this.write(" != ");
						this.visitLiteralLong(BigInt(rightLength));
						if (rightLength > 0) {
							this.write(" || ");
							this.writeSubstringEqual(call, rightValue, CiPriority.COND_OR, true);
						}
						if (parent > CiPriority.COND_OR)
							this.writeChar(41);
					}
					else {
						if (parent > CiPriority.COND_AND || parent == CiPriority.COND_OR)
							this.writeChar(40);
						lengthExpr.accept(this, CiPriority.EQUALITY);
						this.write(" == ");
						this.visitLiteralLong(BigInt(rightLength));
						if (rightLength > 0) {
							this.write(" && ");
							this.writeSubstringEqual(call, rightValue, CiPriority.COND_AND, false);
						}
						if (parent > CiPriority.COND_AND || parent == CiPriority.COND_OR)
							this.writeChar(41);
					}
					return;
				}
			}
			this.writeEqualStringInternal(left, right, parent, not);
		}
		else
			super.writeEqual(left, right, parent, not);
	}

	writeStringLength(expr)
	{
		this.include("string.h");
		this.writeCall("(int) strlen", expr);
	}

	#writeStringMethod(name, obj, args)
	{
		this.include("string.h");
		this.write("CiString_");
		this.writeCall(name, obj, args[0]);
	}

	#writeSizeofCompare(elementType)
	{
		this.write(", sizeof(");
		let typeId = elementType.id;
		this.writeNumericType(typeId);
		this.write("), CiCompare_");
		this.writeNumericType(typeId);
		this.writeChar(41);
		this.#compares.add(typeId);
	}

	writeArrayFill(obj, args)
	{
		this.write("for (int _i = 0; _i < ");
		if (args.length == 1)
			this.writeArrayStorageLength(obj);
		else
			args[2].accept(this, CiPriority.REL);
		this.writeLine("; _i++)");
		this.writeChar(9);
		obj.accept(this, CiPriority.PRIMARY);
		this.writeChar(91);
		if (args.length > 1)
			this.startAdd(args[1]);
		this.write("_i] = ");
		args[0].accept(this, CiPriority.ARGUMENT);
	}

	#writeListAddInsert(obj, insert, function_, args)
	{
		let elementType = obj.type.asClassType().getElementType();
		let id = this.#writeCTemporary(elementType, elementType.isFinal() ? null : args.at(-1));
		let storage;
		if ((storage = elementType) instanceof CiStorageType && this.needsConstructor(storage.class)) {
			this.writeName(storage.class);
			this.write("_Construct(&citemp");
			this.visitLiteralLong(BigInt(id));
			this.writeLine(");");
		}
		this.write(function_);
		this.writeChar(40);
		obj.accept(this, CiPriority.ARGUMENT);
		if (insert) {
			this.write(", ");
			args[0].accept(this, CiPriority.ARGUMENT);
		}
		this.write(", citemp");
		this.visitLiteralLong(BigInt(id));
		this.writeChar(41);
		this.currentTemporaries[id] = elementType;
	}

	#writeDictionaryLookup(obj, function_, key)
	{
		this.write(function_);
		this.writeChar(40);
		obj.accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		this.#writeGConstPointerCast(key);
		this.writeChar(41);
	}

	#writeArgsAndRightParenthesis(method, args)
	{
		let i = 0;
		for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
			if (i > 0 || method.callType != CiCallType.STATIC)
				this.write(", ");
			if (i >= args.length)
				param.value.accept(this, CiPriority.ARGUMENT);
			else
				this.writeCoerced(param.type, args[i], CiPriority.ARGUMENT);
			i++;
		}
		this.writeChar(41);
	}

	#writeCRegexOptions(args)
	{
		if (!this.writeRegexOptions(args, "", " | ", "", "G_REGEX_CASELESS", "G_REGEX_MULTILINE", "G_REGEX_DOTALL"))
			this.writeChar(48);
	}

	writePrintfNotInterpolated(args, newLine)
	{
		this.write("\"%");
		if (args[0].type instanceof CiIntegerType) {
			const intType = args[0].type;
			if (intType.id == CiId.LONG_TYPE)
				this.writePrintfLongPrefix();
			this.writeChar(100);
		}
		else if (args[0].type instanceof CiFloatingType)
			this.writeChar(103);
		else
			this.writeChar(115);
		if (newLine)
			this.write("\\n");
		this.write("\", ");
		this.writeInterpolatedStringArgBase(args[0]);
		this.writeChar(41);
	}

	#writeTextWriterWrite(obj, args, newLine)
	{
		if (args.length == 0) {
			this.write("putc('\\n', ");
			obj.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
		}
		else {
			let interpolated;
			if ((interpolated = args[0]) instanceof CiInterpolatedString) {
				this.write("fprintf(");
				obj.accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				this.writePrintf(interpolated, newLine);
			}
			else if (args[0].type instanceof CiNumericType) {
				this.write("fprintf(");
				obj.accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				this.writePrintfNotInterpolated(args, newLine);
			}
			else if (!newLine) {
				this.write("fputs(");
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				obj.accept(this, CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			else {
				let literal;
				if ((literal = args[0]) instanceof CiLiteralString) {
					this.write("fputs(");
					this.writeStringLiteralWithNewLine(literal.value);
					this.write(", ");
					obj.accept(this, CiPriority.ARGUMENT);
					this.writeChar(41);
				}
				else {
					this.write("fprintf(");
					obj.accept(this, CiPriority.ARGUMENT);
					this.write(", \"%s\\n\", ");
					args[0].accept(this, CiPriority.ARGUMENT);
					this.writeChar(41);
				}
			}
		}
	}

	#writeConsoleWrite(args, newLine)
	{
		this.include("stdio.h");
		if (args.length == 0)
			this.write("putchar('\\n')");
		else {
			let interpolated;
			if ((interpolated = args[0]) instanceof CiInterpolatedString) {
				this.write("printf(");
				this.writePrintf(interpolated, newLine);
			}
			else if (args[0].type instanceof CiNumericType) {
				this.write("printf(");
				this.writePrintfNotInterpolated(args, newLine);
			}
			else if (!newLine) {
				this.write("fputs(");
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(", stdout)");
			}
			else
				this.writeCall("puts", args[0]);
		}
	}

	static #getVtblStructClass(klass)
	{
		while (!klass.addsVirtualMethods()) {
			const baseClass = klass.parent;
			klass = baseClass;
		}
		return klass;
	}

	static #getVtblPtrClass(klass)
	{
		for (let result = null;;) {
			if (klass.addsVirtualMethods())
				result = klass;
			let baseClass;
			if (!((baseClass = klass.parent) instanceof CiClass))
				return result;
			klass = baseClass;
		}
	}

	writeCCall(obj, method, args)
	{
		let klass = this.currentClass;
		let declaringClass = method.parent;
		if (GenC.isReferenceTo(obj, CiId.BASE_PTR)) {
			this.writeName(method);
			this.write("(&self->base");
			this.#writeUpcast(declaringClass, klass.parent);
		}
		else {
			let definingClass = declaringClass;
			switch (method.callType) {
			case CiCallType.ABSTRACT:
			case CiCallType.VIRTUAL:
			case CiCallType.OVERRIDE:
				if (method.callType == CiCallType.OVERRIDE) {
					const declaringClass1 = method.getDeclaringMethod().parent;
					declaringClass = declaringClass1;
				}
				if (obj != null)
					klass = obj.type.asClassType().class;
				let ptrClass = GenC.#getVtblPtrClass(klass);
				let structClass = GenC.#getVtblStructClass(definingClass);
				if (structClass != ptrClass) {
					this.write("((const ");
					this.writeName(structClass);
					this.write("Vtbl *) ");
				}
				if (obj != null) {
					obj.accept(this, CiPriority.PRIMARY);
					this.#writeMemberAccess(obj, ptrClass);
				}
				else
					this.#writeSelfForField(ptrClass);
				this.write("vtbl");
				if (structClass != ptrClass)
					this.writeChar(41);
				this.write("->");
				this.writeCamelCase(method.name);
				break;
			default:
				this.writeName(method);
				break;
			}
			this.writeChar(40);
			if (method.callType != CiCallType.STATIC) {
				if (obj != null)
					this.#writeClassPtr(declaringClass, obj, CiPriority.ARGUMENT);
				else if (klass == declaringClass)
					this.write("self");
				else {
					this.write("&self->base");
					this.#writeUpcast(declaringClass, klass.parent);
				}
			}
		}
		this.#writeArgsAndRightParenthesis(method, args);
	}

	#writeTryParse(obj, args)
	{
		this.includeStdBool();
		this.write("_TryParse(&");
		obj.accept(this, CiPriority.PRIMARY);
		this.write(", ");
		args[0].accept(this, CiPriority.ARGUMENT);
		if (obj.type instanceof CiIntegerType)
			this.writeTryParseRadix(args);
		this.writeChar(41);
	}

	writeStringSubstring(obj, args, parent)
	{
		if (args.length == 1) {
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			this.writeAdd(obj, args[0]);
			if (parent > CiPriority.ADD)
				this.writeChar(41);
		}
		else
			this.notSupported(obj, "Substring");
	}

	#startArrayContains(obj)
	{
		this.write("CiArray_Contains_");
		let typeId = obj.type.asClassType().getElementType().id;
		switch (typeId) {
		case CiId.NONE:
			this.write("object(");
			break;
		case CiId.STRING_STORAGE_TYPE:
		case CiId.STRING_PTR_TYPE:
			typeId = CiId.STRING_PTR_TYPE;
			this.include("string.h");
			this.write("string((const char * const *) ");
			break;
		default:
			this.writeNumericType(typeId);
			this.write("((const ");
			this.writeNumericType(typeId);
			this.write(" *) ");
			break;
		}
		this.#contains.add(typeId);
	}

	#startArrayIndexing(obj, elementType)
	{
		this.write("g_array_index(");
		obj.accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		this.writeType(elementType, false);
		this.write(", ");
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.NONE:
		case CiId.CLASS_TO_STRING:
			this.writeCCall(obj, method, args);
			break;
		case CiId.ENUM_FROM_INT:
			this.writeStaticCast(method.type, args[0]);
			break;
		case CiId.ENUM_HAS_FLAG:
			this.writeEnumHasFlag(obj, args, parent);
			break;
		case CiId.INT_TRY_PARSE:
			this.#intTryParse = true;
			this.write("CiInt");
			this.#writeTryParse(obj, args);
			break;
		case CiId.LONG_TRY_PARSE:
			this.#longTryParse = true;
			this.write("CiLong");
			this.#writeTryParse(obj, args);
			break;
		case CiId.DOUBLE_TRY_PARSE:
			this.#doubleTryParse = true;
			this.write("CiDouble");
			this.#writeTryParse(obj, args);
			break;
		case CiId.STRING_CONTAINS:
			this.include("string.h");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			let c = this.getOneAscii(args[0]);
			if (c >= 0) {
				this.write("strchr(");
				obj.accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				this.visitLiteralChar(c);
				this.writeChar(41);
			}
			else
				this.writeCall("strstr", obj, args[0]);
			this.write(" != NULL");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.STRING_ENDS_WITH:
			this.#stringEndsWith = true;
			this.#writeStringMethod("EndsWith", obj, args);
			break;
		case CiId.STRING_INDEX_OF:
			this.#stringIndexOf = true;
			this.#writeStringMethod("IndexOf", obj, args);
			break;
		case CiId.STRING_LAST_INDEX_OF:
			this.#stringLastIndexOf = true;
			this.#writeStringMethod("LastIndexOf", obj, args);
			break;
		case CiId.STRING_REPLACE:
			this.include("string.h");
			this.#stringAppend = true;
			this.#stringReplace = true;
			this.writeCall("CiString_Replace", obj, args[0], args[1]);
			break;
		case CiId.STRING_STARTS_WITH:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			let c2 = this.getOneAscii(args[0]);
			if (c2 >= 0) {
				this.writePostfix(obj, "[0] == ");
				this.visitLiteralChar(c2);
			}
			else {
				this.include("string.h");
				this.write("strncmp(");
				obj.accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(", strlen(");
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(")) == 0");
			}
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.STRING_SUBSTRING:
			this.writeStringSubstring(obj, args, parent);
			break;
		case CiId.ARRAY_BINARY_SEARCH_ALL:
		case CiId.ARRAY_BINARY_SEARCH_PART:
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			this.write("(const ");
			let elementType2 = obj.type.asClassType().getElementType();
			this.writeType(elementType2, false);
			this.write(" *) bsearch(&");
			args[0].accept(this, CiPriority.PRIMARY);
			this.write(", ");
			if (args.length == 1)
				this.writeArrayPtr(obj, CiPriority.ARGUMENT);
			else
				this.writeArrayPtrAdd(obj, args[1]);
			this.write(", ");
			if (args.length == 1)
				this.writeArrayStorageLength(obj);
			else
				args[2].accept(this, CiPriority.ARGUMENT);
			this.#writeSizeofCompare(elementType2);
			this.write(" - ");
			this.writeArrayPtr(obj, CiPriority.MUL);
			if (parent > CiPriority.ADD)
				this.writeChar(41);
			break;
		case CiId.ARRAY_CONTAINS:
			this.#startArrayContains(obj);
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeArrayStorageLength(obj);
			this.write(", ");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.ARRAY_COPY_TO:
		case CiId.LIST_COPY_TO:
			this.include("string.h");
			let elementType = obj.type.asClassType().getElementType();
			if (GenC.#isHeapAllocated(elementType))
				this.notYet(obj, "CopyTo for this type");
			this.write("memcpy(");
			this.writeArrayPtrAdd(args[1], args[2]);
			this.write(", ");
			this.writeArrayPtrAdd(obj, args[0]);
			this.write(", ");
			if (elementType.id == CiId.S_BYTE_RANGE || elementType.id == CiId.BYTE_RANGE)
				args[3].accept(this, CiPriority.ARGUMENT);
			else {
				args[3].accept(this, CiPriority.MUL);
				this.write(" * sizeof(");
				this.writeType(elementType, false);
				this.writeChar(41);
			}
			this.writeChar(41);
			break;
		case CiId.ARRAY_FILL_ALL:
		case CiId.ARRAY_FILL_PART:
			let literal;
			if ((literal = args[0]) instanceof CiLiteral && literal.isDefaultValue()) {
				this.include("string.h");
				this.write("memset(");
				if (args.length == 1) {
					obj.accept(this, CiPriority.ARGUMENT);
					this.write(", 0, sizeof(");
					obj.accept(this, CiPriority.ARGUMENT);
					this.writeChar(41);
				}
				else {
					this.writeArrayPtrAdd(obj, args[1]);
					this.write(", 0, ");
					args[2].accept(this, CiPriority.MUL);
					this.write(" * sizeof(");
					this.writeType(obj.type.asClassType().getElementType(), false);
					this.writeChar(41);
				}
				this.writeChar(41);
			}
			else
				this.writeArrayFill(obj, args);
			break;
		case CiId.ARRAY_SORT_ALL:
			this.write("qsort(");
			this.writeArrayPtr(obj, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeArrayStorageLength(obj);
			this.#writeSizeofCompare(obj.type.asClassType().getElementType());
			break;
		case CiId.ARRAY_SORT_PART:
		case CiId.LIST_SORT_PART:
			this.write("qsort(");
			this.writeArrayPtrAdd(obj, args[0]);
			this.write(", ");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.#writeSizeofCompare(obj.type.asClassType().getElementType());
			break;
		case CiId.LIST_ADD:
		case CiId.STACK_PUSH:
			let storage;
			if (obj.type.asClassType().getElementType() instanceof CiArrayStorageType || ((storage = obj.type.asClassType().getElementType()) instanceof CiStorageType && storage.class.id == CiId.NONE && !this.needsConstructor(storage.class))) {
				this.write("g_array_set_size(");
				obj.accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				this.writePostfix(obj, "->len + 1)");
			}
			else
				this.#writeListAddInsert(obj, false, "g_array_append_val", args);
			break;
		case CiId.LIST_CLEAR:
		case CiId.STACK_CLEAR:
			this.write("g_array_set_size(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", 0)");
			break;
		case CiId.LIST_CONTAINS:
			this.#startArrayContains(obj);
			this.writePostfix(obj, "->data, ");
			this.writePostfix(obj, "->len, ");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.LIST_INSERT:
			this.#writeListAddInsert(obj, true, "g_array_insert_val", args);
			break;
		case CiId.LIST_LAST:
		case CiId.STACK_PEEK:
			this.#startArrayIndexing(obj, obj.type.asClassType().getElementType());
			this.writePostfix(obj, "->len - 1)");
			break;
		case CiId.LIST_REMOVE_AT:
			this.writeCall("g_array_remove_index", obj, args[0]);
			break;
		case CiId.LIST_REMOVE_RANGE:
			this.writeCall("g_array_remove_range", obj, args[0], args[1]);
			break;
		case CiId.LIST_SORT_ALL:
			this.write("g_array_sort(");
			obj.accept(this, CiPriority.ARGUMENT);
			let typeId2 = obj.type.asClassType().getElementType().id;
			this.write(", CiCompare_");
			this.writeNumericType(typeId2);
			this.writeChar(41);
			this.#compares.add(typeId2);
			break;
		case CiId.QUEUE_CLEAR:
			this.write("g_queue_clear(");
			this.#writeQueueObject(obj);
			this.writeChar(41);
			break;
		case CiId.QUEUE_DEQUEUE:
			this.#writeQueueGet("g_queue_pop_head", obj, parent);
			break;
		case CiId.QUEUE_ENQUEUE:
			this.write("g_queue_push_tail(");
			this.#writeQueueObject(obj);
			this.write(", ");
			this.#writeGPointerCast(obj.type.asClassType().getElementType(), args[0]);
			this.writeChar(41);
			break;
		case CiId.QUEUE_PEEK:
			this.#writeQueueGet("g_queue_peek_head", obj, parent);
			break;
		case CiId.STACK_POP:
			if (parent == CiPriority.STATEMENT)
				this.writePostfix(obj, "->len--");
			else {
				this.#startArrayIndexing(obj, obj.type.asClassType().getElementType());
				this.write("--");
				this.writePostfix(obj, "->len)");
			}
			break;
		case CiId.HASH_SET_ADD:
			this.write("g_hash_table_add(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeGPointerCast(obj.type.asClassType().getElementType(), args[0]);
			this.writeChar(41);
			break;
		case CiId.HASH_SET_CLEAR:
		case CiId.DICTIONARY_CLEAR:
			this.writeCall("g_hash_table_remove_all", obj);
			break;
		case CiId.HASH_SET_CONTAINS:
		case CiId.DICTIONARY_CONTAINS_KEY:
			this.#writeDictionaryLookup(obj, "g_hash_table_contains", args[0]);
			break;
		case CiId.HASH_SET_REMOVE:
		case CiId.DICTIONARY_REMOVE:
			this.#writeDictionaryLookup(obj, "g_hash_table_remove", args[0]);
			break;
		case CiId.SORTED_SET_ADD:
			this.write("g_tree_insert(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeGPointerCast(obj.type.asClassType().getKeyType(), args[0]);
			this.write(", NULL)");
			break;
		case CiId.DICTIONARY_ADD:
			this.#startDictionaryInsert(obj, args[0]);
			let valueType = obj.type.asClassType().getValueType().asClassType();
			switch (valueType.class.id) {
			case CiId.LIST_CLASS:
			case CiId.STACK_CLASS:
			case CiId.DICTIONARY_CLASS:
			case CiId.SORTED_DICTIONARY_CLASS:
				this.writeNewStorage(valueType);
				break;
			default:
				if (valueType.class.isPublic && valueType.class.constructor_ != null && valueType.class.constructor_.visibility == CiVisibility.PUBLIC) {
					this.writeName(valueType.class);
					this.write("_New()");
				}
				else {
					this.write("malloc(sizeof(");
					this.writeType(valueType, false);
					this.write("))");
				}
				break;
			}
			this.writeChar(41);
			break;
		case CiId.SORTED_SET_CLEAR:
		case CiId.SORTED_DICTIONARY_CLEAR:
			this.write("g_tree_destroy(g_tree_ref(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write("))");
			break;
		case CiId.SORTED_SET_CONTAINS:
		case CiId.SORTED_DICTIONARY_CONTAINS_KEY:
			this.write("g_tree_lookup_extended(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeGConstPointerCast(args[0]);
			this.write(", NULL, NULL)");
			break;
		case CiId.SORTED_SET_REMOVE:
		case CiId.SORTED_DICTIONARY_REMOVE:
			this.#writeDictionaryLookup(obj, "g_tree_remove", args[0]);
			break;
		case CiId.TEXT_WRITER_WRITE:
			this.#writeTextWriterWrite(obj, args, false);
			break;
		case CiId.TEXT_WRITER_WRITE_CHAR:
			this.writeCall("putc", args[0], obj);
			break;
		case CiId.TEXT_WRITER_WRITE_LINE:
			this.#writeTextWriterWrite(obj, args, true);
			break;
		case CiId.CONSOLE_WRITE:
			this.#writeConsoleWrite(args, false);
			break;
		case CiId.CONSOLE_WRITE_LINE:
			this.#writeConsoleWrite(args, true);
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			this.writeStringLength(args[0]);
			break;
		case CiId.U_T_F8_GET_BYTES:
			this.include("string.h");
			this.write("memcpy(");
			this.writeArrayPtrAdd(args[1], args[2]);
			this.write(", ");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", strlen(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write("))");
			break;
		case CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE:
			this.writeCall("getenv", args[0]);
			break;
		case CiId.REGEX_COMPILE:
			this.#writeGlib("g_regex_new(");
			this.#writeTemporaryOrExpr(args[0], CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeCRegexOptions(args);
			this.write(", 0, NULL)");
			break;
		case CiId.REGEX_ESCAPE:
			this.#writeGlib("g_regex_escape_string(");
			this.#writeTemporaryOrExpr(args[0], CiPriority.ARGUMENT);
			this.write(", -1)");
			break;
		case CiId.REGEX_IS_MATCH_STR:
			this.#writeGlib("g_regex_match_simple(");
			this.#writeTemporaryOrExpr(args[1], CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeTemporaryOrExpr(args[0], CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeCRegexOptions(args);
			this.write(", 0)");
			break;
		case CiId.REGEX_IS_MATCH_REGEX:
			this.write("g_regex_match(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeTemporaryOrExpr(args[0], CiPriority.ARGUMENT);
			this.write(", 0, NULL)");
			break;
		case CiId.MATCH_FIND_STR:
			this.#matchFind = true;
			this.write("CiMatch_Find(&");
			obj.accept(this, CiPriority.PRIMARY);
			this.write(", ");
			this.#writeTemporaryOrExpr(args[0], CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeTemporaryOrExpr(args[1], CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeCRegexOptions(args);
			this.writeChar(41);
			break;
		case CiId.MATCH_FIND_REGEX:
			this.write("g_regex_match(");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeTemporaryOrExpr(args[0], CiPriority.ARGUMENT);
			this.write(", 0, &");
			obj.accept(this, CiPriority.PRIMARY);
			this.writeChar(41);
			break;
		case CiId.MATCH_GET_CAPTURE:
			this.writeCall("g_match_info_fetch", obj, args[0]);
			break;
		case CiId.MATH_METHOD:
		case CiId.MATH_IS_FINITE:
		case CiId.MATH_IS_NA_N:
		case CiId.MATH_LOG2:
			this.includeMath();
			this.writeLowercase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_ABS:
			switch (args[0].type.id) {
			case CiId.LONG_TYPE:
				this.writeCall("llabs", args[0]);
				break;
			case CiId.FLOAT_TYPE:
				this.includeMath();
				this.writeCall("fabsf", args[0]);
				break;
			case CiId.FLOAT_INT_TYPE:
			case CiId.DOUBLE_TYPE:
				this.includeMath();
				this.writeCall("fabs", args[0]);
				break;
			default:
				this.writeCall("abs", args[0]);
				break;
			}
			break;
		case CiId.MATH_CEILING:
			this.includeMath();
			this.writeCall("ceil", args[0]);
			break;
		case CiId.MATH_FUSED_MULTIPLY_ADD:
			this.includeMath();
			this.writeCall("fma", args[0], args[1], args[2]);
			break;
		case CiId.MATH_IS_INFINITY:
			this.includeMath();
			this.writeCall("isinf", args[0]);
			break;
		case CiId.MATH_MAX_DOUBLE:
			this.includeMath();
			this.writeCall("fmax", args[0], args[1]);
			break;
		case CiId.MATH_MIN_DOUBLE:
			this.includeMath();
			this.writeCall("fmin", args[0], args[1]);
			break;
		case CiId.MATH_ROUND:
			this.includeMath();
			this.writeCall("round", args[0]);
			break;
		case CiId.MATH_TRUNCATE:
			this.includeMath();
			this.writeCall("trunc", args[0]);
			break;
		default:
			this.notSupported(obj, method.name);
			break;
		}
	}

	#writeDictionaryIndexing(function_, expr, parent)
	{
		let valueType = expr.left.type.asClassType().getValueType();
		if (valueType instanceof CiIntegerType && valueType.id != CiId.LONG_TYPE) {
			this.write("GPOINTER_TO_INT(");
			this.#writeDictionaryLookup(expr.left, function_, expr.right);
			this.writeChar(41);
		}
		else {
			if (parent > CiPriority.MUL)
				this.writeChar(40);
			let storage;
			if ((storage = valueType) instanceof CiStorageType && (storage.class.id == CiId.NONE || storage.class.id == CiId.ARRAY_STORAGE_CLASS))
				this.#writeDynamicArrayCast(valueType);
			else {
				this.writeStaticCastType(valueType);
				if (valueType instanceof CiEnum) {
					console.assert(parent <= CiPriority.MUL, "Should close two parens");
					this.write("GPOINTER_TO_INT(");
				}
			}
			this.#writeDictionaryLookup(expr.left, function_, expr.right);
			if (parent > CiPriority.MUL || valueType instanceof CiEnum)
				this.writeChar(41);
		}
	}

	writeIndexingExpr(expr, parent)
	{
		let klass;
		if ((klass = expr.left.type) instanceof CiClassType) {
			switch (klass.class.id) {
			case CiId.LIST_CLASS:
				if (klass.getElementType() instanceof CiArrayStorageType) {
					this.writeChar(40);
					this.#writeDynamicArrayCast(klass.getElementType());
					this.writePostfix(expr.left, "->data)[");
					expr.right.accept(this, CiPriority.ARGUMENT);
					this.writeChar(93);
				}
				else {
					this.#startArrayIndexing(expr.left, klass.getElementType());
					expr.right.accept(this, CiPriority.ARGUMENT);
					this.writeChar(41);
				}
				return;
			case CiId.DICTIONARY_CLASS:
				this.#writeDictionaryIndexing("g_hash_table_lookup", expr, parent);
				return;
			case CiId.SORTED_DICTIONARY_CLASS:
				this.#writeDictionaryIndexing("g_tree_lookup", expr, parent);
				return;
			default:
				break;
			}
		}
		super.writeIndexingExpr(expr, parent);
	}

	visitBinaryExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.PLUS:
			if (expr.type.id == CiId.STRING_STORAGE_TYPE)
				this.notSupported(expr, "String concatenation");
			break;
		case CiToken.EQUAL:
		case CiToken.NOT_EQUAL:
		case CiToken.GREATER:
			let str = GenC.isStringEmpty(expr);
			if (str != null) {
				this.writePostfix(str, expr.op == CiToken.EQUAL ? "[0] == '\\0'" : "[0] != '\\0'");
				return;
			}
			break;
		case CiToken.ADD_ASSIGN:
			if (expr.left.type.id == CiId.STRING_STORAGE_TYPE) {
				let rightInterpolated;
				if ((rightInterpolated = expr.right) instanceof CiInterpolatedString) {
					this.#stringAssign = true;
					this.write("CiString_Assign(&");
					expr.left.accept(this, CiPriority.PRIMARY);
					this.#stringFormat = true;
					this.include("stdarg.h");
					this.include("stdio.h");
					this.write(", CiString_Format(\"%s");
					this.writePrintfFormat(rightInterpolated);
					this.write("\", ");
					expr.left.accept(this, CiPriority.ARGUMENT);
					this.writeInterpolatedStringArgs(rightInterpolated);
					this.writeChar(41);
				}
				else {
					this.include("string.h");
					this.#stringAppend = true;
					this.write("CiString_Append(&");
					expr.left.accept(this, CiPriority.PRIMARY);
					this.write(", ");
					expr.right.accept(this, CiPriority.ARGUMENT);
				}
				this.writeChar(41);
				return;
			}
			break;
		case CiToken.IS:
			this.notSupported(expr, "'is' operator");
			break;
		default:
			break;
		}
		super.visitBinaryExpr(expr, parent);
	}

	writeResource(name, length)
	{
		this.write("CiResource_");
		this.writeResourceName(name);
	}

	visitLambdaExpr(expr)
	{
		this.notSupported(expr, "Lambda expression");
	}

	#writeDestructLoopOrSwitch(loopOrSwitch)
	{
		for (let i = this.#varsToDestruct.length; --i >= 0;) {
			let def = this.#varsToDestruct[i];
			if (!loopOrSwitch.encloses(def))
				break;
			this.#writeDestruct(def);
		}
	}

	#trimVarsToDestruct(i)
	{
		this.#varsToDestruct.splice(i, this.#varsToDestruct.length - i);
	}

	cleanupBlock(statement)
	{
		let i = this.#varsToDestruct.length;
		for (; i > 0; i--) {
			let def = this.#varsToDestruct[i - 1];
			if (def.parent != statement)
				break;
			if (statement.completesNormally())
				this.#writeDestruct(def);
		}
		this.#trimVarsToDestruct(i);
	}

	visitBreak(statement)
	{
		this.#writeDestructLoopOrSwitch(statement.loopOrSwitch);
		super.visitBreak(statement);
	}

	visitContinue(statement)
	{
		this.#writeDestructLoopOrSwitch(statement.loop);
		super.visitContinue(statement);
	}

	visitExpr(statement)
	{
		this.#writeCTemporaries(statement);
		let throwingMethod = GenC.#getThrowingMethod(statement);
		if (throwingMethod != null) {
			this.ensureChildBlock();
			statement.accept(this, this.#startForwardThrow(throwingMethod));
			this.#endForwardThrow(throwingMethod);
			this.cleanupTemporaries();
		}
		else if (statement instanceof CiCallExpr && statement.type.id == CiId.STRING_STORAGE_TYPE) {
			this.write("free(");
			statement.accept(this, CiPriority.ARGUMENT);
			this.writeLine(");");
			this.cleanupTemporaries();
		}
		else if (statement instanceof CiCallExpr && statement.type instanceof CiDynamicPtrType) {
			this.#sharedRelease = true;
			this.write("CiShared_Release(");
			statement.accept(this, CiPriority.ARGUMENT);
			this.writeLine(");");
			this.cleanupTemporaries();
		}
		else
			super.visitExpr(statement);
	}

	#startForeachHashTable(statement)
	{
		this.openBlock();
		this.writeLine("GHashTableIter cidictit;");
		this.write("g_hash_table_iter_init(&cidictit, ");
		statement.collection.accept(this, CiPriority.ARGUMENT);
		this.writeLine(");");
	}

	#writeDictIterVar(iter, value)
	{
		this.writeTypeAndName(iter);
		this.write(" = ");
		if (iter.type instanceof CiIntegerType && iter.type.id != CiId.LONG_TYPE) {
			this.write("GPOINTER_TO_INT(");
			this.write(value);
			this.writeChar(41);
		}
		else {
			this.writeStaticCastType(iter.type);
			this.write(value);
		}
		this.writeCharLine(59);
	}

	visitForeach(statement)
	{
		let element = statement.getVar().name;
		if (statement.collection.type instanceof CiArrayStorageType) {
			const array = statement.collection.type;
			this.write("for (int ");
			this.writeCamelCaseNotKeyword(element);
			this.write(" = 0; ");
			this.writeCamelCaseNotKeyword(element);
			this.write(" < ");
			this.visitLiteralLong(BigInt(array.length));
			this.write("; ");
			this.writeCamelCaseNotKeyword(element);
			this.write("++)");
			this.writeChild(statement.body);
		}
		else if (statement.collection.type instanceof CiClassType) {
			const klass = statement.collection.type;
			switch (klass.class.id) {
			case CiId.STRING_CLASS:
				this.write("for (");
				this.writeStringPtrType();
				this.writeCamelCaseNotKeyword(element);
				this.write(" = ");
				statement.collection.accept(this, CiPriority.ARGUMENT);
				this.write("; *");
				this.writeCamelCaseNotKeyword(element);
				this.write(" != '\\0'; ");
				this.writeCamelCaseNotKeyword(element);
				this.write("++)");
				this.writeChild(statement.body);
				break;
			case CiId.LIST_CLASS:
				this.write("for (");
				let elementType = klass.getElementType();
				this.writeType(elementType, false);
				this.write(" const *");
				this.writeCamelCaseNotKeyword(element);
				this.write(" = (");
				this.writeType(elementType, false);
				this.write(" const *) ");
				this.writePostfix(statement.collection, "->data, ");
				for (; elementType.isArray(); elementType = elementType.asClassType().getElementType())
					this.writeChar(42);
				if (elementType instanceof CiClassType)
					this.write("* const ");
				this.write("*ciend = ");
				this.writeCamelCaseNotKeyword(element);
				this.write(" + ");
				this.writePostfix(statement.collection, "->len; ");
				this.writeCamelCaseNotKeyword(element);
				this.write(" < ciend; ");
				this.writeCamelCaseNotKeyword(element);
				this.write("++)");
				this.writeChild(statement.body);
				break;
			case CiId.HASH_SET_CLASS:
				this.#startForeachHashTable(statement);
				this.writeLine("gpointer cikey;");
				this.write("while (g_hash_table_iter_next(&cidictit, &cikey, NULL)) ");
				this.openBlock();
				this.#writeDictIterVar(statement.getVar(), "cikey");
				this.flattenBlock(statement.body);
				this.closeBlock();
				this.closeBlock();
				break;
			case CiId.SORTED_SET_CLASS:
				this.write("for (GTreeNode *cisetit = g_tree_node_first(");
				statement.collection.accept(this, CiPriority.ARGUMENT);
				this.write("); cisetit != NULL; cisetit = g_tree_node_next(cisetit)) ");
				this.openBlock();
				this.#writeDictIterVar(statement.getVar(), "g_tree_node_key(cisetit)");
				this.flattenBlock(statement.body);
				this.closeBlock();
				break;
			case CiId.DICTIONARY_CLASS:
				this.#startForeachHashTable(statement);
				this.writeLine("gpointer cikey, civalue;");
				this.write("while (g_hash_table_iter_next(&cidictit, &cikey, &civalue)) ");
				this.openBlock();
				this.#writeDictIterVar(statement.getVar(), "cikey");
				this.#writeDictIterVar(statement.getValueVar(), "civalue");
				this.flattenBlock(statement.body);
				this.closeBlock();
				this.closeBlock();
				break;
			case CiId.SORTED_DICTIONARY_CLASS:
				this.write("for (GTreeNode *cidictit = g_tree_node_first(");
				statement.collection.accept(this, CiPriority.ARGUMENT);
				this.write("); cidictit != NULL; cidictit = g_tree_node_next(cidictit)) ");
				this.openBlock();
				this.#writeDictIterVar(statement.getVar(), "g_tree_node_key(cidictit)");
				this.#writeDictIterVar(statement.getValueVar(), "g_tree_node_value(cidictit)");
				this.flattenBlock(statement.body);
				this.closeBlock();
				break;
			default:
				this.notSupported(statement.collection, klass.class.name);
				break;
			}
		}
		else
			this.notSupported(statement.collection, statement.collection.type.toString());
	}

	visitLock(statement)
	{
		this.write("mtx_lock(&");
		statement.lock.accept(this, CiPriority.PRIMARY);
		this.writeLine(");");
		statement.body.acceptStatement(this);
		this.write("mtx_unlock(&");
		statement.lock.accept(this, CiPriority.PRIMARY);
		this.writeLine(");");
	}

	visitReturn(statement)
	{
		if (statement.value == null) {
			this.#writeDestructAll();
			this.writeLine(this.currentMethod.throws ? "return true;" : "return;");
		}
		else if (statement.value instanceof CiLiteral || (this.#varsToDestruct.length == 0 && !GenC.#hasTemporariesToDestruct(statement.value))) {
			this.#writeDestructAll();
			this.#writeCTemporaries(statement.value);
			super.visitReturn(statement);
		}
		else {
			let symbol;
			let local;
			if ((symbol = statement.value) instanceof CiSymbolReference && (local = symbol.symbol) instanceof CiVar) {
				if (this.#varsToDestruct.includes(local)) {
					this.#writeDestructAll(local);
					this.write("return ");
					let resultPtr;
					if ((resultPtr = this.currentMethod.type) instanceof CiClassType)
						this.#writeClassPtr(resultPtr.class, symbol, CiPriority.ARGUMENT);
					else
						symbol.accept(this, CiPriority.ARGUMENT);
					this.writeCharLine(59);
					return;
				}
				this.#writeDestructAll();
				super.visitReturn(statement);
				return;
			}
			this.#writeCTemporaries(statement.value);
			this.ensureChildBlock();
			this.#startDefinition(this.currentMethod.type, true, true);
			this.write("returnValue = ");
			this.writeCoerced(this.currentMethod.type, statement.value, CiPriority.ARGUMENT);
			this.writeCharLine(59);
			this.cleanupTemporaries();
			this.#writeDestructAll();
			this.writeLine("return returnValue;");
		}
	}

	writeSwitchCaseBody(statements)
	{
		let konst;
		if (statements[0] instanceof CiVar || ((konst = statements[0]) instanceof CiConst && konst.type instanceof CiArrayStorageType))
			this.writeCharLine(59);
		let varsToDestructCount = this.#varsToDestruct.length;
		this.writeStatements(statements);
		this.#trimVarsToDestruct(varsToDestructCount);
	}

	visitSwitch(statement)
	{
		if (statement.isTypeMatching())
			this.notSupported(statement, "Type-matching 'switch'");
		else
			super.visitSwitch(statement);
	}

	visitThrow(statement)
	{
		this.#writeThrow();
	}

	#tryWriteCallAndReturn(statements, lastCallIndex, returnValue)
	{
		if (this.#varsToDestruct.length > 0)
			return false;
		for (let i = 0; i < lastCallIndex; i++) {
			let def;
			if ((def = statements[i]) instanceof CiVar && GenC.#needToDestruct(def))
				return false;
		}
		let call;
		if (!((call = statements[lastCallIndex]) instanceof CiExpr))
			return false;
		let throwingMethod = GenC.#getThrowingMethod(call);
		if (throwingMethod == null)
			return false;
		this.writeFirstStatements(statements, lastCallIndex);
		this.write("return ");
		if (throwingMethod.type instanceof CiNumericType) {
			if (throwingMethod.type instanceof CiIntegerType) {
				call.accept(this, CiPriority.EQUALITY);
				this.write(" != -1");
			}
			else {
				this.includeMath();
				this.write("!isnan(");
				call.accept(this, CiPriority.ARGUMENT);
				this.writeChar(41);
			}
		}
		else if (throwingMethod.type.id == CiId.VOID_TYPE)
			call.accept(this, CiPriority.SELECT);
		else {
			call.accept(this, CiPriority.EQUALITY);
			this.write(" != NULL");
		}
		if (returnValue != null) {
			this.write(" ? ");
			returnValue.accept(this, CiPriority.SELECT);
			this.write(" : ");
			this.#writeThrowReturnValue();
		}
		this.writeCharLine(59);
		return true;
	}

	writeStatements(statements)
	{
		let i = statements.length - 2;
		let ret;
		if (i >= 0 && (ret = statements[i + 1]) instanceof CiReturn && this.#tryWriteCallAndReturn(statements, i, ret.value))
			return;
		super.writeStatements(statements);
	}

	writeEnum(enu)
	{
		this.writeNewLine();
		this.writeDoc(enu.documentation);
		this.write("typedef enum ");
		this.openBlock();
		enu.acceptValues(this);
		this.writeNewLine();
		this.indent--;
		this.write("} ");
		this.writeName(enu);
		this.writeCharLine(59);
	}

	#writeTypedef(klass)
	{
		if (klass.callType == CiCallType.STATIC)
			return;
		this.write("typedef struct ");
		this.writeName(klass);
		this.writeChar(32);
		this.writeName(klass);
		this.writeCharLine(59);
	}

	writeTypedefs(program, pub)
	{
		for (let type = program.first; type != null; type = type.next) {
			if (type instanceof CiClass) {
				const klass = type;
				if (klass.isPublic == pub)
					this.#writeTypedef(klass);
			}
			else if (type instanceof CiEnum) {
				const enu = type;
				if (enu.isPublic == pub)
					this.writeEnum(enu);
			}
			else
				throw new Error();
		}
	}

	#writeInstanceParameters(method)
	{
		this.writeChar(40);
		if (!method.isMutator)
			this.write("const ");
		this.writeName(method.parent);
		this.write(" *self");
		this.writeRemainingParameters(method, false, false);
	}

	#writeSignature(method)
	{
		const klass = method.parent;
		if (!klass.isPublic || method.visibility != CiVisibility.PUBLIC)
			this.write("static ");
		this.#writeReturnType(method);
		this.writeName(klass);
		this.writeChar(95);
		this.write(method.name);
		if (method.callType != CiCallType.STATIC)
			this.#writeInstanceParameters(method);
		else if (method.parameters.count() == 0)
			this.write("(void)");
		else
			this.writeParameters(method, false);
	}

	#writeVtblFields(klass)
	{
		let baseClass;
		if ((baseClass = klass.parent) instanceof CiClass)
			this.#writeVtblFields(baseClass);
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let method;
			if ((method = symbol) instanceof CiMethod && method.isAbstractOrVirtual()) {
				this.#writeReturnType(method);
				this.write("(*");
				this.writeCamelCase(method.name);
				this.writeChar(41);
				this.#writeInstanceParameters(method);
				this.writeCharLine(59);
			}
		}
	}

	#writeVtblStruct(klass)
	{
		this.write("typedef struct ");
		this.openBlock();
		this.#writeVtblFields(klass);
		this.indent--;
		this.write("} ");
		this.writeName(klass);
		this.writeLine("Vtbl;");
	}

	getConst(array)
	{
		return "const ";
	}

	writeConst(konst)
	{
		let array;
		if ((array = konst.type) instanceof CiArrayStorageType) {
			this.write("static ");
			this.write(this.getConst(array));
			this.writeTypeAndName(konst);
			this.write(" = ");
			konst.value.accept(this, CiPriority.ARGUMENT);
			this.writeCharLine(59);
		}
		else if (konst.visibility == CiVisibility.PUBLIC) {
			this.write("#define ");
			this.writeName(konst);
			this.writeChar(32);
			konst.value.accept(this, CiPriority.ARGUMENT);
			this.writeNewLine();
		}
	}

	writeField(field)
	{
		throw new Error();
	}

	static #hasVtblValue(klass)
	{
		if (klass.callType == CiCallType.STATIC || klass.callType == CiCallType.ABSTRACT)
			return false;
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let method;
			if ((method = symbol) instanceof CiMethod) {
				switch (method.callType) {
				case CiCallType.VIRTUAL:
				case CiCallType.OVERRIDE:
				case CiCallType.SEALED:
					return true;
				default:
					break;
				}
			}
		}
		return false;
	}

	needsConstructor(klass)
	{
		if (klass.id == CiId.MATCH_CLASS)
			return false;
		let baseClass;
		return super.needsConstructor(klass) || GenC.#hasVtblValue(klass) || ((baseClass = klass.parent) instanceof CiClass && this.needsConstructor(baseClass));
	}

	#writeXstructorSignature(name, klass)
	{
		this.write("static void ");
		this.writeName(klass);
		this.writeChar(95);
		this.write(name);
		this.writeChar(40);
		this.writeName(klass);
		this.write(" *self)");
	}

	writeSignatures(klass, pub)
	{
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let konst;
			let method;
			if ((konst = symbol) instanceof CiConst && (konst.visibility == CiVisibility.PUBLIC) == pub) {
				if (pub) {
					this.writeNewLine();
					this.writeDoc(konst.documentation);
				}
				this.writeConst(konst);
			}
			else if ((method = symbol) instanceof CiMethod && method.isLive && (method.visibility == CiVisibility.PUBLIC) == pub && method.callType != CiCallType.ABSTRACT) {
				this.writeNewLine();
				this.writeMethodDoc(method);
				this.#writeSignature(method);
				this.writeCharLine(59);
			}
		}
	}

	writeClassInternal(klass)
	{
		this.currentClass = klass;
		if (klass.callType != CiCallType.STATIC) {
			this.writeNewLine();
			if (klass.addsVirtualMethods())
				this.#writeVtblStruct(klass);
			this.writeDoc(klass.documentation);
			this.write("struct ");
			this.writeName(klass);
			this.writeChar(32);
			this.openBlock();
			if (GenC.#getVtblPtrClass(klass) == klass) {
				this.write("const ");
				this.writeName(klass);
				this.writeLine("Vtbl *vtbl;");
			}
			if (klass.parent instanceof CiClass) {
				this.writeName(klass.parent);
				this.writeLine(" base;");
			}
			for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
				let field;
				if ((field = symbol) instanceof CiField) {
					this.writeDoc(field.documentation);
					this.writeTypeAndName(field);
					this.writeCharLine(59);
				}
			}
			this.indent--;
			this.writeLine("};");
			if (this.needsConstructor(klass)) {
				this.#writeXstructorSignature("Construct", klass);
				this.writeCharLine(59);
			}
			if (GenC.#needsDestructor(klass)) {
				this.#writeXstructorSignature("Destruct", klass);
				this.writeCharLine(59);
			}
		}
		this.writeSignatures(klass, false);
	}

	#writeVtbl(definingClass, declaringClass)
	{
		let baseClass;
		if ((baseClass = declaringClass.parent) instanceof CiClass)
			this.#writeVtbl(definingClass, baseClass);
		for (let symbol = declaringClass.first; symbol != null; symbol = symbol.next) {
			let declaredMethod;
			if ((declaredMethod = symbol) instanceof CiMethod && declaredMethod.isAbstractOrVirtual()) {
				let definedMethod = definingClass.tryLookup(declaredMethod.name, false);
				if (declaredMethod != definedMethod) {
					this.writeChar(40);
					this.#writeReturnType(declaredMethod);
					this.write("(*)");
					this.#writeInstanceParameters(declaredMethod);
					this.write(") ");
				}
				this.writeName(definedMethod);
				this.writeCharLine(44);
			}
		}
	}

	writeConstructor(klass)
	{
		if (!this.needsConstructor(klass))
			return;
		this.switchesWithGoto.length = 0;
		this.writeNewLine();
		this.#writeXstructorSignature("Construct", klass);
		this.writeNewLine();
		this.openBlock();
		let baseClass;
		if ((baseClass = klass.parent) instanceof CiClass && this.needsConstructor(baseClass)) {
			this.writeName(baseClass);
			this.writeLine("_Construct(&self->base);");
		}
		if (GenC.#hasVtblValue(klass)) {
			let structClass = GenC.#getVtblStructClass(klass);
			this.write("static const ");
			this.writeName(structClass);
			this.write("Vtbl vtbl = ");
			this.openBlock();
			this.#writeVtbl(klass, structClass);
			this.indent--;
			this.writeLine("};");
			let ptrClass = GenC.#getVtblPtrClass(klass);
			this.#writeSelfForField(ptrClass);
			this.write("vtbl = ");
			if (ptrClass != structClass) {
				this.write("(const ");
				this.writeName(ptrClass);
				this.write("Vtbl *) ");
			}
			this.writeLine("&vtbl;");
		}
		this.writeConstructorBody(klass);
		this.closeBlock();
	}

	#writeDestructFields(symbol)
	{
		if (symbol != null) {
			this.#writeDestructFields(symbol.next);
			let field;
			if ((field = symbol) instanceof CiField)
				this.#writeDestruct(field);
		}
	}

	writeDestructor(klass)
	{
		if (!GenC.#needsDestructor(klass))
			return;
		this.writeNewLine();
		this.#writeXstructorSignature("Destruct", klass);
		this.writeNewLine();
		this.openBlock();
		this.#writeDestructFields(klass.first);
		let baseClass;
		if ((baseClass = klass.parent) instanceof CiClass && GenC.#needsDestructor(baseClass)) {
			this.writeName(baseClass);
			this.writeLine("_Destruct(&self->base);");
		}
		this.closeBlock();
	}

	#writeNewDelete(klass, define)
	{
		if (!klass.isPublic || klass.constructor_ == null || klass.constructor_.visibility != CiVisibility.PUBLIC)
			return;
		this.writeNewLine();
		this.writeName(klass);
		this.write(" *");
		this.writeName(klass);
		this.write("_New(void)");
		if (define) {
			this.writeNewLine();
			this.openBlock();
			this.writeName(klass);
			this.write(" *self = (");
			this.writeName(klass);
			this.write(" *) malloc(sizeof(");
			this.writeName(klass);
			this.writeLine("));");
			if (this.needsConstructor(klass)) {
				this.writeLine("if (self != NULL)");
				this.indent++;
				this.writeName(klass);
				this.writeLine("_Construct(self);");
				this.indent--;
			}
			this.writeLine("return self;");
			this.closeBlock();
			this.writeNewLine();
		}
		else
			this.writeCharLine(59);
		this.write("void ");
		this.writeName(klass);
		this.write("_Delete(");
		this.writeName(klass);
		this.write(" *self)");
		if (define) {
			this.writeNewLine();
			this.openBlock();
			if (GenC.#needsDestructor(klass)) {
				this.writeLine("if (self == NULL)");
				this.indent++;
				this.writeLine("return;");
				this.indent--;
				this.writeName(klass);
				this.writeLine("_Destruct(self);");
			}
			this.writeLine("free(self);");
			this.closeBlock();
		}
		else
			this.writeCharLine(59);
	}

	writeMethod(method)
	{
		if (!method.isLive || method.callType == CiCallType.ABSTRACT)
			return;
		this.switchesWithGoto.length = 0;
		this.writeNewLine();
		this.#writeSignature(method);
		for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
			if (GenC.#needToDestruct(param))
				this.#varsToDestruct.push(param);
		}
		this.writeNewLine();
		this.currentMethod = method;
		this.openBlock();
		let block;
		if ((block = method.body) instanceof CiBlock) {
			let statements = block.statements;
			if (!block.completesNormally())
				this.writeStatements(statements);
			else if (method.throws && method.type.id == CiId.VOID_TYPE) {
				if (statements.length == 0 || !this.#tryWriteCallAndReturn(statements, statements.length - 1, null)) {
					this.writeStatements(statements);
					this.#writeDestructAll();
					this.writeLine("return true;");
				}
			}
			else {
				this.writeStatements(statements);
				this.#writeDestructAll();
			}
		}
		else
			method.body.acceptStatement(this);
		this.#varsToDestruct.length = 0;
		this.closeBlock();
		this.currentMethod = null;
	}

	#writeTryParseLibrary(signature, call)
	{
		this.writeNewLine();
		this.write("static bool Ci");
		this.writeLine(signature);
		this.openBlock();
		this.writeLine("if (*str == '\\0')");
		this.writeLine("\treturn false;");
		this.writeLine("char *end;");
		this.write("*result = strto");
		this.write(call);
		this.writeLine(");");
		this.writeLine("return *end == '\\0';");
		this.closeBlock();
	}

	#writeLibrary()
	{
		if (this.#intTryParse)
			this.#writeTryParseLibrary("Int_TryParse(int *result, const char *str, int base)", "l(str, &end, base");
		if (this.#longTryParse)
			this.#writeTryParseLibrary("Long_TryParse(int64_t *result, const char *str, int base)", "ll(str, &end, base");
		if (this.#doubleTryParse)
			this.#writeTryParseLibrary("Double_TryParse(double *result, const char *str)", "d(str, &end");
		if (this.#stringAssign) {
			this.writeNewLine();
			this.writeLine("static void CiString_Assign(char **str, char *value)");
			this.openBlock();
			this.writeLine("free(*str);");
			this.writeLine("*str = value;");
			this.closeBlock();
		}
		if (this.#stringSubstring) {
			this.writeNewLine();
			this.writeLine("static char *CiString_Substring(const char *str, int len)");
			this.openBlock();
			this.writeLine("char *p = malloc(len + 1);");
			this.writeLine("memcpy(p, str, len);");
			this.writeLine("p[len] = '\\0';");
			this.writeLine("return p;");
			this.closeBlock();
		}
		if (this.#stringAppend) {
			this.writeNewLine();
			this.writeLine("static void CiString_AppendSubstring(char **str, const char *suffix, size_t suffixLen)");
			this.openBlock();
			this.writeLine("if (suffixLen == 0)");
			this.writeLine("\treturn;");
			this.writeLine("size_t prefixLen = *str == NULL ? 0 : strlen(*str);");
			this.writeLine("*str = realloc(*str, prefixLen + suffixLen + 1);");
			this.writeLine("memcpy(*str + prefixLen, suffix, suffixLen);");
			this.writeLine("(*str)[prefixLen + suffixLen] = '\\0';");
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("static void CiString_Append(char **str, const char *suffix)");
			this.openBlock();
			this.writeLine("CiString_AppendSubstring(str, suffix, strlen(suffix));");
			this.closeBlock();
		}
		if (this.#stringIndexOf) {
			this.writeNewLine();
			this.writeLine("static int CiString_IndexOf(const char *str, const char *needle)");
			this.openBlock();
			this.writeLine("const char *p = strstr(str, needle);");
			this.writeLine("return p == NULL ? -1 : (int) (p - str);");
			this.closeBlock();
		}
		if (this.#stringLastIndexOf) {
			this.writeNewLine();
			this.writeLine("static int CiString_LastIndexOf(const char *str, const char *needle)");
			this.openBlock();
			this.writeLine("if (needle[0] == '\\0')");
			this.writeLine("\treturn (int) strlen(str);");
			this.writeLine("int result = -1;");
			this.writeLine("const char *p = strstr(str, needle);");
			this.write("while (p != NULL) ");
			this.openBlock();
			this.writeLine("result = (int) (p - str);");
			this.writeLine("p = strstr(p + 1, needle);");
			this.closeBlock();
			this.writeLine("return result;");
			this.closeBlock();
		}
		if (this.#stringEndsWith) {
			this.writeNewLine();
			this.writeLine("static bool CiString_EndsWith(const char *str, const char *suffix)");
			this.openBlock();
			this.writeLine("size_t strLen = strlen(str);");
			this.writeLine("size_t suffixLen = strlen(suffix);");
			this.writeLine("return strLen >= suffixLen && memcmp(str + strLen - suffixLen, suffix, suffixLen) == 0;");
			this.closeBlock();
		}
		if (this.#stringReplace) {
			this.writeNewLine();
			this.writeLine("static char *CiString_Replace(const char *s, const char *oldValue, const char *newValue)");
			this.openBlock();
			this.write("for (char *result = NULL;;) ");
			this.openBlock();
			this.writeLine("const char *p = strstr(s, oldValue);");
			this.writeLine("if (p == NULL) {");
			this.writeLine("\tCiString_Append(&result, s);");
			this.writeLine("\treturn result == NULL ? strdup(\"\") : result;");
			this.writeCharLine(125);
			this.writeLine("CiString_AppendSubstring(&result, s, p - s);");
			this.writeLine("CiString_Append(&result, newValue);");
			this.writeLine("s = p + strlen(oldValue);");
			this.closeBlock();
			this.closeBlock();
		}
		if (this.#stringFormat) {
			this.writeNewLine();
			this.writeLine("static char *CiString_Format(const char *format, ...)");
			this.openBlock();
			this.writeLine("va_list args1;");
			this.writeLine("va_start(args1, format);");
			this.writeLine("va_list args2;");
			this.writeLine("va_copy(args2, args1);");
			this.writeLine("size_t len = vsnprintf(NULL, 0, format, args1) + 1;");
			this.writeLine("va_end(args1);");
			this.writeLine("char *str = malloc(len);");
			this.writeLine("vsnprintf(str, len, format, args2);");
			this.writeLine("va_end(args2);");
			this.writeLine("return str;");
			this.closeBlock();
		}
		if (this.#matchFind) {
			this.writeNewLine();
			this.writeLine("static bool CiMatch_Find(GMatchInfo **match_info, const char *input, const char *pattern, GRegexCompileFlags options)");
			this.openBlock();
			this.writeLine("GRegex *regex = g_regex_new(pattern, options, 0, NULL);");
			this.writeLine("bool result = g_regex_match(regex, input, 0, match_info);");
			this.writeLine("g_regex_unref(regex);");
			this.writeLine("return result;");
			this.closeBlock();
		}
		if (this.#matchPos) {
			this.writeNewLine();
			this.writeLine("static int CiMatch_GetPos(const GMatchInfo *match_info, int which)");
			this.openBlock();
			this.writeLine("int start;");
			this.writeLine("int end;");
			this.writeLine("g_match_info_fetch_pos(match_info, 0, &start, &end);");
			this.writeLine("switch (which) {");
			this.writeLine("case 0:");
			this.writeLine("\treturn start;");
			this.writeLine("case 1:");
			this.writeLine("\treturn end;");
			this.writeLine("default:");
			this.writeLine("\treturn end - start;");
			this.writeCharLine(125);
			this.closeBlock();
		}
		if (this.#ptrConstruct) {
			this.writeNewLine();
			this.writeLine("static void CiPtr_Construct(void **ptr)");
			this.openBlock();
			this.writeLine("*ptr = NULL;");
			this.closeBlock();
		}
		if (this.#sharedMake || this.#sharedAddRef || this.#sharedRelease) {
			this.writeNewLine();
			this.writeLine("typedef void (*CiMethodPtr)(void *);");
			this.writeLine("typedef struct {");
			this.indent++;
			this.writeLine("size_t count;");
			this.writeLine("size_t unitSize;");
			this.writeLine("size_t refCount;");
			this.writeLine("CiMethodPtr destructor;");
			this.indent--;
			this.writeLine("} CiShared;");
		}
		if (this.#sharedMake) {
			this.writeNewLine();
			this.writeLine("static void *CiShared_Make(size_t count, size_t unitSize, CiMethodPtr constructor, CiMethodPtr destructor)");
			this.openBlock();
			this.writeLine("CiShared *self = (CiShared *) malloc(sizeof(CiShared) + count * unitSize);");
			this.writeLine("self->count = count;");
			this.writeLine("self->unitSize = unitSize;");
			this.writeLine("self->refCount = 1;");
			this.writeLine("self->destructor = destructor;");
			this.write("if (constructor != NULL) ");
			this.openBlock();
			this.writeLine("for (size_t i = 0; i < count; i++)");
			this.writeLine("\tconstructor((char *) (self + 1) + i * unitSize);");
			this.closeBlock();
			this.writeLine("return self + 1;");
			this.closeBlock();
		}
		if (this.#sharedAddRef) {
			this.writeNewLine();
			this.writeLine("static void *CiShared_AddRef(void *ptr)");
			this.openBlock();
			this.writeLine("if (ptr != NULL)");
			this.writeLine("\t((CiShared *) ptr)[-1].refCount++;");
			this.writeLine("return ptr;");
			this.closeBlock();
		}
		if (this.#sharedRelease || this.#sharedAssign) {
			this.writeNewLine();
			this.writeLine("static void CiShared_Release(void *ptr)");
			this.openBlock();
			this.writeLine("if (ptr == NULL)");
			this.writeLine("\treturn;");
			this.writeLine("CiShared *self = (CiShared *) ptr - 1;");
			this.writeLine("if (--self->refCount != 0)");
			this.writeLine("\treturn;");
			this.write("if (self->destructor != NULL) ");
			this.openBlock();
			this.writeLine("for (size_t i = self->count; i > 0;)");
			this.writeLine("\tself->destructor((char *) ptr + --i * self->unitSize);");
			this.closeBlock();
			this.writeLine("free(self);");
			this.closeBlock();
		}
		if (this.#sharedAssign) {
			this.writeNewLine();
			this.writeLine("static void CiShared_Assign(void **ptr, void *value)");
			this.openBlock();
			this.writeLine("CiShared_Release(*ptr);");
			this.writeLine("*ptr = value;");
			this.closeBlock();
		}
		for (const [name, content] of Object.entries(this.#listFrees).sort((a, b) => a[0].localeCompare(b[0]))) {
			this.writeNewLine();
			this.write("static void CiList_Free");
			this.write(name);
			this.writeLine("(void *ptr)");
			this.openBlock();
			this.write(content);
			this.writeCharLine(59);
			this.closeBlock();
		}
		if (this.#treeCompareInteger) {
			this.writeNewLine();
			this.write("static int CiTree_CompareInteger(gconstpointer pa, gconstpointer pb, gpointer user_data)");
			this.openBlock();
			this.writeLine("gintptr a = (gintptr) pa;");
			this.writeLine("gintptr b = (gintptr) pb;");
			this.writeLine("return (a > b) - (a < b);");
			this.closeBlock();
		}
		if (this.#treeCompareString) {
			this.writeNewLine();
			this.write("static int CiTree_CompareString(gconstpointer a, gconstpointer b, gpointer user_data)");
			this.openBlock();
			this.writeLine("return strcmp((const char *) a, (const char *) b);");
			this.closeBlock();
		}
		for (const typeId of new Int32Array(this.#compares).sort()) {
			this.writeNewLine();
			this.write("static int CiCompare_");
			this.writeNumericType(typeId);
			this.writeLine("(const void *pa, const void *pb)");
			this.openBlock();
			this.writeNumericType(typeId);
			this.write(" a = *(const ");
			this.writeNumericType(typeId);
			this.writeLine(" *) pa;");
			this.writeNumericType(typeId);
			this.write(" b = *(const ");
			this.writeNumericType(typeId);
			this.writeLine(" *) pb;");
			switch (typeId) {
			case CiId.BYTE_RANGE:
			case CiId.S_BYTE_RANGE:
			case CiId.SHORT_RANGE:
			case CiId.U_SHORT_RANGE:
				this.writeLine("return a - b;");
				break;
			default:
				this.writeLine("return (a > b) - (a < b);");
				break;
			}
			this.closeBlock();
		}
		for (const typeId of new Int32Array(this.#contains).sort()) {
			this.writeNewLine();
			this.write("static bool CiArray_Contains_");
			if (typeId == CiId.NONE)
				this.write("object(const void * const *a, size_t len, const void *");
			else if (typeId == CiId.STRING_PTR_TYPE)
				this.write("string(const char * const *a, size_t len, const char *");
			else {
				this.writeNumericType(typeId);
				this.write("(const ");
				this.writeNumericType(typeId);
				this.write(" *a, size_t len, ");
				this.writeNumericType(typeId);
			}
			this.writeLine(" value)");
			this.openBlock();
			this.writeLine("for (size_t i = 0; i < len; i++)");
			if (typeId == CiId.STRING_PTR_TYPE)
				this.writeLine("\tif (strcmp(a[i], value) == 0)");
			else
				this.writeLine("\tif (a[i] == value)");
			this.writeLine("\t\treturn true;");
			this.writeLine("return false;");
			this.closeBlock();
		}
	}

	writeResources(resources)
	{
		if (Object.keys(resources).length == 0)
			return;
		this.writeNewLine();
		for (const [name, content] of Object.entries(resources).sort((a, b) => a[0].localeCompare(b[0]))) {
			this.write("static const ");
			this.writeNumericType(CiId.BYTE_RANGE);
			this.writeChar(32);
			this.writeResource(name, -1);
			this.writeChar(91);
			this.visitLiteralLong(BigInt(content.length));
			this.writeLine("] = {");
			this.writeChar(9);
			this.writeBytes(content);
			this.writeLine(" };");
		}
	}

	writeProgram(program)
	{
		this.writtenClasses.clear();
		this.inHeaderFile = true;
		this.openStringWriter();
		for (const klass of program.classes) {
			this.#writeNewDelete(klass, false);
			this.writeSignatures(klass, true);
		}
		this.createHeaderFile(".h");
		this.writeLine("#ifdef __cplusplus");
		this.writeLine("extern \"C\" {");
		this.writeLine("#endif");
		this.writeTypedefs(program, true);
		this.closeStringWriter();
		this.writeNewLine();
		this.writeLine("#ifdef __cplusplus");
		this.writeCharLine(125);
		this.writeLine("#endif");
		this.closeFile();
		this.inHeaderFile = false;
		this.#intTryParse = false;
		this.#longTryParse = false;
		this.#doubleTryParse = false;
		this.#stringAssign = false;
		this.#stringSubstring = false;
		this.#stringAppend = false;
		this.#stringIndexOf = false;
		this.#stringLastIndexOf = false;
		this.#stringEndsWith = false;
		this.#stringReplace = false;
		this.#stringFormat = false;
		this.#matchFind = false;
		this.#matchPos = false;
		this.#ptrConstruct = false;
		this.#sharedMake = false;
		this.#sharedAddRef = false;
		this.#sharedRelease = false;
		this.#sharedAssign = false;
		for (const key in this.#listFrees)
			delete this.#listFrees[key];;
		this.#treeCompareInteger = false;
		this.#treeCompareString = false;
		this.#compares.clear();
		this.#contains.clear();
		this.openStringWriter();
		for (const klass of program.classes)
			this.writeClass(klass, program);
		this.writeResources(program.resources);
		for (const klass of program.classes) {
			this.currentClass = klass;
			this.writeConstructor(klass);
			this.writeDestructor(klass);
			this.#writeNewDelete(klass, true);
			this.writeMethods(klass);
		}
		this.include("stdlib.h");
		this.createImplementationFile(program, ".h");
		this.#writeLibrary();
		this.writeRegexOptionsEnum(program);
		this.writeTypedefs(program, false);
		this.closeStringWriter();
		this.closeFile();
	}
}

export class GenCl extends GenC
{
	#stringLength;
	#stringEquals;
	#stringStartsWith;
	#bytesEqualsString;

	getTargetName()
	{
		return "OpenCL C";
	}

	includeStdBool()
	{
	}

	includeMath()
	{
	}

	writeNumericType(id)
	{
		switch (id) {
		case CiId.S_BYTE_RANGE:
			this.write("char");
			break;
		case CiId.BYTE_RANGE:
			this.write("uchar");
			break;
		case CiId.SHORT_RANGE:
			this.write("short");
			break;
		case CiId.U_SHORT_RANGE:
			this.write("ushort");
			break;
		case CiId.INT_TYPE:
			this.write("int");
			break;
		case CiId.LONG_TYPE:
			this.write("long");
			break;
		default:
			throw new Error();
		}
	}

	writeStringPtrType()
	{
		this.write("constant char *");
	}

	writeClassType(klass, space)
	{
		switch (klass.class.id) {
		case CiId.NONE:
			if (klass instanceof CiDynamicPtrType)
				this.notSupported(klass, "Dynamic reference");
			else
				super.writeClassType(klass, space);
			break;
		case CiId.STRING_CLASS:
			if (klass.id == CiId.STRING_STORAGE_TYPE)
				this.notSupported(klass, "string()");
			else
				this.writeStringPtrType();
			break;
		default:
			this.notSupported(klass, klass.class.name);
			break;
		}
	}

	writePrintfLongPrefix()
	{
		this.writeChar(108);
	}

	writeInterpolatedStringArgBase(expr)
	{
		expr.accept(this, CiPriority.ARGUMENT);
	}

	visitInterpolatedString(expr, parent)
	{
		this.notSupported(expr, "Interpolated strings");
	}

	writeCamelCaseNotKeyword(name)
	{
		switch (name) {
		case "Constant":
		case "Global":
		case "Kernel":
		case "Local":
		case "Private":
		case "constant":
		case "global":
		case "kernel":
		case "local":
		case "private":
			this.writeCamelCase(name);
			this.writeChar(95);
			break;
		default:
			super.writeCamelCaseNotKeyword(name);
			break;
		}
	}

	getConst(array)
	{
		return array.ptrTaken ? "const " : "constant ";
	}

	writeSubstringEqual(call, literal, parent, not)
	{
		if (not)
			this.writeChar(33);
		if (GenCl.isUTF8GetString(call)) {
			this.#bytesEqualsString = true;
			this.write("CiBytes_Equals(");
		}
		else {
			this.#stringStartsWith = true;
			this.write("CiString_StartsWith(");
		}
		this.writeStringPtrAdd(call);
		this.write(", ");
		this.visitLiteralString(literal);
		this.writeChar(41);
	}

	writeEqualStringInternal(left, right, parent, not)
	{
		this.#stringEquals = true;
		if (not)
			this.writeChar(33);
		this.writeCall("CiString_Equals", left, right);
	}

	writeStringLength(expr)
	{
		this.#stringLength = true;
		this.writeCall("strlen", expr);
	}

	#writeConsoleWrite(args, newLine)
	{
		this.write("printf(");
		if (args.length == 0)
			this.write("\"\\n\")");
		else {
			let interpolated;
			if ((interpolated = args[0]) instanceof CiInterpolatedString)
				this.writePrintf(interpolated, newLine);
			else
				this.writePrintfNotInterpolated(args, newLine);
		}
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.NONE:
		case CiId.CLASS_TO_STRING:
			this.writeCCall(obj, method, args);
			break;
		case CiId.ENUM_FROM_INT:
			this.writeStaticCast(method.type, args[0]);
			break;
		case CiId.ENUM_HAS_FLAG:
			this.writeEnumHasFlag(obj, args, parent);
			break;
		case CiId.STRING_STARTS_WITH:
			let c = this.getOneAscii(args[0]);
			if (c >= 0) {
				if (parent > CiPriority.EQUALITY)
					this.writeChar(40);
				this.writePostfix(obj, "[0] == ");
				this.visitLiteralChar(c);
				if (parent > CiPriority.EQUALITY)
					this.writeChar(41);
			}
			else {
				this.#stringStartsWith = true;
				this.writeCall("CiString_StartsWith", obj, args[0]);
			}
			break;
		case CiId.STRING_SUBSTRING:
			this.writeStringSubstring(obj, args, parent);
			break;
		case CiId.ARRAY_COPY_TO:
			this.write("for (size_t _i = 0; _i < ");
			args[3].accept(this, CiPriority.REL);
			this.writeLine("; _i++)");
			this.writeChar(9);
			args[1].accept(this, CiPriority.PRIMARY);
			this.writeChar(91);
			this.startAdd(args[2]);
			this.write("_i] = ");
			obj.accept(this, CiPriority.PRIMARY);
			this.writeChar(91);
			this.startAdd(args[0]);
			this.write("_i]");
			break;
		case CiId.ARRAY_FILL_ALL:
		case CiId.ARRAY_FILL_PART:
			this.writeArrayFill(obj, args);
			break;
		case CiId.CONSOLE_WRITE:
			this.#writeConsoleWrite(args, false);
			break;
		case CiId.CONSOLE_WRITE_LINE:
			this.#writeConsoleWrite(args, true);
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			this.writeStringLength(args[0]);
			break;
		case CiId.U_T_F8_GET_BYTES:
			this.write("for (size_t _i = 0; ");
			args[0].accept(this, CiPriority.PRIMARY);
			this.writeLine("[_i] != '\\0'; _i++)");
			this.writeChar(9);
			args[1].accept(this, CiPriority.PRIMARY);
			this.writeChar(91);
			this.startAdd(args[2]);
			this.write("_i] = ");
			this.writePostfix(args[0], "[_i]");
			break;
		case CiId.MATH_METHOD:
		case CiId.MATH_CLAMP:
		case CiId.MATH_IS_FINITE:
		case CiId.MATH_IS_NA_N:
		case CiId.MATH_LOG2:
		case CiId.MATH_MAX_INT:
		case CiId.MATH_MIN_INT:
		case CiId.MATH_ROUND:
			this.writeLowercase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_ABS:
			if (args[0].type instanceof CiFloatingType)
				this.writeChar(102);
			this.writeCall("abs", args[0]);
			break;
		case CiId.MATH_CEILING:
			this.writeCall("ceil", args[0]);
			break;
		case CiId.MATH_FUSED_MULTIPLY_ADD:
			this.writeCall("fma", args[0], args[1], args[2]);
			break;
		case CiId.MATH_IS_INFINITY:
			this.writeCall("isinf", args[0]);
			break;
		case CiId.MATH_MAX_DOUBLE:
			this.writeCall("fmax", args[0], args[1]);
			break;
		case CiId.MATH_MIN_DOUBLE:
			this.writeCall("fmin", args[0], args[1]);
			break;
		case CiId.MATH_TRUNCATE:
			this.writeCall("trunc", args[0]);
			break;
		default:
			this.notSupported(obj, method.name);
			break;
		}
	}

	writeAssert(statement)
	{
	}

	writeSwitchCaseBody(statements)
	{
		if (statements.every(statement => statement instanceof CiAssert))
			this.writeCharLine(59);
		else
			super.writeSwitchCaseBody(statements);
	}

	#writeLibrary()
	{
		if (this.#stringLength) {
			this.writeNewLine();
			this.writeLine("static int strlen(constant char *str)");
			this.openBlock();
			this.writeLine("int len = 0;");
			this.writeLine("while (str[len] != '\\0')");
			this.writeLine("\tlen++;");
			this.writeLine("return len;");
			this.closeBlock();
		}
		if (this.#stringEquals) {
			this.writeNewLine();
			this.writeLine("static bool CiString_Equals(constant char *str1, constant char *str2)");
			this.openBlock();
			this.writeLine("for (size_t i = 0; str1[i] == str2[i]; i++) {");
			this.writeLine("\tif (str1[i] == '\\0')");
			this.writeLine("\t\treturn true;");
			this.writeCharLine(125);
			this.writeLine("return false;");
			this.closeBlock();
		}
		if (this.#stringStartsWith) {
			this.writeNewLine();
			this.writeLine("static bool CiString_StartsWith(constant char *str1, constant char *str2)");
			this.openBlock();
			this.writeLine("for (int i = 0; str2[i] != '\\0'; i++) {");
			this.writeLine("\tif (str1[i] != str2[i])");
			this.writeLine("\t\treturn false;");
			this.writeCharLine(125);
			this.writeLine("return true;");
			this.closeBlock();
		}
		if (this.#bytesEqualsString) {
			this.writeNewLine();
			this.writeLine("static bool CiBytes_Equals(const uchar *mem, constant char *str)");
			this.openBlock();
			this.writeLine("for (int i = 0; str[i] != '\\0'; i++) {");
			this.writeLine("\tif (mem[i] != str[i])");
			this.writeLine("\t\treturn false;");
			this.writeCharLine(125);
			this.writeLine("return true;");
			this.closeBlock();
		}
	}

	writeProgram(program)
	{
		this.writtenClasses.clear();
		this.#stringLength = false;
		this.#stringEquals = false;
		this.#stringStartsWith = false;
		this.#bytesEqualsString = false;
		this.openStringWriter();
		for (const klass of program.classes) {
			this.currentClass = klass;
			this.writeConstructor(klass);
			this.writeDestructor(klass);
			this.writeMethods(klass);
		}
		this.createOutputFile();
		this.writeTopLevelNatives(program);
		this.writeRegexOptionsEnum(program);
		this.writeTypedefs(program, true);
		for (const klass of program.classes)
			this.writeSignatures(klass, true);
		this.writeTypedefs(program, false);
		for (const klass of program.classes)
			this.writeClass(klass, program);
		this.writeResources(program.resources);
		this.#writeLibrary();
		this.closeStringWriter();
		this.closeFile();
	}
}

export class GenCpp extends GenCCpp
{
	#usingStringViewLiterals;
	#hasEnumFlags;
	#stringReplace;

	getTargetName()
	{
		return "C++";
	}

	includeStdInt()
	{
		this.include("cstdint");
	}

	includeAssert()
	{
		this.include("cassert");
	}

	includeMath()
	{
		this.include("cmath");
	}

	visitLiteralNull()
	{
		this.write("nullptr");
	}

	#startMethodCall(obj)
	{
		obj.accept(this, CiPriority.PRIMARY);
		this.writeMemberOp(obj, null);
	}

	writeInterpolatedStringArg(expr)
	{
		let klass;
		if ((klass = expr.type) instanceof CiClassType && klass.class.id != CiId.STRING_CLASS) {
			this.#startMethodCall(expr);
			this.write("toString()");
		}
		else
			super.writeInterpolatedStringArg(expr);
	}

	visitInterpolatedString(expr, parent)
	{
		this.include("format");
		this.write("std::format(\"");
		for (const part of expr.parts) {
			this.writeDoubling(part.prefix, 123);
			this.writeChar(123);
			this.writePyFormat(part);
		}
		this.writeDoubling(expr.suffix, 123);
		this.writeChar(34);
		this.writeInterpolatedStringArgs(expr);
		this.writeChar(41);
	}

	#writeCamelCaseNotKeyword(name)
	{
		this.writeCamelCase(name);
		switch (name) {
		case "And":
		case "Asm":
		case "Auto":
		case "Bool":
		case "Break":
		case "Byte":
		case "Case":
		case "Catch":
		case "Char":
		case "Class":
		case "Const":
		case "Continue":
		case "Default":
		case "Delete":
		case "Do":
		case "Double":
		case "Else":
		case "Enum":
		case "Explicit":
		case "Export":
		case "Extern":
		case "False":
		case "Float":
		case "For":
		case "Goto":
		case "If":
		case "Inline":
		case "Int":
		case "Long":
		case "Namespace":
		case "New":
		case "Not":
		case "Nullptr":
		case "Operator":
		case "Or":
		case "Override":
		case "Private":
		case "Protected":
		case "Public":
		case "Register":
		case "Return":
		case "Short":
		case "Signed":
		case "Sizeof":
		case "Static":
		case "Struct":
		case "Switch":
		case "Throw":
		case "True":
		case "Try":
		case "Typedef":
		case "Union":
		case "Unsigned":
		case "Using":
		case "Virtual":
		case "Void":
		case "Volatile":
		case "While":
		case "Xor":
		case "and":
		case "asm":
		case "auto":
		case "catch":
		case "char":
		case "delete":
		case "explicit":
		case "export":
		case "extern":
		case "goto":
		case "inline":
		case "namespace":
		case "not":
		case "nullptr":
		case "operator":
		case "or":
		case "private":
		case "register":
		case "signed":
		case "sizeof":
		case "struct":
		case "try":
		case "typedef":
		case "union":
		case "unsigned":
		case "using":
		case "volatile":
		case "xor":
			this.writeChar(95);
			break;
		default:
			break;
		}
	}

	writeName(symbol)
	{
		if (symbol instanceof CiContainerType)
			this.write(symbol.name);
		else if (symbol instanceof CiVar || symbol instanceof CiMember)
			this.#writeCamelCaseNotKeyword(symbol.name);
		else
			throw new Error();
	}

	writeLocalName(symbol, parent)
	{
		if (symbol instanceof CiField)
			this.write("this->");
		this.writeName(symbol);
	}

	#writeCollectionType(name, elementType)
	{
		this.include(name);
		this.write("std::");
		this.write(name);
		this.writeChar(60);
		this.writeType(elementType, false);
		this.writeChar(62);
	}

	writeType(type, promote)
	{
		ciswitch0: {
			if (type instanceof CiIntegerType)
				this.writeNumericType(this.getTypeId(type, promote));
			else if (type instanceof CiDynamicPtrType) {
				const dynamic = type;
				switch (dynamic.class.id) {
				case CiId.REGEX_CLASS:
					this.include("regex");
					this.write("std::regex");
					break;
				case CiId.ARRAY_PTR_CLASS:
					this.include("memory");
					this.write("std::shared_ptr<");
					this.writeType(dynamic.getElementType(), false);
					this.write("[]>");
					break;
				default:
					this.include("memory");
					this.write("std::shared_ptr<");
					this.write(dynamic.class.name);
					this.writeChar(62);
					break;
				}
			}
			else if (type instanceof CiClassType) {
				const klass = type;
				if (klass.class.typeParameterCount == 0) {
					if (klass.class.id == CiId.STRING_CLASS) {
						let cppType = klass.id == CiId.STRING_STORAGE_TYPE ? "string" : "string_view";
						this.include(cppType);
						this.write("std::");
						this.write(cppType);
						break ciswitch0;
					}
					if (!(klass instanceof CiReadWriteClassType))
						this.write("const ");
					switch (klass.class.id) {
					case CiId.TEXT_WRITER_CLASS:
						this.include("iostream");
						this.write("std::ostream");
						break;
					case CiId.STRING_WRITER_CLASS:
						this.include("sstream");
						this.write("std::ostringstream");
						break;
					case CiId.REGEX_CLASS:
						this.include("regex");
						this.write("std::regex");
						break;
					case CiId.MATCH_CLASS:
						this.include("regex");
						this.write("std::cmatch");
						break;
					case CiId.LOCK_CLASS:
						this.include("mutex");
						this.write("std::recursive_mutex");
						break;
					default:
						this.write(klass.class.name);
						break;
					}
				}
				else if (klass.class.id == CiId.ARRAY_PTR_CLASS) {
					this.writeType(klass.getElementType(), false);
					if (!(klass instanceof CiReadWriteClassType))
						this.write(" const");
				}
				else {
					let cppType;
					switch (klass.class.id) {
					case CiId.ARRAY_STORAGE_CLASS:
						cppType = "array";
						break;
					case CiId.LIST_CLASS:
						cppType = "vector";
						break;
					case CiId.QUEUE_CLASS:
						cppType = "queue";
						break;
					case CiId.STACK_CLASS:
						cppType = "stack";
						break;
					case CiId.HASH_SET_CLASS:
						cppType = "unordered_set";
						break;
					case CiId.SORTED_SET_CLASS:
						cppType = "set";
						break;
					case CiId.DICTIONARY_CLASS:
						cppType = "unordered_map";
						break;
					case CiId.SORTED_DICTIONARY_CLASS:
						cppType = "map";
						break;
					default:
						this.notSupported(type, klass.class.name);
						cppType = "NOT_SUPPORTED";
						break;
					}
					this.include(cppType);
					if (!(klass instanceof CiReadWriteClassType))
						this.write("const ");
					this.write("std::");
					this.write(cppType);
					this.writeChar(60);
					this.writeType(klass.typeArg0, false);
					let arrayStorage;
					if ((arrayStorage = klass) instanceof CiArrayStorageType) {
						this.write(", ");
						this.visitLiteralLong(BigInt(arrayStorage.length));
					}
					else if (klass.class.typeParameterCount == 2) {
						this.write(", ");
						this.writeType(klass.getValueType(), false);
					}
					this.writeChar(62);
				}
				if (!(klass instanceof CiStorageType))
					this.write(" *");
			}
			else
				this.write(type.name);
		}
	}

	writeNewArray(elementType, lengthExpr, parent)
	{
		this.include("memory");
		this.write("std::make_shared<");
		this.writeType(elementType, false);
		this.write("[]>(");
		lengthExpr.accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	writeNew(klass, parent)
	{
		this.include("memory");
		this.write("std::make_shared<");
		this.write(klass.class.name);
		this.write(">()");
	}

	writeStorageInit(def)
	{
	}

	writeVarInit(def)
	{
		if (def.value != null && def.type.id == CiId.STRING_STORAGE_TYPE) {
			this.writeChar(123);
			def.value.accept(this, CiPriority.ARGUMENT);
			this.writeChar(125);
		}
		else if (def.type instanceof CiArrayStorageType) {
			let literal;
			if (def.value == null) {
			}
			else if ((literal = def.value) instanceof CiLiteral && literal.isDefaultValue())
				this.write(" {}");
			else
				throw new Error();
		}
		else
			super.writeVarInit(def);
	}

	static #isSharedPtr(expr)
	{
		if (expr.type instanceof CiDynamicPtrType)
			return true;
		let symbol;
		let loop;
		return (symbol = expr) instanceof CiSymbolReference && (loop = symbol.symbol.parent) instanceof CiForeach && loop.collection.type.asClassType().getElementType() instanceof CiDynamicPtrType;
	}

	writeStaticCast(type, expr)
	{
		let dynamic;
		if ((dynamic = type) instanceof CiDynamicPtrType) {
			this.write("std::static_pointer_cast<");
			this.write(dynamic.class.name);
		}
		else {
			this.write("static_cast<");
			this.writeType(type, false);
		}
		this.write(">(");
		if (expr.type instanceof CiStorageType) {
			this.writeChar(38);
			expr.accept(this, CiPriority.PRIMARY);
		}
		else if (!(type instanceof CiDynamicPtrType) && GenCpp.#isSharedPtr(expr))
			this.writePostfix(expr, ".get()");
		else
			this.getStaticCastInner(type, expr).accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	static #needStringPtrData(expr)
	{
		let call;
		if ((call = expr) instanceof CiCallExpr && call.method.symbol.id == CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE)
			return false;
		return expr.type.id == CiId.STRING_PTR_TYPE;
	}

	writeEqual(left, right, parent, not)
	{
		if (GenCpp.#needStringPtrData(left) && right.type.id == CiId.NULL_TYPE) {
			this.writePostfix(left, ".data()");
			this.write(GenCpp.getEqOp(not));
			this.write("nullptr");
		}
		else if (left.type.id == CiId.NULL_TYPE && GenCpp.#needStringPtrData(right)) {
			this.write("nullptr");
			this.write(GenCpp.getEqOp(not));
			this.writePostfix(right, ".data()");
		}
		else
			super.writeEqual(left, right, parent, not);
	}

	static #isClassPtr(type)
	{
		let ptr;
		return (ptr = type) instanceof CiClassType && !(type instanceof CiStorageType) && ptr.class.id != CiId.STRING_CLASS && ptr.class.id != CiId.ARRAY_PTR_CLASS;
	}

	static #isCppPtr(expr)
	{
		if (GenCpp.#isClassPtr(expr.type)) {
			let symbol;
			let loop;
			if ((symbol = expr) instanceof CiSymbolReference && (loop = symbol.symbol.parent) instanceof CiForeach && (symbol.symbol == loop.getVar() ? loop.collection.type.asClassType().typeArg0 : loop.collection.type.asClassType().typeArg1) instanceof CiStorageType)
				return false;
			return true;
		}
		return false;
	}

	writeIndexingExpr(expr, parent)
	{
		const klass = expr.left.type;
		if (parent != CiPriority.ASSIGN) {
			switch (klass.class.id) {
			case CiId.DICTIONARY_CLASS:
			case CiId.SORTED_DICTIONARY_CLASS:
			case CiId.ORDERED_DICTIONARY_CLASS:
				this.#startMethodCall(expr.left);
				this.write("find(");
				this.writeStronglyCoerced(klass.getKeyType(), expr.right);
				this.write(")->second");
				return;
			default:
				break;
			}
		}
		if (GenCpp.#isClassPtr(expr.left.type)) {
			this.write("(*");
			expr.left.accept(this, CiPriority.PRIMARY);
			this.writeChar(41);
		}
		else
			expr.left.accept(this, CiPriority.PRIMARY);
		this.writeChar(91);
		switch (klass.class.id) {
		case CiId.ARRAY_PTR_CLASS:
		case CiId.ARRAY_STORAGE_CLASS:
		case CiId.LIST_CLASS:
			expr.right.accept(this, CiPriority.ARGUMENT);
			break;
		default:
			this.writeStronglyCoerced(klass.getKeyType(), expr.right);
			break;
		}
		this.writeChar(93);
	}

	writeMemberOp(left, symbol)
	{
		if (symbol != null && symbol.symbol instanceof CiConst)
			this.write("::");
		else if (GenCpp.#isCppPtr(left))
			this.write("->");
		else
			this.writeChar(46);
	}

	writeEnumAsInt(expr, parent)
	{
		this.write("static_cast<int>(");
		expr.accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	#writeCollectionObject(obj, priority)
	{
		if (GenCpp.#isCppPtr(obj)) {
			this.writeChar(42);
			obj.accept(this, CiPriority.PRIMARY);
		}
		else
			obj.accept(this, priority);
	}

	#writeBeginEnd(obj)
	{
		this.#startMethodCall(obj);
		this.write("begin(), ");
		this.#startMethodCall(obj);
		this.write("end()");
	}

	#writePtrRange(obj, index, count)
	{
		this.writeArrayPtrAdd(obj, index);
		this.write(", ");
		this.writeArrayPtrAdd(obj, index);
		this.write(" + ");
		count.accept(this, CiPriority.MUL);
	}

	#writeNotRawStringLiteral(obj, priority)
	{
		obj.accept(this, priority);
		if (obj instanceof CiLiteralString) {
			this.include("string_view");
			this.#usingStringViewLiterals = true;
			this.write("sv");
		}
	}

	#writeStringMethod(obj, name, method, args)
	{
		this.#writeNotRawStringLiteral(obj, CiPriority.PRIMARY);
		this.writeChar(46);
		this.write(name);
		let c = this.getOneAscii(args[0]);
		if (c >= 0) {
			this.writeChar(40);
			this.visitLiteralChar(c);
			this.writeChar(41);
		}
		else
			this.writeArgsInParentheses(method, args);
	}

	#writeAllAnyContains(function_, obj, args)
	{
		this.include("algorithm");
		this.write("std::");
		this.write(function_);
		this.writeChar(40);
		this.#writeBeginEnd(obj);
		this.write(", ");
		args[0].accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	#writeCString(expr)
	{
		if (expr instanceof CiLiteralString)
			expr.accept(this, CiPriority.ARGUMENT);
		else
			this.writePostfix(expr, ".data()");
	}

	#writeRegex(args, argIndex)
	{
		this.include("regex");
		this.write("std::regex(");
		args[argIndex].accept(this, CiPriority.ARGUMENT);
		this.writeRegexOptions(args, ", std::regex::ECMAScript | ", " | ", "", "std::regex::icase", "std::regex::multiline", "std::regex::NOT_SUPPORTED_singleline");
		this.writeChar(41);
	}

	#writeWrite(args, newLine)
	{
		this.include("iostream");
		if (args.length == 1) {
			let interpolated;
			if ((interpolated = args[0]) instanceof CiInterpolatedString) {
				let uppercase = false;
				let hex = false;
				let flt = 71;
				for (const part of interpolated.parts) {
					switch (part.format) {
					case 69:
					case 71:
					case 88:
						if (!uppercase) {
							this.write(" << std::uppercase");
							uppercase = true;
						}
						break;
					case 101:
					case 103:
					case 120:
						if (uppercase) {
							this.write(" << std::nouppercase");
							uppercase = false;
						}
						break;
					default:
						break;
					}
					switch (part.format) {
					case 69:
					case 101:
						if (flt != 69) {
							this.write(" << std::scientific");
							flt = 69;
						}
						break;
					case 70:
					case 102:
						if (flt != 70) {
							this.write(" << std::fixed");
							flt = 70;
						}
						break;
					case 88:
					case 120:
						if (!hex) {
							this.write(" << std::hex");
							hex = true;
						}
						break;
					default:
						if (hex) {
							this.write(" << std::dec");
							hex = false;
						}
						if (flt != 71) {
							this.write(" << std::defaultfloat");
							flt = 71;
						}
						break;
					}
					if (part.prefix.length > 0) {
						this.write(" << ");
						this.visitLiteralString(part.prefix);
					}
					this.write(" << ");
					part.argument.accept(this, CiPriority.MUL);
				}
				if (uppercase)
					this.write(" << std::nouppercase");
				if (hex)
					this.write(" << std::dec");
				if (flt != 71)
					this.write(" << std::defaultfloat");
				if (interpolated.suffix.length > 0) {
					this.write(" << ");
					if (newLine) {
						this.writeStringLiteralWithNewLine(interpolated.suffix);
						return;
					}
					this.visitLiteralString(interpolated.suffix);
				}
			}
			else {
				this.write(" << ");
				let literal;
				if (newLine && (literal = args[0]) instanceof CiLiteralString) {
					this.writeStringLiteralWithNewLine(literal.value);
					return;
				}
				else if (args[0] instanceof CiLiteralChar)
					this.writeCall("static_cast<int>", args[0]);
				else
					args[0].accept(this, CiPriority.MUL);
			}
		}
		if (newLine)
			this.write(" << '\\n'");
	}

	#writeRegexArgument(expr)
	{
		if (expr.type instanceof CiDynamicPtrType)
			expr.accept(this, CiPriority.ARGUMENT);
		else {
			this.writeChar(42);
			expr.accept(this, CiPriority.PRIMARY);
		}
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.NONE:
		case CiId.CLASS_TO_STRING:
		case CiId.LIST_CLEAR:
		case CiId.STACK_PUSH:
		case CiId.HASH_SET_CLEAR:
		case CiId.HASH_SET_CONTAINS:
		case CiId.SORTED_SET_CLEAR:
		case CiId.SORTED_SET_CONTAINS:
		case CiId.DICTIONARY_CLEAR:
		case CiId.SORTED_DICTIONARY_CLEAR:
			if (obj != null) {
				if (GenCpp.isReferenceTo(obj, CiId.BASE_PTR)) {
					this.writeName(method.parent);
					this.write("::");
				}
				else {
					obj.accept(this, CiPriority.PRIMARY);
					if (method.callType == CiCallType.STATIC)
						this.write("::");
					else
						this.writeMemberOp(obj, null);
				}
			}
			this.writeName(method);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.ENUM_FROM_INT:
			this.writeStaticCast(method.type, args[0]);
			break;
		case CiId.ENUM_HAS_FLAG:
			this.writeEnumHasFlag(obj, args, parent);
			break;
		case CiId.INT_TRY_PARSE:
		case CiId.LONG_TRY_PARSE:
			this.include("cstdlib");
			this.write("[&] { char *ciend; ");
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = std::strtol");
			if (method.id == CiId.LONG_TRY_PARSE)
				this.writeChar(108);
			this.writeChar(40);
			this.#writeCString(args[0]);
			this.write(", &ciend");
			this.writeTryParseRadix(args);
			this.write("); return *ciend == '\\0'; }()");
			break;
		case CiId.DOUBLE_TRY_PARSE:
			this.include("cstdlib");
			this.write("[&] { char *ciend; ");
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = std::strtod(");
			this.#writeCString(args[0]);
			this.write(", &ciend); return *ciend == '\\0'; }()");
			break;
		case CiId.STRING_CONTAINS:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			this.#writeStringMethod(obj, "find", method, args);
			this.write(" != std::string::npos");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.STRING_ENDS_WITH:
			this.#writeStringMethod(obj, "ends_with", method, args);
			break;
		case CiId.STRING_INDEX_OF:
			this.write("static_cast<int>(");
			this.#writeStringMethod(obj, "find", method, args);
			this.writeChar(41);
			break;
		case CiId.STRING_LAST_INDEX_OF:
			this.write("static_cast<int>(");
			this.#writeStringMethod(obj, "rfind", method, args);
			this.writeChar(41);
			break;
		case CiId.STRING_REPLACE:
			this.#stringReplace = true;
			this.writeCall("CiString_replace", obj, args[0], args[1]);
			break;
		case CiId.STRING_STARTS_WITH:
			this.#writeStringMethod(obj, "starts_with", method, args);
			break;
		case CiId.STRING_SUBSTRING:
			this.#writeStringMethod(obj, "substr", method, args);
			break;
		case CiId.ARRAY_BINARY_SEARCH_ALL:
		case CiId.ARRAY_BINARY_SEARCH_PART:
			this.include("algorithm");
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			this.write("std::lower_bound(");
			if (args.length == 1)
				this.#writeBeginEnd(obj);
			else
				this.#writePtrRange(obj, args[1], args[2]);
			this.write(", ");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(") - ");
			this.writeArrayPtr(obj, CiPriority.MUL);
			if (parent > CiPriority.ADD)
				this.writeChar(41);
			break;
		case CiId.ARRAY_CONTAINS:
		case CiId.LIST_CONTAINS:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			this.#writeAllAnyContains("find", obj, args);
			this.write(" != ");
			this.#startMethodCall(obj);
			this.write("end()");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.ARRAY_COPY_TO:
		case CiId.LIST_COPY_TO:
			this.include("algorithm");
			this.write("std::copy_n(");
			this.writeArrayPtrAdd(obj, args[0]);
			this.write(", ");
			args[3].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeArrayPtrAdd(args[1], args[2]);
			this.writeChar(41);
			break;
		case CiId.ARRAY_FILL_ALL:
			this.#startMethodCall(obj);
			this.write("fill(");
			this.writeCoerced(obj.type.asClassType().getElementType(), args[0], CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.ARRAY_FILL_PART:
			this.include("algorithm");
			this.write("std::fill_n(");
			this.writeArrayPtrAdd(obj, args[1]);
			this.write(", ");
			args[2].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeCoerced(obj.type.asClassType().getElementType(), args[0], CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.ARRAY_SORT_ALL:
		case CiId.LIST_SORT_ALL:
			this.include("algorithm");
			this.write("std::sort(");
			this.#writeBeginEnd(obj);
			this.writeChar(41);
			break;
		case CiId.ARRAY_SORT_PART:
		case CiId.LIST_SORT_PART:
			this.include("algorithm");
			this.write("std::sort(");
			this.#writePtrRange(obj, args[0], args[1]);
			this.writeChar(41);
			break;
		case CiId.LIST_ADD:
			this.#startMethodCall(obj);
			if (args.length == 0)
				this.write("emplace_back()");
			else {
				this.write("push_back(");
				this.writeCoerced(obj.type.asClassType().getElementType(), args[0], CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			break;
		case CiId.LIST_ADD_RANGE:
			this.#startMethodCall(obj);
			this.write("insert(");
			this.#startMethodCall(obj);
			this.write("end(), ");
			this.#writeBeginEnd(args[0]);
			this.writeChar(41);
			break;
		case CiId.LIST_ALL:
			this.#writeAllAnyContains("all_of", obj, args);
			break;
		case CiId.LIST_ANY:
			this.include("algorithm");
			this.#writeAllAnyContains("any_of", obj, args);
			break;
		case CiId.LIST_INDEX_OF:
			{
				let elementType = obj.type.asClassType().getElementType();
				this.write("[](const ");
				this.#writeCollectionType("vector", elementType);
				this.write(" &v, ");
				this.writeType(elementType, false);
				this.include("algorithm");
				this.write(" value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(");
				this.#writeCollectionObject(obj, CiPriority.ARGUMENT);
				this.write(", ");
				this.writeCoerced(elementType, args[0], CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			break;
		case CiId.LIST_INSERT:
			this.#startMethodCall(obj);
			if (args.length == 1) {
				this.write("emplace(");
				this.writeArrayPtrAdd(obj, args[0]);
			}
			else {
				this.write("insert(");
				this.writeArrayPtrAdd(obj, args[0]);
				this.write(", ");
				this.writeCoerced(obj.type.asClassType().getElementType(), args[1], CiPriority.ARGUMENT);
			}
			this.writeChar(41);
			break;
		case CiId.LIST_LAST:
			this.#startMethodCall(obj);
			this.write("back()");
			break;
		case CiId.LIST_REMOVE_AT:
			this.#startMethodCall(obj);
			this.write("erase(");
			this.writeArrayPtrAdd(obj, args[0]);
			this.writeChar(41);
			break;
		case CiId.LIST_REMOVE_RANGE:
			this.#startMethodCall(obj);
			this.write("erase(");
			this.#writePtrRange(obj, args[0], args[1]);
			this.writeChar(41);
			break;
		case CiId.QUEUE_CLEAR:
		case CiId.STACK_CLEAR:
			this.#writeCollectionObject(obj, CiPriority.ASSIGN);
			this.write(" = {}");
			break;
		case CiId.QUEUE_DEQUEUE:
			if (parent == CiPriority.STATEMENT) {
				this.#startMethodCall(obj);
				this.write("pop()");
			}
			else {
				let elementType = obj.type.asClassType().getElementType();
				this.write("[](");
				this.#writeCollectionType("queue", elementType);
				this.write(" &q) { ");
				this.writeType(elementType, false);
				this.write(" front = q.front(); q.pop(); return front; }(");
				this.#writeCollectionObject(obj, CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			break;
		case CiId.QUEUE_ENQUEUE:
			this.writeMethodCall(obj, "push", args[0]);
			break;
		case CiId.QUEUE_PEEK:
			this.#startMethodCall(obj);
			this.write("front()");
			break;
		case CiId.STACK_PEEK:
			this.#startMethodCall(obj);
			this.write("top()");
			break;
		case CiId.STACK_POP:
			if (parent == CiPriority.STATEMENT) {
				this.#startMethodCall(obj);
				this.write("pop()");
			}
			else {
				let elementType = obj.type.asClassType().getElementType();
				this.write("[](");
				this.#writeCollectionType("stack", elementType);
				this.write(" &s) { ");
				this.writeType(elementType, false);
				this.write(" top = s.top(); s.pop(); return top; }(");
				this.#writeCollectionObject(obj, CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			break;
		case CiId.HASH_SET_ADD:
		case CiId.SORTED_SET_ADD:
			this.writeMethodCall(obj, obj.type.asClassType().getElementType().id == CiId.STRING_STORAGE_TYPE && args[0].type.id == CiId.STRING_PTR_TYPE ? "emplace" : "insert", args[0]);
			break;
		case CiId.HASH_SET_REMOVE:
		case CiId.SORTED_SET_REMOVE:
		case CiId.DICTIONARY_REMOVE:
		case CiId.SORTED_DICTIONARY_REMOVE:
			this.writeMethodCall(obj, "erase", args[0]);
			break;
		case CiId.DICTIONARY_ADD:
			this.writeIndexing(obj, args[0]);
			break;
		case CiId.DICTIONARY_CONTAINS_KEY:
		case CiId.SORTED_DICTIONARY_CONTAINS_KEY:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			this.#startMethodCall(obj);
			this.write("count(");
			this.writeStronglyCoerced(obj.type.asClassType().getKeyType(), args[0]);
			this.write(") != 0");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.TEXT_WRITER_WRITE:
			this.#writeCollectionObject(obj, CiPriority.SHIFT);
			this.#writeWrite(args, false);
			break;
		case CiId.TEXT_WRITER_WRITE_CHAR:
			this.#writeCollectionObject(obj, CiPriority.SHIFT);
			this.write(" << ");
			let literalChar;
			if ((literalChar = args[0]) instanceof CiLiteralChar && literalChar.value < 127)
				args[0].accept(this, CiPriority.MUL);
			else
				this.writeCall("static_cast<char>", args[0]);
			break;
		case CiId.TEXT_WRITER_WRITE_CODE_POINT:
			let literalChar2;
			if ((literalChar2 = args[0]) instanceof CiLiteralChar && literalChar2.value < 127) {
				this.#writeCollectionObject(obj, CiPriority.SHIFT);
				this.write(" << ");
				args[0].accept(this, CiPriority.MUL);
			}
			else {
				this.write("if (");
				args[0].accept(this, CiPriority.REL);
				this.writeLine(" < 0x80)");
				this.writeChar(9);
				this.#writeCollectionObject(obj, CiPriority.SHIFT);
				this.write(" << ");
				this.writeCall("static_cast<char>", args[0]);
				this.writeCharLine(59);
				this.write("else if (");
				args[0].accept(this, CiPriority.REL);
				this.writeLine(" < 0x800)");
				this.writeChar(9);
				this.#writeCollectionObject(obj, CiPriority.SHIFT);
				this.write(" << static_cast<char>(0xc0 | ");
				args[0].accept(this, CiPriority.SHIFT);
				this.write(" >> 6) << static_cast<char>(0x80 | (");
				args[0].accept(this, CiPriority.AND);
				this.writeLine(" & 0x3f));");
				this.write("else if (");
				args[0].accept(this, CiPriority.REL);
				this.writeLine(" < 0x10000)");
				this.writeChar(9);
				this.#writeCollectionObject(obj, CiPriority.SHIFT);
				this.write(" << static_cast<char>(0xe0 | ");
				args[0].accept(this, CiPriority.SHIFT);
				this.write(" >> 12) << static_cast<char>(0x80 | (");
				args[0].accept(this, CiPriority.SHIFT);
				this.write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
				args[0].accept(this, CiPriority.AND);
				this.writeLine(" & 0x3f));");
				this.writeLine("else");
				this.writeChar(9);
				this.#writeCollectionObject(obj, CiPriority.SHIFT);
				this.write(" << static_cast<char>(0xf0 | ");
				args[0].accept(this, CiPriority.SHIFT);
				this.write(" >> 18) << static_cast<char>(0x80 | (");
				args[0].accept(this, CiPriority.SHIFT);
				this.write(" >> 12 & 0x3f)) << static_cast<char>(0x80 | (");
				args[0].accept(this, CiPriority.SHIFT);
				this.write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
				args[0].accept(this, CiPriority.AND);
				this.write(" & 0x3f))");
			}
			break;
		case CiId.TEXT_WRITER_WRITE_LINE:
			this.#writeCollectionObject(obj, CiPriority.SHIFT);
			this.#writeWrite(args, true);
			break;
		case CiId.STRING_WRITER_CLEAR:
			this.include("string");
			this.#startMethodCall(obj);
			this.write("str(std::string())");
			break;
		case CiId.CONSOLE_WRITE:
			this.write("std::cout");
			this.#writeWrite(args, false);
			break;
		case CiId.CONSOLE_WRITE_LINE:
			this.write("std::cout");
			this.#writeWrite(args, true);
			break;
		case CiId.STRING_WRITER_TO_STRING:
			this.#startMethodCall(obj);
			this.write("str()");
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			if (args[0] instanceof CiLiteral) {
				if (parent > CiPriority.ADD)
					this.writeChar(40);
				this.write("sizeof(");
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(") - 1");
				if (parent > CiPriority.ADD)
					this.writeChar(41);
			}
			else
				this.writeStringLength(args[0]);
			break;
		case CiId.U_T_F8_GET_BYTES:
			if (args[0] instanceof CiLiteral) {
				this.include("algorithm");
				this.write("std::copy_n(");
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(", sizeof(");
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(") - 1, ");
				this.writeArrayPtrAdd(args[1], args[2]);
				this.writeChar(41);
			}
			else {
				this.writePostfix(args[0], ".copy(reinterpret_cast<char *>(");
				this.writeArrayPtrAdd(args[1], args[2]);
				this.write("), ");
				this.writePostfix(args[0], ".size())");
			}
			break;
		case CiId.U_T_F8_GET_STRING:
			this.include("string_view");
			this.write("std::string_view(reinterpret_cast<const char *>(");
			this.writeArrayPtrAdd(args[0], args[1]);
			this.write("), ");
			args[2].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE:
			this.include("cstdlib");
			this.write("std::getenv(");
			this.#writeCString(args[0]);
			this.writeChar(41);
			break;
		case CiId.REGEX_COMPILE:
			this.#writeRegex(args, 0);
			break;
		case CiId.REGEX_IS_MATCH_STR:
		case CiId.REGEX_IS_MATCH_REGEX:
		case CiId.MATCH_FIND_STR:
		case CiId.MATCH_FIND_REGEX:
			this.write("std::regex_search(");
			if (args[0].type.id == CiId.STRING_PTR_TYPE && !(args[0] instanceof CiLiteral))
				this.#writeBeginEnd(args[0]);
			else
				args[0].accept(this, CiPriority.ARGUMENT);
			if (method.id == CiId.MATCH_FIND_STR || method.id == CiId.MATCH_FIND_REGEX) {
				this.write(", ");
				obj.accept(this, CiPriority.ARGUMENT);
			}
			this.write(", ");
			if (method.id == CiId.REGEX_IS_MATCH_REGEX)
				this.#writeRegexArgument(obj);
			else if (method.id == CiId.MATCH_FIND_REGEX)
				this.#writeRegexArgument(args[1]);
			else
				this.#writeRegex(args, 1);
			this.writeChar(41);
			break;
		case CiId.MATCH_GET_CAPTURE:
			this.#startMethodCall(obj);
			this.writeCall("str", args[0]);
			break;
		case CiId.MATH_METHOD:
		case CiId.MATH_ABS:
		case CiId.MATH_IS_FINITE:
		case CiId.MATH_IS_NA_N:
		case CiId.MATH_LOG2:
		case CiId.MATH_ROUND:
			this.includeMath();
			this.write("std::");
			this.writeLowercase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_CEILING:
			this.includeMath();
			this.writeCall("std::ceil", args[0]);
			break;
		case CiId.MATH_CLAMP:
			this.include("algorithm");
			this.writeCall("std::clamp", args[0], args[1], args[2]);
			break;
		case CiId.MATH_FUSED_MULTIPLY_ADD:
			this.includeMath();
			this.writeCall("std::fma", args[0], args[1], args[2]);
			break;
		case CiId.MATH_IS_INFINITY:
			this.includeMath();
			this.writeCall("std::isinf", args[0]);
			break;
		case CiId.MATH_MAX_INT:
		case CiId.MATH_MAX_DOUBLE:
			this.include("algorithm");
			this.writeCall("std::max", args[0], args[1]);
			break;
		case CiId.MATH_MIN_INT:
		case CiId.MATH_MIN_DOUBLE:
			this.include("algorithm");
			this.writeCall("std::min", args[0], args[1]);
			break;
		case CiId.MATH_TRUNCATE:
			this.includeMath();
			this.writeCall("std::trunc", args[0]);
			break;
		default:
			this.notSupported(obj, method.name);
			break;
		}
	}

	writeResource(name, length)
	{
		this.write("CiResource::");
		this.writeResourceName(name);
	}

	writeArrayPtr(expr, parent)
	{
		let klass;
		if (expr.type instanceof CiArrayStorageType || expr.type instanceof CiStringType)
			this.writePostfix(expr, ".data()");
		else if (expr.type instanceof CiDynamicPtrType)
			this.writePostfix(expr, ".get()");
		else if ((klass = expr.type) instanceof CiClassType && klass.class.id == CiId.LIST_CLASS) {
			this.#startMethodCall(expr);
			this.write("begin()");
		}
		else
			expr.accept(this, parent);
	}

	writeCoercedInternal(type, expr, parent)
	{
		let klass;
		if ((klass = type) instanceof CiClassType && !(klass instanceof CiDynamicPtrType) && !(klass instanceof CiStorageType)) {
			if (klass.class.id == CiId.STRING_CLASS) {
				if (expr.type.id == CiId.NULL_TYPE) {
					this.include("string_view");
					this.write("std::string_view()");
				}
				else
					expr.accept(this, parent);
				return;
			}
			if (klass.class.id == CiId.ARRAY_PTR_CLASS) {
				this.writeArrayPtr(expr, parent);
				return;
			}
			if (GenCpp.#isSharedPtr(expr)) {
				if (klass.class.id == CiId.REGEX_CLASS) {
					this.writeChar(38);
					expr.accept(this, CiPriority.PRIMARY);
				}
				else
					this.writePostfix(expr, ".get()");
				return;
			}
			if (expr.type instanceof CiClassType && !GenCpp.#isCppPtr(expr)) {
				this.writeChar(38);
				if (expr instanceof CiCallExpr) {
					this.write("static_cast<");
					if (!(klass instanceof CiReadWriteClassType))
						this.write("const ");
					this.writeName(klass.class);
					this.write(" &>(");
					expr.accept(this, CiPriority.ARGUMENT);
					this.writeChar(41);
				}
				else
					expr.accept(this, CiPriority.PRIMARY);
				return;
			}
		}
		super.writeCoercedInternal(type, expr, parent);
	}

	writeSelectValues(type, expr)
	{
		let trueClass;
		let falseClass;
		if ((trueClass = expr.onTrue.type) instanceof CiClassType && (falseClass = expr.onFalse.type) instanceof CiClassType && !trueClass.class.isSameOrBaseOf(falseClass.class) && !falseClass.class.isSameOrBaseOf(trueClass.class)) {
			this.writeStaticCast(type, expr.onTrue);
			this.write(" : ");
			this.writeStaticCast(type, expr.onFalse);
		}
		else
			super.writeSelectValues(type, expr);
	}

	writeStringLength(expr)
	{
		this.#writeNotRawStringLiteral(expr, CiPriority.PRIMARY);
		this.write(".length()");
	}

	#writeMatchProperty(expr, name)
	{
		this.#startMethodCall(expr.left);
		this.write(name);
		this.write("()");
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.CONSOLE_ERROR:
			this.write("std::cerr");
			break;
		case CiId.LIST_COUNT:
		case CiId.QUEUE_COUNT:
		case CiId.STACK_COUNT:
		case CiId.HASH_SET_COUNT:
		case CiId.SORTED_SET_COUNT:
		case CiId.DICTIONARY_COUNT:
		case CiId.SORTED_DICTIONARY_COUNT:
		case CiId.ORDERED_DICTIONARY_COUNT:
			expr.left.accept(this, CiPriority.PRIMARY);
			this.writeMemberOp(expr.left, expr);
			this.write("size()");
			break;
		case CiId.MATCH_START:
			this.#writeMatchProperty(expr, "position");
			break;
		case CiId.MATCH_END:
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			this.#writeMatchProperty(expr, "position");
			this.write(" + ");
			this.#writeMatchProperty(expr, "length");
			if (parent > CiPriority.ADD)
				this.writeChar(41);
			break;
		case CiId.MATCH_LENGTH:
			this.#writeMatchProperty(expr, "length");
			break;
		case CiId.MATCH_VALUE:
			this.#writeMatchProperty(expr, "str");
			break;
		default:
			super.visitSymbolReference(expr, parent);
			break;
		}
	}

	#writeGtRawPtr(expr)
	{
		this.write(">(");
		if (GenCpp.#isSharedPtr(expr))
			this.writePostfix(expr, ".get()");
		else
			expr.accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	#writeIsVar(expr, def, parent)
	{
		if (def.name != "_") {
			if (parent > CiPriority.ASSIGN)
				this.writeChar(40);
			this.writeName(def);
			this.write(" = ");
		}
		let dynamic;
		if ((dynamic = def.type) instanceof CiDynamicPtrType) {
			this.write("std::dynamic_pointer_cast<");
			this.write(dynamic.class.name);
			this.writeCall(">", expr);
		}
		else {
			this.write("dynamic_cast<");
			this.writeType(def.type, true);
			this.#writeGtRawPtr(expr);
		}
		if (def.name != "_" && parent > CiPriority.ASSIGN)
			this.writeChar(41);
	}

	visitBinaryExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.PLUS:
			if (expr.type.id == CiId.STRING_STORAGE_TYPE) {
				if (parent > CiPriority.ADD)
					this.writeChar(40);
				this.writeStronglyCoerced(expr.type, expr.left);
				this.write(" + ");
				this.writeStronglyCoerced(expr.type, expr.right);
				if (parent > CiPriority.ADD)
					this.writeChar(41);
				return;
			}
			break;
		case CiToken.EQUAL:
		case CiToken.NOT_EQUAL:
		case CiToken.GREATER:
			let str = GenCpp.isStringEmpty(expr);
			if (str != null) {
				if (expr.op != CiToken.EQUAL)
					this.writeChar(33);
				this.writePostfix(str, ".empty()");
				return;
			}
			break;
		case CiToken.ASSIGN:
			let length = GenCpp.isTrimSubstring(expr);
			if (length != null && expr.left.type.id == CiId.STRING_STORAGE_TYPE && parent == CiPriority.STATEMENT) {
				this.writeMethodCall(expr.left, "resize", length);
				return;
			}
			break;
		case CiToken.IS:
			if (expr.right instanceof CiSymbolReference) {
				const symbol = expr.right;
				if (parent >= CiPriority.OR && parent <= CiPriority.MUL)
					this.write("!!");
				this.write("dynamic_cast<const ");
				this.write(symbol.symbol.name);
				this.write(" *");
				this.#writeGtRawPtr(expr.left);
				return;
			}
			else if (expr.right instanceof CiVar) {
				const def = expr.right;
				this.#writeIsVar(expr.left, def, parent);
				return;
			}
			else
				throw new Error();
		default:
			break;
		}
		super.visitBinaryExpr(expr, parent);
	}

	visitLambdaExpr(expr)
	{
		this.write("[](const ");
		this.writeType(expr.first.type, false);
		this.write(" &");
		this.writeName(expr.first);
		this.write(") { ");
		this.writeTemporaries(expr.body);
		this.write("return ");
		expr.body.accept(this, CiPriority.ARGUMENT);
		this.write("; }");
	}

	writeUnreachable(statement)
	{
		this.include("cstdlib");
		this.write("std::");
		super.writeUnreachable(statement);
	}

	writeConst(konst)
	{
		this.write("static constexpr ");
		this.writeTypeAndName(konst);
		this.write(" = ");
		konst.value.accept(this, CiPriority.ARGUMENT);
		this.writeCharLine(59);
	}

	visitForeach(statement)
	{
		let element = statement.getVar();
		this.write("for (");
		if (statement.count() == 2) {
			this.write("const auto &[");
			this.#writeCamelCaseNotKeyword(element.name);
			this.write(", ");
			this.#writeCamelCaseNotKeyword(statement.getValueVar().name);
			this.writeChar(93);
		}
		else {
			if (statement.collection.type.asClassType().getElementType() instanceof CiStorageType) {
				const storage = statement.collection.type.asClassType().getElementType();
				if (!(element.type instanceof CiReadWriteClassType))
					this.write("const ");
				this.write(storage.class.name);
				this.write(" &");
				this.#writeCamelCaseNotKeyword(element.name);
			}
			else if (statement.collection.type.asClassType().getElementType() instanceof CiDynamicPtrType) {
				const dynamic = statement.collection.type.asClassType().getElementType();
				this.write("const ");
				this.writeType(dynamic, true);
				this.write(" &");
				this.#writeCamelCaseNotKeyword(element.name);
			}
			else
				this.writeTypeAndName(element);
		}
		this.write(" : ");
		if (statement.collection.type instanceof CiStringType)
			this.#writeNotRawStringLiteral(statement.collection, CiPriority.ARGUMENT);
		else
			this.#writeCollectionObject(statement.collection, CiPriority.ARGUMENT);
		this.writeChar(41);
		this.writeChild(statement.body);
	}

	embedIfWhileIsVar(expr, write)
	{
		let binary;
		let def;
		if ((binary = expr) instanceof CiBinaryExpr && binary.op == CiToken.IS && (def = binary.right) instanceof CiVar) {
			if (write)
				this.writeType(def.type, true);
			return true;
		}
		return false;
	}

	visitLock(statement)
	{
		this.openBlock();
		this.write("const std::lock_guard<std::recursive_mutex> lock(");
		statement.lock.accept(this, CiPriority.ARGUMENT);
		this.writeLine(");");
		this.flattenBlock(statement.body);
		this.closeBlock();
	}

	writeStronglyCoerced(type, expr)
	{
		if (type.id == CiId.STRING_STORAGE_TYPE && expr.type.id == CiId.STRING_PTR_TYPE && !(expr instanceof CiLiteral)) {
			this.write("std::string(");
			expr.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
		}
		else {
			let call = GenCpp.isStringSubstring(expr);
			if (call != null && type.id == CiId.STRING_STORAGE_TYPE && GenCpp.getStringSubstringPtr(call).type.id != CiId.STRING_STORAGE_TYPE) {
				this.write("std::string(");
				let cast = GenCpp.isUTF8GetString(call);
				if (cast)
					this.write("reinterpret_cast<const char *>(");
				this.writeStringPtrAdd(call);
				if (cast)
					this.writeChar(41);
				this.write(", ");
				GenCpp.getStringSubstringLength(call).accept(this, CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			else
				super.writeStronglyCoerced(type, expr);
		}
	}

	writeSwitchCaseCond(statement, value, parent)
	{
		let def;
		if ((def = value) instanceof CiVar) {
			if (parent == CiPriority.ARGUMENT && def.name != "_")
				this.writeType(def.type, true);
			this.#writeIsVar(statement.value, def, parent);
		}
		else
			super.writeSwitchCaseCond(statement, value, parent);
	}

	static #isIsVar(expr)
	{
		let binary;
		return (binary = expr) instanceof CiBinaryExpr && binary.op == CiToken.IS && binary.right instanceof CiVar;
	}

	#hasVariables(statement)
	{
		if (statement instanceof CiVar)
			return true;
		else if (statement instanceof CiAssert) {
			const asrt = statement;
			return GenCpp.#isIsVar(asrt.cond);
		}
		else if (statement instanceof CiBlock || statement instanceof CiBreak || statement instanceof CiConst || statement instanceof CiContinue || statement instanceof CiLock || statement instanceof CiNative || statement instanceof CiThrow)
			return false;
		else if (statement instanceof CiIf) {
			const ifStatement = statement;
			return GenCpp.hasTemporaries(ifStatement.cond) && !GenCpp.#isIsVar(ifStatement.cond);
		}
		else if (statement instanceof CiLoop) {
			const loop = statement;
			return loop.cond != null && GenCpp.hasTemporaries(loop.cond);
		}
		else if (statement instanceof CiReturn) {
			const ret = statement;
			return ret.value != null && GenCpp.hasTemporaries(ret.value);
		}
		else if (statement instanceof CiSwitch) {
			const switch_ = statement;
			return GenCpp.hasTemporaries(switch_.value);
		}
		else if (statement instanceof CiExpr) {
			const expr = statement;
			return GenCpp.hasTemporaries(expr);
		}
		else
			throw new Error();
	}

	writeSwitchCaseBody(statements)
	{
		let block = false;
		for (const statement of statements) {
			if (!block && this.#hasVariables(statement)) {
				this.openBlock();
				block = true;
			}
			statement.acceptStatement(this);
		}
		if (block)
			this.closeBlock();
	}

	visitSwitch(statement)
	{
		if (statement.isTypeMatching())
			this.writeSwitchAsIfsWithGoto(statement);
		else
			super.visitSwitch(statement);
	}

	visitThrow(statement)
	{
		this.include("exception");
		this.writeLine("throw std::exception();");
	}

	#openNamespace()
	{
		if (this.namespace.length == 0)
			return;
		this.writeNewLine();
		this.write("namespace ");
		this.writeLine(this.namespace);
		this.writeCharLine(123);
	}

	#closeNamespace()
	{
		if (this.namespace.length != 0)
			this.writeCharLine(125);
	}

	writeEnum(enu)
	{
		this.writeNewLine();
		this.writeDoc(enu.documentation);
		this.write("enum class ");
		this.writeLine(enu.name);
		this.openBlock();
		enu.acceptValues(this);
		this.writeNewLine();
		this.indent--;
		this.writeLine("};");
		if (enu instanceof CiEnumFlags) {
			this.include("type_traits");
			this.#hasEnumFlags = true;
			this.write("CI_ENUM_FLAG_OPERATORS(");
			this.write(enu.name);
			this.writeCharLine(41);
		}
	}

	static #getConstructorVisibility(klass)
	{
		switch (klass.callType) {
		case CiCallType.STATIC:
			return CiVisibility.PRIVATE;
		case CiCallType.ABSTRACT:
			return CiVisibility.PROTECTED;
		default:
			return CiVisibility.PUBLIC;
		}
	}

	static #hasMembersOfVisibility(klass, visibility)
	{
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let member;
			if ((member = symbol) instanceof CiMember && member.visibility == visibility)
				return true;
		}
		return false;
	}

	writeField(field)
	{
		this.writeDoc(field.documentation);
		this.writeVar(field);
		this.writeCharLine(59);
	}

	#writeParametersAndConst(method, defaultArguments)
	{
		this.writeParameters(method, defaultArguments);
		if (method.callType != CiCallType.STATIC && !method.isMutator)
			this.write(" const");
	}

	#writeDeclarations(klass, visibility, visibilityKeyword)
	{
		let constructor = GenCpp.#getConstructorVisibility(klass) == visibility;
		let destructor = visibility == CiVisibility.PUBLIC && (klass.hasSubclasses || klass.addsVirtualMethods());
		if (!constructor && !destructor && !GenCpp.#hasMembersOfVisibility(klass, visibility))
			return;
		this.write(visibilityKeyword);
		this.writeCharLine(58);
		this.indent++;
		if (constructor) {
			if (klass.constructor_ != null)
				this.writeDoc(klass.constructor_.documentation);
			this.write(klass.name);
			this.write("()");
			if (klass.callType == CiCallType.STATIC)
				this.write(" = delete");
			else if (!this.needsConstructor(klass))
				this.write(" = default");
			this.writeCharLine(59);
		}
		if (destructor) {
			this.write("virtual ~");
			this.write(klass.name);
			this.writeLine("() = default;");
		}
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			let member;
			if (!((member = symbol) instanceof CiMember) || member.visibility != visibility)
				continue;
			if (member instanceof CiConst) {
				const konst = member;
				this.writeDoc(konst.documentation);
				this.writeConst(konst);
			}
			else if (member instanceof CiField) {
				const field = member;
				this.writeField(field);
			}
			else if (member instanceof CiMethod) {
				const method = member;
				this.writeMethodDoc(method);
				switch (method.callType) {
				case CiCallType.STATIC:
					this.write("static ");
					break;
				case CiCallType.ABSTRACT:
				case CiCallType.VIRTUAL:
					this.write("virtual ");
					break;
				default:
					break;
				}
				this.writeTypeAndName(method);
				this.#writeParametersAndConst(method, true);
				switch (method.callType) {
				case CiCallType.ABSTRACT:
					this.write(" = 0");
					break;
				case CiCallType.OVERRIDE:
					this.write(" override");
					break;
				case CiCallType.SEALED:
					this.write(" final");
					break;
				default:
					break;
				}
				this.writeCharLine(59);
			}
			else
				throw new Error();
		}
		this.indent--;
	}

	writeClassInternal(klass)
	{
		this.writeNewLine();
		this.writeDoc(klass.documentation);
		this.openClass(klass, klass.callType == CiCallType.SEALED ? " final" : "", " : public ");
		this.indent--;
		this.#writeDeclarations(klass, CiVisibility.PUBLIC, "public");
		this.#writeDeclarations(klass, CiVisibility.PROTECTED, "protected");
		this.#writeDeclarations(klass, CiVisibility.INTERNAL, "public");
		this.#writeDeclarations(klass, CiVisibility.PRIVATE, "private");
		this.writeLine("};");
	}

	#writeConstructor(klass)
	{
		if (!this.needsConstructor(klass))
			return;
		this.switchesWithGoto.length = 0;
		this.write(klass.name);
		this.write("::");
		this.write(klass.name);
		this.writeLine("()");
		this.openBlock();
		this.writeConstructorBody(klass);
		this.closeBlock();
	}

	writeMethod(method)
	{
		if (method.callType == CiCallType.ABSTRACT)
			return;
		this.switchesWithGoto.length = 0;
		this.writeNewLine();
		this.writeType(method.type, true);
		this.writeChar(32);
		this.write(method.parent.name);
		this.write("::");
		this.#writeCamelCaseNotKeyword(method.name);
		this.#writeParametersAndConst(method, false);
		this.writeBody(method);
	}

	#writeResources(resources, define)
	{
		if (Object.keys(resources).length == 0)
			return;
		this.writeNewLine();
		this.writeLine("namespace");
		this.openBlock();
		this.writeLine("namespace CiResource");
		this.openBlock();
		for (const [name, content] of Object.entries(resources).sort((a, b) => a[0].localeCompare(b[0]))) {
			if (!define)
				this.write("extern ");
			this.include("array");
			this.include("cstdint");
			this.write("const std::array<uint8_t, ");
			this.visitLiteralLong(BigInt(content.length));
			this.write("> ");
			this.writeResourceName(name);
			if (define) {
				this.writeLine(" = {");
				this.writeChar(9);
				this.writeBytes(content);
				this.write(" }");
			}
			this.writeCharLine(59);
		}
		this.closeBlock();
		this.closeBlock();
	}

	writeProgram(program)
	{
		this.writtenClasses.clear();
		this.inHeaderFile = true;
		this.#usingStringViewLiterals = false;
		this.#hasEnumFlags = false;
		this.#stringReplace = false;
		this.openStringWriter();
		this.#openNamespace();
		this.writeRegexOptionsEnum(program);
		for (let type = program.first; type != null; type = type.next) {
			let enu;
			if ((enu = type) instanceof CiEnum)
				this.writeEnum(enu);
			else {
				this.write("class ");
				this.write(type.name);
				this.writeCharLine(59);
			}
		}
		for (const klass of program.classes)
			this.writeClass(klass, program);
		this.#closeNamespace();
		this.createHeaderFile(".hpp");
		if (this.#hasEnumFlags) {
			this.writeLine("#define CI_ENUM_FLAG_OPERATORS(T) \\");
			this.writeLine("\tinline constexpr T operator~(T a) { return static_cast<T>(~static_cast<std::underlying_type_t<T>>(a)); } \\");
			this.writeLine("\tinline constexpr T operator&(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) & static_cast<std::underlying_type_t<T>>(b)); } \\");
			this.writeLine("\tinline constexpr T operator|(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) | static_cast<std::underlying_type_t<T>>(b)); } \\");
			this.writeLine("\tinline constexpr T operator^(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) ^ static_cast<std::underlying_type_t<T>>(b)); } \\");
			this.writeLine("\tinline constexpr T &operator&=(T &a, T b) { return (a = a & b); } \\");
			this.writeLine("\tinline constexpr T &operator|=(T &a, T b) { return (a = a | b); } \\");
			this.writeLine("\tinline constexpr T &operator^=(T &a, T b) { return (a = a ^ b); }");
		}
		this.closeStringWriter();
		this.closeFile();
		this.inHeaderFile = false;
		this.openStringWriter();
		this.#writeResources(program.resources, false);
		this.#openNamespace();
		for (const klass of program.classes) {
			this.#writeConstructor(klass);
			this.writeMethods(klass);
		}
		this.#writeResources(program.resources, true);
		this.#closeNamespace();
		if (this.#stringReplace) {
			this.include("string");
			this.include("string_view");
		}
		this.createImplementationFile(program, ".hpp");
		if (this.#usingStringViewLiterals)
			this.writeLine("using namespace std::string_view_literals;");
		if (this.#stringReplace) {
			this.writeNewLine();
			this.writeLine("static std::string CiString_replace(std::string_view s, std::string_view oldValue, std::string_view newValue)");
			this.openBlock();
			this.writeLine("std::string result;");
			this.writeLine("result.reserve(s.size());");
			this.writeLine("for (std::string_view::size_type i = 0;;) {");
			this.writeLine("\tauto j = s.find(oldValue, i);");
			this.writeLine("\tif (j == std::string::npos) {");
			this.writeLine("\t\tresult.append(s, i);");
			this.writeLine("\t\treturn result;");
			this.writeLine("\t}");
			this.writeLine("\tresult.append(s, i, j - i);");
			this.writeLine("\tresult.append(newValue);");
			this.writeLine("\ti = j + oldValue.size();");
			this.writeCharLine(125);
			this.closeBlock();
		}
		this.closeStringWriter();
		this.closeFile();
	}
}

export class GenCs extends GenTyped
{

	getTargetName()
	{
		return "C++";
	}

	startDocLine()
	{
		this.write("/// ");
	}

	writeDocPara(para, many)
	{
		if (many) {
			this.writeNewLine();
			this.write("/// <para>");
		}
		for (const inline of para.children) {
			if (inline instanceof CiDocText) {
				const text = inline;
				this.writeXmlDoc(text.text);
			}
			else if (inline instanceof CiDocCode) {
				const code = inline;
				switch (code.text) {
				case "true":
				case "false":
				case "null":
					this.write("<see langword=\"");
					this.write(code.text);
					this.write("\" />");
					break;
				default:
					this.write("<c>");
					this.writeXmlDoc(code.text);
					this.write("</c>");
					break;
				}
			}
			else if (inline instanceof CiDocLine) {
				this.writeNewLine();
				this.startDocLine();
			}
			else
				throw new Error();
		}
		if (many)
			this.write("</para>");
	}

	writeDocList(list)
	{
		this.writeNewLine();
		this.writeLine("/// <list type=\"bullet\">");
		for (const item of list.items) {
			this.write("/// <item>");
			this.writeDocPara(item, false);
			this.writeLine("</item>");
		}
		this.write("/// </list>");
	}

	writeDoc(doc)
	{
		if (doc == null)
			return;
		this.write("/// <summary>");
		this.writeDocPara(doc.summary, false);
		this.writeLine("</summary>");
		if (doc.details.length > 0) {
			this.write("/// <remarks>");
			if (doc.details.length == 1)
				this.writeDocBlock(doc.details[0], false);
			else {
				for (const block of doc.details)
					this.writeDocBlock(block, true);
			}
			this.writeLine("</remarks>");
		}
	}

	writeName(symbol)
	{
		let konst;
		if ((konst = symbol) instanceof CiConst && konst.inMethod != null)
			this.write(konst.inMethod.name);
		this.write(symbol.name);
		switch (symbol.name) {
		case "as":
		case "await":
		case "catch":
		case "char":
		case "checked":
		case "decimal":
		case "delegate":
		case "event":
		case "explicit":
		case "extern":
		case "finally":
		case "fixed":
		case "goto":
		case "implicit":
		case "interface":
		case "is":
		case "lock":
		case "namespace":
		case "object":
		case "operator":
		case "out":
		case "params":
		case "private":
		case "readonly":
		case "ref":
		case "sbyte":
		case "sizeof":
		case "stackalloc":
		case "struct":
		case "try":
		case "typeof":
		case "ulong":
		case "unchecked":
		case "unsafe":
		case "using":
		case "volatile":
			this.writeChar(95);
			break;
		default:
			break;
		}
	}

	getLiteralChars()
	{
		return 65536;
	}

	#writeVisibility(visibility)
	{
		switch (visibility) {
		case CiVisibility.PRIVATE:
			break;
		case CiVisibility.INTERNAL:
			this.write("internal ");
			break;
		case CiVisibility.PROTECTED:
			this.write("protected ");
			break;
		case CiVisibility.PUBLIC:
			this.write("public ");
			break;
		default:
			throw new Error();
		}
	}

	#writeCallType(callType, sealedString)
	{
		switch (callType) {
		case CiCallType.STATIC:
			this.write("static ");
			break;
		case CiCallType.NORMAL:
			break;
		case CiCallType.ABSTRACT:
			this.write("abstract ");
			break;
		case CiCallType.VIRTUAL:
			this.write("virtual ");
			break;
		case CiCallType.OVERRIDE:
			this.write("override ");
			break;
		case CiCallType.SEALED:
			this.write(sealedString);
			break;
		}
	}

	#writeElementType(elementType)
	{
		this.include("System.Collections.Generic");
		this.writeChar(60);
		this.writeType(elementType, false);
		this.writeChar(62);
	}

	writeType(type, promote)
	{
		if (type instanceof CiIntegerType) {
			switch (this.getTypeId(type, promote)) {
			case CiId.S_BYTE_RANGE:
				this.write("sbyte");
				break;
			case CiId.BYTE_RANGE:
				this.write("byte");
				break;
			case CiId.SHORT_RANGE:
				this.write("short");
				break;
			case CiId.U_SHORT_RANGE:
				this.write("ushort");
				break;
			case CiId.INT_TYPE:
				this.write("int");
				break;
			case CiId.LONG_TYPE:
				this.write("long");
				break;
			default:
				throw new Error();
			}
		}
		else if (type instanceof CiClassType) {
			const klass = type;
			switch (klass.class.id) {
			case CiId.STRING_CLASS:
				this.write("string");
				break;
			case CiId.ARRAY_PTR_CLASS:
			case CiId.ARRAY_STORAGE_CLASS:
				this.writeType(klass.getElementType(), false);
				this.write("[]");
				break;
			case CiId.LIST_CLASS:
			case CiId.QUEUE_CLASS:
			case CiId.STACK_CLASS:
			case CiId.HASH_SET_CLASS:
			case CiId.SORTED_SET_CLASS:
				this.write(klass.class.name);
				this.#writeElementType(klass.getElementType());
				break;
			case CiId.DICTIONARY_CLASS:
			case CiId.SORTED_DICTIONARY_CLASS:
				this.include("System.Collections.Generic");
				this.write(klass.class.name);
				this.writeChar(60);
				this.writeType(klass.getKeyType(), false);
				this.write(", ");
				this.writeType(klass.getValueType(), false);
				this.writeChar(62);
				break;
			case CiId.ORDERED_DICTIONARY_CLASS:
				this.include("System.Collections.Specialized");
				this.write("OrderedDictionary");
				break;
			case CiId.TEXT_WRITER_CLASS:
			case CiId.STRING_WRITER_CLASS:
				this.include("System.IO");
				this.write(klass.class.name);
				break;
			case CiId.REGEX_CLASS:
			case CiId.MATCH_CLASS:
				this.include("System.Text.RegularExpressions");
				this.write(klass.class.name);
				break;
			case CiId.LOCK_CLASS:
				this.write("object");
				break;
			default:
				this.write(klass.class.name);
				break;
			}
		}
		else
			this.write(type.name);
	}

	writeNewWithFields(type, init)
	{
		this.write("new ");
		this.writeType(type, false);
		this.writeObjectLiteral(init, " = ");
	}

	writeCoercedLiteral(type, expr)
	{
		let range;
		if (expr instanceof CiLiteralChar && (range = type) instanceof CiRangeType && range.max <= 255)
			this.writeStaticCast(type, expr);
		else
			super.writeCoercedLiteral(type, expr);
	}

	isPromoted(expr)
	{
		return super.isPromoted(expr) || expr instanceof CiLiteralChar;
	}

	visitInterpolatedString(expr, parent)
	{
		this.write("$\"");
		for (const part of expr.parts) {
			this.writeDoubling(part.prefix, 123);
			this.writeChar(123);
			part.argument.accept(this, CiPriority.ARGUMENT);
			if (part.widthExpr != null) {
				this.writeChar(44);
				this.visitLiteralLong(BigInt(part.width));
			}
			if (part.format != 32) {
				this.writeChar(58);
				this.writeChar(part.format);
				if (part.precision >= 0)
					this.visitLiteralLong(BigInt(part.precision));
			}
			this.writeChar(125);
		}
		this.writeDoubling(expr.suffix, 123);
		this.writeChar(34);
	}

	writeNewArray(elementType, lengthExpr, parent)
	{
		this.write("new ");
		this.writeType(elementType.getBaseType(), false);
		this.writeChar(91);
		lengthExpr.accept(this, CiPriority.ARGUMENT);
		this.writeChar(93);
		let array;
		while ((array = elementType) instanceof CiClassType && array.isArray()) {
			this.write("[]");
			elementType = array.getElementType();
		}
	}

	writeNew(klass, parent)
	{
		this.write("new ");
		this.writeType(klass, false);
		this.write("()");
	}

	hasInitCode(def)
	{
		let array;
		return (array = def.type) instanceof CiArrayStorageType && array.getElementType() instanceof CiStorageType;
	}

	writeInitCode(def)
	{
		if (!this.hasInitCode(def))
			return;
		let array = def.type;
		let nesting = 0;
		let innerArray;
		while ((innerArray = array.getElementType()) instanceof CiArrayStorageType) {
			this.openLoop("int", nesting++, array.length);
			this.writeArrayElement(def, nesting);
			this.write(" = ");
			this.writeNewArray(innerArray.getElementType(), innerArray.lengthExpr, CiPriority.ARGUMENT);
			this.writeCharLine(59);
			array = innerArray;
		}
		let klass;
		if ((klass = array.getElementType()) instanceof CiStorageType) {
			this.openLoop("int", nesting++, array.length);
			this.writeArrayElement(def, nesting);
			this.write(" = ");
			this.writeNew(klass, CiPriority.ARGUMENT);
			this.writeCharLine(59);
		}
		while (--nesting >= 0)
			this.closeBlock();
	}

	writeResource(name, length)
	{
		this.write("CiResource.");
		this.writeResourceName(name);
	}

	writeStringLength(expr)
	{
		this.writePostfix(expr, ".Length");
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.CONSOLE_ERROR:
			this.include("System");
			this.write("Console.Error");
			break;
		case CiId.MATCH_START:
			this.writePostfix(expr.left, ".Index");
			break;
		case CiId.MATCH_END:
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			this.writePostfix(expr.left, ".Index + ");
			this.writeStringLength(expr.left);
			if (parent > CiPriority.ADD)
				this.writeChar(41);
			break;
		case CiId.MATH_NA_N:
		case CiId.MATH_NEGATIVE_INFINITY:
		case CiId.MATH_POSITIVE_INFINITY:
			this.write("float.");
			this.write(expr.symbol.name);
			break;
		default:
			let forEach;
			let dict;
			if ((forEach = expr.symbol.parent) instanceof CiForeach && (dict = forEach.collection.type) instanceof CiClassType && dict.class.id == CiId.ORDERED_DICTIONARY_CLASS) {
				if (parent == CiPriority.PRIMARY)
					this.writeChar(40);
				let element = forEach.getVar();
				if (expr.symbol == element) {
					this.writeStaticCastType(dict.getKeyType());
					this.writeName(element);
					this.write(".Key");
				}
				else {
					this.writeStaticCastType(dict.getValueType());
					this.writeName(element);
					this.write(".Value");
				}
				if (parent == CiPriority.PRIMARY)
					this.writeChar(41);
			}
			else
				super.visitSymbolReference(expr, parent);
			break;
		}
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.ENUM_FROM_INT:
			this.writeStaticCast(method.type, args[0]);
			break;
		case CiId.INT_TRY_PARSE:
		case CiId.LONG_TRY_PARSE:
		case CiId.DOUBLE_TRY_PARSE:
			this.write(obj.type.name);
			this.write(".TryParse(");
			args[0].accept(this, CiPriority.ARGUMENT);
			if (args.length == 2) {
				let radix;
				if (!((radix = args[1]) instanceof CiLiteralLong) || radix.value != 16)
					this.notSupported(args[1], "Radix");
				this.include("System.Globalization");
				this.write(", NumberStyles.HexNumber, null");
			}
			this.write(", out ");
			obj.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.STRING_INDEX_OF:
		case CiId.STRING_LAST_INDEX_OF:
			obj.accept(this, CiPriority.PRIMARY);
			this.writeChar(46);
			this.write(method.name);
			this.writeChar(40);
			let c = this.getOneAscii(args[0]);
			if (c >= 0)
				this.visitLiteralChar(c);
			else
				args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.ARRAY_BINARY_SEARCH_ALL:
		case CiId.ARRAY_BINARY_SEARCH_PART:
			this.include("System");
			this.write("Array.BinarySearch(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			if (args.length == 3) {
				args[1].accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				args[2].accept(this, CiPriority.ARGUMENT);
				this.write(", ");
			}
			this.writeNotPromoted(obj.type.asClassType().getElementType(), args[0]);
			this.writeChar(41);
			break;
		case CiId.ARRAY_CONTAINS:
			this.include("System.Linq");
			this.writeMethodCall(obj, "Contains", args[0]);
			break;
		case CiId.ARRAY_COPY_TO:
			this.include("System");
			this.write("Array.Copy(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeArgs(method, args);
			this.writeChar(41);
			break;
		case CiId.ARRAY_FILL_ALL:
		case CiId.ARRAY_FILL_PART:
			this.include("System");
			let literal;
			if ((literal = args[0]) instanceof CiLiteral && literal.isDefaultValue()) {
				this.write("Array.Clear(");
				obj.accept(this, CiPriority.ARGUMENT);
				if (args.length == 1) {
					this.write(", 0, ");
					this.writeArrayStorageLength(obj);
				}
			}
			else {
				this.write("Array.Fill(");
				obj.accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				this.writeNotPromoted(obj.type.asClassType().getElementType(), args[0]);
			}
			if (args.length == 3) {
				this.write(", ");
				args[1].accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				args[2].accept(this, CiPriority.ARGUMENT);
			}
			this.writeChar(41);
			break;
		case CiId.ARRAY_SORT_ALL:
			this.include("System");
			this.writeCall("Array.Sort", obj);
			break;
		case CiId.ARRAY_SORT_PART:
			this.include("System");
			this.writeCall("Array.Sort", obj, args[0], args[1]);
			break;
		case CiId.LIST_ADD:
			this.writeListAdd(obj, "Add", args);
			break;
		case CiId.LIST_ALL:
			this.include("System.Linq");
			this.writeMethodCall(obj, "All", args[0]);
			break;
		case CiId.LIST_ANY:
			this.include("System.Linq");
			this.writeMethodCall(obj, "Any", args[0]);
			break;
		case CiId.LIST_INSERT:
			this.writeListInsert(obj, "Insert", args);
			break;
		case CiId.LIST_LAST:
			this.writePostfix(obj, "[^1]");
			break;
		case CiId.LIST_SORT_PART:
			this.writePostfix(obj, ".Sort(");
			this.writeArgs(method, args);
			this.write(", null)");
			break;
		case CiId.DICTIONARY_ADD:
			this.writePostfix(obj, ".Add(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeNewStorage(obj.type.asClassType().getValueType());
			this.writeChar(41);
			break;
		case CiId.ORDERED_DICTIONARY_CONTAINS_KEY:
			this.writeMethodCall(obj, "Contains", args[0]);
			break;
		case CiId.TEXT_WRITER_WRITE:
		case CiId.TEXT_WRITER_WRITE_LINE:
		case CiId.CONSOLE_WRITE:
		case CiId.CONSOLE_WRITE_LINE:
			this.include("System");
			obj.accept(this, CiPriority.PRIMARY);
			this.writeChar(46);
			this.write(method.name);
			this.writeChar(40);
			if (args.length != 0) {
				if (args[0] instanceof CiLiteralChar) {
					this.write("(int) ");
					args[0].accept(this, CiPriority.PRIMARY);
				}
				else
					args[0].accept(this, CiPriority.ARGUMENT);
			}
			this.writeChar(41);
			break;
		case CiId.STRING_WRITER_CLEAR:
			this.writePostfix(obj, ".GetStringBuilder().Clear()");
			break;
		case CiId.TEXT_WRITER_WRITE_CHAR:
			this.writeCharMethodCall(obj, "Write", args[0]);
			break;
		case CiId.TEXT_WRITER_WRITE_CODE_POINT:
			this.writePostfix(obj, ".Write(");
			let literalChar;
			if ((literalChar = args[0]) instanceof CiLiteralChar && literalChar.value < 65536)
				args[0].accept(this, CiPriority.ARGUMENT);
			else {
				this.include("System.Text");
				this.writeCall("new Rune", args[0]);
			}
			this.writeChar(41);
			break;
		case CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE:
			this.include("System");
			obj.accept(this, CiPriority.PRIMARY);
			this.writeChar(46);
			this.write(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			this.include("System.Text");
			this.write("Encoding.UTF8.GetByteCount(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.U_T_F8_GET_BYTES:
			this.include("System.Text");
			this.write("Encoding.UTF8.GetBytes(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", 0, ");
			this.writePostfix(args[0], ".Length, ");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			args[2].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.U_T_F8_GET_STRING:
			this.include("System.Text");
			this.write("Encoding.UTF8.GetString");
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.REGEX_COMPILE:
			this.include("System.Text.RegularExpressions");
			this.write("new Regex");
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.REGEX_ESCAPE:
		case CiId.REGEX_IS_MATCH_STR:
		case CiId.REGEX_IS_MATCH_REGEX:
			this.include("System.Text.RegularExpressions");
			obj.accept(this, CiPriority.PRIMARY);
			this.writeChar(46);
			this.write(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATCH_FIND_STR:
			this.include("System.Text.RegularExpressions");
			this.writeChar(40);
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = Regex.Match");
			this.writeArgsInParentheses(method, args);
			this.write(").Success");
			break;
		case CiId.MATCH_FIND_REGEX:
			this.include("System.Text.RegularExpressions");
			this.writeChar(40);
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			this.writeMethodCall(args[1], "Match", args[0]);
			this.write(").Success");
			break;
		case CiId.MATCH_GET_CAPTURE:
			this.writePostfix(obj, ".Groups[");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write("].Value");
			break;
		case CiId.MATH_METHOD:
		case CiId.MATH_ABS:
		case CiId.MATH_CEILING:
		case CiId.MATH_CLAMP:
		case CiId.MATH_FUSED_MULTIPLY_ADD:
		case CiId.MATH_LOG2:
		case CiId.MATH_MAX_INT:
		case CiId.MATH_MAX_DOUBLE:
		case CiId.MATH_MIN_INT:
		case CiId.MATH_MIN_DOUBLE:
		case CiId.MATH_ROUND:
		case CiId.MATH_TRUNCATE:
			this.include("System");
			this.write("Math.");
			this.write(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_IS_FINITE:
		case CiId.MATH_IS_INFINITY:
		case CiId.MATH_IS_NA_N:
			this.write("double.");
			this.writeCall(method.name, args[0]);
			break;
		default:
			if (obj != null) {
				obj.accept(this, CiPriority.PRIMARY);
				this.writeChar(46);
			}
			this.writeName(method);
			this.writeArgsInParentheses(method, args);
			break;
		}
	}

	#writeOrderedDictionaryIndexing(expr)
	{
		if (expr.right.type.id == CiId.INT_TYPE || expr.right.type instanceof CiRangeType) {
			this.writePostfix(expr.left, "[(object) ");
			expr.right.accept(this, CiPriority.PRIMARY);
			this.writeChar(93);
		}
		else
			super.writeIndexingExpr(expr, CiPriority.AND);
	}

	writeIndexingExpr(expr, parent)
	{
		let dict;
		if ((dict = expr.left.type) instanceof CiClassType && dict.class.id == CiId.ORDERED_DICTIONARY_CLASS) {
			if (parent == CiPriority.PRIMARY)
				this.writeChar(40);
			this.writeStaticCastType(expr.type);
			this.#writeOrderedDictionaryIndexing(expr);
			if (parent == CiPriority.PRIMARY)
				this.writeChar(41);
		}
		else
			super.writeIndexingExpr(expr, parent);
	}

	writeAssign(expr, parent)
	{
		let indexing;
		let dict;
		if ((indexing = expr.left) instanceof CiBinaryExpr && indexing.op == CiToken.LEFT_BRACKET && (dict = indexing.left.type) instanceof CiClassType && dict.class.id == CiId.ORDERED_DICTIONARY_CLASS) {
			this.#writeOrderedDictionaryIndexing(indexing);
			this.write(" = ");
			this.writeAssignRight(expr);
		}
		else
			super.writeAssign(expr, parent);
	}

	visitBinaryExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.AND_ASSIGN:
		case CiToken.OR_ASSIGN:
		case CiToken.XOR_ASSIGN:
			if (parent > CiPriority.ASSIGN)
				this.writeChar(40);
			expr.left.accept(this, CiPriority.ASSIGN);
			this.writeChar(32);
			this.write(expr.getOpString());
			this.writeChar(32);
			this.writeAssignRight(expr);
			if (parent > CiPriority.ASSIGN)
				this.writeChar(41);
			break;
		default:
			super.visitBinaryExpr(expr, parent);
			break;
		}
	}

	visitLambdaExpr(expr)
	{
		this.writeName(expr.first);
		this.write(" => ");
		expr.body.accept(this, CiPriority.STATEMENT);
	}

	defineObjectLiteralTemporary(expr)
	{
	}

	defineIsVar(binary)
	{
	}

	writeAssert(statement)
	{
		if (statement.completesNormally()) {
			this.include("System.Diagnostics");
			this.write("Debug.Assert(");
			statement.cond.accept(this, CiPriority.ARGUMENT);
			if (statement.message != null) {
				this.write(", ");
				statement.message.accept(this, CiPriority.ARGUMENT);
			}
		}
		else {
			this.include("System");
			this.write("throw new NotImplementedException(");
			if (statement.message != null)
				statement.message.accept(this, CiPriority.ARGUMENT);
		}
		this.writeLine(");");
	}

	visitForeach(statement)
	{
		this.write("foreach (");
		let dict;
		if ((dict = statement.collection.type) instanceof CiClassType && dict.class.typeParameterCount == 2) {
			if (dict.class.id == CiId.ORDERED_DICTIONARY_CLASS) {
				this.include("System.Collections");
				this.write("DictionaryEntry ");
				this.writeName(statement.getVar());
			}
			else {
				this.writeChar(40);
				this.writeTypeAndName(statement.getVar());
				this.write(", ");
				this.writeTypeAndName(statement.getValueVar());
				this.writeChar(41);
			}
		}
		else
			this.writeTypeAndName(statement.getVar());
		this.write(" in ");
		statement.collection.accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
		this.writeChild(statement.body);
	}

	visitLock(statement)
	{
		this.writeCall("lock ", statement.lock);
		this.writeChild(statement.body);
	}

	visitThrow(statement)
	{
		this.include("System");
		this.write("throw new Exception(");
		statement.message.accept(this, CiPriority.ARGUMENT);
		this.writeLine(");");
	}

	writeEnum(enu)
	{
		this.writeNewLine();
		this.writeDoc(enu.documentation);
		if (enu instanceof CiEnumFlags) {
			this.include("System");
			this.writeLine("[Flags]");
		}
		this.writePublic(enu);
		this.write("enum ");
		this.writeLine(enu.name);
		this.openBlock();
		enu.acceptValues(this);
		this.writeNewLine();
		this.closeBlock();
	}

	writeRegexOptionsEnum(program)
	{
		if (program.regexOptionsEnum)
			this.include("System.Text.RegularExpressions");
	}

	writeConst(konst)
	{
		this.writeNewLine();
		this.writeDoc(konst.documentation);
		this.#writeVisibility(konst.visibility);
		this.write(konst.type instanceof CiArrayStorageType ? "static readonly " : "const ");
		this.writeTypeAndName(konst);
		this.write(" = ");
		this.writeCoercedExpr(konst.type, konst.value);
		this.writeCharLine(59);
	}

	writeField(field)
	{
		this.writeNewLine();
		this.writeDoc(field.documentation);
		this.#writeVisibility(field.visibility);
		if (field.type.isFinal() && !field.isAssignableStorage())
			this.write("readonly ");
		this.writeVar(field);
		this.writeCharLine(59);
	}

	writeParameterDoc(param, first)
	{
		this.write("/// <param name=\"");
		this.writeName(param);
		this.write("\">");
		this.writeDocPara(param.documentation.summary, false);
		this.writeLine("</param>");
	}

	writeMethod(method)
	{
		if (method.id == CiId.CLASS_TO_STRING && method.callType == CiCallType.ABSTRACT)
			return;
		this.writeNewLine();
		this.writeDoc(method.documentation);
		this.writeParametersDoc(method);
		this.#writeVisibility(method.visibility);
		if (method.id == CiId.CLASS_TO_STRING)
			this.write("override ");
		else
			this.#writeCallType(method.callType, "sealed override ");
		this.writeTypeAndName(method);
		this.writeParameters(method, true);
		let ret;
		if ((ret = method.body) instanceof CiReturn) {
			this.write(" => ");
			this.writeCoerced(method.type, ret.value, CiPriority.ARGUMENT);
			this.writeCharLine(59);
		}
		else
			this.writeBody(method);
	}

	writeClass(klass, program)
	{
		this.writeNewLine();
		this.writeDoc(klass.documentation);
		this.writePublic(klass);
		this.#writeCallType(klass.callType, "sealed ");
		this.openClass(klass, "", " : ");
		if (this.needsConstructor(klass)) {
			if (klass.constructor_ != null) {
				this.writeDoc(klass.constructor_.documentation);
				this.#writeVisibility(klass.constructor_.visibility);
			}
			else
				this.write("internal ");
			this.write(klass.name);
			this.writeLine("()");
			this.openBlock();
			this.writeConstructorBody(klass);
			this.closeBlock();
		}
		this.writeMembers(klass, true);
		this.closeBlock();
	}

	#writeResources(resources)
	{
		this.writeNewLine();
		this.writeLine("internal static class CiResource");
		this.openBlock();
		for (const [name, content] of Object.entries(resources).sort((a, b) => a[0].localeCompare(b[0]))) {
			this.write("internal static readonly byte[] ");
			this.writeResourceName(name);
			this.writeLine(" = {");
			this.writeChar(9);
			this.writeBytes(content);
			this.writeLine(" };");
		}
		this.closeBlock();
	}

	writeProgram(program)
	{
		this.openStringWriter();
		if (this.namespace.length != 0) {
			this.write("namespace ");
			this.writeLine(this.namespace);
			this.openBlock();
		}
		this.writeTopLevelNatives(program);
		this.writeTypes(program);
		if (Object.keys(program.resources).length > 0)
			this.#writeResources(program.resources);
		if (this.namespace.length != 0)
			this.closeBlock();
		this.createOutputFile();
		this.writeIncludes("using ", ";");
		this.closeStringWriter();
		this.closeFile();
	}
}

export class GenD extends GenCCppD
{
	#hasListInsert;
	#hasListRemoveAt;
	#hasQueueDequeue;
	#hasStackPop;
	#hasSortedDictionaryInsert;
	#hasSortedDictionaryFind;

	getTargetName()
	{
		return "D";
	}

	startDocLine()
	{
		this.write("/// ");
	}

	writeDocPara(para, many)
	{
		if (many) {
			this.writeNewLine();
			this.startDocLine();
		}
		for (const inline of para.children) {
			if (inline instanceof CiDocText) {
				const text = inline;
				this.writeXmlDoc(text.text);
			}
			else if (inline instanceof CiDocCode) {
				const code = inline;
				this.writeChar(96);
				this.writeXmlDoc(code.text);
				this.writeChar(96);
			}
			else if (inline instanceof CiDocLine) {
				this.writeNewLine();
				this.startDocLine();
			}
			else
				throw new Error();
		}
		if (many)
			this.writeNewLine();
	}

	writeParameterDoc(param, first)
	{
		if (first) {
			this.startDocLine();
			this.writeLine("Params:");
		}
		this.startDocLine();
		this.writeName(param);
		this.write(" = ");
		this.writeDocPara(param.documentation.summary, false);
		this.writeNewLine();
	}

	writeDocList(list)
	{
		this.writeLine("///");
		this.writeLine("/// <ul>");
		for (const item of list.items) {
			this.write("/// <li>");
			this.writeDocPara(item, false);
			this.writeLine("</li>");
		}
		this.writeLine("/// </ul>");
		this.write("///");
	}

	writeDoc(doc)
	{
		if (doc == null)
			return;
		this.startDocLine();
		this.writeDocPara(doc.summary, false);
		this.writeNewLine();
		if (doc.details.length > 0) {
			this.startDocLine();
			if (doc.details.length == 1)
				this.writeDocBlock(doc.details[0], false);
			else {
				for (const block of doc.details)
					this.writeDocBlock(block, true);
			}
			this.writeNewLine();
		}
	}

	writeName(symbol)
	{
		if (symbol instanceof CiContainerType) {
			this.write(symbol.name);
			return;
		}
		this.writeCamelCase(symbol.name);
		switch (symbol.name) {
		case "Abstract":
		case "Alias":
		case "Align":
		case "Asm":
		case "Assert":
		case "Auto":
		case "Body":
		case "Bool":
		case "Break":
		case "Byte":
		case "Case":
		case "Cast":
		case "Catch":
		case "Cdouble":
		case "Cent":
		case "Cfloat":
		case "Char":
		case "Class":
		case "Const":
		case "Continue":
		case "Creal":
		case "Dchar":
		case "Debug":
		case "Default":
		case "Delegate":
		case "Delete":
		case "Deprecated":
		case "Do":
		case "Double":
		case "Else":
		case "Enum":
		case "Export":
		case "Extern":
		case "False":
		case "Final":
		case "Finally":
		case "Float":
		case "For":
		case "Foreach":
		case "Foreach_reverse":
		case "Function":
		case "Goto":
		case "Idouble":
		case "If":
		case "IfLoat":
		case "Immutable":
		case "Import":
		case "In":
		case "Inout":
		case "Int":
		case "Interface":
		case "Invariant":
		case "Ireal":
		case "Is":
		case "Lazy":
		case "Long":
		case "Macro":
		case "Mixin":
		case "Module":
		case "New":
		case "Nothrow":
		case "Null":
		case "Out":
		case "Override":
		case "Package":
		case "Pragma":
		case "Private":
		case "Protected":
		case "Public":
		case "Pure":
		case "Real":
		case "Ref":
		case "Return":
		case "Scope":
		case "Shared":
		case "Short":
		case "Sizeof":
		case "Static":
		case "String":
		case "Struct":
		case "Super":
		case "Switch":
		case "Synchronized":
		case "Template":
		case "Throw":
		case "True":
		case "Try":
		case "Typeid":
		case "Typeof":
		case "Ubyte":
		case "Ucent":
		case "Uint":
		case "Ulong":
		case "Union":
		case "Unittest":
		case "Ushort":
		case "Version":
		case "Void":
		case "Wchar":
		case "While":
		case "With":
		case "alias":
		case "align":
		case "asm":
		case "auto":
		case "body":
		case "cast":
		case "catch":
		case "cdouble":
		case "cent":
		case "cfloat":
		case "char":
		case "creal":
		case "dchar":
		case "debug":
		case "delegate":
		case "delete":
		case "deprecated":
		case "export":
		case "extern":
		case "final":
		case "finally":
		case "foreach_reverse":
		case "function":
		case "goto":
		case "idouble":
		case "ifloat":
		case "immutable":
		case "import":
		case "in":
		case "inout":
		case "interface":
		case "invariant":
		case "ireal":
		case "lazy":
		case "macro":
		case "mixin":
		case "module":
		case "nothrow":
		case "out":
		case "package":
		case "pragma":
		case "private":
		case "pure":
		case "real":
		case "ref":
		case "scope":
		case "shared":
		case "sizeof":
		case "struct":
		case "super":
		case "synchronized":
		case "template":
		case "try":
		case "typeid":
		case "typeof":
		case "ubyte":
		case "ucent":
		case "uint":
		case "ulong":
		case "union":
		case "unittest":
		case "ushort":
		case "version":
		case "wchar":
		case "with":
		case "__FILE__":
		case "__FILE_FULL_PATH__":
		case "__MODULE__":
		case "__LINE__":
		case "__FUNCTION__":
		case "__PRETTY_FUNCTION__":
		case "__gshared":
		case "__traits":
		case "__vector":
		case "__parameters":
			this.writeChar(95);
			break;
		default:
			break;
		}
	}

	getLiteralChars()
	{
		return 65536;
	}

	#writeVisibility(visibility)
	{
		switch (visibility) {
		case CiVisibility.PRIVATE:
			this.write("private ");
			break;
		case CiVisibility.INTERNAL:
		case CiVisibility.PUBLIC:
			break;
		case CiVisibility.PROTECTED:
			this.write("protected ");
			break;
		default:
			throw new Error();
		}
	}

	#writeCallType(callType, sealedString)
	{
		switch (callType) {
		case CiCallType.STATIC:
			this.write("static ");
			break;
		case CiCallType.NORMAL:
			break;
		case CiCallType.ABSTRACT:
			this.write("abstract ");
			break;
		case CiCallType.VIRTUAL:
			break;
		case CiCallType.OVERRIDE:
			this.write("override ");
			break;
		case CiCallType.SEALED:
			this.write(sealedString);
			break;
		}
	}

	static #isCreateWithNew(type)
	{
		let klass;
		if ((klass = type) instanceof CiClassType) {
			let stg;
			if ((stg = klass) instanceof CiStorageType)
				return stg.class.id != CiId.ARRAY_STORAGE_CLASS;
			return true;
		}
		return false;
	}

	static #isStructPtr(type)
	{
		let ptr;
		return (ptr = type) instanceof CiClassType && (ptr.class.id == CiId.LIST_CLASS || ptr.class.id == CiId.STACK_CLASS || ptr.class.id == CiId.QUEUE_CLASS);
	}

	#writeElementType(type)
	{
		this.writeType(type, false);
		if (GenD.#isStructPtr(type))
			this.writeChar(42);
	}

	writeType(type, promote)
	{
		if (type instanceof CiIntegerType) {
			switch (this.getTypeId(type, promote)) {
			case CiId.S_BYTE_RANGE:
				this.write("byte");
				break;
			case CiId.BYTE_RANGE:
				this.write("ubyte");
				break;
			case CiId.SHORT_RANGE:
				this.write("short");
				break;
			case CiId.U_SHORT_RANGE:
				this.write("ushort");
				break;
			case CiId.INT_TYPE:
				this.write("int");
				break;
			case CiId.LONG_TYPE:
				this.write("long");
				break;
			default:
				throw new Error();
			}
		}
		else if (type instanceof CiClassType) {
			const klass = type;
			switch (klass.class.id) {
			case CiId.STRING_CLASS:
				this.write("string");
				break;
			case CiId.ARRAY_STORAGE_CLASS:
			case CiId.ARRAY_PTR_CLASS:
				this.#writeElementType(klass.getElementType());
				this.writeChar(91);
				let arrayStorage;
				if ((arrayStorage = klass) instanceof CiArrayStorageType)
					this.visitLiteralLong(BigInt(arrayStorage.length));
				this.writeChar(93);
				break;
			case CiId.LIST_CLASS:
			case CiId.STACK_CLASS:
				this.include("std.container.array");
				this.write("Array!(");
				this.#writeElementType(klass.getElementType());
				this.writeChar(41);
				break;
			case CiId.QUEUE_CLASS:
				this.include("std.container.dlist");
				this.write("DList!(");
				this.#writeElementType(klass.getElementType());
				this.writeChar(41);
				break;
			case CiId.HASH_SET_CLASS:
				this.write("bool[");
				this.#writeElementType(klass.getElementType());
				this.writeChar(93);
				break;
			case CiId.DICTIONARY_CLASS:
				this.#writeElementType(klass.getValueType());
				this.writeChar(91);
				this.writeType(klass.getKeyType(), false);
				this.writeChar(93);
				break;
			case CiId.SORTED_SET_CLASS:
				this.include("std.container.rbtree");
				this.write("RedBlackTree!(");
				this.#writeElementType(klass.getElementType());
				this.writeChar(41);
				break;
			case CiId.SORTED_DICTIONARY_CLASS:
				this.include("std.container.rbtree");
				this.include("std.typecons");
				this.write("RedBlackTree!(Tuple!(");
				this.#writeElementType(klass.getKeyType());
				this.write(", ");
				this.#writeElementType(klass.getValueType());
				this.write("), \"a[0] < b[0]\")");
				break;
			case CiId.ORDERED_DICTIONARY_CLASS:
				this.include("std.typecons");
				this.write("Tuple!(Array!(");
				this.#writeElementType(klass.getValueType());
				this.write("), \"data\", size_t[");
				this.writeType(klass.getKeyType(), false);
				this.write("], \"dict\")");
				break;
			case CiId.TEXT_WRITER_CLASS:
				this.include("std.stdio");
				this.write("File");
				break;
			case CiId.REGEX_CLASS:
				this.include("std.regex");
				this.write("Regex!char");
				break;
			case CiId.MATCH_CLASS:
				this.include("std.regex");
				this.write("Captures!string");
				break;
			case CiId.LOCK_CLASS:
				this.write("Object");
				break;
			default:
				this.write(klass.class.name);
				break;
			}
		}
		else
			this.write(type.name);
	}

	writeTypeAndName(value)
	{
		this.writeType(value.type, true);
		if (GenD.#isStructPtr(value.type))
			this.writeChar(42);
		this.writeChar(32);
		this.writeName(value);
	}

	visitAggregateInitializer(expr)
	{
		this.write("[ ");
		this.writeCoercedLiterals(expr.type.asClassType().getElementType(), expr.items);
		this.write(" ]");
	}

	writeStaticCast(type, expr)
	{
		this.write("cast(");
		this.writeType(type, false);
		this.write(")(");
		this.getStaticCastInner(type, expr).accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	visitInterpolatedString(expr, parent)
	{
		this.include("std.format");
		this.write("format(");
		this.writePrintf(expr, false);
	}

	writeStorageInit(def)
	{
		this.write(" = ");
		this.writeNewStorage(def.type);
	}

	writeVarInit(def)
	{
		if (def.type instanceof CiArrayStorageType)
			return;
		super.writeVarInit(def);
	}

	hasInitCode(def)
	{
		if (def.value != null && !(def.value instanceof CiLiteral))
			return true;
		let type = def.type;
		let array;
		if ((array = type) instanceof CiArrayStorageType) {
			let innerArray;
			while ((innerArray = array.getElementType()) instanceof CiArrayStorageType)
				array = innerArray;
			type = array.getElementType();
		}
		return type instanceof CiStorageType;
	}

	writeInitField(field)
	{
		this.writeInitCode(field);
	}

	writeInitCode(def)
	{
		if (!this.hasInitCode(def))
			return;
		let array;
		if ((array = def.type) instanceof CiArrayStorageType) {
			let nesting = 0;
			let innerArray;
			while ((innerArray = array.getElementType()) instanceof CiArrayStorageType) {
				this.openLoop("size_t", nesting++, array.length);
				array = innerArray;
			}
			let klass;
			if ((klass = array.getElementType()) instanceof CiStorageType) {
				this.openLoop("size_t", nesting++, array.length);
				this.writeArrayElement(def, nesting);
				this.write(" = ");
				this.writeNew(klass, CiPriority.ARGUMENT);
				this.writeCharLine(59);
			}
			while (--nesting >= 0)
				this.closeBlock();
		}
		else {
			let klass;
			if ((klass = def.type) instanceof CiReadWriteClassType) {
				switch (klass.class.id) {
				case CiId.STRING_CLASS:
				case CiId.ARRAY_STORAGE_CLASS:
				case CiId.ARRAY_PTR_CLASS:
				case CiId.DICTIONARY_CLASS:
				case CiId.HASH_SET_CLASS:
				case CiId.SORTED_DICTIONARY_CLASS:
				case CiId.ORDERED_DICTIONARY_CLASS:
				case CiId.REGEX_CLASS:
				case CiId.MATCH_CLASS:
				case CiId.LOCK_CLASS:
					break;
				default:
					if (def.parent instanceof CiClass) {
						this.writeName(def);
						this.write(" = ");
						if (def.value == null)
							this.writeNew(klass, CiPriority.ARGUMENT);
						else
							this.writeCoercedExpr(def.type, def.value);
						this.writeCharLine(59);
					}
					super.writeInitCode(def);
					break;
				}
			}
		}
	}

	writeNewArray(elementType, lengthExpr, parent)
	{
		this.write("new ");
		this.writeType(elementType, false);
		this.writeChar(91);
		lengthExpr.accept(this, CiPriority.ARGUMENT);
		this.writeChar(93);
	}

	#writeStaticInitializer(type)
	{
		this.writeChar(40);
		this.writeType(type, false);
		this.write(").init");
	}

	writeNew(klass, parent)
	{
		if (GenD.#isCreateWithNew(klass)) {
			this.write("new ");
			this.writeType(klass, false);
		}
		else
			this.#writeStaticInitializer(klass);
	}

	writeResource(name, length)
	{
		this.write("CiResource.");
		this.writeResourceName(name);
	}

	writeStringLength(expr)
	{
		this.writePostfix(expr, ".length");
	}

	#writeClassReference(expr, priority = CiPriority.PRIMARY)
	{
		if (GenD.#isStructPtr(expr.type)) {
			this.write("(*");
			expr.accept(this, priority);
			this.writeChar(41);
		}
		else
			expr.accept(this, priority);
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.CONSOLE_ERROR:
			this.write("stderr");
			break;
		case CiId.LIST_COUNT:
		case CiId.STACK_COUNT:
		case CiId.HASH_SET_COUNT:
		case CiId.DICTIONARY_COUNT:
		case CiId.SORTED_SET_COUNT:
		case CiId.SORTED_DICTIONARY_COUNT:
			this.writeStringLength(expr.left);
			break;
		case CiId.QUEUE_COUNT:
			this.include("std.range");
			this.#writeClassReference(expr.left);
			this.write("[].walkLength");
			break;
		case CiId.MATCH_START:
			this.writePostfix(expr.left, ".pre.length");
			break;
		case CiId.MATCH_END:
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			this.writePostfix(expr.left, ".pre.length + ");
			this.writePostfix(expr.left, ".hit.length");
			if (parent > CiPriority.ADD)
				this.writeChar(41);
			break;
		case CiId.MATCH_LENGTH:
			this.writePostfix(expr.left, ".hit.length");
			break;
		case CiId.MATCH_VALUE:
			this.writePostfix(expr.left, ".hit");
			break;
		case CiId.MATH_NA_N:
			this.write("double.nan");
			break;
		case CiId.MATH_NEGATIVE_INFINITY:
			this.write("-double.infinity");
			break;
		case CiId.MATH_POSITIVE_INFINITY:
			this.write("double.infinity");
			break;
		default:
			super.visitSymbolReference(expr, parent);
			break;
		}
	}

	#writeWrite(args, newLine)
	{
		this.include("std.stdio");
		if (args.length == 0)
			this.write("writeln()");
		else {
			let interpolated;
			if ((interpolated = args[0]) instanceof CiInterpolatedString) {
				this.write(newLine ? "writefln(" : "writef(");
				this.writePrintf(interpolated, false);
			}
			else
				this.writeCall(newLine ? "writeln" : "write", args[0]);
		}
	}

	#writeInsertedArg(type, args, index = 0)
	{
		if (args.length <= index) {
			const klass = type;
			this.writeNew(klass, CiPriority.ARGUMENT);
		}
		else
			this.writeCoercedExpr(type, args[index]);
		this.writeChar(41);
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.ENUM_FROM_INT:
			this.writeStaticCast(method.type, args[0]);
			break;
		case CiId.ENUM_HAS_FLAG:
			this.writeEnumHasFlag(obj, args, parent);
			break;
		case CiId.INT_TRY_PARSE:
		case CiId.LONG_TRY_PARSE:
		case CiId.DOUBLE_TRY_PARSE:
			this.include("std.conv");
			this.write("() { try { ");
			this.writePostfix(obj, " = ");
			this.writePostfix(args[0], ".to!");
			this.write(obj.type.name);
			if (args.length == 2) {
				this.writeChar(40);
				args[1].accept(this, CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			this.write("; return true; } catch (ConvException e) return false; }()");
			break;
		case CiId.STRING_CONTAINS:
			this.include("std.algorithm");
			this.writePostfix(obj, ".canFind");
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.STRING_ENDS_WITH:
			this.include("std.string");
			this.writeMethodCall(obj, "endsWith", args[0]);
			break;
		case CiId.STRING_INDEX_OF:
			this.include("std.string");
			this.writeMethodCall(obj, "indexOf", args[0]);
			break;
		case CiId.STRING_LAST_INDEX_OF:
			this.include("std.string");
			this.writeMethodCall(obj, "lastIndexOf", args[0]);
			break;
		case CiId.STRING_REPLACE:
			this.include("std.string");
			this.writeMethodCall(obj, "replace", args[0], args[1]);
			break;
		case CiId.STRING_STARTS_WITH:
			this.include("std.string");
			this.writeMethodCall(obj, "startsWith", args[0]);
			break;
		case CiId.STRING_SUBSTRING:
			obj.accept(this, CiPriority.PRIMARY);
			this.writeChar(91);
			this.writePostfix(args[0], " .. $]");
			if (args.length > 1) {
				this.write("[0 .. ");
				args[1].accept(this, CiPriority.ARGUMENT);
				this.writeChar(93);
			}
			break;
		case CiId.ARRAY_BINARY_SEARCH_ALL:
		case CiId.ARRAY_BINARY_SEARCH_PART:
			this.include("std.range");
			this.write("() { size_t cibegin = ");
			if (args.length == 3)
				args[1].accept(this, CiPriority.ARGUMENT);
			else
				this.writeChar(48);
			this.write("; auto cisearch = ");
			this.#writeClassReference(obj);
			this.writeChar(91);
			if (args.length == 3) {
				this.write("cibegin .. cibegin + ");
				args[2].accept(this, CiPriority.ADD);
			}
			this.write("].assumeSorted.trisect(");
			this.writeNotPromoted(obj.type.asClassType().getElementType(), args[0]);
			this.write("); return cisearch[1].length ? cibegin + cisearch[0].length : -1; }()");
			break;
		case CiId.ARRAY_CONTAINS:
		case CiId.LIST_CONTAINS:
			this.include("std.algorithm");
			this.#writeClassReference(obj);
			this.writeCall("[].canFind", args[0]);
			break;
		case CiId.ARRAY_COPY_TO:
		case CiId.LIST_COPY_TO:
			this.include("std.algorithm");
			this.#writeClassReference(obj);
			this.writeChar(91);
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(" .. $][0 .. ");
			args[3].accept(this, CiPriority.ARGUMENT);
			this.write("].copy(");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.writeChar(91);
			args[2].accept(this, CiPriority.ARGUMENT);
			this.write(" .. $])");
			break;
		case CiId.ARRAY_FILL_ALL:
		case CiId.ARRAY_FILL_PART:
			this.include("std.algorithm");
			this.#writeClassReference(obj);
			this.writeChar(91);
			if (args.length == 3) {
				args[1].accept(this, CiPriority.ARGUMENT);
				this.write(" .. $][0 .. ");
				args[2].accept(this, CiPriority.ARGUMENT);
			}
			this.write("].fill(");
			this.writeNotPromoted(obj.type.asClassType().getElementType(), args[0]);
			this.writeChar(41);
			break;
		case CiId.ARRAY_SORT_ALL:
		case CiId.ARRAY_SORT_PART:
		case CiId.LIST_SORT_ALL:
		case CiId.LIST_SORT_PART:
			this.include("std.algorithm");
			this.#writeClassReference(obj);
			this.writeChar(91);
			if (args.length == 2) {
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(" .. $][0 .. ");
				args[1].accept(this, CiPriority.ARGUMENT);
			}
			this.write("].sort");
			break;
		case CiId.LIST_ADD:
		case CiId.QUEUE_ENQUEUE:
			this.writePostfix(obj, ".insertBack(");
			this.#writeInsertedArg(obj.type.asClassType().getElementType(), args);
			break;
		case CiId.LIST_ADD_RANGE:
			this.#writeClassReference(obj);
			this.write(" ~= ");
			this.#writeClassReference(args[0]);
			this.write("[]");
			break;
		case CiId.LIST_ALL:
			this.include("std.algorithm");
			this.#writeClassReference(obj);
			this.writeCall("[].all!", args[0]);
			break;
		case CiId.LIST_ANY:
			this.include("std.algorithm");
			this.#writeClassReference(obj);
			this.write("[].any!(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.LIST_INSERT:
			this.#hasListInsert = true;
			this.writePostfix(obj, ".insertInPlace(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeInsertedArg(obj.type.asClassType().getElementType(), args, 1);
			break;
		case CiId.LIST_LAST:
			this.writePostfix(obj, ".back");
			break;
		case CiId.LIST_REMOVE_AT:
		case CiId.LIST_REMOVE_RANGE:
			this.#hasListRemoveAt = true;
			this.writePostfix(obj, ".removeAt");
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.LIST_INDEX_OF:
			this.include("std.algorithm");
			this.#writeClassReference(obj);
			this.write("[].countUntil");
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.QUEUE_DEQUEUE:
			this.#hasQueueDequeue = true;
			this.include("std.container.dlist");
			this.#writeClassReference(obj);
			this.write(".dequeue()");
			break;
		case CiId.QUEUE_PEEK:
			this.writePostfix(obj, ".front");
			break;
		case CiId.STACK_PEEK:
			this.writePostfix(obj, ".back");
			break;
		case CiId.STACK_PUSH:
			this.#writeClassReference(obj);
			this.write(" ~= ");
			args[0].accept(this, CiPriority.ASSIGN);
			break;
		case CiId.STACK_POP:
			this.#hasStackPop = true;
			this.#writeClassReference(obj);
			this.write(".pop()");
			break;
		case CiId.HASH_SET_ADD:
			this.writePostfix(obj, ".require(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", true)");
			break;
		case CiId.HASH_SET_CLEAR:
		case CiId.DICTIONARY_CLEAR:
			this.writePostfix(obj, ".clear()");
			break;
		case CiId.HASH_SET_CONTAINS:
		case CiId.DICTIONARY_CONTAINS_KEY:
			this.writeChar(40);
			args[0].accept(this, CiPriority.REL);
			this.write(" in ");
			obj.accept(this, CiPriority.PRIMARY);
			this.writeChar(41);
			break;
		case CiId.SORTED_SET_ADD:
			this.writePostfix(obj, ".insert(");
			this.#writeInsertedArg(obj.type.asClassType().getElementType(), args, 0);
			break;
		case CiId.SORTED_SET_REMOVE:
			this.writePostfix(obj, ".removeKey");
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.DICTIONARY_ADD:
			if (obj.type.asClassType().class.id == CiId.SORTED_DICTIONARY_CLASS) {
				this.#hasSortedDictionaryInsert = true;
				this.writePostfix(obj, ".replace(");
			}
			else
				this.writePostfix(obj, ".require(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeInsertedArg(obj.type.asClassType().getValueType(), args, 1);
			break;
		case CiId.SORTED_DICTIONARY_CONTAINS_KEY:
			this.write("tuple(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeStaticInitializer(obj.type.asClassType().getValueType());
			this.write(") in ");
			this.#writeClassReference(obj);
			break;
		case CiId.SORTED_DICTIONARY_REMOVE:
			this.#writeClassReference(obj);
			this.write(".removeKey(tuple(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.#writeStaticInitializer(obj.type.asClassType().getValueType());
			this.write("))");
			break;
		case CiId.TEXT_WRITER_WRITE:
		case CiId.TEXT_WRITER_WRITE_LINE:
			this.writePostfix(obj, ".");
			this.#writeWrite(args, method.id == CiId.TEXT_WRITER_WRITE_LINE);
			break;
		case CiId.TEXT_WRITER_WRITE_CHAR:
			this.writePostfix(obj, ".write(");
			if (!(args[0] instanceof CiLiteralChar))
				this.write("cast(char) ");
			args[0].accept(this, CiPriority.PRIMARY);
			this.writeChar(41);
			break;
		case CiId.TEXT_WRITER_WRITE_CODE_POINT:
			this.writePostfix(obj, ".write(cast(dchar) ");
			args[0].accept(this, CiPriority.PRIMARY);
			this.writeChar(41);
			break;
		case CiId.CONSOLE_WRITE:
		case CiId.CONSOLE_WRITE_LINE:
			this.#writeWrite(args, method.id == CiId.CONSOLE_WRITE_LINE);
			break;
		case CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE:
			this.include("std.process");
			this.write("environment.get");
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			this.writePostfix(args[0], ".length");
			break;
		case CiId.U_T_F8_GET_BYTES:
			this.include("std.string");
			this.include("std.algorithm");
			this.writePostfix(args[0], ".representation.copy(");
			this.writePostfix(args[1], "[");
			args[2].accept(this, CiPriority.ARGUMENT);
			this.write(" .. $])");
			break;
		case CiId.U_T_F8_GET_STRING:
			this.write("cast(string) (");
			this.writePostfix(args[0], "[");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.write(" .. $][0 .. ");
			args[2].accept(this, CiPriority.ARGUMENT);
			this.write("])");
			break;
		case CiId.REGEX_COMPILE:
			this.include("std.regex");
			this.write("regex(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
			this.writeChar(41);
			break;
		case CiId.REGEX_ESCAPE:
			this.include("std.regex");
			this.include("std.conv");
			this.writePostfix(args[0], ".escaper.to!string");
			break;
		case CiId.REGEX_IS_MATCH_REGEX:
			this.include("std.regex");
			this.writePostfix(args[0], ".matchFirst(");
			(args.length > 1 ? args[1] : obj).accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.REGEX_IS_MATCH_STR:
			this.include("std.regex");
			this.writePostfix(args[0], ".matchFirst(");
			if (this.getRegexOptions(args) != RegexOptions.NONE)
				this.write("regex(");
			(args.length > 1 ? args[1] : obj).accept(this, CiPriority.ARGUMENT);
			this.writeRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
			this.writeChar(41);
			break;
		case CiId.MATCH_FIND_STR:
			this.include("std.regex");
			this.writeChar(40);
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			args[0].accept(this, CiPriority.PRIMARY);
			this.write(".matchFirst(");
			if (this.getRegexOptions(args) != RegexOptions.NONE)
				this.write("regex(");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.writeRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
			this.write("))");
			break;
		case CiId.MATCH_FIND_REGEX:
			this.include("std.regex");
			this.writeChar(40);
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			this.writeMethodCall(args[0], "matchFirst", args[1]);
			this.writeChar(41);
			break;
		case CiId.MATCH_GET_CAPTURE:
			this.writeIndexing(obj, args[0]);
			break;
		case CiId.MATH_METHOD:
		case CiId.MATH_ABS:
		case CiId.MATH_IS_FINITE:
		case CiId.MATH_IS_INFINITY:
		case CiId.MATH_IS_NA_N:
		case CiId.MATH_LOG2:
		case CiId.MATH_ROUND:
			this.include("std.math");
			this.writeCamelCase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_CEILING:
			this.include("std.math");
			this.writeCall("ceil", args[0]);
			break;
		case CiId.MATH_CLAMP:
		case CiId.MATH_MAX_INT:
		case CiId.MATH_MAX_DOUBLE:
		case CiId.MATH_MIN_INT:
		case CiId.MATH_MIN_DOUBLE:
			this.include("std.algorithm");
			this.writeLowercase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_FUSED_MULTIPLY_ADD:
			this.include("std.math");
			this.writeCall("fma", args[0], args[1], args[2]);
			break;
		case CiId.MATH_TRUNCATE:
			this.include("std.math");
			this.writeCall("trunc", args[0]);
			break;
		default:
			if (obj != null) {
				if (GenD.isReferenceTo(obj, CiId.BASE_PTR))
					this.write("super.");
				else {
					this.#writeClassReference(obj);
					this.writeChar(46);
				}
			}
			this.writeName(method);
			this.writeArgsInParentheses(method, args);
			break;
		}
	}

	writeIndexingExpr(expr, parent)
	{
		this.#writeClassReference(expr.left);
		const klass = expr.left.type;
		switch (klass.class.id) {
		case CiId.ARRAY_PTR_CLASS:
		case CiId.ARRAY_STORAGE_CLASS:
		case CiId.DICTIONARY_CLASS:
		case CiId.LIST_CLASS:
			this.writeChar(91);
			expr.right.accept(this, CiPriority.ARGUMENT);
			this.writeChar(93);
			break;
		case CiId.SORTED_DICTIONARY_CLASS:
			console.assert(parent != CiPriority.ASSIGN);
			this.#hasSortedDictionaryFind = true;
			this.include("std.container.rbtree");
			this.include("std.typecons");
			this.write(".find(");
			this.writeStronglyCoerced(klass.getKeyType(), expr.right);
			this.writeChar(41);
			break;
		case CiId.ORDERED_DICTIONARY_CLASS:
			this.notSupported(expr, "OrderedDictionary");
			break;
		default:
			throw new Error();
		}
	}

	static #isIsComparable(expr)
	{
		let klass;
		return expr instanceof CiLiteralNull || ((klass = expr.type) instanceof CiClassType && klass.class.id == CiId.ARRAY_PTR_CLASS);
	}

	writeEqual(left, right, parent, not)
	{
		if (GenD.#isIsComparable(left) || GenD.#isIsComparable(right))
			this.writeEqualExpr(left, right, parent, not ? " !is " : " is ");
		else
			super.writeEqual(left, right, parent, not);
	}

	writeAssign(expr, parent)
	{
		let indexing;
		let dict;
		if ((indexing = expr.left) instanceof CiBinaryExpr && indexing.op == CiToken.LEFT_BRACKET && (dict = indexing.left.type) instanceof CiClassType) {
			switch (dict.class.id) {
			case CiId.SORTED_DICTIONARY_CLASS:
				this.#hasSortedDictionaryInsert = true;
				this.writePostfix(indexing.left, ".replace(");
				indexing.right.accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				this.writeNotPromoted(expr.type, expr.right);
				this.writeChar(41);
				return;
			default:
				break;
			}
		}
		super.writeAssign(expr, parent);
	}

	#writeIsVar(expr, def, parent)
	{
		let thisPriority = def.name == "_" ? CiPriority.PRIMARY : CiPriority.ASSIGN;
		if (parent > thisPriority)
			this.writeChar(40);
		if (def.name != "_") {
			this.writeName(def);
			this.write(" = ");
		}
		this.write("cast(");
		this.writeType(def.type, true);
		this.write(") ");
		expr.accept(this, CiPriority.PRIMARY);
		if (parent > thisPriority)
			this.writeChar(41);
	}

	visitBinaryExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.IS:
			if (parent >= CiPriority.OR && parent <= CiPriority.MUL)
				parent = CiPriority.PRIMARY;
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			if (expr.right instanceof CiSymbolReference) {
				const symbol = expr.right;
				this.write("cast(");
				this.write(symbol.symbol.name);
				this.write(") ");
				expr.left.accept(this, CiPriority.PRIMARY);
			}
			else if (expr.right instanceof CiVar) {
				const def = expr.right;
				this.#writeIsVar(expr.left, def, CiPriority.EQUALITY);
			}
			else
				throw new Error();
			this.write(" !is null");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			return;
		case CiToken.PLUS:
			if (expr.type.id == CiId.STRING_STORAGE_TYPE) {
				expr.left.accept(this, CiPriority.ASSIGN);
				this.write(" ~ ");
				expr.right.accept(this, CiPriority.ASSIGN);
				return;
			}
			break;
		case CiToken.ADD_ASSIGN:
			if (expr.left.type.id == CiId.STRING_STORAGE_TYPE) {
				expr.left.accept(this, CiPriority.ASSIGN);
				this.write(" ~= ");
				this.writeAssignRight(expr);
				return;
			}
			break;
		default:
			break;
		}
		super.visitBinaryExpr(expr, parent);
	}

	visitLambdaExpr(expr)
	{
		this.writeName(expr.first);
		this.write(" => ");
		expr.body.accept(this, CiPriority.STATEMENT);
	}

	writeAssert(statement)
	{
		this.write("assert(");
		statement.cond.accept(this, CiPriority.ARGUMENT);
		if (statement.message != null) {
			this.write(", ");
			statement.message.accept(this, CiPriority.ARGUMENT);
		}
		this.writeLine(");");
	}

	visitForeach(statement)
	{
		this.write("foreach (");
		let dict;
		if ((dict = statement.collection.type) instanceof CiClassType && dict.class.typeParameterCount == 2) {
			this.writeTypeAndName(statement.getVar());
			this.write(", ");
			this.writeTypeAndName(statement.getValueVar());
		}
		else
			this.writeTypeAndName(statement.getVar());
		this.write("; ");
		this.#writeClassReference(statement.collection);
		let set;
		if ((set = statement.collection.type) instanceof CiClassType && set.class.id == CiId.HASH_SET_CLASS)
			this.write(".byKey");
		this.writeChar(41);
		this.writeChild(statement.body);
	}

	visitLock(statement)
	{
		this.writeCall("synchronized ", statement.lock);
		this.writeChild(statement.body);
	}

	writeSwitchCaseTypeVar(value)
	{
		this.defineVar(value);
	}

	writeSwitchCaseCond(statement, value, parent)
	{
		let def;
		if ((def = value) instanceof CiVar) {
			this.#writeIsVar(statement.value, def, CiPriority.EQUALITY);
			this.write(" !is null");
		}
		else
			super.writeSwitchCaseCond(statement, value, parent);
	}

	visitSwitch(statement)
	{
		this.writeTemporaries(statement.value);
		if (statement.isTypeMatching() || statement.hasWhen())
			this.writeSwitchAsIfsWithGoto(statement);
		else {
			this.startSwitch(statement);
			this.writeLine("default:");
			this.indent++;
			if (statement.defaultBody.length > 0)
				this.writeSwitchCaseBody(statement.defaultBody);
			else
				this.writeLine("assert(false);");
			this.indent--;
			this.writeCharLine(125);
		}
	}

	visitThrow(statement)
	{
		this.include("std.exception");
		this.write("throw new Exception(");
		statement.message.accept(this, CiPriority.ARGUMENT);
		this.writeLine(");");
	}

	writeEnum(enu)
	{
		this.writeNewLine();
		this.writeDoc(enu.documentation);
		this.writePublic(enu);
		this.write("enum ");
		this.write(enu.name);
		this.openBlock();
		enu.acceptValues(this);
		this.writeNewLine();
		this.closeBlock();
	}

	writeConst(konst)
	{
		this.writeDoc(konst.documentation);
		this.write("static ");
		this.writeTypeAndName(konst);
		this.write(" = ");
		this.writeCoercedExpr(konst.type, konst.value);
		this.writeCharLine(59);
	}

	writeField(field)
	{
		this.writeNewLine();
		this.writeDoc(field.documentation);
		this.#writeVisibility(field.visibility);
		this.writeTypeAndName(field);
		if (field.value instanceof CiLiteral) {
			this.write(" = ");
			this.writeCoercedExpr(field.type, field.value);
		}
		this.writeCharLine(59);
	}

	writeMethod(method)
	{
		if (method.id == CiId.CLASS_TO_STRING && method.callType == CiCallType.ABSTRACT)
			return;
		this.writeNewLine();
		this.writeDoc(method.documentation);
		this.writeParametersDoc(method);
		this.#writeVisibility(method.visibility);
		if (method.id == CiId.CLASS_TO_STRING)
			this.write("override ");
		else
			this.#writeCallType(method.callType, "final override ");
		this.writeTypeAndName(method);
		this.writeParameters(method, true);
		this.writeBody(method);
	}

	writeClass(klass, program)
	{
		this.writeNewLine();
		this.writeDoc(klass.documentation);
		if (klass.callType == CiCallType.SEALED)
			this.write("final ");
		this.openClass(klass, "", " : ");
		if (this.needsConstructor(klass)) {
			if (klass.constructor_ != null) {
				this.writeDoc(klass.constructor_.documentation);
				this.#writeVisibility(klass.constructor_.visibility);
			}
			else
				this.write("private ");
			this.writeLine("this()");
			this.openBlock();
			this.writeConstructorBody(klass);
			this.closeBlock();
		}
		for (let symbol = klass.first; symbol != null; symbol = symbol.next) {
			if (!(symbol instanceof CiMember))
				continue;
			if (symbol instanceof CiConst) {
				const konst = symbol;
				this.writeConst(konst);
			}
			else if (symbol instanceof CiField) {
				const field = symbol;
				this.writeField(field);
			}
			else if (symbol instanceof CiMethod) {
				const method = symbol;
				this.writeMethod(method);
				this.currentTemporaries.length = 0;
			}
			else if (symbol instanceof CiVar) {
			}
			else
				throw new Error();
		}
		this.closeBlock();
	}

	static #isLong(expr)
	{
		switch (expr.symbol.id) {
		case CiId.ARRAY_LENGTH:
		case CiId.STRING_LENGTH:
		case CiId.LIST_COUNT:
			return true;
		default:
			return false;
		}
	}

	writeCoercedInternal(type, expr, parent)
	{
		if (type instanceof CiRangeType)
			this.writeStaticCast(type, expr);
		else {
			let symref;
			if (type instanceof CiIntegerType && (symref = expr) instanceof CiSymbolReference && GenD.#isLong(symref))
				this.writeStaticCast(type, expr);
			else if (type instanceof CiFloatingType && !(expr.type instanceof CiFloatingType))
				this.writeStaticCast(type, expr);
			else if (type instanceof CiClassType && !(type instanceof CiArrayStorageType) && expr.type instanceof CiArrayStorageType) {
				super.writeCoercedInternal(type, expr, CiPriority.PRIMARY);
				this.write("[]");
			}
			else
				super.writeCoercedInternal(type, expr, parent);
		}
	}

	#writeResources(resources)
	{
		this.writeNewLine();
		this.writeLine("private static struct CiResource");
		this.openBlock();
		for (const [name, content] of Object.entries(resources).sort((a, b) => a[0].localeCompare(b[0]))) {
			this.write("private static ubyte[] ");
			this.writeResourceName(name);
			this.writeLine(" = [");
			this.writeChar(9);
			this.writeBytes(content);
			this.writeLine(" ];");
		}
		this.closeBlock();
	}

	writeProgram(program)
	{
		this.#hasListInsert = false;
		this.#hasListRemoveAt = false;
		this.#hasQueueDequeue = false;
		this.#hasStackPop = false;
		this.#hasSortedDictionaryInsert = false;
		this.#hasSortedDictionaryFind = false;
		this.openStringWriter();
		if (this.namespace.length != 0) {
			this.write("struct ");
			this.writeLine(this.namespace);
			this.openBlock();
			this.writeLine("static:");
		}
		this.writeTopLevelNatives(program);
		this.writeTypes(program);
		if (Object.keys(program.resources).length > 0)
			this.#writeResources(program.resources);
		if (this.namespace.length != 0)
			this.closeBlock();
		this.createOutputFile();
		if (this.#hasListInsert || this.#hasListRemoveAt || this.#hasStackPop)
			this.include("std.container.array");
		if (this.#hasSortedDictionaryInsert) {
			this.include("std.container.rbtree");
			this.include("std.typecons");
		}
		this.writeIncludes("import ", ";");
		if (this.#hasListInsert) {
			this.writeNewLine();
			this.writeLine("private void insertInPlace(T, U...)(Array!T* arr, size_t pos, auto ref U stuff)");
			this.openBlock();
			this.writeLine("arr.insertAfter((*arr)[0 .. pos], stuff);");
			this.closeBlock();
		}
		if (this.#hasListRemoveAt) {
			this.writeNewLine();
			this.writeLine("private void removeAt(T)(Array!T* arr, size_t pos, size_t count = 1)");
			this.openBlock();
			this.writeLine("arr.linearRemove((*arr)[pos .. pos + count]);");
			this.closeBlock();
		}
		if (this.#hasQueueDequeue) {
			this.writeNewLine();
			this.writeLine("private T dequeue(T)(ref DList!T q)");
			this.openBlock();
			this.writeLine("scope(exit) q.removeFront(); return q.front;");
			this.closeBlock();
		}
		if (this.#hasStackPop) {
			this.writeNewLine();
			this.writeLine("private T pop(T)(ref Array!T stack)");
			this.openBlock();
			this.writeLine("scope(exit) stack.removeBack(); return stack.back;");
			this.closeBlock();
		}
		if (this.#hasSortedDictionaryFind) {
			this.writeNewLine();
			this.writeLine("private U find(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key)");
			this.openBlock();
			this.writeLine("return dict.equalRange(tuple(key, U.init)).front[1];");
			this.closeBlock();
		}
		if (this.#hasSortedDictionaryInsert) {
			this.writeNewLine();
			this.writeLine("private void replace(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key, lazy U value)");
			this.openBlock();
			this.writeLine("dict.removeKey(tuple(key, U.init));");
			this.writeLine("dict.insert(tuple(key, value));");
			this.closeBlock();
		}
		this.closeStringWriter();
		this.closeFile();
	}
}

export class GenJava extends GenTyped
{
	#switchCaseDiscards;

	getTargetName()
	{
		return "Java";
	}

	visitLiteralLong(value)
	{
		super.visitLiteralLong(value);
		if (value < -2147483648 || value > 2147483647)
			this.writeChar(76);
	}

	getLiteralChars()
	{
		return 65536;
	}

	#writeToString(expr, parent)
	{
		switch (expr.type.id) {
		case CiId.LONG_TYPE:
			this.write("Long");
			break;
		case CiId.FLOAT_TYPE:
			this.write("Float");
			break;
		case CiId.DOUBLE_TYPE:
		case CiId.FLOAT_INT_TYPE:
			this.write("Double");
			break;
		case CiId.STRING_PTR_TYPE:
		case CiId.STRING_STORAGE_TYPE:
			expr.accept(this, parent);
			return;
		default:
			if (expr.type instanceof CiIntegerType)
				this.write("Integer");
			else if (expr.type instanceof CiClassType) {
				this.writePostfix(expr, ".toString()");
				return;
			}
			else
				throw new Error();
			break;
		}
		this.writeCall(".toString", expr);
	}

	writePrintfWidth(part)
	{
		if (part.precision >= 0 && part.argument.type instanceof CiIntegerType) {
			this.writeChar(48);
			this.visitLiteralLong(BigInt(part.precision));
		}
		else
			super.writePrintfWidth(part);
	}

	visitInterpolatedString(expr, parent)
	{
		if (expr.suffix.length == 0 && expr.parts.length == 1 && expr.parts[0].prefix.length == 0 && expr.parts[0].widthExpr == null && expr.parts[0].format == 32)
			this.#writeToString(expr.parts[0].argument, parent);
		else {
			this.write("String.format(");
			this.writePrintf(expr, false);
		}
	}

	#writeCamelCaseNotKeyword(name)
	{
		this.writeCamelCase(name);
		switch (name) {
		case "Abstract":
		case "Assert":
		case "Boolean":
		case "Break":
		case "Byte":
		case "Case":
		case "Catch":
		case "Char":
		case "Class":
		case "Const":
		case "Continue":
		case "Default":
		case "Do":
		case "Double":
		case "Else":
		case "Enum":
		case "Extends":
		case "False":
		case "Final":
		case "Finally":
		case "Float":
		case "For":
		case "Foreach":
		case "Goto":
		case "If":
		case "Implements":
		case "Import":
		case "Instanceof":
		case "Int":
		case "Interface":
		case "Long":
		case "Native":
		case "New":
		case "Null":
		case "Package":
		case "Private":
		case "Protected":
		case "Public":
		case "Return":
		case "Short":
		case "Static":
		case "Strictfp":
		case "String":
		case "Super":
		case "Switch":
		case "Synchronized":
		case "Transient":
		case "Throw":
		case "Throws":
		case "True":
		case "Try":
		case "Void":
		case "Volatile":
		case "While":
		case "Yield":
		case "boolean":
		case "catch":
		case "char":
		case "extends":
		case "final":
		case "finally":
		case "goto":
		case "implements":
		case "import":
		case "instanceof":
		case "interface":
		case "package":
		case "private":
		case "strictfp":
		case "super":
		case "synchronized":
		case "transient":
		case "try":
		case "volatile":
		case "yield":
			this.writeChar(95);
			break;
		default:
			break;
		}
	}

	writeName(symbol)
	{
		if (symbol instanceof CiContainerType)
			this.write(symbol.name);
		else if (symbol instanceof CiConst) {
			const konst = symbol;
			if (konst.inMethod != null) {
				this.writeUppercaseWithUnderscores(konst.inMethod.name);
				this.writeChar(95);
			}
			this.writeUppercaseWithUnderscores(symbol.name);
		}
		else if (symbol instanceof CiVar) {
			let forEach;
			if ((forEach = symbol.parent) instanceof CiForeach && forEach.count() == 2) {
				let element = forEach.getVar();
				this.#writeCamelCaseNotKeyword(element.name);
				this.write(symbol == element ? ".getKey()" : ".getValue()");
			}
			else
				this.#writeCamelCaseNotKeyword(symbol.name);
		}
		else if (symbol instanceof CiMember)
			this.#writeCamelCaseNotKeyword(symbol.name);
		else
			throw new Error();
	}

	#writeVisibility(visibility)
	{
		switch (visibility) {
		case CiVisibility.PRIVATE:
			this.write("private ");
			break;
		case CiVisibility.INTERNAL:
			break;
		case CiVisibility.PROTECTED:
			this.write("protected ");
			break;
		case CiVisibility.PUBLIC:
			this.write("public ");
			break;
		default:
			throw new Error();
		}
	}

	getTypeId(type, promote)
	{
		let id = super.getTypeId(type, promote);
		switch (id) {
		case CiId.BYTE_RANGE:
			return CiId.S_BYTE_RANGE;
		case CiId.U_SHORT_RANGE:
			return CiId.INT_TYPE;
		default:
			return id;
		}
	}

	#writeCollectionType(name, elementType)
	{
		this.include("java.util." + name);
		this.write(name);
		this.writeChar(60);
		this.#writeJavaType(elementType, false, true);
		this.writeChar(62);
	}

	#writeDictType(name, dict)
	{
		this.write(name);
		this.writeChar(60);
		this.#writeJavaType(dict.getKeyType(), false, true);
		this.write(", ");
		this.#writeJavaType(dict.getValueType(), false, true);
		this.writeChar(62);
	}

	#writeJavaType(type, promote, needClass)
	{
		if (type instanceof CiNumericType) {
			switch (this.getTypeId(type, promote)) {
			case CiId.S_BYTE_RANGE:
				this.write(needClass ? "Byte" : "byte");
				break;
			case CiId.SHORT_RANGE:
				this.write(needClass ? "Short" : "short");
				break;
			case CiId.INT_TYPE:
				this.write(needClass ? "Integer" : "int");
				break;
			case CiId.LONG_TYPE:
				this.write(needClass ? "Long" : "long");
				break;
			case CiId.FLOAT_TYPE:
				this.write(needClass ? "Float" : "float");
				break;
			case CiId.DOUBLE_TYPE:
				this.write(needClass ? "Double" : "double");
				break;
			default:
				throw new Error();
			}
		}
		else if (type instanceof CiEnum) {
			const enu = type;
			this.write(enu.id == CiId.BOOL_TYPE ? needClass ? "Boolean" : "boolean" : needClass ? "Integer" : "int");
		}
		else if (type instanceof CiClassType) {
			const klass = type;
			switch (klass.class.id) {
			case CiId.STRING_CLASS:
				this.write("String");
				break;
			case CiId.ARRAY_PTR_CLASS:
			case CiId.ARRAY_STORAGE_CLASS:
				this.writeType(klass.getElementType(), false);
				this.write("[]");
				break;
			case CiId.LIST_CLASS:
				this.#writeCollectionType("ArrayList", klass.getElementType());
				break;
			case CiId.QUEUE_CLASS:
				this.#writeCollectionType("ArrayDeque", klass.getElementType());
				break;
			case CiId.STACK_CLASS:
				this.#writeCollectionType("Stack", klass.getElementType());
				break;
			case CiId.HASH_SET_CLASS:
				this.#writeCollectionType("HashSet", klass.getElementType());
				break;
			case CiId.SORTED_SET_CLASS:
				this.#writeCollectionType("TreeSet", klass.getElementType());
				break;
			case CiId.DICTIONARY_CLASS:
				this.include("java.util.HashMap");
				this.#writeDictType("HashMap", klass);
				break;
			case CiId.SORTED_DICTIONARY_CLASS:
				this.include("java.util.TreeMap");
				this.#writeDictType("TreeMap", klass);
				break;
			case CiId.ORDERED_DICTIONARY_CLASS:
				this.include("java.util.LinkedHashMap");
				this.#writeDictType("LinkedHashMap", klass);
				break;
			case CiId.TEXT_WRITER_CLASS:
				this.write("Appendable");
				break;
			case CiId.REGEX_CLASS:
				this.include("java.util.regex.Pattern");
				this.write("Pattern");
				break;
			case CiId.MATCH_CLASS:
				this.include("java.util.regex.Matcher");
				this.write("Matcher");
				break;
			case CiId.LOCK_CLASS:
				this.write("Object");
				break;
			default:
				this.write(klass.class.name);
				break;
			}
		}
		else
			this.write(type.name);
	}

	writeType(type, promote)
	{
		this.#writeJavaType(type, promote, false);
	}

	writeNew(klass, parent)
	{
		this.write("new ");
		this.writeType(klass, false);
		this.write("()");
	}

	writeResource(name, length)
	{
		this.write("CiResource.getByteArray(");
		this.visitLiteralString(name);
		this.write(", ");
		this.visitLiteralLong(BigInt(length));
		this.writeChar(41);
	}

	static #isUnsignedByte(type)
	{
		let range;
		return type.id == CiId.BYTE_RANGE && (range = type) instanceof CiRangeType && range.max > 127;
	}

	static #isUnsignedByteIndexing(expr)
	{
		return expr.isIndexing() && GenJava.#isUnsignedByte(expr.type);
	}

	#writeIndexingInternal(expr)
	{
		if (expr.left.type.isArray())
			super.writeIndexingExpr(expr, CiPriority.AND);
		else
			this.writeMethodCall(expr.left, "get", expr.right);
	}

	visitPrefixExpr(expr, parent)
	{
		if ((expr.op == CiToken.INCREMENT || expr.op == CiToken.DECREMENT) && GenJava.#isUnsignedByteIndexing(expr.inner)) {
			if (parent > CiPriority.AND)
				this.writeChar(40);
			this.write(expr.op == CiToken.INCREMENT ? "++" : "--");
			const indexing = expr.inner;
			this.#writeIndexingInternal(indexing);
			if (parent != CiPriority.STATEMENT)
				this.write(" & 0xff");
			if (parent > CiPriority.AND)
				this.writeChar(41);
		}
		else
			super.visitPrefixExpr(expr, parent);
	}

	visitPostfixExpr(expr, parent)
	{
		if ((expr.op == CiToken.INCREMENT || expr.op == CiToken.DECREMENT) && GenJava.#isUnsignedByteIndexing(expr.inner)) {
			if (parent > CiPriority.AND)
				this.writeChar(40);
			const indexing = expr.inner;
			this.#writeIndexingInternal(indexing);
			this.write(expr.op == CiToken.INCREMENT ? "++" : "--");
			if (parent != CiPriority.STATEMENT)
				this.write(" & 0xff");
			if (parent > CiPriority.AND)
				this.writeChar(41);
		}
		else
			super.visitPostfixExpr(expr, parent);
	}

	#writeSByteLiteral(literal)
	{
		this.visitLiteralLong((literal.value ^ 128n) - 128n);
	}

	writeEqual(left, right, parent, not)
	{
		if ((left.type instanceof CiStringType && right.type.id != CiId.NULL_TYPE) || (right.type instanceof CiStringType && left.type.id != CiId.NULL_TYPE)) {
			if (not)
				this.writeChar(33);
			this.writeMethodCall(left, "equals", right);
		}
		else {
			let rightLiteral;
			if (GenJava.#isUnsignedByteIndexing(left) && (rightLiteral = right) instanceof CiLiteralLong && rightLiteral.type.id == CiId.BYTE_RANGE) {
				if (parent > CiPriority.EQUALITY)
					this.writeChar(40);
				const indexing = left;
				this.#writeIndexingInternal(indexing);
				this.write(GenJava.getEqOp(not));
				this.#writeSByteLiteral(rightLiteral);
				if (parent > CiPriority.EQUALITY)
					this.writeChar(41);
			}
			else
				super.writeEqual(left, right, parent, not);
		}
	}

	writeCoercedLiteral(type, expr)
	{
		if (GenJava.#isUnsignedByte(type)) {
			const literal = expr;
			this.#writeSByteLiteral(literal);
		}
		else
			super.writeCoercedLiteral(type, expr);
	}

	writeAnd(expr, parent)
	{
		let rightLiteral;
		if (GenJava.#isUnsignedByteIndexing(expr.left) && (rightLiteral = expr.right) instanceof CiLiteralLong) {
			if (parent > CiPriority.COND_AND && parent != CiPriority.AND)
				this.writeChar(40);
			const indexing = expr.left;
			this.#writeIndexingInternal(indexing);
			this.write(" & ");
			this.visitLiteralLong(255n & rightLiteral.value);
			if (parent > CiPriority.COND_AND && parent != CiPriority.AND)
				this.writeChar(41);
		}
		else
			super.writeAnd(expr, parent);
	}

	writeStringLength(expr)
	{
		this.writePostfix(expr, ".length()");
	}

	writeCharAt(expr)
	{
		this.writeMethodCall(expr.left, "charAt", expr.right);
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.CONSOLE_ERROR:
			this.write("System.err");
			break;
		case CiId.LIST_COUNT:
		case CiId.QUEUE_COUNT:
		case CiId.STACK_COUNT:
		case CiId.HASH_SET_COUNT:
		case CiId.SORTED_SET_COUNT:
		case CiId.DICTIONARY_COUNT:
		case CiId.SORTED_DICTIONARY_COUNT:
		case CiId.ORDERED_DICTIONARY_COUNT:
			expr.left.accept(this, CiPriority.PRIMARY);
			this.writeMemberOp(expr.left, expr);
			this.write("size()");
			break;
		case CiId.MATH_NA_N:
			this.write("Float.NaN");
			break;
		case CiId.MATH_NEGATIVE_INFINITY:
			this.write("Float.NEGATIVE_INFINITY");
			break;
		case CiId.MATH_POSITIVE_INFINITY:
			this.write("Float.POSITIVE_INFINITY");
			break;
		default:
			if (!this.writeJavaMatchProperty(expr, parent))
				super.visitSymbolReference(expr, parent);
			break;
		}
	}

	#writeArrayBinarySearchFill(obj, method, args)
	{
		this.include("java.util.Arrays");
		this.write("Arrays.");
		this.write(method);
		this.writeChar(40);
		obj.accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		if (args.length == 3) {
			this.writeStartEnd(args[1], args[2]);
			this.write(", ");
		}
		this.writeNotPromoted(obj.type.asClassType().getElementType(), args[0]);
		this.writeChar(41);
	}

	#writeWrite(method, args, newLine)
	{
		let interpolated;
		if (args.length == 1 && (interpolated = args[0]) instanceof CiInterpolatedString) {
			this.write(".format(");
			this.writePrintf(interpolated, newLine);
		}
		else {
			this.write(".print");
			if (newLine)
				this.write("ln");
			this.writeArgsInParentheses(method, args);
		}
	}

	#writeCompileRegex(args, argIndex)
	{
		this.include("java.util.regex.Pattern");
		this.write("Pattern.compile(");
		args[argIndex].accept(this, CiPriority.ARGUMENT);
		this.writeRegexOptions(args, ", ", " | ", "", "Pattern.CASE_INSENSITIVE", "Pattern.MULTILINE", "Pattern.DOTALL");
		this.writeChar(41);
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.NONE:
		case CiId.CLASS_TO_STRING:
		case CiId.STRING_CONTAINS:
		case CiId.STRING_ENDS_WITH:
		case CiId.STRING_INDEX_OF:
		case CiId.STRING_LAST_INDEX_OF:
		case CiId.STRING_REPLACE:
		case CiId.STRING_STARTS_WITH:
		case CiId.LIST_CLEAR:
		case CiId.LIST_CONTAINS:
		case CiId.LIST_INDEX_OF:
		case CiId.QUEUE_CLEAR:
		case CiId.STACK_CLEAR:
		case CiId.STACK_PEEK:
		case CiId.STACK_PUSH:
		case CiId.STACK_POP:
		case CiId.HASH_SET_ADD:
		case CiId.HASH_SET_CLEAR:
		case CiId.HASH_SET_CONTAINS:
		case CiId.HASH_SET_REMOVE:
		case CiId.SORTED_SET_ADD:
		case CiId.SORTED_SET_CLEAR:
		case CiId.SORTED_SET_CONTAINS:
		case CiId.SORTED_SET_REMOVE:
		case CiId.DICTIONARY_CLEAR:
		case CiId.DICTIONARY_CONTAINS_KEY:
		case CiId.DICTIONARY_REMOVE:
		case CiId.SORTED_DICTIONARY_CLEAR:
		case CiId.SORTED_DICTIONARY_CONTAINS_KEY:
		case CiId.SORTED_DICTIONARY_REMOVE:
		case CiId.ORDERED_DICTIONARY_CLEAR:
		case CiId.ORDERED_DICTIONARY_CONTAINS_KEY:
		case CiId.ORDERED_DICTIONARY_REMOVE:
		case CiId.STRING_WRITER_TO_STRING:
		case CiId.MATH_METHOD:
		case CiId.MATH_ABS:
		case CiId.MATH_MAX_INT:
		case CiId.MATH_MAX_DOUBLE:
		case CiId.MATH_MIN_INT:
		case CiId.MATH_MIN_DOUBLE:
			if (obj != null) {
				if (GenJava.isReferenceTo(obj, CiId.BASE_PTR))
					this.write("super");
				else
					obj.accept(this, CiPriority.PRIMARY);
				this.writeChar(46);
			}
			this.writeName(method);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.ENUM_FROM_INT:
			args[0].accept(this, parent);
			break;
		case CiId.ENUM_HAS_FLAG:
			this.writeEnumHasFlag(obj, args, parent);
			break;
		case CiId.DOUBLE_TRY_PARSE:
			this.include("java.util.function.DoubleSupplier");
			this.write("!Double.isNaN(");
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = ((DoubleSupplier) () -> { try { return Double.parseDouble(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write("); } catch (NumberFormatException e) { return Double.NaN; } }).getAsDouble())");
			break;
		case CiId.STRING_SUBSTRING:
			this.writePostfix(obj, ".substring(");
			args[0].accept(this, CiPriority.ARGUMENT);
			if (args.length == 2) {
				this.write(", ");
				this.writeAdd(args[0], args[1]);
			}
			this.writeChar(41);
			break;
		case CiId.ARRAY_BINARY_SEARCH_ALL:
		case CiId.ARRAY_BINARY_SEARCH_PART:
			this.#writeArrayBinarySearchFill(obj, "binarySearch", args);
			break;
		case CiId.ARRAY_COPY_TO:
			this.write("System.arraycopy(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeArgs(method, args);
			this.writeChar(41);
			break;
		case CiId.ARRAY_FILL_ALL:
		case CiId.ARRAY_FILL_PART:
			this.#writeArrayBinarySearchFill(obj, "fill", args);
			break;
		case CiId.ARRAY_SORT_ALL:
			this.include("java.util.Arrays");
			this.writeCall("Arrays.sort", obj);
			break;
		case CiId.ARRAY_SORT_PART:
			this.include("java.util.Arrays");
			this.write("Arrays.sort(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeStartEnd(args[0], args[1]);
			this.writeChar(41);
			break;
		case CiId.LIST_ADD:
			this.writeListAdd(obj, "add", args);
			break;
		case CiId.LIST_ADD_RANGE:
			this.writeMethodCall(obj, "addAll", args[0]);
			break;
		case CiId.LIST_ALL:
			this.writeMethodCall(obj, "stream().allMatch", args[0]);
			break;
		case CiId.LIST_ANY:
			this.writeMethodCall(obj, "stream().anyMatch", args[0]);
			break;
		case CiId.LIST_COPY_TO:
			this.write("for (int _i = 0; _i < ");
			args[3].accept(this, CiPriority.REL);
			this.writeLine("; _i++)");
			this.write("\t");
			args[1].accept(this, CiPriority.PRIMARY);
			this.writeChar(91);
			this.startAdd(args[2]);
			this.write("_i] = ");
			this.writePostfix(obj, ".get(");
			this.startAdd(args[0]);
			this.write("_i)");
			break;
		case CiId.LIST_INSERT:
			this.writeListInsert(obj, "add", args);
			break;
		case CiId.LIST_LAST:
			this.writePostfix(obj, ".get(");
			this.writePostfix(obj, ".size() - 1)");
			break;
		case CiId.LIST_REMOVE_AT:
			this.writeMethodCall(obj, "remove", args[0]);
			break;
		case CiId.LIST_REMOVE_RANGE:
			this.writePostfix(obj, ".subList(");
			this.writeStartEnd(args[0], args[1]);
			this.write(").clear()");
			break;
		case CiId.LIST_SORT_ALL:
			this.writePostfix(obj, ".sort(null)");
			break;
		case CiId.LIST_SORT_PART:
			this.writePostfix(obj, ".subList(");
			this.writeStartEnd(args[0], args[1]);
			this.write(").sort(null)");
			break;
		case CiId.QUEUE_DEQUEUE:
			this.writePostfix(obj, ".remove()");
			break;
		case CiId.QUEUE_ENQUEUE:
			this.writeMethodCall(obj, "add", args[0]);
			break;
		case CiId.QUEUE_PEEK:
			this.writePostfix(obj, ".element()");
			break;
		case CiId.DICTIONARY_ADD:
			this.writePostfix(obj, ".put(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeNewStorage(obj.type.asClassType().getValueType());
			this.writeChar(41);
			break;
		case CiId.TEXT_WRITER_WRITE:
			if (GenJava.isReferenceTo(obj, CiId.CONSOLE_ERROR)) {
				this.write("System.err");
				this.#writeWrite(method, args, false);
			}
			else {
				this.write("try { ");
				this.writePostfix(obj, ".append(");
				this.#writeToString(args[0], CiPriority.ARGUMENT);
				this.include("java.io.IOException");
				this.write("); } catch (IOException e) { throw new RuntimeException(e); }");
			}
			break;
		case CiId.TEXT_WRITER_WRITE_CHAR:
			if (GenJava.isReferenceTo(obj, CiId.CONSOLE_ERROR))
				this.writeCharMethodCall(obj, "print", args[0]);
			else {
				this.write("try { ");
				this.writeCharMethodCall(obj, "append", args[0]);
				this.include("java.io.IOException");
				this.write("; } catch (IOException e) { throw new RuntimeException(e); }");
			}
			break;
		case CiId.TEXT_WRITER_WRITE_CODE_POINT:
			if (GenJava.isReferenceTo(obj, CiId.CONSOLE_ERROR)) {
				this.writeCall("System.err.print(Character.toChars", args[0]);
				this.writeChar(41);
			}
			else {
				this.write("try { ");
				this.writeMethodCall(obj, "append(Character.toString", args[0]);
				this.include("java.io.IOException");
				this.write("); } catch (IOException e) { throw new RuntimeException(e); }");
			}
			break;
		case CiId.TEXT_WRITER_WRITE_LINE:
			if (GenJava.isReferenceTo(obj, CiId.CONSOLE_ERROR)) {
				this.write("System.err");
				this.#writeWrite(method, args, true);
			}
			else {
				this.write("try { ");
				this.writePostfix(obj, ".append(");
				if (args.length == 0)
					this.write("'\\n'");
				else {
					let interpolated;
					if ((interpolated = args[0]) instanceof CiInterpolatedString) {
						this.write("String.format(");
						this.writePrintf(interpolated, true);
					}
					else {
						this.#writeToString(args[0], CiPriority.ARGUMENT);
						this.write(").append('\\n'");
					}
				}
				this.include("java.io.IOException");
				this.write("); } catch (IOException e) { throw new RuntimeException(e); }");
			}
			break;
		case CiId.STRING_WRITER_CLEAR:
			this.writePostfix(obj, ".getBuffer().setLength(0)");
			break;
		case CiId.CONSOLE_WRITE:
			this.write("System.out");
			this.#writeWrite(method, args, false);
			break;
		case CiId.CONSOLE_WRITE_LINE:
			this.write("System.out");
			this.#writeWrite(method, args, true);
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			this.include("java.nio.charset.StandardCharsets");
			this.writePostfix(args[0], ".getBytes(StandardCharsets.UTF_8).length");
			break;
		case CiId.U_T_F8_GET_BYTES:
			this.include("java.nio.ByteBuffer");
			this.include("java.nio.CharBuffer");
			this.include("java.nio.charset.StandardCharsets");
			this.write("StandardCharsets.UTF_8.newEncoder().encode(CharBuffer.wrap(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write("), ByteBuffer.wrap(");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			args[2].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writePostfix(args[1], ".length");
			if (!args[2].isLiteralZero()) {
				this.write(" - ");
				args[2].accept(this, CiPriority.MUL);
			}
			this.write("), true)");
			break;
		case CiId.U_T_F8_GET_STRING:
			this.include("java.nio.charset.StandardCharsets");
			this.write("new String(");
			this.writeArgs(method, args);
			this.write(", StandardCharsets.UTF_8)");
			break;
		case CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE:
			this.writeCall("System.getenv", args[0]);
			break;
		case CiId.REGEX_COMPILE:
			this.#writeCompileRegex(args, 0);
			break;
		case CiId.REGEX_ESCAPE:
			this.include("java.util.regex.Pattern");
			this.writeCall("Pattern.quote", args[0]);
			break;
		case CiId.REGEX_IS_MATCH_STR:
			this.#writeCompileRegex(args, 1);
			this.writeCall(".matcher", args[0]);
			this.write(".find()");
			break;
		case CiId.REGEX_IS_MATCH_REGEX:
			this.writeMethodCall(obj, "matcher", args[0]);
			this.write(".find()");
			break;
		case CiId.MATCH_FIND_STR:
		case CiId.MATCH_FIND_REGEX:
			this.writeChar(40);
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			if (method.id == CiId.MATCH_FIND_STR)
				this.#writeCompileRegex(args, 1);
			else
				args[1].accept(this, CiPriority.PRIMARY);
			this.writeCall(".matcher", args[0]);
			this.write(").find()");
			break;
		case CiId.MATCH_GET_CAPTURE:
			this.writeMethodCall(obj, "group", args[0]);
			break;
		case CiId.MATH_CEILING:
			this.writeCall("Math.ceil", args[0]);
			break;
		case CiId.MATH_CLAMP:
			this.write("Math.min(Math.max(");
			this.writeClampAsMinMax(args);
			break;
		case CiId.MATH_FUSED_MULTIPLY_ADD:
			this.writeCall("Math.fma", args[0], args[1], args[2]);
			break;
		case CiId.MATH_IS_FINITE:
			this.writeCall("Double.isFinite", args[0]);
			break;
		case CiId.MATH_IS_INFINITY:
			this.writeCall("Double.isInfinite", args[0]);
			break;
		case CiId.MATH_IS_NA_N:
			this.writeCall("Double.isNaN", args[0]);
			break;
		case CiId.MATH_LOG2:
			if (parent > CiPriority.MUL)
				this.writeChar(40);
			this.writeCall("Math.log", args[0]);
			this.write(" * 1.4426950408889635");
			if (parent > CiPriority.MUL)
				this.writeChar(41);
			break;
		case CiId.MATH_ROUND:
			this.writeCall("Math.rint", args[0]);
			break;
		default:
			this.notSupported(obj, method.name);
			break;
		}
	}

	writeIndexingExpr(expr, parent)
	{
		if (parent != CiPriority.ASSIGN && GenJava.#isUnsignedByte(expr.type)) {
			if (parent > CiPriority.AND)
				this.writeChar(40);
			this.#writeIndexingInternal(expr);
			this.write(" & 0xff");
			if (parent > CiPriority.AND)
				this.writeChar(41);
		}
		else
			this.#writeIndexingInternal(expr);
	}

	isPromoted(expr)
	{
		return super.isPromoted(expr) || GenJava.#isUnsignedByteIndexing(expr);
	}

	writeAssignRight(expr)
	{
		let rightBinary;
		if (!GenJava.#isUnsignedByteIndexing(expr.left) && (rightBinary = expr.right) instanceof CiBinaryExpr && rightBinary.isAssign() && GenJava.#isUnsignedByte(expr.right.type)) {
			this.writeChar(40);
			super.writeAssignRight(expr);
			this.write(") & 0xff");
		}
		else
			super.writeAssignRight(expr);
	}

	writeAssign(expr, parent)
	{
		let indexing;
		let klass;
		if ((indexing = expr.left) instanceof CiBinaryExpr && indexing.op == CiToken.LEFT_BRACKET && (klass = indexing.left.type) instanceof CiClassType && !klass.isArray()) {
			this.writePostfix(indexing.left, klass.class.id == CiId.LIST_CLASS ? ".set(" : ".put(");
			indexing.right.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeNotPromoted(expr.type, expr.right);
			this.writeChar(41);
		}
		else
			super.writeAssign(expr, parent);
	}

	getIsOperator()
	{
		return " instanceof ";
	}

	writeVar(def)
	{
		if (def.type.isFinal() && !def.isAssignableStorage())
			this.write("final ");
		super.writeVar(def);
	}

	hasInitCode(def)
	{
		return (def.type instanceof CiArrayStorageType && def.type.getStorageType() instanceof CiStorageType) || super.hasInitCode(def);
	}

	writeInitCode(def)
	{
		if (!this.hasInitCode(def))
			return;
		let array;
		if ((array = def.type) instanceof CiArrayStorageType) {
			let nesting = 0;
			let innerArray;
			while ((innerArray = array.getElementType()) instanceof CiArrayStorageType) {
				this.openLoop("int", nesting++, array.length);
				array = innerArray;
			}
			this.openLoop("int", nesting++, array.length);
			this.writeArrayElement(def, nesting);
			this.write(" = ");
			const storage = array.getElementType();
			this.writeNew(storage, CiPriority.ARGUMENT);
			this.writeCharLine(59);
			while (--nesting >= 0)
				this.closeBlock();
		}
		else
			super.writeInitCode(def);
	}

	visitLambdaExpr(expr)
	{
		this.writeName(expr.first);
		this.write(" -> ");
		expr.body.accept(this, CiPriority.STATEMENT);
	}

	defineIsVar(binary)
	{
	}

	writeAssert(statement)
	{
		if (statement.completesNormally()) {
			this.write("assert ");
			statement.cond.accept(this, CiPriority.ARGUMENT);
			if (statement.message != null) {
				this.write(" : ");
				statement.message.accept(this, CiPriority.ARGUMENT);
			}
		}
		else {
			this.write("throw new AssertionError(");
			if (statement.message != null)
				statement.message.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
		}
		this.writeCharLine(59);
	}

	visitForeach(statement)
	{
		this.write("for (");
		const klass = statement.collection.type;
		switch (klass.class.id) {
		case CiId.STRING_CLASS:
			this.write("int _i = 0; _i < ");
			this.writeStringLength(statement.collection);
			this.write("; _i++) ");
			this.openBlock();
			this.writeTypeAndName(statement.getVar());
			this.write(" = ");
			statement.collection.accept(this, CiPriority.PRIMARY);
			this.writeLine(".charAt(_i);");
			this.flattenBlock(statement.body);
			this.closeBlock();
			return;
		case CiId.DICTIONARY_CLASS:
		case CiId.SORTED_DICTIONARY_CLASS:
		case CiId.ORDERED_DICTIONARY_CLASS:
			this.include("java.util.Map");
			this.#writeDictType("Map.Entry", klass);
			this.writeChar(32);
			this.write(statement.getVar().name);
			this.write(" : ");
			this.writePostfix(statement.collection, ".entrySet()");
			break;
		default:
			this.writeTypeAndName(statement.getVar());
			this.write(" : ");
			statement.collection.accept(this, CiPriority.ARGUMENT);
			break;
		}
		this.writeChar(41);
		this.writeChild(statement.body);
	}

	visitLock(statement)
	{
		this.writeCall("synchronized ", statement.lock);
		this.writeChild(statement.body);
	}

	writeSwitchValue(expr)
	{
		if (GenJava.#isUnsignedByteIndexing(expr)) {
			const indexing = expr;
			this.#writeIndexingInternal(indexing);
		}
		else
			super.writeSwitchValue(expr);
	}

	#writeSwitchCaseVar(expr)
	{
		expr.accept(this, CiPriority.ARGUMENT);
		let def;
		if ((def = expr) instanceof CiVar && def.name == "_") {
			this.visitLiteralLong(BigInt(this.#switchCaseDiscards++));
			return true;
		}
		return false;
	}

	writeSwitchCase(statement, kase)
	{
		if (statement.isTypeMatching()) {
			for (const expr of kase.values) {
				this.write("case ");
				let discard;
				let when1;
				if ((when1 = expr) instanceof CiBinaryExpr) {
					discard = this.#writeSwitchCaseVar(when1.left);
					this.write(" when ");
					when1.right.accept(this, CiPriority.ARGUMENT);
				}
				else
					discard = this.#writeSwitchCaseVar(expr);
				this.writeCharLine(58);
				this.indent++;
				this.writeSwitchCaseBody(kase.body);
				this.indent--;
				if (discard)
					this.#switchCaseDiscards--;
			}
		}
		else
			super.writeSwitchCase(statement, kase);
	}

	visitThrow(statement)
	{
		this.write("throw new Exception(");
		statement.message.accept(this, CiPriority.ARGUMENT);
		this.writeLine(");");
	}

	#createJavaFile(className)
	{
		this.createFile(this.outputFile, className + ".java");
		if (this.namespace.length != 0) {
			this.write("package ");
			this.write(this.namespace);
			this.writeCharLine(59);
		}
	}

	visitEnumValue(konst, previous)
	{
		this.writeDoc(konst.documentation);
		this.write("int ");
		this.writeUppercaseWithUnderscores(konst.name);
		this.write(" = ");
		let imp;
		if ((imp = konst.value) instanceof CiImplicitEnumValue)
			this.visitLiteralLong(BigInt(imp.value));
		else
			konst.value.accept(this, CiPriority.ARGUMENT);
		this.writeCharLine(59);
	}

	writeEnum(enu)
	{
		this.#createJavaFile(enu.name);
		this.writeNewLine();
		this.writeDoc(enu.documentation);
		this.writePublic(enu);
		this.write("interface ");
		this.writeLine(enu.name);
		this.openBlock();
		enu.acceptValues(this);
		this.closeBlock();
		this.closeFile();
	}

	#writeSignature(method, paramCount)
	{
		this.writeNewLine();
		this.writeMethodDoc(method);
		this.#writeVisibility(method.visibility);
		switch (method.callType) {
		case CiCallType.STATIC:
			this.write("static ");
			break;
		case CiCallType.VIRTUAL:
			break;
		case CiCallType.ABSTRACT:
			this.write("abstract ");
			break;
		case CiCallType.OVERRIDE:
			this.write("@Override ");
			break;
		case CiCallType.NORMAL:
			if (method.visibility != CiVisibility.PRIVATE)
				this.write("final ");
			break;
		case CiCallType.SEALED:
			this.write("final @Override ");
			break;
		default:
			throw new Error();
		}
		this.writeTypeAndName(method);
		this.writeChar(40);
		let param = method.parameters.firstParameter();
		for (let i = 0; i < paramCount; i++) {
			if (i > 0)
				this.write(", ");
			this.writeTypeAndName(param);
			param = param.nextParameter();
		}
		this.writeChar(41);
		if (method.throws)
			this.write(" throws Exception");
	}

	#writeOverloads(method, paramCount)
	{
		if (paramCount + 1 < method.parameters.count())
			this.#writeOverloads(method, paramCount + 1);
		this.#writeSignature(method, paramCount);
		this.writeNewLine();
		this.openBlock();
		if (method.type.id != CiId.VOID_TYPE)
			this.write("return ");
		this.writeName(method);
		this.writeChar(40);
		let param = method.parameters.firstParameter();
		for (let i = 0; i < paramCount; i++) {
			this.writeName(param);
			this.write(", ");
			param = param.nextParameter();
		}
		param.value.accept(this, CiPriority.ARGUMENT);
		this.writeLine(");");
		this.closeBlock();
	}

	writeConst(konst)
	{
		this.writeNewLine();
		this.writeDoc(konst.documentation);
		this.#writeVisibility(konst.visibility);
		this.write("static final ");
		this.writeTypeAndName(konst);
		this.write(" = ");
		this.writeCoercedExpr(konst.type, konst.value);
		this.writeCharLine(59);
	}

	writeField(field)
	{
		this.writeDoc(field.documentation);
		this.#writeVisibility(field.visibility);
		this.writeVar(field);
		this.writeCharLine(59);
	}

	writeMethod(method)
	{
		this.#writeSignature(method, method.parameters.count());
		this.writeBody(method);
		let i = 0;
		for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
			if (param.value != null) {
				this.#writeOverloads(method, i);
				break;
			}
			i++;
		}
	}

	writeClass(klass, program)
	{
		this.openStringWriter();
		this.writeDoc(klass.documentation);
		this.writePublic(klass);
		switch (klass.callType) {
		case CiCallType.NORMAL:
			break;
		case CiCallType.ABSTRACT:
			this.write("abstract ");
			break;
		case CiCallType.STATIC:
		case CiCallType.SEALED:
			this.write("final ");
			break;
		default:
			throw new Error();
		}
		this.openClass(klass, "", " extends ");
		if (klass.callType == CiCallType.STATIC) {
			this.write("private ");
			this.write(klass.name);
			this.writeLine("()");
			this.openBlock();
			this.closeBlock();
		}
		else if (this.needsConstructor(klass)) {
			if (klass.constructor_ != null) {
				this.writeDoc(klass.constructor_.documentation);
				this.#writeVisibility(klass.constructor_.visibility);
			}
			this.write(klass.name);
			this.writeLine("()");
			this.openBlock();
			this.writeConstructorBody(klass);
			this.closeBlock();
		}
		this.writeMembers(klass, true);
		this.closeBlock();
		this.#createJavaFile(klass.name);
		this.writeTopLevelNatives(program);
		this.writeIncludes("import ", ";");
		this.writeNewLine();
		this.closeStringWriter();
		this.closeFile();
	}

	#writeResources()
	{
		this.#createJavaFile("CiResource");
		this.writeLine("import java.io.DataInputStream;");
		this.writeLine("import java.io.IOException;");
		this.writeNewLine();
		this.write("class CiResource");
		this.writeNewLine();
		this.openBlock();
		this.writeLine("static byte[] getByteArray(String name, int length)");
		this.openBlock();
		this.write("DataInputStream dis = new DataInputStream(");
		this.writeLine("CiResource.class.getResourceAsStream(name));");
		this.writeLine("byte[] result = new byte[length];");
		this.write("try ");
		this.openBlock();
		this.write("try ");
		this.openBlock();
		this.writeLine("dis.readFully(result);");
		this.closeBlock();
		this.write("finally ");
		this.openBlock();
		this.writeLine("dis.close();");
		this.closeBlock();
		this.closeBlock();
		this.write("catch (IOException e) ");
		this.openBlock();
		this.writeLine("throw new RuntimeException();");
		this.closeBlock();
		this.writeLine("return result;");
		this.closeBlock();
		this.closeBlock();
		this.closeFile();
	}

	writeProgram(program)
	{
		this.#switchCaseDiscards = 0;
		this.writeTypes(program);
		if (Object.keys(program.resources).length > 0)
			this.#writeResources();
	}
}

export class GenJsNoModule extends GenBase
{
	#switchesWithLabel = [];
	#stringWriter = false;

	getTargetName()
	{
		return "JavaScript";
	}

	#writeCamelCaseNotKeyword(name)
	{
		this.writeCamelCase(name);
		switch (name) {
		case "Constructor":
		case "arguments":
		case "await":
		case "catch":
		case "debugger":
		case "delete":
		case "export":
		case "extends":
		case "finally":
		case "function":
		case "implements":
		case "import":
		case "instanceof":
		case "interface":
		case "let":
		case "package":
		case "private":
		case "super":
		case "try":
		case "typeof":
		case "var":
		case "with":
		case "yield":
			this.writeChar(95);
			break;
		default:
			break;
		}
	}

	writeName(symbol)
	{
		if (symbol instanceof CiContainerType)
			this.write(symbol.name);
		else if (symbol instanceof CiConst) {
			const konst = symbol;
			if (konst.inMethod != null) {
				this.writeUppercaseWithUnderscores(konst.inMethod.name);
				this.writeChar(95);
			}
			this.writeUppercaseWithUnderscores(symbol.name);
		}
		else if (symbol instanceof CiVar)
			this.#writeCamelCaseNotKeyword(symbol.name);
		else if (symbol instanceof CiMember) {
			const member = symbol;
			if (member.visibility == CiVisibility.PRIVATE) {
				this.writeChar(35);
				this.writeCamelCase(symbol.name);
				if (symbol.name == "Constructor")
					this.writeChar(95);
			}
			else
				this.#writeCamelCaseNotKeyword(symbol.name);
		}
		else
			throw new Error();
	}

	writeTypeAndName(value)
	{
		this.writeName(value);
	}

	writeArrayElementType(type)
	{
		switch (type.id) {
		case CiId.S_BYTE_RANGE:
			this.write("Int8");
			break;
		case CiId.BYTE_RANGE:
			this.write("Uint8");
			break;
		case CiId.SHORT_RANGE:
			this.write("Int16");
			break;
		case CiId.U_SHORT_RANGE:
			this.write("Uint16");
			break;
		case CiId.INT_TYPE:
			this.write("Int32");
			break;
		case CiId.LONG_TYPE:
			this.write("BigInt64");
			break;
		case CiId.FLOAT_TYPE:
			this.write("Float32");
			break;
		case CiId.DOUBLE_TYPE:
			this.write("Float64");
			break;
		default:
			throw new Error();
		}
	}

	visitAggregateInitializer(expr)
	{
		const array = expr.type;
		let numeric = false;
		let number;
		if ((number = array.getElementType()) instanceof CiNumericType) {
			this.write("new ");
			this.writeArrayElementType(number);
			this.write("Array(");
			numeric = true;
		}
		this.write("[ ");
		this.writeCoercedLiterals(null, expr.items);
		this.write(" ]");
		if (numeric)
			this.writeChar(41);
	}

	writeNew(klass, parent)
	{
		switch (klass.class.id) {
		case CiId.LIST_CLASS:
		case CiId.QUEUE_CLASS:
		case CiId.STACK_CLASS:
			this.write("[]");
			break;
		case CiId.HASH_SET_CLASS:
		case CiId.SORTED_SET_CLASS:
			this.write("new Set()");
			break;
		case CiId.DICTIONARY_CLASS:
		case CiId.SORTED_DICTIONARY_CLASS:
			this.write("{}");
			break;
		case CiId.ORDERED_DICTIONARY_CLASS:
			this.write("new Map()");
			break;
		case CiId.LOCK_CLASS:
			this.notSupported(klass, "Lock");
			break;
		default:
			this.write("new ");
			if (klass.class.id == CiId.STRING_WRITER_CLASS)
				this.#stringWriter = true;
			this.write(klass.class.name);
			this.write("()");
			break;
		}
	}

	writeNewWithFields(type, init)
	{
		this.write("Object.assign(");
		this.writeNew(type, CiPriority.ARGUMENT);
		this.writeChar(44);
		this.writeObjectLiteral(init, ": ");
		this.writeChar(41);
	}

	writeVar(def)
	{
		this.write(def.type.isFinal() && !def.isAssignableStorage() ? "const " : "let ");
		super.writeVar(def);
	}

	#writeInterpolatedLiteral(s)
	{
		let i = 0;
		for (const c of s) {
			i++;
			if (c.codePointAt(0) == 96 || (c.codePointAt(0) == 36 && i < s.length && s.charCodeAt(i) == 123))
				this.writeChar(92);
			this.writeChar(c.codePointAt(0));
		}
	}

	visitInterpolatedString(expr, parent)
	{
		this.writeChar(96);
		for (const part of expr.parts) {
			this.#writeInterpolatedLiteral(part.prefix);
			this.write("${");
			if (part.width != 0 || part.format != 32) {
				if (part.argument instanceof CiLiteralLong || part.argument instanceof CiPrefixExpr) {
					this.writeChar(40);
					part.argument.accept(this, CiPriority.PRIMARY);
					this.writeChar(41);
				}
				else
					part.argument.accept(this, CiPriority.PRIMARY);
				if (part.argument.type instanceof CiNumericType) {
					switch (part.format) {
					case 69:
						this.write(".toExponential(");
						if (part.precision >= 0)
							this.visitLiteralLong(BigInt(part.precision));
						this.write(").toUpperCase()");
						break;
					case 101:
						this.write(".toExponential(");
						if (part.precision >= 0)
							this.visitLiteralLong(BigInt(part.precision));
						this.writeChar(41);
						break;
					case 70:
					case 102:
						this.write(".toFixed(");
						if (part.precision >= 0)
							this.visitLiteralLong(BigInt(part.precision));
						this.writeChar(41);
						break;
					case 88:
						this.write(".toString(16).toUpperCase()");
						break;
					case 120:
						this.write(".toString(16)");
						break;
					default:
						this.write(".toString()");
						break;
					}
					if (part.precision >= 0) {
						switch (part.format) {
						case 68:
						case 100:
						case 88:
						case 120:
							this.write(".padStart(");
							this.visitLiteralLong(BigInt(part.precision));
							this.write(", \"0\")");
							break;
						default:
							break;
						}
					}
				}
				if (part.width > 0) {
					this.write(".padStart(");
					this.visitLiteralLong(BigInt(part.width));
					this.writeChar(41);
				}
				else if (part.width < 0) {
					this.write(".padEnd(");
					this.visitLiteralLong(BigInt(-part.width));
					this.writeChar(41);
				}
			}
			else
				part.argument.accept(this, CiPriority.ARGUMENT);
			this.writeChar(125);
		}
		this.#writeInterpolatedLiteral(expr.suffix);
		this.writeChar(96);
	}

	writeLocalName(symbol, parent)
	{
		let member;
		if ((member = symbol) instanceof CiMember) {
			if (!member.isStatic())
				this.write("this");
			else if (this.currentMethod != null)
				this.write(this.currentMethod.parent.name);
			else {
				let konst;
				if ((konst = symbol) instanceof CiConst) {
					konst.value.accept(this, parent);
					return;
				}
				else
					throw new Error();
			}
			this.writeChar(46);
		}
		this.writeName(symbol);
		let forEach;
		if ((forEach = symbol.parent) instanceof CiForeach && forEach.collection.type instanceof CiStringType)
			this.write(".codePointAt(0)");
	}

	writeCoercedInternal(type, expr, parent)
	{
		if (type instanceof CiNumericType) {
			if (type.id == CiId.LONG_TYPE) {
				if (expr instanceof CiLiteralLong) {
					expr.accept(this, CiPriority.PRIMARY);
					this.writeChar(110);
					return;
				}
				if (expr.type.id != CiId.LONG_TYPE) {
					this.writeCall("BigInt", expr);
					return;
				}
			}
			else if (expr.type.id == CiId.LONG_TYPE) {
				this.writeCall("Number", expr);
				return;
			}
		}
		expr.accept(this, parent);
	}

	writeNewArray(elementType, lengthExpr, parent)
	{
		this.write("new ");
		if (elementType instanceof CiNumericType)
			this.writeArrayElementType(elementType);
		this.writeCall("Array", lengthExpr);
	}

	hasInitCode(def)
	{
		let array;
		return (array = def.type) instanceof CiArrayStorageType && array.getElementType() instanceof CiStorageType;
	}

	writeInitCode(def)
	{
		if (!this.hasInitCode(def))
			return;
		let array = def.type;
		let nesting = 0;
		let innerArray;
		while ((innerArray = array.getElementType()) instanceof CiArrayStorageType) {
			this.openLoop("let", nesting++, array.length);
			this.writeArrayElement(def, nesting);
			this.write(" = ");
			this.writeNewArray(innerArray.getElementType(), innerArray.lengthExpr, CiPriority.ARGUMENT);
			this.writeCharLine(59);
			array = innerArray;
		}
		let klass;
		if ((klass = array.getElementType()) instanceof CiStorageType) {
			this.openLoop("let", nesting++, array.length);
			this.writeArrayElement(def, nesting);
			this.write(" = ");
			this.writeNew(klass, CiPriority.ARGUMENT);
			this.writeCharLine(59);
		}
		while (--nesting >= 0)
			this.closeBlock();
	}

	writeResource(name, length)
	{
		this.write("Ci.");
		this.writeResourceName(name);
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.CONSOLE_ERROR:
			this.write("process.stderr");
			break;
		case CiId.LIST_COUNT:
		case CiId.QUEUE_COUNT:
		case CiId.STACK_COUNT:
			this.writePostfix(expr.left, ".length");
			break;
		case CiId.HASH_SET_COUNT:
		case CiId.SORTED_SET_COUNT:
		case CiId.ORDERED_DICTIONARY_COUNT:
			this.writePostfix(expr.left, ".size");
			break;
		case CiId.DICTIONARY_COUNT:
		case CiId.SORTED_DICTIONARY_COUNT:
			this.writeCall("Object.keys", expr.left);
			this.write(".length");
			break;
		case CiId.MATCH_START:
			this.writePostfix(expr.left, ".index");
			break;
		case CiId.MATCH_END:
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			this.writePostfix(expr.left, ".index + ");
			this.writePostfix(expr.left, "[0].length");
			if (parent > CiPriority.ADD)
				this.writeChar(41);
			break;
		case CiId.MATCH_LENGTH:
			this.writePostfix(expr.left, "[0].length");
			break;
		case CiId.MATCH_VALUE:
			this.writePostfix(expr.left, "[0]");
			break;
		case CiId.MATH_NA_N:
			this.write("NaN");
			break;
		case CiId.MATH_NEGATIVE_INFINITY:
			this.write("-Infinity");
			break;
		case CiId.MATH_POSITIVE_INFINITY:
			this.write("Infinity");
			break;
		default:
			super.visitSymbolReference(expr, parent);
			break;
		}
	}

	writeStringLength(expr)
	{
		this.writePostfix(expr, ".length");
	}

	writeCharAt(expr)
	{
		this.writeMethodCall(expr.left, "charCodeAt", expr.right);
	}

	writeBinaryOperand(expr, parent, binary)
	{
		this.writeCoerced(binary.isRel() ? expr.type : binary.type, expr, parent);
	}

	static #isIdentifier(s)
	{
		if (s.length == 0 || s.charCodeAt(0) < 65)
			return false;
		for (const c of s) {
			if (!CiLexer.isLetterOrDigit(c.codePointAt(0)))
				return false;
		}
		return true;
	}

	#writeNewRegex(args, argIndex)
	{
		let pattern = args[argIndex];
		let literal;
		if ((literal = pattern) instanceof CiLiteralString) {
			this.writeChar(47);
			let escaped = false;
			for (const c of literal.value) {
				switch (c.codePointAt(0)) {
				case 92:
					if (!escaped) {
						escaped = true;
						continue;
					}
					escaped = false;
					break;
				case 34:
				case 39:
					escaped = false;
					break;
				case 47:
					escaped = true;
					break;
				default:
					break;
				}
				if (escaped) {
					this.writeChar(92);
					escaped = false;
				}
				this.writeChar(c.codePointAt(0));
			}
			this.writeChar(47);
			this.writeRegexOptions(args, "", "", "", "i", "m", "s");
		}
		else {
			this.write("new RegExp(");
			pattern.accept(this, CiPriority.ARGUMENT);
			this.writeRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
			this.writeChar(41);
		}
	}

	static #hasLong(args)
	{
		return args.some(arg => arg.type.id == CiId.LONG_TYPE);
	}

	#writeMathMaxMin(method, name, op, args)
	{
		if (GenJsNoModule.#hasLong(args)) {
			this.write("((x, y) => x ");
			this.writeChar(op);
			this.write(" y ? x : y)");
			this.writeArgsInParentheses(method, args);
		}
		else
			this.writeCall(name, args[0], args[1]);
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.NONE:
		case CiId.CLASS_TO_STRING:
		case CiId.STRING_ENDS_WITH:
		case CiId.STRING_INDEX_OF:
		case CiId.STRING_LAST_INDEX_OF:
		case CiId.STRING_STARTS_WITH:
		case CiId.ARRAY_SORT_ALL:
		case CiId.LIST_INDEX_OF:
		case CiId.STACK_PUSH:
		case CiId.STACK_POP:
		case CiId.HASH_SET_ADD:
		case CiId.HASH_SET_CLEAR:
		case CiId.SORTED_SET_ADD:
		case CiId.SORTED_SET_CLEAR:
		case CiId.ORDERED_DICTIONARY_CLEAR:
		case CiId.STRING_WRITER_CLEAR:
		case CiId.STRING_WRITER_TO_STRING:
		case CiId.MATH_METHOD:
		case CiId.MATH_LOG2:
		case CiId.MATH_MAX_DOUBLE:
		case CiId.MATH_MIN_DOUBLE:
		case CiId.MATH_ROUND:
			if (obj == null)
				this.writeLocalName(method, CiPriority.PRIMARY);
			else {
				if (GenJsNoModule.isReferenceTo(obj, CiId.BASE_PTR))
					this.write("super");
				else
					obj.accept(this, CiPriority.PRIMARY);
				this.writeChar(46);
				this.writeName(method);
			}
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.ENUM_FROM_INT:
			args[0].accept(this, parent);
			break;
		case CiId.ENUM_HAS_FLAG:
			this.writeEnumHasFlag(obj, args, parent);
			break;
		case CiId.INT_TRY_PARSE:
			this.write("!isNaN(");
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = parseInt(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeTryParseRadix(args);
			this.write("))");
			break;
		case CiId.LONG_TRY_PARSE:
			if (args.length != 1)
				this.notSupported(args[1], "Radix");
			this.write("(() => { try { ");
			obj.accept(this, CiPriority.ASSIGN);
			this.write("  = BigInt(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write("); return true; } catch { return false; }})()");
			break;
		case CiId.DOUBLE_TRY_PARSE:
			this.write("!isNaN(");
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = parseFloat(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write("))");
			break;
		case CiId.STRING_CONTAINS:
		case CiId.ARRAY_CONTAINS:
		case CiId.LIST_CONTAINS:
			this.writeMethodCall(obj, "includes", args[0]);
			break;
		case CiId.STRING_REPLACE:
			this.writeMethodCall(obj, "replaceAll", args[0], args[1]);
			break;
		case CiId.STRING_SUBSTRING:
			this.writePostfix(obj, ".substring(");
			args[0].accept(this, CiPriority.ARGUMENT);
			if (args.length == 2) {
				this.write(", ");
				this.writeAdd(args[0], args[1]);
			}
			this.writeChar(41);
			break;
		case CiId.ARRAY_FILL_ALL:
		case CiId.ARRAY_FILL_PART:
			this.writePostfix(obj, ".fill(");
			args[0].accept(this, CiPriority.ARGUMENT);
			if (args.length == 3) {
				this.write(", ");
				this.writeStartEnd(args[1], args[2]);
			}
			this.writeChar(41);
			break;
		case CiId.ARRAY_COPY_TO:
		case CiId.LIST_COPY_TO:
			args[1].accept(this, CiPriority.PRIMARY);
			let sourceStorage;
			let literalLength;
			let wholeSource = (sourceStorage = obj.type) instanceof CiArrayStorageType && args[0].isLiteralZero() && (literalLength = args[3]) instanceof CiLiteralLong && literalLength.value == sourceStorage.length;
			if (obj.type.asClassType().getElementType() instanceof CiNumericType) {
				this.write(".set(");
				if (wholeSource)
					obj.accept(this, CiPriority.ARGUMENT);
				else {
					this.writePostfix(obj, method.id == CiId.ARRAY_COPY_TO ? ".subarray(" : ".slice(");
					this.writeStartEnd(args[0], args[3]);
					this.writeChar(41);
				}
				if (!args[2].isLiteralZero()) {
					this.write(", ");
					args[2].accept(this, CiPriority.ARGUMENT);
				}
			}
			else {
				this.write(".splice(");
				args[2].accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				args[3].accept(this, CiPriority.ARGUMENT);
				this.write(", ...");
				obj.accept(this, CiPriority.PRIMARY);
				if (!wholeSource) {
					this.write(".slice(");
					this.writeStartEnd(args[0], args[3]);
					this.writeChar(41);
				}
			}
			this.writeChar(41);
			break;
		case CiId.ARRAY_SORT_PART:
			this.writePostfix(obj, ".subarray(");
			this.writeStartEnd(args[0], args[1]);
			this.write(").sort()");
			break;
		case CiId.LIST_ADD:
			this.writeListAdd(obj, "push", args);
			break;
		case CiId.LIST_ADD_RANGE:
			this.writePostfix(obj, ".push(...");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.LIST_ALL:
			this.writeMethodCall(obj, "every", args[0]);
			break;
		case CiId.LIST_ANY:
			this.writeMethodCall(obj, "some", args[0]);
			break;
		case CiId.LIST_CLEAR:
		case CiId.QUEUE_CLEAR:
		case CiId.STACK_CLEAR:
			this.writePostfix(obj, ".length = 0");
			break;
		case CiId.LIST_INSERT:
			this.writeListInsert(obj, "splice", args, ", 0, ");
			break;
		case CiId.LIST_LAST:
		case CiId.STACK_PEEK:
			this.writePostfix(obj, ".at(-1)");
			break;
		case CiId.LIST_REMOVE_AT:
			this.writePostfix(obj, ".splice(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", 1)");
			break;
		case CiId.LIST_REMOVE_RANGE:
			this.writeMethodCall(obj, "splice", args[0], args[1]);
			break;
		case CiId.LIST_SORT_ALL:
			this.writePostfix(obj, ".sort((a, b) => a - b)");
			break;
		case CiId.LIST_SORT_PART:
			this.writePostfix(obj, ".splice(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.write(", ...");
			this.writePostfix(obj, ".slice(");
			this.writeStartEnd(args[0], args[1]);
			this.write(").sort((a, b) => a - b))");
			break;
		case CiId.QUEUE_DEQUEUE:
			this.writePostfix(obj, ".shift()");
			break;
		case CiId.QUEUE_ENQUEUE:
			this.writeMethodCall(obj, "push", args[0]);
			break;
		case CiId.QUEUE_PEEK:
			this.writePostfix(obj, "[0]");
			break;
		case CiId.HASH_SET_CONTAINS:
		case CiId.SORTED_SET_CONTAINS:
		case CiId.ORDERED_DICTIONARY_CONTAINS_KEY:
			this.writeMethodCall(obj, "has", args[0]);
			break;
		case CiId.HASH_SET_REMOVE:
		case CiId.SORTED_SET_REMOVE:
		case CiId.ORDERED_DICTIONARY_REMOVE:
			this.writeMethodCall(obj, "delete", args[0]);
			break;
		case CiId.DICTIONARY_ADD:
			this.writeDictionaryAdd(obj, args);
			break;
		case CiId.DICTIONARY_CLEAR:
		case CiId.SORTED_DICTIONARY_CLEAR:
			this.write("for (const key in ");
			obj.accept(this, CiPriority.ARGUMENT);
			this.writeCharLine(41);
			this.write("\tdelete ");
			this.writePostfix(obj, "[key];");
			break;
		case CiId.DICTIONARY_CONTAINS_KEY:
		case CiId.SORTED_DICTIONARY_CONTAINS_KEY:
			this.writeMethodCall(obj, "hasOwnProperty", args[0]);
			break;
		case CiId.DICTIONARY_REMOVE:
		case CiId.SORTED_DICTIONARY_REMOVE:
			this.write("delete ");
			this.writeIndexing(obj, args[0]);
			break;
		case CiId.TEXT_WRITER_WRITE:
			this.writePostfix(obj, ".write(");
			if (args[0].type instanceof CiStringType)
				args[0].accept(this, CiPriority.ARGUMENT);
			else
				this.writeCall("String", args[0]);
			this.writeChar(41);
			break;
		case CiId.TEXT_WRITER_WRITE_CHAR:
			this.writeMethodCall(obj, "write(String.fromCharCode", args[0]);
			this.writeChar(41);
			break;
		case CiId.TEXT_WRITER_WRITE_CODE_POINT:
			this.writeMethodCall(obj, "write(String.fromCodePoint", args[0]);
			this.writeChar(41);
			break;
		case CiId.TEXT_WRITER_WRITE_LINE:
			if (GenJsNoModule.isReferenceTo(obj, CiId.CONSOLE_ERROR)) {
				this.write("console.error(");
				if (args.length == 0)
					this.write("\"\"");
				else
					args[0].accept(this, CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			else {
				this.writePostfix(obj, ".write(");
				if (args.length != 0) {
					args[0].accept(this, CiPriority.ADD);
					this.write(" + ");
				}
				this.write("\"\\n\")");
			}
			break;
		case CiId.CONSOLE_WRITE:
			this.write("process.stdout.write(");
			if (args[0].type instanceof CiStringType)
				args[0].accept(this, CiPriority.ARGUMENT);
			else
				this.writeCall("String", args[0]);
			this.writeChar(41);
			break;
		case CiId.CONSOLE_WRITE_LINE:
			this.write("console.log(");
			if (args.length == 0)
				this.write("\"\"");
			else
				args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			this.write("new TextEncoder().encode(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(").length");
			break;
		case CiId.U_T_F8_GET_BYTES:
			this.write("new TextEncoder().encodeInto(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			if (args[2].isLiteralZero())
				args[1].accept(this, CiPriority.ARGUMENT);
			else
				this.writeMethodCall(args[1], "subarray", args[2]);
			this.writeChar(41);
			break;
		case CiId.U_T_F8_GET_STRING:
			this.write("new TextDecoder().decode(");
			this.writePostfix(args[0], ".subarray(");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			this.writeAdd(args[1], args[2]);
			this.write("))");
			break;
		case CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE:
			let literal;
			if ((literal = args[0]) instanceof CiLiteralString && GenJsNoModule.#isIdentifier(literal.value)) {
				this.write("process.env.");
				this.write(literal.value);
			}
			else {
				this.write("process.env[");
				args[0].accept(this, CiPriority.ARGUMENT);
				this.writeChar(93);
			}
			break;
		case CiId.REGEX_COMPILE:
			this.#writeNewRegex(args, 0);
			break;
		case CiId.REGEX_ESCAPE:
			this.writePostfix(args[0], ".replace(/[-\\/\\\\^$*+?.()|[\\]{}]/g, '\\\\$&')");
			break;
		case CiId.REGEX_IS_MATCH_STR:
			this.#writeNewRegex(args, 1);
			this.writeCall(".test", args[0]);
			break;
		case CiId.REGEX_IS_MATCH_REGEX:
			this.writeMethodCall(obj, "test", args[0]);
			break;
		case CiId.MATCH_FIND_STR:
		case CiId.MATCH_FIND_REGEX:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			this.writeChar(40);
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			if (method.id == CiId.MATCH_FIND_STR)
				this.#writeNewRegex(args, 1);
			else
				args[1].accept(this, CiPriority.PRIMARY);
			this.writeCall(".exec", args[0]);
			this.write(") != null");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.MATCH_GET_CAPTURE:
			this.writeIndexing(obj, args[0]);
			break;
		case CiId.MATH_ABS:
			this.writeCall(args[0].type.id == CiId.LONG_TYPE ? "(x => x < 0n ? -x : x)" : "Math.abs", args[0]);
			break;
		case CiId.MATH_CEILING:
			this.writeCall("Math.ceil", args[0]);
			break;
		case CiId.MATH_CLAMP:
			if (method.type.id == CiId.INT_TYPE && GenJsNoModule.#hasLong(args)) {
				this.write("((x, min, max) => x < min ? min : x > max ? max : x)");
				this.writeArgsInParentheses(method, args);
			}
			else {
				this.write("Math.min(Math.max(");
				this.writeClampAsMinMax(args);
			}
			break;
		case CiId.MATH_FUSED_MULTIPLY_ADD:
			if (parent > CiPriority.ADD)
				this.writeChar(40);
			args[0].accept(this, CiPriority.MUL);
			this.write(" * ");
			args[1].accept(this, CiPriority.MUL);
			this.write(" + ");
			args[2].accept(this, CiPriority.ADD);
			if (parent > CiPriority.ADD)
				this.writeChar(41);
			break;
		case CiId.MATH_IS_FINITE:
		case CiId.MATH_IS_NA_N:
			this.writeCamelCase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_IS_INFINITY:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			this.writeCall("Math.abs", args[0]);
			this.write(" == Infinity");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.MATH_MAX_INT:
			this.#writeMathMaxMin(method, "Math.max", 62, args);
			break;
		case CiId.MATH_MIN_INT:
			this.#writeMathMaxMin(method, "Math.min", 60, args);
			break;
		case CiId.MATH_TRUNCATE:
			this.writeCall("Math.trunc", args[0]);
			break;
		default:
			this.notSupported(obj, method.name);
			break;
		}
	}

	writeIndexingExpr(expr, parent)
	{
		let dict;
		if ((dict = expr.left.type) instanceof CiClassType && dict.class.id == CiId.ORDERED_DICTIONARY_CLASS)
			this.writeMethodCall(expr.left, "get", expr.right);
		else
			super.writeIndexingExpr(expr, parent);
	}

	writeAssign(expr, parent)
	{
		let indexing;
		let dict;
		if ((indexing = expr.left) instanceof CiBinaryExpr && indexing.op == CiToken.LEFT_BRACKET && (dict = indexing.left.type) instanceof CiClassType && dict.class.id == CiId.ORDERED_DICTIONARY_CLASS)
			this.writeMethodCall(indexing.left, "set", indexing.right, expr.right);
		else
			super.writeAssign(expr, parent);
	}

	getIsOperator()
	{
		return " instanceof ";
	}

	writeBoolAndOr(expr)
	{
		this.write("!!");
		super.visitBinaryExpr(expr, CiPriority.PRIMARY);
	}

	#writeBoolAndOrAssign(expr, parent)
	{
		expr.right.accept(this, parent);
		this.writeCharLine(41);
		this.writeChar(9);
		expr.left.accept(this, CiPriority.ASSIGN);
	}

	#writeIsVar(expr, def, assign, parent)
	{
		if (parent > CiPriority.REL)
			this.writeChar(40);
		if (assign) {
			this.writeChar(40);
			this.#writeCamelCaseNotKeyword(def.name);
			this.write(" = ");
			expr.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
		}
		else
			expr.accept(this, CiPriority.REL);
		this.write(" instanceof ");
		this.write(def.type.name);
		if (parent > CiPriority.REL)
			this.writeChar(41);
	}

	visitBinaryExpr(expr, parent)
	{
		let def;
		if (expr.op == CiToken.SLASH && expr.type instanceof CiIntegerType && expr.type.id != CiId.LONG_TYPE) {
			if (parent > CiPriority.OR)
				this.writeChar(40);
			expr.left.accept(this, CiPriority.MUL);
			this.write(" / ");
			expr.right.accept(this, CiPriority.PRIMARY);
			this.write(" | 0");
			if (parent > CiPriority.OR)
				this.writeChar(41);
		}
		else if (expr.op == CiToken.DIV_ASSIGN && expr.type instanceof CiIntegerType && expr.type.id != CiId.LONG_TYPE) {
			if (parent > CiPriority.ASSIGN)
				this.writeChar(40);
			expr.left.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			expr.left.accept(this, CiPriority.MUL);
			this.write(" / ");
			expr.right.accept(this, CiPriority.PRIMARY);
			this.write(" | 0");
			if (parent > CiPriority.ASSIGN)
				this.writeChar(41);
		}
		else if ((expr.op == CiToken.AND && expr.type.id == CiId.BOOL_TYPE) || (expr.op == CiToken.OR && expr.type.id == CiId.BOOL_TYPE))
			this.writeBoolAndOr(expr);
		else if (expr.op == CiToken.XOR && expr.type.id == CiId.BOOL_TYPE)
			this.writeEqual(expr.left, expr.right, parent, true);
		else if (expr.op == CiToken.AND_ASSIGN && expr.type.id == CiId.BOOL_TYPE) {
			this.write("if (!");
			this.#writeBoolAndOrAssign(expr, CiPriority.PRIMARY);
			this.write(" = false");
		}
		else if (expr.op == CiToken.OR_ASSIGN && expr.type.id == CiId.BOOL_TYPE) {
			this.write("if (");
			this.#writeBoolAndOrAssign(expr, CiPriority.ARGUMENT);
			this.write(" = true");
		}
		else if (expr.op == CiToken.XOR_ASSIGN && expr.type.id == CiId.BOOL_TYPE) {
			expr.left.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			this.writeEqual(expr.left, expr.right, CiPriority.ARGUMENT, true);
		}
		else if (expr.op == CiToken.IS && (def = expr.right) instanceof CiVar)
			this.#writeIsVar(expr.left, def, true, parent);
		else
			super.visitBinaryExpr(expr, parent);
	}

	visitLambdaExpr(expr)
	{
		this.writeName(expr.first);
		this.write(" => ");
		if (GenJsNoModule.hasTemporaries(expr.body)) {
			this.openBlock();
			this.writeTemporaries(expr.body);
			this.write("return ");
			expr.body.accept(this, CiPriority.ARGUMENT);
			this.writeCharLine(59);
			this.closeBlock();
		}
		else
			expr.body.accept(this, CiPriority.STATEMENT);
	}

	startTemporaryVar(type)
	{
		throw new Error();
	}

	defineObjectLiteralTemporary(expr)
	{
	}

	writeAsType(def)
	{
	}

	#writeVarCast(def, value)
	{
		this.write(def.isAssigned ? "let " : "const ");
		this.#writeCamelCaseNotKeyword(def.name);
		this.write(" = ");
		value.accept(this, CiPriority.ARGUMENT);
		this.writeAsType(def);
		this.writeCharLine(59);
	}

	writeAssertCast(expr)
	{
		const def = expr.right;
		this.#writeVarCast(def, expr.left);
	}

	writeAssert(statement)
	{
		if (statement.completesNormally()) {
			this.writeTemporaries(statement.cond);
			this.write("console.assert(");
			statement.cond.accept(this, CiPriority.ARGUMENT);
			if (statement.message != null) {
				this.write(", ");
				statement.message.accept(this, CiPriority.ARGUMENT);
			}
		}
		else {
			this.write("throw new Error(");
			if (statement.message != null)
				statement.message.accept(this, CiPriority.ARGUMENT);
		}
		this.writeLine(");");
	}

	visitBreak(statement)
	{
		let switchStatement;
		if ((switchStatement = statement.loopOrSwitch) instanceof CiSwitch) {
			let label = this.#switchesWithLabel.indexOf(switchStatement);
			if (label >= 0) {
				this.write("break ciswitch");
				this.visitLiteralLong(BigInt(label));
				this.writeCharLine(59);
				return;
			}
		}
		super.visitBreak(statement);
	}

	visitForeach(statement)
	{
		this.write("for (const ");
		const klass = statement.collection.type;
		switch (klass.class.id) {
		case CiId.STRING_CLASS:
		case CiId.ARRAY_STORAGE_CLASS:
		case CiId.LIST_CLASS:
		case CiId.HASH_SET_CLASS:
			this.writeName(statement.getVar());
			this.write(" of ");
			statement.collection.accept(this, CiPriority.ARGUMENT);
			break;
		case CiId.SORTED_SET_CLASS:
			this.writeName(statement.getVar());
			this.write(" of ");
			if (klass.getElementType() instanceof CiNumericType) {
				const number = klass.getElementType();
				this.write("new ");
				this.writeArrayElementType(number);
				this.write("Array(");
			}
			else if (klass.getElementType() instanceof CiEnum)
				this.write("new Int32Array(");
			else
				this.write("Array.from(");
			statement.collection.accept(this, CiPriority.ARGUMENT);
			this.write(").sort()");
			break;
		case CiId.DICTIONARY_CLASS:
		case CiId.SORTED_DICTIONARY_CLASS:
		case CiId.ORDERED_DICTIONARY_CLASS:
			this.writeChar(91);
			this.writeName(statement.getVar());
			this.write(", ");
			this.writeName(statement.getValueVar());
			this.write("] of ");
			if (klass.class.id == CiId.ORDERED_DICTIONARY_CLASS)
				statement.collection.accept(this, CiPriority.ARGUMENT);
			else {
				this.writeCall("Object.entries", statement.collection);
				if (statement.getVar().type instanceof CiStringType) {
					if (klass.class.id == CiId.SORTED_DICTIONARY_CLASS)
						this.write(".sort((a, b) => a[0].localeCompare(b[0]))");
				}
				else if (statement.getVar().type instanceof CiNumericType || statement.getVar().type instanceof CiEnum) {
					this.write(".map(e => [+e[0], e[1]])");
					if (klass.class.id == CiId.SORTED_DICTIONARY_CLASS)
						this.write(".sort((a, b) => a[0] - b[0])");
				}
				else
					throw new Error();
			}
			break;
		default:
			throw new Error();
		}
		this.writeChar(41);
		this.writeChild(statement.body);
	}

	visitLock(statement)
	{
		this.notSupported(statement, "'lock'");
	}

	writeSwitchCaseCond(statement, value, parent)
	{
		let def;
		if ((def = value) instanceof CiVar)
			this.#writeIsVar(statement.value, def, parent == CiPriority.COND_AND && def.name != "_", parent);
		else
			super.writeSwitchCaseCond(statement, value, parent);
	}

	writeIfCaseBody(body, doWhile, statement, kase)
	{
		let caseVar;
		if (kase != null && (caseVar = kase.values[0]) instanceof CiVar && caseVar.name != "_") {
			this.writeChar(32);
			this.openBlock();
			this.#writeVarCast(caseVar, statement.value);
			this.writeFirstStatements(kase.body, CiSwitch.lengthWithoutTrailingBreak(kase.body));
			this.closeBlock();
		}
		else
			super.writeIfCaseBody(body, doWhile, statement, kase);
	}

	visitSwitch(statement)
	{
		if (statement.isTypeMatching() || statement.hasWhen()) {
			if (statement.cases.some(kase => CiSwitch.hasEarlyBreak(kase.body)) || CiSwitch.hasEarlyBreak(statement.defaultBody)) {
				this.write("ciswitch");
				this.visitLiteralLong(BigInt(this.#switchesWithLabel.length));
				this.#switchesWithLabel.push(statement);
				this.write(": ");
				this.openBlock();
				this.writeSwitchAsIfs(statement, false);
				this.closeBlock();
			}
			else
				this.writeSwitchAsIfs(statement, false);
		}
		else
			super.visitSwitch(statement);
	}

	visitThrow(statement)
	{
		this.write("throw ");
		statement.message.accept(this, CiPriority.ARGUMENT);
		this.writeCharLine(59);
	}

	startContainerType(container)
	{
		this.writeNewLine();
		this.writeDoc(container.documentation);
	}

	visitEnumValue(konst, previous)
	{
		if (previous != null)
			this.writeCharLine(44);
		this.writeDoc(konst.documentation);
		this.writeUppercaseWithUnderscores(konst.name);
		this.write(" : ");
		this.visitLiteralLong(BigInt(konst.value.intValue()));
	}

	writeEnum(enu)
	{
		this.startContainerType(enu);
		this.write("const ");
		this.write(enu.name);
		this.write(" = ");
		this.openBlock();
		enu.acceptValues(this);
		this.writeNewLine();
		this.closeBlock();
	}

	writeConst(konst)
	{
		if (konst.visibility != CiVisibility.PRIVATE || konst.type instanceof CiArrayStorageType) {
			this.writeNewLine();
			this.writeDoc(konst.documentation);
			this.write("static ");
			this.writeName(konst);
			this.write(" = ");
			konst.value.accept(this, CiPriority.ARGUMENT);
			this.writeCharLine(59);
		}
	}

	writeField(field)
	{
		this.writeDoc(field.documentation);
		super.writeVar(field);
		this.writeCharLine(59);
	}

	writeMethod(method)
	{
		if (method.callType == CiCallType.ABSTRACT)
			return;
		this.#switchesWithLabel.length = 0;
		this.writeNewLine();
		this.writeMethodDoc(method);
		if (method.callType == CiCallType.STATIC)
			this.write("static ");
		this.writeName(method);
		this.writeParameters(method, true);
		this.writeBody(method);
	}

	writeConstructor(klass)
	{
		this.#switchesWithLabel.length = 0;
		this.writeLine("constructor()");
		this.openBlock();
		if (klass.parent instanceof CiClass)
			this.writeLine("super();");
		this.writeConstructorBody(klass);
		this.closeBlock();
	}

	writeClass(klass, program)
	{
		if (!this.writeBaseClass(klass, program))
			return;
		this.startContainerType(klass);
		this.openClass(klass, "", " extends ");
		if (this.needsConstructor(klass)) {
			if (klass.constructor_ != null)
				this.writeDoc(klass.constructor_.documentation);
			this.writeConstructor(klass);
		}
		this.writeMembers(klass, true);
		this.closeBlock();
	}

	writeLib(resources)
	{
		if (this.#stringWriter) {
			this.writeNewLine();
			this.writeLine("class StringWriter");
			this.openBlock();
			this.writeLine("#buf = \"\";");
			this.writeNewLine();
			this.writeLine("write(s)");
			this.openBlock();
			this.writeLine("this.#buf += s;");
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("clear()");
			this.openBlock();
			this.writeLine("this.#buf = \"\";");
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("toString()");
			this.openBlock();
			this.writeLine("return this.#buf;");
			this.closeBlock();
			this.closeBlock();
		}
		if (Object.keys(resources).length == 0)
			return;
		this.writeNewLine();
		this.writeLine("class Ci");
		this.openBlock();
		for (const [name, content] of Object.entries(resources).sort((a, b) => a[0].localeCompare(b[0]))) {
			this.write("static ");
			this.writeResourceName(name);
			this.writeLine(" = new Uint8Array([");
			this.writeChar(9);
			this.writeBytes(content);
			this.writeLine(" ]);");
		}
		this.writeNewLine();
		this.closeBlock();
	}

	writeUseStrict()
	{
		this.writeNewLine();
		this.writeLine("\"use strict\";");
	}

	writeProgram(program)
	{
		this.createOutputFile();
		this.writeTopLevelNatives(program);
		this.writeTypes(program);
		this.writeLib(program.resources);
		this.closeFile();
	}
}

export class GenJs extends GenJsNoModule
{

	startContainerType(container)
	{
		super.startContainerType(container);
		if (container.isPublic)
			this.write("export ");
	}

	writeUseStrict()
	{
	}
}

export class GenTs extends GenJs
{
	#system;
	#genFullCode = false;

	getTargetName()
	{
		return "TypeScript";
	}

	withGenFullCode()
	{
		this.#genFullCode = true;
		return this;
	}

	visitEnumValue(konst, previous)
	{
		this.writeEnumValue(konst);
		this.writeCharLine(44);
	}

	writeEnum(enu)
	{
		this.startContainerType(enu);
		this.write("enum ");
		this.write(enu.name);
		this.writeChar(32);
		this.openBlock();
		enu.acceptValues(this);
		this.closeBlock();
	}

	writeTypeAndName(value)
	{
		this.writeName(value);
		this.write(": ");
		this.#writeType(value.type);
	}

	#writeType(type, readOnly = false)
	{
		if (type instanceof CiNumericType)
			this.write(type.id == CiId.LONG_TYPE ? "bigint" : "number");
		else if (type instanceof CiEnum) {
			const enu = type;
			this.write(enu.id == CiId.BOOL_TYPE ? "boolean" : enu.name);
		}
		else if (type instanceof CiClassType) {
			const klass = type;
			if (!(klass instanceof CiReadWriteClassType))
				readOnly = true;
			if (klass.class.id == CiId.STRING_CLASS)
				this.write("string");
			else if ((klass.class.id == CiId.ARRAY_PTR_CLASS && !(klass.getElementType() instanceof CiNumericType)) || (klass.class.id == CiId.ARRAY_STORAGE_CLASS && !(klass.getElementType() instanceof CiNumericType)) || klass.class.id == CiId.LIST_CLASS || klass.class.id == CiId.QUEUE_CLASS || klass.class.id == CiId.STACK_CLASS) {
				if (readOnly)
					this.write("readonly ");
				if (klass.getElementType().nullable)
					this.writeChar(40);
				this.#writeType(klass.getElementType());
				if (klass.getElementType().nullable)
					this.writeChar(41);
				this.write("[]");
			}
			else {
				if (readOnly && klass.class.typeParameterCount > 0)
					this.write("Readonly<");
				switch (klass.class.id) {
				case CiId.ARRAY_PTR_CLASS:
				case CiId.ARRAY_STORAGE_CLASS:
					this.writeArrayElementType(klass.getElementType());
					this.write("Array");
					break;
				case CiId.HASH_SET_CLASS:
				case CiId.SORTED_SET_CLASS:
					this.write("Set<");
					this.#writeType(klass.getElementType(), false);
					this.writeChar(62);
					break;
				case CiId.DICTIONARY_CLASS:
				case CiId.SORTED_DICTIONARY_CLASS:
					if (klass.getKeyType() instanceof CiEnum)
						this.write("Partial<");
					this.write("Record<");
					this.#writeType(klass.getKeyType());
					this.write(", ");
					this.#writeType(klass.getValueType());
					this.writeChar(62);
					if (klass.getKeyType() instanceof CiEnum)
						this.writeChar(62);
					break;
				case CiId.ORDERED_DICTIONARY_CLASS:
					this.write("Map<");
					this.#writeType(klass.getKeyType());
					this.write(", ");
					this.#writeType(klass.getValueType());
					this.writeChar(62);
					break;
				case CiId.REGEX_CLASS:
					this.write("RegExp");
					break;
				case CiId.MATCH_CLASS:
					this.write("RegExpMatchArray");
					break;
				default:
					this.write(klass.class.name);
					break;
				}
				if (readOnly && klass.class.typeParameterCount > 0)
					this.writeChar(62);
			}
			if (type.nullable)
				this.write(" | null");
		}
		else
			this.write(type.name);
	}

	writeAsType(def)
	{
		this.write(" as ");
		this.write(def.type.name);
	}

	writeBinaryOperand(expr, parent, binary)
	{
		let type = binary.type;
		if (expr.type instanceof CiNumericType && binary.isRel()) {
			type = this.#system.promoteNumericTypes(binary.left.type, binary.right.type);
		}
		this.writeCoerced(type, expr, parent);
	}

	writeEqualOperand(expr, other)
	{
		if (expr.type instanceof CiNumericType)
			this.writeCoerced(this.#system.promoteNumericTypes(expr.type, other.type), expr, CiPriority.EQUALITY);
		else
			expr.accept(this, CiPriority.EQUALITY);
	}

	writeBoolAndOr(expr)
	{
		this.write("[ ");
		expr.left.accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		expr.right.accept(this, CiPriority.ARGUMENT);
		this.write(" ].");
		this.write(expr.op == CiToken.AND ? "every" : "some");
		this.write("(Boolean)");
	}

	defineIsVar(binary)
	{
		let def;
		if ((def = binary.right) instanceof CiVar) {
			this.ensureChildBlock();
			this.write("let ");
			this.writeName(def);
			this.write(": ");
			this.#writeType(binary.left.type);
			this.endStatement();
		}
	}

	#writeVisibility(visibility)
	{
		switch (visibility) {
		case CiVisibility.PRIVATE:
		case CiVisibility.INTERNAL:
			break;
		case CiVisibility.PROTECTED:
			this.write("protected ");
			break;
		case CiVisibility.PUBLIC:
			this.write("public ");
			break;
		default:
			throw new Error();
		}
	}

	writeConst(konst)
	{
		this.writeNewLine();
		this.writeDoc(konst.documentation);
		this.#writeVisibility(konst.visibility);
		this.write("static readonly ");
		this.writeName(konst);
		this.write(": ");
		this.#writeType(konst.type, true);
		if (this.#genFullCode) {
			this.write(" = ");
			konst.value.accept(this, CiPriority.ARGUMENT);
		}
		this.writeCharLine(59);
	}

	writeField(field)
	{
		this.writeDoc(field.documentation);
		this.#writeVisibility(field.visibility);
		if (field.type.isFinal() && !field.isAssignableStorage())
			this.write("readonly ");
		this.writeTypeAndName(field);
		if (this.#genFullCode)
			this.writeVarInit(field);
		this.writeCharLine(59);
	}

	writeMethod(method)
	{
		this.writeNewLine();
		this.writeMethodDoc(method);
		this.#writeVisibility(method.visibility);
		switch (method.callType) {
		case CiCallType.STATIC:
			this.write("static ");
			break;
		case CiCallType.VIRTUAL:
			break;
		case CiCallType.ABSTRACT:
			this.write("abstract ");
			break;
		case CiCallType.OVERRIDE:
			break;
		case CiCallType.NORMAL:
			break;
		case CiCallType.SEALED:
			break;
		default:
			throw new Error();
		}
		this.writeName(method);
		this.writeChar(40);
		let i = 0;
		for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
			if (i > 0)
				this.write(", ");
			this.writeName(param);
			if (param.value != null && !this.#genFullCode)
				this.writeChar(63);
			this.write(": ");
			this.#writeType(param.type);
			if (param.value != null && this.#genFullCode)
				this.writeVarInit(param);
			i++;
		}
		this.write("): ");
		this.#writeType(method.type);
		if (this.#genFullCode)
			this.writeBody(method);
		else
			this.writeCharLine(59);
	}

	writeClass(klass, program)
	{
		if (!this.writeBaseClass(klass, program))
			return;
		this.startContainerType(klass);
		switch (klass.callType) {
		case CiCallType.NORMAL:
			break;
		case CiCallType.ABSTRACT:
			this.write("abstract ");
			break;
		case CiCallType.STATIC:
		case CiCallType.SEALED:
			break;
		default:
			throw new Error();
		}
		this.openClass(klass, "", " extends ");
		if (this.needsConstructor(klass) || klass.callType == CiCallType.STATIC) {
			if (klass.constructor_ != null) {
				this.writeDoc(klass.constructor_.documentation);
				this.#writeVisibility(klass.constructor_.visibility);
			}
			else if (klass.callType == CiCallType.STATIC)
				this.write("private ");
			if (this.#genFullCode)
				this.writeConstructor(klass);
			else
				this.writeLine("constructor();");
		}
		this.writeMembers(klass, this.#genFullCode);
		this.closeBlock();
	}

	writeProgram(program)
	{
		this.#system = program.system;
		this.createOutputFile();
		if (this.#genFullCode)
			this.writeTopLevelNatives(program);
		this.writeTypes(program);
		if (this.#genFullCode)
			this.writeLib(program.resources);
		this.closeFile();
	}
}

export class GenPySwift extends GenBase
{

	writeDocPara(para, many)
	{
		if (many) {
			this.writeNewLine();
			this.startDocLine();
			this.writeNewLine();
			this.startDocLine();
		}
		for (const inline of para.children) {
			if (inline instanceof CiDocText) {
				const text = inline;
				this.write(text.text);
			}
			else if (inline instanceof CiDocCode) {
				const code = inline;
				this.writeChar(96);
				this.write(code.text);
				this.writeChar(96);
			}
			else if (inline instanceof CiDocLine) {
				this.writeNewLine();
				this.startDocLine();
			}
			else
				throw new Error();
		}
	}

	writeDocList(list)
	{
		this.writeNewLine();
		for (const item of list.items) {
			this.write(this.getDocBullet());
			this.writeDocPara(item, false);
			this.writeNewLine();
		}
		this.startDocLine();
	}

	writeLocalName(symbol, parent)
	{
		let member;
		if ((member = symbol) instanceof CiMember) {
			if (member.isStatic())
				this.writeName(this.currentMethod.parent);
			else
				this.write("self");
			this.writeChar(46);
		}
		this.writeName(symbol);
	}

	visitAggregateInitializer(expr)
	{
		this.write("[ ");
		this.writeCoercedLiterals(expr.type.asClassType().getElementType(), expr.items);
		this.write(" ]");
	}

	visitPrefixExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.INCREMENT:
		case CiToken.DECREMENT:
			expr.inner.accept(this, parent);
			break;
		default:
			super.visitPrefixExpr(expr, parent);
			break;
		}
	}

	visitPostfixExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.INCREMENT:
		case CiToken.DECREMENT:
			expr.inner.accept(this, parent);
			break;
		default:
			super.visitPostfixExpr(expr, parent);
			break;
		}
	}

	static #isPtr(expr)
	{
		let klass;
		return (klass = expr.type) instanceof CiClassType && klass.class.id != CiId.STRING_CLASS && !(klass instanceof CiStorageType);
	}

	writeEqual(left, right, parent, not)
	{
		if (GenPySwift.#isPtr(left) || GenPySwift.#isPtr(right))
			this.writeEqualExpr(left, right, parent, this.getReferenceEqOp(not));
		else
			super.writeEqual(left, right, parent, not);
	}

	writeExpr(expr, parent)
	{
		expr.accept(this, parent);
	}

	writeListAppend(obj, args)
	{
		this.writePostfix(obj, ".append(");
		let elementType = obj.type.asClassType().getElementType();
		if (args.length == 0)
			this.writeNewStorage(elementType);
		else
			this.writeCoerced(elementType, args[0], CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	visitPreCall(call)
	{
		return false;
	}

	visitXcrement(expr, postfix, write)
	{
		let seen;
		if (expr instanceof CiVar) {
			const def = expr;
			return def.value != null && this.visitXcrement(def.value, postfix, write);
		}
		else if (expr instanceof CiAggregateInitializer || expr instanceof CiLiteral || expr instanceof CiLambdaExpr)
			return false;
		else if (expr instanceof CiInterpolatedString) {
			const interp = expr;
			seen = false;
			for (const part of interp.parts)
				if (this.visitXcrement(part.argument, postfix, write))
					seen = true;
			return seen;
		}
		else if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			return symbol.left != null && this.visitXcrement(symbol.left, postfix, write);
		}
		else if (expr instanceof CiUnaryExpr) {
			const unary = expr;
			if (unary.inner == null)
				return false;
			seen = this.visitXcrement(unary.inner, postfix, write);
			if ((unary.op == CiToken.INCREMENT || unary.op == CiToken.DECREMENT) && postfix == unary instanceof CiPostfixExpr) {
				if (write) {
					this.writeExpr(unary.inner, CiPriority.ASSIGN);
					this.writeLine(unary.op == CiToken.INCREMENT ? " += 1" : " -= 1");
				}
				seen = true;
			}
			return seen;
		}
		else if (expr instanceof CiBinaryExpr) {
			const binary = expr;
			seen = this.visitXcrement(binary.left, postfix, write);
			if (binary.op == CiToken.IS)
				return seen;
			if (binary.op == CiToken.COND_AND || binary.op == CiToken.COND_OR)
				console.assert(!this.visitXcrement(binary.right, postfix, false));
			else
				if (this.visitXcrement(binary.right, postfix, write))
					seen = true;
			return seen;
		}
		else if (expr instanceof CiSelectExpr) {
			const select = expr;
			seen = this.visitXcrement(select.cond, postfix, write);
			console.assert(!this.visitXcrement(select.onTrue, postfix, false));
			console.assert(!this.visitXcrement(select.onFalse, postfix, false));
			return seen;
		}
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			seen = this.visitXcrement(call.method, postfix, write);
			for (const arg of call.arguments)
				if (this.visitXcrement(arg, postfix, write))
					seen = true;
			if (!postfix)
				if (this.visitPreCall(call))
					seen = true;
			return seen;
		}
		else
			throw new Error();
	}

	visitExpr(statement)
	{
		this.visitXcrement(statement, false, true);
		let unary;
		if (!((unary = statement) instanceof CiUnaryExpr) || (unary.op != CiToken.INCREMENT && unary.op != CiToken.DECREMENT)) {
			this.writeExpr(statement, CiPriority.STATEMENT);
			this.writeNewLine();
			let def;
			if ((def = statement) instanceof CiVar)
				this.writeInitCode(def);
		}
		this.visitXcrement(statement, true, true);
		this.cleanupTemporaries();
	}

	endStatement()
	{
		this.writeNewLine();
	}

	writeChild(statement)
	{
		this.openChild();
		statement.acceptStatement(this);
		this.closeChild();
	}

	visitBlock(statement)
	{
		this.writeStatements(statement.statements);
	}

	#openCond(statement, cond, parent)
	{
		this.visitXcrement(cond, false, true);
		this.write(statement);
		this.writeExpr(cond, parent);
		this.openChild();
		return this.visitXcrement(cond, true, true);
	}

	writeContinueDoWhile(cond)
	{
		this.#openCond("if ", cond, CiPriority.ARGUMENT);
		this.writeLine("continue");
		this.closeChild();
		this.visitXcrement(cond, true, true);
		this.writeLine("break");
	}

	needCondXcrement(loop)
	{
		return loop.cond != null;
	}

	#endBody(loop)
	{
		let forLoop;
		if ((forLoop = loop) instanceof CiFor) {
			if (forLoop.isRange)
				return;
			this.visitOptionalStatement(forLoop.advance);
		}
		if (this.needCondXcrement(loop))
			this.visitXcrement(loop.cond, false, true);
	}

	visitContinue(statement)
	{
		let doWhile;
		if ((doWhile = statement.loop) instanceof CiDoWhile)
			this.writeContinueDoWhile(doWhile.cond);
		else {
			this.#endBody(statement.loop);
			this.writeLine("continue");
		}
	}

	#openWhileTrue()
	{
		this.write("while ");
		this.visitLiteralTrue();
		this.openChild();
	}

	visitDoWhile(statement)
	{
		this.#openWhileTrue();
		statement.body.acceptStatement(this);
		if (statement.body.completesNormally()) {
			this.#openCond(this.getIfNot(), statement.cond, CiPriority.PRIMARY);
			this.writeLine("break");
			this.closeChild();
			this.visitXcrement(statement.cond, true, true);
		}
		this.closeChild();
	}

	openWhile(loop)
	{
		this.#openCond("while ", loop.cond, CiPriority.ARGUMENT);
	}

	#closeWhile(loop)
	{
		loop.body.acceptStatement(this);
		if (loop.body.completesNormally())
			this.#endBody(loop);
		this.closeChild();
		if (this.needCondXcrement(loop)) {
			if (loop.hasBreak && this.visitXcrement(loop.cond, true, false)) {
				this.write("else");
				this.openChild();
				this.visitXcrement(loop.cond, true, true);
				this.closeChild();
			}
			else
				this.visitXcrement(loop.cond, true, true);
		}
	}

	visitFor(statement)
	{
		if (statement.isRange) {
			const iter = statement.init;
			this.write("for ");
			if (statement.isIteratorUsed)
				this.writeName(iter);
			else
				this.writeChar(95);
			this.write(" in ");
			const cond = statement.cond;
			this.writeForRange(iter, cond, statement.rangeStep);
			this.writeChild(statement.body);
		}
		else {
			this.visitOptionalStatement(statement.init);
			if (statement.cond != null)
				this.openWhile(statement);
			else
				this.#openWhileTrue();
			this.#closeWhile(statement);
		}
	}

	visitIf(statement)
	{
		let condPostXcrement = this.#openCond("if ", statement.cond, CiPriority.ARGUMENT);
		statement.onTrue.acceptStatement(this);
		this.closeChild();
		if (statement.onFalse == null && condPostXcrement && !statement.onTrue.completesNormally())
			this.visitXcrement(statement.cond, true, true);
		else if (statement.onFalse != null || condPostXcrement) {
			let childIf;
			if (!condPostXcrement && (childIf = statement.onFalse) instanceof CiIf && !this.visitXcrement(childIf.cond, false, false)) {
				this.writeElseIf();
				this.visitIf(childIf);
			}
			else {
				this.write("else");
				this.openChild();
				this.visitXcrement(statement.cond, true, true);
				this.visitOptionalStatement(statement.onFalse);
				this.closeChild();
			}
		}
	}

	visitReturn(statement)
	{
		if (statement.value == null)
			this.writeLine("return");
		else {
			this.visitXcrement(statement.value, false, true);
			this.writeTemporaries(statement.value);
			if (this.visitXcrement(statement.value, true, false)) {
				this.writeResultVar();
				this.write(" = ");
				this.writeCoercedExpr(this.currentMethod.type, statement.value);
				this.writeNewLine();
				this.visitXcrement(statement.value, true, true);
				this.writeLine("return result");
			}
			else {
				this.write("return ");
				this.writeCoercedExpr(this.currentMethod.type, statement.value);
				this.writeNewLine();
			}
			this.cleanupTemporaries();
		}
	}

	visitWhile(statement)
	{
		this.openWhile(statement);
		this.#closeWhile(statement);
	}
}

export class GenSwift extends GenPySwift
{
	#system;
	#throw;
	#arrayRef;
	#stringCharAt;
	#stringIndexOf;
	#stringSubstring;
	#varsAtIndent = [];
	#varBytesAtIndent = [];

	getTargetName()
	{
		return "Swift";
	}

	startDocLine()
	{
		this.write("/// ");
	}

	getDocBullet()
	{
		return "/// * ";
	}

	writeDoc(doc)
	{
		if (doc != null)
			this.writeContent(doc);
	}

	#writeCamelCaseNotKeyword(name)
	{
		switch (name) {
		case "this":
			this.write("self");
			break;
		case "As":
		case "Associatedtype":
		case "Await":
		case "Break":
		case "Case":
		case "Catch":
		case "Class":
		case "Continue":
		case "Default":
		case "Defer":
		case "Deinit":
		case "Do":
		case "Else":
		case "Enum":
		case "Extension":
		case "Fallthrough":
		case "False":
		case "Fileprivate":
		case "For":
		case "Foreach":
		case "Func":
		case "Guard":
		case "If":
		case "Import":
		case "In":
		case "Init":
		case "Inout":
		case "Int":
		case "Internal":
		case "Is":
		case "Let":
		case "Nil":
		case "Operator":
		case "Private":
		case "Protocol":
		case "Public":
		case "Repeat":
		case "Rethrows":
		case "Return":
		case "Self":
		case "Static":
		case "Struct":
		case "Switch":
		case "Subscript":
		case "Super":
		case "Throw":
		case "Throws":
		case "True":
		case "Try":
		case "Typealias":
		case "Var":
		case "Void":
		case "Where":
		case "While":
		case "as":
		case "associatedtype":
		case "await":
		case "catch":
		case "defer":
		case "deinit":
		case "extension":
		case "fallthrough":
		case "fileprivate":
		case "func":
		case "guard":
		case "import":
		case "init":
		case "inout":
		case "is":
		case "let":
		case "nil":
		case "operator":
		case "private":
		case "protocol":
		case "repeat":
		case "rethrows":
		case "self":
		case "struct":
		case "subscript":
		case "super":
		case "try":
		case "typealias":
		case "var":
		case "where":
			this.writeCamelCase(name);
			this.writeChar(95);
			break;
		default:
			this.writeCamelCase(name);
			break;
		}
	}

	writeName(symbol)
	{
		let konst;
		if (symbol instanceof CiContainerType)
			this.write(symbol.name);
		else if ((konst = symbol) instanceof CiConst && konst.inMethod != null) {
			this.writeCamelCase(konst.inMethod.name);
			this.writePascalCase(symbol.name);
		}
		else if (symbol instanceof CiVar || symbol instanceof CiMember)
			this.#writeCamelCaseNotKeyword(symbol.name);
		else
			throw new Error();
	}

	writeLocalName(symbol, parent)
	{
		let forEach;
		if ((forEach = symbol.parent) instanceof CiForeach && forEach.collection.type instanceof CiStringType) {
			this.write("Int(");
			this.#writeCamelCaseNotKeyword(symbol.name);
			this.write(".value)");
		}
		else
			super.writeLocalName(symbol, parent);
	}

	writeMemberOp(left, symbol)
	{
		if (left.type != null && left.type.nullable)
			this.writeChar(33);
		this.writeChar(46);
	}

	#openIndexing(collection)
	{
		collection.accept(this, CiPriority.PRIMARY);
		if (collection.type.nullable)
			this.writeChar(33);
		this.writeChar(91);
	}

	static #isArrayRef(array)
	{
		return array.ptrTaken || array.getElementType() instanceof CiStorageType;
	}

	#writeClassName(klass)
	{
		switch (klass.class.id) {
		case CiId.STRING_CLASS:
			this.write("String");
			break;
		case CiId.ARRAY_PTR_CLASS:
			this.#arrayRef = true;
			this.write("ArrayRef<");
			this.#writeType(klass.getElementType());
			this.writeChar(62);
			break;
		case CiId.LIST_CLASS:
		case CiId.QUEUE_CLASS:
		case CiId.STACK_CLASS:
			this.writeChar(91);
			this.#writeType(klass.getElementType());
			this.writeChar(93);
			break;
		case CiId.HASH_SET_CLASS:
		case CiId.SORTED_SET_CLASS:
			this.write("Set<");
			this.#writeType(klass.getElementType());
			this.writeChar(62);
			break;
		case CiId.DICTIONARY_CLASS:
		case CiId.SORTED_DICTIONARY_CLASS:
			this.writeChar(91);
			this.#writeType(klass.getKeyType());
			this.write(": ");
			this.#writeType(klass.getValueType());
			this.writeChar(93);
			break;
		case CiId.ORDERED_DICTIONARY_CLASS:
			this.notSupported(klass, "OrderedDictionary");
			break;
		case CiId.LOCK_CLASS:
			this.include("Foundation");
			this.write("NSRecursiveLock");
			break;
		default:
			this.write(klass.class.name);
			break;
		}
	}

	#writeType(type)
	{
		if (type instanceof CiNumericType) {
			switch (type.id) {
			case CiId.S_BYTE_RANGE:
				this.write("Int8");
				break;
			case CiId.BYTE_RANGE:
				this.write("UInt8");
				break;
			case CiId.SHORT_RANGE:
				this.write("Int16");
				break;
			case CiId.U_SHORT_RANGE:
				this.write("UInt16");
				break;
			case CiId.INT_TYPE:
				this.write("Int");
				break;
			case CiId.LONG_TYPE:
				this.write("Int64");
				break;
			case CiId.FLOAT_TYPE:
				this.write("Float");
				break;
			case CiId.DOUBLE_TYPE:
				this.write("Double");
				break;
			default:
				throw new Error();
			}
		}
		else if (type instanceof CiEnum)
			this.write(type.id == CiId.BOOL_TYPE ? "Bool" : type.name);
		else if (type instanceof CiArrayStorageType) {
			const arrayStg = type;
			if (GenSwift.#isArrayRef(arrayStg)) {
				this.#arrayRef = true;
				this.write("ArrayRef<");
				this.#writeType(arrayStg.getElementType());
				this.writeChar(62);
			}
			else {
				this.writeChar(91);
				this.#writeType(arrayStg.getElementType());
				this.writeChar(93);
			}
		}
		else if (type instanceof CiClassType) {
			const klass = type;
			this.#writeClassName(klass);
			if (klass.nullable)
				this.writeChar(63);
		}
		else
			this.write(type.name);
	}

	writeTypeAndName(value)
	{
		this.writeName(value);
		if (!value.type.isFinal() || value.isAssignableStorage()) {
			this.write(" : ");
			this.#writeType(value.type);
		}
	}

	visitLiteralNull()
	{
		this.write("nil");
	}

	#writeUnwrapped(expr, parent, substringOk)
	{
		if (expr.type.nullable) {
			expr.accept(this, CiPriority.PRIMARY);
			this.writeChar(33);
		}
		else {
			let call;
			if (!substringOk && (call = expr) instanceof CiCallExpr && call.method.symbol.id == CiId.STRING_SUBSTRING)
				this.writeCall("String", expr);
			else
				expr.accept(this, parent);
		}
	}

	visitInterpolatedString(expr, parent)
	{
		if (expr.parts.some(part => part.widthExpr != null || part.format != 32 || part.precision >= 0)) {
			this.include("Foundation");
			this.write("String(format: ");
			this.writePrintf(expr, false);
		}
		else {
			this.writeChar(34);
			for (const part of expr.parts) {
				this.write(part.prefix);
				this.write("\\(");
				this.#writeUnwrapped(part.argument, CiPriority.ARGUMENT, true);
				this.writeChar(41);
			}
			this.write(expr.suffix);
			this.writeChar(34);
		}
	}

	writeCoercedInternal(type, expr, parent)
	{
		let binary;
		if (type instanceof CiNumericType && !(expr instanceof CiLiteral) && this.getTypeId(type, false) != this.getTypeId(expr.type, (binary = expr) instanceof CiBinaryExpr && binary.op != CiToken.LEFT_BRACKET)) {
			this.#writeType(type);
			this.writeChar(40);
			let call;
			if (type instanceof CiIntegerType && (call = expr) instanceof CiCallExpr && call.method.symbol.id == CiId.MATH_TRUNCATE)
				call.arguments[0].accept(this, CiPriority.ARGUMENT);
			else
				expr.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
		}
		else if (!type.nullable)
			this.#writeUnwrapped(expr, parent, false);
		else
			expr.accept(this, parent);
	}

	writeStringLength(expr)
	{
		this.#writeUnwrapped(expr, CiPriority.PRIMARY, true);
		this.write(".count");
	}

	writeCharAt(expr)
	{
		this.#stringCharAt = true;
		this.write("ciStringCharAt(");
		this.#writeUnwrapped(expr.left, CiPriority.ARGUMENT, false);
		this.write(", ");
		expr.right.accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.MATH_NA_N:
			this.write("Float.nan");
			break;
		case CiId.MATH_NEGATIVE_INFINITY:
			this.write("-Float.infinity");
			break;
		case CiId.MATH_POSITIVE_INFINITY:
			this.write("Float.infinity");
			break;
		default:
			super.visitSymbolReference(expr, parent);
			break;
		}
	}

	getReferenceEqOp(not)
	{
		return not ? " !== " : " === ";
	}

	#writeStringContains(obj, name, args)
	{
		this.#writeUnwrapped(obj, CiPriority.PRIMARY, true);
		this.writeChar(46);
		this.write(name);
		this.writeChar(40);
		this.#writeUnwrapped(args[0], CiPriority.ARGUMENT, true);
		this.writeChar(41);
	}

	#writeRange(startIndex, length)
	{
		this.writeCoerced(this.#system.intType, startIndex, CiPriority.SHIFT);
		this.write("..<");
		this.writeAdd(startIndex, length);
	}

	#addVar(name)
	{
		let vars = this.#varsAtIndent[this.indent];
		if (vars.has(name))
			return false;
		vars.add(name);
		return true;
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.NONE:
		case CiId.ARRAY_CONTAINS:
		case CiId.LIST_CONTAINS:
		case CiId.LIST_SORT_ALL:
		case CiId.HASH_SET_CONTAINS:
		case CiId.HASH_SET_REMOVE:
		case CiId.SORTED_SET_CONTAINS:
		case CiId.SORTED_SET_REMOVE:
			if (obj == null) {
				if (method.isStatic()) {
					this.writeName(this.currentMethod.parent);
					this.writeChar(46);
				}
			}
			else if (GenSwift.isReferenceTo(obj, CiId.BASE_PTR))
				this.write("super.");
			else {
				obj.accept(this, CiPriority.PRIMARY);
				this.writeMemberOp(obj, null);
			}
			this.writeName(method);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.CLASS_TO_STRING:
			obj.accept(this, CiPriority.PRIMARY);
			this.writeMemberOp(obj, null);
			this.write("description");
			break;
		case CiId.ENUM_FROM_INT:
			this.write(method.type.name);
			this.write("(rawValue: ");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.ENUM_HAS_FLAG:
			this.writeMethodCall(obj, "contains", args[0]);
			break;
		case CiId.STRING_CONTAINS:
			this.#writeStringContains(obj, "contains", args);
			break;
		case CiId.STRING_ENDS_WITH:
			this.#writeStringContains(obj, "hasSuffix", args);
			break;
		case CiId.STRING_INDEX_OF:
			this.include("Foundation");
			this.#stringIndexOf = true;
			this.write("ciStringIndexOf(");
			this.#writeUnwrapped(obj, CiPriority.ARGUMENT, true);
			this.write(", ");
			this.#writeUnwrapped(args[0], CiPriority.ARGUMENT, true);
			this.writeChar(41);
			break;
		case CiId.STRING_LAST_INDEX_OF:
			this.include("Foundation");
			this.#stringIndexOf = true;
			this.write("ciStringIndexOf(");
			this.#writeUnwrapped(obj, CiPriority.ARGUMENT, true);
			this.write(", ");
			this.#writeUnwrapped(args[0], CiPriority.ARGUMENT, true);
			this.write(", .backwards)");
			break;
		case CiId.STRING_REPLACE:
			this.#writeUnwrapped(obj, CiPriority.PRIMARY, true);
			this.write(".replacingOccurrences(of: ");
			this.#writeUnwrapped(args[0], CiPriority.ARGUMENT, true);
			this.write(", with: ");
			this.#writeUnwrapped(args[1], CiPriority.ARGUMENT, true);
			this.writeChar(41);
			break;
		case CiId.STRING_STARTS_WITH:
			this.#writeStringContains(obj, "hasPrefix", args);
			break;
		case CiId.STRING_SUBSTRING:
			if (args[0].isLiteralZero())
				this.#writeUnwrapped(obj, CiPriority.PRIMARY, true);
			else {
				this.#stringSubstring = true;
				this.write("ciStringSubstring(");
				this.#writeUnwrapped(obj, CiPriority.ARGUMENT, false);
				this.write(", ");
				this.writeCoerced(this.#system.intType, args[0], CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			if (args.length == 2) {
				this.write(".prefix(");
				this.writeCoerced(this.#system.intType, args[1], CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			break;
		case CiId.ARRAY_COPY_TO:
		case CiId.LIST_COPY_TO:
			this.#openIndexing(args[1]);
			this.#writeRange(args[2], args[3]);
			this.write("] = ");
			this.#openIndexing(obj);
			this.#writeRange(args[0], args[3]);
			this.writeChar(93);
			break;
		case CiId.ARRAY_FILL_ALL:
			obj.accept(this, CiPriority.ASSIGN);
			let array;
			if ((array = obj.type) instanceof CiArrayStorageType && !GenSwift.#isArrayRef(array)) {
				this.write(" = [");
				this.#writeType(array.getElementType());
				this.write("](repeating: ");
				this.writeCoerced(array.getElementType(), args[0], CiPriority.ARGUMENT);
				this.write(", count: ");
				this.visitLiteralLong(BigInt(array.length));
				this.writeChar(41);
			}
			else {
				this.write(".fill");
				this.writeArgsInParentheses(method, args);
			}
			break;
		case CiId.ARRAY_FILL_PART:
			let array2;
			if ((array2 = obj.type) instanceof CiArrayStorageType && !GenSwift.#isArrayRef(array2)) {
				this.#openIndexing(obj);
				this.#writeRange(args[1], args[2]);
				this.write("] = ArraySlice(repeating: ");
				this.writeCoerced(array2.getElementType(), args[0], CiPriority.ARGUMENT);
				this.write(", count: ");
				this.writeCoerced(this.#system.intType, args[2], CiPriority.ARGUMENT);
				this.writeChar(41);
			}
			else {
				obj.accept(this, CiPriority.PRIMARY);
				this.writeMemberOp(obj, null);
				this.write("fill");
				this.writeArgsInParentheses(method, args);
			}
			break;
		case CiId.ARRAY_SORT_ALL:
			this.writePostfix(obj, "[0..<");
			const array3 = obj.type;
			this.visitLiteralLong(BigInt(array3.length));
			this.write("].sort()");
			break;
		case CiId.ARRAY_SORT_PART:
		case CiId.LIST_SORT_PART:
			this.#openIndexing(obj);
			this.#writeRange(args[0], args[1]);
			this.write("].sort()");
			break;
		case CiId.LIST_ADD:
		case CiId.QUEUE_ENQUEUE:
		case CiId.STACK_PUSH:
			this.writeListAppend(obj, args);
			break;
		case CiId.LIST_ADD_RANGE:
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" += ");
			args[0].accept(this, CiPriority.ARGUMENT);
			break;
		case CiId.LIST_ALL:
			this.writePostfix(obj, ".allSatisfy ");
			args[0].accept(this, CiPriority.ARGUMENT);
			break;
		case CiId.LIST_ANY:
			this.writePostfix(obj, ".contains ");
			args[0].accept(this, CiPriority.ARGUMENT);
			break;
		case CiId.LIST_CLEAR:
		case CiId.QUEUE_CLEAR:
		case CiId.STACK_CLEAR:
		case CiId.HASH_SET_CLEAR:
		case CiId.SORTED_SET_CLEAR:
		case CiId.DICTIONARY_CLEAR:
		case CiId.SORTED_DICTIONARY_CLEAR:
			this.writePostfix(obj, ".removeAll()");
			break;
		case CiId.LIST_INDEX_OF:
			if (parent > CiPriority.REL)
				this.writeChar(40);
			this.writePostfix(obj, ".firstIndex(of: ");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(") ?? -1");
			if (parent > CiPriority.REL)
				this.writeChar(41);
			break;
		case CiId.LIST_INSERT:
			this.writePostfix(obj, ".insert(");
			let elementType = obj.type.asClassType().getElementType();
			if (args.length == 1)
				this.writeNewStorage(elementType);
			else
				this.writeCoerced(elementType, args[1], CiPriority.ARGUMENT);
			this.write(", at: ");
			this.writeCoerced(this.#system.intType, args[0], CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.LIST_LAST:
		case CiId.STACK_PEEK:
			this.writePostfix(obj, ".last");
			break;
		case CiId.LIST_REMOVE_AT:
			this.writePostfix(obj, ".remove(at: ");
			this.writeCoerced(this.#system.intType, args[0], CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.LIST_REMOVE_RANGE:
			this.writePostfix(obj, ".removeSubrange(");
			this.#writeRange(args[0], args[1]);
			this.writeChar(41);
			break;
		case CiId.QUEUE_DEQUEUE:
			this.writePostfix(obj, ".removeFirst()");
			break;
		case CiId.QUEUE_PEEK:
			this.writePostfix(obj, ".first");
			break;
		case CiId.STACK_POP:
			this.writePostfix(obj, ".removeLast()");
			break;
		case CiId.HASH_SET_ADD:
		case CiId.SORTED_SET_ADD:
			this.writePostfix(obj, ".insert(");
			this.writeCoerced(obj.type.asClassType().getElementType(), args[0], CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.DICTIONARY_ADD:
			this.writeDictionaryAdd(obj, args);
			break;
		case CiId.DICTIONARY_CONTAINS_KEY:
		case CiId.SORTED_DICTIONARY_CONTAINS_KEY:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			this.writeIndexing(obj, args[0]);
			this.write(" != nil");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.DICTIONARY_REMOVE:
		case CiId.SORTED_DICTIONARY_REMOVE:
			this.writePostfix(obj, ".removeValue(forKey: ");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.CONSOLE_WRITE:
			this.write("print(");
			this.#writeUnwrapped(args[0], CiPriority.ARGUMENT, true);
			this.write(", terminator: \"\")");
			break;
		case CiId.CONSOLE_WRITE_LINE:
			this.write("print(");
			if (args.length == 1)
				this.#writeUnwrapped(args[0], CiPriority.ARGUMENT, true);
			this.writeChar(41);
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			this.#writeUnwrapped(args[0], CiPriority.PRIMARY, true);
			this.write(".utf8.count");
			break;
		case CiId.U_T_F8_GET_BYTES:
			if (this.#addVar("cibytes"))
				this.write(this.#varBytesAtIndent[this.indent] ? "var " : "let ");
			this.write("cibytes = [UInt8](");
			this.#writeUnwrapped(args[0], CiPriority.PRIMARY, true);
			this.writeLine(".utf8)");
			this.#openIndexing(args[1]);
			this.writeCoerced(this.#system.intType, args[2], CiPriority.SHIFT);
			if (args[2].isLiteralZero())
				this.write("..<");
			else {
				this.write(" ..< ");
				this.writeCoerced(this.#system.intType, args[2], CiPriority.ADD);
				this.write(" + ");
			}
			this.writeLine("cibytes.count] = cibytes[...]");
			break;
		case CiId.U_T_F8_GET_STRING:
			this.write("String(decoding: ");
			this.#openIndexing(args[0]);
			this.#writeRange(args[1], args[2]);
			this.write("], as: UTF8.self)");
			break;
		case CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE:
			this.include("Foundation");
			this.write("ProcessInfo.processInfo.environment[");
			this.#writeUnwrapped(args[0], CiPriority.ARGUMENT, false);
			this.writeChar(93);
			break;
		case CiId.MATH_METHOD:
		case CiId.MATH_LOG2:
			this.include("Foundation");
			this.writeCamelCase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_ABS:
		case CiId.MATH_MAX_INT:
		case CiId.MATH_MAX_DOUBLE:
		case CiId.MATH_MIN_INT:
		case CiId.MATH_MIN_DOUBLE:
			this.writeCamelCase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_CEILING:
			this.include("Foundation");
			this.writeCall("ceil", args[0]);
			break;
		case CiId.MATH_CLAMP:
			this.write("min(max(");
			this.writeClampAsMinMax(args);
			break;
		case CiId.MATH_FUSED_MULTIPLY_ADD:
			this.include("Foundation");
			this.writeCall("fma", args[0], args[1], args[2]);
			break;
		case CiId.MATH_IS_FINITE:
			this.writePostfix(args[0], ".isFinite");
			break;
		case CiId.MATH_IS_INFINITY:
			this.writePostfix(args[0], ".isInfinite");
			break;
		case CiId.MATH_IS_NA_N:
			this.writePostfix(args[0], ".isNaN");
			break;
		case CiId.MATH_ROUND:
			this.writePostfix(args[0], ".rounded()");
			break;
		case CiId.MATH_TRUNCATE:
			this.include("Foundation");
			this.writeCall("trunc", args[0]);
			break;
		default:
			this.notSupported(obj, method.name);
			break;
		}
	}

	writeNewArrayStorage(array)
	{
		if (GenSwift.#isArrayRef(array))
			super.writeNewArrayStorage(array);
		else {
			this.writeChar(91);
			this.#writeType(array.getElementType());
			this.write("](repeating: ");
			this.#writeDefaultValue(array.getElementType());
			this.write(", count: ");
			this.visitLiteralLong(BigInt(array.length));
			this.writeChar(41);
		}
	}

	writeNew(klass, parent)
	{
		this.#writeClassName(klass);
		this.write("()");
	}

	#writeDefaultValue(type)
	{
		if (type instanceof CiNumericType)
			this.writeChar(48);
		else if (type instanceof CiEnum) {
			const enu = type;
			if (enu.id == CiId.BOOL_TYPE)
				this.write("false");
			else {
				this.writeName(enu);
				this.writeChar(46);
				this.writeName(enu.getFirstValue());
			}
		}
		else if (type instanceof CiStringType && !type.nullable)
			this.write("\"\"");
		else if (type instanceof CiArrayStorageType) {
			const array = type;
			this.writeNewArrayStorage(array);
		}
		else
			this.write("nil");
	}

	writeNewArray(elementType, lengthExpr, parent)
	{
		this.#arrayRef = true;
		this.write("ArrayRef<");
		this.#writeType(elementType);
		this.write(">(");
		if (elementType instanceof CiArrayStorageType) {
			this.write("factory: { ");
			this.writeNewStorage(elementType);
			this.write(" }");
		}
		else if (elementType instanceof CiStorageType) {
			const klass = elementType;
			this.write("factory: ");
			this.writeName(klass.class);
			this.write(".init");
		}
		else {
			this.write("repeating: ");
			this.#writeDefaultValue(elementType);
		}
		this.write(", count: ");
		lengthExpr.accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	visitPrefixExpr(expr, parent)
	{
		if (expr.op == CiToken.TILDE && expr.type instanceof CiEnumFlags) {
			this.write(expr.type.name);
			this.write("(rawValue: ~");
			this.writePostfix(expr.inner, ".rawValue)");
		}
		else
			super.visitPrefixExpr(expr, parent);
	}

	writeIndexingExpr(expr, parent)
	{
		this.#openIndexing(expr.left);
		const klass = expr.left.type;
		let indexType;
		switch (klass.class.id) {
		case CiId.ARRAY_PTR_CLASS:
		case CiId.ARRAY_STORAGE_CLASS:
		case CiId.LIST_CLASS:
			indexType = this.#system.intType;
			break;
		default:
			indexType = klass.getKeyType();
			break;
		}
		this.writeCoerced(indexType, expr.right, CiPriority.ARGUMENT);
		this.writeChar(93);
		let dict;
		if (parent != CiPriority.ASSIGN && (dict = expr.left.type) instanceof CiClassType && dict.class.typeParameterCount == 2)
			this.writeChar(33);
	}

	writeBinaryOperand(expr, parent, binary)
	{
		if (expr.type.id != CiId.BOOL_TYPE) {
			if (binary.op == CiToken.PLUS && binary.type.id == CiId.STRING_STORAGE_TYPE) {
				this.#writeUnwrapped(expr, parent, true);
				return;
			}
			if (binary.op == CiToken.PLUS || binary.op == CiToken.MINUS || binary.op == CiToken.ASTERISK || binary.op == CiToken.SLASH || binary.op == CiToken.MOD || binary.op == CiToken.AND || binary.op == CiToken.OR || binary.op == CiToken.XOR || (binary.op == CiToken.SHIFT_LEFT && expr == binary.left) || (binary.op == CiToken.SHIFT_RIGHT && expr == binary.left)) {
				if (!(expr instanceof CiLiteral)) {
					let type = this.#system.promoteNumericTypes(binary.left.type, binary.right.type);
					if (type != expr.type) {
						this.writeCoerced(type, expr, parent);
						return;
					}
				}
			}
			else if (binary.op == CiToken.EQUAL || binary.op == CiToken.NOT_EQUAL || binary.op == CiToken.LESS || binary.op == CiToken.LESS_OR_EQUAL || binary.op == CiToken.GREATER || binary.op == CiToken.GREATER_OR_EQUAL) {
				let typeComp = this.#system.promoteFloatingTypes(binary.left.type, binary.right.type);
				if (typeComp != null && typeComp != expr.type) {
					this.writeCoerced(typeComp, expr, parent);
					return;
				}
			}
		}
		expr.accept(this, parent);
	}

	#writeEnumFlagsAnd(left, method, notMethod, right)
	{
		let negation;
		if ((negation = right) instanceof CiPrefixExpr && negation.op == CiToken.TILDE)
			this.writeMethodCall(left, notMethod, negation.inner);
		else
			this.writeMethodCall(left, method, right);
	}

	#writeAssignNested(expr)
	{
		let rightBinary;
		if ((rightBinary = expr.right) instanceof CiBinaryExpr && rightBinary.isAssign()) {
			this.visitBinaryExpr(rightBinary, CiPriority.STATEMENT);
			this.writeNewLine();
			return rightBinary.left;
		}
		return expr.right;
	}

	#writeSwiftAssign(expr, right)
	{
		expr.left.accept(this, CiPriority.ASSIGN);
		this.writeChar(32);
		this.write(expr.getOpString());
		this.writeChar(32);
		let leftBinary;
		let dict;
		if (right instanceof CiLiteralNull && (leftBinary = expr.left) instanceof CiBinaryExpr && leftBinary.op == CiToken.LEFT_BRACKET && (dict = leftBinary.left.type) instanceof CiClassType && dict.class.typeParameterCount == 2) {
			this.#writeType(dict.getValueType());
			this.write(".none");
		}
		else
			this.writeCoerced(expr.type, right, CiPriority.ARGUMENT);
	}

	visitBinaryExpr(expr, parent)
	{
		let right;
		switch (expr.op) {
		case CiToken.SHIFT_LEFT:
			this.writeBinaryExpr(expr, parent > CiPriority.MUL, CiPriority.PRIMARY, " << ", CiPriority.PRIMARY);
			break;
		case CiToken.SHIFT_RIGHT:
			this.writeBinaryExpr(expr, parent > CiPriority.MUL, CiPriority.PRIMARY, " >> ", CiPriority.PRIMARY);
			break;
		case CiToken.AND:
			if (expr.type.id == CiId.BOOL_TYPE)
				this.writeCall("{ a, b in a && b }", expr.left, expr.right);
			else if (expr.type instanceof CiEnumFlags)
				this.#writeEnumFlagsAnd(expr.left, "intersection", "subtracting", expr.right);
			else
				this.writeBinaryExpr(expr, parent > CiPriority.MUL, CiPriority.MUL, " & ", CiPriority.PRIMARY);
			break;
		case CiToken.OR:
			if (expr.type.id == CiId.BOOL_TYPE)
				this.writeCall("{ a, b in a || b }", expr.left, expr.right);
			else if (expr.type instanceof CiEnumFlags)
				this.writeMethodCall(expr.left, "union", expr.right);
			else
				this.writeBinaryExpr(expr, parent > CiPriority.ADD, CiPriority.ADD, " | ", CiPriority.MUL);
			break;
		case CiToken.XOR:
			if (expr.type.id == CiId.BOOL_TYPE)
				this.writeEqual(expr.left, expr.right, parent, true);
			else if (expr.type instanceof CiEnumFlags)
				this.writeMethodCall(expr.left, "symmetricDifference", expr.right);
			else
				this.writeBinaryExpr(expr, parent > CiPriority.ADD, CiPriority.ADD, " ^ ", CiPriority.MUL);
			break;
		case CiToken.ASSIGN:
		case CiToken.ADD_ASSIGN:
		case CiToken.SUB_ASSIGN:
		case CiToken.MUL_ASSIGN:
		case CiToken.DIV_ASSIGN:
		case CiToken.MOD_ASSIGN:
		case CiToken.SHIFT_LEFT_ASSIGN:
		case CiToken.SHIFT_RIGHT_ASSIGN:
			this.#writeSwiftAssign(expr, this.#writeAssignNested(expr));
			break;
		case CiToken.AND_ASSIGN:
			right = this.#writeAssignNested(expr);
			if (expr.type.id == CiId.BOOL_TYPE) {
				this.write("if ");
				let negation;
				if ((negation = right) instanceof CiPrefixExpr && negation.op == CiToken.EXCLAMATION_MARK) {
					negation.inner.accept(this, CiPriority.ARGUMENT);
				}
				else {
					this.writeChar(33);
					right.accept(this, CiPriority.PRIMARY);
				}
				this.openChild();
				expr.left.accept(this, CiPriority.ASSIGN);
				this.writeLine(" = false");
				this.indent--;
				this.writeChar(125);
			}
			else if (expr.type instanceof CiEnumFlags)
				this.#writeEnumFlagsAnd(expr.left, "formIntersection", "subtract", right);
			else
				this.#writeSwiftAssign(expr, right);
			break;
		case CiToken.OR_ASSIGN:
			right = this.#writeAssignNested(expr);
			if (expr.type.id == CiId.BOOL_TYPE) {
				this.write("if ");
				right.accept(this, CiPriority.ARGUMENT);
				this.openChild();
				expr.left.accept(this, CiPriority.ASSIGN);
				this.writeLine(" = true");
				this.indent--;
				this.writeChar(125);
			}
			else if (expr.type instanceof CiEnumFlags)
				this.writeMethodCall(expr.left, "formUnion", right);
			else
				this.#writeSwiftAssign(expr, right);
			break;
		case CiToken.XOR_ASSIGN:
			right = this.#writeAssignNested(expr);
			if (expr.type.id == CiId.BOOL_TYPE) {
				expr.left.accept(this, CiPriority.ASSIGN);
				this.write(" = ");
				expr.left.accept(this, CiPriority.EQUALITY);
				this.write(" != ");
				expr.right.accept(this, CiPriority.EQUALITY);
			}
			else if (expr.type instanceof CiEnumFlags)
				this.writeMethodCall(expr.left, "formSymmetricDifference", right);
			else
				this.#writeSwiftAssign(expr, right);
			break;
		default:
			super.visitBinaryExpr(expr, parent);
			break;
		}
	}

	writeResource(name, length)
	{
		this.write("CiResource.");
		this.writeResourceName(name);
	}

	static #throws(expr)
	{
		if (expr instanceof CiVar || expr instanceof CiLiteral || expr instanceof CiLambdaExpr)
			return false;
		else if (expr instanceof CiAggregateInitializer) {
			const init = expr;
			return init.items.some(field => GenSwift.#throws(field));
		}
		else if (expr instanceof CiInterpolatedString) {
			const interp = expr;
			return interp.parts.some(part => GenSwift.#throws(part.argument));
		}
		else if (expr instanceof CiSymbolReference) {
			const symbol = expr;
			return symbol.left != null && GenSwift.#throws(symbol.left);
		}
		else if (expr instanceof CiUnaryExpr) {
			const unary = expr;
			return unary.inner != null && GenSwift.#throws(unary.inner);
		}
		else if (expr instanceof CiBinaryExpr) {
			const binary = expr;
			return GenSwift.#throws(binary.left) || GenSwift.#throws(binary.right);
		}
		else if (expr instanceof CiSelectExpr) {
			const select = expr;
			return GenSwift.#throws(select.cond) || GenSwift.#throws(select.onTrue) || GenSwift.#throws(select.onFalse);
		}
		else if (expr instanceof CiCallExpr) {
			const call = expr;
			const method = call.method.symbol;
			return method.throws || (call.method.left != null && GenSwift.#throws(call.method.left)) || call.arguments.some(arg => GenSwift.#throws(arg));
		}
		else
			throw new Error();
	}

	writeExpr(expr, parent)
	{
		if (GenSwift.#throws(expr))
			this.write("try ");
		super.writeExpr(expr, parent);
	}

	writeCoercedExpr(type, expr)
	{
		if (GenSwift.#throws(expr))
			this.write("try ");
		super.writeCoercedExpr(type, expr);
	}

	startTemporaryVar(type)
	{
		this.write("var ");
	}

	visitExpr(statement)
	{
		this.writeTemporaries(statement);
		let call;
		if ((call = statement) instanceof CiCallExpr && statement.type.id != CiId.VOID_TYPE)
			this.write("_ = ");
		super.visitExpr(statement);
	}

	#initVarsAtIndent()
	{
		while (this.#varsAtIndent.length <= this.indent) {
			this.#varsAtIndent.push(new Set());
			this.#varBytesAtIndent.push(false);
		}
		this.#varsAtIndent[this.indent].clear();
		this.#varBytesAtIndent[this.indent] = false;
	}

	openChild()
	{
		this.writeChar(32);
		this.openBlock();
		this.#initVarsAtIndent();
	}

	closeChild()
	{
		this.closeBlock();
	}

	writeVar(def)
	{
		if (def instanceof CiField || this.#addVar(def.name)) {
			let array;
			let stg;
			let local;
			this.write(((array = def.type) instanceof CiArrayStorageType ? GenSwift.#isArrayRef(array) : (stg = def.type) instanceof CiStorageType ? stg.class.typeParameterCount == 0 && !def.isAssignableStorage() : (local = def) instanceof CiVar && !local.isAssigned) ? "let " : "var ");
			super.writeVar(def);
		}
		else {
			this.writeName(def);
			this.writeVarInit(def);
		}
	}

	static #needsVarBytes(statements)
	{
		let count = 0;
		for (const statement of statements) {
			let call;
			if ((call = statement) instanceof CiCallExpr && call.method.symbol.id == CiId.U_T_F8_GET_BYTES) {
				if (++count == 2)
					return true;
			}
		}
		return false;
	}

	writeStatements(statements)
	{
		this.#varBytesAtIndent[this.indent] = GenSwift.#needsVarBytes(statements);
		super.writeStatements(statements);
	}

	visitLambdaExpr(expr)
	{
		this.write("{ ");
		this.writeName(expr.first);
		this.write(" in ");
		expr.body.accept(this, CiPriority.STATEMENT);
		this.write(" }");
	}

	writeAssertCast(expr)
	{
		this.write("let ");
		const def = expr.right;
		this.#writeCamelCaseNotKeyword(def.name);
		this.write(" = ");
		expr.left.accept(this, CiPriority.EQUALITY);
		this.write(" as! ");
		this.writeLine(def.type.name);
	}

	writeAssert(statement)
	{
		this.write("assert(");
		this.writeExpr(statement.cond, CiPriority.ARGUMENT);
		if (statement.message != null) {
			this.write(", ");
			this.writeExpr(statement.message, CiPriority.ARGUMENT);
		}
		this.writeCharLine(41);
	}

	visitBreak(statement)
	{
		this.writeLine("break");
	}

	needCondXcrement(loop)
	{
		return loop.cond != null && (!loop.hasBreak || !this.visitXcrement(loop.cond, true, false));
	}

	getIfNot()
	{
		return "if !";
	}

	writeContinueDoWhile(cond)
	{
		this.visitXcrement(cond, false, true);
		this.writeLine("continue");
	}

	visitDoWhile(statement)
	{
		if (this.visitXcrement(statement.cond, true, false))
			super.visitDoWhile(statement);
		else {
			this.write("repeat");
			this.openChild();
			statement.body.acceptStatement(this);
			if (statement.body.completesNormally())
				this.visitXcrement(statement.cond, false, true);
			this.closeChild();
			this.write("while ");
			this.writeExpr(statement.cond, CiPriority.ARGUMENT);
			this.writeNewLine();
		}
	}

	writeElseIf()
	{
		this.write("else ");
	}

	openWhile(loop)
	{
		if (this.needCondXcrement(loop))
			super.openWhile(loop);
		else {
			this.write("while true");
			this.openChild();
			this.visitXcrement(loop.cond, false, true);
			this.write("let ciDoLoop = ");
			loop.cond.accept(this, CiPriority.ARGUMENT);
			this.writeNewLine();
			this.visitXcrement(loop.cond, true, true);
			this.write("if !ciDoLoop");
			this.openChild();
			this.writeLine("break");
			this.closeChild();
		}
	}

	writeForRange(iter, cond, rangeStep)
	{
		if (rangeStep == 1) {
			this.writeExpr(iter.value, CiPriority.SHIFT);
			switch (cond.op) {
			case CiToken.LESS:
				this.write("..<");
				cond.right.accept(this, CiPriority.SHIFT);
				break;
			case CiToken.LESS_OR_EQUAL:
				this.write("...");
				cond.right.accept(this, CiPriority.SHIFT);
				break;
			default:
				throw new Error();
			}
		}
		else {
			this.write("stride(from: ");
			this.writeExpr(iter.value, CiPriority.ARGUMENT);
			switch (cond.op) {
			case CiToken.LESS:
			case CiToken.GREATER:
				this.write(", to: ");
				this.writeExpr(cond.right, CiPriority.ARGUMENT);
				break;
			case CiToken.LESS_OR_EQUAL:
			case CiToken.GREATER_OR_EQUAL:
				this.write(", through: ");
				this.writeExpr(cond.right, CiPriority.ARGUMENT);
				break;
			default:
				throw new Error();
			}
			this.write(", by: ");
			this.visitLiteralLong(rangeStep);
			this.writeChar(41);
		}
	}

	visitForeach(statement)
	{
		this.write("for ");
		if (statement.count() == 2) {
			this.writeChar(40);
			this.writeName(statement.getVar());
			this.write(", ");
			this.writeName(statement.getValueVar());
			this.writeChar(41);
		}
		else
			this.writeName(statement.getVar());
		this.write(" in ");
		const klass = statement.collection.type;
		switch (klass.class.id) {
		case CiId.STRING_CLASS:
			this.writePostfix(statement.collection, ".unicodeScalars");
			break;
		case CiId.SORTED_SET_CLASS:
			this.writePostfix(statement.collection, ".sorted()");
			break;
		case CiId.SORTED_DICTIONARY_CLASS:
			this.writePostfix(statement.collection, klass.getKeyType().nullable ? ".sorted(by: { $0.key! < $1.key! })" : ".sorted(by: { $0.key < $1.key })");
			break;
		default:
			this.writeExpr(statement.collection, CiPriority.ARGUMENT);
			break;
		}
		this.writeChild(statement.body);
	}

	visitLock(statement)
	{
		statement.lock.accept(this, CiPriority.PRIMARY);
		this.writeLine(".lock()");
		this.write("do");
		this.openChild();
		this.write("defer { ");
		statement.lock.accept(this, CiPriority.PRIMARY);
		this.writeLine(".unlock() }");
		statement.body.acceptStatement(this);
		this.closeChild();
	}

	writeResultVar()
	{
		this.write("let result : ");
		this.#writeType(this.currentMethod.type);
	}

	#writeSwitchCaseVar(def)
	{
		if (def.name == "_")
			this.write("is ");
		else {
			this.write("let ");
			this.#writeCamelCaseNotKeyword(def.name);
			this.write(" as ");
		}
		this.#writeType(def.type);
	}

	#writeSwiftSwitchCaseBody(statement, body)
	{
		this.indent++;
		this.visitXcrement(statement.value, true, true);
		this.#initVarsAtIndent();
		this.writeSwitchCaseBody(body);
		this.indent--;
	}

	visitSwitch(statement)
	{
		this.visitXcrement(statement.value, false, true);
		this.write("switch ");
		this.writeExpr(statement.value, CiPriority.ARGUMENT);
		this.writeLine(" {");
		for (const kase of statement.cases) {
			this.write("case ");
			for (let i = 0; i < kase.values.length; i++) {
				this.writeComma(i);
				let when1;
				if ((when1 = kase.values[i]) instanceof CiBinaryExpr && when1.op == CiToken.WHEN) {
					let whenVar;
					if ((whenVar = when1.left) instanceof CiVar)
						this.#writeSwitchCaseVar(whenVar);
					else
						this.writeCoerced(statement.value.type, when1.left, CiPriority.ARGUMENT);
					this.write(" where ");
					this.writeExpr(when1.right, CiPriority.ARGUMENT);
				}
				else if (kase.values[i] instanceof CiVar) {
					const def = kase.values[i];
					this.#writeSwitchCaseVar(def);
				}
				else
					this.writeCoerced(statement.value.type, kase.values[i], CiPriority.ARGUMENT);
			}
			this.writeCharLine(58);
			this.#writeSwiftSwitchCaseBody(statement, kase.body);
		}
		if (statement.defaultBody.length > 0) {
			this.writeLine("default:");
			this.#writeSwiftSwitchCaseBody(statement, statement.defaultBody);
		}
		this.writeCharLine(125);
	}

	visitThrow(statement)
	{
		this.#throw = true;
		this.visitXcrement(statement.message, false, true);
		this.write("throw CiError.error(");
		this.writeExpr(statement.message, CiPriority.ARGUMENT);
		this.writeCharLine(41);
	}

	#writeReadOnlyParameter(param)
	{
		this.write("ciParam");
		this.writePascalCase(param.name);
	}

	writeParameter(param)
	{
		this.write("_ ");
		if (param.isAssigned)
			this.#writeReadOnlyParameter(param);
		else
			this.writeName(param);
		this.write(" : ");
		this.#writeType(param.type);
	}

	visitEnumValue(konst, previous)
	{
		this.writeDoc(konst.documentation);
		this.write("static let ");
		this.writeName(konst);
		this.write(" = ");
		this.write(konst.parent.name);
		this.writeChar(40);
		let i = konst.value.intValue();
		if (i == 0)
			this.write("[]");
		else {
			this.write("rawValue: ");
			this.visitLiteralLong(BigInt(i));
		}
		this.writeCharLine(41);
	}

	writeEnum(enu)
	{
		this.writeNewLine();
		this.writeDoc(enu.documentation);
		this.writePublic(enu);
		if (enu instanceof CiEnumFlags) {
			this.write("struct ");
			this.write(enu.name);
			this.writeLine(" : OptionSet");
			this.openBlock();
			this.writeLine("let rawValue : Int");
			enu.acceptValues(this);
		}
		else {
			this.write("enum ");
			this.write(enu.name);
			if (enu.hasExplicitValue)
				this.write(" : Int");
			this.writeNewLine();
			this.openBlock();
			const valueToConst = {};
			for (let symbol = enu.first; symbol != null; symbol = symbol.next) {
				let konst;
				if ((konst = symbol) instanceof CiConst) {
					this.writeDoc(konst.documentation);
					let i = konst.value.intValue();
					if (valueToConst.hasOwnProperty(i)) {
						this.write("static let ");
						this.writeName(konst);
						this.write(" = ");
						this.writeName(valueToConst[i]);
					}
					else {
						this.write("case ");
						this.writeName(konst);
						if (!(konst.value instanceof CiImplicitEnumValue)) {
							this.write(" = ");
							this.visitLiteralLong(BigInt(i));
						}
						valueToConst[i] = konst;
					}
					this.writeNewLine();
				}
			}
		}
		this.closeBlock();
	}

	#writeVisibility(visibility)
	{
		switch (visibility) {
		case CiVisibility.PRIVATE:
			this.write("private ");
			break;
		case CiVisibility.INTERNAL:
			this.write("fileprivate ");
			break;
		case CiVisibility.PROTECTED:
		case CiVisibility.PUBLIC:
			this.write("public ");
			break;
		default:
			throw new Error();
		}
	}

	writeConst(konst)
	{
		this.writeNewLine();
		this.writeDoc(konst.documentation);
		this.#writeVisibility(konst.visibility);
		this.write("static let ");
		this.writeName(konst);
		this.write(" = ");
		if (konst.type.id == CiId.INT_TYPE || konst.type instanceof CiEnum || konst.type.id == CiId.STRING_PTR_TYPE)
			konst.value.accept(this, CiPriority.ARGUMENT);
		else {
			this.#writeType(konst.type);
			this.writeChar(40);
			konst.value.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
		}
		this.writeNewLine();
	}

	writeField(field)
	{
		this.writeNewLine();
		this.writeDoc(field.documentation);
		this.#writeVisibility(field.visibility);
		let klass;
		if ((klass = field.type) instanceof CiClassType && klass.class.id != CiId.STRING_CLASS && !(klass instanceof CiDynamicPtrType) && !(klass instanceof CiStorageType))
			this.write("unowned ");
		this.writeVar(field);
		if (field.value == null && (field.type instanceof CiNumericType || field.type instanceof CiEnum || field.type.id == CiId.STRING_STORAGE_TYPE)) {
			this.write(" = ");
			this.#writeDefaultValue(field.type);
		}
		else if (field.isAssignableStorage()) {
			this.write(" = ");
			this.writeName(field.type.asClassType().class);
			this.write("()");
		}
		this.writeNewLine();
	}

	writeParameterDoc(param, first)
	{
		this.write("/// - parameter ");
		this.writeName(param);
		this.writeChar(32);
		this.writeDocPara(param.documentation.summary, false);
		this.writeNewLine();
	}

	writeMethod(method)
	{
		this.writeNewLine();
		this.writeDoc(method.documentation);
		this.writeParametersDoc(method);
		switch (method.callType) {
		case CiCallType.STATIC:
			this.#writeVisibility(method.visibility);
			this.write("static ");
			break;
		case CiCallType.NORMAL:
			this.#writeVisibility(method.visibility);
			break;
		case CiCallType.ABSTRACT:
		case CiCallType.VIRTUAL:
			this.write(method.visibility == CiVisibility.INTERNAL ? "fileprivate " : "open ");
			break;
		case CiCallType.OVERRIDE:
			this.write(method.visibility == CiVisibility.INTERNAL ? "fileprivate " : "open ");
			this.write("override ");
			break;
		case CiCallType.SEALED:
			this.#writeVisibility(method.visibility);
			this.write("final override ");
			break;
		}
		if (method.id == CiId.CLASS_TO_STRING)
			this.write("var description : String");
		else {
			this.write("func ");
			this.writeName(method);
			this.writeParameters(method, true);
			if (method.throws)
				this.write(" throws");
			if (method.type.id != CiId.VOID_TYPE) {
				this.write(" -> ");
				this.#writeType(method.type);
			}
		}
		this.writeNewLine();
		this.openBlock();
		if (method.callType == CiCallType.ABSTRACT)
			this.writeLine("preconditionFailure(\"Abstract method called\")");
		else {
			for (let param = method.parameters.firstParameter(); param != null; param = param.nextParameter()) {
				if (param.isAssigned) {
					this.write("var ");
					this.writeTypeAndName(param);
					this.write(" = ");
					this.#writeReadOnlyParameter(param);
					this.writeNewLine();
				}
			}
			this.#initVarsAtIndent();
			this.currentMethod = method;
			method.body.acceptStatement(this);
			this.currentMethod = null;
		}
		this.closeBlock();
	}

	writeClass(klass, program)
	{
		this.writeNewLine();
		this.writeDoc(klass.documentation);
		this.writePublic(klass);
		if (klass.callType == CiCallType.SEALED)
			this.write("final ");
		this.startClass(klass, "", " : ");
		if (klass.addsToString()) {
			this.write(klass.hasBaseClass() ? ", " : " : ");
			this.write("CustomStringConvertible");
		}
		this.writeNewLine();
		this.openBlock();
		if (this.needsConstructor(klass)) {
			if (klass.constructor_ != null) {
				this.writeDoc(klass.constructor_.documentation);
				this.#writeVisibility(klass.constructor_.visibility);
			}
			else
				this.write("fileprivate ");
			if (klass.hasBaseClass())
				this.write("override ");
			this.writeLine("init()");
			this.openBlock();
			this.#initVarsAtIndent();
			this.writeConstructorBody(klass);
			this.closeBlock();
		}
		this.writeMembers(klass, true);
		this.closeBlock();
	}

	#writeLibrary()
	{
		if (this.#throw) {
			this.writeNewLine();
			this.writeLine("public enum CiError : Error");
			this.openBlock();
			this.writeLine("case error(String)");
			this.closeBlock();
		}
		if (this.#arrayRef) {
			this.writeNewLine();
			this.writeLine("public class ArrayRef<T> : Sequence");
			this.openBlock();
			this.writeLine("var array : [T]");
			this.writeNewLine();
			this.writeLine("init(_ array : [T])");
			this.openBlock();
			this.writeLine("self.array = array");
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("init(repeating: T, count: Int)");
			this.openBlock();
			this.writeLine("self.array = [T](repeating: repeating, count: count)");
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("init(factory: () -> T, count: Int)");
			this.openBlock();
			this.writeLine("self.array = (1...count).map({_ in factory() })");
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("subscript(index: Int) -> T");
			this.openBlock();
			this.writeLine("get");
			this.openBlock();
			this.writeLine("return array[index]");
			this.closeBlock();
			this.writeLine("set(value)");
			this.openBlock();
			this.writeLine("array[index] = value");
			this.closeBlock();
			this.closeBlock();
			this.writeLine("subscript(bounds: Range<Int>) -> ArraySlice<T>");
			this.openBlock();
			this.writeLine("get");
			this.openBlock();
			this.writeLine("return array[bounds]");
			this.closeBlock();
			this.writeLine("set(value)");
			this.openBlock();
			this.writeLine("array[bounds] = value");
			this.closeBlock();
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("func fill(_ value: T)");
			this.openBlock();
			this.writeLine("array = [T](repeating: value, count: array.count)");
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("func fill(_ value: T, _ startIndex : Int, _ count : Int)");
			this.openBlock();
			this.writeLine("array[startIndex ..< startIndex + count] = ArraySlice(repeating: value, count: count)");
			this.closeBlock();
			this.writeNewLine();
			this.writeLine("public func makeIterator() -> IndexingIterator<Array<T>>");
			this.openBlock();
			this.writeLine("return array.makeIterator()");
			this.closeBlock();
			this.closeBlock();
		}
		if (this.#stringCharAt) {
			this.writeNewLine();
			this.writeLine("fileprivate func ciStringCharAt(_ s: String, _ offset: Int) -> Int");
			this.openBlock();
			this.writeLine("return Int(s.unicodeScalars[s.index(s.startIndex, offsetBy: offset)].value)");
			this.closeBlock();
		}
		if (this.#stringIndexOf) {
			this.writeNewLine();
			this.writeLine("fileprivate func ciStringIndexOf<S1 : StringProtocol, S2 : StringProtocol>(_ haystack: S1, _ needle: S2, _ options: String.CompareOptions = .literal) -> Int");
			this.openBlock();
			this.writeLine("guard let index = haystack.range(of: needle, options: options) else { return -1 }");
			this.writeLine("return haystack.distance(from: haystack.startIndex, to: index.lowerBound)");
			this.closeBlock();
		}
		if (this.#stringSubstring) {
			this.writeNewLine();
			this.writeLine("fileprivate func ciStringSubstring(_ s: String, _ offset: Int) -> Substring");
			this.openBlock();
			this.writeLine("return s[s.index(s.startIndex, offsetBy: offset)...]");
			this.closeBlock();
		}
	}

	#writeResources(resources)
	{
		if (Object.keys(resources).length == 0)
			return;
		this.#arrayRef = true;
		this.writeNewLine();
		this.writeLine("fileprivate final class CiResource");
		this.openBlock();
		for (const [name, content] of Object.entries(resources).sort((a, b) => a[0].localeCompare(b[0]))) {
			this.write("static let ");
			this.writeResourceName(name);
			this.writeLine(" = ArrayRef<UInt8>([");
			this.writeChar(9);
			this.writeBytes(content);
			this.writeLine(" ])");
		}
		this.closeBlock();
	}

	writeProgram(program)
	{
		this.#system = program.system;
		this.#throw = false;
		this.#arrayRef = false;
		this.#stringCharAt = false;
		this.#stringIndexOf = false;
		this.#stringSubstring = false;
		this.openStringWriter();
		this.writeTypes(program);
		this.createOutputFile();
		this.writeTopLevelNatives(program);
		this.writeIncludes("import ", "");
		this.closeStringWriter();
		this.#writeLibrary();
		this.#writeResources(program.resources);
		this.closeFile();
	}
}

export class GenPy extends GenPySwift
{
	#childPass;
	#switchBreak;

	getTargetName()
	{
		return "Python";
	}

	writeBanner()
	{
		this.writeLine("# Generated automatically with \"cito\". Do not edit.");
	}

	startDocLine()
	{
	}

	getDocBullet()
	{
		return " * ";
	}

	#startDoc(doc)
	{
		this.write("\"\"\"");
		this.writeDocPara(doc.summary, false);
		if (doc.details.length > 0) {
			this.writeNewLine();
			for (const block of doc.details) {
				this.writeNewLine();
				this.writeDocBlock(block, false);
			}
		}
	}

	writeDoc(doc)
	{
		if (doc != null) {
			this.#startDoc(doc);
			this.writeLine("\"\"\"");
		}
	}

	writeParameterDoc(param, first)
	{
		if (first) {
			this.writeNewLine();
			this.writeNewLine();
		}
		this.write(":param ");
		this.writeName(param);
		this.write(": ");
		this.writeDocPara(param.documentation.summary, false);
		this.writeNewLine();
	}

	#writePyDoc(method)
	{
		if (method.documentation == null)
			return;
		this.#startDoc(method.documentation);
		this.writeParametersDoc(method);
		this.writeLine("\"\"\"");
	}

	visitLiteralNull()
	{
		this.write("None");
	}

	visitLiteralFalse()
	{
		this.write("False");
	}

	visitLiteralTrue()
	{
		this.write("True");
	}

	#writeNameNotKeyword(name)
	{
		switch (name) {
		case "this":
			this.write("self");
			break;
		case "and":
		case "array":
		case "as":
		case "async":
		case "await":
		case "def":
		case "del":
		case "elif":
		case "enum":
		case "except":
		case "finally":
		case "from":
		case "global":
		case "import":
		case "is":
		case "lambda":
		case "len":
		case "math":
		case "nonlocal":
		case "not":
		case "or":
		case "pass":
		case "pyfma":
		case "raise":
		case "re":
		case "sys":
		case "try":
		case "with":
		case "yield":
			this.write(name);
			this.writeChar(95);
			break;
		default:
			this.writeLowercaseWithUnderscores(name);
			break;
		}
	}

	writeName(symbol)
	{
		if (symbol instanceof CiContainerType) {
			const container = symbol;
			if (!container.isPublic)
				this.writeChar(95);
			this.write(symbol.name);
		}
		else if (symbol instanceof CiConst) {
			const konst = symbol;
			if (konst.visibility != CiVisibility.PUBLIC)
				this.writeChar(95);
			if (konst.inMethod != null) {
				this.writeUppercaseWithUnderscores(konst.inMethod.name);
				this.writeChar(95);
			}
			this.writeUppercaseWithUnderscores(symbol.name);
		}
		else if (symbol instanceof CiVar)
			this.#writeNameNotKeyword(symbol.name);
		else if (symbol instanceof CiMember) {
			const member = symbol;
			if (member.id == CiId.CLASS_TO_STRING)
				this.write("__str__");
			else if (member.visibility == CiVisibility.PUBLIC)
				this.#writeNameNotKeyword(symbol.name);
			else {
				this.writeChar(95);
				this.writeLowercaseWithUnderscores(symbol.name);
			}
		}
		else
			throw new Error();
	}

	writeTypeAndName(value)
	{
		this.writeName(value);
	}

	writeLocalName(symbol, parent)
	{
		let forEach;
		if ((forEach = symbol.parent) instanceof CiForeach && forEach.collection.type instanceof CiStringType) {
			this.write("ord(");
			this.#writeNameNotKeyword(symbol.name);
			this.writeChar(41);
		}
		else
			super.writeLocalName(symbol, parent);
	}

	static #getArrayCode(type)
	{
		switch (type.id) {
		case CiId.S_BYTE_RANGE:
			return 98;
		case CiId.BYTE_RANGE:
			return 66;
		case CiId.SHORT_RANGE:
			return 104;
		case CiId.U_SHORT_RANGE:
			return 72;
		case CiId.INT_TYPE:
			return 105;
		case CiId.LONG_TYPE:
			return 113;
		case CiId.FLOAT_TYPE:
			return 102;
		case CiId.DOUBLE_TYPE:
			return 100;
		default:
			throw new Error();
		}
	}

	visitAggregateInitializer(expr)
	{
		const array = expr.type;
		let number;
		if ((number = array.getElementType()) instanceof CiNumericType) {
			let c = GenPy.#getArrayCode(number);
			if (c == 66)
				this.write("bytes(");
			else {
				this.include("array");
				this.write("array.array(\"");
				this.writeChar(c);
				this.write("\", ");
			}
			super.visitAggregateInitializer(expr);
			this.writeChar(41);
		}
		else
			super.visitAggregateInitializer(expr);
	}

	visitInterpolatedString(expr, parent)
	{
		this.write("f\"");
		for (const part of expr.parts) {
			this.writeDoubling(part.prefix, 123);
			this.writeChar(123);
			part.argument.accept(this, CiPriority.ARGUMENT);
			this.writePyFormat(part);
		}
		this.writeDoubling(expr.suffix, 123);
		this.writeChar(34);
	}

	visitPrefixExpr(expr, parent)
	{
		if (expr.op == CiToken.EXCLAMATION_MARK) {
			if (parent > CiPriority.COND_AND)
				this.writeChar(40);
			this.write("not ");
			expr.inner.accept(this, CiPriority.OR);
			if (parent > CiPriority.COND_AND)
				this.writeChar(41);
		}
		else
			super.visitPrefixExpr(expr, parent);
	}

	getReferenceEqOp(not)
	{
		return not ? " is not " : " is ";
	}

	writeCharAt(expr)
	{
		this.write("ord(");
		this.writeIndexingExpr(expr, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	writeStringLength(expr)
	{
		this.writeCall("len", expr);
	}

	visitSymbolReference(expr, parent)
	{
		switch (expr.symbol.id) {
		case CiId.CONSOLE_ERROR:
			this.include("sys");
			this.write("sys.stderr");
			break;
		case CiId.LIST_COUNT:
		case CiId.QUEUE_COUNT:
		case CiId.STACK_COUNT:
		case CiId.HASH_SET_COUNT:
		case CiId.SORTED_SET_COUNT:
		case CiId.DICTIONARY_COUNT:
		case CiId.SORTED_DICTIONARY_COUNT:
		case CiId.ORDERED_DICTIONARY_COUNT:
			this.writeStringLength(expr.left);
			break;
		case CiId.MATH_NA_N:
			this.include("math");
			this.write("math.nan");
			break;
		case CiId.MATH_NEGATIVE_INFINITY:
			this.include("math");
			this.write("-math.inf");
			break;
		case CiId.MATH_POSITIVE_INFINITY:
			this.include("math");
			this.write("math.inf");
			break;
		default:
			if (!this.writeJavaMatchProperty(expr, parent))
				super.visitSymbolReference(expr, parent);
			break;
		}
	}

	visitBinaryExpr(expr, parent)
	{
		switch (expr.op) {
		case CiToken.SLASH:
			if (expr.type instanceof CiIntegerType) {
				let floorDiv;
				let leftRange;
				let rightRange;
				if ((leftRange = expr.left) instanceof CiRangeType && leftRange.min >= 0 && (rightRange = expr.right) instanceof CiRangeType && rightRange.min >= 0) {
					if (parent > CiPriority.OR)
						this.writeChar(40);
					floorDiv = true;
				}
				else {
					this.write("int(");
					floorDiv = false;
				}
				expr.left.accept(this, CiPriority.MUL);
				this.write(floorDiv ? " // " : " / ");
				expr.right.accept(this, CiPriority.PRIMARY);
				if (!floorDiv || parent > CiPriority.OR)
					this.writeChar(41);
			}
			else
				super.visitBinaryExpr(expr, parent);
			break;
		case CiToken.COND_AND:
			this.writeBinaryExpr(expr, parent > CiPriority.COND_AND || parent == CiPriority.COND_OR, CiPriority.COND_AND, " and ", CiPriority.COND_AND);
			break;
		case CiToken.COND_OR:
			this.writeBinaryExpr2(expr, parent, CiPriority.COND_OR, " or ");
			break;
		case CiToken.ASSIGN:
			if (this.atLineStart) {
				let rightBinary;
				for (let right = expr.right; (rightBinary = right) instanceof CiBinaryExpr && rightBinary.isAssign(); right = rightBinary.right) {
					if (rightBinary.op != CiToken.ASSIGN) {
						this.visitBinaryExpr(rightBinary, CiPriority.STATEMENT);
						this.writeNewLine();
						break;
					}
				}
			}
			expr.left.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			{
				let rightBinary;
				((rightBinary = expr.right) instanceof CiBinaryExpr && rightBinary.isAssign() && rightBinary.op != CiToken.ASSIGN ? rightBinary.left : expr.right).accept(this, CiPriority.ASSIGN);
			}
			break;
		case CiToken.ADD_ASSIGN:
		case CiToken.SUB_ASSIGN:
		case CiToken.MUL_ASSIGN:
		case CiToken.DIV_ASSIGN:
		case CiToken.MOD_ASSIGN:
		case CiToken.SHIFT_LEFT_ASSIGN:
		case CiToken.SHIFT_RIGHT_ASSIGN:
		case CiToken.AND_ASSIGN:
		case CiToken.OR_ASSIGN:
		case CiToken.XOR_ASSIGN:
			{
				let right = expr.right;
				let rightBinary;
				if ((rightBinary = right) instanceof CiBinaryExpr && rightBinary.isAssign()) {
					this.visitBinaryExpr(rightBinary, CiPriority.STATEMENT);
					this.writeNewLine();
					right = rightBinary.left;
				}
				expr.left.accept(this, CiPriority.ASSIGN);
				this.writeChar(32);
				if (expr.op == CiToken.DIV_ASSIGN && expr.type instanceof CiIntegerType)
					this.writeChar(47);
				this.write(expr.getOpString());
				this.writeChar(32);
				right.accept(this, CiPriority.ARGUMENT);
			}
			break;
		case CiToken.IS:
			let symbol;
			if ((symbol = expr.right) instanceof CiSymbolReference) {
				this.write("isinstance(");
				expr.left.accept(this, CiPriority.ARGUMENT);
				this.write(", ");
				this.writeName(symbol.symbol);
				this.writeChar(41);
			}
			else
				this.notSupported(expr, "'is' with a variable");
			break;
		default:
			super.visitBinaryExpr(expr, parent);
			break;
		}
	}

	writeCoercedSelect(type, expr, parent)
	{
		if (parent > CiPriority.SELECT)
			this.writeChar(40);
		this.writeCoerced(type, expr.onTrue, CiPriority.SELECT);
		this.write(" if ");
		expr.cond.accept(this, CiPriority.SELECT_COND);
		this.write(" else ");
		this.writeCoerced(type, expr.onFalse, CiPriority.SELECT);
		if (parent > CiPriority.SELECT)
			this.writeChar(41);
	}

	#writeDefaultValue(type)
	{
		if (type instanceof CiNumericType)
			this.writeChar(48);
		else if (type.id == CiId.BOOL_TYPE)
			this.write("False");
		else if (type.id == CiId.STRING_STORAGE_TYPE)
			this.write("\"\"");
		else
			this.write("None");
	}

	#writePyNewArray(elementType, value, lengthExpr)
	{
		if (elementType instanceof CiStorageType) {
			this.write("[ ");
			this.writeNewStorage(elementType);
			this.write(" for _ in range(");
			lengthExpr.accept(this, CiPriority.ARGUMENT);
			this.write(") ]");
		}
		else if (elementType instanceof CiNumericType) {
			let c = GenPy.#getArrayCode(elementType);
			if (c == 66 && (value == null || value.isLiteralZero()))
				this.writeCall("bytearray", lengthExpr);
			else {
				this.include("array");
				this.write("array.array(\"");
				this.writeChar(c);
				this.write("\", [ ");
				if (value == null)
					this.writeChar(48);
				else
					value.accept(this, CiPriority.ARGUMENT);
				this.write(" ]) * ");
				lengthExpr.accept(this, CiPriority.MUL);
			}
		}
		else {
			this.write("[ ");
			if (value == null)
				this.#writeDefaultValue(elementType);
			else
				value.accept(this, CiPriority.ARGUMENT);
			this.write(" ] * ");
			lengthExpr.accept(this, CiPriority.MUL);
		}
	}

	writeNewArray(elementType, lengthExpr, parent)
	{
		this.#writePyNewArray(elementType, null, lengthExpr);
	}

	writeArrayStorageInit(array, value)
	{
		this.write(" = ");
		this.#writePyNewArray(array.getElementType(), null, array.lengthExpr);
	}

	writeNew(klass, parent)
	{
		switch (klass.class.id) {
		case CiId.LIST_CLASS:
		case CiId.STACK_CLASS:
			let number;
			if ((number = klass.getElementType()) instanceof CiNumericType) {
				let c = GenPy.#getArrayCode(number);
				if (c == 66)
					this.write("bytearray()");
				else {
					this.include("array");
					this.write("array.array(\"");
					this.writeChar(c);
					this.write("\")");
				}
			}
			else
				this.write("[]");
			break;
		case CiId.QUEUE_CLASS:
			this.include("collections");
			this.write("collections.deque()");
			break;
		case CiId.HASH_SET_CLASS:
		case CiId.SORTED_SET_CLASS:
			this.write("set()");
			break;
		case CiId.DICTIONARY_CLASS:
		case CiId.SORTED_DICTIONARY_CLASS:
			this.write("{}");
			break;
		case CiId.ORDERED_DICTIONARY_CLASS:
			this.include("collections");
			this.write("collections.OrderedDict()");
			break;
		case CiId.STRING_WRITER_CLASS:
			this.include("io");
			this.write("io.StringIO()");
			break;
		case CiId.LOCK_CLASS:
			this.include("threading");
			this.write("threading.RLock()");
			break;
		default:
			this.writeName(klass.class);
			this.write("()");
			break;
		}
	}

	#writeContains(haystack, needle)
	{
		needle.accept(this, CiPriority.REL);
		this.write(" in ");
		haystack.accept(this, CiPriority.REL);
	}

	#writeSlice(startIndex, length)
	{
		this.writeChar(91);
		startIndex.accept(this, CiPriority.ARGUMENT);
		this.writeChar(58);
		if (length != null)
			this.writeAdd(startIndex, length);
		this.writeChar(93);
	}

	#writeAssignSorted(obj, byteArray)
	{
		this.write(" = ");
		let c = GenPy.#getArrayCode(obj.type.asClassType().getElementType());
		if (c == 66) {
			this.write(byteArray);
			this.writeChar(40);
		}
		else {
			this.include("array");
			this.write("array.array(\"");
			this.writeChar(c);
			this.write("\", ");
		}
		this.write("sorted(");
	}

	#writeAllAny(function_, obj, args)
	{
		this.write(function_);
		this.writeChar(40);
		const lambda = args[0];
		lambda.body.accept(this, CiPriority.ARGUMENT);
		this.write(" for ");
		this.writeName(lambda.first);
		this.write(" in ");
		obj.accept(this, CiPriority.ARGUMENT);
		this.writeChar(41);
	}

	#writePyRegexOptions(args)
	{
		this.include("re");
		this.writeRegexOptions(args, ", ", " | ", "", "re.I", "re.M", "re.S");
	}

	#writeRegexSearch(args)
	{
		this.write("re.search(");
		args[1].accept(this, CiPriority.ARGUMENT);
		this.write(", ");
		args[0].accept(this, CiPriority.ARGUMENT);
		this.#writePyRegexOptions(args);
		this.writeChar(41);
	}

	writeCallExpr(obj, method, args, parent)
	{
		switch (method.id) {
		case CiId.ENUM_FROM_INT:
			this.writeName(method.type);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.ENUM_HAS_FLAG:
		case CiId.STRING_CONTAINS:
		case CiId.ARRAY_CONTAINS:
		case CiId.LIST_CONTAINS:
		case CiId.HASH_SET_CONTAINS:
		case CiId.SORTED_SET_CONTAINS:
		case CiId.DICTIONARY_CONTAINS_KEY:
		case CiId.SORTED_DICTIONARY_CONTAINS_KEY:
		case CiId.ORDERED_DICTIONARY_CONTAINS_KEY:
			this.#writeContains(obj, args[0]);
			break;
		case CiId.STRING_ENDS_WITH:
			this.writeMethodCall(obj, "endswith", args[0]);
			break;
		case CiId.STRING_INDEX_OF:
			this.writeMethodCall(obj, "find", args[0]);
			break;
		case CiId.STRING_LAST_INDEX_OF:
			this.writeMethodCall(obj, "rfind", args[0]);
			break;
		case CiId.STRING_STARTS_WITH:
			this.writeMethodCall(obj, "startswith", args[0]);
			break;
		case CiId.STRING_SUBSTRING:
			obj.accept(this, CiPriority.PRIMARY);
			this.#writeSlice(args[0], args.length == 2 ? args[1] : null);
			break;
		case CiId.ARRAY_BINARY_SEARCH_ALL:
			this.include("bisect");
			this.writeCall("bisect.bisect_left", obj, args[0]);
			break;
		case CiId.ARRAY_BINARY_SEARCH_PART:
			this.include("bisect");
			this.write("bisect.bisect_left(");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			args[1].accept(this, CiPriority.ARGUMENT);
			this.write(", ");
			args[2].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.ARRAY_COPY_TO:
		case CiId.LIST_COPY_TO:
			args[1].accept(this, CiPriority.PRIMARY);
			this.#writeSlice(args[2], args[3]);
			this.write(" = ");
			obj.accept(this, CiPriority.PRIMARY);
			this.#writeSlice(args[0], args[3]);
			break;
		case CiId.ARRAY_FILL_ALL:
		case CiId.ARRAY_FILL_PART:
			obj.accept(this, CiPriority.PRIMARY);
			if (args.length == 1) {
				this.write("[:] = ");
				const array = obj.type;
				this.#writePyNewArray(array.getElementType(), args[0], array.lengthExpr);
			}
			else {
				this.#writeSlice(args[1], args[2]);
				this.write(" = ");
				this.#writePyNewArray(obj.type.asClassType().getElementType(), args[0], args[2]);
			}
			break;
		case CiId.ARRAY_SORT_ALL:
		case CiId.LIST_SORT_ALL:
			obj.accept(this, CiPriority.ASSIGN);
			this.#writeAssignSorted(obj, "bytearray");
			obj.accept(this, CiPriority.ARGUMENT);
			this.write("))");
			break;
		case CiId.ARRAY_SORT_PART:
		case CiId.LIST_SORT_PART:
			obj.accept(this, CiPriority.PRIMARY);
			this.#writeSlice(args[0], args[1]);
			this.#writeAssignSorted(obj, "bytes");
			obj.accept(this, CiPriority.PRIMARY);
			this.#writeSlice(args[0], args[1]);
			this.write("))");
			break;
		case CiId.LIST_ADD:
			this.writeListAdd(obj, "append", args);
			break;
		case CiId.LIST_ADD_RANGE:
			obj.accept(this, CiPriority.ASSIGN);
			this.write(" += ");
			args[0].accept(this, CiPriority.ARGUMENT);
			break;
		case CiId.LIST_ALL:
			this.#writeAllAny("all", obj, args);
			break;
		case CiId.LIST_ANY:
			this.#writeAllAny("any", obj, args);
			break;
		case CiId.LIST_CLEAR:
		case CiId.STACK_CLEAR:
			let number;
			if ((number = obj.type.asClassType().getElementType()) instanceof CiNumericType && GenPy.#getArrayCode(number) != 66) {
				this.write("del ");
				this.writePostfix(obj, "[:]");
			}
			else
				this.writePostfix(obj, ".clear()");
			break;
		case CiId.LIST_INDEX_OF:
			if (parent > CiPriority.SELECT)
				this.writeChar(40);
			this.writeMethodCall(obj, "index", args[0]);
			this.write(" if ");
			this.#writeContains(obj, args[0]);
			this.write(" else -1");
			if (parent > CiPriority.SELECT)
				this.writeChar(41);
			break;
		case CiId.LIST_INSERT:
			this.writeListInsert(obj, "insert", args);
			break;
		case CiId.LIST_LAST:
		case CiId.STACK_PEEK:
			this.writePostfix(obj, "[-1]");
			break;
		case CiId.LIST_REMOVE_AT:
		case CiId.DICTIONARY_REMOVE:
		case CiId.SORTED_DICTIONARY_REMOVE:
		case CiId.ORDERED_DICTIONARY_REMOVE:
			this.write("del ");
			this.writeIndexing(obj, args[0]);
			break;
		case CiId.LIST_REMOVE_RANGE:
			this.write("del ");
			obj.accept(this, CiPriority.PRIMARY);
			this.#writeSlice(args[0], args[1]);
			break;
		case CiId.QUEUE_DEQUEUE:
			this.writePostfix(obj, ".popleft()");
			break;
		case CiId.QUEUE_ENQUEUE:
		case CiId.STACK_PUSH:
			this.writeListAppend(obj, args);
			break;
		case CiId.QUEUE_PEEK:
			this.writePostfix(obj, "[0]");
			break;
		case CiId.DICTIONARY_ADD:
			this.writeDictionaryAdd(obj, args);
			break;
		case CiId.TEXT_WRITER_WRITE:
			this.write("print(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", end=\"\", file=");
			obj.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.TEXT_WRITER_WRITE_CHAR:
		case CiId.TEXT_WRITER_WRITE_CODE_POINT:
			this.writeMethodCall(obj, "write(chr", args[0]);
			this.writeChar(41);
			break;
		case CiId.TEXT_WRITER_WRITE_LINE:
			this.write("print(");
			if (args.length == 1) {
				args[0].accept(this, CiPriority.ARGUMENT);
				this.write(", ");
			}
			this.write("file=");
			obj.accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.CONSOLE_WRITE:
			this.write("print(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.write(", end=\"\")");
			break;
		case CiId.CONSOLE_WRITE_LINE:
			this.write("print(");
			if (args.length == 1)
				args[0].accept(this, CiPriority.ARGUMENT);
			this.writeChar(41);
			break;
		case CiId.STRING_WRITER_CLEAR:
			this.writePostfix(obj, ".seek(0)");
			this.writeNewLine();
			this.writePostfix(obj, ".truncate(0)");
			break;
		case CiId.STRING_WRITER_TO_STRING:
			this.writePostfix(obj, ".getvalue()");
			break;
		case CiId.U_T_F8_GET_BYTE_COUNT:
			this.write("len(");
			this.writePostfix(args[0], ".encode(\"utf8\"))");
			break;
		case CiId.U_T_F8_GET_BYTES:
			this.write("cibytes = ");
			args[0].accept(this, CiPriority.PRIMARY);
			this.writeLine(".encode(\"utf8\")");
			args[1].accept(this, CiPriority.PRIMARY);
			this.writeChar(91);
			args[2].accept(this, CiPriority.ARGUMENT);
			this.writeChar(58);
			this.startAdd(args[2]);
			this.writeLine("len(cibytes)] = cibytes");
			break;
		case CiId.U_T_F8_GET_STRING:
			args[0].accept(this, CiPriority.PRIMARY);
			this.#writeSlice(args[1], args[2]);
			this.write(".decode(\"utf8\")");
			break;
		case CiId.ENVIRONMENT_GET_ENVIRONMENT_VARIABLE:
			this.include("os");
			this.writeCall("os.getenv", args[0]);
			break;
		case CiId.REGEX_COMPILE:
			this.write("re.compile(");
			args[0].accept(this, CiPriority.ARGUMENT);
			this.#writePyRegexOptions(args);
			this.writeChar(41);
			break;
		case CiId.REGEX_ESCAPE:
			this.include("re");
			this.writeCall("re.escape", args[0]);
			break;
		case CiId.REGEX_IS_MATCH_STR:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			this.#writeRegexSearch(args);
			this.write(" is not None");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.REGEX_IS_MATCH_REGEX:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			this.writeMethodCall(obj, "search", args[0]);
			this.write(" is not None");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.MATCH_FIND_STR:
		case CiId.MATCH_FIND_REGEX:
			if (parent > CiPriority.EQUALITY)
				this.writeChar(40);
			obj.accept(this, CiPriority.EQUALITY);
			this.write(" is not None");
			if (parent > CiPriority.EQUALITY)
				this.writeChar(41);
			break;
		case CiId.MATCH_GET_CAPTURE:
			this.writeMethodCall(obj, "group", args[0]);
			break;
		case CiId.MATH_METHOD:
		case CiId.MATH_IS_FINITE:
		case CiId.MATH_IS_NA_N:
		case CiId.MATH_LOG2:
			this.include("math");
			this.write("math.");
			this.writeLowercase(method.name);
			this.writeArgsInParentheses(method, args);
			break;
		case CiId.MATH_ABS:
			this.writeCall("abs", args[0]);
			break;
		case CiId.MATH_CEILING:
			this.include("math");
			this.writeCall("math.ceil", args[0]);
			break;
		case CiId.MATH_CLAMP:
			this.write("min(max(");
			this.writeClampAsMinMax(args);
			break;
		case CiId.MATH_FUSED_MULTIPLY_ADD:
			this.include("pyfma");
			this.writeCall("pyfma.fma", args[0], args[1], args[2]);
			break;
		case CiId.MATH_IS_INFINITY:
			this.include("math");
			this.writeCall("math.isinf", args[0]);
			break;
		case CiId.MATH_MAX_INT:
		case CiId.MATH_MAX_DOUBLE:
			this.writeCall("max", args[0], args[1]);
			break;
		case CiId.MATH_MIN_INT:
		case CiId.MATH_MIN_DOUBLE:
			this.writeCall("min", args[0], args[1]);
			break;
		case CiId.MATH_ROUND:
			this.writeCall("round", args[0]);
			break;
		case CiId.MATH_TRUNCATE:
			this.include("math");
			this.writeCall("math.trunc", args[0]);
			break;
		default:
			if (obj == null)
				this.writeLocalName(method, CiPriority.PRIMARY);
			else if (GenPy.isReferenceTo(obj, CiId.BASE_PTR)) {
				this.writeName(method.parent);
				this.writeChar(46);
				this.writeName(method);
				this.write("(self");
				if (args.length > 0) {
					this.write(", ");
					this.writeArgs(method, args);
				}
				this.writeChar(41);
				break;
			}
			else {
				obj.accept(this, CiPriority.PRIMARY);
				this.writeChar(46);
				this.writeName(method);
			}
			this.writeArgsInParentheses(method, args);
			break;
		}
	}

	writeResource(name, length)
	{
		this.write("_CiResource.");
		this.writeResourceName(name);
	}

	visitPreCall(call)
	{
		switch (call.method.symbol.id) {
		case CiId.MATCH_FIND_STR:
			call.method.left.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			this.#writeRegexSearch(call.arguments);
			this.writeNewLine();
			return true;
		case CiId.MATCH_FIND_REGEX:
			call.method.left.accept(this, CiPriority.ASSIGN);
			this.write(" = ");
			this.writeMethodCall(call.arguments[1], "search", call.arguments[0]);
			this.writeNewLine();
			return true;
		default:
			return false;
		}
	}

	startTemporaryVar(type)
	{
	}

	hasInitCode(def)
	{
		return (def.value != null || def.type.isFinal()) && !def.isAssignableStorage();
	}

	visitExpr(statement)
	{
		let def;
		if (!((def = statement) instanceof CiVar) || this.hasInitCode(def)) {
			this.writeTemporaries(statement);
			super.visitExpr(statement);
		}
	}

	startLine()
	{
		super.startLine();
		this.#childPass = false;
	}

	openChild()
	{
		this.writeCharLine(58);
		this.indent++;
		this.#childPass = true;
	}

	closeChild()
	{
		if (this.#childPass)
			this.writeLine("pass");
		this.indent--;
	}

	visitLambdaExpr(expr)
	{
		throw new Error();
	}

	writeAssertCast(expr)
	{
		const def = expr.right;
		this.write(def.name);
		this.write(" = ");
		expr.left.accept(this, CiPriority.ARGUMENT);
		this.writeNewLine();
	}

	writeAssert(statement)
	{
		this.write("assert ");
		statement.cond.accept(this, CiPriority.ARGUMENT);
		if (statement.message != null) {
			this.write(", ");
			statement.message.accept(this, CiPriority.ARGUMENT);
		}
		this.writeNewLine();
	}

	visitBreak(statement)
	{
		this.writeLine(statement.loopOrSwitch instanceof CiSwitch ? "raise _CiBreak()" : "break");
	}

	getIfNot()
	{
		return "if not ";
	}

	#writeInclusiveLimit(limit, increment, incrementString)
	{
		let literal;
		if ((literal = limit) instanceof CiLiteralLong)
			this.visitLiteralLong(literal.value + BigInt(increment));
		else {
			limit.accept(this, CiPriority.ADD);
			this.write(incrementString);
		}
	}

	writeForRange(iter, cond, rangeStep)
	{
		this.write("range(");
		if (rangeStep != 1 || !iter.value.isLiteralZero()) {
			iter.value.accept(this, CiPriority.ARGUMENT);
			this.write(", ");
		}
		switch (cond.op) {
		case CiToken.LESS:
		case CiToken.GREATER:
			cond.right.accept(this, CiPriority.ARGUMENT);
			break;
		case CiToken.LESS_OR_EQUAL:
			this.#writeInclusiveLimit(cond.right, 1, " + 1");
			break;
		case CiToken.GREATER_OR_EQUAL:
			this.#writeInclusiveLimit(cond.right, -1, " - 1");
			break;
		default:
			throw new Error();
		}
		if (rangeStep != 1) {
			this.write(", ");
			this.visitLiteralLong(rangeStep);
		}
		this.writeChar(41);
	}

	visitForeach(statement)
	{
		this.write("for ");
		this.writeName(statement.getVar());
		const klass = statement.collection.type;
		if (klass.class.typeParameterCount == 2) {
			this.write(", ");
			this.writeName(statement.getValueVar());
			this.write(" in ");
			if (klass.class.id == CiId.SORTED_DICTIONARY_CLASS) {
				this.write("sorted(");
				this.writePostfix(statement.collection, ".items())");
			}
			else
				this.writePostfix(statement.collection, ".items()");
		}
		else {
			this.write(" in ");
			if (klass.class.id == CiId.SORTED_SET_CLASS)
				this.writeCall("sorted", statement.collection);
			else
				statement.collection.accept(this, CiPriority.ARGUMENT);
		}
		this.writeChild(statement.body);
	}

	writeElseIf()
	{
		this.write("el");
	}

	visitLock(statement)
	{
		this.visitXcrement(statement.lock, false, true);
		this.write("with ");
		statement.lock.accept(this, CiPriority.ARGUMENT);
		this.openChild();
		this.visitXcrement(statement.lock, true, true);
		statement.body.acceptStatement(this);
		this.closeChild();
	}

	writeResultVar()
	{
		this.write("result");
	}

	#writeSwitchCaseVar(def)
	{
		this.writeName(def.type.asClassType().class);
		this.write("()");
		if (def.name != "_") {
			this.write(" as ");
			this.#writeNameNotKeyword(def.name);
		}
	}

	#writePyCaseBody(statement, body)
	{
		this.openChild();
		this.visitXcrement(statement.value, true, true);
		this.writeFirstStatements(body, CiSwitch.lengthWithoutTrailingBreak(body));
		this.closeChild();
	}

	visitSwitch(statement)
	{
		let earlyBreak = statement.cases.some(kase => CiSwitch.hasEarlyBreak(kase.body)) || CiSwitch.hasEarlyBreak(statement.defaultBody);
		if (earlyBreak) {
			this.#switchBreak = true;
			this.write("try");
			this.openChild();
		}
		this.visitXcrement(statement.value, false, true);
		this.write("match ");
		statement.value.accept(this, CiPriority.ARGUMENT);
		this.openChild();
		for (const kase of statement.cases) {
			let op = "case ";
			for (const caseValue of kase.values) {
				this.write(op);
				if (caseValue instanceof CiVar) {
					const def = caseValue;
					this.#writeSwitchCaseVar(def);
				}
				else if (caseValue instanceof CiBinaryExpr) {
					const when1 = caseValue;
					let whenVar;
					if ((whenVar = when1.left) instanceof CiVar)
						this.#writeSwitchCaseVar(whenVar);
					else
						when1.left.accept(this, CiPriority.ARGUMENT);
					this.write(" if ");
					when1.right.accept(this, CiPriority.ARGUMENT);
				}
				else
					caseValue.accept(this, CiPriority.OR);
				op = " | ";
			}
			this.#writePyCaseBody(statement, kase.body);
		}
		if (statement.hasDefault()) {
			this.write("case _");
			this.#writePyCaseBody(statement, statement.defaultBody);
		}
		this.closeChild();
		if (earlyBreak) {
			this.closeChild();
			this.write("except _CiBreak");
			this.openChild();
			this.closeChild();
		}
	}

	visitThrow(statement)
	{
		this.visitXcrement(statement.message, false, true);
		this.write("raise Exception(");
		statement.message.accept(this, CiPriority.ARGUMENT);
		this.writeCharLine(41);
	}

	visitEnumValue(konst, previous)
	{
		this.writeUppercaseWithUnderscores(konst.name);
		this.write(" = ");
		this.visitLiteralLong(BigInt(konst.value.intValue()));
		this.writeNewLine();
		this.writeDoc(konst.documentation);
	}

	writeEnum(enu)
	{
		this.include("enum");
		this.writeNewLine();
		this.write("class ");
		this.writeName(enu);
		this.write(enu instanceof CiEnumFlags ? "(enum.Flag)" : "(enum.Enum)");
		this.openChild();
		this.writeDoc(enu.documentation);
		enu.acceptValues(this);
		this.closeChild();
	}

	writeConst(konst)
	{
		if (konst.visibility != CiVisibility.PRIVATE || konst.type instanceof CiArrayStorageType) {
			this.writeNewLine();
			this.writeName(konst);
			this.write(" = ");
			konst.value.accept(this, CiPriority.ARGUMENT);
			this.writeNewLine();
			this.writeDoc(konst.documentation);
		}
	}

	writeField(field)
	{
	}

	writeMethod(method)
	{
		if (method.callType == CiCallType.ABSTRACT)
			return;
		this.writeNewLine();
		if (method.callType == CiCallType.STATIC)
			this.writeLine("@staticmethod");
		this.write("def ");
		this.writeName(method);
		if (method.callType == CiCallType.STATIC)
			this.writeParameters(method, true);
		else {
			this.write("(self");
			this.writeRemainingParameters(method, false, true);
		}
		this.currentMethod = method;
		this.openChild();
		this.#writePyDoc(method);
		method.body.acceptStatement(this);
		this.closeChild();
		this.currentMethod = null;
	}

	#inheritsConstructor(klass)
	{
		let baseClass;
		while ((baseClass = klass.parent) instanceof CiClass) {
			if (this.needsConstructor(baseClass))
				return true;
			klass = baseClass;
		}
		return false;
	}

	writeInitField(field)
	{
		if (this.hasInitCode(field)) {
			this.write("self.");
			this.writeVar(field);
			this.writeNewLine();
			this.writeInitCode(field);
		}
	}

	writeClass(klass, program)
	{
		if (!this.writeBaseClass(klass, program))
			return;
		this.writeNewLine();
		this.write("class ");
		this.writeName(klass);
		let baseClass;
		if ((baseClass = klass.parent) instanceof CiClass) {
			this.writeChar(40);
			this.writeName(baseClass);
			this.writeChar(41);
		}
		this.openChild();
		this.writeDoc(klass.documentation);
		if (this.needsConstructor(klass)) {
			this.writeNewLine();
			this.write("def __init__(self)");
			this.openChild();
			if (klass.constructor_ != null)
				this.writeDoc(klass.constructor_.documentation);
			if (this.#inheritsConstructor(klass)) {
				this.writeName(klass.parent);
				this.writeLine(".__init__(self)");
			}
			this.writeConstructorBody(klass);
			this.closeChild();
		}
		this.writeMembers(klass, true);
		this.closeChild();
	}

	#writeResourceByte(b)
	{
		this.write(`\\x${b.toString(16).padStart(2, "0")}`);
	}

	#writeResources(resources)
	{
		if (Object.keys(resources).length == 0)
			return;
		this.writeNewLine();
		this.write("class _CiResource");
		this.openChild();
		for (const [name, content] of Object.entries(resources).sort((a, b) => a[0].localeCompare(b[0]))) {
			this.writeResourceName(name);
			this.writeLine(" = (");
			this.indent++;
			this.write("b\"");
			let i = 0;
			for (const b of content) {
				if (i > 0 && (i & 15) == 0) {
					this.writeCharLine(34);
					this.write("b\"");
				}
				this.#writeResourceByte(b);
				i++;
			}
			this.writeLine("\" )");
			this.indent--;
		}
		this.closeChild();
	}

	writeProgram(program)
	{
		this.#switchBreak = false;
		this.openStringWriter();
		this.writeTypes(program);
		this.createOutputFile();
		this.writeTopLevelNatives(program);
		this.writeIncludes("import ", "");
		if (this.#switchBreak) {
			this.writeNewLine();
			this.writeLine("class _CiBreak(Exception): pass");
		}
		this.closeStringWriter();
		this.#writeResources(program.resources);
		this.closeFile();
	}
}

class StringWriter
{
	#buf = "";

	write(s)
	{
		this.#buf += s;
	}

	clear()
	{
		this.#buf = "";
	}

	toString()
	{
		return this.#buf;
	}
}
