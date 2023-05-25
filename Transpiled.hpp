// Generated automatically with "cito". Do not edit.
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
#define CI_ENUM_FLAG_OPERATORS(T) \
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
CI_ENUM_FLAG_OPERATORS(RegexOptions)

enum class CiToken
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
	preIf,
	preElIf,
	preElse,
	preEndIf
};

enum class CiPreState
{
	notYet,
	already,
	alreadyElse
};
class CiLexer;

enum class CiVisibility
{
	private_,
	internal,
	protected_,
	public_,
	numericElementType,
	finalValueType
};

enum class CiCallType
{
	static_,
	normal,
	abstract,
	virtual_,
	override_,
	sealed
};

enum class CiPriority
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

enum class CiId
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
	longType,
	floatType,
	doubleType,
	floatIntType,
	boolType,
	stringClass,
	stringPtrType,
	stringStorageType,
	arrayPtrClass,
	arrayStorageClass,
	listClass,
	queueClass,
	stackClass,
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
	lockClass,
	stringLength,
	arrayLength,
	consoleError,
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
	arrayBinarySearchAll,
	arrayBinarySearchPart,
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
	mathMethod,
	mathAbs,
	mathCeiling,
	mathClamp,
	mathFusedMultiplyAdd,
	mathIsFinite,
	mathIsInfinity,
	mathIsNaN,
	mathLog2,
	mathMaxInt,
	mathMaxDouble,
	mathMinInt,
	mathMinDouble,
	mathRound,
	mathTruncate
};
class CiDocInline;
class CiDocText;
class CiDocCode;
class CiDocLine;
class CiDocBlock;
class CiDocPara;
class CiDocList;
class CiCodeDoc;
class CiVisitor;
class CiStatement;
class CiExpr;
class CiSymbol;
class CiScope;
class CiAggregateInitializer;
class CiLiteral;
class CiLiteralNull;
class CiLiteralFalse;
class CiLiteralTrue;
class CiLiteralLong;
class CiLiteralChar;
class CiLiteralDouble;
class CiLiteralString;
class CiInterpolatedPart;
class CiInterpolatedString;
class CiImplicitEnumValue;
class CiSymbolReference;
class CiUnaryExpr;
class CiPrefixExpr;
class CiPostfixExpr;
class CiBinaryExpr;
class CiSelectExpr;
class CiCallExpr;
class CiLambdaExpr;
class CiCondCompletionStatement;
class CiBlock;
class CiAssert;
class CiLoop;
class CiBreak;
class CiContinue;
class CiDoWhile;
class CiFor;
class CiForeach;
class CiIf;
class CiLock;
class CiNative;
class CiReturn;
class CiCase;
class CiSwitch;
class CiThrow;
class CiWhile;
class CiParameters;
class CiType;
class CiNumericType;
class CiIntegerType;
class CiRangeType;
class CiFloatingType;
class CiNamedValue;
class CiMember;
class CiVar;

enum class CiVisitStatus
{
	notYet,
	inProgress,
	done
};
class CiConst;
class CiField;
class CiProperty;
class CiStaticProperty;
class CiMethodBase;
class CiMethod;
class CiMethodGroup;
class CiContainerType;
class CiEnum;
class CiEnumFlags;
class CiClass;
class CiClassType;
class CiReadWriteClassType;
class CiStorageType;
class CiDynamicPtrType;
class CiArrayStorageType;
class CiStringType;
class CiStringStorageType;
class CiPrintableType;
class CiSystem;
class CiProgram;
class CiParser;
class CiConsoleParser;
class CiSema;
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

class CiLexer
{
public:
	virtual ~CiLexer() = default;
	void addPreSymbol(std::string_view symbol);
	static bool isLetterOrDigit(int c);
	static int getEscapedChar(int c);
	static std::string_view tokenToString(CiToken token);
protected:
	CiLexer() = default;
	uint8_t const * input;
	int charOffset;
	std::string filename;
	int line;
	int column;
	int tokenColumn;
	int lexemeOffset;
	CiToken currentToken;
	int64_t longValue;
	std::string stringValue;
	bool parsingTypeArg = false;
	void open(std::string_view filename, uint8_t const * input, int inputLength);
	virtual void reportError(std::string_view message) = 0;
	int peekChar() const;
	int readChar();
	CiToken readString(bool interpolated);
	std::string getLexeme() const;
	bool see(CiToken token) const;
	bool check(CiToken expected);
	CiToken nextToken();
	bool eat(CiToken token);
	bool expect(CiToken expected);
	void expectOrSkip(CiToken expected);
private:
	int inputLength;
	int nextOffset;
	int nextChar;
	std::unordered_set<std::string> preSymbols;
	bool atLineStart = true;
	bool lineMode = false;
	bool enableDocComments = true;
	std::stack<bool> preElseStack;
	int readByte();
	static constexpr int replacementChar = 65533;
	int readContinuationByte(int hi);
	void fillNextChar();
	bool eatChar(int c);
	void skipWhitespace();
	CiToken readIntegerLiteral(int bits);
	CiToken readFloatLiteral(bool needDigit);
	CiToken readNumberLiteral(int64_t i);
	int readCharLiteral();
	void readId(int c);
	CiToken readPreToken();
	void nextPreToken();
	bool eatPre(CiToken token);
	bool parsePrePrimary();
	bool parsePreEquality();
	bool parsePreAnd();
	bool parsePreOr();
	bool parsePreExpr();
	void expectEndOfLine(std::string_view directive);
	bool popPreElse(std::string_view directive);
	void skipUnmet(CiPreState state);
	CiToken readToken();
};

class CiDocInline
{
public:
	virtual ~CiDocInline() = default;
protected:
	CiDocInline() = default;
};

class CiDocText : public CiDocInline
{
public:
	CiDocText() = default;
public:
	std::string text;
};

class CiDocCode : public CiDocInline
{
public:
	CiDocCode() = default;
public:
	std::string text;
};

class CiDocLine : public CiDocInline
{
public:
	CiDocLine() = default;
};

class CiDocBlock
{
public:
	virtual ~CiDocBlock() = default;
protected:
	CiDocBlock() = default;
};

class CiDocPara : public CiDocBlock
{
public:
	CiDocPara() = default;
public:
	std::vector<std::shared_ptr<CiDocInline>> children;
};

class CiDocList : public CiDocBlock
{
public:
	CiDocList() = default;
public:
	std::vector<CiDocPara> items;
};

class CiCodeDoc
{
public:
	CiCodeDoc() = default;
public:
	CiDocPara summary;
	std::vector<std::shared_ptr<CiDocBlock>> details;
};

class CiVisitor
{
public:
	virtual ~CiVisitor() = default;
	virtual void visitConst(const CiConst * statement) = 0;
	virtual void visitExpr(const CiExpr * statement) = 0;
	virtual void visitBlock(const CiBlock * statement) = 0;
	virtual void visitAssert(const CiAssert * statement) = 0;
	virtual void visitBreak(const CiBreak * statement) = 0;
	virtual void visitContinue(const CiContinue * statement) = 0;
	virtual void visitDoWhile(const CiDoWhile * statement) = 0;
	virtual void visitFor(const CiFor * statement) = 0;
	virtual void visitForeach(const CiForeach * statement) = 0;
	virtual void visitIf(const CiIf * statement) = 0;
	virtual void visitLock(const CiLock * statement) = 0;
	virtual void visitNative(const CiNative * statement) = 0;
	virtual void visitReturn(const CiReturn * statement) = 0;
	virtual void visitSwitch(const CiSwitch * statement) = 0;
	virtual void visitThrow(const CiThrow * statement) = 0;
	virtual void visitWhile(const CiWhile * statement) = 0;
	virtual void visitEnumValue(const CiConst * konst, const CiConst * previous) = 0;
	virtual void visitLiteralNull() = 0;
	virtual void visitLiteralFalse() = 0;
	virtual void visitLiteralTrue() = 0;
	virtual void visitLiteralLong(int64_t value) = 0;
	virtual void visitLiteralChar(int value) = 0;
	virtual void visitLiteralDouble(double value) = 0;
	virtual void visitLiteralString(std::string_view value) = 0;
	virtual void visitAggregateInitializer(const CiAggregateInitializer * expr) = 0;
	virtual void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) = 0;
	virtual void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) = 0;
	virtual void visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent) = 0;
	virtual void visitPostfixExpr(const CiPostfixExpr * expr, CiPriority parent) = 0;
	virtual void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) = 0;
	virtual void visitSelectExpr(const CiSelectExpr * expr, CiPriority parent) = 0;
	virtual void visitCallExpr(const CiCallExpr * expr, CiPriority parent) = 0;
	virtual void visitLambdaExpr(const CiLambdaExpr * expr) = 0;
	virtual void visitVar(const CiVar * expr) = 0;
protected:
	CiVisitor() = default;
	virtual const CiContainerType * getCurrentContainer() const = 0;
	void visitOptionalStatement(const CiStatement * statement);
	void reportError(const CiStatement * statement, std::string_view message);
public:
	bool hasErrors = false;
};

class CiStatement
{
public:
	virtual ~CiStatement() = default;
	virtual bool completesNormally() const = 0;
	virtual void acceptStatement(CiVisitor * visitor) const = 0;
protected:
	CiStatement() = default;
public:
	int line;
};

class CiExpr : public CiStatement
{
public:
	virtual ~CiExpr() = default;
	bool completesNormally() const override;
	virtual std::string toString() const;
	virtual bool isIndexing() const;
	virtual bool isLiteralZero() const;
	virtual bool isConstEnum() const;
	virtual int intValue() const;
	virtual void accept(CiVisitor * visitor, CiPriority parent) const;
	void acceptStatement(CiVisitor * visitor) const override;
	virtual bool isReferenceTo(const CiSymbol * symbol) const;
protected:
	CiExpr() = default;
public:
	std::shared_ptr<CiType> type;
};

class CiSymbol : public CiExpr
{
public:
	virtual ~CiSymbol() = default;
	std::string toString() const override;
protected:
	CiSymbol() = default;
public:
	CiId id = CiId::none;
	std::string name;
	CiSymbol * next;
	CiScope * parent;
	std::shared_ptr<CiCodeDoc> documentation = nullptr;
};

class CiScope : public CiSymbol
{
public:
	CiScope() = default;
	virtual ~CiScope() = default;
	int count() const;
	CiVar * firstParameter() const;
	CiContainerType * getContainer();
	bool contains(const CiSymbol * symbol) const;
	std::shared_ptr<CiSymbol> tryLookup(std::string_view name, bool global) const;
	void add(std::shared_ptr<CiSymbol> symbol);
	bool encloses(const CiSymbol * symbol) const;
protected:
	std::unordered_map<std::string_view, std::shared_ptr<CiSymbol>> dict;
public:
	CiSymbol * first = nullptr;
private:
	CiSymbol * last;
};

class CiAggregateInitializer : public CiExpr
{
public:
	CiAggregateInitializer() = default;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
public:
	std::vector<std::shared_ptr<CiExpr>> items;
};

class CiLiteral : public CiExpr
{
public:
	virtual ~CiLiteral() = default;
	virtual bool isDefaultValue() const = 0;
	virtual std::string getLiteralString() const;
protected:
	CiLiteral() = default;
};

class CiLiteralNull : public CiLiteral
{
public:
	CiLiteralNull() = default;
	bool isDefaultValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	std::string toString() const override;
};

class CiLiteralFalse : public CiLiteral
{
public:
	CiLiteralFalse() = default;
	bool isDefaultValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	std::string toString() const override;
};

class CiLiteralTrue : public CiLiteral
{
public:
	CiLiteralTrue() = default;
	bool isDefaultValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	std::string toString() const override;
};

class CiLiteralLong : public CiLiteral
{
public:
	CiLiteralLong() = default;
	virtual ~CiLiteralLong() = default;
	bool isLiteralZero() const override;
	int intValue() const override;
	bool isDefaultValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	std::string getLiteralString() const override;
	std::string toString() const override;
public:
	int64_t value;
};

