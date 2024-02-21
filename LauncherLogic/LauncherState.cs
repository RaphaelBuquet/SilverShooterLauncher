using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Palladium.Logging;
using PalladiumUpdater.Protocol;

namespace LauncherLogic;

public class LauncherState : IDisposable
{
	private readonly Log log;
	private readonly string? appExe;

	public readonly IObservable<bool> GameUpdateAvailable;

	public readonly IObservable<bool> LauncherUpdateAvailable;

	private readonly LoadingStack loadingStack;
	private readonly ReplaySubject<bool> isLoading;
	private readonly ReplaySubject<bool> isGameInstalled;
	private readonly ReplaySubject<string?> installedGameVersion;
	private readonly HttpClient client;
	private readonly ReplaySubject<string> userMessage;
	private readonly ReplaySubject<string?> availableLauncherVersion;
	private readonly ReplaySubject<string?> availableGameVersion;

	public LauncherState(Log log, string? appExe)
	{
		this.log = log;
		this.appExe = appExe;

		isLoading = new ReplaySubject<bool>();
		loadingStack = new LoadingStack(isLoading);

		isGameInstalled = new ReplaySubject<bool>(1);
		installedGameVersion = new ReplaySubject<string?>(1);
		availableGameVersion = new ReplaySubject<string?>(1);
		userMessage = new ReplaySubject<string>(1);
		availableLauncherVersion = new ReplaySubject<string?>(1);

		RefreshGameInstallState();

		client = new HttpClient();

		// network calls
		{
			GetAvailableGameVersion(client)
				.BindToLoadingStack(loadingStack)
				.ToObservable()
				.Subscribe(availableGameVersion.OnNext, availableGameVersion.OnError);
			GetAvailableLauncherVersion(client)
				.BindToLoadingStack(loadingStack)
				.ToObservable()
				.Subscribe(availableLauncherVersion.OnNext, availableLauncherVersion.OnError);
		}

		// state logic
		{
			GameUpdateAvailable = installedGameVersion.CombineLatest(availableGameVersion).Select(pair =>
			{
				(string? installedVersionValue, string? availableVersionValue) = pair;
				return installedVersionValue != null && availableVersionValue != null && installedVersionValue != availableVersionValue;
			});
			LauncherUpdateAvailable = availableLauncherVersion.Select(x =>
			{
				// consider no update is available if the system failed to get the latest version.
				if (x == null)
				{
					return false;
				}
				return x != LauncherInstall.Version;
			});
		}

		// user facing messages
		{
			availableGameVersion.Where(x => x is null)
				.Subscribe(_ => { userMessage.OnNext("The server is unavailable."); });
			LauncherUpdateAvailable
				.Take(1) // only show once to user
				.Where(x => x)
				.Subscribe(_ => { userMessage.OnNext("A launcher update is available."); });

			GameUpdateAvailable
				.Take(1) // only show once to user
				.CombineLatest(LauncherUpdateAvailable.Take(1))
				.Where(pair => pair.First && !pair.Second) // only show if launcher update isn't shown
				.Subscribe(_ => { userMessage.OnNext("A game update is available!"); });
		}

		// error handling
		{
			isLoading.Subscribe(_ => { }, HandleException);
			installedGameVersion.Subscribe(_ => { }, HandleException);
			availableGameVersion.Subscribe(_ => { }, HandleException);
			availableLauncherVersion.Subscribe(_ => { }, HandleException);
			isGameInstalled.Subscribe(_ => { }, HandleException);
			LauncherUpdateAvailable.Subscribe(_ => { }, HandleException);
			GameUpdateAvailable.Subscribe(_ => { }, HandleException);
		}
	}

	/// <summary>
	///     Emits null when it isn't installed, emits a value when it is installed or found.
	/// </summary>
	public IObservable<string?> InstalledGameVersion => installedGameVersion;

	/// <summary>
	///     Emits null when it isn't found, or the value when it is fetched.
	/// </summary>
	public IObservable<string?> AvailableGameVersion => availableGameVersion;

	/// <summary>
	///     Emits null when it isn't found, or the value when it is fetched.
	/// </summary>
	public IObservable<string?> AvailableLauncherVersion => availableLauncherVersion;

	public IObservable<bool> IsGameInstalled => isGameInstalled;

	public IObservable<bool> IsLoading => isLoading;

	/// <summary>
	///     Emits user facing message updates.
	/// </summary>
	public IObservable<string> UserMessage => userMessage;

	private void RefreshGameInstallState()
	{
		installedGameVersion.OnNext(DateTime.Now.ToString());
		isGameInstalled.OnNext(GameInstall.IsGameInstalled());
		GameInstall.GetGameVersion()
			.BindToLoadingStack(loadingStack)
			.ToObservable()
			.Subscribe(installedGameVersion.OnNext, installedGameVersion.OnError);
	}

	private void HandleException(Exception e)
	{
		log.Error($"An error was propagated: {e}");
		userMessage.OnNext("An internal error occured.");
	}

	private async Task<string?> GetAvailableLauncherVersion(HttpClient client)
	{
		UpdateSourceConfig config = await LauncherInstall.GetLauncherUpdateSourceConfig(log);
		try
		{
			return await HttpDownloader.GetLatestVersion(client, config.LatestVersionUrl, log);
		}
		catch (Exception e)
		{
			log.Error($"Failed to get the available launcher version: {e}");
			return null;
		}
	}

