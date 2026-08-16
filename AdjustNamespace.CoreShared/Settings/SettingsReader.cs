using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace AdjustNamespace.Settings
{
    /// <summary>
    /// Reader/writer of the settings file which lives in the solution folder.
    /// </summary>
    public class SettingsReader
    {
        /// <summary>
        /// Name of the settings file. It is intended to be committed to the source control.
        /// </summary>
        public const string SettingFileName = "adjust_namespaces_settings.xml";

        private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(AdjustNamespaceSettings));
        private readonly string _solutionFolder;

        /// <param name="solutionFolder">Folder of the solution the settings belong to.</param>
        public SettingsReader(string solutionFolder)
        {
            if (solutionFolder is null)
            {
                throw new ArgumentNullException(nameof(solutionFolder));
            }

            _solutionFolder = solutionFolder;
        }

        /// <summary>
        /// Read the settings of the solution.
        /// </summary>
        /// <returns><c>null</c> if the solution has no settings file yet.</returns>
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
                var result = (AdjustNamespaceSettings?)_serializer.Deserialize(fs);
                return result;
            }
        }

        /// <summary>
        /// Write the settings of the solution (the existing file is overwritten).
        /// </summary>
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
