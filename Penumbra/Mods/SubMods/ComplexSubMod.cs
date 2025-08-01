using Newtonsoft.Json.Linq;
using OtterGui.Extensions;
using Penumbra.Mods.Groups;

namespace Penumbra.Mods.SubMods;

public sealed class ComplexSubMod(ComplexModGroup group) : IModOption
{
    public Mod Mod
        => group.Mod;

    public IModGroup Group { get; }      = group;
    public string    Name  { get; set; } = "Option";

    public string FullName
        => $"{Group.Name}: {Name}";

    public MaskedSetting Conditions    = MaskedSetting.Zero;
    public int           Indentation   = 0;
    public string        SubGroupLabel = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int GetIndex()
        => Group.Options.IndexOf(this);

    public ComplexSubMod(ComplexModGroup group, JToken json)
        : this(group)
    {
        SubMod.LoadOptionData(json, this);
        var mask  = json["ConditionMask"]?.ToObject<ulong>() ?? 0;
        var value = json["ConditionMask"]?.ToObject<ulong>() ?? 0;
        Conditions    = new MaskedSetting(mask, value);
        Indentation   = json["Indentation"]?.ToObject<int>() ?? 0;
        SubGroupLabel = json["SubGroup"]?.ToObject<string>() ?? string.Empty;
    }
}
