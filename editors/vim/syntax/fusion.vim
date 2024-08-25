" Vim syntax file
" Language:     Fusion
" Maintainer:   Piotr Fusik <piotr@fusion-lang.org>
" Filenames:    *.fu
" URL:          https://github.com/fusionlanguage/fut

if exists('b:current_syntax')
  finish
endif

syn keyword	fusionTodo	contained TODO FIXME XXX
syn match	fusionLineComment	"//.*$" contains=fusionTodo,@Spell
syn region	fusionBlockComment	start="/\*" end="\*/" contains=fusionTodo,@Spell
syn cluster	fusionComment	contains=fusionLineComment,fusionBlockComment

syn region	fusionPreCondit	start="^#\%(if\|elif\|else\|endif\)\>" end="$" contains=fusionLineComment keepend

syn keyword	fusionAccessModifier	internal protected public
syn keyword	fusionModifier	abstract const override sealed static throws virtual
syn keyword	fusionContainer	class enum
syn keyword	fusionType	bool byte double float int long nint short string uint ushort void

syn keyword	fusionConditional	else if switch
syn keyword	fusionRepeat	do for foreach while
syn keyword	fusionException	throw
syn keyword	fusionStatement	assert break continue lock native return
syn keyword	fusionLabel	case default

syn keyword	fusionOperator	in is new resource when
syn keyword	fusionAccess	base this

syn keyword	fusionBoolean	false true
syn keyword	fusionNull	null

syn match	fusionInteger	"\<\d\+\%(_\+\d\+\)*\>" display
syn match	fusionInteger	"\<0x[[:xdigit:]_]*\x\>" display
syn match	fusionReal	"\<\d\+\%(_\+\d\+\)*\.\d\+\%(_\+\d\+\)*\%([Ee][-+]\=\d\+\%(_\+\d\+\)*\)\=" display
syn match	fusionReal	"\<\d\+\%(_\+\d\+\)*[Ee][-+]\=\d\+\%(_\+\d\+\)*\>" display
syn cluster	fusionNumber	contains=fusionInteger,fusionReal

syn match	fusionSpecialError	"\\." contained
syn match	fusionSpecialCharError	"[^']" contained
syn match	fusionSpecialChar	+\\["\\'nrt]+ contained display
syn match	fusionCharacter	"'[^']*'" contains=fusionSpecialChar,fusionSpecialCharError display
syn match	fusionCharacter	"'\\''" contains=fusionSpecialChar display
syn match	fusionCharacter	"'[^\\]'" display
syn region	fusionString	matchgroup=fusionQuote start=+"+ end=+"+ end=+$+ extend contains=fusionSpecialChar,fusionSpecialError,@Spell

syn region	fusionInterpolation	matchgroup=fusionInterpolationDelimiter start=+{+ end=+}+ keepend contained contains=@fusionAll,fusionBraced,fusionBracketed,fusionInterpolationAlign,fusionInterpolationFormat
syn match	fusionEscapedInterpolation	"{{" transparent contains=NONE display
syn match	fusionEscapedInterpolation	"}}" transparent contains=NONE display
syn region	fusionInterpolationAlign	matchgroup=fusionInterpolationAlignDel start=+,+ end=+}+ end=+:+me=e-1 contained contains=@fusionNumber,fusionBoolean,fusionCharacter,fusionParens,fusionString,fusionBracketed display
syn match	fusionInterpolationFormat	+:[^}]\+}+ contained contains=fusionInterpolationFormatDel display
syn match	fusionInterpolationAlignDel	+,+ contained display
syn match	fusionInterpolationFormatDel	+:+ contained display
syn region	fusionInterpolatedString	matchgroup=fusionQuote start=+\$"+ end=+"+ extend contains=fusionInterpolation,fusionEscapedInterpolation,fusionSpecialChar,fusionSpecialError,@Spell

syn cluster	fusionString	contains=fusionString,fusionInterpolatedString
syn cluster	fusionLiteral	contains=fusionBoolean,fusionNull,@fusionNumber,fusionCharacter,@fusionString

syn match	fusionParens	"[()]" display
syn region	fusionBracketed	matchgroup=fusionParens start=+(+ end=+)+ extend contained transparent contains=@fusionAll,fusionBraced,fusionBracketed
syn region	fusionBraced	matchgroup=fusionParens start=+{+ end=+}+ extend contained transparent contains=@fusionAll,fusionBraced,fusionBracketed

syn cluster	fusionAll	contains=@fusionLiteral,@fusionComment,fuLabel,fusionParens,fusionPreCondit,fusionType

" The default highlighting.

hi def link	fusionTodo	Todo
hi def link	fusionLineComment	Comment
hi def link	fusionBlockComment	Comment

hi def link	fusionPreCondit	PreCondit

hi def link	fusionModifier	StorageClass
hi def link	fusionAccessModifier	fusionModifier
hi def link	fusionContainer	Structure
hi def link	fusionType	Type

hi def link	fusionConditional	Conditional
hi def link	fusionRepeat	Repeat
hi def link	fusionException	Exception
hi def link	fusionStatement	Statement
hi def link	fusionLabel	Label

hi def link	fusionOperator	Operator
hi def link	fusionAccess	Keyword

hi def link	fusionBoolean	Boolean
hi def link	fusionNull	Constant

hi def link	fusionInteger	Number
hi def link	fusionReal	Float

hi def link	fusionSpecialError	Error
hi def link	fusionSpecialCharError	Error
hi def link	fusionSpecialChar	SpecialChar
hi def link	fusionCharacter	Character
hi def link	fusionString	String
hi def link	fusionQuote	String

hi def link	fusionInterpolationDelimiter	Delimiter
hi def link	fusionInterpolationAlignDel	fusionInterpolationDelimiter
hi def link	fusionInterpolationFormat	fusionInterpolationDelimiter
hi def link	fusionInterpolationFormatDel	fusionInterpolationDelimiter
hi def link	fusionInterpolatedString	String

let b:current_syntax = 'fusion'

" vim: vts=16,32
