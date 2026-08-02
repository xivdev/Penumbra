using System.Text.Json;
using ImSharp;
using Luna;
using Luna.Generators;
using Newtonsoft.Json.Linq;
using Penumbra.Api.Enums;
using Penumbra.Files;

namespace Penumbra;

public sealed partial class EditingConfig(SaveService saveService, MessageService messager)
    : ConfigurationFile<FilenameService>(saveService, messager)
{
    [ConfigProperty]
    private long _lowerSizeLimit = 1 << 20;

    [ConfigProperty]
    private int _smallDimensionLimit = 32;

    [ConfigProperty]
    private int _largeDimensionLimit = 4096;

    [ConfigProperty]
    private int _textureDimensionLimit = 4096;

    [ConfigProperty]
    private bool _createBackups = true;

    [ConfigProperty]
    private bool _defaultEditWindowModPinned = true;

    [ConfigProperty]
    private bool _editRawTileTransforms = false;

    [ConfigProperty]
    private bool _wholePairSelectorAlwaysHighlights = false;

    [ConfigProperty]
    private bool _allDyeChannels = false;

    [ConfigProperty]
    private Dictionary<ResourceType, string> _preferredEditorFactories = [];

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        using (var tempObject = j.TemporaryObject("Advanced"u8))
        {
            tempObject.WriteIfNot("DefaultEditWindowModPinned"u8, DefaultEditWindowModPinned, true);
            if (PreferredEditorFactories.Count is not 0)
            {
                tempObject.WriteProperty("PreferredEditorFactories"u8);
                j.WriteStartObject();
                foreach (var (resource, text) in PreferredEditorFactories)
                    j.WriteString(resource.StringU8, text);
                j.WriteEndObject();
            }
        }

        using (var tempObject = j.TemporaryObject("Materials"u8))
        {
            tempObject.WriteIfNot("EditRawTileTransforms"u8,             EditRawTileTransforms,             false);
            tempObject.WriteIfNot("WholePairSelectorAlwaysHighlights"u8, WholePairSelectorAlwaysHighlights, false);
            tempObject.WriteIfNot("AllDyeChannels"u8,                    AllDyeChannels,                    false);
        }

        using (var tempObject = j.TemporaryObject("TextureManagement"u8))
        {
            tempObject.WriteIfNot("LowerSizeLimit"u8,        LowerSizeLimit,        1L << 20);
            tempObject.WriteIfNot("SmallDimensionLimit"u8,   SmallDimensionLimit,   32);
            tempObject.WriteIfNot("LargeDimensionLimit"u8,   LargeDimensionLimit,   4096);
            tempObject.WriteIfNot("TextureDimensionLimit"u8, TextureDimensionLimit, 4096);
            tempObject.WriteIfNot("CreateBackups"u8,         CreateBackups,         true);
        }
    }

    protected override void LoadData(JObject j)
    {
        throw new NotImplementedException();
    }

    public override string ToFilePath(FilenameService fileNames)
        => throw new NotImplementedException();
}
