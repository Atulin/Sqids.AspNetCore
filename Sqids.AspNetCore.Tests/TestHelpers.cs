namespace Sqids.AspNetCore.Tests;

internal static class TestHelpers
{
	public static Exception Capture(Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			return ex;
		}

		throw new InvalidOperationException("Expected an exception to be thrown.");
	}
}
