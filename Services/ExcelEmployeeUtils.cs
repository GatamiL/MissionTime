using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

namespace MissionTime.Services
{
    public static class ExcelEmployeeUtils
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
        #endregion

        /// <summary>
        /// Главный метод генерации отчета. Берет на себя всю работу с БД и Excel.
        /// </summary>
        public static void GenerateReport(
            string templatePath,
            string outPath,
            DbService db,
            long programId,
            long departmentId,
            DateTime periodStart,
            DateTime periodEnd,
            string doneFio,
            string checkedFio,
            long? executorId = null,
            long? reviewerId = null)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("Путь к шаблону не указан.");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Шаблон Excel не найден", templatePath);
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            // Устанавливаем контекст лицензии EPPlus
            //ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            EnsureDirectory(Path.GetDirectoryName(outPath));

            string programName = "Программа";
            var dtProg = db.Query("SELECT Name, DateStart, DateEnd FROM Programs WHERE Id = @id", new SQLiteParameter("@id", programId));
            if (dtProg.Rows.Count > 0) 
            {
                programName = Convert.ToString(dtProg.Rows[0]["Name"]);
                
                if (dtProg.Rows[0]["DateStart"] != DBNull.Value && DateTime.TryParse(dtProg.Rows[0]["DateStart"].ToString(), out DateTime progStart))
                {
                    if (periodStart < progStart) periodStart = progStart;
                }
                
                if (dtProg.Rows[0]["DateEnd"] != DBNull.Value && DateTime.TryParse(dtProg.Rows[0]["DateEnd"].ToString(), out DateTime progEnd))
                {
                    if (periodEnd > progEnd) periodEnd = progEnd;
                }
            }

            // Если после обрезки старт оказался больше конца (например, период вообще вне программы)
            if (periodStart > periodEnd)
            {
                periodStart = periodEnd; 
            }

            string departmentName = "Отдел";
            string complexName = "";
            var dtDep = db.Query("SELECT Name, ParentId FROM Departments WHERE Id = @id", new SQLiteParameter("@id", departmentId));
            if (dtDep.Rows.Count > 0) 
            {
                departmentName = Convert.ToString(dtDep.Rows[0]["Name"]);
                if (dtDep.Rows[0]["ParentId"] != DBNull.Value)
                {
                    var dtParent = db.Query("SELECT Name FROM Departments WHERE Id = @id", new SQLiteParameter("@id", dtDep.Rows[0]["ParentId"]));
                    if (dtParent.Rows.Count > 0) complexName = Convert.ToString(dtParent.Rows[0]["Name"]);
                }
            }

            string startStr = periodStart.ToString("yyyy-MM-dd");
            string endStr = periodEnd.ToString("yyyy-MM-dd");

            // --- ОПТИМИЗАЦИЯ: Все данные вытаскиваем за 4 быстрых запроса ---

            // 1. Сотрудники, у которых есть часы в этом периоде по этой программе
            string sqlEmployees = @"
                SELECT DISTINCT h.Id as EphId, e.Fio, p.Name as PositionName
                FROM EmployeePositionsHistory h
                JOIN Employees e ON h.EmployeeId = e.Id
                JOIN Positions p ON h.PositionId = p.Id
                JOIN Timesheet ts ON h.Id = ts.EmployeePositionsHistoryId
                JOIN TimesheetEntry te ON ts.Id = te.TimesheetId
                WHERE ts.ProgramId = @progId 
                  AND te.WorkDate >= @start 
                  AND te.WorkDate <= @end
                  AND te.Minutes > 0
                  AND (h.DepartmentId = @depId OR h.DepartmentId IN (SELECT Id FROM Departments WHERE ParentId = @depId))
                ORDER BY e.Fio";

            var dtEmployees = db.Query(sqlEmployees, 
                new SQLiteParameter("@progId", programId),
                new SQLiteParameter("@start", startStr),
                new SQLiteParameter("@end", endStr),
                new SQLiteParameter("@depId", departmentId));

