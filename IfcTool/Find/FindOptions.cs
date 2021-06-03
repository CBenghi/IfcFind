using CommandLine;
using IfcTool.Find;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Ifc;

namespace IfcTool
{
	enum schema
	{
		any,
		ifc2x3,
		ifc4
	}


	[Verb("find", HelpText = "cache folder's ifc properties and perform quick search of large archives.")]
	internal class FindOptions
	{
		[Option('d', "directory", Required = true, HelpText = "IFC archive folder to search.")]
		public IEnumerable<string> SearchDir { get; set; }

		[Option('e', "errors", Required = false, HelpText = "Files that could not be parsed.")]
		public bool Error { get; set; }

		[Option('s', "schema", Required = false, Default = schema.any, HelpText = "Limits the search by schema version.")]
		public schema Schema { get; set; }

		[Option('c', "classes", Required = false, HelpText = "returns file with all specified classes.")]
		public IEnumerable<string> Classes { get; set; }

		[Option('p', "classPatterns", Required = false, HelpText = "returns file with partial class match (includes).")]
		public IEnumerable<string> PartClasses { get; set; }

		internal static string BareFolderFileName(FileInfo x)
		{
			return Path.Combine(Path.GetDirectoryName(x.FullName), Path.GetFileNameWithoutExtension(x.FullName));
		}

		internal static Status Run(FindOptions opts)
		{
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

			IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
			Console.WriteLine("Executing Find.");
			var foundCount = 0;
			var filesCount = 0;
			foreach (var dirString in opts.SearchDir)
			{
				DirectoryInfo d = new DirectoryInfo(dirString);
				if (!d.Exists)
				{
					Console.WriteLine($"Directory {dirString} not found.");
					continue;
				}
				var fopts = new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive };
				var files = new List<string>();
				foreach (var extension in IfcFileInfo.Extensions)
				{
					files.AddRange(d.GetFiles($"*.{extension}", fopts).Select(x =>
						BareFolderFileName(x)
						).ToList());
				}
				files = files.Distinct().ToList();

				// first we check the archive to make sure info is cached
				var changeCount = 0;
				foreach (var file in files)
				{
					last = file;
					var changed = IfcFileInfo.Index(file);
					if (changed)
						changeCount++;
				}
				Console.WriteLine($"Updated {changeCount} files.");

				// now execute search
				List<IFindRequirement> reqs = new List<IFindRequirement>();
				reqs.Add(new FindSchemaVersionRequirement(opts.Schema));
				foreach (var cl in opts?.Classes)
				{
					reqs.Add(new FindExactClassRequirement(cl));
				}
				foreach (var cl in opts?.PartClasses)
				{
					reqs.Add(new FindPartClassRequirement(cl));
				}
				if (opts.Error)
				{
					reqs.Add(new FindErrorRequirement());
				}


				var Matching = GetMatching(files, reqs);

				
				foreach (var m in Matching)
				{
					foundCount++;
					Console.WriteLine($"found: {m.BimFile.FullName}\t{m.EntityCount()}");
				}
				filesCount += files.Count;
			}
			Console.WriteLine($"total: {foundCount}/{filesCount}");

			return Status.Ok;
		}

		private static IEnumerable<IfcFileInfo> GetMatching(List<string> files, List<IFindRequirement> reqs)
		{
			foreach (var file in files)
			{
				last = file;
				var t = IfcFileInfo.Load(file);
				var valid = true;
				foreach (var req in reqs)
				{
					valid = req.Valid(t);
					if (!valid)
						break;
				}
				if (valid)
				{
					yield return t;
				}
			}
		}

		private static string last;

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var t = new IfcFileInfo(last);
			t.GetFromBim(true);
			t.Error = "Unhandled exception: " + e.ExceptionObject.ToString();
			_ = t.SaveAsJsonAsync();

		}
	}
}
