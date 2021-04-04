using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IfcTool.Find
{
	interface IFindRequirement
	{
		bool Valid(IfcFileInfo fileToCheck);
	}
}
