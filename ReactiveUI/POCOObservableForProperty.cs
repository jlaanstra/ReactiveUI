using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using Splat;

namespace ReactiveUI
{
    /// <summary>
    /// This class is the final fallback for WhenAny, and will simply immediately
    /// return the value of the type at the time it was created. It will also 
    /// warn the user that this is probably not what they want to do
    /// </summary>
    public class POCOObservableForExpression : ICreatesObservableForExpression 
    {
        public int GetAffinityForMember(Type type, MemberInfo member, bool beforeChanged = false)
        {
            return 1;
        }

        static readonly Dictionary<Type, bool> hasWarned = new Dictionary<Type, bool>();
        public IObservable<IObservedChange<object, object>> GetNotificationForExpression(object sender, Expression expr, bool beforeChanged = false)
        {
            var type = sender.GetType();
            if (!hasWarned.ContainsKey(type)) {
                this.Log().Warn(
                    "{0} is a POCO type and won't send change notifications, WhenAny will only return a single value!",
                    type.FullName);
                hasWarned[type] = true;
            }

            return Observable.Return(new ObservedChange<object, object>(sender, expr), RxApp.MainThreadScheduler)
                .Concat(Observable.Never<IObservedChange<object, object>>());
        }
    }
}
