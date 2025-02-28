using FuzzySharp;
using LibGit2Sharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

class Program
{
    public const bool VIEW_CODE_CHANGES = true;
    class Commit
    {
        public string Message;
        public string Hash;
        public string Repo;
        public Commit(string formatted, string repo)
        {
            var parts = formatted.Split(' ', 2);
            Hash = parts[0].Trim();
            Message = parts.Length > 1 ? parts[1].Trim() : "";
            Repo = repo;
        }

        public bool IsProbablyNotCode()
        {
            return Message.Contains("Changelog") || Regex.IsMatch(Message, @"\d+\.\d+\.\d+");
        }

        public string Changes()
        {
            return ExecuteGitCommand(Repo, $"show --pretty=short {Hash}");
        }

        public string CleanedMessage()
        {
            return Regex.Replace(Message.Trim(), @"\([^()]*#\d+[^()]*\)", "");
        }

        public override string ToString()
        {
            return $"[{Hash.Substring(0, 5)}..] {Message}";
        }
    }
    static void Main(string[] args)
    {
        
        string repoPath = @"C:\Users\stypl\development\crabscratch\rclcpp";

        ExecuteGitCommand(repoPath, "fetch --all");
        
        /*
        string branches = ExecuteGitCommand(repoPath, "branch -r");
        var branchList = branches.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        */
        // string firstNewCommit = ExecuteGitCommand(repoPath, "merge-base jazzy rolling").Trim();
        
         string masterBranch = "rolling";
         string[] branchesToCheckForSimilarCommits = [ "jazzy" ];

         ExecuteGitCommand(repoPath, $"checkout {masterBranch}");
         var masterCommitsList = ExecuteGitCommand(repoPath, $"log --pretty=\"format:%H %s\"")
             .Split('\n', StringSplitOptions.RemoveEmptyEntries)
             .Select(str => new Commit(str, repoPath));
         Console.WriteLine($"{masterBranch} has {masterCommitsList.Count()} commits.");

         foreach (string branchToCheck in branchesToCheckForSimilarCommits)
         {
             ExecuteGitCommand(repoPath, $"checkout {branchToCheck}");
             string firstNewCommit = ExecuteGitCommand(repoPath, $"merge-base {branchToCheck} {masterBranch}").Trim();

             var branchCommitsList = ExecuteGitCommand(repoPath, $"log --pretty=\"format:%H %s\" {firstNewCommit}..HEAD")
             .Split('\n', StringSplitOptions.RemoveEmptyEntries)
             .Select(str => new Commit(str, repoPath))
             .Where(c => !c.IsProbablyNotCode())
             .ToList();

             var nonMatchedCommitsList = branchCommitsList.ToList();

             Console.WriteLine($"{branchToCheck} has {branchCommitsList.Count()} commits.");


             int similarCount = 0;
             foreach (var branchCommit in branchCommitsList)
             {
                 foreach (var masterCommit in masterCommitsList)
                 {
                     if (Fuzz.Ratio(branchCommit.CleanedMessage(), masterCommit.CleanedMessage()) > 95)
                     {
                         similarCount++;
                         Console.WriteLine("----------------------------------------------------------------------------------------------------------------------------------------------------------------");
                         Console.WriteLine($"{masterBranch} \t{masterCommit}");
                         Console.WriteLine($"{branchToCheck} \t\t{branchCommit}");
                         PrintSideBySide(masterCommit.Changes(), branchCommit.Changes(), 80);
                         Console.WriteLine();
                         Console.ResetColor();

                         nonMatchedCommitsList.Remove(branchCommit);
                     }
                 }

             }
             Console.WriteLine($"Similar count: {similarCount}");

             Console.ForegroundColor = ConsoleColor.Magenta; 
             Console.WriteLine("----------------------------");
             Console.WriteLine($"{nonMatchedCommitsList.Count} Commits in {branchToCheck} that couldn't be paired up to a commit in {masterBranch}");

             Console.ResetColor();
             foreach (var commit in nonMatchedCommitsList)
             {
                 Commit bestGuess = null;
                 int matchScore = 0;
                 foreach (var masterCommit in masterCommitsList)
                 {
                     int ratio = Fuzz.Ratio(commit.CleanedMessage(), masterCommit.CleanedMessage());
                     if (ratio > matchScore)
                     {
                         matchScore = ratio;
                         bestGuess = masterCommit;
                     }
                 }

                 Console.WriteLine($"Best guess match:\t{masterBranch} \t{bestGuess} [{matchScore} Match Score]");
                 Console.WriteLine($"\t\t\t{branchToCheck} \t\t{commit}");
                 Console.WriteLine("----------------------------");
             }

         }

      //   int count = 0;

      //   string[] bugKeywords = "fix\r\nbug\r\nbugfix\r\nhotfix\r\npatch\r\nresolve\r\nresolved\r\ncorrect\r\ncorrected\r\nrepair\r\nrepaired\r\nadjust\r\nadjusted\r\naddress\r\naddressed\r\ndefect\r\ndebug\r\nerror\r\nissue\r\ncrash\r\nexception\r\nfail\r\nfailed\r\nfailure\r\nglitch\r\ninvalidate\r\ninvalid\r\ninconsistency\r\nincorrect\r\nwrong\r\nunexpected\r\nmistake\r\noops\r\ntypo\r\nflaw\r\nbreak\r\nbroken\r\nrevert\r\nregression\r\nmismatch\r\nnull\r\nundefined\r\noverflow\r\nunderflow\r\ncrashfix\r\nrollback\r\nworkaround\r\nsanitize\r\nvalidate\r\ncleanup\r\nrepair\r\nhandle\r\ncatch\r\nprevent\r\nguard\r\nretry\r\nfailsafe\r\nrestore\r\ncheck\r\ncorrectness\r\nfixup\r\nbypass\r\nstopgap\r\nlock\r\nunlock\r\nconsistency\r\nhalt\r\nrecover\r\npatchup\r\nhotpatch\r\nassert\r\nverify\r\nsafeguard\r\nmisbehave\r\ndependency\r\nconflict\r\ntimeout\r\nrace (as in race condition)\r\ndeadlock\r\noverflow\r\nnullcheck\r\nfallback\r\nboundary\r\nsanitize\r\nrollback\r\nerrorfix".Split("\r\n");
         // string[] bugKeywords = { "fix" };
         

    }

