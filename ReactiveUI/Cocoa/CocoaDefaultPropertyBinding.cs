using System;
using System.Linq;
using System.Reflection;

#if UIKIT
using MonoTouch.UIKit;
#else
using MonoMac.AppKit;
#endif

namespace ReactiveUI.Cocoa
{
    /// <summary>
    /// Provides default property bindings for a number of common Cocoa controls.
    /// </summary>
    public class CocoaDefaultPropertyBinding : IDefaultPropertyBindingProvider
    {
        public Tuple<MemberInfo, int> GetPropertyForControl(object control)
        {
            // NB: These are intentionally arranged in priority order from most
            // specific to least specific.
#if UIKIT
            var items = new[] {
                typeof(UISlider).GetRuntimeProperty("Value"),
                typeof(UITextView).GetRuntimeProperty("Text"),
                typeof(UITextField).GetRuntimeProperty("Text"),
                typeof(UIButton).GetRuntimeProperty("Title"),
                typeof(UIImageView).GetRuntimeProperty("Image"),
            };
#else
            var items = new[] {
                typeof(NSSlider).GetRuntimeProperty("DoubleValue"),
                typeof(NSTextView).GetRuntimeProperty("Value",
                typeof(NSTextField).GetRuntimeProperty("StringValue",
                typeof(NSLevelIndicator).GetRuntimeProperty("DoubleValue",
                typeof(NSProgressIndicator).GetRuntimeProperty("DoubleValue"),
                typeof(NSButton).GetRuntimeProperty("Title"),
                typeof(NSImageView).GetRuntimeProperty("Image"),
            };
#endif

            var type = control.GetType();
            var member = items.FirstOrDefault(x => x.DeclaringType.IsAssignableFrom(type));

            return member != null ? Tuple.Create<MemberInfo,int>(member, 5) : null;
        }
    }
}