class CiLiteralChar : public CiLiteralLong
{
public:
	CiLiteralChar() = default;
	static std::shared_ptr<CiLiteralChar> new_(int value, int line);
	void accept(CiVisitor * visitor, CiPriority parent) const override;
};

class CiLiteralDouble : public CiLiteral
{
public:
	CiLiteralDouble() = default;
	bool isDefaultValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	std::string getLiteralString() const override;
	std::string toString() const override;
public:
	double value;
};

class CiLiteralString : public CiLiteral
{
public:
	CiLiteralString() = default;
	bool isDefaultValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	std::string getLiteralString() const override;
	std::string toString() const override;
	int getAsciiLength() const;
	int getAsciiAt(int i) const;
	int getOneAscii() const;
public:
	std::string value;
};

class CiInterpolatedPart
{
public:
	CiInterpolatedPart() = default;
public:
	std::string prefix;
	std::shared_ptr<CiExpr> argument;
	std::shared_ptr<CiExpr> widthExpr;
	int width;
	int format;
	int precision;
};

class CiInterpolatedString : public CiExpr
{
public:
	CiInterpolatedString() = default;
	void addPart(std::string_view prefix, std::shared_ptr<CiExpr> arg, std::shared_ptr<CiExpr> widthExpr = nullptr, int format = ' ', int precision = -1);
	void accept(CiVisitor * visitor, CiPriority parent) const override;
public:
	std::vector<CiInterpolatedPart> parts;
	std::string suffix;
};

class CiImplicitEnumValue : public CiExpr
{
public:
	CiImplicitEnumValue() = default;
	int intValue() const override;
public:
	int value;
};

class CiSymbolReference : public CiExpr
{
public:
	CiSymbolReference() = default;
	bool isConstEnum() const override;
	int intValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	bool isReferenceTo(const CiSymbol * symbol) const override;
	std::string toString() const override;
public:
	std::shared_ptr<CiExpr> left;
	std::string name;
	CiSymbol * symbol;
};

class CiUnaryExpr : public CiExpr
{
public:
	virtual ~CiUnaryExpr() = default;
protected:
	CiUnaryExpr() = default;
public:
	CiToken op;
	std::shared_ptr<CiExpr> inner;
};

class CiPrefixExpr : public CiUnaryExpr
{
public:
	CiPrefixExpr() = default;
	bool isConstEnum() const override;
	int intValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
};

class CiPostfixExpr : public CiUnaryExpr
{
public:
	CiPostfixExpr() = default;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
};

class CiBinaryExpr : public CiExpr
{
public:
	CiBinaryExpr() = default;
	bool isIndexing() const override;
	bool isConstEnum() const override;
	int intValue() const override;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	bool isAssign() const;
	std::string_view getOpString() const;
	std::string toString() const override;
public:
	std::shared_ptr<CiExpr> left;
	CiToken op;
	std::shared_ptr<CiExpr> right;
};

class CiSelectExpr : public CiExpr
{
public:
	CiSelectExpr() = default;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	std::string toString() const override;
public:
	std::shared_ptr<CiExpr> cond;
	std::shared_ptr<CiExpr> onTrue;
	std::shared_ptr<CiExpr> onFalse;
};

class CiCallExpr : public CiExpr
{
public:
	CiCallExpr() = default;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
public:
	std::shared_ptr<CiSymbolReference> method;
	std::vector<std::shared_ptr<CiExpr>> arguments;
};

class CiLambdaExpr : public CiScope
{
public:
	CiLambdaExpr() = default;
	void accept(CiVisitor * visitor, CiPriority parent) const override;
public:
	std::shared_ptr<CiExpr> body;
};

class CiCondCompletionStatement : public CiScope
{
public:
	virtual ~CiCondCompletionStatement() = default;
	bool completesNormally() const override;
	void setCompletesNormally(bool value);
protected:
	CiCondCompletionStatement() = default;
private:
	bool completesNormallyValue;
};

class CiBlock : public CiCondCompletionStatement
{
public:
	CiBlock() = default;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	std::vector<std::shared_ptr<CiStatement>> statements;
};

class CiAssert : public CiStatement
{
public:
	CiAssert() = default;
	bool completesNormally() const override;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	std::shared_ptr<CiExpr> cond;
	std::shared_ptr<CiExpr> message = nullptr;
};

class CiLoop : public CiCondCompletionStatement
{
public:
	virtual ~CiLoop() = default;
protected:
	CiLoop() = default;
public:
	std::shared_ptr<CiExpr> cond;
	std::shared_ptr<CiStatement> body;
	bool hasBreak = false;
};

class CiBreak : public CiStatement
{
public:
	CiBreak() = default;
	bool completesNormally() const override;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	CiCondCompletionStatement * loopOrSwitch;
};

class CiContinue : public CiStatement
{
public:
	CiContinue() = default;
	bool completesNormally() const override;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	const CiLoop * loop;
};

class CiDoWhile : public CiLoop
{
public:
	CiDoWhile() = default;
	void acceptStatement(CiVisitor * visitor) const override;
};

class CiFor : public CiLoop
{
public:
	CiFor() = default;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	std::shared_ptr<CiExpr> init;
	std::shared_ptr<CiExpr> advance;
	bool isRange = false;
	bool isIteratorUsed;
	int64_t rangeStep;
};

class CiForeach : public CiLoop
{
public:
	CiForeach() = default;
	void acceptStatement(CiVisitor * visitor) const override;
	CiVar * getVar() const;
	CiVar * getValueVar() const;
public:
	std::shared_ptr<CiExpr> collection;
};

class CiIf : public CiCondCompletionStatement
{
public:
	CiIf() = default;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	std::shared_ptr<CiExpr> cond;
	std::shared_ptr<CiStatement> onTrue;
	std::shared_ptr<CiStatement> onFalse;
};

class CiLock : public CiStatement
{
public:
	CiLock() = default;
	bool completesNormally() const override;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	std::shared_ptr<CiExpr> lock;
	std::shared_ptr<CiStatement> body;
};

class CiNative : public CiStatement
{
public:
	CiNative() = default;
	bool completesNormally() const override;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	std::string content;
};

class CiReturn : public CiStatement
{
public:
	CiReturn() = default;
	bool completesNormally() const override;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	std::shared_ptr<CiExpr> value;
};

class CiCase
{
public:
	CiCase() = default;
public:
	std::vector<std::shared_ptr<CiExpr>> values;
	std::vector<std::shared_ptr<CiStatement>> body;
};

class CiSwitch : public CiCondCompletionStatement
{
public:
	CiSwitch() = default;
	void acceptStatement(CiVisitor * visitor) const override;
	bool isTypeMatching() const;
	bool hasWhen() const;
	static int lengthWithoutTrailingBreak(const std::vector<std::shared_ptr<CiStatement>> * body);
	bool hasDefault() const;
	static bool hasEarlyBreak(const std::vector<std::shared_ptr<CiStatement>> * body);
	static bool hasEarlyBreakAndContinue(const std::vector<std::shared_ptr<CiStatement>> * body);
public:
	std::shared_ptr<CiExpr> value;
	std::vector<CiCase> cases;
	std::vector<std::shared_ptr<CiStatement>> defaultBody;
private:
	static bool hasBreak(const CiStatement * statement);
	static bool listHasContinue(const std::vector<std::shared_ptr<CiStatement>> * statements);
	static bool hasContinue(const CiStatement * statement);
};

class CiThrow : public CiStatement
{
public:
	CiThrow() = default;
	bool completesNormally() const override;
	void acceptStatement(CiVisitor * visitor) const override;
public:
	std::shared_ptr<CiExpr> message;
};

class CiWhile : public CiLoop
{
public:
	CiWhile() = default;
	void acceptStatement(CiVisitor * visitor) const override;
};

class CiParameters : public CiScope
{
public:
	CiParameters() = default;
};

class CiType : public CiScope
{
public:
	CiType() = default;
	virtual ~CiType() = default;
	virtual std::string getArraySuffix() const;
	virtual bool isAssignableFrom(const CiType * right) const;
	virtual bool equalsType(const CiType * right) const;
	virtual bool isArray() const;
	virtual bool isFinal() const;
	virtual const CiType * getBaseType() const;
	virtual const CiType * getStorageType() const;
	const CiClassType * asClassType() const;
public:
	bool nullable = false;
};

class CiNumericType : public CiType
{
public:
	virtual ~CiNumericType() = default;
protected:
	CiNumericType() = default;
};

class CiIntegerType : public CiNumericType
{
public:
	CiIntegerType() = default;
	virtual ~CiIntegerType() = default;
	bool isAssignableFrom(const CiType * right) const override;
};

class CiRangeType : public CiIntegerType
{
public:
	CiRangeType() = default;
	static std::shared_ptr<CiRangeType> new_(int min, int max);
	std::string toString() const override;
	bool isAssignableFrom(const CiType * right) const override;
	bool equalsType(const CiType * right) const override;
	static int getMask(int v);
	int getVariableBits() const;
public:
	int min;
	int max;
private:
	static void addMinMaxValue(std::shared_ptr<CiRangeType> target, std::string_view name, int value);
};

class CiFloatingType : public CiNumericType
{
public:
	CiFloatingType() = default;
	bool isAssignableFrom(const CiType * right) const override;
};

class CiNamedValue : public CiSymbol
{
public:
	virtual ~CiNamedValue() = default;
	bool isAssignableStorage() const;
protected:
	CiNamedValue() = default;
public:
	std::shared_ptr<CiExpr> typeExpr;
	std::shared_ptr<CiExpr> value;
};

class CiMember : public CiNamedValue
{
public:
	virtual ~CiMember() = default;
	virtual bool isStatic() const = 0;
protected:
	CiMember();
public:
	CiVisibility visibility;
};

class CiVar : public CiNamedValue
{
public:
	CiVar() = default;
	static std::shared_ptr<CiVar> new_(std::shared_ptr<CiType> type, std::string_view name, std::shared_ptr<CiExpr> defaultValue = nullptr);
	void accept(CiVisitor * visitor, CiPriority parent) const override;
	CiVar * nextParameter() const;
public:
	bool isAssigned = false;
};

class CiConst : public CiMember
{
public:
	CiConst() = default;
	void acceptStatement(CiVisitor * visitor) const override;
	bool isStatic() const override;
public:
	const CiMethodBase * inMethod;
	CiVisitStatus visitStatus;
};

class CiField : public CiMember
{
public:
	CiField() = default;
	bool isStatic() const override;
};

class CiProperty : public CiMember
{
public:
	CiProperty() = default;
	bool isStatic() const override;
	static std::shared_ptr<CiProperty> new_(std::shared_ptr<CiType> type, CiId id, std::string_view name);
};

class CiStaticProperty : public CiMember
{
public:
	CiStaticProperty() = default;
	bool isStatic() const override;
	static std::shared_ptr<CiStaticProperty> new_(std::shared_ptr<CiType> type, CiId id, std::string_view name);
};

class CiMethodBase : public CiMember
{
public:
	CiMethodBase() = default;
	virtual ~CiMethodBase() = default;
	bool isStatic() const override;
public:
	bool isMutator = false;
	bool throws;
	std::shared_ptr<CiStatement> body;
	bool isLive = false;
	std::unordered_set<CiMethod *> calls;
};

class CiMethod : public CiMethodBase
{
public:
	CiMethod() = default;
	static std::shared_ptr<CiMethod> new_(CiVisibility visibility, std::shared_ptr<CiType> type, CiId id, std::string_view name, std::shared_ptr<CiVar> param0 = nullptr, std::shared_ptr<CiVar> param1 = nullptr, std::shared_ptr<CiVar> param2 = nullptr, std::shared_ptr<CiVar> param3 = nullptr);
	static std::shared_ptr<CiMethod> newStatic(std::shared_ptr<CiType> type, CiId id, std::string_view name, std::shared_ptr<CiVar> param0, std::shared_ptr<CiVar> param1 = nullptr, std::shared_ptr<CiVar> param2 = nullptr);
	static std::shared_ptr<CiMethod> newMutator(CiVisibility visibility, std::shared_ptr<CiType> type, CiId id, std::string_view name, std::shared_ptr<CiVar> param0 = nullptr, std::shared_ptr<CiVar> param1 = nullptr, std::shared_ptr<CiVar> param2 = nullptr);
	bool isStatic() const override;
	bool isAbstractOrVirtual() const;
	const CiMethod * getDeclaringMethod() const;
	bool isToString() const;
public:
	CiCallType callType;
	CiParameters parameters;
	CiScope methodScope;
};

