using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace learn_achievement_migrate
{
    class Program
    {
        static FileInfo AchievementsFileInfo { get; set; }
        static List<DirectoryInfo> ModulesDirectoryInfos { get; set; }
        static DirectoryInfo LearningPathsDirectoryInfo { get; set; }
        static List<string> ExtraArguments { get; set; }
        static bool ShouldShowHelp { get; set; } = false;

        static OptionSet Options { get; } = new OptionSet { 
            { "achievements=", "Path to a Microsoft Learn repo achievements.yml file.", p => AchievementsFileInfo = new FileInfo(p) },
            { "modules=", "Path(s) to modules referenced by achievements, separated by semicolon (;) for multiple paths.", paths => ModulesDirectoryInfos = paths.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => new DirectoryInfo(p)).ToList() },
            { "learningPaths=", "Path to learning paths directory reference by achievements.", p => LearningPathsDirectoryInfo = new DirectoryInfo(p) },
            { "h|help", "Show help message and exit.", h => ShouldShowHelp = h != null },
        };

        static void ProcessArguments(string[] args) {
            try {
                // parse the command line
                ExtraArguments = Options.Parse(args);
                AchievementsFileInfo = AchievementsFileInfo ?? new FileInfo("C:\\dev\\learn-pr\\achievements.yml");
                if (AchievementsFileInfo.Exists && ModulesDirectoryInfos == null && LearningPathsDirectoryInfo == null) {
                    // We were given an achievements.yml file but none of the rest, try to guess some common-sense options.
                    string pathsFolderName = "paths";
                    List<string> ignoreFolders = new List<string> {
                        "achievements",
                        "includes",
                        pathsFolderName,
                    };
                    var achievementParentDirectoryInfo = AchievementsFileInfo.Directory;
                    var achievementSiblingDirectoryInfos = achievementParentDirectoryInfo.GetDirectories();
                    ModulesDirectoryInfos = achievementSiblingDirectoryInfos.Where(d => !ignoreFolders.Contains(d.Name)).ToList();
                    LearningPathsDirectoryInfo = achievementSiblingDirectoryInfos.Where(d => d.Name == pathsFolderName).FirstOrDefault();
                }
            } catch (OptionException e) {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `--help' for more information.");
                return;
            }
        }

        private static void Debug_PrintOptions() {
            Console.WriteLine($"AchievementsPath: {AchievementsFileInfo.FullName}");
            Console.WriteLine($"ModulesPaths: {string.Join(";", ModulesDirectoryInfos.Select(di => di.FullName))}");
            Console.WriteLine($"{nameof(ShouldShowHelp)}: {ShouldShowHelp}");
        }

        private static void ShowHelp(OptionSet p) {
            Console.WriteLine("Usage: learn-achievement-migrate.exe --path='C:\\dev\\learn-pr\\'");
            Console.WriteLine("Migrate any achievement.yml badges to their respective module index.yml files.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        public static void InjectAchievementIntoChildYaml(FileInfo childIndexYamlFileInfo, Achievement achievement) {
            // Handle the YAML file as-is, so we don't destroy any comments and only modify the exact lines.
            var tempNewIndexYamlFile = Path.GetTempFileName();
            // NOTE: If more values from the source achievement are ever needed, serialize them here with more `.Append($"  field: {achievement.Field}")` lines.
            var linesToKeep = File.ReadLines(childIndexYamlFileInfo.FullName) 
                .Where(line => !line.StartsWith("achievement:"))
                .Append($"{achievement.Type}:")
                .Append($"  uid: {achievement.Uid}");
            File.WriteAllLines(tempNewIndexYamlFile, linesToKeep);
            File.Delete(childIndexYamlFileInfo.FullName);
            File.Move(tempNewIndexYamlFile, childIndexYamlFileInfo.FullName);

            Console.WriteLine($"Wrote new index.yml: {childIndexYamlFileInfo.FullName}.");
        }

        public static bool ArePathsValid() {
            var isEverythingValid = true;
            if (AchievementsFileInfo == null) {
                Console.WriteLine($"Achievements YAML parameter null");
                isEverythingValid = false;
            }
            else if (!AchievementsFileInfo.Exists) {
                Console.WriteLine($"Achievements YAML file not found: {AchievementsFileInfo.FullName}");
                isEverythingValid = false;
            }
            if (ModulesDirectoryInfos == null) {
                Console.WriteLine($"Module directories parameter null");
                isEverythingValid = false;
            }
            else {
                var modulesDirectoriesNotFound = ModulesDirectoryInfos.Where(di => !di.Exists);
                if (modulesDirectoriesNotFound.Any()) {
                    foreach (var moduleDirectoryInfo in modulesDirectoriesNotFound) {
                        Console.WriteLine($"Module directory not found: {moduleDirectoryInfo}");
                    }
                    isEverythingValid = false;
                }
            }
            if (LearningPathsDirectoryInfo == null) {
                Console.WriteLine($"Learning path directory parameter null");
                isEverythingValid = false;
            }
            else if (!LearningPathsDirectoryInfo.Exists) {
                Console.WriteLine($"Learning paths directory not found: {LearningPathsDirectoryInfo.FullName}.");
                isEverythingValid = false;
            }
            return isEverythingValid;
        }

        static void Main(string[] args) {
            ProcessArguments(args);

            // Debug_PrintOptions();

            if (ShouldShowHelp) {
                ShowHelp(Options);
                Console.WriteLine("Exiting without any changes.");
                Console.Read();
                return;
            }

            var arePathsValid = ArePathsValid();
            if (!arePathsValid) {
                Console.WriteLine("Exiting without any changes.");
                Console.Read();
                return;
            }

            var achievementsFileName = AchievementsFileInfo.Name;
            List<Achievement> achievementsToProcess;
            using (var achievementsStreamReader = new StreamReader(AchievementsFileInfo.FullName)) {
                var achievementsDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(new CamelCaseNamingConvention())
                    .Build();
                AchievementsList achievementList = achievementsDeserializer.Deserialize<AchievementsList>(achievementsStreamReader.ReadToEnd());
                achievementsToProcess = achievementList.Achievements.ToList();
            }

            var moduleDirectoryInfos = ModulesDirectoryInfos.SelectMany(di => di.GetDirectories()).ToList();
            var preparsedModuleIndexYamlFiles = moduleDirectoryInfos.Where(m => {
                var moduleIndexYaml = m.GetFiles("index.yml").FirstOrDefault();
                return moduleIndexYaml != null;
            }).Select(m => {
                var moduleIndexYaml = m.GetFiles("index.yml").Single();
                using (var moduleYamlStreamReader = new StreamReader(moduleIndexYaml.FullName)) {
                    var moduleDeserializer = new DeserializerBuilder()
                        .WithNamingConvention(new CamelCaseNamingConvention())
                        .Build();
                    var moduleInfo = moduleDeserializer.Deserialize<Module>(moduleYamlStreamReader.ReadToEnd());

                    return new ParsedLearnElementAndSourceIndexYaml {
                        IndexYamlFileInfo = moduleIndexYaml,
                        LearnElement = moduleInfo
                    };
                }
            }).ToList();
            Console.Write("\n");

            var learningPathDirectoryInfos = LearningPathsDirectoryInfo.GetDirectories();
            var preparsedLearningPathIndexYamlFiles = learningPathDirectoryInfos.Where(m => {
                var learningPathIndexYaml = m.GetFiles("index.yml").FirstOrDefault();
                return learningPathIndexYaml != null;
            }).Select(m => {
                var learningPathIndexYaml = m.GetFiles("index.yml").Single();
                using (var learningPathYamlStreamReader = new StreamReader(learningPathIndexYaml.FullName)) {
                    var learningPathDeserializer = new DeserializerBuilder()
                        .WithNamingConvention(new CamelCaseNamingConvention())
                        .Build();
                    var learningPathInfo = learningPathDeserializer.Deserialize<LearningPath>(learningPathYamlStreamReader.ReadToEnd());

                    return new ParsedLearnElementAndSourceIndexYaml {
                        LearnElement = learningPathInfo,
                        IndexYamlFileInfo = learningPathIndexYaml
                    };
                }
            }).ToList();
            Console.Write("\n");

            var achievementsMatchedWithIndexYaml = new List<AchievmentAndIndexYamlMatch>();
            var achievementsWithIssues = new List<Achievement>();
            var achievementsForDeprecatedElements = new List<Achievement>();

            foreach (var achievement in achievementsToProcess) {
                var matchedIndexYamls = new List<ParsedLearnElementAndSourceIndexYaml>();
                List<ParsedLearnElementAndSourceIndexYaml> collectionToSearch = new List<ParsedLearnElementAndSourceIndexYaml>();
                if (achievement.Type == "trophy") {
                    // Search learning paths for trophies.
                    collectionToSearch = preparsedLearningPathIndexYamlFiles;
                }
                else if (achievement.Type == "badge") {
                    // Search modules for badges.
                    collectionToSearch = preparsedModuleIndexYamlFiles;
                }
                matchedIndexYamls = collectionToSearch.Where(m => {
                    return m.LearnElement.Achievement == achievement.Uid;
                }).ToList();

                var matchedIndexYamlsCount = matchedIndexYamls.Count();
                if (matchedIndexYamlsCount == 0) {
                    // No learning path found. Not processing deprecated achievement.
                    achievementsForDeprecatedElements.Add(achievement);
                    continue;
                }

                if (matchedIndexYamlsCount > 1) {
                    // Multiple learning paths found for one achievement. Not processing achievement.
                    Console.WriteLine($"Found multiple items for achievement: {achievement.Uid}");
                    foreach (var learningPathInfo in matchedIndexYamls) {
                        Console.WriteLine($" - {learningPathInfo.IndexYamlFileInfo.FullName}");
                    }
                    achievementsWithIssues.Add(achievement);
                    continue;
                }

                // NOTE: Currently limited to 1 matched location by above short-circuit, but this _should_ still work for multiple.
                foreach (var foundIndexYaml in matchedIndexYamls) {
                    achievementsMatchedWithIndexYaml.Add(new AchievmentAndIndexYamlMatch() {
                        Achievement = achievement,
                        IndexYamlFileInfo = foundIndexYaml.IndexYamlFileInfo
                    });
                    Console.Write(".");
                }
            }

            if (achievementsMatchedWithIndexYaml.Any()) {
                Console.WriteLine();
                Console.WriteLine("Migrating achievement to child YAMLs");
                Console.WriteLine("---");
                foreach (var match in achievementsMatchedWithIndexYaml) {
                    InjectAchievementIntoChildYaml(match.IndexYamlFileInfo, match.Achievement);
                }
            }

            if (achievementsWithIssues.Any()) {
                Console.WriteLine();
                Console.WriteLine("Achievements with issues");
                Console.WriteLine("---");
                foreach (var achievementWithIssue in achievementsWithIssues) {
                    Console.WriteLine($" * {achievementWithIssue.Uid}");
                }
            }

            UpdateRootAchievementsYamlFile(AchievementsFileInfo, achievementsMatchedWithIndexYaml);
        }

        public static void UpdateRootAchievementsYamlFile(FileInfo achievementFileInfo, List<AchievmentAndIndexYamlMatch> achievementsToRemove) {
            Console.WriteLine();
            Console.WriteLine("Writing proposed achievements YAML");
            Console.WriteLine("---");

            var existingAchievementLines = File.ReadAllLines(achievementFileInfo.FullName);
            var remainingAchievementLines = new List<string>();
            foreach (var achievementMigrated in achievementsToRemove) {
                Console.WriteLine($"{achievementMigrated.Achievement.Uid}");
            }
            for (int lineIndex = 0; lineIndex < existingAchievementLines.Length; lineIndex += 1) {
                var line = existingAchievementLines[lineIndex];
                if (!line.StartsWith("- uid:")) {
                    // Not a UID line, add by default.
                    remainingAchievementLines.Add(line);
                    // Console.WriteLine($"+ {line}");
                    continue;
                }

                var lineUid = line.Split(":", StringSplitOptions.None).Select(s => s.Trim()).Skip(1).FirstOrDefault();
                if (string.IsNullOrEmpty(lineUid)) {
                    // Not able to parse out UID properly; skipping for now.
                    remainingAchievementLines.Add(line);
                    // Console.WriteLine($"+ {line}");
                    continue;
                }

                var isMigratedUid = achievementsToRemove.Any(b => b.Achievement.Uid == lineUid);
                if (!isMigratedUid) {
                    // Not a migrated badge UID, add by default (rest of badge should hit above condition for adding).
                    remainingAchievementLines.Add(line);
                    // Console.WriteLine($"+ {line}");
                    continue;
                }
                
                // Else: UID line of a badge that was migrated; skip it and sibling data.
                // NOTE: Assumes a badge is exactly 5 lines (uid, type, title, summary, and iconUrl).
                lineIndex += 4;
                Console.WriteLine($"- {line}");
            }

            // TODO: Overwrite the source achievement file.
            var proposedAchievementsYamlDestinationPath = Path.Combine(AchievementsFileInfo.Directory.FullName, "achievements-proposed.yml");
            Console.WriteLine(proposedAchievementsYamlDestinationPath);
            var tempProposedAchievementsYamlFile = Path.GetTempFileName();
            File.WriteAllLines(tempProposedAchievementsYamlFile, remainingAchievementLines);
            File.Delete(proposedAchievementsYamlDestinationPath);
            File.Move(tempProposedAchievementsYamlFile, proposedAchievementsYamlDestinationPath);
        }
    }
}
