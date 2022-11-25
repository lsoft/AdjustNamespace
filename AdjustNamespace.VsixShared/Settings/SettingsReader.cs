using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace AdjustNamespace.VsixShared.Settings
{
    public class SettingsReader
    {
        public const string SettingFileName = "adjust_namespaces_settings.xml";
     
        private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(AdjustNamespaceSettings));
        private readonly string _solutionFolder;

        public SettingsReader(string solutionFolder)
        {
            if (solutionFolder is null)
            {
                throw new ArgumentNullException(nameof(solutionFolder));
            }

            _solutionFolder = solutionFolder;
        }

        public  AdjustNamespaceSettings? ReadSettings(
            )
        {
            var settingsFilePath = Path.Combine(_solutionFolder, SettingFileName);
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

        public void Save(
            AdjustNamespaceSettings settings
            )
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var settingsFilePath = Path.Combine(_solutionFolder, SettingFileName);
            if (File.Exists(settingsFilePath))
            {
                File.Delete(settingsFilePath);
            }

            using (var fs = new FileStream(settingsFilePath, FileMode.Create))
            {
                _serializer.Serialize(
                    fs,
                    settings
                    );
            }
        }
    }
}
