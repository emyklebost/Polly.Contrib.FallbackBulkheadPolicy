using Polly.Bulkhead;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    /// <summary>
    /// Defines properties and methods common to all bulkhead policies generic-typed for executions returning results of type <typeparamref name="TResult"/>.
    /// </summary>
    public interface IFallbackBulkheadPolicy<TResult> : IBulkheadPolicy
    {
    }
}
