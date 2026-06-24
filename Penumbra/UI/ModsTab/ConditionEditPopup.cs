using ImSharp;
using ImSharp.ImNodes;
using Luna;
using Penumbra.Mods.Groups;
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

        ImNodes.Tester.Show("editor"u8);

        //Im.ScaledDummy(640);
        //Im.Cursor.Position = cursor;
        //if (drawer.Draw(obj.Condition, new ModSettingContext(obj.Mod, ModSettings.Empty, obj), out var newCondition))
        //    obj.Condition = newCondition;
    }

    protected override void PrePopup()
    {
        Im.Window.SetNextSize(ImEx.ScaledVector(640, 640));
        ImStyleDouble.WindowPadding.Push(Vector2.Zero);
    }

    protected override void PostPopup()
        => Im.StyleDisposable.PopUnsafe();
}
