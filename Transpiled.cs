// Generated automatically with "cito". Do not edit.
using System;
using System.Collections.Generic;
using System.Text;
namespace Foxoft.Ci
{

	public enum CiToken
	{
		EndOfFile,
		Id,
		LiteralLong,
		LiteralDouble,
		LiteralChar,
		LiteralString,
		InterpolatedString,
		Semicolon,
		Dot,
		Comma,
		LeftParenthesis,
		RightParenthesis,
		LeftBracket,
		RightBracket,
		LeftBrace,
		RightBrace,
		Plus,
		Minus,
		Asterisk,
		Slash,
		Mod,
		And,
		Or,
		Xor,
		Tilde,
		ShiftLeft,
		ShiftRight,
		Equal,
		NotEqual,
		Less,
		LessOrEqual,
		Greater,
		GreaterOrEqual,
		RightAngle,
		CondAnd,
		CondOr,
		ExclamationMark,
		Hash,
		Assign,
		AddAssign,
		SubAssign,
		MulAssign,
		DivAssign,
		ModAssign,
		AndAssign,
		OrAssign,
		XorAssign,
		ShiftLeftAssign,
		ShiftRightAssign,
		Increment,
		Decrement,
		QuestionMark,
		Colon,
		FatArrow,
		Range,
		DocComment,
		Abstract,
		Assert,
		Break,
		Case,
		Class,
		Const,
		Continue,
		Default,
		Do,
		Else,
		Enum,
		False,
		For,
		Foreach,
		If,
		In,
		Internal,
		Is,
		Lock_,
		Native,
		New,
		Null,
		Override,
		Protected,
		Public,
		Resource,
		Return,
		Sealed,
		Static,
		Switch,
		Throw,
		Throws,
		True,
		Virtual,
		Void,
		While,
		EndOfLine,
		PreIf,
		PreElIf,
		PreElse,
		PreEndIf
	}

	public enum CiDocToken
	{
		EndOfFile,
		Char,
		CodeDelimiter,
		Bullet,
		Para,
		Period
	}

	enum CiPreState
	{
		NotYet,
		Already,
		AlreadyElse
	}

	public class CiLexer
	{

		protected byte[] Input;

		int InputLength;

		internal bool HasErrors = false;

		int NextOffset;

		protected int CharOffset;

		int NextChar;

		protected string Filename;

		protected int Line;

		int LexemeOffset;

		protected CiToken CurrentToken;

		protected long LongValue;

		protected string StringValue;

		readonly HashSet<string> PreSymbols = new HashSet<string>();

		bool AtLineStart = true;

		bool LineMode = false;

		bool EnableDocComments = true;

		protected bool ParsingTypeArg = false;

		readonly Stack<bool> PreElseStack = new Stack<bool>();

		public void AddPreSymbol(string symbol)
		{
			this.PreSymbols.Add(symbol);
		}

		protected void Open(string filename, byte[] input, int inputLength)
		{
			this.Filename = filename;
			this.Input = input;
			this.InputLength = inputLength;
			this.NextOffset = 0;
			this.Line = 1;
			FillNextChar();
			if (this.NextChar == 65279)
				FillNextChar();
			NextToken();
		}

		protected void ReportError(string message)
		{
			Console.Error.WriteLine($"{this.Filename}({this.Line}): ERROR: {message}");
			this.HasErrors = true;
		}

		int ReadByte()
		{
			if (this.NextOffset >= this.InputLength)
				return -1;
			return this.Input[this.NextOffset++];
		}

		const int ReplacementChar = 65533;

		int ReadContinuationByte(int hi)
		{
			int b = ReadByte();
			if (hi != 65533) {
				if (b >= 128 && b <= 191)
					return (hi << 6) + b - 128;
				ReportError("Invalid UTF-8");
			}
			return 65533;
		}

		void FillNextChar()
		{
			this.CharOffset = this.NextOffset;
			int b = ReadByte();
			if (b >= 128) {
				if (b < 194 || b > 244) {
					ReportError("Invalid UTF-8");
					b = 65533;
				}
				else if (b < 224)
					b = ReadContinuationByte(b - 192);
				else if (b < 240) {
					b = ReadContinuationByte(b - 224);
					b = ReadContinuationByte(b);
				}
				else {
					b = ReadContinuationByte(b - 240);
					b = ReadContinuationByte(b);
					b = ReadContinuationByte(b);
				}
			}
			this.NextChar = b;
		}

