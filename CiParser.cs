// CiParser.cs - Ci parser
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
using System.Diagnostics;
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

	CiType ParseArrayType(CiType baseType)
	{
		if (Eat(CiToken.LeftBracket)) {
			if (Eat(CiToken.RightBracket))
				return new CiArrayPtrType { ElementType = ParseArrayType(baseType) };
			CiExpr len = ParseExpr();
			Expect(CiToken.RightBracket);
			return new CiArrayStorageType {
				LengthExpr = len,
				ElementType = ParseArrayType(baseType)
			};
		}
		return baseType;
	}

	CiType ParseType()
	{
		string baseName = ParseId();
		CiType baseType;
		if (Eat(CiToken.LeftParenthesis)) {
			if (baseName == "string") {
				baseType = new CiStringStorageType { LengthExpr = ParseExpr() };
				Expect(CiToken.RightParenthesis);
			}
			else {
				Expect(CiToken.RightParenthesis);
				baseType = new CiClassStorageType { Name = baseName, Class = new CiUnknownClass { Name = baseName } };
			}
		}
		else
			baseType = LookupType(baseName);
		return ParseArrayType(baseType);
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

	CiBinaryResourceExpr ParseBinaryResource()
	{
		Expect(CiToken.LeftParenthesis);
		CiExpr nameExpr = ParseExpr();
		Expect(CiToken.RightParenthesis);
		return new CiBinaryResourceExpr { NameExpr = nameExpr };
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
				result = ParseBinaryResource();
			else {
				CiSymbol symbol = this.Symbols.TryLookup(name);
				if (symbol is CiMacro) {
					Expand((CiMacro) symbol);
					Expect(CiToken.LeftParenthesis);
					result = ParseExpr();
					Expect(CiToken.RightParenthesis);
				}
				else {
					if (symbol == null)
						symbol = new CiUnknownSymbol { Name = name };
					result = new CiSymbolAccess { Symbol = symbol };
				}
			}
		}
		else if (Eat(CiToken.New)) {
			CiType newType = ParseType();
			if (!(newType is CiClassStorageType || newType is CiArrayStorageType))
				throw new ParseException("'new' syntax error");
			result = new CiNewExpr { NewType = newType };
		}
		else
			throw new ParseException("Invalid expression");
		for (;;) {
			if (Eat(CiToken.Dot))
				result = new CiUnknownMemberAccess { Parent = result, Name = ParseId() };
			else if (Eat(CiToken.LeftParenthesis)) {
				CiMethodCall call = new CiMethodCall();
				call.Obj = result;
				List<CiExpr> args = new List<CiExpr>();
				if (!See(CiToken.RightParenthesis)) {
					do
						args.Add(ParseExpr());
					while (Eat(CiToken.Comma));
				}
				Expect(CiToken.RightParenthesis);
				call.Arguments = args.ToArray();
				result = call;
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
		while (See(CiToken.Asterisk) || See(CiToken.Slash) || See(CiToken.Mod)) {
			CiToken op = this.CurrentToken;
			NextToken();
			left = new CiBinaryExpr { Left = left, Op = op, Right = ParsePrimaryExpr() };
		}
		return left;
	}

	CiExpr ParseAddExpr()
	{
		CiExpr left = ParseMulExpr();
		while (See(CiToken.Plus) || See(CiToken.Minus)) {
			CiToken op = this.CurrentToken;
			NextToken();
			left = new CiBinaryExpr { Left = left, Op = op, Right = ParseMulExpr() };
		}
		return left;
	}

	CiExpr ParseShiftExpr()
	{
		CiExpr left = ParseAddExpr();
		while (See(CiToken.ShiftLeft) || See(CiToken.ShiftRight)) {
			CiToken op = this.CurrentToken;
			NextToken();
			left = new CiBinaryExpr { Left = left, Op = op, Right = ParseAddExpr() };
		}
		return left;
	}

	CiExpr ParseRelExpr()
	{
		CiExpr left = ParseShiftExpr();
		while (See(CiToken.Less) || See(CiToken.LessOrEqual) || See(CiToken.Greater) || See(CiToken.GreaterOrEqual)) {
			CiToken op = this.CurrentToken;
			NextToken();
			left = new CiBoolBinaryExpr { Left = left, Op = op, Right = ParseShiftExpr() };
		}
		return left;
	}

	CiExpr ParseEqualityExpr()
	{
		CiExpr left = ParseRelExpr();
		while (See(CiToken.Equal) || See(CiToken.NotEqual)) {
			CiToken op = this.CurrentToken;
			NextToken();
			left = new CiBoolBinaryExpr { Left = left, Op = op, Right = ParseRelExpr() };
		}
		return left;
	}

	CiExpr ParseAndExpr()
	{
		CiExpr left = ParseEqualityExpr();
		while (Eat(CiToken.And))
			left = new CiBinaryExpr { Left = left, Op = CiToken.And, Right = ParseEqualityExpr() };
		return left;
	}

	CiExpr ParseXorExpr()
	{
		CiExpr left = ParseAndExpr();
		while (Eat(CiToken.Xor))
			left = new CiBinaryExpr { Left = left, Op = CiToken.Xor, Right = ParseAndExpr() };
		return left;
	}

	CiExpr ParseOrExpr()
	{
		CiExpr left = ParseXorExpr();
		while (Eat(CiToken.Or))
			left = new CiBinaryExpr { Left = left, Op = CiToken.Or, Right = ParseXorExpr() };
		return left;
	}

	CiExpr ParseCondAndExpr()
	{
		CiExpr left = ParseOrExpr();
		while (Eat(CiToken.CondAnd))
			left = new CiBoolBinaryExpr { Left = left, Op = CiToken.CondAnd, Right = ParseOrExpr() };
		return left;
	}

	CiExpr ParseCondOrExpr()
	{
		CiExpr left = ParseCondAndExpr();
		while (Eat(CiToken.CondOr))
			left = new CiBoolBinaryExpr { Left = left, Op = CiToken.CondOr, Right = ParseCondAndExpr() };
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

	ICiStatement ParseVarOrExpr()
	{
		string name = this.CurrentString;
		CiSymbol symbol = this.Symbols.TryLookup(name);
		if (symbol is CiMacro) {
			NextToken();
			Expand((CiMacro) symbol);
			return ParseStatement();
		}
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

	CiNativeBlock ParseNativeBlock()
	{
		StringBuilder sb = new StringBuilder();
		this.CopyTo = sb;
		try {
			Expect(CiToken.LeftBrace);
			int level = 1;
			for (;;) {
				if (See(CiToken.EndOfFile))
					throw new ParseException("Native block not terminated");
				if (See(CiToken.LeftBrace))
					level++;
				else if (See(CiToken.RightBrace))
					if (--level == 0)
						break;
				NextToken();
			}
		}
		finally {
			this.CopyTo = null;
		}
		NextToken();
		Trace.Assert(sb[sb.Length - 1] == '}');
		sb.Length--;
		return new CiNativeBlock { Content = sb.ToString() };
	}

	CiSwitch ParseSwitch()
	{
		Expect(CiToken.LeftParenthesis);
		CiSwitch result = new CiSwitch();
		result.Value = ParseExpr();
		Expect(CiToken.RightParenthesis);
		Expect(CiToken.LeftBrace);

		List<CiCase> cases = new List<CiCase>();
		while (Eat(CiToken.Case)) {
			List<object> values = new List<object>();
			do {
				values.Add(ParseExpr());
				Expect(CiToken.Colon);
			} while (Eat(CiToken.Case));
			if (See(CiToken.Default))
				throw new ParseException("Please remove case before default");
			CiCase kase = new CiCase { Values = values.ToArray() };

			List<ICiStatement> statements = new List<ICiStatement>();
			do
				statements.Add(ParseStatement());
			while (!See(CiToken.Case) && !See(CiToken.Default) && !See(CiToken.Goto) && !See(CiToken.RightBrace));
			kase.Body = statements.ToArray();

			if (Eat(CiToken.Goto)) {
				if (Eat(CiToken.Case))
					kase.FallthroughTo = ParseExpr();
				else if (Eat(CiToken.Default))
					kase.FallthroughTo = null;
				else
					throw new ParseException("Expected goto case or goto default");
				Expect(CiToken.Semicolon);
				kase.Fallthrough = true;
			}
			cases.Add(kase);
		}
		if (cases.Count == 0)
			throw new ParseException("Switch with no cases");
		result.Cases = cases.ToArray();

		if (Eat(CiToken.Default)) {
			Expect(CiToken.Colon);
			List<ICiStatement> statements = new List<ICiStatement>();
			do
				statements.Add(ParseStatement());
			while (!See(CiToken.RightBrace));
			result.DefaultBody = statements.ToArray();
		}

		Expect(CiToken.RightBrace);
		return result;
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
			Expect(CiToken.Semicolon);
			return new CiBreak();
		}
		if (See(CiToken.Const)) {
			CiConst konst = ParseConst();
			this.Symbols.Add(konst);
			return konst;
		}
		if (Eat(CiToken.Continue)) {
			Expect(CiToken.Semicolon);
			return new CiContinue();
		}
		if (Eat(CiToken.Delete)) {
			CiExpr expr = ParseExpr();
			Expect(CiToken.Semicolon);
			return new CiDelete { Expr = expr };
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
		if (Eat(CiToken.Native))
			return ParseNativeBlock();
		if (Eat(CiToken.Return)) {
			CiReturn result = new CiReturn();
			if (this.CurrentMethod.Signature.ReturnType != CiType.Void)
				result.Value = ParseExpr();
			Expect(CiToken.Semicolon);
			return result;
		}
		if (Eat(CiToken.Switch))
			return ParseSwitch();
		if (Eat(CiToken.Throw)) {
			CiThrow result = new CiThrow();
			result.Message = ParseExpr();
			Expect(CiToken.Semicolon);
			return result;
		}
		if (Eat(CiToken.While)) {
			CiWhile result = new CiWhile();
			result.Cond = ParseCond();
			result.Body = ParseStatement();
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

	CiType ParseReturnType()
	{
		if (Eat(CiToken.Void))
			return CiType.Void;
		return ParseType();
	}

	CiParam[] ParseParams()
	{
		Expect(CiToken.LeftParenthesis);
		List<CiParam> paramz = new List<CiParam>();
		if (!See(CiToken.RightParenthesis)) {
			do {
				CiParam param = new CiParam();
				param.Documentation = ParseDoc();
				param.Type = ParseType();
				param.Name = ParseId();
				this.Symbols.Add(param);
				paramz.Add(param);
			} while (Eat(CiToken.Comma));
		}
		Expect(CiToken.RightParenthesis);
		return paramz.ToArray();
	}

	void ParseMethod(CiMethod method)
	{
		this.CurrentMethod = method;
		OpenScope();
		if (method.CallType != CiCallType.Static)
			method.This = CreateThis();
		method.Signature.Params = ParseParams();
		if (method.CallType == CiCallType.Abstract)
			Expect(CiToken.Semicolon);
		else
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
		CiMethod method = new CiMethod(
			CiType.Void, "<constructor>") {
			Class = this.CurrentClass,
			CallType = CiCallType.Normal,
			This = CreateThis()
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
		klass.SourceFilename = this.Filename;
		if (Eat(CiToken.Abstract))
			klass.IsAbstract = true;
		Expect(CiToken.Class);
		klass.Name = ParseId();
		if (Eat(CiToken.Colon))
			klass.BaseClass = new CiUnknownClass { Name = ParseId() };
		Expect(CiToken.LeftBrace);
		OpenScope();
		this.CurrentClass = klass;
		klass.Members = this.Symbols;
		while (!Eat(CiToken.RightBrace)) {
			CiCodeDoc doc = ParseDoc();
			CiVisibility visibility = CiVisibility.Private;
			if (Eat(CiToken.Public))
				visibility = CiVisibility.Public;
			else if (Eat(CiToken.Internal))
				visibility = CiVisibility.Internal;
			CiSymbol symbol;
			if (See(CiToken.Const)) {
				symbol = ParseConst();
				((CiConst) symbol).Class = klass;
			}
			else if (Eat(CiToken.Macro)) {
				if (visibility != CiVisibility.Private)
					throw new ParseException("Macros must be private");
				symbol = ParseMacro();
			}
			else {
				if (See(CiToken.Id) && this.CurrentString == klass.Name) {
					if (klass.Constructor != null)
						throw new ParseException("Duplicate constructor");
					klass.Constructor = ParseConstructor();
					continue;
				}
				CiCallType callType;
				if (Eat(CiToken.Static))
					callType = CiCallType.Static;
				else if (Eat(CiToken.Abstract)) {
					if (!klass.IsAbstract)
						throw new ParseException("Abstract methods only allowed in abstract classes");
					callType = CiCallType.Abstract;
					if (visibility == CiVisibility.Private)
						visibility = CiVisibility.Internal;
				}
				else if (Eat(CiToken.Virtual)) {
					callType = CiCallType.Virtual;
					if (visibility == CiVisibility.Private)
						visibility = CiVisibility.Internal;
				}
				else if (Eat(CiToken.Override)) {
					callType = CiCallType.Override;
					if (visibility == CiVisibility.Private)
						visibility = CiVisibility.Internal;
				}
				else
					callType = CiCallType.Normal;
				CiType type = ParseReturnType();
				string name = ParseId();
				if (See(CiToken.LeftParenthesis)) {
					CiMethod method = new CiMethod(type, name) {
						Class = klass,
						CallType = callType
					};
					ParseMethod(method);
					symbol = method;
				}
				else {
					if (visibility != CiVisibility.Private)
						throw new ParseException("Fields must be private");
					if (callType != CiCallType.Normal)
						throw new ParseException("Fields cannot be static, abstract, virtual or override");
					if (type == CiType.Void)
						throw new ParseException("Field is void");
					Expect(CiToken.Semicolon);
					symbol = new CiField { Class = klass, Type = type, Name = name };
				}
			}
			symbol.Documentation = doc;
			symbol.Visibility = visibility;
			klass.Members.Add(symbol);
		}
		this.CurrentClass = null;
		CloseScope();
		klass.ConstArrays = this.ConstArrays.ToArray();
		this.ConstArrays.Clear();
		return klass;
	}

	CiDelegate ParseDelegate()
	{
		CiDelegate del = new CiDelegate();
		Expect(CiToken.Delegate);
		del.ReturnType = ParseReturnType();
		del.Name = ParseId();
		OpenScope();
		del.Params = ParseParams();
		CloseScope();
		Expect(CiToken.Semicolon);
		return del;
	}

	public void Parse(string filename, TextReader reader)
	{
		Open(filename, reader);
		while (!See(CiToken.EndOfFile)) {
			CiCodeDoc doc = ParseDoc();
			bool pub = Eat(CiToken.Public);
			CiSymbol symbol;
			if (See(CiToken.Enum))
				symbol = ParseEnum();
			else if (See(CiToken.Class) || See(CiToken.Abstract))
				symbol = ParseClass();
			else if (See(CiToken.Delegate))
				symbol = ParseDelegate();
			else
				throw new ParseException("Expected class, enum or delegate");
			symbol.Documentation = doc;
			symbol.Visibility = pub ? CiVisibility.Public : CiVisibility.Internal;
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
