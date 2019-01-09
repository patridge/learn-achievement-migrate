using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
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
}
