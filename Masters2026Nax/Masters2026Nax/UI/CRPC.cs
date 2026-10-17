using CwsDebug;
using System;
using System.Text.Json.Serialization;

namespace Masters2026Nax
{
    public static class CRPC
    {
        /// <summary>
        /// Creates a JSON-serialized source change message for the specified UI instance and source.  This message must be sent to the UI's MediaPlayer_Message_FB input every time the source changes.
        /// </summary>
        /// <param name="uiInstance">The identifier of the UI instance.  Must be greater than 0.  If 0, an empty string is returned and an error is logged.</param>
        /// <param name="source">The identifier of the source.</param>
        /// <returns>A JSON string representing CRPCSourceChangeProperties for the specified UI instance and source.</returns>
        public static string GetSourceChangeMessage(ushort uiInstance, ushort source)
        {
            if (uiInstance == 0)
            {
                CwsDebugPage.LogError($"CRPC.GetSourceChangeMessage({uiInstance},{source}).  UI instance must be greater than 0.  Message not generated.");
                return string.Empty;
            }

            var properties = new CRPCSourceChangeProperties(uiInstance, source);
            return System.Text.Json.JsonSerializer.Serialize(properties);
        }

        /// <summary>
        /// Properties for a source-change message sent to the hardware media player instance.
        /// </summary>
        /// <remarks>These properties are serialized to JSON and sent to the hardware media player instance.  The names are case-sensitive, so we set the JsonPropertyName attributes accordingly.</remarks>
        private sealed class CRPCSourceChangeProperties
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
            /// Initializes a new instance of CRPCSourceChangeProperties with the specified instance, source, and default ver value.
            /// </summary>
            /// <param name="uiInstance">The instance number used to identify which UI's Media Player requested the change.  This is formatted as a two-digit decimal string.</param>
            /// <param name="source">The source identifier.  This is converted to a decimal string.</param>
            public CRPCSourceChangeProperties(ushort uiInstance, ushort source)
            {
                this.Tag = uiInstance.ToString("D2");
                this.Src = source.ToString();
            }
        }
    }
}
