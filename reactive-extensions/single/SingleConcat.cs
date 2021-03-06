﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using static akarnokd.reactive_extensions.ValidationHelper;

namespace akarnokd.reactive_extensions
{
    /// <summary>
    /// Run the source single and signal their success
    /// value in order, optionally delaying errors.
    /// </summary>
    /// <typeparam name="T">The success and item type.</typeparam>
    /// <remarks>Since 0.0.12</remarks>
    internal sealed class SingleConcat<T> : IObservable<T>
    {
        readonly ISingleSource<T>[] sources;

        readonly bool delayErrors;

        public SingleConcat(ISingleSource<T>[] sources, bool delayErrors)
        {
            this.sources = sources;
            this.delayErrors = delayErrors;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            var parent = new ConcatCoordinator(observer, sources, delayErrors);
            parent.Next();
            return parent;
        }

        sealed class ConcatCoordinator : SingleConcatObserver<T>
        {
            readonly ISingleSource<T>[] sources;

            int index;

            internal ConcatCoordinator(IObserver<T> downstream, ISingleSource<T>[] sources, bool delayErrors) : base(downstream, delayErrors)
            {
                this.sources = sources;
            }

            internal override void Next()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                var srcs = sources;
                var n = srcs.Length;

                for (; ; )
                {
                    if (!IsDisposed())
                    {

                        if (!delayErrors)
                        {
                            var ex = errors;
                            if (ex != null)
                            {
                                DisposableHelper.WeakDispose(ref upstream);
                                downstream.OnError(ex);
                                continue;
                            }
                        }

                        var idx = index;

                        if (idx == n)
                        {
                            DisposableHelper.WeakDispose(ref upstream);
                            var ex = errors;
                            if (ex != null)
                            {
                                downstream.OnError(ex);
                            }
                            else
                            {
                                downstream.OnCompleted();
                            }

                            continue;
                        }

                        var src = srcs[idx];

                        if (src == null)
                        {
                            DisposableHelper.WeakDispose(ref upstream);
                            downstream.OnError(new NullReferenceException("The ISingleSource at index " + idx + " is null"));
                            continue;
                        }
                        else
                        {
                            index = idx + 1;
                            src.Subscribe(this);
                        }
                    }
                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Base class for subscribing to a sequence of sources
    /// once the previous terminated.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <remarks>Since 0.0.12</remarks>
    internal abstract class SingleConcatObserver<T> : ISingleObserver<T>, IDisposable
    {
        protected readonly IObserver<T> downstream;

        protected readonly bool delayErrors;

        protected IDisposable upstream;

        protected Exception errors;

        protected int wip;

        protected SingleConcatObserver(IObserver<T> downstream, bool delayErrors)
        {
            this.downstream = downstream;
            this.delayErrors = delayErrors;
        }

        public virtual void Dispose()
        {
            DisposableHelper.Dispose(ref upstream);
        }

        protected bool IsDisposed()
        {
            return DisposableHelper.IsDisposed(ref upstream);
        }

        public void OnError(Exception error)
        {
            if (delayErrors)
            {
                ExceptionHelper.AddException(ref errors, error);
            }
            else
            {
                errors = error;
            }
            Next();
        }

        public void OnSubscribe(IDisposable d)
        {
            DisposableHelper.Replace(ref upstream, d);
        }

        public void OnSuccess(T value)
        {
            downstream.OnNext(value);
            Next();
        }

        internal abstract void Next();
    }

    /// <summary>
    /// Run the source single and signal their success
    /// value in order, optionally delaying errors.
    /// </summary>
    /// <typeparam name="T">The success and item type.</typeparam>
    /// <remarks>Since 0.0.12</remarks>
    internal sealed class SingleConcatEnumerable<T> : IObservable<T>
    {
        readonly IEnumerable<ISingleSource<T>> sources;

        readonly bool delayErrors;

        public SingleConcatEnumerable(IEnumerable<ISingleSource<T>> sources, bool delayErrors)
        {
            this.sources = sources;
            this.delayErrors = delayErrors;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            var en = default(IEnumerator<ISingleSource<T>>);

            try
            {
                en = RequireNonNullRef(sources.GetEnumerator(), "The IEnumerable returned a null IEnumerator");
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
                return DisposableHelper.EMPTY;
            }

            var parent = new ConcatCoordinator(observer, en, delayErrors);
            parent.Next();
            return parent;
        }

        sealed class ConcatCoordinator : SingleConcatObserver<T>
        {
            IEnumerator<ISingleSource<T>> sources;

            internal ConcatCoordinator(IObserver<T> downstream, IEnumerator<ISingleSource<T>> sources, bool delayErrors) : base(downstream, delayErrors)
            {
                this.sources = sources;
            }

            public override void Dispose()
            {
                base.Dispose();
                Next();
            }

            internal override void Next()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                var srcs = sources;

                for (; ; )
                {
                    if (IsDisposed())
                    {
                        srcs?.Dispose();
                        sources = null;
                    }
                    else
                    {

                        if (!delayErrors)
                        {
                            var ex = errors;
                            if (ex != null)
                            {
                                DisposableHelper.WeakDispose(ref upstream);
                                downstream.OnError(ex);
                                continue;
                            }
                        }

                        var b = false;
                        var src = default(ISingleSource<T>);

                        try
                        {
                            b = srcs.MoveNext();
                            if (b)
                            {
                                src = srcs.Current;
                            }
                        }
                        catch (Exception ex)
                        {
                            DisposableHelper.WeakDispose(ref upstream);
                            downstream.OnError(ex);
                            continue;
                        }

                        if (!b)
                        {
                            DisposableHelper.WeakDispose(ref upstream);
                            var ex = errors;
                            if (ex != null)
                            {
                                downstream.OnError(ex);
                            }
                            else
                            {
                                downstream.OnCompleted();
                            }

                            continue;
                        }

                        if (src == null)
                        {
                            DisposableHelper.WeakDispose(ref upstream);
                            downstream.OnError(new NullReferenceException("The ISingleSource returned by the IEnumerator is null"));
                            continue;
                        }
                        else
                        {
                            src.Subscribe(this);
                        }
                    }
                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}
