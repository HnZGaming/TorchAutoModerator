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

        public static Task MoveToThreadPool()
        {
            var taskSource = new TaskCompletionSource<byte>();

            try
            {
                ThreadPool.QueueUserWorkItem(_ => taskSource.SetResult(0));
            }
            catch (Exception e)
            {
                taskSource.SetException(e);
            }

            return taskSource.Task;
        }

        public static Task RunUntilCancelledAsync(this CancellationToken self, Func<CancellationToken, Task> f)
        {
            return Task.Factory.StartNew(async () =>
            {
                try
                {
                    await f(self);
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
            }, self);
        }

        public static async Task Delay(this CancellationToken self, TimeSpan timeSpan)
        {
            try
            {
                await Task.Delay(timeSpan, self);
            }
            catch (ObjectDisposedException)
            {
                throw new OperationCanceledException();
            }
        }
    }
}