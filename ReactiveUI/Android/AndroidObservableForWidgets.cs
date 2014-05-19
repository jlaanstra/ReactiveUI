using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Collections.Generic;
using Android.Views;
using Android.Widget;
using System.Reactive;
using Android.Text;
using Java.Util;
using Observable = System.Reactive.Linq.Observable;
using System.Reflection;

namespace ReactiveUI.Android
{
    /// <summary>
    /// Android view objects are not Generally Observable™, so hard-code some
    /// particularly useful types.
    /// </summary>
    public class AndroidObservableForWidgets : ICreatesObservableForExpression
    {
        static readonly IDictionary<MemberInfo, Func<object, IObservable<IObservedChange<object, object>>>> dispatchTable;
        static AndroidObservableForWidgets()
        {
            dispatchTable = new[] { 
                createFromWidget<TextView, TextChangedEventArgs>(v => v.Text, (v, h) => v.TextChanged += h, (v, h) => v.TextChanged -= h),
                createFromWidget<NumberPicker, NumberPicker.ValueChangeEventArgs>(v => v.Value, (v, h) => v.ValueChanged += h, (v, h) => v.ValueChanged -= h),
                createFromWidget<RatingBar, RatingBar.RatingBarChangeEventArgs>(v => v.Rating, (v, h) => v.RatingBarChange += h, (v, h) => v.RatingBarChange -= h),
                createFromWidget<CompoundButton, CompoundButton.CheckedChangeEventArgs>(v => v.Checked, (v, h) => v.CheckedChange += h, (v, h) => v.CheckedChange -= h),
                createFromWidget<CalendarView, CalendarView.DateChangeEventArgs>(v => v.Date, (v, h) => v.DateChange += h, (v, h) => v.DateChange -= h),
                createFromWidget<TabHost, TabHost.TabChangeEventArgs>(v => v.CurrentTab, (v, h) => v.TabChanged += h, (v, h) => v.TabChanged -= h),
                createFromWidget<TimePicker, TimePicker.TimeChangedEventArgs>(v => v.CurrentHour, (v, h) => v.TimeChanged += h, (v, h) => v.TimeChanged -= h),
                createFromWidget<TimePicker, TimePicker.TimeChangedEventArgs>(v => v.CurrentMinute, (v, h) => v.TimeChanged += h, (v, h) => v.TimeChanged -= h),
                createFromAdapterView(),
            }.ToDictionary(k => k.Expression.GetMemberInfo(), v => v.Func);
        }

        public int GetAffinityForMember(Type type, MemberInfo member, bool beforeChanged = false)
        {
            if (beforeChanged) return 0;
            return dispatchTable.Keys.Any(x => x.DeclaringType.IsAssignableFrom(type) && x == member) ? 5 : 0;
        }

        public IObservable<IObservedChange<object, object>> GetNotificationForExpression(object sender, Expression expression, bool beforeChanged = false)
        {
            var type = sender.GetType();
            var tableItem = dispatchTable.Keys.First(x => x.DeclaringType.IsAssignableFrom(type) && x == expression.GetMemberInfo());

            return dispatchTable[tableItem](sender);
        }

        class DispatchTuple
        {
            public Expression Expression { get; set; }
            public Func<object, IObservable<IObservedChange<object, object>>> Func { get; set; } 
        }

        static DispatchTuple createFromAdapterView()
        {
            // AdapterView is more complicated because there are two events - one for select and one for deselect
            // respond to both

            Expression expr = Expression.MakeMemberAccess(Expression.Default(typeof(AdapterView)), typeof(AdapterView).GetProperty("SelectedItem"));
            return new DispatchTuple
            {
                Expression = expr,
                Func = x =>
                {
                    var v = (AdapterView)x;
                    var getter = Reflection.GetValueFetcherOrThrow(expr.GetMemberInfo());

                    return 
                        Observable.Merge(
                            Observable.FromEventPattern<AdapterView.ItemSelectedEventArgs>(h => v.ItemSelected += h, h => v.ItemSelected -=h)
                                .Select(_ => new ObservedChange<object, object>(v, expr, getter(v, null))),
                            Observable.FromEventPattern<AdapterView.NothingSelectedEventArgs>(h => v.NothingSelected += h, h => v.NothingSelected -= h)
                                .Select(_ => new ObservedChange<object, object>(v, expr))
                        );
                }
            };
        }

        static DispatchTuple createFromWidget<TView, TEventArgs>(Expression<Func<TView, object>> property, Action<TView, EventHandler<TEventArgs>> addHandler, Action<TView, EventHandler<TEventArgs>> removeHandler)
            where TView : View
            where TEventArgs : EventArgs
        {
            var body = property.Body;

            if (body.GetParent().NodeType != ExpressionType.Parameter) {
                throw new ArgumentException("property must be in the form 'x => x.SomeValue'", "property");
            }

            return new DispatchTuple {
                Expression = body,
                Func = x => {
                    var v = (TView)x;
                    var getter = Reflection.GetValueFetcherOrThrow(body.GetMemberInfo());

                    return Observable.FromEventPattern<TEventArgs>(h => addHandler(v, h) , h => removeHandler(v, h))
                        .Select(_ => new ObservedChange<object, object>(v, body, getter(v, null))); 
                }
            };
        }
    }
}

