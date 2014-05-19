using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using ReactiveUI;
using Splat;

namespace ReactiveUI
{
    public static class Reflection 
    {
        static readonly ExpressionRewriter rewriter = new ExpressionRewriter();

        static readonly MemoizingMRUCache<MemberInfo, Func<object, object[], object>> memberReaderCache =
            new MemoizingMRUCache<MemberInfo, Func<object, object[], object>>((x, _) =>
        {
            FieldInfo field = x as FieldInfo;
            if (field != null)
            {
                return (obj,__) => field.GetValue(obj);
            }

            PropertyInfo property = x as PropertyInfo;
            if (property != null)
            {
                return (obj, @params) => property.GetValue(obj, @params);
            }
            return null;
        }, RxApp.BigCacheLimit);

        static readonly MemoizingMRUCache<MemberInfo, Action<object, object, object[]>> propWriterCache = 
            new MemoizingMRUCache<MemberInfo, Action<object, object, object[]>>((x,_) => {
                FieldInfo field = x as FieldInfo;
                if (field != null)
                {
                    return (obj, value, __) => field.SetValue(obj, value);
                }

                PropertyInfo property = x as PropertyInfo;
                if (property != null)
                {
                    return (obj, value, indexers) => property.SetValue(obj, value, indexers);
                }

                return null;
            }, RxApp.BigCacheLimit);

        public static Expression RewriteExpression(Expression orig)
        {
            return rewriter.Visit(orig);
        }

        public static string SimpleExpressionToPropertyName(Expression expression)
        {
            Contract.Requires(expression != null);

            expression = RewriteExpression(expression);

            MemberExpression member = expression as MemberExpression;
            if (member != null && member.Expression.NodeType == ExpressionType.Parameter)
            {
                return member.Member.Name;
            }
            else
            {
                throw new ArgumentException("Property expression must be of the form 'x => x.SomeProperty.SomeOtherProperty'");
            }
        }

        public static string ExpressionToPropertyNames(Expression expression)
        {
            Contract.Requires(expression != null);

            StringBuilder sb = new StringBuilder();

            foreach (var exp in expression.GetExpressionChain())
            {
                if (exp.NodeType != ExpressionType.Parameter)
                {
                    // Indexer expression
                    if (exp.NodeType == ExpressionType.Index)
                    {
                        var ie = (IndexExpression)exp;
                        sb.Append(ie.Indexer.Name);
                        sb.Append('[');
                        foreach (var argument in ie.Arguments)
                        {
                            sb.Append(((ConstantExpression)argument).Value);
                            sb.Append(',');
                        }
                        sb.Replace(',',']',sb.Length - 1, 1);
                    }
                    else if (exp.NodeType == ExpressionType.MemberAccess)
                    {
                        var me = (MemberExpression)exp;
                        sb.Append(me.Member.Name);
                    }
                }
                sb.Append('.');
            }
            sb.Remove(sb.Length - 1, 1);

            return sb.ToString();
        }

        //public static MemberInfo[] ExpressionToMemberInfos<TObj, TRet>(Expression<Func<TObj, TRet>> property)
        //{
        //    var ret = new List<MemberInfo>();

        //    var current = property.Body;
        //    while (current.NodeType != ExpressionType.Parameter)
        //    {

        //        // This happens when a value type gets boxed
        //        if (current.NodeType == ExpressionType.Convert || current.NodeType == ExpressionType.ConvertChecked)
        //        {
        //            var ue = (UnaryExpression)current;
        //            current = ue.Operand;
        //            continue;
        //        }

        //        // Indexer expression
        //        if (current.NodeType == ExpressionType.Call)
        //        {
        //            var mce = (MethodCallExpression)current;
        //            // indexer expressions are special names
        //            if (!mce.Method.IsSpecialName)
        //            {
        //                throw new ArgumentException("Property expression must be of the form 'x => x.SomeProperty.SomeOtherProperty'");
        //            }
        //            ret.Insert(0, mce.Method);
        //            current = mce.Object;
        //            continue;
        //        }

