#nullable disable

using CwsDebug;
using Masters2026Nax.Configuration;
using MessageBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Masters2026Nax.Crpc
{
    public class CrpcUiConnection
    {
        /// <summary>
        /// Tracks the number of times this class has been instantiated.
        /// </summary>
        /// <remarks>Incremented when an instance is created.</remarks>
        private static ushort instanceCount = 0;

        /// <summary>
        /// This is the instance ID of the instantiated class.  IDs start at 1.
        /// This is used to identify the UI instance in CRPC messages sent to the UI's
        /// Media Player.
        /// </summary>
        private readonly ushort instanceId;

        /// <summary>
        /// The media player this panel is connected to, or null if none.  Used to
        /// send CRPC to the right player and to unsubscribe from the message broker.
        /// </summary>
        private MediaPlayerId connectedMediaPlayer = null;

        /// <summary>
        /// This is the delegate that sends the source change message to the UI's
        /// Media Player.
        /// </summary>
        private readonly Action<string> SendMessageFbToUi;

        /// <summary>
        /// This is the delegate that sends Crpc Fb messages from the hardware players
        /// to the UI's Media Player.
        /// </summary>
        private readonly Action<string> SendCrpcFbToUi;

        /// <summary>
        /// Forwards a CRPC feedback message to the UI.  This gives us a consistent 
        /// object to pass to the broker for subscribing and unsubscribing.
        /// </summary>
        /// <param name="m">Message containing the serial Crcp message to forward.</param>
        private void ForwardCrpcFbToUi(Message m)
        {
            CwsDebugPage.LogInfo($"UI instance {instanceId} is forwarding CRPC Fb from {connectedMediaPlayer} to the Construct Media Player: {m.Serial}");

            SendCrpcFbToUi(m.Serial);
        }

        // Give the Media Players time to exchange the source change messages.
        private const int sourceChangeSettleTimeInMs = 200;

        /// <summary>
        /// Initializes a new instance of the <c>CrpcUiConnection</c> class.
        /// Create one instance per panel (if it has an IP ID and Media Player in the UI,
        /// it should have its own instance of this class).
        /// </summary>
        /// <remarks></remarks>
        /// <param name="sendMessageFbToUi">Callback used to send message feedback to the UI to change sources.</param>
        /// <param name="sendCrpcFbToUi">Callback used to send CRPC feedback to the UI from the hardware-based players.</param>
        public CrpcUiConnection(Action<string> sendMessageFbToUi, Action<string> sendCrpcFbToUi)
        {
            instanceId = ++instanceCount;

            SendMessageFbToUi = sendMessageFbToUi;
            SendCrpcFbToUi = sendCrpcFbToUi;
        }

        /// <summary>
        /// Changes the Media Player source and subscribes to its CRPC feedback.
        /// </summary>
        /// <remarks>Builds and sends a CRPC source-change message and logs it, 
        /// unsubscribes from the previous media player when the ID differs, 
        /// subscribes to the new player's CRPC feedback via the broker, and 
        /// updates connectedMediaPlayer. If mediaPlayer equals the current one,
        /// no unsubscribe/subscribe is performed.</remarks>
        /// <param name="mediaPlayer">The media player to connect to.</param>
        public void Connect(MediaPlayerId mediaPlayer)
        {
            var sourceChangeMessage = CRPC.GetSourceChangeMessage(
                instanceId,
                NaxSourceConfiguration.GetMediaPlayerSrc(mediaPlayer));
            CwsDebugPage.LogNotice($"CRPC Source Change Message: {sourceChangeMessage}");


            // The Construct Media Player object needs this connection information
            // before it can exchange CRPC messages with the NAX Media Player.
            SendMessageFbToUi(sourceChangeMessage);

            // MediaPlayerId is a record, so == compares HardwareId and PlayerNumber.
            if (mediaPlayer == connectedMediaPlayer)
            {
                // The UI is already connected to the Media Player. No need to
                // unsubscribe and resubscribe.
                return;
            }

            UnsubscribeFromLastMediaPlayer();

            // Subscribe to the Hardware Media Player's CRPC feedback.  The feedback
            // is sent to the UI through the broker and then forwarded to the UI's
            // Media Player object.
            //
            // We cannot use a lambda here because we need to be able to unsubscribe
            // from the broker later.  The delegate must be a named method.
            Broker.AddDelegate(
                mediaPlayer.FeedbackKey,
                ForwardCrpcFbToUi);
            CwsDebugPage.LogNotice($"UI instance {instanceId} connected to {mediaPlayer.FeedbackKey}");

            // Store the connected media player so we can unsubscribe from it later.
            connectedMediaPlayer = mediaPlayer;
        }

        /// <summary>
        /// Sends the source disconnect message to the panel's media player,
        /// waits briefly for those messages to be exchanged, then unsubscribes
        /// from the previous media player.
        /// </summary>
        /// <remarks>Exceptions are caught and logged to avoid propagating disconnect 
        /// failures.</remarks>
        public async Task DisconnectAsync()
        {
            try
            {
                if (connectedMediaPlayer == null)
                {
                    CwsDebugPage.LogInfo($"UI instance {instanceId} is not connected to a media player.  No disconnect action taken.");
                    return;
                }

                var player = connectedMediaPlayer;

                SendMessageFbToUi(CRPC.GetSourceChangeMessage(instanceId, 0));

                // Send the source change message to the Media Player while
                // the subpage is still open so it can process the message.
                // This keeps the player in the correct state when it is
                // reopened later.  I found that if the subpage is closed
                // by a direct page flip and this change message isn't sent,
                // the player may not connect properly to the next player,
                // particularly if it is a source change.
                await Task.Delay(sourceChangeSettleTimeInMs);

                // There's a delay, so check again to see if the connected media
                // player is still the same one we were connected to before unsubscribing.
                if (connectedMediaPlayer == player)
                {
                    UnsubscribeFromLastMediaPlayer();
                }

            }
            catch (Exception e)
            {
                CwsDebugPage.LogError($"UI instance {instanceId} failed to disconnect from the media player: {e}");
            }
        }

        /// <summary>
        /// Send the Crpc payload from the panel to the associated player.
        /// </summary>
        /// <param name="crpcPayload">The Crpc message from the panel.</param>
        public void SendCommand(string crpcPayload)
        {
            if (connectedMediaPlayer == null)
            {
                CwsDebugPage.LogInfo($"UI instance {instanceId} received a CRPC message from the Construct Media Player, but no media player is currently connected. Message ignored: {crpcPayload}");
                return;
            }

            CwsDebugPage.LogInfo($"UI instance {instanceId} is sending CRPC message from Construct Media Player to {connectedMediaPlayer}: {crpcPayload}");

            // CRPC from the touchpanel is simply passed into the Message Broker.
            var delivered = Broker.SendMessage(
                connectedMediaPlayer.CommandKey,
                new Message { Serial = crpcPayload });

            // Nothing listening means the configuration points at hardware or a
            // player that HardwareSetup never connected. Say so instead of failing
            // silently.
            if (!delivered)
            {
                CwsDebugPage.LogError($"UI instance {instanceId}: no hardware is listening for {connectedMediaPlayer}. Check that its HardwareId and PlayerNumber match the hardware set up in HardwareSetup.");
            }
        }

        /// <summary>
        /// Unsubscribes the broker from the previously connected hardware media player 
        /// and clears the stored last media player identifier.
        /// </summary>
        /// <remarks>If no media player is connected, no action is taken. Logs a
        /// disconnect notice and removes the delegate from the player's FeedbackKey.</remarks>
        private void UnsubscribeFromLastMediaPlayer()
        {
            if (connectedMediaPlayer == null)
            {
                return;
            }

            CwsDebugPage.LogNotice($"UI instance {instanceId} is unsubscribing from Media Player {connectedMediaPlayer.FeedbackKey}");
            Broker.RemoveDelegate(
                connectedMediaPlayer.FeedbackKey,
                ForwardCrpcFbToUi);

            connectedMediaPlayer = null;
        }
    }
}
