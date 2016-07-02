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

namespace Reactor.Core.publisher
{
    sealed class PublisherScan<T> : IFlux<T>, IMono<T>
    {
        readonly IPublisher<T> source;

        readonly Func<T, T, T> scanner;

        internal PublisherScan(IPublisher<T> source, Func<T, T, T> scanner)
        {
            this.source = source;
            this.scanner = scanner;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            source.Subscribe(new ScanSubscriber(s, scanner));
        }

        sealed class ScanSubscriber : BasicSubscriber<T, T>
        {
            readonly Func<T, T, T> scanner;

            T value;

            bool hasValue;

            public ScanSubscriber(ISubscriber<T> actual, Func<T, T, T> scanner) : base(actual)
            {
                this.scanner = scanner;
            }

            public override void OnComplete()
            {
                actual.OnComplete();
            }

            public override void OnError(Exception e)
            {
                value = default(T);
                actual.OnError(e);
            }

            public override void OnNext(T t)
            {
                if (!hasValue)
                {
                    hasValue = true;
                    value = t;
                    actual.OnNext(t);
                }
                else
                {
                    T v;
                    try
                    {
                        v = scanner(value, t);
                        value = v;
                    }
                    catch (Exception ex)
                    {
                        Fail(ex);
                        return;
                    }

                    actual.OnNext(v);
                }
            }
        }
    }
}
