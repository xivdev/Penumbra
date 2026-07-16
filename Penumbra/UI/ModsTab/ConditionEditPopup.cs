using ImSharp;
using Luna;
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
    }

    protected override Im.PopupDisposable Begin()
        => Im.Popup.BeginResizable(PopupId);

    protected override void PrePopup()
    {
        Im.Window.SetNextSizeConstraints(ImEx.ScaledVector(640, 640), Vector2.PositiveInfinity);
        ImStyleDouble.WindowPadding.Push(Vector2.Zero);
    }

    protected override void PostPopup()
        => Im.StyleDisposable.PopUnsafe();
}
