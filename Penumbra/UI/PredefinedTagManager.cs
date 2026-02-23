using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using MessageService = Penumbra.Services.MessageService;

namespace Penumbra.UI;

public sealed class PredefinedTagManager : PredefinedTagManager<FilenameService, Mod>
{
    private readonly ModManager _modManager;

    public PredefinedTagManager(ModManager modManager, SaveService saveService, MessageService messager)
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

    protected override bool HandleVersionMigration(string logName, JObject data, int version)
    {
        if (version is 1)
        {
            if (data["Tags"] is not JObject tags)
                return true;

            foreach (var (tag, _) in tags)
            {
                if (!PredefinedTags.AddUnique(tag))
                    Messager.NotificationMessage($"Duplicate tag {tag} found in predefined tags, ignoring.");
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
        => ColorId.PredefinedTagAdd.Value().ToVector();

    public override Vector4 RemoveButtonColor
        => ColorId.PredefinedTagRemove.Value().ToVector();

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
