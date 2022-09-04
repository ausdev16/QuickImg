using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace QuickImg
{
    public class FloatToPercentageWSymbolStringConverter : IValueConverter
    {
        /// <summary>
        /// Converts a double to a percentage string, with symbol.
        /// Currently used to convert a ZoomFactor double to a readable percentage for the user.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string percentage;

            percentage = Math.Round((float)value * 100).ToString() + "%";

            return percentage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class FloatToPercentageDoubleConverter : IValueConverter
    {
        /// <summary>
        /// Converts a double to a percentage double, with no symbol.
        /// Currently used to convert a ZoomFactor double to a percentage for the Zoom Slider.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="language"></param>
        /// <returns></returns>

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double zoomFactor;
            if (value == null)
            {
                zoomFactor = 50;
            }
            else
            {
                zoomFactor = System.Convert.ToDouble(value);
            }

            double percentage;
                        
            if (zoomFactor == 1)
            {
                percentage = 50;
            }
            else if (zoomFactor >= 0.2 && zoomFactor < 1)
            {
                // ((x - min) * 100 / (max - min)) (100 [%] is an example... in my case I am using a different scale for each range above and below 50.)
                //percentage = ((zoomFactor - 0.2) * 100 / 0.8) * 0.5; =>
                //percentage = ((zoomFactor - 0.2) / (1 - 0.2)) * 50; =>
                percentage = (zoomFactor - 0.2) * 62.5;

            }
            else if (zoomFactor > 1 && zoomFactor <= 5)
            {
                //percentage = 50 + (zoomFactor - 1) / (5 - 1) * 50; // As we want 5 times zoom max. =>
                percentage = 50 + (zoomFactor - 1) * 12.5;
            }
            else // Out of bounds!
            {
                percentage = 50;
            }

            return percentage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
