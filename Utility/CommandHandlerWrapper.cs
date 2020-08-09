using System;
using System.Threading.Tasks;

namespace mktool.Utility
{
    static class CommandHandlerWrapper
    {
        public static async Task<int> ExecuteCommandHandler<T>(T options, Func<T, Task> handler)
        {
            try
            {
                await handler(options);
                return 0;
            }
            catch (MktoolException ex)
            {
                return (int)ex.ExitCode;
            }
        }
    }
}
