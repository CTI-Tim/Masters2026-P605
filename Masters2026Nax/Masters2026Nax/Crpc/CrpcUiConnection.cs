// Nullable checking stays on here (see ControlSystem.cs). This class uses no
// Crestron types, and null has a real meaning: "no media player connected".
using CwsDebug;
using Masters2026Nax.Configuration;
using MessageBroker;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Masters2026Nax.Crpc
{
    /// <summary>
    /// Manages CRPC communication between a UI media player instance and hardware 
    /// media players, including source selection, command forwarding, feedback 
    /// routing, and subscription lifecycle.
    /// </summary>
    /// <remarks>Assigns a unique instance identifier per UI connection, sends 
    /// source-change messages to keep the UI media player synchronized, subscribes
    /// and unsubscribes broker delegates when the connected media player changes,
    /// and supports asynchronous disconnect with a short settle delay to reduce
    /// connectivity issues with the CH5 media player.</remarks>
    /// <example>
    /// Wire a connection to a panel's Construct Media Player by supplying two
    /// lambdas that push strings into the Media Player's feedback inputs (this
    /// is how the UI class does it):
    /// <code>
    /// myCrpcMediaPlayerConnection = new CrpcUiConnection(
    ///     message => myContract.MediaControl.MediaPlayer.MediaPlayer_Message_FB(message),
    ///     crpc => myContract.MediaControl.MediaPlayer.MediaPlayer_CRPC_FB(crpc));
    /// </code>
    /// </example>
    public class CrpcUiConnection
    {
        /// <summary>
        /// Sends a source-change message to the panel's Media Player.
        /// Implement this by writing the string to the Media Player's
        /// Message_FB input in the Construct contract.
        /// </summary>
        /// <param name="message">Serialized source-change message (JSON).  Send it as-is.</param>
        public delegate void SendMessageFbToUi(string message);

        /// <summary>
        /// Sends CRPC feedback from a hardware player to the panel's Media Player.
        /// Implement this by writing the string to the Media Player's CRPC_FB
        /// input in the Construct contract.
        /// </summary>
        /// <param name="crpcPayload">Serialized CRPC feedback message.  Send it as-is.</param>
        public delegate void SendCrpcFbToUi(string crpcPayload);

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
        private MediaPlayerId? connectedMediaPlayer = null;

        /// <summary>
        /// This is the delegate that sends the source change message to the UI's
        /// Media Player.
        /// </summary>
        private readonly SendMessageFbToUi sendMessageFbToUi;

        /// <summary>
        /// This is the delegate that sends Crpc Fb messages from the hardware players
        /// to the UI's Media Player.
        /// </summary>
        private readonly SendCrpcFbToUi sendCrpcFbToUi;

        /// <summary>
        /// Give the Media Players time to exchange the source change messages.
        /// 200ms is typical.
        /// </summary>
        private const int sourceChangeSettleTimeInMs = 200;

        /// <summary>
        /// Initializes a new instance of the <c>CrpcUiConnection</c> class.
        /// Create one instance per panel (if it has an IP ID and Media Player in the UI,
        /// it should have its own instance of this class).
        /// </summary>
        /// <remarks></remarks>
        /// <param name="sendMessageFbToUi">Callback that sends a source-change message to the panel's Media Player.  See <see cref="SendMessageFbToUi"/> and the class example.</param>
        /// <param name="sendCrpcFbToUi">Callback that sends CRPC feedback from the hardware players to the panel's Media Player.  See <see cref="SendCrpcFbToUi"/> and the class example.</param>
        public CrpcUiConnection(SendMessageFbToUi sendMessageFbToUi, SendCrpcFbToUi sendCrpcFbToUi)
        {
            instanceId = ++instanceCount;

            this.sendMessageFbToUi = sendMessageFbToUi;
            this.sendCrpcFbToUi = sendCrpcFbToUi;
        }

        /// <summary>
        /// Forwards a CRPC feedback message to the UI.  This gives us a consistent 
        /// object to pass to the broker for subscribing and unsubscribing.
        /// </summary>
        /// <param name="m">Message containing the serial Crcp message to forward.</param>
        private void ForwardCrpcFbToUi(Message m)
        {
            CwsDebugPage.LogInfo($"UI instance {instanceId} is forwarding CRPC Fb from {connectedMediaPlayer} to the Construct Media Player: {m.Serial}");

            sendCrpcFbToUi(m.Serial);
        }

        /// <summary>
        /// Changes the Media Player source and subscribes to its CRPC feedback.
        /// </summary>
        /// <remarks>Builds and sends a CRPC source-change message and logs it, 
        /// unsubscribes from the previous media player when the ID differs, 
        /// subscribes to the new player's CRPC feedback via the broker, and 
        /// updates connectedMediaPlayer. If mediaPlayer equals the current one,
        /// no unsubscribe/subscribe is performed.</remarks>
        /// <param name="mediaPlayer">The media player to connect to.  Null indicates no media player and sends a source of 0.</param>
        public void Connect(MediaPlayerId? mediaPlayer)
        {
            // Get the source for the media player.  If mediaPlayer is null, src
            // is 0.
            ushort src = mediaPlayer == null ? (ushort)0 : NaxSourceConfiguration.GetMediaPlayerSrc(mediaPlayer);

            var sourceChangeMessage = GetSourceChangeMessage(
                instanceId,
                src);
            CwsDebugPage.LogNotice($"CRPC Source Change Message: {sourceChangeMessage}");


            // The Construct Media Player object needs this connection information
            // before it can exchange CRPC messages with the NAX Media Player.
            sendMessageFbToUi(sourceChangeMessage);

            // MediaPlayerId is a record, so == compares HardwareId and PlayerNumber.
            if (mediaPlayer == connectedMediaPlayer)
            {
                // The UI is already connected to the Media Player. No need to
                // unsubscribe and resubscribe.
                return;
            }

            UnsubscribeFromLastMediaPlayer();

            // Store the connected media player so we can unsubscribe from it later.
            connectedMediaPlayer = mediaPlayer;

            // If mediaPlayer is null, the UI is not connecting to a media player.  Move
            // on without subscribing.
            if (mediaPlayer == null)
            {
                CwsDebugPage.LogInfo($"UI instance {instanceId} is not connecting to a media player.  No subscription action taken.");
                return;
            }

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

                sendMessageFbToUi(GetSourceChangeMessage(instanceId, 0));

                // Send the source change message to the Media Player while
                // the subpage is still open so it can process the message.
                // This keeps the player in the correct state when it is
                // reopened later.  I found that if the subpage is closed
                // by a direct page flip and this change message isn't processed,
                // the player may not connect properly to the next player,
                // particularly if it is a source change.
                await Task.Delay(sourceChangeSettleTimeInMs);

                // There's a delay, so check again to see if the connected media
                // player is still the same one we were connected to before unsubscribing.
                //
                // Known limitation: this check can't tell "still connected" from
                // "reconnected to the same player during the delay."  If Connect()
                // is called with the same player within the delay, Connect() skips
                // resubscribing (same player) and we still unsubscribe here, which
                // leaves the Media Player open with no feedback.
                //
                // The current UI can't trigger this.  Source buttons are only on
                // the Main page, and the only way back to Main is the Return
                // button, which awaits this method before the page flip.  A layout
                // that shows source buttons alongside the Media Player could hit it
                // with two source presses within the delay.
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
        /// Send the CRPC payload from the panel to the associated player.
        /// </summary>
        /// <param name="crpcPayload">The CRPC message from the panel.</param>
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

        /// <summary>
        /// Creates a JSON-serialized source change message for the specified UI instance and source.  This message must be sent to the UI's MediaPlayer_Message_FB input every time the source changes.
        /// </summary>
        /// <param name="uiInstance">The identifier of the UI instance.  Must be greater than 0.  If 0, an empty string is returned and an error is logged.</param>
        /// <param name="source">The identifier of the source.</param>
        /// <returns>A JSON string representing SourceChangeMessage for the specified UI instance and source.</returns>
        private string GetSourceChangeMessage(ushort uiInstance, ushort source)
        {
            if (uiInstance == 0)
            {
                CwsDebugPage.LogError($"GetSourceChangeMessage({uiInstance},{source}).  UI instance must be greater than 0.  Message not generated.");
                return string.Empty;
            }

            var properties = new SourceChangeMessage(uiInstance, source);
            return JsonSerializer.Serialize(properties);

            // Returns a message like: {"ver":"4.0.0","tag":"02","src":"3"}
        }

        /// <summary>
        /// Properties for a source-change message sent to the UI's media player instance.
        /// </summary>
        /// <remarks>These properties are serialized to JSON.  The names are case-sensitive, so we set the JsonPropertyName attributes accordingly.</remarks>
        private sealed class SourceChangeMessage
        {
            /// <summary>
            /// Payload schema version in semantic versioning (SemVer) format.
            /// </summary>
            /// <remarks>Defaults to "4.0.0". Treated as a static identifier for the message or schema
            /// version; changing it may break something.</remarks>
            [JsonPropertyName("ver")]
            public string Ver { get; } = "4.0.0";

            /// <summary>
            /// Identifier for the UI player requesting the source change.
            /// </summary>
            /// <remarks>Two-digit, zero-padded numeric string (for example, "01", "10"); value must
            /// be greater than "00".</remarks>
            [JsonPropertyName("tag")]
            public string Tag { get; }

            /// <summary>
            /// Identifier for the selected source.
            /// </summary>
            /// <remarks>Must be changed whenever the user switches sources.</remarks>
            [JsonPropertyName("src")]
            public string Src { get; }

            /// <summary>
            /// Initializes a new instance of SourceChangeMessage with the specified instance, source, and default ver value.
            /// </summary>
            /// <param name="uiInstance">The instance number used to identify which UI's Media Player requested the change.  This is formatted as a two-digit decimal string.</param>
            /// <param name="source">The source identifier.  This is converted to a decimal string.</param>
            public SourceChangeMessage(ushort uiInstance, ushort source)
            {
                this.Tag = uiInstance.ToString("D2");
                this.Src = source.ToString();
            }
        }
    }
}
