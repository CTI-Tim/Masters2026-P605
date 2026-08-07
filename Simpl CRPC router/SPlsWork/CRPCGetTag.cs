using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;
using Crestron;
using Crestron.Logos.SplusLibrary;
using Crestron.Logos.SplusObjects;
using Crestron.SimplSharp;
using Crestron.AppHelperClassesv2_0;

namespace UserModule_CRPCGETTAG
{
    public class UserModuleClass_CRPCGETTAG : SplusObject
    {
        static CCriticalSection g_criticalSection = new CCriticalSection();
        
        
        
        
        Crestron.Logos.SplusObjects.DigitalInput GET_NEW_TAG;
        Crestron.Logos.SplusObjects.DigitalOutput READY;
        Crestron.Logos.SplusObjects.AnalogOutput TAGHIVAL;
        Crestron.Logos.SplusObjects.AnalogOutput TAGLOVAL;
        Crestron.Logos.SplusObjects.StringOutput TAG__DOLLAR__;
        private CrestronString GENERATETAG (  SplusExecutionContext __context__ ) 
            { 
            int GENERATEDTAG = 0;
            
            CrestronString TAG;
            TAG  = new CrestronString( Crestron.Logos.SplusObjects.CrestronStringEncoding.eEncodingASCII, 5, this );
            
            
            __context__.SourceCodeLine = 53;
            GENERATEDTAG = (int) ( CRPCJoinTagGenerator.GetNewTag() ) ; 
            __context__.SourceCodeLine = 56;
            if ( Functions.TestForTrue  ( ( Functions.BoolToInt (GENERATEDTAG == 0))  ) ) 
                { 
                __context__.SourceCodeLine = 58;
                GENERATEDTAG = (int) ( CRPCJoinTagGenerator.GetNewTag() ) ; 
                } 
            
            __context__.SourceCodeLine = 61;
            MakeString ( TAG , "{0:X2}", GENERATEDTAG) ; 
            __context__.SourceCodeLine = 63;
            return ( TAG ) ; 
            
            }
            
        object GET_NEW_TAG_OnPush_0 ( Object __EventInfo__ )
        
            { 
            Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
            try
            {
                SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
                CrestronString TAG;
                TAG  = new CrestronString( Crestron.Logos.SplusObjects.CrestronStringEncoding.eEncodingASCII, 5, this );
                
                
                __context__.SourceCodeLine = 75;
                TAG  .UpdateValue ( GENERATETAG (  __context__  )  ) ; 
                __context__.SourceCodeLine = 77;
                TAGHIVAL  .Value = (ushort) ( Byte( TAG , (int)( 1 ) ) ) ; 
                __context__.SourceCodeLine = 78;
                TAGLOVAL  .Value = (ushort) ( Byte( TAG , (int)( 2 ) ) ) ; 
                __context__.SourceCodeLine = 79;
                TAG__DOLLAR__  .UpdateValue ( TAG  ) ; 
                
                
            }
            catch(Exception e) { ObjectCatchHandler(e); }
            finally { ObjectFinallyHandler( __SignalEventArg__ ); }
            return this;
            
        }
        
