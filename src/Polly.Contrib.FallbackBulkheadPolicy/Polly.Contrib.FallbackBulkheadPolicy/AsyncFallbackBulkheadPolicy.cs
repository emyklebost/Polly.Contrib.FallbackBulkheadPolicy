using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    public partial class AsyncFallbackBulkheadPolicy
    {
    }

    /// <summary>
    /// A bulkhead-isolation policy which can be applied to delegates.
    /// </summary>
    public class AsyncFallbackBulkheadPolicy<TResult> : AsyncPolicy<TResult>, IFallbackBulkheadPolicy<TResult>
    {
        private readonly IReadOnlyCollection<int> _maxQueueingActionsLimits;
        private readonly SemaphoreSlim _maxParallelizationSemaphore;
        private readonly FallbackAction<TResult> _fallbackAction;
        private readonly ConcurrentQueue<ActionContext<TResult>> _queuedActions;

        internal AsyncFallbackBulkheadPolicy(
            int maxParallelization,
            FallbackAction<TResult> fallbackAction,
            params int[] maxQueueingActionsLimits)
        {
            _maxQueueingActionsLimits = maxQueueingActionsLimits.OrderByDescending(x => x).ToArray();
            _maxParallelizationSemaphore = new SemaphoreSlim(maxParallelization, maxParallelization);
            _fallbackAction = fallbackAction;
            _queuedActions = new ConcurrentQueue<ActionContext<TResult>>();
        }

        /// <summary>
        /// Gets the number of slots currently available for executing actions through the bulkhead.
        /// </summary>
        public int BulkheadAvailableCount => _maxParallelizationSemaphore.CurrentCount;

        /// <summary>
        /// Gets the number of slots currently available for queuing actions for execution through the bulkhead.
        /// </summary>
        public int QueueAvailableCount => Math.Max(_maxQueueingActionsLimits.Min() - _queuedActions.Count, 0);

        /// <inheritdoc/>
        protected override Task<TResult> ImplementationAsync(
            Func<Context, CancellationToken, Task<TResult>> action,
            Context context,
            CancellationToken cancellationToken,
            bool continueOnCapturedContext)
        {
            var actionCtx = new ActionContext<TResult>(action, context, cancellationToken);
            _queuedActions.Enqueue(actionCtx);

            _ = HandleAsync();

            return actionCtx.TaskCompletionSource.Task;
        }

        private async Task HandleAsync()
        {
            if (!_maxParallelizationSemaphore.Wait(0))
            {
                return;
            }

            while (true)
            {
                var batch = default(List<ActionContext<TResult>>);
                var actionCtx = default(ActionContext<TResult>);

                lock (_queuedActions)
                {
                    var brokenLimits = _maxQueueingActionsLimits.Where(x => _queuedActions.Count >= x);
                    if (brokenLimits.Any())
                    {
                        batch = Dequeue(brokenLimits.First());
                    }
                    else if (!_queuedActions.TryDequeue(out actionCtx))
                    {
                        actionCtx = null;
                    }
                }

                if (batch != null)
                {
                    await ExecuteFallbackAsync(batch);
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

            _maxParallelizationSemaphore.Release();
        }

        private List<ActionContext<TResult>> Dequeue(int count)
        {
            var batch = new List<ActionContext<TResult>>(count);
            while (batch.Count != count)
            {
                _queuedActions.TryDequeue(out var actionCtx);
                batch.Add(actionCtx);
            }

            return batch;
        }

        private async Task ExecuteFallbackAsync(IReadOnlyCollection<ActionContext<TResult>> batch)
        {
            try
            {
                await _fallbackAction(batch);
            }
            catch (Exception ex)
            {
                foreach (var actionCtx in batch)
                {
                    actionCtx.TaskCompletionSource.TrySetException(ex);
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose() => _maxParallelizationSemaphore.Dispose();
    }
}
