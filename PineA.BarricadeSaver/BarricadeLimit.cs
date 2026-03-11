using System;
using System.Xml.Serialization;

namespace PineA.BarricadeSaver
{
    /// <summary>
    /// 用来表示单条限制规则，例如：
    /// Type="ALL" Count="400" RequiredPermission="平民" CountInToALL="true"
    /// </summary>
    [Serializable]
    public class BarricadeLimit
    {
        /// <summary>该限制对应的类型，比如"ALL", "FARM", "BED"等</summary>
        [XmlAttribute]
        public string Type { get; set; }

        /// <summary>可放置的上限数量</summary>
        [XmlAttribute]
        public int Count { get; set; }

        /// <summary>要求的权限组。例如"平民","VIP","default"等。</summary>
        [XmlAttribute]
        public string RequiredPermission { get; set; }

        /// <summary>该类型是否要计入"ALL"的总数中</summary>
        [XmlAttribute]
        public bool CountInToALL { get; set; }
    }
}