using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;


public interface IModDataContainer
{
    public IMod Mod { get; }
    public IModGroup? Group { get; }

    public Dictionary<Utf8GamePath, FullPath> Files { get; set; }
    public Dictionary<Utf8GamePath, FullPath> FileSwaps { get; set; }
    public HashSet<MetaManipulation> Manipulations { get; set; }

    public string GetName()
        => this switch
        {
            IModOption o  => o.FullName,
            DefaultSubMod => DefaultSubMod.FullName,
            _             => $"Container {GetDataIndices().DataIndex + 1}",
        };

    public string GetFullName()
        => this switch
        {
            IModOption o         => o.FullName,
            DefaultSubMod        => DefaultSubMod.FullName,
            _ when Group != null => $"{Group.Name}: Container {GetDataIndices().DataIndex + 1}",
            _                    => $"Container {GetDataIndices().DataIndex + 1}",
        };

    public (int GroupIndex, int DataIndex) GetDataIndices();
}

public interface IModDataOption : IModOption, IModDataContainer;
