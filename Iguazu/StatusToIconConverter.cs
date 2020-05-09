using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Iguazu
{
    class StatusToIconConverter : IValueConverter
    {
        const string ICON_CHECKMARK = "checkmark.png";
        const string ICON_CROSS = "cross.png";
        const string ICON_ROUND = "round.png";

        private string _basePath = Path.Combine("", "Icons");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((MainViewModel.Status) value == MainViewModel.Status.Pending)
            {
                return Path.Combine(_basePath, ICON_ROUND);
            }
            if ((MainViewModel.Status) value == MainViewModel.Status.Done)
            {
                return Path.Combine(_basePath, ICON_CHECKMARK);
            }
            if ((MainViewModel.Status) value == MainViewModel.Status.Error)
            {
                return Path.Combine(_basePath, ICON_CROSS);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((string) value == Path.Combine(_basePath, ICON_ROUND))
            {
                return MainViewModel.Status.Pending;
            }
            if ((string) value == Path.Combine(_basePath, ICON_CHECKMARK))
            {
                return MainViewModel.Status.Done;
            }
            if ((string) value == Path.Combine(_basePath, ICON_CROSS))
            {
                return MainViewModel.Status.Error;
            }
            return MainViewModel.Status.NotStarted;
        }
    }
}
