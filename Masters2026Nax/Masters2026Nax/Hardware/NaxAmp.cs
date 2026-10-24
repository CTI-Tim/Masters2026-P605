// Using Crestron libraries, turn this off for your own sanity.
#nullable disable

using Crestron.SimplSharp;
using Crestron.SimplSharpPro.AudioDistribution;
using Crestron.SimplSharpPro.Media;
using CwsDebug;
using System;
using System.Collections.Generic;

namespace Masters2026Nax.Hardware
{
    // -----------------------------------------------------------------------------
    // Why don't your classes do lots of validation?
    //
    // This class assumes the programmer is using the API as documented. The goal
    // is to keep the code simple, readable, and easy to understand rather than
    // filling every method with defensive checks.
    //
    // If I was coding a public library used by thousands of developers, especially
    // one with incomplete or poor documentation, additional validation would make
    // more sense because you cannot control how the API is used.
    //
    // In a well-documented library, however, the documentation is part of the
    // contract. The programmer is expected to read it and use the API correctly.
    //
    // Yes I just said the programmer is expected to read documentation.
    //
    // Adding dozens of checks to guard against incorrect usage often adds more code,
    // more complexity, and more maintenance than it adds value. As with everything
    // in software engineering, this is a design decision, not a rule.
    // Once again only add complexity when it solves a real problem,
    // not simply because you can.
    // -----------------------------------------------------------------------------

    /// <summary>
    /// This is a wrapper class to simplify and unify operation of Crestron hardware.
    /// Yes this class is going to be tightly coupled but it's not designed to really
    /// be infinitely reusable. It can be reused system to system and you could create
    /// an array of NaxAmp objects in this program, but it's specific to this hardware
    /// and specific to my use decisions.
    ///
    /// It's OK to make code that is tightly coupled. Not everything has to be abstracted.
    /// </summary>
    public class NaxAmp
    {
        private DmNax8Zsa myNaxAmp;

        // The NAX Amp Media Players can sometimes send the exact same CRPC message
        // more than once. Keep the last message for each player so we can ignore a
        // duplicate that arrives immediately after it. This is tracked per player
        // so identical messages from different players are not mistaken for duplicates.
        private readonly Dictionary<uint, string> lastCrpcMessages =
            new Dictionary<uint, string>();

        /// <summary>
        /// This identifies the hardware hosting the media players and is used
        /// to subscribe to brokered messages.  This should be unique for each
        /// piece of hardware in the system that hosts media players.  The UI uses 
        /// this to subscribe to the correct player instance.
        /// 
        /// We could use a string in the HardwareSetup class to identify the hardware, 
        /// but that would be a "magic string".  Instead we place the identifier in 
        /// the hardware wrapper so that the UI and HardwareSetup can both use it without
        /// hardcoding the string in multiple places.
        /// </summary>
        public string HardwareId { get; private set; }

        public event EventHandler<ZoneFeedbackEventArgs> ZoneFeedbackChanged;
        public event EventHandler<CrpcMessageEventArgs> CrpcMessageReceived;


        // #########################################################################
        // Constructor and Hardware Setup
        // #########################################################################

        public NaxAmp(ushort ampIpid, string hardwareId, ControlSystem cs)
        {
            HardwareId = hardwareId;
            myNaxAmp = new DmNax8Zsa(ampIpid, cs);

            // Hook the hardware events before registration so we are ready to receive
            // feedback as soon as the device starts communicating with the program.
            myNaxAmp.Nax.NaxPropertyChange += Nax_NaxPropertyChange;
            myNaxAmp.BaseEvent += MyNaxAmp_BaseEvent;
            myNaxAmp.OnlineStatusChange += MyNaxAmp_OnlineStatusChange;
            myNaxAmp.OnZoneChange += MyNaxAmp_OnZoneChange;
            myNaxAmp.Players.TransportControlPropertyChange += Players_TransportControlPropertyChange;
            myNaxAmp.Routing.RoutingPropertyChange += Routing_RoutingPropertyChange;

            myNaxAmp.Register();

            // IMPORTANT:
            // Register() only tells us the device has been registered with the Crestron
            // program. It does NOT mean the physical device is ready to accept commands.
            // Do not send startup configuration until we have seen feedback from it.

            // We expect the NAX receivers to automatically initiate their streams.
            foreach (var rx in myNaxAmp.Nax.Rx)
            {
                rx.AutomaticInitiationEnable();
            }
        }


