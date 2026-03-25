using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Services;
using Penumbra.UI.Classes;
using TabType = Penumbra.Api.Enums.TabType;

namespace Penumbra.UI.MainWindow;

public sealed class MainWindow : Window
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Configuration           _config;
    private readonly ValidityChecker         _validityChecker;
    private readonly GlobalModImporter       _globalModImporter;
    private readonly UiNavigator             _navigator;
    private          Penumbra?               _penumbra;
    private          MainTabBar              _configTabs = null!;
    private          string?                 _lastException;

    public MainWindow(IDalamudPluginInterface pi, Configuration config, ValidityChecker checker,
        TutorialService tutorial, GlobalModImporter globalModImporter, UiNavigator navigator)
        : base(checker.GetMainWindowLabel())
    {
        _pluginInterface   = pi;
        _config            = config;
        _validityChecker   = checker;
        _globalModImporter = globalModImporter;
        _navigator         = navigator;

        _navigator.ToggleMainWindow += OnToggleMainWindow;
        RespectCloseHotkey          =  true;
        tutorial.UpdateTutorialStep();
        IsOpen = _config.OpenWindowAtStart;
    }

    public void OpenSettings()
    {
        _configTabs.NextTab = TabType.Settings;
        IsOpen              = true;
    }

    public void Setup(Penumbra penumbra, MainTabBar configTabs)
    {
        _penumbra           = penumbra;
        _configTabs         = configTabs;
        _configTabs.NextTab = _config.Ephemeral.SelectedTab;
    }

    public override bool DrawConditions()
        => _penumbra != null;

    public override void PreDraw()
    {
        if (_config.Ephemeral.FixMainWindow)
            Flags |= WindowFlags.NoResize | WindowFlags.NoMove;
        else
            Flags &= ~(WindowFlags.NoResize | WindowFlags.NoMove);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = _config.MinimumSize,
            MaximumSize = new Vector2(4096, 2160),
        };
    }

    public override void Draw()
    {
        UiHelpers.SetupCommonSizes();
        _globalModImporter.DrawWindowTarget();
        try
        {
            if (!_validityChecker.IsValidSourceRepo)
            {
                DrawProblemWindow(
                    $"You are loading a release version of Penumbra from the repository \"{_pluginInterface.SourceRepository}\" instead of the official repository.\n"
                  + $"Please use the official repository at {ValidityChecker.Repository} or the suite repository at {ValidityChecker.SeaOfStars}.\n\n"
                  + "If you are developing for Penumbra and see this, you should compile your version in debug mode to avoid it.");
            }
            else if (_validityChecker.IsNotInstalledPenumbra)
            {
                DrawProblemWindow(
                    $"You are loading a release version of Penumbra from \"{_pluginInterface.AssemblyLocation.Directory?.FullName ?? "Unknown"}\" instead of the installedPlugins directory.\n\n"
                  + "You should not install Penumbra manually, but rather add the plugin repository under settings and then install it via the plugin installer.\n\n"
                  + "If you do not know how to do this, please take a look at the readme in Penumbras github repository or join us in discord.\n"
                  + "If you are developing for Penumbra and see this, you should compile your version in debug mode to avoid it.");
            }
            else if (_validityChecker.DevPenumbraExists)
            {
                DrawProblemWindow(
                    $"You are loading a installed version of Penumbra from \"{_pluginInterface.AssemblyLocation.Directory?.FullName ?? "Unknown"}\", "
                  + "but also still have some remnants of a custom install of Penumbra in your devPlugins folder.\n\n"
                  + "This can cause some issues, so please go to your \"%%appdata%%\\XIVLauncher\\devPlugins\" folder and delete the Penumbra folder from there.\n\n"
                  + "If you are developing for Penumbra, try to avoid mixing versions. This warning will not appear if compiled in Debug mode.");
            }
            else
            {
                _configTabs.Draw();
            }

            _lastException = null;
        }
        catch (Exception e)
        {
            if (_lastException != null)
            {
                var text = e.ToString();
                if (text == _lastException)
                    return;

                _lastException = text;
            }
            else
            {
                _lastException = e.ToString();
            }

            Penumbra.Log.Error($"Exception thrown during UI Render:\n{_lastException}");
        }
    }

   

    private void DrawProblemWindow(Utf8StringHandler<TextStringHandlerBuffer> text)
    {
        using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
        Im.Line.New();
        Im.Line.New();
        Im.TextWrapped(ref text);
        color.Pop();

        Im.Line.New();
        Im.Line.New();
        SupportButton.Discord(Penumbra.Messager, 0);
        Im.Line.Same();
        UiHelpers.DrawSupportButton(_penumbra!);
        Im.Line.New();
        Im.Line.New();
    }

    private void OnToggleMainWindow(bool open)
        => IsOpen = open;
}
