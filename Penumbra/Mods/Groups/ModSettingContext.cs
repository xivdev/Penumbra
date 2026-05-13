using System.Text.Json;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.Groups;

public readonly record struct ModSettingContext(Mod Mod, ModSettings Settings);

public sealed class MultiSettingAllCondition(string group, params IReadOnlyCollection<string> options) : MultiSettingCondition(group, options)
{
    protected override ReadOnlySpan<byte> Type
        => "MultiSettingAll"u8;

    public override ICondition<ModSettingContext> DeepCopy()
        => new MultiSettingAllCondition(Group, this);

    protected override bool EvaluateGroup(IModGroup group, Setting settings)
    {
        var matches = 0;
        foreach (var option in this)
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

        return matches == Count;
    }
}

public sealed class MultiSettingAnyCondition(string group, params IReadOnlyCollection<string> options) : MultiSettingCondition(group, options)
{
    protected override ReadOnlySpan<byte> Type
        => "MultiSettingAny"u8;

    public override ICondition<ModSettingContext> DeepCopy()
        => new MultiSettingAnyCondition(Group, this);

    protected override bool EvaluateGroup(IModGroup group, Setting settings)
    {
        foreach (var option in this)
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

        return false;
    }
}

public sealed class SingleSettingCondition(string group, string option) : ICondition<ModSettingContext>
{
    public string Group  = group;
    public string Option = option;

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

    public IEnumerable<ICondition<ModSettingContext>> Subconditions
        => [];

    public int RemoveSubconditions(Func<ICondition<ModSettingContext>, bool> predicate)
        => 0;
}

public abstract class MultiSettingCondition(string group) : List<string>, ICondition<ModSettingContext>
{
    protected MultiSettingCondition(string group, IReadOnlyCollection<string> options)
        : this(group)
    {
        EnsureCapacity(options.Count);
        AddRange(options);
    }

    public string Group = group;

    public abstract    ICondition<ModSettingContext> DeepCopy();
    protected abstract ReadOnlySpan<byte>            Type { get; }
    protected abstract bool                          EvaluateGroup(IModGroup group, Setting settings);

    public bool Evaluate(in ModSettingContext context)
    {
        if (Count is 0)
            return true;

        foreach (var (index, group) in context.Mod.Groups.Index())
        {
            if (group.Name != Group)
                continue;

            if (group.Type is GroupType.Single && Count > 1)
                continue;

            var settings = context.Settings.Settings[index];
            if (EvaluateGroup(group, settings))
                return true;
        }

        return false;
    }

    public ICondition<ModSettingContext> Reduce()
    {
        this.RemoveDuplicates();
        return this;
    }

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8,  Type);
        j.WriteString("Group"u8, Group);
        j.WriteStartArray("Options"u8);
        foreach (var option in this)
            j.WriteStringValue(option);
        j.WriteEndArray();
        j.WriteEndObject();
    }

    public IEnumerable<ICondition<ModSettingContext>> Subconditions
        => [];

    public int RemoveSubconditions(Func<ICondition<ModSettingContext>, bool> predicate)
        => 0;
}
