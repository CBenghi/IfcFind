using IfcTool.Find;
using System.Linq;

namespace IfcTool
{
	internal class FindExactClassRequirement : IFindRequirement
	{
		private string className;

		public FindExactClassRequirement(string cl)
		{
			className = cl;
		}

		public bool Valid(IfcFileInfo fileToCheck)
		{
			// exact match is faster: try it
			if (fileToCheck.Classes.ContainsKey(className))
				return true;
			var low = className.ToLowerInvariant();
			return (fileToCheck.Classes.Keys.Any(x => x.ToLowerInvariant() == low));
		}
	}
}