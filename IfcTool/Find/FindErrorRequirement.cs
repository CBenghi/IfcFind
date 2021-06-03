using IfcTool.Find;
using System.Linq;

namespace IfcTool
{
	internal class FindErrorRequirement : IFindRequirement
	{
		public FindErrorRequirement()
		{
			
		}

		public bool Valid(IfcFileInfo fileToCheck)
		{
			return (string.IsNullOrEmpty(fileToCheck.Error));
		}
	}
}