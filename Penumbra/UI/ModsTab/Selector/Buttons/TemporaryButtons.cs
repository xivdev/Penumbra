using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Settings;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class TemporaryButtons(ModFileSystemDrawer drawer) : BaseButton<IFileSystemData>
{
    public override ReadOnlySpan<byte> Label(in IFileSystemData data)
        => throw new NotImplementedException();

    public override bool DrawMenuItem(in IFileSystemData data)
    {
        if (data.GetValue<Mod>() is not { } mod)
            return false;

        var current      = drawer.CollectionManager.Active.Current;
        var tempSettings = current.GetTempSettings(mod.Index);
        if (tempSettings is { Lock: > 0 })
            return false;

        var editor = drawer.CollectionManager.Editor;
        if (tempSettings is { Lock: <= 0 } && Im.Menu.Item("Remove Temporary Settings"u8))
            editor.SetTemporarySettings(current, mod, null);
        var actual = current.GetActualSettings(mod.Index).Settings;
        if (actual?.Enabled is true && Im.Menu.Item("Disable Temporarily"u8))
            editor.SetTemporarySettings(current, mod, new TemporaryModSettings(mod, actual) { Enabled = false });

        if (actual is not { Enabled: true } && Im.Menu.Item("Enable Temporarily"u8))
        {
            var newSettings = actual is null
                ? TemporaryModSettings.DefaultSettings(mod, TemporaryModSettings.OwnSource, true)
                : new TemporaryModSettings(mod, actual) { Enabled = true };
            editor.SetTemporarySettings(current, mod, newSettings);
        }

        if (tempSettings is null && Im.Menu.Item("Turn Temporary"u8))
            editor.SetTemporarySettings(current, mod, new TemporaryModSettings(mod, actual));
        return false;
    }
}
