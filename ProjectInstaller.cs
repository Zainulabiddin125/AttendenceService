using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;
using System.ServiceProcess;

namespace AttendenceService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
            ServiceProcessInstaller processInstaller = new ServiceProcessInstaller();
            ServiceInstaller serviceInstaller = new ServiceInstaller();

            // Run service as Local System
            processInstaller.Account = ServiceAccount.LocalSystem;

            // Configure Service
            serviceInstaller.ServiceName = "AttendanceService";
            serviceInstaller.DisplayName = "Attendance Service";
            serviceInstaller.Description = "A service to fetch attendance data.";
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            // Add installers
            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
