// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace HttpServer
{
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using proxyserver;

    class Program
    {
        static Program()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
        }
        public static IChannel ch = null;
        static async Task CreateUpperStream()
        {
            IEventLoopGroup group;
            group = new MultithreadEventLoopGroup();
            var bootstrap = new Bootstrap();
            bootstrap
                .Group(group)
                .Option(ChannelOption.TcpNodelay, true);
            bootstrap.Channel<TcpSocketChannel>();
            bootstrap.Handler(new ActionChannelInitializer<IChannel>(channel =>
            {
                IChannelPipeline pipeline = channel.Pipeline;

                pipeline.AddLast(
                    new HttpResponseDecoder(),
                    new HttpRequestEncoder(),
                    new HttpObjectAggregator(1048576),
                    new TestEventChannelInboundHandlerAdapter()
                    );
            }));
            HandleProxy.upStream = bootstrap;
            //Program.ch = await bootstrap.ConnectAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10001));
        }
        static async Task RunServerAsync()
        {
            await CreateUpperStream();
            Console.WriteLine(
                $"\n{RuntimeInformation.OSArchitecture} {RuntimeInformation.OSDescription}"
                + $"\n{RuntimeInformation.ProcessArchitecture} {RuntimeInformation.FrameworkDescription}"
                + $"\nProcessor Count : {Environment.ProcessorCount}\n");
            bool useLibuv = false;
            Console.WriteLine("Transport type : " + (useLibuv ? "Libuv" : "Socket"));
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            }
            Console.WriteLine($"Server garbage collection: {GCSettings.IsServerGC}");
            Console.WriteLine($"Current latency mode for garbage collection: {GCSettings.LatencyMode}");
            IEventLoopGroup group;
            IEventLoopGroup workGroup;
            group = new MultithreadEventLoopGroup(1);
            workGroup = new MultithreadEventLoopGroup();
            X509Certificate2 tlsCertificate = null;
            try
            {
               var bootstrap = new ServerBootstrap();
               bootstrap.Group(group, workGroup);
               bootstrap.Channel<TcpServerSocketChannel>();
               bootstrap
                    .Option(ChannelOption.SoBacklog, 8192)
                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        if (tlsCertificate != null)
                        {
                            pipeline.AddLast(TlsHandler.Server(tlsCertificate));
                        }
                        pipeline.AddLast("encoder", new HttpResponseEncoder());
                        pipeline.AddLast("decoder", new HttpRequestDecoder(4096, 8192, 8192, false));
                        pipeline.AddLast("handler", new RequestHandler());
                    }));
                IChannel bootstrapChannel = await bootstrap.BindAsync(IPAddress.Any, 9000);
                Console.WriteLine($"Httpd started. Listening on {bootstrapChannel.LocalAddress}");
                Console.ReadLine();
                await bootstrapChannel.CloseAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                group.ShutdownGracefullyAsync().Wait();
            }
        }
        static void Main() => RunServerAsync().Wait();
    }
   
}