        // #########################################################################
        // Public Hardware Methods
        // #########################################################################

        public void Volume(uint zone, ushort volume)
        {
            myNaxAmp.Zones[zone].Volume.UShortValue = volume;
        }

        public void Mute(uint zone, bool muteOn)
        {
            if(muteOn)
                myNaxAmp.Zones[zone].MuteOn();
            else
                myNaxAmp.Zones[zone].MuteOff();
        }


        /// <summary>
        /// Returns the current AES67 Session Name being received by a zone.
        /// </summary>
        /// <param name="output">NAX output zone 1-8.</param>
        /// <returns>The current AES67 Session Name.</returns>
        public string GetSessionFb(uint output)
        {
            return myNaxAmp.Nax.Rx[output].SessionNameFeedback.StringValue;
        }

        /// <summary>
        /// Routes an audio source to a NAX output zone.
        /// </summary>
        /// <param name="zone">Audio output zone 1-8.</param>
        /// <param name="source">
        /// 0 = Unroute, 1-4 = Digital Inputs, 5-8 = Analog Inputs,
        /// 9-16 = Media Players, 17 = AES67 Stream.
        /// </param>
        public void RouteAudio(uint zone, ushort source)
        {
            if (zone >= 1 && zone <= 8)
            {
                myNaxAmp.Routing.Zones[zone].UShortValue = source;
            }
        }

        /// <summary>
        /// Sets the AES67 stream for the specified NAX zone.
        /// AES67 is source 17 on the NAX amplifier.
        /// NAX currently uses the AES67 Session Name to select the stream
        /// rather than allowing us to configure the multicast address directly.
        /// </summary>
        /// <param name="zone">NAX zone to configure.</param>
        /// <param name="session">AES67 Session Name to connect to.</param>
        public void SetStream(uint zone, string session)
        {
            myNaxAmp.Nax.Rx[zone].SessionName.StringValue = session;
        }

        /// <summary>
        /// Clears the AES67 stream from the specified NAX zone.
        /// </summary>
        /// <param name="zone">NAX zone to clear.</param>
        public void ClearStream(uint zone)
        {
            myNaxAmp.Nax.Rx[zone].SessionName.StringValue = "";
        }

        /// <summary>
        /// Sends CRPC data to the selected Media Player in the NAX amplifier.
        /// </summary>
        /// <param name="player">Media Player number.</param>
        /// <param name="crpcPayload">
        /// CRPC string to send to the player, usually from the touchpanel Media
        /// Player object or a CRPC router.
        /// </param>
        public void SendCrpc(uint player, string crpcPayload)
        {
            myNaxAmp.Players.TransportControls[player].Crpc.StringValue = crpcPayload;
        }


        // #########################################################################
        // Helper Methods
        // #########################################################################

        private void InitializeZoneRouting()
        {
            for (uint zone = 1; zone <= 8; zone++)
            {
                RouteAudio(zone, 0);
            }
        }


        // #########################################################################
        // Hardware Event Handlers
        // #########################################################################

        private void MyNaxAmp_OnlineStatusChange(
            Crestron.SimplSharpPro.GenericBase currentDevice,
            Crestron.SimplSharpPro.OnlineOfflineEventArgs args)
        {
            CwsDebugPage.LogInfo($"Online Event {args.DeviceOnLine}");
        }

