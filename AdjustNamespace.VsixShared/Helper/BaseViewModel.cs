using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace AdjustNamespace.Helper
{
    /// <summary>
    /// Base class of the MVVM viewmodels of the extension.
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged, IDisposable
    {

        /// <summary>
        /// Property change notification for the WPF bindings.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Throw an exception (instead of a Debug.Fail) if a notification
        /// is raised for a property which does not exist. Debug only, see <see cref="VerifyPropertyName"/>.
        /// </summary>
        protected bool _throwOnInvalidPropertyName;

        protected BaseViewModel()
        {
        }

        /// <summary>
        /// Notify that every property of this viewmodel may have been changed.
        /// </summary>
        protected void OnPropertyChanged()
        {
            OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// Notify that the given property has been changed.
        /// </summary>
        /// <param name="propertyName">Name of the changed property; an empty string means `all of them`.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            VerifyPropertyName(propertyName);

            var handler = PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }

            //the commands of the viewmodels rely on CommandManager.RequerySuggested,
            //so their CanExecute has to be re-evaluated after every property change
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Debug check: the property with such a name really exists in this viewmodel.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public void VerifyPropertyName(string propertyName)
        {
            if (!string.IsNullOrEmpty(propertyName))
            {
                // Verify that the property name matches a real,  
                // public, instance property on this object.
                var propertiesList = TypeDescriptor.GetProperties(this);
                if (propertiesList[propertyName] == null)
                {
                    var msg = "Invalid property name: " + propertyName;

                    if (_throwOnInvalidPropertyName)
                    {
                        throw new Exception(msg);
                    }

                    Debug.Fail(msg);
                }
            }
        }

        /// <summary>
        /// Release the resources of the derived viewmodel. Does nothing by default.
        /// </summary>
        protected virtual void DisposeViewModel()
        {

        }

        #region Implementation of IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeViewModel();
        }

        #endregion
    }
}
