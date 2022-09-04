using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace QuickImg
{
    public static class FileHelper
    {
        // Stores the Exception "Message" if there are any issues in the below methods.
        public static string LastErrorMessage;

        /// <summary>
        /// Returns the Image Type (Enum) when passed a string image file extension.
        /// </summary>
        /// <param name="fileType"></param>
        /// <returns></returns>
        public static ImageType GetImageType(string fileType)
        {
            // Set image file type
            ImageType type;
            // Application now has read/write access to the picked file
            if (fileType == ".svg")
            {
                type = ImageType.SVG;
            }
            else
            {
                type = ImageType.Bitmap;
            }

            return type;
        }

        /// <summary>
        /// Returns all image files in the same directory as the current image file
        /// TO DO: This can be slow if there are a lot of image files in the directory.
        /// The plan is to run this in the background as soon as a user opens a file,
        /// And store it until/if they change directory,
        /// Rather than every time they press next/back.
        /// </summary>
        /// <param name="imageFile"></param>
        /// <returns></returns>
        public static async Task<List<StorageFile>> GetImageFilesAsync(StorageFile imageFile)
        {
            if (imageFile == null)
            {
                LastErrorMessage = "Image file is null!";

                return null;
            }

            StorageFolder folder = await imageFile.GetParentAsync();
            if (folder == null)
            {
                LastErrorMessage = "Unable to get parent folder of the current image file.";

                return null;
            }
            
            IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
            if (files == null)
            {
                LastErrorMessage = "Unable to get files within parent folder.";

                return null;
            }
            else if (files.Count < 1)
            {
                LastErrorMessage = "No files within parent folder.";

                return null;
            }
            else if (files.Count == 1)
            {
                LastErrorMessage = "Current image is the only file within parent folder.";

                return null;
            }

            List<StorageFile> imageFiles = files.Where(file => Constants.FILE_TYPES.Contains(file.FileType)).ToList();
            if (imageFiles == null)
            {
                LastErrorMessage = "Unable to get image files within parent folder.";

                return null;
            }
            else if (imageFiles.Count < 1)
            {
                LastErrorMessage = "There are no image files within parent folder.";

                return null;
            }
            else if (imageFiles.Count == 1)
            {
                LastErrorMessage = "Current image is the only image file within parent folder (others are not images).";

                return null;
            }
            else //imageFiles.Count > 1
            {
                return imageFiles;
            }
        }

        /// <summary>
        /// Returns the next image file in a sequence when passed the currently opened image file, and a List of all image Files within a directory.
        /// </summary>
        /// <param name="imageFiles"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static StorageFile GetNextImage(List<StorageFile> imageFiles, StorageFile file)
        {
            int index = 0;
            int count = imageFiles.Count;
            StorageFile next;

            if (file == null)
            {
                LastErrorMessage = "Current image is null.";

                return null;
            }

            if (imageFiles == null)
            {
                LastErrorMessage = "Image files are null.";

                return null;
            }
            else if (count == 0)
            {
                LastErrorMessage = "No image files in folder.";

                return null;
            }
            else if (count == 1)
            {
                LastErrorMessage = "Only one image in folder, no next image.";

                return null;
            }
            else if (count > 1)
            {
                for (int i = 0; i < count; i++)
                {
                    if (imageFiles[i].FolderRelativeId == file.FolderRelativeId)
                    {                        
                        if (i == count - 1)
                        {
                            index = 0;
                        }
                        else
                        {
                            index = i + 1;
                        }
                        break;
                    }
                }
            }

            next = imageFiles[index];

            return next;
        }

        /// <summary>
        /// Returns the previous image file in a sequence when passed the currently opened image file, and a List of all image Files within a directory.
        /// </summary>
        /// <param name="imageFiles"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static StorageFile GetPreviousImage(List<StorageFile> imageFiles, StorageFile file)
        {
            int index = 0;
            int count = imageFiles.Count;
            StorageFile previous;

            if (file == null)
            {
                LastErrorMessage = "Current image is null.";

                return null;
            }

            if (imageFiles == null)
            {
                LastErrorMessage = "Image files are null.";

                return null;
            }
            else if (count == 0)
            {
                LastErrorMessage = "No image files in folder.";

                return null;
            }
            else if (count == 1)
            {
                LastErrorMessage = "Only one image in folder, no previous image.";

                return null;
            }
            for (int i = count - 1; i >= 0; i--)
            {
                if (imageFiles[i].Name == file.Name)
                {                    
                    if (i == 0)
                    {
                        index = count - 1;
                    }
                    else
                    {
                        index = i - 1;
                    }
                    break;
                }
            }

            previous = imageFiles[index];

            return previous;
        }
    }
}
