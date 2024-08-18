using Penumbra.Mods.Groups;

namespace Penumbra.Mods.SubMods;

public interface IModOption
{
    public Mod       Mod   { get; }
    public IModGroup Group { get; }

    public string Name        { get; set; }
    public string FullName    { get; }
    public string Description { get; set; }

    public int GetIndex();
}