class CiMethodGroup : public CiMember
{
public:
	CiMethodGroup();
	bool isStatic() const override;
	static std::shared_ptr<CiMethodGroup> new_(std::shared_ptr<CiMethod> method0, std::shared_ptr<CiMethod> method1);
public:
	std::array<std::shared_ptr<CiMethod>, 2> methods;
};

class CiContainerType : public CiType
{
public:
	virtual ~CiContainerType() = default;
protected:
	CiContainerType() = default;
public:
	bool isPublic;
	std::string filename;
};

class CiEnum : public CiContainerType
{
public:
	CiEnum() = default;
	virtual ~CiEnum() = default;
	const CiSymbol * getFirstValue() const;
	void acceptValues(CiVisitor * visitor) const;
public:
	bool hasExplicitValue = false;
};

class CiEnumFlags : public CiEnum
{
public:
	CiEnumFlags() = default;
};

class CiClass : public CiContainerType
{
public:
	CiClass();
	bool hasBaseClass() const;
	bool addsVirtualMethods() const;
	static std::shared_ptr<CiClass> new_(CiCallType callType, CiId id, std::string_view name, int typeParameterCount = 0);
	bool isSameOrBaseOf(const CiClass * derived) const;
	bool hasToString() const;
	bool addsToString() const;
public:
	CiCallType callType;
	int typeParameterCount = 0;
	bool hasSubclasses = false;
	std::string baseClassName{""};
	std::shared_ptr<CiMethodBase> constructor;
	std::vector<const CiConst *> constArrays;
};

class CiClassType : public CiType
{
public:
	CiClassType() = default;
	virtual ~CiClassType() = default;
	std::shared_ptr<CiType> getElementType() const;
	const CiType * getKeyType() const;
	std::shared_ptr<CiType> getValueType() const;
	bool isArray() const override;
	const CiType * getBaseType() const override;
	bool isAssignableFrom(const CiType * right) const override;
	bool equalsType(const CiType * right) const override;
	std::string getArraySuffix() const override;
	virtual std::string_view getClassSuffix() const;
	std::string toString() const override;
protected:
	bool isAssignableFromClass(const CiClassType * right) const;
	bool equalsTypeInternal(const CiClassType * that) const;
public:
	const CiClass * class_;
	std::shared_ptr<CiType> typeArg0;
	std::shared_ptr<CiType> typeArg1;
	bool equalTypeArguments(const CiClassType * right) const;
private:
	std::string_view getNullableSuffix() const;
};

class CiReadWriteClassType : public CiClassType
{
public:
	CiReadWriteClassType() = default;
	virtual ~CiReadWriteClassType() = default;
	bool isAssignableFrom(const CiType * right) const override;
	bool equalsType(const CiType * right) const override;
	std::string getArraySuffix() const override;
	std::string_view getClassSuffix() const override;
};

class CiStorageType : public CiReadWriteClassType
{
public:
	CiStorageType() = default;
	virtual ~CiStorageType() = default;
	bool isFinal() const override;
	bool isAssignableFrom(const CiType * right) const override;
	bool equalsType(const CiType * right) const override;
	std::string_view getClassSuffix() const override;
};

class CiDynamicPtrType : public CiReadWriteClassType
{
public:
	CiDynamicPtrType() = default;
	bool isAssignableFrom(const CiType * right) const override;
	bool equalsType(const CiType * right) const override;
	std::string getArraySuffix() const override;
	std::string_view getClassSuffix() const override;
};

class CiArrayStorageType : public CiStorageType
{
public:
	CiArrayStorageType() = default;
	const CiType * getBaseType() const override;
	bool isArray() const override;
	std::string getArraySuffix() const override;
	bool equalsType(const CiType * right) const override;
	const CiType * getStorageType() const override;
public:
	std::shared_ptr<CiExpr> lengthExpr;
	int length;
	bool ptrTaken = false;
};

class CiStringType : public CiClassType
{
public:
	CiStringType() = default;
	virtual ~CiStringType() = default;
};

class CiStringStorageType : public CiStringType
{
public:
	CiStringStorageType() = default;
	bool isAssignableFrom(const CiType * right) const override;
	std::string_view getClassSuffix() const override;
};

class CiPrintableType : public CiType
{
public:
	CiPrintableType() = default;
	bool isAssignableFrom(const CiType * right) const override;
};

class CiSystem : public CiScope
{
public:
	CiSystem();
public:
	std::shared_ptr<CiType> voidType = std::make_shared<CiType>();
	std::shared_ptr<CiType> nullType = std::make_shared<CiType>();
	std::shared_ptr<CiIntegerType> intType = std::make_shared<CiIntegerType>();
	std::shared_ptr<CiIntegerType> longType = std::make_shared<CiIntegerType>();
	std::shared_ptr<CiRangeType> byteType = CiRangeType::new_(0, 255);
	std::shared_ptr<CiFloatingType> doubleType = std::make_shared<CiFloatingType>();
	std::shared_ptr<CiRangeType> charType = CiRangeType::new_(-128, 65535);
	std::shared_ptr<CiEnum> boolType = std::make_shared<CiEnum>();
	std::shared_ptr<CiStringType> stringPtrType = std::make_shared<CiStringType>();
	std::shared_ptr<CiStringType> stringNullablePtrType = std::make_shared<CiStringType>();
	std::shared_ptr<CiStringStorageType> stringStorageType = std::make_shared<CiStringStorageType>();
	std::shared_ptr<CiType> printableType = std::make_shared<CiPrintableType>();
	std::shared_ptr<CiClass> arrayPtrClass = CiClass::new_(CiCallType::normal, CiId::arrayPtrClass, "ArrayPtr", 1);
	std::shared_ptr<CiClass> arrayStorageClass = CiClass::new_(CiCallType::normal, CiId::arrayStorageClass, "ArrayStorage", 1);
	std::shared_ptr<CiEnum> regexOptionsEnum;
	std::shared_ptr<CiReadWriteClassType> lockPtrType = std::make_shared<CiReadWriteClassType>();
	std::shared_ptr<CiLiteralLong> newLiteralLong(int64_t value, int line = 0) const;
	std::shared_ptr<CiLiteralString> newLiteralString(std::string_view value, int line = 0) const;
	std::shared_ptr<CiType> promoteIntegerTypes(const CiType * left, const CiType * right) const;
	std::shared_ptr<CiType> promoteFloatingTypes(const CiType * left, const CiType * right) const;
	std::shared_ptr<CiType> promoteNumericTypes(std::shared_ptr<CiType> left, std::shared_ptr<CiType> right) const;
	std::shared_ptr<CiEnum> newEnum(bool flags) const;
	static std::shared_ptr<CiSystem> new_();
private:
	std::shared_ptr<CiType> typeParam0 = std::make_shared<CiType>();
	std::shared_ptr<CiRangeType> uIntType = CiRangeType::new_(0, 2147483647);
	std::shared_ptr<CiFloatingType> floatType = std::make_shared<CiFloatingType>();
	std::shared_ptr<CiClass> stringClass = CiClass::new_(CiCallType::normal, CiId::stringClass, "string");
	CiClass * addCollection(CiId id, std::string_view name, int typeParameterCount, CiId clearId, CiId countId);
	void addSet(CiId id, std::string_view name, CiId addId, CiId clearId, CiId containsId, CiId countId, CiId removeId);
	void addDictionary(CiId id, std::string_view name, CiId clearId, CiId containsKeyId, CiId countId, CiId removeId);
	static void addEnumValue(std::shared_ptr<CiEnum> enu, std::shared_ptr<CiConst> value);
	std::shared_ptr<CiConst> newConstLong(std::string_view name, int64_t value) const;
	std::shared_ptr<CiConst> newConstDouble(std::string_view name, double value) const;
	void addMinMaxValue(CiIntegerType * target, int64_t min, int64_t max) const;
};

class CiProgram : public CiScope
{
public:
	CiProgram() = default;
public:
	const CiSystem * system;
	std::vector<std::string> topLevelNatives;
	std::vector<CiClass *> classes;
	std::map<std::string, std::vector<uint8_t>> resources;
	bool regexOptionsEnum = false;
};

class CiParser : public CiLexer
{
public:
	virtual ~CiParser() = default;
	void parse(std::string_view filename, uint8_t const * input, int inputLength);
protected:
	CiParser() = default;
public:
	CiProgram * program;
private:
	std::string_view xcrementParent = std::string_view();
	const CiLoop * currentLoop = nullptr;
	CiCondCompletionStatement * currentLoopOrSwitch = nullptr;
	bool docParseLine(CiDocPara * para);
	void docParsePara(CiDocPara * para);
	std::shared_ptr<CiCodeDoc> parseDoc();
	void checkXcrementParent();
	std::shared_ptr<CiLiteralDouble> parseDouble();
	bool seeDigit() const;
	std::shared_ptr<CiInterpolatedString> parseInterpolatedString();
	std::shared_ptr<CiExpr> parseParenthesized();
	std::shared_ptr<CiSymbolReference> parseSymbolReference(std::shared_ptr<CiExpr> left);
	void parseCollection(std::vector<std::shared_ptr<CiExpr>> * result, CiToken closing);
	std::shared_ptr<CiExpr> parsePrimaryExpr(bool type);
	std::shared_ptr<CiExpr> parseMulExpr();
	std::shared_ptr<CiExpr> parseAddExpr();
	std::shared_ptr<CiExpr> parseShiftExpr();
	std::shared_ptr<CiExpr> parseRelExpr();
	std::shared_ptr<CiExpr> parseEqualityExpr();
	std::shared_ptr<CiExpr> parseAndExpr();
	std::shared_ptr<CiExpr> parseXorExpr();
	std::shared_ptr<CiExpr> parseOrExpr();
	std::shared_ptr<CiExpr> parseCondAndExpr();
	std::shared_ptr<CiExpr> parseCondOrExpr();
	std::shared_ptr<CiExpr> parseExpr();
	std::shared_ptr<CiExpr> parseType();
	std::shared_ptr<CiExpr> parseConstInitializer();
	std::shared_ptr<CiAggregateInitializer> parseObjectLiteral();
	std::shared_ptr<CiExpr> parseInitializer();
	void addSymbol(CiScope * scope, std::shared_ptr<CiSymbol> symbol);
	std::shared_ptr<CiVar> parseVar(std::shared_ptr<CiExpr> type);
	std::shared_ptr<CiConst> parseConst();
	std::shared_ptr<CiExpr> parseAssign(bool allowVar);
	std::shared_ptr<CiBlock> parseBlock();
	std::shared_ptr<CiAssert> parseAssert();
	std::shared_ptr<CiBreak> parseBreak();
	std::shared_ptr<CiContinue> parseContinue();
	void parseLoopBody(CiLoop * loop);
	std::shared_ptr<CiDoWhile> parseDoWhile();
	std::shared_ptr<CiFor> parseFor();
	void parseForeachIterator(CiForeach * result);
	std::shared_ptr<CiForeach> parseForeach();
	std::shared_ptr<CiIf> parseIf();
	std::shared_ptr<CiLock> parseLock();
	std::shared_ptr<CiNative> parseNative();
	std::shared_ptr<CiReturn> parseReturn();
	std::shared_ptr<CiSwitch> parseSwitch();
	std::shared_ptr<CiThrow> parseThrow();
	std::shared_ptr<CiWhile> parseWhile();
	std::shared_ptr<CiStatement> parseStatement();
	CiCallType parseCallType();
	void parseMethod(CiMethod * method);
	static std::string_view callTypeToString(CiCallType callType);
	void parseClass(std::shared_ptr<CiCodeDoc> doc, bool isPublic, CiCallType callType);
	void parseEnum(std::shared_ptr<CiCodeDoc> doc, bool isPublic);
};