        private void MyNaxAmp_BaseEvent(
            Crestron.SimplSharpPro.GenericBase device,
            Crestron.SimplSharpPro.BaseEventArgs args)
        {
            CwsDebugPage.LogInfo($"Base Event {args.EventId}");
        }

        private void Nax_NaxPropertyChange(
            object sender,
            Crestron.SimplSharpPro.DeviceSupport.GenericEventArgs args)
        {
            CwsDebugPage.LogInfo($"NAX Property Event {args.EventId}");
        }

        private void MyNaxAmp_OnZoneChange(object sender, ZoneEventArgs args)
        {
            var zone = args.Zone.Number;

            // The event tells us which zone changed and gives us some of its feedback.
            // For anything not included in the event we simply read the current value
            // directly from the device. There is no reason to make whoever subscribes
            // to our event go back and ask the hardware for the rest of the information.
            var e = new ZoneFeedbackEventArgs
            {
                ZoneNumber = (ushort)zone,
                SourceFb = myNaxAmp.Routing.Zones[zone].UShortValue,
                VolumeFb = args.Zone.VolumeFeedback.UShortValue,
                AmpOnFb = args.Zone.MuteOnFeedback.BoolValue
            };

            RaiseZoneFeedbackChanged(e);
        }

        private void Routing_RoutingPropertyChange(
            object sender,
            Crestron.SimplSharpPro.DeviceSupport.GenericEventArgs args)
        {
            // The event tells us that something changed, but neglects to tell us what
            // actually changed. Fortunately args.Index identifies the zone that generated
            // the event, so we can use that to query the current routing feedback.
            //
            // While it might seem like the event should have included this information,
            // the current device class and SIMPL module have been built around this
            // behavior for quite some time. Changing it now would introduce compatibility
            // issues, so this is effectively the contract of the API.
            var zone = (uint)args.Index;

            var e = new ZoneFeedbackEventArgs
            {
                ZoneNumber = (ushort)zone,
                SourceFb = myNaxAmp.Routing.ZoneFeedbacks[zone].UShortValue
            };

            RaiseZoneFeedbackChanged(e);
        }

        private void Players_TransportControlPropertyChange(
            DmNaxXPlayers device,
            PlayerTransportControlEventArgs args)
        {
            var player = args.PlayerNumber;
            var crpc =
                myNaxAmp.Players.TransportControls[player].CrpcFeedback.StringValue;

            // The NAX Media Players can occasionally send the exact same CRPC message
            // twice. There is no reason to send duplicate data through the rest of our
            // program, so ignore it when it is identical to the previous message.
            //
            // This only catches messages repeated back to back from the same player. If
            // another message is received in between, the original message is allowed
            // through again.
            if (!lastCrpcMessages.TryGetValue(player, out var lastCrpc) || crpc != lastCrpc)
            {
                lastCrpcMessages[player] = crpc;

                var e = new CrpcMessageEventArgs
                {
                    Player = player,
                    CRPCMessage = crpc
                };

                RaiseCrpcMessageReceived(e);
            }
        }


        // #########################################################################
        // Events We Send to the Application
        // #########################################################################

        protected virtual void RaiseZoneFeedbackChanged(
            ZoneFeedbackEventArgs args)
        {
            // Years ago we always copied the delegate into a local variable. This was
            // in case we got unsubscribed and it avoided exceptions being thrown.
            // Since C# 6 the compiler does that work for us when we use ?.Invoke().
            // If you look through older code, you'll see both styles.
            ZoneFeedbackChanged?.Invoke(this, args);
        }

        protected virtual void RaiseCrpcMessageReceived(
            CrpcMessageEventArgs args)
        {
            CrpcMessageReceived?.Invoke(this, args);
        }


        // #########################################################################
        // Event Data
        // #########################################################################

        public class ZoneFeedbackEventArgs : EventArgs
        {
            public ushort ZoneNumber;
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