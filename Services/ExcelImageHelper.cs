using System;
using System.IO;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using OfficeOpenXml;

namespace MissionTime.Services
{
    public static class ExcelImageHelper
    {
        // ====================================================================
        // КОЭФФИЦИЕНТЫ ТОНКОЙ ПОДГОНКИ (КАЛИБРОВКА)
        // В окне WPF используется Viewbox, который "растягивает" картинку визуально.
        // Эти константы приводят пиксели экрана к реальным размерам в Excel.
        
        // Если подпись в Excel выходит слишком ГРОМОЗДКОЙ — уменьшай этот параметр (например, 0.75)
        private const double ScaleCorrection = 0.75;
        
        // Если в окне смещаешь сильно, а в Excel сдвигается ЕЛЕ-ЕЛЕ — увеличивай этот параметр (например, 1.3)
        private const double OffsetCorrection = 1;
        // ====================================================================

        /// <summary>
        /// Вставляет подпись сотрудника в указанную ячейку Excel-листа с учетом сохраненного масштаба и смещения.
        /// </summary>
        public static void InsertSignature(ExcelWorksheet ws, long? employeeId, string cellAddress, DbService db)
        {
            if (ws == null || !employeeId.HasValue || string.IsNullOrWhiteSpace(cellAddress) || db == null)
                return;

            try
            {
                var dt = db.Query("SELECT ImageData, OffsetX, OffsetY, Scale FROM EmployeeSign WHERE EmployeeId = @id", 
                    new SQLiteParameter("@id", employeeId.Value));
                
                if (dt == null || dt.Rows.Count == 0)
                    return;

                byte[] data = dt.Rows[0]["ImageData"] as byte[];
                if (data == null || data.Length == 0)
                    return;

                double offX = Convert.ToDouble(dt.Rows[0]["OffsetX"] != DBNull.Value ? dt.Rows[0]["OffsetX"] : 0);
                double offY = Convert.ToDouble(dt.Rows[0]["OffsetY"] != DBNull.Value ? dt.Rows[0]["OffsetY"] : 0);
                double scale = Convert.ToDouble(dt.Rows[0]["Scale"] != DBNull.Value ? dt.Rows[0]["Scale"] : 1.0);
                if (scale <= 0) scale = 1.0;

                using (var ms = new MemoryStream(data))
                using (var img = Image.FromStream(ms))
                {
                    // 1. Синхронизируем размеры с WPF (с учетом корректирующего коэффициента).
                    // В превью WPF база Image.Width="150". Применяем ScaleCorrection, чтобы вписать её гармонично.
                    double baseDisplayWidth = 150.0 * ScaleCorrection;
                    double aspectRatio = (double)img.Height / (double)img.Width;
                    double baseDisplayHeight = baseDisplayWidth * aspectRatio;

                    // Финальный размер картинки в пикселях в Excel (с учетом масштаба ползунка)
                    int renderWidth = (int)(baseDisplayWidth * scale);
                    int renderHeight = (int)(baseDisplayHeight * scale);

                    var cell = ws.Cells[cellAddress];
                    int targetRow = cell.Start.Row;
                    int targetCol = cell.Start.Column;

                    // 2. Вычисляем положение полочки (нижней границы целевой ячейки)
                    double rowHeightPts = ws.Row(targetRow).Height;
                    if (rowHeightPts <= 0) rowHeightPts = ws.DefaultRowHeight;
                    if (rowHeightPts <= 0) rowHeightPts = 15.0; 

                    double rowHeightPixels = rowHeightPts * (96.0 / 72.0);

                    // 3. МАТЕМАТИКА ПРИВЯЗКИ К НИЗУ (К ПОЛОЧКЕ)
                    // Применяем OffsetCorrection к вводу пользователя, чтобы компенсировать масштабирование Viewbox
                    double adjustedOffX = offX * OffsetCorrection;
                    double adjustedOffY = offY * OffsetCorrection;

                    double relativeY = rowHeightPixels - renderHeight + adjustedOffY;
                    double relativeX = adjustedOffX;

                    // 4. АЛГОРИТМ ПЕРЕКАТЫВАНИЯ ЧЕРЕЗ ГРАНИЦЫ СТРОК (ОБРАТНЫЙ ХОД)
                    int anchorRow0 = targetRow - 1;
                    while (relativeY < 0 && anchorRow0 > 0)
                    {
                        anchorRow0--;
                        double hPts = ws.Row(anchorRow0 + 1).Height;
                        if (hPts <= 0) hPts = ws.DefaultRowHeight;
                        if (hPts <= 0) hPts = 15.0;
                        
                        relativeY += (hPts * (96.0 / 72.0));
                    }

                    int anchorCol0 = targetCol - 1;
                    while (relativeX < 0 && anchorCol0 > 0)
                    {
                        anchorCol0--;
                        double colW = ws.Column(anchorCol0 + 1).Width;
                        if (colW <= 0) colW = 8.43;
                        relativeX += (colW * 7.0);
                    }

                    int finalRowOffset = (int)Math.Max(0, relativeY);
                    int finalColOffset = (int)Math.Max(0, relativeX);

                    // Генерируем имя и внедряем
                    string name = "Sign_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    var picture = ws.Drawings.AddPicture(name, img);
                    
                    picture.SetPosition(anchorRow0, finalRowOffset, anchorCol0, finalColOffset);
                    picture.SetSize(renderWidth, renderHeight);
                    
                    picture.EditAs = OfficeOpenXml.Drawing.eEditAs.TwoCell;
                }
            }
            catch (Exception ex)
            {
                // Логируем и подавляем ошибку, чтобы генерация всего отчета не свалилась из-за одной картинки
                LogService.Log("ОШИБКА ВСТАВКИ ПОДПИСИ В EXCEL:", ex);
            }
        }

        /// <summary>
        /// Проверяет наличие сохраненной подписи в БД для конкретного сотрудника.
        /// </summary>
        public static bool HasSignature(DbService db, long? employeeId)
        {
            if (db == null || !employeeId.HasValue) return false;
            try
            {
                var dt = db.Query("SELECT 1 FROM EmployeeSign WHERE EmployeeId = @id LIMIT 1", new SQLiteParameter("@id", employeeId.Value));
                return dt != null && dt.Rows.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
