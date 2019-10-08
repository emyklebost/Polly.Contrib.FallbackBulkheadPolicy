using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
   internal static class AsyncFallbackBulkheadEngine
    {
       internal static Task<TResult> ImplementationAsync<TResult>(
            ActionContext<TResult> actionCtx,
            FallbackAction<TResult> onBulkheadRejectedAsync,
            SemaphoreSlim maxParallelizationSemaphore,
            ref ConcurrentQueue<ActionContext<TResult>> queuedActions,
            int maxQueueingActions)
        {
            var batch = default(ConcurrentQueue<ActionContext<TResult>>);

            queuedActions.Enqueue(actionCtx);

            var initialValue = queuedActions;
            if (queuedActions.Count >= maxQueueingActions)
            {
                var newQueue = new ConcurrentQueue<ActionContext<TResult>>();
                batch = Interlocked.CompareExchange(ref queuedActions, newQueue, initialValue);
            }

            if (batch == initialValue && batch.Any())
            {
                _ = ExecuteFallbackAsync(batch, onBulkheadRejectedAsync);
            }
            else if (maxParallelizationSemaphore.Wait(0))
            {
                _ = ExecuteAsync(queuedActions, maxParallelizationSemaphore);
            }

            return actionCtx.TaskCompletionSource.Task;
        }

        private static async Task ExecuteFallbackAsync<TResult>(
            ConcurrentQueue<ActionContext<TResult>> batch,
            FallbackAction<TResult> onBulkheadRejectedAsync)
        {
            try
            {
                await onBulkheadRejectedAsync(batch.ToList());
            }
            catch (Exception ex)
            {
                foreach (var actionCtx in batch)
                {
                    actionCtx.TaskCompletionSource.TrySetException(ex);
                }
            }
        }

        private static async Task ExecuteAsync<TResult>(
            ConcurrentQueue<ActionContext<TResult>> queuedActions,
            SemaphoreSlim maxParallelizationSemaphore)
        {
            while (queuedActions.TryDequeue(out var actionCtx))
            {
                await actionCtx.ExecuteAsync();
            }

            maxParallelizationSemaphore.Release();

            //// This check is needed to handle the possible race-condition where an
            //// action is enqueued between the last _queuedActions.TryDequeue(...)
            //// and _maxParallelizationSemaphore.Release(), AND the thread who did
            //// the enqueuing was unable to enter the _maxParallelizationSemaphore.
            if (!queuedActions.IsEmpty && maxParallelizationSemaphore.Wait(0))
            {
                await ExecuteAsync(queuedActions, maxParallelizationSemaphore);
            }
        }
    }
}
