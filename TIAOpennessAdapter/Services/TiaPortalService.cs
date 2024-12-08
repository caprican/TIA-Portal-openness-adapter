using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Tag;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified.HmiTags;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Blocks.Interface;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;

using TIAOpennessAdapter.Models;
using TIAOpennessAdapter.Models.Devices;
using TIAOpennessAdapter.Utils;

namespace TIAOpennessAdapter.Services
{
    public class TiaPortalService : Contracts.Services.ITiaPortalService
    {
        private readonly List<string>? engineeringVersions;
        private Version? engineeringVersion;
        private FileInfo? projectPath;
        private List<CultureInfo>? projectLanguages;
        private CultureInfo? referenceLanguage;

        private const string userFolder = "UserFiles";

        public List<string>? EngineeringVersions => engineeringVersions;

        private TiaPortal? TiaPortal { get; set; }
        private Project? Project { get; set; }
        private MultiuserProject? MultiuserProject { get; set; }

        public List<PlcSoftware>? PlcSoftwares { get; set; }

        public Version? EngineeringVersion => engineeringVersion;

        public List<CultureInfo>? ProjectLanguages => projectLanguages;

        public CultureInfo? ReferenceLanguage => referenceLanguage;

        public string ProjectPath => Project?.Path.DirectoryName ?? MultiuserProject?.Path.DirectoryName ?? string.Empty;
        public string? ProjectName => Project?.Name ?? MultiuserProject?.Name;

        public event EventHandler? ProjectOpened;
        public event EventHandler<string>? ExportBlocksEnded;

        public TiaPortalService()
        {
            engineeringVersions = Resolver.GetEngineeringVersions();
        }

        public void Initialize(string engineeringVersion, string opennessVersion)
        {
            if (Resolver.GetAssemblyPath(engineeringVersion, opennessVersion) != null)
            {
                this.engineeringVersion = new Version(engineeringVersion);
            }
            else
            {
                this.engineeringVersion = null;
            }

        }

        public List<string>? GetOpennessVersions(string? engineeringVersion = null)
        {
            var engineeringVersionResolver = EngineeringVersion?.ToString();
            if (!string.IsNullOrEmpty(engineeringVersion) || !string.IsNullOrEmpty(engineeringVersionResolver))
            {
                return Resolver.GetAssemblies(engineeringVersion ?? engineeringVersionResolver);
            }

            return null;
        }

        public void Start()
        {
            TiaPortal ??= new TiaPortal(TiaPortalMode.WithUserInterface);

            TiaPortal.Confirmation += TiaPortal_Confirmation;
            TiaPortal.Authentication += TiaPortal_Authentication;
            TiaPortal.Notification += TiaPortal_Notification;
            TiaPortal.Disposed += TiaPortal_Disposed;
        }

        public List<TiaProcess> GetProcesses()
        {
            var tiaProcesses = new List<TiaProcess>();
            foreach (var process in Siemens.Engineering.TiaPortal.GetProcesses())
            {
                tiaProcesses.Add(new TiaProcess(process.Id, process.ProjectPath));
            }
            return tiaProcesses;
        }

        public void Connect(TiaProcess process)
        {
            TiaPortal = Siemens.Engineering.TiaPortal.GetProcess(process.Id).Attach();

            Project = TiaPortal.Projects.FirstOrDefault(f => f.IsPrimary);
            MultiuserProject = TiaPortal?.LocalSessions.FirstOrDefault(p => p.Project.IsPrimary)?.Project;

            projectPath = Project?.Path ?? MultiuserProject?.Path;

            referenceLanguage = Project?.LanguageSettings.ReferenceLanguage.Culture ?? MultiuserProject?.LanguageSettings.ReferenceLanguage.Culture ?? null;
            if (Project?.LanguageSettings?.ActiveLanguages != null)
            {
                foreach (var lang in Project.LanguageSettings.ActiveLanguages)
                {
                    projectLanguages ??= new List<CultureInfo>();
                    projectLanguages.Add(lang.Culture);
                }
            }
            else if (MultiuserProject?.LanguageSettings?.ActiveLanguages != null)
            {
                foreach (var lang in MultiuserProject.LanguageSettings.ActiveLanguages)
                {
                    projectLanguages ??= new List<CultureInfo>();
                    projectLanguages.Add(lang.Culture);
                }
            }

            if(Project != null || MultiuserProject != null) ProjectOpened?.Invoke(this, EventArgs.Empty);
        }

