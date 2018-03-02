using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    /// <summary>
    /// It will handle the connection with the Azure Topic subscription and passing the work to the <see cref="ISymbolsImporter"></see>.
    /// </summary>
    public interface ISymbolProcessor
    {
        /// <summary>
        /// Registers receivers for the topic subscription.
        /// </summary>
        /// <param name="token">The token that on cancelation will stop all the receivers. </param>
        /// <returns></returns>
        IEnumerable<Task> RegisterReceivers(CancellationToken token);

        /// <summary>
        /// Will increase / decrease the count of the receivers based on the state of the subscription.
        /// </summary>
        /// <param name="token">The token that will terminate the watcher.</param>
        /// <param name="millisecondsCallback">The milliseconds for the callback call.</param>
        void RegisterSubscriptionWatcherCallback(CancellationToken token, int millisecondsCallback);
    }
}
