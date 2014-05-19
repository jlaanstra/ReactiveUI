namespace ReactiveUI.Winforms
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Forms;

    public class WinformsDefaultPropertyBinding : IDefaultPropertyBindingProvider
    {
        public Tuple<MemberInfo, int> GetPropertyForControl(object control)
        {
            // NB: These are intentionally arranged in priority order from most
            // specific to least specific.
            var items = new[] {

                typeof(RichTextBox).GetProperty("Text"),
                typeof(Label).GetProperty("Text"),
                typeof(Button).GetProperty("Text"),
                typeof(CheckBox).GetProperty("Checked"),
                typeof(TextBox).GetProperty("Text"),
                typeof(ProgressBar).GetProperty("Value"),
            };
           
            var type = control.GetType();
            var kvp = items.FirstOrDefault(x => x.DeclaringType.IsAssignableFrom(type));

            return kvp != null ? Tuple.Create<MemberInfo,int>(kvp, 5) : null;
        }
    }
}