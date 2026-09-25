using System;
using System.Collections.Generic;
using System.Text;

namespace Masters2026Nax
{
    public partial class UI
    {

    // ------------------------------------------------------------
    // ####         TEMPORARY CRPC CONNECTION TEST ROUTE       ####
    // ------------------------------------------------------------
    //
    // This code is for testing and validation only.
    //
    // Its purpose is to help determine the CRPC communication
    // requirements, connection sequence, and any timing requirements
    // involved when routing CH5 to a NAX media player.
    //
    // This is not intended to be the final CRPC implementation.
    // Once the required behavior has been confirmed, this code will
    // be replaced by a complete CRPC routing class.
    //
    // For testing, the CRPC connection is handled in two stages.
    //
    // 1. Handshake
    //    Only the Crpc.Register request is allowed from CH5 to the
    //    selected NAX player.
    //
    //    Only the matching response from that player is allowed
    //    back to CH5.
    //
    // 2. Connected
    //    Once the Register response has been received, normal
    //    CRPC traffic is routed in both directions.
    //
    // This also helps prevent traffic from a previously selected
    // player from being mixed into a new CH5 CRPC session while
    // testing the connection process.
    // ------------------------------------------------------------
        private enum CrpcRouteState
        {
            Disconnected,
            WaitingForRegister,
            WaitingForRegisterResponse,
            Connected
        }
        private CrpcRouteState _crpcRouteState =
            CrpcRouteState.Disconnected;

        private uint _connectedPlayer = 0;
        private uint _pendingPlayer = 0;
        private int _registerRequestId = 0;

        private readonly List<string> _registerResponseFrames =
            new List<string>();

        /// <summary>
        /// Begins connecting CH5 to a different NAX media player.
        ///
        /// Calling this immediately breaks the existing CRPC route.
        /// No normal CRPC traffic will be passed until the new player
        /// successfully answers the Crpc.Register request.
        /// </summary>
        public void ConnectNaxPlayer(uint playerNumber)
        {
            // Break the existing connection immediately.
            _connectedPlayer = 0;

            // Remember which player we want to connect to.
            _pendingPlayer = playerNumber;

            // Clear anything left from a previous attempt.
            _registerRequestId = 0;
            _registerResponseFrames.Clear();

            // We are now waiting for CH5 to send Crpc.Register.
            _crpcRouteState = CrpcRouteState.WaitingForRegister;
        }

        /// <summary>
        /// Called whenever CRPC data is received from CH5.
        /// </summary>
        public void CrpcFromCh5(string data)
        {
            if (string.IsNullOrEmpty(data))
                return;

            if (_crpcRouteState == CrpcRouteState.Connected)
            {
                SendCrpcToNax(_connectedPlayer, data);
                return;
            }

            if (_crpcRouteState == CrpcRouteState.WaitingForRegister)
            {
                // The first CRPC frame contains the beginning of the
                // JSON message, including the Crpc.Register method.
                // This may be completely overkill.  Not sure yet
                // But this is all based on observed data from a Simpl Connection capture
                string payload = GetCrpcPayload(data);

                if (!payload.Contains("\"method\":\"Crpc.Register\""))
                    return;


                // Grab the JSON-RPC request ID so we can identify the
                // response from the NAX.
                _registerRequestId = GetJsonRpcId(payload);


                // Forward the first Register frame.
                SendCrpcToNax(_pendingPlayer, data);


                // The Register message may contain additional frames.
                //
                // Until the final frame is received, continue sending
                // CH5 data to the pending player.
                _crpcRouteState =
                    CrpcRouteState.WaitingForRegisterResponse;

                return;
            }


            // --------------------------------------------------------
            // REGISTER MESSAGE CONTINUATION
            // --------------------------------------------------------
            //
            // While waiting for the Register response, CH5 may still
            // be finishing the fragmented Register request.
            //
            // Forward those frames to the pending player.
            //
            // Once CH5 has completed Register it should not send
            // anything else until it receives the response.

            if (_crpcRouteState ==
                CrpcRouteState.WaitingForRegisterResponse)
            {
                SendCrpcToNax(_pendingPlayer, data);
            }
        }



        // ============================================================
        // DATA FROM NAX
        // ============================================================

