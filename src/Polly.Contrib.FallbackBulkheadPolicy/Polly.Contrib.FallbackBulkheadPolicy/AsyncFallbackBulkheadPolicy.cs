using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    /// <summary>
    /// A bulkhead-isolation policy which can be applied to delegates.
    /// </summary>
    public class AsyncFallbackBulkheadPolicy<TResult> : AsyncPolicy<TResult>, IFallbackBulkheadPolicy
    {
        private readonly int _maxQueueingActions;
        private readonly SemaphoreSlim _maxParallelizationSemaphore;
        private readonly FallbackAction<TResult> _fallbackAction;
        private ConcurrentQueue<ActionContext<TResult>> _queuedActions;

        internal AsyncFallbackBulkheadPolicy(
            int maxParallelization,
            int maxQueuingActions,
            FallbackAction<TResult> fallbackAction)
        {
            _maxQueueingActions = maxQueuingActions;
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
        public int QueueAvailableCount => Math.Max(_maxQueueingActions - _queuedActions.Count, 0);

        /// <inheritdoc/>
        [DebuggerStepThrough]
        protected override Task<TResult> ImplementationAsync(
            Func<Context, CancellationToken, Task<TResult>> action,
            Context context,
            CancellationToken cancellationToken,
            bool continueOnCapturedContext)
        {
            return AsyncFallbackBulkheadEngine.ImplementationAsync(
                new ActionContext<TResult>(action, context, cancellationToken),
                _fallbackAction,
                _maxParallelizationSemaphore,
                ref _queuedActions,
                _maxQueueingActions);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _maxParallelizationSemaphore.Dispose();
        }
    }
}
