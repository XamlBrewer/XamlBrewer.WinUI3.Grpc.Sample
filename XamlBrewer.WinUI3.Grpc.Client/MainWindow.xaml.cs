using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Grpc.Net.Client.Balancer;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Startrek;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Startrek.Transporter;

namespace XamlBrewer.WinUI3.Grpc.Client
{
    public sealed partial class MainWindow : Window
    {
        private ChannelBase _channel;
        private TransporterClient _client;

        private readonly Random _rnd = new(DateTime.Now.Millisecond);
        private DispatcherTimer _heartBeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };

        private bool _isPowerOn;
        private bool _isBeamingUp = true;
        private bool _isSingleTarget = true;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public MainWindow()
        {
            Title = "XAML Brewer WinUI 3 gRPC Client Sample";

            InitializeComponent();
            AllocConsole();

            _heartBeatTimer.Tick += HeartBeatTimer_Tick;

            WriteLog("Transporter Room Panel Startup.");
        }

        private void OpenChannel()
        {
            WriteLog("Opening a channel.");
            _channel = new Channel("localhost:5175", ChannelCredentials.Insecure);

            // Optional: deadline.
            // Uncomment the delay in Server Program.cs to test this.
            // await _channel.ConnectAsync(deadline: DateTime.UtcNow.AddSeconds(2));

            WriteLog("- Channel open.");

            _heartBeatTimer.Start();

            _client = new TransporterClient(_channel);
        }

        // Requires gRPC.Net.Client
        private void OpenLoadBalancingChannel()
        {
            WriteLog("Opening a channel.");

            var factory = new StaticResolverFactory(addr => new[]
            {
                new BalancerAddress("localhost", 7175),
                new BalancerAddress("localhost", 5175)
            });

            var services = new ServiceCollection();
            services.AddSingleton<ResolverFactory>(factory);

            var loggerFactory = LoggerFactory.Create(logging =>
            {
                logging.AddConsole();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Debug);
            });

            _channel = GrpcChannel.ForAddress(
                "static:///transporter-host",
                new GrpcChannelOptions
                {
                    Credentials = ChannelCredentials.Insecure,
                    ServiceProvider = services.BuildServiceProvider(),
                    LoggerFactory = loggerFactory,
                    ServiceConfig = new ServiceConfig { LoadBalancingConfigs = { new RoundRobinConfig() } }
                });

            WriteLog("- Channel open.");

            _heartBeatTimer.Start();

