// GenJs.cs - JavaScript module code generator
//
// Copyright (C) 2023  Piotr Fusik
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

namespace Foxoft.Ci
{

public class GenJs : GenJsNoModule
{
	protected override void StartContainerType(CiContainerType container)
	{
		base.StartContainerType(container);
		if (container.IsPublic)
			Write("export ");
	}

	protected override void WriteUseStrict()
	{
	}
}

}
