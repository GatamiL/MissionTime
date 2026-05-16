using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MissionTime.Services
{
    public class LevelToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Получаем уровень (0, 1, 2...)
            int level = (value is int l) ? l : 0;

            // Если это наш фиктивный корень "-1", отступ не делаем
            if (level < 0) return new Thickness(0);

            // Умножаем уровень на 15-20 пикселей для визуальной лесенки
            return new Thickness(level * 20, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}