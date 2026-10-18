using System.Collections.Generic;

namespace Masters2026Nax.Configuration
{
    // -----------------------------------------------------------------------------
    // NAX Source Configuration
    //
    // This class keeps all of our NAX source information in one place. The UI uses
    // it to determine what should be displayed on the source buttons while the
    // hardware code uses the same information to determine what should be routed.
    //
    // The important part is that this class does not care where the configuration
    // came from. The programmer can build it manually with AddZone() and AddSource(),
    // load the information from a configuration file, or come up with something
    // completely different later. The rest of the program continues to use the
    // same lookup methods either way.
    //
    // AES67 sources also live here. They are still just sources, they simply need
    // an AES67 Session Name in addition to the DM-NAX source number.
    //
    // NAX currently uses the AES67 Session Name to select the stream rather than
    // allowing us to configure the multicast address and port directly.
    //
    // You'll also notice this is a static class. New programmers often read that
    // "static is a code smell" and conclude it should never be used. Like most
    // advice found on the Internet, the real answer is "it depends."
    //
    // This class contains shared configuration data. It has no reason for multiple
    // copies to exist, so creating instances of it would add code without adding
    // any value.
    //
    // A static class is also not a Singleton. A Singleton has one instance.
    // A static class has no instances at all.
    //
    // Don't avoid a language feature because someone on the Internet said it's bad.
    // Use the feature when it fits the problem and don't use it when it doesn't.
    // -----------------------------------------------------------------------------

    public static class NaxSourceConfiguration
    {
        // The position of each List is also its zone number.
        //
        // Index 0 is intentionally unused so our zone numbers can begin at 1.
        // Wasting one empty List is cheaper than explaining why every zone number
        // is off by one for the rest of the project.
        //
        // We use Lists here because the configuration can now be changed at runtime.
        // The original version used IReadOnlyList because all of the sources were
        // hard coded when the class was created. That is no longer what we want.
        private static readonly List<List<NaxSourceDefinition>> SourceLists =
            new List<List<NaxSourceDefinition>>
            {
                new List<NaxSourceDefinition>()
            };

        // The UI's Media Player needs a "src" number for each player in the CRPC
        // source change message. Player numbers can repeat across hardware (Nax1 and
        // Nax2 both have a Player1), so we hand out our own numbers instead: 1, 2, 3,
        // in the order each player is first added. The same player added to several
        // zones keeps the same number because MediaPlayerId is a record.
        //
        // Numbers start at 1 because src 0 means "no source" to the UI's Media Player.
        private static readonly Dictionary<MediaPlayerId, ushort> MediaPlayerSrcNumbers =
            new Dictionary<MediaPlayerId, ushort>();


        // #########################################################################
        // Configuration Methods
        // #########################################################################

        /// <summary>
        /// Adds a new zone to the configuration and returns its zone number.
        ///
        /// The returned number is used by AddSource() and all of the existing
        /// lookup methods.
        /// </summary>
        public static ushort AddZone()
        {
            SourceLists.Add(new List<NaxSourceDefinition>());

            return (ushort)(SourceLists.Count - 1);
        }


        /// <summary>
        /// Adds a source to an existing zone.
        ///
        /// The order sources are added is important because their position in the
        /// List becomes the button index used by the touchpanel.
        /// </summary>
        /// <param name="name">Friendly name displayed on the touchpanel.</param>
        /// <param name="sourceNumber">Actual source number sent to the NAX device.</param>
        /// <param name="sessionName">AES67 Session Name if this source uses a stream.</param>
        /// <param name="mediaPlayer">The Media Player this source represents, such
        /// as new MediaPlayerId("Nax1", 1). Leave as null for non-player sources.</param>
        public static void AddSource(
            ushort zone,
            string name,
            ushort sourceNumber,
            string sessionName = "",
            MediaPlayerId? mediaPlayer = null)
        {
            SourceLists[zone].Add(
                new NaxSourceDefinition(
                    name,
                    sourceNumber,
                    sessionName,
                    mediaPlayer));

            if (mediaPlayer != null && !MediaPlayerSrcNumbers.ContainsKey(mediaPlayer))
            {
                MediaPlayerSrcNumbers.Add(
                    mediaPlayer,
                    (ushort)(MediaPlayerSrcNumbers.Count + 1));
            }
        }


