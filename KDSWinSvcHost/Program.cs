using System.ServiceProcess;


namespace KDSWinSvcHost
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
#if (!DEBUG)

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service1()
            };
            ServiceBase.Run(ServicesToRun);
#else
            // Debug code: this allows the process to run as a non-service.
            // It will kick off the service start point, but never kill it.
            // Shut down the debugger to exit
            Service1 service = new Service1();
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
#endif 

        }  // method Main
    }  // class Program
}