	private async Task<string?> GetAvailableGameVersion(HttpClient client)
	{
		UpdateSourceConfig config = await GameInstall.GetGameUpdateSourceConfig(log);
		try
		{
			return await HttpDownloader.GetLatestVersion(client, config.LatestVersionUrl, log);
		}
		catch (Exception e)
		{
			log.Error($"Failed to get the available game version: {e}");
			return null;
		}
	}

	public async Task StartGame()
	{
		using LoadingStack.Handle handle = loadingStack.Acquire();
		try
		{
			Process? process = GameInstall.StartGame();
			userMessage.OnNext("");

			// check for success
			if (process != null)
			{
				await Task.Delay(TimeSpan.FromSeconds(2));
				if (!process.HasExited)
				{
					if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
					{
						desktopApp.Shutdown();
					}
				}
			}
			else
			{
				log.Error($"Failed to start the game: no process was started.");
				userMessage.OnNext("Failed to start the game.");
			}
		}
		catch (Exception e)
		{
			log.Error($"Failed to start the game: {e}");
			userMessage.OnNext("Failed to start the game due to an internal error.");
		}
	}

	public async Task StartUpdate()
	{
		if (await LauncherUpdateAvailable.FirstAsync())
		{
			await UpdateLauncher();
		}
		else if (await GameUpdateAvailable.FirstAsync())
		{
			await GetGame(true);
		}
	}

	private async Task UpdateLauncher()
	{
		using LoadingStack.Handle handle = loadingStack.Acquire();
		try
		{
			string? version = await AvailableLauncherVersion.FirstAsync();
			if (version == null)
			{
				throw new InvalidOperationException();
			}

			userMessage.OnNext($"Getting launcher update details...");
			UpdateSourceConfig config = await LauncherInstall.GetLauncherUpdateSourceConfig(log);

			string downloadedArchive;
			try
			{
				downloadedArchive = await HttpDownloader.GetArchive(client,
					config.ArchiveUrl,
					version,
					LauncherInstall.AppName,
					Observer.Create<float>(progress => { userMessage.OnNext($"Downloading launcher update: {Math.Round(progress * 100.0)}%"); }),
					log);
			}
			catch (Exception e)
			{
				log.Error($"Failed to download the launcher update archive: {e}");
				userMessage.OnNext("Failed to download the launcher update.");
				return;
			}

			userMessage.OnNext($"Decompressing...");
			string downloadedUpdate = await ArchiveHandler.Decompress(downloadedArchive, LauncherInstall.AppName, version);

			if (appExe == null)
			{
				userMessage.OnNext("Failed to determine the location of the file to update.");
				return;
			}

			string? downloadedExe = SelfUpdate.FindExeInDownloadedUpdate(downloadedUpdate);
			if (downloadedExe == null)
			{
				userMessage.OnNext("The downloaded launcher update is invalid.");
				return;
			}

			try
			{
				userMessage.OnNext($"Copying files...");
				await Task.Run(async () => await SelfUpdate.InstallUpdateSingleFile(downloadedExe, appExe, typeof(LauncherState).Assembly));
			}
			catch (Exception e)
			{
				log.Error($"Failed to perform auto-update of launcher: {e}");
				userMessage.OnNext("Failed to perform auto-update of launcher.");
				return;
			}
		}
		catch (Exception e)
		{
			log.Error($"Failed to update the launcher: {e}");
			userMessage.OnNext("Failed to update the launcher due to an internal error.");
		}
	}

	public Task StartGameDownload()
	{
		return GetGame(false);
	}

	private async Task GetGame(bool isUpdate)
	{
		using LoadingStack.Handle handle = loadingStack.Acquire();
		try
		{
			string? version = await AvailableGameVersion.FirstAsync();
			if (version == null)
			{
				throw new InvalidOperationException();
			}

			userMessage.OnNext(isUpdate ? "Getting game update details..." : "Getting game details...");
			UpdateSourceConfig config = await GameInstall.GetGameUpdateSourceConfig(log);

			string downloadedArchive;
			try
			{
				downloadedArchive = await HttpDownloader.GetArchive(client,
					config.ArchiveUrl,
					version,
					GameInstall.GameName,
					Observer.Create<float>(progress => { userMessage.OnNext($"Downloading game: {Math.Round(progress * 100.0)}%"); }),
					log);
			}
			catch (Exception e)
			{
				log.Error($"Failed to download the game archive: {e}");
				userMessage.OnNext("Failed to download the game.");
				return;
			}

			userMessage.OnNext($"Decompressing...");
			string downloadedGame = await ArchiveHandler.Decompress(downloadedArchive, GameInstall.GameName, version);

			userMessage.OnNext($"Moving files...");
			try
			{
				await Task.Run(() => AppUpdate.InstallUpdateFromDirectory(downloadedGame, GameInstall.GameName));
			}
			catch (Exception e)
			{
				log.Error($"Failed to install game files: {e}");
				userMessage.OnNext("Failed to install game files.");
				return;
			}
			userMessage.OnNext(isUpdate ? "Game updated!" : "Game installed!");
			RefreshGameInstallState();
		}
		catch (Exception e)
		{
			if (isUpdate)
			{
				log.Error($"Failed to update the game: {e}");
				userMessage.OnNext("Failed to update the game due to an internal error.");
			}
			else
			{
				log.Error($"Failed to download the game: {e}");
				userMessage.OnNext("Failed to download the game due to an internal error.");
			}
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		isLoading.Dispose();
		isGameInstalled.Dispose();
		installedGameVersion.Dispose();
		client.Dispose();
		userMessage.Dispose();
		availableLauncherVersion.Dispose();
		availableGameVersion.Dispose();
	}
}