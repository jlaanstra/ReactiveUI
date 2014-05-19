using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI.Mobile;

namespace ReactiveUI
{
    public class Registrations : IWantsToRegisterStuff
    {
        public void Register(Action<Func<object>, Type> registerFunction)
        {            
            registerFunction(() => new INPCObservableForExpression(), typeof(ICreatesObservableForExpression));
            registerFunction(() => new IROObservableForExpression(), typeof(ICreatesObservableForExpression));
            registerFunction(() => new POCOObservableForExpression(), typeof(ICreatesObservableForExpression));
            registerFunction(() => new NullDefaultPropertyBindingProvider(), typeof(IDefaultPropertyBindingProvider));
            registerFunction(() => new EqualityTypeConverter(), typeof(IBindingTypeConverter));
            registerFunction(() => new StringConverter(), typeof(IBindingTypeConverter));
            registerFunction(() => new DefaultViewLocator(), typeof(IViewLocator));
            registerFunction(() => new DummySuspensionHost(), typeof(ISuspensionHost));
            registerFunction(() => new CanActivateViewFetcher(), typeof(IActivationForViewFetcher));
        }
    }
}
