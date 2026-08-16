using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Runtime.InteropServices;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Body provider which edits a xaml file through an invisible Visual Studio text buffer
    /// (<see cref="IVsInvisibleEditorManager"/>). There is no editor tab, but the change still
    /// participates in a global linked undo transaction and is therefore undoable with Ctrl+Z.
    ///
    /// Must be used on the UI thread. Dispose saves the buffer (if dirty) and releases the
    /// invisible editor; call it after <see cref="UpdateText"/> (or when abandoning a read).
    /// </summary>
    public sealed class InvisibleXamlBodyProvider : IXamlBodyProvider, IDisposable
    {
        private IVsInvisibleEditor? _invisibleEditor;
        private ITextBuffer? _textBuffer;
        private bool _disposed;

        /// <inheritdoc/>
        public string XamlFilePath
        {
            get;
        }

        private InvisibleXamlBodyProvider(
            string xamlFilePath,
            IVsInvisibleEditor invisibleEditor,
            ITextBuffer textBuffer
            )
        {
            XamlFilePath = xamlFilePath;
            _invisibleEditor = invisibleEditor;
            _textBuffer = textBuffer;
        }

        /// <summary>
        /// Open an invisible editor over the given file and return a provider bound to its
        /// text buffer. The caller owns the returned instance and must Dispose it.
        /// </summary>
        public static InvisibleXamlBodyProvider Open(
            string xamlFilePath
            )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }

            var invisibleEditorManager = Package.GetGlobalService(typeof(SVsInvisibleEditorManager))
                as IVsInvisibleEditorManager;
            if (invisibleEditorManager is null)
            {
                throw new InvalidOperationException(
                    "SVsInvisibleEditorManager is not available."
                    );
            }

            ErrorHandler.ThrowOnFailure(
                invisibleEditorManager.RegisterInvisibleEditor(
                    xamlFilePath,
                    pProject: null,
                    dwFlags: (uint)_EDITORREGFLAGS.RIEF_ENABLECACHING,
                    pFactory: null,
                    ppEditor: out var invisibleEditor
                    )
                );

            try
            {
                var vsTextLines = RetrieveDocData(invisibleEditor, needsSave: true);

                var componentModel = Package.GetGlobalService(typeof(SComponentModel))
                    as IComponentModel;
                if (componentModel is null)
                {
                    throw new InvalidOperationException("SComponentModel is not available.");
                }

                var adapterFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
                var textBuffer = adapterFactory.GetDocumentBuffer(vsTextLines);
                if (textBuffer is null)
                {
                    throw new InvalidOperationException(
                        $"Unable to obtain a text buffer for {xamlFilePath}."
                        );
                }

                return new InvisibleXamlBodyProvider(
                    xamlFilePath,
                    invisibleEditor,
                    textBuffer
                    );
            }
            catch
            {
                Marshal.ReleaseComObject(invisibleEditor);
                throw;
            }
        }

        /// <inheritdoc/>
        public string ReadText()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ThrowIfDisposed();

            return _textBuffer!.CurrentSnapshot.GetText();
        }

        /// <inheritdoc/>
        public void UpdateText(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ThrowIfDisposed();

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            using (var edit = _textBuffer!.CreateEdit())
            {
                edit.Delete(0, edit.Snapshot.Length);
                edit.Insert(0, text);
                edit.Apply();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _textBuffer = null;

            try
            {
                SaveIfDirty();
            }
            finally
            {
                if (_invisibleEditor is not null)
                {
                    Marshal.ReleaseComObject(_invisibleEditor);
                    _invisibleEditor = null;
                }
            }
        }

        private void SaveIfDirty()
        {
            var runningDocumentTable4 = Package.GetGlobalService(typeof(SVsRunningDocumentTable))
                as IVsRunningDocumentTable4;
            if (runningDocumentTable4 is null || !runningDocumentTable4.IsMonikerValid(XamlFilePath))
            {
                return;
            }

            var cookie = runningDocumentTable4.GetDocumentCookie(XamlFilePath);
            var runningDocumentTable = (IVsRunningDocumentTable)runningDocumentTable4;

            ErrorHandler.ThrowOnFailure(
                runningDocumentTable.ModifyDocumentFlags(
                    cookie,
                    (uint)_VSRDTFLAGS.RDT_DontAddToMRU,
                    fSet: 1
                    )
                );

            runningDocumentTable.SaveDocuments(
                (uint)__VSRDTSAVEOPTIONS.RDTSAVEOPT_SaveIfDirty,
                pHier: null,
                itemid: 0,
                docCookie: cookie
                );
        }

        private void ThrowIfDisposed()
        {
            if (_disposed || _textBuffer is null || _invisibleEditor is null)
            {
                throw new ObjectDisposedException(nameof(InvisibleXamlBodyProvider));
            }
        }

        private static IVsTextLines RetrieveDocData(
            IVsInvisibleEditor invisibleEditor,
            bool needsSave
            )
        {
            var ensureWritable = needsSave ? 1 : 0;
            var hr = invisibleEditor.GetDocData(
                fEnsureWritable: ensureWritable,
                riid: typeof(IVsTextLines).GUID,
                ppDocData: out var docDataPtr
                );

            try
            {
                if (ErrorHandler.Succeeded(hr)
                    && Marshal.GetObjectForIUnknown(docDataPtr) is IVsTextLines textLines)
                {
                    return textLines;
                }

                throw Marshal.GetExceptionForHR(hr)
                    ?? new InvalidOperationException("Unable to obtain IVsTextLines.");
            }
            finally
            {
                if (docDataPtr != IntPtr.Zero)
                {
                    Marshal.Release(docDataPtr);
                }
            }
        }
    }
}
