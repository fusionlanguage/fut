// CiLexer.cs - Ci lexer
//
// Copyright (C) 2011  Piotr Fusik
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
using System.IO;
using System.Text;

namespace Foxoft.Ci
{

public enum CiToken
{
	EndOfFile,
	Id,
	IntConstant,
	StringConstant,
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
	Not,
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
	CondNot,
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
	DocComment,
	PasteTokens,
	Break,
	Case,
	Class,
	Const,
	Continue,
	Default,
	Do,
	Else,
	Enum,
	For,
	If,
	Macro,
	Namespace,
	Public,
	Return,
	Switch,
	Void,
	While
}

public class ParseException : ApplicationException
{
	public ParseException(string message) : base(message)
	{
	}

	public ParseException(string format, params object[] args) : this(string.Format(format, args))
	{
	}
}

public class CiLexer
{
	TextReader Reader;
	public int InputLineNo = 1;
	protected CiToken CurrentToken;
	protected string CurrentString;
	protected int CurrentInt;
	protected StringBuilder CopyTo;

	public CiLexer(TextReader reader)
	{
		this.Reader = reader;
		NextToken();
	}

	protected virtual bool IsExpandingMacro
	{
		get
		{
			return false;
		}
	}

	protected virtual bool ExpandMacroArg(string name)
	{
		return false;
	}

	protected virtual bool OnStreamEnd()
	{
		return false;
	}

	protected TextReader SetReader(TextReader reader)
	{
		TextReader old = this.Reader;
		this.Reader = reader;
		return old;
	}

	StringReader IdReader = null;

	public int PeekChar()
	{
		if (this.IdReader != null)
			return this.IdReader.Peek();
		return this.Reader.Peek();
	}

	static bool IsLetter(int c)
	{
		if (c >= 'a' && c <= 'z') return true;
		if (c >= 'A' && c <= 'Z') return true;
		if (c >= '0' && c <= '9') return true;
		return c == '_';
	}

