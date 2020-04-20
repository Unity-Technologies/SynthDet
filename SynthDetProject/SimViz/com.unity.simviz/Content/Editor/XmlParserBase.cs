using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace UnityEngine.SimViz.Content
{
    internal class XmlParserBase
    {
        protected internal int NumErrorsLogged { get; protected set; }
        internal HashSet<string> _repetitiveWarnings = new HashSet<string>();

        private const NumberStyles Style = NumberStyles.Any;
        private readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        internal XElement GetRequiredElement(XElement parent, string elementName)
        {
            var element = parent.Element(elementName);
            if (element == null)
            {
                LogMissingElementError(parent, elementName);
                return default;
            }

            return element;
        }

        internal IEnumerable<XElement> GetRequiredElements(XElement parent, string elementName)
        {
            var elements = parent.Elements(elementName);
            if (!elements.Any())
            {
                LogMissingElementError(parent, elementName);
                return default;
            }

            return elements;
        }

        internal bool ReadBoolOrDefault(XElement element, string attributeName, bool defaultValue = false)
        {
            if (TryReadAttribute(element, attributeName, out var valueStr))
            {
                if (bool.TryParse(valueStr, out var valueOut))
                {
                    return valueOut;
                }
                LogFailedParseError(element, attributeName, valueStr, "bool");
            }

            return defaultValue;
        }

        internal TEnum ReadEnum<TEnum>(XElement element, string attributeName) where TEnum : struct
        {
            if (TryReadAttribute(element, attributeName, out var valueStr))
            {
                if (Enum.TryParse<TEnum>(valueStr, true, out var value))
                {
                    return value;
                }
                LogFailedParseError(element, attributeName, valueStr, typeof(TEnum).ToString());
                return default;
            }

            LogMissingAttributeError(element, attributeName);
            return default;
        }

        internal TEnum ReadEnumOrDefault<TEnum>(XElement element, string attributeName, TEnum defaultValue) where TEnum : struct
        {
            if (TryReadAttribute(element, attributeName, out var valueStr))
            {
                if (Enum.TryParse<TEnum>(valueStr, true, out var value))
                {
                    return value;
                }
                LogFailedParseWarning(element, attributeName, valueStr, typeof(TEnum).ToString());
            }

            return defaultValue;
        }

        internal float ReadFloat(XElement element, string attributeName)
        {
            if (TryReadAttribute(element, attributeName, out var valueStr))
            {
                if (float.TryParse(valueStr, Style, Culture, out var value))
                {
                    return value;
                }

                LogFailedParseError(element, attributeName, valueStr, "float");
                return float.NaN;
            }

            LogMissingAttributeError(element, attributeName);
            return float.NaN;
        }

        internal float ReadFloatOrDefault(XElement element, string attributeName, float defaultValue)
        {
            if (TryReadAttribute(element, attributeName, out var valueStr))
            {
                if (float.TryParse(valueStr, Style, Culture, out var value))
                {
                    return value;
                }

                LogFailedParseWarning(element, attributeName, valueStr, "float");
            }

            return defaultValue;
        }

        internal int ReadInt(XElement element, string attributeName)
        {
            if (TryReadAttribute(element, attributeName, out var valueStr))
            {
                int value;
                if (int.TryParse(valueStr, out value))
                {
                    return value;
                }
                LogFailedParseError(element, attributeName, valueStr, "int");
                return default;
            }

            LogMissingAttributeError(element, attributeName);
            return default;
        }

        internal int ReadIntOrDefault(XElement element, string attributeName, int defaultValue)
        {
            if (TryReadAttribute(element, attributeName, out var valueStr))
            {
                int value;
                if (int.TryParse(valueStr, Style, Culture,
                    out value))
                {
                    return value;
                }
                LogFailedParseWarning(element, attributeName, valueStr, "int");
            }

            return defaultValue;
        }

        internal float ReadPositiveFloat(XElement element, string attributeName)
        {
            var val = ReadFloat(element, attributeName);

            if (val < 0)
            {
                var valString = val.ToString(CultureInfo.CurrentCulture);
                LogInvalidValueError(element, attributeName, valString, "it must be non-negative");
                return float.NaN;
            }

            return val;
        }

        internal string ReadString(XElement element, string attributeName)
        {
            if (TryReadAttribute(element, attributeName, out var outValue))
            {
                return outValue;
            }

            LogMissingAttributeError(element, attributeName);
            return null;
        }

        internal string ReadStringOrDefault(XElement element, string attributeName, string defaultValue)
        {
            return TryReadAttribute(element, attributeName, out var value) ? value : defaultValue;
        }

        protected void LogUnsupportedElementWarning(string elementName)
        {
            LogWarningOnce($"No parsing implemented yet for {elementName}");
        }

        protected void LogFailedParseWarning(XElement element, string attributeName, string attributeValue, string typeString)
        {
            Debug.LogWarning(MakeFailedParseMessage(element, attributeName, attributeValue, typeString));
        }

        protected void LogFailedParseError(XElement element, string attributeName, string attributeValue, string typeString)
        {
            Debug.LogError(MakeFailedParseMessage(element, attributeName, attributeValue, typeString));
            ++NumErrorsLogged;
        }

        protected void LogMissingAttributeError(XElement element, string attributeName)
        {
            var lineMessage = MakeLineMessage(element);
            var errorMessage = $"{element.Name} did not have required attribute '{attributeName}'{lineMessage}. ";
            Debug.LogError(errorMessage);
            ++NumErrorsLogged;
        }

        protected void LogMissingElementError(XElement element, string elementName)
        {
            var lineMessage = MakeLineMessage(element);
            var errorMessage = $"{element.Name} did not have required element '{elementName}'{lineMessage}. ";
            Debug.LogError(errorMessage);
            ++NumErrorsLogged;
        }

        protected void LogInvalidValueError(XElement element, string attributeName, string attributeValue,
            string errorReason)
        {
            var lineMessage = MakeLineMessage(element);
            Debug.LogError($"{element.Name}'s {attributeName} value ({attributeValue}){lineMessage} is invalid because {errorReason}.");
            ++NumErrorsLogged;
        }

        // Only logs this warning if it hasn't been logged yet
        protected void LogWarningOnce(string message)
        {
            if (!_repetitiveWarnings.Contains(message))
            {
                Debug.LogWarning(message);
                _repetitiveWarnings.Add(message);
            }
        }

        protected static string MakeFailedParseMessage(XElement element, string attributeName, string attributeValue, string typeString)
        {
            var lineMessage = MakeLineMessage(element);
            return $"{element.Name}'s {attributeName} value ({attributeValue}){lineMessage} could not be parsed as {typeString}.";
        }

        protected static string MakeLineMessage(XElement element)
        {
            return ((IXmlLineInfo)element).HasLineInfo() ? $" at line {((IXmlLineInfo)element).LineNumber}" : "";
        }

        protected static bool TryReadAttribute(XElement element, string attributeName, out string value)
        {
            var attribute = element.Attribute(attributeName);
            if (attribute != null)
            {
                value = attribute.Value;
                return true;
            }

            value = null;
            return false;
        }
    }
}
