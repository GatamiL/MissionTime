using System;

namespace MissionTime.Services
{
    public interface IDialogService
    {
        void ShowMessage(string title, string message);
        bool ShowConfirmation(string title, string message);
        void ShowEmployeesWindow();
        void ShowDepartmentsWindow();
        void ShowPositionsWindow();
        void ShowWorkTypesWindow();
        void ShowProgramsWindow();
        void ShowCardReportWindow(int currentYear, int currentMonth);
        void ShowDepartmentReportWindow(int currentYear, int currentMonth);
        void ShowDivisionReportWindow(int currentYear, int currentMonth);
        (bool success, long workId, string workName) ShowAddWorkWindow(string employeeName, string programName, long? currentWorkId = null);
        void ShowEmployeeSignaturesWindow();
        void ShowAboutWindow();
    }
}
