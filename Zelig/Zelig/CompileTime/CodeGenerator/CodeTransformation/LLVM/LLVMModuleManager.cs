﻿//
// Copyright (c) Microsoft Corporation.    All rights reserved.
//


namespace Microsoft.Zelig.LLVM
{

    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.IO;

    using Importer = Microsoft.Zelig.MetaData.Importer;
    using Normalized = Microsoft.Zelig.MetaData.Normalized;
    using IR = Microsoft.Zelig.CodeGeneration.IR;
    using TS = Microsoft.Zelig.Runtime.TypeSystem;

    public partial class LLVMModuleManager
    {
        private _Module m_module;
        private readonly string m_imageName;
        private readonly IR.TypeSystemForCodeTransformation m_typeSystem;

        private GrowOnlyHashTable <TS.TypeRepresentation,LLVM._Type>                 m_typeRepresentationsToType;
        private GrowOnlyHashTable <TS.MethodRepresentation, Debugging.DebugInfo >    m_debugInfoForMethods;
        private GrowOnlyHashTable < IR.DataManager.DataDescriptor, LLVM._Value >     m_globalInitializedValues;
        private uint                                                                 m_nativeIntSize;
        private bool                                                                 m_typeSystemAlreadyConverted;
        private bool                                                                 m_turnOffCompilationAndValidation;

        //TypeSystem might not be completelly initialized at this point.
        public LLVMModuleManager( IR.TypeSystemForCodeTransformation typeSystem, string imageName )
        {
            m_imageName = imageName;
            m_typeSystem = typeSystem;

            m_nativeIntSize = 32;
            m_debugInfoForMethods = HashTableFactory.New<TS.MethodRepresentation, Debugging.DebugInfo>( );
            m_typeRepresentationsToType = HashTableFactory.New<TS.TypeRepresentation, LLVM._Type>( );
            m_globalInitializedValues = HashTableFactory.New<IR.DataManager.DataDescriptor, LLVM._Value>( );

            m_typeSystemAlreadyConverted = false;
            m_turnOffCompilationAndValidation = false;

            m_module = new _Module( m_imageName, m_nativeIntSize );
        }

        public void Compile( )
        {
            CompleteMissingDataDescriptors( );

            if( !m_turnOffCompilationAndValidation )
            {
                TS.MethodRepresentation bootstrapResetMR = m_typeSystem.GetWellKnownMethod( "Bootstrap_Initialization" );
                LLVM._Function bootstrapReset = GetOrInsertFunction( bootstrapResetMR );
                m_module.CreateAlias( bootstrapReset, "main" );
                m_module.Compile( );
            }
        }

        public void DumpToFile( string filename, bool textRepresentation )
        {
            m_module.DumpToFile( filename, textRepresentation );
        }

        public void TurnOffCompilationAndValidation( )
        {
            m_turnOffCompilationAndValidation = true;
        }

        public LLVM._Function GetOrInsertFunction( TS.MethodRepresentation md )
        {
            return m_module.GetOrInsertFunction( GetFullMethodName( md ), GetOrInsertType( md ) );
        }

        public static string GetFullMethodName( TS.MethodRepresentation md )
        {
            if( md.Flags.HasFlag( TS.MethodRepresentation.Attributes.PinvokeImpl ) )
            {
                return md.Name;
            }
            return md.ToShortString( ) + "_" + md.GetHashCode( );
        }

        private void CompleteMissingDataDescriptors( )
        {
            bool stillMissing = true;

            while( stillMissing )
            {
                stillMissing = false;
             
                foreach( var dd in m_globalInitializedValues.Keys )
                {                    
                    if( m_globalInitializedValues[ dd ].IsAnUninitializedGlobal( ) )
                    {
                        stillMissing = true;                        
                        m_globalInitializedValues[ dd ].SetGlobalInitializer( UCVFromDataDescriptor( dd ) );
                        break;
                    }
                }
            }

        }

        private Llvm.NET.Constant GetUCVStruct( LLVM._Type type, bool anon, params Llvm.NET.Constant[ ] vals )
        {
            var fields = new List<Llvm.NET.Constant>( );
            foreach( var val in vals )
            {
                fields.Add( val );
            }
            return m_module.GetUCVStruct( type, fields, anon );
        }

