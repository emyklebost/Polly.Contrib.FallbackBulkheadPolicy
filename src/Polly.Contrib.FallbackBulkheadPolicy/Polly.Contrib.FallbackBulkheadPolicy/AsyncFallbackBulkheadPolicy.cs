using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
            IEnumerable<int> maxQueueingActionsLimits)
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
        public void Dispose() => _maxParallelizationSemaphore.Dispose();

        /// <inheritdoc/>
        protected override Task<TResult> ImplementationAsync(
            Func<Context, CancellationToken, Task<TResult>> action,
            Context context,
            CancellationToken cancellationToken,
            bool continueOnCapturedContext)
        {
            var actionCtx = new ActionContext<TResult>(action, context, cancellationToken);
            _queuedActions.Enqueue(actionCtx);

            _ = AsyncFallbackBulkheadEngine.ImplementationAsync(
                _maxParallelizationSemaphore,
                _maxQueueingActionsLimits,
                _queuedActions,
                _fallbackAction);

            return actionCtx.TaskCompletionSource.Task;
        }
    }
}
