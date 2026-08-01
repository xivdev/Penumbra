using Dalamud.Configuration;
using Dalamud.Interface.ImGuiNotification;
using Luna;
using Newtonsoft.Json;
using Penumbra.Files;
using Penumbra.Interop.Services;
using Penumbra.Services;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace Penumbra;

public class Configuration : IPluginConfiguration, IService
{
    public const int CurrentVersion = 16;
    public       int Version { get; set; } = CurrentVersion;

    private readonly SaveService _saveService;

    public readonly EphemeralConfig Ephemeral;

    public readonly FilterConfig Filters;


    public readonly UiConfig Ui;

    public readonly MainConfig         Main;
    public readonly EditingConfig      Editing;
    public readonly AdvancedConfig     Advanced;
    public readonly BehaviorConfig     Behavior;
    public readonly IoConfig           Io;
    public readonly PcpSettings        PcpSettings;
    public readonly PersistentUiConfig PersistentUi;

    /// <summary>
    /// Load the current configuration.
    /// Includes adding new colors and migrating from old versions.
    /// </summary>
    public Configuration(CharacterUtility utility, ConfigMigrationService migrator, SaveService saveService, EphemeralConfig ephemeral,
        UiConfig ui, FilterConfig filters, TextureOptimizationConfig textureOptimization)
    {
        _saveService        = saveService;
        Ephemeral           = ephemeral;
        Ui                  = ui;
        Filters             = filters;
        TextureOptimization = textureOptimization;
        Load(utility, migrator);
    }

    public void Load(CharacterUtility utility, ConfigMigrationService migrator)
    {
        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs)
        {
            Penumbra.Log.Error(
                $"Error parsing Configuration at {errorArgs.ErrorContext.Path}, using default or migrating:\n{errorArgs.ErrorContext.Error}");
            errorArgs.ErrorContext.Handled = true;
        }

        if (File.Exists(_saveService.FileNames.ConfigurationFile))
            try
            {
                var text = File.ReadAllText(_saveService.FileNames.ConfigurationFile);
                JsonConvert.PopulateObject(text, this, new JsonSerializerSettings
                {
                    Error = HandleDeserializationError,
                });
            }
            catch (Exception ex)
            {
                Penumbra.Messager.NotificationMessage(ex,
                    "Error reading Configuration, reverting to default.\nYou may be able to restore your configuration using the rolling backups in the XIVLauncher/backups/Penumbra directory.",
                    "Error reading Configuration", NotificationType.Error);
            }

        migrator.Migrate(utility, this);
    }
}
