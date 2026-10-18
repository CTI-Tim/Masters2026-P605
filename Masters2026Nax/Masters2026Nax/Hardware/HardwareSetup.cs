using Crestron.SimplSharp;
using CwsDebug;
//using Masters2026;
using Masters2026Nax.Configuration;
using MessageBroker;
using System;

namespace Masters2026Nax.Hardware
{
    // -----------------------------------------------------------------------------
    // HardwareSetup Class
    //
    // Think of this class as the wiring closet for the application.
    //
    // Its job is to create the hardware wrapper objects, initialize them, connect
    // them to the Message Broker, and hook up any events needed for communication.
    // Once everything is connected, it mostly gets out of the way.
    //
    // What BELONGS here:
    //   - Creating hardware wrapper objects.
    //   - Initializing hardware.
    //   - Connecting hardware to the Message Broker.
    //   - Subscribing to hardware events.
    //   - Registering broker topics and command handlers.
    //   - Translating between application messages and hardware wrapper calls.
    //
    // What DOES NOT belong here:
    //   - Business logic.
    //   - User interface logic.
    //   - Decision making about what the user interface should do.
    //
    // The goal is to keep this class boring. Seriously. If it starts growing into hundreds of
    // lines of decision making, it has become the junk drawer of the application.
    //
    // -----------------------------------------------------------------------------

    /// <summary>
    /// Creates the hardware wrappers for the application and connects them to the
    /// Message Broker. Commands coming from the application are translated into
    /// hardware calls here, while hardware feedback is translated back into broker
    /// messages for whoever is interested in it.
    /// </summary>
    public class HardwareSetup
    {
        
        private NaxAmp myNaxamp;
        private NaxBluetooth myBtIo;
        //private NaxIO myIo;

        public HardwareSetup(ControlSystem cs)
        {

            // ---------------------------------------------------------------------
            // NAX Amplifier
            // ---------------------------------------------------------------------
            myNaxamp = new NaxAmp(0x10, "Nax1", cs);
            myNaxamp.ZoneFeedbackChanged += MyNaxamp_ZoneFeedbackChanged;
            myNaxamp.CrpcMessageReceived += NaxAmp_CrpcMessageReceived;
            ConnectMediaPlayersToBroker(myNaxamp);

            Broker.AddDelegate("Zone1Source", Zone1SourceSelect);
            Broker.AddDelegate("Zone2Source", Zone2SourceSelect);

            // -------------------------------------------------------------------
            // NAX BTIO
            // -------------------------------------------------------------------
            myBtIo = new NaxBluetooth(0x11, cs);
            myBtIo.DeviceFeedbackChanged += MyBtIo_DeviceFeedbackChanged;

            Broker.AddDelegate("Zone3Source", Zone3SourceSelect);
            // --------------------------------------------------------------------
            // NAX AUD-IO
            // --------------------------------------------------------------------
            //myIo = new NaxIO(0x13, cs);

            //myIo.DeviceFeedbackChanged += MyIo_DeviceFeedbackChanged;

            // ---------------------------------------------------------------------
            // VOLUME EVENTS
            // ---------------------------------------------------------------------
            Broker.AddDelegate("Zone1Volume", Zone1VolumeSet);
            Broker.AddDelegate("Zone2Volume", Zone2VolumeSet);
            Broker.AddDelegate("Zone3Volume", Zone3VolumeSet);

        }

        // ---------------------------------------------------------------------
        // VOLUME Helper Methods
        // ---------------------------------------------------------------------
        public void Zone1VolumeSet(Message m)
        {
        myNaxamp.Volume(1, m.Analog);
        }
        public void Zone2VolumeSet(Message m)
        {
            myNaxamp.Volume(2, m.Analog);
        }
        public void Zone3VolumeSet(Message m)
        {
            myBtIo.Volume(2, m.Analog);
        }





        // -----------------------------------------------------------------
        // NAX Amplifier Commands
        // -----------------------------------------------------------------

        public void Zone1Volume(ushort volume)
        {
            myNaxamp.Volume(1, volume);
        }
        public void Zone2Volume(ushort volume)
        {
            myNaxamp.Volume(2, volume);
        }
        public void BtVolume(ushort volume)
        {
            myBtIo.Volume(2, volume);
        }
        public void Zone1SourceSelect(Message m)
        {
            var source = NaxSourceConfiguration.GetSourceNumber(1, m.Analog);

            myNaxamp.RouteAudio(1, source);

            if (NaxSourceConfiguration.IsStreamSource(1, m.Analog))
            {
                myNaxamp.SetStream(
                    1,
                    NaxSourceConfiguration.GetSessionName(1, m.Analog));
            }
        }

        public void Zone2SourceSelect(Message m)
        {
            var source = NaxSourceConfiguration.GetSourceNumber(2, m.Analog);

            myNaxamp.RouteAudio(2, source);

            if (NaxSourceConfiguration.IsStreamSource(2, m.Analog))
            {
                myNaxamp.SetStream(
                    2,
                    NaxSourceConfiguration.GetSessionName(2, m.Analog));
            }
        }


        // ------------------------------------------------------------------
        // NAX Amplifier Feedback
        // ------------------------------------------------------------------

