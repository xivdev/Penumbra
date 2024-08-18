using Penumbra.CrashHandler.Buffers;

namespace Penumbra.CrashHandler;

public sealed class GameEventLogWriter(int pid) : IDisposable
{
    public readonly ICharacterBaseBufferWriter       CharacterBase        = CharacterBaseBuffer.CreateWriter(pid);
    public readonly IModdedFileBufferWriter          FileLoaded           = ModdedFileBuffer.CreateWriter(pid);
    public readonly IAnimationInvocationBufferWriter AnimationFuncInvoked = AnimationInvocationBuffer.CreateWriter(pid);

    public void Dispose()
    {
        (CharacterBase as IDisposable)?.Dispose();
        (FileLoaded as IDisposable)?.Dispose();
        (AnimationFuncInvoked as IDisposable)?.Dispose();
    }
}
