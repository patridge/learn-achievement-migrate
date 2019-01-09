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
        static FileInfo AchievementsFileInfo { get; set; } = new FileInfo("C:\\dev\\learn-pr\\");
        static DirectoryInfo ModulesDirectoryInfo { get; set; } = new DirectoryInfo("C:\\dev\\learn-pr\\learn-pr\\azure\\");
        static List<string> ExtraArguments { get; set; }
        static bool ShouldShowHelp { get; set; } = false;

        static OptionSet Options { get; } = new OptionSet { 
            { "achievements=", "Path to a Microsoft Learn repo achievements.yml file.", p => AchievementsFileInfo = new FileInfo(p) },
            { "modules=", "Path to modules referenced by achievements.", p => ModulesDirectoryInfo = new DirectoryInfo(p) },
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
            Console.WriteLine($"ModulesPath: {ModulesDirectoryInfo.FullName}");
            Console.WriteLine($"{nameof(ShouldShowHelp)}: {ShouldShowHelp}");
        }

        private static void ShowHelp(OptionSet p) {
            Console.WriteLine("Usage: learn-achievement-migrate.exe --path='C:\\dev\\learn-pr\\'");
            Console.WriteLine("Migrate any achievement.yml badges to their respective module index.yml files.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        public static void InjectAchievementIntoModuleYaml(FileInfo moduleIndexYamlFileInfo, Achievement achievement) {
            // Handle the YAML file as-is, so we don't destroy any comments and only modify the exact lines.
            var tempNewIndexYamlFile = Path.GetTempFileName();
            var linesToKeep = File.ReadLines(moduleIndexYamlFileInfo.FullName) 
                .Where(line => !line.StartsWith("achievement:"))
                .Append("badge:")
                .Append($"  uid: {achievement.Uid}")
                .Append($"  title: {achievement.Title}")
                .Append($"  summary: {achievement.Summary}")
                .Append($"  iconUrl: {achievement.IconUrl}");
            File.WriteAllLines(tempNewIndexYamlFile, linesToKeep);
            File.Delete(moduleIndexYamlFileInfo.FullName);
            File.Move(tempNewIndexYamlFile, moduleIndexYamlFileInfo.FullName);

            Console.WriteLine($"Wrote new index.yml: {moduleIndexYamlFileInfo.FullName}.");
        }

        public static bool ArePathsValid() {
            if (!AchievementsFileInfo.Exists) {
                Console.WriteLine($"Achievements YAML file not found: {AchievementsFileInfo.FullName}");
                return false;
            }
            if (!ModulesDirectoryInfo.Exists) {
                Console.WriteLine($"Modules directory not found: {ModulesDirectoryInfo.FullName}.");
                return false;
            }
            return true;
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
            using (var achievementsStreamReader = new StreamReader(AchievementsFileInfo.FullName)) {
                var achievementsDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(new CamelCaseNamingConvention())
                    .Build();
                achievementList = achievementsDeserializer.Deserialize<AchievementsList>(achievementsStreamReader.ReadToEnd());
                badgesToProcess = achievementList.Achievements.Where(a => a.Type == "badge").ToList();
            }

            var moduleDirectoryInfos = ModulesDirectoryInfo.GetDirectories();
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

            var achievementsMatchedWithModules = new List<AchievementAndModuleMatch>();
            var badgesWithIssues = new List<Achievement>();
            var badgesForDeprecatedModules = new List<Achievement>();

            // To limit a run to a few edits for a test run, uncomment the following `Take` line.
            // badgesToProcess = badgesToProcess.Take(5);

            foreach (var achievement in badgesToProcess) {
                // Try to find a module folder with an index.yml containing a matching achievement UID.
                var foundModules = preparsedModuleIndexYamlFiles.Where(m => {
                    return m.Module.Achievement == achievement.Uid;
                });

                var foundModulesCount = foundModules.Count();

                if (foundModulesCount == 0) {
                    // No module found. Not processing deprecated achievement.
                    badgesForDeprecatedModules.Add(achievement);
                    continue;
                }

                if (foundModulesCount > 1) {
                    // Multiple modules found for one achievement. Not processing achievement.
                    foreach (var moduleInfo in foundModules) {
                        Console.WriteLine($" - {moduleInfo.FileInfo.FullName}");
                    }
                    badgesWithIssues.Add(achievement);
                    continue;
                }

                // NOTE: Currently limited to 1 module by above short-circuit, but this _should_ still work.
                foreach (var foundModuleIndexYaml in foundModules) {
                    achievementsMatchedWithModules.Add(new AchievementAndModuleMatch() {
                        Achievement = achievement,
                        ModuleIndexYamlFile = foundModuleIndexYaml.FileInfo
                    });
                    Console.Write(".");
                }
            }
            Console.Write("\n");

            if (achievementsMatchedWithModules.Any()) {
                Console.WriteLine();
                Console.WriteLine("Migrating achievement to module YAML");
                Console.WriteLine("---");
                foreach (var achievementAndModuleMatch in achievementsMatchedWithModules) {
                    InjectAchievementIntoModuleYaml(achievementAndModuleMatch.ModuleIndexYamlFile, achievementAndModuleMatch.Achievement);
                }
            }

            if (badgesWithIssues.Any()) {
                Console.WriteLine();
                Console.WriteLine("Achievements with issues");
                Console.WriteLine("---");
                foreach (var badgeWithIssue in badgesWithIssues) {
                    Console.WriteLine($" * {badgeWithIssue.Uid}");
                }
            }

            UpdateAchievementsYamlFile(AchievementsFileInfo, achievementsMatchedWithModules);
        }

        public static void UpdateAchievementsYamlFile(FileInfo achievementFileInfo, List<AchievementAndModuleMatch> badgesToRemove) {
            Console.WriteLine();
            Console.WriteLine("Writing proposed achievements YAML");
            Console.WriteLine("---");

            var existingAchievementLines = File.ReadAllLines(achievementFileInfo.FullName);
            var remainingAchievementLines = new List<string>();
            foreach (var badgeMigrated in badgesToRemove) {
                Console.WriteLine($"{badgeMigrated.Achievement.Uid}");
            }
            for (int lineIndex = 0; lineIndex < existingAchievementLines.Length; lineIndex += 1) {
                var line = existingAchievementLines[lineIndex];
                if (!line.StartsWith("- uid:")) {
                    // Not a UID line, add by default.
                    remainingAchievementLines.Add(line);
                    Console.WriteLine($"+ {line}");
                    continue;
                }

                var lineUid = line.Split(":", StringSplitOptions.None).Select(s => s.Trim()).Skip(1).FirstOrDefault();
                if (string.IsNullOrEmpty(lineUid)) {
                    // Not able to parse out UID properly; skipping for now.
                    remainingAchievementLines.Add(line);
                    Console.WriteLine($"+ {line}");
                    continue;
                }

                Console.WriteLine($"\"{lineUid}\"");
                var isMigratedUid = badgesToRemove.Any(b => b.Achievement.Uid == lineUid);
                if (!isMigratedUid) {
                    // Not a migrated badge UID, add by default (rest of badge should hit above condition for adding).
                    remainingAchievementLines.Add(line);
                    Console.WriteLine($"+ {line}");
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
