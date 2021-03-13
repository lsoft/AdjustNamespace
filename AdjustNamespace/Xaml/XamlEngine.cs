using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
            var objectDict = GetClrObjectDict();

            foreach (var attribute in attributeList)
            {
                if (!objectDict.ContainsKey(attribute.ClrNamespace))
                {
                    attribute.Attribute.Remove();
                    IsChangesExists = true;
                }
            }
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
            var attributeList = GetClrNamespaceAttributes();
            var objectDict = GetClrObjectDict();


            if (!objectDict.ContainsKey(sourceNamespace))
            {
                //nothing to do
                return;
            }

            XNamespace xTargetNamespace = ClrNamespace + targetNamespace;

            foreach (var clrObject in objectDict[sourceNamespace])
            {
                if (clrObject.ClassName != objectClassName)
                {
                    continue;
                }

                //object to transfer has been found


                //work with target namespace
                var fa = attributeList.FirstOrDefault(a => a.ClrNamespace == targetNamespace);
                if (fa == null)
                {
                    IsChangesExists = true;

                    _xmlDocument.Root.SetAttributeValue(
                        XNamespace.Xmlns + GetUniqueClrNamespace(targetNamespace),
                        xTargetNamespace
                        );
                    attributeList = GetClrNamespaceAttributes();

                    fa = attributeList.FirstOrDefault(a => a.ClrNamespace == targetNamespace);
                    if (fa == null)
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

        private List<XamlClrAttribute> GetClrNamespaceAttributes()
        {
            var result = new List<XamlClrAttribute>();


            foreach (var element in IterateElements(_xmlDocument.Root))
            {
                //Debug.WriteLine(element.Name);

                foreach (var clrAttribute in IterateClrAttributes(element))
                {
                    result.Add(
                        new XamlClrAttribute(
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