            // 2. Группы для сотрудников (Level 3)
            string sqlGroups = @"
                SELECT h.Id as EphId, 
                       CASE 
                           WHEN d.Level = 3 THEN d.Name
                           WHEN d.Level = 4 THEN (SELECT Name FROM Departments WHERE Id = d.ParentId)
                           ELSE d.Name
                       END as GroupName
                FROM EmployeePositionsHistory h
                JOIN Departments d ON h.DepartmentId = d.Id
                WHERE d.Level >= 3 AND h.DepartmentId != @depId";
            var dtGroups = db.Query(sqlGroups, new SQLiteParameter("@depId", departmentId));
            var groupsMap = new Dictionary<long, string>();
            foreach(DataRow r in dtGroups.Rows) groupsMap[Convert.ToInt64(r["EphId"])] = Convert.ToString(r["GroupName"]);

            // 3. Уникальные работы
            string sqlWorks = @"
                SELECT DISTINCT ts.EmployeePositionsHistoryId as EphId, te.WorkId, lw.SpecialCode, lw.Name
                FROM Timesheet ts
                JOIN TimesheetEntry te ON ts.Id = te.TimesheetId
                JOIN ListOfWork lw ON te.WorkId = lw.Id
                WHERE ts.ProgramId = @progId
                  AND te.WorkDate >= @start
                  AND te.WorkDate <= @end
                  AND te.Minutes > 0
                ORDER BY lw.Name";
            var dtAllWorks = db.Query(sqlWorks, 
                new SQLiteParameter("@progId", programId),
                new SQLiteParameter("@start", startStr),
                new SQLiteParameter("@end", endStr));

            // 4. Часы
            string sqlMins = @"
                SELECT ts.EmployeePositionsHistoryId as EphId, te.WorkId, te.WorkDate, SUM(te.Minutes) as MinSum
                FROM Timesheet ts
                JOIN TimesheetEntry te ON ts.Id = te.TimesheetId
                WHERE ts.ProgramId = @progId
                  AND te.WorkDate >= @start
                  AND te.WorkDate <= @end
                  AND te.Minutes > 0
                GROUP BY ts.EmployeePositionsHistoryId, te.WorkId, te.WorkDate";
            var dtAllMins = db.Query(sqlMins,
                new SQLiteParameter("@progId", programId),
                new SQLiteParameter("@start", startStr),
                new SQLiteParameter("@end", endStr));


            var empDataList = new List<EmployeeData>();
            foreach(DataRow rEmp in dtEmployees.Rows)
            {
                long ephId = Convert.ToInt64(rEmp["EphId"]);
                groupsMap.TryGetValue(ephId, out string groupName);
                
                var works = dtAllWorks.Clone();
                foreach(DataRow row in dtAllWorks.Select($"EphId = {ephId}")) works.ImportRow(row);

                var mins = dtAllMins.Clone();
                foreach(DataRow row in dtAllMins.Select($"EphId = {ephId}")) mins.ImportRow(row);

                if (works.Rows.Count > 0)
                {
                    empDataList.Add(new EmployeeData
                    {
                        EphId = ephId,
                        Fio = Convert.ToString(rEmp["Fio"]),
                        PositionName = Convert.ToString(rEmp["PositionName"]),
                        GroupName = groupName ?? "",
                        Works = works,
                        Mins = mins
                    });
                }
            }

