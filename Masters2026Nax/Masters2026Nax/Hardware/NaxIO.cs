// Using Crestron libraries, turn this off for your own sanity.
#nullable disable

using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.AudioDistribution;

namespace Masters2026Nax.Hardware
{
    // ============================================================================
    // A NOTE ABOUT THE DM-NAX-AUD-IO
    // ============================================================================
    // This class wraps the Crestron DM-NAX-AUD-IO and gives the rest of our program
    // a much simpler interface to the hardware. The rest of the program should not
    // need to understand how Crestron exposes routing, volume, mute, or AES67 streams
    // on this particular device.
    //
    // The AUD-IO routing is also a little different from the larger NAX amplifier.
    // There are only three valid routing choices:
    //
    //      0   Off
    //      1   Analog Input
    //      2   AES67 Stream
    //
    // Since these are fixed choices that actually describe what this device can do,
    // an enum makes sense here. This is different from the NAX amplifier where the
    // source number is really part of a larger source configuration.
    //
    // AES67 has two important details in this example. We currently select streams
    // using SessionName because setting MulticastAddress directly does not work as
    // expected with the NAX library.
    //
    // We also cannot assume the physical device is ready just because Register()
    // returned. Commands sent immediately after registration may simply go nowhere.
    // We wait until we have seen feedback from the device before doing our initial
    // setup. At that point we know we are actually talking to the hardware.
    //
    // Like the other wrappers, this class also collects Crestron's individual
    // feedback events and turns them into one event for the rest of our program.
    // The application should not need to know which Crestron event means volume
    // changed and which one means routing changed. That is this wrapper's problem.
    //
    // The goal is not to reproduce the entire Crestron API. We expose what this
    // program needs and hide the device-specific details here where they belong.
    // ============================================================================

    internal class NaxIO
    {
        public enum Sources
        {
            Off = 0,
            AnalogIn = 1,
            Aes67Stream = 2
        }

        private readonly DmNaxAudIoResidential myNaxIo;

        // We only want to perform our startup setup once. The device will continue
        // generating feedback for the rest of its life, so we need to remember when
        // we have already used that first feedback to initialize it.
        private bool deviceInitialized = false;

        public event EventHandler<DeviceFeedbackEventArgs> DeviceFeedbackChanged;

        public NaxIO(uint ipId, ControlSystem cs)
        {
            myNaxIo = new DmNaxAudIoResidential(ipId, cs);

            // Subscribe to feedback BEFORE registering the device. If the device
            // starts talking immediately after registration we want to be listening.
            myNaxIo.OnZoneChange += MyNaxIo_OnZoneChange;
            myNaxIo.Routing.RoutingPropertyChange += Routing_RoutingPropertyChange;
            myNaxIo.Nax.Rx[1].NaxTxRxPropertyChange += NaxIo_NaxTxRxPropertyChange;

            myNaxIo.Register();

            // Do not put our setup commands here.
            //
            // Register() tells us Crestron accepted the device into the program. It
            // does not tell us the physical AUD-IO is online and ready for commands.
            //
            // For example, this is a perfectly valid command but it may not work here
            // simply because we are sending it too early:
            //
            // myNaxIo.Nax.Rx[1].SessionName.StringValue =
            //     "MediaStream1900.10.7f.f4.6a.5d";
            //
            // We will do our setup after we have actually seen feedback from the
            // device.
        }

        /// <summary>
        /// Routes one of the available AUD-IO sources to the requested zone.
        /// Input 0 = Off
        /// Input 1 = Analog Input
        /// Input 2 = AES67 Stream
        /// </summary>
        public void RouteAudio(uint zone, ushort input)
        {
            // Contrary to what you might expect from looking at the API, the AUD-IO
            // only accepts 0-2 here.
            //
            // Notice we only check the upper limit of input. ushort can never contain
            // a negative number, so checking input >= 0 would accomplish absolutely
            // nothing. Let the data type do some of the work for us.
            // Also note that the zone = 1 on this device, there really is only one.
            // WE accept a none number in the argument as it is parity with the
            //other device class wrappers
            if (input < 3)
                myNaxIo.Routing.Zones[1].UShortValue = input;
        }

        /// <summary>
        /// Sets the output level of the AUD-IO.
        /// </summary>
        public void OutputLevel(uint zone, ushort level)
        {
            myNaxIo.Zones[zone].Volume.UShortValue = level;
        }

        /// <summary>
        /// Mutes or unmutes the output of the AUD-IO.
        /// </summary>
        public void OutputMute(uint zone, bool muted)
        {
            if (muted)
                myNaxIo.Zones[zone].MuteOn();
            else
                myNaxIo.Zones[zone].MuteOff();
        }

