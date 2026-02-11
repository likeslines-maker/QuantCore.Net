using Grpc.Core;
using Grpc.Net.Client;
using PortfolioStressLab.Wpf.Settings;
using System;
using System.Threading.Tasks;
using Tinkoff.InvestApi.V1;

namespace PortfolioStressLab.Wpf.Services
{
    public sealed class TinkoffInvestClient
    {
        private readonly TinkoffSettings _cfg;

        private readonly UsersService.UsersServiceClient _users;
        private readonly OperationsService.OperationsServiceClient _ops;
        private readonly InstrumentsService.InstrumentsServiceClient _instr;
        private readonly MarketDataService.MarketDataServiceClient _md;

        public TinkoffInvestClient(TinkoffSettings cfg)
        {
            _cfg = cfg;

            var token = ResolveToken(cfg);
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Tinkoff token not set. Set TINKOFF_TOKEN env var or Tinkoff:Token in appsettings.json");

            var creds = CallCredentials.FromInterceptor((context, metadata) =>
            {
                metadata.Add("Authorization", $"Bearer {token}");
                return Task.CompletedTask;
            });

            var channelCreds = ChannelCredentials.Create(new SslCredentials(), creds);

            var address = "https://invest-public-api.tinkoff.ru:443";
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                Credentials = channelCreds,
                // Increase gRPC message limits (Options directory can be large)
                MaxReceiveMessageSize = 64 * 1024 * 1024,// 64 MB
                MaxSendMessageSize = 8 * 1024 * 1024 // 8 MB (more than enough)
            });

            _users = new UsersService.UsersServiceClient(channel);
            _ops = new OperationsService.OperationsServiceClient(channel);
            _instr = new InstrumentsService.InstrumentsServiceClient(channel);
            _md = new MarketDataService.MarketDataServiceClient(channel);
        }

        public async Task<GetInfoResponse> GetUserInfoAsync()
        => await _users.GetInfoAsync(new GetInfoRequest());

        public async Task<string> GetPrimaryAccountIdAsync()
        {
            if (!string.IsNullOrWhiteSpace(_cfg.AccountId)) return _cfg.AccountId!;

            var acc = await _users.GetAccountsAsync(new GetAccountsRequest());
            if (acc.Accounts.Count == 0) throw new InvalidOperationException("No accounts returned.");
            return acc.Accounts[0].Id;
        }

        public OperationsService.OperationsServiceClient Ops => _ops;
        public InstrumentsService.InstrumentsServiceClient Instruments => _instr;
        public MarketDataService.MarketDataServiceClient MarketData => _md;

        private static string? ResolveToken(TinkoffSettings cfg)
        {
            var env = Environment.GetEnvironmentVariable("TINKOFF_TOKEN");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            return cfg.Token;
        }
    }
}
