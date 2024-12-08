namespace TIAOpennessAdapter.Models.Devices.Compositions
{
    public class Block : DeviceComposition
    {
        internal readonly Siemens.Engineering.SW.Blocks.PlcBlock plcBlock;

        public bool IsConsistent => plcBlock.IsConsistent;
        public string Parent => plcBlock.Parent.GetAttribute("Name").ToString();

        public Block(Siemens.Engineering.SW.Blocks.PlcBlock block) : base(block.Name)
        {
            plcBlock = block;
            Number = (uint)plcBlock.Number;
        }

        public Block(Siemens.Engineering.SW.Types.PlcType block) : base(block.Name)
        {
            plcBlock = default;
        }
    }
}
