using System.Text.Json;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.Groups;

public readonly record struct ModSettingContext(Mod Mod, ModSettings Settings);

public sealed record MultiSettingAllCondition(string Group, string[] Options) : ICondition<ModSettingContext>
{
    public bool Evaluate(in ModSettingContext context)
    {
        if (Options.Length is 0)
            return true;

        foreach (var (index, group) in context.Mod.Groups.Index())
        {
            if (group.Name != Group)
                continue;

            if (group.Type is GroupType.Single && Options.Length > 1)
                continue;

            var settings = context.Settings.Settings[index];
            var matches  = 0;
            foreach (var option in Options)
            {
                var optionIndex = group.Options.IndexOf(o => o.Name == option);
                if (optionIndex < 0)
                    break;

                if (group.Type is GroupType.Single)
                {
                    if (optionIndex == settings.AsIndex)
                        ++matches;
                }
                else if (settings.HasFlag(optionIndex))
                {
                    ++matches;
                }
            }

            if (matches == Options.Length)
                return true;
        }

        return false;
    }


    public ICondition<ModSettingContext> Reduce()
    {
        var options = Options.Distinct().ToArray();
        return options.Length < Options.Length ? this with { Options = options } : this;
    }

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8,  "MultiSettingAll"u8);
        j.WriteString("Group"u8, Group);
        j.WriteStartArray("Options"u8);
        foreach (var option in Options)
            j.WriteStringValue(option);
        j.WriteEndArray();
        j.WriteEndObject();
    }

    /// <inheritdoc/>
    public ICondition<ModSettingContext> DeepCopy()
        => new MultiSettingAllCondition(Group, Options.ToArray());
}

public sealed record MultiSettingAnyCondition(string Group, string[] Options) : ICondition<ModSettingContext>
{
    public bool Evaluate(in ModSettingContext context)
    {
        if (Options.Length is 0)
            return false;

        foreach (var (index, group) in context.Mod.Groups.Index())
        {
            if (group.Name != Group)
                continue;

            var settings = context.Settings.Settings[index];
            foreach (var option in Options)
            {
                var optionIndex = group.Options.IndexOf(o => o.Name == option);
                if (optionIndex < 0)
                    break;

                if (group.Type is GroupType.Single)
                {
                    if (optionIndex == settings.AsIndex)
                        return true;
                }
                else if (settings.HasFlag(optionIndex))
                {
                    return true;
                }
            }
        }

        return false;
    }


    public ICondition<ModSettingContext> Reduce()
    {
        var options = Options.Distinct().ToArray();
        return options.Length < Options.Length ? this with { Options = options } : this;
    }

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8,  "MultiSettingAny"u8);
        j.WriteString("Group"u8, Group);
        j.WriteStartArray("Options"u8);
        foreach (var option in Options)
            j.WriteStringValue(option);
        j.WriteEndArray();
        j.WriteEndObject();
    }

    /// <inheritdoc/>
    public ICondition<ModSettingContext> DeepCopy()
        => this with { Options = Options.ToArray() };
}

public sealed record SingleSettingCondition(string Group, string Option) : ICondition<ModSettingContext>
{
    public bool Evaluate(in ModSettingContext context)
    {
        foreach (var (index, group) in context.Mod.Groups.Index())
        {
            if (group.Name != Group)
                continue;

            var settings = context.Settings.Settings[index];
            var option   = group.Options.IndexOf(o => o.Name == Option);
            if (option < 0)
                continue;

            switch (group.Type)
            {
                case GroupType.Single when option == settings.AsIndex:
                case GroupType.Multi when settings.HasFlag(option):
                    return true;
            }
        }

        return false;
    }


    public ICondition<ModSettingContext> Reduce()
        => this;

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8,   "SingleSetting"u8);
        j.WriteString("Group"u8,  Group);
        j.WriteString("Option"u8, Option);
        j.WriteEndObject();
    }

    /// <inheritdoc/>
    public ICondition<ModSettingContext> DeepCopy()
        => new SingleSettingCondition(Group, Option);
}
