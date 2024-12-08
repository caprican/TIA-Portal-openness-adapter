using System.Diagnostics;

using TIAOpennessAdapter.Models.Devices;

namespace TIAOpennessAdapter.Models
{
    [DebuggerDisplay("{PlcTag}")]
    public class UnifiedTag
    {
        public HmiUnifiedDevice Hmi { get; set; }
        public string Connexion { get; set; } = string.Empty;
        public string PlcTag { get; set; } = string.Empty;
        public string Tagname { get; set; } = string.Empty;
        public string? Folder { get; set; }
    }
}
