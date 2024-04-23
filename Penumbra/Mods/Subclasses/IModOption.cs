using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Penumbra.Mods.Subclasses;

public interface IModOption
{
    public string Name        { get; set; }
    public string FullName    { get; }
    public string Description { get; set; }

    public static void Load(JToken json, IModOption option)
    {
        option.Name        = json[nameof(Name)]?.ToObject<string>() ?? string.Empty;
        option.Description = json[nameof(Description)]?.ToObject<string>() ?? string.Empty;
    }

    public static void WriteModOption(JsonWriter j, IModOption option)
    {
        j.WritePropertyName(nameof(Name));
        j.WriteValue(option.Name);
        j.WritePropertyName(nameof(Description));
        j.WriteValue(option.Description);
    }
}
