using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// A global linked undo transaction (<c>mdtGlobal</c>): every text-buffer edit made while
    /// it is open becomes a single Ctrl+Z unit, including edits of documents that were never
    /// shown as editor tabs (see <see cref="IVsLinkedUndoTransactionManager"/>).
    ///
    /// Must be opened and closed on the UI thread. Dispose closes the transaction; if the
    /// work failed, call <see cref="Abort"/> before Dispose so partial edits are rolled back
    /// instead of being committed as one undo unit.
    /// </summary>
    public sealed class LinkedUndoTransaction : IDisposable
    {
        private readonly IVsLinkedUndoTransactionManager _manager;
        private bool _closed;
        private bool _aborted;

        private LinkedUndoTransaction(
            IVsLinkedUndoTransactionManager manager
            )
        {
            _manager = manager;
        }

        /// <summary>
        /// Open a global linked undo transaction with the given description
        /// (the string shown in the Undo dropdown).
        /// </summary>
        public static LinkedUndoTransaction Open(
            string description
            )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (description is null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            var manager = Package.GetGlobalService(typeof(SVsLinkedUndoTransactionManager))
                as IVsLinkedUndoTransactionManager;
            if (manager is null)
            {
                throw new InvalidOperationException(
                    "SVsLinkedUndoTransactionManager is not available."
                    );
            }

            ErrorHandler.ThrowOnFailure(
                manager.OpenLinkedUndo(
                    (uint)LinkedTransactionFlags2.mdtGlobal,
                    description
                    )
                );

            return new LinkedUndoTransaction(manager);
        }

        /// <summary>
        /// Discard the transaction and roll back every linked edit made inside it.
        /// Prefer this after an unexpected failure. Do not use it for a user cancel:
        /// Abort rolls the changes back, and a cancel is supposed to keep what has
        /// already been applied.
        /// </summary>
        public void Abort()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_closed || _aborted)
            {
                return;
            }

            ErrorHandler.ThrowOnFailure(_manager.AbortLinkedUndo());
            _aborted = true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_closed || _aborted)
            {
                return;
            }

            ErrorHandler.ThrowOnFailure(_manager.CloseLinkedUndo());
            _closed = true;
        }
    }
}
