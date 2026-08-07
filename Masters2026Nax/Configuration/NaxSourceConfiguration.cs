using System.Collections.Generic;

namespace Masters2026Nax.Configuration
{
    // -----------------------------------------------------------------------------
    // NAX Source Configuration
    //
    // This file is the single source of truth for every source shown on the
    // touchpanel. The UI uses it to label the buttons. The hardware setup uses it
    // to determine what each button actually routes.
    // Keeping everything together means we only have one place to update when
    // sources change. Maintaining multiple lists that all describe the same thing
    // sounds easy... right up until one of them doesn't get updated.
    //
    // AES67 sources also live here. They're still just sources, they simply need an
    // IP address and port in addition to the DM-NAX source number.
    //
    // You'll also notice this is a static class. New programmers often read that
    // "static is a code smell" and conclude it should never be used. Like most
    // advice found on the Internet, the real answer is "it depends."
    // This class contains configuration data. It has no state, no lifetime, and no
    // reason for multiple copies to exist. Creating instances of it would add code
    // without adding any value, so making it static is the easiest solution.
    // A static class is often confused with the Singleton design pattern. They are
    // not the same thing. A Singleton has one instance. A static class has no
    // instances at all. In this case we don't need an object, we simply need one
    // place to store shared configuration.
    // Don't avoid a language feature because someone on the Internet said it's
    //  bad. Every feature exists because it solves a particular problem.
    // The trick is knowing when it actually fits the problem,
    // and when to not listen to people's opinions on Reddit or stack overflow..
    // -----------------------------------------------------------------------------

    /// <summary>
    /// Provides the shared NAX source configuration used by both the UI and the
    /// hardware setup code.
    /// </summary>
    public static class NaxSourceConfiguration
    {
        /// <summary>
        /// Contains one source list for each touchpanel source-selection button
        /// list used by the project.
        /// SourceLists[0] is intentionally unused so source-list numbers can begin
        /// at 1. Wasting one empty list is cheaper than explaining why every list
        /// number is off by one for the rest of the project.
        /// Change the names, source numbers, stream addresses, ports, and ordering
        /// below to match the project. The order of each list determines the
        /// button order shown on the touchpanel.
        /// </summary>
        public static readonly IReadOnlyList<IReadOnlyList<NaxSourceDefinition>>
            SourceLists =
                new List<IReadOnlyList<NaxSourceDefinition>>
                {
                    // Index 0 is intentionally unused.
                    new List<NaxSourceDefinition>(),

                    // Source List 1
                    new List<NaxSourceDefinition>
                    {
                        new NaxSourceDefinition("Digital Input 1", 1),
                        new NaxSourceDefinition("Analog Input 1", 5),
                        new NaxSourceDefinition("Media Player 1", 9),
                        new NaxSourceDefinition("Media Player 2", 10),
                        new NaxSourceDefinition(
                            "Wasteland Public Radio",
                            17,
                            "239.10.20.31",
                            5004),
                        new NaxSourceDefinition(
                            "News Radio",
                            17,
                            "239.10.20.32",
                            5004)
                    },

                    // Source List 2
                    new List<NaxSourceDefinition>
                    {
                        new NaxSourceDefinition("Digital Input 2", 2),
                        new NaxSourceDefinition("Analog Input 2", 6),
                        new NaxSourceDefinition("Media Player 3", 11),
                        new NaxSourceDefinition("Media Player 4", 12),
                        new NaxSourceDefinition(
                            "Music Stream",
                            17,
                            "239.10.20.33",
                            5004),
                        new NaxSourceDefinition(
                            "Announcement Stream",
                            17,
                            "239.10.20.34",
                            5004)
                    },

                    // Source List 3
                    new List<NaxSourceDefinition>
                    {
                        new NaxSourceDefinition("Digital Input 3", 3),
                        new NaxSourceDefinition("Analog Input 3", 7),
                        new NaxSourceDefinition("Media Player 5", 13),
                        new NaxSourceDefinition("Media Player 6", 14),
                        new NaxSourceDefinition(
                            "Conference Stream",
                            17,
                            "239.10.20.35",
                            5004),
                        new NaxSourceDefinition(
                            "Overflow Stream",
                            17,
                            "239.10.20.36",
                            5004)
                    },

                    // Source List 4
                    new List<NaxSourceDefinition>
                    {
                        new NaxSourceDefinition("Digital Input 4", 4),
                        new NaxSourceDefinition("Analog Input 4", 8),
                        new NaxSourceDefinition("Media Player 7", 15),
                        new NaxSourceDefinition("Media Player 8", 16),
                        new NaxSourceDefinition(
                            "Lobby Stream",
                            17,
                            "239.10.20.37",
                            5004),
                        new NaxSourceDefinition(
                            "Backup Stream",
                            17,
                            "239.10.20.38",
                            5004)
                    }
                };


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
        /// Returns the touchpanel button index assigned to an actual DM-NAX source
        /// number within the selected source list.
        /// This works for sources that appear only once in the list. AES67 sources
        /// may share source number 17, so feedback for those sources should normally
        /// track the selected button index or compare the stream address as well.
        /// </summary>
        /// <param name="sourceList">
        /// The source-list number.
        /// </param>
        /// <param name="sourceNumber">
        /// The actual source number reported by, or sent to, the DM-NAX amplifier.
        /// </param>
        /// <returns>
        /// The first matching zero-based touchpanel button index. Returns
        /// ushort.MaxValue when the source is not included in the selected list.
        /// </returns>
        public static ushort GetButtonIndex(
            ushort sourceList,
            ushort sourceNumber)
        {
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
        /// Returns the touchpanel button index assigned to an AES67 stream address
        /// within the selected source list.
        /// This is useful because several buttons may all route source 17 while
        /// selecting different multicast streams.
        /// </summary>
        /// <param name="sourceList">
        /// The source-list number.
        /// </param>
        /// <param name="streamAddress">
        /// The AES67 multicast address to find.
        /// </param>
        /// <returns>
        /// The matching zero-based touchpanel button index. Returns ushort.MaxValue
        /// when the stream address is not included in the selected list.
        /// </returns>
        public static ushort GetButtonIndexFromStreamAddress(
            ushort sourceList,
            string streamAddress)
        {
            IReadOnlyList<NaxSourceDefinition> sources =
                SourceLists[sourceList];

            for (ushort index = 0; index < sources.Count; index++)
            {
                if (sources[index].StreamAddress == streamAddress)
                {
                    return index;
                }
            }

            return ushort.MaxValue;
        }

        /// <summary>
        /// Returns the AES67 multicast address assigned to the selected button.
        /// For non-AES67 sources, this returns an empty string.
        /// </summary>
        /// <param name="sourceList">
        /// The source-list number.
        /// </param>
        /// <param name="buttonIndex">
        /// The zero-based index returned by the touchpanel button list.
        /// </param>
        /// <returns>
        /// The configured AES67 multicast address, or an empty string if this source
        /// does not use AES67.
        /// </returns>
        public static string GetStreamAddress(
            ushort sourceList,
            ushort buttonIndex)
        {
            return SourceLists[sourceList][buttonIndex].StreamAddress;
        }

        /// <summary>
        /// Returns the AES67 port assigned to the selected button.
        /// For non-AES67 sources, this returns 0.
        /// </summary>
        /// <param name="sourceList">
        /// The source-list number.
        /// </param>
        /// <param name="buttonIndex">
        /// The zero-based index returned by the touchpanel button list.
        /// </param>
        /// <returns>
        /// The configured AES67 port, or 0 if this source does not use AES67.
        /// </returns>
        public static short GetStreamPort(
            ushort sourceList,
            ushort buttonIndex)
        {
            return SourceLists[sourceList][buttonIndex].StreamPort;
        }

        /// <summary>
        /// Returns true if the selected source uses an AES67 stream.
        /// This is determined by checking whether a stream address has been configured.
        /// </summary>
        /// <param name="sourceList">
        /// The source-list number.
        /// </param>
        /// <param name="buttonIndex">
        /// The zero-based index returned by the touchpanel button list.
        /// </param>
        /// <returns>
        /// True if the selected source uses an AES67 stream; otherwise false.
        /// </returns>
        public static bool IsStreamSource(
            ushort sourceList,
            ushort buttonIndex)
        {
            return SourceLists[sourceList][buttonIndex].StreamAddress != string.Empty;
        }

        /// <summary>
        /// Returns the number of source buttons configured in the selected source
        /// list.
        /// </summary>
        public static ushort GetSourceCount(ushort sourceList)
        {
            return (ushort)SourceLists[sourceList].Count;
        }
    }


