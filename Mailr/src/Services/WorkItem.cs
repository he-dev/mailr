using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mailr.Services
{
    public class WorkItem
    {
        public Func<CancellationToken, Task> Task { get; set; } = _ => System.Threading.Tasks.Task.CompletedTask;

        public string? Tag { get; set; }

        public static WorkItem Empty => new WorkItem { Tag = "This work-item doesn't do anything." };
    }
}