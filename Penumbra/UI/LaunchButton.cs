using Dalamud.Interface;
using ImGuiNET;
using Penumbra.UI.Custom;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class ManageModsButton
    {
        // magic numbers
        private const int    Width           = 200;
        private const int    Height          = 45;
        private const string MenuButtonsName = "Penumbra Menu Buttons";
        private const string MenuButtonLabel = "Manage Mods";

        private const ImGuiWindowFlags ButtonFlags =
            ImGuiWindowFlags.AlwaysAutoResize
          | ImGuiWindowFlags.NoBackground
          | ImGuiWindowFlags.NoDecoration
          | ImGuiWindowFlags.NoMove
          | ImGuiWindowFlags.NoScrollbar
          | ImGuiWindowFlags.NoResize
          | ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoSavedSettings;

        private readonly SettingsInterface _base;

        public ManageModsButton( SettingsInterface ui )
            => _base = ui;

        internal bool ForceDraw = false;

        public void Draw()
        {
            if( !ForceDraw && ( Dalamud.Conditions.Any() || _base._menu.Visible ) )
            {
                return;
            }

            using var color = ImGuiRaii.PushColor( ImGuiCol.Button, 0xFF0000C8, ForceDraw );

            var ss = ImGui.GetMainViewport().Size + ImGui.GetMainViewport().Pos;
            ImGui.SetNextWindowViewport( ImGui.GetMainViewport().ID );

            var windowSize = ImGuiHelpers.ScaledVector2( Width, Height );

            ImGui.SetNextWindowPos( ss - windowSize - Penumbra.Config.ManageModsButtonOffset * ImGuiHelpers.GlobalScale, ImGuiCond.Always );

            if( ImGui.Begin( MenuButtonsName, ButtonFlags )
            && ImGui.Button( MenuButtonLabel, windowSize ) )
            {
                _base.FlipVisibility();
            }

            ImGui.End();
        }
    }
}