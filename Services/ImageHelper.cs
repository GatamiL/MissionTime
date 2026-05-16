using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MissionTime.Services
{
    public static class ImageHelper
    {
        /// <summary>
        /// Обрабатывает загруженное изображение: 
        /// 1. Удаляет белый фон (делает прозрачным)
        /// 2. Автоматически обрезает пустые края (autocrop)
        /// </summary>
        public static byte[] ProcessSignatureImage(byte[] rawBytes)
        {
            try
            {
                if (rawBytes == null || rawBytes.Length == 0) return rawBytes;

                // 1. Загрузка через декодер
                using (var ms = new MemoryStream(rawBytes))
                {
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var source = decoder.Frames[0];

                    // Конвертируем в BGRA32, чтобы гарантированно работать с альфа-каналом и 4-байтовыми пикселями
                    var bgra32 = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                    
                    int width = bgra32.PixelWidth;
                    int height = bgra32.PixelHeight;
                    int stride = width * 4;
                    
                    byte[] pixels = new byte[height * stride];
                    bgra32.CopyPixels(pixels, stride, 0);

                    // --- 2. АНАЛИЗ И УДАЛЕНИЕ ФОНА ---
                    int minX = width, maxX = 0, minY = height, maxY = 0;
                    bool hasContent = false;
                    
                    // Порог "белого" цвета (если R, G, B > 220 — считаем фоном)
                    const int WhiteThreshold = 210; 

                    for (int y = 0; y < height; y++)
                    {
                        int rowOffset = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int idx = rowOffset + (x * 4);
                            byte b = pixels[idx];
                            byte g = pixels[idx + 1];
                            byte r = pixels[idx + 2];
                            byte a = pixels[idx + 3];

                            // Проверяем, светлый ли пиксель
                            // Либо он УЖЕ прозрачный в исходнике
                            bool isBackground = (r > WhiteThreshold && g > WhiteThreshold && b > WhiteThreshold) || a < 10;

                            if (isBackground)
                            {
                                pixels[idx + 3] = 0; // Превращаем в полную прозрачность
                            }
                            else
                            {
                                // Это контент подписи
                                hasContent = true;
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }

                    // Если вся картинка белая, ничего не трогаем или возвращаем оригинал
                    if (!hasContent) return rawBytes;

                    // --- 3. КАДРИРОВАНИЕ (CROP) ---
                    // Добавим небольшой отступ 10px вокруг подписи для красоты
                    int padding = 10;
                    minX = Math.Max(0, minX - padding);
                    minY = Math.Max(0, minY - padding);
                    maxX = Math.Min(width - 1, maxX + padding);
                    maxY = Math.Min(height - 1, maxY + padding);

                    int targetW = maxX - minX + 1;
                    int targetH = maxY - minY + 1;

                    if (targetW <= 0 || targetH <= 0) return rawBytes;

                    // Создаем массив для новой обрезанной картинки
                    int outStride = targetW * 4;
                    byte[] outPixels = new byte[targetH * outStride];

                    for (int y = 0; y < targetH; y++)
                    {
                        int sourceY = minY + y;
                        int sourceOffset = (sourceY * stride) + (minX * 4);
                        int destOffset = y * outStride;
                        Buffer.BlockCopy(pixels, sourceOffset, outPixels, destOffset, outStride);
                    }

                    // --- 4. СОЗДАНИЕ ФИНАЛЬНОГО БИТМАПА И СОХРАНЕНИЕ ---
                    var finalBitmap = BitmapSource.Create(
                        targetW, targetH, 
                        bgra32.DpiX, bgra32.DpiY, 
                        PixelFormats.Bgra32, 
                        null, outPixels, outStride);

                    using (var outStream = new MemoryStream())
                    {
                        var encoder = new PngBitmapEncoder(); // Всегда пишем в PNG для прозрачности
                        encoder.Frames.Add(BitmapFrame.Create(finalBitmap));
                        encoder.Save(outStream);
                        return outStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но возвращаем оригинал, чтобы не ломать загрузку полностью
                LogService.Log("Ошибка авто-обрезки изображения: " + ex.Message);
                return rawBytes;
            }
        }
    }
}
