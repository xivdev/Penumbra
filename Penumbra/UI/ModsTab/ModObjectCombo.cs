using ImSharp;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab;

internal sealed class ModObjectCache
{
    public readonly IModObject?     Object;
    public readonly ModObjectCache? Group;
    public readonly StringPair      Name;
    public readonly StringPair      GroupName;
    public          bool            CausesCycle;
    public          bool            Visible;

    public Guid Id
        => Object?.Id ?? Guid.Empty;

    private ModObjectCache()
    {
        Name      = new StringPair("None"u8);
        Visible   = true;
        GroupName = StringPair.Empty;
    }

    public ModObjectCache(IModObject o, ModObjectCache? parent)
    {
        Object    = o;
        Group     = parent;
        Name      = new StringPair(o.Name);
        GroupName = parent?.Name ?? StringPair.Empty;
    }

    public static readonly ModObjectCache None = new();
}

internal abstract class ModObjectCombo : FilterComboBase<ModObjectCache>
{
    protected Mod?       Mod;
    protected IModGroup? Group;

    protected ModObjectCombo()
    {
        DirtyCacheOnClosingPopup = true;
        ComputeWidth             = true;
        Filter                   = new ItemFilter();
    }

    private sealed class Cache(ModObjectCombo parent) : FilterComboBaseCache<ModObjectCache>(parent)
    {
        private float _fullNameWidth;
        private float _nameWidth;

        protected override void ComputeWidth()
        {
            _fullNameWidth = 0;
            _nameWidth     = 0;
            ModObjectCache? group     = null;
            float           groupSize = 0;
            foreach (var item in AllItems)
            {
                var   name = item.Name.Utf8.CalculateSize().X;
                float soloName, groupName;
                if (item.Group is { } g)
                {
                    soloName = name + Im.Style.IndentSpacing;
                    if (group != g)
                    {
                        group     = g;
                        groupSize = item.GroupName.Utf8.CalculateSize().X + Im.Style.ItemSpacing.X;
                    }

                    groupName = name + groupSize;
                }
                else
                {
                    group     = item;
                    soloName  = name;
                    groupName = name;
                    groupSize = name + Im.Style.ItemSpacing.X;
                }

                if (_nameWidth < soloName)
                    _nameWidth = soloName;
                if (_fullNameWidth < groupName)
                    _fullNameWidth = groupName;
            }

            ComboWidth = Filter.IsEmpty ? _nameWidth : _fullNameWidth;
        }

        protected override void OnFilterUpdate()
        {
            base.OnFilterUpdate();
            ComboWidth = Filter.IsEmpty ? _nameWidth : _fullNameWidth;
        }
    }

    private sealed class ItemFilter : TextFilterBase<ModObjectCache>
    {
        public override bool WouldBeVisible(in ModObjectCache item, int globalIndex)
            => item.Visible = base.WouldBeVisible(item.Name.Utf16) || base.WouldBeVisible(item.GroupName.Utf16);

        protected override string ToFilterString(in ModObjectCache item, int globalIndex)
            => item.Name.Utf16;
    }

    protected override FilterComboBaseCache<ModObjectCache> CreateCache()
        => new Cache(this);

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;
}
