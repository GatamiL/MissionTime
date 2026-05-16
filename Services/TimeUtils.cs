namespace MissionTime.Services
{
    public static class TimeUtils
    {
        // Из минут в строку 8:15
        public static string MinutesToHHmm(int minutes)
        {
            if (minutes <= 0) return "";
            int h = minutes / 60;
            int m = minutes % 60;
            return string.Format("{0}:{1:00}", h, m);
        }

        // Из строки 8:15 обратно в минуты (495)
        public static int HHmmToMinutes(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            text = text.Replace('.', ':').Replace(',', ':'); // На случай, если введут 8.30

            var parts = text.Split(':');
            if (parts.Length == 1)
            {
                // Если ввели просто "8", считаем что это 8 часов
                if (int.TryParse(parts[0], out int hOnly)) return hOnly * 60;
            }
            else if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
                {
                    return (h * 60) + m;
                }
            }
            return 0;
        }
    }
}