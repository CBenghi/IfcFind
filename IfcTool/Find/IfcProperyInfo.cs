using System.Collections.Generic;

namespace IfcTool
{
    public class IfcProperyInfo
    {
        public IfcProperyInfo((string, string) infor)
        {
            PropertyAndValueTypes = new List<(string, string)> { infor };
        }

        public List<(string, string)> PropertyAndValueTypes { get; set; }
        
        public void FoundProp(string propType, string propValueType)
        {
            var t = (propType, propValueType);
            PropertyAndValueTypes.Add(t);
        }

        internal void TryAdd((string, string) infor)
        {
            if (PropertyAndValueTypes.Contains(infor))
                return;
            PropertyAndValueTypes.Add(infor);
        }
    }
}