using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
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
