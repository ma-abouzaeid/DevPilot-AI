namespace DevPilotAI.API.Models
{
    public class ApiResponse<T>
    {
        public bool success { get; set; }
        public T? data { get; set; }
        public string? errorCode { get; set; }
        public string? message { get; set; }
    }
}