class CiConsoleParser : public CiParser
{
public:
	CiConsoleParser() = default;
protected:
	void reportError(std::string_view message) override;
public:
	bool hasErrors = false;
};

class CiSema
{
public:
	CiSema();
	virtual ~CiSema() = default;
	void process(CiProgram * program);
protected:
	CiProgram * program;
	void reportError(const CiStatement * statement, std::string_view message);
	virtual int getResourceLength(std::string_view name, const CiPrefixExpr * expr);
public:
	bool hasErrors = false;
private:
	CiMethodBase * currentMethod = nullptr;
	CiScope * currentScope;
	std::unordered_set<const CiMethod *> currentPureMethods;
	std::unordered_map<const CiVar *, std::shared_ptr<CiExpr>> currentPureArguments;
	std::shared_ptr<CiType> poison = std::make_shared<CiType>();
	const CiContainerType * getCurrentContainer() const;
	std::shared_ptr<CiType> poisonError(const CiStatement * statement, std::string_view message);
	void resolveBase(CiClass * klass);
	void checkBaseCycle(CiClass * klass);
	static void takePtr(const CiExpr * expr);
	bool coerce(const CiExpr * expr, const CiType * type);
	std::shared_ptr<CiExpr> visitInterpolatedString(std::shared_ptr<CiInterpolatedString> expr);
	std::shared_ptr<CiExpr> lookup(std::shared_ptr<CiSymbolReference> expr, const CiScope * scope);
	std::shared_ptr<CiExpr> visitSymbolReference(std::shared_ptr<CiSymbolReference> expr);
	static std::shared_ptr<CiRangeType> union_(std::shared_ptr<CiRangeType> left, std::shared_ptr<CiRangeType> right);
	std::shared_ptr<CiType> getIntegerType(const CiExpr * left, const CiExpr * right);
	std::shared_ptr<CiIntegerType> getShiftType(const CiExpr * left, const CiExpr * right);
	std::shared_ptr<CiType> getNumericType(const CiExpr * left, const CiExpr * right);
	static int saturatedNeg(int a);
	static int saturatedAdd(int a, int b);
	static int saturatedSub(int a, int b);
	static int saturatedMul(int a, int b);
	static int saturatedDiv(int a, int b);
	static int saturatedShiftRight(int a, int b);
	static std::shared_ptr<CiRangeType> bitwiseUnsignedOp(const CiRangeType * left, CiToken op, const CiRangeType * right);
	bool isEnumOp(const CiExpr * left, const CiExpr * right);
	std::shared_ptr<CiType> bitwiseOp(const CiExpr * left, CiToken op, const CiExpr * right);
	static std::shared_ptr<CiRangeType> newRangeType(int a, int b, int c, int d);
	std::shared_ptr<CiLiteral> toLiteralBool(const CiExpr * expr, bool value) const;
	std::shared_ptr<CiLiteralLong> toLiteralLong(const CiExpr * expr, int64_t value) const;
	std::shared_ptr<CiLiteralDouble> toLiteralDouble(const CiExpr * expr, double value) const;
	void checkLValue(const CiExpr * expr);
	std::shared_ptr<CiInterpolatedString> concatenate(const CiInterpolatedString * left, const CiInterpolatedString * right) const;
	std::shared_ptr<CiInterpolatedString> toInterpolatedString(std::shared_ptr<CiExpr> expr) const;
	void checkComparison(const CiExpr * left, const CiExpr * right);
	void openScope(CiScope * scope);
	void closeScope();
	std::shared_ptr<CiExpr> resolveNew(std::shared_ptr<CiPrefixExpr> expr);
	std::shared_ptr<CiExpr> visitPrefixExpr(std::shared_ptr<CiPrefixExpr> expr);
	std::shared_ptr<CiExpr> visitPostfixExpr(std::shared_ptr<CiPostfixExpr> expr);
	static bool canCompareEqual(const CiType * left, const CiType * right);
	std::shared_ptr<CiExpr> resolveEquality(const CiBinaryExpr * expr, std::shared_ptr<CiExpr> left, std::shared_ptr<CiExpr> right);
	std::shared_ptr<CiExpr> resolveIs(std::shared_ptr<CiBinaryExpr> expr, std::shared_ptr<CiExpr> left, const CiExpr * right);
	std::shared_ptr<CiExpr> visitBinaryExpr(std::shared_ptr<CiBinaryExpr> expr);
	std::shared_ptr<CiType> tryGetPtr(std::shared_ptr<CiType> type, bool nullable) const;
	static const CiClass * getLowestCommonAncestor(const CiClass * left, const CiClass * right);
	std::shared_ptr<CiType> getCommonType(const CiExpr * left, const CiExpr * right);
	std::shared_ptr<CiExpr> visitSelectExpr(const CiSelectExpr * expr);
	std::shared_ptr<CiType> evalType(const CiClassType * generic, std::shared_ptr<CiType> type) const;
	bool canCall(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * arguments) const;
	std::shared_ptr<CiExpr> resolveCallWithArguments(std::shared_ptr<CiCallExpr> expr, const std::vector<std::shared_ptr<CiExpr>> * arguments);
	std::shared_ptr<CiExpr> visitCallExpr(std::shared_ptr<CiCallExpr> expr);
	void resolveObjectLiteral(const CiClassType * klass, const CiAggregateInitializer * init);
	void visitVar(std::shared_ptr<CiVar> expr);
	std::shared_ptr<CiExpr> visitExpr(std::shared_ptr<CiExpr> expr);
	std::shared_ptr<CiExpr> resolveBool(std::shared_ptr<CiExpr> expr);
	static std::shared_ptr<CiClassType> createClassPtr(const CiClass * klass, CiToken ptrModifier, bool nullable);
	void fillGenericClass(CiClassType * result, const CiClass * klass, const CiAggregateInitializer * typeArgExprs);
	void expectNoPtrModifier(const CiExpr * expr, CiToken ptrModifier, bool nullable);
	std::shared_ptr<CiType> toBaseType(const CiExpr * expr, CiToken ptrModifier, bool nullable);
	std::shared_ptr<CiType> toType(std::shared_ptr<CiExpr> expr, bool dynamic);
	std::shared_ptr<CiType> resolveType(CiNamedValue * def);
	void visitAssert(CiAssert * statement);
	bool resolveStatements(const std::vector<std::shared_ptr<CiStatement>> * statements);
	void visitBlock(CiBlock * statement);
	void resolveLoopCond(CiLoop * statement);
	void visitDoWhile(CiDoWhile * statement);
	void visitFor(CiFor * statement);
	void visitForeach(CiForeach * statement);
	void visitIf(CiIf * statement);
	void visitLock(CiLock * statement);
	void visitReturn(CiReturn * statement);
	void visitSwitch(CiSwitch * statement);
	void visitThrow(CiThrow * statement);
	void visitWhile(CiWhile * statement);
	void visitStatement(std::shared_ptr<CiStatement> statement);
	std::shared_ptr<CiExpr> foldConst(std::shared_ptr<CiExpr> expr);
	int foldConstInt(std::shared_ptr<CiExpr> expr);
	void resolveTypes(CiClass * klass);
	void resolveConst(CiConst * konst);
	void resolveConsts(CiContainerType * container);
	void resolveCode(CiClass * klass);
	static void markMethodLive(CiMethodBase * method);
	static void markClassLive(const CiClass * klass);
};

class GenHost
{
public:
	virtual ~GenHost() = default;
	virtual std::ostream * createFile(std::string_view directory, std::string_view filename) = 0;
	virtual void closeFile() = 0;
protected:
	GenHost() = default;
};

class GenBase : public CiVisitor
{
public:
	virtual ~GenBase() = default;
	void visitLiteralNull() override;
	void visitLiteralFalse() override;
	void visitLiteralTrue() override;
	void visitLiteralLong(int64_t i) override;
	void visitLiteralChar(int c) override;
	void visitLiteralDouble(double value) override;
	void visitLiteralString(std::string_view value) override;
	void visitVar(const CiVar * expr) override;
	void visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent) override;
	void visitPostfixExpr(const CiPostfixExpr * expr, CiPriority parent) override;
	void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitCallExpr(const CiCallExpr * expr, CiPriority parent) override;
	void visitSelectExpr(const CiSelectExpr * expr, CiPriority parent) override;
	void visitExpr(const CiExpr * statement) override;
	void visitConst(const CiConst * statement) override;
	void visitAssert(const CiAssert * statement) override;
	void visitBlock(const CiBlock * statement) override;
	void visitBreak(const CiBreak * statement) override;
	void visitContinue(const CiContinue * statement) override;
	void visitDoWhile(const CiDoWhile * statement) override;
	void visitFor(const CiFor * statement) override;
	void visitIf(const CiIf * statement) override;
	void visitNative(const CiNative * statement) override;
	void visitReturn(const CiReturn * statement) override;
	void visitSwitch(const CiSwitch * statement) override;
	void visitWhile(const CiWhile * statement) override;
	void visitEnumValue(const CiConst * konst, const CiConst * previous) override;
	virtual void writeProgram(const CiProgram * program) = 0;
