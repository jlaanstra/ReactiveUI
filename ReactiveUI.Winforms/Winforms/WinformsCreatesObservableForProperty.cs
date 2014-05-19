using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows.Forms;
using Splat;

namespace ReactiveUI.Winforms
{
    public class WinformsCreatesObservableForProperty : ICreatesObservableForExpression
    {
        static readonly MemoizingMRUCache<MemberInfo, EventInfo> eventInfoCache = new MemoizingMRUCache<MemberInfo, EventInfo>((member, _) => {
            return member.DeclaringType.GetEvents(System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .FirstOrDefault(x => x.Name == member.Name + "Changed");
        }, RxApp.SmallCacheLimit);

        public int GetAffinityForMember(Type type, MemberInfo member, bool beforeChanged = false)
        {
            bool supportsTypeBinding = typeof(Control).IsAssignableFrom(type);
            if (!supportsTypeBinding) return 0;

            lock (eventInfoCache) {
                var ei = eventInfoCache.Get(member);
                return (beforeChanged == false && ei != null) ?  8 : 0;
            }
        }

        public IObservable<IObservedChange<object, object>> GetNotificationForExpression(object sender, Expression expression, bool beforeChanged = false)
        {
            var type = sender.GetType();
            var ei = default(EventInfo);
            var getter = Reflection.GetValueFetcherOrThrow(expression.GetMemberInfo());

            lock (eventInfoCache) {
                ei = eventInfoCache.Get(expression.GetMemberInfo());
            }

            return Observable.Create<IObservedChange<object, object>>(subj => {
                bool completed = false;
                var handler = new EventHandler((o, e) => {
                    if (completed) return;
                    try {
                        subj.OnNext(new ObservedChange<object, object>(sender, expression, getter(sender, null)));
                    } catch (Exception ex) {
                        subj.OnError(ex);
                        completed = true;
                    }
                });

                ei.AddEventHandler(sender, handler);
                return Disposable.Create(() => ei.RemoveEventHandler(sender, handler));
            });
        }
    }
}
