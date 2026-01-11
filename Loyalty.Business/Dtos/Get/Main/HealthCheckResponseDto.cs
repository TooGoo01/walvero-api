namespace Loyalty.Business.Dtos.Get.Main
{
    public class HealthCheckResponseDto
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TimeSpan Uptime { get; set; }
        public HealthCheckDetails Details { get; set; } = new();
    }

    public class HealthCheckDetails
    {
        public ComponentStatus Database { get; set; } = new();
        public ComponentStatus Authentication { get; set; } = new();
        public MemoryStatus Memory { get; set; } = new();
        public long ResponseTimeMs { get; set; }
    }

    public class ComponentStatus
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public long ResponseTimeMs { get; set; }
    }

    public class MemoryStatus
    {
        public long WorkingSetBytes { get; set; }
        public long GCMemoryBytes { get; set; }
        public string WorkingSetMB { get; set; } = string.Empty;
        public string GCMemoryMB { get; set; } = string.Empty;
    }
}

