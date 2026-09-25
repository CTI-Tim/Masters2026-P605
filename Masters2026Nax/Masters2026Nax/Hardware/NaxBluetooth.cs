// Using crestron libraries, turn this off for your own sanity.
#nullable disable

using Crestron.SimplSharpPro.AudioDistribution;
using CwsDebug;

namespace Masters2026Nax.Hardware
{
    public class NaxBluetooth
    {

        // ============================================================================
        // A NOTE ABOUT RESIDENTIAL MODE
        // ============================================================================
        // The BTIO can operate in Residential or Commercial mode and they are VERY
        // different programming models. This class is for RESIDENTIAL mode.
        // Residential mode treats the stereo audio paths as complete sources and zones
        // instead of exposing the individual Left and Right channels like Commercial.
        // This makes the device considerably easier to work with.
        // Also in most commercial uses the stereo pair is desirable anyways so leaving
        // it in "residential" for a commerical use is actually a very common use case.
        //
        // INPUT SOURCES
        //      0   No Source
        //      1   Analog Input
        //      2   Bluetooth Input
        //      3   AES67 Stream
        //
        // OUTPUT ZONES
        //      Zones[1]    LineOut
        //      Zones[2]    BTOut
        //
        // NAX RECEIVERS - Naming is strange, think of this as these stream recievers
        //                 Are dedicated to these audio output devices as a source
        //      Rx[1]       LineOut
        //      Rx[2]       BTOut
        //
        // NAX TRANSMITTERS
        //      Tx[1]       LineIn
        //      Tx[2]       BTIn
        //
        // This mapping can also be seen very clearly in the device admin webpage.
        // Always know what mode the hardware is in BEFORE writing code for it.
        // This is something to watch for with NAX hardware. If the C# class exposes
        // more channels than you expect, log into the device's admin web interface.
        // It will usually make what those channels actually represent much more obvious.
        // ============================================================================

        DmNaxBtio1GResidential myNaxBtIo;  // Do not use commercial.  It's broken badly and will crash the processor
        public event EventHandler<DeviceFeedbackEventArgs> DeviceFeedbackChanged;

        public NaxBluetooth(uint IpId, ControlSystem cs)
        {
            myNaxBtIo = new DmNaxBtio1GResidential(IpId, cs);
            myNaxBtIo.OnZoneChange += MyNaxBtIo_OnZoneChange;
            myNaxBtIo.Routing.RoutingPropertyChange += Routing_RoutingPropertyChange;
            myNaxBtIo.Nax.Rx[2].NaxTxRxPropertyChange += NaxBluetooth_NaxTxRxPropertyChange;

            myNaxBtIo.Register();

            // Set some defaults for bluetooth
            myNaxBtIo.Routing.Zones[2].UShortValue = 3; //Aes67stream
            myNaxBtIo.Routing.Zones[1].UShortValue = 3;
            myNaxBtIo.Nax.Rx[2].AutomaticInitiationEnable();
            //clear on startup
            //myNaxBtIo.Nax.Rx[1].SessionName.StringValue = ""; //LineOut
            //myNaxBtIo.Nax.Rx[2].SessionName.StringValue = ""; //Bluetooth Out
        }

        /// <summary>
        /// Routes a source to one of the two Residential output zones.
        /// Output 1 is LineOut and Output 2 is BTOut.
        /// Input 0 = No Source
        /// Input 1 = Analog Input
        /// Input 2 = Bluetooth Input
        /// Input 3 = AES67 Stream
        /// </summary>
        public void Route(uint Output, ushort Input)
        {
            // Notice that I am only checking the upper bound here and not the lower for Input.
            // Some of you already know why, some may not. It is because of the data type.
            // uint and ushort can NEVER contain a number below 0, it is simply impossible.
            // Checking if the value is >= 0 would always be true and accomplish nothing.
            // Output is different because even though 0 is possible, there is no Output 0.
            // Valid Outputs are 1-2, while Input 0 is valid because Crestron uses it to unroute.
            // Let the data type do its job and don't check for an impossible condition.

            if (Output > 0 && Output <= 2 && Input <= 3)
                myNaxBtIo.Routing.Zones[Output].UShortValue = Input;
        }
        public void OutputLevel(uint Output, ushort Level)
        {
            if (Output > 0 && Output <= 2)
                myNaxBtIo.Zones[Output].Volume.UShortValue = Level;
        }
        public void OutputMute(uint Output, bool Muted)
        {
            if (Output > 0 && Output <= 2)
            {
                if (Muted)
                    myNaxBtIo.Zones[Output].MuteOn();
                else
                    myNaxBtIo.Zones[Output].MuteOff();
            }
        }

        public void SetRxStream(uint stream, string session)
        {
            
            myNaxBtIo.Nax.Rx[stream].SessionName.StringValue = session;
        }

