using System;
using Dalamud.Plugin;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

public static partial class Ipc
{
    public static class ResolveDefaultPath
    {
        public const string Label = $"Penumbra.{nameof( ResolveDefaultPath )}";

        public static FuncProvider< string, string > Provider( DalamudPluginInterface pi, Func< string, string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class ResolveInterfacePath
    {
        public const string Label = $"Penumbra.{nameof( ResolveInterfacePath )}";

        public static FuncProvider< string, string > Provider( DalamudPluginInterface pi, Func< string, string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class ResolvePlayerPath
    {
        public const string Label = $"Penumbra.{nameof( ResolvePlayerPath )}";

        public static FuncProvider< string, string > Provider( DalamudPluginInterface pi, Func< string, string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class ResolveCharacterPath
    {
        public const string Label = $"Penumbra.{nameof( ResolveCharacterPath )}";

        public static FuncProvider< string, string, string > Provider( DalamudPluginInterface pi, Func< string, string, string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string, string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class ReverseResolvePath
    {
        public const string Label = $"Penumbra.{nameof( ReverseResolvePath )}";

        public static FuncProvider< string, string, string[] > Provider( DalamudPluginInterface pi, Func< string, string, string[] > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string, string[] > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class ReverseResolvePlayerPath
    {
        public const string Label = $"Penumbra.{nameof( ReverseResolvePlayerPath )}";

        public static FuncProvider< string, string[] > Provider( DalamudPluginInterface pi, Func< string, string[] > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string[] > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }
}