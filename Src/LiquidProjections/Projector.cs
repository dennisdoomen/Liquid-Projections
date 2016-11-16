﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class Projector
    {
        private readonly IEventMap<ProjectionContext> map;
        private readonly IReadOnlyList<Projector> children;

        public Projector(IEventMapBuilder<ProjectionContext> eventMapBuilder, IEnumerable<Projector> children = null)
        {
            if (eventMapBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMapBuilder));
            }

            SetupHandlers(eventMapBuilder);
            map = eventMapBuilder.Build();
            this.children = children?.ToList() ?? new List<Projector>();

            if (this.children.Contains(null))
            {
                throw new ArgumentException("There is null child projector.", nameof(children));
            }
        }

        private void SetupHandlers(IEventMapBuilder<ProjectionContext> eventMapBuilder)
        {
            eventMapBuilder.HandleCustomActionsAs((context, projector) => projector());
        }

        /// <summary>
        /// Instructs the projector to handle a collection of ordered transactions.
        /// </summary>
        /// <param name="transactions">
        /// </param>
        public async Task Handle(IReadOnlyList<Transaction> transactions)
        {
            foreach (Transaction transaction in transactions)
            {
                await ProjectTransaction(transaction);
            }
        }

        private async Task ProjectTransaction(Transaction transaction)
        {
            foreach (EventEnvelope eventEnvelope in transaction.Events)
            {
                try
                {
                    await ProjectEvent(
                        eventEnvelope.Body,
                        new ProjectionContext
                        {
                            TransactionId = transaction.Id,
                            StreamId = transaction.StreamId,
                            TimeStampUtc = transaction.TimeStampUtc,
                            Checkpoint = transaction.Checkpoint,
                            EventHeaders = eventEnvelope.Headers,
                            TransactionHeaders = transaction.Headers
                        });
                }
                catch (ProjectionException projectionException)
                {
                    projectionException.CurrentEvent = eventEnvelope;
                    projectionException.TransactionId = transaction.Id;
                    projectionException.SetTransactionBatch(new[] { transaction });
                    throw;
                }
                catch (Exception exception)
                {
                    var projectionException = new ProjectionException("Projector failed to project an event.", exception)
                    {
                        CurrentEvent = eventEnvelope,
                        TransactionId = transaction.Id
                    };

                    projectionException.SetTransactionBatch(new[] { transaction });
                    throw projectionException;
                }
            }
        }

        private async Task ProjectEvent(object anEvent, ProjectionContext context)
        {
            foreach (Projector child in children)
            {
                await child.ProjectEvent(anEvent, context);
            }

            // There is no way to identify the child projector when an exception happens so we don't handle exceptions here.
            await map.Handle(anEvent, context);
        }
    }
}
