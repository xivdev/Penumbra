using System;
using System.Collections;
using System.Collections.Generic;

namespace Penumbra.GameData.ByteString;

// Utf8String is a wrapper around unsafe byte strings.
// It may be used to store owned strings in unmanaged space,
// as well as refer to unowned strings.
// Unowned strings may change their value and thus become corrupt,
// so they should never be stored, just used locally or with great care.
// The string keeps track of whether it is owned or not, it also can keep track
// of some other information, like the string being pure ASCII, ASCII-lowercase or null-terminated.
// Owned strings are always null-terminated.
// Any constructed string will compute its own CRC32-value (as long as the string itself is not changed).
public sealed unsafe partial class Utf8String : IEnumerable< byte >
{
    // We keep information on some of the state of the Utf8String in specific bits.
    // This costs some potential max size, but that is not relevant for our case.
    // Except for destruction/dispose, or if the non-owned pointer changes values,
    // the CheckedFlag, AsciiLowerCaseFlag and AsciiFlag are the only things that are mutable.
    private const uint NullTerminatedFlag    = 0x80000000;
    private const uint OwnedFlag             = 0x40000000;
    private const uint AsciiCheckedFlag      = 0x04000000;
    private const uint AsciiFlag             = 0x08000000;
    private const uint AsciiLowerCheckedFlag = 0x10000000;
    private const uint AsciiLowerFlag        = 0x20000000;
    private const uint FlagMask              = 0x03FFFFFF;

    public bool IsNullTerminated
        => ( _length & NullTerminatedFlag ) != 0;

    public bool IsOwned
        => ( _length & OwnedFlag ) != 0;

    public bool IsAscii
        => CheckAscii();

    public bool IsAsciiLowerCase
        => CheckAsciiLower();

    public byte* Path
        => _path;

    public int Crc32
        => _crc32;

    public int Length
        => ( int )( _length & FlagMask );

    public bool IsEmpty
        => Length == 0;

    public ReadOnlySpan< byte > Span
        => new(_path, Length);

    public byte this[ int idx ]
        => ( uint )idx < Length ? _path[ idx ] : throw new IndexOutOfRangeException();

    public IEnumerator< byte > GetEnumerator()
    {
        for( var i = 0; i < Length; ++i )
        {
            yield return Span[ i ];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    // Only not readonly due to dispose.
    // ReSharper disable once NonReadonlyMemberInGetHashCode
    public override int GetHashCode()
        => _crc32;
}