using OtterGui.Text;

namespace Penumbra.UI.Tabs.Debug;

public static class DebugConfigurationDrawer
{
    public static void Draw()
    {
        using var id = ImUtf8.CollapsingHeaderId("Debug Logging Options"u8);
        if (!id)
            return;

        ImUtf8.Checkbox("Log IMC File Replacements"u8, ref DebugConfiguration.WriteImcBytesToLog);
    }
}
