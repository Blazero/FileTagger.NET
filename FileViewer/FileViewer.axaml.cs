using Avalonia.Controls;
using Avalonia.Input;

namespace FileTagger.NET;

public partial class FileViewer : UserControl
{
    public FileViewer()
    {
        InitializeComponent();
        DataContext = new FileViewerViewModel();
    }

    private void Files_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        (DataContext as FileViewerViewModel)!.SelectedFiles = (sender as DataGrid)!.SelectedItems;
    }

    private async void File_DoubleTapped(object sender, TappedEventArgs e)
    {
        var doubleTappedFile = (sender as DataGrid)!.SelectedItem as FileWithTag;
        if (doubleTappedFile == null) return;
        await (DataContext as FileViewerViewModel)!.OpenAsync(doubleTappedFile);
    }

    private void AddressBar_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        (DataContext as FileViewerViewModel)?.GoToDirCommand.Execute(null);
    }

    private void NewLabel_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var tb = (sender as TextBox)!;
        if (tb.Text != null) (DataContext as FileViewerViewModel)?.AddTag(tb.Text);
        tb.Clear();
    }

    private void FilenameFilter_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        (DataContext as FileViewerViewModel)?.FilterFileListCommand.Execute(null);
    }

    private void TagFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems[0] is string tagFilter)
            (DataContext as FileViewerViewModel)?.FilterFileListByTag(tagFilter);
    }
}