// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*=============================================================================
**
** Class: TimeoutException
**
**
** Purpose: Exception class for Timeout
**
**
=============================================================================*/

namespace System
{
    using System.Runtime.Serialization;

    [Serializable()]
////[System.Runtime.InteropServices.ComVisible( true )]
    public class TimeoutException : SystemException
    {

#if EXCEPTION_STRINGS
        public TimeoutException() : base( Environment.GetResourceString( "Arg_TimeoutException" ) )
#else
        public TimeoutException()
#endif
        {
////        SetErrorCode( __HResults.COR_E_TIMEOUT );
        }

        public TimeoutException( String message ) : base( message )
        {
////        SetErrorCode( __HResults.COR_E_TIMEOUT );
        }

        public TimeoutException( String message, Exception innerException ) : base( message, innerException )
        {
////        SetErrorCode( __HResults.COR_E_TIMEOUT );
        }

        //
        //This constructor is required for serialization.
        //
////    protected TimeoutException( SerializationInfo info, StreamingContext context ) : base( info, context )
////    {
////    }
    }
}

