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


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T[] AddItem<T>(this T[] array, T element, int count = 1)
    {
        var length   = array.Length;
        var newArray = new T[array.Length + count];
        Array.Copy(array, newArray, length);
        for (var i = length; i < newArray.Length; ++i)
            newArray[i] = element;

        return newArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T[] RemoveItems<T>(this T[] array, int offset, int count = 1)
    {
        var newArray = new T[array.Length - count];
        Array.Copy(array, newArray,       offset);
        Array.Copy(array, offset + count, newArray, offset, newArray.Length - offset);
        return newArray;
    }
}
