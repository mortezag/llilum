// ==++==
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--==
namespace System
{
    using System;

    public interface IFormattable
    {
        String ToString( String format, IFormatProvider formatProvider );
    }
}
