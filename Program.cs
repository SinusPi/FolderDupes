using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderDupes
{
	class Program
	{
		static string[] Folders = { @"C:\FOTO\LRplugins" };

		static List<FileInfo> Files;
		static void Main(string[] args)
		{
			Files = new List<FileInfo>();
			foreach (string f in Folders) {
				ReadFiles(f);
			}
		}

		static void ReadFiles(string path)
		{
			DirectoryInfo di = new DirectoryInfo(path);

			FileInfo[] files = di.GetFiles("*.*", SearchOption.TopDirectoryOnly);
			Files.AddRange(files);

			DirectoryInfo[] directories = di.GetDirectories("*.*", SearchOption.TopDirectoryOnly);
		}

		static void RunComparison(CompareMode mode = CompareMode.Name | CompareMode.Size)
		{

		}

		[Flags]
		public enum CompareMode {
			Name = 0b0001,
			Date = 0b0010,
			Size = 0b0100,
			Hash = 0b1000
		}
	}
}
