using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xbim.Common.Metadata;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace IfcTool
{
	public class IfcFileInfo
	{
		// props saved
		const int ThisCacheVersion = 1;
		public DateTime LastWriteUTC { get; set; }
		public string Error { get; set; } = null;
		public string Schema { get; set; }
		public List<string> Applications { get; set; }
		public Dictionary<string, IfcClassInfo> Classes { get; set; }		
		public int CacheVersion { get; set; }

		// plumbing
		private DirectoryInfo Directory;
		private string BareName;

		public IfcFileInfo()
		{

		}

		public IfcFileInfo(string file)
		{
			SetFileName(file);
		}

		private void SetFileName(string file)
		{
			Directory = new DirectoryInfo(Path.GetDirectoryName(file));
			BareName = Path.GetFileName(file);
		}

		public static string[] Extensions = new[] { "ifc", "ifczip", "xbim" };

		public static string IndexExtension = "ifcfind";

		internal FileInfo IndexFile
		{
			get
			{
				var filename = $"{BareName}.{IndexExtension}";
				var full = Path.Combine(Directory.FullName, filename);
				return new FileInfo(full);
			}
		}

		internal FileInfo BimFile
		{
			get
			{
				List<FileInfo> fls = new List<FileInfo>();
				foreach (var ext in Extensions)
				{
					var filename = $"{BareName}.{ext}";
					var full = Path.Combine(Directory.FullName, filename);
					FileInfo f = new FileInfo(full);
					if (f.Exists)
						fls.Add(f);
				}
				if (!fls.Any())
					return null;
				return fls.OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
			}
		}

		internal static bool Index(string file)
		{
			var t = From(file);
			if (t.BimFile == null)
			{
				t.RemoveIndex();
				return false;
			}
			return t.CreateOrUpdate();
		}

		private static IfcFileInfo From(string file)
		{
			var t = new IfcFileInfo(file);
			if (t.IndexFile.Exists)
			{
				return Load(t.IndexFile);
			}
			return t;
		}

		private bool CreateOrUpdate()
		{
			if (BimFile == null)
				return false;
			if (!IndexFile.Exists)
			{
				GetFromBim();
				_ = SaveAsJsonAsync(IndexFile.FullName);
				return true;
			}
			// if it existed then it's loaded... check it's up to date.
			if (
				CacheVersion != ThisCacheVersion
				|| LastWriteUTC != BimFile.LastWriteTimeUtc
				)
			{
				GetFromBim();
				_ = SaveAsJsonAsync(IndexFile.FullName);
				return true;
			}
			return false;
		}

		public static IfcFileInfo Load(string indexFileName)
		{
			var infoName = indexFileName + "." + IndexExtension;
			FileInfo f = new FileInfo(infoName);
			if (!f.Exists)
				return null;
			return Load(f);
		}

		public static IfcFileInfo Load(FileInfo indexFile)
		{
			var jsonString = File.ReadAllText(indexFile.FullName);
			var loaded = JsonSerializer.Deserialize<IfcFileInfo>(jsonString);
			loaded.SetFileName(FindOptions.BareFolderFileName(indexFile));
			return loaded;
		}

		internal void GetFromBim(bool omitContent = false)
		{
			LastWriteUTC = BimFile.LastWriteTimeUtc;
			CacheVersion = ThisCacheVersion;
			if (omitContent)
				return;
			try
			{
				Console.WriteLine($"Updating {BimFile.FullName}");
				using IfcStore st = IfcStore.Open(BimFile.FullName, null, null, null, Xbim.IO.XbimDBAccess.Read);
				Schema = st.SchemaVersion.ToString();
				
				var apps = st.Instances.OfType<IIfcApplication>();
				Applications = new List<string>();
				foreach (var app in apps)
				{
					Applications.Add($"{app.ApplicationFullName}  {app.ApplicationIdentifier} {app.Version}");
				}
				// very low efficiency, just to have it quick and dirty.
				// we're using expresstype because we might add more features later
				var TypeAndCount = new Dictionary<ExpressType, int>();
				foreach (var modelInstance in st.Instances)
				{
					var t = modelInstance.ExpressType;
					if (TypeAndCount.ContainsKey(t))
						TypeAndCount[t] += 1;
					else
						TypeAndCount.Add(t, 1);
				}

				var keys = TypeAndCount.Keys.ToList();
				keys.Sort( // sort inverted
						(x1, x2) => TypeAndCount[x2].CompareTo(TypeAndCount[x1])
					);
				Classes = new Dictionary<string, IfcClassInfo>();
				foreach (var key in keys)
				{
					Classes.Add(key.ExpressName, new IfcClassInfo(TypeAndCount[key]));
				}
				st.Close();
				// whatever happened with opening the file, replace the last write it had before reading it
				
			}
			catch (Exception ex)
			{
				Error = ex.Message;
			}
			BimFile.LastWriteTimeUtc = LastWriteUTC;
		}

		public async System.Threading.Tasks.Task SaveAsJsonAsync(string destinationFile = null)
		{
			if (destinationFile == null)
				destinationFile = IndexFile.FullName;
			if (destinationFile == null)
				return;			
			using FileStream createStream = File.Create(destinationFile);
			await JsonSerializer.SerializeAsync(createStream, this);
		}

		private void RemoveIndex()
		{
			var ifile = IndexFile;
			if (ifile.Exists)
				ifile.Delete();
		}

	}

	public class IfcClassInfo
	{
		public IfcClassInfo()
		{

		}

		public IfcClassInfo(int useCount)
		{
			// Name = expressName;
			Count = useCount;
		}

		// public string Name { get; set; }
		public int Count { get; set; }

	}
}
