namespace MissionTime.Models
{
    public class EmployeeBrief
    {
        public long Id { get; set; }
        public string Fio { get; set; }
        public string PositionName { get; set; }    // Актуальная должность
        public string DepartmentName { get; set; }  // Актуальный отдел
        public bool IsFired { get; set; }           // Флаг (Action == 3)
    }
}
