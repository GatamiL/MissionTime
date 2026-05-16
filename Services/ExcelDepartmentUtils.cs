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
    public static class ExcelDepartmentUtils
    {
        #region Вспомогательные классы для данных
        private class EmployeeData
        {
            public long EphId { get; set; }
            public string Fio { get; set; }
            public string PositionName { get; set; }
            public string GroupName { get; set; } 
            public DataTable Works { get; set; }
            public DataTable Mins { get; set; }
        }

        private class WorkNode
        {
            public long WorkId { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public int[] TotalMins { get; set; } = new int[7];
            public List<EmployeeWorkNode> Employees { get; set; } = new List<EmployeeWorkNode>();
        }

        private class EmployeeWorkNode
        {
            public string Fio { get; set; }
            public string Position { get; set; }
            public int[] Mins { get; set; } = new int[7];
        }

        private class WorkPeriod
        {
            public int WeekNum { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }
        #endregion

        /// <summary>
        /// Главный метод генерации отчета по отделу.
        /// </summary>
        public static void GenerateReport(
            string templatePath,
            string outPath,
            DbService db,
            long programId,
            long departmentId,
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

            //ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            EnsureDirectory(Path.GetDirectoryName(outPath));

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
                throw new InvalidOperationException("У программы не задана дата начала (DateStart).");

            // Обрезаем период так же, как в ExcelEmployeeUtils
            if (periodStart < programStart) periodStart = programStart;
            if (periodEnd > programEnd) periodEnd = programEnd;
            if (periodStart > periodEnd) periodStart = periodEnd;

            string departmentName = "Отдел";
            string complexName = "";

            var dtDep = db.Query("SELECT Name, ParentId FROM Departments WHERE Id = @id", new SQLiteParameter("@id", departmentId));
            if (dtDep.Rows.Count > 0)
            {
                departmentName = Convert.ToString(dtDep.Rows[0]["Name"]);
                if (dtDep.Rows[0]["ParentId"] != DBNull.Value)
                {
                    var dtParent = db.Query("SELECT Name FROM Departments WHERE Id = @pid", new SQLiteParameter("@pid", dtDep.Rows[0]["ParentId"]));
                    if (dtParent.Rows.Count > 0) complexName = Convert.ToString(dtParent.Rows[0]["Name"]);
                }
            }

            // Получаем все дочерние отделы через рекурсивный CTE
            string treeSql = @"
                WITH RECURSIVE
                DeptTree(Id) AS (
                    SELECT Id FROM Departments WHERE Id = @rootId
                    UNION ALL
                    SELECT d.Id FROM Departments d
                    INNER JOIN DeptTree t ON d.ParentId = t.Id
                )
                SELECT Id FROM DeptTree;
            ";
            var dtTree = db.Query(treeSql, new SQLiteParameter("@rootId", departmentId));
            var depIds = new List<long>();
            foreach (DataRow r in dtTree.Rows) depIds.Add(Convert.ToInt64(r["Id"]));
            string depIdsStr = string.Join(",", depIds);

            // --- 1. ВЫГРУЖАЕМ ДАННЫЕ ДЛЯ ЛИСТА 2 (ТЕКУЩАЯ НЕДЕЛЯ) ---
            string startStr = periodStart.ToString("yyyy-MM-dd");
            string endStr = periodEnd.ToString("yyyy-MM-dd");

            string weekSql = $@"
                SELECT 
                    eph.Id as EphId,
                    e.Fio,
                    p.Name as PositionName,
                    (SELECT d.Name FROM Departments d WHERE d.Id = eph.DepartmentId AND d.Level >= 4 LIMIT 1) as GroupName,
                    w.Id as WorkId,
                    w.SpecialCode,
                    w.Name as WorkName,
                    te.WorkDate,
                    SUM(te.Minutes) as MinSum
                FROM TimesheetEntry te
                JOIN Timesheet ts ON te.TimesheetId = ts.Id
                JOIN EmployeePositionsHistory eph ON ts.EmployeePositionsHistoryId = eph.Id
                JOIN Employees e ON eph.EmployeeId = e.Id
                JOIN Positions p ON eph.PositionId = p.Id
                JOIN ListOfWork w ON te.WorkId = w.Id
                WHERE ts.ProgramId = @progId 
                  AND te.WorkDate >= @start AND te.WorkDate <= @end
                  AND eph.DepartmentId IN ({depIdsStr})
                GROUP BY eph.Id, e.Fio, p.Name, w.Id, w.SpecialCode, w.Name, te.WorkDate
                HAVING SUM(te.Minutes) > 0
            ";
            
            var dtWeek = db.Query(weekSql, 
                new SQLiteParameter("@progId", programId),
                new SQLiteParameter("@start", startStr),
                new SQLiteParameter("@end", endStr));

            var empDataList = new List<EmployeeData>();
            var groupedByEph = dtWeek.AsEnumerable().GroupBy(r => new {
                EphId = r.Field<long>("EphId"),
                Fio = r.Field<string>("Fio"),
                PositionName = r.Field<string>("PositionName"),
                GroupName = r.Field<string>("GroupName")
            });

            foreach (var g in groupedByEph)
            {
                var worksDt = new DataTable();
                worksDt.Columns.Add("WorkId", typeof(long));
                worksDt.Columns.Add("SpecialCode", typeof(string));
                worksDt.Columns.Add("Name", typeof(string));

                var minsDt = new DataTable();
                minsDt.Columns.Add("WorkId", typeof(long));
                minsDt.Columns.Add("WorkDate", typeof(string));
                minsDt.Columns.Add("MinSum", typeof(int));

                var worksSet = new HashSet<long>();
                foreach (var row in g)
                {
                    long wId = row.Field<long>("WorkId");
                    if (!worksSet.Contains(wId))
                    {
                        worksSet.Add(wId);
                        worksDt.Rows.Add(wId, row.Field<string>("SpecialCode"), row.Field<string>("WorkName"));
                    }
                    minsDt.Rows.Add(wId, row.Field<string>("WorkDate"), Convert.ToInt32(row.Field<long>("MinSum")));
                }

                empDataList.Add(new EmployeeData
                {
                    EphId = g.Key.EphId,
                    Fio = (g.Key.Fio ?? "").Trim(),
                    PositionName = (g.Key.PositionName ?? "").Trim(),
                    GroupName = (g.Key.GroupName ?? "").Trim(),
                    Works = worksDt,
                    Mins = minsDt
                });
            }

            // --- 2. ВЫГРУЖАЕМ ДАННЫЕ ДЛЯ ЛИСТА 1 (СВОДКА ЗА ВСЕ ВРЕМЯ) ---
            DateTime endOfMonth = new DateTime(periodEnd.Year, periodEnd.Month, DateTime.DaysInMonth(periodEnd.Year, periodEnd.Month));
            string endOfMonthStr = endOfMonth.ToString("yyyy-MM-dd");

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
                new SQLiteParameter("@endMonth", endOfMonthStr));

            var activeDates = new HashSet<DateTime>();
            foreach (DataRow r in fullAgg.Rows)
            {
                if (DateTime.TryParse(r["WorkDate"].ToString(), out DateTime wd))
                {
                    activeDates.Add(wd);
                }
            }

            var activeWeeks = GetActiveWeeks(programStart, endOfMonth, activeDates, programEnd);
            var historyWeeks = activeWeeks.Where(w => w.Start <= periodEnd.Date).ToList();
            var displayWeeks = historyWeeks.Skip(Math.Max(0, historyWeeks.Count - 6)).ToList();

            var templateFi = new FileInfo(templatePath);
            using (var package = new ExcelPackage(templateFi))
            {
                package.Compatibility.IsWorksheets1Based = true;
                var wb = package.Workbook;
                if (wb.Worksheets.Count < 2)
                    throw new InvalidOperationException("Шаблон отчета по отделу должен содержать минимум 2 листа.");

                // --- Лист 1 (Сводка: активные недели с начала программы) ---
                var ws1 = wb.Worksheets[1];
                
                SetMergedCellText(ws1, "C4:I4", programName);
                SetMergedCellText(ws1, "C8:I8", complexName);
                SetMergedCellText(ws1, "C10:I10", departmentName);
                int ins1 = FillSheet1_Summary(ws1, displayWeeks, fullAgg);
                
                int finalRow1 = 18 + ins1;
                ExcelImageHelper.InsertSignature(ws1, responsibleId, $"G{finalRow1}", db);

                // --- Лист 2 (Детализация: Работа -> Сотрудники за неделю) ---
                var ws2 = wb.Worksheets[2];

                SetMergedCellText(ws2, "C4:J4", programName);
                SetMergedCellText(ws2, "C8:J8", complexName);
                SetMergedCellText(ws2, "C10:J10", departmentName);
                ws2.Cells["C12"].Value = periodStart;
                ws2.Cells["D12"].Value = periodEnd;
                ws2.Cells["C12:D12"].Style.Numberformat.Format = "dd.MM.yyyy";

                int ins2 = FillSheet2_DepartmentDaily(ws2, periodStart, periodEnd, empDataList);
                int finalRow2 = 19 + ins2;
                ExcelImageHelper.InsertSignature(ws2, responsibleId, $"G{finalRow2}", db);

                foreach (var w in wb.Worksheets) w.View.TabSelected = false;
                wb.Worksheets[1].View.TabSelected = true;
                wb.View.ActiveTab = 0;

                package.SaveAs(new FileInfo(outPath));
            }
        }

        #region Вспомогательные методы генерации листов
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

        private static int FillSheet1_Summary(ExcelWorksheet ws, List<WorkPeriod> weeks, DataTable aggData)
        {
            int inserted = 0;
            int headerRow = 14;
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

        private static int FillSheet2_DepartmentDaily(ExcelWorksheet ws, DateTime periodStart, DateTime periodEnd, List<EmployeeData> empDataList)
        {
            int inserted = 0;
            int headerRow = 16;
            int startCol = 3; 

            int diff = (7 + ((int)periodStart.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
            DateTime weekMonday = periodStart.Date.AddDays(-diff);

            for (int i = 0; i < 7; i++)
            {
                DateTime day = weekMonday.AddDays(i);
                if (day >= periodStart.Date && day <= periodEnd.Date)
                    ws.Cells[headerRow, startCol + i].Value = day.ToString("dd.MM.yy");
            }

            var worksDict = new Dictionary<long, WorkNode>();

            foreach (var emp in empDataList)
            {
                var empNodes = new Dictionary<long, EmployeeWorkNode>();

                foreach (DataRow wRow in emp.Works.Rows)
                {
                    long wId = Convert.ToInt64(wRow["WorkId"]);
                    if (!worksDict.ContainsKey(wId))
                    {
                        worksDict[wId] = new WorkNode
                        {
                            WorkId = wId,
                            Code = Convert.ToString(wRow["SpecialCode"]) ?? "",
                            Name = Convert.ToString(wRow["Name"]) ?? ""
                        };
                    }
                    var eNode = new EmployeeWorkNode { Fio = emp.Fio, Position = emp.PositionName };
                    worksDict[wId].Employees.Add(eNode);
                    empNodes[wId] = eNode;
                }

                foreach (DataRow mRow in emp.Mins.Rows)
                {
                    long wId = Convert.ToInt64(mRow["WorkId"]);
                    DateTime wd = DateTime.Parse(Convert.ToString(mRow["WorkDate"])).Date;
                    int dayIdx = (int)(wd - weekMonday).TotalDays;

                    if (dayIdx >= 0 && dayIdx < 7 && empNodes.TryGetValue(wId, out var eNode))
                    {
                        int minSum = Convert.ToInt32(mRow["MinSum"]);
                        eNode.Mins[dayIdx] += minSum;
                        worksDict[wId].TotalMins[dayIdx] += minSum;
                    }
                }
            }

            foreach (var w in worksDict.Values)
                w.Employees = w.Employees.Where(e => e.Mins.Sum() > 0).OrderBy(e => e.Fio).ToList();

            var validWorks = worksDict.Values.Where(w => w.TotalMins.Sum() > 0).OrderBy(w => w.Name).ToList();

            int startRow = 17;
            int totalRowsNeeded = validWorks.Count + validWorks.Sum(w => w.Employees.Count);
            if (totalRowsNeeded == 0) return 0;

            if (totalRowsNeeded > 1)
            {
                inserted = totalRowsNeeded - 1;
                ws.InsertRow(startRow + 1, inserted, startRow);
            }

            int currentRow = startRow;
            foreach (var w in validWorks)
            {
                ws.Cells[currentRow, 1].Value = w.Code;
                ws.Cells[currentRow, 2].Value = w.Name;

                var workRowRange = ws.Cells[currentRow, 1, currentRow, 10];
                workRowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                workRowRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                workRowRange.Style.Font.Bold = true;

                ws.Cells[currentRow, 2].Style.Font.Italic = false;
                ws.Row(currentRow).CustomHeight = false;

                for (int d = 0; d < 7; d++)
                {
                    DateTime day = weekMonday.AddDays(d);
                    if (day >= periodStart.Date && day <= periodEnd.Date)
                    {
                        var cell = ws.Cells[currentRow, startCol + d];
                        cell.Value = MinutesToExcelTime(w.TotalMins[d]);
                        cell.Style.Numberformat.Format = "[h]:mm";
                    }
                }

                ws.Cells[currentRow, 10].Formula = $"SUM(C{currentRow}:I{currentRow})";
                ws.Cells[currentRow, 10].Style.Numberformat.Format = "[h]:mm";

                currentRow++;

                foreach (var emp in w.Employees)
                {
                    ws.Cells[currentRow, 1].Value = null; 
                    ws.Cells[currentRow, 2].Value = $"{emp.Fio} ({emp.Position})";
                    ws.Cells[currentRow, 2].Style.Font.Bold = false;
                    ws.Cells[currentRow, 2].Style.Font.Italic = true; 
                    ws.Cells[currentRow, 2].Style.Indent = 2; 

                    ws.Row(currentRow).CustomHeight = false;

                    for (int d = 0; d < 7; d++)
                    {
                        DateTime day = weekMonday.AddDays(d);
                        if (day >= periodStart.Date && day <= periodEnd.Date)
                        {
                            var cell = ws.Cells[currentRow, startCol + d];
                            cell.Value = MinutesToExcelTime(emp.Mins[d]);
                            cell.Style.Numberformat.Format = "[h]:mm";
                            cell.Style.Font.Bold = false;
                        }
                    }
                    ws.Cells[currentRow, 10].Formula = $"SUM(C{currentRow}:I{currentRow})";
                    ws.Cells[currentRow, 10].Style.Numberformat.Format = "[h]:mm";
                    ws.Cells[currentRow, 10].Style.Font.Bold = false;

                    currentRow++;
                }
            }
            return inserted;
        }

        #endregion

        #region Утилиты (расчет высоты, имен, стилей)
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

        private static void SetMergedCellText(ExcelWorksheet ws, string rangeAddress, string text)
        {
            var range = ws.Cells[rangeAddress];
            text = text ?? "";
            range.Value = text;

            range.Style.WrapText = true;
            range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            range.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

            if (string.IsNullOrWhiteSpace(text)) return;

            string fontName = string.IsNullOrEmpty(range.Style.Font.Name) ? "Arial" : range.Style.Font.Name;
            float fontSize = range.Style.Font.Size;
            bool isBold = range.Style.Font.Bold;

            float widthPx = 0;
            for (int col = range.Start.Column; col <= range.End.Column; col++)
            {
                widthPx += (float)(ws.Column(col).Width * 7.0 + 5.0);
            }

            if (widthPx < 20) widthPx = 20;

            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font(fontName, fontSize, isBold ? FontStyle.Bold : FontStyle.Regular))
            {
                var size = g.MeasureString(text, font, new SizeF(widthPx, 10000));
                double heightPt = size.Height * 72.0 / g.DpiY;
                heightPt = (heightPt * 1.15) + 10;

                double currentHeight = ws.Row(range.Start.Row).Height;
                if (heightPt > currentHeight)
                {
                    ws.Row(range.Start.Row).CustomHeight = true;
                    ws.Row(range.Start.Row).Height = heightPt;
                }
            }
        }

        private static void AutoFitRowHeightByText(ExcelWorksheet ws, int row, int colFrom, int colTo, string fontName = "Arial", float fontSize = 12f, float paddingPx = 6f)
        {
            float widthPx = 0;
            for (int c = colFrom; c <= colTo; c++)
                widthPx += (float)(ws.Column(c).Width * 7.0 + 5.0);

            if (widthPx < 20) widthPx = 20;

            string text = Convert.ToString(ws.Cells[row, colFrom].Value) ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                ws.Row(row).CustomHeight = true;
                ws.Row(row).Height = 18;
                return;
            }

            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font(fontName, fontSize))
            {
                var size = g.MeasureString(text, font, new SizeF(widthPx, 10000));
                double heightPt = (size.Height + paddingPx) * 72.0 / g.DpiY;
                heightPt = (heightPt * 1.15) + 10;

                ws.Row(row).CustomHeight = true;
                ws.Row(row).Height = heightPt < 18 ? 18 : heightPt;
            }
        }
        #endregion
    }
}
