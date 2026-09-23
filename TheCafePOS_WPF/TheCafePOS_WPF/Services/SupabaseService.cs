using System;
using System.Threading.Tasks;
using Supabase;

namespace TheCafePOS_WPF.Services
{
    public class SupabaseService
    {
        // Supabase configuration
        public static readonly string Url = Environment.GetEnvironmentVariable("THECAFEPOS_SUPABASE_URL") ?? "";
        public static readonly string Key = Environment.GetEnvironmentVariable("THECAFEPOS_SUPABASE_KEY") ?? "";

        private static SupabaseService? _instance;
        public static SupabaseService Instance => _instance ??= new SupabaseService();

        public Client Client { get; private set; }
        public bool IsInitialized { get; private set; }

        private SupabaseService()
        {
            if (string.IsNullOrWhiteSpace(Url) || string.IsNullOrWhiteSpace(Key))
                throw new InvalidOperationException("Chưa cấu hình THECAFEPOS_SUPABASE_URL và THECAFEPOS_SUPABASE_KEY.");

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true
            };

            Client = new Client(Url, Key, options);
        }

        public async Task InitializeAsync()
        {
            if (!IsInitialized)
            {
                await Client.InitializeAsync();
                IsInitialized = true;
            }
        }
    }
}

