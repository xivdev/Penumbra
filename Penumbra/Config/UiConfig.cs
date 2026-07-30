using System.Text.Json;
using Luna;
using Luna.Generators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Files;
using Penumbra.UI.Classes;
using MessageService = Penumbra.Services.MessageService;

namespace Penumbra;

public sealed partial class UiConfig : ConfigurationFile<FilenameService>, IDisposable
{
    [JsonIgnore]
    public readonly ColorCache<ColorId, ColorIdData> ColorCache;

    public UiConfig(SaveService saveService, MessageService messager)
        : base(saveService, messager, TimeSpan.FromMinutes(5))
    {
        ColorCache = new ColorCache<ColorId, ColorIdData>(Colors);
        Load();
        UI.Classes.Colors.SetCache(ColorCache);
    }

    protected override void AddData(Utf8JsonWriter j)
    {
        j.WritePropertyName("Colors"u8);
        Colors.Serialize(j, false);
        CollectionsTabScale.WriteJson(j, "CollectionsTab"u8);
        ModTabScale.WriteJson(j, "ModsTab"u8);
        if (ModSettingItemSpacingFactor is not 1)
            j.WriteNumber("ModSettingItemSpacingFactor"u8, ModSettingItemSpacingFactor);
        if (ModSettingLabelAlignment is not 0)
            j.WriteNumber("ModSettingLabelAlignment"u8, ModSettingLabelAlignment);
        if (ModSettingMaximumExtendLabelWidth is not 200f)
            j.WriteNumber("ModSettingMaximumExtendLabelWidth"u8, ModSettingMaximumExtendLabelWidth);
        if (ModSettingBorderScale is not 2f)
            j.WriteNumber("ModSettingBorderScale"u8, ModSettingBorderScale);
        if (ModSettingLineScale is not 2f)
            j.WriteNumber("ModSettingLineScale"u8, ModSettingLineScale);
    }

    protected override void LoadData(JObject j)
    {
        // TODO: Optimize this entire type to not use newtonsoft...
        _collectionsTabScale               = TwoPanelWidth.ReadJson(j, "CollectionsTab", new TwoPanelWidth(0.25f, ScalingMode.Percentage));
        _modTabScale                       = TwoPanelWidth.ReadJson(j, "ModsTab",        new TwoPanelWidth(0.3f,  ScalingMode.Percentage));
        _modSettingItemSpacingFactor       = j["ModSettingItemSpacingFactor"]?.ToObject<float>() ?? 1f;
        _modSettingLabelAlignment          = j["ModSettingLabelAlignment"]?.ToObject<float>() ?? 0f;
        _modSettingMaximumExtendLabelWidth = j["ModSettingMaximumExtendLabelWidth"]?.ToObject<float>() ?? 200f;
        _modSettingBorderScale             = j["ModSettingBorderScale"]?.ToObject<float>() ?? 2f;
        _modSettingLineScale               = j["ModSettingLineScale"]?.ToObject<float>() ?? 2f;

        if (j["Colors"] is { } token)
        {
            var backToText = Encoding.UTF8.GetBytes(token.ToString(Formatting.None));
            if (backToText.Length > 0)
            {
                var reader = new Utf8JsonReader(backToText, JsonFunctions.ReaderOptions);
                if (reader.Read())
                {
                    var colors = ColorDictionary<ColorId, ColorIdData>.Deserialize(Messager, ref reader, true, true, true);
                    Colors.Apply(colors, true);
                }
            }
        }
        else
        {
            Colors.ResetToDefault();
        }
    }

    public override int CurrentVersion
        => 1;

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.UiConfigFile;

    public readonly ColorDictionary<ColorId, ColorIdData> Colors = new();

    [ConfigProperty]
    private TwoPanelWidth _collectionsTabScale = new(0.25f, ScalingMode.Percentage);

    [ConfigProperty]
    private TwoPanelWidth _modTabScale = new(0.3f, ScalingMode.Percentage);

    [ConfigProperty]
    private float _modSettingItemSpacingFactor = 1f;

    [ConfigProperty]
    private float _modSettingBorderScale = 2f;

    [ConfigProperty]
    private float _modSettingLineScale = 2f;

    [ConfigProperty]
    private float _modSettingMaximumExtendLabelWidth = 200f;

    [ConfigProperty]
    private float _modSettingLabelAlignment;

    public void Dispose()
    {
        UI.Classes.Colors.SetCache(null!);
        ColorCache.Dispose();
    }
}
