using Newtonsoft.Json;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

public interface IModGroup : IReadOnlyCollection<SubMod>
{
    public const int MaxMultiOptions = 63;

    public string      Name            { get; }
    public string      Description     { get; }
    public GroupType   Type            { get; }
    public ModPriority Priority        { get; }
    public Setting     DefaultSettings { get; set; }

    public ModPriority OptionPriority(Index optionIdx);

    public SubMod this[Index idx] { get; }

    public bool IsOption { get; }

    public IModGroup Convert(GroupType type);
    public bool      MoveOption(int optionIdxFrom, int optionIdxTo);
    public void      UpdatePositions(int from = 0);

    public void AddData(Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations);

    /// <summary> Ensure that a value is valid for a group. </summary>
    public Setting FixSetting(Setting setting);
}

public readonly struct ModSaveGroup : ISavable
{
    private readonly DirectoryInfo _basePath;
    private readonly IModGroup?    _group;
    private readonly int           _groupIdx;
    private readonly SubMod?      _defaultMod;
    private readonly bool          _onlyAscii;

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

    public ModSaveGroup(DirectoryInfo basePath, SubMod @default, bool onlyAscii)
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
        if (_groupIdx >= 0)
        {
            j.WriteStartObject();
            j.WritePropertyName(nameof(_group.Name));
            j.WriteValue(_group!.Name);
            j.WritePropertyName(nameof(_group.Description));
            j.WriteValue(_group.Description);
            j.WritePropertyName(nameof(_group.Priority));
            j.WriteValue(_group.Priority.Value);
            j.WritePropertyName(nameof(Type));
            j.WriteValue(_group.Type.ToString());
            j.WritePropertyName(nameof(_group.DefaultSettings));
            j.WriteValue(_group.DefaultSettings.Value);
            switch (_group)
            {
                case SingleModGroup single:
                    j.WritePropertyName("Options");
                    j.WriteStartArray();
                    foreach (var option in single.OptionData)
                        SubMod.WriteSubMod(j, serializer, option, _basePath, null);
                    j.WriteEndArray();
                    j.WriteEndObject();
                    break;
                case MultiModGroup multi:
                    j.WritePropertyName("Options");
                    j.WriteStartArray();
                    foreach (var (option, priority) in multi.PrioritizedOptions)
                        SubMod.WriteSubMod(j, serializer, option, _basePath, priority);
                    j.WriteEndArray();
                    j.WriteEndObject();
                    break;
            }
        }
        else
        {
            SubMod.WriteSubMod(j, serializer, _defaultMod!, _basePath, null);
        }
    }
}
