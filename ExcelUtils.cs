using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;

namespace MissionTime
{
    public static partial class ExcelUtils
    {
        private static float ExcelColumnWidthToPixels(double excelWidth)
        {
            // Приближенная формула: для стандартных шрифтов (Arial/Calibri)
            // Хорошо работает на практике для авто-высоты строк.
            return (float)(excelWidth * 7.0 + 5.0);
        }
        private static void AutoFitRowHeightByText(ExcelWorksheet ws,int row,int colFrom,int colTo,string fontName = "Arial",float fontSize = 12f,float paddingPx = 6f)
        {
            // Суммарная ширина колонок (в пикселях)
            float widthPx = 0;
            for (int c = colFrom; c <= colTo; c++)
                widthPx += ExcelColumnWidthToPixels(ws.Column(c).Width);

            if (widthPx < 20) widthPx = 20;

            // Берём текст из colFrom (обычно это B — имя работы)
            string text = Convert.ToString(ws.Cells[row, colFrom].Value) ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                // минимальная высота для Arial 12 (примерно)
                ws.Row(row).CustomHeight = true;
                ws.Row(row).Height = 18;
                return;
            }

            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font(fontName, fontSize))
            {
                // MeasureString считает высоту для wrap в заданную ширину
                var size = g.MeasureString(text, font, new SizeF(widthPx, 10000));

                // перевод пикселей в points (Excel Height в points)
                float dpi = g.DpiY; // обычно 96
                double heightPt = (size.Height + paddingPx) * 72.0 / dpi;

                // минимальная высота
                if (heightPt < 18) heightPt = 18;

                ws.Row(row).CustomHeight = true;
                ws.Row(row).Height = heightPt;
            }
        }
        public static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        private static void SetMergedCellText(ExcelWorksheet ws,string rangeAddress,string text)
        {
            var range = ws.Cells[rangeAddress];

            text = text ?? "";
            range.Value = text;

            range.Style.WrapText = true;
            range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            range.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

            // ---- Получаем шрифт ----
            var font = range.Style.Font;
            double fontSize = font.Size; // например 12

            // ---- Суммарная ширина объединённых колонок ----
            double totalWidth = 0;
            for (int col = range.Start.Column; col <= range.End.Column; col++)
            {
                totalWidth += ws.Column(col).Width;
            }

            if (totalWidth <= 0)
                totalWidth = 10;

            // ---- Подбор коэффициента для Arial ----
            // Для Arial 12 лучше работает 1.0 – 1.05
            double widthCoefficient = 1.1;

            double charsPerLine = totalWidth * widthCoefficient;
            double lines = Math.Ceiling(text.Length / charsPerLine);
            if (lines < 1) lines = 1;

            // ---- Базовая высота строки ----
            // Для Arial 12 стандарт ≈ 16.5–18
            double baseHeight = fontSize * 1.5;

            ws.Row(range.Start.Row).Height = baseHeight * lines;
        }
        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "report";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            while (name.Contains("  ")) name = name.Replace("  ", " ");
            return name.Trim();
        }
        public static void GenerateLaborCardFromTemplate_MultiEmployee(
            string templatePath,
            string outputPath,
            string programName,
            string complexName,
            string departmentName,
            DateTime periodStart,
            DateTime periodEnd,
            DataTable employees,
            DataTable deptAgg,
            Func<long, (DataTable works, DataTable mins)> loadEmployeeData,
            string doneFio,
            string checkedFio)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("templatePath empty");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Excel template not found", templatePath);

            if (loadEmployeeData == null)
                throw new ArgumentNullException(nameof(loadEmployeeData));

            EnsureDirectory(Path.GetDirectoryName(outputPath));

            var templateFi = new FileInfo(templatePath);
            using (var package = new ExcelPackage(templateFi))
            {
                var wb = package.Workbook;
                if (wb.Worksheets.Count < 3)
                    throw new InvalidOperationException("Template must contain at least 3 worksheets.");

                // Лист 1
                var ws1 = wb.Worksheets[1];
                SetMergedCellText(ws1, "C4:I4", programName);
                SetMergedCellText(ws1, "C8:I8", complexName);
                SetMergedCellText(ws1, "C10:I10", departmentName);

                // Лист 2
                var ws2 = wb.Worksheets[2];
                SetMergedCellText(ws2, "C4:J4", programName);
                SetMergedCellText(ws2, "C8:J8", complexName);
                SetMergedCellText(ws2, "C10:J10", departmentName);
                ws2.Cells["C12"].Value = periodStart;
                ws2.Cells["D12"].Value = periodEnd;
                ws2.Cells["C12:D12"].Style.Numberformat.Format = "dd.MM.yyyy";

                FillSheet2_DepartmentDaily(ws2, periodStart, periodEnd, deptAgg);

                // Лист 3 — базовый шаблон карточки
                var baseWs = wb.Worksheets[3];

                var tmplWs = wb.Worksheets.Add("tmpl_" + Guid.NewGuid().ToString("N").Substring(0, 8), baseWs);

                // если нет сотрудников — сохраняем как есть
                if (employees == null || employees.Rows.Count == 0)
                {
                    FillEmployeeSheet(baseWs, programName, complexName, departmentName, periodStart, periodEnd, "", "", doneFio, checkedFio);
                    baseWs.Name = SafeWorksheetName("Нет сотрудников");
                    package.SaveAs(new FileInfo(outputPath));
                    return;
                }

                var fioCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                bool baseUsed = false; // чтобы первый реально работавший занял baseWs

                for (int i = 0; i < employees.Rows.Count; i++)
                {
                    string fio = (Convert.ToString(employees.Rows[i]["Fio"]) ?? "").Trim();
                    string pos = (Convert.ToString(employees.Rows[i]["PositionName"]) ?? "").Trim();
                    long ephId = Convert.ToInt64(employees.Rows[i]["EPHId"]);

                    // грузим фактические данные часов
                    var (works, mins) = loadEmployeeData(ephId);

                    // если работ не было — пропускаем (не работал)
                    if (works == null || works.Rows.Count == 0)
                        continue;

                    ExcelWorksheet ws;
                    if (!baseUsed)
                    {
                        ws = baseWs;
                        baseUsed = true;
                    }
                    else
                    {
                        // имя листа будет после, но Add требует имя сразу -> временное уникальное
                        ws = wb.Worksheets.Add("tmp_" + Guid.NewGuid().ToString("N").Substring(0, 8), baseWs);
                    }

                    FillEmployeeSheet(ws, programName, complexName, departmentName, periodStart, periodEnd, fio, pos, doneFio, checkedFio);
                    FillSheet3_EmployeeDaily(ws, periodStart, periodEnd, works, mins);

                    string sheetName = BuildNameWithDuplicateSuffix(fio, fioCounts);
                    sheetName = MakeUniqueSheetName(wb, sheetName);
                    ws.Name = sheetName;
                }

                wb.Worksheets.Delete(baseWs);
                wb.Worksheets.Delete(tmplWs);

                // Если вообще никто не работал — оставим baseWs, но назовём
                if (!baseUsed)
                {
                    FillEmployeeSheet(baseWs, programName, complexName, departmentName, periodStart, periodEnd, "", "", doneFio, checkedFio);
                    baseWs.Name = SafeWorksheetName("Нет часов");
                }

                // Снять группировку листов
                foreach (var w in wb.Worksheets)
                    w.View.TabSelected = false;

                wb.Worksheets[1].View.TabSelected = true;
                wb.View.ActiveTab = 0;

                package.SaveAs(new FileInfo(outputPath));
            }
        }
        private static string BuildNameWithDuplicateSuffix(string fio, Dictionary<string, int> fioCounts)
        {
            // База имени — просто ФИО, как ты хочешь.
            // Если пусто — используем "Сотрудник"
            string key = string.IsNullOrWhiteSpace(fio) ? "Сотрудник" : fio;

            // Наращиваем счётчик
            if (!fioCounts.TryGetValue(key, out int n))
                n = 0;
            n++;
            fioCounts[key] = n;

            // 1-й раз без суффикса, дальше (2), (3)...
            return n == 1 ? key : $"{key}({n})";
        }
        private static void FillEmployeeSheet(
            ExcelWorksheet ws,
            string programName,
            string complexName,
            string departmentName,
            DateTime periodStart,
            DateTime periodEnd,
            string employeeFio,
            string positionName,
            string doneFio,
            string checkedFio)
        {
            SetMergedCellText(ws, "C4:J4", programName);
            SetMergedCellText(ws, "C8:J8", complexName);
            SetMergedCellText(ws, "C10:J10", departmentName);
            SetMergedCellText(ws, "C12:J12", employeeFio);
            SetMergedCellText(ws, "C14:J14", positionName);

            // даты лучше как DateTime + формат, чтобы Excel понимал дату
            ws.Cells["C16"].Value = periodStart;
            ws.Cells["D16"].Value = periodEnd;
            ws.Cells["C16:D16"].Style.Numberformat.Format = "dd.MM.yyyy";

            // подписи
            ws.Cells["E25"].Value = doneFio ?? "";
            ws.Cells["I25"].Value = checkedFio ?? "";

            // фикс высоты строки 16 (как ты делал)
            ws.Row(16).CustomHeight = true;
            ws.Row(16).Height = 15.75;
            ws.Cells["C16:D16"].Style.WrapText = false;
            ws.Cells["C16:D16"].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            ws.Cells["C16:D16"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        }
        private static string SafeWorksheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Sheet";

            foreach (var c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(c, '_');

            // Excel ограничение 31 символ
            if (name.Length > 31)
                name = name.Substring(0, 31);

            return name.Trim();
        }
        private static double MinutesToExcelTime(int minutes)
        {
            if (minutes < 0) minutes = 0;
            return minutes / 1440.0; // 1440 минут в сутках
        }
        public static void FillSheet3_EmployeeDaily(
    ExcelWorksheet ws,
    DateTime periodStart,
    DateTime periodEnd,
    DataTable works,
    DataTable minutes)
        {
            int actualDays = (int)(periodEnd.Date - periodStart.Date).TotalDays + 1;
            if (actualDays != 7)
                throw new InvalidOperationException(
                    $"Период должен быть 7 дней. Сейчас: {actualDays} (с {periodStart:dd.MM.yyyy} по {periodEnd:dd.MM.yyyy}).");

            // D21..J21
            for (int i = 0; i < 7; i++)
                ws.Cells[21, 4 + i].Value = periodStart.Date.AddDays(i).ToString("dd.MM.yy");

            // map minutes
            var minsMap = new Dictionary<(long WorkId, DateTime Day), int>();
            foreach (DataRow r in minutes.Rows)
            {
                long workId = Convert.ToInt64(r["WorkId"]);
                DateTime day = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                int minSum = Convert.ToInt32(r["MinSum"]);
                minsMap[(workId, day)] = minSum;
            }

            int startRow = 22;
            int workCount = works?.Rows.Count ?? 0;
            if (workCount == 0) return;

            if (workCount > 1)
                ws.InsertRow(startRow + 1, workCount - 1, startRow);

            // ⚠ один раз задаём стиль для колонки B (если не сделано в шаблоне)
            var bRange = ws.Cells[startRow, 2, startRow + workCount - 1, 2];
            bRange.Style.WrapText = true;
            bRange.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            bRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;

            // ⚠ один раз задаём формат времени на D..J (если не сделано в шаблоне)
            var timeRange = ws.Cells[startRow, 4, startRow + workCount - 1, 10];
            timeRange.Style.Numberformat.Format = "[h]:mm";

            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;

                long workId = Convert.ToInt64(works.Rows[idx]["WorkId"]);
                string code = Convert.ToString(works.Rows[idx]["SpecialCode"]) ?? "";
                string name = Convert.ToString(works.Rows[idx]["Name"]) ?? "";

                ws.Cells[row, 1].Value = idx + 1; // A
                ws.Cells[row, 2].Value = name;    // B Name
                ws.Cells[row, 3].Value = code;    // C SpecialCode

                // автоподбор высоты по B после установки текста
                AutoFitRowHeightByText(ws, row, colFrom: 2, colTo: 2, fontName: "Arial", fontSize: 12f);

                for (int d = 0; d < 7; d++)
                {
                    DateTime day = periodStart.Date.AddDays(d);
                    int col = 4 + d;

                    minsMap.TryGetValue((workId, day), out int m);
                    ws.Cells[row, col].Value = MinutesToExcelTime(m); // число
                }
            }
        }
        public static void FillSheet2_DepartmentDaily(
ExcelWorksheet ws,
DateTime periodStart,
DateTime periodEnd,
DataTable agg // WorkId, SpecialCode, Name, WorkDate, MinSum
)
        {
            int actualDays = (int)(periodEnd.Date - periodStart.Date).TotalDays + 1;
            if (actualDays != 7)
                throw new InvalidOperationException(
                    $"Период должен быть 7 дней. Сейчас: {actualDays} (с {periodStart:dd.MM.yyyy} по {periodEnd:dd.MM.yyyy}).");

            // C16..I16
            int headerRow = 16;
            int startCol = 3; // C
            for (int i = 0; i < 7; i++)
                ws.Cells[headerRow, startCol + i].Value = periodStart.Date.AddDays(i).ToString("dd.MM.yy");

            // pivot
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
                        workInfo[workId] = (
                            (Convert.ToString(r["SpecialCode"]) ?? ""),
                            (Convert.ToString(r["Name"]) ?? "")
                        );
                        worksOrder.Add(workId);
                    }

                    DateTime wd = DateTime.Parse(Convert.ToString(r["WorkDate"])).Date;
                    int dayIdx = (int)(wd - periodStart.Date).TotalDays;
                    if (dayIdx < 0 || dayIdx > 6) continue;

                    int mins = Convert.ToInt32(r["MinSum"]);
                    minsMap[(workId, dayIdx)] = minsMap.TryGetValue((workId, dayIdx), out int cur) ? cur + mins : mins;
                }
            }

            int startRow = 17; // строка-шаблон с формулой в J
            int workCount = worksOrder.Count;

            if (workCount == 0)
                return;

            if (workCount > 1)
                ws.InsertRow(startRow + 1, workCount - 1, startRow);

            for (int idx = 0; idx < workCount; idx++)
            {
                int row = startRow + idx;
                long workId = worksOrder[idx];
                var info = workInfo[workId];

                ws.Cells[row, 1].Value = info.Code; // A
                ws.Cells[row, 2].Value = info.Name; // B

                for (int d = 0; d < 7; d++)
                {
                    minsMap.TryGetValue((workId, d), out int m);

                    var cell = ws.Cells[row, startCol + d]; // C..I
                    cell.Value = MinutesToExcelTime(m);
                    cell.Style.Numberformat.Format = "[h]:mm"; // на всякий случай
                }
                // J не трогаем (там формула)
            }
        }
        private static string MakeUniqueSheetName(ExcelWorkbook wb, string desired)
        {
            desired = SafeWorksheetName(desired);
            if (string.IsNullOrWhiteSpace(desired)) desired = "Sheet";

            string name = desired;
            int k = 2;

            while (wb.Worksheets[name] != null)
            {
                // держим суффикс внутри 31 символа
                string suffix = $"({k})";
                int maxBase = Math.Max(1, 31 - suffix.Length);
                string baseName = desired.Length > maxBase ? desired.Substring(0, maxBase) : desired;
                name = baseName + suffix;
                k++;
            }
            return name;
        }
    }
}
