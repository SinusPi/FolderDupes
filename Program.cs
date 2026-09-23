using FolderDupesDLL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static FolderDupesDLL.Dupes;

namespace FolderDupesCLI
{
	public class ArgException: Exception
	{
		public ArgException() { }
		public ArgException(string message) : base(message) { }
		public ArgException(string message,Exception innerException) : base(message, innerException) { }
	}

	class Program
	{
		private const int STD_OUTPUT_HANDLE = -11;
		private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll")]
		private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

		//static string[] SearchFolders = { @"C:\FOTO\" };
		//static Regex[] NotPatterns = { new Regex(@"^C:\\FOTO\\LRplugins"), new Regex(@".*lrdata$"), new Regex(@".*lrdata$") };
		static int MinDupes = 5;
		private const int WrappedIndent = 10;
		private static readonly Regex AnsiEscapeRegex = new Regex(@"\x1B\[[0-9;]*m", RegexOptions.Compiled);

		private static void EnableVirtualTerminalProcessing()
		{
			var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
			if (iStdOut == IntPtr.Zero || iStdOut == new IntPtr(-1)) return;

			uint outConsoleMode;
			if (!GetConsoleMode(iStdOut, out outConsoleMode)) return;
			outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
			SetConsoleMode(iStdOut, outConsoleMode);
		}

		private static int GetConsoleWidth()
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

		private static int VisibleLength(string text)
		{
			return string.IsNullOrEmpty(text) ? 0 : AnsiEscapeRegex.Replace(text, string.Empty).Length;
		}

		private static void WriteWrappedLine(string text)
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

		static List<String> includeFolders = new List<string>();
		static List<String> excludeFolders = new List<string>(new string[] { @"\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv", @"\.picasa\.ini", @"Thumbs\.db", @"\.svn" });
		static String focusFolder = null;
		static Dupes.CompareMode compareMode = Dupes.CompareMode.Name | Dupes.CompareMode.Size;
		static bool verbose = false;
		static bool OnlyDupes = false;

