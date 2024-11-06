// Generated automatically with "fut". Do not edit.
#pragma once
#include <array>
#include <cstdint>
#include <iostream>
#include <map>
#include <memory>
#include <set>
#include <sstream>
#include <stack>
#include <string>
#include <string_view>
#include <type_traits>
#include <unordered_map>
#include <unordered_set>
#include <vector>
#define FU_ENUM_FLAG_OPERATORS(T) \
	inline constexpr T operator~(T a) { return static_cast<T>(~static_cast<std::underlying_type_t<T>>(a)); } \
	inline constexpr T operator&(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) & static_cast<std::underlying_type_t<T>>(b)); } \
	inline constexpr T operator|(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) | static_cast<std::underlying_type_t<T>>(b)); } \
	inline constexpr T operator^(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) ^ static_cast<std::underlying_type_t<T>>(b)); } \
	inline constexpr T &operator&=(T &a, T b) { return (a = a & b); } \
	inline constexpr T &operator|=(T &a, T b) { return (a = a | b); } \
	inline constexpr T &operator^=(T &a, T b) { return (a = a ^ b); }

enum class RegexOptions
{
	none = 0,
	ignoreCase = 1,
	multiline = 2,
	singleline = 16
};
FU_ENUM_FLAG_OPERATORS(RegexOptions)
class FuParserHost;

enum class FuToken
{
	endOfFile,
	id,
	literalLong,
	literalDouble,
	literalChar,
	literalString,
	interpolatedString,
	semicolon,
	dot,
	comma,
	leftParenthesis,
	rightParenthesis,
	leftBracket,
	rightBracket,
	leftBrace,
	rightBrace,
	plus,
	minus,
	asterisk,
	slash,
	mod,
	and_,
	or_,
	xor_,
	tilde,
	shiftLeft,
	shiftRight,
	equal,
	notEqual,
	less,
	lessOrEqual,
	greater,
	greaterOrEqual,
	rightAngle,
	condAnd,
	condOr,
	exclamationMark,
	hash,
	assign,
	addAssign,
	subAssign,
	mulAssign,
	divAssign,
	modAssign,
	andAssign,
	orAssign,
	xorAssign,
	shiftLeftAssign,
	shiftRightAssign,
	increment,
	decrement,
	questionMark,
	colon,
	fatArrow,
	range,
	docRegular,
	docBullet,
	docBlank,
	abstract,
	assert,
	break_,
	case_,
	class_,
	const_,
	continue_,
	default_,
	do_,
	else_,
	enum_,
	false_,
	for_,
	foreach,
	if_,
	in,
	internal,
	is,
	lock_,
	native,
	new_,
	null,
	override_,
	protected_,
	public_,
	resource,
	return_,
	sealed,
	static_,
	switch_,
	throw_,
	throws,
	true_,
	virtual_,
	void_,
	when,
	while_,
	endOfLine,
	preUnknown,
	preIf,
	preElIf,
	preElse,
	preEndIf
};

enum class FuPreState
{
	notYet,
	already,
	alreadyElse
};
class FuLexer;

enum class FuVisibility
{
	private_,
	internal,
	protected_,
	public_,
	numericElementType,
	finalValueType
};

enum class FuCallType
{
	static_,
	normal,
	abstract,
	virtual_,
	override_,
	sealed
};

enum class FuPriority
{
	statement,
	argument,
	assign,
	select,
	selectCond,
	condOr,
	condAnd,
	or_,
	xor_,
	and_,
	equality,
	rel,
	shift,
	add,
	mul,
	primary
};

enum class FuId
{
	none,
	voidType,
	nullType,
	basePtr,
	typeParam0,
	typeParam0NotFinal,
	typeParam0Predicate,
	sByteRange,
	byteRange,
	shortRange,
	uShortRange,
	intType,
	nIntType,
	longType,
	floatType,
	doubleType,
	floatIntType,
	floatingType,
	numericType,
	boolType,
	stringClass,
	stringPtrType,
	stringStorageType,
	mainArgsType,
	arrayPtrClass,
	arrayStorageClass,
	exceptionClass,
	listClass,
	queueClass,
	stackClass,
	priorityQueueClass,
	hashSetClass,
	sortedSetClass,
	dictionaryClass,
	sortedDictionaryClass,
	orderedDictionaryClass,
	textWriterClass,
	stringWriterClass,
	regexOptionsEnum,
	regexClass,
	matchClass,
	jsonElementClass,
	lockClass,
	stringLength,
	arrayLength,
	consoleError,
	main,
	classToString,
	matchStart,
	matchEnd,
	matchLength,
	matchValue,
	mathNaN,
	mathNegativeInfinity,
	mathPositiveInfinity,
	enumFromInt,
	enumHasFlag,
	intTryParse,
	longTryParse,
	doubleTryParse,
	stringContains,
	stringEndsWith,
	stringIndexOf,
	stringLastIndexOf,
	stringReplace,
	stringStartsWith,
	stringSubstring,
	stringToLower,
	stringToUpper,
	arrayBinarySearchAll,
	arrayBinarySearchPart,
	arrayContains,
	arrayCopyTo,
	arrayFillAll,
	arrayFillPart,
	arraySortAll,
	arraySortPart,
	listAdd,
	listAddRange,
	listAll,
	listAny,
	listClear,
	listContains,
	listCopyTo,
	listCount,
	listIndexOf,
	listInsert,
	listLast,
	listRemoveAt,
	listRemoveRange,
	listSortAll,
	listSortPart,
	queueClear,
	queueCount,
	queueDequeue,
	queueEnqueue,
	queuePeek,
	stackClear,
	stackCount,
	stackPeek,
	stackPush,
	stackPop,
	priorityQueueClear,
	priorityQueueCount,
	priorityQueueDequeue,
	priorityQueueEnqueue,
	priorityQueuePeek,
	hashSetAdd,
	hashSetClear,
	hashSetContains,
	hashSetCount,
	hashSetRemove,
	sortedSetAdd,
	sortedSetClear,
	sortedSetContains,
	sortedSetCount,
	sortedSetRemove,
	dictionaryAdd,
	dictionaryClear,
	dictionaryContainsKey,
	dictionaryCount,
	dictionaryRemove,
	sortedDictionaryClear,
	sortedDictionaryContainsKey,
	sortedDictionaryCount,
	sortedDictionaryRemove,
	orderedDictionaryClear,
	orderedDictionaryContainsKey,
	orderedDictionaryCount,
	orderedDictionaryRemove,
	textWriterWrite,
	textWriterWriteChar,
	textWriterWriteCodePoint,
	textWriterWriteLine,
	consoleWrite,
	consoleWriteLine,
	stringWriterClear,
	stringWriterToString,
	convertToBase64String,
	uTF8GetByteCount,
	uTF8GetBytes,
	uTF8GetString,
	environmentGetEnvironmentVariable,
	regexCompile,
	regexEscape,
	regexIsMatchStr,
	regexIsMatchRegex,
	matchFindStr,
	matchFindRegex,
	matchGetCapture,
	jsonElementParse,
	jsonElementIsObject,
	jsonElementIsArray,
	jsonElementIsString,
	jsonElementIsNumber,
	jsonElementIsBoolean,
	jsonElementIsNull,
	jsonElementGetObject,
	jsonElementGetArray,
	jsonElementGetString,
	jsonElementGetDouble,
	jsonElementGetBoolean,
	mathMethod,
	mathAbs,
	mathCeiling,
	mathClamp,
	mathFusedMultiplyAdd,
	mathIsFinite,
	mathIsInfinity,
	mathIsNaN,
	mathLog2,
	mathMax,
	mathMin,
	mathRound,
	mathTruncate
};
class FuDocInline;
class FuDocText;
class FuDocCode;
class FuDocLine;
class FuDocBlock;
class FuDocPara;
class FuDocList;
class FuCodeDoc;
class FuVisitor;
class FuStatement;
class FuExpr;
class FuName;
class FuSymbol;
class FuScope;
class FuAggregateInitializer;
class FuLiteral;
class FuLiteralNull;
class FuLiteralFalse;
class FuLiteralTrue;
class FuLiteralLong;
class FuLiteralChar;
class FuLiteralDouble;
class FuLiteralString;
class FuInterpolatedPart;
class FuInterpolatedString;
class FuImplicitEnumValue;
class FuSymbolReference;
class FuUnaryExpr;
class FuPrefixExpr;
class FuPostfixExpr;
class FuBinaryExpr;
class FuSelectExpr;
class FuCallExpr;
class FuLambdaExpr;
class FuCondCompletionStatement;
class FuBlock;
class FuAssert;
class FuLoop;
class FuBreak;
class FuContinue;
class FuDoWhile;
class FuFor;
class FuForeach;
class FuIf;
class FuLock;
class FuNative;
class FuReturn;
class FuCase;
class FuSwitch;
class FuThrow;
class FuWhile;
class FuParameters;
class FuType;
class FuNumericType;
class FuIntegerType;
class FuRangeType;
class FuFloatingType;
class FuNamedValue;
class FuMember;
class FuVar;

enum class FuVisitStatus
{
	notYet,
	inProgress,
	done
};
class FuConst;
class FuField;
class FuProperty;
class FuStaticProperty;
class FuThrowsDeclaration;
class FuMethodBase;
class FuMethod;
class FuMethodGroup;
class FuContainerType;
class FuEnum;
class FuEnumFlags;
class FuClass;
class FuClassType;
class FuReadWriteClassType;
class FuOwningType;
class FuStorageType;
class FuDynamicPtrType;
class FuArrayStorageType;
class FuStringType;
class FuStringStorageType;
class FuPrintableType;
class FuSystem;
class FuSourceFile;
class FuProgram;
class FuParser;
class FuConsoleHost;
class FuSemaHost;
class FuSema;
class GenHost;
class GenBase;
class GenTyped;
class GenCCppD;
class GenCCpp;
class GenC;
class GenCl;
class GenCpp;
class GenCs;
class GenD;
class GenJava;
class GenJsNoModule;
class GenJs;
class GenTs;
class GenPySwift;
class GenSwift;
class GenPy;

class FuParserHost
{
public:
	virtual ~FuParserHost() = default;
	virtual void reportError(std::string_view filename, int line, int startUtf16Column, int endUtf16Column, std::string_view message) = 0;
protected:
	FuParserHost() = default;
public:
	FuProgram * program;
	void reportStatementError(const FuStatement * statement, std::string_view message);
};

class FuLexer
{
public:
	virtual ~FuLexer() = default;
	void setHost(FuParserHost * host);
	void addPreSymbol(std::string_view symbol);
protected:
	FuLexer() = default;
	uint8_t const * input;
	int charOffset;
	FuParserHost * host;
	int loc = 0;
	int tokenLoc;
	int lexemeOffset;
	FuToken currentToken;
	int64_t longValue;
	std::string stringValue;
	bool parsingTypeArg = false;
	void open(std::string_view filename, uint8_t const * input, int inputLength);
	void reportError(std::string_view message) const;
	int peekChar() const;
	int readChar();
	FuToken readString(bool interpolated);
	std::string getLexeme() const;
	bool see(FuToken token) const;
	bool check(FuToken expected) const;
	FuToken nextToken();
	bool eat(FuToken token);
	bool expect(FuToken expected);
	void expectOrSkip(FuToken expected);
public:
	static bool isLetterOrDigit(int c);
	static int getEscapedChar(int c);
	static std::string_view tokenToString(FuToken token);
private:
	int inputLength;
	int nextOffset;
	int nextChar;
	std::unordered_set<std::string> preSymbols;
	bool atLineStart = true;
	bool lineMode = false;
	bool skippingUnmet = false;
	std::stack<bool> preElseStack;
	int readByte();
	static constexpr int replacementChar = 65533;
	int readContinuationByte(int hi);
	void fillNextChar();
	bool eatChar(int c);
	void skipWhitespace();
	FuToken readIntegerLiteral(int bits);
	FuToken readFloatLiteral(bool needDigit);
	FuToken readNumberLiteral(int64_t i);
	int readCharLiteral();
	bool endWord(int c);
	FuToken readPreToken();
	void nextPreToken();
	bool eatPre(FuToken token);
	bool parsePrePrimary();
	bool parsePreEquality();
	bool parsePreAnd();
	bool parsePreOr();
	bool parsePreExpr();
	void expectEndOfLine(std::string_view directive);
	bool popPreElse(std::string_view directive);
	void skipUnmet(FuPreState state);
	FuToken readToken();
};

class FuDocInline
{
public:
	virtual ~FuDocInline() = default;
protected:
	FuDocInline() = default;
};

class FuDocText : public FuDocInline
{
public:
	FuDocText() = default;
public:
	std::string text;
};

class FuDocCode : public FuDocInline
{
public:
	FuDocCode() = default;
public:
	std::string text;
};

class FuDocLine : public FuDocInline
{
public:
	FuDocLine() = default;
};

class FuDocBlock
{
public:
	virtual ~FuDocBlock() = default;
protected:
	FuDocBlock() = default;
};

class FuDocPara : public FuDocBlock
{
public:
	FuDocPara() = default;
public:
	std::vector<std::shared_ptr<FuDocInline>> children;
};

class FuDocList : public FuDocBlock
{
public:
	FuDocList() = default;
public:
	std::vector<FuDocPara> items;
};

class FuCodeDoc
{
public:
	FuCodeDoc() = default;
public:
	FuDocPara summary;
	std::vector<std::shared_ptr<FuDocBlock>> details;
};

