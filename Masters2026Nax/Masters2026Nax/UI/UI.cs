// Using Crestron libraries, turn this off for your own sanity.
#nullable disable

using Crestron.AutoUpdateApiCommon;  // Required by the generated contract.g.cs
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.AudioDistribution;
using Crestron.SimplSharpPro.UI;
using CwsDebug;
using Masters2026Nax.Configuration;
using Masters2026NaxPanel;
using MessageBroker;

namespace Masters2026Nax
{
    // -----------------------------------------------------------------------------
    // User Interface
    //
    // This class handles communication between the Construct HTML5 interface and
    // the rest of the application.
    //
    // The UI should know how to display information and how to report what the user
    // did. It should not know how the NAX hardware actually works. When the user
    // presses something we send a message through the Message Broker. When system
    // feedback changes we receive a message from the broker and update the panel.
    //
    // This separation is important. We could replace this XPanel with another user
    // interface without having to teach the new UI how to directly control a NAX
    // amplifier.
    // -----------------------------------------------------------------------------

    public class UI
    {
        private XpanelForHtml5 myXpanel;
        private Contract myContract;

        private ushort lastSourceTouched = 0;


        // #########################################################################
        // Constructor and UI Setup
        // #########################################################################

        /// <summary>
        /// Creates the HTML5 XPanel, connects the generated Construct contract,
        /// registers our UI events, and connects the UI to the Message Broker.
        ///
        /// The NAX source configuration must already exist before this class is
        /// created because we use it here to build the source button labels.
        /// </summary>
        public UI(uint ipId, ControlSystem cs)
        {
            myXpanel = new XpanelForHtml5(ipId, cs);
            myXpanel.OnlineStatusChange += PanelOnlineOfflineEventHandler;

            myContract = new Contract();
            myContract.AddDevice(myXpanel);

            myXpanel.Register();


            // ---------------------------------------------------------------------
            // Main Page Setup
            // ---------------------------------------------------------------------

            myContract.Main.RoomLabel_Indirect("Masters 2026 C# for NAX");
            myContract.Main.Output1Label_Indirect("NaxAmp Out 1");
            myContract.Main.Output2Label_Indirect("NaxAmp Out 2");
            myContract.Main.Output3Label_Indirect("BlueTooth Out");


            // ---------------------------------------------------------------------
            // Contract Event Handlers
            //
            // Construct generates a separate object for each source-selection list,
            // so each one needs its own event handler. The handlers below eventually
            // call the same SourceSelect() method once we know which zone generated
            // the event.
            // ---------------------------------------------------------------------

            myContract.Main.Output1SourceSel.Button_PressEvent += Source1Select_Button_PressEvent;
            myContract.Main.Output2SourceSel.Button_PressEvent += Source2Select_Button_PressEvent;
            myContract.Main.Output3SourceSel.Button_PressEvent += Source3Select_Button_PressEvent;

            // Volume controls
            myContract.Main.Output1Volume_LowerTouchEvent += Main_Output1Volume_LowerTouchEvent;
            myContract.Main.Output2Volume_LowerTouchEvent += Main_Output2Volume_LowerTouchEvent;
            myContract.Main.Output3Volume_LowerTouchEvent += Main_Output3Volume_LowerTouchEvent;

            // Media Player CRPC
            myContract.MediaControl.MediaPlayer.MediaPlayer_CRPCEvent += MediaPlayer_MediaPlayer_CRPCEvent;


            // ---------------------------------------------------------------------
            // Message Broker Connections
            // ---------------------------------------------------------------------

            Broker.AddDelegate("Zone1SourceFb", Zone1SetSourceFb);
            Broker.AddDelegate("Zone2SourceFb", Zone2SetSourceFb);
            Broker.AddDelegate("Zone3SourceFb", Zone3SetSourceFb);
            Broker.AddDelegate("Zone1VolumeFb", Zone1SetVolumeFb);
            Broker.AddDelegate("Zone2VolumeFb", Zone2SetVolumeFb);
            Broker.AddDelegate("Zone3VolumeFb", Zone3SetVolumeFb);

            Broker.AddDelegate("MediaPlayerCrpcFb", MediaPlayerObjectPlayerFb);


            // ---------------------------------------------------------------------
            // Source Button Labels
            //
            // The UI does not contain its own copy of the source names. It asks the
            // NAX source configuration for them. This keeps the source configuration
            // as our single source of truth.
            // ---------------------------------------------------------------------

            for (ushort i = 0;
                i < NaxSourceConfiguration.GetSourceCount(1);
                i++)
            {
                myContract.Main.Output1SourceSel.Button_Text(
                    i,
                    NaxSourceConfiguration.GetSourceName(1, i));
            }

            for (ushort i = 0;
                i < NaxSourceConfiguration.GetSourceCount(2);
                i++)
            {
                myContract.Main.Output2SourceSel.Button_Text(
                    i,
                    NaxSourceConfiguration.GetSourceName(2, i));
            }

            for (ushort i = 0;
                i < NaxSourceConfiguration.GetSourceCount(3);
                i++)
            {
                myContract.Main.Output3SourceSel.Button_Text(
                    i,
                    NaxSourceConfiguration.GetSourceName(3, i));
            }
        }