        private object GetDirectFromArrayDescriptor( IR.DataManager.ArrayDescriptor ad, int pos )
        {
            return ad.Values != null ? ad.Values[ pos ] : ad.Source.GetValue( pos );
        }

        private Llvm.NET.Constant GetUCVArray( IR.DataManager.ArrayDescriptor ad )
        {
            uint arrayLength = ( uint )( ( ad.Values == null ) ? ad.Source.Length : ad.Values.Length );

            var fields = new List<Llvm.NET.Constant>( );
            TS.TypeRepresentation elTR = ( ( TS.ArrayReferenceTypeRepresentation )ad.Context ).ContainedType;

            //
            // Special case for common scalar arrays.
            //
            if( ad.Values == null )
            {
                for( uint i = 0; i < arrayLength; i++ )
                {
                    object fdVal = GetDirectFromArrayDescriptor( ad, ( int )i );
                    fields.Add( GetScalarTypeUCV( elTR, fdVal is ulong ? ( ulong )fdVal : ( ulong )Convert.ToInt64( fdVal ) ) );
                }
            }
            else
            {
                for( uint i = 0; i < arrayLength; i++ )
                {
                    object val = GetDirectFromArrayDescriptor( ad, ( int )i );

                    if( val == null )
                    {
                        fields.Add( GetScalarTypeUCV( elTR, 0 ) );
                    }
                    else if( val is IR.DataManager.DataDescriptor )
                    {
                        IR.DataManager.DataDescriptor valDD = ( IR.DataManager.DataDescriptor )val;

                        if( valDD.Nesting != null )
                        {
                            fields.Add( UCVFromDataDescriptor( valDD ) );
                        }
                        else
                        {
                            fields.Add( m_module.GetUCVConstantPointerFromValue( GetConstantPointerToDD( valDD ) ) );
                        }
                    }
                    else if( ad.Context.ContainedType is TS.ScalarTypeRepresentation )
                    {
                        fields.Add( GetScalarTypeUCV( elTR, val is ulong ? ( ulong )val : ( ulong )Convert.ToInt64( val ) ) );
                    }
                    else
                    {
                        throw TypeConsistencyErrorException.Create( "Don't know how to write {0}", val );
                    }
                }
            }

            return m_module.GetUCVArray( GetOrInsertType( elTR ), fields );
        }

        private Llvm.NET.Constant GetScalarTypeUCV( TS.TypeRepresentation tr, UInt64 v )
        {
            if( v == 0 )
            {
                return m_module.GetUCVZeroInitialized( GetOrInsertType( tr ) );
            }
            else
            {
                Llvm.NET.Constant ucv = GetUCVIntAsSVT( tr, ( UInt64 )v );
                ucv = GetUCVStruct( GetOrInsertType( tr ), false, ucv );
                return ucv;
            }
        }

        private Llvm.NET.Constant GetUCVIntAsSVT( TS.TypeRepresentation tr, UInt64 v )
        {
            return m_module.GetUCVInt( GetOrInsertBasicTypeAsLLVMSingleValueType( tr ), v, tr.IsSigned );
        }

        private bool TypeChangesAfterCreation( IR.DataManager.DataDescriptor dd )
        {
            return dd is IR.DataManager.ArrayDescriptor || dd.Context == m_typeSystem.WellKnownTypes.System_String;
        }

        private void AddToGlobalInitializedValues( IR.DataManager.DataDescriptor dd, LLVM._Value global )
        {
            if( m_globalInitializedValues.ContainsKey( dd ) )
            {
                global.MergeToAndRemove( m_globalInitializedValues[ dd ] );
            }
            else 
            {
                m_globalInitializedValues[ dd ] = global;
                if( !dd.IsMutable )
                {
                    m_globalInitializedValues[ dd ].FlagAsConstant( );
                }
            }
        }

        private LLVM._Value GetConstantPointerToDD( IR.DataManager.DataDescriptor dd )
        {
            if( !m_globalInitializedValues.ContainsKey( dd ) )
            {
                //I force initialization of arrays and strings to correctly set the static
                //type for the array.
                if( TypeChangesAfterCreation( dd ) )
                {
                    Llvm.NET.Constant ucv = UCVFromDataDescriptor( dd );
                    AddToGlobalInitializedValues( dd, m_module.GetGlobalFromUCV( GetOrInsertType( dd.Context ), ucv ) );
                }
                else
                {
                    AddToGlobalInitializedValues( dd, m_module.GetUninitializedGlobal( GetOrInsertType( dd.Context ) ) );
                }
            }

            return m_globalInitializedValues[ dd ];
        }

