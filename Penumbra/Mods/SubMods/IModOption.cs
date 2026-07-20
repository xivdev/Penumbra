using ImSharp;
using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.UI.Classes;

namespace Penumbra.Mods.SubMods;

[Flags]
public enum ModSettingsLayout : ulong
{
    None            = 0,
    Disable         = 0x01, // Disable the option or group instead of hiding it when the conditions are not fulfilled.
    Indent          = 0x02, // Indent the group if it is placed under another option or group.
    ParentHeader    = 0x04, // Show the groups name or just its options if it is placed under another option or group.
    Separator       = 0x08, // Add a separator after this option.
    DefaultClosed   = 0x10, // A group should be closed by default.
    HideOptionLabel = 0x20, // An option's label should not be drawn when it is in a single line checkbox.
}

public interface IModObject : CycleChecker.IHasParent<IModObject>, IEquatable<IModObject>
{
    public Mod                            Mod         { get; }
    public IModGroup                      Group       { get; }
    public Guid                           Id          { get; set; }
    public string                         Name        { get; set; }
    public string                         Description { get; set; }
    public ModSettingsLayout              Layout      { get; set; }
    public ICondition<ModSettingContext>? Condition   { get; set; }

    bool IEquatable<IModObject>.Equals(IModObject? other)
        => ReferenceEquals(this, other);
}

public interface IModOption : IModObject, IIndexed
{
    public string FullName { get; }

    public int GroupIndex
        => Group.Index;

    public ColorId Color { get; set; }

    IModObject? CycleChecker.IHasParent<IModObject>.Parent
        => Group.Parent;

    public static ColorId ConvertColor(int color)
        => color switch
        {
            1 => ColorId.OptionColor1,
            2 => ColorId.OptionColor2,
            3 => ColorId.OptionColor3,
            4 => ColorId.OptionColor4,
            5 => ColorId.OptionColor5,
            6 => ColorId.OptionColor6,
            7 => ColorId.OptionColor7,
            8 => ColorId.OptionColor8,
            _ => default,
        };

    public Vector4 ColorValue
        => ColorAsInteger is 0 ? Im.Style[ImGuiColor.Text] : Color.Value().ToVector();

    public int ColorAsInteger
        => Color switch
        {
            ColorId.OptionColor1 => 1,
            ColorId.OptionColor2 => 2,
            ColorId.OptionColor3 => 3,
            ColorId.OptionColor4 => 4,
            ColorId.OptionColor5 => 5,
            ColorId.OptionColor6 => 6,
            ColorId.OptionColor7 => 7,
            ColorId.OptionColor8 => 8,
            _                    => 0,
        };

    public bool IsEnabled(ModSettings settings)
    {
        var setting = settings.IsEmpty || settings.Settings.Count <= GroupIndex
            ? Group.DefaultSettings
            : settings.Settings[GroupIndex];
        if (Group.Behaviour is GroupDrawBehaviour.MultiSelection)
            return setting.HasFlag(Index);

        return setting.AsIndex == Index;
    }
}

public static class ModSettingsLayoutExtensions
{
    public const ModSettingsLayout GroupValid = ModSettingsLayout.Disable
      | ModSettingsLayout.Indent
      | ModSettingsLayout.ParentHeader
      | ModSettingsLayout.DefaultClosed;

    public const ModSettingsLayout OptionValid = ModSettingsLayout.Disable | ModSettingsLayout.Separator | ModSettingsLayout.HideOptionLabel;

    extension(ModSettingsLayout layout)
    {
        public IEnumerable<ModSettingsLayout> Iterate()
        {
            if (layout.HasFlag(ModSettingsLayout.Disable))
                yield return ModSettingsLayout.Disable;
            if (layout.HasFlag(ModSettingsLayout.Indent))
                yield return ModSettingsLayout.Indent;
            if (layout.HasFlag(ModSettingsLayout.ParentHeader))
                yield return ModSettingsLayout.ParentHeader;
            if (layout.HasFlag(ModSettingsLayout.Separator))
                yield return ModSettingsLayout.Separator;
            if (layout.HasFlag(ModSettingsLayout.DefaultClosed))
                yield return ModSettingsLayout.DefaultClosed;
            if (layout.HasFlag(ModSettingsLayout.HideOptionLabel))
                yield return ModSettingsLayout.HideOptionLabel;
        }

        public ModSettingsLayout Reduce(IModObject @object)
            => @object switch
            {
                IModGroup  => layout & GroupValid,
                IModOption => layout & OptionValid,
                _          => 0,
            };
    }
}