class FuVisitor
{
public:
	virtual ~FuVisitor() = default;
protected:
	FuVisitor() = default;
	void visitOptionalStatement(const FuStatement * statement);
public:
	virtual void visitConst(const FuConst * statement) = 0;
	virtual void visitExpr(const FuExpr * statement) = 0;
	virtual void visitBlock(const FuBlock * statement) = 0;
	virtual void visitAssert(const FuAssert * statement) = 0;
	virtual void visitBreak(const FuBreak * statement) = 0;
	virtual void visitContinue(const FuContinue * statement) = 0;
	virtual void visitDoWhile(const FuDoWhile * statement) = 0;
	virtual void visitFor(const FuFor * statement) = 0;
	virtual void visitForeach(const FuForeach * statement) = 0;
	virtual void visitIf(const FuIf * statement) = 0;
	virtual void visitLock(const FuLock * statement) = 0;
	virtual void visitNative(const FuNative * statement) = 0;
	virtual void visitReturn(const FuReturn * statement) = 0;
	virtual void visitSwitch(const FuSwitch * statement) = 0;
	virtual void visitThrow(const FuThrow * statement) = 0;
	virtual void visitWhile(const FuWhile * statement) = 0;
	virtual void visitEnumValue(const FuConst * konst, const FuConst * previous) = 0;
	virtual void visitLiteralNull() = 0;
	virtual void visitLiteralFalse() = 0;
	virtual void visitLiteralTrue() = 0;
	virtual void visitLiteralLong(int64_t value) = 0;
	virtual void visitLiteralChar(int value) = 0;
	virtual void visitLiteralDouble(double value) = 0;
	virtual void visitLiteralString(std::string_view value) = 0;
	virtual void visitAggregateInitializer(const FuAggregateInitializer * expr) = 0;
	virtual void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) = 0;
	virtual void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) = 0;
	virtual void visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent) = 0;
	virtual void visitPostfixExpr(const FuPostfixExpr * expr, FuPriority parent) = 0;
	virtual void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) = 0;
	virtual void visitSelectExpr(const FuSelectExpr * expr, FuPriority parent) = 0;
	virtual void visitCallExpr(const FuCallExpr * expr, FuPriority parent) = 0;
	virtual void visitLambdaExpr(const FuLambdaExpr * expr) = 0;
	virtual void visitVar(const FuVar * expr) = 0;
};

class FuStatement
{
public:
	virtual ~FuStatement() = default;
	virtual int getLocLength() const;
	virtual bool completesNormally() const = 0;
	virtual void acceptStatement(FuVisitor * visitor) const = 0;
protected:
	FuStatement() = default;
public:
	int loc = 0;
};

class FuExpr : public FuStatement
{
public:
	virtual ~FuExpr() = default;
	bool completesNormally() const override;
	virtual std::string toString() const;
	virtual bool isIndexing() const;
	virtual bool isLiteralZero() const;
	virtual bool isConstEnum() const;
	virtual int intValue() const;
	virtual void accept(FuVisitor * visitor, FuPriority parent) const;
	void acceptStatement(FuVisitor * visitor) const override;
	virtual bool isReferenceTo(const FuSymbol * symbol) const;
	virtual bool isNewString(bool substringOffset) const;
	virtual bool isUnique() const;
	virtual void setShared() const;
protected:
	FuExpr() = default;
public:
	std::shared_ptr<FuType> type;
};

class FuName : public FuExpr
{
public:
	virtual ~FuName() = default;
	int getLocLength() const override;
	virtual const FuSymbol * getSymbol() const = 0;
protected:
	FuName() = default;
public:
	std::string name{""};
};

class FuSymbol : public FuName
{
public:
	virtual ~FuSymbol() = default;
	const FuSymbol * getSymbol() const override;
	std::string toString() const override;
protected:
	FuSymbol() = default;
public:
	FuId id = FuId::none;
	FuSymbol * next;
	FuScope * parent;
	std::shared_ptr<FuCodeDoc> documentation = nullptr;
};

class FuScope : public FuSymbol
{
public:
	virtual ~FuScope() = default;
	int count() const;
	bool contains(const FuSymbol * symbol) const;
	std::shared_ptr<FuSymbol> tryLookup(std::string_view name, bool global) const;
	void add(std::shared_ptr<FuSymbol> symbol);
	bool encloses(const FuSymbol * symbol) const;
protected:
	FuScope() = default;
	std::unordered_map<std::string_view, std::shared_ptr<FuSymbol>> dict;
	void addToList(std::shared_ptr<FuSymbol> symbol);
public:
	FuSymbol * first = nullptr;
	FuSymbol * last = nullptr;
};

class FuAggregateInitializer : public FuExpr
{
public:
	FuAggregateInitializer() = default;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
public:
	std::vector<std::shared_ptr<FuExpr>> items;
};

class FuLiteral : public FuExpr
{
public:
	virtual ~FuLiteral() = default;
	virtual bool isDefaultValue() const = 0;
	virtual std::string getLiteralString() const;
protected:
	FuLiteral() = default;
};

class FuLiteralNull : public FuLiteral
{
public:
	FuLiteralNull() = default;
	int getLocLength() const override;
	bool isDefaultValue() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	bool isUnique() const override;
	std::string toString() const override;
};

class FuLiteralFalse : public FuLiteral
{
public:
	FuLiteralFalse() = default;
	int getLocLength() const override;
	bool isDefaultValue() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	std::string toString() const override;
};

class FuLiteralTrue : public FuLiteral
{
public:
	FuLiteralTrue() = default;
	int getLocLength() const override;
	bool isDefaultValue() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	std::string toString() const override;
};

class FuLiteralLong : public FuLiteral
{
public:
	FuLiteralLong() = default;
	virtual ~FuLiteralLong() = default;
	bool isLiteralZero() const override;
	int intValue() const override;
	bool isDefaultValue() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	std::string getLiteralString() const override;
	std::string toString() const override;
public:
	int64_t value;
};

class FuLiteralChar : public FuLiteralLong
{
public:
	FuLiteralChar() = default;
	static std::shared_ptr<FuLiteralChar> new_(int value, int loc);
	int getLocLength() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
};

class FuLiteralDouble : public FuLiteral
{
public:
	FuLiteralDouble() = default;
	bool isDefaultValue() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	std::string getLiteralString() const override;
	std::string toString() const override;
public:
	double value;
};

class FuLiteralString : public FuLiteral
{
public:
	FuLiteralString() = default;
	bool isDefaultValue() const override;
	int getLocLength() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	std::string getLiteralString() const override;
	std::string toString() const override;
	int getAsciiLength() const;
	int getAsciiAt(int i) const;
	int getOneAscii() const;
public:
	std::string value;
};

class FuInterpolatedPart
{
public:
	FuInterpolatedPart() = default;
public:
	std::string prefix;
	std::shared_ptr<FuExpr> argument;
	std::shared_ptr<FuExpr> widthExpr;
	int width;
	int format;
	int precision;
};

class FuInterpolatedString : public FuExpr
{
public:
	FuInterpolatedString() = default;
	void addPart(std::string_view prefix, std::shared_ptr<FuExpr> arg, std::shared_ptr<FuExpr> widthExpr = nullptr, int format = ' ', int precision = -1);
	int getLocLength() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	bool isNewString(bool substringOffset) const override;
public:
	std::vector<FuInterpolatedPart> parts;
	std::string suffix;
};

class FuImplicitEnumValue : public FuExpr
{
public:
	FuImplicitEnumValue() = default;
	int intValue() const override;
public:
	int value;
};

class FuSymbolReference : public FuName
{
public:
	FuSymbolReference() = default;
	virtual ~FuSymbolReference() = default;
	bool isConstEnum() const override;
	int intValue() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	bool isReferenceTo(const FuSymbol * symbol) const override;
	bool isNewString(bool substringOffset) const override;
	void setShared() const override;
	const FuSymbol * getSymbol() const override;
	std::string toString() const override;
public:
	std::shared_ptr<FuExpr> left = nullptr;
	FuSymbol * symbol;
};

class FuUnaryExpr : public FuExpr
{
public:
	virtual ~FuUnaryExpr() = default;
	int getLocLength() const override;
protected:
	FuUnaryExpr() = default;
public:
	FuToken op;
	std::shared_ptr<FuExpr> inner;
};

class FuPrefixExpr : public FuUnaryExpr
{
public:
	FuPrefixExpr() = default;
	bool isConstEnum() const override;
	int intValue() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	bool isUnique() const override;
};

class FuPostfixExpr : public FuUnaryExpr
{
public:
	FuPostfixExpr() = default;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
};

class FuBinaryExpr : public FuExpr
{
public:
	FuBinaryExpr() = default;
	int getLocLength() const override;
	bool isIndexing() const override;
	bool isConstEnum() const override;
	int intValue() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	bool isNewString(bool substringOffset) const override;
	bool isRel() const;
	bool isAssign() const;
	std::string_view getOpString() const;
	std::string toString() const override;
public:
	std::shared_ptr<FuExpr> left;
	FuToken op;
	std::shared_ptr<FuExpr> right;
};

class FuSelectExpr : public FuExpr
{
public:
	FuSelectExpr() = default;
	int getLocLength() const override;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	bool isUnique() const override;
	void setShared() const override;
	std::string toString() const override;
public:
	std::shared_ptr<FuExpr> cond;
	std::shared_ptr<FuExpr> onTrue;
	std::shared_ptr<FuExpr> onFalse;
};

class FuCallExpr : public FuExpr
{
public:
	FuCallExpr() = default;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	bool isNewString(bool substringOffset) const override;
public:
	std::shared_ptr<FuSymbolReference> method;
	std::vector<std::shared_ptr<FuExpr>> arguments;
};

class FuLambdaExpr : public FuScope
{
public:
	FuLambdaExpr() = default;
	void accept(FuVisitor * visitor, FuPriority parent) const override;
public:
	std::shared_ptr<FuExpr> body;
};

class FuCondCompletionStatement : public FuScope
{
public:
	virtual ~FuCondCompletionStatement() = default;
	bool completesNormally() const override;
	void setCompletesNormally(bool value);
protected:
	FuCondCompletionStatement() = default;
private:
	bool completesNormallyValue;
};

class FuBlock : public FuCondCompletionStatement
{
public:
	FuBlock() = default;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	std::vector<std::shared_ptr<FuStatement>> statements;
};

class FuAssert : public FuStatement
{
public:
	FuAssert() = default;
	int getLocLength() const override;
	bool completesNormally() const override;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	std::shared_ptr<FuExpr> cond;
	std::shared_ptr<FuExpr> message = nullptr;
};

class FuLoop : public FuCondCompletionStatement
{
public:
	virtual ~FuLoop() = default;
protected:
	FuLoop() = default;
public:
	std::shared_ptr<FuExpr> cond;
	std::shared_ptr<FuStatement> body;
	bool hasBreak = false;
};

class FuBreak : public FuStatement
{
public:
	FuBreak() = default;
	int getLocLength() const override;
	bool completesNormally() const override;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	FuCondCompletionStatement * loopOrSwitch;
};

class FuContinue : public FuStatement
{
public:
	FuContinue() = default;
	int getLocLength() const override;
	bool completesNormally() const override;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	const FuLoop * loop;
};

class FuDoWhile : public FuLoop
{
public:
	FuDoWhile() = default;
	int getLocLength() const override;
	void acceptStatement(FuVisitor * visitor) const override;
};

class FuFor : public FuLoop
{
public:
	FuFor() = default;
	int getLocLength() const override;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	std::shared_ptr<FuExpr> init;
	std::shared_ptr<FuExpr> advance;
	bool isRange = false;
	bool isIndVarUsed;
	int64_t rangeStep;
};

class FuForeach : public FuLoop
{
public:
	FuForeach() = default;
	int getLocLength() const override;
	void acceptStatement(FuVisitor * visitor) const override;
	FuVar * getVar() const;
	FuVar * getValueVar() const;
public:
	std::shared_ptr<FuExpr> collection;
};

class FuIf : public FuCondCompletionStatement
{
public:
	FuIf() = default;
	int getLocLength() const override;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	std::shared_ptr<FuExpr> cond;
	std::shared_ptr<FuStatement> onTrue;
	std::shared_ptr<FuStatement> onFalse;
};

class FuLock : public FuStatement
{
public:
	FuLock() = default;
	int getLocLength() const override;
	bool completesNormally() const override;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	std::shared_ptr<FuExpr> lock;
	std::shared_ptr<FuStatement> body;
};

class FuNative : public FuSymbol
{
public:
	FuNative() = default;
	int getLocLength() const override;
	bool completesNormally() const override;
	void acceptStatement(FuVisitor * visitor) const override;
	const FuMember * getFollowingMember() const;
public:
	std::string content;
};

class FuReturn : public FuScope
{
public:
	FuReturn() = default;
	int getLocLength() const override;
	bool completesNormally() const override;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	std::shared_ptr<FuExpr> value;
};

class FuCase
{
public:
	FuCase() = default;
public:
	std::vector<std::shared_ptr<FuExpr>> values;
	std::vector<std::shared_ptr<FuStatement>> body;
};

class FuSwitch : public FuCondCompletionStatement
{
public:
	FuSwitch() = default;
	int getLocLength() const override;
	void acceptStatement(FuVisitor * visitor) const override;
	bool isTypeMatching() const;
	bool hasWhen() const;
	static int lengthWithoutTrailingBreak(const std::vector<std::shared_ptr<FuStatement>> * body);
	bool hasDefault() const;
	static bool hasEarlyBreak(const std::vector<std::shared_ptr<FuStatement>> * body);
	static bool hasEarlyBreakAndContinue(const std::vector<std::shared_ptr<FuStatement>> * body);
public:
	std::shared_ptr<FuExpr> value;
	std::vector<FuCase> cases;
	std::vector<std::shared_ptr<FuStatement>> defaultBody;
private:
	static bool hasBreak(const FuStatement * statement);
	static bool listHasContinue(const std::vector<std::shared_ptr<FuStatement>> * statements);
	static bool hasContinue(const FuStatement * statement);
};

class FuThrow : public FuStatement
{
public:
	FuThrow() = default;
	int getLocLength() const override;
	bool completesNormally() const override;
	void acceptStatement(FuVisitor * visitor) const override;
public:
	std::shared_ptr<FuSymbolReference> class_;
	std::shared_ptr<FuExpr> message;
};

class FuWhile : public FuLoop
{
public:
	FuWhile() = default;
	int getLocLength() const override;
	void acceptStatement(FuVisitor * visitor) const override;
};

class FuParameters : public FuScope
{
public:
	FuParameters() = default;
};

class FuType : public FuScope
{
public:
	FuType() = default;
	virtual ~FuType() = default;
	virtual std::string getArraySuffix() const;
	virtual bool isAssignableFrom(const FuType * right) const;
	virtual bool equalsType(const FuType * right) const;
	virtual bool isArray() const;
	virtual bool isFinal() const;
	virtual const FuType * getBaseType() const;
	virtual const FuType * getStorageType() const;
	const FuClassType * asClassType() const;
public:
	bool nullable = false;
};

class FuNumericType : public FuType
{
public:
	FuNumericType() = default;
	virtual ~FuNumericType() = default;
};