		static void Main(string[] args)
		{
			try
			{
				EnableVirtualTerminalProcessing();

				//Dupes.Init(new string[] { @"C:\Dokumenty\" },new string[] { @"^C:\\FOTO\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv" });
				//Dupes.Init(new string[] { @"S:\FOTO\", @"S:\Belly Dance" },new string[] { @"\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv", @"\.picasa\.ini", @"Thumbs\.db" });
				//Dupes.Init(new string[] { @"S:\-SINUS-", @"S:\-AILI-", @"S:\wrzut\", @"S:\Nautilus", @"S:\STRYCH", @"S:\Archiwa WWW" },new string[] { @"\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv", @"\.picasa\.ini", @"Thumbs\.db", @"\.svn" });
				//List<String> includeFolders = new List<string>(new string[] { @"c:\foto" });

				ParseArgs(args);

				includeFolders = includeFolders.Distinct().ToList();
				excludeFolders = excludeFolders.Distinct().ToList();

				if (includeFolders.Count == 0) throw new ArgException("No include (-i) folders specified. Nothing to do!");
				Console.WriteLine("Folders:");
				includeFolders.ForEach(f => Console.WriteLine((f==focusFolder ? "> " : "- ") + f));
				Console.WriteLine("Excluded: " + String.Join(", ", excludeFolders.ToArray()));
				Console.WriteLine("Mode: " + compareMode.ToString());
				if (OnlyDupes) Console.WriteLine("Listing only dupes.");

				Dupes.Init(includeFolders.ToArray(), excludeFolders.ToArray());

				int prevprogress = 0;
				Dupes.Enumerate((string t, string f) =>
				{
					switch (t)
					{
						case "start": Console.Write("Enumerating:       0"); prevprogress = 0; break;
						case "end": Console.WriteLine(String.Format("\x8\x8\x8\x8\x8\x8\x8{0,7}", f)); break;
						case "progress": int progress = 0; int.TryParse(f, out progress); if (progress - prevprogress > 1000) { prevprogress = progress; Console.Write(String.Format("\x8\x8\x8\x8\x8\x8\x8{0,7}", progress)); } break;
						case "dirprogress": if (verbose) Console.WriteLine("\n" + f); break;
					}
				}
				);
				Console.WriteLine("Found " + Dupes.Files.Count + " files in " + Dupes.Folders.Count + " folders.");

				//ReadMeta();

				var tnow = DateTime.MinValue;
				Action<float> percentProgressCallback = (f => {
					var now = DateTime.Now; if (tnow != null && now.Subtract(tnow).TotalSeconds >= 0.5)
					{
						// write int percent padded, move cursor back
						Console.Write("\x8\x8\x8\x8\x8\x8" + String.Format("{0,2}%", (int)(f * 100)) + "...");
						tnow = now;
					}
				});

				Console.Write("Comparing       ");
				Dupes.RunComparison(compareMode, focusFolder, percentProgressCallback);
				Console.WriteLine("\x8\x8\x8\x8\x8\x0008done.   ");

				if (focusFolder != null)
				{
					RunFocused(focusFolder, compareMode, OnlyDupes);

					//var uniquity = Dupes.GetFolderUniquity(new DirectoryInfo(focusFolder));
					//Console.WriteLine((uniquity.duplicity * 100).ToString("0") + "% duped:\n" + uniquity.dupes.ToString());
					//foreach (var d in uniquity.dupes) Console.WriteLine(uniquity.allFilesRelative[d] +" = " + uniquity.dupeDirs[d]);
					//Console.WriteLine("Unique:");
					//foreach (var s in uniquity.) Console.WriteLine(s);
				}
				else
				{
					// show all folders' uniquities, ordered.
					tnow = DateTime.MinValue; // reset timer
					Console.Write("Finding uniquity       ");
					Dupes.CalculateFolderUniquity(percentProgressCallback);
					Console.WriteLine("\x8\x8\x8\x8\x8\x0008done.   ");

					string[] folders = Dupes.FolderUniquities.Keys.ToArray();
					folders = folders.Where(t => !Dupes.SkipUniquities.Contains(t)).ToArray();
					var ordered = folders.OrderBy(f => Dupes.FolderUniquities[f].duplicity * 100000 + Dupes.FolderUniquities[f].dupes.Length);

					if (verbose)
					{
						Console.WriteLine("Uniquity:");
						foreach (var f in folders) Console.WriteLine(" - " + f + " = " + Dupes.FolderUniquities[f].duplicity);
					}

					Console.WriteLine("Duplicated folders:");

					List<string> listDirs(Dictionary<string, int> dir, Dupes.folderUniquity me_uniq)
					{
						var result = new List<string>();
						var k = dir.Keys;
						try
						{
							foreach (string dirName in k)
							{
								//if (verbose) Console.WriteLine("- listing: "+dirName);
								if (!Dupes.FolderUniquities.ContainsKey(dirName)) Console.Error.WriteLine("ERROR: " + dirName + " has no uniquity result!");
								var dupe_uniq = Dupes.FolderUniquities[dirName];
								//if (verbose) Console.WriteLine("- listing...");
								if (!dir.ContainsKey(dirName)) Console.Error.WriteLine("ERROR: WTF? " + dirName + " not present in dir");
								//if (verbose) Console.WriteLine("- contains...");
								result.Add(String.Format("  {0} : \x1b[33;1m{1}\x1b[0m{2}"/*+dupe_uniq.filesHash*/,
									dirName,
									dir[dirName],
									(me_uniq.filesHash == dupe_uniq.filesHash ? " - \x1b[42;37;1m EQUAL \x1b[0m" :
									 (dupe_uniq.allFilesRelative.Count() == dir[dirName] ? " - \x1b[44;37;1m ALL \x1b[0m" :
									 " of " + dupe_uniq.allFilesRelative.Count()))
									//, String.Join(",", dupe_uniq.allFilesRelative.ToArray())
									));
								//if (verbose) Console.WriteLine("- listed.");
							}
						}
						catch (Exception e)
						{
							Console.Error.WriteLine("Exceptioned when listing dirs: " + string.Join(",", k));
							throw;
						}
						return result;
					}
					var skips = new Dictionary<string, bool>();
					foreach (var o in ordered)
					{
						var fuq = Dupes.FolderUniquities[o];
						if (fuq.dupes.Length < MinDupes) continue;
						if (skips.ContainsKey(o)) continue;
						if (fuq.totallyDuped && Dupes.FolderUniquities.TryGetValue(o.Remove(o.LastIndexOf('\\')), out var fuq2) && fuq2.totallyDuped) continue; // parent is fully duped, too
						Console.WriteLine(String.Format("{0} - {1}% duped (\x1b[33;1m{2}\x1b[0m dupes, {3} unique):",
							o,
							(fuq.duplicity * 100).ToString("0"),
							fuq.dupes.Count(),
							(fuq.unique > 0 ? "\x1b[32;1m" : "") + fuq.unique + "\x1b[0m"
							//, String.Join(",", fuq.allFilesRelative.ToArray())
							));
						Console.WriteLine(String.Join("\n", listDirs(fuq.dupeDirs, fuq)));
						Console.WriteLine("");
					}
				}

				//Dupes.CompareFolders();

				/*
				foreach (var bu in Buckets)
				{
					Console.WriteLine(bu[1].FullName + " - " + bu.Count + " dupes");
				}
				*/

				Console.WriteLine("Done.");
			}
			catch (ArgException e)
			{
				Console.Error.WriteLine(e.Message);
			}

#if DEBUG
			Console.WriteLine("Program ends.");
			Console.ReadLine();
#endif
		}

