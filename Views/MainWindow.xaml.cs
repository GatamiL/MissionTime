using MissionTime.Services;
using MissionTime.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MissionTime.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly DbService _db;
        private DataRowView _lastSelectedRow;
        private int _lastSelectedColumnIndex = 1;

        public MainWindow(DbService db)
        {
            InitializeComponent();
            _db = db;
            var dialogService = new WpfDialogService(db);
            _vm = new MainViewModel(db, dialogService);
            this.DataContext = _vm;

            InitColumnsOnce();
            UpdateColumnsVisibility(_vm.SelectedYear, _vm.SelectedMonth + 1);
            _vm.OnMonthChanged += (y, m) => UpdateColumnsVisibility(y, m);
            _vm.OnDataNeedsRefresh += RefreshDataGridPreservingFocus;

            dgMainReport.PreparingCellForEdit += dgMainReport_PreparingCellForEdit;
            dgMainReport.SelectedCellsChanged += dgMainReport_SelectedCellsChanged;
        }
        private void dgMainReport_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (dgMainReport.CurrentCell.Item is DataRowView row)
            {
                _lastSelectedRow = row;
                if (dgMainReport.CurrentCell.Column != null)
                {
                    _lastSelectedColumnIndex = dgMainReport.Columns.IndexOf(dgMainReport.CurrentCell.Column);
                }
            }
        }

        private void dgMainReport_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            DataGrid dg = sender as DataGrid;
            if (dg == null || dg.CurrentCell == null) return;

            // Если мы уже редактируем (курсор мигает), выходим — сработает логика TextBox.
            // Это МЕГА-БЫСТРЫЙ способ узнать, редактируем ли мы, без поиска по Visual Tree!
            if (e.OriginalSource is TextBox) return;

            // 1. Мой новый обработчик УДАЛЕНИЯ МНОГИХ ЯЧЕЕК (DEL)
            if (e.Key == Key.Delete)
            {
                var cells = dgMainReport.SelectedCells;
                if (cells.Count > 0)
                {
                    foreach (var cell in cells)
                    {
                        if (cell.Column == null || cell.Item == null) continue;
                        if (cell.Column.Header is DayHeaderData)
                        {
                            if (cell.Item is DataRowView rowView && rowView["RowType"]?.ToString() == "Work")
                            {
                                var binding = (cell.Column as DataGridTextColumn)?.Binding as Binding;
                                string path = binding?.Path?.Path;
                                if (!string.IsNullOrEmpty(path) && path.StartsWith("Day"))
                                {
                                    rowView[path] = ""; // Пишем в БД
                                }
                            }
                        }
                    }
                    e.Handled = true;
                    return; // Важно! Выходим, чтобы дальше по коду не пошло
                }
            }
            // 2. ВЫРЕЗАНИЕ (CTRL+X)
            else if (e.Key == Key.X && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // 1. Копируем данные стандартным способом в буфер
                ApplicationCommands.Copy.Execute(null, dgMainReport);

                // 2. Удаляем данные из выделенных ячеек (как в DEL)
                var cells = dgMainReport.SelectedCells;
                if (cells.Count > 0)
                {
                    foreach (var cell in cells)
                    {
                        if (cell.Column == null || cell.Item == null) continue;
                        if (cell.Column.Header is DayHeaderData)
                        {
                            if (cell.Item is DataRowView rowView && rowView["RowType"]?.ToString() == "Work")
                            {
                                var binding = (cell.Column as DataGridTextColumn)?.Binding as Binding;
                                string path = binding?.Path?.Path;
                                if (!string.IsNullOrEmpty(path) && path.StartsWith("Day"))
                                {
                                    rowView[path] = ""; // Удаляет в БД
                                }
                            }
                        }
                    }
                }
                e.Handled = true;
                return;
            }
            // 3. ВСТАВКА ИЗ EXCEL (CTRL+V)
            else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                PasteClipboardToGrid();
                e.Handled = true;
                return;
            }

            // Разрешаем системные клавиши (кроме Delete, который мы обработали выше)
            if (e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Back || 
                e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down) return;

            // Получаем цифру
            string strKey = "";
            if (e.Key >= Key.D0 && e.Key <= Key.D9) strKey = (e.Key - Key.D0).ToString();
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) strKey = (e.Key - Key.NumPad0).ToString();
            else { e.Handled = true; return; } // Блокируем буквы

            // МАГИЯ: Мы сами начинаем редактирование
            e.Handled = true; // Отменяем стандартное открытие (чтобы буква/цифра не двоилась)
            dg.BeginEdit();   // Принудительно открываем TextBox

            // Находим созданный TextBox
            Dispatcher.BeginInvoke(new Action(() => {
                if (dg.CurrentCell != null)
                {
                    var editingElement = dg.CurrentCell.Column.GetCellContent(dg.CurrentCell.Item) as TextBox;
                    if (editingElement != null)
                    {
                        // Применяем твою логику для ПЕРВОГО символа (pos == 0)
                        int digit = int.Parse(strKey);
                        if (digit > 2)
                        {
                            editingElement.Text = "0" + strKey + ":";
                            editingElement.SelectionStart = 3;
                        }
                        else
                        {
                            editingElement.Text = strKey;
                            editingElement.SelectionStart = 1;
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // 2. Ловим TextBox в момент появления (убедись, что этот метод есть и подписан)
        private void dgMainReport_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is TextBox tb)
            {
                tb.PreviewKeyDown -= TextBox_PreviewKeyDown;
                tb.PreviewKeyDown += TextBox_PreviewKeyDown;
                tb.ContextMenu = null;
            }
        }

        // 3. УМНАЯ ЛОГИКА ВВОДА (Полная версия)
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb == null) return;

            // --- 1. ЛОГИКА TAB / ENTER (Автодополнение) ---
            if (e.Key == Key.Tab || e.Key == Key.Enter)
            {
                string val = tb.Text;
                if (string.IsNullOrEmpty(val)) return;

                // Если введено просто число "1", "2", "20"
                if (!val.Contains(":"))
                {
                    if (int.TryParse(val, out int h) && h <= 24)
                        tb.Text = h.ToString("D2") + ":00";
                }
                // Если введено "12:" -> "12:00"
                else if (val.EndsWith(":"))
                {
                    tb.Text += "00";
                }
                // Если введено "12:3" -> "12:30"
                else if (val.Length == 4)
                {
                    tb.Text += "0";
                }

                // Принудительно сохраняем в базу перед уходом фокуса
                var binding = tb.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
                dgMainReport.CommitEdit(DataGridEditingUnit.Row, true);
                return;
            }

            if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right)
                return;

            // --- 2. ФИЛЬТР ЦИФР ---
            string strKey = "";
            if (e.Key >= Key.D0 && e.Key <= Key.D9) strKey = (e.Key - Key.D0).ToString();
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) strKey = (e.Key - Key.NumPad0).ToString();
            else { e.Handled = true; return; }

            e.Handled = true;
            string text = tb.Text;
            int pos = tb.SelectionStart;

            if (tb.SelectionLength == tb.Text.Length) { text = ""; pos = 0; }

            // --- 3. ТВОЯ ПОШАГОВАЯ ЛОГИКА ---
            int digit = int.Parse(strKey);

            if (pos == 0)
            {
                // Сперва 1 или 2 — ждем дальше
                if (digit <= 2) { tb.Text = strKey; tb.SelectionStart = 1; }
                // Сперва 3-9 — сразу "03:"
                else { tb.Text = "0" + strKey + ":"; tb.SelectionStart = 3; }
            }
            else if (pos == 1)
            {
                int first = int.Parse(text[0].ToString());

                // Сперва 1, потом любое число -> "1x:"
                if (first == 1) { tb.Text = text + strKey + ":"; tb.SelectionStart = 3; }
                // Сперва 2, потом число <= 4 -> "2x:"
                else if (first == 2 && digit <= 4) { tb.Text = text + strKey + ":"; tb.SelectionStart = 3; }
                // Сперва 2, потом число > 4 -> "20:x" (т.к. 25 часов нельзя)
                else if (first == 2 && digit > 4) { tb.Text = "20:" + strKey; tb.SelectionStart = 4; }
            }
            else if (pos == 3) // После двоеточия (например "12:")
            {
                // Вводим число -> "12:x"
                tb.Text = text + strKey;
                tb.SelectionStart = 4;
            }
            else if (pos == 4) // Вторая цифра минут
            {
                // "12:3" + "5" -> "12:35"
                tb.Text = text + strKey;
                tb.SelectionStart = 5;
            }
        }

        private void dgMainReport_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Нас интересует только момент фиксации данных (Commit)
            if (e.EditAction == DataGridEditAction.Commit)
            {
                if (e.EditingElement is TextBox tb)
                {
                    string val = tb.Text.Trim();
                    if (string.IsNullOrEmpty(val)) return;

                    // 1. Если в конце двоеточие (например "03:") -> делаем "03:00"
                    if (val.EndsWith(":"))
                    {
                        val += "00";
                    }
                    // 2. Если введено не до конца (например "03:5") -> делаем "03:50"
                    else if (val.Contains(":") && val.Length == 4)
                    {
                        val += "0";
                    }
                    // 3. Если введено просто число без двоеточия (например "8") -> "08:00"
                    else if (!val.Contains(":"))
                    {
                        if (int.TryParse(val, out int h) && h <= 24)
                            val = h.ToString("D2") + ":00";
                    }

                    // 4. Проверка на корректность (чтобы не проскочило "03:70")
                    if (val.Length == 5)
                    {
                        var parts = val.Split(':');
                        if (int.TryParse(parts[1], out int m) && m > 59)
                        {
                            // Если минут больше 59, сбрасываем в 00 или корректируем
                            val = parts[0] + ":00";
                        }
                    }
                    else
                    {
                        // Если формат совсем битый — очищаем
                        val = "";
                    }

                    if (val.EndsWith(":")) val += "00";
                    else if (val.Contains(":") && val.Length == 4) val += "0";

                    // 1. Сначала меняем текст в самом поле
                    tb.Text = val;

                    // 2. А ТЕПЕРЬ САМОЕ ГЛАВНОЕ:
                    // Принудительно заталкиваем это значение в DataTable
                    var binding = tb.GetBindingExpression(TextBox.TextProperty);
                    binding?.UpdateSource();
                }
            }
        }

        private void dgMainReport_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // Если колонка первая (ФИО), сразу отменяем редактирование
            if (e.Column.DisplayIndex == 0)
            {
                e.Cancel = true;
                return;
            }
            var rowView = e.Row.Item as DataRowView;
            if (rowView == null) return;

            string rowType = rowView["RowType"]?.ToString();

            // 1. Если это сотрудник — вообще ничего не даем нажимать
            if (rowType == "Employee")
            {
                e.Cancel = true;
                return;
            }

            // 2. Если это работа, но колонка ФИО (индекс 0) — тоже запрещаем
            if (rowType == "Work" && e.Column.DisplayIndex == 0)
            {
                e.Cancel = true;
            }
        }

        private void InitColumnsOnce()
        {
            dgMainReport.Columns.Clear();

            var headerStyle = Application.Current.FindResource("DayHeaderStyle") as Style;
            var cellTextStyle = Application.Current.FindResource("CenterTextStyle") as Style;
            var dayCellStyle = Application.Current.FindResource("DayCellStyle") as Style;
            
            dgMainReport.Columns.Add(new DataGridTextColumn
            {
                Header = "TS_ID",
                Binding = new Binding("TimesheetId"),
                Visibility = Visibility.Collapsed // СКРЫВАЕМ
            });
            // 1. ФИО с фиксацией и минимальной шириной (Используем TemplateColumn для жирного шрифта кода)
            var fullNameCol = new DataGridTemplateColumn
            {
                Header = "ФИО",
                Width = 220,
                MinWidth = 150,
                CellStyle = dayCellStyle,
                IsReadOnly = true
            };

            var template = new DataTemplate();
            var spFactory = new FrameworkElementFactory(typeof(StackPanel));
            spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // Наша круглая кнопка свернуть/развернуть сотрудника
            var toggleBtnFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton));
            toggleBtnFactory.SetValue(System.Windows.Controls.Primitives.ToggleButton.StyleProperty, (Style)this.FindResource("RowExpandToggleStyle"));
            toggleBtnFactory.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            spFactory.AppendChild(toggleBtnFactory);

            // Code (bold)
            var codeFactory = new FrameworkElementFactory(typeof(TextBlock));
            codeFactory.SetBinding(TextBlock.TextProperty, new Binding("WorkCode"));
            codeFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            codeFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            spFactory.AppendChild(codeFactory);

            // Name
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new Binding("FullName"));
            spFactory.AppendChild(nameFactory);

            template.VisualTree = spFactory;
            fullNameCol.CellTemplate = template;

            dgMainReport.Columns.Add(fullNameCol);

            // 2. Колонки дней
            for (int i = 1; i <= 31; i++)
            {
                var headerData = new DayHeaderData { DayNumber = i.ToString(), DayName = "" };

                var col = new DataGridTextColumn
                {
                    Header = headerData,
                    HeaderStyle = headerStyle,
                    ElementStyle = cellTextStyle,
                    CellStyle = dayCellStyle,
                    Binding = new Binding($"Day{i}") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                    Width = 70 // БЫЛО Star. Фиксированная ширина включает настоящую виртуализацию и убирает тормоза!
                };

                dgMainReport.Columns.Add(col);
            }

            // 3. Итого
            dgMainReport.Columns.Add(new DataGridTextColumn
            {
                Header = "Итого",
                Binding = new Binding("Total"),
                Width = 80,
                MinWidth = 95,
                IsReadOnly = true,
                FontWeight = FontWeights.Bold,
                ElementStyle = cellTextStyle,
                CellStyle = dayCellStyle
            });
        }

        private void RefreshDataGridPreservingFocus()
        {
            var currentCell = dgMainReport.CurrentCell;
            int columnIndex = -1;
            long? ephId = null; 
            string rowType = "";
            long? workId = null;

            DataRowView rowToPreserve = null;
            if (currentCell.IsValid && currentCell.Column != null && currentCell.Item is DataRowView r)
            {
                columnIndex = dgMainReport.Columns.IndexOf(currentCell.Column);
                rowToPreserve = r;
            }
            else if (_lastSelectedRow != null)
            {
                rowToPreserve = _lastSelectedRow;
                columnIndex = _lastSelectedColumnIndex;
            }

            _lastSelectedRow = null; // Обнуляем, восстановим внутри Dispatcher при успехе

            if (rowToPreserve != null)
            {
                // Достаем EPH_Id из скрытой колонки DataTable
                if (rowToPreserve.Row.Table.Columns.Contains("EPH_Id") && rowToPreserve["EPH_Id"] != DBNull.Value)
                {
                    ephId = Convert.ToInt64(rowToPreserve["EPH_Id"]);
                    rowType = rowToPreserve["RowType"]?.ToString();

                    if (rowType == "Work" && rowToPreserve.Row.Table.Columns.Contains("WorkId"))
                        workId = rowToPreserve["WorkId"] != DBNull.Value ? (long?)Convert.ToInt64(rowToPreserve["WorkId"]) : null;
                }
            }

            // ОБНОВЛЯЕМ (не ломая биндинг)
            // При добавлении строки DataView сам обновляет DataGrid. 
            // Нам нужно только дождаться перерисовки и вернуть фокус.
            
            if (ephId == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var item in dgMainReport.Items)
                {
                    if (item is DataRowView row && row["EPH_Id"] != DBNull.Value && Convert.ToInt64(row["EPH_Id"]) == ephId)
                    {
                        // Если это строка работы, проверяем еще и WorkId
                        if (rowType == "Work")
                        {
                            if (row["WorkId"] == DBNull.Value || Convert.ToInt64(row["WorkId"]) != workId) continue;
                        }
                        else if (row["RowType"].ToString() != rowType) continue;

                        int colIdx = (columnIndex >= 0 && columnIndex < dgMainReport.Columns.Count) ? columnIndex : 0;
                        var newCellInfo = new DataGridCellInfo(item, dgMainReport.Columns[colIdx]);
                        
                        dgMainReport.CurrentCell = newCellInfo;
                        dgMainReport.SelectedCells.Clear();
                        dgMainReport.SelectedCells.Add(newCellInfo);

                        _lastSelectedRow = row; // Восстанавливаем ссылку на последнюю выделенную строку!
                        
                        dgMainReport.ScrollIntoView(item);
                        dgMainReport.Focus(); // Фокусируем сетку, чтобы ячейка визуально выделилась

                        var cellContent = dgMainReport.CurrentCell.Column.GetCellContent(item);
                        var cell = cellContent?.Parent as DataGridCell;
                        cell?.Focus();
                        break;
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void UpdateColumnsVisibility(int year, int month)
        {
            // Защита: ФИО(0) + 31 день + Итого(32) = минимум 33 колонки должно быть
            if (dgMainReport.Columns.Count < 33) return;

            int daysInMonth = System.DateTime.DaysInMonth(year, month);
            string[] dayNames = { "Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };

            // Берем готовые стили из ресурсов
            var cellTextStyle = Application.Current.FindResource("CenterTextStyle") as Style;
            var weekendTextStyle = Application.Current.FindResource("WeekendTextStyle") as Style;
            var weekendColor = (SolidColorBrush)Application.Current.FindResource("WeekendForeground");

            for (int i = 1; i <= 31; i++)
            {
                var column = dgMainReport.Columns[i + 1] as DataGridTextColumn;
                if (column == null) continue;

                if (i <= daysInMonth)
                {
                    column.Visibility = Visibility.Visible;
                    if (column.Header is DayHeaderData data)
                    {
                        var dt = new DateTime(year, month, i);
                        bool isWeekend = dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday;

                        data.DayName = dayNames[(int)dt.DayOfWeek];
                        data.DayColor = isWeekend ? weekendColor : Brushes.Black;

                        // Просто подставляем готовый стиль, ничего не создавая в цикле
                        column.ElementStyle = isWeekend ? weekendTextStyle : cellTextStyle;
                    }
                }
                else
                {
                    column.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void PasteClipboardToGrid()
        {
            string clipboardData = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(clipboardData)) return;

            var currentCell = dgMainReport.CurrentCell;
            if (!currentCell.IsValid || currentCell.Column == null || currentCell.Item == null) return;

            int startRowIdx = dgMainReport.Items.IndexOf(currentCell.Item);
            int startColIdx = currentCell.Column.DisplayIndex;

            string[] rows = clipboardData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            if (rows.Length > 0 && string.IsNullOrEmpty(rows[rows.Length - 1]))
            {
                var tmp = rows.ToList();
                tmp.RemoveAt(tmp.Count - 1);
                rows = tmp.ToArray();
            }

            // Собираем список изменений, чтобы передать во ViewModel одним махом
            var changes = new List<Tuple<DataRow, string, string>>();

            for (int r = 0; r < rows.Length; r++)
            {
                int targetRowIdx = startRowIdx + r;
                if (targetRowIdx >= dgMainReport.Items.Count) break;

                var targetRowItem = dgMainReport.Items[targetRowIdx] as DataRowView;
                if (targetRowItem == null || targetRowItem["RowType"]?.ToString() != "Work") continue;

                string[] cols = rows[r].Split('\t');
                for (int c = 0; c < cols.Length; c++)
                {
                    int targetColDisplayIdx = startColIdx + c;
                    var column = dgMainReport.Columns.Cast<DataGridColumn>().FirstOrDefault(col => col.DisplayIndex == targetColDisplayIdx);
                    
                    if (column == null || !(column.Header is DayHeaderData)) continue;

                    var binding = (column as DataGridTextColumn)?.Binding as Binding;
                    string path = binding?.Path?.Path;

                    if (!string.IsNullOrEmpty(path) && path.StartsWith("Day"))
                    {
                        string rawVal = cols[c]?.Trim() ?? "";
                        string formatted = FormatInputValue(rawVal);
                        changes.Add(new Tuple<DataRow, string, string>(targetRowItem.Row, path, formatted));
                    }
                }
            }

            // Отправляем в ViewModel для пакетной асинхронной обработки
            if (changes.Count > 0)
            {
                this.Cursor = Cursors.Wait;
                try
                {
                    await _vm.PasteDataAsync(changes);
                }
                finally
                {
                    this.Cursor = null;
                }
            }
        }

        private string FormatInputValue(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return "";
            
            // Если уже время, просто вернем
            if (val.Contains(":")) return val;

            string normalized = val.Replace(",", ".").Trim();
            if (double.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hours))
            {
                int totalMinutes = (int)Math.Round(hours * 60);
                if (totalMinutes <= 0) return "";
                return TimeUtils.MinutesToHHmm(totalMinutes);
            }
            
            return val;
        }
        private void btnAddWork_Click(object sender, RoutedEventArgs e)
        {
            _vm.AddWork(_lastSelectedRow);
        }

        private void btnEditWork_Click(object sender, RoutedEventArgs e)
        {
            _vm.EditWork(_lastSelectedRow);
        }

        private void btnDeleteWork_Click(object sender, RoutedEventArgs e)
        {
            _vm.DeleteWork(_lastSelectedRow);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MissionTime Export (*.mte)|*.mte",
                Title = "Выберите файл для импорта"
            };

            if (ofd.ShowDialog() == true)
            {
                await _vm.ImportDataAsync(ofd.FileName);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            // Открываем наше новое окно экспорта
            var exportWin = new ExportWindow(_db, _vm.SelectedYear, _vm.SelectedMonth) 
            { 
                Owner = this 
            };
            exportWin.ShowDialog();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWin = new SettingsWindow(_db) { Owner = this };
            if (settingsWin.ShowDialog() == true && settingsWin.RequestRestart)
            {
                // 1. Создаем окно выбора базы
                var selectWin = new DbSelect();
                // 2. Показываем его
                selectWin.Show();
                // 3. Назначаем его временным главным окном, чтобы закрытие текущего не убило приложение
                Application.Current.MainWindow = selectWin;
                // 4. Закрываем главное окно
                this.Close();
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        
        private void ShowEmployees_Click(object sender, RoutedEventArgs e)
        {
            _vm.ShowEmployees();
        }
        
        private void ShowDepartments_Click(object sender, RoutedEventArgs e)
        {
            _vm.ShowDepartments();
        }
        
        private void ShowPositions_Click(object sender, RoutedEventArgs e) => _vm.ShowPositions();
        
        private void ShowWorkTypes_Click(object sender, RoutedEventArgs e)
        {
            _vm.ShowWorkTypes();
        }
        
        private void ShowPrograms_Click(object sender, RoutedEventArgs e)
        {
            _vm.ShowPrograms();
        }
        
        private void ShowSignatures_Click(object sender, RoutedEventArgs e) => _vm.ShowSignatures();

        private void ShowCard_Click(object sender, RoutedEventArgs e) => _vm.ShowCardReport();
        private void ShowReportForDepartment_Click(object sender, RoutedEventArgs e) => _vm.ShowReportForDepartment();
        private void ShowReportForDivision_Click(object sender, RoutedEventArgs e) => _vm.ShowReportForDivision();
        private void ShowReportDirectory_Click(object sender, RoutedEventArgs e) => _vm.ShowReportDirectory();
        private void ShowAbout_Click(object sender, RoutedEventArgs e) => _vm.ShowAbout();
        
        private void ReportDept_Click(object sender, RoutedEventArgs e) => _vm.ShowReportDept();
    }
    public class DayHeaderData : ViewModelBase // Чтобы работало обновление
    {
        private string _dayNumber;
        public string DayNumber { get => _dayNumber; set => SetProperty(ref _dayNumber, value); }

        private string _dayName;
        public string DayName { get => _dayName; set => SetProperty(ref _dayName, value); }

        private System.Windows.Media.Brush _dayColor;
        public System.Windows.Media.Brush DayColor { get => _dayColor; set => SetProperty(ref _dayColor, value); }
    }
}