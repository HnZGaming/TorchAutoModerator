using System.Linq;
using System.Text;
using NLog;
using Torch.Commands;
using Torch.Commands.Permissions;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;
using VRageMath;

namespace AutoModerator
{
    [Category("lag")]
    public sealed partial class AutoModeratorCommandModule : CommandModule
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        AutoModeratorPlugin Plugin => (AutoModeratorPlugin)Context.Plugin;
        AutoModeratorConfig Config => Plugin.Config;
        Core.AutoModerator AutoModerator => Plugin.AutoModerator;

        [Command("configs", "Get or set config")]
        [Permission(MyPromoteLevel.None)]
        public void GetOrSetConfig() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config);
        });

        [Command("commands", "Get a list of commands")]
        [Permission(MyPromoteLevel.None)]
        public void ShowCommandList() => this.CatchAndReport(() =>
        {
            this.ShowCommands();
        });

        [Command("clear", "Clear all internal state.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Clear() => this.CatchAndReport(() =>
        {
            WarnIfIdle();
            AutoModerator.ClearCache();
            Context.Respond("cleared all internal state");
        });

        [Command("gpslist", "Show the list of GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowGpss() => this.CatchAndReport(() =>
        {
            WarnIfIdle();
            var gpss = AutoModerator.GetAllGpss();

            if (!gpss.Any())
            {
                Context.Respond("No GPS entities found");
                return;
            }

            var msgBuilder = new StringBuilder();
            foreach (var gps in gpss)
            {
                msgBuilder.AppendLine($"> {gps.Name}");
            }

            Context.Respond($"Custom GPS entities: \n{msgBuilder}");
        });

        [Command("gpsmute", "Mute broadcasting.")]
        [Permission(MyPromoteLevel.None)]
        public void MuteBroadcastsToPlayer() => this.CatchAndReport(() =>
        {
            WarnIfIdle();

            if (Context.Player == null)
            {
                Context.Respond("Can be called by a player only", Color.Red);
                return;
            }

            Config.AddMutedPlayer(Context.Player.SteamUserId);
            Context.Respond("Muted broadcasting. It may take some time to take effect.");
        });

        [Command("gpsunmute", "Unmute broadcasting.")]
        [Permission(MyPromoteLevel.None)]
        public void UnmuteBroadcastsToPlayer() => this.CatchAndReport(() =>
        {
            WarnIfIdle();

            if (Context.Player == null)
            {
                Context.Respond("Can be called by a player only", Color.Red);
                return;
            }

            Config.RemoveMutedPlayer(Context.Player.SteamUserId);
            Context.Respond("Unmuted broadcasting. It may take some time to take effect.");
        });

        [Command("gpsunmuteall", "Force every player to unmute broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void UnmuteBroadcastsToAll() => this.CatchAndReport(() =>
        {
            WarnIfIdle();
            Config.RemoveAllMutedPlayers();
        });

        [Command("clearwarning", "Clear quest HUD.")]
        [Permission(MyPromoteLevel.None)]
        public void ClearQuests() => this.CatchAndReport(() =>
        {
            WarnIfIdle();
            Context.Player.ThrowIfNull("must be called by a player");
            AutoModerator.ClearQuestForUser(Context.Player.IdentityId);
        });

        void WarnIfIdle()
        {
            if (!Plugin.Config.IsEnabled)
            {
                Context.Respond("WARNING Plugin not enabled; see 'Enable plugin' in config", Color.Yellow);
                return;
            }

            if (AutoModerator.IsIdle)
            {
                Context.Respond("WARNING Plugin idle; see 'First idle seconds' in config", Color.Yellow);
            }
        }
    }
}