        private Llvm.NET.Constant GetUCVObjectHeader( IR.DataManager.DataDescriptor dd )
        {
            TS.WellKnownFields wkf = m_typeSystem.WellKnownFields;
            TS.WellKnownTypes wkt = m_typeSystem.WellKnownTypes;

            var fields = new List<Llvm.NET.Constant>( );

            Runtime.ObjectHeader.GarbageCollectorFlags flags;

            if( dd.IsMutable )
            {
                flags = Runtime.ObjectHeader.GarbageCollectorFlags.UnreclaimableObject;
            }
            else
            {
                flags = Runtime.ObjectHeader.GarbageCollectorFlags.ReadOnlyObject;
            }


            fields.Add( GetScalarTypeUCV( wkf.ObjectHeader_MultiUseWord.FieldType, ( UInt64 )flags ) ); //MultiUser Word
            fields.Add( m_module.GetUCVConstantPointerFromValue( GetConstantPointerToDD( ( IR.DataManager.DataDescriptor )dd.Owner.GetObjectDescriptor( dd.Context.VirtualTable ) ) ) ); //VTable

            return m_module.GetUCVStruct( GetOrInsertType( wkt.Microsoft_Zelig_Runtime_ObjectHeader ), fields, false );
        }

        public Llvm.NET.Constant UCVFromDataDescriptor( IR.DataManager.DataDescriptor dd, TS.TypeRepresentation treatObjectDescriptorAsType = null )
        {
            TS.WellKnownFields wkf = m_typeSystem.WellKnownFields;
            TS.WellKnownTypes wkt = m_typeSystem.WellKnownTypes;

            if( dd is IR.DataManager.ObjectDescriptor )
            {
                IR.DataManager.ObjectDescriptor od = ( IR.DataManager.ObjectDescriptor )dd;
                TS.TypeRepresentation currentType = treatObjectDescriptorAsType;

                if( currentType == null )
                    currentType = dd.Context;


                if( currentType == wkt.System_Object )
                {
                    Llvm.NET.Constant ucv = GetUCVObjectHeader( dd );
                    return GetUCVStruct( GetOrInsertType( wkt.System_Object ), false, ucv );
                }

                var fields = new List<Llvm.NET.Constant>( );

                if( !( currentType is TS.ValueTypeRepresentation ) )
                {
                    fields.Add( UCVFromDataDescriptor( dd, currentType.Extends ) );
                }


                for( uint i = 0; i < currentType.Size - ( currentType.Extends == null ? 0 : currentType.Extends.Size ); i++ )
                {
                    var fd = currentType.FindFieldAtOffset( ( int )( i + ( currentType.Extends == null ? 0 : currentType.Extends.Size ) ) );
                   
                    if( fd != null )
                    {
                        i += fd.FieldType.SizeOfHoldingVariable - 1;

                        if( !od.Values.ContainsKey( fd ) )
                        {
                            fields.Add( m_module.GetUCVZeroInitialized( GetOrInsertType( fd.FieldType ) ) );
                            continue;
                        }

                        var fdVal = od.Values[ fd ];

                        if( fd == wkf.CodePointer_Target )
                        {
                            //
                            // Special case for code pointers: substitute with actual code pointers.
                            //
                            IntPtr id = ( IntPtr )fdVal;

                            object ptr = od.Owner.GetCodePointerFromUniqueID( id );

                            if( ptr is TS.MethodRepresentation )
                            {
                                TS.MethodRepresentation md = ( TS.MethodRepresentation )ptr;
                                Llvm.NET.Constant ucv = m_module.GetUCVConstantPointerFromValue( GetOrInsertFunction( md ) );
                                ucv = GetUCVStruct( GetOrInsertType( fd.FieldType ), false, ucv );
                                fields.Add( ucv );
                            }
                            else if( ptr is IR.ExceptionHandlerBasicBlock )
                            {
                                IR.ExceptionHandlerBasicBlock ehBB = ( IR.ExceptionHandlerBasicBlock )ptr;
                                throw new Exception( "Warning ExceptionHandlerBasicBlock not handled:" + ptr );
                            }
                            else
                            {
                                throw new Exception( "I'm not sure what kind of code pointer is this:" + ptr );
                            }

                        }
                        else if( fdVal == null )
                        {
                            fields.Add( GetScalarTypeUCV( fd.FieldType, 0 ) );
                        }
                        else if( fdVal is IR.DataManager.DataDescriptor )
                        {
                            IR.DataManager.DataDescriptor valDD = ( IR.DataManager.DataDescriptor )fdVal;

                            if( valDD.Nesting != null )
                            {
                                fields.Add( UCVFromDataDescriptor( valDD ) );
                            }
                            else
                            {
                                fields.Add( m_module.GetUCVConstantPointerFromValue( GetConstantPointerToDD( valDD ) ) );
                            }
                        }
                        else if( fd.FieldType is TS.ScalarTypeRepresentation )
                        {
                            fields.Add( GetScalarTypeUCV( fd.FieldType, fdVal is ulong ? ( ulong )fdVal : ( ulong )Convert.ToInt64( fdVal ) ) );
                        }
                        else
                        {
                            throw TypeConsistencyErrorException.Create( "Don't know how to write {0}", fdVal );
                        }
                    }
                    else
                    {
                        fields.Add( GetScalarTypeUCV( wkt.System_Byte, 0xAB ) );
                    }
                }

                if( currentType == wkt.System_String )
                {
                    var chars = new List<Llvm.NET.Constant>( );
                    foreach( var c in ( ( string )od.Source ).ToCharArray( ) )
                    {
                        chars.Add( GetScalarTypeUCV( wkf.StringImpl_FirstChar.FieldType, ( ulong )c ) );

                    }
                    fields.Add( m_module.GetUCVArray( GetOrInsertType( wkf.StringImpl_FirstChar.FieldType ), chars ) );
                }

                return m_module.GetUCVStruct( GetOrInsertType( currentType ), fields, currentType == wkt.System_String );

            }
            else if( dd is IR.DataManager.ArrayDescriptor )
            {
                IR.DataManager.ArrayDescriptor ad = ( IR.DataManager.ArrayDescriptor )dd;

                Llvm.NET.Constant ucv = GetUCVObjectHeader( dd );
                ucv = GetUCVStruct( GetOrInsertType( wkt.System_Object ), false, ucv );
                ucv = GetUCVStruct( GetOrInsertType( wkt.System_Array ), false, ucv, GetScalarTypeUCV( wkf.ArrayImpl_m_numElements.FieldType, ( ulong )ad.Length ) );
                return GetUCVStruct( GetOrInsertType( dd.Context ), true, ucv, GetUCVArray( ad ) );
            }
            else
            {
                throw new System.InvalidOperationException( "DataDescriptor type not supported:" + dd );
            }
        }


        public LLVM._Value GlobalValueFromDataDescriptor( IR.DataManager.DataDescriptor dd )
        {
            if( !m_globalInitializedValues.ContainsKey( dd ) )
            {
                Llvm.NET.Constant ucv = UCVFromDataDescriptor( dd );
                
                //Split the global creation in two step for DDs that keep their type the same
                //after conversion

                if( TypeChangesAfterCreation( dd ) )
                {
                    AddToGlobalInitializedValues( dd, m_module.GetGlobalFromUCV( GetOrInsertType( dd.Context ), ucv ) );
                }
                else
                {
                    AddToGlobalInitializedValues(dd, m_module.GetUninitializedGlobal(GetOrInsertType(dd.Context)));
                    m_globalInitializedValues[dd].SetGlobalInitializer(ucv);
                }
            }

            return m_globalInitializedValues[ dd ];
        }

        public LLVM._Module Module
        {
            get
            {
                return m_module;
            }
        }
        public uint NativeIntSize
        {
            get
            {
                return m_nativeIntSize;
            }
        }

        public GrowOnlyHashTable<TS.MethodRepresentation, Debugging.DebugInfo> DebugInfoForMethods
        {
            get
            {
                return m_debugInfoForMethods;
            }
        }

    }
}
