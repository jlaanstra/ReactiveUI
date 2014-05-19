using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using Splat;

#if WINRT
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
#else
using System.Windows;
using System.Windows.Data;
#endif

namespace ReactiveUI.Xaml
{
    public class DependencyObjectObservableForExpression : ICreatesObservableForExpression
    {
        public int GetAffinityForMember(Type type, MemberInfo member, bool beforeChanged = false)
        {
            if (!typeof(DependencyObject).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo())) return 0;
            if (getDependencyPropertyFetcher(type, member) == null) return 0;

            return 4;
        }

        public IObservable<IObservedChange<object, object>> GetNotificationForExpression(object sender, System.Linq.Expressions.Expression expression, bool beforeChanged = false)
        {
            Contract.Requires(sender != null && sender is DependencyObject);
            var type = sender.GetType();
            var propertyName = expression.GetMemberInfo().Name;

            if (beforeChanged == true) {
                this.Log().Warn("Tried to bind DO {0}.{1}, but DPs can't do beforeChanged. Binding as POCO object",
                    type.FullName, propertyName);

                var ret = new POCOObservableForExpression();
                return ret.GetNotificationForExpression(sender,expression, beforeChanged);
            }

            var dpFetcher = getDependencyPropertyFetcher(sender.GetType(), expression.GetMemberInfo());
            if (dpFetcher == null) {
                this.Log().Warn("Tried to bind DO {0}.{1}, but DP doesn't exist. Binding as POCO object",
                    type.FullName, propertyName);

                var ret = new POCOObservableForExpression();
                return ret.GetNotificationForExpression(sender, expression, beforeChanged);
            }

            var dpAndSubj = createAttachedProperty(expression);

            return Observable.Create<IObservedChange<object, object>>(obs => {
                BindingOperations.SetBinding(sender as DependencyObject, dpAndSubj.Item1,
                    new Binding() { Source = sender as DependencyObject, Path = new PropertyPath(propertyName) });

                var disp = dpAndSubj.Item2
                    .Where(x => x == sender)
                    .Select(x => new ObservedChange<object, object>(x, expression))
                    .Subscribe(obs);
                // ClearBinding calls ClearValue http://stackoverflow.com/questions/1639219/clear-binding-in-silverlight-remove-data-binding-from-setbinding
                return new CompositeDisposable(Disposable.Create(() => (sender as DependencyObject).ClearValue(dpAndSubj.Item1)), disp);
            });
        }

        Func<DependencyProperty> getDependencyPropertyFetcher(Type type, MemberInfo member)
        {
            var typeInfo = type.GetTypeInfo();
            var name = member.Name;
#if WINRT
            // Look for the DependencyProperty attached to this property name
            var pi = actuallyGetProperty(typeInfo, name + "Property");
            if (pi != null) {
                return () => (DependencyProperty)pi.GetValue(null);
            }
#endif

            var fi = actuallyGetField(typeInfo, name + "Property");
            if (fi != null) {
                return () => (DependencyProperty)fi.GetValue(null);
            }

            return null;
        }

        PropertyInfo actuallyGetProperty(TypeInfo typeInfo, string propertyName)
        {
            var current = typeInfo;
            while (current != null) {
                var ret = typeInfo.GetDeclaredProperty(propertyName);
                if (ret != null && ret.IsStatic()) return ret;

                current = current.BaseType.GetTypeInfo();
            }

            return null;
        }

        FieldInfo actuallyGetField(TypeInfo typeInfo, string propertyName)
        {
            var current = typeInfo;
            while (current != null) {
                var ret = typeInfo.GetDeclaredField(propertyName);
                if (ret != null && ret.IsStatic) return ret;

                current = current.BaseType.GetTypeInfo();
            }

            return null;
        }

        static readonly Dictionary<MemberInfo, Tuple<DependencyProperty, Subject<object>>> attachedListener =
            new Dictionary<MemberInfo, Tuple<DependencyProperty, Subject<object>>>();

        Tuple<DependencyProperty, Subject<object>> createAttachedProperty(System.Linq.Expressions.Expression expression)
        {
            MemberInfo memberInfo = expression.GetMemberInfo();
            if (attachedListener.ContainsKey(memberInfo)) return attachedListener[memberInfo];

            var subj = new Subject<object>();

            // NB: There is no way to unregister an attached property, 
            // we just have to leak it. Luckily it's per-type, so it's
            // not *that* bad.
            var dp = DependencyProperty.RegisterAttached(
                "ListenAttached" + memberInfo.Name + this.GetHashCode().ToString("{0:x}"),
                typeof(object), expression.Type,
                new PropertyMetadata(null, (o, e) => subj.OnNext(o)));

            var ret = Tuple.Create(dp, subj);
            attachedListener[memberInfo] = ret;
            return ret;
        }
    }
}
