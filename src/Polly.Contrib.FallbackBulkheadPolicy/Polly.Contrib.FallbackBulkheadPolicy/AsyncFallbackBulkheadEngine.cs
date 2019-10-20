using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    internal static class AsyncFallbackBulkheadEngine
    {
        internal static async Task ImplementationAsync<TResult>(
            SemaphoreSlim maxParallelizationSemaphore,
            int[] maxQueueingActionsLimits,
            ConcurrentQueue<ActionContext<TResult>> queuedActions,
            FallbackAction<TResult> fallbackAction)
        {
            do
            {
                if (!maxParallelizationSemaphore.Wait(0))
                {
                    return;
                }

                await Task
                    .Run(() => queuedActions.ExecuteAsync(maxQueueingActionsLimits, fallbackAction))
                    .ConfigureAwait(false);

                maxParallelizationSemaphore.Release();

            // This extra check is needed to prevent the possible race-condition where an action has
            // been enqueued in an empty queue while the Semaphore is being released, leading to a
            // situation where the last action is stuck in the queue until a new action is enqueued.
            } while (!queuedActions.IsEmpty);
        }

        private static async Task ExecuteAsync<TResult>(
            this ConcurrentQueue<ActionContext<TResult>> queuedActions,
            int[] maxQueueingActionsLimits,
            FallbackAction<TResult> fallbackAction)
        {
            while (true)
            {
                var actionCtxBatch = default(ActionContext<TResult>[]);
                var actionCtx = default(ActionContext<TResult>);

                lock (queuedActions)
                {
                    var exceededLimits = maxQueueingActionsLimits.Where(x => x <= queuedActions.Count);
                    if (exceededLimits.Any())
                    {
                        actionCtxBatch = queuedActions.Dequeue(exceededLimits.First());
                    }
                    else if (queuedActions.TryDequeue(out actionCtx))
                    {
                    }
                    else
                    {
                        break;
                    }
                }

                if (actionCtxBatch != null)
                {
                    await actionCtxBatch.ExecuteAsync(fallbackAction);
                }
                else
                {
                    await actionCtx.ExecuteAsync();
                }
            }
        }

        private static T[] Dequeue<T>(
            this ConcurrentQueue<T> queue,
            int count)
        {
            var batch = new T[count];
            for (int i = 0; i < count; ++i)
            {
                queue.TryDequeue(out batch[i]);
            }

            return batch;
        }

        private static async Task ExecuteAsync<TResult>(
            this ActionContext<TResult>[] actionCtxBatch,
            FallbackAction<TResult> fallbackAction)
        {
            try
            {
                await fallbackAction(actionCtxBatch);
            }
            catch (Exception ex)
            {
                foreach (var actionCtx in actionCtxBatch)
                {
                    actionCtx.TaskCompletionSource.TrySetException(ex);
                }
            }
        }
    }
}
