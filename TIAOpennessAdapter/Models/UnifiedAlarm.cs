
using System.Collections.Generic;
using System.Diagnostics;

using TIAOpennessAdapter.Models.Devices;

namespace TIAOpennessAdapter.Models
{
    [DebuggerDisplay("{Tagname}")]
    public class UnifiedAlarm
    {
        public HmiUnifiedDevice Hmi { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Tagname { get; set; } = string.Empty;
        public string? Origin { get; set; }
        public Dictionary<string, string> Descriptions { get; set; }
    }
}
