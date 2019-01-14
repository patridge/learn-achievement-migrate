using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
    class Module : LearnElement {
        [YamlMember(Alias = "badge")]
        public AchievementElement Badge { get; set; }

        // Stuff we don't care about, but YamlDotNet does.
        public List<string> units { get; set; }
    }
}
