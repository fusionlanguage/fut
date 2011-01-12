// CiParser.cs - Ci parser
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
using System.Linq;

namespace Foxoft.Ci
{

public class CiParser
{
	readonly CiLexer Lexer;
	SymbolTable Symbols;

	public CiParser(CiLexer lexer)
	{
		this.Lexer = lexer;
	}

	void NextToken()
	{
		this.Lexer.NextToken();
	}

	bool See(CiToken token)
	{
		return this.Lexer.CurrentToken == token;
	}

	bool Eat(CiToken token)
	{
		if (See(token)) {
			NextToken();
			return true;
		}
		return false;
	}

	void Expect(CiToken expected)
	{
		if (!See(expected))
			throw new ParseException("Expected {0}, got {1}", expected, this.Lexer.CurrentToken);
		NextToken();
	}

	string ParseId()
	{
		string id = this.Lexer.CurrentString;
		Expect(CiToken.Id);
		return id;
	}

	CiCodeDoc ParseDoc()
	{
		if (See(CiToken.DocComment)) {
			CiDocLexer lexer = new CiDocLexer(this.Lexer);
			CiDocParser parser = new CiDocParser(lexer);
			return parser.ParseCodeDoc();
		}
		return null;
	}

	CiType ParseType(string baseName)
	{
		if (Eat(CiToken.LeftBracket)) {
			int len = this.Lexer.CurrentInt;
			if (Eat(CiToken.IntConstant)) {
				Expect(CiToken.RightBracket);
				return new CiArrayStorageType {
					Length = len,
					ElementType = ParseType(baseName)
				};
			}
			Expect(CiToken.RightBracket);
			return new CiArrayPtrType { ElementType = ParseType(baseName) };
		}
		if (Eat(CiToken.LeftParenthesis)) {
			if (baseName == "string") {
				int len = this.Lexer.CurrentInt;
				Expect(CiToken.IntConstant);
				Expect(CiToken.RightParenthesis);
				return new CiStringStorageType { Length = len };
			}
			Expect(CiToken.RightParenthesis);
			CiClass clazz = this.Symbols.Lookup(baseName) as CiClass;
			if (clazz == null)
				throw new ParseException("{0} is not a class", baseName);
			return new CiClassStorageType { Class = clazz };
		}
		CiSymbol symbol = this.Symbols.Lookup(baseName);
		if (symbol is CiClass)
			return new CiClassPtrType { Class = (CiClass) symbol};
		if (symbol is CiType)
			return (CiType) symbol;
		throw new ParseException("{0} is not a type", baseName);
	}

	CiType ParseType()
	{
		string baseName = ParseId();
		return ParseType(baseName);
	}

	CiConst ParseConst(CiCodeDoc doc, bool pub)
	{
		Expect(CiToken.Const);
		CiConst def = new CiConst();
		def.Documentation = doc;
		def.IsPublic = pub;
		def.Type = ParseType();
		def.Name = ParseId();
		Expect(CiToken.Assign);
		def.Value = this.Lexer.CurrentInt;
		if (Eat(CiToken.IntConstant)) {
		}
		else if (Eat(CiToken.LeftBrace)) {
			do {
				//this.Lexer.CurrentInt;
				Expect(CiToken.IntConstant);
				// TODO
			} while (Eat(CiToken.Comma));
			Expect(CiToken.RightBrace);
			// TODO
		}
		// TODO
		Expect(CiToken.Semicolon);
		this.Symbols.Add(def);
		return def;
	}

	CiField ParseField()
	{
		CiCodeDoc doc = ParseDoc();
		bool pub = Eat(CiToken.Public);
		CiType type = ParseType();
		string name = ParseId();
		Expect(CiToken.Semicolon);
		return new CiField {
			Documentation = doc,
			IsPublic = pub,
			Type = type,
			Name = name
		};
	}

	CiClass ParseClass(CiCodeDoc doc, bool pub)
	{
		CiClass clazz = new CiClass();
		clazz.Documentation = doc;
		clazz.IsPublic = pub;
		Expect(CiToken.Class);
		clazz.Name = ParseId();
		this.Symbols.Add(clazz);
		Expect(CiToken.LeftBrace);
		List<CiField> fields = new List<CiField>();
		while (!See(CiToken.RightBrace))
			fields.Add(ParseField());
		NextToken();
		clazz.Fields = fields.ToArray();
		return clazz;
	}

