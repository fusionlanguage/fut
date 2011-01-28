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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

public partial class CiParser : CiLexer
{
	SymbolTable Symbols;
	List<CiConst> ConstArrays;
	public CiFunction CurrentFunction;

	public CiParser(TextReader reader) : base(reader)
	{
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
		this.Symbols.Add(enu);
		return enu;
	}

	CiType ParseType(string baseName)
	{
		if (Eat(CiToken.LeftBracket)) {
			int len = this.CurrentInt;
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
				int len = this.CurrentInt;
				Expect(CiToken.IntConstant);
				Expect(CiToken.RightParenthesis);
				return new CiStringStorageType { Length = len };
			}
			Expect(CiToken.RightParenthesis);
			CiClass clazz = this.Symbols.Lookup(baseName) as CiClass;
			if (clazz == null)
				throw new ParseException("{0} is not a class", baseName);
			return new CiClassStorageType { Name = baseName, Class = clazz };
		}
		CiSymbol symbol = this.Symbols.Lookup(baseName);
		if (symbol is CiClass)
			return new CiClassPtrType { Name = baseName, Class = (CiClass) symbol};
		if (symbol is CiType)
			return (CiType) symbol;
		throw new ParseException("{0} is not a type", baseName);
	}

	CiType ParseType()
	{
		string baseName = ParseId();
		return ParseType(baseName);
	}

	object ParseConstInitializer(CiType type)
	{
		if (type == CiIntType.Value) {
			int result = this.CurrentInt;
			Expect(CiToken.IntConstant);
			return result;
		}
		if (type == CiByteType.Value) {
			int result = this.CurrentInt;
			Expect(CiToken.IntConstant);
			if (result < 0 || result > 255)
				throw new ParseException("Byte constant out of range");
			return (byte) result;
		}
		if (type == CiStringPtrType.Value) {
			string result = this.CurrentString;
			Expect(CiToken.StringConstant);
			return result;
		}
		if (type is CiArrayType) {
			CiType elementType = ((CiArrayType) type).ElementType;
			if (Eat(CiToken.LeftBrace)) {
				ArrayList list = new ArrayList();
				if (!See(CiToken.RightBrace)) {
					do
						list.Add(ParseConstInitializer(elementType));
					while (Eat(CiToken.Comma));
				}
				Expect(CiToken.RightBrace);
				if (type is CiArrayStorageType) {
					int expected = ((CiArrayStorageType) type).Length;
					if (list.Count != expected)
						throw new ParseException("Expected {0} array elements, got {1}", expected, list.Count);
				}
				return list.ToArray(elementType.DotNetType);
			}
			return ParseConstInitializer(elementType);
		}
		throw new ParseException("Invalid const type");
	}

	CiConst ParseConst()
	{
		Expect(CiToken.Const);
		CiConst def = new CiConst();
		def.Type = ParseType();
		def.Name = ParseId();
		Expect(CiToken.Assign);
		def.Value = ParseConstInitializer(def.Type);
		Expect(CiToken.Semicolon);
		this.Symbols.Add(def);
		if (this.Symbols.Parent != null && def.Type is CiArrayType) {
			this.ConstArrays.Add(def);
			def.GlobalName = "CiConstArray_" + this.ConstArrays.Count;
		}
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

	CiClass ParseClass()
	{
		CiClass clazz = new CiClass();
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

	CiFunctionCall ParseFunctionCall(CiFunctionCall call)
	{
		Expect(CiToken.LeftParenthesis);
		CiParam[] paramz = call.Function.Params;
		call.Arguments = new CiExpr[paramz.Length];
		for (int i = 0; i < paramz.Length; i++) {
			if (i > 0)
				Expect(CiToken.Comma);
			CiExpr arg = ParseExpr();
			ExpectType(arg, paramz[i].Type);
			call.Arguments[i] = arg;
		}
		Expect(CiToken.RightParenthesis);
		return call;
	}

	void ExpectType(CiMaybeAssign expr, CiType expected)
	{
		if (!expected.IsAssignableFrom(expr.Type))
			throw new ParseException("Expected {0}, got {1}", expected, expr.Type);
	}

	void CheckCompatibleTypes(CiExpr expr1, CiExpr expr2)
	{
		if (!expr1.Type.IsAssignableFrom(expr2.Type) && !expr2.Type.IsAssignableFrom(expr1.Type))
			throw new ParseException("Incompatible types");
	}

	CiExpr ParsePrimaryExpr()
	{
		if (See(CiToken.Increment) || See(CiToken.Decrement) || See(CiToken.Minus) || See(CiToken.Not)) {
			CiToken op = this.CurrentToken;
			NextToken();
			CiExpr inner = ParsePrimaryExpr();
			ExpectType(inner, CiIntType.Value);
			return new CiUnaryExpr { Op = op, Inner = inner };
		}
		if (Eat(CiToken.CondNot)) {
			CiExpr inner = ParsePrimaryExpr();
			ExpectType(inner, CiBoolType.Value);
			return new CiCondNotExpr { Inner = inner };
		}
		CiExpr result;
		if (See(CiToken.IntConstant)) {
			int value = this.CurrentInt;
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
			else if (symbol is CiConst) {
				CiConst konst = (CiConst) symbol;
				if (konst.Type is CiArrayType)
					result = new CiConstAccess { Const = konst };
				else
					result = new CiConstExpr { Value = konst.Value };
			}
			else if (symbol is CiEnum) {
				Expect(CiToken.Dot);
				CiEnum enu = (CiEnum) symbol;
				return new CiConstExpr { Value = enu.LookupMember(ParseId()) };
			}
			else if (symbol is CiFunction) {
				CiFunctionCall call = new CiFunctionCall();
				call.Function = (CiFunction) symbol;
				return ParseFunctionCall(call);
			}
			else if (symbol is CiMacro) {
				Expand((CiMacro) symbol);
				Expect(CiToken.LeftParenthesis);
				result = ParseExpr();
				Expect(CiToken.RightParenthesis);
			}
			else
				throw new ParseException("Invalid expression");
		}
		else
			throw new ParseException("Invalid expression");
		for (;;) {
			if (Eat(CiToken.Dot)) {
				string name = ParseId();
				CiSymbol member = result.Type.LookupMember(name);
				if (member is CiFunction) {
					CiMethodCall call = new CiMethodCall();
					call.Obj = result;
					call.Function = (CiFunction) member;
					return ParseFunctionCall(call);
				}
				if (member is CiField) {
					result = new CiFieldAccess {
						Obj = result,
						Field = (CiField) member
					};
				}
				else if (member is CiProperty) {
					result = new CiPropertyAccess {
						Obj = result,
						Property = (CiProperty) member
					};
				}
				else
					throw new ParseException("Member {0} is of incorrect type");
			}
			else if (Eat(CiToken.LeftBracket)) {
				// TODO: type check, string
				CiExpr index = ParseExpr();
				ExpectType(index, CiIntType.Value);
				Expect(CiToken.RightBracket);
				result = new CiArrayAccess { Array = result, Index = index };
			}
			else if (See(CiToken.Increment) || See(CiToken.Decrement)) {
				ExpectType(result, CiIntType.Value);
				CiToken op = this.CurrentToken;
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
			ExpectType(left, CiIntType.Value);
			CiToken op = this.CurrentToken;
			NextToken();
			CiExpr right = ParsePrimaryExpr();
			ExpectType(right, CiIntType.Value);
			if (left is CiConstExpr && right is CiConstExpr) {
				int a = (int) ((CiConstExpr) left).Value;
				int b = (int) ((CiConstExpr) right).Value;
				switch (op) {
				case CiToken.Asterisk: a *= b; break;
				case CiToken.Slash: a /= b; break;
				case CiToken.Mod: a %= b; break;
				case CiToken.And: a &= b; break;
				case CiToken.ShiftLeft: a <<= b; break;
				case CiToken.ShiftRight: a >>= b; break;
				}
				left = new CiConstExpr { Value = a };
			}
			else
				left = new CiBinaryExpr { Left = left, Op = op, Right = right };
		}
		return left;
	}

	CiExpr ParseAddExpr()
	{
		CiExpr left = ParseMulExpr();
		while (See(CiToken.Plus) || See(CiToken.Minus) || See(CiToken.Or) || See(CiToken.Xor)) {
			ExpectType(left, CiIntType.Value);
			CiToken op = this.CurrentToken;
			NextToken();
			CiExpr right = ParseMulExpr();
			ExpectType(right, CiIntType.Value);
			if (left is CiConstExpr && right is CiConstExpr) {
				int a = (int) ((CiConstExpr) left).Value;
				int b = (int) ((CiConstExpr) right).Value;
				switch (op) {
				case CiToken.Plus: a += b; break;
				case CiToken.Minus: a -= b; break;
				case CiToken.Or: a |= b; break;
				case CiToken.Xor: a ^= b; break;
				}
				left = new CiConstExpr { Value = a };
			}
			else
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
			CheckCompatibleTypes(left, right);
			left = new CiBoolBinaryExpr { Left = left, Op = op, Right = right };
		}
		return left;
	}

	CiExpr ParseCondAndExpr()
	{
		CiExpr left = ParseRelExpr();
		while (Eat(CiToken.CondAnd)) {
			ExpectType(left, CiBoolType.Value);
			CiExpr right = ParseRelExpr();
			ExpectType(right, CiBoolType.Value);
			left = new CiBoolBinaryExpr { Left = left, Op = CiToken.CondAnd, Right = right };
		}
		return left;
	}

	CiExpr ParseCondOrExpr()
	{
		CiExpr left = ParseCondAndExpr();
		while (Eat(CiToken.CondOr)) {
			ExpectType(left, CiBoolType.Value);
			CiExpr right = ParseCondAndExpr();
			ExpectType(right, CiBoolType.Value);
			left = new CiBoolBinaryExpr { Left = left, Op = CiToken.CondOr, Right = right };
		}
		return left;
	}

	CiExpr ParseExpr()
	{
		CiExpr left = ParseCondOrExpr();
		if (Eat(CiToken.QuestionMark)) {
			ExpectType(left, CiBoolType.Value);
			CiCondExpr result = new CiCondExpr();
			result.Cond = left;
			result.OnTrue = ParseExpr();
			Expect(CiToken.Colon);
			result.OnFalse = ParseExpr();
//			CheckCompatibleTypes(result.OnTrue, result.OnFalse);
			if (left is CiConstExpr)
				return (bool) ((CiConstExpr) left).Value ? result.OnTrue : result.OnFalse;
			return result;
		}
		return left;
	}

	object ParseConstExpr(CiType type)
	{
		CiConstExpr expr = ParseExpr() as CiConstExpr;
		if (expr == null)
			throw new ParseException("Expression is not constant");
		ExpectType(expr, type);
		return expr.Value;
	}

	CiMaybeAssign ParseMaybeAssign()
	{
		CiExpr left = ParseExpr();
		CiToken op = this.CurrentToken;
		if (op == CiToken.Assign || op == CiToken.AddAssign || op == CiToken.SubAssign || op == CiToken.MulAssign || op == CiToken.DivAssign || op == CiToken.ModAssign
		 || op == CiToken.AndAssign || op == CiToken.OrAssign || op == CiToken.XorAssign || op == CiToken.ShiftLeftAssign || op == CiToken.ShiftRightAssign) {
			NextToken();
			CiLValue target = left as CiLValue;
			if (target == null)
				throw new ParseException("Not an l-value for an assignment");
			CiAssign result = new CiAssign();
			result.Target = target;
			result.Op = op;
			result.Source = ParseMaybeAssign();
			if (target.Type == CiByteType.Value && result.Source.Type == CiIntType.Value)
				result.CastIntToByte = true;
			else
				ExpectType(result.Source, target.Type);
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
		ExpectType(cond, CiBoolType.Value);
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
		CiSymbol symbol = this.Symbols.Lookup(this.CurrentString);
		if (symbol is CiMacro) {
			NextToken();
			Expand((CiMacro) symbol);
			return ParseStatement();
		}
		if (symbol is CiType || symbol is CiClass)
			return ParseVar();
		ICiStatement result = ParseExprWithSideEffect();
		Expect(CiToken.Semicolon);
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
		if (See(CiToken.Const))
			return ParseConst();
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
			if (See(CiToken.Id))
				result.Init = ParseVarOrExpr();
			else
				Expect(CiToken.Semicolon);
			if (!See(CiToken.Semicolon)) {
				result.Cond = ParseExpr();
				ExpectType(result.Cond, CiBoolType.Value);
			}
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
			if (this.CurrentFunction.ReturnType != CiType.Void) {
				result.Value = ParseExpr();
				ExpectType(result.Value, this.CurrentFunction.ReturnType);
			}
			Expect(CiToken.Semicolon);
			return result;
		}
		if (Eat(CiToken.Switch)) {
			Expect(CiToken.LeftParenthesis);
			CiSwitch result = new CiSwitch();
			result.Value = ParseExpr();
			CiType type = result.Value.Type;
			Expect(CiToken.RightParenthesis);
			Expect(CiToken.LeftBrace);
			List<CiCase> cases = new List<CiCase>();
			for (;;) {
				CiCase caze;
				if (Eat(CiToken.Case)) {
					caze = new CiCase();
					caze.Value = ParseConstExpr(type);
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

	CiFunction ParseFunction()
	{
		CiFunction func = new CiFunction();
		if (Eat(CiToken.Void))
			func.ReturnType = CiType.Void;
		else
			func.ReturnType = ParseType();
		func.Name = ParseId();
		this.Symbols.Add(func);
		this.CurrentFunction = func;
	
		Expect(CiToken.LeftParenthesis);
		OpenScope();
		List<CiParam> paramz = new List<CiParam>();
		do {
			CiParam param = new CiParam();
			param.Documentation = ParseDoc();
			param.Type = ParseType();
			param.Name = ParseId();
			this.Symbols.Add(param);
			paramz.Add(param);
		} while (Eat(CiToken.Comma));
		Expect(CiToken.RightParenthesis);
		func.Params = paramz.ToArray();
		func.Body = ParseBlock();
		CloseScope();
		this.CurrentFunction = null;
		return func;
	}

	public CiProgram ParseProgram()
	{
		SymbolTable globals = new SymbolTable();
		globals.Add(CiBoolType.Value);
		globals.Add(CiByteType.Value);
		globals.Add(CiIntType.Value);
		globals.Add(CiStringPtrType.Value);
		globals.Add(new CiConst { Name = "true", Value = true });
		globals.Add(new CiConst { Name = "false", Value = false });
		globals.Add(new CiConst { Name = "null", Value = null });
		this.Symbols = globals;
		this.ConstArrays = new List<CiConst>();
		this.CurrentFunction = null;

		Expect(CiToken.Namespace);
		List<string> namespaceElements = new List<string>();
		namespaceElements.Add(ParseId());
		while (See(CiToken.Dot)) {
			NextToken();
			namespaceElements.Add(ParseId());
		}
		Expect(CiToken.Semicolon);

		while (!See(CiToken.EndOfFile)) {
			while (Eat(CiToken.Macro))
				this.Symbols.Add(ParseMacro());
			CiCodeDoc doc = ParseDoc();
			bool pub = Eat(CiToken.Public);
			CiSymbol symbol;
			if (See(CiToken.Const))
				symbol = ParseConst();
			else if (See(CiToken.Enum))
				symbol = ParseEnum();
			else if (See(CiToken.Class))
				symbol = ParseClass();
			else
				symbol = ParseFunction();
			symbol.Documentation = doc;
			symbol.IsPublic = pub;
		}

		return new CiProgram {
			NamespaceElements = namespaceElements.ToArray(),
			Globals = globals,
			ConstArrays = this.ConstArrays.ToArray()
		};
	}
}

}
