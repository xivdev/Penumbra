using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Penumbra.Extensions
{
    public static class FuckedExtensions
    {
        private delegate ref TFieldType RefGet< in TObject, TFieldType >( TObject obj );

        /// <summary>
        /// Create a delegate which will return a zero-copy reference to a given field in a manner that's fucked tiers of quick and
        /// fucked tiers of stupid, but hey, why not?
        /// </summary>
        /// <remarks>
        /// The only thing that this can't do is inline, this always ends up as a call instruction because we're generating code at
        /// runtime and need to jump to it. That said, this is still super quick and provides a convenient and type safe shim around
        /// a primitive type
        ///
        /// You can use the resultant <see cref="RefGet{TObject,TFieldType}"/> to access a ref to a field on an object without invoking any
        /// unsafe code too.
        /// </remarks>
        /// <param name="fieldName">The name of the field to grab a reference to</param>
        /// <typeparam name="TObject">The object that holds the field</typeparam>
        /// <typeparam name="TField">The type of the underlying field</typeparam>
        /// <returns>A delegate that will return a reference to a particular field - zero copy</returns>
        /// <exception cref="MissingFieldException"></exception>
        private static RefGet< TObject, TField > CreateRefGetter< TObject, TField >( string fieldName )
            where TField : unmanaged
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

            var fieldInfo = typeof( TObject ).GetField( fieldName, flags );
            if( fieldInfo == null )
            {
                throw new MissingFieldException( typeof( TObject ).Name, fieldName );
            }

            var dm = new DynamicMethod(
                $"__refget_{typeof( TObject ).Name}_{fieldInfo.Name}",
                typeof( TField ).MakeByRefType(),
                new[] { typeof( TObject ) },
                typeof( TObject ),
                true
            );

            var il = dm.GetILGenerator();

            il.Emit( OpCodes.Ldarg_0 );
            il.Emit( OpCodes.Ldflda, fieldInfo );
            il.Emit( OpCodes.Ret );

            return ( RefGet< TObject, TField > )dm.CreateDelegate( typeof( RefGet< TObject, TField > ) );
        }

        private static readonly RefGet< string, byte > StringRefGet = CreateRefGetter< string, byte >( "_firstChar" );

        public static unsafe IntPtr UnsafePtr( this string str )
        {
            // nb: you can do it without __makeref but the code becomes way shittier because the way of getting the ptr
            // is more fucked up so it's easier to just abuse __makeref
            // but you can just use the StringRefGet func to get a `ref byte` too, though you'll probs want a better delegate so it's
            // actually usable, lol
            var fieldRef = __makeref( StringRefGet( str ) );

            return *( IntPtr* )&fieldRef;
        }

        public static unsafe int UnsafeLength( this string str )
        {
            var fieldRef = __makeref( StringRefGet( str ) );

            // c# strings are utf16 so we just multiply len by 2 to get the total byte count + 2 for null terminator (:D)
            // very simple and intuitive

            // this also maps to a defined structure, so you can just move the pointer backwards to read from the native string struct
            // see: https://github.com/dotnet/coreclr/blob/master/src/vm/object.h#L897-L909
            return *( int* )( *( IntPtr* )&fieldRef - 4 ) * 2 + 2;
        }
    }
}