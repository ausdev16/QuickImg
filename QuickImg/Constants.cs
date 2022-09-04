using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickImg
{
    public static class Constants
    {
        /// <summary>
        ///  Supported image file types (extensions).
        ///  This is used by the File Picker on the Main Page.
        /// </summary>
        public static readonly string[] FILE_TYPES = { ".jpg", ".jpeg", ".gif", ".tif", ".tiff", ".png", ".heic", ".heif", ".svg" };
    }
}