        public List<Models.Devices.DeviceComposition>? GetDevices()
        {
            List<Models.Devices.DeviceComposition>? devices = null;
            if (Project != null)
            {
                if(Project.Devices != null && Project.Devices.Count > 0)
                {
                    devices ??= new List<Models.Devices.DeviceComposition>();
                    devices.AddRange(GetProjectDevices(Project.Devices));
                }

                if (Project.DeviceGroups != null && Project.DeviceGroups.Count > 0)
                {
                    var groups = Project.DeviceGroups.ToList();
                    do
                    {
                        if (GetProjectDevices(groups[0].Devices) is List<Models.Devices.DeviceComposition> tempDevices)
                        {
                            devices ??= new List<Models.Devices.DeviceComposition>();
                            devices.AddRange(tempDevices);
                        }

                        groups.AddRange(groups[0].Groups);

                        groups.RemoveAt(0);
                    } while (groups.Any());
                }

                if (GetProjectDevices(Project.UngroupedDevicesGroup.Devices) is List<Models.Devices.DeviceComposition> tempUngroupDevices)
                {
                    devices ??= new List<Models.Devices.DeviceComposition>();
                    devices.AddRange(tempUngroupDevices);
                }
            }
            else if (MultiuserProject != null)
            {
                if (MultiuserProject.Devices != null && MultiuserProject.Devices.Count > 0)
                {
                    devices ??= new List<Models.Devices.DeviceComposition>();
                    devices.AddRange(GetProjectDevices(MultiuserProject.Devices));
                }

                if (MultiuserProject.DeviceGroups != null && MultiuserProject.DeviceGroups.Count > 0)
                {
                    var groups = MultiuserProject.DeviceGroups.ToList();
                    do
                    {
                        if (GetProjectDevices(groups[0].Devices) is List<Models.Devices.DeviceComposition> tempDevices)
                        {
                            devices ??= new List<Models.Devices.DeviceComposition>();
                            devices.AddRange(tempDevices);
                        }

                        groups.AddRange(groups[0].Groups);

                        groups.RemoveAt(0);
                    } while (groups.Any());
                }

                if (GetProjectDevices(MultiuserProject.UngroupedDevicesGroup.Devices) is List<Models.Devices.DeviceComposition> tempUngroupDevices)
                {
                    devices ??= new List<Models.Devices.DeviceComposition>();
                    devices.AddRange(tempUngroupDevices);
                }
            }




            return devices;
        }

