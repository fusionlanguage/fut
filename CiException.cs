// CiException.cs - error in a Ci program
//
// Copyright (C) 2014  Piotr Fusik
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

namespace Foxoft.Ci
{

[Serializable]
public class CiException : Exception
{
	public readonly string Filename;
	public readonly int Line;

	public CiException(string filename, int line, string message) : base(message)
	{
		this.Filename = filename;
		this.Line = line;
	}

	public CiException(CiScope scope, string message) : this(scope.Filename, scope.Line, message)
	{
	}

	public CiException(CiScope scope, string format, params object[] args) : this(scope, string.Format(format, args))
	{
	}
}

}
