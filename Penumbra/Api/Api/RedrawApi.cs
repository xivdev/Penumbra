using Dalamud.Game.ClientState.Objects.Types;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Interop.Services;

namespace Penumbra.Api.Api;

public class RedrawApi(RedrawService redrawService) : IPenumbraApiRedraw, IApiService
{
    public void RedrawObject(int gameObjectIndex, RedrawType setting)
        => redrawService.RedrawObject(gameObjectIndex, setting);

    public void RedrawObject(string name, RedrawType setting)
        => redrawService.RedrawObject(name, setting);

    public void RedrawObject(IGameObject? gameObject, RedrawType setting)
        => redrawService.RedrawObject(gameObject, setting);

    public void RedrawAll(RedrawType setting)
        => redrawService.RedrawAll(setting);

    public event GameObjectRedrawnDelegate? GameObjectRedrawn
    {
        add => redrawService.GameObjectRedrawn += value;
        remove => redrawService.GameObjectRedrawn -= value;
    }
}
