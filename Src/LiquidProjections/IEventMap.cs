﻿using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Defines the contract between event maps and projection storage providers.
    /// </summary>
    /// <typeparam name="TContext">
    /// An object that provides additional information and metadata to the consuming projection code.
    /// </typeparam>
    public interface IEventMap<in TContext>
    {
        /// <summary>
        /// Handles <paramref name="anEvent"/> asynchronously.
        /// </summary>
        Task Handle(object anEvent, TContext context);
    }
}