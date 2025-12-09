using ImSharp;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.UI.AdvancedWindow;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Combos;

public sealed class OptionSelectCombo : FilterComboBase<OptionSelectCombo.Option>
{
    private sealed class OptionFilter : Utf8FilterBase<Option>
    {
        protected override ReadOnlySpan<byte> ToFilterString(in Option item, int globalIndex)
            => item.FullName;
    }

    public sealed class Option(IModDataContainer container)
    {
        public readonly IModDataContainer? Container  = container;
        public readonly StringU8           FullName   = new(container.GetFullName());
        public readonly int                GroupIndex = container.Group?.GetIndex() ?? -1;
        public readonly int                DataIndex  = container.GetDataIndices().DataIndex;
    }

    private readonly Im.ColorStyleDisposable _border = new();
    private readonly ModEditor               _editor;
    private readonly ModEditWindow           _window;

    public OptionSelectCombo(ModEditor editor, ModEditWindow window)
    {
        _editor = editor;
        _window = window;
        Filter  = new OptionFilter();
    }

    protected override IEnumerable<Option> GetItems()
        => _window.Mod?.AllDataContainers.Select(c => new Option(c)) ?? [];

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in Option item, int globalIndex, bool selected)
        => Im.Selectable(item.FullName, selected);

    protected override bool IsSelected(Option item, int globalIndex)
        => _editor.Option == item.Container;

    protected override void PreDrawCombo(float width)
    {
        Flags = _editor.Mod?.AllDataContainers.Count() switch
        {
            null or 0 or 1 => ComboFlags.NoArrowButton,
            > 8            => ComboFlags.HeightLargest,
            _              => ComboFlags.None,
        };
        _border.Push(ImStyleBorder.Frame, ColorId.FolderLine.Value());
    }

    protected override void PostDrawCombo(float width)
        => _border.Dispose();
}