        private List<Models.Devices.DeviceComposition>? GetProjectDevices(Siemens.Engineering.HW.DeviceComposition devices)
        {
            List<Models.Devices.DeviceComposition>? composition = null;

            foreach (var device in devices)
            {
                foreach (var item in device.DeviceItems)
                {

                    if (item.GetService<SoftwareContainer>() is SoftwareContainer software)
                        switch(software.Software)
                        {
                            case PlcSoftware plcSoftware:
                                var PlcComposition = new Models.Devices.PlcDevice(plcSoftware);
                                
                                PlcSoftwares ??= new List<PlcSoftware>();
                                PlcSoftwares.Add(plcSoftware);

                                //composition ??= [];
                                //composition.Add(new Models.DeviceComposition
                                //{
                                //    PlcName = $"{item.Name} []",
                                //    Path = $"{item.Name}"
                                //});

                                if (GetPlcBlocks(plcSoftware.BlockGroup.Blocks, @$"{item.Name}") is List<Models.Devices.Compositions.Block> blocks)
                                {
                                    PlcComposition.Items ??= new List<Models.Devices.DeviceComposition>();
                                    PlcComposition.Items.AddRange(blocks);

                                    PlcComposition.Blocks ??= new List<Siemens.Engineering.SW.Blocks.PlcBlock>();
                                    PlcComposition.Blocks.AddRange(blocks.Select(s => s.plcBlock));
                                }
                                if (GetPlcBlocksGroups(plcSoftware.BlockGroup.Groups, @$"{item.Name}") is List<Models.Devices.Compositions.Block> blockGroups)
                                {
                                    PlcComposition.Items ??= new List<Models.Devices.DeviceComposition>();
                                    PlcComposition.Items.AddRange(blockGroups);

                                    PlcComposition.Blocks ??= new List<Siemens.Engineering.SW.Blocks.PlcBlock>();
                                    PlcComposition.Blocks.AddRange(blockGroups.Select(s => s.plcBlock));
                                }

                                if (GetPlcTypes(plcSoftware.TypeGroup.Types, @$"{item.Name}") is List<Models.Devices.DeviceComposition> types)
                                {
                                    PlcComposition.Items ??= new List<Models.Devices.DeviceComposition>();
                                    PlcComposition.Items.AddRange(types);
                                }
                                if (GetPlcTypesGroups(plcSoftware.TypeGroup.Groups, @$"{item.Name}") is List<Models.Devices.DeviceComposition> typeGroups)
                                {
                                    PlcComposition.Items ??= new List<Models.Devices.DeviceComposition>();
                                    PlcComposition.Items.AddRange(typeGroups);
                                }

                                if (GetPlcTags(plcSoftware.TagTableGroup.TagTables, @$"{item.Name}") is List<Models.Devices.DeviceComposition> tags)
                                {
                                    PlcComposition.Items ??= new List<Models.Devices.DeviceComposition>();
                                    PlcComposition.Items.AddRange(tags);
                                }
                                if (GetPlcTagsGroups(plcSoftware.TagTableGroup.Groups, @$"{item.Name}") is List<Models.Devices.DeviceComposition> tagGroups)
                                {
                                    PlcComposition.Items ??= new List<Models.Devices.DeviceComposition>();
                                    PlcComposition.Items.AddRange(tagGroups);
                                }

                                composition ??= new List<Models.Devices.DeviceComposition>();
                                composition.Add(PlcComposition);
                                
                                break;
                            case HmiTarget hmi:
                                var HmiComposition = new Models.Devices.HmiDevice(hmi.Name);

                                composition ??= new List<Models.Devices.DeviceComposition>();
                                composition.Add(HmiComposition);
                                break;
                            case HmiSoftware unifiedHmi:
                                var unifiedComposition = new Models.Devices.HmiUnifiedDevice(unifiedHmi);
                                
                                composition ??= new List<Models.Devices.DeviceComposition>();
                                composition.Add(unifiedComposition);
                                break;
                            default:
                                composition ??= new List<Models.Devices.DeviceComposition>();
                                composition.Add(new Models.Device(device.Name));
                                break;
                        }
                }
            }

            return composition;
        }

        private List<Models.Devices.Compositions.Block>? GetPlcBlocks(PlcBlockComposition plcBlocks, string parentPath)
        {
            List<Models.Devices.Compositions.Block>? blocks = null;
            if (plcBlocks != null)
            {
                blocks = new List<Models.Devices.Compositions.Block>();
                foreach (var block in plcBlocks)
                {
                    Models.Devices.Compositions.Block deviceComposition;
                    switch (block)
                    {
                        case OB _ :
                            deviceComposition = new Models.Devices.Compositions.OB(block)
                            {
                                Path = Path.Combine(parentPath, block.Name),
                                Number = (uint)block.Number,
                            };
                            
                            break;
                        case FC _:
                            deviceComposition = new Models.Devices.Compositions.FC(block)
                            {
                                Path = Path.Combine(parentPath, block.Name),
                                Number = (uint)block.Number,
                            };
                            break;
                        case FB _:
                            deviceComposition = new Models.Devices.Compositions.FB(block)
                            {
                                Path = Path.Combine(parentPath, block.Name),
                                Number = (uint)block.Number,
                            };
                            break;
                        case GlobalDB _:
                            deviceComposition = new Models.Devices.Compositions.GlobalData(block)
                            {
                                Path = Path.Combine(parentPath, block.Name),
                                Number = (uint)block.Number,
                            };
                            break;
                        case InstanceDB _:
                            deviceComposition = new Models.Devices.Compositions.InstanceData(block)
                            {
                                Path = Path.Combine(parentPath, block.Name),
                                Number = (uint)block.Number,
                            };
                            break;
                        default:
                            deviceComposition = new Models.Devices.Compositions.Block(block)
                            {
                                Path = Path.Combine(parentPath, block.Name),
                                Number = (uint)block.Number,
                            };
                            break;
                    }
                    
                    blocks.Add(deviceComposition);
                }
            }

            return blocks;
        }

