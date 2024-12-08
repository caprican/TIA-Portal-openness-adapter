using System.Collections.Generic;

using Siemens.Engineering.SW;

namespace TIAOpennessAdapter.Models.Devices
{
    public class PlcDevice : Device
    {
        internal readonly PlcSoftware Plc;

        internal List<Siemens.Engineering.SW.Blocks.PlcBlock>? Blocks;

        public PlcDevice(PlcSoftware plc) : base(plc.Name)
        {
            Plc = plc;

        }
    }
}