        /// <summary>
        /// Selects the AES67 stream by SessionName.
        /// </summary>
        public void SetStream(string streamName)
        {
            // We currently use SessionName instead of MulticastAddress because setting
            // MulticastAddress directly does not work correctly with the NAX library.
            //
            // This method assumes we have already seen feedback from the device and
            // therefore know the physical device is actually ready for commands.
            myNaxIo.Nax.Rx[1].SessionName.StringValue = streamName;
        }

        /// <summary>
        /// return the current AES64 Session that is being recieved
        /// </summary>
        /// <param name="output">output number 1 line out 2 bluetooth</param>
        /// <returns>session name string</returns>
        public string GetSessionFb(uint output)
        {
            var session = myNaxIo.Nax.Rx[output].SessionNameFeedback.StringValue;
            return session;
        }

        private void Routing_RoutingPropertyChange(
            object sender,
            Crestron.SimplSharpPro.DeviceSupport.GenericEventArgs args)
        {
            // The routing event tells us a source assignment changed. Rather than
            // making the rest of the program understand Crestron's routing event, we
            // grab the current state and send it through our own feedback event.
            SendOutputFeedback((uint)args.Index, "Routing");
        }

        private void MyNaxIo_OnZoneChange(object sender, ZoneEventArgs args)
        {
            // OnZoneChange can fire for several reasons. Right now we only care about
            // volume feedback, so ignore the other zone events.
            if (args.EventId == ZoneEventIds.VolumeFeedbackEventId)
                SendOutputFeedback(args.Zone.Number, "OutputChange");
        }

        // NaxTxRxPropertyChange is another generic Crestron event. Several different
        // things about the AES67 receiver can cause this same event to fire, so the
        // EventId tells us what actually changed.
        private void NaxIo_NaxTxRxPropertyChange(
            object sender,
            Crestron.SimplSharpPro.DeviceSupport.GenericEventArgs args)
        {
            // Any real feedback from the physical device tells us we have gotten far
            // enough through startup that it is actually communicating with us.
            DeviceReady();

            if (args.EventId == DmNaxXEventIds.StreamStatusFeedbackEventId)
            {
                // We know the AES67 receiver changed state. There is currently nothing
                // else the application needs to do with that information, but this is
                // where we would expose stream status later if we need it.
            }
        }

        // ########################### Event Helpers below

        private void DeviceReady()
        {
            // Feedback events will continue firing, but our startup setup should only
            // happen once.
            if (deviceInitialized)
                return;

            deviceInitialized = true;

            // Now that we have actually heard from the physical device it is safe to
            // send the setup commands that would have been too early in the constructor.
            myNaxIo.Nax.Rx[1].AutomaticInitiationEnable();

            // Set our default starting volume.
            myNaxIo.Zones[1].Volume.UShortValue = 35000;
        }

        private void SendOutputFeedback(uint output, string message = "")
        {
            // Receiving routing or zone feedback also proves that the physical device
            // is alive and talking to us, so this can perform our one-time startup
            // setup if another feedback event has not already done it.
            DeviceReady();

            // Just like the BTIO, collect the current state here and send one useful
            // event to the rest of the program. Whoever subscribes to this event does
            // not need to understand the Crestron events that got us here.
            DeviceFeedbackChanged?.Invoke(this,
                new DeviceFeedbackEventArgs(
                    output,
                    myNaxIo.Routing.Zones[output].UShortValue,
                    myNaxIo.Zones[output].Volume.UShortValue,
                    myNaxIo.Zones[output].MuteOnFeedback.BoolValue,
                    message));
        }

        /// <summary>
        /// Contains the current feedback state from the AUD-IO.
        /// </summary>
        public class DeviceFeedbackEventArgs : EventArgs
        {
            public uint Output { get; private set; }
            public ushort SourceFb { get; private set; }
            public ushort Level { get; private set; }
            public bool Muted { get; private set; }
            public string Information { get; private set; }

            // Instead of making the caller assemble this object one property at a
            // time, the constructor requires the complete feedback state. That makes
            // raising the event simple, but it also means we send all of the values
            // every time even if only one of them changed.
            //
            // For this small device that is a perfectly reasonable tradeoff.

            public DeviceFeedbackEventArgs(
                uint output,
                ushort source,
                ushort level,
                bool muted,
                string information)
            {
                Output = output;
                SourceFb = source;
                Level = level;
                Muted = muted;
                Information = information;
            }
        }
    }
}