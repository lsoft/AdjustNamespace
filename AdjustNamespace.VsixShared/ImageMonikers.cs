using Microsoft.VisualStudio.Imaging.Interop;
using System;

namespace AdjustNamespace
{
    /// <summary>
    /// Image monikers of the extension, see Monikers.imagemanifest.
    /// </summary>
    public static class ImageMonikers
    {
        /// <summary>
        /// Logo of the extension.
        /// </summary>
        public static ImageMoniker Logo
        {
            get;
        } = new ImageMoniker
        {
            Guid = new Guid("872022f4-493a-4d7b-97f5-8b474662c341"),
            Id = 0
        };


    }
}