protected:
	GenBase() = default;
	int indent = 0;
	bool atLineStart = true;
	bool inHeaderFile = false;
	const CiMethodBase * currentMethod = nullptr;
	std::unordered_set<const CiClass *> writtenClasses;
	std::vector<const CiExpr *> currentTemporaries;
	const CiContainerType * getCurrentContainer() const override;
	virtual std::string_view getTargetName() const = 0;
	void notSupported(const CiStatement * statement, std::string_view feature);
	void notYet(const CiStatement * statement, std::string_view feature);
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
	virtual void writeName(const CiSymbol * symbol) = 0;
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
	virtual void writeDocPara(const CiDocPara * para, bool many);
	virtual void writeDocList(const CiDocList * list);
	void writeDocBlock(const CiDocBlock * block, bool many);
	void writeContent(const CiCodeDoc * doc);
	virtual void writeDoc(const CiCodeDoc * doc);
	virtual void writeSelfDoc(const CiMethod * method);
	virtual void writeParameterDoc(const CiVar * param, bool first);
	void writeParametersDoc(const CiMethod * method);
	void writeMethodDoc(const CiMethod * method);
	void writeTopLevelNatives(const CiProgram * program);
	void openBlock();
	void closeBlock();
	virtual void endStatement();
	void writeComma(int i);
	void writeBytes(const std::vector<uint8_t> * content);
	virtual CiId getTypeId(const CiType * type, bool promote) const;
	virtual void writeTypeAndName(const CiNamedValue * value) = 0;
	virtual void writeLocalName(const CiSymbol * symbol, CiPriority parent);
	void writeDoubling(std::string_view s, int doubled);
	virtual void writePrintfWidth(const CiInterpolatedPart * part);
	void writePrintfFormat(const CiInterpolatedString * expr);
	void writePyFormat(const CiInterpolatedPart * part);
	virtual void writeInterpolatedStringArg(const CiExpr * expr);
	void writeInterpolatedStringArgs(const CiInterpolatedString * expr);
	void writePrintf(const CiInterpolatedString * expr, bool newLine);
	void writePostfix(const CiExpr * obj, std::string_view s);
	void writeCall(std::string_view function, const CiExpr * arg0, const CiExpr * arg1 = nullptr, const CiExpr * arg2 = nullptr);
	virtual void writeMemberOp(const CiExpr * left, const CiSymbolReference * symbol);
	void writeMethodCall(const CiExpr * obj, std::string_view method, const CiExpr * arg0, const CiExpr * arg1 = nullptr);
	virtual void writeSelectValues(const CiType * type, const CiSelectExpr * expr);
	virtual void writeCoercedSelect(const CiType * type, const CiSelectExpr * expr, CiPriority parent);
	virtual void writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent);
	void writeCoerced(const CiType * type, const CiExpr * expr, CiPriority parent);
	virtual void writeCoercedExpr(const CiType * type, const CiExpr * expr);
	virtual void writeStronglyCoerced(const CiType * type, const CiExpr * expr);
	virtual void writeCoercedLiteral(const CiType * type, const CiExpr * literal);
	void writeCoercedLiterals(const CiType * type, const std::vector<std::shared_ptr<CiExpr>> * exprs);
	void writeArgs(const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeArgsInParentheses(const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args);
	virtual void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) = 0;
	virtual void writeNewArrayStorage(const CiArrayStorageType * array);
	virtual void writeNew(const CiReadWriteClassType * klass, CiPriority parent) = 0;
	void writeNewStorage(const CiType * type);
	virtual void writeArrayStorageInit(const CiArrayStorageType * array, const CiExpr * value);
	virtual void writeNewWithFields(const CiReadWriteClassType * type, const CiAggregateInitializer * init);
	virtual void writeStorageInit(const CiNamedValue * def);
	virtual void writeVarInit(const CiNamedValue * def);
	virtual void writeVar(const CiNamedValue * def);
	void writeObjectLiteral(const CiAggregateInitializer * init, std::string_view separator);
	virtual void writeInitCode(const CiNamedValue * def);
	virtual void defineIsVar(const CiBinaryExpr * binary);
	void writeArrayElement(const CiNamedValue * def, int nesting);
	void openLoop(std::string_view intString, int nesting, int count);
	void writeResourceName(std::string_view name);
	virtual void writeResource(std::string_view name, int length) = 0;
	void startAdd(const CiExpr * expr);
	void writeAdd(const CiExpr * left, const CiExpr * right);
	void writeStartEnd(const CiExpr * startIndex, const CiExpr * length);
	virtual void writeBinaryOperand(const CiExpr * expr, CiPriority parent, const CiBinaryExpr * binary);
	void writeBinaryExpr(const CiBinaryExpr * expr, bool parentheses, CiPriority left, std::string_view op, CiPriority right);
	void writeBinaryExpr2(const CiBinaryExpr * expr, CiPriority parent, CiPriority child, std::string_view op);
	static std::string_view getEqOp(bool not_);
	virtual void writeEqualOperand(const CiExpr * expr, const CiExpr * other);
	void writeEqualExpr(const CiExpr * left, const CiExpr * right, CiPriority parent, std::string_view op);
	virtual void writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_);
	virtual void writeAnd(const CiBinaryExpr * expr, CiPriority parent);
	virtual void writeAssignRight(const CiBinaryExpr * expr);
	virtual void writeAssign(const CiBinaryExpr * expr, CiPriority parent);
	void writeIndexing(const CiExpr * collection, const CiExpr * index);
	virtual void writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent);
	virtual std::string_view getIsOperator() const;
	virtual void writeStringLength(const CiExpr * expr) = 0;
	static bool isReferenceTo(const CiExpr * expr, CiId id);
	bool writeJavaMatchProperty(const CiSymbolReference * expr, CiPriority parent);
	virtual void writeCharAt(const CiBinaryExpr * expr) = 0;
	virtual void writeNotPromoted(const CiType * type, const CiExpr * expr);
	virtual void writeEnumAsInt(const CiExpr * expr, CiPriority parent);
	void writeEnumHasFlag(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent);
	void writeTryParseRadix(const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeListAdd(const CiExpr * obj, std::string_view method, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeListInsert(const CiExpr * obj, std::string_view method, const std::vector<std::shared_ptr<CiExpr>> * args, std::string_view separator = ", ");
	void writeDictionaryAdd(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeClampAsMinMax(const std::vector<std::shared_ptr<CiExpr>> * args);
	RegexOptions getRegexOptions(const std::vector<std::shared_ptr<CiExpr>> * args) const;
	bool writeRegexOptions(const std::vector<std::shared_ptr<CiExpr>> * args, std::string_view prefix, std::string_view separator, std::string_view suffix, std::string_view i, std::string_view m, std::string_view s);
	virtual void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) = 0;
	void ensureChildBlock();
	virtual void startTemporaryVar(const CiType * type) = 0;
	virtual void defineObjectLiteralTemporary(const CiUnaryExpr * expr);
	void writeTemporaries(const CiExpr * expr);
	virtual void cleanupTemporary(int i, const CiExpr * temp);
	void cleanupTemporaries();
	virtual void writeAssertCast(const CiBinaryExpr * expr) = 0;
	virtual void writeAssert(const CiAssert * statement) = 0;
	void writeFirstStatements(const std::vector<std::shared_ptr<CiStatement>> * statements, int count);
	virtual void writeStatements(const std::vector<std::shared_ptr<CiStatement>> * statements);
	virtual void cleanupBlock(const CiBlock * statement);
	virtual void writeChild(CiStatement * statement);
	virtual bool embedIfWhileIsVar(const CiExpr * expr, bool write);
	void defineVar(const CiExpr * value);
	virtual void writeSwitchCaseTypeVar(const CiExpr * value);
	virtual void writeSwitchValue(const CiExpr * expr);
	virtual void writeSwitchCaseBody(const std::vector<std::shared_ptr<CiStatement>> * statements);
	virtual void writeSwitchCase(const CiSwitch * statement, const CiCase * kase);
	void startSwitch(const CiSwitch * statement);
	virtual void writeSwitchCaseCond(const CiSwitch * statement, const CiExpr * value, CiPriority parent);
	virtual void writeIfCaseBody(const std::vector<std::shared_ptr<CiStatement>> * body, bool doWhile, const CiSwitch * statement, const CiCase * kase);
	void writeSwitchAsIfs(const CiSwitch * statement, bool doWhile);
	void flattenBlock(CiStatement * statement);
	virtual bool hasInitCode(const CiNamedValue * def) const;
	virtual bool needsConstructor(const CiClass * klass) const;
	virtual void writeInitField(const CiField * field);
	void writeConstructorBody(const CiClass * klass);
	virtual void writeParameter(const CiVar * param);
	void writeRemainingParameters(const CiMethod * method, bool first, bool defaultArguments);
	void writeParameters(const CiMethod * method, bool defaultArguments);
	void writeBody(const CiMethod * method);
	void writePublic(const CiContainerType * container);
	void writeEnumValue(const CiConst * konst);
	virtual void writeEnum(const CiEnum * enu) = 0;
	virtual void writeRegexOptionsEnum(const CiProgram * program);
	void startClass(const CiClass * klass, std::string_view suffix, std::string_view extendsClause);
	void openClass(const CiClass * klass, std::string_view suffix, std::string_view extendsClause);
	virtual void writeConst(const CiConst * konst) = 0;
	virtual void writeField(const CiField * field) = 0;
	virtual void writeMethod(const CiMethod * method) = 0;
	void writeMembers(const CiClass * klass, bool constArrays);
	bool writeBaseClass(const CiClass * klass, const CiProgram * program);
	virtual void writeClass(const CiClass * klass, const CiProgram * program) = 0;
	void writeTypes(const CiProgram * program);
public:
	std::string namespace_;
	std::string outputFile;
	GenHost * host;
private:
	std::ostream * writer;
	std::ostringstream stringWriter;
	bool atChildStart = false;
	bool inChildBlock = false;
	std::map<std::string, bool> includes;
	void writeLowercaseChar(int c);
	void writeUppercaseChar(int c);
	static int getPrintfFormat(const CiType * type, int format);
	static const CiAggregateInitializer * getAggregateInitializer(const CiNamedValue * def);
	void writeAggregateInitField(const CiExpr * obj, const CiExpr * item);
	static bool isBitOp(CiPriority parent);
	void writeRel(const CiBinaryExpr * expr, CiPriority parent, std::string_view op);
	void startIfWhile(const CiExpr * expr);
	void writeIf(const CiIf * statement);
};

class GenTyped : public GenBase
{
public:
	virtual ~GenTyped() = default;
	void visitLiteralDouble(double value) override;
	void visitAggregateInitializer(const CiAggregateInitializer * expr) override;
protected:
	GenTyped() = default;
	virtual void writeType(const CiType * type, bool promote) = 0;
	void writeTypeAndName(const CiNamedValue * value) override;
	void writeArrayStorageLength(const CiExpr * expr);
	void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) override;
	int getOneAscii(const CiExpr * expr) const;
	static bool isNarrower(CiId left, CiId right);
	const CiExpr * getStaticCastInner(const CiType * type, const CiExpr * expr) const;
	void writeStaticCastType(const CiType * type);
	virtual void writeStaticCast(const CiType * type, const CiExpr * expr);
	void writeNotPromoted(const CiType * type, const CiExpr * expr) override;
	virtual bool isPromoted(const CiExpr * expr) const;
	void writeAssignRight(const CiBinaryExpr * expr) override;
	void writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent) override;
	void writeCharAt(const CiBinaryExpr * expr) override;
	void startTemporaryVar(const CiType * type) override;
	void writeAssertCast(const CiBinaryExpr * expr) override;
};

class GenCCppD : public GenTyped
{
public:
	virtual ~GenCCppD() = default;
	void visitLiteralLong(int64_t i) override;
	void visitConst(const CiConst * statement) override;
	void visitBreak(const CiBreak * statement) override;
protected:
	GenCCppD() = default;
	std::vector<const CiSwitch *> switchesWithGoto;
	void writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_) override;
	void writeSwitchAsIfsWithGoto(const CiSwitch * statement);
private:
	static bool isPtrTo(const CiExpr * ptr, const CiExpr * other);
};

