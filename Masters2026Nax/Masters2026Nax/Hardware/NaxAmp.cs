// Using crestron libraries, turn this off for your own sanity.
#nullable disable

using Crestron.SimplSharpPro.AudioDistribution;
using Crestron.SimplSharpPro.EthernetCommunication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Masters2026Nax.Hardware
{
    // -----------------------------------------------------------------------------
    // Why don't your classes do lots of validation?
    //
    // This class assumes the programmer is using the API as documented. The goal
    // is to keep the code simple, readable, and easy to understand rather than
    // filling every method with defensive checks.
    //
    // If I was coding a public library used by thousands of developers, especially one
    // with incomplete or poor documentation, additional validation would make more
    // sense because you cannot control how the API is used.
    //
    // In a well-documented library, however, the documentation is part of the
    // contract. The programmer is expected to read it and use the API correctly.
    //
    // Yes I just said the programmer is expected to read documentation.
    //
    // Adding dozens of checks to guard against incorrect usage often adds more code,
    // more complexity, and more maintenance than it adds value. As with everything in
    // software engineering, this is a design decision, not a rule.
    // Once again only add complexity when it solves a real problem,
    // not simply because you can.
    // -----------------------------------------------------------------------------

    /// <summary>
    /// This is a wrapper class to simplify and unify operation of crestron hardware.
    /// Yes this class is going to be tightly coupled but it's not designed to really
    /// be infinately reuseable.  It can be re-used system to system and you could
    /// create an array of NaxAmp's in this program, but it's specific to this hardware
    /// and specific to my use decisions.
    /// 
    /// It's OK to make code that is tightly coupled, not everything has to be abstracted
    /// </summary>
    public class NaxAmp
    {
        DmNax8Zsa myNaxAmp;

        public event EventHandler<ZoneFeedbackEventArgs> ZoneFeedbackChanged;
        //public event EventHandler<AmplifierFeedbackEventArgs> AmplifierFeedbackChanged;
        public event EventHandler<CrpcMessageEventArgs> CrpcMessageReceived;

          public NaxAmp(ushort AmpIpid, ControlSystem cs )
        {
            myNaxAmp = new DmNax8Zsa( AmpIpid, cs );
            myNaxAmp.OnZoneChange += MyNaxAmp_OnZoneChange;
            myNaxAmp.Players.TransportControlPropertyChange += Players_TransportControlPropertyChange;
            myNaxAmp.Register();

            // Set everything to auto initiate as we expect it
            // also defailt to no stream in startup
            foreach(var rx in myNaxAmp.Nax.Rx)
            {
                rx.AutomaticInitiationEnable();
                rx.MulticastAddress.StringValue = "0.0.0.0";  
            }


        }

        /// <summary>
        /// Select audio zone and source to route. Invalid selection does nothing
        /// 
        /// </summary>
        /// <param name="Zone"> Audio Output 1-8</param>
        /// <param name="Source"> 0 No Audio, 1-4 DigIn,5-8Analog,9-16MediaPlayer,17 AES67Stream</param>
        public void RouteAudio(uint Zone, DmNaxSource Source)
        {
            if (Zone >= 0 || Zone < 8)
            {
                // myNaxAmp.Zones[Zone]!  we added the ! to tell the compiler to shut up we know it's not nullable if we did not shit off the notices above.
                myNaxAmp.Zones[Zone]!.Source.UShortValue = (ushort)Source;
            }
        }
        /// <summary>
        /// Sets the stream for this Zone's AES67 reciever that is at source 17.
        /// </summary>
        /// <param name="Zone"></param>
        /// <param name="Address"></param>
        /// <param name="Port"></param>
        public void SetStream(uint Zone, string Address, short Port)
        {
            myNaxAmp.Nax.Rx[Zone].Port.ShortValue = Port;
            myNaxAmp.Nax.Rx[Zone].MulticastAddress.StringValue = Address;
        }
        public void ClearStream(uint Zone)
        {
            myNaxAmp.Nax.Rx[Zone].MulticastAddress.StringValue = "0.0.0.0";
        }

        /// <summary>
        /// Sends CRPC data to the chosen player in the NAXAmp
        /// </summary>
        /// <param name="player">Index of the player desired</param>
        /// <param name="CrpcPayload">CRPC string payload to send to the player, usually from the touchpanel media player object or a CRPC router</param>
        public void SendCrpc(uint player, string CrpcPayload)
        {
            myNaxAmp.Players.TransportControls[player].Crpc.StringValue = CrpcPayload;
        }


        // ################################### helper methods

        /// <summary>
        /// Converts a DM-NAX source number into its corresponding DmNaxSource
        /// enumeration. This is primarily used when converting numeric values from the
        /// Message Broker or other external interfaces into the strongly typed enum
        /// used by the wrapper.
        /// </summary>
        /// <param name="sourceNumber">
        /// The numeric source number reported by the hardware.
        /// </param>
        /// <returns>
        /// The corresponding DmNaxSource enumeration.
        /// </returns>
        public static DmNaxSource GetSourceFromNumber(ushort sourceNumber)
        {
            return (DmNaxSource)sourceNumber;
        }

        /// <summary>
        /// Converts a DmNaxSource enumeration into the numeric source value expected by
        /// the DM-NAX hardware. This is primarily used when sending commands to the
        /// hardware or publishing source information through the Message Broker.
        /// </summary>
        /// <param name="source">
        /// The source enumeration.
        /// </param>
        /// <returns>
        /// The numeric source value.
        /// </returns>
        public static ushort GetSourceNumber(DmNaxSource source)
        {
            return (ushort)source;
        }

        //  ############################# Event Handlers From Hardware

        private void MyNaxAmp_OnZoneChange(object sender, ZoneEventArgs args)
        {
            // we build our object to send to the event. Note some of the data we can not get
            // from the event so we instead read it directly.  Why move extra work to your 
            // event handler elsewhere when we can simply do it here.
            ZoneFeedbackEventArgs e = new ZoneFeedbackEventArgs
            {
                ZoneNumber = args.Zone.Number,
                SourceFb = (DmNaxSource)args.Zone.SourceFeedback.ShortValue,
                VolumeFb = args.Zone.VolumeFeedback.UShortValue,
                AmpOnFb = myNaxAmp.Zones[args.Zone.Number].ZoneAmplifierOnFeedback.BoolValue
            };


            RaiseZoneFeedbackChanged(e); //Fire the event

        }

        private void Players_TransportControlPropertyChange(DmNaxXPlayers device, PlayerTransportControlEventArgs paramArgs)
        {
            var player = paramArgs.PlayerNumber;
            var crpc = myNaxAmp.Players.TransportControls[player].CrpcFeedback;
        }


        // Our Event Handlers
        protected virtual void RaiseZoneFeedbackChanged(ZoneFeedbackEventArgs args)
        {
            // Years ago we always copied the delegate into a local variable.
            // This was in case we got unsubscribed and it avoided exceptions being thrown
            // Since C# 6, the compiler does that work for us when we use ?.Invoke().
            // If you look through older code, you'll see both styles.

            ZoneFeedbackChanged.Invoke(this, args);
        }
        protected virtual void RaiseCrpcMessageReceived(CrpcMessageEventArgs args)
        {
            CrpcMessageReceived.Invoke(this, args);
        }


        public class ZoneFeedbackEventArgs : EventArgs
        {
            public uint ZoneNumber;
            public DmNaxSource SourceFb;
            public ushort VolumeFb;
            public bool AmpOnFb;
        }
        public class CrpcMessageEventArgs : EventArgs
        {
            public ushort Player;
            public string CRPCMessage;
        }

        // ############################### ENUMS

        
        public enum DmNaxSource
        {
            Unroute = 0,

            DigitalInput1 = 1,
            DigitalInput2 = 2,
            DigitalInput3 = 3,
            DigitalInput4 = 4,

            AnalogInput1 = 5,
            AnalogInput2 = 6,
            AnalogInput3 = 7,
            AnalogInput4 = 8,

            MediaPlayer1 = 9,
            MediaPlayer2 = 10,
            MediaPlayer3 = 11,
            MediaPlayer4 = 12,
            MediaPlayer5 = 13,
            MediaPlayer6 = 14,
            MediaPlayer7 = 15,
            MediaPlayer8 = 16,

            Aes67Stream = 17
        }


    }
}
