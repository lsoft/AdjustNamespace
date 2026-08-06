using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AdjustNamespace.UI.Control
{
    /// <summary>
    /// A behavior trigger which fires on the Space key only.
    /// It is used to invert the checkboxes of the selected items with the keyboard.
    /// </summary>
    public class SpaceKeyDownEventTrigger : EventTrigger
    {

        public SpaceKeyDownEventTrigger()
            : base("KeyUp")
        {
        }

        /// <inheritdoc/>
        protected override void OnEvent(EventArgs eventArgs)
        {
            var e = eventArgs as KeyEventArgs;
            if (e != null && e.Key == Key.Space)
                this.InvokeActions(eventArgs);
        }
    }
}
