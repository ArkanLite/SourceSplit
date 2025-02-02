﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace LiveSplit.SourceSplit.Utilities
{
    public static class XMLUtils
    {
        public static XmlElement ToElement<E>(XmlDocument document, string name, E value)
        {
            XmlElement str = document.CreateElement(name);
            str.InnerText = value.ToString();
            return str;
        }
    }
}
