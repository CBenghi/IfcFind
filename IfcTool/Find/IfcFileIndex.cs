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
		public Dictionary<string, IfcProperyInfo> Properties { get; set; }		
		public int CacheVersion { get; set; }

		public int EntityCount()
		{
			if (Classes == null)
				return 0;
			var ret = 0;
			foreach (var classv in Classes.Values)
			{
				ret += classv.Count;
			}
			return ret;
		}

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

		internal IEnumerable<FileInfo> BimFiles
		{
			get
            {
				foreach (var ext in Extensions)
				{
					var filename = $"{BareName}.{ext}";
					var full = Path.Combine(Directory.FullName, filename);
					FileInfo f = new FileInfo(full);
					if (f.Exists)
						yield return f;
				}
			}
		}

		internal FileInfo OldestBimFile
		{
			get
			{
				var fls = BimFiles.ToList();
				if (!fls.Any())
					return null;
				return fls.OrderBy(x => x.LastWriteTime).FirstOrDefault();
			}
		}

		internal FileInfo StandardBimFile
        {
			get
			{
				return BimFiles.FirstOrDefault();
			}
        }
		internal FileInfo CachedBimFile
		{
			get
			{
				return BimFiles.LastOrDefault();
			}
		}


		internal FileInfo NewestBimFile
		{
			get
			{
				var fls = BimFiles.ToList();
				if (!fls.Any())
					return null;
				return fls.OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
			}
		}

		/// <returns>true if index was changed</returns>
		internal static bool Index(string file, bool preventUpdate = false)
		{
			var t = From(file);
			if (t == null)	
				return false;
			if (t.NewestBimFile == null)
			{
				t.RemoveIndex(); // delete the index if the related bim file has been removed.
				return false;
			}
			return t.CreateOrUpdate(preventUpdate);
		}

		private static IfcFileInfo From(string file)
		{
			var t = new IfcFileInfo(file);
			if (t.IndexFile.Exists)
			{
                try
                {
					return Load(t.IndexFile);
				}
                catch (Exception)
                {
					if (t.IndexFile.Exists)
						t.IndexFile.Delete(); // delete a failing file
				}
				
			}
			return t;
		}

		private bool CreateOrUpdate(bool preventUpdate)
		{
			if (NewestBimFile == null)
				return false;
			if (!IndexFile.Exists)
			{
				if (preventUpdate)
					return false;
				GetFromBim();
				_ = SaveAsJsonAsync(IndexFile.FullName);
				return true;
			}
			// if it existed then it's loaded... check it's up to date.
			if (
				preventUpdate == false 
				&&
					(
					CacheVersion != ThisCacheVersion
					|| 
					LastWriteUTC != NewestBimFile.LastWriteTimeUtc
					)
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
			LastWriteUTC = NewestBimFile.LastWriteTimeUtc;
			CacheVersion = ThisCacheVersion;
			if (omitContent)
				return;
			try
			{
				Console.WriteLine($"Updating {NewestBimFile.FullName}");
				using IfcStore st = IfcStore.Open(NewestBimFile.FullName, null, null, null, Xbim.IO.XbimDBAccess.Read);
				Schema = st.SchemaVersion.ToString();
				
				var apps = st.Instances.OfType<IIfcApplication>();
				Applications = new List<string>();
				foreach (var app in apps)
				{
					Applications.Add($"{app.ApplicationFullName}  {app.ApplicationIdentifier} {app.Version}");
				}

				// classes

				// very low efficiency, just to have it quick and dirty.
				// we're using expresstype because we might add more features later
				//
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


				Properties = new Dictionary<string, IfcProperyInfo>();
				// properties
				if (false)
				{
					foreach (var modelProp in st.Instances.OfType<IIfcProperty>())
					{
						if (modelProp is IIfcPropertySingleValue psv)
						{
							string propName = psv.Name.Value.ToString();
							var infor = (
								psv.GetType().Name,
								psv.NominalValue.GetType().Name
								);
							if (!psv.PartOfPset.Any())
							{
								RecordInformation(propName, infor, "");
							}
							else
							{
								foreach (var pset in psv.PartOfPset)
								{
									RecordInformation(propName, infor, pset.Name.Value.ToString());
								}
							}
						}
					}
				}
				st.Close();
				// whatever happened with opening the file, replace the last write it had before reading it
				
			}
			catch (Exception ex)
			{
				Error = ex.Message;
			}
			NewestBimFile.LastWriteTimeUtc = LastWriteUTC;
		}

        private void RecordInformation(string propName, (string, string) infor, string psetName)
        {
            var CombinedName = psetName + "/" + propName;
            if (Properties.TryGetValue(CombinedName, out var fnd))
                fnd.TryAdd(infor);
            else
                Properties.Add(CombinedName, new IfcProperyInfo(infor));
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
}