    /// <summary>
    /// Defines one source presented by a touchpanel source-selection button.
    ///
    /// The position of this object in a source list determines the touchpanel
    /// button index. Name is displayed on the touchpanel, SourceNumber is the
    /// actual routing value expected by the DM-NAX amplifier, and the optional
    /// stream information is used for AES67 sources.
    /// </summary>
    public class NaxSourceDefinition
    {
        /// <summary>
        /// Gets the friendly source name displayed on the touchpanel.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the actual source number used by the DM-NAX amplifier.
        /// </summary>
        public ushort SourceNumber { get; private set; }

        /// <summary>
        /// Gets the optional AES67 multicast address used by this source.
        /// An empty string means the source does not require an AES67 stream.
        /// </summary>
        public string StreamAddress { get; private set; }

        /// <summary>
        /// Gets the optional AES67 port used by this source.
        /// A value of 0 means the source does not require an AES67 stream.
        /// </summary>
        public short StreamPort { get; private set; }

        /// <summary>
        /// Creates a source definition containing the touchpanel name, DM-NAX
        /// source number, and optional AES67 stream information.
        /// </summary>
        /// <param name="name">
        /// The source name displayed on the touchpanel.
        /// </param>
        /// <param name="sourceNumber">
        /// The source number expected by the DM-NAX amplifier.
        /// </param>
        /// <param name="streamAddress">
        /// Optional AES67 multicast address. Leave empty for non-streaming sources.
        /// </param>
        /// <param name="streamPort">
        /// Optional AES67 port. Leave at 0 for non-streaming sources.
        /// </param>
        public NaxSourceDefinition(
            string name,
            ushort sourceNumber,
            string streamAddress = "",
            short streamPort = 0)
        {
            Name = name;
            SourceNumber = sourceNumber;
            StreamAddress = streamAddress;
            StreamPort = streamPort;
        }
    }
}
