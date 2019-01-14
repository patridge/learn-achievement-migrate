using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
    class LearningPath {
        [YamlMember(Alias = "uid")]
        public string Uid { get; set; }
        [YamlMember(Alias = "achievement")]
        public string Achievement { get; set; }
        [YamlMember(Alias = "trophy")]
        public AchievementElement Trophy { get; set; }

        // Stuff we don't care about, but YamlDotNet does.
        public LearnMetadata metadata { get; set; }
        public string title { get; set; }
        public string summary { get; set; }
        public string prerequisites { get; set; }
        public string iconUrl { get; set; }
        public List<string> levels { get; set; }
        public List<string> roles { get; set; }
        public List<string> products { get; set; }
        public List<string> modules { get; set; }
    }
}