        /// <summary>
        /// Deletes a source from a zone using its button index.
        ///
        /// Because the List position is also the touchpanel button index, every
        /// source after the deleted source will move down one button position.
        /// </summary>
        public static void DeleteSource(
            ushort zone,
            ushort buttonIndex)
        {
            SourceLists[zone].RemoveAt(buttonIndex);
        }


        /// <summary>
        /// Deletes a zone by clearing all of the sources assigned to it.
        ///
        /// We deliberately do not remove the actual List. The List index is also
        /// the zone number used throughout the rest of the program. If we removed
        /// Zone 2 from the List then Zone 3 would suddenly become Zone 2, which
        /// would be a rather unpleasant surprise for everything using Zone 3.
        /// </summary>
        public static void DeleteZone(ushort zone)
        {
            SourceLists[zone].Clear();
        }


        // #########################################################################
        // Source Lookup Methods
        //
        // Everything below here is how the rest of the application talks to this
        // class. These methods do not care whether the configuration was manually
        // entered, loaded from a file, or created some other way.
        // #########################################################################

        /// <summary>
        /// Returns the actual DM-NAX source number assigned to a button in the
        /// selected source list.
        /// </summary>
        public static ushort GetSourceNumber(
            ushort sourceList,
            ushort buttonIndex)
        {
            return SourceLists[sourceList][buttonIndex].SourceNumber;
        }


        /// <summary>
        /// Returns the friendly source name assigned to a button in the selected
        /// source list.
        /// </summary>
        public static string GetSourceName(
            ushort sourceList,
            ushort buttonIndex)
        {
            return SourceLists[sourceList][buttonIndex].Name;
        }


        /// <summary>
        /// Returns the touchpanel button index that matches the source feedback
        /// received from the NAX device.
        ///
        /// Each source definition already tells us what is required to identify
        /// that source. The source number must always match. If the source also has
        /// a Session Name, that must match as well.
        ///
        /// This keeps the lookup independent of the actual NAX hardware. We don't
        /// need to know how many sources the device has or which source number it
        /// uses for AES67 because the configuration already knows that.
        /// </summary>
        public static ushort GetButtonIndexFromFeedback(
            ushort sourceList,
            ushort sourceNumber,
            string sessionName)
        {
            if (sourceList >= SourceLists.Count)
                return 0;

            IReadOnlyList<NaxSourceDefinition> sources =
                SourceLists[sourceList];

            for (ushort index = 0; index < sources.Count; index++)
            {
                // The source number must always match.
                if (sources[index].SourceNumber == sourceNumber)
                {
                    // If there is no Session Name then the source number is enough
                    // information to identify this source.
                    if (sources[index].SessionName == string.Empty)
                        return index;

                    // If the source has a Session Name then it is also part of the
                    // identity of the source and must match the feedback.
                    if (sources[index].SessionName == sessionName)
                        return index;
                }
            }

            return 0;
        }


        /// <summary>
        /// Returns the touchpanel button index assigned to an actual DM-NAX source
        /// number within the selected source list.
        ///
        /// This works for sources that appear only once in the List. AES67 sources
        /// may share the same source number, so those should normally be identified
        /// using their Session Name as well.
        /// </summary>
        public static ushort GetButtonIndex(
            ushort sourceList,
            ushort sourceNumber)
        {
            if (sourceList >= SourceLists.Count)
                return 0;

            IReadOnlyList<NaxSourceDefinition> sources =
                SourceLists[sourceList];

            for (ushort index = 0; index < sources.Count; index++)
            {
                if (sources[index].SourceNumber == sourceNumber)
                {
                    return index;
                }
            }

            return ushort.MaxValue;
        }


        /// <summary>
        /// Returns the touchpanel button index assigned to an AES67 Session Name
        /// within the selected source list.
        ///
        /// Several buttons may use the same NAX source number for AES67, so the
        /// Session Name identifies which stream is actually selected.
        /// </summary>
        public static ushort GetButtonIndexFromSessionName(
            ushort sourceList,
            string sessionName)
        {
            if (sourceList >= SourceLists.Count)
                return 0;

            IReadOnlyList<NaxSourceDefinition> sources =
                SourceLists[sourceList];

            for (ushort index = 0; index < sources.Count; index++)
            {
                if (sources[index].SessionName == sessionName)
                {
                    return index;
                }
            }

            return ushort.MaxValue;
        }


