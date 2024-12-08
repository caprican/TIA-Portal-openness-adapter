
namespace TIAOpennessAdapter.Models.Devices.Compositions
{
    public class DataStruct : DeviceComposition
    {
        public DataStruct(Siemens.Engineering.SW.Types.PlcType plcBlock) : base(plcBlock.Name)
        {
            
        }
    }
}
