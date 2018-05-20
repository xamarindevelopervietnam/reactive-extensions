﻿using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Text;

namespace akarnokd.reactive_extensions
{
    /// <summary>
    /// Signals 0L after a specified time elapsed on the given scheduler.
    /// </summary>
    /// <remarks>Since 0.0.11</remarks>
    internal sealed class MaybeTimer : IMaybeSource<long>
    {
        readonly TimeSpan time;

        readonly IScheduler scheduler;

        static readonly Func<IScheduler, IMaybeObserver<long>, IDisposable> COMPLETE =
            (s, o) => { o.OnSuccess(0L); return DisposableHelper.EMPTY; };

        public MaybeTimer(TimeSpan time, IScheduler scheduler)
        {
            this.time = time;
            this.scheduler = scheduler;
        }

        public void Subscribe(IMaybeObserver<long> observer)
        {
            var sad = new SingleAssignmentDisposable();
            observer.OnSubscribe(sad);

            sad.Disposable = scheduler.Schedule(observer, time, COMPLETE);
        }
    }
}
