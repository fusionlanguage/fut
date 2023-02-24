// CiResolver.cs - Ci symbol resolver
//
// Copyright (C) 2011-2023  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
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
using System.Linq;
using System.Text;

namespace Foxoft.Ci
{

public class CiResolver : CiSema
{
	readonly List<string> SearchDirs;
	readonly HashSet<CiMethod> CurrentPureMethods = new HashSet<CiMethod>();

	byte[] ReadResource(string name, CiStatement statement)
	{
		foreach (string dir in this.SearchDirs) {
			string path = Path.Combine(dir, name);
			if (File.Exists(path))
				return File.ReadAllBytes(path);
		}
		if (File.Exists(name))
			return File.ReadAllBytes(name);
		ReportError(statement, $"File {name} not found");
		return Array.Empty<byte>();
	}

	protected override CiExpr VisitInterpolatedString(CiInterpolatedString expr)
	{
		int partsCount = 0;
		StringBuilder sb = new StringBuilder();
		for (int partsIndex = 0; partsIndex < expr.Parts.Count; partsIndex++) {
			CiInterpolatedPart part = expr.Parts[partsIndex];
			sb.Append(part.Prefix);
			CiExpr arg = Resolve(part.Argument);
			Coerce(arg, this.Program.System.PrintableType);
			switch (arg.Type) {
			case CiIntegerType _:
				if (" DdXx".IndexOf((char) part.Format) < 0)
					ReportError(arg, "Invalid format string");
				break;
			case CiFloatingType _:
				if (" FfEe".IndexOf((char) part.Format) < 0)
					ReportError(arg, "Invalid format string");
				break;
			default:
				if (part.Format != ' ')
					ReportError(arg, "Invalid format string");
				break;
			}
			int width = 0;
			if (part.WidthExpr != null)
				width = FoldConstInt(part.WidthExpr);
			if (arg is CiLiteral literal && !(arg.Type is CiFloatingType)) { // float formatting is runtime-locale-specific
				string stringArg = part.Format == ' ' ? literal.GetLiteralString()
					: ((CiLiteralLong) arg).Value.ToString((char) part.Format + (part.Precision < 0 ? "" : part.Precision.ToString()));
				if (part.WidthExpr != null)
					stringArg = width >= 0 ? stringArg.PadLeft(width) : stringArg.PadRight(-width);
				sb.Append(stringArg);
			}
			else {
				CiInterpolatedPart targetPart = expr.Parts[partsCount++];
				targetPart.Prefix = sb.ToString();
				targetPart.Argument = arg;
				targetPart.WidthExpr = part.WidthExpr;
				targetPart.Width = width;
				targetPart.Format = part.Format;
				targetPart.Precision = part.Precision;
				sb.Clear();
			}
		}
		sb.Append(expr.Suffix);
		if (partsCount == 0)
			return this.Program.System.NewLiteralString(sb.ToString(), expr.Line);
		expr.Type = this.Program.System.StringStorageType;
		expr.Parts.RemoveRange(partsCount, expr.Parts.Count - partsCount);
		expr.Suffix = sb.ToString();
		return expr;
	}

	protected override CiExpr VisitPrefixExpr(CiPrefixExpr expr)
	{
		CiExpr inner;
		CiType type;
		CiRangeType range;
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			inner = Resolve(expr.Inner);
			CheckLValue(inner);
			Coerce(inner, this.Program.System.DoubleType);
			range = inner.Type as CiRangeType;
			if (range != null) {
				int delta = expr.Op == CiToken.Increment ? 1 : -1;
				type = CiRangeType.New(range.Min + delta, range.Max + delta);
			}
			else
				type = inner.Type;
			expr.Inner = inner;
			expr.Type = type;
			return expr;
		case CiToken.Minus:
			inner = Resolve(expr.Inner);
			Coerce(inner, this.Program.System.DoubleType);
			range = inner.Type as CiRangeType;
			if (range != null)
				type = range = CiRangeType.New(SaturatedNeg(range.Max), SaturatedNeg(range.Min));
			else if (inner is CiLiteralDouble d)
				return ToLiteralDouble(expr, -d.Value);
			else if (inner is CiLiteralLong l)
				return ToLiteralLong(expr, -l.Value);
			else
				type = inner.Type;
			break;
		case CiToken.Tilde:
			inner = Resolve(expr.Inner);
			if (inner.Type is CiEnumFlags) {
				type = inner.Type;
				range = null;
			}
			else {
				Coerce(inner, this.Program.System.IntType);
				range = inner.Type as CiRangeType;
				if (range != null)
					type = range = CiRangeType.New(~range.Max, ~range.Min);
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
			string name = resourceName.Value;
			if (!this.Program.Resources.TryGetValue(name, out byte[] content)) {
				content = ReadResource(name, expr);
				this.Program.Resources.Add(name, content);
			}
			type = new CiArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = this.Program.System.ByteType, Length = content.Length };
			range = null;
			break;
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		if (range != null && range.Min == range.Max)
			return ToLiteralLong(expr, range.Min);
		return new CiPrefixExpr { Line = expr.Line, Op = expr.Op, Inner = inner, Type = type };
	}

	protected override CiExpr VisitCallExpr(CiCallExpr expr)
	{
		if (!(Resolve(expr.Method) is CiSymbolReference symbol))
			return this.Poison;
		List<CiExpr> arguments;
		int i;
		if (this.CurrentPureArguments.Count == 0) {
			arguments = expr.Arguments;
			for (i = 0; i < arguments.Count; i++) {
				if (!(arguments[i] is CiLambdaExpr))
					arguments[i] = Resolve(arguments[i]);
			}
		}
		else {
			arguments = new List<CiExpr>(expr.Arguments.Count);
			foreach (CiExpr arg in expr.Arguments)
				arguments.Add(Resolve(arg));
		}
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

		// TODO: check static
		i = 0;
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
				lambda.First.Type = ((CiClassType) symbol.Left.Type).TypeArg0;
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

		if (method.CallType == CiCallType.Static
		 && method.Body is CiReturn ret
		 && arguments.All(arg => arg is CiLiteral)
		 && this.CurrentPureMethods.Add(method)) {
			i = 0;
			for (CiVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter())
				this.CurrentPureArguments.Add(param, i < arguments.Count ? arguments[i++] : param.Value);
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
						OpenScope(new CiScope()); // don't add "is Derived d" to parameters
					method.Body.AcceptStatement(this);
					if (method.Type.Id != CiId.VoidType && method.Body.CompletesNormally())
						ReportError(method.Body, "Method can complete without a return value");
					this.CurrentMethod = null;
				}
			}
		}
	}

	public CiResolver(CiProgram program, List<string> searchDirs)
	{
		this.Program = program;
		this.SearchDirs = searchDirs;
		for (CiSymbol type = program.First; type != null; type = type.Next) {
			if (type is CiClass klass)
				ResolveBase(klass);
		}
		foreach (CiClass klass in program.Classes)
			CheckBaseCycle(klass);
		for (CiSymbol type = program.First; type != null; type = type.Next)
			ResolveConsts((CiContainerType) type);
		foreach (CiClass klass in program.Classes)
			ResolveTypes(klass);
		foreach (CiClass klass in program.Classes)
			ResolveCode(klass);
		foreach (CiClass klass in program.Classes)
			MarkClassLive(klass);
	}
}

}
