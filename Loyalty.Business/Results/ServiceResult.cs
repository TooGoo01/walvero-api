namespace Loyalty.Business.Results
{
    public class ServiceResult
    {
        public object Data { get; }
        public string Message { get; }
        public bool Success { get; }

        // NEW: register kimi idempotent halları bildirmək üçün
        public bool? AlreadyExists { get; }

        // ----- Mövcud imzalar saxlanılıb -----

        public ServiceResult(bool success, object data, string message)
        {
            Data = data;
            Success = success;
            Message = message ?? "";
        }

        public ServiceResult(bool success, string message)
        {
            Data = null!;
            Success = success;
            Message = message ?? "";
        }

        public ServiceResult(bool success, object data)
        {
            Data = data;
            Success = success;
            Message = "";
        }

        public ServiceResult(bool success)
        {
            Data = null!;
            Success = success;
            Message = "";
        }

        // ----- YENİ overload-lar (istəyə bağlı AlreadyExists) -----

        public ServiceResult(bool success, object data, string message, bool? alreadyExists)
        {
            Data = data;
            Success = success;
            Message = message ?? "";
            AlreadyExists = alreadyExists;
        }

        public ServiceResult(bool success, string message, bool? alreadyExists)
        {
            Data = null!;
            Success = success;
            Message = message ?? "";
            AlreadyExists = alreadyExists;
        }

        public ServiceResult(bool success, object data, bool? alreadyExists)
        {
            Data = data;
            Success = success;
            Message = "";
            AlreadyExists = alreadyExists;
        }

        public ServiceResult(bool success, bool? alreadyExists)
        {
            Data = null!;
            Success = success;
            Message = "";
            AlreadyExists = alreadyExists;
        }

        // (Opsional) Rahat fabrik metodları
        public static ServiceResult Ok(object? data = null, string message = "", bool? alreadyExists = null)
            => data is null
                ? new ServiceResult(true, message, alreadyExists)
                : new ServiceResult(true, data, message, alreadyExists);

        public static ServiceResult Fail(string message = "")
            => new ServiceResult(false, message);
    }
}
