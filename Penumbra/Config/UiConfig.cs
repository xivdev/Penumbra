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
        CollectionsTabScale.WriteJson(j, "CollectionsTab");
        ModTabScale.WriteJson(j, "ModsTab");
    }

    protected override void LoadData(JObject j)
    {
        _collectionsTabScale = TwoPanelWidth.ReadJson(j, "CollectionsTab", new TwoPanelWidth(0.25f, ScalingMode.Percentage));
        _modTabScale         = TwoPanelWidth.ReadJson(j, "ModsTab",        new TwoPanelWidth(0.3f,  ScalingMode.Percentage));
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
