using CwsDebug;
using Masters2026Nax.Configuration;
using Masters2026Nax.Hardware;
using MessageBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Masters2026Nax.Crpc
{
    public class CrpcFbConnection
    {
        private string hardwareId;
        private readonly Action<ushort, string> SendCrpcToStreamer;


        /// <summary>
        /// Initializes a new instance of the <c>CrpcFbConnection</c> class.
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
        /// <param name="sendCrpcToStreamer">Callback used to send CRPC commands to the streamer from the UIs.</param>
        public CrpcFbConnection(string hardwareId, ushort playerCount, Action<ushort, string> sendCrpcToStreamer)
        {
            this.hardwareId = hardwareId;
            SendCrpcToStreamer = sendCrpcToStreamer;

            for (ushort playerNumber = 1; playerNumber <= playerCount; playerNumber++)
            {
                var mediaPlayer = new MediaPlayerId(hardwareId, playerNumber);

                // If this delegate ever needed to be removed, we would need to use
                // a named method instead of a lambda.  Since each streaming player
                // will always be listening for commands, there is no need to remove
                // the subscription so we can use a lambda here.
                //
                // We cannot simply use playerNumber in the lambda because it is a loop
                // variable and will be incremented before the lambda is called.  This
                // is a common issue when using lambdas in C#.  The solution is to
                // create local variables inside the loop and use those in the lambda.
                // This ensures that each lambda has its own instance of the variable
                // and not a shared one.
                Broker.AddDelegate(
                    mediaPlayer.CommandKey,
                    m => SendCrpcToStreamer(mediaPlayer.PlayerNumber, m.Serial));

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
        public void SendFeedback(uint playerNumber, string crpcPayload)
        {
            var mediaPlayer = new MediaPlayerId(hardwareId, (ushort)playerNumber);

            // mediaPlayer.FeedbackKey resolves to "MediaPlayer[Nax1.Player#]CrpcFb"

            // SendMessage returns false when no UI has this player open. That is
            // normal, so there is nothing to log.
            Broker.SendMessage(
                mediaPlayer.FeedbackKey,
                new Message { Serial = crpcPayload });

            CwsDebugPage.LogInfo($"CRPC feedback from {mediaPlayer.FeedbackKey} sent to subscribed UIs: {crpcPayload}");
        }
    }
}
