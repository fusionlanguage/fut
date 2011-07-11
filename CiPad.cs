// CiPad.cs - small Ci editor with on-the-fly translation
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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("CiPad")]
[assembly: AssemblyDescription("Ci Editor")]

namespace Foxoft.Ci
{

public class CiPad : Form
{
	readonly CiPadGroup CiGroup;
	readonly CiPadGroup C89Group;
	readonly CiPadGroup C99Group;
	readonly CiPadGroup JavaGroup;
	readonly CiPadGroup CsGroup;
	readonly CiPadGroup JsGroup;
	readonly CiPadGroup AsGroup;
	readonly CiPadGroup DGroup;
	TextBox Messages;

	void InitializeComponent()
	{
		this.ClientSize = new Size(760, 500);
		this.Text = "CiPad";
		this.Messages = new TextBox();
		this.Messages.Multiline = true;
		this.Messages.ReadOnly = true;
		this.Messages.ScrollBars = ScrollBars.Both;
		this.Messages.WordWrap = false;
		this.Controls.Add(this.Messages);
	}

	public CiPad()
	{
		this.SuspendLayout();
		InitializeComponent();
		this.CiGroup = new CiPadGroup(this);
		this.C89Group = new CiPadGroup(this);
		this.C99Group = new CiPadGroup(this);
		this.JavaGroup = new CiPadGroup(this);
		this.CsGroup = new CiPadGroup(this);
		this.JsGroup = new CiPadGroup(this);
		this.AsGroup = new CiPadGroup(this);
		this.DGroup = new CiPadGroup(this);
		this.CiGroup.Set("hello.ci",
@"public class HelloCi
{
	public static bool Differ(string s1, string s2)
	{
		return s1 != s2;
	}

	public static int Div(int a, int b)
	{
		return a / b;
	}

	public static string GetMessage()
	{
		return ""Hello, world!"";
	}

	public static bool IsNullOrEmpty(string s)
	{
		return s == null || s.Length == 0;
	}

	public const string Version = ""0.1"";
}
".Replace("\n", "\r\n"), false);
		Translate();
		this.ResumeLayout();
	}

	protected override void OnLayout(LayoutEventArgs e)
	{
		int cx = ClientRectangle.Width;
		int cy = ClientRectangle.Height;
		this.CiGroup.SetBounds(0, 0, cx / 3, cy / 2);
		this.Messages.SetBounds(0, cy / 2, cx / 3, cy / 6);
		this.C89Group.SetBounds(cx / 3, 0, cx / 3, cy / 3);
		this.C99Group.SetBounds(cx * 2 / 3, 0, cx / 3, cy / 3);
		this.JavaGroup.SetBounds(cx / 3, cy / 3, cx / 3, cy / 3);
		this.CsGroup.SetBounds(cx * 2 / 3, cy / 3, cx / 3, cy / 3);
		this.JsGroup.SetBounds(0, cy * 2 / 3, cx / 3, cy / 3);
		this.AsGroup.SetBounds(cx / 3, cy * 2 / 3, cx / 3, cy / 3);
		this.DGroup.SetBounds(cx * 2 / 3, cy * 2 / 3, cx / 3, cy / 3);
	}

	void Translate()
	{
		try {
			CiParser parser = new CiParser();
			foreach (string content in this.CiGroup.Contents)
				parser.Parse(new StringReader(content));
			CiProgram program = parser.Program;
			CiResolver resolver = new CiResolver();
			resolver.Resolve(program);
			this.C89Group.Load("hello.c", new GenC89(), program);
			this.C99Group.Load("hello99.c", new GenC(), program);
			this.JavaGroup.Load(".", new GenJava(null), program);
			this.CsGroup.Load("hello.cs", new GenCs(null), program);
			this.JsGroup.Load("hello.js", new GenJs(), program);
			this.AsGroup.Load(".", new GenAs(null), program);
			this.DGroup.Load("hello.d", new GenD(), program);
			this.Messages.BackColor = SystemColors.Window;
			this.Messages.Text = "OK";
		}
		catch (Exception ex)
		{
			this.Messages.BackColor = Color.LightCoral;
			this.Messages.Text = ex.Message;
		}
	}

	internal void CiText_TextChanged(object sender, EventArgs e)
	{
		Translate();
	}

	public static void Main(string[] args)
	{
		Application.Run(new CiPad());
	}
}

class CiPadWriter : StringWriter
{
	CiPadGroup Parent;
	string Name;
	public CiPadWriter(CiPadGroup parent, string name)
	{
		this.Parent = parent;
		this.Name = name;
	}
	public override void Close()
	{
		base.Close();
		this.Parent.Set(this.Name, base.ToString(), true);
	}
}

class CiPadGroup
{
	readonly CiPad Form;
	readonly TabControl TabControl = new TabControl();
	HashSet<TabPage> TabsToRemove;

	public CiPadGroup(CiPad form)
	{
		this.Form = form;
		form.Controls.Add(this.TabControl);
	}

	public void SetBounds(int x, int y, int w, int h)
	{
		this.TabControl.SetBounds(x, y, w, h);
	}

	public IEnumerable<string> Contents
	{
		get
		{
			return
				from TabPage page in this.TabControl.TabPages
				select page.Controls[0].Text;
		}
	}

	public void Set(string name, string content, bool readOnly)
	{
		TabPage page = this.TabControl.TabPages[name];
		if (page == null) {
			page = new TabPage();
			page.Name = name;
			page.Text = name;
			TextBox text = new TextBox();
			if (!readOnly) {
				text.AcceptsReturn = true;
				text.AcceptsTab = true;
				text.TextChanged += this.Form.CiText_TextChanged;
			}
			text.Dock = DockStyle.Fill;
			text.Multiline = true;
			text.ReadOnly = readOnly;
			text.ScrollBars = ScrollBars.Both;
			text.TabStop = false;
			text.WordWrap = false;
			page.Controls.Add(text);
			this.TabControl.TabPages.Add(page);
		}
		else if (this.TabsToRemove != null)
			this.TabsToRemove.Remove(page);
		page.Controls[0].Text = content;
	}

	TextWriter CreatePadWriter(string filename)
	{
		return new CiPadWriter(this, filename);
	}

	public void Load(string outputFile, SourceGenerator gen, CiProgram program)
	{
		gen.OutputFile = outputFile;
		gen.CreateTextWriter = this.CreatePadWriter;
		this.TabsToRemove = new HashSet<TabPage>(from TabPage page in this.TabControl.TabPages select page);
		gen.Write(program);
		foreach (TabPage page in this.TabsToRemove)
			this.TabControl.TabPages.Remove(page);
	}
}

}