class FuIntegerType : public FuNumericType
{
public:
	FuIntegerType() = default;
	virtual ~FuIntegerType() = default;
	bool isAssignableFrom(const FuType * right) const override;
};

class FuRangeType : public FuIntegerType
{
public:
	FuRangeType() = default;
	static std::shared_ptr<FuRangeType> new_(int min, int max);
	std::string toString() const override;
	bool isAssignableFrom(const FuType * right) const override;
	bool equalsType(const FuType * right) const override;
	static int getMask(int v);
	int getVariableBits() const;
public:
	int min;
	int max;
private:
	static void addMinMaxValue(std::shared_ptr<FuRangeType> target, std::string_view name, int value);
};

class FuFloatingType : public FuNumericType
{
public:
	FuFloatingType() = default;
	bool isAssignableFrom(const FuType * right) const override;
};

class FuNamedValue : public FuSymbol
{
public:
	virtual ~FuNamedValue() = default;
	bool isAssignableStorage() const;
protected:
	FuNamedValue() = default;
public:
	std::shared_ptr<FuExpr> typeExpr;
	std::shared_ptr<FuExpr> value;
};

class FuMember : public FuNamedValue
{
public:
	virtual ~FuMember() = default;
	virtual bool isStatic() const = 0;
protected:
	FuMember();
public:
	FuVisibility visibility;
	int startLine;
	int startColumn;
	int endLine;
	int endColumn;
};

class FuVar : public FuNamedValue
{
public:
	FuVar() = default;
	static std::shared_ptr<FuVar> new_(std::shared_ptr<FuType> type, std::string_view name, std::shared_ptr<FuExpr> defaultValue = nullptr);
	void accept(FuVisitor * visitor, FuPriority parent) const override;
	FuVar * nextVar() const;
public:
	bool isAssigned = false;
};

class FuConst : public FuMember
{
public:
	FuConst() = default;
	void acceptStatement(FuVisitor * visitor) const override;
	bool isStatic() const override;
public:
	const FuMethodBase * inMethod;
	int inMethodIndex = 0;
	FuVisitStatus visitStatus;
};

class FuField : public FuMember
{
public:
	FuField() = default;
	bool isStatic() const override;
};

class FuProperty : public FuMember
{
public:
	FuProperty() = default;
	bool isStatic() const override;
	static std::shared_ptr<FuProperty> new_(std::shared_ptr<FuType> type, FuId id, std::string_view name);
};

class FuStaticProperty : public FuMember
{
public:
	FuStaticProperty() = default;
	bool isStatic() const override;
	static std::shared_ptr<FuStaticProperty> new_(std::shared_ptr<FuType> type, FuId id, std::string_view name);
};

class FuThrowsDeclaration : public FuSymbolReference
{
public:
	FuThrowsDeclaration() = default;
public:
	std::shared_ptr<FuCodeDoc> documentation;
};

class FuMethodBase : public FuMember
{
public:
	FuMethodBase() = default;
	virtual ~FuMethodBase() = default;
	bool isStatic() const override;
	void addThis(const FuClass * klass, bool isMutator);
	bool isMutator() const;
public:
	FuParameters parameters;
	std::vector<std::shared_ptr<FuThrowsDeclaration>> throws;
	std::shared_ptr<FuScope> body;
	bool isLive = false;
	std::unordered_set<FuMethod *> calls;
};

class FuMethod : public FuMethodBase
{
public:
	FuMethod() = default;
	static std::shared_ptr<FuMethod> new_(const FuClass * klass, FuVisibility visibility, FuCallType callType, std::shared_ptr<FuType> type, FuId id, std::string_view name, bool isMutator, std::shared_ptr<FuVar> param0 = nullptr, std::shared_ptr<FuVar> param1 = nullptr, std::shared_ptr<FuVar> param2 = nullptr, std::shared_ptr<FuVar> param3 = nullptr);
	bool isStatic() const override;
	bool isAbstractOrVirtual() const;
	bool isAbstractVirtualOrOverride() const;
	static std::string_view callTypeToString(FuCallType callType);
	FuVar * firstParameter() const;
	int getParametersCount() const;
	const FuMethod * getDeclaringMethod() const;
public:
	FuCallType callType;
};

class FuMethodGroup : public FuMember
{
public:
	FuMethodGroup();
	bool isStatic() const override;
	static std::shared_ptr<FuMethodGroup> new_(std::shared_ptr<FuMethod> method0, std::shared_ptr<FuMethod> method1);
public:
	std::array<std::shared_ptr<FuMethod>, 2> methods;
};

class FuContainerType : public FuType
{
public:
	virtual ~FuContainerType() = default;
protected:
	FuContainerType() = default;
public:
	bool isPublic;
	int startLine;
	int startColumn;
	int endLine;
	int endColumn;
};

class FuEnum : public FuContainerType
{
public:
	FuEnum() = default;
	virtual ~FuEnum() = default;
	const FuSymbol * getFirstValue() const;
	void acceptValues(FuVisitor * visitor) const;
public:
	bool hasExplicitValue = false;
};

class FuEnumFlags : public FuEnum
{
public:
	FuEnumFlags() = default;
};

class FuClass : public FuContainerType
{
public:
	FuClass() = default;
	bool hasBaseClass() const;
	bool addsVirtualMethods() const;
	static std::shared_ptr<FuClass> new_(FuCallType callType, FuId id, std::string_view name, int typeParameterCount = 0);
	void addMethod(std::shared_ptr<FuType> type, FuId id, std::string_view name, bool isMutator, std::shared_ptr<FuVar> param0 = nullptr, std::shared_ptr<FuVar> param1 = nullptr, std::shared_ptr<FuVar> param2 = nullptr, std::shared_ptr<FuVar> param3 = nullptr);
	void addStaticMethod(std::shared_ptr<FuType> type, FuId id, std::string_view name, std::shared_ptr<FuVar> param0, std::shared_ptr<FuVar> param1 = nullptr, std::shared_ptr<FuVar> param2 = nullptr);
	void addNative(std::shared_ptr<FuNative> nat);
	bool isSameOrBaseOf(const FuClass * derived) const;
	bool hasToString() const;
	bool addsToString() const;
public:
	FuCallType callType;
	int typeParameterCount = 0;
	bool hasSubclasses = false;
	FuSymbolReference baseClass;
	std::shared_ptr<FuMethodBase> constructor;
	std::vector<FuConst *> constArrays;
private:
	std::vector<std::shared_ptr<FuNative>> natives;
};

class FuClassType : public FuType
{
public:
	FuClassType() = default;
	virtual ~FuClassType() = default;
	std::shared_ptr<FuType> getElementType() const;
	const FuType * getKeyType() const;
	std::shared_ptr<FuType> getValueType() const;
	bool isArray() const override;
	const FuType * getBaseType() const override;
	bool isAssignableFrom(const FuType * right) const override;
	bool equalsType(const FuType * right) const override;
	std::string getArraySuffix() const override;
	virtual std::string_view getClassSuffix() const;
	std::string toString() const override;
protected:
	bool isAssignableFromClass(const FuClassType * right) const;
	bool equalsTypeInternal(const FuClassType * that) const;
public:
	const FuClass * class_;
	std::shared_ptr<FuType> typeArg0;
	std::shared_ptr<FuType> typeArg1;
	bool equalTypeArguments(const FuClassType * right) const;
private:
	std::string_view getNullableSuffix() const;
};

class FuReadWriteClassType : public FuClassType
{
public:
	FuReadWriteClassType() = default;
	virtual ~FuReadWriteClassType() = default;
	bool isAssignableFrom(const FuType * right) const override;
	bool equalsType(const FuType * right) const override;
	std::string getArraySuffix() const override;
	std::string_view getClassSuffix() const override;
};

class FuOwningType : public FuReadWriteClassType
{
public:
	virtual ~FuOwningType() = default;
protected:
	FuOwningType() = default;
};

class FuStorageType : public FuOwningType
{
public:
	FuStorageType() = default;
	virtual ~FuStorageType() = default;
	bool isFinal() const override;
	bool isAssignableFrom(const FuType * right) const override;
	bool equalsType(const FuType * right) const override;
	std::string_view getClassSuffix() const override;
};

class FuDynamicPtrType : public FuOwningType
{
public:
	FuDynamicPtrType() = default;
	bool isAssignableFrom(const FuType * right) const override;
	bool equalsType(const FuType * right) const override;
	std::string getArraySuffix() const override;
	std::string_view getClassSuffix() const override;
public:
	bool unique = false;
};

class FuArrayStorageType : public FuStorageType
{
public:
	FuArrayStorageType() = default;
	const FuType * getBaseType() const override;
	bool isArray() const override;
	std::string getArraySuffix() const override;
	bool equalsType(const FuType * right) const override;
	const FuType * getStorageType() const override;
public:
	std::shared_ptr<FuExpr> lengthExpr;
	int length;
	bool ptrTaken = false;
};

class FuStringType : public FuClassType
{
public:
	FuStringType() = default;
	virtual ~FuStringType() = default;
};

class FuStringStorageType : public FuStringType
{
public:
	FuStringStorageType() = default;
	bool isAssignableFrom(const FuType * right) const override;
	std::string_view getClassSuffix() const override;
};

class FuPrintableType : public FuType
{
public:
	FuPrintableType() = default;
	bool isAssignableFrom(const FuType * right) const override;
};

class FuSystem : public FuScope
{
public:
	FuSystem();
public:
	std::shared_ptr<FuType> voidType = std::make_shared<FuType>();
	std::shared_ptr<FuType> nullType = std::make_shared<FuType>();
	std::shared_ptr<FuIntegerType> intType = std::make_shared<FuIntegerType>();
	std::shared_ptr<FuIntegerType> longType = std::make_shared<FuIntegerType>();
	std::shared_ptr<FuRangeType> byteType = FuRangeType::new_(0, 255);
	std::shared_ptr<FuFloatingType> floatType = std::make_shared<FuFloatingType>();
	std::shared_ptr<FuFloatingType> doubleType = std::make_shared<FuFloatingType>();
	std::shared_ptr<FuRangeType> charType = FuRangeType::new_(-128, 65535);
	std::shared_ptr<FuEnum> boolType = std::make_shared<FuEnum>();
	std::shared_ptr<FuStringType> stringPtrType = std::make_shared<FuStringType>();
	std::shared_ptr<FuStringType> stringNullablePtrType = std::make_shared<FuStringType>();
	std::shared_ptr<FuStringStorageType> stringStorageType = std::make_shared<FuStringStorageType>();
	std::shared_ptr<FuType> printableType = std::make_shared<FuPrintableType>();
	std::shared_ptr<FuClass> arrayPtrClass = FuClass::new_(FuCallType::normal, FuId::arrayPtrClass, "ArrayPtr", 1);
	std::shared_ptr<FuClass> arrayStorageClass = FuClass::new_(FuCallType::normal, FuId::arrayStorageClass, "ArrayStorage", 1);
	std::shared_ptr<FuEnum> regexOptionsEnum;
	std::unique_ptr<FuReadWriteClassType> lockPtrType = std::make_unique<FuReadWriteClassType>();
	std::shared_ptr<FuLiteralLong> newLiteralLong(int64_t value, int loc = 0) const;
	std::shared_ptr<FuLiteralString> newLiteralString(std::string_view value, int loc = 0) const;
	std::shared_ptr<FuType> promoteIntegerTypes(const FuType * left, const FuType * right) const;
	std::shared_ptr<FuType> promoteFloatingTypes(const FuType * left, const FuType * right) const;
	std::shared_ptr<FuType> promoteNumericTypes(std::shared_ptr<FuType> left, std::shared_ptr<FuType> right) const;
	std::shared_ptr<FuEnum> newEnum(bool flags) const;
	static std::shared_ptr<FuSystem> new_();
private:
	std::shared_ptr<FuType> typeParam0 = std::make_shared<FuType>();
	std::shared_ptr<FuRangeType> uIntType = FuRangeType::new_(0, 2147483647);
	std::shared_ptr<FuIntegerType> nIntType = std::make_shared<FuIntegerType>();
	std::shared_ptr<FuClass> stringClass = FuClass::new_(FuCallType::normal, FuId::stringClass, "string");
	FuClass * addCollection(FuId id, std::string_view name, int typeParameterCount, FuId clearId, FuId countId);
	void addSet(FuId id, std::string_view name, FuId addId, FuId clearId, FuId containsId, FuId countId, FuId removeId);
	const FuClass * addDictionary(FuId id, std::string_view name, FuId clearId, FuId containsKeyId, FuId countId, FuId removeId);
	static void addEnumValue(std::shared_ptr<FuEnum> enu, std::shared_ptr<FuConst> value);
	std::shared_ptr<FuConst> newConstLong(std::string_view name, int64_t value) const;
	std::shared_ptr<FuConst> newConstDouble(std::string_view name, double value) const;
	void addMinMaxValue(FuIntegerType * target, int64_t min, int64_t max) const;
};

class FuSourceFile
{
public:
	FuSourceFile() = default;
public:
	std::string filename;
	int line;
};

class FuProgram : public FuScope
{
public:
	FuProgram() = default;
public:
	const FuSystem * system;
	std::vector<std::string> topLevelNatives;
	std::vector<FuClass *> classes;
	const FuMethod * main = nullptr;
	std::map<std::string, std::vector<uint8_t>> resources;
	bool regexOptionsEnum = false;
	std::vector<int> lineLocs;
	std::vector<FuSourceFile> sourceFiles;
	int getLine(int loc) const;
	const FuSourceFile * getSourceFile(int line) const;
};

