using System;

namespace IfcTool
{
	public class TransientFaultHandlingOptions
	{
		public bool Enabled { get; set; }
		public TimeSpan AutoRetryDelay { get; set; }
	}
}