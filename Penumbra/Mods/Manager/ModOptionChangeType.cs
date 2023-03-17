namespace Penumbra.Mods;

public enum ModOptionChangeType
{
    GroupRenamed,
    GroupAdded,
    GroupDeleted,
    GroupMoved,
    GroupTypeChanged,
    PriorityChanged,
    OptionAdded,
    OptionDeleted,
    OptionMoved,
    OptionFilesChanged,
    OptionFilesAdded,
    OptionSwapsChanged,
    OptionMetaChanged,
    DisplayChange,
    PrepareChange,
    DefaultOptionChanged,
}

public static class ModOptionChangeTypeExtension
{
    /// <summary>
    /// Give information for each type of change.
    /// If requiresSaving, collections need to be re-saved after this change.
    /// If requiresReloading, caches need to be manipulated after this change.
    /// If wasPrepared, caches have already removed the mod beforehand, then need add it again when this event is fired.
    /// Otherwise, caches need to reload the mod itself.
    /// </summary>
    public static void HandlingInfo( this ModOptionChangeType type, out bool requiresSaving, out bool requiresReloading, out bool wasPrepared )
    {
        ( requiresSaving, requiresReloading, wasPrepared ) = type switch
        {
            ModOptionChangeType.GroupRenamed         => ( true, false, false ),
            ModOptionChangeType.GroupAdded           => ( true, false, false ),
            ModOptionChangeType.GroupDeleted         => ( true, true, false ),
            ModOptionChangeType.GroupMoved           => ( true, false, false ),
            ModOptionChangeType.GroupTypeChanged     => ( true, true, true ),
            ModOptionChangeType.PriorityChanged      => ( true, true, true ),
            ModOptionChangeType.OptionAdded          => ( true, true, true ),
            ModOptionChangeType.OptionDeleted        => ( true, true, false ),
            ModOptionChangeType.OptionMoved          => ( true, false, false ),
            ModOptionChangeType.OptionFilesChanged   => ( false, true, false ),
            ModOptionChangeType.OptionFilesAdded     => ( false, true, true ),
            ModOptionChangeType.OptionSwapsChanged   => ( false, true, false ),
            ModOptionChangeType.OptionMetaChanged    => ( false, true, false ),
            ModOptionChangeType.DisplayChange        => ( false, false, false ),
            ModOptionChangeType.DefaultOptionChanged => ( true, false, false ),
            _                                        => ( false, false, false ),
        };
    }
}