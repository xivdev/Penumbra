using ImGuiNET;
using OtterGui.Classes;
using OtterGui.Widgets;
using Penumbra.Collections;

namespace Penumbra.UI.CollectionTab;

public sealed class SpecialCombo : FilterComboBase<(CollectionType, string, string)>
{
    private readonly CollectionManager _collectionManager;

    public (CollectionType, string, string)? CurrentType
        => CollectionTypeExtensions.Special[CurrentIdx];

    public int CurrentIdx;
    private readonly float _unscaledWidth;
    private readonly string _label;

    public SpecialCombo(CollectionManager collectionManager, string label, float unscaledWidth)
        : base(CollectionTypeExtensions.Special, false)
    {
        _collectionManager = collectionManager;
        _label = label;
        _unscaledWidth = unscaledWidth;
    }

    public void Draw()
    {
        var preview = CurrentIdx >= 0 ? Items[CurrentIdx].Item2 : string.Empty;
        Draw(_label, preview, string.Empty, ref CurrentIdx, _unscaledWidth * UiHelpers.Scale,
            ImGui.GetTextLineHeightWithSpacing());
    }

    protected override string ToString((CollectionType, string, string) obj)
        => obj.Item2;

    protected override bool IsVisible(int globalIdx, LowerString filter)
    {
        var obj = Items[globalIdx];
        return filter.IsContained(obj.Item2) && _collectionManager.ByType(obj.Item1) == null;
    }
}