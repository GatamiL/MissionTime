using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionTime
{
    public class WorkPeriod
    {
        public int WeekNum { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public override string ToString()
        {
            return $"Неделя {WeekNum}: {Start:dd.MM.yy} - {End:dd.MM.yy}";
        }
    }

    public static class PeriodCalculator
    {
        /// <summary>
        /// Генерирует активные недели программы с правильной сквозной нумерацией.
        /// Нумеруются ТОЛЬКО те недели, в которых были списаны часы.
        /// Возвращает список только тех активных недель, которые зацепили запрошенный месяц.
        /// </summary>
        public static List<WorkPeriod> GetActiveWeeks(DateTime programStart, int targetYear, int targetMonth, HashSet<DateTime> activeDates, DateTime? programEnd = null)
        {
            var results = new List<WorkPeriod>();

            DateTime monthStart = new DateTime(targetYear, targetMonth, 1);
            int daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
            DateTime monthEnd = new DateTime(targetYear, targetMonth, daysInMonth);

            if (programStart.Date > monthEnd)
                return results;

            DateTime currentStart = programStart.Date;

            // СЧЕТЧИК ТОЛЬКО ДЛЯ РАБОЧИХ НЕДЕЛЬ
            int activeWeekNum = 1;

            while (currentStart <= monthEnd)
            {
                int daysToSunday = ((int)DayOfWeek.Sunday - (int)currentStart.DayOfWeek + 7) % 7;
                DateTime currentEnd = currentStart.AddDays(daysToSunday);

                if (programEnd.HasValue && currentEnd > programEnd.Value.Date)
                    currentEnd = programEnd.Value.Date;

                // Проверяем, есть ли хотя бы один рабочий день в ЭТОЙ конкретной неделе
                bool hasHours = activeDates != null && activeDates.Any(d => d >= currentStart && d <= currentEnd);

                if (hasHours)
                {
                    // Если неделя зацепила наш отчетный месяц — добавляем ее в финальный результат
                    if (currentEnd >= monthStart)
                    {
                        results.Add(new WorkPeriod
                        {
                            WeekNum = activeWeekNum, // Записываем правильный "активный" номер
                            Start = currentStart,
                            End = currentEnd
                        });
                    }

                    // Увеличиваем счетчик ТОЛЬКО потому, что в неделе кто-то работал!
                    activeWeekNum++;
                }

                if (programEnd.HasValue && currentEnd >= programEnd.Value.Date)
                    break;

                currentStart = currentEnd.AddDays(1);
            }

            return results;
        }
    }
}