using System.Text.Json;
using ImSharp;
using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
using Penumbra.Files;
using Penumbra.Services;

namespace Penumbra;

public sealed partial class EditingConfig : ConfigurationFile<FilenameService>
{
    #region Texture Management

    [ConfigProperty]
    private long _lowerTextureSizeLimit = 1 << 20;

    [ConfigProperty]
    private int _smallTextureDimensionLimit = 32;

    [ConfigProperty]
    private int _largeTextureDimensionLimit = 4096;

    [ConfigProperty]
    private int _textureDimensionLimit = 4096;

    [ConfigProperty]
    private bool _createTextureBackups = true;

    #endregion

    #region Materials

    [ConfigProperty]
    private bool _editRawTileTransforms = false;

    [ConfigProperty]
    private bool _wholePairSelectorAlwaysHighlights = false;

    [ConfigProperty]
    private bool _allDyeChannels = false;

    #endregion

    #region Advanced

    [ConfigProperty]
    private bool _defaultEditWindowModPinned = true;

    [ConfigProperty]
    private Dictionary<ResourceType, string> _preferredEditorFactories = [];

    /// <inheritdoc/>
    public EditingConfig(SaveService saveService, PenumbraMessager messager)
        : base(saveService, messager)
        => Load();

    #endregion

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        using (var tempObject = j.TemporaryObject("Materials"u8))
        {
            tempObject.WriteIfNot("EditRawTileTransforms"u8,             EditRawTileTransforms,             false);
            tempObject.WriteIfNot("WholePairSelectorAlwaysHighlights"u8, WholePairSelectorAlwaysHighlights, false);
            tempObject.WriteIfNot("AllDyeChannels"u8,                    AllDyeChannels,                    false);
        }

        using (var tempObject = j.TemporaryObject("TextureManagement"u8))
        {
            tempObject.WriteIfNot("LowerSizeLimit"u8,        LowerTextureSizeLimit,      1L << 20);
            tempObject.WriteIfNot("SmallDimensionLimit"u8,   SmallTextureDimensionLimit, 32);
            tempObject.WriteIfNot("LargeDimensionLimit"u8,   LargeTextureDimensionLimit, 4096);
            tempObject.WriteIfNot("TextureDimensionLimit"u8, TextureDimensionLimit,      4096);
            tempObject.WriteIfNot("CreateBackups"u8,         CreateTextureBackups,       true);
        }

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
    }

    protected override void LoadData(in JsonElement j)
    {
        if (j.TryReadObject("Materials"u8, out var materials))
        {
            EditRawTileTransforms = materials.PropertyOrDefault("EditRawTileTransforms"u8, EditRawTileTransforms);
            WholePairSelectorAlwaysHighlights = materials.PropertyOrDefault("WholePairSelectorAlwaysHighlights"u8, WholePairSelectorAlwaysHighlights);
            AllDyeChannels = materials.PropertyOrDefault("AllDyeChannels"u8, AllDyeChannels);
        }

        if (j.TryReadObject("TextureManagement"u8, out var textures))
        {
            LowerTextureSizeLimit = textures.PropertyOrDefault("LowerSizeLimit"u8, LowerTextureSizeLimit);
            SmallTextureDimensionLimit = textures.PropertyOrDefault("SmallDimensionLimit"u8, SmallTextureDimensionLimit);
            LargeTextureDimensionLimit = textures.PropertyOrDefault("LargeDimensionLimit"u8, LargeTextureDimensionLimit);
            TextureDimensionLimit = textures.PropertyOrDefault("TextureDimensionLimit"u8, TextureDimensionLimit);
            CreateTextureBackups = textures.PropertyOrDefault("CreateBackups"u8, CreateTextureBackups);
        }

        if (j.TryReadObject("Advanced"u8, out var advanced))
        {
            DefaultEditWindowModPinned = advanced.PropertyOrDefault("DefaultEditWindowModPinned"u8, DefaultEditWindowModPinned);
            if (advanced.TryReadObject("PreferredEditorFactories"u8, out var factories))
                PreferredEditorFactories = factories.Deserialize<Dictionary<ResourceType, string>>() ?? PreferredEditorFactories;
        }
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.Config.Editing;
}
