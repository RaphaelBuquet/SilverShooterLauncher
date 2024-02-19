using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using LauncherLogic;
using ReactiveUI;

namespace SilverShooterLauncher.ViewModels;

public class MainWindowViewModel : ViewModelBase, IMainWindowViewModel, IDisposable
{
	private readonly ObservableAsPropertyHelper<string> installedVersion;

	public string InstalledVersion => installedVersion.Value;

	private readonly ObservableAsPropertyHelper<string> latestVersion;

	public string LatestVersion => latestVersion.Value;

	private readonly ObservableAsPropertyHelper<bool> isPlayVisible;

	public bool IsPlayVisible => isPlayVisible.Value;

	private readonly ObservableAsPropertyHelper<bool> isUpdateVisible;

	public bool IsUpdateVisible => isUpdateVisible.Value;

	private readonly ObservableAsPropertyHelper<string> statusText;

	private readonly ObservableAsPropertyHelper<bool> isDownloadVisible;
	private readonly LauncherState state;

	public bool IsDownloadVisible => isDownloadVisible.Value;

	public string StatusText => statusText.Value;

	private readonly ObservableAsPropertyHelper<bool> isLoading;

	public bool IsLoading => isLoading.Value;

	/// <param name="appExe"></param>
	/// <inheritdoc />
	public MainWindowViewModel(string? appExe)
	{
		state = new LauncherState(appExe);
		installedVersion = state.InstalledGameVersion
			.Select(x =>
			{
				if (x == null)
				{
					return "";
				}

				return $"Installed version: {x}";
			})
			.Prepend("")
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.InstalledVersion);

		latestVersion = state.AvailableGameVersion.Select(x =>
			{
				if (x == null)
				{
					return "";
				}

				return $"Latest version: {x}";
			})
			.Prepend("")
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.LatestVersion);

		PlayCommand = ReactiveCommand.CreateFromTask(StartGame, state.IsGameInstalled.ObserveOn(RxApp.MainThreadScheduler));
		isPlayVisible = PlayCommand.CanExecute
			.Prepend(false)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.IsPlayVisible);

		UpdateCommand = ReactiveCommand.CreateFromTask(Update,
			Or(state.GameUpdateAvailable, state.LauncherUpdateAvailable)
				.ObserveOn(RxApp.MainThreadScheduler));
		isUpdateVisible = UpdateCommand.CanExecute
			.Prepend(false)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.IsUpdateVisible);

		DownloadCommand = ReactiveCommand.CreateFromTask(Download,
					And(
							// the game is not installed,
						state.IsGameInstalled.Select(isInstalled => !isInstalled),
						// the game is available to download (the server works),
						state.AvailableGameVersion.Select(x => x != null),
						// and there isn't a launcher update available (overrides game download).
						state.LauncherUpdateAvailable.Select(isAvailable => !isAvailable))
				.ObserveOn(RxApp.MainThreadScheduler));
		isDownloadVisible = DownloadCommand.CanExecute
			.Prepend(false)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.IsDownloadVisible);

		statusText = state.UserMessage
			.Prepend("")
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.StatusText);

		isLoading = state.IsLoading
			.Prepend(true)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.IsLoading);
	}

	private Task Download()
	{
		return Task.Run(() => state.StartDownload());
	}

	private Task Update()
	{
		return state.StartUpdate();
	}

	private Task StartGame()
	{
		return state.StartGame();
	}

	public ReactiveCommand<Unit, Unit> PlayCommand { get; }

	public ReactiveCommand<Unit, Unit> UpdateCommand { get; }


	public ReactiveCommand<Unit, Unit> DownloadCommand { get; }


	public string LauncherVersion => $"Launcher version {LauncherInstall.Version}";

	private static IObservable<bool> Or(IObservable<bool> one, IObservable<bool> two)
	{
		return Observable.Create<bool>(observer =>
		{
			var disposables = new CompositeDisposable();

			var oneState = false;
			var twoState = false;

			void Update()
			{
				observer.OnNext(oneState || twoState);
			}

			one.Subscribe(x =>
			{
				oneState = x;
				Update();
			}).DisposeWith(disposables);
			two.Subscribe(x =>
			{
				twoState = x;
				Update();
			}).DisposeWith(disposables);

			return disposables;
		});
	}

	private static IObservable<bool> And(IObservable<bool> one, IObservable<bool> two)
	{
		return Observable.Create<bool>(observer =>
		{
			var disposables = new CompositeDisposable();

			var oneState = false;
			var twoState = false;

			void Update()
			{
				observer.OnNext(oneState && twoState);
			}

			one.Subscribe(x =>
			{
				oneState = x;
				Update();
			}).DisposeWith(disposables);
			two.Subscribe(x =>
			{
				twoState = x;
				Update();
			}).DisposeWith(disposables);

			return disposables;
		});
	}

	private static IObservable<bool> And(IObservable<bool> one, IObservable<bool> two, IObservable<bool> three)
	{
		return Observable.Create<bool>(observer =>
		{
			var disposables = new CompositeDisposable();

			var oneState = false;
			var twoState = false;
			var threeState = false;

			void Update()
			{
				observer.OnNext(oneState && twoState && threeState);
			}

			one.Subscribe(x =>
			{
				oneState = x;
				Update();
			}).DisposeWith(disposables);
			two.Subscribe(x =>
			{
				twoState = x;
				Update();
			}).DisposeWith(disposables);
			three.Subscribe(x =>
			{
				threeState = x;
				Update();
			}).DisposeWith(disposables);

			return disposables;
		});
	}

	/// <inheritdoc />
	public void Dispose()
	{
		installedVersion.Dispose();
		latestVersion.Dispose();
		isPlayVisible.Dispose();
		isUpdateVisible.Dispose();
		statusText.Dispose();
		isDownloadVisible.Dispose();
		state.Dispose();
		PlayCommand.Dispose();
		UpdateCommand.Dispose();
		DownloadCommand.Dispose();
		isLoading.Dispose();
	}
}