﻿using BluePointLilac.Controls;
using BluePointLilac.Methods;
using ContextMenuManager.Controls.Interfaces;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace ContextMenuManager.Controls
{
    sealed class ShellList : MyList
    {
        public const string MENUPATH_FILE = @"HKEY_CLASSES_ROOT\*";//文件
        public const string MENUPATH_FOLDER = @"HKEY_CLASSES_ROOT\Folder";//文件夹
        public const string MENUPATH_DIRECTORY = @"HKEY_CLASSES_ROOT\Directory";//目录
        public const string MENUPATH_BACKGROUND = @"HKEY_CLASSES_ROOT\Directory\Background";//目录背景
        public const string MENUPATH_DESKTOP = @"HKEY_CLASSES_ROOT\DesktopBackground";//桌面背景
        public const string MENUPATH_DRIVE = @"HKEY_CLASSES_ROOT\Drive";//磁盘分区
        public const string MENUPATH_ALLOBJECTS = @"HKEY_CLASSES_ROOT\AllFilesystemObjects";//所有对象
        public const string MENUPATH_COMPUTER = @"HKEY_CLASSES_ROOT\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}";//此电脑
        public const string MENUPATH_RECYCLEBIN = @"HKEY_CLASSES_ROOT\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}";//回收站
        public const string MENUPATH_LIBRARY = @"HKEY_CLASSES_ROOT\LibraryFolder";//库
        public const string MENUPATH_LIBRARY_BACKGROUND = @"HKEY_CLASSES_ROOT\LibraryFolder\Background";//库背景
        public const string MENUPATH_LIBRARY_USER = @"HKEY_CLASSES_ROOT\UserLibraryFolder";//用户库
        public const string MENUPATH_UWPLNK = @"HKEY_CLASSES_ROOT\Launcher.ImmersiveApplication";//UWP快捷方式
        public const string MENUPATH_UNKNOWN = @"HKEY_CLASSES_ROOT\Unknown";//未知格式
        public const string SYSFILEASSPATH = @"HKEY_CLASSES_ROOT\SystemFileAssociations";//系统扩展名注册表父项路径
        private const string LASTKEYPATH = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit";//上次打开的注册表项路径记录

        public enum Scenes
        {
            File, Folder, Directory, Background, Desktop, Drive, AllObjects, Computer, RecycleBin, Library,
            LnkFile, UwpLnk, ExeFile, UnknownType, CustomExtension, PerceivedType, DirectoryType,
            CommandStore, DragDrop, CustomRegPath, MenuAnalysis
        }

        private static readonly string[] DirectoryTypes = { "Document", "Image", "Video", "Audio" };
        private static readonly string[] PerceivedTypes = { null, "Text", "Document", "Image", "Video", "Audio", "Compressed", "System" };
        private static readonly string[] FileObjectTypes = { AppString.SideBar.File, AppString.SideBar.Directory };
        private static readonly string[] DirectoryTypeNames =
        {
            AppString.Dialog.DocumentDirectory, AppString.Dialog.ImageDirectory,
            AppString.Dialog.VideoDirectory, AppString.Dialog.AudioDirectory
        };
        private static readonly string[] PerceivedTypeNames =
        {
            AppString.Dialog.NoPerceivedType, AppString.Dialog.TextFile, AppString.Dialog.DocumentFile, AppString.Dialog.ImageFile,
            AppString.Dialog.VideoFile, AppString.Dialog.AudioFile, AppString.Dialog.CompressedFile, AppString.Dialog.SystemFile
        };

        private static string GetDirectoryTypeName(string directoryType)
        {
            if(directoryType != null)
            {
                for(int i = 0; i < DirectoryTypes.Length; i++)
                {
                    if(directoryType.Equals(DirectoryTypes[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return DirectoryTypeNames[i];
                    }
                }
            }
            return null;
        }

        private static string GetPerceivedTypeName(string perceivedType)
        {
            int index = 0;
            if(perceivedType != null)
            {
                for(int i = 1; i < PerceivedTypes.Length; i++)
                {
                    if(perceivedType.Equals(PerceivedTypes[i], StringComparison.OrdinalIgnoreCase)) index = i;
                }
            }
            return PerceivedTypeNames[index];
        }

        private static string CurrentExtension = null;
        private static string CurrentDirectoryType = null;
        private static string CurrentPerceivedType = null;
        public static string CurrentCustomRegPath = null;
        public static string CurrentFileObjectPath = null;

        private static string GetShellPath(string scenePath) => $@"{scenePath}\shell";
        private static string GetShellExPath(string scenePath) => $@"{scenePath}\ShellEx";
        private static string GetSysAssExtPath(string typeName) => typeName != null ? $@"{SYSFILEASSPATH}\{typeName}" : null;
        private static string GetOpenModePath(string extension) => extension != null ? $@"{RegistryEx.CLASSESROOT}\{FileExtension.GetOpenMode(extension)}" : null;

        public Scenes Scene { get; set; }

        public ShellList()
        {
            SelectItem.SelectedChanged += (sender, e) => { this.ClearItems(); this.LoadItems(); };
        }

        public void LoadItems()
        {
            string scenePath = null;
            switch(Scene)
            {
                case Scenes.File:
                    scenePath = MENUPATH_FILE; break;
                case Scenes.Folder:
                    scenePath = MENUPATH_FOLDER; break;
                case Scenes.Directory:
                    scenePath = MENUPATH_DIRECTORY; break;
                case Scenes.Background:
                    scenePath = MENUPATH_BACKGROUND; break;
                case Scenes.Desktop:
                    //Vista系统没有这一项
                    if(WindowsOsVersion.IsEqualVista) return;
                    scenePath = MENUPATH_DESKTOP; break;
                case Scenes.Drive:
                    scenePath = MENUPATH_DRIVE; break;
                case Scenes.AllObjects:
                    scenePath = MENUPATH_ALLOBJECTS; break;
                case Scenes.Computer:
                    scenePath = MENUPATH_COMPUTER; break;
                case Scenes.RecycleBin:
                    scenePath = MENUPATH_RECYCLEBIN; break;
                case Scenes.Library:
                    //Vista系统没有这一项
                    if(WindowsOsVersion.IsEqualVista) return;
                    scenePath = MENUPATH_LIBRARY; break;
                case Scenes.LnkFile:
                    scenePath = GetOpenModePath(".lnk"); break;
                case Scenes.UwpLnk:
                    //Win8之前没有Uwp
                    if(WindowsOsVersion.IsBefore8) return;
                    scenePath = MENUPATH_UWPLNK; break;
                case Scenes.ExeFile:
                    scenePath = GetSysAssExtPath(".exe"); break;
                case Scenes.UnknownType:
                    scenePath = MENUPATH_UNKNOWN; break;
                case Scenes.CustomExtension:
                    bool isLnk = CurrentExtension?.ToLower() == ".lnk";
                    if(isLnk) scenePath = GetOpenModePath(".lnk");
                    else scenePath = GetSysAssExtPath(CurrentExtension);
                    break;
                case Scenes.PerceivedType:
                    scenePath = GetSysAssExtPath(CurrentPerceivedType); break;
                case Scenes.DirectoryType:
                    if(CurrentDirectoryType == null) scenePath = null;
                    else scenePath = GetSysAssExtPath($"Directory.{CurrentDirectoryType}"); break;
                case Scenes.MenuAnalysis:
                    this.AddItem(new SelectItem(Scene));
                    this.LoadAnalysisItems();
                    return;
                case Scenes.CustomRegPath:
                    scenePath = CurrentCustomRegPath; break;
                case Scenes.CommandStore:
                    //Vista系统没有这一项
                    if(WindowsOsVersion.IsEqualVista) return;
                    this.AddNewItem(RegistryEx.GetParentPath(ShellItem.CommandStorePath));
                    this.LoadStoreItems();
                    return;
                case Scenes.DragDrop:
                    this.AddNewItem(MENUPATH_FOLDER);
                    this.LoadShellExItems(GetShellExPath(MENUPATH_FOLDER));
                    this.LoadShellExItems(GetShellExPath(MENUPATH_DIRECTORY));
                    this.LoadShellExItems(GetShellExPath(MENUPATH_DRIVE));
                    this.LoadShellExItems(GetShellExPath(MENUPATH_ALLOBJECTS));
                    return;
            }
            this.AddNewItem(scenePath);
            this.LoadItems(scenePath);
            if(WindowsOsVersion.ISAfterOrEqual10) this.AddUwpModeItem();
            switch(Scene)
            {
                case Scenes.Background:
                    this.AddItem(new VisibleRegRuleItem(VisibleRegRuleItem.CustomFolder));
                    break;
                case Scenes.Computer:
                    this.AddItem(new VisibleRegRuleItem(VisibleRegRuleItem.NetworkDrive));
                    break;
                case Scenes.RecycleBin:
                    this.AddItem(new VisibleRegRuleItem(VisibleRegRuleItem.RecycleBinProperties));
                    break;
                case Scenes.Library:
                    this.LoadItems(MENUPATH_LIBRARY_BACKGROUND);
                    this.LoadItems(MENUPATH_LIBRARY_USER);
                    break;
                case Scenes.ExeFile:
                    this.LoadItems(GetOpenModePath(".exe"));
                    break;
                case Scenes.CustomExtension:
                case Scenes.PerceivedType:
                case Scenes.DirectoryType:
                case Scenes.CustomRegPath:
                    this.InsertItem(new SelectItem(Scene), 0);
                    if(Scene == Scenes.CustomExtension)
                    {
                        this.LoadItems(GetOpenModePath(CurrentExtension));
                        if(CurrentExtension != null) this.InsertItem(new PerceivedTypeItem(), 1);
                    }
                    break;
            }
        }

        private void LoadItems(string scenePath)
        {
            if(scenePath == null) return;
            RegTrustedInstaller.TakeRegKeyOwnerShip(scenePath);
            this.LoadShellItems(GetShellPath(scenePath));
            this.LoadShellExItems(GetShellExPath(scenePath));
        }

        private void LoadShellItems(string shellPath)
        {
            using(RegistryKey shellKey = RegistryEx.GetRegistryKey(shellPath))
            {
                if(shellKey == null) return;
                RegTrustedInstaller.TakeRegTreeOwnerShip(shellKey.Name);
                foreach(string keyName in shellKey.GetSubKeyNames())
                {
                    this.AddItem(new ShellItem($@"{shellPath}\{keyName}"));
                }
            }
        }

        private void LoadShellExItems(string shellExPath)
        {
            List<string> names = new List<string>();
            using(RegistryKey shellExKey = RegistryEx.GetRegistryKey(shellExPath))
            {
                if(shellExKey == null) return;
                bool isDragDrop = Scene == Scenes.DragDrop;
                RegTrustedInstaller.TakeRegTreeOwnerShip(shellExKey.Name);
                Dictionary<string, Guid> dic = ShellExItem.GetPathAndGuids(shellExPath, isDragDrop);
                GroupPathItem groupItem = null;
                if(isDragDrop)
                {
                    groupItem = GetDragDropGroupItem(shellExPath);
                    this.AddItem(groupItem);
                }
                foreach(string path in dic.Keys)
                {
                    string keyName = RegistryEx.GetKeyName(path);
                    if(!names.Contains(keyName))
                    {
                        ShellExItem item = new ShellExItem(dic[path], path);
                        if(groupItem != null) item.FoldGroupItem = groupItem;
                        this.AddItem(item);
                        names.Add(keyName);
                    }
                }
                if(groupItem != null) groupItem.IsFold = true;
            }
        }

        private GroupPathItem GetDragDropGroupItem(string shellExPath)
        {
            string text = null;
            Image image = null;
            string path = shellExPath.Substring(0, shellExPath.LastIndexOf('\\'));
            switch(path)
            {
                case MENUPATH_FOLDER:
                    text = AppString.SideBar.Folder;
                    image = AppImage.Folder;
                    break;
                case MENUPATH_DIRECTORY:
                    text = AppString.SideBar.Directory;
                    image = AppImage.Directory;
                    break;
                case MENUPATH_DRIVE:
                    text = AppString.SideBar.Drive;
                    image = AppImage.Drive;
                    break;
                case MENUPATH_ALLOBJECTS:
                    text = AppString.SideBar.AllObjects;
                    image = AppImage.AllObjects;
                    break;
            }
            return new GroupPathItem(shellExPath, ObjectPath.PathType.Registry) { Text = text, Image = image };
        }

        private void AddNewItem(string scenePath)
        {
            NewItem newItem = new NewItem { Visible = scenePath != null };
            this.AddItem(newItem);
            newItem.AddNewItem += (sender, e) =>
            {
                bool isShell;
                if(Scene == Scenes.CommandStore) isShell = true;
                else if(Scene == Scenes.DragDrop) isShell = false;
                else
                {
                    using(SelectDialog dlg = new SelectDialog())
                    {
                        dlg.Items = new[] { "Shell", "ShellEx" };
                        dlg.Title = AppString.Dialog.SelectNewItemType;
                        if(dlg.ShowDialog() != DialogResult.OK) return;
                        isShell = dlg.SelectedIndex == 0;
                    }
                }
                if(isShell) this.AddNewShellItem(scenePath);
                else this.AddNewShellExItem(scenePath);
            };
        }

        private void AddNewShellItem(string scenePath)
        {
            string shellPath = GetShellPath(scenePath);
            using(NewShellDialog dlg = new NewShellDialog())
            {
                dlg.ScenePath = scenePath;
                dlg.ShellPath = shellPath;
                if(dlg.ShowDialog() != DialogResult.OK) return;
                for(int i = 0; i < this.Controls.Count; i++)
                {
                    if(this.Controls[i] is NewItem)
                    {
                        ShellItem item;
                        if(Scene != Scenes.CommandStore) item = new ShellItem(dlg.NewItemRegPath);
                        else item = new StoreShellItem(dlg.NewItemRegPath, true, false);
                        this.InsertItem(item, i + 1);
                        break;
                    }
                }
            }
        }

        private void AddNewShellExItem(string scenePath)
        {
            bool isDragDrop = Scene == Scenes.DragDrop;
            using(InputDialog dlg1 = new InputDialog { Title = AppString.Dialog.InputGuid })
            {
                if(GuidEx.TryParse(Clipboard.GetText(), out Guid guid)) dlg1.Text = guid.ToString();
                if(dlg1.ShowDialog() != DialogResult.OK) return;
                if(GuidEx.TryParse(dlg1.Text, out guid))
                {
                    if(isDragDrop)
                    {
                        using(SelectDialog dlg2 = new SelectDialog())
                        {
                            dlg2.Title = AppString.Dialog.SelectGroup;
                            dlg2.Items = new[] { AppString.SideBar.Folder, AppString.SideBar.Directory,
                                        AppString.SideBar.Drive, AppString.SideBar.AllObjects };
                            if(dlg2.ShowDialog() != DialogResult.OK) return;
                            switch(dlg2.SelectedIndex)
                            {
                                case 0:
                                    scenePath = MENUPATH_FOLDER; break;
                                case 1:
                                    scenePath = MENUPATH_DIRECTORY; break;
                                case 2:
                                    scenePath = MENUPATH_DRIVE; break;
                                case 3:
                                    scenePath = MENUPATH_ALLOBJECTS; break;
                            }
                        }
                    }
                    string shellExPath = GetShellExPath(scenePath);
                    if(ShellExItem.GetPathAndGuids(shellExPath, isDragDrop).Values.Contains(guid))
                    {
                        MessageBoxEx.Show(AppString.MessageBox.HasBeenAdded);
                    }
                    else
                    {
                        string part = isDragDrop ? ShellExItem.DdhParts[0] : ShellExItem.CmhParts[0];
                        string regPath = $@"{shellExPath}\{part}\{guid:B}";
                        Registry.SetValue(regPath, "", guid.ToString("B"));
                        ShellExItem item = new ShellExItem(guid, regPath);
                        for(int i = 0; i < this.Controls.Count; i++)
                        {
                            if(isDragDrop)
                            {
                                if(this.Controls[i] is GroupPathItem groupItem)
                                {
                                    if(groupItem.TargetPath.Equals(shellExPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        this.InsertItem(item, i + 1);
                                        item.FoldGroupItem = groupItem;
                                        item.Visible = !groupItem.IsFold;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                if(this.Controls[i] is NewItem)
                                {
                                    this.InsertItem(item, i + 1);
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    MessageBoxEx.Show(AppString.MessageBox.MalformedGuid);
                }
            }
        }

        private void LoadStoreItems()
        {
            using(var shellKey = RegistryEx.GetRegistryKey(ShellItem.CommandStorePath))
            {
                Array.ForEach(Array.FindAll(shellKey.GetSubKeyNames(), itemName =>
                    !ShellItem.SysStoreItemNames.Contains(itemName, StringComparer.OrdinalIgnoreCase)), itemName =>
                        this.AddItem(new StoreShellItem($@"{ShellItem.CommandStorePath}\{itemName}", true, false)));
            }
        }

        private void AddUwpModeItem()
        {
            XmlDocument doc = AppDic.ReadXml(AppConfig.WebUwpModeItemsDic,
                AppConfig.UserUwpModeItemsDic, Properties.Resources.UwpModeItemsDic);
            List<Guid> guids = new List<Guid>();
            foreach(XmlElement sceneXE in doc.DocumentElement.ChildNodes)
            {
                if(sceneXE.Name == Scene.ToString())
                {
                    foreach(XmlElement itemXE in sceneXE.ChildNodes)
                    {
                        if(GuidEx.TryParse(itemXE.GetAttribute("Guid"), out Guid guid))
                        {
                            if(guids.Contains(guid)) continue;
                            string uwpName = GuidInfo.GetUwpName(guid);
                            if(!string.IsNullOrEmpty(uwpName))
                            {
                                this.AddItem(new UwpModeItem(uwpName, guid));
                                guids.Add(guid);
                            }
                        }
                    }
                }
            }
        }

        private void LoadAnalysisItems()
        {
            if(CurrentFileObjectPath == null) return;

            void AddFileItems(string filePath)
            {
                string extension = Path.GetExtension(filePath);
                if(extension == string.Empty) extension = ".";
                string perceivedType = Registry.GetValue($@"{RegistryEx.CLASSESROOT}\{extension}", "PerceivedType", null)?.ToString();
                string perceivedTypeName = GetPerceivedTypeName(perceivedType);
                string openMode = FileExtension.GetOpenMode(extension);
                JumpItem.Extension = extension;
                JumpItem.PerceivedType = perceivedType;
                this.AddItem(new JumpItem(Scenes.File));
                this.AddItem(new JumpItem(Scenes.AllObjects));
                this.AddItem(new JumpItem(Scenes.CustomExtension));
                if(openMode == null) this.AddItem(new JumpItem(Scenes.UnknownType));
                if(perceivedType != null) this.AddItem(new JumpItem(Scenes.PerceivedType));
            }

            void AddDirItems(string dirPath)
            {
                if(!dirPath.EndsWith(":\\"))
                {
                    this.AddItem(new JumpItem(Scenes.Folder));
                    this.AddItem(new JumpItem(Scenes.Directory));
                    this.AddItem(new JumpItem(Scenes.AllObjects));
                    this.AddItem(new JumpItem(Scenes.DirectoryType));
                }
                else
                {
                    this.AddItem(new JumpItem(Scenes.Drive));
                }
            }

            if(File.Exists(CurrentFileObjectPath))
            {
                string extension = Path.GetExtension(CurrentFileObjectPath).ToLower();
                if(extension == ".lnk")
                {
                    this.AddItem(new JumpItem(Scenes.LnkFile));
                    using(ShellLink shellLink = new ShellLink(CurrentFileObjectPath))
                    {
                        string targetPath = shellLink.TargetPath;
                        if(File.Exists(targetPath))
                        {
                            AddFileItems(targetPath);
                        }
                        else if(Directory.Exists(targetPath))
                        {
                            AddDirItems(targetPath);
                        }
                    }
                }
                else
                {
                    AddFileItems(CurrentFileObjectPath);
                }
            }
            else if(Directory.Exists(CurrentFileObjectPath))
            {
                AddDirItems(CurrentFileObjectPath);
            }
        }

        public sealed class SelectItem : MyListItem
        {
            static string selected;
            public static string Selected
            {
                get => selected;
                set
                {
                    selected = value;
                    SelectedChanged?.Invoke(null, null);
                }
            }

            public static event EventHandler SelectedChanged;

            readonly PictureButton BtnSelect = new PictureButton(AppImage.Select);

            public Scenes Scene { get; private set; }

            public SelectItem(Scenes scene)
            {
                this.Scene = scene;
                this.AddCtr(BtnSelect);
                this.SetTextAndTip();
                this.Image = AppImage.Custom;
                BtnSelect.MouseDown += (sender, e) => ShowSelectDialog();
                this.ImageDoubleClick += (sender, e) => ShowSelectDialog();
                this.TextDoubleClick += (sender, e) => ShowSelectDialog();
            }

            private void SetTextAndTip()
            {
                string tip = "";
                string text = "";
                switch(Scene)
                {
                    case Scenes.CustomExtension:
                        tip = AppString.Dialog.SelectExtension;
                        if(CurrentExtension == null) text = tip;
                        else text = AppString.Other.CurrentExtension.Replace("%s", CurrentExtension);
                        break;
                    case Scenes.PerceivedType:
                        tip = AppString.Dialog.SelectPerceivedType;
                        if(CurrentPerceivedType == null) text = tip;
                        else text = AppString.Other.CurrentPerceivedType.Replace("%s", GetPerceivedTypeName(CurrentPerceivedType));
                        break;
                    case Scenes.DirectoryType:
                        tip = AppString.Dialog.SelectDirectoryType;
                        if(CurrentDirectoryType == null) text = tip;
                        else text = AppString.Other.CurrentDirectoryType.Replace("%s", GetDirectoryTypeName(CurrentDirectoryType));
                        break;
                    case Scenes.CustomRegPath:
                        tip = AppString.Other.SelectRegPath;
                        if(CurrentCustomRegPath == null) text = tip;
                        else text = AppString.Other.CurrentRegPath + "\n" + CurrentCustomRegPath;
                        break;
                    case Scenes.MenuAnalysis:
                        tip = AppString.Tip.DropOrSelectObject;
                        if(CurrentFileObjectPath == null) text = tip;
                        else text = AppString.Other.CurrentFilePath + "\n" + CurrentFileObjectPath;
                        break;

                }
                MyToolTip.SetToolTip(BtnSelect, tip);
                this.Text = text;
            }

            private void ShowSelectDialog()
            {
                SelectDialog dlg = null;
                switch(Scene)
                {
                    case Scenes.CustomExtension:
                        dlg = new FileExtensionDialog
                        {
                            Selected = CurrentExtension?.Substring(1)
                        };
                        break;
                    case Scenes.PerceivedType:
                        dlg = new SelectDialog
                        {
                            Items = PerceivedTypeNames,
                            Title = AppString.Dialog.SelectPerceivedType,
                            Selected = GetPerceivedTypeName(CurrentPerceivedType)
                        };
                        break;
                    case Scenes.DirectoryType:
                        dlg = new SelectDialog
                        {
                            Items = DirectoryTypeNames,
                            Title = AppString.Dialog.SelectDirectoryType,
                            Selected = GetDirectoryTypeName(CurrentDirectoryType)
                        };
                        break;
                    case Scenes.MenuAnalysis:
                        dlg = new SelectDialog
                        {
                            Items = FileObjectTypes,
                            Title = AppString.Dialog.SelectObjectType,
                        };
                        break;
                    case Scenes.CustomRegPath:
                        if(MessageBoxEx.Show(AppString.MessageBox.SelectRegPath,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                        Form frm = this.FindForm();
                        frm.Opacity = 0;
                        using(Process process = Process.Start("regedit.exe", "-m"))
                        {
                            process.WaitForExit();
                        }
                        string path = Registry.GetValue(LASTKEYPATH, "LastKey", "").ToString();
                        int index = path.IndexOf('\\');
                        if(index == -1) return;
                        path = path.Substring(index + 1);
                        Selected = CurrentCustomRegPath = path;
                        frm.Activate();
                        frm.Opacity = 1;
                        break;
                }
                switch(Scene)
                {
                    case Scenes.CustomExtension:
                    case Scenes.PerceivedType:
                    case Scenes.DirectoryType:
                    case Scenes.MenuAnalysis:
                        if(dlg.ShowDialog() != DialogResult.OK) return;
                        break;
                }
                switch(Scene)
                {
                    case Scenes.CustomExtension:
                        Selected = CurrentExtension = dlg.Selected;
                        break;
                    case Scenes.PerceivedType:
                        Selected = CurrentPerceivedType = PerceivedTypes[dlg.SelectedIndex];
                        break;
                    case Scenes.DirectoryType:
                        Selected = CurrentDirectoryType = DirectoryTypes[dlg.SelectedIndex];
                        break;
                    case Scenes.MenuAnalysis:
                        if(dlg.SelectedIndex == 0)
                        {
                            using(var dialog = new System.Windows.Forms.OpenFileDialog())
                            {
                                dialog.DereferenceLinks = false;
                                if(dialog.ShowDialog() != DialogResult.OK) return;
                                Selected = CurrentFileObjectPath = dialog.FileName;
                            }
                        }
                        else
                        {
                            using(var dialog = new FolderBrowserDialog())
                            {
                                if(dialog.ShowDialog() != DialogResult.OK) return;
                                Selected = CurrentFileObjectPath = dialog.SelectedPath;
                            }
                        }
                        break;
                }
            }
        }

        sealed class JumpItem : MyListItem
        {
            public JumpItem(Scenes scene)
            {
                this.AddCtr(btnJump);
                string text = "";
                Image image = null;
                int index1 = 0;
                int index2 = 0;
                switch(scene)
                {
                    case Scenes.File:
                        text = $"[ {AppString.ToolBar.Home} ]  ▶  [ {AppString.SideBar.File} ]";
                        image = AppImage.File;
                        break;
                    case Scenes.Folder:
                        text = $"[ {AppString.ToolBar.Home} ]  ▶  [ {AppString.SideBar.Folder} ]";
                        image = AppImage.Folder;
                        index2 = 1;
                        break;
                    case Scenes.Directory:
                        text = $"[ {AppString.ToolBar.Home} ]  ▶  [ {AppString.SideBar.Directory} ]";
                        image = AppImage.Directory;
                        index2 = 2;
                        break;
                    case Scenes.Drive:
                        text = $"[ {AppString.ToolBar.Home} ]  ▶  [ {AppString.SideBar.Drive} ]";
                        image = AppImage.Drive;
                        index2 = 5;
                        break;
                    case Scenes.AllObjects:
                        text = $"[ {AppString.ToolBar.Home} ]  ▶  [ {AppString.SideBar.AllObjects} ]";
                        image = AppImage.AllObjects;
                        index2 = 6;
                        break;
                    case Scenes.LnkFile:
                        text = $"[ {AppString.ToolBar.Type} ]  ▶  [ {AppString.SideBar.LnkFile} ]";
                        image = AppImage.LnkFile;
                        index1 = 1;
                        break;
                    case Scenes.UnknownType:
                        text = $"[ {AppString.ToolBar.Type} ]  ▶  [ {AppString.SideBar.UnknownType} ]";
                        image = AppImage.NotFound;
                        index1 = 1;
                        index2 = 8;
                        break;
                    case Scenes.CustomExtension:
                        text = $"[ {AppString.ToolBar.Type} ]  ▶  [ {AppString.SideBar.CustomExtension} ]  ▶  [ {Extension} ]";
                        using(Icon icon = ResourceIcon.GetExtensionIcon(Extension))
                        {
                            image = icon.ToBitmap();
                        }
                        index1 = 1;
                        index2 = 4;
                        break;
                    case Scenes.PerceivedType:
                        text = $"[ {AppString.ToolBar.Type} ]  ▶  [ {AppString.SideBar.PerceivedType} ]  ▶  [ {GetPerceivedTypeName(PerceivedType)} ]";
                        image = AppImage.File;
                        index1 = 1;
                        index2 = 5;
                        break;
                    case Scenes.DirectoryType:
                        text = $"[ {AppString.ToolBar.Type} ]  ▶  [ {AppString.SideBar.DirectoryType} ]";
                        image = AppImage.Directory;
                        index1 = 1;
                        index2 = 6;
                        break;
                }
                this.Text = text;
                this.Image = image;
                void SwitchTab()
                {
                    switch(scene)
                    {
                        case Scenes.CustomExtension:
                            CurrentExtension = Extension; break;
                        case Scenes.PerceivedType:
                            CurrentPerceivedType = PerceivedType; break;
                    }
                    ((MainForm)this.FindForm()).SwitchTab(index1, index2);
                };
                btnJump.MouseDown += (sender, e) => SwitchTab();
                this.ImageDoubleClick += (sender, e) => SwitchTab();
                this.TextDoubleClick += (sender, e) => SwitchTab();
            }

            readonly PictureButton btnJump = new PictureButton(AppImage.Jump);

            public static string Extension = null;
            public static string PerceivedType = null;
        }

        sealed class PerceivedTypeItem : MyListItem, ITsiRegPathItem
        {
            public PerceivedTypeItem()
            {
                this.AddCtr(btnSelect);
                this.ContextMenuStrip = new ContextMenuStrip();
                TsiRegLocation = new RegLocationMenuItem(this);
                this.ContextMenuStrip.Items.Add(TsiRegLocation);
                this.Text = $@"{Tip} {GetPerceivedTypeName(PerceivedType)}";
                using(Icon icon = ResourceIcon.GetExtensionIcon(CurrentExtension))
                {
                    this.Image = icon.ToBitmap();
                }
                MyToolTip.SetToolTip(btnSelect, Tip);
                btnSelect.MouseDown += (sender, e) => ShowSelectDialog();
                this.TextDoubleClick += (sender, e) => ShowSelectDialog();
                this.ImageDoubleClick += (sender, e) => ShowSelectDialog();
            }

            public string ValueName => "PerceivedType";
            public string RegPath => $@"{RegistryEx.CLASSESROOT}\{CurrentExtension}";
            private string Tip => AppString.Other.SetPerceivedType.Replace("%s", CurrentExtension);
            public string PerceivedType
            {
                get => Registry.GetValue(RegPath, ValueName, null)?.ToString();
                set
                {
                    if(value == null) RegistryEx.DeleteValue(RegPath, ValueName);
                    else Registry.SetValue(RegPath, ValueName, value, RegistryValueKind.String);
                }
            }

            readonly PictureButton btnSelect = new PictureButton(AppImage.Select);
            public RegLocationMenuItem TsiRegLocation { get; set; }

            private void ShowSelectDialog()
            {
                using(SelectDialog dlg = new SelectDialog())
                {
                    dlg.Items = PerceivedTypeNames;
                    dlg.Title = AppString.Dialog.SelectPerceivedType;
                    dlg.Selected = GetPerceivedTypeName(PerceivedType);
                    if(dlg.ShowDialog() != DialogResult.OK) return;
                    this.Text = $@"{Tip} {dlg.Selected}";
                    PerceivedType = PerceivedTypes[dlg.SelectedIndex];
                }
            }
        }
    }
}