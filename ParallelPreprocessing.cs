using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PRISM
{
    /// <summary>
    /// Static class to contain an extension method for limited parallel processing of an IEnumerable and the class containing all of the implementation details
    /// </summary>
    public static class ParallelPreprocessing
    {
        /// <summary>
        /// Performs pre-processing using parallelization. Up to <paramref name="maxThreads"/> threads will be used to process data prior to it being requested by (and simultaneous with) the enumerable consumer. Backed by a producer-consumer queue pattern
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="sourceEnum">source enumerable; preferably something like a list of file that need to be loaded</param>
        /// <param name="processFunction">Transform function from <paramref name="sourceEnum"/> to return type; should involve heavy processing (if x => x, you may see a performance penalty)</param>
        /// <param name="maxThreads">Max number of <paramref name="sourceEnum"/> items to process simultaneously</param>
        /// <param name="maxPreprocessed">Max number of items to allow being preprocessed or completed-but-not-consumed at any time; defaults to <paramref name="maxThreads"/></param>
        /// <param name="checkIntervalSeconds">How often to check for completion of the preprocessing</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>IEnumerable of items that have been processed via <paramref name="processFunction"/></returns>
        public static IEnumerable<TResult> ParallelPreprocess<T, TResult>(this IEnumerable<T> sourceEnum, Func<T, TResult> processFunction,
            int maxThreads = 1, int maxPreprocessed = -1, double checkIntervalSeconds = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return new ParallelPreprocessor<T, TResult>(sourceEnum, processFunction, maxThreads, maxPreprocessed, checkIntervalSeconds, cancellationToken).ConsumeAll();
        }

        /// <summary>
        /// Implementation details for the extension method; implements a producer-consumer pattern with 1 consumer and x producers.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        private class ParallelPreprocessor<T, TResult> : IDisposable
        {
            /// <summary>
            /// Target and source block for the producer-consumer pattern
            /// </summary>
            private readonly BufferQueue<TResult> buffer;

            /// <summary>
            /// Semaphore to limit the number of items that are being/have been preprocessed. Incremented by the producer(s), decremented by the consumer
            /// </summary>
            private readonly SemaphoreSlim preprocessedLimiter;

            /// <summary>
            /// List of thread to monitor the producers and determine when they are done, to properly mark the target block as completed.
            /// </summary>
            private readonly List<Thread> producerThreads = new List<Thread>();

            /// <summary>
            /// Count of threads that have exited
            /// </summary>
            private int threadsDone = 0;

            /// <summary>
            /// Cancellation token to support early cancellation
            /// </summary>
            private readonly CancellationToken cancelToken;

            /// <summary>
            /// Return a processed item one at a time, as they are requested and become available, until done.
            /// </summary>
            /// <returns></returns>
            public IEnumerable<TResult> ConsumeAll()
            {
                Tuple<bool, TResult> item;

                while (!buffer.IsCompleted)
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // while we get an item with a boolean value of true, return the result
                    while ((item = TryConsume().Result).Item1)
                    {
                        yield return item.Item2;
                    }

                    if (!buffer.IsCompleted)
                    {
                        Thread.Sleep(200);
                    }
                }
            }

            /// <summary>
            /// Try to consume an item.
            /// </summary>
            /// <returns>Tuple with a boolean and TResult; the boolean is true if successful, false otherwise</returns>
            /// <remarks>out and ref parameters are not allowed with async methods, otherwise this would just return a bool and have an out parameter with the result</remarks>
            private async Task<Tuple<bool, TResult>> TryConsume()
            {
                while (await buffer.OutputAvailableAsync(cancelToken))
                {
                    preprocessedLimiter.Release(); // release one, allow another item to be preprocessed
                    return new Tuple<bool, TResult>(true, buffer.Receive());
                }

                return new Tuple<bool, TResult>(false, default(TResult));
            }

            /// <summary>
            /// Setup for and start the producers
            /// </summary>
            /// <param name="sourceEnum"></param>
            /// <param name="processFunction"></param>
            /// <param name="numThreads"></param>
            /// <param name="checkIntervalSeconds">How often to check for completion of the preprocessing</param>
            private void Start(IEnumerable<T> sourceEnum, Func<T, TResult> processFunction, int numThreads = 1, double checkIntervalSeconds = 1)
            {
                var enumerator = sourceEnum.GetEnumerator();
                var enumeratorLock = new object();

                for (var i = 0; i < numThreads; i++)
                {
                    // A 'threadId' for debugging purposes
                    var threadId = i;
                    var thread = new Thread(() => Producer(enumerator, processFunction, enumeratorLock, threadId));
                    producerThreads.Add(thread);
                    thread.Start();
                }

                // Monitor once per second
                threadMonitor = new Timer(ThreadMonitorCheck, this, TimeSpan.FromSeconds(checkIntervalSeconds), TimeSpan.FromSeconds(checkIntervalSeconds));
            }

            /// <summary>
            /// Timer used to monitor the producers
            /// </summary>
            private Timer threadMonitor;

            /// <summary>
            /// Timer callback function: check on the producer threads, if they are no longer alive, then mark the target block as complete
            /// </summary>
            /// <param name="sender"></param>
            private void ThreadMonitorCheck(object sender)
            {
                var done = true;

                foreach (var thread in producerThreads)
                {
                    if (thread.IsAlive)
                    {
                        // if any thread is still alive, we're not yet done.
                        done = false;
                        break;
                    }
                }

                if (threadsDone < producerThreads.Count && !buffer.IsCompleted)
                {
                    done = false;
                }

                if (done)
                {
                    threadMonitor?.Dispose();
                    foreach (var thread in producerThreads)
                    {
                        thread.Join();
                    }

                    // Report no more items
                    buffer.Complete();
                }
            }

            /// <summary>
            /// Producer function: process the source enumerable in parallel, with limits and checks
            /// </summary>
            /// <param name="sourceEnumerator"></param>
            /// <param name="processFunction"></param>
            /// <param name="accessLock"></param>
            /// <param name="threadId">A 'threadID' for debugging purposes</param>
            private async void Producer(IEnumerator<T> sourceEnumerator, Func<T, TResult> processFunction, object accessLock, int threadId)
            {
                try
                {
                    while (true)
                    {
                        if (cancelToken.IsCancellationRequested)
                        {
                            return;
                        }

                        await preprocessedLimiter
                            .WaitAsync(
                                cancelToken); // check the preprocessing limit, wait until there is another "space" available
                        T item;
                        // read one item. lock required because we have no guarantees on the thread-safety of the enumerator
                        lock (accessLock)
                        {
                            if (!sourceEnumerator.MoveNext())
                            {
                                preprocessedLimiter.Release(1); // Release the one we used
                                break;
                            }

                            item = sourceEnumerator.Current;
                        }

                        // Run the process function on the item from the enumerator
                        var processed = processFunction(item);

                        // synchronously attempt to add an item to the target block; this will fail if we've hit the upper bound limit of the target block
                        //buffer.Post(processed);

                        // asynchronously attempt to add an item to the target block; this will wait if we've hit the upper bound limit of the target block
                        var result = await buffer.SendAsync(processed, cancelToken);
                        if (!result)
                        {
                            Console.WriteLine("ERROR: Producer.SendAsync() failed to add item to processing queue!!!");
                        }
                    }
                }
                finally
                {
                    Interlocked.Increment(ref threadsDone);
                }
            }

            /// <summary>
            /// Performs pre-processing using parallelization. Up to <paramref name="maxThreads"/> threads will be used to process data prior to it being requested by (and simultaneous with) the enumerable consumer.
            /// </summary>
            /// <param name="source">source enumerable; preferably something like a list of file that need to be loaded</param>
            /// <param name="processFunction">Transform function from <paramref name="source"/> to return type; should involve heavy processing (if x => x, you may see a performance penalty)</param>
            /// <param name="maxThreads">Max number of <paramref name="source"/> items to process simultaneously</param>
            /// <param name="maxPreprocessed">Max number of items to allow being preprocessed or completed-but-not-consumed at any time; defaults to <paramref name="maxThreads"/></param>
            /// <param name="checkIntervalSeconds">How often to check for completion of the preprocessing</param>
            /// <param name="cancellationToken">Cancellation token</param>
            /// <returns>IEnumerable of items that have been processed via <paramref name="processFunction"/></returns>
            public ParallelPreprocessor(IEnumerable<T> source, Func<T, TResult> processFunction, int maxThreads, int maxPreprocessed = -1, double checkIntervalSeconds = 1,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                cancelToken = cancellationToken;
                if (maxPreprocessed < 1)
                {
                    maxPreprocessed = maxThreads;
                }

                preprocessedLimiter = new SemaphoreSlim(maxPreprocessed, maxPreprocessed);

                buffer = new BufferQueue<TResult>(maxPreprocessed + 1);

                Start(source, processFunction, maxThreads, checkIntervalSeconds);
            }

            ~ParallelPreprocessor()
            {
                Dispose();
            }

            public void Dispose()
            {
                preprocessedLimiter?.Dispose();
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Modeled after the idea of a BufferBlock, but not using a BufferBlock because that requires tracking a NuGet package.
            /// </summary>
            /// <typeparam name="TU"></typeparam>
            private class BufferQueue<TU> : IDisposable
            {
                private readonly Queue<TU> queue = new Queue<TU>();
                private readonly SemaphoreSlim trigger = new SemaphoreSlim(0);
                private readonly SemaphoreSlim maxEntries = null;
                private readonly object addRemoveLock = new object();
                private bool complete = false;

                public int BoundedCapacity { get; }

                public bool IsCompleted => complete;

                public BufferQueue(int boundedCapacity = -1)
                {
                    BoundedCapacity = boundedCapacity;
                    if (boundedCapacity <= 0)
                    {
                        BoundedCapacity = -1;
                    }
                    else
                    {
                        maxEntries = new SemaphoreSlim(BoundedCapacity, BoundedCapacity);
                    }
                }

                /// <summary>
                /// Returns true if there is available output, false if an error or marked complete (with no more output)
                /// </summary>
                /// <returns></returns>
                public async Task<bool> OutputAvailableAsync(CancellationToken ct = default(CancellationToken))
                {
                    await trigger.WaitAsync(ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested)
                    {
                        return false;
                    }

                    return ItemsAvailable() ?? false;
                }

                /// <summary>
                /// Retrieve an item off of the queue
                /// </summary>
                /// <returns></returns>
                public TU Receive()
                {
                    lock (addRemoveLock)
                    {
                        maxEntries?.Release();
                        return queue.Dequeue();
                    }
                }

                private bool? ItemsAvailable()
                {
                    if (queue.Count > 0)
                    {
                        return true;
                    }

                    if (complete)
                    {
                        return false;
                    }

                    // error state, or pre-wait check
                    return null;
                }

                /// <summary>
                /// Add an item to the queue, notifying any consumer(s) of the available item.
                /// </summary>
                /// <param name="item"></param>
                private void Post(TU item)
                {
                    lock (addRemoveLock)
                    {
                        queue.Enqueue(item);
                    }

                    trigger.Release();
                }

                /// <summary>
                /// Add an item to the queue, notifying any consumer(s) of the available item.
                /// </summary>
                /// <param name="item"></param>
                /// <param name="ct"></param>
                public async Task<bool> SendAsync(TU item, CancellationToken ct = default(CancellationToken))
                {
                    if (maxEntries != null)
                    {
                        await maxEntries.WaitAsync(ct).ConfigureAwait(false);
                    }
                    if (ct.IsCancellationRequested)
                    {
                        return false;
                    }

                    Post(item);
                    return true;
                }

                /// <summary>
                /// Mark the queue as complete, so that things will exit out properly
                /// </summary>
                public void Complete()
                {
                    this.complete = true;
                    trigger.Release();
                }

                ~BufferQueue()
                {
                    Dispose();
                }

                /// <summary>
                /// Clean up.
                /// </summary>
                public void Dispose()
                {
                    if (!complete)
                    {
                        Complete();
                    }
                    trigger?.Dispose();
                    maxEntries?.Dispose();
                    GC.SuppressFinalize(this);
                }
            }
        }
    }
}
