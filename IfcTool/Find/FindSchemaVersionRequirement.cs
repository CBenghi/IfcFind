using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IfcTool.Find
{
	class FindSchemaVersionRequirement : IFindRequirement
	{
		private schema schema;

		public FindSchemaVersionRequirement(schema schema)
		{
			this.schema = schema;
		}

		public bool Valid(IfcFileInfo t)
		{
			if (t.Error != null)
				return false;
			if (schema == schema.any)
				return true;
			return t.Schema.ToLowerInvariant() == schema.ToString().ToLowerInvariant();
		}
	}
}