class GenCCpp : public GenCCppD
{
public:
	virtual ~GenCCpp() = default;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitSwitch(const CiSwitch * statement) override;
protected:
	GenCCpp() = default;
	virtual void includeStdInt() = 0;
	virtual void includeAssert() = 0;
	virtual void includeMath() = 0;
	int getLiteralChars() const override;
	virtual void writeNumericType(CiId id);
	static const CiExpr * isStringEmpty(const CiBinaryExpr * expr);
	virtual void writeArrayPtr(const CiExpr * expr, CiPriority parent) = 0;
	void writeArrayPtrAdd(const CiExpr * array, const CiExpr * index);
	static const CiCallExpr * isStringSubstring(const CiExpr * expr);
	static bool isUTF8GetString(const CiCallExpr * call);
	static const CiExpr * getStringSubstringPtr(const CiCallExpr * call);
	static const CiExpr * getStringSubstringOffset(const CiCallExpr * call);
	static const CiExpr * getStringSubstringLength(const CiCallExpr * call);
	void writeStringPtrAdd(const CiCallExpr * call);
	static const CiExpr * isTrimSubstring(const CiBinaryExpr * expr);
	void writeStringLiteralWithNewLine(std::string_view s);
	virtual void writeUnreachable(const CiAssert * statement);
	void writeAssert(const CiAssert * statement) override;
	void writeMethods(const CiClass * klass);
	virtual void writeClassInternal(const CiClass * klass) = 0;
	void writeClass(const CiClass * klass, const CiProgram * program) override;
	void createHeaderFile(std::string_view headerExt);
	void createImplementationFile(const CiProgram * program, std::string_view headerExt);
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
	void visitLiteralNull() override;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void visitLambdaExpr(const CiLambdaExpr * expr) override;
	void visitBreak(const CiBreak * statement) override;
	void visitContinue(const CiContinue * statement) override;
	void visitExpr(const CiExpr * statement) override;
	void visitForeach(const CiForeach * statement) override;
	void visitLock(const CiLock * statement) override;
	void visitReturn(const CiReturn * statement) override;
	void visitSwitch(const CiSwitch * statement) override;
	void visitThrow(const CiThrow * statement) override;
	void writeProgram(const CiProgram * program) override;
protected:
	const CiClass * currentClass;
	const CiContainerType * getCurrentContainer() const override;
	std::string_view getTargetName() const override;
	void writeSelfDoc(const CiMethod * method) override;
	void includeStdInt() override;
	void includeAssert() override;
	void includeMath() override;
	virtual void includeStdBool();
	virtual void writePrintfLongPrefix();
	void writePrintfWidth(const CiInterpolatedPart * part) override;
	virtual void writeInterpolatedStringArgBase(const CiExpr * expr);
	void writeInterpolatedStringArg(const CiExpr * expr) override;
	virtual void writeCamelCaseNotKeyword(std::string_view name);
	void writeName(const CiSymbol * symbol) override;
	void writeLocalName(const CiSymbol * symbol, CiPriority parent) override;
	virtual void writeStringPtrType();
	virtual void writeClassType(const CiClassType * klass, bool space);
	void writeType(const CiType * type, bool promote) override;
	void writeTypeAndName(const CiNamedValue * value) override;
	void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) override;
	void writeArrayStorageInit(const CiArrayStorageType * array, const CiExpr * value) override;
	void writeNew(const CiReadWriteClassType * klass, CiPriority parent) override;
	void writeStorageInit(const CiNamedValue * def) override;
	void writeVarInit(const CiNamedValue * def) override;
	void cleanupTemporary(int i, const CiExpr * temp) override;
	void writeVar(const CiNamedValue * def) override;
	void writeAssign(const CiBinaryExpr * expr, CiPriority parent) override;
	bool hasInitCode(const CiNamedValue * def) const override;
	void writeInitCode(const CiNamedValue * def) override;
	void writeMemberOp(const CiExpr * left, const CiSymbolReference * symbol) override;
	void writeArrayPtr(const CiExpr * expr, CiPriority parent) override;
	void writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent) override;
	virtual void writeSubstringEqual(const CiCallExpr * call, std::string_view literal, CiPriority parent, bool not_);
	virtual void writeEqualStringInternal(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_);
	void writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeArrayFill(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writePrintfNotInterpolated(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine);
	void writeCCall(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeStringSubstring(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent);
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	void cleanupBlock(const CiBlock * statement) override;
	void writeSwitchCaseBody(const std::vector<std::shared_ptr<CiStatement>> * statements) override;
	void writeStatements(const std::vector<std::shared_ptr<CiStatement>> * statements) override;
	void writeEnum(const CiEnum * enu) override;
	void writeTypedefs(const CiProgram * program, bool pub);
	virtual std::string_view getConst(const CiArrayStorageType * array) const;
	void writeConst(const CiConst * konst) override;
	void writeField(const CiField * field) override;
	bool needsConstructor(const CiClass * klass) const override;
	void writeSignatures(const CiClass * klass, bool pub);
	void writeClassInternal(const CiClass * klass) override;
	void writeConstructor(const CiClass * klass);
	void writeDestructor(const CiClass * klass);
	void writeMethod(const CiMethod * method) override;
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
private:
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
	std::map<std::string_view, std::string_view> listFrees;
	bool treeCompareInteger;
	bool treeCompareString;
	std::set<CiId> compares;
	std::set<CiId> contains;
	std::vector<const CiVar *> varsToDestruct;
	void writeStringPtrAddCast(const CiCallExpr * call);
	static bool isDictionaryClassStgIndexing(const CiExpr * expr);
	void writeTemporaryOrExpr(const CiExpr * expr, CiPriority parent);
	void writeUpcast(const CiClass * resultClass, const CiSymbol * klass);
	void writeClassPtr(const CiClass * resultClass, const CiExpr * expr, CiPriority parent);
	void writeSelfForField(const CiSymbol * fieldClass);
	void writeMatchProperty(const CiSymbolReference * expr, int which);
	void writeGlib(std::string_view s);
	void writeArrayPrefix(const CiType * type);
	void startDefinition(const CiType * type, bool promote, bool space);
	void endDefinition(const CiType * type);
	void writeReturnType(const CiMethod * method);
	void writeDynamicArrayCast(const CiType * elementType);
	void writeXstructorPtr(bool need, const CiClass * klass, std::string_view name);
	static bool isHeapAllocated(const CiType * type);
	static bool needToDestructType(const CiType * type);
	static bool needToDestruct(const CiSymbol * symbol);
	static bool needsDestructor(const CiClass * klass);
	void writeXstructorPtrs(const CiClass * klass);
	static bool isNewString(const CiExpr * expr);
	void writeStringStorageValue(const CiExpr * expr);
	std::string getDictionaryDestroy(const CiType * type);
	void writeHashEqual(const CiType * keyType);
	void writeNewHashTable(const CiType * keyType, std::string_view valueDestroy);
	void writeNewTree(const CiType * keyType, std::string_view valueDestroy);
	void writeAssignTemporary(const CiType * type, const CiExpr * expr);
	int writeCTemporary(const CiType * type, const CiExpr * expr);
	void writeStorageTemporary(const CiExpr * expr);
	void writeCTemporaries(const CiExpr * expr);
	static bool hasTemporariesToDestruct(const CiExpr * expr);
	void writeGPointerCast(const CiType * type, const CiExpr * expr);
	void writeGConstPointerCast(const CiExpr * expr);
	void writeQueueObject(const CiExpr * obj);
	void writeQueueGet(std::string_view function, const CiExpr * obj, CiPriority parent);
	void startDictionaryInsert(const CiExpr * dict, const CiExpr * key);
	static const CiMethod * getThrowingMethod(const CiExpr * expr);
	static bool hasListDestroy(const CiType * type);
	CiPriority startForwardThrow(const CiMethod * throwingMethod);
	void writeDestruct(const CiSymbol * symbol);
	void writeDestructAll(const CiVar * exceptVar = nullptr);
	void writeThrowReturnValue();
	void writeThrow();
	void endForwardThrow(const CiMethod * throwingMethod);
	void writeMemberAccess(const CiExpr * left, const CiSymbol * symbolClass);
	void writeStringMethod(std::string_view name, const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeSizeofCompare(const CiType * elementType);
	void writeListAddInsert(const CiExpr * obj, bool insert, std::string_view function, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeDictionaryLookup(const CiExpr * obj, std::string_view function, const CiExpr * key);
	void writeArgsAndRightParenthesis(const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeCRegexOptions(const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeTextWriterWrite(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine);
	void writeConsoleWrite(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine);
	static const CiClass * getVtblStructClass(const CiClass * klass);
	static const CiClass * getVtblPtrClass(const CiClass * klass);
	void writeTryParse(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args);
	void startArrayIndexing(const CiExpr * obj, const CiType * elementType);
	void writeDictionaryIndexing(std::string_view function, const CiBinaryExpr * expr, CiPriority parent);
	void writeDestructLoopOrSwitch(const CiCondCompletionStatement * loopOrSwitch);
	void trimVarsToDestruct(int i);
	void startForeachHashTable(const CiForeach * statement);
	void writeDictIterVar(const CiNamedValue * iter, std::string_view value);
	bool tryWriteCallAndReturn(const std::vector<std::shared_ptr<CiStatement>> * statements, int lastCallIndex, const CiExpr * returnValue);
	void writeTypedef(const CiClass * klass);
	void writeInstanceParameters(const CiMethod * method);
	void writeSignature(const CiMethod * method);
	void writeVtblFields(const CiClass * klass);
	void writeVtblStruct(const CiClass * klass);
	static bool hasVtblValue(const CiClass * klass);
	void writeXstructorSignature(std::string_view name, const CiClass * klass);
	void writeVtbl(const CiClass * definingClass, const CiClass * declaringClass);
	void writeDestructFields(const CiSymbol * symbol);
	void writeNewDelete(const CiClass * klass, bool define);
	void writeTryParseLibrary(std::string_view signature, std::string_view call);
	void writeLibrary();
};

class GenCl : public GenC
{
public:
	GenCl() = default;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void includeStdBool() override;
	void includeMath() override;
	void writeNumericType(CiId id) override;
	void writeStringPtrType() override;
	void writeClassType(const CiClassType * klass, bool space) override;
	void writePrintfLongPrefix() override;
	void writeInterpolatedStringArgBase(const CiExpr * expr) override;
	void writeCamelCaseNotKeyword(std::string_view name) override;
	std::string_view getConst(const CiArrayStorageType * array) const override;
	void writeSubstringEqual(const CiCallExpr * call, std::string_view literal, CiPriority parent, bool not_) override;
	void writeEqualStringInternal(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeAssert(const CiAssert * statement) override;
	void writeSwitchCaseBody(const std::vector<std::shared_ptr<CiStatement>> * statements) override;
private:
	bool stringLength;
	bool stringEquals;
	bool stringStartsWith;
	bool bytesEqualsString;
	void writeConsoleWrite(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine);
	void writeLibrary();
};

class GenCpp : public GenCCpp
{
public:
	GenCpp() = default;
	void visitLiteralNull() override;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void visitLambdaExpr(const CiLambdaExpr * expr) override;
	void visitForeach(const CiForeach * statement) override;
	void visitLock(const CiLock * statement) override;
	void visitSwitch(const CiSwitch * statement) override;
	void visitThrow(const CiThrow * statement) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void includeStdInt() override;
	void includeAssert() override;
	void includeMath() override;
	void writeInterpolatedStringArg(const CiExpr * expr) override;
	void writeName(const CiSymbol * symbol) override;
	void writeLocalName(const CiSymbol * symbol, CiPriority parent) override;
	void writeType(const CiType * type, bool promote) override;
	void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) override;
	void writeNew(const CiReadWriteClassType * klass, CiPriority parent) override;
	void writeStorageInit(const CiNamedValue * def) override;
	void writeVarInit(const CiNamedValue * def) override;
	void writeStaticCast(const CiType * type, const CiExpr * expr) override;
	void writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_) override;
	void writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void writeMemberOp(const CiExpr * left, const CiSymbolReference * symbol) override;
	void writeEnumAsInt(const CiExpr * expr, CiPriority parent) override;
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	void writeArrayPtr(const CiExpr * expr, CiPriority parent) override;
	void writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent) override;
	void writeSelectValues(const CiType * type, const CiSelectExpr * expr) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeUnreachable(const CiAssert * statement) override;
	void writeConst(const CiConst * konst) override;
	bool embedIfWhileIsVar(const CiExpr * expr, bool write) override;
	void writeStronglyCoerced(const CiType * type, const CiExpr * expr) override;
	void writeSwitchCaseCond(const CiSwitch * statement, const CiExpr * value, CiPriority parent) override;
	void writeSwitchCaseBody(const std::vector<std::shared_ptr<CiStatement>> * statements) override;
	void writeEnum(const CiEnum * enu) override;
	void writeField(const CiField * field) override;
	void writeClassInternal(const CiClass * klass) override;
	void writeMethod(const CiMethod * method) override;
private:
	bool usingStringViewLiterals;
	bool hasEnumFlags;
	bool stringReplace;
	void startMethodCall(const CiExpr * obj);
	void writeCamelCaseNotKeyword(std::string_view name);
	void writeCollectionType(std::string_view name, const CiType * elementType);
	static bool isSharedPtr(const CiExpr * expr);
	static bool needStringPtrData(const CiExpr * expr);
	static bool isClassPtr(const CiType * type);
	static bool isCppPtr(const CiExpr * expr);
	void writeCollectionObject(const CiExpr * obj, CiPriority priority);
	void writeBeginEnd(const CiExpr * obj);
	void writePtrRange(const CiExpr * obj, const CiExpr * index, const CiExpr * count);
	void writeNotRawStringLiteral(const CiExpr * obj, CiPriority priority);
	void writeStringMethod(const CiExpr * obj, std::string_view name, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeAllAnyContains(std::string_view function, const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeCString(const CiExpr * expr);
	void writeRegex(const std::vector<std::shared_ptr<CiExpr>> * args, int argIndex);
	void writeWrite(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine);
	void writeRegexArgument(const CiExpr * expr);
	void writeMatchProperty(const CiSymbolReference * expr, std::string_view name);
	void writeGtRawPtr(const CiExpr * expr);
	void writeIsVar(const CiExpr * expr, const CiVar * def, CiPriority parent);
	static bool hasTemporaries(const CiExpr * expr);
	static bool isIsVar(const CiExpr * expr);
	bool hasVariables(const CiStatement * statement) const;
	void openNamespace();
	void closeNamespace();
	static CiVisibility getConstructorVisibility(const CiClass * klass);
	static bool hasMembersOfVisibility(const CiClass * klass, CiVisibility visibility);
	void writeParametersAndConst(const CiMethod * method, bool defaultArguments);
	void writeDeclarations(const CiClass * klass, CiVisibility visibility, std::string_view visibilityKeyword);
	void writeConstructor(const CiClass * klass);
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources, bool define);
};

class GenCs : public GenTyped
{
public:
	GenCs() = default;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void visitLambdaExpr(const CiLambdaExpr * expr) override;
	void visitForeach(const CiForeach * statement) override;
	void visitLock(const CiLock * statement) override;
	void visitThrow(const CiThrow * statement) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void startDocLine() override;
	void writeDocPara(const CiDocPara * para, bool many) override;
	void writeDocList(const CiDocList * list) override;
	void writeDoc(const CiCodeDoc * doc) override;
	void writeName(const CiSymbol * symbol) override;
	int getLiteralChars() const override;
	void writeType(const CiType * type, bool promote) override;
	void writeNewWithFields(const CiReadWriteClassType * type, const CiAggregateInitializer * init) override;
	void writeCoercedLiteral(const CiType * type, const CiExpr * literal) override;
	bool isPromoted(const CiExpr * expr) const override;
	void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) override;
	void writeNew(const CiReadWriteClassType * klass, CiPriority parent) override;
	bool hasInitCode(const CiNamedValue * def) const override;
	void writeInitCode(const CiNamedValue * def) override;
	void writeResource(std::string_view name, int length) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void writeAssign(const CiBinaryExpr * expr, CiPriority parent) override;
	void defineObjectLiteralTemporary(const CiUnaryExpr * expr) override;
	void defineIsVar(const CiBinaryExpr * binary) override;
	void writeAssert(const CiAssert * statement) override;
	void writeEnum(const CiEnum * enu) override;
	void writeRegexOptionsEnum(const CiProgram * program) override;
	void writeConst(const CiConst * konst) override;
	void writeField(const CiField * field) override;
	void writeParameterDoc(const CiVar * param, bool first) override;
	void writeMethod(const CiMethod * method) override;
	void writeClass(const CiClass * klass, const CiProgram * program) override;
private:
	void writeVisibility(CiVisibility visibility);
	void writeCallType(CiCallType callType, std::string_view sealedString);
	void writeElementType(const CiType * elementType);
	void writeOrderedDictionaryIndexing(const CiBinaryExpr * expr);
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
};

class GenD : public GenCCppD
{
public:
	GenD() = default;
	void visitAggregateInitializer(const CiAggregateInitializer * expr) override;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void visitLambdaExpr(const CiLambdaExpr * expr) override;
	void visitForeach(const CiForeach * statement) override;
	void visitLock(const CiLock * statement) override;
	void visitSwitch(const CiSwitch * statement) override;
	void visitThrow(const CiThrow * statement) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void startDocLine() override;
	void writeDocPara(const CiDocPara * para, bool many) override;
	void writeParameterDoc(const CiVar * param, bool first) override;
	void writeDocList(const CiDocList * list) override;
	void writeDoc(const CiCodeDoc * doc) override;
	void writeName(const CiSymbol * symbol) override;
	int getLiteralChars() const override;
	void writeType(const CiType * type, bool promote) override;
	void writeTypeAndName(const CiNamedValue * value) override;
	void writeStaticCast(const CiType * type, const CiExpr * expr) override;
	void writeStorageInit(const CiNamedValue * def) override;
	void writeVarInit(const CiNamedValue * def) override;
	bool hasInitCode(const CiNamedValue * def) const override;
	void writeInitField(const CiField * field) override;
	void writeInitCode(const CiNamedValue * def) override;
	void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) override;
	void writeNew(const CiReadWriteClassType * klass, CiPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_) override;
	void writeAssign(const CiBinaryExpr * expr, CiPriority parent) override;
	void writeAssert(const CiAssert * statement) override;
	void writeSwitchCaseTypeVar(const CiExpr * value) override;
	void writeSwitchCaseCond(const CiSwitch * statement, const CiExpr * value, CiPriority parent) override;
	void writeEnum(const CiEnum * enu) override;
	void writeConst(const CiConst * konst) override;
	void writeField(const CiField * field) override;
	void writeMethod(const CiMethod * method) override;
	void writeClass(const CiClass * klass, const CiProgram * program) override;
	void writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent) override;
private:
	bool hasListInsert;
	bool hasListRemoveAt;
	bool hasQueueDequeue;
	bool hasStackPop;
	bool hasSortedDictionaryInsert;
	bool hasSortedDictionaryFind;
	void writeVisibility(CiVisibility visibility);
	void writeCallType(CiCallType callType, std::string_view sealedString);
	static bool isCreateWithNew(const CiType * type);
	static bool isStructPtr(const CiType * type);
	void writeElementType(const CiType * type);
	void writeStaticInitializer(const CiType * type);
	void writeClassReference(const CiExpr * expr, CiPriority priority = CiPriority::primary);
	void writeWrite(const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine);
	void writeInsertedArg(const CiType * type, const std::vector<std::shared_ptr<CiExpr>> * args, int index = 0);
	static bool isIsComparable(const CiExpr * expr);
	void writeIsVar(const CiExpr * expr, const CiVar * def, CiPriority parent);
	static bool isLong(const CiSymbolReference * expr);
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
};

class GenJava : public GenTyped
{
public:
	GenJava() = default;
	void visitLiteralLong(int64_t value) override;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent) override;
	void visitPostfixExpr(const CiPostfixExpr * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitLambdaExpr(const CiLambdaExpr * expr) override;
	void visitForeach(const CiForeach * statement) override;
	void visitLock(const CiLock * statement) override;
	void visitThrow(const CiThrow * statement) override;
	void visitEnumValue(const CiConst * konst, const CiConst * previous) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	int getLiteralChars() const override;
	void writePrintfWidth(const CiInterpolatedPart * part) override;
	void writeName(const CiSymbol * symbol) override;
	CiId getTypeId(const CiType * type, bool promote) const override;
	void writeType(const CiType * type, bool promote) override;
	void writeNew(const CiReadWriteClassType * klass, CiPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	void writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_) override;
	void writeCoercedLiteral(const CiType * type, const CiExpr * expr) override;
	void writeAnd(const CiBinaryExpr * expr, CiPriority parent) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeCharAt(const CiBinaryExpr * expr) override;
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	bool isPromoted(const CiExpr * expr) const override;
	void writeAssignRight(const CiBinaryExpr * expr) override;
	void writeAssign(const CiBinaryExpr * expr, CiPriority parent) override;
	std::string_view getIsOperator() const override;
	void writeVar(const CiNamedValue * def) override;
	bool hasInitCode(const CiNamedValue * def) const override;
	void writeInitCode(const CiNamedValue * def) override;
	void defineIsVar(const CiBinaryExpr * binary) override;
	void writeAssert(const CiAssert * statement) override;
	void writeSwitchValue(const CiExpr * expr) override;
	void writeSwitchCase(const CiSwitch * statement, const CiCase * kase) override;
	void writeEnum(const CiEnum * enu) override;
	void writeConst(const CiConst * konst) override;
	void writeField(const CiField * field) override;
	void writeMethod(const CiMethod * method) override;
	void writeClass(const CiClass * klass, const CiProgram * program) override;
private:
	int switchCaseDiscards;
	void writeCamelCaseNotKeyword(std::string_view name);
	void writeVisibility(CiVisibility visibility);
	void writeCollectionType(std::string_view name, const CiType * elementType);
	void writeDictType(std::string_view name, const CiClassType * dict);
	void writeJavaType(const CiType * type, bool promote, bool needClass);
	static bool isUnsignedByte(const CiType * type);
	static bool isUnsignedByteIndexing(const CiExpr * expr);
	void writeIndexingInternal(const CiBinaryExpr * expr);
	void writeSByteLiteral(const CiLiteralLong * literal);
	void writeArrayBinarySearchFill(const CiExpr * obj, std::string_view method, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeWrite(const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, bool newLine);
	void writeCompileRegex(const std::vector<std::shared_ptr<CiExpr>> * args, int argIndex);
	bool writeSwitchCaseVar(const CiExpr * expr);
	void createJavaFile(std::string_view className);
	void writeSignature(const CiMethod * method, int paramCount);
	void writeOverloads(const CiMethod * method, int paramCount);
	void writeResources();
};

class GenJsNoModule : public GenBase
{
public:
	GenJsNoModule() = default;
	virtual ~GenJsNoModule() = default;
	void visitAggregateInitializer(const CiAggregateInitializer * expr) override;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void visitLambdaExpr(const CiLambdaExpr * expr) override;
	void visitBreak(const CiBreak * statement) override;
	void visitForeach(const CiForeach * statement) override;
	void visitLock(const CiLock * statement) override;
	void visitSwitch(const CiSwitch * statement) override;
	void visitThrow(const CiThrow * statement) override;
	void visitEnumValue(const CiConst * konst, const CiConst * previous) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	virtual bool isJsPrivate(const CiMember * member) const;
	void writeName(const CiSymbol * symbol) override;
	void writeTypeAndName(const CiNamedValue * value) override;
	void writeArrayElementType(const CiType * type);
	void writeNew(const CiReadWriteClassType * klass, CiPriority parent) override;
	void writeNewWithFields(const CiReadWriteClassType * type, const CiAggregateInitializer * init) override;
	void writeVar(const CiNamedValue * def) override;
	void writeLocalName(const CiSymbol * symbol, CiPriority parent) override;
	void writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent) override;
	void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) override;
	bool hasInitCode(const CiNamedValue * def) const override;
	void writeInitCode(const CiNamedValue * def) override;
	void writeResource(std::string_view name, int length) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeCharAt(const CiBinaryExpr * expr) override;
	void writeBinaryOperand(const CiExpr * expr, CiPriority parent, const CiBinaryExpr * binary) override;
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void writeAssign(const CiBinaryExpr * expr, CiPriority parent) override;
	std::string_view getIsOperator() const override;
	virtual void writeBoolAndOr(const CiBinaryExpr * expr);
	void startTemporaryVar(const CiType * type) override;
	void defineObjectLiteralTemporary(const CiUnaryExpr * expr) override;
	virtual void writeAsType(const CiVar * def);
	void writeAssertCast(const CiBinaryExpr * expr) override;
	void writeAssert(const CiAssert * statement) override;
	void writeSwitchCaseCond(const CiSwitch * statement, const CiExpr * value, CiPriority parent) override;
	void writeIfCaseBody(const std::vector<std::shared_ptr<CiStatement>> * body, bool doWhile, const CiSwitch * statement, const CiCase * kase) override;
	virtual void startContainerType(const CiContainerType * container);
	void writeEnum(const CiEnum * enu) override;
	void writeConst(const CiConst * konst) override;
	void writeField(const CiField * field) override;
	void writeMethod(const CiMethod * method) override;
	void writeConstructor(const CiClass * klass);
	void writeClass(const CiClass * klass, const CiProgram * program) override;
	void writeLib(const std::map<std::string, std::vector<uint8_t>> * resources);
	virtual void writeUseStrict();
private:
	std::vector<const CiSwitch *> switchesWithLabel;
	bool stringWriter = false;
	void writeCamelCaseNotKeyword(std::string_view name);
	void writeInterpolatedLiteral(std::string_view s);
	static bool isIdentifier(std::string_view s);
	void writeNewRegex(const std::vector<std::shared_ptr<CiExpr>> * args, int argIndex);
	static bool hasLong(const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeMathMaxMin(const CiMethod * method, std::string_view name, int op, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeBoolAndOrAssign(const CiBinaryExpr * expr, CiPriority parent);
	void writeIsVar(const CiExpr * expr, const CiVar * def, bool assign, CiPriority parent);
	void writeVarCast(const CiVar * def, const CiExpr * value);
};

class GenJs : public GenJsNoModule
{
public:
	GenJs() = default;
	virtual ~GenJs() = default;
protected:
	void startContainerType(const CiContainerType * container) override;
	void writeUseStrict() override;
};

class GenTs : public GenJs
{
public:
	GenTs() = default;
	const GenTs * withGenFullCode();
	void visitEnumValue(const CiConst * konst, const CiConst * previous) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	bool isJsPrivate(const CiMember * member) const override;
	void writeEnum(const CiEnum * enu) override;
	void writeTypeAndName(const CiNamedValue * value) override;
	void writeAsType(const CiVar * def) override;
	void writeBinaryOperand(const CiExpr * expr, CiPriority parent, const CiBinaryExpr * binary) override;
	void writeEqualOperand(const CiExpr * expr, const CiExpr * other) override;
	void writeBoolAndOr(const CiBinaryExpr * expr) override;
	void defineIsVar(const CiBinaryExpr * binary) override;
	void writeConst(const CiConst * konst) override;
	void writeField(const CiField * field) override;
	void writeMethod(const CiMethod * method) override;
	void writeClass(const CiClass * klass, const CiProgram * program) override;
private:
	const CiSystem * system;
	bool genFullCode = false;
	void writeType(const CiType * type, bool readOnly = false);
	void writeVisibility(CiVisibility visibility);
};

class GenPySwift : public GenBase
{
public:
	virtual ~GenPySwift() = default;
	void visitAggregateInitializer(const CiAggregateInitializer * expr) override;
	void visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent) override;
	void visitPostfixExpr(const CiPostfixExpr * expr, CiPriority parent) override;
	void visitExpr(const CiExpr * statement) override;
	void visitBlock(const CiBlock * statement) override;
	void visitContinue(const CiContinue * statement) override;
	void visitDoWhile(const CiDoWhile * statement) override;
	void visitFor(const CiFor * statement) override;
	void visitIf(const CiIf * statement) override;
	void visitReturn(const CiReturn * statement) override;
	void visitWhile(const CiWhile * statement) override;
protected:
	GenPySwift() = default;
	void writeDocPara(const CiDocPara * para, bool many) override;
	virtual std::string_view getDocBullet() const = 0;
	void writeDocList(const CiDocList * list) override;
	void writeLocalName(const CiSymbol * symbol, CiPriority parent) override;
	virtual std::string_view getReferenceEqOp(bool not_) const = 0;
	void writeEqual(const CiExpr * left, const CiExpr * right, CiPriority parent, bool not_) override;
	virtual void writeExpr(const CiExpr * expr, CiPriority parent);
	void writeListAppend(const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args);
	virtual bool visitPreCall(const CiCallExpr * call);
	bool visitXcrement(const CiExpr * expr, bool postfix, bool write);
	void endStatement() override;
	virtual void openChild() = 0;
	virtual void closeChild() = 0;
	void writeChild(CiStatement * statement) override;
	virtual void writeContinueDoWhile(const CiExpr * cond);
	virtual bool needCondXcrement(const CiLoop * loop);
	virtual std::string_view getIfNot() const = 0;
	virtual void openWhile(const CiLoop * loop);
	virtual void writeForRange(const CiVar * iter, const CiBinaryExpr * cond, int64_t rangeStep) = 0;
	virtual void writeElseIf() = 0;
	virtual void writeResultVar() = 0;
private:
	static bool isPtr(const CiExpr * expr);
	bool openCond(std::string_view statement, const CiExpr * cond, CiPriority parent);
	void endBody(const CiLoop * loop);
	void openWhileTrue();
	void closeWhile(const CiLoop * loop);
};

class GenSwift : public GenPySwift
{
public:
	GenSwift() = default;
	void visitLiteralNull() override;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent) override;
	void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void visitExpr(const CiExpr * statement) override;
	void visitLambdaExpr(const CiLambdaExpr * expr) override;
	void visitBreak(const CiBreak * statement) override;
	void visitDoWhile(const CiDoWhile * statement) override;
	void visitForeach(const CiForeach * statement) override;
	void visitLock(const CiLock * statement) override;
	void visitSwitch(const CiSwitch * statement) override;
	void visitThrow(const CiThrow * statement) override;
	void visitEnumValue(const CiConst * konst, const CiConst * previous) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void startDocLine() override;
	std::string_view getDocBullet() const override;
	void writeDoc(const CiCodeDoc * doc) override;
	void writeName(const CiSymbol * symbol) override;
	void writeLocalName(const CiSymbol * symbol, CiPriority parent) override;
	void writeMemberOp(const CiExpr * left, const CiSymbolReference * symbol) override;
	void writeTypeAndName(const CiNamedValue * value) override;
	void writeCoercedInternal(const CiType * type, const CiExpr * expr, CiPriority parent) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeCharAt(const CiBinaryExpr * expr) override;
	std::string_view getReferenceEqOp(bool not_) const override;
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeNewArrayStorage(const CiArrayStorageType * array) override;
	void writeNew(const CiReadWriteClassType * klass, CiPriority parent) override;
	void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) override;
	void writeIndexingExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void writeBinaryOperand(const CiExpr * expr, CiPriority parent, const CiBinaryExpr * binary) override;
	void writeResource(std::string_view name, int length) override;
	void writeExpr(const CiExpr * expr, CiPriority parent) override;
	void writeCoercedExpr(const CiType * type, const CiExpr * expr) override;
	void startTemporaryVar(const CiType * type) override;
	void openChild() override;
	void closeChild() override;
	void writeVar(const CiNamedValue * def) override;
	void writeStatements(const std::vector<std::shared_ptr<CiStatement>> * statements) override;
	void writeAssertCast(const CiBinaryExpr * expr) override;
	void writeAssert(const CiAssert * statement) override;
	bool needCondXcrement(const CiLoop * loop) override;
	std::string_view getIfNot() const override;
	void writeContinueDoWhile(const CiExpr * cond) override;
	void writeElseIf() override;
	void openWhile(const CiLoop * loop) override;
	void writeForRange(const CiVar * iter, const CiBinaryExpr * cond, int64_t rangeStep) override;
	void writeResultVar() override;
	void writeParameter(const CiVar * param) override;
	void writeEnum(const CiEnum * enu) override;
	void writeConst(const CiConst * konst) override;
	void writeField(const CiField * field) override;
	void writeParameterDoc(const CiVar * param, bool first) override;
	void writeMethod(const CiMethod * method) override;
	void writeClass(const CiClass * klass, const CiProgram * program) override;
