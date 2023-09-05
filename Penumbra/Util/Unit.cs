using System;

namespace Penumbra.Util;

/// <summary>
/// An empty structure. Can be used as value of a concurrent dictionary, to use it as a set.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Instance = new();

    public bool Equals(Unit other)
        => true;

    public override bool Equals(object? obj)
        => obj is Unit;

    public override int GetHashCode()
        => 0;

    public static bool operator ==(Unit left, Unit right)
        => true;

    public static bool operator !=(Unit left, Unit right)
        => false;
}
