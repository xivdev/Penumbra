using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.ResourceTree;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Services;

public class PcpService : IApiService, IDisposable
{
    public const string Extension = ".pcp";

    private readonly Configuration       _config;
    private readonly SaveService         _files;
    private readonly ResourceTreeFactory _treeFactory;
    private readonly ObjectManager       _objectManager;
    private readonly ActorManager        _actors;
    private readonly FrameworkManager    _framework;
    private readonly CollectionResolver  _collectionResolver;
    private readonly CollectionManager   _collections;
    private readonly ModCreator          _modCreator;
    private readonly ModExportManager    _modExport;
    private readonly CommunicatorService _communicator;
    private readonly SHA1                _sha1 = SHA1.Create();
    private readonly ModFileSystem       _fileSystem;
    private readonly ModManager          _mods;

    public PcpService(Configuration config,
        SaveService files,
        ResourceTreeFactory treeFactory,
        ObjectManager objectManager,
        ActorManager actors,
        FrameworkManager framework,
        CollectionManager collections,
        CollectionResolver collectionResolver,
        ModCreator modCreator,
        ModExportManager modExport,
        CommunicatorService communicator,
        ModFileSystem fileSystem,
        ModManager mods)
    {
        _config             = config;
        _files              = files;
        _treeFactory        = treeFactory;
        _objectManager      = objectManager;
        _actors             = actors;
        _framework          = framework;
        _collectionResolver = collectionResolver;
        _collections        = collections;
        _modCreator         = modCreator;
        _modExport          = modExport;
        _communicator       = communicator;
        _fileSystem         = fileSystem;
        _mods               = mods;

        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.PcpService);
    }

    public void CleanPcpMods()
    {
        var mods = _mods.Where(m => m.ModTags.Contains("PCP")).ToList();
        Penumbra.Log.Information($"[PCPService] Deleting {mods.Count} mods containing the tag PCP.");
        foreach (var mod in mods)
            _mods.DeleteMod(mod);
    }

    public void CleanPcpCollections()
    {
        var collections = _collections.Storage.Where(c => c.Identity.Name.StartsWith("PCP/")).ToList();
        Penumbra.Log.Information($"[PCPService] Deleting {collections.Count} mods containing the tag PCP.");
        foreach (var collection in collections)
            _collections.Storage.Delete(collection);
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory, DirectoryInfo? newDirectory)
    {
        if (type is not ModPathChangeType.Added || _config.PcpSettings.DisableHandling || newDirectory is null)
            return;

        try
        {
            var file = Path.Combine(newDirectory.FullName, "character.json");
            if (!File.Exists(file))
            {
                // First version had collection.json, changed.
                var oldFile = Path.Combine(newDirectory.FullName, "collection.json");
                if (File.Exists(oldFile))
                {
                    Penumbra.Log.Information("[PCPService] Renaming old PCP file from collection.json to character.json.");
                    File.Move(oldFile, file, true);
                }
                else
                    return;
            }

            Penumbra.Log.Information($"[PCPService] Found a PCP file for {mod.Name}, applying.");
            var text       = File.ReadAllText(file);
            var jObj       = JObject.Parse(text);
            var collection = ModCollection.Empty;
            // Create collection.
            if (_config.PcpSettings.CreateCollection)
            {
                var identifier = _actors.FromJson(jObj["Actor"] as JObject);
                if (identifier.IsValid && jObj["Collection"]?.ToObject<string>() is { } collectionName)
                {
                    var name = $"PCP/{collectionName}";
                    if (_collections.Storage.AddCollection(name, null))
                    {
                        collection = _collections.Storage[^1];
                        _collections.Editor.SetModState(collection, mod, true);

                        // Assign collection.
                        if (_config.PcpSettings.AssignCollection)
                        {
                            var identifierGroup = _collections.Active.Individuals.GetGroup(identifier);
                            _collections.Active.SetCollection(collection, CollectionType.Individual, identifierGroup);
                        }
                    }
                }
            }

            // Move to folder.
            if (_fileSystem.TryGetValue(mod, out var leaf))
            {
                try
                {
                    var folder = _fileSystem.FindOrCreateAllFolders(_config.PcpSettings.FolderName);
                    _fileSystem.Move(leaf, folder);
                }
                catch
                {
                    // ignored.
                }
            }

            // Invoke IPC.
            if (_config.PcpSettings.AllowIpc)
                _communicator.PcpParsing.Invoke(jObj, mod.Identifier, collection.Identity.Id);
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Error reading the character.json file from {mod.Identifier}:\n{ex}");
        }
    }

    public void Dispose()
        => _communicator.ModPathChanged.Unsubscribe(OnModPathChange);

    public async Task<(bool, string)> CreatePcp(ObjectIndex objectIndex, string note = "", CancellationToken cancel = default)
    {
        try
        {
            Penumbra.Log.Information($"[PCPService] Creating PCP file for game object {objectIndex.Index}.");
            var (identifier, tree, meta) = await _framework.Framework.RunOnFrameworkThread(() =>
            {
                var (actor, identifier) = CheckActor(objectIndex);
                cancel.ThrowIfCancellationRequested();
                unsafe
                {
                    var collection = _collectionResolver.IdentifyCollection((GameObject*)actor.Address, true);
                    if (!collection.Valid || !collection.ModCollection.HasCache)
                        throw new Exception($"Actor {identifier} has no mods applying, nothing to do.");

                    cancel.ThrowIfCancellationRequested();
                    if (_treeFactory.FromCharacter(actor, 0) is not { } tree)
                        throw new Exception($"Unable to fetch modded resources for {identifier}.");

                    var meta = new MetaDictionary(collection.ModCollection.MetaCache, actor.Address);
                    return (identifier.CreatePermanent(), tree, meta);
                }
            });
            cancel.ThrowIfCancellationRequested();
            var time         = DateTime.Now;
            var modDirectory = CreateMod(identifier, note, time);
            await CreateDefaultMod(modDirectory, meta, tree, cancel);
            await CreateCollectionInfo(modDirectory, objectIndex, identifier, note, time, cancel);
            var file = ZipUp(modDirectory);
            return (true, file);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string ZipUp(DirectoryInfo directory)
    {
        var fileName = directory.FullName + Extension;
        ZipFile.CreateFromDirectory(directory.FullName, fileName, CompressionLevel.Optimal, false);
        directory.Delete(true);
        return fileName;
    }

    private async Task CreateCollectionInfo(DirectoryInfo directory, ObjectIndex index, ActorIdentifier actor, string note, DateTime time,
        CancellationToken cancel = default)
    {
        var jObj = new JObject
        {
            ["Version"]    = 1,
            ["Actor"]      = actor.ToJson(),
            ["Mod"]        = directory.Name,
            ["Collection"] = note.Length > 0 ? $"{actor.ToName()}: {note}" : actor.ToName(),
            ["Time"]       = time,
            ["Note"]       = note,
        };
        if (note.Length > 0)
            cancel.ThrowIfCancellationRequested();
        if (_config.PcpSettings.AllowIpc)
            await _framework.Framework.RunOnFrameworkThread(() => _communicator.PcpCreation.Invoke(jObj, index.Index, directory.FullName));
        var             filePath = Path.Combine(directory.FullName, "character.json");
        await using var file     = File.Open(filePath, File.Exists(filePath) ? FileMode.Truncate : FileMode.CreateNew);
        await using var stream   = new StreamWriter(file);
        await using var json     = new JsonTextWriter(stream);
        json.Formatting = Formatting.Indented;
        await jObj.WriteToAsync(json, cancel);
    }

    private DirectoryInfo CreateMod(ActorIdentifier actor, string note, DateTime time)
    {
        var directory = _modExport.ExportDirectory;
        directory.Create();
        var actorName  = actor.ToName();
        var authorName = _actors.GetCurrentPlayer().ToName();
        var suffix = note.Length > 0
            ? note
            : time.ToString("yyyy-MM-ddTHH\\:mm", CultureInfo.InvariantCulture);
        var modName     = $"{actorName} - {suffix}";
        var description = $"On-Screen Data for {actorName} as snapshotted on {time}.";
        return _modCreator.CreateEmptyMod(directory, modName, description, authorName, "PCP")
         ?? throw new Exception($"Unable to create mod {modName} in {directory.FullName}.");
    }

    private async Task CreateDefaultMod(DirectoryInfo modDirectory, MetaDictionary meta, ResourceTree tree,
        CancellationToken cancel = default)
    {
        var subDirectory = modDirectory.CreateSubdirectory("files");
        var subMod = new DefaultSubMod(null!)
        {
            Manipulations = meta,
        };

        foreach (var node in tree.FlatNodes)
        {
            cancel.ThrowIfCancellationRequested();
            var gamePath = node.GamePath;
            var fullPath = node.FullPath;
            if (fullPath.IsRooted)
            {
                var hash = await _sha1.ComputeHashAsync(File.OpenRead(fullPath.FullName), cancel).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();
                var name    = Convert.ToHexString(hash) + fullPath.Extension;
                var newFile = Path.Combine(subDirectory.FullName, name);
                if (!File.Exists(newFile))
                    File.Copy(fullPath.FullName, newFile);
                subMod.Files.TryAdd(gamePath, new FullPath(newFile));
            }
            else if (gamePath.Path != fullPath.InternalName)
            {
                subMod.FileSwaps.TryAdd(gamePath, fullPath);
            }
        }

        cancel.ThrowIfCancellationRequested();

        var saveGroup = new ModSaveGroup(modDirectory, subMod, _config.ReplaceNonAsciiOnImport);
        var filePath  = _files.FileNames.OptionGroupFile(modDirectory.FullName, -1, string.Empty, _config.ReplaceNonAsciiOnImport);
        cancel.ThrowIfCancellationRequested();
        await using var fileStream = File.Open(filePath, File.Exists(filePath) ? FileMode.Truncate : FileMode.CreateNew);
        await using var writer     = new StreamWriter(fileStream);
        saveGroup.Save(writer);
    }

    private (ICharacter Actor, ActorIdentifier Identifier) CheckActor(ObjectIndex objectIndex)
    {
        var actor = _objectManager[objectIndex];
        if (!actor.Valid)
            throw new Exception($"No Actor at index {objectIndex} found.");

        if (!actor.Identifier(_actors, out var identifier))
            throw new Exception($"Could not create valid identifier for actor at index {objectIndex}.");

        if (!actor.IsCharacter)
            throw new Exception($"Actor {identifier} at index {objectIndex} is not a valid character.");

        if (!actor.Model.Valid)
            throw new Exception($"Actor {identifier} at index {objectIndex} has no model.");

        if (_objectManager.Objects.CreateObjectReference(actor.Address) is not ICharacter character)
            throw new Exception($"Actor {identifier} at index {objectIndex} could not be converted to ICharacter");

        return (character, identifier);
    }
}
