using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace QuickImg
{
    public class ImageDetails
    {
        /// <summary>
        /// This class contains the image/file details that I currently consider the best selection that a user would be interested in, and are available from the Framework.
        /// At some later point, I might make this list exhaustive, but currently these are the values most users (according to me with no proof at all) will find useful.
        /// The reason for choosing these properties carefully is, not all properities are readable in one nice async method.
        /// So, these are grouped by what the M$ framework supports per async method, useful for optimising for speed
        /// TO DO: /// Implement a Image Properties pane?
        /// </summary>
        public string ID { get; set; }

        public string DisplayName { get; set; }
        public string DisplayType { get; set; }
        
        // File Details
        public string Name { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }
        //DateTime accessed { get; set; } // Who cares?
        public double Size { get; set; }

        // Image Details
        public int Width { get; set; }
        public int Height { get; set; }
        public double DPI { get; set; } // TO DO
        public double BitDepth { get; set; } // TO DO

        // Camera Details
        public DateTime DateTaken { get; set; }
        public string Maker { get; set; }
        public string Model { get; set; }        

        // Application Details
        public string App { get; set; } // TO DO

        public ImageDetails()
        {
            // TO DO: Initialise variables with empty values?
        }

        /// <summary>
        /// Returns the image dimensions as a string in the most common format, width x height
        /// </summary>
        /// <returns></returns>
        public string GetDimensions()
        {
            return Width.ToString() + " x " + Height.ToString();
        }

        /// <summary>
        /// This method sets image file details that are already available from picking a file, without calling additional async methods which would slow down the app.
        /// </summary>
        public void SetImageDetails(StorageFile file)
        {
            if (file == null) { return; } // Just in case...

            // Set image file details
            ID = file.FolderRelativeId;

            DisplayName = file.DisplayName;
            //DisplayType = file.DisplayType; // This description is not that great... esp for SVG. Will use the file extension for now.
            DisplayType = file.FileType.Substring(1).ToUpper();

            Name = file.Name;
            Type = file.FileType;
            Path = file.Path;

            DateCreated = file.DateCreated.DateTime;
        }
    }
}
