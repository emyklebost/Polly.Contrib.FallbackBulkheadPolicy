using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    public class BulkheadFallbackPolicy<TResult> : AsyncPolicy<TResult>
    {
        private readonly int _maxQueueingActions;
        private readonly SemaphoreSlim _maxParallelizationSemaphore;
        private readonly FallbackAction<TResult> _fallbackAction;
        private ConcurrentQueue<ActionContext<TResult>> _queuedActions;

        /// <summary>
        /// Initializes a new instance of the <see cref="BulkheadFallbackPolicy{TResult}"/> class, which limits the maximum
        /// concurrency of actions executed through the policy.
        /// Imposing a maximum concurrency limits the potential of governed actions, when faulting, to bring down the system.
        /// <para>When an execution would cause the number of actions executing concurrently through the policy to exceed
        /// <paramref name="maxParallelization"/>, the policy allows a further <paramref name="maxQueuingActions"/> executions to queue,
        /// waiting for a concurrent execution slot. When an execution would cause the number of queuing actions to exceed
        /// <paramref name="maxQueuingActions"/>, <paramref name="fallbackAction"/> is called asynchronously with a list of the
        /// queued actions in FIFO order and length >= <paramref name="maxQueuingActions"/>.</para>
        /// </summary>
        /// <param name="maxParallelization">The maximum number of concurrent actions that may be executing through the policy.</param>
        /// <param name="maxQueuingActions">The maxmimum number of actions that may be queuing, waiting for an execution slot.</param>
        /// <param name="fallbackAction">An action to call asynchronously, if the bulkhead rejects execution due to oversubscription.</param>
        /// <returns>The policy instance.</returns>
        public BulkheadFallbackPolicy(
            int maxParallelization,
            int maxQueuingActions,
            FallbackAction<TResult> fallbackAction)
        {
            _maxQueueingActions = maxQueuingActions;
            _maxParallelizationSemaphore = new SemaphoreSlim(maxParallelization, maxParallelization);
            _fallbackAction = fallbackAction;
            _queuedActions = new ConcurrentQueue<ActionContext<TResult>>();
        }

        protected override Task<TResult> ImplementationAsync(
            Func<Context, CancellationToken, Task<TResult>> action,
            Context context,
            CancellationToken cancellationToken,
            bool continueOnCapturedContext)
        {
            var actionCtx = new ActionContext<TResult>(action, context, cancellationToken);
            var batch = default(ConcurrentQueue<ActionContext<TResult>>);

            _queuedActions.Enqueue(actionCtx);

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
                foreach (var ctx in batch)
                {
                    ctx.TaskCompletionSource.TrySetException(ex);
                }
            }
        }

        private async Task ExecuteAsync()
        {
            while (_queuedActions.TryDequeue(out var ctx))
            {
                await ctx.ExecuteAsync();
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
    }
}
