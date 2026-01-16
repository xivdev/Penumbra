using Dalamud.Interface.ImGuiNotification;
using Luna;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Services;

namespace Penumbra;

public abstract class ConfigurationFile(SaveService saveService, TimeSpan? saveDelay = null) : ISavable, IService
{
    public abstract int CurrentVersion { get; }

    [JsonIgnore]
    protected readonly SaveService SaveService = saveService;

    public virtual void Save()
        => SaveService.DelaySave(this, SaveDelay);

    protected TimeSpan SaveDelay { get; set; } = saveDelay ?? TimeSpan.FromMinutes(1);

    public virtual void Save(StreamWriter writer)
    {
        using var j = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        j.WriteStartObject();
        j.WritePropertyName("Version");
        j.WriteValue(CurrentVersion);
        AddData(j);
        j.WriteEndObject();
    }

    protected abstract void AddData(JsonTextWriter j);
    protected abstract void LoadData(JObject j);

    public abstract string ToFilePath(FilenameService fileNames);

    protected virtual void Load()
    {
        var fileName = ToFilePath(SaveService.FileNames);
        var logName  = ((ISavable)this).LogName(fileName);
        if (!File.Exists(fileName))
            return;

        try
        {
            Penumbra.Log.Debug($"Reading {logName}...");
            var text = File.ReadAllText(fileName);
            var jObj = JObject.Parse(text);
            if (jObj["Version"]?.Value<int>() != CurrentVersion)
                throw new Exception("Unsupported version.");

            LoadData(jObj);
        }
        catch (Exception ex)
        {
            Penumbra.Messager.NotificationMessage(ex, $"Error reading {logName}, reverting to default.",
                $"Error reading {logName}", NotificationType.Error);
        }
    }
}
