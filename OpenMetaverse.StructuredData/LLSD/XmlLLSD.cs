/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Text;

namespace OpenMetaverse.StructuredData
{
    /// <summary>
    /// 
    /// </summary>
    public static partial class OSDParser
    {
        private static XmlSchema XmlSchema;
        private static XmlTextReader XmlTextReader;
        private static string LastXmlErrors = String.Empty;
        private static object XmlValidationLock = new object();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static OSD DeserializeLLSDXml(byte[] xmlData)
        {
            using(XmlTextReader xrd =  new XmlTextReader(new MemoryStream(xmlData, false)))
                return DeserializeLLSDXml(xrd);
        }

        public static OSD DeserializeLLSDXml(Stream xmlStream)
        {
            using(XmlTextReader xrd = new XmlTextReader(xmlStream))
                return DeserializeLLSDXml(xrd);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static OSD DeserializeLLSDXml(string xmlData)
        {
            byte[] bytes = Utils.StringToBytes(xmlData);
            using(XmlTextReader xrd = new XmlTextReader(new MemoryStream(bytes, false)))
                return DeserializeLLSDXml(xrd);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static OSD DeserializeLLSDXml(XmlTextReader xmlData)
        {
            try
            {
                xmlData.Read();
                SkipWhitespace(xmlData);

                xmlData.Read();
                OSD ret = ParseLLSDXmlElement(xmlData);

                return ret;
            }
            catch
            {
                return new OSD();
            }
        }

        public static byte[] SerializeLLSDXmlBytes(OSD data, bool formal = false)
        {
            return Encoding.UTF8.GetBytes(SerializeLLSDXmlString(data, formal));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string SerializeLLSDXmlString(OSD data, bool formal = false)
        {
            StringBuilder sb = new StringBuilder(16384);
            if(formal)
                sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

            sb.Append("<llsd>");
            SerializeLLSDXmlElement(sb, data, formal);
            sb.Append("</llsd>");

            return sb.ToString();
        }

        public static string SerializeLLSDInnerXmlString(OSD data,bool formal = false)
        {
            StringBuilder sb = new StringBuilder(16384);
            SerializeLLSDXmlElement(sb, data, formal);

            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="data"></param>
        public static void SerializeLLSDXmlElement(StringBuilder sb, OSD data, bool formal)
        {
            switch (data.Type)
            {
                case OSDType.Unknown:
                    sb.Append("<undef />");
                    break;
                case OSDType.Boolean:
                    sb.Append("<boolean>");
                    sb.Append(data.AsString());
                    sb.Append("</boolean>");
                    break;
                case OSDType.Integer:
                    sb.Append("<integer>");
                    sb.Append(data.AsString());
                    sb.Append("</integer>");
                    break;
                case OSDType.Real:
                    sb.Append("<real>");
                    sb.Append(data.AsString());
                    sb.Append("</real>");
                    break;
                case OSDType.String:
                    sb.Append("<string>");
                    sb.Append(data.AsString());
                    sb.Append("</string>");
                    break;
                case OSDType.UUID:
                    sb.Append("<uuid>");
                    sb.Append(data.AsString());
                    sb.Append("</uuid>");
                    break;
                case OSDType.Date:
                    sb.Append("<date>");
                    sb.Append(data.AsString());
                    sb.Append("</date>");
                    break;
                case OSDType.URI:
                    sb.Append("<uri>");
                    sb.Append(data.AsString());
                    sb.Append("</uri>");
                    break;
                case OSDType.Binary:
                    if(formal)
                        sb.Append("<binary encoding=\"base64\">");
                    else
                        sb.Append("<binary>");
                    sb.Append(data.AsString());
                    sb.Append("</binary>");
                    break;
                case OSDType.Map:
                    OSDMap map = (OSDMap)data;
                    sb.Append("<map>");
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        sb.Append("<key>");
                        sb.Append(kvp.Key);
                        sb.Append("</key>");

                        SerializeLLSDXmlElement(sb, kvp.Value, formal);
                    }
                    sb.Append("</map>");
                    break;
                case OSDType.Array:
                    OSDArray array = (OSDArray)data;
                    sb.Append("<array>");
                    for (int i = 0; i < array.Count; i++)
                    {
                        SerializeLLSDXmlElement(sb, array[i], formal);
                    }
                    sb.Append("</array>");
                    break;
                case OSDType.LLSDxml:
                    sb.Append(data.AsString());
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlData"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool TryValidateLLSDXml(XmlTextReader xmlData, out string error)
        {
            lock (XmlValidationLock)
            {
                LastXmlErrors = String.Empty;
                XmlTextReader = xmlData;

                CreateLLSDXmlSchema();

                XmlReaderSettings readerSettings = new XmlReaderSettings();
                readerSettings.ValidationType = ValidationType.Schema;
                readerSettings.Schemas.Add(XmlSchema);
                readerSettings.ValidationEventHandler += new ValidationEventHandler(LLSDXmlSchemaValidationHandler);

                using(XmlReader reader = XmlReader.Create(xmlData, readerSettings))
                {

                    try
                    {
                        while (reader.Read()) { }
                    }
                    catch (XmlException)
                    {
                        error = LastXmlErrors;
                        return false;
                    }

                    if (LastXmlErrors == String.Empty)
                    {
                        error = null;
                        return true;
                    }
                    else
                    {
                        error = LastXmlErrors;
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static OSD ParseLLSDXmlElement(XmlTextReader reader)
        {
            SkipWhitespace(reader);

            if (reader.NodeType != XmlNodeType.Element)
                throw new OSDException("Expected an element");

            string type = reader.LocalName;
            OSD ret;

            switch (type)
            {
                case "undef":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return new OSD();
                    }

                    reader.Read();
                    SkipWhitespace(reader);
                    ret = new OSD();
                    break;
                case "boolean":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromBoolean(false);
                    }

                    if (reader.Read())
                    {
                        string s = reader.ReadString().Trim();

                        if (!String.IsNullOrEmpty(s) && (s == "true" || s == "1"))
                        {
                            ret = OSD.FromBoolean(true);
                            break;
                        }
                    }

                    ret = OSD.FromBoolean(false);
                    break;
                case "integer":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromInteger(0);
                    }

                    if (reader.Read())
                    {
                        int value = 0;
                        Int32.TryParse(reader.ReadString().Trim(), out value);
                        ret = OSD.FromInteger(value);
                        break;
                    }

                    ret = OSD.FromInteger(0);
                    break;
                case "real":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromReal(0d);
                    }

                    if (reader.Read())
                    {
                        double value = 0d;
                        string str = reader.ReadString().Trim().ToLower();

                        if (str == "nan")
                            value = Double.NaN;
                        else
                            Utils.TryParseDouble(str, out value);

                        ret = OSD.FromReal(value);
                        break;
                    }

                    ret = OSD.FromReal(0d);
                    break;
                case "uuid":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromUUID(UUID.Zero);
                    }

                    if (reader.Read())
                    {
                        UUID value = UUID.Zero;
                        UUID.TryParse(reader.ReadString().Trim(), out value);
                        ret = OSD.FromUUID(value);
                        break;
                    }

                    ret = OSD.FromUUID(UUID.Zero);
                    break;
                case "date":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromDate(Utils.Epoch);
                    }

                    if (reader.Read())
                    {
                        DateTime value = Utils.Epoch;
                        DateTime.TryParse(reader.ReadString().Trim(), out value);
                        ret = OSD.FromDate(value);
                        break;
                    }

                    ret = OSD.FromDate(Utils.Epoch);
                    break;
                case "string":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromString(String.Empty);
                    }

                    if (reader.Read())
                    {
                        ret = OSD.FromString(reader.ReadString());
                        break;
                    }

                    ret = OSD.FromString(String.Empty);
                    break;
                case "binary":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromBinary(Utils.EmptyBytes);
                    }

                    if (reader.GetAttribute("encoding") != null && reader.GetAttribute("encoding") != "base64")
                        throw new OSDException("Unsupported binary encoding: " + reader.GetAttribute("encoding"));

                    if (reader.Read())
                    {
                        try
                        {
                            ret = OSD.FromBinary(Convert.FromBase64String(reader.ReadString().Trim()));
                            break;
                        }
                        catch (FormatException ex)
                        {
                            throw new OSDException("Binary decoding exception: " + ex.Message);
                        }
                    }

                    ret = OSD.FromBinary(Utils.EmptyBytes);
                    break;
                case "uri":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromUri(new Uri(String.Empty, UriKind.RelativeOrAbsolute));
                    }

                    if (reader.Read())
                    {
                        ret = OSD.FromUri(new Uri(reader.ReadString(), UriKind.RelativeOrAbsolute));
                        break;
                    }

                    ret = OSD.FromUri(new Uri(String.Empty, UriKind.RelativeOrAbsolute));
                    break;
                case "map":
                    return ParseLLSDXmlMap(reader);
                case "array":
                    return ParseLLSDXmlArray(reader);
                default:
                    reader.Read();
                    ret = null;
                    break;
            }

            if (reader.NodeType != XmlNodeType.EndElement || reader.LocalName != type)
            {
                throw new OSDException("Expected </" + type + ">");
            }
            else
            {
                reader.Read();
                return ret;
            }
        }

