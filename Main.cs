using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System.Collections;
using UnityEngine;

namespace ArenaPlus
{
    public class ArenaPlugin : RocketPlugin
    {
        public static string PluginVersion = "1.0";

        private HashSet<CSteamID> deadPlayers = new HashSet<CSteamID>();
        private Dictionary<CSteamID, Vector3> deathPositions = new Dictionary<CSteamID, Vector3>();

        protected override void Load()
        {
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            UnturnedPlayerEvents.OnPlayerRevive += OnPlayerRevive;
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
            ItemManager.onTakeItemRequested += OnTakeItemRequested;
            Level.onLevelLoaded += OnLevelLoaded;
            LevelManager.onArenaMessageUpdated += OnArenaMessageUpdated;

            Debug.Log("ArenaPlus v" + PluginVersion + " loaded successfully.");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            UnturnedPlayerEvents.OnPlayerRevive -= OnPlayerRevive;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
            ItemManager.onTakeItemRequested -= OnTakeItemRequested;
            Level.onLevelLoaded -= OnLevelLoaded;
            LevelManager.onArenaMessageUpdated -= OnArenaMessageUpdated;

            Debug.Log("ArenaPlus unloaded.");
        }

        private void OnArenaMessageUpdated(EArenaMessage message)
        {
            if (message == EArenaMessage.WIN || message == EArenaMessage.LOSE || message == EArenaMessage.INTERMISSION)
            {
                UnturnedChat.Say("=== Round ended, cleaning up spectators... ===", Color.yellow);
                StartCoroutine(DelayedResetAll());
            }
            else if (message == EArenaMessage.PLAY)
            {
                UnturnedChat.Say("=== The fight begins! Good luck. ===", Color.green);
            }
        }

        private IEnumerator DelayedResetAll()
        {
            yield return new WaitForSeconds(0.3f);

            List<CSteamID> playersToReset = new List<CSteamID>(deadPlayers);
            foreach (CSteamID id in playersToReset)
            {
                UnturnedPlayer p = UnturnedPlayer.FromCSteamID(id);
                if (p != null)
                {
                    p.VanishMode = false;
                    p.GodMode = false;

                    if (p.Player != null && p.Player.look != null)
                        p.Player.look.sendFreecamAllowed(false);

                    p.Suicide();
                }
            }
            deadPlayers.Clear();
            deathPositions.Clear();
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer)
        {
            if (LevelManager.arenaState == EArenaState.PLAY)
            {
                if (!deadPlayers.Contains(player.CSteamID))
                {
                    deadPlayers.Add(player.CSteamID);
                    deathPositions[player.CSteamID] = player.Position;
                }

                UnturnedPlayer killer = UnturnedPlayer.FromCSteamID(murderer);
                if (killer != null && killer.Id != player.Id)
                {
                    UnturnedChat.Say(player.CharacterName + " was eliminated by " + killer.CharacterName + "!", Color.red);
                }
            }

            StartCoroutine(ForceRespawn(player));
        }

        private IEnumerator ForceRespawn(UnturnedPlayer player)
        {
            yield return new WaitForSeconds(0.4f);
            if (player != null && player.Player != null && player.Player.life != null)
            {
                player.Player.life.sendRevive();
            }
        }

        private void OnPlayerRevive(UnturnedPlayer player, Vector3 position, byte angle)
        {
            if (deadPlayers.Contains(player.CSteamID) && LevelManager.arenaState == EArenaState.PLAY)
            {
                StartCoroutine(InitializeSpectator(player));
            }
            else
            {
                ResetPlayerState(player);

                if (LevelManager.arenaState != EArenaState.PLAY)
                {
                    UnturnedChat.Say(player, "Server using ArenaPlus v" + PluginVersion, Color.green);
                    UnturnedChat.Say(player, "Welcome to the Arena!", Color.green);
                }
            }
        }

        private IEnumerator InitializeSpectator(UnturnedPlayer player)
        {
            yield return new WaitForSeconds(0.2f);
            if (player != null && player.Player != null)
            {
                player.VanishMode = true;
                player.GodMode = true;
                player.Player.look.sendFreecamAllowed(true);

                Vector3 lastPos;
                if (deathPositions.TryGetValue(player.CSteamID, out lastPos))
                {
                    player.Teleport(lastPos + (Vector3.up * 3), 0);
                }

                UnturnedChat.Say(player, "SPECTATOR MODE: Press F1 for Freecam.", Color.cyan);
            }
        }

        private void OnTakeItemRequested(Player player, byte x, byte y, uint instanceID, byte to_x, byte to_y, byte rot, byte page, ItemData itemData, ref bool shouldAllow)
        {
            if (LevelManager.arenaState != EArenaState.PLAY || deadPlayers.Contains(player.channel.owner.playerID.steamID))
            {
                shouldAllow = false;
            }
        }

        private void OnLevelLoaded(int level)
        {
            deadPlayers.Clear();
            deathPositions.Clear();
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            deadPlayers.Remove(player.CSteamID);
            deathPositions.Remove(player.CSteamID);
        }

        private void ResetPlayerState(UnturnedPlayer player)
        {
            if (player == null) return;

            deadPlayers.Remove(player.CSteamID);
            deathPositions.Remove(player.CSteamID);

            player.VanishMode = false;
            player.GodMode = false;

            if (player.Player != null && player.Player.look != null)
            {
                player.Player.look.sendFreecamAllowed(false);
            }
        }
    }
}
