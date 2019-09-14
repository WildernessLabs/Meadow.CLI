using System;

namespace MeadowCLI.DeviceManagement
{
    /// <summary>
    /// TODO: put deployment stuff here
    ///
    /// TODO: consider using a singleton.
    /// </summary>
    public static class DeploymentManager
    {
        static DeploymentManager()
        {
        }

        public static Tuple<bool, string> DeployApp(string appOutputPath, MeadowDevice device)
        {
            bool success = false;
            string message = "";

            // 0) TODO: any init checks?

            // 1) TODO: enumerate all the files in the output path
            
            // 2) TODO: get a list of the files already present on the device

            // 3) TODO: compare file names and sizes/checksums or whatever, and
            // and figure out what we need to deploy

            // 4) TODO: Deploy app files

            // 5) TODO: restart device

            return new Tuple<bool, string>(success, message);
        }
    }
}
