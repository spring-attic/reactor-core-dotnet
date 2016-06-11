using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscription;
using Reactor.Core.util;
using Reactor.Core.subscriber;

namespace Reactor.Core.publisher
{
    sealed class PublisherMap<T, R> : IFlux<R>, IMono<R>
    {
        readonly IPublisher<T> source;

        readonly Func<T, R> mapper;

        internal PublisherMap(IPublisher<T> source, Func<T, R> mapper)
        {
            this.source = source;
            this.mapper = mapper;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            if (s is IConditionalSubscriber<R>)
            {
                source.Subscribe(new MapConditionalSubscriber<T, R>((IConditionalSubscriber<R>)s, mapper));
            }
            else
            {
                source.Subscribe(new MapSubscriber<T, R>(s, mapper));
            }
        }
    }

    sealed class MapSubscriber<T, R> : BasicFuseableSubscriber<T, R>
    {
        readonly Func<T, R> mapper;

        public MapSubscriber(ISubscriber<R> actual, Func<T, R> mapper) : base(actual)
        {
            this.mapper = mapper;
        }

        public override void OnComplete()
        {
            Complete();
        }

        public override void OnError(Exception e)
        {
            Error(e);
        }

        public override void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            R v;

            try
            {
                v = mapper(t);
            }
            catch (Exception ex)
            {
                Fail(ex);
                return;
            }

            actual.OnNext(v);
        }

        public override bool Poll(out R value)
        {
            T local;

            if (qs.Poll(out local))
            {
                value = mapper(local);
                return true;
            }
            value = default(R);
            return false;
        }

        public override int RequestFusion(int mode)
        {
            return TransitiveBoundaryFusion(mode);
        }
    }

    sealed class MapConditionalSubscriber<T, R> : BasicFuseableConditionalSubscriber<T, R>
    {
        readonly Func<T, R> mapper;

        public MapConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, R> mapper) : base(actual)
        {
            this.mapper = mapper;
        }

        public override void OnComplete()
        {
            Complete();
        }

        public override void OnError(Exception e)
        {
            Error(e);
        }

        public override void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            R v;

            try
            {
                v = mapper(t);
            }
            catch (Exception ex)
            {
                Fail(ex);
                return;
            }

            actual.OnNext(v);
        }

        public override bool Poll(out R value)
        {
            T local;

            if (qs.Poll(out local))
            {
                value = mapper(local);
                return true;
            }
            value = default(R);
            return false;
        }

        public override int RequestFusion(int mode)
        {
            return TransitiveBoundaryFusion(mode);
        }

        public override bool TryOnNext(T t)
        {
            if (done)
            {
                return true;
            }

            R v;

            try
            {
                v = mapper(t);
            }
            catch (Exception ex)
            {
                Fail(ex);
                return true;
            }

            return actual.TryOnNext(v);
        }
    }

}
