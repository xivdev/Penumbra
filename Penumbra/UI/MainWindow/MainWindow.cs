using Dalamud.Interface.Windowing;
using Luna;
using Penumbra.Communication;
using Penumbra.Services;
using Penumbra.UI.Classes;
using TabType = Penumbra.Api.Enums.TabType;
using Window = Luna.Window;

namespace Penumbra.UI.MainWindow;

public sealed class MainWindow : Window
{
    private readonly Configuration     _config;
    private readonly GlobalModImporter _globalModImporter;
    private readonly UiNavigator       _navigator;
    private readonly MainTabBar        _configTabs;
    private          string?           _lastException;

    public MainWindow(IPluginLoader loader, Configuration config, TutorialService tutorial, GlobalModImporter globalModImporter,
        UiNavigator navigator, MainTabBar configTabs)
        : base(loader.GetWindowLabel("PenumbraConfigWindow"))
    {
        _config            = config;
        _globalModImporter = globalModImporter;
        _navigator         = navigator;
        _configTabs        = configTabs;

        _navigator.ToggleMainWindow += OnToggleMainWindow;
        RespectCloseHotkey          =  true;
        tutorial.UpdateTutorialStep();
        IsOpen = _config.Ui.OpenWindowAtStart;
    }

    public void OpenSettings()
    {
        _configTabs.NextTab = TabType.Settings;
        IsOpen              = true;
    }

    public override void PreDraw()
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = _config.Advanced.MinimumSize,
            MaximumSize = new Vector2(4096, 2160),
        };
    }

    public override void Draw()
    {
        UiHelpers.SetupCommonSizes();
        _globalModImporter.DrawWindowTarget();
        try
        {
            _configTabs.Draw();
            _lastException = null;
        }
        catch (Exception e)
        {
            if (_lastException is not null)
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

    private void OnToggleMainWindow(bool open)
        => IsOpen = open;
}
