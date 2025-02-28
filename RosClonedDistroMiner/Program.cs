using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;

class Program
{
    static void Main(string[] args)
    {
        string repoPath = @"C:\Users\stypl\development\crabscratch\rclcpp";

        ExecuteGitCommand(repoPath, "fetch --all");

        string branches = ExecuteGitCommand(repoPath, "branch -r");
        var branchList = branches.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        ExecuteGitCommand(repoPath, "checkout rolling");

        string commits = ExecuteGitCommand(repoPath, "log --pretty=\"format:%H %s\"");

        var commitList = commits.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int count = 0;

        string[] bugKeywords = "fix\r\nbug\r\nbugfix\r\nhotfix\r\npatch\r\nresolve\r\nresolved\r\ncorrect\r\ncorrected\r\nrepair\r\nrepaired\r\nadjust\r\nadjusted\r\naddress\r\naddressed\r\ndefect\r\ndebug\r\nerror\r\nissue\r\ncrash\r\nexception\r\nfail\r\nfailed\r\nfailure\r\nglitch\r\ninvalidate\r\ninvalid\r\ninconsistency\r\nincorrect\r\nwrong\r\nunexpected\r\nmistake\r\noops\r\ntypo\r\nflaw\r\nbreak\r\nbroken\r\nrevert\r\nregression\r\nmismatch\r\nnull\r\nundefined\r\noverflow\r\nunderflow\r\ncrashfix\r\nrollback\r\nworkaround\r\nsanitize\r\nvalidate\r\ncleanup\r\nrepair\r\nhandle\r\ncatch\r\nprevent\r\nguard\r\nretry\r\nfailsafe\r\nrestore\r\ncheck\r\ncorrectness\r\nfixup\r\nbypass\r\nstopgap\r\nlock\r\nunlock\r\nconsistency\r\nhalt\r\nrecover\r\npatchup\r\nhotpatch\r\nassert\r\nverify\r\nsafeguard\r\nmisbehave\r\ndependency\r\nconflict\r\ntimeout\r\nrace (as in race condition)\r\ndeadlock\r\noverflow\r\nnullcheck\r\nfallback\r\nboundary\r\nsanitize\r\nrollback\r\nerrorfix".Split("\r\n");
        // string[] bugKeywords = { "fix" };

        Dictionary<string, int> fileCounts = new();

        foreach (var commit in commitList)
        {
            var parts = commit.Split(' ', 2);
            string commitHash = parts[0].Trim();
            string commitMessage = parts.Length > 1 ? parts[1].Trim() : "";

            

            if (bugKeywords.Any(keyword => commitMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                count++;
                Console.WriteLine($"At: {commitHash} - {commitMessage}");
                string[] filesChanged = ExecuteGitCommand(repoPath, $"diff-tree --no-commit-id --name-only -r {commitHash}").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (string file in filesChanged)
                {
                    if (fileCounts.ContainsKey(file)) fileCounts[file]++;
                    else fileCounts[file] = 0;
                }
            }
        }



        Console.WriteLine("------------Summary-------------");
        var fileCountsSorted = fileCounts.ToList().OrderBy(pair => pair.Value);

        foreach (var entry in fileCountsSorted)
        {
            Console.WriteLine($"{entry.Key} : {entry.Value}");
        }
    }

    static string ExecuteGitCommand(string repoPath, string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoPath
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
