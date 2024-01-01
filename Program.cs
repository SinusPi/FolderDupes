using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FolderDupesDLL;

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
		//static string[] SearchFolders = { @"C:\FOTO\" };
		//static Regex[] NotPatterns = { new Regex(@"^C:\\FOTO\\LRplugins"), new Regex(@".*lrdata$"), new Regex(@".*lrdata$") };
		static int MinDupes = 5;

		static void Main(string[] args)
		{
			try
			{
				//Dupes.Init(new string[] { @"C:\Dokumenty\" },new string[] { @"^C:\\FOTO\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv" });
				//Dupes.Init(new string[] { @"S:\FOTO\", @"S:\Belly Dance" },new string[] { @"\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv", @"\.picasa\.ini", @"Thumbs\.db" });
				//Dupes.Init(new string[] { @"S:\-SINUS-", @"S:\-AILI-", @"S:\wrzut\", @"S:\Nautilus", @"S:\STRYCH", @"S:\Archiwa WWW" },new string[] { @"\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv", @"\.picasa\.ini", @"Thumbs\.db", @"\.svn" });
				List<String> includeFolders = new List<string>();
				//List<String> includeFolders = new List<string>(new string[] { @"c:\foto" });
				List<String> excludeFolders = new List<string>(new string[] { @"\\LRplugins", @"\.lrdata$", @"\\node_modules", @"\\\.git", @"\.scriv", @"\.picasa\.ini", @"Thumbs\.db", @"\.svn" });
				String focusFolder = null;
				Dupes.CompareMode compareMode = 0;
				for (int i = 0; i < args.Length; i++)
				{
					if (args[i] == "--help")
					{
						Console.WriteLine("-i <folder> - add to included folders");
						Console.WriteLine("-x <folder> - add to excluded folders");
						Console.WriteLine("-f <folder> - focus on folder");
						Console.WriteLine("-m <mode> - compare mode: [dnsc]+ = date, name, size, content; default: 'ns'");
						Console.WriteLine();
						Console.WriteLine("Default excludes: " + String.Join(", ", excludeFolders.ToArray()));
						Console.WriteLine("Use '-x !' to clear default excludes, before adding any new.");
					}
					else if (args[i] == "-i")
						includeFolders.Add(args[++i]);
					else if (args[i] == "-f")
					{
						focusFolder = args[++i];
						var di = new DirectoryInfo(focusFolder);
						if (!di.Exists) throw new ArgException("-f : focus folder '" + focusFolder + "' doesn't exist.");
						includeFolders.Add(focusFolder);
					}
					else if (args[i] == "-x")
					{
						var arg = args[++i];
						if (arg == "!")
							excludeFolders.Clear(); // remove defaults
						else
							excludeFolders.Add(arg);
					}
					else if (args[i]=="-m")
					{
						var arg = args[++i];
						compareMode = 0;
						if (arg.IndexOf("n") >= 0) compareMode |= Dupes.CompareMode.Name;
						if (arg.IndexOf("d") >= 0) compareMode |= Dupes.CompareMode.Date;
						if (arg.IndexOf("s") >= 0) compareMode |= Dupes.CompareMode.Size;
						if (arg.IndexOf("c") >= 0) compareMode |= Dupes.CompareMode.Hash; // c for content
					}
				}
				includeFolders = includeFolders.Distinct().ToList();
				excludeFolders = excludeFolders.Distinct().ToList();

				if (includeFolders.Count == 0) throw new ArgException("No include (-i) folders specified. Nothing to do!");
				Console.WriteLine("Include folders:");
				includeFolders.ForEach(f => Console.WriteLine(f));
				Console.WriteLine("Excluded: " + String.Join(", ", excludeFolders.ToArray()));
				if (focusFolder != null) Console.WriteLine("Focus: " + focusFolder);
				Console.WriteLine("Mode: " + compareMode.ToString());

				Dupes.Init(includeFolders.ToArray(), excludeFolders.ToArray());
				Dupes.Read(f=>Console.WriteLine("Enumerating " + f + "..."));
				Console.WriteLine(Dupes.Files.Count + " files in " + Dupes.Folders.Count + " folders found.");

				//ReadMeta();

				Console.WriteLine("Comparing...");
				var tnow = DateTime.Now;
				Dupes.RunComparison(compareMode, focusFolder, f => { var now = DateTime.Now; if (tnow != null && now.Subtract(tnow).TotalSeconds >= 1) Console.WriteLine("{0}", (int)(f * 100)); tnow = now; });

				if (focusFolder != null)
				{
					foreach (var fi in Dupes.Files)
					{
						if (fi.FullName.StartsWith(focusFolder + "\\"))
						{
							var d = Dupes.GetDupes(fi)?.FindAll(f=>f.FullName!=fi.FullName);
							Console.Write(fi.FullName.Replace(focusFolder+"\\","")+" - ");
							if (d==null || d.Count == 0) Console.WriteLine("unique");
							else Console.WriteLine(d.Count + ": " + String.Join(", ", d.Select(f => f.FullName)));
						}

					}
					//var uniquity = Dupes.GetFolderUniquity(new DirectoryInfo(focusFolder));
					//Console.WriteLine((uniquity.duplicity * 100).ToString("0") + "% duped:\n" + uniquity.dupes.ToString());
					//foreach (var d in uniquity.dupes) Console.WriteLine(uniquity.allFilesRelative[d] +" = " + uniquity.dupeDirs[d]);
					//Console.WriteLine("Unique:");
					//foreach (var s in uniquity.) Console.WriteLine(s);
				}
				else
				{
					// show all folders' uniquities, ordered.
					Console.WriteLine("Finding uniquity...");
					Dupes.CalculateFolderUniquity();

					string[] folders = Dupes.FolderUniquities.Keys.ToArray();
					folders = folders.Where(t => !Dupes.SkipUniquities.Contains(t)).ToArray();
					var ordered = folders.OrderBy(f => Dupes.FolderUniquities[f].duplicity);

					Console.WriteLine("Dupes:");

					string listDirs(Dictionary<string, int> dir, Dupes.folderUniquity me_uniq)
					{
						List<string> result = new List<string>();
						foreach (string dirName in dir.Keys)
						{
							var dupe_uniq = Dupes.FolderUniquities[dirName];
							result.Add(String.Format("  {0} : {1}{2}", dirName, dir[dirName], (me_uniq.unique == dupe_uniq.unique && me_uniq.dupes.Count() == dupe_uniq.dupes.Count()) ? " = EQUAL" : ""));
						}
						return String.Join("\n", result);
					}
					var skips = new Dictionary<string, bool>();
					foreach (var o in ordered)
					{
						var fuq = Dupes.FolderUniquities[o];
						if (fuq.dupes.Length < MinDupes) continue;
						if (skips.ContainsKey(o)) continue;
						if (fuq.totallyDuped && Dupes.FolderUniquities.TryGetValue(o.Remove(o.LastIndexOf('\\')), out var fuq2) && fuq2.totallyDuped) continue; // parent is fully duped, too
						Console.WriteLine(o + " - " + (fuq.duplicity * 100).ToString("0") + "% duped:\n" + listDirs(fuq.dupeDirs, fuq));
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
			} catch (ArgException e) {
				Console.WriteLine(e.Message);
			}
		}


	}
}
