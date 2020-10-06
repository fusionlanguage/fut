// GenTs.cs - TypeScript code generator
//
// Copyright (C) 2011-2020  Piotr Fusik
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

namespace Foxoft.Ci
{
  /**
   * TypeScript code generator
   *
   * At the moment, this only generates TypeScript declarations (.d.ts files) which don't contain method bodies.
   * In the future we could implement full TS source code generation.
   */
  public class GenTs : GenJs
  {

    void Write(CiEnum enu)
    {
      // WARNING: TypeScript enums allow reverse lookup that the Js generator currently
      // doesn't implement
      WriteLine();
      Write(enu.Documentation);
      Write("export enum ");
      Write(enu.Name);
      OpenBlock();
      int i = 0;
      foreach (CiConst konst in enu)
      {
        Write(konst.Documentation);
        WriteUppercaseWithUnderscores(konst.Name);
        Write(" = ");
        if (konst.Value != null)
          konst.Value.Accept(this, CiPriority.Statement);
        else
          Write(i);
        WriteLine(',');
        i++;
      }
      CloseBlock();
    }

    protected override void WriteTypeAndName(CiNamedValue value)
    {
      WriteName(value);
      Write(": ");
      Write(value.Type);
    }

    void Write(CiType type)
    {
      switch (type)
      {
        case null:
          Write("void");
          break;
        case CiNumericType _:
          Write("number");
          break;
        case CiStringType _:
          Write("string");
          break;
        case CiEnum enu:
          Write(enu.Name);
          break;
        case CiListType list:
          Write(list.ElementType);
          Write("[]");
          break;
        case CiSortedDictionaryType dict:
          Write("Record<");
          Write(dict.KeyType);
          Write(", ");
          Write(dict.ValueType);
          Write('>');
          break;
        case CiDictionaryType dict:
          Write("Record<");
          Write(dict.KeyType);
          Write(", ");
          Write(dict.ValueType);
          Write('>');
          break;
        case CiArrayType array:
          Write(array.ElementType);
          Write("[]");
          break;
        default:
          if (type == CiSystem.MatchClass)
          {
            Write("RegExp");
          }
          else
          {
            Write(type.Name);
          }
          break;
      }
    }

    void Write(CiVisibility visibility)
    {
      switch (visibility)
      {
        case CiVisibility.Private:
          Write("private ");
          break;
        case CiVisibility.Internal:
          break;
        case CiVisibility.Protected:
          Write("protected ");
          break;
        case CiVisibility.Public:
          Write("public ");
          break;
      }
    }

    void WriteSignature(CiMethod method, int paramCount)
    {
      WriteDoc(method);
      Write(method.Visibility);
      switch (method.CallType)
      {
        case CiCallType.Static:
          Write("static ");
          break;
        case CiCallType.Virtual:
          break;
        case CiCallType.Abstract:
          Write("abstract ");
          break;
        case CiCallType.Override:
          break;
        case CiCallType.Normal:
          // no final methods in TS
          break;
        case CiCallType.Sealed:
          // no final methods in TS
          break;
        default:
          throw new NotImplementedException(method.CallType.ToString());
      }
      WriteName(method);
      Write('(');
      int i = 0;
      foreach (CiVar param in method.Parameters)
      {
        if (i >= paramCount)
          break;
        if (i > 0)
          Write(", ");
        WriteName(param);
        if (param.Value != null)
          Write('?');
        Write(": ");
        Write(param.Type);
        i++;
      }
      Write("): ");
      Write(method.Type);
      WriteLine(";");
    }

    void WriteConsts(IEnumerable<CiConst> consts)
    {
      foreach (CiConst konst in consts)
      {
        WriteLine();
        Write(konst.Documentation);
        Write(konst.Visibility);
        Write("static readonly ");
        WriteTypeAndName(konst);
        WriteLine(';');
      }
    }

    void Write(CiClass klass)
    {
      Write(klass.Documentation);
      Write("export ");
      switch (klass.CallType)
      {
        case CiCallType.Normal:
          break;
        case CiCallType.Abstract:
          Write("abstract ");
          break;
        case CiCallType.Static:
        case CiCallType.Sealed:
          // there's no final/sealed keyword, but we can accomplish it by marking the constructor private
          break;
        default:
          throw new NotImplementedException(klass.CallType.ToString());
      }
      OpenClass(klass, "", " extends ");

      if (klass.CallType == CiCallType.Static || klass.CallType == CiCallType.Sealed)
      {
        Write("private constructor();");
      }
      else if (klass.Constructor != null && klass.Constructor.Visibility != CiVisibility.Public)
      {
        if (klass.Constructor != null)
        {
          Write(klass.Constructor.Documentation);
          Write(klass.Constructor.Visibility);
        }
        WriteLine("constructor();");
      }

      WriteConsts(klass.Consts);

      foreach (CiField field in klass.Fields)
      {
        Write(field.Visibility);
        WriteTypeAndName(field);
        WriteLine(';');
      }

      foreach (CiMethod method in klass.Methods)
      {
        WriteSignature(method, method.Parameters.Count);
      }

      WriteConsts(klass.ConstArrays);
      CloseBlock();
      WriteLine();
    }


    public override void Write(CiProgram program)
    {
      CreateFile(this.OutputFile);
      foreach (CiEnum enu in program.OfType<CiEnum>())
        Write(enu);
      foreach (CiClass klass in program.OfType<CiClass>()) // TODO: topological sort of class hierarchy
        Write(klass);
      CloseFile();
    }

    // If we later implement full TS source code generation we can implement these methods

    protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent) { throw new NotImplementedException(); }
    protected override void WriteListStorageInit(CiListType list) { throw new NotImplementedException(); }
    protected override void WriteDictionaryStorageInit(CiDictionaryType dict) { throw new NotImplementedException(); }
    protected override void WriteInitCode(CiNamedValue def) { throw new NotImplementedException(); }
    protected override void WriteResource(string name, int length) { throw new NotImplementedException(); }
    protected override void WriteStringLength(CiExpr expr) { throw new NotImplementedException(); }
    protected override void WriteCharAt(CiBinaryExpr expr) { throw new NotImplementedException(); }
    protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiCollection expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiVar expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiLiteral expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiSymbolReference expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiSelectExpr expr, CiPriority parent) { throw new NotImplementedException(); }
    public override CiExpr Visit(CiCallExpr expr, CiPriority parent) { throw new NotImplementedException(); }
    public override void Visit(CiConst statement) { throw new NotImplementedException(); }
    public override void Visit(CiExpr statement) { throw new NotImplementedException(); }
    public override void Visit(CiBlock statement) { throw new NotImplementedException(); }
    public override void Visit(CiAssert statement) { throw new NotImplementedException(); }
    public override void Visit(CiBreak statement) { throw new NotImplementedException(); }
    public override void Visit(CiContinue statement) { throw new NotImplementedException(); }
    public override void Visit(CiDoWhile statement) { throw new NotImplementedException(); }
    public override void Visit(CiFor statement) { throw new NotImplementedException(); }
    public override void Visit(CiForeach statement) { throw new NotImplementedException(); }
    public override void Visit(CiIf statement) { throw new NotImplementedException(); }
    public override void Visit(CiNative statement) { throw new NotImplementedException(); }
    public override void Visit(CiReturn statement) { throw new NotImplementedException(); }
    public override void Visit(CiSwitch statement) { throw new NotImplementedException(); }
    public override void Visit(CiThrow statement) { throw new NotImplementedException(); }
    public override void Visit(CiWhile statement) { throw new NotImplementedException(); }
  }

}