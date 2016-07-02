using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscriber;
using Reactor.Core.subscription;
using Reactor.Core.util;
using System.Runtime.InteropServices;

namespace Reactor.Core.publisher
{
    sealed class PublisherOnBackpressureLatest<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        internal PublisherOnBackpressureLatest(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            source.Subscribe(new OnBackpressureLatestSubscriber(s));
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class OnBackpressureLatestSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            ISubscription s;

            long requested;

            long produced;

            bool done;
            Exception error;

            bool cancelled;

            Node latest;

            Pad128 p0;

            int wip;

            Pad120 p1;

            internal OnBackpressureLatestSubscriber(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }

            public void OnNext(T t)
            {
                Interlocked.Exchange(ref latest, new Node(t));
                Drain();
            }

            public void OnError(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Drain();
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                if (QueueDrainHelper.Enter(ref wip))
                {
                    latest = null;
                }
            }

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                int missed = 1;
                var a = actual;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = produced;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            latest = null;
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        Node n = Volatile.Read(ref latest);
                        if (n != null)
                        {
                            n = Interlocked.Exchange(ref latest, null);
                        }

                        bool empty = n == null;

                        if (d && empty)
                        {
                            var ex = error;
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(n.value);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            latest = null;
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        Node n = Volatile.Read(ref latest);

                        if (d && n == null)
                        {
                            var ex = error;
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }
                            return;
                        }
                    }

                    produced = e;
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Class to host the latest value (needed for Interlocked.Exchange).
        /// </summary>
        internal sealed class Node
        {
            internal readonly T value;

            internal Node(T value)
            {
                this.value = value;
            }
        }
    }
}
