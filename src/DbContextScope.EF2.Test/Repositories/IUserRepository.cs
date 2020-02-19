using System;
using System.Threading.Tasks;
using EntityFrameworkCore.DbContextScope.Test.DomainModel;

namespace EntityFrameworkCore.DbContextScope.Test.Repositories {
    public interface IUserRepository {
        User Get(Guid userId);
        Task<User> GetAsync(Guid userId);
        void Add(User user);
    }
}