        //        if (current.NodeType != ExpressionType.MemberAccess)
        //        {
        //            throw new ArgumentException("Property expression must be of the form 'x => x.SomeProperty.SomeOtherProperty'");
        //        }

        //        var me = (MemberExpression)current;
        //        ret.Insert(0, me.Member);
        //        current = me.Expression;
        //    }

        //    return ret.ToArray();
        //}

        //public static string[] expressiontopropertynames<tobj, tret>(expression<func<tobj, tret>> property)
        //{
        //    var ret = new list<string>();

        //    var current = property.body;
        //    while(current.nodetype != expressiontype.parameter) {

        //        // this happens when a value type gets boxed
        //        if (current.nodetype == expressiontype.convert || current.nodetype == expressiontype.convertchecked) {
        //            var ue = (unaryexpression) current;
        //            current = ue.operand;
        //            continue;
        //        }

        //        // indexer expression
        //        if (current.nodetype == expressiontype.call)
        //        {
        //            var mce = (methodcallexpression)current;
        //            // indexer expressions are special names
        //            if (!mce.method.isspecialname)
        //            {
        //                throw new argumentexception("property expression must be of the form 'x => x.someproperty.someotherproperty'");
        //            }
        //            ret.insert(0, mce.method.name + "(" + string.join<string>(" ", mce.arguments.cast<constantexpression>().select(c => c.value.tostring())) + ")");
        //            current = mce.object;
        //            continue;
        //        }

        //        if (current.nodetype != expressiontype.memberaccess) {
        //            throw new argumentexception("property expression must be of the form 'x => x.someproperty.someotherproperty'");
        //        }

        //        var me = (memberexpression)current;
        //        ret.insert(0, me.member.name);
        //        current = me.expression;
        //    }

        //    return ret.toarray();
        //}

        //public static Type[] ExpressionToPropertyTypes<TObj, TRet>(Expression<Func<TObj, TRet>> property)
        //{
        //    var current = property.Body;

        //    var startingType = ((ParameterExpression)current).Type;
        //    var memberInfos = ExpressionToMemberInfos(property);

        //    return GetTypesForPropChain(startingType, memberInfos);
        //}

        //public static Type[] GetTypesForPropChain(Type startingType, MemberInfo[] members)
        //{
        //    return members.Aggregate(new List<Type>(new[] {startingType}), (acc, x) => {
        //        var type = acc.Last();

        //        FieldInfo field = x as FieldInfo;
        //        if(field != null)
        //        {
        //            acc.Add(field.FieldType);
        //            return acc;
        //        }

        //        PropertyInfo property = x as PropertyInfo;
        //        if (property != null)
        //        {
        //            acc.Add(property.PropertyType);
        //            return acc;
        //        }

        //        MethodInfo method = x as MethodInfo;
        //        if (method != null)
        //        {
        //            acc.Add(method.ReturnType);
        //            return acc;
        //        }

        //        throw new ArgumentException("Member must be a field property or method.'");
        //    }).Skip(1).ToArray();
        //}     

        public static Func<TObj, object[], object> GetValueFetcherForProperty<TObj>(MemberInfo member) where TObj : class
        {
            var ret = GetValueFetcherForProperty(member);
            return ret;
        }

        public static Func<object, object[], object> GetValueFetcherForProperty(MemberInfo member)
        {
            Contract.Requires(member != null);

            lock (memberReaderCache) {
                return memberReaderCache.Get(member);
            }
        }

        public static Func<object, object[], object> GetValueFetcherOrThrow(MemberInfo member)
        {
            var ret = GetValueFetcherForProperty(member);

            if (ret == null) {
                throw new ArgumentException(String.Format("Type '{0}' must have a field, property or method '{1}'", member.DeclaringType, member.Name));
            }
            return ret;
        }

