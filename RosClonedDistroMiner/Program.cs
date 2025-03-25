using FuzzySharp;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RosClonedDistroMiner
{
    class Program
    {
        public const bool VIEW_CODE_CHANGES = true;
        public const string REPO_PATH = @"C:\Users\stypl\development\crabscratch\rclcpp";
        static void Main(string[] args)
        {
            Repository rclpp = new Repository(REPO_PATH);

            rclpp.ExecuteGitCommand("fetch --all");

            string mainlineBranchName = "rolling";
            string forkedBranchName = "jazzy";
            string firstNewCommitInForked = rclpp.ExecuteGitCommand($"merge-base {forkedBranchName} {mainlineBranchName}").Trim();

            rclpp.ExecuteGitCommand($"checkout {mainlineBranchName}");
            var mainlineCommitsList = rclpp.ExecuteGitCommand($"log --pretty=\"format:%H %s\" {firstNewCommitInForked}..HEAD")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(str => new Commit(str, rclpp));
            Console.WriteLine($"{mainlineBranchName} has {mainlineCommitsList.Count()} commits.");


            rclpp.ExecuteGitCommand($"checkout {forkedBranchName}");

            var forkedCommitsList = rclpp.ExecuteGitCommand($"log --pretty=\"format:%H %s\" {firstNewCommitInForked}..HEAD")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(str => new Commit(str, rclpp))
            .Where(c => !c.IsProbablyNotCode())
            .ToList();

            var unmatchedCommitsForked = forkedCommitsList.ToList();
            var unmatchedCommitsMainline = mainlineCommitsList.ToList();

            Console.WriteLine($"{forkedBranchName} has {forkedCommitsList.Count()} commits.");

            int similarCount = 0;
            foreach (var forkedCommit in forkedCommitsList)
            {
                foreach (var mainlineCommit in mainlineCommitsList)
                {
                     if (similarCount > 0) return;
                    if (Fuzz.Ratio(forkedCommit.CleanedMessage(), mainlineCommit.CleanedMessage()) > 95)
                    {
                        similarCount++;
                        Console.WriteLine("----------------------------------------------------------------------------------------------------------------------------------------------------------------");
                        Console.WriteLine($"{mainlineBranchName} \t{mainlineCommit}");
                        Console.WriteLine($"{forkedBranchName} \t\t{forkedCommit}");

                        /*    foreach (string fileName in forkedCommit.Changes().Keys)
                            {
                                PrintSideBySide(fileName, "nothing yet...", 80);
                            }

                            //   PrintSideBySide(masterCommit.Changes(), branchCommit.Changes(), 80);
                            Console.WriteLine();
                            Console.ResetColor();*/

                        JsonSerializerOptions jso = new();
                        jso.WriteIndented = true;
                        Console.WriteLine(JsonSerializer.Serialize(forkedCommit.Changes(), jso));
                     
                        unmatchedCommitsForked.Remove(forkedCommit);
                        unmatchedCommitsMainline.Remove(mainlineCommit);
                    }
                }

            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Similar count: {similarCount}");
            Console.WriteLine("----------------------------");
            Console.WriteLine($"{unmatchedCommitsForked.Count} Commits in {forkedBranchName} that couldn't be paired up to a commit in {mainlineBranchName}");

            Console.ResetColor();
            foreach (var commit in unmatchedCommitsForked)
            {
                Commit bestGuess = null;
                int matchScore = 0;
                foreach (var masterCommit in mainlineCommitsList)
                {
                    int ratio = Fuzz.Ratio(commit.CleanedMessage(), masterCommit.CleanedMessage());
                    if (ratio > matchScore)
                    {
                        matchScore = ratio;
                        bestGuess = masterCommit;
                    }
                }

                Console.WriteLine($"Best guess match:\t{mainlineBranchName} \t{bestGuess} [{matchScore} Match Score]");
                Console.WriteLine($"\t\t\t{forkedBranchName} \t\t{commit}");
                Console.WriteLine("----------------------------");
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("----------------------------");
            Console.WriteLine($"{unmatchedCommitsMainline.Count} Commits in {mainlineBranchName} that couldn't be paired up to a commit in {forkedBranchName}");

            Console.ResetColor();



            //   int count = 0;

            //   string[] bugKeywords = "fix\r\nbug\r\nbugfix\r\nhotfix\r\npatch\r\nresolve\r\nresolved\r\ncorrect\r\ncorrected\r\nrepair\r\nrepaired\r\nadjust\r\nadjusted\r\naddress\r\naddressed\r\ndefect\r\ndebug\r\nerror\r\nissue\r\ncrash\r\nexception\r\nfail\r\nfailed\r\nfailure\r\nglitch\r\ninvalidate\r\ninvalid\r\ninconsistency\r\nincorrect\r\nwrong\r\nunexpected\r\nmistake\r\noops\r\ntypo\r\nflaw\r\nbreak\r\nbroken\r\nrevert\r\nregression\r\nmismatch\r\nnull\r\nundefined\r\noverflow\r\nunderflow\r\ncrashfix\r\nrollback\r\nworkaround\r\nsanitize\r\nvalidate\r\ncleanup\r\nrepair\r\nhandle\r\ncatch\r\nprevent\r\nguard\r\nretry\r\nfailsafe\r\nrestore\r\ncheck\r\ncorrectness\r\nfixup\r\nbypass\r\nstopgap\r\nlock\r\nunlock\r\nconsistency\r\nhalt\r\nrecover\r\npatchup\r\nhotpatch\r\nassert\r\nverify\r\nsafeguard\r\nmisbehave\r\ndependency\r\nconflict\r\ntimeout\r\nrace (as in race condition)\r\ndeadlock\r\noverflow\r\nnullcheck\r\nfallback\r\nboundary\r\nsanitize\r\nrollback\r\nerrorfix".Split("\r\n");
            // string[] bugKeywords = { "fix" };


        }
        public static void PrintSideBySide(string str1, string str2, int columnWidth)
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
}
