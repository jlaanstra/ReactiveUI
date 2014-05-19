using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

#if WINRT
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
#else
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
#endif

namespace ReactiveUI.Xaml
{
    public class XamlDefaultPropertyBinding : IDefaultPropertyBindingProvider
    {
        public Tuple<MemberInfo, int> GetPropertyForControl(object control)
        {
            // NB: These are intentionally arranged in priority order from most
            // specific to least specific.
            var items = new[] {
#if !WINRT
                typeof(RichTextBox).GetTypeInfo().GetDeclaredProperty("Document"),
#endif
                typeof(Slider).GetRuntimeProperty("Value"),
#if !SILVERLIGHT && !WINRT
                typeof(Expander).GetTypeInfo().GetDeclaredProperty("IsExpanded"),
#endif 
                typeof(ToggleButton).GetTypeInfo().GetDeclaredProperty("IsChecked"),
                typeof(TextBox).GetTypeInfo().GetDeclaredProperty("Text"),
                typeof(TextBlock).GetTypeInfo().GetDeclaredProperty("Text"),
                typeof(ProgressBar).GetRuntimeProperty("Value"),
                typeof(ItemsControl).GetTypeInfo().GetDeclaredProperty("ItemsSource"),
                typeof(Image).GetTypeInfo().GetDeclaredProperty("Source"),
                typeof(ContentControl).GetRuntimeProperty("Content"),
                typeof(FrameworkElement).GetRuntimeProperty("Visibility"),
            };

            var type = control.GetType();
            var kvp = items.FirstOrDefault(x => x.DeclaringType.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()));

            return kvp != null ? Tuple.Create<MemberInfo,int>(kvp, 5) : null;
        }
    }
}