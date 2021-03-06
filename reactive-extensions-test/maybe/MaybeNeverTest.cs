﻿using NUnit.Framework;
using System;
using akarnokd.reactive_extensions;

namespace akarnokd.reactive_extensions_test.maybe
{
    [TestFixture]
    public class MaybeNeverTest
    {
        [Test]
        public void Basic()
        {
            MaybeSource.Never<int>()
                .Test()
                .AssertSubscribed()
                .AssertEmpty();
        }
    }
}
