using MissionTime.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionTime
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var dbForm = new DBSelect())
            {
                dbForm.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

                if (dbForm.ShowDialog() == DialogResult.OK)
                {
                    Application.Run(new MainForm(dbForm.SelectedDbPath));
                }
            }
        }
    }
}
