using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Markup;
using System.Xaml;
using System.Xml.Linq;

namespace AdjustNamespace.Xaml
{
    public class XamlEngine
    {
        public const string ClrNamespace = "clr-namespace:";

        private readonly XDocument _xmlDocument;
        private readonly long _uniqueKey;

        public string XamlFilePath
        {
            get;
        }

        public bool IsChangesExists
        {
            get;
            private set;
        }

        public XamlEngine(string xamlFilePath)
        {
            XamlFilePath = xamlFilePath;

            _xmlDocument = XDocument.Load(xamlFilePath);

            _uniqueKey = DateTime.Now.Ticks / 1000000L;
        }

        public void RemoveObsoleteClrNamespaces()
        {
            var attributeList = GetClrNamespaceAttributes();
            var references = GetReferences();

            foreach (var attribute in attributeList)
            {
                if (references.Any(r => r.DoesReferenceTo(attribute)))
                {
                    continue;
                }

                attribute.Attribute.Remove();
                IsChangesExists = true;
            }

            //var objectDict = GetClrObjectDict();

            //foreach (var attribute in attributeList)
            //{
            //    if (!objectDict.ContainsKey(attribute.ClrNamespace))
            //    {
            //        attribute.Attribute.Remove();
            //        IsChangesExists = true;
            //    }
            //}
        }