class FuParser : public FuLexer
{
public:
	FuParser() = default;
	void findName(std::string_view filename, int line, int column);
	const FuSymbol * getFoundDefinition() const;
	void parse(std::string_view filename, uint8_t const * input, int inputLength);
private:
	std::string_view xcrementParent = std::string_view();
	const FuLoop * currentLoop = nullptr;
	FuCondCompletionStatement * currentLoopOrSwitch = nullptr;
	std::string findNameFilename;
	int findNameLine = -1;
	int findNameColumn;
	const FuName * foundName = nullptr;
	bool docParseLine(FuDocPara * para);
	void docParsePara(FuDocPara * para);
	std::shared_ptr<FuCodeDoc> parseDoc();
	void checkXcrementParent();
	std::shared_ptr<FuLiteralDouble> parseDouble();
	bool seeDigit() const;
	std::shared_ptr<FuInterpolatedString> parseInterpolatedString();
	std::shared_ptr<FuExpr> parseParenthesized();
	bool isFindName() const;
	bool parseName(FuName * result);
	void parseCollection(std::vector<std::shared_ptr<FuExpr>> * result, FuToken closing);
	std::shared_ptr<FuExpr> parsePrimaryExpr(bool type);
	std::shared_ptr<FuExpr> parseMulExpr();
	std::shared_ptr<FuExpr> parseAddExpr();
	std::shared_ptr<FuExpr> parseShiftExpr();
	std::shared_ptr<FuExpr> parseRelExpr();
	std::shared_ptr<FuExpr> parseEqualityExpr();
	std::shared_ptr<FuExpr> parseAndExpr();
	std::shared_ptr<FuExpr> parseXorExpr();
	std::shared_ptr<FuExpr> parseOrExpr();
	std::shared_ptr<FuExpr> parseCondAndExpr();
	std::shared_ptr<FuExpr> parseCondOrExpr();
	std::shared_ptr<FuExpr> parseExpr();
	std::shared_ptr<FuExpr> parseType();
	std::shared_ptr<FuExpr> parseConstInitializer();
	std::shared_ptr<FuAggregateInitializer> parseObjectLiteral();
	std::shared_ptr<FuExpr> parseInitializer();
	void addSymbol(FuScope * scope, std::shared_ptr<FuSymbol> symbol);
	std::shared_ptr<FuVar> parseVar(std::shared_ptr<FuExpr> type, bool initializer);
	std::shared_ptr<FuConst> parseConst(FuVisibility visibility);
	std::shared_ptr<FuExpr> parseAssign(bool allowVar);
	std::shared_ptr<FuBlock> parseBlock(FuMethodBase * method);
	std::shared_ptr<FuAssert> parseAssert();
	std::shared_ptr<FuBreak> parseBreak();
	std::shared_ptr<FuContinue> parseContinue();
	void parseLoopBody(FuLoop * loop);
	std::shared_ptr<FuDoWhile> parseDoWhile();
	std::shared_ptr<FuFor> parseFor();
	void parseForeachIterator(FuForeach * result);
	std::shared_ptr<FuForeach> parseForeach();
	std::shared_ptr<FuIf> parseIf();
	std::shared_ptr<FuLock> parseLock();
	std::shared_ptr<FuNative> parseNative();
	int getCurrentLine() const;
	int getTokenColumn() const;
	void setMemberEnd(FuMember * member) const;
	void closeMember(FuToken expected, FuMember * member);
	void closeContainer(FuContainerType * type);
	std::shared_ptr<FuReturn> parseReturn(FuMethod * method);
	std::shared_ptr<FuSwitch> parseSwitch();
	std::shared_ptr<FuThrow> parseThrow();
	std::shared_ptr<FuWhile> parseWhile();
	std::shared_ptr<FuStatement> parseStatement();
	FuCallType parseCallType();
	void parseMethod(FuClass * klass, std::shared_ptr<FuMethod> method);
	void reportFormerError(int line, int column, int length, std::string_view message) const;
	void reportCallTypeError(int line, int column, std::string_view kind, FuCallType callType) const;
	void parseClass(std::shared_ptr<FuCodeDoc> doc, int line, int column, bool isPublic, FuCallType callType);
	void parseEnum(std::shared_ptr<FuCodeDoc> doc, int line, int column, bool isPublic);
};

class FuSemaHost : public FuParserHost
{
public:
	virtual ~FuSemaHost() = default;
protected:
	FuSemaHost() = default;
public:
	virtual int getResourceLength(std::string_view name, const FuPrefixExpr * expr);
};

class GenHost : public FuSemaHost
{
public:
	virtual ~GenHost() = default;
	virtual std::ostream * createFile(std::string_view directory, std::string_view filename) = 0;
	virtual void closeFile() = 0;
protected:
	GenHost() = default;
};

class FuConsoleHost : public GenHost
{
public:
	void reportError(std::string_view filename, int line, int startUtf16Column, int endUtf16Column, std::string_view message) override;
protected:
	FuConsoleHost() = default;
public:
	bool hasErrors = false;
};

class FuSema
{
public:
	FuSema();
	void setHost(FuSemaHost * host);
	void process();
private:
	FuSemaHost * host;
	FuMethodBase * currentMethod = nullptr;
	FuScope * currentScope;
	std::unordered_set<const FuMethod *> currentPureMethods;
	std::unordered_map<const FuVar *, std::shared_ptr<FuExpr>> currentPureArguments;
	std::shared_ptr<FuType> poison = std::make_shared<FuType>();
	void reportError(const FuStatement * statement, std::string_view message) const;
	std::shared_ptr<FuType> poisonError(const FuStatement * statement, std::string_view message) const;
	void resolveBase(FuClass * klass);
	void checkBaseCycle(FuClass * klass);
	static void takePtr(const FuExpr * expr);
	bool coerce(const FuExpr * expr, const FuType * type) const;
	bool coercePermanent(const FuExpr * expr, const FuType * type) const;
	std::shared_ptr<FuExpr> visitInterpolatedString(std::shared_ptr<FuInterpolatedString> expr);
	std::shared_ptr<FuExpr> lookup(std::shared_ptr<FuSymbolReference> expr, const FuScope * scope);
	FuContainerType * getCurrentContainer() const;
	std::shared_ptr<FuExpr> visitSymbolReference(std::shared_ptr<FuSymbolReference> expr);
	static std::shared_ptr<FuRangeType> union_(std::shared_ptr<FuRangeType> left, std::shared_ptr<FuRangeType> right);
	std::shared_ptr<FuType> getIntegerType(const FuExpr * left, const FuExpr * right) const;
	std::shared_ptr<FuType> getShiftType(const FuExpr * left, const FuExpr * right) const;
	std::shared_ptr<FuType> getNumericType(const FuExpr * left, const FuExpr * right) const;
	static int saturatedNeg(int a);
	static int saturatedAdd(int a, int b);
	static int saturatedSub(int a, int b);
	static int saturatedMul(int a, int b);
	static int saturatedDiv(int a, int b);
	static int saturatedShiftRight(int a, int b);
	static std::shared_ptr<FuRangeType> bitwiseUnsignedOp(const FuRangeType * left, FuToken op, const FuRangeType * right);
	bool isEnumOp(const FuExpr * left, const FuExpr * right) const;
	std::shared_ptr<FuType> bitwiseOp(const FuExpr * left, FuToken op, const FuExpr * right) const;
	static std::shared_ptr<FuRangeType> newRangeType(int a, int b, int c, int d);
	std::shared_ptr<FuLiteral> toLiteralBool(const FuExpr * expr, bool value) const;
	std::shared_ptr<FuLiteralLong> toLiteralLong(const FuExpr * expr, int64_t value) const;
	std::shared_ptr<FuLiteralDouble> toLiteralDouble(const FuExpr * expr, double value) const;
	void checkLValue(const FuExpr * expr) const;
	std::shared_ptr<FuInterpolatedString> concatenate(const FuInterpolatedString * left, const FuInterpolatedString * right) const;
	std::shared_ptr<FuInterpolatedString> toInterpolatedString(std::shared_ptr<FuExpr> expr) const;
	void checkComparison(const FuExpr * left, const FuExpr * right) const;
	void openScope(FuScope * scope);
	void closeScope();
	std::shared_ptr<FuExpr> resolveNew(std::shared_ptr<FuPrefixExpr> expr);
	std::shared_ptr<FuExpr> visitPrefixExpr(std::shared_ptr<FuPrefixExpr> expr);
	std::shared_ptr<FuExpr> visitPostfixExpr(std::shared_ptr<FuPostfixExpr> expr);
	static bool canCompareEqual(const FuType * left, const FuType * right);
	std::shared_ptr<FuExpr> resolveEquality(const FuBinaryExpr * expr, std::shared_ptr<FuExpr> left, std::shared_ptr<FuExpr> right) const;
	void setSharedAssign(const FuExpr * left, const FuExpr * right) const;
	void checkIsHierarchy(const FuClassType * leftPtr, const FuExpr * left, const FuClass * rightClass, const FuExpr * expr, std::string_view op, std::string_view alwaysMessage, std::string_view neverMessage) const;
	void checkIsVar(const FuExpr * left, const FuVar * def, const FuExpr * expr, std::string_view op, std::string_view alwaysMessage, std::string_view neverMessage) const;
	std::shared_ptr<FuExpr> resolveIs(std::shared_ptr<FuBinaryExpr> expr, std::shared_ptr<FuExpr> left, const FuExpr * right) const;
	std::shared_ptr<FuExpr> visitBinaryExpr(std::shared_ptr<FuBinaryExpr> expr);
	std::shared_ptr<FuType> tryGetPtr(std::shared_ptr<FuType> type, bool nullable) const;
	static const FuClass * getLowestCommonAncestor(const FuClass * left, const FuClass * right);
	std::shared_ptr<FuType> getCommonType(const FuExpr * left, const FuExpr * right) const;
	std::shared_ptr<FuExpr> visitSelectExpr(const FuSelectExpr * expr);
	std::shared_ptr<FuType> evalType(const FuClassType * generic, std::shared_ptr<FuType> type) const;
	bool canCall(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * arguments) const;
	static bool methodHasThrows(const FuMethodBase * method, const FuClass * exception);
	std::shared_ptr<FuExpr> resolveCallWithArguments(std::shared_ptr<FuCallExpr> expr, const std::vector<std::shared_ptr<FuExpr>> * arguments);
	std::shared_ptr<FuExpr> visitCallExpr(std::shared_ptr<FuCallExpr> expr);
	void resolveObjectLiteral(const FuClassType * klass, const FuAggregateInitializer * init);
	static void initUnique(const FuNamedValue * varOrField);
	void visitVar(std::shared_ptr<FuVar> expr);
	std::shared_ptr<FuExpr> visitExpr(std::shared_ptr<FuExpr> expr);
	std::shared_ptr<FuExpr> resolveBool(std::shared_ptr<FuExpr> expr);
	static std::shared_ptr<FuClassType> createClassPtr(const FuClass * klass, FuToken ptrModifier, bool nullable);
	void fillGenericClass(FuClassType * result, const FuClass * klass, const FuAggregateInitializer * typeArgExprs);
	bool expectNoPtrModifier(const FuExpr * expr, FuToken ptrModifier, bool nullable) const;
	std::shared_ptr<FuType> toBaseType(FuExpr * expr, FuToken ptrModifier, bool nullable);
	std::shared_ptr<FuType> toType(std::shared_ptr<FuExpr> expr, bool dynamic);
	std::shared_ptr<FuType> resolveType(FuNamedValue * def);
	void visitAssert(FuAssert * statement);
	bool resolveStatements(const std::vector<std::shared_ptr<FuStatement>> * statements);
	void checkInitialized(const FuVar * def);
	void visitBlock(FuBlock * statement);
	void resolveLoopCond(FuLoop * statement);
	void visitDoWhile(FuDoWhile * statement);
	void visitFor(FuFor * statement);
	void visitForeach(FuForeach * statement);
	void visitIf(FuIf * statement);
	void visitLock(FuLock * statement);
	void visitReturn(FuReturn * statement);
	void resolveCaseType(FuSwitch * statement, const FuClassType * switchPtr, std::shared_ptr<FuExpr> value);
	void visitSwitch(FuSwitch * statement);
	void resolveException(std::shared_ptr<FuSymbolReference> symbol);
	void visitThrow(FuThrow * statement);
	void visitWhile(FuWhile * statement);
	void visitStatement(std::shared_ptr<FuStatement> statement);
	std::shared_ptr<FuExpr> foldConst(std::shared_ptr<FuExpr> expr);
	int foldConstInt(std::shared_ptr<FuExpr> expr);
	void resolveConst(FuConst * konst);
	void resolveConsts(FuContainerType * container);
	void resolveTypes(FuClass * klass);
	void resolveCode(FuClass * klass);
	static void markMethodLive(FuMethodBase * method);
	static void markClassLive(const FuClass * klass);
};

