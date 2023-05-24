// Generated automatically with "cito". Do not edit.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
		When,
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

	public abstract class CiLexer
	{

		protected byte[] Input;

		int InputLength;

		int NextOffset;

		protected int CharOffset;

		int NextChar;

		protected string Filename;

		protected int Line;

		protected int Column;

		protected int TokenColumn;

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
			this.Column = 1;
			FillNextChar();
			if (this.NextChar == 65279)
				FillNextChar();
			NextToken();
		}

		protected abstract void ReportError(string message);

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
				this.Column++;
				break;
			case '\n':
				this.Line++;
				this.Column = 1;
				this.AtLineStart = true;
				break;
			default:
				this.Column++;
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
			for (int offset = this.CharOffset;; ReadCharLiteral()) {
				switch (PeekChar()) {
				case -1:
					ReportError("Unterminated string literal");
					return CiToken.EndOfFile;
				case '\n':
					ReportError("Unterminated string literal");
					this.StringValue = "";
					return CiToken.LiteralString;
				case '"':
					{
						int endOffset = this.CharOffset;
						ReadChar();
						this.StringValue = Encoding.UTF8.GetString(this.Input, offset, endOffset - offset);
					}
					return CiToken.LiteralString;
				case '{':
					if (interpolated) {
						int endOffset = this.CharOffset;
						ReadChar();
						if (PeekChar() != '{') {
							this.StringValue = Encoding.UTF8.GetString(this.Input, offset, endOffset - offset);
							return CiToken.InterpolatedString;
						}
					}
					break;
				default:
					break;
				}
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
				this.TokenColumn = this.Column;
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
					case "when":
						return CiToken.When;
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
			case CiToken.When:
				return "'when'";
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
		SelectCond,
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
		SByteRange,
		ByteRange,
		ShortRange,
		UShortRange,
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
		SortedSetClass,
		DictionaryClass,
		SortedDictionaryClass,
		OrderedDictionaryClass,
		TextWriterClass,
		StringWriterClass,
		RegexOptionsEnum,
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
		EnumFromInt,
		EnumHasFlag,
		IntTryParse,
		LongTryParse,
		DoubleTryParse,
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
		ListAddRange,
		ListAll,
		ListAny,
		ListClear,
		ListContains,
		ListCopyTo,
		ListCount,
		ListIndexOf,
		ListInsert,
		ListLast,
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
		SortedSetAdd,
		SortedSetClear,
		SortedSetContains,
		SortedSetCount,
		SortedSetRemove,
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
		TextWriterWrite,
		TextWriterWriteChar,
		TextWriterWriteCodePoint,
		TextWriterWriteLine,
		ConsoleWrite,
		ConsoleWriteLine,
		StringWriterClear,
		StringWriterToString,
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
		MathAbs,
		MathCeiling,
		MathClamp,
		MathFusedMultiplyAdd,
		MathIsFinite,
		MathIsInfinity,
		MathIsNaN,
		MathLog2,
		MathMaxInt,
		MathMaxDouble,
		MathMinInt,
		MathMinDouble,
		MathRound,
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

		protected void VisitOptionalStatement(CiStatement statement)
		{
			if (statement != null)
				statement.AcceptStatement(this);
		}

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

		public abstract void VisitLiteralNull();

		public abstract void VisitLiteralFalse();

		public abstract void VisitLiteralTrue();

		public abstract void VisitLiteralLong(long value);

		public abstract void VisitLiteralChar(int value);

		public abstract void VisitLiteralDouble(double value);

		public abstract void VisitLiteralString(string value);

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

		public virtual void Accept(CiVisitor visitor, CiPriority parent)
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

		public CiSymbol TryLookup(string name, bool global)
		{
			for (CiScope scope = this; scope != null && (global || !(scope is CiProgram || scope is CiSystem)); scope = scope.Parent) {
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

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

	public class CiLiteralNull : CiLiteral
	{

		public override bool IsDefaultValue() => true;

		public override void Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralNull();
		}

		public override string ToString() => "null";
	}

	public class CiLiteralFalse : CiLiteral
	{

		public override bool IsDefaultValue() => true;

		public override void Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralFalse();
		}

		public override string ToString() => "false";
	}

	public class CiLiteralTrue : CiLiteral
	{

		public override bool IsDefaultValue() => false;

		public override void Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralTrue();
		}

		public override string ToString() => "true";
	}

	public class CiLiteralLong : CiLiteral
	{

		internal long Value;

		public override bool IsLiteralZero() => this.Value == 0;

		public override int IntValue() => (int) this.Value;

		public override bool IsDefaultValue() => this.Value == 0;

		public override void Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralLong(this.Value);
		}

		public override string GetLiteralString() => $"{this.Value}";

		public override string ToString() => $"{this.Value}";
	}

	public class CiLiteralChar : CiLiteralLong
	{

		public static CiLiteralChar New(int value, int line) => new CiLiteralChar { Line = line, Type = CiRangeType.New(value, value), Value = value };

		public override void Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralChar((int) this.Value);
		}
	}

	public class CiLiteralDouble : CiLiteral
	{

		internal double Value;

		public override bool IsDefaultValue() => this.Value == 0 && 1.0f / this.Value > 0;

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

		public int GetAsciiAt(int i)
		{
			bool escaped = false;
			foreach (int c in this.Value) {
				if (c < 0 || c > 127)
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
				return c >= 0 && c <= 127 ? c : -1;
			case 2:
				return this.Value[0] == '\\' ? CiLexer.GetEscapedChar(this.Value[1]) : -1;
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
			CiInterpolatedPart part = this.Parts[^1];
			part.Prefix = prefix;
			part.Argument = arg;
			part.WidthExpr = widthExpr;
			part.Format = format;
			part.Precision = precision;
		}

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

		public override void Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitPrefixExpr(this, parent);
		}
	}

	public class CiPostfixExpr : CiUnaryExpr
	{

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

		public override void Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitSelectExpr(this, parent);
		}

		public override string ToString() => $"({this.Cond} ? {this.OnTrue} : {this.OnFalse})";
	}

	public class CiCallExpr : CiExpr
	{

		internal CiSymbolReference Method;

		internal readonly List<CiExpr> Arguments = new List<CiExpr>();

		public override void Accept(CiVisitor visitor, CiPriority parent)
		{
			visitor.VisitCallExpr(this, parent);
		}
	}

	public class CiLambdaExpr : CiScope
	{

		internal CiExpr Body;

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

		public bool HasWhen() => this.Cases.Any(kase => kase.Values.Any(value => value is CiBinaryExpr when1 && when1.Op == CiToken.When));

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

		internal bool Nullable = false;

		public virtual string GetArraySuffix() => "";

		public virtual bool IsAssignableFrom(CiType right) => this == right;

		public virtual bool EqualsType(CiType right) => this == right;

		public virtual bool IsArray() => false;

		public virtual bool IsFinal() => false;

		public virtual CiType GetBaseType() => this;

		public virtual CiType GetStorageType() => this;

		public CiClassType AsClassType()
		{
			CiClassType klass = (CiClassType) this;
			return klass;
		}
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

		static void AddMinMaxValue(CiRangeType target, string name, int value)
		{
			CiRangeType type = target.Min == target.Max ? target : new CiRangeType { Min = value, Max = value };
			target.Add(new CiConst { Visibility = CiVisibility.Public, Name = name, Value = new CiLiteralLong { Type = type, Value = value }, VisitStatus = CiVisitStatus.Done });
		}

		public static CiRangeType New(int min, int max)
		{
			Debug.Assert(min <= max);
			CiRangeType result = new CiRangeType { Id = min >= 0 && max <= 255 ? CiId.ByteRange : min >= -128 && max <= 127 ? CiId.SByteRange : min >= -32768 && max <= 32767 ? CiId.ShortRange : min >= 0 && max <= 65535 ? CiId.UShortRange : CiId.IntType, Min = min, Max = max };
			AddMinMaxValue(result, "MinValue", min);
			AddMinMaxValue(result, "MaxValue", max);
			return result;
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

	public abstract class CiMember : CiNamedValue
	{
		protected CiMember()
		{
		}

		internal CiVisibility Visibility;

		public abstract bool IsStatic();
	}

	public class CiVar : CiNamedValue
	{

		internal bool IsAssigned = false;

		public static CiVar New(CiType type, string name, CiExpr defaultValue = null) => new CiVar { Type = type, Name = name, Value = defaultValue };

		public override void Accept(CiVisitor visitor, CiPriority parent)
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

	public class CiProperty : CiMember
	{

		public override bool IsStatic() => false;

		public static CiProperty New(CiType type, CiId id, string name) => new CiProperty { Visibility = CiVisibility.Public, Type = type, Id = id, Name = name };
	}

	public class CiStaticProperty : CiMember
	{

		public override bool IsStatic() => true;

		public static CiStaticProperty New(CiType type, CiId id, string name) => new CiStaticProperty { Visibility = CiVisibility.Public, Type = type, Id = id, Name = name };
	}

	public class CiMethodBase : CiMember
	{

		internal bool IsMutator = false;

		internal bool Throws;

		internal CiStatement Body;

		internal bool IsLive = false;

		internal readonly HashSet<CiMethod> Calls = new HashSet<CiMethod>();

		public override bool IsStatic() => false;
	}

	public class CiMethod : CiMethodBase
	{

		internal CiCallType CallType;

		internal readonly CiParameters Parameters = new CiParameters();

		internal readonly CiScope MethodScope = new CiScope();

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
				CiMethod baseMethod = (CiMethod) method.Parent.Parent.TryLookup(method.Name, false);
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

		public override bool IsStatic()
		{
			throw new NotImplementedException();
		}

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

		public CiSymbol GetFirstValue()
		{
			CiSymbol symbol = this.First;
			while (!(symbol is CiConst))
				symbol = symbol.Next;
			return symbol;
		}

		public void AcceptValues(CiVisitor visitor)
		{
			CiConst previous = null;
			for (CiSymbol symbol = this.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiConst konst) {
					visitor.VisitEnumValue(konst, previous);
					previous = konst;
				}
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

		internal bool HasSubclasses = false;

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

		public bool HasToString() => TryLookup("ToString", false) is CiMethod method && method.IsToString();

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

		public override bool IsArray() => this.Class.Id == CiId.ArrayPtrClass;

		public override CiType GetBaseType() => IsArray() ? GetElementType().GetBaseType() : this;

		internal bool EqualTypeArguments(CiClassType right)
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
			return (this.Nullable && right.Id == CiId.NullType) || (right is CiClassType rightClass && IsAssignableFromClass(rightClass));
		}

		protected bool EqualsTypeInternal(CiClassType that) => this.Nullable == that.Nullable && this.Class == that.Class && EqualTypeArguments(that);

		public override bool EqualsType(CiType right) => right is CiClassType that && !(right is CiReadWriteClassType) && EqualsTypeInternal(that);

		public override string GetArraySuffix() => IsArray() ? "[]" : "";

		public virtual string GetClassSuffix() => "";

		string GetNullableSuffix() => this.Nullable ? "?" : "";

		public override string ToString()
		{
			if (IsArray())
				return $"{GetElementType().GetBaseType()}{GetArraySuffix()}{GetNullableSuffix()}{GetElementType().GetArraySuffix()}";
			switch (this.Class.TypeParameterCount) {
			case 0:
				return $"{this.Class.Name}{GetClassSuffix()}{GetNullableSuffix()}";
			case 1:
				return $"{this.Class.Name}<{this.TypeArg0}>{GetClassSuffix()}{GetNullableSuffix()}";
			case 2:
				return $"{this.Class.Name}<{this.TypeArg0}, {this.TypeArg1}>{GetClassSuffix()}{GetNullableSuffix()}";
			default:
				throw new NotImplementedException();
			}
		}
	}

	public class CiReadWriteClassType : CiClassType
	{

		public override bool IsAssignableFrom(CiType right)
		{
			return (this.Nullable && right.Id == CiId.NullType) || (right is CiReadWriteClassType rightClass && IsAssignableFromClass(rightClass));
		}

		public override bool EqualsType(CiType right) => right is CiReadWriteClassType that && !(right is CiStorageType) && !(right is CiDynamicPtrType) && EqualsTypeInternal(that);

		public override string GetArraySuffix() => IsArray() ? "[]!" : "";

		public override string GetClassSuffix() => "!";
	}

	public class CiStorageType : CiReadWriteClassType
	{

		public override bool IsFinal() => this.Class.Id != CiId.MatchClass;

		public override bool IsAssignableFrom(CiType right) => right is CiStorageType rightClass && this.Class == rightClass.Class && EqualTypeArguments(rightClass);

		public override bool EqualsType(CiType right) => right is CiStorageType that && EqualsTypeInternal(that);

		public override string GetClassSuffix() => "()";
	}

	public class CiDynamicPtrType : CiReadWriteClassType
	{

		public override bool IsAssignableFrom(CiType right)
		{
			return (this.Nullable && right.Id == CiId.NullType) || (right is CiDynamicPtrType rightClass && IsAssignableFromClass(rightClass));
		}

		public override bool EqualsType(CiType right) => right is CiDynamicPtrType that && EqualsTypeInternal(that);

		public override string GetArraySuffix() => IsArray() ? "[]#" : "";

		public override string GetClassSuffix() => "#";
	}

	public class CiArrayStorageType : CiStorageType
	{

		internal CiExpr LengthExpr;

		internal int Length;

		internal bool PtrTaken = false;

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
			this.Parent = null;
			CiSymbol basePtr = CiVar.New(null, "base");
			basePtr.Id = CiId.BasePtr;
			Add(basePtr);
			AddMinMaxValue(this.IntType, -2147483648, 2147483647);
			this.IntType.Add(CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.IntTryParse, "TryParse", CiVar.New(this.StringPtrType, "value"), CiVar.New(this.IntType, "radix", NewLiteralLong(0))));
			Add(this.IntType);
			this.UIntType.Name = "uint";
			Add(this.UIntType);
			AddMinMaxValue(this.LongType, -9223372036854775808, 9223372036854775807);
			this.LongType.Add(CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.LongTryParse, "TryParse", CiVar.New(this.StringPtrType, "value"), CiVar.New(this.IntType, "radix", NewLiteralLong(0))));
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
			this.DoubleType.Add(CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.DoubleTryParse, "TryParse", CiVar.New(this.StringPtrType, "value")));
			Add(this.DoubleType);
			Add(this.BoolType);
			this.StringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringContains, "Contains", CiVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringEndsWith, "EndsWith", CiVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(CiMethod.New(CiVisibility.Public, minus1Type, CiId.StringIndexOf, "IndexOf", CiVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(CiMethod.New(CiVisibility.Public, minus1Type, CiId.StringLastIndexOf, "LastIndexOf", CiVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(CiProperty.New(this.UIntType, CiId.StringLength, "Length"));
			this.StringClass.Add(CiMethod.New(CiVisibility.Public, this.StringStorageType, CiId.StringReplace, "Replace", CiVar.New(this.StringPtrType, "oldValue"), CiVar.New(this.StringPtrType, "newValue")));
			this.StringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringStartsWith, "StartsWith", CiVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(CiMethod.New(CiVisibility.Public, this.StringStorageType, CiId.StringSubstring, "Substring", CiVar.New(this.IntType, "offset"), CiVar.New(this.IntType, "length", NewLiteralLong(-1))));
			this.StringPtrType.Class = this.StringClass;
			Add(this.StringPtrType);
			this.StringNullablePtrType.Class = this.StringClass;
			this.StringStorageType.Class = this.StringClass;
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
			this.ArrayStorageClass.Add(CiProperty.New(this.UIntType, CiId.ArrayLength, "Length"));
			this.ArrayStorageClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.NumericElementType, this.VoidType, CiId.ArraySortAll, "Sort"), arraySortPart));
			CiType typeParam0NotFinal = new CiType { Id = CiId.TypeParam0NotFinal, Name = "T" };
			CiType typeParam0Predicate = new CiType { Id = CiId.TypeParam0Predicate, Name = "Predicate<T>" };
			CiClass listClass = AddCollection(CiId.ListClass, "List", 1, CiId.ListClear, CiId.ListCount);
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ListAdd, "Add", CiVar.New(typeParam0NotFinal, "value")));
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ListAddRange, "AddRange", CiVar.New(new CiClassType { Class = listClass, TypeArg0 = this.TypeParam0 }, "source")));
			listClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.ListAll, "All", CiVar.New(typeParam0Predicate, "predicate")));
			listClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.ListAny, "Any", CiVar.New(typeParam0Predicate, "predicate")));
			listClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.ListContains, "Contains", CiVar.New(this.TypeParam0, "value")));
			listClass.Add(CiMethod.New(CiVisibility.Public, this.VoidType, CiId.ListCopyTo, "CopyTo", CiVar.New(this.IntType, "sourceIndex"), CiVar.New(new CiReadWriteClassType { Class = this.ArrayPtrClass, TypeArg0 = this.TypeParam0 }, "destinationArray"), CiVar.New(this.IntType, "destinationIndex"), CiVar.New(this.IntType, "count")));
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.IntType, CiId.ListIndexOf, "IndexOf", CiVar.New(this.TypeParam0, "value")));
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.ListInsert, "Insert", CiVar.New(this.UIntType, "index"), CiVar.New(typeParam0NotFinal, "value")));
			listClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.TypeParam0, CiId.ListLast, "Last"));
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
			AddSet(CiId.HashSetClass, "HashSet", CiId.HashSetAdd, CiId.HashSetClear, CiId.HashSetContains, CiId.HashSetCount, CiId.HashSetRemove);
			AddSet(CiId.SortedSetClass, "SortedSet", CiId.SortedSetAdd, CiId.SortedSetClear, CiId.SortedSetContains, CiId.SortedSetCount, CiId.SortedSetRemove);
			AddDictionary(CiId.DictionaryClass, "Dictionary", CiId.DictionaryClear, CiId.DictionaryContainsKey, CiId.DictionaryCount, CiId.DictionaryRemove);
			AddDictionary(CiId.SortedDictionaryClass, "SortedDictionary", CiId.SortedDictionaryClear, CiId.SortedDictionaryContainsKey, CiId.SortedDictionaryCount, CiId.SortedDictionaryRemove);
			AddDictionary(CiId.OrderedDictionaryClass, "OrderedDictionary", CiId.OrderedDictionaryClear, CiId.OrderedDictionaryContainsKey, CiId.OrderedDictionaryCount, CiId.OrderedDictionaryRemove);
			CiClass textWriterClass = CiClass.New(CiCallType.Normal, CiId.TextWriterClass, "TextWriter");
			textWriterClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.TextWriterWrite, "Write", CiVar.New(this.PrintableType, "value")));
			textWriterClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.TextWriterWriteChar, "WriteChar", CiVar.New(this.IntType, "c")));
			textWriterClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.TextWriterWriteCodePoint, "WriteCodePoint", CiVar.New(this.IntType, "c")));
			textWriterClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.TextWriterWriteLine, "WriteLine", CiVar.New(this.PrintableType, "value", NewLiteralString(""))));
			Add(textWriterClass);
			CiClass consoleClass = CiClass.New(CiCallType.Static, CiId.None, "Console");
			consoleClass.Add(CiMethod.NewStatic(this.VoidType, CiId.ConsoleWrite, "Write", CiVar.New(this.PrintableType, "value")));
			consoleClass.Add(CiMethod.NewStatic(this.VoidType, CiId.ConsoleWriteLine, "WriteLine", CiVar.New(this.PrintableType, "value", NewLiteralString(""))));
			consoleClass.Add(CiStaticProperty.New(new CiStorageType { Class = textWriterClass }, CiId.ConsoleError, "Error"));
			Add(consoleClass);
			CiClass stringWriterClass = CiClass.New(CiCallType.Sealed, CiId.StringWriterClass, "StringWriter");
			stringWriterClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.StringWriterClear, "Clear"));
			stringWriterClass.Add(CiMethod.New(CiVisibility.Public, this.StringPtrType, CiId.StringWriterToString, "ToString"));
			Add(stringWriterClass);
			stringWriterClass.Parent = textWriterClass;
			CiClass utf8EncodingClass = CiClass.New(CiCallType.Sealed, CiId.None, "UTF8Encoding");
			utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, this.IntType, CiId.UTF8GetByteCount, "GetByteCount", CiVar.New(this.StringPtrType, "str")));
			utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, this.VoidType, CiId.UTF8GetBytes, "GetBytes", CiVar.New(this.StringPtrType, "str"), CiVar.New(new CiReadWriteClassType { Class = this.ArrayPtrClass, TypeArg0 = this.ByteType }, "bytes"), CiVar.New(this.IntType, "byteIndex")));
			utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, this.StringStorageType, CiId.UTF8GetString, "GetString", CiVar.New(new CiClassType { Class = this.ArrayPtrClass, TypeArg0 = this.ByteType }, "bytes"), CiVar.New(this.IntType, "offset"), CiVar.New(this.IntType, "length")));
			CiClass encodingClass = CiClass.New(CiCallType.Static, CiId.None, "Encoding");
			encodingClass.Add(CiStaticProperty.New(utf8EncodingClass, CiId.None, "UTF8"));
			Add(encodingClass);
			CiClass environmentClass = CiClass.New(CiCallType.Static, CiId.None, "Environment");
			environmentClass.Add(CiMethod.NewStatic(this.StringNullablePtrType, CiId.EnvironmentGetEnvironmentVariable, "GetEnvironmentVariable", CiVar.New(this.StringPtrType, "name")));
			Add(environmentClass);
			this.RegexOptionsEnum = NewEnum(true);
			this.RegexOptionsEnum.IsPublic = true;
			this.RegexOptionsEnum.Id = CiId.RegexOptionsEnum;
			this.RegexOptionsEnum.Name = "RegexOptions";
			CiConst regexOptionsNone = NewConstLong("None", 0);
			AddEnumValue(this.RegexOptionsEnum, regexOptionsNone);
			AddEnumValue(this.RegexOptionsEnum, NewConstLong("IgnoreCase", 1));
			AddEnumValue(this.RegexOptionsEnum, NewConstLong("Multiline", 2));
			AddEnumValue(this.RegexOptionsEnum, NewConstLong("Singleline", 16));
			Add(this.RegexOptionsEnum);
			CiClass regexClass = CiClass.New(CiCallType.Sealed, CiId.RegexClass, "Regex");
			regexClass.Add(CiMethod.NewStatic(this.StringStorageType, CiId.RegexEscape, "Escape", CiVar.New(this.StringPtrType, "str")));
			regexClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.BoolType, CiId.RegexIsMatchStr, "IsMatch", CiVar.New(this.StringPtrType, "input"), CiVar.New(this.StringPtrType, "pattern"), CiVar.New(this.RegexOptionsEnum, "options", regexOptionsNone)), CiMethod.New(CiVisibility.Public, this.BoolType, CiId.RegexIsMatchRegex, "IsMatch", CiVar.New(this.StringPtrType, "input"))));
			regexClass.Add(CiMethod.NewStatic(new CiDynamicPtrType { Class = regexClass }, CiId.RegexCompile, "Compile", CiVar.New(this.StringPtrType, "pattern"), CiVar.New(this.RegexOptionsEnum, "options", regexOptionsNone)));
			Add(regexClass);
			CiClass matchClass = CiClass.New(CiCallType.Sealed, CiId.MatchClass, "Match");
			matchClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.MatchFindStr, "Find", CiVar.New(this.StringPtrType, "input"), CiVar.New(this.StringPtrType, "pattern"), CiVar.New(this.RegexOptionsEnum, "options", regexOptionsNone)), CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.MatchFindRegex, "Find", CiVar.New(this.StringPtrType, "input"), CiVar.New(new CiClassType { Class = regexClass }, "pattern"))));
			matchClass.Add(CiProperty.New(this.IntType, CiId.MatchStart, "Start"));
			matchClass.Add(CiProperty.New(this.IntType, CiId.MatchEnd, "End"));
			matchClass.Add(CiMethod.New(CiVisibility.Public, this.StringPtrType, CiId.MatchGetCapture, "GetCapture", CiVar.New(this.UIntType, "group")));
			matchClass.Add(CiProperty.New(this.UIntType, CiId.MatchLength, "Length"));
			matchClass.Add(CiProperty.New(this.StringPtrType, CiId.MatchValue, "Value"));
			Add(matchClass);
			CiFloatingType floatIntType = new CiFloatingType { Id = CiId.FloatIntType, Name = "float" };
			CiClass mathClass = CiClass.New(CiCallType.Static, CiId.None, "Math");
			mathClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.IntType, CiId.MathAbs, "Abs", CiVar.New(this.LongType, "a")), CiMethod.NewStatic(this.FloatType, CiId.MathAbs, "Abs", CiVar.New(this.DoubleType, "a"))));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Acos", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Asin", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Atan", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Atan2", CiVar.New(this.DoubleType, "y"), CiVar.New(this.DoubleType, "x")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Cbrt", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathCeiling, "Ceiling", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.IntType, CiId.MathClamp, "Clamp", CiVar.New(this.LongType, "value"), CiVar.New(this.LongType, "min"), CiVar.New(this.LongType, "max")), CiMethod.NewStatic(this.FloatType, CiId.MathClamp, "Clamp", CiVar.New(this.DoubleType, "value"), CiVar.New(this.DoubleType, "min"), CiVar.New(this.DoubleType, "max"))));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Cos", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Cosh", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(NewConstDouble("E", 2.718281828459045));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Exp", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathMethod, "Floor", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathFusedMultiplyAdd, "FusedMultiplyAdd", CiVar.New(this.DoubleType, "x"), CiVar.New(this.DoubleType, "y"), CiVar.New(this.DoubleType, "z")));
			mathClass.Add(CiMethod.NewStatic(this.BoolType, CiId.MathIsFinite, "IsFinite", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.BoolType, CiId.MathIsInfinity, "IsInfinity", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.BoolType, CiId.MathIsNaN, "IsNaN", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Log", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathLog2, "Log2", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Log10", CiVar.New(this.DoubleType, "a")));
			mathClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.IntType, CiId.MathMaxInt, "Max", CiVar.New(this.LongType, "a"), CiVar.New(this.LongType, "b")), CiMethod.NewStatic(this.FloatType, CiId.MathMaxDouble, "Max", CiVar.New(this.DoubleType, "a"), CiVar.New(this.DoubleType, "b"))));
			mathClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.IntType, CiId.MathMinInt, "Min", CiVar.New(this.LongType, "a"), CiVar.New(this.LongType, "b")), CiMethod.NewStatic(this.FloatType, CiId.MathMinDouble, "Min", CiVar.New(this.DoubleType, "a"), CiVar.New(this.DoubleType, "b"))));
			mathClass.Add(CiStaticProperty.New(this.FloatType, CiId.MathNaN, "NaN"));
			mathClass.Add(CiStaticProperty.New(this.FloatType, CiId.MathNegativeInfinity, "NegativeInfinity"));
			mathClass.Add(NewConstDouble("PI", 3.141592653589793));
			mathClass.Add(CiStaticProperty.New(this.FloatType, CiId.MathPositiveInfinity, "PositiveInfinity"));
			mathClass.Add(CiMethod.NewStatic(this.FloatType, CiId.MathMethod, "Pow", CiVar.New(this.DoubleType, "x"), CiVar.New(this.DoubleType, "y")));
			mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathRound, "Round", CiVar.New(this.DoubleType, "a")));
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

		internal CiType NullType = new CiType { Id = CiId.NullType, Name = "null", Nullable = true };

		CiType TypeParam0 = new CiType { Id = CiId.TypeParam0, Name = "T" };

		internal CiIntegerType IntType = new CiIntegerType { Id = CiId.IntType, Name = "int" };

		CiRangeType UIntType = CiRangeType.New(0, 2147483647);

		internal CiIntegerType LongType = new CiIntegerType { Id = CiId.LongType, Name = "long" };

		internal CiRangeType ByteType = CiRangeType.New(0, 255);

		CiFloatingType FloatType = new CiFloatingType { Id = CiId.FloatType, Name = "float" };

		internal CiFloatingType DoubleType = new CiFloatingType { Id = CiId.DoubleType, Name = "double" };

		internal CiRangeType CharType = CiRangeType.New(-128, 65535);

		internal CiEnum BoolType = new CiEnum { Id = CiId.BoolType, Name = "bool" };

		CiClass StringClass = CiClass.New(CiCallType.Normal, CiId.StringClass, "string");

		internal CiStringType StringPtrType = new CiStringType { Id = CiId.StringPtrType, Name = "string" };

		internal CiStringType StringNullablePtrType = new CiStringType { Id = CiId.StringPtrType, Name = "string", Nullable = true };

		internal CiStringStorageType StringStorageType = new CiStringStorageType { Id = CiId.StringStorageType };

		internal CiType PrintableType = new CiPrintableType { Name = "printable" };

		internal CiClass ArrayPtrClass = CiClass.New(CiCallType.Normal, CiId.ArrayPtrClass, "ArrayPtr", 1);

		internal CiClass ArrayStorageClass = CiClass.New(CiCallType.Normal, CiId.ArrayStorageClass, "ArrayStorage", 1);

		internal CiEnum RegexOptionsEnum;

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

		internal CiEnum NewEnum(bool flags)
		{
			CiEnum enu = flags ? new CiEnumFlags() : new CiEnum();
			enu.Add(CiMethod.NewStatic(enu, CiId.EnumFromInt, "FromInt", CiVar.New(this.IntType, "value")));
			if (flags)
				enu.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.EnumHasFlag, "HasFlag", CiVar.New(enu, "flag")));
			return enu;
		}

		CiClass AddCollection(CiId id, string name, int typeParameterCount, CiId clearId, CiId countId)
		{
			CiClass result = CiClass.New(CiCallType.Normal, id, name, typeParameterCount);
			result.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, clearId, "Clear"));
			result.Add(CiProperty.New(this.UIntType, countId, "Count"));
			Add(result);
			return result;
		}

		void AddSet(CiId id, string name, CiId addId, CiId clearId, CiId containsId, CiId countId, CiId removeId)
		{
			CiClass set = AddCollection(id, name, 1, clearId, countId);
			set.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, addId, "Add", CiVar.New(this.TypeParam0, "value")));
			set.Add(CiMethod.New(CiVisibility.Public, this.BoolType, containsId, "Contains", CiVar.New(this.TypeParam0, "value")));
			set.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, removeId, "Remove", CiVar.New(this.TypeParam0, "value")));
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

		CiConst NewConstLong(string name, long value)
		{
			CiConst result = new CiConst { Visibility = CiVisibility.Public, Name = name, Value = NewLiteralLong(value), VisitStatus = CiVisitStatus.Done };
			result.Type = result.Value.Type;
			return result;
		}

		CiConst NewConstDouble(string name, double value) => new CiConst { Visibility = CiVisibility.Public, Name = name, Value = new CiLiteralDouble { Value = value, Type = this.DoubleType }, Type = this.DoubleType, VisitStatus = CiVisitStatus.Done };

		void AddMinMaxValue(CiIntegerType target, long min, long max)
		{
			target.Add(NewConstLong("MinValue", min));
			target.Add(NewConstLong("MaxValue", max));
		}

		internal static CiSystem New() => new CiSystem();
	}

	public class CiProgram : CiScope
	{

		internal CiSystem System;

		internal readonly List<string> TopLevelNatives = new List<string>();

		internal readonly List<CiClass> Classes = new List<CiClass>();

		internal readonly SortedDictionary<string, List<byte>> Resources = new SortedDictionary<string, List<byte>>();

		internal bool RegexOptionsEnum = false;
	}

	public abstract class CiParser : CiLexer
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
						DocParsePara(list.Items[^1]);
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
			if (!double.TryParse(GetLexeme().Replace("_", ""), out d))
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

		CiExpr ParsePrimaryExpr(bool type)
		{
			CiExpr result;
			switch (this.CurrentToken) {
			case CiToken.Increment:
			case CiToken.Decrement:
				CheckXcrementParent();
				return new CiPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr(false) };
			case CiToken.Minus:
			case CiToken.Tilde:
			case CiToken.ExclamationMark:
				return new CiPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr(false) };
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
				if (type && Eat(CiToken.Less)) {
					CiAggregateInitializer typeArgs = new CiAggregateInitializer();
					bool saveTypeArg = this.ParsingTypeArg;
					this.ParsingTypeArg = true;
					do
						typeArgs.Items.Add(ParseType());
					while (Eat(CiToken.Comma));
					Expect(CiToken.RightAngle);
					this.ParsingTypeArg = saveTypeArg;
					symbol.Left = typeArgs;
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
				case CiToken.QuestionMark:
					if (!type)
						return result;
					result = new CiPostfixExpr { Line = this.Line, Inner = result, Op = NextToken() };
					break;
				default:
					return result;
				}
			}
		}

		CiExpr ParseMulExpr()
		{
			CiExpr left = ParsePrimaryExpr(false);
			for (;;) {
				switch (this.CurrentToken) {
				case CiToken.Asterisk:
				case CiToken.Slash:
				case CiToken.Mod:
					left = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParsePrimaryExpr(false) };
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
					CiBinaryExpr isExpr = new CiBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParsePrimaryExpr(false) };
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
			CiExpr left = ParsePrimaryExpr(true);
			if (Eat(CiToken.Range))
				return new CiBinaryExpr { Line = this.Line, Left = left, Op = CiToken.Range, Right = ParsePrimaryExpr(true) };
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
			CiNative result = new CiNative { Line = this.Line };
			Expect(CiToken.Native);
			if (See(CiToken.LiteralString))
				result.Content = this.StringValue;
			else {
				int offset = this.CharOffset;
				Expect(CiToken.LeftBrace);
				int nesting = 1;
				for (;;) {
					if (See(CiToken.EndOfFile)) {
						Expect(CiToken.RightBrace);
						return result;
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
				result.Content = Encoding.UTF8.GetString(this.Input, offset, this.CharOffset - 1 - offset);
			}
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
				CiCase kase = result.Cases[^1];
				do {
					CiExpr expr = ParseExpr();
					if (See(CiToken.Id))
						expr = ParseVar(expr);
					if (Eat(CiToken.When))
						expr = new CiBinaryExpr { Line = this.Line, Left = expr, Op = CiToken.When, Right = ParseExpr() };
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
			CiEnum enu = this.Program.System.NewEnum(flags);
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

	public class CiConsoleParser : CiParser
	{

		internal bool HasErrors = false;

		protected override void ReportError(string message)
		{
			Console.Error.WriteLine($"{this.Filename}({this.Line}): ERROR: {message}");
			this.HasErrors = true;
		}
	}

	public class CiSema
	{

		protected CiProgram Program;

		internal bool HasErrors = false;

		CiMethodBase CurrentMethod = null;

		CiScope CurrentScope;

		readonly HashSet<CiMethod> CurrentPureMethods = new HashSet<CiMethod>();

		readonly Dictionary<CiVar, CiExpr> CurrentPureArguments = new Dictionary<CiVar, CiExpr>();

		CiType Poison = new CiType { Name = "poison" };

		CiContainerType GetCurrentContainer() => this.CurrentScope.GetContainer();

		protected void ReportError(CiStatement statement, string message)
		{
			Console.Error.WriteLine($"{GetCurrentContainer().Filename}({statement.Line}): ERROR: {message}");
			this.HasErrors = true;
		}

		CiType PoisonError(CiStatement statement, string message)
		{
			ReportError(statement, message);
			return this.Poison;
		}

		void ResolveBase(CiClass klass)
		{
			if (klass.HasBaseClass()) {
				this.CurrentScope = klass;
				if (this.Program.TryLookup(klass.BaseClassName, true) is CiClass baseClass) {
					if (klass.IsPublic && !baseClass.IsPublic)
						ReportError(klass, "Public class cannot derive from an internal class");
					baseClass.HasSubclasses = true;
					klass.Parent = baseClass;
				}
				else
					ReportError(klass, $"Base class {klass.BaseClassName} not found");
			}
			this.Program.Classes.Add(klass);
		}

		void CheckBaseCycle(CiClass klass)
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

		static void TakePtr(CiExpr expr)
		{
			if (expr.Type is CiArrayStorageType arrayStg)
				arrayStg.PtrTaken = true;
		}

		bool Coerce(CiExpr expr, CiType type)
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

		CiExpr VisitInterpolatedString(CiInterpolatedString expr)
		{
			int partsCount = 0;
			string s = "";
			for (int partsIndex = 0; partsIndex < expr.Parts.Count; partsIndex++) {
				CiInterpolatedPart part = expr.Parts[partsIndex];
				s += part.Prefix;
				CiExpr arg = VisitExpr(part.Argument);
				Coerce(arg, this.Program.System.PrintableType);
				switch (arg.Type) {
				case CiIntegerType _:
					switch (part.Format) {
					case ' ':
						if (arg is CiLiteralLong literalLong && part.WidthExpr == null) {
							s += $"{literalLong.Value}";
							continue;
						}
						break;
					case 'D':
					case 'd':
					case 'X':
					case 'x':
						if (part.WidthExpr != null && part.Precision >= 0)
							ReportError(part.WidthExpr, "Cannot format an integer with both width and precision");
						break;
					default:
						ReportError(arg, "Invalid format string");
						break;
					}
					break;
				case CiFloatingType _:
					switch (part.Format) {
					case ' ':
					case 'F':
					case 'f':
					case 'E':
					case 'e':
						break;
					default:
						ReportError(arg, "Invalid format string");
						break;
					}
					break;
				default:
					if (part.Format != ' ')
						ReportError(arg, "Invalid format string");
					else if (arg is CiLiteralString literalString && part.WidthExpr == null) {
						s += literalString.Value;
						continue;
					}
					break;
				}
				CiInterpolatedPart targetPart = expr.Parts[partsCount++];
				targetPart.Prefix = s;
				targetPart.Argument = arg;
				targetPart.WidthExpr = part.WidthExpr;
				targetPart.Width = part.WidthExpr != null ? FoldConstInt(part.WidthExpr) : 0;
				targetPart.Format = part.Format;
				targetPart.Precision = part.Precision;
				s = "";
			}
			s += expr.Suffix;
			if (partsCount == 0)
				return this.Program.System.NewLiteralString(s, expr.Line);
			expr.Type = this.Program.System.StringStorageType;
			expr.Parts.RemoveRange(partsCount, expr.Parts.Count - partsCount);
			expr.Suffix = s;
			return expr;
		}

		CiExpr Lookup(CiSymbolReference expr, CiScope scope)
		{
			if (expr.Symbol == null) {
				expr.Symbol = scope.TryLookup(expr.Name, expr.Left == null);
				if (expr.Symbol == null)
					return PoisonError(expr, $"{expr.Name} not found");
				expr.Type = expr.Symbol.Type;
			}
			if (!(scope is CiEnum) && expr.Symbol is CiConst konst) {
				ResolveConst(konst);
				if (konst.Value is CiLiteral || konst.Value is CiSymbolReference)
					return konst.Value;
			}
			return expr;
		}

		CiExpr VisitSymbolReference(CiSymbolReference expr)
		{
			if (expr.Left == null) {
				CiExpr resolved = Lookup(expr, this.CurrentScope);
				if (expr.Symbol is CiMember nearMember) {
					if (nearMember.Visibility == CiVisibility.Private && nearMember.Parent is CiClass memberClass && memberClass != GetCurrentContainer())
						ReportError(expr, $"Cannot access private member {expr.Name}");
					if (!nearMember.IsStatic() && (this.CurrentMethod == null || this.CurrentMethod.IsStatic()))
						ReportError(expr, $"Cannot use instance member {expr.Name} from static context");
				}
				if (resolved is CiSymbolReference symbol) {
					if (symbol.Symbol is CiVar v) {
						if (v.Parent is CiFor loop)
							loop.IsIteratorUsed = true;
						else if (this.CurrentPureArguments.ContainsKey(v))
							return this.CurrentPureArguments[v];
					}
					else if (symbol.Symbol.Id == CiId.RegexOptionsEnum)
						this.Program.RegexOptionsEnum = true;
				}
				return resolved;
			}
			CiExpr left = VisitExpr(expr.Left);
			if (left == this.Poison)
				return left;
			CiScope scope;
			bool isBase = left is CiSymbolReference baseSymbol && baseSymbol.Symbol.Id == CiId.BasePtr;
			if (isBase) {
				if (this.CurrentMethod == null || !(this.CurrentMethod.Parent.Parent is CiClass baseClass))
					return PoisonError(expr, "No base class");
				scope = baseClass;
			}
			else if (left is CiSymbolReference leftSymbol && leftSymbol.Symbol is CiScope obj)
				scope = obj;
			else {
				scope = left.Type;
				if (scope is CiClassType klass)
					scope = klass.Class;
			}
			CiExpr result = Lookup(expr, scope);
			if (result != expr)
				return result;
			if (expr.Symbol is CiMember member) {
				switch (member.Visibility) {
				case CiVisibility.Private:
					if (member.Parent != this.CurrentMethod.Parent || this.CurrentMethod.Parent != scope)
						ReportError(expr, $"Cannot access private member {expr.Name}");
					break;
				case CiVisibility.Protected:
					if (isBase)
						break;
					CiClass currentClass = (CiClass) this.CurrentMethod.Parent;
					CiClass scopeClass = (CiClass) scope;
					if (!currentClass.IsSameOrBaseOf(scopeClass))
						ReportError(expr, $"Cannot access protected member {expr.Name}");
					break;
				case CiVisibility.NumericElementType:
					if (left.Type is CiClassType klass && !(klass.GetElementType() is CiNumericType))
						ReportError(expr, "Method restricted to collections of numbers");
					break;
				case CiVisibility.FinalValueType:
					if (!left.Type.AsClassType().GetValueType().IsFinal())
						ReportError(expr, "Method restricted to dictionaries with storage values");
					break;
				default:
					switch (expr.Symbol.Id) {
					case CiId.ArrayLength:
						CiArrayStorageType arrayStorage = (CiArrayStorageType) left.Type;
						return ToLiteralLong(expr, arrayStorage.Length);
					case CiId.StringLength:
						if (left is CiLiteralString leftLiteral) {
							int length = leftLiteral.GetAsciiLength();
							if (length >= 0)
								return ToLiteralLong(expr, length);
						}
						break;
					default:
						break;
					}
					break;
				}
				if (!(member is CiMethodGroup)) {
					if (left is CiSymbolReference leftContainer && leftContainer.Symbol is CiContainerType) {
						if (!member.IsStatic())
							ReportError(expr, $"Cannot use instance member {expr.Name} without an object");
					}
					else if (member.IsStatic())
						ReportError(expr, $"{expr.Name} is static");
				}
			}
			return new CiSymbolReference { Line = expr.Line, Left = left, Name = expr.Name, Symbol = expr.Symbol, Type = expr.Type };
		}

		static CiRangeType Union(CiRangeType left, CiRangeType right)
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

		CiType GetIntegerType(CiExpr left, CiExpr right)
		{
			CiType type = this.Program.System.PromoteIntegerTypes(left.Type, right.Type);
			Coerce(left, type);
			Coerce(right, type);
			return type;
		}

		CiIntegerType GetShiftType(CiExpr left, CiExpr right)
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

		CiType GetNumericType(CiExpr left, CiExpr right)
		{
			CiType type = this.Program.System.PromoteNumericTypes(left.Type, right.Type);
			Coerce(left, type);
			Coerce(right, type);
			return type;
		}

		static int SaturatedNeg(int a)
		{
			if (a == -2147483648)
				return 2147483647;
			return -a;
		}

		static int SaturatedAdd(int a, int b)
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

		static int SaturatedSub(int a, int b)
		{
			if (b == -2147483648)
				return a < 0 ? a ^ b : 2147483647;
			return SaturatedAdd(a, -b);
		}

		static int SaturatedMul(int a, int b)
		{
			if (a == 0 || b == 0)
				return 0;
			if (a == -2147483648)
				return b >> 31 ^ a;
			if (b == -2147483648)
				return a >> 31 ^ b;
			if (2147483647 / Math.Abs(a) < Math.Abs(b))
				return (a ^ b) >> 31 ^ 2147483647;
			return a * b;
		}

		static int SaturatedDiv(int a, int b)
		{
			if (a == -2147483648 && b == -1)
				return 2147483647;
			return a / b;
		}

		static int SaturatedShiftRight(int a, int b) => a >> (b >= 31 || b < 0 ? 31 : b);

		static CiRangeType BitwiseUnsignedOp(CiRangeType left, CiToken op, CiRangeType right)
		{
			int leftVariableBits = left.GetVariableBits();
			int rightVariableBits = right.GetVariableBits();
			int min;
			int max;
			switch (op) {
			case CiToken.And:
				min = left.Min & right.Min & ~CiRangeType.GetMask(~left.Min & ~right.Min & (leftVariableBits | rightVariableBits));
				max = (left.Max | leftVariableBits) & (right.Max | rightVariableBits);
				if (max > left.Max)
					max = left.Max;
				if (max > right.Max)
					max = right.Max;
				break;
			case CiToken.Or:
				min = (left.Min & ~leftVariableBits) | (right.Min & ~rightVariableBits);
				max = left.Max | right.Max | CiRangeType.GetMask(left.Max & right.Max & CiRangeType.GetMask(leftVariableBits | rightVariableBits));
				if (min < left.Min)
					min = left.Min;
				if (min < right.Min)
					min = right.Min;
				break;
			case CiToken.Xor:
				int variableBits = leftVariableBits | rightVariableBits;
				min = (left.Min ^ right.Min) & ~variableBits;
				max = (left.Max ^ right.Max) | variableBits;
				break;
			default:
				throw new NotImplementedException();
			}
			if (min > max)
				return CiRangeType.New(max, min);
			return CiRangeType.New(min, max);
		}

		bool IsEnumOp(CiExpr left, CiExpr right)
		{
			if (left.Type is CiEnum) {
				if (left.Type.Id != CiId.BoolType && !(left.Type is CiEnumFlags))
					ReportError(left, $"Define flags enumeration as: enum* {left.Type}");
				Coerce(right, left.Type);
				return true;
			}
			return false;
		}

		CiType BitwiseOp(CiExpr left, CiToken op, CiExpr right)
		{
			if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange) {
				CiRangeType range = null;
				CiRangeType rightNegative;
				CiRangeType rightPositive;
				if (rightRange.Min >= 0) {
					rightNegative = null;
					rightPositive = rightRange;
				}
				else if (rightRange.Max < 0) {
					rightNegative = rightRange;
					rightPositive = null;
				}
				else {
					rightNegative = CiRangeType.New(rightRange.Min, -1);
					rightPositive = CiRangeType.New(0, rightRange.Max);
				}
				if (leftRange.Min < 0) {
					CiRangeType leftNegative = leftRange.Max < 0 ? leftRange : CiRangeType.New(leftRange.Min, -1);
					if (rightNegative != null)
						range = BitwiseUnsignedOp(leftNegative, op, rightNegative);
					if (rightPositive != null)
						range = Union(BitwiseUnsignedOp(leftNegative, op, rightPositive), range);
				}
				if (leftRange.Max >= 0) {
					CiRangeType leftPositive = leftRange.Min >= 0 ? leftRange : CiRangeType.New(0, leftRange.Max);
					if (rightNegative != null)
						range = Union(BitwiseUnsignedOp(leftPositive, op, rightNegative), range);
					if (rightPositive != null)
						range = Union(BitwiseUnsignedOp(leftPositive, op, rightPositive), range);
				}
				return range;
			}
			if (IsEnumOp(left, right))
				return left.Type;
			return GetIntegerType(left, right);
		}

		static CiRangeType NewRangeType(int a, int b, int c, int d)
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

		CiLiteral ToLiteralBool(CiExpr expr, bool value)
		{
			CiLiteral result = value ? new CiLiteralTrue() : new CiLiteralFalse();
			result.Line = expr.Line;
			result.Type = this.Program.System.BoolType;
			return result;
		}

		CiLiteralLong ToLiteralLong(CiExpr expr, long value) => this.Program.System.NewLiteralLong(value, expr.Line);

		CiLiteralDouble ToLiteralDouble(CiExpr expr, double value) => new CiLiteralDouble { Line = expr.Line, Type = this.Program.System.DoubleType, Value = value };

		void CheckLValue(CiExpr expr)
		{
			switch (expr) {
			case CiSymbolReference symbol:
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
						default:
							throw new NotImplementedException();
						}
					}
					break;
				default:
					ReportError(expr, "Cannot modify this");
					break;
				}
				break;
			case CiBinaryExpr indexing when indexing.Op == CiToken.LeftBracket:
				switch (indexing.Left.Type) {
				case CiStorageType _:
					break;
				case CiReadWriteClassType _:
					break;
				case CiClassType _:
					ReportError(expr, "Cannot modify array through a read-only reference");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			default:
				ReportError(expr, "Cannot modify this");
				break;
			}
		}

		CiInterpolatedString Concatenate(CiInterpolatedString left, CiInterpolatedString right)
		{
			CiInterpolatedString result = new CiInterpolatedString { Line = left.Line, Type = this.Program.System.StringStorageType };
			result.Parts.AddRange(left.Parts);
			if (right.Parts.Count == 0)
				result.Suffix = left.Suffix + right.Suffix;
			else {
				result.Parts.AddRange(right.Parts);
				CiInterpolatedPart middle = result.Parts[left.Parts.Count];
				middle.Prefix = left.Suffix + middle.Prefix;
				result.Suffix = right.Suffix;
			}
			return result;
		}

		CiInterpolatedString ToInterpolatedString(CiExpr expr)
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

		void CheckComparison(CiExpr left, CiExpr right)
		{
			if (left.Type is CiEnum)
				Coerce(right, left.Type);
			else {
				CiType doubleType = this.Program.System.DoubleType;
				Coerce(left, doubleType);
				Coerce(right, doubleType);
			}
		}

		void OpenScope(CiScope scope)
		{
			scope.Parent = this.CurrentScope;
			this.CurrentScope = scope;
		}

		void CloseScope()
		{
			this.CurrentScope = this.CurrentScope.Parent;
		}

		CiExpr ResolveNew(CiPrefixExpr expr)
		{
			if (expr.Type != null)
				return expr;
			CiType type;
			if (expr.Inner is CiBinaryExpr binaryNew && binaryNew.Op == CiToken.LeftBrace) {
				type = ToType(binaryNew.Left, true);
				if (!(type is CiClassType klass) || klass is CiReadWriteClassType)
					return PoisonError(expr, "Invalid argument to new");
				CiAggregateInitializer init = (CiAggregateInitializer) binaryNew.Right;
				ResolveObjectLiteral(klass, init);
				expr.Type = new CiDynamicPtrType { Line = expr.Line, Class = klass.Class };
				expr.Inner = init;
				return expr;
			}
			type = ToType(expr.Inner, true);
			switch (type) {
			case CiArrayStorageType array:
				expr.Type = new CiDynamicPtrType { Line = expr.Line, Class = this.Program.System.ArrayPtrClass, TypeArg0 = array.GetElementType() };
				expr.Inner = array.LengthExpr;
				return expr;
			case CiStorageType klass:
				expr.Type = new CiDynamicPtrType { Line = expr.Line, Class = klass.Class };
				expr.Inner = null;
				return expr;
			default:
				return PoisonError(expr, "Invalid argument to new");
			}
		}

		protected virtual int GetResourceLength(string name, CiPrefixExpr expr) => 0;

		CiExpr VisitPrefixExpr(CiPrefixExpr expr)
		{
			CiExpr inner;
			CiType type;
			switch (expr.Op) {
			case CiToken.Increment:
			case CiToken.Decrement:
				inner = VisitExpr(expr.Inner);
				CheckLValue(inner);
				Coerce(inner, this.Program.System.DoubleType);
				if (inner.Type is CiRangeType xcrementRange) {
					int delta = expr.Op == CiToken.Increment ? 1 : -1;
					type = CiRangeType.New(xcrementRange.Min + delta, xcrementRange.Max + delta);
				}
				else
					type = inner.Type;
				expr.Inner = inner;
				expr.Type = type;
				return expr;
			case CiToken.Minus:
				inner = VisitExpr(expr.Inner);
				Coerce(inner, this.Program.System.DoubleType);
				if (inner.Type is CiRangeType negRange) {
					if (negRange.Min == negRange.Max)
						return ToLiteralLong(expr, -negRange.Min);
					type = CiRangeType.New(SaturatedNeg(negRange.Max), SaturatedNeg(negRange.Min));
				}
				else if (inner is CiLiteralDouble d)
					return ToLiteralDouble(expr, -d.Value);
				else if (inner is CiLiteralLong l)
					return ToLiteralLong(expr, -l.Value);
				else
					type = inner.Type;
				break;
			case CiToken.Tilde:
				inner = VisitExpr(expr.Inner);
				if (inner.Type is CiEnumFlags)
					type = inner.Type;
				else {
					Coerce(inner, this.Program.System.IntType);
					if (inner.Type is CiRangeType notRange)
						type = CiRangeType.New(~notRange.Max, ~notRange.Min);
					else
						type = inner.Type;
				}
				break;
			case CiToken.ExclamationMark:
				inner = ResolveBool(expr.Inner);
				return new CiPrefixExpr { Line = expr.Line, Op = CiToken.ExclamationMark, Inner = inner, Type = this.Program.System.BoolType };
			case CiToken.New:
				return ResolveNew(expr);
			case CiToken.Resource:
				if (!(FoldConst(expr.Inner) is CiLiteralString resourceName))
					return PoisonError(expr, "Resource name must be string");
				inner = resourceName;
				type = new CiArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = this.Program.System.ByteType, Length = GetResourceLength(resourceName.Value, expr) };
				break;
			default:
				throw new NotImplementedException();
			}
			return new CiPrefixExpr { Line = expr.Line, Op = expr.Op, Inner = inner, Type = type };
		}

		CiExpr VisitPostfixExpr(CiPostfixExpr expr)
		{
			expr.Inner = VisitExpr(expr.Inner);
			switch (expr.Op) {
			case CiToken.Increment:
			case CiToken.Decrement:
				CheckLValue(expr.Inner);
				Coerce(expr.Inner, this.Program.System.DoubleType);
				expr.Type = expr.Inner.Type;
				return expr;
			default:
				return PoisonError(expr, $"Unexpected {CiLexer.TokenToString(expr.Op)}");
			}
		}

		static bool CanCompareEqual(CiType left, CiType right)
		{
			switch (left) {
			case CiNumericType _:
				return right is CiNumericType;
			case CiEnum _:
				return left == right;
			case CiClassType leftClass:
				if (left.Nullable && right.Id == CiId.NullType)
					return true;
				if ((left is CiStorageType && (right is CiStorageType || right is CiDynamicPtrType)) || (left is CiDynamicPtrType && right is CiStorageType))
					return false;
				return right is CiClassType rightClass && (leftClass.Class.IsSameOrBaseOf(rightClass.Class) || rightClass.Class.IsSameOrBaseOf(leftClass.Class)) && leftClass.EqualTypeArguments(rightClass);
			default:
				return left.Id == CiId.NullType && right.Nullable;
			}
		}

		CiExpr ResolveEquality(CiBinaryExpr expr, CiExpr left, CiExpr right)
		{
			if (!CanCompareEqual(left.Type, right.Type))
				return PoisonError(expr, $"Cannot compare {left.Type} with {right.Type}");
			if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange) {
				if (leftRange.Min == leftRange.Max && leftRange.Min == rightRange.Min && leftRange.Min == rightRange.Max)
					return ToLiteralBool(expr, expr.Op == CiToken.Equal);
				if (leftRange.Max < rightRange.Min || leftRange.Min > rightRange.Max)
					return ToLiteralBool(expr, expr.Op == CiToken.NotEqual);
			}
			else {
				switch (left) {
				case CiLiteralLong leftLong when right is CiLiteralLong rightLong:
					return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (leftLong.Value == rightLong.Value));
				case CiLiteralDouble leftDouble when right is CiLiteralDouble rightDouble:
					return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (leftDouble.Value == rightDouble.Value));
				case CiLiteralString leftString when right is CiLiteralString rightString:
					return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (leftString.Value == rightString.Value));
				case CiLiteralNull _:
					return ToLiteralBool(expr, expr.Op == CiToken.Equal);
				case CiLiteralFalse _:
					return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ right is CiLiteralFalse);
				case CiLiteralTrue _:
					return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ right is CiLiteralTrue);
				default:
					break;
				}
				if (left.IsConstEnum() && right.IsConstEnum())
					return ToLiteralBool(expr, (expr.Op == CiToken.NotEqual) ^ (left.IntValue() == right.IntValue()));
			}
			TakePtr(left);
			TakePtr(right);
			return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = this.Program.System.BoolType };
		}

		CiExpr ResolveIs(CiBinaryExpr expr, CiExpr left, CiExpr right)
		{
			if (!(left.Type is CiClassType leftPtr) || left.Type is CiStorageType)
				return PoisonError(expr, "Left hand side of the 'is' operator must be an object reference");
			CiClass klass;
			switch (right) {
			case CiSymbolReference symbol:
				if (symbol.Symbol is CiClass klass2)
					klass = klass2;
				else
					return PoisonError(expr, "Right hand side of the 'is' operator must be a class name");
				break;
			case CiVar def:
				if (!(def.Type is CiClassType rightPtr))
					return PoisonError(expr, "Right hand side of the 'is' operator must be an object reference definition");
				if (rightPtr is CiReadWriteClassType && !(leftPtr is CiDynamicPtrType) && (rightPtr is CiDynamicPtrType || !(leftPtr is CiReadWriteClassType)))
					return PoisonError(expr, $"{leftPtr} cannot be casted to {rightPtr}");
				klass = rightPtr.Class;
				break;
			default:
				return PoisonError(expr, "Right hand side of the 'is' operator must be a class name");
			}
			if (klass.IsSameOrBaseOf(leftPtr.Class))
				return PoisonError(expr, $"{leftPtr} is {klass.Name}, the 'is' operator would always return 'true'");
			if (!leftPtr.Class.IsSameOrBaseOf(klass))
				return PoisonError(expr, $"{leftPtr} is not base class of {klass.Name}, the 'is' operator would always return 'false'");
			expr.Left = left;
			expr.Type = this.Program.System.BoolType;
			return expr;
		}

		CiExpr VisitBinaryExpr(CiBinaryExpr expr)
		{
			CiExpr left = VisitExpr(expr.Left);
			CiExpr right = VisitExpr(expr.Right);
			if (left == this.Poison || right == this.Poison)
				return this.Poison;
			CiType type;
			switch (expr.Op) {
			case CiToken.LeftBracket:
				if (!(left.Type is CiClassType klass))
					return PoisonError(expr, "Cannot index this object");
				switch (klass.Class.Id) {
				case CiId.StringClass:
					Coerce(right, this.Program.System.IntType);
					if (left is CiLiteralString stringLiteral && right is CiLiteralLong indexLiteral) {
						long i = indexLiteral.Value;
						if (i >= 0 && i <= 2147483647) {
							int c = stringLiteral.GetAsciiAt((int) i);
							if (c >= 0)
								return CiLiteralChar.New(c, expr.Line);
						}
					}
					type = this.Program.System.CharType;
					break;
				case CiId.ArrayPtrClass:
				case CiId.ArrayStorageClass:
				case CiId.ListClass:
					Coerce(right, this.Program.System.IntType);
					type = klass.GetElementType();
					break;
				case CiId.DictionaryClass:
				case CiId.SortedDictionaryClass:
				case CiId.OrderedDictionaryClass:
					Coerce(right, klass.GetKeyType());
					type = klass.GetValueType();
					break;
				default:
					return PoisonError(expr, "Cannot index this object");
				}
				break;
			case CiToken.Plus:
				if (left.Type is CiRangeType leftAdd && right.Type is CiRangeType rightAdd) {
					type = CiRangeType.New(SaturatedAdd(leftAdd.Min, rightAdd.Min), SaturatedAdd(leftAdd.Max, rightAdd.Max));
				}
				else if (left.Type is CiStringType || right.Type is CiStringType) {
					Coerce(left, this.Program.System.PrintableType);
					Coerce(right, this.Program.System.PrintableType);
					if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral)
						return this.Program.System.NewLiteralString(leftLiteral.GetLiteralString() + rightLiteral.GetLiteralString(), expr.Line);
					if (left is CiInterpolatedString || right is CiInterpolatedString)
						return Concatenate(ToInterpolatedString(left), ToInterpolatedString(right));
					type = this.Program.System.StringStorageType;
				}
				else
					type = GetNumericType(left, right);
				break;
			case CiToken.Minus:
				if (left.Type is CiRangeType leftSub && right.Type is CiRangeType rightSub) {
					type = CiRangeType.New(SaturatedSub(leftSub.Min, rightSub.Max), SaturatedSub(leftSub.Max, rightSub.Min));
				}
				else
					type = GetNumericType(left, right);
				break;
			case CiToken.Asterisk:
				if (left.Type is CiRangeType leftMul && right.Type is CiRangeType rightMul) {
					type = NewRangeType(SaturatedMul(leftMul.Min, rightMul.Min), SaturatedMul(leftMul.Min, rightMul.Max), SaturatedMul(leftMul.Max, rightMul.Min), SaturatedMul(leftMul.Max, rightMul.Max));
				}
				else
					type = GetNumericType(left, right);
				break;
			case CiToken.Slash:
				if (left.Type is CiRangeType leftDiv && right.Type is CiRangeType rightDiv) {
					int denMin = rightDiv.Min;
					if (denMin == 0)
						denMin = 1;
					int denMax = rightDiv.Max;
					if (denMax == 0)
						denMax = -1;
					type = NewRangeType(SaturatedDiv(leftDiv.Min, denMin), SaturatedDiv(leftDiv.Min, denMax), SaturatedDiv(leftDiv.Max, denMin), SaturatedDiv(leftDiv.Max, denMax));
				}
				else
					type = GetNumericType(left, right);
				break;
			case CiToken.Mod:
				if (left.Type is CiRangeType leftMod && right.Type is CiRangeType rightMod) {
					int den = ~Math.Min(rightMod.Min, -rightMod.Max);
					if (den < 0)
						return PoisonError(expr, "Mod zero");
					type = CiRangeType.New(leftMod.Min >= 0 ? 0 : Math.Max(leftMod.Min, -den), leftMod.Max < 0 ? 0 : Math.Min(leftMod.Max, den));
				}
				else
					type = GetIntegerType(left, right);
				break;
			case CiToken.And:
			case CiToken.Or:
			case CiToken.Xor:
				type = BitwiseOp(left, expr.Op, right);
				break;
			case CiToken.ShiftLeft:
				if (left.Type is CiRangeType leftShl && right.Type is CiRangeType rightShl && leftShl.Min == leftShl.Max && rightShl.Min == rightShl.Max) {
					int result = leftShl.Min << rightShl.Min;
					type = CiRangeType.New(result, result);
				}
				else
					type = GetShiftType(left, right);
				break;
			case CiToken.ShiftRight:
				if (left.Type is CiRangeType leftShr && right.Type is CiRangeType rightShr) {
					if (rightShr.Min < 0)
						rightShr = CiRangeType.New(0, 32);
					type = CiRangeType.New(SaturatedShiftRight(leftShr.Min, leftShr.Min < 0 ? rightShr.Min : rightShr.Max), SaturatedShiftRight(leftShr.Max, leftShr.Max < 0 ? rightShr.Max : rightShr.Min));
				}
				else
					type = GetShiftType(left, right);
				break;
			case CiToken.Equal:
			case CiToken.NotEqual:
				return ResolveEquality(expr, left, right);
			case CiToken.Less:
				if (left.Type is CiRangeType leftLess && right.Type is CiRangeType rightLess) {
					if (leftLess.Max < rightLess.Min)
						return ToLiteralBool(expr, true);
					if (leftLess.Min >= rightLess.Max)
						return ToLiteralBool(expr, false);
				}
				else
					CheckComparison(left, right);
				type = this.Program.System.BoolType;
				break;
			case CiToken.LessOrEqual:
				if (left.Type is CiRangeType leftLeq && right.Type is CiRangeType rightLeq) {
					if (leftLeq.Max <= rightLeq.Min)
						return ToLiteralBool(expr, true);
					if (leftLeq.Min > rightLeq.Max)
						return ToLiteralBool(expr, false);
				}
				else
					CheckComparison(left, right);
				type = this.Program.System.BoolType;
				break;
			case CiToken.Greater:
				if (left.Type is CiRangeType leftGreater && right.Type is CiRangeType rightGreater) {
					if (leftGreater.Min > rightGreater.Max)
						return ToLiteralBool(expr, true);
					if (leftGreater.Max <= rightGreater.Min)
						return ToLiteralBool(expr, false);
				}
				else
					CheckComparison(left, right);
				type = this.Program.System.BoolType;
				break;
			case CiToken.GreaterOrEqual:
				if (left.Type is CiRangeType leftGeq && right.Type is CiRangeType rightGeq) {
					if (leftGeq.Min >= rightGeq.Max)
						return ToLiteralBool(expr, true);
					if (leftGeq.Max < rightGeq.Min)
						return ToLiteralBool(expr, false);
				}
				else
					CheckComparison(left, right);
				type = this.Program.System.BoolType;
				break;
			case CiToken.CondAnd:
				Coerce(left, this.Program.System.BoolType);
				Coerce(right, this.Program.System.BoolType);
				if (left is CiLiteralTrue)
					return right;
				if (left is CiLiteralFalse || right is CiLiteralTrue)
					return left;
				type = this.Program.System.BoolType;
				break;
			case CiToken.CondOr:
				Coerce(left, this.Program.System.BoolType);
				Coerce(right, this.Program.System.BoolType);
				if (left is CiLiteralTrue || right is CiLiteralFalse)
					return left;
				if (left is CiLiteralFalse)
					return right;
				type = this.Program.System.BoolType;
				break;
			case CiToken.Assign:
				CheckLValue(left);
				Coerce(right, left.Type);
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case CiToken.AddAssign:
				CheckLValue(left);
				if (left.Type.Id == CiId.StringStorageType)
					Coerce(right, this.Program.System.PrintableType);
				else {
					Coerce(left, this.Program.System.DoubleType);
					Coerce(right, left.Type);
				}
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case CiToken.SubAssign:
			case CiToken.MulAssign:
			case CiToken.DivAssign:
				CheckLValue(left);
				Coerce(left, this.Program.System.DoubleType);
				Coerce(right, left.Type);
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case CiToken.ModAssign:
			case CiToken.ShiftLeftAssign:
			case CiToken.ShiftRightAssign:
				CheckLValue(left);
				Coerce(left, this.Program.System.IntType);
				Coerce(right, this.Program.System.IntType);
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case CiToken.AndAssign:
			case CiToken.OrAssign:
			case CiToken.XorAssign:
				CheckLValue(left);
				if (!IsEnumOp(left, right)) {
					Coerce(left, this.Program.System.IntType);
					Coerce(right, this.Program.System.IntType);
				}
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case CiToken.Is:
				return ResolveIs(expr, left, right);
			case CiToken.Range:
				return PoisonError(expr, "Range within an expression");
			default:
				throw new NotImplementedException();
			}
			if (type is CiRangeType range && range.Min == range.Max)
				return ToLiteralLong(expr, range.Min);
			return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = type };
		}

		CiType TryGetPtr(CiType type, bool nullable)
		{
			if (type.Id == CiId.StringStorageType)
				return nullable ? this.Program.System.StringNullablePtrType : this.Program.System.StringPtrType;
			if (type is CiStorageType storage)
				return new CiReadWriteClassType { Class = storage.Class.Id == CiId.ArrayStorageClass ? this.Program.System.ArrayPtrClass : storage.Class, Nullable = nullable, TypeArg0 = storage.TypeArg0, TypeArg1 = storage.TypeArg1 };
			if (nullable && type is CiClassType ptr && !ptr.Nullable) {
				CiClassType result;
				if (type is CiDynamicPtrType)
					result = new CiDynamicPtrType();
				else if (type is CiReadWriteClassType)
					result = new CiReadWriteClassType();
				else
					result = new CiClassType();
				result.Class = ptr.Class;
				result.Nullable = true;
				result.TypeArg0 = ptr.TypeArg0;
				result.TypeArg1 = ptr.TypeArg1;
				return result;
			}
			return type;
		}

		static CiClass GetLowestCommonAncestor(CiClass left, CiClass right)
		{
			for (;;) {
				if (left.IsSameOrBaseOf(right))
					return left;
				if (left.Parent is CiClass parent)
					left = parent;
				else
					return null;
			}
		}

		CiType GetCommonType(CiExpr left, CiExpr right)
		{
			if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange)
				return Union(leftRange, rightRange);
			bool nullable = left.Type.Nullable || right.Type.Nullable;
			CiType ptr = TryGetPtr(left.Type, nullable);
			if (ptr.IsAssignableFrom(right.Type))
				return ptr;
			ptr = TryGetPtr(right.Type, nullable);
			if (ptr.IsAssignableFrom(left.Type))
				return ptr;
			if (left.Type is CiClassType leftClass && right.Type is CiClassType rightClass && leftClass.EqualTypeArguments(rightClass)) {
				CiClass klass = GetLowestCommonAncestor(leftClass.Class, rightClass.Class);
				if (klass != null) {
					CiClassType result;
					if (!(leftClass is CiReadWriteClassType) || !(rightClass is CiReadWriteClassType))
						result = new CiClassType();
					else if (leftClass is CiDynamicPtrType && rightClass is CiDynamicPtrType)
						result = new CiDynamicPtrType();
					else
						result = new CiReadWriteClassType();
					result.Class = klass;
					result.Nullable = nullable;
					result.TypeArg0 = leftClass.TypeArg0;
					result.TypeArg1 = leftClass.TypeArg1;
					return result;
				}
			}
			return PoisonError(left, $"Incompatible types: {left.Type} and {right.Type}");
		}

		CiExpr VisitSelectExpr(CiSelectExpr expr)
		{
			CiExpr cond = ResolveBool(expr.Cond);
			CiExpr onTrue = VisitExpr(expr.OnTrue);
			CiExpr onFalse = VisitExpr(expr.OnFalse);
			if (onTrue == this.Poison || onFalse == this.Poison)
				return this.Poison;
			CiType type = GetCommonType(onTrue, onFalse);
			Coerce(onTrue, type);
			Coerce(onFalse, type);
			if (cond is CiLiteralTrue)
				return onTrue;
			if (cond is CiLiteralFalse)
				return onFalse;
			return new CiSelectExpr { Line = expr.Line, Cond = cond, OnTrue = onTrue, OnFalse = onFalse, Type = type };
		}

		CiType EvalType(CiClassType generic, CiType type)
		{
			if (type.Id == CiId.TypeParam0)
				return generic.TypeArg0;
			if (type.Id == CiId.TypeParam0NotFinal)
				return generic.TypeArg0.IsFinal() ? null : generic.TypeArg0;
			if (type is CiClassType collection && collection.Class.TypeParameterCount == 1 && collection.TypeArg0.Id == CiId.TypeParam0) {
				CiClassType result = type is CiReadWriteClassType ? new CiReadWriteClassType() : new CiClassType();
				result.Class = collection.Class;
				result.TypeArg0 = generic.TypeArg0;
				return result;
			}
			return type;
		}

		bool CanCall(CiExpr obj, CiMethod method, List<CiExpr> arguments)
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

		CiExpr ResolveCallWithArguments(CiCallExpr expr, List<CiExpr> arguments)
		{
			if (!(VisitExpr(expr.Method) is CiSymbolReference symbol))
				return this.Poison;
			CiMethod method;
			switch (symbol.Symbol) {
			case null:
				return this.Poison;
			case CiMethod m:
				method = m;
				break;
			case CiMethodGroup group:
				method = group.Methods[0];
				if (!CanCall(symbol.Left, method, arguments))
					method = group.Methods[1];
				break;
			default:
				return PoisonError(symbol, "Expected a method");
			}
			int i = 0;
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				CiType type = param.Type;
				if (symbol.Left != null && symbol.Left.Type is CiClassType generic) {
					type = EvalType(generic, type);
					if (type == null)
						continue;
				}
				if (i >= arguments.Count) {
					if (param.Value != null)
						break;
					return PoisonError(expr, $"Too few arguments for '{method.Name}'");
				}
				CiExpr arg = arguments[i++];
				if (type.Id == CiId.TypeParam0Predicate && arg is CiLambdaExpr lambda) {
					lambda.First.Type = symbol.Left.Type.AsClassType().TypeArg0;
					OpenScope(lambda);
					lambda.Body = VisitExpr(lambda.Body);
					CloseScope();
					Coerce(lambda.Body, this.Program.System.BoolType);
				}
				else
					Coerce(arg, type);
			}
			if (i < arguments.Count)
				return PoisonError(arguments[i], $"Too many arguments for '{method.Name}'");
			if (method.Throws) {
				if (this.CurrentMethod == null)
					return PoisonError(expr, $"Cannot call method '{method.Name}' here because it is marked 'throws'");
				if (!this.CurrentMethod.Throws)
					return PoisonError(expr, "Method marked 'throws' called from a method not marked 'throws'");
			}
			symbol.Symbol = method;
			if (method.CallType == CiCallType.Static && method.Body is CiReturn ret && arguments.All(arg => arg is CiLiteral) && !this.CurrentPureMethods.Contains(method)) {
				this.CurrentPureMethods.Add(method);
				i = 0;
				for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
					if (i < arguments.Count)
						this.CurrentPureArguments[param] = arguments[i++];
					else
						this.CurrentPureArguments[param] = param.Value;
				}
				CiExpr result = VisitExpr(ret.Value);
				for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter())
					this.CurrentPureArguments.Remove(param);
				this.CurrentPureMethods.Remove(method);
				if (result is CiLiteral)
					return result;
			}
			if (this.CurrentMethod != null)
				this.CurrentMethod.Calls.Add(method);
			if (this.CurrentPureArguments.Count == 0) {
				expr.Method = symbol;
				CiType type = method.Type;
				if (symbol.Left != null && symbol.Left.Type is CiClassType generic)
					type = EvalType(generic, type);
				expr.Type = type;
			}
			return expr;
		}

		CiExpr VisitCallExpr(CiCallExpr expr)
		{
			if (this.CurrentPureArguments.Count == 0) {
				List<CiExpr> arguments = expr.Arguments;
				for (int i = 0; i < arguments.Count; i++) {
					if (!(arguments[i] is CiLambdaExpr))
						arguments[i] = VisitExpr(arguments[i]);
				}
				return ResolveCallWithArguments(expr, arguments);
			}
			else {
				List<CiExpr> arguments = new List<CiExpr>();
				foreach (CiExpr arg in expr.Arguments)
					arguments.Add(VisitExpr(arg));
				return ResolveCallWithArguments(expr, arguments);
			}
		}

		void ResolveObjectLiteral(CiClassType klass, CiAggregateInitializer init)
		{
			foreach (CiExpr item in init.Items) {
				CiBinaryExpr field = (CiBinaryExpr) item;
				Debug.Assert(field.Op == CiToken.Assign);
				CiSymbolReference symbol = (CiSymbolReference) field.Left;
				Lookup(symbol, klass.Class);
				if (symbol.Symbol is CiField) {
					field.Right = VisitExpr(field.Right);
					Coerce(field.Right, symbol.Type);
				}
				else
					ReportError(field, "Expected a field");
			}
		}

		void VisitVar(CiVar expr)
		{
			CiType type = ResolveType(expr);
			if (expr.Value != null) {
				if (type is CiStorageType storage && expr.Value is CiAggregateInitializer init)
					ResolveObjectLiteral(storage, init);
				else {
					expr.Value = VisitExpr(expr.Value);
					if (!expr.IsAssignableStorage()) {
						if (type is CiArrayStorageType array) {
							type = array.GetElementType();
							if (!(expr.Value is CiLiteral literal) || !literal.IsDefaultValue())
								ReportError(expr.Value, "Only null, zero and false supported as an array initializer");
						}
						Coerce(expr.Value, type);
					}
				}
			}
			this.CurrentScope.Add(expr);
		}

		CiExpr VisitExpr(CiExpr expr)
		{
			switch (expr) {
			case CiAggregateInitializer aggregate:
				List<CiExpr> items = aggregate.Items;
				for (int i = 0; i < items.Count; i++)
					items[i] = VisitExpr(items[i]);
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
				return VisitPostfixExpr(postfix);
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

		CiExpr ResolveBool(CiExpr expr)
		{
			expr = VisitExpr(expr);
			Coerce(expr, this.Program.System.BoolType);
			return expr;
		}

		static CiClassType CreateClassPtr(CiClass klass, CiToken ptrModifier, bool nullable)
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
			ptr.Nullable = nullable;
			return ptr;
		}

		void FillGenericClass(CiClassType result, CiClass klass, CiAggregateInitializer typeArgExprs)
		{
			List<CiType> typeArgs = new List<CiType>();
			foreach (CiExpr typeArgExpr in typeArgExprs.Items)
				typeArgs.Add(ToType(typeArgExpr, false));
			if (typeArgs.Count != klass.TypeParameterCount) {
				ReportError(result, $"Expected {klass.TypeParameterCount} type arguments for {klass.Name}, got {typeArgs.Count}");
				return;
			}
			result.Class = klass;
			result.TypeArg0 = typeArgs[0];
			if (typeArgs.Count == 2)
				result.TypeArg1 = typeArgs[1];
		}

		void ExpectNoPtrModifier(CiExpr expr, CiToken ptrModifier, bool nullable)
		{
			if (ptrModifier != CiToken.EndOfFile)
				ReportError(expr, $"Unexpected {CiLexer.TokenToString(ptrModifier)} on a non-reference type");
			if (nullable)
				ReportError(expr, "Nullable value types not supported");
		}

		CiType ToBaseType(CiExpr expr, CiToken ptrModifier, bool nullable)
		{
			switch (expr) {
			case CiSymbolReference symbol:
				if (this.Program.TryLookup(symbol.Name, true) is CiType type) {
					if (type is CiClass klass) {
						if (klass.Id == CiId.MatchClass && ptrModifier != CiToken.EndOfFile)
							ReportError(expr, "Read-write references to the built-in class Match are not supported");
						CiClassType ptr = CreateClassPtr(klass, ptrModifier, nullable);
						if (symbol.Left is CiAggregateInitializer typeArgExprs)
							FillGenericClass(ptr, klass, typeArgExprs);
						else if (symbol.Left != null)
							return PoisonError(expr, "Invalid type");
						else
							ptr.Name = klass.Name;
						return ptr;
					}
					else if (symbol.Left != null)
						return PoisonError(expr, "Invalid type");
					if (type.Id == CiId.StringPtrType && nullable) {
						type = this.Program.System.StringNullablePtrType;
						nullable = false;
					}
					ExpectNoPtrModifier(expr, ptrModifier, nullable);
					return type;
				}
				return PoisonError(expr, $"Type {symbol.Name} not found");
			case CiCallExpr call:
				ExpectNoPtrModifier(expr, ptrModifier, nullable);
				if (call.Arguments.Count != 0)
					ReportError(call, "Expected empty parentheses for storage type");
				if (call.Method.Left is CiAggregateInitializer typeArgExprs2) {
					CiStorageType storage = new CiStorageType { Line = call.Line };
					if (this.Program.TryLookup(call.Method.Name, true) is CiClass klass) {
						FillGenericClass(storage, klass, typeArgExprs2);
						return storage;
					}
					return PoisonError(typeArgExprs2, $"{call.Method.Name} is not a class");
				}
				else if (call.Method.Left != null)
					return PoisonError(expr, "Invalid type");
				if (call.Method.Name == "string")
					return this.Program.System.StringStorageType;
				if (this.Program.TryLookup(call.Method.Name, true) is CiClass klass2)
					return new CiStorageType { Class = klass2 };
				return PoisonError(expr, $"Class {call.Method.Name} not found");
			default:
				return PoisonError(expr, "Invalid type");
			}
		}

		CiType ToType(CiExpr expr, bool dynamic)
		{
			CiExpr minExpr = null;
			if (expr is CiBinaryExpr range && range.Op == CiToken.Range) {
				minExpr = range.Left;
				expr = range.Right;
			}
			bool nullable;
			CiToken ptrModifier;
			CiClassType outerArray = null;
			CiClassType innerArray = null;
			for (;;) {
				if (expr is CiPostfixExpr question && question.Op == CiToken.QuestionMark) {
					expr = question.Inner;
					nullable = true;
				}
				else
					nullable = false;
				if (expr is CiPostfixExpr postfix && (postfix.Op == CiToken.ExclamationMark || postfix.Op == CiToken.Hash)) {
					expr = postfix.Inner;
					ptrModifier = postfix.Op;
				}
				else
					ptrModifier = CiToken.EndOfFile;
				if (expr is CiBinaryExpr binary && binary.Op == CiToken.LeftBracket) {
					if (binary.Right != null) {
						ExpectNoPtrModifier(expr, ptrModifier, nullable);
						CiExpr lengthExpr = VisitExpr(binary.Right);
						CiArrayStorageType arrayStorage = new CiArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = outerArray, LengthExpr = lengthExpr, Length = 0 };
						if (Coerce(lengthExpr, this.Program.System.IntType) && (!dynamic || binary.Left.IsIndexing())) {
							if (lengthExpr is CiLiteralLong literal) {
								long length = literal.Value;
								if (length < 0)
									ReportError(expr, "Expected non-negative integer");
								else if (length > 2147483647)
									ReportError(expr, "Integer too big");
								else
									arrayStorage.Length = (int) length;
							}
							else
								ReportError(lengthExpr, "Expected constant value");
						}
						outerArray = arrayStorage;
					}
					else {
						CiType elementType = outerArray;
						outerArray = CreateClassPtr(this.Program.System.ArrayPtrClass, ptrModifier, nullable);
						outerArray.TypeArg0 = elementType;
					}
					if (innerArray == null)
						innerArray = outerArray;
					expr = binary.Left;
				}
				else
					break;
			}
			CiType baseType;
			if (minExpr != null) {
				ExpectNoPtrModifier(expr, ptrModifier, nullable);
				int min = FoldConstInt(minExpr);
				int max = FoldConstInt(expr);
				if (min > max)
					return PoisonError(expr, "Range min greater than max");
				baseType = CiRangeType.New(min, max);
			}
			else
				baseType = ToBaseType(expr, ptrModifier, nullable);
			baseType.Line = expr.Line;
			if (outerArray == null)
				return baseType;
			innerArray.TypeArg0 = baseType;
			return outerArray;
		}

		CiType ResolveType(CiNamedValue def)
		{
			def.Type = ToType(def.TypeExpr, false);
			return def.Type;
		}

		void VisitAssert(CiAssert statement)
		{
			statement.Cond = ResolveBool(statement.Cond);
			if (statement.Message != null) {
				statement.Message = VisitExpr(statement.Message);
				if (!(statement.Message.Type is CiStringType))
					ReportError(statement, "The second argument of 'assert' must be a string");
			}
		}

		bool ResolveStatements(List<CiStatement> statements)
		{
			bool reachable = true;
			foreach (CiStatement statement in statements) {
				if (statement is CiConst konst) {
					ResolveConst(konst);
					this.CurrentScope.Add(konst);
					if (konst.Type is CiArrayStorageType) {
						CiClass klass = (CiClass) this.CurrentScope.GetContainer();
						klass.ConstArrays.Add(konst);
					}
				}
				else
					VisitStatement(statement);
				if (!reachable) {
					ReportError(statement, "Unreachable statement");
					return false;
				}
				reachable = statement.CompletesNormally();
			}
			return reachable;
		}

		void VisitBlock(CiBlock statement)
		{
			OpenScope(statement);
			statement.SetCompletesNormally(ResolveStatements(statement.Statements));
			CloseScope();
		}

		void ResolveLoopCond(CiLoop statement)
		{
			if (statement.Cond != null) {
				statement.Cond = ResolveBool(statement.Cond);
				statement.SetCompletesNormally(!(statement.Cond is CiLiteralTrue));
			}
			else
				statement.SetCompletesNormally(false);
		}

		void VisitDoWhile(CiDoWhile statement)
		{
			OpenScope(statement);
			ResolveLoopCond(statement);
			VisitStatement(statement.Body);
			CloseScope();
		}

		void VisitFor(CiFor statement)
		{
			OpenScope(statement);
			if (statement.Init != null)
				VisitStatement(statement.Init);
			ResolveLoopCond(statement);
			if (statement.Advance != null)
				VisitStatement(statement.Advance);
			if (statement.Init is CiVar iter && iter.Type is CiIntegerType && iter.Value != null && statement.Cond is CiBinaryExpr cond && cond.Left.IsReferenceTo(iter) && (cond.Right is CiLiteral || (cond.Right is CiSymbolReference limitSymbol && limitSymbol.Symbol is CiVar))) {
				long step = 0;
				switch (statement.Advance) {
				case CiUnaryExpr unary when unary.Inner != null && unary.Inner.IsReferenceTo(iter):
					switch (unary.Op) {
					case CiToken.Increment:
						step = 1;
						break;
					case CiToken.Decrement:
						step = -1;
						break;
					default:
						break;
					}
					break;
				case CiBinaryExpr binary when binary.Left.IsReferenceTo(iter) && binary.Right is CiLiteralLong literalStep:
					switch (binary.Op) {
					case CiToken.AddAssign:
						step = literalStep.Value;
						break;
					case CiToken.SubAssign:
						step = -literalStep.Value;
						break;
					default:
						break;
					}
					break;
				default:
					break;
				}
				if ((step > 0 && (cond.Op == CiToken.Less || cond.Op == CiToken.LessOrEqual)) || (step < 0 && (cond.Op == CiToken.Greater || cond.Op == CiToken.GreaterOrEqual))) {
					statement.IsRange = true;
					statement.RangeStep = step;
				}
				statement.IsIteratorUsed = false;
			}
			VisitStatement(statement.Body);
			CloseScope();
		}

		void VisitForeach(CiForeach statement)
		{
			OpenScope(statement);
			CiVar element = statement.GetVar();
			ResolveType(element);
			VisitExpr(statement.Collection);
			if (statement.Collection.Type is CiClassType klass) {
				switch (klass.Class.Id) {
				case CiId.StringClass:
					if (statement.Count() != 1 || !element.Type.IsAssignableFrom(this.Program.System.IntType))
						ReportError(statement, "Expected int iterator variable");
					break;
				case CiId.ArrayStorageClass:
				case CiId.ListClass:
				case CiId.HashSetClass:
				case CiId.SortedSetClass:
					if (statement.Count() != 1)
						ReportError(statement, "Expected one iterator variable");
					else if (!element.Type.IsAssignableFrom(klass.GetElementType()))
						ReportError(statement, $"Cannot coerce {klass.GetElementType()} to {element.Type}");
					break;
				case CiId.DictionaryClass:
				case CiId.SortedDictionaryClass:
				case CiId.OrderedDictionaryClass:
					if (statement.Count() != 2)
						ReportError(statement, "Expected (TKey key, TValue value) iterator");
					else {
						CiVar value = statement.GetValueVar();
						ResolveType(value);
						if (!element.Type.IsAssignableFrom(klass.GetKeyType()))
							ReportError(statement, $"Cannot coerce {klass.GetKeyType()} to {element.Type}");
						else if (!value.Type.IsAssignableFrom(klass.GetValueType()))
							ReportError(statement, $"Cannot coerce {klass.GetValueType()} to {value.Type}");
					}
					break;
				default:
					ReportError(statement, $"'foreach' invalid on {klass.Class.Name}");
					break;
				}
			}
			else
				ReportError(statement, $"'foreach' invalid on {statement.Collection.Type}");
			statement.SetCompletesNormally(true);
			VisitStatement(statement.Body);
			CloseScope();
		}

		void VisitIf(CiIf statement)
		{
			statement.Cond = ResolveBool(statement.Cond);
			VisitStatement(statement.OnTrue);
			if (statement.OnFalse != null) {
				VisitStatement(statement.OnFalse);
				statement.SetCompletesNormally(statement.OnTrue.CompletesNormally() || statement.OnFalse.CompletesNormally());
			}
			else
				statement.SetCompletesNormally(true);
		}

		void VisitLock(CiLock statement)
		{
			statement.Lock = VisitExpr(statement.Lock);
			Coerce(statement.Lock, this.Program.System.LockPtrType);
			VisitStatement(statement.Body);
		}

		void VisitReturn(CiReturn statement)
		{
			if (this.CurrentMethod.Type.Id == CiId.VoidType) {
				if (statement.Value != null)
					ReportError(statement, "Void method cannot return a value");
			}
			else if (statement.Value == null)
				ReportError(statement, "Missing return value");
			else {
				statement.Value = VisitExpr(statement.Value);
				Coerce(statement.Value, this.CurrentMethod.Type);
				if (statement.Value is CiSymbolReference symbol && symbol.Symbol is CiVar local && ((local.Type.IsFinal() && !(this.CurrentMethod.Type is CiStorageType)) || (local.Type.Id == CiId.StringStorageType && this.CurrentMethod.Type.Id != CiId.StringStorageType)))
					ReportError(statement, "Returning dangling reference to local storage");
			}
		}

		void VisitSwitch(CiSwitch statement)
		{
			OpenScope(statement);
			statement.Value = VisitExpr(statement.Value);
			switch (statement.Value.Type) {
			case CiIntegerType i when i.Id != CiId.LongType:
			case CiEnum _:
				break;
			case CiClassType klass when !(klass is CiStorageType):
				break;
			default:
				ReportError(statement.Value, $"Switch on type {statement.Value.Type} - expected int, enum, string or object reference");
				return;
			}
			statement.SetCompletesNormally(false);
			foreach (CiCase kase in statement.Cases) {
				for (int i = 0; i < kase.Values.Count; i++) {
					if (statement.Value.Type is CiClassType switchPtr && switchPtr.Class.Id != CiId.StringClass) {
						CiExpr value = kase.Values[i];
						if (value is CiBinaryExpr when1 && when1.Op == CiToken.When)
							value = when1.Left;
						if (value is CiLiteralNull) {
						}
						else if (!(value is CiVar def) || def.Value != null)
							ReportError(kase.Values[i], "Expected 'case Type name'");
						else if (!(ResolveType(def) is CiClassType casePtr) || casePtr is CiStorageType)
							ReportError(def, "'case' with non-reference type");
						else if (casePtr is CiReadWriteClassType && !(switchPtr is CiDynamicPtrType) && (casePtr is CiDynamicPtrType || !(switchPtr is CiReadWriteClassType)))
							ReportError(def, $"{switchPtr} cannot be casted to {casePtr}");
						else if (casePtr.Class.IsSameOrBaseOf(switchPtr.Class))
							ReportError(def, $"{statement.Value} is {switchPtr}, 'case {casePtr}' would always match");
						else if (!switchPtr.Class.IsSameOrBaseOf(casePtr.Class))
							ReportError(def, $"{switchPtr} is not base class of {casePtr.Class.Name}, 'case {casePtr}' would never match");
						else {
							statement.Add(def);
							if (kase.Values[i] is CiBinaryExpr when2 && when2.Op == CiToken.When)
								when2.Right = ResolveBool(when2.Right);
						}
					}
					else if (kase.Values[i] is CiBinaryExpr when1 && when1.Op == CiToken.When) {
						when1.Left = FoldConst(when1.Left);
						Coerce(when1.Left, statement.Value.Type);
						when1.Right = ResolveBool(when1.Right);
					}
					else {
						kase.Values[i] = FoldConst(kase.Values[i]);
						Coerce(kase.Values[i], statement.Value.Type);
					}
				}
				if (ResolveStatements(kase.Body))
					ReportError(kase.Body[^1], "Case must end with break, continue, return or throw");
			}
			if (statement.DefaultBody.Count > 0) {
				bool reachable = ResolveStatements(statement.DefaultBody);
				if (reachable)
					ReportError(statement.DefaultBody[^1], "Default must end with break, continue, return or throw");
			}
			CloseScope();
		}

		void VisitThrow(CiThrow statement)
		{
			if (!this.CurrentMethod.Throws)
				ReportError(statement, "'throw' in a method not marked 'throws'");
			statement.Message = VisitExpr(statement.Message);
			if (!(statement.Message.Type is CiStringType))
				ReportError(statement, "The argument of 'throw' must be a string");
		}

		void VisitWhile(CiWhile statement)
		{
			OpenScope(statement);
			ResolveLoopCond(statement);
			VisitStatement(statement.Body);
			CloseScope();
		}

		void VisitStatement(CiStatement statement)
		{
			switch (statement) {
			case CiAssert asrt:
				VisitAssert(asrt);
				break;
			case CiBlock block:
				VisitBlock(block);
				break;
			case CiBreak brk:
				brk.LoopOrSwitch.SetCompletesNormally(true);
				break;
			case CiContinue _:
			case CiNative _:
				break;
			case CiDoWhile doWhile:
				VisitDoWhile(doWhile);
				break;
			case CiFor forLoop:
				VisitFor(forLoop);
				break;
			case CiForeach foreachLoop:
				VisitForeach(foreachLoop);
				break;
			case CiIf ifStatement:
				VisitIf(ifStatement);
				break;
			case CiLock lockStatement:
				VisitLock(lockStatement);
				break;
			case CiReturn ret:
				VisitReturn(ret);
				break;
			case CiSwitch switchStatement:
				VisitSwitch(switchStatement);
				break;
			case CiThrow throwStatement:
				VisitThrow(throwStatement);
				break;
			case CiWhile whileStatement:
				VisitWhile(whileStatement);
				break;
			case CiExpr expr:
				VisitExpr(expr);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		CiExpr FoldConst(CiExpr expr)
		{
			expr = VisitExpr(expr);
			if (expr is CiLiteral || expr.IsConstEnum())
				return expr;
			ReportError(expr, "Expected constant value");
			return expr;
		}

		int FoldConstInt(CiExpr expr)
		{
			if (FoldConst(expr) is CiLiteralLong literal) {
				long l = literal.Value;
				if (l < -2147483648 || l > 2147483647) {
					ReportError(expr, "Only 32-bit ranges supported");
					return 0;
				}
				return (int) l;
			}
			ReportError(expr, "Expected integer");
			return 0;
		}

		void ResolveTypes(CiClass klass)
		{
			this.CurrentScope = klass;
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				switch (symbol) {
				case CiField field:
					CiType type = ResolveType(field);
					if (field.Value != null) {
						field.Value = VisitExpr(field.Value);
						if (!field.IsAssignableStorage())
							Coerce(field.Value, type is CiArrayStorageType array ? array.GetElementType() : type);
					}
					break;
				case CiMethod method:
					if (method.TypeExpr == this.Program.System.VoidType)
						method.Type = this.Program.System.VoidType;
					else
						ResolveType(method);
					for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
						ResolveType(param);
						if (param.Value != null) {
							param.Value = FoldConst(param.Value);
							Coerce(param.Value, param.Type);
						}
					}
					if (method.CallType == CiCallType.Override || method.CallType == CiCallType.Sealed) {
						if (klass.Parent.TryLookup(method.Name, false) is CiMethod baseMethod) {
							switch (baseMethod.CallType) {
							case CiCallType.Abstract:
							case CiCallType.Virtual:
							case CiCallType.Override:
								break;
							default:
								ReportError(method, "Base method is not abstract or virtual");
								break;
							}
							if (!method.Type.EqualsType(baseMethod.Type))
								ReportError(method, "Base method has a different return type");
							if (method.IsMutator != baseMethod.IsMutator) {
								if (method.IsMutator)
									ReportError(method, "Mutating method cannot override a non-mutating method");
								else
									ReportError(method, "Non-mutating method cannot override a mutating method");
							}
							CiVar baseParam = baseMethod.Parameters.FirstParameter();
							for (CiVar param = method.Parameters.FirstParameter();; param = param.NextParameter()) {
								if (param == null) {
									if (baseParam != null)
										ReportError(method, "Fewer parameters than the overridden method");
									break;
								}
								if (baseParam == null) {
									ReportError(method, "More parameters than the overridden method");
									break;
								}
								if (!param.Type.EqualsType(baseParam.Type)) {
									ReportError(method, "Base method has a different parameter type");
									break;
								}
								baseParam = baseParam.NextParameter();
							}
							baseMethod.Calls.Add(method);
						}
						else
							ReportError(method, "No method to override");
					}
					break;
				default:
					break;
				}
			}
		}

		void ResolveConst(CiConst konst)
		{
			switch (konst.VisitStatus) {
			case CiVisitStatus.NotYet:
				break;
			case CiVisitStatus.InProgress:
				konst.Value = PoisonError(konst, $"Circular dependency in value of constant {konst.Name}");
				konst.VisitStatus = CiVisitStatus.Done;
				return;
			case CiVisitStatus.Done:
				return;
			}
			konst.VisitStatus = CiVisitStatus.InProgress;
			if (!(this.CurrentScope is CiEnum))
				ResolveType(konst);
			konst.Value = VisitExpr(konst.Value);
			if (konst.Value is CiAggregateInitializer coll) {
				if (konst.Type is CiClassType array) {
					CiType elementType = array.GetElementType();
					if (array is CiArrayStorageType arrayStg) {
						if (arrayStg.Length != coll.Items.Count)
							ReportError(konst, $"Declared {arrayStg.Length} elements, initialized {coll.Items.Count}");
					}
					else if (array is CiReadWriteClassType)
						ReportError(konst, "Invalid constant type");
					else
						konst.Type = new CiArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = elementType, Length = coll.Items.Count };
					coll.Type = konst.Type;
					foreach (CiExpr item in coll.Items)
						Coerce(item, elementType);
				}
				else
					ReportError(konst, $"Array initializer for scalar constant {konst.Name}");
			}
			else if (this.CurrentScope is CiEnum && konst.Value.Type is CiRangeType && konst.Value is CiLiteral) {
			}
			else if (konst.Value is CiLiteral || konst.Value.IsConstEnum())
				Coerce(konst.Value, konst.Type);
			else if (konst.Value != this.Poison)
				ReportError(konst.Value, $"Value for constant {konst.Name} is not constant");
			konst.InMethod = this.CurrentMethod;
			konst.VisitStatus = CiVisitStatus.Done;
		}

		void ResolveConsts(CiContainerType container)
		{
			this.CurrentScope = container;
			switch (container) {
			case CiClass klass:
				for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
					if (symbol is CiConst konst)
						ResolveConst(konst);
				}
				break;
			case CiEnum enu:
				CiConst previous = null;
				for (CiSymbol symbol = enu.First; symbol != null; symbol = symbol.Next) {
					if (symbol is CiConst konst) {
						if (konst.Value != null) {
							ResolveConst(konst);
							enu.HasExplicitValue = true;
						}
						else
							konst.Value = new CiImplicitEnumValue { Value = previous == null ? 0 : previous.Value.IntValue() + 1 };
						previous = konst;
					}
				}
				break;
			default:
				throw new NotImplementedException();
			}
		}

		void ResolveCode(CiClass klass)
		{
			if (klass.Constructor != null) {
				this.CurrentScope = klass;
				this.CurrentMethod = klass.Constructor;
				VisitStatement(klass.Constructor.Body);
				this.CurrentMethod = null;
			}
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiMethod method) {
					if (method.Name == "ToString" && method.CallType != CiCallType.Static && method.Parameters.Count() == 0)
						method.Id = CiId.ClassToString;
					if (method.Body != null) {
						this.CurrentScope = method.Parameters;
						this.CurrentMethod = method;
						if (!(method.Body is CiScope))
							OpenScope(method.MethodScope);
						VisitStatement(method.Body);
						if (method.Type.Id != CiId.VoidType && method.Body.CompletesNormally())
							ReportError(method.Body, "Method can complete without a return value");
						this.CurrentMethod = null;
					}
				}
			}
		}

		static void MarkMethodLive(CiMethodBase method)
		{
			if (method.IsLive)
				return;
			method.IsLive = true;
			foreach (CiMethod called in method.Calls)
				MarkMethodLive(called);
		}

		static void MarkClassLive(CiClass klass)
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

		public void Process(CiProgram program)
		{
			this.Program = program;
			for (CiSymbol type = program.First; type != null; type = type.Next) {
				if (type is CiClass klass)
					ResolveBase(klass);
			}
			foreach (CiClass klass in program.Classes)
				CheckBaseCycle(klass);
			for (CiSymbol type = program.First; type != null; type = type.Next) {
				CiContainerType container = (CiContainerType) type;
				ResolveConsts(container);
			}
			foreach (CiClass klass in program.Classes)
				ResolveTypes(klass);
			foreach (CiClass klass in program.Classes)
				ResolveCode(klass);
			foreach (CiClass klass in program.Classes)
				MarkClassLive(klass);
		}
	}

	public abstract class GenHost
	{

		public abstract TextWriter CreateFile(string directory, string filename);

		public abstract void CloseFile();
	}

	public abstract class GenBase : CiVisitor
	{

		internal string Namespace;

		internal string OutputFile;

		internal GenHost Host;

		TextWriter Writer;

		readonly StringWriter StringWriter = new StringWriter();

		protected int Indent = 0;

		protected bool AtLineStart = true;

		bool AtChildStart = false;

		bool InChildBlock = false;

		protected bool InHeaderFile = false;

		readonly SortedDictionary<string, bool> Includes = new SortedDictionary<string, bool>();

		protected CiMethodBase CurrentMethod = null;

		protected readonly HashSet<CiClass> WrittenClasses = new HashSet<CiClass>();

		protected readonly List<CiExpr> CurrentTemporaries = new List<CiExpr>();

		protected override CiContainerType GetCurrentContainer()
		{
			CiClass klass = (CiClass) this.CurrentMethod.Parent;
			return klass;
		}

		protected abstract string GetTargetName();

		protected void NotSupported(CiStatement statement, string feature)
		{
			ReportError(statement, $"{feature} not supported when targeting {GetTargetName()}");
		}

		protected void NotYet(CiStatement statement, string feature)
		{
			ReportError(statement, $"{feature} not supported yet when targeting {GetTargetName()}");
		}

		protected virtual void StartLine()
		{
			if (this.AtLineStart) {
				if (this.AtChildStart) {
					this.AtChildStart = false;
					this.Writer.Write('\n');
					this.Indent++;
				}
				for (int i = 0; i < this.Indent; i++)
					this.Writer.Write('\t');
				this.AtLineStart = false;
			}
		}

		protected void WriteChar(int c)
		{
			StartLine();
			this.Writer.Write(new Rune(c));
		}

		protected void Write(string s)
		{
			StartLine();
			this.Writer.Write(s);
		}

		public override void VisitLiteralNull()
		{
			Write("null");
		}

		public override void VisitLiteralFalse()
		{
			Write("false");
		}

		public override void VisitLiteralTrue()
		{
			Write("true");
		}

		public override void VisitLiteralLong(long i)
		{
			this.Writer.Write(i);
		}

		protected virtual int GetLiteralChars() => 0;

		public override void VisitLiteralChar(int c)
		{
			if (c < GetLiteralChars()) {
				WriteChar('\'');
				switch (c) {
				case '\n':
					Write("\\n");
					break;
				case '\r':
					Write("\\r");
					break;
				case '\t':
					Write("\\t");
					break;
				case '\'':
					Write("\\'");
					break;
				case '\\':
					Write("\\\\");
					break;
				default:
					WriteChar(c);
					break;
				}
				WriteChar('\'');
			}
			else
				this.Writer.Write(c);
		}

		public override void VisitLiteralDouble(double value)
		{
			string s = $"{value}";
			Write(s);
			foreach (int c in s) {
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
			Write(".0");
		}

		public override void VisitLiteralString(string value)
		{
			WriteChar('"');
			Write(value);
			WriteChar('"');
		}

		void WriteLowercaseChar(int c)
		{
			if (c >= 'A' && c <= 'Z')
				c += 32;
			this.Writer.Write((char) c);
		}

		void WriteUppercaseChar(int c)
		{
			if (c >= 'a' && c <= 'z')
				c -= 32;
			this.Writer.Write((char) c);
		}

		protected void WriteLowercase(string s)
		{
			StartLine();
			foreach (int c in s)
				WriteLowercaseChar(c);
		}

		protected void WriteCamelCase(string s)
		{
			StartLine();
			WriteLowercaseChar(s[0]);
			this.Writer.Write(s.Substring(1));
		}

		protected void WritePascalCase(string s)
		{
			StartLine();
			WriteUppercaseChar(s[0]);
			this.Writer.Write(s.Substring(1));
		}

		protected void WriteUppercaseWithUnderscores(string s)
		{
			StartLine();
			bool first = true;
			foreach (int c in s) {
				if (!first && c >= 'A' && c <= 'Z') {
					this.Writer.Write('_');
					this.Writer.Write((char) c);
				}
				else
					WriteUppercaseChar(c);
				first = false;
			}
		}

		protected void WriteLowercaseWithUnderscores(string s)
		{
			StartLine();
			bool first = true;
			foreach (int c in s) {
				if (c >= 'A' && c <= 'Z') {
					if (!first)
						this.Writer.Write('_');
					WriteLowercaseChar(c);
				}
				else
					this.Writer.Write((char) c);
				first = false;
			}
		}

		protected void WriteNewLine()
		{
			this.Writer.Write('\n');
			this.AtLineStart = true;
		}

		protected void WriteCharLine(int c)
		{
			WriteChar(c);
			WriteNewLine();
		}

		protected void WriteLine(string s)
		{
			Write(s);
			WriteNewLine();
		}

		protected abstract void WriteName(CiSymbol symbol);

		protected virtual void WriteBanner()
		{
			WriteLine("// Generated automatically with \"cito\". Do not edit.");
		}

		protected void CreateFile(string directory, string filename)
		{
			this.Writer = this.Host.CreateFile(directory, filename);
			WriteBanner();
		}

		protected void CreateOutputFile()
		{
			CreateFile(null, this.OutputFile);
		}

		protected void CloseFile()
		{
			this.Host.CloseFile();
		}

		protected void OpenStringWriter()
		{
			this.Writer = this.StringWriter;
		}

		protected void CloseStringWriter()
		{
			this.Writer.Write(this.StringWriter.ToString());
			this.StringWriter.GetStringBuilder().Clear();
		}

		protected void Include(string name)
		{
			if (!this.Includes.ContainsKey(name))
				this.Includes[name] = this.InHeaderFile;
		}

		protected void WriteIncludes(string prefix, string suffix)
		{
			foreach ((string name, bool inHeaderFile) in this.Includes) {
				if (inHeaderFile == this.InHeaderFile) {
					Write(prefix);
					Write(name);
					WriteLine(suffix);
				}
			}
			if (!this.InHeaderFile)
				this.Includes.Clear();
		}

		protected virtual void StartDocLine()
		{
			Write(" * ");
		}

		protected void WriteXmlDoc(string text)
		{
			foreach (int c in text) {
				switch (c) {
				case '&':
					Write("&amp;");
					break;
				case '<':
					Write("&lt;");
					break;
				case '>':
					Write("&gt;");
					break;
				default:
					WriteChar(c);
					break;
				}
			}
		}

		protected virtual void WriteDocPara(CiDocPara para, bool many)
		{
			if (many) {
				WriteNewLine();
				Write(" * <p>");
			}
			foreach (CiDocInline inline in para.Children) {
				switch (inline) {
				case CiDocText text:
					WriteXmlDoc(text.Text);
					break;
				case CiDocCode code:
					Write("<code>");
					WriteXmlDoc(code.Text);
					Write("</code>");
					break;
				case CiDocLine _:
					WriteNewLine();
					StartDocLine();
					break;
				default:
					throw new NotImplementedException();
				}
			}
		}

		protected virtual void WriteDocList(CiDocList list)
		{
			WriteNewLine();
			WriteLine(" * <ul>");
			foreach (CiDocPara item in list.Items) {
				Write(" * <li>");
				WriteDocPara(item, false);
				WriteLine("</li>");
			}
			Write(" * </ul>");
		}

		protected void WriteDocBlock(CiDocBlock block, bool many)
		{
			switch (block) {
			case CiDocPara para:
				WriteDocPara(para, many);
				break;
			case CiDocList list:
				WriteDocList(list);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected void WriteContent(CiCodeDoc doc)
		{
			StartDocLine();
			WriteDocPara(doc.Summary, false);
			WriteNewLine();
			if (doc.Details.Count > 0) {
				StartDocLine();
				if (doc.Details.Count == 1)
					WriteDocBlock(doc.Details[0], false);
				else {
					foreach (CiDocBlock block in doc.Details)
						WriteDocBlock(block, true);
				}
				WriteNewLine();
			}
		}

		protected virtual void WriteDoc(CiCodeDoc doc)
		{
			if (doc != null) {
				WriteLine("/**");
				WriteContent(doc);
				WriteLine(" */");
			}
		}

		protected virtual void WriteSelfDoc(CiMethod method)
		{
		}

		protected virtual void WriteParameterDoc(CiVar param, bool first)
		{
			Write(" * @param ");
			WriteName(param);
			WriteChar(' ');
			WriteDocPara(param.Documentation.Summary, false);
			WriteNewLine();
		}

		protected void WriteParametersDoc(CiMethod method)
		{
			bool first = true;
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (param.Documentation != null) {
					WriteParameterDoc(param, first);
					first = false;
				}
			}
		}

		protected void WriteMethodDoc(CiMethod method)
		{
			if (method.Documentation == null)
				return;
			WriteLine("/**");
			WriteContent(method.Documentation);
			WriteSelfDoc(method);
			WriteParametersDoc(method);
			WriteLine(" */");
		}

		protected void WriteTopLevelNatives(CiProgram program)
		{
			foreach (string content in program.TopLevelNatives)
				Write(content);
		}

		protected void OpenBlock()
		{
			WriteCharLine('{');
			this.Indent++;
		}

		protected void CloseBlock()
		{
			this.Indent--;
			WriteCharLine('}');
		}

		protected virtual void EndStatement()
		{
			WriteCharLine(';');
		}

		protected void WriteComma(int i)
		{
			if (i > 0) {
				if ((i & 15) == 0) {
					WriteCharLine(',');
					WriteChar('\t');
				}
				else
					Write(", ");
			}
		}

		protected void WriteBytes(List<byte> content)
		{
			int i = 0;
			foreach (int b in content) {
				WriteComma(i++);
				VisitLiteralLong(b);
			}
		}

		protected virtual CiId GetTypeId(CiType type, bool promote) => promote && type is CiRangeType ? CiId.IntType : type.Id;

		protected abstract void WriteTypeAndName(CiNamedValue value);

		protected virtual void WriteLocalName(CiSymbol symbol, CiPriority parent)
		{
			if (symbol is CiField)
				Write("this.");
			WriteName(symbol);
		}

		protected void WriteDoubling(string s, int doubled)
		{
			foreach (int c in s) {
				if (c == doubled)
					WriteChar(c);
				WriteChar(c);
			}
		}

		protected virtual void WritePrintfWidth(CiInterpolatedPart part)
		{
			if (part.WidthExpr != null)
				VisitLiteralLong(part.Width);
			if (part.Precision >= 0) {
				WriteChar('.');
				VisitLiteralLong(part.Precision);
			}
		}

		static int GetPrintfFormat(CiType type, int format)
		{
			switch (type) {
			case CiIntegerType _:
				return format == 'x' || format == 'X' ? format : 'd';
			case CiNumericType _:
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
			case CiClassType _:
				return 's';
			default:
				throw new NotImplementedException();
			}
		}

		protected void WritePrintfFormat(CiInterpolatedString expr)
		{
			foreach (CiInterpolatedPart part in expr.Parts) {
				WriteDoubling(part.Prefix, '%');
				WriteChar('%');
				WritePrintfWidth(part);
				WriteChar(GetPrintfFormat(part.Argument.Type, part.Format));
			}
			WriteDoubling(expr.Suffix, '%');
		}

		protected void WritePyFormat(CiInterpolatedPart part)
		{
			if (part.WidthExpr != null || part.Precision >= 0 || (part.Format != ' ' && part.Format != 'D'))
				WriteChar(':');
			if (part.WidthExpr != null) {
				if (part.Width >= 0) {
					if (!(part.Argument.Type is CiNumericType))
						WriteChar('>');
					VisitLiteralLong(part.Width);
				}
				else {
					WriteChar('<');
					VisitLiteralLong(-part.Width);
				}
			}
			if (part.Precision >= 0) {
				WriteChar(part.Argument.Type is CiIntegerType ? '0' : '.');
				VisitLiteralLong(part.Precision);
			}
			if (part.Format != ' ' && part.Format != 'D')
				WriteChar(part.Format);
			WriteChar('}');
		}

		protected virtual void WriteInterpolatedStringArg(CiExpr expr)
		{
			expr.Accept(this, CiPriority.Argument);
		}

		protected void WriteInterpolatedStringArgs(CiInterpolatedString expr)
		{
			foreach (CiInterpolatedPart part in expr.Parts) {
				Write(", ");
				WriteInterpolatedStringArg(part.Argument);
			}
		}

		protected void WritePrintf(CiInterpolatedString expr, bool newLine)
		{
			WriteChar('"');
			WritePrintfFormat(expr);
			if (newLine)
				Write("\\n");
			WriteChar('"');
			WriteInterpolatedStringArgs(expr);
			WriteChar(')');
		}

		protected void WritePostfix(CiExpr obj, string s)
		{
			obj.Accept(this, CiPriority.Primary);
			Write(s);
		}

		protected void WriteCall(string function, CiExpr arg0, CiExpr arg1 = null, CiExpr arg2 = null)
		{
			Write(function);
			WriteChar('(');
			arg0.Accept(this, CiPriority.Argument);
			if (arg1 != null) {
				Write(", ");
				arg1.Accept(this, CiPriority.Argument);
				if (arg2 != null) {
					Write(", ");
					arg2.Accept(this, CiPriority.Argument);
				}
			}
			WriteChar(')');
		}

		protected virtual void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
		{
			WriteChar('.');
		}

		protected void WriteMethodCall(CiExpr obj, string method, CiExpr arg0, CiExpr arg1 = null)
		{
			obj.Accept(this, CiPriority.Primary);
			WriteMemberOp(obj, null);
			WriteCall(method, arg0, arg1);
		}

		protected virtual void WriteSelectValues(CiType type, CiSelectExpr expr)
		{
			WriteCoerced(type, expr.OnTrue, CiPriority.Select);
			Write(" : ");
			WriteCoerced(type, expr.OnFalse, CiPriority.Select);
		}

		protected virtual void WriteCoercedSelect(CiType type, CiSelectExpr expr, CiPriority parent)
		{
			if (parent > CiPriority.Select)
				WriteChar('(');
			expr.Cond.Accept(this, CiPriority.SelectCond);
			Write(" ? ");
			WriteSelectValues(type, expr);
			if (parent > CiPriority.Select)
				WriteChar(')');
		}

		protected virtual void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
		{
			expr.Accept(this, parent);
		}

		protected void WriteCoerced(CiType type, CiExpr expr, CiPriority parent)
		{
			if (expr is CiSelectExpr select)
				WriteCoercedSelect(type, select, parent);
			else
				WriteCoercedInternal(type, expr, parent);
		}

		protected virtual void WriteCoercedExpr(CiType type, CiExpr expr)
		{
			WriteCoerced(type, expr, CiPriority.Argument);
		}

		protected virtual void WriteStronglyCoerced(CiType type, CiExpr expr)
		{
			WriteCoerced(type, expr, CiPriority.Argument);
		}

		protected virtual void WriteCoercedLiteral(CiType type, CiExpr literal)
		{
			literal.Accept(this, CiPriority.Argument);
		}

		protected void WriteCoercedLiterals(CiType type, List<CiExpr> exprs)
		{
			for (int i = 0; i < exprs.Count; i++) {
				WriteComma(i);
				WriteCoercedLiteral(type, exprs[i]);
			}
		}

		protected void WriteArgs(CiMethod method, List<CiExpr> args)
		{
			CiVar param = method.Parameters.FirstParameter();
			bool first = true;
			foreach (CiExpr arg in args) {
				if (!first)
					Write(", ");
				first = false;
				WriteStronglyCoerced(param.Type, arg);
				param = param.NextParameter();
			}
		}

		protected void WriteArgsInParentheses(CiMethod method, List<CiExpr> args)
		{
			WriteChar('(');
			WriteArgs(method, args);
			WriteChar(')');
		}

		protected abstract void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent);

		protected virtual void WriteNewArrayStorage(CiArrayStorageType array)
		{
			WriteNewArray(array.GetElementType(), array.LengthExpr, CiPriority.Argument);
		}

		protected abstract void WriteNew(CiReadWriteClassType klass, CiPriority parent);

		protected void WriteNewStorage(CiType type)
		{
			switch (type) {
			case CiArrayStorageType array:
				WriteNewArrayStorage(array);
				break;
			case CiStorageType storage:
				WriteNew(storage, CiPriority.Argument);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected virtual void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
		{
			Write(" = ");
			WriteNewArrayStorage(array);
		}

		protected virtual void WriteNewWithFields(CiReadWriteClassType type, CiAggregateInitializer init)
		{
			WriteNew(type, CiPriority.Argument);
		}

		protected virtual void WriteStorageInit(CiNamedValue def)
		{
			Write(" = ");
			if (def.Value is CiAggregateInitializer init) {
				CiReadWriteClassType klass = (CiReadWriteClassType) def.Type;
				WriteNewWithFields(klass, init);
			}
			else
				WriteNewStorage(def.Type);
		}

		protected virtual void WriteVarInit(CiNamedValue def)
		{
			if (def.IsAssignableStorage()) {
			}
			else if (def.Type is CiArrayStorageType array)
				WriteArrayStorageInit(array, def.Value);
			else if (def.Value != null && !(def.Value is CiAggregateInitializer)) {
				Write(" = ");
				WriteCoercedExpr(def.Type, def.Value);
			}
			else if (def.Type.IsFinal() && !(def.Parent is CiParameters))
				WriteStorageInit(def);
		}

		protected virtual void WriteVar(CiNamedValue def)
		{
			WriteTypeAndName(def);
			WriteVarInit(def);
		}

		public override void VisitVar(CiVar expr)
		{
			WriteVar(expr);
		}

		protected void WriteObjectLiteral(CiAggregateInitializer init, string separator)
		{
			string prefix = " { ";
			foreach (CiExpr item in init.Items) {
				Write(prefix);
				CiBinaryExpr assign = (CiBinaryExpr) item;
				CiSymbolReference field = (CiSymbolReference) assign.Left;
				WriteName(field.Symbol);
				Write(separator);
				WriteCoerced(assign.Left.Type, assign.Right, CiPriority.Argument);
				prefix = ", ";
			}
			Write(" }");
		}

		static CiAggregateInitializer GetAggregateInitializer(CiNamedValue def)
		{
			CiExpr expr = def.Value;
			if (expr is CiPrefixExpr unary)
				expr = unary.Inner;
			return expr is CiAggregateInitializer init ? init : null;
		}

		void WriteAggregateInitField(CiExpr obj, CiExpr item)
		{
			CiBinaryExpr assign = (CiBinaryExpr) item;
			CiSymbolReference field = (CiSymbolReference) assign.Left;
			WriteMemberOp(obj, field);
			WriteName(field.Symbol);
			Write(" = ");
			WriteCoerced(field.Type, assign.Right, CiPriority.Argument);
			EndStatement();
		}

		protected virtual void WriteInitCode(CiNamedValue def)
		{
			CiAggregateInitializer init = GetAggregateInitializer(def);
			if (init != null) {
				foreach (CiExpr item in init.Items) {
					WriteLocalName(def, CiPriority.Primary);
					WriteAggregateInitField(def, item);
				}
			}
		}

		protected virtual void DefineIsVar(CiBinaryExpr binary)
		{
			if (binary.Right is CiVar def) {
				EnsureChildBlock();
				WriteVar(def);
				EndStatement();
			}
		}

		protected void WriteArrayElement(CiNamedValue def, int nesting)
		{
			WriteLocalName(def, CiPriority.Primary);
			for (int i = 0; i < nesting; i++) {
				Write("[_i");
				VisitLiteralLong(i);
				WriteChar(']');
			}
		}

		protected void OpenLoop(string intString, int nesting, int count)
		{
			Write("for (");
			Write(intString);
			Write(" _i");
			VisitLiteralLong(nesting);
			Write(" = 0; _i");
			VisitLiteralLong(nesting);
			Write(" < ");
			VisitLiteralLong(count);
			Write("; _i");
			VisitLiteralLong(nesting);
			Write("++) ");
			OpenBlock();
		}

		protected void WriteResourceName(string name)
		{
			foreach (int c in name)
				WriteChar(CiLexer.IsLetterOrDigit(c) ? c : '_');
		}

		protected abstract void WriteResource(string name, int length);

		public override void VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Increment:
				Write("++");
				break;
			case CiToken.Decrement:
				Write("--");
				break;
			case CiToken.Minus:
				WriteChar('-');
				if (expr.Inner is CiPrefixExpr inner && (inner.Op == CiToken.Minus || inner.Op == CiToken.Decrement))
					WriteChar(' ');
				break;
			case CiToken.Tilde:
				WriteChar('~');
				break;
			case CiToken.ExclamationMark:
				WriteChar('!');
				break;
			case CiToken.New:
				CiDynamicPtrType dynamic = (CiDynamicPtrType) expr.Type;
				if (dynamic.Class.Id == CiId.ArrayPtrClass)
					WriteNewArray(dynamic.GetElementType(), expr.Inner, parent);
				else if (expr.Inner is CiAggregateInitializer init) {
					int tempId = this.CurrentTemporaries.IndexOf(expr);
					if (tempId >= 0) {
						Write("citemp");
						VisitLiteralLong(tempId);
					}
					else
						WriteNewWithFields(dynamic, init);
				}
				else
					WriteNew(dynamic, parent);
				return;
			case CiToken.Resource:
				CiLiteralString name = (CiLiteralString) expr.Inner;
				CiArrayStorageType array = (CiArrayStorageType) expr.Type;
				WriteResource(name.Value, array.Length);
				return;
			default:
				throw new NotImplementedException();
			}
			expr.Inner.Accept(this, CiPriority.Primary);
		}

		public override void VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent)
		{
			expr.Inner.Accept(this, CiPriority.Primary);
			switch (expr.Op) {
			case CiToken.Increment:
				Write("++");
				break;
			case CiToken.Decrement:
				Write("--");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected void StartAdd(CiExpr expr)
		{
			if (!expr.IsLiteralZero()) {
				expr.Accept(this, CiPriority.Add);
				Write(" + ");
			}
		}

		protected void WriteAdd(CiExpr left, CiExpr right)
		{
			if (left is CiLiteralLong leftLiteral) {
				long leftValue = leftLiteral.Value;
				if (leftValue == 0) {
					right.Accept(this, CiPriority.Argument);
					return;
				}
				if (right is CiLiteralLong rightLiteral) {
					VisitLiteralLong(leftValue + rightLiteral.Value);
					return;
				}
			}
			else if (right.IsLiteralZero()) {
				left.Accept(this, CiPriority.Argument);
				return;
			}
			left.Accept(this, CiPriority.Add);
			Write(" + ");
			right.Accept(this, CiPriority.Add);
		}

		protected void WriteStartEnd(CiExpr startIndex, CiExpr length)
		{
			startIndex.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteAdd(startIndex, length);
		}

		static bool IsBitOp(CiPriority parent)
		{
			switch (parent) {
			case CiPriority.Or:
			case CiPriority.Xor:
			case CiPriority.And:
			case CiPriority.Shift:
				return true;
			default:
				return false;
			}
		}

		protected virtual void WriteBinaryOperand(CiExpr expr, CiPriority parent, CiBinaryExpr binary)
		{
			expr.Accept(this, parent);
		}

		protected void WriteBinaryExpr(CiBinaryExpr expr, bool parentheses, CiPriority left, string op, CiPriority right)
		{
			if (parentheses)
				WriteChar('(');
			WriteBinaryOperand(expr.Left, left, expr);
			Write(op);
			WriteBinaryOperand(expr.Right, right, expr);
			if (parentheses)
				WriteChar(')');
		}

		protected void WriteBinaryExpr2(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
		{
			WriteBinaryExpr(expr, parent > child, child, op, child);
		}

		void WriteRel(CiBinaryExpr expr, CiPriority parent, string op)
		{
			WriteBinaryExpr(expr, parent > CiPriority.CondAnd, CiPriority.Rel, op, CiPriority.Rel);
		}

		protected static string GetEqOp(bool not) => not ? " != " : " == ";

		protected virtual void WriteEqualOperand(CiExpr expr, CiExpr other)
		{
			expr.Accept(this, CiPriority.Equality);
		}

		protected void WriteEqualExpr(CiExpr left, CiExpr right, CiPriority parent, string op)
		{
			if (parent > CiPriority.CondAnd)
				WriteChar('(');
			WriteEqualOperand(left, right);
			Write(op);
			WriteEqualOperand(right, left);
			if (parent > CiPriority.CondAnd)
				WriteChar(')');
		}

		protected virtual void WriteEqual(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			WriteEqualExpr(left, right, parent, GetEqOp(not));
		}

		protected virtual void WriteAnd(CiBinaryExpr expr, CiPriority parent)
		{
			WriteBinaryExpr(expr, parent > CiPriority.CondAnd && parent != CiPriority.And, CiPriority.And, " & ", CiPriority.And);
		}

		protected virtual void WriteAssignRight(CiBinaryExpr expr)
		{
			WriteCoerced(expr.Left.Type, expr.Right, CiPriority.Argument);
		}

		protected virtual void WriteAssign(CiBinaryExpr expr, CiPriority parent)
		{
			if (parent > CiPriority.Assign)
				WriteChar('(');
			expr.Left.Accept(this, CiPriority.Assign);
			Write(" = ");
			WriteAssignRight(expr);
			if (parent > CiPriority.Assign)
				WriteChar(')');
		}

		protected void WriteIndexing(CiExpr collection, CiExpr index)
		{
			collection.Accept(this, CiPriority.Primary);
			WriteChar('[');
			index.Accept(this, CiPriority.Argument);
			WriteChar(']');
		}

		protected virtual void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
		{
			WriteIndexing(expr.Left, expr.Right);
		}

		protected virtual string GetIsOperator() => " is ";

		public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Plus:
				WriteBinaryExpr(expr, parent > CiPriority.Add || IsBitOp(parent), CiPriority.Add, " + ", CiPriority.Add);
				break;
			case CiToken.Minus:
				WriteBinaryExpr(expr, parent > CiPriority.Add || IsBitOp(parent), CiPriority.Add, " - ", CiPriority.Mul);
				break;
			case CiToken.Asterisk:
				WriteBinaryExpr2(expr, parent, CiPriority.Mul, " * ");
				break;
			case CiToken.Slash:
				WriteBinaryExpr(expr, parent > CiPriority.Mul, CiPriority.Mul, " / ", CiPriority.Primary);
				break;
			case CiToken.Mod:
				WriteBinaryExpr(expr, parent > CiPriority.Mul, CiPriority.Mul, " % ", CiPriority.Primary);
				break;
			case CiToken.ShiftLeft:
				WriteBinaryExpr(expr, parent > CiPriority.Shift, CiPriority.Shift, " << ", CiPriority.Mul);
				break;
			case CiToken.ShiftRight:
				WriteBinaryExpr(expr, parent > CiPriority.Shift, CiPriority.Shift, " >> ", CiPriority.Mul);
				break;
			case CiToken.Less:
				WriteRel(expr, parent, " < ");
				break;
			case CiToken.LessOrEqual:
				WriteRel(expr, parent, " <= ");
				break;
			case CiToken.Greater:
				WriteRel(expr, parent, " > ");
				break;
			case CiToken.GreaterOrEqual:
				WriteRel(expr, parent, " >= ");
				break;
			case CiToken.Equal:
				WriteEqual(expr.Left, expr.Right, parent, false);
				break;
			case CiToken.NotEqual:
				WriteEqual(expr.Left, expr.Right, parent, true);
				break;
			case CiToken.And:
				WriteAnd(expr, parent);
				break;
			case CiToken.Or:
				WriteBinaryExpr2(expr, parent, CiPriority.Or, " | ");
				break;
			case CiToken.Xor:
				WriteBinaryExpr(expr, parent > CiPriority.Xor || parent == CiPriority.Or, CiPriority.Xor, " ^ ", CiPriority.Xor);
				break;
			case CiToken.CondAnd:
				WriteBinaryExpr(expr, parent > CiPriority.CondAnd || parent == CiPriority.CondOr, CiPriority.CondAnd, " && ", CiPriority.CondAnd);
				break;
			case CiToken.CondOr:
				WriteBinaryExpr2(expr, parent, CiPriority.CondOr, " || ");
				break;
			case CiToken.Assign:
				WriteAssign(expr, parent);
				break;
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
				if (parent > CiPriority.Assign)
					WriteChar('(');
				expr.Left.Accept(this, CiPriority.Assign);
				WriteChar(' ');
				Write(expr.GetOpString());
				WriteChar(' ');
				expr.Right.Accept(this, CiPriority.Argument);
				if (parent > CiPriority.Assign)
					WriteChar(')');
				break;
			case CiToken.LeftBracket:
				if (expr.Left.Type is CiStringType)
					WriteCharAt(expr);
				else
					WriteIndexingExpr(expr, parent);
				break;
			case CiToken.Is:
				if (parent > CiPriority.Rel)
					WriteChar('(');
				expr.Left.Accept(this, CiPriority.Rel);
				Write(GetIsOperator());
				switch (expr.Right) {
				case CiSymbolReference symbol:
					WriteName(symbol.Symbol);
					break;
				case CiVar def:
					WriteTypeAndName(def);
					break;
				default:
					throw new NotImplementedException();
				}
				if (parent > CiPriority.Rel)
					WriteChar(')');
				break;
			case CiToken.When:
				expr.Left.Accept(this, CiPriority.Argument);
				Write(" when ");
				expr.Right.Accept(this, CiPriority.Argument);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected abstract void WriteStringLength(CiExpr expr);

		protected static bool IsReferenceTo(CiExpr expr, CiId id) => expr is CiSymbolReference symbol && symbol.Symbol.Id == id;

		protected bool WriteJavaMatchProperty(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.MatchStart:
				WritePostfix(expr.Left, ".start()");
				return true;
			case CiId.MatchEnd:
				WritePostfix(expr.Left, ".end()");
				return true;
			case CiId.MatchLength:
				if (parent > CiPriority.Add)
					WriteChar('(');
				WritePostfix(expr.Left, ".end() - ");
				WritePostfix(expr.Left, ".start()");
				if (parent > CiPriority.Add)
					WriteChar(')');
				return true;
			case CiId.MatchValue:
				WritePostfix(expr.Left, ".group()");
				return true;
			default:
				return false;
			}
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			if (expr.Left == null)
				WriteLocalName(expr.Symbol, parent);
			else if (expr.Symbol.Id == CiId.StringLength)
				WriteStringLength(expr.Left);
			else {
				expr.Left.Accept(this, CiPriority.Primary);
				WriteMemberOp(expr.Left, expr);
				WriteName(expr.Symbol);
			}
		}

		protected abstract void WriteCharAt(CiBinaryExpr expr);

		protected virtual void WriteNotPromoted(CiType type, CiExpr expr)
		{
			expr.Accept(this, CiPriority.Argument);
		}

		protected virtual void WriteEnumAsInt(CiExpr expr, CiPriority parent)
		{
			expr.Accept(this, parent);
		}

		protected void WriteEnumHasFlag(CiExpr obj, List<CiExpr> args, CiPriority parent)
		{
			if (parent > CiPriority.Equality)
				WriteChar('(');
			int i = args[0].IntValue();
			if ((i & (i - 1)) == 0 && i != 0) {
				WriteChar('(');
				WriteEnumAsInt(obj, CiPriority.And);
				Write(" & ");
				WriteEnumAsInt(args[0], CiPriority.And);
				Write(") != 0");
			}
			else {
				Write("(~");
				WriteEnumAsInt(obj, CiPriority.Primary);
				Write(" & ");
				WriteEnumAsInt(args[0], CiPriority.And);
				Write(") == 0");
			}
			if (parent > CiPriority.Equality)
				WriteChar(')');
		}

		protected void WriteTryParseRadix(List<CiExpr> args)
		{
			Write(", ");
			if (args.Count == 2)
				args[1].Accept(this, CiPriority.Argument);
			else
				Write("10");
		}

		protected void WriteListAdd(CiExpr obj, string method, List<CiExpr> args)
		{
			obj.Accept(this, CiPriority.Primary);
			WriteChar('.');
			Write(method);
			WriteChar('(');
			CiType elementType = obj.Type.AsClassType().GetElementType();
			if (args.Count == 0)
				WriteNewStorage(elementType);
			else
				WriteNotPromoted(elementType, args[0]);
			WriteChar(')');
		}

		protected void WriteListInsert(CiExpr obj, string method, List<CiExpr> args, string separator = ", ")
		{
			obj.Accept(this, CiPriority.Primary);
			WriteChar('.');
			Write(method);
			WriteChar('(');
			args[0].Accept(this, CiPriority.Argument);
			Write(separator);
			CiType elementType = obj.Type.AsClassType().GetElementType();
			if (args.Count == 1)
				WriteNewStorage(elementType);
			else
				WriteNotPromoted(elementType, args[1]);
			WriteChar(')');
		}

		protected void WriteDictionaryAdd(CiExpr obj, List<CiExpr> args)
		{
			WriteIndexing(obj, args[0]);
			Write(" = ");
			WriteNewStorage(obj.Type.AsClassType().GetValueType());
		}

		protected void WriteClampAsMinMax(List<CiExpr> args)
		{
			args[0].Accept(this, CiPriority.Argument);
			Write(", ");
			args[1].Accept(this, CiPriority.Argument);
			Write("), ");
			args[2].Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		protected RegexOptions GetRegexOptions(List<CiExpr> args)
		{
			CiExpr expr = args[^1];
			if (expr.Type is CiEnum)
				return (RegexOptions) expr.IntValue();
			return RegexOptions.None;
		}

		protected bool WriteRegexOptions(List<CiExpr> args, string prefix, string separator, string suffix, string i, string m, string s)
		{
			RegexOptions options = GetRegexOptions(args);
			if (options == RegexOptions.None)
				return false;
			Write(prefix);
			if (options.HasFlag(RegexOptions.IgnoreCase))
				Write(i);
			if (options.HasFlag(RegexOptions.Multiline)) {
				if (options.HasFlag(RegexOptions.IgnoreCase))
					Write(separator);
				Write(m);
			}
			if (options.HasFlag(RegexOptions.Singleline)) {
				if (options != RegexOptions.Singleline)
					Write(separator);
				Write(s);
			}
			Write(suffix);
			return true;
		}

		protected abstract void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent);

		public override void VisitCallExpr(CiCallExpr expr, CiPriority parent)
		{
			CiMethod method = (CiMethod) expr.Method.Symbol;
			WriteCallExpr(expr.Method.Left, method, expr.Arguments, parent);
		}

		public override void VisitSelectExpr(CiSelectExpr expr, CiPriority parent)
		{
			WriteCoercedSelect(expr.Type, expr, parent);
		}

		protected void EnsureChildBlock()
		{
			if (this.AtChildStart) {
				this.AtLineStart = false;
				this.AtChildStart = false;
				WriteChar(' ');
				OpenBlock();
				this.InChildBlock = true;
			}
		}

		protected abstract void StartTemporaryVar(CiType type);

		protected virtual void DefineObjectLiteralTemporary(CiUnaryExpr expr)
		{
			if (expr.Inner is CiAggregateInitializer init) {
				EnsureChildBlock();
				int id = this.CurrentTemporaries.IndexOf(expr.Type);
				if (id < 0) {
					id = this.CurrentTemporaries.Count;
					StartTemporaryVar(expr.Type);
					this.CurrentTemporaries.Add(expr);
				}
				else
					this.CurrentTemporaries[id] = expr;
				Write("citemp");
				VisitLiteralLong(id);
				Write(" = ");
				CiDynamicPtrType dynamic = (CiDynamicPtrType) expr.Type;
				WriteNew(dynamic, CiPriority.Argument);
				EndStatement();
				foreach (CiExpr item in init.Items) {
					Write("citemp");
					VisitLiteralLong(id);
					WriteAggregateInitField(expr, item);
				}
			}
		}

		protected void WriteTemporaries(CiExpr expr)
		{
			switch (expr) {
			case CiVar def:
				if (def.Value != null) {
					if (def.Value is CiUnaryExpr unary && unary.Inner is CiAggregateInitializer)
						WriteTemporaries(unary.Inner);
					else
						WriteTemporaries(def.Value);
				}
				break;
			case CiAggregateInitializer init:
				foreach (CiExpr item in init.Items) {
					CiBinaryExpr assign = (CiBinaryExpr) item;
					WriteTemporaries(assign.Right);
				}
				break;
			case CiLiteral _:
			case CiLambdaExpr _:
				break;
			case CiInterpolatedString interp:
				foreach (CiInterpolatedPart part in interp.Parts)
					WriteTemporaries(part.Argument);
				break;
			case CiSymbolReference symbol:
				if (symbol.Left != null)
					WriteTemporaries(symbol.Left);
				break;
			case CiUnaryExpr unary:
				if (unary.Inner != null) {
					WriteTemporaries(unary.Inner);
					DefineObjectLiteralTemporary(unary);
				}
				break;
			case CiBinaryExpr binary:
				WriteTemporaries(binary.Left);
				if (binary.Op == CiToken.Is)
					DefineIsVar(binary);
				else
					WriteTemporaries(binary.Right);
				break;
			case CiSelectExpr select:
				WriteTemporaries(select.Cond);
				WriteTemporaries(select.OnTrue);
				WriteTemporaries(select.OnFalse);
				break;
			case CiCallExpr call:
				WriteTemporaries(call.Method);
				foreach (CiExpr arg in call.Arguments)
					WriteTemporaries(arg);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected virtual void CleanupTemporary(int i, CiExpr temp)
		{
		}

		protected void CleanupTemporaries()
		{
			for (int i = this.CurrentTemporaries.Count; --i >= 0;) {
				CiExpr temp = this.CurrentTemporaries[i];
				if (!(temp is CiType)) {
					CleanupTemporary(i, temp);
					this.CurrentTemporaries[i] = temp.Type;
				}
			}
		}

		public override void VisitExpr(CiExpr statement)
		{
			WriteTemporaries(statement);
			statement.Accept(this, CiPriority.Statement);
			WriteCharLine(';');
			if (statement is CiVar def)
				WriteInitCode(def);
			CleanupTemporaries();
		}

		public override void VisitConst(CiConst statement)
		{
		}

		protected abstract void WriteAssertCast(CiBinaryExpr expr);

		protected abstract void WriteAssert(CiAssert statement);

		public override void VisitAssert(CiAssert statement)
		{
			if (statement.Cond is CiBinaryExpr binary && binary.Op == CiToken.Is && binary.Right is CiVar)
				WriteAssertCast(binary);
			else
				WriteAssert(statement);
		}

		protected void WriteFirstStatements(List<CiStatement> statements, int count)
		{
			for (int i = 0; i < count; i++)
				statements[i].AcceptStatement(this);
		}

		protected virtual void WriteStatements(List<CiStatement> statements)
		{
			WriteFirstStatements(statements, statements.Count);
		}

		protected virtual void CleanupBlock(CiBlock statement)
		{
		}

		public override void VisitBlock(CiBlock statement)
		{
			if (this.AtChildStart) {
				this.AtLineStart = false;
				this.AtChildStart = false;
				WriteChar(' ');
			}
			OpenBlock();
			int temporariesCount = this.CurrentTemporaries.Count;
			WriteStatements(statement.Statements);
			CleanupBlock(statement);
			this.CurrentTemporaries.RemoveRange(temporariesCount, this.CurrentTemporaries.Count - temporariesCount);
			CloseBlock();
		}

		protected virtual void WriteChild(CiStatement statement)
		{
			bool wasInChildBlock = this.InChildBlock;
			this.AtLineStart = true;
			this.AtChildStart = true;
			this.InChildBlock = false;
			statement.AcceptStatement(this);
			if (this.InChildBlock)
				CloseBlock();
			else if (!(statement is CiBlock))
				this.Indent--;
			this.InChildBlock = wasInChildBlock;
		}

		public override void VisitBreak(CiBreak statement)
		{
			WriteLine("break;");
		}

		public override void VisitContinue(CiContinue statement)
		{
			WriteLine("continue;");
		}

		public override void VisitDoWhile(CiDoWhile statement)
		{
			Write("do");
			WriteChild(statement.Body);
			Write("while (");
			statement.Cond.Accept(this, CiPriority.Argument);
			WriteLine(");");
		}

		public override void VisitFor(CiFor statement)
		{
			if (statement.Cond != null)
				WriteTemporaries(statement.Cond);
			Write("for (");
			if (statement.Init != null)
				statement.Init.Accept(this, CiPriority.Statement);
			WriteChar(';');
			if (statement.Cond != null) {
				WriteChar(' ');
				statement.Cond.Accept(this, CiPriority.Argument);
			}
			WriteChar(';');
			if (statement.Advance != null) {
				WriteChar(' ');
				statement.Advance.Accept(this, CiPriority.Statement);
			}
			WriteChar(')');
			WriteChild(statement.Body);
		}

		protected virtual bool EmbedIfWhileIsVar(CiExpr expr, bool write) => false;

		void StartIfWhile(CiExpr expr)
		{
			EmbedIfWhileIsVar(expr, true);
			expr.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		void WriteIf(CiIf statement)
		{
			Write("if (");
			StartIfWhile(statement.Cond);
			WriteChild(statement.OnTrue);
			if (statement.OnFalse != null) {
				Write("else");
				if (statement.OnFalse is CiIf elseIf) {
					bool wasInChildBlock = this.InChildBlock;
					this.AtLineStart = true;
					this.AtChildStart = true;
					this.InChildBlock = false;
					if (!EmbedIfWhileIsVar(elseIf.Cond, false))
						WriteTemporaries(elseIf.Cond);
					if (this.InChildBlock) {
						WriteIf(elseIf);
						CloseBlock();
					}
					else {
						this.AtLineStart = false;
						this.AtChildStart = false;
						WriteChar(' ');
						WriteIf(elseIf);
					}
					this.InChildBlock = wasInChildBlock;
				}
				else
					WriteChild(statement.OnFalse);
			}
		}

		public override void VisitIf(CiIf statement)
		{
			if (!EmbedIfWhileIsVar(statement.Cond, false))
				WriteTemporaries(statement.Cond);
			WriteIf(statement);
		}

		public override void VisitNative(CiNative statement)
		{
			Write(statement.Content);
		}

		public override void VisitReturn(CiReturn statement)
		{
			if (statement.Value == null)
				WriteLine("return;");
			else {
				WriteTemporaries(statement.Value);
				Write("return ");
				WriteStronglyCoerced(this.CurrentMethod.Type, statement.Value);
				WriteCharLine(';');
				CleanupTemporaries();
			}
		}

		protected void DefineVar(CiExpr value)
		{
			if (value is CiVar def && def.Name != "_") {
				WriteVar(def);
				EndStatement();
			}
		}

		protected virtual void WriteSwitchCaseTypeVar(CiExpr value)
		{
		}

		protected virtual void WriteSwitchValue(CiExpr expr)
		{
			expr.Accept(this, CiPriority.Argument);
		}

		protected virtual void WriteSwitchCaseBody(List<CiStatement> statements)
		{
			WriteStatements(statements);
		}

		protected virtual void WriteSwitchCase(CiSwitch statement, CiCase kase)
		{
			foreach (CiExpr value in kase.Values) {
				Write("case ");
				WriteCoercedLiteral(statement.Value.Type, value);
				WriteCharLine(':');
			}
			this.Indent++;
			WriteSwitchCaseBody(kase.Body);
			this.Indent--;
		}

		protected void StartSwitch(CiSwitch statement)
		{
			Write("switch (");
			WriteSwitchValue(statement.Value);
			WriteLine(") {");
			foreach (CiCase kase in statement.Cases)
				WriteSwitchCase(statement, kase);
		}

		protected virtual void WriteSwitchCaseCond(CiSwitch statement, CiExpr value, CiPriority parent)
		{
			if (value is CiBinaryExpr when1 && when1.Op == CiToken.When) {
				if (parent > CiPriority.SelectCond)
					WriteChar('(');
				WriteSwitchCaseCond(statement, when1.Left, CiPriority.CondAnd);
				Write(" && ");
				when1.Right.Accept(this, CiPriority.CondAnd);
				if (parent > CiPriority.SelectCond)
					WriteChar(')');
			}
			else
				WriteEqual(statement.Value, value, parent, false);
		}

		protected virtual void WriteIfCaseBody(List<CiStatement> body, bool doWhile, CiSwitch statement, CiCase kase)
		{
			int length = CiSwitch.LengthWithoutTrailingBreak(body);
			if (doWhile && CiSwitch.HasEarlyBreak(body)) {
				this.Indent++;
				WriteNewLine();
				Write("do ");
				OpenBlock();
				WriteFirstStatements(body, length);
				CloseBlock();
				WriteLine("while (0);");
				this.Indent--;
			}
			else if (length != 1 || body[0] is CiIf) {
				WriteChar(' ');
				OpenBlock();
				WriteFirstStatements(body, length);
				CloseBlock();
			}
			else
				WriteChild(body[0]);
		}

		protected void WriteSwitchAsIfs(CiSwitch statement, bool doWhile)
		{
			foreach (CiCase kase in statement.Cases) {
				foreach (CiExpr value in kase.Values) {
					if (value is CiBinaryExpr when1 && when1.Op == CiToken.When) {
						DefineVar(when1.Left);
						WriteTemporaries(when1);
					}
					else
						WriteSwitchCaseTypeVar(value);
				}
			}
			string op = "if (";
			foreach (CiCase kase in statement.Cases) {
				CiPriority parent = kase.Values.Count == 1 ? CiPriority.Argument : CiPriority.CondOr;
				foreach (CiExpr value in kase.Values) {
					Write(op);
					WriteSwitchCaseCond(statement, value, parent);
					op = " || ";
				}
				WriteChar(')');
				WriteIfCaseBody(kase.Body, doWhile, statement, kase);
				op = "else if (";
			}
			if (statement.HasDefault()) {
				Write("else");
				WriteIfCaseBody(statement.DefaultBody, doWhile, statement, null);
			}
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			WriteTemporaries(statement.Value);
			StartSwitch(statement);
			if (statement.DefaultBody.Count > 0) {
				WriteLine("default:");
				this.Indent++;
				WriteSwitchCaseBody(statement.DefaultBody);
				this.Indent--;
			}
			WriteCharLine('}');
		}

		public override void VisitWhile(CiWhile statement)
		{
			if (!EmbedIfWhileIsVar(statement.Cond, false))
				WriteTemporaries(statement.Cond);
			Write("while (");
			StartIfWhile(statement.Cond);
			WriteChild(statement.Body);
		}

		protected void FlattenBlock(CiStatement statement)
		{
			if (statement is CiBlock block)
				WriteStatements(block.Statements);
			else
				statement.AcceptStatement(this);
		}

		protected virtual bool HasInitCode(CiNamedValue def) => GetAggregateInitializer(def) != null;

		protected virtual bool NeedsConstructor(CiClass klass)
		{
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiField field && HasInitCode(field))
					return true;
			}
			return klass.Constructor != null;
		}

		protected virtual void WriteInitField(CiField field)
		{
			WriteInitCode(field);
		}

		protected void WriteConstructorBody(CiClass klass)
		{
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiField field)
					WriteInitField(field);
			}
			if (klass.Constructor != null) {
				this.CurrentMethod = klass.Constructor;
				CiBlock block = (CiBlock) klass.Constructor.Body;
				WriteStatements(block.Statements);
				this.CurrentMethod = null;
			}
			this.CurrentTemporaries.Clear();
		}

		protected virtual void WriteParameter(CiVar param)
		{
			WriteTypeAndName(param);
		}

		protected void WriteRemainingParameters(CiMethod method, bool first, bool defaultArguments)
		{
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (!first)
					Write(", ");
				first = false;
				WriteParameter(param);
				if (defaultArguments)
					WriteVarInit(param);
			}
			WriteChar(')');
		}

		protected void WriteParameters(CiMethod method, bool defaultArguments)
		{
			WriteChar('(');
			WriteRemainingParameters(method, true, defaultArguments);
		}

		protected void WriteBody(CiMethod method)
		{
			if (method.CallType == CiCallType.Abstract)
				WriteCharLine(';');
			else {
				WriteNewLine();
				this.CurrentMethod = method;
				OpenBlock();
				FlattenBlock(method.Body);
				CloseBlock();
				this.CurrentMethod = null;
			}
		}

		protected void WritePublic(CiContainerType container)
		{
			if (container.IsPublic)
				Write("public ");
		}

		protected void WriteEnumValue(CiConst konst)
		{
			WriteDoc(konst.Documentation);
			WriteName(konst);
			if (!(konst.Value is CiImplicitEnumValue)) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Argument);
			}
		}

		public override void VisitEnumValue(CiConst konst, CiConst previous)
		{
			if (previous != null)
				WriteCharLine(',');
			WriteEnumValue(konst);
		}

		protected abstract void WriteEnum(CiEnum enu);

		protected virtual void WriteRegexOptionsEnum(CiProgram program)
		{
			if (program.RegexOptionsEnum)
				WriteEnum(program.System.RegexOptionsEnum);
		}

		protected void StartClass(CiClass klass, string suffix, string extendsClause)
		{
			Write("class ");
			Write(klass.Name);
			Write(suffix);
			if (klass.HasBaseClass()) {
				Write(extendsClause);
				Write(klass.BaseClassName);
			}
		}

		protected void OpenClass(CiClass klass, string suffix, string extendsClause)
		{
			StartClass(klass, suffix, extendsClause);
			WriteNewLine();
			OpenBlock();
		}

		protected abstract void WriteConst(CiConst konst);

		protected abstract void WriteField(CiField field);

		protected abstract void WriteMethod(CiMethod method);

		protected void WriteMembers(CiClass klass, bool constArrays)
		{
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				switch (symbol) {
				case CiConst konst:
					WriteConst(konst);
					break;
				case CiField field:
					WriteField(field);
					break;
				case CiMethod method:
					WriteMethod(method);
					this.CurrentTemporaries.Clear();
					break;
				case CiVar _:
					break;
				default:
					throw new NotImplementedException();
				}
			}
			if (constArrays) {
				foreach (CiConst konst in klass.ConstArrays)
					WriteConst(konst);
			}
		}

		protected bool WriteBaseClass(CiClass klass, CiProgram program)
		{
			if (this.WrittenClasses.Contains(klass))
				return false;
			this.WrittenClasses.Add(klass);
			if (klass.Parent is CiClass baseClass)
				WriteClass(baseClass, program);
			return true;
		}

		protected abstract void WriteClass(CiClass klass, CiProgram program);

		protected void WriteTypes(CiProgram program)
		{
			WriteRegexOptionsEnum(program);
			for (CiSymbol type = program.First; type != null; type = type.Next) {
				switch (type) {
				case CiClass klass:
					WriteClass(klass, program);
					break;
				case CiEnum enu:
					WriteEnum(enu);
					break;
				default:
					throw new NotImplementedException();
				}
			}
		}

		public abstract void WriteProgram(CiProgram program);
	}

	public abstract class GenTyped : GenBase
	{

		protected abstract void WriteType(CiType type, bool promote);

		protected override void WriteTypeAndName(CiNamedValue value)
		{
			WriteType(value.Type, true);
			WriteChar(' ');
			WriteName(value);
		}

		public override void VisitLiteralDouble(double value)
		{
			base.VisitLiteralDouble(value);
			float f = (float) value;
			if (f == value)
				WriteChar('f');
		}

		public override void VisitAggregateInitializer(CiAggregateInitializer expr)
		{
			Write("{ ");
			WriteCoercedLiterals(expr.Type.AsClassType().GetElementType(), expr.Items);
			Write(" }");
		}

		protected void WriteArrayStorageLength(CiExpr expr)
		{
			CiArrayStorageType array = (CiArrayStorageType) expr.Type;
			VisitLiteralLong(array.Length);
		}

		protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
		{
			Write("new ");
			WriteType(elementType.GetBaseType(), false);
			WriteChar('[');
			lengthExpr.Accept(this, CiPriority.Argument);
			WriteChar(']');
			while (elementType.IsArray()) {
				WriteChar('[');
				if (elementType is CiArrayStorageType arrayStorage)
					arrayStorage.LengthExpr.Accept(this, CiPriority.Argument);
				WriteChar(']');
				elementType = elementType.AsClassType().GetElementType();
			}
		}

		protected int GetOneAscii(CiExpr expr) => expr is CiLiteralString literal ? literal.GetOneAscii() : -1;

		protected static bool IsNarrower(CiId left, CiId right)
		{
			switch (left) {
			case CiId.SByteRange:
				switch (right) {
				case CiId.ByteRange:
				case CiId.ShortRange:
				case CiId.UShortRange:
				case CiId.IntType:
				case CiId.LongType:
					return true;
				default:
					return false;
				}
			case CiId.ByteRange:
				switch (right) {
				case CiId.SByteRange:
				case CiId.ShortRange:
				case CiId.UShortRange:
				case CiId.IntType:
				case CiId.LongType:
					return true;
				default:
					return false;
				}
			case CiId.ShortRange:
				switch (right) {
				case CiId.UShortRange:
				case CiId.IntType:
				case CiId.LongType:
					return true;
				default:
					return false;
				}
			case CiId.UShortRange:
				switch (right) {
				case CiId.ShortRange:
				case CiId.IntType:
				case CiId.LongType:
					return true;
				default:
					return false;
				}
			case CiId.IntType:
				return right == CiId.LongType;
			default:
				return false;
			}
		}

		protected CiExpr GetStaticCastInner(CiType type, CiExpr expr)
		{
			if (expr is CiBinaryExpr binary && binary.Op == CiToken.And && binary.Right is CiLiteralLong rightMask && type is CiIntegerType) {
				long mask;
				switch (type.Id) {
				case CiId.ByteRange:
				case CiId.SByteRange:
					mask = 255;
					break;
				case CiId.ShortRange:
				case CiId.UShortRange:
					mask = 65535;
					break;
				case CiId.IntType:
					mask = 4294967295;
					break;
				default:
					return expr;
				}
				if ((rightMask.Value & mask) == mask)
					return binary.Left;
			}
			return expr;
		}

		protected void WriteStaticCastType(CiType type)
		{
			WriteChar('(');
			WriteType(type, false);
			Write(") ");
		}

		protected virtual void WriteStaticCast(CiType type, CiExpr expr)
		{
			WriteStaticCastType(type);
			GetStaticCastInner(type, expr).Accept(this, CiPriority.Primary);
		}

		protected override void WriteNotPromoted(CiType type, CiExpr expr)
		{
			if (type is CiIntegerType && IsNarrower(type.Id, GetTypeId(expr.Type, true)))
				WriteStaticCast(type, expr);
			else
				expr.Accept(this, CiPriority.Argument);
		}

		protected virtual bool IsPromoted(CiExpr expr) => !(expr is CiBinaryExpr binary && (binary.Op == CiToken.LeftBracket || binary.IsAssign()));

		protected override void WriteAssignRight(CiBinaryExpr expr)
		{
			if (expr.Left.IsIndexing()) {
				if (expr.Right is CiLiteralLong) {
					WriteCoercedLiteral(expr.Left.Type, expr.Right);
					return;
				}
				CiId leftTypeId = expr.Left.Type.Id;
				CiId rightTypeId = GetTypeId(expr.Right.Type, IsPromoted(expr.Right));
				if (leftTypeId == CiId.SByteRange && rightTypeId == CiId.SByteRange) {
					expr.Right.Accept(this, CiPriority.Assign);
					return;
				}
				if (IsNarrower(leftTypeId, rightTypeId)) {
					WriteStaticCast(expr.Left.Type, expr.Right);
					return;
				}
			}
			base.WriteAssignRight(expr);
		}

		protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
		{
			if (type is CiIntegerType && type.Id != CiId.LongType && expr.Type.Id == CiId.LongType)
				WriteStaticCast(type, expr);
			else if (type.Id == CiId.FloatType && expr.Type.Id == CiId.DoubleType) {
				if (expr is CiLiteralDouble literal) {
					base.VisitLiteralDouble(literal.Value);
					WriteChar('f');
				}
				else
					WriteStaticCast(type, expr);
			}
			else if (type is CiIntegerType && expr.Type.Id == CiId.FloatIntType) {
				if (expr is CiCallExpr call && call.Method.Symbol.Id == CiId.MathTruncate) {
					expr = call.Arguments[0];
					if (expr is CiLiteralDouble literal) {
						VisitLiteralLong((long) literal.Value);
						return;
					}
				}
				WriteStaticCast(type, expr);
			}
			else
				base.WriteCoercedInternal(type, expr, parent);
		}

		protected override void WriteCharAt(CiBinaryExpr expr)
		{
			WriteIndexing(expr.Left, expr.Right);
		}

		protected override void StartTemporaryVar(CiType type)
		{
			WriteType(type, true);
			WriteChar(' ');
		}

		protected override void WriteAssertCast(CiBinaryExpr expr)
		{
			CiVar def = (CiVar) expr.Right;
			WriteTypeAndName(def);
			Write(" = ");
			WriteStaticCast(def.Type, expr.Left);
			WriteCharLine(';');
		}
	}

	public abstract class GenCCppD : GenTyped
	{

		protected readonly List<CiSwitch> SwitchesWithGoto = new List<CiSwitch>();

		public override void VisitLiteralLong(long i)
		{
			if (i == -9223372036854775808)
				Write("(-9223372036854775807 - 1)");
			else
				base.VisitLiteralLong(i);
		}

		static bool IsPtrTo(CiExpr ptr, CiExpr other) => ptr.Type is CiClassType klass && klass.Class.Id != CiId.StringClass && klass.IsAssignableFrom(other.Type);

		protected override void WriteEqual(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			CiType coercedType;
			if (IsPtrTo(left, right))
				coercedType = left.Type;
			else if (IsPtrTo(right, left))
				coercedType = right.Type;
			else {
				base.WriteEqual(left, right, parent, not);
				return;
			}
			if (parent > CiPriority.Equality)
				WriteChar('(');
			WriteCoerced(coercedType, left, CiPriority.Equality);
			Write(GetEqOp(not));
			WriteCoerced(coercedType, right, CiPriority.Equality);
			if (parent > CiPriority.Equality)
				WriteChar(')');
		}

		public override void VisitConst(CiConst statement)
		{
			if (statement.Type is CiArrayStorageType)
				WriteConst(statement);
		}

		public override void VisitBreak(CiBreak statement)
		{
			if (statement.LoopOrSwitch is CiSwitch switchStatement) {
				int gotoId = this.SwitchesWithGoto.IndexOf(switchStatement);
				if (gotoId >= 0) {
					Write("goto ciafterswitch");
					VisitLiteralLong(gotoId);
					WriteCharLine(';');
					return;
				}
			}
			base.VisitBreak(statement);
		}

		protected void WriteSwitchAsIfsWithGoto(CiSwitch statement)
		{
			if (statement.Cases.Any(kase => CiSwitch.HasEarlyBreakAndContinue(kase.Body)) || CiSwitch.HasEarlyBreakAndContinue(statement.DefaultBody)) {
				int gotoId = this.SwitchesWithGoto.Count;
				this.SwitchesWithGoto.Add(statement);
				WriteSwitchAsIfs(statement, false);
				Write("ciafterswitch");
				VisitLiteralLong(gotoId);
				WriteLine(": ;");
			}
			else
				WriteSwitchAsIfs(statement, true);
		}
	}

	public abstract class GenCCpp : GenCCppD
	{

		protected abstract void IncludeStdInt();

		protected abstract void IncludeAssert();

		protected abstract void IncludeMath();

		void WriteCIncludes()
		{
			WriteIncludes("#include <", ">");
		}

		protected override int GetLiteralChars() => 127;

		protected virtual void WriteNumericType(CiId id)
		{
			switch (id) {
			case CiId.SByteRange:
				IncludeStdInt();
				Write("int8_t");
				break;
			case CiId.ByteRange:
				IncludeStdInt();
				Write("uint8_t");
				break;
			case CiId.ShortRange:
				IncludeStdInt();
				Write("int16_t");
				break;
			case CiId.UShortRange:
				IncludeStdInt();
				Write("uint16_t");
				break;
			case CiId.IntType:
				Write("int");
				break;
			case CiId.LongType:
				IncludeStdInt();
				Write("int64_t");
				break;
			case CiId.FloatType:
				Write("float");
				break;
			case CiId.DoubleType:
				Write("double");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.MathNaN:
				IncludeMath();
				Write("NAN");
				break;
			case CiId.MathNegativeInfinity:
				IncludeMath();
				Write("-INFINITY");
				break;
			case CiId.MathPositiveInfinity:
				IncludeMath();
				Write("INFINITY");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		protected static CiExpr IsStringEmpty(CiBinaryExpr expr)
		{
			if (expr.Left is CiSymbolReference symbol && symbol.Symbol.Id == CiId.StringLength && expr.Right.IsLiteralZero())
				return symbol.Left;
			return null;
		}

		protected abstract void WriteArrayPtr(CiExpr expr, CiPriority parent);

		protected void WriteArrayPtrAdd(CiExpr array, CiExpr index)
		{
			if (index.IsLiteralZero())
				WriteArrayPtr(array, CiPriority.Argument);
			else {
				WriteArrayPtr(array, CiPriority.Add);
				Write(" + ");
				index.Accept(this, CiPriority.Mul);
			}
		}

		protected static CiCallExpr IsStringSubstring(CiExpr expr)
		{
			if (expr is CiCallExpr call) {
				CiId id = call.Method.Symbol.Id;
				if ((id == CiId.StringSubstring && call.Arguments.Count == 2) || id == CiId.UTF8GetString)
					return call;
			}
			return null;
		}

		protected static bool IsUTF8GetString(CiCallExpr call) => call.Method.Symbol.Id == CiId.UTF8GetString;

		protected static CiExpr GetStringSubstringPtr(CiCallExpr call) => IsUTF8GetString(call) ? call.Arguments[0] : call.Method.Left;

		protected static CiExpr GetStringSubstringOffset(CiCallExpr call) => call.Arguments[IsUTF8GetString(call) ? 1 : 0];

		protected static CiExpr GetStringSubstringLength(CiCallExpr call) => call.Arguments[IsUTF8GetString(call) ? 2 : 1];

		protected void WriteStringPtrAdd(CiCallExpr call)
		{
			WriteArrayPtrAdd(GetStringSubstringPtr(call), GetStringSubstringOffset(call));
		}

		protected static CiExpr IsTrimSubstring(CiBinaryExpr expr)
		{
			CiCallExpr call = IsStringSubstring(expr.Right);
			if (call != null && !IsUTF8GetString(call) && expr.Left is CiSymbolReference leftSymbol && GetStringSubstringPtr(call).IsReferenceTo(leftSymbol.Symbol) && GetStringSubstringOffset(call).IsLiteralZero())
				return GetStringSubstringLength(call);
			return null;
		}

		protected void WriteStringLiteralWithNewLine(string s)
		{
			WriteChar('"');
			Write(s);
			Write("\\n\"");
		}

		protected virtual void WriteUnreachable(CiAssert statement)
		{
			Write("abort();");
			if (statement.Message != null) {
				Write(" // ");
				statement.Message.Accept(this, CiPriority.Argument);
			}
			WriteNewLine();
		}

		protected override void WriteAssert(CiAssert statement)
		{
			if (statement.CompletesNormally()) {
				WriteTemporaries(statement.Cond);
				IncludeAssert();
				Write("assert(");
				if (statement.Message == null)
					statement.Cond.Accept(this, CiPriority.Argument);
				else {
					statement.Cond.Accept(this, CiPriority.CondAnd);
					Write(" && ");
					statement.Message.Accept(this, CiPriority.Argument);
				}
				WriteLine(");");
			}
			else
				WriteUnreachable(statement);
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			if (statement.Value.Type is CiStringType || statement.HasWhen())
				WriteSwitchAsIfsWithGoto(statement);
			else
				base.VisitSwitch(statement);
		}

		protected void WriteMethods(CiClass klass)
		{
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiMethod method) {
					WriteMethod(method);
					this.CurrentTemporaries.Clear();
				}
			}
		}

		protected abstract void WriteClassInternal(CiClass klass);

		protected override void WriteClass(CiClass klass, CiProgram program)
		{
			if (!WriteBaseClass(klass, program))
				return;
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiField field && field.Type.GetBaseType() is CiStorageType storage && storage.Class.Id == CiId.None)
					WriteClass(storage.Class, program);
			}
			WriteClassInternal(klass);
		}

		static string ChangeExtension(string path, string ext)
		{
			int extIndex = path.Length;
			for (int i = extIndex; --i >= 0 && path[i] != '/' && path[i] != '\\';) {
				if (path[i] == '.') {
					extIndex = i;
					break;
				}
			}
			return path.Substring(0, extIndex) + ext;
		}

		protected void CreateHeaderFile(string headerExt)
		{
			CreateFile(null, ChangeExtension(this.OutputFile, headerExt));
			WriteLine("#pragma once");
			WriteCIncludes();
		}

		static string GetFilenameWithoutExtension(string path)
		{
			int pathLength = path.Length;
			int extIndex = pathLength;
			int i = pathLength;
			while (--i >= 0 && path[i] != '/' && path[i] != '\\') {
				if (path[i] == '.' && extIndex == pathLength)
					extIndex = i;
			}
			i++;
			return path.Substring(i, extIndex - i);
		}

		protected void CreateImplementationFile(CiProgram program, string headerExt)
		{
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteCIncludes();
			Write("#include \"");
			Write(GetFilenameWithoutExtension(this.OutputFile));
			Write(headerExt);
			WriteCharLine('"');
		}
	}

	public class GenC : GenCCpp
	{

		bool IntTryParse;

		bool LongTryParse;

		bool DoubleTryParse;

		bool StringAssign;

		bool StringSubstring;

		bool StringAppend;

		bool StringIndexOf;

		bool StringLastIndexOf;

		bool StringEndsWith;

		bool StringReplace;

		bool StringFormat;

		bool MatchFind;

		bool MatchPos;

		bool PtrConstruct;

		bool SharedMake;

		bool SharedAddRef;

		bool SharedRelease;

		bool SharedAssign;

		readonly SortedDictionary<string, string> ListFrees = new SortedDictionary<string, string>();

		bool TreeCompareInteger;

		bool TreeCompareString;

		readonly SortedSet<CiId> Compares = new SortedSet<CiId>();

		readonly SortedSet<CiId> Contains = new SortedSet<CiId>();

		readonly List<CiVar> VarsToDestruct = new List<CiVar>();

		protected CiClass CurrentClass;

		protected override CiContainerType GetCurrentContainer() => this.CurrentClass;

		protected override string GetTargetName() => "C";

		protected override void WriteSelfDoc(CiMethod method)
		{
			if (method.CallType == CiCallType.Static)
				return;
			Write(" * @param self This <code>");
			WriteName(method.Parent);
			WriteLine("</code>.");
		}

		protected override void IncludeStdInt()
		{
			Include("stdint.h");
		}

		protected override void IncludeAssert()
		{
			Include("assert.h");
		}

		protected override void IncludeMath()
		{
			Include("math.h");
		}

		protected virtual void IncludeStdBool()
		{
			Include("stdbool.h");
		}

		public override void VisitLiteralNull()
		{
			Write("NULL");
		}

		protected virtual void WritePrintfLongPrefix()
		{
			Write("ll");
		}

		protected override void WritePrintfWidth(CiInterpolatedPart part)
		{
			base.WritePrintfWidth(part);
			if (IsStringSubstring(part.Argument) != null) {
				Debug.Assert(part.Precision < 0);
				Write(".*");
			}
			if (part.Argument.Type.Id == CiId.LongType)
				WritePrintfLongPrefix();
		}

		protected virtual void WriteInterpolatedStringArgBase(CiExpr expr)
		{
			if (expr.Type.Id == CiId.LongType) {
				Write("(long long) ");
				expr.Accept(this, CiPriority.Primary);
			}
			else
				expr.Accept(this, CiPriority.Argument);
		}

		void WriteStringPtrAddCast(CiCallExpr call)
		{
			if (IsUTF8GetString(call))
				Write("(const char *) ");
			WriteStringPtrAdd(call);
		}

		static bool IsDictionaryClassStgIndexing(CiExpr expr)
		{
			return expr is CiBinaryExpr indexing && indexing.Op == CiToken.LeftBracket && indexing.Left.Type is CiClassType dict && dict.Class.TypeParameterCount == 2 && dict.GetValueType() is CiStorageType;
		}

		void WriteTemporaryOrExpr(CiExpr expr, CiPriority parent)
		{
			int tempId = this.CurrentTemporaries.IndexOf(expr);
			if (tempId >= 0) {
				Write("citemp");
				VisitLiteralLong(tempId);
			}
			else
				expr.Accept(this, parent);
		}

		void WriteUpcast(CiClass resultClass, CiSymbol klass)
		{
			for (; klass != resultClass; klass = klass.Parent)
				Write(".base");
		}

		void WriteClassPtr(CiClass resultClass, CiExpr expr, CiPriority parent)
		{
			switch (expr.Type) {
			case CiStorageType storage when storage.Class.Id == CiId.None && !IsDictionaryClassStgIndexing(expr):
				WriteChar('&');
				WriteTemporaryOrExpr(expr, CiPriority.Primary);
				WriteUpcast(resultClass, storage.Class);
				break;
			case CiClassType ptr when ptr.Class != resultClass:
				WriteChar('&');
				WritePostfix(expr, "->base");
				WriteUpcast(resultClass, ptr.Class.Parent);
				break;
			default:
				expr.Accept(this, parent);
				break;
			}
		}

		protected override void WriteInterpolatedStringArg(CiExpr expr)
		{
			CiCallExpr call = IsStringSubstring(expr);
			if (call != null) {
				GetStringSubstringLength(call).Accept(this, CiPriority.Argument);
				Write(", ");
				WriteStringPtrAddCast(call);
			}
			else if (expr.Type is CiClassType klass && klass.Class.Id != CiId.StringClass) {
				Write(this.Namespace);
				Write(klass.Class.Name);
				Write("_ToString(");
				WriteClassPtr(klass.Class, expr, CiPriority.Argument);
				WriteChar(')');
			}
			else
				WriteInterpolatedStringArgBase(expr);
		}

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			Include("stdarg.h");
			Include("stdio.h");
			this.StringFormat = true;
			Write("CiString_Format(");
			WritePrintf(expr, false);
		}

		protected virtual void WriteCamelCaseNotKeyword(string name)
		{
			switch (name) {
			case "this":
				Write("self");
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
				WriteCamelCase(name);
				WriteChar('_');
				break;
			default:
				WriteCamelCase(name);
				break;
			}
		}

		protected override void WriteName(CiSymbol symbol)
		{
			switch (symbol) {
			case CiContainerType _:
				Write(this.Namespace);
				Write(symbol.Name);
				break;
			case CiMethod _:
				Write(this.Namespace);
				Write(symbol.Parent.Name);
				WriteChar('_');
				Write(symbol.Name);
				break;
			case CiConst _:
				if (symbol.Parent is CiContainerType) {
					Write(this.Namespace);
					Write(symbol.Parent.Name);
					WriteChar('_');
				}
				WriteUppercaseWithUnderscores(symbol.Name);
				break;
			default:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			}
		}

		void WriteSelfForField(CiSymbol fieldClass)
		{
			Write("self->");
			for (CiSymbol klass = this.CurrentClass; klass != fieldClass; klass = klass.Parent)
				Write("base.");
		}

		protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
		{
			if (symbol.Parent is CiForeach forEach) {
				CiClassType klass = (CiClassType) forEach.Collection.Type;
				switch (klass.Class.Id) {
				case CiId.StringClass:
				case CiId.ListClass:
					if (parent == CiPriority.Primary)
						WriteChar('(');
					WriteChar('*');
					WriteCamelCaseNotKeyword(symbol.Name);
					if (parent == CiPriority.Primary)
						WriteChar(')');
					return;
				case CiId.ArrayStorageClass:
					if (klass.GetElementType() is CiStorageType) {
						if (parent > CiPriority.Add)
							WriteChar('(');
						forEach.Collection.Accept(this, CiPriority.Add);
						Write(" + ");
						WriteCamelCaseNotKeyword(symbol.Name);
						if (parent > CiPriority.Add)
							WriteChar(')');
					}
					else {
						forEach.Collection.Accept(this, CiPriority.Primary);
						WriteChar('[');
						WriteCamelCaseNotKeyword(symbol.Name);
						WriteChar(']');
					}
					return;
				default:
					break;
				}
			}
			if (symbol is CiField)
				WriteSelfForField(symbol.Parent);
			WriteName(symbol);
		}

		void WriteMatchProperty(CiSymbolReference expr, int which)
		{
			this.MatchPos = true;
			Write("CiMatch_GetPos(");
			expr.Left.Accept(this, CiPriority.Argument);
			Write(", ");
			VisitLiteralLong(which);
			WriteChar(')');
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.ConsoleError:
				Include("stdio.h");
				Write("stderr");
				break;
			case CiId.ListCount:
			case CiId.StackCount:
				WritePostfix(expr.Left, "->len");
				break;
			case CiId.QueueCount:
				expr.Left.Accept(this, CiPriority.Primary);
				if (expr.Left.Type is CiStorageType)
					WriteChar('.');
				else
					Write("->");
				Write("length");
				break;
			case CiId.HashSetCount:
			case CiId.DictionaryCount:
				WriteCall("g_hash_table_size", expr.Left);
				break;
			case CiId.SortedSetCount:
			case CiId.SortedDictionaryCount:
				WriteCall("g_tree_nnodes", expr.Left);
				break;
			case CiId.MatchStart:
				WriteMatchProperty(expr, 0);
				break;
			case CiId.MatchEnd:
				WriteMatchProperty(expr, 1);
				break;
			case CiId.MatchLength:
				WriteMatchProperty(expr, 2);
				break;
			case CiId.MatchValue:
				Write("g_match_info_fetch(");
				expr.Left.Accept(this, CiPriority.Argument);
				Write(", 0)");
				break;
			default:
				if (expr.Left == null || expr.Symbol is CiConst)
					WriteLocalName(expr.Symbol, parent);
				else if (IsDictionaryClassStgIndexing(expr.Left)) {
					WritePostfix(expr.Left, "->");
					WriteName(expr.Symbol);
				}
				else
					base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		void WriteGlib(string s)
		{
			Include("glib.h");
			Write(s);
		}

		protected virtual void WriteStringPtrType()
		{
			Write("const char *");
		}

		protected virtual void WriteClassType(CiClassType klass, bool space)
		{
			switch (klass.Class.Id) {
			case CiId.None:
				if (!(klass is CiReadWriteClassType))
					Write("const ");
				WriteName(klass.Class);
				if (!(klass is CiStorageType))
					Write(" *");
				else if (space)
					WriteChar(' ');
				break;
			case CiId.StringClass:
				if (klass.Id == CiId.StringStorageType)
					Write("char *");
				else
					WriteStringPtrType();
				break;
			case CiId.ListClass:
			case CiId.StackClass:
				WriteGlib("GArray *");
				break;
			case CiId.QueueClass:
				WriteGlib("GQueue ");
				if (!(klass is CiStorageType))
					WriteChar('*');
				break;
			case CiId.HashSetClass:
			case CiId.DictionaryClass:
				WriteGlib("GHashTable *");
				break;
			case CiId.SortedSetClass:
			case CiId.SortedDictionaryClass:
				WriteGlib("GTree *");
				break;
			case CiId.TextWriterClass:
				Include("stdio.h");
				Write("FILE *");
				break;
			case CiId.RegexClass:
				if (!(klass is CiReadWriteClassType))
					Write("const ");
				WriteGlib("GRegex *");
				break;
			case CiId.MatchClass:
				if (!(klass is CiReadWriteClassType))
					Write("const ");
				WriteGlib("GMatchInfo *");
				break;
			case CiId.LockClass:
				NotYet(klass, "Lock");
				Include("threads.h");
				Write("mtx_t ");
				break;
			default:
				NotSupported(klass, klass.Class.Name);
				break;
			}
		}

		void WriteArrayPrefix(CiType type)
		{
			if (type is CiClassType array && array.IsArray()) {
				WriteArrayPrefix(array.GetElementType());
				if (!(type is CiArrayStorageType)) {
					if (array.GetElementType() is CiArrayStorageType)
						WriteChar('(');
					if (!(type is CiReadWriteClassType))
						Write("const ");
					WriteChar('*');
				}
			}
		}

		void StartDefinition(CiType type, bool promote, bool space)
		{
			CiType baseType = type.GetBaseType();
			switch (baseType) {
			case CiIntegerType _:
				WriteNumericType(GetTypeId(baseType, promote && type == baseType));
				if (space)
					WriteChar(' ');
				break;
			case CiEnum _:
				if (baseType.Id == CiId.BoolType) {
					IncludeStdBool();
					Write("bool");
				}
				else
					WriteName(baseType);
				if (space)
					WriteChar(' ');
				break;
			case CiClassType klass:
				WriteClassType(klass, space);
				break;
			default:
				Write(baseType.Name);
				if (space)
					WriteChar(' ');
				break;
			}
			WriteArrayPrefix(type);
		}

		void EndDefinition(CiType type)
		{
			while (type.IsArray()) {
				CiType elementType = type.AsClassType().GetElementType();
				if (type is CiArrayStorageType arrayStorage) {
					WriteChar('[');
					VisitLiteralLong(arrayStorage.Length);
					WriteChar(']');
				}
				else if (elementType is CiArrayStorageType)
					WriteChar(')');
				type = elementType;
			}
		}

		void WriteReturnType(CiMethod method)
		{
			if (method.Type.Id == CiId.VoidType && method.Throws) {
				IncludeStdBool();
				Write("bool ");
			}
			else
				StartDefinition(method.Type, true, true);
		}

		protected override void WriteType(CiType type, bool promote)
		{
			StartDefinition(type, promote, type is CiClassType arrayPtr && arrayPtr.Class.Id == CiId.ArrayPtrClass);
			EndDefinition(type);
		}

		protected override void WriteTypeAndName(CiNamedValue value)
		{
			StartDefinition(value.Type, true, true);
			WriteName(value);
			EndDefinition(value.Type);
		}

		void WriteDynamicArrayCast(CiType elementType)
		{
			WriteChar('(');
			StartDefinition(elementType, false, true);
			Write(elementType.IsArray() ? "(*)" : "*");
			EndDefinition(elementType);
			Write(") ");
		}

		void WriteXstructorPtr(bool need, CiClass klass, string name)
		{
			if (need) {
				Write("(CiMethodPtr) ");
				WriteName(klass);
				WriteChar('_');
				Write(name);
			}
			else
				Write("NULL");
		}

		static bool IsHeapAllocated(CiType type) => type.Id == CiId.StringStorageType || type is CiDynamicPtrType;

		static bool NeedToDestructType(CiType type)
		{
			if (IsHeapAllocated(type))
				return true;
			if (type is CiStorageType storage) {
				switch (storage.Class.Id) {
				case CiId.ListClass:
				case CiId.StackClass:
				case CiId.HashSetClass:
				case CiId.SortedSetClass:
				case CiId.DictionaryClass:
				case CiId.SortedDictionaryClass:
				case CiId.MatchClass:
				case CiId.LockClass:
					return true;
				default:
					return NeedsDestructor(storage.Class);
				}
			}
			return false;
		}

		static bool NeedToDestruct(CiSymbol symbol)
		{
			CiType type = symbol.Type;
			while (type is CiArrayStorageType array)
				type = array.GetElementType();
			return NeedToDestructType(type);
		}

		static bool NeedsDestructor(CiClass klass)
		{
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiField field && NeedToDestruct(field))
					return true;
			}
			return klass.Parent is CiClass baseClass && NeedsDestructor(baseClass);
		}

		void WriteXstructorPtrs(CiClass klass)
		{
			WriteXstructorPtr(NeedsConstructor(klass), klass, "Construct");
			Write(", ");
			WriteXstructorPtr(NeedsDestructor(klass), klass, "Destruct");
		}

		protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
		{
			this.SharedMake = true;
			if (parent > CiPriority.Mul)
				WriteChar('(');
			WriteDynamicArrayCast(elementType);
			Write("CiShared_Make(");
			lengthExpr.Accept(this, CiPriority.Argument);
			Write(", sizeof(");
			WriteType(elementType, false);
			Write("), ");
			switch (elementType) {
			case CiStringStorageType _:
				this.PtrConstruct = true;
				this.ListFrees["String"] = "free(*(void **) ptr)";
				Write("(CiMethodPtr) CiPtr_Construct, CiList_FreeString");
				break;
			case CiStorageType storage:
				WriteXstructorPtrs(storage.Class);
				break;
			case CiDynamicPtrType _:
				this.PtrConstruct = true;
				this.SharedRelease = true;
				this.ListFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
				Write("(CiMethodPtr) CiPtr_Construct, CiList_FreeShared");
				break;
			default:
				Write("NULL, NULL");
				break;
			}
			WriteChar(')');
			if (parent > CiPriority.Mul)
				WriteChar(')');
		}

		static bool IsNewString(CiExpr expr)
		{
			return expr is CiInterpolatedString || (expr is CiCallExpr call && expr.Type.Id == CiId.StringStorageType && (call.Method.Symbol.Id != CiId.StringSubstring || call.Arguments.Count == 2));
		}

		void WriteStringStorageValue(CiExpr expr)
		{
			CiCallExpr call = IsStringSubstring(expr);
			if (call != null) {
				Include("string.h");
				this.StringSubstring = true;
				Write("CiString_Substring(");
				WriteStringPtrAddCast(call);
				Write(", ");
				GetStringSubstringLength(call).Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			else if (IsNewString(expr))
				expr.Accept(this, CiPriority.Argument);
			else {
				Include("string.h");
				WriteCall("strdup", expr);
			}
		}

		protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
		{
			switch (value) {
			case null:
				if (IsHeapAllocated(array.GetStorageType()))
					Write(" = { NULL }");
				break;
			case CiLiteral literal when literal.IsDefaultValue():
				Write(" = { ");
				literal.Accept(this, CiPriority.Argument);
				Write(" }");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		string GetDictionaryDestroy(CiType type)
		{
			switch (type) {
			case CiStringStorageType _:
			case CiArrayStorageType _:
				return "free";
			case CiStorageType storage:
				switch (storage.Class.Id) {
				case CiId.ListClass:
				case CiId.StackClass:
					return "(GDestroyNotify) g_array_unref";
				case CiId.HashSetClass:
				case CiId.DictionaryClass:
					return "(GDestroyNotify) g_hash_table_unref";
				case CiId.SortedSetClass:
				case CiId.SortedDictionaryClass:
					return "(GDestroyNotify) g_tree_unref";
				default:
					return NeedsDestructor(storage.Class) ? $"(GDestroyNotify) {storage.Class.Name}_Delete" : "free";
				}
			case CiDynamicPtrType _:
				this.SharedRelease = true;
				return "CiShared_Release";
			default:
				return "NULL";
			}
		}

		void WriteHashEqual(CiType keyType)
		{
			Write(keyType is CiStringType ? "g_str_hash, g_str_equal" : "NULL, NULL");
		}

		void WriteNewHashTable(CiType keyType, string valueDestroy)
		{
			Write("g_hash_table_new");
			string keyDestroy = GetDictionaryDestroy(keyType);
			if (keyDestroy == "NULL" && valueDestroy == "NULL") {
				WriteChar('(');
				WriteHashEqual(keyType);
			}
			else {
				Write("_full(");
				WriteHashEqual(keyType);
				Write(", ");
				Write(keyDestroy);
				Write(", ");
				Write(valueDestroy);
			}
			WriteChar(')');
		}

		void WriteNewTree(CiType keyType, string valueDestroy)
		{
			if (keyType.Id == CiId.StringPtrType && valueDestroy == "NULL")
				Write("g_tree_new((GCompareFunc) strcmp");
			else {
				Write("g_tree_new_full(CiTree_Compare");
				switch (keyType) {
				case CiIntegerType _:
					this.TreeCompareInteger = true;
					Write("Integer");
					break;
				case CiStringType _:
					this.TreeCompareString = true;
					Write("String");
					break;
				default:
					NotSupported(keyType, keyType.ToString());
					break;
				}
				Write(", NULL, ");
				Write(GetDictionaryDestroy(keyType));
				Write(", ");
				Write(valueDestroy);
			}
			WriteChar(')');
		}

		protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
		{
			switch (klass.Class.Id) {
			case CiId.ListClass:
			case CiId.StackClass:
				Write("g_array_new(FALSE, FALSE, sizeof(");
				WriteType(klass.GetElementType(), false);
				Write("))");
				break;
			case CiId.QueueClass:
				Write("G_QUEUE_INIT");
				break;
			case CiId.HashSetClass:
				WriteNewHashTable(klass.GetElementType(), "NULL");
				break;
			case CiId.SortedSetClass:
				WriteNewTree(klass.GetElementType(), "NULL");
				break;
			case CiId.DictionaryClass:
				WriteNewHashTable(klass.GetKeyType(), GetDictionaryDestroy(klass.GetValueType()));
				break;
			case CiId.SortedDictionaryClass:
				WriteNewTree(klass.GetKeyType(), GetDictionaryDestroy(klass.GetValueType()));
				break;
			default:
				this.SharedMake = true;
				if (parent > CiPriority.Mul)
					WriteChar('(');
				WriteStaticCastType(klass);
				Write("CiShared_Make(1, sizeof(");
				WriteName(klass.Class);
				Write("), ");
				WriteXstructorPtrs(klass.Class);
				WriteChar(')');
				if (parent > CiPriority.Mul)
					WriteChar(')');
				break;
			}
		}

		protected override void WriteStorageInit(CiNamedValue def)
		{
			if (def.Type.AsClassType().Class.TypeParameterCount > 0)
				base.WriteStorageInit(def);
		}

		protected override void WriteVarInit(CiNamedValue def)
		{
			if (def.Value == null && IsHeapAllocated(def.Type))
				Write(" = NULL");
			else
				base.WriteVarInit(def);
		}

		void WriteAssignTemporary(CiType type, CiExpr expr)
		{
			Write(" = ");
			if (expr != null)
				WriteCoerced(type, expr, CiPriority.Argument);
			else
				WriteNewStorage(type);
		}

		int WriteCTemporary(CiType type, CiExpr expr)
		{
			EnsureChildBlock();
			bool assign = expr != null || (type is CiClassType klass && (klass.Class.Id == CiId.ListClass || klass.Class.Id == CiId.DictionaryClass || klass.Class.Id == CiId.SortedDictionaryClass));
			int id = this.CurrentTemporaries.IndexOf(type);
			if (id < 0) {
				id = this.CurrentTemporaries.Count;
				StartDefinition(type, false, true);
				Write("citemp");
				VisitLiteralLong(id);
				EndDefinition(type);
				if (assign)
					WriteAssignTemporary(type, expr);
				WriteCharLine(';');
				this.CurrentTemporaries.Add(expr);
			}
			else if (assign) {
				Write("citemp");
				VisitLiteralLong(id);
				WriteAssignTemporary(type, expr);
				WriteCharLine(';');
				this.CurrentTemporaries[id] = expr;
			}
			return id;
		}

		void WriteStorageTemporary(CiExpr expr)
		{
			if (IsNewString(expr) || (expr is CiCallExpr && expr.Type is CiStorageType))
				WriteCTemporary(expr.Type, expr);
		}

		void WriteCTemporaries(CiExpr expr)
		{
			switch (expr) {
			case CiVar def:
				if (def.Value != null)
					WriteCTemporaries(def.Value);
				break;
			case CiAggregateInitializer init:
				foreach (CiExpr item in init.Items) {
					CiBinaryExpr assign = (CiBinaryExpr) item;
					WriteCTemporaries(assign.Right);
				}
				break;
			case CiLiteral _:
			case CiLambdaExpr _:
				break;
			case CiInterpolatedString interp:
				foreach (CiInterpolatedPart part in interp.Parts)
					WriteCTemporaries(part.Argument);
				break;
			case CiSymbolReference symbol:
				if (symbol.Left != null)
					WriteCTemporaries(symbol.Left);
				break;
			case CiUnaryExpr unary:
				if (unary.Inner != null)
					WriteCTemporaries(unary.Inner);
				break;
			case CiBinaryExpr binary:
				WriteCTemporaries(binary.Left);
				if (IsStringSubstring(binary.Left) == null)
					WriteStorageTemporary(binary.Left);
				WriteCTemporaries(binary.Right);
				if (binary.Op != CiToken.Assign)
					WriteStorageTemporary(binary.Right);
				break;
			case CiSelectExpr select:
				WriteCTemporaries(select.Cond);
				break;
			case CiCallExpr call:
				if (call.Method.Left != null) {
					WriteCTemporaries(call.Method.Left);
					WriteStorageTemporary(call.Method.Left);
				}
				CiMethod method = (CiMethod) call.Method.Symbol;
				CiVar param = method.Parameters.FirstParameter();
				foreach (CiExpr arg in call.Arguments) {
					WriteCTemporaries(arg);
					if (call.Method.Symbol.Id != CiId.ConsoleWrite && call.Method.Symbol.Id != CiId.ConsoleWriteLine && !(param.Type is CiStorageType))
						WriteStorageTemporary(arg);
					param = param.NextParameter();
				}
				break;
			default:
				throw new NotImplementedException();
			}
		}

		static bool HasTemporariesToDestruct(CiExpr expr)
		{
			switch (expr) {
			case CiAggregateInitializer init:
				return init.Items.Any(field => HasTemporariesToDestruct(field));
			case CiLiteral _:
			case CiLambdaExpr _:
				return false;
			case CiInterpolatedString interp:
				return interp.Parts.Any(part => HasTemporariesToDestruct(part.Argument));
			case CiSymbolReference symbol:
				return symbol.Left != null && HasTemporariesToDestruct(symbol.Left);
			case CiUnaryExpr unary:
				return unary.Inner != null && HasTemporariesToDestruct(unary.Inner);
			case CiBinaryExpr binary:
				return HasTemporariesToDestruct(binary.Left) || (binary.Op != CiToken.Is && HasTemporariesToDestruct(binary.Right));
			case CiSelectExpr select:
				return HasTemporariesToDestruct(select.Cond);
			case CiCallExpr call:
				return (call.Method.Left != null && (HasTemporariesToDestruct(call.Method.Left) || IsNewString(call.Method.Left))) || call.Arguments.Any(arg => HasTemporariesToDestruct(arg) || IsNewString(arg));
			default:
				throw new NotImplementedException();
			}
		}

		protected override void CleanupTemporary(int i, CiExpr temp)
		{
			if (temp.Type.Id == CiId.StringStorageType) {
				Write("free(citemp");
				VisitLiteralLong(i);
				WriteLine(");");
			}
		}

		protected override void WriteVar(CiNamedValue def)
		{
			base.WriteVar(def);
			if (NeedToDestruct(def)) {
				CiVar local = (CiVar) def;
				this.VarsToDestruct.Add(local);
			}
		}

		void WriteGPointerCast(CiType type, CiExpr expr)
		{
			if (type is CiNumericType || type is CiEnum) {
				Write("GINT_TO_POINTER(");
				expr.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			else if (type.Id == CiId.StringPtrType && expr.Type.Id == CiId.StringPtrType) {
				Write("(gpointer) ");
				expr.Accept(this, CiPriority.Primary);
			}
			else
				WriteCoerced(type, expr, CiPriority.Argument);
		}

		void WriteGConstPointerCast(CiExpr expr)
		{
			if (expr.Type is CiClassType && !(expr.Type is CiStorageType))
				expr.Accept(this, CiPriority.Argument);
			else {
				Write("(gconstpointer) ");
				expr.Accept(this, CiPriority.Primary);
			}
		}

		void WriteQueueObject(CiExpr obj)
		{
			if (obj.Type is CiStorageType) {
				WriteChar('&');
				obj.Accept(this, CiPriority.Primary);
			}
			else
				obj.Accept(this, CiPriority.Argument);
		}

		void WriteQueueGet(string function, CiExpr obj, CiPriority parent)
		{
			CiType elementType = obj.Type.AsClassType().GetElementType();
			bool parenthesis;
			if (elementType is CiIntegerType && elementType.Id != CiId.LongType) {
				Write("GPOINTER_TO_INT(");
				parenthesis = true;
			}
			else {
				parenthesis = parent > CiPriority.Mul;
				if (parenthesis)
					WriteChar('(');
				WriteStaticCastType(elementType);
			}
			Write(function);
			WriteChar('(');
			WriteQueueObject(obj);
			WriteChar(')');
			if (parenthesis)
				WriteChar(')');
		}

		void StartDictionaryInsert(CiExpr dict, CiExpr key)
		{
			CiClassType type = (CiClassType) dict.Type;
			Write(type.Class.Id == CiId.SortedDictionaryClass ? "g_tree_insert(" : "g_hash_table_insert(");
			dict.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteGPointerCast(type.GetKeyType(), key);
			Write(", ");
		}

		protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
		{
			if (expr.Left is CiBinaryExpr indexing && indexing.Op == CiToken.LeftBracket && indexing.Left.Type is CiClassType dict && dict.Class.TypeParameterCount == 2) {
				StartDictionaryInsert(indexing.Left, indexing.Right);
				WriteGPointerCast(dict.GetValueType(), expr.Right);
				WriteChar(')');
			}
			else if (expr.Left.Type.Id == CiId.StringStorageType) {
				CiExpr length = IsTrimSubstring(expr);
				if (length != null && parent == CiPriority.Statement) {
					WriteIndexing(expr.Left, length);
					Write(" = '\\0'");
				}
				else {
					this.StringAssign = true;
					Write("CiString_Assign(&");
					expr.Left.Accept(this, CiPriority.Primary);
					Write(", ");
					WriteStringStorageValue(expr.Right);
					WriteChar(')');
				}
			}
			else if (expr.Left.Type is CiDynamicPtrType dynamic) {
				if (dynamic.Class.Id == CiId.RegexClass) {
					base.WriteAssign(expr, parent);
				}
				else {
					this.SharedAssign = true;
					Write("CiShared_Assign((void **) &");
					expr.Left.Accept(this, CiPriority.Primary);
					Write(", ");
					if (expr.Right is CiSymbolReference) {
						this.SharedAddRef = true;
						Write("CiShared_AddRef(");
						expr.Right.Accept(this, CiPriority.Argument);
						WriteChar(')');
					}
					else
						expr.Right.Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
			}
			else
				base.WriteAssign(expr, parent);
		}

		static CiMethod GetThrowingMethod(CiExpr expr)
		{
			switch (expr) {
			case CiBinaryExpr binary when binary.Op == CiToken.Assign:
				return GetThrowingMethod(binary.Right);
			case CiCallExpr call:
				CiMethod method = (CiMethod) call.Method.Symbol;
				return method.Throws ? method : null;
			default:
				return null;
			}
		}

		static bool HasListDestroy(CiType type)
		{
			return type is CiStorageType list && (list.Class.Id == CiId.ListClass || list.Class.Id == CiId.StackClass) && NeedToDestructType(list.GetElementType());
		}

		protected override bool HasInitCode(CiNamedValue def)
		{
			if (def.IsAssignableStorage())
				return false;
			return (def is CiField && (def.Value != null || IsHeapAllocated(def.Type.GetStorageType()) || (def.Type is CiClassType klass && (klass.Class.Id == CiId.ListClass || klass.Class.Id == CiId.DictionaryClass || klass.Class.Id == CiId.SortedDictionaryClass)))) || GetThrowingMethod(def.Value) != null || (def.Type.GetStorageType() is CiStorageType storage && (storage.Class.Id == CiId.LockClass || NeedsConstructor(storage.Class))) || HasListDestroy(def.Type) || base.HasInitCode(def);
		}

		CiPriority StartForwardThrow(CiMethod throwingMethod)
		{
			Write("if (");
			switch (throwingMethod.Type.Id) {
			case CiId.FloatType:
			case CiId.DoubleType:
				IncludeMath();
				Write("isnan(");
				return CiPriority.Argument;
			case CiId.VoidType:
				WriteChar('!');
				return CiPriority.Primary;
			default:
				return CiPriority.Equality;
			}
		}

		void WriteDestruct(CiSymbol symbol)
		{
			if (!NeedToDestruct(symbol))
				return;
			EnsureChildBlock();
			CiType type = symbol.Type;
			int nesting = 0;
			while (type is CiArrayStorageType array) {
				Write("for (int _i");
				VisitLiteralLong(nesting);
				Write(" = ");
				VisitLiteralLong(array.Length - 1);
				Write("; _i");
				VisitLiteralLong(nesting);
				Write(" >= 0; _i");
				VisitLiteralLong(nesting);
				WriteLine("--)");
				this.Indent++;
				nesting++;
				type = array.GetElementType();
			}
			bool arrayFree = false;
			switch (type) {
			case CiDynamicPtrType dynamic:
				if (dynamic.Class.Id == CiId.RegexClass)
					Write("g_regex_unref(");
				else {
					this.SharedRelease = true;
					Write("CiShared_Release(");
				}
				break;
			case CiStorageType storage:
				switch (storage.Class.Id) {
				case CiId.ListClass:
				case CiId.StackClass:
					Write("g_array_free(");
					arrayFree = true;
					break;
				case CiId.QueueClass:
					Write("g_queue_clear(&");
					break;
				case CiId.HashSetClass:
				case CiId.DictionaryClass:
					Write("g_hash_table_unref(");
					break;
				case CiId.SortedSetClass:
				case CiId.SortedDictionaryClass:
					Write("g_tree_unref(");
					break;
				case CiId.MatchClass:
					Write("g_match_info_free(");
					break;
				case CiId.LockClass:
					Write("mtx_destroy(&");
					break;
				default:
					WriteName(storage.Class);
					Write("_Destruct(&");
					break;
				}
				break;
			default:
				Write("free(");
				break;
			}
			WriteLocalName(symbol, CiPriority.Primary);
			for (int i = 0; i < nesting; i++) {
				Write("[_i");
				VisitLiteralLong(i);
				WriteChar(']');
			}
			if (arrayFree)
				Write(", TRUE");
			WriteLine(");");
			this.Indent -= nesting;
		}

		void WriteDestructAll(CiVar exceptVar = null)
		{
			for (int i = this.VarsToDestruct.Count; --i >= 0;) {
				CiVar def = this.VarsToDestruct[i];
				if (def != exceptVar)
					WriteDestruct(def);
			}
		}

		void WriteThrowReturnValue()
		{
			if (this.CurrentMethod.Type is CiNumericType) {
				if (this.CurrentMethod.Type is CiIntegerType)
					Write("-1");
				else {
					IncludeMath();
					Write("NAN");
				}
			}
			else if (this.CurrentMethod.Type.Id == CiId.VoidType)
				Write("false");
			else
				Write("NULL");
		}

		void WriteThrow()
		{
			WriteDestructAll();
			Write("return ");
			WriteThrowReturnValue();
			WriteCharLine(';');
		}

		void EndForwardThrow(CiMethod throwingMethod)
		{
			switch (throwingMethod.Type.Id) {
			case CiId.FloatType:
			case CiId.DoubleType:
				WriteChar(')');
				break;
			case CiId.VoidType:
				break;
			default:
				Write(throwingMethod.Type is CiIntegerType ? " == -1" : " == NULL");
				break;
			}
			WriteChar(')');
			if (this.VarsToDestruct.Count > 0) {
				WriteChar(' ');
				OpenBlock();
				WriteThrow();
				CloseBlock();
			}
			else {
				WriteNewLine();
				this.Indent++;
				WriteThrow();
				this.Indent--;
			}
		}

		protected override void WriteInitCode(CiNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			CiType type = def.Type;
			int nesting = 0;
			while (type is CiArrayStorageType array) {
				OpenLoop("int", nesting++, array.Length);
				type = array.GetElementType();
			}
			if (type is CiStorageType lok && lok.Class.Id == CiId.LockClass) {
				Write("mtx_init(&");
				WriteArrayElement(def, nesting);
				WriteLine(", mtx_plain | mtx_recursive);");
			}
			else if (type is CiStorageType storage && NeedsConstructor(storage.Class)) {
				WriteName(storage.Class);
				Write("_Construct(&");
				WriteArrayElement(def, nesting);
				WriteLine(");");
			}
			else {
				if (def is CiField) {
					WriteArrayElement(def, nesting);
					if (nesting > 0) {
						Write(" = ");
						if (IsHeapAllocated(type))
							Write("NULL");
						else
							def.Value.Accept(this, CiPriority.Argument);
					}
					else
						WriteVarInit(def);
					WriteCharLine(';');
				}
				CiMethod throwingMethod = GetThrowingMethod(def.Value);
				if (throwingMethod != null) {
					StartForwardThrow(throwingMethod);
					WriteArrayElement(def, nesting);
					EndForwardThrow(throwingMethod);
				}
			}
			if (HasListDestroy(type)) {
				Write("g_array_set_clear_func(");
				WriteArrayElement(def, nesting);
				Write(", ");
				switch (type.AsClassType().GetElementType()) {
				case CiStringStorageType _:
					this.ListFrees["String"] = "free(*(void **) ptr)";
					Write("CiList_FreeString");
					break;
				case CiDynamicPtrType _:
					this.SharedRelease = true;
					this.ListFrees["Shared"] = "CiShared_Release(*(void **) ptr)";
					Write("CiList_FreeShared");
					break;
				case CiStorageType storage:
					switch (storage.Class.Id) {
					case CiId.ListClass:
					case CiId.StackClass:
						this.ListFrees["List"] = "g_array_free(*(GArray **) ptr, TRUE)";
						Write("CiList_FreeList");
						break;
					case CiId.HashSetClass:
					case CiId.DictionaryClass:
						this.ListFrees["HashTable"] = "g_hash_table_unref(*(GHashTable **) ptr)";
						Write("CiList_FreeHashTable");
						break;
					case CiId.SortedSetClass:
					case CiId.SortedDictionaryClass:
						this.ListFrees["Tree"] = "g_tree_unref(*(GTree **) ptr)";
						Write("CiList_FreeTree");
						break;
					default:
						Write("(GDestroyNotify) ");
						WriteName(storage.Class);
						Write("_Destruct");
						break;
					}
					break;
				default:
					throw new NotImplementedException();
				}
				WriteLine(");");
			}
			while (--nesting >= 0)
				CloseBlock();
			base.WriteInitCode(def);
		}

		void WriteMemberAccess(CiExpr left, CiSymbol symbolClass)
		{
			if (left.Type is CiStorageType)
				WriteChar('.');
			else
				Write("->");
			for (CiSymbol klass = left.Type.AsClassType().Class; klass != symbolClass; klass = klass.Parent)
				Write("base.");
		}

		protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
		{
			WriteMemberAccess(left, symbol.Symbol.Parent);
		}

		protected override void WriteArrayPtr(CiExpr expr, CiPriority parent)
		{
			if (expr.Type is CiClassType list && list.Class.Id == CiId.ListClass) {
				WriteChar('(');
				WriteType(list.GetElementType(), false);
				Write(" *) ");
				WritePostfix(expr, "->data");
			}
			else
				expr.Accept(this, parent);
		}

		protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
		{
			switch (type) {
			case CiDynamicPtrType dynamic when expr is CiSymbolReference && parent != CiPriority.Equality:
				this.SharedAddRef = true;
				if (dynamic.Class.Id == CiId.ArrayPtrClass)
					WriteDynamicArrayCast(dynamic.GetElementType());
				else {
					WriteChar('(');
					WriteName(dynamic.Class);
					Write(" *) ");
				}
				WriteCall("CiShared_AddRef", expr);
				break;
			case CiClassType klass when klass.Class.Id != CiId.StringClass && klass.Class.Id != CiId.ArrayPtrClass && !(klass is CiStorageType):
				if (klass.Class.Id == CiId.QueueClass && expr.Type is CiStorageType) {
					WriteChar('&');
					expr.Accept(this, CiPriority.Primary);
				}
				else
					WriteClassPtr(klass.Class, expr, parent);
				break;
			default:
				if (type.Id == CiId.StringStorageType)
					WriteStringStorageValue(expr);
				else if (expr.Type.Id == CiId.StringStorageType)
					WriteTemporaryOrExpr(expr, parent);
				else
					base.WriteCoercedInternal(type, expr, parent);
				break;
			}
		}

		protected virtual void WriteSubstringEqual(CiCallExpr call, string literal, CiPriority parent, bool not)
		{
			if (parent > CiPriority.Equality)
				WriteChar('(');
			Include("string.h");
			Write("memcmp(");
			WriteStringPtrAdd(call);
			Write(", ");
			VisitLiteralString(literal);
			Write(", ");
			VisitLiteralLong(literal.Length);
			WriteChar(')');
			Write(GetEqOp(not));
			WriteChar('0');
			if (parent > CiPriority.Equality)
				WriteChar(')');
		}

		protected virtual void WriteEqualStringInternal(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			if (parent > CiPriority.Equality)
				WriteChar('(');
			Include("string.h");
			Write("strcmp(");
			WriteTemporaryOrExpr(left, CiPriority.Argument);
			Write(", ");
			WriteTemporaryOrExpr(right, CiPriority.Argument);
			WriteChar(')');
			Write(GetEqOp(not));
			WriteChar('0');
			if (parent > CiPriority.Equality)
				WriteChar(')');
		}

		protected override void WriteEqual(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			if (left.Type is CiStringType && right.Type is CiStringType) {
				CiCallExpr call = IsStringSubstring(left);
				if (call != null && right is CiLiteralString literal) {
					CiExpr lengthExpr = GetStringSubstringLength(call);
					int rightLength = literal.GetAsciiLength();
					if (rightLength >= 0) {
						string rightValue = literal.Value;
						if (lengthExpr is CiLiteralLong leftLength) {
							if (leftLength.Value != rightLength)
								NotYet(left, "String comparison with unmatched length");
							WriteSubstringEqual(call, rightValue, parent, not);
						}
						else if (not) {
							if (parent > CiPriority.CondOr)
								WriteChar('(');
							lengthExpr.Accept(this, CiPriority.Equality);
							Write(" != ");
							VisitLiteralLong(rightLength);
							Write(" || ");
							WriteSubstringEqual(call, rightValue, CiPriority.CondOr, true);
							if (parent > CiPriority.CondOr)
								WriteChar(')');
						}
						else {
							if (parent > CiPriority.CondAnd || parent == CiPriority.CondOr)
								WriteChar('(');
							lengthExpr.Accept(this, CiPriority.Equality);
							Write(" == ");
							VisitLiteralLong(rightLength);
							Write(" && ");
							WriteSubstringEqual(call, rightValue, CiPriority.CondAnd, false);
							if (parent > CiPriority.CondAnd || parent == CiPriority.CondOr)
								WriteChar(')');
						}
						return;
					}
				}
				WriteEqualStringInternal(left, right, parent, not);
			}
			else
				base.WriteEqual(left, right, parent, not);
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			Include("string.h");
			WriteCall("(int) strlen", expr);
		}

		void WriteStringMethod(string name, CiExpr obj, List<CiExpr> args)
		{
			Include("string.h");
			Write("CiString_");
			WriteCall(name, obj, args[0]);
		}

		void WriteSizeofCompare(CiType elementType)
		{
			Write(", sizeof(");
			CiId typeId = elementType.Id;
			WriteNumericType(typeId);
			Write("), CiCompare_");
			WriteNumericType(typeId);
			WriteChar(')');
			this.Compares.Add(typeId);
		}

		protected void WriteArrayFill(CiExpr obj, List<CiExpr> args)
		{
			Write("for (int _i = 0; _i < ");
			if (args.Count == 1)
				WriteArrayStorageLength(obj);
			else
				args[2].Accept(this, CiPriority.Rel);
			WriteLine("; _i++)");
			WriteChar('\t');
			obj.Accept(this, CiPriority.Primary);
			WriteChar('[');
			if (args.Count > 1)
				StartAdd(args[1]);
			Write("_i] = ");
			args[0].Accept(this, CiPriority.Argument);
		}

		void WriteListAddInsert(CiExpr obj, bool insert, string function, List<CiExpr> args)
		{
			CiType elementType = obj.Type.AsClassType().GetElementType();
			int id = WriteCTemporary(elementType, elementType.IsFinal() ? null : args[^1]);
			if (elementType is CiStorageType storage && NeedsConstructor(storage.Class)) {
				WriteName(storage.Class);
				Write("_Construct(&citemp");
				VisitLiteralLong(id);
				WriteLine(");");
			}
			Write(function);
			WriteChar('(');
			obj.Accept(this, CiPriority.Argument);
			if (insert) {
				Write(", ");
				args[0].Accept(this, CiPriority.Argument);
			}
			Write(", citemp");
			VisitLiteralLong(id);
			WriteChar(')');
			this.CurrentTemporaries[id] = elementType;
		}

		void WriteDictionaryLookup(CiExpr obj, string function, CiExpr key)
		{
			Write(function);
			WriteChar('(');
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteGConstPointerCast(key);
			WriteChar(')');
		}

		void WriteArgsAndRightParenthesis(CiMethod method, List<CiExpr> args)
		{
			int i = 0;
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (i > 0 || method.CallType != CiCallType.Static)
					Write(", ");
				if (i >= args.Count)
					param.Value.Accept(this, CiPriority.Argument);
				else
					WriteCoerced(param.Type, args[i], CiPriority.Argument);
				i++;
			}
			WriteChar(')');
		}

		void WriteCRegexOptions(List<CiExpr> args)
		{
			if (!WriteRegexOptions(args, "", " | ", "", "G_REGEX_CASELESS", "G_REGEX_MULTILINE", "G_REGEX_DOTALL"))
				WriteChar('0');
		}

		protected void WritePrintfNotInterpolated(List<CiExpr> args, bool newLine)
		{
			Write("\"%");
			switch (args[0].Type) {
			case CiIntegerType intType:
				if (intType.Id == CiId.LongType)
					WritePrintfLongPrefix();
				WriteChar('d');
				break;
			case CiFloatingType _:
				WriteChar('g');
				break;
			default:
				WriteChar('s');
				break;
			}
			if (newLine)
				Write("\\n");
			Write("\", ");
			WriteInterpolatedStringArgBase(args[0]);
			WriteChar(')');
		}

		void WriteTextWriterWrite(CiExpr obj, List<CiExpr> args, bool newLine)
		{
			if (args.Count == 0) {
				Write("putc('\\n', ");
				obj.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			else if (args[0] is CiInterpolatedString interpolated) {
				Write("fprintf(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WritePrintf(interpolated, newLine);
			}
			else if (args[0].Type is CiNumericType) {
				Write("fprintf(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WritePrintfNotInterpolated(args, newLine);
			}
			else if (!newLine) {
				Write("fputs(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				obj.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			else if (args[0] is CiLiteralString literal) {
				Write("fputs(");
				WriteStringLiteralWithNewLine(literal.Value);
				Write(", ");
				obj.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			else {
				Write("fprintf(");
				obj.Accept(this, CiPriority.Argument);
				Write(", \"%s\\n\", ");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
		}

		void WriteConsoleWrite(List<CiExpr> args, bool newLine)
		{
			Include("stdio.h");
			if (args.Count == 0)
				Write("putchar('\\n')");
			else if (args[0] is CiInterpolatedString interpolated) {
				Write("printf(");
				WritePrintf(interpolated, newLine);
			}
			else if (args[0].Type is CiNumericType) {
				Write("printf(");
				WritePrintfNotInterpolated(args, newLine);
			}
			else if (!newLine) {
				Write("fputs(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", stdout)");
			}
			else
				WriteCall("puts", args[0]);
		}

		static CiClass GetVtblStructClass(CiClass klass)
		{
			while (!klass.AddsVirtualMethods()) {
				CiClass baseClass = (CiClass) klass.Parent;
				klass = baseClass;
			}
			return klass;
		}

		static CiClass GetVtblPtrClass(CiClass klass)
		{
			for (CiClass result = null;;) {
				if (klass.AddsVirtualMethods())
					result = klass;
				if (!(klass.Parent is CiClass baseClass))
					return result;
				klass = baseClass;
			}
		}

		protected void WriteCCall(CiExpr obj, CiMethod method, List<CiExpr> args)
		{
			CiClass klass = this.CurrentClass;
			CiClass declaringClass = (CiClass) method.Parent;
			if (IsReferenceTo(obj, CiId.BasePtr)) {
				WriteName(method);
				Write("(&self->base");
				WriteUpcast(declaringClass, klass.Parent);
			}
			else {
				CiClass definingClass = declaringClass;
				switch (method.CallType) {
				case CiCallType.Abstract:
				case CiCallType.Virtual:
				case CiCallType.Override:
					if (method.CallType == CiCallType.Override) {
						CiClass declaringClass1 = (CiClass) method.GetDeclaringMethod().Parent;
						declaringClass = declaringClass1;
					}
					if (obj != null)
						klass = obj.Type.AsClassType().Class;
					CiClass ptrClass = GetVtblPtrClass(klass);
					CiClass structClass = GetVtblStructClass(definingClass);
					if (structClass != ptrClass) {
						Write("((const ");
						WriteName(structClass);
						Write("Vtbl *) ");
					}
					if (obj != null) {
						obj.Accept(this, CiPriority.Primary);
						WriteMemberAccess(obj, ptrClass);
					}
					else
						WriteSelfForField(ptrClass);
					Write("vtbl");
					if (structClass != ptrClass)
						WriteChar(')');
					Write("->");
					WriteCamelCase(method.Name);
					break;
				default:
					WriteName(method);
					break;
				}
				WriteChar('(');
				if (method.CallType != CiCallType.Static) {
					if (obj != null)
						WriteClassPtr(declaringClass, obj, CiPriority.Argument);
					else if (klass == declaringClass)
						Write("self");
					else {
						Write("&self->base");
						WriteUpcast(declaringClass, klass.Parent);
					}
				}
			}
			WriteArgsAndRightParenthesis(method, args);
		}

		void WriteTryParse(CiExpr obj, List<CiExpr> args)
		{
			IncludeStdBool();
			Write("_TryParse(&");
			obj.Accept(this, CiPriority.Primary);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			if (obj.Type is CiIntegerType)
				WriteTryParseRadix(args);
			WriteChar(')');
		}

		void StartArrayIndexing(CiExpr obj, CiType elementType)
		{
			Write("g_array_index(");
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			WriteType(elementType, false);
			Write(", ");
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.None:
			case CiId.ClassToString:
				WriteCCall(obj, method, args);
				break;
			case CiId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case CiId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case CiId.IntTryParse:
				this.IntTryParse = true;
				Write("CiInt");
				WriteTryParse(obj, args);
				break;
			case CiId.LongTryParse:
				this.LongTryParse = true;
				Write("CiLong");
				WriteTryParse(obj, args);
				break;
			case CiId.DoubleTryParse:
				this.DoubleTryParse = true;
				Write("CiDouble");
				WriteTryParse(obj, args);
				break;
			case CiId.StringContains:
				Include("string.h");
				if (parent > CiPriority.Equality)
					WriteChar('(');
				int c = GetOneAscii(args[0]);
				if (c >= 0) {
					Write("strchr(");
					obj.Accept(this, CiPriority.Argument);
					Write(", ");
					VisitLiteralChar(c);
					WriteChar(')');
				}
				else
					WriteCall("strstr", obj, args[0]);
				Write(" != NULL");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.StringEndsWith:
				this.StringEndsWith = true;
				WriteStringMethod("EndsWith", obj, args);
				break;
			case CiId.StringIndexOf:
				this.StringIndexOf = true;
				WriteStringMethod("IndexOf", obj, args);
				break;
			case CiId.StringLastIndexOf:
				this.StringLastIndexOf = true;
				WriteStringMethod("LastIndexOf", obj, args);
				break;
			case CiId.StringReplace:
				Include("string.h");
				this.StringAppend = true;
				this.StringReplace = true;
				WriteCall("CiString_Replace", obj, args[0], args[1]);
				break;
			case CiId.StringStartsWith:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				int c2 = GetOneAscii(args[0]);
				if (c2 >= 0) {
					WritePostfix(obj, "[0] == ");
					VisitLiteralChar(c2);
				}
				else {
					Include("string.h");
					Write("strncmp(");
					obj.Accept(this, CiPriority.Argument);
					Write(", ");
					args[0].Accept(this, CiPriority.Argument);
					Write(", strlen(");
					args[0].Accept(this, CiPriority.Argument);
					Write(")) == 0");
				}
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.StringSubstring:
				if (args.Count == 1) {
					if (parent > CiPriority.Add)
						WriteChar('(');
					WriteAdd(obj, args[0]);
					if (parent > CiPriority.Add)
						WriteChar(')');
				}
				else
					NotSupported(obj, "Substring");
				break;
			case CiId.ArrayBinarySearchAll:
			case CiId.ArrayBinarySearchPart:
				if (parent > CiPriority.Add)
					WriteChar('(');
				Write("(const ");
				CiType elementType2 = obj.Type.AsClassType().GetElementType();
				WriteType(elementType2, false);
				Write(" *) bsearch(&");
				args[0].Accept(this, CiPriority.Primary);
				Write(", ");
				if (args.Count == 1)
					WriteArrayPtr(obj, CiPriority.Argument);
				else
					WriteArrayPtrAdd(obj, args[1]);
				Write(", ");
				if (args.Count == 1)
					WriteArrayStorageLength(obj);
				else
					args[2].Accept(this, CiPriority.Argument);
				WriteSizeofCompare(elementType2);
				Write(" - ");
				WriteArrayPtr(obj, CiPriority.Mul);
				if (parent > CiPriority.Add)
					WriteChar(')');
				break;
			case CiId.ArrayCopyTo:
			case CiId.ListCopyTo:
				Include("string.h");
				CiType elementType = obj.Type.AsClassType().GetElementType();
				if (IsHeapAllocated(elementType))
					NotYet(obj, "CopyTo for this type");
				Write("memcpy(");
				WriteArrayPtrAdd(args[1], args[2]);
				Write(", ");
				WriteArrayPtrAdd(obj, args[0]);
				Write(", ");
				if (elementType.Id == CiId.SByteRange || elementType.Id == CiId.ByteRange)
					args[3].Accept(this, CiPriority.Argument);
				else {
					args[3].Accept(this, CiPriority.Mul);
					Write(" * sizeof(");
					WriteType(elementType, false);
					WriteChar(')');
				}
				WriteChar(')');
				break;
			case CiId.ArrayFillAll:
			case CiId.ArrayFillPart:
				if (args[0] is CiLiteral literal && literal.IsDefaultValue()) {
					Include("string.h");
					Write("memset(");
					if (args.Count == 1) {
						obj.Accept(this, CiPriority.Argument);
						Write(", 0, sizeof(");
						obj.Accept(this, CiPriority.Argument);
						WriteChar(')');
					}
					else {
						WriteArrayPtrAdd(obj, args[1]);
						Write(", 0, ");
						args[2].Accept(this, CiPriority.Mul);
						Write(" * sizeof(");
						WriteType(obj.Type.AsClassType().GetElementType(), false);
						WriteChar(')');
					}
					WriteChar(')');
				}
				else
					WriteArrayFill(obj, args);
				break;
			case CiId.ArraySortAll:
				Write("qsort(");
				WriteArrayPtr(obj, CiPriority.Argument);
				Write(", ");
				WriteArrayStorageLength(obj);
				WriteSizeofCompare(obj.Type.AsClassType().GetElementType());
				break;
			case CiId.ArraySortPart:
			case CiId.ListSortPart:
				Write("qsort(");
				WriteArrayPtrAdd(obj, args[0]);
				Write(", ");
				args[1].Accept(this, CiPriority.Argument);
				WriteSizeofCompare(obj.Type.AsClassType().GetElementType());
				break;
			case CiId.ListAdd:
			case CiId.StackPush:
				switch (obj.Type.AsClassType().GetElementType()) {
				case CiArrayStorageType _:
				case CiStorageType storage when storage.Class.Id == CiId.None && !NeedsConstructor(storage.Class):
					Write("g_array_set_size(");
					obj.Accept(this, CiPriority.Argument);
					Write(", ");
					WritePostfix(obj, "->len + 1)");
					break;
				default:
					WriteListAddInsert(obj, false, "g_array_append_val", args);
					break;
				}
				break;
			case CiId.ListClear:
			case CiId.StackClear:
				Write("g_array_set_size(");
				obj.Accept(this, CiPriority.Argument);
				Write(", 0)");
				break;
			case CiId.ListContains:
				Write("CiArray_Contains_");
				CiId typeId = obj.Type.AsClassType().GetElementType().Id;
				if (typeId == CiId.StringStorageType)
					typeId = CiId.StringPtrType;
				if (typeId == CiId.StringPtrType) {
					Include("string.h");
					Write("string((const char * const");
				}
				else {
					WriteNumericType(typeId);
					Write("((const ");
					WriteNumericType(typeId);
				}
				Write(" *) ");
				WritePostfix(obj, "->data, ");
				WritePostfix(obj, "->len, ");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				this.Contains.Add(typeId);
				break;
			case CiId.ListInsert:
				WriteListAddInsert(obj, true, "g_array_insert_val", args);
				break;
			case CiId.ListLast:
			case CiId.StackPeek:
				StartArrayIndexing(obj, obj.Type.AsClassType().GetElementType());
				WritePostfix(obj, "->len - 1)");
				break;
			case CiId.ListRemoveAt:
				WriteCall("g_array_remove_index", obj, args[0]);
				break;
			case CiId.ListRemoveRange:
				WriteCall("g_array_remove_range", obj, args[0], args[1]);
				break;
			case CiId.ListSortAll:
				Write("g_array_sort(");
				obj.Accept(this, CiPriority.Argument);
				CiId typeId2 = obj.Type.AsClassType().GetElementType().Id;
				Write(", CiCompare_");
				WriteNumericType(typeId2);
				WriteChar(')');
				this.Compares.Add(typeId2);
				break;
			case CiId.QueueClear:
				Write("g_queue_clear(");
				WriteQueueObject(obj);
				WriteChar(')');
				break;
			case CiId.QueueDequeue:
				WriteQueueGet("g_queue_pop_head", obj, parent);
				break;
			case CiId.QueueEnqueue:
				Write("g_queue_push_tail(");
				WriteQueueObject(obj);
				Write(", ");
				WriteGPointerCast(obj.Type.AsClassType().GetElementType(), args[0]);
				WriteChar(')');
				break;
			case CiId.QueuePeek:
				WriteQueueGet("g_queue_peek_head", obj, parent);
				break;
			case CiId.StackPop:
				StartArrayIndexing(obj, obj.Type.AsClassType().GetElementType());
				Write("--");
				WritePostfix(obj, "->len)");
				break;
			case CiId.HashSetAdd:
				Write("g_hash_table_add(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteGPointerCast(obj.Type.AsClassType().GetElementType(), args[0]);
				WriteChar(')');
				break;
			case CiId.HashSetClear:
			case CiId.DictionaryClear:
				WriteCall("g_hash_table_remove_all", obj);
				break;
			case CiId.HashSetContains:
			case CiId.DictionaryContainsKey:
				WriteDictionaryLookup(obj, "g_hash_table_contains", args[0]);
				break;
			case CiId.HashSetRemove:
			case CiId.DictionaryRemove:
				WriteDictionaryLookup(obj, "g_hash_table_remove", args[0]);
				break;
			case CiId.SortedSetAdd:
				Write("g_tree_insert(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteGPointerCast(obj.Type.AsClassType().GetKeyType(), args[0]);
				Write(", NULL)");
				break;
			case CiId.DictionaryAdd:
				StartDictionaryInsert(obj, args[0]);
				CiClassType valueType = obj.Type.AsClassType().GetValueType().AsClassType();
				switch (valueType.Class.Id) {
				case CiId.ListClass:
				case CiId.StackClass:
				case CiId.DictionaryClass:
				case CiId.SortedDictionaryClass:
					WriteNewStorage(valueType);
					break;
				default:
					if (valueType.Class.IsPublic && valueType.Class.Constructor != null && valueType.Class.Constructor.Visibility == CiVisibility.Public) {
						WriteName(valueType.Class);
						Write("_New()");
					}
					else {
						Write("malloc(sizeof(");
						WriteType(valueType, false);
						Write("))");
					}
					break;
				}
				WriteChar(')');
				break;
			case CiId.SortedSetClear:
			case CiId.SortedDictionaryClear:
				Write("g_tree_destroy(g_tree_ref(");
				obj.Accept(this, CiPriority.Argument);
				Write("))");
				break;
			case CiId.SortedSetContains:
			case CiId.SortedDictionaryContainsKey:
				Write("g_tree_lookup_extended(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteGConstPointerCast(args[0]);
				Write(", NULL, NULL)");
				break;
			case CiId.SortedSetRemove:
			case CiId.SortedDictionaryRemove:
				WriteDictionaryLookup(obj, "g_tree_remove", args[0]);
				break;
			case CiId.TextWriterWrite:
				WriteTextWriterWrite(obj, args, false);
				break;
			case CiId.TextWriterWriteChar:
				WriteCall("putc", args[0], obj);
				break;
			case CiId.TextWriterWriteLine:
				WriteTextWriterWrite(obj, args, true);
				break;
			case CiId.ConsoleWrite:
				WriteConsoleWrite(args, false);
				break;
			case CiId.ConsoleWriteLine:
				WriteConsoleWrite(args, true);
				break;
			case CiId.UTF8GetByteCount:
				WriteStringLength(args[0]);
				break;
			case CiId.UTF8GetBytes:
				Include("string.h");
				Write("memcpy(");
				WriteArrayPtrAdd(args[1], args[2]);
				Write(", ");
				args[0].Accept(this, CiPriority.Argument);
				Write(", strlen(");
				args[0].Accept(this, CiPriority.Argument);
				Write("))");
				break;
			case CiId.EnvironmentGetEnvironmentVariable:
				WriteCall("getenv", args[0]);
				break;
			case CiId.RegexCompile:
				WriteGlib("g_regex_new(");
				WriteTemporaryOrExpr(args[0], CiPriority.Argument);
				Write(", ");
				WriteCRegexOptions(args);
				Write(", 0, NULL)");
				break;
			case CiId.RegexEscape:
				WriteGlib("g_regex_escape_string(");
				WriteTemporaryOrExpr(args[0], CiPriority.Argument);
				Write(", -1)");
				break;
			case CiId.RegexIsMatchStr:
				WriteGlib("g_regex_match_simple(");
				WriteTemporaryOrExpr(args[1], CiPriority.Argument);
				Write(", ");
				WriteTemporaryOrExpr(args[0], CiPriority.Argument);
				Write(", ");
				WriteCRegexOptions(args);
				Write(", 0)");
				break;
			case CiId.RegexIsMatchRegex:
				Write("g_regex_match(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteTemporaryOrExpr(args[0], CiPriority.Argument);
				Write(", 0, NULL)");
				break;
			case CiId.MatchFindStr:
				this.MatchFind = true;
				Write("CiMatch_Find(&");
				obj.Accept(this, CiPriority.Primary);
				Write(", ");
				WriteTemporaryOrExpr(args[0], CiPriority.Argument);
				Write(", ");
				WriteTemporaryOrExpr(args[1], CiPriority.Argument);
				Write(", ");
				WriteCRegexOptions(args);
				WriteChar(')');
				break;
			case CiId.MatchFindRegex:
				Write("g_regex_match(");
				args[1].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteTemporaryOrExpr(args[0], CiPriority.Argument);
				Write(", 0, &");
				obj.Accept(this, CiPriority.Primary);
				WriteChar(')');
				break;
			case CiId.MatchGetCapture:
				WriteCall("g_match_info_fetch", obj, args[0]);
				break;
			case CiId.MathMethod:
			case CiId.MathIsFinite:
			case CiId.MathIsNaN:
			case CiId.MathLog2:
				IncludeMath();
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathAbs:
				switch (args[0].Type.Id) {
				case CiId.LongType:
					WriteCall("llabs", args[0]);
					break;
				case CiId.FloatType:
					IncludeMath();
					WriteCall("fabsf", args[0]);
					break;
				case CiId.FloatIntType:
				case CiId.DoubleType:
					IncludeMath();
					WriteCall("fabs", args[0]);
					break;
				default:
					WriteCall("abs", args[0]);
					break;
				}
				break;
			case CiId.MathCeiling:
				IncludeMath();
				WriteCall("ceil", args[0]);
				break;
			case CiId.MathFusedMultiplyAdd:
				IncludeMath();
				WriteCall("fma", args[0], args[1], args[2]);
				break;
			case CiId.MathIsInfinity:
				IncludeMath();
				WriteCall("isinf", args[0]);
				break;
			case CiId.MathMaxDouble:
				IncludeMath();
				WriteCall("fmax", args[0], args[1]);
				break;
			case CiId.MathMinDouble:
				IncludeMath();
				WriteCall("fmin", args[0], args[1]);
				break;
			case CiId.MathRound:
				IncludeMath();
				WriteCall("round", args[0]);
				break;
			case CiId.MathTruncate:
				IncludeMath();
				WriteCall("trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		void WriteDictionaryIndexing(string function, CiBinaryExpr expr, CiPriority parent)
		{
			CiType valueType = expr.Left.Type.AsClassType().GetValueType();
			if (valueType is CiIntegerType && valueType.Id != CiId.LongType) {
				Write("GPOINTER_TO_INT(");
				WriteDictionaryLookup(expr.Left, function, expr.Right);
				WriteChar(')');
			}
			else {
				if (parent > CiPriority.Mul)
					WriteChar('(');
				if (valueType is CiStorageType storage && (storage.Class.Id == CiId.None || storage.Class.Id == CiId.ArrayStorageClass))
					WriteDynamicArrayCast(valueType);
				else {
					WriteStaticCastType(valueType);
					if (valueType is CiEnum) {
						Debug.Assert(parent <= CiPriority.Mul, "Should close two parens");
						Write("GPOINTER_TO_INT(");
					}
				}
				WriteDictionaryLookup(expr.Left, function, expr.Right);
				if (parent > CiPriority.Mul || valueType is CiEnum)
					WriteChar(')');
			}
		}

		protected override void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
		{
			if (expr.Left.Type is CiClassType klass) {
				switch (klass.Class.Id) {
				case CiId.ListClass:
					if (klass.GetElementType() is CiArrayStorageType) {
						WriteChar('(');
						WriteDynamicArrayCast(klass.GetElementType());
						WritePostfix(expr.Left, "->data)[");
						expr.Right.Accept(this, CiPriority.Argument);
						WriteChar(']');
					}
					else {
						StartArrayIndexing(expr.Left, klass.GetElementType());
						expr.Right.Accept(this, CiPriority.Argument);
						WriteChar(')');
					}
					return;
				case CiId.DictionaryClass:
					WriteDictionaryIndexing("g_hash_table_lookup", expr, parent);
					return;
				case CiId.SortedDictionaryClass:
					WriteDictionaryIndexing("g_tree_lookup", expr, parent);
					return;
				default:
					break;
				}
			}
			base.WriteIndexingExpr(expr, parent);
		}

		public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Plus:
				if (expr.Type.Id == CiId.StringStorageType)
					NotSupported(expr, "String concatenation");
				break;
			case CiToken.Equal:
			case CiToken.NotEqual:
			case CiToken.Greater:
				CiExpr str = IsStringEmpty(expr);
				if (str != null) {
					WritePostfix(str, expr.Op == CiToken.Equal ? "[0] == '\\0'" : "[0] != '\\0'");
					return;
				}
				break;
			case CiToken.AddAssign:
				if (expr.Left.Type.Id == CiId.StringStorageType) {
					if (expr.Right is CiInterpolatedString rightInterpolated) {
						this.StringAssign = true;
						Write("CiString_Assign(&");
						expr.Left.Accept(this, CiPriority.Primary);
						this.StringFormat = true;
						Include("stdarg.h");
						Include("stdio.h");
						Write(", CiString_Format(\"%s");
						WritePrintfFormat(rightInterpolated);
						Write("\", ");
						expr.Left.Accept(this, CiPriority.Argument);
						WriteInterpolatedStringArgs(rightInterpolated);
						WriteChar(')');
					}
					else {
						Include("string.h");
						this.StringAppend = true;
						Write("CiString_Append(&");
						expr.Left.Accept(this, CiPriority.Primary);
						Write(", ");
						expr.Right.Accept(this, CiPriority.Argument);
					}
					WriteChar(')');
					return;
				}
				break;
			case CiToken.Is:
				NotSupported(expr, "'is' operator");
				break;
			default:
				break;
			}
			base.VisitBinaryExpr(expr, parent);
		}

		protected override void WriteResource(string name, int length)
		{
			Write("CiResource_");
			WriteResourceName(name);
		}

		public override void VisitLambdaExpr(CiLambdaExpr expr)
		{
			NotSupported(expr, "Lambda expression");
		}

		void WriteDestructLoopOrSwitch(CiCondCompletionStatement loopOrSwitch)
		{
			for (int i = this.VarsToDestruct.Count; --i >= 0;) {
				CiVar def = this.VarsToDestruct[i];
				if (!loopOrSwitch.Encloses(def))
					break;
				WriteDestruct(def);
			}
		}

		void TrimVarsToDestruct(int i)
		{
			this.VarsToDestruct.RemoveRange(i, this.VarsToDestruct.Count - i);
		}

		protected override void CleanupBlock(CiBlock statement)
		{
			int i = this.VarsToDestruct.Count;
			for (; i > 0; i--) {
				CiVar def = this.VarsToDestruct[i - 1];
				if (def.Parent != statement)
					break;
				if (statement.CompletesNormally())
					WriteDestruct(def);
			}
			TrimVarsToDestruct(i);
		}

		public override void VisitBreak(CiBreak statement)
		{
			WriteDestructLoopOrSwitch(statement.LoopOrSwitch);
			base.VisitBreak(statement);
		}

		public override void VisitContinue(CiContinue statement)
		{
			WriteDestructLoopOrSwitch(statement.Loop);
			base.VisitContinue(statement);
		}

		public override void VisitExpr(CiExpr statement)
		{
			WriteCTemporaries(statement);
			CiMethod throwingMethod = GetThrowingMethod(statement);
			if (throwingMethod != null) {
				EnsureChildBlock();
				statement.Accept(this, StartForwardThrow(throwingMethod));
				EndForwardThrow(throwingMethod);
				CleanupTemporaries();
			}
			else if (statement is CiCallExpr && statement.Type.Id == CiId.StringStorageType) {
				Write("free(");
				statement.Accept(this, CiPriority.Argument);
				WriteLine(");");
				CleanupTemporaries();
			}
			else if (statement is CiCallExpr && statement.Type is CiDynamicPtrType) {
				this.SharedRelease = true;
				Write("CiShared_Release(");
				statement.Accept(this, CiPriority.Argument);
				WriteLine(");");
				CleanupTemporaries();
			}
			else
				base.VisitExpr(statement);
		}

		void StartForeachHashTable(CiForeach statement)
		{
			OpenBlock();
			WriteLine("GHashTableIter cidictit;");
			Write("g_hash_table_iter_init(&cidictit, ");
			statement.Collection.Accept(this, CiPriority.Argument);
			WriteLine(");");
		}

		void WriteDictIterVar(CiNamedValue iter, string value)
		{
			WriteTypeAndName(iter);
			Write(" = ");
			if (iter.Type is CiIntegerType && iter.Type.Id != CiId.LongType) {
				Write("GPOINTER_TO_INT(");
				Write(value);
				WriteChar(')');
			}
			else {
				WriteStaticCastType(iter.Type);
				Write(value);
			}
			WriteCharLine(';');
		}

		public override void VisitForeach(CiForeach statement)
		{
			string element = statement.GetVar().Name;
			switch (statement.Collection.Type) {
			case CiArrayStorageType array:
				Write("for (int ");
				WriteCamelCaseNotKeyword(element);
				Write(" = 0; ");
				WriteCamelCaseNotKeyword(element);
				Write(" < ");
				VisitLiteralLong(array.Length);
				Write("; ");
				WriteCamelCaseNotKeyword(element);
				Write("++)");
				WriteChild(statement.Body);
				break;
			case CiClassType klass:
				switch (klass.Class.Id) {
				case CiId.StringClass:
					Write("for (");
					WriteStringPtrType();
					WriteCamelCaseNotKeyword(element);
					Write(" = ");
					statement.Collection.Accept(this, CiPriority.Argument);
					Write("; *");
					WriteCamelCaseNotKeyword(element);
					Write(" != '\\0'; ");
					WriteCamelCaseNotKeyword(element);
					Write("++)");
					WriteChild(statement.Body);
					break;
				case CiId.ListClass:
					Write("for (");
					CiType elementType = klass.GetElementType();
					WriteType(elementType, false);
					Write(" const *");
					WriteCamelCaseNotKeyword(element);
					Write(" = (");
					WriteType(elementType, false);
					Write(" const *) ");
					WritePostfix(statement.Collection, "->data, ");
					for (; elementType.IsArray(); elementType = elementType.AsClassType().GetElementType())
						WriteChar('*');
					if (elementType is CiClassType)
						Write("* const ");
					Write("*ciend = ");
					WriteCamelCaseNotKeyword(element);
					Write(" + ");
					WritePostfix(statement.Collection, "->len; ");
					WriteCamelCaseNotKeyword(element);
					Write(" < ciend; ");
					WriteCamelCaseNotKeyword(element);
					Write("++)");
					WriteChild(statement.Body);
					break;
				case CiId.HashSetClass:
					StartForeachHashTable(statement);
					WriteLine("gpointer cikey;");
					Write("while (g_hash_table_iter_next(&cidictit, &cikey, NULL)) ");
					OpenBlock();
					WriteDictIterVar(statement.GetVar(), "cikey");
					FlattenBlock(statement.Body);
					CloseBlock();
					CloseBlock();
					break;
				case CiId.SortedSetClass:
					Write("for (GTreeNode *cisetit = g_tree_node_first(");
					statement.Collection.Accept(this, CiPriority.Argument);
					Write("); cisetit != NULL; cisetit = g_tree_node_next(cisetit)) ");
					OpenBlock();
					WriteDictIterVar(statement.GetVar(), "g_tree_node_key(cisetit)");
					FlattenBlock(statement.Body);
					CloseBlock();
					break;
				case CiId.DictionaryClass:
					StartForeachHashTable(statement);
					WriteLine("gpointer cikey, civalue;");
					Write("while (g_hash_table_iter_next(&cidictit, &cikey, &civalue)) ");
					OpenBlock();
					WriteDictIterVar(statement.GetVar(), "cikey");
					WriteDictIterVar(statement.GetValueVar(), "civalue");
					FlattenBlock(statement.Body);
					CloseBlock();
					CloseBlock();
					break;
				case CiId.SortedDictionaryClass:
					Write("for (GTreeNode *cidictit = g_tree_node_first(");
					statement.Collection.Accept(this, CiPriority.Argument);
					Write("); cidictit != NULL; cidictit = g_tree_node_next(cidictit)) ");
					OpenBlock();
					WriteDictIterVar(statement.GetVar(), "g_tree_node_key(cidictit)");
					WriteDictIterVar(statement.GetValueVar(), "g_tree_node_value(cidictit)");
					FlattenBlock(statement.Body);
					CloseBlock();
					break;
				default:
					NotSupported(statement.Collection, klass.Class.Name);
					break;
				}
				break;
			default:
				NotSupported(statement.Collection, statement.Collection.Type.ToString());
				break;
			}
		}

		public override void VisitLock(CiLock statement)
		{
			Write("mtx_lock(&");
			statement.Lock.Accept(this, CiPriority.Primary);
			WriteLine(");");
			statement.Body.AcceptStatement(this);
			Write("mtx_unlock(&");
			statement.Lock.Accept(this, CiPriority.Primary);
			WriteLine(");");
		}

		public override void VisitReturn(CiReturn statement)
		{
			if (statement.Value == null) {
				WriteDestructAll();
				WriteLine(this.CurrentMethod.Throws ? "return true;" : "return;");
			}
			else if (statement.Value is CiLiteral || (this.VarsToDestruct.Count == 0 && !HasTemporariesToDestruct(statement.Value))) {
				WriteDestructAll();
				WriteCTemporaries(statement.Value);
				base.VisitReturn(statement);
			}
			else {
				if (statement.Value is CiSymbolReference symbol && symbol.Symbol is CiVar local) {
					if (this.VarsToDestruct.Contains(local)) {
						WriteDestructAll(local);
						Write("return ");
						if (this.CurrentMethod.Type is CiClassType resultPtr)
							WriteClassPtr(resultPtr.Class, symbol, CiPriority.Argument);
						else
							symbol.Accept(this, CiPriority.Argument);
						WriteCharLine(';');
						return;
					}
					WriteDestructAll();
					base.VisitReturn(statement);
					return;
				}
				WriteCTemporaries(statement.Value);
				EnsureChildBlock();
				StartDefinition(this.CurrentMethod.Type, true, true);
				Write("returnValue = ");
				WriteCoerced(this.CurrentMethod.Type, statement.Value, CiPriority.Argument);
				WriteCharLine(';');
				CleanupTemporaries();
				WriteDestructAll();
				WriteLine("return returnValue;");
			}
		}

		protected override void WriteSwitchCaseBody(List<CiStatement> statements)
		{
			if (statements[0] is CiVar || (statements[0] is CiConst konst && konst.Type is CiArrayStorageType))
				WriteCharLine(';');
			int varsToDestructCount = this.VarsToDestruct.Count;
			WriteStatements(statements);
			TrimVarsToDestruct(varsToDestructCount);
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			if (statement.IsTypeMatching())
				NotSupported(statement, "Type-matching 'switch'");
			else
				base.VisitSwitch(statement);
		}

		public override void VisitThrow(CiThrow statement)
		{
			WriteThrow();
		}

		bool TryWriteCallAndReturn(List<CiStatement> statements, int lastCallIndex, CiExpr returnValue)
		{
			if (this.VarsToDestruct.Count > 0)
				return false;
			for (int i = 0; i < lastCallIndex; i++) {
				if (statements[i] is CiVar def && NeedToDestruct(def))
					return false;
			}
			if (!(statements[lastCallIndex] is CiExpr call))
				return false;
			CiMethod throwingMethod = GetThrowingMethod(call);
			if (throwingMethod == null)
				return false;
			WriteFirstStatements(statements, lastCallIndex);
			Write("return ");
			if (throwingMethod.Type is CiNumericType) {
				if (throwingMethod.Type is CiIntegerType) {
					call.Accept(this, CiPriority.Equality);
					Write(" != -1");
				}
				else {
					IncludeMath();
					Write("!isnan(");
					call.Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
			}
			else if (throwingMethod.Type.Id == CiId.VoidType)
				call.Accept(this, CiPriority.Select);
			else {
				call.Accept(this, CiPriority.Equality);
				Write(" != NULL");
			}
			if (returnValue != null) {
				Write(" ? ");
				returnValue.Accept(this, CiPriority.Select);
				Write(" : ");
				WriteThrowReturnValue();
			}
			WriteCharLine(';');
			return true;
		}

		protected override void WriteStatements(List<CiStatement> statements)
		{
			int i = statements.Count - 2;
			if (i >= 0 && statements[i + 1] is CiReturn ret && TryWriteCallAndReturn(statements, i, ret.Value))
				return;
			base.WriteStatements(statements);
		}

		protected override void WriteEnum(CiEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			Write("typedef enum ");
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			this.Indent--;
			Write("} ");
			WriteName(enu);
			WriteCharLine(';');
		}

		void WriteTypedef(CiClass klass)
		{
			if (klass.CallType == CiCallType.Static)
				return;
			Write("typedef struct ");
			WriteName(klass);
			WriteChar(' ');
			WriteName(klass);
			WriteCharLine(';');
		}

		protected void WriteTypedefs(CiProgram program, bool pub)
		{
			for (CiSymbol type = program.First; type != null; type = type.Next) {
				switch (type) {
				case CiClass klass:
					if (klass.IsPublic == pub)
						WriteTypedef(klass);
					break;
				case CiEnum enu:
					if (enu.IsPublic == pub)
						WriteEnum(enu);
					break;
				default:
					throw new NotImplementedException();
				}
			}
		}

		void WriteInstanceParameters(CiMethod method)
		{
			WriteChar('(');
			if (!method.IsMutator)
				Write("const ");
			WriteName(method.Parent);
			Write(" *self");
			WriteRemainingParameters(method, false, false);
		}

		void WriteSignature(CiMethod method)
		{
			CiClass klass = (CiClass) method.Parent;
			if (!klass.IsPublic || method.Visibility != CiVisibility.Public)
				Write("static ");
			WriteReturnType(method);
			WriteName(klass);
			WriteChar('_');
			Write(method.Name);
			if (method.CallType != CiCallType.Static)
				WriteInstanceParameters(method);
			else if (method.Parameters.Count() == 0)
				Write("(void)");
			else
				WriteParameters(method, false);
		}

		void WriteVtblFields(CiClass klass)
		{
			if (klass.Parent is CiClass baseClass)
				WriteVtblFields(baseClass);
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiMethod method && method.IsAbstractOrVirtual()) {
					WriteReturnType(method);
					Write("(*");
					WriteCamelCase(method.Name);
					WriteChar(')');
					WriteInstanceParameters(method);
					WriteCharLine(';');
				}
			}
		}

		void WriteVtblStruct(CiClass klass)
		{
			Write("typedef struct ");
			OpenBlock();
			WriteVtblFields(klass);
			this.Indent--;
			Write("} ");
			WriteName(klass);
			WriteLine("Vtbl;");
		}

		protected virtual string GetConst(CiArrayStorageType array) => "const ";

		protected override void WriteConst(CiConst konst)
		{
			if (konst.Type is CiArrayStorageType array) {
				Write("static ");
				Write(GetConst(array));
				WriteTypeAndName(konst);
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Argument);
				WriteCharLine(';');
			}
			else if (konst.Visibility == CiVisibility.Public) {
				Write("#define ");
				WriteName(konst);
				WriteChar(' ');
				konst.Value.Accept(this, CiPriority.Argument);
				WriteNewLine();
			}
		}

		protected override void WriteField(CiField field)
		{
			throw new NotImplementedException();
		}

		static bool HasVtblValue(CiClass klass)
		{
			if (klass.CallType == CiCallType.Static || klass.CallType == CiCallType.Abstract)
				return false;
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiMethod method) {
					switch (method.CallType) {
					case CiCallType.Virtual:
					case CiCallType.Override:
					case CiCallType.Sealed:
						return true;
					default:
						break;
					}
				}
			}
			return false;
		}

		protected override bool NeedsConstructor(CiClass klass)
		{
			if (klass.Id == CiId.MatchClass)
				return false;
			return base.NeedsConstructor(klass) || HasVtblValue(klass) || (klass.Parent is CiClass baseClass && NeedsConstructor(baseClass));
		}

		void WriteXstructorSignature(string name, CiClass klass)
		{
			Write("static void ");
			WriteName(klass);
			WriteChar('_');
			Write(name);
			WriteChar('(');
			WriteName(klass);
			Write(" *self)");
		}

		protected void WriteSignatures(CiClass klass, bool pub)
		{
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				switch (symbol) {
				case CiConst konst when (konst.Visibility == CiVisibility.Public) == pub:
					if (pub) {
						WriteNewLine();
						WriteDoc(konst.Documentation);
					}
					WriteConst(konst);
					break;
				case CiMethod method when method.IsLive && (method.Visibility == CiVisibility.Public) == pub && method.CallType != CiCallType.Abstract:
					WriteNewLine();
					WriteMethodDoc(method);
					WriteSignature(method);
					WriteCharLine(';');
					break;
				default:
					break;
				}
			}
		}

		protected override void WriteClassInternal(CiClass klass)
		{
			if (klass.CallType != CiCallType.Static) {
				WriteNewLine();
				if (klass.AddsVirtualMethods())
					WriteVtblStruct(klass);
				WriteDoc(klass.Documentation);
				Write("struct ");
				WriteName(klass);
				WriteChar(' ');
				OpenBlock();
				if (GetVtblPtrClass(klass) == klass) {
					Write("const ");
					WriteName(klass);
					WriteLine("Vtbl *vtbl;");
				}
				if (klass.Parent is CiClass) {
					WriteName(klass.Parent);
					WriteLine(" base;");
				}
				for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
					if (symbol is CiField field) {
						WriteDoc(field.Documentation);
						WriteTypeAndName(field);
						WriteCharLine(';');
					}
				}
				this.Indent--;
				WriteLine("};");
				if (NeedsConstructor(klass)) {
					WriteXstructorSignature("Construct", klass);
					WriteCharLine(';');
				}
				if (NeedsDestructor(klass)) {
					WriteXstructorSignature("Destruct", klass);
					WriteCharLine(';');
				}
			}
			WriteSignatures(klass, false);
		}

		void WriteVtbl(CiClass definingClass, CiClass declaringClass)
		{
			if (declaringClass.Parent is CiClass baseClass)
				WriteVtbl(definingClass, baseClass);
			for (CiSymbol symbol = declaringClass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiMethod declaredMethod && declaredMethod.IsAbstractOrVirtual()) {
					CiSymbol definedMethod = definingClass.TryLookup(declaredMethod.Name, false);
					if (declaredMethod != definedMethod) {
						WriteChar('(');
						WriteReturnType(declaredMethod);
						Write("(*)");
						WriteInstanceParameters(declaredMethod);
						Write(") ");
					}
					WriteName(definedMethod);
					WriteCharLine(',');
				}
			}
		}

		protected void WriteConstructor(CiClass klass)
		{
			if (!NeedsConstructor(klass))
				return;
			this.SwitchesWithGoto.Clear();
			WriteNewLine();
			WriteXstructorSignature("Construct", klass);
			WriteNewLine();
			OpenBlock();
			if (klass.Parent is CiClass baseClass && NeedsConstructor(baseClass)) {
				WriteName(baseClass);
				WriteLine("_Construct(&self->base);");
			}
			if (HasVtblValue(klass)) {
				CiClass structClass = GetVtblStructClass(klass);
				Write("static const ");
				WriteName(structClass);
				Write("Vtbl vtbl = ");
				OpenBlock();
				WriteVtbl(klass, structClass);
				this.Indent--;
				WriteLine("};");
				CiClass ptrClass = GetVtblPtrClass(klass);
				WriteSelfForField(ptrClass);
				Write("vtbl = ");
				if (ptrClass != structClass) {
					Write("(const ");
					WriteName(ptrClass);
					Write("Vtbl *) ");
				}
				WriteLine("&vtbl;");
			}
			WriteConstructorBody(klass);
			CloseBlock();
		}

		void WriteDestructFields(CiSymbol symbol)
		{
			if (symbol != null) {
				WriteDestructFields(symbol.Next);
				if (symbol is CiField field)
					WriteDestruct(field);
			}
		}

		protected void WriteDestructor(CiClass klass)
		{
			if (!NeedsDestructor(klass))
				return;
			WriteNewLine();
			WriteXstructorSignature("Destruct", klass);
			WriteNewLine();
			OpenBlock();
			WriteDestructFields(klass.First);
			if (klass.Parent is CiClass baseClass && NeedsDestructor(baseClass)) {
				WriteName(baseClass);
				WriteLine("_Destruct(&self->base);");
			}
			CloseBlock();
		}

		void WriteNewDelete(CiClass klass, bool define)
		{
			if (!klass.IsPublic || klass.Constructor == null || klass.Constructor.Visibility != CiVisibility.Public)
				return;
			WriteNewLine();
			WriteName(klass);
			Write(" *");
			WriteName(klass);
			Write("_New(void)");
			if (define) {
				WriteNewLine();
				OpenBlock();
				WriteName(klass);
				Write(" *self = (");
				WriteName(klass);
				Write(" *) malloc(sizeof(");
				WriteName(klass);
				WriteLine("));");
				if (NeedsConstructor(klass)) {
					WriteLine("if (self != NULL)");
					this.Indent++;
					WriteName(klass);
					WriteLine("_Construct(self);");
					this.Indent--;
				}
				WriteLine("return self;");
				CloseBlock();
				WriteNewLine();
			}
			else
				WriteCharLine(';');
			Write("void ");
			WriteName(klass);
			Write("_Delete(");
			WriteName(klass);
			Write(" *self)");
			if (define) {
				WriteNewLine();
				OpenBlock();
				if (NeedsDestructor(klass)) {
					WriteLine("if (self == NULL)");
					this.Indent++;
					WriteLine("return;");
					this.Indent--;
					WriteName(klass);
					WriteLine("_Destruct(self);");
				}
				WriteLine("free(self);");
				CloseBlock();
			}
			else
				WriteCharLine(';');
		}

		protected override void WriteMethod(CiMethod method)
		{
			if (!method.IsLive || method.CallType == CiCallType.Abstract)
				return;
			this.SwitchesWithGoto.Clear();
			WriteNewLine();
			WriteSignature(method);
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (NeedToDestruct(param))
					this.VarsToDestruct.Add(param);
			}
			WriteNewLine();
			this.CurrentMethod = method;
			OpenBlock();
			if (method.Body is CiBlock block) {
				List<CiStatement> statements = block.Statements;
				if (!block.CompletesNormally())
					WriteStatements(statements);
				else if (method.Throws && method.Type.Id == CiId.VoidType) {
					if (statements.Count == 0 || !TryWriteCallAndReturn(statements, statements.Count - 1, null)) {
						WriteStatements(statements);
						WriteDestructAll();
						WriteLine("return true;");
					}
				}
				else {
					WriteStatements(statements);
					WriteDestructAll();
				}
			}
			else
				method.Body.AcceptStatement(this);
			this.VarsToDestruct.Clear();
			CloseBlock();
			this.CurrentMethod = null;
		}

		void WriteTryParseLibrary(string signature, string call)
		{
			WriteNewLine();
			Write("static bool Ci");
			WriteLine(signature);
			OpenBlock();
			WriteLine("if (*str == '\\0')");
			WriteLine("\treturn false;");
			WriteLine("char *end;");
			Write("*result = strto");
			Write(call);
			WriteLine(");");
			WriteLine("return *end == '\\0';");
			CloseBlock();
		}

		void WriteLibrary()
		{
			if (this.IntTryParse)
				WriteTryParseLibrary("Int_TryParse(int *result, const char *str, int base)", "l(str, &end, base");
			if (this.LongTryParse)
				WriteTryParseLibrary("Long_TryParse(int64_t *result, const char *str, int base)", "ll(str, &end, base");
			if (this.DoubleTryParse)
				WriteTryParseLibrary("Double_TryParse(double *result, const char *str)", "d(str, &end");
			if (this.StringAssign) {
				WriteNewLine();
				WriteLine("static void CiString_Assign(char **str, char *value)");
				OpenBlock();
				WriteLine("free(*str);");
				WriteLine("*str = value;");
				CloseBlock();
			}
			if (this.StringSubstring) {
				WriteNewLine();
				WriteLine("static char *CiString_Substring(const char *str, int len)");
				OpenBlock();
				WriteLine("char *p = malloc(len + 1);");
				WriteLine("memcpy(p, str, len);");
				WriteLine("p[len] = '\\0';");
				WriteLine("return p;");
				CloseBlock();
			}
			if (this.StringAppend) {
				WriteNewLine();
				WriteLine("static void CiString_AppendSubstring(char **str, const char *suffix, size_t suffixLen)");
				OpenBlock();
				WriteLine("if (suffixLen == 0)");
				WriteLine("\treturn;");
				WriteLine("size_t prefixLen = *str == NULL ? 0 : strlen(*str);");
				WriteLine("*str = realloc(*str, prefixLen + suffixLen + 1);");
				WriteLine("memcpy(*str + prefixLen, suffix, suffixLen);");
				WriteLine("(*str)[prefixLen + suffixLen] = '\\0';");
				CloseBlock();
				WriteNewLine();
				WriteLine("static void CiString_Append(char **str, const char *suffix)");
				OpenBlock();
				WriteLine("CiString_AppendSubstring(str, suffix, strlen(suffix));");
				CloseBlock();
			}
			if (this.StringIndexOf) {
				WriteNewLine();
				WriteLine("static int CiString_IndexOf(const char *str, const char *needle)");
				OpenBlock();
				WriteLine("const char *p = strstr(str, needle);");
				WriteLine("return p == NULL ? -1 : (int) (p - str);");
				CloseBlock();
			}
			if (this.StringLastIndexOf) {
				WriteNewLine();
				WriteLine("static int CiString_LastIndexOf(const char *str, const char *needle)");
				OpenBlock();
				WriteLine("if (needle[0] == '\\0')");
				WriteLine("\treturn (int) strlen(str);");
				WriteLine("int result = -1;");
				WriteLine("const char *p = strstr(str, needle);");
				Write("while (p != NULL) ");
				OpenBlock();
				WriteLine("result = (int) (p - str);");
				WriteLine("p = strstr(p + 1, needle);");
				CloseBlock();
				WriteLine("return result;");
				CloseBlock();
			}
			if (this.StringEndsWith) {
				WriteNewLine();
				WriteLine("static bool CiString_EndsWith(const char *str, const char *suffix)");
				OpenBlock();
				WriteLine("size_t strLen = strlen(str);");
				WriteLine("size_t suffixLen = strlen(suffix);");
				WriteLine("return strLen >= suffixLen && memcmp(str + strLen - suffixLen, suffix, suffixLen) == 0;");
				CloseBlock();
			}
			if (this.StringReplace) {
				WriteNewLine();
				WriteLine("static char *CiString_Replace(const char *s, const char *oldValue, const char *newValue)");
				OpenBlock();
				Write("for (char *result = NULL;;) ");
				OpenBlock();
				WriteLine("const char *p = strstr(s, oldValue);");
				WriteLine("if (p == NULL) {");
				WriteLine("\tCiString_Append(&result, s);");
				WriteLine("\treturn result == NULL ? strdup(\"\") : result;");
				WriteCharLine('}');
				WriteLine("CiString_AppendSubstring(&result, s, p - s);");
				WriteLine("CiString_Append(&result, newValue);");
				WriteLine("s = p + strlen(oldValue);");
				CloseBlock();
				CloseBlock();
			}
			if (this.StringFormat) {
				WriteNewLine();
				WriteLine("static char *CiString_Format(const char *format, ...)");
				OpenBlock();
				WriteLine("va_list args1;");
				WriteLine("va_start(args1, format);");
				WriteLine("va_list args2;");
				WriteLine("va_copy(args2, args1);");
				WriteLine("size_t len = vsnprintf(NULL, 0, format, args1) + 1;");
				WriteLine("va_end(args1);");
				WriteLine("char *str = malloc(len);");
				WriteLine("vsnprintf(str, len, format, args2);");
				WriteLine("va_end(args2);");
				WriteLine("return str;");
				CloseBlock();
			}
			if (this.MatchFind) {
				WriteNewLine();
				WriteLine("static bool CiMatch_Find(GMatchInfo **match_info, const char *input, const char *pattern, GRegexCompileFlags options)");
				OpenBlock();
				WriteLine("GRegex *regex = g_regex_new(pattern, options, 0, NULL);");
				WriteLine("bool result = g_regex_match(regex, input, 0, match_info);");
				WriteLine("g_regex_unref(regex);");
				WriteLine("return result;");
				CloseBlock();
			}
			if (this.MatchPos) {
				WriteNewLine();
				WriteLine("static int CiMatch_GetPos(const GMatchInfo *match_info, int which)");
				OpenBlock();
				WriteLine("int start;");
				WriteLine("int end;");
				WriteLine("g_match_info_fetch_pos(match_info, 0, &start, &end);");
				WriteLine("switch (which) {");
				WriteLine("case 0:");
				WriteLine("\treturn start;");
				WriteLine("case 1:");
				WriteLine("\treturn end;");
				WriteLine("default:");
				WriteLine("\treturn end - start;");
				WriteCharLine('}');
				CloseBlock();
			}
			if (this.PtrConstruct) {
				WriteNewLine();
				WriteLine("static void CiPtr_Construct(void **ptr)");
				OpenBlock();
				WriteLine("*ptr = NULL;");
				CloseBlock();
			}
			if (this.SharedMake || this.SharedAddRef || this.SharedRelease) {
				WriteNewLine();
				WriteLine("typedef void (*CiMethodPtr)(void *);");
				WriteLine("typedef struct {");
				this.Indent++;
				WriteLine("size_t count;");
				WriteLine("size_t unitSize;");
				WriteLine("size_t refCount;");
				WriteLine("CiMethodPtr destructor;");
				this.Indent--;
				WriteLine("} CiShared;");
			}
			if (this.SharedMake) {
				WriteNewLine();
				WriteLine("static void *CiShared_Make(size_t count, size_t unitSize, CiMethodPtr constructor, CiMethodPtr destructor)");
				OpenBlock();
				WriteLine("CiShared *self = (CiShared *) malloc(sizeof(CiShared) + count * unitSize);");
				WriteLine("self->count = count;");
				WriteLine("self->unitSize = unitSize;");
				WriteLine("self->refCount = 1;");
				WriteLine("self->destructor = destructor;");
				Write("if (constructor != NULL) ");
				OpenBlock();
				WriteLine("for (size_t i = 0; i < count; i++)");
				WriteLine("\tconstructor((char *) (self + 1) + i * unitSize);");
				CloseBlock();
				WriteLine("return self + 1;");
				CloseBlock();
			}
			if (this.SharedAddRef) {
				WriteNewLine();
				WriteLine("static void *CiShared_AddRef(void *ptr)");
				OpenBlock();
				WriteLine("if (ptr != NULL)");
				WriteLine("\t((CiShared *) ptr)[-1].refCount++;");
				WriteLine("return ptr;");
				CloseBlock();
			}
			if (this.SharedRelease || this.SharedAssign) {
				WriteNewLine();
				WriteLine("static void CiShared_Release(void *ptr)");
				OpenBlock();
				WriteLine("if (ptr == NULL)");
				WriteLine("\treturn;");
				WriteLine("CiShared *self = (CiShared *) ptr - 1;");
				WriteLine("if (--self->refCount != 0)");
				WriteLine("\treturn;");
				Write("if (self->destructor != NULL) ");
				OpenBlock();
				WriteLine("for (size_t i = self->count; i > 0;)");
				WriteLine("\tself->destructor((char *) ptr + --i * self->unitSize);");
				CloseBlock();
				WriteLine("free(self);");
				CloseBlock();
			}
			if (this.SharedAssign) {
				WriteNewLine();
				WriteLine("static void CiShared_Assign(void **ptr, void *value)");
				OpenBlock();
				WriteLine("CiShared_Release(*ptr);");
				WriteLine("*ptr = value;");
				CloseBlock();
			}
			foreach ((string name, string content) in this.ListFrees) {
				WriteNewLine();
				Write("static void CiList_Free");
				Write(name);
				WriteLine("(void *ptr)");
				OpenBlock();
				Write(content);
				WriteCharLine(';');
				CloseBlock();
			}
			if (this.TreeCompareInteger) {
				WriteNewLine();
				Write("static int CiTree_CompareInteger(gconstpointer pa, gconstpointer pb, gpointer user_data)");
				OpenBlock();
				WriteLine("gintptr a = (gintptr) pa;");
				WriteLine("gintptr b = (gintptr) pb;");
				WriteLine("return (a > b) - (a < b);");
				CloseBlock();
			}
			if (this.TreeCompareString) {
				WriteNewLine();
				Write("static int CiTree_CompareString(gconstpointer a, gconstpointer b, gpointer user_data)");
				OpenBlock();
				WriteLine("return strcmp((const char *) a, (const char *) b);");
				CloseBlock();
			}
			foreach (CiId typeId in this.Compares) {
				WriteNewLine();
				Write("static int CiCompare_");
				WriteNumericType(typeId);
				WriteLine("(const void *pa, const void *pb)");
				OpenBlock();
				WriteNumericType(typeId);
				Write(" a = *(const ");
				WriteNumericType(typeId);
				WriteLine(" *) pa;");
				WriteNumericType(typeId);
				Write(" b = *(const ");
				WriteNumericType(typeId);
				WriteLine(" *) pb;");
				switch (typeId) {
				case CiId.ByteRange:
				case CiId.SByteRange:
				case CiId.ShortRange:
				case CiId.UShortRange:
					WriteLine("return a - b;");
					break;
				default:
					WriteLine("return (a > b) - (a < b);");
					break;
				}
				CloseBlock();
			}
			foreach (CiId typeId in this.Contains) {
				WriteNewLine();
				Write("static bool CiArray_Contains_");
				if (typeId == CiId.StringPtrType)
					Write("string(const char * const *a, size_t len, const char *");
				else {
					WriteNumericType(typeId);
					Write("(const ");
					WriteNumericType(typeId);
					Write(" *a, size_t len, ");
					WriteNumericType(typeId);
				}
				WriteLine(" value)");
				OpenBlock();
				WriteLine("for (size_t i = 0; i < len; i++)");
				if (typeId == CiId.StringPtrType)
					WriteLine("\tif (strcmp(a[i], value) == 0)");
				else
					WriteLine("\tif (a[i] == value)");
				WriteLine("\t\treturn true;");
				WriteLine("return false;");
				CloseBlock();
			}
		}

		protected void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			if (resources.Count == 0)
				return;
			WriteNewLine();
			foreach ((string name, List<byte> content) in resources) {
				Write("static const ");
				WriteNumericType(CiId.ByteRange);
				WriteChar(' ');
				WriteResource(name, -1);
				WriteChar('[');
				VisitLiteralLong(content.Count);
				WriteLine("] = {");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" };");
			}
		}

		public override void WriteProgram(CiProgram program)
		{
			this.WrittenClasses.Clear();
			this.InHeaderFile = true;
			OpenStringWriter();
			foreach (CiClass klass in program.Classes) {
				WriteNewDelete(klass, false);
				WriteSignatures(klass, true);
			}
			CreateHeaderFile(".h");
			WriteLine("#ifdef __cplusplus");
			WriteLine("extern \"C\" {");
			WriteLine("#endif");
			WriteTypedefs(program, true);
			CloseStringWriter();
			WriteNewLine();
			WriteLine("#ifdef __cplusplus");
			WriteCharLine('}');
			WriteLine("#endif");
			CloseFile();
			this.InHeaderFile = false;
			this.IntTryParse = false;
			this.LongTryParse = false;
			this.DoubleTryParse = false;
			this.StringAssign = false;
			this.StringSubstring = false;
			this.StringAppend = false;
			this.StringIndexOf = false;
			this.StringLastIndexOf = false;
			this.StringEndsWith = false;
			this.StringReplace = false;
			this.StringFormat = false;
			this.MatchFind = false;
			this.MatchPos = false;
			this.PtrConstruct = false;
			this.SharedMake = false;
			this.SharedAddRef = false;
			this.SharedRelease = false;
			this.SharedAssign = false;
			this.ListFrees.Clear();
			this.TreeCompareInteger = false;
			this.TreeCompareString = false;
			this.Compares.Clear();
			this.Contains.Clear();
			OpenStringWriter();
			foreach (CiClass klass in program.Classes)
				WriteClass(klass, program);
			WriteResources(program.Resources);
			foreach (CiClass klass in program.Classes) {
				this.CurrentClass = klass;
				WriteConstructor(klass);
				WriteDestructor(klass);
				WriteNewDelete(klass, true);
				WriteMethods(klass);
			}
			Include("stdlib.h");
			CreateImplementationFile(program, ".h");
			WriteLibrary();
			WriteRegexOptionsEnum(program);
			WriteTypedefs(program, false);
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenCl : GenC
	{

		bool StringLength;

		bool StringEquals;

		bool StringStartsWith;

		bool BytesEqualsString;

		protected override string GetTargetName() => "OpenCL C";

		protected override void IncludeStdBool()
		{
		}

		protected override void IncludeMath()
		{
		}

		protected override void WriteNumericType(CiId id)
		{
			switch (id) {
			case CiId.SByteRange:
				Write("char");
				break;
			case CiId.ByteRange:
				Write("uchar");
				break;
			case CiId.ShortRange:
				Write("short");
				break;
			case CiId.UShortRange:
				Write("ushort");
				break;
			case CiId.IntType:
				Write("int");
				break;
			case CiId.LongType:
				Write("long");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteStringPtrType()
		{
			Write("constant char *");
		}

		protected override void WriteClassType(CiClassType klass, bool space)
		{
			switch (klass.Class.Id) {
			case CiId.None:
				if (klass is CiDynamicPtrType)
					NotSupported(klass, "Dynamic reference");
				else
					base.WriteClassType(klass, space);
				break;
			case CiId.StringClass:
				if (klass.Id == CiId.StringStorageType)
					NotSupported(klass, "string()");
				else
					WriteStringPtrType();
				break;
			default:
				NotSupported(klass, klass.Class.Name);
				break;
			}
		}

		protected override void WritePrintfLongPrefix()
		{
			WriteChar('l');
		}

		protected override void WriteInterpolatedStringArgBase(CiExpr expr)
		{
			expr.Accept(this, CiPriority.Argument);
		}

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			NotSupported(expr, "Interpolated strings");
		}

		protected override void WriteCamelCaseNotKeyword(string name)
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
				WriteCamelCase(name);
				WriteChar('_');
				break;
			default:
				base.WriteCamelCaseNotKeyword(name);
				break;
			}
		}

		protected override string GetConst(CiArrayStorageType array) => array.PtrTaken ? "const " : "constant ";

		protected override void WriteSubstringEqual(CiCallExpr call, string literal, CiPriority parent, bool not)
		{
			if (not)
				WriteChar('!');
			if (IsUTF8GetString(call)) {
				this.BytesEqualsString = true;
				Write("CiBytes_Equals(");
			}
			else {
				this.StringStartsWith = true;
				Write("CiString_StartsWith(");
			}
			WriteStringPtrAdd(call);
			Write(", ");
			VisitLiteralString(literal);
			WriteChar(')');
		}

		protected override void WriteEqualStringInternal(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			this.StringEquals = true;
			if (not)
				WriteChar('!');
			WriteCall("CiString_Equals", left, right);
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			this.StringLength = true;
			WriteCall("strlen", expr);
		}

		void WriteConsoleWrite(List<CiExpr> args, bool newLine)
		{
			Write("printf(");
			if (args.Count == 0)
				Write("\"\\n\")");
			else if (args[0] is CiInterpolatedString interpolated)
				WritePrintf(interpolated, newLine);
			else
				WritePrintfNotInterpolated(args, newLine);
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.None:
			case CiId.ClassToString:
				WriteCCall(obj, method, args);
				break;
			case CiId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case CiId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case CiId.StringStartsWith:
				int c = GetOneAscii(args[0]);
				if (c >= 0) {
					if (parent > CiPriority.Equality)
						WriteChar('(');
					WritePostfix(obj, "[0] == ");
					VisitLiteralChar(c);
					if (parent > CiPriority.Equality)
						WriteChar(')');
				}
				else {
					this.StringStartsWith = true;
					WriteCall("CiString_StartsWith", obj, args[0]);
				}
				break;
			case CiId.StringSubstring:
				Debug.Assert(args.Count == 1);
				if (parent > CiPriority.Add)
					WriteChar('(');
				WriteAdd(obj, args[0]);
				if (parent > CiPriority.Add)
					WriteChar(')');
				break;
			case CiId.ArrayCopyTo:
				Write("for (size_t _i = 0; _i < ");
				args[3].Accept(this, CiPriority.Rel);
				WriteLine("; _i++)");
				WriteChar('\t');
				args[1].Accept(this, CiPriority.Primary);
				WriteChar('[');
				StartAdd(args[2]);
				Write("_i] = ");
				obj.Accept(this, CiPriority.Primary);
				WriteChar('[');
				StartAdd(args[0]);
				Write("_i]");
				break;
			case CiId.ArrayFillAll:
			case CiId.ArrayFillPart:
				WriteArrayFill(obj, args);
				break;
			case CiId.ConsoleWrite:
				WriteConsoleWrite(args, false);
				break;
			case CiId.ConsoleWriteLine:
				WriteConsoleWrite(args, true);
				break;
			case CiId.UTF8GetByteCount:
				WriteStringLength(args[0]);
				break;
			case CiId.UTF8GetBytes:
				Write("for (size_t _i = 0; ");
				args[0].Accept(this, CiPriority.Primary);
				WriteLine("[_i] != '\\0'; _i++)");
				WriteChar('\t');
				args[1].Accept(this, CiPriority.Primary);
				WriteChar('[');
				StartAdd(args[2]);
				Write("_i] = ");
				WritePostfix(args[0], "[_i]");
				break;
			case CiId.MathMethod:
			case CiId.MathClamp:
			case CiId.MathIsFinite:
			case CiId.MathIsNaN:
			case CiId.MathLog2:
			case CiId.MathMaxInt:
			case CiId.MathMinInt:
			case CiId.MathRound:
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathAbs:
				if (args[0].Type is CiFloatingType)
					WriteChar('f');
				WriteCall("abs", args[0]);
				break;
			case CiId.MathCeiling:
				WriteCall("ceil", args[0]);
				break;
			case CiId.MathFusedMultiplyAdd:
				WriteCall("fma", args[0], args[1], args[2]);
				break;
			case CiId.MathIsInfinity:
				WriteCall("isinf", args[0]);
				break;
			case CiId.MathMaxDouble:
				WriteCall("fmax", args[0], args[1]);
				break;
			case CiId.MathMinDouble:
				WriteCall("fmin", args[0], args[1]);
				break;
			case CiId.MathTruncate:
				WriteCall("trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteAssert(CiAssert statement)
		{
		}

		protected override void WriteSwitchCaseBody(List<CiStatement> statements)
		{
			if (statements.All(statement => statement is CiAssert))
				WriteCharLine(';');
			else
				base.WriteSwitchCaseBody(statements);
		}

		void WriteLibrary()
		{
			if (this.StringLength) {
				WriteNewLine();
				WriteLine("static int strlen(constant char *str)");
				OpenBlock();
				WriteLine("int len = 0;");
				WriteLine("while (str[len] != '\\0')");
				WriteLine("\tlen++;");
				WriteLine("return len;");
				CloseBlock();
			}
			if (this.StringEquals) {
				WriteNewLine();
				WriteLine("static bool CiString_Equals(constant char *str1, constant char *str2)");
				OpenBlock();
				WriteLine("for (size_t i = 0; str1[i] == str2[i]; i++) {");
				WriteLine("\tif (str1[i] == '\\0')");
				WriteLine("\t\treturn true;");
				WriteCharLine('}');
				WriteLine("return false;");
				CloseBlock();
			}
			if (this.StringStartsWith) {
				WriteNewLine();
				WriteLine("static bool CiString_StartsWith(constant char *str1, constant char *str2)");
				OpenBlock();
				WriteLine("for (int i = 0; str2[i] != '\\0'; i++) {");
				WriteLine("\tif (str1[i] != str2[i])");
				WriteLine("\t\treturn false;");
				WriteCharLine('}');
				WriteLine("return true;");
				CloseBlock();
			}
			if (this.BytesEqualsString) {
				WriteNewLine();
				WriteLine("static bool CiBytes_Equals(const uchar *mem, constant char *str)");
				OpenBlock();
				WriteLine("for (int i = 0; str[i] != '\\0'; i++) {");
				WriteLine("\tif (mem[i] != str[i])");
				WriteLine("\t\treturn false;");
				WriteCharLine('}');
				WriteLine("return true;");
				CloseBlock();
			}
		}

		public override void WriteProgram(CiProgram program)
		{
			this.WrittenClasses.Clear();
			this.StringLength = false;
			this.StringEquals = false;
			this.StringStartsWith = false;
			this.BytesEqualsString = false;
			OpenStringWriter();
			foreach (CiClass klass in program.Classes) {
				this.CurrentClass = klass;
				WriteConstructor(klass);
				WriteDestructor(klass);
				WriteMethods(klass);
			}
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteRegexOptionsEnum(program);
			WriteTypedefs(program, true);
			foreach (CiClass klass in program.Classes)
				WriteSignatures(klass, true);
			WriteTypedefs(program, false);
			foreach (CiClass klass in program.Classes)
				WriteClass(klass, program);
			WriteResources(program.Resources);
			WriteLibrary();
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenCpp : GenCCpp
	{

		bool UsingStringViewLiterals;

		bool HasEnumFlags;

		bool StringReplace;

		protected override string GetTargetName() => "C++";

		protected override void IncludeStdInt()
		{
			Include("cstdint");
		}

		protected override void IncludeAssert()
		{
			Include("cassert");
		}

		protected override void IncludeMath()
		{
			Include("cmath");
		}

		public override void VisitLiteralNull()
		{
			Write("nullptr");
		}

		void StartMethodCall(CiExpr obj)
		{
			obj.Accept(this, CiPriority.Primary);
			WriteMemberOp(obj, null);
		}

		protected override void WriteInterpolatedStringArg(CiExpr expr)
		{
			if (expr.Type is CiClassType klass && klass.Class.Id != CiId.StringClass) {
				StartMethodCall(expr);
				Write("toString()");
			}
			else
				base.WriteInterpolatedStringArg(expr);
		}

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			Include("format");
			Write("std::format(\"");
			foreach (CiInterpolatedPart part in expr.Parts) {
				WriteDoubling(part.Prefix, '{');
				WriteChar('{');
				WritePyFormat(part);
			}
			WriteDoubling(expr.Suffix, '{');
			WriteChar('"');
			WriteInterpolatedStringArgs(expr);
			WriteChar(')');
		}

		void WriteCamelCaseNotKeyword(string name)
		{
			WriteCamelCase(name);
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
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override void WriteName(CiSymbol symbol)
		{
			switch (symbol) {
			case CiContainerType _:
				Write(symbol.Name);
				break;
			case CiVar _:
			case CiMember _:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
		{
			if (symbol is CiField)
				Write("this->");
			WriteName(symbol);
		}

		void WriteCollectionType(string name, CiType elementType)
		{
			Include(name);
			Write("std::");
			Write(name);
			WriteChar('<');
			WriteType(elementType, false);
			WriteChar('>');
		}

		protected override void WriteType(CiType type, bool promote)
		{
			switch (type) {
			case CiIntegerType _:
				WriteNumericType(GetTypeId(type, promote));
				break;
			case CiDynamicPtrType dynamic:
				switch (dynamic.Class.Id) {
				case CiId.RegexClass:
					Include("regex");
					Write("std::regex");
					break;
				case CiId.ArrayPtrClass:
					Include("memory");
					Write("std::shared_ptr<");
					WriteType(dynamic.GetElementType(), false);
					Write("[]>");
					break;
				default:
					Include("memory");
					Write("std::shared_ptr<");
					Write(dynamic.Class.Name);
					WriteChar('>');
					break;
				}
				break;
			case CiClassType klass:
				if (klass.Class.TypeParameterCount == 0) {
					if (klass.Class.Id == CiId.StringClass) {
						string cppType = klass.Id == CiId.StringStorageType ? "string" : "string_view";
						Include(cppType);
						Write("std::");
						Write(cppType);
						break;
					}
					if (!(klass is CiReadWriteClassType))
						Write("const ");
					switch (klass.Class.Id) {
					case CiId.TextWriterClass:
						Include("iostream");
						Write("std::ostream");
						break;
					case CiId.StringWriterClass:
						Include("sstream");
						Write("std::ostringstream");
						break;
					case CiId.RegexClass:
						Include("regex");
						Write("std::regex");
						break;
					case CiId.MatchClass:
						Include("regex");
						Write("std::cmatch");
						break;
					case CiId.LockClass:
						Include("mutex");
						Write("std::recursive_mutex");
						break;
					default:
						Write(klass.Class.Name);
						break;
					}
				}
				else if (klass.Class.Id == CiId.ArrayPtrClass) {
					WriteType(klass.GetElementType(), false);
					if (!(klass is CiReadWriteClassType))
						Write(" const");
				}
				else {
					string cppType;
					switch (klass.Class.Id) {
					case CiId.ArrayStorageClass:
						cppType = "array";
						break;
					case CiId.ListClass:
						cppType = "vector";
						break;
					case CiId.QueueClass:
						cppType = "queue";
						break;
					case CiId.StackClass:
						cppType = "stack";
						break;
					case CiId.HashSetClass:
						cppType = "unordered_set";
						break;
					case CiId.SortedSetClass:
						cppType = "set";
						break;
					case CiId.DictionaryClass:
						cppType = "unordered_map";
						break;
					case CiId.SortedDictionaryClass:
						cppType = "map";
						break;
					default:
						NotSupported(type, klass.Class.Name);
						cppType = "NOT_SUPPORTED";
						break;
					}
					Include(cppType);
					if (!(klass is CiReadWriteClassType))
						Write("const ");
					Write("std::");
					Write(cppType);
					WriteChar('<');
					WriteType(klass.TypeArg0, false);
					if (klass is CiArrayStorageType arrayStorage) {
						Write(", ");
						VisitLiteralLong(arrayStorage.Length);
					}
					else if (klass.Class.TypeParameterCount == 2) {
						Write(", ");
						WriteType(klass.GetValueType(), false);
					}
					WriteChar('>');
				}
				if (!(klass is CiStorageType))
					Write(" *");
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
		{
			Include("memory");
			Write("std::make_shared<");
			WriteType(elementType, false);
			Write("[]>(");
			lengthExpr.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
		{
			Include("memory");
			Write("std::make_shared<");
			Write(klass.Class.Name);
			Write(">()");
		}

		protected override void WriteStorageInit(CiNamedValue def)
		{
		}

		protected override void WriteVarInit(CiNamedValue def)
		{
			if (def.Value != null && def.Type.Id == CiId.StringStorageType) {
				WriteChar('{');
				def.Value.Accept(this, CiPriority.Argument);
				WriteChar('}');
			}
			else if (def.Type is CiArrayStorageType) {
				switch (def.Value) {
				case null:
					break;
				case CiLiteral literal when literal.IsDefaultValue():
					Write(" {}");
					break;
				default:
					throw new NotImplementedException();
				}
			}
			else
				base.WriteVarInit(def);
		}

		static bool IsSharedPtr(CiExpr expr)
		{
			if (expr.Type is CiDynamicPtrType)
				return true;
			return expr is CiSymbolReference symbol && symbol.Symbol.Parent is CiForeach loop && loop.Collection.Type.AsClassType().GetElementType() is CiDynamicPtrType;
		}

		protected override void WriteStaticCast(CiType type, CiExpr expr)
		{
			if (type is CiDynamicPtrType dynamic) {
				Write("std::static_pointer_cast<");
				Write(dynamic.Class.Name);
			}
			else {
				Write("static_cast<");
				WriteType(type, false);
			}
			Write(">(");
			if (expr.Type is CiStorageType) {
				WriteChar('&');
				expr.Accept(this, CiPriority.Primary);
			}
			else if (!(type is CiDynamicPtrType) && IsSharedPtr(expr))
				WritePostfix(expr, ".get()");
			else
				GetStaticCastInner(type, expr).Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		static bool NeedStringPtrData(CiExpr expr)
		{
			if (expr is CiCallExpr call && call.Method.Symbol.Id == CiId.EnvironmentGetEnvironmentVariable)
				return false;
			return expr.Type.Id == CiId.StringPtrType;
		}

		protected override void WriteEqual(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			if (NeedStringPtrData(left) && right.Type.Id == CiId.NullType) {
				WritePostfix(left, ".data()");
				Write(GetEqOp(not));
				Write("nullptr");
			}
			else if (left.Type.Id == CiId.NullType && NeedStringPtrData(right)) {
				Write("nullptr");
				Write(GetEqOp(not));
				WritePostfix(right, ".data()");
			}
			else
				base.WriteEqual(left, right, parent, not);
		}

		static bool IsClassPtr(CiType type) => type is CiClassType ptr && !(type is CiStorageType) && ptr.Class.Id != CiId.StringClass && ptr.Class.Id != CiId.ArrayPtrClass;

		static bool IsCppPtr(CiExpr expr)
		{
			if (IsClassPtr(expr.Type)) {
				if (expr is CiSymbolReference symbol && symbol.Symbol.Parent is CiForeach loop && (symbol.Symbol == loop.GetVar() ? loop.Collection.Type.AsClassType().TypeArg0 : loop.Collection.Type.AsClassType().TypeArg1) is CiStorageType)
					return false;
				return true;
			}
			return false;
		}

		protected override void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
		{
			CiClassType klass = (CiClassType) expr.Left.Type;
			if (parent != CiPriority.Assign) {
				switch (klass.Class.Id) {
				case CiId.DictionaryClass:
				case CiId.SortedDictionaryClass:
				case CiId.OrderedDictionaryClass:
					StartMethodCall(expr.Left);
					Write("find(");
					WriteStronglyCoerced(klass.GetKeyType(), expr.Right);
					Write(")->second");
					return;
				default:
					break;
				}
			}
			if (IsClassPtr(expr.Left.Type)) {
				Write("(*");
				expr.Left.Accept(this, CiPriority.Primary);
				WriteChar(')');
			}
			else
				expr.Left.Accept(this, CiPriority.Primary);
			WriteChar('[');
			switch (klass.Class.Id) {
			case CiId.ArrayPtrClass:
			case CiId.ArrayStorageClass:
			case CiId.ListClass:
				expr.Right.Accept(this, CiPriority.Argument);
				break;
			default:
				WriteStronglyCoerced(klass.GetKeyType(), expr.Right);
				break;
			}
			WriteChar(']');
		}

		protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
		{
			if (symbol != null && symbol.Symbol is CiConst)
				Write("::");
			else if (IsCppPtr(left))
				Write("->");
			else
				WriteChar('.');
		}

		protected override void WriteEnumAsInt(CiExpr expr, CiPriority parent)
		{
			Write("static_cast<int>(");
			expr.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		void WriteCollectionObject(CiExpr obj, CiPriority priority)
		{
			if (IsCppPtr(obj)) {
				WriteChar('*');
				obj.Accept(this, CiPriority.Primary);
			}
			else
				obj.Accept(this, priority);
		}

		void WriteBeginEnd(CiExpr obj)
		{
			StartMethodCall(obj);
			Write("begin(), ");
			StartMethodCall(obj);
			Write("end()");
		}

		void WritePtrRange(CiExpr obj, CiExpr index, CiExpr count)
		{
			WriteArrayPtrAdd(obj, index);
			Write(", ");
			WriteArrayPtrAdd(obj, index);
			Write(" + ");
			count.Accept(this, CiPriority.Mul);
		}

		void WriteNotRawStringLiteral(CiExpr obj, CiPriority priority)
		{
			obj.Accept(this, priority);
			if (obj is CiLiteralString) {
				Include("string_view");
				this.UsingStringViewLiterals = true;
				Write("sv");
			}
		}

		void WriteStringMethod(CiExpr obj, string name, CiMethod method, List<CiExpr> args)
		{
			WriteNotRawStringLiteral(obj, CiPriority.Primary);
			WriteChar('.');
			Write(name);
			int c = GetOneAscii(args[0]);
			if (c >= 0) {
				WriteChar('(');
				VisitLiteralChar(c);
				WriteChar(')');
			}
			else
				WriteArgsInParentheses(method, args);
		}

		void WriteAllAnyContains(string function, CiExpr obj, List<CiExpr> args)
		{
			Include("algorithm");
			Write("std::");
			Write(function);
			WriteChar('(');
			WriteBeginEnd(obj);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		void WriteCString(CiExpr expr)
		{
			if (expr is CiLiteralString)
				expr.Accept(this, CiPriority.Argument);
			else
				WritePostfix(expr, ".data()");
		}

		void WriteRegex(List<CiExpr> args, int argIndex)
		{
			Include("regex");
			Write("std::regex(");
			args[argIndex].Accept(this, CiPriority.Argument);
			WriteRegexOptions(args, ", std::regex::ECMAScript | ", " | ", "", "std::regex::icase", "std::regex::multiline", "std::regex::NOT_SUPPORTED_singleline");
			WriteChar(')');
		}

		void WriteWrite(List<CiExpr> args, bool newLine)
		{
			Include("iostream");
			if (args.Count == 1) {
				if (args[0] is CiInterpolatedString interpolated) {
					bool uppercase = false;
					bool hex = false;
					int flt = 'G';
					foreach (CiInterpolatedPart part in interpolated.Parts) {
						switch (part.Format) {
						case 'E':
						case 'G':
						case 'X':
							if (!uppercase) {
								Write(" << std::uppercase");
								uppercase = true;
							}
							break;
						case 'e':
						case 'g':
						case 'x':
							if (uppercase) {
								Write(" << std::nouppercase");
								uppercase = false;
							}
							break;
						default:
							break;
						}
						switch (part.Format) {
						case 'E':
						case 'e':
							if (flt != 'E') {
								Write(" << std::scientific");
								flt = 'E';
							}
							break;
						case 'F':
						case 'f':
							if (flt != 'F') {
								Write(" << std::fixed");
								flt = 'F';
							}
							break;
						case 'X':
						case 'x':
							if (!hex) {
								Write(" << std::hex");
								hex = true;
							}
							break;
						default:
							if (hex) {
								Write(" << std::dec");
								hex = false;
							}
							if (flt != 'G') {
								Write(" << std::defaultfloat");
								flt = 'G';
							}
							break;
						}
						if (part.Prefix.Length > 0) {
							Write(" << ");
							VisitLiteralString(part.Prefix);
						}
						Write(" << ");
						part.Argument.Accept(this, CiPriority.Mul);
					}
					if (uppercase)
						Write(" << std::nouppercase");
					if (hex)
						Write(" << std::dec");
					if (flt != 'G')
						Write(" << std::defaultfloat");
					if (interpolated.Suffix.Length > 0) {
						Write(" << ");
						if (newLine) {
							WriteStringLiteralWithNewLine(interpolated.Suffix);
							return;
						}
						VisitLiteralString(interpolated.Suffix);
					}
				}
				else {
					Write(" << ");
					if (newLine && args[0] is CiLiteralString literal) {
						WriteStringLiteralWithNewLine(literal.Value);
						return;
					}
					else if (args[0] is CiLiteralChar)
						WriteCall("static_cast<int>", args[0]);
					else
						args[0].Accept(this, CiPriority.Mul);
				}
			}
			if (newLine)
				Write(" << '\\n'");
		}

		void WriteRegexArgument(CiExpr expr)
		{
			if (expr.Type is CiDynamicPtrType)
				expr.Accept(this, CiPriority.Argument);
			else {
				WriteChar('*');
				expr.Accept(this, CiPriority.Primary);
			}
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.None:
			case CiId.ClassToString:
			case CiId.ListClear:
			case CiId.StackPush:
			case CiId.HashSetClear:
			case CiId.HashSetContains:
			case CiId.SortedSetClear:
			case CiId.SortedSetContains:
			case CiId.DictionaryClear:
			case CiId.SortedDictionaryClear:
				if (obj != null) {
					if (IsReferenceTo(obj, CiId.BasePtr)) {
						WriteName(method.Parent);
						Write("::");
					}
					else {
						obj.Accept(this, CiPriority.Primary);
						if (method.CallType == CiCallType.Static)
							Write("::");
						else
							WriteMemberOp(obj, null);
					}
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case CiId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case CiId.IntTryParse:
			case CiId.LongTryParse:
				Include("cstdlib");
				Write("[&] { char *ciend; ");
				obj.Accept(this, CiPriority.Assign);
				Write(" = std::strtol");
				if (method.Id == CiId.LongTryParse)
					WriteChar('l');
				WriteChar('(');
				WriteCString(args[0]);
				Write(", &ciend");
				WriteTryParseRadix(args);
				Write("); return *ciend == '\\0'; }()");
				break;
			case CiId.DoubleTryParse:
				Include("cstdlib");
				Write("[&] { char *ciend; ");
				obj.Accept(this, CiPriority.Assign);
				Write(" = std::strtod(");
				WriteCString(args[0]);
				Write(", &ciend); return *ciend == '\\0'; }()");
				break;
			case CiId.StringContains:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				WriteStringMethod(obj, "find", method, args);
				Write(" != std::string::npos");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.StringEndsWith:
				WriteStringMethod(obj, "ends_with", method, args);
				break;
			case CiId.StringIndexOf:
				Write("static_cast<int>(");
				WriteStringMethod(obj, "find", method, args);
				WriteChar(')');
				break;
			case CiId.StringLastIndexOf:
				Write("static_cast<int>(");
				WriteStringMethod(obj, "rfind", method, args);
				WriteChar(')');
				break;
			case CiId.StringReplace:
				this.StringReplace = true;
				WriteCall("CiString_replace", obj, args[0], args[1]);
				break;
			case CiId.StringStartsWith:
				WriteStringMethod(obj, "starts_with", method, args);
				break;
			case CiId.StringSubstring:
				WriteStringMethod(obj, "substr", method, args);
				break;
			case CiId.ArrayBinarySearchAll:
			case CiId.ArrayBinarySearchPart:
				Include("algorithm");
				if (parent > CiPriority.Add)
					WriteChar('(');
				Write("std::lower_bound(");
				if (args.Count == 1)
					WriteBeginEnd(obj);
				else
					WritePtrRange(obj, args[1], args[2]);
				Write(", ");
				args[0].Accept(this, CiPriority.Argument);
				Write(") - ");
				WriteArrayPtr(obj, CiPriority.Mul);
				if (parent > CiPriority.Add)
					WriteChar(')');
				break;
			case CiId.ArrayCopyTo:
			case CiId.ListCopyTo:
				Include("algorithm");
				Write("std::copy_n(");
				WriteArrayPtrAdd(obj, args[0]);
				Write(", ");
				args[3].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteArrayPtrAdd(args[1], args[2]);
				WriteChar(')');
				break;
			case CiId.ArrayFillAll:
				StartMethodCall(obj);
				Write("fill(");
				WriteCoerced(obj.Type.AsClassType().GetElementType(), args[0], CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ArrayFillPart:
				Include("algorithm");
				Write("std::fill_n(");
				WriteArrayPtrAdd(obj, args[1]);
				Write(", ");
				args[2].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteCoerced(obj.Type.AsClassType().GetElementType(), args[0], CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ArraySortAll:
			case CiId.ListSortAll:
				Include("algorithm");
				Write("std::sort(");
				WriteBeginEnd(obj);
				WriteChar(')');
				break;
			case CiId.ArraySortPart:
			case CiId.ListSortPart:
				Include("algorithm");
				Write("std::sort(");
				WritePtrRange(obj, args[0], args[1]);
				WriteChar(')');
				break;
			case CiId.ListAdd:
				StartMethodCall(obj);
				if (args.Count == 0)
					Write("emplace_back()");
				else {
					Write("push_back(");
					WriteCoerced(obj.Type.AsClassType().GetElementType(), args[0], CiPriority.Argument);
					WriteChar(')');
				}
				break;
			case CiId.ListAddRange:
				StartMethodCall(obj);
				Write("insert(");
				StartMethodCall(obj);
				Write("end(), ");
				WriteBeginEnd(args[0]);
				WriteChar(')');
				break;
			case CiId.ListAll:
				WriteAllAnyContains("all_of", obj, args);
				break;
			case CiId.ListAny:
				Include("algorithm");
				WriteAllAnyContains("any_of", obj, args);
				break;
			case CiId.ListContains:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				WriteAllAnyContains("find", obj, args);
				Write(" != ");
				StartMethodCall(obj);
				Write("end()");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.ListIndexOf:
				{
					CiType elementType = obj.Type.AsClassType().GetElementType();
					Write("[](const ");
					WriteCollectionType("vector", elementType);
					Write(" &v, ");
					WriteType(elementType, false);
					Include("algorithm");
					Write(" value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(");
					WriteCollectionObject(obj, CiPriority.Argument);
					Write(", ");
					WriteCoerced(elementType, args[0], CiPriority.Argument);
					WriteChar(')');
				}
				break;
			case CiId.ListInsert:
				StartMethodCall(obj);
				if (args.Count == 1) {
					Write("emplace(");
					WriteArrayPtrAdd(obj, args[0]);
				}
				else {
					Write("insert(");
					WriteArrayPtrAdd(obj, args[0]);
					Write(", ");
					WriteCoerced(obj.Type.AsClassType().GetElementType(), args[1], CiPriority.Argument);
				}
				WriteChar(')');
				break;
			case CiId.ListLast:
				StartMethodCall(obj);
				Write("back()");
				break;
			case CiId.ListRemoveAt:
				StartMethodCall(obj);
				Write("erase(");
				WriteArrayPtrAdd(obj, args[0]);
				WriteChar(')');
				break;
			case CiId.ListRemoveRange:
				StartMethodCall(obj);
				Write("erase(");
				WritePtrRange(obj, args[0], args[1]);
				WriteChar(')');
				break;
			case CiId.QueueClear:
			case CiId.StackClear:
				WriteCollectionObject(obj, CiPriority.Assign);
				Write(" = {}");
				break;
			case CiId.QueueDequeue:
				if (parent == CiPriority.Statement) {
					StartMethodCall(obj);
					Write("pop()");
				}
				else {
					CiType elementType = obj.Type.AsClassType().GetElementType();
					Write("[](");
					WriteCollectionType("queue", elementType);
					Write(" &q) { ");
					WriteType(elementType, false);
					Write(" front = q.front(); q.pop(); return front; }(");
					WriteCollectionObject(obj, CiPriority.Argument);
					WriteChar(')');
				}
				break;
			case CiId.QueueEnqueue:
				WriteMethodCall(obj, "push", args[0]);
				break;
			case CiId.QueuePeek:
				StartMethodCall(obj);
				Write("front()");
				break;
			case CiId.StackPeek:
				StartMethodCall(obj);
				Write("top()");
				break;
			case CiId.StackPop:
				if (parent == CiPriority.Statement) {
					StartMethodCall(obj);
					Write("pop()");
				}
				else {
					CiType elementType = obj.Type.AsClassType().GetElementType();
					Write("[](");
					WriteCollectionType("stack", elementType);
					Write(" &s) { ");
					WriteType(elementType, false);
					Write(" top = s.top(); s.pop(); return top; }(");
					WriteCollectionObject(obj, CiPriority.Argument);
					WriteChar(')');
				}
				break;
			case CiId.HashSetAdd:
			case CiId.SortedSetAdd:
				WriteMethodCall(obj, obj.Type.AsClassType().GetElementType().Id == CiId.StringStorageType && args[0].Type.Id == CiId.StringPtrType ? "emplace" : "insert", args[0]);
				break;
			case CiId.HashSetRemove:
			case CiId.SortedSetRemove:
			case CiId.DictionaryRemove:
			case CiId.SortedDictionaryRemove:
				WriteMethodCall(obj, "erase", args[0]);
				break;
			case CiId.DictionaryAdd:
				WriteIndexing(obj, args[0]);
				break;
			case CiId.DictionaryContainsKey:
			case CiId.SortedDictionaryContainsKey:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				StartMethodCall(obj);
				Write("count(");
				WriteStronglyCoerced(obj.Type.AsClassType().GetKeyType(), args[0]);
				Write(") != 0");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.TextWriterWrite:
				WriteCollectionObject(obj, CiPriority.Shift);
				WriteWrite(args, false);
				break;
			case CiId.TextWriterWriteChar:
				WriteCollectionObject(obj, CiPriority.Shift);
				Write(" << ");
				if (args[0] is CiLiteralChar literalChar && literalChar.Value < 127)
					args[0].Accept(this, CiPriority.Mul);
				else
					WriteCall("static_cast<char>", args[0]);
				break;
			case CiId.TextWriterWriteCodePoint:
				if (args[0] is CiLiteralChar literalChar2 && literalChar2.Value < 127) {
					WriteCollectionObject(obj, CiPriority.Shift);
					Write(" << ");
					args[0].Accept(this, CiPriority.Mul);
				}
				else {
					Write("if (");
					args[0].Accept(this, CiPriority.Rel);
					WriteLine(" < 0x80)");
					WriteChar('\t');
					WriteCollectionObject(obj, CiPriority.Shift);
					Write(" << ");
					WriteCall("static_cast<char>", args[0]);
					WriteCharLine(';');
					Write("else if (");
					args[0].Accept(this, CiPriority.Rel);
					WriteLine(" < 0x800)");
					WriteChar('\t');
					WriteCollectionObject(obj, CiPriority.Shift);
					Write(" << static_cast<char>(0xc0 | ");
					args[0].Accept(this, CiPriority.Shift);
					Write(" >> 6) << static_cast<char>(0x80 | (");
					args[0].Accept(this, CiPriority.And);
					WriteLine(" & 0x3f));");
					Write("else if (");
					args[0].Accept(this, CiPriority.Rel);
					WriteLine(" < 0x10000)");
					WriteChar('\t');
					WriteCollectionObject(obj, CiPriority.Shift);
					Write(" << static_cast<char>(0xe0 | ");
					args[0].Accept(this, CiPriority.Shift);
					Write(" >> 12) << static_cast<char>(0x80 | (");
					args[0].Accept(this, CiPriority.Shift);
					Write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
					args[0].Accept(this, CiPriority.And);
					WriteLine(" & 0x3f));");
					WriteLine("else");
					WriteChar('\t');
					WriteCollectionObject(obj, CiPriority.Shift);
					Write(" << static_cast<char>(0xf0 | ");
					args[0].Accept(this, CiPriority.Shift);
					Write(" >> 18) << static_cast<char>(0x80 | (");
					args[0].Accept(this, CiPriority.Shift);
					Write(" >> 12 & 0x3f)) << static_cast<char>(0x80 | (");
					args[0].Accept(this, CiPriority.Shift);
					Write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
					args[0].Accept(this, CiPriority.And);
					Write(" & 0x3f))");
				}
				break;
			case CiId.TextWriterWriteLine:
				WriteCollectionObject(obj, CiPriority.Shift);
				WriteWrite(args, true);
				break;
			case CiId.StringWriterClear:
				Include("string");
				StartMethodCall(obj);
				Write("str(std::string())");
				break;
			case CiId.ConsoleWrite:
				Write("std::cout");
				WriteWrite(args, false);
				break;
			case CiId.ConsoleWriteLine:
				Write("std::cout");
				WriteWrite(args, true);
				break;
			case CiId.StringWriterToString:
				StartMethodCall(obj);
				Write("str()");
				break;
			case CiId.UTF8GetByteCount:
				if (args[0] is CiLiteral) {
					if (parent > CiPriority.Add)
						WriteChar('(');
					Write("sizeof(");
					args[0].Accept(this, CiPriority.Argument);
					Write(") - 1");
					if (parent > CiPriority.Add)
						WriteChar(')');
				}
				else
					WriteStringLength(args[0]);
				break;
			case CiId.UTF8GetBytes:
				if (args[0] is CiLiteral) {
					Include("algorithm");
					Write("std::copy_n(");
					args[0].Accept(this, CiPriority.Argument);
					Write(", sizeof(");
					args[0].Accept(this, CiPriority.Argument);
					Write(") - 1, ");
					WriteArrayPtrAdd(args[1], args[2]);
					WriteChar(')');
				}
				else {
					WritePostfix(args[0], ".copy(reinterpret_cast<char *>(");
					WriteArrayPtrAdd(args[1], args[2]);
					Write("), ");
					WritePostfix(args[0], ".size())");
				}
				break;
			case CiId.UTF8GetString:
				Include("string_view");
				Write("std::string_view(reinterpret_cast<const char *>(");
				WriteArrayPtrAdd(args[0], args[1]);
				Write("), ");
				args[2].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.EnvironmentGetEnvironmentVariable:
				Include("cstdlib");
				Write("std::getenv(");
				WriteCString(args[0]);
				WriteChar(')');
				break;
			case CiId.RegexCompile:
				WriteRegex(args, 0);
				break;
			case CiId.RegexIsMatchStr:
			case CiId.RegexIsMatchRegex:
			case CiId.MatchFindStr:
			case CiId.MatchFindRegex:
				Write("std::regex_search(");
				if (args[0].Type.Id == CiId.StringPtrType && !(args[0] is CiLiteral))
					WriteBeginEnd(args[0]);
				else
					args[0].Accept(this, CiPriority.Argument);
				if (method.Id == CiId.MatchFindStr || method.Id == CiId.MatchFindRegex) {
					Write(", ");
					obj.Accept(this, CiPriority.Argument);
				}
				Write(", ");
				if (method.Id == CiId.RegexIsMatchRegex)
					WriteRegexArgument(obj);
				else if (method.Id == CiId.MatchFindRegex)
					WriteRegexArgument(args[1]);
				else
					WriteRegex(args, 1);
				WriteChar(')');
				break;
			case CiId.MatchGetCapture:
				StartMethodCall(obj);
				WriteCall("str", args[0]);
				break;
			case CiId.MathMethod:
			case CiId.MathAbs:
			case CiId.MathIsFinite:
			case CiId.MathIsNaN:
			case CiId.MathLog2:
			case CiId.MathRound:
				IncludeMath();
				Write("std::");
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathCeiling:
				IncludeMath();
				WriteCall("std::ceil", args[0]);
				break;
			case CiId.MathClamp:
				Include("algorithm");
				WriteCall("std::clamp", args[0], args[1], args[2]);
				break;
			case CiId.MathFusedMultiplyAdd:
				IncludeMath();
				WriteCall("std::fma", args[0], args[1], args[2]);
				break;
			case CiId.MathIsInfinity:
				IncludeMath();
				WriteCall("std::isinf", args[0]);
				break;
			case CiId.MathMaxInt:
			case CiId.MathMaxDouble:
				Include("algorithm");
				WriteCall("std::max", args[0], args[1]);
				break;
			case CiId.MathMinInt:
			case CiId.MathMinDouble:
				Include("algorithm");
				WriteCall("std::min", args[0], args[1]);
				break;
			case CiId.MathTruncate:
				IncludeMath();
				WriteCall("std::trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteResource(string name, int length)
		{
			Write("CiResource::");
			WriteResourceName(name);
		}

		protected override void WriteArrayPtr(CiExpr expr, CiPriority parent)
		{
			switch (expr.Type) {
			case CiArrayStorageType _:
			case CiStringType _:
				WritePostfix(expr, ".data()");
				break;
			case CiDynamicPtrType _:
				WritePostfix(expr, ".get()");
				break;
			case CiClassType klass when klass.Class.Id == CiId.ListClass:
				StartMethodCall(expr);
				Write("begin()");
				break;
			default:
				expr.Accept(this, parent);
				break;
			}
		}

		protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
		{
			if (type is CiClassType klass && !(klass is CiDynamicPtrType) && !(klass is CiStorageType)) {
				if (klass.Class.Id == CiId.StringClass) {
					if (expr.Type.Id == CiId.NullType) {
						Include("string_view");
						Write("std::string_view()");
					}
					else
						expr.Accept(this, parent);
					return;
				}
				if (klass.Class.Id == CiId.ArrayPtrClass) {
					WriteArrayPtr(expr, parent);
					return;
				}
				if (IsSharedPtr(expr)) {
					if (klass.Class.Id == CiId.RegexClass) {
						WriteChar('&');
						expr.Accept(this, CiPriority.Primary);
					}
					else
						WritePostfix(expr, ".get()");
					return;
				}
				if (expr.Type is CiClassType && !IsCppPtr(expr)) {
					WriteChar('&');
					if (expr is CiCallExpr) {
						Write("static_cast<");
						if (!(klass is CiReadWriteClassType))
							Write("const ");
						WriteName(klass.Class);
						Write(" &>(");
						expr.Accept(this, CiPriority.Argument);
						WriteChar(')');
					}
					else
						expr.Accept(this, CiPriority.Primary);
					return;
				}
			}
			base.WriteCoercedInternal(type, expr, parent);
		}

		protected override void WriteSelectValues(CiType type, CiSelectExpr expr)
		{
			if (expr.OnTrue.Type is CiClassType trueClass && expr.OnFalse.Type is CiClassType falseClass && !trueClass.Class.IsSameOrBaseOf(falseClass.Class) && !falseClass.Class.IsSameOrBaseOf(trueClass.Class)) {
				WriteStaticCast(type, expr.OnTrue);
				Write(" : ");
				WriteStaticCast(type, expr.OnFalse);
			}
			else
				base.WriteSelectValues(type, expr);
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			WriteNotRawStringLiteral(expr, CiPriority.Primary);
			Write(".length()");
		}

		void WriteMatchProperty(CiSymbolReference expr, string name)
		{
			StartMethodCall(expr.Left);
			Write(name);
			Write("()");
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.ConsoleError:
				Write("std::cerr");
				break;
			case CiId.ListCount:
			case CiId.QueueCount:
			case CiId.StackCount:
			case CiId.HashSetCount:
			case CiId.SortedSetCount:
			case CiId.DictionaryCount:
			case CiId.SortedDictionaryCount:
			case CiId.OrderedDictionaryCount:
				expr.Left.Accept(this, CiPriority.Primary);
				WriteMemberOp(expr.Left, expr);
				Write("size()");
				break;
			case CiId.MatchStart:
				WriteMatchProperty(expr, "position");
				break;
			case CiId.MatchEnd:
				if (parent > CiPriority.Add)
					WriteChar('(');
				WriteMatchProperty(expr, "position");
				Write(" + ");
				WriteMatchProperty(expr, "length");
				if (parent > CiPriority.Add)
					WriteChar(')');
				break;
			case CiId.MatchLength:
				WriteMatchProperty(expr, "length");
				break;
			case CiId.MatchValue:
				WriteMatchProperty(expr, "str");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		void WriteGtRawPtr(CiExpr expr)
		{
			Write(">(");
			if (IsSharedPtr(expr))
				WritePostfix(expr, ".get()");
			else
				expr.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		void WriteIsVar(CiExpr expr, CiVar def, CiPriority parent)
		{
			if (def.Name != "_") {
				if (parent > CiPriority.Assign)
					WriteChar('(');
				WriteName(def);
				Write(" = ");
			}
			if (def.Type is CiDynamicPtrType dynamic) {
				Write("std::dynamic_pointer_cast<");
				Write(dynamic.Class.Name);
				WriteCall(">", expr);
			}
			else {
				Write("dynamic_cast<");
				WriteType(def.Type, true);
				WriteGtRawPtr(expr);
			}
			if (def.Name != "_" && parent > CiPriority.Assign)
				WriteChar(')');
		}

		public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Plus:
				if (expr.Type.Id == CiId.StringStorageType) {
					if (parent > CiPriority.Add)
						WriteChar('(');
					WriteStronglyCoerced(expr.Type, expr.Left);
					Write(" + ");
					WriteStronglyCoerced(expr.Type, expr.Right);
					if (parent > CiPriority.Add)
						WriteChar(')');
					return;
				}
				break;
			case CiToken.Equal:
			case CiToken.NotEqual:
			case CiToken.Greater:
				CiExpr str = IsStringEmpty(expr);
				if (str != null) {
					if (expr.Op != CiToken.Equal)
						WriteChar('!');
					WritePostfix(str, ".empty()");
					return;
				}
				break;
			case CiToken.Assign:
				CiExpr length = IsTrimSubstring(expr);
				if (length != null && expr.Left.Type.Id == CiId.StringStorageType && parent == CiPriority.Statement) {
					WriteMethodCall(expr.Left, "resize", length);
					return;
				}
				break;
			case CiToken.Is:
				switch (expr.Right) {
				case CiSymbolReference symbol:
					if (parent >= CiPriority.Or && parent <= CiPriority.Mul)
						Write("!!");
					Write("dynamic_cast<const ");
					Write(symbol.Symbol.Name);
					Write(" *");
					WriteGtRawPtr(expr.Left);
					return;
				case CiVar def:
					WriteIsVar(expr.Left, def, parent);
					return;
				default:
					throw new NotImplementedException();
				}
			default:
				break;
			}
			base.VisitBinaryExpr(expr, parent);
		}

		public override void VisitLambdaExpr(CiLambdaExpr expr)
		{
			Write("[](const ");
			WriteType(expr.First.Type, false);
			Write(" &");
			WriteName(expr.First);
			Write(") { ");
			WriteTemporaries(expr.Body);
			Write("return ");
			expr.Body.Accept(this, CiPriority.Argument);
			Write("; }");
		}

		protected override void WriteUnreachable(CiAssert statement)
		{
			Include("cstdlib");
			Write("std::");
			base.WriteUnreachable(statement);
		}

		protected override void WriteConst(CiConst konst)
		{
			Write("static constexpr ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, CiPriority.Argument);
			WriteCharLine(';');
		}

		public override void VisitForeach(CiForeach statement)
		{
			CiVar element = statement.GetVar();
			Write("for (");
			if (statement.Count() == 2) {
				Write("const auto &[");
				WriteCamelCaseNotKeyword(element.Name);
				Write(", ");
				WriteCamelCaseNotKeyword(statement.GetValueVar().Name);
				WriteChar(']');
			}
			else {
				switch (statement.Collection.Type.AsClassType().GetElementType()) {
				case CiStorageType storage:
					if (!(element.Type is CiReadWriteClassType))
						Write("const ");
					Write(storage.Class.Name);
					Write(" &");
					WriteCamelCaseNotKeyword(element.Name);
					break;
				case CiDynamicPtrType dynamic:
					Write("const ");
					WriteType(dynamic, true);
					Write(" &");
					WriteCamelCaseNotKeyword(element.Name);
					break;
				default:
					WriteTypeAndName(element);
					break;
				}
			}
			Write(" : ");
			if (statement.Collection.Type is CiStringType)
				WriteNotRawStringLiteral(statement.Collection, CiPriority.Argument);
			else
				WriteCollectionObject(statement.Collection, CiPriority.Argument);
			WriteChar(')');
			WriteChild(statement.Body);
		}

		protected override bool EmbedIfWhileIsVar(CiExpr expr, bool write)
		{
			if (expr is CiBinaryExpr binary && binary.Op == CiToken.Is && binary.Right is CiVar def) {
				if (write)
					WriteType(def.Type, true);
				return true;
			}
			return false;
		}

		public override void VisitLock(CiLock statement)
		{
			OpenBlock();
			Write("const std::lock_guard<std::recursive_mutex> lock(");
			statement.Lock.Accept(this, CiPriority.Argument);
			WriteLine(");");
			FlattenBlock(statement.Body);
			CloseBlock();
		}

		protected override void WriteStronglyCoerced(CiType type, CiExpr expr)
		{
			if (type.Id == CiId.StringStorageType && expr.Type.Id == CiId.StringPtrType && !(expr is CiLiteral)) {
				Write("std::string(");
				expr.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			else {
				CiCallExpr call = IsStringSubstring(expr);
				if (call != null && type.Id == CiId.StringStorageType && GetStringSubstringPtr(call).Type.Id != CiId.StringStorageType) {
					Write("std::string(");
					bool cast = IsUTF8GetString(call);
					if (cast)
						Write("reinterpret_cast<const char *>(");
					WriteStringPtrAdd(call);
					if (cast)
						WriteChar(')');
					Write(", ");
					GetStringSubstringLength(call).Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
				else
					base.WriteStronglyCoerced(type, expr);
			}
		}

		protected override void WriteSwitchCaseCond(CiSwitch statement, CiExpr value, CiPriority parent)
		{
			if (value is CiVar def) {
				if (parent == CiPriority.Argument && def.Name != "_")
					WriteType(def.Type, true);
				WriteIsVar(statement.Value, def, parent);
			}
			else
				base.WriteSwitchCaseCond(statement, value, parent);
		}

		static bool HasTemporaries(CiExpr expr)
		{
			switch (expr) {
			case CiAggregateInitializer init:
				return init.Items.Any(item => HasTemporaries(item));
			case CiLiteral _:
			case CiLambdaExpr _:
				return false;
			case CiInterpolatedString interp:
				return interp.Parts.Any(part => HasTemporaries(part.Argument));
			case CiSymbolReference symbol:
				return symbol.Left != null && HasTemporaries(symbol.Left);
			case CiUnaryExpr unary:
				return unary.Inner != null && (HasTemporaries(unary.Inner) || unary.Inner is CiAggregateInitializer);
			case CiBinaryExpr binary:
				if (HasTemporaries(binary.Left))
					return true;
				if (binary.Op == CiToken.Is)
					return binary.Right is CiVar;
				return HasTemporaries(binary.Right);
			case CiSelectExpr select:
				return HasTemporaries(select.Cond) || HasTemporaries(select.OnTrue) || HasTemporaries(select.OnFalse);
			case CiCallExpr call:
				return HasTemporaries(call.Method) || call.Arguments.Any(arg => HasTemporaries(arg));
			default:
				throw new NotImplementedException();
			}
		}

		static bool IsIsVar(CiExpr expr) => expr is CiBinaryExpr binary && binary.Op == CiToken.Is && binary.Right is CiVar;

		bool HasVariables(CiStatement statement)
		{
			switch (statement) {
			case CiVar _:
				return true;
			case CiAssert asrt:
				return IsIsVar(asrt.Cond);
			case CiBlock _:
			case CiBreak _:
			case CiConst _:
			case CiContinue _:
			case CiLock _:
			case CiNative _:
			case CiThrow _:
				return false;
			case CiIf ifStatement:
				return HasTemporaries(ifStatement.Cond) && !IsIsVar(ifStatement.Cond);
			case CiLoop loop:
				return loop.Cond != null && HasTemporaries(loop.Cond);
			case CiReturn ret:
				return ret.Value != null && HasTemporaries(ret.Value);
			case CiSwitch switch_:
				return HasTemporaries(switch_.Value);
			case CiExpr expr:
				return HasTemporaries(expr);
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteSwitchCaseBody(List<CiStatement> statements)
		{
			bool block = false;
			foreach (CiStatement statement in statements) {
				if (!block && HasVariables(statement)) {
					OpenBlock();
					block = true;
				}
				statement.AcceptStatement(this);
			}
			if (block)
				CloseBlock();
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			if (statement.IsTypeMatching())
				WriteSwitchAsIfsWithGoto(statement);
			else
				base.VisitSwitch(statement);
		}

		public override void VisitThrow(CiThrow statement)
		{
			Include("exception");
			WriteLine("throw std::exception();");
		}

		void OpenNamespace()
		{
			if (this.Namespace.Length == 0)
				return;
			WriteNewLine();
			Write("namespace ");
			WriteLine(this.Namespace);
			WriteCharLine('{');
		}

		void CloseNamespace()
		{
			if (this.Namespace.Length != 0)
				WriteCharLine('}');
		}

		protected override void WriteEnum(CiEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			Write("enum class ");
			WriteLine(enu.Name);
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			this.Indent--;
			WriteLine("};");
			if (enu is CiEnumFlags) {
				Include("type_traits");
				this.HasEnumFlags = true;
				Write("CI_ENUM_FLAG_OPERATORS(");
				Write(enu.Name);
				WriteCharLine(')');
			}
		}

		static CiVisibility GetConstructorVisibility(CiClass klass)
		{
			switch (klass.CallType) {
			case CiCallType.Static:
				return CiVisibility.Private;
			case CiCallType.Abstract:
				return CiVisibility.Protected;
			default:
				return CiVisibility.Public;
			}
		}

		static bool HasMembersOfVisibility(CiClass klass, CiVisibility visibility)
		{
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is CiMember member && member.Visibility == visibility)
					return true;
			}
			return false;
		}

		protected override void WriteField(CiField field)
		{
			WriteDoc(field.Documentation);
			WriteVar(field);
			WriteCharLine(';');
		}

		void WriteParametersAndConst(CiMethod method, bool defaultArguments)
		{
			WriteParameters(method, defaultArguments);
			if (method.CallType != CiCallType.Static && !method.IsMutator)
				Write(" const");
		}

		void WriteDeclarations(CiClass klass, CiVisibility visibility, string visibilityKeyword)
		{
			bool constructor = GetConstructorVisibility(klass) == visibility;
			bool destructor = visibility == CiVisibility.Public && (klass.HasSubclasses || klass.AddsVirtualMethods());
			if (!constructor && !destructor && !HasMembersOfVisibility(klass, visibility))
				return;
			Write(visibilityKeyword);
			WriteCharLine(':');
			this.Indent++;
			if (constructor) {
				if (klass.Constructor != null)
					WriteDoc(klass.Constructor.Documentation);
				Write(klass.Name);
				Write("()");
				if (klass.CallType == CiCallType.Static)
					Write(" = delete");
				else if (!NeedsConstructor(klass))
					Write(" = default");
				WriteCharLine(';');
			}
			if (destructor) {
				Write("virtual ~");
				Write(klass.Name);
				WriteLine("() = default;");
			}
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (!(symbol is CiMember member) || member.Visibility != visibility)
					continue;
				switch (member) {
				case CiConst konst:
					WriteDoc(konst.Documentation);
					WriteConst(konst);
					break;
				case CiField field:
					WriteField(field);
					break;
				case CiMethod method:
					WriteMethodDoc(method);
					switch (method.CallType) {
					case CiCallType.Static:
						Write("static ");
						break;
					case CiCallType.Abstract:
					case CiCallType.Virtual:
						Write("virtual ");
						break;
					default:
						break;
					}
					WriteTypeAndName(method);
					WriteParametersAndConst(method, true);
					switch (method.CallType) {
					case CiCallType.Abstract:
						Write(" = 0");
						break;
					case CiCallType.Override:
						Write(" override");
						break;
					case CiCallType.Sealed:
						Write(" final");
						break;
					default:
						break;
					}
					WriteCharLine(';');
					break;
				default:
					throw new NotImplementedException();
				}
			}
			this.Indent--;
		}

		protected override void WriteClassInternal(CiClass klass)
		{
			WriteNewLine();
			WriteDoc(klass.Documentation);
			OpenClass(klass, klass.CallType == CiCallType.Sealed ? " final" : "", " : public ");
			this.Indent--;
			WriteDeclarations(klass, CiVisibility.Public, "public");
			WriteDeclarations(klass, CiVisibility.Protected, "protected");
			WriteDeclarations(klass, CiVisibility.Internal, "public");
			WriteDeclarations(klass, CiVisibility.Private, "private");
			WriteLine("};");
		}

		void WriteConstructor(CiClass klass)
		{
			if (!NeedsConstructor(klass))
				return;
			this.SwitchesWithGoto.Clear();
			Write(klass.Name);
			Write("::");
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			WriteConstructorBody(klass);
			CloseBlock();
		}

		protected override void WriteMethod(CiMethod method)
		{
			if (method.CallType == CiCallType.Abstract)
				return;
			this.SwitchesWithGoto.Clear();
			WriteNewLine();
			WriteType(method.Type, true);
			WriteChar(' ');
			Write(method.Parent.Name);
			Write("::");
			WriteCamelCaseNotKeyword(method.Name);
			WriteParametersAndConst(method, false);
			WriteBody(method);
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources, bool define)
		{
			if (resources.Count == 0)
				return;
			WriteNewLine();
			WriteLine("namespace");
			OpenBlock();
			WriteLine("namespace CiResource");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				if (!define)
					Write("extern ");
				Include("array");
				Include("cstdint");
				Write("const std::array<uint8_t, ");
				VisitLiteralLong(content.Count);
				Write("> ");
				WriteResourceName(name);
				if (define) {
					WriteLine(" = {");
					WriteChar('\t');
					WriteBytes(content);
					Write(" }");
				}
				WriteCharLine(';');
			}
			CloseBlock();
			CloseBlock();
		}

		public override void WriteProgram(CiProgram program)
		{
			this.WrittenClasses.Clear();
			this.InHeaderFile = true;
			this.UsingStringViewLiterals = false;
			this.HasEnumFlags = false;
			this.StringReplace = false;
			OpenStringWriter();
			OpenNamespace();
			WriteRegexOptionsEnum(program);
			for (CiSymbol type = program.First; type != null; type = type.Next) {
				if (type is CiEnum enu)
					WriteEnum(enu);
				else {
					Write("class ");
					Write(type.Name);
					WriteCharLine(';');
				}
			}
			foreach (CiClass klass in program.Classes)
				WriteClass(klass, program);
			CloseNamespace();
			CreateHeaderFile(".hpp");
			if (this.HasEnumFlags) {
				WriteLine("#define CI_ENUM_FLAG_OPERATORS(T) \\");
				WriteLine("\tinline constexpr T operator~(T a) { return static_cast<T>(~static_cast<std::underlying_type_t<T>>(a)); } \\");
				WriteLine("\tinline constexpr T operator&(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) & static_cast<std::underlying_type_t<T>>(b)); } \\");
				WriteLine("\tinline constexpr T operator|(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) | static_cast<std::underlying_type_t<T>>(b)); } \\");
				WriteLine("\tinline constexpr T operator^(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) ^ static_cast<std::underlying_type_t<T>>(b)); } \\");
				WriteLine("\tinline constexpr T &operator&=(T &a, T b) { return (a = a & b); } \\");
				WriteLine("\tinline constexpr T &operator|=(T &a, T b) { return (a = a | b); } \\");
				WriteLine("\tinline constexpr T &operator^=(T &a, T b) { return (a = a ^ b); }");
			}
			CloseStringWriter();
			CloseFile();
			this.InHeaderFile = false;
			OpenStringWriter();
			WriteResources(program.Resources, false);
			OpenNamespace();
			foreach (CiClass klass in program.Classes) {
				WriteConstructor(klass);
				WriteMethods(klass);
			}
			WriteResources(program.Resources, true);
			CloseNamespace();
			if (this.StringReplace) {
				Include("string");
				Include("string_view");
			}
			CreateImplementationFile(program, ".hpp");
			if (this.UsingStringViewLiterals)
				WriteLine("using namespace std::string_view_literals;");
			if (this.StringReplace) {
				WriteNewLine();
				WriteLine("static std::string CiString_replace(std::string_view s, std::string_view oldValue, std::string_view newValue)");
				OpenBlock();
				WriteLine("std::string result;");
				WriteLine("result.reserve(s.size());");
				WriteLine("for (std::string_view::size_type i = 0;;) {");
				WriteLine("\tauto j = s.find(oldValue, i);");
				WriteLine("\tif (j == std::string::npos) {");
				WriteLine("\t\tresult.append(s, i);");
				WriteLine("\t\treturn result;");
				WriteLine("\t}");
				WriteLine("\tresult.append(s, i, j - i);");
				WriteLine("\tresult.append(newValue);");
				WriteLine("\ti = j + oldValue.size();");
				WriteCharLine('}');
				CloseBlock();
			}
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenCs : GenTyped
	{

		protected override string GetTargetName() => "C++";

		protected override void StartDocLine()
		{
			Write("/// ");
		}

		protected override void WriteDocPara(CiDocPara para, bool many)
		{
			if (many) {
				WriteNewLine();
				Write("/// <para>");
			}
			foreach (CiDocInline inline in para.Children) {
				switch (inline) {
				case CiDocText text:
					WriteXmlDoc(text.Text);
					break;
				case CiDocCode code:
					switch (code.Text) {
					case "true":
					case "false":
					case "null":
						Write("<see langword=\"");
						Write(code.Text);
						Write("\" />");
						break;
					default:
						Write("<c>");
						WriteXmlDoc(code.Text);
						Write("</c>");
						break;
					}
					break;
				case CiDocLine _:
					WriteNewLine();
					StartDocLine();
					break;
				default:
					throw new NotImplementedException();
				}
			}
			if (many)
				Write("</para>");
		}

		protected override void WriteDocList(CiDocList list)
		{
			WriteNewLine();
			WriteLine("/// <list type=\"bullet\">");
			foreach (CiDocPara item in list.Items) {
				Write("/// <item>");
				WriteDocPara(item, false);
				WriteLine("</item>");
			}
			Write("/// </list>");
		}

		protected override void WriteDoc(CiCodeDoc doc)
		{
			if (doc == null)
				return;
			Write("/// <summary>");
			WriteDocPara(doc.Summary, false);
			WriteLine("</summary>");
			if (doc.Details.Count > 0) {
				Write("/// <remarks>");
				if (doc.Details.Count == 1)
					WriteDocBlock(doc.Details[0], false);
				else {
					foreach (CiDocBlock block in doc.Details)
						WriteDocBlock(block, true);
				}
				WriteLine("</remarks>");
			}
		}

		protected override void WriteName(CiSymbol symbol)
		{
			if (symbol is CiConst konst && konst.InMethod != null)
				Write(konst.InMethod.Name);
			Write(symbol.Name);
			switch (symbol.Name) {
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
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override int GetLiteralChars() => 65536;

		void WriteVisibility(CiVisibility visibility)
		{
			switch (visibility) {
			case CiVisibility.Private:
				break;
			case CiVisibility.Internal:
				Write("internal ");
				break;
			case CiVisibility.Protected:
				Write("protected ");
				break;
			case CiVisibility.Public:
				Write("public ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		void WriteCallType(CiCallType callType, string sealedString)
		{
			switch (callType) {
			case CiCallType.Static:
				Write("static ");
				break;
			case CiCallType.Normal:
				break;
			case CiCallType.Abstract:
				Write("abstract ");
				break;
			case CiCallType.Virtual:
				Write("virtual ");
				break;
			case CiCallType.Override:
				Write("override ");
				break;
			case CiCallType.Sealed:
				Write(sealedString);
				break;
			}
		}

		void WriteElementType(CiType elementType)
		{
			Include("System.Collections.Generic");
			WriteChar('<');
			WriteType(elementType, false);
			WriteChar('>');
		}

		protected override void WriteType(CiType type, bool promote)
		{
			switch (type) {
			case CiIntegerType _:
				switch (GetTypeId(type, promote)) {
				case CiId.SByteRange:
					Write("sbyte");
					break;
				case CiId.ByteRange:
					Write("byte");
					break;
				case CiId.ShortRange:
					Write("short");
					break;
				case CiId.UShortRange:
					Write("ushort");
					break;
				case CiId.IntType:
					Write("int");
					break;
				case CiId.LongType:
					Write("long");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			case CiClassType klass:
				switch (klass.Class.Id) {
				case CiId.StringClass:
					Write("string");
					break;
				case CiId.ArrayPtrClass:
				case CiId.ArrayStorageClass:
					WriteType(klass.GetElementType(), false);
					Write("[]");
					break;
				case CiId.ListClass:
				case CiId.QueueClass:
				case CiId.StackClass:
				case CiId.HashSetClass:
				case CiId.SortedSetClass:
					Write(klass.Class.Name);
					WriteElementType(klass.GetElementType());
					break;
				case CiId.DictionaryClass:
				case CiId.SortedDictionaryClass:
					Include("System.Collections.Generic");
					Write(klass.Class.Name);
					WriteChar('<');
					WriteType(klass.GetKeyType(), false);
					Write(", ");
					WriteType(klass.GetValueType(), false);
					WriteChar('>');
					break;
				case CiId.OrderedDictionaryClass:
					Include("System.Collections.Specialized");
					Write("OrderedDictionary");
					break;
				case CiId.TextWriterClass:
				case CiId.StringWriterClass:
					Include("System.IO");
					Write(klass.Class.Name);
					break;
				case CiId.RegexClass:
				case CiId.MatchClass:
					Include("System.Text.RegularExpressions");
					Write(klass.Class.Name);
					break;
				case CiId.LockClass:
					Write("object");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteNewWithFields(CiReadWriteClassType type, CiAggregateInitializer init)
		{
			Write("new ");
			WriteType(type, false);
			WriteObjectLiteral(init, " = ");
		}

		protected override void WriteCoercedLiteral(CiType type, CiExpr literal)
		{
			if (literal is CiLiteralChar && type is CiRangeType range && range.Max <= 255)
				WriteStaticCast(type, literal);
			else
				literal.Accept(this, CiPriority.Argument);
		}

		protected override bool IsPromoted(CiExpr expr) => base.IsPromoted(expr) || expr is CiLiteralChar;

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			Write("$\"");
			foreach (CiInterpolatedPart part in expr.Parts) {
				WriteDoubling(part.Prefix, '{');
				WriteChar('{');
				part.Argument.Accept(this, CiPriority.Argument);
				if (part.WidthExpr != null) {
					WriteChar(',');
					VisitLiteralLong(part.Width);
				}
				if (part.Format != ' ') {
					WriteChar(':');
					WriteChar(part.Format);
					if (part.Precision >= 0)
						VisitLiteralLong(part.Precision);
				}
				WriteChar('}');
			}
			WriteDoubling(expr.Suffix, '{');
			WriteChar('"');
		}

		protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
		{
			Write("new ");
			WriteType(elementType.GetBaseType(), false);
			WriteChar('[');
			lengthExpr.Accept(this, CiPriority.Argument);
			WriteChar(']');
			while (elementType is CiClassType array && array.IsArray()) {
				Write("[]");
				elementType = array.GetElementType();
			}
		}

		protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
		{
			Write("new ");
			WriteType(klass, false);
			Write("()");
		}

		protected override bool HasInitCode(CiNamedValue def) => def.Type is CiArrayStorageType array && array.GetElementType() is CiStorageType;

		protected override void WriteInitCode(CiNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			CiArrayStorageType array = (CiArrayStorageType) def.Type;
			int nesting = 0;
			while (array.GetElementType() is CiArrayStorageType innerArray) {
				OpenLoop("int", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNewArray(innerArray.GetElementType(), innerArray.LengthExpr, CiPriority.Argument);
				WriteCharLine(';');
				array = innerArray;
			}
			if (array.GetElementType() is CiStorageType klass) {
				OpenLoop("int", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNew(klass, CiPriority.Argument);
				WriteCharLine(';');
			}
			while (--nesting >= 0)
				CloseBlock();
		}

		protected override void WriteResource(string name, int length)
		{
			Write("CiResource.");
			WriteResourceName(name);
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			WritePostfix(expr, ".Length");
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.ConsoleError:
				Include("System");
				Write("Console.Error");
				break;
			case CiId.MatchStart:
				WritePostfix(expr.Left, ".Index");
				break;
			case CiId.MatchEnd:
				if (parent > CiPriority.Add)
					WriteChar('(');
				WritePostfix(expr.Left, ".Index + ");
				WriteStringLength(expr.Left);
				if (parent > CiPriority.Add)
					WriteChar(')');
				break;
			case CiId.MathNaN:
			case CiId.MathNegativeInfinity:
			case CiId.MathPositiveInfinity:
				Write("float.");
				Write(expr.Symbol.Name);
				break;
			default:
				if (expr.Symbol.Parent is CiForeach forEach && forEach.Collection.Type is CiClassType dict && dict.Class.Id == CiId.OrderedDictionaryClass) {
					if (parent == CiPriority.Primary)
						WriteChar('(');
					CiVar element = forEach.GetVar();
					if (expr.Symbol == element) {
						WriteStaticCastType(dict.GetKeyType());
						WriteName(element);
						Write(".Key");
					}
					else {
						WriteStaticCastType(dict.GetValueType());
						WriteName(element);
						Write(".Value");
					}
					if (parent == CiPriority.Primary)
						WriteChar(')');
				}
				else
					base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case CiId.IntTryParse:
			case CiId.LongTryParse:
			case CiId.DoubleTryParse:
				Write(obj.Type.Name);
				Write(".TryParse(");
				args[0].Accept(this, CiPriority.Argument);
				if (args.Count == 2) {
					if (!(args[1] is CiLiteralLong radix) || radix.Value != 16)
						NotSupported(args[1], "Radix");
					Include("System.Globalization");
					Write(", NumberStyles.HexNumber, null");
				}
				Write(", out ");
				obj.Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.StringIndexOf:
			case CiId.StringLastIndexOf:
				obj.Accept(this, CiPriority.Primary);
				WriteChar('.');
				Write(method.Name);
				WriteChar('(');
				int c = GetOneAscii(args[0]);
				if (c >= 0)
					VisitLiteralChar(c);
				else
					args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ArrayBinarySearchAll:
			case CiId.ArrayBinarySearchPart:
				Include("System");
				Write("Array.BinarySearch(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				if (args.Count == 3) {
					args[1].Accept(this, CiPriority.Argument);
					Write(", ");
					args[2].Accept(this, CiPriority.Argument);
					Write(", ");
				}
				WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
				WriteChar(')');
				break;
			case CiId.ArrayCopyTo:
				Include("System");
				Write("Array.Copy(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteArgs(method, args);
				WriteChar(')');
				break;
			case CiId.ArrayFillAll:
			case CiId.ArrayFillPart:
				Include("System");
				if (args[0] is CiLiteral literal && literal.IsDefaultValue()) {
					Write("Array.Clear(");
					obj.Accept(this, CiPriority.Argument);
					if (args.Count == 1) {
						Write(", 0, ");
						WriteArrayStorageLength(obj);
					}
				}
				else {
					Write("Array.Fill(");
					obj.Accept(this, CiPriority.Argument);
					Write(", ");
					WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
				}
				if (args.Count == 3) {
					Write(", ");
					args[1].Accept(this, CiPriority.Argument);
					Write(", ");
					args[2].Accept(this, CiPriority.Argument);
				}
				WriteChar(')');
				break;
			case CiId.ArraySortAll:
				Include("System");
				WriteCall("Array.Sort", obj);
				break;
			case CiId.ArraySortPart:
				Include("System");
				WriteCall("Array.Sort", obj, args[0], args[1]);
				break;
			case CiId.ListAdd:
				WriteListAdd(obj, "Add", args);
				break;
			case CiId.ListAll:
				Include("System.Linq");
				WriteMethodCall(obj, "All", args[0]);
				break;
			case CiId.ListAny:
				Include("System.Linq");
				WriteMethodCall(obj, "Any", args[0]);
				break;
			case CiId.ListInsert:
				WriteListInsert(obj, "Insert", args);
				break;
			case CiId.ListLast:
				WritePostfix(obj, "[^1]");
				break;
			case CiId.ListSortPart:
				WritePostfix(obj, ".Sort(");
				WriteArgs(method, args);
				Write(", null)");
				break;
			case CiId.DictionaryAdd:
				WritePostfix(obj, ".Add(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteNewStorage(obj.Type.AsClassType().GetValueType());
				WriteChar(')');
				break;
			case CiId.OrderedDictionaryContainsKey:
				WriteMethodCall(obj, "Contains", args[0]);
				break;
			case CiId.TextWriterWrite:
			case CiId.TextWriterWriteLine:
			case CiId.ConsoleWrite:
			case CiId.ConsoleWriteLine:
				Include("System");
				obj.Accept(this, CiPriority.Primary);
				WriteChar('.');
				Write(method.Name);
				WriteChar('(');
				if (args.Count != 0) {
					if (args[0] is CiLiteralChar) {
						Write("(int) ");
						args[0].Accept(this, CiPriority.Primary);
					}
					else
						args[0].Accept(this, CiPriority.Argument);
				}
				WriteChar(')');
				break;
			case CiId.StringWriterClear:
				WritePostfix(obj, ".GetStringBuilder().Clear()");
				break;
			case CiId.TextWriterWriteChar:
				WritePostfix(obj, ".Write(");
				if (args[0] is CiLiteralChar)
					args[0].Accept(this, CiPriority.Argument);
				else {
					Write("(char) ");
					args[0].Accept(this, CiPriority.Primary);
				}
				WriteChar(')');
				break;
			case CiId.TextWriterWriteCodePoint:
				WritePostfix(obj, ".Write(");
				if (args[0] is CiLiteralChar literalChar && literalChar.Value < 65536)
					args[0].Accept(this, CiPriority.Argument);
				else {
					Include("System.Text");
					WriteCall("new Rune", args[0]);
				}
				WriteChar(')');
				break;
			case CiId.EnvironmentGetEnvironmentVariable:
				Include("System");
				obj.Accept(this, CiPriority.Primary);
				WriteChar('.');
				Write(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.UTF8GetByteCount:
				Include("System.Text");
				Write("Encoding.UTF8.GetByteCount(");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.UTF8GetBytes:
				Include("System.Text");
				Write("Encoding.UTF8.GetBytes(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", 0, ");
				WritePostfix(args[0], ".Length, ");
				args[1].Accept(this, CiPriority.Argument);
				Write(", ");
				args[2].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.UTF8GetString:
				Include("System.Text");
				Write("Encoding.UTF8.GetString");
				WriteArgsInParentheses(method, args);
				break;
			case CiId.RegexCompile:
				Include("System.Text.RegularExpressions");
				Write("new Regex");
				WriteArgsInParentheses(method, args);
				break;
			case CiId.RegexEscape:
			case CiId.RegexIsMatchStr:
			case CiId.RegexIsMatchRegex:
				Include("System.Text.RegularExpressions");
				obj.Accept(this, CiPriority.Primary);
				WriteChar('.');
				Write(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MatchFindStr:
				Include("System.Text.RegularExpressions");
				WriteChar('(');
				obj.Accept(this, CiPriority.Assign);
				Write(" = Regex.Match");
				WriteArgsInParentheses(method, args);
				Write(").Success");
				break;
			case CiId.MatchFindRegex:
				Include("System.Text.RegularExpressions");
				WriteChar('(');
				obj.Accept(this, CiPriority.Assign);
				Write(" = ");
				WriteMethodCall(args[1], "Match", args[0]);
				Write(").Success");
				break;
			case CiId.MatchGetCapture:
				WritePostfix(obj, ".Groups[");
				args[0].Accept(this, CiPriority.Argument);
				Write("].Value");
				break;
			case CiId.MathMethod:
			case CiId.MathAbs:
			case CiId.MathCeiling:
			case CiId.MathClamp:
			case CiId.MathFusedMultiplyAdd:
			case CiId.MathLog2:
			case CiId.MathMaxInt:
			case CiId.MathMaxDouble:
			case CiId.MathMinInt:
			case CiId.MathMinDouble:
			case CiId.MathRound:
			case CiId.MathTruncate:
				Include("System");
				Write("Math.");
				Write(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathIsFinite:
			case CiId.MathIsInfinity:
			case CiId.MathIsNaN:
				Write("double.");
				WriteCall(method.Name, args[0]);
				break;
			default:
				if (obj != null) {
					obj.Accept(this, CiPriority.Primary);
					WriteChar('.');
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			}
		}

		void WriteOrderedDictionaryIndexing(CiBinaryExpr expr)
		{
			if (expr.Right.Type.Id == CiId.IntType || expr.Right.Type is CiRangeType) {
				WritePostfix(expr.Left, "[(object) ");
				expr.Right.Accept(this, CiPriority.Primary);
				WriteChar(']');
			}
			else
				base.WriteIndexingExpr(expr, CiPriority.And);
		}

		protected override void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
		{
			if (expr.Left.Type is CiClassType dict && dict.Class.Id == CiId.OrderedDictionaryClass) {
				if (parent == CiPriority.Primary)
					WriteChar('(');
				WriteStaticCastType(expr.Type);
				WriteOrderedDictionaryIndexing(expr);
				if (parent == CiPriority.Primary)
					WriteChar(')');
			}
			else
				base.WriteIndexingExpr(expr, parent);
		}

		protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
		{
			if (expr.Left is CiBinaryExpr indexing && indexing.Op == CiToken.LeftBracket && indexing.Left.Type is CiClassType dict && dict.Class.Id == CiId.OrderedDictionaryClass) {
				WriteOrderedDictionaryIndexing(indexing);
				Write(" = ");
				WriteAssignRight(expr);
			}
			else
				base.WriteAssign(expr, parent);
		}

		public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.AndAssign:
			case CiToken.OrAssign:
			case CiToken.XorAssign:
				if (parent > CiPriority.Assign)
					WriteChar('(');
				expr.Left.Accept(this, CiPriority.Assign);
				WriteChar(' ');
				Write(expr.GetOpString());
				WriteChar(' ');
				WriteAssignRight(expr);
				if (parent > CiPriority.Assign)
					WriteChar(')');
				break;
			default:
				base.VisitBinaryExpr(expr, parent);
				break;
			}
		}

		public override void VisitLambdaExpr(CiLambdaExpr expr)
		{
			WriteName(expr.First);
			Write(" => ");
			expr.Body.Accept(this, CiPriority.Statement);
		}

		protected override void DefineObjectLiteralTemporary(CiUnaryExpr expr)
		{
		}

		protected override void DefineIsVar(CiBinaryExpr binary)
		{
		}

		protected override void WriteAssert(CiAssert statement)
		{
			if (statement.CompletesNormally()) {
				Include("System.Diagnostics");
				Write("Debug.Assert(");
				statement.Cond.Accept(this, CiPriority.Argument);
				if (statement.Message != null) {
					Write(", ");
					statement.Message.Accept(this, CiPriority.Argument);
				}
			}
			else {
				Include("System");
				Write("throw new NotImplementedException(");
				if (statement.Message != null)
					statement.Message.Accept(this, CiPriority.Argument);
			}
			WriteLine(");");
		}

		public override void VisitForeach(CiForeach statement)
		{
			Write("foreach (");
			if (statement.Collection.Type is CiClassType dict && dict.Class.TypeParameterCount == 2) {
				if (dict.Class.Id == CiId.OrderedDictionaryClass) {
					Include("System.Collections");
					Write("DictionaryEntry ");
					WriteName(statement.GetVar());
				}
				else {
					WriteChar('(');
					WriteTypeAndName(statement.GetVar());
					Write(", ");
					WriteTypeAndName(statement.GetValueVar());
					WriteChar(')');
				}
			}
			else
				WriteTypeAndName(statement.GetVar());
			Write(" in ");
			statement.Collection.Accept(this, CiPriority.Argument);
			WriteChar(')');
			WriteChild(statement.Body);
		}

		public override void VisitLock(CiLock statement)
		{
			Write("lock (");
			statement.Lock.Accept(this, CiPriority.Argument);
			WriteChar(')');
			WriteChild(statement.Body);
		}

		public override void VisitThrow(CiThrow statement)
		{
			Include("System");
			Write("throw new Exception(");
			statement.Message.Accept(this, CiPriority.Argument);
			WriteLine(");");
		}

		protected override void WriteEnum(CiEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			if (enu is CiEnumFlags) {
				Include("System");
				WriteLine("[Flags]");
			}
			WritePublic(enu);
			Write("enum ");
			WriteLine(enu.Name);
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			CloseBlock();
		}

		protected override void WriteRegexOptionsEnum(CiProgram program)
		{
			if (program.RegexOptionsEnum)
				Include("System.Text.RegularExpressions");
		}

		protected override void WriteConst(CiConst konst)
		{
			WriteNewLine();
			WriteDoc(konst.Documentation);
			WriteVisibility(konst.Visibility);
			Write(konst.Type is CiArrayStorageType ? "static readonly " : "const ");
			WriteTypeAndName(konst);
			Write(" = ");
			WriteCoercedExpr(konst.Type, konst.Value);
			WriteCharLine(';');
		}

		protected override void WriteField(CiField field)
		{
			WriteNewLine();
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			if (field.Type.IsFinal() && !field.IsAssignableStorage())
				Write("readonly ");
			WriteVar(field);
			WriteCharLine(';');
		}

		protected override void WriteParameterDoc(CiVar param, bool first)
		{
			Write("/// <param name=\"");
			WriteName(param);
			Write("\">");
			WriteDocPara(param.Documentation.Summary, false);
			WriteLine("</param>");
		}

		protected override void WriteMethod(CiMethod method)
		{
			if (method.Id == CiId.ClassToString && method.CallType == CiCallType.Abstract)
				return;
			WriteNewLine();
			WriteDoc(method.Documentation);
			WriteParametersDoc(method);
			WriteVisibility(method.Visibility);
			if (method.Id == CiId.ClassToString)
				Write("override ");
			else
				WriteCallType(method.CallType, "sealed override ");
			WriteTypeAndName(method);
			WriteParameters(method, true);
			if (method.Body is CiReturn ret) {
				Write(" => ");
				WriteCoerced(method.Type, ret.Value, CiPriority.Argument);
				WriteCharLine(';');
			}
			else
				WriteBody(method);
		}

		protected override void WriteClass(CiClass klass, CiProgram program)
		{
			WriteNewLine();
			WriteDoc(klass.Documentation);
			WritePublic(klass);
			WriteCallType(klass.CallType, "sealed ");
			OpenClass(klass, "", " : ");
			if (NeedsConstructor(klass)) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				else
					Write("internal ");
				Write(klass.Name);
				WriteLine("()");
				OpenBlock();
				WriteConstructorBody(klass);
				CloseBlock();
			}
			WriteMembers(klass, true);
			CloseBlock();
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			WriteNewLine();
			WriteLine("internal static class CiResource");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				Write("internal static readonly byte[] ");
				WriteResourceName(name);
				WriteLine(" = {");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" };");
			}
			CloseBlock();
		}

		public override void WriteProgram(CiProgram program)
		{
			OpenStringWriter();
			if (this.Namespace.Length != 0) {
				Write("namespace ");
				WriteLine(this.Namespace);
				OpenBlock();
			}
			WriteTopLevelNatives(program);
			WriteTypes(program);
			if (program.Resources.Count > 0)
				WriteResources(program.Resources);
			if (this.Namespace.Length != 0)
				CloseBlock();
			CreateOutputFile();
			WriteIncludes("using ", ";");
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenD : GenCCppD
	{

		bool HasListInsert;

		bool HasListRemoveAt;

		bool HasQueueDequeue;

		bool HasStackPop;

		bool HasSortedDictionaryInsert;

		bool HasSortedDictionaryFind;

		protected override string GetTargetName() => "D";

		protected override void StartDocLine()
		{
			Write("/// ");
		}

		protected override void WriteDocPara(CiDocPara para, bool many)
		{
			if (many) {
				WriteNewLine();
				StartDocLine();
			}
			foreach (CiDocInline inline in para.Children) {
				switch (inline) {
				case CiDocText text:
					WriteXmlDoc(text.Text);
					break;
				case CiDocCode code:
					WriteChar('`');
					WriteXmlDoc(code.Text);
					WriteChar('`');
					break;
				case CiDocLine _:
					WriteNewLine();
					StartDocLine();
					break;
				default:
					throw new NotImplementedException();
				}
			}
			if (many)
				WriteNewLine();
		}

		protected override void WriteParameterDoc(CiVar param, bool first)
		{
			if (first) {
				StartDocLine();
				WriteLine("Params:");
			}
			StartDocLine();
			WriteName(param);
			Write(" = ");
			WriteDocPara(param.Documentation.Summary, false);
			WriteNewLine();
		}

		protected override void WriteDocList(CiDocList list)
		{
			WriteLine("///");
			WriteLine("/// <ul>");
			foreach (CiDocPara item in list.Items) {
				Write("/// <li>");
				WriteDocPara(item, false);
				WriteLine("</li>");
			}
			WriteLine("/// </ul>");
			Write("///");
		}

		protected override void WriteDoc(CiCodeDoc doc)
		{
			if (doc == null)
				return;
			StartDocLine();
			WriteDocPara(doc.Summary, false);
			WriteNewLine();
			if (doc.Details.Count > 0) {
				StartDocLine();
				if (doc.Details.Count == 1)
					WriteDocBlock(doc.Details[0], false);
				else {
					foreach (CiDocBlock block in doc.Details)
						WriteDocBlock(block, true);
				}
				WriteNewLine();
			}
		}

		protected override void WriteName(CiSymbol symbol)
		{
			if (symbol is CiContainerType) {
				Write(symbol.Name);
				return;
			}
			WriteCamelCase(symbol.Name);
			switch (symbol.Name) {
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
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override int GetLiteralChars() => 65536;

		void WriteVisibility(CiVisibility visibility)
		{
			switch (visibility) {
			case CiVisibility.Private:
				Write("private ");
				break;
			case CiVisibility.Internal:
			case CiVisibility.Public:
				break;
			case CiVisibility.Protected:
				Write("protected ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		void WriteCallType(CiCallType callType, string sealedString)
		{
			switch (callType) {
			case CiCallType.Static:
				Write("static ");
				break;
			case CiCallType.Normal:
				break;
			case CiCallType.Abstract:
				Write("abstract ");
				break;
			case CiCallType.Virtual:
				break;
			case CiCallType.Override:
				Write("override ");
				break;
			case CiCallType.Sealed:
				Write(sealedString);
				break;
			}
		}

		static bool IsCreateWithNew(CiType type)
		{
			if (type is CiClassType klass) {
				if (klass is CiStorageType stg)
					return stg.Class.Id != CiId.ArrayStorageClass;
				return true;
			}
			return false;
		}

		static bool IsStructPtr(CiType type) => type is CiClassType ptr && (ptr.Class.Id == CiId.ListClass || ptr.Class.Id == CiId.StackClass || ptr.Class.Id == CiId.QueueClass);

		void WriteElementType(CiType type)
		{
			WriteType(type, false);
			if (IsStructPtr(type))
				WriteChar('*');
		}

		protected override void WriteType(CiType type, bool promote)
		{
			switch (type) {
			case CiIntegerType _:
				switch (GetTypeId(type, promote)) {
				case CiId.SByteRange:
					Write("byte");
					break;
				case CiId.ByteRange:
					Write("ubyte");
					break;
				case CiId.ShortRange:
					Write("short");
					break;
				case CiId.UShortRange:
					Write("ushort");
					break;
				case CiId.IntType:
					Write("int");
					break;
				case CiId.LongType:
					Write("long");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			case CiClassType klass:
				switch (klass.Class.Id) {
				case CiId.StringClass:
					Write("string");
					break;
				case CiId.ArrayStorageClass:
				case CiId.ArrayPtrClass:
					WriteElementType(klass.GetElementType());
					WriteChar('[');
					if (klass is CiArrayStorageType arrayStorage)
						VisitLiteralLong(arrayStorage.Length);
					WriteChar(']');
					break;
				case CiId.ListClass:
				case CiId.StackClass:
					Include("std.container.array");
					Write("Array!(");
					WriteElementType(klass.GetElementType());
					WriteChar(')');
					break;
				case CiId.QueueClass:
					Include("std.container.dlist");
					Write("DList!(");
					WriteElementType(klass.GetElementType());
					WriteChar(')');
					break;
				case CiId.HashSetClass:
					Write("bool[");
					WriteElementType(klass.GetElementType());
					WriteChar(']');
					break;
				case CiId.DictionaryClass:
					WriteElementType(klass.GetValueType());
					WriteChar('[');
					WriteType(klass.GetKeyType(), false);
					WriteChar(']');
					break;
				case CiId.SortedSetClass:
					Include("std.container.rbtree");
					Write("RedBlackTree!(");
					WriteElementType(klass.GetElementType());
					WriteChar(')');
					break;
				case CiId.SortedDictionaryClass:
					Include("std.container.rbtree");
					Include("std.typecons");
					Write("RedBlackTree!(Tuple!(");
					WriteElementType(klass.GetKeyType());
					Write(", ");
					WriteElementType(klass.GetValueType());
					Write("), \"a[0] < b[0]\")");
					break;
				case CiId.OrderedDictionaryClass:
					Include("std.typecons");
					Write("Tuple!(Array!(");
					WriteElementType(klass.GetValueType());
					Write("), \"data\", size_t[");
					WriteType(klass.GetKeyType(), false);
					Write("], \"dict\")");
					break;
				case CiId.TextWriterClass:
					Include("std.stdio");
					Write("File");
					break;
				case CiId.RegexClass:
					Include("std.regex");
					Write("Regex!char");
					break;
				case CiId.MatchClass:
					Include("std.regex");
					Write("Captures!string");
					break;
				case CiId.LockClass:
					Write("Object");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteTypeAndName(CiNamedValue value)
		{
			WriteType(value.Type, true);
			if (IsStructPtr(value.Type))
				WriteChar('*');
			WriteChar(' ');
			WriteName(value);
		}

		public override void VisitAggregateInitializer(CiAggregateInitializer expr)
		{
			Write("[ ");
			WriteCoercedLiterals(expr.Type.AsClassType().GetElementType(), expr.Items);
			Write(" ]");
		}

		protected override void WriteStaticCast(CiType type, CiExpr expr)
		{
			Write("cast(");
			WriteType(type, false);
			Write(")(");
			GetStaticCastInner(type, expr).Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			Include("std.format");
			Write("format(");
			WritePrintf(expr, false);
		}

		protected override void WriteStorageInit(CiNamedValue def)
		{
			Write(" = ");
			WriteNewStorage(def.Type);
		}

		protected override void WriteVarInit(CiNamedValue def)
		{
			if (def.Type is CiArrayStorageType)
				return;
			base.WriteVarInit(def);
		}

		protected override bool HasInitCode(CiNamedValue def)
		{
			if (def.Value != null && !(def.Value is CiLiteral))
				return true;
			CiType type = def.Type;
			if (type is CiArrayStorageType array) {
				while (array.GetElementType() is CiArrayStorageType innerArray)
					array = innerArray;
				type = array.GetElementType();
			}
			return type is CiStorageType;
		}

		protected override void WriteInitField(CiField field)
		{
			WriteInitCode(field);
		}

		protected override void WriteInitCode(CiNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			if (def.Type is CiArrayStorageType array) {
				int nesting = 0;
				while (array.GetElementType() is CiArrayStorageType innerArray) {
					OpenLoop("size_t", nesting++, array.Length);
					array = innerArray;
				}
				if (array.GetElementType() is CiStorageType klass) {
					OpenLoop("size_t", nesting++, array.Length);
					WriteArrayElement(def, nesting);
					Write(" = ");
					WriteNew(klass, CiPriority.Argument);
					WriteCharLine(';');
				}
				while (--nesting >= 0)
					CloseBlock();
			}
			else {
				if (def.Type is CiReadWriteClassType klass) {
					switch (klass.Class.Id) {
					case CiId.StringClass:
					case CiId.ArrayStorageClass:
					case CiId.ArrayPtrClass:
					case CiId.DictionaryClass:
					case CiId.HashSetClass:
					case CiId.SortedDictionaryClass:
					case CiId.OrderedDictionaryClass:
					case CiId.LockClass:
						break;
					case CiId.RegexClass:
					case CiId.MatchClass:
						break;
					default:
						if (def.Parent is CiClass) {
							WriteName(def);
							Write(" = ");
							if (def.Value == null)
								WriteNew(klass, CiPriority.Argument);
							else
								WriteCoercedExpr(def.Type, def.Value);
							WriteCharLine(';');
						}
						base.WriteInitCode(def);
						break;
					}
				}
			}
		}

		protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
		{
			Write("new ");
			WriteType(elementType, false);
			WriteChar('[');
			lengthExpr.Accept(this, CiPriority.Argument);
			WriteChar(']');
		}

		void WriteStaticInitializer(CiType type)
		{
			WriteChar('(');
			WriteType(type, false);
			Write(").init");
		}

		protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
		{
			if (IsCreateWithNew(klass)) {
				Write("new ");
				WriteType(klass, false);
			}
			else
				WriteStaticInitializer(klass);
		}

		protected override void WriteResource(string name, int length)
		{
			Write("CiResource.");
			WriteResourceName(name);
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			WritePostfix(expr, ".length");
		}

		void WriteClassReference(CiExpr expr, CiPriority priority = CiPriority.Primary)
		{
			if (IsStructPtr(expr.Type)) {
				Write("(*");
				expr.Accept(this, priority);
				WriteChar(')');
			}
			else
				expr.Accept(this, priority);
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.ConsoleError:
				Write("stderr");
				break;
			case CiId.ListCount:
			case CiId.StackCount:
			case CiId.HashSetCount:
			case CiId.DictionaryCount:
			case CiId.SortedSetCount:
			case CiId.SortedDictionaryCount:
				WriteStringLength(expr.Left);
				break;
			case CiId.QueueCount:
				Include("std.range");
				WriteClassReference(expr.Left);
				Write("[].walkLength");
				break;
			case CiId.MatchStart:
				WritePostfix(expr.Left, ".pre.length");
				break;
			case CiId.MatchEnd:
				if (parent > CiPriority.Add)
					WriteChar('(');
				WritePostfix(expr.Left, ".pre.length + ");
				WritePostfix(expr.Left, ".hit.length");
				if (parent > CiPriority.Add)
					WriteChar(')');
				break;
			case CiId.MatchLength:
				WritePostfix(expr.Left, ".hit.length");
				break;
			case CiId.MatchValue:
				WritePostfix(expr.Left, ".hit");
				break;
			case CiId.MathNaN:
				Write("double.nan");
				break;
			case CiId.MathNegativeInfinity:
				Write("-double.infinity");
				break;
			case CiId.MathPositiveInfinity:
				Write("double.infinity");
				break;
			default:
				Debug.Assert(!(expr.Symbol.Parent is CiForeach forEach && forEach.Collection.Type is CiClassType dict && dict.Class.Id == CiId.OrderedDictionaryClass));
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		void WriteWrite(List<CiExpr> args, bool newLine)
		{
			Include("std.stdio");
			if (args.Count == 0)
				Write("writeln()");
			else if (args[0] is CiInterpolatedString interpolated) {
				Write(newLine ? "writefln(" : "writef(");
				WritePrintf(interpolated, false);
			}
			else {
				Write(newLine ? "writeln(" : "write(");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
		}

		void WriteInsertedArg(CiType type, List<CiExpr> args, int index = 0)
		{
			if (args.Count <= index) {
				CiReadWriteClassType klass = (CiReadWriteClassType) type;
				WriteNew(klass, CiPriority.Argument);
			}
			else
				WriteCoercedExpr(type, args[index]);
			WriteChar(')');
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case CiId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case CiId.IntTryParse:
			case CiId.LongTryParse:
			case CiId.DoubleTryParse:
				Include("std.conv");
				Write("() { try { ");
				WritePostfix(obj, " = ");
				WritePostfix(args[0], ".to!");
				Write(obj.Type.Name);
				if (args.Count == 2) {
					WriteChar('(');
					args[1].Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
				Write("; return true; } catch (ConvException e) return false; }()");
				break;
			case CiId.StringContains:
				Include("std.algorithm");
				WritePostfix(obj, ".canFind");
				WriteArgsInParentheses(method, args);
				break;
			case CiId.StringEndsWith:
				Include("std.string");
				WriteMethodCall(obj, "endsWith", args[0]);
				break;
			case CiId.StringIndexOf:
				Include("std.string");
				WriteMethodCall(obj, "indexOf", args[0]);
				break;
			case CiId.StringLastIndexOf:
				Include("std.string");
				WriteMethodCall(obj, "lastIndexOf", args[0]);
				break;
			case CiId.StringReplace:
				Include("std.string");
				WriteMethodCall(obj, "replace", args[0], args[1]);
				break;
			case CiId.StringStartsWith:
				Include("std.string");
				WriteMethodCall(obj, "startsWith", args[0]);
				break;
			case CiId.StringSubstring:
				obj.Accept(this, CiPriority.Primary);
				WriteChar('[');
				WritePostfix(args[0], " .. $]");
				if (args.Count > 1) {
					Write("[0 .. ");
					args[1].Accept(this, CiPriority.Argument);
					WriteChar(']');
				}
				break;
			case CiId.ArrayBinarySearchAll:
			case CiId.ArrayBinarySearchPart:
				Include("std.range");
				Write("() { size_t cibegin = ");
				if (args.Count == 3)
					args[1].Accept(this, CiPriority.Argument);
				else
					WriteChar('0');
				Write("; auto cisearch = ");
				WriteClassReference(obj);
				WriteChar('[');
				if (args.Count == 3) {
					Write("cibegin .. cibegin + ");
					args[2].Accept(this, CiPriority.Add);
				}
				Write("].assumeSorted.trisect(");
				WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
				Write("); return cisearch[1].length ? cibegin + cisearch[0].length : -1; }()");
				break;
			case CiId.ArrayCopyTo:
			case CiId.ListCopyTo:
				Include("std.algorithm");
				WriteClassReference(obj);
				WriteChar('[');
				args[0].Accept(this, CiPriority.Argument);
				Write(" .. $][0 .. ");
				args[3].Accept(this, CiPriority.Argument);
				Write("].copy(");
				args[1].Accept(this, CiPriority.Argument);
				WriteChar('[');
				args[2].Accept(this, CiPriority.Argument);
				Write(" .. $])");
				break;
			case CiId.ArrayFillAll:
			case CiId.ArrayFillPart:
				Include("std.algorithm");
				WriteClassReference(obj);
				WriteChar('[');
				if (args.Count == 3) {
					args[1].Accept(this, CiPriority.Argument);
					Write(" .. $][0 .. ");
					args[2].Accept(this, CiPriority.Argument);
				}
				Write("].fill(");
				WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
				WriteChar(')');
				break;
			case CiId.ArraySortAll:
			case CiId.ArraySortPart:
			case CiId.ListSortAll:
			case CiId.ListSortPart:
				Include("std.algorithm");
				WriteClassReference(obj);
				WriteChar('[');
				if (args.Count == 2) {
					args[0].Accept(this, CiPriority.Argument);
					Write(" .. $][0 .. ");
					args[1].Accept(this, CiPriority.Argument);
				}
				Write("].sort");
				break;
			case CiId.ListAdd:
			case CiId.QueueEnqueue:
				WritePostfix(obj, ".insertBack(");
				WriteInsertedArg(obj.Type.AsClassType().GetElementType(), args);
				break;
			case CiId.ListAddRange:
				WriteClassReference(obj);
				Write(" ~= ");
				WriteClassReference(args[0]);
				Write("[]");
				break;
			case CiId.ListAll:
				Include("std.algorithm");
				WriteClassReference(obj);
				Write("[].all!(");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ListAny:
				Include("std.algorithm");
				WriteClassReference(obj);
				Write("[].any!(");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ListContains:
				Include("std.algorithm");
				WriteClassReference(obj);
				Write("[].canFind(");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ListInsert:
				this.HasListInsert = true;
				WritePostfix(obj, ".insertInPlace(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteInsertedArg(obj.Type.AsClassType().GetElementType(), args, 1);
				break;
			case CiId.ListLast:
				WritePostfix(obj, ".back");
				break;
			case CiId.ListRemoveAt:
			case CiId.ListRemoveRange:
				this.HasListRemoveAt = true;
				WritePostfix(obj, ".removeAt");
				WriteArgsInParentheses(method, args);
				break;
			case CiId.ListIndexOf:
				Include("std.algorithm");
				WriteClassReference(obj);
				Write("[].countUntil");
				WriteArgsInParentheses(method, args);
				break;
			case CiId.QueueDequeue:
				this.HasQueueDequeue = true;
				Include("std.container.dlist");
				WriteClassReference(obj);
				Write(".dequeue()");
				break;
			case CiId.QueuePeek:
				WritePostfix(obj, ".front");
				break;
			case CiId.StackPeek:
				WritePostfix(obj, ".back");
				break;
			case CiId.StackPush:
				WriteClassReference(obj);
				Write(" ~= ");
				args[0].Accept(this, CiPriority.Assign);
				break;
			case CiId.StackPop:
				this.HasStackPop = true;
				WriteClassReference(obj);
				Write(".pop()");
				break;
			case CiId.HashSetAdd:
				WritePostfix(obj, ".require(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", true)");
				break;
			case CiId.HashSetClear:
			case CiId.DictionaryClear:
				WritePostfix(obj, ".clear()");
				break;
			case CiId.HashSetContains:
			case CiId.DictionaryContainsKey:
				WriteChar('(');
				args[0].Accept(this, CiPriority.Rel);
				Write(" in ");
				obj.Accept(this, CiPriority.Primary);
				WriteChar(')');
				break;
			case CiId.SortedSetAdd:
				WritePostfix(obj, ".insert(");
				WriteInsertedArg(obj.Type.AsClassType().GetElementType(), args, 0);
				break;
			case CiId.SortedSetRemove:
				WritePostfix(obj, ".removeKey");
				WriteArgsInParentheses(method, args);
				break;
			case CiId.DictionaryAdd:
				if (obj.Type.AsClassType().Class.Id == CiId.SortedDictionaryClass) {
					this.HasSortedDictionaryInsert = true;
					WritePostfix(obj, ".replace(");
				}
				else
					WritePostfix(obj, ".require(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteInsertedArg(obj.Type.AsClassType().GetValueType(), args, 1);
				break;
			case CiId.SortedDictionaryContainsKey:
				Write("tuple(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteStaticInitializer(obj.Type.AsClassType().GetValueType());
				Write(") in ");
				WriteClassReference(obj);
				break;
			case CiId.SortedDictionaryRemove:
				WriteClassReference(obj);
				Write(".removeKey(tuple(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteStaticInitializer(obj.Type.AsClassType().GetValueType());
				Write("))");
				break;
			case CiId.TextWriterWrite:
			case CiId.TextWriterWriteLine:
				WritePostfix(obj, ".");
				WriteWrite(args, method.Id == CiId.TextWriterWriteLine);
				break;
			case CiId.TextWriterWriteChar:
				WritePostfix(obj, ".write(");
				if (args[0] is CiLiteralChar)
					Write("cast(char) ");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ConsoleWrite:
			case CiId.ConsoleWriteLine:
				WriteWrite(args, method.Id == CiId.ConsoleWriteLine);
				break;
			case CiId.EnvironmentGetEnvironmentVariable:
				Include("std.process");
				Write("environment.get");
				WriteArgsInParentheses(method, args);
				break;
			case CiId.UTF8GetByteCount:
				WritePostfix(args[0], ".length");
				break;
			case CiId.UTF8GetBytes:
				Include("std.string");
				Include("std.algorithm");
				WritePostfix(args[0], ".representation.copy(");
				WritePostfix(args[1], "[");
				args[2].Accept(this, CiPriority.Argument);
				Write(" .. $])");
				break;
			case CiId.UTF8GetString:
				Write("cast(string) (");
				WritePostfix(args[0], "[");
				args[1].Accept(this, CiPriority.Argument);
				Write(" .. $][0 .. ");
				args[2].Accept(this, CiPriority.Argument);
				Write("])");
				break;
			case CiId.RegexCompile:
				Include("std.regex");
				Write("regex(");
				args[0].Accept(this, CiPriority.Argument);
				WriteRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
				WriteChar(')');
				break;
			case CiId.RegexEscape:
				Include("std.regex");
				Include("std.conv");
				args[0].Accept(this, CiPriority.Argument);
				Write(".escaper.to!string");
				break;
			case CiId.RegexIsMatchRegex:
				Include("std.regex");
				WritePostfix(args[0], ".matchFirst(");
				(args.Count > 1 ? args[1] : obj).Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.RegexIsMatchStr:
				Include("std.regex");
				WritePostfix(args[0], ".matchFirst(");
				if (GetRegexOptions(args) != RegexOptions.None)
					Write("regex(");
				(args.Count > 1 ? args[1] : obj).Accept(this, CiPriority.Argument);
				WriteRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
				WriteChar(')');
				break;
			case CiId.MatchFindStr:
				Include("std.regex");
				WriteChar('(');
				obj.Accept(this, CiPriority.Assign);
				Write(" = ");
				args[0].Accept(this, CiPriority.Primary);
				Write(".matchFirst(");
				if (GetRegexOptions(args) != RegexOptions.None)
					Write("regex(");
				args[1].Accept(this, CiPriority.Argument);
				WriteRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
				Write("))");
				break;
			case CiId.MatchFindRegex:
				Include("std.regex");
				WriteChar('(');
				obj.Accept(this, CiPriority.Assign);
				Write(" = ");
				WriteMethodCall(args[0], "matchFirst", args[1]);
				WriteChar(')');
				break;
			case CiId.MatchGetCapture:
				WriteIndexing(obj, args[0]);
				break;
			case CiId.MathMethod:
			case CiId.MathAbs:
			case CiId.MathIsFinite:
			case CiId.MathIsInfinity:
			case CiId.MathIsNaN:
			case CiId.MathLog2:
			case CiId.MathRound:
				Include("std.math");
				WriteCamelCase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathCeiling:
				Include("std.math");
				WriteCall("ceil", args[0]);
				break;
			case CiId.MathClamp:
			case CiId.MathMaxInt:
			case CiId.MathMaxDouble:
			case CiId.MathMinInt:
			case CiId.MathMinDouble:
				Include("std.algorithm");
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathFusedMultiplyAdd:
				Include("std.math");
				WriteCall("fma", args[0], args[1], args[2]);
				break;
			case CiId.MathTruncate:
				Include("std.math");
				WriteCall("trunc", args[0]);
				break;
			default:
				if (obj != null) {
					if (IsReferenceTo(obj, CiId.BasePtr))
						Write("super.");
					else {
						WriteClassReference(obj);
						WriteChar('.');
					}
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			}
		}

		protected override void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
		{
			WriteClassReference(expr.Left);
			CiClassType klass = (CiClassType) expr.Left.Type;
			switch (klass.Class.Id) {
			case CiId.ArrayPtrClass:
			case CiId.ArrayStorageClass:
			case CiId.DictionaryClass:
			case CiId.ListClass:
				WriteChar('[');
				expr.Right.Accept(this, CiPriority.Argument);
				WriteChar(']');
				break;
			case CiId.SortedDictionaryClass:
				Debug.Assert(parent != CiPriority.Assign);
				this.HasSortedDictionaryFind = true;
				Include("std.container.rbtree");
				Include("std.typecons");
				Write(".find(");
				WriteStronglyCoerced(klass.GetKeyType(), expr.Right);
				WriteChar(')');
				return;
			default:
				throw new NotImplementedException();
			}
		}

		static bool IsIsComparable(CiExpr expr) => expr is CiLiteralNull || (expr.Type is CiClassType klass && klass.Class.Id == CiId.ArrayPtrClass);

		protected override void WriteEqual(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			if (IsIsComparable(left) || IsIsComparable(right))
				WriteEqualExpr(left, right, parent, not ? " !is " : " is ");
			else
				base.WriteEqual(left, right, parent, not);
		}

		protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
		{
			if (expr.Left is CiBinaryExpr indexing && indexing.Op == CiToken.LeftBracket && indexing.Left.Type is CiClassType dict) {
				switch (dict.Class.Id) {
				case CiId.SortedDictionaryClass:
					this.HasSortedDictionaryInsert = true;
					WritePostfix(indexing.Left, ".replace(");
					indexing.Right.Accept(this, CiPriority.Argument);
					Write(", ");
					WriteNotPromoted(expr.Type, expr.Right);
					WriteChar(')');
					return;
				default:
					break;
				}
			}
			base.WriteAssign(expr, parent);
		}

		void WriteIsVar(CiExpr expr, CiVar def, CiPriority parent)
		{
			CiPriority thisPriority = def.Name == "_" ? CiPriority.Primary : CiPriority.Assign;
			if (parent > thisPriority)
				WriteChar('(');
			if (def.Name != "_") {
				WriteName(def);
				Write(" = ");
			}
			Write("cast(");
			WriteType(def.Type, true);
			Write(") ");
			expr.Accept(this, CiPriority.Primary);
			if (parent > thisPriority)
				WriteChar(')');
		}

		public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Is:
				if (parent >= CiPriority.Or && parent <= CiPriority.Mul)
					parent = CiPriority.Primary;
				if (parent > CiPriority.Equality)
					WriteChar('(');
				switch (expr.Right) {
				case CiSymbolReference symbol:
					Write("cast(");
					Write(symbol.Symbol.Name);
					Write(") ");
					expr.Left.Accept(this, CiPriority.Primary);
					break;
				case CiVar def:
					WriteIsVar(expr.Left, def, CiPriority.Equality);
					break;
				default:
					throw new NotImplementedException();
				}
				Write(" !is null");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				return;
			case CiToken.Plus:
				if (expr.Type.Id == CiId.StringStorageType) {
					expr.Left.Accept(this, CiPriority.Assign);
					Write(" ~ ");
					expr.Right.Accept(this, CiPriority.Assign);
					return;
				}
				break;
			case CiToken.AddAssign:
				if (expr.Left.Type.Id == CiId.StringStorageType) {
					expr.Left.Accept(this, CiPriority.Assign);
					Write(" ~= ");
					WriteAssignRight(expr);
					return;
				}
				break;
			default:
				break;
			}
			base.VisitBinaryExpr(expr, parent);
		}

		public override void VisitLambdaExpr(CiLambdaExpr expr)
		{
			WriteName(expr.First);
			Write(" => ");
			expr.Body.Accept(this, CiPriority.Statement);
		}

		protected override void WriteAssert(CiAssert statement)
		{
			Write("assert(");
			statement.Cond.Accept(this, CiPriority.Argument);
			if (statement.Message != null) {
				Write(", ");
				statement.Message.Accept(this, CiPriority.Argument);
			}
			WriteLine(");");
		}

		public override void VisitForeach(CiForeach statement)
		{
			Write("foreach (");
			if (statement.Collection.Type is CiClassType dict && dict.Class.TypeParameterCount == 2) {
				WriteTypeAndName(statement.GetVar());
				Write(", ");
				WriteTypeAndName(statement.GetValueVar());
			}
			else
				WriteTypeAndName(statement.GetVar());
			Write("; ");
			WriteClassReference(statement.Collection);
			if (statement.Collection.Type is CiClassType set && set.Class.Id == CiId.HashSetClass)
				Write(".byKey");
			WriteChar(')');
			WriteChild(statement.Body);
		}

		public override void VisitLock(CiLock statement)
		{
			Write("synchronized (");
			statement.Lock.Accept(this, CiPriority.Argument);
			WriteChar(')');
			WriteChild(statement.Body);
		}

		protected override void WriteSwitchCaseTypeVar(CiExpr value)
		{
			DefineVar(value);
		}

		protected override void WriteSwitchCaseCond(CiSwitch statement, CiExpr value, CiPriority parent)
		{
			if (value is CiVar def) {
				WriteIsVar(statement.Value, def, CiPriority.Equality);
				Write(" !is null");
			}
			else
				base.WriteSwitchCaseCond(statement, value, parent);
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			WriteTemporaries(statement.Value);
			if (statement.IsTypeMatching() || statement.HasWhen())
				WriteSwitchAsIfsWithGoto(statement);
			else {
				StartSwitch(statement);
				WriteLine("default:");
				this.Indent++;
				if (statement.DefaultBody.Count > 0)
					WriteSwitchCaseBody(statement.DefaultBody);
				else
					WriteLine("assert(false);");
				this.Indent--;
				WriteCharLine('}');
			}
		}

		public override void VisitThrow(CiThrow statement)
		{
			Include("std.exception");
			Write("throw new Exception(");
			statement.Message.Accept(this, CiPriority.Argument);
			WriteLine(");");
		}

		protected override void WriteEnum(CiEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			WritePublic(enu);
			Write("enum ");
			Write(enu.Name);
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			CloseBlock();
		}

		protected override void WriteConst(CiConst konst)
		{
			WriteDoc(konst.Documentation);
			Write("static ");
			WriteTypeAndName(konst);
			Write(" = ");
			WriteCoercedExpr(konst.Type, konst.Value);
			WriteCharLine(';');
		}

		protected override void WriteField(CiField field)
		{
			WriteNewLine();
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			WriteTypeAndName(field);
			if (field.Value is CiLiteral) {
				Write(" = ");
				WriteCoercedExpr(field.Type, field.Value);
			}
			WriteCharLine(';');
		}

		protected override void WriteMethod(CiMethod method)
		{
			if (method.Id == CiId.ClassToString && method.CallType == CiCallType.Abstract)
				return;
			WriteNewLine();
			WriteDoc(method.Documentation);
			WriteParametersDoc(method);
			WriteVisibility(method.Visibility);
			if (method.Id == CiId.ClassToString)
				Write("override ");
			else
				WriteCallType(method.CallType, "final override ");
			WriteTypeAndName(method);
			WriteParameters(method, true);
			WriteBody(method);
		}

		protected override void WriteClass(CiClass klass, CiProgram program)
		{
			WriteNewLine();
			WriteDoc(klass.Documentation);
			if (klass.CallType == CiCallType.Sealed)
				Write("final ");
			OpenClass(klass, "", " : ");
			if (NeedsConstructor(klass)) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				else
					Write("private ");
				WriteLine("this()");
				OpenBlock();
				WriteConstructorBody(klass);
				CloseBlock();
			}
			for (CiSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (!(symbol is CiMember))
					continue;
				switch (symbol) {
				case CiConst konst:
					WriteConst(konst);
					break;
				case CiField field:
					WriteField(field);
					break;
				case CiMethod method:
					WriteMethod(method);
					this.CurrentTemporaries.Clear();
					break;
				case CiVar _:
					break;
				default:
					throw new NotImplementedException();
				}
			}
			CloseBlock();
		}

		static bool IsLong(CiSymbolReference expr)
		{
			switch (expr.Symbol.Id) {
			case CiId.ArrayLength:
			case CiId.StringLength:
			case CiId.ListCount:
				return true;
			default:
				return false;
			}
		}

		protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
		{
			if (type is CiRangeType)
				WriteStaticCast(type, expr);
			else if (type is CiIntegerType && expr is CiSymbolReference symref && IsLong(symref))
				WriteStaticCast(type, expr);
			else if (type is CiFloatingType && !(expr.Type is CiFloatingType))
				WriteStaticCast(type, expr);
			else if (type is CiClassType && !(type is CiArrayStorageType) && expr.Type is CiArrayStorageType) {
				base.WriteCoercedInternal(type, expr, CiPriority.Primary);
				Write("[]");
			}
			else
				base.WriteCoercedInternal(type, expr, parent);
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			WriteNewLine();
			WriteLine("private static struct CiResource");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				Write("private static ubyte[] ");
				WriteResourceName(name);
				WriteLine(" = [");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" ];");
			}
			CloseBlock();
		}

		public override void WriteProgram(CiProgram program)
		{
			this.HasListInsert = false;
			this.HasListRemoveAt = false;
			this.HasQueueDequeue = false;
			this.HasStackPop = false;
			this.HasSortedDictionaryInsert = false;
			this.HasSortedDictionaryFind = false;
			OpenStringWriter();
			if (this.Namespace.Length != 0) {
				Write("struct ");
				WriteLine(this.Namespace);
				OpenBlock();
				WriteLine("static:");
			}
			WriteTopLevelNatives(program);
			WriteTypes(program);
			if (program.Resources.Count > 0)
				WriteResources(program.Resources);
			if (this.Namespace.Length != 0)
				CloseBlock();
			CreateOutputFile();
			if (this.HasListInsert || this.HasListRemoveAt || this.HasStackPop)
				Include("std.container.array");
			if (this.HasSortedDictionaryInsert) {
				Include("std.container.rbtree");
				Include("std.typecons");
			}
			WriteIncludes("import ", ";");
			if (this.HasListInsert) {
				WriteNewLine();
				WriteLine("private void insertInPlace(T, U...)(Array!T* arr, size_t pos, auto ref U stuff)");
				OpenBlock();
				WriteLine("arr.insertAfter((*arr)[0 .. pos], stuff);");
				CloseBlock();
			}
			if (this.HasListRemoveAt) {
				WriteNewLine();
				WriteLine("private void removeAt(T)(Array!T* arr, size_t pos, size_t count = 1)");
				OpenBlock();
				WriteLine("arr.linearRemove((*arr)[pos .. pos + count]);");
				CloseBlock();
			}
			if (this.HasQueueDequeue) {
				WriteNewLine();
				WriteLine("private T dequeue(T)(ref DList!T q)");
				OpenBlock();
				WriteLine("scope(exit) q.removeFront(); return q.front;");
				CloseBlock();
			}
			if (this.HasStackPop) {
				WriteNewLine();
				WriteLine("private T pop(T)(ref Array!T stack)");
				OpenBlock();
				WriteLine("scope(exit) stack.removeBack(); return stack.back;");
				CloseBlock();
			}
			if (this.HasSortedDictionaryFind) {
				WriteNewLine();
				WriteLine("private U find(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key)");
				OpenBlock();
				WriteLine("return dict.equalRange(tuple(key, U.init)).front[1];");
				CloseBlock();
			}
			if (this.HasSortedDictionaryInsert) {
				WriteNewLine();
				WriteLine("private void replace(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key, lazy U value)");
				OpenBlock();
				WriteLine("dict.removeKey(tuple(key, U.init));");
				WriteLine("dict.insert(tuple(key, value));");
				CloseBlock();
			}
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenJava : GenTyped
	{

		int SwitchCaseDiscards;

		protected override string GetTargetName() => "Java";

		public override void VisitLiteralLong(long value)
		{
			base.VisitLiteralLong(value);
			if (value < -2147483648 || value > 2147483647)
				WriteChar('L');
		}

		protected override int GetLiteralChars() => 65536;

		protected override void WritePrintfWidth(CiInterpolatedPart part)
		{
			if (part.Precision >= 0 && part.Argument.Type is CiIntegerType) {
				WriteChar('0');
				VisitLiteralLong(part.Precision);
			}
			else
				base.WritePrintfWidth(part);
		}

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			if (expr.Suffix.Length == 0 && expr.Parts.Count == 1 && expr.Parts[0].Prefix.Length == 0 && expr.Parts[0].WidthExpr == null && expr.Parts[0].Format == ' ') {
				CiExpr arg = expr.Parts[0].Argument;
				switch (arg.Type.Id) {
				case CiId.LongType:
					Write("Long");
					break;
				case CiId.FloatType:
					Write("Float");
					break;
				case CiId.DoubleType:
				case CiId.FloatIntType:
					Write("Double");
					break;
				case CiId.StringPtrType:
				case CiId.StringStorageType:
					arg.Accept(this, parent);
					return;
				default:
					if (arg.Type is CiIntegerType)
						Write("Integer");
					else if (arg.Type is CiClassType) {
						WritePostfix(arg, ".toString()");
						return;
					}
					else
						throw new NotImplementedException();
					break;
				}
				WriteCall(".toString", arg);
			}
			else {
				Write("String.format(");
				WritePrintf(expr, false);
			}
		}

		void WriteCamelCaseNotKeyword(string name)
		{
			WriteCamelCase(name);
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
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override void WriteName(CiSymbol symbol)
		{
			switch (symbol) {
			case CiContainerType _:
				Write(symbol.Name);
				break;
			case CiConst konst:
				if (konst.InMethod != null) {
					WriteUppercaseWithUnderscores(konst.InMethod.Name);
					WriteChar('_');
				}
				WriteUppercaseWithUnderscores(symbol.Name);
				break;
			case CiVar _:
				if (symbol.Parent is CiForeach forEach && forEach.Count() == 2) {
					CiVar element = forEach.GetVar();
					WriteCamelCaseNotKeyword(element.Name);
					Write(symbol == element ? ".getKey()" : ".getValue()");
				}
				else
					WriteCamelCaseNotKeyword(symbol.Name);
				break;
			case CiMember _:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		void WriteVisibility(CiVisibility visibility)
		{
			switch (visibility) {
			case CiVisibility.Private:
				Write("private ");
				break;
			case CiVisibility.Internal:
				break;
			case CiVisibility.Protected:
				Write("protected ");
				break;
			case CiVisibility.Public:
				Write("public ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override CiId GetTypeId(CiType type, bool promote)
		{
			CiId id = base.GetTypeId(type, promote);
			switch (id) {
			case CiId.ByteRange:
				return CiId.SByteRange;
			case CiId.UShortRange:
				return CiId.IntType;
			default:
				return id;
			}
		}

		void WriteCollectionType(string name, CiType elementType)
		{
			Include("java.util." + name);
			Write(name);
			WriteChar('<');
			WriteJavaType(elementType, false, true);
			WriteChar('>');
		}

		void WriteDictType(string name, CiClassType dict)
		{
			Write(name);
			WriteChar('<');
			WriteJavaType(dict.GetKeyType(), false, true);
			Write(", ");
			WriteJavaType(dict.GetValueType(), false, true);
			WriteChar('>');
		}

		void WriteJavaType(CiType type, bool promote, bool needClass)
		{
			switch (type) {
			case CiNumericType _:
				switch (GetTypeId(type, promote)) {
				case CiId.SByteRange:
					Write(needClass ? "Byte" : "byte");
					break;
				case CiId.ShortRange:
					Write(needClass ? "Short" : "short");
					break;
				case CiId.IntType:
					Write(needClass ? "Integer" : "int");
					break;
				case CiId.LongType:
					Write(needClass ? "Long" : "long");
					break;
				case CiId.FloatType:
					Write(needClass ? "Float" : "float");
					break;
				case CiId.DoubleType:
					Write(needClass ? "Double" : "double");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			case CiEnum enu:
				Write(enu.Id == CiId.BoolType ? needClass ? "Boolean" : "boolean" : needClass ? "Integer" : "int");
				break;
			case CiClassType klass:
				switch (klass.Class.Id) {
				case CiId.StringClass:
					Write("String");
					break;
				case CiId.ArrayPtrClass:
				case CiId.ArrayStorageClass:
					WriteType(klass.GetElementType(), false);
					Write("[]");
					break;
				case CiId.ListClass:
					WriteCollectionType("ArrayList", klass.GetElementType());
					break;
				case CiId.QueueClass:
					WriteCollectionType("ArrayDeque", klass.GetElementType());
					break;
				case CiId.StackClass:
					WriteCollectionType("Stack", klass.GetElementType());
					break;
				case CiId.HashSetClass:
					WriteCollectionType("HashSet", klass.GetElementType());
					break;
				case CiId.SortedSetClass:
					WriteCollectionType("TreeSet", klass.GetElementType());
					break;
				case CiId.DictionaryClass:
					Include("java.util.HashMap");
					WriteDictType("HashMap", klass);
					break;
				case CiId.SortedDictionaryClass:
					Include("java.util.TreeMap");
					WriteDictType("TreeMap", klass);
					break;
				case CiId.OrderedDictionaryClass:
					Include("java.util.LinkedHashMap");
					WriteDictType("LinkedHashMap", klass);
					break;
				case CiId.RegexClass:
					Include("java.util.regex.Pattern");
					Write("Pattern");
					break;
				case CiId.MatchClass:
					Include("java.util.regex.Matcher");
					Write("Matcher");
					break;
				case CiId.LockClass:
					Write("Object");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteType(CiType type, bool promote)
		{
			WriteJavaType(type, promote, false);
		}

		protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
		{
			Write("new ");
			WriteType(klass, false);
			Write("()");
		}

		protected override void WriteResource(string name, int length)
		{
			Write("CiResource.getByteArray(");
			VisitLiteralString(name);
			Write(", ");
			VisitLiteralLong(length);
			WriteChar(')');
		}

		static bool IsUnsignedByte(CiType type) => type.Id == CiId.ByteRange && type is CiRangeType range && range.Max > 127;

		static bool IsUnsignedByteIndexing(CiExpr expr) => expr.IsIndexing() && IsUnsignedByte(expr.Type);

		void WriteIndexingInternal(CiBinaryExpr expr)
		{
			if (expr.Left.Type.IsArray())
				base.WriteIndexingExpr(expr, CiPriority.And);
			else
				WriteMethodCall(expr.Left, "get", expr.Right);
		}

		public override void VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
		{
			if ((expr.Op == CiToken.Increment || expr.Op == CiToken.Decrement) && IsUnsignedByteIndexing(expr.Inner)) {
				if (parent > CiPriority.And)
					WriteChar('(');
				Write(expr.Op == CiToken.Increment ? "++" : "--");
				CiBinaryExpr indexing = (CiBinaryExpr) expr.Inner;
				WriteIndexingInternal(indexing);
				if (parent != CiPriority.Statement)
					Write(" & 0xff");
				if (parent > CiPriority.And)
					WriteChar(')');
			}
			else
				base.VisitPrefixExpr(expr, parent);
		}

		public override void VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent)
		{
			if ((expr.Op == CiToken.Increment || expr.Op == CiToken.Decrement) && IsUnsignedByteIndexing(expr.Inner)) {
				if (parent > CiPriority.And)
					WriteChar('(');
				CiBinaryExpr indexing = (CiBinaryExpr) expr.Inner;
				WriteIndexingInternal(indexing);
				Write(expr.Op == CiToken.Increment ? "++" : "--");
				if (parent != CiPriority.Statement)
					Write(" & 0xff");
				if (parent > CiPriority.And)
					WriteChar(')');
			}
			else
				base.VisitPostfixExpr(expr, parent);
		}

		void WriteSByteLiteral(CiLiteralLong literal)
		{
			VisitLiteralLong((literal.Value ^ 128) - 128);
		}

		protected override void WriteEqual(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			if ((left.Type is CiStringType && right.Type.Id != CiId.NullType) || (right.Type is CiStringType && left.Type.Id != CiId.NullType)) {
				if (not)
					WriteChar('!');
				WriteMethodCall(left, "equals", right);
			}
			else if (IsUnsignedByteIndexing(left) && right is CiLiteralLong rightLiteral && rightLiteral.Type.Id == CiId.ByteRange) {
				if (parent > CiPriority.Equality)
					WriteChar('(');
				CiBinaryExpr indexing = (CiBinaryExpr) left;
				WriteIndexingInternal(indexing);
				Write(GetEqOp(not));
				WriteSByteLiteral(rightLiteral);
				if (parent > CiPriority.Equality)
					WriteChar(')');
			}
			else
				base.WriteEqual(left, right, parent, not);
		}

		protected override void WriteCoercedLiteral(CiType type, CiExpr expr)
		{
			if (IsUnsignedByte(type)) {
				CiLiteralLong literal = (CiLiteralLong) expr;
				WriteSByteLiteral(literal);
			}
			else
				expr.Accept(this, CiPriority.Argument);
		}

		protected override void WriteAnd(CiBinaryExpr expr, CiPriority parent)
		{
			if (IsUnsignedByteIndexing(expr.Left) && expr.Right is CiLiteralLong rightLiteral) {
				if (parent > CiPriority.CondAnd && parent != CiPriority.And)
					WriteChar('(');
				CiBinaryExpr indexing = (CiBinaryExpr) expr.Left;
				WriteIndexingInternal(indexing);
				Write(" & ");
				VisitLiteralLong(255 & rightLiteral.Value);
				if (parent > CiPriority.CondAnd && parent != CiPriority.And)
					WriteChar(')');
			}
			else
				base.WriteAnd(expr, parent);
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			WritePostfix(expr, ".length()");
		}

		protected override void WriteCharAt(CiBinaryExpr expr)
		{
			WriteMethodCall(expr.Left, "charAt", expr.Right);
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.ConsoleError:
				Write("System.err");
				break;
			case CiId.ListCount:
			case CiId.QueueCount:
			case CiId.StackCount:
			case CiId.HashSetCount:
			case CiId.SortedSetCount:
			case CiId.DictionaryCount:
			case CiId.SortedDictionaryCount:
			case CiId.OrderedDictionaryCount:
				expr.Left.Accept(this, CiPriority.Primary);
				WriteMemberOp(expr.Left, expr);
				Write("size()");
				break;
			case CiId.MathNaN:
				Write("Float.NaN");
				break;
			case CiId.MathNegativeInfinity:
				Write("Float.NEGATIVE_INFINITY");
				break;
			case CiId.MathPositiveInfinity:
				Write("Float.POSITIVE_INFINITY");
				break;
			default:
				if (!WriteJavaMatchProperty(expr, parent))
					base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		void WriteArrayBinarySearchFill(CiExpr obj, string method, List<CiExpr> args)
		{
			Include("java.util.Arrays");
			Write("Arrays.");
			Write(method);
			WriteChar('(');
			obj.Accept(this, CiPriority.Argument);
			Write(", ");
			if (args.Count == 3) {
				WriteStartEnd(args[1], args[2]);
				Write(", ");
			}
			WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
			WriteChar(')');
		}

		void WriteWrite(CiMethod method, List<CiExpr> args, bool newLine)
		{
			if (args.Count == 1 && args[0] is CiInterpolatedString interpolated) {
				Write(".format(");
				WritePrintf(interpolated, newLine);
			}
			else {
				Write(".print");
				if (newLine)
					Write("ln");
				WriteArgsInParentheses(method, args);
			}
		}

		void WriteCompileRegex(List<CiExpr> args, int argIndex)
		{
			Include("java.util.regex.Pattern");
			Write("Pattern.compile(");
			args[argIndex].Accept(this, CiPriority.Argument);
			WriteRegexOptions(args, ", ", " | ", "", "Pattern.CASE_INSENSITIVE", "Pattern.MULTILINE", "Pattern.DOTALL");
			WriteChar(')');
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.None:
			case CiId.ClassToString:
			case CiId.StringContains:
			case CiId.StringEndsWith:
			case CiId.StringIndexOf:
			case CiId.StringLastIndexOf:
			case CiId.StringReplace:
			case CiId.StringStartsWith:
			case CiId.ListClear:
			case CiId.ListContains:
			case CiId.ListIndexOf:
			case CiId.QueueClear:
			case CiId.StackClear:
			case CiId.StackPeek:
			case CiId.StackPush:
			case CiId.StackPop:
			case CiId.HashSetAdd:
			case CiId.HashSetClear:
			case CiId.HashSetContains:
			case CiId.HashSetRemove:
			case CiId.SortedSetAdd:
			case CiId.SortedSetClear:
			case CiId.SortedSetContains:
			case CiId.SortedSetRemove:
			case CiId.DictionaryClear:
			case CiId.DictionaryContainsKey:
			case CiId.DictionaryRemove:
			case CiId.SortedDictionaryClear:
			case CiId.SortedDictionaryContainsKey:
			case CiId.SortedDictionaryRemove:
			case CiId.OrderedDictionaryClear:
			case CiId.OrderedDictionaryContainsKey:
			case CiId.OrderedDictionaryRemove:
			case CiId.StringWriterToString:
			case CiId.MathMethod:
			case CiId.MathAbs:
			case CiId.MathMaxInt:
			case CiId.MathMaxDouble:
			case CiId.MathMinInt:
			case CiId.MathMinDouble:
				if (obj != null) {
					if (IsReferenceTo(obj, CiId.BasePtr))
						Write("super");
					else
						obj.Accept(this, CiPriority.Primary);
					WriteChar('.');
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.EnumFromInt:
				args[0].Accept(this, parent);
				break;
			case CiId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case CiId.DoubleTryParse:
				Include("java.util.function.DoubleSupplier");
				Write("!Double.isNaN(");
				obj.Accept(this, CiPriority.Assign);
				Write(" = ((DoubleSupplier) () -> { try { return Double.parseDouble(");
				args[0].Accept(this, CiPriority.Argument);
				Write("); } catch (NumberFormatException e) { return Double.NaN; } }).getAsDouble())");
				break;
			case CiId.StringSubstring:
				WritePostfix(obj, ".substring(");
				args[0].Accept(this, CiPriority.Argument);
				if (args.Count == 2) {
					Write(", ");
					WriteAdd(args[0], args[1]);
				}
				WriteChar(')');
				break;
			case CiId.ArrayBinarySearchAll:
			case CiId.ArrayBinarySearchPart:
				WriteArrayBinarySearchFill(obj, "binarySearch", args);
				break;
			case CiId.ArrayCopyTo:
				Write("System.arraycopy(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteArgs(method, args);
				WriteChar(')');
				break;
			case CiId.ArrayFillAll:
			case CiId.ArrayFillPart:
				WriteArrayBinarySearchFill(obj, "fill", args);
				break;
			case CiId.ArraySortAll:
				Include("java.util.Arrays");
				WriteCall("Arrays.sort", obj);
				break;
			case CiId.ArraySortPart:
				Include("java.util.Arrays");
				Write("Arrays.sort(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteStartEnd(args[0], args[1]);
				WriteChar(')');
				break;
			case CiId.ListAdd:
				WriteListAdd(obj, "add", args);
				break;
			case CiId.ListAddRange:
				WriteMethodCall(obj, "addAll", args[0]);
				break;
			case CiId.ListAll:
				WriteMethodCall(obj, "stream().allMatch", args[0]);
				break;
			case CiId.ListAny:
				WriteMethodCall(obj, "stream().anyMatch", args[0]);
				break;
			case CiId.ListCopyTo:
				Write("for (int _i = 0; _i < ");
				args[3].Accept(this, CiPriority.Rel);
				WriteLine("; _i++)");
				Write("\t");
				args[1].Accept(this, CiPriority.Primary);
				WriteChar('[');
				StartAdd(args[2]);
				Write("_i] = ");
				WritePostfix(obj, ".get(");
				StartAdd(args[0]);
				Write("_i)");
				break;
			case CiId.ListInsert:
				WriteListInsert(obj, "add", args);
				break;
			case CiId.ListLast:
				WritePostfix(obj, ".get(");
				WritePostfix(obj, ".size() - 1)");
				break;
			case CiId.ListRemoveAt:
				WriteMethodCall(obj, "remove", args[0]);
				break;
			case CiId.ListRemoveRange:
				WritePostfix(obj, ".subList(");
				WriteStartEnd(args[0], args[1]);
				Write(").clear()");
				break;
			case CiId.ListSortAll:
				WritePostfix(obj, ".sort(null)");
				break;
			case CiId.ListSortPart:
				WritePostfix(obj, ".subList(");
				WriteStartEnd(args[0], args[1]);
				Write(").sort(null)");
				break;
			case CiId.QueueDequeue:
				WritePostfix(obj, ".remove()");
				break;
			case CiId.QueueEnqueue:
				WriteMethodCall(obj, "add", args[0]);
				break;
			case CiId.QueuePeek:
				WritePostfix(obj, ".element()");
				break;
			case CiId.DictionaryAdd:
				WritePostfix(obj, ".put(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteNewStorage(obj.Type.AsClassType().GetValueType());
				WriteChar(')');
				break;
			case CiId.TextWriterWrite:
				obj.Accept(this, CiPriority.Primary);
				WriteWrite(method, args, false);
				break;
			case CiId.TextWriterWriteChar:
				WriteMethodCall(obj, "write", args[0]);
				break;
			case CiId.TextWriterWriteLine:
				obj.Accept(this, CiPriority.Primary);
				WriteWrite(method, args, true);
				break;
			case CiId.StringWriterClear:
				WritePostfix(obj, ".getBuffer().setLength(0)");
				break;
			case CiId.ConsoleWrite:
				Write("System.out");
				WriteWrite(method, args, false);
				break;
			case CiId.ConsoleWriteLine:
				Write("System.out");
				WriteWrite(method, args, true);
				break;
			case CiId.UTF8GetByteCount:
				Include("java.nio.charset.StandardCharsets");
				WritePostfix(args[0], ".getBytes(StandardCharsets.UTF_8).length");
				break;
			case CiId.UTF8GetBytes:
				Include("java.nio.ByteBuffer");
				Include("java.nio.CharBuffer");
				Include("java.nio.charset.StandardCharsets");
				Write("StandardCharsets.UTF_8.newEncoder().encode(CharBuffer.wrap(");
				args[0].Accept(this, CiPriority.Argument);
				Write("), ByteBuffer.wrap(");
				args[1].Accept(this, CiPriority.Argument);
				Write(", ");
				args[2].Accept(this, CiPriority.Argument);
				Write(", ");
				WritePostfix(args[1], ".length");
				if (!args[2].IsLiteralZero()) {
					Write(" - ");
					args[2].Accept(this, CiPriority.Mul);
				}
				Write("), true)");
				break;
			case CiId.UTF8GetString:
				Include("java.nio.charset.StandardCharsets");
				Write("new String(");
				WriteArgs(method, args);
				Write(", StandardCharsets.UTF_8)");
				break;
			case CiId.EnvironmentGetEnvironmentVariable:
				WriteCall("System.getenv", args[0]);
				break;
			case CiId.RegexCompile:
				WriteCompileRegex(args, 0);
				break;
			case CiId.RegexEscape:
				Include("java.util.regex.Pattern");
				WriteCall("Pattern.quote", args[0]);
				break;
			case CiId.RegexIsMatchStr:
				WriteCompileRegex(args, 1);
				WriteCall(".matcher", args[0]);
				Write(".find()");
				break;
			case CiId.RegexIsMatchRegex:
				WriteMethodCall(obj, "matcher", args[0]);
				Write(".find()");
				break;
			case CiId.MatchFindStr:
			case CiId.MatchFindRegex:
				WriteChar('(');
				obj.Accept(this, CiPriority.Assign);
				Write(" = ");
				if (method.Id == CiId.MatchFindStr)
					WriteCompileRegex(args, 1);
				else
					args[1].Accept(this, CiPriority.Primary);
				WriteCall(".matcher", args[0]);
				Write(").find()");
				break;
			case CiId.MatchGetCapture:
				WriteMethodCall(obj, "group", args[0]);
				break;
			case CiId.MathCeiling:
				WriteCall("Math.ceil", args[0]);
				break;
			case CiId.MathClamp:
				Write("Math.min(Math.max(");
				WriteClampAsMinMax(args);
				break;
			case CiId.MathFusedMultiplyAdd:
				WriteCall("Math.fma", args[0], args[1], args[2]);
				break;
			case CiId.MathIsFinite:
				WriteCall("Double.isFinite", args[0]);
				break;
			case CiId.MathIsInfinity:
				WriteCall("Double.isInfinite", args[0]);
				break;
			case CiId.MathIsNaN:
				WriteCall("Double.isNaN", args[0]);
				break;
			case CiId.MathLog2:
				if (parent > CiPriority.Mul)
					WriteChar('(');
				WriteCall("Math.log", args[0]);
				Write(" * 1.4426950408889635");
				if (parent > CiPriority.Mul)
					WriteChar(')');
				break;
			case CiId.MathRound:
				WriteCall("Math.rint", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
		{
			if (parent != CiPriority.Assign && IsUnsignedByte(expr.Type)) {
				if (parent > CiPriority.And)
					WriteChar('(');
				WriteIndexingInternal(expr);
				Write(" & 0xff");
				if (parent > CiPriority.And)
					WriteChar(')');
			}
			else
				WriteIndexingInternal(expr);
		}

		protected override bool IsPromoted(CiExpr expr) => base.IsPromoted(expr) || IsUnsignedByteIndexing(expr);

		protected override void WriteAssignRight(CiBinaryExpr expr)
		{
			if (!IsUnsignedByteIndexing(expr.Left) && expr.Right is CiBinaryExpr rightBinary && rightBinary.IsAssign() && IsUnsignedByte(expr.Right.Type)) {
				WriteChar('(');
				base.WriteAssignRight(expr);
				Write(") & 0xff");
			}
			else
				base.WriteAssignRight(expr);
		}

		protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
		{
			if (expr.Left is CiBinaryExpr indexing && indexing.Op == CiToken.LeftBracket && indexing.Left.Type is CiClassType klass && !klass.IsArray()) {
				WritePostfix(indexing.Left, klass.Class.Id == CiId.ListClass ? ".set(" : ".put(");
				indexing.Right.Accept(this, CiPriority.Argument);
				Write(", ");
				WriteNotPromoted(expr.Type, expr.Right);
				WriteChar(')');
			}
			else
				base.WriteAssign(expr, parent);
		}

		protected override string GetIsOperator() => " instanceof ";

		protected override void WriteVar(CiNamedValue def)
		{
			if (def.Type.IsFinal() && !def.IsAssignableStorage())
				Write("final ");
			base.WriteVar(def);
		}

		protected override bool HasInitCode(CiNamedValue def) => (def.Type is CiArrayStorageType && def.Type.GetStorageType() is CiStorageType) || base.HasInitCode(def);

		protected override void WriteInitCode(CiNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			if (def.Type is CiArrayStorageType array) {
				int nesting = 0;
				while (array.GetElementType() is CiArrayStorageType innerArray) {
					OpenLoop("int", nesting++, array.Length);
					array = innerArray;
				}
				OpenLoop("int", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				CiStorageType storage = (CiStorageType) array.GetElementType();
				WriteNew(storage, CiPriority.Argument);
				WriteCharLine(';');
				while (--nesting >= 0)
					CloseBlock();
			}
			else
				base.WriteInitCode(def);
		}

		public override void VisitLambdaExpr(CiLambdaExpr expr)
		{
			WriteName(expr.First);
			Write(" -> ");
			expr.Body.Accept(this, CiPriority.Statement);
		}

		protected override void DefineIsVar(CiBinaryExpr binary)
		{
		}

		protected override void WriteAssert(CiAssert statement)
		{
			if (statement.CompletesNormally()) {
				Write("assert ");
				statement.Cond.Accept(this, CiPriority.Argument);
				if (statement.Message != null) {
					Write(" : ");
					statement.Message.Accept(this, CiPriority.Argument);
				}
			}
			else {
				Write("throw new AssertionError(");
				if (statement.Message != null)
					statement.Message.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			WriteCharLine(';');
		}

		public override void VisitForeach(CiForeach statement)
		{
			Write("for (");
			CiClassType klass = (CiClassType) statement.Collection.Type;
			switch (klass.Class.Id) {
			case CiId.StringClass:
				Write("int _i = 0; _i < ");
				WriteStringLength(statement.Collection);
				Write("; _i++) ");
				OpenBlock();
				WriteTypeAndName(statement.GetVar());
				Write(" = ");
				statement.Collection.Accept(this, CiPriority.Primary);
				WriteLine(".charAt(_i);");
				FlattenBlock(statement.Body);
				CloseBlock();
				return;
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
			case CiId.OrderedDictionaryClass:
				Include("java.util.Map");
				WriteDictType("Map.Entry", klass);
				WriteChar(' ');
				Write(statement.GetVar().Name);
				Write(" : ");
				WritePostfix(statement.Collection, ".entrySet()");
				break;
			default:
				WriteTypeAndName(statement.GetVar());
				Write(" : ");
				statement.Collection.Accept(this, CiPriority.Argument);
				break;
			}
			WriteChar(')');
			WriteChild(statement.Body);
		}

		public override void VisitLock(CiLock statement)
		{
			Write("synchronized (");
			statement.Lock.Accept(this, CiPriority.Argument);
			WriteChar(')');
			WriteChild(statement.Body);
		}

		protected override void WriteSwitchValue(CiExpr expr)
		{
			if (IsUnsignedByteIndexing(expr)) {
				CiBinaryExpr indexing = (CiBinaryExpr) expr;
				WriteIndexingInternal(indexing);
			}
			else
				base.WriteSwitchValue(expr);
		}

		bool WriteSwitchCaseVar(CiExpr expr)
		{
			expr.Accept(this, CiPriority.Argument);
			if (expr is CiVar def && def.Name == "_") {
				VisitLiteralLong(this.SwitchCaseDiscards++);
				return true;
			}
			return false;
		}

		protected override void WriteSwitchCase(CiSwitch statement, CiCase kase)
		{
			if (statement.IsTypeMatching()) {
				foreach (CiExpr expr in kase.Values) {
					Write("case ");
					bool discard;
					if (expr is CiBinaryExpr when1) {
						discard = WriteSwitchCaseVar(when1.Left);
						Write(" when ");
						when1.Right.Accept(this, CiPriority.Argument);
					}
					else
						discard = WriteSwitchCaseVar(expr);
					WriteCharLine(':');
					this.Indent++;
					WriteSwitchCaseBody(kase.Body);
					this.Indent--;
					if (discard)
						this.SwitchCaseDiscards--;
				}
			}
			else
				base.WriteSwitchCase(statement, kase);
		}

		public override void VisitThrow(CiThrow statement)
		{
			Write("throw new Exception(");
			statement.Message.Accept(this, CiPriority.Argument);
			WriteLine(");");
		}

		void CreateJavaFile(string className)
		{
			CreateFile(this.OutputFile, className + ".java");
			if (this.Namespace.Length != 0) {
				Write("package ");
				Write(this.Namespace);
				WriteCharLine(';');
			}
		}

		public override void VisitEnumValue(CiConst konst, CiConst previous)
		{
			WriteDoc(konst.Documentation);
			Write("int ");
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			if (konst.Value is CiImplicitEnumValue imp)
				VisitLiteralLong(imp.Value);
			else
				konst.Value.Accept(this, CiPriority.Argument);
			WriteCharLine(';');
		}

		protected override void WriteEnum(CiEnum enu)
		{
			CreateJavaFile(enu.Name);
			WriteNewLine();
			WriteDoc(enu.Documentation);
			WritePublic(enu);
			Write("interface ");
			WriteLine(enu.Name);
			OpenBlock();
			enu.AcceptValues(this);
			CloseBlock();
			CloseFile();
		}

		void WriteSignature(CiMethod method, int paramCount)
		{
			WriteNewLine();
			WriteMethodDoc(method);
			WriteVisibility(method.Visibility);
			switch (method.CallType) {
			case CiCallType.Static:
				Write("static ");
				break;
			case CiCallType.Virtual:
				break;
			case CiCallType.Abstract:
				Write("abstract ");
				break;
			case CiCallType.Override:
				Write("@Override ");
				break;
			case CiCallType.Normal:
				if (method.Visibility != CiVisibility.Private)
					Write("final ");
				break;
			case CiCallType.Sealed:
				Write("final @Override ");
				break;
			default:
				throw new NotImplementedException();
			}
			WriteTypeAndName(method);
			WriteChar('(');
			CiVar param = method.Parameters.FirstParameter();
			for (int i = 0; i < paramCount; i++) {
				if (i > 0)
					Write(", ");
				WriteTypeAndName(param);
				param = param.NextParameter();
			}
			WriteChar(')');
			if (method.Throws)
				Write(" throws Exception");
		}

		void WriteOverloads(CiMethod method, int paramCount)
		{
			if (paramCount + 1 < method.Parameters.Count())
				WriteOverloads(method, paramCount + 1);
			WriteSignature(method, paramCount);
			WriteNewLine();
			OpenBlock();
			if (method.Type.Id != CiId.VoidType)
				Write("return ");
			WriteName(method);
			WriteChar('(');
			CiVar param = method.Parameters.FirstParameter();
			for (int i = 0; i < paramCount; i++) {
				WriteName(param);
				Write(", ");
				param = param.NextParameter();
			}
			param.Value.Accept(this, CiPriority.Argument);
			WriteLine(");");
			CloseBlock();
		}

		protected override void WriteConst(CiConst konst)
		{
			WriteNewLine();
			WriteDoc(konst.Documentation);
			WriteVisibility(konst.Visibility);
			Write("static final ");
			WriteTypeAndName(konst);
			Write(" = ");
			WriteCoercedExpr(konst.Type, konst.Value);
			WriteCharLine(';');
		}

		protected override void WriteField(CiField field)
		{
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			WriteVar(field);
			WriteCharLine(';');
		}

		protected override void WriteMethod(CiMethod method)
		{
			WriteSignature(method, method.Parameters.Count());
			WriteBody(method);
			int i = 0;
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (param.Value != null) {
					WriteOverloads(method, i);
					break;
				}
				i++;
			}
		}

		protected override void WriteClass(CiClass klass, CiProgram program)
		{
			OpenStringWriter();
			WriteDoc(klass.Documentation);
			WritePublic(klass);
			switch (klass.CallType) {
			case CiCallType.Normal:
				break;
			case CiCallType.Abstract:
				Write("abstract ");
				break;
			case CiCallType.Static:
			case CiCallType.Sealed:
				Write("final ");
				break;
			default:
				throw new NotImplementedException();
			}
			OpenClass(klass, "", " extends ");
			if (klass.CallType == CiCallType.Static) {
				Write("private ");
				Write(klass.Name);
				WriteLine("()");
				OpenBlock();
				CloseBlock();
			}
			else if (NeedsConstructor(klass)) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				Write(klass.Name);
				WriteLine("()");
				OpenBlock();
				WriteConstructorBody(klass);
				CloseBlock();
			}
			WriteMembers(klass, true);
			CloseBlock();
			CreateJavaFile(klass.Name);
			WriteTopLevelNatives(program);
			WriteIncludes("import ", ";");
			WriteNewLine();
			CloseStringWriter();
			CloseFile();
		}

		void WriteResources()
		{
			CreateJavaFile("CiResource");
			WriteLine("import java.io.DataInputStream;");
			WriteLine("import java.io.IOException;");
			WriteNewLine();
			Write("class CiResource");
			WriteNewLine();
			OpenBlock();
			WriteLine("static byte[] getByteArray(String name, int length)");
			OpenBlock();
			Write("DataInputStream dis = new DataInputStream(");
			WriteLine("CiResource.class.getResourceAsStream(name));");
			WriteLine("byte[] result = new byte[length];");
			Write("try ");
			OpenBlock();
			Write("try ");
			OpenBlock();
			WriteLine("dis.readFully(result);");
			CloseBlock();
			Write("finally ");
			OpenBlock();
			WriteLine("dis.close();");
			CloseBlock();
			CloseBlock();
			Write("catch (IOException e) ");
			OpenBlock();
			WriteLine("throw new RuntimeException();");
			CloseBlock();
			WriteLine("return result;");
			CloseBlock();
			CloseBlock();
			CloseFile();
		}

		public override void WriteProgram(CiProgram program)
		{
			this.SwitchCaseDiscards = 0;
			WriteTypes(program);
			if (program.Resources.Count > 0)
				WriteResources();
		}
	}

	public class GenJsNoModule : GenBase
	{

		readonly List<CiSwitch> SwitchesWithLabel = new List<CiSwitch>();

		bool StringWriter = false;

		protected override string GetTargetName() => "JavaScript";

		void WriteCamelCaseNotKeyword(string name)
		{
			WriteCamelCase(name);
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
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected virtual bool IsJsPrivate(CiMember member) => member.Visibility == CiVisibility.Private;

		protected override void WriteName(CiSymbol symbol)
		{
			switch (symbol) {
			case CiContainerType _:
				Write(symbol.Name);
				break;
			case CiConst konst:
				if (konst.InMethod != null) {
					WriteUppercaseWithUnderscores(konst.InMethod.Name);
					WriteChar('_');
				}
				WriteUppercaseWithUnderscores(symbol.Name);
				break;
			case CiVar _:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			case CiMember member:
				if (IsJsPrivate(member)) {
					WriteChar('#');
					WriteCamelCase(symbol.Name);
					if (symbol.Name == "Constructor")
						WriteChar('_');
				}
				else
					WriteCamelCaseNotKeyword(symbol.Name);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteTypeAndName(CiNamedValue value)
		{
			WriteName(value);
		}

		protected void WriteArrayElementType(CiType type)
		{
			switch (type.Id) {
			case CiId.SByteRange:
				Write("Int8");
				break;
			case CiId.ByteRange:
				Write("Uint8");
				break;
			case CiId.ShortRange:
				Write("Int16");
				break;
			case CiId.UShortRange:
				Write("Uint16");
				break;
			case CiId.IntType:
				Write("Int32");
				break;
			case CiId.LongType:
				Write("BigInt64");
				break;
			case CiId.FloatType:
				Write("Float32");
				break;
			case CiId.DoubleType:
				Write("Float64");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		public override void VisitAggregateInitializer(CiAggregateInitializer expr)
		{
			CiArrayStorageType array = (CiArrayStorageType) expr.Type;
			bool numeric = false;
			if (array.GetElementType() is CiNumericType number) {
				Write("new ");
				WriteArrayElementType(number);
				Write("Array(");
				numeric = true;
			}
			Write("[ ");
			WriteCoercedLiterals(null, expr.Items);
			Write(" ]");
			if (numeric)
				WriteChar(')');
		}

		protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
		{
			switch (klass.Class.Id) {
			case CiId.ListClass:
			case CiId.QueueClass:
			case CiId.StackClass:
				Write("[]");
				break;
			case CiId.HashSetClass:
			case CiId.SortedSetClass:
				Write("new Set()");
				break;
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
				Write("{}");
				break;
			case CiId.OrderedDictionaryClass:
				Write("new Map()");
				break;
			case CiId.LockClass:
				NotSupported(klass, "Lock");
				break;
			default:
				Write("new ");
				if (klass.Class.Id == CiId.StringWriterClass)
					this.StringWriter = true;
				Write(klass.Class.Name);
				Write("()");
				break;
			}
		}

		protected override void WriteNewWithFields(CiReadWriteClassType type, CiAggregateInitializer init)
		{
			Write("Object.assign(");
			WriteNew(type, CiPriority.Argument);
			WriteChar(',');
			WriteObjectLiteral(init, ": ");
			WriteChar(')');
		}

		protected override void WriteVar(CiNamedValue def)
		{
			Write(def.Type.IsFinal() && !def.IsAssignableStorage() ? "const " : "let ");
			base.WriteVar(def);
		}

		void WriteInterpolatedLiteral(string s)
		{
			int i = 0;
			foreach (int c in s) {
				i++;
				if (c == '`' || (c == '$' && i < s.Length && s[i] == '{'))
					WriteChar('\\');
				WriteChar(c);
			}
		}

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			WriteChar('`');
			foreach (CiInterpolatedPart part in expr.Parts) {
				WriteInterpolatedLiteral(part.Prefix);
				Write("${");
				if (part.Width != 0 || part.Format != ' ') {
					if (part.Argument is CiLiteralLong || part.Argument is CiPrefixExpr) {
						WriteChar('(');
						part.Argument.Accept(this, CiPriority.Primary);
						WriteChar(')');
					}
					else
						part.Argument.Accept(this, CiPriority.Primary);
					if (part.Argument.Type is CiNumericType) {
						switch (part.Format) {
						case 'E':
							Write(".toExponential(");
							if (part.Precision >= 0)
								VisitLiteralLong(part.Precision);
							Write(").toUpperCase()");
							break;
						case 'e':
							Write(".toExponential(");
							if (part.Precision >= 0)
								VisitLiteralLong(part.Precision);
							WriteChar(')');
							break;
						case 'F':
						case 'f':
							Write(".toFixed(");
							if (part.Precision >= 0)
								VisitLiteralLong(part.Precision);
							WriteChar(')');
							break;
						case 'X':
							Write(".toString(16).toUpperCase()");
							break;
						case 'x':
							Write(".toString(16)");
							break;
						default:
							Write(".toString()");
							break;
						}
						if (part.Precision >= 0) {
							switch (part.Format) {
							case 'D':
							case 'd':
							case 'X':
							case 'x':
								Write(".padStart(");
								VisitLiteralLong(part.Precision);
								Write(", \"0\")");
								break;
							default:
								break;
							}
						}
					}
					if (part.Width > 0) {
						Write(".padStart(");
						VisitLiteralLong(part.Width);
						WriteChar(')');
					}
					else if (part.Width < 0) {
						Write(".padEnd(");
						VisitLiteralLong(-part.Width);
						WriteChar(')');
					}
				}
				else
					part.Argument.Accept(this, CiPriority.Argument);
				WriteChar('}');
			}
			WriteInterpolatedLiteral(expr.Suffix);
			WriteChar('`');
		}

		protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
		{
			if (symbol is CiMember member) {
				if (!member.IsStatic())
					Write("this");
				else if (this.CurrentMethod != null)
					Write(this.CurrentMethod.Parent.Name);
				else if (symbol is CiConst konst) {
					konst.Value.Accept(this, parent);
					return;
				}
				else
					throw new NotImplementedException();
				WriteChar('.');
			}
			WriteName(symbol);
			if (symbol.Parent is CiForeach forEach && forEach.Collection.Type is CiStringType)
				Write(".codePointAt(0)");
		}

		protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
		{
			if (type is CiNumericType) {
				if (type.Id == CiId.LongType) {
					if (expr is CiLiteralLong) {
						expr.Accept(this, CiPriority.Primary);
						WriteChar('n');
						return;
					}
					if (expr.Type.Id != CiId.LongType) {
						WriteCall("BigInt", expr);
						return;
					}
				}
				else if (expr.Type.Id == CiId.LongType) {
					WriteCall("Number", expr);
					return;
				}
			}
			expr.Accept(this, parent);
		}

		protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
		{
			Write("new ");
			if (elementType is CiNumericType)
				WriteArrayElementType(elementType);
			WriteCall("Array", lengthExpr);
		}

		protected override bool HasInitCode(CiNamedValue def) => def.Type is CiArrayStorageType array && array.GetElementType() is CiStorageType;

		protected override void WriteInitCode(CiNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			CiArrayStorageType array = (CiArrayStorageType) def.Type;
			int nesting = 0;
			while (array.GetElementType() is CiArrayStorageType innerArray) {
				OpenLoop("let", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNewArray(innerArray.GetElementType(), innerArray.LengthExpr, CiPriority.Argument);
				WriteCharLine(';');
				array = innerArray;
			}
			if (array.GetElementType() is CiStorageType klass) {
				OpenLoop("let", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNew(klass, CiPriority.Argument);
				WriteCharLine(';');
			}
			while (--nesting >= 0)
				CloseBlock();
		}

		protected override void WriteResource(string name, int length)
		{
			Write("Ci.");
			WriteResourceName(name);
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.ConsoleError:
				Write("process.stderr");
				break;
			case CiId.ListCount:
			case CiId.QueueCount:
			case CiId.StackCount:
				WritePostfix(expr.Left, ".length");
				break;
			case CiId.HashSetCount:
			case CiId.SortedSetCount:
			case CiId.OrderedDictionaryCount:
				WritePostfix(expr.Left, ".size");
				break;
			case CiId.DictionaryCount:
			case CiId.SortedDictionaryCount:
				WriteCall("Object.keys", expr.Left);
				Write(".length");
				break;
			case CiId.MatchStart:
				WritePostfix(expr.Left, ".index");
				break;
			case CiId.MatchEnd:
				if (parent > CiPriority.Add)
					WriteChar('(');
				WritePostfix(expr.Left, ".index + ");
				WritePostfix(expr.Left, "[0].length");
				if (parent > CiPriority.Add)
					WriteChar(')');
				break;
			case CiId.MatchLength:
				WritePostfix(expr.Left, "[0].length");
				break;
			case CiId.MatchValue:
				WritePostfix(expr.Left, "[0]");
				break;
			case CiId.MathNaN:
				Write("NaN");
				break;
			case CiId.MathNegativeInfinity:
				Write("-Infinity");
				break;
			case CiId.MathPositiveInfinity:
				Write("Infinity");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			WritePostfix(expr, ".length");
		}

		protected override void WriteCharAt(CiBinaryExpr expr)
		{
			WriteMethodCall(expr.Left, "charCodeAt", expr.Right);
		}

		protected override void WriteBinaryOperand(CiExpr expr, CiPriority parent, CiBinaryExpr binary)
		{
			WriteCoerced(binary.Type, expr, parent);
		}

		static bool IsIdentifier(string s)
		{
			if (s.Length == 0 || s[0] < 'A')
				return false;
			foreach (int c in s) {
				if (!CiLexer.IsLetterOrDigit(c))
					return false;
			}
			return true;
		}

		void WriteNewRegex(List<CiExpr> args, int argIndex)
		{
			CiExpr pattern = args[argIndex];
			if (pattern is CiLiteralString literal) {
				WriteChar('/');
				bool escaped = false;
				foreach (int c in literal.Value) {
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
						WriteChar('\\');
						escaped = false;
					}
					WriteChar(c);
				}
				WriteChar('/');
				WriteRegexOptions(args, "", "", "", "i", "m", "s");
			}
			else {
				Write("new RegExp(");
				pattern.Accept(this, CiPriority.Argument);
				WriteRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
				WriteChar(')');
			}
		}

		static bool HasLong(List<CiExpr> args) => args.Any(arg => arg.Type.Id == CiId.LongType);

		void WriteMathMaxMin(CiMethod method, string name, int op, List<CiExpr> args)
		{
			if (HasLong(args)) {
				Write("((x, y) => x ");
				WriteChar(op);
				Write(" y ? x : y)");
				WriteArgsInParentheses(method, args);
			}
			else
				WriteCall(name, args[0], args[1]);
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.None:
			case CiId.ClassToString:
			case CiId.StringEndsWith:
			case CiId.StringIndexOf:
			case CiId.StringLastIndexOf:
			case CiId.StringStartsWith:
			case CiId.ArraySortAll:
			case CiId.ListIndexOf:
			case CiId.StackPush:
			case CiId.StackPop:
			case CiId.HashSetAdd:
			case CiId.HashSetClear:
			case CiId.SortedSetAdd:
			case CiId.SortedSetClear:
			case CiId.OrderedDictionaryClear:
			case CiId.StringWriterClear:
			case CiId.StringWriterToString:
			case CiId.MathMethod:
			case CiId.MathLog2:
			case CiId.MathMaxDouble:
			case CiId.MathMinDouble:
			case CiId.MathRound:
				if (obj == null)
					WriteLocalName(method, CiPriority.Primary);
				else {
					if (IsReferenceTo(obj, CiId.BasePtr))
						Write("super");
					else
						obj.Accept(this, CiPriority.Primary);
					WriteChar('.');
					WriteName(method);
				}
				WriteArgsInParentheses(method, args);
				break;
			case CiId.EnumFromInt:
				args[0].Accept(this, parent);
				break;
			case CiId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case CiId.IntTryParse:
				Write("!isNaN(");
				obj.Accept(this, CiPriority.Assign);
				Write(" = parseInt(");
				args[0].Accept(this, CiPriority.Argument);
				WriteTryParseRadix(args);
				Write("))");
				break;
			case CiId.LongTryParse:
				if (args.Count != 1)
					NotSupported(args[1], "Radix");
				Write("(() => { try { ");
				obj.Accept(this, CiPriority.Assign);
				Write("  = BigInt(");
				args[0].Accept(this, CiPriority.Argument);
				Write("); return true; } catch { return false; }})()");
				break;
			case CiId.DoubleTryParse:
				Write("!isNaN(");
				obj.Accept(this, CiPriority.Assign);
				Write(" = parseFloat(");
				args[0].Accept(this, CiPriority.Argument);
				Write("))");
				break;
			case CiId.StringContains:
			case CiId.ListContains:
				WriteMethodCall(obj, "includes", args[0]);
				break;
			case CiId.StringReplace:
				WriteMethodCall(obj, "replaceAll", args[0], args[1]);
				break;
			case CiId.StringSubstring:
				WritePostfix(obj, ".substring(");
				args[0].Accept(this, CiPriority.Argument);
				if (args.Count == 2) {
					Write(", ");
					WriteAdd(args[0], args[1]);
				}
				WriteChar(')');
				break;
			case CiId.ArrayFillAll:
			case CiId.ArrayFillPart:
				WritePostfix(obj, ".fill(");
				args[0].Accept(this, CiPriority.Argument);
				if (args.Count == 3) {
					Write(", ");
					WriteStartEnd(args[1], args[2]);
				}
				WriteChar(')');
				break;
			case CiId.ArrayCopyTo:
			case CiId.ListCopyTo:
				args[1].Accept(this, CiPriority.Primary);
				bool wholeSource = obj.Type is CiArrayStorageType sourceStorage && args[0].IsLiteralZero() && args[3] is CiLiteralLong literalLength && literalLength.Value == sourceStorage.Length;
				if (obj.Type.AsClassType().GetElementType() is CiNumericType) {
					Write(".set(");
					if (wholeSource)
						obj.Accept(this, CiPriority.Argument);
					else {
						WritePostfix(obj, method.Id == CiId.ArrayCopyTo ? ".subarray(" : ".slice(");
						WriteStartEnd(args[0], args[3]);
						WriteChar(')');
					}
					if (!args[2].IsLiteralZero()) {
						Write(", ");
						args[2].Accept(this, CiPriority.Argument);
					}
				}
				else {
					Write(".splice(");
					args[2].Accept(this, CiPriority.Argument);
					Write(", ");
					args[3].Accept(this, CiPriority.Argument);
					Write(", ...");
					obj.Accept(this, CiPriority.Primary);
					if (!wholeSource) {
						Write(".slice(");
						WriteStartEnd(args[0], args[3]);
						WriteChar(')');
					}
				}
				WriteChar(')');
				break;
			case CiId.ArraySortPart:
				WritePostfix(obj, ".subarray(");
				WriteStartEnd(args[0], args[1]);
				Write(").sort()");
				break;
			case CiId.ListAdd:
				WriteListAdd(obj, "push", args);
				break;
			case CiId.ListAddRange:
				WritePostfix(obj, ".push(...");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ListAll:
				WriteMethodCall(obj, "every", args[0]);
				break;
			case CiId.ListAny:
				WriteMethodCall(obj, "some", args[0]);
				break;
			case CiId.ListClear:
			case CiId.QueueClear:
			case CiId.StackClear:
				WritePostfix(obj, ".length = 0");
				break;
			case CiId.ListInsert:
				WriteListInsert(obj, "splice", args, ", 0, ");
				break;
			case CiId.ListLast:
			case CiId.StackPeek:
				WritePostfix(obj, ".at(-1)");
				break;
			case CiId.ListRemoveAt:
				WritePostfix(obj, ".splice(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", 1)");
				break;
			case CiId.ListRemoveRange:
				WriteMethodCall(obj, "splice", args[0], args[1]);
				break;
			case CiId.ListSortAll:
				WritePostfix(obj, ".sort((a, b) => a - b)");
				break;
			case CiId.ListSortPart:
				WritePostfix(obj, ".splice(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				args[1].Accept(this, CiPriority.Argument);
				Write(", ...");
				WritePostfix(obj, ".slice(");
				WriteStartEnd(args[0], args[1]);
				Write(").sort((a, b) => a - b))");
				break;
			case CiId.QueueDequeue:
				WritePostfix(obj, ".shift()");
				break;
			case CiId.QueueEnqueue:
				WriteMethodCall(obj, "push", args[0]);
				break;
			case CiId.QueuePeek:
				WritePostfix(obj, "[0]");
				break;
			case CiId.HashSetContains:
			case CiId.SortedSetContains:
			case CiId.OrderedDictionaryContainsKey:
				WriteMethodCall(obj, "has", args[0]);
				break;
			case CiId.HashSetRemove:
			case CiId.SortedSetRemove:
			case CiId.OrderedDictionaryRemove:
				WriteMethodCall(obj, "delete", args[0]);
				break;
			case CiId.DictionaryAdd:
				WriteDictionaryAdd(obj, args);
				break;
			case CiId.DictionaryClear:
			case CiId.SortedDictionaryClear:
				Write("for (const key in ");
				obj.Accept(this, CiPriority.Argument);
				WriteCharLine(')');
				Write("\tdelete ");
				WritePostfix(obj, "[key];");
				break;
			case CiId.DictionaryContainsKey:
			case CiId.SortedDictionaryContainsKey:
				WriteMethodCall(obj, "hasOwnProperty", args[0]);
				break;
			case CiId.DictionaryRemove:
			case CiId.SortedDictionaryRemove:
				Write("delete ");
				WriteIndexing(obj, args[0]);
				break;
			case CiId.TextWriterWrite:
				WritePostfix(obj, ".write(");
				if (args[0].Type is CiStringType)
					args[0].Accept(this, CiPriority.Argument);
				else
					WriteCall("String", args[0]);
				WriteChar(')');
				break;
			case CiId.TextWriterWriteChar:
				WriteMethodCall(obj, "write(String.fromCharCode", args[0]);
				WriteChar(')');
				break;
			case CiId.TextWriterWriteCodePoint:
				WriteMethodCall(obj, "write(String.fromCodePoint", args[0]);
				WriteChar(')');
				break;
			case CiId.TextWriterWriteLine:
				if (IsReferenceTo(obj, CiId.ConsoleError)) {
					Write("console.error(");
					if (args.Count == 0)
						Write("\"\"");
					else
						args[0].Accept(this, CiPriority.Argument);
					WriteChar(')');
				}
				else {
					WritePostfix(obj, ".write(");
					if (args.Count != 0) {
						args[0].Accept(this, CiPriority.Add);
						Write(" + ");
					}
					Write("\"\\n\")");
				}
				break;
			case CiId.ConsoleWrite:
				Write("process.stdout.write(");
				if (args[0].Type is CiStringType)
					args[0].Accept(this, CiPriority.Argument);
				else
					WriteCall("String", args[0]);
				WriteChar(')');
				break;
			case CiId.ConsoleWriteLine:
				Write("console.log(");
				if (args.Count == 0)
					Write("\"\"");
				else
					args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.UTF8GetByteCount:
				Write("new TextEncoder().encode(");
				args[0].Accept(this, CiPriority.Argument);
				Write(").length");
				break;
			case CiId.UTF8GetBytes:
				Write("new TextEncoder().encodeInto(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				if (args[2].IsLiteralZero())
					args[1].Accept(this, CiPriority.Argument);
				else
					WriteMethodCall(args[1], "subarray", args[2]);
				WriteChar(')');
				break;
			case CiId.UTF8GetString:
				Write("new TextDecoder().decode(");
				WritePostfix(args[0], ".subarray(");
				args[1].Accept(this, CiPriority.Argument);
				Write(", ");
				WriteAdd(args[1], args[2]);
				Write("))");
				break;
			case CiId.EnvironmentGetEnvironmentVariable:
				if (args[0] is CiLiteralString literal && IsIdentifier(literal.Value)) {
					Write("process.env.");
					Write(literal.Value);
				}
				else {
					Write("process.env[");
					args[0].Accept(this, CiPriority.Argument);
					WriteChar(']');
				}
				break;
			case CiId.RegexCompile:
				WriteNewRegex(args, 0);
				break;
			case CiId.RegexEscape:
				WritePostfix(args[0], ".replace(/[-\\/\\\\^$*+?.()|[\\]{}]/g, '\\\\$&')");
				break;
			case CiId.RegexIsMatchStr:
				WriteNewRegex(args, 1);
				WriteCall(".test", args[0]);
				break;
			case CiId.RegexIsMatchRegex:
				WriteMethodCall(obj, "test", args[0]);
				break;
			case CiId.MatchFindStr:
			case CiId.MatchFindRegex:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				WriteChar('(');
				obj.Accept(this, CiPriority.Assign);
				Write(" = ");
				if (method.Id == CiId.MatchFindStr)
					WriteNewRegex(args, 1);
				else
					args[1].Accept(this, CiPriority.Primary);
				WriteCall(".exec", args[0]);
				Write(") != null");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.MatchGetCapture:
				WriteIndexing(obj, args[0]);
				break;
			case CiId.MathAbs:
				WriteCall(args[0].Type.Id == CiId.LongType ? "(x => x < 0n ? -x : x)" : "Math.abs", args[0]);
				break;
			case CiId.MathCeiling:
				WriteCall("Math.ceil", args[0]);
				break;
			case CiId.MathClamp:
				if (method.Type.Id == CiId.IntType && HasLong(args)) {
					Write("((x, min, max) => x < min ? min : x > max ? max : x)");
					WriteArgsInParentheses(method, args);
				}
				else {
					Write("Math.min(Math.max(");
					WriteClampAsMinMax(args);
				}
				break;
			case CiId.MathFusedMultiplyAdd:
				if (parent > CiPriority.Add)
					WriteChar('(');
				args[0].Accept(this, CiPriority.Mul);
				Write(" * ");
				args[1].Accept(this, CiPriority.Mul);
				Write(" + ");
				args[2].Accept(this, CiPriority.Add);
				if (parent > CiPriority.Add)
					WriteChar(')');
				break;
			case CiId.MathIsFinite:
			case CiId.MathIsNaN:
				WriteCamelCase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathIsInfinity:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				WriteCall("Math.abs", args[0]);
				Write(" == Infinity");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.MathMaxInt:
				WriteMathMaxMin(method, "Math.max", '>', args);
				break;
			case CiId.MathMinInt:
				WriteMathMaxMin(method, "Math.min", '<', args);
				break;
			case CiId.MathTruncate:
				WriteCall("Math.trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
		{
			if (expr.Left.Type is CiClassType dict && dict.Class.Id == CiId.OrderedDictionaryClass)
				WriteMethodCall(expr.Left, "get", expr.Right);
			else
				base.WriteIndexingExpr(expr, parent);
		}

		protected override void WriteAssign(CiBinaryExpr expr, CiPriority parent)
		{
			if (expr.Left is CiBinaryExpr indexing && indexing.Op == CiToken.LeftBracket && indexing.Left.Type is CiClassType dict && dict.Class.Id == CiId.OrderedDictionaryClass)
				WriteMethodCall(indexing.Left, "set", indexing.Right, expr.Right);
			else
				base.WriteAssign(expr, parent);
		}

		protected override string GetIsOperator() => " instanceof ";

		protected virtual void WriteBoolAndOr(CiBinaryExpr expr)
		{
			Write("!!");
			base.VisitBinaryExpr(expr, CiPriority.Primary);
		}

		void WriteBoolAndOrAssign(CiBinaryExpr expr, CiPriority parent)
		{
			expr.Right.Accept(this, parent);
			WriteCharLine(')');
			WriteChar('\t');
			expr.Left.Accept(this, CiPriority.Assign);
		}

		void WriteIsVar(CiExpr expr, CiVar def, bool assign, CiPriority parent)
		{
			if (parent > CiPriority.Rel)
				WriteChar('(');
			if (assign) {
				WriteChar('(');
				WriteCamelCaseNotKeyword(def.Name);
				Write(" = ");
				expr.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			else
				expr.Accept(this, CiPriority.Rel);
			Write(" instanceof ");
			Write(def.Type.Name);
			if (parent > CiPriority.Rel)
				WriteChar(')');
		}

		public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Slash when expr.Type is CiIntegerType && expr.Type.Id != CiId.LongType:
				if (parent > CiPriority.Or)
					WriteChar('(');
				expr.Left.Accept(this, CiPriority.Mul);
				Write(" / ");
				expr.Right.Accept(this, CiPriority.Primary);
				Write(" | 0");
				if (parent > CiPriority.Or)
					WriteChar(')');
				break;
			case CiToken.DivAssign when expr.Type is CiIntegerType && expr.Type.Id != CiId.LongType:
				if (parent > CiPriority.Assign)
					WriteChar('(');
				expr.Left.Accept(this, CiPriority.Assign);
				Write(" = ");
				expr.Left.Accept(this, CiPriority.Mul);
				Write(" / ");
				expr.Right.Accept(this, CiPriority.Primary);
				Write(" | 0");
				if (parent > CiPriority.Assign)
					WriteChar(')');
				break;
			case CiToken.And when expr.Type.Id == CiId.BoolType:
			case CiToken.Or when expr.Type.Id == CiId.BoolType:
				WriteBoolAndOr(expr);
				break;
			case CiToken.Xor when expr.Type.Id == CiId.BoolType:
				WriteEqual(expr.Left, expr.Right, parent, true);
				break;
			case CiToken.AndAssign when expr.Type.Id == CiId.BoolType:
				Write("if (!");
				WriteBoolAndOrAssign(expr, CiPriority.Primary);
				Write(" = false");
				break;
			case CiToken.OrAssign when expr.Type.Id == CiId.BoolType:
				Write("if (");
				WriteBoolAndOrAssign(expr, CiPriority.Argument);
				Write(" = true");
				break;
			case CiToken.XorAssign when expr.Type.Id == CiId.BoolType:
				expr.Left.Accept(this, CiPriority.Assign);
				Write(" = ");
				WriteEqual(expr.Left, expr.Right, CiPriority.Argument, true);
				break;
			case CiToken.Is when expr.Right is CiVar def:
				WriteIsVar(expr.Left, def, true, parent);
				break;
			default:
				base.VisitBinaryExpr(expr, parent);
				break;
			}
		}

		public override void VisitLambdaExpr(CiLambdaExpr expr)
		{
			WriteName(expr.First);
			Write(" => ");
			expr.Body.Accept(this, CiPriority.Statement);
		}

		protected override void StartTemporaryVar(CiType type)
		{
			throw new NotImplementedException();
		}

		protected override void DefineObjectLiteralTemporary(CiUnaryExpr expr)
		{
		}

		protected virtual void WriteAsType(CiVar def)
		{
		}

		void WriteVarCast(CiVar def, CiExpr value)
		{
			Write("const ");
			WriteCamelCaseNotKeyword(def.Name);
			Write(" = ");
			value.Accept(this, CiPriority.Argument);
			WriteAsType(def);
			WriteCharLine(';');
		}

		protected override void WriteAssertCast(CiBinaryExpr expr)
		{
			CiVar def = (CiVar) expr.Right;
			WriteVarCast(def, expr.Left);
		}

		protected override void WriteAssert(CiAssert statement)
		{
			if (statement.CompletesNormally()) {
				Write("console.assert(");
				statement.Cond.Accept(this, CiPriority.Argument);
				if (statement.Message != null) {
					Write(", ");
					statement.Message.Accept(this, CiPriority.Argument);
				}
			}
			else {
				Write("throw new Error(");
				if (statement.Message != null)
					statement.Message.Accept(this, CiPriority.Argument);
			}
			WriteLine(");");
		}

		public override void VisitBreak(CiBreak statement)
		{
			if (statement.LoopOrSwitch is CiSwitch switchStatement) {
				int label = this.SwitchesWithLabel.IndexOf(switchStatement);
				if (label >= 0) {
					Write("break ciswitch");
					VisitLiteralLong(label);
					WriteCharLine(';');
					return;
				}
			}
			base.VisitBreak(statement);
		}

		public override void VisitForeach(CiForeach statement)
		{
			Write("for (const ");
			CiClassType klass = (CiClassType) statement.Collection.Type;
			switch (klass.Class.Id) {
			case CiId.StringClass:
			case CiId.ArrayStorageClass:
			case CiId.ListClass:
			case CiId.HashSetClass:
				WriteName(statement.GetVar());
				Write(" of ");
				statement.Collection.Accept(this, CiPriority.Argument);
				break;
			case CiId.SortedSetClass:
				WriteName(statement.GetVar());
				Write(" of Array.from(");
				statement.Collection.Accept(this, CiPriority.Argument);
				Write(").sort(");
				if (klass.GetElementType() is CiNumericType)
					Write("(a, b) => a - b");
				WriteChar(')');
				break;
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
			case CiId.OrderedDictionaryClass:
				WriteChar('[');
				WriteName(statement.GetVar());
				Write(", ");
				WriteName(statement.GetValueVar());
				Write("] of ");
				if (klass.Class.Id == CiId.OrderedDictionaryClass)
					statement.Collection.Accept(this, CiPriority.Argument);
				else {
					WriteCall("Object.entries", statement.Collection);
					switch (statement.GetVar().Type) {
					case CiStringType _:
						if (klass.Class.Id == CiId.SortedDictionaryClass)
							Write(".sort((a, b) => a[0].localeCompare(b[0]))");
						break;
					case CiNumericType _:
						Write(".map(e => [+e[0], e[1]])");
						if (klass.Class.Id == CiId.SortedDictionaryClass)
							Write(".sort((a, b) => a[0] - b[0])");
						break;
					default:
						throw new NotImplementedException();
					}
				}
				break;
			default:
				throw new NotImplementedException();
			}
			WriteChar(')');
			WriteChild(statement.Body);
		}

		public override void VisitLock(CiLock statement)
		{
			NotSupported(statement, "'lock'");
		}

		protected override void WriteSwitchCaseCond(CiSwitch statement, CiExpr value, CiPriority parent)
		{
			if (value is CiVar def)
				WriteIsVar(statement.Value, def, parent == CiPriority.CondAnd && def.Name != "_", parent);
			else
				base.WriteSwitchCaseCond(statement, value, parent);
		}

		protected override void WriteIfCaseBody(List<CiStatement> body, bool doWhile, CiSwitch statement, CiCase kase)
		{
			if (kase != null && kase.Values[0] is CiVar caseVar && caseVar.Name != "_") {
				WriteChar(' ');
				OpenBlock();
				WriteVarCast(caseVar, statement.Value);
				WriteFirstStatements(kase.Body, CiSwitch.LengthWithoutTrailingBreak(kase.Body));
				CloseBlock();
			}
			else
				base.WriteIfCaseBody(body, doWhile, statement, kase);
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			if (statement.IsTypeMatching() || statement.HasWhen()) {
				if (statement.Cases.Any(kase => CiSwitch.HasEarlyBreak(kase.Body)) || CiSwitch.HasEarlyBreak(statement.DefaultBody)) {
					Write("ciswitch");
					VisitLiteralLong(this.SwitchesWithLabel.Count);
					this.SwitchesWithLabel.Add(statement);
					Write(": ");
					OpenBlock();
					WriteSwitchAsIfs(statement, false);
					CloseBlock();
				}
				else
					WriteSwitchAsIfs(statement, false);
			}
			else
				base.VisitSwitch(statement);
		}

		public override void VisitThrow(CiThrow statement)
		{
			Write("throw ");
			statement.Message.Accept(this, CiPriority.Argument);
			WriteCharLine(';');
		}

		protected virtual void StartContainerType(CiContainerType container)
		{
			WriteNewLine();
			WriteDoc(container.Documentation);
		}

		public override void VisitEnumValue(CiConst konst, CiConst previous)
		{
			if (previous != null)
				WriteCharLine(',');
			WriteDoc(konst.Documentation);
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" : ");
			VisitLiteralLong(konst.Value.IntValue());
		}

		protected override void WriteEnum(CiEnum enu)
		{
			StartContainerType(enu);
			Write("const ");
			Write(enu.Name);
			Write(" = ");
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			CloseBlock();
		}

		protected override void WriteConst(CiConst konst)
		{
			if (konst.Visibility != CiVisibility.Private || konst.Type is CiArrayStorageType) {
				WriteNewLine();
				WriteDoc(konst.Documentation);
				Write("static ");
				WriteName(konst);
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Argument);
				WriteCharLine(';');
			}
		}

		protected override void WriteField(CiField field)
		{
			WriteDoc(field.Documentation);
			base.WriteVar(field);
			WriteCharLine(';');
		}

		protected override void WriteMethod(CiMethod method)
		{
			if (method.CallType == CiCallType.Abstract)
				return;
			this.SwitchesWithLabel.Clear();
			WriteNewLine();
			WriteMethodDoc(method);
			if (method.CallType == CiCallType.Static)
				Write("static ");
			WriteName(method);
			WriteParameters(method, true);
			WriteBody(method);
		}

		protected void WriteConstructor(CiClass klass)
		{
			this.SwitchesWithLabel.Clear();
			WriteLine("constructor()");
			OpenBlock();
			if (klass.Parent is CiClass)
				WriteLine("super();");
			WriteConstructorBody(klass);
			CloseBlock();
		}

		protected override void WriteClass(CiClass klass, CiProgram program)
		{
			if (!WriteBaseClass(klass, program))
				return;
			StartContainerType(klass);
			OpenClass(klass, "", " extends ");
			if (NeedsConstructor(klass)) {
				if (klass.Constructor != null)
					WriteDoc(klass.Constructor.Documentation);
				WriteConstructor(klass);
			}
			WriteMembers(klass, true);
			CloseBlock();
		}

		protected void WriteLib(SortedDictionary<string, List<byte>> resources)
		{
			if (this.StringWriter) {
				WriteNewLine();
				WriteLine("class StringWriter");
				OpenBlock();
				WriteLine("#buf = \"\";");
				WriteNewLine();
				WriteLine("write(s)");
				OpenBlock();
				WriteLine("this.#buf += s;");
				CloseBlock();
				WriteNewLine();
				WriteLine("clear()");
				OpenBlock();
				WriteLine("this.#buf = \"\";");
				CloseBlock();
				WriteNewLine();
				WriteLine("toString()");
				OpenBlock();
				WriteLine("return this.#buf;");
				CloseBlock();
				CloseBlock();
			}
			if (resources.Count == 0)
				return;
			WriteNewLine();
			WriteLine("class Ci");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				Write("static ");
				WriteResourceName(name);
				WriteLine(" = new Uint8Array([");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" ]);");
			}
			WriteNewLine();
			CloseBlock();
		}

		protected virtual void WriteUseStrict()
		{
			WriteNewLine();
			WriteLine("\"use strict\";");
		}

		public override void WriteProgram(CiProgram program)
		{
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteTypes(program);
			WriteLib(program.Resources);
			CloseFile();
		}
	}

	public class GenJs : GenJsNoModule
	{

		protected override void StartContainerType(CiContainerType container)
		{
			base.StartContainerType(container);
			if (container.IsPublic)
				Write("export ");
		}

		protected override void WriteUseStrict()
		{
		}
	}

	public class GenTs : GenJs
	{

		CiSystem System;

		bool GenFullCode = false;

		protected override string GetTargetName() => "TypeScript";

		public GenTs WithGenFullCode()
		{
			this.GenFullCode = true;
			return this;
		}

		protected override bool IsJsPrivate(CiMember member) => false;

		public override void VisitEnumValue(CiConst konst, CiConst previous)
		{
			WriteEnumValue(konst);
			WriteCharLine(',');
		}

		protected override void WriteEnum(CiEnum enu)
		{
			StartContainerType(enu);
			Write("enum ");
			Write(enu.Name);
			WriteChar(' ');
			OpenBlock();
			enu.AcceptValues(this);
			CloseBlock();
		}

		protected override void WriteTypeAndName(CiNamedValue value)
		{
			WriteName(value);
			Write(": ");
			WriteType(value.Type);
		}

		void WriteType(CiType type, bool readOnly = false)
		{
			switch (type) {
			case CiNumericType _:
				Write(type.Id == CiId.LongType ? "bigint" : "number");
				break;
			case CiEnum enu:
				Write(enu.Id == CiId.BoolType ? "boolean" : enu.Name);
				break;
			case CiClassType klass:
				readOnly |= !(klass is CiReadWriteClassType);
				switch (klass.Class.Id) {
				case CiId.StringClass:
					Write("string");
					break;
				case CiId.ArrayPtrClass when !(klass.GetElementType() is CiNumericType):
				case CiId.ArrayStorageClass when !(klass.GetElementType() is CiNumericType):
				case CiId.ListClass:
				case CiId.QueueClass:
				case CiId.StackClass:
					if (readOnly)
						Write("readonly ");
					if (klass.GetElementType().Nullable)
						WriteChar('(');
					WriteType(klass.GetElementType());
					if (klass.GetElementType().Nullable)
						WriteChar(')');
					Write("[]");
					break;
				default:
					if (readOnly && klass.Class.TypeParameterCount > 0)
						Write("Readonly<");
					switch (klass.Class.Id) {
					case CiId.ArrayPtrClass:
					case CiId.ArrayStorageClass:
						WriteArrayElementType(klass.GetElementType());
						Write("Array");
						break;
					case CiId.HashSetClass:
					case CiId.SortedSetClass:
						Write("Set<");
						WriteType(klass.GetElementType(), false);
						WriteChar('>');
						break;
					case CiId.DictionaryClass:
					case CiId.SortedDictionaryClass:
						if (klass.GetKeyType() is CiEnum)
							Write("Partial<");
						Write("Record<");
						WriteType(klass.GetKeyType());
						Write(", ");
						WriteType(klass.GetValueType());
						WriteChar('>');
						if (klass.GetKeyType() is CiEnum)
							WriteChar('>');
						break;
					case CiId.OrderedDictionaryClass:
						Write("Map<");
						WriteType(klass.GetKeyType());
						Write(", ");
						WriteType(klass.GetValueType());
						WriteChar('>');
						break;
					case CiId.RegexClass:
						Write("RegExp");
						break;
					case CiId.MatchClass:
						Write("RegExpMatchArray");
						break;
					default:
						Write(klass.Class.Name);
						break;
					}
					if (readOnly && klass.Class.TypeParameterCount > 0)
						WriteChar('>');
					break;
				}
				if (type.Nullable)
					Write(" | null");
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteAsType(CiVar def)
		{
			Write(" as ");
			Write(def.Type.Name);
		}

		protected override void WriteBinaryOperand(CiExpr expr, CiPriority parent, CiBinaryExpr binary)
		{
			CiType type = binary.Type;
			if (expr.Type is CiNumericType) {
				switch (binary.Op) {
				case CiToken.Equal:
				case CiToken.NotEqual:
				case CiToken.Less:
				case CiToken.LessOrEqual:
				case CiToken.Greater:
				case CiToken.GreaterOrEqual:
					type = this.System.PromoteNumericTypes(binary.Left.Type, binary.Right.Type);
					break;
				default:
					break;
				}
			}
			WriteCoerced(type, expr, parent);
		}

		protected override void WriteEqualOperand(CiExpr expr, CiExpr other)
		{
			if (expr.Type is CiNumericType)
				WriteCoerced(this.System.PromoteNumericTypes(expr.Type, other.Type), expr, CiPriority.Equality);
			else
				expr.Accept(this, CiPriority.Equality);
		}

		protected override void WriteBoolAndOr(CiBinaryExpr expr)
		{
			Write("[ ");
			expr.Left.Accept(this, CiPriority.Argument);
			Write(", ");
			expr.Right.Accept(this, CiPriority.Argument);
			Write(" ].");
			Write(expr.Op == CiToken.And ? "every" : "some");
			Write("(Boolean)");
		}

		protected override void DefineIsVar(CiBinaryExpr binary)
		{
			if (binary.Right is CiVar def) {
				EnsureChildBlock();
				Write("let ");
				WriteName(def);
				Write(": ");
				WriteType(binary.Left.Type);
				EndStatement();
			}
		}

		void WriteVisibility(CiVisibility visibility)
		{
			switch (visibility) {
			case CiVisibility.Private:
				Write("private ");
				break;
			case CiVisibility.Internal:
				break;
			case CiVisibility.Protected:
				Write("protected ");
				break;
			case CiVisibility.Public:
				Write("public ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteConst(CiConst konst)
		{
			WriteNewLine();
			WriteDoc(konst.Documentation);
			WriteVisibility(konst.Visibility);
			Write("static readonly ");
			WriteName(konst);
			Write(": ");
			WriteType(konst.Type, true);
			if (this.GenFullCode) {
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Argument);
			}
			WriteCharLine(';');
		}

		protected override void WriteField(CiField field)
		{
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			if (field.Type.IsFinal() && !field.IsAssignableStorage())
				Write("readonly ");
			WriteTypeAndName(field);
			if (this.GenFullCode)
				WriteVarInit(field);
			WriteCharLine(';');
		}

		protected override void WriteMethod(CiMethod method)
		{
			WriteNewLine();
			WriteMethodDoc(method);
			WriteVisibility(method.Visibility);
			switch (method.CallType) {
			case CiCallType.Static:
				Write("static ");
				break;
			case CiCallType.Virtual:
				break;
			case CiCallType.Abstract:
				Write("abstract ");
				break;
			case CiCallType.Override:
				break;
			case CiCallType.Normal:
				break;
			case CiCallType.Sealed:
				break;
			default:
				throw new NotImplementedException();
			}
			WriteName(method);
			WriteChar('(');
			int i = 0;
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (i > 0)
					Write(", ");
				WriteName(param);
				if (param.Value != null && !this.GenFullCode)
					WriteChar('?');
				Write(": ");
				WriteType(param.Type);
				if (param.Value != null && this.GenFullCode)
					WriteVarInit(param);
				i++;
			}
			Write("): ");
			WriteType(method.Type);
			if (this.GenFullCode)
				WriteBody(method);
			else
				WriteCharLine(';');
		}

		protected override void WriteClass(CiClass klass, CiProgram program)
		{
			if (!WriteBaseClass(klass, program))
				return;
			StartContainerType(klass);
			switch (klass.CallType) {
			case CiCallType.Normal:
				break;
			case CiCallType.Abstract:
				Write("abstract ");
				break;
			case CiCallType.Static:
			case CiCallType.Sealed:
				break;
			default:
				throw new NotImplementedException();
			}
			OpenClass(klass, "", " extends ");
			if (NeedsConstructor(klass) || klass.CallType == CiCallType.Static) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				else if (klass.CallType == CiCallType.Static)
					Write("private ");
				if (this.GenFullCode)
					WriteConstructor(klass);
				else
					WriteLine("constructor();");
			}
			WriteMembers(klass, this.GenFullCode);
			CloseBlock();
		}

		public override void WriteProgram(CiProgram program)
		{
			this.System = program.System;
			CreateOutputFile();
			if (this.GenFullCode)
				WriteTopLevelNatives(program);
			WriteTypes(program);
			if (this.GenFullCode)
				WriteLib(program.Resources);
			CloseFile();
		}
	}

	public abstract class GenPySwift : GenBase
	{

		protected override void WriteDocPara(CiDocPara para, bool many)
		{
			if (many) {
				WriteNewLine();
				StartDocLine();
				WriteNewLine();
				StartDocLine();
			}
			foreach (CiDocInline inline in para.Children) {
				switch (inline) {
				case CiDocText text:
					Write(text.Text);
					break;
				case CiDocCode code:
					WriteChar('`');
					Write(code.Text);
					WriteChar('`');
					break;
				case CiDocLine _:
					WriteNewLine();
					StartDocLine();
					break;
				default:
					throw new NotImplementedException();
				}
			}
		}

		protected abstract string GetDocBullet();

		protected override void WriteDocList(CiDocList list)
		{
			WriteNewLine();
			foreach (CiDocPara item in list.Items) {
				Write(GetDocBullet());
				WriteDocPara(item, false);
				WriteNewLine();
			}
			StartDocLine();
		}

		protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
		{
			if (symbol is CiMember member) {
				if (member.IsStatic())
					WriteName(this.CurrentMethod.Parent);
				else
					Write("self");
				WriteChar('.');
			}
			WriteName(symbol);
		}

		public override void VisitAggregateInitializer(CiAggregateInitializer expr)
		{
			Write("[ ");
			WriteCoercedLiterals(expr.Type.AsClassType().GetElementType(), expr.Items);
			Write(" ]");
		}

		public override void VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Increment:
			case CiToken.Decrement:
				expr.Inner.Accept(this, parent);
				break;
			default:
				base.VisitPrefixExpr(expr, parent);
				break;
			}
		}

		public override void VisitPostfixExpr(CiPostfixExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Increment:
			case CiToken.Decrement:
				expr.Inner.Accept(this, parent);
				break;
			default:
				base.VisitPostfixExpr(expr, parent);
				break;
			}
		}

		static bool IsPtr(CiExpr expr) => expr.Type is CiClassType klass && klass.Class.Id != CiId.StringClass && !(klass is CiStorageType);

		protected abstract string GetReferenceEqOp(bool not);

		protected override void WriteEqual(CiExpr left, CiExpr right, CiPriority parent, bool not)
		{
			if (IsPtr(left) || IsPtr(right))
				WriteEqualExpr(left, right, parent, GetReferenceEqOp(not));
			else
				base.WriteEqual(left, right, parent, not);
		}

		protected virtual void WriteExpr(CiExpr expr, CiPriority parent)
		{
			expr.Accept(this, parent);
		}

		protected void WriteListAppend(CiExpr obj, List<CiExpr> args)
		{
			WritePostfix(obj, ".append(");
			CiType elementType = obj.Type.AsClassType().GetElementType();
			if (args.Count == 0)
				WriteNewStorage(elementType);
			else
				WriteCoerced(elementType, args[0], CiPriority.Argument);
			WriteChar(')');
		}

		protected virtual bool VisitPreCall(CiCallExpr call) => false;

		protected bool VisitXcrement(CiExpr expr, bool postfix, bool write)
		{
			bool seen;
			switch (expr) {
			case CiVar def:
				return def.Value != null && VisitXcrement(def.Value, postfix, write);
			case CiAggregateInitializer _:
			case CiLiteral _:
			case CiLambdaExpr _:
				return false;
			case CiInterpolatedString interp:
				seen = false;
				foreach (CiInterpolatedPart part in interp.Parts)
					seen |= VisitXcrement(part.Argument, postfix, write);
				return seen;
			case CiSymbolReference symbol:
				return symbol.Left != null && VisitXcrement(symbol.Left, postfix, write);
			case CiUnaryExpr unary:
				if (unary.Inner == null)
					return false;
				seen = VisitXcrement(unary.Inner, postfix, write);
				if ((unary.Op == CiToken.Increment || unary.Op == CiToken.Decrement) && postfix == unary is CiPostfixExpr) {
					if (write) {
						WriteExpr(unary.Inner, CiPriority.Assign);
						WriteLine(unary.Op == CiToken.Increment ? " += 1" : " -= 1");
					}
					seen = true;
				}
				return seen;
			case CiBinaryExpr binary:
				seen = VisitXcrement(binary.Left, postfix, write);
				if (binary.Op == CiToken.Is)
					return seen;
				if (binary.Op == CiToken.CondAnd || binary.Op == CiToken.CondOr)
					Debug.Assert(!VisitXcrement(binary.Right, postfix, false));
				else
					seen |= VisitXcrement(binary.Right, postfix, write);
				return seen;
			case CiSelectExpr select:
				seen = VisitXcrement(select.Cond, postfix, write);
				Debug.Assert(!VisitXcrement(select.OnTrue, postfix, false));
				Debug.Assert(!VisitXcrement(select.OnFalse, postfix, false));
				return seen;
			case CiCallExpr call:
				seen = VisitXcrement(call.Method, postfix, write);
				foreach (CiExpr arg in call.Arguments)
					seen |= VisitXcrement(arg, postfix, write);
				if (!postfix)
					seen |= VisitPreCall(call);
				return seen;
			default:
				throw new NotImplementedException();
			}
		}

		public override void VisitExpr(CiExpr statement)
		{
			VisitXcrement(statement, false, true);
			if (!(statement is CiUnaryExpr unary) || (unary.Op != CiToken.Increment && unary.Op != CiToken.Decrement)) {
				WriteExpr(statement, CiPriority.Statement);
				WriteNewLine();
				if (statement is CiVar def)
					WriteInitCode(def);
			}
			VisitXcrement(statement, true, true);
			CleanupTemporaries();
		}

		protected override void EndStatement()
		{
			WriteNewLine();
		}

		protected abstract void OpenChild();

		protected abstract void CloseChild();

		protected override void WriteChild(CiStatement statement)
		{
			OpenChild();
			statement.AcceptStatement(this);
			CloseChild();
		}

		public override void VisitBlock(CiBlock statement)
		{
			WriteStatements(statement.Statements);
		}

		bool OpenCond(string statement, CiExpr cond, CiPriority parent)
		{
			VisitXcrement(cond, false, true);
			Write(statement);
			WriteExpr(cond, parent);
			OpenChild();
			return VisitXcrement(cond, true, true);
		}

		protected virtual void WriteContinueDoWhile(CiExpr cond)
		{
			OpenCond("if ", cond, CiPriority.Argument);
			WriteLine("continue");
			CloseChild();
			VisitXcrement(cond, true, true);
			WriteLine("break");
		}

		protected virtual bool NeedCondXcrement(CiLoop loop) => loop.Cond != null;

		void EndBody(CiLoop loop)
		{
			if (loop is CiFor forLoop) {
				if (forLoop.IsRange)
					return;
				VisitOptionalStatement(forLoop.Advance);
			}
			if (NeedCondXcrement(loop))
				VisitXcrement(loop.Cond, false, true);
		}

		public override void VisitContinue(CiContinue statement)
		{
			if (statement.Loop is CiDoWhile doWhile)
				WriteContinueDoWhile(doWhile.Cond);
			else {
				EndBody(statement.Loop);
				WriteLine("continue");
			}
		}

		void OpenWhileTrue()
		{
			Write("while ");
			VisitLiteralTrue();
			OpenChild();
		}

		protected abstract string GetIfNot();

		public override void VisitDoWhile(CiDoWhile statement)
		{
			OpenWhileTrue();
			statement.Body.AcceptStatement(this);
			if (statement.Body.CompletesNormally()) {
				OpenCond(GetIfNot(), statement.Cond, CiPriority.Primary);
				WriteLine("break");
				CloseChild();
				VisitXcrement(statement.Cond, true, true);
			}
			CloseChild();
		}

		protected virtual void OpenWhile(CiLoop loop)
		{
			OpenCond("while ", loop.Cond, CiPriority.Argument);
		}

		void CloseWhile(CiLoop loop)
		{
			loop.Body.AcceptStatement(this);
			if (loop.Body.CompletesNormally())
				EndBody(loop);
			CloseChild();
			if (NeedCondXcrement(loop)) {
				if (loop.HasBreak && VisitXcrement(loop.Cond, true, false)) {
					Write("else");
					OpenChild();
					VisitXcrement(loop.Cond, true, true);
					CloseChild();
				}
				else
					VisitXcrement(loop.Cond, true, true);
			}
		}

		protected abstract void WriteForRange(CiVar iter, CiBinaryExpr cond, long rangeStep);

		public override void VisitFor(CiFor statement)
		{
			if (statement.IsRange) {
				CiVar iter = (CiVar) statement.Init;
				Write("for ");
				if (statement.IsIteratorUsed)
					WriteName(iter);
				else
					WriteChar('_');
				Write(" in ");
				CiBinaryExpr cond = (CiBinaryExpr) statement.Cond;
				WriteForRange(iter, cond, statement.RangeStep);
				WriteChild(statement.Body);
			}
			else {
				VisitOptionalStatement(statement.Init);
				if (statement.Cond != null)
					OpenWhile(statement);
				else
					OpenWhileTrue();
				CloseWhile(statement);
			}
		}

		protected abstract void WriteElseIf();

		public override void VisitIf(CiIf statement)
		{
			bool condPostXcrement = OpenCond("if ", statement.Cond, CiPriority.Argument);
			statement.OnTrue.AcceptStatement(this);
			CloseChild();
			if (statement.OnFalse == null && condPostXcrement && !statement.OnTrue.CompletesNormally())
				VisitXcrement(statement.Cond, true, true);
			else if (statement.OnFalse != null || condPostXcrement) {
				if (!condPostXcrement && statement.OnFalse is CiIf childIf && !VisitXcrement(childIf.Cond, false, false)) {
					WriteElseIf();
					VisitIf(childIf);
				}
				else {
					Write("else");
					OpenChild();
					VisitXcrement(statement.Cond, true, true);
					VisitOptionalStatement(statement.OnFalse);
					CloseChild();
				}
			}
		}

		protected abstract void WriteResultVar();

		public override void VisitReturn(CiReturn statement)
		{
			if (statement.Value == null)
				WriteLine("return");
			else {
				VisitXcrement(statement.Value, false, true);
				WriteTemporaries(statement.Value);
				if (VisitXcrement(statement.Value, true, false)) {
					WriteResultVar();
					Write(" = ");
					WriteCoercedExpr(this.CurrentMethod.Type, statement.Value);
					WriteNewLine();
					VisitXcrement(statement.Value, true, true);
					WriteLine("return result");
				}
				else {
					Write("return ");
					WriteCoercedExpr(this.CurrentMethod.Type, statement.Value);
					WriteNewLine();
				}
				CleanupTemporaries();
			}
		}

		public override void VisitWhile(CiWhile statement)
		{
			OpenWhile(statement);
			CloseWhile(statement);
		}
	}

	public class GenSwift : GenPySwift
	{

		CiSystem System;

		bool Throw;

		bool ArrayRef;

		bool StringCharAt;

		bool StringIndexOf;

		bool StringSubstring;

		readonly List<HashSet<string>> VarsAtIndent = new List<HashSet<string>>();

		readonly List<bool> VarBytesAtIndent = new List<bool>();

		protected override string GetTargetName() => "Swift";

		protected override void StartDocLine()
		{
			Write("/// ");
		}

		protected override string GetDocBullet() => "/// * ";

		protected override void WriteDoc(CiCodeDoc doc)
		{
			if (doc != null)
				WriteContent(doc);
		}

		void WriteCamelCaseNotKeyword(string name)
		{
			switch (name) {
			case "this":
				Write("self");
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
				WriteCamelCase(name);
				WriteChar('_');
				break;
			default:
				WriteCamelCase(name);
				break;
			}
		}

		protected override void WriteName(CiSymbol symbol)
		{
			switch (symbol) {
			case CiContainerType _:
				Write(symbol.Name);
				break;
			case CiConst konst when konst.InMethod != null:
				WriteCamelCase(konst.InMethod.Name);
				WritePascalCase(symbol.Name);
				break;
			case CiVar _:
			case CiMember _:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
		{
			if (symbol.Parent is CiForeach forEach && forEach.Collection.Type is CiStringType) {
				Write("Int(");
				WriteCamelCaseNotKeyword(symbol.Name);
				Write(".value)");
			}
			else
				base.WriteLocalName(symbol, parent);
		}

		protected override void WriteMemberOp(CiExpr left, CiSymbolReference symbol)
		{
			if (left.Type != null && left.Type.Nullable)
				WriteChar('!');
			WriteChar('.');
		}

		void OpenIndexing(CiExpr collection)
		{
			collection.Accept(this, CiPriority.Primary);
			if (collection.Type.Nullable)
				WriteChar('!');
			WriteChar('[');
		}

		static bool IsArrayRef(CiArrayStorageType array) => array.PtrTaken || array.GetElementType() is CiStorageType;

		void WriteClassName(CiClassType klass)
		{
			switch (klass.Class.Id) {
			case CiId.StringClass:
				Write("String");
				break;
			case CiId.ArrayPtrClass:
				this.ArrayRef = true;
				Write("ArrayRef<");
				WriteType(klass.GetElementType());
				WriteChar('>');
				break;
			case CiId.ListClass:
			case CiId.QueueClass:
			case CiId.StackClass:
				WriteChar('[');
				WriteType(klass.GetElementType());
				WriteChar(']');
				break;
			case CiId.HashSetClass:
			case CiId.SortedSetClass:
				Write("Set<");
				WriteType(klass.GetElementType());
				WriteChar('>');
				break;
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
				WriteChar('[');
				WriteType(klass.GetKeyType());
				Write(": ");
				WriteType(klass.GetValueType());
				WriteChar(']');
				break;
			case CiId.OrderedDictionaryClass:
				NotSupported(klass, "OrderedDictionary");
				break;
			case CiId.LockClass:
				Include("Foundation");
				Write("NSRecursiveLock");
				break;
			default:
				Write(klass.Class.Name);
				break;
			}
		}

		void WriteType(CiType type)
		{
			switch (type) {
			case CiNumericType _:
				switch (type.Id) {
				case CiId.SByteRange:
					Write("Int8");
					break;
				case CiId.ByteRange:
					Write("UInt8");
					break;
				case CiId.ShortRange:
					Write("Int16");
					break;
				case CiId.UShortRange:
					Write("UInt16");
					break;
				case CiId.IntType:
					Write("Int");
					break;
				case CiId.LongType:
					Write("Int64");
					break;
				case CiId.FloatType:
					Write("Float");
					break;
				case CiId.DoubleType:
					Write("Double");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			case CiEnum _:
				Write(type.Id == CiId.BoolType ? "Bool" : type.Name);
				break;
			case CiArrayStorageType arrayStg:
				if (IsArrayRef(arrayStg)) {
					this.ArrayRef = true;
					Write("ArrayRef<");
					WriteType(arrayStg.GetElementType());
					WriteChar('>');
				}
				else {
					WriteChar('[');
					WriteType(arrayStg.GetElementType());
					WriteChar(']');
				}
				break;
			case CiClassType klass:
				WriteClassName(klass);
				if (klass.Nullable)
					WriteChar('?');
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteTypeAndName(CiNamedValue value)
		{
			WriteName(value);
			if (!value.Type.IsFinal() || value.IsAssignableStorage()) {
				Write(" : ");
				WriteType(value.Type);
			}
		}

		public override void VisitLiteralNull()
		{
			Write("nil");
		}

		void WriteUnwrapped(CiExpr expr, CiPriority parent, bool substringOk)
		{
			if (expr.Type.Nullable) {
				expr.Accept(this, CiPriority.Primary);
				WriteChar('!');
			}
			else if (!substringOk && expr is CiCallExpr call && call.Method.Symbol.Id == CiId.StringSubstring)
				WriteCall("String", expr);
			else
				expr.Accept(this, parent);
		}

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			if (expr.Parts.Any(part => part.WidthExpr != null || part.Format != ' ' || part.Precision >= 0)) {
				Include("Foundation");
				Write("String(format: ");
				WritePrintf(expr, false);
			}
			else {
				WriteChar('"');
				foreach (CiInterpolatedPart part in expr.Parts) {
					Write(part.Prefix);
					Write("\\(");
					WriteUnwrapped(part.Argument, CiPriority.Argument, true);
					WriteChar(')');
				}
				Write(expr.Suffix);
				WriteChar('"');
			}
		}

		protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
		{
			if (type is CiNumericType && !(expr is CiLiteral) && GetTypeId(type, false) != GetTypeId(expr.Type, expr is CiBinaryExpr binary && binary.Op != CiToken.LeftBracket)) {
				WriteType(type);
				WriteChar('(');
				if (type is CiIntegerType && expr is CiCallExpr call && call.Method.Symbol.Id == CiId.MathTruncate)
					call.Arguments[0].Accept(this, CiPriority.Argument);
				else
					expr.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			else if (!type.Nullable)
				WriteUnwrapped(expr, parent, false);
			else
				expr.Accept(this, parent);
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			WriteUnwrapped(expr, CiPriority.Primary, true);
			Write(".count");
		}

		protected override void WriteCharAt(CiBinaryExpr expr)
		{
			this.StringCharAt = true;
			Write("ciStringCharAt(");
			WriteUnwrapped(expr.Left, CiPriority.Argument, false);
			Write(", ");
			expr.Right.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.MathNaN:
				Write("Float.nan");
				break;
			case CiId.MathNegativeInfinity:
				Write("-Float.infinity");
				break;
			case CiId.MathPositiveInfinity:
				Write("Float.infinity");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		protected override string GetReferenceEqOp(bool not) => not ? " !== " : " === ";

		void WriteStringContains(CiExpr obj, string name, List<CiExpr> args)
		{
			WriteUnwrapped(obj, CiPriority.Primary, true);
			WriteChar('.');
			Write(name);
			WriteChar('(');
			WriteUnwrapped(args[0], CiPriority.Argument, true);
			WriteChar(')');
		}

		void WriteRange(CiExpr startIndex, CiExpr length)
		{
			WriteCoerced(this.System.IntType, startIndex, CiPriority.Shift);
			Write("..<");
			WriteAdd(startIndex, length);
		}

		bool AddVar(string name)
		{
			HashSet<string> vars = this.VarsAtIndent[this.Indent];
			if (vars.Contains(name))
				return false;
			vars.Add(name);
			return true;
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.None:
			case CiId.ListContains:
			case CiId.ListSortAll:
			case CiId.HashSetContains:
			case CiId.HashSetRemove:
			case CiId.SortedSetContains:
			case CiId.SortedSetRemove:
				if (obj == null) {
					if (method.IsStatic()) {
						WriteName(this.CurrentMethod.Parent);
						WriteChar('.');
					}
				}
				else if (IsReferenceTo(obj, CiId.BasePtr))
					Write("super.");
				else {
					obj.Accept(this, CiPriority.Primary);
					WriteMemberOp(obj, null);
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.ClassToString:
				obj.Accept(this, CiPriority.Primary);
				WriteMemberOp(obj, null);
				Write("description");
				break;
			case CiId.EnumFromInt:
				Write(method.Type.Name);
				Write("(rawValue: ");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.EnumHasFlag:
				WriteMethodCall(obj, "contains", args[0]);
				break;
			case CiId.StringContains:
				WriteStringContains(obj, "contains", args);
				break;
			case CiId.StringEndsWith:
				WriteStringContains(obj, "hasSuffix", args);
				break;
			case CiId.StringIndexOf:
				Include("Foundation");
				this.StringIndexOf = true;
				Write("ciStringIndexOf(");
				WriteUnwrapped(obj, CiPriority.Argument, true);
				Write(", ");
				WriteUnwrapped(args[0], CiPriority.Argument, true);
				WriteChar(')');
				break;
			case CiId.StringLastIndexOf:
				Include("Foundation");
				this.StringIndexOf = true;
				Write("ciStringIndexOf(");
				WriteUnwrapped(obj, CiPriority.Argument, true);
				Write(", ");
				WriteUnwrapped(args[0], CiPriority.Argument, true);
				Write(", .backwards)");
				break;
			case CiId.StringReplace:
				WriteUnwrapped(obj, CiPriority.Primary, true);
				Write(".replacingOccurrences(of: ");
				WriteUnwrapped(args[0], CiPriority.Argument, true);
				Write(", with: ");
				WriteUnwrapped(args[1], CiPriority.Argument, true);
				WriteChar(')');
				break;
			case CiId.StringStartsWith:
				WriteStringContains(obj, "hasPrefix", args);
				break;
			case CiId.StringSubstring:
				if (args[0].IsLiteralZero())
					WriteUnwrapped(obj, CiPriority.Primary, true);
				else {
					this.StringSubstring = true;
					Write("ciStringSubstring(");
					WriteUnwrapped(obj, CiPriority.Argument, false);
					Write(", ");
					WriteCoerced(this.System.IntType, args[0], CiPriority.Argument);
					WriteChar(')');
				}
				if (args.Count == 2) {
					Write(".prefix(");
					WriteCoerced(this.System.IntType, args[1], CiPriority.Argument);
					WriteChar(')');
				}
				break;
			case CiId.ArrayCopyTo:
			case CiId.ListCopyTo:
				OpenIndexing(args[1]);
				WriteRange(args[2], args[3]);
				Write("] = ");
				OpenIndexing(obj);
				WriteRange(args[0], args[3]);
				WriteChar(']');
				break;
			case CiId.ArrayFillAll:
				obj.Accept(this, CiPriority.Assign);
				if (obj.Type is CiArrayStorageType array && !IsArrayRef(array)) {
					Write(" = [");
					WriteType(array.GetElementType());
					Write("](repeating: ");
					WriteCoerced(array.GetElementType(), args[0], CiPriority.Argument);
					Write(", count: ");
					VisitLiteralLong(array.Length);
					WriteChar(')');
				}
				else {
					Write(".fill");
					WriteArgsInParentheses(method, args);
				}
				break;
			case CiId.ArrayFillPart:
				if (obj.Type is CiArrayStorageType array2 && !IsArrayRef(array2)) {
					OpenIndexing(obj);
					WriteRange(args[1], args[2]);
					Write("] = ArraySlice(repeating: ");
					WriteCoerced(array2.GetElementType(), args[0], CiPriority.Argument);
					Write(", count: ");
					WriteCoerced(this.System.IntType, args[2], CiPriority.Argument);
					WriteChar(')');
				}
				else {
					obj.Accept(this, CiPriority.Primary);
					WriteMemberOp(obj, null);
					Write("fill");
					WriteArgsInParentheses(method, args);
				}
				break;
			case CiId.ArraySortAll:
				WritePostfix(obj, "[0..<");
				CiArrayStorageType array3 = (CiArrayStorageType) obj.Type;
				VisitLiteralLong(array3.Length);
				Write("].sort()");
				break;
			case CiId.ArraySortPart:
			case CiId.ListSortPart:
				OpenIndexing(obj);
				WriteRange(args[0], args[1]);
				Write("].sort()");
				break;
			case CiId.ListAdd:
			case CiId.QueueEnqueue:
			case CiId.StackPush:
				WriteListAppend(obj, args);
				break;
			case CiId.ListAddRange:
				obj.Accept(this, CiPriority.Assign);
				Write(" += ");
				args[0].Accept(this, CiPriority.Argument);
				break;
			case CiId.ListAll:
				WritePostfix(obj, ".allSatisfy ");
				args[0].Accept(this, CiPriority.Argument);
				break;
			case CiId.ListAny:
				WritePostfix(obj, ".contains ");
				args[0].Accept(this, CiPriority.Argument);
				break;
			case CiId.ListClear:
			case CiId.QueueClear:
			case CiId.StackClear:
			case CiId.HashSetClear:
			case CiId.SortedSetClear:
			case CiId.DictionaryClear:
			case CiId.SortedDictionaryClear:
				WritePostfix(obj, ".removeAll()");
				break;
			case CiId.ListIndexOf:
				if (parent > CiPriority.Rel)
					WriteChar('(');
				WritePostfix(obj, ".firstIndex(of: ");
				args[0].Accept(this, CiPriority.Argument);
				Write(") ?? -1");
				if (parent > CiPriority.Rel)
					WriteChar(')');
				break;
			case CiId.ListInsert:
				WritePostfix(obj, ".insert(");
				CiType elementType = obj.Type.AsClassType().GetElementType();
				if (args.Count == 1)
					WriteNewStorage(elementType);
				else
					WriteCoerced(elementType, args[1], CiPriority.Argument);
				Write(", at: ");
				WriteCoerced(this.System.IntType, args[0], CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ListLast:
			case CiId.StackPeek:
				WritePostfix(obj, ".last");
				break;
			case CiId.ListRemoveAt:
				WritePostfix(obj, ".remove(at: ");
				WriteCoerced(this.System.IntType, args[0], CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ListRemoveRange:
				WritePostfix(obj, ".removeSubrange(");
				WriteRange(args[0], args[1]);
				WriteChar(')');
				break;
			case CiId.QueueDequeue:
				WritePostfix(obj, ".removeFirst()");
				break;
			case CiId.QueuePeek:
				WritePostfix(obj, ".first");
				break;
			case CiId.StackPop:
				WritePostfix(obj, ".removeLast()");
				break;
			case CiId.HashSetAdd:
			case CiId.SortedSetAdd:
				WritePostfix(obj, ".insert(");
				WriteCoerced(obj.Type.AsClassType().GetElementType(), args[0], CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.DictionaryAdd:
				WriteDictionaryAdd(obj, args);
				break;
			case CiId.DictionaryContainsKey:
			case CiId.SortedDictionaryContainsKey:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				WriteIndexing(obj, args[0]);
				Write(" != nil");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.DictionaryRemove:
			case CiId.SortedDictionaryRemove:
				WritePostfix(obj, ".removeValue(forKey: ");
				args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ConsoleWrite:
				Write("print(");
				WriteUnwrapped(args[0], CiPriority.Argument, true);
				Write(", terminator: \"\")");
				break;
			case CiId.ConsoleWriteLine:
				Write("print(");
				if (args.Count == 1)
					WriteUnwrapped(args[0], CiPriority.Argument, true);
				WriteChar(')');
				break;
			case CiId.UTF8GetByteCount:
				WriteUnwrapped(args[0], CiPriority.Primary, true);
				Write(".utf8.count");
				break;
			case CiId.UTF8GetBytes:
				if (AddVar("cibytes"))
					Write(this.VarBytesAtIndent[this.Indent] ? "var " : "let ");
				Write("cibytes = [UInt8](");
				WriteUnwrapped(args[0], CiPriority.Primary, true);
				WriteLine(".utf8)");
				OpenIndexing(args[1]);
				WriteCoerced(this.System.IntType, args[2], CiPriority.Shift);
				if (args[2].IsLiteralZero())
					Write("..<");
				else {
					Write(" ..< ");
					WriteCoerced(this.System.IntType, args[2], CiPriority.Add);
					Write(" + ");
				}
				WriteLine("cibytes.count] = cibytes[...]");
				break;
			case CiId.UTF8GetString:
				Write("String(decoding: ");
				OpenIndexing(args[0]);
				WriteRange(args[1], args[2]);
				Write("], as: UTF8.self)");
				break;
			case CiId.EnvironmentGetEnvironmentVariable:
				Include("Foundation");
				Write("ProcessInfo.processInfo.environment[");
				WriteUnwrapped(args[0], CiPriority.Argument, false);
				WriteChar(']');
				break;
			case CiId.MathMethod:
			case CiId.MathLog2:
				Include("Foundation");
				WriteCamelCase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathAbs:
			case CiId.MathMaxInt:
			case CiId.MathMaxDouble:
			case CiId.MathMinInt:
			case CiId.MathMinDouble:
				WriteCamelCase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathCeiling:
				Include("Foundation");
				WriteCall("ceil", args[0]);
				break;
			case CiId.MathClamp:
				Write("min(max(");
				WriteClampAsMinMax(args);
				break;
			case CiId.MathFusedMultiplyAdd:
				Include("Foundation");
				WriteCall("fma", args[0], args[1], args[2]);
				break;
			case CiId.MathIsFinite:
				WritePostfix(args[0], ".isFinite");
				break;
			case CiId.MathIsInfinity:
				WritePostfix(args[0], ".isInfinite");
				break;
			case CiId.MathIsNaN:
				WritePostfix(args[0], ".isNaN");
				break;
			case CiId.MathRound:
				WritePostfix(args[0], ".rounded()");
				break;
			case CiId.MathTruncate:
				Include("Foundation");
				WriteCall("trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteNewArrayStorage(CiArrayStorageType array)
		{
			if (IsArrayRef(array))
				base.WriteNewArrayStorage(array);
			else {
				WriteChar('[');
				WriteType(array.GetElementType());
				Write("](repeating: ");
				WriteDefaultValue(array.GetElementType());
				Write(", count: ");
				VisitLiteralLong(array.Length);
				WriteChar(')');
			}
		}

		protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
		{
			WriteClassName(klass);
			Write("()");
		}

		void WriteDefaultValue(CiType type)
		{
			switch (type) {
			case CiNumericType _:
				WriteChar('0');
				break;
			case CiEnum enu:
				if (enu.Id == CiId.BoolType)
					Write("false");
				else {
					WriteName(enu);
					WriteChar('.');
					WriteName(enu.GetFirstValue());
				}
				break;
			case CiStringType _ when !type.Nullable:
				Write("\"\"");
				break;
			case CiArrayStorageType array:
				WriteNewArrayStorage(array);
				break;
			default:
				Write("nil");
				break;
			}
		}

		protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
		{
			this.ArrayRef = true;
			Write("ArrayRef<");
			WriteType(elementType);
			Write(">(");
			switch (elementType) {
			case CiArrayStorageType _:
				Write("factory: { ");
				WriteNewStorage(elementType);
				Write(" }");
				break;
			case CiStorageType klass:
				Write("factory: ");
				WriteName(klass.Class);
				Write(".init");
				break;
			default:
				Write("repeating: ");
				WriteDefaultValue(elementType);
				break;
			}
			Write(", count: ");
			lengthExpr.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		public override void VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
		{
			if (expr.Op == CiToken.Tilde && expr.Type is CiEnumFlags) {
				Write(expr.Type.Name);
				Write("(rawValue: ~");
				WritePostfix(expr.Inner, ".rawValue)");
			}
			else
				base.VisitPrefixExpr(expr, parent);
		}

		protected override void WriteIndexingExpr(CiBinaryExpr expr, CiPriority parent)
		{
			OpenIndexing(expr.Left);
			CiClassType klass = (CiClassType) expr.Left.Type;
			CiType indexType;
			switch (klass.Class.Id) {
			case CiId.ArrayPtrClass:
			case CiId.ArrayStorageClass:
			case CiId.ListClass:
				indexType = this.System.IntType;
				break;
			default:
				indexType = klass.GetKeyType();
				break;
			}
			WriteCoerced(indexType, expr.Right, CiPriority.Argument);
			WriteChar(']');
			if (parent != CiPriority.Assign && expr.Left.Type is CiClassType dict && dict.Class.TypeParameterCount == 2)
				WriteChar('!');
		}

		protected override void WriteBinaryOperand(CiExpr expr, CiPriority parent, CiBinaryExpr binary)
		{
			if (expr.Type.Id != CiId.BoolType) {
				if (binary.Op == CiToken.Plus && binary.Type.Id == CiId.StringStorageType) {
					WriteUnwrapped(expr, parent, true);
					return;
				}
				switch (binary.Op) {
				case CiToken.Plus:
				case CiToken.Minus:
				case CiToken.Asterisk:
				case CiToken.Slash:
				case CiToken.Mod:
				case CiToken.And:
				case CiToken.Or:
				case CiToken.Xor:
				case CiToken.ShiftLeft when expr == binary.Left:
				case CiToken.ShiftRight when expr == binary.Left:
					if (!(expr is CiLiteral)) {
						CiType type = this.System.PromoteNumericTypes(binary.Left.Type, binary.Right.Type);
						if (type != expr.Type) {
							WriteCoerced(type, expr, parent);
							return;
						}
					}
					break;
				case CiToken.Less:
				case CiToken.LessOrEqual:
				case CiToken.Greater:
				case CiToken.GreaterOrEqual:
				case CiToken.Equal:
				case CiToken.NotEqual:
					CiType typeComp = this.System.PromoteFloatingTypes(binary.Left.Type, binary.Right.Type);
					if (typeComp != null && typeComp != expr.Type) {
						WriteCoerced(typeComp, expr, parent);
						return;
					}
					break;
				default:
					break;
				}
			}
			expr.Accept(this, parent);
		}

		void WriteEnumFlagsAnd(CiExpr left, string method, string notMethod, CiExpr right)
		{
			if (right is CiPrefixExpr negation && negation.Op == CiToken.Tilde)
				WriteMethodCall(left, notMethod, negation.Inner);
			else
				WriteMethodCall(left, method, right);
		}

		CiExpr WriteAssignNested(CiBinaryExpr expr)
		{
			if (expr.Right is CiBinaryExpr rightBinary && rightBinary.IsAssign()) {
				VisitBinaryExpr(rightBinary, CiPriority.Statement);
				WriteNewLine();
				return rightBinary.Left;
			}
			return expr.Right;
		}

		void WriteSwiftAssign(CiBinaryExpr expr, CiExpr right)
		{
			expr.Left.Accept(this, CiPriority.Assign);
			WriteChar(' ');
			Write(expr.GetOpString());
			WriteChar(' ');
			if (right is CiLiteralNull && expr.Left is CiBinaryExpr leftBinary && leftBinary.Op == CiToken.LeftBracket && leftBinary.Left.Type is CiClassType dict && dict.Class.TypeParameterCount == 2) {
				WriteType(dict.GetValueType());
				Write(".none");
			}
			else
				WriteCoerced(expr.Type, right, CiPriority.Argument);
		}

		public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
		{
			CiExpr right;
			switch (expr.Op) {
			case CiToken.ShiftLeft:
				WriteBinaryExpr(expr, parent > CiPriority.Mul, CiPriority.Primary, " << ", CiPriority.Primary);
				break;
			case CiToken.ShiftRight:
				WriteBinaryExpr(expr, parent > CiPriority.Mul, CiPriority.Primary, " >> ", CiPriority.Primary);
				break;
			case CiToken.And:
				if (expr.Type.Id == CiId.BoolType)
					WriteCall("{ a, b in a && b }", expr.Left, expr.Right);
				else if (expr.Type is CiEnumFlags)
					WriteEnumFlagsAnd(expr.Left, "intersection", "subtracting", expr.Right);
				else
					WriteBinaryExpr(expr, parent > CiPriority.Mul, CiPriority.Mul, " & ", CiPriority.Primary);
				break;
			case CiToken.Or:
				if (expr.Type.Id == CiId.BoolType)
					WriteCall("{ a, b in a || b }", expr.Left, expr.Right);
				else if (expr.Type is CiEnumFlags)
					WriteMethodCall(expr.Left, "union", expr.Right);
				else
					WriteBinaryExpr(expr, parent > CiPriority.Add, CiPriority.Add, " | ", CiPriority.Mul);
				break;
			case CiToken.Xor:
				if (expr.Type.Id == CiId.BoolType)
					WriteEqual(expr.Left, expr.Right, parent, true);
				else if (expr.Type is CiEnumFlags)
					WriteMethodCall(expr.Left, "symmetricDifference", expr.Right);
				else
					WriteBinaryExpr(expr, parent > CiPriority.Add, CiPriority.Add, " ^ ", CiPriority.Mul);
				break;
			case CiToken.Assign:
			case CiToken.AddAssign:
			case CiToken.SubAssign:
			case CiToken.MulAssign:
			case CiToken.DivAssign:
			case CiToken.ModAssign:
			case CiToken.ShiftLeftAssign:
			case CiToken.ShiftRightAssign:
				WriteSwiftAssign(expr, WriteAssignNested(expr));
				break;
			case CiToken.AndAssign:
				right = WriteAssignNested(expr);
				if (expr.Type.Id == CiId.BoolType) {
					Write("if ");
					if (right is CiPrefixExpr negation && negation.Op == CiToken.ExclamationMark) {
						negation.Inner.Accept(this, CiPriority.Argument);
					}
					else {
						WriteChar('!');
						right.Accept(this, CiPriority.Primary);
					}
					OpenChild();
					expr.Left.Accept(this, CiPriority.Assign);
					WriteLine(" = false");
					this.Indent--;
					WriteChar('}');
				}
				else if (expr.Type is CiEnumFlags)
					WriteEnumFlagsAnd(expr.Left, "formIntersection", "subtract", right);
				else
					WriteSwiftAssign(expr, right);
				break;
			case CiToken.OrAssign:
				right = WriteAssignNested(expr);
				if (expr.Type.Id == CiId.BoolType) {
					Write("if ");
					right.Accept(this, CiPriority.Argument);
					OpenChild();
					expr.Left.Accept(this, CiPriority.Assign);
					WriteLine(" = true");
					this.Indent--;
					WriteChar('}');
				}
				else if (expr.Type is CiEnumFlags)
					WriteMethodCall(expr.Left, "formUnion", right);
				else
					WriteSwiftAssign(expr, right);
				break;
			case CiToken.XorAssign:
				right = WriteAssignNested(expr);
				if (expr.Type.Id == CiId.BoolType) {
					expr.Left.Accept(this, CiPriority.Assign);
					Write(" = ");
					expr.Left.Accept(this, CiPriority.Equality);
					Write(" != ");
					expr.Right.Accept(this, CiPriority.Equality);
				}
				else if (expr.Type is CiEnumFlags)
					WriteMethodCall(expr.Left, "formSymmetricDifference", right);
				else
					WriteSwiftAssign(expr, right);
				break;
			default:
				base.VisitBinaryExpr(expr, parent);
				break;
			}
		}

		protected override void WriteResource(string name, int length)
		{
			Write("CiResource.");
			WriteResourceName(name);
		}

		static bool Throws(CiExpr expr)
		{
			switch (expr) {
			case CiVar _:
			case CiLiteral _:
			case CiLambdaExpr _:
				return false;
			case CiAggregateInitializer init:
				return init.Items.Any(field => Throws(field));
			case CiInterpolatedString interp:
				return interp.Parts.Any(part => Throws(part.Argument));
			case CiSymbolReference symbol:
				return symbol.Left != null && Throws(symbol.Left);
			case CiUnaryExpr unary:
				return unary.Inner != null && Throws(unary.Inner);
			case CiBinaryExpr binary:
				return Throws(binary.Left) || Throws(binary.Right);
			case CiSelectExpr select:
				return Throws(select.Cond) || Throws(select.OnTrue) || Throws(select.OnFalse);
			case CiCallExpr call:
				CiMethod method = (CiMethod) call.Method.Symbol;
				return method.Throws || (call.Method.Left != null && Throws(call.Method.Left)) || call.Arguments.Any(arg => Throws(arg));
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteExpr(CiExpr expr, CiPriority parent)
		{
			if (Throws(expr))
				Write("try ");
			base.WriteExpr(expr, parent);
		}

		protected override void WriteCoercedExpr(CiType type, CiExpr expr)
		{
			if (Throws(expr))
				Write("try ");
			base.WriteCoercedExpr(type, expr);
		}

		protected override void StartTemporaryVar(CiType type)
		{
			Write("var ");
		}

		public override void VisitExpr(CiExpr statement)
		{
			WriteTemporaries(statement);
			if (statement is CiCallExpr call && statement.Type.Id != CiId.VoidType)
				Write("_ = ");
			base.VisitExpr(statement);
		}

		void InitVarsAtIndent()
		{
			while (this.VarsAtIndent.Count <= this.Indent) {
				this.VarsAtIndent.Add(new HashSet<string>());
				this.VarBytesAtIndent.Add(false);
			}
			this.VarsAtIndent[this.Indent].Clear();
			this.VarBytesAtIndent[this.Indent] = false;
		}

		protected override void OpenChild()
		{
			WriteChar(' ');
			OpenBlock();
			InitVarsAtIndent();
		}

		protected override void CloseChild()
		{
			CloseBlock();
		}

		protected override void WriteVar(CiNamedValue def)
		{
			if (def is CiField || AddVar(def.Name)) {
				Write((def.Type is CiArrayStorageType array ? IsArrayRef(array) : def.Type is CiStorageType stg ? stg.Class.TypeParameterCount == 0 && !def.IsAssignableStorage() : def is CiVar local && !local.IsAssigned) ? "let " : "var ");
				base.WriteVar(def);
			}
			else {
				WriteName(def);
				WriteVarInit(def);
			}
		}

		static bool NeedsVarBytes(List<CiStatement> statements)
		{
			int count = 0;
			foreach (CiStatement statement in statements) {
				if (statement is CiCallExpr call && call.Method.Symbol.Id == CiId.UTF8GetBytes) {
					if (++count == 2)
						return true;
				}
			}
			return false;
		}

		protected override void WriteStatements(List<CiStatement> statements)
		{
			this.VarBytesAtIndent[this.Indent] = NeedsVarBytes(statements);
			base.WriteStatements(statements);
		}

		public override void VisitLambdaExpr(CiLambdaExpr expr)
		{
			Write("{ ");
			WriteName(expr.First);
			Write(" in ");
			expr.Body.Accept(this, CiPriority.Statement);
			Write(" }");
		}

		protected override void WriteAssertCast(CiBinaryExpr expr)
		{
			Write("let ");
			CiVar def = (CiVar) expr.Right;
			WriteCamelCaseNotKeyword(def.Name);
			Write(" = ");
			expr.Left.Accept(this, CiPriority.Equality);
			Write(" as! ");
			WriteLine(def.Type.Name);
		}

		protected override void WriteAssert(CiAssert statement)
		{
			Write("assert(");
			WriteExpr(statement.Cond, CiPriority.Argument);
			if (statement.Message != null) {
				Write(", ");
				WriteExpr(statement.Message, CiPriority.Argument);
			}
			WriteCharLine(')');
		}

		public override void VisitBreak(CiBreak statement)
		{
			WriteLine("break");
		}

		protected override bool NeedCondXcrement(CiLoop loop) => loop.Cond != null && (!loop.HasBreak || !VisitXcrement(loop.Cond, true, false));

		protected override string GetIfNot() => "if !";

		protected override void WriteContinueDoWhile(CiExpr cond)
		{
			VisitXcrement(cond, false, true);
			WriteLine("continue");
		}

		public override void VisitDoWhile(CiDoWhile statement)
		{
			if (VisitXcrement(statement.Cond, true, false))
				base.VisitDoWhile(statement);
			else {
				Write("repeat");
				OpenChild();
				statement.Body.AcceptStatement(this);
				if (statement.Body.CompletesNormally())
					VisitXcrement(statement.Cond, false, true);
				CloseChild();
				Write("while ");
				WriteExpr(statement.Cond, CiPriority.Argument);
				WriteNewLine();
			}
		}

		protected override void WriteElseIf()
		{
			Write("else ");
		}

		protected override void OpenWhile(CiLoop loop)
		{
			if (NeedCondXcrement(loop))
				base.OpenWhile(loop);
			else {
				Write("while true");
				OpenChild();
				VisitXcrement(loop.Cond, false, true);
				Write("let ciDoLoop = ");
				loop.Cond.Accept(this, CiPriority.Argument);
				WriteNewLine();
				VisitXcrement(loop.Cond, true, true);
				Write("if !ciDoLoop");
				OpenChild();
				WriteLine("break");
				CloseChild();
			}
		}

		protected override void WriteForRange(CiVar iter, CiBinaryExpr cond, long rangeStep)
		{
			if (rangeStep == 1) {
				WriteExpr(iter.Value, CiPriority.Shift);
				switch (cond.Op) {
				case CiToken.Less:
					Write("..<");
					cond.Right.Accept(this, CiPriority.Shift);
					break;
				case CiToken.LessOrEqual:
					Write("...");
					cond.Right.Accept(this, CiPriority.Shift);
					break;
				default:
					throw new NotImplementedException();
				}
			}
			else {
				Write("stride(from: ");
				WriteExpr(iter.Value, CiPriority.Argument);
				switch (cond.Op) {
				case CiToken.Less:
				case CiToken.Greater:
					Write(", to: ");
					WriteExpr(cond.Right, CiPriority.Argument);
					break;
				case CiToken.LessOrEqual:
				case CiToken.GreaterOrEqual:
					Write(", through: ");
					WriteExpr(cond.Right, CiPriority.Argument);
					break;
				default:
					throw new NotImplementedException();
				}
				Write(", by: ");
				VisitLiteralLong(rangeStep);
				WriteChar(')');
			}
		}

		public override void VisitForeach(CiForeach statement)
		{
			Write("for ");
			if (statement.Count() == 2) {
				WriteChar('(');
				WriteName(statement.GetVar());
				Write(", ");
				WriteName(statement.GetValueVar());
				WriteChar(')');
			}
			else
				WriteName(statement.GetVar());
			Write(" in ");
			CiClassType klass = (CiClassType) statement.Collection.Type;
			switch (klass.Class.Id) {
			case CiId.StringClass:
				WritePostfix(statement.Collection, ".unicodeScalars");
				break;
			case CiId.SortedSetClass:
				WritePostfix(statement.Collection, ".sorted()");
				break;
			case CiId.SortedDictionaryClass:
				WritePostfix(statement.Collection, klass.GetKeyType().Nullable ? ".sorted(by: { $0.key! < $1.key! })" : ".sorted(by: { $0.key < $1.key })");
				break;
			default:
				WriteExpr(statement.Collection, CiPriority.Argument);
				break;
			}
			WriteChild(statement.Body);
		}

		public override void VisitLock(CiLock statement)
		{
			statement.Lock.Accept(this, CiPriority.Primary);
			WriteLine(".lock()");
			Write("do");
			OpenChild();
			Write("defer { ");
			statement.Lock.Accept(this, CiPriority.Primary);
			WriteLine(".unlock() }");
			statement.Body.AcceptStatement(this);
			CloseChild();
		}

		protected override void WriteResultVar()
		{
			Write("let result : ");
			WriteType(this.CurrentMethod.Type);
		}

		void WriteSwitchCaseVar(CiVar def)
		{
			if (def.Name == "_")
				Write("is ");
			else {
				Write("let ");
				WriteCamelCaseNotKeyword(def.Name);
				Write(" as ");
			}
			WriteType(def.Type);
		}

		void WriteSwiftSwitchCaseBody(CiSwitch statement, List<CiStatement> body)
		{
			this.Indent++;
			VisitXcrement(statement.Value, true, true);
			InitVarsAtIndent();
			WriteSwitchCaseBody(body);
			this.Indent--;
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			VisitXcrement(statement.Value, false, true);
			Write("switch ");
			WriteExpr(statement.Value, CiPriority.Argument);
			WriteLine(" {");
			foreach (CiCase kase in statement.Cases) {
				Write("case ");
				for (int i = 0; i < kase.Values.Count; i++) {
					WriteComma(i);
					switch (kase.Values[i]) {
					case CiBinaryExpr when1 when when1.Op == CiToken.When:
						if (when1.Left is CiVar whenVar)
							WriteSwitchCaseVar(whenVar);
						else
							WriteCoerced(statement.Value.Type, when1.Left, CiPriority.Argument);
						Write(" where ");
						WriteExpr(when1.Right, CiPriority.Argument);
						break;
					case CiVar def:
						WriteSwitchCaseVar(def);
						break;
					default:
						WriteCoerced(statement.Value.Type, kase.Values[i], CiPriority.Argument);
						break;
					}
				}
				WriteCharLine(':');
				WriteSwiftSwitchCaseBody(statement, kase.Body);
			}
			if (statement.DefaultBody.Count > 0) {
				WriteLine("default:");
				WriteSwiftSwitchCaseBody(statement, statement.DefaultBody);
			}
			WriteCharLine('}');
		}

		public override void VisitThrow(CiThrow statement)
		{
			this.Throw = true;
			VisitXcrement(statement.Message, false, true);
			Write("throw CiError.error(");
			WriteExpr(statement.Message, CiPriority.Argument);
			WriteCharLine(')');
		}

		void WriteReadOnlyParameter(CiVar param)
		{
			Write("ciParam");
			WritePascalCase(param.Name);
		}

		protected override void WriteParameter(CiVar param)
		{
			Write("_ ");
			if (param.IsAssigned)
				WriteReadOnlyParameter(param);
			else
				WriteName(param);
			Write(" : ");
			WriteType(param.Type);
		}

		public override void VisitEnumValue(CiConst konst, CiConst previous)
		{
			WriteDoc(konst.Documentation);
			Write("static let ");
			WriteName(konst);
			Write(" = ");
			Write(konst.Parent.Name);
			WriteChar('(');
			int i = konst.Value.IntValue();
			if (i == 0)
				Write("[]");
			else {
				Write("rawValue: ");
				VisitLiteralLong(i);
			}
			WriteCharLine(')');
		}

		protected override void WriteEnum(CiEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			WritePublic(enu);
			if (enu is CiEnumFlags) {
				Write("struct ");
				Write(enu.Name);
				WriteLine(" : OptionSet");
				OpenBlock();
				WriteLine("let rawValue : Int");
				enu.AcceptValues(this);
			}
			else {
				Write("enum ");
				Write(enu.Name);
				if (enu.HasExplicitValue)
					Write(" : Int");
				WriteNewLine();
				OpenBlock();
				Dictionary<int, CiConst> valueToConst = new Dictionary<int, CiConst>();
				for (CiSymbol symbol = enu.First; symbol != null; symbol = symbol.Next) {
					if (symbol is CiConst konst) {
						WriteDoc(konst.Documentation);
						int i = konst.Value.IntValue();
						if (valueToConst.ContainsKey(i)) {
							Write("static let ");
							WriteName(konst);
							Write(" = ");
							WriteName(valueToConst[i]);
						}
						else {
							Write("case ");
							WriteName(konst);
							if (!(konst.Value is CiImplicitEnumValue)) {
								Write(" = ");
								VisitLiteralLong(i);
							}
							valueToConst[i] = konst;
						}
						WriteNewLine();
					}
				}
			}
			CloseBlock();
		}

		void WriteVisibility(CiVisibility visibility)
		{
			switch (visibility) {
			case CiVisibility.Private:
				Write("private ");
				break;
			case CiVisibility.Internal:
				Write("fileprivate ");
				break;
			case CiVisibility.Protected:
			case CiVisibility.Public:
				Write("public ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteConst(CiConst konst)
		{
			WriteNewLine();
			WriteDoc(konst.Documentation);
			WriteVisibility(konst.Visibility);
			Write("static let ");
			WriteName(konst);
			Write(" = ");
			if (konst.Type.Id == CiId.IntType || konst.Type is CiEnum || konst.Type.Id == CiId.StringPtrType)
				konst.Value.Accept(this, CiPriority.Argument);
			else {
				WriteType(konst.Type);
				WriteChar('(');
				konst.Value.Accept(this, CiPriority.Argument);
				WriteChar(')');
			}
			WriteNewLine();
		}

		protected override void WriteField(CiField field)
		{
			WriteNewLine();
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			if (field.Type is CiClassType klass && klass.Class.Id != CiId.StringClass && !(klass is CiDynamicPtrType) && !(klass is CiStorageType))
				Write("unowned ");
			WriteVar(field);
			if (field.Value == null && (field.Type is CiNumericType || field.Type is CiEnum || field.Type.Id == CiId.StringStorageType)) {
				Write(" = ");
				WriteDefaultValue(field.Type);
			}
			else if (field.IsAssignableStorage()) {
				Write(" = ");
				WriteName(field.Type.AsClassType().Class);
				Write("()");
			}
			WriteNewLine();
		}

		protected override void WriteParameterDoc(CiVar param, bool first)
		{
			Write("/// - parameter ");
			WriteName(param);
			WriteChar(' ');
			WriteDocPara(param.Documentation.Summary, false);
			WriteNewLine();
		}

		protected override void WriteMethod(CiMethod method)
		{
			WriteNewLine();
			WriteDoc(method.Documentation);
			WriteParametersDoc(method);
			switch (method.CallType) {
			case CiCallType.Static:
				WriteVisibility(method.Visibility);
				Write("static ");
				break;
			case CiCallType.Normal:
				WriteVisibility(method.Visibility);
				break;
			case CiCallType.Abstract:
			case CiCallType.Virtual:
				Write(method.Visibility == CiVisibility.Internal ? "fileprivate " : "open ");
				break;
			case CiCallType.Override:
				Write(method.Visibility == CiVisibility.Internal ? "fileprivate " : "open ");
				Write("override ");
				break;
			case CiCallType.Sealed:
				WriteVisibility(method.Visibility);
				Write("final override ");
				break;
			}
			if (method.Id == CiId.ClassToString)
				Write("var description : String");
			else {
				Write("func ");
				WriteName(method);
				WriteParameters(method, true);
				if (method.Throws)
					Write(" throws");
				if (method.Type.Id != CiId.VoidType) {
					Write(" -> ");
					WriteType(method.Type);
				}
			}
			WriteNewLine();
			OpenBlock();
			if (method.CallType == CiCallType.Abstract)
				WriteLine("preconditionFailure(\"Abstract method called\")");
			else {
				for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
					if (param.IsAssigned) {
						Write("var ");
						WriteTypeAndName(param);
						Write(" = ");
						WriteReadOnlyParameter(param);
						WriteNewLine();
					}
				}
				InitVarsAtIndent();
				this.CurrentMethod = method;
				method.Body.AcceptStatement(this);
				this.CurrentMethod = null;
			}
			CloseBlock();
		}

		protected override void WriteClass(CiClass klass, CiProgram program)
		{
			WriteNewLine();
			WriteDoc(klass.Documentation);
			WritePublic(klass);
			if (klass.CallType == CiCallType.Sealed)
				Write("final ");
			StartClass(klass, "", " : ");
			if (klass.AddsToString()) {
				Write(klass.HasBaseClass() ? ", " : " : ");
				Write("CustomStringConvertible");
			}
			WriteNewLine();
			OpenBlock();
			if (NeedsConstructor(klass)) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				else
					Write("fileprivate ");
				if (klass.HasBaseClass())
					Write("override ");
				WriteLine("init()");
				OpenBlock();
				InitVarsAtIndent();
				WriteConstructorBody(klass);
				CloseBlock();
			}
			WriteMembers(klass, true);
			CloseBlock();
		}

		void WriteLibrary()
		{
			if (this.Throw) {
				WriteNewLine();
				WriteLine("public enum CiError : Error");
				OpenBlock();
				WriteLine("case error(String)");
				CloseBlock();
			}
			if (this.ArrayRef) {
				WriteNewLine();
				WriteLine("public class ArrayRef<T> : Sequence");
				OpenBlock();
				WriteLine("var array : [T]");
				WriteNewLine();
				WriteLine("init(_ array : [T])");
				OpenBlock();
				WriteLine("self.array = array");
				CloseBlock();
				WriteNewLine();
				WriteLine("init(repeating: T, count: Int)");
				OpenBlock();
				WriteLine("self.array = [T](repeating: repeating, count: count)");
				CloseBlock();
				WriteNewLine();
				WriteLine("init(factory: () -> T, count: Int)");
				OpenBlock();
				WriteLine("self.array = (1...count).map({_ in factory() })");
				CloseBlock();
				WriteNewLine();
				WriteLine("subscript(index: Int) -> T");
				OpenBlock();
				WriteLine("get");
				OpenBlock();
				WriteLine("return array[index]");
				CloseBlock();
				WriteLine("set(value)");
				OpenBlock();
				WriteLine("array[index] = value");
				CloseBlock();
				CloseBlock();
				WriteLine("subscript(bounds: Range<Int>) -> ArraySlice<T>");
				OpenBlock();
				WriteLine("get");
				OpenBlock();
				WriteLine("return array[bounds]");
				CloseBlock();
				WriteLine("set(value)");
				OpenBlock();
				WriteLine("array[bounds] = value");
				CloseBlock();
				CloseBlock();
				WriteNewLine();
				WriteLine("func fill(_ value: T)");
				OpenBlock();
				WriteLine("array = [T](repeating: value, count: array.count)");
				CloseBlock();
				WriteNewLine();
				WriteLine("func fill(_ value: T, _ startIndex : Int, _ count : Int)");
				OpenBlock();
				WriteLine("array[startIndex ..< startIndex + count] = ArraySlice(repeating: value, count: count)");
				CloseBlock();
				WriteNewLine();
				WriteLine("public func makeIterator() -> IndexingIterator<Array<T>>");
				OpenBlock();
				WriteLine("return array.makeIterator()");
				CloseBlock();
				CloseBlock();
			}
			if (this.StringCharAt) {
				WriteNewLine();
				WriteLine("fileprivate func ciStringCharAt(_ s: String, _ offset: Int) -> Int");
				OpenBlock();
				WriteLine("return Int(s.unicodeScalars[s.index(s.startIndex, offsetBy: offset)].value)");
				CloseBlock();
			}
			if (this.StringIndexOf) {
				WriteNewLine();
				WriteLine("fileprivate func ciStringIndexOf<S1 : StringProtocol, S2 : StringProtocol>(_ haystack: S1, _ needle: S2, _ options: String.CompareOptions = .literal) -> Int");
				OpenBlock();
				WriteLine("guard let index = haystack.range(of: needle, options: options) else { return -1 }");
				WriteLine("return haystack.distance(from: haystack.startIndex, to: index.lowerBound)");
				CloseBlock();
			}
			if (this.StringSubstring) {
				WriteNewLine();
				WriteLine("fileprivate func ciStringSubstring(_ s: String, _ offset: Int) -> Substring");
				OpenBlock();
				WriteLine("return s[s.index(s.startIndex, offsetBy: offset)...]");
				CloseBlock();
			}
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			if (resources.Count == 0)
				return;
			this.ArrayRef = true;
			WriteNewLine();
			WriteLine("fileprivate final class CiResource");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				Write("static let ");
				WriteResourceName(name);
				WriteLine(" = ArrayRef<UInt8>([");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" ])");
			}
			CloseBlock();
		}

		public override void WriteProgram(CiProgram program)
		{
			this.System = program.System;
			this.Throw = false;
			this.ArrayRef = false;
			this.StringCharAt = false;
			this.StringIndexOf = false;
			this.StringSubstring = false;
			OpenStringWriter();
			WriteTypes(program);
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteIncludes("import ", "");
			CloseStringWriter();
			WriteLibrary();
			WriteResources(program.Resources);
			CloseFile();
		}
	}

	public class GenPy : GenPySwift
	{

		bool ChildPass;

		bool SwitchBreak;

		protected override string GetTargetName() => "Python";

		protected override void WriteBanner()
		{
			WriteLine("# Generated automatically with \"cito\". Do not edit.");
		}

		protected override void StartDocLine()
		{
		}

		protected override string GetDocBullet() => " * ";

		void StartDoc(CiCodeDoc doc)
		{
			Write("\"\"\"");
			WriteDocPara(doc.Summary, false);
			if (doc.Details.Count > 0) {
				WriteNewLine();
				foreach (CiDocBlock block in doc.Details) {
					WriteNewLine();
					WriteDocBlock(block, false);
				}
			}
		}

		protected override void WriteDoc(CiCodeDoc doc)
		{
			if (doc != null) {
				StartDoc(doc);
				WriteLine("\"\"\"");
			}
		}

		protected override void WriteParameterDoc(CiVar param, bool first)
		{
			if (first) {
				WriteNewLine();
				WriteNewLine();
			}
			Write(":param ");
			WriteName(param);
			Write(": ");
			WriteDocPara(param.Documentation.Summary, false);
			WriteNewLine();
		}

		void WritePyDoc(CiMethod method)
		{
			if (method.Documentation == null)
				return;
			StartDoc(method.Documentation);
			WriteParametersDoc(method);
			WriteLine("\"\"\"");
		}

		public override void VisitLiteralNull()
		{
			Write("None");
		}

		public override void VisitLiteralFalse()
		{
			Write("False");
		}

		public override void VisitLiteralTrue()
		{
			Write("True");
		}

		void WriteNameNotKeyword(string name)
		{
			switch (name) {
			case "this":
				Write("self");
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
				Write(name);
				WriteChar('_');
				break;
			default:
				WriteLowercaseWithUnderscores(name);
				break;
			}
		}

		protected override void WriteName(CiSymbol symbol)
		{
			switch (symbol) {
			case CiContainerType container:
				if (!container.IsPublic)
					WriteChar('_');
				Write(symbol.Name);
				break;
			case CiConst konst:
				if (konst.Visibility != CiVisibility.Public)
					WriteChar('_');
				if (konst.InMethod != null) {
					WriteUppercaseWithUnderscores(konst.InMethod.Name);
					WriteChar('_');
				}
				WriteUppercaseWithUnderscores(symbol.Name);
				break;
			case CiVar _:
				WriteNameNotKeyword(symbol.Name);
				break;
			case CiMember member:
				if (member.Id == CiId.ClassToString)
					Write("__str__");
				else if (member.Visibility == CiVisibility.Public)
					WriteNameNotKeyword(symbol.Name);
				else {
					WriteChar('_');
					WriteLowercaseWithUnderscores(symbol.Name);
				}
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteTypeAndName(CiNamedValue value)
		{
			WriteName(value);
		}

		protected override void WriteLocalName(CiSymbol symbol, CiPriority parent)
		{
			if (symbol.Parent is CiForeach forEach && forEach.Collection.Type is CiStringType) {
				Write("ord(");
				WriteNameNotKeyword(symbol.Name);
				WriteChar(')');
			}
			else
				base.WriteLocalName(symbol, parent);
		}

		static int GetArrayCode(CiType type)
		{
			switch (type.Id) {
			case CiId.SByteRange:
				return 'b';
			case CiId.ByteRange:
				return 'B';
			case CiId.ShortRange:
				return 'h';
			case CiId.UShortRange:
				return 'H';
			case CiId.IntType:
				return 'i';
			case CiId.LongType:
				return 'q';
			case CiId.FloatType:
				return 'f';
			case CiId.DoubleType:
				return 'd';
			default:
				throw new NotImplementedException();
			}
		}

		public override void VisitAggregateInitializer(CiAggregateInitializer expr)
		{
			CiArrayStorageType array = (CiArrayStorageType) expr.Type;
			if (array.GetElementType() is CiNumericType number) {
				int c = GetArrayCode(number);
				if (c == 'B')
					Write("bytes(");
				else {
					Include("array");
					Write("array.array(\"");
					WriteChar(c);
					Write("\", ");
				}
				base.VisitAggregateInitializer(expr);
				WriteChar(')');
			}
			else
				base.VisitAggregateInitializer(expr);
		}

		public override void VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
		{
			Write("f\"");
			foreach (CiInterpolatedPart part in expr.Parts) {
				WriteDoubling(part.Prefix, '{');
				WriteChar('{');
				part.Argument.Accept(this, CiPriority.Argument);
				WritePyFormat(part);
			}
			WriteDoubling(expr.Suffix, '{');
			WriteChar('"');
		}

		public override void VisitPrefixExpr(CiPrefixExpr expr, CiPriority parent)
		{
			if (expr.Op == CiToken.ExclamationMark) {
				if (parent > CiPriority.CondAnd)
					WriteChar('(');
				Write("not ");
				expr.Inner.Accept(this, CiPriority.Or);
				if (parent > CiPriority.CondAnd)
					WriteChar(')');
			}
			else
				base.VisitPrefixExpr(expr, parent);
		}

		protected override string GetReferenceEqOp(bool not) => not ? " is not " : " is ";

		protected override void WriteCharAt(CiBinaryExpr expr)
		{
			Write("ord(");
			WriteIndexingExpr(expr, CiPriority.Argument);
			WriteChar(')');
		}

		protected override void WriteStringLength(CiExpr expr)
		{
			WriteCall("len", expr);
		}

		public override void VisitSymbolReference(CiSymbolReference expr, CiPriority parent)
		{
			switch (expr.Symbol.Id) {
			case CiId.ConsoleError:
				Include("sys");
				Write("sys.stderr");
				break;
			case CiId.ListCount:
			case CiId.QueueCount:
			case CiId.StackCount:
			case CiId.HashSetCount:
			case CiId.SortedSetCount:
			case CiId.DictionaryCount:
			case CiId.SortedDictionaryCount:
			case CiId.OrderedDictionaryCount:
				WriteStringLength(expr.Left);
				break;
			case CiId.MathNaN:
				Include("math");
				Write("math.nan");
				break;
			case CiId.MathNegativeInfinity:
				Include("math");
				Write("-math.inf");
				break;
			case CiId.MathPositiveInfinity:
				Include("math");
				Write("math.inf");
				break;
			default:
				if (!WriteJavaMatchProperty(expr, parent))
					base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		public override void VisitBinaryExpr(CiBinaryExpr expr, CiPriority parent)
		{
			switch (expr.Op) {
			case CiToken.Slash:
				if (expr.Type is CiIntegerType) {
					bool floorDiv;
					if (expr.Left is CiRangeType leftRange && leftRange.Min >= 0 && expr.Right is CiRangeType rightRange && rightRange.Min >= 0) {
						if (parent > CiPriority.Or)
							WriteChar('(');
						floorDiv = true;
					}
					else {
						Write("int(");
						floorDiv = false;
					}
					expr.Left.Accept(this, CiPriority.Mul);
					Write(floorDiv ? " // " : " / ");
					expr.Right.Accept(this, CiPriority.Primary);
					if (!floorDiv || parent > CiPriority.Or)
						WriteChar(')');
				}
				else
					base.VisitBinaryExpr(expr, parent);
				break;
			case CiToken.CondAnd:
				WriteBinaryExpr(expr, parent > CiPriority.CondAnd || parent == CiPriority.CondOr, CiPriority.CondAnd, " and ", CiPriority.CondAnd);
				break;
			case CiToken.CondOr:
				WriteBinaryExpr2(expr, parent, CiPriority.CondOr, " or ");
				break;
			case CiToken.Assign:
				if (this.AtLineStart) {
					for (CiExpr right = expr.Right; right is CiBinaryExpr rightBinary && rightBinary.IsAssign(); right = rightBinary.Right) {
						if (rightBinary.Op != CiToken.Assign) {
							VisitBinaryExpr(rightBinary, CiPriority.Statement);
							WriteNewLine();
							break;
						}
					}
				}
				expr.Left.Accept(this, CiPriority.Assign);
				Write(" = ");
				{
					(expr.Right is CiBinaryExpr rightBinary && rightBinary.IsAssign() && rightBinary.Op != CiToken.Assign ? rightBinary.Left : expr.Right).Accept(this, CiPriority.Assign);
				}
				break;
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
				{
					CiExpr right = expr.Right;
					if (right is CiBinaryExpr rightBinary && rightBinary.IsAssign()) {
						VisitBinaryExpr(rightBinary, CiPriority.Statement);
						WriteNewLine();
						right = rightBinary.Left;
					}
					expr.Left.Accept(this, CiPriority.Assign);
					WriteChar(' ');
					if (expr.Op == CiToken.DivAssign && expr.Type is CiIntegerType)
						WriteChar('/');
					Write(expr.GetOpString());
					WriteChar(' ');
					right.Accept(this, CiPriority.Argument);
				}
				break;
			case CiToken.Is:
				if (expr.Right is CiSymbolReference symbol) {
					Write("isinstance(");
					expr.Left.Accept(this, CiPriority.Argument);
					Write(", ");
					WriteName(symbol.Symbol);
					WriteChar(')');
				}
				else
					NotSupported(expr, "'is' with a variable");
				break;
			default:
				base.VisitBinaryExpr(expr, parent);
				break;
			}
		}

		protected override void WriteCoercedSelect(CiType type, CiSelectExpr expr, CiPriority parent)
		{
			if (parent > CiPriority.Select)
				WriteChar('(');
			WriteCoerced(type, expr.OnTrue, CiPriority.Select);
			Write(" if ");
			expr.Cond.Accept(this, CiPriority.SelectCond);
			Write(" else ");
			WriteCoerced(type, expr.OnFalse, CiPriority.Select);
			if (parent > CiPriority.Select)
				WriteChar(')');
		}

		void WriteDefaultValue(CiType type)
		{
			if (type is CiNumericType)
				WriteChar('0');
			else if (type.Id == CiId.BoolType)
				Write("False");
			else if (type.Id == CiId.StringStorageType)
				Write("\"\"");
			else
				Write("None");
		}

		void WritePyNewArray(CiType elementType, CiExpr value, CiExpr lengthExpr)
		{
			switch (elementType) {
			case CiStorageType _:
				Write("[ ");
				WriteNewStorage(elementType);
				Write(" for _ in range(");
				lengthExpr.Accept(this, CiPriority.Argument);
				Write(") ]");
				break;
			case CiNumericType _:
				int c = GetArrayCode(elementType);
				if (c == 'B' && (value == null || value.IsLiteralZero()))
					WriteCall("bytearray", lengthExpr);
				else {
					Include("array");
					Write("array.array(\"");
					WriteChar(c);
					Write("\", [ ");
					if (value == null)
						WriteChar('0');
					else
						value.Accept(this, CiPriority.Argument);
					Write(" ]) * ");
					lengthExpr.Accept(this, CiPriority.Mul);
				}
				break;
			default:
				Write("[ ");
				if (value == null)
					WriteDefaultValue(elementType);
				else
					value.Accept(this, CiPriority.Argument);
				Write(" ] * ");
				lengthExpr.Accept(this, CiPriority.Mul);
				break;
			}
		}

		protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
		{
			WritePyNewArray(elementType, null, lengthExpr);
		}

		protected override void WriteArrayStorageInit(CiArrayStorageType array, CiExpr value)
		{
			Write(" = ");
			WritePyNewArray(array.GetElementType(), null, array.LengthExpr);
		}

		protected override void WriteNew(CiReadWriteClassType klass, CiPriority parent)
		{
			switch (klass.Class.Id) {
			case CiId.ListClass:
			case CiId.StackClass:
				if (klass.GetElementType() is CiNumericType number) {
					int c = GetArrayCode(number);
					if (c == 'B')
						Write("bytearray()");
					else {
						Include("array");
						Write("array.array(\"");
						WriteChar(c);
						Write("\")");
					}
				}
				else
					Write("[]");
				break;
			case CiId.QueueClass:
				Include("collections");
				Write("collections.deque()");
				break;
			case CiId.HashSetClass:
			case CiId.SortedSetClass:
				Write("set()");
				break;
			case CiId.DictionaryClass:
			case CiId.SortedDictionaryClass:
				Write("{}");
				break;
			case CiId.OrderedDictionaryClass:
				Include("collections");
				Write("collections.OrderedDict()");
				break;
			case CiId.StringWriterClass:
				Include("io");
				Write("io.StringIO()");
				break;
			case CiId.LockClass:
				Include("threading");
				Write("threading.RLock()");
				break;
			default:
				WriteName(klass.Class);
				Write("()");
				break;
			}
		}

		void WriteContains(CiExpr haystack, CiExpr needle)
		{
			needle.Accept(this, CiPriority.Rel);
			Write(" in ");
			haystack.Accept(this, CiPriority.Rel);
		}

		void WriteSlice(CiExpr startIndex, CiExpr length)
		{
			WriteChar('[');
			startIndex.Accept(this, CiPriority.Argument);
			WriteChar(':');
			if (length != null)
				WriteAdd(startIndex, length);
			WriteChar(']');
		}

		void WriteAssignSorted(CiExpr obj, string byteArray)
		{
			Write(" = ");
			int c = GetArrayCode(obj.Type.AsClassType().GetElementType());
			if (c == 'B') {
				Write(byteArray);
				WriteChar('(');
			}
			else {
				Include("array");
				Write("array.array(\"");
				WriteChar(c);
				Write("\", ");
			}
			Write("sorted(");
		}

		void WriteAllAny(string function, CiExpr obj, List<CiExpr> args)
		{
			Write(function);
			WriteChar('(');
			CiLambdaExpr lambda = (CiLambdaExpr) args[0];
			lambda.Body.Accept(this, CiPriority.Argument);
			Write(" for ");
			WriteName(lambda.First);
			Write(" in ");
			obj.Accept(this, CiPriority.Argument);
			WriteChar(')');
		}

		void WritePyRegexOptions(List<CiExpr> args)
		{
			Include("re");
			WriteRegexOptions(args, ", ", " | ", "", "re.I", "re.M", "re.S");
		}

		void WriteRegexSearch(List<CiExpr> args)
		{
			Write("re.search(");
			args[1].Accept(this, CiPriority.Argument);
			Write(", ");
			args[0].Accept(this, CiPriority.Argument);
			WritePyRegexOptions(args);
			WriteChar(')');
		}

		protected override void WriteCallExpr(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
		{
			switch (method.Id) {
			case CiId.EnumFromInt:
				WriteName(method.Type);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.EnumHasFlag:
			case CiId.StringContains:
			case CiId.ListContains:
			case CiId.HashSetContains:
			case CiId.SortedSetContains:
			case CiId.DictionaryContainsKey:
			case CiId.SortedDictionaryContainsKey:
			case CiId.OrderedDictionaryContainsKey:
				WriteContains(obj, args[0]);
				break;
			case CiId.StringEndsWith:
				WriteMethodCall(obj, "endswith", args[0]);
				break;
			case CiId.StringIndexOf:
				WriteMethodCall(obj, "find", args[0]);
				break;
			case CiId.StringLastIndexOf:
				WriteMethodCall(obj, "rfind", args[0]);
				break;
			case CiId.StringStartsWith:
				WriteMethodCall(obj, "startswith", args[0]);
				break;
			case CiId.StringSubstring:
				obj.Accept(this, CiPriority.Primary);
				WriteSlice(args[0], args.Count == 2 ? args[1] : null);
				break;
			case CiId.ArrayBinarySearchAll:
				Include("bisect");
				WriteCall("bisect.bisect_left", obj, args[0]);
				break;
			case CiId.ArrayBinarySearchPart:
				Include("bisect");
				Write("bisect.bisect_left(");
				obj.Accept(this, CiPriority.Argument);
				Write(", ");
				args[0].Accept(this, CiPriority.Argument);
				Write(", ");
				args[1].Accept(this, CiPriority.Argument);
				Write(", ");
				args[2].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ArrayCopyTo:
			case CiId.ListCopyTo:
				args[1].Accept(this, CiPriority.Primary);
				WriteSlice(args[2], args[3]);
				Write(" = ");
				obj.Accept(this, CiPriority.Primary);
				WriteSlice(args[0], args[3]);
				break;
			case CiId.ArrayFillAll:
			case CiId.ArrayFillPart:
				obj.Accept(this, CiPriority.Primary);
				if (args.Count == 1) {
					Write("[:] = ");
					CiArrayStorageType array = (CiArrayStorageType) obj.Type;
					WritePyNewArray(array.GetElementType(), args[0], array.LengthExpr);
				}
				else {
					WriteSlice(args[1], args[2]);
					Write(" = ");
					WritePyNewArray(obj.Type.AsClassType().GetElementType(), args[0], args[2]);
				}
				break;
			case CiId.ArraySortAll:
			case CiId.ListSortAll:
				obj.Accept(this, CiPriority.Assign);
				WriteAssignSorted(obj, "bytearray");
				obj.Accept(this, CiPriority.Argument);
				Write("))");
				break;
			case CiId.ArraySortPart:
			case CiId.ListSortPart:
				obj.Accept(this, CiPriority.Primary);
				WriteSlice(args[0], args[1]);
				WriteAssignSorted(obj, "bytes");
				obj.Accept(this, CiPriority.Primary);
				WriteSlice(args[0], args[1]);
				Write("))");
				break;
			case CiId.ListAdd:
				WriteListAdd(obj, "append", args);
				break;
			case CiId.ListAddRange:
				obj.Accept(this, CiPriority.Assign);
				Write(" += ");
				args[0].Accept(this, CiPriority.Argument);
				break;
			case CiId.ListAll:
				WriteAllAny("all", obj, args);
				break;
			case CiId.ListAny:
				WriteAllAny("any", obj, args);
				break;
			case CiId.ListClear:
			case CiId.StackClear:
				if (obj.Type.AsClassType().GetElementType() is CiNumericType number && GetArrayCode(number) != 'B') {
					Write("del ");
					WritePostfix(obj, "[:]");
				}
				else
					WritePostfix(obj, ".clear()");
				break;
			case CiId.ListIndexOf:
				if (parent > CiPriority.Select)
					WriteChar('(');
				WriteMethodCall(obj, "index", args[0]);
				Write(" if ");
				WriteContains(obj, args[0]);
				Write(" else -1");
				if (parent > CiPriority.Select)
					WriteChar(')');
				break;
			case CiId.ListInsert:
				WriteListInsert(obj, "insert", args);
				break;
			case CiId.ListLast:
			case CiId.StackPeek:
				WritePostfix(obj, "[-1]");
				break;
			case CiId.ListRemoveAt:
			case CiId.DictionaryRemove:
			case CiId.SortedDictionaryRemove:
			case CiId.OrderedDictionaryRemove:
				Write("del ");
				WriteIndexing(obj, args[0]);
				break;
			case CiId.ListRemoveRange:
				Write("del ");
				obj.Accept(this, CiPriority.Primary);
				WriteSlice(args[0], args[1]);
				break;
			case CiId.QueueDequeue:
				WritePostfix(obj, ".popleft()");
				break;
			case CiId.QueueEnqueue:
			case CiId.StackPush:
				WriteListAppend(obj, args);
				break;
			case CiId.QueuePeek:
				WritePostfix(obj, "[0]");
				break;
			case CiId.DictionaryAdd:
				WriteDictionaryAdd(obj, args);
				break;
			case CiId.TextWriterWrite:
				Write("print(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", end=\"\", file=");
				obj.Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.TextWriterWriteChar:
				WriteMethodCall(obj, "write(chr", args[0]);
				WriteChar(')');
				break;
			case CiId.TextWriterWriteLine:
				Write("print(");
				if (args.Count == 1) {
					args[0].Accept(this, CiPriority.Argument);
					Write(", ");
				}
				Write("file=");
				obj.Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.ConsoleWrite:
				Write("print(");
				args[0].Accept(this, CiPriority.Argument);
				Write(", end=\"\")");
				break;
			case CiId.ConsoleWriteLine:
				Write("print(");
				if (args.Count == 1)
					args[0].Accept(this, CiPriority.Argument);
				WriteChar(')');
				break;
			case CiId.StringWriterClear:
				WritePostfix(obj, ".seek(0)");
				WriteNewLine();
				WritePostfix(obj, ".truncate(0)");
				break;
			case CiId.StringWriterToString:
				WritePostfix(obj, ".getvalue()");
				break;
			case CiId.UTF8GetByteCount:
				Write("len(");
				WritePostfix(args[0], ".encode(\"utf8\"))");
				break;
			case CiId.UTF8GetBytes:
				Write("cibytes = ");
				args[0].Accept(this, CiPriority.Primary);
				WriteLine(".encode(\"utf8\")");
				args[1].Accept(this, CiPriority.Primary);
				WriteChar('[');
				args[2].Accept(this, CiPriority.Argument);
				WriteChar(':');
				StartAdd(args[2]);
				WriteLine("len(cibytes)] = cibytes");
				break;
			case CiId.UTF8GetString:
				args[0].Accept(this, CiPriority.Primary);
				WriteSlice(args[1], args[2]);
				Write(".decode(\"utf8\")");
				break;
			case CiId.EnvironmentGetEnvironmentVariable:
				Include("os");
				WriteCall("os.getenv", args[0]);
				break;
			case CiId.RegexCompile:
				Write("re.compile(");
				args[0].Accept(this, CiPriority.Argument);
				WritePyRegexOptions(args);
				WriteChar(')');
				break;
			case CiId.RegexEscape:
				Include("re");
				WriteCall("re.escape", args[0]);
				break;
			case CiId.RegexIsMatchStr:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				WriteRegexSearch(args);
				Write(" is not None");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.RegexIsMatchRegex:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				WriteMethodCall(obj, "search", args[0]);
				Write(" is not None");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.MatchFindStr:
			case CiId.MatchFindRegex:
				if (parent > CiPriority.Equality)
					WriteChar('(');
				obj.Accept(this, CiPriority.Equality);
				Write(" is not None");
				if (parent > CiPriority.Equality)
					WriteChar(')');
				break;
			case CiId.MatchGetCapture:
				WriteMethodCall(obj, "group", args[0]);
				break;
			case CiId.MathMethod:
			case CiId.MathIsFinite:
			case CiId.MathIsNaN:
			case CiId.MathLog2:
				Include("math");
				Write("math.");
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case CiId.MathAbs:
				WriteCall("abs", args[0]);
				break;
			case CiId.MathCeiling:
				Include("math");
				WriteCall("math.ceil", args[0]);
				break;
			case CiId.MathClamp:
				Write("min(max(");
				WriteClampAsMinMax(args);
				break;
			case CiId.MathFusedMultiplyAdd:
				Include("pyfma");
				WriteCall("pyfma.fma", args[0], args[1], args[2]);
				break;
			case CiId.MathIsInfinity:
				Include("math");
				WriteCall("math.isinf", args[0]);
				break;
			case CiId.MathMaxInt:
			case CiId.MathMaxDouble:
				WriteCall("max", args[0], args[1]);
				break;
			case CiId.MathMinInt:
			case CiId.MathMinDouble:
				WriteCall("min", args[0], args[1]);
				break;
			case CiId.MathRound:
				WriteCall("round", args[0]);
				break;
			case CiId.MathTruncate:
				Include("math");
				WriteCall("math.trunc", args[0]);
				break;
			default:
				if (obj == null)
					WriteLocalName(method, CiPriority.Primary);
				else if (IsReferenceTo(obj, CiId.BasePtr)) {
					WriteName(method.Parent);
					WriteChar('.');
					WriteName(method);
					Write("(self");
					if (args.Count > 0) {
						Write(", ");
						WriteArgs(method, args);
					}
					WriteChar(')');
					break;
				}
				else {
					obj.Accept(this, CiPriority.Primary);
					WriteChar('.');
					WriteName(method);
				}
				WriteArgsInParentheses(method, args);
				break;
			}
		}

		protected override void WriteResource(string name, int length)
		{
			Write("_CiResource.");
			WriteResourceName(name);
		}

		protected override bool VisitPreCall(CiCallExpr call)
		{
			switch (call.Method.Symbol.Id) {
			case CiId.MatchFindStr:
				call.Method.Left.Accept(this, CiPriority.Assign);
				Write(" = ");
				WriteRegexSearch(call.Arguments);
				WriteNewLine();
				return true;
			case CiId.MatchFindRegex:
				call.Method.Left.Accept(this, CiPriority.Assign);
				Write(" = ");
				WriteMethodCall(call.Arguments[1], "search", call.Arguments[0]);
				WriteNewLine();
				return true;
			default:
				return false;
			}
		}

		protected override void StartTemporaryVar(CiType type)
		{
		}

		protected override bool HasInitCode(CiNamedValue def) => (def.Value != null || def.Type.IsFinal()) && !def.IsAssignableStorage();

		public override void VisitExpr(CiExpr statement)
		{
			if (!(statement is CiVar def) || HasInitCode(def)) {
				WriteTemporaries(statement);
				base.VisitExpr(statement);
			}
		}

		protected override void StartLine()
		{
			base.StartLine();
			this.ChildPass = false;
		}

		protected override void OpenChild()
		{
			WriteCharLine(':');
			this.Indent++;
			this.ChildPass = true;
		}

		protected override void CloseChild()
		{
			if (this.ChildPass)
				WriteLine("pass");
			this.Indent--;
		}

		public override void VisitLambdaExpr(CiLambdaExpr expr)
		{
			throw new NotImplementedException();
		}

		protected override void WriteAssertCast(CiBinaryExpr expr)
		{
			CiVar def = (CiVar) expr.Right;
			Write(def.Name);
			Write(" = ");
			expr.Left.Accept(this, CiPriority.Argument);
			WriteNewLine();
		}

		protected override void WriteAssert(CiAssert statement)
		{
			Write("assert ");
			statement.Cond.Accept(this, CiPriority.Argument);
			if (statement.Message != null) {
				Write(", ");
				statement.Message.Accept(this, CiPriority.Argument);
			}
			WriteNewLine();
		}

		public override void VisitBreak(CiBreak statement)
		{
			WriteLine(statement.LoopOrSwitch is CiSwitch ? "raise _CiBreak()" : "break");
		}

		protected override string GetIfNot() => "if not ";

		void WriteInclusiveLimit(CiExpr limit, int increment, string incrementString)
		{
			if (limit is CiLiteralLong literal)
				VisitLiteralLong(literal.Value + increment);
			else {
				limit.Accept(this, CiPriority.Add);
				Write(incrementString);
			}
		}

		protected override void WriteForRange(CiVar iter, CiBinaryExpr cond, long rangeStep)
		{
			Write("range(");
			if (rangeStep != 1 || !iter.Value.IsLiteralZero()) {
				iter.Value.Accept(this, CiPriority.Argument);
				Write(", ");
			}
			switch (cond.Op) {
			case CiToken.Less:
			case CiToken.Greater:
				cond.Right.Accept(this, CiPriority.Argument);
				break;
			case CiToken.LessOrEqual:
				WriteInclusiveLimit(cond.Right, 1, " + 1");
				break;
			case CiToken.GreaterOrEqual:
				WriteInclusiveLimit(cond.Right, -1, " - 1");
				break;
			default:
				throw new NotImplementedException();
			}
			if (rangeStep != 1) {
				Write(", ");
				VisitLiteralLong(rangeStep);
			}
			WriteChar(')');
		}

		public override void VisitForeach(CiForeach statement)
		{
			Write("for ");
			WriteName(statement.GetVar());
			CiClassType klass = (CiClassType) statement.Collection.Type;
			if (klass.Class.TypeParameterCount == 2) {
				Write(", ");
				WriteName(statement.GetValueVar());
				Write(" in ");
				if (klass.Class.Id == CiId.SortedDictionaryClass) {
					Write("sorted(");
					WritePostfix(statement.Collection, ".items())");
				}
				else
					WritePostfix(statement.Collection, ".items()");
			}
			else {
				Write(" in ");
				if (klass.Class.Id == CiId.SortedSetClass)
					WriteCall("sorted", statement.Collection);
				else
					statement.Collection.Accept(this, CiPriority.Argument);
			}
			WriteChild(statement.Body);
		}

		protected override void WriteElseIf()
		{
			Write("el");
		}

		public override void VisitLock(CiLock statement)
		{
			VisitXcrement(statement.Lock, false, true);
			Write("with ");
			statement.Lock.Accept(this, CiPriority.Argument);
			OpenChild();
			VisitXcrement(statement.Lock, true, true);
			statement.Body.AcceptStatement(this);
			CloseChild();
		}

		protected override void WriteResultVar()
		{
			Write("result");
		}

		void WriteSwitchCaseVar(CiVar def)
		{
			WriteName(def.Type.AsClassType().Class);
			Write("()");
			if (def.Name != "_") {
				Write(" as ");
				WriteNameNotKeyword(def.Name);
			}
		}

		void WritePyCaseBody(CiSwitch statement, List<CiStatement> body)
		{
			OpenChild();
			VisitXcrement(statement.Value, true, true);
			WriteFirstStatements(body, CiSwitch.LengthWithoutTrailingBreak(body));
			CloseChild();
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			bool earlyBreak = statement.Cases.Any(kase => CiSwitch.HasEarlyBreak(kase.Body)) || CiSwitch.HasEarlyBreak(statement.DefaultBody);
			if (earlyBreak) {
				this.SwitchBreak = true;
				Write("try");
				OpenChild();
			}
			VisitXcrement(statement.Value, false, true);
			Write("match ");
			statement.Value.Accept(this, CiPriority.Argument);
			OpenChild();
			foreach (CiCase kase in statement.Cases) {
				string op = "case ";
				foreach (CiExpr caseValue in kase.Values) {
					Write(op);
					switch (caseValue) {
					case CiVar def:
						WriteSwitchCaseVar(def);
						break;
					case CiBinaryExpr when1:
						if (when1.Left is CiVar whenVar)
							WriteSwitchCaseVar(whenVar);
						else
							when1.Left.Accept(this, CiPriority.Argument);
						Write(" if ");
						when1.Right.Accept(this, CiPriority.Argument);
						break;
					default:
						caseValue.Accept(this, CiPriority.Or);
						break;
					}
					op = " | ";
				}
				WritePyCaseBody(statement, kase.Body);
			}
			if (statement.HasDefault()) {
				Write("case _");
				WritePyCaseBody(statement, statement.DefaultBody);
			}
			CloseChild();
			if (earlyBreak) {
				CloseChild();
				Write("except _CiBreak");
				OpenChild();
				CloseChild();
			}
		}

		public override void VisitThrow(CiThrow statement)
		{
			VisitXcrement(statement.Message, false, true);
			Write("raise Exception(");
			statement.Message.Accept(this, CiPriority.Argument);
			WriteCharLine(')');
		}

		public override void VisitEnumValue(CiConst konst, CiConst previous)
		{
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			VisitLiteralLong(konst.Value.IntValue());
			WriteNewLine();
			WriteDoc(konst.Documentation);
		}

		protected override void WriteEnum(CiEnum enu)
		{
			Include("enum");
			WriteNewLine();
			Write("class ");
			WriteName(enu);
			Write(enu is CiEnumFlags ? "(enum.Flag)" : "(enum.Enum)");
			OpenChild();
			WriteDoc(enu.Documentation);
			enu.AcceptValues(this);
			CloseChild();
		}

		protected override void WriteConst(CiConst konst)
		{
			if (konst.Visibility != CiVisibility.Private || konst.Type is CiArrayStorageType) {
				WriteNewLine();
				WriteName(konst);
				Write(" = ");
				konst.Value.Accept(this, CiPriority.Argument);
				WriteNewLine();
				WriteDoc(konst.Documentation);
			}
		}

		protected override void WriteField(CiField field)
		{
		}

		protected override void WriteMethod(CiMethod method)
		{
			if (method.CallType == CiCallType.Abstract)
				return;
			WriteNewLine();
			if (method.CallType == CiCallType.Static)
				WriteLine("@staticmethod");
			Write("def ");
			WriteName(method);
			if (method.CallType == CiCallType.Static)
				WriteParameters(method, true);
			else {
				Write("(self");
				WriteRemainingParameters(method, false, true);
			}
			this.CurrentMethod = method;
			OpenChild();
			WritePyDoc(method);
			method.Body.AcceptStatement(this);
			CloseChild();
			this.CurrentMethod = null;
		}

		bool InheritsConstructor(CiClass klass)
		{
			while (klass.Parent is CiClass baseClass) {
				if (NeedsConstructor(baseClass))
					return true;
				klass = baseClass;
			}
			return false;
		}

		protected override void WriteInitField(CiField field)
		{
			if (HasInitCode(field)) {
				Write("self.");
				WriteVar(field);
				WriteNewLine();
				WriteInitCode(field);
			}
		}

		protected override void WriteClass(CiClass klass, CiProgram program)
		{
			if (!WriteBaseClass(klass, program))
				return;
			WriteNewLine();
			Write("class ");
			WriteName(klass);
			if (klass.Parent is CiClass baseClass) {
				WriteChar('(');
				WriteName(baseClass);
				WriteChar(')');
			}
			OpenChild();
			WriteDoc(klass.Documentation);
			if (NeedsConstructor(klass)) {
				WriteNewLine();
				Write("def __init__(self)");
				OpenChild();
				if (klass.Constructor != null)
					WriteDoc(klass.Constructor.Documentation);
				if (InheritsConstructor(klass)) {
					WriteName(klass.Parent);
					WriteLine(".__init__(self)");
				}
				WriteConstructorBody(klass);
				CloseChild();
			}
			WriteMembers(klass, true);
			CloseChild();
		}

		void WriteResourceByte(int b)
		{
			Write($"\\x{b:x2}");
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			if (resources.Count == 0)
				return;
			WriteNewLine();
			Write("class _CiResource");
			OpenChild();
			foreach ((string name, List<byte> content) in resources) {
				WriteResourceName(name);
				WriteLine(" = (");
				this.Indent++;
				Write("b\"");
				int i = 0;
				foreach (int b in content) {
					if (i > 0 && (i & 15) == 0) {
						WriteCharLine('"');
						Write("b\"");
					}
					WriteResourceByte(b);
					i++;
				}
				WriteLine("\" )");
				this.Indent--;
			}
			CloseChild();
		}

		public override void WriteProgram(CiProgram program)
		{
			this.SwitchBreak = false;
			OpenStringWriter();
			WriteTypes(program);
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteIncludes("import ", "");
			if (this.SwitchBreak) {
				WriteNewLine();
				WriteLine("class _CiBreak(Exception): pass");
			}
			CloseStringWriter();
			WriteResources(program.Resources);
			CloseFile();
		}
	}
}