        // This is intentionally clear rather than clever. Every time the amplifier
        // reports source feedback we translate that hardware feedback back into the
        // button index used by our source configuration and send it to the broker.
        //
        // The source number alone is not always enough to identify the source because
        // AES67 sources may share the same routing number. GetButtonIndexFromFeedback()
        // also checks the Session Name when the source configuration requires it.
        private void MyNaxamp_ZoneFeedbackChanged(
            object? sender,
            NaxAmp.ZoneFeedbackEventArgs e)
        {
            var buttonIndex =
                NaxSourceConfiguration.GetButtonIndexFromFeedback(
                    e.ZoneNumber,
                    e.SourceFb,
                    myNaxamp.GetSessionFb(e.ZoneNumber));

            Broker.SendMessage(
                $"Zone{e.ZoneNumber}SourceFb",
                new Message { Analog = buttonIndex });

            Broker.SendMessage($"Zone{e.ZoneNumber}VolumeFb",
                new Message { Analog = e.VolumeFb });

            CwsDebugPage.LogNotice(
                $"Source Select Zone {e.ZoneNumber} feedback was {e.SourceFb} was set to {buttonIndex} Volume = {e.VolumeFb}");
        }


        // --------------------------------------------------------------
        // NAX AUD-BTIO Commands
        // --------------------------------------------------------------

        public void Zone3SourceSelect(Message m)
        {
            var source = NaxSourceConfiguration.GetSourceNumber(3, m.Analog);

            myBtIo.RouteAudio(2, source);  // On the BTIO BTOut is out 2 if in transmitter mode

            if (NaxSourceConfiguration.IsStreamSource(3, m.Analog))
            {
                myBtIo.SetStream(
                    NaxSourceConfiguration.GetSessionName(3, m.Analog));
            }
        }

        // #########################################################################
        // NAX BTIO Feedback
        // #########################################################################

        private void MyBtIo_DeviceFeedbackChanged(
            object? sender,
            NaxBluetooth.DeviceFeedbackEventArgs e)
        {
            var buttonIndex =
                NaxSourceConfiguration.GetButtonIndexFromFeedback(
                    3,
                    e.SourceFb,
                    myBtIo.GetSessionFb(2));
            CwsDebugPage.LogInfo($"BTIOFB = {e.SourceFb} and {myBtIo.GetSessionFb(2)} volume = {e.VolumeFb} output={e.Output}");
            // HardwareSetup does not care who is listening to this feedback. It simply
            // reports the current Zone 3 source index to the broker. In this program
            // the UI is listening and uses that index to update its button feedback.
            Broker.SendMessage("Zone3SourceFb",
                    new Message { Analog = buttonIndex });
            Broker.SendMessage($"Zone3VolumeFb",
                    new Message { Analog = e.VolumeFb });
            
        }


        // #########################################################################
        // Media Player CRPC
        // #########################################################################

        /// <summary>
        /// Subscribes every media player on a NAX amp to its CRPC command key, so
        /// CRPC sent by any UI reaches the right player. Call this once per amp.
        /// </summary>
        /// <param name="amp">The NAX amp whose players should listen for CRPC.</param>
        private void ConnectMediaPlayersToBroker(NaxAmp amp)
        {
            foreach (var playerNumber in amp.PlayerNumbers)
            {
                var mediaPlayer = new MediaPlayerId(amp.HardwareId, (ushort)playerNumber);

                // The broker message only carries the CRPC text, so each player gets
                // its own small handler that remembers which player it belongs to.
                // foreach gives every pass its own mediaPlayer variable, so each
                // lambda sends to its own player.
                Broker.AddDelegate(
                    mediaPlayer.CommandKey,
                    m => SendCrpcToPlayer(amp, mediaPlayer, m));

                CwsDebugPage.LogInfo($"{mediaPlayer} is listening for CRPC on {mediaPlayer.CommandKey}");
            }
        }

        /// <summary>
        /// Forwards a CRPC message received from a UI to one media player.
        /// </summary>
        /// <param name="amp">The NAX amp hosting the player.</param>
        /// <param name="mediaPlayer">The player the message is for.</param>
        /// <param name="m">The CRPC message. m.Serial holds the CRPC text.</param>
        private void SendCrpcToPlayer(NaxAmp amp, MediaPlayerId mediaPlayer, Message m)
        {
            CwsDebugPage.LogNotice($"HardwareSetup is sending CRPC from UI to {mediaPlayer}: {m.Serial}");

            amp.SendCrpc(mediaPlayer.PlayerNumber, m.Serial);
        }

        /// <summary>
        /// CRPC feedback came from a NAX amp. Put it on the broker so any UI
        /// connected to that player can consume it without needing to know
        /// anything about NaxAmp.
        /// </summary>
        /// <param name="sender">The NaxAmp that received the feedback.</param>
        /// <param name="e">The player number and CRPC text.</param>
        private void NaxAmp_CrpcMessageReceived(
            object? sender,
            NaxAmp.CrpcMessageEventArgs e)
        {
            // Use the sender instead of myNaxamp, so this one handler works for
            // every NAX amp in the system.
            if (sender is not NaxAmp amp)
                return;

            var mediaPlayer = new MediaPlayerId(amp.HardwareId, (ushort)e.Player);

            // SendMessage returns false when no UI has this player open. That is
            // normal, so there is nothing to log.
            Broker.SendMessage(
                mediaPlayer.FeedbackKey,
                new Message { Serial = e.CRPCMessage });
        }
    }
}