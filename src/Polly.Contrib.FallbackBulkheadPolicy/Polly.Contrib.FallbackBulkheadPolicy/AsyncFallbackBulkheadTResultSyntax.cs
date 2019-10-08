﻿using Polly.Bulkhead;
using Polly.Utilities;
using System;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    public partial class AsyncFallbackBulkheadTResultSyntax
    {
        /// <summary>
        /// <para>Builds a bulkhead isolation <see cref="AsyncPolicy{TResult}"/>, which limits the maximum concurrency of actions executed through the policy.  Imposing a maximum concurrency limits the potential of governed actions, when faulting, to bring down the system.</para>
        /// <para>When an execution would cause the number of actions executing concurrently through the policy to exceed <paramref name="maxParallelization"/>, the action is not executed and a <see cref="BulkheadRejectedException"/> is thrown.</para>
        /// </summary>
        /// <param name="maxParallelization">The maximum number of concurrent actions that may be executing through the policy.</param>
        /// <returns>The policy instance.</returns>
        public static AsyncFallbackBulkheadPolicy<TResult> Create<TResult>(int maxParallelization)
        {
            FallbackAction<TResult> doNothingAsync = _ => TaskHelper.EmptyTask;
            return Create<TResult>(maxParallelization, 0, doNothingAsync);
        }

        /// <summary>
        /// <para>Builds a bulkhead isolation <see cref="AsyncPolicy{TResult}"/>, which limits the maximum concurrency of actions executed through the policy.  Imposing a maximum concurrency limits the potential of governed actions, when faulting, to bring down the system.</para>
        /// <para>When an execution would cause the number of actions executing concurrently through the policy to exceed <paramref name="maxParallelization"/>, the action is not executed and a <see cref="BulkheadRejectedException"/> is thrown.</para>
        /// </summary>
        /// <param name="maxParallelization">The maximum number of concurrent actions that may be executing through the policy.</param>
        /// <param name="onBulkheadRejectedAsync">An action to call asynchronously, if the bulkhead rejects execution due to oversubscription.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">maxParallelization;Value must be greater than zero.</exception>
        /// <exception cref="System.ArgumentNullException">onBulkheadRejectedAsync</exception>
        /// <returns>The policy instance.</returns>
        public static AsyncFallbackBulkheadPolicy<TResult> Create<TResult>(int maxParallelization, FallbackAction<TResult> onBulkheadRejectedAsync)
            => Create<TResult>(maxParallelization, 0, onBulkheadRejectedAsync);

        /// <summary>
        /// Builds a bulkhead isolation <see cref="AsyncPolicy{TResult}" />, which limits the maximum concurrency of actions executed through the policy.  Imposing a maximum concurrency limits the potential of governed actions, when faulting, to bring down the system.
        /// <para>When an execution would cause the number of actions executing concurrently through the policy to exceed <paramref name="maxParallelization" />, the policy allows a further <paramref name="maxQueuingActions" /> executions to queue, waiting for a concurrent execution slot.  When an execution would cause the number of queuing actions to exceed <paramref name="maxQueuingActions" />, a <see cref="BulkheadRejectedException" /> is thrown.</para>
        /// </summary>
        /// <param name="maxParallelization">The maximum number of concurrent actions that may be executing through the policy.</param>
        /// <param name="maxQueuingActions">The maxmimum number of actions that may be queuing, waiting for an execution slot.</param>
        /// <returns>The policy instance.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">maxParallelization;Value must be greater than zero.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">maxQueuingActions;Value must be greater than or equal to zero.</exception>
        /// <exception cref="System.ArgumentNullException">onBulkheadRejectedAsync</exception>
        public static AsyncFallbackBulkheadPolicy<TResult> Create<TResult>(int maxParallelization, int maxQueuingActions)
        {
            FallbackAction<TResult> doNothingAsync = _ => TaskHelper.EmptyTask;
            return Create<TResult>(maxParallelization, maxQueuingActions, doNothingAsync);
        }

        /// <summary>
        /// Builds a bulkhead isolation <see cref="AsyncPolicy{TResult}" />, which limits the maximum concurrency of actions executed through the policy.  Imposing a maximum concurrency limits the potential of governed actions, when faulting, to bring down the system.
        /// <para>When an execution would cause the number of actions executing concurrently through the policy to exceed <paramref name="maxParallelization" />, the policy allows a further <paramref name="maxQueuingActions" /> executions to queue, waiting for a concurrent execution slot.  When an execution would cause the number of queuing actions to exceed <paramref name="maxQueuingActions" />, a <see cref="BulkheadRejectedException" /> is thrown.</para>
        /// </summary>
        /// <param name="maxParallelization">The maximum number of concurrent actions that may be executing through the policy.</param>
        /// <param name="maxQueuingActions">The maxmimum number of actions that may be queuing, waiting for an execution slot.</param>
        /// <param name="onBulkheadRejectedAsync">An action to call asynchronously, if the bulkhead rejects execution due to oversubscription.</param>
        /// <returns>The policy instance.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">maxParallelization;Value must be greater than zero.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">maxQueuingActions;Value must be greater than or equal to zero.</exception>
        /// <exception cref="System.ArgumentNullException">onBulkheadRejectedAsync</exception>
        public static AsyncFallbackBulkheadPolicy<TResult> Create<TResult>(int maxParallelization, int maxQueuingActions, FallbackAction<TResult> onBulkheadRejectedAsync)
        {
            if (maxParallelization <= 0) throw new ArgumentOutOfRangeException(nameof(maxParallelization), "Value must be greater than zero.");
            if (maxQueuingActions < 0) throw new ArgumentOutOfRangeException(nameof(maxQueuingActions), "Value must be greater than or equal to zero.");
            if (onBulkheadRejectedAsync == null) throw new ArgumentNullException(nameof(onBulkheadRejectedAsync));

            var maxQueuingCompounded = maxQueuingActions <= int.MaxValue - maxParallelization
                ? maxQueuingActions + maxParallelization
                : int.MaxValue;

            return new AsyncFallbackBulkheadPolicy<TResult>(
                maxParallelization,
                maxQueuingCompounded,
                onBulkheadRejectedAsync
                );
        }
    }
}