using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Mailr.Services
{
    public interface IWorkItemQueue
    {
        void Enqueue(Func<CancellationToken, Task> workItem);

        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }

    public class WorkItemQueue : IWorkItemQueue
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _workItemQueue = new ConcurrentQueue<Func<CancellationToken, Task>>();

        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        public void Enqueue(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null) { throw new ArgumentNullException(nameof(workItem)); }

            _workItemQueue.Enqueue(workItem);
            _signal.Release();
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _workItemQueue.TryDequeue(out var workItem);

            return workItem;
        }
    }

    public class WorkItemQueueService : IHostedService
    {
        private readonly IWorkItemQueue _workItemQueue;

        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();

        private Task _backgroundTask;

        public WorkItemQueueService(IWorkItemQueue workItemQueue)
        {
            _workItemQueue = workItemQueue;
        }

        #region IHostedService

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // ReSharper disable once MethodSupportsCancellation - this task is not supposted to be cancelled until shutdown
            _backgroundTask = Task.Run(BackgroundProceessing);

            return Task.CompletedTask;
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _shutdown.Cancel();
            return Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        #endregion

        public void Enqueue(Func<CancellationToken, Task> workItem)
        {
            _workItemQueue.Enqueue(workItem);
        }

        private async Task BackgroundProceessing()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var workItem = await _workItemQueue.DequeueAsync(_shutdown.Token);

                try
                {
                    await workItem(_shutdown.Token);
                }
                catch (Exception)
                {
                    Debug.Fail("Work item should handle its own exceptions.");
                }
            }
        }
    }
}
