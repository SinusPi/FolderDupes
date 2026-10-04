using System;
using System.Text;
using System.Text.RegularExpressions;

namespace FolderDupesCLI
{
	public class Progress
	{
		private static readonly Regex AnsiEscapeRegex = new Regex(@"\x1B\[[0-9;]*m", RegexOptions.Compiled);

		private readonly int? barWidth;
		private readonly bool showPercent;
		private readonly bool rawNumeric;
		private readonly int rawWidth;
		private readonly bool showDirectoryProgress;
		private readonly string prefix;
		private readonly TimeSpan minUpdateInterval;
		private DateTime lastUpdate = DateTime.MinValue;
		private int lastVisibleLength;
		private int lastEnumeratedValue;

		public Action<string, string> EnumerateAction { get; private set; }
		public Action<float> ProgressAction { get; private set; }

		public Progress(int? barWidth = null, bool showPercent = false, bool rawNumeric = false, int rawWidth=5, bool showDirectoryProgress = false, int updateIntervalMs = 500, string prefix = null)
		{
			this.barWidth = barWidth;
			this.showPercent = showPercent;
			this.rawNumeric = rawNumeric;
			this.rawWidth = rawWidth;
			this.showDirectoryProgress = showDirectoryProgress;
			this.prefix = prefix ?? string.Empty;
			this.minUpdateInterval = TimeSpan.FromMilliseconds(updateIntervalMs);

			EnumerateAction = HandleEnumerate;
			ProgressAction = HandleProgress;
		}

		private static int VisibleLength(string text)
		{
			return string.IsNullOrEmpty(text) ? 0 : AnsiEscapeRegex.Replace(text, string.Empty).Length;
		}

		private void ClearLine()
		{
			if (lastVisibleLength <= 0) return;
			Console.Write("\r" + new string(' ', lastVisibleLength) + "\r");
			lastVisibleLength = 0;
		}

		private void WriteLine(string text)
		{
			ClearLine();
			Console.Write(text);
			lastVisibleLength = VisibleLength(text);
		}

		private void WriteProgressText(string text)
		{
			ClearLine();
			Console.Write(text);
			lastVisibleLength = VisibleLength(text);
		}

		private void HandleEnumerate(string type, string value)
		{
			switch (type)
			{
				case "start":
					HandleProgress(-1);
					break;
				case "progress":
					int progress;
					if (!int.TryParse(value, out progress)) return;
					HandleProgress(progress);
					break;
				case "dirprogress":
					if (showDirectoryProgress)
					{
						Console.WriteLine();
						Console.WriteLine(value);
						lastVisibleLength = 0;
					}
					break;
				case "end":
					ClearLine();
					break;
			}
		}

		private void HandleProgress(float value)
		{
			if (value < 0f)
			{
				// just initialize, make sure the bar gets updated on the next call
				lastUpdate = DateTime.MinValue;
				return;
			}

			var now = DateTime.Now;
			if (lastUpdate != DateTime.MinValue && now.Subtract(lastUpdate) < minUpdateInterval) return;

			var progress = (int)(value * 100);
			string text = "";
			if (rawNumeric)
			{
				// Display raw numeric value as int with specified widths, space padded
				text += ((int)value).ToString().PadLeft(rawWidth) + " ";
			}
			if (showPercent)
			{
				text += string.Format("{0,3}% ", progress);
			}
			if (barWidth.HasValue && barWidth.Value > 0)
			{
				var filled = Math.Max(0, Math.Min(barWidth.Value, (int)(value * barWidth.Value)));
				var bar = new string('#', filled) + new string('.', barWidth.Value - filled);
				text += string.Format("{0}", bar);
			}

			WriteProgressText(prefix + text);
			lastUpdate = now;
		}
	}
}
