using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
    class LearnElement {
        [YamlMember(Alias = "uid")]
        public string Uid { get; set; }
        [YamlMember(Alias = "achievement")]
        public string Achievement { get; set; }

        // Stuff we don't care about, but YamlDotNet does.
        public LearnMetadata metadata { get; set; }
        public string title { get; set; }
        public string summary { get; set; }
        public string @abstract { get; set; }
        public string cardDescription { get; set; }
        public string prerequisites { get; set; }
        public string iconUrl { get; set; }
        public List<string> levels { get; set; }
        public List<string> roles { get; set; }
        public List<string> products { get; set; }
    }
}
