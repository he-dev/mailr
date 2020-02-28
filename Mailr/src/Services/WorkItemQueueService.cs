﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Reusable.OmniLog;
using Reusable.OmniLog.Abstractions;
using Reusable.OmniLog.SemanticExtensions;

namespace Mailr.Services
{
    public class WorkItemQueueService : IHostedService
    {
        private readonly ILogger<WorkItemQueueService> _logger;
        private readonly IWorkItemQueue _workItemQueue;

        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();

        private Task? _backgroundTask;

        public WorkItemQueueService(ILogger<WorkItemQueueService> logger, IWorkItemQueue workItemQueue)
        {
            _logger = logger;
            _workItemQueue = workItemQueue;
        }

        #region IHostedService

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // ReSharper disable once MethodSupportsCancellation - this task is not supported to be cancelled until shutdown
            _backgroundTask = Task.Run(BackgroundProcessing);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _shutdown.Cancel();
            return Task.WhenAny(_backgroundTask ?? Task.CompletedTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        #endregion

        private async Task BackgroundProcessing()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var workItem = await _workItemQueue.DequeueAsync(_shutdown.Token);

                try
                {
                    await workItem.Job(_shutdown.Token);
                }
                catch (Exception inner)
                {
                    _logger.Log(Abstraction.Layer.Service().Routine("InvokeWorkItem").Faulted(inner), l => l.Message($"Tag: '{workItem.Tag}'."));
                }
            }
        }
    }
}