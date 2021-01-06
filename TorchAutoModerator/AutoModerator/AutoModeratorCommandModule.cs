using System.Linq;
using System.Text;
using Torch.Commands;
using Torch.Commands.Permissions;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;
using VRageMath;

namespace AutoModerator
{
    [Category("lg")]
    public sealed class AutoModeratorCommandModule : CommandModule
    {
        AutoModeratorPlugin Plugin => (AutoModeratorPlugin) Context.Plugin;

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

        [Command("admins-only", "Get or set the current \"admins only\" value.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AdminsOnly() => this.CatchAndReport(() =>
        {
            if (!Context.Args.Any())
            {
                var value = Plugin.AdminsOnly;
                Context.Respond($"{value}");
                return;
            }

            var arg = Context.Args[0];
            if (!bool.TryParse(arg, out var newValue))
            {
                Context.Respond($"Failed to parse bool value: {arg}", Color.Red);
                return;
            }

            Plugin.AdminsOnly = newValue;
            Context.Respond($"Set admins-only: {newValue}");
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
            var gpss = Plugin.GetAllCustomGpsEntities();

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

        [Command("unmute_all", "Force every player to unmute broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void UnmuteBroadcastsToAll() => this.CatchAndReport(() =>
        {
            Plugin.UnmuteAll();
        });

        [Command("profile", "Profile online faction members.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ProfileOnlineFactionMembers() => this.CatchAndReport(async () =>
        {
            var profileTime = 5d;
            var top = 10;

            foreach (var arg in Context.Args)
            {
                if (CommandOption.TryGetOption(arg, out var option))
                {
                    if (option.TryParseDouble("time", out profileTime) ||
                        option.TryParseInt("top", out top))
                    {
                        continue;
                    }

                    Context.Respond($"Unknown option: {arg}", Color.Red);
                    return;
                }
            }

            Context.Respond($"Profiling (profile time: {profileTime:0.0}s, top: {top} factions)...");

            var result = await Plugin.ProfileFactionMembers(profileTime.Seconds());
            result = result.OrderByDescending(r => r.Mspf).Take(top);

            if (!result.Any())
            {
                Context.Respond("No factions found");
                return;
            }

            var msgBuilder = new StringBuilder();
            foreach (var (faction, count, mspf) in result)
            {
                msgBuilder.AppendLine($"> {faction.Tag}: {mspf:0.00}mspf ({count} online players)");
            }

            Context.Respond($"Finished profiling:\n{msgBuilder}");
        });

        [Command("scan", "Scan laggy grids.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ScanLaggyGrids() => this.CatchAndReport(async () =>
        {
            var profileTime = 5d;
            var broadcast = false;
            var buffered = false;

            foreach (var arg in Context.Args)
            {
                if (CommandOption.TryGetOption(arg, out var option))
                {
                    if (option.TryParseDouble("time", out profileTime) ||
                        option.IsParameterless("broadcast", out broadcast) ||
                        option.IsParameterless("buffered", out buffered))
                    {
                        continue;
                    }

                    Context.Respond($"Unknown option: {arg}", Color.Red);
                    return;
                }
            }

            Context.Respond($"Scanning (profile time: {profileTime:0.0}s, buffered: {buffered}, broadcast: {broadcast})...");

            var reports = await Plugin.FindLaggyGrids(profileTime.Seconds(), buffered);

            if (!reports.Any())
            {
                Context.Respond("No laggy grids found");
                return;
            }

            var msgBuilder = new StringBuilder();
            foreach (var report in reports)
            {
                msgBuilder.AppendLine($"> {report}");
            }

            Context.Respond($"Finished scanning:\n{msgBuilder}");

            if (broadcast)
            {
                await Plugin.BroadcastLaggyGrids(reports);
                Context.Respond("Broadcasting finished");
            }
        });
    }
}