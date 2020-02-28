using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Mailr.Services
{
    public interface IWorkItemQueue
    {
        void Enqueue(Func<CancellationToken, Task> job, string? tag = default);

        Task<WorkItem> DequeueAsync(CancellationToken cancellationToken);
    }
    
    public class WorkItemQueue : IWorkItemQueue
    {
        private readonly ConcurrentQueue<WorkItem> _workItemQueue = new ConcurrentQueue<WorkItem>();

        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        public void Enqueue(Func<CancellationToken, Task> job, string? tag = default)
        {
            _workItemQueue.Enqueue(new WorkItem { Job = job, Tag = tag });
            _signal.Release();
        }

        public async Task<WorkItem> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            return _workItemQueue.TryDequeue(out var workItem) ? workItem : WorkItem.Empty;
        }
    }
}