using OtterGui.Text;

namespace Penumbra.UI.Tabs.Debug;

public static class DebugConfigurationDrawer
{
    public static void Draw()
    {
        if (!ImUtf8.CollapsingHeaderId("Debug Logging Options"))
            return;

        ImUtf8.Checkbox("Log IMC File Replacements"u8, ref DebugConfiguration.WriteImcBytesToLog);
    }
}
