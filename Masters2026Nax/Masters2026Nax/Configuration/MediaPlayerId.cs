using System;

namespace Masters2026Nax.Configuration
{
    // ##############################################################################
    // [Hardware] Media Player ID
    //
    // Every hardware media player in the system gets one of these.  It answers two
    // questions: which piece of hardware hosts the player, and which player on
    // that hardware it is.
    //
    // It also owns the Message Broker keys used to talk to that player.  The UI and
    // HardwareSetup both ask this object for the key instead of each building the
    // string on their own, so we're not dealing with magic strings.
    //
    // This is a record, so two MediaPlayerId objects holding the same
    // HardwareId and PlayerNumber are equal.  That lets the UI simply ask "is this
    // the same player I'm already connected to?" with ==.  We would have to write a
    // lot of code to do that if this were a class.
    // ##############################################################################

    /// <summary>
    /// Identifies one hardware media player in the system, such as Player 1 on Nax1.
    /// </summary>
    public sealed record MediaPlayerId
    {
        /// <summary>
        /// The hardware hosting the player, such as "Nax1".  This must match the
        /// HardwareId given to the hardware wrapper.
        /// </summary>
        public string HardwareId { get; }

        /// <summary>
        /// The player number on that hardware.  Crestron NAX players start at 1.
        ///
        /// Always number players from 1, even if the device numbers them from 0.
        /// CrpcHardwareConnection only listens on players 1 through playerCount,
        /// so a PlayerNumber of 0 is accepted here but never reaches the hardware.
        /// For a 0-based device, translate in the CrpcHardwareConnection callback
        /// (playerNumber - 1) and translate back when calling SendFeedback.
        /// </summary>
        public ushort PlayerNumber { get; }

        /// <param name="hardwareId">The hardware hosting the player, such as "Nax1".
        /// Cannot be blank.</param>
        /// <param name="playerNumber">The player number on that hardware.</param>
        /// <exception cref="ArgumentException">hardwareId is blank.</exception>
        public MediaPlayerId(string hardwareId, ushort playerNumber)
        {
            // A blank HardwareId is a configuration mistake.  Stop here, at startup,
            // instead of letting CRPC messages quietly go nowhere later.
            if (string.IsNullOrWhiteSpace(hardwareId))
            {
                throw new ArgumentException(
                    $"A media player needs a HardwareId, such as \"Nax1\". Player {playerNumber} has none.",
                    nameof(hardwareId));
            }

            HardwareId = hardwareId;
            PlayerNumber = playerNumber;
        }

        /// <summary>
        /// Broker key for CRPC traveling from a UI to this player.
        /// The hardware subscribes to it; the UI sends on it.
        /// 
        /// {this} leverages string interpolation and an override of ToString() to 
        /// produce a readable form such as "Nax1.Player1".
        /// </summary>
        public string CommandKey => $"MediaPlayer[{this}]Crpc";

        /// <summary>
        /// Broker key for CRPC traveling from this player back to the UI.
        /// The UI subscribes to it; the hardware sends on it.
        /// </summary>
        public string FeedbackKey => $"MediaPlayer[{this}]CrpcFb";

        /// <summary>
        /// Readable form used in logs and broker keys, such as "Nax1.Player1".
        /// </summary>
        public override string ToString() => $"{HardwareId}.Player{PlayerNumber}";
    }
}
