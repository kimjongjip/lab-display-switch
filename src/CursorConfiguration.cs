using System;
using System.Xml;

namespace LabDisplaySwitch {
    internal static class CursorConfiguration {
        public static string Complete(string xml) {
            var doc=new XmlDocument { XmlResolver=null,PreserveWhitespace=true };doc.LoadXml(xml);bool changed=false;
            var root=doc.SelectSingleNode("/vdd_settings");if(root==null)throw new InvalidOperationException("예상한 가상 디스플레이 설정 파일이 아닙니다.");
            var cursor=root.SelectSingleNode("cursor");if(cursor==null){cursor=doc.CreateElement("cursor");root.AppendChild(cursor);changed=true;}
            string[] keys={"HardwareCursor","CursorMaxX","CursorMaxY","AlphaCursorSupport"};string[] values={"true","128","128","true"};
            for(int i=0;i<keys.Length;i++)if(cursor.SelectSingleNode(keys[i])==null){var node=doc.CreateElement(keys[i]);node.InnerText=values[i];cursor.AppendChild(node);changed=true;}
            return changed?doc.OuterXml:xml;
        }
    }
}
