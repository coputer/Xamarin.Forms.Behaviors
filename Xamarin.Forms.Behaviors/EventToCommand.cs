using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Input;

namespace Xamarin.Forms.Behaviors
{

    public class EventToCommandBehavior: BindableBehavior<View>   
    {
        public static readonly BindableProperty EventNameProperty = BindableProperty.Create<EventToCommandBehavior, string>(p => p.EventName, null);
        public static readonly BindableProperty CommandProperty = BindableProperty.Create<EventToCommandBehavior, ICommand>(p => p.Command, null);
        public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create<EventToCommandBehavior, object>(p => p.CommandParameter, null);
        public static readonly BindableProperty CommandNameProperty = BindableProperty.Create<EventToCommandBehavior, string>(p => p.CommandName, null);
        public static readonly BindableProperty CommandNameContextProperty = BindableProperty.Create<EventToCommandBehavior, object>(p => p.CommandNameContext, null);
        public static readonly BindableProperty UseEventArgsAsParamProperty = BindableProperty.Create<EventToCommandBehavior, bool>(p => p.UseEventArgsAsParam, false);

        private Delegate _handler;
        private EventInfo _eventInfo;

        public string EventName
        {
            get { return (string) GetValue(EventNameProperty); }
            set { SetValue(EventNameProperty, value); }
        }

        public ICommand Command
        {
            get { return (ICommand) GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public object CommandParameter
        {
            get { return GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        public string CommandName
        {
            get { return (string) GetValue(CommandNameProperty); }
            set { SetValue(CommandNameProperty, value); }
        }

        public object CommandNameContext
        {
            get { return GetValue(CommandNameContextProperty); }
            set { SetValue(CommandNameContextProperty, value); }
        }

        public bool UseEventArgsAsParam
        {
            get { return (bool) GetValue(UseEventArgsAsParamProperty); }
            set { SetValue(UseEventArgsAsParamProperty, value); }
        }

        protected override void OnAttachedTo(View bindable)
        {
            base.OnAttachedTo(bindable);
            var events = bindable.GetType().GetRuntimeEvents().ToList();
            if (!events.Any()) return;
            _eventInfo = events.FirstOrDefault(e => e.Name == EventName);
            if (_eventInfo == null) throw new ArgumentException(string.Format("EventToCommand: Can't find any event named '{0}' on attached type", EventName));
            AddEventHandler(_eventInfo, bindable, OnFired, OnFiredWithEventArgs);
        }

        protected override void OnDetachingFrom(View bindable)
        {
            base.OnDetachingFrom(bindable);
            if (_handler != null) _eventInfo.RemoveEventHandler(bindable, _handler);
        }

        private void AddEventHandler(EventInfo info, object item, Action action, Action<object, object> action2)
        {
            //Got inspiration from here: http://stackoverflow.com/questions/9753366/subscribing-an-action-to-any-event-type-via-reflection
            var mi = info.EventHandlerType.GetRuntimeMethods().First(rtm => rtm.Name == "Invoke");
            var parameters = mi.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToList();

            Expression exp = UseEventArgsAsParam ? 
                Expression.Call(Expression.Constant(this), action2.GetMethodInfo(), parameters) : 
                Expression.Call(Expression.Constant(this), action.GetMethodInfo(), null);

            _handler = Expression.Lambda(info.EventHandlerType, exp, parameters).Compile();
            info.AddEventHandler(item, _handler);
        }

        private void OnFired()
        {
            if (!string.IsNullOrEmpty(CommandName))
            {
                if (Command == null) CreateRelativeBinding();
            }

            if (Command == null) throw new InvalidOperationException("No command available, Is Command properly properly set up?");

            if (Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }
        }

        private void OnFiredWithEventArgs(object param, object e)
        {
            if (!string.IsNullOrEmpty(CommandName))
            {
                if (Command == null) CreateRelativeBinding();
            }

            if (Command == null) throw new InvalidOperationException("No command available, Is Command properly properly set up?");

            if (Command.CanExecute(CommandParameter))
            {
                Command.Execute(e);
            }
        }

        private void CreateRelativeBinding()
        {
            if (CommandNameContext == null)
                throw new ArgumentNullException(@"CommandNameContext property cannot be null when using CommandName property, consider using CommandNameContext={{b:RelativeContext [ElementName]}} markup markup extension.");
            if (Command != null) throw new InvalidOperationException("Both Command and CommandName properties specified, only one mode supported.");
            var pi = CommandNameContext.GetType().GetRuntimeProperty(CommandName);
            if (pi == null) throw new ArgumentNullException(string.Format("Can't find a command named '{0}'", CommandName));
            Command = pi.GetValue(CommandNameContext) as ICommand;
            if (Command == null) throw new ArgumentNullException(string.Format("Can't create binding with CommandName '{0}'", CommandName));
        }
    }
}
