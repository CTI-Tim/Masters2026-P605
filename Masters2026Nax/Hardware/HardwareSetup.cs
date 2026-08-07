using Crestron.SimplSharp;
using Masters2026Nax.Configuration;
using MessageBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    //   • Creating hardware wrapper objects.
    //   • Initializing hardware.
    //   • Connecting hardware to the Message Broker.
    //   • Subscribing to hardware events.
    //   • Registering broker topics and command handlers.
    //   • General startup and shutdown tasks.
    //
    // What DOES NOT belong here:
    //   • Hardware control logic.
    //   • Business logic.
    //   • User interface logic.
    //   • Decision making.
    //   • Anything that starts with "if the user presses..."
    //
    // If you find yourself writing code here that changes volume, routes audio,
    // plays music, or otherwise operates the hardware, you're probably putting the
    // code in the wrong place. That functionality belongs inside the appropriate
    // hardware wrapper class.
    //
    // The goal is to keep this class boring. Seriously. This file should read more
    // like a checklist than a novel. If it starts growing into hundreds of lines of
    // decision making, it has become the junk drawer of the application, and it's
    // time to move that code somewhere else.
    //
    // A good HardwareSetup class answers one question:
    //
    //     "What hardware does this application have, and how is it connected?"
    //
    // It should NOT answer:
    //
    //     "What should the hardware do?"
    // -----------------------------------------------------------------------------

    /// <summary>
    /// Creates and initializes the hardware wrapper classes for the application,
    /// connects them to the Message Broker, and manages their lifecycle. This class
    /// serves as the central hardware configuration point and intentionally contains
    /// setup code only, leaving hardware control and application logic to the
    /// appropriate wrapper classes or any automation classes.
    /// </summary>
    public class HardwareSetup
    {
        private NaxAmp myNaxamp;
        public HardwareSetup(ControlSystem cs)
        {
            // ##################################### NAx Amp
            myNaxamp = new NaxAmp(0x10, cs);
            myNaxamp.ZoneFeedbackChanged += MyNaxamp_ZoneFeedbackChanged;
            myNaxamp.CrpcMessageReceived += MyNaxamp_CrpcMessageReceived;
            
            Broker.AddDelegate("Zone1Source", Zone1SourceSelect);

            Broker.AddDelegate("UiMediaPlayerCrpcMessage", CrpcIncoming);


        }



        private void MyNaxamp_CrpcMessageReceived(object? sender, NaxAmp.CrpcMessageEventArgs e)
        {

            Broker.SendMessage("MediaPlayerCrpcFb", new Message { Serial = e.CRPCMessage });
        }

        private void MyNaxamp_ZoneFeedbackChanged(object? sender, NaxAmp.ZoneFeedbackEventArgs e)
        {
            var buttonindex = NaxSourceConfiguration.GetButtonIndex(1, e.SourceFb);

            Broker.SendMessage("Zone1SourceFb", new Message { Analog = buttonindex });

        }


        public void Zone1SourceSelect(Message m)
        {
            CrestronConsole.PrintLine($"got {m.Analog}");

            var source = NaxSourceConfiguration.GetSourceNumber(1, m.Analog);
            // 0 = Unroute, 1-4 = Digital Inputs, 5-8 = Analog Inputs, 9-16 = Media Players, 17 = AES67 Stream
            // If a Media Player is selected, send the flip to media player message

            CrestronConsole.PrintLine($"Source sent is  {source}");

            myNaxamp.RouteAudio( 1, source);
        }

        public void CrpcIncoming(Message m)
        {
            CrestronConsole.PrintLine($"FromTp ={m.Serial}");
            var crpc = m.Serial;
            myNaxamp.SendCrpc(1, crpc);
        }


    }
}