	public int ReadChar()
	{
		int c;
		if (this.IdReader != null) {
			c = this.IdReader.Read();
			if (this.IdReader.Peek() < 0)
				this.IdReader = null;
		}
		else {
			c = this.Reader.Read();
			if (IsLetter(c)) {
				StringBuilder sb = new StringBuilder();
				for (;;) {
					sb.Append((char) c);
					c = this.Reader.Peek();
					if (!IsLetter(c))
						break;
					this.Reader.Read();
				}
				if (c == '#' && this.IsExpandingMacro) {
					this.Reader.Read();
					if (this.Reader.Read() != '#')
						throw new ParseException("Invalid character");
				}
				string s = sb.ToString();
				if (!ExpandMacroArg(s))
					this.IdReader = new StringReader(s);
				return ReadChar();
			}
			if (c == '\n' && !this.IsExpandingMacro)
				this.InputLineNo++;
		}
		if (c >= 0) {
			if (this.CopyTo != null)
				this.CopyTo.Append((char) c);
			while (this.Reader.Peek() < 0 && OnStreamEnd());
		}
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

	int ReadDigit(bool hex)
	{
		int c = PeekChar();
		if (c >= '0' && c <= '9')
			return ReadChar() - '0';
		if (hex && c >= 'a' && c <= 'f')
			return ReadChar() - 'a' + 10;
		return -1;
	}

	char ReadCharLiteral()
	{
		int c = ReadChar();
		if (c < 32)
			throw new ParseException("Invalid character in literal");
		if (c != '\\')
			return (char) c;
		switch (ReadChar()) {
		case 't': return '\t';
		case 'r': return '\r';
		case 'n': return '\n';
		case '\\': return '\\';
		case '\'': return '\'';
		case '"': return '"';
		default: throw new ParseException("Unknown escape sequence");
		}
	}

	CiToken ReadToken()
	{
		for (;;) {
			int c = ReadChar();
			switch (c) {
			case -1:
				return CiToken.EndOfFile;
			case '\t': case '\n': case '\r': case ' ':
				continue;
			case '#': if (EatChar('#')) return CiToken.PasteTokens;
				break;
			case ';': return CiToken.Semicolon;
			case '.': return CiToken.Dot;
			case ',': return CiToken.Comma;
			case '(': return CiToken.LeftParenthesis;
			case ')': return CiToken.RightParenthesis;
			case '[': return CiToken.LeftBracket;
			case ']': return CiToken.RightBracket;
			case '{': return CiToken.LeftBrace;
			case '}': return CiToken.RightBrace;
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
					if (c == '/') {
						while (EatChar(' '));
						return CiToken.DocComment;
					}
					while (c != '\n' && c >= 0)
						c = ReadChar();
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
				return CiToken.CondNot;
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
			case '~':
				return CiToken.Not;
			case '?':
				return CiToken.QuestionMark;
			case ':':
				return CiToken.Colon;
			case '\'':
				this.CurrentInt = ReadCharLiteral();
				if (ReadChar() != '\'')
					throw new ParseException("Unterminated character literal");
				return CiToken.IntConstant;
			case '"': {
				StringBuilder sb = new StringBuilder();
				while (PeekChar() != '"')
					sb.Append(ReadCharLiteral());
				ReadChar();
				this.CurrentString = sb.ToString();
				return CiToken.StringConstant;
			}
			case '0':
				if (EatChar('x')) {
					int i = ReadDigit(true);
					if (i < 0)
						throw new ParseException("Invalid hex number");
					for (;;) {
						int d = ReadDigit(true);
						if (d < 0) {
							this.CurrentInt = i;
							return CiToken.IntConstant;
						}
						if (i > 0x7ffffff)
							throw new ParseException("Hex number too big");
						i = (i << 4) + d;
					}
				}
				goto case '1';
			case '1': case '2': case '3': case '4':
			case '5': case '6': case '7': case '8': case '9': {
				int i = c - '0';
				for (;;) {
					int d = ReadDigit(false);
					if (d < 0) {
						this.CurrentInt = i;
						return CiToken.IntConstant;
					}
					if (i > 214748364)
						throw new ParseException("Integer too big");
					i = 10 * i + d;
					if (i < 0)
						throw new ParseException("Integer too big");
				}
			}
			case 'A': case 'B': case 'C': case 'D': case 'E':
			case 'F': case 'G': case 'H': case 'I': case 'J':
			case 'K': case 'L': case 'M': case 'N': case 'O':
			case 'P': case 'Q': case 'R': case 'S': case 'T':
			case 'U': case 'V': case 'W': case 'X': case 'Y':
			case 'Z': case '_':
			case 'a': case 'b': case 'c': case 'd': case 'e':
			case 'f': case 'g': case 'h': case 'i': case 'j':
			case 'k': case 'l': case 'm': case 'n': case 'o':
			case 'p': case 'q': case 'r': case 's': case 't':
			case 'u': case 'v': case 'w': case 'x': case 'y':
			case 'z': {
				StringBuilder sb = new StringBuilder();
				for (;;) {
					sb.Append((char) c);
					if (!IsLetter(PeekChar()))
						break;
					c = ReadChar();
				}
				string s = sb.ToString();
				switch (s) {
				case "break": return CiToken.Break;
				case "case": return CiToken.Case;
				case "class": return CiToken.Class;
				case "const": return CiToken.Const;
				case "continue": return CiToken.Continue;
				case "default": return CiToken.Default;
				case "do": return CiToken.Do;
				case "else": return CiToken.Else;
				case "enum": return CiToken.Enum;
				case "for": return CiToken.For;
				case "if": return CiToken.If;
				case "macro": return CiToken.Macro;
				case "namespace": return CiToken.Namespace;
				case "public": return CiToken.Public;
				case "return": return CiToken.Return;
				case "switch": return CiToken.Switch;
				case "void": return CiToken.Void;
				case "while": return CiToken.While;
				default:
					this.CurrentString = s;
					return CiToken.Id;
				}
			}
			default:
				break;
			}
			throw new ParseException("Invalid character");
		}
	}

	public CiToken NextToken()
	{
		CiToken token = ReadToken();
		this.CurrentToken = token;
		return token;
	}

	public bool See(CiToken token)
	{
		return this.CurrentToken == token;
	}

	public bool Eat(CiToken token)
	{
		if (See(token)) {
			NextToken();
			return true;
		}
		return false;
	}

	public void Check(CiToken expected)
	{
		if (!See(expected))
			throw new ParseException("Expected {0}, got {1}", expected, this.CurrentToken);
	}

	public void Expect(CiToken expected)
	{
		Check(expected);
		NextToken();
	}

	public void DebugLexer()
	{
		while (this.CurrentToken != CiToken.EndOfFile) {
			Console.WriteLine(this.CurrentToken);
			NextToken();
		}
	}
}

}