private:
	const CiSystem * system;
	bool throw_;
	bool arrayRef;
	bool stringCharAt;
	bool stringIndexOf;
	bool stringSubstring;
	std::vector<std::unordered_set<std::string_view>> varsAtIndent;
	std::vector<bool> varBytesAtIndent;
	void writeCamelCaseNotKeyword(std::string_view name);
	void openIndexing(const CiExpr * collection);
	static bool isArrayRef(const CiArrayStorageType * array);
	void writeClassName(const CiClassType * klass);
	void writeType(const CiType * type);
	void writeUnwrapped(const CiExpr * expr, CiPriority parent, bool substringOk);
	void writeStringContains(const CiExpr * obj, std::string_view name, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeRange(const CiExpr * startIndex, const CiExpr * length);
	bool addVar(std::string_view name);
	void writeDefaultValue(const CiType * type);
	void writeEnumFlagsAnd(const CiExpr * left, std::string_view method, std::string_view notMethod, const CiExpr * right);
	const CiExpr * writeAssignNested(const CiBinaryExpr * expr);
	void writeSwiftAssign(const CiBinaryExpr * expr, const CiExpr * right);
	static bool throws(const CiExpr * expr);
	void initVarsAtIndent();
	static bool needsVarBytes(const std::vector<std::shared_ptr<CiStatement>> * statements);
	void writeSwitchCaseVar(const CiVar * def);
	void writeSwiftSwitchCaseBody(const CiSwitch * statement, const std::vector<std::shared_ptr<CiStatement>> * body);
	void writeReadOnlyParameter(const CiVar * param);
	void writeVisibility(CiVisibility visibility);
	void writeLibrary();
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
};

