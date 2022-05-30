namespace IfcTool
{
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
