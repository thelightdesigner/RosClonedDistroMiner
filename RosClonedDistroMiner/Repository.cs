using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RosClonedDistroMiner
{
    public class Repository
    {
        public string Path;
        public string Name;
        public Repository(string path, string name)
        {
            Path = path;
            Name = name;
        }
        public string ExecuteGitCommand(string command)
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error) && process.ExitCode != 0)
            {
                Console.WriteLine("Git error: " + error);
            }

            return output;
        }
    }
}
