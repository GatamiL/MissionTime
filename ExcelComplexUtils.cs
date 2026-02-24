using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MissionTime
{
    public static class ExcelComplexUtils
    {
        public static void GenerateReport(
            string templatePath,
            string outPath,
            DbService db,
            long programId,
            long complexId,
            string complexName,
            DateTime periodStart,
            DateTime periodEnd)
        {
            if (string.IsNullOrWhiteSpace(templatePath)) throw new ArgumentException("Путь к шаблону не указан.");
            if (!File.Exists(templatePath)) throw new FileNotFoundException("Шаблон Excel не найден", templatePath);
            if (db == null) throw new ArgumentNullException(nameof(db));

            // 1. Получаем общие данные из БД
            var prog = db.Program_GetInfo((int)programId);
            if (!prog.HasValue) throw new InvalidOperationException("Программа не найдена: " + programId);
            string programName = prog.Value.Name;

            EnsureDirectory(Path.GetDirectoryName(outPath));

            var templateFi = new FileInfo(templatePath);
            using (var package = new ExcelPackage(templateFi))
            {
                var wb = package.Workbook;
                if (wb.Worksheets.Count < 1)
                    throw new InvalidOperationException("Шаблон должен содержать хотя бы 1 лист.");

                // --- Лист 1 (Сводка: последние 6 активных недель по всему комплексу до выбранной даты) ---
                var ws1 = wb.Worksheets[1];

                // Титул по твоим координатам
                SetMergedCellText(ws1, "C2:I2", programName, "Arial", 11f);
                SetMergedCellText(ws1, "C6:I6", complexName, "Arial", 12f);

                ws1.Cells["C8"].Value = periodStart;
                ws1.Cells["C8"].Style.Numberformat.Format = "dd.MM.yyyy";

                // ИСПРАВЛЕНО: Дата конца периода теперь в D8
                ws1.Cells["D8"].Value = periodEnd;
                ws1.Cells["D8"].Style.Numberformat.Format = "dd.MM.yyyy";

                // Узнаем реальную дату старта программы
                var progDates = db.Programs_GetById((int)programId);
                DateTime programStart = progDates.HasValue ? progDates.Value.StartDate.Date : periodStart.Date;

                // ИСПРАВЛЕНО: Конец отчета строго равен концу выбранного периода (а не концу месяца!)
                DateTime reportEnd = periodEnd.Date;

                // Собираем исторические данные помесячно (чтобы обойти ограничение SQL)
                DataTable fullAgg = null;
                DateTime iterMonth = new DateTime(programStart.Year, programStart.Month, 1);
                DateTime endMonth = new DateTime(reportEnd.Year, reportEnd.Month, 1);

                while (iterMonth <= endMonth)
                {
                    DateTime ms = iterMonth;
                    DateTime me = iterMonth.AddMonths(1).AddDays(-1);

                    // Передаем complexId — база сама вытянет всё по дереву вниз
                    var partAgg = db.TimesheetMinutes_ByDeptTree_Program_WorkDayPeriod(complexId, (int)programId, ms, me);

                    if (partAgg != null && partAgg.Rows.Count > 0)
                    {
                        if (fullAgg == null) fullAgg = partAgg.Clone();
                        foreach (DataRow row in partAgg.Rows) fullAgg.ImportRow(row);
                    }
                    iterMonth = iterMonth.AddMonths(1);
                }

                // Разбиваем на недели СТРОГО до reportEnd (до выбранной тобой даты)
                var allWeeks = GetProgramWeeks(programStart, reportEnd);
                var activeDates = new HashSet<DateTime>();
                if (fullAgg != null)
                {
                    foreach (DataRow r in fullAgg.Rows)
                    {
                        // Берем только те даты, которые не выходят за рамки reportEnd
                        DateTime wd = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                        if (Convert.ToInt32(r["MinSum"]) > 0 && wd <= reportEnd)
                        {
                            activeDates.Add(wd);
                        }
                    }
                }

                // Оставляем только те недели, в которых были часы
                var activeWeeks = allWeeks.Where(w => activeDates.Any(d => d >= w.Start && d <= w.End)).ToList();

                // Берем 6 последних "живых" недель (крайняя справа будет именно та, которую ты выбрал!)
                var last6Weeks = activeWeeks.Skip(Math.Max(0, activeWeeks.Count - 6)).ToList();

                // Заполняем таблицу
                FillSheet1_Summary(ws1, last6Weeks, fullAgg);

                // --- Лист 2 (Сводка по комплексу за неделю) ---
                var ws2 = wb.Worksheets[2];

                // Используем нашу прокачанную утилиту для шапки
                SetMergedCellText(ws2, "C4:J4", programName, "Arial", 11f);
                SetMergedCellText(ws2, "C8:J8", complexName, "Arial", 12f);

                ws2.Cells["C10"].Value = periodStart;
                ws2.Cells["D10"].Value = periodEnd;
                ws2.Cells["C10:D10"].Style.Numberformat.Format = "dd.MM.yyyy";

                // Вытягиваем данные по всему комплексу строго за выбранную неделю (7 дней)
                var complexAgg = db.TimesheetMinutes_ByDeptTree_Program_WorkDayPeriod(complexId, (int)programId, periodStart, periodEnd);

                // Заполняем таблицу
                FillSheet2_ComplexDaily(ws2, periodStart, complexAgg);

                // Сохраняем
                package.SaveAs(new FileInfo(outPath));
            }
        }

        #region Вспомогательные методы
        private static void FillSheet1_Summary(ExcelWorksheet ws, List<(int WeekNum, DateTime Start, DateTime End)> weeks, DataTable aggData)
        {
            int headerRow = 12; // Шапка теперь на 12 строке
            int startCol = 3;   // Колонка C

            // 1. Формируем шапку
            for (int i = 0; i < 6; i++)
            {
                int col = startCol + i;
                if (i < weeks.Count)
                {
                    var w = weeks[i];
                    ws.Cells[headerRow, col].Value = $"{w.WeekNum} неделя\n{w.Start:dd.MM}-{w.End:dd.MM.yy}";
                }
                else
                {
                    ws.Cells[headerRow, col].Value = null;
                }
                ws.Cells[headerRow, col].Style.WrapText = true;
                ws.Cells[headerRow, col].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                ws.Cells[headerRow, col].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            }

            // 2. Группируем данные 
            var worksOrder = new List<long>();
            var workInfo = new Dictionary<long, (string Code, string Name)>();
            var minsMap = new Dictionary<(long WorkId, int WeekNum), int>();

            if (aggData != null)
            {
                foreach (DataRow r in aggData.Rows)
                {
                    long workId = Convert.ToInt64(r["WorkId"]);
                    if (!workInfo.ContainsKey(workId))
                    {
                        workInfo[workId] = (Convert.ToString(r["SpecialCode"]) ?? "", Convert.ToString(r["Name"]) ?? "");
                        worksOrder.Add(workId);
                    }

                    DateTime wd = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                    var week = weeks.FirstOrDefault(w => wd >= w.Start && wd <= w.End);
                    if (week.WeekNum > 0)
                    {
                        int mins = Convert.ToInt32(r["MinSum"]);
                        var key = (workId, week.WeekNum);
                        minsMap[key] = minsMap.TryGetValue(key, out int cur) ? cur + mins : mins;
                    }
                }
            }

            int startRow = 13; // Данные начинаются с 13 строки
            int workCount = worksOrder.Count;
            if (workCount == 0) return;

            if (workCount > 1)
                ws.InsertRow(startRow + 1, workCount - 1, startRow);

            // 3. Выводим строки
            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = worksOrder[idx];
                var info = workInfo[workId];

                ws.Cells[row, 1].Value = info.Code; // A: Код
                ws.Cells[row, 2].Value = info.Name; // B: Наименование
                AutoFitRowHeightByText(ws, row, 2, 2, "Arial", 11f); // Чуть уменьшил шрифт, если для сводного отчета надо

                for (int i = 0; i < 6; i++)
                {
                    int col = startCol + i;
                    var cell = ws.Cells[row, col];

                    if (i < weeks.Count)
                    {
                        minsMap.TryGetValue((workId, weeks[i].WeekNum), out int m);
                        cell.Value = MinutesToExcelTime(m);
                        cell.Style.Numberformat.Format = "[h]:mm";
                    }
                    else
                    {
                        cell.Value = null;
                    }
                }

                // 4. Формула ИТОГО в колонке I
                ws.Cells[row, 9].Formula = $"SUM(C{row}:H{row})";
                ws.Cells[row, 9].Style.Numberformat.Format = "[h]:mm";
            }
        }
        private static void FillSheet2_ComplexDaily(ExcelWorksheet ws, DateTime periodStart, DataTable agg)
        {
            int headerRow = 14;
            int startCol = 3; // Колонка C

            // 1. Формируем шапку с датами (C14 - I14)
            for (int i = 0; i < 7; i++)
            {
                ws.Cells[headerRow, startCol + i].Value = periodStart.Date.AddDays(i).ToString("dd.MM.yy");
                ws.Cells[headerRow, startCol + i].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            }

            // 2. Группируем данные (Работы -> Часы по дням)
            var worksOrder = new List<long>();
            var workInfo = new Dictionary<long, (string Code, string Name)>();
            var minsMap = new Dictionary<(long WorkId, int DayIdx), int>();

            if (agg != null)
            {
                foreach (DataRow r in agg.Rows)
                {
                    long workId = Convert.ToInt64(r["WorkId"]);
                    if (!workInfo.ContainsKey(workId))
                    {
                        workInfo[workId] = (Convert.ToString(r["SpecialCode"]) ?? "", Convert.ToString(r["Name"]) ?? "");
                        worksOrder.Add(workId);
                    }

                    DateTime wd = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                    int dayIdx = (int)(wd - periodStart.Date).TotalDays;

                    // Если день попадает в наши 7 дней (от 0 до 6)
                    if (dayIdx >= 0 && dayIdx < 7)
                    {
                        int mins = Convert.ToInt32(r["MinSum"]);
                        minsMap[(workId, dayIdx)] = minsMap.TryGetValue((workId, dayIdx), out int cur) ? cur + mins : mins;
                    }
                }
            }

            int startRow = 15; // Данные с 15 строки
            int workCount = worksOrder.Count;
            if (workCount == 0) return;

            // Раздвигаем строки шаблона
            if (workCount > 1)
                ws.InsertRow(startRow + 1, workCount - 1, startRow);

            // 3. Выводим строки
            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = worksOrder[idx];
                var info = workInfo[workId];

                ws.Cells[row, 1].Value = info.Code; // A15: Код работы
                ws.Cells[row, 2].Value = info.Name; // B15: Наименование работы
                AutoFitRowHeightByText(ws, row, 2, 2, "Arial", 11f);

                // Расставляем часы с понедельника по воскресенье (C15 - I15)
                for (int d = 0; d < 7; d++)
                {
                    minsMap.TryGetValue((workId, d), out int m);
                    var cell = ws.Cells[row, startCol + d];

                    // Записываем нули как 0:00 (либо часы)
                    cell.Value = MinutesToExcelTime(m);
                    cell.Style.Numberformat.Format = "[h]:mm";
                }

                // 4. Формула ИТОГО в колонке J (индекс 10)
                ws.Cells[row, 10].Formula = $"SUM(C{row}:I{row})";
                ws.Cells[row, 10].Style.Numberformat.Format = "[h]:mm";
            }
        }

        #endregion

        #region Утилиты
        private static void SetMergedCellText(ExcelWorksheet ws, string rangeAddress, string text, string fontName = "Arial", float fontSize = 11f)
        {
            var range = ws.Cells[rangeAddress];
            text = text ?? "";
            range.Value = text;

            range.Style.Font.Name = fontName;
            range.Style.Font.Size = fontSize;
            range.Style.WrapText = true;
            range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            range.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

            double totalWidth = 0;
            for (int col = range.Start.Column; col <= range.End.Column; col++)
            {
                totalWidth += ws.Column(col).Width;
            }
            if (totalWidth <= 0) totalWidth = 10;

            // ИСПРАВЛЕНИЕ 1: Уменьшили коэффициент вместимости с 1.1 до 0.85. 
            // Теперь алгоритм думает, что строка уже, и смелее добавляет новые линии текста.
            double charsPerLine = totalWidth * 1;

            // Считаем количество строк
            double lines = Math.Ceiling(text.Length / charsPerLine);

            // ИСПРАВЛЕНИЕ 2: Добавляем 1 запасную строку, если текст длиннее одной линии
            if (lines > 1) lines += 0.5;
            if (lines < 1) lines = 1;

            // ИСПРАВЛЕНИЕ 3: Увеличили множитель высоты строки с 1.5 до 1.7
            double requiredTotalHeight = (fontSize * 1.7) * lines;

            int rowCount = range.End.Row - range.Start.Row + 1;
            double heightPerRow = requiredTotalHeight / rowCount;

            for (int r = range.Start.Row; r <= range.End.Row; r++)
            {
                ws.Row(r).CustomHeight = true;
                ws.Row(r).Height = heightPerRow < 15 ? 15 : heightPerRow;
            }
        }
        public static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "report";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            while (name.Contains("  ")) name = name.Replace("  ", " ");
            return name.Trim();
        }

        private static double MinutesToExcelTime(int minutes) => Math.Max(0, minutes) / 1440.0;

        private static void AutoFitRowHeightByText(ExcelWorksheet ws, int row, int colFrom, int colTo, string fontName = "Arial", float fontSize = 11f, float paddingPx = 6f)
        {
            float widthPx = 0;
            for (int c = colFrom; c <= colTo; c++)
                widthPx += (float)(ws.Column(c).Width * 7.0 + 5.0);

            if (widthPx < 20) widthPx = 20;

            string text = Convert.ToString(ws.Cells[row, colFrom].Value) ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                ws.Row(row).CustomHeight = true;
                ws.Row(row).Height = 16;
                return;
            }

            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font(fontName, fontSize))
            {
                var size = g.MeasureString(text, font, new SizeF(widthPx, 10000));
                double heightPt = (size.Height + paddingPx) * 72.0 / g.DpiY;
                ws.Row(row).CustomHeight = true;
                ws.Row(row).Height = heightPt < 16 ? 16 : heightPt;
            }
        }

        private static List<(int WeekNum, DateTime Start, DateTime End)> GetProgramWeeks(DateTime startDate, DateTime endDate)
        {
            var weeks = new List<(int WeekNum, DateTime Start, DateTime End)>();
            DateTime currentStart = startDate.Date;
            DateTime finalEnd = endDate.Date;
            int weekNum = 1;

            while (currentStart <= finalEnd)
            {
                int daysToSunday = ((int)DayOfWeek.Sunday - (int)currentStart.DayOfWeek + 7) % 7;
                DateTime currentEnd = currentStart.AddDays(daysToSunday);
                if (currentEnd > finalEnd) currentEnd = finalEnd;

                weeks.Add((weekNum, currentStart, currentEnd));
                weekNum++;
                currentStart = currentEnd.AddDays(1);
            }
            return weeks;
        }
        #endregion
    }
}