	CiExpr ParsePrimaryExpr()
	{
		if (See(CiToken.Increment) || See(CiToken.Decrement) || See(CiToken.Minus) || See(CiToken.Not) || See(CiToken.CondNot)) {
			CiToken op = this.Lexer.CurrentToken;
			NextToken();
			return new CiUnaryExpr { Op = op, Inner = ParsePrimaryExpr() };
		}
		CiExpr result;
		if (See(CiToken.IntConstant)) {
			int value = this.Lexer.CurrentInt;
			NextToken();
			result = new CiConstExpr { Value = value };
		}
		else if (Eat(CiToken.LeftParenthesis)) {
			result = ParseExpr();
			Expect(CiToken.RightParenthesis);
		}
		else if (See(CiToken.Id)) {
			CiSymbol symbol = this.Symbols.Lookup(ParseId());
			if (symbol is CiVar)
				result = new CiVarAccess { Var = (CiVar) symbol };
			else if (symbol is CiConst)
				result = new CiConstExpr { Value = ((CiConst) symbol).Value };
			else if (symbol is CiFunction) {
				CiFunctionCall call = new CiFunctionCall();
				call.Function = (CiFunction) symbol;
				Expect(CiToken.LeftParenthesis);
				List<CiExpr> arguments = new List<CiExpr>();
				do
					arguments.Add(ParseExpr());
				while (Eat(CiToken.Comma));
				Expect(CiToken.RightParenthesis);
				call.Arguments = arguments.ToArray();
				return call;
			}
			else
				throw new ParseException("Invalid expression");
		}
		else
			throw new ParseException("Invalid expression");
		for (;;) {
			if (Eat(CiToken.Dot)) {
				ParseId(); // TODO
				result = new CiFieldAccess {
					Obj = result,
					Field = null // TODO
				};
			}
			else if (Eat(CiToken.LeftBracket)) {
				CiExpr index = ParseExpr();
				Expect(CiToken.RightBracket);
				result = new CiArrayAccess { Array = result, Index = index };
			}
			else if (See(CiToken.Increment) || See(CiToken.Decrement)) {
				CiToken op = this.Lexer.CurrentToken;
				NextToken();
				CiLValue lvalue = result as CiLValue;
				if (lvalue == null)
					throw new ParseException("Not an l-value for the postfix operator");
				return new CiPostfixExpr { Inner = lvalue, Op = op };
			}
			else
				break;
		}
		return result;
	}

	CiExpr ParseMulExpr()
	{
		CiExpr left = ParsePrimaryExpr();
		while (See(CiToken.Asterisk) || See(CiToken.Slash) || See(CiToken.Mod) || See(CiToken.And) || See(CiToken.ShiftLeft) || See(CiToken.ShiftRight)) {
			CiToken op = this.Lexer.CurrentToken;
			NextToken();
			CiExpr right = ParsePrimaryExpr();
			left = new CiBinaryExpr { Left = left, Op = op, Right = right };
		}
		return left;
	}

	CiExpr ParseAddExpr()
	{
		CiExpr left = ParseMulExpr();
		while (See(CiToken.Plus) || See(CiToken.Minus) || See(CiToken.Or) || See(CiToken.Xor)) {
			CiToken op = this.Lexer.CurrentToken;
			NextToken();
			CiExpr right = ParseMulExpr();
			left = new CiBinaryExpr { Left = left, Op = op, Right = right };
		}
		return left;
	}

	CiExpr ParseRelExpr()
	{
		CiExpr left = ParseAddExpr();
		while (See(CiToken.Equal) || See(CiToken.NotEqual) || See(CiToken.Less) || See(CiToken.LessOrEqual) || See(CiToken.Greater) || See(CiToken.GreaterOrEqual)) {
			CiToken op = this.Lexer.CurrentToken;
			NextToken();
			CiExpr right = ParseAddExpr();
			left = new CiBinaryExpr { Left = left, Op = op, Right = right };
		}
		return left;
	}

	CiExpr ParseCondAndExpr()
	{
		CiExpr left = ParseRelExpr();
		while (Eat(CiToken.CondAnd))
			left = new CiBinaryExpr { Left = left, Op = CiToken.CondAnd, Right = ParseRelExpr() };
		return left;
	}