        private List<Models.Devices.Compositions.Block>? GetPlcBlocksGroups(PlcBlockUserGroupComposition plcBlockGroups, string parentPath)
        {
            List<Models.Devices.Compositions.Block>? groups = null;

            if (plcBlockGroups != null && plcBlockGroups.Count > 0)
            {
                var plcGroup = plcBlockGroups.ToList();
                var groupPath = parentPath;
                do
                {
                    if (GetPlcBlocks(plcGroup[0].Blocks, Path.Combine(groupPath, plcGroup[0].Name)) is List<Models.Devices.Compositions.Block> blocks)
                    {
                        groups ??= new List<Models.Devices.Compositions.Block>();
                        groups.AddRange(blocks);
                    }

                    if (plcGroup[0].Groups != null && plcGroup[0].Groups.Count > 0 &&
                        GetPlcBlocksGroups(plcGroup[0].Groups, Path.Combine(groupPath, plcGroup[0].Name)) is List<Models.Devices.Compositions.Block> lstGroup)
                    {
                        groups ??= new List<Models.Devices.Compositions.Block>();
                        groups.AddRange(lstGroup);
                    }

                    plcGroup.RemoveAt(0);
                } while (plcGroup.Count > 0);
            }

            return groups;
        }

        private List<Models.Devices.DeviceComposition>? GetPlcTypes(PlcTypeComposition plcTypes, string parentPath)
        {
            List<Models.Devices.DeviceComposition>? types = null;
            if (plcTypes != null)
            {
                types = new List<Models.Devices.DeviceComposition>();
                foreach (var block in plcTypes)
                {
                    types.Add(new Models.Devices.Compositions.DataStruct(block)
                    {
                        Path = Path.Combine(parentPath, block.Name),
                    });
                }
            }

            return types;
        }

        private List<Models.Devices.DeviceComposition>? GetPlcTypesGroups(PlcTypeUserGroupComposition plcTypeGroups, string parentPath)
        {
            List<Models.Devices.DeviceComposition>? groups = null;

            if (plcTypeGroups != null && plcTypeGroups.Count > 0)
            {
                var plcGroup = plcTypeGroups.ToList();
                var groupPath = parentPath;
                do
                {
                    if (GetPlcTypes(plcGroup[0].Types, Path.Combine(groupPath, plcGroup[0].Name)) is List<Models.Devices.DeviceComposition> blocks)
                    {
                        groups ??= new List<Models.Devices.DeviceComposition>();
                        groups.AddRange(blocks);
                    }

                    if (plcGroup[0].Groups != null && plcGroup[0].Groups.Count > 0 &&
                        GetPlcTypesGroups(plcGroup[0].Groups, Path.Combine(groupPath, plcGroup[0].Name)) is List<Models.Devices.DeviceComposition> lstGroup)
                    {
                        groups ??= new List<Models.Devices.DeviceComposition>();
                        groups.AddRange(lstGroup);
                    }

                    plcGroup.RemoveAt(0);
                } while (plcGroup.Count > 0);
            }

            return groups;
        }

        private List<Models.Devices.DeviceComposition>? GetPlcTags(PlcTagTableComposition plcTags, string parentPath)
        {
            List<Models.Devices.DeviceComposition>? types = null;
            if (plcTags != null)
            {
                types = new List<Models.Devices.DeviceComposition>();
                foreach (var tagTable in plcTags)
                {
                    types.Add(new Models.Devices.Compositions.Tags(tagTable)
                    {
                        Path = Path.Combine(parentPath, tagTable.Name),
                    });
                }
            }

            return types;
        }

