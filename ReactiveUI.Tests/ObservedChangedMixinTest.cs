﻿using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Linq;
using System.Reactive.Subjects;
using Xunit;
using ReactiveUI.Testing;

using Microsoft.Reactive.Testing;
using System.Linq.Expressions;

namespace ReactiveUI.Tests
{
    public class ObservedChangedMixinTest
    {
        [Fact]
        public void GetValueShouldActuallyReturnTheValue()
        {
            var input = new[] {"Foo", "Bar", "Baz"};
            var output = new List<string>();

            (new TestScheduler()).With(sched => {
                var fixture = new TestFixture();
                
                // ...whereas ObservableForProperty *is* guaranteed to.
                fixture.ObservableForProperty(x => x.IsOnlyOneWord).Subscribe(x => {
                    output.Add(x.GetValue());
                });

                foreach (var v in input) { fixture.IsOnlyOneWord = v; }

                sched.AdvanceToMs(1000);

                input.AssertAreEqual(output);
            });
        }

        [Fact]
        public void GetValueShouldReturnTheValueFromAPath()
        {
            var input = new HostTestFixture() {
                Child = new TestFixture() {IsNotNullString = "Foo"},
            };

            Expression<Func<HostTestFixture, string>> expr = tf => tf.Child.IsNotNullString;
            var fixture = new ObservedChange<HostTestFixture, string>(input, expr.Body);

            Assert.Equal("Foo", fixture.GetValue());
        }        

        [Fact]
        public void BindToSmokeTest()
        {
            (new TestScheduler()).With(sched => {
                var input = new ScheduledSubject<string>(sched);
                var fixture = new HostTestFixture() {Child = new TestFixture()};

                input.BindTo(fixture, x => x.Child.IsNotNullString);

                Assert.Null(fixture.Child.IsNotNullString);

                input.OnNext("Foo");
                sched.Start();
                Assert.Equal("Foo", fixture.Child.IsNotNullString);

                input.OnNext("Bar");
                sched.Start();
                Assert.Equal("Bar", fixture.Child.IsNotNullString);
            });
        }

        [Fact]
        public void DisposingDisconnectsTheBindTo()
        {
            (new TestScheduler()).With(sched => {
                var input = new ScheduledSubject<string>(sched);
                var fixture = new HostTestFixture() {Child = new TestFixture()};

                var subscription = input.BindTo(fixture, x => x.Child.IsNotNullString);

                Assert.Null(fixture.Child.IsNotNullString);

                input.OnNext("Foo");
                sched.Start();
                Assert.Equal("Foo", fixture.Child.IsNotNullString);

                subscription.Dispose();

                input.OnNext("Bar");
                sched.Start();
                Assert.Equal("Foo", fixture.Child.IsNotNullString);
            });
        }

        [Fact]
        public void BindToIsNotFooledByIntermediateObjectSwitching()
        {
            (new TestScheduler()).With(sched => {
                var input = new ScheduledSubject<string>(sched);
                var fixture = new HostTestFixture() {Child = new TestFixture()};

                input.BindTo(fixture, x => x.Child.IsNotNullString);

                Assert.Null(fixture.Child.IsNotNullString);

                input.OnNext("Foo");
                sched.Start();
                Assert.Equal("Foo", fixture.Child.IsNotNullString);

                fixture.Child = new TestFixture();
                sched.Start();
                Assert.Equal("Foo", fixture.Child.IsNotNullString);

                input.OnNext("Bar");
                sched.Start();
                Assert.Equal("Bar", fixture.Child.IsNotNullString);
            });
        }

        [Fact]
        public void BindToStackOverFlowTest()
        {
            // Before the code changes packed in the same commit
            // as this test the test would go into an infinite 
            // event storm. The critical issue is that the
            // property StackOverflowTrigger will clone the
            // value before setting it.
            //
            // If this test executes through without hanging then
            // the problem has been fixed.
            (new TestScheduler()).With(sched => {
                var fixturea = new TestFixture();
                var fixtureb = new TestFixture();

                var source = new BehaviorSubject<List<string>>(new List<string>());

                source.BindTo(fixturea, x => x.StackOverflowTrigger);
            });
            
        }

    }
}
