// CiLexer.cs - Ci lexer
//
// Copyright (C) 2011-2013  Piotr Fusik
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
	Abstract,
	Break,
	Case,
	Class,
	Const,
	Continue,
	Default,
	Delegate,
	Delete,
	Do,
	Else,
	Enum,
	For,
	Goto,
	If,
	Internal,
	Macro,
	Native,
	New,
	Override,
	Public,
	Return,
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

[Serializable]
public class ParseException : Exception
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
	protected string Filename;
	public int InputLineNo;
	protected CiToken CurrentToken;
	protected string CurrentString;
	protected int CurrentInt;
	protected StringBuilder CopyTo;
	public HashSet<string> PreSymbols;
	bool AtLineStart = true;
	bool LineMode = false;

	public CiLexer()
	{
		this.PreSymbols = new HashSet<string>();
		this.PreSymbols.Add("true");
	}

	protected void Open(string filename, TextReader reader)
	{
		this.Filename = filename;
		this.Reader = reader;
		this.InputLineNo = 1;
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

	public static bool IsLetter(int c)
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
			if (c == '\n' && !this.IsExpandingMacro) {
				this.InputLineNo++;
				this.AtLineStart = true;
			}
		}
		if (c >= 0) {
			if (this.CopyTo != null)
				this.CopyTo.Append((char) c);
			switch (c) {
			case '\t': case '\r': case ' ': case '\n': break;
			default: this.AtLineStart = false; break;
			}
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
		if (hex) {
			if (c >= 'a' && c <= 'f')
				return ReadChar() - 'a' + 10;
			if (c >= 'A' && c <= 'F')
				return ReadChar() - 'A' + 10;
		}
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

	string ReadId(int c)
	{
		StringBuilder sb = new StringBuilder();
		for (;;) {
			sb.Append((char) c);
			if (!IsLetter(PeekChar()))
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
			case '\t': case '\r': case ' ':
				continue;
			case '\n':
				if (this.LineMode) return CiToken.EndOfLine;
				continue;
			case '#':
				c = ReadChar();
				if (c == '#') return CiToken.PasteTokens;
				if (atLineStart && IsLetter(c)) {
					string s = ReadId(c);
					switch (s) {
					case "if": return CiToken.PreIf;
					case "elif": return CiToken.PreElIf;
					case "else": return CiToken.PreElse;
					case "endif": return CiToken.PreEndIf;
					default: throw new ParseException("Unknown preprocessor directive #" + s);
					}
				}
				throw new ParseException("Invalid character");
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
					if (c == '\n' && this.LineMode) return CiToken.EndOfLine;
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
					if (i == 0)
						throw new ParseException("Octal numbers not supported");
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
				string s = ReadId(c);
				switch (s) {
				case "abstract": return CiToken.Abstract;
				case "break": return CiToken.Break;
				case "case": return CiToken.Case;
				case "class": return CiToken.Class;
				case "const": return CiToken.Const;
				case "continue": return CiToken.Continue;
				case "default": return CiToken.Default;
				case "delegate": return CiToken.Delegate;
				case "delete": return CiToken.Delete;
				case "do": return CiToken.Do;
				case "else": return CiToken.Else;
				case "enum": return CiToken.Enum;
				case "for": return CiToken.For;
				case "goto": return CiToken.Goto;
				case "if": return CiToken.If;
				case "internal": return CiToken.Internal;
				case "macro": return CiToken.Macro;
				case "native": return CiToken.Native;
				case "new": return CiToken.New;
				case "override": return CiToken.Override;
				case "public": return CiToken.Public;
				case "return": return CiToken.Return;
				case "static": return CiToken.Static;
				case "switch": return CiToken.Switch;
				case "throw": return CiToken.Throw;
				case "virtual": return CiToken.Virtual;
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
		if (EatPre(CiToken.CondNot))
			return !ParsePrePrimary();
		if (EatPre(CiToken.LeftParenthesis)) {
			bool result = ParsePreOr();
			Check(CiToken.RightParenthesis);
			NextPreToken();
			return result;
		}
		if (See(CiToken.Id)) {
			bool result = this.PreSymbols.Contains(this.CurrentString);
			NextPreToken();
			return result;
		}
		throw new ParseException("Invalid preprocessor expression");
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
			throw new ParseException("Unexpected characters after " + directive);
		this.LineMode = false;
	}

	enum PreDirectiveClass
	{
		IfOrElIf,
		Else
	}

	readonly Stack<PreDirectiveClass> PreStack = new Stack<PreDirectiveClass>();

	void PopPreStack(string directive)
	{
		try {
			PreDirectiveClass pdc = this.PreStack.Pop();
			if (directive != "#endif" && pdc == PreDirectiveClass.Else)
				throw new ParseException(directive + " after #else");
		}
		catch (InvalidOperationException) {
			throw new ParseException(directive + " with no matching #if");
		}
	}

	void SkipUntilPreMet()
	{
		for (;;) {
			// we are in a conditional that wasn't met yet
			switch (ReadPreToken()) {
			case CiToken.EndOfFile:
				throw new ParseException("Expected #endif, got end of file");
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
				throw new ParseException("Expected #endif, got end of file");
			case CiToken.PreIf:
				ParsePreExpr();
				SkipUntilPreEndIf(false);
				break;
			case CiToken.PreElIf:
				if (wasElse)
					throw new ParseException("#elif after #else");
				ParsePreExpr();
				break;
			case CiToken.PreElse:
				if (wasElse)
					throw new ParseException("#else after #else");
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
					throw new ParseException("Expected #endif, got end of file");
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
