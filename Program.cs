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
        static FileInfo AchievementsFileInfo { get; set; } = new FileInfo("C:\\dev\\learn-pr\\achievements.yml");
        static List<DirectoryInfo> ModulesDirectoryInfos { get; set; } = new List<DirectoryInfo> {
            new DirectoryInfo("C:\\dev\\learn-pr\\learn-pr\\azure\\")
        };
        static DirectoryInfo LearningPathsDirectoryInfo { get; set; } = new DirectoryInfo("C:\\dev\\learn-pr\\learn-pr\\paths\\");
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
            if (!AchievementsFileInfo.Exists) {
                Console.WriteLine($"Achievements YAML file not found: {AchievementsFileInfo.FullName}");
                isEverythingValid = false;
            }
            var modulesDirectoriesNotFound = ModulesDirectoryInfos.Where(di => !di.Exists);
            if (modulesDirectoriesNotFound.Any()) {
                foreach (var moduleDirectoryInfo in modulesDirectoriesNotFound) {
                    Console.WriteLine($"Module directory not found: {moduleDirectoryInfo}");
                }
                isEverythingValid = false;
            }
            if (!LearningPathsDirectoryInfo.Exists) {
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
            AchievementsList achievementList;
            List<Achievement> badgesToProcess;
            List<Achievement> trophiesToProcess;
            using (var achievementsStreamReader = new StreamReader(AchievementsFileInfo.FullName)) {
                var achievementsDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(new CamelCaseNamingConvention())
                    .Build();
                achievementList = achievementsDeserializer.Deserialize<AchievementsList>(achievementsStreamReader.ReadToEnd());
                var allAchievementsToProcess = achievementList.Achievements.ToList();
                badgesToProcess = allAchievementsToProcess.Where(a => a.Type == "badge").ToList();
                trophiesToProcess = allAchievementsToProcess.Where(a => a.Type == "trophy").ToList();
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

                    return new { FileInfo = moduleIndexYaml, Module = moduleInfo };
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

                    return new { FileInfo = learningPathIndexYaml, LearningPath = learningPathInfo };
                }
            }).ToList();
            Console.Write("\n");

            var trophyAchievementsMatchedWithLearningPaths = new List<AchievmentAndIndexYamlMatch>();
            var trophiesWithIssues = new List<Achievement>();
            var trophiesForDeprecatedLearningPaths = new List<Achievement>();

            foreach (var trophyAchievement in trophiesToProcess) {
                // Try to find a module folder with an index.yml containing a matching achievement UID.
                var foundLearningPaths = preparsedLearningPathIndexYamlFiles.Where(lp => {
                    return lp.LearningPath.Achievement == trophyAchievement.Uid;
                });

                var foundLearningPathsCount = foundLearningPaths.Count();
                if (foundLearningPathsCount == 0) {
                    // No learning path found. Not processing deprecated achievement.
                    trophiesForDeprecatedLearningPaths.Add(trophyAchievement);
                    continue;
                }

                if (foundLearningPathsCount > 1) {
                    // Multiple learning paths found for one achievement. Not processing achievement.
                    Console.WriteLine($"Found multiple modules for achievement: {trophyAchievement.Uid}");
                    foreach (var learningPathInfo in foundLearningPaths) {
                        Console.WriteLine($" - {learningPathInfo.FileInfo.FullName}");
                    }
                    trophiesWithIssues.Add(trophyAchievement);
                    continue;
                }

                // NOTE: Currently limited to 1 learning path by above short-circuit, but this _should_ still work for multiple.
                foreach (var foundIndexYaml in foundLearningPaths) {
                    trophyAchievementsMatchedWithLearningPaths.Add(new AchievmentAndIndexYamlMatch() {
                        Achievement = trophyAchievement,
                        IndexYamlFile = foundIndexYaml.FileInfo
                    });
                    Console.Write(".");
                }
            }

            var badgeAchievementsMatchedWithModules = new List<AchievmentAndIndexYamlMatch>();
            var badgesWithIssues = new List<Achievement>();
            var badgesForDeprecatedModules = new List<Achievement>();

            foreach (var badgeAchievement in badgesToProcess) {
                // Try to find a module folder with an index.yml containing a matching achievement UID.
                var foundModules = preparsedModuleIndexYamlFiles.Where(m => {
                    return m.Module.Achievement == badgeAchievement.Uid;
                });

                var foundModulesCount = foundModules.Count();

                if (foundModulesCount == 0) {
                    // No module found. Not processing deprecated achievement.
                    badgesForDeprecatedModules.Add(badgeAchievement);
                    continue;
                }

                if (foundModulesCount > 1) {
                    // Multiple modules found for one achievement. Not processing achievement.
                    Console.WriteLine($"Found multiple modules for achievement: {badgeAchievement.Uid}");
                    foreach (var moduleInfo in foundModules) {
                        Console.WriteLine($" - {moduleInfo.FileInfo.FullName}");
                    }
                    badgesWithIssues.Add(badgeAchievement);
                    continue;
                }

                // NOTE: Currently limited to 1 module by above short-circuit, but this _should_ still work for multiple.
                foreach (var foundModuleIndexYaml in foundModules) {
                    badgeAchievementsMatchedWithModules.Add(new AchievmentAndIndexYamlMatch() {
                        Achievement = badgeAchievement,
                        IndexYamlFile = foundModuleIndexYaml.FileInfo
                    });
                    Console.Write(".");
                }
            }
            Console.Write("\n");

            if (badgeAchievementsMatchedWithModules.Any()) {
                Console.WriteLine();
                Console.WriteLine("Migrating achievement to module YAML");
                Console.WriteLine("---");
                foreach (var match in badgeAchievementsMatchedWithModules) {
                    InjectAchievementIntoChildYaml(match.IndexYamlFile, match.Achievement);
                }
            }

            if (badgesWithIssues.Any()) {
                Console.WriteLine();
                Console.WriteLine("Badge achievements with issues");
                Console.WriteLine("---");
                foreach (var badgeWithIssue in badgesWithIssues) {
                    Console.WriteLine($" * {badgeWithIssue.Uid}");
                }
            }

            if (trophyAchievementsMatchedWithLearningPaths.Any()) {
                Console.WriteLine();
                Console.WriteLine("Migrating achievement to learning path YAML");
                Console.WriteLine("---");
                foreach (var match in trophyAchievementsMatchedWithLearningPaths) {
                    InjectAchievementIntoChildYaml(match.IndexYamlFile, match.Achievement);
                }
            }

            if (trophiesWithIssues.Any()) {
                Console.WriteLine();
                Console.WriteLine("Trophy achievements with issues");
                Console.WriteLine("---");
                foreach (var trophyWithIssue in trophiesWithIssues) {
                    Console.WriteLine($" * {trophyWithIssue.Uid}");
                }
            }

            UpdateRootAchievementsYamlFile(AchievementsFileInfo, badgeAchievementsMatchedWithModules.Concat(trophyAchievementsMatchedWithLearningPaths).ToList());
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
