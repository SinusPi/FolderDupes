using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FolderDupesDLL
{
	internal static class EnumerationUtility
	{
		private class FilesAndDirs
		{
			public FileInfo[] files;
			public DirectoryInfo[] dirs;
		}

		private static Dictionary<string, FilesAndDirs> fileInfoCache = new Dictionary<string, FilesAndDirs>();

		public static void Reset()
		{
			fileInfoCache = new Dictionary<string, FilesAndDirs>();
		}

		public static void Enumerate(string[] searchFolders, int maxDepth, Regex[] excludePatterns, List<FileInfo> files, List<DirectoryInfo> folders, Action<string, string> callback = null)
		{
			int readcounter = 0;
			if (callback != null) callback("start", readcounter.ToString());
			foreach (string f in searchFolders)
			{
				EnumerateFolder(f, maxDepth, excludePatterns, files, folders, callback, ref readcounter, 0);
			}
			if (callback != null) callback("end", readcounter.ToString());
		}

		public static bool TryGetCachedContents(string fullPath, out FileInfo[] files, out DirectoryInfo[] dirs)
		{
			FilesAndDirs cached;
			if (fileInfoCache.TryGetValue(fullPath, out cached))
			{
				files = cached.files;
				dirs = cached.dirs;
				return true;
			}

			files = null;
			dirs = null;
			return false;
		}

		private static void EnumerateFolder(string path, int maxDepth, Regex[] excludePatterns, List<FileInfo> files, List<DirectoryInfo> folders, Action<string, string> callback, ref int readcounter, int depth)
		{
			depth++;
			if (depth > maxDepth) return;
			if (DoesMatchExcludePatterns(path, excludePatterns)) return;

			try
			{
				if (callback != null) callback("dirprogress", path);

				DirectoryInfo di = new DirectoryInfo(path);
				var fullpath = di.FullName;

				if (folders.Find(f => String.Compare(f.FullName, fullpath, true) == 0) != null) return;

				folders.Add(di);

				FileInfo[] dirFiles = di.GetFiles();
				var goodfiles = dirFiles.Where(f => !DoesMatchExcludePatterns(f.FullName, excludePatterns)).ToArray();
				readcounter += goodfiles.Length;
				files.AddRange(goodfiles);

				DirectoryInfo[] directories = di.GetDirectories();
				var gooddirs = directories.Where(d => d.Name != "." && d.Name != ".." && !DoesMatchExcludePatterns(d.FullName, excludePatterns)).ToArray();

				if (callback != null) callback("progress", readcounter.ToString());

				fileInfoCache[fullpath] = new FilesAndDirs();
				fileInfoCache[fullpath].files = goodfiles;
				fileInfoCache[fullpath].dirs = gooddirs;

				foreach (var d in gooddirs)
					EnumerateFolder(d.FullName, maxDepth, excludePatterns, files, folders, callback, ref readcounter, depth);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		private static bool DoesMatchExcludePatterns(string value, Regex[] excludePatterns)
		{
			if (excludePatterns == null || excludePatterns.Length == 0) return false;
			try
			{
				return excludePatterns.First(r => r.IsMatch(value)) != null;
			}
			catch
			{
				return false;
			}
		}
	}
}