using Luna;
using Luna.Generators;
using Penumbra.Files;

namespace Penumbra;

public sealed partial class PcpSettings : ConfigurationFile<FilenameService>
{
    [ConfigProperty]
    private bool _createCollection = true;

    [ConfigProperty]
    private bool _assignCollection = true;

    [ConfigProperty]
    private bool _allowIpc = true;

    [ConfigProperty]
    private bool _disableHandling = false;

    [ConfigProperty]
    private string _folderName = "PCP";

    [ConfigProperty]
    private string _pcpExtension = ".pcp";
}
