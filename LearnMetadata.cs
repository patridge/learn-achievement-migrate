using YamlDotNet.Serialization;

namespace learn_achievement_migrate
{
    public class LearnMetadata {
        public string title { get; set; }
        public string description { get; set; }
        [YamlMember(Alias = "ms.date")]
        public string msDate { get; set; }
        public string brand { get; set; }
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
}
