using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
            var actionCtx = new ActionContext<TResult>(action, context, cancellationToken);

            _queuedActions.Enqueue(actionCtx);

            var batch = default(ConcurrentQueue<ActionContext<TResult>>);
            var initialValue = _queuedActions;

            if (_queuedActions.Count >= _maxQueueingActions)
            {
                var newQueue = new ConcurrentQueue<ActionContext<TResult>>();
                batch = Interlocked.CompareExchange(ref _queuedActions, newQueue, initialValue);
            }

            if (batch == initialValue && batch.Any())
            {
                _ = ExecuteFallbackAsync(batch.ToList());
            }
            else if (_maxParallelizationSemaphore.Wait(0))
            {
                _ = ExecuteAsync();
            }

            return actionCtx.TaskCompletionSource.Task;
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

        private async Task ExecuteAsync()
        {
            while (_queuedActions.TryDequeue(out var actionCtx))
            {
                await actionCtx.ExecuteAsync();
            }

            _maxParallelizationSemaphore.Release();

            //// This check is needed to handle the possible race-condition where an
            //// action is enqueued between the last _queuedActions.TryDequeue(...)
            //// and _maxParallelizationSemaphore.Release(), AND the thread who did
            //// the enqueuing was unable to enter the _maxParallelizationSemaphore.
            if (!_queuedActions.IsEmpty && _maxParallelizationSemaphore.Wait(0))
            {
                await ExecuteAsync();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _maxParallelizationSemaphore.Dispose();
        }
    }
}