class GenBase : public FuVisitor
{
public:
	virtual ~GenBase() = default;
	void setHost(GenHost * host);
	virtual void writeProgram(const FuProgram * program) = 0;
protected:
	GenBase() = default;
	int indent = 0;
	bool atLineStart = true;
	bool inHeaderFile = false;
	const FuMethodBase * currentMethod = nullptr;
	std::unordered_set<const FuClass *> writtenClasses;
	std::vector<const FuSwitch *> switchesWithGoto;
	std::vector<const FuExpr *> currentTemporaries;
	virtual std::string_view getTargetName() const = 0;
	void notSupported(const FuStatement * statement, std::string_view feature) const;
	void notYet(const FuStatement * statement, std::string_view feature) const;
	virtual void startLine();
	void writeChar(int c);
	void write(std::string_view s);
	virtual int getLiteralChars() const;
	void writeLowercase(std::string_view s);
	void writeCamelCase(std::string_view s);
	void writePascalCase(std::string_view s);
	void writeUppercaseWithUnderscores(std::string_view s);
	void writeLowercaseWithUnderscores(std::string_view s);
	void writeNewLine();
	void writeCharLine(int c);
	void writeLine(std::string_view s);
	void writeUppercaseConstName(const FuConst * konst);
	virtual void writeName(const FuSymbol * symbol) = 0;
	virtual void writeBanner();
	void createFile(std::string_view directory, std::string_view filename);
	void createOutputFile();
	void closeFile();
	void openStringWriter();
	void closeStringWriter();
	void include(std::string_view name);
	void writeIncludes(std::string_view prefix, std::string_view suffix);
	virtual void startDocLine();
	void writeXmlDoc(std::string_view text);
	virtual void writeDocCode(std::string_view s);
	virtual void writeDocPara(const FuDocPara * para, bool many);
	virtual void writeDocList(const FuDocList * list);
	void writeDocBlock(const FuDocBlock * block, bool many);
	void writeContent(const FuCodeDoc * doc);
	virtual void writeDoc(const FuCodeDoc * doc);
	virtual void writeSelfDoc(const FuMethod * method);
	virtual void writeParameterDoc(const FuVar * param, bool first);
	virtual void writeReturnDoc(const FuMethod * method);
	virtual void writeThrowsDoc(const FuThrowsDeclaration * decl);
	void writeParametersAndThrowsDoc(const FuMethod * method);
	void writeMethodDoc(const FuMethod * method);
	void writeTopLevelNatives(const FuProgram * program);
	void openBlock();
	void closeBlock();
	virtual void endStatement();
	void writeComma(int i);
	void writeBytes(const std::vector<uint8_t> * content);
	virtual FuId getTypeId(const FuType * type, bool promote) const;
	virtual void writeTypeAndName(const FuNamedValue * value) = 0;
	virtual void writeLocalName(const FuSymbol * symbol, FuPriority parent);
	void writeDoubling(std::string_view s, int doubled);
	virtual void writePrintfWidth(const FuInterpolatedPart * part);
	void writePrintfFormat(const FuInterpolatedString * expr);
	void writePyFormat(const FuInterpolatedPart * part);
	virtual void writeInterpolatedStringArg(const FuExpr * expr);
	void writeInterpolatedStringArgs(const FuInterpolatedString * expr);
	void writePrintf(const FuInterpolatedString * expr, bool newLine);
	void writePostfix(const FuExpr * obj, std::string_view s);
	void writeCall(std::string_view function, const FuExpr * arg0, const FuExpr * arg1 = nullptr, const FuExpr * arg2 = nullptr);
	virtual void writeMemberOp(const FuExpr * left, const FuSymbolReference * symbol);
	void writeMethodCall(const FuExpr * obj, std::string_view method, const FuExpr * arg0, const FuExpr * arg1 = nullptr);
	void writeInParentheses(const std::vector<std::shared_ptr<FuExpr>> * args);
	virtual void writeSelectValues(const FuType * type, const FuSelectExpr * expr);
	virtual void writeCoercedSelect(const FuType * type, const FuSelectExpr * expr, FuPriority parent);
	virtual void writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent);
	void writeCoerced(const FuType * type, const FuExpr * expr, FuPriority parent);
	virtual void writeCoercedExpr(const FuType * type, const FuExpr * expr);
	virtual void writeStronglyCoerced(const FuType * type, const FuExpr * expr);
	virtual void writeCoercedLiteral(const FuType * type, const FuExpr * expr);
	void writeCoercedLiterals(const FuType * type, const std::vector<std::shared_ptr<FuExpr>> * exprs);
	void writeCoercedArgs(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeCoercedArgsInParentheses(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args);
	virtual void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) = 0;
	virtual void writeNewArrayStorage(const FuArrayStorageType * array);
	virtual void writeNew(const FuReadWriteClassType * klass, FuPriority parent) = 0;
	void writeNewStorage(const FuType * type);
	virtual void writeArrayStorageInit(const FuArrayStorageType * array, const FuExpr * value);
	virtual void writeNewWithFields(const FuReadWriteClassType * type, const FuAggregateInitializer * init);
	virtual void writeStorageInit(const FuNamedValue * def);
	virtual void writeVarInit(const FuNamedValue * def);
	virtual void writeVar(const FuNamedValue * def);
	void writeObjectLiteral(const FuAggregateInitializer * init, std::string_view separator);
	virtual void writeInitCode(const FuNamedValue * def);
	virtual void defineIsVar(const FuBinaryExpr * binary);
	void writeArrayElement(const FuNamedValue * def, int nesting);
	void openLoop(std::string_view intString, int nesting, int count);
	void writeTemporaryName(int id);
	bool tryWriteTemporary(const FuExpr * expr);
	void writeResourceName(std::string_view name);
	virtual void writeResource(std::string_view name, int length) = 0;
	bool isWholeArray(const FuExpr * array, const FuExpr * offset, const FuExpr * length) const;
	void startAdd(const FuExpr * expr);
	void writeAdd(const FuExpr * left, const FuExpr * right);
	void writeStartEnd(const FuExpr * startIndex, const FuExpr * length);
	virtual void writeBinaryOperand(const FuExpr * expr, FuPriority parent, const FuBinaryExpr * binary);
	void writeBinaryExpr(const FuBinaryExpr * expr, bool parentheses, FuPriority left, std::string_view op, FuPriority right);
	void writeBinaryExpr2(const FuBinaryExpr * expr, FuPriority parent, FuPriority child, std::string_view op);
	static std::string_view getEqOp(bool not_);
	virtual void writeEqualOperand(const FuExpr * expr, const FuExpr * other);
	void writeEqualExpr(const FuExpr * left, const FuExpr * right, FuPriority parent, std::string_view op);
	virtual void writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_);
	virtual void writeRel(const FuBinaryExpr * expr, FuPriority parent, std::string_view op);
	virtual void writeAnd(const FuBinaryExpr * expr, FuPriority parent);
	virtual void writeAssignRight(const FuBinaryExpr * expr);
	virtual void writeAssign(const FuBinaryExpr * expr, FuPriority parent);
	virtual void writeOpAssignRight(const FuBinaryExpr * expr);
	void writeIndexing(const FuExpr * collection, const FuExpr * index);
	virtual void writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent);
	virtual std::string_view getIsOperator() const;
	virtual void writeStringLength(const FuExpr * expr) = 0;
	virtual void writeArrayLength(const FuExpr * expr, FuPriority parent);
	static bool isReferenceTo(const FuExpr * expr, FuId id);
	bool writeJavaMatchProperty(const FuSymbolReference * expr, FuPriority parent);
	virtual void writeCharAt(const FuBinaryExpr * expr) = 0;
	virtual void writeNotPromoted(const FuType * type, const FuExpr * expr);
	virtual void writeEnumAsInt(const FuExpr * expr, FuPriority parent);
	void writeEnumHasFlag(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent);
	void writeTryParseRadix(const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeListAdd(const FuExpr * obj, std::string_view method, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeListInsert(const FuExpr * obj, std::string_view method, const std::vector<std::shared_ptr<FuExpr>> * args, std::string_view separator = ", ");
	void writeDictionaryAdd(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeClampAsMinMax(const std::vector<std::shared_ptr<FuExpr>> * args);
	RegexOptions getRegexOptions(const std::vector<std::shared_ptr<FuExpr>> * args) const;
	bool writeRegexOptions(const std::vector<std::shared_ptr<FuExpr>> * args, std::string_view prefix, std::string_view separator, std::string_view suffix, std::string_view i, std::string_view m, std::string_view s);
	virtual void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) = 0;
	void ensureChildBlock();
	static bool hasTemporaries(const FuExpr * expr);
	virtual void startTemporaryVar(const FuType * type) = 0;
	virtual void defineObjectLiteralTemporary(const FuUnaryExpr * expr);
	virtual void writeTemporariesNotSubstring(const FuExpr * expr);
	virtual void writeOwningTemporary(const FuExpr * expr);
	virtual void writeArgTemporary(const FuMethod * method, const FuVar * param, const FuExpr * arg);
	void writeTemporaries(const FuExpr * expr);
	virtual void cleanupTemporary(int i, const FuExpr * temp);
	void cleanupTemporaries();
	virtual void writeAssertCast(const FuBinaryExpr * expr) = 0;
	virtual void writeAssert(const FuAssert * statement) = 0;
	void writeFirstStatements(const std::vector<std::shared_ptr<FuStatement>> * statements, int count);
	virtual void writeStatements(const std::vector<std::shared_ptr<FuStatement>> * statements);
	virtual void cleanupBlock(const FuBlock * statement);
	virtual void writeChild(FuStatement * statement);
	virtual void startBreakGoto();
	virtual bool embedIfWhileIsVar(const FuExpr * expr, bool write);
	virtual void startIf(const FuExpr * expr);
	void defineVar(const FuExpr * value);
	virtual void writeSwitchCaseTypeVar(const FuExpr * value);
	virtual void writeSwitchValue(const FuExpr * expr);
	virtual void writeSwitchCaseValue(const FuSwitch * statement, const FuExpr * value);
	virtual void writeSwitchCaseBody(const std::vector<std::shared_ptr<FuStatement>> * statements);
	virtual void writeSwitchCase(const FuSwitch * statement, const FuCase * kase);
	void startSwitch(const FuSwitch * statement);
	virtual void writeSwitchCaseCond(const FuSwitch * statement, const FuExpr * value, FuPriority parent);
	virtual void writeIfCaseBody(const std::vector<std::shared_ptr<FuStatement>> * body, bool doWhile, const FuSwitch * statement, const FuCase * kase);
	void writeSwitchAsIfs(const FuSwitch * statement, bool doWhile);
	virtual void writeException();
	void writeExceptionClass(const FuSymbol * klass);
	virtual void writeThrowNoMessage();
	void writeThrowArgument(const FuThrow * statement);
	void flattenBlock(FuStatement * statement);
	virtual bool hasInitCode(const FuNamedValue * def) const;
	virtual bool needsConstructor(const FuClass * klass) const;
	virtual void writeInitField(const FuField * field);
	void writeConstructorBody(const FuClass * klass);
	virtual void writeParameter(const FuVar * param);
	void writeRemainingParameters(const FuMethod * method, bool first, bool defaultArguments);
	void writeParameters(const FuMethod * method, bool defaultArguments);
	virtual bool isShortMethod(const FuMethod * method) const;
	void writeBody(const FuMethod * method);
	void writePublic(const FuContainerType * container);
	void writeEnumValue(const FuConst * konst);
	virtual void writeEnum(const FuEnum * enu) = 0;
	virtual void writeRegexOptionsEnum(const FuProgram * program);
	void startClass(const FuClass * klass, std::string_view suffix, std::string_view extendsClause);
	void openClass(const FuClass * klass, std::string_view suffix, std::string_view extendsClause);
	virtual void writeConst(const FuConst * konst) = 0;
	virtual void writeField(const FuField * field) = 0;
	virtual void writeMethod(const FuMethod * method) = 0;
	void writeMembers(const FuClass * klass, bool constArrays);
	bool writeBaseClass(const FuClass * klass, const FuProgram * program);
	virtual void writeClass(const FuClass * klass, const FuProgram * program) = 0;
	void writeTypes(const FuProgram * program);
public:
	std::string namespace_;
	std::string outputFile;
	void visitLiteralNull() override;
	void visitLiteralFalse() override;
	void visitLiteralTrue() override;
	void visitLiteralLong(int64_t i) override;
	void visitLiteralChar(int c) override;
	void visitLiteralDouble(double value) override;
	void visitLiteralString(std::string_view value) override;
	void visitVar(const FuVar * expr) override;
	void visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent) override;
	void visitPostfixExpr(const FuPostfixExpr * expr, FuPriority parent) override;
	void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitCallExpr(const FuCallExpr * expr, FuPriority parent) override;
	void visitSelectExpr(const FuSelectExpr * expr, FuPriority parent) override;
	void visitExpr(const FuExpr * statement) override;
	void visitConst(const FuConst * statement) override;
	void visitAssert(const FuAssert * statement) override;
	void visitBlock(const FuBlock * statement) override;
	void visitBreak(const FuBreak * statement) override;
	void visitContinue(const FuContinue * statement) override;
	void visitDoWhile(const FuDoWhile * statement) override;
	void visitFor(const FuFor * statement) override;
	void visitIf(const FuIf * statement) override;
	void visitNative(const FuNative * statement) override;
	void visitReturn(const FuReturn * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
	void visitThrow(const FuThrow * statement) override;
	void visitWhile(const FuWhile * statement) override;
	void visitEnumValue(const FuConst * konst, const FuConst * previous) override;
private:
	GenHost * host;
	std::ostream * writer;
	std::ostringstream stringWriter;
	bool atChildStart = false;
	bool inChildBlock = false;
	std::map<std::string, bool> includes;
	void reportError(const FuStatement * statement, std::string_view message) const;
	void writeLowercaseChar(int c);
	void writeUppercaseChar(int c);
	static int getPrintfFormat(const FuType * type, int format);
	static const FuAggregateInitializer * getAggregateInitializer(const FuNamedValue * def);
	void writeAggregateInitField(const FuExpr * obj, const FuExpr * item);
	static bool isBitOp(FuPriority parent);
	void startIfWhile(const FuExpr * expr);
	void writeIf(const FuIf * statement);
};

class GenTyped : public GenBase
{
public:
	virtual ~GenTyped() = default;
protected:
	GenTyped() = default;
	virtual void writeType(const FuType * type, bool promote) = 0;
	void writeCoercedLiteral(const FuType * type, const FuExpr * expr) override;
	void writeTypeAndName(const FuNamedValue * value) override;
	void writeArrayStorageLength(const FuExpr * expr);
	void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) override;
	int getOneAscii(const FuExpr * expr) const;
	void writeCharMethodCall(const FuExpr * obj, std::string_view method, const FuExpr * arg);
	static bool isNarrower(FuId left, FuId right);
	const FuExpr * getStaticCastInner(const FuType * type, const FuExpr * expr) const;
	void writeStaticCastType(const FuType * type);
	virtual void writeStaticCast(const FuType * type, const FuExpr * expr);
	void writeNotPromoted(const FuType * type, const FuExpr * expr) override;
	virtual bool isPromoted(const FuExpr * expr) const;
	void writeAssignRight(const FuBinaryExpr * expr) override;
	void writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent) override;
	void writeCharAt(const FuBinaryExpr * expr) override;
	void startTemporaryVar(const FuType * type) override;
	void writeAssertCast(const FuBinaryExpr * expr) override;
	void writeExceptionConstructor(const FuClass * klass, std::string_view s);
public:
	void visitAggregateInitializer(const FuAggregateInitializer * expr) override;
};

class GenCCppD : public GenTyped
{
public:
	virtual ~GenCCppD() = default;
protected:
	GenCCppD() = default;
	void writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_) override;
	void writeSwitchAsIfsWithGoto(const FuSwitch * statement);
	void writeThrowNoMessage() override;
public:
	void visitLiteralLong(int64_t i) override;
	void visitConst(const FuConst * statement) override;
private:
	static bool isPtrTo(const FuExpr * ptr, const FuExpr * other);
};

