using Penumbra.CrashHandler.Buffers;

namespace Penumbra.CrashHandler;

public sealed class GameEventLogWriter : IDisposable
{
    public readonly ICharacterBaseBufferWriter       CharacterBase        = CharacterBaseBuffer.CreateWriter();
    public readonly IModdedFileBufferWriter          FileLoaded           = ModdedFileBuffer.CreateWriter();
    public readonly IAnimationInvocationBufferWriter AnimationFuncInvoked = AnimationInvocationBuffer.CreateWriter();

    public void Dispose()
    {
        (CharacterBase as IDisposable)?.Dispose();
        (FileLoaded as IDisposable)?.Dispose();
        (AnimationFuncInvoked as IDisposable)?.Dispose();
    }
}
