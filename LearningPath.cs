using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
    class LearningPath : LearnElement {
        [YamlMember(Alias = "trophy")]
        public AchievementElement Trophy { get; set; }

        // Stuff we don't care about, but YamlDotNet does.
        public List<string> modules { get; set; }
    }
}
