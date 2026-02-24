using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MissionTime
{
    public static class ExcelDepartmentUtils
    {
        #region Вспомогательные классы для данных
        private class EmployeeData
        {
            public long EphId { get; set; }
            public string Fio { get; set; }
            public string PositionName { get; set; }
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
            string checkedFio)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("Путь к шаблону не указан.");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Шаблон Excel не найден", templatePath);
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            var prog = db.Program_GetInfo((int)programId);
            if (!prog.HasValue) throw new InvalidOperationException("Программа не найдена: " + programId);
            string programName = prog.Value.Name;

            var depNames = db.Department_GetComplexAndDepartmentNames(departmentId);
            string complexName = depNames.ComplexName;
            string departmentName = depNames.DepartmentName;

            EnsureDirectory(Path.GetDirectoryName(outPath));

            // --- 1. ОПТИМИЗАЦИЯ: Выгружаем детальные часы всех людей ОДИН РАЗ ---
            var employees = db.Employees_WorkedForProgramPeriod(departmentId, (int)programId, periodStart, periodEnd);
            var empDataList = new List<EmployeeData>();

            if (employees != null)
            {
                foreach (DataRow r in employees.Rows)
                {
                    long ephId = Convert.ToInt64(r["EPHId"]);
                    var works = db.ListOfWork_UsedForEphProgramPeriod(ephId, (int)programId, periodStart, periodEnd);
                    var mins = db.TimesheetMinutes_ByEphProgramWorkDayPeriod(ephId, (int)programId, periodStart, periodEnd);

                    if (works != null && works.Rows.Count > 0)
                    {
                        empDataList.Add(new EmployeeData
                        {
                            EphId = ephId,
                            Fio = (Convert.ToString(r["Fio"]) ?? "").Trim(),
                            PositionName = (Convert.ToString(r["PositionName"]) ?? "").Trim(),
                            Works = works,
                            Mins = mins
                        });
                    }
                }
            }

            var templateFi = new FileInfo(templatePath);
            using (var package = new ExcelPackage(templateFi))
            {
                var wb = package.Workbook;
                if (wb.Worksheets.Count < 3)
                    throw new InvalidOperationException("Шаблон должен содержать минимум 3 листа.");

                // --- Лист 1 (Сводка: активные недели с начала программы) ---
                var ws1 = wb.Worksheets[1];
                SetMergedCellText(ws1, "C4:I4", programName);
                SetMergedCellText(ws1, "C8:I8", complexName);
                SetMergedCellText(ws1, "C10:I10", departmentName);

                DateTime programStart = prog.Value.Start;
                int year = periodStart.Year;
                int month = periodStart.Month;
                DateTime reportEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

                // Собираем исторические данные помесячно
                DataTable fullAgg = null;
                DateTime iterMonth = new DateTime(programStart.Year, programStart.Month, 1);
                DateTime endMonth = new DateTime(year, month, 1);

                while (iterMonth <= endMonth)
                {
                    DateTime ms = iterMonth;
                    DateTime me = iterMonth.AddMonths(1).AddDays(-1);
                    var partAgg = db.TimesheetMinutes_ByDeptTree_Program_WorkDayPeriod(departmentId, (int)programId, ms, me);

                    if (partAgg != null && partAgg.Rows.Count > 0)
                    {
                        if (fullAgg == null) fullAgg = partAgg.Clone();
                        foreach (DataRow row in partAgg.Rows) fullAgg.ImportRow(row);
                    }
                    iterMonth = iterMonth.AddMonths(1);
                }

                var allWeeks = GetProgramWeeks(programStart, reportEnd);
                var activeDates = new HashSet<DateTime>();
                if (fullAgg != null)
                {
                    foreach (DataRow r in fullAgg.Rows)
                    {
                        if (Convert.ToInt32(r["MinSum"]) > 0)
                            activeDates.Add(DateTime.Parse(Convert.ToString(r["WorkDate"])).Date);
                    }
                }

                var activeWeeks = allWeeks.Where(w => activeDates.Any(d => d >= w.Start && d <= w.End)).ToList();
                var last6Weeks = activeWeeks.Skip(Math.Max(0, activeWeeks.Count - 6)).ToList();

                FillSheet1_Summary(ws1, last6Weeks, fullAgg);

                // --- Лист 2 (Детализация: Работа -> Сотрудники за неделю) ---
                var ws2 = wb.Worksheets[2];
                SetMergedCellText(ws2, "C4:J4", programName);
                SetMergedCellText(ws2, "C8:J8", complexName);
                SetMergedCellText(ws2, "C10:J10", departmentName);
                ws2.Cells["C12"].Value = periodStart;
                ws2.Cells["D12"].Value = periodEnd;
                ws2.Cells["C12:D12"].Style.Numberformat.Format = "dd.MM.yyyy";

                // Передаем сюда наш кэш сотрудников!
                FillSheet2_DepartmentDaily(ws2, periodStart, periodEnd, empDataList);

                // --- Лист 3 (Базовый шаблон карточки сотрудника) ---
                var baseWs = wb.Worksheets[3];
                var tmplWs = wb.Worksheets.Add("tmpl_" + Guid.NewGuid().ToString("N").Substring(0, 8), baseWs);

                if (empDataList.Count == 0)
                {
                    FillEmployeeSheet(baseWs, programName, complexName, departmentName, periodStart, periodEnd, "", "", doneFio, checkedFio);
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

                    FillEmployeeSheet(ws, programName, complexName, departmentName, periodStart, periodEnd, emp.Fio, emp.PositionName, doneFio, checkedFio);
                    FillSheet3_EmployeeDaily(ws, periodStart, periodEnd, emp.Works, emp.Mins);

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
        private static void FillSheet1_Summary(ExcelWorksheet ws, List<(int WeekNum, DateTime Start, DateTime End)> weeks, DataTable aggData)
        {
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

            int startRow = 15;
            int workCount = worksOrder.Count;
            if (workCount == 0) return;

            if (workCount > 1) ws.InsertRow(startRow + 1, workCount - 1, startRow);

            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = worksOrder[idx];
                var info = workInfo[workId];

                ws.Cells[row, 1].Value = info.Code;
                ws.Cells[row, 2].Value = info.Name;
                AutoFitRowHeightByText(ws, row, 2, 2, "Arial", 12f);

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

        private static void FillSheet2_DepartmentDaily(ExcelWorksheet ws, DateTime periodStart, DateTime periodEnd, List<EmployeeData> empDataList)
        {
            int headerRow = 16;
            int startCol = 3; // Колонка C

            for (int i = 0; i < 7; i++)
                ws.Cells[headerRow, startCol + i].Value = periodStart.Date.AddDays(i).ToString("dd.MM.yy");

            // Структурируем данные: Работа -> Сотрудники
            var worksDict = new Dictionary<long, WorkNode>();

            foreach (var emp in empDataList)
            {
                var empNodes = new Dictionary<long, EmployeeWorkNode>();

                // Добавляем работы сотрудника
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

                // Добавляем часы сотрудника
                foreach (DataRow mRow in emp.Mins.Rows)
                {
                    long wId = Convert.ToInt64(mRow["WorkId"]);
                    DateTime wd = DateTime.Parse(Convert.ToString(mRow["WorkDate"])).Date;
                    int dayIdx = (int)(wd - periodStart.Date).TotalDays;

                    if (dayIdx >= 0 && dayIdx < 7 && empNodes.TryGetValue(wId, out var eNode))
                    {
                        int minSum = Convert.ToInt32(mRow["MinSum"]);
                        eNode.Mins[dayIdx] += minSum;
                        worksDict[wId].TotalMins[dayIdx] += minSum;
                    }
                }
            }

            // Очищаем пустые записи и сортируем по алфавиту
            foreach (var w in worksDict.Values)
                w.Employees = w.Employees.Where(e => e.Mins.Sum() > 0).OrderBy(e => e.Fio).ToList();

            var validWorks = worksDict.Values.Where(w => w.TotalMins.Sum() > 0).OrderBy(w => w.Name).ToList();

            int startRow = 17;
            int totalRowsNeeded = validWorks.Count + validWorks.Sum(w => w.Employees.Count);
            if (totalRowsNeeded == 0) return;

            // Раздвигаем строки шаблона под все работы и всех сотрудников
            if (totalRowsNeeded > 1)
                ws.InsertRow(startRow + 1, totalRowsNeeded - 1, startRow);

            int currentRow = startRow;
            foreach (var w in validWorks)
            {
                // 1. СТРОКА: ИТОГО ПО РАБОТЕ (Жирным шрифтом и с серым фоном)
                ws.Cells[currentRow, 1].Value = w.Code;
                ws.Cells[currentRow, 2].Value = w.Name;

                // Красим всю строку с работой (от 1 до 10 колонки) в серый цвет
                var workRowRange = ws.Cells[currentRow, 1, currentRow, 10];
                workRowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                workRowRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray); // Светло-серый фон
                workRowRange.Style.Font.Bold = true; // Сразу делаем всю строку жирной

                ws.Cells[currentRow, 2].Style.Font.Italic = false;
                AutoFitRowHeightByText(ws, currentRow, 2, 2, "Arial", 12f);

                for (int d = 0; d < 7; d++)
                {
                    var cell = ws.Cells[currentRow, startCol + d];
                    cell.Value = MinutesToExcelTime(w.TotalMins[d]);
                    cell.Style.Numberformat.Format = "[h]:mm";
                }

                ws.Cells[currentRow, 10].Formula = $"SUM(C{currentRow}:I{currentRow})";
                ws.Cells[currentRow, 10].Style.Numberformat.Format = "[h]:mm";

                currentRow++;

                // 2. СТРОКИ: РАСШИФРОВКА ПО СОТРУДНИКАМ (Курсив)
                foreach (var emp in w.Employees)
                {
                    ws.Cells[currentRow, 1].Value = null; // Код пустой
                    ws.Cells[currentRow, 2].Value = $"{emp.Fio} ({emp.Position})";
                    ws.Cells[currentRow, 2].Style.Font.Bold = false;
                    ws.Cells[currentRow, 2].Style.Font.Italic = true; // Курсив!
                    ws.Cells[currentRow, 2].Style.Indent = 2; // Красивый отступ от левого края

                    ws.Row(currentRow).CustomHeight = true;
                    ws.Row(currentRow).Height = 16;

                    for (int d = 0; d < 7; d++)
                    {
                        var cell = ws.Cells[currentRow, startCol + d];
                        cell.Value = MinutesToExcelTime(emp.Mins[d]);
                        cell.Style.Numberformat.Format = "[h]:mm";
                        cell.Style.Font.Bold = false;
                    }
                    ws.Cells[currentRow, 10].Formula = $"SUM(C{currentRow}:I{currentRow})";
                    ws.Cells[currentRow, 10].Style.Numberformat.Format = "[h]:mm";
                    ws.Cells[currentRow, 10].Style.Font.Bold = false;

                    currentRow++;
                }
            }
        }

        private static void FillSheet3_EmployeeDaily(ExcelWorksheet ws, DateTime periodStart, DateTime periodEnd, DataTable works, DataTable minutes)
        {
            for (int i = 0; i < 7; i++)
                ws.Cells[21, 4 + i].Value = periodStart.Date.AddDays(i).ToString("dd.MM.yy");

            var minsMap = new Dictionary<(long WorkId, DateTime Day), int>();
            foreach (DataRow r in minutes.Rows)
            {
                long workId = Convert.ToInt64(r["WorkId"]);
                DateTime day = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                minsMap[(workId, day)] = Convert.ToInt32(r["MinSum"]);
            }

            int startRow = 22;
            int workCount = works?.Rows.Count ?? 0;
            if (workCount == 0) return;

            if (workCount > 1) ws.InsertRow(startRow + 1, workCount - 1, startRow);

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

                AutoFitRowHeightByText(ws, row, 2, 2, "Arial", 11f);

                for (int d = 0; d < 7; d++)
                {
                    DateTime day = periodStart.Date.AddDays(d);
                    minsMap.TryGetValue((workId, day), out int m);
                    ws.Cells[row, 4 + d].Value = MinutesToExcelTime(m);
                }
            }
        }

        private static void FillEmployeeSheet(ExcelWorksheet ws, string programName, string complexName, string departmentName, DateTime periodStart, DateTime periodEnd, string employeeFio, string positionName, string doneFio, string checkedFio)
        {
            SetMergedCellText(ws, "C4:J4", programName);
            SetMergedCellText(ws, "C8:J8", complexName);
            SetMergedCellText(ws, "C10:J10", departmentName);
            SetMergedCellText(ws, "C12:J12", employeeFio);
            SetMergedCellText(ws, "C14:J14", positionName);

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

            double fontSize = range.Style.Font.Size;
            double totalWidth = 0;
            for (int col = range.Start.Column; col <= range.End.Column; col++)
                totalWidth += ws.Column(col).Width;

            if (totalWidth <= 0) totalWidth = 10;
            double charsPerLine = totalWidth * 1.1;
            double lines = Math.Ceiling(text.Length / charsPerLine);
            if (lines < 1) lines = 1;

            ws.Row(range.Start.Row).Height = (fontSize * 1.5) * lines;
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