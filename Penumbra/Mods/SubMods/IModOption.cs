namespace Penumbra.Mods.SubMods;

public interface IModOption
{
    public string Name        { get; set; }
    public string FullName    { get; }
    public string Description { get; set; }

    public (int GroupIndex, int OptionIndex) GetOptionIndices();
}
