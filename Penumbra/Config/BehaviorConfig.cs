using Luna;
using Luna.Generators;
using Penumbra.Files;

namespace Penumbra;

public sealed partial class BehaviorConfig : ConfigurationFile<FilenameService>
{
    [ConfigProperty]
    private bool _autoSelectCollection = false;

    [ConfigProperty]
    private bool _showModsInLobby = true;

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

    [ConfigProperty]
    private bool _useNoModsInInspect = false;

    [ConfigProperty]
    private bool _useDalamudUiTextureRedirection = true;
}
