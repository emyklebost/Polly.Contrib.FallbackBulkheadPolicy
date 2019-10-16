using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    internal static class AsyncFallbackBulkheadEngine
    {
        internal static async Task ImplementationAsync<TResult>(
            SemaphoreSlim maxParallelizationSemaphore,
            IReadOnlyCollection<int> maxQueueingActionsLimits,
            ConcurrentQueue<ActionContext<TResult>> queuedActions,
            FallbackAction<TResult> fallbackAction)
        {
            if (!maxParallelizationSemaphore.Wait(0))
            {
                return;
            }

            while (true)
            {
                var batch = default(ActionContext<TResult>[]);
                var actionCtx = default(ActionContext<TResult>);

                lock (queuedActions)
                {
                    var brokenLimits = maxQueueingActionsLimits.Where(x => queuedActions.Count >= x);
                    if (brokenLimits.Any())
                    {
                        batch = queuedActions.Dequeue(brokenLimits.First());
                    }
                    else if (!queuedActions.TryDequeue(out actionCtx))
                    {
                        actionCtx = null;
                    }
                }

                if (batch != null)
                {
                    await fallbackAction.ExecuteAsync(batch);
                }
                else if (actionCtx != null)
                {
                    await actionCtx.ExecuteAsync();
                }
                else
                {
                    break;
                }
            }

            maxParallelizationSemaphore.Release();
        }

        private static T[] Dequeue<T>(
            this ConcurrentQueue<T> queue,
            int count)
        {
            var batch = new T[count];
            for (int i = 0; i < count; ++i)
            {
                queue.TryDequeue(out var actionCtx);
                batch[i] = actionCtx;
            }

            return batch;
        }

        private static async Task ExecuteAsync<TResult>(
            this FallbackAction<TResult> fallbackAction,
            ActionContext<TResult>[] batch)
        {
            try
            {
                await fallbackAction(batch);
            }
            catch (Exception ex)
            {
                foreach (var actionCtx in batch)
                {
                    actionCtx.TaskCompletionSource.TrySetException(ex);
                }
            }
        }
    }
}
