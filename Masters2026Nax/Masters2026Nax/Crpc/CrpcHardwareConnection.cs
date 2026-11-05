using CwsDebug;
using Masters2026Nax.Configuration;
using MessageBroker;

namespace Masters2026Nax.Crpc
{
    /// <summary>
    /// Manages CRPC communication between a hardware media player instance and UI
    /// media players, including command forwarding, feedback routing, and
    /// subscription lifecycle.
    /// </summary>
    /// <remarks>Create one instance per media server. At startup, it registers a 
    /// command subscription for each hardware player and keeps those subscriptions 
    /// active for the lifetime of the process so commands from the UI are always
    /// ready to be received.</remarks>
    /// <example>
    /// Wire a connection to a device-specific class by supplying a lambda that
    /// forwards each command to the device (this is how HardwareSetup does it
    /// for the NAX amplifier):
    /// <code>
    /// myCrpcHardwareConnection = new CrpcHardwareConnection(
    ///     myNaxamp.HardwareId,
    ///     8,   // number of streaming players on the device
    ///     (playerNumber, crpcPayload) =>
    ///     {
    ///         myNaxamp.SendCrpc(playerNumber, crpcPayload);
    ///     });
    ///
    /// // Feedback from the device goes back out through the same connection.
    /// myNaxamp.CrpcMessageReceived += (sender, e) =>
    ///     myCrpcHardwareConnection.SendFeedback((ushort)e.Player, e.CRPCMessage);
    /// </code>
    /// </example>
    public class CrpcHardwareConnection
    {
        /// <summary>
        /// Delivers a CRPC command from a UI to one player on the hardware.
        /// Implement this by forwarding to the device-specific class (for
        /// example, NaxAmp.SendCrpc).
        /// </summary>
        /// <param name="playerNumber">1-based player number on this hardware (1 through the playerCount given to the constructor).</param>
        /// <param name="crpcPayload">Serialized CRPC message, forwarded as received.  Do not modify it.</param>
        public delegate void SendCrpcToHardware(ushort playerNumber, string crpcPayload);

        /// <summary>
        /// This is the unique hardware identifier for the device hosting the
        /// media players.  It is used as part of the key when establishing
        /// subscriptions through the Broker.
        /// </summary>
        private readonly string hardwareId;

        /// <summary>
        /// This tracks every hardwareId given to this class.  We refer to it
        /// during instantiation to ensure each instance has a unique identifier.
        /// If a duplicate is detected, an exception is thrown to prevent the
        /// program from starting up in a configuration that would cause CRPC
        /// messages to be routed incorrectly.
        /// 
        /// This ignores case in order to reject near-duplicates like "Nax1" 
        /// and "nax1" which would be confusing, even though the broker would
        /// treat them as distinct keys.
        /// </summary>
        private static readonly HashSet<string> usedHardwareIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// This is the delegate that sends CRPC messages from the UI to the
        /// individual hardware players.
        /// </summary>
        private readonly SendCrpcToHardware sendCrpcToHardware;


        /// <summary>
        /// Initializes a new instance of the <c>CrpcHardwareConnection</c> class.
        /// Create one instance per media server.
        /// 
        /// Subscribes every media player on the hardware to incoming CRPC
        /// commands from the UIs' Media Players.  Each hardware player gets
        /// its own key, which allows the broker to route the message to the
        /// correct player.
        /// 
        /// These subscriptions are created at startup and remain as long as the
        /// program is running, ensuring that the players are always ready to 
        /// receive CRPC commands from the UI.
        /// </summary>
        /// <param name="hardwareId">Hardware identifier used to create the NAX amplifier instance.  Must be unique.</param>
        /// <param name="playerCount">The number of streaming players on the hardware.</param>
        /// <param name="sendCrpcToHardware">Callback that forwards a UI's CRPC command to the given hardware player.  See <see cref="SendCrpcToHardware"/> for the argument meanings and the class example for a sample lambda.</param>
        public CrpcHardwareConnection(string hardwareId, ushort playerCount, SendCrpcToHardware sendCrpcToHardware)
        {
            // HashSet.Add returns false if the value is already there.
            if (!usedHardwareIds.Add(hardwareId))
            {
                throw new ArgumentException($"A CrpcHardwareConnection with hardwareId '{hardwareId}' already exists or a case-insensitive match (near-duplicate) was detected.");
            }

            this.hardwareId = hardwareId;
            this.sendCrpcToHardware = sendCrpcToHardware;

            for (ushort playerNumber = 1; playerNumber <= playerCount; playerNumber++)
            {
                var mediaPlayer = new MediaPlayerId(hardwareId, playerNumber);

                // We cannot use playerNumber in the lambda below because it will be
                // captured as a single variable for all instances in the loop, not
                // its current value.  When the lambda is ultimately called, long after
                // the loop completes, playerNumber has the value of playerCount + 1,
                // which is not what we want.  This is a common issue when using
                // lambdas within FOR loops.  FOREACH loops do not have this problem
                // because the loop variable is scoped to each iteration.
                //
                // The solution is to use local variables inside the loop and use those
                // in the lambda.  This ensures that each lambda has its own, locally
                // scoped instance of the variable and not a shared one.
                // mediaPlayer.PlayerNumber is a property of the local variable, and
                // takes the place of the loop variable in the lambda.  This is a
                // common pattern in C# when using lambdas in FOR loops.
                //
                // If this delegate ever needed to be removed, we would need to use
                // a named method instead of a lambda.  Since each streaming player
                // will always be listening for commands, there is no need to remove
                // the subscription so we can use a lambda here.
                Broker.AddDelegate(
                    mediaPlayer.CommandKey,
                    m => this.sendCrpcToHardware(mediaPlayer.PlayerNumber, m.Serial));

                CwsDebugPage.LogNotice($"{mediaPlayer} is listening for CRPC on {mediaPlayer.CommandKey}");
            }
        }

        /// <summary>
        /// Sends the CRPC feedback payload to any UIs that are using it.  Put it on 
        /// the broker so any UI connected to that player can consume it without 
        /// needing to know anything about NaxAmp.
        /// </summary>
        /// <param name="playerNumber">Identifies the media player whose feedback channel generated the message.</param>
        /// <param name="crpcPayload">Contains the serialized CRPC payload to send.</param>
        public void SendFeedback(ushort playerNumber, string crpcPayload)
        {
            var mediaPlayer = new MediaPlayerId(hardwareId, playerNumber);

            // mediaPlayer.FeedbackKey resolves to "MediaPlayer[Nax1.Player#]CrpcFb"

            // SendMessage returns false when no UI has this player open. That is
            // normal, so there is nothing to log.
            Broker.SendMessage(
                mediaPlayer.FeedbackKey,
                new Message { Serial = crpcPayload });
        }
    }
}
