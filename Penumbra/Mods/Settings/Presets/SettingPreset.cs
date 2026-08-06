using System.Text.Json;
using ImSharp;
using Luna;

namespace Penumbra.Mods.Settings;

public sealed class SettingPreset
{
    public Guid               Identifier { get; init; } = Guid.NewGuid();
    public string             Name       { get; set; }  = "Preset";
    public DateTimeOffset     LastEdit   { get; set; }  = DateTimeOffset.UtcNow;
    public SettingsDictionary Settings   { get; set; }  = [];
    public ModPriority?       Priority   { get; set; }
    public ModState           State      { get; set; }

    public static SettingPreset ParseJson(ref Utf8JsonReader j, out string mod)
    {
        var obj = j.CreateObjectLimit();
        mod = string.Empty;
        Guid?              identifier = null;
        string?            name       = null;
        ModPriority?       priority   = null;
        var                lastEdit   = DateTimeOffset.UtcNow;
        SettingsDictionary dict       = [];
        var                state      = ModState.Ignored;
        while (obj.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                continue;

            if (j.NumberProperty("Version"u8, out int version))
                HandleVersion(version);
            else if (j.GuidProperty("Identifier"u8, out var g))
                identifier = g;
            else if (j.StringProperty("Name"u8, out string? n, true))
                name = n;
            else if (j.EnumProperty<ModState>("State"u8, out var s))
                state = s;
            else if (j.ArrayProperty("Settings"u8, out var settings, true))
                dict.ReadJson(ref j, settings);
            else if (j.StringProperty("Mod"u8, out string? m, true))
                mod = m ?? string.Empty;
            else if (j.NumberProperty("LastEdit"u8, out long? timestamp) && timestamp.HasValue)
                lastEdit = DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value);
            else
                j.Skip();
        }

        if (identifier is null)
            throw new Exception("No identifier provided for setting preset.");

        return new SettingPreset
        {
            Identifier = identifier.Value,
            Name       = name ?? "Preset",
            Priority   = priority,
            LastEdit   = lastEdit,
            State      = state,
            Settings   = dict,
        };
    }

    private static void HandleVersion(int version)
    {
        if (version is not 1)
            throw new Exception($"Invalid Setting Preset with unknown version {version}.");
    }

    public void WriteJson(Utf8JsonWriter writer, string mod, int? version)
    {
        writer.WriteStartObject();
        if (version.HasValue)
            writer.WriteNumber("Version"u8, version.Value);
        writer.WriteString("Identifier"u8, Identifier);
        writer.WriteNumber("LastEdit"u8, LastEdit.ToUnixTimeMilliseconds());
        writer.WriteNonEmptyString("Mod"u8,   mod);
        writer.WriteNonEmptyString("Name"u8,  Name);
        writer.WriteString("State"u8, State.StringU8);
        writer.WritePropertyName("Priority"u8);
        if (Priority is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(Priority.Value.Value);
        if (Settings.Count > 0)
        {
            writer.WritePropertyName("Settings"u8);
            Settings.WriteJson(writer);
        }

        writer.WriteEndObject();
    }

    public bool UpdateName(string newName)
    {
        if (Name == newName)
            return false;

        Name     = newName;
        LastEdit = DateTimeOffset.UtcNow;
        return true;
    }

    public bool SetStateIgnored()
    {
        if (State is ModState.Ignored)
            return false;

        State    = ModState.Ignored;
        LastEdit = DateTimeOffset.UtcNow;
        return true;
    }

    public bool SetPriorityIgnored()
    {
        if (Priority is null)
            return false;

        Priority = null;
        LastEdit = DateTimeOffset.UtcNow;
        return true;
    }

    public bool UpdateState(bool? state, bool force)
    {
        if (State is ModState.Ignored && !force)
            return false;

        var newState = state switch
        {
            null  => ModState.Inherited,
            true  => ModState.Enabled,
            false => ModState.Disabled,
        };
        if (newState == State)
            return false;

        State    = newState;
        LastEdit = DateTimeOffset.UtcNow;
        return true;
    }

    public bool UpdatePriority(ModPriority? priority, bool force)
    {
        if (Priority is null && !force)
            return false;

        var newPriority = priority ?? ModPriority.Default;
        if (newPriority == Priority)
            return false;

        Priority = newPriority;
        LastEdit = DateTimeOffset.UtcNow;
        return true;
    }

    public bool Update(Mod mod, ModSettings? settings)
    {
        var ret = false;
        ret |= UpdateState(settings?.Enabled, false);
        ret |= UpdatePriority(settings?.Priority, false);

        var newSettings = new SettingsDictionary(mod, settings);
        foreach (var (group, data) in Settings)
        {
            foreach (var ignored in data.Ignored)
                newSettings.SetIdentifier(group, ignored, null);
        }

        if (!Settings.Equals(newSettings))
        {
            Settings = newSettings;
            ret      = true;
            LastEdit = DateTimeOffset.UtcNow;
        }

        return ret;
    }
}
