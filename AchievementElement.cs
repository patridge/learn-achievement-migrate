using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
    public class AchievementElement {
        [YamlMember(Alias = "uid")]
        public string Uid { get; set; }
    }
}
