using CommandLine;
using IfcTool.Find;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

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

		[Option('b', "bare", Required = false, HelpText = "Just return file names.")]
		public bool Bare { get; set; }

		[Option('x', "preferXbim", Required = false, HelpText = "if xbim version is available use in report.")]
		public bool PreferXbim { get; set; }

		[Option('e', "errors", Required = false, HelpText = "Files that could not be parsed.")]
		public bool Error { get; set; }

		[Option('a', "applications", Required = false, HelpText = "Files produced by specified application regex.")]
		public IEnumerable<string> Applications { get; set; }

		[Option('s', "schema", Required = false, Default = schema.any, HelpText = "Limits the search by schema version.")]
		public schema Schema { get; set; }

		[Option('c', "classes", Required = false, HelpText = "returns file with all specified classes.")]
		public IEnumerable<string> Classes { get; set; }

		[Option('r', "classPatterns", Required = false, HelpText = "returns file with partial class match (uses contains function).")]
		public IEnumerable<string> PartClasses { get; set; }

		[Option('p', "propertyPatterns", Required = false, HelpText = "returns file with regex property match. (PsetName/PropName/PropType/PropValueType)")]
		public IEnumerable<string> Properties { get; set; }

		internal static string BareFolderFileName(FileInfo x)
		{
			return Path.Combine(Path.GetDirectoryName(x.FullName), Path.GetFileNameWithoutExtension(x.FullName));
		}

		internal static Status Run(FindOptions opts)
		{
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

			IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
			if (!opts.Bare)
				Console.WriteLine("Executing Find.");
			var foundCount = 0;
			var filesCount = 0;
			Dictionary<string, int> types = new Dictionary<string, int>();
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
				if (!opts.Bare)
					Console.WriteLine($"Updated {changeCount} files.");

				// now execute search
				//
				List<IFindRequirement> reqs = new List<IFindRequirement>();
				reqs.Add(new FindSchemaVersionRequirement(opts.Schema));
				foreach (var cl in opts?.Classes)
					reqs.Add(new FindExactClassRequirement(cl));
				foreach (var cl in opts?.Properties)
					reqs.Add(new FindPartClassRequirement(cl));
				foreach (var cl in opts?.PartClasses)
					reqs.Add(new FindPartClassRequirement(cl));
				if (opts.Applications != null && opts.Applications.Any())
					reqs.Add(new FindApplicationRequirement(opts.Applications));
				if (opts.Error)
					reqs.Add(new FindErrorRequirement());
				
				var Matching = GetMatching(files, reqs);
				bool example = false;
				foreach (var match in Matching)
                {
					// special action
					if (example)
						PerformActionRetainingTimeStamp(match, types, FindIIfcRelDefinesByTypeTypes);
                    
					// add count and report
					foundCount++;
					var repFile = opts.PreferXbim
						? match.CachedBimFile.FullName
						: match.StandardBimFile.FullName;

					if (opts.Bare)
						Console.WriteLine($"{repFile}");
					else
						Console.WriteLine($"found:\t{repFile}\t{match.EntityCount()}");
				}
                filesCount += files.Count;  
			}
			if (types != null)
			{
				foreach (var keyValuePair in types)
				{
					Console.WriteLine($"{keyValuePair.Key}\t{keyValuePair.Value}");
				}
			}
			if (!opts.Bare)
				Console.WriteLine($"total: {foundCount}/{filesCount}");

			return Status.Ok;
		}

        private static void PerformActionRetainingTimeStamp(IfcFileInfo m, Dictionary<string, int> types, Action<IfcFileInfo, Dictionary<string, int>> function)
        {
			// LastWriteTimeUtc is the value cached for checking last update
			var prevValue = File.GetLastWriteTimeUtc(m.OldestBimFile.FullName);
			function(m, types);
			File.SetLastWriteTimeUtc(m.OldestBimFile.FullName, prevValue);
		}

        private static void FindIIfcRelDefinesByTypeTypes(IfcFileInfo m, Dictionary<string, int> types)
		{
            IfcStore s = IfcStore.Open(m.OldestBimFile.FullName, accessMode: Xbim.IO.XbimDBAccess.Read);
            foreach (var entity in s.Instances.OfType<IIfcRelDefinesByType>())
            {
                foreach (var ro in entity.RelatedObjects)
                {
                    var str = $"{m.Schema}\t{entity.RelatingType.GetType().Name}\t{ro.GetType().Name}";
                    if (types.ContainsKey(str))
                        types[str] += 1;
                    else
                        types.Add(str, 1);
                }
            }
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
