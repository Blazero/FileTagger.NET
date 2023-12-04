using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using MemoryPack;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace FileTagger.NET;

using TagType = string;
using IdentifierType = long;
using TagSetType = HashSet<string>;
using TagDictType = Dictionary<long, HashSet<string>?>;

[MemoryPackable]
public partial record TagInfo(TagSetType AllTags, TagDictType TagDict);

public class FileWithTag(string Name, string Dir, DateTime LastWriteTime, long CreationFileTime, TagSetType? Tags)
{
    public string Name { get; init; } = Name;
    public string Dir { get; init; } = Dir;
    public DateTime LastWriteTime { get; init; } = LastWriteTime;
    public long CreationFileTime { get; init; } = CreationFileTime;
    public TagSetType? Tags { get; init; } = Tags;
    public IdentifierType Identifier { get => CreationFileTime; }
    public string Path { get => System.IO.Path.Combine(Dir, Name); }

    public FileWithTag(FileSystemInfo info, TagSetType? tags)
        : this(info.Name, Directory.GetParent(info.FullName)!.FullName, info.LastWriteTime, info.CreationTime.ToFileTime(), tags)
    { }

    public FileWithTag(FileSystemInfo info, long creationFileTime, TagSetType? tags)
        : this(info.Name, Directory.GetParent(info.FullName)!.FullName, info.LastWriteTime, creationFileTime, tags)
    { }
}

class FilterableObservableCollection<T>
{
    private readonly Collection<T> _allItems = [];
    private readonly ObservableCollection<T> _filteredItems = [];

    public Func<T, bool>? Filter { get; set; }
    public ReadOnlyObservableCollection<T> FilteredItems { get; }
    public int Count => _allItems.Count;

    public FilterableObservableCollection() => FilteredItems = new(_filteredItems);

    public void Add(T item)
    {
        _allItems.Add(item);
        if (Filter == null || Filter(item))
            _filteredItems.Add(item);
    }

    public void AddRange(IEnumerable<T> items)
    {
        _allItems.AddRange(items);
        if (Filter == null) _filteredItems.AddRange(items);
        else _filteredItems.AddRange(items.Where(item => Filter(item)));
    }

    public void Replace(T original, T replaceWith)
    {
        _allItems.Replace(original, replaceWith);
        if (Filter == null || Filter(replaceWith))
            _filteredItems.Replace(original, replaceWith);
        else _filteredItems.Remove(original);
    }

    public void Remove(T item)
    {
        _allItems.Remove(item);
        _filteredItems.Remove(item);
    }

    public void Clear()
    {
        _allItems.Clear();
        _filteredItems.Clear();
    }

    public void ApplyFilter()
    {
        _filteredItems.Clear();
        if (Filter == null) _filteredItems.AddRange(_allItems);
        else _filteredItems.AddRange(_allItems.Where(Filter));
    }

    public T Single(Func<T, bool> predicate)
    {
        return _allItems.Single(predicate);
    }

    public T First()
    {
        return _allItems.First();
    }
}

public partial class FileViewerViewModel : ObservableObject
{
    public FileViewerViewModel()
    {
        _filesCache = new();
        FileList = _filesCache.FilteredItems;
    }

    const string TAG_FILE_NAME = "tags.mempak";

    private readonly FilterableObservableCollection<FileWithTag> _filesCache;
    private TagDictType _tagDict = [];

    public ReadOnlyObservableCollection<FileWithTag> FileList { get; }
    public ObservableCollection<TagType> AllTags { get; } = [];
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string _filenameFilter = "";
    [ObservableProperty] private string _tagFilter = "";
    public System.Collections.IList? SelectedFiles { private get; set; }
    public List<TagType> SelectedTags { get; } = [];

    [RelayCommand]
    private async Task GoToDir()
    {
        if (Address == null) return;
        var di = new DirectoryInfo(Address);
        if (!di.Exists) return;

        ResetFilterContent();
        await LoadTagsAsync(Address);
        LoadFileList(di);
    }

    [RelayCommand]
    private async Task GoUp()
    {
        if (Address == null) return;
        var parent = Directory.GetParent(Address);
        if (parent == null) return;
        Address = parent.FullName;
        await GoToDir();
    }

