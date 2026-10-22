using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace TheCafePOS_WPF.Views
{
    /// <summary>
    /// Creates windows and dialog helpers with their services injected; runtime arguments (a product, an amount) are passed explicitly.
    /// </summary>
    public interface IDialogFactory
    {
        T Create<T>(Window? owner, params object[] args) where T : class;
    }

    public sealed class DialogFactory : IDialogFactory
    {
        private readonly IServiceProvider _services;

        public DialogFactory(IServiceProvider services) => _services = services;

        public T Create<T>(Window? owner, params object[] args) where T : class
        {
            T created;
            try { created = ActivatorUtilities.CreateInstance<T>(_services, args); }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Constructors reject the caller (e.g. not a manager) with a message meant for the cashier; keep it.
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
            if (owner is not null && created is Window window) window.Owner = owner;
            return created;
        }
    }
}
