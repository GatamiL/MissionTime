using MissionTime.Views;
using System.Windows;

namespace MissionTime.Services
{
    public class WpfDialogService : IDialogService
    {
        private readonly DbService _db;
        private Window OwnerWindow => Application.Current.MainWindow;

        public WpfDialogService(DbService db)
        {
            _db = db;
        }

        public void ShowMessage(string title, string message)
        {
            MissionMessageBox.Show(OwnerWindow, title, message);
        }

        public bool ShowConfirmation(string title, string message)
        {
            return MissionMessageBox.Show(OwnerWindow, title, message, isQuestion: true) == true;
        }

        public void ShowEmployeesWindow()
        {
            new Employees(_db) { Owner = OwnerWindow }.ShowDialog();
        }

        public void ShowDepartmentsWindow()
        {
            new Departments(_db) { Owner = OwnerWindow }.ShowDialog();
        }

        public void ShowPositionsWindow()
        {
            new EmployeePositions(_db) { Owner = OwnerWindow }.ShowDialog();
        }

        public void ShowWorkTypesWindow()
        {
            new ListOfWork(_db) { Owner = OwnerWindow }.ShowDialog();
        }

        public void ShowProgramsWindow()
        {
            new ListOfProgram(_db) { Owner = OwnerWindow }.ShowDialog();
        }

        public void ShowCardReportWindow(int currentYear, int currentMonth)
        {
            new CardReportWindow(_db, currentYear, currentMonth) { Owner = OwnerWindow }.ShowDialog();
        }

        public void ShowDepartmentReportWindow(int currentYear, int currentMonth)
        {
            new DepartmentReportWindow(_db, currentYear, currentMonth) { Owner = OwnerWindow }.ShowDialog();
        }

        public void ShowDivisionReportWindow(int currentYear, int currentMonth)
        {
            new DivisionReportWindow(_db, currentYear, currentMonth) { Owner = OwnerWindow }.ShowDialog();
        }

        public (bool success, long workId, string workName) ShowAddWorkWindow(string employeeName, string programName, long? currentWorkId = null)
        {
            var dialog = new AddWorkWindow(_db, employeeName, programName, currentWorkId) { Owner = OwnerWindow };
            if (dialog.ShowDialog() == true)
            {
                return (true, dialog.SelectedWorkId, dialog.SelectedWorkName);
            }
            return (false, 0, null);
        }

        public void ShowEmployeeSignaturesWindow()
        {
            new EmployeeSignatures(_db) { Owner = OwnerWindow }.ShowDialog();
        }
        public void ShowAboutWindow()
        {
            new AboutWindow { Owner = OwnerWindow }.ShowDialog();
        }
    }
}
