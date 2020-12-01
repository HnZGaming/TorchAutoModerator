using Torch.API;
using Torch.Managers;
using Torch.Managers.PatchManager;
using Utils.Torch;

namespace TorchShittyShitShitter
{
    public sealed class ShittyShitShitterManager : Manager
    {
#pragma warning disable 649
        [Dependency(Ordered = false)]
        readonly PatchManager _patchMgr;
#pragma warning restore 649

        PatchContext _patchContext;

        public ShittyShitShitterManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        public override void Attach()
        {
            base.Attach();

            _patchContext = _patchMgr.AcquireContext();
            GameLoopObserver.Patch(_patchContext);
        }

        public override void Detach()
        {
            base.Detach();

            _patchMgr.FreeContext(_patchContext);
        }
    }
}