using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Penumbra.GameData;

public static class UtilityFunctions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T? FirstOrNull<T>(this IEnumerable<T> values, Func<T, bool> predicate) where T : struct
        => values.Cast<T?>().FirstOrDefault(v => predicate(v!.Value));
}
