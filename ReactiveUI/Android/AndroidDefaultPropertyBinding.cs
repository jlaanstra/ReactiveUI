using System;
using System.Linq;
using ReactiveUI;
using Android.Widget;
using System.Reflection;

namespace ReactiveUI.Android
{
    /// <summary>
    /// Default property bindings for common Android widgets
    /// </summary>
    public class AndroidDefaultPropertyBinding : IDefaultPropertyBindingProvider
    {
        public Tuple<MemberInfo, int> GetPropertyForControl(object control)
        {
            // NB: These are intentionally arranged in priority order from most
            // specific to least specific.
            var items = new[] {
                typeof(TextView).GetProperty("Text"),
                typeof(ImageView).GetProperty("Drawable"),
                typeof(ProgressBar).GetProperty("Progress"),
                typeof(CompoundButton).GetProperty("Checked"),
            };

            var type = control.GetType();
            var kvp = items.FirstOrDefault(x => x.DeclaringType.IsAssignableFrom(type));

            return kvp != null ? Tuple.Create<MemberInfo,int>(kvp, 5) : null;
        }
    }
}