using System.Text.Json;
using ImSharp;
using Luna;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Groups;

public readonly record struct ModSettingContext(Mod Mod, ModSettings Settings) : IConditionContext<ModSettingContext>
{
    public static ICondition<ModSettingContext>? ParseCustomType(ref Utf8JsonReader reader, Utf8JsonObjectLimit obj, StringU8 type)
    {
        if (type.Equals("AllSettings"u8))
        {
            var options = GetMultiIds(obj, ref reader);
            return new IdListCondition(false, options ?? []);
        }

        if (type.Equals("AnySetting"u8))
        {
            var options = GetMultiIds(obj, ref reader);
            return new IdListCondition(true, options ?? []);
        }

        return null;
    }

    private static List<Guid>? GetMultiIds(Utf8JsonObjectLimit obj, ref Utf8JsonReader reader)
    {
        List<Guid>? options = null;
        while (obj.Read(ref reader))
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
                continue;

            if (reader.ArrayProperty("Options"u8, out _))
                options = reader.ReadGuidArray() ?? [];
            else
                reader.Skip();
        }

        return options;
    }
}

public sealed class AllSettingsCondition(params IReadOnlyCollection<IModOption> options) : MultiSettingCondition(options)
{
    protected override ReadOnlySpan<byte> Type
        => "AllSettings"u8;

    public override ICondition<ModSettingContext> DeepCopy()
        => new AllSettingsCondition(this);

    public override bool Evaluate(in ModSettingContext context)
    {
        foreach (var option in this)
        {
            var group   = context.Mod.Groups[option.GroupIndex];
            var setting = context.Settings.IsEmpty ? group.DefaultSettings : context.Settings.Settings[option.GroupIndex];
            switch (group.Behaviour)
            {
                case GroupDrawBehaviour.MultiSelection when !setting.HasFlag(option.Index):
                case GroupDrawBehaviour.SingleSelection when setting.AsIndex != option.Index:
                    return false;
            }
        }

        return true;
    }

    public override bool Equals(ICondition<ModSettingContext>? other)
        => other is AllSettingsCondition s && s.SequenceEqual(this);

    public override int GetHashCode()
        => this.Aggregate(HashCode.Combine(typeof(AllSettingsCondition).GetHashCode()), HashCode.Combine);
}

public sealed class AnySettingCondition(params IReadOnlyCollection<IModOption> options) : MultiSettingCondition(options)
{
    protected override ReadOnlySpan<byte> Type
        => "AnySetting"u8;

    public override bool Evaluate(in ModSettingContext context)
    {
        foreach (var option in this)
        {
            var group   = context.Mod.Groups[option.GroupIndex];
            var setting = context.Settings.IsEmpty ? group.DefaultSettings : context.Settings.Settings[option.GroupIndex];
            switch (group.Behaviour)
            {
                case GroupDrawBehaviour.MultiSelection when setting.HasFlag(option.Index):
                case GroupDrawBehaviour.SingleSelection when setting.AsIndex == option.Index:
                    return true;
            }
        }

        return false;
    }

    public override ICondition<ModSettingContext> DeepCopy()
        => new AnySettingCondition(this);

    public override bool Equals(ICondition<ModSettingContext>? other)
        => other is AnySettingCondition s && s.SequenceEqual(this);

    public override int GetHashCode()
        => this.Aggregate(HashCode.Combine(typeof(AnySettingCondition).GetHashCode()), HashCode.Combine);
}

public abstract class MultiSettingCondition : List<IModOption>, ICondition<ModSettingContext>
{
    protected MultiSettingCondition(IReadOnlyCollection<IModOption> options)
    {
        EnsureCapacity(options.Count);
        AddRange(options);
    }

    public abstract    ICondition<ModSettingContext> DeepCopy();
    protected abstract ReadOnlySpan<byte>            Type { get; }

    public abstract bool Evaluate(in ModSettingContext context);

    public virtual ICondition<ModSettingContext> Reduce()
    {
        this.RemoveDuplicates<IModOption, IModObject>();
        return this;
    }

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8, Type);
        j.WriteStartArray("Options"u8);
        foreach (var option in this)
            j.WriteStringValue(option.Id);
        j.WriteEndArray();
        j.WriteEndObject();
    }

    public IEnumerable<ICondition<ModSettingContext>> Subconditions
        => [];

    public int RemoveSubconditions(Func<ICondition<ModSettingContext>, bool> predicate)
        => 0;

    public ICondition<ModSettingContext>? EditConditions(Func<ICondition<ModSettingContext>, ICondition<ModSettingContext>?> method)
        => method(this);

    public abstract bool Equals(ICondition<ModSettingContext>? other);
}

/// <summary> Used for delayed loading after parsing all objects. </summary>
public sealed class IdListCondition : List<Guid>, ICondition<ModSettingContext>
{
    /// <summary> Whether this is a list for All or Any. </summary>
    public readonly bool Any;

    public IdListCondition(bool any, IReadOnlyCollection<Guid> options)
    {
        Any = any;
        EnsureCapacity(options.Count);
        AddRange(options);
    }

    public bool Equals(ICondition<ModSettingContext>? other)
        => other is IdListCondition id && id.SequenceEqual(this);

    public bool Evaluate(in ModSettingContext context)
        => throw new NotImplementedException();

    public ICondition<ModSettingContext> Reduce()
    {
        this.RemoveDuplicates();
        return this;
    }

    public void WriteJson(Utf8JsonWriter j)
        => throw new NotImplementedException();

    public ICondition<ModSettingContext> DeepCopy()
        => throw new NotImplementedException();

    public IEnumerable<ICondition<ModSettingContext>> Subconditions
        => [];

    public int RemoveSubconditions(Func<ICondition<ModSettingContext>, bool> predicate)
        => 0;

    public ICondition<ModSettingContext>? EditConditions(Func<ICondition<ModSettingContext>, ICondition<ModSettingContext>?> method)
        => method(this);
}
