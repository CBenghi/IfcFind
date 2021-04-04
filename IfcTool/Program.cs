using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Ifc;

namespace IfcTool
{
	[Flags]
	internal enum Status
	{
		Ok = 0,
		NotImplemented = 1,
		CommandLineError = 2,
		NotFoundError = 4,
		ContentError = 8,
		ContentMismatchError = 16,
		XsdSchemaError = 32,
	}

	class Program
	{
		
		static int Main(string[] args)
		{
			

			var t = Parser.Default.ParseArguments<FindOptions, ErrorCodeOptions>(args)
			  .MapResult(
				(FindOptions opts) => FindOptions.Run(opts),
				(ErrorCodeOptions opts) => ErrorCodeOptions.Run(opts),
				errs => Status.CommandLineError);
			return (int)t;

			
		}


	}
}
