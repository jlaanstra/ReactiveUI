using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReactiveUI
{
    public static class ReflectionMixins
    {
        public static bool IsStatic(this PropertyInfo This)
        {
            return (This.GetMethod ?? This.SetMethod).IsStatic;
        }

        public static MemberInfo GetRuntimeMember(this Type This, string propertyName)
        {
            return This.GetRuntimeField(propertyName) ?? (MemberInfo)This.GetRuntimeProperty(propertyName) ?? This.GetRuntimeMethod(propertyName, null);
        }
    }
}