    static string ExecuteGitCommand(string repoPath, string command)
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

    static void PrintSideBySide(string str1, string str2, int columnWidth)
    {
        var lines1 = str1.Split('\n');
        var lines2 = str2.Split('\n');

        var wrappedLines1 = lines1.SelectMany(line => CharWrap(line, columnWidth).Split('\n')).ToArray();
        var wrappedLines2 = lines2.SelectMany(line => CharWrap(line, columnWidth).Split('\n')).ToArray();

        int maxLines = Math.Max(wrappedLines1.Length, wrappedLines2.Length);

        for (int i = 0; i < maxLines; i++)
        {
            string line1 = i < wrappedLines1.Length ? wrappedLines1[i].PadRight(columnWidth) : new string(' ', columnWidth);
            string line2 = i < wrappedLines2.Length ? wrappedLines2[i].PadRight(columnWidth) : new string(' ', columnWidth);

            if (line1[0] == '+') Console.ForegroundColor = ConsoleColor.Green;
            if (line1[0] == '-') Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(line1);
            Console.ResetColor();
            Console.Write(" | ");
            if (line2[0] == '+') Console.ForegroundColor = ConsoleColor.Green;
            if (line2[0] == '-') Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(line2);
            Console.ResetColor();
        }
    }

    static string CharWrap(string text, int maxWidth)
    {
        var result = "";
        for (int i = 0; i < text.Length; i += maxWidth)
        {
            result += text.Substring(i, Math.Min(maxWidth, text.Length - i)) + "\n";
        }
        return result.TrimEnd();
    }
}