            _client = new TransporterClient(_channel);
        }

        private void HeartBeatTimer_Tick(object sender, object e)
        {
            var client = new Health.HealthClient(_channel);

            try
            {
                var response = client.Check(new HealthCheckRequest());
                WriteLog($"*** Transporter service status: {response.Status}.");
            }
            catch (Exception ex)
            {
                WriteLog($"*** Transporter service error: {ex.Message}.");
            }
        }

        private void BeamUpOne()
        {
            var location = new Location
            {
                Description = Data.Locations.WhereEver()
            };

            var lifeForm = _client.BeamUp(location);

            WriteLog($"Beamed up {lifeForm.Rank} {lifeForm.Name} ({lifeForm.Species}) from {location.Description}.");
        }

        private async Task BeamDownOne()
        {
            var whoEver = Data.LifeForms.WhoEver();
            var lifeForm = new LifeForm
            {
                Species = whoEver.Item1,
                Name = whoEver.Item2,
                Rank = whoEver.Item3
            };

            try
            {
                // Uncomment the delay in the Service method to test the deadline.
                var location = await _client.BeamDownAsync(lifeForm, deadline: DateTime.UtcNow.AddSeconds(5));

                WriteLog($"Beamed down {lifeForm.Rank} {lifeForm.Name} ({lifeForm.Species}) to {location.Description}.");
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                WriteLog("!!! Beam down timeout.");
            }
        }

        private async Task BeamUpParty()
        {
            var location = new Location
            {
                Description = Data.Locations.WhereEver()
            };

            WriteLog($"Beaming up a party from {location.Description}.");

            using (var lifeForms = _client.BeamUpParty(location))
            {
                while (await lifeForms.ResponseStream.MoveNext())
                {
                    var lifeForm = lifeForms.ResponseStream.Current;
                    WriteLog($"- Beamed up {lifeForm.Rank} {lifeForm.Name} ({lifeForm.Species}).");
                }
            }

            WriteLog($"- Party beamed up.");
        }

        private async void BeamDownParty()
        {
            var rnd = _rnd.Next(2, 5);
            var lifeForms = new List<LifeForm>();
            for (int i = 0; i < rnd; i++)
            {
                var whoEver = Data.LifeForms.WhoEver();
                var lifeForm = new LifeForm
                {
                    Species = whoEver.Item1,
                    Name = whoEver.Item2,
                    Rank = whoEver.Item3
                };

                lifeForms.Add(lifeForm);
            }

            WriteLog($"Beaming down a party.");

            using (var call = _client.BeamDownParty())
            {
                foreach (var lifeForm in lifeForms)
                {
                    await call.RequestStream.WriteAsync(lifeForm);
                    WriteLog($"- Beamed down {lifeForm.Rank} {lifeForm.Name} ({lifeForm.Species}).");
                }

                await call.RequestStream.CompleteAsync();

                var location = await call.ResponseAsync;

                WriteLog($"- Party beamed down to {location.Description}.");
            }
        }

        private async void ReplaceParty()
        {
            // Creating a party.
            var rnd = _rnd.Next(2, 5);
            var lifeForms = new List<LifeForm>();
            for (int i = 0; i < rnd; i++)
            {
                var whoEver = Data.LifeForms.WhoEver();
                var lifeForm = new LifeForm
                {
                    Species = whoEver.Item1,
                    Name = whoEver.Item2,
                    Rank = whoEver.Item3
                };

                lifeForms.Add(lifeForm);
            }

            WriteLog($"Replacing a party.");
            DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            using (var call = _client.ReplaceParty())
            {
                var responseReaderTask = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext())
                    {
                        var beamedDown = call.ResponseStream.Current;
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            WriteLog($"- Beamed down {beamedDown.Rank} {beamedDown.Name} ({beamedDown.Species}).");
                        });
                    }
                });

                foreach (var request in lifeForms)
                {
                    await call.RequestStream.WriteAsync(request);
                    WriteLog($"- Beamed up {request.Rank} {request.Name} ({request.Species}).");
                };

                await call.RequestStream.CompleteAsync();

                await responseReaderTask;
            }

            WriteLog($"- Party replaced.");
        }

        private async void CloseChannel()
        {
            WriteLog("Routing all energy to deflector shields.");
            _heartBeatTimer.Stop();
            await _channel.ShutdownAsync();
            WriteLog("- Transporter channel closed.");
        }

        private void WriteLog(string message)
        {
            Log.Text += message + " (stardate " + Stardate + ")" + Environment.NewLine;
            LogScroll.ChangeView(0, double.MaxValue, 1);
        }

        private string Stardate => DateTime.Now.ToString("mm:ss.fff");

        private void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            _isPowerOn = !_isPowerOn;

            if (_isPowerOn)
            {
                //OpenChannel();
                OpenLoadBalancingChannel();
                PowerButton.Content = "On";
            }
            else
            {
                CloseChannel();
                PowerButton.Content = "Off";
            }
        }

        private void DirectionButton_Click(object sender, RoutedEventArgs e)
        {
            _isBeamingUp = !_isBeamingUp;

            if (_isBeamingUp)
            {
                WriteLog("Beam direction is up.");
                DirectionButton.Content = "Up";
            }
            else
            {
                WriteLog("Beam direction is down.");

                DirectionButton.Content = "Down";
            }
        }

        private void TargetButton_Click(object sender, RoutedEventArgs e)
        {
            _isSingleTarget = !_isSingleTarget;

            if (_isSingleTarget)
            {
                WriteLog("Locked on single.");

                TargetButton.Content = "Single";
            }
            else
            {
                WriteLog("Locked on party.");

                TargetButton.Content = "Party";
            }
        }

        private async void EnergizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBeamingUp)
            {
                if (_isSingleTarget)
                {
                    BeamUpOne();
                }
                else
                {
                    await BeamUpParty();
                }
            }
            else
            {
                if (_isSingleTarget)
                {
                    await BeamDownOne();
                }
                else
                {
                    BeamDownParty();
                }
            }
        }

        private void PanicButton_Click(object sender, RoutedEventArgs e)
        {
            ReplaceParty();
        }
    }
}