		protected int PeekChar() => this.NextChar;

		public static bool IsLetterOrDigit(int c)
		{
			if (c >= 'a' && c <= 'z')
				return true;
			if (c >= 'A' && c <= 'Z')
				return true;
			if (c >= '0' && c <= '9')
				return true;
			return c == '_';
		}

		protected int ReadChar()
		{
			int c = this.NextChar;
			switch (c) {
			case '\t':
			case ' ':
				break;
			case '\n':
				this.Line++;
				this.AtLineStart = true;
				break;
			default:
				this.AtLineStart = false;
				break;
			}
			FillNextChar();
			return c;
		}

		bool EatChar(int c)
		{
			if (PeekChar() == c) {
				ReadChar();
				return true;
			}
			return false;
		}

		CiToken ReadIntegerLiteral(int bits)
		{
			bool invalidDigit = false;
			bool tooBig = false;
			bool needDigit = true;
			for (long i = 0;; ReadChar()) {
				int c = PeekChar();
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
					this.LongValue = i;
					if (invalidDigit || needDigit)
						ReportError("Invalid integer");
					else if (tooBig)
						ReportError("Integer too big");
					return CiToken.LiteralLong;
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

		CiToken ReadFloatLiteral(bool needDigit)
		{
			bool underscoreE = false;
			for (;;) {
				int c = PeekChar();
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
					ReadChar();
					needDigit = false;
					break;
				case 'E':
				case 'e':
					if (needDigit)
						underscoreE = true;
					ReadChar();
					c = PeekChar();
					if (c == '+' || c == '-')
						ReadChar();
					needDigit = true;
					break;
				case '_':
					ReadChar();
					needDigit = true;
					break;
				default:
					if (underscoreE || needDigit || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
						ReportError("Invalid floating-point number");
					return CiToken.LiteralDouble;
				}
			}
		}

		CiToken ReadNumberLiteral(long i)
		{
			bool leadingZero = false;
			bool tooBig = false;
			for (bool needDigit = false;; ReadChar()) {
				int c = PeekChar();
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
					return ReadFloatLiteral(needDigit);
				case '_':
					needDigit = true;
					continue;
				default:
					this.LongValue = i;
					if (leadingZero)
						ReportError("Leading zeros are not permitted, octal numbers must begin with 0o");
					if (needDigit || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
						ReportError("Invalid integer");
					else if (tooBig)
						ReportError("Integer too big");
					return CiToken.LiteralLong;
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

		public static int GetEscapedChar(int c)
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

		int ReadCharLiteral()
		{
			int c = ReadChar();
			if (c < 32) {
				ReportError("Invalid character in literal");
				return 65533;
			}
			if (c != '\\')
				return c;
			c = GetEscapedChar(ReadChar());
			if (c < 0) {
				ReportError("Unknown escape sequence");
				return 65533;
			}
			return c;
		}

		protected CiToken ReadString(bool interpolated)
		{
			for (int offset = this.CharOffset;;) {
				int c = PeekChar();
				if (c == '"') {
					int endOffset = this.CharOffset;
					ReadChar();
					this.StringValue = Encoding.UTF8.GetString(this.Input, offset, endOffset - offset);
					return CiToken.LiteralString;
				}
				if (interpolated && c == '{') {
					int endOffset = this.CharOffset;
					ReadChar();
					if (PeekChar() != '{') {
						this.StringValue = Encoding.UTF8.GetString(this.Input, offset, endOffset - offset);
						return CiToken.InterpolatedString;
					}
				}
				ReadCharLiteral();
			}
		}

		protected string GetLexeme() => Encoding.UTF8.GetString(this.Input, this.LexemeOffset, this.CharOffset - this.LexemeOffset);

		void ReadId(int c)
		{
			if (IsLetterOrDigit(c)) {
				while (IsLetterOrDigit(PeekChar()))
					ReadChar();
				this.StringValue = GetLexeme();
			}
			else {
				ReportError("Invalid character");
				this.StringValue = "";
			}
		}

		CiToken ReadPreToken()
		{
			for (;;) {
				bool atLineStart = this.AtLineStart;
				this.LexemeOffset = this.CharOffset;
				int c = ReadChar();
				switch (c) {
				case -1:
					return CiToken.EndOfFile;
				case '\t':
				case '\r':
				case ' ':
					break;
				case '\n':
					if (this.LineMode)
						return CiToken.EndOfLine;
					break;
				case '#':
					if (!atLineStart)
						return CiToken.Hash;
					this.LexemeOffset = this.CharOffset;
					ReadId(ReadChar());
					switch (this.StringValue) {
					case "if":
						return CiToken.PreIf;
					case "elif":
						return CiToken.PreElIf;
					case "else":
						return CiToken.PreElse;
					case "endif":
						return CiToken.PreEndIf;
					default:
						ReportError("Unknown preprocessor directive");
						continue;
					}
				case ';':
					return CiToken.Semicolon;
				case '.':
					if (EatChar('.'))
						return CiToken.Range;
					return CiToken.Dot;
				case ',':
					return CiToken.Comma;
				case '(':
					return CiToken.LeftParenthesis;
				case ')':
					return CiToken.RightParenthesis;
				case '[':
					return CiToken.LeftBracket;
				case ']':
					return CiToken.RightBracket;
				case '{':
					return CiToken.LeftBrace;
				case '}':
					return CiToken.RightBrace;
				case '~':
					return CiToken.Tilde;
				case '?':
					return CiToken.QuestionMark;
				case ':':
					return CiToken.Colon;
				case '+':
					if (EatChar('+'))
						return CiToken.Increment;
					if (EatChar('='))
						return CiToken.AddAssign;
					return CiToken.Plus;
				case '-':
					if (EatChar('-'))
						return CiToken.Decrement;
					if (EatChar('='))
						return CiToken.SubAssign;
					return CiToken.Minus;
				case '*':
					if (EatChar('='))
						return CiToken.MulAssign;
					return CiToken.Asterisk;
				case '/':
					if (EatChar('/')) {
						c = ReadChar();
						if (c == '/' && this.EnableDocComments) {
							while (EatChar(' ')) {
							}
							return CiToken.DocComment;
						}
						while (c != '\n' && c >= 0)
							c = ReadChar();
						if (c == '\n' && this.LineMode)
							return CiToken.EndOfLine;
						break;
					}
					if (EatChar('*')) {
						int startLine = this.Line;
						do {
							c = ReadChar();
							if (c < 0) {
								ReportError($"Unterminated multi-line comment, started in line {startLine}");
								return CiToken.EndOfFile;
							}
						}
						while (c != '*' || PeekChar() != '/');
						ReadChar();
						break;
					}
					if (EatChar('='))
						return CiToken.DivAssign;
					return CiToken.Slash;
				case '%':
					if (EatChar('='))
						return CiToken.ModAssign;
					return CiToken.Mod;
				case '&':
					if (EatChar('&'))
						return CiToken.CondAnd;
					if (EatChar('='))
						return CiToken.AndAssign;
					return CiToken.And;
				case '|':
					if (EatChar('|'))
						return CiToken.CondOr;
					if (EatChar('='))
						return CiToken.OrAssign;
					return CiToken.Or;
				case '^':
					if (EatChar('='))
						return CiToken.XorAssign;
					return CiToken.Xor;
				case '=':
					if (EatChar('='))
						return CiToken.Equal;
					if (EatChar('>'))
						return CiToken.FatArrow;
					return CiToken.Assign;
				case '!':
					if (EatChar('='))
						return CiToken.NotEqual;
					return CiToken.ExclamationMark;
				case '<':
					if (EatChar('<')) {
						if (EatChar('='))
							return CiToken.ShiftLeftAssign;
						return CiToken.ShiftLeft;
					}
					if (EatChar('='))
						return CiToken.LessOrEqual;
					return CiToken.Less;
				case '>':
					if (this.ParsingTypeArg)
						return CiToken.RightAngle;
					if (EatChar('>')) {
						if (EatChar('='))
							return CiToken.ShiftRightAssign;
						return CiToken.ShiftRight;
					}
					if (EatChar('='))
						return CiToken.GreaterOrEqual;
					return CiToken.Greater;
				case '\'':
					if (PeekChar() == '\'')
						ReportError("Empty character literal");
					else
						this.LongValue = ReadCharLiteral();
					if (!EatChar('\''))
						ReportError("Unterminated character literal");
					return CiToken.LiteralChar;
				case '"':
					return ReadString(false);
				case '$':
					if (EatChar('"'))
						return ReadString(true);
					ReportError("Expected interpolated string");
					break;
				case '0':
					switch (PeekChar()) {
					case 'B':
					case 'b':
						ReadChar();
						return ReadIntegerLiteral(1);
					case 'O':
					case 'o':
						ReadChar();
						return ReadIntegerLiteral(3);
					case 'X':
					case 'x':
						ReadChar();
						return ReadIntegerLiteral(4);
					default:
						return ReadNumberLiteral(0);
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
					return ReadNumberLiteral(c - '0');
				default:
					ReadId(c);
					switch (this.StringValue) {
					case "":
						continue;
					case "abstract":
						return CiToken.Abstract;
					case "assert":
						return CiToken.Assert;
					case "break":
						return CiToken.Break;
					case "case":
						return CiToken.Case;
					case "class":
						return CiToken.Class;
					case "const":
						return CiToken.Const;
					case "continue":
						return CiToken.Continue;
					case "default":
						return CiToken.Default;
					case "do":
						return CiToken.Do;
					case "else":
						return CiToken.Else;
					case "enum":
						return CiToken.Enum;
					case "false":
						return CiToken.False;
					case "for":
						return CiToken.For;
					case "foreach":
						return CiToken.Foreach;
					case "if":
						return CiToken.If;
					case "in":
						return CiToken.In;
					case "internal":
						return CiToken.Internal;
					case "is":
						return CiToken.Is;
					case "lock":
						return CiToken.Lock_;
					case "native":
						return CiToken.Native;
					case "new":
						return CiToken.New;
					case "null":
						return CiToken.Null;
					case "override":
						return CiToken.Override;
					case "protected":
						return CiToken.Protected;
					case "public":
						return CiToken.Public;
					case "resource":
						return CiToken.Resource;
					case "return":
						return CiToken.Return;
					case "sealed":
						return CiToken.Sealed;
					case "static":
						return CiToken.Static;
					case "switch":
						return CiToken.Switch;
					case "throw":
						return CiToken.Throw;
					case "throws":
						return CiToken.Throws;
					case "true":
						return CiToken.True;
					case "virtual":
						return CiToken.Virtual;
					case "void":
						return CiToken.Void;
					case "while":
						return CiToken.While;
					default:
						return CiToken.Id;
					}
				}
			}
		}

		void NextPreToken()
		{
			this.CurrentToken = ReadPreToken();
		}

		protected bool See(CiToken token) => this.CurrentToken == token;

		static string TokenToString(CiToken token)
		{
			switch (token) {
			case CiToken.EndOfFile:
				return "end-of-file";
			case CiToken.Id:
				return "identifier";
			case CiToken.LiteralLong:
				return "integer constant";
			case CiToken.LiteralDouble:
				return "floating-point constant";
			case CiToken.LiteralChar:
				return "character constant";
			case CiToken.LiteralString:
				return "string constant";
			case CiToken.InterpolatedString:
				return "interpolated string";
			case CiToken.Semicolon:
				return "';'";
			case CiToken.Dot:
				return "'.'";
			case CiToken.Comma:
				return "','";
			case CiToken.LeftParenthesis:
				return "'('";
			case CiToken.RightParenthesis:
				return "')'";
			case CiToken.LeftBracket:
				return "'['";
			case CiToken.RightBracket:
				return "']'";
			case CiToken.LeftBrace:
				return "'{'";
			case CiToken.RightBrace:
				return "'}'";
			case CiToken.Plus:
				return "'+'";
			case CiToken.Minus:
				return "'-'";
			case CiToken.Asterisk:
				return "'*'";
			case CiToken.Slash:
				return "'/'";
			case CiToken.Mod:
				return "'%'";
			case CiToken.And:
				return "'&'";
			case CiToken.Or:
				return "'|'";
			case CiToken.Xor:
				return "'^'";
			case CiToken.Tilde:
				return "'~'";
			case CiToken.ShiftLeft:
				return "'<<'";
			case CiToken.ShiftRight:
				return "'>>'";
			case CiToken.Equal:
				return "'=='";
			case CiToken.NotEqual:
				return "'!='";
			case CiToken.Less:
				return "'<'";
			case CiToken.LessOrEqual:
				return "'<='";
			case CiToken.Greater:
				return "'>'";
			case CiToken.GreaterOrEqual:
				return "'>='";
			case CiToken.RightAngle:
				return "'>'";
			case CiToken.CondAnd:
				return "'&&'";
			case CiToken.CondOr:
				return "'||'";
			case CiToken.ExclamationMark:
				return "'!'";
			case CiToken.Hash:
				return "'#'";
			case CiToken.Assign:
				return "'='";
			case CiToken.AddAssign:
				return "'+='";
			case CiToken.SubAssign:
				return "'-='";
			case CiToken.MulAssign:
				return "'*='";
			case CiToken.DivAssign:
				return "'/='";
			case CiToken.ModAssign:
				return "'%='";
			case CiToken.AndAssign:
				return "'&='";
			case CiToken.OrAssign:
				return "'|='";
			case CiToken.XorAssign:
				return "'^='";
			case CiToken.ShiftLeftAssign:
				return "'<<='";
			case CiToken.ShiftRightAssign:
				return "'>>='";
			case CiToken.Increment:
				return "'++'";
			case CiToken.Decrement:
				return "'--'";
			case CiToken.QuestionMark:
				return "'?'";
			case CiToken.Colon:
				return "':'";
			case CiToken.FatArrow:
				return "'=>'";
			case CiToken.Range:
				return "'..'";
			case CiToken.DocComment:
				return "'///'";
			case CiToken.Abstract:
				return "'abstract'";
			case CiToken.Assert:
				return "'assert'";
			case CiToken.Break:
				return "'break'";
			case CiToken.Case:
				return "'case'";
			case CiToken.Class:
				return "'class'";
			case CiToken.Const:
				return "'const'";
			case CiToken.Continue:
				return "'continue'";
			case CiToken.Default:
				return "'default'";
			case CiToken.Do:
				return "'do'";
			case CiToken.Else:
				return "'else'";
			case CiToken.Enum:
				return "'enum'";
			case CiToken.False:
				return "'false'";
			case CiToken.For:
				return "'for'";
			case CiToken.Foreach:
				return "'foreach'";
			case CiToken.If:
				return "'if'";
			case CiToken.In:
				return "'in'";
			case CiToken.Internal:
				return "'internal'";
			case CiToken.Is:
				return "'is'";
			case CiToken.Lock_:
				return "'lock'";
			case CiToken.Native:
				return "'native'";
			case CiToken.New:
				return "'new'";
			case CiToken.Null:
				return "'null'";
			case CiToken.Override:
				return "'override'";
			case CiToken.Protected:
				return "'protected'";
			case CiToken.Public:
				return "'public'";
			case CiToken.Resource:
				return "'resource'";
			case CiToken.Return:
				return "'return'";
			case CiToken.Sealed:
				return "'sealed'";
			case CiToken.Static:
				return "'static'";
			case CiToken.Switch:
				return "'switch'";
			case CiToken.Throw:
				return "'throw'";
			case CiToken.Throws:
				return "'throws'";
			case CiToken.True:
				return "'true'";
			case CiToken.Virtual:
				return "'virtual'";
			case CiToken.Void:
				return "'void'";
			case CiToken.While:
				return "'while'";
			case CiToken.EndOfLine:
				return "end-of-line";
			case CiToken.PreIf:
				return "'#if'";
			case CiToken.PreElIf:
				return "'#elif'";
			case CiToken.PreElse:
				return "'#else'";
			case CiToken.PreEndIf:
				return "'#endif'";
			default:
				throw new NotImplementedException();
			}
		}

		protected bool Check(CiToken expected)
		{
			if (See(expected))
				return true;
			ReportError($"Expected {TokenToString(expected)}, got {TokenToString(this.CurrentToken)}");
			return false;
		}

		bool EatPre(CiToken token)
		{
			if (See(token)) {
				NextPreToken();
				return true;
			}
			return false;
		}

		bool ParsePrePrimary()
		{
			if (EatPre(CiToken.ExclamationMark))
				return !ParsePrePrimary();
			if (EatPre(CiToken.LeftParenthesis)) {
				bool result = ParsePreOr();
				Check(CiToken.RightParenthesis);
				NextPreToken();
				return result;
			}
			if (See(CiToken.Id)) {
				bool result = this.PreSymbols.Contains(this.StringValue);
				NextPreToken();
				return result;
			}
			if (EatPre(CiToken.False))
				return false;
			if (EatPre(CiToken.True))
				return true;
			ReportError("Invalid preprocessor expression");
			return false;
		}

		bool ParsePreEquality()
		{
			bool result = ParsePrePrimary();
			for (;;) {
				if (EatPre(CiToken.Equal))
					result = result == ParsePrePrimary();
				else if (EatPre(CiToken.NotEqual))
					result ^= ParsePrePrimary();
				else
					return result;
			}
		}

		bool ParsePreAnd()
		{
			bool result = ParsePreEquality();
			while (EatPre(CiToken.CondAnd))
				result &= ParsePreEquality();
			return result;
		}

		bool ParsePreOr()
		{
			bool result = ParsePreAnd();
			while (EatPre(CiToken.CondOr))
				result |= ParsePreAnd();
			return result;
		}

		bool ParsePreExpr()
		{
			this.LineMode = true;
			NextPreToken();
			bool result = ParsePreOr();
			Check(CiToken.EndOfLine);
			this.LineMode = false;
			return result;
		}

		void ExpectEndOfLine(string directive)
		{
			this.LineMode = true;
			CiToken token = ReadPreToken();
			if (token != CiToken.EndOfLine && token != CiToken.EndOfFile)
				ReportError($"Unexpected characters after '{directive}'");
			this.LineMode = false;
		}

		bool PopPreElse(string directive)
		{
			if (this.PreElseStack.Count == 0) {
				ReportError($"'{directive}' with no matching '#if'");
				return false;
			}
			if (this.PreElseStack.Pop() && directive != "#endif")
				ReportError($"'{directive}' after '#else'");
			return true;
		}

		void SkipUnmet(CiPreState state)
		{
			this.EnableDocComments = false;
			for (;;) {
				switch (ReadPreToken()) {
				case CiToken.EndOfFile:
					ReportError("Expected '#endif', got end-of-file");
					return;
				case CiToken.PreIf:
					ParsePreExpr();
					SkipUnmet(CiPreState.Already);
					break;
				case CiToken.PreElIf:
					if (state == CiPreState.AlreadyElse)
						ReportError("'#elif' after '#else'");
					if (ParsePreExpr() && state == CiPreState.NotYet) {
						this.PreElseStack.Push(false);
						return;
					}
					break;
				case CiToken.PreElse:
					if (state == CiPreState.AlreadyElse)
						ReportError("'#else' after '#else'");
					ExpectEndOfLine("#else");
					if (state == CiPreState.NotYet) {
						this.PreElseStack.Push(true);
						return;
					}
					state = CiPreState.AlreadyElse;
					break;
				case CiToken.PreEndIf:
					ExpectEndOfLine("#endif");
					return;
				default:
					break;
				}
			}
		}

		CiToken ReadToken()
		{
			for (;;) {
				this.EnableDocComments = true;
				CiToken token = ReadPreToken();
				bool matched;
				switch (token) {
				case CiToken.EndOfFile:
					if (this.PreElseStack.Count != 0)
						ReportError("Expected '#endif', got end-of-file");
					return CiToken.EndOfFile;
				case CiToken.PreIf:
					if (ParsePreExpr())
						this.PreElseStack.Push(false);
					else
						SkipUnmet(CiPreState.NotYet);
					break;
				case CiToken.PreElIf:
					matched = PopPreElse("#elif");
					ParsePreExpr();
					if (matched)
						SkipUnmet(CiPreState.Already);
					break;
				case CiToken.PreElse:
					matched = PopPreElse("#else");
					ExpectEndOfLine("#else");
					if (matched)
						SkipUnmet(CiPreState.AlreadyElse);
					break;
				case CiToken.PreEndIf:
					PopPreElse("#endif");
					ExpectEndOfLine("#endif");
					break;
				default:
					return token;
				}
			}
		}

		protected CiToken NextToken()
		{
			CiToken token = this.CurrentToken;
			this.CurrentToken = ReadToken();
			return token;
		}

		protected bool Eat(CiToken token)
		{
			if (See(token)) {
				NextToken();
				return true;
			}
			return false;
		}

		protected void Expect(CiToken expected)
		{
			Check(expected);
			NextToken();
		}

		protected void ExpectOrSkip(CiToken expected)
		{
			if (Check(expected))
				NextToken();
			else {
				do
					NextToken();
				while (!See(CiToken.EndOfFile) && !Eat(expected));
			}
		}

		bool DocCheckPeriod;

		protected int DocCurrentChar;

		CiDocToken DocCurrentToken;

		CiDocToken DocReadToken()
		{
			for (int lastChar = this.DocCurrentChar;;) {
				int c = ReadChar();
				if (c == '\n') {
					NextToken();
					if (!See(CiToken.DocComment))
						return CiDocToken.EndOfFile;
				}
				this.DocCurrentChar = c;
				switch (c) {
				case -1:
					return CiDocToken.EndOfFile;
				case '`':
					return CiDocToken.CodeDelimiter;
				case '*':
					if (lastChar == '\n' && PeekChar() == ' ') {
						ReadChar();
						return CiDocToken.Bullet;
					}
					return CiDocToken.Char;
				case '\r':
					continue;
				case '\n':
					if (this.DocCheckPeriod && lastChar == '.') {
						this.DocCheckPeriod = false;
						return CiDocToken.Period;
					}
					if (lastChar == '\n')
						return CiDocToken.Para;
					return CiDocToken.Char;
				default:
					return CiDocToken.Char;
				}
			}
		}

		protected void DocNextToken()
		{
			this.DocCurrentToken = DocReadToken();
		}

		protected void DocStartLexing()
		{
			this.DocCheckPeriod = true;
			this.DocCurrentChar = '\n';
			DocNextToken();
		}

		protected bool DocSee(CiDocToken token) => this.DocCurrentToken == token;

		protected bool DocEat(CiDocToken token)
		{
			if (DocSee(token)) {
				DocNextToken();
				return true;
			}
			return false;
		}
	}

	public enum CiVisibility
	{
		Private,
		Internal,
		Protected,
		Public,
		NumericElementType,
		FinalValueType
	}

	public enum CiCallType
	{
		Static,
		Normal,
		Abstract,
		Virtual,
		Override,
		Sealed
	}

	public enum CiPriority
	{
		Statement,
		Argument,
		Assign,
		Select,
		CondOr,
		CondAnd,
		Or,
		Xor,
		And,
		Equality,
		Rel,
		Shift,
		Add,
		Mul,
		Primary
	}

	public enum CiId
	{
		None,
		ArrayPtrClass,
		ArrayStorageClass,
		ListClass,
		QueueClass,
		StackClass,
		HashSetClass,
		DictionaryClass,
		SortedDictionaryClass,
		OrderedDictionaryClass,
		RegexClass,
		MatchClass,
		LockClass,
		StringLength,
		ArrayLength,
		ConsoleError,
		MatchStart,
		MatchEnd,
		MatchLength,
		MatchValue,
		MathNaN,
		MathNegativeInfinity,
		MathPositiveInfinity,
		StringContains,
		StringEndsWith,
		StringIndexOf,
		StringLastIndexOf,
		StringStartsWith,
		StringSubstring,
		ArrayBinarySearchAll,
		ArrayBinarySearchPart,
		ArrayCopyTo,
		ArrayFillAll,
		ArrayFillPart,
		ArraySortAll,
		ArraySortPart,
		ListAdd,
		ListAny,
		ListClear,
		ListContains,
		ListCopyTo,
		ListCount,
		ListInsert,
		ListRemoveAt,
		ListRemoveRange,
		ListSortAll,
		ListSortPart,
		QueueClear,
		QueueCount,
		QueueDequeue,
		QueueEnqueue,
		QueuePeek,
		StackClear,
		StackCount,
		StackPeek,
		StackPush,
		StackPop,
		HashSetAdd,
		HashSetClear,
		HashSetContains,
		HashSetCount,
		HashSetRemove,
		DictionaryAdd,
		DictionaryClear,
		DictionaryContainsKey,
		DictionaryCount,
		DictionaryRemove,
		SortedDictionaryClear,
		SortedDictionaryContainsKey,
		SortedDictionaryCount,
		SortedDictionaryRemove,
		OrderedDictionaryClear,
		OrderedDictionaryContainsKey,
		OrderedDictionaryCount,
		OrderedDictionaryRemove,
		ConsoleWrite,
		ConsoleWriteLine,
		UTF8GetByteCount,
		UTF8GetBytes,
		UTF8GetString,
		EnvironmentGetEnvironmentVariable,
		RegexCompile,
		RegexEscape,
		RegexIsMatchStr,
		RegexIsMatchRegex,
		MatchFindStr,
		MatchFindRegex,
		MatchGetCapture,
		MathMethod,
		MathCeiling,
		MathFusedMultiplyAdd,
		MathIsFinite,
		MathIsInfinity,
		MathIsNaN,
		MathLog2,
		MathTruncate
	}

	abstract class CiDocInline
	{

		internal string Text;
	}

	class CiDocText : CiDocInline
	{
	}

	class CiDocCode : CiDocInline
	{
	}

	public abstract class CiDocBlock
	{
	}

	public class CiDocPara : CiDocBlock
	{

		internal readonly List<CiDocInline> Children = new List<CiDocInline>();
	}

	public class CiDocList : CiDocBlock
	{

		internal readonly List<CiDocPara> Items = new List<CiDocPara>();
	}

	public class CiCodeDoc
	{

		internal readonly CiDocPara Summary = new CiDocPara();

		internal readonly List<CiDocBlock> Details = new List<CiDocBlock>();
	}

	public abstract class CiStatement
	{

		internal int Line;

		public abstract bool CompletesNormally();

		public abstract void AcceptStatement(CiVisitor visitor);
	}

	public abstract class CiUnaryExpr : CiExpr
	{

		internal CiToken Op;

		internal CiExpr Inner;
	}

	public abstract class CiCondCompletionStatement : CiScope
	{

		bool CompletesNormallyValue;

		public override bool CompletesNormally() => this.CompletesNormallyValue;

		public void SetCompletesNormally(bool value)
		{
			this.CompletesNormallyValue = value;
		}
	}

	public abstract class CiLoop : CiCondCompletionStatement
	{

		internal CiExpr Cond;

		internal CiStatement Body;

		internal bool HasBreak = false;
	}

	public class CiCase
	{

		internal readonly List<CiExpr> Values = new List<CiExpr>();

		internal readonly List<CiStatement> Body = new List<CiStatement>();
	}

	public class CiParameters : CiScope
	{
	}

	public class CiType : CiScope
	{

		public virtual string GetArraySuffix() => "";

		public virtual bool IsAssignableFrom(CiType right) => this == right;

		public virtual bool EqualsType(CiType right) => this == right;

		public virtual bool IsArray() => false;

		public virtual bool IsFinal() => false;

		public virtual bool IsNullable() => false;

		public virtual CiType GetBaseType() => this;

		public virtual CiType GetStorageType() => this;

		public virtual CiType GetPtrOrSelf() => this;
	}

	public abstract class CiNumericType : CiType
	{
	}

	public abstract class CiContainerType : CiType
	{

		internal bool IsPublic;

		internal string Filename;
	}

	public enum CiVisitStatus
	{
		NotYet,
		InProgress,
		Done
	}
}