    public override object FunctionMain (  object __obj__ ) 
        { 
        CrestronString TAG;
        TAG  = new CrestronString( Crestron.Logos.SplusObjects.CrestronStringEncoding.eEncodingASCII, 5, this );
        
        try
        {
            SplusExecutionContext __context__ = SplusFunctionMainStartCode();
            
            __context__.SourceCodeLine = 92;
            WaitForInitializationComplete ( ) ; 
            __context__.SourceCodeLine = 94;
            TAG  .UpdateValue ( GENERATETAG (  __context__  )  ) ; 
            __context__.SourceCodeLine = 96;
            TAGHIVAL  .Value = (ushort) ( Byte( TAG , (int)( 1 ) ) ) ; 
            __context__.SourceCodeLine = 97;
            TAGLOVAL  .Value = (ushort) ( Byte( TAG , (int)( 2 ) ) ) ; 
            __context__.SourceCodeLine = 98;
            TAG__DOLLAR__  .UpdateValue ( TAG  ) ; 
            __context__.SourceCodeLine = 100;
            READY  .Value = (ushort) ( 1 ) ; 
            
            
        }
        catch(Exception e) { ObjectCatchHandler(e); }
        finally { ObjectFinallyHandler(); }
        return __obj__;
        }
        
    
    public override void LogosSplusInitialize()
    {
        SocketInfo __socketinfo__ = new SocketInfo( 1, this );
        InitialParametersClass.ResolveHostName = __socketinfo__.ResolveHostName;
        _SplusNVRAM = new SplusNVRAM( this );
        
        GET_NEW_TAG = new Crestron.Logos.SplusObjects.DigitalInput( GET_NEW_TAG__DigitalInput__, this );
        m_DigitalInputList.Add( GET_NEW_TAG__DigitalInput__, GET_NEW_TAG );
        
        READY = new Crestron.Logos.SplusObjects.DigitalOutput( READY__DigitalOutput__, this );
        m_DigitalOutputList.Add( READY__DigitalOutput__, READY );
        
        TAGHIVAL = new Crestron.Logos.SplusObjects.AnalogOutput( TAGHIVAL__AnalogSerialOutput__, this );
        m_AnalogOutputList.Add( TAGHIVAL__AnalogSerialOutput__, TAGHIVAL );
        
        TAGLOVAL = new Crestron.Logos.SplusObjects.AnalogOutput( TAGLOVAL__AnalogSerialOutput__, this );
        m_AnalogOutputList.Add( TAGLOVAL__AnalogSerialOutput__, TAGLOVAL );
        
        TAG__DOLLAR__ = new Crestron.Logos.SplusObjects.StringOutput( TAG__DOLLAR____AnalogSerialOutput__, this );
        m_StringOutputList.Add( TAG__DOLLAR____AnalogSerialOutput__, TAG__DOLLAR__ );
        
        
        GET_NEW_TAG.OnDigitalPush.Add( new InputChangeHandlerWrapper( GET_NEW_TAG_OnPush_0, false ) );
        
        _SplusNVRAM.PopulateCustomAttributeList( true );
        
        NVRAM = _SplusNVRAM;
        
    }
    
    public override void LogosSimplSharpInitialize()
    {
        
        
    }
    
    public UserModuleClass_CRPCGETTAG ( string InstanceName, string ReferenceID, Crestron.Logos.SplusObjects.CrestronStringEncoding nEncodingType ) : base( InstanceName, ReferenceID, nEncodingType ) {}
    
    
    
    
    const uint GET_NEW_TAG__DigitalInput__ = 0;
    const uint READY__DigitalOutput__ = 0;
    const uint TAGHIVAL__AnalogSerialOutput__ = 0;
    const uint TAGLOVAL__AnalogSerialOutput__ = 1;
    const uint TAG__DOLLAR____AnalogSerialOutput__ = 2;
    
    [SplusStructAttribute(-1, true, false)]
    public class SplusNVRAM : SplusStructureBase
    {
    
        public SplusNVRAM( SplusObject __caller__ ) : base( __caller__ ) {}
        
        
    }
    
    SplusNVRAM _SplusNVRAM = null;
    
    public class __CEvent__ : CEvent
    {
        public __CEvent__() {}
        public void Close() { base.Close(); }
        public int Reset() { return base.Reset() ? 1 : 0; }
        public int Set() { return base.Set() ? 1 : 0; }
        public int Wait( int timeOutInMs ) { return base.Wait( timeOutInMs ) ? 1 : 0; }
    }
    public class __CMutex__ : CMutex
    {
        public __CMutex__() {}
        public void Close() { base.Close(); }
        public void ReleaseMutex() { base.ReleaseMutex(); }
        public int WaitForMutex() { return base.WaitForMutex() ? 1 : 0; }
    }
     public int IsNull( object obj ){ return (obj == null) ? 1 : 0; }
}


}