	CiExpr ParseCondOrExpr()
	{
		CiExpr left = ParseCondAndExpr();
		while (Eat(CiToken.CondOr))
			left = new CiBinaryExpr { Left = left, Op = CiToken.CondOr, Right = ParseCondAndExpr() };
		return left;
	}

	CiExpr ParseExpr()
	{
		CiExpr left = ParseCondOrExpr();
		if (Eat(CiToken.QuestionMark)) {
			CiCondExpr result = new CiCondExpr();
			result.Cond = left;
			result.OnTrue = ParseExpr();
			Expect(CiToken.Colon);
			result.OnFalse = ParseExpr();
			return result;
		}
		return left;
	}

	CiMaybeAssign ParseMaybeAssign()
	{
		CiExpr left = ParseExpr();
		CiToken op = this.Lexer.CurrentToken;
		if (op == CiToken.Assign || op == CiToken.AddAssign || op == CiToken.SubAssign || op == CiToken.MulAssign || op == CiToken.DivAssign || op == CiToken.ModAssign
		 || op == CiToken.AndAssign || op == CiToken.OrAssign || op == CiToken.XorAssign || op == CiToken.ShiftLeftAssign || op == CiToken.ShiftRightAssign) {
			NextToken();
			CiLValue target = left as CiLValue;
			if (target == null)
				throw new ParseException("Not an l-value for an assignment");
			return new CiAssign { Target = target, Op = op, Source = ParseMaybeAssign() };
		}
		return left;
	}

	ICiStatement ParseExprWithSideEffect()
	{
		ICiStatement result = ParseMaybeAssign() as ICiStatement;
		if (result == null)
			throw new ParseException("Useless expression");
		return result;
	}

	CiExpr ParseCond()
	{
		Expect(CiToken.LeftParenthesis);
		CiExpr cond = ParseExpr();
		Expect(CiToken.RightParenthesis);
		return cond;
	}

	void OpenScope()
	{
		this.Symbols = new SymbolTable { Parent = this.Symbols };
	}

	void CloseScope()
	{
		this.Symbols = this.Symbols.Parent;
	}

	CiVar ParseVar()
	{
		CiVar def = new CiVar();
		def.Type = ParseType();
		def.Name = ParseId();
		if (Eat(CiToken.Assign))
			def.InitialValue = ParseExpr();
		Expect(CiToken.Semicolon);
		this.Symbols.Add(def);
		return def;
	}

	ICiStatement ParseStatement()
	{
		if (See(CiToken.Id)) {
			CiSymbol symbol = this.Symbols.Lookup(this.Lexer.CurrentString);
			if (symbol is CiType || symbol is CiClass)
				return ParseVar();
			ICiStatement result = ParseExprWithSideEffect();
			Expect(CiToken.Semicolon);
			return result;
		}
		if (See(CiToken.LeftBrace)) {
			OpenScope();
			CiBlock result = ParseBlock();
			CloseScope();
			return result;
		}
		if (Eat(CiToken.Break)) {
			Expect(CiToken.Semicolon);
			return new CiBreak();
		}
		if (See(CiToken.Const))
			return ParseConst(null, false);
		if (Eat(CiToken.Continue)) {
			Expect(CiToken.Semicolon);
			return new CiContinue();
		}
		if (Eat(CiToken.Do)) {
			CiDoWhile result = new CiDoWhile();
			result.Body = ParseStatement();
			Expect(CiToken.While);
			result.Cond = ParseCond();
			Expect(CiToken.Semicolon);
			return result;
		}
		if (Eat(CiToken.For)) {
			Expect(CiToken.LeftParenthesis);
			OpenScope();
			CiFor result = new CiFor();
			if (!Eat(CiToken.Semicolon))
				result.Init = ParseVar();
			if (!See(CiToken.Semicolon))
				result.Cond = ParseExpr();
			Expect(CiToken.Semicolon);
			if (!See(CiToken.RightParenthesis))
				result.Advance = ParseExprWithSideEffect();
			Expect(CiToken.RightParenthesis);
			result.Body = ParseStatement();
			CloseScope();
			return result;
		}
		if (Eat(CiToken.If)) {
			CiIf result = new CiIf();
			result.Cond = ParseCond();
			result.OnTrue = ParseStatement();
			if (Eat(CiToken.Else))
				result.OnFalse = ParseStatement();
			return result;
		}
		if (Eat(CiToken.Return)) {
			CiReturn result = new CiReturn();
			if (!See(CiToken.Semicolon))
				result.Value = ParseExpr();
			Expect(CiToken.Semicolon);
			return result;
		}
		if (Eat(CiToken.Switch)) {
			Expect(CiToken.LeftParenthesis);
			CiSwitch result = new CiSwitch();
			result.Value = ParseExpr();
			Expect(CiToken.RightParenthesis);
			Expect(CiToken.LeftBrace);
			List<CiCase> cases = new List<CiCase>();
			for (;;) {
				CiCase caze;
				if (Eat(CiToken.Case)) {
					caze = new CiCase();
					caze.Value = this.Lexer.CurrentInt;
					Expect(CiToken.IntConstant);
				}
				else if (Eat(CiToken.Default))
					caze = new CiCase();
				else
					break;
				Expect(CiToken.Colon);
				List<ICiStatement> statements = new List<ICiStatement>();
				while (!See(CiToken.Case) && !See(CiToken.Default) && !See(CiToken.RightBrace))
					statements.Add(ParseStatement());
				caze.Body = statements.ToArray();
				cases.Add(caze);
			}
			Expect(CiToken.RightBrace);
			result.Cases = cases.ToArray();
			return result;
		}
		if (Eat(CiToken.While)) {
			CiWhile result = new CiWhile();
			result.Cond = ParseCond();
			result.Body = ParseStatement();
			return result;
		}
		// TODO
		throw new ParseException("Invalid statement");
	}

