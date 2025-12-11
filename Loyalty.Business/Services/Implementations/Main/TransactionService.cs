using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TransactionService : ITransactionService
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITransactionRepository _transactionRepository;

        public TransactionService(ITransactionRepository transactionRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _transactionRepository = transactionRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public Task<ServiceResult> Get(int id)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> GetActive()
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> GetAll()
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> GetByProgramId(int id)
        {
            var respose = await _transactionRepository.GetByCardIdAsync(id);

            var response = _mapper.Map<List<GetTransactionDto>>(respose);
            return new ServiceResult(true, response);
        }

        public Task<ServiceResult> GetLast()
        {
            throw new NotImplementedException();
        }
    }
}
