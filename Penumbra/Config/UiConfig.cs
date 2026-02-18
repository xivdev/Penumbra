using Luna;
using Luna.Generators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Services;
using MessageService = Penumbra.Services.MessageService;

namespace Penumbra;

public sealed partial class UiConfig : ConfigurationFile<FilenameService>
{
    public UiConfig(SaveService saveService, MessageService messager)
        : base(saveService, messager, TimeSpan.FromMinutes(5))
    {
        Load();
    }

    protected override void AddData(JsonTextWriter j)
    {
        j.WritePropertyName("CollectionsTab");
        j.WriteStartObject();
        j.WritePropertyName("Mode");
        j.WriteValue(CollectionsTabScale.Mode.ToString());
        j.WritePropertyName("Width");
        j.WriteValue(CollectionsTabScale.Width);
        j.WriteEndObject();
        j.WritePropertyName("ModsTab");
        j.WriteStartObject();
        j.WritePropertyName("Mode");
        j.WriteValue(ModTabScale.Mode.ToString());
        j.WritePropertyName("Width");
        j.WriteValue(ModTabScale.Width);
        j.WriteEndObject();
    }

    protected override void LoadData(JObject j)
    {
        if (j["CollectionsTab"] is JObject collections)
            _collectionsTabScale = new TwoPanelWidth(collections["Width"].ValueOr(float.NaN),
                collections["Mode"].TextEnum(ScalingMode.Percentage));

        if (j["ModsTab"] is JObject mods)
            _modTabScale = new TwoPanelWidth(mods["Width"].ValueOr(float.NaN), mods["Mode"].TextEnum(ScalingMode.Percentage));
    }

    public override int CurrentVersion
        => 1;

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.UiConfigFile;

    [ConfigProperty]
    private TwoPanelWidth _collectionsTabScale = new(0.25f, ScalingMode.Percentage);

    [ConfigProperty]
    private TwoPanelWidth _modTabScale = new(0.3f, ScalingMode.Percentage);
}
