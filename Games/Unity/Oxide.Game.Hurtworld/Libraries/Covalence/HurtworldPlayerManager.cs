﻿using System;
using System.Collections.Generic;
using System.Linq;

using ProtoBuf;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Game.Hurtworld.Libraries.Covalence
{
    /// <summary>
    /// Represents a generic player manager
    /// </summary>
    public class HurtworldPlayerManager : IPlayerManager
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
        private struct PlayerRecord
        {
            public string Name;
            public ulong Id;
        }

        private readonly IDictionary<string, PlayerRecord> playerData;
        private readonly IDictionary<string, HurtworldPlayer> allPlayers;
        private readonly IDictionary<string, HurtworldPlayer> connectedPlayers;

        internal HurtworldPlayerManager()
        {
            // Load player data
            Utility.DatafileToProto<Dictionary<string, PlayerRecord>>("oxide.covalence");
            playerData = ProtoStorage.Load<Dictionary<string, PlayerRecord>>("oxide.covalence") ?? new Dictionary<string, PlayerRecord>();
            allPlayers = new Dictionary<string, HurtworldPlayer>();
            foreach (var pair in playerData) allPlayers.Add(pair.Key, new HurtworldPlayer(pair.Value.Id, pair.Value.Name));
            connectedPlayers = new Dictionary<string, HurtworldPlayer>();

            // Cleanup old .data
            Cleanup.Add(ProtoStorage.GetFileDataPath("oxide.covalence.playerdata.data"));
        }

        private void NotifyPlayerJoin(PlayerSession session)
        {
            var id = session.SteamId.ToString();

            // Do they exist?
            PlayerRecord record;
            if (playerData.TryGetValue(id, out record))
            {
                // Update
                record.Name = session.Name;
                playerData[id] = record;

                // Swap out Rust player
                allPlayers.Remove(id);
                allPlayers.Add(id, new HurtworldPlayer(session));
            }
            else
            {
                // Insert
                record = new PlayerRecord { Id = (ulong)session.SteamId, Name = session.Name };
                playerData.Add(id, record);

                // Create Rust player
                allPlayers.Add(id, new HurtworldPlayer(session));
            }

            // Save
            ProtoStorage.Save(playerData, "oxide.covalence");
        }

        internal void NotifyPlayerConnect(PlayerSession session)
        {
            NotifyPlayerJoin(session);
            connectedPlayers[session.SteamId.ToString()] = new HurtworldPlayer(session);
        }

        internal void NotifyPlayerDisconnect(PlayerSession session) => connectedPlayers.Remove(session.SteamId.ToString());

        #region Player Finding

        /// <summary>
        /// Gets all players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> All => allPlayers.Values.Cast<IPlayer>();

        /// <summary>
        /// Gets all connected players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> Connected => connectedPlayers.Values.Cast<IPlayer>();

        /// <summary>
        /// Finds a single player given unique ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IPlayer FindPlayerById(string id)
        {
            HurtworldPlayer player;
            return allPlayers.TryGetValue(id, out player) ? player : null;
        }

        /// <summary>
        /// Finds a single player given a partial name or unique ID (case-insensitive, wildcards accepted, multiple matches returns null)
        /// </summary>
        /// <param name="partialNameOrId"></param>
        /// <returns></returns>
        public IPlayer FindPlayer(string partialNameOrId)
        {
            var players = FindPlayers(partialNameOrId).ToArray();
            return players.Length == 1 ? players[0] : null;
        }

        /// <summary>
        /// Finds any number of players given a partial name or unique ID (case-insensitive, wildcards accepted)
        /// </summary>
        /// <param name="partialNameOrId"></param>
        /// <returns></returns>
        public IEnumerable<IPlayer> FindPlayers(string partialNameOrId)
        {
            foreach (var player in allPlayers.Values)
            {
                if (player.Name != null && player.Name.IndexOf(partialNameOrId, StringComparison.OrdinalIgnoreCase) >= 0 || player.Id == partialNameOrId)
                    yield return player;
            }
        }

        #endregion
    }
}
