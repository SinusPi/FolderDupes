using System;
using System.Text;
using System.Text.RegularExpressions;

namespace FolderDupesCLI
{
	internal class WrapWriter
	{

		private int WrappedIndent = 10;

		public WrapWriter(int indent)
		{
			WrappedIndent = indent;
		}
		private static readonly Regex AnsiEscapeRegex = new Regex(@"\x1B\[[0-9;]*m", RegexOptions.Compiled);

		private static int VisibleLength(string text)
		{
			return string.IsNullOrEmpty(text) ? 0 : AnsiEscapeRegex.Replace(text, string.Empty).Length;
		}

		private int GetConsoleWidth()
		{
			try
			{
				return Math.Max(WrappedIndent + 1, Console.WindowWidth);
			}
			catch
			{
				return 80;
			}
		}

		public void WriteWrappedLine(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				Console.WriteLine();
				return;
			}

			var width = GetConsoleWidth();
			if (VisibleLength(text) <= width)
			{
				Console.WriteLine(text);
				return;
			}

			var indent = new string(' ', WrappedIndent);
			var line = new StringBuilder();
			int visible = 0;

			for (int i = 0; i < text.Length;)
			{
				if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
				{
					int start = i;
					i += 2;
					while (i < text.Length && text[i] != 'm') i++;
					if (i < text.Length) i++;
					line.Append(text, start, i - start);
					continue;
				}

				if (visible >= width)
				{
					Console.WriteLine(line.ToString());
					line.Clear();
					line.Append(indent);
					visible = WrappedIndent;
				}

				line.Append(text[i]);
				visible++;
				i++;
			}

			if (line.Length > 0)
			{
				Console.WriteLine(line.ToString());
			}
		}
	}
}