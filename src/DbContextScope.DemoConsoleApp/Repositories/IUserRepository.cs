using System;
using System.Threading.Tasks;
using DbContextScope.DemoConsoleApp.DomainModel;

namespace DbContextScope.DemoConsoleApp.Repositories {
    public interface IUserRepository {
        User Get(Guid userId);
        Task<User> GetAsync(Guid userId);
        void Add(User user);
    }
}