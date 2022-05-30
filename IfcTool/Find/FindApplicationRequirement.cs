using IfcTool.Find;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IfcTool
{
	internal class FindApplicationRequirement : IFindRequirement
	{
		private List<Regex> appReg;

		public FindApplicationRequirement(IEnumerable<string> applicationRegexes)
		{
			appReg = new List<Regex>();
            foreach (var item in applicationRegexes)
            {
				appReg.Add(new Regex(item, RegexOptions.IgnoreCase));
            }
		}

		public bool Valid(IfcFileInfo fileToCheck)
		{
            foreach (var test in appReg)
            {
                foreach (var app in fileToCheck.Applications)
                {
					var match = test.Match(app);
					if (match.Success)
						return true;
				}
            }
			return false;
		}
	}
}