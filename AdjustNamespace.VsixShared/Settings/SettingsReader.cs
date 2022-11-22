using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace AdjustNamespace.VsixShared.Settings
{
    internal class SettingsReader
    {
        public const string SettingFileName = "adjust_namespaces_settings.xml";
     
        private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(AdjustNamespaceSettings));
        public static AdjustNamespaceSettings? ReadSettings(
            string solutionFolder
            )
        {
            var settingsFilePath = Path.Combine(solutionFolder, SettingFileName);
            if(!File.Exists(settingsFilePath))
            {
                return null;
            }

            using (var fs = new FileStream(settingsFilePath, FileMode.Open))
            {
                var result = (AdjustNamespaceSettings)_serializer.Deserialize(fs);
                return result;
            }
        }

        //public static void Write(
        //    string solutionFolder
        //    )
        //{
        //    var settingsFilePath = Path.Combine(solutionFolder, SettingFileName);
        //    if (File.Exists(settingsFilePath))
        //    {
        //        File.Delete(settingsFilePath);
        //    }

        //    using (var fs = new FileStream(settingsFilePath, FileMode.Create))
        //    {
        //        var s = new AdjustNamespaceSettings();
        //        s.SkippedFolderSuffixes.Add(@"C:\projects\AdjustNamespace\Tests\Subject");
        //        s.SkippedFolderSuffixes.Add(@"AdjustNamespace\Tests\Subject");
        //        _serializer.Serialize(
        //            fs,
        //            s
        //            );
        //    }
        //}
    }
}
