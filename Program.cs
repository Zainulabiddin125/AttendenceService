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
        //static void Main()
        //{
        //    ServiceBase[] ServicesToRun;
        //    ServicesToRun = new ServiceBase[]
        //    {
        //        new AttendanceService()
        //    };
        //    ServiceBase.Run(ServicesToRun);
        //}

        //static void Main(string[] args)
        //{
        //    var service = new AttendanceService();
        //    Console.WriteLine("Service is running");
        //    service.TestStart(args); // Start the service logic
        //    Console.WriteLine("Press Enter to stop...");
        //    Console.ReadLine();
        //    service.TestStop(); // Stop the service logic
        //}

        //For Development Debuging
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                // Run as a console application
                AttendanceService service = new AttendanceService();
                Console.WriteLine("Starting service in console mode...");
                service.TestStart(args);

                Console.WriteLine("Press ENTER to stop...");
                Console.ReadLine();
                service.TestStop();
            }
            else
            {
                // Run as a Windows service
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new AttendanceService() };
                ServiceBase.Run(ServicesToRun);
            }
        }

    }
}