            var templateFi = new FileInfo(templatePath);
            // --- ОПТИМИЗАЦИЯ: Создаем объекты для расчета графики один раз на весь отчет ---
            using (var bmp = new Bitmap(1, 1))
            using (var graphics = Graphics.FromImage(bmp))
            using (var package = new ExcelPackage(templateFi))
            {
                package.Compatibility.IsWorksheets1Based = true;
                var wb = package.Workbook;
                var baseWs = wb.Worksheets[1];
                
                // Вставляем подписи ПОЗЖЕ, после того как листы будут раздуты работами,
                // иначе EPPlus ломает координаты картинок при вставке строк.
                
                var tmplWs = wb.Worksheets.Add("tmpl_" + Guid.NewGuid().ToString("N").Substring(0, 8), baseWs);

                if (empDataList.Count == 0)
                {
                    FillEmployeeSheet(baseWs, programName, complexName, departmentName, periodStart, periodEnd, "", "", doneFio, checkedFio, graphics);
                    baseWs.Name = SafeWorksheetName("Нет сотрудников");
                    package.SaveAs(new FileInfo(outPath));
                    return;
                }

                var fioCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                bool baseUsed = false;

                foreach (var emp in empDataList)
                {
                    ExcelWorksheet ws;
                    if (!baseUsed)
                    {
                        ws = baseWs;
                        baseUsed = true;
                    }
                    else
                    {
                        ws = wb.Worksheets.Add("tmp_" + Guid.NewGuid().ToString("N").Substring(0, 8), tmplWs);
                    }

                    string fullDeptName = string.IsNullOrWhiteSpace(emp.GroupName)
                        ? departmentName
                        : $"{departmentName} {emp.GroupName}";

                    FillEmployeeSheet(ws, programName, complexName, fullDeptName, periodStart, periodEnd, emp.Fio, emp.PositionName, doneFio, checkedFio, graphics);
                    
                    int insertedCount = FillSheet_EmployeeDaily(ws, periodStart, periodEnd, emp.Works, emp.Mins, graphics);

                    // А ВОТ ТЕПЕРЬ, КОГДА ЛИСТ РАЗДУЛСЯ, ВЫЧИСЛЯЕМ КОНЕЧНУЮ СТРОКУ И ВТЫКАЕМ ПОДПИСЬ!
                    int finalRow = 25 + insertedCount;
                    ExcelImageHelper.InsertSignature(ws, executorId, $"C{finalRow}", db);
                    ExcelImageHelper.InsertSignature(ws, reviewerId, $"G{finalRow}", db);

                    string sheetName = BuildNameWithDuplicateSuffix(emp.Fio, fioCounts);
                    sheetName = MakeUniqueSheetName(wb, sheetName);
                    ws.Name = sheetName;
                }

                if (wb.Worksheets[tmplWs.Name] != null) wb.Worksheets.Delete(tmplWs);

                foreach (var w in wb.Worksheets) w.View.TabSelected = false;
                wb.Worksheets[1].View.TabSelected = true;
                wb.View.ActiveTab = 0;

                package.SaveAs(new FileInfo(outPath));
            }
        }

        #region Вспомогательные методы генерации листов
        
        private static int FillSheet_EmployeeDaily(ExcelWorksheet ws, DateTime periodStart, DateTime periodEnd, DataTable works, DataTable minutes, Graphics graphics)
        {
            int insertedRows = 0;
            int daysCount = (int)(periodEnd.Date - periodStart.Date).TotalDays + 1;
            if (daysCount <= 0) return 0;

            int diff = (7 + ((int)periodStart.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
            DateTime weekMonday = periodStart.Date.AddDays(-diff);

            for (int i = 0; i < daysCount; i++)
            {
                DateTime day = periodStart.Date.AddDays(i);
                int colOffset = (int)(day - weekMonday).TotalDays;
                ws.Cells[21, 4 + colOffset].Value = day.ToString("dd.MM.yy");
            }

            var minsMap = new Dictionary<(long WorkId, DateTime Day), int>();
            foreach (DataRow r in minutes.Rows)
            {
                long workId = Convert.ToInt64(r["WorkId"]);
                DateTime day = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                minsMap[(workId, day)] = Convert.ToInt32(r["MinSum"]);
            }

            int startRow = 22;
            int workCount = works?.Rows.Count ?? 0;
            if (workCount == 0) return 0;

            if (workCount > 1)
            {
                insertedRows = workCount - 1;
                ws.InsertRow(startRow + 1, insertedRows, startRow);
            }

            var bRange = ws.Cells[startRow, 2, startRow + workCount - 1, 2];
            bRange.Style.WrapText = true;
            bRange.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            bRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;

            var timeRange = ws.Cells[startRow, 4, startRow + workCount - 1, 10];
            timeRange.Style.Numberformat.Format = "[h]:mm";

            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = Convert.ToInt64(works.Rows[idx]["WorkId"]);
                string code = Convert.ToString(works.Rows[idx]["SpecialCode"]) ?? "";
                string name = Convert.ToString(works.Rows[idx]["Name"]) ?? "";

                ws.Cells[row, 1].Value = idx + 1;
                ws.Cells[row, 2].Value = name;
                ws.Cells[row, 3].Value = code;

                ws.Row(row).CustomHeight = false;

                for (int d = 0; d < daysCount; d++)
                {
                    DateTime day = periodStart.Date.AddDays(d);
                    int colOffset = (int)(day - weekMonday).TotalDays;
                    minsMap.TryGetValue((workId, day), out int m);
                    ws.Cells[row, 4 + colOffset].Value = MinutesToExcelTime(m);
                }
            }
            return insertedRows;
        }

        private static void FillEmployeeSheet(ExcelWorksheet ws, string programName, string complexName, string departmentName, DateTime periodStart, DateTime periodEnd, string employeeFio, string positionName, string doneFio, string checkedFio, Graphics graphics)
        {
            SetMergedCellText(ws, "C4:J4", programName, graphics);
            SetMergedCellText(ws, "C8:J8", complexName, graphics);
            SetMergedCellText(ws, "C10:J10", departmentName, graphics);
            SetMergedCellText(ws, "C12:J12", employeeFio, graphics);
            SetMergedCellText(ws, "C14:J14", positionName, graphics);

            ws.Cells["C16"].Value = periodStart;
            ws.Cells["D16"].Value = periodEnd;
            ws.Cells["C16:D16"].Style.Numberformat.Format = "dd.MM.yyyy";

            ws.Cells["E25"].Value = doneFio ?? "";
            ws.Cells["I25"].Value = checkedFio ?? "";

            ws.Row(16).CustomHeight = true;
            ws.Row(16).Height = 15.75;
            ws.Cells["C16:D16"].Style.WrapText = false;
            ws.Cells["C16:D16"].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            ws.Cells["C16:D16"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        }
        #endregion

        #region Утилиты (расчет высоты, имен, стилей)
        
        private static readonly Regex _deptRegex = new Regex(@"(?i)отдел\s*([0-9]+[а-яА-Яa-zA-Z]?)", RegexOptions.Compiled);

        public static string GetShortDepartmentName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "Отдел";

            var match = _deptRegex.Match(fullName);

            if (match.Success)
            {
                return $"Отд {match.Groups[1].Value}";
            }

            if (fullName.Length > 20)
                return fullName.Substring(0, 20).Trim() + "..";

            return fullName;
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

        private static void SetMergedCellText(ExcelWorksheet ws, string rangeAddress, string text, Graphics graphics)
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

            using (var font = new Font(fontName, fontSize, isBold ? FontStyle.Bold : FontStyle.Regular))
            {
                var size = graphics.MeasureString(text, font, new SizeF(widthPx, 10000));
                double heightPt = size.Height * 72.0 / graphics.DpiY;
                heightPt = (heightPt * 1.15) + 10;

                double currentHeight = ws.Row(range.Start.Row).Height;
                if (heightPt > currentHeight)
                {
                    ws.Row(range.Start.Row).CustomHeight = true;
                    ws.Row(range.Start.Row).Height = heightPt;
                }
            }
        }

        private static void AutoFitRowHeightByText(ExcelWorksheet ws, int row, int colFrom, int colTo, Graphics graphics, string fontName = "Arial", float fontSize = 12f, float paddingPx = 6f)
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

            using (var font = new Font(fontName, fontSize))
            {
                var size = graphics.MeasureString(text, font, new SizeF(widthPx, 10000));
                double heightPt = (size.Height + paddingPx) * 72.0 / graphics.DpiY;
                heightPt = (heightPt * 1.15) + 10;

                ws.Row(row).CustomHeight = true;
                ws.Row(row).Height = heightPt < 18 ? 18 : heightPt;
            }
        }

        private static string SafeWorksheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Sheet";
            foreach (var c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(c, '_');
            return name.Length > 31 ? name.Substring(0, 31).Trim() : name.Trim();
        }

        private static string BuildNameWithDuplicateSuffix(string fio, Dictionary<string, int> fioCounts)
        {
            string key = string.IsNullOrWhiteSpace(fio) ? "Сотрудник" : fio;
            if (!fioCounts.TryGetValue(key, out int n)) n = 0;
            fioCounts[key] = ++n;
            return n == 1 ? key : $"{key}({n})";
        }

        private static string MakeUniqueSheetName(ExcelWorkbook wb, string desired)
        {
            desired = SafeWorksheetName(desired);
            string name = desired;
            int k = 2;

            while (wb.Worksheets[name] != null)
            {
                string suffix = $"({k})";
                int maxBase = Math.Max(1, 31 - suffix.Length);
                name = desired.Substring(0, desired.Length > maxBase ? maxBase : desired.Length) + suffix;
                k++;
            }
            return name;
        }
        #endregion
    }
}