        // -------------------------------------------------------------------------
        // UI Event Handlers
        //
        // These methods tell the application what the user did. They do not directly
        // control any NAX hardware. That job belongs on the other side of the Message
        // Broker.
        // -------------------------------------------------------------------------

        // Crestron Contracts are tightly coupled to the generated Contract objects.
        // Each SourceSelect object exposes its own Button_PressEvent, so we still
        // need a separate event handler for each zone.
        //
        // Could we try to hide all of this behind another abstraction? Sure.
        // It would also make a simple button press harder for a new programmer to
        // follow, so these three small methods are staying right here.

        private void Source1Select_Button_PressEvent(
            object sender,
            IndexedButtonEventArgs e)
        {
            SourceSelect(1, e.ButtonIndex);
        }


        private void Source2Select_Button_PressEvent(
            object sender,
            IndexedButtonEventArgs e)
        {
            SourceSelect(2, e.ButtonIndex);
        }


        private void Source3Select_Button_PressEvent(
            object sender,
            IndexedButtonEventArgs e)
        {
            SourceSelect(3, e.ButtonIndex);
        }


        private void SourceSelect(uint zone, uint source)
        {
            Broker.SendMessage(
                $"Zone{zone}Source",
                new Message { Analog = (ushort)source });

            // The source configuration already knows whether this button represents
            // a Media Player. There is no reason for the UI to try to figure that
            // out again from source numbers.
            CheckMediaPlayer(
                (ushort)zone,
                (ushort)source);
        }

        // Volume
        private void Main_Output1Volume_LowerTouchEvent(object sender, UIEventArgs e)
        {
            VolumeSend(1, e.SigArgs.Sig.UShortValue);
        }
        private void Main_Output2Volume_LowerTouchEvent(object sender, UIEventArgs e)
        {
            VolumeSend(2, e.SigArgs.Sig.UShortValue);
        }
        private void Main_Output3Volume_LowerTouchEvent(object sender, UIEventArgs e)
        {
            VolumeSend(3, e.SigArgs.Sig.UShortValue);
        }
        private void VolumeSend(uint zone, ushort level)
        {
            Broker.SendMessage(
                $"Zone{zone}Volume",
                new Message { Analog = level });
        }

        // #########################################################################
        // Panel Online / Offline
        // #########################################################################

        private void PanelOnlineOfflineEventHandler(
            GenericBase currentDevice,
            OnlineOfflineEventArgs args)
        {
            Broker.SendMessage(
                "XpanelOnlineOffline",
                new Message { Digital = args.DeviceOnLine });

            if (args.DeviceOnLine)
            {
                // VisibilityJoin works like a page flip. We pulse it by setting the
                // join high and then low again.
                myContract.Main.VisibilityJoin(true);
                myContract.Main.VisibilityJoin(false);
            }
        }


        // #########################################################################
        // Message Broker Feedback Handlers
        //
        // These methods receive feedback from the rest of the application and update
        // the touchpanel. They do not decide what the system state should be.
        //
        // The hardware reports its state, that information travels through the
        // Message Broker, and the UI displays it. Keeping the UI this simple means
        // it does not need to understand how any of the NAX hardware actually works.
        // #########################################################################

