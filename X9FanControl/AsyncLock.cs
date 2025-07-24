namespace X9FanControl;

using System;
using System.Threading;
using System.Threading.Tasks;

class AsyncLock
{
	private readonly SemaphoreSlim sem = new(1, 1);

	public async Task Lock(Func<Task> pred)
	{
		await sem.WaitAsync();

		try
		{
			await pred();
		}
		finally
		{
			sem.Release();
		}
	}

	public async Task<T> Lock<T>(Func<Task<T>> pred)
	{
		await sem.WaitAsync();

		try
		{
			return await pred();
		}
		finally
		{
			sem.Release();
		}
	}
}
