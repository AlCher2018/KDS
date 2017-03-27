using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ClientOrderQueue.Lib
{
    public static class ImageHelper
    {
        private static Dictionary<string, BitmapImage> _images = new Dictionary<string, BitmapImage>();


        internal static System.Windows.Media.ImageSource GetBitmapImage(string imagePath)
        {
            if (imagePath == null) return null;

            if (_images.Any(i => i.Key == imagePath))
            {
                return _images[imagePath];
            }
            else
            {
                if (!File.Exists(imagePath)) return null;

                BitmapImage bi = new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute));
                _images.Add(imagePath, bi);

                return _images[imagePath];
            }

        }

    }
}
