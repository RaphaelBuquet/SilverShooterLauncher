using System.Net;

namespace SilverShooterLauncher.Tests;

public class MockServer
{
	private readonly HttpListener listener;

	public class StopHandle : IDisposable
	{
		private readonly CancellationTokenSource cancellationTokenSource = new ();
		public CancellationToken CancellationToken => cancellationTokenSource.Token;

		/// <inheritdoc />
		public void Dispose()
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
		}
	}

	public MockServer(string url)
	{
		listener = new HttpListener();
		listener.Prefixes.Add(url);
		listener.Start();
	}

	public async Task Run(StopHandle handle, Action<HttpListenerContext> onRequest)
	{
		try
		{
			while (!handle.CancellationToken.IsCancellationRequested)
			{
				HttpListenerContext context = await listener.GetContextAsync().WaitAsync(handle.CancellationToken);
				onRequest.Invoke(context);
			}
		}
		catch (OperationCanceledException)
		{
			// silence, this is expected to happen.
		}
		finally
		{
			((IDisposable)listener).Dispose();
		}
	}
}