using Penumbra.GameData.Util;

namespace Penumbra.GameData.ByteString;

public static unsafe partial class ByteStringFunctions
{
    // Lexicographically compare two byte arrays of given length.
    public static int Compare( byte* lhs, int lhsLength, byte* rhs, int rhsLength )
    {
        if( lhsLength == rhsLength )
        {
            return lhs == rhs ? 0 : Functions.MemCmpUnchecked( lhs, rhs, rhsLength );
        }

        if( lhsLength < rhsLength )
        {
            var cmp = Functions.MemCmpUnchecked( lhs, rhs, lhsLength );
            return cmp != 0 ? cmp : -1;
        }

        var cmp2 = Functions.MemCmpUnchecked( lhs, rhs, rhsLength );
        return cmp2 != 0 ? cmp2 : 1;
    }

    // Lexicographically compare one byte array of given length with a null-terminated byte array of unknown length.
    public static int Compare( byte* lhs, int lhsLength, byte* rhs )
    {
        var end = lhs + lhsLength;
        for( var tmp = lhs; tmp < end; ++tmp, ++rhs )
        {
            if( *rhs == 0 )
            {
                return 1;
            }

            var diff = *tmp - *rhs;
            if( diff != 0 )
            {
                return diff;
            }
        }

        return 0;
    }

    // Lexicographically compare two null-terminated byte arrays of unknown length not larger than maxLength.
    public static int Compare( byte* lhs, byte* rhs, int maxLength = int.MaxValue )
    {
        var end = lhs + maxLength;
        for( var tmp = lhs; tmp < end; ++tmp, ++rhs )
        {
            if( *lhs == 0 )
            {
                return *rhs == 0 ? 0 : -1;
            }

            if( *rhs == 0 )
            {
                return 1;
            }

            var diff = *tmp - *rhs;
            if( diff != 0 )
            {
                return diff;
            }
        }

        return 0;
    }

    // Check two byte arrays of given length for equality.
    public static bool Equals( byte* lhs, int lhsLength, byte* rhs, int rhsLength )
    {
        if( lhsLength != rhsLength )
        {
            return false;
        }

        if( lhs == rhs || lhsLength == 0 )
        {
            return true;
        }

        return Functions.MemCmpUnchecked( lhs, rhs, lhsLength ) == 0;
    }

    // Check one byte array of given length for equality against a null-terminated byte array of unknown length.
    private static bool Equal( byte* lhs, int lhsLength, byte* rhs )
        => Compare( lhs, lhsLength, rhs ) == 0;

    // Check two null-terminated byte arrays of unknown length not larger than maxLength for equality.
    private static bool Equal( byte* lhs, byte* rhs, int maxLength = int.MaxValue )
        => Compare( lhs, rhs, maxLength ) == 0;
}