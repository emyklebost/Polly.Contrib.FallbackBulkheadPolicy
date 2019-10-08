using System;
using System.Threading;
using System.Threading.Tasks;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    public class ActionContext<TResult>
    {
        public ActionContext(
            Func<Context, CancellationToken, Task<TResult>> action,
            Context context,
            CancellationToken cancellationToken)
        {
            Action = action;
            PollyContext = context;
            CancellationToken = cancellationToken;
            TaskCompletionSource = new TaskCompletionSource<TResult>();
        }

        public Func<Context, CancellationToken, Task<TResult>> Action { get; }

        public Context PollyContext { get; }

        public CancellationToken CancellationToken { get; }

        public TaskCompletionSource<TResult> TaskCompletionSource { get; }

        public async Task ExecuteAsync()
        {
            try
            {
                var result = await Action(PollyContext, CancellationToken);
                TaskCompletionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                TaskCompletionSource.SetException(ex);
            }
        }
    }
}
