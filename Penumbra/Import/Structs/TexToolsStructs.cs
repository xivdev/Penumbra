using Penumbra.Api.Enums;

namespace Penumbra.Import.Structs;

internal static class DefaultTexToolsData
{
    public const string Name          = "New Mod";
    public const string Author        = "Unknown";
    public const string Description   = "Mod imported from TexTools mod pack.";
    public const string DefaultOption = "Default";
}

[Serializable]
public class SimpleMod
{
    public string  Name         = string.Empty;
    public string  Category     = string.Empty;
    public string  FullPath     = string.Empty;
    public string  DatFile      = string.Empty;
    public long    ModOffset    = 0;
    public long    ModSize      = 0;
    public object? ModPackEntry = null;
}

[Serializable]
public class ModPackPage
{
    public int        PageIndex = 0;
    public ModGroup[] ModGroups = Array.Empty<ModGroup>();
}

[Serializable]
public class ModGroup
{
    public string       GroupName     = string.Empty;
    public GroupType    SelectionType = GroupType.Single;
    public OptionList[] OptionList    = Array.Empty<OptionList>();
    public string       Description   = string.Empty;
}

[Serializable]
public class OptionList
{
    public string      Name          = string.Empty;
    public string      Description   = string.Empty;
    public string      ImagePath     = string.Empty;
    public SimpleMod[] ModsJsons     = Array.Empty<SimpleMod>();
    public string      GroupName     = string.Empty;
    public GroupType   SelectionType = GroupType.Single;
    public bool        IsChecked     = false;
}

[Serializable]
public class ExtendedModPack
{
    public string        PackVersion    = string.Empty;
    public string        Name           = DefaultTexToolsData.Name;
    public string        Author         = DefaultTexToolsData.Author;
    public string        Version        = string.Empty;
    public string        Description    = DefaultTexToolsData.Description;
    public string        Url            = string.Empty;
    public ModPackPage[] ModPackPages   = Array.Empty<ModPackPage>();
    public SimpleMod[]   SimpleModsList = Array.Empty<SimpleMod>();
}

[Serializable]
public class SimpleModPack
{
    public string      TtmpVersion    = string.Empty;
    public string      Name           = DefaultTexToolsData.Name;
    public string      Author         = DefaultTexToolsData.Author;
    public string      Version        = string.Empty;
    public string      Description    = DefaultTexToolsData.Description;
    public string      Url            = string.Empty;
    public SimpleMod[] SimpleModsList = Array.Empty<SimpleMod>();
}