        public void MoveObject(
            string sourceNamespace,
            string objectClassName,
            string targetNamespace
            )
        {
            if (sourceNamespace is null)
            {
                throw new ArgumentNullException(nameof(sourceNamespace));
            }

            if (objectClassName is null)
            {
                throw new ArgumentNullException(nameof(objectClassName));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            MoveReferencedClass(sourceNamespace, objectClassName, targetNamespace);

            MoveCurrentClass(sourceNamespace, objectClassName, targetNamespace);

        }

        public XDocument GetModifiedXmlDocument()
        {
            return _xmlDocument;
        }


        public void SaveIfChangesExists()
        {
            if (IsChangesExists)
            {
                RemoveObsoleteClrNamespaces();

                var sb = new StringBuilder();
                using (var xw = new CustomXmlWriter(sb))
                {
                    _xmlDocument.Save(xw);
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                    //flush async does not implemented, at least in net 4.8
                    xw.Flush();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                }

                var plainBody = sb.ToString();

                File.WriteAllText(XamlFilePath, plainBody);
            }
        }

        public bool GetRootInfo(out string? rootNamespace, out string? rootName)
        {
            var xClassAttribute = IterateXClassAttributes(_xmlDocument.Root).FirstOrDefault();

            if (xClassAttribute == null)
            {
                rootNamespace = null;
                rootName = null;
                return false;
            }

            var namespaceAndName = xClassAttribute.Value;

            var indexof = namespaceAndName.LastIndexOf('.');
            if(indexof <= 0)
            {
                rootNamespace = null;
                rootName = null;
                return false;
            }

            rootNamespace = namespaceAndName.Substring(
                0,
                indexof
                );
            rootName = namespaceAndName.Substring(
                indexof + 1
                );

            return true;
        }

        public List<XamlReference> GetReferences()
        {
            var result = new List<XamlReference>();

            StringReader? sr = null;
            XamlXmlReader? xamlReader = null;
            try
            {
                sr = new StringReader(_xmlDocument.ToString());
                xamlReader = new XamlXmlReader(sr);

                var valueCatch = -1;
                while (xamlReader.Read())
                {
                    if (valueCatch == 0)
                    {
                        if (xamlReader.Value is string vs)
                        {
                            if (vs.Contains(':') && vs.Contains('.'))
                            {
                                var indexof0 = vs.IndexOf(':');
                                var alias = vs.Substring(0, indexof0);

                                //var classAndMemberName = vs.Substring(indexof0 + 1);
                                //var indexof1 = classAndMemberName.IndexOf('.');
                                //var className = classAndMemberName.Substring(0, indexof1);

                                result.Add(
                                    new XamlReference(
                                        alias,
                                        null
                                        //,className
                                        )
                                    );
                            }
                        }

                        valueCatch = -1;
                    }

                    if (xamlReader.Type != null)
                    {
                        if (xamlReader.Type.Name == "StaticExtension")
                        {
                            valueCatch = 2;
                        }
                        
                        if (xamlReader.Type.PreferredXamlNamespace.StartsWith(ClrNamespace))
                        {
                            var preNamespace = xamlReader.Type.PreferredXamlNamespace.Substring(ClrNamespace.Length);

                            if (preNamespace.Contains(';'))
                            {
                                preNamespace = preNamespace.Substring(
                                    0,
                                    preNamespace.IndexOf(';')
                                    );
                            }

                            result.Add(
                                new XamlReference(
                                    null,
                                    preNamespace
                                    //,xamlReader.Type.Name
                                    )
                                );
                        }
                    }

                    if (valueCatch > 0)
                    {
                        valueCatch--;
                    }
                }
            }
            finally
            {
                xamlReader?.Close();
                sr?.Close();
            }

            return result;
        }


        private void MoveCurrentClass(string sourceNamespace, string objectClassName, string targetNamespace)
        {
            var objectFullName = sourceNamespace + "." + objectClassName;
            foreach (var element in IterateElements(_xmlDocument.Root))
            {
                foreach (var attribute in IterateXClassAttributes(element))
                {
                    if (objectFullName == attribute.Value)
                    {
                        attribute.Value = targetNamespace + "." + objectClassName;
                        IsChangesExists = true;
                    }
                }
            }
        }

        private void MoveReferencedClass(string sourceNamespace, string objectClassName, string targetNamespace)
        {
            var namespaceList = GetClrNamespaceAttributes();

            XNamespace xTargetNamespace = ClrNamespace + targetNamespace;

            var objectDict = GetClrObjectDict();
            if (objectDict.ContainsKey(sourceNamespace))
            {
                foreach (var clrObject in objectDict[sourceNamespace])
                {
                    if (clrObject.ClassName != objectClassName)
                    {
                        continue;
                    }

                    //object to transfer has been found


                    //work with target namespace
                    var foundNamespace = namespaceList.FirstOrDefault(a => a.ClrNamespace == targetNamespace);
                    if (foundNamespace == null)
                    {
                        IsChangesExists = true;

                        _xmlDocument.Root.SetAttributeValue(
                            XNamespace.Xmlns + GetUniqueClrNamespace(targetNamespace),
                            xTargetNamespace
                            );
                        namespaceList = GetClrNamespaceAttributes();

                        foundNamespace = namespaceList.FirstOrDefault(a => a.ClrNamespace == targetNamespace);
                        if (foundNamespace == null)
                        {
                            throw new InvalidOperationException("Cannot add new xmlns namespace attribute");
                        }
                    }

                    XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
                    XElement replaced = new XElement(
                        xTargetNamespace + clrObject.ClassName
                        );
                    foreach (var a in clrObject.Element.Attributes())
                    {
                        replaced.Add(a);
                    }
                    foreach (var e in clrObject.Element.Elements())
                    {
                        replaced.Add(e);
                    }

                    clrObject.Element.ReplaceWith(
                        replaced
                        );
                    IsChangesExists = true;
                }
            }

            var attributeList = GetClrAttributes();
            if (attributeList.Count > 0)
            {
                foreach (var clrAttribute in attributeList.FindAll(a => a.ClassName == objectClassName))
                {
                    var clrNamespace = namespaceList.FirstOrDefault(n => n.XamlKey == clrAttribute.Alias);
                    if (clrNamespace == null)
                    {
                        continue;
                    }

                    if (clrNamespace.ClrNamespace != sourceNamespace)
                    {
                        continue;
                    }

                    //work with target namespace
                    var foundNamespace = namespaceList.FirstOrDefault(a => a.ClrNamespace == targetNamespace);
                    if (foundNamespace == null)
                    {
                        IsChangesExists = true;

                        _xmlDocument.Root.SetAttributeValue(
                            XNamespace.Xmlns + GetUniqueClrNamespace(targetNamespace),
                            xTargetNamespace
                            );
                        namespaceList = GetClrNamespaceAttributes();

                        foundNamespace = namespaceList.FirstOrDefault(a => a.ClrNamespace == targetNamespace);
                        if (foundNamespace == null)
                        {
                            throw new InvalidOperationException("Cannot add new xmlns namespace attribute");
                        }
                    }

                    clrAttribute.Attribute.Parent.SetAttributeValue(
                        clrAttribute.Attribute.Name,
                        clrAttribute.Attribute.Value.Replace($" {clrAttribute.Alias}:", $" {foundNamespace.XamlKey}:")
                        );

                    IsChangesExists = true;
                }
            }
        }


        private Dictionary<string, List<XamlClrObject>> GetClrObjectDict()
        {
            var dict = new Dictionary<string, List<XamlClrObject>>();

            foreach (var clrObject in GetClrObjects())
            {
                if (!dict.ContainsKey(clrObject.Namespace))
                {
                    dict[clrObject.Namespace] = new List<XamlClrObject>();
                }

                dict[clrObject.Namespace].Add(clrObject);
            }

            return dict;
        }

        private List<XamlClrAttribute> GetClrAttributes()
        {
            var result = new List<XamlClrAttribute>();

            var xaml2006 = Get2006XamlAttribute();
            if (xaml2006 == null)
            {
                return result;
            }

            var substring = $"{xaml2006.Name.LocalName}:Static";

            foreach (var element in IterateElements(_xmlDocument.Root))
            {
                foreach (var attribute in element.Attributes())
                {
                    var av = attribute.Value;
                    if (av.Contains(substring))
                    {
                        var i = av.IndexOf(substring);
                        var suf = av.Substring(i + substring.Length).Trim();
                        var indexof0 = suf.IndexOf(':');
                        if (indexof0 > 0)
                        {
                            var alias = suf.Substring(0, indexof0);

                            var classAndMemberName = suf.Substring(indexof0 + 1);
                            var indexof1 = classAndMemberName.IndexOf('.');
                            var className = classAndMemberName.Substring(0, indexof1);

                            result.Add(
                                new XamlClrAttribute(
                                    alias,
                                    className,
                                    attribute
                                    )
                                );
                        }
                    }
                }
            }

            return result;
        }

        private List<XamlClrObject> GetClrObjects()
        {
            var result = new List<XamlClrObject>();

            foreach (var element in IterateElements(_xmlDocument.Root))
            {
                if (element.Name.Namespace.NamespaceName.StartsWith(ClrNamespace))
                {
                    Debug.WriteLine(element.Name);

                    result.Add(
                        new XamlClrObject(
                            element
                            )
                        );
                }
            }

            return result;
        }

        private List<XamlClrNamespace> GetClrNamespaceAttributes()
        {
            var result = new List<XamlClrNamespace>();


            foreach (var element in IterateElements(_xmlDocument.Root))
            {
                //Debug.WriteLine(element.Name);

                foreach (var clrAttribute in IterateClrAttributes(element))
                {
                    result.Add(
                        new XamlClrNamespace(
                            clrAttribute
                            )
                        );
                }
            }


            return result;
        }

        private IEnumerable<XAttribute> IterateXClassAttributes(XElement element)
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name.Namespace == "http://schemas.microsoft.com/winfx/2006/xaml" && attribute.Name.LocalName == "Class")
                {
                    yield return attribute;
                }
            }

        }

        private XAttribute? Get2006XamlAttribute()
        {
            foreach (var attribute in _xmlDocument.Root.Attributes())
            {
                if (attribute.Value == "http://schemas.microsoft.com/winfx/2006/xaml")
                {
                    return attribute;
                }
            }

            return null;
        }

        private IEnumerable<XAttribute> IterateClrAttributes(XElement element)
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name.Namespace == "http://www.w3.org/2000/xmlns/")
                {
                    if (!attribute.Value.StartsWith(ClrNamespace))
                    {
                        continue;
                    }

                    yield return attribute;
                }
            }

        }

        private IEnumerable<XElement> IterateElements(XElement root)
        {
            yield return root;

            foreach (var element in root.Elements())
            {
                foreach (var child in IterateElements(element))
                {
                    yield return child;
                }
            }
        }

        private string GetUniqueClrNamespace(string targetNamespace)
        {
            return $"cs_{Math.Abs(targetNamespace.GetHashCode())}_{_uniqueKey}";
        }

    }
}
