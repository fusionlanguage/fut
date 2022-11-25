// CiTree.cs - Ci abstract syntax tree
//
// Copyright (C) 2011-2022  Piotr Fusik
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
using System.Globalization;
using System.Text;

namespace Foxoft.Ci
{

public class CiLiteralChar : CiLiteralLong
{
	CiLiteralChar()
	{
	}
	public static CiLiteralChar New(int value, int line) => new CiLiteralChar { Line = line, Type = CiRangeType.New(value, value), Value = value };
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralChar((int) this.Value);
		return this;
	}
	public override string ToString()
	{
		switch (this.Value) {
		case '\n': return "'\\n'";
		case '\r': return "'\\r'";
		case '\t': return "'\\t'";
		case '\\': return "'\\\\'";
		case '\'': return "'\\''";
		default: return $"'{(char) this.Value}'";
		}
	}
}

public class CiLiteralDouble : CiLiteral
{
	internal double Value;
	public override bool IsDefaultValue() => BitConverter.DoubleToInt64Bits(this.Value) == 0; // rule out -0.0
	public override CiExpr Accept(CiVisitor visitor, CiPriority parent)
	{
		visitor.VisitLiteralDouble(this.Value);
		return this;
	}
	public override string GetLiteralString() => this.Value.ToString(CultureInfo.InvariantCulture);
	public override string ToString() => GetLiteralString();
}

public class CiSystem : CiScope
{
	internal readonly CiType VoidType = new CiType { Id = CiId.VoidType, Name = "void" };
	internal readonly CiType NullType = new CiType { Id = CiId.NullType, Name = "null" };
	readonly CiType TypeParam0 = new CiType { Id = CiId.TypeParam0, Name = "T" };
	internal readonly CiIntegerType IntType = new CiIntegerType { Id = CiId.IntType, Name = "int" };
	readonly CiRangeType UIntType = CiRangeType.New(0, int.MaxValue);
	internal readonly CiIntegerType LongType = new CiIntegerType { Id = CiId.LongType, Name = "long" };
	internal readonly CiRangeType ByteType = CiRangeType.New(0, 0xff);
	readonly CiFloatingType FloatType = new CiFloatingType { Id = CiId.FloatType, Name = "float" };
	internal readonly CiFloatingType DoubleType = new CiFloatingType { Id = CiId.DoubleType, Name = "double" };
	internal readonly CiRangeType CharType = CiRangeType.New(-0x80, 0xffff);
	internal readonly CiEnum BoolType = new CiEnum { Id = CiId.BoolType, Name = "bool" };
	internal readonly CiStringType StringPtrType = new CiStringType { Id = CiId.StringPtrType, Name = "string" };
	internal readonly CiStringStorageType StringStorageType = new CiStringStorageType { Id = CiId.StringStorageType };
	internal readonly CiType PrintableType = new CiPrintableType { Name = "printable" };
	internal readonly CiClass ArrayPtrClass = CiClass.New(CiCallType.Normal, CiId.ArrayPtrClass, "ArrayPtr", 1);
	internal readonly CiClass ArrayStorageClass = CiClass.New(CiCallType.Normal, CiId.ArrayStorageClass, "ArrayStorage", 1);
	internal readonly CiReadWriteClassType LockPtrType = new CiReadWriteClassType();

	internal CiLiteralLong NewLiteralLong(long value, int line = 0)
	{
		CiType type = value >= int.MinValue && value <= int.MaxValue ? CiRangeType.New((int) value, (int) value) : LongType;
		return new CiLiteralLong { Line = line, Type = type, Value = value };
	}

	internal CiLiteralString NewLiteralString(string value, int line = 0) => new CiLiteralString { Line = line, Type = StringPtrType, Value = value };

	internal CiType PromoteIntegerTypes(CiType left, CiType right)
	{
		return left == LongType || right == LongType ? LongType : IntType;
	}

	internal CiType PromoteFloatingTypes(CiType left, CiType right)
	{
		if (left.Id == CiId.DoubleType || right.Id == CiId.DoubleType)
			return DoubleType;
		if (left.Id == CiId.FloatType || right.Id == CiId.FloatType
		 || left.Id == CiId.FloatIntType || right.Id == CiId.FloatIntType)
			return FloatType;
		return null;
	}

