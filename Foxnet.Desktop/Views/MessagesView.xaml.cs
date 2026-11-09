using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Foxnet.Desktop.ViewModels;

namespace Foxnet.Desktop.Views;

public partial class MessagesView : UserControl
{
    public MessagesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MessagesViewModel oldModel)
            oldModel.PropertyChanged -= OnMessagesChanged;
        if (e.NewValue is MessagesViewModel model)
            model.PropertyChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MessagesViewModel.Messages))
        {
            Action scroll = sender is MessagesViewModel { Messages.Count: 1 } ? History.ScrollToTop : History.ScrollToEnd;
            Dispatcher.InvokeAsync(scroll, DispatcherPriority.Loaded);
        }
    }
}