    [RelayCommand]
    private async Task BrowseDir()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } provider)
            throw new NullReferenceException("Missing StorageProvider instance.");

        var dirs = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            Title = "选择文件夹",
            AllowMultiple = false
        });
        if (!(dirs?.Count >= 1)) return;

        Address = dirs[0].Path.LocalPath;
        await GoToDir();
    }

    public async Task OpenAsync(FileWithTag file)
    {
        if (Directory.Exists(file.Path)) // 是文件夹
        {
            Address = file.Path;
            await GoToDir();
        }
        else // 是文件
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = file.Path,
                UseShellExecute = true
            });
        }
    }

    private async Task LoadTagsAsync(string address)
    {
        var tagFile = Path.Combine(address, TAG_FILE_NAME);
        if (File.Exists(tagFile))
        {
            using var fs = File.OpenRead(tagFile);
            var tagInfo = await MemoryPackSerializer.DeserializeAsync<TagInfo>(fs);
            if (tagInfo != null)
            {
                AllTags.Clear();
                AllTags.AddRange(tagInfo.AllTags);
                _tagDict = tagInfo.TagDict;
                return;
            }
        }
        AllTags.Clear();
        _tagDict = [];
    }

    private void LoadFileList(DirectoryInfo di)
    {
        _filesCache.Clear();

        var creationTimes = new HashSet<long>();
        foreach (var fsi in di.GetFileSystemInfos())
        {
            var fileTime = fsi.CreationTime.ToFileTime();
            if (creationTimes.Contains(fileTime))
            {
                do fileTime++;
                while (creationTimes.Contains(fileTime));
                // 遇到重复的 CreationTime，则将其微调至不重复
                fsi.CreationTime = DateTime.FromFileTime(fileTime);
            }
            creationTimes.Add(fileTime);

            _filesCache.Add(new FileWithTag(fsi, fileTime, _tagDict.GetValueOrDefault(fileTime)));
        }
    }

    [RelayCommand]
    private void FilterFileList()
    {
        DoFilter(FilenameFilter, TagFilter);
    }

    /* TagFilter_SelectionChanged 事件触发时，
     * 绑定的 TagFilter 值还未发生变化，
     * 因此增加此函数手动传入 TagFilter 的值 */
    public void FilterFileListByTag(string TagFilter)
    {
        DoFilter(FilenameFilter, TagFilter);
    }

    private void DoFilter(string FilenameFilter, string TagFilter)
    {
        _filesCache.Filter =
            file => file.Name.ToLower().Contains(FilenameFilter, StringComparison.CurrentCultureIgnoreCase) &&
            (string.IsNullOrWhiteSpace(TagFilter) || (file.Tags != null && file.Tags.Contains(TagFilter)));
        _filesCache.ApplyFilter();
    }

    private void ResetFilterContent()
    {
        FilenameFilter = "";
        TagFilter = "";
        _filesCache.Filter = null;
    }

    [RelayCommand]
    private void ResetFilter()
    {
        ResetFilterContent();
        _filesCache.ApplyFilter();
    }

    public async Task AddTag(TagType tag)
    {
        if (_filesCache.Count == 0) return;

        tag = tag.Trim();
        if (AllTags.Contains(tag)) return;

        AllTags.Add(tag);
        await SaveTagInfoToFileAsync();
    }

    [RelayCommand]
    public async Task RemoveTags()
    {
        if (SelectedTags.Count == 0) return;

        var box = MessageBoxManager.GetMessageBoxStandard("删除确认", $"确定要删除标签 [{String.Join("], [", SelectedTags)}] 吗？\n此操作将同时移除所有文件上的相应标签。", ButtonEnum.YesNo);
        var result = await box.ShowAsync();
        if (result != ButtonResult.Yes) return;

        // 移除标签会触发 SelectedTags 变化，因此先复制一份
        var tags = SelectedTags.ToImmutableList();

        foreach (var pair in _tagDict)
        {
            if (pair.Value == null) continue;

            var exist = false;
            foreach (var tag in tags)
                exist = exist && pair.Value.Remove(tag);
            if (!exist) continue;

            var currentFile = _filesCache.Single(i => i.Identifier == pair.Key);
            var modifiedFile = new FileWithTag(currentFile.Name, currentFile.Dir, currentFile.LastWriteTime, currentFile.CreationFileTime, pair.Value);
            _filesCache.Replace(currentFile, modifiedFile);
        }

        AllTags.Remove(tags);

        await SaveTagInfoToFileAsync();
    }

    [RelayCommand]
    public async Task AddTagsToFiles()
    {
        if (SelectedFiles == null || SelectedFiles.Count == 0 || SelectedTags.Count == 0) return;

        // 循环内修改 DataGrid 数据会导致 SelectedFiles 变化，因此先拷贝一份
        var files = SelectedFiles.Cast<FileWithTag>().ToImmutableList();
        foreach (FileWithTag file in files)
        {
            if (_tagDict.GetValueOrDefault(file.Identifier) == null) _tagDict[file.Identifier] = new(SelectedTags);
            else _tagDict[file.Identifier]!.UnionWith(SelectedTags); // UnionWith 批量添加

            _filesCache.Replace(file,
                new FileWithTag(file.Name, file.Dir, file.LastWriteTime, file.CreationFileTime, _tagDict[file.Identifier]));
        }

        await SaveTagInfoToFileAsync();
    }

    [RelayCommand]
    public async Task RemoveTagsFromFiles()
    {
        if (SelectedFiles == null || SelectedFiles.Count == 0 || SelectedTags.Count == 0) return;

        // 循环内将会修改 DataGrid 数据，导致 SelectedFiles 变化，因此需要先拷贝一份
        var files = SelectedFiles.Cast<FileWithTag>().ToImmutableList();
        foreach (FileWithTag file in files)
        {
            if (_tagDict.GetValueOrDefault(file.Identifier) == null) continue;
            else _tagDict[file.Identifier]!.ExceptWith(SelectedTags); // ExceptWith 批量移除

            _filesCache.Replace(file,
                new FileWithTag(file.Name, file.Dir, file.LastWriteTime, file.CreationFileTime, _tagDict[file.Identifier]));
        }

        await SaveTagInfoToFileAsync();
    }

    private async Task SaveTagInfoToFileAsync()
    {
        if (_filesCache.Count == 0) return;

        var tagFile = Path.Combine(_filesCache.First().Dir, TAG_FILE_NAME);
        using var fs = File.OpenWrite(tagFile);
        await MemoryPackSerializer.SerializeAsync(fs, new TagInfo([.. AllTags], _tagDict));
    }
}
