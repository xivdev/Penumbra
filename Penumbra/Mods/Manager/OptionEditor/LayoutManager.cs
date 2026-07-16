using Luna;
using Penumbra.Communication;
using Penumbra.Files;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Manager.OptionEditor;

public sealed class LayoutManager : IRequiredService, IDisposable
{
    public readonly SaveService         SaveService;
    public readonly CommunicatorService Communicator;
    public readonly LunaLogger          Log;

    public LayoutManager(SaveService saveService, CommunicatorService communicator, LunaLogger log)
    {
        SaveService  = saveService;
        Communicator = communicator;
        Log          = log;

        Communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.LayoutManager);
    }

    public void Dispose()
    {
        Communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
    }

    public static string? ValidateCondition(IModObject parent, IModOption option)
    {
        if (option == parent)
            return $"The option {parent.Name} can not depend on itself.";

        if (option.Group == parent)
            return $"The group {parent.Name} can not depend on an option {option.Name} of itself.";

        if (option.Group.Behaviour is GroupDrawBehaviour.SingleSelection && option.Group == parent.Group)
            return $"The option {parent.Name} can not depend on another option {option.Name} in its own single group {parent.Group.Name}.";

        return null;
    }


    private void OnModOptionChange(in ModOptionChanged.Arguments arguments)
    {
        switch (arguments.Type)
        {
            case ModOptionChangeType.OptionDeleted:
            {
                if (arguments.Mod.SubObjects.Remove(arguments.Id, out var obj))
                    foreach (var @object in arguments.Mod.SubObjects.Values)
                        RemoveObjectFromObject(@object, obj);
                return;
            }
            case ModOptionChangeType.PrepareGroupDeletion:
            {
                var group = arguments.Group;
                foreach (var option in group?.Options ?? [])
                {
                    if (arguments.Mod.SubObjects.Remove(option.Id, out var obj))
                        foreach (var @object in arguments.Mod.SubObjects.Values.Where(o => o.Group != group))
                            RemoveObjectFromObject(@object, obj);
                }

                return;
            }
            case ModOptionChangeType.GroupDeleted:
            {
                if (arguments.Mod.SubObjects.Remove(arguments.Id, out var obj))
                    foreach (var @object in arguments.Mod.SubObjects.Values)
                        RemoveObjectFromObject(@object, obj);
                return;
            }
            case ModOptionChangeType.OptionAdded when arguments.Option is { } option:
            {
                if (!arguments.Mod.SubObjects.TryAdd(option.Id, option))
                    Log.Error($"Could not add option {option.Name} to {arguments.Mod.Name}'s sub objects: GUID {option.Id} already present.");
                return;
            }
            case ModOptionChangeType.GroupAdded when arguments.Group is { } group:
            {
                foreach (var @object in group.Options.Prepend<IModObject>(group))
                {
                    if (!arguments.Mod.SubObjects.TryAdd(@object.Id, @object))
                        Log.Error(
                            $"Could not add option {@object.Name} to {arguments.Mod.Name}'s sub objects: GUID {@object.Id} already present.");
                }

                return;
            }
        }
    }

    private static void RemoveObjectFromObject(IModObject @object, IModObject toRemove)
    {
        if (@object is IModGroup group && ReferenceEquals(group.ParentSetting, toRemove))
            group.ParentSetting = null;
        @object.Condition?.RemoveSubconditions(c => c is SettingCondition s && s.Option == toRemove);
        @object.Condition = @object.Condition?.Reduce();
        if (@object.Condition is TrueCondition<ModSettingContext>)
            @object.Condition = null;
    }
}
