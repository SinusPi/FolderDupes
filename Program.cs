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
				Dupes.CompareMode compareMode = Dupes.CompareMode.Name | Dupes.CompareMode.Size;
				bool verbose = false;
				bool OnlyDupes = false;

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
					}
					else if (args[i] == "-i")
					{
						var fn = args[++i];
						fn = fn.TrimEnd('\\');
						var di = new DirectoryInfo(fn);
						if (!di.Exists) { throw new ArgException("Not found -i: " + fn); }
						includeFolders.Add(di.FullName);
					}
					else if (args[i] == "-f")
					{
						focusFolder = args[++i];
						focusFolder = focusFolder.TrimEnd('\\');
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
				}
				includeFolders = includeFolders.Distinct().ToList();
				excludeFolders = excludeFolders.Distinct().ToList();

				if (includeFolders.Count == 0) throw new ArgException("No include (-i) folders specified. Nothing to do!");
				Console.WriteLine("Include folders:");
				includeFolders.ForEach(f => Console.WriteLine(f));
				Console.WriteLine("Excluded: " + String.Join(", ", excludeFolders.ToArray()));
				if (focusFolder != null) Console.WriteLine("Focus: " + focusFolder);
				Console.WriteLine("Mode: " + compareMode.ToString());
				if (OnlyDupes) Console.WriteLine("Listing only dupes.");

				Dupes.Init(includeFolders.ToArray(), excludeFolders.ToArray());
				
				int prevprogress = 0;
				Dupes.Read((string t,string f)=>
				{
					switch (t)
					{
						case "start": Console.Write("Enumerating " + f + "..."); prevprogress = 0;  break;
						case "end": Console.WriteLine(String.Format("{0,6}",f)); break;
						case "progress": int progress=0; int.TryParse(f, out progress); if (progress - prevprogress > 1000) { prevprogress = progress; Console.Write(String.Format("{0,6}\x8\x8\x8\x8\x8\x8", progress)); }  break;
						case "dirprogress": if (verbose) Console.WriteLine("\n" + f); break;
					}
				}
				);
				Console.WriteLine(Dupes.Files.Count + " files in " + Dupes.Folders.Count + " folders found.");

				//ReadMeta();

				Console.WriteLine("Comparing...");
				var tnow = DateTime.Now;
				Dupes.RunComparison(compareMode, focusFolder, f => { var now = DateTime.Now; if (tnow != null && now.Subtract(tnow).TotalSeconds >= 1) Console.WriteLine("{0}", (int)(f * 100)); tnow = now; });

				if (focusFolder != null)
				{
					var results = new Dictionary<string,string[]> ();
					foreach (var fi in Dupes.Files)
					{
						if (fi.FullName.ToUpperInvariant().StartsWith(focusFolder.ToUpperInvariant() + "\\"))
						{
							var dupes = Dupes.GetDupes(fi)?.FindAll(f=>string.Compare(f.FullName,fi.FullName,true)!=0); // remove same-file instances
							var fn = fi.FullName;
							if (fn.ToUpperInvariant().StartsWith(focusFolder.ToUpperInvariant())) fn = fn.Substring(focusFolder.Length + 1);
							if (dupes == null) dupes = new List<FileInfo>();
							results[fn] = dupes.Select(f => f.FullName).ToArray();
						}
					}

					var names = results.Keys.OrderBy(f => f);
					foreach (var name in names)
					{
						var dupes = results[name];
						if (OnlyDupes && dupes == null || dupes.Count() == 0) continue;
						Console.Write(name + " - ");
						if (dupes == null || dupes.Count() == 0) Console.WriteLine("\x1b[32;1munique\x1b[0m");
						else Console.WriteLine("\x1b[31;1m"+dupes.Count() + "\x1b[0m: " + String.Join(", ", dupes));

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

					if (verbose)
					{
						Console.WriteLine("Uniquity:");
						foreach (var f in folders) Console.WriteLine(" - " + f + " = " + Dupes.FolderUniquities[f].duplicity);
					}

					Console.WriteLine("Duplicated folders:");

					List<string>listDirs(Dictionary<string, int> dir, Dupes.folderUniquity me_uniq)
					{
						var result = new List<string>();
						var k = dir.Keys;
						try
						{
							foreach (string dirName in k)
							{
								//if (verbose) Console.WriteLine("- listing: "+dirName);
								if (!Dupes.FolderUniquities.ContainsKey(dirName)) Console.Error.WriteLine("ERROR: "+dirName+" has no uniquity result!");
								var dupe_uniq = Dupes.FolderUniquities[dirName];
								//if (verbose) Console.WriteLine("- listing...");
								if (!dir.ContainsKey(dirName)) Console.Error.WriteLine("ERROR: WTF? " + dirName + " not present in dir");
								//if (verbose) Console.WriteLine("- contains...");
								result.Add(String.Format("  {0} : \x1b[33;1m{1}\x1b[0m{2}"/*+dupe_uniq.filesHash*/,
									dirName,
									dir[dirName],
									(me_uniq.filesHash == dupe_uniq.filesHash ? " - \x1b[42;37;1m EQUAL \x1b[0m" :
									 (dupe_uniq.allFilesRelative.Count() == dir[dirName] ? " - \x1b[44;37;1m ALL \x1b[0m" :
									 " of "+ dupe_uniq.allFilesRelative.Count()))
									//, String.Join(",", dupe_uniq.allFilesRelative.ToArray())
									));
								//if (verbose) Console.WriteLine("- listed.");
							}
						} catch (Exception e)
						{
							Console.Error.WriteLine("Exceptioned when listing dirs: " + string.Join(",", k));
							throw e;
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
							(fuq.unique>0 ? "\x1b[32;1m":"") + fuq.unique + "\x1b[0m"
							//, String.Join(",", fuq.allFilesRelative.ToArray())
							));
						Console.WriteLine(String.Join("\n",listDirs(fuq.dupeDirs, fuq)));
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
				Console.Error.WriteLine(e.Message);
			}

#if DEBUG
			Console.WriteLine("Program ends.");
			Console.ReadLine();
#endif
		}


	}
}
