using ImSharp;
using Luna;
using Penumbra.Api.Preset;
using Penumbra.GameData.Gui;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed class PresetCombo : FilterComboBase<PresetCombo.CacheItem>, IUiService, IDisposable
{
    private readonly ModSelection         _selection;
    public readonly  SettingPresetManager PresetManager;
    public           SettingPreset?       Selected      { get; private set; }
    public           string               ModIdentifier { get; private set; } = string.Empty;
    private          StringU8             _selectedName = StringU8.Empty;


    public PresetCombo(SettingPresetManager presetManager, ModSelection selection)
    {
        PresetManager              = presetManager;
        _selection                 = selection;
        ClearFilterOnSelection     = true;
        ClearFilterOnCacheDisposal = true;
        DirtyCacheOnClosingPopup   = true;
        ComputeWidth               = true;
        Filter                     = new ItemFilter();
        _selection.Subscribe(OnSelectionChange, ModSelection.Priority.ModPanel);
        PresetManager.Deleted += OnPresetDeleted;
    }

    private void OnSelectionChange(in ModSelection.Arguments arguments)
    {
        if (ModIdentifier.Length is 0)
            return;

        ModIdentifier = string.Empty;
        Selected      = null;
        _selectedName = StringU8.Empty;
    }

    public bool Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, float width)
    {
        if (base.Draw(label, Selected is null ? "Select Preset..."u8 : _selectedName, StringU8.Empty, width,
                out var newItem))
        {
            Selected      = newItem.Preset;
            _selectedName = newItem.Name;
            ModIdentifier = newItem.ModIdentifier;
            return true;
        }

        return false;
    }

    protected override FilterComboBaseCache<CacheItem> CreateCache()
        => new Cache(this);

    public readonly struct CacheItem(SettingPreset preset, Mod? mod)
    {
        public readonly SettingPreset Preset = preset;

        public readonly StringU8 Name = preset.Name.Length is 0
            ? new StringU8($"Preset {preset.Identifier.ShortGuidU8()}")
            : new StringU8(preset.Name);

        public readonly string ModIdentifier = mod?.Identifier ?? string.Empty;
    }

    private sealed class ItemFilter : Utf8FilterBase<CacheItem>
    {
        protected override ReadOnlySpan<byte> ToFilterString(in CacheItem item, int globalIndex)
            => item.Name;
    }

    private sealed class Cache(PresetCombo parent) : FilterComboBaseCache<CacheItem>(parent)
    {
        protected override void ComputeWidth()
        {
            var generic = Im.Style.ItemSpacing.X + Im.Font.CalculateSize("(Generic)"u8).X;
            if (AllItems.Count is 0)
                base.ComputeWidth();
            else
                ComboWidth = AllItems.Max(i => i.Name.CalculateSize().X + (i.ModIdentifier.Length is 0 ? generic : 0));
        }
    }

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override IEnumerable<CacheItem> GetItems()
    {
        var mod            = _selection.Mod;
        var modPresets     = mod?.Presets ?? [];
        var generalPresets = PresetManager.GenericPresets;
        return modPresets.OrderBy(m => m.Name).ThenByDescending(m => m.LastEdit).Select(m => new CacheItem(m, mod))
            .Concat(generalPresets.OrderBy(m => m.Name).ThenByDescending(m => m.LastEdit).Select(m => new CacheItem(m, null)));
    }

    protected override bool DrawItem(in CacheItem item, int globalIndex, bool selected)
    {
        bool ret;
        using (ImGuiColor.Text.Push(ColorId.ModSpecificPreset.Vector, item.ModIdentifier.Length > 0))
        {
            ret = Im.Selectable(item.Name, selected);
        }

        if (Im.Item.Hovered())
        {
            using var _  = Im.Style.PushDefault();
            using var tt = Im.Tooltip.Begin();
            DrawTooltip(item.Preset);
        }

        if (item.ModIdentifier.Length is 0)
            using (ImGuiColor.Text.Push(ColorId.ItemId.Vector))
            {
                Im.Line.NoSpacing();
                ImEx.TextRightAligned("(Generic)"u8);
            }


        return ret;
    }

    public void DrawTooltip(SettingPreset preset)
        => preset.Data.DrawTooltip(_selection.Inherited ? null : _selection.Settings.Enabled, GetGroupData);

    protected override bool IsSelected(CacheItem item, int globalIndex)
        => item.Preset == Selected;

    public void Dispose()
    {
        _selection.Unsubscribe(OnSelectionChange);
        PresetManager.Deleted -= OnPresetDeleted;
    }

    private void OnPresetDeleted(SettingPreset obj)
    {
        if (obj != Selected)
            return;

        Selected      = null;
        ModIdentifier = string.Empty;
        _selectedName = StringU8.Empty;
    }

    private IReadOnlyList<(ModObjectIdentifier, bool)>? GetGroupData(in ModObjectIdentifier groupIdentifier, out string? name)
    {
        if (groupIdentifier.FindGroup(_selection.Mod) is not { } group)
        {
            name = null;
            return null;
        }

        name = group.Name;
        return new PresetTooltipAdapterList(group.Options, _selection.Settings.IsEmpty ? group.DefaultSettings : _selection.Settings.Settings[group.Index],
            group.Behaviour);
    }

    internal sealed class PresetTooltipAdapterList(IReadOnlyList<IModOption> options, Setting setting, GroupDrawBehaviour single)
        : IReadOnlyList<(ModObjectIdentifier, bool)>
    {
        public IEnumerator<(ModObjectIdentifier, bool)> GetEnumerator()
            => options.Select(Convert).GetEnumerator();

        private (ModObjectIdentifier, bool) Convert(IModOption option, int index)
            => (ModObjectIdentifier.From(option),
                single is GroupDrawBehaviour.SingleSelection ? setting.AsIndex == index : setting.HasFlag(index));

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public int Count
            => options.Count;

        public (ModObjectIdentifier, bool) this[int index]
            => Convert(options[index], index);
    }
}
