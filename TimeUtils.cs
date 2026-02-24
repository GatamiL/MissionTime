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
    }
}
