using System.Threading.Tasks;

namespace SampleApp
{
    public static class TaskExtensions
    {
        public static Task WaitWithoutExceptionAsync(this Task task)
        {
            return task.ContinueWith(t => { });
        }
    }
}