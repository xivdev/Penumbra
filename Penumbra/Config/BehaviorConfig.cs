using System.Text.Json;
using Luna;
using Luna.Generators;
using Newtonsoft.Json.Linq;
using Penumbra.Files;

namespace Penumbra;

public sealed partial class BehaviorConfig(SaveService saveService, MessageService messager)
    : ConfigurationFile<FilenameService>(saveService, messager)
{
    [ConfigProperty(EventName = "AutoSelectCollectionChanged")]
    private bool _autoSelectCollection = false;

    [ConfigProperty]
    private bool _showModsInLobby = true;

    [ConfigProperty(EventName = "DalamudSubstitutionChanged")]
    private bool _useDalamudUiTextureRedirection = true;

    [ConfigProperty]
    private bool _useNoModsInInspect = false;

    [ConfigProperty]
    private bool _useCharacterCollectionInMainWindow = true;

    [ConfigProperty]
    private bool _useCharacterCollectionsInCards = true;

    [ConfigProperty]
    private bool _useCharacterCollectionInInspect = true;

    [ConfigProperty]
    private bool _useCharacterCollectionInTryOn = true;

    [ConfigProperty]
    private bool _useOwnerNameForCharacterCollection = true;

    [ConfigProperty]
    private bool _useOwnerForHostiles = false;

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        using (var tempObject = j.TemporaryObject("Mod Application"u8))
        {
            tempObject.WriteIfNot("AutoSelectCollection"u8,           AutoSelectCollection,           false);
            tempObject.WriteIfNot("ShowModsInLobby"u8,                ShowModsInLobby,                true);
            tempObject.WriteIfNot("UseDalamudUiTextureRedirection"u8, UseDalamudUiTextureRedirection, true);
            tempObject.WriteIfNot("UseNoModsInInspect"u8,             UseNoModsInInspect,             false);
        }

        using (var tempObject = j.TemporaryObject("Collection Association"u8))
        {
            tempObject.WriteIfNot("UseCharacterCollectionInMainWindow"u8, UseCharacterCollectionInMainWindow, true);
            tempObject.WriteIfNot("UseCharacterCollectionsInCards"u8,     UseCharacterCollectionsInCards,     true);
            tempObject.WriteIfNot("UseCharacterCollectionInInspect"u8,    UseCharacterCollectionInInspect,    true);
            tempObject.WriteIfNot("UseCharacterCollectionInTryOn"u8,      UseCharacterCollectionInTryOn,      true);
            tempObject.WriteIfNot("UseOwnerNameForCharacterCollection"u8, UseOwnerNameForCharacterCollection, true);
            tempObject.WriteIfNot("UseOwnerForHostiles"u8,                UseOwnerForHostiles,                false);
        }
    }

    protected override void LoadData(JObject j)
    {
        throw new NotImplementedException();
    }

    public override string ToFilePath(FilenameService fileNames)
        => throw new NotImplementedException();
}
