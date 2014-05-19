using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Disposables;

namespace ReactiveUI.Xaml
{
    public class DependencyObjectObservableForExpression : ICreatesObservableForExpression
    {
        public int GetAffinityForMember(Type type, MemberInfo member, bool beforeChanged = false)
        {
            if (!typeof(System.Windows.DependencyObject).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo())) return 0;
            return getDependencyProperty(type, member) != null ? 4 : 0;
        }

        public IObservable<IObservedChange<object, object>> GetNotificationForExpression(object sender, Expression expression, bool beforeChanged = false)
        {
            var dpd = DependencyPropertyDescriptor.FromProperty(getDependencyProperty(sender.GetType(), expression.GetMemberInfo()), sender.GetType());

            return Observable.Create<IObservedChange<object, object>>(subj => {
                var handler = new EventHandler((o, e) => {
                    subj.OnNext(new ObservedChange<object, object>(sender, expression));
                });

                dpd.AddValueChanged(sender, handler);
                return Disposable.Create(() => dpd.RemoveValueChanged(sender, handler));
            });
        }

        System.Windows.DependencyProperty getDependencyProperty(Type type, MemberInfo member)
        {
            var fi = type.GetTypeInfo().GetFields(BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(x => x.Name == member.Name + "Property" && x.IsStatic);

            if (fi != null) {
                return (System.Windows.DependencyProperty)fi.GetValue(null);
            }

            return null;
        }
    }
}