class GenPy : public GenPySwift
{
public:
	GenPy() = default;
	void visitLiteralNull() override;
	void visitLiteralFalse() override;
	void visitLiteralTrue() override;
	void visitAggregateInitializer(const CiAggregateInitializer * expr) override;
	void visitInterpolatedString(const CiInterpolatedString * expr, CiPriority parent) override;
	void visitPrefixExpr(const CiPrefixExpr * expr, CiPriority parent) override;
	void visitSymbolReference(const CiSymbolReference * expr, CiPriority parent) override;
	void visitBinaryExpr(const CiBinaryExpr * expr, CiPriority parent) override;
	void visitExpr(const CiExpr * statement) override;
	void visitLambdaExpr(const CiLambdaExpr * expr) override;
	void visitBreak(const CiBreak * statement) override;
	void visitForeach(const CiForeach * statement) override;
	void visitLock(const CiLock * statement) override;
	void visitSwitch(const CiSwitch * statement) override;
	void visitThrow(const CiThrow * statement) override;
	void visitEnumValue(const CiConst * konst, const CiConst * previous) override;
	void writeProgram(const CiProgram * program) override;
protected:
	std::string_view getTargetName() const override;
	void writeBanner() override;
	void startDocLine() override;
	std::string_view getDocBullet() const override;
	void writeDoc(const CiCodeDoc * doc) override;
	void writeParameterDoc(const CiVar * param, bool first) override;
	void writeName(const CiSymbol * symbol) override;
	void writeTypeAndName(const CiNamedValue * value) override;
	void writeLocalName(const CiSymbol * symbol, CiPriority parent) override;
	std::string_view getReferenceEqOp(bool not_) const override;
	void writeCharAt(const CiBinaryExpr * expr) override;
	void writeStringLength(const CiExpr * expr) override;
	void writeCoercedSelect(const CiType * type, const CiSelectExpr * expr, CiPriority parent) override;
	void writeNewArray(const CiType * elementType, const CiExpr * lengthExpr, CiPriority parent) override;
	void writeArrayStorageInit(const CiArrayStorageType * array, const CiExpr * value) override;
	void writeNew(const CiReadWriteClassType * klass, CiPriority parent) override;
	void writeCallExpr(const CiExpr * obj, const CiMethod * method, const std::vector<std::shared_ptr<CiExpr>> * args, CiPriority parent) override;
	void writeResource(std::string_view name, int length) override;
	bool visitPreCall(const CiCallExpr * call) override;
	void startTemporaryVar(const CiType * type) override;
	bool hasInitCode(const CiNamedValue * def) const override;
	void startLine() override;
	void openChild() override;
	void closeChild() override;
	void writeAssertCast(const CiBinaryExpr * expr) override;
	void writeAssert(const CiAssert * statement) override;
	std::string_view getIfNot() const override;
	void writeForRange(const CiVar * iter, const CiBinaryExpr * cond, int64_t rangeStep) override;
	void writeElseIf() override;
	void writeResultVar() override;
	void writeEnum(const CiEnum * enu) override;
	void writeConst(const CiConst * konst) override;
	void writeField(const CiField * field) override;
	void writeMethod(const CiMethod * method) override;
	void writeInitField(const CiField * field) override;
	void writeClass(const CiClass * klass, const CiProgram * program) override;
private:
	bool childPass;
	bool switchBreak;
	void startDoc(const CiCodeDoc * doc);
	void writePyDoc(const CiMethod * method);
	void writeNameNotKeyword(std::string_view name);
	static int getArrayCode(const CiType * type);
	void writeDefaultValue(const CiType * type);
	void writePyNewArray(const CiType * elementType, const CiExpr * value, const CiExpr * lengthExpr);
	void writeContains(const CiExpr * haystack, const CiExpr * needle);
	void writeSlice(const CiExpr * startIndex, const CiExpr * length);
	void writeAssignSorted(const CiExpr * obj, std::string_view byteArray);
	void writeAllAny(std::string_view function, const CiExpr * obj, const std::vector<std::shared_ptr<CiExpr>> * args);
	void writePyRegexOptions(const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeRegexSearch(const std::vector<std::shared_ptr<CiExpr>> * args);
	void writeInclusiveLimit(const CiExpr * limit, int increment, std::string_view incrementString);
	void writeSwitchCaseVar(const CiVar * def);
	void writePyCaseBody(const CiSwitch * statement, const std::vector<std::shared_ptr<CiStatement>> * body);
	bool inheritsConstructor(const CiClass * klass) const;
	void writeResourceByte(int b);
	void writeResources(const std::map<std::string, std::vector<uint8_t>> * resources);
};
