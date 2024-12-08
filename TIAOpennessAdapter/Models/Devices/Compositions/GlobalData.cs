
namespace TIAOpennessAdapter.Models.Devices.Compositions
{
    public class GlobalData : Block
    {
        public bool IsConsistent => plcBlock.IsConsistent;
        public string Parent => plcBlock.Parent.GetAttribute("Name").ToString();

        public GlobalData(Siemens.Engineering.SW.Blocks.PlcBlock plcBlock) : base(plcBlock)
        {
            
        }
    }
}
