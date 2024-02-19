namespace LauncherLogic;

public class LoadingStack(IObserver<bool> isLoading)
{
	public readonly IObserver<bool> IsLoading = isLoading;
	public int Count;

	public Handle Acquire()
	{
		int newCount = Interlocked.Increment(ref Count);
		if (newCount == 1)
		{
			IsLoading.OnNext(true);
		}
		return new Handle()
		{
			Owner = this
		};
	}

	public struct Handle : IDisposable
	{
		public required LoadingStack Owner;

		/// <inheritdoc />
		public void Dispose()
		{
			int newCount = Interlocked.Decrement(ref Owner.Count);
			if (newCount == 0)
			{
				Owner.IsLoading.OnNext(false);
			}
		}
	}
}