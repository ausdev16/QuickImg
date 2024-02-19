using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickImg
{
    /// <summary>
    /// Supported Themes.
    /// Used by the Main Page to specify the current and/or next theme.
    /// </summary>
    public enum Theme
    {
        Light, 
        Dark, 
        Default
    }

    /// <summary>
    /// This is the image source type.
    /// Bitmap supports most rasterized types, e.g. JPEG, PNG, GIF, etc.
    /// SVG supports SVG only.
    /// </summary>
    public enum ImageType 
    {
        Bitmap,
        SVG
    }

    /// <summary>
    /// The current View Mode for the Scroll Viewer / Layout Transform (used as the Image Container to support rotation).
    /// </summary>
    public enum ViewMode
    {
        ActualPixels,
        Fit,
        CustomZoom
    }

    /// <summary>
    /// The View Mode to use on initial startup
    /// </summary>
    public enum InitialViewMode
    {
        ActualSize,
        Fit,
        LastUsed
    }
}
