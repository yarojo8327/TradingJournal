namespace Application.WPF.Common.Helpers;

public static class AsyncHelper
{
    public static void FireAndForget(Task task, Action<Exception>? onError = null)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception is not null)
                onError?.Invoke(t.Exception.InnerException ?? t.Exception);
        }, TaskScheduler.Default);
    }
}
