﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class MemoryEventSource : IEventStore
    {
        private readonly int batchSize;
        private static long lastCheckpoint;

        private readonly List<Subscriber> subscribers = new List<Subscriber>();
        private readonly List<Transaction> history = new List<Transaction>();

        public MemoryEventSource(int batchSize = 10)
        {
            this.batchSize = batchSize;
        }

        public IDisposable Subscribe(long? fromCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            var subscriber = new Subscriber(fromCheckpoint ?? 0, batchSize, handler);

            subscribers.Add(subscriber);

            Task.Run(async () =>
            {
                foreach (Transaction transaction in history)
                {
                    await subscriber.Send(new[] { transaction });
                }

            }).Wait();

            return subscriber;
        }


        public async Task<Transaction> Write(object @event)
        {
            Transaction transaction = new Transaction
            {
                Events =
                {
                    new EventEnvelope
                    {
                        Body = @event
                    }
                }
            };

            await Write(transaction);

            return transaction;
        }

        public async Task Write(params Transaction[] transactions)
        {
            foreach (var transaction in transactions)
            {
                transaction.Checkpoint = (++lastCheckpoint);
                history.Add(transaction);
            }

            foreach (var subscriber in subscribers)
            {
                await subscriber.Send(transactions);
            }
        }
    }

    internal class Subscriber : IDisposable
    {
        private readonly long fromCheckpoint;
        private readonly int batchSize;
        private readonly Func<IReadOnlyList<Transaction>, Task> handler;
        
        public Subscriber(long fromCheckpoint, int batchSize, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            this.fromCheckpoint = fromCheckpoint;
            this.batchSize = batchSize;
            this.handler = handler;
        }

        public bool Disposed { get; private set; } = false;

        public async Task Send(IEnumerable<Transaction> transactions)
        {
            if (!Disposed)
            {
                foreach (var batch in transactions.Where(t => t.Checkpoint >= fromCheckpoint).InBatchesOf(batchSize))
                {
                    await handler(batch.ToList().AsReadOnly());
                }
            }
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}