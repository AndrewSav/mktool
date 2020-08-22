using System;
using System.Threading.Tasks;

namespace mktool
{
    static class CommandHandlerWrapper
    {
        public static async Task<int> ExecuteCommandHandler<T>(T options, Func<T, Task> handler)
        {
            try
            {
                await handler(options);
                return (int)ExitCode.Success;
            }
            catch (MktoolException ex)
            {
                return (int)ex.ExitCode;
            }
        }
    }
}
