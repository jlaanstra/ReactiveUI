using System;
using ReactiveUI;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Reactive.Disposables;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ReactiveUI.Cocoa
{
    public abstract class UIKitObservableForExpressionBase :
        ICreatesObservableForExpression
    {
        public int GetAffinityForMember(Type type, MemberInfo member, bool beforeChanged = false)
        {
            if (beforeChanged)
                return 0;
            
            var match = config.Keys
                .Where(x=> x.IsAssignableFrom(type) && config[x].Keys.Contains(member))
                .Select(x=> config[x][member])
                .OrderByDescending(x=> x.Affinity)
                .FirstOrDefault();

            if(match == null)
                return 0;

            return match.Affinity;
        }

        public IObservable<IObservedChange<object, object>> GetNotificationForExpression(object sender, Expression expression, bool beforeChanged = false)
        {
            if (beforeChanged)
                return Observable.Never<IObservedChange<object, object>>();

            var type = sender.GetType();
            var member = expression.GetMemberInfo();

            var match = config.Keys
                .Where(x=> x.IsAssignableFrom(type) && config[x].Keys.Contains(member))
                .Select(x=> config[x][member])
                .OrderByDescending(x=> x.Affinity)
                .FirstOrDefault();

            if(match == null)
                throw new NotSupportedException(string.Format("Notifications for {0}.{1} are not supported", type.Name, member.Name));

            return match.CreateObservable((NSObject) sender, expression);
        }

        internal class ObservablePropertyInfo
        {
            public int Affinity;
            public Func<NSObject, Expression, IObservable<IObservedChange<object, object>>> CreateObservable;
        }

        /// <summary>
        /// Configuration map
        /// </summary>
        readonly Dictionary<Type, Dictionary<MemberInfo, ObservablePropertyInfo>> config =
            new Dictionary<Type, Dictionary<MemberInfo, ObservablePropertyInfo>>();

        /// <summary>
        /// Registers an observable factory for the specified type and property.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <param name="property">Property.</param>
        /// <param name="createObservable">Create observable.</param>
        protected void Register(MemberInfo member, int affinity, Func<NSObject, Expression, IObservable<IObservedChange<object, object>>> createObservable)
        {
            Type type = member.DeclaringType;
            Dictionary<MemberInfo, ObservablePropertyInfo> typeProperties;
            if(!config.TryGetValue(type, out typeProperties))
            {
                typeProperties = new Dictionary<MemberInfo, ObservablePropertyInfo>();
                config[type] = typeProperties;
            }

            var info = new ObservablePropertyInfo { Affinity = affinity, CreateObservable = createObservable };
            typeProperties[member] = info;
        }

        /// <summary>
        /// Creates an Observable for a UIControl Event
        /// </summary>
        /// <returns>An observable</returns>
        /// <param name="sender">The sender</param>
        /// <param name="propertyName">The property name </param>
        /// <param name="evt">The control event to listen for</param>
        protected static IObservable<IObservedChange<object, object>> ObservableFromUIControlEvent(NSObject sender, Expression expression, UIControlEvent evt)
        {
            return Observable.Create<IObservedChange<object, object>>(subj =>
            {
                var control = (UIControl) sender;

                EventHandler handler = (s,e)=>
                {
                    subj.OnNext(new ObservedChange<object, object>(sender, expression));
                };

                control.AddTarget(handler, evt);

                return Disposable.Create(() =>
                {
                    control.RemoveTarget(handler, evt);
                });
            });
        }

        /// <summary>
        /// Creates an Observable for a NSNotificationCenter notification
        /// </summary>
        /// <returns>The from notification.</returns>
        /// <param name="sender">Sender.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="notification">Notification.</param>
        protected static IObservable<IObservedChange<object, object>> ObservableFromNotification(NSObject sender, Expression expression, NSString notification)
        {
            return Observable.Create<IObservedChange<object, object>>(subj =>
            {
                var handle = NSNotificationCenter.DefaultCenter.AddObserver (notification, (e)=>
                {
                    subj.OnNext(new ObservedChange<object, object>(sender, expression));
                }, sender);

                return Disposable.Create(() =>
                {
                    NSNotificationCenter.DefaultCenter.RemoveObserver(handle);
                });
            });
        }

        /// <summary>
        /// Creates an Observable for a NSNotificationCenter notification
        /// </summary>
        /// <returns>The from notification.</returns>
        /// <param name="sender">Sender.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="notification">Notification.</param>
        protected static IObservable<IObservedChange<object, object>> ObservableFromEvent(NSObject sender, Expression expression, string eventName)
        {
            return Observable.Create<IObservedChange<object, object>>(subj =>
            {
                return Observable.FromEventPattern(sender, eventName).Subscribe((e) =>
                {
                    subj.OnNext(new ObservedChange<object, object>(sender, expression));
                });
            });
        }
    }
}

