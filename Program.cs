using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace AttendenceService
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>       

        static void Main(string[] args)
        {
            //for Development
            //if (Environment.UserInteractive)
            //{
            //    //    // Run as a console application
            //    AttendanceService service = new AttendanceService();
            //    Console.WriteLine("Starting service in console mode...");
            //    service.TestStart(args);

            //    Console.WriteLine("Press ENTER to stop...");
            //    Console.ReadLine();
            //    service.TestStop();
            //}
            //else
            //{
                // Run in Windows Service mode
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                 new AttendanceService()
                };
                ServiceBase.Run(ServicesToRun);
            //}
        }
    }
}
