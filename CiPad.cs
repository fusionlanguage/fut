// CiPad.cs - small Ci editor with on-the-fly translation
//
// Copyright (C) 2011-2013  Piotr Fusik
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
		this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
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
	public const int VersionMajor = 0;
	public const int VersionMinor = 3;
	public const string Version = VersionMajor + ""."" + VersionMinor;

	/// Returns `true` if and only if `x` is a power of 2 (1, 2, 4, 8, 16, ...).
	public static bool IsPowerOfTwo(int x)
	{
		return (x & x - 1) == 0 && x > 0;
	}

	/// Calculates greatest common divisor of `a` and `b`.
	public static int GreatestCommonDivisor(int a, int b)
	{
		// Euclidean algorithm
		while (b != 0) {
			int t = b;
			b = a % b;
			a = t;
		}
		return a;
	}

	/// Checks whether the given string is a palindrome.
	/// Note: empty string is considered palindrome.
	public static bool IsPalindrome(string s)
	{
		int j = s.Length;
		for (int i = 0; i < --j; i++)
			if (s[i] != s[j])
				return false;
		return true;
	}

	/// Gets a boolean value out of strings `""true""` and `""false""`.
	/// In other cases returns `defaultValue`.
	public static bool ParseBool(string s, bool defaultValue)
	{
		if (s == ""true"")
			return true;
		if (s == ""false"")
			return false;
		return defaultValue;
	}

	/// Converts an unsigned integer from its decimal representation.
	public static int ParseUnsignedInt(string s)
	{
		if (s == null || s.Length == 0)
			throw ""null or empty argument"";
		int n = s.Length;
		int r = 0;
		for (int i = 0; i < n; i++) {
			int c = s[i];
			if (c < '0' || c > '9')
				throw ""Not a digit"";
			// TODO: detect overflow
			r = r * 10 + c - '0';
		}
		return r;
	}
}
".Replace("\n", "\r\n"), false);
		Translate();
		TextBox ciBox = (TextBox) this.CiGroup.TabPages.First().Controls[0];
		ciBox.Select(0, 0); // don't want all text initially selected
		ciBox.Select(); // focus
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
			foreach (TabPage page in this.CiGroup.TabPages)
				parser.Parse(page.Text, new StringReader(page.Controls[0].Text));
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
		// When editing class name, TabControls for Java and AS get new TabPages and receive focus.
		// Restore focus, so we can continue typing.
		this.ActiveControl = (Control) sender;
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

	public IEnumerable<TabPage> TabPages
	{
		get
		{
			return this.TabControl.TabPages.Cast<TabPage>();
		}
	}

	const int EM_SETTABSTOPS = 0xcb;

	[DllImport("user32.dll")]
	static extern IntPtr SendMessage(IntPtr wnd, uint msg, IntPtr wParam, int[] lParam);

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
			SendMessage(text.Handle, EM_SETTABSTOPS, new IntPtr(1), new int[] { 12 });
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
		this.TabsToRemove = new HashSet<TabPage>(this.TabPages);
		gen.Write(program);
		foreach (TabPage page in this.TabsToRemove)
			this.TabControl.TabPages.Remove(page);
	}
}

}
