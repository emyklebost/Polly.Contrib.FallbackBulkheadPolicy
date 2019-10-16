using System;
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

        #region onBulkheadRejected delegate

        [Fact]
        public void Should_call_onBulkheadRejected_with_passed_context()
        {
        }

        #endregion


        #region Bulkhead behaviour

        #endregion

    }
}