        /// <summary>
        /// Called whenever CRPC feedback is received from a NAX
        /// media player.
        /// </summary>
        public void CrpcFromNax(uint playerNumber, string data)
        {
            if (string.IsNullOrEmpty(data))
                return;


            // --------------------------------------------------------
            // NORMAL CONNECTED OPERATION
            // --------------------------------------------------------

            if (_crpcRouteState == CrpcRouteState.Connected)
            {
                // Ignore feedback from every player except the one
                // currently routed to CH5.
                if (playerNumber == _connectedPlayer)
                {
                    SendCrpcToCh5(data);
                }

                return;
            }


            // --------------------------------------------------------
            // WAITING FOR REGISTER RESPONSE
            // --------------------------------------------------------

            if (_crpcRouteState ==
                CrpcRouteState.WaitingForRegisterResponse)
            {
                // Ignore all other NAX players.
                if (playerNumber != _pendingPlayer)
                    return;


                // Do not send this directly to CH5 yet.
                //
                // Hold the frames until we know this really is the
                // response to our Register request.
                _registerResponseFrames.Add(data);


                // 0x1E in the second byte identifies the final frame
                // in the CRPC message seen in the capture.
                if (!IsFinalCrpcFrame(data))
                    return;


                // We now have the complete CRPC response.
                string json = GetBufferedPayload(
                    _registerResponseFrames);


                // Make sure this response belongs to the Register
                // request that started this connection.
                if (!ContainsJsonRpcId(
                    json,
                    _registerRequestId))
                {
                    _registerResponseFrames.Clear();
                    return;
                }


                // ----------------------------------------------------
                // CONNECTION COMPLETE
                // ----------------------------------------------------
                //
                // Open the normal route BEFORE sending the Register
                // response to CH5.
                //
                // CH5 may immediately send Crpc.GetObjects when it
                // receives the successful Register response.

                _connectedPlayer = _pendingPlayer;
                _pendingPlayer = 0;

                _crpcRouteState =
                    CrpcRouteState.Connected;


                // Now release the complete Register response to CH5.
                foreach (string frame in _registerResponseFrames)
                {
                    SendCrpcToCh5(frame);
                }


                _registerResponseFrames.Clear();
            }
        }



        // ============================================================
        // ACTUAL ROUTING
        // ============================================================
        //
        // These two methods are where this class connects to the
        // existing UI/NAX code.
        //
        // For now they are intentionally left simple.  They can be
        // connected to the existing Crestron string signals or NAX
        // objects once we plug this into the real UI class.
        // ============================================================

        private void SendCrpcToNax(
            uint playerNumber,
            string data)
        {
            //
            // TODO:
            //
            // Route data to the CRPC input of the requested
            // NAX media player.
            //
            // Example:
            //
            // if (playerNumber == 1)
            //     Player1Crpc.StringValue = data;
            //
            // if (playerNumber == 2)
            //     Player2Crpc.StringValue = data;
            //
        }


        private void SendCrpcToCh5(string data)
        {
            //
            // TODO:
            //
            // Send NAX CRPC feedback back to the CH5 CRPC_FB
            // signal.
            //
            // Example:
            //
            // CrpcFb.StringValue = data;
            //
        }



        // ============================================================
        // SMALL CRPC HELPERS
        // ============================================================

        /// <summary>
        /// Returns the JSON portion of a CRPC frame.
        ///
        /// The captured CRPC packets contain a four-byte header
        /// followed by the UTF-8 JSON payload.
        /// </summary>
        private string GetCrpcPayload(string data)
        {
            if (data.Length <= 4)
                return string.Empty;

            return data.Substring(4);
        }


        /// <summary>
        /// Determines whether this is the final frame of a fragmented
        /// CRPC message.
        ///
        /// From the capture:
        ///
        ///     0x1C = more frames follow
        ///     0x1E = final frame
        /// </summary>
        private bool IsFinalCrpcFrame(string data)
        {
            if (data.Length < 2)
                return false;

            return data[1] == 0x1E;
        }


        /// <summary>
        /// Combines the JSON payload from several CRPC frames.
        /// </summary>
        private string GetBufferedPayload(
            List<string> frames)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string frame in frames)
            {
                builder.Append(GetCrpcPayload(frame));
            }

            return builder.ToString();
        }


        /// <summary>
        /// Gets the JSON-RPC ID from the beginning of a request.
        ///
        /// This is intentionally a very small parser.  We only need
        /// the numeric ID from Crpc.Register at this point.
        /// </summary>
        private int GetJsonRpcId(string json)
        {
            const string search = "\"id\":";

            int start = json.IndexOf(
                search,
                StringComparison.Ordinal);

            if (start < 0)
                return 0;

            start += search.Length;

            int end = start;

            while (end < json.Length &&
                   char.IsDigit(json[end]))
            {
                end++;
            }

            int id;

            if (int.TryParse(
                json.Substring(start, end - start),
                out id))
            {
                return id;
            }

            return 0;
        }


        /// <summary>
        /// Checks whether a completed JSON-RPC response contains
        /// the expected request ID.
        /// </summary>
        private bool ContainsJsonRpcId(
            string json,
            int id)
        {
            string search = "\"id\":" + id.ToString();

            return json.IndexOf(
                search,
                StringComparison.Ordinal) >= 0;
        }
    }
}