using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Ifc;

namespace IfcFind
{
	class Program
	{
		static void Main(string[] args)
		{
			IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
			Console.WriteLine("IfcFind");
			DirectoryInfo d = new DirectoryInfo(@"C:\Data\Ifc\Basics\");
			var opts = new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive };
			var files = new List<string>();
			foreach (var extension in IfcFileInfo.Extensions)
			{
				files.AddRange(d.GetFiles($"*.{extension}", opts).Select(x =>
					BareFolderFileName(x)
					).ToList());
			}
			files = files.Distinct().ToList();

			var changeCount = 0;
			foreach (var file in files)
			{
				var changed = IfcFileInfo.Index(file);
				if (changed)
					changeCount++;
			}
			Console.WriteLine($"Updated {changeCount} files.");
		}

		internal static string BareFolderFileName(FileInfo x)
		{
			return Path.Combine(Path.GetDirectoryName(x.FullName), Path.GetFileNameWithoutExtension(x.FullName));
		}
	}
}