		public static void RunFocused(string focusFolder, Dupes.CompareMode compareMode, bool OnlyDupes)
		{
			var results = new Dictionary<string, string[]>();
			foreach (var fi in Dupes.Files)
			{
				if (fi.FullName.StartsWith(focusFolder + "\\", StringComparison.OrdinalIgnoreCase))
				{
					var dupes = Dupes.GetDupes(fi)?.FindAll(f => string.Compare(f.FullName, fi.FullName, StringComparison.OrdinalIgnoreCase) != 0); // remove same-file instances
					var fn = fi.FullName;
					if (dupes == null) dupes = new List<FileInfo>();
					results[fn] = dupes.Select(f => f.FullName).ToArray();
				}
			}

			int count_unique = 0;
			int count_dupes = 0;
			int count_hash = 0;

			var names = results.Keys.OrderBy(f => f);
			foreach (var fullname in names)
			{
				var dupes = results[fullname];
				var shortname = fullname;
				if (shortname.StartsWith(focusFolder, StringComparison.OrdinalIgnoreCase)) shortname = shortname.Substring(focusFolder.Length + 1); // get relative path

				if (OnlyDupes && (dupes == null || dupes.Count() == 0)) continue;
				if (dupes == null || dupes.Count() == 0)
				{
					if (Dupes.DifferentByHash.Find(f => f.FullName == fullname) != null)
					{
						count_hash++;
						// unique, but hash differs from other file with same name/size/date
						WriteWrappedLine("\x1b[33;1mD\x1b[0m: " + shortname + " \x1b[33;1m(different)\x1b[0m != " + String.Join(", ", Dupes.DifferentByHash.FindAll(f => f.FullName != fullname).Select(f => f.FullName)));
					}
					else
					{
						count_unique++;
						WriteWrappedLine("\x1b[32;1mU\x1b[0m: " + shortname);
					}
				}
				else
				{
					count_dupes++;
					string dupecount = (dupes.Count()+1).ToString(); // show count of all files, including the original
					if (dupes.Count() > 9) dupecount = "+";
					WriteWrappedLine("\x1b[31;1m"+dupecount+"\x1b[0m: " + shortname + "  =  " + String.Join(", ", dupes));
				}
			}

			Console.WriteLine("Unique: " + (count_unique > 0 ? "\x1b[32;1m" + count_unique.ToString() + "\x1b[0m" : "\x1b[33;1m"+count_unique.ToString()) + "\x1b[0m, Dupes: " + (count_dupes > 0 ? "\x1b[31;1m" + count_dupes.ToString() + "\x1b[0m" : count_dupes.ToString()) + ", Different: " + (count_hash > 0 ? "\x1b[33;1m" + count_hash.ToString() + "\x1b[0m" : count_hash.ToString()));
		}

		public static void ParseArgs(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--help")
				{
					Console.WriteLine("-i <folder> - add to included folders");
					Console.WriteLine("-x <folder> - add to excluded folders");
					Console.WriteLine("-f <folder> - focus on folder");
					Console.WriteLine("-m <mode> - compare mode: [dnsce]+ = date, name, size, content, ends; default: 'ns'");
					//Console.WriteLine("-I - interactive mode");
					Console.WriteLine("-v - verbose");
					Console.WriteLine("--only-dupes - skip uniques in -f mode");
					Console.WriteLine();
					Console.WriteLine("Default excludes: " + String.Join(", ", excludeFolders.ToArray()));
					Console.WriteLine("Use '-x !' to clear default excludes, before adding any new.");
					Environment.Exit(0);
				}
				else if (args[i] == "-i")
				{
					var fn = args[++i];
					fn = fn.TrimEnd('\"');
					var di = new DirectoryInfo(fn);
					if (!di.Exists) { throw new ArgException("Not found -i: " + fn); }
					includeFolders.Add(di.FullName);
				}
				else if (args[i] == "-f")
				{
					focusFolder = args[++i];
					focusFolder = Path.GetFullPath(focusFolder.TrimEnd('\"'));
					var di = new DirectoryInfo(focusFolder);
					if (!di.Exists) throw new ArgException("-f : focus folder '" + focusFolder + "' doesn't exist.");
					includeFolders.Add(di.FullName);
				}
				else if (args[i] == "-x")
				{
					var arg = args[++i];
					arg = arg.TrimEnd('\\');
					if (arg == "!")
						excludeFolders.Clear(); // remove defaults
					else
						excludeFolders.Add(arg);
				}
				else if (args[i] == "-m")
				{
					var arg = args[++i];
					compareMode = 0;
					if (arg.IndexOf("n") >= 0) compareMode |= Dupes.CompareMode.Name;
					if (arg.IndexOf("d") >= 0) compareMode |= Dupes.CompareMode.Date;
					if (arg.IndexOf("s") >= 0) compareMode |= Dupes.CompareMode.Size;
					if (arg.IndexOf("c") >= 0) compareMode |= Dupes.CompareMode.Hash; // c for content
					if (arg.IndexOf("e") >= 0) compareMode |= Dupes.CompareMode.Hush; // e for ends
				}
				else if (args[i] == "--only-dupes")
				{
					OnlyDupes = true;
				}
				else if (args[i] == "-v")
				{
					verbose = true;
				}

				includeFolders = includeFolders.Distinct().ToList();
				excludeFolders = excludeFolders.Distinct().ToList();
			}

		}
	}
}