        public static Action<object, object, object[]> GetValueSetterForProperty(MemberInfo member)
        {
            Contract.Requires(member != null);

            lock (propWriterCache) {
                return propWriterCache.Get(member);
            }
        }

        public static Action<object, object, object[]> GetValueSetterOrThrow(MemberInfo member)
        {
            var ret = GetValueSetterForProperty(member);

            if (ret == null) {
                throw new ArgumentException(String.Format("Type '{0}' must have a property '{1}'", member.DeclaringType, member.Name));
            }
            return ret;
        }

        public static bool TryGetValueForExpressionChain<TValue>(object current, Expression expr, out TValue changeValue)
        {
            return TryGetValueForExpressionChain(current, expr, out changeValue);
        }

        public static bool TryGetValueForExpressionChain<TValue>(object current, IEnumerable<Expression> chain, out TValue changeValue)
        {
            foreach (var expression in chain.SkipLast(1)) {
                if (current == null) {
                    changeValue = default(TValue);
                    return false;
                }

                current = GetValueFetcherOrThrow(expression.GetMemberInfo())(current, expression.GetArgumentsArray());
            }

            if (current == null) {
                changeValue = default(TValue);
                return false;
            }

            Expression lastExpression = chain.Last();
            changeValue = (TValue) GetValueFetcherOrThrow(lastExpression.GetMemberInfo())(current, lastExpression.GetArgumentsArray());
            return true;
        }

        public static bool TryGetAllValuesForExpressionChain(object current, Expression expr, out IObservedChange<object, object>[] changeValues)
        {
            return TryGetAllValuesForExpressionChain(current, expr.GetExpressionChain(), out changeValues);
        }

        public static bool TryGetAllValuesForExpressionChain(object current, IEnumerable<Expression> chain, out IObservedChange<object, object>[] changeValues)
        {
            int currentIndex = 0;
            changeValues = new IObservedChange<object,object>[chain.Count()];

            foreach (var expression in chain.SkipLast(1)) {
                if (current == null) {
                    changeValues[currentIndex] = null;
                    return false;
                }

                var sender = current;
                current = GetValueFetcherOrThrow(expression.GetMemberInfo())(current, expression.GetArgumentsArray());
                var box = new ObservedChange<object, object>(sender, expression, current);

                changeValues[currentIndex] = box;
                currentIndex++;
            }

            if (current == null) {
                changeValues[currentIndex] = null;
                return false;
            }

            Expression lastExpression = chain.Last();
            changeValues[currentIndex] = new ObservedChange<object, object>(current, lastExpression, GetValueFetcherOrThrow(lastExpression.GetMemberInfo())(current, lastExpression.GetArgumentsArray()));
            return true;
        }

        public static bool TrySetValueToExpressionChain<TValue>(object target, Expression expr, TValue value, bool shouldThrow = true)
        {
            return TrySetValueToExpressionChain(target, expr.GetExpressionChain(), value, shouldThrow);
        }

        public static bool TrySetValueToExpressionChain<TValue>(object target, IEnumerable<Expression> props, TValue value, bool shouldThrow = true)
        {
            foreach (var prop in props.SkipLast(1)) {
                var getter = shouldThrow ?
                    GetValueFetcherOrThrow(prop.GetMemberInfo()) :
                    GetValueFetcherForProperty(prop.GetMemberInfo());

                target = getter(target, prop.GetArgumentsArray());
            }

            if (target == null) return false;

            Expression lastExpression = props.Last();
            var setter = shouldThrow ?
                GetValueSetterOrThrow(lastExpression.GetMemberInfo()) :
                GetValueSetterForProperty(lastExpression.GetMemberInfo());

            if (setter == null) return false;
            setter(target, value, lastExpression.GetArgumentsArray());
            return true;
        }

        static readonly MemoizingMRUCache<string, Type> typeCache = new MemoizingMRUCache<string, Type>((type,_) => {
            return Type.GetType(type, false);
        }, 20);