        /// <summary>
        /// return the current AES64 Session that is being recieved
        /// </summary>
        /// <param name="output">output number 1 line out 2 bluetooth</param>
        /// <returns>session name string</returns>
        public string GetSessionFb(uint output)
        {
            var session = myNaxBtIo.Nax.Rx[output].SessionNameFeedback.StringValue;
            return session;
        }

        /// <summary>
        /// Sets the Bluetooth direction of the BTIO.
        /// True puts Bluetooth into Receive mode, false puts it into Transmit mode.
        /// </summary>
        public void BlueToothRxMode(bool Rx)
        {
            // Bluetooth on the BTIO can work in two directions. It can either receive
            // Bluetooth audio from a device or transmit Bluetooth audio to a device.
            // I am using a bool here to keep this simple for the rest of the program.
            // The Crestron SDK uses the eBluetoothMode enum, so this method also hides
            // that little bit of SDK detail from whatever is using this class.

            if (Rx)
                myNaxBtIo.Bluetooth.Mode = eBluetoothMode.Receiving;
            else
                myNaxBtIo.Bluetooth.Mode = eBluetoothMode.Transmitting;
        }

        private void Routing_RoutingPropertyChange(object sender, Crestron.SimplSharpPro.DeviceSupport.GenericEventArgs args)
        {
            SendOutputFeedback((uint)args.Index, "Routing");
        }
        private void Bluetooth_BluetoothPropertyChange(object sender, Crestron.SimplSharpPro.DeviceSupport.GenericEventArgs args)
        {
            SendOutputFeedback(0, "Bluetooth");
        }
        private void MyNaxBtIo_OnZoneChange(object sender, ZoneEventArgs args)
        {
            if (args.EventId == ZoneEventIds.VolumeFeedbackEventId) // we only care about volume change feedback
            {
                CwsDebugPage.LogInfo($"BTIO zoneevent = {args.EventId}");
                SendOutputFeedback(args.Zone.Number, "OutputChange");
            }
        }

        // NaxTxRxPropertyChange is a generic event that can fire for several different
        // things that change on this NAX stream. Just getting the event does not tell
        // us WHAT changed, and this is where EventId comes in.
        // Crestron assigns an EventId to each type of feedback this event can report.
        // We check that ID to determine why the event fired and if we care about it.
        // In this case we only care about the Stream Status changing, everything else
        // that fires this event can simply be ignored.
        // Think of EventId as Crestron saying "the event fired, and this is why."

        private void NaxBluetooth_NaxTxRxPropertyChange(object sender, Crestron.SimplSharpPro.DeviceSupport.GenericEventArgs args)
        {
            if (args.EventId == DmNaxXEventIds.StreamStatusFeedbackEventId) // detect only Stream status has changed
            {
                // 2 is the bluetooth stream
                CwsDebugPage.LogInfo($"RxStream1 status = {myNaxBtIo.Nax.Rx[2].StreamStatusFeedback} at {myNaxBtIo.Nax.Rx[2].MulticastAdddressFeedback}");
            }
        }

        // ########################### Event Helpers below 
        private void SendOutputFeedback(uint output, string message = "")
        {
            var btmode = false;

            if (myNaxBtIo.Bluetooth.ModeFeedback == eBluetoothMode.Receiving)
                btmode = true;

            // LEARNING POINT: A comparison already returns a bool, so we do not need an
            // if statement to set this true or false. If ModeFeedback is Receiving the
            // comparison is true, anything else and it is false.
            //  so we could just let C# do the work with this line below instead
            //
            // var btmode = myNaxBtIo.Bluetooth.ModeFeedback == eBluetoothMode.Receiving;
            //
            // The IF has more clarity for a programmer coming from Simpl+

            DeviceFeedbackChanged?.Invoke(this,
                new DeviceFeedbackEventArgs(
                    output,
                    myNaxBtIo.Routing.Zones[output].UShortValue,
                    myNaxBtIo.Zones[output].Volume.UShortValue,
                    myNaxBtIo.Zones[output].MuteOnFeedback.BoolValue,
                    btmode,
                    message));
        }




        /// <summary>
        /// Contains the current feedback state from the BTIO.
        /// Output 1-8 identifies an audio output.
        /// Output 0 means this is device information such as Bluetooth or status.
        /// </summary>
        public class DeviceFeedbackEventArgs : EventArgs
        {
            public uint Output { get; private set; }
            public ushort Source { get; private set; }
            public ushort Level { get; private set; }
            public bool Muted { get; private set; }
            public bool BtRx { get; private set; }
            public string Information { get; private set; }

            // You can put methods in here!  instead of the traditional  { param = value }
            // we can instead leverage the default constructor to do work for us.
            // Now this has drawbacks.  we MUST send all the data every time.
            // Using the traditional assembling of the object allows us to only send what is needed

            public DeviceFeedbackEventArgs(uint output, ushort source, ushort level,
                bool muted, bool btrx, string information)
            {
                Output = output;
                Source = source;
                Level = level;
                Muted = muted;
                BtRx = btrx;
                Information = information;
            }
        }
    }
}
