// CiParser.cs - Ci parser
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
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Foxoft.Ci
{

public partial class CiParser : CiLexer
{
	SymbolTable Symbols;
	readonly List<CiConst> ConstArrays = new List<CiConst>();
	public CiClass CurrentClass = null;
	public CiMethod CurrentMethod = null;
	int LoopLevel = 0;
	int SwitchLevel = 0;

	public CiParser()
	{
		SymbolTable globals = new SymbolTable();
		globals.Add(CiBoolType.Value);
		globals.Add(CiByteType.Value);
		globals.Add(CiIntType.Value);
		globals.Add(CiStringPtrType.Value);
		globals.Add(new CiConst { Name = "true", Value = true, Type = CiBoolType.Value });
		globals.Add(new CiConst { Name = "false", Value = false, Type = CiBoolType.Value });
		globals.Add(new CiConst { Name = "null", Value = null, Type = CiType.Null });
		this.Symbols = new SymbolTable { Parent = globals };
	}

	string ParseId()
	{
		string id = this.CurrentString;
		Expect(CiToken.Id);
		return id;
	}

	CiCodeDoc ParseDoc()
	{
		if (See(CiToken.DocComment)) {
			CiDocParser parser = new CiDocParser(this);
			return parser.ParseCodeDoc();
		}
		return null;
	}

	CiEnum ParseEnum()
	{
		CiEnum enu = new CiEnum();
		Expect(CiToken.Enum);
		enu.Name = ParseId();
		Expect(CiToken.LeftBrace);
		List<CiEnumValue> values = new List<CiEnumValue>();
		do {
			CiEnumValue value = new CiEnumValue();
			value.Documentation = ParseDoc();
			value.Name = ParseId();
			value.Type = enu;
			values.Add(value);
		} while (Eat(CiToken.Comma));
		Expect(CiToken.RightBrace);
		enu.Values = values.ToArray();
		return enu;
	}

	CiType LookupType(string name)
	{
		CiSymbol symbol = this.Symbols.TryLookup(name);
		if (symbol is CiType)
			return (CiType) symbol;
		if (symbol is CiClass)
			return new CiClassPtrType { Name = name, Class = (CiClass) symbol};
		if (symbol == null) {
			CiType unknown = new CiUnknownType();
			unknown.Name = name;
			return unknown;
		}
		throw new ParseException("{0} is not a type", name);
	}

	CiType ParseType(string baseName)
	{
		if (Eat(CiToken.LeftBracket)) {
			if (Eat(CiToken.RightBracket))
				return new CiArrayPtrType { ElementType = ParseType(baseName) };
			CiExpr len = ParseExpr();
			Expect(CiToken.RightBracket);
			return new CiArrayStorageType {
				LengthExpr = len,
				ElementType = ParseType(baseName)
			};
		}
		if (Eat(CiToken.LeftParenthesis)) {
			if (baseName == "string") {
				CiExpr len = ParseExpr();
				Expect(CiToken.RightParenthesis);
				return new CiStringStorageType { LengthExpr = len };
			}
			Expect(CiToken.RightParenthesis);
			return new CiClassStorageType { Name = baseName, Class = new CiUnknownClass { Name = baseName } };
		}
		return LookupType(baseName);
	}

	CiType ParseType()
	{
		string baseName = ParseId();
		return ParseType(baseName);
	}

	object ParseConstInitializer(CiType type)
	{
		if (type is CiArrayType) {
			Expect(CiToken.LeftBrace);
			CiType elementType = ((CiArrayType) type).ElementType;
			List<object> list = new List<object>();
			if (!See(CiToken.RightBrace)) {
				do
					list.Add(ParseConstInitializer(elementType));
				while (Eat(CiToken.Comma));
			}
			Expect(CiToken.RightBrace);
			return list.ToArray();
		}
		return ParseExpr();
	}

	CiConst ParseConst()
	{
		Expect(CiToken.Const);
		CiConst konst = new CiConst();
		konst.Type = ParseType();
		konst.Name = ParseId();
		Expect(CiToken.Assign);
		konst.Value = ParseConstInitializer(konst.Type);
		Expect(CiToken.Semicolon);
		if (this.Symbols.Parent != null && konst.Type is CiArrayType) {
			this.ConstArrays.Add(konst);
			konst.GlobalName = "CiConstArray_" + this.ConstArrays.Count;
		}
		return konst;
	}

	CiField ParseField()
	{
		CiType type = ParseType();
		string name = ParseId();
		Expect(CiToken.Semicolon);
		return new CiField {
			Type = type,
			Name = name
		};
	}

	CiBinaryResourceExpr ParseBinaryResource()
	{
		Expect(CiToken.LeftParenthesis);
		CiExpr nameExpr = ParseExpr();
		Expect(CiToken.RightParenthesis);
		return new CiBinaryResourceExpr { NameExpr = nameExpr };
	}

	void ParseMethodCall(CiMethodCall call)
	{
		Expect(CiToken.LeftParenthesis);
		List<CiExpr> args = new List<CiExpr>();
		if (!Eat(CiToken.RightParenthesis)) {
			do
				args.Add(ParseExpr());
			while (Eat(CiToken.Comma));
			Expect(CiToken.RightParenthesis);
		}
		call.Arguments = args.ToArray();
	}

	CiExpr ParsePrimaryExpr()
	{
		if (See(CiToken.Increment) || See(CiToken.Decrement) || See(CiToken.Minus) || See(CiToken.Not)) {
			CiToken op = this.CurrentToken;
			NextToken();
			CiExpr inner = ParsePrimaryExpr();
			return new CiUnaryExpr { Op = op, Inner = inner };
		}
		if (Eat(CiToken.CondNot)) {
			CiExpr inner = ParsePrimaryExpr();
			return new CiCondNotExpr { Inner = inner };
		}
		CiExpr result;
		if (See(CiToken.IntConstant)) {
			result = new CiConstExpr(this.CurrentInt);
			NextToken();
		}
		else if (See(CiToken.StringConstant)) {
			result = new CiConstExpr(this.CurrentString);
			NextToken();
		}
		else if (Eat(CiToken.LeftParenthesis)) {
			result = ParseExpr();
			Expect(CiToken.RightParenthesis);
		}
		else if (See(CiToken.Id)) {
			string name = ParseId();
			if (name == "BinaryResource")
				return ParseBinaryResource();
			CiSymbol symbol = this.Symbols.TryLookup(name);
			if (symbol is CiMacro) {
				Expand((CiMacro) symbol);
				Expect(CiToken.LeftParenthesis);
				result = ParseExpr();
				Expect(CiToken.RightParenthesis);
			}
			else {
				if (See(CiToken.LeftParenthesis)) {
					CiMethodCall call = new CiMethodCall();
					call.Name = name;
					ParseMethodCall(call);
					result = call;
				}
				else {
					if (symbol == null)
						symbol = new CiUnknownSymbol { Name = name };
					result = new CiSymbolAccess { Symbol = symbol };
				}
			}
		}
		else
			throw new ParseException("Invalid expression");
		for (;;) {
			if (Eat(CiToken.Dot)) {
				string name = ParseId();
				if (See(CiToken.LeftParenthesis)) {
					CiMethodCall call = new CiMethodCall();
					call.Obj = result;
					call.Name = name;
					ParseMethodCall(call);
					result = call;
				}
				else
					result = new CiUnknownMemberAccess { Parent = result, Name = name };
			}
			else if (Eat(CiToken.LeftBracket)) {
				CiExpr index = ParseExpr();
				Expect(CiToken.RightBracket);
				result = new CiIndexAccess { Parent = result, Index = index };
			}
			else if (See(CiToken.Increment) || See(CiToken.Decrement)) {
				CiToken op = this.CurrentToken;
				NextToken();
				return new CiPostfixExpr { Inner = result, Op = op };
			}
			else
				return result;
		}
	}

	CiExpr ParseMulExpr()
	{
		CiExpr left = ParsePrimaryExpr();
		while (See(CiToken.Asterisk) || See(CiToken.Slash) || See(CiToken.Mod) || See(CiToken.And) || See(CiToken.ShiftLeft) || See(CiToken.ShiftRight)) {
			CiToken op = this.CurrentToken;
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
			CiToken op = this.CurrentToken;
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
			CiToken op = this.CurrentToken;
			NextToken();
			CiExpr right = ParseAddExpr();
			left = new CiBoolBinaryExpr { Left = left, Op = op, Right = right };
		}
		return left;
	}

	CiExpr ParseCondAndExpr()
	{
		CiExpr left = ParseRelExpr();
		while (Eat(CiToken.CondAnd)) {
			CiExpr right = ParseRelExpr();
			left = new CiBoolBinaryExpr { Left = left, Op = CiToken.CondAnd, Right = right };
		}
		return left;
	}

	CiExpr ParseCondOrExpr()
	{
		CiExpr left = ParseCondAndExpr();
		while (Eat(CiToken.CondOr)) {
			CiExpr right = ParseCondAndExpr();
			left = new CiBoolBinaryExpr { Left = left, Op = CiToken.CondOr, Right = right };
		}
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
		CiToken op = this.CurrentToken;
		if (op == CiToken.Assign || op == CiToken.AddAssign || op == CiToken.SubAssign || op == CiToken.MulAssign || op == CiToken.DivAssign || op == CiToken.ModAssign
		 || op == CiToken.AndAssign || op == CiToken.OrAssign || op == CiToken.XorAssign || op == CiToken.ShiftLeftAssign || op == CiToken.ShiftRightAssign) {
			NextToken();
			CiAssign result = new CiAssign();
			result.Target = left;
			result.Op = op;
			result.Source = ParseMaybeAssign();
			return result;
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

	ICiStatement ParseVarOrExpr(string name)
	{
		// try var
		StringBuilder sb = new StringBuilder();
		this.CopyTo = sb;
		try {
			return ParseVar();
		}
		catch (ParseException) {
		}
		finally {
			this.CopyTo = null;
		}

		// try expr
		this.CurrentString = name;
		this.CurrentToken = CiToken.Id;
		BeginExpand("ambigous code", sb.ToString(), null);
		SetReader(new StringReader(sb.ToString()));
		ICiStatement result = ParseExprWithSideEffect();
		Expect(CiToken.Semicolon);
		return result;
	}

	ICiStatement ParseVarOrExpr()
	{
		string name = this.CurrentString;
		CiSymbol symbol = this.Symbols.TryLookup(name);
		if (symbol is CiMacro) {
			NextToken();
			Expand((CiMacro) symbol);
			return ParseStatement();
		}
		if (symbol is CiType || symbol is CiClass)
			return ParseVar();
		if (symbol == null)
			return ParseVarOrExpr(name);
		ICiStatement result = ParseExprWithSideEffect();
		Expect(CiToken.Semicolon);
		return result;
	}

	ICiStatement ParseLoop()
	{
		this.LoopLevel++;
		ICiStatement body = ParseStatement();
		this.LoopLevel--;
		return body;
	}

	ICiStatement ParseStatement()
	{
		while (Eat(CiToken.Macro))
			this.Symbols.Add(ParseMacro());
		if (See(CiToken.Id))
			return ParseVarOrExpr();
		if (See(CiToken.LeftBrace)) {
			OpenScope();
			CiBlock result = ParseBlock();
			CloseScope();
			return result;
		}
		if (Eat(CiToken.Break)) {
			if (this.LoopLevel == 0 && this.SwitchLevel == 0)
				throw new ParseException("break outside loop and switch");
			Expect(CiToken.Semicolon);
			return new CiBreak();
		}
		if (See(CiToken.Const)) {
			CiConst konst = ParseConst();
			this.Symbols.Add(konst);
			return konst;
		}
		if (Eat(CiToken.Continue)) {
			if (this.LoopLevel == 0)
				throw new ParseException("continue outside loop");
			Expect(CiToken.Semicolon);
			return new CiContinue();
		}
		if (Eat(CiToken.Do)) {
			CiDoWhile result = new CiDoWhile();
			result.Body = ParseLoop();
			Expect(CiToken.While);
			result.Cond = ParseCond();
			Expect(CiToken.Semicolon);
			return result;
		}
		if (Eat(CiToken.For)) {
			Expect(CiToken.LeftParenthesis);
			OpenScope();
			CiFor result = new CiFor();
			if (See(CiToken.Id))
				result.Init = ParseVarOrExpr();
			else
				Expect(CiToken.Semicolon);
			if (!See(CiToken.Semicolon))
				result.Cond = ParseExpr();
			Expect(CiToken.Semicolon);
			if (!See(CiToken.RightParenthesis))
				result.Advance = ParseExprWithSideEffect();
			Expect(CiToken.RightParenthesis);
			result.Body = ParseLoop();
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
			if (this.CurrentMethod.ReturnType != CiType.Void)
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
			this.SwitchLevel++;
			List<CiCase> cases = new List<CiCase>();
			for (;;) {
				CiCase caze;
				if (Eat(CiToken.Case)) {
					caze = new CiCase();
					caze.Value = ParseExpr();
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
			this.SwitchLevel--;
			Expect(CiToken.RightBrace);
			result.Cases = cases.ToArray();
			return result;
		}
		if (Eat(CiToken.Throw)) {
			CiThrow result = new CiThrow();
			result.Message = ParseExpr();
			Expect(CiToken.Semicolon);
			return result;
		}
		if (Eat(CiToken.While)) {
			CiWhile result = new CiWhile();
			result.Cond = ParseCond();
			result.Body = ParseLoop();
			return result;
		}
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

	CiParam CreateThis()
	{
		CiParam thiz = new CiParam();
		thiz.Type = new CiClassPtrType { Name = this.CurrentClass.Name, Class = this.CurrentClass };
		thiz.Name = "this";
		this.Symbols.Add(thiz);
		return thiz;
	}

	void ParseMethod(CiMethod method)
	{
		this.CurrentMethod = method;
		OpenScope();
		if (!method.IsStatic)
			method.This = CreateThis();
		List<CiParam> paramz = new List<CiParam>();
		if (!Eat(CiToken.RightParenthesis)) {
			do {
				CiParam param = new CiParam();
				param.Documentation = ParseDoc();
				param.Type = ParseType();
				param.Name = ParseId();
				this.Symbols.Add(param);
				paramz.Add(param);
			} while (Eat(CiToken.Comma));
			Expect(CiToken.RightParenthesis);
		}
		method.Params = paramz.ToArray();
		method.Body = ParseBlock();
		CloseScope();
		this.CurrentMethod = null;
	}

	CiMethod ParseConstructor()
	{
		NextToken();
		Expect(CiToken.LeftParenthesis);
		Expect(CiToken.RightParenthesis);
		OpenScope();
		CiMethod method = new CiMethod {
			Class = this.CurrentClass,
			IsStatic = false,
			ReturnType = CiType.Void,
			This = CreateThis(),
			Params = new CiParam[0]
		};
		this.CurrentMethod = method;
		method.Body = ParseBlock();
		CloseScope();
		this.CurrentMethod = null;
		return method;
	}

	CiClass ParseClass()
	{
		CiClass klass = new CiClass();
		Expect(CiToken.Class);
		klass.Name = ParseId();
		Expect(CiToken.LeftBrace);
		OpenScope();
		this.CurrentClass = klass;
		klass.Members = this.Symbols;
		while (!Eat(CiToken.RightBrace)) {
			CiCodeDoc doc = ParseDoc();
			bool pub = Eat(CiToken.Public);
			CiSymbol symbol;
			if (See(CiToken.Const))
				symbol = ParseConst();
			else if (Eat(CiToken.Macro))
				symbol = ParseMacro();
			else {
				if (See(CiToken.Id) && this.CurrentString == klass.Name) {
					if (klass.Constructor != null)
						throw new ParseException("Duplicate constructor");
					klass.Constructor = ParseConstructor();
					continue;
				}
				bool isStatic = Eat(CiToken.Static);
				CiType type;
				if (Eat(CiToken.Void))
					type = CiType.Void;
				else
					type = ParseType();
				string name = ParseId();
				if (Eat(CiToken.LeftParenthesis)) {
					CiMethod method = new CiMethod {
						Class = klass,
						IsStatic = isStatic,
						ReturnType = type,
						Name = name
					};
					ParseMethod(method);
					symbol = method;
				}
				else {
					Expect(CiToken.Semicolon);
					if (isStatic)
						throw new ParseException("Static fields not supported");
					if (type == CiType.Void)
						throw new ParseException("Field is void");
					symbol = new CiField { Type = type, Name = name };
				}
			}
			symbol.Documentation = doc;
			symbol.IsPublic = pub;
			klass.Members.Add(symbol);
		}
		this.CurrentClass = null;
		CloseScope();
		klass.ConstArrays = this.ConstArrays.ToArray();
		this.ConstArrays.Clear();
		return klass;
	}

	public void Parse(TextReader reader)
	{
		Open(reader);
		while (!See(CiToken.EndOfFile)) {
			CiCodeDoc doc = ParseDoc();
			bool pub = Eat(CiToken.Public);
			CiSymbol symbol;
			if (See(CiToken.Enum))
				symbol = ParseEnum();
			else if (See(CiToken.Class))
				symbol = ParseClass();
			else
				throw new ParseException("Expected enum or class");
			symbol.Documentation = doc;
			symbol.IsPublic = pub;
			this.Symbols.Add(symbol);
		}
	}

	public CiProgram Program
	{
		get
		{
			return new CiProgram { Globals = this.Symbols };
		}
	}
}

}
