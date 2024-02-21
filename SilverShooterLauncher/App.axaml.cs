using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LauncherLogic;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Palladium.Logging;
using ReactiveUI;
using SilverShooterLauncher.ViewModels;
using SilverShooterLauncher.Views;
using Path = System.IO.Path;

namespace SilverShooterLauncher;

public partial class App : Application
{
	private Log? log;

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			InstallLogging(desktop);
			Debug.Assert(log != null);

			string? appExe = Environment.ProcessPath;
			if (!File.Exists(appExe))
			{
				appExe = null;
				log.Error("Could not determine the path to the .exe file. Self-update will be unavailable.");
			}
			else
			{
				log.Info($"Running from \"{appExe}\". Self-update is available.");
			}
			desktop.MainWindow = new MainWindow
			{
				DataContext = new MainWindowViewModel(log, appExe)
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	private void InstallLogging(IClassicDesktopStyleApplicationLifetime desktop)
	{
		// catch all unhandled errors
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		// TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		RxApp.DefaultExceptionHandler = Observer.Create<Exception>(OnUnhandledRxException);

		log = new Log();
		string targetFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LauncherInstall.AppName, "Logs.log");
		var logToFile = new LogToFile(log, targetFile);
		_ = new LogToConsole(log);

		desktop.ShutdownRequested += (sender, args) =>
		{
			// request stop log thread
			logToFile.Dispose();
		};
	}

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		// show user
		log?.Error("Unhandled exception", (Exception)e.ExceptionObject);
		ShowMessageBox("Unhandled Error", ((Exception)e.ExceptionObject).Message);
	}

	private void OnUnhandledRxException(Exception e)
	{
		// show user
		log?.Error("Unhandled Rx exception", e);
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