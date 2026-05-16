using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MissionTime.Services
{
    public static class ExcelComplexUtils
    {
        private class WorkPeriod
        {
            public int WeekNum { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }

        public static void GenerateReport(
            string templatePath,
            string outPath,
            DbService db,
            long programId,
            long complexId,
            DateTime periodStart,
            DateTime periodEnd,
            long? responsibleId = null)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("Путь к шаблону не указан.");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Шаблон Excel не найден", templatePath);
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            EnsureDirectory(Path.GetDirectoryName(outPath));

            // 1. Данные программы
            string programName = "Программа";
            DateTime programStart = DateTime.MinValue;
            DateTime programEnd = DateTime.MaxValue;

            var dtProg = db.Query("SELECT Name, DateStart, DateEnd FROM Programs WHERE Id = @id", new SQLiteParameter("@id", programId));
            if (dtProg.Rows.Count > 0)
            {
                programName = Convert.ToString(dtProg.Rows[0]["Name"]);
                if (dtProg.Rows[0]["DateStart"] != DBNull.Value && DateTime.TryParse(dtProg.Rows[0]["DateStart"].ToString(), out DateTime pStart))
                    programStart = pStart;
                if (dtProg.Rows[0]["DateEnd"] != DBNull.Value && DateTime.TryParse(dtProg.Rows[0]["DateEnd"].ToString(), out DateTime pEnd))
                    programEnd = pEnd;
            }

            if (programStart == DateTime.MinValue)
                throw new InvalidOperationException("У программы не задана дата начала.");

            // Обрезаем периоды
            if (periodStart < programStart) periodStart = programStart;
            if (periodEnd > programEnd) periodEnd = programEnd;
            if (periodStart > periodEnd) periodStart = periodEnd;

            // 2. Название комплекса
            string complexName = "Комплекс";
            var dtComp = db.Query("SELECT Name FROM Departments WHERE Id = @id", new SQLiteParameter("@id", complexId));
            if (dtComp.Rows.Count > 0) complexName = Convert.ToString(dtComp.Rows[0]["Name"]);

            // 3. Рекурсивный CTE для сбора всех детей комплекса
            string treeSql = @"
                WITH RECURSIVE DeptTree(Id) AS (
                    SELECT Id FROM Departments WHERE Id = @rootId
                    UNION ALL
                    SELECT d.Id FROM Departments d
                    INNER JOIN DeptTree t ON d.ParentId = t.Id
                )
                SELECT Id FROM DeptTree;";
            var dtTree = db.Query(treeSql, new SQLiteParameter("@rootId", complexId));
            var depIds = new List<long>();
            foreach (DataRow r in dtTree.Rows) depIds.Add(Convert.ToInt64(r["Id"]));
            string depIdsStr = string.Join(",", depIds);

            // 4. Однократный запрос всей агрегации до конца выбранного месяца
            DateTime endOfMonth = new DateTime(periodEnd.Year, periodEnd.Month, DateTime.DaysInMonth(periodEnd.Year, periodEnd.Month));
            string aggSql = $@"
                SELECT 
                    w.Id as WorkId,
                    w.SpecialCode,
                    w.Name as WorkName,
                    te.WorkDate,
                    SUM(te.Minutes) as MinSum
                FROM TimesheetEntry te
                JOIN Timesheet ts ON te.TimesheetId = ts.Id
                JOIN EmployeePositionsHistory eph ON ts.EmployeePositionsHistoryId = eph.Id
                JOIN ListOfWork w ON te.WorkId = w.Id
                WHERE ts.ProgramId = @progId 
                  AND te.WorkDate <= @endMonth
                  AND eph.DepartmentId IN ({depIdsStr})
                GROUP BY w.Id, w.SpecialCode, w.Name, te.WorkDate
                HAVING SUM(te.Minutes) > 0
            ";
            var fullAgg = db.Query(aggSql, 
                new SQLiteParameter("@progId", programId),
                new SQLiteParameter("@endMonth", endOfMonth.ToString("yyyy-MM-dd")));

            // Получаем список живых дат
            var activeDates = new HashSet<DateTime>();
            foreach (DataRow r in fullAgg.Rows)
            {
                if (DateTime.TryParse(r["WorkDate"].ToString(), out DateTime wd))
                    activeDates.Add(wd.Date);
            }

            // 5. Формирование недель для сводки
            var activeWeeks = GetActiveWeeks(programStart, endOfMonth, activeDates, programEnd);
            var historyWeeks = activeWeeks.Where(w => w.Start <= periodEnd.Date).ToList();
            var last6Weeks = historyWeeks.Skip(Math.Max(0, historyWeeks.Count - 6)).ToList();

            // 6. Генерация Excel
            var templateFi = new FileInfo(templatePath);
            using (var package = new ExcelPackage(templateFi))
            {
                package.Compatibility.IsWorksheets1Based = true;
                var wb = package.Workbook;
                if (wb.Worksheets.Count < 2)
                    throw new InvalidOperationException("Шаблон должен содержать как минимум 2 листа.");

                // --- Лист 1 (Сводка) ---
                var ws1 = wb.Worksheets[1];
                // Подпись перенесена ниже, ПОСЛЕ FillSheet1_Summary

                SetMergedCellText(ws1, "C2:I2", programName, 11f);
                SetMergedCellText(ws1, "C6:I6", complexName, 12f);
                ws1.Cells["C8"].Value = periodStart;
                ws1.Cells["D8"].Value = periodEnd;
                ws1.Cells["C8:D8"].Style.Numberformat.Format = "dd.MM.yyyy";
                int ins1 = FillSheet1_Summary(ws1, last6Weeks, fullAgg);

                int finalRow1 = 16 + ins1;
                ExcelImageHelper.InsertSignature(ws1, responsibleId, $"F{finalRow1}", db);

                // --- Лист 2 (Детализация за неделю) ---
                var ws2 = wb.Worksheets[2];

                SetMergedCellText(ws2, "C4:J4", programName, 11f);
                SetMergedCellText(ws2, "C8:J8", complexName, 12f);
                ws2.Cells["C10"].Value = periodStart;
                ws2.Cells["D10"].Value = periodEnd;
                ws2.Cells["C10:D10"].Style.Numberformat.Format = "dd.MM.yyyy";
                int ins2 = FillSheet2_ComplexWeekly(ws2, periodStart, periodEnd, fullAgg);
                
                int finalRow2 = 17 + ins2;
                ExcelImageHelper.InsertSignature(ws2, responsibleId, $"G{finalRow2}", db);

                package.SaveAs(new FileInfo(outPath));
            }
        }

        private static int FillSheet1_Summary(ExcelWorksheet ws, List<WorkPeriod> weeks, DataTable aggData)
        {
            int inserted = 0;
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

            foreach (DataRow r in aggData.Rows)
            {
                long workId = Convert.ToInt64(r["WorkId"]);
                if (!workInfo.ContainsKey(workId))
                {
                    workInfo[workId] = (Convert.ToString(r["SpecialCode"]) ?? "", Convert.ToString(r["WorkName"]) ?? "");
                    worksOrder.Add(workId);
                }

                DateTime wd = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                var week = weeks.FirstOrDefault(w => wd >= w.Start && wd <= w.End);
                if (week != null)
                {
                    int mins = Convert.ToInt32(r["MinSum"]);
                    var key = (workId, week.WeekNum);
                    minsMap[key] = minsMap.TryGetValue(key, out int cur) ? cur + mins : mins;
                }
            }

            int startRow = 13;
            int workCount = worksOrder.Count;
            if (workCount == 0) return 0;

            if (workCount > 1)
            {
                inserted = workCount - 1;
                ws.InsertRow(startRow + 1, inserted, startRow);
            }

            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = worksOrder[idx];
                var info = workInfo[workId];

                ws.Cells[row, 1].Value = info.Code;
                ws.Cells[row, 2].Value = info.Name;
                ws.Row(row).CustomHeight = false;

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
            return inserted;
        }

        private static int FillSheet2_ComplexWeekly(ExcelWorksheet ws, DateTime periodStart, DateTime periodEnd, DataTable aggData)
        {
            int inserted = 0;
            int headerRow = 14;
            int startCol = 3;

            // Учитываем Monday Anchor, как и просил пользователь!
            int diff = (7 + ((int)periodStart.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
            DateTime weekMonday = periodStart.Date.AddDays(-diff);

            for (int i = 0; i < 7; i++)
            {
                DateTime day = weekMonday.AddDays(i);
                if (day >= periodStart.Date && day <= periodEnd.Date)
                {
                    ws.Cells[headerRow, startCol + i].Value = day.ToString("dd.MM.yy");
                }
                ws.Cells[headerRow, startCol + i].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            }

            var worksOrder = new List<long>();
            var workInfo = new Dictionary<long, (string Code, string Name)>();
            var minsMap = new Dictionary<(long WorkId, DateTime Day), int>();

            foreach (DataRow r in aggData.Rows)
            {
                DateTime wd = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                if (wd >= periodStart.Date && wd <= periodEnd.Date)
                {
                    long workId = Convert.ToInt64(r["WorkId"]);
                    if (!workInfo.ContainsKey(workId))
                    {
                        workInfo[workId] = (Convert.ToString(r["SpecialCode"]) ?? "", Convert.ToString(r["WorkName"]) ?? "");
                        worksOrder.Add(workId);
                    }
                    minsMap[(workId, wd)] = Convert.ToInt32(r["MinSum"]);
                }
            }

            int startRow = 15;
            int workCount = worksOrder.Count;
            if (workCount == 0) return 0;

            if (workCount > 1)
            {
                inserted = workCount - 1;
                ws.InsertRow(startRow + 1, inserted, startRow);
            }

            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = worksOrder[idx];
                var info = workInfo[workId];

                ws.Cells[row, 1].Value = info.Code;
                ws.Cells[row, 2].Value = info.Name;
                ws.Row(row).CustomHeight = false;

                for (int d = 0; d < 7; d++)
                {
                    DateTime day = weekMonday.AddDays(d);
                    if (day >= periodStart.Date && day <= periodEnd.Date)
                    {
                        minsMap.TryGetValue((workId, day), out int m);
                        var cell = ws.Cells[row, startCol + d];
                        cell.Value = MinutesToExcelTime(m);
                        cell.Style.Numberformat.Format = "[h]:mm";
                    }
                }
                ws.Cells[row, 10].Formula = $"SUM(C{row}:I{row})";
                ws.Cells[row, 10].Style.Numberformat.Format = "[h]:mm";
            }
            return inserted;
        }

        private static List<WorkPeriod> GetActiveWeeks(DateTime programStart, DateTime endOfMonth, HashSet<DateTime> activeDates, DateTime programEnd)
        {
            var weeks = new List<WorkPeriod>();
            int diff = (7 + (programStart.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime currentWeekStart = programStart.AddDays(-diff).Date;
            
            int weekNum = 1;
            while (currentWeekStart <= endOfMonth)
            {
                DateTime currentWeekEnd = currentWeekStart.AddDays(6);
                bool isActive = false;
                for (DateTime d = currentWeekStart; d <= currentWeekEnd; d = d.AddDays(1))
                {
                    if (activeDates.Contains(d) && d >= programStart && d <= programEnd)
                    {
                        isActive = true;
                        break;
                    }
                }
                
                if (isActive)
                {
                    weeks.Add(new WorkPeriod { WeekNum = weekNum, Start = currentWeekStart, End = currentWeekEnd });
                    weekNum++;
                }
                currentWeekStart = currentWeekStart.AddDays(7);
            }
            return weeks;
        }

        private static double MinutesToExcelTime(int minutes) => Math.Max(0, minutes) / 1440.0;

        private static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "report";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            while (name.Contains("  ")) name = name.Replace("  ", " ");
            return name.Trim();
        }

        private static void SetMergedCellText(ExcelWorksheet ws, string rangeAddress, string text, float fontSize = 12f)
        {
            var range = ws.Cells[rangeAddress];
            text = text ?? "";
            range.Value = text;
            range.Style.Font.Name = "Arial";
            range.Style.Font.Size = fontSize;
            range.Style.WrapText = true;
            range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            range.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

            if (string.IsNullOrWhiteSpace(text)) return;
            
            float widthPx = 0;
            for (int col = range.Start.Column; col <= range.End.Column; col++)
                widthPx += (float)(ws.Column(col).Width * 7.0 + 5.0);
            
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Arial", fontSize, range.Style.Font.Bold ? FontStyle.Bold : FontStyle.Regular))
            {
                var size = g.MeasureString(text, font, new SizeF(widthPx < 20 ? 20 : widthPx, 10000));
                double heightPt = (size.Height * 72.0 / g.DpiY * 1.15) + 10;
                if (heightPt > ws.Row(range.Start.Row).Height)
                {
                    ws.Row(range.Start.Row).CustomHeight = true;
                    ws.Row(range.Start.Row).Height = heightPt;
                }
            }
        }
    }
}
