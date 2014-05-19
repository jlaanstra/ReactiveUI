using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;

namespace ReactiveUI
{
    /// <summary>
    /// Generates Observables based on observing INotifyPropertyChanged objects
    /// </summary>
    public class INPCObservableForExpression : ICreatesObservableForExpression
    {
        public int GetAffinityForMember(Type type, MemberInfo member, bool beforeChanged)
        {
            var target = beforeChanged ? typeof (INotifyPropertyChanging) : typeof (INotifyPropertyChanged);
            return target.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()) ? 5 : 0;
        }

        public IObservable<IObservedChange<object, object>> GetNotificationForExpression(object sender, Expression expression, bool beforeChanged)
        {
            var before = sender as INotifyPropertyChanging;
            var after = sender as INotifyPropertyChanged;

            if (beforeChanged ? before == null : after == null) {
                return Observable.Never<IObservedChange<object, object>>();
            }

            MemberInfo memberInfo = expression.GetMemberInfo();
            if (beforeChanged) {
                var obs = Observable.FromEventPattern<PropertyChangingEventHandler, PropertyChangingEventArgs>(
                    x => before.PropertyChanging += x, x => before.PropertyChanging -= x);

                if (expression.NodeType == ExpressionType.Index)
                {
                    return obs.Where(x => x.EventArgs.PropertyName.Equals(memberInfo.Name + "[]"))
                        .Select(x => new ObservedChange<object, object>(sender, expression));
                }
                else
                {
                    return obs.Where(x => x.EventArgs.PropertyName.Equals(memberInfo.Name))
                    .Select(x => new ObservedChange<object, object>(sender, expression));
                }
            } else {
                var obs = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    x => after.PropertyChanged += x, x => after.PropertyChanged -= x);

                if (expression.NodeType == ExpressionType.Index)
                {
                    return obs.Where(x => x.EventArgs.PropertyName.Equals(memberInfo.Name + "[]"))
                    .Select(x => new ObservedChange<object, object>(sender, expression));
                }
                else
                {
                    return obs.Where(x => x.EventArgs.PropertyName.Equals(memberInfo.Name))
                    .Select(x => new ObservedChange<object, object>(sender, expression));
                }
            }
        }
    }
}