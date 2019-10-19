using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Polly.Utilities;
using Xunit;

namespace Polly.Contrib.FallbackBulkheadPolicy.Specs
{
    public class AsyncFallbackBulkheadTResultSpecs
    {
        #region Configuration

        [Fact]
        public void Should_throw_when_maxparallelization_less_or_equal_to_zero()
        {
            Action policy = () => AsyncFallbackBulkheadPolicy
                .Create<int>(0);

            policy.ShouldThrow<ArgumentOutOfRangeException>().And
                .ParamName.Should().Be("maxParallelization");
        }

        [Fact]
        public void Should_throw_when_maxQueuingActionsLimits_less_than_zero()
        {
            Action policy = () => AsyncFallbackBulkheadPolicy
                .Create<int>(1, _ => TaskHelper.EmptyTask, -1);

            policy.ShouldThrow<ArgumentOutOfRangeException>().And
                .ParamName.Should().Be("maxQueuingActionsLimits");
        }

        [Fact]
        public void Should_throw_when_fallbackActionAsync_is_null()
        {
            Action policy = () => AsyncFallbackBulkheadPolicy
                .Create<int>(1, null, 1);

            policy.ShouldThrow<ArgumentNullException>().And
                .ParamName.Should().Be("fallbackActionAsync");
        }

        #endregion

        #region fallbackAction

        [Theory]
        [InlineData(4, 100, 1000)]
        [InlineData(1, 100, 100)]
        [InlineData(1, 100, 10)]
        [InlineData(100, 100, 100)]
        public async Task Should_call_fallbackAction_on_exceeded_queueLimit(
            int maxParallelization,
            int maxQueuingActionsLimit,
            int numberOfActions)
        {
            const string argKey = "arg";
            var numberOfRegularCalls = 0;
            var numberOfFallbackCalls = 0;

            async Task<TArg> RegularActionAsync<TArg>(Context pollyContext)
            {
                await Task.Delay(10);
                Interlocked.Increment(ref numberOfRegularCalls);
                return (TArg)pollyContext[argKey];
            }

            async Task FallbackActionAsync<TArg>(
                IReadOnlyCollection<ActionContext<TArg>> actionContexts)
            {
                await Task.Delay(50);
                Interlocked.Increment(ref numberOfFallbackCalls);

                foreach (var actionCtx in actionContexts)
                {
                    var result = (TArg)actionCtx.PollyContext[argKey];
                    actionCtx.TaskCompletionSource.SetResult(result);
                }
            }

            using (var sut = AsyncFallbackBulkheadPolicy.Create<int>(
                maxParallelization,
                FallbackActionAsync,
                maxQueuingActionsLimit))
            {
                var tasks = new List<Task<int>>();

                for (int arg = 0; arg < numberOfActions; ++arg)
                {
                    var task = sut.ExecuteAsync(
                        RegularActionAsync<int>,
                        new Context { [argKey] = arg });

                    tasks.Add(task);
                }

                var expected = Enumerable.Range(0, numberOfActions);
                var results = await Task.WhenAll(tasks);
                Assert.Equal(expected, results);

                Assert.Equal(numberOfActions, numberOfRegularCalls + (numberOfFallbackCalls * maxQueuingActionsLimit));
            }
        }

        #endregion


        #region Bulkhead behaviour

        #endregion

    }
}
