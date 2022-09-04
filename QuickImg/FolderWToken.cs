using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace QuickImg
{
    /// <summary>
    /// A simple class, which stores a StorageFolder along with its Token within the Future Access List.
    /// This is required to keep these items together in a logical way as StorageFolder only contains readonly properties.
    /// A Token automatically adds a file/folder as an "Allowed" location when the user manually browses to an image using the system file picker
    /// </summary>
    public class FolderWToken
    {
        public string Token { get; set; }

        private StorageFolder _folder;
        public StorageFolder Folder
        {
            get
            {
                return _folder;
            }
            set
            {
                _folder = value;
                DisplayName = _folder.DisplayName;
            }

        }

        private string _displayName;
        public string DisplayName
        {
            get
            {
                return _displayName;
            }
            set
            {
                _displayName = value;
            }
        }

        public FolderWToken()
        {

        }

        public FolderWToken(StorageFolder folder, String token)
        {
            Token = token;
            Folder = folder;
            DisplayName = Folder.DisplayName;
        }
    }
}
