using Torch.Commands;
using Torch.Commands.Permissions;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchShittyShitShitter
{
    [Category(Cmd_Category)]
    public sealed class ShittyShitShitterCommandModule : CommandModule
    {
        const string Cmd_Category = "lgb";

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

        [Command("cleargps", "Clear all custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ClearCustomGps() => this.CatchAndReport(() =>
        {
            Plugin.CleanAllCustomGps();
        });
    }
}