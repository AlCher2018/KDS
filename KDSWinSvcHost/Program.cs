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
                new ServiceKDS()
            };
            ServiceBase.Run(ServicesToRun);
#else
            // Debug code: this allows the process to run as a non-service.
            // It will kick off the service start point, but never kill it.
            // Shut down the debugger to exit
            ServiceKDS service = new ServiceKDS();
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
#endif 

        }  // method Main
    }  // class Program
}
