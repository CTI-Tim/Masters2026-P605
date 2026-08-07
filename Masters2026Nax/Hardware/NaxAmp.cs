// Using crestron libraries, turn this off for your own sanity.
#nullable disable

using Crestron.SimplSharp;
using Crestron.SimplSharpPro.AudioDistribution;
using Crestron.SimplSharpPro.EthernetCommunication;
using Crestron.SimplSharpPro.Media;
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

        public NaxAmp(ushort AmpIpid, ControlSystem cs)
        {
            myNaxAmp = new DmNax8Zsa(AmpIpid, cs);
            myNaxAmp.OnZoneChange += MyNaxAmp_OnZoneChange;
            myNaxAmp.Players.TransportControlPropertyChange += Players_TransportControlPropertyChange;
            myNaxAmp.Routing.RoutingPropertyChange += Routing_RoutingPropertyChange;
            myNaxAmp.Register();

            // Set everything to auto initiate as we expect it
            // also defailt to no stream in startup
            foreach (var rx in myNaxAmp.Nax.Rx)
            {
                rx.AutomaticInitiationEnable();
                rx.MulticastAddress.StringValue = "0.0.0.0";

                // Due to feedback being broken we need to unroute all zones to match the fakeFeedback
                //InitializeZoneRouting();


            }


        }



        /// <summary>
        /// Select audio zone and source to route. Invalid selection does nothing
        /// 
        /// </summary>
        /// <param name="Zone"> Audio Output 1-8</param>
        /// <param name="Source">0 = Unroute, 1-4 = Digital Inputs, 5-8 = Analog Inputs, 9-16 = Media Players, 17 = AES67 Stream.</param>
        public void RouteAudio(uint Zone, ushort Source)
        {
            if (Zone >= 1 && Zone <= 8)
            {
                myNaxAmp.Routing.Zones[Zone].UShortValue = Source;
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

        private void InitializeZoneRouting()
        {
            for (uint zone = 1; zone <= 8; zone++)
            {
                RouteAudio(zone, 0);
            }
        }

        //  ############################# Event Handlers From Hardware

        private void MyNaxAmp_OnZoneChange(object sender, ZoneEventArgs args)
        {
            uint zone = args.Zone.Number;
            // we build our object to send to the event. Note some of the data we can not get
            // from the event so we instead read it directly.  Why move extra work to your 
            // event handler elsewhere when we can simply do it here.
            var e = new ZoneFeedbackEventArgs
            {
                ZoneNumber = zone,
                SourceFb = myNaxAmp.Routing.Zones[zone].UShortValue,
                VolumeFb = args.Zone.VolumeFeedback.UShortValue,
                AmpOnFb = args.Zone.MuteOnFeedback.BoolValue
            };
            RaiseZoneFeedbackChanged(e); //Fire the event
        }

        private void Routing_RoutingPropertyChange(object sender, Crestron.SimplSharpPro.DeviceSupport.GenericEventArgs args)
        {
            // The event tells us that something changed, but neglects to tell us what
            // actually changed. Fortunately, args.Index identifies the zone that generated
            // the event, so we can use that to query the current state.
            //
            // While it might seem like the event should have included this information,
            // the current device class and SIMPL module have been built around this
            // behavior for quite some time. Changing it now would introduce compatibility
            // issues, so this is effectively the contract of the API.
            var zone = (uint)args.Index;

            var e = new ZoneFeedbackEventArgs
            {
                ZoneNumber = zone,
                SourceFb = myNaxAmp.Routing.ZoneFeedbacks[zone].UShortValue
            };
            RaiseZoneFeedbackChanged(e);

        }

        private void Players_TransportControlPropertyChange(DmNaxXPlayers device, PlayerTransportControlEventArgs paramArgs)
        {
            var player = paramArgs.PlayerNumber;
            string crpc = myNaxAmp.Players.TransportControls[player].CrpcFeedback.ToString();

            var e = new CrpcMessageEventArgs
            {
                Player = player,
                CRPCMessage = crpc
            };
            RaiseCrpcMessageReceived(e);

        }


        // Our Event Handlers
        protected virtual void RaiseZoneFeedbackChanged(ZoneFeedbackEventArgs args)
        {
            // Years ago we always copied the delegate into a local variable.
            // This was in case we got unsubscribed and it avoided exceptions being thrown
            // Since C# 6, the compiler does that work for us when we use ?.Invoke().
            // If you look through older code, you'll see both styles.

            ZoneFeedbackChanged?.Invoke(this, args);
        }
        protected virtual void RaiseCrpcMessageReceived(CrpcMessageEventArgs args)
        {
            CrpcMessageReceived?.Invoke(this, args);
        }


        public class ZoneFeedbackEventArgs : EventArgs
        {
            public uint ZoneNumber;
            public ushort SourceFb;
            public ushort VolumeFb;
            public bool AmpOnFb;
        }
        public class CrpcMessageEventArgs : EventArgs
        {
            public uint Player;
            public string CRPCMessage;
        }



    }
}
