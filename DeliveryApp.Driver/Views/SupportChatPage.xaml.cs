using System.Collections.Specialized;
using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class SupportChatPage : ContentPage
{
    private readonly SupportChatViewModel _vm;
    private bool _scrollScheduled;
    private bool _subscribed;

    public SupportChatPage(SupportChatViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_subscribed)
        {
            _vm.Messages.CollectionChanged += OnMessagesChanged;
            _subscribed = true;
        }

        _vm.InitIfNeeded();
        ScheduleScrollToLastMessage();
    }

    protected override void OnDisappearing()
    {
        if (_subscribed)
        {
            _vm.Messages.CollectionChanged -= OnMessagesChanged;
            _subscribed = false;
        }

        base.OnDisappearing();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScheduleScrollToLastMessage();

    private void ScheduleScrollToLastMessage()
    {
        if (_scrollScheduled)
            return;

        _scrollScheduled = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Give CollectionView time to finish layout/item generation on Android.
            await Task.Delay(120);

            try
            {
                if (Handler == null || ChatList.Handler == null || _vm.Messages.Count == 0)
                    return;

                var lastIndex = _vm.Messages.Count - 1;
                if (lastIndex < 0)
                    return;

                // Use a valid numeric index instead of passing LastOrDefault(), which
                // can be null while the CollectionView is still being created.
                ChatList.ScrollTo(lastIndex, position: ScrollToPosition.End, animate: false);
            }
            catch (ArgumentOutOfRangeException)
            {
                // The collection may change between checking Count and layout.
            }
            catch (InvalidOperationException)
            {
                // Android can reject ScrollTo while RecyclerView is laying out.
            }
            finally
            {
                _scrollScheduled = false;
            }
        });
    }
}
