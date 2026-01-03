using Dalamud.Interface.ImGuiNotification;
using Luna;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Services;

namespace Penumbra;

public class UiConfig : ISavable, IService
{
    public const int CurrentVersion = 1;

    [JsonIgnore]
    private readonly SaveService _saveService;

    public UiConfig(SaveService saveService)
    {
        _saveService = saveService;
        Load();
    }

    private TwoPanelWidth _collectionsTabScale = new(0.25f, ScalingMode.Percentage);

    public TwoPanelWidth CollectionTabScale
    {
        get => _collectionsTabScale;
        set
        {
            if (value == _collectionsTabScale)
                return;

            _collectionsTabScale = value;
            Save();
        }
    }

    private TwoPanelWidth _modTabScale = new(0.3f, ScalingMode.Percentage);

    public TwoPanelWidth ModTabScale
    {
        get => _modTabScale;
        set
        {
            if (value == _modTabScale)
                return;

            _modTabScale = value;
            Save();
        }
    }

    public string ToFilePath(FilenameService fileNames)
        => fileNames.UiConfigFile;

    public void Save()
        => _saveService.DelaySave(this);

    public void Save(StreamWriter writer)
    {
        using var j = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        j.WriteStartObject();
        j.WritePropertyName("Version");
        j.WriteValue(CurrentVersion);
        j.WritePropertyName("CollectionsTab");
        j.WriteStartObject();
        j.WritePropertyName("Mode");
        j.WriteValue(CollectionTabScale.Mode.ToString());
        j.WritePropertyName("Width");
        j.WriteValue(CollectionTabScale.Width);
        j.WriteEndObject();
        j.WritePropertyName("ModsTab");
        j.WriteStartObject();
        j.WritePropertyName("Mode");
        j.WriteValue(ModTabScale.Mode.ToString());
        j.WritePropertyName("Width");
        j.WriteValue(ModTabScale.Width);
        j.WriteEndObject();
        j.WriteEndObject();
    }

    private void Load()
    {
        if (!File.Exists(_saveService.FileNames.UiConfigFile))
            return;

        try
        {
            var text = File.ReadAllText(_saveService.FileNames.UiConfigFile);
            var jObj = JObject.Parse(text);
            if (jObj["Version"]?.Value<int>() is not CurrentVersion)
                throw new Exception("Unsupported version.");

            if (jObj["CollectionsTab"] is JObject collections)
                _collectionsTabScale = new TwoPanelWidth(collections["Width"].ValueOr(float.NaN),
                    collections["Mode"].TextEnum(ScalingMode.Percentage));

            if (jObj["ModsTab"] is JObject mods)
                _modTabScale = new TwoPanelWidth(mods["Width"].ValueOr(float.NaN), mods["Mode"].TextEnum(ScalingMode.Percentage));
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex,
                "Error reading UI Configuration, reverting to default.",
                "Error reading UI Configuration", NotificationType.Error);
        }
    }
}