        private List<Models.Devices.DeviceComposition>? GetPlcTagsGroups(PlcTagTableUserGroupComposition plcTagGroups, string parentPath)
        {
            List<Models.Devices.DeviceComposition>? groups = null;

            if (plcTagGroups != null && plcTagGroups.Count > 0)
            {
                var plcGroup = plcTagGroups.ToList();
                var groupPath = parentPath;
                do
                {
                    if (GetPlcTags(plcGroup[0].TagTables, Path.Combine(groupPath, plcGroup[0].Name)) is List<Models.Devices.DeviceComposition> tagTables)
                    {
                        groups ??= new List<Models.Devices.DeviceComposition>();
                        groups.AddRange(tagTables);
                    }

                    if (plcGroup[0].Groups != null && plcGroup[0].Groups.Count > 0 &&
                        GetPlcTagsGroups(plcGroup[0].Groups, Path.Combine(groupPath, plcGroup[0].Name)) is List<Models.Devices.DeviceComposition> tagGroups)
                    {
                        groups ??= new List<Models.Devices.DeviceComposition>();
                        groups.AddRange(tagGroups);
                    }

                    plcGroup.RemoveAt(0);
                } while (plcGroup.Count > 0);
            }

            return groups;
        }

        public void Close()
        {
            if (TiaPortal != null)
            {
                TiaPortal.Confirmation -= TiaPortal_Confirmation;
                TiaPortal.Authentication -= TiaPortal_Authentication;
                TiaPortal.Notification -= TiaPortal_Notification;
                TiaPortal.Disposed -= TiaPortal_Disposed;

                TiaPortal.Dispose();
                TiaPortal = null;
            }
        }

        private void TiaPortal_Disposed(object sender, System.EventArgs e)
        {
            if (TiaPortal != null)
            {
                TiaPortal.Confirmation -= TiaPortal_Confirmation;
                TiaPortal.Authentication -= TiaPortal_Authentication;
                TiaPortal.Notification -= TiaPortal_Notification;
                TiaPortal.Disposed -= TiaPortal_Disposed;

                TiaPortal.Dispose();
                TiaPortal = null;
            }
        }

        private void TiaPortal_Notification(object sender, NotificationEventArgs e)
        {
    
        }

        private void TiaPortal_Authentication(object sender, AuthenticationEventArgs e)
        {
    
        }

        private void TiaPortal_Confirmation(object sender, ConfirmationEventArgs e)
        {
    
        }

