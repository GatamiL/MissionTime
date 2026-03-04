using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MissionTime
{
    public static class TimeUtils
    {
        public static string MinutesToHHmm(int minutes)
        {
            if (minutes <= 0) return "";
            int h = minutes / 60;
            int m = minutes % 60;
            return string.Format("{0}:{1:00}", h, m);
        }

        public static string GetDowRu2(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Monday: return "Пн";
                case DayOfWeek.Tuesday: return "Вт";
                case DayOfWeek.Wednesday: return "Ср";
                case DayOfWeek.Thursday: return "Чт";
                case DayOfWeek.Friday: return "Пт";
                case DayOfWeek.Saturday: return "Сб";
                case DayOfWeek.Sunday: return "Вс";
                default: return "";
            }
        }
        public static string GetMonthRu(int month)
        {
            string[] m =
            {
                "", "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
                "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
            };
            return (month >= 1 && month <= 12) ? m[month] : month.ToString();
        }
        public static List<(int WeekNum, DateTime Start, DateTime End)> GetProgramWeeksForMonth(DateTime programStart, int year, int month, DateTime? programEnd = null)
        {
            var results = new List<(int WeekNum, DateTime Start, DateTime End)>();

            DateTime monthStart = new DateTime(year, month, 1);
            int daysInMonth = DateTime.DaysInMonth(year, month);
            DateTime monthEnd = new DateTime(year, month, daysInMonth);

            // Если программа начинается ПОЗЖЕ этого месяца — периодов тут нет
            if (programStart.Date > monthEnd)
                return results;

            DateTime currentStart = programStart.Date;
            int weekNum = 1;

            // Идем, пока начало недели не выйдет за пределы нашего отчетного месяца
            while (currentStart <= monthEnd)
            {
                // Вычисляем конец текущей недели (строго Воскресенье)
                int daysToSunday = ((int)DayOfWeek.Sunday - (int)currentStart.DayOfWeek + 7) % 7;
                DateTime currentEnd = currentStart.AddDays(daysToSunday);

                // Если у программы есть жесткая дата завершения и мы на нее напоролись — обрезаем хвост
                if (programEnd.HasValue && currentEnd > programEnd.Value.Date)
                {
                    currentEnd = programEnd.Value.Date;
                }

                // Нам нужны только те недели, которые хоть одним днем залезли в наш отчетный месяц
                if (currentEnd >= monthStart)
                {
                    // Добавляем неделю КАК ЕСТЬ (без обрезки по 1-му или 31-му числу!)
                    results.Add((weekNum, currentStart, currentEnd));
                }

                // Если программа закончилась на этой неделе — дальше считать нет смысла
                if (programEnd.HasValue && currentEnd >= programEnd.Value.Date)
                    break;

                // Переходим к следующему понедельнику
                currentStart = currentEnd.AddDays(1);
                weekNum++;
            }

            return results;
        }
    }
}
