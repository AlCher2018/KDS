using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClientOrderQueue.Lib
{
    public static class ImageHelper
    {
        private static Dictionary<string, BitmapImage> _images = new Dictionary<string, BitmapImage>();


        internal static ImageSource GetBitmapImage(string imagePath)
        {
            if (imagePath == null) return null;

            ImageSource retVal = null;
            if (_images.Any(i => i.Key == imagePath))
            {
                retVal = _images[imagePath];
            }
            else
            {
                if (!File.Exists(imagePath)) return null;

                try
                {
                    BitmapImage bi = new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute));
                    _images.Add(imagePath, bi);

                    retVal = _images[imagePath];
                }
                catch (Exception)
                {
                }
            }

            return retVal;
        }  // method

    }  // class
}
