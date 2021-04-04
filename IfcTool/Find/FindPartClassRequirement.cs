using IfcTool.Find;
using System.Linq;

namespace IfcTool
{
	internal class FindPartClassRequirement : IFindRequirement
	{
		private string className;

		public FindPartClassRequirement(string cl)
		{
			className = cl;
		}

		public bool Valid(IfcFileInfo fileToCheck)
		{
			var low = className.ToLowerInvariant();
			var t = fileToCheck.Classes.Keys.FirstOrDefault(x => x.ToLowerInvariant().Contains(low));
			return (t != null);
		}
	}
}