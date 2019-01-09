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

            // TODO: Adapt system to work with existing achievements YAML without loss of trophies or comments.
            if (badgesForDeprecatedModules.Any()) {
                Console.WriteLine();
                Console.WriteLine("Achievements considered deprecated");
                Console.WriteLine("---");
                foreach (var deprecatedAchievement in badgesForDeprecatedModules) {
                    Console.WriteLine($" * {deprecatedAchievement.Uid}");
                }

                // Output all in achievementsWithIssues to new achievements-badges-deprecated.yml file. (Then compare results to original achievements.yml since dealing with comments would be tough.)
                var achievementsYamlHeader = new[] {
                    "### YamlMime:Achievements",
                    "achievements:",
                };
                var deprecatedAchievementsYamlDestinationPath = Path.Combine(AchievementsFileInfo.Directory.FullName, "achievements-badges-deprecated.yml");
                Console.WriteLine(deprecatedAchievementsYamlDestinationPath);
                var tempNewIndexYamlFile = Path.GetTempFileName();
                var deprecatedAchievementsLines = achievementsYamlHeader
                    .Concat(badgesForDeprecatedModules
                        .SelectMany(a => {
                            return new[] {
                                $"- uid: {a.Uid}",
                                $"  type: badge",
                                $"  title: {a.Title}",
                                $"  summary: {a.Summary}",
                                $"  iconUrl: {a.IconUrl}"
                            };
                        })).ToList();
                File.WriteAllLines(tempNewIndexYamlFile, deprecatedAchievementsLines);
                File.Delete(deprecatedAchievementsYamlDestinationPath);
                File.Move(tempNewIndexYamlFile, deprecatedAchievementsYamlDestinationPath);

                Console.WriteLine($"Wrote new deprecated achievements: {deprecatedAchievementsYamlDestinationPath}.");
            }

            if (badgesWithIssues.Any()) {
                Console.WriteLine();
                Console.WriteLine("Achievements with issues");
                Console.WriteLine("---");
                foreach (var badgeWithIssue in badgesWithIssues) {
                    Console.WriteLine($" * {badgeWithIssue.Uid}");
                }
            }
        }
    }
    class AchievementAndModuleMatch {
        public Achievement Achievement { get; set; }
        public FileInfo ModuleIndexYamlFile { get; set; }
    }
    class Module {
        [YamlMember(Alias = "uid")]
        public string Uid { get; set; }
        [YamlMember(Alias = "achievement")]
        public string Achievement { get; set; }
        [YamlMember(Alias = "badge")]
        public AchievementBadge Badge { get; set; }
        public class AchievementBadge {
            [YamlMember(Alias = "uid")]
            public string Uid { get; set; }
            [YamlMember(Alias = "title")]
            public string Title { get; set; }
            [YamlMember(Alias = "summary")]
            public string Summary { get; set; }
            [YamlMember(Alias = "iconUrl")]
            public string IconUrl { get; set; }
        }

        // Stuff we don't care about, but YamlDotNet does.
        public class ModuleMetadata {
            public string title { get; set; }
            public string description { get; set; }
            [YamlMember(Alias = "ms.date")]
            public string msDate { get; set; }
            public string author { get; set; }
            [YamlMember(Alias = "ms.author")]
            public string msAuthor { get; set; }
            [YamlMember(Alias = "ms.topic")]
            public string msTopic { get; set; }
            [YamlMember(Alias = "ms.prod")]
            public string msProd { get; set; }
            [YamlMember(Alias = "ms.learn-contact")]
            public string msLearnContact { get; set; }
        }
        public ModuleMetadata metadata { get; set; }
        public string title { get; set; }
        public string summary { get; set; }
        public string @abstract { get; set; }
        public string cardDescription { get; set; }
        public string prerequisites { get; set; }
        public string iconUrl { get; set; }
        public List<string> levels { get; set; }
        public List<string> roles { get; set; }
        public List<string> products { get; set; }
        public List<string> units { get; set; }
    }
    class AchievementsList {
        [YamlMember(Alias = "achievements")]
        public List<Achievement> Achievements { get; set; }
    }
    class Achievement {
        // uid: learn.module-template.badge
        [YamlMember(Alias = "uid")]
        public string Uid { get; set; }
        // type: badge #|| trophy
        [YamlMember(Alias = "type")]
        public string Type { get; set; }
        // title: Module template
        [YamlMember(Alias = "title")]
        public string Title { get; set; }
        // summary: Module template badge.
        [YamlMember(Alias = "summary")]
        public string Summary { get; set; }
        // iconUrl: http://via.placeholder.com/120x120
        [YamlMember(Alias = "iconUrl")]
        public string IconUrl { get; set; }
    }
}
