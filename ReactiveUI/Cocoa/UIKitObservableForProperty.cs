using System;
using System.Reflection;
using ReactiveUI;
using System.Collections.Generic;
using MonoTouch.UIKit;
using System.Linq;
using MonoTouch.Foundation;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Reactive.Disposables;

namespace ReactiveUI.Cocoa
{
    public class UIKitObservableForExpression : UIKitObservableForExpressionBase
    {
        public static Lazy<UIKitObservableForExpression> Instance = new Lazy<UIKitObservableForExpression>();

        public UIKitObservableForExpression ()
        {
            Register(typeof(UIControl).GetRuntimeProperty("Value"), 20, (s, p)=> ObservableFromUIControlEvent(s, p, UIControlEvent.ValueChanged));
            Register(typeof(UITextField).GetRuntimeProperty("Text"), 30, (s, p) => ObservableFromNotification(s, p, UITextField.TextFieldTextDidChangeNotification));
            Register(typeof(UITextView).GetRuntimeProperty("Text"), 30, (s, p) => ObservableFromNotification(s, p, UITextView.TextDidChangeNotification));
            Register(typeof(UIDatePicker).GetRuntimeProperty("Date"), 30, (s, p)=> ObservableFromUIControlEvent(s, p, UIControlEvent.ValueChanged));
            Register(typeof(UISegmentedControl).GetRuntimeProperty("SelectedSegment"), 30, (s, p)=> ObservableFromUIControlEvent(s, p, UIControlEvent.ValueChanged));
            Register(typeof(UISwitch).GetRuntimeProperty("On"), 30, (s, p)=> ObservableFromUIControlEvent(s, p, UIControlEvent.ValueChanged));
            Register(typeof(UISegmentedControl).GetRuntimeProperty("SelectedSegment"), 30, (s, p)=> ObservableFromUIControlEvent(s, p, UIControlEvent.ValueChanged));
            
            // Warning: This will stomp the Control's delegate
            Register(typeof(UITabBar).GetRuntimeProperty("SelectedItem"), 30, (s, p) => ObservableFromEvent(s, p, "ItemSelected"));

            // Warning: This will stomp the Control's delegate
            Register(typeof(UISearchBar).GetRuntimeProperty("Text"), 30, (s, p) => ObservableFromEvent(s, p, "TextChanged"));
        }
    }
}

