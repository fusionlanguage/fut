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
		StringClass,
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

	public abstract class CiVisitor
	{

		public abstract void VisitAggregateInitializer(CiAggregateInitializer expr);

		public abstract void VisitVar(CiVar expr);

		public abstract void VisitLiteralLong(long value);

		public abstract void VisitLiteralChar(int value);

		public abstract void VisitLiteralDouble(double value);

		public abstract void VisitLiteralString(string value);

		public abstract void VisitLiteralNull();

		public abstract void VisitLiteralFalse();

		public abstract void VisitLiteralTrue();

		public abstract CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent);

		public abstract CiExpr VisitSymbolReference(CiSymbolReference expr, CiPriority parent);

		public abstract CiExpr VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent);

		public abstract CiExpr VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent);

		public abstract CiExpr VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent);

		public abstract CiExpr VisitSelectExpr(CiSelectExpr expr, CiPriority parent);

		public abstract CiExpr VisitCallExpr(CiCallExpr expr, CiPriority parent);

		public abstract void VisitLambdaExpr(CiLambdaExpr expr);

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

		public virtual bool IsIndexing() => false;

		public virtual bool IsLiteralZero() => false;

		public virtual bool IsConstEnum() => false;

		public virtual int IntValue()
		{
			throw new NotImplementedException();
		}

		public virtual CiExpr Accept(CiVisitor visitor, CiPriority parent)
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

	public abstract class CiScope : CiSymbol
	{

		readonly Dictionary<string, CiSymbol> Dict = new Dictionary<string, CiSymbol>();

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

		public CiSymbol TryShallowLookup(string name) => this.Dict.ContainsKey(name) ? this.Dict[name] : null;

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

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitAggregateInitializer(this);
			return this;
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

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralFalse();
			return this;
		}

		public override string ToString() => "false";
	}

	public class CiLiteralTrue : CiLiteral
	{

		public override bool IsDefaultValue() => false;

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralTrue();
			return this;
		}

		public override string ToString() => "true";
	}

	public class CiLiteralNull : CiLiteral
	{

		public override bool IsDefaultValue() => true;

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralNull();
			return this;
		}

		public override string ToString() => "null";
	}

	public class CiLiteralLong : CiLiteral
	{

		internal long Value;

		public override bool IsLiteralZero() => this.Value == 0;

		public override int IntValue() => (int) this.Value;

		public override bool IsDefaultValue() => this.Value == 0;

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralLong(this.Value);
			return this;
		}

		public override string GetLiteralString() => $"{this.Value}";

		public override string ToString() => $"{this.Value}";
	}

	public class CiLiteralString : CiLiteral
	{

		internal string Value;

		public override bool IsDefaultValue() => false;

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralString(this.Value);
			return this;
		}

		public override string GetLiteralString() => this.Value;

		public override string ToString() => '"' + this.Value + '"';

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

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitInterpolatedString(this, parent);
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

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitSymbolReference(this, parent);

		public override bool IsReferenceTo(CiSymbol symbol) => this.Symbol == symbol;
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

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitPrefixExpr(this, parent);
	}

	public class CiPostfixExpr : CiUnaryExpr
	{

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitPostfixExpr(this, parent);
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

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitBinaryExpr(this, parent);

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
	}

	public class CiSelectExpr : CiExpr
	{

		internal CiExpr Cond;

		internal CiExpr OnTrue;

		internal CiExpr OnFalse;

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitSelectExpr(this, parent);
	}

	public class CiCallExpr : CiExpr
	{

		internal CiSymbolReference Method;

		internal readonly List<CiExpr> Arguments = new List<CiExpr>();

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent) => visitor.VisitCallExpr(this, parent);
	}

	public class CiLambdaExpr : CiScope
	{

		internal CiExpr Body;

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLambdaExpr(this);
			return this;
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
				return block.Statements.Any(statement => HasBreak(statement));
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

		public virtual CiType GetPtrOrSelf() => this;
	}

	public abstract class CiNumericType : CiType
	{
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

		public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitVar(this);
			return this;
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

		internal bool Throws;

		internal CiStatement Body;

		internal bool IsLive = false;

		internal readonly HashSet<CiMethod> Calls = new HashSet<CiMethod>();
	}

	public class CiMethod : CiMethodBase
	{

		internal CiCallType CallType;

		internal bool IsMutator = false;

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

	public abstract class CiParserBase : CiLexer
	{

		string XcrementParent = null;

		CiLoop CurrentLoop = null;

		CiCondCompletionStatement CurrentLoopOrSwitch = null;

		protected abstract string DocParseText();

		void DocParsePara(CiDocPara para)
		{
			for (;;) {
				if (DocSee(CiDocToken.Char))
					para.Children.Add(new CiDocText { Text = DocParseText() });
				else if (DocEat(CiDocToken.CodeDelimiter)) {
					para.Children.Add(new CiDocCode { Text = DocParseText() });
					if (!DocEat(CiDocToken.CodeDelimiter))
						ReportError("Unterminated code in documentation comment");
				}
				else
					break;
			}
			DocEat(CiDocToken.Para);
		}

		CiDocBlock DocParseBlock()
		{
			if (DocEat(CiDocToken.Bullet)) {
				CiDocList list = new CiDocList();
				do {
					list.Items.Add(new CiDocPara());
					DocParsePara(list.Items[list.Items.Count - 1]);
				}
				while (DocEat(CiDocToken.Bullet));
				DocEat(CiDocToken.Para);
				return list;
			}
			CiDocPara para = new CiDocPara();
			DocParsePara(para);
			return para;
		}

		CiCodeDoc ParseCodeDoc()
		{
			DocStartLexing();
			CiCodeDoc doc = new CiCodeDoc();
			DocParsePara(doc.Summary);
			if (DocEat(CiDocToken.Period)) {
				DocEat(CiDocToken.Para);
				while (!DocSee(CiDocToken.EndOfFile))
					doc.Details.Add(DocParseBlock());
			}
			return doc;
		}

		protected CiCodeDoc ParseDoc() => See(CiToken.DocComment) ? ParseCodeDoc() : null;

		protected CiExpr ParseSymbolReference(CiExpr left)
		{
			CiExpr result = new CiSymbolReference { Line = this.Line, Left = left, Name = this.StringValue };
			NextToken();
			return result;
		}

		protected void CheckXcrementParent()
		{
			if (this.XcrementParent != null) {
				string op = See(CiToken.Increment) ? "++" : "--";
				ReportError($"{op} not allowed on the right side of {this.XcrementParent}");
			}
		}

		protected CiExpr ParseParenthesized()
		{
			Expect(CiToken.LeftParenthesis);
			CiExpr result = ParseExpr();
			Expect(CiToken.RightParenthesis);
			return result;
		}

		protected bool SeeDigit()
		{
			int c = PeekChar();
			return c >= '0' && c <= '9';
		}

		protected abstract CiExpr ParsePrimaryExpr();

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

		protected CiExpr ParseExpr()
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

		protected CiExpr ParseType()
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

		protected void ParseCollection(List<CiExpr> result, CiToken closing)
		{
			if (!See(closing)) {
				do
					result.Add(ParseExpr());
				while (Eat(CiToken.Comma));
			}
			ExpectOrSkip(closing);
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

		protected CiAggregateInitializer ParseObjectLiteral()
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

		protected CiExpr ParseInitializer()
		{
			if (!Eat(CiToken.Assign))
				return null;
			if (Eat(CiToken.LeftBrace))
				return ParseObjectLiteral();
			return ParseExpr();
		}

		protected void AddSymbol(CiScope scope, CiSymbol symbol)
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

		protected CiConst ParseConst()
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

		protected CiBlock ParseBlock()
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
			this.CurrentLoopOrSwitch = this.CurrentLoop = loop;
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

		protected CiNative ParseNative()
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

		protected CiCallType ParseCallType()
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

		protected void ParseMethod(CiMethod method)
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

		protected static string CallTypeToString(CiCallType callType)
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
	}
}
