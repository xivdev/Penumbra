using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using Penumbra.Services;

namespace Penumbra.Mods.Subclasses;

public interface IModGroup : IEnumerable<ISubMod>
{
    public const int MaxMultiOptions = 32;

    public string    Name            { get; }
    public string    Description     { get; }
    public GroupType Type            { get; }
    public int       Priority        { get; }
    public uint      DefaultSettings { get; set; }

    public int OptionPriority(Index optionIdx);

    public ISubMod this[Index idx] { get; }

    public int Count { get; }

    public bool IsOption
        => Type switch
        {
            GroupType.Single => Count > 1,
            GroupType.Multi  => Count > 0,
            _                => false,
        };

    public IModGroup Convert(GroupType type);
    public bool      MoveOption(int optionIdxFrom, int optionIdxTo);
    public void      UpdatePositions(int from = 0);
}

public readonly struct ModSaveGroup : ISavable
{
    private readonly DirectoryInfo _basePath;
    private readonly IModGroup?    _group;
    private readonly int           _groupIdx;
    private readonly ISubMod?      _defaultMod;

    public ModSaveGroup(Mod mod, int groupIdx)
    {
        _basePath = mod.ModPath;
        _groupIdx = groupIdx;
        if (_groupIdx < 0)
            _defaultMod = mod.Default;
        else
            _group = mod.Groups[_groupIdx];
    }

    public ModSaveGroup(DirectoryInfo basePath, IModGroup group, int groupIdx)
    {
        _basePath = basePath;
        _group    = group;
        _groupIdx = groupIdx;
    }

    public ModSaveGroup(DirectoryInfo basePath, ISubMod @default)
    {
        _basePath   = basePath;
        _groupIdx   = -1;
        _defaultMod = @default;
    }

    public string ToFilename(FilenameService fileNames)
        => fileNames.OptionGroupFile(_basePath.FullName, _groupIdx, _group?.Name ?? string.Empty);

    public void Save(StreamWriter writer)
    {
        using var j          = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
        var       serializer = new JsonSerializer { Formatting         = Formatting.Indented };
        if (_groupIdx >= 0)
        {
            j.WriteStartObject();
            j.WritePropertyName(nameof(_group.Name));
            j.WriteValue(_group!.Name);
            j.WritePropertyName(nameof(_group.Description));
            j.WriteValue(_group.Description);
            j.WritePropertyName(nameof(_group.Priority));
            j.WriteValue(_group.Priority);
            j.WritePropertyName(nameof(Type));
            j.WriteValue(_group.Type.ToString());
            j.WritePropertyName(nameof(_group.DefaultSettings));
            j.WriteValue(_group.DefaultSettings);
            j.WritePropertyName("Options");
            j.WriteStartArray();
            for (var idx = 0; idx < _group.Count; ++idx)
                ISubMod.WriteSubMod(j, serializer, _group[idx], _basePath, _group.Type == GroupType.Multi ? _group.OptionPriority(idx) : null);

            j.WriteEndArray();
            j.WriteEndObject();
        }
        else
        {
            ISubMod.WriteSubMod(j, serializer, _defaultMod!, _basePath, null);
        }
    }
}
