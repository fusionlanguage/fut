// SymbolTable.cs - symbol table
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

using System.Collections.Generic;

namespace Foxoft.Ci
{

public class SymbolTable
{
	public SymbolTable Parent;
	readonly Dictionary<string, CiSymbol> Dict = new Dictionary<string, CiSymbol>();

	public void Add(CiSymbol symbol)
	{
		string name = symbol.Name;
		for (SymbolTable t = this; t != null; t = t.Parent)
			if (t.Dict.ContainsKey(name))
				throw new ParseException("Symbol {0} already defined", name);
		this.Dict.Add(name, symbol);
	}

	public CiSymbol Lookup(string name)
	{
		for (SymbolTable t = this; t != null; t = t.Parent) {
			CiSymbol result;
			if (t.Dict.TryGetValue(name, out result))
				return result;
		}
		throw new ParseException("Unknown symbol {0}", name);
	}
}

}