        // Yes, we're brute force clearing every button first. There are only a
        // handful of buttons, so this is simple, easy to understand, and
        // guarantees that only one button remains selected.
        //
        // Could we write more code to make this infinitely expandable? Sure.
        // Do we need to? No.
        //
        // These three methods also look very similar. Normally that would make
        // me want to combine them, but the generated Contract gives us three
        // different button-list objects. Sometimes tightly coupled generated
        // code is unavoidable and the simple duplicate methods are easier to understand.
        // The problem is that these objects come from the Construct generated Contract.
        // Even though Output1SourceSel, Output2SourceSel, and Output3SourceSel may expose
        // exactly the same methods, Construct may generate them as different C# types.
        // Do not add interfaces, dynamic, reflection, or other clever code just to eliminate
        // a few repeated lines. Refactoring code that is based on generated code
        // like these contracts rarely pays off and only causes lost time.

        private void Zone1SetSourceFb(Message m)
        {
            for (ushort i = 0;
                i < NaxSourceConfiguration.GetSourceCount(1);
                i++)
            {
                myContract.Main.Output1SourceSel.Button_Selected(i, false);
            }

            myContract.Main.Output1SourceSel.Button_Selected(
                m.Analog,
                true);
        }
        private void Zone2SetSourceFb(Message m)
        {
            for (ushort i = 0;
                i < NaxSourceConfiguration.GetSourceCount(2);
                i++)
            {
                myContract.Main.Output2SourceSel.Button_Selected(i, false);
            }

            myContract.Main.Output2SourceSel.Button_Selected(
                m.Analog,
                true);
        }
        private void Zone3SetSourceFb(Message m)
        {
            for (ushort i = 0;
                i < NaxSourceConfiguration.GetSourceCount(3);
                i++)
            {
                myContract.Main.Output3SourceSel.Button_Selected(i, false);
            }

            myContract.Main.Output3SourceSel.Button_Selected(
                m.Analog,
                true);
        }
        // #########################################################################
        // Volumes  
        // #########################################################################
        private void Zone1SetVolumeFb(Message m)
        {
            myContract.Main.Output1Volume_LowerTouchfb(m.Analog); 
        }
        private void Zone2SetVolumeFb(Message m)
        {
            myContract.Main.Output2Volume_LowerTouchfb(m.Analog);
        }
        private void Zone3SetVolumeFb(Message m)
        {
            myContract.Main.Output3Volume_LowerTouchfb(m.Analog);
        }

        // #########################################################################
        // Media Player and CRPC
        // #########################################################################

        private void CheckMediaPlayer(
            ushort zone,
            ushort buttonIndex)
        {
            if (NaxSourceConfiguration.IsMediaPlayer(
                zone,
                buttonIndex))
            {
                // The selected source represents a NAX Media Player, so show the
                // Media Player controls and make the Return button available.
                myContract.MediaControl.VisibilityJoin(true);
                myContract.MediaControl.VisibilityJoin(false);

                ConnectCrpc();
            }
        }


        private void ConnectCrpc()
        {
            // The Construct Media Player object needs this connection information
            // before it can exchange CRPC messages with the NAX Media Player.
            myContract.MediaControl.MediaPlayer.MediaPlayer_Message_FB(
                " {\"ver\":\"4.0.0\",\"tag\":\"94\",\"src\":\"1\"}");

            myContract.MediaControl.MediaPlayer.MediaPlayer_Offline(false);
            myContract.MediaControl.MediaPlayer.MediaPlayer_Player_Name("TEST");
        }


        private void MediaPlayer_MediaPlayer_CRPCEvent(
            object sender,
            UIEventArgs e)
        {
            // CRPC from the touchpanel is simply passed into the Message Broker.
            // The UI does not need to know which hardware object eventually receives it.
            Broker.SendMessage(
                "UiMediaPlayerCrpcMessage",
                new Message
                {
                    Serial = e.SigArgs.Sig.StringValue
                });
        }


        private void MediaPlayerObjectPlayerFb(Message m)
        {
            // CRPC feedback takes the opposite trip. Hardware sends it through the
            // broker and we pass it directly back into the Construct Media Player.
            myContract.MediaControl.MediaPlayer.MediaPlayer_CRPC_FB(
                m.Serial);
        }
    }
}