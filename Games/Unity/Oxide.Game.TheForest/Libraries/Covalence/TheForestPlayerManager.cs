﻿using System;
using System.Collections.Generic;
using System.Linq;

using ProtoBuf;
using Steamworks;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Game.TheForest.Libraries.Covalence
{
    /// <summary>
    /// Represents a generic player manager
    /// </summary>
    public class TheForestPlayerManager : IPlayerManager
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
        private struct PlayerRecord
        {
            public string Name;
            public ulong Id;
        }

        private readonly IDictionary<string, PlayerRecord> playerData;
        private readonly IDictionary<string, TheForestPlayer> allPlayers;
        private readonly IDictionary<string, TheForestPlayer> connectedPlayers;

        internal TheForestPlayerManager()
        {
            // Load player data
            Utility.DatafileToProto<Dictionary<string, PlayerRecord>>("oxide.covalence");
            playerData = ProtoStorage.Load<Dictionary<string, PlayerRecord>>("oxide.covalence") ?? new Dictionary<string, PlayerRecord>();
            allPlayers = new Dictionary<string, TheForestPlayer>();
            foreach (var pair in playerData) allPlayers.Add(pair.Key, new TheForestPlayer(pair.Value.Id, pair.Value.Name));
            connectedPlayers = new Dictionary<string, TheForestPlayer>();
        }

        private void NotifyPlayerJoin(BoltEntity entity)
        {
            var steamId = entity.source.RemoteEndPoint.SteamId.Id;
            var id = entity.source.RemoteEndPoint.SteamId.Id.ToString();
            var name = SteamFriends.GetFriendPersonaName(new CSteamID(entity.source.RemoteEndPoint.SteamId.Id));

            // Do they exist?
            PlayerRecord record;
            if (playerData.TryGetValue(id, out record))
            {
                // Update
                record.Name = SteamFriends.GetFriendPersonaName(new CSteamID(entity.source.RemoteEndPoint.SteamId.Id));
                playerData[id] = record;

                // Swap out Rust player
                allPlayers.Remove(id);
                allPlayers.Add(id, new TheForestPlayer(entity));
            }
            else
            {
                // Insert
                record = new PlayerRecord { Id = steamId, Name = name };
                playerData.Add(id, record);

                // Create Rust player
                allPlayers.Add(id, new TheForestPlayer(steamId, name));
            }

            // Save
            ProtoStorage.Save(playerData, "oxide.covalence");
        }

        internal void NotifyPlayerConnect(BoltEntity entity)
        {
            var id = entity.source.RemoteEndPoint.SteamId.Id;
            connectedPlayers[id.ToString()] = new TheForestPlayer(entity);
        }

        internal void NotifyPlayerDisconnect(BoltEntity entity) => connectedPlayers.Remove(entity.source.RemoteEndPoint.SteamId.Id.ToString());

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
            TheForestPlayer player;
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
