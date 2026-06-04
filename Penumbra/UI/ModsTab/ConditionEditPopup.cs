using ImSharp;
using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab;

public sealed class ConditionEditPopup(ModGroupConditionDrawer drawer) : ObjectEditPopup, IUiService
{
    protected override ReadOnlySpan<byte> PopupId
        => "ConditionEdit"u8;

    public void Open(IModObject @object)
        => Open((object)@object);

    protected override void DrawInternal()
    {
        if (Current is not IModObject obj)
            return;

        var cursor = Im.Cursor.Position;
        Im.ScaledDummy(640);
        Im.Cursor.Position = cursor;
        if (drawer.Draw(obj.Condition, new ModSettingContext(obj.Mod, ModSettings.Empty), out var newCondition))
            obj.Condition = newCondition;
    }
}
