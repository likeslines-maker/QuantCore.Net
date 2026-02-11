using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortfolioStressLab.Wpf.Services;
using PortfolioStressLab.Wpf.Settings;
using PortfolioStressLab.Wpf.ViewModels;
using System;
using System.IO;
using System.Windows;

namespace PortfolioStressLab.Wpf
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = default!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(cfg);
            services.AddSingleton(cfg.GetSection("Tinkoff").Get<TinkoffSettings>() ?? new TinkoffSettings());
            services.AddSingleton(cfg.GetSection("StressLab").Get<StressLabSettings>() ?? new StressLabSettings());

            services.AddSingleton<TinkoffInvestClient>();
            services.AddSingleton<PortfolioLoader>();
            services.AddSingleton<MarketHistoryLoader>();
            services.AddSingleton<StressCalculator>();

            services.AddSingleton<MainViewModel>();

            Services = services.BuildServiceProvider();
        }
    }
}