	CiBlock ParseBlock()
	{
		Expect(CiToken.LeftBrace);
		List<ICiStatement> statements = new List<ICiStatement>();
		while (!Eat(CiToken.RightBrace))
			statements.Add(ParseStatement());
		return new CiBlock { Statements = statements.ToArray() };
	}

	CiFunction ParseFunction(CiCodeDoc doc, bool pub)
	{
		CiFunction func = new CiFunction();
		func.Documentation = doc;
		func.IsPublic = pub;
		if (Eat(CiToken.Void))
			func.ReturnType = CiType.Void;
		else
			func.ReturnType = ParseType();
		func.Name = ParseId();
		this.Symbols.Add(func);
	
		Expect(CiToken.LeftParenthesis);
		OpenScope();
		List<CiArg> arguments = new List<CiArg>();
		do {
			CiArg arg = new CiArg();
//			arg.Documentation = ParseDoc();
			arg.Type = ParseType();
			arg.Name = ParseId();
			this.Symbols.Add(arg);
			arguments.Add(arg);
		} while (Eat(CiToken.Comma));
		Expect(CiToken.RightParenthesis);
		func.Arguments = arguments.ToArray();
		func.Body = ParseBlock();
		CloseScope();
		return func;
	}

	public CiProgram ParseProgram()
	{
		this.Symbols = new SymbolTable();
		this.Symbols.Add(CiType.Bool);
		this.Symbols.Add(CiType.Byte);
		this.Symbols.Add(CiType.Int);
		this.Symbols.Add(CiStringType.Ptr);
		this.Symbols.Add(new CiConst { Name = "true", Value = true });
		this.Symbols.Add(new CiConst { Name = "false", Value = false });
		this.Symbols.Add(new CiConst { Name = "null", Value = null });

		Expect(CiToken.Namespace);
		List<string> namespaceElements = new List<string>();
		namespaceElements.Add(ParseId());
		while (See(CiToken.Dot)) {
			NextToken();
			namespaceElements.Add(ParseId());
		}
		Expect(CiToken.Semicolon);

		List<CiClass> classes = new List<CiClass>();
		List<CiFunction> functions = new List<CiFunction>();
		while (!See(CiToken.EndOfFile)) {
			CiCodeDoc doc = ParseDoc();
			bool pub = Eat(CiToken.Public);
			if (See(CiToken.Const))
				ParseConst(doc, pub);
			else if (See(CiToken.Class))
				classes.Add(ParseClass(doc, pub));
			else
				functions.Add(ParseFunction(doc, pub));
		}

		return new CiProgram {
			NamespaceElements = namespaceElements.ToArray(),
			Classes = classes.ToArray(),
			Functions = functions.ToArray()
		};
	}
}

}
