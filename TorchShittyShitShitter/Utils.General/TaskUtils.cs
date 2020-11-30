using System;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Utils.General
{
    internal static class TaskUtils
    {
        public static async void Forget(this Task self, ILogger logger)
        {
            await self.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    logger.Error(t.Exception);
                }
            });
        }

        public static ThreadPoolTask MoveToThreadPool()
        {
            return new ThreadPoolTask();
        }

        public static Task StartAsync(this CancellationTokenSource self, Action<CancellationToken> f)
        {
            return Task.Factory.StartNew(() => f(self.Token));
        }
    }
}