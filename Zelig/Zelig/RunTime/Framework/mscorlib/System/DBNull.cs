// ==++==
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--==
////////////////////////////////////////////////////////////////////////////////
// Void
//  This class represents a Missing Variant
////////////////////////////////////////////////////////////////////////////////
namespace System
{
    using System;
////using System.Runtime.Remoting;
    using System.Runtime.Serialization;

    [Serializable]
    public sealed class DBNull /*: ISerializable, IConvertible*/
    {
        public static readonly DBNull Value = new DBNull();

        //Package private constructor
        private DBNull()
        {
        }

////    private DBNull( SerializationInfo info, StreamingContext context )
////    {
////        throw new NotSupportedException( Environment.GetResourceString( "NotSupported_DBNullSerial" ) );
////    }
////
////    public void GetObjectData( SerializationInfo info, StreamingContext context )
////    {
////        UnitySerializationHolder.GetUnitySerializationInfo( info, UnitySerializationHolder.NullUnity, null, null );
////    }
////
////    public override String ToString()
////    {
////        return String.Empty;
////    }

        #region IConvertible
////
////    public TypeCode GetTypeCode()
////    {
////        return TypeCode.DBNull;
////    }
////
////    /// <internalonly/>
////    bool IConvertible.ToBoolean( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    char IConvertible.ToChar( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    sbyte IConvertible.ToSByte( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    byte IConvertible.ToByte( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    short IConvertible.ToInt16( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    ushort IConvertible.ToUInt16( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    int IConvertible.ToInt32( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    uint IConvertible.ToUInt32( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    long IConvertible.ToInt64( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    ulong IConvertible.ToUInt64( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    float IConvertible.ToSingle( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    double IConvertible.ToDouble( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    decimal IConvertible.ToDecimal( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    /// <internalonly/>
////    DateTime IConvertible.ToDateTime( IFormatProvider provider )
////    {
////        throw new InvalidCastException( Environment.GetResourceString( "InvalidCast_FromDBNull" ) );
////    }
////
////    public String ToString( IFormatProvider provider )
////    {
////        return String.Empty;
////    }
////
////    /// <internalonly/>
////    Object IConvertible.ToType( Type type, IFormatProvider provider )
////    {
////        return Convert.DefaultToType( (IConvertible)this, type, provider );
////    }

        #endregion
    }
}

