using System.Collections.Generic;
using TIAOpennessAdapter.Models.Devices;

namespace TIAOpennessAdapter.Models
{
    public class Device : DeviceComposition
    {
        public List<DeviceComposition>? Items { get; set; }

        //public List<Group>? Groups { get; set; }

        public Device(string name) : base(name)
        {
        }
    }
}