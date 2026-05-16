using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MissionTime
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Services.AppSettings.Load(); // Грузим тему и шрифт!


            // 1. Ошибки главного потока UI
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 2. Ошибки фоновых потоков
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 3. Ошибки в асинхронных тасках
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Services.LogService.Log("КРИТИЧЕСКАЯ ОШИБКА (Dispatcher):", e.Exception);
            MessageBox.Show("Произошла непредвиденная ошибка! Детали записаны в файл MissionTime.log.\n\nСообщение: " + e.Exception.Message, 
                "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Попытаемся не ронять приложение, если это возможно
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            Services.LogService.Log("НЕОБРАБОТАННОЕ ИСКЛЮЧЕНИЕ ДОМЕНА:", ex);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            Services.LogService.Log("ОШИБКА В АСИНХРОННОЙ ЗАДАЧЕ:", e.Exception);
            e.SetObserved(); // Помечаем как обработанную, чтобы не валить процесс
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }

        // --- ГЛОБАЛЬНЫЕ ОБРАБОТЧИКИ ДЛЯ КАСТОМНОГО ЗАГОЛОВКА ОКНА ---
        public void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(sender as DependencyObject);
            if (win != null) win.WindowState = WindowState.Minimized;
        }

        public void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(sender as DependencyObject);
            if (win != null)
            {
                // Если окно запрещено разворачивать (например, ToolWindow) - ничего не делаем
                if (win.ResizeMode == ResizeMode.NoResize || win.ResizeMode == ResizeMode.CanMinimize) return;
                win.WindowState = (win.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
            }
        }

        public void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(sender as DependencyObject);
            win?.Close();
        }
    }
}
