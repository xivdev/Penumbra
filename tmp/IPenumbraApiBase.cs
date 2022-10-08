namespace Penumbra.Api;

public interface IPenumbraApiBase
{
    // The API version is staggered in two parts.
    // The major/Breaking version only increments if there are changes breaking backwards compatibility.
    // The minor/Feature version increments any time there is something added
    // and resets when Breaking is incremented.
    public (int Breaking, int Feature) ApiVersion { get; }
    public bool Valid { get; }
}