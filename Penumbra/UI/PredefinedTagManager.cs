using System.Text.Json;
using Luna;
using Penumbra.Files;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed class PredefinedTagManager : PredefinedTagManager<FilenameService, Mod>
{
    private readonly ModManager _modManager;

    public PredefinedTagManager(ModManager modManager, SaveService saveService, PenumbraMessager messager)
        : base(saveService, messager)
    {
        _modManager = modManager;
        Load();
    }

    public override bool HasGlobalTags
        => true;

    public override string GlobalTagName
        => "mod tag";

    public override string ObjectName
        => "mod";

    protected override bool HandleVersionMigration(string logName, in JsonElement data, int version)
    {
        if (version is 1)
        {
            if (!data.TryReadObject("Tags"u8, out var tags))
                return true;

            foreach (var property in tags.EnumerateObject())
            {
                if (!PredefinedTags.AddUnique(property.Name))
                    Messager.NotificationMessage($"Duplicate tag {property.Name} found in predefined tags, ignoring.");
            }

            Messager.Log.Debug($"Migrated {logName} from Version 1 to 2.");
            Save();
            return true;
        }

        // Throws.
        base.HandleVersionMigration(logName, data, version);
        return false;
    }

    public override Vector4 AddButtonColor
        => ColorId.PredefinedTagAdd.Vector;

    public override Vector4 RemoveButtonColor
        => ColorId.PredefinedTagRemove.Vector;

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.PredefinedTagFile;

    protected override IReadOnlyCollection<string> GetLocalTags(Mod obj)
        => obj.LocalTags;

    protected override IReadOnlyCollection<string> GetGlobalTags(Mod obj)
        => obj.ModTags;

    protected override void ChangeLocalTag(Mod obj, int tagIndex, string tag)
        => _modManager.DataEditor.ChangeLocalTag(obj, tagIndex, tag);

    protected override void ChangeGlobalTag(Mod obj, int tagIndex, string tag)
        => _modManager.DataEditor.ChangeModTag(obj, tagIndex, tag);
}
