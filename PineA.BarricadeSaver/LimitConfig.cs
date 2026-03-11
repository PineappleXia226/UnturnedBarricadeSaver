using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace PineA.BarricadeSaver
{
    /// <summary>
    /// 该类对应 PineA.BarricadeLimit.xml 的根节点。
    /// </summary>
    [XmlRoot("LimitConfig")]
    public class LimitConfig : IRocketPluginConfiguration
    {
        /// <summary>
        /// 基础设置节点 <BasicSetting ... />
        /// 其中包含 Enabled / PlacementMessage / PlacementIconURL 三个属性
        /// </summary>
        [XmlElement("BasicSetting")]
        public BasicSetting BasicSetting { get; set; } = new BasicSetting();

        /// <summary>
        /// 定义所有的限制项目（类型+上限+权限等）
        /// </summary>
        [XmlElement("BarricadeLimit")]
        public List<BarricadeLimit> Limits { get; set; } = new List<BarricadeLimit>();

        /// <summary>
        /// IRocketPluginConfiguration 要求的默认值初始化
        /// </summary>
        public void LoadDefaults()
        {
            // 初始化 <BasicSetting> 节点默认值
            BasicSetting.Enabled = true;
            BasicSetting.PlacementMessage = "<b><size=15><color=#FFFACD>[建筑限制]您的建筑物上限:<color=#FFFFFF>{Current}</color>/<color=#FFFFFF>{Limit}</color></color></size></b>";
            BasicSetting.PlacementIconURL = "";

            // 初始化一些示例限制造
            Limits = new List<BarricadeLimit>
            {
                new BarricadeLimit
                {
                    Type = "ALL",
                    Count = 400,
                    RequiredPermission = "平民",
                    CountInToALL = true
                },
                new BarricadeLimit
                {
                    Type = "ALL",
                    Count = 500,
                    RequiredPermission = "VIP",
                    CountInToALL = true
                }
            };
        }
    }

    /// <summary>
    /// 表示 <BasicSetting Enabled="true" PlacementMessage="..." PlacementIconURL="..."/>
    /// </summary>
    public class BasicSetting
    {
        [XmlAttribute("Enabled")]
        public bool Enabled { get; set; }

        [XmlAttribute("PlacementMessage")]
        public string PlacementMessage { get; set; }

        [XmlAttribute("PlacementIconURL")]
        public string PlacementIconURL { get; set; }
    }
}