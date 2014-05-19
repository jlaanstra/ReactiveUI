using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Windows;
using ReactiveUI.Xaml;
using Xunit;

namespace ReactiveUI.Tests
{
    public class DepObjFixture : FrameworkElement
    {
        public static readonly DependencyProperty TestStringProperty = 
            DependencyProperty.Register("TestString", typeof(string), typeof(DepObjFixture), new PropertyMetadata(null));

        public string TestString {
            get { return (string)GetValue(TestStringProperty); }
            set { SetValue(TestStringProperty, value); }
        }
    }

    public class DerivedDepObjFixture : DepObjFixture, IViewFor
    {
        public string AnotherTestString {
            get { return (string)GetValue(AnotherTestStringProperty); }
            set { SetValue(AnotherTestStringProperty, value); }
        }

        public static readonly DependencyProperty AnotherTestStringProperty =
            DependencyProperty.Register("AnotherTestString", typeof(string), typeof(DerivedDepObjFixture), new PropertyMetadata(null));

        public object ViewModel
        {
            get { return this.GetValue(ViewModelProperty); }
            set { this.SetValue(ViewModelProperty, value); }
        }

        public static readonly DependencyProperty ViewModelProperty = 
            DependencyProperty.Register("ViewModel", typeof(object), typeof(DerivedDepObjFixture), new PropertyMetadata(null));
    }

    public class DependencyObjectObservableForPropertyTest
    {
        [Fact]
        public void DependencyObjectObservableForPropertySmokeTest()
        {
            var fixture = new DepObjFixture();
            var binder = new DependencyObjectObservableForExpression();

            Expression<Func<DepObjFixture, string>> expr = fi => fi.TestString;
            Assert.NotEqual(0, binder.GetAffinityForMember(fixture.GetType(), expr.Body.GetMemberInfo()));
            
            var results = new List<IObservedChange<object, object>>();
            var disp1 = binder.GetNotificationForExpression(fixture, expr.Body).Subscribe(results.Add);
            var disp2 = binder.GetNotificationForExpression(fixture, expr.Body).Subscribe(results.Add);

            fixture.TestString = "Foo";
            fixture.TestString = "Bar";

            Assert.Equal(4, results.Count);

            disp1.Dispose();
            disp2.Dispose();
        }

        [Fact]
        public void DependencyObjectObservableForInterfacePropertySmokeTest()
        {
            var fixture = new DerivedDepObjFixture();
            var binder = new DependencyObjectObservableForExpression();

            Expression<Func<IViewFor, object>> expr = fi => fi.ViewModel;
            Assert.NotEqual(0, binder.GetAffinityForMember(fixture.GetType(), expr.Body.GetMemberInfo()));

            var results = new List<IObservedChange<object, object>>();
            var disp1 = binder.GetNotificationForExpression(fixture, expr.Body).Subscribe(results.Add);
            var disp2 = binder.GetNotificationForExpression(fixture, expr.Body).Subscribe(results.Add);

            ((IViewFor)fixture).ViewModel = new object();

            Assert.Equal(2, results.Count);

            disp1.Dispose();
            disp2.Dispose();
        }

        [Fact]
        public void DerivedDependencyObjectObservableForPropertySmokeTest()
        {
            var fixture = new DerivedDepObjFixture();
            var binder = new DependencyObjectObservableForExpression();

            Expression<Func<DerivedDepObjFixture, string>> expr = fi => fi.TestString;
            Assert.NotEqual(0, binder.GetAffinityForMember(fixture.GetType(), expr.Body.GetMemberInfo()));

            var results = new List<IObservedChange<object, object>>();
            var disp1 = binder.GetNotificationForExpression(fixture, expr.Body).Subscribe(results.Add);
            var disp2 = binder.GetNotificationForExpression(fixture, expr.Body).Subscribe(results.Add);

            fixture.TestString = "Foo";
            fixture.TestString = "Bar";

            Assert.Equal(4, results.Count);

            disp1.Dispose();
            disp2.Dispose();
        }



        [Fact]
        public void WhenAnyWithDependencyObjectTest()
        {
            var inputs = new[] {"Foo", "Bar", "Baz"};
            var fixture = new DepObjFixture();

            var outputs = fixture.WhenAnyValue(x => x.TestString).CreateCollection();
            inputs.ForEach(x => fixture.TestString = x);

            Assert.Null(outputs.First());
            Assert.Equal(4, outputs.Count);
            Assert.True(inputs.Zip(outputs.Skip(1), (expected, actual) => expected == actual).All(x => x));
        }
    }
}