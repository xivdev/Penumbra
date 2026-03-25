using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Communication;
using Penumbra.Mods;

namespace Penumbra.UI.ManagementTab;

public abstract class ModColumn<TCacheObject> : TextColumn<TCacheObject>
{
    private readonly UiNavigator _navigator;

    protected sealed override string ComparisonText(in TCacheObject item, int globalIndex)
        => GetModName(item, globalIndex);

    protected sealed override StringU8 DisplayText(in TCacheObject item, int globalIndex)
        => GetModName(item, globalIndex);

    public override void DrawColumn(in TCacheObject item, int globalIndex)
    {
        var mod = GetMod(item, globalIndex);
        using (ImGuiColor.Text.Push(Im.Style[ImGuiColor.TextDisabled], MatchesLastItem(item)))
        {
            if (Im.Selectable(GetModName(item, globalIndex).Utf8) && mod is not null)
                _navigator.MoveTo(mod);
        }

        if (mod is not null)
            Im.Tooltip.OnHover("Click to move to mod."u8);
    }

    protected abstract bool MatchesLastItem(in TCacheObject item);

    protected ModColumn(UiNavigator navigator)
    {
        _navigator          = navigator;
        WidthDependsOnItems = true;
    }

    protected abstract Mod?       GetMod(in TCacheObject item, int globalIndex);
    protected abstract StringPair GetModName(in TCacheObject item, int globalIndex);

    public override float ComputeWidth(IEnumerable<TCacheObject> obj)
        => obj.Max(o => GetModName(o, 0).Utf8.CalculateSize().X, UnscaledWidth);
}
