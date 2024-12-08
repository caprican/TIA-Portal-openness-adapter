
namespace TIAOpennessAdapter.Models.Devices.Compositions
{
    public class Tags : DeviceComposition
    {
        public Tags(Siemens.Engineering.SW.Tags.PlcTagTable plcBlock) : base(plcBlock.Name)
        {
            
        }
    }
}
