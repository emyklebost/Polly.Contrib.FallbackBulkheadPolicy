using System.Collections.Generic;
using System.Threading.Tasks;

namespace Polly.Contrib.FallbackBulkheadPolicy
{
    public delegate Task FallbackAction<TResult>(IReadOnlyCollection<ActionContext<TResult>> actionContexts);
}