        public bool OpenProject(string path)
        {
            if (projectPath is null)
            {
                projectPath = new FileInfo(path);

                if (EngineeringVersion is null) return false;

                var version = EngineeringVersion.Major.ToString();
                if (EngineeringVersion.Minor > 0)
                {
                    version += $"_{EngineeringVersion.Minor}";
                }

                switch(projectPath.Extension.Replace(version, ""))
                {
                    case ".ap":
                        Project = TiaPortal?.Projects.Open(projectPath);
                        break;
                    case ".als":

                        if(TiaPortal?.ProjectServers != null)
                        {
                            foreach (var server in TiaPortal?.ProjectServers)
                            {
                                
                                foreach (var project in server.GetServerProjects())
                                {
                                    server.GetLocalSessions(project);
                                    
                                    
                                }
                            }

                        }

                        

                        break;
                    case ".amc":
                        var p = TiaPortal?.LocalSessions.OpenServerProject(projectPath);
                        break;
                }


                referenceLanguage = Project?.LanguageSettings.ReferenceLanguage.Culture;
                if (Project?.LanguageSettings?.ActiveLanguages != null)
                {
                    foreach (var lang in Project.LanguageSettings.ActiveLanguages)
                    {
                        projectLanguages ??= new List<CultureInfo>();
                        projectLanguages.Add(lang.Culture);
                    }
                }

                ProjectOpened?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return false;
        }

        //public void GetProjects()
        //{        
        //    TiaPortalSettingsFolder? generalSettingsFolder = TiaPortal?.SettingsFolders.Find("General");
        //    var a = generalSettingsFolder.Settings.ToList();
        //}

        public async Task ExportAsync(string blockPath)
        {
            await Task.Run(() =>
            {
                var paths = blockPath.Split(Path.DirectorySeparatorChar);
                var indPath = 0;
                var pathBuild = "Exports";
                List<string> exportPaths = new List<string>();
                if (PlcSoftwares != null && projectPath != null)
                {
                    var plc = PlcSoftwares.Find(f => f.Name == paths[indPath]);
                    Siemens.Engineering.SW.Blocks.PlcBlock? block = null;
                    PlcBlockUserGroup? group = null;
                    indPath++;
                    if (paths.Length == 2)
                    {
                        block = plc.BlockGroup.Blocks.Find(paths[indPath]);
                        if (block is null)
                        {
                            group = plc.BlockGroup.Groups.Find(paths[indPath]);
                            pathBuild += @$"\{paths[indPath]}";
                        }
                    }
                    else if (paths.Length > 2)
                    {
                        var navGroup = plc.BlockGroup.Groups.Find(paths[indPath]);
                        indPath++;
                        for (; indPath < paths.Length - 1; indPath++)
                        {
                            navGroup = navGroup.Groups.Find(paths[indPath]);
                            pathBuild += @$"\{paths[indPath]}";
                        }

                        block = navGroup.Blocks.Find(paths[indPath]);
                        if (block is null)
                        {
                            group = navGroup.Groups.Find(paths[indPath]);
                            pathBuild += @$"\{paths[indPath]}";
                        }
                    }

                    var exportPath = Path.Combine(projectPath.DirectoryName, "UserFiles", pathBuild);
                    CreateDirectoryIfNotExists(exportPath);

                    if (block != null)
                    {
                        exportPaths.Add(ExportBlock($@"{exportPath}\{block.Name}.xml", block));
                    }
                    else if (group != null)
                    {
                        foreach (var groupBlock in group.Blocks)
                        {
                            exportPaths.Add(ExportBlock($@"{exportPath}\{groupBlock.Name}.xml", groupBlock));
                        }
                        exportPaths.AddRange(ExportGroup(group, exportPath));
                    }
                }
            });
        }

        private void CreateDirectoryIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private List<string> ExportGroup(PlcBlockUserGroup plcGroup, string path)
        {
            var tempGroups = new List<(string path, PlcBlockUserGroup group)>();
            List<string> exportPaths = new List<string>();
            foreach (var group in plcGroup.Groups)
            {
                tempGroups.Add(($@"{path}\{group.Name}", group));
            }

            if (tempGroups.Count > 0)
            {
                do
                {
                    CreateDirectoryIfNotExists(tempGroups[0].path);

                    foreach (var block in tempGroups[0].group.Blocks)
                    {
                        exportPaths.Add(ExportBlock($@"{tempGroups[0].path}\{block.Name}.xml", block));
                    }

                    foreach (var group in tempGroups[0].group.Groups)
                    {
                        tempGroups.Add(($@"{tempGroups[0].path}\{group.Name}" , group));
                    }
                    tempGroups.RemoveAt(0);

                } while (tempGroups.Count > 0);
            }
            return exportPaths;
        }

        public string ExportBlock(Models.Devices.Compositions.Block block, string plc = "", string path = "")
        {
            if(string.IsNullOrEmpty(path))
            {
                path = Path.Combine(ProjectPath, userFolder, plc + block.Name + ".xml");
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            block.plcBlock.Export(new FileInfo(path), ExportOptions.None, DocumentInfoOptions.None);
            ExportBlocksEnded?.Invoke(this, path);

            return path;
        }

        private string ExportBlock(string path, PlcBlock block)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            block.Export(new FileInfo(path), ExportOptions.None);
            ExportBlocksEnded?.Invoke(this, path);

            return path;
        }

        public string GetAlarmsClassName(Models.Devices.HmiUnifiedDevice hmiUnified, string classname, string tagname, string defaultClassname)
        {
            if (hmiUnified.Device.AlarmClasses.Find(classname) is HmiAlarmClass groupAlarmClass)
            {
                return groupAlarmClass.Name;
            }
            else if (hmiUnified.Device.AlarmClasses.Find(tagname) is HmiAlarmClass groupAlarmClass2)
            {
                return groupAlarmClass2.Name;
            }
            else
                return hmiUnified.Device.AlarmClasses.Find(defaultClassname).Name;
        }

        public void UnifiedExport(Models.Devices.HmiUnifiedDevice hmiUnified)
        {
            var rst = hmiUnified.Device.TagTableGroups.First(f => f.Name == "N100").TagTables.First(f => f.Name == "100").Tags.Export(new DirectoryInfo(@"C:\Users\capri\Desktop\test"), "DB100.xml");

            foreach (var f in rst)
            {

            }
        }

        public void UnifiedTag(UnifiedTag unifiedTag)
        {
            ExclusiveAccess ??= TiaPortal?.ExclusiveAccess();
            ExclusiveAccess!.Text = $"Rebuild tag {unifiedTag.Tagname}";
            using (var transact = ExclusiveAccess?.Transaction(Project, $"Build {unifiedTag.Tagname}"))
            {
                var plcTag = UnifiedPlcTagNormalized(unifiedTag.PlcTag);

                var hmiTags = unifiedTag.Hmi.Device.Tags.Where(c => c.PlcTag == plcTag);
                foreach (var hmiTag in hmiTags)
                {
                    var tempAlarms = unifiedTag.Hmi.Device.DiscreteAlarms.Where(w => w.RaisedStateTag == hmiTag.Name).Select(s => s.Name);
                    foreach (var alarm in tempAlarms)
                    {
                        unifiedTag.Hmi.Device.DiscreteAlarms.Single(s => s.Name == alarm)?.Delete();
                    }

                    if(hmiTags.Count() > 1)
                    {
                        hmiTag.Delete();
                    }
                }

                HmiTag tag;
                if(!unifiedTag.Hmi.Device.Tags.Any(c => c.PlcTag == plcTag))
                {
                    if (string.IsNullOrEmpty(unifiedTag.Folder))
                        tag = unifiedTag.Hmi.Device.Tags.Create(unifiedTag.Tagname);
                    else
                        tag = unifiedTag.Hmi.Device.Tags.Create(unifiedTag.Tagname, unifiedTag.Folder);
                }
                else
                {
                    tag = unifiedTag.Hmi.Device.Tags.Single(c => c.PlcTag == plcTag);
                }
                tag.Connection = unifiedTag.Hmi.Device.Connections.Single(s => s.Partner == unifiedTag.Connexion).Name;
                tag.PlcTag = plcTag;
                tag.Name = unifiedTag.Tagname;

                transact?.CommitOnDispose();
            }
        }

        public void UnifiedTag(HmiUnifiedDevice device, IEnumerable<UnifiedTag> unifiedTags)
        {
            ExclusiveAccess ??= TiaPortal?.ExclusiveAccess();
            var index = 0;
            var plcTagsUpadted = new List<string>();
            var plcTagsAlarmRemove = new List<string>();
            do
            {
                var plcTag = device.Device.Tags[index].PlcTag;

                if(plcTagsUpadted.Contains(plcTag))
                {
                    plcTagsAlarmRemove.Add(plcTag);
                    ExclusiveAccess!.Text = $"Delet tag {plcTag} ({index}/{device.Device.Tags.Count})";
                    using var transact = ExclusiveAccess!.Transaction(Project, $"Delet {plcTag}");
                    device.Device.Tags[index].Delete();

                    transact.CommitOnDispose();
                }
                else if(unifiedTags.Any(a => a.PlcTag == plcTag) && !plcTagsUpadted.Contains(plcTag))
                {
                    plcTagsUpadted.Add(plcTag);
                    ExclusiveAccess!.Text = $"Rename tag {plcTag} ({index}/{device.Device.Tags.Count})";
                    using var transact = ExclusiveAccess!.Transaction(Project, $"Build {plcTag}");

                    device.Device.Tags[index].Name = UnifiedPlcTagNormalized(unifiedTags.Single(a => a.PlcTag == plcTag).Tagname);
                    //device.Device.Tags[index].TagTableName = unifiedTags.Single(a => a.PlcTag == plcTag).Folder;
                    index++;

                    transact.CommitOnDispose();
                }
                else
                {
                    ExclusiveAccess!.Text = $"Valid tag {plcTag} ({index}/{device.Device.Tags.Count})";
                    index++;
                }
            } while (index < device.Device.Tags.Count);

            foreach (var tag in unifiedTags)
            {
                if(!plcTagsUpadted.Contains(tag.PlcTag))
                {
                    HmiTag hmiTag;
                    if (string.IsNullOrEmpty(tag.Folder))
                        hmiTag = device.Device.Tags.Create(UnifiedPlcTagNormalized(tag.Tagname));
                    else
                        hmiTag = device.Device.Tags.Create(UnifiedPlcTagNormalized(tag.Tagname), tag.Folder);

                    hmiTag.Connection = device.Device.Connections.Single(s => s.Partner == tag.Connexion).Name;
                    hmiTag.PlcTag = tag.PlcTag;
                }
            }


        }

        private string UnifiedPlcTagNormalized(string tagname)
        {
            var compose = tagname.Split('.');
            string plcTag = compose.FirstOrDefault();
            foreach (var item in compose.Skip(1))
            {
                if (item.Contains(" "))
                    plcTag += $".\"{item}\"";
                else
                    plcTag += $".{item}";
            }

            return plcTag;
        }

        public void UnifiedTagsFolder(UnifiedTagsFolder unifiedTagsFolder)
        {
            var tagTables = new List<HmiTagTable>();
            tagTables.AddRange(unifiedTagsFolder.Hmi.Device.TagTables);

            var tagTablesGroup = new List<HmiTagTableGroup>();
            tagTablesGroup.AddRange(unifiedTagsFolder.Hmi.Device.TagTableGroups);
            do
            {
                if (tagTablesGroup.Count > 0)
                {
                    tagTables.AddRange(tagTablesGroup[0].TagTables);
                    tagTablesGroup.AddRange(tagTablesGroup[0].Groups);
                    tagTablesGroup.RemoveAt(0);
                }
            } while (tagTablesGroup.Count > 0);

            if (!tagTables.Any(a => a.Name == unifiedTagsFolder.Name))
                unifiedTagsFolder.Hmi.Device.TagTables.Create(unifiedTagsFolder.Name);
        }

        public void UnifiedAlarm(UnifiedAlarm unifiedAlarm)
        {
            ExclusiveAccess ??= TiaPortal?.ExclusiveAccess();
            ExclusiveAccess!.Text = $"Rebuild alarm {unifiedAlarm.Tagname}";
            using (var transact = ExclusiveAccess?.Transaction((ITransactionSupport)Project ?? (ITransactionSupport)MultiuserProject, $"Build {unifiedAlarm.Tagname}"))
            {
                var alarms = unifiedAlarm.Hmi.Device.DiscreteAlarms.Find(unifiedAlarm.Tagname) ?? unifiedAlarm.Hmi.Device.DiscreteAlarms.Create(unifiedAlarm.Tagname);
                alarms.RaisedStateTag = unifiedAlarm.Tagname;
                alarms.AlarmClass = unifiedAlarm.Hmi.Device.AlarmClasses.Single(s => s.Name == unifiedAlarm.ClassName)?.Name;
                alarms.Origin = unifiedAlarm.Origin;

                if (ProjectLanguages != null)
                {
                    foreach (var lang in ProjectLanguages)
                    {
                        if (alarms.EventText.Items.Single(s => s.Language.Culture.Name == lang.Name) is MultilingualTextItem multilingualText)
                        {
                            unifiedAlarm.Descriptions.TryGetValue(lang.Name, out string txt);
                            multilingualText.Text = $"<body><p>{txt}</p></body>";
                        }
                    }
                }
                transact?.CommitOnDispose();
            }
        }

        public void EndExculsiveAccess()
        {
            ExclusiveAccess?.Dispose();
        }

        public ExclusiveAccess? ExclusiveAccess { get; set; }
        
    }
}