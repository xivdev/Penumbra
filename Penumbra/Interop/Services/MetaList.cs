using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Services;

public class MetaList(CharacterUtility.InternalIndex index)
{
    public readonly CharacterUtility.InternalIndex Index           = index;
    public readonly MetaIndex                      GlobalMetaIndex = CharacterUtility.RelevantIndices[index.Value];

    private nint _defaultResourceData = nint.Zero;
    private int  _defaultResourceSize;
    public  bool Ready { get; private set; }

    public void SetDefaultResource(nint data, int size)
    {
        if (Ready)
            return;

        _defaultResourceData = data;
        _defaultResourceSize = size;
        Ready                = _defaultResourceData != nint.Zero && size != 0;
    }

    public (nint Address, int Size) DefaultResource
        => (_defaultResourceData, _defaultResourceSize);
}
