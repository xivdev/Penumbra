using ImSharp;
using Luna;

namespace Penumbra.UI.Classes;

public class IncognitoService(TutorialService tutorial, Configuration config) : IUiService
{
    public bool IncognitoMode
        => config.Ephemeral.IncognitoMode;

    public void DrawToggle(float width)
    {
        var hold  = config.IncognitoModifier.IsActive();
        var color = ColorId.FolderExpanded.Value();
        using (ImStyleBorder.Frame.Push(color))
        {
            var       tt    = IncognitoMode ? "Toggle incognito mode off."u8 : "Toggle incognito mode on."u8;
            var       icon  = IncognitoMode ? LunaStyle.IncognitoOn : LunaStyle.IncognitoOff;
            if (ImEx.Icon.Button(icon, tt, size: new Vector2(width, Im.Style.FrameHeight), textColor: color) && hold)
            {
                config.Ephemeral.IncognitoMode = !IncognitoMode;
                config.Ephemeral.Save();
            }
        }

        if (!hold)
            Im.Tooltip.OnHover($"\nHold {config.IncognitoModifier} while clicking to toggle.", HoveredFlags.AllowWhenDisabled, true);

        tutorial.OpenTutorial(BasicTutorialSteps.Incognito);
    }
}