        private static OSDMap ParseLLSDXmlMap(XmlTextReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "map")
                throw new NotImplementedException("Expected <map>");

            OSDMap map = new OSDMap();

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return map;
            }

            if (reader.Read())
            {
                while (true)
                {
                    SkipWhitespace(reader);

                    if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "map")
                    {
                        reader.Read();
                        break;
                    }

                    if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "key")
                        throw new OSDException("Expected <key>");

                    string key = reader.ReadString();

                    if (reader.NodeType != XmlNodeType.EndElement || reader.LocalName != "key")
                        throw new OSDException("Expected </key>");

                    if (reader.Read())
                        map[key] = ParseLLSDXmlElement(reader);
                    else
                        throw new OSDException("Failed to parse a value for key " + key);
                }
            }

            return map;
        }

        private static OSDArray ParseLLSDXmlArray(XmlTextReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "array")
                throw new OSDException("Expected <array>");

            OSDArray array = new OSDArray();

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return array;
            }

            if (reader.Read())
            {
                while (true)
                {
                    SkipWhitespace(reader);

                    if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "array")
                    {
                        reader.Read();
                        break;
                    }

                    array.Add(ParseLLSDXmlElement(reader));
                }
            }

            return array;
        }        

        private static void SkipWhitespace(XmlTextReader reader)
        {
            while (
                reader.NodeType == XmlNodeType.Comment ||
                reader.NodeType == XmlNodeType.Whitespace ||
                reader.NodeType == XmlNodeType.SignificantWhitespace ||
                reader.NodeType == XmlNodeType.XmlDeclaration)
            {
                reader.Read();
            }
        }

        private static void CreateLLSDXmlSchema()
        {
            if (XmlSchema == null)
            {
                #region XSD
                string schemaText = @"
<?xml version=""1.0"" encoding=""utf-8""?>
<xs:schema elementFormDefault=""qualified"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <xs:import schemaLocation=""xml.xsd"" namespace=""http://www.w3.org/XML/1998/namespace"" />
  <xs:element name=""uri"" type=""xs:string"" />
  <xs:element name=""uuid"" type=""xs:string"" />
  <xs:element name=""KEYDATA"">
    <xs:complexType>
      <xs:sequence>
        <xs:element ref=""key"" />
        <xs:element ref=""DATA"" />
      </xs:sequence>
    </xs:complexType>
  </xs:element>
  <xs:element name=""date"" type=""xs:string"" />
  <xs:element name=""key"" type=""xs:string"" />
  <xs:element name=""boolean"" type=""xs:string"" />
  <xs:element name=""undef"">
    <xs:complexType>
      <xs:sequence>
        <xs:element ref=""EMPTY"" />
      </xs:sequence>
    </xs:complexType>
  </xs:element>
  <xs:element name=""map"">
    <xs:complexType>
      <xs:sequence>
        <xs:element minOccurs=""0"" maxOccurs=""unbounded"" ref=""KEYDATA"" />
      </xs:sequence>
    </xs:complexType>
  </xs:element>
  <xs:element name=""real"" type=""xs:string"" />
  <xs:element name=""ATOMIC"">
    <xs:complexType>
      <xs:choice>
        <xs:element ref=""undef"" />
        <xs:element ref=""boolean"" />
        <xs:element ref=""integer"" />
        <xs:element ref=""real"" />
        <xs:element ref=""uuid"" />
        <xs:element ref=""string"" />
        <xs:element ref=""date"" />
        <xs:element ref=""uri"" />
        <xs:element ref=""binary"" />
      </xs:choice>
    </xs:complexType>
  </xs:element>
  <xs:element name=""DATA"">
    <xs:complexType>
      <xs:choice>
        <xs:element ref=""ATOMIC"" />
        <xs:element ref=""map"" />
        <xs:element ref=""array"" />
      </xs:choice>
    </xs:complexType>
  </xs:element>
  <xs:element name=""llsd"">
    <xs:complexType>
      <xs:sequence>
        <xs:element ref=""DATA"" />
      </xs:sequence>
    </xs:complexType>
  </xs:element>
  <xs:element name=""binary"">
    <xs:complexType>
      <xs:simpleContent>
        <xs:extension base=""xs:string"">
          <xs:attribute default=""base64"" name=""encoding"" type=""xs:string"" />
        </xs:extension>
      </xs:simpleContent>
    </xs:complexType>
  </xs:element>
  <xs:element name=""array"">
    <xs:complexType>
      <xs:sequence>
        <xs:element minOccurs=""0"" maxOccurs=""unbounded"" ref=""DATA"" />
      </xs:sequence>
    </xs:complexType>
  </xs:element>
  <xs:element name=""integer"" type=""xs:string"" />
  <xs:element name=""string"">
    <xs:complexType>
      <xs:simpleContent>
        <xs:extension base=""xs:string"">
          <xs:attribute ref=""xml:space"" />
        </xs:extension>
      </xs:simpleContent>
    </xs:complexType>
  </xs:element>
</xs:schema>
";
                #endregion XSD

                MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(schemaText));

                XmlSchema = new XmlSchema();
                XmlSchema = XmlSchema.Read(stream, new ValidationEventHandler(LLSDXmlSchemaValidationHandler));
            }
        }

        private static void LLSDXmlSchemaValidationHandler(object sender, ValidationEventArgs args)
        {
            string error = String.Format("Line: {0} - Position: {1} - {2}", XmlTextReader.LineNumber, XmlTextReader.LinePosition,
                args.Message);

            if (LastXmlErrors == String.Empty)
                LastXmlErrors = error;
            else
                LastXmlErrors += Environment.NewLine + error;
        }
    }
}
