using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

using Siemens.Engineering;

using TIAOpennessAdapter.Models;
using TIAOpennessAdapter.Models.Devices;

namespace TIAOpennessAdapter.Contracts.Services
{
    public interface ITiaPortalService
    {
        List<string>? EngineeringVersions { get; }
        Version? EngineeringVersion { get; }

        public List<CultureInfo>? ProjectLanguages { get; }
        public CultureInfo? ReferenceLanguage { get; }

        string ProjectPath { get; }

        string? ProjectName { get; }

        void Initialize(string engineeringVersion, string opennessVersion);

        void Start();

        List<TiaProcess> GetProcesses();

        void Connect(TiaProcess process);

        void Close();

        List<string>? GetOpennessVersions(string engineeringVersion = "");

        bool OpenProject(string path);

        event EventHandler? ProjectOpened;
        event EventHandler<string>? ExportBlocksEnded;

        List<DeviceComposition>? GetDevices();

        Task ExportAsync(string blockPath);

        public string ExportBlock(Models.Devices.Compositions.Block block, string plc = "", string path = "");

        public string GetAlarmsClassName(Models.Devices.HmiUnifiedDevice hmiUnified, string classname, string tagname, string defaultClassname);

        public void UnifiedTag(HmiUnifiedDevice device, IEnumerable<UnifiedTag> unifiedTags);

        public void UnifiedTagsFolder(UnifiedTagsFolder unifiedTagfsFolder);

        public void UnifiedAlarm(UnifiedAlarm unifiedAlarm);

        public void EndExculsiveAccess();

        public void UnifiedExport(Models.Devices.HmiUnifiedDevice hmiUnified);
    }
}

