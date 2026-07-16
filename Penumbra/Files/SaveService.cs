using Luna;
using Penumbra.Mods;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using TerraFX.Interop.Windows;

namespace Penumbra.Files;

/// <summary>
/// Any file type that we want to save via SaveService.
/// </summary>
public interface ISavable : ISavable<FilenameService>
{ }

public sealed class SaveService : BaseSaveService<FilenameService>, IService
{
    public SaveService(LunaLogger log, FrameworkManager framework, FilenameService fileNames, BackupService backupService)
        : base(log, framework, fileNames, backupService.Awaiter)
    {
        BackupMode = BackupMode.SingleBackup;
    }

    [OverloadResolutionPriority(100)]
    public void Save(SaveType type, Mod mod)
        => Save(type, new ModMeta(this, mod));

    [OverloadResolutionPriority(100)]
    public void ImmediateSaveSync(Mod mod)
        => ImmediateSaveSync(new ModMeta(this, mod));

    [OverloadResolutionPriority(100)]
    public void QueueSave(Mod mod)
        => QueueSave(new ModMeta(this, mod));

    [OverloadResolutionPriority(100)]
    public void ImmediateSave(Mod mod)
        => ImmediateSave(new ModMeta(this, mod));

    [OverloadResolutionPriority(10)]
    public void Save(SaveType type, IModObject obj)
        => Save(type, new ModMeta(this, obj.Mod));

    [OverloadResolutionPriority(10)]
    public void ImmediateSaveSync(IModObject obj)
        => ImmediateSaveSync(new ModMeta(this, obj.Mod));

    [OverloadResolutionPriority(10)]
    public void QueueSave(IModObject obj)
        => QueueSave(new ModMeta(this, obj.Mod));

    [OverloadResolutionPriority(10)]
    public void ImmediateSave(IModObject obj)
        => ImmediateSave(new ModMeta(this, obj.Mod));

    public void Save(SaveType type, IModDataContainer container)
        => Save(type, new ModMeta(this, container.Mod as Mod ?? ContainerThrowHelper()));

    public void ImmediateSaveSync(IModDataContainer container)
        => ImmediateSaveSync(new ModMeta(this, container.Mod as Mod ?? ContainerThrowHelper()));

    public void QueueSave(IModDataContainer container)
        => QueueSave(new ModMeta(this, container.Mod as Mod ?? ContainerThrowHelper()));

    public void ImmediateSave(IModDataContainer container)
        => ImmediateSave(new ModMeta(this, container.Mod as Mod ?? ContainerThrowHelper()));

    [DoesNotReturn]
    private static Mod ContainerThrowHelper()
        => throw new ArgumentException("Can not save containers that do not actually belong to a mod.");
}
