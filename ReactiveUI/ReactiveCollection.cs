using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveUI
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ReactiveCollection<T> : Collection<T>, IReactiveCollection<T>, INotifyPropertyChanged, INotifyCollectionChanged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReactiveCollection{T}" /> class.
        /// </summary>
        public ReactiveCollection()
        {
            this.SetupRx();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReactiveCollection{T}" /> class.
        /// </summary>
        /// <param name="items">The items.</param>
        public ReactiveCollection(IEnumerable<T> items)
        {
            this.SetupRx(items);
        }

        /// <summary>
        /// Sets up RX.
        /// </summary>
        /// <param name="items">The items.</param>
        private void SetupRx(IEnumerable<T> items = null)
        {           

            this.beforeItemsAdded = new ScheduledSubject<T>(RxApp.DeferredScheduler);
            this.beforeItemsRemoved = new ScheduledSubject<T>(RxApp.DeferredScheduler);
            this.itemsAdded = new ScheduledSubject<T>(RxApp.DeferredScheduler);
            this.itemsRemoved = new ScheduledSubject<T>(RxApp.DeferredScheduler);
                        
            this.CollectionCountChanging = Observable.Merge(
                BeforeItemsAdded.Select(_ => this.Count),
                BeforeItemsRemoved.Select(_ => this.Count)
            ).Where(_ => AreReplaceChangeNotificationsEnabled).DistinctUntilChanged();

            this.CollectionCountChanged = Observable.Merge(
                ItemsAdded.Select(_ => this.Count),
                ItemsRemoved.Select(_ => this.Count)
            ).Where(_ => AreReplaceChangeNotificationsEnabled).DistinctUntilChanged();

            //IsEmpty is changing from true to false when an item is about to be added so count is still 0
            //IsEmpty is changing from false to true when the last element is about to be removed
            this.CollectionIsEmptyChanging = Observable.Merge(
                BeforeItemsAdded.Where(_ => this.Count == 0).Select(_ => true),
                BeforeItemsRemoved.Where(_ => this.Count > 0).Select(_ => false)
            ).Where(_ => AreReplaceChangeNotificationsEnabled).DistinctUntilChanged();

            this.CollectionIsEmptyChanged = Observable.Merge(
                ItemsAdded.Select(_ => this.Count == 0),
                ItemsRemoved.Select(_ => this.Count == 0)
            ).Where(_ => AreReplaceChangeNotificationsEnabled).DistinctUntilChanged();

            //handle Item Changing and Changed notifications
            ItemsAdded.Subscribe(x =>
            {
                this.Log().Debug("Item Added to {0:X} - {1}", this.GetHashCode(), x);
                if (this.PropertyChangeWatchers != null)
                {
                    this.AddItemToPropertyTracking(x);
                }
            });

            ItemsRemoved.Subscribe(x =>
            {
                this.Log().Debug("Item removed from {0:X} - {1}", this.GetHashCode(), x);
                if (this.PropertyChangeWatchers != null && !this.PropertyChangeWatchers.ContainsKey(x))
                {
                    this.RemoveItemFromPropertyTracking(x);
                }
            });

            //make sure all subjects are initialized before start adding
            if (items != null)
            {
                foreach (T item in items)
                {
                    this.Add(item);
                }
            }
        }

        #region INotifyCollectionChanged

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Raises the <see cref="E:CollectionChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs" /> instance containing the event data.</param>
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (this.CollectionChanged != null)
            {
                this.CollectionChanged(this, e);
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="E:PropertyChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="PropertyChangedEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, e);
            }
        }

        #endregion

        #region INotifyPropertyChanging

        public event PropertyChangingEventHandler PropertyChanging;

        /// <summary>
        /// Raises the <see cref="E:PropertyChanging" /> event.
        /// </summary>
        /// <param name="e">The <see cref="PropertyChangingEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPropertyChanging(PropertyChangingEventArgs e)
        {
            if (this.PropertyChanging != null)
            {
                this.PropertyChanging(this, e);
            }
        }

        #endregion

        #region Overrides

        protected override void ClearItems()
        {
            T[] temp = new T[this.Items.Count];
            this.Items.CopyTo(temp, 0);
            this.Items.ForEach(i =>
            {
                this.beforeItemsRemoved.OnNext(i);
            });

            RxApp.DeferredScheduler.Schedule(() => base.ClearItems());

            temp.ForEach(i =>
            {
                this.itemsRemoved.OnNext(i);
            });
        }

        /// <summary>
        /// Inserts an element into the <see cref="T:System.Collections.ObjectModel.Collection`1" /> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
        /// <param name="item">The object to insert. The value can be null for reference types.</param>
        protected override void InsertItem(int index, T item)
        {
            this.beforeItemsAdded.OnNext(item);

            RxApp.DeferredScheduler.Schedule(() => base.InsertItem(index, item));

            this.itemsAdded.OnNext(item);
        }

        /// <summary>
        /// Removes the element at the specified index of the <see cref="T:System.Collections.ObjectModel.Collection`1" />.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        protected override void RemoveItem(int index)
        {
            T item = this.Items[index];
            this.beforeItemsRemoved.OnNext(item);

            RxApp.DeferredScheduler.Schedule(() => base.RemoveItem(index));

            this.itemsRemoved.OnNext(item);
        }

        protected override void SetItem(int index, T item)
        {
            T oldItem = this.Items[index];
            this.beforeItemsRemoved.OnNext(oldItem);
            this.beforeItemsAdded.OnNext(item);

            RxApp.DeferredScheduler.Schedule(() => base.SetItem(index, item));

            this.itemsRemoved.OnNext(oldItem);
            this.itemsAdded.OnNext(item);
        }

        #endregion

        protected ISubject<T> itemsAdded;

        /// <summary>
        /// Fires when items are added to the collection, once per item added.
        /// Functions that add multiple items such as AddRange should fire this
        /// multiple times. The object provided is the item that was added.
        /// </summary>
        public IObservable<T> ItemsAdded
        {
            get { return this.itemsAdded; }
        }
        IObservable<object> IReactiveCollection.ItemsAdded
        {
            get { return this.ItemsAdded.Select(x => (object)x); }
        }

        protected ISubject<T> beforeItemsAdded;

        /// <summary>
        /// Fires before an item is going to be added to the collection.
        /// When this fires the item is not yet part of the collection.
        /// </summary>
        public IObservable<T> BeforeItemsAdded
        {
            get { return this.beforeItemsAdded; }
        }
        IObservable<object> IReactiveCollection.BeforeItemsAdded
        {
            get { return this.BeforeItemsAdded.Select(x => (object)x); }
        }

        protected ISubject<T> itemsRemoved;

        /// <summary>
        /// Fires once an item has been removed from a collection, providing the
        /// item that was removed. Functions that remove multiple items such as Clear
        /// should call this multiple times.
        /// </summary>
        public IObservable<T> ItemsRemoved
        {
            get { return this.itemsRemoved; }
        }
        IObservable<object> IReactiveCollection.ItemsRemoved
        {
            get { return this.ItemsRemoved.Select(x => (object)x); }
        }

        protected ISubject<T> beforeItemsRemoved;
        /// <summary>
        /// Fires before an item will be removed from a collection, providing
        /// the item that will be removed. 
        /// When this fires the item is still part of the collection. 
        /// </summary>
        public IObservable<T> BeforeItemsRemoved
        {
            get { return this.beforeItemsRemoved; }
        }
        IObservable<object> IReactiveCollection.BeforeItemsRemoved
        {
            get { return this.BeforeItemsRemoved.Select(x => (object)x); }
        }

        protected ISubject<IObservedChange<T, object>> itemChanging;
        /// <summary>
        /// Provides Item Changing notifications for any item in collection that
        /// implements IReactiveNotifyPropertyChanged. This is only enabled when
        /// ChangeTrackingEnabled is set to True.
        /// </summary>
        public IObservable<IObservedChange<T, object>> ItemChanging
        {
            get { return this.itemChanging; }
        }
        IObservable<IObservedChange<object, object>> IReactiveCollection.ItemChanging
        {
            get { return (IObservable<IObservedChange<object, object>>)ItemChanging; }
        }

        protected ISubject<IObservedChange<T, object>> itemChanged;
        /// <summary>
        /// Provides Item Changed notifications for any item in collection that
        /// implements IReactiveNotifyPropertyChanged. This is only enabled when
        /// ChangeTrackingEnabled is set to True.
        /// </summary>
        public IObservable<IObservedChange<T, object>> ItemChanged
        {
            get { return this.itemChanged; }
        }
        IObservable<IObservedChange<object, object>> IReactiveCollection.ItemChanged
        {
            get { return (IObservable<IObservedChange<object, object>>)ItemChanged; }
        }

        #region Count

        public IObservable<int> CollectionCountChanged
        {
            get;
            private set;
        }

        public IObservable<int> CollectionCountChanging
        {
            get;
            private set;
        }

        #endregion

        #region IsEmpty

        /// <summary>
        /// Fires whenever the number of items in a collection has changed,
        /// providing the new Count.
        /// </summary>
        public IObservable<bool> CollectionIsEmptyChanged
        {
            get;
            private set;
        }

        /// <summary>
        /// Fires before a collection is about to change, providing the previous
        /// Count.
        /// </summary>
        public IObservable<bool> CollectionIsEmptyChanging
        {
            get;
            private set;
        }

        #endregion

        ///// <summary>
        ///// Represents an Observable that fires *before* a property is about to
        ///// be changed. Note that this should not fire duplicate change notifications if a
        ///// property is set to the same value multiple times.
        ///// </summary>
        //public IObservable<IObservedChange<object, object>> Changing
        //{
        //    get;
        //    private set;
        //}

        ///// <summary>
        ///// Represents an Observable that fires *after* a property has changed.
        ///// Note that this should not fire duplicate change notifications if a
        ///// property is set to the same value multiple times.
        ///// </summary>
        //public IObservable<IObservedChange<object, object>> Changed
        //{
        //    get;
        //    private set;
        //}

        /// <summary>
        /// Enables the ItemChanging and ItemChanged properties; when this is
        /// enabled, whenever a property on any object implementing
        /// IReactiveNotifyPropertyChanged changes, the change will be
        /// rebroadcast through ItemChanging/ItemChanged.
        /// </summary>
        public bool ChangeTrackingEnabled
        {
            get { return (PropertyChangeWatchers != null); }
            set
            {
                bool isEnabled = this.PropertyChangeWatchers != null;

                if (isEnabled == !value)
                {
                    ReleasePropChangeWatchers();
                    this.PropertyChangeWatchers = null;
                }
                else if (!isEnabled == value)
                {
                    this.PropertyChangeWatchers = new Dictionary<object, RefcountDisposeWrapper>();
                    foreach (var v in this)
                    {
                        AddItemToPropertyTracking(v);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the property change watchers.
        /// </summary>
        /// <value>
        /// The property change watchers.
        /// </value>
        private Dictionary<object, RefcountDisposeWrapper> PropertyChangeWatchers
        {
            get;
            set;
        }

        /// <summary>
        /// Releases the property change watchers.
        /// </summary>
        protected void ReleasePropChangeWatchers()
        {
            if (this.PropertyChangeWatchers != null)
            {
                foreach (var x in this.PropertyChangeWatchers.Values)
                {
                    x.Release();
                }
                this.PropertyChangeWatchers.Clear();
            }
        }

        /// <summary>
        /// Adds the item to property tracking.
        /// </summary>
        /// <param name="toTrack">To track.</param>
        protected void AddItemToPropertyTracking(T toTrack)
        {
            var item = toTrack as IReactiveNotifyPropertyChanged;
            if (item == null)
            {
                return;
            }

            if (this.PropertyChangeWatchers.ContainsKey(toTrack))
            {
                this.PropertyChangeWatchers[toTrack].AddRef();
                return;
            }

            var to_dispose = new[] {
                item.Changing.Subscribe(before_change =>
                    this.itemChanging.OnNext(new ObservedChange<T, object>() { 
                        Sender = toTrack, PropertyName = before_change.PropertyName })),
                item.Changed.Subscribe(change => 
                    this.itemChanged.OnNext(new ObservedChange<T,object>() { 
                        Sender = toTrack, PropertyName = change.PropertyName })),
            };

            this.PropertyChangeWatchers.Add(toTrack,
                new RefcountDisposeWrapper(Disposable.Create(() =>
                {
                    to_dispose[0].Dispose(); to_dispose[1].Dispose();
                    this.PropertyChangeWatchers.Remove(toTrack);
                })));
        }

        /// <summary>
        /// Removes the item from property tracking.
        /// </summary>
        /// <param name="toUntrack">To untrack.</param>
        protected void RemoveItemFromPropertyTracking(T toUntrack)
        {
            this.PropertyChangeWatchers[toUntrack].Release();
        }

        private int replaceNotificationsSuppressed = 0;

        /// <summary>
        /// When this method is called, an object will not fire change
        /// notifications (neither traditional nor Observable notifications)
        /// until the return value is disposed.
        /// </summary>
        /// <returns>An object that, when disposed, reenables change
        /// notifications.</returns>
        private IDisposable SuppressChangeNotifications()
        {
            Interlocked.Increment(ref replaceNotificationsSuppressed);
            return Disposable.Create(() =>
            {
                Interlocked.Decrement(ref replaceNotificationsSuppressed);
            });
        }

        /// <summary>
        /// Gets a value indicating whether [are replace change notifications enabled].
        /// </summary>
        /// <value>
        /// <c>true</c> if [are replace change notifications enabled]; otherwise, <c>false</c>.
        /// </value>
        protected bool AreReplaceChangeNotificationsEnabled
        {
            get
            {
                return replaceNotificationsSuppressed == 0;
            }
        }

        /// <summary>
        /// Indicates if a collection is empty or not.
        /// </summary>
        public bool IsEmpty
        {
            get { return this.Count == 0; }
        }

        /// <summary>
        /// Fires the <see cref="CollectionChanged" /> event with the <see cref="NotifyCollectionChangedAction.Reset" /> action on the collection.
        /// </summary>
        public void Reset()
        {
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    #region Extensions

    public static class ReactiveCollectionMixins
    {
        /// <summary>
        /// Creates a collection based on an an Observable by adding items
        /// provided until the Observable completes, optionally ensuring a
        /// delay. Note that if the Observable never completes and withDelay is
        /// set, this method will leak a Timer. This method also guarantees that
        /// items are always added via the UI thread.
        /// </summary>
        /// <param name="fromObservable">The Observable whose items will be put
        /// into the new collection.</param>
        /// <param name="withDelay">If set, items will be populated in the
        /// collection no faster than the delay provided.</param>
        /// <returns>A new collection which will be populated with the
        /// Observable.</returns>
        public static ReactiveCollection<T> CreateCollection<T>(
            this IObservable<T> fromObservable,
            TimeSpan? withDelay = null)
        {
            var ret = new ReactiveCollection<T>();
            if (withDelay == null)
            {
                fromObservable.ObserveOn(RxApp.DeferredScheduler).Subscribe(ret.Add);
                return ret;
            }

            // On a timer, dequeue items from queue if they are available
            var queue = new Queue<T>();
            var disconnect = Observable.Timer(withDelay.Value, withDelay.Value, RxApp.DeferredScheduler)
                .Subscribe(_ =>
                {
                    if (queue.Count > 0)
                    {
                        ret.Add(queue.Dequeue());
                    }
                });

            // When new items come in from the observable, stuff them in the queue.
            // Using the DeferredScheduler guarantees we'll always access the queue
            // from the same thread.
            fromObservable.ObserveOn(RxApp.DeferredScheduler).Subscribe(queue.Enqueue);

            // This is a bit clever - keep a running count of the items actually 
            // added and compare them to the final count of items provided by the
            // Observable. Combine the two values, and when they're equal, 
            // disconnect the timer
            ret.ItemsAdded.Scan(0, ((acc, _) => acc + 1)).Zip(fromObservable.Aggregate(0, (acc, _) => acc + 1),
                (l, r) => (l == r)).Where(x => x).Subscribe(_ => disconnect.Dispose());

            return ret;
        }

        /// <summary>
        /// Creates a collection based on an an Observable by adding items
        /// provided until the Observable completes, optionally ensuring a
        /// delay. Note that if the Observable never completes and withDelay is
        /// set, this method will leak a Timer. This method also guarantees that
        /// items are always added via the UI thread.
        /// </summary>
        /// <param name="fromObservable">The Observable whose items will be put
        /// into the new collection.</param>
        /// <param name="selector">A Select function that will be run on each
        /// item.</param>
        /// <param name="withDelay">If set, items will be populated in the
        /// collection no faster than the delay provided.</param>
        /// <returns>A new collection which will be populated with the
        /// Observable.</returns>
        public static ReactiveCollection<TRet> CreateCollection<T, TRet>(
            this IObservable<T> fromObservable,
            Func<T, TRet> selector,
            TimeSpan? withDelay = null)
        {
            Contract.Requires(selector != null);
            return fromObservable.Select(selector).CreateCollection(withDelay);
        }
    }

    public static class ObservableCollectionMixin
    {
        /// <summary>
        /// Creates a collection whose contents will "follow" another
        /// collection; this method is useful for creating ViewModel collections
        /// that are automatically updated when the respective Model collection
        /// is updated.
        ///
        /// Note that even though this method attaches itself to any 
        /// IEnumerable, it will only detect changes from objects implementing
        /// INotifyCollectionChanged (like ReactiveCollection). If your source
        /// collection doesn't implement this, signalReset is the way to signal
        /// the derived collection to reorder/refilter itself.
        /// </summary>
        /// <param name="selector">A Select function that will be run on each
        /// item.</param>
        /// <param name="filter">A filter to determine whether to exclude items 
        /// in the derived collection.</param>
        /// <param name="orderer">A comparator method to determine the ordering of
        /// the resulting collection.</param>
        /// <param name="signalReset">When this Observable is signalled, 
        /// the derived collection will be manually 
        /// reordered/refiltered.</param>
        /// <returns>A new collection whose items are equivalent to
        /// Collection.Select().Where().OrderBy() and will mirror changes 
        /// in the initial collection.</returns>
        public static ReactiveCollection<TNew> CreateDerivedCollection<T, TNew, TDontCare>(
            this IEnumerable<T> This,
            Func<T, TNew> selector,
            Func<T, bool> filter = null,
            Func<TNew, TNew, int> orderer = null,
            IObservable<TDontCare> signalReset = null)
        {
            Contract.Requires(selector != null);

            var collChanged = new Subject<NotifyCollectionChangedEventArgs>();

            if (selector == null)
            {
                selector = (x => (TNew)Convert.ChangeType(x, typeof(TNew), CultureInfo.CurrentCulture));
            }

            var origEnum = This;
            origEnum = (filter != null ? origEnum.Where(filter) : origEnum);
            var enumerable = origEnum.Select(selector);
            enumerable = (orderer != null ? enumerable.OrderBy(x => x, new FuncComparator<TNew>(orderer)) : enumerable);

            var ret = new ReactiveCollection<TNew>(enumerable);

            var incc = This as INotifyCollectionChanged;
            if (incc != null)
            {
                ((INotifyCollectionChanged)This).CollectionChanged += (o, e) => collChanged.OnNext(e);
            }

            if (filter != null && orderer == null)
            {
                throw new Exception("If you specify a filter, you must also specify an ordering function");
            }

            signalReset.Subscribe(_ => collChanged.OnNext(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));

            collChanged.Subscribe(args =>
            {
                if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    using (ret.SuppressChangeNotifications())
                    {
                        ret.Clear();
                        enumerable.ForEach(ret.Add);
                    }

                    ret.Reset();
                    return;
                }

                int oldIndex = (args.Action == NotifyCollectionChangedAction.Replace ?
                    args.NewStartingIndex : args.OldStartingIndex);

                if (args.OldItems != null)
                {
                    // NB: Tracking removes gets hard, because unless the items
                    // are objects, we have trouble telling them apart. This code
                    // is also tart, but it works.
                    foreach (T x in args.OldItems)
                    {
                        if (filter != null && !filter(x))
                        {
                            continue;
                        }
                        if (orderer == null)
                        {
                            ret.RemoveAt(oldIndex);
                            continue;
                        }
                        for (int i = 0; i < ret.Count; i++)
                        {
                            if (orderer(ret[i], selector(x)) == 0)
                            {
                                ret.RemoveAt(i);
                            }
                        }
                    }
                }

                if (args.NewItems != null)
                {
                    foreach (T x in args.NewItems)
                    {
                        if (filter != null && !filter(x))
                        {
                            continue;
                        }
                        if (orderer == null)
                        {
                            ret.Insert(args.NewStartingIndex, selector(x));
                            continue;
                        }

                        var toAdd = selector(x);
                        ret.Insert(positionForNewItem(ret, toAdd, orderer), toAdd);
                    }
                }
            });

            return ret;
        }

        /// <summary>
        /// Creates a collection whose contents will "follow" another
        /// collection; this method is useful for creating ViewModel collections
        /// that are automatically updated when the respective Model collection
        /// is updated.
        /// 
        /// Be aware that this overload will result in a collection that *only* 
        /// updates if the source implements INotifyCollectionChanged. If your
        /// list changes but isn't a ReactiveCollection/ObservableCollection,
        /// you probably want to use the other overload.
        /// </summary>
        /// <param name="selector">A Select function that will be run on each
        /// item.</param>
        /// <param name="filter">A filter to determine whether to exclude items 
        /// in the derived collection.</param>
        /// <param name="orderer">A comparator method to determine the ordering of
        /// the resulting collection.</param>
        /// <returns>A new collection whose items are equivalent to
        /// Collection.Select().Where().OrderBy() and will mirror changes 
        /// in the initial collection.</returns>
        public static ReactiveCollection<TNew> CreateDerivedCollection<T, TNew>(
            this IEnumerable<T> This,
            Func<T, TNew> selector,
            Func<T, bool> filter = null,
            Func<TNew, TNew, int> orderer = null)
        {
            return This.CreateDerivedCollection(selector, filter, orderer, Observable.Empty<Unit>());
        }

        static int positionForNewItem<T>(IList<T> list, T item, Func<T, T, int> orderer)
        {
            if (list.Count == 0)
            {
                return 0;
            }
            if (list.Count == 1)
            {
                return orderer(list[0], item) >= 0 ? 1 : 0;
            }

            // NB: This is the most tart way to do this possible
            int? prevCmp = null;
            int cmp;
            for (int i = 0; i < list.Count; i++)
            {
                cmp = orderer(list[i], item);
                if (prevCmp.HasValue && cmp != prevCmp)
                {
                    return i;
                }
                prevCmp = cmp;
            }

            return list.Count;
        }

        class FuncComparator<T> : IComparer<T>
        {
            Func<T, T, int> _inner;

            public FuncComparator(Func<T, T, int> comparer)
            {
                _inner = comparer;
            }

            public int Compare(T x, T y)
            {
                return _inner(x, y);
            }
        }
    }

    #endregion
}