using System;
using Dalamud.Plugin;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

public static partial class Ipc
{
    public static class GetModDirectory
    {
        public const string Label = $"Penumbra.{nameof( GetModDirectory )}";

        public static FuncProvider< string > Provider( DalamudPluginInterface pi, Func< string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class GetConfiguration
    {
        public const string Label = $"Penumbra.{nameof( GetConfiguration )}";

        public static FuncProvider< string > Provider( DalamudPluginInterface pi, Func< string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class ModDirectoryChanged
    {
        public const string Label = $"Penumbra.{nameof( ModDirectoryChanged )}";

        public static EventProvider< string, bool > Provider( DalamudPluginInterface pi,
            Action< Action< string, bool > > sub, Action< Action< string, bool > > unsub )
            => new(pi, Label, ( sub, unsub ));

        public static EventSubscriber< string, bool > Subscriber( DalamudPluginInterface pi, params Action< string, bool >[] actions )
            => new(pi, Label, actions);
    }
}