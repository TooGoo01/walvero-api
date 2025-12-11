using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Repositories;

namespace Loyalty.DataAccess.UnitOfWorks
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<TEntity> Repository<TEntity>() where TEntity : EntityBase;

        void Commit();
    }
}
