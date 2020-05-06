using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs.Http;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using System;
using DotNetty.Common;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DotNetty.Transport.Bootstrapping;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using HttpServer;

namespace proxyserver
{
    public class HandleProxy
    {
        public static ConcurrentQueue<Channel> freeChannels = new ConcurrentQueue<Channel>();


        public static Bootstrap upStream = null;
        static public ConcurrentDictionary<IChannel, Channel> upChannels = new ConcurrentDictionary<IChannel, Channel>();
        static public ConcurrentDictionary<IHttpRequest, IChannelHandlerContext> requests = new ConcurrentDictionary<IHttpRequest, IChannelHandlerContext>();
        static HandleProxy()
        {
            upChannels = new ConcurrentDictionary<IChannel, Channel>();
            requests = new ConcurrentDictionary<IHttpRequest, IChannelHandlerContext>();
            freeChannels = new ConcurrentQueue<Channel>();
        }
        public static async Task AddRequest(IChannelHandlerContext ctx, IHttpRequest request, RequestHandler handler)
        {

            try
            {
                if (!requests.ContainsKey(request))
                {
                    bool success = requests.TryAdd(request, ctx);
                }
                await DoRequest(await GetOrCreateChannel(ctx, handler));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
                //  Close(ctx);
            }

        }

        public static async Task<Channel> GetOrCreateChannel(IChannelHandlerContext requestCtx, RequestHandler handler)
        {
            Channel Ch = null;

            bool success = freeChannels.TryDequeue(out Ch);
            if (success)
            {
                Ch.Busy = true;
                Ch.request = requestCtx;
                Ch.Handler = handler;
            }
            else
            {
                try
                {
                    //Console.WriteLine("open new connection");
                    var channel = await upStream.ConnectAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 80)).ConfigureAwait(false);
                    var ch = new Channel()
                    {
                        upstream = channel,
                        Busy = true,
                        request = requestCtx,
                        Handler = handler
                    };
                    Ch = ch;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return Ch;

        }
        public static async Task DoRequest(Channel channel)
        {
            DefaultFullHttpRequest req = null;

            req = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, "/");
            req.Headers.Add(AsciiString.Cached("Host"), AsciiString.Cached("localhost"));
            req.Headers.Add(AsciiString.Cached("Connection"), AsciiString.Cached("Keep-Alive"));
            // Console.WriteLine("send get");
            channel.Busy = true;

            if (!upChannels.ContainsKey(channel.upstream))
            {
                upChannels.TryAdd(channel.upstream, channel);
            }
            Task.Run(async () =>
            {
                try
                {
                    await channel.upstream.WriteAndFlushAsync(req).ConfigureAwait(false);

                    // Console.WriteLine("send done");
                }
                catch (Exception ex)
                {
                    await channel.request.DisconnectAsync();
                    //Console.WriteLine("close connection");
                    Channel old = null;
                    upChannels.TryRemove(channel.upstream, out old);
                    Console.WriteLine(ex.Message);
                }

            });

        }

        public static async Task OnEnd(IChannelHandlerContext ctx, object message)
        {
            try
            {
                Channel channel = null;
                bool success = upChannels.TryGetValue(ctx.Channel, out channel);
                if (success)
                {
                    var oldContext = channel;
                    var response = new DefaultFullHttpResponse(DotNetty.Codecs.Http.HttpVersion.Http11, HttpResponseStatus.OK, Unpooled.Empty, false);
                    HttpHeaders headers = response.Headers;
                    headers.Set(ContentTypeEntity, TypePlain);
                    headers.Set(ServerEntity, ServerName);
                    int StaticPlaintextLen = 0;
                    headers.Set(ContentLengthEntity, AsciiString.Cached($"{StaticPlaintextLen}"));
                    oldContext.request.WriteAndFlushAsync(message as IFullHttpResponse).ConfigureAwait(false);
                    freeChannels.Enqueue(channel);

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
            }
        }
        static readonly byte[] StaticPlaintext = Encoding.UTF8.GetBytes("Hello, World!");
        static readonly int StaticPlaintextLen = StaticPlaintext.Length;
        static readonly IByteBuffer PlaintextContentBuffer = Unpooled.UnreleasableBuffer(Unpooled.DirectBuffer().WriteBytes(StaticPlaintext));
        static readonly AsciiString PlaintextClheaderValue = AsciiString.Cached($"{StaticPlaintextLen}");
        // static readonly AsciiString JsonClheaderValue = AsciiString.Cached($"{JsonLen()}");

        static readonly AsciiString TypePlain = AsciiString.Cached("text/plain");
        static readonly AsciiString TypeJson = AsciiString.Cached("application/json");
        static readonly AsciiString ServerName = AsciiString.Cached("Netty");
        static readonly AsciiString ContentTypeEntity = HttpHeaderNames.ContentType;
        static readonly AsciiString DateEntity = HttpHeaderNames.Date;
        static readonly AsciiString ContentLengthEntity = HttpHeaderNames.ContentLength;
        static readonly AsciiString ServerEntity = HttpHeaderNames.Server;
        volatile ICharSequence date = Cache.Value;
        static readonly ThreadLocalCache Cache = new ThreadLocalCache();
        //static int JsonLen() => Encoding.UTF8.GetBytes(NewMessage().ToJsonFormat()).Length;

        sealed class ThreadLocalCache : FastThreadLocal<AsciiString>
        {
            protected override AsciiString GetInitialValue()
            {
                DateTime dateTime = DateTime.UtcNow;
                return AsciiString.Cached($"{dateTime.DayOfWeek}, {dateTime:dd MMM yyyy HH:mm:ss z}");
            }
        }
    }
}
