using System;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

public static partial class Ipc
{
    public static class RedrawAll
    {
        public const string Label = $"Penumbra.{nameof( RedrawAll )}";

        public static ActionProvider< RedrawType > Provider( DalamudPluginInterface pi, Action< RedrawType > action )
            => new(pi, Label, action);

        public static ActionSubscriber< RedrawType > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class RedrawObject
    {
        public const string Label = $"Penumbra.{nameof( RedrawObject )}";

        public static ActionProvider< GameObject, RedrawType > Provider( DalamudPluginInterface pi, Action< GameObject, RedrawType > action )
            => new(pi, Label, action);

        public static ActionSubscriber< GameObject, RedrawType > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class RedrawObjectByIndex
    {
        public const string Label = $"Penumbra.{nameof( RedrawObjectByIndex )}";

        public static ActionProvider< int, RedrawType > Provider( DalamudPluginInterface pi, Action< int, RedrawType > action )
            => new(pi, Label, action);

        public static ActionSubscriber< int, RedrawType > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class RedrawObjectByName
    {
        public const string Label = $"Penumbra.{nameof( RedrawObjectByName )}";

        public static ActionProvider< string, RedrawType > Provider( DalamudPluginInterface pi, Action< string, RedrawType > action )
            => new(pi, Label, action);

        public static ActionSubscriber< string, RedrawType > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class GameObjectRedrawn
    {
        public const string Label = $"Penumbra.{nameof( GameObjectRedrawn )}";

        public static EventProvider< nint, int > Provider( DalamudPluginInterface pi, Action add, Action del )
            => new(pi, Label, add, del);

        public static EventSubscriber< nint, int > Subscriber( DalamudPluginInterface pi, params Action< nint, int >[] actions )
            => new(pi, Label, actions);
    }
}