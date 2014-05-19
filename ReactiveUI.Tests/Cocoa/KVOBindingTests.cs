using System;
using System.Reflection;
using Xunit;
using ReactiveUI.Cocoa;

namespace ReactiveUI.Tests
{
    public class FooController : ReactiveViewController, IViewFor<PropertyBindViewModel>
    {
        PropertyBindViewModel _ViewModel;
        public PropertyBindViewModel ViewModel {
            get { return _ViewModel; }
            set { this.RaiseAndSetIfChanged(ref _ViewModel, value); }
        }

        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (PropertyBindViewModel)value; }
        }
    }

    public class KVOBindingTests
    {
        [Fact]
        public void MakeSureKVOBindingsBindToKVOThings()
        {
            var input = new FooController();
            var fixture = new KVOObservableForExpression();

            Assert.NotEqual(0, fixture.GetAffinityForMember(typeof(FooController), typeof(FooController).GetRuntimeProperty("View")));
            Assert.Equal(0, fixture.GetAffinityForMember(typeof(FooController), typeof(FooController).GetRuntimeProperty("ViewModel")));
        }
    }
}
