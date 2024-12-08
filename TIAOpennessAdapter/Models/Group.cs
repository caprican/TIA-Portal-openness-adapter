using System.Collections.Generic;
using TIAOpennessAdapter.Models.Devices;

namespace TIAOpennessAdapter.Models
{
    public class Group : DeviceComposition
    {
        public List<DeviceComposition>? Items { get; set; }

        //public List<Group>? Groups { get; set; }

        public Group(string name) : base(name)
        {

        }
    }
}