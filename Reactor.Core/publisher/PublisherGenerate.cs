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
    sealed class PublisherGenerate<T, S> : IFlux<T>, IFuseable
    {
        readonly Func<S> stateFactory;

        readonly Func<S, ISignalEmitter<T>, S> generator;

        readonly Action<S> stateDisposer;

        internal PublisherGenerate(Func<S> stateFactory, Func<S, ISignalEmitter<T>, S> generator, Action<S> stateDisposer)
        {
            this.stateFactory = stateFactory;
            this.generator = generator;
            this.stateDisposer = stateDisposer;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            S state;

            try
            {
                state = stateFactory();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                EmptySubscription<T>.Error(s, ex);
                return;
            }

            if (s is IConditionalSubscriber<T>)
            {
                s.OnSubscribe(new GenerateConditionalSubscriber((IConditionalSubscriber<T>)s, state, generator, stateDisposer));
            }
            else
            {
                s.OnSubscribe(new GenerateSubscriber(s, state, generator, stateDisposer));
            }
        }

        internal abstract class GenerateBaseSubscription : IQueueSubscription<T>, ISignalEmitter<T>
        {
            protected readonly Func<S, ISignalEmitter<T>, S> generator;

            readonly Action<S> stateDisposer;

            protected S state;

            protected long requested;

            protected bool hasValue;

            protected bool terminated;

            protected bool cancelled;

            protected bool fused;

            protected T value;

            protected Exception error;

            internal GenerateBaseSubscription(S state, Func<S, ISignalEmitter<T>, S> generator, Action<S> stateDisposer)
            {
                this.state = state;
                this.generator = generator;
                this.stateDisposer = stateDisposer;
            }

            internal abstract void SlowPath(long r);

            internal abstract void FastPath();

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (BackpressureHelper.GetAndAddCap(ref requested, n) == 0)
                    {
                        if (n == long.MaxValue)
                        {
                            FastPath();
                        }
                        else
                        {
                            SlowPath(n);
                        }
                    }
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                if (Interlocked.Increment(ref requested) == 1)
                {
                    Clear();
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FuseableHelper.BOUNDARY) != 0)
                {
                    return FuseableHelper.NONE;
                }
                int m = mode & FuseableHelper.SYNC;
                fused = m != 0;
                return m;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                if (terminated)
                {
                    Clear();
                    Exception ex = error;
                    if (ex != null)
                    {
                        throw ex;
                    }
                    value = default(T);
                    return false;
                }

                state = generator(state, this);

                if (hasValue)
                {
                    hasValue = false;
                    value = this.value;
                    return true;
                }
                if (terminated)
                {
                    Clear();
                    Exception ex = error;
                    if (ex != null)
                    {
                        throw ex;
                    }
                    value = default(T);
                    return false;
                }
                throw new InvalidOperationException("The generator didn't produce any signal");
            }

            public bool IsEmpty()
            {
                return !hasValue;
            }

            public void Clear()
            {
                try
                {
                    stateDisposer(state);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
            }

            public abstract void Next(T t);

            public abstract void Error(Exception e);

            public abstract void Complete();
        }

        sealed class GenerateSubscriber : GenerateBaseSubscription
        {
            readonly ISubscriber<T> actual;

            public GenerateSubscriber(ISubscriber<T> actual, S state, Func<S, ISignalEmitter<T>, S> generator, Action<S> stateDisposer) : 
                base(state, generator, stateDisposer)
            {
                this.actual = actual;
            }

            public override void Complete()
            {
                if (terminated)
                {
                    return;
                }
                terminated = true;
                if (!fused)
                {
                    actual.OnComplete();
                }
            }

            public override void Error(Exception e)
            {
                if (terminated)
                {
                    ExceptionHelper.OnErrorDropped(e);
                    return;
                }
                terminated = true;
                if (fused)
                {
                    error = e;
                }
                else
                {
                    actual.OnError(e);
                }
            }

            public override void Next(T t)
            {
                if (hasValue)
                {
                    if (!terminated)
                    {
                        Error(new InvalidOperationException("The generator produced more than one value in a round."));
                    }
                    return;
                }
                if (fused)
                {
                    value = t;
                    hasValue = true;
                }
                else
                {
                    actual.OnNext(t);
                }
            }

            internal override void FastPath()
            {
                S s = state;
                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    try
                    {
                        s = generator(s, this);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        
                        actual.OnError(ex);
                        Clear();
                        return;
                    }

                    if (terminated)
                    {
                        Clear();
                        return;
                    }
                    if (!hasValue)
                    {
                        actual.OnError(new InvalidOperationException("The generator didn't produce any signal"));
                        Clear();
                        return;
                    }
                    hasValue = false;
                }
            }

            internal override void SlowPath(long r)
            {
                S s = state;
                long e = 0L;

                for (;;)
                {
                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        try
                        {
                            s = generator(s, this);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            actual.OnError(ex);
                            Clear();
                            return;
                        }

                        if (terminated)
                        {
                            Clear();
                            return;
                        }
                        if (!hasValue)
                        {
                            actual.OnError(new InvalidOperationException("The generator didn't produce any signal"));
                            Clear();
                            return;
                        }
                        hasValue = false;
                        e++;
                    }

                    r = Volatile.Read(ref requested);
                    if (e == r)
                    {
                        state = s;
                        r = Interlocked.Add(ref requested, -e);
                        if (r == 0L)
                        {
                            break;
                        }
                        e = 0L;
                    }
                }
            }
        }

        sealed class GenerateConditionalSubscriber : GenerateBaseSubscription
        {
            readonly IConditionalSubscriber<T> actual;

            bool skipped;

            public GenerateConditionalSubscriber(IConditionalSubscriber<T> actual, S state, Func<S, ISignalEmitter<T>, S> generator, Action<S> stateDisposer) :
                base(state, generator, stateDisposer)
            {
                this.actual = actual;
            }

            public override void Complete()
            {
                if (terminated)
                {
                    return;
                }
                terminated = true;
                if (!fused)
                {
                    actual.OnComplete();
                }
            }

            public override void Error(Exception e)
            {
                if (terminated)
                {
                    ExceptionHelper.OnErrorDropped(e);
                    return;
                }
                terminated = true;
                if (fused)
                {
                    error = e;
                }
                else
                {
                    actual.OnError(e);
                }
            }

            public override void Next(T t)
            {
                if (hasValue)
                {
                    if (!terminated)
                    {
                        Error(new InvalidOperationException("The generator produced more than one value in a round."));
                    }
                    return;
                }
                if (fused)
                {
                    value = t;
                    hasValue = true;
                }
                else
                {
                    skipped = actual.TryOnNext(t);
                }
            }

            internal override void FastPath()
            {
                S s = state;
                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    try
                    {
                        s = generator(s, this);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);

                        actual.OnError(ex);
                        Clear();
                        return;
                    }

                    if (terminated)
                    {
                        Clear();
                        return;
                    }
                    if (!hasValue)
                    {
                        actual.OnError(new InvalidOperationException("The generator didn't produce any signal"));
                        Clear();
                        return;
                    }
                    hasValue = false;
                }
            }

            internal override void SlowPath(long r)
            {
                S s = state;
                long e = 0L;

                for (;;)
                {
                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        try
                        {
                            s = generator(s, this);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            actual.OnError(ex);
                            Clear();
                            return;
                        }

                        if (terminated)
                        {
                            Clear();
                            return;
                        }
                        if (!hasValue)
                        {
                            actual.OnError(new InvalidOperationException("The generator didn't produce any signal"));
                            Clear();
                            return;
                        }
                        hasValue = false;

                        if (!skipped)
                        {
                            e++;
                        }
                    }

                    r = Volatile.Read(ref requested);
                    if (e == r)
                    {
                        state = s;
                        r = Interlocked.Add(ref requested, -e);
                        if (r == 0L)
                        {
                            break;
                        }
                        e = 0L;
                    }
                }
            }
        }
    }
}
