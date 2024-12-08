using System.Diagnostics;

namespace TIAOpennessAdapter.Models.Devices
{
    [DebuggerDisplay("{Name}")]
    public class DeviceComposition
    {
        public string Name { get; }
        public uint Number { get; internal set; }

        public string? Path { get; internal set; }

        public DeviceComposition(string name)
        {
            Name = name;
        }
    }
}