using System.Collections.Generic;
using System.Linq;

using Siemens.Engineering.HmiUnified;

namespace TIAOpennessAdapter.Models.Devices
{
    public class HmiUnifiedDevice : Device
    {
        internal readonly HmiSoftware Device;

        public IEnumerable<string> Connections { get; }

        public HmiUnifiedDevice(HmiSoftware device) : base(device.Name)
        {
            Device = device;

            Connections = device.Connections.Select(s => s.Partner);
        }
    }
}
