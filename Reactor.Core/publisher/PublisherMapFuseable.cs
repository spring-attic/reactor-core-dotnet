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
    sealed class PublisherMapFuseable<T, R> : IFlux<R>, IMono<R>, IFuseable
    {
        readonly IPublisher<T> source;

        readonly Func<T, R> mapper;

        internal PublisherMapFuseable(IPublisher<T> source, Func<T, R> mapper)
        {
            this.source = source;
            this.mapper = mapper;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            source.Subscribe(new MapFuseableSubscriber<T, R>(s, mapper));
        }
    }

    sealed class MapFuseableSubscriber<T, R> : BasicFuseableSubscriber<T, R>
    {
        readonly Func<T, R> mapper;

        public MapFuseableSubscriber(ISubscriber<R> actual, Func<T, R> mapper) : base(actual)
        {
            this.mapper = mapper;
        }

        public override void Clear()
        {
            s.Clear();
        }

        public override bool IsEmpty()
        {
            return s.IsEmpty();
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
            if (fusionMode == FuseableHelper.ASYNC)
            {
                actual.OnNext(default(R));
                return;
            }

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

            if (v == null)
            {
                Fail(new NullReferenceException("The mapper returned a null value"));
                return;
            }

            actual.OnNext(v);
        }

        public override bool Poll(out R value)
        {
            T v;
            
            if (s.Poll(out v))
            {
                value = mapper(v);
                return true;
            }
            value = default(R);
            return false;
        }

        public override int RequestFusion(int mode)
        {
            return TransitiveBoundaryFusion(mode);
        }

        protected override void OnStart()
        {
            // nothing to do
        }
    }
}
