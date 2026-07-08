using System.Text.Json;
using ImSharp;
using Luna;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Groups;

public readonly record struct ModSettingContext(Mod Mod, ModSettings Settings, IModObject? Object = null) : IConditionContext<ModSettingContext>
{
    public static ICondition<ModSettingContext>? ParseCustomType(ref Utf8JsonReader reader, Utf8JsonObjectLimit obj, StringU8 type)
    {
        if (!type.Equals("Setting"u8))
            return null;

        var guid = Guid.Empty;
        while (obj.Read(ref reader))
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
                return null;

            if (!reader.GuidProperty("Setting"u8, out guid))
                reader.Skip();
        }

        if (guid == Guid.Empty)
            return null;

        return new SettingIdCondition(guid);
    }

    public ICondition<ModSettingContext>? GetRoot()
        => Object?.Condition;

    public void SetRoot(ICondition<ModSettingContext>? condition)
        => Object?.Condition = condition?.Reduce();
}

public sealed class SettingCondition(IModOption option) : ICondition<ModSettingContext>
{
    public IModOption Option { get; set; } = option;

    public bool Equals(ICondition<ModSettingContext>? other)
        => other is SettingCondition s && Option == s.Option;

    public bool Evaluate(in ModSettingContext context)
        => Option.IsEnabled(context.Settings);

    public ICondition<ModSettingContext> Reduce()
        => this;

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8,    "Setting"u8);
        j.WriteString("Setting"u8, Option.Id);
        j.WriteEndObject();
    }

    public ICondition<ModSettingContext> DeepCopy()
        => new SettingCondition(Option);

    public IEnumerable<ICondition<ModSettingContext>> Subconditions
        => [];

    public int RemoveSubconditions(Func<ICondition<ModSettingContext>, bool> predicate)
        => 0;

    public ICondition<ModSettingContext>? EditConditions(Func<ICondition<ModSettingContext>, ICondition<ModSettingContext>?> method)
        => method(this);
}

public sealed class SettingIdCondition(Guid id) : ICondition<ModSettingContext>
{
    public Guid Option { get; } = id;

    public bool Equals(ICondition<ModSettingContext>? other)
        => other is SettingIdCondition s && Option == s.Option;

    public bool Evaluate(in ModSettingContext context)
        => throw new NotImplementedException(
            $"{nameof(SettingIdCondition)} can not be evaluated and is only for parsing and subsequent conversion to {nameof(SettingCondition)}.");

    public ICondition<ModSettingContext> Reduce()
        => this;

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8,    "Setting"u8);
        j.WriteString("Setting"u8, Option);
        j.WriteEndObject();
    }

    public ICondition<ModSettingContext> DeepCopy()
        => new SettingIdCondition(Option);

    public IEnumerable<ICondition<ModSettingContext>> Subconditions
        => [];

    public int RemoveSubconditions(Func<ICondition<ModSettingContext>, bool> predicate)
        => 0;

    public ICondition<ModSettingContext>? EditConditions(Func<ICondition<ModSettingContext>, ICondition<ModSettingContext>?> method)
        => method(this);
}
