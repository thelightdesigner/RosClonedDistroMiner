using FuzzySharp;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace RosClonedDistroMiner
{
    class Program
    {
        public const bool VIEW_CODE_CHANGES = true;
        public const string REPO_PATH = @"C:\Users\stypl\development\crabscratch";
        public const string OUTPUT_PATH = @"C:\Users\stypl\development\crabscratch\output.json";
        public const int COLUMN_WIDTH = 85;


        private static readonly List<MainlineBackportCommitPair> PatchesDataSet = new();
        static void Main(string[] args)
        {

            string[] repos = ["rclpy", "rclcpp", "rosidl_python", "rcpputils", "rpyutils"];
            string[] compareAgainstBranches = ["jazzy", "humble"];
            string mainlineBranchName = "rolling";

            foreach (string repoName in repos)
            {
                Repository rclpp = new Repository(Path.Combine(REPO_PATH, repoName), repoName);
                rclpp.ExecuteGitCommand("fetch --all");
                foreach (string branch in compareAgainstBranches)
                {
                    AnalyzeBranchPair(mainlineBranchName, branch, rclpp);
                }
            }
            

/*
            Console.WriteLine("How many backported commits are indistinguishable from the mainline commits?");
            int identical = 0;
            foreach(var dataPoint in dataSet)
            {
                if (dataPoint.Mainline.ModifiedFiles.Count == dataPoint.Backported.ModifiedFiles.Count) identical++;
            }
            Console.WriteLine(identical);
*/


            //   int count = 0;

            //   string[] bugKeywords = "fix\r\nbug\r\nbugfix\r\nhotfix\r\npatch\r\nresolve\r\nresolved\r\ncorrect\r\ncorrected\r\nrepair\r\nrepaired\r\nadjust\r\nadjusted\r\naddress\r\naddressed\r\ndefect\r\ndebug\r\nerror\r\nissue\r\ncrash\r\nexception\r\nfail\r\nfailed\r\nfailure\r\nglitch\r\ninvalidate\r\ninvalid\r\ninconsistency\r\nincorrect\r\nwrong\r\nunexpected\r\nmistake\r\noops\r\ntypo\r\nflaw\r\nbreak\r\nbroken\r\nrevert\r\nregression\r\nmismatch\r\nnull\r\nundefined\r\noverflow\r\nunderflow\r\ncrashfix\r\nrollback\r\nworkaround\r\nsanitize\r\nvalidate\r\ncleanup\r\nrepair\r\nhandle\r\ncatch\r\nprevent\r\nguard\r\nretry\r\nfailsafe\r\nrestore\r\ncheck\r\ncorrectness\r\nfixup\r\nbypass\r\nstopgap\r\nlock\r\nunlock\r\nconsistency\r\nhalt\r\nrecover\r\npatchup\r\nhotpatch\r\nassert\r\nverify\r\nsafeguard\r\nmisbehave\r\ndependency\r\nconflict\r\ntimeout\r\nrace (as in race condition)\r\ndeadlock\r\noverflow\r\nnullcheck\r\nfallback\r\nboundary\r\nsanitize\r\nrollback\r\nerrorfix".Split("\r\n");
            // string[] bugKeywords = { "fix" };


        }



        static void AnalyzeBranchPair(string mainlineBranchName, string forkedBranchName, Repository repo)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Analyzing {repo.Name}");

            repo.ExecuteGitCommand($"checkout {mainlineBranchName}");
            repo.ExecuteGitCommand($"checkout {forkedBranchName}");

            string firstNewCommitInForked = repo.ExecuteGitCommand($"merge-base {forkedBranchName} {mainlineBranchName}").Trim();

            //Get a list of the mainline's commits up to when this forked from mainline
            repo.ExecuteGitCommand($"checkout {mainlineBranchName}");
            IEnumerable<CommitInfo> mainlineCommits = repo.ExecuteGitCommand($"log --pretty=\"format:%H %s\" {firstNewCommitInForked}..HEAD")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(str => new CommitInfo(str, repo));
            Console.WriteLine($"{mainlineBranchName} has {mainlineCommits.Count()} commits.");

            //Get a list of the forked distro's commits up to when this forked from mainline
            repo.ExecuteGitCommand($"checkout {forkedBranchName}");

            IEnumerable<CommitInfo> forkedCommits = repo.ExecuteGitCommand($"log --pretty=\"format:%H %s\" {firstNewCommitInForked}..HEAD")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(str => new CommitInfo(str, repo))
            .Where(c => !c.IsProbablyNotCode());



            //Create lists of the mainline and fork to remove from.
            List<CommitInfo> unmatchedCommitsForked = forkedCommits.ToArray().ToList();
            List<CommitInfo> unmatchedCommitsMainline = mainlineCommits.ToArray().ToList();

            //List of backport data points

            Console.WriteLine($"{forkedBranchName} has {forkedCommits.Count()} commits.");
            Console.ResetColor();

            int similarCount = 0;
            foreach (var forkedCommit in forkedCommits)
            {
                foreach (var mainlineCommit in mainlineCommits)
                {
                    // if (similarCount > 1) continue;
                    if (Fuzz.Ratio(forkedCommit.CleanedMessage(), mainlineCommit.CleanedMessage()) > 95)
                    {
                        similarCount++;
                        Console.WriteLine("----------------------------------------------------------------------------------------------------------------------------------------------------------------");
                        Console.WriteLine($"{mainlineBranchName} \t{mainlineCommit}");
                        Console.WriteLine($"{forkedBranchName} \t\t{forkedCommit}");

                        PatchesDataSet.Add(new MainlineBackportCommitPair(mainlineCommit.Hash, forkedCommit.Hash, repo));

                        unmatchedCommitsForked.Remove(forkedCommit);
                        unmatchedCommitsMainline.Remove(mainlineCommit);
                    }
                }
            }

            //Visualize data
            //   foreach(MainlineBackportCommitPair dataPoint in dataSet) Console.WriteLine(dataPoint.ToString());

            Console.WriteLine("Writing data file...");
            File.WriteAllText(OUTPUT_PATH, JsonSerializer.Serialize(PatchesDataSet, new JsonSerializerOptions()
            {
                WriteIndented = true
            }));

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Similar count: {similarCount}");
            Console.WriteLine("----------------------------");
            Console.WriteLine($"{unmatchedCommitsForked.Count} Commits in {forkedBranchName} that couldn't be paired up to a commit in {mainlineBranchName}");

            Console.ResetColor();
            foreach (var commit in unmatchedCommitsForked)
            {
                CommitInfo bestGuess = null;
                int matchScore = 0;
                foreach (var masterCommit in mainlineCommits)
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
        }
       
    }
}
