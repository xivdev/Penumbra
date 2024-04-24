using Newtonsoft.Json;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Groups;

public readonly struct ModSaveGroup : ISavable
{
    private readonly DirectoryInfo  _basePath;
    private readonly IModGroup?     _group;
    private readonly int            _groupIdx;
    private readonly DefaultSubMod? _defaultMod;
    private readonly bool           _onlyAscii;

    public ModSaveGroup(Mod mod, int groupIdx, bool onlyAscii)
    {
        _basePath = mod.ModPath;
        _groupIdx = groupIdx;
        if (_groupIdx < 0)
            _defaultMod = mod.Default;
        else
            _group = mod.Groups[_groupIdx];
        _onlyAscii = onlyAscii;
    }

    public ModSaveGroup(DirectoryInfo basePath, IModGroup group, int groupIdx, bool onlyAscii)
    {
        _basePath  = basePath;
        _group     = group;
        _groupIdx  = groupIdx;
        _onlyAscii = onlyAscii;
    }

    public ModSaveGroup(DirectoryInfo basePath, DefaultSubMod @default, bool onlyAscii)
    {
        _basePath   = basePath;
        _groupIdx   = -1;
        _defaultMod = @default;
        _onlyAscii  = onlyAscii;
    }

    public string ToFilename(FilenameService fileNames)
        => fileNames.OptionGroupFile(_basePath.FullName, _groupIdx, _group?.Name ?? string.Empty, _onlyAscii);

    public void Save(StreamWriter writer)
    {
        using var j = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer { Formatting = Formatting.Indented };
        j.WriteStartObject();
        if (_groupIdx >= 0)
            _group!.WriteJson(j, serializer);
        else
            IModDataContainer.WriteModData(j, serializer, _defaultMod!, _basePath);
        j.WriteEndObject();
    }
}
