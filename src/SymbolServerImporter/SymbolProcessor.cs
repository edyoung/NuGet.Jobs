using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace NuGet.Services.SymbolsImporter
{
    public class SymbolProcessor
    {
        ISymbolsImporter _symbolImporter;
        SymbolPackageMessageHandler _messageHandler;
        IBrokeredMessageSerializer<SymbolPackageMessage> _messageSerializer;
        private readonly ILogger<SymbolProcessor> _logger;

        int _dop;
        List<SubscriptionProcessor<SymbolPackageMessage>> _serviceBusSubscriptionClients = new List<ServiceBus.SubscriptionProcessor<SymbolPackageMessage>>();
        ILoggerFactory _loggerFactory;

        string topicConnectionString = "Endpoint=sb://cmanu-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=qIvovHaqzhJ2zMdJoTEkFaZEqBimf0Bg2TZ+WdYJ2+I=";
        string topicPath = "topic1";
        string subscriptionName = "sub1";
        SubscriptionDescription _subscriptionDescription;

        public SymbolProcessor(ISymbolsImporter symbolImporter, int DOP, ILoggerFactory loggerFactory)
        {
            _symbolImporter = symbolImporter ?? throw new ArgumentNullException(nameof(symbolImporter));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<SymbolProcessor>();
            _dop = DOP;
            _messageHandler = new SymbolPackageMessageHandler(_symbolImporter, loggerFactory.CreateLogger<SymbolPackageMessageHandler>());
            _messageSerializer = new SymbolPackageBrokerMessageSerializer();
            var nsmgr = NamespaceManager.CreateFromConnectionString(topicConnectionString);
            _subscriptionDescription = nsmgr.GetSubscription(topicPath, subscriptionName);

            //for (int i =0; i< _dop; i++)
            //{
            //    ISubscriptionClient c = new SubscriptionClientWrapper(topicConnectionString, topicPath, subscriptionName);
            //    _serviceBusSubscriptionClients.Add(new SubscriptionProcessor<SymbolPackageMessage>(c, _messageSerializer, _messageHandler, _loggerFactory.CreateLogger<SubscriptionProcessor<SymbolPackageMessage>>()));
            //}
        }

        private void AddReceiver()
        {
            ISubscriptionClient c = new SubscriptionClientWrapper(topicConnectionString, topicPath, subscriptionName);
            _serviceBusSubscriptionClients.Add(new SubscriptionProcessor<SymbolPackageMessage>(c, _messageSerializer, _messageHandler, _loggerFactory.CreateLogger<SubscriptionProcessor<SymbolPackageMessage>>()));
        }

        private void AddReceivers(long count)
        {
            if(count <= 0)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                AddReceiver();
            }
        }

        private long CalculateOptimalReceiversCount()
        {
            return _dop;
            //return _subscriptionDescription.MessageCount/_dop;
        }

        public IEnumerable<Task> RegisterReceivers(CancellationToken token)
        {
            AddReceivers(CalculateOptimalReceiversCount());

            var receivers = _serviceBusSubscriptionClients.Select(sbcs => 
            {
                if (!token.IsCancellationRequested)
                {
                    return Task.Run(() =>
                    {
                        using (CancellationTokenRegistration ctr = token.Register(async () => await sbcs.ShutdownAsync(TimeSpan.FromMilliseconds(1))))
                        {
                            sbcs.Start();
                        }
                    });
                }
                return null;
            })
            .Where( t=> t != null )
            .ToList();

            return receivers;
        }

        public void RegisterSubscriptionWatcherCallback(CancellationToken token, int millisecondsCallback)
        {
            RegisterSubscriptionWatcherCallback(token, millisecondsCallback, SubscriptionWatcherCallback);
        }

        private void RegisterSubscriptionWatcherCallback(CancellationToken token, int milliseconds, Action<SubscriptionDescription> callback)
        {
            TimerCallback timercallback = (o) =>
            {
                SubscriptionDescription subscrioptionDesc = o as SubscriptionDescription;
                callback(subscrioptionDesc);
            };
            Timer timer = new Timer(timercallback, _subscriptionDescription, milliseconds, milliseconds);
            token.Register( () => timer.Dispose());
        }

        private void SubscriptionWatcherCallback(SubscriptionDescription subscriptionDescription)
        {
            if (CalculateOptimalReceiversCount() > _serviceBusSubscriptionClients.Count)
            {
                AddReceivers(_serviceBusSubscriptionClients.Count - CalculateOptimalReceiversCount());
            }
        }

        public void Print()
        {
            File.WriteAllLines("timesToIngest.txt", _messageHandler.timesToIngest.ToArray());
            File.WriteAllLines("onHandleAsyncTimes.txt", _messageHandler.onHandleAsyncTimes.ToArray());
        }
    }
}
