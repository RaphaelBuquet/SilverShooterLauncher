using System;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using SilverShooterLauncher.ViewModels;
using SilverShooterLauncher.Views;

namespace SilverShooterLauncher;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			InstallLogging();
			
			string appExe = typeof(MainWindowViewModel).Assembly.Location;
			desktop.MainWindow = new MainWindow
			{
				DataContext = new MainWindowViewModel(appExe)
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	private void InstallLogging()
	{
		// catch all unhandled errors
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		// TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		RxApp.DefaultExceptionHandler = Observer.Create<Exception>(OnUnhandledRxException);
	}

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		// show user
		ShowMessageBox("Unhandled Error", ((Exception)e.ExceptionObject).Message);
	}

	private void OnUnhandledRxException(Exception e)
	{
		// show user
		ShowMessageBox("Unhandled Error", e.Message);
	}

	private void ShowMessageBox(string title, string message)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.InvokeAsync(() => ShowMessageBox(title, message));
			return;
		}
		var messageBoxStandardWindow = MessageBoxManager
			.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Stop);

		messageBoxStandardWindow.ShowAsync();
	}
}