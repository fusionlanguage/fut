// CiLexer.cs - Ci lexer
//
// Copyright (C) 2011-2014  Piotr Fusik
//
// This file is part of CiTo, see http://cito.sourceforge.net
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Foxoft.Ci
{

public enum CiToken
{
	EndOfFile,
	Id,
	Literal,
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
	CondAnd,
	CondOr,
	ExclamationMark,
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
	Abstract,
	Break,
	Case,
	Class,
	Const,
	Continue,
	Default,
	Delete,
	Do,
	Else,
	Enum,
	For,
	Goto,
	If,
	Internal,
	Native,
	New,
	Override,
	Protected,
	Public,
	Return,
	Sealed,
	Static,
	Switch,
	Throw,
	Virtual,
	Void,
	While,
	EndOfLine,
	PreIf,
	PreElIf,
	PreElse,
	PreEndIf
}

public class CiLexer
{
	enum PreDirectiveClass
	{
		IfOrElIf,
		Else
	}

	TextReader Reader;
	protected string Filename;
	public int Line;
	protected CiToken CurrentToken;
	protected object CurrentValue; // string for CiToken.Id; long/double/string for CiToken.Literal; not modified otherwise
	public readonly HashSet<string> PreSymbols = new HashSet<string>();
	bool AtLineStart = true;
	bool LineMode = false;
	readonly Stack<PreDirectiveClass> PreStack = new Stack<PreDirectiveClass>();

	protected CiLexer()
	{
		this.PreSymbols.Add("true");
	}

	protected void Open(string filename, TextReader reader)
	{
		this.Filename = filename;
		this.Reader = reader;
		this.Line = 1;
		NextToken();
	}

	protected CiException ParseException(string message)
	{
		return new CiException(this.Filename, this.Line, message);
	}

	protected CiException ParseException(string format, params object[] args)
	{
		return ParseException(string.Format(format, args));
	}

	int PeekChar()
	{
		return this.Reader.Peek();
	}

	static bool IsLetterOrDigit(int c)
	{
		if (c >= 'a' && c <= 'z') return true;
		if (c >= 'A' && c <= 'Z') return true;
		if (c >= '0' && c <= '9') return true;
		return c == '_';
	}

	protected int ReadChar()
	{
		int c = this.Reader.Read();
		switch (c)
		{
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
		return c;
	}

	bool EatChar(char c)
	{
		if (PeekChar() == c) {
			ReadChar();
			return true;
		}
		return false;
	}

	int ReadHexDigit()
	{
		switch (PeekChar())
		{
		case '0': return 0;
		case '1': return 1;
		case '2': return 2;
		case '3': return 3;
		case '4': return 4;
		case '5': return 5;
		case '6': return 6;
		case '7': return 7;
		case '8': return 8;
		case '9': return 9;
		case 'A':
		case 'a': return 10;
		case 'B':
		case 'b': return 11;
		case 'C':
		case 'c': return 12;
		case 'D':
		case 'd': return 13;
		case 'E':
		case 'e': return 14;
		case 'F':
		case 'f': return 15;
		default:
			return -1;
		}
	}

	CiToken ReadHexLiteral()
	{
		long i = ReadHexDigit();
		if (i < 0)
			throw ParseException("Invalid hex number");
		for (;;) {
			ReadChar();
			int d = ReadHexDigit();
			if (d < 0) {
				this.CurrentValue = i;
				return CiToken.Literal;
			}
			if (i > 0xfffffffffffffff)
				throw ParseException("Hex number too big");
			i = (i << 4) + d;
		}
	}

	CiToken ReadFloatLiteral(long i)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(i);
		for (;;) {
			int c = PeekChar();
			switch (c)
			{
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
				sb.Append((char) c);
				break;
			case 'e':
			case 'E':
				sb.Append((char) c);
				c = PeekChar();
				if (c == '+' || c == '-') {
					ReadChar();
					sb.Append((char) c);
				}
				break;
			default:
				this.CurrentValue = double.Parse(sb.ToString(), CultureInfo.InvariantCulture);
				return CiToken.Literal;
			}
		}
	}

	CiToken ReadNumberLiteral(long i)
	{
		for (;;) {
			int d;
			switch (PeekChar())
			{
			case '0': d = 0; break;
			case '1': d = 1; break;
			case '2': d = 2; break;
			case '3': d = 3; break;
			case '4': d = 4; break;
			case '5': d = 5; break;
			case '6': d = 6; break;
			case '7': d = 7; break;
			case '8': d = 8; break;
			case '9': d = 9; break;
			case '.':
			case 'e':
			case 'E':
				return ReadFloatLiteral(i);
			default:
				this.CurrentValue = i;
				return CiToken.Literal;
			}
			if (i == 0)
				throw ParseException("Octal numbers not supported");
			if (i > 922337203685477580)
				throw ParseException("Integer too big");
			i = 10 * i + d;
			if (i < 0)
				throw ParseException("Integer too big");
			ReadChar();
		}
	}

	char ReadCharLiteral()
	{
		int c = ReadChar();
		if (c < 32)
			throw ParseException("Invalid character in literal");
		if (c != '\\')
			return (char) c;
		switch (ReadChar()) {
		case '\'': return '\'';
		case '"': return '"';
		case '\\': return '\\';
		case 'a': return '\a';
		case 'b': return '\b';
		case 'f': return '\f';
		case 'n': return '\n';
		case 'r': return '\r';
		case 't': return '\t';
		case 'v': return '\v';
		default: throw ParseException("Unknown escape sequence");
		}
	}

	string ReadId(int c)
	{
		if (!IsLetterOrDigit(c))
			throw ParseException("Invalid character");
		StringBuilder sb = new StringBuilder();
		for (;;) {
			sb.Append((char) c);
			if (!IsLetterOrDigit(PeekChar()))
				break;
			c = ReadChar();
		}
		return sb.ToString();
	}

	CiToken ReadPreToken()
	{
		for (;;) {
			bool atLineStart = this.AtLineStart;
			int c = ReadChar();
			switch (c) {
			case -1:
				return CiToken.EndOfFile;
			case '\t':
			case '\r':
			case ' ':
				continue;
			case '\n':
				if (this.LineMode)
					return CiToken.EndOfLine;
				continue;
			case '#':
				if (!atLineStart)
					throw ParseException("Invalid character");
				switch (ReadId(ReadChar())) {
				case "if": return CiToken.PreIf;
				case "elif": return CiToken.PreElIf;
				case "else": return CiToken.PreElse;
				case "endif": return CiToken.PreEndIf;
				default: throw ParseException("Unknown preprocessor directive");
				}
			case ';': return CiToken.Semicolon;
			case '.': return CiToken.Dot;
			case ',': return CiToken.Comma;
			case '(': return CiToken.LeftParenthesis;
			case ')': return CiToken.RightParenthesis;
			case '[': return CiToken.LeftBracket;
			case ']': return CiToken.RightBracket;
			case '{': return CiToken.LeftBrace;
			case '}': return CiToken.RightBrace;
			case '~': return CiToken.Tilde;
			case '?': return CiToken.QuestionMark;
			case ':': return CiToken.Colon;
			case '+':
				if (EatChar('+')) return CiToken.Increment;
				if (EatChar('=')) return CiToken.AddAssign;
				return CiToken.Plus;
			case '-':
				if (EatChar('-')) return CiToken.Decrement;
				if (EatChar('=')) return CiToken.SubAssign;
				return CiToken.Minus;
			case '*':
				if (EatChar('=')) return CiToken.MulAssign;
				return CiToken.Asterisk;
			case '/':
				if (EatChar('/')) {
					c = ReadChar();
					while (c != '\n' && c >= 0)
						c = ReadChar();
					if (c == '\n' && this.LineMode) return CiToken.EndOfLine;
					continue;
				}
				if (EatChar('*')) {
					int startLine = this.Line;
					do {
						c = ReadChar();
						if (c < 0)
							throw ParseException("Unterminated multi-line comment, started in line {0}", startLine);
					} while (c != '*' || PeekChar() != '/');
					ReadChar();
					continue;
				}
				if (EatChar('=')) return CiToken.DivAssign;
				return CiToken.Slash;
			case '%':
				if (EatChar('=')) return CiToken.ModAssign;
				return CiToken.Mod;
			case '&':
				if (EatChar('&')) return CiToken.CondAnd;
				if (EatChar('=')) return CiToken.AndAssign;
				return CiToken.And;
			case '|':
				if (EatChar('|')) return CiToken.CondOr;
				if (EatChar('=')) return CiToken.OrAssign;
				return CiToken.Or;
			case '^':
				if (EatChar('=')) return CiToken.XorAssign;
				return CiToken.Xor;
			case '=':
				if (EatChar('=')) return CiToken.Equal;
				return CiToken.Assign;
			case '!':
				if (EatChar('=')) return CiToken.NotEqual;
				return CiToken.ExclamationMark;
			case '<':
				if (EatChar('<')) {
					if (EatChar('=')) return CiToken.ShiftLeftAssign;
					return CiToken.ShiftLeft;
				}
				if (EatChar('=')) return CiToken.LessOrEqual;
				return CiToken.Less;
			case '>':
				if (EatChar('>')) {
					if (EatChar('=')) return CiToken.ShiftRightAssign;
					return CiToken.ShiftRight;
				}
				if (EatChar('=')) return CiToken.GreaterOrEqual;
				return CiToken.Greater;
			case '\'':
				if (PeekChar() == '\'')
					throw ParseException("Empty character literal");
				this.CurrentValue = (long) ReadCharLiteral();
				if (ReadChar() != '\'')
					throw ParseException("Unterminated character literal");
				return CiToken.Literal;
			case '"': {
				StringBuilder sb = new StringBuilder();
				while (PeekChar() != '"')
					sb.Append(ReadCharLiteral());
				ReadChar();
				this.CurrentValue = sb.ToString();
				return CiToken.Literal;
			}
			case '0':
				c = PeekChar();
				if (c == 'x' ||  c == 'X') {
					ReadChar();
					return ReadHexLiteral();
				}
				return ReadNumberLiteral(0);
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
				string s = ReadId(c);
				switch (s) {
				case "abstract": return CiToken.Abstract;
				case "break": return CiToken.Break;
				case "case": return CiToken.Case;
				case "class": return CiToken.Class;
				case "const": return CiToken.Const;
				case "continue": return CiToken.Continue;
				case "default": return CiToken.Default;
				case "delete": return CiToken.Delete;
				case "do": return CiToken.Do;
				case "else": return CiToken.Else;
				case "enum": return CiToken.Enum;
				case "for": return CiToken.For;
				case "goto": return CiToken.Goto;
				case "if": return CiToken.If;
				case "internal": return CiToken.Internal;
				case "native": return CiToken.Native;
				case "new": return CiToken.New;
				case "override": return CiToken.Override;
				case "protected": return CiToken.Protected;
				case "public": return CiToken.Public;
				case "return": return CiToken.Return;
				case "sealed": return CiToken.Sealed;
				case "static": return CiToken.Static;
				case "switch": return CiToken.Switch;
				case "throw": return CiToken.Throw;
				case "virtual": return CiToken.Virtual;
				case "void": return CiToken.Void;
				case "while": return CiToken.While;
				default:
					this.CurrentValue = s;
					return CiToken.Id;
				}
			}
		}
	}

	void NextPreToken()
	{
		this.CurrentToken = ReadPreToken();
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
			bool result = this.PreSymbols.Contains((string) this.CurrentValue);
			NextPreToken();
			return result;
		}
		throw ParseException("Invalid preprocessor expression");
	}

	bool ParsePreAnd()
	{
		bool result = ParsePrePrimary();
		while (EatPre(CiToken.CondAnd))
			result &= ParsePrePrimary();
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
			throw ParseException("Unexpected characters after {0}", directive);
		this.LineMode = false;
	}

	void PopPreStack(string directive)
	{
		try {
			PreDirectiveClass pdc = this.PreStack.Pop();
			if (directive != "#endif" && pdc == PreDirectiveClass.Else)
				throw ParseException("{0} after #else", directive);
		}
		catch (InvalidOperationException) {
			throw ParseException("{0} with no matching #if", directive);
		}
	}

	void SkipUntilPreMet()
	{
		for (;;) {
			// we are in a conditional that wasn't met yet
			switch (ReadPreToken()) {
			case CiToken.EndOfFile:
				throw ParseException("Expected #endif, got end of file");
			case CiToken.PreIf:
				ParsePreExpr();
				SkipUntilPreEndIf(false);
				break;
			case CiToken.PreElIf:
				if (ParsePreExpr()) {
					this.PreStack.Push(PreDirectiveClass.IfOrElIf);
					return;
				}
				break;
			case CiToken.PreElse:
				ExpectEndOfLine("#else");
				this.PreStack.Push(PreDirectiveClass.Else);
				return;
			case CiToken.PreEndIf:
				ExpectEndOfLine("#endif");
				return;
			}
		}
	}

	void SkipUntilPreEndIf(bool wasElse)
	{
		for (;;) {
			// we are in a conditional that was met before
			switch (ReadPreToken()) {
			case CiToken.EndOfFile:
				throw ParseException("Expected #endif, got end of file");
			case CiToken.PreIf:
				ParsePreExpr();
				SkipUntilPreEndIf(false);
				break;
			case CiToken.PreElIf:
				if (wasElse)
					throw ParseException("#elif after #else");
				ParsePreExpr();
				break;
			case CiToken.PreElse:
				if (wasElse)
					throw ParseException("#else after #else");
				ExpectEndOfLine("#else");
				wasElse = true;
				break;
			case CiToken.PreEndIf:
				ExpectEndOfLine("#endif");
				return;
			}
		}
	}

	CiToken ReadToken()
	{
		for (;;) {
			// we are in no conditionals or in all met
			CiToken token = ReadPreToken();
			switch (token) {
			case CiToken.EndOfFile:
				if (this.PreStack.Count != 0)
					throw ParseException("Expected #endif, got end of file");
				return CiToken.EndOfFile;
			case CiToken.PreIf:
				if (ParsePreExpr()) {
					this.PreStack.Push(PreDirectiveClass.IfOrElIf);
					break;
				}
				else
					SkipUntilPreMet();
				break;
			case CiToken.PreElIf:
				PopPreStack("#elif");
				ParsePreExpr();
				SkipUntilPreEndIf(false);
				break;
			case CiToken.PreElse:
				PopPreStack("#else");
				ExpectEndOfLine("#else");
				SkipUntilPreEndIf(true);
				break;
			case CiToken.PreEndIf:
				PopPreStack("#endif");
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

	protected bool See(CiToken token)
	{
		return this.CurrentToken == token;
	}

	protected bool Eat(CiToken token)
	{
		if (See(token)) {
			NextToken();
			return true;
		}
		return false;
	}

	void Check(CiToken expected)
	{
		if (!See(expected))
			throw ParseException("Expected {0}, got {1}", expected, this.CurrentToken);
	}

	protected void Expect(CiToken expected)
	{
		Check(expected);
		NextToken();
	}
}

}
