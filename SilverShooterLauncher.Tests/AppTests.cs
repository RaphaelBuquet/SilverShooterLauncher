using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using LauncherLogic;
using Microsoft.Reactive.Testing;
using PalladiumUpdater.Protocol;
using ReactiveUI;
using ReactiveUI.Testing;
using SilverShooterLauncher.ViewModels;

namespace SilverShooterLauncher.Tests;

[NonParallelizable]
public class AppTests
{
	[Test]
	[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
	public async Task InstallGame()
	{
		GameInstall.DeleteGameInstall();

		int port = ArrangeConfig(out UpdateSourceConfig launcherConfig, out UpdateSourceConfig gameConfig);
		await InstallConfig(launcherConfig, gameConfig);

		using var stopHandle = new MockServer.StopHandle();
		await StartServer(port, stopHandle, launcherConfig, gameConfig, LauncherInstall.Version, "v1");

		await new TestScheduler().With(async scheduler =>
		{
			using var vm = new MainWindowViewModel(null);

			await Task.Delay(TimeSpan.FromMilliseconds(500)); // seems to be needed when the debugger is attached
			scheduler.Start();

			Assert.IsTrue(vm.IsDownloadVisible);
			Assert.IsFalse(vm.IsPlayVisible);
			Assert.IsFalse(vm.IsUpdateVisible);
			Assert.AreEqual("", vm.InstalledVersion);
			Assert.AreEqual("Latest version: v1", vm.LatestVersion);
			Assert.AreEqual("", vm.StatusText);
			Assert.IsFalse(vm.IsLoading);

			var isLoadingWasTrue = vm.WhenAnyValue(x => x.IsLoading)
				.Any(isLoading => isLoading)
				.Timeout(TimeSpan.FromSeconds(1))
				.ToTask();
			await vm.DownloadCommand.Execute();
			scheduler.Start();

			Assert.IsTrue(await isLoadingWasTrue);
			Assert.IsFalse(vm.IsLoading);
			Assert.IsFalse(vm.IsDownloadVisible);
			Assert.IsTrue(vm.IsPlayVisible);
			Assert.IsFalse(vm.IsUpdateVisible);
			Assert.AreEqual("Installed version: v1", vm.InstalledVersion);
			Assert.AreEqual("Game installed!", vm.StatusText);
		});
	}

	[Test]
	[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
	public async Task UpdateGame()
	{
		GameInstall.MockGameInstall("v0.1");

		int port = ArrangeConfig(out UpdateSourceConfig launcherConfig, out UpdateSourceConfig gameConfig);
		await InstallConfig(launcherConfig, gameConfig);

		using var stopHandle = new MockServer.StopHandle();
		await StartServer(port, stopHandle, launcherConfig, gameConfig, LauncherInstall.Version, "v1");

		await new TestScheduler().With(async scheduler =>
		{
			using var vm = new MainWindowViewModel(null);

			await Task.Delay(TimeSpan.FromMilliseconds(500)); // seems to be needed when the debugger is attached
			scheduler.Start();

			Assert.IsFalse(vm.IsDownloadVisible);
			Assert.IsTrue(vm.IsPlayVisible);
			Assert.IsTrue(vm.IsUpdateVisible);
			Assert.AreEqual("Installed version: v0.1", vm.InstalledVersion);
			Assert.AreEqual("Latest version: v1", vm.LatestVersion);
			Assert.AreEqual("A game update is available!", vm.StatusText);
			Assert.IsFalse(vm.IsLoading);

			var isLoadingWasTrue = vm.WhenAnyValue(x => x.IsLoading)
				.Any(isLoading => isLoading)
				.Timeout(TimeSpan.FromSeconds(1))
				.ToTask();
			await vm.UpdateCommand.Execute();
			scheduler.Start();

			Assert.IsTrue(await isLoadingWasTrue);
			Assert.IsFalse(vm.IsLoading);
			Assert.IsFalse(vm.IsDownloadVisible);
			Assert.IsTrue(vm.IsPlayVisible);
			Assert.IsFalse(vm.IsUpdateVisible);
			Assert.AreEqual("Installed version: v1", vm.InstalledVersion);
			Assert.AreEqual("Game updated!", vm.StatusText);
		});
	}

	[Test]
	[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
	public async Task LauncherUpdate()
	{
		GameInstall.MockGameInstall("v1");

		int port = ArrangeConfig(out UpdateSourceConfig launcherConfig, out UpdateSourceConfig gameConfig);
		await InstallConfig(launcherConfig, gameConfig);

		using var stopHandle = new MockServer.StopHandle();
		await StartServer(port, stopHandle, launcherConfig, gameConfig, "v1111111111", "v1");

		await new TestScheduler().With(async scheduler =>
		{
			using var vm = new MainWindowViewModel(null);

			await Task.Delay(TimeSpan.FromMilliseconds(500)); // seems to be needed when the debugger is attached
			scheduler.Start();

			Assert.IsFalse(vm.IsDownloadVisible);
			Assert.IsTrue(vm.IsPlayVisible);
			Assert.IsTrue(vm.IsUpdateVisible);
			Assert.AreEqual("Installed version: v1", vm.InstalledVersion);
			Assert.AreEqual("Latest version: v1", vm.LatestVersion);
			Assert.AreEqual("A launcher update is available.", vm.StatusText);
			Assert.IsFalse(vm.IsLoading);

			var isLoadingWasTrue = vm.WhenAnyValue(x => x.IsLoading)
				.Any(isLoading => isLoading)
				.Timeout(TimeSpan.FromSeconds(1))
				.ToTask();
			await vm.UpdateCommand.Execute();
			scheduler.Start();

			Assert.IsTrue(await isLoadingWasTrue);
			Assert.IsFalse(vm.IsLoading);
			Assert.IsFalse(vm.IsDownloadVisible);
			Assert.IsTrue(vm.IsPlayVisible);
			Assert.IsTrue(vm.IsUpdateVisible);
			Assert.AreEqual("Installed version: v1", vm.InstalledVersion);
			Assert.AreEqual("Failed to determine the location of the file to update.", vm.StatusText);
		});
	}

	private static async Task InstallConfig(UpdateSourceConfig launcherConfig, UpdateSourceConfig gameConfig)
	{
		await File.WriteAllTextAsync(LauncherInstall.ConfigFile, ConfigParser.MakeString(launcherConfig));
		await File.WriteAllTextAsync(GameInstall.ConfigFile, ConfigParser.MakeString(gameConfig));
	}

	private static int ArrangeConfig(out UpdateSourceConfig launcherConfig, out UpdateSourceConfig gameConfig)
	{
		var port = 1888;
		launcherConfig = new UpdateSourceConfig()
		{
			ArchiveUrl = $"http://localhost:{port}/launcher/archive/",
			LatestVersionUrl = $"http://localhost:{port}/launcher/latest/"
		};
		gameConfig = new UpdateSourceConfig()
		{
			ArchiveUrl = $"http://localhost:{port}/game/archive/",
			LatestVersionUrl = $"http://localhost:{port}/game/latest/"
		};
		return port;
	}

	[Test]
	[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
	public async Task ServerUnavailable_WithoutGameInstalled()
	{
		GameInstall.DeleteGameInstall();

		int port = ArrangeConfig(out UpdateSourceConfig launcherConfig, out UpdateSourceConfig gameConfig);
		await InstallConfig(launcherConfig, gameConfig);

		using var stopHandle = new MockServer.StopHandle();
		await Start404Server(port, stopHandle);

		await new TestScheduler().With(async scheduler =>
		{
			using var vm = new MainWindowViewModel(null);

			await Task.Delay(TimeSpan.FromMilliseconds(500)); // seems to be needed when the debugger is attached
			scheduler.Start();

			Assert.IsFalse(vm.IsDownloadVisible);
			Assert.IsFalse(vm.IsPlayVisible);
			Assert.IsFalse(vm.IsUpdateVisible);
			Assert.AreEqual("", vm.InstalledVersion);
			Assert.AreEqual("", vm.LatestVersion);
			Assert.AreEqual("The server is unavailable.", vm.StatusText);
			Assert.IsFalse(vm.IsLoading);
		});
	}

	[Test]
	[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
	public async Task ServerUnavailable_WithInstalled()
	{
		int port = ArrangeConfig(out UpdateSourceConfig launcherConfig, out UpdateSourceConfig gameConfig);
		await InstallConfig(launcherConfig, gameConfig);

		GameInstall.MockGameInstall("v1");

		using var stopHandle = new MockServer.StopHandle();
		await Start404Server(port, stopHandle);

		await new TestScheduler().With(async scheduler =>
		{
			using var vm = new MainWindowViewModel(null);

			await Task.Delay(TimeSpan.FromMilliseconds(500)); // seems to be needed when the debugger is attached
			scheduler.Start();

			Assert.IsFalse(vm.IsDownloadVisible);
			Assert.IsTrue(vm.IsPlayVisible);
			Assert.IsFalse(vm.IsUpdateVisible);
			Assert.AreEqual("Installed version: v1", vm.InstalledVersion);
			Assert.AreEqual("", vm.LatestVersion);
			Assert.AreEqual("The server is unavailable.", vm.StatusText);
			Assert.IsFalse(vm.IsLoading);
		});
	}

	private Task Start404Server(int port, MockServer.StopHandle stopHandle)
	{
		var empty = new UpdateSourceConfig() { ArchiveUrl = "", LatestVersionUrl = "" };
		return StartServer(port, stopHandle, empty, empty, "", "");
	}

	private async Task StartServer(int port, MockServer.StopHandle stopHandle, UpdateSourceConfig launcherConfig, UpdateSourceConfig gameConfig, string launcherVersion, string gameVersion)
	{
		var tcs = new TaskCompletionSource<Unit>();

		var thread = new Thread(() =>
		{
			var server = new MockServer($"http://localhost:{port}/");
			Task runTask = server.Run(stopHandle, context =>
			{
				using HttpListenerResponse response = context.Response;

				if (context.Request.Url?.AbsoluteUri == launcherConfig.ArchiveUrl)
				{
					using FileStream archiveStream = File.OpenRead(Path.Combine("Resources", "SilverShooterLauncher.zip"));
					archiveStream.CopyTo(response.OutputStream);
				}
				else if (context.Request.Url?.AbsoluteUri == gameConfig.ArchiveUrl)
				{
					using FileStream archiveStream = File.OpenRead(Path.Combine("Resources", "SilverShooter.zip"));
					archiveStream.CopyTo(response.OutputStream);
				}
				else if (context.Request.Url?.AbsoluteUri == launcherConfig.LatestVersionUrl)
				{
					response.OutputStream.Write(Encoding.UTF8.GetBytes(launcherVersion));
				}
				else if (context.Request.Url?.AbsoluteUri == gameConfig.LatestVersionUrl)
				{
					response.OutputStream.Write(Encoding.UTF8.GetBytes(gameVersion));
				}
				else
				{
					response.StatusCode = 404;
				}
			});
			tcs.SetResult(new Unit());
			runTask.Wait();
		});
		thread.Start();

		// wait for server to be listening
		await tcs.Task;
	}
}