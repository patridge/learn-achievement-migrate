using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace learn_achievement_migrate
{
    class Program
    {
        static OptionSet Options { get; } = new OptionSet { 
            { "achievements=", "Path to a Microsoft Learn repo achievements.yml file.", p => AchievementsPath = p },
            { "modules=", "Path to modules referenced by achievements.", p => ModulesPath = p },
            { "h|help", "Show help message and exit.", h => ShouldShowHelp = h != null },
        };
        static string AchievementsPath { get; set; } = "C:\\dev\\learn-pr\\";
        static string ModulesPath { get; set; } = "C:\\dev\\learn-pr\\learn-pr\\azure\\";
        static List<string> ExtraArguments { get; set; }
        static bool ShouldShowHelp { get; set; } = false;
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
            Console.WriteLine($"{nameof(AchievementsPath)}: {AchievementsPath}");
            Console.WriteLine($"{nameof(ModulesPath)}: {ModulesPath}");
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
            // TODO: Find achievement YAML line.
            // TODO: Put in serialized achievement (after figuring out what that is).
            // TODO: Read: https://stackoverflow.com/a/45958279/48700
            // Handle the YAML file as-is, so we don't destroy any comments and only modify the exact lines.
            Console.WriteLine("TODO: actually migrate the YAML");
        }

        static void Main(string[] args) {
            ProcessArguments(args);

            // Debug_PrintOptions();

            if (ShouldShowHelp) {
                ShowHelp(Options);
                Console.Read();
                return;
            }

            var achievementsFileInfo = new FileInfo(AchievementsPath);
            if (!achievementsFileInfo.Exists) {
                Console.WriteLine($"Achievements YAML file not found: {AchievementsPath}");
                Console.Read();
                return;
            }
            var modulesDirectoryInfo = new DirectoryInfo(ModulesPath);
            if (!modulesDirectoryInfo.Exists) {
                Console.WriteLine($"Modules directory not found: {AchievementsPath}.");
                Console.Read();
                return;
            }

            var achievementsFileName = achievementsFileInfo.Name;
            var achievementsStreamReader = new StreamReader(AchievementsPath);
            var achievementsDeserializer = new DeserializerBuilder()
                .WithNamingConvention(new CamelCaseNamingConvention())
                .Build();
            var achievementList = achievementsDeserializer.Deserialize<AchievementsList>(achievementsStreamReader.ReadToEnd());
            var badgesToProcess = achievementList.Achievements.Where(a => a.Type == "badge");

            var moduleDirectoryInfos = modulesDirectoryInfo.GetDirectories();
            // TODO: Pre-parse all module index.yml files into something for easier searching.

            var achievementsMatchedWithModules = new List<AchievementAndModuleMatch>();
            var achievementsWithIssues = new List<Achievement>();
            var achievementsForDeprecatedModules = new List<Achievement>();

            // TODO: Progress bar for achievement badges.

            foreach (var achievement in badgesToProcess) {
                // Try to find a module folder with an index.yml containing a matching achievement UID.
                var foundModuleIndexYamlFiles = moduleDirectoryInfos.Where(m => {
                    var moduleIndexYaml = m.GetFiles("index.yml").FirstOrDefault();
                    if (moduleIndexYaml == null) {
                        return false;
                    }

                    var moduleYamlStreamReader = new StreamReader(moduleIndexYaml.FullName);
                    var moduleDeserializer = new DeserializerBuilder()
                        .WithNamingConvention(new CamelCaseNamingConvention())
                        .Build();
                    var moduleInfo = moduleDeserializer.Deserialize<Module>(moduleYamlStreamReader.ReadToEnd());

                    return moduleInfo.Achievement == achievement.Uid;
                }).Select(m => {
                    var moduleIndexYaml = m.GetFiles("index.yml").Single();
                    return moduleIndexYaml;
                });

                var foundModulesCount = foundModuleIndexYamlFiles.Count();

                if (foundModulesCount == 0) {
                    // No module found. Not processing deprecated achievement.
                    achievementsForDeprecatedModules.Add(achievement);
                    continue;
                }

                if (foundModulesCount > 1) {
                    // Multiple modules found for one achievement. Not processing achievement.
                    foreach (var moduleIndexYaml in foundModuleIndexYamlFiles) {
                        Console.WriteLine($" - {moduleIndexYaml.FullName}");
                    }
                    achievementsWithIssues.Add(achievement);
                    continue;
                }

                // NOTE: Currently limited to 1 module by above short-circuit, but this _should_ still work.
                foreach (var foundModuleIndexYaml in foundModuleIndexYamlFiles) {
                    achievementsMatchedWithModules.Add(new AchievementAndModuleMatch() {
                        Achievement = achievement,
                        ModuleIndexYamlFile = foundModuleIndexYaml
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

            if (achievementsForDeprecatedModules.Any()) {
                Console.WriteLine();
                Console.WriteLine("Achievements considered deprecated");
                Console.WriteLine("---");
                foreach (var deprecatedAchievement in achievementsForDeprecatedModules) {
                    Console.WriteLine($" * {deprecatedAchievement.Uid}");
                }
            }

            if (achievementsWithIssues.Any()) {
                Console.WriteLine();
                Console.WriteLine("Achievements with issues");
                Console.WriteLine("---");
                foreach (var achievementWithIssue in achievementsWithIssues) {
                    Console.WriteLine($" * {achievementWithIssue.Uid}");
                }
                // TODO: Output all in achievementsForDeprecatedModules to new achievements-next.yml file. (Then compare results to original achievements.yml since dealing with comments would be tough.)
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
