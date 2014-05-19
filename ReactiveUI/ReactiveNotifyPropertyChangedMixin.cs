using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Linq;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using Splat;

namespace ReactiveUI
{
    public static class ReactiveNotifyPropertyChangedMixin
    {
        static ReactiveNotifyPropertyChangedMixin()
        {
            RxApp.EnsureInitialized();
        }

        /// <summary>
        /// ObservableForProperty returns an Observable representing the
        /// property change notifications for a specific property on a
        /// ReactiveObject. This method (unlike other Observables that return
        /// IObservedChange) guarantees that the Value property of
        /// the IObservedChange is set.
        /// </summary>
        /// <param name="expression">An Expression representing the property (i.e.
        /// 'x => x.SomeProperty.SomeOtherProperty'</param>
        /// <param name="beforeChange">If True, the Observable will notify
        /// immediately before a property is going to change.</param>
        /// <returns>An Observable representing the property change
        /// notifications for the given property.</returns>
        public static IObservable<IObservedChange<TSender, TValue>> ObservableForProperty<TSender, TValue>(
                this TSender This,
                Expression<Func<TSender, TValue>> expression,
                bool beforeChange = false,
                bool skipInitial = true)
        {
            if (This == null) {
                throw new ArgumentNullException("Sender");
            }

            var body = Reflection.RewriteExpression(expression.Body);

            /* x => x.Foo.Bar.Baz;
             * 
             * Subscribe to This, look for Foo
             * Subscribe to Foo, look for Bar
             * Subscribe to Bar, look for Baz
             * Subscribe to Baz, publish to Subject
             * Return Subject
             * 
             * If Bar changes (notification fires on Foo), resubscribe to new Bar
             *  Resubscribe to new Baz, publish to Subject
             * 
             * If Baz changes (notification fires on Bar),
             *  Resubscribe to new Baz, publish to Subject
             */

            return SubscribeToExpressionChain<TSender, TValue>(
                This,
                body,
                beforeChange,
                skipInitial);
        }

        /// <summary>
        /// ObservableForProperty returns an Observable representing the
        /// property change notifications for a specific property on a
        /// ReactiveObject. This method (unlike other Observables that return
        /// IObservedChange) guarantees that the Value property of
        /// the IObservedChange is set.
        /// </summary>
        /// <param name="propertyName">A string containing the property name</param>
        /// <param name="beforeChange">If True, the Observable will notify
        /// immediately before a property is going to change.</param>
        /// <returns>An Observable representing the property change
        /// notifications for the given property.</returns>
        public static IObservable<IObservedChange<TSender, TValue>> ObservableForProperty<TSender, TValue>(
            this TSender This,
            Expression expression,
            bool beforeChange = false,
            bool skipInitial = true)
        {
            expression = Reflection.RewriteExpression(expression);

            var values = notifyForProperty(This, expression, beforeChange);

            if (!skipInitial) {
                values = values.StartWith(new ObservedChange<object, object>(This, expression));
            }

            return values.Select(x => new ObservedChange<TSender, TValue>(This, expression, (TValue)x.GetValue()))
                 .DistinctUntilChanged(x => x.Value);
        }

        static IObservedChange<object, object> observedChangeFor(Expression expr, IObservedChange<object, object> sourceChange)
        {
            if (sourceChange.Value == null)
            {
                return new ObservedChange<object, object>(sourceChange.Value, expr); ;
            }
            else
            {
                object value;
                Reflection.TryGetValueForExpressionChain(sourceChange.Value, new[] { expr }, out value);
                return new ObservedChange<object, object>(sourceChange.Value, expr, value);
            }
        }

        static IObservable<IObservedChange<object, object>> nestedObservedChanges(Expression expr, IObservedChange<object, object> sourceChange, bool beforeChange)
        {
            // Make sure a change at a root node propogates events down
            var kicker = observedChangeFor(expr, sourceChange);

            // Handle null values in the chain
            if (sourceChange.Value == null)
            {
                return Observable.Return(kicker);
            }

            // Handle non null values in the chain
            return notifyForProperty(sourceChange.Value, expr, beforeChange)
                .Select(x => new ObservedChange<object, object>(x.Sender, expr, x.GetValue()))
                .StartWith(kicker);
        }

        internal static IObservable<IObservedChange<TSender, TValue>> SubscribeToExpressionChain<TSender, TValue>(
            this TSender source,
            Expression body,
            bool beforeChange = false,
            bool skipInitial = true)
        {
            IObservable<IObservedChange<object, object>> notifier =
                Observable.Return(new ObservedChange<object, object>(null, null, source));

            IEnumerable<Expression> chain = body.GetExpressionChain();
            notifier = chain.Aggregate(notifier, (n, expr) => n
                .Select(y => nestedObservedChanges(expr, y, beforeChange))
                .Switch());

            if (skipInitial)
            {
                notifier = notifier.Skip(1);
            }

            notifier = notifier.Where(x => x.Sender != null);

            var r = notifier.Select(x => new ObservedChange<TSender, TValue>(source, body, (TValue)x.GetValue()));

            return r.DistinctUntilChanged(x => x.Value);
        }

        static readonly MemoizingMRUCache<Tuple<Type, MemberInfo, bool>, ICreatesObservableForExpression> notifyFactoryCache =
            new MemoizingMRUCache<Tuple<Type, MemberInfo, bool>, ICreatesObservableForExpression>((t, _) => {
                return Locator.Current.GetServices<ICreatesObservableForExpression>()
                    .Aggregate(Tuple.Create(0, (ICreatesObservableForExpression)null), (acc, x) => {
                        int score = x.GetAffinityForMember(t.Item1, t.Item2);
                        return (score > acc.Item1) ? Tuple.Create(score, x) : acc;
                    }).Item2;
            }, RxApp.BigCacheLimit);

        static IObservable<IObservedChange<object, object>> notifyForProperty(object sender, Expression expr, bool beforeChange)
        {
            var result = default(ICreatesObservableForExpression);
            lock (notifyFactoryCache) {
                result = notifyFactoryCache.Get(Tuple.Create(sender.GetType(), expr.GetMemberInfo(), beforeChange));
            }

            if (result == null) {
                throw new Exception(
                    String.Format("Couldn't find a ICreatesObservableForProperty for {0}. This should never happen, your service locator is probably broken.", 
                    sender.GetType()));
            }
            
            return result.GetNotificationForExpression(sender, expr, beforeChange);
        }

        /// <summary>
        /// ObservableForProperty returns an Observable representing the
        /// property change notifications for a specific property on a
        /// ReactiveObject, running the IObservedChange through a Selector
        /// function.
        /// </summary>
        /// <param name="property">An Expression representing the property (i.e.
        /// 'x => x.SomeProperty'</param>
        /// <param name="selector">A Select function that will be run on each
        /// item.</param>
        /// <param name="beforeChange">If True, the Observable will notify
        /// immediately before a property is going to change.</param>
        /// <returns>An Observable representing the property change
        /// notifications for the given property.</returns>
        public static IObservable<TRet> ObservableForProperty<TSender, TValue, TRet>(
                this TSender This, 
                Expression<Func<TSender, TValue>> property, 
                Func<TValue, TRet> selector, 
                bool beforeChange = false)
            where TSender : class
        {           
            Contract.Requires(selector != null);
            return This.ObservableForProperty(property, beforeChange).Select(x => selector(x.Value));
        }
    }
}

// vim: tw=120 ts=4 sw=4 et :
