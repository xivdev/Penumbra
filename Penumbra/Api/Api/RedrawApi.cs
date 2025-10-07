using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Interop.Services;

namespace Penumbra.Api.Api;

public class RedrawApi(RedrawService redrawService, IFramework framework) : IPenumbraApiRedraw, IApiService
{
    public void RedrawObject(int gameObjectIndex, RedrawType setting)
    {
        framework.RunOnFrameworkThread(() => redrawService.RedrawObject(gameObjectIndex, setting));
    }

    public void RedrawObject(string name, RedrawType setting)
    {
        framework.RunOnFrameworkThread(() => redrawService.RedrawObject(name, setting));
    }

    public void RedrawObject(IGameObject? gameObject, RedrawType setting)
    {
        framework.RunOnFrameworkThread(() => redrawService.RedrawObject(gameObject, setting));
    }

    public void RedrawAll(RedrawType setting)
    {
        framework.RunOnFrameworkThread(() => redrawService.RedrawAll(setting));
    }

    public event GameObjectRedrawnDelegate? GameObjectRedrawn
    {
        add => redrawService.GameObjectRedrawn += value;
        remove => redrawService.GameObjectRedrawn -= value;
    }
}
