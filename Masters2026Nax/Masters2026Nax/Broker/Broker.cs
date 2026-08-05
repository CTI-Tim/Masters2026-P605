using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MessageBroker
{
    /// <summary>
    /// Tim's Message Broker 1.80.200
    ///
    /// This broker acts as the central communication bus for the application.
    /// The goal is to keep the code as decoupled as possible by allowing classes
    /// to publish and subscribe to messages instead of passing object references
    /// throughout the entire system.
    ///
    /// In automation programming we often build abstraction layers around hardware
    /// so devices can be swapped out, simulated, or upgraded later. The downside
    /// is that once every class starts needing references to every other class,
    /// maintenance becomes painful and the codebase becomes tightly coupled.
    ///
    /// Using the broker, a device, touch panel, scheduler, logging system,
    /// automation routine, or anything else can publish and consume messages
    /// without knowing anything about the underlying implementation of the
    /// other components.
    ///
    /// Version 1.80.200 supports multiple subscribers per topic and removes the
    /// older DynamicInvoke implementation. Delegates are invoked directly using
    /// strongly typed MessageDelegate references which reduces overhead and
    /// improves performance when message traffic becomes heavy.
    ///
    /// Message delivery is intentionally synchronous.
    ///
    /// Every subscriber is called on the same thread that called SendMessage().
    /// This guarantees that when SendMessage() returns, all subscribers have
    /// completed processing. It also guarantees subscriber execution order and
    /// allows exceptions to be returned directly to the caller through an
    /// AggregateException.
    ///
    /// A common suggestion is to automatically launch a thread for each
    /// subscriber. While that sounds really attractive, it creates more problems than
    /// it solves. Execution order becomes wildly unpredictable, exceptions become harder
    /// to track down, and the caller no longer knows when processing has actually
    /// completed.
    ///
    /// If a subscriber needs to perform long-running work, network operations,
    /// file I/O, database access, or anything else that may block, THAT subscriber method
    /// should launch its own worker thread or task and return immediately. The
    /// broker's job is message delivery, not thread management.
    /// 
    /// Lets say that again for those in back.....
    /// 
    /// The  broker's job is message delivery, not thread management.
    ///
    /// If you subscribe to a message, keep the handler fast and not blocking.
    /// </summary>

    public static class Broker
    {
        public delegate void MessageDelegate(Message m);

        private static readonly ConcurrentDictionary<string, MessageDelegate> Messages =
            new ConcurrentDictionary<string, MessageDelegate>();

        /// <summary>
        /// Subscribe a delegate to a message key. Multiple subscribers are supported per key.
        /// If the key does not exist, it is created automatically.
        /// </summary>
        public static void AddDelegate(string key, MessageDelegate method)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Message key cannot be null, empty, or whitespace.", nameof(key));

            if (method == null)
                throw new ArgumentNullException(nameof(method));

            Messages.AddOrUpdate(
                key,
                method,
                (k, existing) => existing + method
            );
        }

        /// <summary>
        /// Unsubscribe a specific delegate from a message key.
        /// If no subscribers remain, the key is removed.
        /// Returns true if the delegate was removed.
        /// </summary>
        public static bool RemoveDelegate(string key, MessageDelegate method)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Message key cannot be null, empty, or whitespace.", nameof(key));

            if (method == null)
                throw new ArgumentNullException(nameof(method));

            while (Messages.TryGetValue(key, out var existing))
            {
                var updated = existing - method;

                if (updated == existing)
                    return false;

                if (updated == null)
                {
                    bool removed =
                        ((ICollection<KeyValuePair<string, MessageDelegate>>)Messages)
                        .Remove(new KeyValuePair<string, MessageDelegate>(key, existing));

                    if (removed)
                        return true;
                }
                else
                {
                    if (Messages.TryUpdate(key, updated, existing))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Send a message to all subscribers on the given key.
        /// Each subscriber is invoked individually. If one throws, the rest still fire.
        /// Returns false if no subscribers are found.
        /// Returns an Exception if any subscribers throw.
        /// </summary>
        public static bool SendMessage(string key, Message m)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Message key cannot be null, empty, or whitespace.", nameof(key));

            if (m == null)
                throw new ArgumentNullException(nameof(m));

            if (!Messages.TryGetValue(key, out var handler) || handler == null)
                return false;

            var exceptions = new List<Exception>();

            foreach (MessageDelegate subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(m);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Count > 0)
            {

                // This is a perfect use of exception based programming.  I dont know what the subscribers are doing
                // therefore if any of them throw and exception I let the message sender also have an exception so it can either
                // crash the program or handle it.
                throw new AggregateException( $"One or more subscribers threw an exception on key '{key}'.", exceptions );
                
            }

            return true;
        }

        /// <summary>
        /// Removes all message subscriptions from the broker.
        /// Useful during shutdown, testing, or full system reinitialization.
        /// WARNING: Be carefult to not accidently call this as they are all deleted
        /// and there is no possibility of recovering them
        /// </summary>
        public static void Clear()
        {
            Messages.Clear();
        }
    }
}
