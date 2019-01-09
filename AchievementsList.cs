using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
    class AchievementsList {
        [YamlMember(Alias = "achievements")]
        public List<Achievement> Achievements { get; set; }
    }
}
