using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;

#if NETCOREAPP2_0 || NET461
using Microsoft.EntityFrameworkCore.Infrastructure;
#endif

#if NETCOREAPP3_0
using Microsoft.EntityFrameworkCore.Internal;
#endif

namespace DbContextScope.Extensions
{
    public static class DbContextExtensions
    {
        /// <summary>
        /// Convenience method to get the <see cref="IStateManager"/>
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IStateManager GetStateManager(this DbContext context)
        {
            // seems to work for both frameworks
            // v2.2.6
            // v3.1.1
            return context.GetDependencies().StateManager;
#if NETCOREAPP2_0 || NET461
            return context.ChangeTracker.GetInfrastructure();
#endif

#if NETCOREAPP3_0
            return context.GetDependencies().StateManager;
#endif
        }
    }
}
