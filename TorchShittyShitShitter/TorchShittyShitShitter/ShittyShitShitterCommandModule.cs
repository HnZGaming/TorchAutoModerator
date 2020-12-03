using System;
using System.Linq;
using System.Text;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using Utils.Torch;
using VRage.Game.ModAPI;
using VRageMath;

namespace TorchShittyShitShitter
{
    [Category("lg")]
    public sealed class ShittyShitShitterCommandModule : CommandModule
    {
        ShittyShitShitterPlugin Plugin => (ShittyShitShitterPlugin) Context.Plugin;

        [Command("on", "Enable broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void EnableBroadcasting() => this.CatchAndReport(() =>
        {
            Plugin.Enabled = true;
        });

        [Command("off", "Disable broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void DisableBroadcasting() => this.CatchAndReport(() =>
        {
            Plugin.Enabled = false;
        });

        [Command("mspf", "Get or set the current ms/f threshold per online member.")]
        [Permission(MyPromoteLevel.Admin)]
        public void MspfThreshold() => this.CatchAndReport(() =>
        {
            if (!Context.Args.Any())
            {
                var currentThreshold = Plugin.MspfThreshold;
                Context.Respond($"{currentThreshold:0.000}mspf per online member");
                return;
            }

            var arg = Context.Args[0];
            if (!double.TryParse(arg, out var newThreshold))
            {
                Context.Respond($"Failed to parse threshold value: {arg}", Color.Red);
                return;
            }

            Plugin.MspfThreshold = newThreshold;
            Context.Respond($"Set new threshold: {newThreshold:0.000}mspf per online member");
        });

        [Command("ss", "Get or set the current sim speed threshold.")]
        [Permission(MyPromoteLevel.Admin)]
        public void SimSpeedThreshold() => this.CatchAndReport(() =>
        {
            if (!Context.Args.Any())
            {
                var value = Plugin.SimSpeedThreshold;
                Context.Respond($"{value:0.00}ss");
                return;
            }

            var arg = Context.Args[0];
            if (!double.TryParse(arg, out var newThreshold))
            {
                Context.Respond($"Failed to parse threshold value: {arg}", Color.Red);
                return;
            }

            Plugin.SimSpeedThreshold = newThreshold;
            Context.Respond($"Set new threshold: {newThreshold:0.000}ss");
        });

        [Command("clear", "Clear all custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ClearCustomGps() => this.CatchAndReport(() =>
        {
            Plugin.CleanAllCustomGps();
        });

        [Command("show", "Show the list of custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowCustomGpsEntities() => this.CatchAndReport(() =>
        {
            var msgBuilder = new StringBuilder();
            foreach (var (identityId, gps) in Plugin.GetAllCustomGpsEntities())
            {
                var playerName =
                    MySession.Static.Players.TryGetPlayerId(identityId, out var playerId) &&
                    MySession.Static.Players.TryGetPlayerById(playerId, out var player)
                        ? player.DisplayName
                        : "<unknown>";

                var txt = $"{playerName} ({identityId}): {gps.EntityId} \"{gps.Name}\"";
                msgBuilder.AppendLine(txt);
            }

            Context.Respond($"Custom GPS entities: \n{msgBuilder}");
        });

        [Command("mute", "Mute broadcasting.")]
        [Permission(MyPromoteLevel.None)]
        public void MuteBroadcastsToPlayer() => this.CatchAndReport(() =>
        {
            if (Context.Player == null)
            {
                Context.Respond("Can be called by a player only", Color.Red);
                return;
            }

            Plugin.MutePlayer(Context.Player.SteamUserId);
        });

        [Command("unmute", "Unmute broadcasting.")]
        [Permission(MyPromoteLevel.None)]
        public void UnmuteBroadcastsToPlayer() => this.CatchAndReport(() =>
        {
            if (Context.Player == null)
            {
                Context.Respond("Can be called by a player only", Color.Red);
                return;
            }

            Plugin.UnmutePlayer(Context.Player.SteamUserId);
        });
    }
}