        /// <summary>
        /// Returns the AES67 Session Name assigned to the selected button.
        /// For non-AES67 sources this returns an empty string.
        /// </summary>
        public static string GetSessionName(
            ushort sourceList,
            ushort buttonIndex)
        {
            return SourceLists[sourceList][buttonIndex].SessionName;
        }


        /// <summary>
        /// Returns true if the selected source uses an AES67 stream.
        /// A source with a Session Name is considered a stream source.
        /// </summary>
        public static bool IsStreamSource(
            ushort sourceList,
            ushort buttonIndex)
        {
            return SourceLists[sourceList][buttonIndex].SessionName != string.Empty;
        }


        /// <summary>
        /// Returns true if the selected source represents a Media Player.
        ///
        /// We cannot determine this reliably from the NAX source number. On the NAX
        /// amplifier a Media Player is a direct source, while another NAX device may
        /// receive that same Media Player as an AES67 stream. The configuration
        /// already knows what the source represents, so we keep that information here.
        /// </summary>
        public static bool IsMediaPlayer(
            ushort sourceList,
            ushort buttonIndex)
        {
            if (sourceList >= SourceLists.Count)
                return false;

            if (buttonIndex >= SourceLists[sourceList].Count)
                return false;

            return SourceLists[sourceList][buttonIndex].IsMediaPlayer;
        }

        /// <summary>
        /// Get the Media Player for a specific source.
        /// </summary>
        /// <param name="sourceList">Which source list to refer to.</param>
        /// <param name="buttonIndex">Which button the source is linked to.</param>
        /// <returns>The Media Player, or null if this source is not a Media Player.</returns>
        public static MediaPlayerId? GetMediaPlayer(
            ushort sourceList,
            ushort buttonIndex)
        {
            if (sourceList >= SourceLists.Count)
                return null;
            if (buttonIndex >= SourceLists[sourceList].Count)
                return null;
            return SourceLists[sourceList][buttonIndex].MediaPlayer;
        }

        /// <summary>
        /// Returns the "src" number the UI's Media Player uses for this player in
        /// the CRPC source change message. Every player in the configuration has
        /// its own number, starting at 1.
        /// </summary>
        /// <param name="mediaPlayer">The Media Player to look up.</param>
        /// <returns>The src number, or 0 ("no source") if the player was never
        /// added to the configuration.</returns>
        public static ushort GetMediaPlayerSrc(MediaPlayerId mediaPlayer)
        {
            if (MediaPlayerSrcNumbers.TryGetValue(mediaPlayer, out var src))
                return src;

            return 0;
        }

        /// <summary>
        /// Returns the number of sources configured in the selected source list.
        /// </summary>
        public static ushort GetSourceCount(ushort sourceList)
        {
            return (ushort)SourceLists[sourceList].Count;
        }
    }


    // #########################################################################
    // NAX Source Definition
    // #########################################################################

    /// <summary>
    /// Describes one source in a NAX source list.
    ///
    /// The object only describes a source. It does not know which hardware is
    /// using it, where the configuration came from, or what the UI will do with it.
    /// </summary>
    /// <param name="name">Friendly name displayed on the touchpanel.</param>
    /// <param name="sourceNumber">Actual source number sent to the NAX device.</param>
    /// <param name="sessionName">AES67 Session Name if this source uses a stream.</param>
    /// <param name="mediaPlayer">The Media Player this source represents, or null
    /// for non-player sources.</param>
    public class NaxSourceDefinition
    {
        public string Name { get; private set; }
        public ushort SourceNumber { get; private set; }
        public string SessionName { get; private set; }

        /// <summary>
        /// The Media Player this source represents. Null means this source is not
        /// a Media Player.
        /// </summary>
        public MediaPlayerId? MediaPlayer { get; private set; }
        public bool IsMediaPlayer => MediaPlayer != null;


        public NaxSourceDefinition(
            string name,
            ushort sourceNumber,
            string sessionName = "",
            MediaPlayerId? mediaPlayer = null)
        {
            Name = name;
            SourceNumber = sourceNumber;
            SessionName = sessionName;
            MediaPlayer = mediaPlayer;
        }
    }
}