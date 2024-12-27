namespace Penumbra.Collections;

public struct CollectionCounters(int changeCounter)
{
    /// <summary> Count the number of changes of the effective file list. </summary>
    public int Change { get; private set; } = changeCounter;

    /// <summary> Count the number of IMC-relevant changes of the effective file list. </summary>
    public int Imc    { get; private set; }

    /// <summary> Count the number of ATCH-relevant changes of the effective file list. </summary>
    public int Atch   { get; private set; }

    /// <summary> Increment the number of changes in the effective file list. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IncrementChange()
        => ++Change;

    /// <summary> Increment the number of IMC-relevant changes in the effective file list. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IncrementImc()
        => ++Imc;

    /// <summary> Increment the number of ATCH-relevant changes in the effective file list. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IncrementAtch()
        => ++Imc;
}
