using System.Numerics;
using ImGuiNET;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class ManageModsButton
        {
            // magic numbers
            private const int    Padding         = 50;
            private const int    Width           = 200;
            private const int    Height          = 45;
            private const string MenuButtonsName = "Penumbra Menu Buttons";
            private const string MenuButtonLabel = "Manage Mods";

            private static readonly Vector2 WindowSize      = new( Width, Height );
            private static readonly Vector2 WindowPosOffset = new( Padding + Width, Padding + Height );

            private const ImGuiWindowFlags ButtonFlags =
                  ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoSavedSettings;

            private readonly SettingsInterface                  _base;
            private readonly Dalamud.Game.ClientState.Condition _condition;

            public ManageModsButton( SettingsInterface ui )
            {
                _base      = ui;
                _condition = ui._plugin.PluginInterface.ClientState.Condition;
            }

            public void Draw()
            {
                if( _condition.Any() || _base._menu.Visible )
                {
                    return;
                }

                var ss = ImGui.GetMainViewport().Size + ImGui.GetMainViewport().Pos;
                ImGui.SetNextWindowViewport(ImGui.GetMainViewport().ID);
                
                ImGui.SetNextWindowPos( ss - WindowPosOffset, ImGuiCond.Always );

                if( !ImGui.Begin( MenuButtonsName, ButtonFlags ) )
                {
                    return;
                }

                if( ImGui.Button( MenuButtonLabel, WindowSize ) )
                {
                    _base.FlipVisibility();
                }

                ImGui.End();
            }
        }
    }
}
