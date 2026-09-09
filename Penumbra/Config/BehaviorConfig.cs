using System.Text.Json;
using Luna;
using Luna.Generators;
using Penumbra.Files;
using Penumbra.Services;

namespace Penumbra;

public sealed partial class BehaviorConfig : ConfigurationFile<FilenameService>
{
    #region Mod Application

    [ConfigProperty(EventName = "AutoSelectCollectionChanged")]
    private bool _autoSelectCollection = false;

    [ConfigProperty]
    private bool _showModsInLobby = true;

    [ConfigProperty(EventName = "DalamudSubstitutionChanged")]
    private bool _useDalamudUiTextureRedirection = true;

    [ConfigProperty]
    private bool _useNoModsInInspect = false;

    #endregion

    #region Collection Association

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

    /// <inheritdoc/>
    public BehaviorConfig(SaveService saveService, PenumbraMessager messager)
        : base(saveService, messager)
        => Load();

    #endregion

    public override int CurrentVersion
        => 100;

    protected override void AddData(Utf8JsonWriter j)
    {
        using (var tempObject = j.TemporaryObject("ModApplication"u8))
        {
            tempObject.WriteIfNot("AutoSelectCollection"u8,           AutoSelectCollection,           false);
            tempObject.WriteIfNot("ShowModsInLobby"u8,                ShowModsInLobby,                true);
            tempObject.WriteIfNot("UseDalamudUiTextureRedirection"u8, UseDalamudUiTextureRedirection, true);
            tempObject.WriteIfNot("UseNoModsInInspect"u8,             UseNoModsInInspect,             false);
        }

        using (var tempObject = j.TemporaryObject("CollectionAssociation"u8))
        {
            tempObject.WriteIfNot("UseCharacterCollectionInMainWindow"u8, UseCharacterCollectionInMainWindow, true);
            tempObject.WriteIfNot("UseCharacterCollectionsInCards"u8,     UseCharacterCollectionsInCards,     true);
            tempObject.WriteIfNot("UseCharacterCollectionInInspect"u8,    UseCharacterCollectionInInspect,    true);
            tempObject.WriteIfNot("UseCharacterCollectionInTryOn"u8,      UseCharacterCollectionInTryOn,      true);
            tempObject.WriteIfNot("UseOwnerNameForCharacterCollection"u8, UseOwnerNameForCharacterCollection, true);
            tempObject.WriteIfNot("UseOwnerForHostiles"u8,                UseOwnerForHostiles,                false);
        }
    }

    protected override void LoadData(in JsonElement j)
    {
        if (j.TryReadObject("ModApplication"u8, out var mod))
        {
            AutoSelectCollection           = mod.PropertyOrDefault("AutoSelectCollection"u8,           AutoSelectCollection);
            ShowModsInLobby                = mod.PropertyOrDefault("ShowModsInLobby"u8,                ShowModsInLobby);
            UseDalamudUiTextureRedirection = mod.PropertyOrDefault("UseDalamudUiTextureRedirection"u8, UseDalamudUiTextureRedirection);
            UseNoModsInInspect             = mod.PropertyOrDefault("UseNoModsInInspect"u8,             UseNoModsInInspect);
        }

        if (j.TryReadObject("CollectionAssociation"u8, out var coll))
        {
            UseCharacterCollectionInMainWindow =
                coll.PropertyOrDefault("UseCharacterCollectionInMainWindow"u8, UseCharacterCollectionInMainWindow);
            UseCharacterCollectionsInCards  = coll.PropertyOrDefault("UseCharacterCollectionsInCards"u8,  UseCharacterCollectionsInCards);
            UseCharacterCollectionInInspect = coll.PropertyOrDefault("UseCharacterCollectionInInspect"u8, UseCharacterCollectionInInspect);
            UseCharacterCollectionInTryOn   = coll.PropertyOrDefault("UseCharacterCollectionInTryOn"u8,   UseCharacterCollectionInTryOn);
            UseOwnerNameForCharacterCollection =
                coll.PropertyOrDefault("UseOwnerNameForCharacterCollection"u8, UseOwnerNameForCharacterCollection);
            UseOwnerForHostiles = coll.PropertyOrDefault("UseOwnerForHostiles"u8, UseOwnerForHostiles);
        }
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.Config.Behavior;
}
