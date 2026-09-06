using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WindowsTaskViewFSE.Services;
using WindowsTaskViewFSE.ViewModels;

namespace WindowsTaskViewFSE;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var thumbnailProvider = new ThumbnailProvider();
        var windowManager = new WindowManager(thumbnailProvider);
        var controllerService = new ControllerInputService();
        var inputManager = new Services.InputManager(controllerService);

        _viewModel = new MainViewModel(windowManager, inputManager, controllerService, thumbnailProvider);
        DataContext = _viewModel;

        _viewModel.RequestClose += OnRequestClose;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        Closing += OnClosing;
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        _viewModel.RequestClose += OnRequestClose;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize();
        Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // If SearchBox is focused and typing text, allow normal typing unless Escape/Enter/Arrows
        if (SearchBox.IsFocused)
        {
            if (e.Key is Key.Escape or Key.Down or Key.Enter)
            {
                SearchBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
            else
            {
                return;
            }
        }

        (DataContext as MainViewModel)?.NavigateCommand.Execute(null); // InputManager receives KeyDown
        if (DataContext is MainViewModel vm)
        {
            // Forward directly to InputManager
            // InputManager handles keydown via window events if wired
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (SearchBox.IsFocused)
        {
            if (e.Key is Key.Escape)
            {
                if (!string.IsNullOrEmpty(SearchBox.Text))
                {
                    SearchBox.Text = string.Empty;
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key is Key.Down or Key.Enter)
            {
                Keyboard.ClearFocus();
                Focus();
                e.Handled = true;
                return;
            }
            else
            {
                return;
            }
        }

        var navDirection = e.Key switch
        {
            Key.Up => NavigationDirection.Up,
            Key.Down => NavigationDirection.Down,
            Key.Left => NavigationDirection.Left,
            Key.Right => NavigationDirection.Right,
            Key.Enter or Key.Space => NavigationDirection.Select,
            Key.Escape => NavigationDirection.Back,
            Key.Delete or Key.X => NavigationDirection.CloseApp,
            Key.F5 => NavigationDirection.Refresh,
            Key.PageUp or Key.Prior => NavigationDirection.PageLeft,
            Key.PageDown or Key.Next => NavigationDirection.PageRight,
            _ => (NavigationDirection?)null
        };

        if (navDirection.HasValue)
        {
            e.Handled = true;
            _viewModel.Navigate(navDirection.Value);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedWindow))
        {
            // If needed, scroll into view
        }
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.Dispose();
    }
}
