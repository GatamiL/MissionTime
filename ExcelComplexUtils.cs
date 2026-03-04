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

                SetMergedCellText(ws1, "C2:I2", programName, "Arial", 11f);
                SetMergedCellText(ws1, "C6:I6", complexName, "Arial", 12f);

                ws1.Cells["C8"].Value = periodStart;
                ws1.Cells["C8"].Style.Numberformat.Format = "dd.MM.yyyy";

                ws1.Cells["D8"].Value = periodEnd;
                ws1.Cells["D8"].Style.Numberformat.Format = "dd.MM.yyyy";

                var progDates = db.Programs_GetById((int)programId);
                DateTime programStart = progDates.HasValue ? progDates.Value.StartDate.Date : periodStart.Date;
                DateTime reportEnd = periodEnd.Date;

                // Собираем исторические данные помесячно
                DataTable fullAgg = null;
                DateTime iterMonth = new DateTime(programStart.Year, programStart.Month, 1);
                DateTime endMonth = new DateTime(reportEnd.Year, reportEnd.Month, 1);

                while (iterMonth <= endMonth)
                {
                    DateTime ms = iterMonth;
                    DateTime me = iterMonth.AddMonths(1).AddDays(-1);

                    var partAgg = db.TimesheetMinutes_ByDeptTree_Program_WorkDayPeriod(complexId, (int)programId, ms, me);

                    if (partAgg != null && partAgg.Rows.Count > 0)
                    {
                        if (fullAgg == null) fullAgg = partAgg.Clone();
                        foreach (DataRow row in partAgg.Rows) fullAgg.ImportRow(row);
                    }
                    iterMonth = iterMonth.AddMonths(1);
                }

                // --- ИСПОЛЬЗУЕМ УМНЫЕ ПЕРИОДЫ (До выбранной даты) ---
                var activeDates = db.Timesheet_GetActiveDatesForProgram((int)programId);
                var activeWeeks = GetActiveWeeksUpTo(programStart, reportEnd, activeDates);

                // Берем 6 последних "живых" недель (крайняя справа будет именно та, которую ты выбрал!)
                var last6Weeks = activeWeeks.Skip(Math.Max(0, activeWeeks.Count - 6)).ToList();

                // Заполняем таблицу
                FillSheet1_Summary(ws1, last6Weeks, fullAgg);

                // --- Лист 2 (Сводка по комплексу за неделю) ---
                var ws2 = wb.Worksheets[2];

                SetMergedCellText(ws2, "C4:J4", programName, "Arial", 11f);
                SetMergedCellText(ws2, "C8:J8", complexName, "Arial", 12f);

                ws2.Cells["C10"].Value = periodStart;
                ws2.Cells["D10"].Value = periodEnd;
                ws2.Cells["C10:D10"].Style.Numberformat.Format = "dd.MM.yyyy";

                var complexAgg = db.TimesheetMinutes_ByDeptTree_Program_WorkDayPeriod(complexId, (int)programId, periodStart, periodEnd);
                FillSheet2_ComplexDaily(ws2, periodStart, complexAgg);

                package.SaveAs(new FileInfo(outPath));
            }
        }

        #region Вспомогательные методы
        // Обновили сигнатуру на List<WorkPeriod>
        private static void FillSheet1_Summary(ExcelWorksheet ws, List<WorkPeriod> weeks, DataTable aggData)
        {
            int headerRow = 12;
            int startCol = 3;

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

                    // Ищем нашу умную неделю
                    var week = weeks.FirstOrDefault(w => wd >= w.Start && wd <= w.End);
                    if (week != null)
                    {
                        int mins = Convert.ToInt32(r["MinSum"]);
                        var key = (workId, week.WeekNum);
                        minsMap[key] = minsMap.TryGetValue(key, out int cur) ? cur + mins : mins;
                    }
                }
            }

            int startRow = 13;
            int workCount = worksOrder.Count;
            if (workCount == 0) return;

            if (workCount > 1)
                ws.InsertRow(startRow + 1, workCount - 1, startRow);

            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = worksOrder[idx];
                var info = workInfo[workId];

                ws.Cells[row, 1].Value = info.Code;
                ws.Cells[row, 2].Value = info.Name;
                AutoFitRowHeightByText(ws, row, 2, 2, "Arial", 11f);

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

                ws.Cells[row, 9].Formula = $"SUM(C{row}:H{row})";
                ws.Cells[row, 9].Style.Numberformat.Format = "[h]:mm";
            }
        }

        private static void FillSheet2_ComplexDaily(ExcelWorksheet ws, DateTime periodStart, DataTable agg)
        {
            int headerRow = 14;
            int startCol = 3;

            for (int i = 0; i < 7; i++)
            {
                ws.Cells[headerRow, startCol + i].Value = periodStart.Date.AddDays(i).ToString("dd.MM.yy");
                ws.Cells[headerRow, startCol + i].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            }

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

                    if (dayIdx >= 0 && dayIdx < 7)
                    {
                        int mins = Convert.ToInt32(r["MinSum"]);
                        minsMap[(workId, dayIdx)] = minsMap.TryGetValue((workId, dayIdx), out int cur) ? cur + mins : mins;
                    }
                }
            }

            int startRow = 15;
            int workCount = worksOrder.Count;
            if (workCount == 0) return;

            if (workCount > 1)
                ws.InsertRow(startRow + 1, workCount - 1, startRow);

            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = worksOrder[idx];
                var info = workInfo[workId];

                ws.Cells[row, 1].Value = info.Code;
                ws.Cells[row, 2].Value = info.Name;
                AutoFitRowHeightByText(ws, row, 2, 2, "Arial", 11f);

                for (int d = 0; d < 7; d++)
                {
                    minsMap.TryGetValue((workId, d), out int m);
                    var cell = ws.Cells[row, startCol + d];
                    cell.Value = MinutesToExcelTime(m);
                    cell.Style.Numberformat.Format = "[h]:mm";
                }

                ws.Cells[row, 10].Formula = $"SUM(C{row}:I{row})";
                ws.Cells[row, 10].Style.Numberformat.Format = "[h]:mm";
            }
        }
        #endregion

        #region Утилиты (Наши новые прокачанные методы)

        // Специальный метод для Листа 1 Комплекса, который достает ВСЕ недели до нужной даты
        private static List<WorkPeriod> GetActiveWeeksUpTo(DateTime programStart, DateTime endDate, HashSet<DateTime> activeDates)
        {
            var results = new List<WorkPeriod>();
            DateTime currentStart = programStart.Date;
            int activeWeekNum = 1;

            while (currentStart <= endDate.Date)
            {
                int daysToSunday = ((int)DayOfWeek.Sunday - (int)currentStart.DayOfWeek + 7) % 7;
                DateTime currentEnd = currentStart.AddDays(daysToSunday);

                bool hasHours = activeDates != null && activeDates.Any(d => d >= currentStart && d <= currentEnd);

                if (hasHours)
                {
                    results.Add(new WorkPeriod { WeekNum = activeWeekNum, Start = currentStart, End = currentEnd });
                    activeWeekNum++;
                }
                currentStart = currentEnd.AddDays(1);
            }
            return results;
        }

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

            if (string.IsNullOrWhiteSpace(text)) return;

            float widthPx = 0;
            for (int col = range.Start.Column; col <= range.End.Column; col++)
                widthPx += (float)(ws.Column(col).Width * 7.0 + 5.0);

            if (widthPx < 20) widthPx = 20;

            bool isBold = range.Style.Font.Bold;

            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font(fontName, fontSize, isBold ? FontStyle.Bold : FontStyle.Regular))
            {
                var size = g.MeasureString(text, font, new SizeF(widthPx, 10000));
                double heightPt = size.Height * 72.0 / g.DpiY;

                // Наша бронебойная формула запаса
                heightPt = (heightPt * 1.15) + 10;

                double currentHeight = ws.Row(range.Start.Row).Height;
                if (heightPt > currentHeight)
                {
                    ws.Row(range.Start.Row).CustomHeight = true;
                    ws.Row(range.Start.Row).Height = heightPt;
                }
            }
        }

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

                // Наша бронебойная формула запаса
                heightPt = (heightPt * 1.15) + 10;

                ws.Row(row).CustomHeight = true;
                ws.Row(row).Height = heightPt < 16 ? 16 : heightPt;
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
        #endregion
    }
}