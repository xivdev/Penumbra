using System;

namespace Penumbra.Util
{
    /// <summary>
    /// Basic service locator
    /// </summary>
    /// <typeparam name="T">The class you want to store in the service locator</typeparam>
    public static class Service< T > where T : class
    {
        private static T? _object;

        static Service()
        { }

        public static void Set( T obj )
        {
            // ReSharper disable once JoinNullCheckWithUsage
            if( obj == null )
            {
                throw new ArgumentNullException( $"{nameof( obj )} is null!" );
            }

            _object = obj;
        }

        public static T Set()
        {
            _object = Activator.CreateInstance< T >();

            return _object;
        }

        public static T Set( params object[] args )
        {
            var obj = ( T? )Activator.CreateInstance( typeof( T ), args );

            // ReSharper disable once JoinNullCheckWithUsage
            if( obj == null )
            {
                throw new Exception( "what he fuc" );
            }

            _object = obj;

            return obj;
        }

        public static T Get()
        {
            if( _object == null )
            {
                throw new InvalidOperationException( $"{nameof( T )} hasn't been registered!" );
            }

            return _object;
        }
    }
}