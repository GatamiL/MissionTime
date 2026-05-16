using MissionTime.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MissionTime.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly DbService _db;

        public bool RequestRestart { get; private set; } = false;

        public SettingsWindow(DbService db)
        {
            InitializeComponent();
            _db = db;
            txtDbPath.Text = db.DbPath;

            LoadAvailableOptions();
            SelectCurrentValues();
        }

        private void LoadAvailableOptions()
        {
            // 1. Темы (кружочки)
            var themes = new List<ThemeOption>
            {
                new ThemeOption("Синий", "#0078D7"),
                new ThemeOption("Голубой", "#03A9F4"),
                new ThemeOption("Изумруд", "#107C41"),
                new ThemeOption("Зеленый", "#4CAF50"),
                new ThemeOption("Бирюза", "#008080"),
                new ThemeOption("Фиолет", "#6A1B9A"),
                new ThemeOption("Лаванда", "#5C6BC0"),
                new ThemeOption("Графит", "#34495E"),
                new ThemeOption("Красный", "#C0392B"),
                new ThemeOption("Розовый", "#E91E63"),
                new ThemeOption("Оранжевый", "#FF9800"),
                new ThemeOption("Бронзовый", "#A1887F")
            };
            lbTheme.ItemsSource = themes;

            // 2. Шрифты (берем самые ходовые, чтобы не грузить 100500 мусорных)
            var safeFonts = new List<string> { "Segoe UI", "Arial", "Verdana", "Tahoma", "Calibri", "Times New Roman", "Georgia" };
            // Дополняем всеми системными для богатства, но эти ставим вперед
            var systemFonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList();
            cbFont.ItemsSource = safeFonts.Concat(systemFonts.Except(safeFonts)).ToList();

            // 3. Размеры шрифта
            var sizes = new List<double> { 11, 12, 13, 14, 15, 16, 18, 20 };
            cbFontSize.ItemsSource = sizes;
        }

        private void SelectCurrentValues()
        {
            // Ставим то, что сейчас в настройках
            lbTheme.SelectedValue = AppSettings.AccentHex;
            cbFont.SelectedItem = AppSettings.FontFamily;
            cbFontSize.SelectedItem = AppSettings.FontSize;

            // Если в списке нет текущего хекса, выберем первый
            if (lbTheme.SelectedIndex == -1) lbTheme.SelectedIndex = 0;
            if (cbFont.SelectedIndex == -1) cbFont.SelectedItem = "Segoe UI";
            if (cbFontSize.SelectedIndex == -1) cbFontSize.SelectedItem = 14.0;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Считываем значения
            if (lbTheme.SelectedValue != null) AppSettings.AccentHex = lbTheme.SelectedValue.ToString();
            if (cbFont.SelectedItem != null) AppSettings.FontFamily = cbFont.SelectedItem.ToString();
            if (cbFontSize.SelectedItem != null) AppSettings.FontSize = (double)cbFontSize.SelectedItem;

            // Сохраняем и МГНОВЕННО применяем везде!
            AppSettings.Save();
            AppSettings.Apply();

            this.DialogResult = true;
            this.Close();
        }

        private void btnChangeDb_Click(object sender, RoutedEventArgs e)
        {
            if (MissionMessageBox.Show(this, "Смена базы данных", "Вы действительно хотите выйти из текущей базы данных и открыть окно выбора базы?", true) == true)
            {
                RequestRestart = true;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    public class ThemeOption
    {
        public string Name { get; set; }
        public string Hex { get; set; }
        public ThemeOption(string name, string hex)
        {
            Name = name;
            Hex = hex;
        }
    }
}
