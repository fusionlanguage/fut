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
		TextWriterWriteLine,
		ConsoleWrite,
		ConsoleWriteLine,
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

	public class CiLiteralNull : CiLiteral
	{

		public override bool IsDefaultValue() => true;

		public override void Accept(CiExprVisitor visitor, CiPriority parent)
		{
			visitor.VisitLiteralNull();
		}

		public override string ToString() => "null";
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

		internal bool Nullable = false;

		public virtual string GetArraySuffix() => "";

		public virtual bool IsAssignableFrom(CiType right) => this == right;

		public virtual bool EqualsType(CiType right) => this == right;

		public virtual bool IsArray() => false;

		public virtual bool IsFinal() => false;

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

		static void AddMinMaxValue(CiRangeType target, string name, int value)
		{
			CiRangeType type = target.Min == target.Max ? target : new CiRangeType { Min = value, Max = value };
			target.Add(new CiConst { Visibility = CiVisibility.Public, Name = name, Value = new CiLiteralLong { Type = type, Value = value }, VisitStatus = CiVisitStatus.Done });
		}

		public static CiRangeType New(int min, int max)
		{
			Debug.Assert(min <= max);
			CiRangeType result = new CiRangeType { Id = min >= -128 && max <= 127 ? CiId.SByteRange : min >= 0 && max <= 255 ? CiId.ByteRange : min >= -32768 && max <= 32767 ? CiId.ShortRange : min >= 0 && max <= 65535 ? CiId.UShortRange : CiId.IntType, Min = min, Max = max };
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

		public override bool EqualsType(CiType right) => right is CiClassType that && this.Class == that.Class && EqualTypeArguments(that);

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

		public override string GetArraySuffix() => IsArray() ? "[]!" : "";

		public override string GetClassSuffix() => "!";
	}

	public class CiStorageType : CiReadWriteClassType
	{

		public override bool IsFinal() => this.Class.Id != CiId.MatchClass;

		public override bool IsAssignableFrom(CiType right) => right is CiStorageType rightClass && this.Class == rightClass.Class && EqualTypeArguments(rightClass);

		public override string GetClassSuffix() => "()";
	}

	public class CiDynamicPtrType : CiReadWriteClassType
	{

		public override bool IsAssignableFrom(CiType right)
		{
			return (this.Nullable && right.Id == CiId.NullType) || (right is CiDynamicPtrType rightClass && IsAssignableFromClass(rightClass));
		}

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
			CiClass stringClass = CiClass.New(CiCallType.Normal, CiId.StringClass, "string");
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringContains, "Contains", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringEndsWith, "EndsWith", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, minus1Type, CiId.StringIndexOf, "IndexOf", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, minus1Type, CiId.StringLastIndexOf, "LastIndexOf", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiProperty.New(this.UIntType, CiId.StringLength, "Length"));
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.StringStorageType, CiId.StringReplace, "Replace", CiVar.New(this.StringPtrType, "oldValue"), CiVar.New(this.StringPtrType, "newValue")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.BoolType, CiId.StringStartsWith, "StartsWith", CiVar.New(this.StringPtrType, "value")));
			stringClass.Add(CiMethod.New(CiVisibility.Public, this.StringStorageType, CiId.StringSubstring, "Substring", CiVar.New(this.IntType, "offset"), CiVar.New(this.IntType, "length", NewLiteralLong(-1))));
			this.StringPtrType.Class = stringClass;
			Add(this.StringPtrType);
			this.StringNullablePtrType.Class = stringClass;
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
			textWriterClass.Add(CiMethod.NewMutator(CiVisibility.Public, this.VoidType, CiId.TextWriterWriteLine, "WriteLine", CiVar.New(this.PrintableType, "value", NewLiteralString(""))));
			Add(textWriterClass);
			CiClass consoleClass = CiClass.New(CiCallType.Static, CiId.None, "Console");
			consoleClass.Add(CiMethod.NewStatic(this.VoidType, CiId.ConsoleWrite, "Write", CiVar.New(this.PrintableType, "value")));
			consoleClass.Add(CiMethod.NewStatic(this.VoidType, CiId.ConsoleWriteLine, "WriteLine", CiVar.New(this.PrintableType, "value", NewLiteralString(""))));
			consoleClass.Add(CiStaticProperty.New(new CiStorageType { Class = textWriterClass }, CiId.ConsoleError, "Error"));
			Add(consoleClass);
			CiClass stringWriterClass = CiClass.New(CiCallType.Sealed, CiId.StringWriterClass, "StringWriter");
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
			CiEnumFlags regexOptionsEnum = NewEnumFlags();
			regexOptionsEnum.Id = CiId.RegexOptionsEnum;
			regexOptionsEnum.Name = "RegexOptions";
			CiConst regexOptionsNone = NewConstLong("None", 0);
			AddEnumValue(regexOptionsEnum, regexOptionsNone);
			AddEnumValue(regexOptionsEnum, NewConstLong("IgnoreCase", 1));
			AddEnumValue(regexOptionsEnum, NewConstLong("Multiline", 2));
			AddEnumValue(regexOptionsEnum, NewConstLong("Singleline", 16));
			Add(regexOptionsEnum);
			CiClass regexClass = CiClass.New(CiCallType.Sealed, CiId.RegexClass, "Regex");
			regexClass.Add(CiMethod.NewStatic(this.StringStorageType, CiId.RegexEscape, "Escape", CiVar.New(this.StringPtrType, "str")));
			regexClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.BoolType, CiId.RegexIsMatchStr, "IsMatch", CiVar.New(this.StringPtrType, "input"), CiVar.New(this.StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)), CiMethod.New(CiVisibility.Public, this.BoolType, CiId.RegexIsMatchRegex, "IsMatch", CiVar.New(this.StringPtrType, "input"))));
			regexClass.Add(CiMethod.NewStatic(new CiDynamicPtrType { Class = regexClass }, CiId.RegexCompile, "Compile", CiVar.New(this.StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)));
			Add(regexClass);
			CiClass matchClass = CiClass.New(CiCallType.Sealed, CiId.MatchClass, "Match");
			matchClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.MatchFindStr, "Find", CiVar.New(this.StringPtrType, "input"), CiVar.New(this.StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)), CiMethod.NewMutator(CiVisibility.Public, this.BoolType, CiId.MatchFindRegex, "Find", CiVar.New(this.StringPtrType, "input"), CiVar.New(new CiClassType { Class = regexClass }, "pattern"))));
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
			mathClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.IntType, CiId.MathMaxInt, "Max", CiVar.New(this.LongType, "a"), CiVar.New(this.LongType, "b")), CiMethod.NewStatic(this.FloatType, CiId.MathMaxDouble, "Max", CiVar.New(this.DoubleType, "a"), CiVar.New(this.DoubleType, "b"))));
			mathClass.Add(CiMethodGroup.New(CiMethod.NewStatic(this.IntType, CiId.MathMinInt, "Min", CiVar.New(this.LongType, "a"), CiVar.New(this.LongType, "b")), CiMethod.NewStatic(this.FloatType, CiId.MathMinDouble, "Min", CiVar.New(this.DoubleType, "a"), CiVar.New(this.DoubleType, "b"))));
			mathClass.Add(CiStaticProperty.New(this.FloatType, CiId.MathNaN, "NaN"));
			mathClass.Add(CiStaticProperty.New(this.FloatType, CiId.MathNegativeInfinity, "NegativeInfinity"));
			mathClass.Add(NewConstDouble("PI", 3.1415926535897931));
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

		internal CiStringType StringPtrType = new CiStringType { Id = CiId.StringPtrType, Name = "string" };

		internal CiStringType StringNullablePtrType = new CiStringType { Id = CiId.StringPtrType, Name = "string", Nullable = true };

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

		internal CiEnumFlags NewEnumFlags()
		{
			CiEnumFlags enu = new CiEnumFlags();
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

		internal readonly Dictionary<string, byte[]> Resources = new Dictionary<string, byte[]>();
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
					if (See(CiToken.Id)) {
						expr = ParseVar(expr);
						if (Eat(CiToken.When))
							expr = new CiBinaryExpr { Line = this.Line, Left = expr, Op = CiToken.When, Right = ParseExpr() };
					}
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
			CiEnum enu = flags ? this.Program.System.NewEnumFlags() : new CiEnum();
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

	public class CiSema : CiVisitor
	{

		protected CiProgram Program;

		CiMethodBase CurrentMethod;

		CiScope CurrentScope;

		readonly HashSet<CiMethod> CurrentPureMethods = new HashSet<CiMethod>();

		readonly Dictionary<CiVar, CiExpr> CurrentPureArguments = new Dictionary<CiVar, CiExpr>();

		CiType Poison = new CiType { Name = "poison" };

		protected override CiContainerType GetCurrentContainer() => this.CurrentScope.GetContainer();

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
				CiExpr arg = Resolve(part.Argument);
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
				if (resolved is CiSymbolReference symbol && symbol.Symbol is CiVar v) {
					if (v.Parent is CiFor loop)
						loop.IsIteratorUsed = true;
					else if (this.CurrentPureArguments.ContainsKey(v))
						return this.CurrentPureArguments[v];
				}
				return resolved;
			}
			CiExpr left = Resolve(expr.Left);
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
					CiClassType dictionary = (CiClassType) left.Type;
					if (!dictionary.GetValueType().IsFinal())
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
			if (expr.Inner is CiBinaryExpr binaryNew && binaryNew.Op == CiToken.LeftBrace) {
				if (!(ToType(binaryNew.Left, true) is CiClassType klass) || klass is CiReadWriteClassType)
					return PoisonError(expr, "Invalid argument to new");
				CiAggregateInitializer init = (CiAggregateInitializer) binaryNew.Right;
				ResolveObjectLiteral(klass, init);
				expr.Type = new CiDynamicPtrType { Line = expr.Line, Class = klass.Class };
				expr.Inner = init;
				return expr;
			}
			switch (ToType(expr.Inner, true)) {
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
				inner = Resolve(expr.Inner);
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
				inner = Resolve(expr.Inner);
				Coerce(inner, this.Program.System.DoubleType);
				if (inner.Type is CiRangeType negRange)
					type = CiRangeType.New(SaturatedNeg(negRange.Max), SaturatedNeg(negRange.Min));
				else if (inner is CiLiteralDouble d)
					return ToLiteralDouble(expr, -d.Value);
				else if (inner is CiLiteralLong l)
					return ToLiteralLong(expr, -l.Value);
				else
					type = inner.Type;
				break;
			case CiToken.Tilde:
				inner = Resolve(expr.Inner);
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
			if (type is CiRangeType range && range.Min == range.Max)
				return ToLiteralLong(expr, range.Min);
			return new CiPrefixExpr { Line = expr.Line, Op = expr.Op, Inner = inner, Type = type };
		}

		CiExpr VisitPostfixExpr(CiPostfixExpr expr)
		{
			expr.Inner = Resolve(expr.Inner);
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

		CiExpr ResolveEquality(CiBinaryExpr expr, CiExpr left, CiExpr right)
		{
			if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange) {
				if (leftRange.Min == leftRange.Max && leftRange.Min == rightRange.Min && leftRange.Min == rightRange.Max)
					return ToLiteralBool(expr, expr.Op == CiToken.Equal);
				if (leftRange.Max < rightRange.Min || leftRange.Min > rightRange.Max)
					return ToLiteralBool(expr, expr.Op == CiToken.NotEqual);
			}
			else if (left.Type == right.Type) {
				switch (left) {
				case CiLiteralLong leftLong when right is CiLiteralLong rightLong:
					return ToLiteralBool(expr, expr.Op == CiToken.NotEqual ^ leftLong.Value == rightLong.Value);
				case CiLiteralDouble leftDouble when right is CiLiteralDouble rightDouble:
					return ToLiteralBool(expr, expr.Op == CiToken.NotEqual ^ leftDouble.Value == rightDouble.Value);
				case CiLiteralString leftString when right is CiLiteralString rightString:
					return ToLiteralBool(expr, expr.Op == CiToken.NotEqual ^ leftString.Value == rightString.Value);
				case CiLiteralNull _:
					return ToLiteralBool(expr, expr.Op == CiToken.Equal);
				case CiLiteralFalse _:
					return ToLiteralBool(expr, expr.Op == CiToken.NotEqual ^ right is CiLiteralFalse);
				case CiLiteralTrue _:
					return ToLiteralBool(expr, expr.Op == CiToken.NotEqual ^ right is CiLiteralTrue);
				default:
					break;
				}
				if (left.IsConstEnum() && right.IsConstEnum())
					return ToLiteralBool(expr, expr.Op == CiToken.NotEqual ^ left.IntValue() == right.IntValue());
			}
			if (!left.Type.IsAssignableFrom(right.Type) && !right.Type.IsAssignableFrom(left.Type))
				return PoisonError(expr, $"Cannot compare {left.Type} with {right.Type}");
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
			CiExpr left = Resolve(expr.Left);
			CiExpr right = Resolve(expr.Right);
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
			if (!(Resolve(expr.Method) is CiSymbolReference symbol))
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
					CiClassType klass = (CiClassType) symbol.Left.Type;
					lambda.First.Type = klass.TypeArg0;
					OpenScope(lambda);
					lambda.Body = Resolve(lambda.Body);
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
				CiExpr result = Resolve(ret.Value);
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
						arguments[i] = Resolve(arguments[i]);
				}
				return ResolveCallWithArguments(expr, arguments);
			}
			else {
				List<CiExpr> arguments = new List<CiExpr>();
				foreach (CiExpr arg in expr.Arguments)
					arguments.Add(Resolve(arg));
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
					field.Right = Resolve(field.Right);
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
					expr.Value = Resolve(expr.Value);
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

		CiExpr Resolve(CiExpr expr)
		{
			switch (expr) {
			case CiAggregateInitializer aggregate:
				List<CiExpr> items = aggregate.Items;
				for (int i = 0; i < items.Count; i++)
					items[i] = Resolve(items[i]);
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

		public override void VisitExpr(CiExpr statement)
		{
			Resolve(statement);
		}

		CiExpr ResolveBool(CiExpr expr)
		{
			expr = Resolve(expr);
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
						CiExpr lengthExpr = Resolve(binary.Right);
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

		public override void VisitAssert(CiAssert statement)
		{
			statement.Cond = ResolveBool(statement.Cond);
			if (statement.Message != null) {
				statement.Message = Resolve(statement.Message);
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

		void ResolveLoopCond(CiLoop statement)
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

		public override void VisitFor(CiFor statement)
		{
			OpenScope(statement);
			if (statement.Init != null)
				statement.Init.AcceptStatement(this);
			ResolveLoopCond(statement);
			if (statement.Advance != null)
				statement.Advance.AcceptStatement(this);
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
			statement.Body.AcceptStatement(this);
			CloseScope();
		}

		public override void VisitForeach(CiForeach statement)
		{
			OpenScope(statement);
			CiVar element = statement.GetVar();
			ResolveType(element);
			Resolve(statement.Collection);
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

		public override void VisitReturn(CiReturn statement)
		{
			if (this.CurrentMethod.Type.Id == CiId.VoidType) {
				if (statement.Value != null)
					ReportError(statement, "Void method cannot return a value");
			}
			else if (statement.Value == null)
				ReportError(statement, "Missing return value");
			else {
				statement.Value = Resolve(statement.Value);
				Coerce(statement.Value, this.CurrentMethod.Type);
				if (statement.Value is CiSymbolReference symbol && symbol.Symbol is CiVar local && ((local.Type.IsFinal() && !(this.CurrentMethod.Type is CiStorageType)) || (local.Type.Id == CiId.StringStorageType && this.CurrentMethod.Type.Id != CiId.StringStorageType)))
					ReportError(statement, "Returning dangling reference to local storage");
			}
		}

		public override void VisitSwitch(CiSwitch statement)
		{
			OpenScope(statement);
			statement.Value = Resolve(statement.Value);
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
					else {
						kase.Values[i] = FoldConst(kase.Values[i]);
						Coerce(kase.Values[i], statement.Value.Type);
					}
				}
				if (ResolveStatements(kase.Body))
					ReportError(kase.Body.Last(), "Case must end with break, continue, return or throw");
			}
			if (statement.DefaultBody.Count > 0) {
				bool reachable = ResolveStatements(statement.DefaultBody);
				if (reachable)
					ReportError(statement.DefaultBody.Last(), "Default must end with break, continue, return or throw");
			}
			CloseScope();
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

		CiExpr FoldConst(CiExpr expr)
		{
			expr = Resolve(expr);
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
						field.Value = Resolve(field.Value);
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
			konst.Value = Resolve(konst.Value);
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

		public override void VisitConst(CiConst statement)
		{
		}

		public override void VisitEnumValue(CiConst konst, CiConst previous)
		{
			if (konst.Value != null) {
				ResolveConst(konst);
				CiEnum enu = (CiEnum) konst.Parent;
				enu.HasExplicitValue = true;
			}
			else
				konst.Value = new CiImplicitEnumValue { Value = previous == null ? 0 : previous.Value.IntValue() + 1 };
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
				enu.AcceptValues(this);
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
				klass.Constructor.Body.AcceptStatement(this);
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
						method.Body.AcceptStatement(this);
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

	public abstract class GenBaseBase : CiExprVisitor
	{

		internal string Namespace;

		internal string OutputFile;

		protected TextWriter Writer;

		protected StringWriter StringWriter;

		protected int Indent = 0;

		protected bool AtLineStart = true;

		bool AtChildStart = false;

		bool InChildBlock = false;

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
			this.Writer.Write((char) c);
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
			expr.Cond.Accept(this, CiPriority.Select);
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

		protected static CiAggregateInitializer GetAggregateInitializer(CiNamedValue def)
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

		protected static string GetEqOp(bool not) => not ? " != " : " == ";

		protected virtual void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
		{
			WriteBinaryExpr2(expr, parent, CiPriority.Equality, GetEqOp(not));
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
				WriteBinaryExpr2(expr, parent, CiPriority.Rel, " < ");
				break;
			case CiToken.LessOrEqual:
				WriteBinaryExpr2(expr, parent, CiPriority.Rel, " <= ");
				break;
			case CiToken.Greater:
				WriteBinaryExpr2(expr, parent, CiPriority.Rel, " > ");
				break;
			case CiToken.GreaterOrEqual:
				WriteBinaryExpr2(expr, parent, CiPriority.Rel, " >= ");
				break;
			case CiToken.Equal:
				WriteEqual(expr, parent, false);
				break;
			case CiToken.NotEqual:
				WriteEqual(expr, parent, true);
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
			CiClassType klass = (CiClassType) obj.Type;
			CiType elementType = klass.GetElementType();
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
			CiClassType klass = (CiClassType) obj.Type;
			CiType elementType = klass.GetElementType();
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
			CiClassType dict = (CiClassType) obj.Type;
			WriteNewStorage(dict.GetValueType());
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

		protected abstract RegexOptions GetRegexOptions(List<CiExpr> args);

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

		protected void StartIfWhile(CiExpr expr)
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

		protected void DefineVar(CiVar def)
		{
			if (def.Name != "_") {
				WriteVar(def);
				EndStatement();
			}
		}

		protected void WriteSwitchWhenVars(CiSwitch statement, bool whenOnly = true)
		{
			foreach (CiCase kase in statement.Cases) {
				foreach (CiExpr value in kase.Values) {
					if (!whenOnly && value is CiVar var)
						DefineVar(var);
					else if (value is CiBinaryExpr when1 && when1.Op == CiToken.When) {
						CiVar whenVar = (CiVar) when1.Left;
						DefineVar(whenVar);
						WriteTemporaries(when1);
					}
				}
			}
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

		public override void VisitSwitch(CiSwitch statement)
		{
			WriteTemporaries(statement.Value);
			Write("switch (");
			WriteSwitchValue(statement.Value);
			WriteLine(") {");
			foreach (CiCase kase in statement.Cases)
				WriteSwitchCase(statement, kase);
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
}