class GenCCpp : public GenCCppD
{
public:
	virtual ~GenCCpp() = default;
protected:
	GenCCpp() = default;
	void writeDocCode(std::string_view s) override;
	virtual void includeStdInt() = 0;
	virtual void includeStdDef() = 0;
	virtual void includeAssert() = 0;
	virtual void includeMath() = 0;
	int getLiteralChars() const override;
	virtual void writeNumericType(FuId id);
	void writeArrayLength(const FuExpr * expr, FuPriority parent) override;
	void writeArgsIndexing(const FuExpr * index);
	static const FuExpr * isStringEmpty(const FuBinaryExpr * expr);
	virtual void writeArrayPtr(const FuExpr * expr, FuPriority parent) = 0;
	void writeArrayPtrAdd(const FuExpr * array, const FuExpr * index);
	static const FuCallExpr * isStringSubstring(const FuExpr * expr);
	static bool isUTF8GetString(const FuCallExpr * call);
	static const FuExpr * isTrimSubstring(const FuBinaryExpr * expr);
	void writeStringLiteralWithNewLine(std::string_view s);
	virtual void writeUnreachable(const FuAssert * statement);
	void writeAssert(const FuAssert * statement) override;
	void writeMethods(const FuClass * klass);
	virtual void writeClassInternal(const FuClass * klass) = 0;
	void writeClass(const FuClass * klass, const FuProgram * program) override;
	void createHeaderFile(std::string_view headerExt);
	void createImplementationFile(const FuProgram * program, std::string_view headerExt);
public:
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitReturn(const FuReturn * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
private:
	void writeCIncludes();
	static std::string changeExtension(std::string_view path, std::string_view ext);
	static std::string getFilenameWithoutExtension(std::string_view path);
};

class GenC : public GenCCpp
{
public:
	GenC() = default;
	virtual ~GenC() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	const FuClass * currentClass;
	std::string_view getTargetName() const override;
	void writeSelfDoc(const FuMethod * method) override;
	void writeReturnDoc(const FuMethod * method) override;
	void writeThrowsDoc(const FuThrowsDeclaration * decl) override;
	void includeStdInt() override;
	void includeStdDef() override;
	void includeAssert() override;
	void includeMath() override;
	virtual void includeStdBool();
	virtual void writePrintfLongPrefix();
	void writePrintfWidth(const FuInterpolatedPart * part) override;
	virtual void writeInterpolatedStringArgBase(const FuExpr * expr);
	void startTemporaryVar(const FuType * type) override;
	void writeInterpolatedStringArg(const FuExpr * expr) override;
	virtual void writeCamelCaseNotKeyword(std::string_view name);
	void writeName(const FuSymbol * symbol) override;
	void writeLocalName(const FuSymbol * symbol, FuPriority parent) override;
	virtual void writeStringPtrType();
	virtual void writeClassType(const FuClassType * klass, bool space);
	void writeType(const FuType * type, bool promote) override;
	void writeTypeAndName(const FuNamedValue * value) override;
	void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) override;
	void writeArrayStorageInit(const FuArrayStorageType * array, const FuExpr * value) override;
	void writeNew(const FuReadWriteClassType * klass, FuPriority parent) override;
	void writeStorageInit(const FuNamedValue * def) override;
	void writeVarInit(const FuNamedValue * def) override;
	void writeOwningTemporary(const FuExpr * expr) override;
	void writeTemporariesNotSubstring(const FuExpr * expr) override;
	void writeArgTemporary(const FuMethod * method, const FuVar * param, const FuExpr * arg) override;
	void cleanupTemporary(int i, const FuExpr * temp) override;
	void writeVar(const FuNamedValue * def) override;
	void writeAssign(const FuBinaryExpr * expr, FuPriority parent) override;
	bool hasInitCode(const FuNamedValue * def) const override;
	void writeInitCode(const FuNamedValue * def) override;
	void writeMemberOp(const FuExpr * left, const FuSymbolReference * symbol) override;
	void writeArrayPtr(const FuExpr * expr, FuPriority parent) override;
	void writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent) override;
	virtual void writeSubstringEqual(const FuCallExpr * call, std::string_view literal, FuPriority parent, bool not_);
	virtual void writeEqualStringInternal(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_);
	void writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeArrayFill(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writePrintfNotInterpolated(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine);
	void writeCCall(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeStringSubstringStart(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent);
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	void cleanupBlock(const FuBlock * statement) override;
	void startIf(const FuExpr * expr) override;
	void writeSwitchCaseBody(const std::vector<std::shared_ptr<FuStatement>> * statements) override;
	void writeStatements(const std::vector<std::shared_ptr<FuStatement>> * statements) override;
	void writeEnum(const FuEnum * enu) override;
	void writeTypedefs(const FuProgram * program, bool pub);
	virtual std::string_view getConst(const FuArrayStorageType * array) const;
	void writeConst(const FuConst * konst) override;
	void writeField(const FuField * field) override;
	bool needsConstructor(const FuClass * klass) const override;
	void writeSignatures(const FuClass * klass, bool pub);
	void writeClassInternal(const FuClass * klass) override;
	void writeConstructor(const FuClass * klass);
	void writeDestructor(const FuClass * klass);
	void writeMethod(const FuMethod * method) override;
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
public:
	void visitLiteralNull() override;
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitCallExpr(const FuCallExpr * expr, FuPriority parent) override;
	void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void visitLambdaExpr(const FuLambdaExpr * expr) override;
	void visitBlock(const FuBlock * statement) override;
	void visitBreak(const FuBreak * statement) override;
	void visitContinue(const FuContinue * statement) override;
	void visitExpr(const FuExpr * statement) override;
	void visitForeach(const FuForeach * statement) override;
	void visitLock(const FuLock * statement) override;
	void visitReturn(const FuReturn * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
	void visitThrow(const FuThrow * statement) override;
private:
	std::set<FuId> intFunctions;
	std::set<FuId> longFunctions;
	bool intTryParse;
	bool longTryParse;
	bool doubleTryParse;
	bool stringAssign;
	bool stringSubstring;
	bool stringAppend;
	bool stringIndexOf;
	bool stringLastIndexOf;
	bool stringEndsWith;
	bool stringReplace;
	bool stringFormat;
	bool matchFind;
	bool matchPos;
	bool ptrConstruct;
	bool sharedMake;
	bool sharedAddRef;
	bool sharedRelease;
	bool sharedAssign;
	std::set<FuId> listFrees;
	bool treeCompareInteger;
	bool treeCompareString;
	std::set<FuId> compares;
	std::set<FuId> contains;
	std::vector<const FuVar *> varsToDestruct;
	bool conditionVarInScope;
	void writeStringPtrAdd(const FuCallExpr * call, bool cast);
	static bool isDictionaryClassStgIndexing(const FuExpr * expr);
	void writeUpcast(const FuClass * resultClass, const FuSymbol * klass);
	void writeClassPtr(const FuClass * resultClass, const FuExpr * expr, FuPriority parent);
	void writeForeachArrayIndexing(const FuForeach * forEach, const FuSymbol * symbol);
	void writeSelfForField(const FuSymbol * fieldClass);
	void writeMatchProperty(const FuSymbolReference * expr, int which);
	void writeGlib(std::string_view s);
	void writeArrayPrefix(const FuType * type);
	void startDefinition(const FuType * type, bool promote, bool space);
	void endDefinition(const FuType * type);
	void writeReturnType(const FuMethod * method);
	void writeDynamicArrayCast(const FuType * elementType);
	void writeXstructorPtr(bool need, const FuClass * klass, std::string_view name);
	static bool isHeapAllocated(const FuType * type);
	static bool needToDestructType(const FuType * type);
	static bool needToDestruct(const FuSymbol * symbol);
	static bool needsDestructor(const FuClass * klass);
	void writeXstructorPtrs(const FuClass * klass);
	void writeListFreeName(FuId id);
	void addListFree(FuId id);
	void writeListFree(const FuClassType * elementType);
	static const FuExpr * getStringSubstringLength(const FuCallExpr * call);
	void writeStringStorageValue(const FuExpr * expr);
	static bool isUnique(const FuDynamicPtrType * dynamic);
	bool writeDestructMethodName(const FuClassType * klass);
	void startDestructCall(const FuClassType * klass);
	static bool hasDictionaryDestroy(const FuType * type);
	void writeDictionaryDestroy(const FuType * type);
	void writeHashEqual(const FuType * keyType);
	void writeNewHashTable(const FuType * keyType, const FuType * valueType);
	void writeNewTree(const FuType * keyType, const FuType * valueType);
	static bool isCollection(const FuClass * klass);
	void writeAssignTemporary(const FuType * type, const FuExpr * expr);
	int writeCTemporary(const FuType * type, const FuExpr * expr);
	static bool needsOwningTemporary(const FuExpr * expr);
	bool hasTemporariesToDestruct() const;
	void writeGPointerCast(const FuType * type, const FuExpr * expr);
	void writeAddressOf(const FuExpr * expr);
	void writeGConstPointerCast(const FuExpr * expr);
	void writeGPointerToInt(const FuType * type);
	void writeUnstorage(const FuExpr * obj);
	void writeQueueGet(std::string_view function, const FuExpr * obj, FuPriority parent);
	void startDictionaryInsert(const FuExpr * dict, const FuExpr * key);
	static const FuMethod * getThrowingMethod(const FuExpr * expr);
	static bool hasListDestroy(const FuType * type);
	FuPriority startForwardThrow(const FuMethod * throwingMethod);
	void writeDestruct(const FuSymbol * symbol);
	void writeDestructAll(const FuVar * exceptVar = nullptr);
	void writeRangeThrowReturnValue(const FuRangeType * range);
	void writeThrowReturnValue(const FuType * type, bool include);
	void writeThrow();
	void endForwardThrow(const FuMethod * throwingMethod);
	void writeMemberAccess(const FuType * leftType, const FuSymbol * symbolClass);
	void writeStringMethod(std::string_view name, const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeSizeofCompare(const FuType * elementType);
	void writeListAddInsert(const FuExpr * obj, bool insert, std::string_view function, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeDictionaryLookup(const FuExpr * obj, std::string_view function, const FuExpr * key);
	void writeArgsAndRightParenthesis(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeCRegexOptions(const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeTextWriterWrite(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine);
	void writeConsoleWrite(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine);
	static const FuClass * getVtblStructClass(const FuClass * klass);
	static const FuClass * getVtblPtrClass(const FuClass * klass);
	void writeTryParse(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args);
	void startArrayContains(const FuExpr * obj);
	void startArrayIndexing(const FuExpr * obj, const FuType * elementType);
	void writeMathFloating(std::string_view function, const std::vector<std::shared_ptr<FuExpr>> * args);
	bool writeMathClampMaxMin(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeDictionaryIndexing(std::string_view function, const FuBinaryExpr * expr, FuPriority parent);
	void writeDestructLoopOrSwitch(const FuCondCompletionStatement * loopOrSwitch);
	void trimVarsToDestruct(int i);
	void startForeachHashTable(const FuForeach * statement);
	void writeDictIterVar(const FuNamedValue * iter, std::string_view value);
	bool tryWriteCallAndReturn(const std::vector<std::shared_ptr<FuStatement>> * statements, int lastCallIndex, const FuExpr * returnValue);
	void writeTypedef(const FuClass * klass);
	void writeInstanceParameters(const FuMethod * method);
	void writeSignature(const FuMethod * method);
	void writeVtblFields(const FuClass * klass);
	void writeVtblStruct(const FuClass * klass);
	static bool hasVtblValue(const FuClass * klass);
	void writeXstructorSignature(std::string_view name, const FuClass * klass);
	void writeVtbl(const FuClass * definingClass, const FuClass * declaringClass);
	void writeDestructFields(const FuSymbol * symbol);
	void writeNewDelete(const FuClass * klass, bool define);
	static bool canThrow(const FuType * type);
	void writeIntMaxMin(std::string_view klassName, std::string_view method, std::string_view type, int op);
	void writeIntLibrary(std::string_view klassName, std::string_view type, const std::set<FuId> * methods);
	void writeTryParseLibrary(std::string_view signature, std::string_view call);
	void writeLibrary();
};

class GenCl : public GenC
{
public:
	GenCl() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void includeStdBool() override;
	void includeMath() override;
	void writeNumericType(FuId id) override;
	void writeStringPtrType() override;
	void writeClassType(const FuClassType * klass, bool space) override;
	void writePrintfLongPrefix() override;
	void writeInterpolatedStringArgBase(const FuExpr * expr) override;
	void writeCamelCaseNotKeyword(std::string_view name) override;
	std::string_view getConst(const FuArrayStorageType * array) const override;
	void writeSubstringEqual(const FuCallExpr * call, std::string_view literal, FuPriority parent, bool not_) override;
	void writeEqualStringInternal(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeAssert(const FuAssert * statement) override;
	void writeSwitchCaseBody(const std::vector<std::shared_ptr<FuStatement>> * statements) override;
public:
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
private:
	bool stringLength;
	bool stringEquals;
	bool stringStartsWith;
	bool bytesEqualsString;
	void writeConsoleWrite(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine);
	void writeLibrary();
};

class GenCpp : public GenCCpp
{
public:
	GenCpp() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void includeStdInt() override;
	void includeStdDef() override;
	void includeAssert() override;
	void includeMath() override;
	void writeInterpolatedStringArg(const FuExpr * expr) override;
	void writeName(const FuSymbol * symbol) override;
	void writeLocalName(const FuSymbol * symbol, FuPriority parent) override;
	void writeType(const FuType * type, bool promote) override;
	void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) override;
	void writeNew(const FuReadWriteClassType * klass, FuPriority parent) override;
	void writeStorageInit(const FuNamedValue * def) override;
	void writeVarInit(const FuNamedValue * def) override;
	void writeStaticCast(const FuType * type, const FuExpr * expr) override;
	void writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_) override;
	void writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void writeMemberOp(const FuExpr * left, const FuSymbolReference * symbol) override;
	void writeEnumAsInt(const FuExpr * expr, FuPriority parent) override;
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	void writeArrayPtr(const FuExpr * expr, FuPriority parent) override;
	void writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent) override;
	void writeSelectValues(const FuType * type, const FuSelectExpr * expr) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeUnreachable(const FuAssert * statement) override;
	void writeConst(const FuConst * konst) override;
	bool embedIfWhileIsVar(const FuExpr * expr, bool write) override;
	void writeStronglyCoerced(const FuType * type, const FuExpr * expr) override;
	void writeSwitchCaseCond(const FuSwitch * statement, const FuExpr * value, FuPriority parent) override;
	void writeSwitchCaseBody(const std::vector<std::shared_ptr<FuStatement>> * statements) override;
	void writeException() override;
	void writeEnum(const FuEnum * enu) override;
	void writeField(const FuField * field) override;
	void writeClassInternal(const FuClass * klass) override;
	void writeMethod(const FuMethod * method) override;
public:
	void visitLiteralNull() override;
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void visitLambdaExpr(const FuLambdaExpr * expr) override;
	void visitForeach(const FuForeach * statement) override;
	void visitLock(const FuLock * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
	void visitThrow(const FuThrow * statement) override;
private:
	bool usingStringViewLiterals;
	bool hasEnumFlags;
	bool numberTryParse;
	bool stringReplace;
	bool stringToLower;
	bool stringToUpper;
	void startMethodCall(const FuExpr * obj);
	void writeCamelCaseNotKeyword(std::string_view name);
	void writeSharedUnique(std::string_view prefix, bool unique, std::string_view suffix);
	void writeCollectionType(const FuClassType * klass);
	void writeClassType(const FuClassType * klass);
	void writeNewUniqueArray(bool unique, const FuType * elementType, const FuExpr * lengthExpr);
	void writeNewUnique(bool unique, const FuReadWriteClassType * klass);
	static bool isSharedPtr(const FuExpr * expr);
	static bool needStringPtrData(const FuExpr * expr);
	static bool isClassPtr(const FuType * type);
	static bool isCppPtr(const FuExpr * expr);
	void writeCollectionObject(const FuExpr * obj, FuPriority priority);
	void writePop(const FuExpr * obj, FuPriority parent, int p, std::string_view front);
	void writeBeginEnd(const FuExpr * obj);
	void writePtrRange(const FuExpr * obj, const FuExpr * index, const FuExpr * count);
	void writeNotRawStringLiteral(const FuExpr * obj, FuPriority priority);
	void writeStringMethod(const FuExpr * obj, std::string_view name, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeAllAnyContains(std::string_view function, const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeCollectionMethod(const FuExpr * obj, std::string_view name, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeRegex(const std::vector<std::shared_ptr<FuExpr>> * args, int argIndex);
	void writeWrite(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine);
	void writeRegexArgument(const FuExpr * expr);
	void writeMatchProperty(const FuSymbolReference * expr, std::string_view name);
	void writeGtRawPtr(const FuExpr * expr);
	void writeIsVar(const FuExpr * expr, const FuVar * def, FuPriority parent);
	static bool hasLambdaCapture(const FuExpr * expr, const FuLambdaExpr * lambda);
	static bool isIsVar(const FuExpr * expr);
	bool hasVariables(const FuStatement * statement) const;
	void openNamespace();
	void closeNamespace();
	static FuVisibility getConstructorVisibility(const FuClass * klass);
	static bool hasMembersOfVisibility(const FuClass * klass, FuVisibility visibility);
	void writeParametersAndConst(const FuMethod * method, bool defaultArguments);
	void writeDeclarations(const FuClass * klass, FuVisibility visibility, std::string_view visibilityKeyword);
	void writeConstructor(const FuClass * klass);
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources, bool define);
};

class GenCs : public GenTyped
{
public:
	GenCs() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void startDocLine() override;
	void writeDocPara(const FuDocPara * para, bool many) override;
	void writeDocList(const FuDocList * list) override;
	void writeDoc(const FuCodeDoc * doc) override;
	void writeName(const FuSymbol * symbol) override;
	int getLiteralChars() const override;
	void writeType(const FuType * type, bool promote) override;
	void writeNewWithFields(const FuReadWriteClassType * type, const FuAggregateInitializer * init) override;
	void writeCoercedLiteral(const FuType * type, const FuExpr * expr) override;
	bool isPromoted(const FuExpr * expr) const override;
	void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) override;
	void writeNew(const FuReadWriteClassType * klass, FuPriority parent) override;
	bool hasInitCode(const FuNamedValue * def) const override;
	void writeInitCode(const FuNamedValue * def) override;
	void writeResource(std::string_view name, int length) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeArrayLength(const FuExpr * expr, FuPriority parent) override;
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void writeAssign(const FuBinaryExpr * expr, FuPriority parent) override;
	void defineObjectLiteralTemporary(const FuUnaryExpr * expr) override;
	void defineIsVar(const FuBinaryExpr * binary) override;
	void writeAssert(const FuAssert * statement) override;
	void writeException() override;
	void writeEnum(const FuEnum * enu) override;
	void writeRegexOptionsEnum(const FuProgram * program) override;
	void writeConst(const FuConst * konst) override;
	void writeField(const FuField * field) override;
	void writeParameterDoc(const FuVar * param, bool first) override;
	void writeThrowsDoc(const FuThrowsDeclaration * decl) override;
	bool isShortMethod(const FuMethod * method) const override;
	void writeMethod(const FuMethod * method) override;
	void writeClass(const FuClass * klass, const FuProgram * program) override;
public:
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void visitLambdaExpr(const FuLambdaExpr * expr) override;
	void visitForeach(const FuForeach * statement) override;
	void visitLock(const FuLock * statement) override;
private:
	void writeVisibility(FuVisibility visibility);
	void writeCallType(FuCallType callType, std::string_view sealedString);
	void writeElementType(const FuType * elementType);
	void writeJsonElementIs(const FuExpr * obj, std::string_view name, FuPriority parent);
	void writeOrderedDictionaryIndexing(const FuBinaryExpr * expr);
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
};

class GenD : public GenCCppD
{
public:
	GenD() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void startDocLine() override;
	void writeDocPara(const FuDocPara * para, bool many) override;
	void writeParameterDoc(const FuVar * param, bool first) override;
	void writeThrowsDoc(const FuThrowsDeclaration * decl) override;
	void writeDocList(const FuDocList * list) override;
	void writeDoc(const FuCodeDoc * doc) override;
	void writeName(const FuSymbol * symbol) override;
	int getLiteralChars() const override;
	void writeType(const FuType * type, bool promote) override;
	void writeTypeAndName(const FuNamedValue * value) override;
	void writeStaticCast(const FuType * type, const FuExpr * expr) override;
	void writeStorageInit(const FuNamedValue * def) override;
	void writeVarInit(const FuNamedValue * def) override;
	bool hasInitCode(const FuNamedValue * def) const override;
	void writeInitField(const FuField * field) override;
	void writeInitCode(const FuNamedValue * def) override;
	void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) override;
	void writeNew(const FuReadWriteClassType * klass, FuPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_) override;
	void writeAssign(const FuBinaryExpr * expr, FuPriority parent) override;
	void writeAssert(const FuAssert * statement) override;
	void writeSwitchCaseTypeVar(const FuExpr * value) override;
	void writeSwitchCaseCond(const FuSwitch * statement, const FuExpr * value, FuPriority parent) override;
	void writeEnum(const FuEnum * enu) override;
	void writeConst(const FuConst * konst) override;
	void writeField(const FuField * field) override;
	bool isShortMethod(const FuMethod * method) const override;
	void writeMethod(const FuMethod * method) override;
	void writeClass(const FuClass * klass, const FuProgram * program) override;
	void writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent) override;
public:
	void visitAggregateInitializer(const FuAggregateInitializer * expr) override;
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void visitLambdaExpr(const FuLambdaExpr * expr) override;
	void visitForeach(const FuForeach * statement) override;
	void visitLock(const FuLock * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
private:
	bool hasListInsert;
	bool hasListRemoveAt;
	bool hasQueueDequeue;
	bool hasStackPop;
	bool hasSortedDictionaryInsert;
	bool hasSortedDictionaryFind;
	void writeVisibility(FuVisibility visibility);
	void writeCallType(FuCallType callType, std::string_view sealedString);
	static bool isCreateWithNew(const FuType * type);
	static bool isTransitiveConst(const FuClassType * array);
	static bool isJsonElementList(const FuClassType * list);
	static bool isStructPtr(const FuType * type);
	void writeElementType(const FuType * type);
	void writeStaticInitializer(const FuType * type);
	void writeClassReference(const FuExpr * expr, FuPriority priority = FuPriority::primary);
	void writeWrite(const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine);
	void writeSlice(const FuExpr * obj, const FuExpr * offset, const FuExpr * length);
	void writeInsertedArg(const FuType * type, const std::vector<std::shared_ptr<FuExpr>> * args, int index = 0);
	void writeJsonElementIs(const FuExpr * obj, std::string_view name, FuPriority parent);
	static bool isIsComparable(const FuExpr * expr);
	void writeIsVar(const FuExpr * left, const FuExpr * right, FuPriority parent);
	static bool isLong(const FuSymbolReference * expr);
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
	void writeMain(const FuMethod * main);
};

class GenJava : public GenTyped
{
public:
	GenJava() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	int getLiteralChars() const override;
	void writePrintfWidth(const FuInterpolatedPart * part) override;
	void writeName(const FuSymbol * symbol) override;
	FuId getTypeId(const FuType * type, bool promote) const override;
	void writeType(const FuType * type, bool promote) override;
	void writeNew(const FuReadWriteClassType * klass, FuPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	void writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_) override;
	void writeCoercedLiteral(const FuType * type, const FuExpr * expr) override;
	void writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent) override;
	void writeRel(const FuBinaryExpr * expr, FuPriority parent, std::string_view op) override;
	void writeAnd(const FuBinaryExpr * expr, FuPriority parent) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeCharAt(const FuBinaryExpr * expr) override;
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	bool isPromoted(const FuExpr * expr) const override;
	void writeAssignRight(const FuBinaryExpr * expr) override;
	void writeAssign(const FuBinaryExpr * expr, FuPriority parent) override;
	std::string_view getIsOperator() const override;
	void writeVar(const FuNamedValue * def) override;
	bool hasInitCode(const FuNamedValue * def) const override;
	void writeInitCode(const FuNamedValue * def) override;
	void defineIsVar(const FuBinaryExpr * binary) override;
	void writeAssert(const FuAssert * statement) override;
	void startBreakGoto() override;
	void writeSwitchValue(const FuExpr * expr) override;
	void writeSwitchCaseValue(const FuSwitch * statement, const FuExpr * value) override;
	void writeEnum(const FuEnum * enu) override;
	void writeConst(const FuConst * konst) override;
	void writeField(const FuField * field) override;
	void writeMethod(const FuMethod * method) override;
	void writeClass(const FuClass * klass, const FuProgram * program) override;
public:
	void visitLiteralLong(int64_t value) override;
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
	void visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent) override;
	void visitPostfixExpr(const FuPostfixExpr * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitLambdaExpr(const FuLambdaExpr * expr) override;
	void visitForeach(const FuForeach * statement) override;
	void visitIf(const FuIf * statement) override;
	void visitLock(const FuLock * statement) override;
	void visitReturn(const FuReturn * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
	void visitEnumValue(const FuConst * konst, const FuConst * previous) override;
private:
	void writeToString(const FuExpr * expr, FuPriority parent);
	void writeCamelCaseNotKeyword(std::string_view name);
	void writeVisibility(FuVisibility visibility);
	static bool isJavaEnum(const FuEnum * enu);
	void writeCollectionType(std::string_view name, const FuType * elementType);
	void writeDictType(std::string_view name, const FuClassType * dict);
	void writeJavaType(const FuType * type, bool promote, bool needClass);
	static bool isUnsignedByte(const FuType * type);
	static bool isUnsignedByteIndexing(const FuExpr * expr);
	void writeIndexingInternal(const FuBinaryExpr * expr);
	void writeSByteLiteral(const FuLiteralLong * literal);
	void writeArrayBinarySearchFill(const FuExpr * obj, std::string_view method, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeWrite(const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, bool newLine);
	void writeCompileRegex(const std::vector<std::shared_ptr<FuExpr>> * args, int argIndex);
	static bool isTryParse(FuId id);
	void createJavaFile(std::string_view className);
	void writeSignature(const FuMethod * method, int paramCount);
	void writeOverloads(const FuMethod * method, int paramCount);
	void writeResources();
};

class GenJsNoModule : public GenBase
{
public:
	GenJsNoModule() = default;
	virtual ~GenJsNoModule() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void writeName(const FuSymbol * symbol) override;
	void writeTypeAndName(const FuNamedValue * value) override;
	void writeArrayElementType(const FuType * type);
	void writeNew(const FuReadWriteClassType * klass, FuPriority parent) override;
	void writeNewWithFields(const FuReadWriteClassType * type, const FuAggregateInitializer * init) override;
	void writeVar(const FuNamedValue * def) override;
	void writeLocalName(const FuSymbol * symbol, FuPriority parent) override;
	void writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent) override;
	void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) override;
	bool hasInitCode(const FuNamedValue * def) const override;
	void writeInitCode(const FuNamedValue * def) override;
	void writeResource(std::string_view name, int length) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeCharAt(const FuBinaryExpr * expr) override;
	void writeBinaryOperand(const FuExpr * expr, FuPriority parent, const FuBinaryExpr * binary) override;
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeAssign(const FuBinaryExpr * expr, FuPriority parent) override;
	void writeOpAssignRight(const FuBinaryExpr * expr) override;
	void writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	std::string_view getIsOperator() const override;
	virtual void writeBoolAndOr(const FuBinaryExpr * expr);
	void startTemporaryVar(const FuType * type) override;
	void defineObjectLiteralTemporary(const FuUnaryExpr * expr) override;
	virtual void writeAsType(const FuVar * def);
	void writeAssertCast(const FuBinaryExpr * expr) override;
	void writeAssert(const FuAssert * statement) override;
	void startBreakGoto() override;
	void writeSwitchCaseCond(const FuSwitch * statement, const FuExpr * value, FuPriority parent) override;
	void writeIfCaseBody(const std::vector<std::shared_ptr<FuStatement>> * body, bool doWhile, const FuSwitch * statement, const FuCase * kase) override;
	void writeException() override;
	virtual void startContainerType(const FuContainerType * container);
	void writeEnum(const FuEnum * enu) override;
	void writeConst(const FuConst * konst) override;
	void writeField(const FuField * field) override;
	void writeMethod(const FuMethod * method) override;
	void openJsClass(const FuClass * klass);
	void writeConstructor(const FuClass * klass);
	void writeClass(const FuClass * klass, const FuProgram * program) override;
	void writeLib(const FuProgram * program);
	virtual void writeUseStrict();
public:
	void visitAggregateInitializer(const FuAggregateInitializer * expr) override;
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void visitLambdaExpr(const FuLambdaExpr * expr) override;
	void visitForeach(const FuForeach * statement) override;
	void visitLock(const FuLock * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
	void visitEnumValue(const FuConst * konst, const FuConst * previous) override;
private:
	bool stringWriter = false;
	void writeCamelCaseNotKeyword(std::string_view name);
	void writeInterpolatedLiteral(std::string_view s);
	void writeSlice(const FuExpr * array, const FuExpr * offset, const FuExpr * length, FuPriority parent, std::string_view method);
	static bool isIdentifier(std::string_view s);
	void writeNewRegex(const std::vector<std::shared_ptr<FuExpr>> * args, int argIndex);
	void writeTypeofEquals(const FuExpr * obj, std::string_view name, FuPriority parent);
	static bool hasLong(const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeMathMaxMin(const FuMethod * method, std::string_view name, int op, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeBoolAndOrAssign(const FuBinaryExpr * expr, FuPriority parent);
	void writeIsVar(const FuExpr * expr, std::string_view name, const FuSymbol * klass, FuPriority parent);
	void writeVarCast(const FuVar * def, const FuExpr * value);
	void writeMain(const FuMethod * main);
};

class GenJs : public GenJsNoModule
{
public:
	GenJs() = default;
	virtual ~GenJs() = default;
protected:
	void startContainerType(const FuContainerType * container) override;
	void writeUseStrict() override;
};

class GenTs : public GenJs
{
public:
	GenTs() = default;
	const GenTs * withGenFullCode();
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void writeEnum(const FuEnum * enu) override;
	void writeTypeAndName(const FuNamedValue * value) override;
	void writeAsType(const FuVar * def) override;
	void writeBinaryOperand(const FuExpr * expr, FuPriority parent, const FuBinaryExpr * binary) override;
	void writeEqualOperand(const FuExpr * expr, const FuExpr * other) override;
	void writeBoolAndOr(const FuBinaryExpr * expr) override;
	void defineIsVar(const FuBinaryExpr * binary) override;
	void writeConst(const FuConst * konst) override;
	void writeField(const FuField * field) override;
	void writeMethod(const FuMethod * method) override;
	void writeClass(const FuClass * klass, const FuProgram * program) override;
public:
	void visitEnumValue(const FuConst * konst, const FuConst * previous) override;
private:
	const FuSystem * system;
	bool genFullCode = false;
	void writeType(const FuType * type, bool readOnly = false);
	void writeVisibility(FuVisibility visibility);
};

class GenPySwift : public GenBase
{
public:
	virtual ~GenPySwift() = default;
protected:
	GenPySwift() = default;
	void writeDocPara(const FuDocPara * para, bool many) override;
	virtual std::string_view getDocBullet() const = 0;
	void writeDocList(const FuDocList * list) override;
	void writeLocalName(const FuSymbol * symbol, FuPriority parent) override;
	virtual std::string_view getReferenceEqOp(bool not_) const = 0;
	void writeEqual(const FuExpr * left, const FuExpr * right, FuPriority parent, bool not_) override;
	virtual void writeExpr(const FuExpr * expr, FuPriority parent);
	void writeListAppend(const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args);
	virtual bool visitPreCall(const FuCallExpr * call);
	bool visitXcrement(const FuExpr * expr, bool postfix, bool write);
	void endStatement() override;
	virtual void openChild() = 0;
	virtual void closeChild() = 0;
	void writeChild(FuStatement * statement) override;
	virtual void writeContinueDoWhile(const FuExpr * cond);
	virtual bool needCondXcrement(const FuLoop * loop);
	virtual std::string_view getIfNot() const = 0;
	virtual void openWhile(const FuLoop * loop);
	virtual void writeForRange(const FuVar * iter, const FuBinaryExpr * cond, int64_t rangeStep) = 0;
	virtual void writeElseIf() = 0;
	virtual void writeResultVar() = 0;
public:
	void visitAggregateInitializer(const FuAggregateInitializer * expr) override;
	void visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent) override;
	void visitPostfixExpr(const FuPostfixExpr * expr, FuPriority parent) override;
	void visitExpr(const FuExpr * statement) override;
	void visitBlock(const FuBlock * statement) override;
	void visitContinue(const FuContinue * statement) override;
	void visitDoWhile(const FuDoWhile * statement) override;
	void visitFor(const FuFor * statement) override;
	void visitIf(const FuIf * statement) override;
	void visitReturn(const FuReturn * statement) override;
	void visitWhile(const FuWhile * statement) override;
private:
	static bool isPtr(const FuExpr * expr);
	bool openCond(std::string_view statement, const FuExpr * cond, FuPriority parent);
	void endBody(const FuLoop * loop);
	void openWhileTrue();
	void closeWhile(const FuLoop * loop);
};

class GenSwift : public GenPySwift
{
public:
	GenSwift() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void startDocLine() override;
	void writeDocCode(std::string_view s) override;
	std::string_view getDocBullet() const override;
	void writeDoc(const FuCodeDoc * doc) override;
	void writeName(const FuSymbol * symbol) override;
	void writeLocalName(const FuSymbol * symbol, FuPriority parent) override;
	void writeMemberOp(const FuExpr * left, const FuSymbolReference * symbol) override;
	void writeTypeAndName(const FuNamedValue * value) override;
	void writeCoercedInternal(const FuType * type, const FuExpr * expr, FuPriority parent) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeArrayLength(const FuExpr * expr, FuPriority parent) override;
	void writeCharAt(const FuBinaryExpr * expr) override;
	std::string_view getReferenceEqOp(bool not_) const override;
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeNewArrayStorage(const FuArrayStorageType * array) override;
	void writeNew(const FuReadWriteClassType * klass, FuPriority parent) override;
	void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) override;
	void writeIndexingExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void writeBinaryOperand(const FuExpr * expr, FuPriority parent, const FuBinaryExpr * binary) override;
	void writeResource(std::string_view name, int length) override;
	void writeExpr(const FuExpr * expr, FuPriority parent) override;
	void writeCoercedExpr(const FuType * type, const FuExpr * expr) override;
	void startTemporaryVar(const FuType * type) override;
	void openChild() override;
	void closeChild() override;
	void writeVar(const FuNamedValue * def) override;
	void writeStatements(const std::vector<std::shared_ptr<FuStatement>> * statements) override;
	void writeAssertCast(const FuBinaryExpr * expr) override;
	void writeAssert(const FuAssert * statement) override;
	bool needCondXcrement(const FuLoop * loop) override;
	std::string_view getIfNot() const override;
	void writeContinueDoWhile(const FuExpr * cond) override;
	void writeElseIf() override;
	void openWhile(const FuLoop * loop) override;
	void writeForRange(const FuVar * indVar, const FuBinaryExpr * cond, int64_t rangeStep) override;
	void writeResultVar() override;
	void writeException() override;
	void writeParameter(const FuVar * param) override;
	void writeEnum(const FuEnum * enu) override;
	void writeConst(const FuConst * konst) override;
	void writeField(const FuField * field) override;
	void writeParameterDoc(const FuVar * param, bool first) override;
	void writeThrowsDoc(const FuThrowsDeclaration * decl) override;
	void writeMethod(const FuMethod * method) override;
	void writeClass(const FuClass * klass, const FuProgram * program) override;
public:
	void visitLiteralNull() override;
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent) override;
	void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void visitExpr(const FuExpr * statement) override;
	void visitLambdaExpr(const FuLambdaExpr * expr) override;
	void visitBreak(const FuBreak * statement) override;
	void visitDoWhile(const FuDoWhile * statement) override;
	void visitForeach(const FuForeach * statement) override;
	void visitLock(const FuLock * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
	void visitThrow(const FuThrow * statement) override;
	void visitEnumValue(const FuConst * konst, const FuConst * previous) override;
private:
	const FuSystem * system;
	bool throwException;
	bool arrayRef;
	bool stringCharAt;
	bool stringIndexOf;
	bool stringSubstring;
	std::vector<std::unordered_set<std::string_view>> varsAtIndent;
	std::vector<bool> varBytesAtIndent;
	void writeCamelCaseNotKeyword(std::string_view name);
	void openIndexing(const FuExpr * collection);
	static bool isArrayRef(const FuArrayStorageType * array);
	void writeArrayRef(const FuType * elementType);
	void writeClassName(const FuClassType * klass);
	void writeType(const FuType * type);
	void writeUnwrapped(const FuExpr * expr, FuPriority parent, bool substringOk);
	void writeStringContains(const FuExpr * obj, std::string_view name, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeRange(const FuExpr * startIndex, const FuExpr * length);
	bool addVar(std::string_view name);
	void writeJsonElementIs(const FuExpr * obj, std::string_view name, FuPriority parent);
	void writeDefaultValue(const FuType * type);
	void writeEnumFlagsAnd(const FuExpr * left, std::string_view method, std::string_view notMethod, const FuExpr * right);
	const FuExpr * writeAssignNested(const FuBinaryExpr * expr);
	void writeSwiftAssign(const FuBinaryExpr * expr, const FuExpr * right);
	static bool throws(const FuExpr * expr);
	void initVarsAtIndent();
	static bool needsVarBytes(const std::vector<std::shared_ptr<FuStatement>> * statements);
	void writeSwiftCaseValue(const FuSwitch * statement, const FuExpr * value);
	void writeSwiftSwitchCaseBody(const FuSwitch * statement, const std::vector<std::shared_ptr<FuStatement>> * body);
	void writeReadOnlyParameter(const FuVar * param);
	void writeVisibility(FuVisibility visibility);
	void writeLibrary();
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
	void writeMain(const FuMethod * main);
};

class GenPy : public GenPySwift
{
public:
	GenPy() = default;
	void writeProgram(const FuProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void writeBanner() override;
	void startDocLine() override;
	void writeDocCode(std::string_view s) override;
	std::string_view getDocBullet() const override;
	void writeDoc(const FuCodeDoc * doc) override;
	void writeParameterDoc(const FuVar * param, bool first) override;
	void writeThrowsDoc(const FuThrowsDeclaration * decl) override;
	void writeName(const FuSymbol * symbol) override;
	void writeTypeAndName(const FuNamedValue * value) override;
	void writeLocalName(const FuSymbol * symbol, FuPriority parent) override;
	std::string_view getReferenceEqOp(bool not_) const override;
	void writeCharAt(const FuBinaryExpr * expr) override;
	void writeStringLength(const FuExpr * expr) override;
	void writeArrayLength(const FuExpr * expr, FuPriority parent) override;
	void writeCoercedSelect(const FuType * type, const FuSelectExpr * expr, FuPriority parent) override;
	void writeNewArray(const FuType * elementType, const FuExpr * lengthExpr, FuPriority parent) override;
	void writeArrayStorageInit(const FuArrayStorageType * array, const FuExpr * value) override;
	void writeNew(const FuReadWriteClassType * klass, FuPriority parent) override;
	void writeCallExpr(const FuExpr * obj, const FuMethod * method, const std::vector<std::shared_ptr<FuExpr>> * args, FuPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	bool visitPreCall(const FuCallExpr * call) override;
	void startTemporaryVar(const FuType * type) override;
	bool hasInitCode(const FuNamedValue * def) const override;
	void startLine() override;
	void openChild() override;
	void closeChild() override;
	void writeAssertCast(const FuBinaryExpr * expr) override;
	void writeAssert(const FuAssert * statement) override;
	std::string_view getIfNot() const override;
	void writeForRange(const FuVar * indVar, const FuBinaryExpr * cond, int64_t rangeStep) override;
	void writeElseIf() override;
	void writeResultVar() override;
	void writeEnum(const FuEnum * enu) override;
	void writeConst(const FuConst * konst) override;
	void writeField(const FuField * field) override;
	void writeMethod(const FuMethod * method) override;
	void writeInitField(const FuField * field) override;
	void writeClass(const FuClass * klass, const FuProgram * program) override;
public:
	void visitLiteralNull() override;
	void visitLiteralFalse() override;
	void visitLiteralTrue() override;
	void visitAggregateInitializer(const FuAggregateInitializer * expr) override;
	void visitInterpolatedString(const FuInterpolatedString * expr, FuPriority parent) override;
	void visitPrefixExpr(const FuPrefixExpr * expr, FuPriority parent) override;
	void visitSymbolReference(const FuSymbolReference * expr, FuPriority parent) override;
	void visitBinaryExpr(const FuBinaryExpr * expr, FuPriority parent) override;
	void visitExpr(const FuExpr * statement) override;
	void visitLambdaExpr(const FuLambdaExpr * expr) override;
	void visitBreak(const FuBreak * statement) override;
	void visitForeach(const FuForeach * statement) override;
	void visitLock(const FuLock * statement) override;
	void visitSwitch(const FuSwitch * statement) override;
	void visitThrow(const FuThrow * statement) override;
	void visitEnumValue(const FuConst * konst, const FuConst * previous) override;
private:
	std::unordered_set<const FuContainerType *> writtenTypes;
	bool childPass;
	bool switchBreak;
	void startDoc(const FuCodeDoc * doc);
	void writePyDoc(const FuMethod * method);
	void writeNameNotKeyword(std::string_view name);
	void writePyClassAnnotation(const FuContainerType * type);
	void writeCollectionTypeAnnotation(std::string_view name, const FuClassType * klass);
	void writeTypeAnnotation(const FuType * type, bool nullable = false);
	static int getArrayCode(const FuType * type);
	void writeDefaultValue(const FuType * type);
	void writePyNewArray(const FuType * elementType, const FuExpr * value, const FuExpr * lengthExpr);
	void writeContains(const FuExpr * haystack, const FuExpr * needle);
	void writeSlice(const FuExpr * startIndex, const FuExpr * length);
	void writeAssignSorted(const FuExpr * obj, std::string_view byteArray);
	void writeAllAny(std::string_view function, const FuExpr * obj, const std::vector<std::shared_ptr<FuExpr>> * args);
	void writePyRegexOptions(const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeRegexSearch(const std::vector<std::shared_ptr<FuExpr>> * args);
	void writeJsonElementIs(const FuExpr * obj, std::string_view name);
	void writeInclusiveLimit(const FuExpr * limit, int increment, std::string_view incrementString);
	void writePyCaseValue(const FuExpr * value);
	void writePyCaseBody(const FuSwitch * statement, const std::vector<std::shared_ptr<FuStatement>> * body);
	void writePyClass(const FuContainerType * type);
	bool inheritsConstructor(const FuClass * klass) const;
	void writeResourceByte(int b);
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
	void writeMain(const FuMethod * main);
};
