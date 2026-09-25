using Crestron.SimplSharp;
using CwsDebug;
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
        private NaxIO myIo;

        public HardwareSetup(ControlSystem cs)
        {
            // ---------------------------------------------------------------------
            // NAX Amplifier
            // ---------------------------------------------------------------------
            myNaxamp = new NaxAmp(0x10, cs);

            myNaxamp.ZoneFeedbackChanged += MyNaxamp_ZoneFeedbackChanged;
            myNaxamp.CrpcMessageReceived += MyNaxamp_CrpcMessageReceived;

            Broker.AddDelegate("Zone1Source", Zone1SourceSelect);
            Broker.AddDelegate("Zone2Source", Zone2SourceSelect);
            Broker.AddDelegate("UiMediaPlayerCrpcMessage", CrpcIncoming);

            // ---------------------------------------------------------------------
            // NAX BTIO
            // ---------------------------------------------------------------------
            myBtIo = new NaxBluetooth(0x11, cs);

            myBtIo.DeviceFeedbackChanged += MyBtIo_DeviceFeedbackChanged;

            // ---------------------------------------------------------------------
            // NAX AUD-IO
            // ---------------------------------------------------------------------
            myIo = new NaxIO(0x13, cs);

            myIo.DeviceFeedbackChanged += MyIo_DeviceFeedbackChanged;

            Broker.AddDelegate("Zone3Source", Zone3SourceSelect);
        }


        // #########################################################################
        // NAX Amplifier Commands
        // #########################################################################

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


        // #########################################################################
        // NAX Amplifier Feedback
        // #########################################################################

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

            CwsDebugPage.LogNotice(
                $"Source Select Zone {e.ZoneNumber} feedback was {e.SourceFb} was set to {buttonIndex}");
        }


        // #########################################################################
        // NAX AUD-IO Commands
        // #########################################################################

        public void Zone3SourceSelect(Message m)
        {
            var source = NaxSourceConfiguration.GetSourceNumber(3, m.Analog);

            myIo.RouteAudio(1, source);

            if (NaxSourceConfiguration.IsStreamSource(3, m.Analog))
            {
                myIo.SetStream(
                    NaxSourceConfiguration.GetSessionName(3, m.Analog));
            }
        }


        // #########################################################################
        // NAX AUD-IO Feedback
        // #########################################################################

        private void MyIo_DeviceFeedbackChanged(
            object? sender,
            NaxIO.DeviceFeedbackEventArgs e)
        {
            // The AUD-IO tells us its routing source number, but that does not always
            // tell us which button should be selected. Several AES67 sources can all
            // use the same routing number and are identified by Session Name instead.
            //
            // The source configuration translates the hardware feedback back into the
            // same button index that was originally used to select the source.
            var buttonIndex =
                NaxSourceConfiguration.GetButtonIndexFromFeedback(
                    3,
                    e.SourceFb,
                    myIo.GetSessionFb(1));

            // HardwareSetup does not care who is listening to this feedback. It simply
            // reports the current Zone 3 source index to the broker. In this program
            // the UI is listening and uses that index to update its button feedback.
            Broker.SendMessage(
                "Zone3SourceFb",
                new Message { Analog = buttonIndex });
        }


        // #########################################################################
        // NAX BTIO Feedback
        // #########################################################################

        private void MyBtIo_DeviceFeedbackChanged(
            object? sender,
            NaxBluetooth.DeviceFeedbackEventArgs e)
        {
            CwsDebugPage.LogNotice(
                $"BTIO sent out={e.Output} src={e.Source} mute={e.Muted} " +
                $"vol={e.Level} event={e.Information} bt={e.BtRx}");
        }


        // #########################################################################
        // Media Player CRPC
        // #########################################################################

        private void MyNaxamp_CrpcMessageReceived(
            object? sender,
            NaxAmp.CrpcMessageEventArgs e)
        {
            // CRPC feedback came from the NAX amplifier. Put it on the broker so the
            // UI can consume it without needing to know anything about NaxAmp.
            Broker.SendMessage(
                "MediaPlayerCrpcFb",
                new Message { Serial = e.CRPCMessage });
        }

        public void CrpcIncoming(Message m)
        {
            // CRPC coming the other direction arrived from the UI through the broker.
            // HardwareSetup knows which hardware wrapper should receive it.
            myNaxamp.SendCrpc(1, m.Serial);
        }
    }
}