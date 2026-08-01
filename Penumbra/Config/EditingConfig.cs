using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
using Penumbra.Files;

namespace Penumbra;

public sealed partial class EditingConfig : ConfigurationFile<FilenameService>
{
    [ConfigProperty]
    private long _lowerSizeLimit = 1 << 20;

    [ConfigProperty]
    private int _smallDimensionLimit = 32;

    [ConfigProperty]
    private int _largeDimensionLimit = 4096;

    [ConfigProperty]
    private int _textureDimensionLimit = 4096;

    [ConfigProperty]
    private bool _createBackups = true;

    [ConfigProperty]
    private bool _defaultEditWindowModPinned = true;

    [ConfigProperty]
    private bool _editRawTileTransforms = false;

    [ConfigProperty]
    private bool _wholePairSelectorAlwaysHighlights = false;

    [ConfigProperty]
    private bool _allDyeChannels = false;

    [ConfigProperty]
    private Dictionary<ResourceType, string> _preferredEditorFactories = [];
}
