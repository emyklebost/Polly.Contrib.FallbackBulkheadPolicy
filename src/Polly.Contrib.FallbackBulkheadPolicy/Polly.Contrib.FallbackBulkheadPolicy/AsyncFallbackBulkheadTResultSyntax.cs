using System;
using System.Linq;
using Polly.Utilities;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    public partial class AsyncFallbackBulkheadPolicy
    {
        /// <summary>
        /// Builds a bulkhead isolation <see cref="AsyncPolicy{TResult}" />, which limits the maximum concurrency of actions executed through the policy.  Imposing a maximum concurrency limits the potential of governed actions, when faulting, to bring down the system.
        /// <para>When an execution would cause the number of actions executing concurrently through the policy to exceed <paramref name="maxParallelization" /> they will be queued and executed in-order.</para>
        /// </summary>
        /// <param name="maxParallelization">The maximum number of concurrent actions that may be executing through the policy.</param>
        /// <returns>The policy instance.</returns>
        public static AsyncFallbackBulkheadPolicy<TResult> Create<TResult>(int maxParallelization)
        {
            FallbackAction<TResult> doNothingAsync = _ => TaskHelper.EmptyTask;
            return Create(maxParallelization, doNothingAsync);
        }

        /// <summary>
        /// Builds a bulkhead isolation <see cref="AsyncPolicy{TResult}" />, which limits the maximum concurrency of actions executed through the policy.  Imposing a maximum concurrency limits the potential of governed actions, when faulting, to bring down the system.
        /// <para>When an execution would cause the number of actions executing concurrently through the policy to exceed <paramref name="maxParallelization" /> they will be queued and executed in-order.  When an execution would cause the number of queuing actions to exceed any of the <paramref name="maxQueuingActionsLimits" />, <paramref name="fallbackActionAsync" /> is called with a number of <see cref="ActionContext{TResult}" /> equal to the largest exceeded limit in <paramref name="maxQueuingActionsLimits" />.</para>
        /// </summary>
        /// <param name="maxParallelization">The maximum number of concurrent actions that may be executing through the policy.</param>
        /// <param name="maxQueuingActionsLimits">The maxmimum number of actions that may be queuing, waiting for an execution slot.</param>
        /// <param name="fallbackActionAsync">An action to call asynchronously, if the bulkhead rejects execution due to oversubscription.</param>
        /// <returns>The policy instance.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">maxParallelization;Value must be greater than zero.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">maxQueuingActionsLimits;Value must be greater than or equal to zero.</exception>
        /// <exception cref="System.ArgumentNullException">fallbackActionAsync</exception>
        public static AsyncFallbackBulkheadPolicy<TResult> Create<TResult>(int maxParallelization, FallbackAction<TResult> fallbackActionAsync, params int[] maxQueuingActionsLimits)
        {
            if (maxParallelization <= 0) throw new ArgumentOutOfRangeException(nameof(maxParallelization), "Value must be greater than zero.");
            if (maxQueuingActionsLimits.Any(x => x <= 0)) throw new ArgumentOutOfRangeException(nameof(maxQueuingActionsLimits), "Value must be greater than zero.");
            if (fallbackActionAsync == null) throw new ArgumentNullException(nameof(fallbackActionAsync));

            return new AsyncFallbackBulkheadPolicy<TResult>(
                maxParallelization,
                fallbackActionAsync,
                maxQueuingActionsLimits
                );
        }
    }
}