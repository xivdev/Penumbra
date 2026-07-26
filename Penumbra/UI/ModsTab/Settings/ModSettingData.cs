using ImSharp;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab.Settings;

public record ModSettingDataNode(StringU8 Name, StringU8 Description)
{
    public bool Visible;
    public bool Disabled;
}

public sealed record ModSettingPage : ModSettingDataNode
{
    public readonly int                       Id;
    public readonly List<ModSettingGroup>     Groups        = [];
    public readonly List<ModSettingGroup>     VisibleGroups = [];
    public readonly List<ModSettingDrawNode>  Drawing       = [];
    public readonly List<ModSettingsDrawLine> VerticalLines = [];

    public ModSettingPage(int id, string name)
        : base(new StringU8(name), StringU8.Empty)
        => Id = id;
}

public sealed record ModSettingOption(IModOption Data, StringU8 Name, StringU8 Description) : ModSettingDataNode(Name, Description)
{
    public readonly List<ModSettingGroup> Children        = [];
    public readonly List<ModSettingGroup> VisibleChildren = [];
    public          Vector4               Color;
    public          float                 Width;
    public          bool                  Radio;
    public          bool                  Separator;
    public          bool                  Space;
    public          bool                  HideLabel;
}

public sealed record ModSettingGroup(IModGroup Group, StringU8 Name, StringU8 Description) : ModSettingDataNode(Name, Description)
{
    public GroupDrawBehaviour Behaviour
        => Group.Behaviour;

    public readonly List<ModSettingOption>   AllOptions      = new(Group.Options.Count);
    public readonly List<ModSettingGroup>    GroupChildren   = [];
    public readonly List<ModSettingDataNode> VisibleChildren = [];
    public          float                    NameWidth;
    public          float                    ComboWidth = 100 * Im.Style.GlobalScale;
    public          int                      NumOptions;
    public          bool                     Space;
    public          bool                     IsCombo;
    public          bool                     IsSameLineOption;
    public          bool                     HideHeader;
    public          bool                     Collapsible;
}