	internal CiType PromoteNumericTypes(CiType left, CiType right) => PromoteFloatingTypes(left, right) ?? PromoteIntegerTypes(left, right);

	CiClass AddCollection(CiId id, string name, int typeParameterCount, CiId clearId, CiId countId)
	{
		CiClass result = CiClass.New(CiCallType.Normal, id, name, typeParameterCount);
		result.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, clearId, "Clear"));
		result.Add(CiMember.New(UIntType, countId, "Count"));
		Add(result);
		return result;
	}

	void AddDictionary(CiId id, string name, CiId clearId, CiId containsKeyId, CiId countId, CiId removeId)
	{
		CiClass dict = AddCollection(id, name, 2, clearId, countId);
		dict.Add(CiMethod.NewMutator(CiVisibility.FinalValueType, VoidType, CiId.DictionaryAdd, "Add", CiVar.New(TypeParam0, "key")));
		dict.Add(CiMethod.New(CiVisibility.Public, BoolType, containsKeyId, "ContainsKey", CiVar.New(TypeParam0, "key")));
		dict.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, removeId, "Remove", CiVar.New(TypeParam0, "key")));
	}

	static void AddEnumValue(CiEnum enu, CiConst value)
	{
		value.Type = enu;
		enu.Add(value);
	}

	CiConst NewConstInt(string name, int value)
	{
		CiConst result = new CiConst { Visibility = CiVisibility.Public, Name = name, Value = NewLiteralLong(value), VisitStatus = CiVisitStatus.Done };
		result.Type = result.Value.Type;
		return result;
	}

	CiConst NewConstDouble(string name, double value)
		=> new CiConst { Visibility = CiVisibility.Public, Name = name, Value = new CiLiteralDouble { Value = value, Type = DoubleType }, Type = DoubleType, VisitStatus = CiVisitStatus.Done };

	CiSystem()
	{
		CiSymbol basePtr = CiVar.New(null, "base");
		basePtr.Id = CiId.BasePtr;
		Add(basePtr);
		Add(IntType);
		UIntType.Name = "uint";
		Add(UIntType);
		Add(LongType);
		ByteType.Name = "byte";
		Add(ByteType);
		CiRangeType shortType = CiRangeType.New(-0x8000, 0x7fff);
		shortType.Name = "short";
		Add(shortType);
		CiRangeType ushortType = CiRangeType.New(0, 0xffff);
		ushortType.Name = "ushort";
		Add(ushortType);
		CiRangeType minus1Type = CiRangeType.New(-1, int.MaxValue);
		Add(FloatType);
		Add(DoubleType);
		Add(BoolType);
		CiClass stringClass = CiClass.New(CiCallType.Normal, CiId.StringClass, "string");
		stringClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.StringContains, "Contains", CiVar.New(StringPtrType, "value")));
		stringClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.StringEndsWith, "EndsWith", CiVar.New(StringPtrType, "value")));
		stringClass.Add(CiMethod.New(CiVisibility.Public, minus1Type, CiId.StringIndexOf, "IndexOf", CiVar.New(StringPtrType, "value")));
		stringClass.Add(CiMethod.New(CiVisibility.Public, minus1Type, CiId.StringLastIndexOf, "LastIndexOf", CiVar.New(StringPtrType, "value")));
		stringClass.Add(CiMember.New(UIntType, CiId.StringLength, "Length"));
		stringClass.Add(CiMethod.New(CiVisibility.Public, StringStorageType, CiId.StringReplace, "Replace", CiVar.New(StringPtrType, "oldValue"), CiVar.New(StringPtrType, "newValue")));
		stringClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.StringStartsWith, "StartsWith", CiVar.New(StringPtrType, "value")));
		stringClass.Add(CiMethod.New(CiVisibility.Public, StringStorageType, CiId.StringSubstring, "Substring", CiVar.New(IntType, "offset"), CiVar.New(IntType, "length", NewLiteralLong(-1)))); // TODO: UIntType
		StringPtrType.Class = stringClass;
		Add(StringPtrType);
		StringStorageType.Class = stringClass;
		CiMethod arrayBinarySearchPart = CiMethod.New(CiVisibility.NumericElementType, IntType, CiId.ArrayBinarySearchPart, "BinarySearch",
			CiVar.New(TypeParam0, "value"), CiVar.New(IntType, "startIndex"), CiVar.New(IntType, "count"));
		ArrayPtrClass.Add(arrayBinarySearchPart);
		ArrayPtrClass.Add(CiMethod.New(CiVisibility.Public, VoidType, CiId.ArrayCopyTo, "CopyTo", CiVar.New(IntType, "sourceIndex"),
			CiVar.New(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = TypeParam0 }, "destinationArray"), CiVar.New(IntType, "destinationIndex"), CiVar.New(IntType, "count")));
		CiMethod arrayFillPart = CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ArrayFillPart, "Fill",
			CiVar.New(TypeParam0, "value"), CiVar.New(IntType, "startIndex"), CiVar.New(IntType, "count"));
		ArrayPtrClass.Add(arrayFillPart);
		CiMethod arraySortPart = CiMethod.NewMutator(CiVisibility.NumericElementType, VoidType, CiId.ArraySortPart, "Sort", CiVar.New(IntType, "startIndex"), CiVar.New(IntType, "count"));
		ArrayPtrClass.Add(arraySortPart);
		ArrayStorageClass.Parent = ArrayPtrClass;
		ArrayStorageClass.Add(CiMethodGroup.New(CiMethod.New(CiVisibility.NumericElementType, IntType, CiId.ArrayBinarySearchAll, "BinarySearch", CiVar.New(TypeParam0, "value")),
			arrayBinarySearchPart));
		ArrayStorageClass.Add(CiMethodGroup.New(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ArrayFillAll, "Fill", CiVar.New(TypeParam0, "value")),
			arrayFillPart));
		ArrayStorageClass.Add(CiMember.New(UIntType, CiId.ArrayLength, "Length"));
		ArrayStorageClass.Add(CiMethodGroup.New(
			CiMethod.NewMutator(CiVisibility.NumericElementType, VoidType, CiId.ArraySortAll, "Sort"),
			arraySortPart));

		CiType typeParam0NotFinal = new CiType { Id = CiId.TypeParam0NotFinal, Name = "T" };
		CiType typeParam0Predicate = new CiType { Id = CiId.TypeParam0Predicate, Name = "Predicate<T>" };
		CiClass listClass = AddCollection(CiId.ListClass, "List", 1, CiId.ListClear, CiId.ListCount);
		listClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ListAdd, "Add", CiVar.New(typeParam0NotFinal, "value")));
		listClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.ListAny, "Any", CiVar.New(typeParam0Predicate, "predicate")));
		listClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.ListContains, "Contains", CiVar.New(TypeParam0, "value")));
		listClass.Add(CiMethod.New(CiVisibility.Public, VoidType, CiId.ListCopyTo, "CopyTo", CiVar.New(IntType, "sourceIndex"),
			CiVar.New(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = TypeParam0 }, "destinationArray"), CiVar.New(IntType, "destinationIndex"), CiVar.New(IntType, "count")));
		listClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ListInsert, "Insert", CiVar.New(UIntType, "index"), CiVar.New(typeParam0NotFinal, "value")));
		listClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ListRemoveAt, "RemoveAt", CiVar.New(IntType, "index")));
		listClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.ListRemoveRange, "RemoveRange", CiVar.New(IntType, "index"), CiVar.New(IntType, "count")));
		listClass.Add(CiMethodGroup.New(
			CiMethod.NewMutator(CiVisibility.NumericElementType, VoidType, CiId.ListSortAll, "Sort"),
			CiMethod.NewMutator(CiVisibility.NumericElementType, VoidType, CiId.ListSortPart, "Sort", CiVar.New(IntType, "startIndex"), CiVar.New(IntType, "count"))));
		CiClass queueClass = AddCollection(CiId.QueueClass, "Queue", 1, CiId.QueueClear, CiId.QueueCount);
		queueClass.Add(CiMethod.NewMutator(CiVisibility.Public, TypeParam0, CiId.QueueDequeue, "Dequeue"));
		queueClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.QueueEnqueue, "Enqueue", CiVar.New(TypeParam0, "value")));
		queueClass.Add(CiMethod.New(CiVisibility.Public, TypeParam0, CiId.QueuePeek, "Peek"));
		CiClass stackClass = AddCollection(CiId.StackClass, "Stack", 1, CiId.StackClear, CiId.StackCount);
		stackClass.Add(CiMethod.New(CiVisibility.Public, TypeParam0, CiId.StackPeek, "Peek"));
		stackClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.StackPush, "Push", CiVar.New(TypeParam0, "value")));
		stackClass.Add(CiMethod.NewMutator(CiVisibility.Public, TypeParam0, CiId.StackPop, "Pop"));
		CiClass hashSetClass = AddCollection(CiId.HashSetClass, "HashSet", 1, CiId.HashSetClear, CiId.HashSetCount);
		hashSetClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.HashSetAdd, "Add", CiVar.New(TypeParam0, "value")));
		hashSetClass.Add(CiMethod.New(CiVisibility.Public, BoolType, CiId.HashSetContains, "Contains", CiVar.New(TypeParam0, "value")));
		hashSetClass.Add(CiMethod.NewMutator(CiVisibility.Public, VoidType, CiId.HashSetRemove, "Remove", CiVar.New(TypeParam0, "value")));
		AddDictionary(CiId.DictionaryClass, "Dictionary", CiId.DictionaryClear, CiId.DictionaryContainsKey, CiId.DictionaryCount, CiId.DictionaryRemove);
		AddDictionary(CiId.SortedDictionaryClass, "SortedDictionary", CiId.SortedDictionaryClear, CiId.SortedDictionaryContainsKey, CiId.SortedDictionaryCount, CiId.SortedDictionaryRemove);
		AddDictionary(CiId.OrderedDictionaryClass, "OrderedDictionary", CiId.OrderedDictionaryClear, CiId.OrderedDictionaryContainsKey, CiId.OrderedDictionaryCount, CiId.OrderedDictionaryRemove);

		CiClass consoleBase = CiClass.New(CiCallType.Static, CiId.None, "ConsoleBase");
		consoleBase.Add(CiMethod.NewStatic(VoidType, CiId.ConsoleWrite, "Write", CiVar.New(PrintableType, "value")));
		consoleBase.Add(CiMethod.NewStatic(VoidType, CiId.ConsoleWriteLine, "WriteLine", CiVar.New(PrintableType, "value", NewLiteralString(""))));
		CiClass consoleClass = CiClass.New(CiCallType.Static, CiId.None, "Console");
		CiMember consoleError = CiMember.New(consoleBase, CiId.ConsoleError, "Error");
		consoleClass.Add(consoleError);
		Add(consoleClass);
		consoleClass.Parent = consoleBase;
		CiClass utf8EncodingClass = CiClass.New(CiCallType.Sealed, CiId.None, "UTF8Encoding");
		utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, IntType, CiId.UTF8GetByteCount, "GetByteCount", CiVar.New(StringPtrType, "str")));
		utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, VoidType, CiId.UTF8GetBytes, "GetBytes",
			CiVar.New(StringPtrType, "str"), CiVar.New(new CiReadWriteClassType { Class = ArrayPtrClass, TypeArg0 = ByteType }, "bytes"), CiVar.New(IntType, "byteIndex")));
		utf8EncodingClass.Add(CiMethod.New(CiVisibility.Public, StringStorageType, CiId.UTF8GetString, "GetString",
			CiVar.New(new CiClassType { Class = ArrayPtrClass, TypeArg0 = ByteType }, "bytes"), CiVar.New(IntType, "offset"), CiVar.New(IntType, "length"))); // TODO: UIntType
		CiClass encodingClass = CiClass.New(CiCallType.Static, CiId.None, "Encoding");
		encodingClass.Add(CiMember.New(utf8EncodingClass, CiId.None, "UTF8"));
		Add(encodingClass);
		CiClass environmentClass = CiClass.New(CiCallType.Static, CiId.None, "Environment");
		environmentClass.Add(CiMethod.NewStatic(StringPtrType, CiId.EnvironmentGetEnvironmentVariable, "GetEnvironmentVariable", CiVar.New(StringPtrType, "name")));
		Add(environmentClass);
		CiEnum regexOptionsEnum = new CiEnumFlags { Name = "RegexOptions" };
		CiConst regexOptionsNone = NewConstInt("None", 0);
		AddEnumValue(regexOptionsEnum, regexOptionsNone);
		AddEnumValue(regexOptionsEnum, NewConstInt("IgnoreCase", 1));
		AddEnumValue(regexOptionsEnum, NewConstInt("Multiline", 2));
		AddEnumValue(regexOptionsEnum, NewConstInt("Singleline", 16));
		Add(regexOptionsEnum);
		CiClass regexClass = CiClass.New(CiCallType.Sealed, CiId.RegexClass, "Regex");
		regexClass.Add(CiMethod.NewStatic(StringStorageType, CiId.RegexEscape, "Escape", CiVar.New(StringPtrType, "str")));
		regexClass.Add(CiMethodGroup.New(
				CiMethod.NewStatic(BoolType, CiId.RegexIsMatchStr, "IsMatch", CiVar.New(StringPtrType, "input"), CiVar.New(StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)),
				CiMethod.New(CiVisibility.Public, BoolType, CiId.RegexIsMatchRegex, "IsMatch", CiVar.New(StringPtrType, "input"))));
		regexClass.Add(CiMethod.NewStatic(new CiDynamicPtrType { Class = regexClass }, CiId.RegexCompile, "Compile", CiVar.New(StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)));
		Add(regexClass);
		CiClass matchClass = CiClass.New(CiCallType.Sealed, CiId.MatchClass, "Match");
		matchClass.Add(CiMethodGroup.New(
				CiMethod.NewMutator(CiVisibility.Public, BoolType, CiId.MatchFindStr, "Find", CiVar.New(StringPtrType, "input"), CiVar.New(StringPtrType, "pattern"), CiVar.New(regexOptionsEnum, "options", regexOptionsNone)),
				CiMethod.NewMutator(CiVisibility.Public, BoolType, CiId.MatchFindRegex, "Find", CiVar.New(StringPtrType, "input"), CiVar.New(new CiClassType { Class = regexClass }, "pattern"))));
		matchClass.Add(CiMember.New(IntType, CiId.MatchStart, "Start"));
		matchClass.Add(CiMember.New(IntType, CiId.MatchEnd, "End"));
		matchClass.Add(CiMethod.New(CiVisibility.Public, StringPtrType, CiId.MatchGetCapture, "GetCapture", CiVar.New(UIntType, "group")));
		matchClass.Add(CiMember.New(UIntType, CiId.MatchLength, "Length"));
		matchClass.Add(CiMember.New(StringPtrType, CiId.MatchValue, "Value"));
		Add(matchClass);

		CiFloatingType floatIntType = new CiFloatingType { Id = CiId.FloatIntType, Name = "float" };
		CiClass mathClass = CiClass.New(CiCallType.Static, CiId.None, "Math");
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Acos", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Asin", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Atan", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Atan2", CiVar.New(DoubleType, "y"), CiVar.New(DoubleType, "x")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Cbrt", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathCeiling, "Ceiling", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Cos", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Cosh", CiVar.New(DoubleType, "a")));
		mathClass.Add(NewConstDouble("E", Math.E));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Exp", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathMethod, "Floor", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathFusedMultiplyAdd, "FusedMultiplyAdd", CiVar.New(DoubleType, "x"), CiVar.New(DoubleType, "y"), CiVar.New(DoubleType, "z")));
		mathClass.Add(CiMethod.NewStatic(BoolType, CiId.MathIsFinite, "IsFinite", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(BoolType, CiId.MathIsInfinity, "IsInfinity", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(BoolType, CiId.MathIsNaN, "IsNaN", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Log", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathLog2, "Log2", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Log10", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMember.New(FloatType, CiId.MathNaN, "NaN"));
		mathClass.Add(CiMember.New(FloatType, CiId.MathNegativeInfinity, "NegativeInfinity"));
		mathClass.Add(NewConstDouble("PI", Math.PI));
		mathClass.Add(CiMember.New(FloatType, CiId.MathPositiveInfinity, "PositiveInfinity"));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Pow", CiVar.New(DoubleType, "x"), CiVar.New(DoubleType, "y")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Sin", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Sinh", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Sqrt", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Tan", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(FloatType, CiId.MathMethod, "Tanh", CiVar.New(DoubleType, "a")));
		mathClass.Add(CiMethod.NewStatic(floatIntType, CiId.MathTruncate, "Truncate", CiVar.New(DoubleType, "a")));
		Add(mathClass);

		CiClass lockClass = CiClass.New(CiCallType.Sealed, CiId.LockClass, "Lock");
		Add(lockClass);
		LockPtrType.Class = lockClass;
	}

	internal static CiSystem New() => new CiSystem();
}

}
