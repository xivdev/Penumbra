using Penumbra.GameData.Files;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private class MdlTab : IWritable
    {
        public readonly MdlFile Mdl;

        public MdlTab(byte[] bytes)
        {
            Mdl = new MdlFile(bytes);
        }

        public bool Valid => Mdl.Valid;

        public byte[] Write() => Mdl.Write();
    }
}