        public static Type ReallyFindType(string type, bool throwOnFailure) 
        {
            lock (typeCache) {
                var ret = typeCache.Get(type);
                if (ret != null || !throwOnFailure) return ret;
                throw new TypeLoadException();
            }
        }
    
        public static Type GetEventArgsTypeForEvent(Type type, string eventName)
        {
            var ti = type;
            var ei = ti.GetRuntimeEvent(eventName);
            if (ei == null) {
                throw new Exception(String.Format("Couldn't find {0}.{1}", type.FullName, eventName));
            }
    
            // Find the EventArgs type parameter of the event via digging around via reflection
            var eventArgsType = ei.EventHandlerType.GetRuntimeMethods().First(x => x.Name == "Invoke").GetParameters()[1].ParameterType;
            return eventArgsType;
        }

        internal static IObservable<TProp> ViewModelWhenAnyValue<TView, TViewModel, TProp>(TViewModel viewModel, TView view, Expression<Func<TViewModel, TProp>> property)
            where TView : IViewFor
            where TViewModel : class
        {
            return view.WhenAnyValue(x => x.ViewModel)
                .Where(x => x != null)
                .Select(x => ((TViewModel)x).WhenAnyValue(property))
                .Switch();
        }

        internal static IObservable<object> ViewModelWhenAnyValue<TView, TViewModel>(TViewModel viewModel, TView view, Expression chain)
            where TView : IViewFor
            where TViewModel : class
        {
            return view.WhenAny(x => x.ViewModel, x => x.Value)
                .Where(x => x != null)
                .Select(x => chain.NodeType == ExpressionType.Parameter ? Observable.Return(x) : ((TViewModel)x).WhenAnyDynamic(chain, y => y.Value))
                .Switch();
        }

        internal static Expression getViewExpression(object view, Expression vmExpression)
        {
            var controlProperty = view.GetType().GetRuntimeMember(vmExpression.GetMemberInfo().Name);
            if (controlProperty == null)
            {
                throw new Exception(String.Format("Tried to bind to control but it wasn't present on the object: {0}.{1}",
                    view.GetType().FullName, vmExpression.GetMemberInfo().Name));
            }

            return Expression.MakeMemberAccess(Expression.Parameter(view.GetType()), controlProperty);
        }

        internal static Expression getViewExpressionWithProperty(object view, Expression vmExpression)
        {
            var controlExpression = getViewExpression(view, vmExpression);

            var control = GetValueFetcherForProperty(controlExpression.GetMemberInfo())(view, controlExpression.GetArgumentsArray());
            if (control == null)
            {
                throw new Exception(String.Format("Tried to bind to control but it was null: {0}.{1}", view.GetType().FullName,
                    controlExpression.GetMemberInfo().Name));
            }

            var defaultProperty = DefaultPropertyBinding.GetPropertyForControl(control);
            if (defaultProperty == null) {
                throw new Exception(String.Format("Couldn't find a default property for type {0}", control.GetType()));
            }
            return Expression.MakeMemberAccess(controlExpression, defaultProperty);
        }
    }

    internal class NonInvokableIndexExpression : Expression
    {
        public NonInvokableIndexExpression(Expression instance, PropertyInfo indexer)
        {
            this.Object = instance;
            this.Indexer = indexer;
        }

        public override ExpressionType NodeType { get { return ExpressionType.Index; } }

        public Expression Object { get; private set; }
        
        public PropertyInfo Indexer { get; private set; }

        public override sealed Type Type
        {
            get {
                if (this.Indexer != null)
                {
                    return this.Indexer.PropertyType;
                }
                else
                {
                    return this.Object.Type.GetElementType();
                }
            }
        }

        protected override Expression Accept(ExpressionVisitor visitor)
        {
            throw new InvalidOperationException("This expression can not be visited.");
        }
    }
}
