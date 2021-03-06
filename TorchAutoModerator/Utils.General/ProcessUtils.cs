using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Utils.General
{
    public static class ProcessUtils
    {
        // this is introduced in .NET 5.0 but we don't have it yet!!!! >:(
        public static Task<int> WaitForExitAsync(this Process self)
        {
            // https://stackoverflow.com/questions/19658838
            var taskSource = new TaskCompletionSource<int>();
            self.EnableRaisingEvents = true;
            self.Exited += (_, __) => taskSource.TrySetResult(self.ExitCode);
            return taskSource.Task;
        }
    }
}