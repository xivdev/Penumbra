using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab;

public sealed class ConditionEditPopup(ModGroupEditor editor, CommunicatorService communicator) : ObjectEditPopup, IUiService
{
    protected override ReadOnlySpan<byte> PopupId
        => "ConditionEdit"u8;

    public void Open(IModObject @object)
        => Open((object)@object);

    protected override void DrawInternal()
    {
        if (Current is not IModObject obj)
            return;

        using var id = Im.Id.Push(obj.Id.GetHashCode());

        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current,
            () => new ModGroupConditionCache(communicator, new ModSettingContext(obj.Mod, ModSettings.Empty, obj)));
        if (cache.Draw())
            editor.SetCondition(obj, obj.Condition?.Reduce(), true);
        //ImNodes.Tester.Show("editor"u8);

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
