﻿using Grpc.Core;
using Startrek;
using static Startrek.Transporter;

namespace XamlBrewer.WinUI3.Grpc.Server.Services
{
    public class TransporterService : TransporterBase
    {
        private readonly Random _rnd = new(DateTime.Now.Millisecond);

        public override Task<LifeForm> BeamUp(Location request, ServerCallContext context)
        {
            var whoEver = Data.LifeForms.WhoEver();
            var result = new LifeForm
            {
                Species = whoEver.Item1,
                Name = whoEver.Item2,
                Rank = whoEver.Item3
            };
            
            return Task.FromResult(result);
        }

        public override async Task BeamUpParty(Location request, IServerStreamWriter<LifeForm> responseStream, ServerCallContext context)
        {
            var rnd = _rnd.Next(2, 5);
            for (int i = 0; i < rnd; i++)
            {
                var whoEver = Data.LifeForms.WhoEver();
                var result = new LifeForm
                {
                    Species = whoEver.Item1,
                    Name = whoEver.Item2,
                    Rank = whoEver.Item3
                };

                await responseStream.WriteAsync(result);
            }
        }

        public override Task<Location> BeamDown(LifeForm request, ServerCallContext context)
        {
            // Uncomment to test the client deadline.
            // Task.Delay(60000).Wait();

            return Task.FromResult(new Location
            {
                Description = Data.Locations.WhereEver()
            });
        }

        public override async Task<Location> BeamDownParty(IAsyncStreamReader<LifeForm> requestStream, ServerCallContext context)
        {
            while (await requestStream.MoveNext())
            {
                // ...
            }

            return new Location
            {
                Description = Data.Locations.WhereEver()
            };
        }

        public async override Task ReplaceParty(IAsyncStreamReader<LifeForm> requestStream, IServerStreamWriter<LifeForm> responseStream, ServerCallContext context)
        {
            while (await requestStream.MoveNext())
            {
                // var beamedUp = requestStream.Current;
                // ...

                var beamDown = Data.LifeForms.WhoEver();
                await responseStream.WriteAsync(new LifeForm
                {
                    Species = beamDown.Item1,
                    Name = beamDown.Item2,
                    Rank = beamDown.Item3
                });
            }
        }
    }
}