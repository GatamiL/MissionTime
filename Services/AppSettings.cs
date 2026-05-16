using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MissionTime.Services
{
    public static class AppSettings
    {
        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_settings.txt");

        public static string AccentHex { get; set; } = "#0078D7";
        public static string FontFamily { get; set; } = "Segoe UI";
        public static double FontSize { get; set; } = 14.0;

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var lines = File.ReadAllLines(SettingsFile);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        if (parts.Length < 2) continue;
                        string key = parts[0].Trim();
                        string val = parts[1].Trim();

                        if (key == "Accent") AccentHex = val;
                        else if (key == "Font") FontFamily = val;
                        else if (key == "FontSize" && double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double size)) FontSize = size;
                    }
                }
            }
            catch { /* Используем дефолт в случае сбоя */ }
            Apply();
        }

        public static void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    $"Accent={AccentHex}",
                    $"Font={FontFamily}",
                    $"FontSize={FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                };
                File.WriteAllLines(SettingsFile, lines);
            }
            catch { }
        }

        public static void Apply()
        {
            try
            {
                var appRes = Application.Current.Resources;

                // 1. Применяем Цвет Акцента
                var color = (Color)ColorConverter.ConvertFromString(AccentHex);
                appRes["AccentBrush"] = new SolidColorBrush(color);
                
                // Создаем более темный оттенок для Hover (умножаем RGB на 0.8)
                var hoverColor = Color.FromRgb(
                    (byte)(color.R * 0.8),
                    (byte)(color.G * 0.8),
                    (byte)(color.B * 0.8)
                );
                appRes["AccentHoverBrush"] = new SolidColorBrush(hoverColor);

                // 2. Применяем Шрифт
                appRes["MainFontFamily"] = new FontFamily(FontFamily);

                // 3. Применяем Размер
                appRes["MainFontSize"] = FontSize;
            }
            catch { }
        }
    }
}
