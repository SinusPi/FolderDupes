using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FolderDupesDLL
{
	public static class Dupes
	{
		private const int STD_OUTPUT_HANDLE = -11;
		private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
		private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
		[DllImport("kernel32.dll")]
		private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
		
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetStdHandle(int nStdHandle);
		[DllImport("kernel32.dll")]
		private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);


		public static List<FileInfo> Files;
		public static List<DirectoryInfo> Folders;
		public static Regex[] NotPatterns = new Regex[] { };
        public static Dictionary<string, int> Indices;
		public static List<List<FileInfo>> Buckets;
		public static int MaxDepth = 50;
		public static string[] SearchFolders;

		static Dictionary<string, FilesAndDirs> fileInfoCache;
		
		class FilesAndDirs
        {
			public FileInfo[] files;
			public DirectoryInfo[] dirs;
        }
		public static void Init()
		{
			fileInfoCache = new Dictionary<string, FilesAndDirs>();

			Files = new List<FileInfo>();
			Folders = new List<DirectoryInfo>();
			SkipUniquities = new SortedSet<string>();

			var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
			GetConsoleMode(iStdOut, out uint outConsoleMode);
			outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
			SetConsoleMode(iStdOut, outConsoleMode);
		}

		public static void Init(string[] folders, string[] notpatterns = null, int maxdepth = 20)
		{
			Init();

			SetSearchFolders(folders,maxdepth);
			SetNotFolders(notpatterns);
			MaxDepth = maxdepth;
		}

		public static void SetSearchFolders(string[] folders, int maxdepth=50)
        {
			SearchFolders = folders;
			MaxDepth = maxdepth;
        }

		static int depth = 0;
		public static void Read()
        {
			foreach (string f in SearchFolders)
			{
				Console.WriteLine("Enumerating " + f + "...");
				depth = 0;
				ReadFiles(f);
			}
		}

		public static void SetNotFolders(string[] folders)
        {
			NotPatterns = folders.Select(s => new Regex(s,RegexOptions.IgnoreCase)).ToArray();
        }

		static void ReadFiles(string path)
		{
			depth++;
			if (depth > MaxDepth) return;
			if (MatchesNotPattern(path))
			{
				Console.WriteLine("Excluded " + path);
				return;
			}
			
			DirectoryInfo di = new DirectoryInfo(path);

			try
			{
				Folders.Add(di);

				FileInfo[] files = di.GetFiles();
				var goodfiles = files.Where(f => !MatchesNotPattern(f.FullName));

				DirectoryInfo[] directories = di.GetDirectories();
				var gooddirs = directories.Where(d => d.Name!="." && d.Name!=".." && !MatchesNotPattern(d.FullName));
				
				Files.AddRange(goodfiles);

				fileInfoCache[path] = new FilesAndDirs();
				fileInfoCache[path].files = goodfiles.ToArray();
				fileInfoCache[path].dirs = gooddirs.ToArray();

				foreach (var d in gooddirs)
					ReadFiles(d.FullName);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}

			depth--;
		}

		static bool MatchesNotPattern(string s)
        {
			try
			{
				return NotPatterns.First(r => r.IsMatch(s)) != null;
			} catch (Exception e)
			{
				return false;
			}
        }

		public static void ReadMeta()
		{
			return;
			/*
			Sizes = new Dictionary<FileInfo, int>();
			foreach (var f in Files)
			{
				Sizes[f] = System.I
			}
			*/
		}

		static List<FileInfo> GetDupes(FileInfo fi)
		{
			if (Indices.TryGetValue(fi.FullName, out int i))
				return Buckets[i];
			else
				return null;
		}

		static Dictionary<string, int> fingerprints;

		public static void RunComparison(CompareMode mode = CompareMode.Name | CompareMode.Size)
		{
			Indices = new Dictionary<string, int>();
			Buckets = new List<List<FileInfo>>();
			fingerprints = new Dictionary<string, int>();
			for (int i = 0; i < Files.Count; i++)
			{
				var f = Files[i];
				var fprint = (mode.HasFlag(CompareMode.Name) ? f.Name : "") + "::" + (mode.HasFlag(CompareMode.Size) ? f.Length.ToString() : "");
				if (fingerprints.TryGetValue(fprint, out var fpbindex))
				{
					var f1 = Files[fpbindex]; // previous file of same fingerprint
					if (Indices.TryGetValue(f1.FullName, out int bui))
					{
						// there's a bucket already
						Buckets[bui].Add(f);
						Indices[f.FullName] = bui;
					}
					else
					{
						Buckets.Add(new List<FileInfo>(new FileInfo[] { f1, f }));
						Indices[f1.FullName] = Buckets.Count - 1;
						Indices[f.FullName] = Buckets.Count - 1;
					}
				} else {
					fingerprints[fprint] = i; // just note it's there
				}

				if (i % 100 == 0) Console.Write((int)(((float)i / Files.Count) * 100) + "\u001b[9D");
			}
		}

		public static void RunComparison_Old(CompareMode mode = CompareMode.Name | CompareMode.Size)
		{
			Indices = new Dictionary<string, int>();
			Buckets = new List<List<FileInfo>>();
			for (int i = 0; i < Files.Count; i++)
			{
				if (i % 100 == 0) Console.WriteLine((int)(((float)i / Files.Count) * 100));
				for (int j = i + 1; j < Files.Count; j++)
				{
					var f1 = Files[i];
					var f2 = Files[j];
					if ((mode.HasFlag(CompareMode.Name) && f1.Name != f2.Name)) continue;
					if ((mode.HasFlag(CompareMode.Size) && f1.Length != f2.Length)) continue;

					// so f1 and f2 ARE duplicates!

					if (Indices.TryGetValue(f1.FullName, out int i1))
					{
						Buckets[i1].Add(f2);
						Indices[f1.FullName] = i1;
					}
					else
					{
						Buckets.Add(new List<FileInfo>(new FileInfo[] { f1, f2 }));
						Indices[f1.FullName] = Buckets.Count - 1;
						Indices[f2.FullName] = Buckets.Count - 1;
					};
					break;
				}
			}
		}

		[Flags]
		public enum CompareMode
		{
			Name = 0b0001,
			Date = 0b0010,
			Size = 0b0100,
			Hash = 0b1000
		}
		/*

		static void CompareFolders()
		{
			for (int i = 0; i < Folders.Count; i++)
			{
				for (int j = i + 1; j < Folders.Count; j++)
				{
					var d1 = Folders[i];
					var d2 = Folders[j];

					var dup = GetFolderMatch(d1, d2);

					if (dup.Count > 0)
					{
						Console.WriteLine(d1.FullName + " and " + d2.FullName + " : " + dup.Count + " dupes: ");
						foreach (var dli in dup)
						{
							Console.WriteLine(" - " + Buckets[dli][0].Name);
						}
					}
				}
			}
		}

		struct folderMatch
		{
			List<int> dupes;
			int notina, notinb;
		}

		static folderMatch GetFolderMatch(DirectoryInfo d1,DirectoryInfo d2)
		{
			FileInfo[] files1 = GetAllFiles(d1);
			FileInfo[] files2 = GetAllFiles(d2);

			if (files1.Length>files2.Length) { (files2, files1) = (files1, files2); }
			// f1 is now smaller
			List<int> dupes = new List<int>();
			foreach (var f1 in files1)
			{
				if (Indices.TryGetValue(f1.FullName,out int i1)) // f has dupes
				{
					var bu = Buckets[i1];
					foreach (var f2 in bu)
					{
						if (f2.FullName!=f1.FullName && f2.Directory.FullName==d2.FullName) // it has a dupe in d2, so it's a shared dupe
						{
							dupes.Add(i1);
							continue;
						}
					}
				}
			}
			return dupes;
		}
		*/

		public struct folderUniquity
		{
			public int unique;
			public int[] dupes;
			public float duplicity;
            public bool totallyDuped;
			public Dictionary<string,int> dupeDirs;
			public SortedSet<string> allFilesRelative;
            public string filesHash;
        }
		public static Dictionary<string, folderUniquity> FolderUniquities;
		public static Dictionary<string, string> relativeFilesHashes;

		public static void CalculateFolderUniquity()
        {
			FolderUniquities = new Dictionary<string, folderUniquity>();
			relativeFilesHashes = new Dictionary<string, string>();

			foreach (var s in SearchFolders)
            {
				GetFolderUniquity(new DirectoryInfo(s));
            }
        }
		public static folderUniquity GetFolderUniquity(DirectoryInfo di)
		{
			if (FolderUniquities.TryGetValue(di.FullName, out var fu))
				return fu; //fetch

			var uniqity = new folderUniquity();
			FileInfo[] files;
			FilesAndDirs filesdirs;
			if (fileInfoCache.TryGetValue(di.FullName, out filesdirs))
				files = filesdirs.files;
			else
				throw new Exception("Why isn't " + di.FullName + " in fileInfoCache?");
				//else
				//	files = di.GetFiles().Where(f=>!MatchesNotPattern(f.FullName)).ToArray();
			//}
			//catch (Exception e) { Console.Error.WriteLine(e.ToString()); return fu; }
			
			uniqity.unique = 0;
			var dupes = new List<int>();
			foreach (var f in files)
			{
				if (Indices.TryGetValue(f.FullName, out int fi))
					dupes.Add(fi);
				else
					uniqity.unique++;
			}

			uniqity.allFilesRelative = new SortedSet<string>();
			foreach (var f in files) uniqity.allFilesRelative.Add(f.Name);

			// add subfolders
			DirectoryInfo[] dirs = filesdirs.dirs;
			//dirs = di.GetDirectories().Where(d => !MatchesNotPattern(d.FullName)).ToArray();
			foreach (var dir in dirs)
			{
				var du = GetFolderUniquity(dir);
				uniqity.unique += du.unique;
				if (du.dupes!=null) dupes.AddRange(du.dupes);
				
				var relativeToHere = du.allFilesRelative.Select(r => dir.Name + "\\" + r);
				foreach (var f in relativeToHere) uniqity.allFilesRelative.Add(f);
			}
			uniqity.filesHash = GetHash(String.Join("\n", uniqity.allFilesRelative.ToArray()));
			

			// where do the dupes originate from?
			Dictionary<string, int> dupeDirs = new Dictionary<string, int>();
			foreach (var d in dupes)
			{
				var bu = Buckets[d];
				var bui = 0;
				while (bui<bu.Count && bu[bui].DirectoryName.StartsWith(di.FullName)) bui++;
				if (bui >= bu.Count) continue;
				var dirname = bu[bui].DirectoryName;
				if (!dupeDirs.ContainsKey(dirname)) dupeDirs[dirname] = 0;
				dupeDirs[dirname]++;
			}

			uniqity.dupes = dupes.ToArray();
			uniqity.duplicity = (uniqity.dupes.Length + uniqity.unique) >0 ? (float)uniqity.dupes.Length / (uniqity.dupes.Length + uniqity.unique) : 0;
			uniqity.totallyDuped = uniqity.unique == 0 && uniqity.dupes.Length > 0;
			uniqity.dupeDirs = dupeDirs;

			// experimental: forget subfolders if we're totally duped
			if (uniqity.totallyDuped)
            {
				// TODO: store relative full contents, for "100% match" reports

				foreach (var dir in dirs)
                {
					SkipUniquities.Add(dir.FullName);
                }
            }

			FolderUniquities[di.FullName] = uniqity;
			return uniqity;
		}

		public static SortedSet<string> SkipUniquities;

		static Dictionary<string, bool> loopcache;
		static FileInfo[] GetAllFiles(DirectoryInfo di, bool clearLoopCache = true)
		{
			if (clearLoopCache) loopcache = new Dictionary<string, bool>();
			loopcache[di.FullName] = true;
			List<FileInfo> files = new List<FileInfo>();
			files.AddRange(di.GetFiles());
			var dirs = di.GetDirectories();
			foreach (var d in dirs)
			{
				if (d.Name == "." || d.Name == "..") continue;
				files.AddRange(GetAllFiles(d, false));
			}
			return files.ToArray();
		}

		static string GetHash(string s)
        {
			// Convert the input string to a byte array and compute the hash.
			byte[] data = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(s));

			// Create a new Stringbuilder to collect the bytes
			// and create a string.
			var sBuilder = new StringBuilder();

			// Loop through each byte of the hashed data
			// and format each one as a hexadecimal string.
			for (int i = 0; i < data.Length; i++)
			{
				sBuilder.Append(data[i].ToString("x2"));
			}

			// Return the hexadecimal string.
			return sBuilder.ToString();
		}
	}
}
