using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows.Input;
using ReactiveUI;

namespace ReactiveUI.Xaml
{
    /// <summary>
    /// IReactiveCommand represents an ICommand which also notifies when it is
    /// executed (i.e. when Execute is called) via IObservable. Conceptually,
    /// this represents an Event, so as a result this IObservable should never
    /// OnComplete or OnError.
    /// 
    /// In previous versions of ReactiveUI, this interface was split into two
    /// separate interfaces, one to handle async methods and one for "standard"
    /// commands, but these have now been merged - every ReactiveCommand is now
    /// a ReactiveAsyncCommand.
    /// </summary>
    public interface IReactiveCommand : IHandleObservableErrors, IObservable<object>, ICommand, IDisposable, IEnableLogger
    {
        /// <summary>
        /// Registers an asynchronous method to be called whenever the command
        /// is Executed. This method returns an IObservable representing the
        /// asynchronous operation, and is allowed to OnError / should OnComplete.
        /// </summary>
        /// <returns>A filtered version of the Observable which is marshaled 
        /// to the UI thread. This Observable should only report successes and
        /// instead send OnError messages to the ThrownExceptions property.
        /// </returns>
        /// <param name="asyncBlock">The asynchronous method to call.</param>
        IObservable<T> RegisterAsync<T>(Func<object, IObservable<T>> asyncBlock);

        /// <summary>
        /// Gets a value indicating whether this instance can execute observable.
        /// </summary>
        /// <value><c>true</c> if this instance can execute observable; otherwise, <c>false</c>.</value>
        IObservable<bool> CanExecuteObservable { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is executing. This 
        /// Observable is guaranteed to always return a value immediately (i.e.
        /// it is backed by a BehaviorSubject), meaning it is safe to determine
        /// the current state of the command via IsExecuting.First()
        /// </summary>
        /// <value><c>true</c> if this instance is executing; otherwise, <c>false</c>.</value>
        IObservable<bool> IsExecuting { get; }

        /// <summary>
        /// Gets a value indicating whether this 
        /// <see cref="ReactiveUI.IReactiveCommand"/> allows concurrent 
        /// execution. If false, the CanExecute of the command will be disabled
        /// while async operations are currently in-flight.
        /// </summary>
        /// <value><c>true</c> if allows concurrent execution; otherwise, <c>false</c>.</value>
        bool AllowsConcurrentExecution { get; }
    }
}

// vim: tw=120 ts=4 sw=4 et :