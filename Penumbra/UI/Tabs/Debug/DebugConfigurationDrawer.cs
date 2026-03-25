using ImSharp;

namespace Penumbra.UI.Tabs.Debug;

public static class DebugConfigurationDrawer
{
    public static void Draw()
    {
        using var id = Im.Tree.HeaderId("Debugging Options"u8);
        if (!id)
            return;

        Im.Checkbox("Log IMC File Replacements"u8,         ref DebugConfiguration.WriteImcBytesToLog);
        Im.Checkbox("Scan for Skin Material Attributes"u8, ref DebugConfiguration.UseSkinMaterialProcessing);
    }
}
