// Generated automatically with "cito". Do not edit.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
		DocRegular,
		DocBullet,
		DocBlank,
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

		protected int LexemeOffset;

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

		void SkipWhitespace()
		{
			while (PeekChar() == '\t' || PeekChar() == ' ' || PeekChar() == '\r')
				ReadChar();
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
							SkipWhitespace();
							switch (PeekChar()) {
							case '\n':
								return CiToken.DocBlank;
							case '*':
								ReadChar();
								SkipWhitespace();
								return CiToken.DocBullet;
							default:
								return CiToken.DocRegular;
							}
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

		public static string TokenToString(CiToken token)
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
			case CiToken.DocRegular:
			case CiToken.DocBullet:
			case CiToken.DocBlank:
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

		protected bool Expect(CiToken expected)
		{
			bool found = Check(expected);
			NextToken();
			return found;
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
		VoidType,
		NullType,
		BasePtr,
		TypeParam0,
		TypeParam0NotFinal,
		TypeParam0Predicate,
		IntType,
		LongType,
		FloatType,
		DoubleType,
		FloatIntType,
		BoolType,
		StringClass,
		StringPtrType,
		StringStorageType,
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
		ClassToString,
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
		StringReplace,
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
		ListAll,
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
	}

	class CiDocText : CiDocInline
	{

		internal string Text;
	}

	class CiDocCode : CiDocInline
	{

		internal string Text;
	}

	class CiDocLine : CiDocInline
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

	public abstract class CiVisitor
	{

		internal bool HasErrors = false;

		protected abstract CiContainerType GetCurrentContainer();

		protected void ReportError(CiStatement statement, string message)
		{
			Console.Error.WriteLine($"{GetCurrentContainer().Filename}({statement.Line}): ERROR: {message}");
			this.HasErrors = true;
		}

		public abstract void VisitConst(CiConst statement);

		public abstract void VisitExpr(CiExpr statement);

		public abstract void VisitBlock(CiBlock statement);

		public abstract void VisitAssert(CiAssert statement);

		public abstract void VisitBreak(CiBreak statement);

		public abstract void VisitContinue(CiContinue statement);

		public abstract void VisitDoWhile(CiDoWhile statement);

		public abstract void VisitFor(CiFor statement);

		public abstract void VisitForeach(CiForeach statement);

		public abstract void VisitIf(CiIf statement);

		public abstract void VisitLock(CiLock statement);

		public abstract void VisitNative(CiNative statement);

		public abstract void VisitReturn(CiReturn statement);

		public abstract void VisitSwitch(CiSwitch statement);

		public abstract void VisitThrow(CiThrow statement);

		public abstract void VisitWhile(CiWhile statement);

		public abstract void VisitEnumValue(CiConst konst, CiConst previous);
	}

	public abstract class CiExprVisitor : CiVisitor
	{

		public abstract void VisitLiteralLong(long value);

		public abstract void VisitLiteralChar(int value);

		public abstract void VisitLiteralDouble(double value);

		public abstract void VisitLiteralString(string value);

		public abstract void VisitLiteralNull();

		public abstract void VisitLiteralFalse();

		public abstract void VisitLiteralTrue();

		public abstract void VisitAggregateInitializer(CiAggregateInitializer expr);

		public abstract void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent);

		public abstract void VisitSymbolReference(CiSymbolReference expr, CiPriority parent);

		public abstract void VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent);

		public abstract void VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent);

		public abstract void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent);

		public abstract void VisitSelectExpr(CiSelectExpr expr, CiPriority parent);

		public abstract void VisitCallExpr(CiCallExpr expr, CiPriority parent);

		public abstract void VisitLambdaExpr(CiLambdaExpr expr);

		public abstract void VisitVar(CiVar expr);
	}

	public abstract class CiStatement
	{

		internal int Line;

		public abstract bool CompletesNormally();

		public abstract void AcceptStatement(CiVisitor visitor);
	}

	public abstract class CiExpr : CiStatement
	{

		internal CiType Type;

		public override bool CompletesNormally() => true;

		public override string ToString()
		{
			throw new NotImplementedException();
		}

		public virtual bool IsIndexing() => false;

		public virtual bool IsLiteralZero() => false;

		public virtual bool IsConstEnum() => false;

		public virtual int IntValue()
		{
			throw new NotImplementedException();
		}

		public virtual void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			throw new NotImplementedException();
		}

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitExpr(this);
		}

		public virtual bool IsReferenceTo(CiSymbol symbol) => false;
	}

	public abstract class CiSymbol : CiExpr
	{

		internal CiId Id = CiId.None;

		internal string Name;

		internal CiSymbol Next;

		internal CiScope Parent;

		internal CiCodeDoc Documentation = null;

		public override string ToString() => this.Name;
	}

	public class CiScope : CiSymbol
	{

		protected readonly Dictionary<string, CiSymbol> Dict = new Dictionary<string, CiSymbol>();

		internal CiSymbol First = null;

		CiSymbol Last;

		public int Count() => this.Dict.Count;

		public CiVar FirstParameter()
		{
			CiVar result = (CiVar) this.First;
			return result;
		}

		public CiContainerType GetContainer()
		{
			for (CiScope scope = this; scope != null; scope = scope.Parent) {
				if (scope is CiContainerType container)
					return container;
			}
			throw new NotImplementedException();
		}

		public bool Contains(CiSymbol symbol) => this.Dict.ContainsKey(symbol.Name);

		public virtual CiSymbol TryLookup(string name)
		{
			for (CiScope scope = this; scope != null; scope = scope.Parent) {
				if (scope.Dict.ContainsKey(name))
					return scope.Dict[name];
			}
			return null;
		}

		public void Add(CiSymbol symbol)
		{
			this.Dict[symbol.Name] = symbol;
			symbol.Next = null;
			symbol.Parent = this;
			if (this.First == null)
				this.First = symbol;
			else
				this.Last.Next = symbol;
			this.Last = symbol;
		}

		public bool Encloses(CiSymbol symbol)
		{
			for (CiScope scope = symbol.Parent; scope != null; scope = scope.Parent) {
				if (scope == this)
					return true;
			}
			return false;
		}
	}

	public class CiAggregateInitializer : CiExpr
	{

		internal readonly List<CiExpr> Items = new List<CiExpr>();

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitAggregateInitializer(this);
		}
	}

	public abstract class CiLiteral : CiExpr
	{

		public abstract bool IsDefaultValue();

		public virtual string GetLiteralString()
		{
			throw new NotImplementedException();
		}
	}

	public class CiLiteralFalse : CiLiteral
	{

		public override bool IsDefaultValue() => true;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralFalse();
		}

		public override string ToString() => "false";
	}

	public class CiLiteralTrue : CiLiteral
	{

		public override bool IsDefaultValue() => false;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralTrue();
		}

		public override string ToString() => "true";
	}

	public class CiLiteralNull : CiLiteral
	{

		public override bool IsDefaultValue() => true;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralNull();
		}

		public override string ToString() => "null";
	}

	public class CiLiteralLong : CiLiteral
	{

		internal long Value;

		public override bool IsLiteralZero() => this.Value == 0;

		public override int IntValue() => (int) this.Value;

		public override bool IsDefaultValue() => this.Value == 0;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralLong(this.Value);
		}

		public override string GetLiteralString() => $"{this.Value}";

		public override string ToString() => $"{this.Value}";
	}

	public class CiLiteralChar : CiLiteralLong
	{

		public static CiLiteralChar New(int value, int line) => new CiLiteralChar { Line = line, Type = CiRangeType.New(value, value), Value = value };

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralChar((int) this.Value);
		}
	}

	public class CiLiteralDouble : CiLiteral
	{

		internal double Value;

		public override bool IsDefaultValue() => this.Value == 0 && 1.0f / this.Value > 0;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralDouble(this.Value);
		}

		public override string GetLiteralString() => $"{this.Value}";

		public override string ToString() => $"{this.Value}";
	}

	public class CiLiteralString : CiLiteral
	{

		internal string Value;

		public override bool IsDefaultValue() => false;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralString(this.Value);
		}

		public override string GetLiteralString() => this.Value;

		public override string ToString() => $"\"{this.Value}\"";

		public int GetAsciiLength()
		{
			int length = 0;
			bool escaped = false;
			foreach (int c in this.Value) {
				if (c > 127)
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

		public int GetAsciiAt(int i)
		{
			bool escaped = false;
			foreach (int c in this.Value) {
				if (c > 127)
					return -1;
				if (!escaped && c == '\\')
					escaped = true;
				else if (i == 0)
					return escaped ? CiLexer.GetEscapedChar(c) : c;
				else {
					i--;
					escaped = false;
				}
			}
			return -1;
		}

		public int GetOneAscii()
		{
			switch (this.Value.Length) {
			case 1:
				int c = this.Value[0];
				return c > 127 ? -1 : c;
			case 2:
				return this.Value[0] != '\\' ? -1 : CiLexer.GetEscapedChar(this.Value[1]);
			default:
				return -1;
			}
		}
	}

	public class CiInterpolatedPart
	{

		internal string Prefix;

		internal CiExpr Argument;

		internal CiExpr WidthExpr;

		internal int Width;

		internal int Format;

		internal int Precision;
	}

	public class CiInterpolatedString : CiExpr
	{

		internal readonly List<CiInterpolatedPart> Parts = new List<CiInterpolatedPart>();

		internal string Suffix;

		public void AddPart(string prefix, CiExpr arg, CiExpr widthExpr = null, int format = ' ', int precision = -1)
		{
			this.Parts.Add(new CiInterpolatedPart());
			CiInterpolatedPart part = this.Parts[this.Parts.Count - 1];
			part.Prefix = prefix;
			part.Argument = arg;
			part.WidthExpr = widthExpr;
			part.Format = format;
			part.Precision = precision;
		}

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitInterpolatedString(this, parent);
		}
	}

	public class CiImplicitEnumValue : CiExpr
	{

		internal int Value;

		public override int IntValue() => this.Value;
	}

	public class CiSymbolReference : CiExpr
	{

		internal CiExpr Left;

		internal string Name;

		internal CiSymbol Symbol;

		public override bool IsConstEnum() => this.Symbol.Parent is CiEnum;

		public override int IntValue()
		{
			CiConst konst = (CiConst) this.Symbol;
			return konst.Value.IntValue();
		}

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitSymbolReference(this, parent);
		}

		public override bool IsReferenceTo(CiSymbol symbol) => this.Symbol == symbol;

		public override string ToString() => this.Left != null ? $"{this.Left}.{this.Name}" : this.Name;
	}

	public abstract class CiUnaryExpr : CiExpr
	{

		internal CiToken Op;

		internal CiExpr Inner;
	}

	public class CiPrefixExpr : CiUnaryExpr
	{

		public override bool IsConstEnum() => this.Type is CiEnumFlags && this.Inner.IsConstEnum();

		public override int IntValue()
		{
			Debug.Assert(this.Op == CiToken.Tilde);
			return ~this.Inner.IntValue();
		}

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitPrefixExpr(this, parent);
		}
	}

	public class CiPostfixExpr : CiUnaryExpr
	{

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitPostfixExpr(this, parent);
		}
	}

	public class CiBinaryExpr : CiExpr
	{

		internal CiExpr Left;

		internal CiToken Op;

		internal CiExpr Right;

		public override bool IsIndexing() => this.Op == CiToken.LeftBracket;

		public override bool IsConstEnum()
		{
			switch (this.Op) {
			case CiToken.And:
			case CiToken.Or:
			case CiToken.Xor:
				return this.Type is CiEnumFlags && this.Left.IsConstEnum() && this.Right.IsConstEnum();
			default:
				return false;
			}
		}

		public override int IntValue()
		{
			switch (this.Op) {
			case CiToken.And:
				return this.Left.IntValue() & this.Right.IntValue();
			case CiToken.Or:
				return this.Left.IntValue() | this.Right.IntValue();
			case CiToken.Xor:
				return this.Left.IntValue() ^ this.Right.IntValue();
			default:
				throw new NotImplementedException();
			}
		}

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitBinaryExpr(this, parent);
		}

		public bool IsAssign()
		{
			switch (this.Op) {
			case CiToken.Assign:
			case CiToken.AddAssign:
			case CiToken.SubAssign:
			case CiToken.MulAssign:
			case CiToken.DivAssign:
			case CiToken.ModAssign:
			case CiToken.ShiftLeftAssign:
			case CiToken.ShiftRightAssign:
			case CiToken.AndAssign:
			case CiToken.OrAssign:
			case CiToken.XorAssign:
				return true;
			default:
				return false;
			}
		}

		public string GetOpString()
		{
			switch (this.Op) {
			case CiToken.Plus:
				return "+";
			case CiToken.Minus:
				return "-";
			case CiToken.Asterisk:
				return "*";
			case CiToken.Slash:
				return "/";
			case CiToken.Mod:
				return "%";
			case CiToken.ShiftLeft:
				return "<<";
			case CiToken.ShiftRight:
				return ">>";
			case CiToken.Less:
				return "<";
			case CiToken.LessOrEqual:
				return "<=";
			case CiToken.Greater:
				return ">";
			case CiToken.GreaterOrEqual:
				return ">=";
			case CiToken.Equal:
				return "==";
			case CiToken.NotEqual:
				return "!=";
			case CiToken.And:
				return "&";
			case CiToken.Or:
				return "|";
			case CiToken.Xor:
				return "^";
			case CiToken.CondAnd:
				return "&&";
			case CiToken.CondOr:
				return "||";
			case CiToken.Assign:
				return "=";
			case CiToken.AddAssign:
				return "+=";
			case CiToken.SubAssign:
				return "-=";
			case CiToken.MulAssign:
				return "*=";
			case CiToken.DivAssign:
				return "/=";
			case CiToken.ModAssign:
				return "%=";
			case CiToken.ShiftLeftAssign:
				return "<<=";
			case CiToken.ShiftRightAssign:
				return ">>=";
			case CiToken.AndAssign:
				return "&=";
			case CiToken.OrAssign:
				return "|=";
			case CiToken.XorAssign:
				return "^=";
			default:
				throw new NotImplementedException();
			}
		}

		public override string ToString() => this.Op == CiToken.LeftBracket ? $"{this.Left}[{this.Right}]" : $"({this.Left} {GetOpString()} {this.Right})";
	}

	public class CiSelectExpr : CiExpr
	{

		internal CiExpr Cond;

		internal CiExpr OnTrue;

		internal CiExpr OnFalse;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitSelectExpr(this, parent);
		}

		public override string ToString() => $"({this.Cond} ? {this.OnTrue} : {this.OnFalse})";
	}

	public class CiCallExpr : CiExpr
	{

		internal CiSymbolReference Method;

		internal readonly List<CiExpr> Arguments = new List<CiExpr>();

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitCallExpr(this, parent);
		}
	}

	public class CiLambdaExpr : CiScope
	{

		internal CiExpr Body;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLambdaExpr(this);
		}
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

	public class CiBlock : CiCondCompletionStatement
	{

		internal readonly List<CiStatement> Statements = new List<CiStatement>();

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitBlock(this);
		}
	}

	public class CiAssert : CiStatement
	{

		internal CiExpr Cond;

		internal CiExpr Message = null;

		public override bool CompletesNormally() => !(this.Cond is CiLiteralFalse);

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitAssert(this);
		}
	}

	public abstract class CiLoop : CiCondCompletionStatement
	{

		internal CiExpr Cond;

		internal CiStatement Body;

		internal bool HasBreak = false;
	}

	public class CiBreak : CiStatement
	{

		internal CiCondCompletionStatement LoopOrSwitch;

		public override bool CompletesNormally() => false;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitBreak(this);
		}
	}

	public class CiContinue : CiStatement
	{

		internal CiLoop Loop;

		public override bool CompletesNormally() => false;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitContinue(this);
		}
	}

	public class CiDoWhile : CiLoop
	{

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitDoWhile(this);
		}
	}

	public class CiFor : CiLoop
	{

		internal CiExpr Init;

		internal CiExpr Advance;

		internal bool IsRange = false;

		internal bool IsIteratorUsed;

		internal long RangeStep;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitFor(this);
		}
	}

	public class CiForeach : CiLoop
	{

		internal CiExpr Collection;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitForeach(this);
		}

		public CiVar GetVar() => this.FirstParameter();

		public CiVar GetValueVar() => this.FirstParameter().NextParameter();
	}

	public class CiIf : CiCondCompletionStatement
	{

		internal CiExpr Cond;

		internal CiStatement OnTrue;

		internal CiStatement OnFalse;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitIf(this);
		}
	}

	public class CiLock : CiStatement
	{

		internal CiExpr Lock;

		internal CiStatement Body;

		public override bool CompletesNormally() => this.Body.CompletesNormally();

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitLock(this);
		}
	}

	public class CiNative : CiStatement
	{

		internal string Content;

		public override bool CompletesNormally() => true;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitNative(this);
		}
	}

	public class CiReturn : CiStatement
	{

		internal CiExpr Value;

		public override bool CompletesNormally() => false;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitReturn(this);
		}
	}

	public class CiCase
	{

		internal readonly List<CiExpr> Values = new List<CiExpr>();

		internal readonly List<CiStatement> Body = new List<CiStatement>();
	}

	public class CiSwitch : CiCondCompletionStatement
	{

		internal CiExpr Value;

		internal readonly List<CiCase> Cases = new List<CiCase>();

		internal readonly List<CiStatement> DefaultBody = new List<CiStatement>();

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitSwitch(this);
		}

		public bool IsTypeMatching() => this.Value.Type is CiClassType klass && klass.Class.Id != CiId.StringClass;

		public static int LengthWithoutTrailingBreak(List<CiStatement> body)
		{
			int length = body.Count;
			if (length > 0 && body[length - 1] is CiBreak)
				length--;
			return length;
		}

		public bool HasDefault() => LengthWithoutTrailingBreak(this.DefaultBody) > 0;

		static bool HasBreak(CiStatement statement)
		{
			switch (statement) {
			case CiBreak _:
				return true;
			case CiIf ifStatement:
				return HasBreak(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasBreak(ifStatement.OnFalse));
			case CiBlock block:
				return block.Statements.Any(child => HasBreak(child));
			default:
				return false;
			}
		}

		public static bool HasEarlyBreak(List<CiStatement> body)
		{
			int length = LengthWithoutTrailingBreak(body);
			for (int i = 0; i < length; i++) {
				if (HasBreak(body[i]))
					return true;
			}
			return false;
		}

		static bool ListHasContinue(List<CiStatement> statements) => statements.Any(statement => HasContinue(statement));

		static bool HasContinue(CiStatement statement)
		{
			switch (statement) {
			case CiContinue _:
				return true;
			case CiIf ifStatement:
				return HasContinue(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasContinue(ifStatement.OnFalse));
			case CiSwitch switchStatement:
				return switchStatement.Cases.Any(kase => ListHasContinue(kase.Body)) || ListHasContinue(switchStatement.DefaultBody);
			case CiBlock block:
				return ListHasContinue(block.Statements);
			default:
				return false;
			}
		}

		public static bool HasEarlyBreakAndContinue(List<CiStatement> body) => HasEarlyBreak(body) && ListHasContinue(body);
	}

	public class CiThrow : CiStatement
	{

		internal CiExpr Message;

		public override bool CompletesNormally() => false;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitThrow(this);
		}
	}

	public class CiWhile : CiLoop
	{

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitWhile(this);
		}
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
	}

	public abstract class CiNumericType : CiType
	{
	}

	public class CiIntegerType : CiNumericType
	{

		public override bool IsAssignableFrom(CiType right) => right is CiIntegerType || right.Id == CiId.FloatIntType;
	}

	public class CiRangeType : CiIntegerType
	{

		internal int Min;

		internal int Max;

		public static CiRangeType New(int min, int max)
		{
			Debug.Assert(min <= max);
			return new CiRangeType { Min = min, Max = max };
		}

		public override string ToString() => this.Min == this.Max ? $"{this.Min}" : $"({this.Min} .. {this.Max})";

		public override bool IsAssignableFrom(CiType right)
		{
			switch (right) {
			case CiRangeType range:
				return this.Min <= range.Max && this.Max >= range.Min;
			case CiIntegerType _:
				return true;
			default:
				return right.Id == CiId.FloatIntType;
			}
		}

		public override bool EqualsType(CiType right) => right is CiRangeType that && this.Min == that.Min && this.Max == that.Max;

		public static int GetMask(int v)
		{
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			return v;
		}

		public int GetVariableBits() => GetMask(this.Min ^ this.Max);
	}

	public class CiFloatingType : CiNumericType
	{

		public override bool IsAssignableFrom(CiType right) => right is CiNumericType;
	}

	public abstract class CiNamedValue : CiSymbol
	{

		internal CiExpr TypeExpr;

		internal CiExpr Value;

		public bool IsAssignableStorage() => this.Type is CiStorageType && !(this.Type is CiArrayStorageType) && this.Value is CiLiteralNull;
	}

	public class CiMember : CiNamedValue
	{
		protected CiMember()
		{
		}

		internal CiVisibility Visibility;

		public static CiMember New(CiType type, CiId id, string name) => new CiMember { Visibility = CiVisibility.Public, Type = type, Id = id, Name = name };

		public virtual bool IsStatic()
		{
			throw new NotImplementedException();
		}
	}

	public class CiVar : CiNamedValue
	{

		internal bool IsAssigned = false;

		public static CiVar New(CiType type, string name, CiExpr defaultValue = null) => new CiVar { Type = type, Name = name, Value = defaultValue };

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitVar(this);
		}

		public CiVar NextParameter()
		{
			CiVar def = (CiVar) this.Next;
			return def;
		}
	}

	public enum CiVisitStatus
	{
		NotYet,
		InProgress,
		Done
	}

	public class CiConst : CiMember
	{

		internal CiMethodBase InMethod;

		internal CiVisitStatus VisitStatus;

		public override void AcceptStatement(CiVisitor visitor)
		{
			visitor.VisitConst(this);
		}

		public override bool IsStatic() => true;
	}

	public class CiField : CiMember
	{

		public override bool IsStatic() => false;
	}

	public class CiMethodBase : CiMember
	{

		internal bool IsMutator = false;

		internal bool Throws;

		internal CiStatement Body;

		internal bool IsLive = false;

		internal readonly HashSet<CiMethod> Calls = new HashSet<CiMethod>();
	}

	public class CiMethod : CiMethodBase
	{

		internal CiCallType CallType;

		internal readonly CiParameters Parameters = new CiParameters();

		public static CiMethod New(CiVisibility visibility, CiType type, CiId id, string name, CiVar param0 = null, CiVar param1 = null, CiVar param2 = null, CiVar param3 = null)
		{
			CiMethod result = new CiMethod { Visibility = visibility, CallType = CiCallType.Normal, Type = type, Id = id, Name = name };
			if (param0 != null) {
				result.Parameters.Add(param0);
				if (param1 != null) {
					result.Parameters.Add(param1);
					if (param2 != null) {
						result.Parameters.Add(param2);
						if (param3 != null)
							result.Parameters.Add(param3);
					}
				}
			}
			return result;
		}

		public static CiMethod NewStatic(CiType type, CiId id, string name, CiVar param0, CiVar param1 = null, CiVar param2 = null)
		{
			CiMethod result = New(CiVisibility.Public, type, id, name, param0, param1, param2);
			result.CallType = CiCallType.Static;
			return result;
		}

		public static CiMethod NewMutator(CiVisibility visibility, CiType type, CiId id, string name, CiVar param0 = null, CiVar param1 = null, CiVar param2 = null)
		{
			CiMethod result = New(visibility, type, id, name, param0, param1, param2);
			result.IsMutator = true;
			return result;
		}

		public override bool IsStatic() => this.CallType == CiCallType.Static;

		public bool IsAbstractOrVirtual() => this.CallType == CiCallType.Abstract || this.CallType == CiCallType.Virtual;

		public CiMethod GetDeclaringMethod()
		{
			CiMethod method = this;
			while (method.CallType == CiCallType.Override) {
				CiMethod baseMethod = (CiMethod) method.Parent.Parent.TryLookup(method.Name);
				method = baseMethod;
			}
			return method;
		}

		public bool IsToString() => this.Name == "ToString" && this.CallType != CiCallType.Static && this.Parameters.Count() == 0;
	}

	public class CiMethodGroup : CiMember
	{
		internal CiMethodGroup()
		{
		}

		internal readonly CiMethod[] Methods = new CiMethod[2];

		public static CiMethodGroup New(CiMethod method0, CiMethod method1)
		{
			CiMethodGroup result = new CiMethodGroup { Visibility = method0.Visibility, Name = method0.Name };
			result.Methods[0] = method0;
			result.Methods[1] = method1;
			return result;
		}
	}

	public abstract class CiContainerType : CiType
	{

		internal bool IsPublic;

		internal string Filename;
	}

	public class CiEnum : CiContainerType
	{

		internal bool HasExplicitValue = false;

		public void AcceptValues(CiVisitor visitor)
		{
			CiConst previous = null;
			for (CiSymbol symbol = this.First; symbol != null; symbol = symbol.Next) {
				CiConst konst = (CiConst) symbol;
				visitor.VisitEnumValue(konst, previous);
				previous = konst;
			}
		}
	}

	public class CiEnumFlags : CiEnum
	{
	}

	public class CiClass : CiContainerType
	{
		public CiClass()
		{
			Add(CiVar.New(new CiReadWriteClassType { Class = this }, "this"));
		}

		internal CiCallType CallType;

		internal int TypeParameterCount = 0;

		internal string BaseClassName = "";

		internal CiMethodBase Constructor;

		internal readonly List<CiConst> ConstArrays = new List<CiConst>();

		public bool HasBaseClass() => this.BaseClassName.Length > 0;

		public bool AddsVirtualMethods()
		{
			for (CiSymbol symbol = this.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiMethod method && method.IsAbstractOrVirtual())
					return true;
			}
			return false;
		}

		public static CiClass New(CiCallType callType, CiId id, string name, int typeParameterCount = 0) => new CiClass { CallType = callType, Id = id, Name = name, TypeParameterCount = typeParameterCount };

		public bool IsSameOrBaseOf(CiClass derived)
		{
			while (derived != this) {
				if (derived.Parent is CiClass parent)
					derived = parent;
				else
					return false;
			}
			return true;
		}

		public bool HasToString() => TryLookup("ToString") is CiMethod method && method.IsToString();

		public bool AddsToString() => this.Dict.ContainsKey("ToString") && this.Dict["ToString"] is CiMethod method && method.IsToString() && method.CallType != CiCallType.Override && method.CallType != CiCallType.Sealed;
	}

	public class CiClassType : CiType
	{

		internal CiClass Class;

		internal CiType TypeArg0;

		internal CiType TypeArg1;

		public CiType GetElementType() => this.TypeArg0;

		public CiType GetKeyType() => this.TypeArg0;

		public CiType GetValueType() => this.TypeArg1;

		public override bool IsNullable() => true;

		public override bool IsArray() => this.Class.Id == CiId.ArrayPtrClass;

		public override CiType GetBaseType() => IsArray() ? GetElementType().GetBaseType() : this;

		public override CiSymbol TryLookup(string name) => this.Class.TryLookup(name);

		protected bool EqualTypeArguments(CiClassType right)
		{
			switch (this.Class.TypeParameterCount) {
			case 0:
				return true;
			case 1:
				return this.TypeArg0.EqualsType(right.TypeArg0);
			case 2:
				return this.TypeArg0.EqualsType(right.TypeArg0) && this.TypeArg1.EqualsType(right.TypeArg1);
			default:
				throw new NotImplementedException();
			}
		}

		protected bool IsAssignableFromClass(CiClassType right) => this.Class.IsSameOrBaseOf(right.Class) && EqualTypeArguments(right);

		public override bool IsAssignableFrom(CiType right)
		{
			return right.Id == CiId.NullType || (right is CiClassType rightClass && IsAssignableFromClass(rightClass));
		}

		public override bool EqualsType(CiType right) => right is CiClassType that && this.Class == that.Class && EqualTypeArguments(that);

		public override string GetArraySuffix() => IsArray() ? "[]" : "";

		public virtual string GetClassSuffix() => "";

		public override string ToString()
		{
			if (IsArray())
				return GetElementType().GetBaseType().ToString() + GetArraySuffix() + GetElementType().GetArraySuffix();
			switch (this.Class.TypeParameterCount) {
			case 0:
				return this.Class.Name + GetClassSuffix();
			case 1:
				return $"{this.Class.Name}<{this.TypeArg0}>{GetClassSuffix()}";
			case 2:
				return $"{this.Class.Name}<{this.TypeArg0}, {this.TypeArg1}>{GetClassSuffix()}";
			default:
				throw new NotImplementedException();
			}
		}
	}

	public class CiReadWriteClassType : CiClassType
	{

		public override bool IsAssignableFrom(CiType right)
		{
			return right.Id == CiId.NullType || (right is CiReadWriteClassType rightClass && IsAssignableFromClass(rightClass));
		}

		public override string GetArraySuffix() => IsArray() ? "[]!" : "";

		public override string GetClassSuffix() => "!";
	}

	public class CiStorageType : CiReadWriteClassType
	{

		public override bool IsFinal() => this.Class.Id != CiId.MatchClass;

		public override bool IsNullable() => false;

		public override bool IsAssignableFrom(CiType right) => right is CiStorageType rightClass && this.Class == rightClass.Class && EqualTypeArguments(rightClass);

		public override string GetClassSuffix() => "()";
	}

	public class CiDynamicPtrType : CiReadWriteClassType
	{

		public override bool IsAssignableFrom(CiType right)
		{
			return right.Id == CiId.NullType || (right is CiDynamicPtrType rightClass && IsAssignableFromClass(rightClass));
		}

		public override string GetArraySuffix() => IsArray() ? "[]#" : "";

		public override string GetClassSuffix() => "#";
	}

	public class CiArrayStorageType : CiStorageType
	{

		internal CiExpr LengthExpr;

		internal int Length;

		internal bool PtrTaken = false;

		public override string ToString() => GetBaseType().ToString() + GetArraySuffix() + GetElementType().GetArraySuffix();

		public override CiType GetBaseType() => GetElementType().GetBaseType();

		public override bool IsArray() => true;

		public override string GetArraySuffix() => $"[{this.Length}]";

		public override bool EqualsType(CiType right) => right is CiArrayStorageType that && GetElementType().EqualsType(that.GetElementType()) && this.Length == that.Length;

		public override CiType GetStorageType() => GetElementType().GetStorageType();
	}

	public class CiStringType : CiClassType
	{
	}

	public class CiStringStorageType : CiStringType
	{

		public override bool IsNullable() => false;

		public override bool IsAssignableFrom(CiType right) => right is CiStringType;

		public override string GetClassSuffix() => "()";
	}

	public class CiPrintableType : CiType
	{

		public override bool IsAssignableFrom(CiType right)
		{
			switch (right) {
			case CiNumericType _:
			case CiStringType _:
				return true;
			case CiClassType klass:
				return klass.Class.HasToString();
			default:
				return false;
			}
		}
	}

	public class CiSystem : CiScope
	{
		internal CiSystem()
		{
			CiSymbol basePtr = CiVar.New(null, "base");
			basePtr.Id = CiId.BasePtr;
			Add(basePtr);
			Add(this.IntType);
			this.UIntType.Name = "uint";
			Add(this.UIntType);
			Add(this.LongType);
			this.ByteType.Name = "byte";
			Add(this.ByteType);
			CiRangeType shortType = CiRangeType.New(-32768, 32767);
			shortType.Name = "short";
			Add(shortType);
			CiRangeType ushortType = CiRangeType.New(0, 65535);
			ushortType.Name = "ushort";
			Add(ushortType);
			CiRangeType minus1Type = CiRangeType.New(-1, 2147483647);
			Add(this.FloatType);
			Add(this.DoubleType);
			Add(this.BoolType);
			CiClass stringClass = CiClass.New(CiCallType.Normal, CiId.StringClass, "string");
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringContains, "Contains", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringEndsWith, "EndsWith", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, minus1Type, CiId.StringIndexOf, "IndexOf", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, minus1Type, CiId.StringLastIndexOf, "LastIndexOf", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMember.New(this.UIntType, CiId.StringLength, "Length"));
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.StringStorageType, CiId.StringReplace, "Replace", CiVar.New(this.StringPtrType, "oldValue"), CiVar.New(this.StringPtrType, "newValue")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringStartsWith, "StartsWith", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.StringStorageType, CiId.StringSubstring, "Substring", CiVar.New(this.IntType, "offset"), CiVar.New(this.IntType, "length", NewLiteralLong(-1))));
			this.StringPtrType.Class = stringClass;
			Add(this.StringPtrType);
			this.StringStorageType.Class = stringClass;
			CiMethod arrayBinarySearchPart = CiMethod.New(CiVisibility.NumericElementType, this.IntType, CiId.ArrayBinarySearchPart, "BinarySearch", CiVar.New(this.TypeParam0, "value"), CiVar.New(this.IntType, "startIndex"), CiVar.New(this.IntType, "count"));
			this.ArrayPtrClass.Add(arrayBinarySearchPart);
			this.ArrayPtrClass.Add(CiMethod.New(CiVisibility.Public, this.VoidType, CiId.ArrayCopyTo, "CopyTo", CiVar.New(this.IntType, "sourceIndex"), CiVar.New(new CiReadWriteClassType { Class = this.ArrayPtrClass, TypeArg0 = this.TypeParam0 }, "destinationArray"), CiVar.New(this.IntType, "destinationIndex"), CiVar.New(this.IntType, "count")));
			CiMethod arrayFillPart = CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ArrayFillPart, "Fill", CiVar.New(this.TypeParam0, "value"), CiVar.New(this.IntType, "startIndex"), CiVar.New(this.IntType, "count"));
			this.ArrayPtrClass.Add(arrayFillPart);
			CiMethod arraySortPart = CiMethod.NewMutator(CiVisibility.NumericElementType, this.VoidType, CiId.ArraySortPart, "Sort", CiVar.New(this.IntType, "startIndex"), CiVar.New(this.IntType, "count"));
			this.ArrayPtrClass.Add(arraySortPart);
			this.ArrayStorageClass.Parent = this.ArrayPtrClass;
			this.ArrayStorageClass.Add(CiMethodGroup.New(CiMethod.New(CiVisibility.NumericElementType, this.IntType, CiId.ArrayBinarySearchAll, "BinarySearch", CiVar.New(this.TypeParam0, "value")), arrayBinarySearchPart));
			this.ArrayStorageClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ArrayFillAll, "Fill", CiVar.New(this.TypeParam0, "value")), arrayFillPart));
			this.ArrayStorageClass.Add(CiMember.New(this.UIntType, CiId.ArrayLength, "Length"));
			this.ArrayStorageClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.NumericElementType, this.VoidType, CiId.ArraySortAll, "Sort"), arraySortPart));
			CiType typeParam0NotFinal = new CiType { Id = CiId.TypeParam0NotFinal, Name = "T" };
			CiType typeParam0Predicate = new CiType { Id = CiId.TypeParam0Predicate, Name = "Predicate<T>" };
			CiClass listClass = AddCollection(CiId.ListClass, "List", 1, CiId.ListClear, CiId.ListCount);
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ListAdd, "Add", CiVar.New(typeParam0NotFinal, "value")));
			listClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.ListAll, "All", CiVar.New(typeParam0Predicate, "predicate")));
			listClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.ListAny, "Any", CiVar.New(typeParam0Predicate, "predicate")));
			listClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.ListContains, "Contains", CiVar.New(this.TypeParam0, "value")));
			listClass.Add(CiMethod.New(CiVisibility.Public, this.VoidType, CiId.ListCopyTo, "CopyTo", CiVar.New(this.IntType, "sourceIndex"), CiVar.New(new CiReadWriteClassType { Class = this.ArrayPtrClass, TypeArg0 = this.TypeParam0 }, "destinationArray"), CiVar.New(this.IntType, "destinationIndex"), CiVar.New(this.IntType, "count")));
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ListInsert, "Insert", CiVar.New(this.UIntType, "index"), CiVar.New(typeParam0NotFinal, "value")));
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ListRemoveAt, "RemoveAt", CiVar.New(this.IntType, "index")));
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ListRemoveRange, "RemoveRange", CiVar.New(this.IntType, "index"), CiVar.New(this.IntType, "count")));
			listClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.NumericElementType, this.VoidType, CiId.ListSortAll, "Sort"), CiMethod.NewMutator(CiVisibility.NumericElementType, this.VoidType, CiId.ListSortPart, "Sort", CiVar.New(this.IntType, "startIndex"), CiVar.New(this.IntType, "count"))));
			CiClass queueClass = AddCollection(CiId.QueueClass, "Queue", 1, CiId.QueueClear, CiId.QueueCount);
			queueClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.TypeParam0, CiId.QueueDequeue, "Dequeue"));
			queueClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.QueueEnqueue, "Enqueue", CiVar.New(this.TypeParam0, "value")));
			queueClass.Add(CiMethod.New(CiVisibility.Public, this.TypeParam0, CiId.QueuePeek, "Peek"));
			CiClass stackClass = AddCollection(CiId.StackClass, "Stack", 1, CiId.StackClear, CiId.StackCount);
			stackClass.Add(CiMethod.New(CiVisibility.Public, this.TypeParam0, CiId.StackPeek, "Peek"));
			stackClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.StackPush, "Push", CiVar.New(this.TypeParam0, "value")));
			stackClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.TypeParam0, CiId.StackPop, "Pop"));
			CiClass hashSetClass = AddCollection(CiId.HashSetClass, "HashSet", 1, CiId.HashSetClear, CiId.HashSetCount);
			hashSetClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.HashSetAdd, "Add", CiVar.New(this.TypeParam0, "value")));
			hashSetClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.HashSetContains, "Contains", CiVar.New(this.TypeParam0, "value")));
			hashSetClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.HashSetRemove, "Remove", CiVar.New(this.TypeParam0, "value")));
			AddDictionary(CiId.DictionaryClass, "Dictionary", CiId.DictionaryClear, CiId.DictionaryContainsKey, CiId.DictionaryCount, CiId.DictionaryRemove);
			AddDictionary(CiId.SortedDictionaryClass, "SortedDictionary", CiId.SortedDictionaryClear, CiId.SortedDictionaryContainsKey, CiId.SortedDictionaryCount, CiId.SortedDictionaryRemove);
			AddDictionary(CiId.OrderedDictionaryClass, "OrderedDictionary", CiId.OrderedDictionaryClear, CiId.OrderedDictionaryContainsKey, CiId.OrderedDictionaryCount, CiId.OrderedDictionaryRemove);
			CiClass consoleBase = CiClass.New(CiCallType.Static, CiId.None, "ConsoleBase");
			consoleBase.Add(CiMethod.NewStatic(this.VoidType, CiId.ConsoleWrite, "Write", CiVar.New(this.PrintableType, "value")));
			consoleBase.Add(CiMethod.NewStatic(this.VoidType, CiId.ConsoleWriteLine, "WriteLine", CiVar.New(this.PrintableType, "value", NewLiteralString(""))));
			CiClass consoleClass = CiClass.New(CiCallType.Static, CiId.None, "Console");
			CiMember consoleError = CiMember.New(consoleBase, CiId.ConsoleError, "Error");
			consoleClass.Add(consoleError);
			Add(consoleClass);
			consoleClass.Parent = consoleBase;
			CiClass utf8EncodingClass = CiClass.New(CiCallType.Sealed, CiId.None, "UTF8Encoding");
			utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, this.IntType, CiId.UTF8GetByteCount, "GetByteCount", CiVar.New(this.StringPtrType, "str")));
			utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, this.VoidType, CiId.UTF8GetBytes, "GetBytes", CiVar.New(this.StringPtrType, "str"), CiVar.New(new CiReadWriteClassType { Class = this.ArrayPtrClass, TypeArg0 = this.ByteType }, "bytes"), CiVar.New(this.IntType, "byteIndex")));
			utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, this.StringStorageType, CiId.UTF8GetString, "GetString", CiVar.New(new CiClassType { Class = this.ArrayPtrClass, TypeArg0 = this.ByteType }, "bytes"), CiVar.New(this.IntType, "offset"), CiVar.New(this.IntType, "length")));
			CiClass encodingClass = CiClass.New(CiCallType.Static, CiId.None, "Encoding");
			encodingClass.Add(CiMember.New(utf8EncodingClass, CiId.None, "UTF8"));
			Add(encodingClass);
			CiClass environmentClass = CiClass.New(CiCallType.Static, CiId.None, "Environment");
			environmentClass.Add(CiMethod.NewStatic(this.StringPtrType, CiId.EnvironmentGetEnvironmentVariable, "GetEnvironmentVariable", CiVar.New(this.StringPtrType, "name")));
			Add(environmentClass);
			CiEnum regexOptionsEnum = new CiEnumFlags { Name = "RegexOptions" };
			CiConst regexOptionsNone = NewConstInt("None", 0);
			AddEnumValue(regexOptionsEnum, regexOptionsNone);
			AddEnumValue(regexOptionsEnum, NewConstInt("IgnoreCase", 1));
			AddEnumValue(regexOptionsEnum, NewConstInt("Multiline", 2));
			AddEnumValue(regexOptionsEnum, NewConstInt("Singleline", 16));
			Add(regexOptionsEnum);
			CiClass regexClass = CiClass.New(CiCallType.Sealed, CiId.RegexClass, "Regex");
			regexClass.Add(CiMethod.NewStatic(this.StringStorageType, CiId.RegexEscape, "Escape", CiVar.New(this.StringPtrType, "str")));
			regexClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.BoolType, CiId.RegexIsMatchStr, "IsMatch", CiVar.New(this.StringPtrType, "input"), CiVar.New(this.StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)), CiMethod.New(CiVisibility.Public, this.BoolType, CiId.RegexIsMatchRegex, "IsMatch", CiVar.New(this.StringPtrType, "input"))));
			regexClass.Add(CiMethod.NewStatic(new CiDynamicPtrType { Class = regexClass }, CiId.RegexCompile, "Compile", CiVar.New(this.StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)));
			Add(regexClass);
			CiClass matchClass = CiClass.New(CiCallType.Sealed, CiId.MatchClass, "Match");
			matchClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.MatchFindStr, "Find", CiVar.New(this.StringPtrType, "input"), CiVar.New(this.StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)), CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.MatchFindRegex, "Find", CiVar.New(this.StringPtrType, "input"), CiVar.New(new CiClassType { Class = regexClass }, "pattern"))));
			matchClass.Add(CiMember.New(this.IntType, CiId.MatchStart, "Start"));
			matchClass.Add(CiMember.New(this.IntType, CiId.MatchEnd, "End"));
			matchClass.Add(CiMethod.New(CiVisibility.Public, this.StringPtrType, CiId.MatchGetCapture, "GetCapture", CiVar.New(this.UIntType, "group")));
			matchClass.Add(CiMember.New(this.UIntType, CiId.MatchLength, "Length"));
			matchClass.Add(CiMember.New(this.StringPtrType, CiId.MatchValue, "Value"));
			Add(matchClass);
			CiFloatingType floatIntType = new CiFloatingType { Id = CiId.FloatIntType, Name = "float" };
			CiClass mathClass = CiClass.New(CiCallType.Static, CiId.None, "Math");
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Acos", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Asin", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Atan", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Atan2", CiVar.New(this.DoubleType, "y"), CiVar.New(this.DoubleType, "x")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Cbrt", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathCeiling, "Ceiling", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Cos", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Cosh", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(NewConstDouble("E", 2.7182818284590451));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Exp", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathMethod, "Floor", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathFusedMultiplyAdd, "FusedMultiplyAdd", CiVar.New(this.DoubleType, "x"), CiVar.New(this.DoubleType, "y"), CiVar.New(this.DoubleType, "z")));
			mathClass.Add(CiMethod.NewStatic(this.BoolType, CiId.MathIsFinite, "IsFinite", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.BoolType, CiId.MathIsInfinity, "IsInfinity", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.BoolType, CiId.MathIsNaN, "IsNaN", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Log", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathLog2, "Log2", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Log10", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMember.New(this.FloatType, CiId.MathNaN, "NaN"));
			mathClass.Add(CiMember.New(this.FloatType, CiId.MathNegativeInfinity, "NegativeInfinity"));
			mathClass.Add(NewConstDouble("PI", 3.1415926535897931));
			mathClass.Add(CiMember.New(this.FloatType, CiId.MathPositiveInfinity, "PositiveInfinity"));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Pow", CiVar.New(this.DoubleType, "x"), CiVar.New(this.DoubleType, "y")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Sin", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Sinh", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Sqrt", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Tan", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Tanh", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathTruncate, "Truncate", CiVar.New(this.DoubleType, "a")));
			Add(mathClass);
			CiClass lockClass = CiClass.New(CiCallType.Sealed, CiId.LockClass, "Lock");
			Add(lockClass);
			this.LockPtrType.Class = lockClass;
		}

		internal CiType VoidType = new CiType { Id = CiId.VoidType, Name = "void" };

		internal CiType NullType = new CiType { Id = CiId.NullType, Name = "null" };

		CiType TypeParam0 = new CiType { Id = CiId.TypeParam0, Name = "T" };

		internal CiIntegerType IntType = new CiIntegerType { Id = CiId.IntType, Name = "int" };

		CiRangeType UIntType = CiRangeType.New(0, 2147483647);

		internal CiIntegerType LongType = new CiIntegerType { Id = CiId.LongType, Name = "long" };

		internal CiRangeType ByteType = CiRangeType.New(0, 255);

		CiFloatingType FloatType = new CiFloatingType { Id = CiId.FloatType, Name = "float" };

		internal CiFloatingType DoubleType = new CiFloatingType { Id = CiId.DoubleType, Name = "double" };

		internal CiRangeType CharType = CiRangeType.New(-128, 65535);

		internal CiEnum BoolType = new CiEnum { Id = CiId.BoolType, Name = "bool" };

		internal CiStringType StringPtrType = new CiStringType { Id = CiId.StringPtrType, Name = "string" };

		internal CiStringStorageType StringStorageType = new CiStringStorageType { Id = CiId.StringStorageType };

		internal CiType PrintableType = new CiPrintableType { Name = "printable" };

		internal CiClass ArrayPtrClass = CiClass.New(CiCallType.Normal, CiId.ArrayPtrClass, "ArrayPtr", 1);

		internal CiClass ArrayStorageClass = CiClass.New(CiCallType.Normal, CiId.ArrayStorageClass, "ArrayStorage", 1);

		internal CiReadWriteClassType LockPtrType = new CiReadWriteClassType();

		internal CiLiteralLong NewLiteralLong(long value, int line = 0)
		{
			CiType type = value >= -2147483648 && value <= 2147483647 ? CiRangeType.New((int) value, (int) value) : this.LongType;
			return new CiLiteralLong { Line = line, Type = type, Value = value };
		}

		internal CiLiteralString NewLiteralString(string value, int line = 0) => new CiLiteralString { Line = line, Type = this.StringPtrType, Value = value };

		internal CiType PromoteIntegerTypes(CiType left, CiType right)
		{
			return left == this.LongType || right == this.LongType ? this.LongType : this.IntType;
		}

		internal CiType PromoteFloatingTypes(CiType left, CiType right)
		{
			if (left.Id == CiId.DoubleType || right.Id == CiId.DoubleType)
				return this.DoubleType;
			if (left.Id == CiId.FloatType || right.Id == CiId.FloatType || left.Id == CiId.FloatIntType || right.Id == CiId.FloatIntType)
				return this.FloatType;
			return null;
		}

		internal CiType PromoteNumericTypes(CiType left, CiType right)
		{
			CiType result = PromoteFloatingTypes(left, right);
			return result != null ? result : PromoteIntegerTypes(left, right);
		}

		CiClass AddCollection(CiId id, string name, int typeParameterCount, CiId clearId, CiId countId)
		{
			CiClass result = CiClass.New(CiCallType.Normal, id, name, typeParameterCount);
			result.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, clearId, "Clear"));
			result.Add(CiMember.New(this.UIntType, countId, "Count"));
			Add(result);
			return result;
		}

		void AddDictionary(CiId id, string name, CiId clearId, CiId containsKeyId, CiId countId, CiId removeId)
		{
			CiClass dict = AddCollection(id, name, 2, clearId, countId);
			dict.Add(CiMethod.NewMutator(CiVisibility.FinalValueType, this.VoidType, CiId.DictionaryAdd, "Add", CiVar.New(this.TypeParam0, "key")));
			dict.Add(CiMethod.New(CiVisibility.Public, this.BoolType, containsKeyId, "ContainsKey", CiVar.New(this.TypeParam0, "key")));
			dict.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, removeId, "Remove", CiVar.New(this.TypeParam0, "key")));
		}

		static void AddEnumValue(CiEnum enu, CiConst value)
		{
			value.Type = enu;
			enu.Add(value);
		}

		CiConst NewConstInt(string name, int value)
		{
			CiConst result = new CiConst { Visibility = CiVisibility.Public, Name = name, Value = NewLiteralLong(value), VisitStatus = CiVisitStatus.Done };
			result.Type = result.Value.Type;
			return result;
		}

		CiConst NewConstDouble(string name, double value) => new CiConst { Visibility = CiVisibility.Public, Name = name, Value = new CiLiteralDouble { Value = value, Type = this.DoubleType }, Type = this.DoubleType, VisitStatus = CiVisitStatus.Done };

		internal static CiSystem New() => new CiSystem();
	}

	public class CiProgram : CiScope
	{

		internal CiSystem System;

		internal readonly List<string> TopLevelNatives = new List<string>();

		internal readonly List<CiClass> Classes = new List<CiClass>();

		internal readonly Dictionary<string, byte[]> Resources = new Dictionary<string, byte[]>();
	}

	public class CiParser : CiLexer
	{

		internal CiProgram Program;

		string XcrementParent = null;

		CiLoop CurrentLoop = null;

		CiCondCompletionStatement CurrentLoopOrSwitch = null;

		bool DocParseLine(CiDocPara para)
		{
			if (para.Children.Count > 0)
				para.Children.Add(new CiDocLine());
			this.LexemeOffset = this.CharOffset;
			for (int lastNonWhitespace = 0;;) {
				switch (PeekChar()) {
				case -1:
				case '\n':
				case '\r':
					para.Children.Add(new CiDocText { Text = GetLexeme() });
					return lastNonWhitespace == '.';
				case '\t':
				case ' ':
					ReadChar();
					break;
				case '`':
					if (this.CharOffset > this.LexemeOffset)
						para.Children.Add(new CiDocText { Text = GetLexeme() });
					ReadChar();
					this.LexemeOffset = this.CharOffset;
					for (;;) {
						int c = PeekChar();
						if (c == '`') {
							para.Children.Add(new CiDocCode { Text = GetLexeme() });
							ReadChar();
							break;
						}
						if (c < 0 || c == '\n') {
							ReportError("Unterminated code in documentation comment");
							break;
						}
						ReadChar();
					}
					this.LexemeOffset = this.CharOffset;
					lastNonWhitespace = '`';
					break;
				default:
					lastNonWhitespace = ReadChar();
					break;
				}
			}
		}

		void DocParsePara(CiDocPara para)
		{
			do {
				DocParseLine(para);
				NextToken();
			}
			while (See(CiToken.DocRegular));
		}

		CiCodeDoc ParseDoc()
		{
			if (!See(CiToken.DocRegular))
				return null;
			CiCodeDoc doc = new CiCodeDoc();
			bool period;
			do {
				period = DocParseLine(doc.Summary);
				NextToken();
			}
			while (!period && See(CiToken.DocRegular));
			for (;;) {
				switch (this.CurrentToken) {
				case CiToken.DocRegular:
					CiDocPara para = new CiDocPara();
					DocParsePara(para);
					doc.Details.Add(para);
					break;
				case CiToken.DocBullet:
					CiDocList list = new CiDocList();
					do {
						list.Items.Add(new CiDocPara());
						DocParsePara(list.Items[list.Items.Count - 1]);
					}
					while (See(CiToken.DocBullet));
					doc.Details.Add(list);
					break;
				case CiToken.DocBlank:
					NextToken();
					break;
				default:
					return doc;
				}
			}
		}

		void CheckXcrementParent()
		{
			if (this.XcrementParent != null) {
				string op = See(CiToken.Increment) ? "++" : "--";
				ReportError($"{op} not allowed on the right side of {this.XcrementParent}");
			}
		}

		CiLiteralDouble ParseDouble()
		{
			double d;
			bool ok;
			 ok = double.TryParse(GetLexeme().Replace("_", ""), out d); if (!ok)
				ReportError("Invalid floating-point number");
			CiLiteralDouble result = new CiLiteralDouble { Line = this.Line, Type = this.Program.System.DoubleType, Value = d };
			NextToken();
			return result;
		}

		bool SeeDigit()
		{
			int c = PeekChar();
			return c >= '0' && c <= '9';
		}

		CiInterpolatedString ParseInterpolatedString()
		{
			CiInterpolatedString result = new CiInterpolatedString { Line = this.Line };
			do {
				string prefix = this.StringValue.Replace("{{", "{");
				NextToken();
				CiExpr arg = ParseExpr();
				CiExpr width = Eat(CiToken.Comma) ? ParseExpr() : null;
				int format = ' ';
				int precision = -1;
				if (See(CiToken.Colon)) {
					format = ReadChar();
					if (SeeDigit()) {
						precision = ReadChar() - '0';
						if (SeeDigit())
							precision = precision * 10 + ReadChar() - '0';
					}
					NextToken();
				}
				result.AddPart(prefix, arg, width, format, precision);
				Check(CiToken.RightBrace);
			}
			while (ReadString(true) == CiToken.InterpolatedString);
			result.Suffix = this.StringValue.Replace("{{", "{");
			NextToken();
			return result;
		}

		CiExpr ParseParenthesized()
		{
			Expect(CiToken.LeftParenthesis);
			CiExpr result = ParseExpr();
			Expect(CiToken.RightParenthesis);
			return result;
		}

		CiSymbolReference ParseSymbolReference(CiExpr left)
		{
			CiSymbolReference result = new CiSymbolReference { Line = this.Line, Left = left, Name = this.StringValue };
			NextToken();
			return result;
		}

		void ParseCollection(List<CiExpr> result, CiToken closing)
		{
			if (!See(closing)) {
				do
					result.Add(ParseExpr());
				while (Eat(CiToken.Comma));
			}
			ExpectOrSkip(closing);
		}

		CiExpr ParsePrimaryExpr()
		{
			CiExpr result;
			switch (this.CurrentToken) {
			case CiToken.Increment:
			case CiToken.Decrement:
				CheckXcrementParent();
				return new CiPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr() };
			case CiToken.Minus:
			case CiToken.Tilde:
			case CiToken.ExclamationMark:
				return new CiPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr() };
			case CiToken.New:
				CiPrefixExpr newResult = new CiPrefixExpr { Line = this.Line, Op = NextToken() };
				result = ParseType();
				if (Eat(CiToken.LeftBrace))
					result = new CiBinaryExpr { Line = this.Line, Left = result, Op = CiToken.LeftBrace, Right = ParseObjectLiteral() };
				newResult.Inner = result;
				return newResult;
			case CiToken.LiteralLong:
				result = this.Program.System.NewLiteralLong(this.LongValue, this.Line);
				NextToken();
				break;
			case CiToken.LiteralDouble:
				result = ParseDouble();
				break;
			case CiToken.LiteralChar:
				result = CiLiteralChar.New((int) this.LongValue, this.Line);
				NextToken();
				break;
			case CiToken.LiteralString:
				result = this.Program.System.NewLiteralString(this.StringValue, this.Line);
				NextToken();
				break;
			case CiToken.False:
				result = new CiLiteralFalse { Line = this.Line, Type = this.Program.System.BoolType };
				NextToken();
				break;
			case CiToken.True:
				result = new CiLiteralTrue { Line = this.Line, Type = this.Program.System.BoolType };
				NextToken();
				break;
			case CiToken.Null:
				result = new CiLiteralNull { Line = this.Line, Type = this.Program.System.NullType };
				NextToken();
				break;
			case CiToken.InterpolatedString:
				result = ParseInterpolatedString();
				break;
			case CiToken.LeftParenthesis:
				result = ParseParenthesized();
				break;
			case CiToken.Id:
				CiSymbolReference symbol = ParseSymbolReference(null);
				if (Eat(CiToken.FatArrow)) {
					CiLambdaExpr lambda = new CiLambdaExpr { Line = symbol.Line };
					lambda.Add(CiVar.New(null, symbol.Name));
					lambda.Body = ParseExpr();
					return lambda;
				}
				result = symbol;
				break;
			case CiToken.Resource:
				NextToken();
				if (Eat(CiToken.Less) && this.StringValue == "byte" && Eat(CiToken.Id) && Eat(CiToken.LeftBracket) && Eat(CiToken.RightBracket) && Eat(CiToken.Greater))
					result = new CiPrefixExpr { Line = this.Line, Op = CiToken.Resource, Inner = ParseParenthesized() };
				else {
					ReportError("Expected 'resource<byte[]>'");
					result = null;
				}
				break;
			default:
				ReportError("Invalid expression");
				result = null;
				break;
			}
			for (;;) {
				switch (this.CurrentToken) {
				case CiToken.Dot:
					NextToken();
					result = ParseSymbolReference(result);
					break;
				case CiToken.LeftParenthesis:
					NextToken();
					if (result is CiSymbolReference method) {
						CiCallExpr call = new CiCallExpr { Line = this.Line, Method = method };
						ParseCollection(call.Arguments, CiToken.RightParenthesis);
						result = call;
					}
					else
						ReportError("Expected a method");
					break;
				case CiToken.LeftBracket:
					result = new CiBinaryExpr { Line = this.Line, Left = result, Op = NextToken(), Right = See(CiToken.RightBracket) ? null : ParseExpr() };
					Expect(CiToken.RightBracket);
					break;
				case CiToken.Increment:
				case CiToken.Decrement:
					CheckXcrementParent();
					result = new CiPostfixExpr { Line = this.Line, Inner = result, Op = NextToken() };
					break;
				case CiToken.ExclamationMark:
				case CiToken.Hash:
					result = new CiPostfixExpr { Line = this.Line, Inner = result, Op = NextToken() };
					break;
				default:
					return result;
				}
			}
		}

		CiExpr ParseMulExpr()
		{
			CiExpr left = ParsePrimaryExpr();
			for (;;) {
				switch (this.CurrentToken) {
				case CiToken.Asterisk:
				case CiToken.Slash:
				case CiToken.Mod:
					left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParsePrimaryExpr() };
					break;
				default:
					return left;
				}
			}
		}

		CiExpr ParseAddExpr()
		{
			CiExpr left = ParseMulExpr();
			while (See(CiToken.Plus) || See(CiToken.Minus))
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseMulExpr() };
			return left;
		}

		CiExpr ParseShiftExpr()
		{
			CiExpr left = ParseAddExpr();
			while (See(CiToken.ShiftLeft) || See(CiToken.ShiftRight))
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAddExpr() };
			return left;
		}

		CiExpr ParseRelExpr()
		{
			CiExpr left = ParseShiftExpr();
			for (;;) {
				switch (this.CurrentToken) {
				case CiToken.Less:
				case CiToken.LessOrEqual:
				case CiToken.Greater:
				case CiToken.GreaterOrEqual:
					left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseShiftExpr() };
					break;
				case CiToken.Is:
					CiBinaryExpr isExpr = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParsePrimaryExpr() };
					if (See(CiToken.Id)) {
						isExpr.Right = new CiVar { Line = this.Line, TypeExpr = isExpr.Right, Name = this.StringValue };
						NextToken();
					}
					return isExpr;
				default:
					return left;
				}
			}
		}

		CiExpr ParseEqualityExpr()
		{
			CiExpr left = ParseRelExpr();
			while (See(CiToken.Equal) || See(CiToken.NotEqual))
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseRelExpr() };
			return left;
		}

		CiExpr ParseAndExpr()
		{
			CiExpr left = ParseEqualityExpr();
			while (See(CiToken.And))
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseEqualityExpr() };
			return left;
		}

		CiExpr ParseXorExpr()
		{
			CiExpr left = ParseAndExpr();
			while (See(CiToken.Xor))
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAndExpr() };
			return left;
		}

		CiExpr ParseOrExpr()
		{
			CiExpr left = ParseXorExpr();
			while (See(CiToken.Or))
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseXorExpr() };
			return left;
		}

		CiExpr ParseCondAndExpr()
		{
			CiExpr left = ParseOrExpr();
			while (See(CiToken.CondAnd)) {
				string saveXcrementParent = this.XcrementParent;
				this.XcrementParent = "&&";
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseOrExpr() };
				this.XcrementParent = saveXcrementParent;
			}
			return left;
		}

		CiExpr ParseCondOrExpr()
		{
			CiExpr left = ParseCondAndExpr();
			while (See(CiToken.CondOr)) {
				string saveXcrementParent = this.XcrementParent;
				this.XcrementParent = "||";
				left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseCondAndExpr() };
				this.XcrementParent = saveXcrementParent;
			}
			return left;
		}

		CiExpr ParseExpr()
		{
			CiExpr left = ParseCondOrExpr();
			if (See(CiToken.QuestionMark)) {
				CiSelectExpr result = new CiSelectExpr { Line = this.Line, Cond = left };
				NextToken();
				string saveXcrementParent = this.XcrementParent;
				this.XcrementParent = "?";
				result.OnTrue = ParseExpr();
				Expect(CiToken.Colon);
				result.OnFalse = ParseExpr();
				this.XcrementParent = saveXcrementParent;
				return result;
			}
			return left;
		}

		CiExpr ParseType()
		{
			CiExpr left = ParsePrimaryExpr();
			if (Eat(CiToken.Range))
				return new CiBinaryExpr { Line = this.Line, Left = left, Op = CiToken.Range, Right = ParsePrimaryExpr() };
			if (left is CiSymbolReference symbol && Eat(CiToken.Less)) {
				CiAggregateInitializer typeArgs = new CiAggregateInitializer();
				left = new CiSymbolReference { Line = this.Line, Left = typeArgs, Name = symbol.Name };
				bool saveTypeArg = this.ParsingTypeArg;
				this.ParsingTypeArg = true;
				do
					typeArgs.Items.Add(ParseType());
				while (Eat(CiToken.Comma));
				Expect(CiToken.RightAngle);
				this.ParsingTypeArg = saveTypeArg;
				if (Eat(CiToken.ExclamationMark))
					left = new CiPostfixExpr { Line = this.Line, Inner = left, Op = CiToken.ExclamationMark };
				else if (Eat(CiToken.LeftParenthesis)) {
					Expect(CiToken.RightParenthesis);
					CiSymbolReference classType = (CiSymbolReference) left;
					left = new CiCallExpr { Line = this.Line, Method = classType };
				}
			}
			return left;
		}

		CiExpr ParseConstInitializer()
		{
			if (Eat(CiToken.LeftBrace)) {
				CiAggregateInitializer result = new CiAggregateInitializer { Line = this.Line };
				ParseCollection(result.Items, CiToken.RightBrace);
				return result;
			}
			return ParseExpr();
		}

		CiAggregateInitializer ParseObjectLiteral()
		{
			CiAggregateInitializer result = new CiAggregateInitializer { Line = this.Line };
			do {
				int line = this.Line;
				CiExpr field = ParseSymbolReference(null);
				Expect(CiToken.Assign);
				result.Items.Add(new CiBinaryExpr { Line = line, Left = field, Op = CiToken.Assign, Right = ParseExpr() });
			}
			while (Eat(CiToken.Comma));
			Expect(CiToken.RightBrace);
			return result;
		}

		CiExpr ParseInitializer()
		{
			if (!Eat(CiToken.Assign))
				return null;
			if (Eat(CiToken.LeftBrace))
				return ParseObjectLiteral();
			return ParseExpr();
		}

		void AddSymbol(CiScope scope, CiSymbol symbol)
		{
			if (scope.Contains(symbol))
				ReportError("Duplicate symbol");
			else
				scope.Add(symbol);
		}

		CiVar ParseVar(CiExpr type)
		{
			CiVar result = new CiVar { Line = this.Line, TypeExpr = type, Name = this.StringValue };
			NextToken();
			result.Value = ParseInitializer();
			return result;
		}

		CiConst ParseConst()
		{
			Expect(CiToken.Const);
			CiConst konst = new CiConst { Line = this.Line, TypeExpr = ParseType(), Name = this.StringValue };
			NextToken();
			Expect(CiToken.Assign);
			konst.Value = ParseConstInitializer();
			Expect(CiToken.Semicolon);
			return konst;
		}

		CiExpr ParseAssign(bool allowVar)
		{
			CiExpr left = allowVar ? ParseType() : ParseExpr();
			switch (this.CurrentToken) {
			case CiToken.Assign:
			case CiToken.AddAssign:
			case CiToken.SubAssign:
			case CiToken.MulAssign:
			case CiToken.DivAssign:
			case CiToken.ModAssign:
			case CiToken.AndAssign:
			case CiToken.OrAssign:
			case CiToken.XorAssign:
			case CiToken.ShiftLeftAssign:
			case CiToken.ShiftRightAssign:
				return new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAssign(false) };
			case CiToken.Id:
				if (allowVar)
					return ParseVar(left);
				return left;
			default:
				return left;
			}
		}

		CiBlock ParseBlock()
		{
			CiBlock result = new CiBlock { Line = this.Line };
			Expect(CiToken.LeftBrace);
			while (!See(CiToken.RightBrace) && !See(CiToken.EndOfFile))
				result.Statements.Add(ParseStatement());
			Expect(CiToken.RightBrace);
			return result;
		}

		CiAssert ParseAssert()
		{
			CiAssert result = new CiAssert { Line = this.Line };
			Expect(CiToken.Assert);
			result.Cond = ParseExpr();
			if (Eat(CiToken.Comma))
				result.Message = ParseExpr();
			Expect(CiToken.Semicolon);
			return result;
		}

		CiBreak ParseBreak()
		{
			if (this.CurrentLoopOrSwitch == null)
				ReportError("break outside loop or switch");
			CiBreak result = new CiBreak { Line = this.Line, LoopOrSwitch = this.CurrentLoopOrSwitch };
			Expect(CiToken.Break);
			Expect(CiToken.Semicolon);
			if (this.CurrentLoopOrSwitch is CiLoop loop)
				loop.HasBreak = true;
			return result;
		}

		CiContinue ParseContinue()
		{
			if (this.CurrentLoop == null)
				ReportError("continue outside loop");
			CiContinue result = new CiContinue { Line = this.Line, Loop = this.CurrentLoop };
			Expect(CiToken.Continue);
			Expect(CiToken.Semicolon);
			return result;
		}

		void ParseLoopBody(CiLoop loop)
		{
			CiLoop outerLoop = this.CurrentLoop;
			CiCondCompletionStatement outerLoopOrSwitch = this.CurrentLoopOrSwitch;
			this.CurrentLoop = loop;
			this.CurrentLoopOrSwitch = loop;
			loop.Body = ParseStatement();
			this.CurrentLoopOrSwitch = outerLoopOrSwitch;
			this.CurrentLoop = outerLoop;
		}

		CiDoWhile ParseDoWhile()
		{
			CiDoWhile result = new CiDoWhile { Line = this.Line };
			Expect(CiToken.Do);
			ParseLoopBody(result);
			Expect(CiToken.While);
			result.Cond = ParseParenthesized();
			Expect(CiToken.Semicolon);
			return result;
		}

		CiFor ParseFor()
		{
			CiFor result = new CiFor { Line = this.Line };
			Expect(CiToken.For);
			Expect(CiToken.LeftParenthesis);
			if (!See(CiToken.Semicolon))
				result.Init = ParseAssign(true);
			Expect(CiToken.Semicolon);
			if (!See(CiToken.Semicolon))
				result.Cond = ParseExpr();
			Expect(CiToken.Semicolon);
			if (!See(CiToken.RightParenthesis))
				result.Advance = ParseAssign(false);
			Expect(CiToken.RightParenthesis);
			ParseLoopBody(result);
			return result;
		}

		void ParseForeachIterator(CiForeach result)
		{
			AddSymbol(result, new CiVar { Line = this.Line, TypeExpr = ParseType(), Name = this.StringValue });
			NextToken();
		}

		CiForeach ParseForeach()
		{
			CiForeach result = new CiForeach { Line = this.Line };
			Expect(CiToken.Foreach);
			Expect(CiToken.LeftParenthesis);
			if (Eat(CiToken.LeftParenthesis)) {
				ParseForeachIterator(result);
				Expect(CiToken.Comma);
				ParseForeachIterator(result);
				Expect(CiToken.RightParenthesis);
			}
			else
				ParseForeachIterator(result);
			Expect(CiToken.In);
			result.Collection = ParseExpr();
			Expect(CiToken.RightParenthesis);
			ParseLoopBody(result);
			return result;
		}

		CiIf ParseIf()
		{
			CiIf result = new CiIf { Line = this.Line };
			Expect(CiToken.If);
			result.Cond = ParseParenthesized();
			result.OnTrue = ParseStatement();
			if (Eat(CiToken.Else))
				result.OnFalse = ParseStatement();
			return result;
		}

		CiLock ParseLock()
		{
			CiLock result = new CiLock { Line = this.Line };
			Expect(CiToken.Lock_);
			result.Lock = ParseParenthesized();
			result.Body = ParseStatement();
			return result;
		}

		CiNative ParseNative()
		{
			int line = this.Line;
			Expect(CiToken.Native);
			int offset = this.CharOffset;
			Expect(CiToken.LeftBrace);
			int nesting = 1;
			for (;;) {
				if (See(CiToken.EndOfFile)) {
					Expect(CiToken.RightBrace);
					return null;
				}
				if (See(CiToken.LeftBrace))
					nesting++;
				else if (See(CiToken.RightBrace)) {
					if (--nesting == 0)
						break;
				}
				NextToken();
			}
			Debug.Assert(this.Input[this.CharOffset - 1] == '}');
			CiNative result = new CiNative { Line = line, Content = Encoding.UTF8.GetString(this.Input, offset, this.CharOffset - 1 - offset) };
			NextToken();
			return result;
		}

		CiReturn ParseReturn()
		{
			CiReturn result = new CiReturn { Line = this.Line };
			NextToken();
			if (!See(CiToken.Semicolon))
				result.Value = ParseExpr();
			Expect(CiToken.Semicolon);
			return result;
		}

		CiSwitch ParseSwitch()
		{
			CiSwitch result = new CiSwitch { Line = this.Line };
			Expect(CiToken.Switch);
			result.Value = ParseParenthesized();
			Expect(CiToken.LeftBrace);
			CiCondCompletionStatement outerLoopOrSwitch = this.CurrentLoopOrSwitch;
			this.CurrentLoopOrSwitch = result;
			while (Eat(CiToken.Case)) {
				result.Cases.Add(new CiCase());
				CiCase kase = result.Cases[result.Cases.Count - 1];
				do {
					CiExpr expr = ParseExpr();
					if (See(CiToken.Id))
						expr = ParseVar(expr);
					kase.Values.Add(expr);
					Expect(CiToken.Colon);
				}
				while (Eat(CiToken.Case));
				if (See(CiToken.Default)) {
					ReportError("Please remove 'case' before 'default'");
					break;
				}
				while (!See(CiToken.EndOfFile)) {
					kase.Body.Add(ParseStatement());
					switch (this.CurrentToken) {
					case CiToken.Case:
					case CiToken.Default:
					case CiToken.RightBrace:
						break;
					default:
						continue;
					}
					break;
				}
			}
			if (result.Cases.Count == 0)
				ReportError("Switch with no cases");
			if (Eat(CiToken.Default)) {
				Expect(CiToken.Colon);
				do {
					if (See(CiToken.EndOfFile))
						break;
					result.DefaultBody.Add(ParseStatement());
				}
				while (!See(CiToken.RightBrace));
			}
			Expect(CiToken.RightBrace);
			this.CurrentLoopOrSwitch = outerLoopOrSwitch;
			return result;
		}

		CiThrow ParseThrow()
		{
			CiThrow result = new CiThrow { Line = this.Line };
			Expect(CiToken.Throw);
			result.Message = ParseExpr();
			Expect(CiToken.Semicolon);
			return result;
		}

		CiWhile ParseWhile()
		{
			CiWhile result = new CiWhile { Line = this.Line };
			Expect(CiToken.While);
			result.Cond = ParseParenthesized();
			ParseLoopBody(result);
			return result;
		}

		CiStatement ParseStatement()
		{
			switch (this.CurrentToken) {
			case CiToken.LeftBrace:
				return ParseBlock();
			case CiToken.Assert:
				return ParseAssert();
			case CiToken.Break:
				return ParseBreak();
			case CiToken.Const:
				return ParseConst();
			case CiToken.Continue:
				return ParseContinue();
			case CiToken.Do:
				return ParseDoWhile();
			case CiToken.For:
				return ParseFor();
			case CiToken.Foreach:
				return ParseForeach();
			case CiToken.If:
				return ParseIf();
			case CiToken.Lock_:
				return ParseLock();
			case CiToken.Native:
				return ParseNative();
			case CiToken.Return:
				return ParseReturn();
			case CiToken.Switch:
				return ParseSwitch();
			case CiToken.Throw:
				return ParseThrow();
			case CiToken.While:
				return ParseWhile();
			default:
				CiExpr expr = ParseAssign(true);
				Expect(CiToken.Semicolon);
				return expr;
			}
		}

		CiCallType ParseCallType()
		{
			switch (this.CurrentToken) {
			case CiToken.Static:
				NextToken();
				return CiCallType.Static;
			case CiToken.Abstract:
				NextToken();
				return CiCallType.Abstract;
			case CiToken.Virtual:
				NextToken();
				return CiCallType.Virtual;
			case CiToken.Override:
				NextToken();
				return CiCallType.Override;
			case CiToken.Sealed:
				NextToken();
				return CiCallType.Sealed;
			default:
				return CiCallType.Normal;
			}
		}

		void ParseMethod(CiMethod method)
		{
			method.IsMutator = Eat(CiToken.ExclamationMark);
			Expect(CiToken.LeftParenthesis);
			if (!See(CiToken.RightParenthesis)) {
				do {
					CiCodeDoc doc = ParseDoc();
					CiVar param = ParseVar(ParseType());
					param.Documentation = doc;
					AddSymbol(method.Parameters, param);
				}
				while (Eat(CiToken.Comma));
			}
			Expect(CiToken.RightParenthesis);
			method.Throws = Eat(CiToken.Throws);
			if (method.CallType == CiCallType.Abstract)
				Expect(CiToken.Semicolon);
			else if (See(CiToken.FatArrow))
				method.Body = ParseReturn();
			else if (Check(CiToken.LeftBrace))
				method.Body = ParseBlock();
		}

		static string CallTypeToString(CiCallType callType)
		{
			switch (callType) {
			case CiCallType.Static:
				return "static";
			case CiCallType.Normal:
				return "normal";
			case CiCallType.Abstract:
				return "abstract";
			case CiCallType.Virtual:
				return "virtual";
			case CiCallType.Override:
				return "override";
			case CiCallType.Sealed:
				return "sealed";
			default:
				throw new NotImplementedException();
			}
		}

		void ParseClass(CiCodeDoc doc, bool isPublic, CiCallType callType)
		{
			Expect(CiToken.Class);
			CiClass klass = new CiClass { Filename = this.Filename, Line = this.Line, Documentation = doc, IsPublic = isPublic, CallType = callType, Name = this.StringValue };
			if (Expect(CiToken.Id))
				AddSymbol(this.Program, klass);
			if (Eat(CiToken.Colon)) {
				klass.BaseClassName = this.StringValue;
				Expect(CiToken.Id);
			}
			Expect(CiToken.LeftBrace);
			while (!See(CiToken.RightBrace) && !See(CiToken.EndOfFile)) {
				doc = ParseDoc();
				CiVisibility visibility;
				switch (this.CurrentToken) {
				case CiToken.Internal:
					visibility = CiVisibility.Internal;
					NextToken();
					break;
				case CiToken.Protected:
					visibility = CiVisibility.Protected;
					NextToken();
					break;
				case CiToken.Public:
					visibility = CiVisibility.Public;
					NextToken();
					break;
				case CiToken.Semicolon:
					ReportError("Semicolon in class definition");
					NextToken();
					continue;
				default:
					visibility = CiVisibility.Private;
					break;
				}
				if (See(CiToken.Const)) {
					CiConst konst = ParseConst();
					konst.Documentation = doc;
					konst.Visibility = visibility;
					AddSymbol(klass, konst);
					continue;
				}
				callType = ParseCallType();
				CiExpr type = Eat(CiToken.Void) ? this.Program.System.VoidType : ParseType();
				if (See(CiToken.LeftBrace) && type is CiCallExpr call) {
					if (call.Method.Name != klass.Name)
						ReportError("Method with no return type");
					else {
						if (klass.CallType == CiCallType.Static)
							ReportError("Constructor in a static class");
						if (callType != CiCallType.Normal)
							ReportError($"Constructor cannot be {CallTypeToString(callType)}");
						if (call.Arguments.Count != 0)
							ReportError("Constructor parameters not supported");
						if (klass.Constructor != null)
							ReportError($"Duplicate constructor, already defined in line {klass.Constructor.Line}");
					}
					if (visibility == CiVisibility.Private)
						visibility = CiVisibility.Internal;
					klass.Constructor = new CiMethodBase { Line = call.Line, Documentation = doc, Visibility = visibility, Parent = klass, Type = this.Program.System.VoidType, Name = klass.Name, IsMutator = true, Body = ParseBlock() };
					continue;
				}
				int line = this.Line;
				string name = this.StringValue;
				if (!Expect(CiToken.Id))
					continue;
				if (See(CiToken.LeftParenthesis) || See(CiToken.ExclamationMark)) {
					if (callType == CiCallType.Static || klass.CallType == CiCallType.Abstract) {
					}
					else if (klass.CallType == CiCallType.Static)
						ReportError("Only static methods allowed in a static class");
					else if (callType == CiCallType.Abstract)
						ReportError("Abstract methods allowed only in an abstract class");
					else if (klass.CallType == CiCallType.Sealed && callType == CiCallType.Virtual)
						ReportError("Virtual methods disallowed in a sealed class");
					if (visibility == CiVisibility.Private && callType != CiCallType.Static && callType != CiCallType.Normal)
						ReportError($"{CallTypeToString(callType)} method cannot be private");
					CiMethod method = new CiMethod { Line = line, Documentation = doc, Visibility = visibility, CallType = callType, TypeExpr = type, Name = name };
					AddSymbol(klass, method);
					method.Parameters.Parent = klass;
					ParseMethod(method);
					continue;
				}
				if (visibility == CiVisibility.Public)
					ReportError("Field cannot be public");
				if (callType != CiCallType.Normal)
					ReportError($"Field cannot be {CallTypeToString(callType)}");
				if (type == this.Program.System.VoidType)
					ReportError("Field cannot be void");
				CiField field = new CiField { Line = line, Documentation = doc, Visibility = visibility, TypeExpr = type, Name = name, Value = ParseInitializer() };
				AddSymbol(klass, field);
				Expect(CiToken.Semicolon);
			}
			Expect(CiToken.RightBrace);
		}

		void ParseEnum(CiCodeDoc doc, bool isPublic)
		{
			Expect(CiToken.Enum);
			bool flags = Eat(CiToken.Asterisk);
			CiEnum enu = flags ? new CiEnumFlags() : new CiEnum();
			enu.Filename = this.Filename;
			enu.Line = this.Line;
			enu.Documentation = doc;
			enu.IsPublic = isPublic;
			enu.Name = this.StringValue;
			if (Expect(CiToken.Id))
				AddSymbol(this.Program, enu);
			Expect(CiToken.LeftBrace);
			do {
				CiConst konst = new CiConst { Visibility = CiVisibility.Public, Documentation = ParseDoc(), Line = this.Line, Name = this.StringValue, Type = enu };
				Expect(CiToken.Id);
				if (Eat(CiToken.Assign))
					konst.Value = ParseExpr();
				else if (flags)
					ReportError("enum* symbol must be assigned a value");
				AddSymbol(enu, konst);
			}
			while (Eat(CiToken.Comma));
			Expect(CiToken.RightBrace);
		}

		public void Parse(string filename, byte[] input, int inputLength)
		{
			Open(filename, input, inputLength);
			while (!See(CiToken.EndOfFile)) {
				CiCodeDoc doc = ParseDoc();
				bool isPublic = Eat(CiToken.Public);
				switch (this.CurrentToken) {
				case CiToken.Class:
					ParseClass(doc, isPublic, CiCallType.Normal);
					break;
				case CiToken.Static:
				case CiToken.Abstract:
				case CiToken.Sealed:
					ParseClass(doc, isPublic, ParseCallType());
					break;
				case CiToken.Enum:
					ParseEnum(doc, isPublic);
					break;
				case CiToken.Native:
					this.Program.TopLevelNatives.Add(ParseNative().Content);
					break;
				default:
					ReportError("Expected class or enum");
					NextToken();
					break;
				}
			}
		}
	}

	public abstract class CiSema : CiVisitor
	{

		protected CiProgram Program;

		protected CiMethodBase CurrentMethod;

		protected CiScope CurrentScope;

		protected CiType Poison = new CiType { Name = "poison" };

		protected override CiContainerType GetCurrentContainer() => this.CurrentScope.GetContainer();

		protected CiType PoisonError(CiStatement statement, string message)
		{
			ReportError(statement, message);
			return this.Poison;
		}

		protected void ResolveBase(CiClass klass)
		{
			if (klass.HasBaseClass()) {
				this.CurrentScope = klass;
				if (this.Program.TryLookup(klass.BaseClassName) is CiClass baseClass) {
					if (klass.IsPublic && !baseClass.IsPublic)
						ReportError(klass, "Public class cannot derive from an internal class");
					klass.Parent = baseClass;
				}
				else
					ReportError(klass, $"Base class {klass.BaseClassName} not found");
			}
			this.Program.Classes.Add(klass);
		}

		protected void CheckBaseCycle(CiClass klass)
		{
			CiSymbol hare = klass;
			CiSymbol tortoise = klass;
			do {
				hare = hare.Parent;
				if (hare == null)
					return;
				hare = hare.Parent;
				if (hare == null)
					return;
				tortoise = tortoise.Parent;
			}
			while (tortoise != hare);
			this.CurrentScope = klass;
			ReportError(klass, $"Circular inheritance for class {klass.Name}");
		}

		protected static void TakePtr(CiExpr expr)
		{
			if (expr.Type is CiArrayStorageType arrayStg)
				arrayStg.PtrTaken = true;
		}

		protected bool Coerce(CiExpr expr, CiType type)
		{
			if (expr == this.Poison)
				return false;
			if (!type.IsAssignableFrom(expr.Type)) {
				ReportError(expr, $"Cannot coerce {expr.Type} to {type}");
				return false;
			}
			if (expr is CiPrefixExpr prefix && prefix.Op == CiToken.New && !(type is CiDynamicPtrType)) {
				CiDynamicPtrType newType = (CiDynamicPtrType) expr.Type;
				string kind = newType.Class.Id == CiId.ArrayPtrClass ? "array" : "object";
				ReportError(expr, $"Dynamically allocated {kind} must be assigned to a {expr.Type} reference");
				return false;
			}
			TakePtr(expr);
			return true;
		}

		void VisitAggregateInitializer(CiAggregateInitializer expr)
		{
			List<CiExpr> items = expr.Items;
			for (int i = 0; i < items.Count; i++)
				items[i] = Resolve(items[i]);
		}

		protected static CiRangeType Union(CiRangeType left, CiRangeType right)
		{
			if (right == null)
				return left;
			if (right.Min < left.Min) {
				if (right.Max >= left.Max)
					return right;
				return CiRangeType.New(right.Min, left.Max);
			}
			if (right.Max > left.Max)
				return CiRangeType.New(left.Min, right.Max);
			return left;
		}

		CiType TryGetPtr(CiType type)
		{
			if (type.Id == CiId.StringStorageType)
				return this.Program.System.StringPtrType;
			if (type is CiStorageType storage)
				return new CiReadWriteClassType { Class = storage.Class.Id == CiId.ArrayStorageClass ? this.Program.System.ArrayPtrClass : storage.Class, TypeArg0 = storage.TypeArg0, TypeArg1 = storage.TypeArg1 };
			return type;
		}

		protected CiType GetCommonType(CiExpr left, CiExpr right)
		{
			if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange)
				return Union(leftRange, rightRange);
			CiType ptr = TryGetPtr(left.Type);
			if (ptr.IsAssignableFrom(right.Type))
				return ptr;
			ptr = TryGetPtr(right.Type);
			if (ptr.IsAssignableFrom(left.Type))
				return ptr;
			return PoisonError(left, $"Incompatible types: {left.Type} and {right.Type}");
		}

		protected CiType GetIntegerType(CiExpr left, CiExpr right)
		{
			CiType type = this.Program.System.PromoteIntegerTypes(left.Type, right.Type);
			Coerce(left, type);
			Coerce(right, type);
			return type;
		}

		protected CiIntegerType GetShiftType(CiExpr left, CiExpr right)
		{
			CiIntegerType intType = this.Program.System.IntType;
			Coerce(right, intType);
			if (left.Type.Id == CiId.LongType) {
				CiIntegerType longType = (CiIntegerType) left.Type;
				return longType;
			}
			Coerce(left, intType);
			return intType;
		}

		protected CiType GetNumericType(CiExpr left, CiExpr right)
		{
			CiType type = this.Program.System.PromoteNumericTypes(left.Type, right.Type);
			Coerce(left, type);
			Coerce(right, type);
			return type;
		}

		protected static int SaturatedShiftRight(int a, int b) => a >> (b >= 31 || b < 0 ? 31 : b);

		protected static CiRangeType UnsignedAnd(CiRangeType left, CiRangeType right)
		{
			int leftVariableBits = left.GetVariableBits();
			int rightVariableBits = right.GetVariableBits();
			int min = left.Min & right.Min & ~CiRangeType.GetMask(~left.Min & ~right.Min & (leftVariableBits | rightVariableBits));
			int max = (left.Max | leftVariableBits) & (right.Max | rightVariableBits);
			if (max > left.Max)
				max = left.Max;
			if (max > right.Max)
				max = right.Max;
			if (min > max)
				return CiRangeType.New(max, min);
			return CiRangeType.New(min, max);
		}

		protected static CiRangeType UnsignedOr(CiRangeType left, CiRangeType right)
		{
			int leftVariableBits = left.GetVariableBits();
			int rightVariableBits = right.GetVariableBits();
			int min = (left.Min & ~leftVariableBits) | (right.Min & ~rightVariableBits);
			int max = left.Max | right.Max | CiRangeType.GetMask(left.Max & right.Max & CiRangeType.GetMask(leftVariableBits | rightVariableBits));
			if (min < left.Min)
				min = left.Min;
			if (min < right.Min)
				min = right.Min;
			if (min > max)
				return CiRangeType.New(max, min);
			return CiRangeType.New(min, max);
		}

		protected static CiRangeType UnsignedXor(CiRangeType left, CiRangeType right)
		{
			int variableBits = left.GetVariableBits() | right.GetVariableBits();
			int min = (left.Min ^ right.Min) & ~variableBits;
			int max = (left.Max ^ right.Max) | variableBits;
			if (min > max)
				return CiRangeType.New(max, min);
			return CiRangeType.New(min, max);
		}

		protected static CiRangeType NewRangeType(int a, int b, int c, int d)
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
			return CiRangeType.New(a <= c ? a : c, b >= d ? b : d);
		}

		protected bool IsEnumOp(CiExpr left, CiExpr right)
		{
			if (left.Type is CiEnum) {
				if (left.Type.Id != CiId.BoolType && !(left.Type is CiEnumFlags))
					ReportError(left, $"Define flags enumeration as: enum* {left.Type}");
				Coerce(right, left.Type);
				return true;
			}
			return false;
		}

		protected CiLiteralLong ToLiteralLong(CiExpr expr, long value) => this.Program.System.NewLiteralLong(value, expr.Line);

		protected CiLiteralDouble ToLiteralDouble(CiExpr expr, double value) => new CiLiteralDouble { Line = expr.Line, Type = this.Program.System.DoubleType, Value = value };

		protected void CheckLValue(CiExpr expr)
		{
			if (expr is CiSymbolReference symbol) {
				switch (symbol.Symbol) {
				case CiVar def:
					def.IsAssigned = true;
					switch (symbol.Symbol.Parent) {
					case CiFor forLoop:
						forLoop.IsRange = false;
						break;
					case CiForeach _:
						ReportError(expr, "Cannot assign a foreach iteration variable");
						break;
					default:
						break;
					}
					for (CiScope scope = this.CurrentScope; !(scope is CiClass); scope = scope.Parent) {
						if (scope is CiFor forLoop && forLoop.IsRange && forLoop.Cond is CiBinaryExpr binaryCond && binaryCond.Right.IsReferenceTo(symbol.Symbol))
							forLoop.IsRange = false;
					}
					break;
				case CiField _:
					if (symbol.Left == null) {
						if (!this.CurrentMethod.IsMutator)
							ReportError(expr, "Cannot modify field in a non-mutating method");
					}
					else {
						switch (symbol.Left.Type) {
						case CiStorageType _:
							break;
						case CiReadWriteClassType _:
							break;
						case CiClassType _:
							ReportError(expr, "Cannot modify field through a read-only reference");
							break;
						}
					}
					break;
				default:
					ReportError(expr, "Cannot modify this");
					break;
				}
			}
		}

		protected CiInterpolatedString ToInterpolatedString(CiExpr expr)
		{
			if (expr is CiInterpolatedString interpolated)
				return interpolated;
			CiInterpolatedString result = new CiInterpolatedString { Line = expr.Line, Type = this.Program.System.StringStorageType };
			if (expr is CiLiteral literal)
				result.Suffix = literal.GetLiteralString();
			else {
				result.AddPart("", expr);
				result.Suffix = "";
			}
			return result;
		}

		protected void CheckComparison(CiExpr left, CiExpr right)
		{
			CiType doubleType = this.Program.System.DoubleType;
			Coerce(left, doubleType);
			Coerce(right, doubleType);
		}

		protected CiType EvalType(CiClassType generic, CiType type)
		{
			if (type.Id == CiId.TypeParam0)
				return generic.TypeArg0;
			if (type.Id == CiId.TypeParam0NotFinal)
				return generic.TypeArg0.IsFinal() ? null : generic.TypeArg0;
			if (type is CiReadWriteClassType array && array.IsArray() && array.GetElementType().Id == CiId.TypeParam0)
				return new CiReadWriteClassType { Class = this.Program.System.ArrayPtrClass, TypeArg0 = generic.TypeArg0 };
			return type;
		}

		protected bool CanCall(CiExpr obj, CiMethod method, List<CiExpr> arguments)
		{
			CiVar param = method.Parameters.FirstParameter();
			foreach (CiExpr arg in arguments) {
				if (param == null)
					return false;
				CiType type = param.Type;
				if (obj != null && obj.Type is CiClassType generic)
					type = EvalType(generic, type);
				if (!type.IsAssignableFrom(arg.Type))
					return false;
				param = param.NextParameter();
			}
			return param == null || param.Value != null;
		}

		protected void OpenScope(CiScope scope)
		{
			scope.Parent = this.CurrentScope;
			this.CurrentScope = scope;
		}

		protected void CloseScope()
		{
			this.CurrentScope = this.CurrentScope.Parent;
		}

		protected abstract CiExpr VisitInterpolatedString(CiInterpolatedString expr);

		protected abstract CiExpr VisitSymbolReference(CiSymbolReference expr);

		protected abstract CiExpr VisitPrefixExpr(CiPrefixExpr expr);

		void VisitPostfixExpr(CiPostfixExpr expr)
		{
			expr.Inner = Resolve(expr.Inner);
			switch (expr.Op) {
			case CiToken.Increment:
			case CiToken.Decrement:
				CheckLValue(expr.Inner);
				Coerce(expr.Inner, this.Program.System.DoubleType);
				expr.Type = expr.Inner.Type;
				break;
			default:
				ReportError(expr, $"Unexpected {CiLexer.TokenToString(expr.Op)}");
				break;
			}
		}

		protected abstract CiExpr VisitBinaryExpr(CiBinaryExpr expr);

		CiExpr VisitSelectExpr(CiSelectExpr expr)
		{
			CiExpr cond = ResolveBool(expr.Cond);
			CiExpr onTrue = Resolve(expr.OnTrue);
			CiExpr onFalse = Resolve(expr.OnFalse);
			CiType type = GetCommonType(onTrue, onFalse);
			Coerce(onTrue, type);
			Coerce(onFalse, type);
			if (cond is CiLiteralTrue)
				return onTrue;
			if (cond is CiLiteralFalse)
				return onFalse;
			return new CiSelectExpr { Line = expr.Line, Cond = cond, OnTrue = onTrue, OnFalse = onFalse, Type = type };
		}

		protected abstract CiExpr VisitCallExpr(CiCallExpr expr);

		protected abstract void VisitVar(CiVar expr);

		protected CiExpr Resolve(CiExpr expr)
		{
			switch (expr) {
			case CiAggregateInitializer aggregate:
				VisitAggregateInitializer(aggregate);
				return expr;
			case CiLiteral _:
				return expr;
			case CiInterpolatedString interpolated:
				return VisitInterpolatedString(interpolated);
			case CiSymbolReference symbol:
				return VisitSymbolReference(symbol);
			case CiPrefixExpr prefix:
				return VisitPrefixExpr(prefix);
			case CiPostfixExpr postfix:
				VisitPostfixExpr(postfix);
				return expr;
			case CiBinaryExpr binary:
				return VisitBinaryExpr(binary);
			case CiSelectExpr select:
				return VisitSelectExpr(select);
			case CiCallExpr call:
				return VisitCallExpr(call);
			case CiLambdaExpr _:
				ReportError(expr, "Unexpected lambda expression");
				return expr;
			case CiVar def:
				VisitVar(def);
				return expr;
			default:
				throw new NotImplementedException();
			}
		}

		public override void VisitExpr(CiExpr statement)
		{
			Resolve(statement);
		}

		protected CiExpr ResolveBool(CiExpr expr)
		{
			expr = Resolve(expr);
			Coerce(expr, this.Program.System.BoolType);
			return expr;
		}

		public override void VisitAssert(CiAssert statement)
		{
			statement.Cond = ResolveBool(statement.Cond);
			if (statement.Message != null) {
				statement.Message = Resolve(statement.Message);
				if (!(statement.Message.Type is CiStringType))
					ReportError(statement, "The second argument of 'assert' must be a string");
			}
		}

		protected bool ResolveStatements(List<CiStatement> statements)
		{
			bool reachable = true;
			foreach (CiStatement statement in statements) {
				statement.AcceptStatement(this);
				if (!reachable) {
					ReportError(statement, "Unreachable statement");
					return false;
				}
				reachable = statement.CompletesNormally();
			}
			return reachable;
		}

		public override void VisitBlock(CiBlock statement)
		{
			OpenScope(statement);
			statement.SetCompletesNormally(ResolveStatements(statement.Statements));
			CloseScope();
		}

		public override void VisitBreak(CiBreak statement)
		{
			statement.LoopOrSwitch.SetCompletesNormally(true);
		}

		public override void VisitContinue(CiContinue statement)
		{
		}

		protected void ResolveLoopCond(CiLoop statement)
		{
			if (statement.Cond != null) {
				statement.Cond = ResolveBool(statement.Cond);
				statement.SetCompletesNormally(!(statement.Cond is CiLiteralTrue));
			}
			else
				statement.SetCompletesNormally(false);
		}

		public override void VisitDoWhile(CiDoWhile statement)
		{
			OpenScope(statement);
			ResolveLoopCond(statement);
			statement.Body.AcceptStatement(this);
			CloseScope();
		}

		public override void VisitIf(CiIf statement)
		{
			statement.Cond = ResolveBool(statement.Cond);
			statement.OnTrue.AcceptStatement(this);
			if (statement.OnFalse != null) {
				statement.OnFalse.AcceptStatement(this);
				statement.SetCompletesNormally(statement.OnTrue.CompletesNormally() || statement.OnFalse.CompletesNormally());
			}
			else
				statement.SetCompletesNormally(true);
		}

		public override void VisitLock(CiLock statement)
		{
			statement.Lock = Resolve(statement.Lock);
			Coerce(statement.Lock, this.Program.System.LockPtrType);
			statement.Body.AcceptStatement(this);
		}

		public override void VisitNative(CiNative statement)
		{
		}

		public override void VisitThrow(CiThrow statement)
		{
			if (!this.CurrentMethod.Throws)
				ReportError(statement, "'throw' in a method not marked 'throws'");
			statement.Message = Resolve(statement.Message);
			if (!(statement.Message.Type is CiStringType))
				ReportError(statement, "The argument of 'throw' must be a string");
		}

		public override void VisitWhile(CiWhile statement)
		{
			OpenScope(statement);
			ResolveLoopCond(statement);
			statement.Body.AcceptStatement(this);
			CloseScope();
		}

		protected void ExpectNoPtrModifier(CiExpr expr, CiToken ptrModifier)
		{
			if (ptrModifier != CiToken.EndOfFile)
				ReportError(expr, $"Unexpected {CiLexer.TokenToString(ptrModifier)} on a non-reference type");
		}

		protected CiExpr FoldConst(CiExpr expr)
		{
			expr = Resolve(expr);
			if (expr is CiLiteral || expr.IsConstEnum())
				return expr;
			ReportError(expr, "Expected constant value");
			return expr;
		}

		protected static CiClassType CreateClassPtr(CiClass klass, CiToken ptrModifier)
		{
			CiClassType ptr;
			switch (ptrModifier) {
			case CiToken.EndOfFile:
				ptr = new CiClassType();
				break;
			case CiToken.ExclamationMark:
				ptr = new CiReadWriteClassType();
				break;
			case CiToken.Hash:
				ptr = new CiDynamicPtrType();
				break;
			default:
				throw new NotImplementedException();
			}
			ptr.Class = klass;
			return ptr;
		}

		static void MarkMethodLive(CiMethodBase method)
		{
			if (method.IsLive)
				return;
			method.IsLive = true;
			foreach (CiMethod called in method.Calls)
				MarkMethodLive(called);
		}

		protected static void MarkClassLive(CiClass klass)
		{
			if (!klass.IsPublic)
				return;
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiMethod method && (method.Visibility == CiVisibility.Public || method.Visibility == CiVisibility.Protected))
					MarkMethodLive(method);
			}
			if (klass.Constructor != null)
				MarkMethodLive(klass.Constructor);
		}
	}
}
