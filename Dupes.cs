using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FolderDupesDLL
{
	/// <summary>
	/// Exposes folder-scanning and duplicate-detection functionality for the DLL.
	/// The API lets callers initialize search roots, scan files, compare file groups,
	/// and calculate folder uniquity summaries from the collected file set.
	/// </summary>
	public static class Dupes
	{
		public static List<FileInfo> Files = new List<FileInfo>();
		public static List<DirectoryInfo> Folders = new List<DirectoryInfo>();
		public static Regex[] ExcludePatterns = new Regex[0];
		public static Dictionary<string, int> BucketIndices = new Dictionary<string, int>();
		public static List<List<FileInfo>> Buckets = new List<List<FileInfo>>();
		public static List<FileInfo> DifferentByHash = new List<FileInfo>();
		public static Dictionary<string, int> HashDiffBucketIndices = new Dictionary<string, int>();
		public static List<List<FileInfo>> HashDiffBuckets = new List<List<FileInfo>>();
		public static int MaxDepth = 50;
		public static string[] SearchFolders = new string[0];

		/// <summary>
		/// Resets all cached scan state and prepares the DLL for a new comparison run.
		/// </summary>
		public static void Init()
		{
			EnumerationUtility.Reset();

			Files = new List<FileInfo>();
			Folders = new List<DirectoryInfo>();
			SkipUniquities = new SortedSet<string>();
		}

		/// <summary>
		/// Resets state and configures the search folders, exclude patterns, and max recursion depth.
		/// </summary>
		public static void Init(string[] includeFolders, string[] excludePatterns = null, int maxdepth = 20)
		{
			Init();

			SetSearchFolders(includeFolders, maxdepth);
			if (excludePatterns != null) SetExcludePatterns(excludePatterns);
		}

		/// <summary>
		/// Stores the folder roots that will be scanned.
		/// </summary>
		public static void SetSearchFolders(string[] folders, int maxdepth = 50)
		{
			SearchFolders = folders;
			MaxDepth = maxdepth;
		}

		/// <summary>
		/// Converts exclude patterns into case-insensitive regular expressions.
		/// </summary>
		public static void SetExcludePatterns(string[] patterns)
		{
			ExcludePatterns = patterns.Select(s => new Regex(s, RegexOptions.IgnoreCase)).ToArray();
		}


		/// <summary>
		/// Recursively scans each configured search folder and collects files and directories in the Files and Folders lists.
		/// </summary>
		public static void Enumerate(Action<string, string> callback = null)
		{
			EnumerationUtility.Enumerate(SearchFolders, MaxDepth, ExcludePatterns, Files, Folders, callback);
		}

		/// <summary>
		/// Placeholder for metadata loading; currently does nothing.
		/// </summary>
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

		/// <summary>
		/// Returns the duplicate bucket for the given file.
		/// </summary>
		public static List<FileInfo> GetDupes(FileInfo fi)
		{
			return GetDupes(fi.FullName);
		}

		/// <summary>
		/// Returns the duplicate bucket for the file path, if one exists.
		/// </summary>
		public static List<FileInfo> GetDupes(string path)
		{
			if (BucketIndices.TryGetValue(path, out int i))
				return Buckets[i];
			else
				return null;
		}

		/// <summary>
		/// Computes a full MD5 hash for the file contents.
		/// </summary>
		static string CalculateMD5(string filename)
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(filename))
				{
					var hash = md5.ComputeHash(stream);
					return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				}
			}
		}

		/// <summary>
		/// Computes a lightweight MD5 hash from the beginning and end of a file.
		/// </summary>
		static string CalculateCrappyMD5(string filename)
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(filename))
				{
					var buf = new byte[256];
					stream.Read(buf, 0, 128);
					if (stream.Length > 128) stream.Seek(-128, SeekOrigin.End);
					else stream.Seek(0, SeekOrigin.Begin);
					stream.Read(buf, 128, 128);
					var hash = md5.ComputeHash(buf);
					return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				}
			}
		}

		static void CompareIntoBuckets(List<FileInfo> files, CompareMode mode, Dictionary<string, int> bucketIndices, List<List<FileInfo>> buckets, Action<float> progressCallback = null)
		{
			progressCallback?.Invoke(0f);

			/// Store file numbers under "fingerprint" indexing
			var seen_fingerprint = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

			for (int file_id = 0; file_id < files.Count; file_id++)
			{
				var f = files[file_id];
				var fprint = (mode.HasFlag(CompareMode.Name) ? f.Name : "");
				if (mode.HasFlag(CompareMode.Size)) fprint += "::" + f.Length.ToString();
				if (mode.HasFlag(CompareMode.Date)) fprint += "::" + f.LastWriteTime.ToFileTime().ToString();
				if (mode.HasFlag(CompareMode.ExifDate)) {
					var date = ReadEXIF(f,"DateTimeOriginal");
					//Console.WriteLine("EXIF date for " + f.FullName + " is " + (date==null ? "null" : date));
					if (date == null) // skip file
						continue;
					else
						fprint += "::" + date;
				}
				if (mode.HasFlag(CompareMode.Hash)) fprint += "::" + CalculateMD5(f.FullName); // computes full MD5 hash of file contents; this is slow for large files, so use sparingly
				else if (mode.HasFlag(CompareMode.Hush)) fprint += "::" + CalculateCrappyMD5(f.FullName); // computes a "crappy" MD5 hash from the first and last 128 bytes of the file; this is faster but less reliable

				var seen = seen_fingerprint.TryGetValue(fprint, out var orig_id);
				if (!seen)
				{
					// first time seeing this fingerprint, so just note it, don't start a bucket yet
					seen_fingerprint[fprint] = file_id;
				}
				else
				{
					var orig_f = files[orig_id]; // previous file of same fingerprint; "original"
					var has_bucket = bucketIndices.TryGetValue(orig_f.FullName, out int bucket_index);
					if (!has_bucket)
					{
						// retroactively create a bucket for the original file, since it now has a dupe
						buckets.Add(new List<FileInfo>(new FileInfo[] { orig_f }));
						bucket_index = buckets.Count - 1; // index of the new bucket
						bucketIndices[orig_f.FullName] = bucket_index;
					}

					buckets[bucket_index].Add(f);
					bucketIndices[f.FullName] = bucket_index;
				}

				progressCallback?.Invoke((float)file_id / files.Count);

				//if (i % 100 == 0) Console.Write((int)(((float)i / Files.Count) * 100) + "\u001b[9D");
			}

		}

		/// <summary>
		/// Groups scanned Files into duplicate Buckets using the selected comparison mode.
		/// </summary>
		public static void RunComparison(CompareMode mode = CompareMode.Name | CompareMode.Size, string focusFolder = null, Action<float> progressCallback = null)
		{
			BucketIndices = new Dictionary<string, int>();
			Buckets = new List<List<FileInfo>>();

			CompareMode content_flags = 0;
			if (focusFolder!=null) content_flags = (mode & (CompareMode.Hash | CompareMode.ExifDate)); // leave content-based flags for second pass if focusFolder is specified
			if (content_flags == mode) content_flags = 0; // buuut if the mode is ONLY content-based, then we don't need to do a second pass, so just do it all in one pass

			CompareIntoBuckets(Files, mode & ~content_flags, BucketIndices, Buckets, progressCallback);

			if (content_flags != 0)
			{
				// now re-run the comparison for the buckets that have more than one file, but only for files in the focusFolder
				var focusDir = new DirectoryInfo(focusFolder);
				var focusFiles = Files.Where(f => f.Directory.FullName.StartsWith(focusDir.FullName)).ToList();
				// add all files in the same bucket as any of the focusFiles, so we can compare them all together
				var dupes = focusFiles.SelectMany(f => GetDupes(f) ?? new List<FileInfo>()).Distinct();
				focusFiles = dupes.ToList();
				var oldBucketIndices = BucketIndices;
				var oldBuckets = Buckets;
				BucketIndices = new Dictionary<string, int>();
				Buckets = new List<List<FileInfo>>();
				if (focusFiles.Count > 0)
				{
					CompareIntoBuckets(focusFiles, mode, BucketIndices, Buckets, progressCallback);
				}
				// find files that no longer have a bucket, and mark them in new array as their name/size matches but hash does not match
				HashDiffBucketIndices = new Dictionary<string, int>();
				HashDiffBuckets = new List<List<FileInfo>>();
				foreach (var f in focusFiles.Where(f => oldBucketIndices.ContainsKey(f.FullName) && !BucketIndices.ContainsKey(f.FullName)))
				{
					// add f to HashDiffBuckets
					HashDiffBuckets.Add(oldBuckets[oldBucketIndices[f.FullName]]);
					HashDiffBucketIndices[f.FullName] = HashDiffBuckets.Count - 1;
				}
			}
		}

		[Obsolete("This method is deprecated. Use RunComparison instead.")]
		[Description("Runs the older O(n²) duplicate comparison algorithm.")]
		public static void RunComparison_Old(CompareMode mode = CompareMode.Name | CompareMode.Size)
		{
			BucketIndices = new Dictionary<string, int>();
			Buckets = new List<List<FileInfo>>();
			for (int i = 0; i < Files.Count; i++)
			{
				//if (i % 100 == 0) Console.WriteLine((int)(((float)i / Files.Count) * 100));
				for (int j = i + 1; j < Files.Count; j++)
				{
					var f1 = Files[i];
					var f2 = Files[j];
					if ((mode.HasFlag(CompareMode.Name) && f1.Name != f2.Name)) continue;
					if ((mode.HasFlag(CompareMode.Size) && f1.Length != f2.Length)) continue;

					// so f1 and f2 ARE duplicates!

					if (BucketIndices.TryGetValue(f1.FullName, out int i1))
					{
						Buckets[i1].Add(f2);
						BucketIndices[f1.FullName] = i1;
					}
					else
					{
						Buckets.Add(new List<FileInfo>(new FileInfo[] { f1, f2 }));
						BucketIndices[f1.FullName] = Buckets.Count - 1;
						BucketIndices[f2.FullName] = Buckets.Count - 1;
					}
					;
					break;
				}
			}
		}


		[Flags]
		public enum CompareMode
		{
			Name = 0b000001,
			Date = 0b000010,
			Size = 0b000100,
			Hash = 0b001000,
			Hush = 0b010000,
			ExifDate = 0b100000
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
			public Dictionary<string, int> dupeDirs;
			public SortedSet<string> allFilesRelative;
			public string filesHash;
		}
		public static Dictionary<string, folderUniquity> FolderUniquities = null;
		public static Dictionary<string, string> relativeFilesHashes;

		public static void CalculateFolderUniquity(Action<float> callback = null)
		{
			FolderUniquities = new Dictionary<string, folderUniquity>();
			relativeFilesHashes = new Dictionary<string, string>();

			/*
			foreach (var s in SearchFolders)
            {
				var di = new DirectoryInfo(s);
				FolderUniquities[di.FullName] = GetFolderUniquity(di);
            }
			// What? This calc uniquities only for directly specified folders, makes no sense
			*/
			callback?.Invoke((float)FolderUniquities.Count / Folders.Count);
			foreach (var di in Folders)
			{
				FolderUniquities[di.FullName] = GetFolderUniquity(di);
				callback?.Invoke((float)FolderUniquities.Count / Folders.Count);
			}

		}
		/// <summary>
		/// Calculates the uniquity details for a single directory.
		/// </summary>
		public static folderUniquity GetFolderUniquity(DirectoryInfo di)
		{
			if (FolderUniquities != null && FolderUniquities.TryGetValue(di.FullName, out var fu)) //maybe cached already
				return fu; //fetch

			var uniqity = new folderUniquity();
			FileInfo[] files;
			DirectoryInfo[] dirs;
			if (EnumerationUtility.TryGetCachedContents(di.FullName, out files, out dirs))
			{
				// cache hit; files and dirs are already populated above
			}
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
				var is_dupe = BucketIndices.TryGetValue(f.FullName, out int bucket_id);
				if (is_dupe)
					dupes.Add(bucket_id);
				else
					uniqity.unique++;
			}

			uniqity.allFilesRelative = new SortedSet<string>();
			foreach (var f in files) uniqity.allFilesRelative.Add(f.Name + "::" + f.Length);

			// add subfolders
			//dirs = di.GetDirectories().Where(d => !DoesMatchExcludePatterns(d.FullName)).ToArray();
			foreach (var dir in dirs)
			{
				var subdir_uniquity = GetFolderUniquity(dir);
				uniqity.unique += subdir_uniquity.unique;
				if (subdir_uniquity.dupes != null) dupes.AddRange(subdir_uniquity.dupes);

				var relativeToHere = subdir_uniquity.allFilesRelative.Select(r => dir.Name + "\\" + r);
				foreach (var f in relativeToHere) uniqity.allFilesRelative.Add(f);
			}
			uniqity.filesHash = GetHash(String.Join("\n", uniqity.allFilesRelative.ToArray()));


			// where do the dupes originate from?
			Dictionary<string, int> dupeDirs = new Dictionary<string, int>();
			foreach (var d in dupes)
			{
				var bucket = Buckets[d];

				/*
				while (bui<bu.Count && bu[bui].DirectoryName.StartsWith(di.FullName)) bui++;
				if (bui >= bu.Count) continue;
				var dirname = bu[bui].DirectoryName;
				if (!dupeDirs.ContainsKey(dirname)) dupeDirs[dirname] = 0;
				dupeDirs[dirname]++;
				*/
				// count ALL dupes, not just the first one
				foreach (var dupdir in bucket)
				{
					var dirname = dupdir.DirectoryName;
					if (dirname == di.FullName) continue;
					if (!dupeDirs.ContainsKey(dirname)) dupeDirs[dirname] = 0;
					dupeDirs[dirname]++;
				}
			}

			uniqity.dupes = dupes.ToArray();
			uniqity.duplicity = (uniqity.dupes.Length + uniqity.unique) > 0 ? (float)uniqity.dupes.Length / (uniqity.dupes.Length + uniqity.unique) : 0;
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

			return uniqity;
		}

		public static SortedSet<string> SkipUniquities;

		static Dictionary<string, bool> loopcache;
		private static CompareMode Mode;

		/// <summary>
		/// Returns all files contained in a directory tree.
		/// </summary>
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

		/// <summary>
		/// Computes an MD5 hash for the supplied string.
		/// </summary>
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

		public enum CompareResult
		{
			Unique,
			Dupe,
			Different
		}
		public class FileResult
		{
			public string path;
			public CompareResult dupeState;
			public string[] dupePaths;
		}


		public static IEnumerable<FileResult> IterFileUniquities(IEnumerable<string[]> filegroups, string focusFolder, bool OnlyDupes, int maxlen)
		{
			foreach (var filegroup in filegroups)
			{
				var fullname = filegroup[0];
				var dupes = filegroup.Skip(1).ToArray();

				var isUnique = dupes.Length == 0;
				var isDiffContent = Dupes.HashDiffBucketIndices.TryGetValue(fullname, out var hash_index);

				if (OnlyDupes && (isUnique && !isDiffContent)) continue;


				if (isUnique)
				{
					if (isDiffContent)
					{
						// unique BUT hash differs from other file with same name/size/date, likely a modified/broken file. Show it as a "different" file.
						yield return new FileResult { path = fullname, dupeState = CompareResult.Different, dupePaths = HashDiffBuckets[hash_index].Where(f=>String.Compare(f.FullName,fullname, StringComparison.OrdinalIgnoreCase) != 0).Select(f => f.FullName).ToArray() };
					}
					else
					{
						yield return new FileResult { path = fullname, dupeState = CompareResult.Unique, dupePaths = null };
					}
				}
				else
				{
					yield return new FileResult { path = fullname, dupeState = CompareResult.Dupe, dupePaths = dupes };
				}
			}
		}

		/**
		 * Filter through all files in known Files, matching the specified folder, optionally including subfolders.
		 * @param folder The folder to filter through.
		 * @param includeSubfolders Whether to include subfolders.
		 * @return An enumerable of FileInfo objects representing the files in the folder.
		 */
		public static IEnumerable<FileInfo> IterFilesInFolder(string folder, bool includeSubfolders = true)
		{
			foreach (var f in Files)
			{
				if (f.FullName.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase))
				{
					if (includeSubfolders || f.Directory.FullName.Equals(folder, StringComparison.OrdinalIgnoreCase))
					{
						yield return f;
					}
				}
			}
		}

		public static IEnumerable<string[]> IterDupesInFolder(string focusFolder)
		{
			foreach (var fi in IterFilesInFolder(focusFolder))
			{
				// get all dupes of this file, with the original file at the front of the list
				var dupes = (GetDupes(fi) ?? new List<FileInfo>()).Select(f => f.FullName).ToList().FindAll(f => string.Compare(f, fi.FullName, StringComparison.OrdinalIgnoreCase) != 0); // remove same-file instances
				dupes.Insert(0, fi.FullName); // bring the original file to the front of the list
				yield return dupes.ToArray();
			}
		}

		public static string ReadEXIF (FileInfo fi, string tag)
		{
			try
			{
				int EXIF_ID;
				if (tag == "DateTimeOriginal") EXIF_ID = 0x9003;
				else if (tag == "DateTimeDigitized") EXIF_ID = 0x9004;
				else if (tag == "DateTime") EXIF_ID = 0x0132;
				else return null;

				using (var fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var br = new BinaryReader(fs))
				using (var er = new ExifReader(br))
				{
					return er.ReadExifTag(EXIF_ID);
				}
			}
			catch { }
			return null;
		}

	}
}
